using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 传送 : Skill
    {
        public Vector3 pos = new Vector3(float.MaxValue, 0, float.MaxValue);
        public string useMod = "Force";
        public string targetPos = "";
        public float distanceLimit = 0;
        public override void Init()
        {
            base.Init();
            targetPos = SkillData.Data.GetStr("TargetPos");
            useMod = SkillData.Data.GetStr("UseMod");
            if (useMod == "limitDistance")
                distanceLimit = SkillData.Data.GetFloat("DistanceLimit");
        }

        public override void Start()
        {
            switch (targetPos)
            {
                case "useSelfPos":
                    //Debug.Log("useSlefPos:" + Unit.Position);
                    pos = Unit.Position;
                    break;
                case "useTargetPos":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            pos = targets[0].Position;
                        }
                    }
                    else
                    {
                        pos = Unit.Position;
                    }
                    break;
                case "useAttackPoint":
                    foreach (var point in AttackPoints)
                    {
                        if (point != Unit.Position2)
                        {
                            pos.x = point.x;
                            pos.y = point.y;
                        }
                    }
                    break;
            }
            base.Start();
        }
        public override void Cast()
        {
            foreach (var target in Targets)
            {
                if (target is Units.敌人 enemy)
                {
                    if (useMod == "Force")
                        enemy.Position = pos;
                    else if (useMod == "IfCanArrvie")
                    {
                        if (enemy.IsCanArrive(new PathPoint() { Pos = enemy.Position }, new PathPoint() { Pos = pos }))
                            enemy.Position = pos;
                    }
                    else if (useMod == ">limitDistance")
                    {
                        var dis = Vector3.Distance(enemy.Position, pos);
                        if (dis >= distanceLimit)
                            enemy.Position = pos;
                    }
                    else if (useMod == "<limitDistance")
                    {
                        var dis = Vector3.Distance(enemy.Position, pos);
                        if (dis <= distanceLimit)
                            enemy.Position = pos;
                    }
                }
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
