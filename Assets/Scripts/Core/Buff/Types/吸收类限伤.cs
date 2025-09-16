using System;
using UnityEngine;
using Buffs;

namespace Buffs
{
    public class 吸收类限伤 : Buff, IShield
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
        public void Absorb(DamageInfo damageInfo)
        {
            if (damageInfo.FinalDamage >= MinResponseLimit)
                damageInfo.FinalDamage = MinResponseLimit;
        }

        public override void Finish()
        {
            base.Finish();
            MinResponseLimit = 0f;
        }
    }
}
