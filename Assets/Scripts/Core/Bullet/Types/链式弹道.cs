using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bullets
{
    public class 链式弹道 : Bullet
    {
        private float moveHeight;
        private float tickTime;
        private string skillId;
        private int maxLinkNum;
        private int linkNum;
        //private float reductionBase;
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
        private float _lifeTime;
        private bool homing;
        private float homingNavigationConstant = HomingNavigation.DefaultNavigationConstant;
        private float homingMaxTurnRate;
        private float homingHitRange = 0.1f;
        private bool isSeekingTarget;
        private float searchInterval = 0.1f;
        private float searchTimer;

        private bool showRange;
        private string color;
        private float alpha;

        public int LinkNum => linkNum;
        //public float reductionRate;
        //public float ReductionRate => reductionRate;
        private List<GameObject> tiles = new List<GameObject>();
        private CountDown lifeTime;

        public override void Init()
        {
            base.Init();

            isFinished = false;
            isSeekingTarget = false;

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

            showRange = BulletData.Data.GetBool("ShowRange");
            color = BulletData.Data.GetStr("Color");
            alpha = BulletData.Data.GetFloat("Alpha", 1f);

            _lifeTime = BulletData.Data.GetFloat("LifeTime", 0);
            if (_lifeTime > 0) lifeTime.Set(_lifeTime);
            else lifeTime = null;

            homing = BulletData.Data.GetBool("Homing");
            if (homing)
            {
                homingNavigationConstant = BulletData.Data.GetFloat("NavigationConstant", HomingNavigation.DefaultNavigationConstant);
                homingMaxTurnRate = BulletData.Data.GetFloat("MaxTurnRate", 0f);
                homingHitRange = BulletData.Data.GetFloat("HomingHitRange", 0.1f);
            }

            searchInterval = Mathf.Max(0f, BulletData.Data.GetFloat("SearchInterval", 0.1f));
            searchTimer = 0f;

            bool hasTarget = Target != null && Target.Alive();

            // 有初始指向目标时保持原逻辑。
            if (hasTarget)
            {
                isDirectHit = Vector3.Distance(StartPosition, HomingNavigation.GetTargetPosition(this, Target)) < Mathf.Epsilon;
                TargetPos = HomingNavigation.GetTargetPosition(this, Target);

                if (moveHeight == 0 && BulletData.FaceCamera == 2 && !isDirectHit)
                    Direction = TargetPos - Position;
            }
            else
            {
                // 非指向技能（例如弩箭）发射时没有初始 Target。
                // TargetPos 仍由 CreateBullet 传入，可作为初始直线搜索方向。
                isDirectHit = false;

                Vector3 aim = TargetPos - Position;
                if (aim.sqrMagnitude <= 0.0001f)
                {
                    Vector3 casterDir = new Vector3(Skill.Unit.Direction.x, 0f, Skill.Unit.Direction.y);
                    if (casterDir.sqrMagnitude > 0.0001f)
                    {
                        TargetPos = Position + casterDir.normalized;
                        aim = casterDir;
                    }
                }

                if (aim.sqrMagnitude > 0.0001f)
                    Direction = aim;

                // 追踪模式下允许无目标开局：先直线飞行并索敌。
                if (homing)
                    isSeekingTarget = true;
            }

            if (BulletData.FaceCamera == 1)
                BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);

            // 设置缩放
            float scaleX = 1f;
            if (BulletData.ScaleX == 1)
                scaleX = hasTarget ? Target.ScaleX : Skill.Unit.ScaleX;
            else if (BulletData.ScaleX == 2)
                scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);

            // 创建临时单位用于索敌
            skill = CreateTempUnit();

            // 如果是直接命中，立即处理命中逻辑

            isInitialized = true;

            // ShowRange 基于临时索敌单位 tempUnit 显示，因此无初始 Target 时也可显示。
            if (showRange)
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
            tempUnit.Position = Position;
            if (tempUnit == null) return null;

            var skillData = Database.Instance.GetIndex<SkillData>(skillId);
            if (skillData != -1)
            {
                findTargetSkill = tempUnit.LearnSkill(skillData);
                findTargetSkill.Init();
            }
            //Debug.Log("临时单位索敌半径"+tempUnit.AttackRange);
            return findTargetSkill;
        }

        public override void Update()
        {
            if (lifeTime is not null)
            {
                lifeTime.Update(SystemConfig.DeltaTime);
                if (lifeTime.Finished())
                {
                    Finish();
                    return;
                }
            }
            if (isFinished) return;
            base.Update();
            if (isDirectHit)
            {
                HandleDirectHit();
                if (isFinished) return;
                if (isDirectHit) return; // 同点回跳改为下一帧处理，避免同帧递归无限命中
            }
            if (!isInitialized) return;

            tickTime += SystemConfig.DeltaTime;

            // 临时索敌单位始终跟随子弹当前位置，确保直线飞行途中也能用当前位置索敌。
            if (tempUnit != null)
                tempUnit.Position = Position;

            // 更新目标位置或检查目标有效性
            if (Target != null && Target.Alive())
            {
                TargetPos = GetTargetPos(Target);
            }
            else if (Target != null && Target.IfHide)
            {
                FindNextTarget(Position);
                TargetPos = GetTargetPos(Target);
            }
            else if (isSeekingTarget)
            {
                // 搜索状态下按配置间隔索敌，避免每帧执行完整 FindTarget。
                searchTimer -= SystemConfig.DeltaTime;
                if (searchTimer <= 0f)
                {
                    searchTimer = searchInterval;
                    FindNextTarget(Position);
                }
            }
            else
            {
                FindNextTarget(Position);
            }

            if (Target is null && !isSeekingTarget)
            {
                Finish();
                return;
            }

            // 计算新位置
            if (homing)
            {
                // 同点命中放到下一帧处理，避免同帧递归无限命中。
                if (isDirectHit)
                {
                    // 不移动，下一帧走 HandleDirectHit。
                }
                else if (Target != null && Target.Alive())
                {
                    Position = CalculateHomingPosition();
                }
                else if (isSeekingTarget)
                {
                    Position = CalculateSeekingPosition();
                }
            }
            else if (moveHeight == 0)
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
                range.UpdateRange(this.Position.ToV2(), skill?.SkillData?.AttackRange ?? 0);
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
                HandleTargetReached();
                return Position; // 命中后以实际吸附位置作为当前帧位置，不再用“过冲点”覆盖
            }

            if (position.y < 0) position.y = moveHeight;

            return position;
        }

        private Vector3 CalculateHomingPosition()
        {
            // 比例导引每次根据当前子弹位置/当前目标位置计算下一步，
            // 不再使用 startPositionCache 的线性插值。
            Vector3 next = HomingNavigation.GetNextPosition(this, SystemConfig.DeltaTime, homingNavigationConstant, homingMaxTurnRate);

            // 到达判定：目标在当前帧步长可覆盖的范围内，或已经非常接近时视为命中。
            float step = Mathf.Max(homingHitRange, BulletData.Speed * Speed * SystemConfig.DeltaTime);
            if ((next - TargetPos).sqrMagnitude <= step * step ||
                (Position - TargetPos).sqrMagnitude <= step * step)
            {
                Position = TargetPos;
                HandleTargetReached();
                return Position;
            }

            return next;
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
            FindNextTarget(Position);
        }

        private void HandleTargetReached()
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
            FindNextTarget(Position);
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

            //Debug.Log($"下一目标: {(findTargetSkill.Targets.Count > 0 ? findTargetSkill.Targets[0].UnitData.Id : "无")}");

            if ((maxLinkNum > 0 && findTargetSkill.Targets.Count > 0) || maxLinkNum < 0)
            {
                Unit nextTarget = null;

                if (!canBack)
                {
                    nextTarget = findTargetSkill.Targets.Find(x => x.Alive() && !usedTargets.Contains(x));
                    //Debug.Log($"寻找下一个目标 (不允许回跳): {(nextTarget != null ? nextTarget.UnitData.Id : "无")} 位置: {nextTarget?.Position}");
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
                    isSeekingTarget = false;
                    Target = nextTarget;
                    TargetPos = GetTargetPos(Target);
                    startPositionCache = currentPosition;

                    // 检查新目标是否与当前位置相同
                    if (Vector3.Distance(currentPosition, TargetPos) < Mathf.Epsilon)
                    {
                        // 直接处理命中，避免除零错误
                        isDirectHit = true; // 同点回跳放到下一帧处理，保留无限回跳但不递归
                        tickTime = 0;
                    }
                    else
                    {
                        tickTime = 0;
                    }
                    return;
                }

                // 当前索敌范围内没有可用目标。
                // 追踪模式下不立即结束：保留 lastTarget 回跳能力，
                // 但如果连 lastTarget 都已不可用，则进入直线飞行搜索状态。
                if (lastTarget is null || !lastTarget.IfAlive)
                {
                    if (homing)
                    {
                        EnterSeekingState();
                        return;
                    }
                    Finish();
                    return;
                }

                isSeekingTarget = false;
                Target = lastTarget;
                TargetPos = GetTargetPos(Target);
                startPositionCache = currentPosition;

                if (Vector3.Distance(currentPosition, TargetPos) < Mathf.Epsilon)
                {
                    // 直接处理命中，避免除零错误
                    isDirectHit = true; // 同点回跳放到下一帧处理，保留无限回跳但不递归
                    tickTime = 0;
                }
                else
                {
                    tickTime = 0;
                }
                return;
            }

            // 链接次数用尽后仍维持原有结束逻辑。
            if (maxLinkNum == 0)
            {
                Finish();
                return;
            }

            // 还有剩余链接次数，但当前位置暂时索不到目标。
            // 追踪模式下进入直线飞行搜索；否则维持原有结束逻辑。
            if (homing)
            {
                EnterSeekingState();
                return;
            }

            Finish();
        }

        private void EnterSeekingState()
        {
            isSeekingTarget = true;
            Target = null;
            TargetPos = Position;
            isDirectHit = false;
            tickTime = 0f;
            startPositionCache = Position;
            searchTimer = searchInterval;
        }

        private Vector3 CalculateSeekingPosition()
        {
            Vector3 direction = Direction;
            if (direction.sqrMagnitude <= 0.0001f &&
                (TargetPos - Position).sqrMagnitude > 0.0001f)
            {
                direction = (TargetPos - Position).normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                return Position;

            direction.Normalize();
            Direction = direction;
            return Position + direction * BulletData.Speed * Speed * SystemConfig.DeltaTime;
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
            if (tempUnit == null)
                return;

            var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowRange");
            GameObject go = UnityEngine.Object.Instantiate(tileAsset);

            var tile = Battle.Map.Tiles[tempUnit.GridPos.x, tempUnit.GridPos.y];
            if (tile == null || tile.MapGrid == null)
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            go.transform.SetParent(tile.MapGrid.transform);
            go.transform.localPosition = new Vector3(0, tile.FarAttackGrid ? -0.25f : 0.15f, 0);

            ShowRange showRange = go.GetComponent<ShowRange>();
            showRange.targetTile = tile.MapGrid.gameObject;
            showRange.unitUniqueIndex = Battle.AllUnits.IndexOf(tempUnit);
            showRange.useGridPos = false;
            showRange.unitGridPos = tempUnit.GridPos;
            //doNotShowRange.unitGridPos = Skill.Unit.GridPos;
            showRange.unitWorldPos = tempUnit.Position.ToV2();
            showRange.colorHex = String.IsNullOrEmpty(color) ? "#6385FF" : color;
            showRange.alpha = alpha;
            showRange.rangeRadius = skill?.SkillData?.AttackRange ?? 0;
            if (skill?.SkillData?.AttackPoints?.Length > 0)
                showRange.polygonRange = skill.AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();
            else showRange.polygonRange = null;
            //doNotShowRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();    
            showRange.Init();
            tiles.Add(go);
            //Debug.Log(showRange.rangeRadius);
        }
    }
}
