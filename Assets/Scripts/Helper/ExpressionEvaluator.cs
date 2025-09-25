using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;

public class ExpressionEvaluator
{
    // 缓存编译后的表达式委托 (key: 表达式字符串, value: 委托)
    private static readonly Dictionary<string, Delegate> _expressionCache = new Dictionary<string, Delegate>();
    // 缓存类型成员信息以减少反射开销
    private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> _memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

    private readonly Unit _unit;
    private readonly List<Unit> _targets;

    public ExpressionEvaluator(Unit unit, List<Unit> targets)
    {
        _unit = unit;
        _targets = targets ?? new List<Unit>();
    }

    public List<Unit> Filter(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return new List<Unit>();

        try
        {
            // 无论目标列表是否为空，先获取编译后的委托（触发缓存逻辑）
            var predicate = GetCompiledPredicate(expression);

            // 若目标列表为空，直接返回空（不执行过滤）
            if (_targets == null || _targets.Count == 0)
                return new List<Unit>();

            // 原有过滤逻辑...
            var results = new List<Unit>();
            foreach (var target in _targets)
            {
                if (target == null) continue;
                if ((bool)predicate.DynamicInvoke(_unit, target))
                {
                    results.Add(target);
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"表达式编译/执行错误: {ex.Message}\n表达式: {expression}");
            return new List<Unit>();
        }
    }

    // 获取编译后的委托（带缓存）
    private Delegate GetCompiledPredicate(string expression)
    {
        if (_expressionCache.TryGetValue(expression, out var cached))
            return cached;

        // 构建表达式参数 (Unit和Target作为输入参数)
        var unitParam = Expression.Parameter(typeof(Unit), "Unit");
        var targetParam = Expression.Parameter(typeof(Unit), "Target");
        var parameters = new[] { unitParam, targetParam };

        // 预处理表达式（替换关键字、修正语法）
        var processedExpr = PreprocessExpression(expression);

        // 使用DynamicExpressionParser解析并编译表达式
        var lambda = DynamicExpressionParser.ParseLambda(
            parameters,
            typeof(bool),
            processedExpr,
            new ParsingConfig() // 旧版默认配置，无 AllowNewKeyword 等属性
        );

        // 缓存编译后的委托
        _expressionCache[expression] = lambda.Compile();
        return _expressionCache[expression];
    }

    // 表达式预处理
    private string PreprocessExpression(string expression)
    {
        // 替换逻辑运算符别名并清理空格
        return expression.Replace("and", "&&")
                        .Replace("or", "||")
                        .Replace("not", "!")
                        .Replace("=", "==") // 防止单等号错误
                        .Replace("  ", " ")
                        .Trim();
    }

    // 清理缓存（用于热重载等场景）
    public static void ClearCache()
    {
        _expressionCache.Clear();
        _memberCache.Clear();
    }
}