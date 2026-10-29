using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;

namespace Bullets
{
    /// <summary>
    /// 火球弹道：
    /// 1. 拥有类似棘刺2的可扩展 AOE 范围；
    /// 2. AOE 伤害来源由 Data.DamageSkillId / Data.AoeSkillId 指定的施法者技能；
    /// 3. 通过 Data.SkillId 指定的独立索敌技能寻找并追踪目标；
    /// 4. 拥有独立碰撞半径，碰撞伤害使用发射技能 Skill.Hit；
    /// 5. 无初始目标时沿发射方向直线飞行，并周期性使用索敌技能寻找目标。
    ///
    /// 常用 Data 配置：
    /// InitRadius / MaxRadius / RadiusExponentRate：AOE 初始半径、最终半径、扩展速度
    /// DamageSkillId / AoeSkillId / AoeDamageSkillId：AOE 伤害来源技能 id
    /// SkillId：独立索敌技能 id
    /// CollisionRadius：碰撞判定半径
    /// Homing / NavigationConstant / MaxTurnRate / SearchTurnRate / HomingDelay / SearchInterval：追踪配置
    /// Trigger / TriggerTimes / MaxTargetCount：AOE 伤害触发间隔、触发次数、每轮最多命中目标数
    /// TargetTeam：AOE 目标阵营，不配置时使用伤害技能或发射技能的 TargetTeam
    /// LifeTime：存活时间
    /// </summary>
    public class 火球 : Bullet
    {
        // ---- AOE 配置 ----
        private float initRadius;
        private float maxRadius;
        private float radiusExponentRate;
        private float radius;
        private int aoeTargetTeam;

        private float triggerInterval;
        private CountDown triggerTimer = new CountDown();
        private int triggerTimes;
        private int maxTargetCount;
        private int currentTargetCount;
        private bool countLimit;
        private bool startAttack;

        private readonly HashSet<Unit> damagedUnits = new HashSet<Unit>();
        private Skill aoeDamageSkill;

        // ---- 独立索敌/追踪配置 ----
        private string seekSkillId;
        private Skill findTargetSkill;
        private Unit tempUnit;

        private bool homing;
        private float navigationConstant = HomingNavigation.DefaultNavigationConstant;
        private float maxTurnRate;
        private float searchTurnRate;
        private float homingDelay;
        private float homingDelayTimer;
        private float minTurnRadius;
        private float searchMinTurnRadius;
        private float searchInterval;
        private float searchTimer;
        private bool isSeekingTarget;
        private bool acquiredFromSearch;

        // ---- 碰撞配置 ----
        private float collisionRadius;

        // ---- 生命周期 ----
        private float lifeTime;
        private CountDown lifeTimer = new CountDown();
        private bool finished;

        // ---- 显示范围 ----
        private string color;
        private float alpha;
        private bool doNotShowRange = false;
        List<GameObject> tiles = new List<GameObject>();

        public override void Init()
        {
            base.Init();

            var data = BulletData.Data;

            // ===== AOE =====
            initRadius = data.GetFloat("InitRadius", 0.5f);
            maxRadius = data.GetFloat("MaxRadius", initRadius);
            radiusExponentRate = Mathf.Max(0f, data.GetFloat("RadiusExponentRate", 0f));
            radius = initRadius;

            triggerInterval = Mathf.Max(0f, data.GetFloat("Trigger", 0f));
            triggerTimes = data.GetInt("TriggerTimes", -1);
            maxTargetCount = data.GetInt("MaxTargetCount", -1);
            countLimit = maxTargetCount != -1;
            currentTargetCount = 0;

            // ===== 伤害来源技能 =====
            string aoeSkillId = data.GetStr("DamageSkillId");
            if (string.IsNullOrEmpty(aoeSkillId))
                aoeSkillId = data.GetStr("AoeSkillId");
            if (string.IsNullOrEmpty(aoeSkillId))
                aoeSkillId = data.GetStr("AoeDamageSkillId");
            aoeDamageSkill = FindCasterSkill(aoeSkillId);
            if (aoeDamageSkill == null && !string.IsNullOrEmpty(aoeSkillId))
                Debug.LogError($"火球 {BulletData.Id} 找不到施法者技能: {aoeSkillId}");

            aoeTargetTeam = data.GetInt("TargetTeam", -1);
            if (aoeTargetTeam == -1)
                aoeTargetTeam = aoeDamageSkill?.SkillData?.TargetTeam ?? Skill.SkillData.TargetTeam;

            // ===== 追踪 =====
            homing = data == null || !data.ContainsKey("Homing") || data.GetBool("Homing");
            navigationConstant = data.GetFloat("NavigationConstant", HomingNavigation.DefaultNavigationConstant);
            maxTurnRate = data.GetFloat("MaxTurnRate", 1440f);
            searchTurnRate = data.GetFloat("SearchTurnRate", -1f);
            if (searchTurnRate < 0f)
                searchTurnRate = maxTurnRate;
            homingDelay = Mathf.Max(0f, data.GetFloat("HomingDelay", 0f));
            homingDelayTimer = homingDelay;
            searchInterval = Mathf.Max(0f, data.GetFloat("SearchInterval", 0.1f));
            searchTimer = 0f;

            minTurnRadius = HomingNavigation.CalculateMinTurnRadius(BulletData.Speed, maxTurnRate);
            searchMinTurnRadius = HomingNavigation.CalculateMinTurnRadius(BulletData.Speed, searchTurnRate);

            seekSkillId = data.GetStr("SkillId");
            CreateTempUnit();

            // ===== 碰撞 =====
            collisionRadius = Mathf.Max(0f, data.GetFloat("CollisionRadius", 0.25f));

            // ===== 生命周期 =====
            lifeTime = data.GetFloat("LifeTime", 0f);
            if (lifeTime > 0f)
                lifeTimer.Set(lifeTime);

            // ===== 初始方向 / 初始目标 =====
            bool hasTarget = Target != null && Target.Alive();
            if (hasTarget)
            {
                TargetPos = HomingNavigation.GetTargetPosition(this, Target);
            }
            else
            {
                Target = null;
                Vector3 aim = TargetPos - Position;
                if (aim.sqrMagnitude <= 0.0001f)
                {
                    Vector3 casterDir = new Vector3(Skill.Unit.Direction.x, 0f, Skill.Unit.Direction.y);
                    if (casterDir.sqrMagnitude > 0.0001f)
                        aim = casterDir;
                }

                Direction = aim;
                if (homing)
                {
                    isSeekingTarget = true;
                    searchTimer = 0f;
                }
            }

            if (hasTarget)
                Direction = TargetPos - Position;

            if (BulletData.FaceCamera == 1)
                BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);

