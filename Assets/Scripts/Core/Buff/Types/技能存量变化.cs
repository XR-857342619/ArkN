using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Buffs
{
    /// <summary>
    /// 修改目标单位某个技能的 Opening 值（可增加/减少）。
    /// 配置字段：
    ///   - Delay（float）：延迟生效时间，默认 0 立即生效。
    ///   - SkillId（int）：要修改的技能 ID：
    ///         -1：主技能（MainSkill）
    ///         -2：当前攻击技能（AttackingSkill）——注意子弹命中后可能已被清空
    ///         -3：所有技能
    ///         -4：本次攻击的来源技能（即 this.Skill）
    ///         其他正数：指定技能 ID
    ///   - ModifyValue（float）：要增加/减少的数值（正数增加，负数减少）。
    /// </summary>
    public class 技能存量变化 : Buff
    {
        private float delayTime;
        private CountDown delayTimer;
        private int skillId;
        private float modifyValue;
        private Skill sourceSkill;

        public override void Init()
        {
            base.Init();

            sourceSkill = this.Skill;
            delayTime = BuffData.Data.GetFloat("Delay", 0f);
            modifyValue = BuffData.Data.GetFloat("ModifyValue", 0f);
            skillId = BuffData.Data.GetInt("SkillId", -1);

            if (modifyValue == 0f)
            {
                Finish();
                return;
            }

            if (delayTime <= 0f)
            {
                ApplyModification();
                Finish();
            }
            else
            {
                delayTimer = new CountDown(delayTime);
            }
        }

        public override void Update()
        {
            base.Update();

            if (delayTimer != null && delayTimer.Update(SystemConfig.DeltaTime))
            {
                ApplyModification();
                Finish();
            }
        }

        private void ApplyModification()
        {
            if (Unit == null) return;

            var targetSkills = GetTargetSkills();
            if (targetSkills == null || targetSkills.Count == 0) return;

            foreach (var skill in targetSkills)
            {
                if (skill == null || skill.Opening.Finished()) continue;

                float oldVal = skill.Opening.value;
                float newVal = oldVal + modifyValue;

                // 防止负值
                if (newVal < 0f) newVal = 0f;

                skill.Opening.Set(newVal);

                // 如果本次是减少操作，且剩余时间被削减到接近 0，执行清理
                if (modifyValue < 0f && newVal <= 0.001f)
                {
                    if (Unit != null)
                    {
                        Unit.OverWriteAnimation = null;
                        Unit.State = StateEnum.Idle;
                        // 可选：可以在此处停止攻击动作等
                        // Unit.AttackingAction?.Finish();
                        Log.Debug($"[技能存量变化] 技能 {skill.SkillData?.Name} Opening 归零，强制设为 Idle");
                    }
                }

                Log.Debug($"[技能存量变化] 技能 {skill.SkillData?.Name} Opening: {oldVal} -> {newVal}");
            }
        }

        private List<Skill> GetTargetSkills()
        {
            var list = new List<Skill>();

            if (skillId == -1)
            {
                if (Unit.MainSkill != null) list.Add(Unit.MainSkill);
            }
            else if (skillId == -2)
            {
                if (Unit.AttackingSkill != null) list.Add(Unit.AttackingSkill);
            }
            else if (skillId == -3)
            {
                list.AddRange(Unit.Skills);
            }
            else if (skillId == -4)
            {
                if (sourceSkill != null) list.Add(sourceSkill);
            }
            else
            {
                var skill = Unit.Skills.FirstOrDefault(s => s.Id == skillId);
                if (skill != null) list.Add(skill);
            }

            return list;
        }
    }
}