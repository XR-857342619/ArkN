using BattleUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

namespace Units
{
    public class 干员 : Unit
    {
        public ICard Card;
        public int MainSkillId = -1;

        public DirectionEnum Direction_E;

        public CountDown Reseting = new CountDown();

        // 再部署策略：默认 / 唯一 / 队列
        public const string RedeployPolicy_Default = "默认";
        public const string RedeployPolicy_Unique = "唯一";
        public const string RedeployPolicy_Queue = "队列";

        /// <summary>当前单位生效的再部署策略；未配置或空值按“默认”处理。</summary>
        public string RedeployPolicy => string.IsNullOrEmpty(UnitData.RedeployPolicy) ? RedeployPolicy_Default : UnitData.RedeployPolicy;

        /// <summary>
        /// 再部署时间
        /// </summary>
        public float ResetTime;
        public float ResetTimeBase, ResetTimeAdd, ResetTimeRate;

        /// <summary>
        /// 建造次数
        /// </summary>
        public int BuildTime;

        public float LastCost;
        public float Cost;
        public float CostBase, CostAdd;

        public GameObject selfDirection = null;
        public GameObject dircectAssetAsset = null;

        public override void Init(bool isTmp = false)
        {
            base.Init(isTmp);
            if (UnitData.MainSkill != null)
            {
                MainSkillId = MainSkillId >　UnitData.MainSkill.Length - 1 ? 0 : MainSkillId;
                if (MainSkillId >= 0)
                    MainSkill = LearnSkill(UnitData.MainSkill[MainSkillId], null);
                else
                    MainSkill = LearnSkill(UnitData.MainSkill[0], null);
            }
            
            //Debug.Log(Position);
            //if (MainSkill == null) Log.Debug("MainSkill is null");
            //Log.Debug($"PowerType actual type: {MainSkill?.SkillData?.PowerType.GetType().Name}");
        }

        public override void baseAttributeInit()
        {
            base.baseAttributeInit();
            CostBase = UnitData.Cost + UnitData.CostEx;
            LastCost = CostBase;
            ResetTimeBase = UnitData.ResetTime + UnitData.ResetTimeEx;
        }

        public override void Refresh()
        {
            CostAdd = 0;
            ResetTimeAdd = ResetTimeRate = 0;
            base.Refresh();
            Cost = Math.Max(0, CostBase + CostAdd);
            //Debug.Log("Cost = " + CostBase + "+" + CostAdd + "=" + Cost);
            if (Cost != LastCost) UI_Battle.Instance.TriggerUnitStateUpdate(this);
            LastCost = Cost;
            //if (Cost == 0) UI_Battle.Instance.TriggerUnitStateUpdate(this);

            ResetTime = (ResetTimeBase + ResetTimeAdd) * (1 + ResetTimeRate);

            int costAll = StopUnits.Sum(x => x.StopCost);
            if (StopCount < costAll)
            {
                for (int i = StopUnits.Count - 1; i >= StopCount; i--)
                {
                    costAll -= StopUnits[i].StopCost;
                    RemoveStop(StopUnits[i]);
                    if (StopCount >= costAll) break;
                }
            }
        }

        public override void UpdateAction()
        {
            base.UpdateAction();

            //不管怎么样 都要检测阻挡是否已经失效
            foreach (var target in StopUnits.ToList())
            {
                if ((target.Position2 - Position2).magnitude > UnitData.Radius + target.UnitData.Radius + 敌人.StopExCheck || !CanStop(target))
                {
                    Debug.Log("移除阻挡");
                    RemoveStop(target);
                }
                if (Height == 0 && target.Height > 0)   // target 是飞行敌人且当前高度 0
                {
                    RemoveStop(target);
                }
            }

            //不在战场也能转放置CD
            Reseting.Update(SystemConfig.DeltaTime);
            if (State == StateEnum.Default) return;

            if (!Start.Finished() && State != StateEnum.Die)
            {
                if (Start.Update(SystemConfig.DeltaTime))
                {
                    StartEnd();
                }
                return;
            }
            if (this.State == StateEnum.Start)
            {
                return;
            }
            if (ScaleX != TargetScaleX)
            {
                var delta = Math.Sign(TargetScaleX - ScaleX) / SystemConfig.TurningTime * SystemConfig.DeltaTime;
                if (Mathf.Abs(TargetScaleX - ScaleX) < Mathf.Abs(delta))
                {
                    ScaleX = TargetScaleX;
                }
                else
                    ScaleX += delta;
            }

            if (this.State == StateEnum.Die)
            {
                UpdateDie();
            }
            else
            {
                UpdateSkills();
                if (LifeTime != null && LifeTime.Update(SystemConfig.DeltaTime))
                {
                    DoDie(null);
                }
            }
            //Recover.Update(SystemConfig.DeltaTime);
        }

