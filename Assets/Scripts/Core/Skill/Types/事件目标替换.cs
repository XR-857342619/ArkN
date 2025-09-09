using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 事件目标替换:Skill
    {
        public Unit t;
        public override void FindTarget()
        {
            GetAttackTarget();
            var td = Battle.TriggerDatas;
            if (td.Count > 0)
                t = td.Peek().Target;
            //Log.Debug("事件目标替换: " + td.Peek());
            if (t != null && CanUseTo(t) && tempTargets.Contains(t))
            {
                Targets.Add(t);
            }
        }
    }
}
