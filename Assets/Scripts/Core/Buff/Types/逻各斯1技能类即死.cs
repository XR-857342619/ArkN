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
            Log.Debug("逻各斯1技能类即死 Buff 初始化开始");

            base.Init();

            if (!this.Unit.IfAlive)
            {
                Log.Debug("单位已死亡，直接结束Buff");
                this.Finish();
                return;
            }

            this.targetOriginalHp = this.Unit.Hp;
            Log.Debug($"目标原始HP: {targetOriginalHp}");

            this.healPercentage = base.BuffData.Data.GetFloat("Recover", 0f);
            Log.Debug($"治疗百分比: {healPercentage}");

            if (this.healPercentage > 0f && this.Skill.Unit != null)
            {
                Log.Debug($"执行治疗，治疗量: {this.healPercentage * this.Skill.Unit.MaxHp}");
                this.Skill.Unit.Hp += this.healPercentage * this.Skill.Unit.MaxHp;
            }

            this.forceKill = base.BuffData.Data.GetBool("强制击杀");
            Log.Debug($"强制击杀: {forceKill}");

            if (this.Unit is 敌人)
            {
                Log.Debug("目标是敌人，执行即死逻辑");
                this.Unit.Hp = -1f;

                base.Battle.TriggerDatas.Push(new TriggerData
                {
                    Target = this.Unit
                });

                Log.Debug("触发致命事件");
                this.Unit.Trigger(TriggerEnum.致命);
                base.Battle.TriggerDatas.Pop();

                if (this.Unit.Hp <= 0f)
                {
                    Log.Debug("单位HP<=0，准备触发濒死事件");
                    base.Battle.TriggerDatas.Push(new TriggerData
                    {
                        Target = this.Unit
                    });
                    base.Battle.TriggerDatas.Pop();
                }

                if (this.Unit.Hp <= 0f)
                {
                    Log.Debug("执行单位死亡");
                    this.Unit.Hp = 0f;
                    this.Unit.DoDie(this.Skill.Unit);
                }

                Log.Debug("执行连锁反应");
                this.ExecuteChainReaction();
            }
            else
            {
                Log.Debug("目标不是敌人，不执行即死");
            }

            this.Finish();
            Log.Debug("Buff结束");
        }

        public override void Apply()
        {
            Log.Debug("Buff应用");
            base.Apply();
        }

        public virtual void ExecuteChainReaction()
        {
            Log.Debug("执行连锁反应方法开始");

            if (this.Skill.Unit == null)
            {
                Log.Debug("Skill.Unit 为 null，无法执行连锁反应");
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
            Log.Debug($"攻击点数: {attackPoints.Count}");

            HashSet<Unit> unitsInRange = base.Battle.FindAll(attackPoints, 2, true);
            Log.Debug($"范围内单位数: {unitsInRange.Count}");

            this.validTargets.Clear();
            this.validTargets.AddRange(unitsInRange);

            if (this.validTargets.Contains(this.Unit))
            {
                this.validTargets.Remove(this.Unit);
                Log.Debug("移除原始目标");
            }

            // 移除所有非敌人的单位
            for (int i = this.validTargets.Count - 1; i >= 0; i--)
            {
                if (!(this.validTargets[i] is 敌人))
                {
                    this.validTargets.RemoveAt(i);
                }
            }

            Log.Debug($"有效目标数: {validTargets.Count}");
            
            if (this.validTargets.Count > 0)
            {
                int index = base.Battle.Random.Next(0, this.validTargets.Count);
                Unit selectedTarget = this.validTargets[index];

                Bullet bullet = base.Battle._CreateBullet(
                Database.Instance.GetIndex<BulletData>("罗1子弹"),
                new Vector3(this.Unit.Position.x, 0.5f, this.Unit.Position.z),
                new Vector3(selectedTarget.Position.x, 0.5f, selectedTarget.Position.z),
                selectedTarget,
                this.targetOriginalHp); // 修正：直接传递值，而不是赋值语句// 原变量名: sawt
            }
                Log.Debug("执行连锁反应方法结束");
            }
        
    }
}