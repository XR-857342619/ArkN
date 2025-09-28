using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;
using System.Linq;

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
        //Log.Debug(expression);
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
                //Log.Debug(target.UnitData.Name);
                if (target == null) continue;
                if ((bool)predicate.DynamicInvoke(_unit, target))
                {
                    //Log.Debug("true");
                    results.Add(target);
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"表达式编译/执行错误: {ex.Message}\n表达式: {expression}");
            TipManager.Instance.ShowTip($"表达式编译/执行错误: {ex.Message}\n表达式: {expression}");
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

        Log.Debug(parameters);

        // 使用DynamicExpressionParser解析并编译表达式
        var lambda = DynamicExpressionParser.ParseLambda(
            parameters,
            typeof(bool),
            processedExpr,
            new ParsingConfig()
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
                        //.Replace("=", "==")
                        .Replace("  ", " ")
                        .Trim();
    }

    /// <summary>
    /// 计算包含Unit和Target参数的数学表达式
    /// </summary>
    /// <param name="expression">数学表达式（支持Unit.XXX和Target.XXX作为参数）</param>
    /// <returns>表达式计算结果</returns>
    public object EvaluateExpressionWithParameters(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            // 检查是否包含逻辑运算符（纯数学计算不支持）
            var logicOperators = new[] { "&&", "||", "!", "and", "or", "not", "==", "!=", "<", ">", "<=", ">=" };
            if (logicOperators.Any(op => expression.Contains(op)))
                throw new ArgumentException("表达式不能包含逻辑运算符", nameof(expression));

            // 创建参数表达式（与Filter方法保持一致的参数名）
            var unitParam = Expression.Parameter(typeof(Unit), "Unit");
            var targetParam = Expression.Parameter(typeof(Unit), "Target");
            var parameters = new[] { unitParam, targetParam };

            // 预处理表达式（保持与现有逻辑一致的替换规则）
            var processedExpr = PreprocessMathExpression(expression);

            // 解析数学表达式（返回类型自动推断）
            var lambda = DynamicExpressionParser.ParseLambda(
                parameters,
                null,  // 自动推断返回类型
                processedExpr,
                new ParsingConfig()
            );

            // 执行表达式并返回结果（使用当前实例的Unit和Target参数）
            return lambda.Compile().DynamicInvoke(_unit, _targets.FirstOrDefault());
        }
        catch (Exception ex)
        {
            var innerException = ex.InnerException ?? ex;
            UnityEngine.Debug.LogError($"表达式计算失败: {innerException.Message}\n表达式: {expression}");
            throw;
        }
    }

    /// <summary>
    /// 数学表达式专用预处理（避免替换数学符号）
    /// </summary>
    private string PreprocessMathExpression(string expression)
    {
        // 只处理逻辑关键词替换，保留数学运算符（不替换=号，避免影响赋值语义）
        return expression.Replace("and", "&&")
                        .Replace("or", "||")
                        .Replace("not", "!")
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