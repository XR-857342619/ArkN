using System;
using UnityEngine;
using Buffs;
using System.Collections.Generic;
using System.Linq;

namespace Buffs
{
    public class 锁血 : Buff, IShield
    {
        public float triggerRate;
        public float triggerPoint;
        public bool canRecover;
        bool isTriggered;
        float targetHp;
        int orderCode;
        public int OrderCode
        {
            get { return orderCode; }
        }

        public override void Init()
        {
            base.Init();
            triggerRate = BuffData.Data.GetFloat("TriggerRate");
            triggerPoint = BuffData.Data.GetFloat("TriggerPoint");
            canRecover = BuffData.Data.GetBool("CanRecover");
            orderCode = BuffData.Data.GetInt("OrderCode",0);
        }
        public override void Reset()
        {
            base.Reset();
            //Init();
            isTriggered = false;
            targetHp = 0;
        }
        public override void Update()
        {
            if (triggerRate == 0 && triggerPoint == 0) return;
            base.Update();
            if (!isTriggered)
            {
                float percentThreshold = Unit.MaxHp > 0 ? Unit.MaxHp * triggerRate : 0;
                float finalTriggerThreshold = Mathf.Max(percentThreshold, triggerPoint); // 取更严格的阈值

                if (Unit.Hp < finalTriggerThreshold)
                {
                    isTriggered = true;
                    targetHp = finalTriggerThreshold;
                    Unit.Hp = targetHp;
                }
            }
            if (isTriggered)
            {
                if (Unit.Hp > targetHp)
                {
                    if (!canRecover)
                        Unit.Hp = targetHp;
                }
                else
                    Unit.Hp = targetHp;
            }
        }
        //Unit.RewriteDamage = MinResponseLimit;
        public override void Finish()
        {
            base.Finish();
            isTriggered = false;
            targetHp = 0;
        }
        public void Absorb(DamageInfo damageInfo)
        {
            if (isTriggered)
            {
                // 计算“当前Hp - 伤害”后的剩余Hp
                float remainingHp = Unit.Hp - damageInfo.FinalDamage;
                // 若剩余Hp低于targetHp，仅承受“当前Hp - targetHp”的伤害（确保Hp不低于targetHp）
                if (remainingHp < targetHp)
                {
                    damageInfo.FinalDamage = Unit.Hp - targetHp;
                }
                // 若剩余Hp >= targetHp，伤害正常生效（由Update逻辑处理是否允许超过targetHp）
            }
        }
    }
}
