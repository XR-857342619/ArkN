using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    /// <summary>
    /// 启用弱点伤害的修饰器。
    /// 应用此修饰器的伤害，会在 Unit.Damage 中根据目标防御/法抗重新决定物理或法术类型。
    /// </summary>
    public class 弱点伤害 : Modify, IDamageModify
    {
        public void Modify(DamageInfo damageInfo)
        {
            DamageTypeEnum originalDamageType = damageInfo.DamageType;
            DamageTypeEnum switchedDamageType = originalDamageType == DamageTypeEnum.Normal ? DamageTypeEnum.Magic : DamageTypeEnum.Normal;

            if (originalDamageType is not DamageTypeEnum.Normal && originalDamageType is not DamageTypeEnum.Magic) return;

            float expextedOriginalDamage = 0f;
            float expextedSwitchedDamage = 0f;

            expextedOriginalDamage = damageInfo.Target.basicDamageCalculation(damageInfo);

            damageInfo.DamageType = switchedDamageType;
            expextedSwitchedDamage = damageInfo.Target.basicDamageCalculation(damageInfo);

            damageInfo.BasicDamage = 0;

            if (expextedOriginalDamage - expextedSwitchedDamage > 0.001f)
            {
                damageInfo.DamageType = originalDamageType;
            }
            else
            {
                damageInfo.DamageType = switchedDamageType;
            }
        }
    }
}