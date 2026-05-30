using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;
using System.Reflection;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using UnityEngine;

public class UnifiedExpressionEngine
{
    // 共享缓存（统一管理，避免重复）
    private static readonly Dictionary<string, Delegate> _compiledExpressions = new Dictionary<string, Delegate>();
    private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> _memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

    // 上下文对象
    private readonly object _context;
    private readonly List<Unit> _targets; // 仅在需要时使用

    public UnifiedExpressionEngine(object context, List<Unit> targets = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _targets = targets;
    }

    // 1. 过滤功能（原ExpressionEvaluator）
    public List<Unit> FilterTargets(string expression)
    {
        if (string.IsNullOrEmpty(expression) || _targets == null || _targets.Count == 0)
            return new List<Unit>();

        try
        {
            var predicate = GetCompiledPredicate<Unit, Unit, bool>(expression, "Unit", "Target");
            var results = new List<Unit>();

            foreach (var target in _targets)
            {
                if (target == null) continue;
                if (predicate(_context as Unit, target))
                {
                    results.Add(target);
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "目标过滤");
            return new List<Unit>();
        }
    }

    // 2. 数学计算（原ExpressionEvaluator）
    public T Evaluate<T>(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            // 检查逻辑运算符（纯数学计算不支持）
            var logicOperators = new[] { "&&", "||", "!", "and", "or", "not", "==", "!=", "<", ">", "<=", ">=" };
            if (logicOperators.Any(op => expression.Contains(op)))
                throw new ArgumentException("数学表达式不能包含逻辑运算符", nameof(expression));

            var lambda = GetCompiledLambda<object, object, T>(expression, "Context", "Target");
            return lambda(_context, _targets?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "数学计算");
            throw;
        }
    }

