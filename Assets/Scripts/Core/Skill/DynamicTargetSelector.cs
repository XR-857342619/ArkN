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
    private static readonly Dictionary<string, Type> _selectorStrategyMap = new();

    static TargetSelectorFactory()
    {
        // 自动扫描程序集中所有实现 IFilterStrategy 的类
        var filterTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IFilterStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in filterTypes)
        {
            try
            {
                var instance = (IFilterStrategy)CreateDefaultInstance(type);
                _filterStrategyMap[instance.Name] = type;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"TargetSelectorFactory 注册 IFilterStrategy {type.Name} 失败: {e.Message}");
            }
        }

        var sorterTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ISortStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in sorterTypes)
        {
            try
            {
                var instance = (ISortStrategy)CreateDefaultInstance(type);
                _sortStrategyMap[instance.Name] = type;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"TargetSelectorFactory 注册 ISortStrategy {type.Name} 失败: {e.Message}");
            }
        }

        var selectorTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ISelectorStrategy).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in selectorTypes)
        {
            try
            {
                var instance = (ISelectorStrategy)CreateDefaultInstance(type);
                _selectorStrategyMap[instance.Name] = type;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"TargetSelectorFactory 注册 ISelectorStrategy {type.Name} 失败: {e.Message}");
            }
        }
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

    public static bool ContainsFilter(string strategyName)
    {
        return !string.IsNullOrEmpty(strategyName) && _filterStrategyMap.ContainsKey(strategyName);
    }

    public static bool ContainsSorter(string strategyName)
    {
        return !string.IsNullOrEmpty(strategyName) && _sortStrategyMap.ContainsKey(strategyName);
    }

    public static bool ContainsSelector(string strategyName)
    {
        return !string.IsNullOrEmpty(strategyName) && _selectorStrategyMap.ContainsKey(strategyName);
    }

    /// <summary>
    /// 根据 JSON Data 字典创建筛选器。
    /// </summary>
    public static IFilterStrategy CreateFilterFromData(string strategyName, SkillContext skillContext, Dictionary<string, object> data)
    {
        if (!_filterStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到筛选策略: {strategyName}");
            return null;
        }

        var args = JsonConfigHelper.BuildParameters(data, type, skillContext);
        if (args == null) return null;

        return (IFilterStrategy)Activator.CreateInstance(type, args);
    }

    /// <summary>
    /// 根据 JSON Data 字典创建排序器。
    /// </summary>
    public static ISortStrategy CreateSorterFromData(string strategyName, SkillContext skillContext, Dictionary<string, object> data)
    {
        if (!_sortStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到排序策略: {strategyName}");
            return null;
        }

        var args = JsonConfigHelper.BuildParameters(data, type, skillContext);
        if (args == null) return null;

        return (ISortStrategy)Activator.CreateInstance(type, args);
    }

    /// <summary>
    /// 根据 JSON Data 字典创建主动选择器。
    /// </summary>
    public static ISelectorStrategy CreateSelectorFromData(string strategyName, SkillContext skillContext, Dictionary<string, object> data)
    {
        if (!_selectorStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到选择器策略: {strategyName}");
            return null;
        }

        var args = JsonConfigHelper.BuildParameters(data, type, skillContext);
        if (args == null) return null;

        return (ISelectorStrategy)Activator.CreateInstance(type, args);
    }

    // 保留旧 API，兼容已有调用。
    public static ISortStrategy CreateSorter(string strategyName, SkillContext skillContext, object[] parameters = null)
    {
        if (!_sortStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到排序策略: {strategyName}");
            return null;
        }

        if (parameters != null && parameters.Length > 0)
        {
            return (ISortStrategy)Activator.CreateInstance(type, skillContext, parameters);
        }

        return (ISortStrategy)Activator.CreateInstance(type, skillContext);
    }

    public static IFilterStrategy CreateFilter(string strategyName, SkillContext skillContext, object[] parameters = null)
    {
        if (!_filterStrategyMap.TryGetValue(strategyName, out var type))
        {
            Debug.Log($"未找到筛选策略: {strategyName}");
            return null;
        }

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

    /// <summary>
    /// JsonSkill 使用的入口：根据 SelectorNode/SorterNode 完成“产生候选 -> 筛选 -> 排序”。
    /// </summary>
    public List<Unit> SelectTargets(SkillContext context, List<SelectorNode> selectors, List<SorterNode> sorters)
    {
        if (context == null || context.Caster == null || context.Caster.Battle == null)
        {
            return new List<Unit>();
        }

        List<Unit> result = null;

        if (selectors == null || selectors.Count == 0)
        {
            return new List<Unit>();
        }

        foreach (var node in selectors)
        {
            if (node == null) continue;
            result = ApplySelectorNode(context, result, node);
        }

        if (result == null)
        {
            return new List<Unit>();
        }

        result = SortTargets(result, sorters, context);

        return result ?? new List<Unit>();
    }

    private List<Unit> ApplySelectorNode(SkillContext context, List<Unit> current, SelectorNode node)
    {
        // 优先作为主动选择器：可以产生全新的候选集合
        var selector = TargetSelectorFactory.CreateSelectorFromData(node.Type, context, node.Data);
        if (selector != null)
        {
            return selector.GetTargets(context, node.Data) ?? new List<Unit>();
        }

        // 否则作为筛选器：在已有候选集合上过滤
        var filter = TargetSelectorFactory.CreateFilterFromData(node.Type, context, node.Data);
        if (filter != null)
        {
            if (current == null)
            {
                current = context.Caster.Battle.AllUnits?.ToList() ?? new List<Unit>();
            }

            var predicate = filter.GetPredicate();
            return current.Where(predicate).ToList();
        }

        Debug.LogWarning($"DynamicTargetSelector 未找到选择器/筛选器: {node.Type}");
        return current ?? new List<Unit>();
    }

    public List<Unit> FilterTargets(List<Unit> targets, List<FilterConfigNode> configList)
    {
        if (targets == null || targets.Count == 0) return targets;
        if (configList == null || configList.Count == 0) return targets;

        IEnumerable<Unit> filteredUnits = targets;

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

            filteredUnits = filteredUnits.Where(keySelector);
        }

        var result = filteredUnits.ToList();

        return result;
    }

    public List<Unit> SortTargets(List<Unit> targets, List<SortConfigNode> configList)
    {
        if (targets == null || targets.Count == 0) return targets;

        IOrderedEnumerable<Unit> orderedQuery = null;

        for (int i = 0; i < configList.Count; i++)
        {
            var node = configList[i];
            var strategy = TargetSelectorFactory.CreateSorter(node.Type, node.SkillContext, node.Parameters);
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

    /// <summary>
    /// 使用 JsonSkill 的 SorterNode 列表排序。
    /// </summary>
    public List<Unit> SortTargets(List<Unit> targets, List<SorterNode> configList, SkillContext context)
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
