using System;
using System.Linq;

namespace Buffs
{
    public class 强制结束技能 : Buff
    {
        public override void Init()
        {
            // 免疫忽略（如果基类有该字段可设）
            // this.ImmuneIgnore = true;

            base.Init();

            EndCurrentSkill();

            // 若需要沉默效果可取消注释
            // if (Unit != null) Unit.IfSilence = true;
        }

        public override void Update()
        {
            base.Update();

            if (!Dead && Unit != null)
            {
                // 持续期间不断检查并强制结束（如果 Opening 被重新开启，则会再次强制结束）
                EndCurrentSkill();
            }
        }

        /// <summary>
        /// 强制结束单位当前主技能（如果正在开启），并清理动画状态
        /// </summary>
        private void EndCurrentSkill()
        {
            if (Unit?.MainSkill == null)
                return;

            if (!Unit.MainSkill.Opening.Finished())
            {
                // 强制结束 Opening
                Unit.MainSkill.Opening.Finish();

                // 触发技能结束事件
                Unit.Trigger(TriggerEnum.技能结束);

                // 清理动画和状态，避免动画卡死
                Unit.OverWriteAnimation = null;
                Unit.State = StateEnum.Idle;  // 通过 SetStatus 自动中断攻击动作并切换动画

                Log.Debug($"[强制结束技能] 强制结束 {Unit.UnitData?.Name} 的主技能，并设为 Idle");
            }
        }
    }
}