using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 校验 SkillJsonData 的 Type、Trigger 和必填字段。
/// 可在编辑器保存时和运行时加载后调用。
/// </summary>
public static class SkillJsonValidator
{
    public static List<string> Validate(SkillJsonData data)
    {
        var errors = new List<string>();

        if (data == null)
        {
            errors.Add("SkillJsonData 为 null");
            return errors;
        }

        if (string.IsNullOrEmpty(data.Id))
        {
            errors.Add("技能 Id 不能为空");
        }

        ValidateSelectors(data.Selectors, errors);
        ValidateSorters(data.Sorters, errors);
        ValidateEffects(data.Effects, errors);

        return errors;
    }

    public static string ValidateToString(SkillJsonData data)
    {
        var errors = Validate(data);
        if (errors.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"SkillJson {data?.Id} 校验失败：");
        foreach (var error in errors)
        {
            sb.AppendLine("- " + error);
        }
        return sb.ToString();
    }

    private static void ValidateSelectors(List<SelectorNode> selectors, List<string> errors)
    {
        if (selectors == null) return;

        for (int i = 0; i < selectors.Count; i++)
        {
            var node = selectors[i];
            if (node == null)
            {
                errors.Add($"Selectors[{i}] 为 null");
                continue;
            }

            if (string.IsNullOrEmpty(node.Type))
            {
                errors.Add($"Selectors[{i}].Type 为空");
                continue;
            }

            if (!TargetSelectorFactory.ContainsSelector(node.Type) && !TargetSelectorFactory.ContainsFilter(node.Type))
            {
                errors.Add($"Selectors[{i}].Type='{node.Type}' 未找到对应的 ISelectorStrategy/IFilterStrategy");
            }
        }
    }

    private static void ValidateSorters(List<SorterNode> sorters, List<string> errors)
    {
        if (sorters == null) return;

        for (int i = 0; i < sorters.Count; i++)
        {
            var node = sorters[i];
            if (node == null)
            {
                errors.Add($"Sorters[{i}] 为 null");
                continue;
            }

            if (string.IsNullOrEmpty(node.Type))
            {
                errors.Add($"Sorters[{i}].Type 为空");
                continue;
            }

            if (!TargetSelectorFactory.ContainsSorter(node.Type))
            {
                errors.Add($"Sorters[{i}].Type='{node.Type}' 未找到对应的 ISortStrategy");
            }
        }
    }

    private static void ValidateEffects(List<EffectNode> effects, List<string> errors)
    {
        if (effects == null) return;

        for (int i = 0; i < effects.Count; i++)
        {
            var node = effects[i];
            if (node == null)
            {
                errors.Add($"Effects[{i}] 为 null");
                continue;
            }

            if (string.IsNullOrEmpty(node.Type))
            {
                errors.Add($"Effects[{i}].Type 为空");
                continue;
            }

            if (!SkillEffectFactory.Contains(node.Type))
            {
                errors.Add($"Effects[{i}].Type='{node.Type}' 未找到对应的 ISkillEffect");
            }

            if (string.IsNullOrEmpty(node.Trigger))
            {
                errors.Add($"Effects[{i}].Trigger 为空");
                continue;
            }

            if (!TryParseTrigger(node.Trigger, out _))
            {
                errors.Add($"Effects[{i}].Trigger='{node.Trigger}' 不是合法的 SkillEffectTrigger");
            }
        }
    }

    public static bool TryParseTrigger(string trigger, out SkillEffectTrigger result)
    {
        result = SkillEffectTrigger.None;
        if (string.IsNullOrEmpty(trigger)) return false;

        // 兼容 "OnCast"、"Cast"、"On攻击"、"释放技能" 等写法
        var normalized = trigger.Trim();
        if (Enum.TryParse(normalized, true, out result))
        {
            return true;
        }

        // 去掉 On 前缀再试一次
        if (normalized.StartsWith("On", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(normalized.Substring(2), true, out result))
        {
            return true;
        }

        // 不带 On 前缀的英文映射
        var lower = normalized.ToLowerInvariant();
        switch (lower)
        {
            case "start": result = SkillEffectTrigger.OnStart; return true;
            case "cast": result = SkillEffectTrigger.OnCast; return true;
            case "attack": result = SkillEffectTrigger.OnAttack; return true;
            case "hit": result = SkillEffectTrigger.OnHit; return true;
            case "end": result = SkillEffectTrigger.OnEnd; return true;
            case "break": result = SkillEffectTrigger.OnBreak; return true;
            case "kill": result = SkillEffectTrigger.OnKill; return true;
            case "death": result = SkillEffectTrigger.OnDeath; return true;
            case "init": result = SkillEffectTrigger.OnInit; return true;
            case "loopstart": result = SkillEffectTrigger.OnLoopStart; return true;
            case "looptick": result = SkillEffectTrigger.OnLoopTick; return true;
            case "loopend": result = SkillEffectTrigger.OnLoopEnd; return true;
        }

        // 旧 TriggerEnum 中文名映射
        switch (normalized)
        {
            case "起始": result = SkillEffectTrigger.OnStart; return true;
            case "释放技能": result = SkillEffectTrigger.OnCast; return true;
            case "攻击": result = SkillEffectTrigger.OnAttack; return true;
            case "击中": result = SkillEffectTrigger.OnHit; return true;
            case "技能结束": result = SkillEffectTrigger.OnEnd; return true;
            case "击杀": result = SkillEffectTrigger.OnKill; return true;
            case "死亡": result = SkillEffectTrigger.OnDeath; return true;
        }

        return false;
    }
}
