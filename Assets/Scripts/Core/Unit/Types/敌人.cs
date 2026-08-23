using FairyGUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

namespace Units
{
    /**
     普通敌人逻辑:
        出生确定行动路线，按照设置好的路径点挨个走最近路线。
        每帧先跑一遍buff，刷新buff数值
        判断死亡,
        判断阻挡,
        判断技能，
        判断移动。
        [对于多数敌人来说，技能可以释放的条件会加上“被阻挡”]
        最后判断移动，如果当前格子有阻挡者，也会阻止移动。
         */
    public class 敌人 : Unit
    {
        public const float StopExCheck = 0.01f, TempArriveDistance = 0.01f;
        public Unit StopUnit;
        new public 敌人 Parent;

        public WaveInfo WaveData;//=> Database.Instance.Get<WaveData>(WaveId);

        /// <summary>寻路逻辑已抽离到该组件中，敌人本体只负责移动、阻挡、技能等状态逻辑。</summary>
        public EnemyPathfinder Pathfinder = new EnemyPathfinder();

        // 以下属性均代理到 Pathfinder，保持旧代码的访问方式不变。
        public CountDown PathWaiting => Pathfinder.PathWaiting;
        public List<PathPoint> PathPoints => Pathfinder.PathPoints;
        public List<PathPoint> CheckPoints => Pathfinder.CheckPoints;
        public int currentPathIndex { get => Pathfinder.currentPathIndex; set => Pathfinder.currentPathIndex = value; }
        public int currentCheckIndex { get => Pathfinder.currentCheckIndex; set => Pathfinder.currentCheckIndex = value; }
        protected PathPoint NowPathPoint => Pathfinder.NowPathPoint;
        protected PathPoint NextPathPoint => Pathfinder.NextPathPoint;
        public PathPoint NowCheckPoint => Pathfinder.NowCheckPoint;
        public PathPoint NextCheckPoint => Pathfinder.NextCheckPoint;
        public bool OnlyCheckPoint { get => Pathfinder.OnlyCheckPoint; set => Pathfinder.OnlyCheckPoint = value; }
        public bool NeedResetPath { get => Pathfinder.NeedResetPath; set => Pathfinder.NeedResetPath = value; }
        public List<Vector3> TempPath { get => Pathfinder.TempPath; set => Pathfinder.TempPath = value; }
        public int TempIndex { get => Pathfinder.TempIndex; set => Pathfinder.TempIndex = value; }
        protected Vector3 TempTarget => Pathfinder.TempTarget;
        public List<PathPoint> tmpPathPointList => Pathfinder.tmpPathPointList;
        public List<CountDown> tmpPathPointLastList => Pathfinder.tmpPathPointLastList;

        public bool Visiable = true;
        public bool UnStopped;
        public int StopCost;
        public float WaitTimeEx;

        public float distance2Final = 0;
        public bool distanceChenged = true;

        /// <summary>上次刷新技能攻击范围时所在的格子，用于避免同格内每帧无意义刷新。</summary>
        private Vector2Int lastAttackGridPos = new Vector2Int(int.MinValue, int.MinValue);

