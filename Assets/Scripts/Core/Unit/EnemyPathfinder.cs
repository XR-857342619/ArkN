using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Units
{
    /// <summary>
    /// 敌人专属寻路器。
    /// 把“路径点维护 -> 临时路径点插入 -> A* 寻路 -> 到达检测”等逻辑从 敌人 中抽离，
    /// 集中在这里维护，便于阅读和调试。
    ///
    /// 基本流程：
    /// 1. 出生时 Initialize() 读取波次配置中的 PathInfo，得到一串 PathPoint；
    /// 2. 每个 PathPoint 之间可能不是直线可达，因此每次需要移动时调用 FindNewPath()，
    ///    用 A* 算出当前位置到 NextPathPoint 的 TempPath；
    /// 3. UpdateMove 中沿 TempPath 逐段移动；
    /// 4. CheckArrival() 判断是否到达 TempPath 末尾，到达后推进 currentPathIndex / currentCheckIndex；
    /// 5. 若期间有技能插入临时路径点，TryAddTempPoint() 会临时改变当前目标点。
    /// </summary>
    public class EnemyPathfinder
    {
        public 敌人 Owner;

        /// <summary>出生时读取到的全部原始路径点（含 CheckPoint）。</summary>
        public List<PathPoint> PathPoints = new List<PathPoint>();

        /// <summary>所有 CheckPoint 路径点，供 OnlyCheckPoint 模式使用。</summary>
        public List<PathPoint> CheckPoints = new List<PathPoint>();

        /// <summary>当前原始路径点下标。</summary>
        public int currentPathIndex;

        /// <summary>当前 CheckPoint 下标。</summary>
        public int currentCheckIndex;

        /// <summary>为 true 时只沿 CheckPoint 移动（传送、重寻路等场景会打开）。</summary>
        public bool OnlyCheckPoint;

        /// <summary>因外力/传送导致偏离原路线，需要重新调用 FindNewPath。</summary>
        public bool NeedResetPath;

        /// <summary>当前路径点等待计时。</summary>
        public CountDown PathWaiting = new CountDown();

        /// <summary>A* 计算出的“当前位置 -> 下一路径点”的临时路径。</summary>
        public List<Vector3> TempPath;

        /// <summary>当前已经走到 TempPath 的第几个点。</summary>
        public int TempIndex;

        /// <summary>技能插入的临时路径点。</summary>
        public List<PathPoint> tmpPathPointList = new List<PathPoint>();

        /// <summary>与 tmpPathPointList 一一对应的持续时间计时器。</summary>
        public List<CountDown> tmpPathPointLastList = new List<CountDown>();

        // 更新临时路径点时，用于暂存本帧需要移除的项，避免遍历中修改列表。
        private readonly List<PathPoint> toRemoveTmpPathPointList = new List<PathPoint>();
        private readonly List<CountDown> toRemoveTmpPathPointLastList = new List<CountDown>();

        /// <summary>当前位置对应的“当前路径点”。</summary>
        public PathPoint NowPathPoint => GetPoint(OnlyCheckPoint ? currentCheckIndex : currentPathIndex);

        /// <summary>当前位置要前往的“下一路径点”。</summary>
        public PathPoint NextPathPoint => GetPoint(OnlyCheckPoint ? currentCheckIndex + 1 : currentPathIndex + 1);

        public PathPoint NowCheckPoint => CheckPoints.Count > 0 ? CheckPoints[Mathf.Clamp(currentCheckIndex, 0, CheckPoints.Count - 1)] : null;

        public PathPoint NextCheckPoint => CheckPoints.Count > 0
            ? CheckPoints[Mathf.Clamp(currentCheckIndex + 1, 0, CheckPoints.Count - 1)]
            : null;

        /// <summary>TempPath 中当前要去的下一个点。</summary>
        public Vector3 TempTarget
        {
            get
            {
                if (TempPath == null || TempPath.Count == 0)
                {
                    Debug.LogWarning($"[EnemyPathfinder] TempPath 为空，回退到单位当前位置。Owner={(Owner != null ? Owner.UnitData.Id : "null")}");
                    return Owner != null ? Owner.Position : Vector3.zero;
                }

                int targetIndex = Mathf.Clamp(TempIndex + 1, 0, TempPath.Count - 1);
                return TempPath[targetIndex];
            }
        }

        /// <summary>
        /// 出生时初始化路径点。
        /// </summary>
        public void Initialize(敌人 owner, WaveInfo waveData)
        {
            Owner = owner;
            currentPathIndex = 0;
            currentCheckIndex = 0;
            OnlyCheckPoint = false;
            NeedResetPath = false;
            PathWaiting = new CountDown();
            PathPoints.Clear();
            CheckPoints.Clear();
            tmpPathPointList.Clear();
            tmpPathPointLastList.Clear();
            TempPath = null;
            TempIndex = 0;

            if (waveData == null)
            {
                Debug.LogError("[EnemyPathfinder] Initialize: waveData 为空，无法初始化路径。");
                return;
            }

            PathInfo pathInfo = null;
            if (Owner.Battle.MapData.PathInfos != null)
                pathInfo = Owner.Battle.MapData.PathInfos.Find(x => x.Name == waveData.Path);
            if (pathInfo == null || pathInfo.Path == null || pathInfo.Path.Count == 0)
            {
                Debug.LogError($"[EnemyPathfinder] Initialize: 找不到波次路径 {waveData.Path}。");
                return;
            }

            PathPoints.AddRange(pathInfo.Path);

            // 首尾固定为 CheckPoint，保证至少有两个检查点，避免 NowCheckPoint 越界。
            PathPoints[0].CheckPoint = true;
            PathPoints[PathPoints.Count - 1].CheckPoint = true;
            PathPoints[0].IsArrive = true;

            CheckPoints = PathPoints.Where(x => x.CheckPoint).ToList();

            Owner.Position = PathPoints[0].Pos;
            Owner.Position = new Vector3(Owner.Position.x, Owner.Battle.Map.Tiles[Owner.GridPos.x, Owner.GridPos.y].Pos.y, Owner.Position.z);
            PathWaiting.Set(PathPoints[0].Delay);

            Owner.ScaleX = Owner.TargetScaleX = (NextPathPoint != null && (NextPathPoint.Pos.x - Owner.Position.x) > 0) ? 1 : -1;

            PathDebugger.LogPath(Owner.UnitData.Id, "初始化路径点", PathPoints.Select(x => x.Pos).ToList());
        }

        /// <summary>
        /// 获取指定下标的路径点。
        /// 优先返回尚未到达的临时路径点；其次在 OnlyCheckPoint 模式下返回 CheckPoints；
        /// 最后回退到 PathPoints。越界返回 null。
        /// </summary>
        public PathPoint GetPoint(int index)
        {
            // 有临时路径点时，临时路径点优先级最高。
            PathPoint tmpCheckPoint = tmpPathPointList.FirstOrDefault(x => !x.IsArrive);
            if (tmpCheckPoint != null)
                return tmpCheckPoint;

            if (OnlyCheckPoint && CheckPoints.Count > 0 && index >= 0 && index < CheckPoints.Count)
                return CheckPoints[index];

            if (index >= 0 && index < PathPoints.Count)
                return PathPoints[index];

            return null;
        }

        /// <summary>
        /// 更新技能插入的临时路径点计时，到期的点会失效并触发重新寻路。
        /// </summary>
        public void UpdateTempPoints(float deltaTime)
        {
            if (tmpPathPointList.Count == 0) return;

            toRemoveTmpPathPointList.Clear();
            toRemoveTmpPathPointLastList.Clear();

            for (int i = 0; i < tmpPathPointLastList.Count; i++)
            {
                CountDown tmpPathPointLast = tmpPathPointLastList[i];
                tmpPathPointLast.Update(deltaTime);

                if (tmpPathPointLast.Finished())
                {
                    toRemoveTmpPathPointLastList.Add(tmpPathPointLast);
                    toRemoveTmpPathPointList.Add(tmpPathPointList[i]);

                    tmpPathPointList[i].IsArrive = true;
                    tmpPathPointList[i].CheckPoint = false;
                    tmpPathPointList[i].IsTemp = false;

                    NeedResetPath = true;
                }
            }

            for (int i = 0; i < toRemoveTmpPathPointList.Count; i++)
            {
                tmpPathPointLastList.Remove(toRemoveTmpPathPointLastList[i]);
                tmpPathPointList.Remove(toRemoveTmpPathPointList[i]);
            }

            PathDebugger.LogPath(Owner.UnitData.Id, "临时路径点更新", tmpPathPointList.Select(x => x.Pos).ToList());
        }

        /// <summary>
        /// 到达检测：每帧调用，判断是否已经到达 TempPath 的下一目标点。
        /// </summary>
        public void CheckArrival()
        {
            if (TempPath == null || TempPath.Count == 0) return;

            Vector3 target = TempTarget;
            if ((target - Owner.Position).sqrMagnitude <= 敌人.TempArriveDistance)
            {
                TempIndex++;

                // 到达 TempPath 末尾：推进原始路径点
                if (TempIndex >= TempPath.Count - 1)
                {
                    OnArriveAtPathPoint();
                }
            }
        }

        /// <summary>
        /// 到达临时路径末尾后，推进 currentPathIndex / currentCheckIndex，并处理终点、等待、隐藏移动。
        /// </summary>
        private void OnArriveAtPathPoint()
        {
            PathPoint nowPoint = NowPathPoint;
            if (nowPoint == null)
            {
                Debug.LogWarning("[EnemyPathfinder] OnArriveAtPathPoint: NowPathPoint 为空，无法推进路径。");
                return;
            }

            PathDebugger.Log(Owner.UnitData.Id, $"到达路径点 {nowPoint.Pos}，IsTemp={nowPoint.IsTemp}，CheckPoint={nowPoint.CheckPoint}");

            if (!nowPoint.IsTemp)
            {
                currentPathIndex++;
                nowPoint.IsArrive = true;
            }

            if (nowPoint.CheckPoint && !nowPoint.IsTemp)
            {
                currentCheckIndex++;
                if (OnlyCheckPoint && currentCheckIndex < CheckPoints.Count)
                {
                    currentPathIndex = PathPoints.IndexOf(CheckPoints[currentCheckIndex]);
                    for (int i = 0; i <= currentPathIndex; i++)
                        PathPoints[i].IsArrive = true;
                }
                OnlyCheckPoint = false;
            }

            // 到达终点：破门伤害
            if (PathPoints.Count > 0 &&
                (PathPoints[PathPoints.Count - 1].Pos - Owner.Position).sqrMagnitude <= 敌人.TempArriveDistance)
            {
                Owner.Battle.DoDamage(Owner.UnitData.Damage);
                Owner.Battle.TriggerDatas.Push(new TriggerData { User = Owner });
                Owner.Trigger(TriggerEnum.到达路径终点);
                Owner.Battle.TriggerDatas.Pop();
                Owner.Finish(true);
                return;
            }

            // 等待/隐藏移动应使用“推进后的当前路径点”（即刚到达的下一个路径点）配置，
            // 否则会错误地使用到达前的旧路径点 Delay，导致等待被推迟到下一段路径。
            PathPoint waitPoint = NowPathPoint ?? nowPoint;
            PathWaiting.Set(waitPoint.Delay);
            if (waitPoint.HideMove)
            {
                PathDebugger.Log(Owner.UnitData.Id, "隐藏移动");
                if (PathWaiting.value > 0)
                    Owner.Visiable = false;
                else
                    Owner.FinishHide();
            }

            // 本段临时路径走完，置空等待下一轮重新寻路
            TempPath = null;
            TempIndex = 0;
        }

        /// <summary>
        /// 敌人完成隐藏移动后调用：推进当前点，并准备下一段路径。
        /// </summary>
        public void FinishHideMove()
        {
            PathPoint nextPoint = NextPathPoint;
            if (nextPoint != null)
            {
                nextPoint.IsArrive = true;
                Owner.Position = nextPoint.Pos;
                currentPathIndex++;
                if (NowPathPoint != null && NowPathPoint.CheckPoint && NowCheckPoint != null && !NowCheckPoint.IsTemp)
                    currentCheckIndex++;
            }

            Owner.Visiable = true;
            Owner.UnitModel?.gameObject.SetActive(true);
            FindNewPath(OnlyCheckPoint);
        }

        /// <summary>
        /// 重新计算“当前位置 -> 下一路径点”的临时路径。
        /// </summary>
        public void FindNewPath(bool onlyCheckPoint)
        {
            Vector3 start = Owner.Position;
            PathPoint nextPoint = NextPathPoint;
            Vector3 end = nextPoint != null ? nextPoint.Pos : start;

            PathDebugger.Log(Owner.UnitData.Id, $"开始寻路: start={start} end={end} onlyCheckPoint={onlyCheckPoint}");

            Vector3 offset = new Vector3(Owner.WaveData.OffsetX, 0, Owner.WaveData.OffetsetY);

            bool sameGrid = Mathf.RoundToInt(start.x) == Mathf.RoundToInt(end.x)
                            && Mathf.RoundToInt(start.z) == Mathf.RoundToInt(end.z);

            if (sameGrid || Owner.Height > 0)
            {
                // 同一格或飞行单位：直接连线即可，不需要 A*
                TempPath = new List<Vector3> { start - offset, end - offset };
            }
            else
            {
                // 地面单位：A* 寻路
                TempPath = AStarPathFinder.FindPath(Owner.Battle.Map.Tiles, new List<Vector3> { start - offset, end - offset }, false);
            }

            // 极端情况保护：A* 失败返回空路径时，退化为两点直线，避免 TempTarget 越界。
            if (TempPath == null || TempPath.Count == 0)
            {
                Debug.LogWarning($"[EnemyPathfinder] A* 返回空路径，退化为直线。start={start} end={end}");
                TempPath = new List<Vector3> { start - offset, end - offset };
            }

            // 把 offset 加回去，让临时路径回到世界坐标
            for (int i = 0; i < TempPath.Count; i++)
            {
                TempPath[i] += offset;
            }

            TempIndex = 0;
            NeedResetPath = false;

            PathDebugger.DrawPath(TempPath, Color.yellow, 1f);
            PathDebugger.LogPath(Owner.UnitData.Id, "临时路径", TempPath);
        }

        /// <summary>
        /// 尝试插入一个临时路径点（例如技能生成的地块）。
        /// </summary>
        public bool TryAddTempPoint(Vector3 pos, float time)
        {
            if (!IsCanArrive(Owner.Position, pos))
            {
                PathDebugger.Log(Owner.UnitData.Id, $"临时路径点不可达: {pos}");
                return false;
            }

            PathPoint exists = tmpPathPointList.FirstOrDefault(x => x.Pos == pos);
            if (exists != null)
            {
                int index = tmpPathPointList.IndexOf(exists);
                if (index >= 0 && index < tmpPathPointLastList.Count)
                {
                    tmpPathPointLastList[index].value += time;
                    PathWaiting.Finish();
                }
                PathDebugger.Log(Owner.UnitData.Id, $"更新已存在的临时路径点: {pos} +{time}s");
                return true;
            }

            PathPoint tmpPoint = new PathPoint
            {
                Pos = pos,
                Delay = time,
                HideMove = false,
                CheckPoint = true,
                IsArrive = false,
                IsTemp = true,
            };

            tmpPathPointList.Add(tmpPoint);
            tmpPathPointLastList.Add(new CountDown(time));

            OnlyCheckPoint = true;
            FindNewPath(OnlyCheckPoint);

            PathDebugger.Log(Owner.UnitData.Id, $"插入临时路径点: {pos} 持续 {time}s");
            PathDebugger.DrawPoint(pos, Color.cyan, 0.5f, 1f);

            return true;
        }

        /// <summary>
        /// 判断从 start 到 end 是否可到达。高度不同的格子判定为不可到达。
        /// </summary>
        public bool IsCanArrive(Vector3 start, Vector3 end)
        {
            Tile startTile = Owner.Battle.Map.Tiles[(int)start.x, (int)start.z];
            Tile endTile = Owner.Battle.Map.Tiles[(int)end.x, (int)end.z];

            if (startTile.FarAttackGrid != endTile.FarAttackGrid)
                return false;

            List<Vector3> path = AStarPathFinder.FindPath(Owner.Battle.Map.Tiles, new List<Vector3> { start, end }, false);
            return path.Count > 0;
        }

        /// <summary>
        /// 计算敌人到终点的剩余路程（用于距离排序等）。保留近似欧氏距离，避免高频调用 A*。
        /// </summary>
        public float DistanceToFinal(Vector3 position)
        {
            float totalDistance = 0f;
            int currentIndex = currentPathIndex;
            bool onlyCheckPoint = OnlyCheckPoint;
            List<Vector3> pathPointsToCalculate = new List<Vector3>();

            if (TempPath != null && TempPath.Count > 0)
            {
                int safeIndex = Mathf.Clamp(TempIndex, 0, TempPath.Count - 1);
                totalDistance += (position - TempPath[safeIndex]).magnitude;
                for (int i = safeIndex; i < TempPath.Count - 1; i++)
                    totalDistance += (TempPath[i] - TempPath[i + 1]).magnitude;
            }
            else if (tmpPathPointList.Count > 0)
            {
                Vector3 nextPos = NextPathPoint != null ? NextPathPoint.Pos : position;
                totalDistance += (tmpPathPointList[tmpPathPointList.Count - 1].Pos - nextPos).magnitude;
                for (int i = 1; i < tmpPathPointList.Count - 1; i++)
                    totalDistance += (tmpPathPointList[i].Pos - tmpPathPointList[i + 1].Pos).magnitude;
            }

            for (int i = currentIndex + 1; i < PathPoints.Count; i++)
            {
                pathPointsToCalculate.Add(PathPoints[i].Pos);
            }

            if (pathPointsToCalculate.Count > 0)
            {
                Vector3 lastPos = NextPathPoint != null ? NextPathPoint.Pos : position;
                if (tmpPathPointList.Count == 0)
                    lastPos = NowPathPoint != null ? NowPathPoint.Pos : position;

                foreach (Vector3 point in pathPointsToCalculate)
                {
                    totalDistance += (lastPos - point).magnitude;
                    lastPos = point;
                }
            }

            return totalDistance;
        }

        /// <summary>
        /// 在 Scene 视图绘制当前临时路径与临时路径点，便于调试。
        /// </summary>
        public void DrawDebug()
        {
            if (TempPath != null && TempPath.Count > 1)
            {
                PathDebugger.DrawPath(TempPath, Color.yellow, 0f);
            }

            foreach (PathPoint p in tmpPathPointList)
            {
                PathDebugger.DrawPoint(p.Pos, Color.cyan, 0.5f, 0f);
            }

            if (NowPathPoint != null)
                PathDebugger.DrawPoint(NowPathPoint.Pos, Color.green, 0.35f, 0f);
            if (NextPathPoint != null)
                PathDebugger.DrawPoint(NextPathPoint.Pos, Color.red, 0.35f, 0f);
        }
    }
}
