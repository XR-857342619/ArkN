using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

/// <summary>
/// 将 JSON 节点中的 Dictionary&lt;string, object&gt; 参数反射绑定到策略/效果器的构造函数参数。
/// 例如 SelectorNode.Data 中的 "Team" 会自动匹配构造函数参数 team。
/// </summary>
public static class JsonConfigHelper
{
    public static object[] BuildParameters(Dictionary<string, object> data, Type targetType, SkillContext context = null)
    {
        if (targetType == null) return null;

        var ctor = targetType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor == null) return null;

        var parameters = ctor.GetParameters();
        var args = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if (p.ParameterType == typeof(SkillContext))
            {
                args[i] = context;
                continue;
            }

            object raw = null;
            var found = data != null && data.TryGetValue(p.Name, out raw);
            if (!found && data != null)
            {
                // 兼容大小写差异：JSON 常用 PascalCase，C# 参数常用 camelCase
                var key = data.Keys.FirstOrDefault(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    found = true;
                    raw = data[key];
                }
            }

            if (found)
            {
                args[i] = ConvertValue(raw, p.ParameterType);
            }
            else
            {
                args[i] = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
            }
        }

        return args;
    }

    public static SkillReadyEnum ParseReadyType(object value)
    {
        if (value == null) return SkillReadyEnum.None;
        if (value is SkillReadyEnum ready) return ready;

        var str = Convert.ToString(value);
        if (string.IsNullOrEmpty(str)) return SkillReadyEnum.None;

        if (string.Equals(str, "SP", StringComparison.OrdinalIgnoreCase))
        {
            return SkillReadyEnum.特技激活;
        }

        if (Enum.TryParse(str, true, out SkillReadyEnum result))
        {
            return result;
        }

        return SkillReadyEnum.None;
    }

    public static object ConvertValue(object raw, Type targetType)
    {
        if (raw == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(object))
        {
            return raw is JValue jv ? jv.Value : raw is JToken jt ? jt.ToObject<object>() : raw;
        }

        if (targetType.IsInstanceOfType(raw))
        {
            return raw;
        }

        if (targetType.IsEnum)
        {
            if (raw is string enumStr)
            {
                // 兼容设计文档中的常用写法
                if (targetType == typeof(SkillReadyEnum) && string.Equals(enumStr, "SP", StringComparison.OrdinalIgnoreCase))
                {
                    return SkillReadyEnum.特技激活;
                }

                try
                {
                    return Enum.Parse(targetType, enumStr, true);
                }
                catch
                {
                    return Enum.ToObject(targetType, 0);
                }
            }
            return Enum.ToObject(targetType, Convert.ToInt32(raw));
        }

        if (targetType == typeof(int))
        {
            return Convert.ToInt32(raw);
        }

        if (targetType == typeof(float))
        {
            return Convert.ToSingle(raw);
        }

        if (targetType == typeof(double))
        {
            return Convert.ToDouble(raw);
        }

        if (targetType == typeof(bool))
        {
            return Convert.ToBoolean(raw);
        }

        if (targetType == typeof(string))
        {
            return Convert.ToString(raw);
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType();
            var rawArray = raw as JArray ?? (raw is IEnumerable<object> list ? new JArray(list.Cast<object>()) : null);
            if (rawArray == null)
            {
                // 尝试把单个值包装成数组
                rawArray = new JArray(raw);
            }

            var array = Array.CreateInstance(elementType, rawArray.Count);
            for (int i = 0; i < rawArray.Count; i++)
            {
                array.SetValue(ConvertValue(rawArray[i], elementType), i);
            }
            return array;
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType) && targetType.IsGenericType)
        {
            var argType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(argType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType);

            var rawArray = raw as JArray ?? (raw is IEnumerable<object> enumerable ? new JArray(enumerable.Cast<object>()) : null);
            if (rawArray != null)
            {
                foreach (var item in rawArray)
                {
                    list.Add(ConvertValue(item, argType));
                }
            }

            return list;
        }

        // 最后尝试让 Newtonsoft 转换
        try
        {
            return raw is JToken token ? token.ToObject(targetType) : raw;
        }
        catch
        {
            return raw;
        }
    }

    public static object GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}