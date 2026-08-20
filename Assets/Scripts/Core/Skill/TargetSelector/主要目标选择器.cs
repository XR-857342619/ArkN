using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

#region 弃用的选择器
//public class 获取单位所在地块 : ITargetSelector
//{
//    public List<Vector3> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config = null)
//    {
//        return targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).Select(t => new Vector3(t.GridPos.x, 0, t.GridPos.y)).Distinct().ToList();
//    }
//}
//public class 获取单位所在地块 : IFilterStrategy
//{
//    public string Name => "获取单位所在地块";
//    public int _targetTeam;
//    public 获取单位所在地块(int targetTeam)
//    {
//        _targetTeam = targetTeam;
//    }
//    public Func<Unit, bool> GetPredicate() => (unit) =>
//    {
//        if (!unit.Abnormal) return false;
//        return (_targetTeam >> unit.Team) % 2 == 1 && unit.Position.x % 1 == 0 && unit.Position.z % 1 == 0;
//    };
//}

//public class 获取地块上的单位 : ITargetSelector
//{
//    public List<Unit> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config = null)
//    {
//        Battle Battle = skill.Unit.Battle;
//        List<Unit> result = new List<Unit>();

//        result.AddRange(Battle.FindAll(targets.Select(t => new Vector2Int((int)t.x, (int)t.z)).ToList(), config.TargetTeam, !config.DeadFind));

//        return result.Distinct().ToList();
//    }
//}
//    public class 从攻击范围获取单位 : ITargetSelector
//{
//    //public List<Vector2Int> AttackPoints;
//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config = null)
//    {
//        Battle Battle = skill.Unit.Battle;
//        Unit Unit = skill.Unit;
//        List<Unit> result = new List<Unit>();

//        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
//        {
//            result.AddRange(Battle.FindAll(Unit.Position2, skill.SkillData.AttackRange * Unit.AttackRange, config.TargetTeam, !config.DeadFind));
//        }
//        else
//        {
//            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
//            result.AddRange(Battle.FindAll(attackPoints, skill.SkillData.TargetTeam, !config.DeadFind));
//        }

//        return result;
//    }
//}
//public class 从攻击范围获取单位 : IFilterStrategy
//{
//    //public List<Vector2Int> AttackPoints;
//    public string Name => "从攻击范围获取单位";
//    public int _targetTeam;
//    public 从攻击范围获取单位(int targetTeam)
//    {
//        _targetTeam = targetTeam;
//    }
//    public Func<Unit, bool> GetPredicate() => (unit) =>
//    {
//        return false;
//    };
//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config = null)
//    {
//        Battle Battle = skill.Unit.Battle;
//        Unit Unit = skill.Unit;
//        List<Unit> result = new List<Unit>();

//        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
//        {
//            result.AddRange(Battle.FindAll(Unit.Position2, skill.SkillData.AttackRange * Unit.AttackRange, config.TargetTeam, !config.DeadFind));
//        }
//        else
//        {
//            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
//            result.AddRange(Battle.FindAll(attackPoints, skill.SkillData.TargetTeam, !config.DeadFind));
//        }

//        return result;
//    }
//}

//public class 从攻击范围获取地块 : ITargetSelector
//{
//    //public List<Vector2Int> AttackPoints;
//    public List<Vector3> GetTargetsPos(Skill skill, List<Unit> targets, SelectorConfig config = null)
//    {
//        Battle Battle = skill.Unit.Battle;
//        Unit Unit = skill.Unit;
//        List<Vector3> result = new List<Vector3>();

//        if (skill.AttackPoints == null && !skill.SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
//        {
//            float attackRange = skill.SkillData.AttackRange * Unit.AttackRange;
//            result.AddRange(GetTileFromRange(Unit, attackRange));
//        }
//        else
//        {
//            var attackPoints = skill.SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : skill.AttackPoints;
//            result.AddRange(attackPoints.Select(p => new Vector3(p.x, 0, p.y)).ToList());
//        }

