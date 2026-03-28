using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Units;
using UnityEngine;
using Spine;
using Spine.Unity;
using static UnityEngine.GraphicsBuffer;

public class Unit
{
    public static string[] StartAnimation = new string[] { "Start" };
    public static string[] DieAnimation = new string[] { "Die" };
    public Battle Battle;
    public UnitData UnitData => Database.Instance.Get<UnitData>(Id);
    public int Id;

    public int index;

    public UnitModel UnitModel;
    public BattleUI.UI_BattleUnit uiUnit;

    public CountDown beAttacked = new(0.0f);

    public Vector3 Position;

    /// <summary>
    /// 单位是第几帧进入场地的,-1表示未放置
    /// </summary>
    public int InputTime = -1;

    public float Height;
    
    public Vector2 Position2 => new(Position.x, Position.z);

    public Vector2Int GridPos => new(Mathf.RoundToInt(Position.x), Mathf.RoundToInt(Position.z));

    public Vector2 Direction = new(1, 0);

    public Tile NowGrid => Battle.Map.Tiles[Mathf.RoundToInt(Position.x), Mathf.RoundToInt(Position.z)];

    public StateEnum State = StateEnum.Default;
    public float Hp;
    //public float HpDp, HpDt, HpFp, HpFt;
    public Unit Parent;
    public List<Unit> Children = new List<Unit>();

    public List<Skill> Skills = new();
    public List<Skill> ElementOutBreak = new();
    public Skill MainSkill;

    public List<Buff> Buffs = new();
    public List<IDamageRewrite> DamageRewrites = new();
    public List<int> IgnoreBuffs = new();

    public float MaxHp;
    public float HpBase, HpAdd, HpRate, HpAddFin, HpRateFin;
    /// <summary>
    /// 攻速
    /// </summary>
    public float Agi;
    public float AgiBase, AgiAdd, AgiRate, AgiAddFin, AgiRateFin;
    /// <summary>
    /// 移速
    /// </summary>
    public float Speed;
    public float SpeedBase, SpeedRate, SpeedAdd;

    public float AttackGap;
    public float AttackGapBase, AttackGapAdd, AttackGapRate;

    public float Attack;
    public float AttackBase,AttackRate, AttackAdd, AttackRateFin, AttackAddFin;

    public float Defence;
    public float DefenceBase, DefenceRate, DefenceAdd, DefenceRateFin, DefenceAddFin;

    public float MagicDefence;
    public float MagicDefenceBase, MagicDefenceRate, MagicDefenceAdd, MagicDefenceRateFin, MagicDefenceAddFin;

    public float AllBlock,Block, MagBlock;

    public float PowerSpeed, PowerSpeedAdd;

    public float HpRecoverP;
    public float HpRecover;
    public float HpRecoverBase, HpRecoverAdd, HpRecoverRate;

    public float ElementBreakRecoverRate;

    public int Team;

    public int Weight;
    public float WeightBase, WeightAdd;

    public float SkillCost;
    public float SkillCostAdd;

    public float Resist;
    public float ResistAdd;

    public float AttackRange;
    public float AttackRangeAdd, AttackRangeRate;

    public int StopCount;
    public float StopCountAdd;

    public bool IfHide;
    public bool IfHideAnti;
    protected bool hideBase;

    public bool CanAttack;
    public bool CanStopOther;
    public float Hatre;

    public float RewriteDamage;

    public bool IfAlive = true;

    public bool IfSleep = false;
    public bool IfSelectable = true;//能否被技能指定为目标
    public bool CanBeHeal = false;
    public List<int> HealOnly = new();//绝食状态下，依旧可被这些单位治疗

    //public int IsCantCastCount = 0;
    //public bool IsCantCast = false;

    public float DamageReceiveRate, MagicDamageReceiveRate, HealReceiveRate, NormalDamageReceiveRate, ElementDamageReceiveRate;

    public float PushPower;

    public CountDown Start = new();//入场
    /// <summary>
    /// 攻击动画
    /// </summary>
    public CountDown AttackingAction = new();

    public Skill FirstSkill;
    public Skill AttackingSkill;

    public float InjurePoint;
    public Dictionary<string, float> EleInjures = new();
    public CountDown ElementProtect = new();
    public float ElementProtectMax = 0.1f;

    public CountDown LifeTime;
    /// <summary>
    /// 死亡动画
    /// </summary>
    public CountDown Dying = new();
    /// <summary>
    /// 硬直
    /// </summary>
    //public CountDown Recover = new CountDown();

    public List<Units.敌人> StopUnits = new();

    public bool IfStun;

    public float ScaleX = -1;
    public float TargetScaleX = -1;

    public string[] AnimationName;
    public string[] OverWriteAnimation;
    public string[] OverWriteIdle;
    public string[] OverWriteMove;
    public string[] OverWriteDie;
    public bool CanChangeAnimation = true;
    public float AnimationSpeed = 1;
    //public Dictionary<string, (float, float, float)> progressBarData = new Dictionary<string, (float, float, float)>();
    private bool isRefreshing = false;

