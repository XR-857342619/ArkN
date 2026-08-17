using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OfficeOpenXml;

/// <summary>
/// Excel 列同步工具。
/// 选择基准 Excel 文件后，遍历指定文件夹下的其余 xlsx，
/// 以“基准表第 2 行字段名（为空时退回第 1 行）”为列标识：
///   - 基准中存在、目标缺失的列 -> 追加到目标表末尾（复制前三行表头，数据行留空）；
///   - 目标中存在、基准缺失的列 -> 从目标表删除；
///   - 目标缺失的 Sheet -> 新建并复制前三行表头。
/// 修改前会自动备份到目标文件夹的 ExcelSyncBackup 子目录。
/// </summary>
public class ExcelColumnSyncTool
{
    [MenuItem("Tools/Excel列同步/选择基准文件并同步")]
    public static void SyncWithDialog()
    {
        ExcelColumnSyncWindow.ShowWindow();
    }

    /// <summary>
    /// 同步指定文件夹下所有 xlsx 的指定 sheet 列结构（推荐使用）。
    /// </summary>
    public static void SyncColumns(string basePath, string targetFolder, string sheetName)
    {
        var baseFile = new FileInfo(basePath);
        if (!baseFile.Exists)
            throw new FileNotFoundException("基准 Excel 文件不存在", basePath);

        var baseFullPath = Path.GetFullPath(basePath);
        var targetFiles = Directory.GetFiles(targetFolder, "*.xlsx", SearchOption.AllDirectories)
            .Where(x => !string.Equals(Path.GetFullPath(x), baseFullPath, StringComparison.OrdinalIgnoreCase))
            .Where(x => !Path.GetFileName(x).StartsWith("~$"))
            .Where(x => !IsInBackupFolder(x))
            .OrderBy(x => x)
            .ToList();

        if (targetFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "目标文件夹下没有可同步的 xlsx 文件。", "确定");
            return;
        }

        // 1. 读取基准表中指定 sheet 的列结构（sheetName 为空时同步全部非 # sheet）
        Dictionary<string, List<string>> baseColumns = ReadColumnDefinitions(basePath, sheetName);
        if (baseColumns.Count == 0)
        {
            string msg = string.IsNullOrEmpty(sheetName)
                ? "基准 Excel 中没有可同步的 sheet。"
                : $"基准 Excel 中不存在 sheet：{sheetName}";
            EditorUtility.DisplayDialog("提示", msg, "确定");
            return;
        }

        int successCount = 0;
        var failedFiles = new List<string>();
        var skippedFiles = new List<string>();

        // 2. 逐个同步目标文件
        foreach (string targetPath in targetFiles)
        {
            try
            {
                bool hasSheet = SheetExistsInFile(targetPath, sheetName, baseColumns);
                if (!hasSheet)
                {
                    if (!string.IsNullOrEmpty(sheetName))
                    {
                        skippedFiles.Add($"{targetPath}（缺少 sheet: {sheetName}）");
                        continue;
                    }
                }

                SyncOneFile(targetPath, baseColumns, basePath, sheetName);
                successCount++;
            }
            catch (Exception e)
            {
                failedFiles.Add($"{targetPath}: {e.Message}");
                Debug.LogError($"同步文件失败 {targetPath}\n{e}");
            }
        }

