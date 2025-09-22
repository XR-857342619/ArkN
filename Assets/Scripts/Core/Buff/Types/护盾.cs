using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 护盾 : Buff, IDamageRewrite
    {
        public int Count;
        public int orderCode;
        public int OrderCode
        {
            get { return orderCode; } 
        }

        public override void Init()
        {
            base.Init();
            Count = BuffData.Data.GetInt("Count");
            orderCode = BuffData.Data.GetInt("OrderCode",1000);
        }

        public void DamageRewrite(DamageInfo damageInfo)
        {
            if (damageInfo.FinalDamage > 0)
            {
                Count--;
                damageInfo.FinalDamage = 0;
            }
            if (Count == 0) Finish();
        }
    }
}
