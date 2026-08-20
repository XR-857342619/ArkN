using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于 SkillJsonData 的 JSON 技能宿主。
/// 它复用 Skill 基类的 SP/冷却/连发/蓄力等通用机制，具体行为由 Selectors/Sorters/Effects 组合完成。
/// </summary>
public class JsonSkill : Skill
{
    private SkillJsonData _jsonData;
    private EffectDispatcher _dispatcher = new EffectDispatcher();

    public SkillJsonData JsonData => _jsonData;
    public EffectDispatcher Dispatcher => _dispatcher;

    public override void Init()
    {
        var skillData = SkillData;
        if (skillData == null)
        {
            Debug.LogError($"JsonSkill 初始化失败：未找到 SkillData 占位配置 Id={Id}");
            return;
        }

        _jsonData = Database.Instance.Get<SkillJsonData>(skillData.Id);
        if (_jsonData == null)
        {
            // 兼容 SkillJsonData 文件行序与 SkillData 一致的情况
            _jsonData = Database.Instance.Get<SkillJsonData>(Id);
        }

        if (_jsonData == null)
        {
            Debug.LogError($"JsonSkill 初始化失败：未找到 SkillJsonData，SkillData.Id={skillData.Id}, SkillIndex={Id}");
            return;
        }

        ApplyBaseConfig();
        base.Init();

        _dispatcher.Build(_jsonData.Effects);
        Dispatch(SkillEffectTrigger.OnInit, CreateContext());

        var errors = SkillJsonValidator.Validate(_jsonData);
        if (errors.Count > 0)
        {
            Debug.LogWarning(SkillJsonValidator.ValidateToString(_jsonData));
        }
    }

    private void ApplyBaseConfig()
    {
        var skillData = SkillData;
        var b = _jsonData.Base ?? new SkillBaseConfig();

        // 将 JSON 基础字段写回占位 SkillData，使 Skill 基类的通用机制无需大改即可工作。
        skillData.SkillCost = b.SkillCost;
        skillData.MaxPower = b.MaxPower;
        skillData.StartPower = b.StartPower;
        skillData.PowerCount = b.PowerCount;
        skillData.PowerType = b.PowerType;
        skillData.PowerUseType = b.PowerUseType;
        skillData.UseType = b.UseType;
        skillData.ReadyType = JsonConfigHelper.ParseReadyType(b.ReadyType);
        skillData.Cooldown = b.Cooldown;
        skillData.OpenTime = b.OpenTime;
        skillData.BurstCount = b.BurstCount;
        skillData.BurstDelay = b.BurstDelay;
        skillData.BurstFind = b.BurstFind;
        skillData.TargetTeam = b.TargetTeam;
        skillData.DeadFind = b.DeadFind;
        skillData.AttackRange = b.AttackRange;
        skillData.AttackPoints = b.AttackPoints?.ToArray();
        skillData.AttackAreaWithMain = b.AttackAreaWithMain;
        skillData.ModelAnimation = b.ModelAnimation;
        skillData.OverwriteAnimation = b.OverwriteAnimation;
        skillData.ShootPoint = b.ShootPoint;
        skillData.AutoUse = b.AutoUse;
        skillData.NoTargetAlsoUse = b.NoTargetAlsoUse;
        skillData.RegetTarget = b.RegetTarget;
        skillData.StopBreak = b.StopBreak;
        skillData.CanStop = b.CanStop;
        skillData.MaxUseCount = b.MaxUseCount;
        skillData.AnimationTime = b.AnimationTime;
        skillData.AttackMode = b.AttackMode;

        // 清理旧占位配置里的表现字段，避免 JsonSkill 意外播放旧技能特效。
        skillData.Trigger = TriggerEnum.无;
        skillData.ModelAnimationDown = null;
        skillData.OverwriteAnimationDown = null;
        skillData.StartEffect = null;
        skillData.CastEffect = null;
        skillData.HitEffect = null;
        skillData.ReadyEffect = null;
        skillData.LoopStartEffect = null;
        skillData.LoopCastEffect = null;
        skillData.GatherEffect = null;
        skillData.EffectEffect = null;

        // 关闭旧 Skill 子类默认行为，避免 JsonSkill 产生重复伤害/效果。
        skillData.SkillCondition = null;
        skillData.OrderExpression = null;
        skillData.TargetFilter = SkillTargetFilterEnum.无;
        skillData.ProfessionLimit = UnitTypeEnum.无;
        skillData.AttackOrder = AttackTargetOrderEnum.无;
        skillData.AttackOrder2 = AttackTargetOrder2Enum.无;
        skillData.DamageRate = 0;
        skillData.DamageBase = 0;
        skillData.Bullet = null;
        skillData.Buffs = null;
        skillData.BuffRemoves = null;
        skillData.ExSkills = null;
        skillData.ExSkillWeight = null;
        skillData.Skills = null;
        skillData.UpgradeSkill = null;
        skillData.Modifys = null;
    }

