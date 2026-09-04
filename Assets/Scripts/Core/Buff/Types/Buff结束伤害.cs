using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class Buff结束伤害 : Buff
    {
        private float damageValue;      // 计算后的最终伤害
        private DamageTypeEnum damageType;

        public override void Init()
        {
            base.Init();

            // 1. 读取伤害类型（与中毒一致）
            damageType = (DamageTypeEnum)Enum.Parse(
                typeof(DamageTypeEnum),
                BuffData.Data.GetStr("DamageType", "Real")  // 默认真实伤害
            );

            // 2. 根据 DamageBase 计算伤害基数
            int damageBase = BuffData.Data.GetInt("DamageBase", 2);  // 默认按最大生命值
            float baseValue = BuffData.Data.GetFloat("DamageValue", 0.2f); // 通用数值字段

            switch (damageBase)
            {
                case 0: // 固定值
                    damageValue = baseValue;
                    break;
                case 1: // 基于攻击力
                    damageValue = baseValue * Skill.Unit.Attack;
                    break;
                case 2: // 基于最大生命值（与原逻辑一致）
                default:
                    damageValue = baseValue * Unit.MaxHp;
                    break;
            }

            // 可选：支持远程单位倍率（参考中毒，如果不需要可以忽略）
            float farRate = BuffData.Data.GetFloat("FarAttackUnitRate", 1f);
            if (Unit.FirstSkill != null && Unit.FirstSkill.SkillData.AttackRange > 0)
            {
                damageValue *= farRate;
            }
        }

        public override void Finish()
        {
            // 触发伤害
            Unit.Damage(new DamageInfo()
            {
                Attack = damageValue,
                Source = this,
                DamageType = damageType,
                Target = Unit   // 通常伤害目标为自身（消去类）
            });

            base.Finish();
        }
    }
}