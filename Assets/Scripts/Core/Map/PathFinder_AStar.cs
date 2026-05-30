using System.Collections.Generic;
//using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>
/// 寻路节点数据
/// </summary>
public class PathNode
{
    public int X, Y;
    public float G; // 移动代价
    public float H; // 启发代价
    public float F => G + H; // 总代价
    public PathNode Parent; // 父节点

    public PathNode(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// 独立寻路工具类
/// </summary>
public static class AStarPathFinder
{
    // 角色半径配置
    private const float CharacterRadius = 0.25f;
    // 最大搜索步数，防止死循环
    private const int MaxSearchSteps = 10000;

    public const float cornerSmoothDistance = 0.5f; // 拐角过渡距离
    public const int segmentsPerCorner = 3; // 每个拐角的过渡段数

    /// <summary>
    /// 核心寻路方法
    /// </summary>
    /// <param name="map">地图数据源 (Tile[,])</param>
    /// <param name="start">起始世界坐标</param>
    /// <param name="end">目标世界坐标</param>
    /// <returns>平滑后的世界坐标路径列表</returns>
    public static List<Vector3> FindPath<T>(T[,] map, List<Vector3> pathPoints, bool isFly) where T : ITileData
    {
        List<Vector3> finalPath = new List<Vector3>();
        List<Vector3> rawPath = new List<Vector3>();
        //List<Vector3> tempPath = new List<Vector3>();
        List<Vector3> smoothPath = new List<Vector3>();

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector3 start = pathPoints[i];
            Vector3 end = pathPoints[i + 1];
            // 1. 坐标转换：世界坐标 -> 地图索引
            int startX = Mathf.RoundToInt(start.x);
            int startY = Mathf.RoundToInt(start.z);
            int endX = Mathf.RoundToInt(end.x);
            int endY = Mathf.RoundToInt(end.z);

            // 边界检查
            if (!IsWithinBounds(map, startX, startY) || !IsWithinBounds(map, endX, endY))
            {
                Debug.LogWarning("寻路失败：起点或终点越界");
                //return finalPath;
                continue;
            }

            // 检查起终点是否可通行
            if (!map[startX, startY].Passable || !map[endX, endY].Passable)
            {
                Debug.LogWarning("寻路失败：起点或终点是障碍物");
                //return finalPath;
                continue;
            }

            // 2. 执行 A* 核心搜索
            List<PathNode> pathNodes = AStarSearch(map, startX, startY, endX, endY, isFly);

            if (pathNodes == null || pathNodes.Count == 0)
                //return finalPath;
                continue;

            // 3. 转换为世界坐标 (原始折线路径)
            rawPath.Clear();
            foreach (var node in pathNodes)
            {
                Vector3 piont = new Vector3(node.X, 0, node.Y);
                // 将索引转回世界坐标 (假设格子中心即整数坐标)
                //if (!rawPath.Contains(piont))
                rawPath.Add(new Vector3(node.X, 0, node.Y));
            }

            if (rawPath.Count == 0) continue;

            smoothPath.Clear();
            //4.路径平滑(视线检测)
            SmoothPath(rawPath, smoothPath, isFly);

            //if (rawPath.Count != 0) smoothPath.RemoveAt(0);
            
            finalPath.AddRange(smoothPath);
        }

        //finalPath.AddRange(rawPath);
        //SmoothPath(rawPath, finalPath);

        if (finalPath.Count == 0) return finalPath;

        finalPath = ApplyBezierSmoothing(finalPath);
        //finalPath = ReshapeAndSmooth(finalPath);
        //finalPath = SmoothPathCatmullRom(finalPath);

        // 5. 修正首尾点 (确保精确起止)
        if (finalPath.Count > 0)
        {
            finalPath[0] = pathPoints[0];
            if (finalPath.Count > 1)
                finalPath[finalPath.Count - 1] = pathPoints[^1];
        }
        else
        {
            // 如果平滑后为空（例如起终点很近），直接连线
            finalPath.Add(pathPoints[0]);
            finalPath.Add(pathPoints[^1]);
        }

        //for (int i = 0; i < finalPath.Count - 1; i++)
        //{
        //    Debug.DrawLine(finalPath[i] + Vector3.up, finalPath[i + 1] + Vector3.up, Color.red, 5f);
        //}

        return finalPath;
    }

    /// <summary>
    /// A* 算法核心实现
    /// </summary>
    private static List<PathNode> AStarSearch<T>(T[,] map, int startX, int startY, int endX, int endY, bool isFly) where T : ITileData
    {
        List<PathNode> openList = new List<PathNode>();
        HashSet<string> closedSet = new HashSet<string>();

        PathNode startNode = new PathNode(startX, startY);
        PathNode endNode = new PathNode(endX, endY);

        startNode.G = 0;
        // 使用欧几里得距离作为启发函数
        startNode.H = Vector2.Distance(new Vector2(startX, startY), new Vector2(endX, endY));

        openList.Add(startNode);
        int searchSteps = 0;

        while (openList.Count > 0 && searchSteps < MaxSearchSteps)
        {
            searchSteps++;

            // 寻找 F 值最小的节点
            PathNode currentNode = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].F < currentNode.F)
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);

