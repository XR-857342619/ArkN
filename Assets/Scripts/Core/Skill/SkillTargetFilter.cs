using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

public class SkillTargetFilter
{
    // 存储Unit对象（技能拥有者）
    private Unit _unit;

    // 存储目标列表
    private List<Unit> _targets = new List<Unit>();

    public SkillTargetFilter(Unit unit = null, List<Unit> targets = null)
    {
        _unit = unit;
        if (targets != null)
        {
            _targets = targets;
        }
    }

    /// <summary>
    /// 筛选满足表达式的目标
    /// </summary>
    /// <param name="expression">筛选表达式字符串</param>
    /// <returns>满足条件的目标列表</returns>
    public List<Unit> FilterTargets(string expression)
    {
        List<Unit> validTargets = new List<Unit>();

        if (_targets.Count == 0)
        {
            return validTargets;
        }

        // 预处理表达式：替换Unit属性访问（只需要做一次）
        string expressionWithUnitValues = PreprocessUnitExpression(expression);

        foreach (var target in _targets)
        {
            try
            {
                // 预处理表达式：替换Target属性访问（对每个目标都需要做）
                string finalExpression = PreprocessTargetExpression(expressionWithUnitValues, target);

                // 解析并计算表达式
                double result = EvaluateExpression(finalExpression);

                // 检查表达式是否成立（结果是否为真，通常非零为真）
                if (Math.Abs(result) > double.Epsilon)
                {
                    validTargets.Add(target);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"表达式验证错误: {ex.Message}");
                // 忽略错误的目标，继续处理其他目标
            }
        }

        return validTargets;
    }

    /// <summary>
    /// 预处理表达式，替换Unit属性访问
    /// </summary>
    private string PreprocessUnitExpression(string expression)
    {
        if (_unit == null)
        {
            throw new InvalidOperationException("Unit对象未设置，无法处理Unit字段");
        }

        // 匹配 Unit.fieldName 模式
        var regex = new Regex(@"Unit\.([a-zA-Z_][a-zA-Z0-9_]*)");
        var matches = regex.Matches(expression);

        foreach (Match match in matches)
        {
            // 提取字段名
            string fieldName = match.Groups[1].Value;

            // 获取字段值
            double fieldValue = GetFieldValue(_unit, fieldName);

            // 替换表达式中的属性访问为实际值
            expression = expression.Replace(match.Value, fieldValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return expression;
    }

    /// <summary>
    /// 预处理表达式，替换Target属性访问
    /// </summary>
    private string PreprocessTargetExpression(string expression, Unit target)
    {
        // 匹配 Target.fieldName 模式
        var regex = new Regex(@"Target\.([a-zA-Z_][a-zA-Z0-9_]*)");
        var matches = regex.Matches(expression);

        foreach (Match match in matches)
        {
            // 提取字段名
            string fieldName = match.Groups[1].Value;

            // 获取字段值
            double fieldValue = GetFieldValue(target, fieldName);

            // 替换表达式中的属性访问为实际值
            expression = expression.Replace(match.Value, fieldValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return expression;
    }

    /// <summary>
    /// 获取字段的值
    /// </summary>
    private double GetFieldValue(Unit obj, string fieldName)
    {
        try
        {
            // 使用反射获取字段值
            Type objType = obj.GetType();
            FieldInfo fieldInfo = objType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null)
            {
                throw new ArgumentException($"找不到字段: {fieldName}");
            }

            object value = fieldInfo.GetValue(obj);

            // 转换为double
            return Convert.ToDouble(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取字段值失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 计算数学表达式
    /// </summary>
    private double EvaluateExpression(string expression)
    {
        // 移除所有空格
        expression = expression.Replace(" ", "");

        // 处理逻辑运算符
        expression = expression.Replace("and", "&&");
        expression = expression.Replace("or", "||");
        expression = expression.Replace("not", "!");

        // 处理比较运算符
        expression = expression.Replace("=", "==");

        // 使用DataTable计算表达式
        var dataTable = new System.Data.DataTable();
        var result = dataTable.Compute(expression, "");
        Log.Debug(result);

        // 处理布尔结果
        if (result is bool boolResult)
        {
            return boolResult ? 1.0 : 0.0;
        }

        return Convert.ToDouble(result);
    }

    /// <summary>
    /// 设置Unit对象
    /// </summary>
    public void SetUnitObject(Unit unitObject)
    {
        _unit = unitObject;
    }

    /// <summary>
    /// 设置目标列表
    /// </summary>
    public void SetTargets(List<Unit> targets)
    {
        _targets = targets;
    }

    /// <summary>
    /// 添加目标
    /// </summary>
    public void AddTarget(Unit target)
    {
        _targets.Add(target);
    }

    /// <summary>
    /// 清空目标列表
    /// </summary>
    public void ClearTargets()
    {
        _targets.Clear();
    }

    /// <summary>
    /// 获取所有可用的字段名称
    /// </summary>
    public List<string> GetAvailableFieldNames(Unit obj)
    {
        if (obj == null)
        {
            return new List<string>();
        }

        Type objType = obj.GetType();
        var fields = objType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        return fields.Select(f => f.Name).ToList();
    }

    /// <summary>
    /// 验证表达式语法是否正确（不实际计算值）
    /// </summary>
    public bool ValidateExpressionSyntax(string expression)
    {
        try
        {
            // 先替换所有Unit.fieldName和Target.fieldName为占位符值
            string testExpression = Regex.Replace(expression, @"(Unit|Target)\.[a-zA-Z_][a-zA-Z0-9_]*", "1.0");

            // 尝试计算表达式
            var dataTable = new System.Data.DataTable();
            var result = dataTable.Compute(testExpression, "");

            return true;
        }
        catch
        {
            return false;
        }
    }
}
