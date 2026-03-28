using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 专注失调 : Buff
    {
        public override void Update()
        {
            base.Update();
            var sk = Unit?.MainSkill ?? null;
            if (sk is null) return;
            if (sk.CanOpen()) sk.DoOpen();
        }
    }
}