            closedSet.Add(GetNodeKey(currentNode.X, currentNode.Y));

            // 到达终点
            if (currentNode.X == endNode.X && currentNode.Y == endNode.Y)
            {
                return RetracePath(startNode, currentNode);
            }

            // 遍历 4 方向邻居
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    if (x != 0 && y != 0) continue;
                    //if ((x != 0 || y != 0) && !isFly)
                    //{
                    //    // 检查相邻的两个正交格子
                    //    // 如果两个有一个是障碍物，则禁止斜向移动
                    //    if ((!IsWithinBounds(map, currentNode.X + x, currentNode.Y) || !map[currentNode.X + x, currentNode.Y].Passable) &&
                    //        (!IsWithinBounds(map, currentNode.X, currentNode.Y + y) || !map[currentNode.X, currentNode.Y + y].Passable))
                    //    {
                    //        continue; // 两个方向都堵死了，不能斜着走
                    //    }
                    //}

                    int checkX = currentNode.X + x;
                    int checkY = currentNode.Y + y;

                    if (!IsWithinBounds(map, checkX, checkY)) continue;

                    string nodeKey = GetNodeKey(checkX, checkY);
                    if (closedSet.Contains(nodeKey)) continue;

                    T checkTile = map[checkX, checkY];
                    if ((!checkTile.Passable) && !isFly) continue;

                    // 计算代价：基础移动代价 + 地块额外代价
                    float baseCost = (x != 0 && y != 0) ? 1.414f : 1.0f;
                    float totalCost = baseCost + checkTile.PassCost;

                    float tentativeG = currentNode.G + totalCost;

                    PathNode neighborNode = openList.Find(n => n.X == checkX && n.Y == checkY);
                    bool isNewNode = (neighborNode == null);

                    if (isNewNode)
                    {
                        neighborNode = new PathNode(checkX, checkY);
                        neighborNode.H = Vector2.Distance(new Vector2(checkX, checkY), new Vector2(endX, endY));
                        //Log.Debug(neighborNode.X + "," + neighborNode.Y);
                        openList.Add(neighborNode);
                    }
                    else if (tentativeG >= neighborNode.G)
                    {
                        continue;
                    }

