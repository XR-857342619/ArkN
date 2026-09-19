using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于 SkillJsonData 的 JSON 技能宿主。
/// 它复用 Skill 基类的 SP/冷却/连发/蓄力等通用机制，具体行为由 Selectors/Sorters/Effects 组合完成。
/// <para>
/// 逐帧派发：技能进入“持续(loop)阶段”后，Update 会依次派发 OnLoopStart / OnLoopTick / OnLoopEnd，
/// 并推进实现了 ISkillEffectTick 的持续型效果器。
/// </para>
/// </summary>
public class JsonSkill : Skill
{
    private SkillJsonData _jsonData;
    private EffectDispatcher _dispatcher = new EffectDispatcher();

    // ---- 持续(loop)阶段状态 ----
    // >0：固定持续秒数；<0：直到技能结束/被打断；0：由持续型效果自行结束
    private float _loopTime;
    private readonly CountDown _loopTimer = new CountDown();
    private bool _loopActive;
    private bool _loopFinished; // 本次激活的 loop 是否已结束，避免同一次施法重复开启
    private bool _deathDispatched; // 防止 DoDie 重入导致 OnDeath 重复派发

    public SkillJsonData JsonData => _jsonData;
    public EffectDispatcher Dispatcher => _dispatcher;

    /// <summary>当前是否处于持续(loop)阶段。</summary>
    public bool LoopActive => _loopActive;

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

        _loopActive = false;
        _loopFinished = false;
        _deathDispatched = false;
        _loopTimer.Finish();

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

        // loop 阶段时长由 JsonSkill 自己管理，不写回 SkillData
        _loopTime = b.LoopTime;

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
        _loopFinished = false;
        Dispatch(SkillEffectTrigger.OnStart, CreateContext());

        // 部分 ReadyType（如“充能释放”）会先执行 Cast 再触发 OnSkillOpen，
        // 这里补一次开启，确保 OnStart 注册的持续效果能进入逐帧阶段。
        if (_dispatcher.HasRunningTicks) TryBeginLoop();
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
        SpSkillEffect();

        if (SkillData.BurstCount > 0)
        {
            BurstCount = SkillData.BurstCount;
            IsBursting = true;
            BurstGap.Set(SkillData.BurstDelay);
            LastTargets.Clear();
            LastTargets.AddRange(Targets);
            Burst();
        }

        // 本次施法结束后尝试进入持续阶段；放在 Targets.Clear 之前，让 OnLoopStart 仍能看到本次目标
        TryBeginLoop();

