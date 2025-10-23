using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;
using System.Reflection;
using UnityEngine;

public class ExpressionExecutor
{
    //private readonly Unit _buffSourceUnit; // 施加Buff的单位
    //private readonly Unit _buffTargetUnit; // 被Buff影响的单位
    private readonly Buff _buff;

    public ExpressionExecutor(Buff buff)
    {
        _buff = buff ?? throw new ArgumentNullException(nameof(buff));
        //_buffTargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
    }

    /// <summary>
    /// 执行赋值表达式（如 Buff.Unit.AttackRate = Buff.Skill.Unit.Hp / Buff.Skill.Unit.MaxHp）
    /// </summary>
    public void ExecuteAssignment(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("表达式不能为空", nameof(expression));

        try
        {
            // 分割赋值表达式（左侧为属性路径，右侧为值表达式）
            var parts = expression.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                throw new FormatException("表达式格式错误，需包含赋值操作（如 A = B）");

            var leftExpr = parts[0].Trim();  // 例如 "Buff.Unit.SpeedAdd"
            var rightExpr = parts[1].Trim(); // 例如 "Buff.Duration.value"

            // 创建参数（仅使用Buff作为根参数）
            var buffParam = Expression.Parameter(typeof(Buff), "Buff");
            var parameters = new[] { buffParam };

            // 解析左侧属性路径（构建赋值目标）
            var leftAccess = ParseMemberAccess(leftExpr, parameters);
            if (!(leftAccess is MemberExpression leftMember))
                throw new InvalidOperationException("左侧表达式必须是可赋值的属性/字段");

            // 解析右侧值表达式（构建赋值来源）
            var rightAccess = ParseMemberAccess(rightExpr, parameters);

            // 构建赋值表达式树（a = b）
            var assignment = Expression.Assign(leftMember, rightAccess);
            var lambda = Expression.Lambda<Action<Buff>>(assignment, buffParam);

            // 执行赋值
            lambda.Compile().Invoke(_buff);
        }
        catch (Exception ex)
        {
            var innerException = ex.InnerException ?? ex;
            Debug.LogError($"赋值表达式执行失败: {innerException.Message}\n表达式: {expression}");
            TipManager.Instance.ShowTip($"赋值表达式执行失败: {innerException.Message}\n表达式: {expression}");
            throw;
        }
    }

    // 辅助方法：解析成员访问路径（如 "Buff.Unit.SpeedAdd"）
    private Expression ParseMemberAccess(string path, ParameterExpression[] parameters)
    {
        var parts = path.Split('.');
        if (parts.Length == 0)
            throw new FormatException($"无效的成员路径: {path}");

        // 找到根参数（目前仅支持"Buff"作为根）
        var root = parameters.FirstOrDefault(p => p.Name == parts[0]);
        if (root == null)
            throw new KeyNotFoundException($"未找到参数: {parts[0]}");

        Expression current = root;
        // 解析后续成员（如 "Unit" -> "SpeedAdd"）
        for (int i = 1; i < parts.Length; i++)
        {
            var memberName = parts[i];
            var currentType = current.Type;
            // 查找字段或属性
            var member = currentType.GetField(memberName) as MemberInfo ??
                         currentType.GetProperty(memberName);
            if (member == null)
                throw new MissingMemberException(currentType.Name, memberName);
            current = Expression.MakeMemberAccess(current, member);
        }
        return current;
    }

    private string PreprocessExpression(string expression)
    {
        // 替换表达式中的关键字为内部参数名
        return expression
            //.Replace("Buff.Unit.", "Buff_Unit.")
            //.Replace("Buff.Skill.Unit.", "Buff_Skill_Unit.")
            .Replace("and", "&&")
            .Replace("or", "||")
            .Replace("not", "!")
            .Replace("  ", " ")
            .Trim();
    }
}