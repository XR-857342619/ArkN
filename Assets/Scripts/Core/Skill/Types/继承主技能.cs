using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 继承主技能 : Skill
    {
        protected Skill mainSkill;
        public string targetMod = "";
        public Unit SkillSource;
        public override void Init()
        {
            base.Init();
            targetMod = SkillData.Data.GetStr("TargetMod");
        }

        public override void Start()
        {
            base.Start();
            switch (targetMod)
            {
                case "useFixedUnitSkill":
                    SkillSource = Battle.AllUnits.Find(u => u.UnitData.Name == SkillData.Data.GetStr("FixedUnitID"));
                    break;
                case "useSkillsTargetSkill":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            SkillSource = targets[0];
                        }
                    }
                    else
                    {
                        SkillSource = Targets[0];
                    }
                    break;
                case "useParentSkill":
                    if (Unit is Units.干员 op)
                        SkillSource = op.Parent;
                    else if (Unit is Units.敌人 em)
                        SkillSource = em.Parent;
                    else
                        SkillSource = Unit;
                    break;
                case "useSelfTargetSkill":
                    SkillSource = Targets?[0] ?? null;
                    break;
                default:
                    SkillSource = Targets?[0]?? Unit;
                    break;

            }
        }
        public override void Cast()
        {
            if (SkillSource != null)
            {
                Unit.MainSkill = new Skill();
                Unit.MainSkill.CopyState(SkillSource.MainSkill);
                Unit.MainSkill.Unit = Unit;
            }
            else if (targetMod == "useSkillId")
            {
                var skillData = Database.Instance.Get<SkillData>(Database.Instance.GetIndex<SkillData>(SkillData.Data.GetStr("SkillId")));
                Unit.MainSkill = Unit.LearnSkill(skillData);
                Unit.MainSkill.Init();
            }
            base.Cast();
        }
    }
}
