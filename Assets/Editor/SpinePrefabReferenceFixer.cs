#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量检查/修复 Bundles/Units 下 prefab 的 Spine 引用错位问题。
/// 背景：部分 prefab（如 enemy_10184_pppsbr）是从其他单位（如 enemy_1031_mrogue_2）复制而来，
/// SkeletonAnimation 的 skeletonDataAsset 与 MeshRenderer 材质仍指向来源单位的 Spine 资源。
///
/// 用法：
///   Tools/Spine引用修复/1-扫描错误引用(只报告)   —— 遍历全部 prefab 并输出引用错位清单，不修改
///   Tools/Spine引用修复/2-修复选中的Prefab      —— 只修复 Project 窗口当前选中的 prefab
///       （支持多选；选中的若是文件夹则处理其下所有 prefab）
///
/// 匹配规则：prefab 文件名 = 模型ID（如 enemy_10184_pppsbr），
/// 期望资源 = 工程内文件名恰为 <模型ID>_SkeletonData / <模型ID>_Material 的 Spine 资产。
/// </summary>
public static class SpinePrefabReferenceFixer
{
    const string UnitsFolder = "Assets/Bundles/Units";

    [MenuItem("Tools/Spine引用修复/1-扫描错误引用(只报告)")]
    public static void Audit() => Run(EnumerateAllPrefabs(), false);

    [MenuItem("Tools/Spine引用修复/2-修复选中的Prefab")]
    public static void FixSelection() => Run(EnumerateSelectedPrefabs(), true);

    /// <summary>仅当选中内容里包含 prefab 或文件夹时菜单可用</summary>
    [MenuItem("Tools/Spine引用修复/2-修复选中的Prefab", true)]
    public static bool ValidateFixSelection()
    {
        foreach (var obj in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(p)) continue;
            if (p.EndsWith(".prefab") || Directory.Exists(p)) return true;
        }
        return false;
    }

    static IEnumerable<string> EnumerateAllPrefabs()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { UnitsFolder }))
            yield return AssetDatabase.GUIDToAssetPath(g);
    }

    static IEnumerable<string> EnumerateSelectedPrefabs()
    {
        var folders = new List<string>();
        var direct = new List<string>();
        foreach (var obj in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(p)) continue;
            if (p.EndsWith(".prefab")) direct.Add(p);
            else if (Directory.Exists(p)) folders.Add(p);
        }
        if (folders.Count > 0)
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
                yield return AssetDatabase.GUIDToAssetPath(g);
        foreach (var p in direct.Distinct())
            yield return p;
    }

    static void Run(IEnumerable<string> prefabPaths, bool fix)
    {
        var report = new StringBuilder();
        int scanned = 0, wrong = 0, fixedCount = 0, skipped = 0;

        foreach (string path in prefabPaths)
        {
            string model = Path.GetFileNameWithoutExtension(path);
            scanned++;

            // 1. 按文件名查找该模型"自己"的 Spine 资源
            SkeletonDataAsset expectSkel = FindSkeletonData(model + "_SkeletonData");
            Material expectMat = FindMaterial(model + "_Material");
            if (expectSkel == null)
            {
                skipped++;
                continue; // 没有自己 Spine 资源的单位（普通预制体模型），跳过
            }

            // 2. 打开 prefab 检查引用
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sr = root.GetComponentInChildren<SkeletonRenderer>(true);
                if (sr == null) { skipped++; continue; }

                bool skelWrong = sr.skeletonDataAsset != expectSkel;
                var renderer = sr.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = sr.GetComponentInChildren<MeshRenderer>(true);
                bool matWrong = false;
                if (renderer != null && expectMat != null)
                {
                    var mats = renderer.sharedMaterials;
                    matWrong = mats == null || mats.Length == 0 || mats[0] != expectMat;
                }

                if (!skelWrong && !matWrong) continue;
                wrong++;
                report.AppendLine($"[错位] {Path.GetFileName(path)}");
                if (skelWrong)
                    report.AppendLine($"    SkeletonData: {(sr.skeletonDataAsset ? sr.skeletonDataAsset.name : "null")}  =>  应为 {expectSkel.name}");
                if (matWrong && renderer != null)
                {
                    string cur = (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials[0])
                        ? renderer.sharedMaterials[0].name : "null";
                    report.AppendLine($"    Material:     {cur}  =>  应为 {expectMat.name}");
                }

                // 3. 修复
                if (fix)
                {
                    if (skelWrong) sr.skeletonDataAsset = expectSkel;
                    if (matWrong && renderer != null)
                    {
                        var mats = renderer.sharedMaterials;
                        if (mats == null || mats.Length == 0) mats = new Material[1];
                        mats[0] = expectMat; // Spine 单位材质固定单张；多材质情况会打印警告
                        if (mats.Length > 1)
                            report.AppendLine($"    警告: {model} 有 {mats.Length} 个材质槽，仅替换了第 1 个，请人工复核");
                        renderer.sharedMaterials = mats;
                    }
                    EditorUtility.SetDirty(sr);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedCount++;
                    report.AppendLine($"    => 已修复并保存");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        string scope = fix ? "选中项" : "全部";
        report.Insert(0, $"扫描范围: {scope} | 共 {scanned} 个 prefab | 引用错位 {wrong} 个 | 跳过(无Spine资源或非Spine) {skipped} 个 | {(fix ? $"已修复 {fixedCount} 个" : "未修改")}\n\n");
        string reportPath = Path.Combine(UnitsFolder, fix ? "SpineFixReport.txt" : "SpineAuditReport.txt");
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log(report.ToString());
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Spine 引用" + (fix ? "修复" : "扫描"),
            $"范围: {scope}\n扫描 {scanned}，错位 {wrong}，{(fix ? $"修复 {fixedCount}" : "未修改")}。\n详细报告见 {reportPath}", "OK");
    }

    /// <summary>按精确文件名在 Assets/Res/Spine 下查找 SkeletonDataAsset</summary>
    static SkeletonDataAsset FindSkeletonData(string fileName)
    {
        foreach (var g in AssetDatabase.FindAssets(fileName + " t:SkeletonDataAsset", new[] { "Assets/Res/Spine" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == fileName)
                return AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(p);
        }
        return null;
    }

    static Material FindMaterial(string fileName)
    {
        foreach (var g in AssetDatabase.FindAssets(fileName + " t:Material", new[] { "Assets/Res/Spine" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == fileName)
                return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }
}
#endif
