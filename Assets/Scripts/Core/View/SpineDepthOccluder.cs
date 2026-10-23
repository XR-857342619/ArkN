using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 高台地块的「深度代理」。
///
/// 背景：Spine 单位由 SpineOverlayCamera 单独渲染（Clear = Nothing），它的深度缓冲里只有
/// DeviceDepthCamera 写进去的「装置深度」。所以只有 Device 层的东西能挡住 Spine。
///
/// 想把高台地块也变成遮挡物，最直接的做法是把地块摆到 Device/high 层，但那会连累：
///   - 地块 Collider 换层后，MapManager / BattleCamera 里写的 Physics.Raycast(..., 1<<0)
///     和 Main Camera 上 PhysicsRaycaster 的 eventMask 都会拾取不到地块 → 点击/放人/刷地皮全废；
///   - 地块也跟着离开主相机着色通道，需要额外处理。
///
/// 因此这里采用「代理」方案：不改原物体，只在其每个 MeshRenderer 的 Transform 下挂一份
/// 同网格、同变换、材质为 Custom/DeviceDepthOnly（ColorMask 0）的副本，放到 high 层。high 层被 SpineOverlayCameraUtil 从主相机
/// 剔除、并加入 DeviceDepthCamera 的 cullingMask —— 于是代理只在深度通道里画（被替换成
/// Custom/DeviceDepthOnly，ColorMask 0），既不影响颜色、也不影响任何物理与拾取。
///
/// 用法：
///   1. 造图构建的格子：MapGrid.AutoBuild() 里对 FarAttackGrid（高台）自动调用；
///   2. 场景里手工摆放的高台：给该物体挂 SpineDepthOccluder 组件即可；
///   3. 也可以代码里直接调用 SpineDepthOccluderUtil.Build(任意GameObject)。
/// 总开关：FeatureFlags.HighTileDepthOcclusion。
/// </summary>
public static class SpineDepthOccluderUtil
{
    public const string ProxySuffix = "__DepthProxy";

    /// <summary>代理用的"只写深度"材质（ColorMask 0）。万一主相机没来得及剔除 high 层，
    /// 代理也画不出颜色，不会出现重影。</summary>
    static Material _depthOnlyMat;
    static bool _depthOnlyMatSearched;

    static Material GetDepthOnlyMaterial()
    {
        if (_depthOnlyMatSearched) return _depthOnlyMat;
        _depthOnlyMatSearched = true;
        var sh = Shader.Find("Custom/DeviceDepthOnly");
        if (sh != null) _depthOnlyMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        return _depthOnlyMat;
    }

    /// <summary>为 root 下所有 MeshRenderer 生成深度代理；已生成过则跳过。返回是否新建了代理。</summary>
    public static bool Build(GameObject root)
    {
        if (root == null) return false;
        if (!FeatureFlags.HighTileDepthOcclusion) return false;

        int layer = LayerMask.NameToLayer(SpineOverlayCameraUtil.OccluderLayerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[SpineDepthOccluder] 缺少渲染层「{SpineOverlayCameraUtil.OccluderLayerName}」，高台无法遮挡 Spine。");
            return false;
        }

        bool created = false;
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var mr = renderers[i];
            if (mr == null) continue;
            // 跳过代理自身，避免无限套娃
            if (mr.gameObject.name.EndsWith(ProxySuffix)) continue;

            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            string proxyName = mr.gameObject.name + ProxySuffix;
            if (mr.transform.Find(proxyName) != null) continue; // 幂等

            var proxy = new GameObject(proxyName);
            proxy.layer = layer;
            proxy.transform.SetParent(mr.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            var proxyFilter = proxy.AddComponent<MeshFilter>();
            proxyFilter.sharedMesh = mf.sharedMesh;

            var proxyRenderer = proxy.AddComponent<MeshRenderer>();
            var depthMat = GetDepthOnlyMaterial();
            if (depthMat != null)
            {
                var mats = new Material[mf.sharedMesh.subMeshCount > 0 ? mf.sharedMesh.subMeshCount : 1];
                for (int m = 0; m < mats.Length; m++) mats[m] = depthMat;
                proxyRenderer.sharedMaterials = mats;
            }
            else
            {
                proxyRenderer.sharedMaterials = mr.sharedMaterials;
            }
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.lightProbeUsage = LightProbeUsage.Off;
            proxyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            proxyRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            proxyRenderer.allowOcclusionWhenDynamic = false;

            created = true;
        }

        // 让相机剔除 mask 立即生效（避免首帧代理被主相机着色画出重影）
        if (created) SpineOverlayCameraUtil.Refresh();

        return created;
    }

    /// <summary>销毁 root 下已生成的深度代理。</summary>
    public static void Clear(GameObject root)
    {
        if (root == null) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = all.Length - 1; i >= 0; i--)
        {
            var t = all[i];
            if (t != null && t.name.EndsWith(ProxySuffix))
            {
                if (Application.isPlaying) Object.Destroy(t.gameObject);
                else Object.DestroyImmediate(t.gameObject);
            }
        }
    }
}

/// <summary>
/// 挂在场景里手工摆放的高台/遮挡物上：Awake 时自动生成深度代理，让 Spine 单位被它遮挡。
/// 不需要手动改 Layer，也不需要挂到 MapGrid 上。
/// </summary>
[DisallowMultipleComponent]
public class SpineDepthOccluder : MonoBehaviour
{
    private void Awake()
    {
        SpineDepthOccluderUtil.Build(gameObject);
    }
}
