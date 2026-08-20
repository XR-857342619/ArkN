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
    public class 启用弱点伤害 : Modify, IDamageModify
    {
        public void Modify(DamageInfo damageInfo)
        {
            // 标记此伤害需要应用弱点判断
            damageInfo.EnableWeakness = true;
        }
    }
}