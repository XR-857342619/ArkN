using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Buff
{
    public static List<Vector2Int> Round1 = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
    public BuffData BuffData => Database.Instance.Get<BuffData>(Id);
    public int Id;
    public int Index;
    public float LastTime;

    [System.NonSerialized]
    public Unit Unit;
    public Bullet Bullet;
    public Skill Skill;
    protected Battle Battle => Skill.Unit.Battle;

    public CountDown Duration = new CountDown();

    public Effect LastingEffect;

    public Buff RelayBuff;
    public bool Dead;

    public float isBlocking = -1.0f;

    List<Vector2Int> rounds;

    //入梦砖相关
    public bool CancelsCancelableBuffs { get; set; }
    public bool MakesBuffsCancelable { get; set; } // 这个BUFF是否使施加者施加的BUFF变为可抵挡
    //public Unit SourceUnit { get; set; } // 记录原始施加者
    public Unit SourceUnit => Skill.Unit;

    public virtual void Init()
    {
        updateLastTime();
        //Log.Debug(Unit.UnitData.Name + "获取了buff: " + BuffData.Id + "持续时间" + Duration.value);
        // 持续特效（显示用）
        if (BuffData.LastingEffect.HasValue)
        {
            LastingEffect = EffectManager.Instance.GetEffect(BuffData.LastingEffect.Value);
            if (Unit is not null) LastingEffect.Init(SourceUnit, Unit, Unit.Position, Unit.Direction);
            else if (Bullet is not null) LastingEffect.Init(Bullet);
            LastingEffect.SetLifeTime(float.PositiveInfinity);
        }
        
        if (Unit is not null && Unit.InputTime == -1) HideEffect();
        //if (Unit is null && Bullet is not null) ShowEffect();

        if (Unit is null) return;

        RegisterProgressBarIfNeeded();
        // 范围需求
        if (BuffData.RoundNeed == 1)
        {
            rounds = new List<Vector2Int>();
            foreach (var v in Round1)
            {
                var point = v + Unit.GridPos;
                if (point.x < 0 || point.x >= Battle.Map.Tiles.GetLength(0) ||
                    point.y < 0 || point.y >= Battle.Map.Tiles.GetLength(1))
                    continue;
                rounds.Add(point);
            }
        }
        if (Unit.Buffs.Any(x => x is Buffs.Buff抵挡) && SourceUnit.Buffs.Any(x => x is Buffs.Buff可抵挡) && !BuffData.NotCancelable)
        {
            Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.First(x => x is Buffs.Buff抵挡);
            if (blockbuff.Duration.value  == 0)
                return;
            //Log.Debug(Unit.UnitData.Name + "抵挡了" + BuffData.Id);
            if (Duration.value > blockbuff.Duration.value)
            {
                //Log.Debug(BuffData.Id + "生效延后" + blockbuff.Duration.value + "秒" + "持续" + (Duration.value - blockbuff.Duration.value) + "秒");
                blockbuff.AddBuff(new object[] { Id, Skill, Index, Duration.value - blockbuff.Duration.value });
            }
            Finish();
        }
    }


        private void RegisterProgressBarIfNeeded()
        {
            if (Unit == null || BuffData?.Data == null) return;

            string barType = BuffData.Data.GetStr("绑定进度条");
            if (!string.IsNullOrEmpty(barType))
            {
                Debug.Log($"Registering buff {BuffData.Id} to progress bar {barType} for unit {Unit.UnitData.Id}");
            UnitProgressBarManager.Instance.RegisterBuff(Unit, this, barType);
            }
        }

    public bool Enable()
    {
        if (Unit is null) return true;
        if (BuffData.StopNeed != 0 && Unit is Units.干员 u && u.StopUnits.Count < BuffData.StopNeed) return false;
        if (BuffData.StopLess != 0 && Unit is Units.干员 u2 && u2.StopUnits.Count >= BuffData.StopLess) return false;
        if (BuffData.StopNeed != 0 && Unit is Units.敌人 u1 && u1.StopUnit == null) return false;
        if (BuffData.RoundNeed != 0)
        {
            var units = Battle.FindAll(rounds, 1);
            if (units.Count > 1) return false;
        }
        return true;
    }

    public virtual void ApplyToUnit()
    {

    }

    public virtual void ApplyToBullet()
    {

    }

    public virtual void Reset()
    {
        updateLastTime();
        if (Unit is not null)
        {
            if ((Unit.Buffs.Any(x => x is Buffs.Buff抵挡) && SourceUnit.Buffs.Any(x => x is Buffs.Buff可抵挡)) && !BuffData.NotCancelable)
            {
                Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.First(x => x is Buffs.Buff抵挡);
                if (blockbuff.Duration.value != 0)
                {
                    if (Duration.value > blockbuff.Duration.value)
                        blockbuff.AddBuff(new object[] { Id, Skill, Index, Duration.value - blockbuff.Duration.value });
                    Finish();
                }
            }
        }
        if (BuffData.Upgrade != null)
        {
            if (LastingEffect != null)
            {
                EffectManager.Instance.ReturnEffect(LastingEffect);
                LastingEffect = null;
            }
            //Finish();
            //Unit.RemoveBuff(this);
            if (Unit is not null) Unit.AddBuff(BuffData.Upgrade.Value, this.Skill, Index);        
            if (Bullet is not null) Bullet.AddBuff(BuffData.Upgrade.Value, this.Skill,Index);        
        }
        if (BuffData.IfSwitch)
        {
            Finish();
        }
    }

    protected virtual void updateLastTime()
    {
        if (LastTime >= 0)
        {
            Duration.Set(LastTime);
            return;
        }
        float lastTime = BuffData.LastTime;
        if (Skill.SkillData.BuffLastTime != null)
        {
            lastTime = Skill.SkillData.BuffLastTime.Value;
        }
        Duration.Set(lastTime);
    }

    public virtual void Update()
    {
        if (isBlocking >= 0)
        {
            Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.Find(x => x is Buffs.Buff抵挡);
            blockbuff.AddBuff(new object[] { Id, Skill, Index, isBlocking });
            Finish();
        }

        if (Skill.SkillData.BuffRely)//单位离开技能范围，或施法者死亡时，buff自动消失
        {
            if (!Skill.Unit.Alive() || (Skill.SkillData.OpenTime > 0 && Skill.Opening.Finished() || (Skill.SkillData.UseType != SkillUseTypeEnum.被动 && !Skill.GetAttackTarget().Contains(Unit))))
            {
                Finish();
            }
        }

        if (BuffData.RelyBuff != null)
        {
            if (RelayBuff == null) RelayBuff = Unit.Buffs.FirstOrDefault(x => x.Id == BuffData.RelyBuff.Value);
            if (RelayBuff == null || RelayBuff.Dead) Finish();
        }

        if (BuffData.Resist)
        {
            if (Unit.Resist == 0)
                Duration.Finish();
            else
                Duration.Update(SystemConfig.DeltaTime / Unit.Resist);
        }
        else
            Duration.Update(SystemConfig.DeltaTime);
        if (Duration.Finished())
        {
            Finish();
        }
    }

    public virtual void UpdateView()
    {

    }

    public void ShowEffect()
    {
        if (LastingEffect is null) return;
        LastingEffect.IsHide = false;
        LastingEffect.gameObject.SetActive(true);
    }

    public void HideEffect() 
    {
        if (LastingEffect is null) return;
        LastingEffect.IsHide = true;
        LastingEffect.gameObject.SetActive(false);
    }

    public virtual void Finish()
    {
        Dead = true;
        Unit.RemoveBuff(this);
        if (LastingEffect != null)
        {
            EffectManager.Instance.ReturnEffect(LastingEffect);
            LastingEffect = null;
        }

        //Log.Debug($"{Unit.UnitData.Id} 失去了buff {BuffData.Id}");
    }
}