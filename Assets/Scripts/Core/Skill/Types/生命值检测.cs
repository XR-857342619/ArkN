using System;
using Units;
using UnityEngine;

namespace Skills
{
    public class 生命值检测技能 : Skill
    {
        // 配置参数
        private float hpFactor = 1.5f; // 生命值系数

        public override void Init()
        {
            base.Init();

            // 从技能配置中读取参数
            hpFactor = SkillData.Data.GetFloat("HpFactor", 1.5f);
        }

        public bool CanUseTo(Unit target)
        {
            // 先执行基础的技能条件检查
            if (!base.CanUseTo(target))
                return false;

            // 检查生命值条件
            bool hpCondition = target.Hp <= this.Unit.Attack * hpFactor;

            // 检查重写伤害条件
            //bool rewriteDamageCondition = ignoreRewritedamage || target.RewriteDamage <= 0f;

            // 检查复活状态条件
            //bool reviveCondition = ignoreRevive || !target.IfRevive;

            return hpCondition/*&& rewriteDamageCondition && reviveCondition*/;
        }
    }
}