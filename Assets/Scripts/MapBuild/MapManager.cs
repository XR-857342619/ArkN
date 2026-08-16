using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;
//using Pathfinding;
using System;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    public MapGrid[,] Grids;

    private MapGrid[] mapGrids;

    bool choose;
    bool brush;
    Action<MapGrid> Action;
    TaskCompletionSource<MapGrid> tcs;

    private void Awake()
    {
        Instance = this;
        init();
    }
    void init()
    {
        MapGrid[] grids = GetComponentsInChildren<MapGrid>();
        if (grids != null && grids.Length > 0)
        {
            Grids = new MapGrid[grids.Max(x => x.X) + 1, grids.Max(x => x.Y) + 1];

            foreach (var g in grids)
            {
                Grids[g.X, g.Y] = g;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if ((choose && Input.GetKeyDown(KeyCode.Mouse0)) || (brush && Input.GetKey(KeyCode.Mouse0) && !FairyGUI.Stage.isTouchOnUI))
        {
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit,100,1<<0))
            {
                var grid = hit.collider.GetComponentInParent<MapGrid>();
                if (grid != null)
                {
                    if (choose)
                        tcs.SetResult(grid);
                    if (brush)
                        Action(grid);
                }
            }
        }
    }

    public async Task<MapGrid> SelectGrid()
    {
        // 若上一次等待尚未结束，先取消旧任务，避免旧的等待者永远挂起
        if (tcs != null && !tcs.Task.IsCompleted)
        {
            tcs.TrySetCanceled();
            choose = false;
        }
        choose = true;
        tcs = new TaskCompletionSource<MapGrid>();
        var result = await tcs.Task;
        choose = false;
        return result;
    }

    public void Brush(Action<MapGrid> action)
    {
        brush = true;
        this.Action = action;
    }

    public void EndBrush()
    {
        brush = false;
        Action = null;
    }


    //StartEndModifier startEndModifier = new StartEndModifier()
    //{
    //    exactStartPoint = StartEndModifier.Exactness.ClosestOnNode,
    //    exactEndPoint = StartEndModifier.Exactness.ClosestOnNode,
    //};
    //RaycastModifier raycastModifier = new RaycastModifier()
    //{
    //    useGraphRaycasting = true,
    //    useRaycasting = false,
    //    //thickRaycastRadius = 0.25f
    //};
    LineRenderer Line;
    Pool<Transform> Pool = new Pool<Transform>();
    List<Transform> Sphere = new List<Transform>();
    public void ShowPath(List<PathPoint> points, bool fly = false)
    {
        //Debug.Log(Grids.Length);
        if (Line == null)
        {
            Line = ResHelper.Instantiate("Assets/Bundles/Other/Line").GetComponent<LineRenderer>();
            // 优化LineRenderer设置以更好地显示平滑曲线
            Line.numCornerVertices = 8;
            Line.numCapVertices = 8;
        }

        // 清空之前的球体标记
        foreach (var t in Sphere)
        {
            Pool.Despawn(t);
        }
        Sphere.Clear();

        if (points != null && points.Count > 0)
        {
            // 添加路径点标记
            foreach (var p in points)
            {
                var point = MapBuilderUI.UI_MapBuilder.Instance.m_PathPage.NowPoints;
                var t = Pool.Spawn(ResHelper.GetAsset<GameObject>("Assets/Bundles/Other/Sphere" + (point == p ? "1" : "")).transform,
                                  p.Pos + new Vector3(0, fly ? 0.55f : 0.15f, 0));
                Sphere.Add(t);
            }

            List<Vector3> rawPath = points.Select(x => x.Pos).ToList();
            List<Vector3> pathPoints = AStarPathFinder.FindPath(Grids, rawPath, fly);

            //if (!fly)
            //{
            //    //// 处理非飞行路径
            //    //for (int i = 0; i < points.Count - 1; i++)
            //    //{
            //    //    PathPoint point = points[i];
            //    //    if (points[i].HideMove)
            //    //    {
            //    //        pathPoints.Add(points[i].Pos);
            //    //        pathPoints.Add(points[i + 1].Pos);
            //    //    }
            //    //    //else
            //    //    //{
            //    //    //    //var p = ABPath.Construct(points[i].Pos, points[i + 1].Pos);
            //    //    //    //AstarPath.StartPath(p);
            //    //    //    //p.BlockUntilCalculated();

            //    //    //    //startEndModifier.Apply(p);

            //    //    //    //if (p.vectorPath.Count > 0 && (points[i].DirectMove || points[i].HideMove))
            //    //    //    //    raycastModifier.Apply(p);

            //    //    //    //pathPoints.AddRange(p.vectorPath);
            //    //    //    List<Vector3> p = new List<Vector3>();
            //    //    //    p.AddRange(AStarPathFinder.FindPath(Grids, point.Pos, points[i + 1].Pos));
            //    //    //    //if (p.Count > 2)
            //    //    //    pathPoints.AddRange(p);
            //    //    //}
            //    //}
            //}
            //else
            //{
            //    // 处理飞行路径
            //    pathPoints.AddRange(points.Select(x => x.Pos));
            //}

            // 应用贝塞尔曲线平滑处理
            //List<Vector3> smoothedPoints = ApplyBezierSmoothing(pathPoints);

            // 调整Y轴高度
            for (int i = 0; i < pathPoints.Count; i++)
            {
                pathPoints[i] += new Vector3(0, fly ? 0.5f : 0.1f, 0);
            }

            // 设置LineRenderer的顶点
            Line.positionCount = pathPoints.Count;
            Line.SetPositions(pathPoints.ToArray());
        }
        else
        {
            Line.positionCount = 0;
        }
    }

    ///// <summary>
    ///// 对路径点应用贝塞尔曲线平滑处理
    ///// </summary>
    //private List<Vector3> ApplyBezierSmoothing(List<Vector3> originalPoints)
    //{
    //    if (originalPoints.Count < 3)
    //    {
    //        // 少于3个点，无需平滑处理
    //        return new List<Vector3>(originalPoints);
    //    }

    //    List<Vector3> smoothedPoints = new List<Vector3>();

    //    // 添加第一个点
    //    smoothedPoints.Add(originalPoints[0]);

    //    // 处理中间的每个拐角
    //    for (int i = 1; i < originalPoints.Count - 1; i++)
    //    {
    //        Vector3 prevPoint = originalPoints[i - 1];
    //        Vector3 currentPoint = originalPoints[i];
    //        Vector3 nextPoint = originalPoints[i + 1];

    //        // 计算当前点与前后点的方向
    //        Vector3 dirToCurrent = (currentPoint - prevPoint).normalized;
    //        Vector3 dirFromCurrent = (nextPoint - currentPoint).normalized;

    //        // 计算拐角处的起点和终点偏移
    //        Vector3 startTangent = currentPoint - dirToCurrent * cornerSmoothDistance;
    //        Vector3 endTangent = currentPoint + dirFromCurrent * cornerSmoothDistance;

    //        // 使用贝塞尔曲线生成过渡点
    //        for (int j = 1; j <= segmentsPerCorner; j++)
    //        {
    //            float t = j / (float)segmentsPerCorner;
    //            Vector3 bezierPoint = CalculateQuadraticBezier(startTangent, currentPoint, endTangent, t);
    //            smoothedPoints.Add(bezierPoint);
    //        }
    //    }

    //    // 添加最后一个点
    //    smoothedPoints.Add(originalPoints[originalPoints.Count - 1]);

    //    return smoothedPoints;
    //}

    ///// <summary>
    ///// 计算二次贝塞尔曲线上的点
    ///// </summary>
    //private Vector3 CalculateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    //{
    //    float u = 1 - t;
    //    return u * u * start + 2 * u * t * control + t * t * end;
    //}

    public void AutoBuild()
    {
        var mapRoot = GameObject.Find("S_playground");
        for (int i = 0; i < mapRoot.transform.childCount; i++)
        {
            var t = mapRoot.transform.GetChild(i);
            if (t == mapRoot) continue;
            var gr = t.GetComponent<MapGrid>();
            if (gr == null) gr = t.gameObject.AddComponent<MapGrid>();
            gr.X = (int)(t.transform.position.x);
            gr.Y = (int)(t.transform.position.z);
            gr.FarAttackGrid = gr.transform.localPosition.y != 0;
            gr.CanMove = !gr.FarAttackGrid;
            //Transform t1 = null;
            //if (t.childCount == 0)
            //{
            //    var g = new GameObject("tile" + i);
            //    g.transform.parent = transform;
            //    t.transform.parent = g.transform;
            //    var gr = t.GetComponent<MapGrid>();
            //    if (gr != null) DestroyImmediate(gr);
            //    t1 = t;
            //    t = g.transform;
            //    t.transform.position = t1.transform.position+new Vector3(0,0.5f,0);
            //    t1.transform.localPosition = new Vector3(0, -0.5f, 0);
            //}
            //else
            //{
            //    t1 = t.GetChild(0).transform;
            //}
            //var grid = t.GetComponent<MapGrid>();
            //if (grid == null) grid = t.gameObject.AddComponent<MapGrid>();
            //grid.AutoBuild();
            //grid.X = Mathf.RoundToInt( t.transform.position.x);
            //grid.Y = Mathf.RoundToInt(t.transform.position.z);
            //t.name = "Grid:" + grid.X + "," + grid.Y + "," + grid.MapUnitId;
            //t.position = new Vector3(grid.X, t.transform.position.y, grid.Y);
            //var texName = t1.GetComponent<Renderer>().sharedMaterial.mainTexture.name;
            ////根据贴图名自动匹配
            //switch (texName)
            //{
            //    case "caution_000":
            //        grid.CanBuildUnit = false;
            //        break;
            //    case "stone_000":
            //        //grid.
            //        break;
            //    case "stone_002":
            //        grid.FarAttackGrid = true;
            //        break;
            //    default:
            //        Debug.Log($"未自动设置的贴图名:{texName}");
            //        break;
            //}
        }
        Debug.Log("自动设置地图信息完成");
    }

    public void Build(GridInfo[,] infos)
    {
        //Camera.main.transform.position = new Vector3((infos.GetLength(0) - 1) / 2f, 0.6f * infos.GetLength(0), -3.5f + (infos.GetLength(1) - 1) / 3f);
        // 清理旧地块：优先按已登记列表销毁，否则遍历子物体兜底
        if (mapGrids != null)
        {
            for (int i = mapGrids.Length - 1; i >= 0; i--)
            {
                if (mapGrids[i] != null)
                    DestroyImmediate(mapGrids[i].gameObject);
            }
        }
        else
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.GetComponent<MapGrid>() != null)
                    DestroyImmediate(child.gameObject);
            }
        }

        mapGrids = new MapGrid[infos.GetLength(0) * infos.GetLength(1)];
        for (int i = 0; i < infos.GetLength(0); i++)
        {
            for (int j = 0; j < infos.GetLength(1); j++)
            {
                var g = infos[i, j];
                if (g == null) continue;
                var mapGrid = new GameObject(i + "," + j).AddComponent<MapGrid>();
                mapGrid.X = i;
                mapGrid.Y = j;
                mapGrid.CanBuildUnit = g.CanBuildUnit;
                mapGrid.CanMove = g.CanMove;
                mapGrid.FarAttackGrid = g.FarAttack;
                mapGrid.transform.parent = transform;
                mapGrid.AutoBuild();
                mapGrids[i * infos.GetLength(1) + j] = mapGrid;
            }
        }
        init();
    }
}