        public override void Init()
        {
            base.Init();
            InputTime = Battle.Tick;
            StopCost = 1;
            if (UnitData.StopCount != 0) StopCost = UnitData.StopCount;

            // 路径初始化已抽离到 EnemyPathfinder
            Pathfinder.Initialize(this, WaveData);

            // 出生位置确定后，立即刷新一次技能攻击范围（base.Init 中技能是在 Position 为 0 时计算的）
            lastAttackGridPos = GridPosFloor;
            RefreshSkillAttackPoints();

            // 出生位置确定后重新对齐一次地面（base.Init 时 Position 还是 0,0）
            UnitModel?.AlignHeight();

            SetStatus(StateEnum.Idle);
            BattleUI.UI_Battle.Instance.CreateUIUnit(this);

            hideBase = true;
            Start.Set(UnitModel.GetAnimationDuration("Start"));
            if (Start.Finished()) StartEnd();
            else
                SetStatus(StateEnum.Start);
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

        public override void Finish(bool leaveEvent = true)
        {
            if (StopUnit != null) StopUnit.RemoveStop(this);
            Hp = 0;
            base.Finish(leaveEvent);
            //Debug.Log($"{UnitData.Id}Finish");
            
            Hp = 0;
            if (!UnitData.WithoutCheckCount && Parent == null)
            {
                Battle.EnemyCount--;
                Battle.CheckPoints.Add(Battle.Tick);
            }
            BattleUI.UI_Battle.Instance.ReturnUIUnit(this);
            Battle.AllUnits.Remove(this);
            Battle.Enemys.Remove(this);
            Battle.PlayerUnits2.Remove(this);
            GameObject.Destroy(UnitModel.gameObject);
            UnitModel = null;
        }

        public override void Refresh()
        {
            UnStopped = false;
            WaitTimeEx = 0;
            base.Refresh();
            if (!Visiable) IfSelectable = false;
        }

        public override void UpdateAction()
        {
            if (!Start.Finished() && State != StateEnum.Die)
            {
                if (Start.Update(SystemConfig.DeltaTime))
                {
                    StartEnd();
                }
                return;
            }
            if (State == StateEnum.Default) return;
            if (!Visiable) if (PathWaiting.Update(SystemConfig.DeltaTime + WaitTimeEx)) FinishHide();
            if (!Visiable) return;
            base.UpdateAction();
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
            }
            //Recover.Update(SystemConfig.DeltaTime);

            CheckBlock();
            //ScaleX==TargetScaleX &&
            if (State == StateEnum.Move || State == StateEnum.Idle)
            {
                UpdateMove();
            }
        }

        /// <summary>
        /// 判断是否有人在阻挡自己
        /// </summary>
        public virtual void CheckBlock()
        {
            if (!Alive() || Hp <= 0) return;
            
            if (StopUnit != null && StopUnit.Height != Height)
            {
                StopUnit.RemoveStop(this);   // 让阻挡者把我放掉
                return;                      // 本帧不再查找新阻挡
            }

            if (StopUnit != null) return;

            var potentialBlockers = Battle.FindAll(Position2, UnitData.Radius + StopExCheck, 1)
            .Where(x => x.CanStop(this) && x.Height >= Height) // 添加高度检查
            .ToList();

            //// 按距离排序
            //potentialBlockers.OrderBy(x => (x.Position2 - Position2).magnitude);

            Unit closest = null;
            float minDist = float.MaxValue;
            foreach (var unit in potentialBlockers)
            {
                float dist = (unit.Position2 - Position2).sqrMagnitude;
                if (dist < minDist) { minDist = dist; closest = unit; }
            }

            if (potentialBlockers.Count > 0)
            {
                closest.AddStop(this);

                Battle.TriggerDatas.Push(new TriggerData()
                {
                    User = closest,
                    Target = this,
                });
                Trigger(TriggerEnum.阻挡);
                Battle.TriggerDatas.Pop();
            }
        }

        public virtual void CheckArrive()
        {
            Pathfinder.CheckArrival();
        }

        public void FinishHide()
        {
            Pathfinder.FinishHideMove();
        }

