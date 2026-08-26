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
        private int triggerTimes;
        //float attackGap = -1;

        private bool countLimit = false;
        private bool startAttack = false;
        private bool doNotShowRange = false;

        private string color;

        List<GameObject> tiles = new List<GameObject>();
        public override void Init()
        {
            base.Init();
            if (Target.Alive())
                TargetPos = GetTargetPos(Target);
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
            triggerTimes = BulletData.Data.GetInt("TriggerTimes", -1);

            doNotShowRange = BulletData.Data.GetBool("DoNotShowRange");
            color = BulletData.Data.GetStr("Color");
            alpha = BulletData.Data.GetFloat("Alpha", 1f);

            if (moveHeight == 0 && BulletData.FaceCamera == 2) Direction = TargetPos - this.Position;
            if (BulletData.FaceCamera == 1) BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);
            float scaleX = 1;
            if (BulletData.ScaleX == 1) scaleX = Target.ScaleX;
            if (BulletData.ScaleX == 2) scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);

            if (!doNotShowRange) ShowRangeInit(color, alpha);
        }
        public override void Update()
        {
            if (arrive) return;
            base.Update();
            if (radius < MaxRadius)
                radius += RadiusExponentRate * SystemConfig.DeltaTime;
            tickTime += SystemConfig.DeltaTime;

            //Log.Debug(radius);
            

            if (Target.Alive())
                TargetPos = GetTargetPos(Target);
            else arrive = true;

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
                    if (BulletData.FaceCamera == 2)
                        Direction = getPosOfTime(tickTime + SystemConfig.DeltaTime) - Position;
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
            }
            if (TriggerTime.Update(SystemConfig.DeltaTime))
            {
                if (triggerTimes != -1)
                {
                    triggerTimes--;
                }
                DamagedUnits.Clear();
                TriggerTime.Set(_triggerTime);
            }
            //Debug.Log("target team:" + team);
            foreach (var t in targets)
            {
                if (!DamagedUnits.Contains(t))
                {
                    //Debug.Log("击中:" + t.UnitData.Id);
                    if (countLimit && maxTargetCount == 0)
                        break;
                    DamagedUnits.Add(t);
                    if (countLimit)
                    {
                        if (maxTargetCount > 0)
                        {
                            //Log.Debug("maxTargetCount:" + maxTargetCount);
                            maxTargetCount--;
                            Skill.Hit(t, this);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                        Skill.Hit(t, this);
                }
            }
            if (LifeTime.Update(SystemConfig.DeltaTime) && _lifeTime!=0)
            {
                Finish();
            }
            if (arrive && _lifeTime==0)
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
            float totalTime = (TargetPos - StartPosition).magnitude / BulletData.Speed * Speed;
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
            showRange.rangeRadius = radius;
            //doNotShowRange.polygonRange = AttackPoints.Select(p => new Vector2(p.x, p.y)).ToList();    
            showRange.Init();
            tiles.Add(go);
        }
    }
}
