using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 额外目标依层数 : Modify, ITargetModify
    {
        public int Modify(int count, Unit unit)
        {   
            if (ModifyData.Buff is null) return count;
            Buff buff = unit.Buffs.FirstOrDefault(x => x.Id == ModifyData.Buff);
            if (buff is null) return count;
            if (!(buff is Buffs.数值变化叠加 b)) return count;
            return b.level + count;
        }
    }
}