        public void Jump(float distance)
        {
            if (!Pathfinder.HasPath) return;
            if (StopUnit != null) StopUnit.RemoveStop(this);
            if (TempPath == null) Pathfinder.FindNewPath(OnlyCheckPoint);
            List<Vector3> points = new List<Vector3>();
            points.Add(Position);
            for (int i = TempIndex + 1; i < TempPath.Count; i++)
            {
                points.Add(TempPath[i]);
            }
            //int pathIndex = PathPoints.IndexOf(NowPathPoint);
            int pathIndex = currentPathIndex;
            int index = 1;
            while (distance > 0)
            {
                if (index >= points.Count)
                {
                    if (pathIndex >= PathPoints.Count - 1)//跳跃后已经可以进门了
                    {
                        Position = PathPoints[PathPoints.Count - 1].Pos;
                        break;
                    }
                    else
                    {
                        //否则把下个寻路节点找到
                        var offset = new Vector3(WaveData.OffsetX, 0, WaveData.OffetsetY);
                        List<Vector3> tempPath;
                        if (Height <= 0)
                            //tempPath = Battle.Map.FindPath(Position - offset, GetPoint(pathIndex + 1) - offset, PathPoints[NowPathPoint].DirectMove);
                            tempPath = AStarPathFinder.FindPath(Battle.Map.Tiles, new List<Vector3> { Position - offset, Pathfinder.GetPoint(pathIndex + 1).Pos - offset }, false);
                        else
                            tempPath = new List<Vector3>() { Position - offset, Pathfinder.GetPoint(pathIndex + 1).Pos - offset };
                        for (int i = 1; i < tempPath.Count; i++) //注意不要把起点加进去了
                        {
                            Vector3 p = tempPath[i];
                            points.Add(p + offset);
                        }
                        pathIndex++;
                    }
                }
                float pathDist = (points[index] - points[index - 1]).sqrMagnitude;
                if (pathDist > distance)
                {
                    Position = points[index - 1] + (points[index] - points[index - 1]).normalized * distance;
                    distance = 0;
                }
                else
                {
                    distance -= pathDist;
                    index++;
                }
            }

            if (NowPathPoint != PathPoints[pathIndex])
            {
                //NowPathPoint = PathPoints[pathIndex];
                //NextPathPoint = PathPoints[pathIndex + 1];
                foreach (var p in PathPoints)
                {
                    if (p == NowPathPoint) break;
                    if (!p.IsArrive) continue;
                    p.IsArrive = true;
                }
                PathWaiting.Finish();
                NeedResetPath = true;
            }
            else
            {
                TempIndex = TempPath.IndexOf(points[index - 1]);
            }
        }

        private new void UpdateMove()
        {
            if (!Pathfinder.HasPath)
            {
                if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                return;
            }

            // 先更新临时路径点（到期的点会被移除并触发重寻路）
            Pathfinder.UpdateTempPoints(SystemConfig.DeltaTime);

            // 补充规则：等待计时在向临时点移动/等待期间继续倒计时。
            if (tmpPathPointList.Count > 0 || Pathfinder.TempPointWaiting)
            {
                PathWaiting.Update(SystemConfig.DeltaTime);
            }

            // 普通路径点等待：只有没有临时路径点插入时才执行。
            if (!PathWaiting.Finished() && tmpPathPointList.Count == 0 && !Pathfinder.TempPointWaiting)
            {
                if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                PathWaiting.Update(SystemConfig.DeltaTime + WaitTimeEx);
                return;
            }

            // 等待临时路径点到期。
            if (Pathfinder.TempPointWaiting)
            {
                if (NeedResetPath)
                {
                    Pathfinder.TempPointWaiting = false;
                }
                else
                {
                    if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                    return;
                }

                // 临时点到期后，若原路径点等待仍有剩余，则原地继续等待。
                if (!PathWaiting.Finished() && tmpPathPointList.Count == 0)
                {
                    if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                    return;
                }
            }

            CheckArrive();

            // CheckArrive 可能刚到达临时路径点，本帧不再继续移动。
            if (Pathfinder.TempPointWaiting)
            {
                if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                return;
            }

            if (TempPath == null || NeedResetPath)//无路径或因为外力走出了预定路线，重寻路
            {
                Pathfinder.FindNewPath(OnlyCheckPoint);
            }

            if (Unbalance || !Visiable) return;//失衡状态下不许主动移动
            if (StopUnit != null)
            {
                if (AnimationName == GetMoveAnimation())
                {
                    SetStatus(StateEnum.Idle);
                }
                return;//有人阻挡，停止移动
            }
            if (Speed == 0)
            {
                if (AnimationName == GetMoveAnimation())
                {
                    SetStatus(StateEnum.Idle);
                }
                return;
            }

            AnimationName = GetMoveAnimation();
            AnimationSpeed = 1;

            var delta = TempTarget - Position;
            if (delta != Vector3.zero) Direction = new Vector2(delta.x, delta.z);
            float scaleX = TargetScaleX;
            if (delta.x > 0) scaleX = 1;
            else if (delta.x < 0) scaleX = -1;
            else
            {
                bool success = false;
                for (int i = currentPathIndex + 1; i < PathPoints.Count; i++)
                {
                    PathPoint point = Pathfinder.GetPoint(i);
                    if (point == null) continue;
                    var x = point.Pos.x;
                    if (x != Position.x)
                    {
                        scaleX = Math.Sign(x - Position.x);
                        success = true;
                    }
                }
                if (!success)
                    scaleX = TargetScaleX;
            }
            TargetScaleX = scaleX;

            var target = Position + (TempTarget - Position).normalized * Speed * SystemConfig.DeltaTime;
            Position = target;

            // 只有跨过格子边界时才需要刷新攻击范围，同格内移动不刷新
            if (GridPosFloor != lastAttackGridPos)
            {
                lastAttackGridPos = GridPosFloor;
                RefreshSkillAttackPoints();
            }

            distanceChenged = true;
        }

