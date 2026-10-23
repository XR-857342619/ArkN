using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 环境总控：把 ArknEnvPreset 的参数一键应用到目标场景。
/// 携带内容：主光 Directional 子物体 + 本组件（挂 ArknEnvSetup_ro6b5.prefab）。
/// 编辑器：右键组件头 →「应用环境到当前场景」；运行时：applyOnAwake 自动。
/// 不碰地块，纯环境层，删除即无痕。
/// </summary>
public class ArknEnvSetup : MonoBehaviour
{
    public ArknEnvPreset preset;
    [Tooltip("Play 进入时自动应用")]
    public bool applyOnAwake = true;
    [Tooltip("应用时自动禁用场景里其他方向光（避免默认灯与环境包的灯叠加打架、光斑方向错乱）")]
    public bool disableOtherDirectionalLights = true;

    void Awake()
    {
        if (applyOnAwake && preset != null) Apply();
    }

    [ContextMenu("应用环境到当前场景")]
    public void Apply()
    {
        if (preset == null) { Debug.LogWarning("[ArknEnvSetup] 未指定 preset"); return; }

        // ⓪ 禁用场景里其他方向光：环境包的光只与环境配套，默认灯叠加会导致
        //    画面被白灯压住"看起来没反应"、光斑与烘焙图案方向冲突
        if (disableOtherDirectionalLights)
        {
            int off = 0;
            foreach (var l in FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                if (l.transform.IsChildOf(transform)) continue; // 自己的灯不碰
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RecordObject(l, "禁用场景方向光");
#endif
                l.enabled = false;
                off++;
            }
            if (off > 0) Debug.Log($"[ArknEnvSetup] 已禁用场景其他方向光 ×{off}");
        }

        // ① 主光
        var li = GetComponentInChildren<Light>();
        if (li != null)
        {
            li.type = LightType.Directional;
            li.color = preset.lightColor;
            li.intensity = preset.lightIntensity;
            li.shadows = (LightShadows)preset.mainLightShadows;
            li.enabled = preset.mainLightEnabled;
            li.transform.rotation = preset.lightRotation;
            Debug.Log($"[ArknEnvSetup] 主光已配置：颜色 {li.color} 强度 {li.intensity} 角度 {li.transform.eulerAngles}");
        }
        else Debug.LogWarning("[ArknEnvSetup] prefab 里没有 Light 子物体");

        // ② 环境光 —— 逐值复刻 ro6 b-5 成功场景（Ro6B5SceneBuilder 兜底路）：
        // Trilight × 官方 dump 三色渐变 × ambientIntensity(1.4)，AmbientScale=1.0 原值直出。
        // 成功场景的观感就是这套（2021.3 无 customAmbientProbe 绑定，SH27 从未参与过渲染）。
        RenderSettings.ambientMode = AmbientMode.Trilight;
        if (preset.triSky.r + preset.triSky.g + preset.triSky.b > 0f)
        {
            // 官方三色在（b5 口径，主路）
            RenderSettings.ambientSkyColor = preset.triSky;
            RenderSettings.ambientEquatorColor = preset.triEquator;
            RenderSettings.ambientGroundColor = preset.triGround;
            RenderSettings.ambientIntensity = preset.ambientIntensity * Mathf.Max(0f, preset.ambientBoost);
            Debug.Log($"[ArknEnvSetup] 环境光 = Trilight×官方三色（与 ro6 b-5 成功场景逐值一致），intensity={RenderSettings.ambientIntensity:F2}，sky={preset.triSky}");
        }
        else if (preset.sh27 != null && preset.sh27.Length == 27)
        {
            // 兜底：三色缺失时从官方 SH27 的 L0 均匀化（旧 preset 兼容）
            float boost = preset.ambientIntensity * Mathf.Max(0f, preset.ambientBoost);
            var sky = new Color(preset.sh27[0], preset.sh27[9], preset.sh27[18]) * boost;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky;
            RenderSettings.ambientGroundColor = sky * 0.85f;
            RenderSettings.ambientIntensity = 1f;
            Debug.Log($"[ArknEnvSetup] 环境光 = Trilight×SH27-L0（preset 未填官方三色）：{sky}");
        }
        else
        {
            Debug.LogWarning("[ArknEnvSetup] preset 既无三色也无 SH27，环境光未改");
        }

        // ③ 内置雾关闭（避免与高度雾叠加）
        if (preset.disableBuiltinFog) RenderSettings.fog = false;

        // ④ 高度雾挂主相机
        if (preset.applyFog)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var fog = cam.GetComponent<ArknHeightFog>();
                if (fog == null) fog = cam.gameObject.AddComponent<ArknHeightFog>();
                fog.fogColor = preset.fogColor;
                fog.fogBaseline = preset.fogBaseline;
                fog.fogTop = preset.fogTop;
                fog.density = preset.density;
                fog.fogSpeed = preset.fogSpeed;
            }
            else Debug.LogWarning("[ArknEnvSetup] 场景无 Camera.main（未标记 MainCamera），雾未挂");
        }

        // ⑤ 引擎光照贴图自动补挂：已彻底移除（2026-09-26 定案）。引擎路（lightmapIndex 分配）与
        //    材质手动 _LightmapTex 是两条独立叠加的亮度来源（Tuanjie 特性行为），同时存在 =
        //    双重烘焙 → 过曝（实测：地图经打包/导入后 203 个地块被自动挂槽 0 → 过曝复发）。
        //    导出材质已带 _LightmapTex+uv2，手路独立成立，补挂不再需要。
        //    确需手动补挂（缺手路数据的旧资产兜底）：菜单 Tools/地图环境/给选中物体挂引擎Lightmap，
        //    或代码调 EnsureEngineLightmaps()；撤销用 Tools/地图环境/清除引擎Lightmap分配。

        Debug.Log($"[ArknEnvSetup] 已应用环境：{preset.name}（主光{(preset.mainLightEnabled ? "开" : "关")}，雾{(preset.applyFog ? "开" : "关")}）");
    }

    // ===================== 引擎光照贴图共享工具（静态，仅手动调用） =====================
    // 2026-09-26：Apply 自动补挂、UI_MapBuilder 保存钩子、GridSkinData 进图钩子、
    // 延迟一帧 Runner（RequestEngineLightmapPass）均已删除——补挂自动触发 = 双重烘焙过曝根因。
    // 仅保留 EnsureEngineLightmaps 供编辑器菜单/显式代码手动兜底调用。

    [Tooltip("运行时兜底：comp_light 图集直接引用（打包后没有 AssetDatabase，只能靠这个字段）。留空则编辑器下按名自动搜索工程里的 Lightmap-0_comp_light。")]
    public Texture2D compLightAtlas;

    static bool s_atlasWarned;  // 找不到图集只警告一次，避免刷屏

    /// <summary>同步版：图集为空则先挂 comp_light 到槽 0，再把所有未分配 Renderer（-1/65535）补挂到槽 0。</summary>
    public static void EnsureEngineLightmaps()
    {
        var lms = LightmapSettings.lightmaps;
        if (lms == null || lms.Length == 0)
        {
            var atlas = FindCompLightAtlas();
            if (atlas != null)
            {
                LightmapSettings.lightmaps = new[] { new LightmapData { lightmapColor = atlas, lightmapDir = null, shadowMask = null } };
                LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
                lms = LightmapSettings.lightmaps;
                Debug.Log($"[ArknEnvSetup] 已自动挂载官方 comp_light 图集到槽 0（{atlas.name}）。");
            }
        }
        if (lms == null || lms.Length == 0)
        {
            if (!s_atlasWarned)
            {
                s_atlasWarned = true;
                Debug.LogWarning("[ArknEnvSetup] LightmapSettings 为空且找不到 Lightmap-0_comp_light 图集（可把它拖进 ArknEnvSetup 组件的 compLightAtlas 槽），跳过引擎补挂。");
            }
            return;
        }
        int n = 0;
        foreach (var r in FindObjectsOfType<Renderer>())
        {
            // 未分配 = -1（本引擎场景重载后的默认值，见 LightmapBootstrap 注释）或 65535；
            // 已挂到有效槽位（0~254）的（builder 原物体）不动
            if (r.lightmapIndex != -1 && r.lightmapIndex != 65535) continue;
            // 只补烘焙 shader（Arkn/LightmapBaked）的 Renderer。本引擎对任何 shader 都套引擎路
            //（自定义 shader 也吃引擎图集亮度），把单位/特效/普通材质指到图集上 = uv2 随机采样亮区 → 过曝。
            if (!HasLightmapBakedMat(r)) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(r, "补挂引擎Lightmap");
#endif
            r.lightmapIndex = 0;
            r.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
            n++;
        }
        Debug.Log($"[ArknEnvSetup] 引擎 Lightmap 补挂：{n} 个烘焙 Renderer → 槽 0（场景共 {lms.Length} 槽）。");
    }

    /// <summary>该 Renderer 的材质里是否存在 Arkn/LightmapBaked（唯一该吃引擎烘焙路的 shader）。</summary>
    static bool HasLightmapBakedMat(Renderer r)
    {
        var mats = r.sharedMaterials;
        if (mats == null) return false;
        foreach (var m in mats)
            if (m != null && m.shader != null && m.shader.name.Contains("LightmapBaked")) return true;
        return false;
    }

    /// <summary>找 comp_light 图集：优先 ArknEnvSetup 组件上手动指定的 compLightAtlas（运行时可用），
    /// 其次编辑器按名搜工程（自动修正导入设置：RGBM 数据纹理 = 不压缩 + sRGB 关）。</summary>
    static Texture2D FindCompLightAtlas()
    {
        var inst = FindObjectOfType<ArknEnvSetup>();
        if (inst != null && inst.compLightAtlas != null) return inst.compLightAtlas;
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("Lightmap-0_comp_light t:Texture2D");
        if (guids.Length > 0)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            if (UnityEditor.AssetImporter.GetAtPath(p) is UnityEditor.TextureImporter imp &&
                (imp.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed || imp.sRGBTexture))
            {
                imp.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
                imp.sRGBTexture = false;
                imp.SaveAndReimport();
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
#endif
        return null;
    }
}
