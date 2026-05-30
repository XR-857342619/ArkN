using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static DynamicSorter;
using static UnityEngine.GraphicsBuffer;

public static class TargetSelectorFactory
{
    private static readonly Dictionary<string, Type> _filterStrategyMap = new();
    private static readonly Dictionary<string, Type> _sortStrategyMap = new();

    static TargetSelectorFactory()
    {
        // 自动扫描程序集中所有实现 IFilterStrategy 的类
        var filterTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IFilterStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in filterTypes)
        {
            var instance = (IFilterStrategy)Activator.CreateInstance(type);
            _filterStrategyMap[instance.Name] = type;
        }

        var sorterTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ISortStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in sorterTypes)
        {
            var instance = (ISortStrategy)Activator.CreateInstance(type);
            _sortStrategyMap[instance.Name] = type;
        }
    }

    // 根据配置创建策略实例
    public static ISortStrategy CreateSorter(string strategyName, SkillContext skillContext, object[] parameters = null)
    {
        if (!_sortStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到排序策略: {strategyName}");
            return null;
        }

        // 如果有参数（如自定义BuffID），使用带参数的构造函数
        if (parameters != null && parameters.Length > 0)
        {
            return (ISortStrategy)Activator.CreateInstance(type, skillContext, parameters);
        }

        return (ISortStrategy)Activator.CreateInstance(type, skillContext);
    }

    // 根据配置创建策略实例
    public static IFilterStrategy CreateFilter(string strategyName, SkillContext skillContext, object[] parameters = null)
    {
        if (!_filterStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到筛选策略: {strategyName}");
            return null;
        }

        // 如果有参数（如自定义BuffID），使用带参数的构造函数
        if (parameters != null && parameters.Length > 0)
        {
            return (IFilterStrategy)Activator.CreateInstance(type, skillContext, parameters);
        }

        return (IFilterStrategy)Activator.CreateInstance(type, skillContext);
    }
}

public class DynamicTargetSelector
{
    public class FilterConfigNode
    {
        public string Type;
        public object[] Parameters;   // 可选参数，用于自定义策略
        public SkillContext SkillContext;
    }
    public class SortConfigNode
    {
        public string Type;           // 策略名称，如 "Distance"
        public SortDirection Direction; // 方向
        public object[] Parameters;   // 可选参数，用于自定义策略
        public SkillContext SkillContext;
    }

    public List<Unit> SelectTargets(List<Unit> units, List<FilterConfigNode> filterConfigList, List<SortConfigNode> sortConfigList)
    {
        var result = FilterTargets(units, filterConfigList);
        result = SortTargets(result, sortConfigList);
        
        return result;
    }

    public List<Unit> FilterTargets(List<Unit> targets, List<FilterConfigNode> configList)
    {
        if (targets == null || targets.Count == 0) return targets;

        // 应用所有筛选条件
        IEnumerable<Unit> filteredUnits = targets;

        if (targets.Count == 0) return targets;

        IEnumerable<Unit> orderedQuery = null;

        for (int i = 0; i < configList.Count; i++)
        {
            var node = configList[i];
            var filterStrategy = TargetSelectorFactory.CreateFilter(
                    node.Type,
                    node.SkillContext,
                    node.Parameters
                    );
            if (filterStrategy == null) continue;

            var keySelector = filterStrategy.GetPredicate();

            orderedQuery = targets.Where(keySelector);
        }

        var result = filteredUnits.ToList();

        return result;
    }

    public List<Unit> SortTargets(List<Unit> targets, List<SortConfigNode> configList)
    {
        if (targets.Count == 0) return targets;

        IOrderedEnumerable<Unit> orderedQuery = null;

        for (int i = 0; i < configList.Count; i++)
        {
            var node = configList[i];
            var strategy = TargetSelectorFactory.CreateSorter(node.Type, node.SkillContext, node.Parameters);
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
}
