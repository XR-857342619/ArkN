using Bullets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 链奶伤害衰减 : Modify, IBulletDamageModify
    {
        public float Rate;
        public override void Init()
        {
            base.Init();
            Rate = ModifyData.Data.GetFloat("Rate");
        }

        public void Modify(DamageInfo damageInfo, Bullet bullet)
        {
            if (!(bullet is 链式弹道 b)) return;
            //damageInfo.DamageRate *= Rate * b.LinkNum;
            damageInfo.DamageRate *= (float)Math.Pow(Rate, b.LinkNum);
        }
    }
}
