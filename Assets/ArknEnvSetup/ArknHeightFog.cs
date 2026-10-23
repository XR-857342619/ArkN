using UnityEngine;

/// <summary>
/// 高度雾 —— 还原方舟 AE_* 场景效果 profile 的 heightFog。
///
/// 官方参数（Torappu.Rendering.SceneEffectProfile，全库 150 个 ae_* 统计）：
///   fogColor / fogBaseline / fogTop / fogSpeed / fogNoiseScale / noCenterMask
/// 官方密度曲线：σ(y) = clamp((fogBaseline - y) / (fogBaseline - fogTop), 0, 1)
///   y >= fogBaseline → 无雾； y <= fogTop → 满密度（98% 的 profile 满足 baseline > top）
///
/// 实现要点（v3，2026-09-24）：**沿视线做光学厚度积分**，可见雾量 = 1 - exp(-∫σ ds)。
///   v1/v2 的错法：按像素高度直接插值一个权重、再加"基础雾量 minWeight"整屏贴色 →
///   结果就是"一层膜糊在镜头前"，近处和远处一样浓，完全没有景深感。
///   正解是积分：脚下/近处几何路径短 → 清晰；远处/低处视线穿过的雾厚 → 越远越糊。
///   这才能出现官方那种"从楼顶往楼底望去一片雾蒙蒙"的观感。
///
/// 用法：挂在相机上（OnRenderImage 生效，只在 Game 视图可见）；
///      参数由 builder 菜单「④B 高度雾(官方参数)」写入。
/// </summary>
[RequireComponent(typeof(Camera))]
[ExecuteAlways]
[ImageEffectOpaque]   // 不透明几何之后、透明物体之前：雾只压场景，不糊粒子/UI
public class ArknHeightFog : MonoBehaviour
{
    [Header("实验：高度轴切换（官方数据验证过 Y-up，此开关仅为对照实验）")]
    [Tooltip("勾上 = 把 fogBaseline/fogTop 沿世界 Z 轴解释（Z-up 假设）。对照官方观感二选一。")]
    public bool useZAxis = false;
    [Tooltip("深度预通道总开关（诊断用）：不勾 = 相机不设 depthTextureMode，雾会算错但可验证\"开雾后建筑变灰\"是否由预通道引起")]
    public bool useDepthPrepass = true;

    [Header("官方 AE_* 参数（fogColor / fogBaseline / fogTop / fogSpeed）")]
    public Color fogColor = new Color(0.228f, 0.502f, 0.173f, 1f);
    [Tooltip("雾顶高度：世界 Y 在此及以上 = 无雾")]
    public float fogBaseline = 5.8f;
    [Tooltip("浓端高度：世界 Y 在此及以下 = 满密度")]
    public float fogTop = 2.0f;
    public float fogSpeed = 1f;

    [Header("雾浓度（官方字段里没有显式密度，用消光系数表达）")]
    [Tooltip("消光系数（每世界单位）。0.05 ≈ 穿过 30 单位满密度雾 → 约 78% 不透明度")]
    public float density = 0.05f;
    [Tooltip("光学厚度幂次实验：1=标准 Beer-Lambert；2≈官方式 exp2 曲线（近处更清、远处更陡）")]
    [Range(0.5f, 100f)] public float distancePow = 1f;
    [Range(0f, 1f)] public float maxOpacity = 1f;

    [Header("噪声（官方 fogNoise/fogNoiseScale；多数关卡不用）")]
    public bool useNoise = false;
    [Range(0f, 1f)] public float noiseAmount = 0.25f;
    public Vector2 noiseScale = Vector2.one;
    public float noiseSpeed = 1f;

    [Header("中心遮罩（官方 noCenterMask=1 表示不用遮罩；勾上=启用遮罩留中心孔）")]
    public bool useCenterMask = false;
    [Range(0f, 1f)] public float maskAmount = 1f;
    public float maskRadius = 0.35f;
    public float maskSoftness = 0.25f;

    [Header("天空/远平面")]
    [Tooltip("勾上：天空与远平面按 skyDistance 计算雾（背景板是对应几何，一般保持勾选）")]
    public bool fogSky = true;
    public float skyDistance = 200f;

    [Header("调试")]
    [Tooltip("整屏强制上雾（无视深度与高度）——确认组件是否生效；确认后关掉")]
    public bool debugFullScreen = false;
    [Tooltip("把「沿视线的累计雾厚」画成亮度：近处暗、远处亮 = 积分在工作")]
    public bool debugShowDensity = false;
    [Tooltip("把「深度距离」按 log2 分 10 档画成灰阶：整屏同一档最白 = 深度纹理没数据")]
    public bool debugShowDepth = false;