    // 3. 属性赋值（原ExpressionExecutor）
    public void ExecuteAssignment(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            var parts = expression.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                throw new FormatException("表达式格式不正确，必须是赋值形式 A = B");

            var leftExpr = parts[0].Trim();
            var rightExpr = parts[1].Trim();

            // 编译右边的表达式
            var rightLambda = GetCompiledLambda<object, object, object>(rightExpr, "Context", "Target");
            var rightValue = rightLambda(_context, _targets?.FirstOrDefault());

            // 执行赋值
            SetPropertyValue(_context, leftExpr, rightValue);
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "属性赋值");
            throw;
        }
    }

    // 4. 批量赋值
    public void ExecuteAssignments(IEnumerable<string> expressions)
    {
        foreach (var expr in expressions)
        {
            ExecuteAssignment(expr);
        }
    }

    // 核心：通用表达式编译方法（支持强类型）
    private Func<T1, T2, TResult> GetCompiledPredicate<T1, T2, TResult>(string expression, string param1Name, string param2Name)
    {
        var key = GenerateCacheKey(expression, typeof(T1), typeof(T2), typeof(TResult));

        if (_compiledExpressions.TryGetValue(key, out var cached))
            return (Func<T1, T2, TResult>)cached;

        var param1 = Expression.Parameter(typeof(T1), param1Name);
        var param2 = Expression.Parameter(typeof(T2), param2Name);
        var parameters = new[] { param1, param2 };

        var processedExpr = PreprocessExpression(expression);
        var lambda = DynamicExpressionParser.ParseLambda(
            parameters,
            typeof(TResult),
            processedExpr,
            GetParsingConfig()
        );

        var compiled = lambda.Compile() as Func<T1, T2, TResult>;
        if (compiled == null)
            throw new InvalidOperationException($"无法编译表达式为 Func<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(TResult).Name}>");

        _compiledExpressions[key] = compiled;
        return compiled;
    }

    // 通用Lambda编译方法
    private Func<T1, T2, TResult> GetCompiledLambda<T1, T2, TResult>(string expression, string param1Name, string param2Name)
    {
        var key = GenerateCacheKey(expression, typeof(T1), typeof(T2), typeof(TResult));

        if (_compiledExpressions.TryGetValue(key, out var cached))
            return (Func<T1, T2, TResult>)cached;

        var param1 = Expression.Parameter(typeof(T1), param1Name);
        var param2 = Expression.Parameter(typeof(T2), param2Name);
        var parameters = new[] { param1, param2 };

        var processedExpr = PreprocessExpression(expression);
        var lambda = DynamicExpressionParser.ParseLambda(
            parameters,
            typeof(TResult),
            processedExpr,
            GetParsingConfig()
        );

        var compiled = lambda.Compile() as Func<T1, T2, TResult>;
        if (compiled == null)
            throw new InvalidOperationException($"无法编译表达式为 Func<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(TResult).Name}>");

        _compiledExpressions[key] = compiled;
        return compiled;
    }

    // 属性设置方法（处理赋值）
    private void SetPropertyValue(object obj, string propertyPath, object value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (string.IsNullOrEmpty(propertyPath)) throw new ArgumentException("属性路径不能为空", nameof(propertyPath));

        // 处理嵌套属性 (如 "Buff.Unit.AttackRate")
        var properties = propertyPath.Split('.');
        object currentObj = obj;

        for (int i = 0; i < properties.Length; i++)
        {
            var propName = properties[i];
            var type = currentObj.GetType();

            // 从缓存中获取成员信息
            if (!_memberCache.TryGetValue(type, out var members))
            {
                members = new Dictionary<string, MemberInfo>();
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    members[prop.Name] = prop;
                }
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    members[field.Name] = field;
                }
                _memberCache[type] = members;
            }

            if (!members.TryGetValue(propName, out var member))
            {
                throw new ArgumentException($"对象 {type.Name} 没有属性或字段 '{propName}'");
            }

            if (i == properties.Length - 1)
            {
                // 最后一个属性，进行赋值
                if (member is PropertyInfo prop)
                {
                    // 类型转换
                    var propType = prop.PropertyType;
                    object convertedValue = null;

                    if (value != null && propType.IsAssignableFrom(value.GetType()))
                    {
                        convertedValue = value;
                    }
                    else if (propType.IsValueType && value == null)
                    {
                        convertedValue = Activator.CreateInstance(propType);
                    }
                    else
                    {
                        try
                        {
                            convertedValue = Convert.ChangeType(value, propType);
                        }
                        catch
                        {
                            throw new InvalidOperationException($"无法将值 {value} 转换为类型 {propType.Name}");
                        }
                    }

                    prop.SetValue(currentObj, convertedValue);
                }
                else if (member is FieldInfo field)
                {
                    // 类型转换
                    var fieldType = field.FieldType;
                    object convertedValue = null;

                    if (value != null && fieldType.IsAssignableFrom(value.GetType()))
                    {
                        convertedValue = value;
                    }
                    else if (fieldType.IsValueType && value == null)
                    {
                        convertedValue = Activator.CreateInstance(fieldType);
                    }
                    else
                    {
                        try
                        {
                            convertedValue = Convert.ChangeType(value, fieldType);
                        }
                        catch
                        {
                            throw new InvalidOperationException($"无法将值 {value} 转换为类型 {fieldType.Name}");
                        }
                    }

                    field.SetValue(currentObj, convertedValue);
                }
                else
                {
                    throw new NotSupportedException($"不支持的成员类型: {member.GetType().Name}");
                }
            }
            else
            {
                // 中间属性，获取下一个对象
                if (member is PropertyInfo prop)
                {
                    currentObj = prop.GetValue(currentObj);
                }
                else if (member is FieldInfo field)
                {
                    currentObj = field.GetValue(currentObj);
                }
                else
                {
                    throw new NotSupportedException($"不支持的成员类型: {member.GetType().Name}");
                }

                if (currentObj == null)
                {
                    throw new NullReferenceException($"属性路径中的对象为 null: {string.Join(".", properties.Take(i + 1))}");
                }
            }
        }
    }

    // 通用缓存键生成
    private string GenerateCacheKey(string expression, params Type[] types)
    {
        var processedExpr = PreprocessExpression(expression);
        var normalizedExpr = System.Text.RegularExpressions.Regex.Replace(processedExpr, @"\s+", " ").Trim();
        var typeNames = string.Join("_", types.Select(t => t.Name));
        return $"{normalizedExpr}_{typeNames}";
    }

    // 共享的预处理逻辑
    private string PreprocessExpression(string expression)
    {
        return expression.Replace("and", "&&")
                        .Replace("or", "||")
                        .Replace("not", "!")
                        .Replace("  ", " ")
                        .Trim();
    }

    // 共享的解析配置
    private ParsingConfig GetParsingConfig()
    {
        var customTypeProvider = new CustomDynamicLinqTypeProvider();
        customTypeProvider.AdditionalTypes.Add(typeof(string[]));
        customTypeProvider.AdditionalTypes.Add(typeof(System.Linq.Enumerable));

        return new ParsingConfig { CustomTypeProvider = customTypeProvider };
    }

    // 统一的错误处理
    private void HandleExpressionError(Exception ex, string expression, string operationType)
    {
        var innerException = ex.InnerException ?? ex;
        Debug.LogError($"{operationType}表达式错误: {innerException.Message}\n表达式: {expression}");

        // 假设 TipManager 存在，否则可以注释掉
        if (TipManager.Instance != null)
        {
            TipManager.Instance.ShowTip($"{operationType}表达式错误: {innerException.Message}\n表达式: {expression}");
        }
    }

    // 清理缓存
    public static void ClearCache()
    {
        _compiledExpressions.Clear();
        _memberCache.Clear();
    }
}