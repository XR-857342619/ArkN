using UnityEditor;
using UnityEngine;

/// <summary>
/// ArknEnvSetup 的编辑器扩展：给组件加"应用环境"按钮 + Tools 菜单入口。
/// 解决 ContextMenu 藏在组件标题栏右键里不好找的问题。
/// </summary>
[CustomEditor(typeof(ArknEnvSetup))]
public class ArknEnvSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var setup = (ArknEnvSetup)target;
        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(setup.preset == null))
        {
            if (GUILayout.Button("应用环境到当前场景", GUILayout.Height(28)))
            {
                setup.Apply();
                EditorUtility.SetDirty(setup);
                // 环境光/雾改的是 RenderSettings（场景级），必须标脏才会提示保存
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
        if (setup.preset == null)
            EditorGUILayout.HelpBox("未指定 Preset，按钮不可用。请把 ArknEnvPreset_ro6b5.asset 拖到上面 Preset 字段。", MessageType.Warning);
    }
}

/// <summary>
/// 顶部菜单入口：Tools/地图环境/应用环境到当前场景。
/// 优先用选中的 ArknEnvSetup；没选中时在场景里找（1 个直接用，多个全部应用）。
/// </summary>
public static class ArknEnvSetupMenu
{
    [MenuItem("Tools/地图环境/应用环境到当前场景 %#E", false, 0)]
    static void ApplyFromMenu()
    {
        var setups = Object.FindObjectsOfType<ArknEnvSetup>(true);
        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<ArknEnvSetup>()
            : null;

        int count = 0;
        if (selected != null)
        {
            selected.Apply();
            count = 1;
        }
        else if (setups.Length == 1)
        {
            setups[0].Apply();
            count = 1;
        }
        else if (setups.Length > 1)
        {
            foreach (var s in setups) s.Apply();
            count = setups.Length;
        }

        if (count > 0)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"[ArknEnvSetup] 菜单应用完成：{count} 个环境实例。记得 Ctrl+S 保存场景。");
        }
        else
        {
            Debug.LogWarning("[ArknEnvSetup] 场景里没有 ArknEnvSetup 实例，选中也没带该组件——请先把 ArknEnvSetup_ro6b5.prefab 拖进场景。");
        }
    }

    [MenuItem("Tools/地图环境/应用环境到当前场景 %#E", true)]
    static bool ValidateApply() => true;

    /// <summary>诊断：打印当前场景环境光真实状态。2026-09-25 起本机引擎无 customAmbientProbe 绑定，
    /// Trilight 三色即最终环境光，无需再读探针。</summary>
    [MenuItem("Tools/地图环境/诊断当前场景环境光")]
    static void DiagnoseAmbient()
    {
        var sb = new System.Text.StringBuilder("[环境光诊断]\n");
        sb.AppendLine($"ambientMode = {RenderSettings.ambientMode}（2=Skybox 3=Custom 1=Trilight）");
        sb.AppendLine($"ambientIntensity = {RenderSettings.ambientIntensity}");
        var sky = RenderSettings.ambientSkyColor;
        var eq = RenderSettings.ambientEquatorColor;
        var gd = RenderSettings.ambientGroundColor;
        float k = RenderSettings.ambientIntensity;
        sb.AppendLine($"Trilight 天空 = {sky}  赤道 = {eq}  地面 = {gd}");
        sb.AppendLine($"实际生效环境 ≈ ({sky.r * k:F3}, {sky.g * k:F3}, {sky.b * k:F3})   （官方 SH27 L0 ≈ (0.172, 0.278, 0.382)）");
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Custom)
            sb.AppendLine("⚠ Custom 模式：本机 Tuanjie 2021.3.39f1c1 没有 customAmbientProbe 绑定，探针恒为全 0 → 环境光纯黑。请重新「应用环境」切回 Trilight。");
        sb.AppendLine($"内置雾 = {RenderSettings.fog}  fogColor = {RenderSettings.fogColor}");
        Debug.Log(sb.ToString());
    }

    /// <summary>对比诊断：选中要排查的模型后执行，打印场景里所有方向光的真实状态
    /// + 选中模型用到的每个材质的 _NoBake/_LightmapTex。两个场景各跑一次，贴出来逐行对比。</summary>
    [MenuItem("Tools/地图环境/诊断选中物体光照（对比用）")]
    static void DiagnoseSelectionLighting()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[物体光照诊断] 请先在 Hierarchy 选中要排查的模型根节点"); return; }

        var sb = new System.Text.StringBuilder("[物体光照诊断] 场景=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "\n");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.type != LightType.Directional) continue;
            sb.AppendLine($"灯 {l.name}: enabled={l.enabled} intensity={l.intensity:F2} color={l.color} euler={l.transform.eulerAngles:F1} shadows={l.shadows}");
        }

        var seen = new System.Collections.Generic.HashSet<Material>();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                float nb = m.HasProperty("_NoBake") ? m.GetFloat("_NoBake") : -1f;
                bool hasLm = m.HasProperty("_LightmapTex") && m.GetTexture("_LightmapTex") != null;
                sb.AppendLine($"材质 {m.name}: _NoBake={nb} _LightmapTex={(hasLm ? "有" : "无")} shader={m.shader.name}");
            }
        Debug.Log(sb.ToString());
    }

    /// <summary>uv2 = 烘焙光图采样坐标。导出网格经 Blender FBX 往返后 uv2 可能被丢掉/重生成，
    /// 导致 shader 去场景图集的暗区采样 → 表现为"不吃烘焙"。本菜单打印选中物体每个网格的
    /// uv2 数值范围，与官方原网格一起选中对比，一眼定位。</summary>
    [MenuItem("Tools/地图环境/诊断网格 uv2（烘焙采样坐标）")]
    static void DiagnoseMeshUv2()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[uv2诊断] 请先选中物体。建议把原物体和导出物体一起多选，输出里直接对比。");
            return;
        }
        var sb = new System.Text.StringBuilder("[uv2诊断] 场景=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "\n");
        var seen = new System.Collections.Generic.HashSet<Mesh>();
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !seen.Add(mesh)) continue;
                var uv2 = mesh.uv2;
                var uv1 = mesh.uv;
                sb.AppendLine($"网格 {mesh.name}  顶点={mesh.vertexCount}  uv2数组={uv2.Length}  uv1数组={uv1.Length}");
                if (uv2.Length == mesh.vertexCount && uv2.Length > 0)
                {
                    Vector2 mn = uv2[0], mx = uv2[0]; Vector2 sum = Vector2.zero;
                    foreach (var v in uv2) { mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v); sum += v; }
                    var avg = sum / uv2.Length;
                    sb.AppendLine($"   uv2 min=({mn.x:F4},{mn.y:F4}) max=({mx.x:F4},{mx.y:F4}) 均值=({avg.x:F4},{avg.y:F4})");
                }
                else
                {
                    sb.AppendLine("   ⚠ uv2 数量与顶点数不符或缺失 → shader 拿不到采样坐标，烘焙必然失效");
                }
                if (uv1.Length == mesh.vertexCount && uv1.Length > 0)
                {
                    Vector2 mn = uv1[0], mx = uv1[0];
                    foreach (var v in uv1) { mn = Vector2.Min(mn, v); mx = Vector2.Max(mx, v); }
                    sb.AppendLine($"   uv1 min=({mn.x:F4},{mn.y:F4}) max=({mx.x:F4},{mx.y:F4})");
                }
            }
        Debug.Log(sb.ToString());
    }

    /// <summary>把选中物体的 Renderer 挂到场景引擎光照贴图槽位 0（官方 comp_light 图集），
    /// 全 UV 覆盖 (1,1,0,0)。⚠2026-09-26 定案：引擎路（lightmapIndex 分配）与 shader 手路
    /// （材质 _LightmapTex+uv2）叠加 = 双重烘焙过曝，本菜单仅作缺手路数据旧资产的
    /// 手动兜底，且存图/打包前勿用（分配会随场景固化）。误挂用"清除引擎Lightmap分配"撤销。</summary>
    [MenuItem("Tools/地图环境/给选中物体挂引擎Lightmap")]
    static void AssignEngineLightmap()
    {
        var lms = LightmapSettings.lightmaps;
        if (lms == null || lms.Length == 0)
        {
            Debug.LogWarning("[挂Lightmap] 场景的 LightmapSettings 为空。请先在本场景跑 builder 的「重新挂载官方烘焙光照贴图」菜单，再执行本命令。");
            return;
        }
        int idx = 0; // 槽位 0：builder 重挂的官方 comp_light 图集
        int n = 0;
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                Undo.RecordObject(r, "Assign Engine Lightmap");
                r.lightmapIndex = idx;
                r.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                n++;
            }
        Debug.Log($"[挂Lightmap] 已把 {n} 个 Renderer 挂到引擎槽位 [{idx}]（场景共 {lms.Length} 个槽），scaleOffset=(1,1,0,0)。\n现在看画面亮度是否变化：变 → 引擎路生效；不变 → 引擎路对本 shader 无关。");
    }

    /// <summary>专项诊断：打印场景 LightmapSettings 每个槽位的贴图 + 全场景 Renderer 的
    /// lightmapIndex 分布 + 选中物体的逐个分配明细。用于排查「补挂 0 个但仍暗」的情况。</summary>
    [MenuItem("Tools/地图环境/诊断引擎Lightmap分配")]
    static void DiagnoseEngineLightmap()
    {
        var sb = new System.Text.StringBuilder("[引擎Lightmap诊断] 场景=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "\n");
        var lms = LightmapSettings.lightmaps;
        sb.AppendLine($"槽位数 = {(lms != null ? lms.Length : 0)}");
        if (lms != null)
            for (int i = 0; i < lms.Length; i++)
            {
                var c = lms[i].lightmapColor;
                var d = lms[i].lightmapDir;
                sb.AppendLine($"  槽[{i}]: color={(c != null ? c.name : "空")} ({(c != null ? c.width + "x" + c.height : "-")})  dir={(d != null ? d.name : "空")}");
            }

        // 全场景 index 分布（含未激活物体）
        var dist = new System.Collections.Generic.SortedDictionary<int, int>();
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (!dist.ContainsKey(r.lightmapIndex)) dist[r.lightmapIndex] = 0;
            dist[r.lightmapIndex]++;
        }
        sb.Append("全场景 lightmapIndex 分布：");
        foreach (var kv in dist) sb.Append($" {kv.Key}×{kv.Value}");
        sb.AppendLine();

        // 选中物体明细
        foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"选中 {r.name}: lightmapIndex={r.lightmapIndex} scaleOffset={r.lightmapScaleOffset} enabled={r.enabled} static={UnityEditor.GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
        Debug.Log(sb.ToString());
    }

    /// <summary>一次性修复：清空全场景 Renderer 的引擎光照贴图分配（lightmapIndex=-1）。
    /// 背景：之前补挂后保存的地图把 lightmapIndex=0 序列化进了场景文件，每次加载自动回来，
    /// 与 shader 手动 _LightmapTex 路叠加成双重烘焙 → 过曝。清除后只保留 shader 手路，
    /// 重新保存地图后分配不再随场景回来。</summary>
    [MenuItem("Tools/地图环境/清除引擎Lightmap分配")]
    static void ClearEngineLightmap()
    {
        int n = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r.lightmapIndex == -1) continue;
            Undo.RecordObject(r, "Clear Engine Lightmap");
            r.lightmapIndex = -1;
            n++;
        }
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log($"[清Lightmap] 已把 {n} 个 Renderer 的 lightmapIndex 置回 -1，场景已标脏。\n" +
                  "看画面：变正常 → 实锤双重烘焙，Ctrl+S 重存该地图即可；\n" +
                  "变黑 → shader 手路（_LightmapTex+uv2）未生效，另查 shader 采样。");
    }
}
