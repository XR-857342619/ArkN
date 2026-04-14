using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;

public class 获取单位所在地块 : ITargetSelector
{
    public List<Vector3> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config = null)
    {
        return targets.Select(t => new Vector3(t.GridPos.x, 0, t.GridPos.y)).Distinct().ToList();
    }
}

public class 获取地块上的单位 : ITargetSelector
{
    public List<Unit> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config = null)
    {
        Battle Battle = skill.Unit.Battle;
        List<Unit> result = new List<Unit>();
        
        result.AddRange(Battle.FindAll(targets.Select(t => new Vector2Int((int)t.x, (int)t.z)).ToList(), config.TargetTeam, !config.DeadFind));

        return result.Distinct().ToList();
    }
}
    public class 从攻击范围获取单位 : ITargetSelector
{
    //public List<Vector2Int> AttackPoints;
    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config = null)
    {
        Battle Battle = skill.Unit.Battle;
        Unit Unit = skill.Unit;
        List<Unit> result = new List<Unit>();

        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
        {
            result.AddRange(Battle.FindAll(Unit.Position2, skill.SkillData.AttackRange * Unit.AttackRange, config.TargetTeam, !config.DeadFind));
        }
        else
        {
            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
            result.AddRange(Battle.FindAll(attackPoints, skill.SkillData.TargetTeam, !config.DeadFind));
        }

        return result;
    }
}

public class 从攻击范围获取地块 : ITargetSelector
{
    //public List<Vector2Int> AttackPoints;
    public List<Vector3> GetTargetsPos(Skill skill, List<Unit> targets, SelectorConfig config = null)
    {
        Battle Battle = skill.Unit.Battle;
        Unit Unit = skill.Unit;
        List<Vector3> result = new List<Vector3>();

        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
        {
            float attackRange = skill.SkillData.AttackRange * Unit.AttackRange;
            result.AddRange(GetTileFromRange(Unit, attackRange));
        }
        else
        {
            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
            result.AddRange(attackPoints.Select(p => new Vector3(p.x, 0, p.y)).ToList());
        }

        return result;
    }
    public List<Vector3> GetTileFromRange(Unit unit, float range)
    {
        List<Vector3> result = new List<Vector3>();
        for (int x = (int)-range; x <= range; x++)
        {
            for (int z = (int)-range; z <= range; z++)
            {
                if (x * x + z * z <= range * range)
                {
                    Vector3 pos = new Vector3(unit.Position.x + x, unit.Position.y, unit.Position.z + z);
                    result.Add(pos);
                }
            }
        }
        return result;
    }
    //public List<Vector3> GetTileFromAttackPoints(Unit unit, List<Vector2Int> attackPoints)
    //{
        
    //}
}

public class 获取事件目标单位 : ITargetSelector
{
    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Unit t = skill.Unit.Battle.TriggerDatas.Peek().Target;
        if (t != null)
            targets.Add(t);
        return targets;
    }
}

public class 获取事件目标地块 : ITargetSelector
{
    public List<Vector3> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config)
    {
        Unit t = skill.Unit.Battle.TriggerDatas.Peek().Target;
        if (t != null)
            targets.Add(new Vector3(t.GridPos.x, 0, t.GridPos.y));
        return targets;
    }
}

public class 获取事件来源单位 : ITargetSelector
{
    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Unit t = skill.Unit.Battle.TriggerDatas.Peek().User;
        if (t != null)
            targets.Add(t);
        return targets;
    }
}

public class 获取事件来源地块 : ITargetSelector
{
    public List<Vector3> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config)
    {
        Unit t = skill.Unit.Battle.TriggerDatas.Peek().User;
        if (t != null)
            targets.Add(new Vector3(t.GridPos.x, 0, t.GridPos.y));
        return targets;
    }
}

public partial class SelectorConfig
{
    public float HighHpPercent = -1;
    public float LowHpPercent = -1;
    public float HighHp = -1;
    public float LowHp = -1;
}
public class 血量筛选 : ITargetSelector
{

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        float highHpPercent = config.HighHpPercent;
        float lowHpPercent = config.LowHpPercent;
        float highHp = config.HighHp;
        float lowHp = config.LowHp;

