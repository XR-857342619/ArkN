using System;

/// <summary>
/// 触发全局事件效果器。
/// Data: Event (TriggerEnum 字符串，支持中文)
/// </summary>
public class TriggerEventEffect : ISkillEffect
{
    public string Name => "结算事件";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster?.Battle == null || node?.Data == null) return;

        var eventStr = node.Data.GetStr("Event");
        if (string.IsNullOrEmpty(eventStr)) return;

        if (TryParseTriggerEnum(eventStr, out var trigger))
        {
            context.Caster.Battle.Trigger(trigger);
        }
    }

    private bool TryParseTriggerEnum(string str, out TriggerEnum result)
    {
        result = TriggerEnum.无;
        if (string.IsNullOrEmpty(str)) return false;

        if (Enum.TryParse(str, true, out result))
        {
            return true;
        }

        switch (str)
        {
            case "起始": result = TriggerEnum.起始; return true;
            case "出场": result = TriggerEnum.出场; return true;
            case "入场": result = TriggerEnum.入场; return true;
            case "落地": result = TriggerEnum.落地; return true;
            case "离场": result = TriggerEnum.离场; return true;
            case "攻击": result = TriggerEnum.攻击; return true;
            case "被击": result = TriggerEnum.被击; return true;
            case "治疗": result = TriggerEnum.治疗; return true;
            case "被治疗": result = TriggerEnum.被治疗; return true;
            case "击杀": result = TriggerEnum.击杀; return true;
            case "死亡": result = TriggerEnum.死亡; return true;
            case "释放技能": result = TriggerEnum.释放技能; return true;
            case "技能结束": result = TriggerEnum.技能结束; return true;
            case "击中": result = TriggerEnum.击中; return true;
        }

        return false;
    }
}
