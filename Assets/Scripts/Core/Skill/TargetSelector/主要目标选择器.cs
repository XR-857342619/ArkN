using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public class 从攻击范围中获取 : ITargetSelector
{
    //public List<Vector2Int> AttackPoints;
    public List<T> GetTargets<T>(Skill skill, List<T> targets)
    {
        Battle Battle = skill.Unit.Battle;
        Unit Unit = skill.Unit;
        List<Unit> result = new List<Unit>();

        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
        {
            result.AddRange(Battle.FindAll(Unit.Position2, skill.SkillData.AttackRange * Unit.AttackRange, skill.SkillData.TargetTeam, !skill.SkillData.DeadFind));
        }
        else
        {
            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
            result.AddRange(Battle.FindAll(attackPoints, skill.SkillData.TargetTeam, !skill.SkillData.DeadFind));
        }

        return targets;
    }
}
