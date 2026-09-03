using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MergeCatalogWindow : EditorWindow
{
    const string PrefB1Catalog = "MergeCatalogTool.B1Catalog";
    const string PrefB1BundleRoot = "MergeCatalogTool.B1BundleRoot";
    const string PrefB2Catalog = "MergeCatalogTool.B2Catalog";
    const string PrefB2BundleRoot = "MergeCatalogTool.B2BundleRoot";
    const string PrefOutputCatalog = "MergeCatalogTool.OutputCatalog";
    const string PrefCopyBundles = "MergeCatalogTool.CopyBundles";

    string b1Catalog;
    string b1BundleRoot;
    string b2Catalog;
    string b2BundleRoot;
    string outputCatalog;
    bool copyBundles = true;
    string log = "";

    [MenuItem("Tools/Merge Catalog/Open Merge Window")]
    public static void OpenWindow()
    {
        var window = GetWindow<MergeCatalogWindow>("Catalog Merge Tool");
        window.minSize = new Vector2(640, 420);
        window.Show();
    }

    void OnEnable()
    {
        b1Catalog = EditorPrefs.GetString(PrefB1Catalog, "");
        b1BundleRoot = EditorPrefs.GetString(PrefB1BundleRoot, "");
        b2Catalog = EditorPrefs.GetString(PrefB2Catalog, "");
        b2BundleRoot = EditorPrefs.GetString(PrefB2BundleRoot, "");
        outputCatalog = EditorPrefs.GetString(PrefOutputCatalog, "");
        copyBundles = EditorPrefs.GetBool(PrefCopyBundles, true);
        log = "";
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Catalog Merge Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        b1Catalog = PathField("Branch1 Catalog (base)", b1Catalog, false, PrefB1Catalog);
        b1BundleRoot = FolderField("Branch1 aa Bundle Root", b1BundleRoot, PrefB1BundleRoot);
        EditorGUILayout.Space();

        b2Catalog = PathField("Branch2 Catalog (to merge in)", b2Catalog, false, PrefB2Catalog);
        b2BundleRoot = FolderField("Branch2 aa Bundle Root", b2BundleRoot, PrefB2BundleRoot);
        EditorGUILayout.Space();

        outputCatalog = PathField("Output Catalog (empty = overwrite B1)", outputCatalog, false, PrefOutputCatalog);
        copyBundles = EditorGUILayout.Toggle("Copy B2 bundles into B1", copyBundles);
        EditorPrefs.SetBool(PrefCopyBundles, copyBundles);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Inspect Catalogs", GUILayout.Height(30)))
                Inspect();

            if (GUILayout.Button("Merge + Copy Bundles", GUILayout.Height(30)))
                Merge(true);

            if (GUILayout.Button("Merge Catalog Only", GUILayout.Height(30)))
                Merge(false);
        }

        EditorGUILayout.Space();
        if (!string.IsNullOrEmpty(log))
        {
            EditorGUILayout.HelpBox(log, MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("提示：目录应指向 StreamingAssets/aa 这一层，脚本会自动拼接 StandaloneWindows64。", EditorStyles.helpBox);
    }

    string PathField(string label, string value, bool isFolder, string prefKey)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(240));
            value = EditorGUILayout.TextField(value);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                if (isFolder)
                    value = EditorUtility.OpenFolderPanel(label, string.IsNullOrEmpty(value) ? "" : value, "");
                else
                    value = EditorUtility.OpenFilePanel(label, string.IsNullOrEmpty(value) ? "" : Path.GetDirectoryName(value), "json");
            }
        }

        if (GUI.changed)
            EditorPrefs.SetString(prefKey, value ?? "");
        return value ?? "";
    }

    string FolderField(string label, string value, string prefKey)
    {
        return PathField(label, value, true, prefKey);
    }

    void Inspect()
    {
        if (!ValidatePaths(false))
            return;

        try
        {
            MergeCatalogTool.InspectCatalogs(b1Catalog, b2Catalog);
            log = "Inspect 完成，详细结果请查看 Console。";
        }
        catch (Exception e)
        {
            log = "Inspect 失败：" + e.Message;
            Debug.LogException(e);
        }
    }

    void Merge(bool copy)
    {
        if (!ValidatePaths(true))
            return;

        try
        {
            var output = string.IsNullOrEmpty(outputCatalog) ? b1Catalog : outputCatalog;

            // 复制 bundle 时，B1 Bundle Root 自动跟随输出 catalog 所在目录，
            // 避免误填成 branch2 的 aa 目录导致所有 bundle 被误判为“已存在”。
            if (copy && !string.IsNullOrEmpty(output))
            {
                var outputDir = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    b1BundleRoot = outputDir;
                    EditorPrefs.SetString(PrefB1BundleRoot, b1BundleRoot);
                }
            }

            MergeCatalogTool.MergeCatalogs(b1Catalog, b1BundleRoot, b2Catalog, b2BundleRoot, output, copy);
            log = "Merge 完成，详细结果请查看 Console。";
        }
        catch (Exception e)
        {
            log = "Merge 失败：" + e.Message;
            Debug.LogException(e);
        }
    }

    bool ValidatePaths(bool requireBundleRoots)
    {
        if (string.IsNullOrEmpty(b1Catalog) || !File.Exists(b1Catalog))
        {
            log = "请选择有效的 Branch1 catalog.json。";
            return false;
        }
        if (string.IsNullOrEmpty(b2Catalog) || !File.Exists(b2Catalog))
        {
            log = "请选择有效的 Branch2 catalog.json。";
            return false;
        }
        if (requireBundleRoots)
        {
            if (string.IsNullOrEmpty(b1BundleRoot) || !Directory.Exists(b1BundleRoot))
            {
                log = "请选择有效的 Branch1 aa 目录。";
                return false;
            }
            if (string.IsNullOrEmpty(b2BundleRoot) || !Directory.Exists(b2BundleRoot))
            {
                log = "请选择有效的 Branch2 aa 目录。";
                return false;
            }
        }
        return true;
    }
}
