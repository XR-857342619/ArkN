using System.Collections.Generic;
using UnityEngine;

namespace Bullets
{
    public class 链式弹道 : Bullet
    {
        // 私有字段
        private float moveHeight;
        private float tickTime;
        private string skillId;
        private int maxLinkNum;
        private int linkNum;
        public float reductionRate;
        private float reductionBase;
        private List<Unit> linkedTargets;
        private bool canBack;
        private List<Unit> usedTargets = new List<Unit>();
        private Unit lastTarget;
        private Unit tempUnit;
        private Skill findTargetSkill;
        private Vector3 startPositionCache;
        private bool isInitialized;
        private bool isDirectHit; // 标记是否为直接命中（起点终点相同）

        // 属性
        public int LinkNum => linkNum;
        public float ReductionRate => reductionRate;

        public override void Init()
        {
            base.Init();

            // 检查起点和终点是否相同
            isDirectHit = Vector3.Distance(StartPosition, GetTargetPos(Target)) < Mathf.Epsilon;

            if (Target == null || !Target.Alive())
            {
                Finish();
                return;
            }

            // 缓存初始位置
            startPositionCache = StartPosition;

            // 配置参数
            moveHeight = BulletData.Data.GetFloat("MoveHeight");
            maxLinkNum = BulletData.Data.GetInt("MaxLinkNum");
            linkNum = 0;
            reductionBase = BulletData.Data.GetFloat("ReductionRate", 0);
            reductionRate = 1 - reductionBase * linkNum;
            canBack = BulletData.Data.GetBool("CanBack");
            skillId = BulletData.Data.GetStr("SkillId");

            // 设置目标位置
            TargetPos = GetTargetPos(Target);

            // 设置弹道方向与旋转
            if (moveHeight == 0 && BulletData.FaceCamera == 2 && !isDirectHit)
                Direction = TargetPos - Position;

            if (BulletData.FaceCamera == 1)
                BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);

            // 设置缩放
            float scaleX = 1;
            if (BulletData.ScaleX == 1) scaleX = Target.ScaleX;
            if (BulletData.ScaleX == 2) scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);

            // 创建临时单位用于索敌
            CreateTempUnit();

            // 如果是直接命中，立即处理命中逻辑
            if (isDirectHit)
            {
                HandleDirectHit();
            }

            isInitialized = true;
        }

        private void CreateTempUnit()
        {
            tempUnit = Battle.CreateTempUnit(Position, (TargetPos - Position).ToV2());
            if (tempUnit == null) return;

            var skillData = Database.Instance.GetIndex<SkillData>(skillId);
            if (skillData != null)
            {
                findTargetSkill = tempUnit.LearnSkill(skillData);
                findTargetSkill.Init();
            }
        }

        public override void Update()
        {
            if (!isInitialized) return;

            // 如果是直接命中，已经处理过了，不需要更新
            if (isDirectHit) return;

            tickTime += SystemConfig.DeltaTime;

            // 更新目标位置或检查目标有效性
            if (Target != null && Target.Alive())
            {
                TargetPos = GetTargetPos(Target);
            }
            else if (Target != null) // 目标存在但已死亡
            {
                // 继续飞向目标最后的位置，但不更新目标位置
                // 这样弹道会继续飞行到目标最后的位置，然后尝试链式跳转
            }

            // 更新临时单位位置
            if (tempUnit != null)
                tempUnit.Position = Position;

            // 计算新位置
            if (moveHeight == 0)
            {
                Position = CalculatePositionAtTime(tickTime);
            }
            else
            {
                Position = CalculatePositionAtTime(tickTime);
                if (BulletData.FaceCamera == 2)
                    Direction = CalculatePositionAtTime(tickTime + SystemConfig.DeltaTime) - Position;
            }
        }

        private Vector3 CalculatePositionAtTime(float time)
        {
            // 计算起点到终点的距离
            float distance = Vector3.Distance(startPositionCache, TargetPos);

            // 如果起点终点相同，直接返回目标位置
            if (distance < Mathf.Epsilon)
            {
                return TargetPos;
            }

            float totalTime = distance / BulletData.Speed;
            Vector3 position = startPositionCache + (TargetPos - startPositionCache) * (time / totalTime);

            // 添加抛物线高度
            if (moveHeight > 0)
            {
                float t = time / totalTime;
                position.y += (-5 * t * t + 5 * t) * moveHeight;
            }

            // 检查是否到达目标
            if (time > totalTime)
            {
                HandleTargetReached(position);
            }

            return position;
        }

        private void HandleDirectHit()
        {
            // 直接处理命中逻辑
            if (Target == null)
            {
                Skill.Hit(TargetPos.ToV2(), this);
            }
            else if (Target.Alive())
            {
                Skill.Hit(Target, this);
            }

            // 更新链式计数
            if (maxLinkNum > 0)
            {
                maxLinkNum--;
                linkNum++;
            }
            reductionRate = 1 - reductionBase * linkNum;

            // 记录已命中目标
            if (Target != null)
            {
                usedTargets.Add(Target);
                lastTarget = Target;
            }

            // 寻找下一个目标
            FindNextTarget(TargetPos);
        }

        private void HandleTargetReached(Vector3 position)
        {
            Position = TargetPos;
            if (tempUnit != null)
                tempUnit.Position = Position;

            // 处理命中逻辑
            if (Target == null)
            {
                Skill.Hit(TargetPos.ToV2(), this);
            }
            else if (Target.Alive())
            {
                Skill.Hit(Target, this);
            }

            // 更新链式计数
            if (maxLinkNum > 0)
            {
                maxLinkNum--;
                linkNum++;
            }
            reductionRate = 1 - reductionBase * linkNum;

            // 记录已命中目标
            if (Target != null)
            {
                usedTargets.Add(Target);
                lastTarget = Target;
            }

            // 寻找下一个目标
            FindNextTarget(position);
        }

        private void FindNextTarget(Vector3 currentPosition)
        {
            if (findTargetSkill == null)
            {
                Finish();
                return;
            }

            findTargetSkill.UpdateAttackPoints();
            findTargetSkill.FindTarget();

            if (maxLinkNum > 0 && findTargetSkill.Targets.Count > 0)
            {
                Unit nextTarget = null;

                if (!canBack)
                {
                    nextTarget = findTargetSkill.Targets.Find(x => x.Alive() && !usedTargets.Contains(x));
                }
                else
                {
                    linkedTargets = findTargetSkill.Targets;
                    nextTarget = linkedTargets.Find(x => x.Alive() &&
                        (linkedTargets.Count <= 1 || x != lastTarget));
                }

                if (nextTarget != null)
                {
                    // 设置新目标并重置参数
                    Target = nextTarget;
                    TargetPos = GetTargetPos(Target);
                    startPositionCache = currentPosition;

                    // 检查新目标是否与当前位置相同
                    if (Vector3.Distance(currentPosition, TargetPos) < Mathf.Epsilon)
                    {
                        // 直接处理命中，避免除零错误
                        HandleDirectHit();
                    }
                    else
                    {
                        tickTime = 0;
                    }
                    return;
                }
            }

            // 没有找到有效目标，结束弹道
            CleanUp();
            Finish();
        }

        private void CleanUp()
        {
            if (findTargetSkill != null)
            {
                findTargetSkill.HideUnitAttackArea();
                findTargetSkill.Finish();
            }

            if (tempUnit != null)
            {
                Battle.AllUnits.Remove(tempUnit);
                tempUnit = null;
            }
        }

        public override void Finish()
        {
            CleanUp();
            base.Finish();
        }
    }
}