    public virtual void Init()
    {
        baseAttributeInit();
        AnimationName = UnitData.DefaultAnimation;
        if (UnitData.IgnoreBuff != null) IgnoreBuffs.AddRange(UnitData.IgnoreBuff);
        Team = UnitData.Team;
        //foreach (var golbalskill in Database.Instance.globalSkills)
        //{
        //    LearnSkill(golbalskill);
        //}
        // 确保 UnitData.Skills 不为 null
        List<int> skillsList = UnitData.Skills != null ? UnitData.Skills.ToList() : new List<int>();

        // 添加全局技能
        skillsList.AddRange(Database.Instance.globalSkills);

        // 将列表转换回数组并赋值给 UnitData.Skills
        UnitData.Skills = skillsList.ToArray();
        if (UnitData.Skills != null)
            for (int i = 0; i < UnitData.Skills.Length; i++)
            {
                int skillId = UnitData.Skills[i];
                var skill= LearnSkill(skillId);
                if (i == 0) FirstSkill = skill;
                if (skill.SkillData.Data?.GetStr("ElementType") is not null)
                {
                    ElementOutBreak.Add(skill);
                }
            }
        if (UnitData.LifeTime != 0) LifeTime=new CountDown(UnitData.LifeTime);
        CreateModel();
        //Log.Debug("载入模型");
        Refresh();
        Hp = MaxHp;
    }

    public virtual void baseAttributeInit()
    {
        SpeedBase = UnitData.Speed;
        HpBase = UnitData.Hp + UnitData.HpEx;
        AttackBase = UnitData.Attack + UnitData.AttackEx;
        DefenceBase = UnitData.Defence + UnitData.DefenceEx;
        MagicDefenceBase = UnitData.MagicDefence + UnitData.MagicDefenceEx;
        WeightBase = UnitData.Weight;
        PowerSpeed = 1f;
        AgiBase = 100 + UnitData.ExAgi;
        AttackGapBase = UnitData.AttackGap;
        ElementBreakRecoverRate = 1f;
        EleInjures = new Dictionary<string, float>();
        Height = UnitData.Height;
        if (Battle.MapData.UnitOvDatas != null)
        {
            var ovInfo = Battle.MapData.UnitOvDatas.Find(x => x.UnitId == UnitData.Id);
            if (ovInfo != null)
            {
                HpBase = ovInfo.Hp;
                AttackBase = ovInfo.Atk;
                DefenceBase = ovInfo.Def;
                MagicDefenceBase = ovInfo.MagDef;
                AgiBase += ovInfo.Agi;               
                if (ovInfo.Speed != 0)
                    SpeedBase = ovInfo.Speed;
            }
        }
    }

