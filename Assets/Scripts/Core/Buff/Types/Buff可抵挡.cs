using UnityEngine;
using System.Linq;

namespace Buffs
{
    public class Buff可抵挡 : Buff, ISelfDamageModify, IDamageModify
    {
        public void Modify(DamageInfo damageInfo)
        {
            //Log.Debug("Buff可抵挡伤害重写");
            if (damageInfo.Target.Buffs.Any(x => x.GetType() == typeof(Buff抵挡)))
            {
                damageInfo.Attack = 0;
                damageInfo.Avoid = true;
            }
        }
    }
}
