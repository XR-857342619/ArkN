using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 塑灵术士式索敌 : Skill
    {
        // 使自身索敌可以获取到被token阻挡的单位
        public override List<Unit> GetAttackTarget()
        {
            // 先调用父类方法获取基础目标列表
            var baseTargets = base.GetAttackTarget();
            // 初始化临时列表，包含父类返回的所有目标
            tempTargets = new List<Unit>(baseTargets);

            if (Unit is Units.干员 op)
            {
                foreach (var child in op.Children)
                {
                    var token = child as Units.干员;
                    if (token == null) continue;
                    tempTargets.AddRange(token.StopUnits.Where(t => !baseTargets.Contains(t)));
                }
            }

            orderTargets(tempTargets);

            return tempTargets;
        }
    }
}
