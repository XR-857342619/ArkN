using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 对buff切换伤害类型 : Modify, IDamageModify
    {
        private DamageTypeEnum damageType;
        private bool flag = false;
        public void Modify(DamageInfo damageInfo)
        {
            if (ModifyData.Buff != null)
            {
                if (damageInfo.Target.Buffs.Any(x => x.Id == ModifyData.Buff.Value))
                {
                    flag = Enum.TryParse<DamageTypeEnum>(ModifyData.Data.GetStr("DamageType"), out damageType);
                    damageInfo.DamageType = flag ? damageType : damageInfo.DamageType;
                }
            }
        }
    }
}