        /// <summary>
        /// 刷新所有技能的攻击范围。仅在跨格或传送/出生等位置发生实质变化时调用。
        /// </summary>
        private void RefreshSkillAttackPoints()
        {
            foreach (var skill in Skills)
            {
                if (skill != null)
                    skill.UpdateAttackPoints();
            }
        }

        public override void DoDie(object source)
        {
            if (StopUnit != null)
            {
                StopUnit.RemoveStop(this);
            }
            beAttacked.Finished();
            UnitModel?.ResetColor();
            UnitModel?.SetColor(Color.black);
            //Battle.EnemyCount--;
            base.DoDie(source);
        }

        public void DisplayPath()
        {
            TrailManager.Instance.ShowPath(TempPath);
        }

        //public override float distanceToFinal()
        //{
        //    float result = 0;
        //    for (int i = currentPathIndex+1 ; i < PathPoints.Count-1; i++)
        //    {
        //        result += (GetPoint(i).Pos - GetPoint(i + 1).Pos).sqrMagnitude;
        //    }
        //    if (TempPath != null)
        //    {
        //        for (int i = TempIndex + 1; i < TempPath.Count - 1; i++)
        //        {
        //            result += Mathf.Abs(TempPath[i].x - TempPath[i + 1].x) + Mathf.Abs(TempPath[i].y - TempPath[i + 1].y);
        //        }
        //        result += (Position - TempTarget).sqrMagnitude;
        //    }
        //    return result;
        //}

        public override float distanceToFinal()
        {
            // 检查缓存
            if (!distanceChenged)
            {
                return distance2Final;
            }

            float totalDistance = Pathfinder.DistanceToFinal(Position);

            distanceChenged = false;
            distance2Final = totalDistance;

            return totalDistance;
        }


        public override float Hatred()
        {
            return base.Hatred();
        }

        public override bool IfStoped()
        {
            return StopUnit != null;
        }

        public override void UpdatePush()
        {
            bool wasUnbalanced = Unbalance;
            base.UpdatePush();
            // 失衡（被推拉）期间，每帧检查是否经过了下一未到达检查点中心。
            if (wasUnbalanced || Unbalance)
            {
                Pathfinder.MarkCheckpointsReachedAtPosition(Position);
            }
        }

        /// <summary>
        /// 传送完成后调用：检查传送目标是否正好是下一未到达检查点，
        /// 然后无论是否命中都进入重寻路流程。
        /// </summary>
        public void AfterTeleport(Vector3 pos)
        {
            Pathfinder.MarkCheckpointsReachedAtPosition(pos);
            NeedResetPath = true;
            OnlyCheckPoint = true;
        }


        protected override void RecoverBalance()
        {
            base.RecoverBalance();
            //因推拉等外力导致偏移路线时，需要结束等待并,重新寻路
            NeedResetPath = true;
        }
        public bool IsCanArrive(Vector3 start, Vector3 end)
        {
            return Pathfinder.IsCanArrive(start, end);
        }

        public bool AddTmpPathPoint(Vector3 pos, float time)
        {
            bool success = Pathfinder.TryAddTempPoint(pos, time);
            if (success)
                DisplayPath();
            return success;
        }
    }
}
