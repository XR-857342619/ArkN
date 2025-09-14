using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bullets;
using Units;
using UnityEngine;

namespace Buffs
{
    public class 逻各斯1技能类即死 : Buff
    {
        // Token: 0x06001366 RID: 4966 RVA: 0x00089270 File Offset: 0x00087470
        public override void Init() // 原方法名: rro
        {
            //this.ImmuneIgnore = true;
            base.Init(); // 原方法名: rro

            if (!this.Unit.IfAlive)
            {
                this.Finish(); // 原方法名: rse
                return;
            }

            this.targetOriginalHp = this.Unit.Hp; // 原变量名: sawt
            this.healPercentage = base.BuffData.Data.GetFloat("Recover", 0f); // 原变量名: sai3, 原方法名: her

            if (this.healPercentage > 0f && this.Skill.Unit != null)
            {
                this.Skill.Unit.Hp += this.healPercentage * this.Skill.Unit.MaxHp;
            }

            this.forceKill = base.BuffData.Data.GetBool("强制击杀"); // 原变量名: sai2, 原方法名: hea

            if (this.Unit is 敌人)
            {
                this.Unit.Hp = -1f;
                base.Battle.TriggerDatas.Push(new TriggerData
                {
                    Target = this.Unit
                });

                this.Unit.Trigger(TriggerEnum.致命); // 原方法名: sta
                base.Battle.TriggerDatas.Pop();

                if (this.Unit.Hp <= 0f)
                {
                    base.Battle.TriggerDatas.Push(new TriggerData
                    {
                        Target = this.Unit
                    });
                    //base.Battle.Trigger(TriggerEnum.濒死); // 原方法名: rrh
                    base.Battle.TriggerDatas.Pop();
                }

                if (this.Unit.Hp <= 0f)
                {
                    this.Unit.Hp = 0f;
                    this.Unit.DoDie(this.Skill.Unit); // 原方法名: a3w
                }

                this.ExecuteChainReaction(); // 原方法名: ehr3
            }

            this.Finish(); // 原方法名: rse
        }

        // Token: 0x06001367 RID: 4967 RVA: 0x0000C320 File Offset: 0x0000A520
        public override void Apply() // 原方法名: ra3
        {
            base.Apply(); // 原方法名: ra3
            //this.ImmuneIgnore = true;
        }

        // Token: 0x06001368 RID: 4968 RVA: 0x00089428 File Offset: 0x00087628
        public virtual void ExecuteChainReaction() // 原方法名: ehr3
        {
            if (this.Skill.Unit == null)
            {
                return;
            }

            // 检查是否有特定Buff，选择不同的追击技能
            if (this.Skill.Unit.Buffs.Any(buff => buff.BuffData.Id == "逻各斯Z模组属性补正"))
            {
                this.chainSkill = this.Skill.Unit.Skills.FirstOrDefault(skill => skill.SkillData.Id == "逻各斯1Z光环追击");
            }
            else
            {
                this.chainSkill = this.Skill.Unit.Skills.FirstOrDefault(skill => skill.SkillData.Id == "逻各斯1光环追击");
            }

            List<Vector2Int> attackPoints = this.Skill.Unit.GetNowAttackSkill().AttackPoints; // 原方法名: srt
            HashSet<Unit> unitsInRange = base.Battle.FindAll(attackPoints, 2, true); // 原方法名: re2

            this.validTargets.Clear(); // 原变量名: sawr
            this.validTargets.AddRange(unitsInRange);

            if (this.validTargets.Contains(this.Unit))
            {
                this.validTargets.Remove(this.Unit);
            }

            for (int i = this.validTargets.Count - 1; i >= 0; i--)
            {
                if (!this.chainSkill._CanUseTo(this.validTargets[i])) // 原方法名: ars
                {
                    this.validTargets.Remove(this.validTargets[i]);
                }
            }

            if (this.validTargets.Count > 0)
            {
                int index = base.Battle.Random.Next(0, this.validTargets.Count);
                Unit selectedTarget = this.validTargets[index];

                Bullet bullet = base.Battle.CreateBullet( // 原方法名: reh
                    Database.Instance.GetIndex<BulletData>("逻各斯1弹道"), // 原方法名: s2h
                    new Vector3(this.Unit.Position.x, 0.5f, this.Unit.Position.z),
                    new Vector3(selectedTarget.Position.x, 0.5f, selectedTarget.Position.z),
                    selectedTarget,
                    this.chainSkill
                );

                //if (bullet is 逻各斯1子弹)
                //{
                 //   (bullet as 逻各斯1子弹).LogosBulletAttack = this.targetOriginalHp; // 原变量名: sawt
                //}
            }
        }
        private bool forceKill; // 原变量名: sai2
        private float healPercentage; // 原变量名: sai3
        private float targetOriginalHp; // 原变量名: sawt
        private Skill chainSkill = new Skill(); // 原变量名: sawe
        private List<Unit> validTargets = new List<Unit>(); // 原变量名: sawr
    }
}