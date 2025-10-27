using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 塑灵术士式索敌 : Skill
    {
        // 使自身索敌可以获取到被token阻挡的单位
        //protected List<Unit> tempTargets = new List<Unit>();
        //protected List<Unit> tempTargetsFromEvent = new List<Unit>();
        //protected List<Unit> tempTargetsFromAttackRange = new List<Unit>();
        public override List<Unit> GetAttackTarget()
        {
            //Log.Debug(SkillData.Id + "获取攻击目标");
            tempTargets.Clear();
            tempTargetsFromEvent.Clear();
            tempTargetsFromAttackRange.Clear();
            if (SkillData.UseEventUser && Battle.TriggerDatas.Count > 0)
            {
                //正在事件当中，技能去取事件目标
                var t = Battle.TriggerDatas.Peek().User;
                if (t != null && CanUseTo(t))
                    tempTargetsFromEvent.Add(t);
            }
            if (SkillData.UseEventTarget && Battle.TriggerDatas.Count > 0)
            {
                //正在事件当中，技能去取事件目标
                //Debug.Log("正在事件"+ Battle.TriggerDatas.Peek().ToString() +"当中");
                var t = Battle.TriggerDatas.Peek().Target;
                if (t != null && CanUseTo(t))
                    tempTargetsFromEvent.Add(t);
            }
            //仅自己的情况下 优化一下
            if (tempTargets.Count == 0 && SkillData.TargetFilter == SkillTargetFilterEnum.仅自己)
            {
                tempTargets.Add(Unit);
                return tempTargets;
            }
            //if (!SkillData.UseEventTarget && !SkillData.UseEventUser)
            //{
            if (AttackPoints == null && !SkillData.AttackAreaWithMain)//根据攻击范围进行索敌
            {
                tempTargetsFromAttackRange.AddRange(Battle.FindAll(Unit.Position2, SkillData.AttackRange * Unit.AttackRange, SkillData.TargetTeam, !SkillData.DeadFind));
            }
            else
            {
                var attackPoints = SkillData.AttackAreaWithMain ? Unit.GetNowUseingSkill().AttackPoints : AttackPoints;
                tempTargetsFromAttackRange.AddRange(Battle.FindAll(attackPoints, SkillData.TargetTeam, !SkillData.DeadFind));
            }

            if (tempTargetsFromEvent.Count > 0 && tempTargetsFromAttackRange.Count > 0)
                tempTargets.AddRange(tempTargetsFromAttackRange.FindAll(x => tempTargetsFromEvent.Contains(x)));
            else
            {
                tempTargets.AddRange(tempTargetsFromEvent);
                tempTargets.AddRange(tempTargetsFromAttackRange);
            }
            if (SkillData.SkillCondition is not null && Casting.Finished())
            {
                var evaluator = new ExpressionEvaluator(Unit, tempTargets);
                tempTargets = evaluator.Filter(SkillData.SkillCondition);
            }

            if (Unit is Units.干员 op)
            {
                foreach (var child in op.Children)
                {
                    var token = child as Units.干员;
                    if (token == null) continue;
                    tempTargets.AddRange(token.StopUnits.Where(t => !tempTargets.Contains(t)));
                }
            }

            orderTargets(tempTargets);

            return tempTargets;
        }
    }
}
