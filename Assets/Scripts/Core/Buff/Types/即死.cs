using System;
using Units;

namespace Buffs
{
    public class 即死 : Buff
    {
        public override void Init()
        {
            base.Init();

            // 检查单位是否存活
            if (!this.Unit.IfAlive)
            {
                this.Finish();
                return;
            }

            // 立即杀死单位
            this.Unit.DoDie(this.Skill?.Unit);

            // 触发致命效果相关事件
            base.Battle.TriggerDatas.Push(new TriggerData
            {
                Target = this.Unit
            });
            this.Unit.Trigger(TriggerEnum.致命);
            base.Battle.TriggerDatas.Pop();

            // 移除Buff
            this.Finish();
        }

        public override void Update()
        {
            base.Update();
            // 不需要额外更新逻辑，因为效果是立即的
        }
    }
}