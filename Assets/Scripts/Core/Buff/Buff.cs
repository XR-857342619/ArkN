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

    [NonSerialized] public Unit Unit;
    public Skill Skill;
    protected Battle Battle => Skill.Unit.Battle;

    public CountDown Duration = new CountDown();
    public Effect LastingEffect;
    public Buff RelayBuff;
    public bool Dead;

    private List<Vector2Int> rounds;

    // 入梦砖相关
    public bool CancelsCancelableBuffs { get; set; }
    public bool MakesBuffsCancelable { get; set; }
    public Unit OriginalCaster { get; set; }
    public bool IsSuppressed { get; private set; }
    private float RemainingTime; // 压制期间记录剩余时间
    private float SuppressStartTime;

    public virtual void Init()
    {
        OriginalCaster = Skill?.Unit;

        // 判断是否进入压制状态
        if (BuffData.IsCancelable && Unit.Buffs.Any(b => b.CancelsCancelableBuffs && !b.Dead)
            && OriginalCaster.Buffs.Any(b => b.MakesBuffsCancelable && !b.Dead))
        {
            Suppress(); // 压制
            return;
        }

        updateLastTime();
        InitEffect();
        InitRounds();
    }

    private void InitEffect()
    {
        if (BuffData.LastingEffect.HasValue)
        {
            LastingEffect = EffectManager.Instance.GetEffect(BuffData.LastingEffect.Value);
            LastingEffect.Init(Skill.Unit, Unit, Unit.Position, Unit.Direction);
            LastingEffect.SetLifeTime(float.PositiveInfinity);
        }
    }

    private void InitRounds()
    {
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
    }

    public bool Enable()
    {
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

    public virtual void Apply()
    {
        if (IsSuppressed) return; // 压制状态下不生效
    }

    public virtual void Reset()
    {
        if (!IsSuppressed)
        {
            updateLastTime();
            if (BuffData.Upgrade != null)
            {
                if (LastingEffect != null)
                {
                    EffectManager.Instance.ReturnEffect(LastingEffect);
                    LastingEffect = null;
                }
                Unit.AddBuff(BuffData.Upgrade.Value, Skill, Index);
            }
            if (BuffData.IfSwitch) Finish();
        }
    }

    protected virtual void updateLastTime()
    {
        float lastTime = BuffData.LastTime;
        if (Skill.SkillData.BuffLastTime != null)
            lastTime = Skill.SkillData.BuffLastTime.Value;
        Duration.Set(lastTime);
    }

    public void Suppress()
    {
        if (IsSuppressed) return;
        IsSuppressed = true;
        RemainingTime = Duration.value;
        SuppressStartTime = Time.time;
        if (LastingEffect != null)
        {
            EffectManager.Instance.ReturnEffect(LastingEffect);
            LastingEffect = null;
        }
    }

    public void UnSuppress()
    {
        if (!IsSuppressed) return;
        IsSuppressed = false;
        float elapsed = Time.time - SuppressStartTime;
        RemainingTime = Mathf.Max(0, RemainingTime - elapsed);
        Duration.Set(RemainingTime);
        InitEffect();
    }

    public virtual void Update()
    {
        // 压制期间只更新持续时间
        if (BuffData.IsCancelable &&
            Unit.Buffs.Any(b => b.CancelsCancelableBuffs && !b.Dead) &&
            OriginalCaster.Buffs.Any(b => b.MakesBuffsCancelable && !b.Dead))
        {
            if (!IsSuppressed)
            {
                Suppress(); // 进入压制状态
            }
        }
        else
        {
            if (IsSuppressed)
            {
                UnSuppress(); // 解除压制
            }
        }

        if (Skill.SkillData.BuffRely)
        {
            if (!Skill.Unit.Alive() || (Skill.SkillData.OpenTime > 0 && Skill.Opening.Finished())
                || (Skill.SkillData.UseType != SkillUseTypeEnum.被动 && !Skill.GetAttackTarget().Contains(Unit)))
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
            Duration.Update(SystemConfig.DeltaTime / Unit.Resist);
        else
            Duration.Update(SystemConfig.DeltaTime);

        if (Duration.Finished()) Finish();
        Apply();
    }

    public virtual void UpdateView() 
    {
        
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
    }
}