            float scaleX = 1f;
            if (BulletData.ScaleX == 1)
                scaleX = Target != null ? Target.ScaleX : Skill.Unit.ScaleX;
            else if (BulletData.ScaleX == 2)
                scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);
            // ===== 显示范围 =====
            color = BulletData.Data.GetStr("Color");
            alpha = BulletData.Data.GetFloat("Alpha", 1f);
            doNotShowRange = BulletData.Data.GetBool("DoNotShowRange");

            if (!doNotShowRange) ShowRangeInit(color, alpha);
        }

        private Skill FindCasterSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || Skill?.Unit == null)
                return null;

            var result = Skill.Unit.Skills.Find(s =>
                s != null &&
                s.SkillData != null &&
                s.SkillData.Id == skillId);
            if (result != null)
                return result;

            int index = Database.Instance.GetIndex<SkillData>(skillId);
            if (index != -1)
                return Skill.Unit.Skills.Find(s => s != null && s.Id == index);

            return null;
        }

        private void CreateTempUnit()
        {
            tempUnit = new Unit();
            tempUnit.Id = Skill.Unit.Id;
            tempUnit.Battle = Battle;
            tempUnit.Init(true);
            tempUnit.AttackRange = 1;
            tempUnit.Position = Position;
            tempUnit.Direction = Skill.Unit.Direction;

            if (string.IsNullOrEmpty(seekSkillId))
                return;

            int skillDataId = Database.Instance.GetIndex<SkillData>(seekSkillId);
            if (skillDataId == -1)
                return;

            findTargetSkill = tempUnit.LearnSkill(skillDataId);
            if (findTargetSkill != null)
                findTargetSkill.Init();
        }

        public override void Update()
        {
            if (finished)
                return;

            base.Update();

            // 生命周期优先处理。
            if (lifeTime > 0f && lifeTimer.Update(SystemConfig.DeltaTime))
            {
                Finish();
                return;
            }

            // AOE 半径扩张。
            if (radius < maxRadius)
                radius = Mathf.Min(maxRadius, radius + radiusExponentRate * SystemConfig.DeltaTime);

            // 同步临时索敌单位位置。
            if (tempUnit != null)
                tempUnit.Position = Position;

            UpdateTargetAndSearch();
            UpdateMovement();

            if (!doNotShowRange)
            {
                ShowRange range = tiles[0].GetComponent<ShowRange>();
                range.UpdateRange(this.Position.ToV2(), radius);
            }

            // 碰撞优先级最高：命中后立即结束。
            if (CheckCollision())
                return;

            UpdateAoeDamage();

            if (triggerTimes == 0)
            {
                Finish();
            }
        }

        private void UpdateTargetAndSearch()
        {
            if (Target != null && Target.Alive())
            {
                TargetPos = HomingNavigation.GetTargetPosition(this, Target);
                isSeekingTarget = false;
                return;
            }

            if (Target != null)
                Target = null;

            if (!homing)
                return;

            if (!isSeekingTarget)
            {
                isSeekingTarget = true;
                searchTimer = 0f;
                acquiredFromSearch = false;
            }

            searchTimer -= SystemConfig.DeltaTime;
            if (searchTimer <= 0f)
            {
                searchTimer = searchInterval;
                TryFindTarget();
            }
        }

        private bool TryFindTarget()
        {
            if (findTargetSkill == null || tempUnit == null)
                return false;

            tempUnit.Position = Position;
            findTargetSkill.UpdateAttackPoints();
            findTargetSkill.FindTarget();

            Unit nextTarget = null;
            foreach (var target in findTargetSkill.Targets)
            {
                if (target != null && target.Alive())
                {
                    nextTarget = target;
                    break;
                }
            }

            if (nextTarget == null)
                return false;

            Target = nextTarget;
            TargetPos = HomingNavigation.GetTargetPosition(this, Target);
            isSeekingTarget = false;
            acquiredFromSearch = true;
            return true;
        }

        private void UpdateMovement()
        {
            if (homing && homingDelayTimer > 0f)
            {
                homingDelayTimer -= SystemConfig.DeltaTime;
                if (homingDelayTimer < 0f)
                    homingDelayTimer = 0f;

                Position = MoveStraight();
                return;
            }

            if (homing && Target != null && Target.Alive())
            {
                Position = MoveHoming();
                return;
            }

            // 非追踪模式或无目标：沿当前方向直线飞行。
            Position = MoveStraight();
        }

        private Vector3 MoveHoming()
        {
            float turnRate = acquiredFromSearch ? searchTurnRate : maxTurnRate;
            float radiusLimit = acquiredFromSearch ? searchMinTurnRadius : minTurnRadius;

            return HomingNavigation.GetNextPosition(
                this,
                SystemConfig.DeltaTime,
                navigationConstant,
                turnRate,
                radiusLimit);
        }

        private Vector3 MoveStraight()
        {
            Vector3 direction = Direction;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Vector3 fallback = TargetPos - Position;
                if (fallback.sqrMagnitude > 0.0001f)
                    direction = fallback;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                return Position;

            direction.Normalize();
            Direction = direction;
            return Position + direction * BulletData.Speed * Speed * SystemConfig.DeltaTime;
        }

        private bool CheckCollision()
        {
            if (collisionRadius <= 0f || findTargetSkill == null || tempUnit == null)
                return false;

            int seekTeam = findTargetSkill.SkillData.TargetTeam;
            var nearby = Battle.FindAll(Position.ToV2(), collisionRadius, seekTeam);
            if (nearby.Count == 0)
                return false;

            tempUnit.Position = Position;
            findTargetSkill.UpdateAttackPoints();
            findTargetSkill.FindTarget();

            Unit hitTarget = null;
            foreach (var target in findTargetSkill.Targets)
            {
                if (target != null && target.Alive() && nearby.Contains(target))
                {
                    hitTarget = target;
                    break;
                }
            }

            if (hitTarget == null)
                return false;

            // 碰撞伤害来源为发射技能。
            Skill.Hit(hitTarget, this);
            Finish();
            return true;
        }

        private void UpdateAoeDamage()
        {
            if (aoeDamageSkill == null)
                return;

            var targets = Battle.FindAll(Position.ToV2(), radius, aoeTargetTeam);

            if (targets.Count > 0 && !startAttack)
            {
                startAttack = true;
                triggerTimer.Set(triggerInterval);
                currentTargetCount = 0;
            }

            if (triggerTimer.Update(SystemConfig.DeltaTime))
            {
                if (triggerTimes != -1)
                {
                    triggerTimes--;
                }

                damagedUnits.Clear();
                currentTargetCount = 0;
                triggerTimer.Set(triggerInterval);
            }

            foreach (var target in targets)
            {
                if (target == null || !target.Alive() || damagedUnits.Contains(target))
                    continue;

                if (countLimit && currentTargetCount >= maxTargetCount)
                    break;

                damagedUnits.Add(target);
                currentTargetCount++;

                // AOE 伤害来源为配置的施法者技能。
                aoeDamageSkill.Hit(target, this);
            }
        }

        public override void Finish()
        {
            if (finished)
                return;

            finished = true;

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

            base.Finish();
        }

        public void ShowRangeInit(string color, float alpha)
        {
            var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowRange");
            GameObject go = UnityEngine.Object.Instantiate(tileAsset);

            var grid = Skill.Unit.NowGrid;
            Unit rangeUnit = Target != null ? Target : Skill.Unit;

            go.transform.SetParent(grid.MapGrid.transform);
            go.transform.localPosition = new Vector3(0, grid.FarAttackGrid ? -0.25f : 0.15f, 0);

            ShowRange showRange = go.GetComponent<ShowRange>();
            showRange.targetTile = grid.MapGrid.gameObject;
            showRange.unitUniqueIndex = Battle.AllUnits.IndexOf(rangeUnit);
            showRange.useGridPos = false;
            showRange.unitGridPos = Position.ToV2Int();
            //doNotShowRange.unitGridPos = Skill.Unit.GridPos;
            showRange.unitWorldPos = Position.ToV2();
            showRange.colorHex = String.IsNullOrEmpty(color) ? "#6385FF" : color;
            showRange.alpha = alpha;
            showRange.rangeRadius = radius;
            //doNotShowRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();    
            showRange.Init();
            tiles.Add(go);
        }
    }
}
