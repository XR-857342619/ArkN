using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Data;
using OfficeOpenXml;
using Excel;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Security;
using OfficeOpenXml.FormulaParsing.Excel.Functions.RefAndLookup;
using static EnemyInfoExcelTool;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;


public class ExcelHelper
{
    public static Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>()
    {

    };

    public static void Export(List<string> ExcelList)
    {
        ExportClass(ExcelList);
        ExportData(ExcelList);
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
                        for (int i = 3; i < sheet.Rows.Count; i++)
                        {
                            var Id = GetCellString(sheet, i, 0);
                            if (string.IsNullOrEmpty(Id) || Id.StartsWith("#")) continue;
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
                            Ids.Add(Id);
                        }
                    }
                }
            }
        }
    }
    static void ExportData(List<string> ExcelList)
    {
        for (int i = 0; i < dic.Keys.Count; i++)
            File.Delete(PathHelper.AppHotfixResPath + "/Data/" + dic.Keys.ToList()[i] + ".txt");
        foreach (var path in ExcelList.ToArray())
        {
            if (path.Contains("$")) continue;
            Export(path);
        }
    }

    static void Export(string fileName)
    {
        ExcelDataReader.IExcelDataReader reader;
        using (FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            reader = ExcelDataReader.ExcelReaderFactory.CreateReader(file);
            try
            {
                foreach (System.Data.DataTable sheet in reader.AsDataSet().Tables)
                {
                    //Debug.Log(sheet.TableName);
                    if (sheet.TableName.StartsWith("#")) continue;
                    StringBuilder sb = new StringBuilder();
                    int cellCount = sheet.Columns.Count;
                    for (int i = 3; i < sheet.Rows.Count; i++)
                    {
                        string Id = GetCellString(sheet, i, 0);
                        if (string.IsNullOrEmpty(Id) || Id.StartsWith("#"))
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

                    using (FileStream txt = new FileStream(PathHelper.AppHotfixResPath + "/Data/" + sheet.TableName + ".txt", FileMode.Append))
                    using (StreamWriter sw = new StreamWriter(txt))
                    {
                        sw.Write(sb.ToString());
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
                        sbCache.Append($"{{\"x\":{sp1[0]},\"y\":{sp1[1]}}}");
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
            Debug.Log(e.Message);
            throw e;
        }
    }
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
    {   try
        {
            Dictionary<(string, string, string), int> unitList = new Dictionary<(string name, string type, string icon), int>();
            using ExcelPackage package = new ExcelPackage(new FileInfo(path));
            ExcelWorksheet worksheet = package.Workbook.Worksheets["UnitData"];
            for (int i = 5; i <= worksheet.Dimension.Rows; i++)
            {
                if (worksheet.Cells[i, 1].Value?.ToString().StartsWith("#") == true || worksheet.Cells[i, 1].Value == null)
                {
                    continue;
                }
                else
                {
                    (string, string, string) unitInfo = (
                        worksheet.Cells[i, 33].Value.ToString() + "/" + worksheet.Cells[i, 1].Value.ToString(),
                        worksheet.Cells[i, 2].Value.ToString(),
                        worksheet.Cells[i, 56].Value?.ToString()?? ""
                        );
                    unitList[unitInfo] = i;
                }
            }
            return unitList;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
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
}
