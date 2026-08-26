using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
//using System.Linq.Dynamic.Core.Parser;
using System.Reflection;
//using System.Linq.Dynamic.Core.CustomTypeProviders;
using UnityEngine;



public enum NumericChangeMode
{
    Add,
    Set,
    Max,
}
/// <summary>
/// 统一表达式引擎，支持：
/// - 强类型编译（基于实际对象类型，提升性能）
/// - 自定义参数名（默认 Context / Target）
/// - 过滤、数学计算、属性赋值
/// - 缓存机制（包含参数名和类型）
/// </summary>
public class UnifiedExpressionEngine
{
    private static readonly Dictionary<string, Delegate> _compiledExpressions = new Dictionary<string, Delegate>();
    private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> _memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();
    private static readonly Dictionary<string, Func<object, float>> _numericGetterCache = new Dictionary<string, Func<object, float>>();
    private static readonly Dictionary<string, Action<object, float>> _numericSetterCache = new Dictionary<string, Action<object, float>>();

    private readonly object _context;
    private readonly List<Unit> _targets;

    public UnifiedExpressionEngine(object context, List<Unit> targets = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _targets = targets;
    }

    // ===== 1. 过滤（原 ExpressionEvaluator 功能） =====
    public List<Unit> FilterTargets(string expression, string contextName = "Unit", string targetName = "Target")
    {
        if (string.IsNullOrEmpty(expression) || _targets == null || _targets.Count == 0)
            return new List<Unit>();

        try
        {
            var predicate = CompileLambda<Unit, Unit, bool>(expression, contextName, targetName);
            var results = new List<Unit>();
            foreach (var target in _targets)
            {
                if (target == null) continue;
                if (predicate(_context as Unit, target))
                    results.Add(target);
            }
            return results;
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "目标过滤");
            return new List<Unit>();
        }
    }

    // ===== 2. 数学计算 =====
    public T Evaluate<T>(string expression, string contextName = "Buff", string targetName = "Target")
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            var logicOperators = new[] { "&&", "||", "!", "and", "or", "not", "==", "!=", "<", ">", "<=", ">=" };
            if (logicOperators.Any(op => expression.Contains(op)))
                throw new ArgumentException("数学表达式不能包含逻辑运算符", nameof(expression));

            var contextType = _context.GetType();
            var targetType = _targets?.FirstOrDefault()?.GetType() ?? typeof(object);
            var del = CompileLambdaInternal(expression, contextType, targetType, typeof(T), contextName, targetName);
            return (T)del.DynamicInvoke(_context, _targets?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "数学计算");
            throw;
        }
    }

    // ===== 3. 属性赋值 =====
    public void ExecuteAssignment(string expression, string contextName = "Buff", string targetName = "Target")
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

            var contextType = _context.GetType();
            var targetType = _targets?.FirstOrDefault()?.GetType() ?? typeof(object);
            var rightDel = CompileLambdaInternal(rightExpr, contextType, targetType, typeof(object), contextName, targetName);
            var rightValue = rightDel.DynamicInvoke(_context, _targets?.FirstOrDefault());

            var leftPath = leftExpr;
            if (leftExpr.StartsWith(contextName + "."))
                leftPath = leftExpr.Substring(contextName.Length + 1);
            else if (leftExpr.StartsWith(targetName + "."))
                leftPath = leftExpr.Substring(targetName.Length + 1);

            SetPropertyValue(_context, leftPath, rightValue);
        }
        catch (Exception ex)
        {
            HandleExpressionError(ex, expression, "属性赋值");
            throw;
        }
    }

    // 批量赋值
    public void ExecuteAssignments(IEnumerable<string> expressions, string contextName = "Buff", string targetName = "Target")
    {
        foreach (var expr in expressions)
            ExecuteAssignment(expr, contextName, targetName);
    }

    // ===== 4. 数值变化类 Buff 专用：无反射的成员读取与修改 =====
    public float GetNumericValue(object target, string memberPath)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrEmpty(memberPath)) throw new ArgumentException("成员路径不能为空", nameof(memberPath));

        var key = target.GetType().FullName + "|" + memberPath;
        if (!_numericGetterCache.TryGetValue(key, out var getter))
        {
            getter = CompileNumericGetter(target.GetType(), memberPath);
            _numericGetterCache[key] = getter;
        }

        return getter(target);
    }

    public void ApplyNumericChange(object target, string memberPath, float value, NumericChangeMode mode)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrEmpty(memberPath)) throw new ArgumentException("成员路径不能为空", nameof(memberPath));

        var key = target.GetType().FullName + "|" + memberPath + "|" + mode;
        if (!_numericSetterCache.TryGetValue(key, out var setter))
        {
            setter = CompileNumericSetter(target.GetType(), memberPath, mode);
            _numericSetterCache[key] = setter;
        }

        setter(target, value);
    }

    private Func<object, float> CompileNumericGetter(Type targetType, string memberPath)
    {
        var targetParam = Expression.Parameter(typeof(object), "target");
        var typedTarget = Expression.Convert(targetParam, targetType);
        var member = BuildMemberExpression(typedTarget, memberPath);
        var body = Expression.Convert(member, typeof(float));

        return Expression.Lambda<Func<object, float>>(body, targetParam).Compile();
    }

    private Action<object, float> CompileNumericSetter(Type targetType, string memberPath, NumericChangeMode mode)
    {
        var targetParam = Expression.Parameter(typeof(object), "target");
        var valueParam = Expression.Parameter(typeof(float), "value");
        var typedTarget = Expression.Convert(targetParam, targetType);
        var member = BuildMemberExpression(typedTarget, memberPath);

        Expression valueExpr;
        switch (mode)
        {
            case NumericChangeMode.Set:
                valueExpr = Expression.Convert(valueParam, member.Type);
                break;
            case NumericChangeMode.Add:
                valueExpr = Expression.Convert(
                    Expression.Add(Expression.Convert(member, typeof(float)), valueParam),
                    member.Type);
                break;
            case NumericChangeMode.Max:
                var current = Expression.Convert(member, typeof(float));
                var max = Expression.Condition(
                    Expression.GreaterThan(current, valueParam),
                    current,
                    valueParam);
                valueExpr = Expression.Convert(max, member.Type);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        var assign = Expression.Assign(member, valueExpr);
        return Expression.Lambda<Action<object, float>>(assign, targetParam, valueParam).Compile();
    }

    private Expression BuildMemberExpression(Expression instance, string memberPath)
    {
        Expression current = instance;
            var parts = memberPath.Split('.');


        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
                throw new ArgumentException($"成员路径非法: {memberPath}", nameof(memberPath));

            current = Expression.PropertyOrField(current, parts[i]);
        }

        return current;
    }

    // ===== 核心编译 =====
    private Func<T1, T2, TResult> CompileLambda<T1, T2, TResult>(string expression, string param1Name, string param2Name)
    {
        var del = CompileLambdaInternal(expression, typeof(T1), typeof(T2), typeof(TResult), param1Name, param2Name);
        return (Func<T1, T2, TResult>)del;
    }

    private Delegate CompileLambdaInternal(string expression, Type contextType, Type targetType, Type resultType,
                                           string contextName, string targetName)
    {
        var key = GenerateCacheKey(expression, contextType, targetType, resultType, contextName, targetName);
        if (_compiledExpressions.TryGetValue(key, out var cached))
            return cached;

        var param1 = Expression.Parameter(contextType, contextName);
        var param2 = Expression.Parameter(targetType ?? typeof(object), targetName);
        var parameters = new[] { param1, param2 };

        var processedExpr = PreprocessExpression(expression);
        var lambda = DynamicExpressionParser.ParseLambda(
            parameters,
            resultType,
            processedExpr,
            GetParsingConfig()
        );

        var compiled = lambda.Compile();
        _compiledExpressions[key] = compiled;
        return compiled;
    }

    // ===== 缓存键生成 =====
    private string GenerateCacheKey(string expression, params object[] keyParts)
    {
        var normalizedExpr = System.Text.RegularExpressions.Regex.Replace(expression, @"\s+", " ").Trim();
        var parts = keyParts.Select(p => p?.ToString() ?? "null");
        return $"{normalizedExpr}_{string.Join("_", parts)}";
    }

    // ===== 属性赋值辅助 =====
    private void SetPropertyValue(object obj, string propertyPath, object value)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (string.IsNullOrEmpty(propertyPath)) throw new ArgumentException("属性路径不能为空", nameof(propertyPath));

        var properties = propertyPath.Split('.');
        object currentObj = obj;
        Type currentType = currentObj.GetType();

        for (int i = 0; i < properties.Length; i++)
        {
            var propName = properties[i];

            if (!_memberCache.TryGetValue(currentType, out var members))
            {
                members = new Dictionary<string, MemberInfo>();
                foreach (var prop in currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    members[prop.Name] = prop;
                foreach (var field in currentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    members[field.Name] = field;
                _memberCache[currentType] = members;
            }

            if (!members.TryGetValue(propName, out var member))
                throw new ArgumentException($"对象 {currentType.Name} 没有属性或字段 '{propName}'");

            if (i == properties.Length - 1)
            {
                if (member is PropertyInfo prop)
                {
                    var propType = prop.PropertyType;
                    object convertedValue = ConvertValue(value, propType);
                    prop.SetValue(currentObj, convertedValue);
                }
                else if (member is FieldInfo field)
                {
                    var fieldType = field.FieldType;
                    object convertedValue = ConvertValue(value, fieldType);
                    field.SetValue(currentObj, convertedValue);
                }
                else
                    throw new NotSupportedException($"不支持的成员类型: {member.GetType().Name}");
            }
            else
            {
                if (member is PropertyInfo prop)
                    currentObj = prop.GetValue(currentObj);
                else if (member is FieldInfo field)
                    currentObj = field.GetValue(currentObj);
                else
                    throw new NotSupportedException($"不支持的成员类型: {member.GetType().Name}");

                if (currentObj == null)
                    throw new NullReferenceException($"属性路径中的对象为 null: {string.Join(".", properties.Take(i + 1))}");
                currentType = currentObj.GetType();
            }
        }
    }

    private object ConvertValue(object value, Type targetType)
    {
        if (value == null)
        {
            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType);
            return null;
        }

        var sourceType = value.GetType();
        if (targetType.IsAssignableFrom(sourceType))
            return value;

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            throw new InvalidOperationException($"无法将值 {value} 转换为类型 {targetType.Name}");
        }
    }

    private string PreprocessExpression(string expression)
    {
        return expression.Replace("and", "&&")
                        .Replace("or", "||")
                        .Replace("not", "!")
                        .Replace("  ", " ")
                        .Trim();
    }

    private ParsingConfig GetParsingConfig()
    {
        var customTypeProvider = new CustomDynamicLinqTypeProvider();
        customTypeProvider.AdditionalTypes.Add(typeof(string[]));
        customTypeProvider.AdditionalTypes.Add(typeof(System.Linq.Enumerable));
        customTypeProvider.AdditionalTypes.Add(typeof(Buff));
        customTypeProvider.AdditionalTypes.Add(typeof(Unit));
        customTypeProvider.AdditionalTypes.Add(typeof(PowerRecoverTypeEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(SkillReadyEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(SkillUseTypeEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(TriggerEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(SkillTargetFilterEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(UnitTypeEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(AttackTargetOrderEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(AttackTargetOrder2Enum));
        customTypeProvider.AdditionalTypes.Add(typeof(DamageTypeEnum));
        customTypeProvider.AdditionalTypes.Add(typeof(AttackModeEnum));

        return new ParsingConfig { CustomTypeProvider = customTypeProvider };
    }

    private void HandleExpressionError(Exception ex, string expression, string operationType)
    {
        var innerException = ex.InnerException ?? ex;
        Debug.LogError($"{operationType}表达式错误: {innerException.Message}\n表达式: {expression}");
        TipManager.Instance?.ShowTip($"{operationType}表达式错误: {innerException.Message}\n表达式: {expression}");
    }

    public static void ClearCache()
    {
        _compiledExpressions.Clear();
        _memberCache.Clear();
        _numericGetterCache.Clear();
        _numericSetterCache.Clear();
    }
}