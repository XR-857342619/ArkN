// ============================================================
// ArkFxDiag - 方舟特效运行时诊断器
// 把一个特效实例的全部渲染事实导出成文本报告：
//   节点激活状态/缩放、MeshFilter 网格、Renderer 材质的
//   全部属性（贴图是否为 NULL、wrapMode、tiling/offset、颜色、
//   数值）、粒子系统状态、Animator 状态、Missing Script 计数。
// 用途：排查"特效看不见/不动/太亮"这类问题——报告里
//   "!! NULL !!"/enabled=False/scale=0 就是答案。
// ============================================================
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArkFxDiag
{
    public static string Dump(GameObject root)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"==== ArkFxDiag @ {System.DateTime.Now:HH:mm:ss} | root={root.name} ====");
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.AppendLine();
            sb.AppendLine($"[节点] {PathOf(t, root.transform)} | activeInHierarchy={t.gameObject.activeInHierarchy} | localScale={t.localScale}");

            int missing = 0;
            foreach (var c in t.gameObject.GetComponents<Component>())
                if (c == null) missing++;
            if (missing > 0) sb.AppendLine($"  !! Missing Script x{missing}（原版脚本占位）");

            foreach (var mf in t.GetComponents<MeshFilter>())
                sb.AppendLine($"  MeshFilter: mesh={(mf.sharedMesh != null ? mf.sharedMesh.name + " (" + mf.sharedMesh.vertexCount + " verts)" : "!! NULL——网格没加载，必然看不见 !!")}");

            foreach (var r in t.GetComponents<Renderer>())
            {
                sb.AppendLine($"  {r.GetType().Name}: enabled={r.enabled} | isVisible={r.isVisible} | bounds={r.bounds.center} r={r.bounds.extents}");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    DumpMaterial(sb, i, mats[i]);
            }

            foreach (var ps in t.GetComponents<ParticleSystem>())
                DumpPs(sb, ps);

            foreach (var an in t.GetComponents<Animator>())
                DumpAnim(sb, an);

            // ArkFx 替身组件：UV 验收的关键信息都在这里
            foreach (var b in t.GetComponents<ArkFxBehaviour>())
                DumpBehaviour(sb, b, t);

            var hostC = t.GetComponent<ArkFxEffectHost>();
            if (hostC != null)
                sb.AppendLine($"  [ArkFx] ArkFxEffectHost finished={hostC.Finished} paused={hostC.Paused} isBusy={hostC.IsBusy}");

            var v5C = t.GetComponent<ArkFxUVTween>();
            if (v5C != null)
                sb.AppendLine($"  [ArkFx] ArkFxUVTween(v5) mainSpeed={v5C.mainSpeed} protectMainUV={v5C.protectMainUV} duration={v5C.duration:F2} extras={v5C.extras?.Count ?? 0}");

            var stubC = t.GetComponent<ArkFxScriptStub>();
            if (stubC != null)
                sb.AppendLine($"  [ArkFx] ArkFxScriptStub 原版类名={stubC.className}");
        }
        sb.AppendLine();
        sb.AppendLine("==== ArkFxDiag end ====");
        return sb.ToString();
    }


    /// <summary>
    /// 打印 ArkFx 替身组件的参数与「当前实际生效值」。
    /// UV 验收就是看这里的 offset 有没有随时间变化。
    /// </summary>
    static void DumpBehaviour(StringBuilder sb, ArkFxBehaviour b, Transform owner)
    {
        if (b == null) return;
        sb.AppendLine($"  [ArkFx] {b.GetType().Name} enabled={b.enabled} playbackSpeed={b.PlaybackSpeed}");

        var rot = b as ArkFxUVRotation;
        if (rot != null)
        {
            sb.AppendLine($"      propertyName={rot.propertyName}");
            sb.AppendLine($"      rotateTex=[{rot.rotateTex1},{rot.rotateTex2},{rot.rotateTex3},{rot.rotateTex4}]");
            sb.AppendLine($"      angle=[{rot.angle1:F1},{rot.angle2:F1},{rot.angle3:F1},{rot.angle4:F1}]");
            var r = owner.GetComponent<Renderer>();
            if (r != null)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                int id = Shader.PropertyToID("_HG_UV_ROTATION");
                sb.AppendLine($"      当前 _HG_UV_ROTATION={mpb.GetFloat(id):F2}");
                for (int i = 0; i < 4; i++)
                {
                    int rid = Shader.PropertyToID((rot.propertyName ?? "_Rotation") + i);
                    sb.AppendLine($"      当前 {(rot.propertyName ?? "_Rotation") + i}={mpb.GetVector(rid)}");
                }
            }
            return;
        }

        var tw6 = b as ArkFxUVTweenV6;
        if (tw6 != null)
        {
            sb.AppendLine($"      mainSpeed={tw6.mainSpeed} protectMainUV={tw6.protectMainUV} keepInitOffset={tw6.keepInitOffset}");
            sb.AppendLine($"      useSecondMap={tw6.useSecondMap} secondMapName={tw6.secondMapName} secondSpeed={tw6.secondSpeed} protectSecondUV={tw6.protectSecondUV}");
            DumpLiveST(sb, owner, tw6.secondMapName);
            return;
        }

        var adv = b as ArkFxUVTweenAdvanceV6;
        if (adv != null)
        {
            sb.AppendLine($"      mainSpeed={adv.mainSpeed} tweenRepeat={adv.tweenRepeat} tweenAnmTime={adv.tweenAnmTime:F2} keepInitOffset={adv.keepInitOffset}");
            sb.AppendLine($"      useSecondMap={adv.useSecondMap} secondMapName={adv.secondMapName} secondSpeed={adv.secondSpeed} extraMaps={adv.extraMaps?.Count ?? 0}");
            DumpLiveST(sb, owner, adv.secondMapName);
            return;
        }

    }

    /// <summary>打印材质上「当前实际生效」的 ST（mainTextureOffset 与第二贴图 _ST 向量）</summary>
    static void DumpLiveST(StringBuilder sb, Transform owner, string secondMapName)
    {
        var r = owner.GetComponent<Renderer>();
        if (r == null) return;
        var mat = r.sharedMaterial;
        if (mat == null) { sb.AppendLine("      材质为 null，无法读取 ST"); return; }
        var off = mat.mainTextureOffset;
        var scale = mat.mainTextureScale;
        sb.AppendLine($"      当前 _MainTex_ST: offset=({off.x:F4},{off.y:F4}) scale=({scale.x:F2},{scale.y:F2})");
        if (!string.IsNullOrEmpty(secondMapName) && mat.HasProperty(secondMapName + "_ST"))
            sb.AppendLine($"      当前 {secondMapName}_ST={mat.GetVector(secondMapName + "_ST")}");
    }

    static void DumpMaterial(StringBuilder sb, int idx, Material m)
    {
        if (m == null) { sb.AppendLine($"  材质[{idx}]: !! NULL !!"); return; }
        sb.AppendLine($"  材质[{idx}]: {m.name} | shader={m.shader.name} | supported={m.shader.isSupported} | queue={m.renderQueue}");
        var sh = m.shader;
        int n = sh.GetPropertyCount();
        for (int i = 0; i < n; i++)
        {
            var pname = sh.GetPropertyName(i);
            switch (sh.GetPropertyType(i))
            {
                case ShaderPropertyType.Texture:
                    var tex = m.GetTexture(pname);
                    if (tex != null)
                        sb.AppendLine($"    tex {pname}: {tex.name} {tex.width}x{tex.height} wrap={tex.wrapMode} scale={m.GetTextureScale(pname)} offset={m.GetTextureOffset(pname)}");
                    else
                        sb.AppendLine($"    tex {pname}: !! NULL——贴图没加载（查依赖 AB）!!");
                    break;
                case ShaderPropertyType.Color:
                    sb.AppendLine($"    color {pname}: {m.GetColor(pname)}");
                    break;
                case ShaderPropertyType.Vector:
                    sb.AppendLine($"    vec {pname}: {m.GetVector(pname)}");
                    break;
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    sb.AppendLine($"    float {pname}: {m.GetFloat(pname)}");
                    break;
            }
        }
    }

    static void DumpPs(StringBuilder sb, ParticleSystem ps)
    {
        var main = ps.main;
        var em = ps.emission;
        sb.AppendLine($"  ParticleSystem: isPlaying={ps.isPlaying} isEmitting={ps.isEmitting} loop={main.loop} duration={main.duration:F2}s");
        sb.AppendLine($"    startColor({main.startColor.mode})={ColorOf(main.startColor.colorMin)}~{ColorOf(main.startColor.colorMax)} startSize={main.startSize.constant:F2} startLifetime={main.startLifetime.constant:F2} rate={em.rateOverTime.constant:F1} max={main.maxParticles}");
    }

    static void DumpAnim(StringBuilder sb, Animator an)
    {
        string ctrl = an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "!! 无控制器 !!";
        sb.AppendLine($"  Animator: enabled={an.enabled} controller={ctrl}");
        var clips = an.runtimeAnimatorController != null ? an.runtimeAnimatorController.animationClips : null;
        if (clips != null)
            foreach (var c in clips)
                sb.AppendLine($"    clip: {c.name} ({c.length:F2}s)");
    }

    static string ColorOf(Color c) => $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";

    static string PathOf(Transform t, Transform root)
    {
        if (t == root) return t.name;
        var p = t.name;
        var cur = t.parent;
        while (cur != null && cur != root)
        {
            p = cur.name + "/" + p;
            cur = cur.parent;
        }
        return p;
    }
}
