using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OfficeOpenXml;

/// <summary>
/// Excel 列同步窗口：只同步指定 Sheet 的列结构，其他 Sheet 不受影响。
/// </summary>
public class ExcelColumnSyncWindow : EditorWindow
{
    private string basePath = "";
    private string targetFolder = "Excel";
    private string[] sheetNames = new string[0];
    private int selectedSheetIndex = 0;

    [MenuItem("Tools/Excel列同步/同步指定Sheet列")]
    public static void ShowWindow()
    {
        var window = GetWindow<ExcelColumnSyncWindow>("Excel列同步");
        window.minSize = new Vector2(520, 260);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("仅同步指定 Sheet 的列，其他 Sheet 不受影响", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("基准 Excel 文件");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField(basePath);
        if (GUILayout.Button("选择", GUILayout.Width(60)))
            SelectBaseFile();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("目标文件夹");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField(targetFolder);
        if (GUILayout.Button("选择", GUILayout.Width(60)))
            SelectTargetFolder();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("要同步的 Sheet");
        if (sheetNames.Length == 0)
        {
            EditorGUILayout.HelpBox("请先选择基准 Excel 文件", MessageType.Info);
        }
        else
        {
            selectedSheetIndex = EditorGUILayout.Popup("Sheet", selectedSheetIndex, sheetNames);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("开始同步", GUILayout.Height(30)))
        {
            Sync();
        }
    }

    private void SelectBaseFile()
    {
        string selected = EditorUtility.OpenFilePanel("选择基准 Excel 文件", "Excel", "xlsx");
        if (string.IsNullOrEmpty(selected)) return;

        basePath = selected;
        RefreshSheets();
    }

    private void SelectTargetFolder()
    {
        string selected = EditorUtility.OpenFolderPanel("选择要同步的目标文件夹", "Excel", "");
        if (string.IsNullOrEmpty(selected)) return;

        targetFolder = selected;
    }

    private void RefreshSheets()
    {
        sheetNames = new string[0];
        selectedSheetIndex = 0;

        if (string.IsNullOrEmpty(basePath) || !File.Exists(basePath)) return;

        try
        {
            using (ExcelPackage package = new ExcelPackage(new FileInfo(basePath)))
            {
                sheetNames = package.Workbook.Worksheets
                    .Where(x => !x.Name.StartsWith("#"))
                    .Select(x => x.Name)
                    .ToArray();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"读取基准 Excel 失败：{e.Message}");
        }
    }

    private void Sync()
    {
        if (string.IsNullOrEmpty(basePath) || !File.Exists(basePath))
        {
            EditorUtility.DisplayDialog("提示", "请先选择有效的基准 Excel 文件。", "确定");
            return;
        }

        if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
        {
            EditorUtility.DisplayDialog("提示", "请选择有效的目标文件夹。", "确定");
            return;
        }

        if (sheetNames.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "基准 Excel 中没有可同步的 Sheet。", "确定");
            return;
        }

        string sheetName = sheetNames[Mathf.Clamp(selectedSheetIndex, 0, sheetNames.Length - 1)];
        if (!EditorUtility.DisplayDialog(
                "确认同步",
                $"基准文件：\n{basePath}\n\n目标文件夹：\n{targetFolder}\n\n仅同步 Sheet：\n{sheetName}\n\n其他 Sheet 不会修改，是否继续？",
                "继续",
                "取消"))
            return;

        try
        {
            ExcelColumnSyncTool.SyncColumns(basePath, targetFolder, sheetName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Excel 列同步失败：{e.Message}\n{e}");
            EditorUtility.DisplayDialog("同步失败", e.Message, "确定");
        }
    }
}
