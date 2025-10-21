using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 余火墙 : Skill
    {
        public Unit target;
        public Unit source;
        public Skill sourceSkill;
        public Vector3 pointA;    // 第一个点
        public Vector3 pointB;    // 第二个点
        public Vector3 linePoint; // 直线经过的点
        public Vector2 lineDirection; // 直线方向（xz平面）
        public DamageTypeEnum damageType = DamageTypeEnum.general;
        public bool ignoreDamageTypeLimit;

        public override void Init()
        {
            base.Init();
            ignoreDamageTypeLimit = !DamageTypeEnum.TryParse(SkillData.Data.GetStr("DamageTypeLimit"), out damageType);
        }
        public override void FindTarget()
        {
            if (Battle.TriggerDatas.Count > 0)
            {
                //正在事件当中，技能去取事件目标
                var t = Battle.TriggerDatas.Peek().User;
                if (t != null && CanUseTo(t))
                    target = t;
                var s = Battle.TriggerDatas.Peek().Target;
                if (s != null && CanUseTo(s))
                    source = s;
                var skill = Battle.TriggerDatas.Peek().Skill;
                if (skill != null)
                    sourceSkill = skill;
            }
            pointA = target.Position;
            pointB = source.Position;
            linePoint = Unit.Position;
            if (Unit is Units.干员 op)
                lineDirection = DirectionHelper.DirectionToInt(op.Direction_E);
            else if (Unit is Units.敌人 en)
                lineDirection = en.Direction;
            if (target != null && source != null)
            {
                if (ArePointsOnOppositeSides())
                    if (sourceSkill.SkillData.DamageType == damageType || ignoreDamageTypeLimit)
                        Targets.Add(target);
            }
            //base.Start();
        }
        bool ArePointsOnOppositeSides()
        {
            // 将Vector2方向转换为Vector3（xOz平面）
            Vector3 dir = new Vector3(lineDirection.x, 0, lineDirection.y);

            // 计算法向量（垂直于方向向量）
            Vector3 normal = new Vector3(-dir.z, 0, dir.x); // 或 (dir.z, 0, -dir.x)

            // 计算点A和点B相对于直线的有向距离
            float distanceA = normal.x * (pointA.x - linePoint.x) + normal.z * (pointA.z - linePoint.z);
            float distanceB = normal.x * (pointB.x - linePoint.x) + normal.z * (pointB.z - linePoint.z);

            // 比较符号
            return Mathf.Sign(distanceA) != Mathf.Sign(distanceB);
        }
    }
}
