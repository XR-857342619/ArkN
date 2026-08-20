using UnityEngine;

/// <summary>
/// 治疗效果器：对目标进行治疗。
/// Data: HealRate, HealBase, IfShowHeal
/// </summary>
public class HealEffect : ISkillEffect
{
    public string Name => "治疗";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        var healRate = data.GetFloat("HealRate", 1f);
        var healBase = data.GetInt("HealBase", 0);
        var ifShow = data.GetBool("IfShowHeal") || !data.ContainsKey("IfShowHeal");

        foreach (var target in EffectUtil.GetTargets(context))
        {
            if (target == null) continue;

            var healInfo = new DamageInfo
            {
                Target = target,
                Source = context.Skill,
                DamageType = DamageTypeEnum.Heal,
                DamageRate = healRate,
            };

            if (healBase == 1)
            {
                healInfo.Attack = target.MaxHp;
            }
            else
            {
                healInfo.Attack = context.Caster.Attack;
            }

            target.Heal(healInfo, ifShow);
        }
    }
}