        public void StartEnd()
        {
            hideBase = false;
            SetStatus(StateEnum.Idle);
            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = this,
            });
            Trigger(TriggerEnum.落地);
            Battle.TriggerDatas.Pop();
        }

        public void ChangePos(int x,int y, DirectionEnum directionEnum)
        {
            UnitModel.gameObject.SetActive(true);
            Position = Battle.Map.Tiles[x, y].Pos;
            UnitModel?.AlignHeight();
            Direction_E = directionEnum;
            ResetAttackPoint();
        }

        public void ResetAttackPoint()
        {
            switch (Direction_E)
            {
                case DirectionEnum.Right:
                    ScaleX = TargetScaleX = 1;
                    Direction = new Vector2(1, 0);
                    break;
                case DirectionEnum.Left:
                    ScaleX = TargetScaleX = -1;
                    Direction = new Vector2(-1, 0);
                    break;
                case DirectionEnum.Up:
                    Direction = new Vector2(0, -1);
                    break;
                case DirectionEnum.Down:
                    Direction = new Vector2(0, 1);
                    break;
            }
            foreach (var skill in Skills)
            {
                skill.UpdateAttackPoints();
            }
        }

        public bool CanBuild()
        {
            bool coustEnough = Battle.Cost >= GetCost() || BattleManager.Instance.IsInfCost;
            bool buildEnough = Battle.BuildCount > 0 || UnitData.BuildCountCost <= 0 || BattleManager.Instance.IsInfUnitCount;
            bool ResetFinished = Reseting.Finished() || BattleManager.Instance.IsNoCD;
            bool buildCountLimit = UnitData.BuildCountLimit <= 0 || Battle.PlayerUnits.FindAll(x => x.Id == Id && x.InputTime >= 0).Count() < UnitData.BuildCountLimit || BattleManager.Instance.IsInfUnitCount;

            return coustEnough && buildEnough && ResetFinished && buildCountLimit;
            //return GetCost() <= Battle.Cost && Reseting.Finished() && (Battle.BuildCount > 0 || UnitData.BuildCountCost <= 0);
        }

        public void JoinMap()
        {
            //Debug.Log("StartStart" + Time.time);
            SetStatus(StateEnum.Default);
            IfAlive = true;
            hideBase = true;
            if (!BattleManager.Instance.IsInfCost)
                Battle.Cost -= GetCost();
            if (!BattleManager.Instance.IsInfUnitCount)
                Battle.BuildCount -= UnitData.BuildCountCost;
            Hp = MaxHp;
            Start.Set(UnitModel.GetAnimationDuration("Start"));
            CheckBlock();
            //Debug.Log("start:" + Time.time + "," + Start.value);
            if (Start.Finished()) StartEnd();
            else
                SetStatus(StateEnum.Start);
            InputTime = Battle.Tick;
            List<Unit> tileUnits =Battle.Map.Tiles[GridPos.x, GridPos.y].Units;
            tileUnits.Add(this);

            if (dircectAssetAsset is null || selfDirection is null)
            {
                dircectAssetAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowDirection");
                selfDirection = UnityEngine.Object.Instantiate(dircectAssetAsset);
            }

            selfDirection.SetActive(true);
            selfDirection.transform.GetChild(1).localEulerAngles = new Vector3(0, Vector2.SignedAngle(Direction, Vector2.right), 0);
            selfDirection.transform.position = new Vector3(GridPos.x, Battle.Map.Tiles[GridPos.x, GridPos.y].FarAttackGrid ? 0.25f : 0, GridPos.y);
            selfDirection.transform.SetParent(Battle.Map.Tiles[GridPos.x, GridPos.y].MapGrid.transform);
            //Position = new Vector3(GridPos.x, 0, GridPos.y);
            //if (this.UnitData.NotUseTile && tileUnits.Count > 1)
            //{
            //    if (!tileUnits[tileUnits.Count - 2].UnitData.NotUseTile)
            //    {
            //        Unit tmp = tileUnits[tileUnits.Count - 2];
            //        tileUnits[tileUnits.Count - 2] = this;
            //        tileUnits[tileUnits.Count - 1] = tmp;
            //    }
            //}
            BattleUI.UI_Battle.Instance.CreateUIUnit(this);
            foreach (var skill in Skills)//重置非普攻类技能的基础cd
            {
                if (skill.SkillData.AttackMode != AttackModeEnum.跟随攻击 && skill.SkillData.MaxPower == 0) skill.ResetCooldown(1);
            }
            ApplyRedeployPolicyOnDeploy();

            foreach (var buff in Buffs)
            {
                buff.ShowEffect();
            }

            //UnitModel.Init(this);
            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = this,
            });
            Battle.Trigger(TriggerEnum.入场);
            Battle.TriggerDatas.Pop();

            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = this,
            });
            Trigger(TriggerEnum.自己入场);
            Battle.TriggerDatas.Pop();

            var joinEffect = EffectManager.Instance.GetEffect(Database.Instance.GetIndex<EffectData>("入场"));
            joinEffect.Init(this, this, Position, Direction);
        }

        public void LeaveMap(bool recoverPower = false)
        {
            SetStatus(StateEnum.Default);
            if (recoverPower)
                Battle.Cost += BattleManager.Instance.IsInfCost ? 0 : Mathf.FloorToInt(UnitData.Cost * UnitData.LeaveReturn);
            InjurePoint = 0;
            Battle.TriggerDatas.Push(new TriggerData()
            {
                Target = this,
            });
            //Debug.Log(UnitData.Name + "撤退");
            Trigger(TriggerEnum.撤退);
            Battle.TriggerDatas.Pop();
            Finish(true);
        }

        public override void Finish(bool leaveEvent = true)
        {
            base.Finish(leaveEvent);
            //UnityEngine.Object.Destroy(selfDirection);
            selfDirection?.SetActive(false);
            IfAlive = false;
            UnitModel?.gameObject.SetActive(false);
            BattleUI.UI_Battle.Instance.ReturnUIUnit(this);
            //State = StateEnum.Default;
            SetStatus(StateEnum.Default);
            Direction = new Vector2(1, 0);
            InputTime = -1;
            Battle.Map.Tiles[GridPos.x, GridPos.y].Units.Remove(this);
            ApplyRedeployPolicyOnLeave();
            BuildTime++;
            Battle.BuildCount += UnitData.BuildCountCost;
            if (UnitData.NotReturn)//消耗品
            {
                Battle.PlayerUnits.Remove(this);
                Battle.AllUnits.Remove(this);
                if (Parent != null) Parent.Children.Remove(this);
            }

            if (Parent != null && (Parent as 干员).InputTime < 0 && !this.UnitData.NotReturn)
            {
                Battle.AllUnits.Remove(this);
                Battle.PlayerUnits.Remove(this);
            }

            foreach (干员 unit in Children.Where(x => x is 干员))
            {
                // 尚未部署的子单位不需要走撤退流程，只清理引用，避免 selfDirection/UnitModel 为空触发 NRE
                if (unit.InputTime < 0 && !unit.UnitData.NotReturn)
                {
                    Battle.AllUnits.Remove(unit);
                    Battle.PlayerUnits.Remove(unit);
                    unit.Children.Clear();
                }
                else if (!unit.UnitData.NotReturn)
                {
                    unit.LeaveMap();
                }
            }
            Children.Clear();

            BattleUI.UI_Battle.Instance.UpdateUnitsLayout();
            foreach (var skill in Skills)
            {
                if (skill.StartId != -1)
                {
                    skill.DoUpgrade(skill.StartId);
                }
                else skill.Reset();
            }
        }

        /// <summary>
        /// 部署时按再部署策略处理同 Id 待部署单位的冷却。
        /// 默认：其他待部署单位立即进入冷却。
        /// 唯一：其他待部署单位不可部署（但不进入倒计时），直到场上单位离场。
        /// 队列：其他待部署单位保持原状态，各自离场后独立冷却。
        /// </summary>
        private void ApplyRedeployPolicyOnDeploy()
        {
            switch (RedeployPolicy)
            {
                case RedeployPolicy_Unique:
                    SetOtherReadyCooldown(float.PositiveInfinity);
                    break;
                case RedeployPolicy_Queue:
                    // 队列策略：部署时不改变其他单位的冷却状态
                    break;
                default:
                    SetOtherReadyCooldown(ResetTime);
                    break;
            }
        }

        /// <summary>
        /// 离场时按再部署策略处理同 Id 待部署单位的冷却。
        /// 默认：所有同 Id 待部署单位（含自己）进入冷却。
        /// 唯一：所有同 Id 待部署单位（含自己）进入冷却。
        /// 队列：仅自己进入冷却，其他单位保持原状态。
        /// </summary>
        private void ApplyRedeployPolicyOnLeave()
        {
            if (UnitData.NotReturn) return;

            switch (RedeployPolicy)
            {
                case RedeployPolicy_Queue:
                    Reseting.Set(ResetTime);
                    break;
                case RedeployPolicy_Unique:
                    SetAllReadyCooldown(ResetTime);
                    break;
                default:
                    // 默认策略：其他待部署单位从部署时已开始冷却，这里只让“已结束冷却”的单位重新进入冷却（例如刚撤退的自己）
                    SetFinishedReadyCooldown(ResetTime);
                    break;
            }
        }

        /// <summary>
        /// 设置所有同 Id 未部署单位（含自己）的再部署冷却。
        /// </summary>
        private void SetAllReadyCooldown(float time)
        {
            foreach (var unit in Battle.PlayerUnits)
            {
                if (unit.Id == Id && unit.InputTime < 0)
                {
                    unit.Reseting.Set(time);
                }
            }
        }

        /// <summary>
        /// 仅设置同 Id 未部署单位中“冷却已结束”的单位（含刚离场的自己）进入冷却。
        /// 默认策略下已处于冷却中的其他单位不会被重置。
        /// </summary>
        private void SetFinishedReadyCooldown(float time)
        {
            foreach (var unit in Battle.PlayerUnits)
            {
                if (unit.Id == Id && unit.InputTime < 0 && unit.Reseting.Finished())
                {
                    unit.Reseting.Set(time);
                }
            }
        }

        /// <summary>
        /// 设置同 Id 其他未部署单位的再部署冷却（不含自己）。
        /// </summary>
        private void SetOtherReadyCooldown(float time)
        {
            foreach (var unit in Battle.PlayerUnits)
            {
                if (unit.Id == Id && unit != this && unit.InputTime < 0)
                {
                    unit.Reseting.Set(time);
                }
            }
        }

        public override Vector2Int PointWithDirection(Vector2Int point)
        {
            switch (Direction_E)
            {
                case DirectionEnum.Right:
                    return(GridPosFloor + point);
                case DirectionEnum.Left:
                    return (GridPosFloor - point);
                case DirectionEnum.Up:
                    return (GridPosFloor + new Vector2Int(point.y, -point.x));
                case DirectionEnum.Down:
                    return (GridPosFloor + new Vector2Int(-point.y, point.x));
            }
            return GridPosFloor;
        }

        public int GetCost()
        {
            return (int)(Cost * (BuildTime == 0 ? 1 : BuildTime == 1 ? 1 + UnitData.CostAdd : 1 + UnitData.CostAdd * 2));
        }

        public bool Useable()
        {
            //return false;
            bool ResetFinished = RedeployPolicy == RedeployPolicy_Unique ? Reseting.Finished() : true;
            bool coustEnough = Battle.Cost >= GetCost() || BattleManager.Instance.IsInfCost;
            bool buildEnough = Battle.BuildCount >= UnitData.BuildCountCost || BattleManager.Instance.IsInfUnitCount;
            bool buildCountLimit = UnitData.BuildCountLimit <= 0 || Battle.PlayerUnits.FindAll(x => x.Id == Id && x.InputTime >= 0).Count() < UnitData.BuildCountLimit || BattleManager.Instance.IsInfUnitCount;
            
            return coustEnough && buildEnough && ResetFinished && buildCountLimit;
            //return BattleManager.Instance.IsInfCost ? true : GetCost() <= Battle.Cost && BattleManager.Instance.IsInfUnitCount ? true : Battle.BuildCount >= UnitData.BuildCountCost;
        }

        public override void DoDie(object source)
        {
            base.DoDie(source);

            var leaveEffect = EffectManager.Instance.GetEffect(Database.Instance.GetIndex<EffectData>("离场"));
            leaveEffect.Init(this, this, Position, Direction);
            foreach (var unit in StopUnits)
            {
                unit.StopUnit = null;
            }
            StopUnits.Clear();
        }

        public override float Hatred()
        {
            return base.Hatred();
        }

        public override bool IfStoped()
        {
            return StopUnits.Count > 0;
        }

        void CheckBlock()
        {
            var blockUnits = Battle.FindAll(Position2, UnitData.Radius, 1);
            blockUnits.RemoveWhere(x => x is not 敌人);
            foreach (Units.敌人 u in blockUnits)
            {
                if (CanStop(u))
                {
                    u.StopUnit = this;
                    StopUnits.Add(u);
                    Vector2 pos;
                    if ((u.Position2 - Position2).sqrMagnitude < 0.001f)
                        pos = this.GridPos;
                    else
                        pos = Position2 + (u.Position2 - Position2).normalized * (u.UnitData.Radius + UnitData.Radius);
                    u.Position = new Vector3(pos.x, Position.y, pos.y);
                }
            }
        }

        public override bool Alive()
        {
            return base.Alive() && InputTime > 0;
        }

        public override void CreateModel()
        {
            base.CreateModel();
            UnitModel.gameObject.SetActive(false);
        }
    }
}
