using UnityEngine;

/// <summary>
/// 属性修改效果器。
/// 优先使用 ModifyId 走现有 Modify 系统；也可用 BuffId 走 Buff；最后支持直接改 Unit 字段（仅临时，不推荐）。
/// Data: ModifyId, BuffId, Attribute, Value, Duration
/// </summary>
public class AttributeModifyEffect : ISkillEffect
{
    public string Name => "属性修改";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;

        if (data.ContainsKey("ModifyId"))
        {
            var modifyId = EffectUtil.ToConfigId<ModifyData>(data["ModifyId"]);
            if (modifyId > 0)
            {
                var modify = ModifyManager.Instance.Get(modifyId, context.Skill);
                if (modify is IUnitModify unitModify)
                {
                    foreach (var target in EffectUtil.GetTargets(context))
                    {
                        if (target == null) continue;
                        unitModify.Modify(target);
                        if (context.Skill != null && !context.Skill.Modifies.Contains(modify))
                        {
                            context.Skill.Modifies.Add(modify);
                        }
                    }
                }
                return;
            }
        }

        if (data.ContainsKey("BuffId"))
        {
            var buffId = EffectUtil.ToConfigId<BuffData>(data["BuffId"]);
            var duration = data.GetFloat("Duration", -1f);
            foreach (var target in EffectUtil.GetTargets(context))
            {
                if (target == null) continue;
                target.AddBuff(buffId, context.Skill, data.GetInt("Index", 0), duration);
            }
            return;
        }

        var attribute = data.GetStr("Attribute");
        if (!string.IsNullOrEmpty(attribute) && data.ContainsKey("Value"))
        {
            var value = data.GetFloat("Value", 0f);
            foreach (var target in EffectUtil.GetTargets(context))
            {
                if (target == null) continue;
                var field = target.GetType().GetField(attribute);
                if (field != null)
                {
                    field.SetValue(target, value);
                    Debug.LogWarning($"AttributeModifyEffect 直接修改 {target.UnitData.Id}.{attribute}={value}，Refresh 后可能被覆盖；建议改用 ModifyId/BuffId");
                }
            }
        }
    }
}
