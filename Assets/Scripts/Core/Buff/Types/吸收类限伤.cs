using System;
using UnityEngine;
using Buffs;

public class 吸收类限伤 : Buff, IShield
{
    public int Count;

    public override void Init()
    {
        base.Init();
        Count = BuffData.Data.GetInt("Count", 0);
        //Unit.RewriteDamage = (float)this.Count;
    }
    //public override void Apply()
    //{
    //    base.Apply();
    //    if (this.Dead)
    //    {
    //        return;
    //    }
    //    this.Unit.RewriteDamage = (float)this.Count;
    //}

    public void Absorb(DamageInfo damageInfo)
    {
        if ((float)this.Count > damageInfo.FinalDamage)
        {
            return;
        }
        damageInfo.FinalDamage = (float)this.Count;
    }

    public int OrderCode
    {
        get
        {
            return base.BuffData.OrderCount;
        }
    }

    //public override void Finish()
    //{
    //    base.Finish();
    //    Unit.RewriteDamage = 0f;
    //}

}