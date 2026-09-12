using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Bullets
{
    public class 棘刺2 : Bullet
    {
        HashSet<Unit> DamagedUnits = new HashSet<Unit>();
        //HashSet<Unit> damageUnits = new HashSet<Unit>();

        private bool arrive;
        private CountDown LifeTime = new CountDown();
        private CountDown TriggerTime = new CountDown();
        public float radius;
        private float _lifeTime;
        private float _triggerTime;

        private float InitRadius;
        private float MaxRadius;
        private float RadiusExponentRate;

        private float moveHeight;//0:直线 1:抛物线 2.瞬移 3.静止
        private float tickTime;

        private float alpha;

        private int targetTeam;
        private int maxTargetCount;
        private int currentTargetCount;
        private int triggerTimes;
        private bool hasInitialTarget;
        private bool hasHitTarget;
        //float attackGap = -1;

        private bool countLimit = false;
        private bool startAttack = false;
        private bool doNotShowRange = false;

        private string color;

        List<GameObject> tiles = new List<GameObject>();
        public override void Init()
        {
            base.Init();

            // 是否为指向技能发射：非指向技能没有初始 Target，视为一直没有命中目标。
            hasInitialTarget = Target != null;

            // 有初始目标时使用命中点；非指向技能没有 Target，保留 CreateBullet 传入的 TargetPos。
            if (Target != null && Target.Alive())
                TargetPos = GetTargetPos(Target);
            else if (Target != null)
                arrive = true;

            moveHeight = BulletData.Data.GetFloat("MoveHeight");
            _lifeTime = BulletData.Data.GetFloat("LifeTime",0);
            LifeTime.Set(_lifeTime);
            _triggerTime = BulletData.Data.GetFloat("Trigger",0);
            MaxRadius = BulletData.Data.GetFloat("MaxRadius");
            InitRadius = BulletData.Data.GetFloat("InitRadius");
            RadiusExponentRate = BulletData.Data.GetFloat("RadiusExponentRate",0);
            radius = InitRadius;

            targetTeam = BulletData.Data.GetInt("TargetTeam", -1);
            if (targetTeam == -1) targetTeam = Skill.SkillData.TargetTeam;
            maxTargetCount = BulletData.Data.GetInt("MaxTargetCount", -1);
            countLimit = maxTargetCount != -1;
            currentTargetCount = 0;
            triggerTimes = BulletData.Data.GetInt("TriggerTimes", -1);

            doNotShowRange = BulletData.Data.GetBool("DoNotShowRange");
            color = BulletData.Data.GetStr("Color");
            alpha = BulletData.Data.GetFloat("Alpha", 1f);

            if (moveHeight == 0 && BulletData.FaceCamera == 2) Direction = TargetPos - this.Position;
            if (BulletData.FaceCamera == 1) BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);

            float scaleX = 1;
            if (BulletData.ScaleX == 1)
                scaleX = Target != null ? Target.ScaleX : Skill.Unit.ScaleX;
            else if (BulletData.ScaleX == 2)
                scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);

            if (!doNotShowRange) ShowRangeInit(color, alpha);
        }
        public override void Update()
        {
            base.Update();

            if (radius < MaxRadius)
                radius += RadiusExponentRate * SystemConfig.DeltaTime;

            // 仅未到达时更新移动相关逻辑；到达后仍继续执行伤害、持续时间和触发逻辑。
            if (!arrive)
            {
                tickTime += SystemConfig.DeltaTime;

                // 有目标时持续更新目标位置；无目标时保持 CreateBullet 传入的终点，直线飞过去。
                if (Target != null && Target.Alive())
                    TargetPos = GetTargetPos(Target);
                else if (Target != null)
                    arrive = true;

                if (!arrive)
                {
                    if (moveHeight == 0)
                    {
                        Position = getPosOfTime(tickTime);
                    }
                    else if (moveHeight == 2)
                    {
                        Position = TargetPos;
                        arrive = true;
                    }
                    else
                    {
                        Position = getPosOfTime(tickTime);
                        if (!arrive && BulletData.FaceCamera == 2)
                            Direction = getPosOfTime(tickTime + SystemConfig.DeltaTime) - Position;
                    }
                }
            }
            //Debug.Log($"弹道 {BulletData.Id} 更新范围: 位置={this.Position}, 半径={radius}");
            if (!doNotShowRange)
            {
                ShowRange range = tiles[0].GetComponent<ShowRange>();
                range.UpdateRange(this.Position.ToV2(), radius);
            }

            if ((Position-TargetPos).sqrMagnitude < 0.001f) arrive = true;
            ////if (DamagedUnits.Count > 0 && TriggerTime.Finished())
            //if (TriggerTime.Finished())
            //{
            //    TriggerTime.Set(BulletData.Data.GetFloat("Trigger"));
            //}
            
            var targets = Battle.FindAll(Position.ToV2(), radius, targetTeam);
            targets.UnionWith(Battle.FindAll(Position, Skill.SkillData.AreaRange, 7).Where(x => x.UnitData.Name == Skill.SkillData.Data.GetStr("ExTarget")));
            if (targets.Count > 0 && !startAttack)
            {
                TriggerTime.Set(_triggerTime);
                startAttack = true;
                currentTargetCount = 0;
            }
            if (TriggerTime.Update(SystemConfig.DeltaTime))
            {
                if (triggerTimes != -1)
                {
                    triggerTimes--;
                }
                DamagedUnits.Clear();
                currentTargetCount = 0;
                TriggerTime.Set(_triggerTime);
            }
            //Debug.Log("target team:" + team);
            foreach (var t in targets)
            {
                if (!DamagedUnits.Contains(t))
                {
                    //Debug.Log("击中:" + t.UnitData.Id);
                    if (countLimit && currentTargetCount >= maxTargetCount)
                        break;

                    DamagedUnits.Add(t);
                    currentTargetCount++;

                    if (hasInitialTarget)
                        hasHitTarget = true;

                    Skill.Hit(t, this);
                }
            }

            // 命中目标后：
            // - _lifeTime == 0：立即结束；
            // - _lifeTime > 0：不结束，继续移动；arrive 仍只由子弹中心到达 TargetPos 决定。
            if (hasHitTarget && _lifeTime == 0)
            {
                Finish();
                return;
            }

            if (LifeTime.Update(SystemConfig.DeltaTime) && _lifeTime != 0)
            {
                Finish();
            }

            // 没有配置 LifeTime 时：
            // - 命中过目标：在命中当帧已经 Finish；
            // - 从未命中：到达终点后结束，避免无目标子弹永久存在。
            if (!hasHitTarget && _lifeTime == 0 && arrive)
            {
                Finish();
            }

            if (triggerTimes == 0)
            {
                Finish();
            }
        }

        Vector3 getPosOfTime(float time)
        {
            Vector3 position = Vector3.zero;
            float distance = (TargetPos - StartPosition).magnitude;

            if (distance < 0.0001f)
            {
                arrive = true;
                return TargetPos;
            }

            float totalTime = distance / BulletData.Speed * Speed;
            if (time > totalTime)
            {
                position = TargetPos;
                arrive = true;
            }
            else
                position = StartPosition + (TargetPos - StartPosition) * (time / totalTime);
            if (moveHeight == 1 || moveHeight == 2)
            {
                float t = time / totalTime;
                position.y += (-5 * t * t + 5 * t) * moveHeight;
            }
            if (moveHeight == 3)
            {
                position = StartPosition;
            }
            return position;
        }
        public override void Finish()
        {
            //Debug.Log($"弹道 {BulletData.Id} 结束: 位置={this.Position}, 半径={radius}");
            base.Finish();
            foreach (var tile in tiles)
            {
                UnityEngine.Object.Destroy(tile);
            }
            tiles.Clear();
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
