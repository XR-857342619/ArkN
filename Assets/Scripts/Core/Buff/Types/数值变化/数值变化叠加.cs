using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 数值变化叠加 : 数值变化
    {
        public int Level;
        public int MaxLevel;
        public int AddValue;

        public override void Init()
        {
            IsMultiLevel = true;
            base.Init();
            Level = 1;
            MaxLevel = BuffData.Data.GetInt("MaxLevel");
            AddValue = BuffData.Data.GetInt("AddValue", 1);
            Log.Debug(BuffData.Id + "Init");
        }

        public override void Reset()
        {
            base.Reset();
            Level += AddValue;
            if (Level > MaxLevel && MaxLevel != 0) Level = MaxLevel;
        }

        public override void ApplyToUnit()
        {
            Log.Debug("数值变化可叠加Apply");
            base.ApplyToUnit();
        }

        public override void ApplyToBullet()
        {
            Log.Debug("数值变化可叠加Apply");
            base.ApplyToBullet();
        }

        public override void Update()
        {
            if (isBlocking >= 0)
            {
                Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.Find(x => x is Buffs.Buff抵挡);
                blockbuff.AddBuff(new object[] { Id, Skill, Index, isBlocking });
                Finish();
            }

            if (Skill.SkillData.BuffRely)//单位离开技能范围，或施法者死亡时，buff自动消失
            {
                if (!Skill.Unit.Alive() || (Skill.SkillData.OpenTime > 0 && Skill.Opening.Finished() || (Skill.SkillData.UseType != SkillUseTypeEnum.被动 && !Skill.GetAttackTarget().Contains(Unit))))
                {
                    Finish();
                }
            }

            if (BuffData.RelyBuff != null)
            {
                if (RelayBuff == null) RelayBuff = Unit.Buffs.FirstOrDefault(x => x.Id == BuffData.RelyBuff.Value);
                if (RelayBuff == null || RelayBuff.Dead) Finish();
            }

            if (BuffData.Resist)
            {
                if (Unit.Resist == 0)
                    Duration.Finish();
                else
                    Duration.Update(SystemConfig.DeltaTime / Unit.Resist);
            }
            else
                Duration.Update(SystemConfig.DeltaTime);
            if (Duration.Finished())
            {
                if (Level > 1)
                {
                    Level--;
                    updateLastTime();
                }
                else
                    Finish();
            }
        }

        public override void Finish()
        {
            Level = 0;
            base.Finish();
        }

        protected override float GetValue(int i)
        {
            return base.GetValue(i) * Level;
        }
    }
}
