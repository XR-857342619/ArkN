using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 赋予被动 : Skill
    {
        public object[] SkillIds;
        public override void Init()
        {
            base.Init();
            //targetMod = SkillData.Data.GetStr("TargetMod");
            SkillIds = SkillData.Data.GetArray("技能Id列表");
        }

        public override void Start()
        {
            //base.Start();
            //Debug.Log("赋予被动技能开始");
            FindTarget();
            if (Targets is null || Targets.Count == 0) return;
            foreach (var target in Targets)
            {
                //Debug.Log("赋予被动技能给：" + target.UnitData.Id);
                giveSkill(target);
            }
        }
        public void giveSkill(Unit target)
        {
            if (SkillIds is null || SkillIds.Length == 0) return;
            if (target is null) return;
            foreach (var skillId in SkillIds)
            {
                string skillid = Convert.ToString(skillId);
                target.LearnSkill(Database.Instance.GetIndex<SkillData>(skillid));
            }
        }
    }
}
