using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

public static class SortStrategyFactory
{
    // 缓存所有可用的策略类型
    private static readonly Dictionary<string, Type> _strategyMap = new Dictionary<string, Type>();

    static SortStrategyFactory()
    {
        // 自动扫描程序集中所有实现 ISortStrategy 的类
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ISortStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in types)
        {
            var instance = (ISortStrategy)CreateDefaultInstance(type);
            _strategyMap[instance.Name] = type;
        }
    }

    // 根据配置创建策略实例
    public static ISortStrategy Create(string strategyName, object[] parameters = null)
    {
        if (!_strategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到排序策略: {strategyName}");
            return null;
        }

        // 如果有参数（如自定义BuffID），使用带参数的构造函数
        if (parameters != null && parameters.Length > 0)
        {
            return (ISortStrategy)Activator.CreateInstance(type, parameters);
        }

        return (ISortStrategy)Activator.CreateInstance(type);
    }

    private static object CreateDefaultInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (MissingMethodException)
        {
            var ctor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor == null) throw;

            var parameters = ctor.GetParameters();
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = JsonConfigHelper.GetDefault(parameters[i].ParameterType);
            }

            return Activator.CreateInstance(type, args);
        }
    }
}

public class DynamicSorter
{
    public class SortConfigNode
    {
        public string Type;           // 策略名称，如 "Distance"
        public SortDirection Direction; // 方向
        public object[] Parameters;   // 可选参数，用于自定义策略
    }

    // 3. 动态排序执行器
    public List<Unit> Sort(List<Unit> targets, List<SortConfigNode> configList)
    {
        if (targets == null || targets.Count == 0) return targets;

        IOrderedEnumerable<Unit> orderedQuery = null;

        for (int i = 0; i < configList.Count; i++)
        {
            var node = configList[i];
            var strategy = SortStrategyFactory.Create(node.Type, node.Parameters);
            if (strategy == null) continue;

            var keySelector = strategy.GetKeySelector();

            if (i == 0)
            {
                // 第一级
                orderedQuery = node.Direction == SortDirection.Ascending
                    ? targets.OrderBy(keySelector)
                    : targets.OrderByDescending(keySelector);
            }
            else
            {
                // 后续级别（稳定排序的关键）
                orderedQuery = node.Direction == SortDirection.Ascending
                    ? orderedQuery.ThenBy(keySelector)
                    : orderedQuery.ThenByDescending(keySelector);
            }
        }

        return orderedQuery?.ToList() ?? targets;
    }

    /// <summary>
    /// JsonSkill 使用的 JSON SorterNode 排序入口。
    /// </summary>
    public List<Unit> Sort(List<Unit> targets, List<SorterNode> configList, SkillContext context)
    {
        if (targets == null || targets.Count == 0) return targets;
        if (configList == null || configList.Count == 0) return targets;

        IOrderedEnumerable<Unit> orderedQuery = null;

        for (int i = 0; i < configList.Count; i++)
        {
            var node = configList[i];
            if (node == null) continue;

            var strategy = TargetSelectorFactory.CreateSorterFromData(node.Type, context, node.Data);
            if (strategy == null) continue;

            var keySelector = strategy.GetKeySelector();

            if (i == 0)
            {
                orderedQuery = node.Direction == SortDirection.Ascending
                    ? targets.OrderBy(keySelector)
                    : targets.OrderByDescending(keySelector);
            }
            else
            {
                orderedQuery = node.Direction == SortDirection.Ascending
                    ? orderedQuery.ThenBy(keySelector)
                    : orderedQuery.ThenByDescending(keySelector);
            }
        }

        return orderedQuery?.ToList() ?? targets;
    }
}
