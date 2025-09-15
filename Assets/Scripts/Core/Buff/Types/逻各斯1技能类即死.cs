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
        private bool forceKill;
        private float healPercentage;
        private float targetOriginalHp;
        private Skill chainSkill = new Skill();
        private List<Unit> validTargets = new List<Unit>();
        public override void Init()
        {
            base.Init();

            if (!this.Unit.IfAlive)
            {
                Log.Debug($"targetOriginalHp: {this.targetOriginalHp}");
                this.Finish();
                return;
            }

            this.targetOriginalHp = this.Unit.Hp;

            this.healPercentage = base.BuffData.Data.GetFloat("Recover", 0f);

            if (this.healPercentage > 0f && this.Skill.Unit != null)
            {
                this.Skill.Unit.Hp += this.healPercentage * this.Skill.Unit.MaxHp;
            }

            this.forceKill = base.BuffData.Data.GetBool("强制击杀");

            if (this.Unit is 敌人)
            {
                this.Unit.Hp = -1f;

                base.Battle.TriggerDatas.Push(new TriggerData
                {
                    Target = this.Unit
                });

                this.Unit.Trigger(TriggerEnum.致命);
                base.Battle.TriggerDatas.Pop();

                if (this.Unit.Hp <= 0f)
                {
                    base.Battle.TriggerDatas.Push(new TriggerData
                    {
                        Target = this.Unit
                    });
                    base.Battle.TriggerDatas.Pop();
                }
                
                if (this.Unit.Hp <= 0f)
                {
                    this.Unit.Hp = 0f;
                    this.Unit.DoDie(this.Skill.Unit);
                }
                

                this.ExecuteChainReaction();

            }


            this.Finish();
        }

        public override void Apply()
        {
            base.Apply();
        }

        public virtual void ExecuteChainReaction()
        {


            if (this.Skill.Unit == null)
            {
                return;
            }

            /*检查是否有特定Buff，选择不同的追击技能
            if (this.Skill.Unit.Buffs.Any(buff => buff.BuffData.Id == "逻各斯Z模组属性补正"))
            {
                this.chainSkill = this.Skill.Unit.Skills.FirstOrDefault(skill => skill.SkillData.Id == "逻各斯1Z光环追击");
                Log.Debug($"使用Z模组技能: {this.chainSkill?.SkillData.Id}");
            }
            else
            {
                this.chainSkill = this.Skill.Unit.Skills.FirstOrDefault(skill => skill.SkillData.Id == "逻各斯1光环追击");
                Log.Debug($"使用普通技能: {this.chainSkill?.SkillData.Id}");
            }

            if (this.chainSkill == null)
            {
                Log.DebugError("未找到有效的连锁技能");
                return;
            }
            */
            List<Vector2Int> attackPoints = this.Skill.Unit.GetNowAttackSkill().AttackPoints;

            HashSet<Unit> unitsInRange = base.Battle.FindAll(attackPoints, 2, true);

            this.validTargets.Clear();
            this.validTargets.AddRange(unitsInRange);

            if (this.validTargets.Contains(this.Unit))
            {
                this.validTargets.Remove(this.Unit);
            }

            // 移除所有非敌人的单位
            for (int i = this.validTargets.Count - 1; i >= 0; i--)
            {
                if (!(this.validTargets[i] is 敌人))
                {
                    this.validTargets.RemoveAt(i);
                }
            }


            if (this.validTargets.Count > 0)
            {
                int index = base.Battle.Random.Next(0, this.validTargets.Count);
                Unit selectedTarget = this.validTargets[index];

                Bullet bullet = base.Battle._CreateBullet(
                    Database.Instance.GetIndex<BulletData>("罗1子弹"),
                    new Vector3(this.Unit.Position.x, 0.5f, this.Unit.Position.z),
                    new Vector3(selectedTarget.Position.x, 0.5f, selectedTarget.Position.z),
                    selectedTarget,
                    this.targetOriginalHp,
                    this.Skill);
            }
        }

    }
}