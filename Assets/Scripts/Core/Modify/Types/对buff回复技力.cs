using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 对buff回复技力 : Modify, IUnitModify
    {
        public string skillName;
        public int spCount;
        public bool withTip;
        public bool ignoreOpening;
        public override void Init()
        {
            skillName = ModifyData.Data.GetStr("目标技能");
            spCount = ModifyData.Data.GetInt("技力");
            withTip = ModifyData.Data.GetBool("提示");
            ignoreOpening = ModifyData.Data.GetBool("无视阻回");
        }
        public void Modify(Unit unit)
        {
            if (ModifyData.Buff is null) return;
            if (!unit.Buffs.Any(x => x.Id == ModifyData.Buff.Value)) return;

            Skill targetSkill = unit.Skills.Single(x => x.SkillData.Id == skillName);
            if (targetSkill is null) return;
            
            targetSkill.RecoverPower(spCount, withTip, ignoreOpening);
        }
    }
}
