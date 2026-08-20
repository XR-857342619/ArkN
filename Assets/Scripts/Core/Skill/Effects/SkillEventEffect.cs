using System.Collections.Generic;

/// <summary>
/// 触发其他技能效果器。
/// Data: SkillIds (数组) 或 SkillId (单个)
/// </summary>
public class SkillEventEffect : ISkillEffect
{
    public string Name => "触发技能";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        var skillIds = new List<object>();

        var array = data.GetArray("SkillIds");
        if (array != null)
        {
            foreach (var item in array) skillIds.Add(item);
        }

        if (data.TryGetValue("SkillId", out var single))
        {
            skillIds.Add(single);
        }

        foreach (var rawId in skillIds)
        {
            var skill = FindSkill(context, rawId);
            if (skill != null)
            {
                skill.Start();
            }
        }
    }

    private Skill FindSkill(SkillContext context, object rawId)
    {
        if (rawId == null) return null;

        if (rawId is int intId)
        {
            return context.Caster.Skills.Find(s => s.Id == intId);
        }

        var str = System.Convert.ToString(rawId);
        if (string.IsNullOrEmpty(str)) return null;

        if (int.TryParse(str, out int direct))
        {
            return context.Caster.Skills.Find(s => s.Id == direct);
        }

        return context.Caster.Skills.Find(s => s.SkillData?.Id == str);
    }
}
