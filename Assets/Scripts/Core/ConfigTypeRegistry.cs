using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 配置表 Type 合法性注册表。
///
/// 方案 A：不再维护整份文字白名单，而是反射枚举以下命名空间下
/// 实际可被 Activator.CreateInstance 创建的子类：
///   UnitData   -> Units.&lt;Type&gt;   : Unit
///   SkillData  -> Skills.&lt;Type&gt;  : Skill （另有特殊值 Json -> JsonSkill）
///   BuffData   -> Buffs.&lt;Type&gt;   : Buff
///   ModifyData -> Modifys.&lt;Type&gt; : Modify
///   BulletData -> Bullets.&lt;Type&gt; : Bullet
///
/// 说明：
/// 1. 结果按表缓存，导表时只扫描一次程序集；
/// 2. 过滤 abstract / interface / 泛型定义 / 嵌套类 / 非 public / 无 public 无参构造的类型；
/// 3. 反射枚举失败或结果为空时返回 false，由调用方报错，避免“空集合导致全部合法/全部非法”的歧义；
/// 4. 如果未来拆分 asmdef，把包含配置子类的程序集加入 CandidateAssemblies 即可。
/// </summary>
public static class ConfigTypeRegistry
{
    private sealed class TableTypeRule
    {
        public readonly Type BaseType;
        public readonly string Namespace;

        public TableTypeRule(Type baseType, string expectedNamespace)
        {
            BaseType = baseType;
            Namespace = expectedNamespace;
        }
    }

    private static readonly Dictionary<string, TableTypeRule> Rules =
        new Dictionary<string, TableTypeRule>(StringComparer.Ordinal)
        {
            { "UnitData", new TableTypeRule(typeof(Unit), "Units") },
            { "SkillData", new TableTypeRule(typeof(Skill), "Skills") },
            { "BuffData", new TableTypeRule(typeof(Buff), "Buffs") },
            { "ModifyData", new TableTypeRule(typeof(Modify), "Modifys") },
            { "BulletData", new TableTypeRule(typeof(Bullet), "Bullets") },
        };

