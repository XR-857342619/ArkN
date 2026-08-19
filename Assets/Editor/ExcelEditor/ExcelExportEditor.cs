using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class ExcelExportEditor
{
    // 导出 Excel 根目录下所有 xlsx（含子目录），参考真机 ExcelHelper 的多 Excel 合并行为
    static string excelPath = "./Excel";
    static string exportPath = "./Assets/Bundles/Data/";
    static string scriptPath = "./Assets/Scripts/Config/";


    static Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>()
    {

    };

    [MenuItem("Tools/导出配置")]
    public static void ExportAll()
    {
        ExportClass();
        ExportData();
        AssetDatabase.Refresh();
        Debug.Log("导出结束");
    }

    public static void writeData(string fileName, string obj)
    {
        using (FileStream txt = new FileStream(exportPath + fileName, FileMode.Create))
        {
            using (StreamWriter sw = new StreamWriter(txt))
            {
                sw.Write(obj);
                sw.Close();
                txt.Close();
            }
        }
    }

    static void ExportClass()
    {
        dic.Clear();
        //读取所有表的Id，用于转换索引
        foreach (var path in Directory.GetFiles(excelPath, "*.xlsx", SearchOption.AllDirectories))
        {
            if (path.Contains("$") || IsInBackupFolder(path)) continue;
            IExcelDataReader reader;
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                reader = ExcelReaderFactory.CreateReader(file);
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
                        List<string>    Ids = dic[sheet.TableName];
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

        foreach (var path in Directory.GetFiles(excelPath, "*.xlsx", SearchOption.AllDirectories))
        {
            if (path.Contains("$") || IsInBackupFolder(path)) continue;
            IExcelDataReader reader;
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                reader = ExcelReaderFactory.CreateReader(file);
                foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                {
                    if (sheet.TableName.StartsWith("#")) continue;
                    using (FileStream txt = new FileStream(scriptPath + sheet.TableName + ".cs", FileMode.Create))
                    using (StreamWriter sw = new StreamWriter(txt))
                    {
                        sw.Write($"public class {sheet.TableName} : IConfig \n");
                        sw.Write("{\n");
                        sw.Write("      public string Id { get ; set ; }\n");
                        for (int i = 0; i < sheet.Columns.Count; i++)
                        {
                            var fieldInfo = GetCellString(sheet, 0, i);
                            if (fieldInfo.StartsWith("#") || GetCellString(sheet, 1, i) == "Id") continue;
                            string fieldType = GetCellString(sheet, 2, i);
                            if (string.IsNullOrEmpty(fieldType)) continue;
                            if (dic.ContainsKey(fieldType)) fieldType = "int?";
                            if (dic.ContainsKey(fieldType.TrimEnd("[]".ToCharArray()))) fieldType = "int[]";
                            sw.Write($"      public {fieldType} {GetCellString(sheet, 1, i)};\n");
                        }
                        sw.Write("}\n");
                        sw.Close();
                        txt.Close();
                    }
                }
            }
            //break;
        }
    }

    static void ExportData()
    {
        Directory.CreateDirectory(exportPath);
        // 先删除旧数据文件，并确保每个目标文件被清空，避免 Append 时叠加旧数据
        foreach (string key in dic.Keys)
        {
            string filePath = exportPath + key + ".txt";
            if (File.Exists(filePath))
                File.Delete(filePath);
            using (File.Create(filePath)) { }
        }

        // UnitData 需要保证 Id=0 的占位行始终位于文件首位（战斗会读取 UnitData[0]）
        if (dic.ContainsKey("UnitData"))
        {
            File.WriteAllText(exportPath + "UnitData.txt", "{\"Id\":\"0\",\"Hp\":1}\n");
        }

        foreach (var path in Directory.GetFiles(excelPath, "*.xlsx", SearchOption.AllDirectories))
        {
            if (path.Contains("$") || IsInBackupFolder(path)) continue;
            Export(path);
        }
    }

    static void Export(string fileName)
    {
        IExcelDataReader reader;
        using (FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            reader = ExcelReaderFactory.CreateReader(file);
            try
            {
                foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                {
                    //Debug.Log(sheet.TableName);
                    if (sheet.TableName.StartsWith("#")) continue;
                    StringBuilder sb = new StringBuilder();
                    int cellCount = sheet.Columns.Count;
                    bool isUnitData = sheet.TableName == "UnitData";
                    for (int i = 3; i < sheet.Rows.Count; i++)
                    {
                        string Id = GetCellString(sheet, i, 0);
                        if (string.IsNullOrEmpty(Id) || Id.StartsWith("#"))
                        {
                            continue;
                        }
                        // UnitData 的 Id=0 占位行已由 ExportData 置顶，这里忽略 Excel 中的相同内容
                        if (isUnitData && Id == "0")
                        {
                            continue;
                        }
                        sb.Append("{");
                        for (int j = 0; j < cellCount; j++)
                        {
                            string fieldName = GetCellString(sheet, 1, j);
                            string fieldType = GetCellString(sheet, 2, j);
                            if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldType))
                            {
                                continue;
                            }
                            string fieldValue = GetCellString(sheet, i, j);
                            if (string.IsNullOrEmpty(fieldValue))
                            {
                                continue;
                            }
                            sb.Append($"\"{fieldName}\":{Convert(fieldType, fieldValue)},");
                        }
                        sb.Remove(sb.Length - 1, 1);
                        sb.Append("}\n");
                    }
                    sb.Append("\n");
                    if (sb.Length > 1) sb.Remove(sb.Length - 1, 1);
                    //sb.Remove(sb.Length - 1, 1);
                    using (FileStream txt = new FileStream(exportPath + sheet.TableName + ".txt", FileMode.Append))
                    using (StreamWriter sw = new StreamWriter(txt))
                    {
                        if (sb.ToString() != "\n")
                        {
                            Debug.Log(sb.ToString());
                            sw.Write(sb.ToString());
                        }
                        sw.Close();
                        txt.Close();
                    }
                    sb.Clear();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            file.Close();
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
    static StringBuilder sbCache = new StringBuilder();

    private static string Convert(string type, string value)
    {
        //Debug.Log(type + " " + value);
        try
        {
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
                        sbCache.Append( $"{{\"x\":{sp1[0]},\"y\":{sp1[1]}}}");
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
            Debug.LogError(e);
            Debug.LogError($"type:{type},value:{value}");
            throw e;
        }
    }

    // 判断文件是否位于 ExcelSyncBackup 备份目录中（递归检查父目录）
    private static bool IsInBackupFolder(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (Path.GetFileName(directory).Equals("ExcelSyncBackup", StringComparison.OrdinalIgnoreCase))
                return true;

            // 避免相对路径逐级上升到空字符串时 Path.GetDirectoryName 抛 ArgumentException
            var parent = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent)) break;
            directory = parent;
        }
        return false;
    }
}