//        return result;
//    }
//    public List<Vector3> GetTileFromRange(Unit unit, float range)
//    {
//        List<Vector3> result = new List<Vector3>();
//        for (int x = (int)-range; x <= range; x++)
//        {
//            for (int z = (int)-range; z <= range; z++)
//            {
//                if (x * x + z * z <= range * range)
//                {
//                    Vector3 pos = new Vector3(unit.Position.x + x, unit.Position.y, unit.Position.z + z);
//                    result.Add(pos);
//                }
//            }
//        }
//        return result;
//    }
//    //public List<Vector3> GetTileFromAttackPoints(Unit unit, List<Vector2Int> attackPoints)
//    //{

//    //}
//}

//public class 获取事件目标单位 : ITargetSelector
//{
//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
//    {
//        Unit t = skill.Unit.Battle.TriggerDatas.Peek().Target;
//        if (t != null && (skill.SkillData.TargetTeam >> t.Team) % 2 == 1)
//            targets.Add(t);
//        return targets;
//    }
//}

//public class 获取事件目标地块 : ITargetSelector
//{
//    public List<Vector3> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config)
//    {
//        Unit t = skill.Unit.Battle.TriggerDatas.Peek().Target;
//        if (t != null && (skill.SkillData.TargetTeam >> t.Team) % 2 == 1)
//            targets.Add(new Vector3(t.GridPos.x, 0, t.GridPos.y));
//        return targets;
//    }
//}

//public class 获取事件来源单位 : ITargetSelector
//{
//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
//    {
//        Unit t = skill.Unit.Battle.TriggerDatas.Peek().User;
//        if (t != null && (skill.SkillData.TargetTeam >> t.Team) % 2 == 1)
//            targets.Add(t);
//        return targets;
//    }
//}

//public class 获取事件来源地块 : ITargetSelector
//{
//    public List<Vector3> GetTargets(Skill skill, List<Vector3> targets, SelectorConfig config)
//    {
//        Unit t = skill.Unit.Battle.TriggerDatas.Peek().User;
//        if (t != null && (skill.SkillData.TargetTeam >> t.Team) % 2 == 1)
//            targets.Add(new Vector3(t.GridPos.x, 0, t.GridPos.y));
//        return targets;
//    }
//}

//public partial class SelectorConfig
//{
//    public TargetAttributeEnum Attribute1;
//    public TargetAttributeEnum Attribute2;

//    public float HighAttributePercent = -1;
//    public float LowAttributePercent = -1;
//    public float HighAttribute = -1;
//    public float LowAttribute = -1;
//}
//public class 以属性筛选单位 : ITargetSelector
//{

//    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
//    {
//        float highAttributePercent = config.HighAttributePercent;
//        float lowAttributePercent = config.LowAttributePercent;
//        float highAttribute = config.HighAttribute;
//        float lowAttribute = config.LowAttribute;

//        //List<Unit> result = new List<Unit>();
//        //result.AddRange(targets);
//        for (int i = targets.Count - 1; i >= 0 ; i--)
//        {
//            float hpPercent = targets[i].Hp / targets[i].MaxHp;
//            bool remove = false;

//            if (highAttributePercent > 0 && hpPercent >= highAttributePercent)
//                remove = true;
//            if (lowAttributePercent > 0 && hpPercent <= lowAttributePercent)
//                remove = true;
//            if (highAttribute >= 0 && targets[i].Hp >= highAttribute)
//                remove = true;
//            if (lowAttribute >= 0 && targets[i].Hp <= lowAttribute)
//                remove = true;
//            if (remove) targets.RemoveAt(i);
//        }

//        return targets;
//    }

//    protected float GetAttribute(Unit unit, TargetAttributeEnum attribute)
//    {
//        switch (attribute)
//        {
//            case TargetAttributeEnum.Hp:
//                return unit.Hp;
//            case TargetAttributeEnum.MaxHp:
//                return unit.MaxHp;
//            case TargetAttributeEnum.Attack:
//                return unit.Attack;
//            case TargetAttributeEnum.AttackBase:
//                return unit.AttackBase;
//            case TargetAttributeEnum.Defence:
//                return unit.Defence;
//            case TargetAttributeEnum.DefenceBase:
//                return unit.DefenceBase;
//            case TargetAttributeEnum.Agi:
//                return unit.Agi;
//            case TargetAttributeEnum.AgiBase:
//                return unit.AgiBase;
//            case TargetAttributeEnum.AttackGap:
//                return unit.AttackGap;
//            case TargetAttributeEnum.AttackGapBase:
//                return unit.AttackGapBase;
//            default:
//                return 0;
//        }
//    }
//}
# endregion

