using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Bullets;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;

public class Skill
{
    public Unit Unit;

    public Skill Parent;

    public List<Modify> Modifies = new List<Modify>();

    public List<Unit> Targets = new List<Unit>();

    List<GameObject> tiles = new List<GameObject>();

    bool showRange = false;
    string showBar = "";
    protected Battle Battle => Unit.Battle;

    public SkillData SkillData => Database.Instance.Get<SkillData>(Id);

    public int Id;

    public int StartId = -1;//升级前id

    public List<Vector2Int> AttackPoints;


    /// <summary>
    /// 冷却时间，对于单位主技能来说
    /// </summary>
    public CountDown Cooldown = new CountDown();
    /// <summary>
    /// 技能抬手
    /// </summary>
    public CountDown Casting = new CountDown();

    protected List<Unit> LastTargets = new List<Unit>();

    /// <summary>
    /// 连发计时
    /// </summary>
    public CountDown Bursting = new CountDown();
    public CountDown BurstGap = new CountDown();

    /// <summary>
    /// 连发计数
    /// </summary>
    public int BurstCount = -1;

    /// <summary>
    /// 开启时间
    /// </summary>
    public CountDown Opening = new CountDown();

    public float Power;
    bool canStop;
    public int MaxPower => (int)(Unit.SkillCost * MaxPowerBase);
    public float MaxPowerBase;
    public int PowerCount;
    public int UseCount;

    Effect ReadyEffect;

    public CountDown LoopingStart = new CountDown();
    public CountDown LoopingEnd = new CountDown();
    public CountDown Waiting = new CountDown();
    public Effect LoopStartEffect, LoopCastEffect;
    public bool Destroyed;
    Pool<MapTile> SkillRange = new Pool<MapTile>();

    //public string FilterExpression;
    public SkillTargetFilter filter;

    public bool IsCantOpen = false;
    public bool IsCantUse = false;
    //public bool IsCantCast = true;
    public int IsCantCastCount = 0;
    public bool IsCantCast = false;
    public bool IsCantBurst = false;
    public bool IsCantLoop = false;
    public bool IsNormalAttack = false;
    //public int 递归深度 = 1000;
    public bool IsBursting = false;

    public virtual void Init()
    {
        //Debug.Log(SkillData.Id + "初始化");
        if (SkillData.Modifys != null)
        {
            for (int i = 0; i < SkillData.Modifys.Length; i++)
            {
                //Debug.Log(SkillData.Modifys[i]);
                Modifies.Add(ModifyManager.Instance.Get(SkillData.Modifys[i], this));
            }
        }

        if (SkillData.AttackPoints != null)
        {
            AttackPoints = new List<Vector2Int>();
            UpdateAttackPoints();
        }
        showRange = SkillData.Data?.GetBool("ShowRange")?? false;
        showBar = SkillData.Data?.GetStr("ShowBar")?? "";

        filter = new SkillTargetFilter(Unit, Targets);
        //FilterExpression = SkillData.SkillCondition;

        canStop = SkillData.CanStop;
        MaxPowerBase = SkillData.MaxPower;
        PowerCount = SkillData.PowerCount;
        Reset();
        IsNormalAttack = SkillData.UseType == SkillUseTypeEnum.自动 && SkillData.MaxPower == 0 && SkillData.ModelAnimation != null && SkillData.DamageRate > 0;//4个条件判断技能是否为普攻，判断条件存疑
        //Waiting.Finish();
        //Debug.Log(SkillData.Id + "初始化完成");
    }

    public Skill GetFinalParent()
    {
        var result = this;
        while (result.Parent != null)
            result = result.Parent;
        return result;
    }

    public virtual void Reset()
    {
        Opening.Finish();
        UseCount = 0;
        Power = SkillData.StartPower;
        BreakCast();
        Cooldown.Finish();
    }

