using Bullets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 治疗量提升依溢出 : Modify, IBulletDamageModify
    {
        public float Rate;
        public float LastExDamage = 0;

        public override void Init()
        {
            base.Init();
            Rate = ModifyData.Data.GetFloat("Rate");
        }

        public void Modify(DamageInfo damageInfo, Bullet bullet)
        {
            float damage = damageInfo.Attack * damageInfo.DamageRate * (1 + damageInfo.Target.HealReceiveRate);
            float ExDamage = damage - (damageInfo.Target.MaxHp - damageInfo.Target.Hp);
            if (damageInfo.DamageRate == 0)
            {
                LastExDamage = 0;
                return;
            }
            damageInfo.Attack += LastExDamage * Rate / damageInfo.DamageRate;
            //Log.Debug("治疗量提升依溢出：" + ExDamage);
            LastExDamage = ExDamage > 0 ? ExDamage : 0;
        }
    }
}
