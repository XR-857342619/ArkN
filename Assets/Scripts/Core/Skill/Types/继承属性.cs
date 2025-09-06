using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 继承属性 : Skill
    {
        protected object[] Attributes;
        //protected float[] values;
        public string targetMod = "";
        public Unit AttributeSource;
        public override void Init()
        {
            base.Init();
            targetMod = SkillData.Data.GetStr("TargetMod");
            Attributes = SkillData.Data.GetArray("Attributes");
        }

        public override void Start()
        {
            base.Start();
            switch (targetMod)
            {
                case "useFixedUnitAttribute":
                    AttributeSource = Battle.AllUnits.Find(u => u.UnitData.Name == SkillData.Data.GetStr("FixedUnitID"));
                    break;
                case "useSkillsTargetAttribute":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            AttributeSource = targets[0];
                        }
                    }
                    else
                    {
                        AttributeSource = Targets[0];
                    }
                    break;
                case "useParentAttribute":
                    if (Unit is Units.干员 op)
                        AttributeSource = op.Parent;
                    else if (Unit is Units.敌人 em)
                        AttributeSource = em.Parent;
                    else
                        AttributeSource = Unit;
                    break;
                case "useSelfTargetAttribute":
                    AttributeSource = Targets[0];
                    break;
                default:
                    AttributeSource = Targets[0]?? Unit;
                    break;

            }
        }
        public override void Cast()
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                string fieldName = (string)Attributes[i];
                var targetField = AttributeSource.GetType().GetField(fieldName);
                var selfField = Unit.GetType().GetField(fieldName);
                if (targetField == null || selfField == null)
                {
                    Log.Debug($"{Unit.UnitData.Name}/{AttributeSource.UnitData.Name} 没有 属性 {fieldName}");
                    continue;
                }
                selfField.SetValue(Unit, targetField.GetValue(AttributeSource));
            }
                base.Cast();
        }

        //protected override float GetSkillDelay(string[] animationName, string[] lastState, out float fullDuration, out float beginDuration)
        //{
        //    var f1 = Unit.UnitModel.GetAnimationDuration(SkillData.ModelAnimation[0]);
        //    var f2 = Unit.UnitModel.GetAnimationDuration(SkillData.ModelAnimation[1]);
        //    fullDuration = f1 + f2;
        //    beginDuration = 0;
        //    return f1;
        //    //return base.GetSkillDelay(animationName, lastState, out fullDuration, out beginDuration);
        //}
    }
}
