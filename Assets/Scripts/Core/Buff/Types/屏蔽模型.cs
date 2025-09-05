using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 屏蔽模型:Buff
    {
        public float buffConfig;
        public override void Init()
        {
            base.Init();
            buffConfig = Skill.SkillData.GetBuffData(Index)[0];
        }
        public override void Apply()
        {
            base.Apply();
            switch (buffConfig)
            {
                case 0:
                    Unit.UnitModel.hideModel();
                    Unit.UnitModel.hideShadow();
                    break;
                case 1:
                    Unit.UnitModel.hideModel();
                    break;
                case 2:
                    Unit.UnitModel.hideShadow();
                    break;
            }
        }
        public override void Finish()
        {
            base.Finish();
            Unit.UnitModel.showModel();
            Unit.UnitModel.showShadow();
        }
    }
}
