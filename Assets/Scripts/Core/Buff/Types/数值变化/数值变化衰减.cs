using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 数值变化衰减 : Buff
    {
        private string[] names;
        private UnifiedExpressionEngine engine;

        public override void Init()
        {
            base.Init();
            engine = new UnifiedExpressionEngine(this);

            var datas = BuffData.Data.GetArray("t");
            names = new string[datas.Length];
            for (int i = 0; i < datas.Length; i++)
            {
                names[i] = Convert.ToString(datas[i]);
            }
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

        private float GetValue(int i)
        {
            if (Skill?.SkillData?.BuffLastTime == null || Skill.SkillData.BuffLastTime.Value == 0)
                return 0f;

            return Skill.SkillData.GetBuffData(Index)[i] * Duration.value / Skill.SkillData.BuffLastTime.Value;
        }
    }
}