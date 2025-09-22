using System;
using UnityEngine;
using Buffs;

namespace Buffs
{
    public class 伤害重写 : Buff, IDamageRewrite
    {
        public float MinResponseLimit;
        public float RewriteDamage;
        public int orderCode;
        public int OrderCode
        {
            get { return orderCode; }
        }

        public override void Init()
        {
            base.Init();
            MinResponseLimit = BuffData.Data.GetInt("MinResponseLimit", 1);
            orderCode = BuffData.Data.GetInt("OrderCode", 0);
            //Unit.RewriteDamage = MinResponseLimit;
        }
        public void DamageRewrite(DamageInfo damageInfo)
        {
            if (damageInfo.FinalDamage >= MinResponseLimit)
                damageInfo.FinalDamage = MinResponseLimit;
        }

    }
}
