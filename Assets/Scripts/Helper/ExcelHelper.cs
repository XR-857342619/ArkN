using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if !UNITY_ANDROID
using OfficeOpenXml;
#endif



public class ExcelHelper
{
    public static Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>()
    {

    };

    private static readonly object exportErrorLock = new object();
    private static readonly List<string> exportErrors = new List<string>();

    // ---------------------------------------------------------------------
    // 导表字段类型校验用的类型集合（与独立检查工具保持一致）
    // ---------------------------------------------------------------------
    private static readonly HashSet<string> IntegerTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "int", "int32", "int64", "long",
        "uint", "uint32", "uint64", "ulong",
        "short", "int16", "ushort", "uint16", "byte", "sbyte"
    };

    private static readonly HashSet<string> FloatTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "float", "single", "double", "decimal"
    };

    private static readonly HashSet<string> BoolTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "bool", "boolean"
    };

    private static readonly HashSet<string> StringTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "string", "system.string"
    };

    private static readonly HashSet<string> NoCheckTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "object", "system.object"
    };

    private static readonly Dictionary<string, int> VectorComponents = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        { "UnityEngine.Vector2", 2 },
        { "UnityEngine.Vector2Int", 2 },
        { "UnityEngine.Vector3", 3 },
        { "UnityEngine.Vector3Int", 3 },
        { "UnityEngine.Rect", 4 }
    };

    private static readonly HashSet<string> IntegerVectorTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "UnityEngine.Vector2Int", "UnityEngine.Vector3Int"
    };

    public static bool Export(List<string> ExcelList)
    {
        // 过滤掉无效/旧路径并去重，避免同一 Sheet 被多次导出导致旧数据叠加
        List<string> effectiveList = FilterAndDeduplicateExcelPaths(ExcelList);
        lock (exportErrorLock)
        {
            exportErrors.Clear();
        }

        Debug.Log($"[ExcelHelper] 导表开始，原始路径数：{ExcelList?.Count ?? 0}，有效路径数：{effectiveList.Count}");
        AppendExportLog($"导表开始：原始文件 {ExcelList?.Count ?? 0} 个，有效文件 {effectiveList.Count} 个");

        if (effectiveList.Count == 0)
        {
            AppendExportLog("导表失败：没有可导出的有效 Excel 文件");
            try { TipManager.Instance.ShowTip("导表失败：没有可导出的有效 Excel 文件"); } catch { }
            return false;
        }

        foreach (string path in effectiveList)
        {
            Debug.Log($"[ExcelHelper] 导表路径：{path}");
            AppendExportLog("  导表路径: " + path);
        }

        // 跨表引用需要基于全部 Excel 构建 ID 索引，不能只扫描本次需要更新的文件
        var allExcelPaths = GetAllExcelPaths();
        var validationErrors = ValidateExcelFiles(allExcelPaths.Count > 0 ? allExcelPaths : effectiveList);
        if (validationErrors.Count > 0)
        {
            AppendExportLog($"导表中止：校验失败 {validationErrors.Count} 条");
            ShowValidationErrors(validationErrors);
            return false;
        }

        try
        {
            ExportClass(allExcelPaths.Count > 0 ? allExcelPaths : effectiveList);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            AppendExportLog("导表失败：构建 Id 索引异常 " + e.Message);
            try { TipManager.Instance.ShowTip("导表失败：构建 Id 索引异常"); } catch { }
            return false;
        }

        if (!ExportData(effectiveList))
        {
            AppendExportLog("导表失败：数据文件导出失败");
            return false;
        }

        AppendExportLog("导表完成");
        return true;
    }

    /// <summary>
    /// 导出前基础校验：空 Id、重复 Id、跨表引用不存在、基础数值不合法。
    /// 返回错误列表；Editor 入口可弹 EditorUtility，运行时入口用 TipManager。
    /// </summary>
    public static List<string> ValidateExcelFiles(List<string> excelPaths)
    {
        var errors = new List<string>();
        var tableIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seenIds = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var rows = new List<(string file, string sheet, int row, string rowId, string field, string type, string value)>();
        int ignoredEmptyIdRows = 0;
        int ignoredUnitPlaceholderRows = 0;
        if (excelPaths == null) return errors;

        foreach (var path in excelPaths)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("$") || !File.Exists(path)) continue;
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var reader = ExcelReaderFactory.CreateReader(file);
                    foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                    {
                        if (sheet.TableName.StartsWith("#")) continue;
                        if (!tableIds.ContainsKey(sheet.TableName))
                        {
                            tableIds[sheet.TableName] = new List<string>();
                            seenIds[sheet.TableName] = new Dictionary<string, string>(StringComparer.Ordinal);
                        }
                        if (sheet.TableName == "UnitData" && !seenIds[sheet.TableName].ContainsKey("0"))
                        {
                            tableIds[sheet.TableName].Add("0");
                            seenIds[sheet.TableName]["0"] = "<UnitData placeholder>";
                        }

                        // 方案 A：反射枚举 Units/Skills/Buffs/Modifys/Bullets 下的可实例化子类，
                        // 不依赖易漂移的文字白名单。
                        HashSet<string> validTypes = null;
                        int typeColumn = -1;
                        if (ConfigTypeRegistry.IsTypeTable(sheet.TableName))
                        {
                            if (!ConfigTypeRegistry.TryGetValidTypes(sheet.TableName, out validTypes))
                            {
                                errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\"：Type 注册表为空或反射枚举失败，无法校验 Type（请检查程序集、命名空间或 IL2CPP link.xml）。");
                                validTypes = null;
                            }
                            else
                            {
                                for (int c = 1; c < sheet.Columns.Count; c++)
                                {
                                    if (string.Equals(GetCellString(sheet, 1, c).Trim(), "Type", StringComparison.Ordinal))
                                    {
                                        typeColumn = c;
                                        break;
                                    }
                                }

                                if (typeColumn < 0)
                                {
                                    errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\"：缺少字段 \"Type\"（第 2 行），无法校验 Type。");
                                }
                            }
                        }

                        int columns = sheet.Columns.Count;
                        for (int r = 3; r < sheet.Rows.Count; r++)
                        {
                            string id = GetCellString(sheet, r, 0);
                            bool hasData = false;
                            for (int c = 0; c < columns; c++)
                                if (!string.IsNullOrEmpty(GetCellString(sheet, r, c))) { hasData = true; break; }

                            if (string.IsNullOrEmpty(id))
                            {
                                // 空 Id 行通常是编辑 Excel 时的临时/草稿数据；导出会跳过，
                                // 不属于阻断性错误。
                                if (hasData) ignoredEmptyIdRows++;
                                continue;
                            }
                            if (id.StartsWith("#")) continue;

                            // UnitData 的 Id=0 是导出器内置的占位行（0.txt），
                            // Excel 里保留它只是为了手工查看/编辑；导出时会忽略，
                            // 不能把它算成“重复 ID”。
                            if (sheet.TableName == "UnitData" && id == "0")
                            {
                                ignoredUnitPlaceholderRows++;
                                continue;
                            }

                            if (seenIds[sheet.TableName].TryGetValue(id, out var old))
                                errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\" → Id \"{id}\" → 第 {r + 1} 行：重复 ID（首次出现于 {old}）");
                            else
                            {
                                seenIds[sheet.TableName][id] = $"{Path.GetFileName(path)}:{r + 1}";
                                tableIds[sheet.TableName].Add(id);
                            }

                            // 方案 A：Type 合法性由反射注册表校验；空值/空白也在这里覆盖，
                            // 因为后面的 rows 收集会跳过空字段。
                            if (validTypes != null && typeColumn >= 0)
                            {
                                string typeValue = GetCellString(sheet, r, typeColumn);
                                if (string.IsNullOrEmpty(typeValue))
                                {
                                    errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\" → Id \"{id}\" → 第 {r + 1} 行 → 字段 \"Type\"：Type 为空，运行时无法反射创建对应子类。");
                                }
                                else if (typeValue != typeValue.Trim())
                                {
                                    errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\" → Id \"{id}\" → 第 {r + 1} 行 → 字段 \"Type\"：Type \"{typeValue}\" 首尾包含空白，运行时将按原值查找而失败。");
                                }
                                else if (!validTypes.Contains(typeValue))
                                {
                                    string typeNamespace = ConfigTypeRegistry.GetTypeNamespace(sheet.TableName);
                                    errors.Add($"Excel \"{Path.GetFileName(path)}\" → Sheet \"{sheet.TableName}\" → Id \"{id}\" → 第 {r + 1} 行 → 字段 \"Type\"：Type \"{typeValue}\" 在 {typeNamespace} 命名空间下不存在；如为新增子类，请检查类名/命名空间，或补充 ConfigTypeRegistry 的别名/排除配置。");
                                }
                            }

                            for (int c = 0; c < columns; c++)
                            {
                                string fieldName = GetCellString(sheet, 1, c);
                                string fieldType = GetCellString(sheet, 2, c);
                                string value = GetCellString(sheet, r, c);
                                if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldType) || string.IsNullOrEmpty(value)) continue;
                                rows.Add((path, sheet.TableName, r + 1, id, fieldName, fieldType, value));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                errors.Add($"{Path.GetFileName(path)}：读取失败 {e.Message}");
            }
        }

        foreach (var r in rows)
        {
            string baseType = GetBaseType(r.type);
            bool isArray = IsArrayType(r.type);

            // 跨表引用优先：引用字段的值是目标表 Id，不按普通基础类型处理。
            if (tableIds.ContainsKey(baseType))
            {
                string[] refs = isArray
                    ? r.value.Split(new[] { ',', '\n' }, StringSplitOptions.None)
                    : new[] { r.value };

                if (isArray && refs.Any(raw => raw.Trim().Length == 0))
                    errors.Add($"Excel \"{Path.GetFileName(r.file)}\" → Sheet \"{r.sheet}\" → Id \"{r.rowId}\" → 字段 \"{r.field}\"：引用列表存在空项（第 {r.row} 行）");

                foreach (var raw in refs)
                {
                    string id = raw.Trim();
                    if (id.Length > 0 && !tableIds[baseType].Contains(id))
                        errors.Add($"Excel \"{Path.GetFileName(r.file)}\" → Sheet \"{r.sheet}\" → Id \"{r.rowId}\" → 字段 \"{r.field}\"：无效 ID \"{id}\"（目标表 {baseType} 中不存在）");
                }
                continue;
            }

            // Dictionary/Data 字段：校验 JSON 语法和顶层对象。
            if (IsJsonField(r.field, baseType))
            {
                string jsonError = ValidateJsonField(baseType, r.value);
                if (!string.IsNullOrEmpty(jsonError))
                    errors.Add($"Excel \"{Path.GetFileName(r.file)}\" → Sheet \"{r.sheet}\" → Id \"{r.rowId}\" → 字段 \"{r.field}\"：{jsonError}（第 {r.row} 行）");
                continue;
            }

            // 基础类型：int/float/bool、数组、Vector/Rect、可空类型。
            string basicError = ValidateBasicField(r.type, r.value);
            if (!string.IsNullOrEmpty(basicError))
                errors.Add($"Excel \"{Path.GetFileName(r.file)}\" → Sheet \"{r.sheet}\" → Id \"{r.rowId}\" → 字段 \"{r.field}\"：{basicError}（第 {r.row} 行）");
        }

        if (ignoredEmptyIdRows > 0)
            Debug.Log($"[ExcelHelper] 忽略 {ignoredEmptyIdRows} 行空 Id 临时数据（导出时会跳过）");
        if (ignoredUnitPlaceholderRows > 0)
            Debug.Log($"[ExcelHelper] 忽略 {ignoredUnitPlaceholderRows} 行 UnitData Id=0 占位数据（导出器会生成 0.txt）");

        dic = tableIds;
        return errors;
    }

    // ---------------------------------------------------------------------
    // 字段类型校验辅助函数（JSON / 数组 / Vector / Rect / 可空类型）
    // ---------------------------------------------------------------------

    private static string NormalizeTypeToken(string type)
    {
        string result = (type ?? "").Trim();
        while (result.EndsWith("?", StringComparison.Ordinal))
            result = result.Substring(0, result.Length - 1).Trim();
        return result;
    }

    private static bool IsArrayType(string type)
    {
        return NormalizeTypeToken(type).EndsWith("[]", StringComparison.Ordinal);
    }

    private static string GetBaseType(string type)
    {
        string result = NormalizeTypeToken(type);
        if (result.EndsWith("[]", StringComparison.Ordinal))
            result = result.Substring(0, result.Length - 2).Trim();
        return NormalizeTypeToken(result);
    }

    private static string[] SplitArrayValue(string value)
    {
        return (value ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split(new[] { ',', '\n' }, StringSplitOptions.None);
    }

    private static bool IsJsonField(string fieldName, string baseType)
    {
        string type = NormalizeTypeToken(baseType);
        if (type.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (string.Equals(type, "Data", StringComparison.Ordinal))
            return true;
        if (string.Equals((fieldName ?? "").Trim(), "Data", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(type, "object", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "System.Object", StringComparison.OrdinalIgnoreCase)
                || type.StartsWith("System.Collections", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static string ValidateJsonField(string baseType, string value)
    {
        string json = value;
        if (string.Equals(NormalizeTypeToken(baseType), "Data", StringComparison.Ordinal))
            json = "{" + value + "}";

        if (!StrictJsonValidator.TryValidateObject(json, out string strictError))
            return "JSON 非法：" + strictError;
        return null;
    }

    private static bool TryParseIntegerValue(string value)
    {
        long parsed;
        return long.TryParse(
            (value ?? "").Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out parsed);
    }

    private static bool TryParseFloatValue(string value)
    {
        double parsed;
        return double.TryParse(
                   (value ?? "").Trim(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out parsed)
               && !double.IsNaN(parsed)
               && !double.IsInfinity(parsed);
    }

    private static bool TryParseBoolValue(string value)
    {
        string v = (value ?? "").Trim().ToLowerInvariant();
        return v == "0" || v == "1" || v == "true" || v == "false";
    }

    private static string ValidateNumericArrayValue(string baseType, string value, bool integer)
    {
        string[] parts = SplitArrayValue(value);
        if (parts.Any(part => part.Trim().Length == 0))
            return $"{baseType}[] 存在空元素（值：\"{value}\"）";

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            bool ok = integer ? TryParseIntegerValue(part) : TryParseFloatValue(part);
            if (!ok)
            {
                string expected = integer ? "整数" : "浮点数";
                return $"{baseType}[] 第 {i + 1} 个元素不是合法{expected}（值：\"{part}\"）";
            }
        }
        return null;
    }

    private static string ValidateBoolArrayValue(string baseType, string value)
    {
        string[] parts = SplitArrayValue(value);
        if (parts.Any(part => part.Trim().Length == 0))
            return $"{baseType}[] 存在空元素（值：\"{value}\"）";

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (!TryParseBoolValue(part))
                return $"{baseType}[] 第 {i + 1} 个元素不是合法 bool（只接受 0/1/true/false，值：\"{part}\"）";
        }
        return null;
    }

    private static string ValidateVectorValue(
        string typeName,
        int expectedComponents,
        bool integerComponents,
        string value)
    {
        string[] parts = (value ?? "").Split(',');
        if (parts.Length != expectedComponents)
            return $"{typeName} 需要 {expectedComponents} 个分量，实际 {parts.Length} 个（值：\"{value}\"）";

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            bool ok = integerComponents ? TryParseIntegerValue(part) : TryParseFloatValue(part);
            if (!ok)
            {
                string expected = integerComponents ? "整数" : "数字";
                return $"{typeName} 第 {i + 1} 个分量不是合法{expected}（值：\"{part}\"）";
            }
        }
        return null;
    }

    private static string ValidateVectorArrayValue(
        string typeName,
        int expectedComponents,
        bool integerComponents,
        string value)
    {
        string[] points = (value ?? "").Split('#');
        if (points.Any(point => point.Trim().Length == 0))
            return $"{typeName}[] 存在空点（值：\"{value}\"）";

        for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            string[] parts = points[pointIndex].Split(',');
            if (parts.Length != expectedComponents)
                return $"{typeName}[] 第 {pointIndex + 1} 个点需要 {expectedComponents} 个分量，实际 {parts.Length} 个（值：\"{points[pointIndex]}\"）";

            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                string part = parts[partIndex].Trim();
                bool ok = integerComponents ? TryParseIntegerValue(part) : TryParseFloatValue(part);
                if (!ok)
                {
                    string expected = integerComponents ? "整数" : "数字";
                    return $"{typeName}[] 第 {pointIndex + 1} 个点第 {partIndex + 1} 个分量不是合法{expected}（值：\"{part}\"）";
                }
            }
        }
        return null;
    }

    private static string ValidateBasicField(string rawType, string value)
    {
        string type = NormalizeTypeToken(rawType);
        bool isArray = type.EndsWith("[]", StringComparison.Ordinal);
        string baseType = GetBaseType(type);

        if (StringTypes.Contains(baseType) || NoCheckTypes.Contains(baseType))
            return null;

        if (VectorComponents.TryGetValue(baseType, out int components))
        {
            bool integerComponents = IntegerVectorTypes.Contains(baseType);
            return isArray
                ? ValidateVectorArrayValue(baseType, components, integerComponents, value)
                : ValidateVectorValue(baseType, components, integerComponents, value);
        }

        if (IntegerTypes.Contains(baseType))
        {
            if (isArray) return ValidateNumericArrayValue(baseType, value, true);
            return TryParseIntegerValue(value) ? null : $"不是合法整数（值：\"{value}\"）";
        }

        if (FloatTypes.Contains(baseType))
        {
            if (isArray) return ValidateNumericArrayValue(baseType, value, false);
            return TryParseFloatValue(value) ? null : $"不是合法浮点数（值：\"{value}\"）";
        }

        if (BoolTypes.Contains(baseType))
        {
            if (isArray) return ValidateBoolArrayValue(baseType, value);
            return TryParseBoolValue(value) ? null : $"不是合法 bool（只接受 0/1/true/false，值：\"{value}\"）";
        }

        // 枚举值域、未知类型、object[]/string[] 等不在本轮基础类型检查范围内。
        return null;
    }


    /// <summary>
    /// 游戏内置数据导出工具的通用运行日志（除 Console 外再落一份文件）。
    /// 路径统一由 PathHelper.ExportLogPath 决定：
    ///   编辑器 = 工程/Logs
    ///   非编辑器桌面 = StreamingAssets/Logs
    ///   移动端 = persistentDataPath/&lt;productName&gt;/Logs
    /// </summary>
    public static void AppendExportLog(string message)
    {
        try
        {
            string dir = PathHelper.ExportLogPath;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "ExcelExport.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // 日志写盘失败不能影响导表本身
        }
    }

    public static void ShowValidationErrors(List<string> errors)
    {
        if (errors == null || errors.Count == 0) return;
        string full = string.Join("\n", errors);
        Debug.LogError($"[ExcelHelper] 配置校验失败，共 {errors.Count} 个问题：\n{full}");

        string reportPath = null;
        try
        {
            string dir = PathHelper.ExportLogPath;
            Directory.CreateDirectory(dir);
            reportPath = Path.Combine(dir, "ExcelValidationReport.txt");
            File.WriteAllLines(reportPath, errors);
        }
        catch { }

        try { TipManager.Instance.ShowTip($"配置校验失败：{errors.Count} 个问题，详见 Console" + (reportPath != null ? "：" + reportPath : "")); }
        catch { }
    }

    /// <summary>
    /// 追加引用列的稳定字符串 Id 字段，同时保留旧 int 索引字段：
    ///   LastingEffect -> "LastingEffect": 154, "LastingEffectId": "重构体加攻特效"
    ///   Buffs -> "Buffs": [...], "BuffsIds": ["...", ...]
    /// </summary>
    public static void AppendReferenceIdFields(StringBuilder sb, string fieldType, string fieldName, string fieldValue)
    {
        if (sb == null || string.IsNullOrEmpty(fieldType) || string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldValue)) return;
        bool isArray = fieldType.EndsWith("[]");
        string baseType = isArray ? fieldType.Substring(0, fieldType.Length - 2) : fieldType;
        if (!dic.ContainsKey(baseType)) return;

        if (!isArray)
        {
            sb.Append($"\"{EscapeJson(fieldName)}Id\":\"{EscapeJson(fieldValue.Trim())}\",");
            return;
        }

        var vals = fieldValue.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        sb.Append($"\"{EscapeJson(fieldName)}Ids\":[");
        for (int i = 0; i < vals.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{EscapeJson(vals[i].Trim())}\"");
        }
        sb.Append("],");
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static List<string> GetAllExcelPaths()
    {
        var result = new List<string>();
        foreach (var folder in Database.Instance.GetExcelPathList())
        {
            result.AddRange(Database.Instance.GetExcelFileList(folder));
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static void AddExportError(string message)
    {
        lock (exportErrorLock)
        {
            exportErrors.Add(message);
        }
    }

    private static void FlushExportErrors()
    {
        lock (exportErrorLock)
        {
            foreach (var message in exportErrors)
                TipManager.Instance.ShowTip(message);
            exportErrors.Clear();
        }
    }

    /// <summary>
    /// 过滤并去重 Excel 路径。
    /// 编辑器下保留全部可用路径；构建/真机下只保留位于热更目录（AppHotfixResPath）内的 Excel，
    /// 避免存档中残留的项目开发目录路径（如 D:\UnityWork\zhou-master\Excel）与运行时热更新目录重复导表。
    /// </summary>
    private static List<string> FilterAndDeduplicateExcelPaths(List<string> ExcelList)
    {
        var result = new List<string>();
        if (ExcelList == null || ExcelList.Count == 0) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string hotfixRoot = PathHelper.NormalizeAppPath(PathHelper.AppHotfixResPath);

        foreach (string path in ExcelList)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("$")) continue;

            string fullPath = PathHelper.NormalizeAppPath(path);

            // 构建/真机环境：只保留热更目录下的 Excel；编辑器保留全部
            if (!Application.isEditor && hotfixRoot != null)
            {
                bool inside = fullPath.StartsWith(hotfixRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
                              || fullPath.Equals(hotfixRoot, StringComparison.OrdinalIgnoreCase);
                if (!inside)
                {
                    Debug.LogWarning($"[ExcelHelper] 忽略非热更目录 Excel 路径：{path}");
                    continue;
                }
            }

            if (seen.Add(fullPath))
                result.Add(fullPath);
        }

        return result;
    }

    public static void ExportClass(List<string> ExcelList)
    {
        dic.Clear();
        //读取所有表的Id，用于转换索引
        foreach (var path in ExcelList)
        {
            if (path.Contains("$")) continue;
            ExcelDataReader.IExcelDataReader reader;
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                reader = ExcelDataReader.ExcelReaderFactory.CreateReader(file);
                foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                {
                    if (sheet.TableName.StartsWith("#")) continue;
                    if (!dic.ContainsKey(sheet.TableName))
                    {
                        List<string> Ids = new List<string>();
                        // UnitData 的特殊占位行 Id=0 会由 ExportData 置顶到文件首位，这里必须先占索引 0
                        if (sheet.TableName == "UnitData")
                            Ids.Add("0");

                        for (int i = 3; i < sheet.Rows.Count; i++)
                        {
                            var Id = GetCellString(sheet, i, 0);
                            if (string.IsNullOrEmpty(Id) || Id.StartsWith("#")) continue;
                            if (sheet.TableName == "UnitData" && Id == "0") continue;
                            Ids.Add(Id);
                        }
                        dic.Add(sheet.TableName, Ids);
                    }
                    else
                    {
                        List<string> Ids = dic[sheet.TableName];
                        for (int i = 3; i < sheet.Rows.Count; i++)
                        {
                            var Id = GetCellString(sheet, i, 0);
                            if (string.IsNullOrEmpty(Id) || Id.StartsWith("#")) continue;
                            if (sheet.TableName == "UnitData" && Id == "0") continue;
                            Ids.Add(Id);
                        }
                    }
                }
            }
        }
    }
    private static bool ExportData(List<string> ExcelList)
        {
            return ExportData(ExcelList, PathHelper.AppHotfixResPath + "/Data/");
        }

    private static bool ExportData(List<string> ExcelList, string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir))
                dataDir = PathHelper.AppHotfixResPath + "/Data/";
            Directory.CreateDirectory(dataDir);

            // 先写入临时目录，只有所有 Excel 都成功后才覆盖正式 Data 目录，
            // 避免单个文件失败时留下“部分新数据 + 部分旧数据”的不一致状态。
            string dataRoot = Path.GetDirectoryName(
                dataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(dataRoot))
                dataRoot = PathHelper.AppHotfixResPath;

            string stagingDir = Path.Combine(
                dataRoot,
                "Data_ExportTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDir);

            int failedFiles = 0;
            try
            {
                lock (exportErrorLock)
                {
                    exportErrors.Clear();
                }

                // 并行导出每个 Excel 文件到临时目录。
                Parallel.ForEach(ExcelList.ToArray(), path =>
                {
                    if (path.Contains("$")) return;
                    if (!Export(path, stagingDir))
                        Interlocked.Increment(ref failedFiles);
                });

                bool hasCollectedErrors;
                lock (exportErrorLock)
                {
                    hasCollectedErrors = exportErrors.Count > 0;
                }
                FlushExportErrors();

                if (failedFiles > 0 || hasCollectedErrors)
                {
                    AppendExportLog($"导表失败：{failedFiles} 个文件导出异常，本次未写入正式 Data 目录");
                    return false;
                }

                // 全部成功后再复制到正式 Data 目录；只覆盖本次选中的 Excel 对应文件。
                string[] stagedFiles = Directory.GetFiles(stagingDir, "*.txt", SearchOption.AllDirectories);
                foreach (string sourceFile in stagedFiles)
                {
                    string relativePath = sourceFile
                        .Substring(stagingDir.Length)
                        .TrimStart('/', '\\');
                    string targetFile = Path.Combine(dataDir, relativePath);
                    string targetDir = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDir))
                        Directory.CreateDirectory(targetDir);
                    File.Copy(sourceFile, targetFile, true);
                }

                // 生成每个类型的索引文件，保证多文件加载后索引顺序与旧版一致。
                foreach (var kv in dic)
                {
                    string folder = Path.Combine(dataDir, kv.Key);
                    Directory.CreateDirectory(folder);
                    File.WriteAllLines(Path.Combine(folder, "_index.txt"), kv.Value);

                    if (kv.Key == "UnitData")
                    {
                        string placeholder = Path.Combine(folder, "0.txt");
                        if (!File.Exists(placeholder))
                            File.WriteAllText(placeholder, "{\"Id\":\"0\",\"Hp\":1}\n");
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                AddExportError($"导出数据文件失败: {e.Message}");
                FlushExportErrors();
                AppendExportLog("导表失败：" + e.Message);
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingDir))
                        Directory.Delete(stagingDir, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ExcelHelper] 清理临时导表目录失败: {stagingDir}\n{e.Message}");
                }
            }
        }

    private static bool Export(string fileName, string dataDir)
        {
            try
            {
                using (FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(file);
                    foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                    {
                        if (sheet.TableName.StartsWith("#")) continue;

                        string folder = Path.Combine(dataDir, sheet.TableName);
                        Directory.CreateDirectory(folder);
                        string outputFile = Path.Combine(
                            folder,
                            Path.GetFileNameWithoutExtension(fileName) + ".txt");

                        StringBuilder sb = new StringBuilder();
                        int cellCount = sheet.Columns.Count;
                        bool isUnitData = sheet.TableName == "UnitData";
                        for (int i = 3; i < sheet.Rows.Count; i++)
                        {
                            string Id = GetCellString(sheet, i, 0);
                            if (string.IsNullOrEmpty(Id) || Id.StartsWith("#"))
                                continue;
                            if (isUnitData && Id == "0")
                                continue;

                            sb.Append("{");
                            for (int j = 0; j < cellCount; j++)
                            {
                                string fieldName = GetCellString(sheet, 1, j);
                                string fieldType = GetCellString(sheet, 2, j);
                                if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldType))
                                    continue;

                                string fieldValue = GetCellString(sheet, i, j);
                                if (string.IsNullOrEmpty(fieldValue))
                                    continue;

                                sb.Append($"\"{fieldName}\":{Convert(fieldType, fieldValue, fileName, sheet.TableName, fieldName)},");
                                AppendReferenceIdFields(sb, fieldType, fieldName, fieldValue);
                            }
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append("}\n");
                        }

                        sb.Append("\n");
                        if (sb.Length > 1) sb.Remove(sb.Length - 1, 1);
                        File.WriteAllText(outputFile, sb.ToString());
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                AddExportError($"导出失败: {fileName} -> {e.Message}");
                return false;
            }
        }

        private static string GetCellString(System.Data.DataTable sheet, int i, int j)
        {
            try
            {
                return sheet.Rows[i][j].ToString();
            }
            catch
            {
                return "";
            }
        }

    private static string Convert(string type, string value, string path, string sheetName, string fieldName)
    {
        try
        {
            StringBuilder sbCache = new StringBuilder();
            if (type.EndsWith("Enum[]"))
            {
                Type t = typeof(Init).Assembly.GetType(type.Substring(0, type.Length - 2));
                sbCache.Clear();
                string[] sp = value.Split(',');
                foreach (string s in sp) sbCache.Append((int)Enum.Parse(t, s) + ",");
                sbCache.Remove(sbCache.Length - 1, 1);
                return $"[{sbCache.ToString()}]";
            }
            if (type.EndsWith("Enum"))
            {
                try
                {
                    Type t = typeof(Init).Assembly.GetType(type);
                    return ((int)Enum.Parse(t, value)).ToString();
                }
                catch
                {
                    return value;
                }
            }

            if (dic.ContainsKey(type))
            {
                int index = dic[type].IndexOf(value);
                if (index == -1) throw new Exception(value);
                return index.ToString();
            }

            if (dic.ContainsKey(type.TrimEnd("[]".ToCharArray())))
            {
                sbCache.Clear();
                sbCache.Append("[");
                string[] sp = value.Split(',');
                foreach (var s in sp)
                {
                    int index = dic[type.TrimEnd("[]".ToCharArray())].IndexOf(s);
                    if (index == -1) throw new Exception(value);
                    sbCache.Append($"{index},");
                }
                if (sbCache.Length > 1) sbCache.Remove(sbCache.Length - 1, 1);
                sbCache.Append("]");
                return sbCache.ToString();
            }

            switch (type)
            {
                case "int[]":
                case "int32[]":
                case "long[]":
                case "object[]":
                    return $"[{value}]";
                case "string[]":
                    sbCache.Clear();
                    string[] sp = value.Split(',');
                    if (sp.Length == 1) sp = value.Split('\n');//如果用,分隔失败，尝试用回车分隔
                    foreach (string s in sp) sbCache.Append($"\"{s}\",");
                    sbCache.Remove(sbCache.Length - 1, 1);
                    return $"[{sbCache.ToString()}]";
                case "int":
                case "int32":
                case "int64":
                case "long":
                case "float":
                case "double":
                    return value;
                case "string":
                    return $"\"{value}\"";
                case "bool":
                    return value;
                case "Data":
                    return $"{{{value}}}";
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector2Int":
                    sp = value.Split(',');
                    return $"{{\"x\":{sp[0]},\"y\":{sp[1]}}}";
                case "UnityEngine.Vector3":
                case "UnityEngine.Vector3Int":
                    sp = value.Split(',');
                    return $"{{\"x\":{sp[0]},\"y\":{sp[1]},\"z\":{sp[2]}}}";
                case "UnityEngine.Vector2[]":
                case "UnityEngine.Vector2Int[]":
                    sbCache.Clear();
                    sp = value.Split('#');
                    for (int i = 0; i < sp.Length; i++)
                    {
                        var sp1 = sp[i].Split(',');
                        sbCache.Append($"{{\"x\":{sp1[0]},\"y\":{sp1[1]}}}");
                        if (i != sp.Length - 1) sbCache.Append(",");
                    }
                    return $"[{sbCache}]";
                case "UnityEngine.Vector3[]":
                case "UnityEngine.Vector3Int[]":
                    sbCache.Clear();
                    sp = value.Split('#');
                    for (int i = 0; i < sp.Length; i++)
                    {
                        var sp1 = sp[i].Split(',');
                        sbCache.Append($"{{\"x\":{sp1[0]},\"y\":{sp1[1]},\"z\":{sp1[2]}}}");
                        if (i != sp.Length - 1) sbCache.Append(",");
                    }
                    return $"[{sbCache}]";
                case "UnityEngine.Rect[]":
                    sbCache.Clear();
                    sp = value.Split('#');
                    for (int i = 0; i < sp.Length; i++)
                    {
                        var sp1 = sp[i].Split(',');
                        sbCache.Append($"{{\"x\":{sp1[0]},\"y\":{sp1[1]},\"width\":{sp1[2]},\"height\":{sp1[3]}}}");
                        if (i != sp.Length - 1) sbCache.Append(",");
                    }
                    return $"[{sbCache}]";
                case "UnityEngine.Rect":
                    sp = value.Split(',');
                    return $"{{\"x\":{sp[0]},\"y\":{sp[1]},\"width\":{sp[2]},\"height\":{sp[3]}}}";
                default:
                    //Debug.LogWarning($"unexcpeted type:{type}");
                    if (type.EndsWith("[]"))
                    {
                        return $"[{value}]";
                    }
                    return value;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"导表于{path}中的{sheetName}的{fieldName}字段发生错误");
            Debug.LogError(e);
            Debug.Log(e.Message);
            AddExportError($"导表错误{e.Message}于{path}中的{sheetName}的{fieldName}字段");
            throw e;
        }
    }

