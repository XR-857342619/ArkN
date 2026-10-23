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
                // === 皮肤笔刷补丁：按存档中的 Skin 数据还原皮肤（只处理视觉，不触碰任何机制字段） ===
                // 皮肤笔刷默认关闭，见 FeatureFlags.SkinBrush
                if (FeatureFlags.SkinBrush && g.Skin != null && !string.IsNullOrEmpty(g.Skin.SkinName))
                {
                    var skinData = mapGrid.gameObject.AddComponent<GridSkinData>();
                    skinData.Data = g.Skin.Clone();
                    skinData.ReapplyFromData();
                    // 组合模式：还原叠加皮肤层
                    if (g.SkinExtras != null)
                        foreach (var ex in g.SkinExtras)
                            if (ex != null && !string.IsNullOrEmpty(ex.SkinName))
                                skinData.AddExtraFromData(ex);
                }
                mapGrids[i * infos.GetLength(1) + j] = mapGrid;
            }
        }
        init();
    }

    /// <summary>
    /// === 皮肤笔刷补丁 ===
    /// 对场景中已存在的格子按存档数据还原皮肤（场景地图进入战斗时调用，
    /// 因为场景地图不经过 Build 重建，皮肤数据来自 MapInfo.GridInfos）。
    /// 只处理视觉，不触碰任何机制字段；GridInfo.Skin 为空则不动该格。
    /// </summary>
    public void ApplySkins(GridInfo[,] infos)
    {
        if (!FeatureFlags.SkinBrush) return; // 皮肤笔刷默认关闭（FeatureFlags.SkinBrush），不进任何还原逻辑
        if (infos == null) return;
        int w = infos.GetLength(0), h = infos.GetLength(1);

        var grids = GetComponentsInChildren<MapGrid>(true);
        if (grids == null || grids.Length == 0)
            grids = FindObjectsOfType<MapGrid>();

        foreach (var grid in grids)
        {
            if (grid == null) continue;
            if (grid.X < 0 || grid.Y < 0 || grid.X >= w || grid.Y >= h) continue;
            var g = infos[grid.X, grid.Y];
            if (g == null || g.Skin == null || string.IsNullOrEmpty(g.Skin.SkinName)) continue;

            var data = grid.GetComponent<GridSkinData>();
            // === 组合模式：同步叠加层存档数据 ===
            var extras = g.SkinExtras != null
                ? g.SkinExtras.Where(x => x != null && !string.IsNullOrEmpty(x.SkinName)).Select(x => x.Clone()).ToList()
                : new List<SkinInfo>();
            if (data == null)
            {
                data = grid.gameObject.AddComponent<GridSkinData>();
                data.Data = g.Skin.Clone();
                data.ReapplyFromData();
                foreach (var ex in extras) data.AddExtraFromData(ex);
            }
            else
            {
                // 已有皮肤数据/实例：以存档为准更新（实例缺失则重建）
                data.Data = g.Skin.Clone();
                data.ExtraSkins = extras;
                // v3.5：已有叠加层实例数 > 存档数据层数时，裁掉尾部多余实例。
                // 否则残留的旧叠加层既是"多出来的子物体"，又让 ReapplyExtras 的层序按
                // 兄弟顺序错位（层序 = 兄弟顺序，多一层则后面全部偏一层）。
                var kids = data.GetSkinChildren();
                for (int i = kids.Count - 1; i >= 1 + extras.Count; i--)
                {
                    if (Application.isPlaying) Destroy(kids[i]);
                    else DestroyImmediate(kids[i]);
                }
                if (data.GetSkinChildren().Count == 0)
                {
                    data.ReapplyFromData();
                    data.ReapplyExtras();
                }
                else
                {
                    if (data.Data.TileHidden) data.HideTileRenderers();
                    data.ApplySkinTransform();
                    data.ReapplyExtras();
                }
            }
        }
    }
}
