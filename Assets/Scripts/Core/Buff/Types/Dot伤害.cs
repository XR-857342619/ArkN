using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class DotBuff : Buff
    {
        private float baseDamage;
        private float triggerInterval;
        private DamageTypeEnum damageType;
        private float farAttackRate = 1f;
        private bool multiplyByInterval;

        private enum GrowthMode { Constant, Linear, CountLinear }
        private GrowthMode growthMode = GrowthMode.Constant;
        private float startRate = 1f;
        private float maxRate = 1f;
        private float maxTime = 10f;
        private float ratePerCount = 0.1f;

        private CountDown triggerCD = new CountDown();
        private int triggerCount = 0;
        private float elapsedTime = 0f;

        public override void Init()
        {
            base.Init();

            // 1. 读取基础伤害基数配置
            int damageBaseType = BuffData.Data.GetInt("DamageBase", 2);
            float coeff = BuffData.Data.GetFloat("BaseCoeff", -1f);
            if (coeff < 0)  // 未直接配置，从技能数据数组取
                coeff = Skill.SkillData.GetBuffData(Index)[0];

            switch (damageBaseType)
            {
                case 0: baseDamage = coeff; break;
                case 1: baseDamage = coeff * Skill.Unit.Attack; break;
                case 2:
                default: baseDamage = coeff * Unit.MaxHp; break;
            }

            // 2. 触发间隔
            triggerInterval = BuffData.Data.GetFloat("TriggerInterval", 1f);
            if (triggerInterval < SystemConfig.DeltaTime)
                triggerInterval = SystemConfig.DeltaTime;
            triggerCD.Set(triggerInterval);

            // 3. 伤害类型
            string dtStr = BuffData.Data.GetStr("DamageType", "Real");
            damageType = (DamageTypeEnum)Enum.Parse(typeof(DamageTypeEnum), dtStr);

            // 4. 远程倍率
            if (Unit.FirstSkill != null && Unit.FirstSkill.SkillData.AttackRange > 0)
                farAttackRate = BuffData.Data.GetFloat("FarAttackUnitRate", 1f);

            // 5. 是否乘间隔
            multiplyByInterval = BuffData.Data.GetInt("MultiplyByInterval", 0) == 1;

            // 6. 增长模式
            string growthStr = BuffData.Data.GetStr("GrowthMode", "Constant");
            switch (growthStr)
            {
                case "Linear":
                    growthMode = GrowthMode.Linear;
                    startRate = BuffData.Data.GetFloat("StartRate", 1f);
                    maxRate = BuffData.Data.GetFloat("MaxRate", 1f);
                    maxTime = BuffData.Data.GetFloat("MaxTime", 10f);
                    break;
                case "CountLinear":
                    growthMode = GrowthMode.CountLinear;
                    ratePerCount = BuffData.Data.GetFloat("RatePerCount", 0.1f);
                    break;
                default:
                    growthMode = GrowthMode.Constant;
                    break;
            }
        }

        public override void Update()
        {
            base.Update();
            triggerCD.Update(SystemConfig.DeltaTime);
            if (!triggerCD.Finished()) return;

            triggerCount++;
            elapsedTime += triggerInterval;

            // 计算当前伤害倍率
            float multiplier = 1f;
            switch (growthMode)
            {
                case GrowthMode.Constant:
                    multiplier = 1f;
                    break;
                case GrowthMode.Linear:
                    float progress = Math.Min(elapsedTime / maxTime, 1f);
                    multiplier = startRate + (maxRate - startRate) * progress;
                    break;
                case GrowthMode.CountLinear:
                    multiplier = 1f + ratePerCount * triggerCount;
                    break;
            }

            float finalDamage = baseDamage * multiplier;
            if (multiplyByInterval)
                finalDamage *= triggerInterval;
            finalDamage *= farAttackRate;

            Unit.Damage(new DamageInfo()
            {
                Attack = finalDamage,
                DamageType = damageType,
                Target = Unit,
                Source = this,
            });

            triggerCD.Set(triggerInterval);
        }
    }
}