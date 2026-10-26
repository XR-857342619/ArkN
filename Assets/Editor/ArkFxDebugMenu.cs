// ============================================================
// ArkFxDebugMenu - ArkFx 验收用的编辑器菜单（P0/P1）
//
//   0. 切换 特效来源 工程内 <-> 原版AB   （PlayerPrefs 持久化，重进 Play 仍有效）
//   1. 切换 UV 实现 v5 <-> v6
//   2. 打印选中对象的 ArkFxDiag
//   3. 统计场景里的 ArkFx 组件
//   4. 检查 AB 里的 Shader 是否可用       （品红问题定位）
//   5. 打印选中对象每个材质的 shader 状态
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ArkFxDebugMenu
{
    static string LogPath(string file)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, file);
    }

    [MenuItem("Tools/ArkFx/0. 切换 特效来源 工程内 <-> 原版AB", false, 0)]
    static void ToggleSource()
    {
        ArkFxLoader.ForceAbSource = !ArkFxLoader.ForceAbSource;
        Debug.Log("[ArkFx] 特效来源已切换为：" + (ArkFxLoader.ForceAbSource ? "原版 AB（arkfx）" : "工程内 Addressables")
            + "。开关已写入 PlayerPrefs，重新进 Play 仍有效；请重新进 Play 生效。");
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
        sb.AppendLine("[ArkFx] 场景组件统计");
        sb.AppendLine("  ArkFxEffectHost      : " + host.Length);
        sb.AppendLine("  ArkFxUVTweenV6       : " + v6.Length);
        sb.AppendLine("  ArkFxUVTweenAdvanceV6: " + v6a.Length);
        sb.AppendLine("  ArkFxUVRotation      : " + rot.Length);
        sb.AppendLine("  ArkFxUVTween (v5)    : " + v5.Length);
        sb.AppendLine("  ArkFxScriptStub      : " + stub.Length);
        sb.AppendLine("  UseV6 开关           : " + ArkFxLoader.UseV6);
        sb.AppendLine("  特效来源             : " + (ArkFxLoader.ForceAbSource ? "原版 AB" : "工程内 Addressables"));
        int busy = 0, finished = 0;
        foreach (var h in host) { if (h.IsBusy) busy++; if (h.Finished) finished++; }
        sb.AppendLine("  宿主状态             : " + finished + " 个已结束 / " + busy + " 个等待中");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/ArkFx/4. 检查 AB 里的 Shader 是否可用", false, 4)]
    static void CheckShaders()
    {
        int nb = 0, total = 0, unsupported = 0, nullShader = 0;
        var report = new StringBuilder();
        report.AppendLine("# ArkFx Shader 支持性报告 " + System.DateTime.Now);
        report.AppendLine("# API=" + SystemInfo.graphicsDeviceType + " ShaderLevel=" + SystemInfo.graphicsShaderLevel
            + " maxTex=" + SystemInfo.maxTextureSize);

        foreach (var b in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (b == null) continue;
            nb++;
            Shader[] shaders;
            try { shaders = b.LoadAllAssets<Shader>(); }
            catch { continue; }
            foreach (var sh in shaders)
            {
                if (sh == null) { nullShader++; continue; }
                total++;
                bool ok = sh.isSupported;
                if (!ok) unsupported++;
                report.AppendLine((ok ? "OK   " : "FAIL ") + sh.name + "\t(bundle=" + b.name + ", pass=" + sh.passCount + ")");
            }
        }

        string verdict;
        if (total == 0) verdict = "一个 shader 都没加载到：依赖包 _deps/1b70e77f_lz4.ab 未加载，或 shader 读取失败";
        else if (unsupported == total) verdict = "全部不支持 → Unity 版本不兼容";
        else if (unsupported > total / 2) verdict = "绝大多数不支持 → 大概率仍是版本不兼容；请查看 ArkFxShaders.txt 里少数 OK 的是什么";
        else verdict = "多数可用，品红来自个别 shader，见 ArkFxShaders.txt";

        Debug.Log("[ArkFx] bundle " + nb + " 个 | Shader " + total + " | 不支持 " + unsupported + " | 空 " + nullShader
            + "\n  API=" + SystemInfo.graphicsDeviceType + " ShaderLevel=" + SystemInfo.graphicsShaderLevel
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
}
#endif
