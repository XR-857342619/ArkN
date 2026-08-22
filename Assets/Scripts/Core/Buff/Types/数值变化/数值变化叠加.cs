using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 数值变化叠加 : MultiLevelBuff
    {
        private string[] names;
        private UnifiedExpressionEngine engine;

        //public int Level;
        //public int MaxLevel;
        public int AddValue;

        public override void Init()
        {
            //IsMultiLevel = true;
            base.Init();
            engine = new UnifiedExpressionEngine(this);

            var datas = BuffData.Data.GetArray("t");
            names = new string[datas.Length];
            for (int i = 0; i < datas.Length; i++)
            {
                names[i] = Convert.ToString(datas[i]);
            }

            Level = 1;
            MaxLevel = BuffData.Data.GetInt("MaxLevel");
            AddValue = BuffData.Data.GetInt("AddValue", 1);
        }

        public override void Reset()
        {
            base.Reset();
            Level += AddValue;
            if (Level > MaxLevel && MaxLevel != 0) Level = MaxLevel;
        }

        public override void ApplyToUnit()
        {
            for (int i = 0; i < names.Length; i++)
            {
                engine.ApplyNumericChange(Unit, names[i], GetValue(i), NumericChangeMode.Add);
            }
        }

        public override void ApplyToBullet()
        {
            for (int i = 0; i < names.Length; i++)
            {
                engine.ApplyNumericChange(Bullet, names[i], GetValue(i), NumericChangeMode.Add);
            }
        }

        public override void Update()
        {
            if (isBlocking >= 0)
            {
                Buffs.Buff抵挡 blockbuff = (Buffs.Buff抵挡)Unit.Buffs.Find(x => x is Buffs.Buff抵挡);
                blockbuff.AddBuff(new object[] { Id, Skill, Index, isBlocking });
                Finish();
                return;
            }

            if (Skill.SkillData.BuffRely)//单位离开技能范围，或施法者死亡时，buff自动消失
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
                if (RelayBuff == null) RelayBuff = Unit.Buffs.FirstOrDefault(x => x.Id == BuffData.RelyBuff.Value);
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

        private float GetValue(int i)
        {
            return Skill.SkillData.GetBuffData(Index)[i] * Level;
        }
    }
}