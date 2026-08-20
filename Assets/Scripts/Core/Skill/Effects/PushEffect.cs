using UnityEngine;

/// <summary>
/// 推动效果器。
/// Data: Power/PushPower, Direction(可选，默认施法者朝向)
/// </summary>
public class PushEffect : ISkillEffect
{
    public string Name => "推动";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        var power = data.GetInt("Power", data.GetInt("PushPower", 0));
        if (power <= 0) return;

        Vector2 direction;
        if (data.ContainsKey("Direction"))
        {
            direction = ParseVector2(data["Direction"]);
            if (direction == Vector2.zero) direction = context.Caster.Direction;
        }
        else
        {
            direction = context.Caster.Direction;
        }

        foreach (var target in EffectUtil.GetTargets(context))
        {
            if (target == null || target.Height > 0) continue;

            var existing = target.PushBuffs.Find(x => x is Buffs.推动 b && b.Skill == context.Skill) as Buffs.推动;
            if (existing == null)
            {
                var push = new Buffs.推动
                {
                    Skill = context.Skill,
                    Unit = target,
                    Power = power,
                    Direction = direction,
                };
                target.AddPush(push);
            }
            else
            {
                existing.Power = power;
                existing.Direction = direction;
            }
        }
    }

    private Vector2 ParseVector2(object value)
    {
        if (value is Newtonsoft.Json.Linq.JArray arr && arr.Count >= 2)
        {
            return new Vector2(
                System.Convert.ToSingle(arr[0]),
                System.Convert.ToSingle(arr[1]));
        }

        return Vector2.zero;
    }
}