//public partial class SelectorConfig
//{
//    public List<Buff> MustHaveAnyBuffs = new List<Buff>();
//    public List<Buff> MustHaveAllBuffs = new List<Buff>();
//    public List<Buff> MustNotHaveAnyBuffs = new List<Buff>();
//    public List<Buff> MustNotHaveAllBuffs = new List<Buff>();
//}
public class Buff筛选 : IFilterStrategy
{
    public string Name => "Buff筛选";
    
    private readonly int _targetTeam;
    private readonly HashSet<int> _mustHaveAnyBuffIds;
    private readonly HashSet<int> _mustHaveAllBuffIds;
    private readonly HashSet<int> _mustNotHaveAnyBuffIds;
    private readonly HashSet<int> _mustNotHaveAllBuffIds;

    // 预计算的条件检查标志
    private readonly bool _deadFind;
    private readonly bool _hasMustHaveAny;
    private readonly bool _hasMustHaveAll;
    private readonly bool _hasMustNotHaveAny;
    private readonly bool _hasMustNotHaveAll;

    public Buff筛选(SkillContext skillContext, int[] mustHaveAnyBuffIds, int[] mustHaveAllBuffIds, int[] mustNotHaveAnyBuffIds, int[] mustNotHaveAllBuffIds)
    {
        _targetTeam = skillContext?.TargetTeam ?? 0;
        _deadFind = skillContext?.DeadFind ?? false;
        // 使用HashSet提高查询性能
        _mustHaveAnyBuffIds = new HashSet<int>(mustHaveAnyBuffIds ?? Array.Empty<int>());
        _mustHaveAllBuffIds = new HashSet<int>(mustHaveAllBuffIds ?? Array.Empty<int>());
        _mustNotHaveAnyBuffIds = new HashSet<int>(mustNotHaveAnyBuffIds ?? Array.Empty<int>());
        _mustNotHaveAllBuffIds = new HashSet<int>(mustNotHaveAllBuffIds ?? Array.Empty<int>());

        // 预计算条件标志，避免每次检查数组长度
        _hasMustHaveAny = _mustHaveAnyBuffIds.Count > 0;
        _hasMustHaveAll = _mustHaveAllBuffIds.Count > 0;
        _hasMustNotHaveAny = _mustNotHaveAnyBuffIds.Count > 0;
        _hasMustNotHaveAll = _mustNotHaveAllBuffIds.Count > 0;
    }

    public Func<Unit, bool> GetPredicate() => (unit) =>
    {
        if (!unit.IfAlive && !_deadFind) return false;
        // 缓存buff ID 集合，避免每次创建新的HashSet
        var buffIdCache = new Dictionary<Unit, HashSet<int>>();

        if (!unit.Abnormal) return false;

        // 2. 获取或创建buff ID缓存
        if (!buffIdCache.TryGetValue(unit, out var unitBuffIds))
        {
            unitBuffIds = new HashSet<int>(unit.Buffs.Select(b => b.Id));
            buffIdCache[unit] = unitBuffIds;
        }

        if ((_targetTeam >> unit.Team) % 2 == 0) return false;

        // 3. 必须拥有至少一个buff
        if (_hasMustHaveAny && !_mustHaveAnyBuffIds.Any(id => unitBuffIds.Contains(id)))
            return false;

        // 4. 必须拥有所有buff
        if (_hasMustHaveAll && !_mustHaveAllBuffIds.All(id => unitBuffIds.Contains(id)))
            return false;

        // 5. 不能拥有任何一个buff
        if (_hasMustNotHaveAny && _mustNotHaveAnyBuffIds.Any(id => unitBuffIds.Contains(id)))
            return false;

        // 6. 不能同时拥有所有buff
        if (_hasMustNotHaveAll && _mustNotHaveAllBuffIds.All(id => unitBuffIds.Contains(id)))
            return false;

        return true;
    };
}

//public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
//{
//    var mustHaveAny = new HashSet<Buff>(config.MustHaveAnyBuffs);
//    var mustHaveAll = new HashSet<Buff>(config.MustHaveAllBuffs);
//    var mustNotHaveAny = new HashSet<Buff>(config.MustNotHaveAnyBuffs);
//    var mustNotHaveAll = new HashSet<Buff>(config.MustNotHaveAllBuffs);