    protected override void OnSkillOpen()
    {
        base.OnSkillOpen();
        Dispatch(SkillEffectTrigger.OnStart, CreateContext());
    }

    public override void FindTarget()
    {
        Targets.Clear();
        Targets.AddRange(GetJsonTargets());
    }

    public override List<Unit> GetAttackTarget()
    {
        return GetJsonTargets();
    }

    public List<Unit> GetJsonTargets()
    {
        if (_jsonData == null) return new List<Unit>();

        var context = CreateContext();
        var selector = new DynamicTargetSelector();
        return selector.SelectTargets(context, _jsonData.Selectors, _jsonData.Sorters);
    }

    public override void Cast()
    {
        if (SkillData.ReadyType == SkillReadyEnum.充能释放)
        {
            Power -= MaxPower;
        }

        if (SkillData.PowerUseType == PowerRecoverTypeEnum.攻击)
        {
            UpdateOpening(1);
            if (Unit.MainSkill != null && Unit.MainSkill != this && !Unit.MainSkill.Opening.Finished())
            {
                Unit.MainSkill.UpdateOpening(1);
            }
        }

        if (SkillData.RegetTarget)
        {
            FindTarget();
        }

        Dispatch(SkillEffectTrigger.OnCast, CreateContext());

        if (SkillData.BurstCount > 0)
        {
            BurstCount = SkillData.BurstCount;
            IsBursting = true;
            BurstGap.Set(SkillData.BurstDelay);
            LastTargets.Clear();
            LastTargets.AddRange(Targets);
            Burst();
        }

        Targets.Clear();
    }

    protected override void Burst()
    {
        if (SkillData.BurstFind || SkillData.RegetTarget)
        {
            LastTargets.Clear();
            LastTargets.AddRange(GetAttackTarget());
        }

        var context = CreateContext();
        context.Targets = LastTargets;
        Dispatch(SkillEffectTrigger.OnCast, context);

        BurstCount--;
        if (BurstCount > 0)
        {
            BurstGap.Set(SkillData.BurstDelay);
        }
        else
        {
            IsBursting = false;
        }
    }

    public override void Hit(Unit target, Bullet bullet = null)
    {
        var context = CreateContext();
        context.Targets = new List<Unit> { target };
        Dispatch(SkillEffectTrigger.OnHit, context);
    }

    public override void Hit(Vector2 pos, Bullet bullet = null)
    {
        var context = CreateContext();
        context.Targets = new List<Unit>();
        context.TargetPositions = new List<Vector3> { new Vector3(pos.x, 0, pos.y) };
        Dispatch(SkillEffectTrigger.OnHit, context);
    }

    public override void BreakCast()
    {
        base.BreakCast();
        Dispatch(SkillEffectTrigger.OnBreak, CreateContext());
    }

    public override void Finish()
    {
        base.Finish();
        Dispatch(SkillEffectTrigger.OnEnd, CreateContext());
    }

    private void Dispatch(SkillEffectTrigger trigger, SkillContext context)
    {
        _dispatcher.Dispatch(trigger, context);
    }


    public SkillContext CreateContext()
    {
        var context = new SkillContext(this);
        context.Skill = this;
        context.Targets = Targets;
        return context;
    }
}
