// ============================================================
// ArkFxBuildHook - 构建前后自动切换平台特效素材（避免 APK/包体里打进另一平台的死重）
//
// 背景：
//   Assets/StreamingAssets 下有两套平台相关的特效包
//       arkfx/          Windows（D3D11 shader + DXT 贴图，311 MB）
//       arkfx_android/  Android（GLES3/Vulkan shader + ETC2/ASTC 贴图，264 MB）
//   两套 CAB 名相同、运行时按平台只加载一套（ArkFxLoader.AbFolder），
//   但 Unity 会把 StreamingAssets 下**所有**文件打进包 —— 另一套就是纯死重。
//
// 做法：
//   IPreprocessBuildWithReport  ：把「不是目标平台」的那一套从 Assets 移到 Library/ArkFxBuildSwap/
//   IPostprocessBuildWithReport ：构建结束（无论成功失败）立刻移回
//   [InitializeOnLoadMethod]    ：编辑器启动时兜底恢复（防止构建崩溃后素材卡在临时目录）
//
// 注意：移动的是**目录本身**（同盘 rename，瞬间完成），不是逐个文件复制。
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ArkFxBuildHook : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    const string SaRelDir = "Assets/StreamingAssets";
    const string WindowsFolder = "arkfx";
    const string AndroidFolder = "arkfx_android";

    public int callbackOrder { get { return 0; } }

    static string SaAbsDir { get { return Path.GetFullPath(SaRelDir); } }
    static string SwapRoot
    {
        get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "ArkFxBuildSwap"); }
    }
    static string StateFile { get { return Path.Combine(SwapRoot, "swapped.txt"); } }

    // ---------------- 构建回调 ----------------

    public void OnPreprocessBuild(BuildReport report)
    {
        // 先恢复任何遗留状态（上一次构建异常中断的情况）
        RestoreAll(silent: true);

        bool android = report.summary.platform == BuildTarget.Android;
        string keep = android ? AndroidFolder : WindowsFolder;
        string drop = android ? WindowsFolder : AndroidFolder;

        string keepPath = Path.Combine(SaAbsDir, keep);
        string dropPath = Path.Combine(SaAbsDir, drop);

        if (!Directory.Exists(keepPath))
            Debug.LogWarning($"[ArkFxBuildHook] 目标平台要用的素材目录不存在：{keepPath}（特效将不可用）");

        if (Directory.Exists(dropPath))
        {
            MoveOut(drop);
            Debug.Log($"[ArkFxBuildHook] 构建目标={report.summary.platform}，已把 {drop}/ 移到临时目录（不进包）；保留 {keep}/");
        }
        else
        {
            Debug.Log($"[ArkFxBuildHook] 构建目标={report.summary.platform}，{drop}/ 不存在，无需移动");
        }

        RegenerateFileList();
        AssetDatabase.Refresh();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        RestoreAll(silent: false);
        RegenerateFileList();
        AssetDatabase.Refresh();
        Debug.Log($"[ArkFxBuildHook] 构建结束（{report.summary.result}），已恢复平台素材目录");
    }

    // ---------------- 编辑器启动兜底 ----------------

    [InitializeOnLoadMethod]
    static void EnsureRestoredOnLoad()
    {
        if (File.Exists(StateFile))
        {
            RestoreAll(silent: false);
            AssetDatabase.Refresh();
        }
    }

    // ---------------- 菜单（手动演练/急救） ----------------

    [MenuItem("Tools/ArkFx/8. 手动移出非当前平台素材（演练用）", false, 8)]
    static void ManualMoveOut()
    {
        bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        string drop = android ? WindowsFolder : AndroidFolder;
        if (!Directory.Exists(Path.Combine(SaAbsDir, drop)))
        {
            Debug.Log($"[ArkFxBuildHook] {drop}/ 不存在，无需移动");
            return;
        }
        MoveOut(drop);
        RegenerateFileList();
        AssetDatabase.Refresh();
        Debug.Log($"[ArkFxBuildHook] 已移出 {drop}/（当前平台={EditorUserBuildSettings.activeBuildTarget}）");
    }

    [MenuItem("Tools/ArkFx/9. 恢复被移出的素材", false, 9)]
    static void ManualRestore()
    {
        RestoreAll(silent: false);
        RegenerateFileList();
        AssetDatabase.Refresh();
    }

    // ---------------- 实现 ----------------

    static void MoveOut(string folderName)
    {
        Directory.CreateDirectory(SwapRoot);
        string src = Path.Combine(SaAbsDir, folderName);
        string dst = Path.Combine(SwapRoot, folderName);
        if (Directory.Exists(dst)) Directory.Delete(dst, true);   // 残留

        Directory.Move(src, dst);
        File.WriteAllText(StateFile, folderName);
    }

    static void RestoreAll(bool silent)
    {
        try
        {
            if (!File.Exists(StateFile)) return;

            string folderName = File.ReadAllText(StateFile).Trim();
            if (string.IsNullOrEmpty(folderName)) { File.Delete(StateFile); return; }

            string src = Path.Combine(SwapRoot, folderName);
            string dst = Path.Combine(SaAbsDir, folderName);

            if (!Directory.Exists(src)) { File.Delete(StateFile); return; }
            if (Directory.Exists(dst)) Directory.Delete(dst, true);

            Directory.Move(src, dst);
            File.Delete(StateFile);
            if (!silent)
                Debug.Log($"[ArkFxBuildHook] 已恢复 StreamingAssets/{folderName}/");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArkFxBuildHook] 恢复素材目录失败: " + e);
        }
    }

    /// <summary>
    /// 重新生成 StreamingAssets/filelist.txt（只服务安卓首启拷贝）。
    /// 规则与 Tools/make_filelist.py 一致：列出全部文件，排除 arkfx/（Windows 专用）、
    /// filelist.txt 自身、以及 .meta。
    /// </summary>
    static void RegenerateFileList()
    {
        try
        {
            if (!Directory.Exists(SaAbsDir)) return;
            var list = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(SaAbsDir, "*", SearchOption.AllDirectories))
            {
                string full = Path.GetFullPath(f);
                string rel = full.Substring(SaAbsDir.Length + 1).Replace('\\', '/');
                if (rel == "filelist.txt" || rel.EndsWith(".meta")) continue;
                if (rel.StartsWith(WindowsFolder + "/")) continue;      // Windows 专用
                list.Add(rel);
            }
            list.Sort(System.StringComparer.Ordinal);
            File.WriteAllText(Path.Combine(SaAbsDir, "filelist.txt"), string.Join("\n", list) + "\n");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArkFxBuildHook] 生成 filelist.txt 失败: " + e.Message);
        }
    }
}
#endif
