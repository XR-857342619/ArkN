// ============================================================
// ArkFxDebugMenu - ArkFx 验收用的编辑器菜单
//
//   0. 切换 特效来源 工程内 <-> 原版AB   （PlayerPrefs 持久化）
//   1. 切换 UV 实现 v5 <-> v6
//   2. 打印选中对象的 ArkFxDiag
//   3. 统计场景里的 ArkFx 组件
//   4. 检查 AB 里的 Shader 是否可用       （编辑模式即可跑，不依赖 Play）
//   5. 打印选中对象每个材质的 shader 状态
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ArkFxDebugMenu
{
    static string ArkFxDir { get { return Path.Combine(Application.dataPath, "StreamingAssets", "arkfx"); } }

    static string LogPath(string file)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, file);
    }

    [MenuItem("Tools/ArkFx/0. 切换 特效来源优先级 AB优先 <-> 工程内优先", false, 0)]
    static void ToggleSource()
    {
        ArkFxLoader.PreferAbSource = !ArkFxLoader.PreferAbSource;
        Debug.Log("[ArkFx] 特效来源优先级："
            + (ArkFxLoader.PreferAbSource ? "原版 AB 优先（找不到再回落工程内）" : "工程内 prefab 优先")
            + "。已写入游戏存档 data.sav（与主界面 SwitchEffectsSource 按钮同一个开关），重新进 Play 生效。");
    }

    [MenuItem("Tools/ArkFx/1. 切换 UV 实现 v5 <-> v6", false, 1)]
    static void ToggleV6()
    {
        ArkFxLoader.UseV6 = !ArkFxLoader.UseV6;
        Debug.Log("[ArkFx] UV 实现已切换为 " + (ArkFxLoader.UseV6 ? "v6（IDA 实证版）" : "v5（旧版）") + "。需重新进 Play 生效。");
    }

    [MenuItem("Tools/ArkFx/2. 打印选中对象的 ArkFxDiag", false, 2)]
    static void DumpSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[ArkFx] 先选中一个特效实例"); return; }
        string report = ArkFxDiag.Dump(go);
        Debug.Log(report);
        try
        {
            string path = LogPath("ArkFxDiag.txt");
            File.WriteAllText(path, report);
            Debug.Log("[ArkFx] 诊断报告已写入 " + path);
        }
        catch (System.Exception e) { Debug.LogWarning("[ArkFx] 写盘失败: " + e.Message); }
    }

    [MenuItem("Tools/ArkFx/2. 打印选中对象的 ArkFxDiag", true)]
    static bool DumpSelectedValidate() { return Selection.activeGameObject != null; }

    [MenuItem("Tools/ArkFx/3. 统计场景里的 ArkFx 组件", false, 3)]
    static void Stats()
    {
        var host = Object.FindObjectsOfType<ArkFxEffectHost>();
        var v6 = Object.FindObjectsOfType<ArkFxUVTweenV6>();
        var v6a = Object.FindObjectsOfType<ArkFxUVTweenAdvanceV6>();
        var rot = Object.FindObjectsOfType<ArkFxUVRotation>();
        var v5 = Object.FindObjectsOfType<ArkFxUVTween>();
        var stub = Object.FindObjectsOfType<ArkFxScriptStub>();
        var sb = new StringBuilder();
        sb.AppendLine("[ArkFx] 场景组件统计  (Play=" + Application.isPlaying + ")");
        sb.AppendLine("  ArkFxEffectHost      : " + host.Length);
        sb.AppendLine("  ArkFxUVTweenV6       : " + v6.Length);
        sb.AppendLine("  ArkFxUVTweenAdvanceV6: " + v6a.Length);
        sb.AppendLine("  ArkFxUVRotation      : " + rot.Length);
        sb.AppendLine("  ArkFxUVTween (v5)    : " + v5.Length);
        sb.AppendLine("  ArkFxScriptStub      : " + stub.Length);
        sb.AppendLine("  UseV6 开关           : " + ArkFxLoader.UseV6);
        sb.AppendLine("  来源优先级           : " + (ArkFxLoader.PreferAbSource ? "原版 AB 优先" : "工程内优先"));
        int busy = 0, finished = 0;
        foreach (var h in host) { if (h.IsBusy) busy++; if (h.Finished) finished++; }
        sb.AppendLine("  宿主状态             : " + finished + " 个已结束 / " + busy + " 个等待中");
        Debug.Log(sb.ToString());
    }

    // ---- 4. Shader 可用性：自己扫目录加载，编辑模式也能跑 ----
    [MenuItem("Tools/ArkFx/4. 检查 AB 里的 Shader 是否可用", false, 4)]
    static void CheckShaders()
    {
        if (!Directory.Exists(ArkFxDir))
        {
            Debug.LogError("[ArkFx] 找不到目录: " + ArkFxDir);
            return;
        }

        var files = Directory.GetFiles(ArkFxDir, "*.ab", SearchOption.AllDirectories);
        var loaded = new List<AssetBundle>();
        var seen = new HashSet<string>();
        var all = new List<Shader>();
        int bundleOk = 0, bundleFail = 0;

        foreach (var f in files)
        {
            var ab = AssetBundle.LoadFromFile(f);
            if (ab == null) { bundleFail++; Debug.LogWarning("[ArkFx] BUNDLE_FAIL " + f); continue; }
            loaded.Add(ab);
            bundleOk++;
            var ss = ab.LoadAllAssets<Shader>();
            foreach (var s in ss)
                if (s != null && seen.Add(s.name)) all.Add(s);
        }

        int ok = 0, fail = 0;
        var report = new StringBuilder();
        report.AppendLine("# ArkFx Shader 检查");
        report.AppendLine("# Unity   : " + Application.unityVersion);
        report.AppendLine("# API     : " + SystemInfo.graphicsDeviceType + " / ShaderLevel " + SystemInfo.graphicsShaderLevel);
        report.AppendLine("# 目录    : " + ArkFxDir);
        report.AppendLine("# bundle  : 成功 " + bundleOk + " / 失败 " + bundleFail + " / 共 " + files.Length);
        report.AppendLine();

        foreach (var s in all)
        {
            bool sup = s.isSupported;
            if (sup) ok++; else fail++;
            report.AppendLine((sup ? "OK   " : "FAIL ") + s.name);
        }

        foreach (var ab in loaded) ab.Unload(false);

        string verdict;
        if (bundleOk == 0) verdict = "一个包都没加载成功 —— 检查目录与包格式";
        else if (all.Count == 0) verdict = "包能加载但没有 shader —— 目录不对";
        else if (fail == 0) verdict = "全部 shader 可用 ✅";
        else if (ok == 0) verdict = "全部不可用 —— 引擎版本问题";
        else verdict = "部分可用 " + ok + "/" + all.Count;

        Debug.Log("[ArkFx] bundle " + bundleOk + "/" + files.Length + " 成功 | Shader " + all.Count
            + " | 不支持 " + fail + " | Unity=" + Application.unityVersion
            + "\n  API=" + SystemInfo.graphicsDeviceType
            + "\n  → " + verdict);

        try
        {
            string path = LogPath("ArkFxShaders.txt");
            File.WriteAllText(path, report.ToString());
            Debug.Log("[ArkFx] 完整清单已写入 " + path);
        }
        catch (System.Exception e) { Debug.LogWarning("[ArkFx] 写盘失败: " + e.Message); }
    }

    [MenuItem("Tools/ArkFx/5. 打印选中对象每个材质的 shader 状态", false, 5)]
    static void CheckMaterials()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[ArkFx] 先选中一个特效实例"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("[ArkFx] 渲染器材质 / Shader 状态");
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) { sb.AppendLine("  " + r.name + "[" + i + "]: <材质为 null>"); continue; }
                var sh = m.shader;
                string state;
                if (sh == null) state = "shader=null";
                else if (sh.name == "Hidden/InternalErrorShader") state = "shader=Hidden/InternalErrorShader（引用没解析到）";
                else if (!sh.isSupported) state = "shader=" + sh.name + " | 不支持(isSupported=false)";
                else state = "shader=" + sh.name + " | OK";
                sb.AppendLine("  " + r.name + "[" + i + "] mat=" + m.name + " | " + state);
                if (m.HasProperty("_MainTex"))
                {
                    var tex = m.GetTexture("_MainTex");
                    string info = "null（贴图缺失）";
                    if (tex != null)
                    {
                        var t2d = tex as Texture2D;
                        info = tex.name + " " + tex.width + "x" + tex.height + (t2d != null ? " fmt=" + t2d.format : "");
                    }
                    sb.AppendLine("      _MainTex=" + info);
                }
            }
        }
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/ArkFx/5. 打印选中对象每个材质的 shader 状态", true)]
    static bool CheckMaterialsValidate() { return Selection.activeGameObject != null; }

    // ---- 6. UV 偏移采样：验证 protectMainUV 回绕 ----
    static Transform _sampleRoot;
    static float _sampleT0;
    static Dictionary<string, List<Vector2>> _sampleData;

    [MenuItem("Tools/ArkFx/6. 采样选中特效的 UV 偏移（约 2 秒）", false, 6)]
    static void SampleUV()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[ArkFx] 先在 Hierarchy 选中一个特效实例"); return; }
        _sampleRoot = go.transform;
        _sampleData = new Dictionary<string, List<Vector2>>();
        _sampleT0 = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update -= SampleTick;
        EditorApplication.update += SampleTick;
        Debug.Log("[ArkFx] 开始采样 UV 偏移（约 2 秒）...");
    }

    [MenuItem("Tools/ArkFx/6. 采样选中特效的 UV 偏移（约 2 秒）", true)]
    static bool SampleUVValidate() { return Selection.activeGameObject != null; }

    static void SampleTick()
    {
        float t = (float)EditorApplication.timeSinceStartup - _sampleT0;
        foreach (var r in _sampleRoot.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_MainTex")) continue;
            string key = r.transform.name;
            List<Vector2> list;
            if (!_sampleData.TryGetValue(key, out list))
            {
                list = new List<Vector2>();
                _sampleData[key] = list;
            }
            list.Add(m.mainTextureOffset);
        }
        if (t < 2f) return;

        EditorApplication.update -= SampleTick;

        var sb = new StringBuilder();
        sb.AppendLine("[ArkFx] UV 偏移采样结果（2 秒）");
        sb.AppendLine("  节点 | 采样数 | x范围 | y范围 | 首值 | 末值 | 判定");
        foreach (var kv in _sampleData)
        {
            var v = kv.Value;
            if (v.Count < 3) continue;
            float minx = float.MaxValue, maxx = float.MinValue, miny = float.MaxValue, maxy = float.MinValue;
            int wraps = 0;
            for (int i = 0; i < v.Count; i++)
            {
                if (v[i].x < minx) minx = v[i].x;
                if (v[i].x > maxx) maxx = v[i].x;
                if (v[i].y < miny) miny = v[i].y;
                if (v[i].y > maxy) maxy = v[i].y;
                if (i > 0)
                {
                    if (Mathf.Abs(v[i].x - v[i - 1].x) > 0.5f) wraps++;
                    if (Mathf.Abs(v[i].y - v[i - 1].y) > 0.5f) wraps++;
                }
            }
            bool inRange = minx >= -0.01f && miny >= -0.01f && maxx <= 1.01f && maxy <= 1.01f;
            bool moving = Mathf.Abs(maxx - minx) > 0.01f || Mathf.Abs(maxy - miny) > 0.01f;
            string verdict = !moving ? "静止（该节点无 UV 动画）"
                : (inRange ? (wraps > 0 ? "✅ 在 [0,1) 内回绕（protectMainUV 生效）" : "在 [0,1) 内（未观察到回绕）")
                           : "❌ 越界（未包裹，protectMainUV 未生效）");
            sb.AppendLine($"  {kv.Key} | {v.Count} | x[{minx:F3},{maxx:F3}] | y[{miny:F3},{maxy:F3}] | "
                + $"({v[0].x:F3},{v[0].y:F3}) | ({v[v.Count - 1].x:F3},{v[v.Count - 1].y:F3}) | {verdict}");
        }
        Debug.Log(sb.ToString());
    }
}
#endif