    public Shader shader;
    Material m;

    void OnEnable()
    {
        var cam = GetComponent<Camera>();
        if (cam != null && useDepthPrepass) cam.depthTextureMode |= DepthTextureMode.Depth;
        if (shader == null) shader = Shader.Find("Arkn/HeightFog");
    }

    void Update()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;
        if (!useDepthPrepass)
        {
            // 诊断开关：主动清掉深度标志（若清掉后建筑纹理恢复 = 灰因深度预通道石锤）
            if ((cam.depthTextureMode & DepthTextureMode.Depth) != 0)
            {
                cam.depthTextureMode &= ~DepthTextureMode.Depth;
                Debug.Log($"[ArknHeightFog@{name}] 已清除本相机深度标志（每帧维持清除；若下一条日志又出现'被清掉，已重新设置' = 有另一份组件在抢，去 Hierarchy 搜 ArknHeightFog 全部处理）");
            }
            return;
        }
        // 深度标志守护：若有别的组件（如某些后处理）把 depthTextureMode 整个覆盖清零，
        // _CameraDepthTexture 会采样不到数据 → 所有像素被当成超远 → 雾永远全屏饱和。
        if (cam != null && (cam.depthTextureMode & DepthTextureMode.Depth) == 0)
        {
            cam.depthTextureMode |= DepthTextureMode.Depth;
            Debug.LogWarning("[ArknHeightFog] 检测到相机深度标志被清掉，已重新设置；若本警告反复刷屏 = 场景里有组件在覆盖 depthTextureMode（查 PostProcessLayer 等的赋值方式）");
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // 防呆：相机泡在雾里（Y 低于浓端）时，整条视线都在满密度区，全屏糊是物理正确的，
        // 但容易误判成"实现坏了"。官方观感的前提 = 相机高于 fogBaseline 俯视。
        float warnEvery = 5f; float t = Time.realtimeSinceStartup % warnEvery;
        if (t < 0.1f && transform.position.y < fogTop + 0.5f && !debugFullScreen)
            Debug.LogWarning($"[ArknHeightFog] 相机 Y={transform.position.y:0.0} 低于浓端高度 fogTop={fogTop}（在雾里）→ 全屏糊是正常的。要看官方梯度请把相机升到 Y > fogBaseline={fogBaseline} 俯视，或调低 density。");
        if (debugShowDensity && Time.realtimeSinceStartup % 3f < 0.1f)
            Debug.Log($"[ArknHeightFog] 读数辅助：相机Y={transform.position.y:0.00} 俯角={transform.eulerAngles.x:0}° density={density:0.000} baseline={fogBaseline:0.##} top={fogTop:0.##}（灰阶条带=每档5%雾量；纯俯视Y=10应只有2~4档灰）");
        if (shader == null)
        {
            Debug.LogWarning("[ArknHeightFog] 找不到 shader Arkn/HeightFog → 直通（先看 Console 有没有 shader 编译错误）");
            Graphics.Blit(src, dst);
            return;
        }
        if (m == null || m.shader != shader)
            m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        // 判决开关：density=0 → 完全直通，跳过整个 shader。
        // 若此时画面仍有灰雾 → 灰不是本 shader 画的（查同物体第二个组件 / PostProcess / 另一台相机）。
        if (density <= 0.0001f && !debugFullScreen && !debugShowDensity)
        {
            if (Time.realtimeSinceStartup % 5f < 0.1f)
                Debug.Log("[ArknHeightFog] density=0 → 完全直通（此模式下画面不该有任何雾/灰；若仍有 = 灰来自别处）");
            Graphics.Blit(src, dst);
            return;
        }

        if (Time.realtimeSinceStartup % 3f < 0.1f)
            Debug.Log($"[ArknHeightFog@{name}] 相机Y={transform.position.y:0.00} 俯角={transform.eulerAngles.x:0}° d={density:0.000} full={debugFullScreen} ramp={debugShowDensity} maxOp={maxOpacity:0.00} 同物体组件数={GetComponents<ArknHeightFog>().Length}");

        m.SetColor("_FogColor", fogColor);
        m.SetFloat("_FogBaseline", fogBaseline);
        m.SetFloat("_FogTop", fogTop);
        m.SetFloat("_Density", density);
        m.SetFloat("_DistancePow", distancePow);
        m.SetFloat("_MaxOpacity", maxOpacity);
        m.SetFloat("_UseNoise", useNoise ? 1f : 0f);
        m.SetFloat("_NoiseAmount", noiseAmount);
        m.SetVector("_NoiseScale", new Vector4(noiseScale.x, noiseScale.y, 0f, 0f));
        m.SetFloat("_NoiseSpeed", noiseSpeed);
        m.SetFloat("_UseCenterMask", useCenterMask ? 1f : 0f);
        m.SetFloat("_MaskAmount", maskAmount);
        m.SetFloat("_MaskRadius", maskRadius);
        m.SetFloat("_MaskSoftness", maskSoftness);
        m.SetFloat("_FogSky", fogSky ? 1f : 0f);
        m.SetFloat("_SkyDistance", skyDistance);
        m.SetFloat("_DebugFull", debugFullScreen ? 1f : 0f);
        m.SetFloat("_DebugRamp", debugShowDensity ? 1f : 0f);
        m.SetFloat("_DebugDepth", debugShowDepth ? 1f : 0f);
        m.SetFloat("_AxisZ", useZAxis ? 1f : 0f);
        Graphics.Blit(src, dst, m);
    }

