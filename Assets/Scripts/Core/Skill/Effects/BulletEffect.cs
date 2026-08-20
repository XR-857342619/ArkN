using UnityEngine;

/// <summary>
/// 生成子弹效果器。
/// Data: BulletId, ShootPoint, BulletCount
/// </summary>
public class BulletEffect : ISkillEffect
{
    public string Name => "生成子弹";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster?.Battle == null || node?.Data == null) return;

        var data = node.Data;
        if (!data.ContainsKey("BulletId")) return;

        var bulletId = EffectUtil.ToConfigId<BulletData>(data["BulletId"]);
        if (bulletId <= 0) return;

        var shootPoint = data.GetStr("ShootPoint");
        var startPos = string.IsNullOrEmpty(shootPoint) ? context.Caster.Position : context.Caster.UnitModel.GetPoint(shootPoint);
        var count = Mathf.Max(1, data.GetInt("BulletCount", 1));

        var targets = EffectUtil.GetTargets(context);
        for (int i = 0; i < count; i++)
        {
            if (targets.Count == 0)
            {
                var pos = data.ContainsKey("TargetPos")
                    ? ParseVector3(data["TargetPos"])
                    : context.TargetPositions != null && context.TargetPositions.Count > 0
                        ? context.TargetPositions[0]
                        : context.Caster.Position + (Vector3)context.Caster.Direction;
                context.Caster.Battle.CreateBullet(bulletId, startPos, pos, null, context.Skill);
            }
            else
            {
                var target = targets[i % targets.Count];
                context.Caster.Battle.CreateBullet(bulletId, startPos, Vector3.zero, target, context.Skill);
            }
        }
    }

    private Vector3 ParseVector3(object value)
    {
        if (value is Newtonsoft.Json.Linq.JArray arr && arr.Count >= 3)
        {
            return new Vector3(
                System.Convert.ToSingle(arr[0]),
                System.Convert.ToSingle(arr[1]),
                System.Convert.ToSingle(arr[2]));
        }

        return Vector3.zero;
    }
}
