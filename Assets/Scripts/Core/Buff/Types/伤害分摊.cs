using System;
using UnityEngine;
using Buffs;
using System.Collections.Generic;
using System.Linq;

namespace Buffs
{
    public class 伤害分摊 : Buff, IDamageRewrite
    {
        public float shareRate;
        public DamageTypeEnum damageType;
        public bool onlyFatal;
        public bool isMainUnit;
        public int orderCode;
        public string group;
        public int OrderCode
        {
            get { return orderCode; }
        }

        public override void Init()
        {
            base.Init();
            shareRate = Mathf.Clamp(BuffData.Data.GetFloat("ShareRate", 1.0f), 0, 1);
            if (!Enum.TryParse(BuffData.Data.GetStr("DamageType", "general"), out damageType))
                damageType = DamageTypeEnum.general;
            orderCode = BuffData.Data.GetInt("OrderCode", 0);
            onlyFatal = BuffData.Data.GetBool("OnlyFatal");
            isMainUnit = BuffData.Data.GetBool("IsMainUnit");
            group = BuffData.Data.GetStr("Group", "");
        }
            //Unit.RewriteDamage = MinResponseLimit;
        public void DamageRewrite(DamageInfo damageInfo)
        {
            if(!isMainUnit) return;
            if (onlyFatal && Unit.Hp - damageInfo.FinalDamage > 0) return;
            List<Unit> shareList = Battle.AllUnits
                .Where(u => u.Buffs.OfType<伤害分摊>().Any(b => b.group == group))
                .ToList();
            List<Unit> mainList = shareList.FindAll(u => u.Buffs.Any(b => b is Buffs.伤害分摊 B && B.isMainUnit));

            if (damageInfo.DamageType == damageType || damageType == DamageTypeEnum.general)
            {
                if (mainList.Count > 0)
                {
                    shareList.RemoveAll(x => mainList.Contains(x));
                    if (shareList.Count == 0) return; // 避免除零
                    float totalShareDamage = onlyFatal ? (damageInfo.FinalDamage - Unit.Hp) * shareRate : damageInfo.FinalDamage * shareRate;  // 先算出要分摊的总伤害
                    damageInfo.FinalDamage -= totalShareDamage;  // 原伤害减去分摊部分
                    float damage = totalShareDamage / shareList.Count;
                    foreach (Unit unit in shareList)
                    {
                        unit.Damage(new DamageInfo()
                        {
                            DamageRate = 1,
                            DamageType = DamageTypeEnum.general,
                            Attack = damage // 直接使用传入的基础伤害
                        });
                        //unit.Hp -= damage;
                    }
                }
                else
                {
                    if (shareList.Count <= 1) return;  // 避免除零
                    float totalShareDamage = onlyFatal ? (damageInfo.FinalDamage - Unit.Hp) * shareRate : damageInfo.FinalDamage * shareRate;
                    float damage = totalShareDamage / (shareList.Count - 1);
                    damageInfo.FinalDamage *= (1 - shareRate);
                    foreach (Unit unit in shareList)
                    {
                        if (unit == Unit) continue;
                        unit.Damage(new DamageInfo()
                        {
                            DamageRate = 1,
                            DamageType = DamageTypeEnum.general,
                            Attack = damage // 直接使用传入的基础伤害
                        });
                        //unit.Hp -= damage;
                    }
                }
            }
        }
    }
}