        AssetDatabase.Refresh();
        string message = $"同步完成：成功 {successCount}/{targetFiles.Count} 个文件。";
        if (skippedFiles.Count > 0)
            message += $"\n跳过 {skippedFiles.Count} 个（目标缺少指定 sheet）：\n" + string.Join("\n", skippedFiles.Take(10));
        if (failedFiles.Count > 0)
            message += $"\n失败 {failedFiles.Count} 个：\n" + string.Join("\n", failedFiles.Take(10));
        Debug.Log(message);
        EditorUtility.DisplayDialog("同步完成", message, "确定");
    }

    /// <summary>
    /// 兼容旧调用：同步全部 sheet（高风险，不推荐）。
    /// </summary>
    public static void SyncColumns(string basePath, string targetFolder)
    {
        SyncColumns(basePath, targetFolder, null);
    }

    /// <summary>
    /// 读取基准 Excel 的列结构：sheetName -> 列键列表（跳过 # 开头 sheet 与空字段名列）。
    /// sheetName 非空时只读取指定 sheet。
    /// </summary>
    private static Dictionary<string, List<string>> ReadColumnDefinitions(string excelPath, string sheetName = null)
    {
        var result = new Dictionary<string, List<string>>();

        using (ExcelPackage package = new ExcelPackage(new FileInfo(excelPath)))
        {
            foreach (var sheet in package.Workbook.Worksheets)
            {
                if (sheet.Name.StartsWith("#")) continue;
                if (!string.IsNullOrEmpty(sheetName) && sheet.Name != sheetName) continue;

                var keys = new List<string>();
                if (sheet.Dimension != null)
                {
                    for (int col = 1; col <= sheet.Dimension.Columns; col++)
                    {
                        string key = GetColumnKey(sheet, col);
                        if (!string.IsNullOrEmpty(key))
                            keys.Add(key);
                    }
                }
                result[sheet.Name] = keys;
            }
        }

        return result;
    }

    private static void SyncOneFile(string targetPath, Dictionary<string, List<string>> baseColumns, string basePath, string sheetName)
    {
        var targetFile = new FileInfo(targetPath);
        if (!targetFile.Exists) return;

        // 修改前备份
        string backupDir = Path.Combine(Path.GetDirectoryName(targetPath), "ExcelSyncBackup");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir,
            $"{Path.GetFileNameWithoutExtension(targetPath)}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        File.Copy(targetPath, backupPath, true);

        using (ExcelPackage package = new ExcelPackage(targetFile))
        {
            // 读取基准文件，仅用于复制缺失列前三行与表头
            using (ExcelPackage basePackage = new ExcelPackage(new FileInfo(basePath)))
            {
                foreach (var kv in baseColumns)
                {
                    string targetSheetName = kv.Key;
                    List<string> baseKeys = kv.Value;

                    var baseSheet = basePackage.Workbook.Worksheets[targetSheetName];
                    if (baseSheet == null) continue;

                    var targetSheet = package.Workbook.Worksheets[targetSheetName];
                    if (targetSheet == null)
                    {
                        // 只同步指定 sheet：目标缺失时不新建，避免意外改动其他内容
                        continue;
                    }

                    // 1. 删除目标中所有不在基准列定义中的列（从后往前删除，避免列号偏移）
                    List<string> targetKeys = GetColumnKeys(targetSheet);
                    for (int col = targetKeys.Count; col >= 1; col--)
                    {
                        string key = targetKeys[col - 1];
                        if (!baseKeys.Contains(key))
                        {
                            targetSheet.DeleteColumn(col);
                        }
                    }

                    // 2. 重新读取目标列键（删除后列号已变化）
                    targetKeys = GetColumnKeys(targetSheet);

                    // 3. 补齐目标缺失的列（先临时追加到末尾，后续统一按基准顺序重排）
                    foreach (string key in baseKeys)
                    {
                        if (targetKeys.Contains(key)) continue;

                        int baseCol = FindColumnByKey(baseSheet, key);
                        if (baseCol <= 0) continue;

                        int insertCol = (targetSheet.Dimension?.Columns ?? 0) + 1;
                        targetSheet.Cells[1, insertCol].Value = baseSheet.Cells[1, baseCol].Value;
                        targetSheet.Cells[2, insertCol].Value = baseSheet.Cells[2, baseCol].Value;
                        targetSheet.Cells[3, insertCol].Value = baseSheet.Cells[3, baseCol].Value;
                        targetKeys.Add(key);
                    }

                    // 4. 按基准列顺序重排目标列
                    ReorderColumnsToMatchBase(targetSheet, baseKeys);
                }
            }

            package.Save();
        }
    }

    /// <summary>
    /// 按基准列顺序重排目标 sheet 的列。
    /// 使用“读取全部数据 -> 清空 -> 按新顺序写回”的方式重排，避免使用 InsertColumn 触发命名范围异常。
    /// </summary>
    private static void ReorderColumnsToMatchBase(ExcelWorksheet sheet, List<string> baseKeys)
    {
        if (sheet.Dimension == null) return;

        // 1. 读取当前所有列的键映射与列宽
        int maxCol = sheet.Dimension.Columns;
        var keyToCol = new Dictionary<string, int>();
        var columnWidths = new Dictionary<int, double>();

        for (int col = 1; col <= maxCol; col++)
        {
            string key = GetColumnKey(sheet, col);
            if (!string.IsNullOrEmpty(key) && !keyToCol.ContainsKey(key))
                keyToCol[key] = col;

            columnWidths[col] = sheet.Column(col).Width;
        }

        // 2. 一次性读取整个工作表的值，避免依赖 sheet.Dimension 的行数缓存，
        //    防止因 Dimension 未包含第 3 行及以后的数据而导致重排后数据丢失。
        var allValues = sheet.Cells.Value as object[,];
        int maxRow = allValues != null ? allValues.GetLength(0) : 0;
        int maxColFromData = allValues != null ? allValues.GetLength(1) : 0;

        if (maxRow == 0 || maxColFromData == 0) return;

        // 3. 清空整个 sheet 的值（不触发 InsertColumn / DeleteColumn）
        sheet.Cells.Clear();

        // 4. 按基准列顺序写回
        for (int pos = 1; pos <= baseKeys.Count; pos++)
        {
            string key = baseKeys[pos - 1];
            if (!keyToCol.TryGetValue(key, out int currentCol)) continue;
            if (currentCol <= 0 || currentCol > maxColFromData) continue;

            for (int row = 1; row <= maxRow; row++)
            {
                sheet.Cells[row, pos].Value = allValues[row - 1, currentCol - 1];
            }

            if (columnWidths.TryGetValue(currentCol, out double width))
                sheet.Column(pos).Width = width;
        }
    }

    /// <summary>
    /// 判断目标文件中是否存在指定 sheet。
    /// sheetName 为空时视为存在（同步全部 sheet 模式）。
    /// </summary>
    private static bool SheetExistsInFile(string targetPath, string sheetName, Dictionary<string, List<string>> baseColumns)
    {
        if (string.IsNullOrEmpty(sheetName)) return true;

        using (ExcelPackage package = new ExcelPackage(new FileInfo(targetPath)))
        {
            return package.Workbook.Worksheets[sheetName] != null;
        }
    }

    private static void CopyHeaderRows(ExcelWorksheet baseSheet, ExcelWorksheet targetSheet, List<string> baseKeys)
    {
        int targetCol = 1;
        foreach (string key in baseKeys)
        {
            int baseCol = FindColumnByKey(baseSheet, key);
            if (baseCol <= 0) continue;

            targetSheet.Cells[1, targetCol].Value = baseSheet.Cells[1, baseCol].Value;
            targetSheet.Cells[2, targetCol].Value = baseSheet.Cells[2, baseCol].Value;
            targetSheet.Cells[3, targetCol].Value = baseSheet.Cells[3, baseCol].Value;
            targetCol++;
        }
    }

    /// <summary>
    /// 获取目标 sheet 的列键列表。列键优先取第 2 行字段名，为空时退回第 1 行。
    /// </summary>
    private static List<string> GetColumnKeys(ExcelWorksheet sheet)
    {
        var keys = new List<string>();
        if (sheet.Dimension == null) return keys;

        for (int col = 1; col <= sheet.Dimension.Columns; col++)
        {
            keys.Add(GetColumnKey(sheet, col));
        }
        return keys;
    }

    private static string GetColumnKey(ExcelWorksheet sheet, int col)
    {
        string key = sheet.Cells[2, col].Value?.ToString();
        if (string.IsNullOrWhiteSpace(key))
            key = sheet.Cells[1, col].Value?.ToString();
        return string.IsNullOrWhiteSpace(key) ? "" : key.Trim();
    }

    private static int FindColumnByKey(ExcelWorksheet sheet, string key)
    {
        if (sheet.Dimension == null) return -1;
        for (int col = 1; col <= sheet.Dimension.Columns; col++)
        {
            if (GetColumnKey(sheet, col) == key) return col;
        }
        return -1;
    }

    // 判断文件是否位于 ExcelSyncBackup 备份目录中（递归检查父目录）
    private static bool IsInBackupFolder(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (directory != null)
        {
            if (Path.GetFileName(directory).Equals("ExcelSyncBackup", StringComparison.OrdinalIgnoreCase))
                return true;
            directory = Path.GetDirectoryName(directory);
        }
        return false;
    }
}
