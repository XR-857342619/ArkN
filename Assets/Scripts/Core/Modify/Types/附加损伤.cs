using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public enum AddEleInjureBase
    {
        Fixed,
        UnitAttack,
        Damage,
    }

    public class 附加损伤 : Modify, IAfterDamageEffect
    {
        private float Rate;
        private AddEleInjureBase Base;
        private string EleType;

        public override void Init()
        {
            base.Init();
            Rate = ModifyData.Data.GetFloat("Rate");
            bool parsed = Enum.TryParse(ModifyData.Data.GetStr("Base"), true, out Base);
            if (!parsed)
                Base = AddEleInjureBase.Damage;

            EleType = ModifyData.Data.GetStr("Element")?.Trim();
        }

        public void Modify(DamageInfo damageInfo)
        {
            // 被闪避的攻击没有命中，不应附加元素损伤。
            if (damageInfo == null || damageInfo.Target == null || damageInfo.Avoid)
                return;
            if (string.IsNullOrWhiteSpace(EleType))
                return;

            Unit sourceUnit = damageInfo.GetSourceUnit();
            float injury;

            switch (Base)
            {
                case AddEleInjureBase.Fixed:
                    injury = Rate;
                    break;
                case AddEleInjureBase.UnitAttack:
                    // GetSourceUnit 在部分伤害来源（例如纯 Buff/脚本伤害）下可能为空，
                    // 此时退回本次伤害的攻击面板，避免空引用。
                    injury = (sourceUnit != null ? sourceUnit.Attack : damageInfo.Attack) * Rate;
                    break;
                case AddEleInjureBase.Damage:
                    injury = damageInfo.FinalDamage * Rate;
                    break;
                default:
                    return;
            }

            if (injury <= 0f)
                return;

            damageInfo.Target.ChangeEleInjure(injury, EleType);
        }
    }
}