    // 防御：其他组件（如 PostProcessLayer）可能在 OnEnable 里改写 depthTextureMode，
    // 每帧渲染前再补一次，保证深度纹理一定存在。
    void OnPreRender()
    {
        if (!useDepthPrepass) return;
        var cam = GetComponent<Camera>();
        if (cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    [ContextMenu("诊断：场景相机普查")]
    void CensusCameras()
    {
        var cams = FindObjectsOfType<Camera>();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ArknHeightFog] 场景相机普查：共 {cams.Length} 台");
        foreach (var c in cams)
        {
            var fogs = c.GetComponents<ArknHeightFog>();
            var fp = new System.Text.StringBuilder();
            foreach (var f in fogs)
                fp.Append($"(prepass={f.useDepthPrepass} en={f.enabled} d={f.density:0.000} top={f.fogTop:0.##} " +
                          $"baseline={f.fogBaseline:0.##} fogColor={f.fogColor} debugFull={f.debugFullScreen}) ");
            sb.AppendLine(
                $"- '{c.name}' tag={c.tag} enabled={c.enabled} depth={c.depth:0.0} clearFlags={c.clearFlags} " +
                $"mask=0x{c.cullingMask:X8} depthTex={c.depthTextureMode} hdr={c.allowHDR} msaa={c.allowMSAA} " +
                $"path={c.actualRenderingPath}\n    ArknHeightFog×{fogs.Length} {fp}");
        }
        Debug.Log(sb.ToString());
    }

    [ContextMenu("诊断：打印相机状态")]
    void DiagnoseCamera()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) { Debug.LogWarning("[ArknHeightFog] 本物体上没有 Camera 组件！"); return; }
        Debug.Log(
            $"[ArknHeightFog] 相机={cam.name} tag={cam.tag} enabled={cam.enabled}\n" +
            $"pos={cam.transform.position} rot={cam.transform.eulerAngles}\n" +
            $"depthTextureMode={cam.depthTextureMode} near={cam.nearClipPlane} far={cam.farClipPlane}\n" +
            $"clearFlags={cam.clearFlags} targetTexture={(cam.targetTexture == null ? "null(输出到屏幕)" : "RT")}\n" +
            $"shader={(shader == null ? "null" : shader.name)} 支持={(shader == null ? "-" : shader.isSupported.ToString())}",
            cam);
    }

    /// <summary>按"满密度下走 targetDistance 单位达到 targetOpacity"反推一个消光系数，便于快速找平。</summary>
    public void AutoDensity(float targetDistance = 30f, float targetOpacity = 0.78f)
    {
        targetOpacity = Mathf.Clamp(targetOpacity, 0.01f, 0.999f);
        density = -Mathf.Log(1f - targetOpacity) / Mathf.Max(targetDistance, 0.01f);
    }

    [ContextMenu("自动密度：满密度走 30 单位 ≈ 78% 不透明度")]
    void AutoDensityDefault() { AutoDensity(30f, 0.78f); }

    [ContextMenu("自动密度：浓一些（20 单位 ≈ 85%）")]
    void AutoDensityHeavy() { AutoDensity(20f, 0.85f); }

    [ContextMenu("自动密度：淡一些（50 单位 ≈ 70%）")]
    void AutoDensityLight() { AutoDensity(50f, 0.70f); }
}
