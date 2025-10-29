using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    //与普通技能不同的是，就算攻击范围里没有有效模板，这类技能也能被释放
    public class 溢出打数转化 : Skill
    {
        //public override void Start()
        //{
        //    //if (!Cooldown.Finished()) return;
        //    base.Start();
        //}
        protected override void orderTargets(List<Unit> targets)
        {
            //List<>
            targets.RemoveAll(x => !CanUseTo(x));
            if (targets.Count > 0)
            {
                //首先计算出所有目标的仇恨优先级，然后再选出攻击个数的实际目标
                SortTarget(targets);
                targets.AddRange(Battle.AllUnits.Where(x => x.UnitData?.Name == SkillData.Data?.GetStr("ExTarget") && (SkillData.DeadFind ? true : x.IfAlive)));
                FilterTarget(targets);
            }
        }
        protected override int GetTargetCount()
        {
            int result = 0;
            if (Battle.TriggerDatas.Count == 0) return 0;
            if (!SkillData.Skills.ToList().Contains(Battle.TriggerDatas.Peek().Skill.Id)) return 0;
            
            result = (int)Battle.TriggerDatas.Peek().Count;
            foreach (var modify in Modifies)
            {
                if (modify is ITargetModify targetModify)
                {
                    result = targetModify.Modify(result, Unit);
                }
            }
            Debug.Log(result);
            return result;
        }
    }
}
