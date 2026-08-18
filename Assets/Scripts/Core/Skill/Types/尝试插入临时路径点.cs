using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 尝试插入临时路径点 : Skill
    {
        public Vector3 pos = new Vector3(float.MaxValue, 0, float.MaxValue);
        public string useMod = "";
        public float lasttime = 0;
        public CountDown lastTime;
        public override void Init()
        {
            base.Init();
            useMod = SkillData.Data.GetStr("UseMod");
            lasttime = SkillData.Data.GetFloat("LastTime");
            lastTime = new CountDown(lasttime);
        }
        public override void Update()
        {
            base.Update();
            lastTime.Update(SystemConfig.DeltaTime);
        }
        public override void Start()
        {
            //Debug.Log("尝试插入临时路径点");
            lastTime.Set(lasttime);
            switch (useMod)
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
                if (target is Units.敌人 u)
                {
                    if (lastTime.Finished())
                        break;
                    if (u.AddTmpPathPoint(pos, lastTime.value))
                    {
                        //Debug.Log("插入临时路径点成功:" + pos +　"lasttime:" + lastTime.value);
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
