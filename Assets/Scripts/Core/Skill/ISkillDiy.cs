using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AttributeEnum
{
    Hp,
    MaxHp,
    Attack,
    AttackBase,
    Defence,
    DefenceBase,
    Agi,
    AgiBase,
    AttackGap,
    AttackGapBase,
    MagicDefence,
    MagicDefenceBase,
    Speed,
    SpeedBase,
    AllBlock,
    Block,
    MagBlock,
    PowerSpeed,
    PowerSpeedAdd,
    HpRecoverP,
    HpRecover,
    ElementBreakRecoverRate,
    Weight,
    Resist,
    AttackRange,
    StopCount,
    IfHide,
    CanAttack,
    CanStopOther,
    IfAlive,
    IfSleep,
    IfSelectable,
    CanBeHeal,
    // 可以根据需要添加更多属性
}
//public interface ITargetSelector
//{
//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
//    {
//        return targets;
//    }
//    public List<Unit> GetTargets(Skill skill, List<Tile> targets, SelectorConfig config)
//    {
//        return new List<Unit>();
//    }
//    public List<Vector3> GetTargetsPos(Skill skill, List<Vector3> targets, SelectorConfig config)
//    {
//        return targets;
//    }
//    public List<Vector3> GetTargetsPos(Skill skill, List<Unit> targets, SelectorConfig config)
//    {
//        return new List<Vector3>();
//    }
//}

public interface IFilterStrategy
{
    string Name { get; }
    Func<Unit, bool> GetPredicate();
}

public enum SortDirection
{
    Ascending,  // 升序
    Descending  // 降序
}

public interface ISortStrategy
{
    string Name { get; }
    // 返回一个 Func，用于提取排序键（例如 u => u.Distance）
    Func<Unit, IComparable> GetKeySelector();
}

public class ExecutionResult
{
    protected TriggerEnum Event;
    protected int Effect;
}

public interface IExecutor
{
    public partial class ExecutorConfig
    {
    
    }
    public ExecutionResult Execute<T>(Skill skill, List<T> targets)
    {
        return new ExecutionResult();
    }
}

public class SkillContext
{
    public Unit Caster { get; set; }
    public List<Unit> Targets { get; set; }
    public int TargetTeam { get; set; }
    public bool DeadFind { get; set; }
    public List<Vector2Int> BaseAttackPoints { get; set; }
    public List<Vector2Int> ExAttackPoints { get; set; }
    public float BaseAttackRange { get; set; }
    public float ExAttackRange { get; set; }
    public SkillTargetFilterEnum targetFilterEnum { get; set; }
    // 可以根据需要添加更多上下文信息

    public SkillContext(Skill skill)
    {
        var exAttackPoints = skill.ExAttackPoints.ToHashSet();

        this.Caster = skill.Unit;
        this.Targets = skill.Targets;
        this.TargetTeam = skill.SkillData.TargetTeam;
        this.DeadFind = skill.SkillData.DeadFind;
        this.BaseAttackPoints = skill.AttackPoints;
        this.BaseAttackPoints.RemoveAll(v => exAttackPoints.Contains(v));
        this.ExAttackPoints = skill.ExAttackPoints;
        this.BaseAttackRange = skill.Unit.AttackRange;
        this.ExAttackRange = skill.SkillData.AttackRange;
    }
}