    /// <summary>
    /// 少数无法从类名直接推导的兼容别名。这里只放别名，不维护完整白名单。
    /// 例如 SkillData.Type = "Json" 在运行期会创建 JsonSkill。
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, Type>> ManualAliases =
        new Dictionary<string, Dictionary<string, Type>>(StringComparer.Ordinal)
        {
            {
                "SkillData",
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    { "Json", typeof(JsonSkill) },
                }
            },
        };

    /// <summary>
    /// 设计上不允许作为配置 Type 的类（例如测试类、仅供继承的辅助类）。
    /// 默认不排除任何类；确需排除时在这里补类名即可。
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ManualExclusions =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            { "UnitData", new HashSet<string>(StringComparer.Ordinal) },
            { "SkillData", new HashSet<string>(StringComparer.Ordinal) },
            { "BuffData", new HashSet<string>(StringComparer.Ordinal) },
            { "ModifyData", new HashSet<string>(StringComparer.Ordinal) },
            { "BulletData", new HashSet<string>(StringComparer.Ordinal) },
        };

    /// <summary>
    /// 包含配置子类的程序集。当前工程无 asmdef，基本都集中在 Assembly-CSharp；
    /// 这里仍按基类所在程序集去重收集，方便未来拆分。
    /// </summary>
    private static readonly Assembly[] CandidateAssemblies = new[]
    {
        typeof(Unit).Assembly,
        typeof(Skill).Assembly,
        typeof(Buff).Assembly,
        typeof(Modify).Assembly,
        typeof(Bullet).Assembly,
        typeof(ConfigTypeRegistry).Assembly,
    }.Distinct().ToArray();

    private static readonly object CacheLock = new object();
    private static readonly Dictionary<string, HashSet<string>> Cache =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private static readonly HashSet<string> EmptyLoggedTables =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>该 Sheet 是否属于需要做 Type 合法性检查的表。</summary>
    public static bool IsTypeTable(string tableName)
    {
        return !string.IsNullOrEmpty(tableName) && Rules.ContainsKey(tableName);
    }

    /// <summary>取得某张表运行期反射使用的命名空间，例如 UnitData -> Units。</summary>
    public static string GetTypeNamespace(string tableName)
    {
        if (tableName != null && Rules.TryGetValue(tableName, out var rule))
            return rule.Namespace;
        return null;
    }

    /// <summary>
    /// 取得某张表全部合法 Type。返回 false 表示表名未知或反射枚举结果为空。
    /// </summary>
    public static bool TryGetValidTypes(string tableName, out HashSet<string> validTypes)
    {
        validTypes = null;
        if (string.IsNullOrEmpty(tableName) || !Rules.TryGetValue(tableName, out var rule))
            return false;

        lock (CacheLock)
        {
            if (!Cache.TryGetValue(tableName, out validTypes))
            {
                validTypes = BuildValidTypes(tableName, rule);
                Cache[tableName] = validTypes;
            }
        }

        if (validTypes.Count > 0)
            return true;

        lock (EmptyLoggedTables)
        {
            if (EmptyLoggedTables.Add(tableName))
            {
                Debug.LogError(
                    $"[ConfigTypeRegistry] {tableName} 未枚举到任何合法 Type。" +
                    "请检查程序集、命名空间是否正确；如果使用 IL2CPP，请确认 link.xml 已保留相关类型。");
            }
        }
        return false;
    }

    /// <summary>
    /// 校验一个 Type 值。错误信息已经包含可直接写入导表报告的原因。
    /// </summary>
    public static bool IsValidType(string tableName, string rawType, out string error)
    {
        error = null;

        if (!IsTypeTable(tableName))
        {
            error = $"表 {tableName} 不属于 Type 合法性检查范围";
            return false;
        }

        if (string.IsNullOrEmpty(rawType))
        {
            error = "Type 为空，运行时无法反射创建对应子类";
            return false;
        }

        if (rawType != rawType.Trim())
        {
            error = $"Type \"{rawType}\" 首尾包含空白，运行时将按原值查找而失败";
            return false;
        }

        if (!TryGetValidTypes(tableName, out var validTypes))
        {
            error = $"表 {tableName} 的 Type 注册表为空，无法校验";
            return false;
        }

        if (!validTypes.Contains(rawType))
        {
            error = $"Type \"{rawType}\" 在 {GetTypeNamespace(tableName)} 命名空间下不存在";
            return false;
        }

        return true;
    }

    /// <summary>取得排序后的合法 Type 名称，供编辑器工具/诊断输出使用。</summary>
    public static string[] GetValidTypeNames(string tableName)
    {
        if (!TryGetValidTypes(tableName, out var validTypes))
            return Array.Empty<string>();

        return validTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    /// <summary>清空缓存；编辑器热重载/测试时可用。</summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
        }
        lock (EmptyLoggedTables)
        {
            EmptyLoggedTables.Clear();
        }
    }

    private static HashSet<string> BuildValidTypes(string tableName, TableTypeRule rule)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in CandidateAssemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type == null) continue;
                if (!type.IsClass || !type.IsPublic) continue;
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
                if (type.IsNested) continue;
                if (!string.Equals(type.Namespace, rule.Namespace, StringComparison.Ordinal)) continue;
                if (!rule.BaseType.IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                result.Add(type.Name);
            }
        }

        if (ManualAliases.TryGetValue(tableName, out var aliases))
        {
            foreach (var pair in aliases)
                result.Add(pair.Key);
        }

        if (ManualExclusions.TryGetValue(tableName, out var exclusions))
        {
            result.ExceptWith(exclusions);
        }

        return result;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        if (assembly == null) return Array.Empty<Type>();

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            string loaderErrors = string.Join(
                "\n",
                (e.LoaderExceptions ?? Array.Empty<Exception>())
                    .Where(x => x != null)
                    .Select(x => x.Message)
                    .ToArray());

            Debug.LogError(
                $"[ConfigTypeRegistry] 程序集 {assembly.GetName().Name} 部分类型加载失败：\n{loaderErrors}");

            return e.Types.Where(x => x != null);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[ConfigTypeRegistry] 程序集 {assembly.GetName().Name} 类型枚举失败：{e}");
            return Array.Empty<Type>();
        }
    }
}