        Targets.Clear();
    }

    public override void Update()
    {
        base.Update();
        UpdateLoop(SystemConfig.DeltaTime);
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
        context.TargetPositions = ToPositions(LastTargets);
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
        // 与旧 Skill.Hit 一致：命中单位时先触发 OnAttack（攻击），再触发 OnHit（命中）
        OnAttack(target);

        var context = CreateContext();
        context.Targets = new List<Unit> { target };
        context.TargetPositions = ToPositions(context.Targets);
        Dispatch(SkillEffectTrigger.OnHit, context);
    }

    public override void Hit(Vector2 pos, Bullet bullet = null)
    {
        var context = CreateContext();
        context.Targets = new List<Unit>();
        context.TargetPositions = new List<Vector3> { new Vector3(pos.x, 0, pos.y) };
        Dispatch(SkillEffectTrigger.OnHit, context);
    }

    protected override void OnAttack(Unit target)
    {
        // 保留旧 TriggerEnum.攻击 的广播，再派发 JSON 的 OnAttack
        base.OnAttack(target);

        var context = CreateContext();
        context.Targets = new List<Unit> { target };
        context.TargetPositions = ToPositions(context.Targets);
        Dispatch(SkillEffectTrigger.OnAttack, context);
    }

    /// <summary>本技能造成击杀时由 Unit.DoDie 调用，派发 OnKill。</summary>
    public override void NotifyKillTarget(Unit deadTarget)
    {
        if (deadTarget == null) return;

        var context = CreateContext();
        context.Targets = new List<Unit> { deadTarget };
        context.TargetPositions = ToPositions(context.Targets);
        Dispatch(SkillEffectTrigger.OnKill, context);
    }

    /// <summary>技能持有者死亡时由 Unit.DoDie 调用，派发 OnDeath。</summary>
    public override void NotifyOwnerDeath()
    {
        if (_deathDispatched) return;
        _deathDispatched = true;
        Dispatch(SkillEffectTrigger.OnDeath, CreateContext());
    }

    public override void BreakCast()
    {
        base.BreakCast();
        EndLoop();
        Dispatch(SkillEffectTrigger.OnBreak, CreateContext());
    }

    public override void Finish()
    {
        base.Finish();
        EndLoop();
        Dispatch(SkillEffectTrigger.OnEnd, CreateContext());
    }

    public override void Reset()
    {
        base.Reset();
        _loopActive = false;
        _loopFinished = false;
        _deathDispatched = false;
        _loopTimer.Finish();
        _dispatcher.StopAll();
    }

    #region 持续(loop)阶段

    /// <summary>
    /// 尝试进入持续阶段。只有配置了 LoopTime / Loop 触发 / 持续型效果时才开启。
    /// </summary>
    private void TryBeginLoop()
    {
        if (_loopActive || _loopFinished) return;

        bool hasLoopStart = _dispatcher.HasTrigger(SkillEffectTrigger.OnLoopStart);
        bool hasLoopTick = _dispatcher.HasTrigger(SkillEffectTrigger.OnLoopTick);
        bool hasLoopEnd = _dispatcher.HasTrigger(SkillEffectTrigger.OnLoopEnd);
        bool hasLoopConfig = hasLoopStart || hasLoopTick || hasLoopEnd;

        if (!hasLoopConfig && !_dispatcher.HasRunningTicks && _loopTime == 0f)
        {
            // 普通技能：没有持续配置，也不是由持续效果驱动
            return;
        }

        if (_loopTime == 0f && !_dispatcher.HasRunningTicks && !hasLoopStart)
        {
            // LoopTime=0 时只能靠“可自行结束的持续效果”或 OnLoopStart 注册的持续效果来确定终点
            Debug.LogWarning($"JsonSkill {Id} 配置了 Loop 触发但 LoopTime=0，且没有可自行结束的持续效果，已跳过 loop");
            return;
        }

        _loopActive = true;
        _loopTimer.Set(_loopTime < 0f ? float.PositiveInfinity : Mathf.Max(0f, _loopTime));

        Dispatch(SkillEffectTrigger.OnLoopStart, CreateContext());

        // LoopTime=0 时，如果 OnLoopStart 没有注册任何持续效果，则本次 loop 立即结束
        if (_loopTime == 0f && !_dispatcher.HasRunningTicks)
        {
            EndLoop();
        }
    }

    private void UpdateLoop(float deltaTime)
    {
        if (!_loopActive) return;

        Dispatch(SkillEffectTrigger.OnLoopTick, CreateContext(), deltaTime);
        bool hasRunningTicks = _dispatcher.Tick(deltaTime);

        if (_loopTime < 0f) return; // 无限持续，直到技能结束/被打断

        if (_loopTime > 0f)
        {
            if (_loopTimer.Update(deltaTime)) EndLoop();
            return;
        }

        // LoopTime == 0：由持续型效果自行结束
        if (!hasRunningTicks) EndLoop();
    }

    private void EndLoop()
    {
        if (!_loopActive) return;

        _loopActive = false;
        _loopFinished = true;

        Dispatch(SkillEffectTrigger.OnLoopEnd, CreateContext());
        _dispatcher.StopAll();
    }

    #endregion

    private void Dispatch(SkillEffectTrigger trigger, SkillContext context, float deltaTime = 0f)
    {
        _dispatcher.Dispatch(trigger, context, deltaTime);
    }


    public SkillContext CreateContext()
    {
        var context = new SkillContext(this);
        context.Skill = this;
        context.Targets = Targets;

        // 统一填充“目标位置”通道：
        // SummonEffect 等只依赖 TargetPositions 的效果器可以直接使用，
        // 无需再自己根据“召唤位置”字段做选择。
        if (context.TargetPositions == null)
        {
            context.TargetPositions = ToPositions(Targets);
        }

        return context;
    }

    private static List<Vector3> ToPositions(List<Unit> units)
    {
        var result = new List<Vector3>();
        if (units == null) return result;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
                result.Add(units[i].Position);
        }
        return result;
    }
}