    public virtual void Refresh()
    {
        if (isRefreshing) return; // 防止递归调用

        isRefreshing = true;

        try
        {
            float hpRate = Hp/MaxHp;
            PushPower = 0;
            SpeedAdd = SpeedRate = 0;
            HpAdd = HpRate = HpAddFin = HpRateFin = 0;
            AttackAdd = AttackRate = AttackAddFin = AttackRateFin = 0;
            MagicDefenceAdd = MagicDefenceRate = MagicDefenceAddFin = MagicDefenceRateFin = 0;
            DefenceAdd = DefenceRate = DefenceAddFin = DefenceRateFin = 0;
            AgiAdd = AgiRate = AgiAddFin = AgiRateFin = 0;
            HpRecoverP = 0;
            HpRecoverBase = UnitData.HpRecover;
            HpRecoverAdd = 0;
            WeightAdd = 0;
            AttackGapAdd = AttackGapRate = 0;
            Block = MagBlock = 0;
            SkillCostAdd = 0;
            PowerSpeedAdd = 0;
            CanAttack = true;
            CanBeHeal = true;
            ResistAdd = 0;
            AttackRangeAdd = AttackRangeRate = 0;
            DamageReceiveRate = MagicDamageReceiveRate = HealReceiveRate = NormalDamageReceiveRate = ElementDamageReceiveRate = 0;
            StopCountAdd = 0;
            HpRecoverRate = 0;
            ElementBreakRecoverRate = 1f;
            Hatre = UnitData.Hatred;
            foreach (var buff in Buffs)
            {
                if (buff.Enable()) buff.ApplyToUnit();
            }
            StopCount = UnitData.StopCount + (int)StopCountAdd;
            Speed = (SpeedBase + SpeedAdd) * (1 + SpeedRate) / 2;
            if (Speed < SpeedBase * 0.1f) Speed = SpeedBase * 0.1f;
            MaxHp = ((HpBase + HpAdd) * (1 + HpRate) + HpAddFin) * (1 + HpRateFin);
            Hp = MaxHp * hpRate;
            Attack = ((AttackBase + AttackAdd) * (1 + AttackRate) + AttackAddFin) * (1 + AttackRateFin);
            if (Attack < 0) Attack = 0;
            Defence = ((DefenceBase + DefenceAdd) * (1 + DefenceRate) + DefenceAddFin) * (1 + DefenceRateFin);
            if (Defence < 0) Defence = 0;
            MagicDefence = ((MagicDefenceBase + MagicDefenceAdd) * (1 + MagicDefenceRate) + MagicDefenceAddFin) * (1 + MagicDefenceRateFin);
            if (MagicDefence < 0) MagicDefence = 0;
            //if (MagicDefence > 100) MagicDefence = 100;
            HpRecover = (HpRecoverBase + HpRecoverAdd);
            if (HpRecover > 0) HpRecover = HpRecover * (1 + HpRecoverRate);
            Agi = ((AgiBase + AgiAdd) * (1 + AgiRate) + AgiAddFin) * (1 + AgiRateFin);
            if (Agi < 10f) Agi = 10f;
            Weight = (int)(WeightBase + WeightAdd);
            AttackGap = (AttackGapBase + AttackGapAdd) * (1 + AttackGapRate);
            if (AttackGap < 0.1f) AttackGap = 0.1f;
            SkillCost = SkillCostAdd + 1;
            PowerSpeed = PowerSpeedAdd + 1;
            Resist = ResistAdd;
            AttackRange = (1 + AttackRangeAdd) * (1 + AttackRangeRate);
            //UnitModel.ResetColor();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    public void UpdateBuffs()
    {
        //UpdateBuffSuppression();
        updateElement();
        if (!Alive()) return;
        if (Hp > MaxHp) Hp = MaxHp;
        IfHide = hideBase;
        IfHideAnti = false;
        IfSleep = false;
        IfSelectable = true;
        CanStopOther = true;
        bool lastIfStun = IfStun;
        IfStun = false;
        foreach (var buff in Buffs.Reverse<Buff>())
        {
            buff.Update();
        }
        Refresh();

        if (unbalance) IfStun = true;
        if (lastIfStun && !IfStun)
        {
            SetStatus(StateEnum.Idle);
        }
        if (IfHideAnti || IfStoped()) IfHide = false;
        foreach (var buff in Buffs.Reverse<Buff>())//计算完单位属性后，有些buff要更新显示状态
        {
            buff.UpdateView();
        }
    }
    public virtual void UpdateAction()
    {

        //UpdateBuffSuppression();

        if (Alive())
        {
            //HP自动回复
            Hp += HpRecover * SystemConfig.DeltaTime;
            Hp += HpRecoverP * MaxHp * SystemConfig.DeltaTime;
            if (Hp > MaxHp) Hp = MaxHp;
            if (Hp < 0) DoDie(null);
            if (!UnitModel.isOriginalColor() && beAttacked.Update(SystemConfig.DeltaTime))
                UnitModel.ResetColor();
        }
    }

    protected void UpdateDie()
    {
        if (Dying.Update(SystemConfig.DeltaTime))
        {
            Finish();
        }
    }

    public virtual void DoDie(object source)
    {
        IfAlive = false;
        CanChangeAnimation = true;
        SetStatus(StateEnum.Die);
        Dying.Set(UnitModel?.GetAnimationDuration("Die") ?? 0.01f);

        var box = UnitModel?.GetComponent<BoxCollider>();
        if (box != null) box.enabled = false;

        Unit sourceUnit = null;
        //根据伤害来源，判断击杀事件
        if (source is DamageInfo damageInfo)
        {
            if (damageInfo.Source is Skill skill)
            {
                Battle.TriggerDatas.Push(new TriggerData()
                {
                    Target = this,
                    Skill = skill,
                });
                Debug.Log($"{skill.Unit.UnitData.Id} 击杀了 {UnitData.Id}");
                if (skill.Unit is Units.干员 u && u.Parent != null)//召唤物杀人，算主子击杀
                    sourceUnit = u.Parent;
                else
                    sourceUnit = skill.Unit;
                sourceUnit.Trigger(TriggerEnum.击杀);
                Battle.TriggerDatas.Pop();
            }
        }

        //死亡事件
        Battle.TriggerDatas.Push(new TriggerData()
        {
            Target = this,
            User = sourceUnit,
        });
        Battle.Trigger(TriggerEnum.死亡);
        Battle.TriggerDatas.Pop();

        if (Dying.Finished()) Finish();
    }

    public virtual void Finish(bool leaveEvent=true)
    {
        foreach (var unit in StopUnits)
        {
            unit.StopUnit = null;
        }
        StopUnits.Clear();
        IfAlive = false;
        if (leaveEvent)
        {
            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = this,
            });
            Trigger(TriggerEnum.离场);
            Battle.TriggerDatas.Pop();
        }

        foreach (var buff in Buffs.ToArray())
        {
            if (!buff.BuffData.DeadRemain)
                buff.Finish();
        }
        foreach (var buff in PushBuffs.ToArray())
        {
            (buff as Buff).Finish();
        }
        foreach (var skill in Skills)
        {
            skill.Finish();
        }
        Refresh();
        PushBuffs.Clear();
    }

    protected void UpdateSkills()
    {
        var inAttack = !AttackingAction.Finished();
        AttackingAction.Update(SystemConfig.DeltaTime);
        //if (inAttack && AttackingAction.Finished())
        //{
        //    Debug.Log("Ready to attack");
        //}
        foreach (var skill in Skills)
        {
            skill.UpdateCooldown();
        }
        for (int i = Skills.Count - 1; i >= 0; i--)
        {
            if (i >= Skills.Count) continue;
            var sk = Skills[i];
            if (sk != null)
            {
                sk.Update();
            }
        }
        foreach (var skill in Skills)
        {
            skill.UpdateOpening();
        }
        if (inAttack && AttackingAction.Finished() && State != StateEnum.Die)
        {
            SetStatus(StateEnum.Idle);
        }
    }

    protected virtual void UpdateMove()
    {

    }

