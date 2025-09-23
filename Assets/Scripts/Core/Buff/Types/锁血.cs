using System;
using UnityEngine;
using Buffs;
using System.Collections.Generic;
using System.Linq;

namespace Buffs
{
    public class 锁血 : Buff, IDamageRewrite
    {
        public float triggerRate;
        public float triggerPoint;
        public bool canRecover;
        bool isTriggered;
        //float targetHp;
        int orderCode;
        float finalTriggerThreshold;
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
        public override void Update()
        {
            if (triggerRate == 0 && triggerPoint == 0) return;
            base.Update();
            float percentThreshold = Unit.MaxHp > 0 ? Unit.MaxHp * triggerRate : 0;
            finalTriggerThreshold = Mathf.Max(percentThreshold, triggerPoint);
            if (!isTriggered)
            {
                if (Unit.Hp < finalTriggerThreshold)
                {
                    isTriggered = true;
                    //targetHp = finalTriggerThreshold;
                    Unit.Hp = finalTriggerThreshold;
                }
            }
            if (isTriggered)
            {
                if (Unit.Hp > finalTriggerThreshold)
                {
                    if (!canRecover)
                        Unit.Hp = finalTriggerThreshold;
                }
                else
                    Unit.Hp = finalTriggerThreshold;
            }
        }
        //Unit.RewriteDamage = MinResponseLimit;
        public override void Finish()
        {
            base.Finish();
            isTriggered = false;
            finalTriggerThreshold = 0;
        }
        public void DamageRewrite(DamageInfo damageInfo)
        {
            float remainingHp = Unit.Hp - damageInfo.FinalDamage;
            Debug.Log("锁血剩余Hp：" + remainingHp);
            if (remainingHp <= finalTriggerThreshold)
            {
                Unit.Hp = finalTriggerThreshold;
                damageInfo.FinalDamage = 0;
                isTriggered = true;
            }
            else if (isTriggered)
            {
                damageInfo.FinalDamage = Unit.Hp - finalTriggerThreshold;
            }
            Debug.Log("当前伤害" + damageInfo.FinalDamage);
        }
    }
}