    public virtual void Update()
    {
        if (IsCantCastCount > 0)
            IsCantCast = true;
        else
        {
            IsCantCastCount = 0;
            IsCantCast = false;
        }
        if (LoopingStart.Update(SystemConfig.DeltaTime))
        {
            if (SkillData.LoopCastEffect != null)
            {
                LoopCastEffect = EffectManager.Instance.GetEffect(SkillData.LoopCastEffect.Value);
                LoopCastEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            }
        }
        if (LoopingEnd.Update(SystemConfig.DeltaTime))
        {
            Unit.UnitModel.ChangeToEnd();
            if (LoopCastEffect != null)
            {
                EffectManager.Instance.ReturnEffect(LoopCastEffect);
                LoopCastEffect = null;
            }
        }

        if (SkillData.PowerType == PowerRecoverTypeEnum.自动)
        {
            int exspeed = BattleManager.Instance.RecoverPowervSpeed == 16 ? 1000 : BattleManager.Instance.RecoverPowervSpeed;
            RecoverPower(Unit.PowerSpeed * SystemConfig.DeltaTime * exspeed);
        }

        if (SkillData.ReadyEffect != null)
        {
            if (ReadyEffect == null && Power >= MaxPower)
            {
                ReadyEffect = EffectManager.Instance.GetEffect(SkillData.ReadyEffect.Value);
                ReadyEffect.transform.SetParent(Unit.UnitModel.transform);
                ReadyEffect.transform.position = Unit.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.ReadyEffect.Value).BindPoint);
            }
            else if (ReadyEffect != null && Power < MaxPower)
            {
                EffectManager.Instance.ReturnEffect(ReadyEffect);
                ReadyEffect = null;
            }
        }

        if (SkillData.AutoUse && Power == MaxPower)
        {
            DoOpen();
        }

        if (!Casting.Finished()) //抬手期间，如果无有效目标，则取消抬手
        {
            if ((!SkillData.RegetTarget && !SkillData.NoTargetAlsoUse) && Targets.All(x => !CanUseTo(x)))
            {
                Log.Debug($"{Unit.UnitData.Id}的{SkillData.Name}全部目标不合法,强制打断抬手动作{Time.time}");
                BreakCast();
            }
        }

        if (Ready())
        {
            Start();
        }

        if (Casting.Update(SystemConfig.DeltaTime) && !(IsCantCast && IsNormalAttack))
        {
            Cast();
        }
        //if (Bursting.value != 0) Debug.Log("Bursting:"+Bursting.value);
        //if (Bursting.Update(SystemConfig.DeltaTime))
        if (BurstGap.Update(SystemConfig.DeltaTime) && IsBursting)
        {
            //BurstGap.Update(SystemConfig.DeltaTime);
            //Log.Debug("连击延迟" + BurstGap.value + "秒");
            //Debug.Log("连击延迟" + BurstGap.value + "秒");
            //if (BurstGap.Finished())
            Burst();
        }


        if (SkillData.StopBreak)
        {
            if (Unit.IfStoped() && Unit.AttackingSkill == this)
            {
                BreakCast();
            }
        }
        Waiting.Update(SystemConfig.DeltaTime);
        //BurstGap.Update(SystemConfig.DeltaTime);
        //Debug.Log(Waiting.value);
    }

    //与ready不同的是，被动技能也会受此函数影响
    public virtual bool Useable()
    {
        if (Destroyed) return false;
        if (SkillData.MaxUseCount != 0 && UseCount >= SkillData.MaxUseCount) return false;
        if (GetFinalParent() == Unit.FirstSkill && !Unit.CanAttack)
        {
            //Debug.Log($"{Unit.UnitData.Id} 因为缴械无法使用{SkillData.Id}");
            return false;
        }
        if (SkillData.StopBreak && Unit.IfStoped()) return false;
        if (!Cooldown.Finished()) return false;

        if (SkillData.OpenDisable && !Unit.MainSkill.Opening.Finished()) return false;
        if (SkillData.EnableBuff != null && !SkillData.EnableBuff.All(x => Unit.Buffs.Any(y => y.Id == x)))
            return false;
        if (SkillData.DisableBuff != null && SkillData.DisableBuff.Any(x => Unit.Buffs.Any(y => y.Id == x)))
        {
            return false;
        }
        return true;
    }

    public bool CanUseTo(Unit target)
    {
        if (target == null) return false;
        if (!SkillData.MidLimit && target.GetType() == typeof(Units.中立单位)) return false;
        if ((SkillData.IfHeal && !SkillData.DamageWithFrameRate) && ((!target.CanBeHeal && !target.HealOnly.Contains(Unit.Id)) || target.Hp == target.MaxHp) && (target != Unit)) return false;
        if (target.IfSleep && !SkillData.IgnoreSleep) return false;
        if (!target.IfSelectable && !SkillData.IgnoreSelectable) return false;
        if ((SkillData.TargetTeam >> target.Team) % 2 == 0) return false;
        if (target.UnitData.StopAttackOnly && target != Unit)
        {
            if (Unit is Units.干员 u && !u.StopUnits.Contains(target)) return false;
            if (Unit is Units.敌人 u1 && u1.StopUnit != (target)) return false;
            if (Unit is Units.中立单位) return false;
        }
        switch (SkillData.TargetFilter)
        {
            case SkillTargetFilterEnum.仅自己:
                if (target != Unit) return false;
                break;
            case SkillTargetFilterEnum.自己以外:
                if (target == Unit) return false;
                break;
            case SkillTargetFilterEnum.召唤物:
                if (target != Unit && !(Unit as Units.干员).Children.Contains(target)) return false;
                break;
            case SkillTargetFilterEnum.仅召唤:
                if (!(Unit as Units.干员).Children.Contains(target)) return false;
                break;
        }
        if (SkillData.SelfHpLess != 0 && Unit.Hp / Unit.MaxHp > SkillData.SelfHpLess) return false;
        if (SkillData.TargetHpLess != 0 && target.Hp / target.MaxHp > SkillData.TargetHpLess) return false;
        if (SkillData.TargetHpMore != 0 && target.Hp / target.MaxHp < SkillData.TargetHpMore) return false;
        if (SkillData.UnitLimit != null && !SkillData.UnitLimit.Contains(target.Id)) return false;
        if (SkillData.ProfessionLimit != UnitTypeEnum.无 && SkillData.ProfessionLimit != target.UnitData.Profession)
            return false;
        if (!SkillData.AttackFly && target.Height > 0) return false;
        if (!target.Alive() && !SkillData.DeadFind) return false;
        if ((!(SkillData.AntiHide || Unit.Team == target.Team)) && target.IfHide) return false;
        if (SkillData.TargetDisableBuff != null)
        {
            if (SkillData.TargetDisableBuff.Any(x => target.Buffs.Any(y => y.Id == x))) return false;
        }
        if (SkillData.TargetEnableBuff != null)
        {
            if (SkillData.TargetEnableBuff.Any(x => target.Buffs.All(y => y.Id != x))) return false;
        }
        if (SkillData.RareLimit != 0 && target.UnitData.Rare != SkillData.RareLimit) return false;
        if (SkillData.CostLimit != 0 && target.UnitData.Cost > SkillData.CostLimit) return false;
        if (SkillData.PosLimit != 0)
        {
            if (target.NowGrid == null) return false;
            if (SkillData.PosLimit == 1 && target.NowGrid.FarAttackGrid) return false;
            if (SkillData.PosLimit == 2 && !target.NowGrid.FarAttackGrid) return false;
        }
        return true;
    }
    public virtual void UpdateCooldown()
    {
        if (Cooldown.Update(SystemConfig.DeltaTime))
        {
            if (Unit.AttackingSkill == this)
                Unit.AttackingSkill = null;
        }
    }

    public virtual void UpdateOpening()
    {
        if (!Opening.Finished() && SkillData.PowerUseType == PowerRecoverTypeEnum.自动)
        {
            UpdateOpening(SystemConfig.DeltaTime);
        }
    }

    public void UpdateOpening(float time)
    {
        if (Opening.Update(time))
        {
            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = Unit,
                Skill = this,
            });
            //Debug.Log("技能结束");
            Unit.Trigger(TriggerEnum.技能结束);
            Battle.TriggerDatas.Pop();
            OnOpenEnd();
        }
    }

    public virtual bool Ready()
    {
        if (!LoopingStart.Finished()) return false;
        if (Unit.IfStun && !SkillData.IgnoreStun)
            return false;
        if (SkillData.UseType == SkillUseTypeEnum.被动) return false;
        switch (SkillData.ReadyType)
        {
            case SkillReadyEnum.特技激活:
                if (Unit.MainSkill == null)
                {
                    if (Opening.Finished()) return false;
                }
                else
                {
                    if (Unit.MainSkill.Opening.Finished()) return false;
                }
                break;
            case SkillReadyEnum.禁止主动:
                return false;
            case SkillReadyEnum.充能释放:
                if (Power < MaxPower) return false;
                break;
            default:
                break;
        }
        if (SkillData.StopBreak && Unit.IfStoped()) return false;

        if (SkillData.AttackMode == AttackModeEnum.跟随攻击 && (Unit.AttackingSkill != null || !Unit.AttackingAction.Finished())) return false;

        if (SkillData.UseType == SkillUseTypeEnum.手动) //手动技能在技能开启时可以使用
            return !Opening.Finished();
        //自动充能技在有充能时才能使用,另外要和自动开启技能区分开
        if (SkillData.MaxPower > 0 && SkillData.UseType == SkillUseTypeEnum.自动 && Power < MaxPower && !SkillData.AutoUse) return false;
        if (SkillData.EffectiveRate > 0 && Battle.Random.NextDouble() > SkillData.EffectiveRate) return false;
        //不管什么技能 都要遵循技能CD
        return Cooldown.Finished();
    }

    public virtual bool InAttackUsing()
    {
        if (SkillData.NotAttackFlag) return false;
        if (SkillData.UseType == SkillUseTypeEnum.被动) return false;
        if (SkillData.EnableBuff != null && !SkillData.EnableBuff.All(x => Unit.Buffs.Any(y => y.Id == x)))
            return false;
        if (SkillData.DisableBuff != null && SkillData.DisableBuff.Any(x => Unit.Buffs.Any(y => y.Id == x)))
            return false;
        if (SkillData.AttackPoints == null) return false;

        if (SkillData.DamageRate == 0) return false;

        if (SkillData.ReadyType == SkillReadyEnum.特技激活 &&
            ((SkillData.MaxPower > 0 && !Opening.Finished())
            || (SkillData.MaxPower == 0 && !Unit.MainSkill.Opening.Finished())
            ))
        {
            return true;
        }
        if (SkillData.ReadyType == SkillReadyEnum.None)
        {
            return true;
        }
        return false;
    }

    public virtual void ResetCooldown(float attackSpeed)
    {
        //TODO 读Unit的攻击间隔变化
        var cooldown = (SkillData.Cooldown == 0 && SkillData.AttackMode == AttackModeEnum.跟随攻击 ? Unit.AttackGap : SkillData.Cooldown) * attackSpeed;
        //Debug.Log(SkillData.Id + "cooldown:" + cooldown);
        //if (cooldown < 0.1f) cooldown = 0.1f;
        Cooldown.Set(cooldown);
    }

    public void RecoverPower(float count, bool withTip = false, bool ignoreOpening = false)
    {
        if (PowerCount == 0) return;
        if (!Opening.Finished() && !ignoreOpening)
            return;
        if (SkillData.PowerStopNeed)
        {
            if (Unit.IfStun) return;
            if (Unit is Units.干员 u && u.StopUnits.Count == 0) return;
            if (Unit is Units.敌人 u1 && u1.StopUnit == null) return;
        }
        if (withTip)
        {
            Unit.UnitModel.ShowPower(count);
        }
        Power += count;
        if (Power > MaxPower * PowerCount)
        {
            Power = MaxPower * PowerCount;
        }
    }

    #region 主动相关
    public bool CanOpen()
    {
        if (Unit.State == StateEnum.Die) return false;
        if (SkillData.SkillCost > Battle.Cost) return false;
        if (SkillData.UseType == SkillUseTypeEnum.手动 && Unit.IfStun) return false;
        if (SkillData.ReadyType == SkillReadyEnum.充能释放 && SkillData.UseType == SkillUseTypeEnum.手动 && !SkillData.NoTargetAlsoUse)
        {
            var target = GetAttackTarget();
            if (target.Count == 0)
            {
                return false;
            }
        }
        if (SkillData.ReadyType == SkillReadyEnum.充能释放 & !Useable()) return false;
        return Opening.Finished() && Power >= MaxPower;
    }

    public virtual void DoOpen()
    {
        Battle.Cost -= SkillData.SkillCost;
        if (Unit is Units.干员 u1 && !u1.Start.Finished())
        {
            u1.Start.Finish();
            u1.StartEnd();
        }
        //Debug.Log("OpenSkill");
        if (SkillData.StopOtherSkill)
        {
            Unit.BreakAllCast();
        }
        if (SkillData.ReadyType == SkillReadyEnum.特技激活)
        {
            Power -= MaxPower;
            Opening.Set(SkillData.OpenTime);
        }
        if (SkillData.ReadyType == SkillReadyEnum.充能释放)
        {
            Start();
        }
        var animation = SkillData.OverwriteAnimation;
        if (SkillData.OverwriteAnimationDown != null && Unit is Units.干员 u && u.Direction_E == DirectionEnum.Up) animation = SkillData.OverwriteAnimationDown;
        Unit.OverWriteAnimation = animation;
        if (animation != null && animation.Length > 1)
        {
            if (SkillData.LoopStartEffect != null)
            {
                LoopStartEffect = EffectManager.Instance.GetEffect(SkillData.LoopStartEffect.Value);
                LoopStartEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            }
            LoopingStart.Set(Unit.UnitModel.GetAnimationDuration(animation[0]));
            LoopingEnd.Set(Opening.value - Unit.UnitModel.GetAnimationDuration(animation[2]));
        }
        if (SkillData.OverwriteAnimation == null && SkillData.LoopCastEffect != null)
        {
            LoopCastEffect = EffectManager.Instance.GetEffect(SkillData.LoopCastEffect.Value);
            LoopCastEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            LoopingEnd.Set(Opening.value);
        }
        OnSkillOpen();
    }

    #endregion

    #region 使用流程

    protected virtual float GetSkillDelay(string[] animationName, string[] lastState, out float fullDuration, out float beginDuration)
    {
        return Unit.UnitModel.GetSkillDelay(animationName, lastState, out fullDuration, out beginDuration);
    }

    float lastSpeed = 1;
    /// <summary>
    /// 技能抬手
    /// </summary>
    public virtual void Start()
    {
        if (!Useable())
        {
            //Debug.Log(SkillData.Id + "不可用");
            return;
        }
        if (Targets.Count == 0)
        {
            FindTarget();
        }
        if (Targets.Count > 0)
        {
            if (Targets[0] != Unit && SkillData.ModelAnimation != null && !SkillData.DisableScaleX)//默认不填动作的技能不需要转身
            {
                var scaleX = (Targets[0].Position - Unit.Position).x > 0 ? 1 : -1;
                if (scaleX != Unit.ScaleX)
                {
                    Unit.TargetScaleX = scaleX;
                }
            }
        }
        else if (!SkillData.NoTargetAlsoUse)
        {
            return;
        }
        //走到这里技能就真的用出来了
        UseCount++;
        if (showRange)
            ShowUnitAttackArea();
        //Log.Debug(SkillData.Id + "开始使用");
        //Debug.Log(Unit.UnitData.Id + "的" + SkillData.Id + "使用次数:" + UseCount);
        if (SkillData.ReadyType == SkillReadyEnum.充能释放)
        {
            Opening.Set(SkillData.OpenTime);
        }

        if (SkillData.ModelAnimation == null)
        {
            //Debug.Log(Unit.UnitData.Id + "的" + SkillData.Id + "没有动画,直接使用");
            ResetCooldown(1);
            if (SkillData.AnimationTime == null)
                Cast();
            else
                Casting.Set(SkillData.AnimationTime.Value);
        }
        else
        {
            var animation = SkillData.ModelAnimation;
            if (SkillData.ModelAnimationDown != null && Unit is Units.干员 u && u.Direction_E == DirectionEnum.Up) animation = SkillData.ModelAnimationDown;
            var duration = GetSkillDelay(SkillData.OverwriteAnimation == null ? animation : SkillData.OverwriteAnimation, Unit.GetAnimation(), out float fullDuration, out float beginDuration);//.SkeletonAnimation.skeleton.data.Animations.Find(x => x.Name == "Attack");
            if (SkillData.AnimationTime != null) duration = SkillData.AnimationTime.Value;
            float attackSpeed = 1f / Unit.Agi * 100;//攻速影响冷却时间
            if (SkillData.AttackMode == AttackModeEnum.固定间隔) attackSpeed = 1;
            ResetCooldown(attackSpeed);
            //float aniSpeed = 1;//动画表现上的攻速
            if (fullDuration * attackSpeed != Cooldown.value)
            {
                //动画时间已经超出攻击间隔了，此时攻速被攻击间隔强制拉快，动画速度也会被强制拉快
                //动画时间低于攻击间隔时，动画也会被拉长
                attackSpeed = Cooldown.value / fullDuration;
                attackSpeed = Mathf.Clamp(attackSpeed, 0.1f, Unit.UnitData.MaxAnimationScale);
            }
            duration = duration * attackSpeed;
            fullDuration = fullDuration * attackSpeed;
            if (fullDuration < duration) fullDuration = duration;
            this.lastSpeed = 1f / attackSpeed;
            if (IsNormalAttack && !IsCantCast)
                Unit.AttackingAction.Set(fullDuration > 2 ? 1.5f : fullDuration-0.5f > 0 ? fullDuration-0.5f : 0.1f);
            Unit.AttackingAction.Set(fullDuration);
            Unit.State = StateEnum.Attack;
            Unit.AnimationName = animation;
            Unit.AttackingSkill = this;
            //Debug.Log(SkillData.ModelAnimation);
            if (SkillData.OverwriteAnimation == null)
            {
                Unit.UnitModel?.BreakAnimation();//防止覆盖动画被打断
                Unit.AnimationSpeed = 1 / attackSpeed * (beginDuration + fullDuration) / fullDuration;
            }
            duration = (duration + beginDuration) * fullDuration / (beginDuration + fullDuration);
            //Debug.Log(duration);
            float waitime = duration;
            //Debug.Log(IsNormalAttack + "&&" + IsCantCast + "&&" + Waiting.Finished() + " " + Waiting.value);
            if (IsNormalAttack && IsCantCast && Waiting.Finished())
            {
                //Debug.Log("设置计时器");
                if (fullDuration > 2)
                    waitime = 2f;
                Waiting.Set(waitime);
                //fullDuration += 0.5f;
                Cooldown.value += 0.5f;
                //Log.Debug(Unit.UnitData.Id + "的" + SkillData.Id + "的打断时机:");
            }
            Casting.Set(duration);
            //Debug.Log(Unit.UnitData.Id + "的" + SkillData.Id + "AttackStart,pointDelay:" + duration + ",fullDuration" + fullDuration + ",beginDuration" + beginDuration + ",Time:" + Time.time + ",Cooldown:" + Cooldown.value);
            if (IsCantCast && Waiting.Finished())
            {
                Log.Debug("尝试打断" + Unit.UnitData.Id + "的" + SkillData.Id);
                Unit.UnitModel?.BreakAnimation();
                Unit.State = StateEnum.Idle;
                Unit.AnimationName = Unit.UnitData.IdleAnimation;
                Casting.Finish();
            }
            if (duration == 0 && !IsCantCast)
            {
                //Debug.Log(Unit.UnitData.Id + "的" + SkillData.Id + "Cast" + IsCantCast);
                Cast();
            }
            if (IsCantCastCount > 0)
                IsCantCastCount -= 1;
        }

        if (SkillData.StartEffect != null)
        {
            foreach (var id in SkillData.StartEffect)
            {
                var ps = EffectManager.Instance.GetEffect(id);
                ps.Init(Unit, Unit, Unit.Position, Unit.Direction, lastSpeed);
            }
        }
    }

    /// <summary>
    /// 实际生效点
    /// </summary>
    public virtual void Cast()
    {
        if (SkillData.ReadyType == SkillReadyEnum.充能释放)//充能类技能成功释放时才会消耗充能
        {
            Power -= MaxPower;
        }

        //if (SkillData.PowerUseType == PowerRecoverTypeEnum.攻击 && IsNormalAttack)//有动作有伤害的技能视为普攻，用于消耗弹药
        if (SkillData.PowerUseType == PowerRecoverTypeEnum.攻击)//有动作有伤害的技能视为普攻，用于消耗弹药
        {
            UpdateOpening(1);
            //if (Unit.MainSkill != null && Unit.MainSkill != this && !Unit.MainSkill.Opening.Finished() && Unit.MainSkill.SkillData.PowerUseType == PowerRecoverTypeEnum.无)
            if (Unit.MainSkill != null && Unit.MainSkill != this && !Unit.MainSkill.Opening.Finished())
                Unit.MainSkill.UpdateOpening(1);
        }

        if (SkillData.RegetTarget) FindTarget();//对于某些技能，无法攻击到已经离开攻击区域的单位
        //if (SkillData.Id == "重构体2")
        //{
        //    Debug.Log("重构体索敌" + Targets.First().UnitData.Name);
        //}
        if (Targets.Count > 0)
        {
            if (SkillData.AttackPoint)
            {
                List<Vector2Int> ps = new List<Vector2Int>();
                foreach (var t in Targets) if (!ps.Contains(t.GridPos) && AttackPoints.Contains(t.GridPos)) ps.Add(t.GridPos);
                if (ps.Count < SkillData.Data.GetInt("HitCount"))//索敌数量不够 随机炸
                {
                    int count = SkillData.Data.GetInt("HitCount") - ps.Count;
                    List<Vector2Int> al = new List<Vector2Int>(AttackPoints);
                    foreach (var p in ps) al.Remove(p);
                    for (int i = 0; i < count; i++)
                    {
                        var p = al[Battle.Random.Next(0, al.Count)];
                        al.Remove(p);
                        ps.Add(p);
                    }
                }
                foreach (var p in ps)
                {
                    Effect(Battle.Map.Tiles.Get(p.x, p.y).Pos);
                }
            }
            else
            {
                var a = Targets.ToArray();
                foreach (var t in a) Effect(t);
                if (SkillData.Bullet == null) foreach (var t in a) removeBuff(t);
            }
        }
        CastExSkill();
        if (SkillData.BurstCount > 0)
        {
            //Debug.Log(Unit.UnitData.Id + "的" + SkillData.Id + "开始Burst");
            //Burst();
            BurstCount = SkillData.BurstCount;
            IsBursting = true;
            BurstGap.Set(SkillData.BurstDelay);
            LastTargets.Clear();
            LastTargets.AddRange(Targets);
        }
        Targets.Clear();
        if (SkillData.CastEffect != null)
        {
            foreach (var id in SkillData.CastEffect)
            {
                var ps = EffectManager.Instance.GetEffect(id);
                ps.Init(Unit, Unit, Unit.Position, Unit.Direction, lastSpeed);
            }
        }
    }

    protected virtual void CastExSkill()
    {
        if (SkillData.ExSkills != null)
        {
            if (SkillData.ExSkillWeight == null)
            {
                foreach (var skillId in SkillData.ExSkills)
                {
                    Unit.Skills.Find(x => x.Id == skillId).Start();
                }
            }
            else
            {
                int sum = SkillData.ExSkillWeight.Sum();
                var r = Battle.Random.Next(0, sum);
                for (int i = 0; i < SkillData.ExSkillWeight.Length; i++)
                {
                    r -= SkillData.ExSkillWeight[i];
                    if (r < 0)
                    {
                        Unit.Skills.Find(x => x.Id == SkillData.ExSkills[i]).Start();
                        break;
                    }
                }
            }
        }
    }

    protected virtual void Burst()
    {
        //Debug.Log("正在连发");
        //if (BurstCount == SkillData.BurstCount)
        //{
        //    BurstCount = SkillData.BurstCount;
        //    LastTargets.Clear();
        //    LastTargets.AddRange(Targets);
        //}
        if (SkillData.BurstFind || SkillData.RegetTarget) //当目标为随机时
        {
            LastTargets.Clear();
            LastTargets.AddRange(GetAttackTarget());
        }
        foreach (var target in LastTargets)
        {
            Effect(target);
        }
        //Debug.Log(LastTargets.Count + "个目标");
        BurstCount--;
        if (BurstCount > 0)
            BurstGap.Set(SkillData.BurstDelay);
        else
            IsBursting = false;
    }

    /// <summary>
    /// 技能对一个单位实际生效的效果
    /// </summary>
    /// <param name="target"></param>
    public virtual void Effect(Unit target)
    {
        if (!CanUseTo(target)) return;
        if (SkillData.GatherEffect != null && Targets.Count > 0)
        {
            var ps = EffectManager.Instance.GetEffect(SkillData.GatherEffect.Value);
            ps.Init(Unit, Targets[0], Targets[0].Position, Targets[0].Direction, lastSpeed);
        }
        if (SkillData.Bullet == null)
        {
            Hit(target);
        }
        else
        {
            //创建一个子弹
            var startPoint = Unit.UnitModel.GetPoint(SkillData.ShootPoint);
            //Debug.Log($"攻击{target.Config.Name}:{target.Hp} 起点：{startPoint}");
            Battle.CreateBullet(SkillData.Bullet.Value, startPoint, Vector3.zero, target, this);
        }
    }

    public virtual void Effect(Vector3 pos)
    {
        if (SkillData.GatherEffect != null && Targets.Count > 0)
        {
            var ps = EffectManager.Instance.GetEffect(SkillData.GatherEffect.Value);
            ps.Init(Unit, null, pos, Vector2.zero, lastSpeed);
        }
        if (SkillData.Bullet == null)
        {
            Hit(pos);
        }
        else
        {
            //创建一个子弹
            var startPoint = Unit.UnitModel.GetPoint(SkillData.ShootPoint);
            //Debug.Log($"攻击{target.Config.Name}:{target.Hp} 起点：{startPoint}");
            Battle.CreateBullet(SkillData.Bullet.Value, startPoint, pos, null, this);
        }
    }

    /// <summary>
    /// 伤害判定阶段
    /// </summary>
    /// <param name="target"></param>
    public virtual void Hit(Unit target, Bullet bullet = null)
    {
        Debug.Log("hit1");
        if (SkillData.HitEffect != null)
        {
            var ps = EffectManager.Instance.GetEffect(SkillData.HitEffect.Value);
            ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.HitEffect.Value).BindPoint);
            if (bullet != null)
            {
                ps.Init(Unit, target, bullet.TargetPos, bullet.Direction);
            }
            else ps.Init(Unit, target, target.GetHitPoint(), Vector3.zero); //ps.transform.rotation = Quaternion.identity;
            //ps.Play();
        }
        if (IsNormalAttack)
        {
            foreach (var skill in Unit.Skills)
            {
                if (skill.SkillData.PowerType == PowerRecoverTypeEnum.攻击)
                {
                    skill.RecoverPower(1);
                }
            }
        }
        if (SkillData.DamageRate > 0)
        {
            OnAttack(target);
            DamageInfo dInfo = null;
            if (SkillData.AreaRange != 0)
            {
                var targets = Battle.FindAll(target.Position2, SkillData.AreaRange, SkillData.TargetTeam);
                if (!SkillData.AreaNoCheck) targets.RemoveWhere(x => !CanUseTo(x));
                foreach (var t in targets)
                {
                    addBuff(t);
                    if (SkillData.EffectEffect != null)
                    {
                        var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                        ps.Init(Unit, t, bullet != null ? bullet.Position : Unit.Position, bullet != null ? bullet.Direction : Unit.Direction.ToV3());
                        ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.EffectEffect.Value).BindPoint);
                        //ps.Play();
                    }
                    dInfo = GetDamageInfo(t, (t == target ? SkillData.AreaMainDamage : SkillData.AreaDamage)*((bullet != null && bullet is 链式弹道  linkBullet) ? linkBullet.reductionRate : 1));
                    t.Damage(dInfo);

                    if (!SkillData.IfHeal)
                    {
                        if (dInfo.Avoid)
                        {
                            OnBeAvoid(t);
                        }
                        DoLifeSteal(dInfo);
                        OnBeAttack(t);
                    }
                }
            }
            else if (SkillData.AreaPoints != null)
            {
                var area = SkillData.AreaPoints.Select(x => x + target.GridPos).ToList();
                var targets = Battle.FindAll(area, SkillData.TargetTeam);
                if (!SkillData.AreaNoCheck) targets.RemoveWhere(x => !CanUseTo(x));
                foreach (var t in targets)
                {
                    addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0] ?? "");
                    addBuff(t);
                    if (SkillData.EffectEffect != null)
                    {
                        var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                        ps.Init(Unit, t, bullet != null ? bullet.Position : Unit.Position, bullet != null ? bullet.Direction : Unit.Direction.ToV3());
                        ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.EffectEffect.Value).BindPoint);
                        //ps.Play();
                    }
                    dInfo = GetDamageInfo(t, (t == target ? SkillData.AreaMainDamage : SkillData.AreaDamage) * ((bullet != null && bullet is 链式弹道 linkBullet) ? linkBullet.reductionRate : 1));
                    t.Damage(dInfo);

                    if (!SkillData.IfHeal)
                    {
                        if (dInfo.Avoid)
                        {
                            OnBeAvoid(t);
                        }
                        DoLifeSteal(dInfo);
                        OnBeAttack(t);
                    }
                }
            }
            else
            {
                addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0] ?? "");
                addBuff(target);
                if (SkillData.EffectEffect != null)
                {
                    var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                    ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.EffectEffect.Value).BindPoint);
                    ps.Play();
                }
                if (SkillData.IfHeal)
                {
                    dInfo = GetDamageInfo(target, (bullet != null && bullet is 链式弹道 linkBullet) ? linkBullet.reductionRate : 1);
                    Debug.Log("heal"+dInfo.Attack+" "+dInfo.DamageRate);
                    target.Heal(dInfo, !SkillData.DamageWithFrameRate);
                    OnHeal(target);
                }
                else
                {
                    dInfo = GetDamageInfo(target);
                    target.Damage(dInfo);
                    if (dInfo.Avoid)
                    {
                        OnBeAvoid(target);
                    }
                    DoLifeSteal(dInfo);
                }
                if (!SkillData.IfHeal) OnBeAttack(target);
            }
            if (dInfo != null && dInfo.FinalDamage > 0)
            {
                Battle.TriggerDatas.Push(new TriggerData()
                {
                    User = Unit,
                    Target = target,
                });
                Unit.Trigger(TriggerEnum.击中);
                Battle.TriggerDatas.Pop();
            }
        }
        else
        {
            addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0]?? "");
            //Debug.Log(target.UnitData.Name + "受到" + SkillData.ElementInjure.Keys.ToArray()[0] + "伤害");
            addBuff(target);
        }
        removeBuff(target);
    }

    public virtual void Hit(Vector2 pos, Bullet bullet = null)
    {
        Debug.Log("hit2");
        if (SkillData.HitEffect != null)
        {
            var ps = EffectManager.Instance.GetEffect(SkillData.HitEffect.Value);
            //ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.HitEffect.Value).BindPoint);
            if (bullet != null)
            {
                ps.Init(Unit, null, bullet.TargetPos, bullet.Direction);
            }
            else ps.Init(Unit, null, pos, Vector3.zero); //ps.transform.rotation = Quaternion.identity;
            //ps.Play();
        }
        if (SkillData.DamageRate > 0)
        {
            DamageInfo dInfo = null;
            if (SkillData.AreaRange != 0)
            {
                var targets = Battle.FindAll(pos, SkillData.AreaRange, SkillData.TargetTeam);
                foreach (var t in targets)
                {
                    addBuff(t);
                    if (SkillData.EffectEffect != null)
                    {
                        var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                        ps.Init(Unit, t, bullet != null ? bullet.Position : Unit.Position, bullet != null ? bullet.Direction : Unit.Direction.ToV3());
                        //ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.EffectEffect.Value).BindPoint);
                        //ps.Play();
                    }
                    dInfo = GetDamageInfo(t, SkillData.AreaDamage);
                    t.Damage(dInfo);

                    if (!SkillData.IfHeal)
                    {
                        if (dInfo.Avoid)
                        {
                            OnBeAvoid(t);
                        }
                        DoLifeSteal(dInfo);
                        OnBeAttack(t);
                    }
                }
            }
        }
    }

    public virtual void Hit(Unit target, float fixedDamageValue, Bullet bullet = null, bool ignorBuff = false)
    {
        Debug.Log("hit3");
        if (SkillData.HitEffect != null)
        {
            var ps = EffectManager.Instance.GetEffect(SkillData.HitEffect.Value);
            if (bullet != null)
            {
                ps.Init(Unit, target, bullet.TargetPos, bullet.Direction);
            }
            else ps.Init(Unit, target, target.GetHitPoint(), Vector3.zero);
        }
        if (IsNormalAttack)
        {
            foreach (var skill in Unit.Skills)
            {
                if (skill.SkillData.PowerType == PowerRecoverTypeEnum.攻击)
                {
                    skill.RecoverPower(1);
                }
            }
        }
        if (SkillData.DamageRate > 0 || fixedDamageValue >= 0)
        {
            OnAttack(target);
            DamageInfo dInfo = null;
            if (SkillData.AreaRange != 0)
            {
                var targets = Battle.FindAll(target.Position2, SkillData.AreaRange, SkillData.TargetTeam);
                if (!SkillData.AreaNoCheck) targets.RemoveWhere(x => !CanUseTo(x));
                foreach (var t in targets)
                {
                    if (!ignorBuff)
                        addBuff(t);
                    if (SkillData.EffectEffect != null)
                    {
                        var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                        ps.Init(Unit, t, bullet != null ? bullet.Position : Unit.Position, bullet != null ? bullet.Direction : Unit.Direction.ToV3());
                    }

                    // 使用固定伤害值或计算伤害
                    float damageValue = fixedDamageValue >= 0 ? fixedDamageValue :
                        ((t == target ? SkillData.AreaMainDamage : SkillData.AreaDamage) *
                        ((bullet != null && bullet is 链式弹道 linkBullet) ? linkBullet.reductionRate : 1));

                    dInfo = GetDamageInfo(t, damageValue);
                    t.Damage(dInfo);

                    if (!SkillData.IfHeal)
                    {
                        if (dInfo.Avoid)
                        {
                            OnBeAvoid(t);
                        }
                        DoLifeSteal(dInfo);
                        OnBeAttack(t);
                    }
                }
            }
            else if (SkillData.AreaPoints != null)
            {
                var area = SkillData.AreaPoints.Select(x => x + target.GridPos).ToList();
                var targets = Battle.FindAll(area, SkillData.TargetTeam);
                if (!SkillData.AreaNoCheck) targets.RemoveWhere(x => !CanUseTo(x));
                foreach (var t in targets)
                {
                    addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0] ?? "");
                    if (!ignorBuff)
                        addBuff(t);
                    if (SkillData.EffectEffect != null)
                    {
                        var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                        ps.Init(Unit, t, bullet != null ? bullet.Position : Unit.Position, bullet != null ? bullet.Direction : Unit.Direction.ToV3());
                    }

                    // 使用固定伤害值或计算伤害
                    float damageValue = fixedDamageValue >= 0 ? fixedDamageValue :
                        ((t == target ? SkillData.AreaMainDamage : SkillData.AreaDamage) *
                        ((bullet != null && bullet is 链式弹道 linkBullet) ? linkBullet.reductionRate : 1));

                    dInfo = _GetLogosDamageInfo(t, damageValue);
                    t.Damage(dInfo);

                    if (!SkillData.IfHeal)
                    {
                        if (dInfo.Avoid)
                        {
                            OnBeAvoid(t);
                        }
                        DoLifeSteal(dInfo);
                        OnBeAttack(t);
                    }
                }
            }
            else
            {
                addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0] ?? "");
                if (!ignorBuff)
                    addBuff(target);
                if (SkillData.EffectEffect != null)
                {
                    var ps = EffectManager.Instance.GetEffect(SkillData.EffectEffect.Value);
                    ps.transform.position = target.UnitModel.GetPoint(Database.Instance.Get<EffectData>(SkillData.EffectEffect.Value).BindPoint);
                    ps.Play();
                }
                if (SkillData.IfHeal)
                {
                    dInfo = _GetLogosDamageInfo(target, (bullet != null && bullet is 链式弹道 linkBullet) ? linkBullet.reductionRate : 1);
                    target.Heal(dInfo, !SkillData.DamageWithFrameRate);
                    OnHeal(target);
                }
                else
                {
                    // 使用固定伤害值或计算伤害
                    float damageValue = fixedDamageValue >= 0 ? fixedDamageValue : 1f;
                    dInfo = _GetLogosDamageInfo(target, damageValue);
                    target.Damage(dInfo);
                    if (dInfo.Avoid)
                    {
                        OnBeAvoid(target);
                    }
                    DoLifeSteal(dInfo);
                }
                if (!SkillData.IfHeal) OnBeAttack(target);
            }
            if (dInfo != null && dInfo.FinalDamage > 0)
            {
                Battle.TriggerDatas.Push(new TriggerData()
                {
                    User = Unit,
                    Target = target,
                });
                Unit.Trigger(TriggerEnum.击中);
                Battle.TriggerDatas.Pop();
            }
        }
        else
        {
            addEleInjure(target, SkillData.ElementInjure?.Keys.ToArray()[0] ?? "");
            if (!ignorBuff)
                addBuff(target);
        }
        removeBuff(target);
    }

    protected virtual void addEleInjure(Unit target, string eleType)
    {
        if (eleType == "" || SkillData.ElementInjure == null || SkillData.ElementInjure.Count == 0)
            return;
        float injure = SkillData.ElementInjure.GetFloat(eleType);
        if (injure != 0)
            target.ChangeEleInjure(injure > 1 ? injure : Unit.Attack * injure, eleType);
    }

    protected virtual void addBuff(Unit target)
    {
        if (SkillData.Buffs != null)
        {
            for (int i = 0; i < SkillData.Buffs.Length; i++)
            {
                var buffChance = 0f;
                if (SkillData.BuffChance != null && SkillData.BuffChance.Length > i) buffChance = SkillData.BuffChance[i];
                if (buffChance == 0 || Battle.Random.NextDouble() < buffChance)
                {
                    int buffId = SkillData.Buffs[i];
                    target.AddBuff(buffId, this, i);
                }
            }
        }
    }
    protected virtual void removeBuff(Unit target)
    {
        if (SkillData.BuffRemoves != null)
            foreach (var buffId in SkillData.BuffRemoves)
            {
                var buff = target.Buffs.Find(x => x.Id == buffId);
                if (buff != null) buff.Finish();
            }
    }
    #endregion

    public virtual void FindTarget()
    {
        //Debug.Log("开始获取目标");
        if (showRange)
        {
            HideUnitAttackArea();
            //Debug.Log("展示");
            ShowUnitAttackArea();
        }
        Targets.Clear();
        Targets.AddRange(GetAttackTarget());
        //Debug.Log(Targets.First().Position);
    }

    protected List<Unit> tempTargets = new List<Unit>();
    protected List<Unit> tempTargetsFromEvent = new List<Unit>();
    protected List<Unit> tempTargetsFromAttackRange = new List<Unit>();
    public List<Unit> GetAttackTarget()
    {
        tempTargets.Clear();
        tempTargetsFromEvent.Clear();
        tempTargetsFromAttackRange.Clear();
        if (SkillData.UseEventUser && Battle.TriggerDatas.Count > 0)
        {
            //正在事件当中，技能去取事件目标
            var t = Battle.TriggerDatas.Peek().User;
            if (t != null && CanUseTo(t))
                tempTargetsFromEvent    .Add(t);
        }
        if (SkillData.UseEventTarget && Battle.TriggerDatas.Count > 0)
        {
            //正在事件当中，技能去取事件目标
            //Debug.Log("正在事件"+ Battle.TriggerDatas.Peek().ToString() +"当中");
            var t = Battle.TriggerDatas.Peek().Target;
            if (t != null && CanUseTo(t))
                tempTargetsFromEvent.Add(t);
        }
        //仅自己的情况下 优化一下
        if (tempTargets.Count == 0 && SkillData.TargetFilter == SkillTargetFilterEnum.仅自己)
        {
            tempTargets.Add(Unit);
            return tempTargets;
        }
        //if (!SkillData.UseEventTarget && !SkillData.UseEventUser)
        //{
        if (AttackPoints == null && !SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
        {
            tempTargetsFromAttackRange.AddRange(Battle.FindAll(Unit.Position2, SkillData.AttackRange * Unit.AttackRange, SkillData.TargetTeam, !SkillData.DeadFind));
        }
        else
        {
            var attackPoints = SkillData.AttackAreaWithMain ? Unit.GetNowAttackSkill().AttackPoints : AttackPoints;
            tempTargetsFromAttackRange.AddRange(Battle.FindAll(attackPoints, SkillData.TargetTeam, !SkillData.DeadFind));
        }

        if (tempTargetsFromEvent.Count > 0 && tempTargetsFromAttackRange.Count > 0)
            tempTargets.AddRange(tempTargetsFromAttackRange.FindAll(x => tempTargetsFromEvent.Contains(x)));
        else
        {
            tempTargets.AddRange(tempTargetsFromEvent);
            tempTargets.AddRange(tempTargetsFromAttackRange);
        }
        if (SkillData.SkillCondition != "" && SkillData.SkillCondition != null && Casting.Finished())
        {
            filter.SetTargets(tempTargets);
            //tempTargets.Clear();
            tempTargets = filter.FilterTargets(SkillData.SkillCondition);
            //Targets.Clear();
            //Targets.AddRange(tempTargets);
        }

        orderTargets(tempTargets);
        //if (tempTargets.Count > 0)
        //    Debug.Log("获取到目标：" + tempTargets.First().Position);
        return tempTargets;
    }

    protected void orderTargets(List<Unit> targets)
    {
        targets.RemoveAll(x => !CanUseTo(x));
        if (targets.Count > 0)
        {
            //首先计算出所有目标的仇恨优先级，然后再选出攻击个数的实际目标
            SortTarget(targets);
            FilterTarget(targets);
        }
    }

    protected virtual void SortTarget(List<Unit> targets)
    {
        targets.RemoveAll(OrderFilter);
        var l = targets.OrderBy(GetSortOrder1).ThenBy(GetSortOrder2).ToList();
        targets.Clear();
        targets.AddRange(l);
    }

    protected virtual bool OrderFilter(Unit unit)
    {
        if (SkillData.IfHeal) return unit.Hp == unit.MaxHp;
        //switch (SkillData.AttackOrder)
        //{
        //    case AttackTargetOrderEnum.血量升序:
        //    case AttackTargetOrderEnum.血量未满随机:
        //    case AttackTargetOrderEnum.血量比例升序:
        //        return unit.Hp == unit.MaxHp;
        //    default:
        //        break;
        //}
        return false;
    }

    protected virtual float GetSortOrder1(Unit unit)
    {
        float result = 0;
        switch (SkillData.AttackOrder2)
        {
            case AttackTargetOrder2Enum.近身:
                if (unit is Units.干员 u)
                {
                    if (u.StopUnits.Contains(Unit))
                    {
                        result = -1;
                    }
                }
                break;
            case AttackTargetOrder2Enum.飞行:
                result = -unit.Height;
                break;
            case AttackTargetOrder2Enum.远程:
                result = unit.Skills.Count == 0 ? 0 : -unit.FirstSkill.SkillData.AttackRange;
                Debug.Log($"{unit.UnitData.Id} , {result}");
                break;
            case AttackTargetOrder2Enum.Buff:
                break;
            case AttackTargetOrder2Enum.Tag:
                //firstOrder = x => x.Config.Tags == null ? 0 : -x.Config.Tags.Count();
                break;
            case AttackTargetOrder2Enum.召唤物:
                result = ((Unit as Units.干员).Children.Contains(unit) || unit == Unit) ? 0 : 1;
                break;
            default:
                break;
        }
        return result;
    }

    protected virtual float GetSortOrder2(Unit x)
    {
        if (!string.IsNullOrEmpty(SkillData.OrderTag) && x.UnitData.Tags != null && x.UnitData.Tags.Contains(SkillData.OrderTag)) return -1000000;
        float result = 0;
        switch (SkillData.AttackOrder)
        {
            case AttackTargetOrderEnum.无:
                break;
            case AttackTargetOrderEnum.终点距离:
                result = (x as Units.敌人).distanceToFinal();
                break;
            case AttackTargetOrderEnum.血量升序:
                result = x.Hp;
                break;
            case AttackTargetOrderEnum.血量降序:
                result = -x.Hp;
                break;
            case AttackTargetOrderEnum.血量比例升序:
                result = x.Hp / x.MaxHp;
                break;
            case AttackTargetOrderEnum.血量比例降序:
                result = -x.Hp / x.MaxHp;
                break;
            case AttackTargetOrderEnum.放置降序:
                if (x is Units.干员)
                    result = -(x as Units.干员).InputTime;
                break;
            case AttackTargetOrderEnum.区域顺序:
                result = Math.Abs(x.Position2.x - Unit.Position2.x) + Math.Abs(x.Position2.y - Unit.Position2.y);
                break;
            case AttackTargetOrderEnum.防御降序:
                result = -x.Defence;
                break;
            case AttackTargetOrderEnum.防御升序:
                result = x.Defence;
                break;
            case AttackTargetOrderEnum.攻击力升序:
                result = x.Attack;
                break;
            case AttackTargetOrderEnum.攻击力降序:
                result = -x.Attack;
                break;
            case AttackTargetOrderEnum.最大血量升序:
                result = x.MaxHp;
                break;
            case AttackTargetOrderEnum.最大血量降序:
                result = -x.MaxHp;
                break;
            case AttackTargetOrderEnum.自身距离升序:
                result = (x.Position - Unit.Position).magnitude;
                break;
            case AttackTargetOrderEnum.自身距离降序:
                result = -(x.Position - Unit.Position).magnitude;
                break;
            case AttackTargetOrderEnum.血量未满随机:
                result = Battle.Random.Next(0, 1000);
                break;
            case AttackTargetOrderEnum.重量升序:
                result = x.Weight;
                break;
            case AttackTargetOrderEnum.重量降序:
                result = -x.Weight;
                break;
            case AttackTargetOrderEnum.随机:
                result = Battle.Random.Next(0, 1000);
                break;
            case AttackTargetOrderEnum.隐身优先:
                result = x.IfHide ? 0 : 1;
                break;
            case AttackTargetOrderEnum.未眩晕优先:
                result = x.State == StateEnum.Stun ? 1 : 0;
                break;
            case AttackTargetOrderEnum.飞行优先:
                result = -x.Height;
                break;
            case AttackTargetOrderEnum.未阻挡优先:
                result = (x as Units.敌人).StopUnit == null ? 0 : 1;
                break;
            case AttackTargetOrderEnum.沉睡优先:
                throw new Exception();
                break;
            case AttackTargetOrderEnum.无抵抗优先:
                throw new Exception();
            case AttackTargetOrderEnum.元素损伤升序:
                result = x.InjurePoint;
                break;
        }
        return result + x.Hatred();
    }

    protected virtual void FilterTarget(List<Unit> targets)
    {
        if (SkillData.DamageCount != 0)
        {
            int targetCount = GetTargetCount();
            for (int i = targets.Count() - 1; i >= targetCount; i--)
            {
                targets.RemoveAt(i);
            }
        }
    }

    protected virtual int GetTargetCount()
    {
        int result = SkillData.DamageCount;
        foreach (var modify in Modifies)
        {
            if (modify is ITargetModify targetModify)
            {
                result = targetModify.Modify(result);
            }
        }
        return result;
    }

    public void UpdateAttackPoints()
    {
        if (AttackPoints == null) return;
        AttackPoints.Clear();
        foreach (var p in SkillData.AttackPoints)
        {
            var point = Unit.PointWithDirection(p);
            if (point.x < 0 || point.x >= Battle.Map.Tiles.GetLength(0) || point.y < 0 || point.y >= Battle.Map.Tiles.GetLength(1)) continue;
            AttackPoints.Add(point);
        }
    }

    public virtual void BreakCast()
    {
        Targets.Clear();
        if (Unit.AttackingSkill == this)
        {
            Unit.AttackingSkill = null;
            Unit.AttackingAction.Finish();
        }
        Unit.UnitModel?.BreakAnimation();
        Casting.Finish();
        Bursting.Finish();
        if (SkillData.ReadyType == SkillReadyEnum.充能释放)
        {
            Opening.Finish();
        }
        BurstCount = -1;
        IsBursting = false;
    }

    protected virtual void OnOpenEnd()
    {
        Unit.OverWriteAnimation = null;
        if (SkillData.UpgradeSkill != null)
        {
            DoUpgrade(SkillData.UpgradeSkill.Value);
        }
        if (LoopStartEffect != null)
        {
            EffectManager.Instance.ReturnEffect(LoopStartEffect);
            LoopStartEffect = null;
        }
        if (Unit.AttackingSkill == this && SkillData.OverwriteAnimation != null)
        {
            Unit.AttackingAction.Finish();
        }
    }

    protected virtual void OnAttack(Unit target)
    {
        Battle.TriggerDatas.Push(new TriggerData()
        {
            Target = target,
            Skill = this,
        });
        Unit.Trigger(TriggerEnum.攻击);
        Battle.TriggerDatas.Pop();
    }

    protected virtual void OnBeAttack(Unit target)
    {
        //Debug.Log(target.UnitData.Name +"被攻击");
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = Unit,
            Target = target,
            Skill = this,
        });
        target.Trigger(TriggerEnum.被击);
        Battle.TriggerDatas.Pop();
    }

    public virtual void _OnBeAttack(Unit target)
    {
        //Debug.Log(target.UnitData.Name +"被攻击");
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = Unit,
            Target = target,
            Skill = this,
        });
        target.Trigger(TriggerEnum.被击);
        Battle.TriggerDatas.Pop();
    }

    protected virtual void OnBeAvoid(Unit target)
    {
        foreach (var skill in target.Skills)
        {
            if (skill.SkillData.PowerType == PowerRecoverTypeEnum.闪避)
            {
                skill.RecoverPower(1);
            }
        }
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = Unit,
            Target = target,
            Skill = this,
        });
        target.Trigger(TriggerEnum.闪避);
        Battle.TriggerDatas.Pop();
    }

    protected virtual void OnBeHeal(Skill source)
    {
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = source.Unit,
            Target = this.Unit,
            Skill = source,
        });
        Unit.Trigger(TriggerEnum.被治疗);
        Battle.TriggerDatas.Pop();
    }

    protected virtual void OnHeal(Unit target)
    {
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = Unit,
            Target = target,
            Skill = this,
        });
        target.Trigger(TriggerEnum.治疗);
        Battle.TriggerDatas.Pop();
    }
    protected virtual void DoLifeSteal(DamageInfo damageInfo)
    {
        if (SkillData.LifeSteal == 0 || damageInfo.Avoid) return;
        float healCount = damageInfo.FinalDamage;
        if (healCount < damageInfo.Attack) healCount = damageInfo.Attack;
        Unit.Heal(new DamageInfo()
        {
            Attack = healCount * SkillData.LifeSteal,
            Target = Unit,
            Source = this,
        }, false);
        OnBeHeal(this);
        OnHeal(Unit);
    }

    protected virtual void OnSkillOpen()
    {
        Battle.TriggerDatas.Push(new TriggerData()
        {
            Skill = this,
        });
        Unit.Trigger(TriggerEnum.释放技能);
        //if (Unit is Units.干员 u)
        //    foreach (var unit in u.Children)
        //    {
        //        unit.Trigger(TriggerEnum.释放技能);
        //    }
        Battle.TriggerDatas.Pop();
    }

    //protected DamageInfo GetDamageInfo(Unit target, float damageRate = 1, bool fixedDamage = false)
    protected DamageInfo GetDamageInfo(Unit target, float damageRate = 1)
    {
        var cooldown = SkillData.Cooldown;
        if (cooldown < SystemConfig.DeltaTime) cooldown = SystemConfig.DeltaTime;
        var result = new DamageInfo()
        {
            Target = target,
            AllCount = tempTargets.Count,
            Source = this,
            DamageRate = damageRate * SkillData.DamageRate * (SkillData.DamageWithFrameRate ? cooldown : 1),
            DamageType = SkillData.DamageType,
            MinDamageRate = Unit.UnitData.MinDamageRate,
        };
        switch (SkillData.DamageBase)
        {
            case 0:
                result.Attack = Unit.Attack;
                break;
            case 1:
                result.Attack = target.MaxHp;
                break;
            case 2:
                result.Attack = result.DamageRate;
                result.DamageRate = 1;
                break;
        }
        //Debug.Log(result.Attack);
        foreach (var buff in Unit.Buffs)
        {
            if (buff is ISelfDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }
        foreach (var buff in target.Buffs)
        {
            if (buff is IDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }
        foreach (var modify in Modifies)
        {
            if (modify is IDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }
        return result;
    }

    //<summary>
    //待优化
    //</summary>
# region
    public DamageInfo _GetLogosDamageInfo(Unit target, float fixedDamage, float damageRate = 1)
    {
        var cooldown = SkillData.Cooldown;
        if (cooldown < SystemConfig.DeltaTime) cooldown = SystemConfig.DeltaTime;

        var result = new DamageInfo()
        {
            Target = target,
            AllCount = tempTargets.Count,
            Source = this,
            DamageRate = damageRate * (SkillData.DamageWithFrameRate ? cooldown : 1),
            DamageType = SkillData.DamageType,
            MinDamageRate = Unit.UnitData.MinDamageRate,
            Attack = fixedDamage // 直接使用传入的基础伤害
        };

        // 应用各种伤害修正
        foreach (var buff in Unit.Buffs)
        {
            if (buff is ISelfDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }
        foreach (var buff in target.Buffs)
        {
            if (buff is IDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }
        foreach (var modify in Modifies)
        {
            if (modify is IDamageModify damageModify)
            {
                damageModify.Modify(result);
            }
        }

        return result;
    }
#endregion

    public void DoUpgrade(int skillId)
    {
        Debug.Log($"{SkillData.Id} 升级为 id:{Database.Instance.Get<SkillData>(skillId).Id}");
        if (StartId == -1) StartId = Id;
        Id = skillId;
        Modifies.Clear();
        if (SkillData.Modifys != null)
        {
            for (int i = 0; i < SkillData.Modifys.Length; i++)
            {
                Modifies.Add(ModifyManager.Instance.Get(SkillData.Modifys[i], this));
            }
        }
        else
        {

        }

        if (SkillData.AttackPoints != null)
        {
            AttackPoints = new List<Vector2Int>();
            UpdateAttackPoints();
        }

        Power = SkillData.StartPower;
        MaxPowerBase = SkillData.MaxPower;
        PowerCount = SkillData.PowerCount;
        Reset();
    }
    public virtual void Finish()
    {
        if (ReadyEffect != null)
        {
            EffectManager.Instance.ReturnEffect(ReadyEffect);
            ReadyEffect = null;
        }
        if (LoopStartEffect != null)
        {
            EffectManager.Instance.ReturnEffect(LoopStartEffect);
            LoopStartEffect = null;
        }
        if (LoopCastEffect != null)
        {
            EffectManager.Instance.ReturnEffect(LoopCastEffect);
            LoopCastEffect = null;
        }
        if (showRange)
            HideUnitAttackArea();
    }
    public void ShowUnitAttackArea()
    {
        if (AttackPoints.Count > 0)
        {
            //Debug.LogWarning("ShowAttackArea");
            foreach (var tile in AttackPoints)
            {
                var grid = Battle.Map.Tiles[tile.x, tile.y];
                //Debug.Log(tile.x + " " + tile.y);
                //grid.MapGrid.go.transform.localPosition = new Vector3(0, 0.5f, 0);
                if (grid == null || grid.MapGrid == null) continue;
                var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowRange");
                GameObject go = UnityEngine.Object.Instantiate(tileAsset);
                go.transform.SetParent(grid.MapGrid.transform);
                go.transform.localPosition = new Vector3(0, grid.FarAttackGrid ? -0.25f : 0.15f, 0);
                ShowRange showRange =go.GetComponent<ShowRange>();
                showRange.targetObject = grid.MapGrid.gameObject;
                showRange.unitUniqueIndex = Battle.AllUnits.IndexOf(Unit);
                showRange.useGridPos = Unit is not Units.敌人;
                showRange.unitGridPos = tile;
                showRange.unitWorldPos = new Vector2(Unit.Position.x%1 + tile.x, Unit.Position.z%1 + tile.y);
                showRange.colorHex = SkillData.Data.GetStr("Color", "#6385FF");
                showRange.alpha = SkillData.Data.GetFloat("Alpha", 1.0f);
                //showRange.rangeRadius = SkillData.AttackRange;
                showRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();
                showRange.Init();
                //go.IfHeal(ifHeal);

                tiles.Add(go);
            }
        }
        if (SkillData.AttackRange > 0)
        {
            var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowRange");
            GameObject go = UnityEngine.Object.Instantiate(tileAsset);
            go.transform.SetParent(Unit.NowGrid.MapGrid.transform);
            go.transform.localPosition = new Vector3(0, Battle.Map.Tiles[Unit.NowGrid.X, Unit.NowGrid.Y].FarAttackGrid ? -0.25f : 0.15f, 0);
            ShowRange showRange = go.GetComponent<ShowRange>();
            showRange.targetObject = Battle.Map.Tiles[Unit.NowGrid.X, Unit.NowGrid.Y].MapGrid.gameObject;
            showRange.unitUniqueIndex = Battle.AllUnits.IndexOf(Unit);
            showRange.useGridPos = Unit is not Units.敌人;
            showRange.unitGridPos = Unit.GridPos;
            showRange.unitWorldPos = new Vector2(Unit.Position.x, Unit.Position.z);
            showRange.colorHex = SkillData.Data.GetStr("Color", "#6385FF");
            showRange.alpha = SkillData.Data.GetFloat("Alpha", 1.0f);
            showRange.rangeRadius = SkillData.AttackRange;
            //showRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();    
            showRange.Init();
        }
    }
    public void HideUnitAttackArea()
    {
        foreach (var go in tiles)
        {
            UnityEngine.Object.Destroy(go);
        }
        tiles.Clear();
    }
    public void CopyState(Skill skill)
    {
        if (skill == null) return;

        // 基本属性
        //this.Unit = skill.Unit;
        this.Parent = skill.Parent;
        this.Modifies = new List<Modify>(skill.Modifies);
        this.Targets = new List<Unit>(skill.Targets);
        this.AttackPoints = new List<Vector2Int>(skill.AttackPoints);
        this.Power = skill.Power;
        this.PowerCount = skill.PowerCount;
        this.MaxPowerBase = skill.MaxPowerBase;
        this.StartId = skill.StartId;
        this.Id = skill.Id;
        this.UseCount = skill.UseCount;
        this.IsCantOpen = skill.IsCantOpen;
        this.IsCantUse = skill.IsCantUse;
        this.IsCantCast = skill.IsCantCast;
        this.IsCantCastCount = skill.IsCantCastCount;
        this.IsCantBurst = skill.IsCantBurst;
        this.IsCantLoop = skill.IsCantLoop;
        this.IsNormalAttack = skill.IsNormalAttack;
        this.showRange = skill.showRange;
        this.showBar = skill.showBar;
        this.canStop = skill.canStop;

        // 计时器
        this.Cooldown.Set(skill.Cooldown.value);
        this.Casting.Set(skill.Casting.value);
        this.Bursting.Set(skill.Bursting.value);
        this.Opening.Set(skill.Opening.value);
        this.LoopingStart.Set(skill.LoopingStart.value);
        this.LoopingEnd.Set(skill.LoopingEnd.value);
        this.Waiting.Set(skill.Waiting.value);

        // 效果
        this.ReadyEffect = skill.ReadyEffect;
        this.LoopStartEffect = skill.LoopStartEffect;
        this.LoopCastEffect = skill.LoopCastEffect;

        // 其他属性
        this.Destroyed = skill.Destroyed;
        //this.SkillData = skill.SkillData;;

        // 临时变量
        //this.tempTargets = new List<Unit>(skill.tempTargets);
        //this.tiles = new List<GameObject>(skill.tiles);
    }

}