    public void UpdateCollision()
    {
        if (Position.x < Battle.Map.minX - 0.5f) Position.x = Battle.Map.minX - 0.4999f;
        if (Position.z < Battle.Map.minZ - 0.5f) Position.z = Battle.Map.minZ - 0.4999f;
        if (Position.x > Battle.Map.maxX + 0.5f) Position.x = Battle.Map.maxX + 0.4999f;
        if (Position.z > Battle.Map.maxZ + 0.5f) Position.z = Battle.Map.maxZ + 0.4999f;

        if (Height > 0) return;
        var tile = Battle.Map.Tiles[GridPos.x, GridPos.y];

        if (tile.FarAttackGrid)
        {
            float x = Position2.x - GridPos.x;
            float y = Position2.y - GridPos.y;
            bool b1 = y - x > 0;
            bool b2 = x + y < 0;
            if (b1 && b2)
            {
                var t = Battle.Map.Tiles.Get(GridPos.x - 1, GridPos.y);
                if (t == null || t.FarAttackGrid) return;
                Position.x = Mathf.RoundToInt(Position.x) - 0.5001f;
            }
            if (b1 && !b2)
            {
                var t = Battle.Map.Tiles.Get(GridPos.x + 1, GridPos.y);
                if (t == null || t.FarAttackGrid) return;
                Position.z = Mathf.RoundToInt(Position.z) + 0.5001f;
            }
            if (!b1 && b2)
            {
                var t = Battle.Map.Tiles.Get(GridPos.x, GridPos.y-1);
                if (t == null || t.FarAttackGrid) return;
                Position.z = Mathf.RoundToInt(Position.z) - 0.5001f;
            }
            if (!b1 && !b2)
            {
                var t = Battle.Map.Tiles.Get(GridPos.x, GridPos.y + 1);
                if (t == null || t.FarAttackGrid) return;
                Position.x = Mathf.RoundToInt(Position.x) + 0.5001f;
            }
        }
    }

    public void Trigger(TriggerEnum triggerEnum)
    {
        for (int i = Skills.Count-1; i >= 0; i--)
        {
            Skill skill = Skills[i];
            if (triggerEnum == TriggerEnum.被击 && skill.SkillData.PowerType == PowerRecoverTypeEnum.受击)
            {
                skill.RecoverPower(1);
            }
            //if (triggerEnum == TriggerEnum.打数溢出)
            //    Log.Debug("打数溢出");
            if (skill.SkillData.Trigger == triggerEnum)
            {
                if (skill.SkillData.Trigger != TriggerEnum.元素爆发 || skill.SkillData.Trigger != TriggerEnum.自身元素爆发)
                    skill.Start();
                else
                {
                    if (skill.SkillData.Data?.GetStr("ElementType") == Battle.TriggerDatas?.Peek().Skill.SkillData.Data?.GetStr("ElementType") && IfAlive)
                        skill.Start();
                }
            }
        }
    }