                    neighborNode.Parent = currentNode;
                    neighborNode.G = tentativeG;
                }
            }
        }

        return null; // 未找到路径
    }

    /// <summary>
    /// 路径回溯
    /// </summary>
    private static List<PathNode> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<PathNode> path = new List<PathNode>();
        PathNode current = endNode;

        while (current != startNode)
        {
            //Debug.Log(current.X + "," + current.Y);
            path.Add(current);
            current = current.Parent;
        }
        path.Add(startNode);
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 路径平滑 (使用胶囊体检测)
    /// </summary>
    private static void SmoothPath(List<Vector3> rawPath, List<Vector3> smoothPath, bool isFly)
    {
        if (rawPath.Count == 0) return;

        for (int i = 0; i < rawPath.Count - 1; i++)
        {
            Debug.DrawLine(rawPath[i] + Vector3.up, rawPath[i + 1] + Vector3.up, Color.red, 5f);
        }

        if (!smoothPath.Contains(rawPath[0]))
            smoothPath.Add(rawPath[0]);
        int lastIndex = 0;

        for (int i = 1; i < rawPath.Count; i++)
        {
            var node = rawPath[i];
            RaycastHit? hit = null;

            if (!isFly) hit = HasClearPath(rawPath[lastIndex], rawPath[i]);

            // 尝试从 lastIndex 直接连接到 i
            if (hit.HasValue && !isFly)
            {
                //// 1. 确认平滑逻辑生效：这里会打印出阻挡平滑路径的物体名称
                //Debug.Log($"路径平滑生效！在 {rawPath[lastIndex]} -> {rawPath[i]} 处被阻挡。");
                //Debug.Log($"阻挡物体: <color=red>{hit.Value.collider.name}</color> (Layer: {LayerMask.LayerToName(hit.Value.collider.gameObject.layer)})");

                // 2. 可视化：在 Scene 视图中画出射线，确认阻挡位置
                Debug.DrawRay(hit.Value.point, hit.Value.normal * 0.5f, Color.red, 5f);

                // 无法直连，保留拐点
                //if (lastIndex != i - 1 && !smoothPath.Contains(rawPath[i - 1]))
                if (lastIndex != i - 1)
                {
                    smoothPath.Add(rawPath[i - 1]);
                }
                lastIndex = i - 1;
            }
        }

        if (smoothPath[smoothPath.Count - 1] != rawPath[rawPath.Count - 1])
        {
            smoothPath.Add(rawPath[rawPath.Count - 1]);
        }
    }

    // Catmull-Rom 平滑算法
    private static List<Vector3> SmoothPathCatmullRom(List<Vector3> points)
    {
        List<Vector3> smoothedPath = new List<Vector3>();
        int count = points.Count;

        // 至少需要2个点
        if (count < 2) return points;

        // 如果只有2个点，直接连线
        if (count == 2)
        {
            smoothedPath.Add(points[0]);
            smoothedPath.Add(points[1]);
            return smoothedPath;
        }

        // Catmull-Rom 需要计算每一段之间的插值
        // 公式: P(t) = 0.5 * ( (2*P1) + (-P0 + P2) * t + (2*P0 - 5*P1 + 4*P2 - P3) * t^2 + (-P0 + 3*P1 - 3*P2 + P3) * t^3 )
        // 其中 P0, P1, P2, P3 是连续的4个控制点

        // 遍历点，从第1个到倒数第2个（作为 P1）
        for (int i = 0; i < count - 1; i++)
        {
            // 获取4个控制点
            Vector3 p0, p1, p2, p3;

            if (i == 0)
            {
                // 在起点，P0 不存在，我们让它等于 P1
                p0 = points[0];
                p1 = points[0];
                p2 = points[1];
                p3 = (count > 2) ? points[2] : points[1];
            }
            else if (i == count - 2)
            {
                // 在终点，P3 不存在，我们让它等于 P2
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                p3 = points[i + 1];
            }
            else
            {
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                p3 = points[i + 2];
            }

            // 添加起始点 (P1)
            if (i == 0) smoothedPath.Add(p1);

            // 在 P1 和 P2 之间插值
            int segments = 10; // 每一段之间的插值数量
            for (int j = 1; j <= segments; j++)
            {
                float t = (float)j / segments;
                float t2 = t * t;
                float t3 = t2 * t;

                Vector3 point = 0.5f * (
                    (2 * p1) +
                    (-p0 + p2) * t +
                    (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                    (-p0 + 3 * p1 - 3 * p2 + p3) * t3
                );

                smoothedPath.Add(point);
            }
        }

        return smoothedPath;
    }

    /// <summary>
    /// 视线检测 (物理射线)
    /// </summary>
    private static RaycastHit? HasClearPath(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance < 0.1f) return null;

        // 胶囊体检测，确保角色不会擦着墙走
        // 注意：这里假设障碍物在 Layer "Default" 或 "Wall"
        // 建议添加 LayerMask 参数以提高性能
        if (Physics.CapsuleCast(
            start + new Vector3(0, 0.5f, 0),
            start + Vector3.up, // 垂直方向的胶囊体
            CharacterRadius,
            direction.normalized,
            out RaycastHit hit,
            distance
        ))
        {
            return hit;
        }
        return null;
    }

    /// <summary>
    /// 对路径点应用贝塞尔曲线平滑处理
    /// </summary>
    private static List<Vector3> ApplyBezierSmoothing(List<Vector3> originalPoints)
    {
        if (originalPoints.Count < 3)
        {
            // 少于3个点，无需平滑处理
            return new List<Vector3>(originalPoints);
        }

        List<Vector3> smoothedPoints = new List<Vector3>();

        // 添加第一个点
        smoothedPoints.Add(originalPoints[0]);

        // 处理中间的每个拐角
        for (int i = 1; i < originalPoints.Count - 1; i++)
        {
            Vector3 prevPoint = originalPoints[i - 1];
            Vector3 currentPoint = originalPoints[i];
            Vector3 nextPoint = originalPoints[i + 1];

            // 计算当前点与前后点的方向
            Vector3 dirToCurrent = (currentPoint - prevPoint).normalized;
            Vector3 dirFromCurrent = (nextPoint - currentPoint).normalized;

            // 计算拐角处的起点和终点偏移
            Vector3 startTangent = currentPoint - dirToCurrent * cornerSmoothDistance;
            Vector3 endTangent = currentPoint + dirFromCurrent * cornerSmoothDistance;

            // 使用贝塞尔曲线生成过渡点
            for (int j = 1; j <= segmentsPerCorner; j++)
            {
                float t = j / (float)segmentsPerCorner;
                Vector3 bezierPoint = CalculateQuadraticBezier(startTangent, currentPoint, endTangent, t);
                smoothedPoints.Add(bezierPoint);
            }
        }

        // 添加最后一个点
        smoothedPoints.Add(originalPoints[originalPoints.Count - 1]);

        return smoothedPoints;
    }

    /// <summary>
    /// 计算二次贝塞尔曲线上的点
    /// </summary>
    private static Vector3 CalculateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float u = 1 - t;
        return u * u * start + 2 * u * t * control + t * t * end;
    }

    #region Helper Methods
    private static bool IsWithinBounds<T>(T[,] map, int x, int y) where T : ITileData 
    {
        return x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1);
    }

    private static string GetNodeKey(int x, int y)
    {
        return $"{x},{y}";
    }
    #endregion
    public class DebugTool
    {
        Pool<MapTile> TilePool = new Pool<MapTile>();
        HashSet<MapTile> Tiles = new HashSet<MapTile>();

        public void ShowPath(List<Vector3> path)
        {
            foreach (var go in Tiles)
            {
                TilePool.Despawn(go);
            }
            Tiles.Clear();
            foreach (var tile in path)
            {
                var tileAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "HighLight").GetComponent<MapTile>();
                var go = TilePool.Spawn(tileAsset, tile, null);
                Tiles.Add(go);
            }
        }
    }
}