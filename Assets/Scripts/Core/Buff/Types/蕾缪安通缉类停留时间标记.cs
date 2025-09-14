using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using BattleUI;
using Buffs;
using Units;
using UnityEngine;

namespace Buffs
{
    public class 蕾缪安通缉类停留时间标记 : Buff
    {
        // 存储每个敌人在攻击范围内的停留时间
        private Dictionary<Unit, float> enemyStayTimes = new Dictionary<Unit, float>();

        // 通缉所需的停留时间（从配置读取，默认8秒）
        private float requiredStayTime = 8f;

        // 存储被通缉的敌人列表
        private HashSet<Unit> wantedEnemies = new HashSet<Unit>();

        // 要添加的Buff名称数组
        private string[] wantedBuffNames;

        public override void Init()
        {
            base.Init();

            // 检查是否是干员，如果不是则移除Buff
            if (!(this.Unit is 干员))
            {
                this.Finish();
                return;
            }

            // 从配置读取所需的停留时间
            requiredStayTime = base.BuffData.Data.GetFloat("Time", 8f);

            // 从配置读取要添加的Buff名称
            object[] buffArray = base.BuffData.Data.GetArray("WantedBuffs");
            if (buffArray != null && buffArray.Length > 0)
            {
                wantedBuffNames = new string[buffArray.Length];
                for (int i = 0; i < buffArray.Length; i++)
                {
                    wantedBuffNames[i] = Convert.ToString(buffArray[i]);
                }
            }
            else
            {
                // 如果没有配置，使用默认Buff
                wantedBuffNames = new string[0];
                //Debug.LogWarning("蕾缪安通缉类停留时间标记: 未配置WantedBuffs");
            }
        }

        public override void Update()
        {
            base.Update();

            if (!this.Unit.IfAlive)
            {
                return;
            }

            // 获取攻击范围
            List<Vector2Int> attackPoints = this.Unit.GetNowAttackSkill().AttackPoints;
            if (attackPoints == null)
            {
                return;
            }

            // 获取当前在攻击范围内的所有敌人
            HashSet<Unit> currentEnemies = GetEnemiesInRange(attackPoints);

            // 更新敌人在范围内的停留时间
            UpdateEnemyStayTimes(currentEnemies);

            // 检查是否有敌人达到通缉条件
            CheckWantedEnemies();

            // 清理不再存活的通缉敌人
            CleanUpWantedEnemies();
        }

        // 获取攻击范围内的所有敌人
        private HashSet<Unit> GetEnemiesInRange(List<Vector2Int> points)
        {
            HashSet<Unit> enemies = new HashSet<Unit>();

            foreach (Vector2Int point in points)
            {
                foreach (Unit unit in base.Battle.UnitMap[point.x, point.y])
                {
                    if (unit.Alive() && unit.Team != this.Unit.Team) // 存活且是敌人
                    {
                        enemies.Add(unit);
                    }
                }
            }

            return enemies;
        }

        // 更新敌人在范围内的停留时间
        private void UpdateEnemyStayTimes(HashSet<Unit> currentEnemies)
        {
            // 移除不再范围内的敌人
            List<Unit> enemiesToRemove = new List<Unit>();
            foreach (Unit enemy in enemyStayTimes.Keys)
            {
                if (!currentEnemies.Contains(enemy) || !enemy.Alive())
                {
                    enemiesToRemove.Add(enemy);
                }
            }

            foreach (Unit enemy in enemiesToRemove)
            {
                enemyStayTimes.Remove(enemy);
            }

            // 增加当前在范围内的敌人的停留时间
            float deltaTime = SystemConfig.DeltaTime;
            foreach (Unit enemy in currentEnemies)
            {
                if (enemyStayTimes.ContainsKey(enemy))
                {
                    enemyStayTimes[enemy] += deltaTime;
                }
                else
                {
                    enemyStayTimes[enemy] = deltaTime;
                }
            }
        }

        // 检查是否有敌人达到通缉条件
        private void CheckWantedEnemies()
        {
            foreach (var entry in enemyStayTimes)
            {
                Unit enemy = entry.Key;
                float stayTime = entry.Value;

                if (stayTime >= requiredStayTime && !wantedEnemies.Contains(enemy))
                {
                    // 标记敌人为"通缉"
                    wantedEnemies.Add(enemy);

                    // 添加指定的Buff
                    AddWantedBuffsToEnemy(enemy);

                    // 这里可以添加通缉特效或其他视觉反馈
                    //Debug.Log($"敌人 {enemy.UnitData.Name} 已被通缉!");

                    // 可以在这里触发其他效果，比如播放音效、显示UI提示等
                }
            }
        }

        // 给被通缉的敌人添加Buff
        private void AddWantedBuffsToEnemy(Unit enemy)
        {
            if (wantedBuffNames == null || wantedBuffNames.Length == 0)
                return;

            foreach (string buffName in wantedBuffNames)
            {
                int buffId = Database.Instance.GetIndex<BuffData>(buffName);
                if (buffId >= 0)
                {
                    enemy.AddBuff(buffId, this.Skill, 0);
                }
                else
                {
                    //Debug.LogError($"蕾缪安通缉类停留时间标记: 找不到Buff '{buffName}'");
                }
            }
        }

        // 移除不再存活的通缉敌人
        private void CleanUpWantedEnemies()
        {
            List<Unit> toRemove = new List<Unit>();
            foreach (Unit enemy in wantedEnemies)
            {
                if (!enemy.Alive())
                {
                    toRemove.Add(enemy);
                }
            }

            foreach (Unit enemy in toRemove)
            {
                wantedEnemies.Remove(enemy);
            }
        }

        // 可选：添加一个方法来获取当前被通缉的敌人列表
        public HashSet<Unit> GetWantedEnemies()
        {
            return new HashSet<Unit>(wantedEnemies);
        }

        // 可选：添加一个方法来检查特定敌人是否被通缉
        public bool IsEnemyWanted(Unit enemy)
        {
            return wantedEnemies.Contains(enemy);
        }

        public override void Finish()
        {
            // Buff被移除时清理数据
            enemyStayTimes.Clear();
            wantedEnemies.Clear();
            base.Finish();
        }
    }
}