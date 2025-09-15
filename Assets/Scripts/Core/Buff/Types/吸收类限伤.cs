using System;
using UnityEngine;
using Buffs;

public class 吸收类限伤 : Buff, _IShield
{
    public int Count;

    public override void Init()
    {
        base.Init();
        this.Count = base.BuffData.Data.GetInt("Count", 0);
        this.Unit.RewriteDamage = (float)this.Count;
    }
    public override void Apply()
    {
        base.Apply();
        if (this.Dead)
        {
            return;
        }
        this.Unit.RewriteDamage = (float)this.Count;
    }

    public void Absorb(DamageInfo damageInfo)
    {
        if ((float)this.Count > damageInfo.FinalDamage)
        {
            return;
        }
        damageInfo.FinalDamage = (float)this.Count;
    }

    public int OrderCount
    {
        get
        {
            return base.BuffData.OrderCount;
        }
    }

    public override void Finish()
    {
        base.Finish();
        this.Unit.RewriteDamage = 0f;
    }

}