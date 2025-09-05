using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 麻痹:Buff
    {
        protected int times;
        protected int orgtimes;
        public override void Init()
        {
            base.Init();
            var times = BuffData.Data.GetInt("Times");
            foreach (var skill in Unit.Skills)
            {
                if (skill.IsCantCastCount > 0)
                {
                    orgtimes = skill.IsCantCastCount;
                    skill.IsCantCastCount += times;
                }
                else
                    skill.IsCantCastCount = times;
            }
        }

        public override void Update()
        {
            base.Update();
            foreach (var skill in Unit.Skills)
            {
                if (skill.IsCantCastCount == 0)
                {
                    Duration.Finish();
                    //break;
                }
                //Log.Debug("麻痹层数"+skill.IsCantCastCount);
            }
        }
        public override void Finish()
        {
            base.Finish();
            foreach (var skill in Unit.Skills)
            {
                //if (orgtimes > 0)
                //{
                //    if (orgtimes + times > skill.IsCantCastCount)
                //        if (orgtimes + times - skill.IsCantCastCount < times)
                //            skill.IsCantCastCount = times - orgtimes - times + skill.IsCantCastCount;
                //    else if (orgtimes + times = skill.IsCantCastCount)
                //        skill.IsCantCastCount -= times;
                //    else if (orgtimes + times < skill.IsCantCastCount)
                //        skill.IsCantCastCount -= times;
                //}
                //else if (skill.IsCantCastCount > times)
                //    skill.IsCantCastCount -= times;
                if (skill.IsCantCastCount > times)
                    skill.IsCantCastCount -= times;
                else
                    skill.IsCantCastCount = 0;
            }
        }
        protected virtual float GetValue(int i)
        {
            return Skill.SkillData.GetBuffData(Index)[i];
        }
    }
}
