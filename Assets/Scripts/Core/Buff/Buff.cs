using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;

[System.Serializable]
public class Buff
{
    public static List<Vector2Int> Round1 = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
    public BuffData BuffData => Database.Instance.Get<BuffData>(Id);
    public int Id;
    public int Index;

    [System.NonSerialized]
    public Unit Unit;
    public Skill Skill;
    protected Battle Battle => Skill.Unit.Battle;

    public CountDown Duration = new CountDown();

    public Effect LastingEffect;

    public Buff RelayBuff;
    public bool Dead;

    //public bool MakesBuffsCancelable;

    List<Vector2Int> rounds;

    //入梦砖相关
    public bool CancelsCancelableBuffs { get; set; }
    public bool MakesBuffsCancelable { get; set; } // 这个BUFF是否使施加者施加的BUFF变为可抵挡
    public Unit OriginalCaster { get; set; } // 记录原始施加者

    public virtual void Init()
    {
        OriginalCaster = Skill?.Unit;

        // 如果施加者有「BUFF可抵挡制造」效果 → 这个buff就是可抵挡的
        if (OriginalCaster != null)
        {
            BuffData.IsCancelable = OriginalCaster.Buffs.Any(b => b.MakesBuffsCancelable && !b.Dead);
        }
        
        // 自己是不是「BUFF抵挡制造者」
        if (MakesBuffsCancelable)
            MakesBuffsCancelable = true;

        // 先检查目标是否能抵挡
        if (BuffData.IsCancelable && Unit.Buffs.Any(b => b.CancelsCancelableBuffs && !b.Dead))
        {
            return; // 进入压制状态，不生效，但持续时间仍在走
        }

        updateLastTime();

        // 持续特效（显示用）
        if (BuffData.LastingEffect.HasValue)
        {
            LastingEffect = EffectManager.Instance.GetEffect(BuffData.LastingEffect.Value);
            LastingEffect.Init(Skill.Unit, Unit, Unit.Position, Unit.Direction);
            LastingEffect.SetLifeTime(float.PositiveInfinity);
        }

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

    }

    public virtual void Reset()
    {
        updateLastTime();
        if (BuffData.Upgrade != null)
        {
            if (LastingEffect != null)
            {
                EffectManager.Instance.ReturnEffect(LastingEffect);
                LastingEffect = null;
            }
            //Finish();
            //Unit.RemoveBuff(this);
            Unit.AddBuff(BuffData.Upgrade.Value, this.Skill, Index);
        }
        if (BuffData.IfSwitch)
        {
            Finish();
        }
    }

    protected virtual void updateLastTime()
    {
        float lastTime = BuffData.LastTime;
        if (Skill.SkillData.BuffLastTime != null)
        {
            lastTime = Skill.SkillData.BuffLastTime.Value;
        }
        Duration.Set(lastTime);
    }

    public virtual void Update()
    {
        if (!Enable())
        {
            // Buff 不生效，但仍然需要更新时间
            if (BuffData.Resist)
            {
                Duration.Update(SystemConfig.DeltaTime / Unit.Resist);
            }
            else
                Duration.Update(SystemConfig.DeltaTime);

            if (Duration.Finished())
            {
                Finish();
            }
            return;
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
