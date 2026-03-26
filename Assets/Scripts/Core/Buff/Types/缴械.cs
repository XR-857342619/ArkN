using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 缴械 : Buff
    {
        public override void ApplyToUnit()
        {
            base.ApplyToUnit();
            Unit.CanAttack = false;
        }
    }
}
