using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// JsonSkill 可直接使用的主动选择器（ISelectorStrategy）。
/// </summary>
public class 从攻击范围获取单位 : ISelectorStrategy
{
    public string Name => "从攻击范围获取单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null) return new List<Unit>();

        var skill = context.Skill;
        var team = data.GetInt("Team", context.TargetTeam);
        var aliveOnly = !(data.GetBool("DeadFind") || context.DeadFind);

        if (skill != null && skill.AttackPoints != null && skill.AttackPoints.Count > 0)
        {
            return context.Caster.Battle.FindAll(skill.AttackPoints, team, aliveOnly).ToList();
        }

        var range = data.GetFloat("Range", context.BaseAttackRange * context.ExAttackRange);
        return context.Caster.Battle.FindAll(context.Caster.Position2, range, team, aliveOnly).ToList();
    }
}

public class 获取事件目标单位 : ISelectorStrategy
{
    public string Name => "获取事件目标单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null || context.Caster.Battle.TriggerDatas == null || context.Caster.Battle.TriggerDatas.Count == 0)
        {
            return new List<Unit>();
        }

        var target = context.Caster.Battle.TriggerDatas.Peek().Target;
        if (target == null) return new List<Unit>();

        var team = data.GetInt("Team", context.TargetTeam);
        if ((team & (1 << target.Team)) == 0) return new List<Unit>();

        return new List<Unit> { target };
    }
}

public class 获取事件来源单位 : ISelectorStrategy
{
    public string Name => "获取事件来源单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null || context.Caster.Battle.TriggerDatas == null || context.Caster.Battle.TriggerDatas.Count == 0)
        {
            return new List<Unit>();
        }

        var user = context.Caster.Battle.TriggerDatas.Peek().User;
        if (user == null) return new List<Unit>();

        var team = data.GetInt("Team", context.TargetTeam);
        if ((team & (1 << user.Team)) == 0) return new List<Unit>();

        return new List<Unit> { user };
    }
}

public class 获取所有单位 : ISelectorStrategy
{
    public string Name => "获取所有单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null) return new List<Unit>();
        return context.Caster.Battle.AllUnits?.ToList() ?? new List<Unit>();
    }
}
