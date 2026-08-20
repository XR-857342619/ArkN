using UnityEngine;

/// <summary>
/// 添加 Buff 效果器。
/// Data: BuffId, Duration, Chance, Index
/// </summary>
public class AddBuffEffect : ISkillEffect
{
    public string Name => "添加Buff";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        if (!data.ContainsKey("BuffId")) return;

        var buffId = EffectUtil.ToConfigId<BuffData>(data["BuffId"]);
        if (buffId <= 0) return;

        var duration = data.GetFloat("Duration", -1f);
        var chance = data.GetFloat("Chance", 1f);
        var index = data.GetInt("Index", 0);

        foreach (var target in EffectUtil.GetTargets(context))
        {
            if (target == null) continue;
            if (chance < 1f && context.Caster.Battle.Random.NextDouble() >= chance) continue;

            target.AddBuff(buffId, context.Skill, index, duration);
        }
    }
}
