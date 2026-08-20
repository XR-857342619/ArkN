using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害效果器：对 context.Targets 造成伤害。
/// Data: DamageRate, DamageBase, DamageType, DamageCount, AreaRange
/// </summary>
public class DamageEffect : ISkillEffect
{
    public string Name => "伤害";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        var targets = EffectUtil.GetTargets(context);
        if (targets.Count == 0) return;

        var damageRate = data.GetFloat("DamageRate", 1f);
        var damageBase = data.GetInt("DamageBase", 0);
        var damageType = EffectUtil.ParseDamageType(data.GetStr("DamageType"));

        foreach (var target in targets)
        {
            if (target == null) continue;

            var damageInfo = new DamageInfo
            {
                Target = target,
                Source = context.Skill,
                DamageType = damageType,
                DamageRate = damageRate,
                MinDamageRate = context.Caster.UnitData.MinDamageRate,
            };

            switch (damageBase)
            {
                case 1:
                    damageInfo.Attack = target.MaxHp;
                    break;
                case 2:
                    damageInfo.Attack = damageRate;
                    damageInfo.DamageRate = 1f;
                    break;
                default:
                    damageInfo.Attack = context.Caster.Attack;
                    break;
            }

            target.Damage(damageInfo);
        }
    }
}