        //List<Unit> result = new List<Unit>();
        //result.AddRange(targets);
        for (int i = targets.Count - 1; i >= 0 ; i--)
        {
            float hpPercent = targets[i].Hp / targets[i].MaxHp;
            bool remove = false;

            if (highHpPercent > 0 && hpPercent >= highHpPercent)
                remove = true;
            if (lowHpPercent > 0 && hpPercent <= lowHpPercent)
                remove = true;
            if (highHp >= 0 && targets[i].Hp >= highHp)
                remove = true;
            if (lowHp >= 0 && targets[i].Hp <= lowHp)
                remove = true;
            if (remove) targets.RemoveAt(i);
        }

        return targets;
    }
}

public partial class SelectorConfig
{
    public List<Buff> MustHaveAnyBuffs = new List<Buff>();
    public List<Buff> MustHaveAllBuffs = new List<Buff>();
    public List<Buff> MustNotHaveAnyBuffs = new List<Buff>();
    public List<Buff> MustNotHaveAllBuffs = new List<Buff>();
}
public class Buff筛选 : ITargetSelector
{

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        var mustHaveAny = new HashSet<Buff>(config.MustHaveAnyBuffs);
        var mustHaveAll = new HashSet<Buff>(config.MustHaveAllBuffs);
        var mustNotHaveAny = new HashSet<Buff>(config.MustNotHaveAnyBuffs);
        var mustNotHaveAll = new HashSet<Buff>(config.MustNotHaveAllBuffs);

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            HashSet<Buff> targetBuffs = new HashSet<Buff>(targets[i].Buffs);
            bool remove = false;

            // 必须拥有至少一个
            if (mustHaveAny.Count > 0 && !mustHaveAny.Any(b => targetBuffs.Contains(b)))
                remove = true;

            // 必须拥有所有
            else if (mustHaveAll.Count > 0 && mustHaveAll.Any(b => !targetBuffs.Contains(b)))
                remove = true;

            // 不能拥有任何一个
            else if (mustNotHaveAny.Count > 0 && mustNotHaveAny.Any(b => targetBuffs.Contains(b)))
                remove = true;

            // 不能同时拥有所有（特殊场景）
            else if (mustNotHaveAll.Count > 0 && mustNotHaveAll.All(b => targetBuffs.Contains(b)))
                remove = true;

            if (remove) targets.RemoveAt(i);
        }
        return targets;
    }
}

public partial class SelectorConfig
{
    public SkillTargetFilterEnum FilterEnum;
}
public class 通用选择器 : ITargetSelector
{

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        SkillTargetFilterEnum filterEnum = config.FilterEnum;

        switch (filterEnum)
        {
            case SkillTargetFilterEnum.召唤物:
                targets = targets.Where(t => t != skill.Unit && t.Parent == skill.Unit).ToList();
                break;
            case SkillTargetFilterEnum.自己以外:
                targets.Remove(skill.Unit);
                break;
            case SkillTargetFilterEnum.仅自己:
                targets.Clear();
                targets.Add(skill.Unit);
                break;
            case SkillTargetFilterEnum.仅召唤:
                targets = targets.Where(t => t.Parent == skill.Unit).ToList();
                break;
        }

        return targets;
    }
}

public partial class SelectorConfig
{
    // 距离范围模式
    public float MinRange = -1;  // 最小距离
    public float MaxRange = -1;  // 最大距离

    // 精确距离模式（与范围模式互斥）
    public float ExactDistance = -1;

    // 误差范围（可选，默认0.01）
    public float DistanceEpsilon = 0.01f;

    // 是否启用精确距离模式
    public bool UseExactDistance => ExactDistance >= 0;
}
public class 距离筛选 : ITargetSelector
{
    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Vector3 casterPos = skill.Unit.Position;

