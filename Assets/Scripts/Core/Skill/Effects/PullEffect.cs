using UnityEngine;

/// <summary>
/// 拉动效果器。
/// Data: Power/PushPower
/// </summary>
public class PullEffect : ISkillEffect
{
    public string Name => "拉动";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        var power = data.GetInt("Power", data.GetInt("PushPower", 0));
        if (power <= 0) return;

        foreach (var target in EffectUtil.GetTargets(context))
        {
            if (target == null || target.Height > 0) continue;

            var pull = new Buffs.拉动
            {
                Skill = context.Skill,
                Source = context.Caster,
                Unit = target,
                Power = power,
                FullDuration = data.GetFloat("FullDuration", power - target.Weight > -1 ? 1f : 0.5f),
            };
            pull.Init();
            target.AddPush(pull);
        }
    }
}
