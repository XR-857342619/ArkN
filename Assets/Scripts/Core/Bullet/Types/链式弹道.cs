using System;
using System.Collections.Generic;
using System.Linq;
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
        private bool isFinished;
        private Skill skill;

        private bool showRange;
        private string color;
        private float alpha;

        // 属性
        public int LinkNum => linkNum;
        public float reductionRate;
        //public float ReductionRate => reductionRate;
        List<GameObject> tiles = new List<GameObject>();

        public override void Init()
        {
            base.Init();

            isFinished = false;

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
            //reductionBase = BulletData.Data.GetFloat("ReductionRate", 0);
            //reductionRate = 1 - reductionBase * linkNum;
            canBack = BulletData.Data.GetBool("CanBack");
            skillId = BulletData.Data.GetStr("SkillId");

            showRange = BulletData.Data.GetBool("DoNotShowRange");
            color = BulletData.Data.GetStr("Color");
            alpha = BulletData.Data.GetFloat("Alpha", 1f);

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
            skill = CreateTempUnit();

            // 如果是直接命中，立即处理命中逻辑

            isInitialized = true;

            ShowRangeInit(color, alpha, skill);
        }

        private Skill CreateTempUnit()
        {
            //tempUnit = Battle.CreateTempUnit(Position, new Vector2(0,1));
            tempUnit = new Unit();
            tempUnit.Id = Skill.Unit.Id;
            tempUnit.Battle = Battle;
            tempUnit.Init(true);
            tempUnit.AttackRange = 1;
            tempUnit.Position = Skill.Unit.Position;
            if (tempUnit == null) return null;

            var skillData = Database.Instance.GetIndex<SkillData>(skillId);
            if (skillData != -1)
            {
                findTargetSkill = tempUnit.LearnSkill(skillData);
                findTargetSkill.Init();
            }
            Debug.Log("临时单位索敌半径"+tempUnit.AttackRange);
            return findTargetSkill;
        }

        public override void Update()
        {
            if (isFinished) return;
            base.Update();
            if (isDirectHit)
            {
                HandleDirectHit();
                if (isFinished) return;
            }
            if (!isInitialized) return;

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
                if (isFinished) return;
                if (BulletData.FaceCamera == 2)
                    Direction = CalculatePositionAtTime(tickTime + SystemConfig.DeltaTime) - Position;
            }

            if (tiles.Count == 0)
                return;
            if (showRange)
            {
                ShowRange range = tiles[0].GetComponent<ShowRange>();
                range.UpdateRange(this.Position.ToV2(), 2.5f);
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

            float totalTime = distance / BulletData.Speed * Speed;
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
                Debug.Log($"弹道到达目标位置: {TargetPos}");
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
            //reductionRate = 1 - reductionBase * linkNum;

            // 记录已命中目标
            if (Target != null)
            {
                usedTargets.Add(Target);
                lastTarget = Target;
            }
            isDirectHit = false;
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
            //reductionRate = 1 - reductionBase * linkNum;

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

            Debug.Log($"下一目标: {(findTargetSkill.Targets.Count > 0 ? findTargetSkill.Targets[0].UnitData.Id : "无")}");

            if (maxLinkNum > 0 && findTargetSkill.Targets.Count > 0)
            {
                Unit nextTarget = null;

                if (!canBack)
                {
                    nextTarget = findTargetSkill.Targets.Find(x => x.Alive() && !usedTargets.Contains(x));
                    Debug.Log($"寻找下一个目标 (不允许回跳): {(nextTarget != null ? nextTarget.UnitData.Id : "无")} 位置: {nextTarget?.Position}");
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

            foreach (var tile in tiles)
            {
                UnityEngine.Object.Destroy(tile);
            }
            tiles.Clear();
        }

        public override void Finish()
        {
            if (isFinished) return;
            isFinished = true;
            CleanUp();
            base.Finish();
        }

        public void ShowRangeInit(string color, float alpha, Skill skill)
        {
            var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowRange");
            GameObject go = UnityEngine.Object.Instantiate(tileAsset);
            go.transform.SetParent(Skill.Unit.NowGrid.MapGrid.transform);
            go.transform.localPosition = new Vector3(0, Battle.Map.Tiles[Target.NowGrid.X, Target.NowGrid.Y].FarAttackGrid ? -0.25f : 0.15f, 0);
            ShowRange showRange = go.GetComponent<ShowRange>();
            showRange.targetTile = Battle.Map.Tiles[Skill.Unit.NowGrid.X, Skill.Unit.NowGrid.Y].MapGrid.gameObject;
            showRange.unitUniqueIndex = Battle.AllUnits.IndexOf(Target);
            showRange.useGridPos = false;
            showRange.unitGridPos = Position.ToV2Int();
            //doNotShowRange.unitGridPos = Skill.Unit.GridPos;
            showRange.unitWorldPos = Position.ToV2();
            showRange.colorHex = String.IsNullOrEmpty(color) ? "#6385FF" : color;
            showRange.alpha = alpha;
            showRange.rangeRadius = skill.SkillData.AreaRange;
            showRange.polygonRange = skill.AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();
            //doNotShowRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();    
            showRange.Init();
            tiles.Add(go);
        }
    }
}
