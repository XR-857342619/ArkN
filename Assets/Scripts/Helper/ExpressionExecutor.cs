using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;
using System.Reflection;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using UnityEngine;

public class ExpressionExecutor
{
    private readonly Buff _buff;
    // 复用缓存机制存储编译后的赋值表达式
    private static readonly Dictionary<string, Action<Buff>> _assignmentCache = new Dictionary<string, Action<Buff>>();
    // 复用成员缓存提升反射性能
    private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> _memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

    public ExpressionExecutor(Buff buff)
    {
        _buff = buff ?? throw new ArgumentNullException(nameof(buff));
    }

    /// <summary>
    /// 执行赋值表达式，如 Buff.Unit.AttackRate = Buff.Skill.Unit.Hp / Buff.Skill.Unit.MaxHp
    /// </summary>
    public void ExecuteAssignment(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            // 检查缓存，存在则直接使用
            //if (_assignmentCache.TryGetValue(expression, out var cachedAction))
            //{
            //    cachedAction.Invoke(_buff);
            //    return;
            //}

            var parts = expression.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                throw new FormatException("表达式格式不正确，必须是赋值形式 A = B");

            var leftExpr = parts[0].Trim();
            var rightExpr = parts[1].Trim();

            var buffParam = Expression.Parameter(typeof(Buff), "Buff");
            var parameters = new[] { buffParam };

            // 使用优化的成员访问解析（带缓存）
            var leftAccess = ParseMemberAccessWithCache(leftExpr, parameters);
            if (!(leftAccess is MemberExpression leftMember))
                throw new InvalidOperationException("左侧表达式必须是可赋值的成员/字段");

            // 复用预处理逻辑
            var processedRightExpr = PreprocessExpression(rightExpr);

            // 使用自定义类型提供器，支持更多类型
            var customTypeProvider = new CustomDynamicLinqTypeProvider();
            customTypeProvider.AdditionalTypes.Add(typeof(string[]));
            customTypeProvider.AdditionalTypes.Add(typeof(System.Linq.Enumerable));

            var parsingConfig = new ParsingConfig
            {
                CustomTypeProvider = customTypeProvider
            };

            // 解析右侧表达式
            var rightLambda = DynamicExpressionParser.ParseLambda(
                parameters,
                null,
                processedRightExpr,
                parsingConfig
            );
            var rightAccess = rightLambda.Body;

            // 类型转换处理
            if (leftMember.Type != rightAccess.Type)
            {
                rightAccess = Expression.Convert(rightAccess, leftMember.Type);
            }

            //// ********** 调试日志输出部分 **********
            //// 编译左边成员的获取表达式
            //var leftValueLambda = Expression.Lambda(Expression.Convert(leftMember, typeof(object)), buffParam);
            //var leftValueFunc = (Func<Buff, object>)leftValueLambda.Compile();

            //// 编译右边值的获取表达式
            //var rightValueLambda = Expression.Lambda(Expression.Convert(rightAccess, typeof(object)), buffParam);
            //var rightValueFunc = (Func<Buff, object>)rightValueLambda.Compile();

            //// 获取当前值并输出日志
            //var leftValue = leftValueFunc(_buff);
            //var rightValue = rightValueFunc(_buff);
            //Debug.Log($"执行赋值: {leftExpr} = {rightExpr}\n" +
            //          $"左侧当前值: {leftValue} (类型: {leftMember.Type.Name})\n" +
            //          $"右侧计算值: {rightValue} (类型: {rightAccess.Type.Name})");
            //// ********** 调试日志输出部分结束 **********


            var assignment = Expression.Assign(leftMember, rightAccess);
            var lambda = Expression.Lambda<Action<Buff>>(assignment, buffParam);
            var action = lambda.Compile();

            // 加入缓存
            _assignmentCache[expression] = action;
            action.Invoke(_buff);
        }
        catch (Exception ex)
        {
            var innerException = ex.InnerException ?? ex;
            Debug.LogError($"赋值表达式执行失败: {innerException.Message}\n表达式: {expression}");
            TipManager.Instance.ShowTip($"赋值表达式执行失败: {innerException.Message}\n表达式: {expression}");
            throw;
        }
    }

    // 带缓存的成员访问解析
    private Expression ParseMemberAccessWithCache(string path, ParameterExpression[] parameters)
    {
        var parts = path.Split('.');
        if (parts.Length == 0)
            throw new FormatException($"无效的成员路径: {path}");

        var root = parameters.FirstOrDefault(p => p.Name == parts[0]);
        if (root == null)
            throw new KeyNotFoundException($"未找到参数: {parts[0]}");

        Expression current = root;
        for (int i = 1; i < parts.Length; i++)
        {
            var memberName = parts[i];
            var currentType = current.Type;

            // 检查成员缓存
            if (!_memberCache.TryGetValue(currentType, out var memberDict))
            {
                memberDict = new Dictionary<string, MemberInfo>();
                _memberCache[currentType] = memberDict;
            }

            // 从缓存获取成员信息
            if (!memberDict.TryGetValue(memberName, out var member))
            {
                member = currentType.GetField(memberName) as MemberInfo ??
                         currentType.GetProperty(memberName);
                if (member == null)
                    throw new MissingMemberException(currentType.Name, memberName);
                memberDict[memberName] = member;
            }

            current = Expression.MakeMemberAccess(current, member);
        }
        return current;
    }

    // 复用表达式预处理方法
    private string PreprocessExpression(string expression)
    {
        // 与ExpressionEvaluator保持一致的预处理逻辑
        return expression.Replace("and", "&&")
                        .Replace("or", "||")
                        .Replace("not", "!")
                        .Replace("  ", " ")
                        .Trim();
    }

    // 新增缓存清理方法，与ExpressionEvaluator保持一致
    public static void ClearCache()
    {
        _assignmentCache.Clear();
        _memberCache.Clear();
    }
}