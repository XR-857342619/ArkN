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
        //public int WaveId;
        /// <summary>
        /// 当前走到第几个目标点
        /// </summary>
        //public int NowPathPoint;
        //protected Vector3 NextPathPoint => GetPoint(NowPathPoint + 1);

        public CountDown PathWaiting = new CountDown();
        public List<PathPoint> PathPoints = new List<PathPoint>();
        public List<PathPoint> CheckPoints = new List<PathPoint>();
        
        public int currentPathIndex = 0;
        public int currentCheckIndex = 0;
        //protected PathPoint NowPathPoint => OnlyCheckPoint ? NowCheckPoint : currentPathIndex < PathPoints.Count ? PathPoints[currentPathIndex] : null;
        //protected PathPoint NextPathPoint => OnlyCheckPoint ? NextCheckPoint : (currentPathIndex + 1) < PathPoints.Count ? PathPoints[currentPathIndex + 1] : null;
        protected PathPoint NowPathPoint => OnlyCheckPoint ? GetPoint(currentCheckIndex) : GetPoint(currentPathIndex);
        protected PathPoint NextPathPoint => OnlyCheckPoint ? GetPoint(currentCheckIndex + 1) : GetPoint(currentPathIndex + 1);
        public PathPoint NowCheckPoint => CheckPoints[currentCheckIndex];
        public PathPoint NextCheckPoint => currentCheckIndex >= CheckPoints.Count - 1 ? CheckPoints[CheckPoints.Count - 1] : CheckPoints[currentCheckIndex + 1];
        //public PathPoint NextCheckPoint => CheckPoints[currentCheckIndex + 1];

        public bool OnlyCheckPoint = false;
        public bool NeedResetPath;


        public List<Vector3> TempPath;
        public int TempIndex;
        protected Vector3 TempTarget => TempIndex >= TempPath.Count - 1 ? TempPath[TempPath.Count - 1] : TempPath[TempIndex + 1];

        public bool Visiable = true;
        public bool UnStopped;
        public int StopCost;
        public float WaitTimeEx;

        public List<PathPoint> tmpPathPointList = new List<PathPoint>();
        public List<CountDown> tmpPathPointLastList = new List<CountDown>();

        //List<PathPoint> toRemoveTmpPathPointList = new List<PathPoint>();
        //List<CountDown> toRemoveTmpPathPointLastList = new List<CountDown>();

        public override void Init()
        {
            base.Init();
            InputTime = Battle.Tick;
            StopCost = 1;
            if (UnitData.StopCount != 0) StopCost = UnitData.StopCount;
            
            PathPoints.AddRange(Battle.MapData.PathInfos.Find(x => x.Name == WaveData.Path).Path); //PathManager.Instance.GetPath(WaveData.Path);

            PathPoints[0].CheckPoint = true;
            PathPoints[PathPoints.Count - 1].CheckPoint = true;

            if (PathPoints.Count >= 1) PathPoints[0].IsArrive = true;
            CheckPoints = PathPoints.Where(x => x.CheckPoint).ToList();

            Debug.Log("CheckPointsCount:"+CheckPoints.Count);

            //currentCheckIndex ++;
            //NowPathPoint = PathPoints.FindLast(x => x.IsArrive == true);
            //NextPathPoint = PathPoints.Find(x => x.IsArrive == false);

            Position = GetPoint(0).Pos;
            Position.y = Battle.Map.Tiles[GridPos.x, GridPos.y].Pos.y;
            PathWaiting.Set(PathPoints[0].Delay);

            //findNewPath(OnlyCheckPoint);
            ScaleX = TargetScaleX = (NextPathPoint.Pos.x - Position.x) > 0 ? 1 : -1;
            SetStatus(StateEnum.Idle);
            BattleUI.UI_Battle.Instance.CreateUIUnit(this);

            hideBase = true;
            Start.Set(UnitModel.GetAnimationDuration("Start"));
            if (Start.Finished()) StartEnd();
            else
                SetStatus(StateEnum.Start);
            //tmpPathPointLast.Finish();
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
            if (StopUnit != null) return;
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
            //Debug.Log("CheckArrive");
            if (TempPath == null) return;

            if ((TempTarget - Position).sqrMagnitude <= TempArriveDistance)
            {
                //Debug.Log("Arrived" + TempTarget.ToV2().ToString());
                TempIndex++;
                //Debug.Log(TempIndex);
                if (TempIndex == TempPath.Count - 1)
                {
                    Debug.Log("Arrived End");

                    currentPathIndex++;
                    NowPathPoint.IsArrive = true;

                    if (NowPathPoint.CheckPoint) currentCheckIndex++;

                    //Debug.Log("CheckPointIndex:" + currentCheckIndex);
                    //Debug.Log(NowPathPoint.Pos + "->" + PathPoints[PathPoints.Count-1].Pos);
                    if ((PathPoints[PathPoints.Count - 1].Pos - Position).sqrMagnitude <= TempArriveDistance)
                    {
                        //破门了
                        Battle.DoDamage(UnitData.Damage);
                        Battle.TriggerDatas.Push(new TriggerData()
                        {
                            User = this,
                            //Skill = this,
                        });
                        this.Trigger(TriggerEnum.到达路径终点);
                        Battle.TriggerDatas.Pop();
                        Finish(true);
                        //return;
                    }

                    PathWaiting.Set(NowPathPoint.Delay);
                    if (NowPathPoint.HideMove)
                    {
                        //Debug.Log("隐藏移动");
                        if (PathWaiting.value > 0)
                            Visiable = false;
                        else
                            FinishHide();
                    }
                    
                    //Debug.Log("当前路径点索引"+ currentPathIndex +"下一路径点" + NextPathPoint.Pos.ToV2());

                    TempPath = null;
                    TempIndex = 0;
                    //NeedResetPath = true;
                }
            }
        }

        void FinishHide()
        {
            NextPathPoint.IsArrive = true;
            //NowPathPoint = NextPathPoint;
            //NextPathPoint = PathPoints[PathPoints.IndexOf(NowPathPoint) + 1];
            Position = NextPathPoint.Pos;
            currentPathIndex++;
            if (NowPathPoint.CheckPoint) currentCheckIndex++;
            Visiable = true;
            UnitModel?.gameObject.SetActive(true);
            //Refresh();
            findNewPath(OnlyCheckPoint);
        }

        public void Jump(float distance)
        {
            if (StopUnit != null) StopUnit.RemoveStop(this);
            if (TempPath == null) findNewPath(OnlyCheckPoint);
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
                            tempPath = AStarPathFinder.FindPath(Battle.Map.Tiles, new List<Vector3> { Position - offset, GetPoint(pathIndex + 1).Pos - offset }, false);
                        else
                            tempPath = new List<Vector3>() { Position - offset, GetPoint(pathIndex + 1).Pos - offset };
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
            if (tmpPathPointList.Count > 0)
            {
                //toRemoveTmpPathPointList.Clear();
                //toRemoveTmpPathPointLastList.Clear();
                foreach (CountDown tmpPathPointLast in tmpPathPointLastList)
                {
                    tmpPathPointLast.Update(SystemConfig.DeltaTime);
                    if (tmpPathPointLast.Finished())
                    {
                        int index = tmpPathPointLastList.IndexOf(tmpPathPointLast);
                        //toRemoveTmpPathPointLastList.Add(tmpPathPointLast);
                        //toRemoveTmpPathPointList.Add(tmpPathPointList[index]);
                        tmpPathPointList[index].IsArrive = true;
                        Debug.Log("移除临时路径点成功:" + tmpPathPointList[index].Pos);
                    }
                }
                if (!tmpPathPointList.Any(x => x.IsArrive) && !tmpPathPointLastList.Any(x => !x.Finished()))
                {
                    //tmpPathPointLastList.RemoveAll(x => toRemoveTmpPathPointLastList.Contains(x));
                    ////PathPoints.RemoveAll(x => toRemoveTmpPathPointList.Contains(x));
                    //tmpPathPointList.RemoveAll(x => toRemoveTmpPathPointList.Contains(x));
                    ////NowPathPoint += toRemoveTmpPathPointLastList.Count;
                    ////Debug.Log(PathPoints.Count + " NowPathPoint:" + NowPathPoint);
                    ////NowPathPoint -= toRemoveTmpPathPointLastList.Count();
                    ////NowPathPoint--;
                    
                    tmpPathPointLastList.Clear();
                    tmpPathPointList.Clear();

                    NeedResetPath = true;
                }
            }
                //tmpPathPointLast.update(SystemConfig.DeltaTime);
            if (!PathWaiting.Finished() && tmpPathPointList.Count == 0)
            {
                if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
                PathWaiting.Update(SystemConfig.DeltaTime + WaitTimeEx);
                return;
            }
            CheckArrive();
            if (TempPath == null || NeedResetPath)//无路径或因为外力走出了预定路线，重寻路
            {
                findNewPath(OnlyCheckPoint);
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

            //if (IsCanArrive(Position, NextPathPoint))


            var delta = TempTarget - Position;
            if (delta != Vector3.zero) Direction = new Vector2(delta.x, delta.z);
            float scaleX = TargetScaleX;
            if (delta.x > 0) scaleX = 1;
            else if (delta.x < 0) scaleX = -1;
            else
            {
                bool success = false;
                for (int i = currentPathIndex+1; i < PathPoints.Count; i++)
                {
                    var x = GetPoint(i).Pos.x;
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

            //if ((TempTarget - Position).sqrMagnitude < Speed * SystemConfig.DeltaTime * 0.1f)
            //{
            //    //Debug.Log("Arrive");
            //    Position = TempTarget;
            //    //抵达临时目标
            //    TempIndex++;
            //    if (TempIndex >= TempPath.Count - 1)
            //    {
            //        // 走完临时路径，推进全局路径索引
            //        currentPathIndex++;

            //        Debug.Log(NowPathPoint?.Pos.ToV2() ?? null);
            //        Debug.Log(PathPoints.Last());

            //        if (NowPathPoint == PathPoints.Last())
            //        {
            //            //破门了
            //            Battle.DoDamage(UnitData.Damage);
            //            Battle.TriggerDatas.Push(new TriggerData()
            //            {
            //                User = this,
            //                //Skill = this,
            //            });
            //            this.Trigger(TriggerEnum.到达路径终点);
            //            Battle.TriggerDatas.Pop();
            //            Finish(true);
            //        }
            //        else
            //        {
            //            // 重置等待，准备寻路到下一个点
            //            PathWaiting.Set(NowPathPoint.Delay);
            //            TempPath = null; // 触发下帧重新寻路
            //        }
            //    }
            //}
            //else
            //{
            //    var target = Position + (TempTarget - Position).normalized * Speed * SystemConfig.DeltaTime;
            //    Position = target;
            //    //Debug.Log(Position.x);
            //}
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

        void findNewPath(bool OnlyChekPoint)
        {
            var start = Position;
            //if (OnlyChekPoint) currentPathIndex = PathPoints.IndexOf(PathPoints.First(x => !x.IsArrive && x.CheckPoint));

            //var tmpCheckPoint = tmpPathPointList.FirstOrDefault(x => !x.IsArrive);

            var end = NextPathPoint?.Pos ?? start;
            //Debug.Log(NextPathPoint);
            Debug.Log("临时路径终点:" + end.ToV2());

            //Debug.Log("NowPathPoint:" + NowPathPoint);
            var offset = new Vector3(WaveData.OffsetX, 0, WaveData.OffetsetY);

            if (!(Mathf.RoundToInt(start.x) == Mathf.RoundToInt(end.x) && Mathf.RoundToInt(start.z) == Mathf.RoundToInt(end.z)))
            {
                if (Height <= 0)
                    //TempPath = Battle.Map.FindPath(Position - offset, NextPathPoint - offset, PathPoints[NowPathPoint].DirectMove);
                    TempPath = AStarPathFinder.FindPath(Battle.Map.Tiles, new List<Vector3> { start - offset, end - offset }, false);
                else
                    TempPath = new List<Vector3>() { start - offset, end - offset };
            }
            else TempPath = new List<Vector3>() { start - offset, end - offset };
            //Debug.Log(UnitData.Id+ index + " find new path:"+TempPath.Count);
            //if (TempPath.Count == 0)
            //    TempPath.Add(start - offset);
            //    if ((start - end).sqrMagnitude <= TempArriveDistance)
            //        TempPath.Add(end - offset);
            for (int i = 0; i < TempPath.Count; i++)
            {
                TempPath[i] += offset;
            }

            var log = "";
            foreach (var p in TempPath) log += p.ToString() + ",";
            Debug.Log($"Path:{log}");

            if (OnlyChekPoint) DisplayPath();

            TempIndex = 0;
            NeedResetPath = false;
        }

        public void DisplayPath()
        {
            //List<Vector3> p = new List<Vector3>();
            ////for (int i = NowPathPoint; i < PathPoints.Count - 1; i++)
            ////{
            ////    //var p1 = Battle.Map.FindPath(PathPoints[i].Pos, PathPoints[i + 1].Pos, PathPoints[i].DirectMove);
            ////    p.Add(PathPoints[i].Pos);
            ////}
            //p.AddRange(PathPoints.Where(x => !x.IsArrive).Select(x => x.Pos));
            TrailManager.Instance.ShowPath(TempPath);
        }

        PathPoint GetPoint(int index)
        {
            //PathPoint result = null;
            var tmpCheckPoint = tmpPathPointList.FirstOrDefault(x => !x.IsArrive);

            //if (tmpCheckPoint is not null) result = tmpCheckPoint;
            if (tmpCheckPoint is not null) return tmpCheckPoint;
            //else if (OnlyCheckPoint) result = NowCheckPoint;
            if (OnlyCheckPoint && index <= CheckPoints.Count - 1) return CheckPoints[index];
            //else if (index < PathPoints.Count) result = PathPoints[index];
            if (index < PathPoints.Count) return PathPoints[index];
            //Debug.Log(result.Pos.ToV2());
            //return result;
            return null;
        }

        //PathPoint GetNextPathPoint(PathPoint nowPoint)
        //{
        //    // 1. 提前获取当前点的索引，避免重复查找和计算
        //    int currentIndex = PathPoints.IndexOf(nowPoint);

        //    // 边界保护（防止 nowPoint 不在列表中导致 IndexOf 返回 -1）
        //    if (currentIndex == -1) return null;

        //    if (OnlyCheckPoint)
        //    {
        //        // 2. 查找下一个检查点
        //        var nextCheckPoint = PathPoints.Find(x => !x.IsArrive && x.CheckPoint);

        //        // 3. 如果当前点就是那个检查点，返回“非检查点列表”中的第2个点
        //        if (nextCheckPoint == nowPoint)
        //        {
        //            // 优化：使用 LINQ 替代 FindAll，避免创建临时列表，直接取第2个元素
        //            return PathPoints.Where(x => !x.IsArrive && !x.CheckPoint).Skip(1).FirstOrDefault();
        //        }
        //    }

        //    // 4. 默认情况：返回下一个点
        //    return PathPoints[currentIndex + 1];
        //}

        public override float distanceToFinal()
        {
            float result = 0;
            for (int i = currentPathIndex+1 ; i < PathPoints.Count-1; i++)
            {
                result += (GetPoint(i).Pos - GetPoint(i + 1).Pos).sqrMagnitude;
            }
            if (TempPath != null)
            {
                for (int i = TempIndex + 1; i < TempPath.Count - 1; i++)
                {
                    result += Mathf.Abs(TempPath[i].x - TempPath[i + 1].x) + Mathf.Abs(TempPath[i].y - TempPath[i + 1].y);
                }
                result += (Position - TempTarget).sqrMagnitude;
            }
            return result;
        }
        
        public override float Hatred()
        {
            return base.Hatred();
        }

        public override bool IfStoped()
        {
            return StopUnit != null;
        }

        protected override void RecoverBalance()
        {
            base.RecoverBalance();
            //因推拉等外力导致偏移路线时，需要结束等待并,重新寻路
            NeedResetPath = true;
        }
        public bool IsCanArrive(Vector3 start, Vector3 end)
        {
            //var path = Battle.Map.FindPath(start.Pos, end.Pos, start.DirectMove);
            var path = AStarPathFinder.FindPath(Battle.Map.Tiles, new List<Vector3> { start, end }, false);
            if (Battle.Map.Tiles[(int)start.x, (int)start.y].FarAttackGrid != Battle.Map.Tiles[(int)end.x, (int)end.y].FarAttackGrid)
                return false;
            if (path.Count > 0) return true;
            
            Debug.Log("目标"+ end.ToV2().ToString() +"无法到达");

            return false;
        }

        // 待优化
        public bool AddTmpPathPoint(Vector3 pos, float time)
        {
            PathPoint tmpPoint = new PathPoint() { Pos = pos, Delay = time, HideMove = false, CheckPoint = true, IsArrive = false };
            if (IsCanArrive(Position, pos))
            {
                PathPoint tmp = tmpPathPointList.FirstOrDefault(x => x.Pos == pos);
                if (tmp is not null)
                {
                    int index = tmpPathPointList.IndexOf(tmp);
                    tmpPathPointLastList[index].value += time;
                    PathWaiting.Finish();

                    Debug.Log("更新临时路径点成功:" + pos + "lasttime:" + time);
                    return true;
                }
                tmpPathPointList.Add(tmpPoint);
                tmpPathPointLastList.Add(new CountDown(time));
                //PathPoints.Insert(PathPoints.IndexOf(NowPathPoint) + tmpPathPointList.Count, tmpPoint);
                //PathWaiting.Finish();
                Debug.Log(NextPathPoint.Pos.ToV2());
                findNewPath(OnlyCheckPoint);
                Debug.Log("插入临时路径点成功:" + pos + "lasttime:" + time);
                DisplayPath();
                return true;
            }
            else
                return false;
        }
    }
}