        // 精确距离模式
        if (config.UseExactDistance)
        {
            return FilterByExactDistance(targets, casterPos, config.ExactDistance, config.DistanceEpsilon);
        }
        // 距离范围模式
        else
        {
            return FilterByDistanceRange(targets, casterPos, config.MinRange, config.MaxRange);
        }
    }
    private List<Unit> FilterByExactDistance(List<Unit> targets, Vector3 casterPos, float exactDistance, float epsilon)
    {
        float targetDistanceSqr = exactDistance * exactDistance;
        float epsilonSqr = epsilon * epsilon;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            float distanceSqr = (targets[i].Position - casterPos).sqrMagnitude;
            if (Mathf.Abs(distanceSqr - targetDistanceSqr) > epsilonSqr)
            {
                targets.RemoveAt(i);
            }
        }
        return targets;
    }

    private List<Unit> FilterByDistanceRange(List<Unit> targets, Vector3 casterPos, float minRange, float maxRange)
    {
        float minDistanceSqr = minRange >= 0 ? minRange * minRange : -1;
        float maxDistanceSqr = maxRange >= 0 ? maxRange * maxRange : -1;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            float distanceSqr = (targets[i].Position - casterPos).sqrMagnitude;

            bool remove = false;

            // 检查最大距离
            if (maxDistanceSqr >= 0 && distanceSqr > maxDistanceSqr)
                remove = true;

            // 检查最小距离
            if (!remove && minDistanceSqr >= 0 && distanceSqr < minDistanceSqr)
                remove = true;

            if (remove) targets.RemoveAt(i);
        }
        return targets;
    }
}

public partial class SelectorConfig
{
    /// <summary>
    /// 仅当输入目标列表为空时才执行随机选择
    /// true: 用于上一个TargetSelector失败时的后备方案
    /// false: 总是尝试从输入目标中随机选择
    /// </summary>
    public bool NeedEmptyTargets = false;

    /// <summary>
    /// 要获取的随机单位数量
    /// </summary>
    public int RandomCount = 1;
}
public class 获取随机单位 : ITargetSelector
{

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        if (skill == null || skill.Unit?.Battle == null)
            throw new ArgumentException("无效的技能或战斗上下文");

        var random = skill.Unit.Battle.Random;

        // 验证配置
        if (config.RandomCount <= 0)
            return new List<Unit>();

        // 处理 null 输入
        targets ??= new List<Unit>();

        // 模式1: NeedEmptyTargets = true (只在输入为空时执行)
        if (config.NeedEmptyTargets)
        {
            // 输入有目标：直接返回，不做任何更改
            if (targets.Count > 0)
                return new List<Unit>(targets);

            // 输入为空：需要生成随机目标
            return GetRandomFallbackTargets(skill, config.RandomCount, config, random);
        }

        // 模式2: NeedEmptyTargets = false (总是尝试随机选择)
        // 情况1: 没有可用目标
        if (targets.Count == 0)
            return new List<Unit>();

        // 情况2: 可用目标数量 ≤ 配置数量，直接返回原列表
        if (targets.Count <= config.RandomCount)
            return new List<Unit>(targets);

        // 情况3: 可用目标数量 > 配置数量，随机选择
        return GetRandomTargets(targets, config.RandomCount, random);
    }

    /// <summary>
    /// 从现有目标中随机选择指定数量的单位
    /// </summary>
    private List<Unit> GetRandomTargets(List<Unit> targets, int count, System.Random random)
    {
        var result = new List<Unit>(count);
        var availableIndices = new List<int>(targets.Count);

        // 初始化所有可用索引
        for (int i = 0; i < targets.Count; i++)
            availableIndices.Add(i);

        // 随机选择 count 个索引
        for (int i = 0; i < count; i++)
        {
            int randomIndex = random.Next(availableIndices.Count);
            int selectedIndex = availableIndices[randomIndex];

            result.Add(targets[selectedIndex]);

            // 从可用索引中移除已选中的
            availableIndices.RemoveAt(randomIndex);
        }

        return result;
    }

    /// <summary>
    /// 当输入为空时，从场景中获取随机目标（后备方案）
    /// </summary>
    private List<Unit> GetRandomFallbackTargets(Skill skill, int count, SelectorConfig config, System.Random random)
    {
        // 通常从场景中获取所有合法目标
        // 这里假设获取所有敌方单位（具体逻辑需要根据业务调整）
        var allValidTargets = skill.Unit.Battle.AllUnits.Where(u => u != skill.Unit && u.IfAlive == config.DeadFind).ToList();

        // 没有可用目标
        if (allValidTargets.Count == 0)
            return new List<Unit>();

        // 可用目标数量 ≤ 需求数量，直接返回所有
        if (allValidTargets.Count <= count)
            return new List<Unit>(allValidTargets);

        // 随机选择指定数量
        return GetRandomTargets(allValidTargets, count, random);
    }
}