#if !UNITY_ANDROID
    public static string CreatExcel(string path, string sheetName, List<(int row, int col, string data)> datas,string sourceExcel = "", int sourceRow = 0, bool isOp = false)
    {
        string tempPath = Application.streamingAssetsPath + "/Excel/temp.xlsx";
        string tmpPath = Application.streamingAssetsPath + "/Excel/tmp.xlsx";
        using ExcelPackage tmp = new ExcelPackage(new FileInfo(tempPath)) ;
        tmp.SaveAs(new FileInfo(tmpPath));
        if (sourceExcel != "")
        {
            //Debug.Log("复制行");
            //Debug.Log(sourceExcel);
            //Debug.Log(sheetName);
            //Debug.Log(sourceRow);
            CopyRowCrossExcel(sourceExcel, sheetName, sourceRow, tmpPath, 4);
        }
        try
        {
            using (ExcelPackage package = new ExcelPackage(new FileInfo(tmpPath)))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
                string unitId = "";
                string unitName = "";
                //string isOp = "";
                foreach ((int row, int col, string data) data in datas)
                {
                    if (data.data == "")
                        continue;
                    if (data.row != 0 && data.col != 0)
                        worksheet.Cells[4, data.col].Value = data.data;
                    if (sheetName == "UnitData" && data.col == 33)
                        unitName = data.data;
                    if (sheetName == "UnitData" && data.col == 1)
                        unitId = data.data;
                    //if (data.row == 0 && data.col == 0)
                        //isOp = data.data;
                }
                if (unitId != "" && isOp)
                {
                    ExcelWorksheet cardSheet = package.Workbook.Worksheets["CardData"];
                    cardSheet.Cells[4, 2].Value = unitId;
                    cardSheet.Cells[4, 1].Value = unitName;
                }
                package.SaveAs(new FileInfo(path));
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"写入失败\n{e.Message}\n{path}可能被占用";
        }
        return "写入成功";
    }
    public static string ModifyExcel(string path, string sheetName, List<(int row, int col, string data)> datas, int sourceRow = 0, bool isOp = false)
    {
        try
        {
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
                string newUnitName = "";
                string unitId = "";
                if (sourceRow != 0)
                {
                    CopyRow(worksheet, sourceRow, datas[0].row);
                    string sourceId = worksheet.Cells[sourceRow, 1].Value.ToString();
                    string sourceName = worksheet.Cells[sourceRow, 33].Value.ToString();
                    if (isOp)
                    {
                        ExcelWorksheet cardSheet = package.Workbook.Worksheets["CardData"];
                        cardSheet.Cells[cardSheet.Dimension.Rows + 1, 2].Value = sourceId;
                        cardSheet.Cells[cardSheet.Dimension.Rows + 1, 1].Value = "新单位_" + sourceName;
                    }
                }
                foreach ((int row, int col, string data) data in datas)
                {
                    int row = data.row;
                    int col = data.col;
                    if (row != 0 && col != 0)
                        worksheet.Cells[row, col].Value = data.data;
                    else
                        unitId = data.data;
                    if (sheetName == "UnitData" && col == 33)
                        newUnitName = data.data;
                }
                if (unitId != "" && isOp)
                {
                    var cardSheet = package.Workbook.Worksheets["CardData"];
                    for (int i = 4; i <= cardSheet.Dimension.Rows; i++)
                    {
                        if (cardSheet.Cells[i, 2].Value?.ToString() == unitId)
                        {
                            cardSheet.Cells[i, 1].Value = newUnitName;
                            break;
                        }
                    }
                }
                //worksheet.Cells[pos.row, pos.col].Value = data;
                package.Save();
                return "写入成功";
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"写入失败\n{e.Message}\n{path}可能被占用";
        }
    }



    public static (int row, int col) GetCellPos(string path, string sheetName, string name, string id)
    {
        try
        {
            int row = -1;
            int col = -1;
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
                for (int i = 1; i <= worksheet.Dimension.Columns; i++)
                {
                    if (worksheet.Cells[2, i].Value?.ToString() == id)
                    {
                        col = i;
                        break;
                    }
                }
                for (int i = 4; i <= worksheet.Dimension.Rows; i++)
                {
                    if (worksheet.Cells[i, 1].Value?.ToString() == name)
                    {
                        row = i;
                        break;
                    }
                }
            }
            return (row, col);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return (-1, -1);
        }
    }
    public static Dictionary<(string name, string type, string icon), int> GetUnitList(string path)
    {
        int sourceRowIndex = 5;
        try
        {
            //Debug.Log("开始读取单位列表" + path);
            Dictionary<(string, string, string), int> unitList = new Dictionary<(string name, string type, string icon), int>();
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            ExcelWorksheet worksheet = package.Workbook.Worksheets["UnitData"];
            for (; sourceRowIndex <= worksheet.Dimension.Rows; sourceRowIndex++)
            {
                if (worksheet.Cells[sourceRowIndex, 1].Value?.ToString().StartsWith("#") == true || worksheet.Cells[sourceRowIndex, 1].Value == null)
                {
                    continue;
                }
                else
                {
                    //Debug.Log(worksheet.Cells[i, 1].Value.ToString());
                    //Debug.Log(worksheet.Cells[i, 33].Value.ToString());
                    //Debug.Log(worksheet.Cells[i, 2].Value.ToString());
                    //Debug.Log(worksheet.Cells[i, 56].Value.ToString());
                    (string, string, string) unitInfo = (
                        worksheet.Cells[sourceRowIndex, 33].Value.ToString() + "/" + worksheet.Cells[sourceRowIndex, 1].Value.ToString(),
                        worksheet.Cells[sourceRowIndex, 2].Value.ToString(),
                        worksheet.Cells[sourceRowIndex, 55].Value?.ToString()?? ""
                        );
                    unitList[unitInfo] = sourceRowIndex;
                }
            }
            return unitList;
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            TipManager.Instance.ShowTip("读取单位列表失败:" + e.Message + "\n于" + path + " UnitData第" + sourceRowIndex + "行");
            return new Dictionary<(string name, string type, string icon), int>();
        }
    }
    public static string GetCellData(string path, string sheetName, (int row, int col) pos)
    {
        try
        {
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
            return worksheet.Cells[pos.row, pos.col].Value?.ToString() ?? "";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return "";
        }
    }
    public static int GetExcelRow(string path, string sheetName)
    {
        try
        {
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];
            int i = 1;
            for (int j = 5; j <=  worksheet.Dimension.Rows; j++)
            {
                if (worksheet.Cells[j, 1].Value?.ToString().StartsWith("#") == true || worksheet.Cells[j, 1].Value == null)
                {
                    continue;
                }
                else
                {
                    i = j;
                }
            }
            return i;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return 0;
        }
    }
    public static Dictionary<string, Dictionary<string, int>> GetAttributes(string path)
    {
        Dictionary<string, Dictionary<string, int>> attributes = new Dictionary<string, Dictionary<string, int>>();
        try
        {
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            foreach (var sheet in package.Workbook.Worksheets)
            {
                Dictionary<string, int> attribute = new Dictionary<string, int>();
                for (int i = 1; i <= sheet.Dimension.Columns; i++)
                {
                    string dataType = sheet.Cells[2, i].Value?.ToString()?? "";
                    if (dataType == "" || dataType.StartsWith("#") == true)
                    {
                        continue;
                    }
                    else
                    {
                        string attributeName = sheet.Cells[1, i].Value?.ToString()?? "";
                        string attributeType = sheet.Cells[3, i].Value?.ToString()?? "";
                        //Debug.Log(attributeName);
                        attribute[attributeName != "" ?
                            $"{attributeName}/{dataType}:{attributeType}" : $"{dataType}/{dataType}:{attributeType}"] = i;
                    }
                }
                attributes[sheet.Name] = attribute;
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        return attributes;
    }


    public static void CreateNewExcel(string path, string sheetName, List<(int row, int col, string data)> datas)
    {
        
    }
    public static void CopyRowCrossExcel(string sourcePath, string sheetName, int sourceRowIndex, string targetPath, int targetRowIndex)
    {
        using ExcelPackage sourcePackage = new ExcelPackage(new FileInfo(sourcePath));
        using ExcelPackage targetPackage = new ExcelPackage(new FileInfo(targetPath));

        ExcelWorksheet sourceWorksheet = sourcePackage.Workbook.Worksheets[sheetName];
        ExcelWorksheet targetWorksheet = targetPackage.Workbook.Worksheets[sheetName];
        // 获取源行和目标行对象
        ExcelRow sourceRow = sourceWorksheet.Row(sourceRowIndex);
        ExcelRow targetRow = targetWorksheet.Row(targetRowIndex);

        // 复制行高
        targetRow.Height = sourceRow.Height;

        // 遍历源行的所有单元格
        for (int col = 1; col <= sourceWorksheet.Dimension.Columns; col++)
        {
            ExcelRange sourceCell = sourceWorksheet.Cells[sourceRowIndex, col];
            ExcelRange targetCell = targetWorksheet.Cells[targetRowIndex, col];

            // 复制值
            //targetCell.Formula = sourceCell.Formula;  // 保留公式
            if (string.IsNullOrEmpty(sourceCell.Formula))
            {
                //Debug.Log(sourceCell.Value);
                targetCell.Value = sourceCell.Value;// 直接复制值
                //Debug.Log(targetCell.Value);
            }
        }
        targetPackage.Save();
    }
    public static void CopyRow(ExcelWorksheet worksheet, int sourceRowIndex, int targetRowIndex)
    {
        ExcelRow sourceRow = worksheet.Row(sourceRowIndex);
        ExcelRow targetRow = worksheet.Row(targetRowIndex);

        // 复制行高
        targetRow.Height = sourceRow.Height;

        // 遍历源行的所有单元格
        for (int col = 1; col <= worksheet.Dimension.Columns; col++)
        {
            ExcelRange sourceCell = worksheet.Cells[sourceRowIndex, col];
            ExcelRange targetCell = worksheet.Cells[targetRowIndex, col];

            // 复制值
            //targetCell.Formula = sourceCell.Formula;  // 保留公式
            if (string.IsNullOrEmpty(sourceCell.Formula))
            {
                targetCell.Value = sourceCell.Value;   // 直接复制值
            }
        }
    }

#endif
}