    public Skill LearnSkill(int skillId, Skill parent = null)
    {
        var s = Skills.Find(x => x.Id == skillId);
        if (s != null) return s;
        var skillConfig = Database.Instance.Get<SkillData>(skillId);
        var skill = typeof(Unit).Assembly.CreateInstance(nameof(Skills) + "." + skillConfig.Type) as Skill;
        skill.Unit = this;
        skill.Id = skillId;
        try
        {
            skill.Init();
        }
        catch (Exception e)
        {
            Debug.Log(skillConfig.Id+"技能初始化失败");
            TipManager.Instance.ShowTip(skillConfig.Id+"技能初始化失败"+e.Message);
            Log.Error(e);
        }
        if (parent != null) skill.Parent = parent;
        if (Skills.Count > 0 && skillId < Skills.Last().Id)
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Id > skillId)
                {
                    Skills.Insert(i, skill);
                    break;
                }
            }
        }
        else
            Skills.Add(skill);
        if (skillConfig.Skills != null)
            foreach (var id in skillConfig.Skills)
            {
                LearnSkill(id, skill);
            }
        if (skillConfig.ExSkills != null)
            foreach (var id in skillConfig.ExSkills)
            {
                LearnSkill(id, skill);
            }
        return skill;
    }

    #region 入梦砖
    //public void UpdateBuffSuppression()
    //{
    //    // 检查单位是否有"BUFF抵挡"效果
    //    bool hasBuffDefense = Buffs.Any(b => b.BuffData.CancelsCancelableBuffs && !b.IsSuppressed);

    //    //Log.Debug($"单位 {this.UnitData.Id} 是否有BUFF抵挡效果: {hasBuffDefense}");

    //    foreach (var buff in Buffs)
    //    {
    //        // 检查BUFF是否应该被抑制
    //        bool shouldSuppress = hasBuffDefense &&
    //                             buff.IsCancelable &&
    //                             buff.OriginalCaster != null &&
    //                             buff.OriginalCaster.Buffs.Any(b => b.BuffData.MakesBuffsCancelable && !b.IsSuppressed);

    //        // 记录详细信息
    //        if (buff.IsCancelable && buff.OriginalCaster != null)
    //        {
    //            bool casterHasCancelable = buff.OriginalCaster.Buffs.Any(b => b.BuffData.MakesBuffsCancelable && !b.IsSuppressed);
    //            //Log.Debug($"BUFF {buff.Id} 来自单位 {buff.OriginalCaster.UnitData.Id}, 施加者是否有BUFF可抵挡: {casterHasCancelable}");
    //        }

    //        // 更新抑制状态
    //        if (buff.IsSuppressed != shouldSuppress)
    //        {
    //            //Log.Debug($"BUFF {buff.Id} 抑制状态变化: {buff.IsSuppressed} -> {shouldSuppress}");

    //            buff.IsSuppressed = shouldSuppress;

    //            // 状态变化时的处理
    //            if (shouldSuppress)
    //            {
    //                //Log.Debug($"BUFF {buff.Id} 被抑制");
    //                // BUFF刚被抑制，移除其效果
    //                OnBuffSuppressed(buff);
    //            }
    //            else
    //            {
    //                //Log.Debug($"BUFF {buff.Id} 恢复");
    //                // BUFF刚恢复，重新应用效果
    //                OnBuffRestored(buff);
    //            }
    //        }
    //    }
    //}

    //// BUFF被抑制时的处理
    //public void OnBuffSuppressed(Buff buff)
    //{
    //    // 移除BUFF的效果（如果是持续性效果）
    //    // 例如：如果是一个攻击力提升BUFF，需要暂时降低攻击力
    //    Refresh(); // 重新计算属性
    //}

    //// BUFF恢复时的处理
    //public void OnBuffRestored(Buff buff)
    //{
    //    // 恢复BUFF的效果
    //    Refresh(); // 重新计算属性
    //}

    //// 在每帧更新中调用,此处逻辑在UpdateAction()中,见上文

    #endregion

    public Buff AddBuff(int buffId, Skill source, int index, float lastTime = -1.0f)
    {
        if (IgnoreBuffs.Contains(buffId)) return null;

        var config = Database.Instance.Get<BuffData>(buffId);
        
        if (config.RelyBuff != null && !Buffs.Any(x => x.Id == config.RelyBuff.Value))
            return null;

        // 检查是否存在buff的升级版
        var oldBuff = Buffs.FirstOrDefault(x => (x.Id == buffId || config.Upgrade == x.Id) && (config.UnSourceCheck || x.Skill == source));
        if (oldBuff != null)
        {
            oldBuff.Reset();
            Refresh();
            return oldBuff;
        }
        else
        {
            // 创建新的buff实例
            var newBuff = typeof(Buff).Assembly.CreateInstance(nameof(Buffs) + "." + config.Type) as Buff;
            if (newBuff == null)
            {
                TipManager.Instance.ShowTip("创建" + config.Id + "Buff失败, 请检查" + config.Type + "是否存在");
                return null;
            }
            newBuff.Index = index;
            newBuff.Id = buffId;
            newBuff.Skill = source;
            newBuff.Unit = this;
            newBuff.LastTime = lastTime;

            Debug.Log("Add " + config.Id + " Buff to " + UnitData.Name);

            // 添加到BUFF列表
            Buffs.Add(newBuff);
            
            // 如果伤害重写类型，添加到伤害重写列表
            if (newBuff is IDamageRewrite shield)
            {
                DamageRewrites.Add(shield);
                DamageRewrites.Sort((a, b) => a.OrderCode.CompareTo(b.OrderCode));
            }

            // 初始化BUFF
            newBuff.Init();
            Refresh();
            return newBuff;
        }
    }

    public void RemoveBuff(Buff buff)
    {
        Buffs.Remove(buff);
        if (buff is IDamageRewrite shield) DamageRewrites.Remove(shield);
        //Refresh();
    }
    #region 推拉相关
    public List<IPushBuff> PushBuffs = new();

    /// <summary>
    /// 失衡硬直
    /// </summary>
    public CountDown Unbalancing = new();

    public bool Unbalance => unbalance || !Unbalancing.Finished();

    public bool unbalance;

    Vector2 power;

    public virtual void UpdatePush()
    {
        //if (!Alive()) return;
        Unbalancing.Update(SystemConfig.DeltaTime);
        foreach (Buff buff in PushBuffs.Reverse<IPushBuff>())
        {
            buff.Update();
        }
        if (!Unbalance) return;
        //(this as Units.敌人).CheckBlock();
        Vector2 power = Vector2.zero;
        foreach (var buff in PushBuffs.ToList())
        {
            var pushPower= buff.GetPushPower();
            power += pushPower;
        }
        if (power.magnitude < 0.1f && Unbalancing.Finished()) //力太小，失衡状态结束
        {
            unbalance = false;
        }

        if (Unbalance)
        {
            var posChange = power * SystemConfig.DeltaTime;
            Position += new Vector3(posChange.x, 0, posChange.y);
        }
        if (!Unbalance)
        {
            RecoverBalance();
        }
    }

    protected virtual void RecoverBalance()
    {

    }

    public void AddPush(IPushBuff buff)
    {
        (buff as Buff).Unit = this;
        if (PushBuffs.Count == 0)//进入失衡状态
        {
            unbalance = true;
            BreakAllCast();            
            //SetStatus(StateEnum.Stun);
            Unbalancing.Set(0.1f);
        }
        PushBuffs.Add(buff);
    }

    public void RemovePush(IPushBuff buff)
    {
        PushBuffs.Remove(buff);
    }
    #endregion
    public void RecoverPower(float count)
    {
        if (MainSkill != null && !MainSkill.Opening.Finished())
            return;
        foreach (var skill in Skills)
        {
            skill.RecoverPower(count);
        }
    }

    public virtual bool Alive()
    {
        return IfAlive;
    }

    public void SetStatus(StateEnum state)
    {
        //Log.Debug($"{UnitData.Id} 由 {this.State} 转变为 {state}");
        if (this.State == StateEnum.Die && state != StateEnum.Default) return;
        this.State = state;
        if (CanChangeAnimation)
        {
            if (state == StateEnum.Default)
                AnimationName = UnitData.DefaultAnimation;
            else if (state == StateEnum.Idle)
                AnimationName = GetIdleAnimation();
            else if (state == StateEnum.Move)
                AnimationName = GetMoveAnimation();
            else if (state == StateEnum.Start)
                AnimationName = Unit.StartAnimation;
            else if (state == StateEnum.Die)
                AnimationName = GetDieAnimation();
            AnimationSpeed = 1;
            if (state == StateEnum.Stun)
            {
                if (UnitData.StunAnimation != null)
                    AnimationName = UnitData.StunAnimation;
                else
                {
                    //没有眩晕动画的场合，暂停模型动画
                    AnimationSpeed = 0;
                }
            }
        }
        if (state != StateEnum.Attack && !AttackingAction.Finished())
        {
            AttackingAction.Finish();
        }
    }

    public virtual void CreateModel()
    {
        if (string.IsNullOrEmpty(UnitData.Model)) return;
        //Debug.Log(UnitData.Model);
        GameObject go = ResHelper.Instantiate(PathHelper.UnitPath + UnitData.Model);
        if (go == null)
        {
            //Log.Debug(PathHelper.UnitPath + UnitData.Model);
            //Debug.Log(UnitData.Model + " not found");
            if (!SpineImportHelper.Instance.loadedSkeletons.ContainsKey(UnitData.Model))
            {
                SpineData spineData = Database.Instance.Get<SpineData>(UnitData.Model);
                if (spineData is not null)
                {
                    bool hasBack = !spineData.OnlyFront;
                    string pathHead = spineData.UseAppHotfixResPath ? PathHelper.AppHotfixResPath : "";
                    SpineImportHelper.Instance.LoadSpineAssets(spineData.Id, pathHead + spineData.FrontPngPath, pathHead + spineData.FrontAtlasPath, pathHead + spineData.FrontSkelPath);
                    if (hasBack)
                        SpineImportHelper.Instance.LoadSpineAssets(spineData.Id + "_back", pathHead + spineData.BackPngPath, pathHead + spineData.BackAtlasPath, pathHead + spineData.BackSkelPath);
                }
                else
                    TipManager.Instance.ShowTip("模型" + UnitData.Model + "不存在");
            }
            go = SpineImportHelper.Instance.ReplaceSkeletonComponents(UnitData.Model);
            if (go == null)
            {
                Debug.LogError("模型" + UnitData.Model + "加载失败");
                TipManager.Instance.ShowTip("模型" + UnitData.Model + "加载失败");
                return;
            }
        }
        UnitModel = go.GetComponent<UnitModel>();
        UnitModel.Init(this);
    }

    public void Heal(DamageInfo heal,bool ifShowHeal)
    {
        heal.FinalDamage = heal.Attack * heal.DamageRate * (1 + HealReceiveRate);
        Hp += heal.FinalDamage;
        if (ifShowHeal) UnitModel.ShowHeal(heal);
        if (Hp > MaxHp)
        {
            Battle.TriggerDatas.Push(new TriggerData()
            {
                User = heal.GetSourceUnit(),
                Target = this,
                Count = Hp - MaxHp,
            });
            this.Trigger(TriggerEnum.过量治疗);
            heal.GetSourceUnit().Trigger(TriggerEnum.过量治疗);
            Hp = MaxHp;
        }
        Battle.TriggerDatas.Push(new TriggerData()
        {
            User = heal.GetSourceUnit(),
            Target = this,
            //Skill = Heal.Source,
        });
        Trigger(TriggerEnum.被治疗);
        Battle.TriggerDatas.Pop();
        //Debug.Log(UnitData.Name + " 受到" + Heal.GetSourceUnit().UnitData.Name + "的" + Heal.FinalDamage + "点治疗");
    }

    public void Damage(DamageInfo damageInfo)
    {
        //beAttacked.Add(0.5f);
        float damage = damageInfo.Attack * damageInfo.DamageRate;
        if (damageInfo.DamageType == DamageTypeEnum.Normal) damage *= (1+NormalDamageReceiveRate);
        if (damageInfo.DamageType == DamageTypeEnum.Magic) damage *= (1+MagicDamageReceiveRate);
        if (damageInfo.DamageType == DamageTypeEnum.Element) damage *= (1+ElementDamageReceiveRate);
        damage = damageWithDefence(damage, damageInfo.DamageType,damageInfo.DefIgnore, damageInfo.DefIgnoreRate,damageInfo.MinDamageRate);
        //Debug.Log("伤害" + damage);
        damageInfo.FinalDamage = damage * (1+DamageReceiveRate);
        //Debug.Log("结算易伤伤害" + damageInfo.FinalDamage);
        float damageEx = damageInfo.Attack;
        damageEx = damageWithDefence(damageEx, damageInfo.DamageType, 0, 0, damageInfo.MinDamageRate);
        if (damage > damageEx * 1.5f) UnitModel.ShowCrit(damageInfo);
        if (AllBlock > 0 && Battle.Random.NextDouble() < AllBlock) damageInfo.Avoid = true;
        if (damageInfo.DamageType == DamageTypeEnum.Normal && Block > 0 && Battle.Random.NextDouble() < Block) damageInfo.Avoid = true;
        if (damageInfo.DamageType == DamageTypeEnum.Magic && MagBlock > 0 && Battle.Random.NextDouble() < MagBlock) damageInfo.Avoid = true;
        //Debug.Log(damageInfo.FinalDamage);
        if (!damageInfo.Avoid)
        {
            
            foreach (var shield in DamageRewrites.ToArray())
            {
                shield.DamageRewrite(damageInfo);
            }
            //Debug.Log(damageInfo.FinalDamage);
            //Debug.Log(Hp);
            
            Hp -= damageInfo.FinalDamage;
            
            if (Hp <= 0)
            {
                Battle.TriggerDatas.Push(new TriggerData()
                {
                    Target = this,
                });
                Trigger(TriggerEnum.致命);

                Battle.TriggerDatas.Pop();
            }
            

            //Debug.Log(Hp);
            //致命事件过后，如果血量依旧低于0，则判定单位死亡
            if (Hp <= 0)
            {
                Hp = 0;
                DoDie(damageInfo);
            }
            Unit unit = damageInfo.GetSourceUnit();
            if (unit is Units.干员 && damageInfo.GetSourceUnit() != damageInfo.Target)
            {
                //干员 oprator = unit as 干员;
                while (unit.Parent != null)
                {
                    unit = unit.Parent as 干员;
                    //Log.Debug(unit.UnitData.Name);
                }
                //Debug.Log(oprator.UnitData.Id + damageInfo.DamageType.ToString() + "伤害" + damageInfo.FinalDamage);
                OpDamageInfo opDamageInfo = BattleManager.Instance.OpDamageInfos.Find(x => x.UnitId == unit.UnitData.Id);
                if (damageInfo.DamageType == DamageTypeEnum.Normal)
                {
                    opDamageInfo.NomalDamage += damageInfo.FinalDamage;
                    opDamageInfo.TotalDamage += damageInfo.FinalDamage;
                }
                else if (damageInfo.DamageType == DamageTypeEnum.Magic)
                {
                    opDamageInfo.MagicDamage += damageInfo.FinalDamage;
                    opDamageInfo.TotalDamage += damageInfo.FinalDamage;
                }
                else if (damageInfo.DamageType == DamageTypeEnum.Real || damageInfo.DamageType == DamageTypeEnum.Element)
                {
                    opDamageInfo.RealDamage += damageInfo.FinalDamage;
                    opDamageInfo.TotalDamage += damageInfo.FinalDamage;
                }
                else
                    Debug.LogError("未知伤害类型,不计入统计");
            }
            //Debug.Log(unit.UnitData.Id + damageInfo.DamageType.ToString() + "伤害" + damageInfo.FinalDamage);
        }
        //if (!UnitModel.isOriginalColor())
        //    UnitModel.ResetColor();
    }

    float damageWithDefence(float damage,DamageTypeEnum damageType,float defIgnore, float defIgnoreRate,float minDamageRate)
    {
        switch (damageType)
        {
            case DamageTypeEnum.Normal:
                var defence = Mathf.Max(0, Defence * (1 - defIgnoreRate) - defIgnore);
                damage = Mathf.Max(damage * minDamageRate, damage - defence);//抛光系数0.05
                if (damage < 0) damage = 1;
                break;
            case DamageTypeEnum.Magic:
                var magDefence = Mathf.Max(0, Mathf.Min(100, MagicDefence * (1 - defIgnoreRate)) - defIgnore);
                damage = Mathf.Max(damage * minDamageRate, damage * (100 - magDefence) / 100);
                break;
        }
        return damage;
    }

    public Skill GetNowUseingSkill()
    {
        for (int i = Skills.Count - 1; i >= 0; i--)
        {
            if (Skills[i].InUsing())
            {
                return Skills[i];
            }
        }
        if (Skills.Count > 0)
            return Skills[0];
        else return null;
    }
    public Skill GetNowAttackSkill()
    {
        for (int i = Skills.Count - 1; i >= 0; i--)
        {
            if (Skills[i].InUsing() && Skills[i].GetAttackTarget().Count > 0)
            {
                return Skills[i];
            }
        }
        //if (Skills.Count > 0)
            //return Skills[0];
        return null;
    }
    public virtual float Hatred()
    {
        return -Hatre * 100000;
    }

    public void BreakAllCast()
    {
        //AttackingAction.Finish();
        foreach (var skill in Skills)
        {
            skill.BreakCast();
        }
        SetStatus(StateEnum.Idle);
    }

    public virtual bool IfStoped()
    {
        return false;
    }

    public string[] GetAnimation()
    {
        return OverWriteAnimation == null ? AnimationName : OverWriteAnimation;
    }

    public string[] GetMoveAnimation()
    {
        return OverWriteMove == null ? UnitData.MoveAnimation : OverWriteMove;
    }
    public string[] GetIdleAnimation()
    {
        return OverWriteIdle == null ? UnitData.IdleAnimation : OverWriteIdle;
    }
    public string[] GetDieAnimation()
    {
        return OverWriteDie == null ? UnitData.DeadAnimation : OverWriteDie;
    }

    public virtual Vector2Int PointWithDirection(Vector2Int v2)
    {
        return GridPos + v2;
    }
    public virtual float distanceToFinal()
    {
        return 100000;
    }
    public Vector3 GetHitPoint()
    {
        return UnitModel.GetPoint(UnitData.HitPointName);
    }
    public void AddStop(Units.敌人 target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        StopUnits.Add(target);
        target.StopUnit = this;

        var direction = target.Position - Position;
        float requiredDistance = UnitData.Radius + target.UnitData.Radius;

        // 检查是否需要调整位置（距离过近且方向向量有效）
        if (direction.sqrMagnitude < requiredDistance * requiredDistance &&
            direction.sqrMagnitude > 0.001f)
        {
            target.Position = Position + direction.normalized * requiredDistance;
        }
        else if (direction.sqrMagnitude <= 0.001f)
        {
            // 处理位置完全重叠的情况，例如沿默认方向偏移
            target.Position = Position + Vector3.forward * requiredDistance;
        }
    }

    public void RemoveStop(Units.敌人 target)
    {
        StopUnits.Remove(target);
        target.StopUnit = null;
    }

    public bool CanStop(Units.敌人 target)
    {
        if (Height != target.Height) return false;
        if (Team != 0) return false;
        if (!Alive()) return false;
        if (!CanStopOther) return false;
        if (target.UnStopped) return false;
        if (StopUnits.Contains(target)) return true;
        if (target.StopUnit != null) return false;
        if (NowGrid.FarAttackGrid) return false;
        return StopUnits.Sum(x => x.StopCost) + target.StopCost <= StopCount;
    }

    public void ChangeEleInjure(float count, string eleType)
    {
        if (!ElementProtect.Finished()) return;
        //InjurePoint += count;
        if (!EleInjures.ContainsKey(eleType)) EleInjures.Add(eleType, count);
        else EleInjures[eleType] += count;
        //EleInjures[eleType] += count;
        if (EleInjures[eleType] <= 0) EleInjures[eleType] = 0;
    }

    void updateElement()
    {
        InjurePoint = EleInjures.Values.ToArray().Length > 0 ? EleInjures.Values.ToArray().Max() : 0;
        ElementProtect.Update(SystemConfig.DeltaTime);
        var breakedEle = null as string;
        var breakSkill = null as Skill;
        foreach (var ele in EleInjures)
        {
            if (ele.Value >= 1000)
            {
                breakedEle = ele.Key;
                foreach (var skill in ElementOutBreak)
                {
                    //skill.Targets.Add(this);
                    if (skill.SkillData.Data.GetStr("ElementType") == breakedEle)
                    {
                        if (skill.CanUseTo(this))
                        {
                            breakSkill = skill;
                            if (ElementProtect.Finished() || skill.SkillData.Cooldown * ElementBreakRecoverRate > ElementProtectMax)
                                ElementProtect.Set(skill.SkillData.Cooldown * ElementBreakRecoverRate);
                            ElementProtectMax = skill.SkillData.Cooldown * ElementBreakRecoverRate;
                            //Log.Debug(ElementProtectMax);
                        }
                    }
                    //skill.Targets.Remove(this);
                }
                Battle.TriggerDatas.Push(new TriggerData()
                {
                    User = null,
                    Target = this,
                    Skill = breakSkill,
                });
                //Log.Debug("事件:元素爆发");
                if (this.IfAlive)
                    this.Trigger(TriggerEnum.自身元素爆发);
                Battle.Trigger(TriggerEnum.元素爆发);
                Battle.TriggerDatas.Pop();
            }
        }
        if (breakedEle != null) EleInjures[breakedEle] = 0;
    }

    public void GainChild(int id, int mianSkillId = 0)
    {
        var unit = Battle.CreatePlayerUnit(id);
        Children.Add(unit);
        unit.MainSkillId = mianSkillId;
        unit.Init();
        unit.Parent = this;
        unit.UnitModel?.gameObject.SetActive(false);
        BattleUI.UI_Battle.Instance.UpdateUnitsLayout();
    }

    //int GetEnemyStopCost(敌人 enemy)
    //{
    //    return enemy.StopCost;
    //}

    //int GetShieldOrderCount(_IShield shield)
    //{
    //    return -shield.OrderCount;
    //}

    //int GetBuffPriority(Buff buff)
    //{
    //    return -buff.BuffData.OrderCount;
    //}
    /*
    int GetHealPriority(IHeal Heal)
    {
        return -Heal.HealOrderCount;
    }

    int GetSelfHealPriority(ISelfHeal selfHeal)
    {
        return -selfHeal.HealOrderCount;
    }

    int GetSelfAfterDamagePriority(ISelfAfterDamage selfAfterDamage)
    {
        return -selfAfterDamage.OrderCount;
    }

    int GetSelfAfterNonDamagePriority(ISelfAfterWithoutDamage selfAfterWithoutDamage)
    {
        return -selfAfterWithoutDamage.OrderCount;
    }

    int GetElementShieldPriority(IElementShield elementShield)
    {
        return -elementShield.ElementAbsorbOrderCount;
    }

    float GetElementValue(新版元素损伤 elementDamage)
    {
        return elementDamage.ElementValue;
    }
    */
}
