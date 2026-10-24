using System;
using System.Collections.Generic;
using System.Text;

namespace DiyEditor
{
    /// <summary>
    /// DIY 默认值字符串转换器。
    /// 转换规则与 Assets/Editor/ExcelEditor/ExcelExportEditor.cs 中的 Convert 保持一致，
    /// 区别是引用表 Id -> 下标的映射由调用方传入，不再依赖 Excel 导表过程。
    /// </summary>
    public static class DiyValueConverter
    {
        /// <summary>
        /// 将 Excel 风格的默认值字符串转换为 JSON 字面量。
        /// </summary>
        /// <param name="type">字段类型，例如 int、string、bool、SkillReadyEnum、UnitData[]、UnityEngine.Vector2Int</param>
        /// <param name="value">用户输入的原始字符串</param>
        /// <param name="referenceIds">表名 -> Id 列表，用于把引用字段转换成数组下标</param>
        /// <param name="json">转换后的 JSON 字面量</param>
        /// <param name="error">转换失败原因</param>
        public static bool TryConvert(
            string type,
            string value,
            Dictionary<string, List<string>> referenceIds,
            out string json,
            out string error)
        {
            json = null;
            error = null;

            if (string.IsNullOrEmpty(type))
            {
                json = value;
                return true;
            }

            try
            {
                var sbCache = new StringBuilder();

                if (type.EndsWith("Enum[]"))
                {
                    Type enumType = typeof(Init).Assembly.GetType(type.Substring(0, type.Length - 2));
                    sbCache.Clear();
                    string[] sp = value.Split(',');
                    foreach (string s in sp)
                        sbCache.Append((int)Enum.Parse(enumType, s) + ",");
                    sbCache.Remove(sbCache.Length - 1, 1);
                    json = "[" + sbCache + "]";
                    return true;
                }

                if (type.EndsWith("Enum"))
                {
                    try
                    {
                        Type enumType = typeof(Init).Assembly.GetType(type);
                        json = ((int)Enum.Parse(enumType, value)).ToString();
                        return true;
                    }
                    catch
                    {
                        // 与 ExcelExportEditor.Convert 保持一致：枚举解析失败时原样输出。
                        json = value;
                        return true;
                    }
                }

                if (referenceIds != null && referenceIds.TryGetValue(type, out var ids))
                {
                    int index = ids.IndexOf(value);
                    if (index == -1)
                        throw new Exception(value);
                    json = index.ToString();
                    return true;
                }

                string elementType = type.TrimEnd("[]".ToCharArray());
                if (referenceIds != null && referenceIds.TryGetValue(elementType, out var elementIds))
                {
                    sbCache.Clear();
                    sbCache.Append("[");
                    string[] sp = value.Split(',');
                    foreach (var s in sp)
                    {
                        int index = elementIds.IndexOf(s);
                        if (index == -1)
                            throw new Exception(value);
                        sbCache.Append(index + ",");
                    }

                    if (sbCache.Length > 1)
                        sbCache.Remove(sbCache.Length - 1, 1);
                    sbCache.Append("]");
                    json = sbCache.ToString();
                    return true;
                }

                switch (type)
                {
                    case "int[]":
                    case "int32[]":
                    case "long[]":
                    case "object[]":
                        json = "[" + value + "]";
                        return true;

                    case "string[]":
                        sbCache.Clear();
                        string[] stringParts = value.Split(',');
                        if (stringParts.Length == 1)
                            stringParts = value.Split('\n');
                        foreach (string s in stringParts)
                            sbCache.Append("\"" + s + "\",");
                        sbCache.Remove(sbCache.Length - 1, 1);
                        json = "[" + sbCache + "]";
                        return true;

                    case "int":
                    case "int32":
                    case "int64":
                    case "long":
                    case "float":
                    case "double":
                        json = value;
                        return true;

                    case "string":
                        json = "\"" + value + "\"";
                        return true;

                    case "bool":
                        json = value;
                        return true;

                    case "Data":
                        json = "{" + value + "}";
                        return true;

                    case "UnityEngine.Vector2":
                    case "UnityEngine.Vector2Int":
                    {
                        string[] sp = value.Split(',');
                        json = "{\"x\":" + sp[0] + ",\"y\":" + sp[1] + "}";
                        return true;
                    }

                    case "UnityEngine.Vector3":
                    case "UnityEngine.Vector3Int":
                    {
                        string[] sp = value.Split(',');
                        json = "{\"x\":" + sp[0] + ",\"y\":" + sp[1] + ",\"z\":" + sp[2] + "}";
                        return true;
                    }

                    case "UnityEngine.Vector2[]":
                    case "UnityEngine.Vector2Int[]":
                    {
                        sbCache.Clear();
                        string[] sp = value.Split('#');
                        for (int i = 0; i < sp.Length; i++)
                        {
                            string[] sp1 = sp[i].Split(',');
                            sbCache.Append("{\"x\":" + sp1[0] + ",\"y\":" + sp1[1] + "}");
                            if (i != sp.Length - 1)
                                sbCache.Append(",");
                        }
                        json = "[" + sbCache + "]";
                        return true;
                    }

                    case "UnityEngine.Rect":
                    {
                        string[] sp = value.Split(',');
                        json = "{\"x\":" + sp[0] + ",\"y\":" + sp[1] +
                               ",\"width\":" + sp[2] + ",\"height\":" + sp[3] + "}";
                        return true;
                    }

                    default:
                        if (type.EndsWith("[]"))
                        {
                            json = "[" + value + "]";
                            return true;
                        }
                        json = value;
                        return true;
                }
            }
            catch (Exception e)
            {
                error = $"type:{type}, value:{value}, error:{e.Message}";
                return false;
            }
        }
    }
}
