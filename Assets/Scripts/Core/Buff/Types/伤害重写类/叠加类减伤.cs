using System;
using System.Collections.Generic;
using System.Linq;

namespace Buffs
{
    /// <summary>
    /// 可叠加减伤（独立实现，不依赖原减伤类）
    /// </summary>
    public class 叠加类减伤 : Buff, IDamageModify
    {
        // 从原减伤类复制过来的字段
        private int buffId = -1;
        private float rate;
        private float reducePerLevel; // 单层减伤比例

        // 叠加特有字段
        public int Level;
        public int MaxLevel;
        public int AddValue;

        public override void Init()
        {
            base.Init();

            // 读取基础减伤配置（与原减伤相同）
            rate = BuffData.Data.GetFloat("Rate");
            string buffName = BuffData.Data.GetStr("TargetBuffNeed");
            if (!string.IsNullOrEmpty(buffName))
                buffId = Database.Instance.GetIndex<BuffData>(buffName);

            // 读取叠加配置
            MaxLevel = BuffData.Data.GetInt("MaxLevel", 0);
            AddValue = BuffData.Data.GetInt("AddValue", 1);

            Level = 1;
            reducePerLevel = 1f - rate;
        }

        public override void Reset()
        {
            base.Reset();
            if (Dead) return;

            Level += AddValue;
            if (MaxLevel > 0 && Level > MaxLevel)
                Level = MaxLevel;

            updateLastTime(); // 重置持续时间
        }

        public override void Update()
        {
            // 复制基类 Buff.Update 的全部逻辑（包括抵挡、依赖、抵抗等）
            // 与之前写的 Update 完全相同，这里不再重复，直接复制即可
            // 注意：需要处理层数衰减
            if (isBlocking >= 0)
            {
                Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.Find(x => x is Buffs.Buff抵挡);
                blockbuff.AddBuff(new object[] { Id, Skill, Index, isBlocking });
                Finish();
                return;
            }

            if (Skill.SkillData.BuffRely)
            {
                if (!Skill.Unit.Alive() ||
                    (Skill.SkillData.OpenTime > 0 && Skill.Opening.Finished()) ||
                    (Skill.SkillData.UseType != SkillUseTypeEnum.被动 && !Skill.GetAttackTarget().Contains(Unit)))
                {
                    Finish();
                    return;
                }
            }

            if (BuffData.RelyBuff != null)
            {
                if (RelayBuff == null)
                    RelayBuff = Unit.Buffs.FirstOrDefault(x => x.Id == BuffData.RelyBuff.Value);
                if (RelayBuff == null || RelayBuff.Dead)
                {
                    Finish();
                    return;
                }
            }

            if (BuffData.Resist)
            {
                if (Unit.Resist == 0)
                    Duration.Finish();
                else
                    Duration.Update(SystemConfig.DeltaTime / Unit.Resist);
            }
            else
            {
                Duration.Update(SystemConfig.DeltaTime);
            }

            if (Duration.Finished())
            {
                if (Level > 1)
                {
                    Level--;
                    updateLastTime();
                }
                else
                {
                    Finish();
                }
            }
        }

        public override void Finish()
        {
            Level = 0;
            base.Finish();
        }

        // 实现 IDamageModify 接口（注意这里不重写，而是实现接口方法）
        public void Modify(DamageInfo damageInfo)
        {
            // 条件判断（与原减伤一致）
            if (buffId != -1)
            {
                var source = damageInfo.GetSourceUnit();
                if (source == null || !source.Buffs.Any(x => x.Id == buffId))
                    return;
            }

            float totalReduce = reducePerLevel * Level;
            if (totalReduce > 1f) totalReduce = 1f;
            float finalRate = 1f - totalReduce;

            damageInfo.DamageRate *= finalRate;
        }
    }
}