//    targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

//    for (int i = targets.Count - 1; i >= 0; i--)
//    {
//        HashSet<Buff> targetBuffs = new HashSet<Buff>(targets[i].Buffs);
//        bool remove = false;

//        // 必须拥有至少一个
//        if (mustHaveAny.Count > 0 && !mustHaveAny.Any(b => targetBuffs.Contains(b)))
//            remove = true;

//        // 必须拥有所有
//        else if (mustHaveAll.Count > 0 && mustHaveAll.Any(b => !targetBuffs.Contains(b)))
//            remove = true;

//        // 不能拥有任何一个
//        else if (mustNotHaveAny.Count > 0 && mustNotHaveAny.Any(b => targetBuffs.Contains(b)))
//            remove = true;

//        // 不能同时拥有所有（特殊场景）
//        else if (mustNotHaveAll.Count > 0 && mustNotHaveAll.All(b => targetBuffs.Contains(b)))
//            remove = true;

//        if (remove) targets.RemoveAt(i);
//    }
//    return targets;
//}

public partial class SelectorConfig
{
    public SkillTargetFilterEnum FilterEnum;
}
public class 通用选择器
{

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        SkillTargetFilterEnum filterEnum = config.FilterEnum;
        
        targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

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

public class 常用筛选器 : IFilterStrategy
{
    public string Name => "常用筛选器";

    private readonly Unit _caster;
    private readonly int _targetTeam;
    private readonly SkillTargetFilterEnum _filterEnum;

    public 常用筛选器(int targetTeam, int filterEnum) : this(null, targetTeam, filterEnum)
    {
    }

    public 常用筛选器(SkillContext skillContext, int targetTeam, int filterEnum)
    {
        _caster = skillContext?.Caster;
        _targetTeam = targetTeam;
        _filterEnum = (SkillTargetFilterEnum)filterEnum;
    }

    public Func<Unit, bool> GetPredicate() => (unit) =>
    {
        if (unit == null) return false;

        if ((_targetTeam >> unit.Team) % 2 == 0) return false;
        if (_caster == null) return true;

        switch (_filterEnum)
        {
            case SkillTargetFilterEnum.召唤物:
                return unit != _caster && unit.Parent == _caster;
            case SkillTargetFilterEnum.自己以外:
                return unit != _caster;
            case SkillTargetFilterEnum.仅自己:
                return unit == _caster;
            case SkillTargetFilterEnum.仅召唤:
                return unit.Parent == _caster;
            default:
                return true;
        }
    };
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
public class 距离筛选 : IFilterStrategy
{
    public string Name => "距离筛选";

    private readonly SkillContext _skillContext;
    private readonly float _minRange;
    private readonly float _maxRange;
    private readonly float _exactDistance;
    private readonly float _distanceEpsilon;

    public 距离筛选(SkillContext skillContext, float minDistance = -1, float maxDistance = -1, float exactDistance = -1, float distanceEpsilon = 0.01f)
    {
        _skillContext = skillContext;
        _minRange = minDistance;
        _maxRange = maxDistance;
        _exactDistance = exactDistance;
        _distanceEpsilon = distanceEpsilon;
    }

    public Func<Unit, bool> GetPredicate()
    {
        return unit =>
        {
            if (unit == null || _skillContext?.Caster == null) return false;
            var casterPos = _skillContext.Caster.Position;
            var distanceSqr = (unit.Position - casterPos).sqrMagnitude;

            if (_exactDistance >= 0)
            {
                var targetSqr = _exactDistance * _exactDistance;
                var epsilonSqr = _distanceEpsilon * _distanceEpsilon;
                return Mathf.Abs(distanceSqr - targetSqr) <= epsilonSqr;
            }

            if (_maxRange >= 0 && distanceSqr > _maxRange * _maxRange) return false;
            if (_minRange >= 0 && distanceSqr < _minRange * _minRange) return false;
            return true;
        };
    }

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Vector3 casterPos = skill.Unit.Position;

        targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

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
public class 获取随机单位 : ISelectorStrategy
{
    public string Name => "获取随机单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null) return new List<Unit>();

        var count = data.GetInt("Count", data.GetInt("RandomCount", 1));
        if (count <= 0) return new List<Unit>();

        var team = data.GetInt("Team", context.TargetTeam);
        var candidates = context.Caster.Battle.AllUnits?.Where(u => (team & (1 << u.Team)) != 0).ToList() ?? new List<Unit>();
        if (candidates.Count <= count) return candidates;

        var random = context.Caster.Battle.Random;
        var pool = new List<Unit>(candidates);
        var result = new List<Unit>(count);
        while (result.Count < count && pool.Count > 0)
        {
            var index = random.Next(pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        if (skill == null || skill.Unit?.Battle == null)
            throw new ArgumentException("无效的技能或战斗上下文");

        var random = skill.Unit.Battle.Random;

        // 验证配置
        if (config.RandomCount <= 0)
            return new List<Unit>();

        targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

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
        //var allValidTargets = skill.Unit.Battle.AllUnits.Where(u => u != skill.Unit && u.IfAlive == config.DeadFind).ToList();
        var allValidTargets = skill.Unit.Battle.AllUnits.Where(u => u != skill.Unit).ToList();

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

public class 获取自身阻挡单位 : ISelectorStrategy
{
    public string Name => "获取自身阻挡单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster == null) return new List<Unit>();

        var unit = context.Caster;
        var targets = new List<Unit>();
        if (unit is Units.干员) targets.AddRange(unit.StopUnits);
        else if (unit is Units.敌人 enemy) targets.Add(enemy.StopUnit);

        var team = data.GetInt("Team", context.TargetTeam);
        return targets.Where(t => (team & (1 << t.Team)) != 0).ToList();
    }

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Unit Unit = skill.Unit;

        if (Unit is Units.干员)
            targets.AddRange(Unit.StopUnits);
        else if (Unit is Units.敌人 enemy)
            targets.Add(enemy.StopUnit);
        
        targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

        return targets;
    }
}

public class 获取被阻挡的单位 : ISelectorStrategy
{
    public string Name => "获取被阻挡的单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster?.Battle == null) return new List<Unit>();

        var team = data.GetInt("Team", context.TargetTeam);
        var all = context.Caster.Battle.AllUnits ?? new List<Unit>();
        return all.Where(t =>
                t is Units.干员 && t.StopUnits.Count > 0 ||
                t is Units.敌人 enemy && enemy.StopUnit is not null)
            .Where(t => (team & (1 << t.Team)) != 0)
            .Distinct()
            .ToList();
    }

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        if (targets.Count == 0)
            targets = skill.Unit.Battle.AllUnits;

        targets = targets.Where(t => (skill.SkillData.TargetTeam >> t.Team) % 2 == 1).ToList();

        targets = targets.Where(
            t => t is Units.干员 && t.StopUnits.Count > 0 ||
                 t is Units.敌人 enemy && enemy.StopUnit is not null)
            .Distinct()
            .ToList();

        return targets;
    }
}

public partial class SelectorConfig
{
    public Unit BlockerUnit;
}
public class 获取被指定单位阻挡的单位 : ISelectorStrategy
{
    public string Name => "获取被指定单位阻挡的单位";

    public List<Unit> GetTargets(SkillContext context, Dictionary<string, object> data)
    {
        if (context?.Caster == null) return new List<Unit>();

        var team = data.GetInt("Team", context.TargetTeam);
        Unit blockerUnit = context.Caster;

        if (blockerUnit is Units.敌人 enemy)
        {
            if (enemy.StopUnit == null) return new List<Unit>();
            return (team & (1 << enemy.StopUnit.Team)) != 0 ? new List<Unit> { enemy.StopUnit } : new List<Unit>();
        }

        return blockerUnit.StopUnits
            .Where(t => (team & (1 << t.Team)) != 0)
            .Select(t => (Unit)t)
            .ToList();
    }

    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        Unit blockerUnit = config.BlockerUnit;

        if (blockerUnit == null)
            return targets;

        targets.AddRange(blockerUnit is Units.敌人 enemy ? new List<Unit>() { enemy.StopUnit } : blockerUnit.StopUnits);

        return targets;
    }
}