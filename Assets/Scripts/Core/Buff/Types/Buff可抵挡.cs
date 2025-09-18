using UnityEngine;

namespace Buffs
{
    public class Buff可抵挡 : Buff, ISelfDamageModify, IDamageModify
    {
        public void Modify(DamageInfo damageInfo)
        {
            damageInfo.FinalDamage = 0;
        }
    }
}
