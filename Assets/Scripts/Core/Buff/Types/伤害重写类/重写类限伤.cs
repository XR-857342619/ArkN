using System;
using UnityEngine;
using Buffs;

namespace Buffs
{
    public class 重写类限伤 : Buff
    {
        public int Count;

        public override void Init()
        {
            base.Init();
            this.Count = BuffData.Data.GetInt("Count", 0);
            this.Unit.RewriteDamage = (float)this.Count;
        }
        public override void ApplyToUnit()
        {
            base.ApplyToUnit();
            if (this.Dead)
            {
                return;
            }
            this.Unit.RewriteDamage = (float)this.Count;
        }
        public override void Finish()
        {
            base.Finish();
            this.Unit.RewriteDamage = 0f;
        }

    }
}
    