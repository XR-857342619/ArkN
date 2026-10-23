using System;
using System.Collections.Generic;
using System.Linq;
using FairyGUI;
using MapBuilderUI;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 皮肤笔刷·运行时版：把换皮工具完整整合进游戏内造图界面（MapBuilderUI）。
///
/// - 面板 = 纯代码构建的 FairyGUI 组件（不改 MapBuilderUI_fui.bytes），挂在造图界面根组件下；
/// - 按 T 键显隐（输入框打字时不触发）；面板整体可拖动；右下角拖动缩放（0.7~2.0）；
/// - 六种模式（与编辑器笔刷同语义）：
///   替换=换主皮肤；组合=叠加一层；特殊替换=点击场景物体后用笔刷替换；
///   调整=点击格子选中，选层后用预设数值微调；删除=删最上层（无叠加层则清格）；初始化=清整格；
/// - 笔刷列表 = 滚动列表（滚轮/拖拽浏览，同编辑器），来源 = Addressables 按目录前缀枚举 prefab；
/// - 笔刷预设：偏移/旋转/缩放 3×3 输入 + 清零 + 从选中层读取；
/// - 运行时撤销栈：替换/组合/删除/初始化/微调/清全图均可撤销（面板"撤销"按钮，最多 50 步）；
/// - 全局：同步皮肤数据到 MapInfo、清除全图皮肤（可撤销）。
/// - 编辑器独占功能不在运行时复刻：修复存档皮肤数据（需 AssetDatabase/GUID）、Scene 手柄微调。
///
/// 安装：UI_MapBuilder.Init() 末尾调用 SkinBrushRuntime.Install(this)。
/// </summary>
public class SkinBrushRuntime : MonoBehaviour
{
    public static SkinBrushRuntime Instance;

    /// <summary>默认笔刷目录（与编辑器笔刷 EditorPrefs 默认值一致）</summary>
    const string DefaultFolder = "Assets/Bundles/DungeonTile";
    const float PanelW = 440f, PanelH = 700f;
    const float ItemH = 26f;

    // ---- 面板引用 ----
    GComponent view, panel, listComp, listContent;
    GTextField statusText, templateLabel, targetLabel, layerLabel;
    readonly GComponent[] modeBtns = new GComponent[6];
    GComponent hideBtn, stripBtn;
    readonly List<GComponent> listItems = new List<GComponent>();
    GTextInput inFolder;
    GTextInput inOffX, inOffY, inOffZ, inRotX, inRotY, inRotZ, inSclX, inSclY, inSclZ;

    static readonly string[] ModeNames = { "替换", "组合", "特殊替换", "调整", "删除", "初始化" };

    // ---- 笔刷状态 ----
    int mode = -1;              // -1=停用 0=替换 1=组合 2=特殊替换 3=调整 4=删除 5=初始化
    bool hideOriginal = true;
    bool stripColliders = true;
    bool pickSceneTemplate;     // 下一次场景点击 = 取场景模板笔刷
    GameObject sceneTemplate;   // 场景模板笔刷（无资源地址，战斗内不可还原）
    GameObject replaceTarget;   // 特殊替换模式选中的目标
    MapGrid selectedGrid;       // 调整模式选中的格子
    int selectedLayer;
    readonly List<string> prefabAddrs = new List<string>();
    string folder = DefaultFolder;
    int brushIndex;
    readonly HashSet<MapGrid> strokeGrids = new HashSet<MapGrid>();

    // ---- 列表滚动 / 缩放 ----
    float listScroll;           // 笔刷列表滚动像素
    bool listDragging;
    float listLastY;
    bool resizing;
    Vector2 resizeStart;
    float resizeScaleStart;

    // ---- 滚动条 ----
    GComponent sbThumb;         // 滑条手柄（轨道固定 8×190，手柄 8×40）
    bool sbDragging;
    float sbDragStartY;
    float sbDragStartThumbY;
    const float SBTrackH = 190f;
    const float SBThumbH = 40f;

    // ---- 预览窗口（窗口1）----
    GComponent previewWin;
    GTextField previewTitleText;
    GLoader previewLoader;
    RenderTexture previewRT;
    GameObject previewRig;      // 预览专用摄像机 + 平行光
    GameObject previewInstance; // 当前预览的实例（放在远处独立 layer，不干扰地图）
    const float PreviewPos = 5000f;
    const int PreviewLayer = 21;

    // ---- 撤销栈 ----
    class GridSnapshot
    {
        public MapGrid grid;
        public bool hadComponent;
        public SkinInfo data;
        public List<SkinInfo> extras;
    }
    readonly List<List<GridSnapshot>> undoStack = new List<List<GridSnapshot>>();
    List<GridSnapshot> strokeBatch; // 当前笔画批次：一次按住拖动期间的所有快照都并入这里，松开鼠标才提交 → 一次拖动 = 一步撤销

    // ================= 安装 =================

    public static void Install(GComponent mapView)
    {
        if (!FeatureFlags.SkinBrush) return; // 皮肤笔刷默认关闭（FeatureFlags.SkinBrush）
        if (Instance != null) { Instance.view = mapView; return; }
        var go = new GameObject("SkinBrushRuntime");
        DontDestroyOnLoad(go); // 造图界面视图被 UIManager 缓存复用，运行器跨界面存活
        Instance = go.AddComponent<SkinBrushRuntime>();
        Instance.view = mapView;
        Instance.BuildPanel();
        Instance.RefreshPrefabs();
    }

    // ================= 主循环 =================

    void Update()
    {
        if (panel == null || view == null) return;

        // T 键显隐（输入框打字时不触发；本版 FairyGUI 的输入显示对象是 InputTextField）
        if (Input.GetKeyDown(KeyCode.T) && !(Stage.inst.focus is InputTextField))
            panel.visible = !panel.visible;

        // 笔画批次生命周期放在所有守卫之前：鼠标在 UI 上松开时也能正常收批，防止批次悬挂到下一次点击
        if (Input.GetMouseButtonDown(0)) strokeBatch = new List<GridSnapshot>();
        if (Input.GetMouseButtonUp(0)) EndStrokeBatch();

        // 面板收起 / 界面不可见 / 未选模式 / 场景未加载：不拦截鼠标
        if (!panel.visible || !view.visible || mode < 0) return;
        if (MapManager.Instance == null) return;
        if (Stage.isTouchOnUI) return;

        if (Input.GetMouseButtonDown(0)) strokeGrids.Clear();

        // 替换/组合支持按住拖动刷（同编辑器笔画去重）；其余模式只响应单击
        bool held = (mode == 0 || mode == 1) && Input.GetMouseButton(0);
        bool down = Input.GetMouseButtonDown(0);
        if (held || down) PickAndApply();
    }

    void PickAndApply()
    {
        var cam = Camera.main;
        if (cam == null) return;
        RaycastHit hit;
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 100f, 1 << 0)) return;
        var hitGo = hit.collider != null ? hit.collider.gameObject : null;
        if (hitGo == null) return;

        // 取场景模板笔刷：下一次场景点击优先级最高
        if (pickSceneTemplate)
        {
            pickSceneTemplate = false;
            sceneTemplate = hitGo;
            if (templateLabel != null) templateLabel.text = hitGo.name;
            SetStatus($"已取场景物体为笔刷模板：{hitGo.name}\n（模板无资源地址，战斗内按存档重建时无法还原）");
            return;
        }

        var grid = hitGo.GetComponentInParent<MapGrid>();
        try
        {
            switch (mode)
            {
                case 0: // 替换（支持拖动刷）
                case 1: // 组合（支持拖动刷）
                    if (grid == null) return;
                    if (strokeGrids.Contains(grid)) return;
                    strokeGrids.Add(grid);
                    if (mode == 0) DoReplace(grid); else DoCombine(grid);
                    break;
                case 2: // 特殊替换：选中场景物体（格子本体受保护，同编辑器）
                    if (grid != null) { SetStatus("格子本体/白膜不可特殊替换\n（它们是机制载体）；请点场景中的装饰物等自由物体"); return; }
                    replaceTarget = hitGo;
                    if (targetLabel != null) targetLabel.text = hitGo.name;
                    SetStatus($"已选中目标：{hitGo.name}\n点「替换为笔刷」执行；点「取为笔刷」把它设为模板");
                    break;
                case 3: // 调整：选中格子
                    if (grid == null) return;
                    selectedGrid = grid;
                    selectedLayer = 0;
                    UpdateLayerLabel();
                    SetStatus($"已选中格子 ({grid.X},{grid.Y})，共 {(grid.GetComponent<GridSkinData>() != null ? grid.GetComponent<GridSkinData>().SkinLayerCount : 0)} 层皮肤");
                    break;
                case 4:
                    if (grid != null) DoDelete(grid);
                    break;
                case 5:
                    if (grid != null) DoClear(grid, $"已初始化 ({grid.X},{grid.Y})（清空全部皮肤）");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SkinBrushRT] {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            SetStatus("出错：" + e.Message);
        }
    }

    // ================= 模式实现 =================

    void DoReplace(MapGrid grid)
    {
        var info = BuildCurrentBrushInfo();
        if (info == null) { SetStatus("笔刷未就绪：请先在列表选择笔刷 prefab"); return; }

        var data = grid.GetComponent<GridSkinData>();
        if (data == null) data = grid.gameObject.AddComponent<GridSkinData>();

        // 同款同显隐：拖动时跳过（同编辑器 HasSameSkin）——放在 PushUndo 前，避免压入无效撤销步
        if (data.Data != null && data.Data.SkinAddress == info.SkinAddress
            && data.Data.TileHidden == hideOriginal && data.GetSkinChild() != null) return;

        PushUndo(Capture(grid));

        // 清全部旧皮肤层（运行时 Destroy 延迟生效 → 先摘出层级树，防本帧被 GetSkinChildren 计入）
        foreach (var c in data.GetSkinChildren()) DetachAndDestroy(c);
        data.ExtraSkins.Clear();

        info.TileHidden = hideOriginal;
        data.Data = info;

        // 手动挂载主皮肤（不借道 AttachSkin，以支持"剥离碰撞体"开关）
        var instance = InstantiateCurrentBrush();
        if (instance != null)
        {
            instance.name = GridSkinData.SkinChildName;
            instance.transform.SetParent(grid.transform, false);
            instance.transform.SetSiblingIndex(Mathf.Min(1, grid.transform.childCount - 1));
            if (stripColliders) StripColliders(instance);
        }
        if (hideOriginal) data.HideTileRenderers(); else data.ShowTileRenderers();
        data.ApplySkinTransform();

        SetStatus($"已换皮 ({grid.X},{grid.Y})：{NameOf(info.SkinAddress)}");
    }

    void DoCombine(MapGrid grid)
    {
        var info = BuildCurrentBrushInfo();
        if (info == null) { SetStatus("笔刷未就绪：请先在列表选择笔刷 prefab"); return; }

        var data = grid.GetComponent<GridSkinData>();
        // 无主皮肤退化为普通换皮（首层受"隐藏白膜"控制，同编辑器 CombinePaint）
        if (data == null || data.GetSkinChild() == null) { DoReplace(grid); return; }

        // 该格已有同款层：跳过（同编辑器 HasAnySkinLayer）
        if (data.Data != null && data.Data.SkinAddress == info.SkinAddress) return;
        foreach (var ex in data.ExtraSkins)
            if (ex != null && ex.SkinAddress == info.SkinAddress) return;

        PushUndo(Capture(grid));

        int layer = data.ExtraSkins.Count;
        info.TileHidden = false; // 叠加层不动白膜显隐
        data.ExtraSkins.Add(info);

        var instance = InstantiateCurrentBrush();
        if (instance != null)
        {
            if (stripColliders) StripColliders(instance);
            data.ConfigureExtra(instance, layer + 1); // 命名/置顶/sortingOrder
            data.ApplySkinTransformAt(layer + 1);
        }
        SetStatus($"已叠加 ({grid.X},{grid.Y}) 第 {data.ExtraSkins.Count} 层：{NameOf(info.SkinAddress)}");
    }

    void DoDelete(MapGrid grid)
    {
        var data = grid.GetComponent<GridSkinData>();
        if (data == null || data.GetSkinChildren().Count == 0)
        { SetStatus($"({grid.X},{grid.Y}) 无皮肤"); return; }

        var children = data.GetSkinChildren();
        if (children.Count > 1)
        {
            // 有叠加层：只删最上层（数据 + 实例），白膜显隐不动（同编辑器删除叠加层语义）
            PushUndo(Capture(grid));
            DetachAndDestroy(children[children.Count - 1]);
            data.ExtraSkins.RemoveAt(data.ExtraSkins.Count - 1);
            SetStatus($"已删除 ({grid.X},{grid.Y}) 最上层皮肤");
        }
        else
        {
            DoClear(grid, $"已清除 ({grid.X},{grid.Y}) 整格皮肤");
        }
    }

    /// <summary>清整格：销毁全部皮肤层、按记账恢复白膜、移除数据组件</summary>
    void DoClear(MapGrid grid, string okMsg)
    {
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) { SetStatus($"({grid.X},{grid.Y}) 无皮肤"); return; }

        PushUndo(Capture(grid));
        foreach (var c in data.GetSkinChildren()) DetachAndDestroy(c);
        if (data.Data != null && data.Data.TileHidden) data.ShowTileRenderers();
        Destroy(data); // 数据组件一并移除
        if (selectedGrid == grid) selectedGrid = null;
        SetStatus(okMsg);
    }

    void DoClearAll()
    {
        var mm = MapManager.Instance;
        var grids = mm != null
            ? mm.GetComponentsInChildren<MapGrid>(true)
            : FindObjectsOfType<MapGrid>();

        var snaps = new List<GridSnapshot>();
        foreach (var g in grids)
        {
            if (g == null || g.GetComponent<GridSkinData>() == null) continue;
            snaps.Add(Capture(g));
            foreach (var c in g.GetComponent<GridSkinData>().GetSkinChildren()) DetachAndDestroy(c);
            var d = g.GetComponent<GridSkinData>();
            if (d.Data != null && d.Data.TileHidden) d.ShowTileRenderers();
            Destroy(d);
        }
        if (snaps.Count == 0) { SetStatus("全图没有皮肤"); return; }
        PushUndo(snaps);
        selectedGrid = null;
        SetStatus($"已清除全图 {snaps.Count} 格皮肤（可撤销）");
    }

    // ---- 特殊替换 ----

    void DoReplaceSceneObject()
    {
        if (replaceTarget == null) { SetStatus("先用「特殊替换」模式点击一个场景物体"); return; }

        GameObject instance;
        var addr = CurrentAddress();
        if (addr != null)
        {
            instance = InstantiateByAddress(addr);
            if (instance == null) { SetStatus($"笔刷加载失败：{NameOf(addr)}"); return; }
        }
        else if (sceneTemplate != null)
        {
            instance = Instantiate(sceneTemplate);
        }
        else { SetStatus("笔刷未就绪：请先选 prefab 或取场景模板"); return; }

        // 保留目标的父级/位置/旋转/缩放（同编辑器 ReplaceSceneObject 语义）
        var old = replaceTarget;
        instance.transform.SetParent(old.transform.parent, true);
        instance.transform.localPosition = old.transform.localPosition;
        instance.transform.localRotation = old.transform.localRotation;
        instance.transform.localScale = old.transform.localScale;
        Destroy(old);
        replaceTarget = null;
        if (targetLabel != null) targetLabel.text = "";
        SetStatus("已特殊替换（此操作不在撤销栈内）");
    }

    // ---- 调整模式 ----

    void CycleLayer(int delta)
    {
        var data = selectedGrid != null ? selectedGrid.GetComponent<GridSkinData>() : null;
        if (data == null) { SetStatus("先用「调整」模式点击一个格子"); return; }
        int count = data.SkinLayerCount;
        if (count == 0) { SetStatus("该格没有皮肤层"); return; }
        selectedLayer = (selectedLayer + delta + count) % count;
        UpdateLayerLabel();
    }

    void UpdateLayerLabel()
    {
        if (layerLabel == null) return;
        var data = selectedGrid != null ? selectedGrid.GetComponent<GridSkinData>() : null;
        int count = data != null ? data.SkinLayerCount : 0;
        if (selectedGrid == null || count == 0) layerLabel.text = "（未选中）";
        else layerLabel.text = $"({selectedGrid.X},{selectedGrid.Y}) 第 {selectedLayer} 层 / 共 {count} 层";
    }

    /// <summary>把预设输入框的值应用到选中层（位置/旋转偏移，缩放 0 视为 1）</summary>
    void ApplyPresetToLayer()
    {
        var data = selectedGrid != null ? selectedGrid.GetComponent<GridSkinData>() : null;
        if (data == null || data.SkinLayerCount == 0) { SetStatus("先用「调整」模式点击一个已换皮的格子"); return; }
        int layer = Mathf.Clamp(selectedLayer, 0, data.SkinLayerCount - 1);
        var info = data.GetSkinInfoAt(layer);
        if (info == null) return;

        PushUndo(Capture(selectedGrid));
        info.Offset = ReadPresetOffset();
        info.Rotation = new Vector3(ReadFloat(inRotX), ReadFloat(inRotY), ReadFloat(inRotZ));
        float sx = ReadFloat(inSclX), sy = ReadFloat(inSclY), sz = ReadFloat(inSclZ);
        info.Scale = new Vector3(sx == 0 ? 1 : sx, sy == 0 ? 1 : sy, sz == 0 ? 1 : sz);
        data.ApplySkinTransformAt(layer);
        SetStatus($"已微调 ({selectedGrid.X},{selectedGrid.Y}) 第 {layer} 层（可撤销）");
    }

    /// <summary>读选中层的偏移/旋转/缩放到预设输入框（同编辑器"从选中皮肤读取预设"）</summary>
    void ReadLayerToPreset()
    {
        var data = selectedGrid != null ? selectedGrid.GetComponent<GridSkinData>() : null;
        if (data == null || data.SkinLayerCount == 0) { SetStatus("先用「调整」模式点击一个已换皮的格子"); return; }
        int layer = Mathf.Clamp(selectedLayer, 0, data.SkinLayerCount - 1);
        var info = data.GetSkinInfoAt(layer);
        if (info == null) return;

        SetInput(inOffX, info.Offset.x); SetInput(inOffY, info.Offset.y); SetInput(inOffZ, info.Offset.z);
        SetInput(inRotX, info.Rotation.x); SetInput(inRotY, info.Rotation.y); SetInput(inRotZ, info.Rotation.z);
        var sc = info.GetScaleMultiplier();
        SetInput(inSclX, sc.x); SetInput(inSclY, sc.y); SetInput(inSclZ, sc.z);
        SetStatus($"已读取 ({selectedGrid.X},{selectedGrid.Y}) 第 {layer} 层到预设");
    }

    void ResetLayer()
    {
        var data = selectedGrid != null ? selectedGrid.GetComponent<GridSkinData>() : null;
        if (data == null || data.SkinLayerCount == 0) { SetStatus("先用「调整」模式点击一个已换皮的格子"); return; }
        int layer = Mathf.Clamp(selectedLayer, 0, data.SkinLayerCount - 1);
        var info = data.GetSkinInfoAt(layer);
        if (info == null) return;

        PushUndo(Capture(selectedGrid));
        info.Offset = Vector3.zero;
        info.Rotation = Vector3.zero;
        info.Scale = Vector3.zero; // 全零语义 = 1（兼容旧数据约定）
        data.ApplySkinTransformAt(layer);
        SetStatus($"已重置 ({selectedGrid.X},{selectedGrid.Y}) 第 {layer} 层（可撤销）");
    }

    // ================= 预览窗口（窗口1） =================

    /// <summary>按当前笔刷实例化一个副本放进预览窗口（Addressables 或编辑器 AssetDatabase 均可）</summary>
    void ShowPreviewForCurrentBrush()
    {
        var addr = CurrentAddress();
        GameObject src;
        if (addr != null) src = InstantiateByAddress(addr);
        else if (sceneTemplate != null) src = Instantiate(sceneTemplate);
        else { SetStatus("没有可预览的笔刷"); return; }
        if (src == null) { SetStatus("预览失败：实例化返回空"); return; }
        ShowPreview(src);
    }

    void ShowPreview(GameObject src)
    {
        EnsurePreviewWindow();
        EnsurePreviewRig();
        CleanupPreviewInstance();
        previewInstance = src;

        // 隔离：独立 layer + 远处摆放 + 关掉碰撞体/相机/光源/音源，绝不能干扰地图渲染与射线
        StripColliders(previewInstance);
        SetLayerRecursive(previewInstance.transform, PreviewLayer);
        previewInstance.transform.position = new Vector3(PreviewPos, PreviewPos, PreviewPos);
        foreach (var a in previewInstance.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
        foreach (var c in previewInstance.GetComponentsInChildren<Camera>(true)) c.enabled = false;
        foreach (var l in previewInstance.GetComponentsInChildren<Light>(true)) l.enabled = false;

        var bounds = GetRenderBounds(previewInstance);
        var cam = previewRig.GetComponentInChildren<Camera>();
        if (bounds.extents == Vector3.zero)
        {
            previewWin.visible = true;
            SetStatus($"预览：{CleanName(previewInstance)}（无渲染体，纯逻辑 prefab）");
            return;
        }
        var dir = new Vector3(1f, 0.7f, -1f).normalized; // 右上前方 3/4 视角
        float dist = bounds.extents.magnitude / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f + 0.05f;
        cam.transform.position = bounds.center + dir * dist;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = dist * 20f + 100f;

        previewWin.visible = true;
        previewTitleText.text = $"预览：{CleanName(previewInstance)}";
    }

    void EnsurePreviewWindow()
    {
        if (previewWin != null) { previewWin.visible = true; return; }
        previewWin = new GComponent();
        previewWin.SetSize(300, 330);
        previewWin.draggable = true;
        AddRect(previewWin, 0, 0, 300, 330,
            new Color(0.10f, 0.11f, 0.13f, 0.94f), new Color(0.45f, 0.55f, 0.65f, 1f));
        previewTitleText = MakeText(previewWin, "预览", 8, 8, 250, 14, new Color(0.9f, 0.92f, 0.95f));
        previewTitleText.textFormat.bold = true;
        previewTitleText.text = "预览（窗口1）";
        MakeButton(previewWin, "×", 300 - 36, 6, 28, 22, () =>
        {
            previewWin.visible = false;
            CleanupPreviewInstance();
        });

        previewRT = new RenderTexture(512, 512, 24);
        previewLoader = new GLoader();
        previewWin.AddChild(previewLoader);
        previewLoader.SetXY(4, 30);
        previewLoader.SetSize(292, 296);
        previewLoader.texture = new NTexture(previewRT);

        view.AddChild(previewWin);
        // 放在面板左侧（放右侧会整个压在面板上，挡住列表/预设区的点击）
        previewWin.SetXY(Mathf.Max(0, panel.x - 300 - 8), panel.y);
    }

    void EnsurePreviewRig()
    {
        if (previewRig != null) return;
        previewRig = new GameObject("SkinBrushPreviewRig");
        DontDestroyOnLoad(previewRig); // 与运行器同生命周期

        var camGo = new GameObject("cam");
        camGo.transform.SetParent(previewRig.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = true; // 常开：每帧渲染到 RenderTexture（仅此一台相机，开销可忽略；关掉就永远不渲染了）
        cam.cullingMask = 1 << PreviewLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
        cam.fieldOfView = 30;
        cam.nearClipPlane = 0.01f;
        cam.targetTexture = previewRT;

        var lightGo = new GameObject("light");
        lightGo.transform.SetParent(previewRig.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
        var lit = lightGo.AddComponent<Light>();
        lit.type = LightType.Directional;
        lit.cullingMask = 1 << PreviewLayer; // 只照亮预览层，不影响地图
        lit.intensity = 1.1f;
        lit.shadows = LightShadows.None;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    /// <summary>实例显示名：去掉 "(Clone)" 后缀</summary>
    static string CleanName(GameObject go)
    {
        return go.name.Replace("(Clone)", "").Trim();
    }

    static Bounds GetRenderBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    void CleanupPreviewInstance()
    {
        if (previewInstance == null) return;
        DetachAndDestroy(previewInstance);
        previewInstance = null;
    }

    void OnDestroy()
    {
        CleanupPreviewInstance();
        if (previewRig != null) Destroy(previewRig.gameObject);
        if (previewRT != null) previewRT.Release();
    }

    // ================= 撤销 =================

    GridSnapshot Capture(MapGrid grid)
    {
        var s = new GridSnapshot { grid = grid, extras = new List<SkinInfo>() };
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return s;
        s.hadComponent = true;
        if (data.Data != null) s.data = data.Data.Clone();
        foreach (var ex in data.ExtraSkins)
            if (ex != null) s.extras.Add(ex.Clone());
        return s;
    }

    void PushUndo(List<GridSnapshot> snaps)
    {
        if (snaps == null || snaps.Count == 0) return;
        // 笔画进行中：并入当前批次（一次拖动 = 一步撤销）；松手时 EndStrokeBatch 统一提交
        if (strokeBatch != null) { strokeBatch.AddRange(snaps); return; }
        undoStack.Add(snaps);
        if (undoStack.Count > 50) undoStack.RemoveAt(0);
    }

    void PushUndo(GridSnapshot s) { PushUndo(new List<GridSnapshot> { s }); }

    /// <summary>笔画结束：把本批次快照去重（同格保留最早一份 = 操作前状态）后作为一步撤销入栈</summary>
    void EndStrokeBatch()
    {
        var batch = strokeBatch;
        strokeBatch = null;
        if (batch == null || batch.Count == 0) return;
        var seen = new HashSet<MapGrid>();
        var merged = new List<GridSnapshot>();
        foreach (var s in batch)
            if (s.grid != null && seen.Add(s.grid)) merged.Add(s);
        if (merged.Count == 0) return;
        undoStack.Add(merged);
        if (undoStack.Count > 50) undoStack.RemoveAt(0);
    }

    void DoUndo()
    {
        if (undoStack.Count == 0) { SetStatus("没有可撤销的操作"); return; }
        var snaps = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        RestoreSnapshots(snaps);
        UpdateLayerLabel();
        SetStatus($"已撤销（剩余 {undoStack.Count} 步）");
    }

    void RestoreSnapshots(List<GridSnapshot> snaps)
    {
        foreach (var s in snaps)
        {
            if (s.grid == null) continue;
            var data = s.grid.GetComponent<GridSkinData>();
            if (data != null)
                foreach (var c in data.GetSkinChildren()) DetachAndDestroy(c);

            if (!s.hadComponent)
            {
                if (data != null)
                {
                    data.ShowTileRenderers(); // 当时不存组件 = 白膜原本可见
                    Destroy(data);
                }
                continue;
            }

            if (data == null) data = s.grid.gameObject.AddComponent<GridSkinData>();
            data.Data = s.data != null ? s.data.Clone() : new SkinInfo();
            data.ExtraSkins = s.extras != null
                ? s.extras.Select(x => x.Clone()).ToList()
                : new List<SkinInfo>();
            if (data.Data.TileHidden) data.HideTileRenderers(); else data.ShowTileRenderers();
            if (!string.IsNullOrEmpty(data.Data.SkinName) || data.ExtraSkins.Count > 0)
            {
                data.ReapplyFromData();  // 按数据重建主皮肤实例
                data.ReapplyExtras();    // 补建叠加层并对位
            }
        }
    }

    // ================= 笔刷数据 =================

    /// <summary>按当前笔刷选择构建 SkinInfo（基准变换读 prefab 资源根；场景模板无地址）</summary>
    SkinInfo BuildCurrentBrushInfo()
    {
        var addr = CurrentAddress();
        if (addr == null)
        {
            if (sceneTemplate == null) return null;
            var st = sceneTemplate.transform;
            return new SkinInfo
            {
                SkinName = sceneTemplate.name, // 无地址/GUID：战斗内按存档重建无法还原（同编辑器警告）
                Offset = ReadPresetOffset(),
                Rotation = new Vector3(ReadFloat(inRotX), ReadFloat(inRotY), ReadFloat(inRotZ)),
                Scale = ReadPresetScale(),
                BaseLocalPos = st.localPosition,
                BaseRotation = st.localEulerAngles,
                BaseScale = st.localScale,
                BaseCaptured = true,
            };
        }

        var prefab = LoadPrefabAsset(addr);
        if (prefab == null)
        {
            // 诊断：Addressables 与编辑器 AssetDatabase 都没命中时给出地址，一眼定位
            SetStatus($"笔刷资源加载失败：{addr}\n（编辑器 AssetDatabase 也未命中，路径无效？）");
            return null;
        }
        var t = prefab.transform;
        return new SkinInfo
        {
            SkinAddress = addr,
            SkinName = NameOf(addr),
#if UNITY_EDITOR
            // 记 GUID：战斗内重建时 GridSkinData.LoadSkinInstance 的编辑器兜底靠它（未注册 Addressables 的 prefab）
            SkinGuid = UnityEditor.AssetDatabase.AssetPathToGUID(addr),
#endif
            Offset = ReadPresetOffset(),
            Rotation = new Vector3(ReadFloat(inRotX), ReadFloat(inRotY), ReadFloat(inRotZ)),
            Scale = ReadPresetScale(),
            BaseLocalPos = t.localPosition,
            BaseRotation = t.localEulerAngles,
            BaseScale = t.localScale,
            BaseCaptured = true,
        };
    }

    /// <summary>按地址取 prefab 资源（读基准变换用）。编辑器内 .prefab 路径直接走 AssetDatabase
    /// （这类路径不是 Addressables 地址，先走 Addressables 只会白吃一次失败加载）</summary>
    static GameObject LoadPrefabAsset(string addr)
    {
#if UNITY_EDITOR
        if (addr.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(addr);
            if (p != null) return p;
        }
#endif
        try { return ResHelper.GetAsset<GameObject>(addr); }
        catch { /* 无效地址 */ }
#if UNITY_EDITOR
        if (addr.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(addr);
#endif
        return null;
    }

    /// <summary>按地址实例化笔刷。编辑器内 .prefab 路径直接走 AssetDatabase（保留 prefab 连接）</summary>
    static GameObject InstantiateByAddress(string addr)
    {
#if UNITY_EDITOR
        if (addr.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(addr);
            if (prefab != null)
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
        }
#endif
        GameObject inst = null;
        try { inst = ResHelper.Instantiate(addr); } catch { /* 无效地址 */ }
#if UNITY_EDITOR
        if (inst == null && addr.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(addr);
            if (prefab != null)
                inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
        }
#endif
        return inst;
    }

    /// <summary>按当前笔刷选择实例化（prefab 走地址；场景模板走克隆）</summary>
    GameObject InstantiateCurrentBrush()
    {
        var addr = CurrentAddress();
        if (addr != null) return InstantiateByAddress(addr);
        if (sceneTemplate != null) return Instantiate(sceneTemplate);
        return null;
    }

    /// <summary>销毁一个皮肤实例：先脱离父级再销毁（防父级同帧销毁连带，运行时无 Undo，直接 Destroy）</summary>
    static void DetachAndDestroy(GameObject go)
    {
        if (go == null) return;
        go.transform.SetParent(null, false);
        Destroy(go);
    }

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Destroy(c);
    }

    Vector3 ReadPresetOffset()
    {
        return new Vector3(ReadFloat(inOffX), ReadFloat(inOffY), ReadFloat(inOffZ));
    }

    Vector3 ReadPresetScale()
    {
        float sx = ReadFloat(inSclX), sy = ReadFloat(inSclY), sz = ReadFloat(inSclZ);
        return new Vector3(sx == 0 ? 1 : sx, sy == 0 ? 1 : sy, sz == 0 ? 1 : sz);
    }

    static float ReadFloat(GTextInput input)
    {
        if (input == null || string.IsNullOrEmpty(input.text)) return 0;
        return float.TryParse(input.text.Trim(), out var v) ? v : 0;
    }

    static void SetInput(GTextInput input, float v)
    {
        if (input != null) input.text = v.ToString("0.###");
    }

    string CurrentAddress()
    {
        return prefabAddrs.Count > 0 ? prefabAddrs[Mathf.Clamp(brushIndex, 0, prefabAddrs.Count - 1)] : null;
    }

    static string NameOf(string addr)
    {
        if (string.IsNullOrEmpty(addr)) return "";
        int i = addr.LastIndexOf('/');
        var s = i >= 0 ? addr.Substring(i + 1) : addr;
        // 编辑器列表存的是带 .prefab 的资源路径，展示时去掉扩展名（同 Addressables 地址风格）
        return s.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 7) : s;
    }

    // ================= 笔刷列表（Addressables 前缀枚举 + 滚动列表） =================

    /// <summary>目录输入归一化：反斜杠→正斜杠、去首尾空白与尾斜杠（Addressables 地址都是正斜杠）</summary>
    static string NormalizeFolder(string f)
    {
        f = f.Trim().Replace('\\', '/').TrimEnd('/');
        return string.IsNullOrEmpty(f) ? DefaultFolder : f;
    }

    void RefreshPrefabs()
    {
        prefabAddrs.Clear();
        folder = NormalizeFolder(folder);
        // 注意：静态 Addressables.ResourceLocators 是 IEnumerable（无 Count 属性，Count 只能当方法调用）；
        // sampleKey/locatorCount 要声明在 try 外——状态栏在 try 外引用它们
        int locatorCount = 0;
        string sampleKey = null; // 找不到时给出一个 DungeonTile 键样例，便于对格式
        try
        {
#if UNITY_EDITOR
            // 编辑器内用 AssetDatabase 枚举：覆盖未注册 Addressables 的 prefab
            //（实测 flowersea/Prefabs 磁盘 30 个、仅 6 个注册，Addressables 枚举只见 6 个）
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) prefabAddrs.Add(path);
            }
#else
            // 界面刚创建就点刷新时 Addressables 可能尚未初始化，locator 列表为空
            if (!Addressables.ResourceLocators.Any())
                Addressables.InitializeAsync().WaitForCompletion();

            foreach (var locator in Addressables.ResourceLocators)
            {
                locatorCount++;
                foreach (var keyObj in locator.Keys)
                {
                    var key = keyObj as string;
                    if (key == null) continue;
                    if (sampleKey == null && key.StartsWith("Assets/Bundles/DungeonTile", StringComparison.OrdinalIgnoreCase))
                        sampleKey = key;
                    if (!key.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                    var ext = System.IO.Path.GetExtension(key);
                    if (ext != "" && !ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                    if (locator.Locate(key, typeof(GameObject), out _))
                        prefabAddrs.Add(key);
                }
            }
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SkinBrushRT] 枚举笔刷目录失败：{e.Message}");
        }
        prefabAddrs.Sort();
        brushIndex = 0;
        listScroll = 0;
        RebuildList();
        SetStatus(prefabAddrs.Count > 0
            ? $"笔刷列表：{prefabAddrs.Count} 个（{folder}）"
#if UNITY_EDITOR
            : $"未找到 prefab：{folder}");
#else
            : $"未找到 prefab：{folder}\nlocator={locatorCount} 个，地址样例：{(sampleKey ?? "无")}");
#endif
    }

    /// <summary>重建滚动列表条目（滚轮/拖拽上下浏览，同编辑器列表；点击选中并弹出预览窗口1）</summary>
    void RebuildList()
    {
        if (listContent == null) return;
        listContent.RemoveChildren();
        listItems.Clear();
        for (int i = 0; i < prefabAddrs.Count; i++)
        {
            int idx = i;
            var item = MakeButton(listContent, NameOf(prefabAddrs[i]), 0, i * ItemH,
                PanelW - 46, ItemH - 2, () =>
                {
                    brushIndex = idx;
                    RefreshListHighlight();
                    SetStatus($"当前笔刷：{NameOf(CurrentAddress())}");
                    ShowPreviewForCurrentBrush();
                });
            listItems.Add(item);
        }
        ApplyListScroll();
        RefreshListHighlight();
    }

    void RefreshListHighlight()
    {
        for (int i = 0; i < listItems.Count; i++)
            SetButtonFill(listItems[i], i == brushIndex
                ? new Color(0.2f, 0.5f, 0.3f, 1f)
                : new Color(0.2f, 0.24f, 0.29f, 1f));
    }

    void ApplyListScroll()
    {
        if (listContent == null) return;
        float viewH = 190f;
        float contentH = prefabAddrs.Count * ItemH;
        float maxScroll = Mathf.Max(0, contentH - viewH);
        listScroll = Mathf.Clamp(listScroll, 0, maxScroll);
        listContent.SetXY(0, -listScroll);
        // 同步滑条手柄位置
        if (sbThumb != null)
        {
            bool need = maxScroll > 1f;
            sbThumb.visible = need;
            if (need)
            {
                float trackH = SBTrackH - SBThumbH;
                sbThumb.y = maxScroll > 0 ? listScroll / maxScroll * trackH : 0;
            }
        }
    }

    // ================= 同步 / 全局 =================

    void SyncToMapInfo()
    {
        var viewData = UI_MapBuilder.Instance;
        var mm = MapManager.Instance;
        if (viewData == null || viewData.MapInfo == null || viewData.MapInfo.GridInfos == null || mm == null)
        { SetStatus("需在运行中的造图界面且已加载/新建地图"); return; }
        int n = GridSkinData.SyncSkinsToGridInfos(viewData.MapInfo.GridInfos,
            mm.GetComponentsInChildren<MapGrid>());
        SetStatus($"已同步 {n} 格到 MapInfo\n（点「保存」时也会自动强制同步一次）");
    }

    // ================= 面板构建（纯代码 FairyGUI） =================

    void BuildPanel()
    {
        panel = new GComponent();
        panel.SetSize(PanelW, PanelH);
        panel.draggable = true; // 整体可拖动（按钮/输入框/列表仍可正常交互）
        panel.visible = true;

        AddRect(panel, 0, 0, PanelW, PanelH, new Color(0.10f, 0.11f, 0.13f, 0.94f),
            new Color(0.45f, 0.55f, 0.65f, 1f));

        // 标题 + 关闭
        var title = MakeText(panel, "", 8, 8, 390, 14, new Color(0.9f, 0.92f, 0.95f));
        title.textFormat.bold = true;
        title.text = "皮肤笔刷（T 显隐 · 右下角拖动缩放）";
        MakeButton(panel, "×", (int)PanelW - 36, 6, 28, 22, () => panel.visible = false);

        // ---- 模式行 ----
        float[] mx = { 8, 66, 124, 204, 262, 320 };
        float[] mw = { 54, 54, 76, 54, 54, 68 };
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            modeBtns[i] = MakeButton(panel, ModeNames[i], mx[i], 40, mw[i], 26, () => ToggleMode(idx));
        }

        // ---- 选项行 ----
        hideBtn = MakeButton(panel, "隐藏白膜：开", 8, 74, 150, 24, () =>
        {
            hideOriginal = !hideOriginal;
            RefreshToggleVisual();
        });
        stripBtn = MakeButton(panel, "剥离碰撞体：开", 162, 74, 150, 24, () =>
        {
            stripColliders = !stripColliders;
            RefreshToggleVisual();
        });

        // ---- 笔刷目录行 ----
        MakeText(panel, "目录", 8, 112, 36, 13, TextGray());
        inFolder = MakeInput(panel, folder, 46, 106, 230, 24);
        MakeButton(panel, "刷新", 280, 106, 48, 24, () =>
        {
            folder = NormalizeFolder(string.IsNullOrEmpty(inFolder.text) ? DefaultFolder : inFolder.text);
            inFolder.text = folder;
            RefreshPrefabs();
        });
        MakeButton(panel, "取场景笔刷", 332, 106, 88, 24, () =>
        {
            pickSceneTemplate = true;
            SetStatus("请在场景中点击一个物体作为笔刷模板\n（无资源地址，战斗内按存档重建时无法还原）");
        });

        // ---- 场景模板行 ----
        MakeText(panel, "模板", 8, 144, 36, 13, TextGray());
        templateLabel = MakeText(panel, "（无）", 46, 144, 300, 13, new Color(0.95f, 0.9f, 0.6f));
        MakeButton(panel, "清除", 350, 140, 52, 24, () =>
        {
            sceneTemplate = null;
            templateLabel.text = "（无）";
        });

        // ---- 笔刷滚动列表 ----
        listComp = new GComponent();
        panel.AddChild(listComp);
        listComp.SetXY(8, 170);
        listComp.SetSize(PanelW - 32, 190);
        // 裁剪视口（纯代码无 ScrollPane，用 Container.clipRect 手动裁剪）
        ((Container)listComp.displayObject).clipRect = new Rect(0, 0, PanelW - 32, 190);
        listContent = new GComponent();
        listComp.AddChild(listContent);
        listContent.SetSize(PanelW - 32, 2000);

        // 滚轮滚动
        listComp.displayObject.AddEventListener("onMouseWheel", (EventContext ec) =>
        {
            listScroll -= ec.inputEvent.mouseWheelDelta * 240f;
            ApplyListScroll();
        });
        // 按住拖拽滚动（同缩放手柄：必须 CaptureTouch，onTouchMove 才会派发；StopPropagation 防止整面板被拖走）
        listComp.onTouchBegin.Add((EventContext ec) =>
        {
            listDragging = true;
            listLastY = Stage.inst.touchPosition.y;
            ec.CaptureTouch();
            ec.StopPropagation();
        });
        listComp.onTouchMove.Add((EventContext ec) =>
        {
            if (!listDragging) return;
            float y = Stage.inst.touchPosition.y;
            listScroll -= y - listLastY;
            listLastY = y;
            ApplyListScroll();
        });
        listComp.onTouchEnd.Add((EventContext ec) => listDragging = false);

        // ---- 右侧可拖动滑条 ----
        AddRect(listComp, PanelW - 32 - 10, 0, 8, SBTrackH,
            new Color(0.16f, 0.18f, 0.22f, 1f), new Color(0.3f, 0.35f, 0.4f, 1f));
        sbThumb = new GComponent();
        listComp.AddChild(sbThumb);
        sbThumb.SetSize(8, SBThumbH);
        AddRect(sbThumb, 0, 0, 8, SBThumbH,
            new Color(0.45f, 0.55f, 0.65f, 1f), new Color(0.6f, 0.7f, 0.8f, 1f));
        sbThumb.onTouchBegin.Add((EventContext ec) =>
        {
            sbDragging = true;
            sbDragStartY = Stage.inst.touchPosition.y;
            sbDragStartThumbY = sbThumb.y;
            ec.CaptureTouch();      // 同缩放手柄：不 capture 则 onTouchMove 永不触发
            ec.StopPropagation();  // 不冒泡到面板 draggable / 列表拖拽
        });
        sbThumb.onTouchMove.Add((EventContext ec) =>
        {
            if (!sbDragging) return;
            float maxScroll = Mathf.Max(0, prefabAddrs.Count * ItemH - 190f);
            float trackH = SBTrackH - SBThumbH;
            float ty = Mathf.Clamp(sbDragStartThumbY + (Stage.inst.touchPosition.y - sbDragStartY), 0, trackH);
            sbThumb.y = ty;
            listScroll = trackH > 0 ? ty / trackH * maxScroll : 0;
            listContent.SetXY(0, -listScroll);
        });
        sbThumb.onTouchEnd.Add((EventContext ec) => sbDragging = false);

        // ---- 预设区（3×3） ----
        // 注意：必须直接给字段赋值。C# 数组只存引用拷贝，先建数组再 rows[r][c]=... 不会写回字段
        // （旧写法导致 9 个字段永远 null，读到恒 0 = "应用到该层"把层弹回基准位，表现为固定 Y 向移动）
        MakeText(panel, "预设：替换/组合套用；调整模式选层后「应用到该层」（缩放 0 按 1）", 8, 368, 424, 12, new Color(0.6f, 0.65f, 0.7f));
        float[] rowY = { 388, 414, 440 };
        MakeText(panel, "偏移", 8, rowY[0] + 4, 36, 13, TextGray());
        inOffX = MakeInput(panel, "0", 46, rowY[0], 58, 22);
        inOffY = MakeInput(panel, "0", 108, rowY[0], 58, 22);
        inOffZ = MakeInput(panel, "0", 170, rowY[0], 58, 22);
        MakeText(panel, "旋转", 8, rowY[1] + 4, 36, 13, TextGray());
        inRotX = MakeInput(panel, "0", 46, rowY[1], 58, 22);
        inRotY = MakeInput(panel, "0", 108, rowY[1], 58, 22);
        inRotZ = MakeInput(panel, "0", 170, rowY[1], 58, 22);
        MakeText(panel, "缩放", 8, rowY[2] + 4, 36, 13, TextGray());
        inSclX = MakeInput(panel, "1", 46, rowY[2], 58, 22);
        inSclY = MakeInput(panel, "1", 108, rowY[2], 58, 22);
        inSclZ = MakeInput(panel, "1", 170, rowY[2], 58, 22);
        MakeButton(panel, "清零预设", 246, 388, 82, 22, () =>
        {
            SetInput(inOffX, 0); SetInput(inOffY, 0); SetInput(inOffZ, 0);
            SetInput(inRotX, 0); SetInput(inRotY, 0); SetInput(inRotZ, 0);
            SetInput(inSclX, 1); SetInput(inSclY, 1); SetInput(inSclZ, 1);
        });
        MakeButton(panel, "读该层→预设", 332, 388, 100, 22, ReadLayerToPreset);

        // ---- 调整区 ----
        MakeText(panel, "微调：「调整」模式点击格子选中，选层后用预设数值「应用到该层」", 8, 470, 424, 12, new Color(0.6f, 0.65f, 0.7f));
        MakeText(panel, "层", 8, 498, 30, 13, TextGray());
        MakeButton(panel, "◀", 46, 492, 24, 24, () => CycleLayer(-1));
        layerLabel = MakeText(panel, "（未选中）", 74, 498, 150, 13, new Color(0.95f, 0.9f, 0.6f));
        MakeButton(panel, "▶", 228, 492, 24, 24, () => CycleLayer(1));
        MakeButton(panel, "应用到该层", 256, 492, 100, 24, ApplyPresetToLayer);
        MakeButton(panel, "重置该层", 360, 492, 68, 24, ResetLayer);

        // ---- 特殊替换行 ----
        MakeText(panel, "目标", 8, 534, 36, 13, TextGray());
        targetLabel = MakeText(panel, "", 46, 534, 240, 13, new Color(0.95f, 0.9f, 0.6f));
        MakeButton(panel, "替换为笔刷", 290, 530, 86, 24, DoReplaceSceneObject);
        MakeButton(panel, "取为笔刷", 380, 530, 52, 24, () =>
        {
            if (replaceTarget == null) { SetStatus("先用「特殊替换」模式点击一个场景物体"); return; }
            sceneTemplate = replaceTarget;
            templateLabel.text = replaceTarget.name;
            SetStatus($"已取 {replaceTarget.name} 为笔刷模板");
        });

        // ---- 全局操作行 ----
        MakeButton(panel, "撤销", 8, 564, 64, 26, DoUndo);
        MakeButton(panel, "同步到MapInfo", 76, 564, 120, 26, SyncToMapInfo);
        MakeButton(panel, "清除全图皮肤", 200, 564, 110, 26, DoClearAll);

        // ---- 状态栏（多行） ----
        statusText = MakeText(panel, "", 8, 600, 424, 12, new Color(0.55f, 0.85f, 0.6f));
        statusText.SetSize(424, 80);
        statusText.singleLine = false;

        // ---- 右下角缩放手柄 ----
        var grip = AddRect(panel, PanelW - 22, PanelH - 22, 20, 20,
            new Color(0.3f, 0.4f, 0.5f, 0.9f), new Color(0.55f, 0.65f, 0.75f, 1f));
        grip.onTouchBegin.Add((EventContext ec) =>
        {
            resizing = true;
            resizeStart = Stage.inst.touchPosition;
            resizeScaleStart = panel.scaleX;
            // onTouchMove 只派发给 touchMonitor（CaptureTouch 的对象）；
            // 不 capture 则移动事件只发 Stage，grip.onTouchMove 永远不触发
            ec.CaptureTouch();
            // 阻断冒泡到 panel 的 draggable 处理器，避免缩放时面板同时被拖动
            ec.StopPropagation();
        });
        grip.onTouchMove.Add((EventContext ec) =>
        {
            if (!resizing) return;
            var p = Stage.inst.touchPosition;
            float s = Mathf.Clamp(resizeScaleStart + (p.x - resizeStart.x) / 440f, 0.7f, 2.0f);
            panel.SetScale(s, s);
        });
        grip.onTouchEnd.Add((EventContext ec) =>
        {
            resizing = false;
        });

        // 挂到造图界面（后加的子物体显示在最上；随界面显隐联动）
        view.AddChild(panel);
        panel.SetXY(Mathf.Max(0, view.width - (int)PanelW - 12), 30);
        RefreshToggleVisual();
        RefreshModeVisual();
    }

    void ToggleMode(int idx)
    {
        mode = (mode == idx) ? -1 : idx;
        strokeGrids.Clear();
        RefreshModeVisual();
        SetStatus(mode < 0 ? "已停用（不再拦截鼠标）"
            : $"当前模式：{ModeNames[mode]}\n" + ModeHint(mode));
    }

    static string ModeHint(int m)
    {
        switch (m)
        {
            case 0: return "点击/拖动格子 = 替换该格主皮肤";
            case 1: return "点击/拖动格子 = 叠加一层（无主皮肤则首刷）";
            case 2: return "点击场景物体选中 → 面板「替换为笔刷」";
            case 3: return "点击已换皮格子选中 → 选层 → 应用预设数值";
            case 4: return "点击格子 = 删最上层（无叠加层则清整格）";
            case 5: return "点击格子 = 清空该格全部皮肤并恢复白膜";
            default: return "";
        }
    }

    void RefreshModeVisual()
    {
        for (int i = 0; i < 6; i++)
            SetButtonFill(modeBtns[i], mode == i ? new Color(0.85f, 0.45f, 0.2f, 1f) : new Color(0.2f, 0.24f, 0.29f, 1f));
    }

    void RefreshToggleVisual()
    {
        SetButtonFill(hideBtn, hideOriginal ? new Color(0.2f, 0.45f, 0.3f, 1f) : new Color(0.2f, 0.24f, 0.29f, 1f));
        SetButtonFill(stripBtn, stripColliders ? new Color(0.2f, 0.45f, 0.3f, 1f) : new Color(0.2f, 0.24f, 0.29f, 1f));
        var h = hideBtn != null ? hideBtn.GetChildAt(1) as GTextField : null;
        if (h != null) h.text = "隐藏白膜：" + (hideOriginal ? "开" : "关");
        var sp = stripBtn != null ? stripBtn.GetChildAt(1) as GTextField : null;
        if (sp != null) sp.text = "剥离碰撞体：" + (stripColliders ? "开" : "关");
    }

    void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
    }

    static Color TextGray() { return new Color(0.7f, 0.74f, 0.8f, 1f); }

    // ================= FairyGUI 控件辅助 =================

    static GGraph AddRect(GComponent parent, float x, float y, float w, float h, Color fill, Color line)
    {
        var g = new GGraph();
        parent.AddChild(g);
        g.SetXY(x, y);
        g.SetSize(w, h);
        g.DrawRect(w, h, line.a > 0 ? 1 : 0, line, fill);
        return g;
    }

    static GTextField MakeText(GComponent parent, string text, float x, float y, float w, int size, Color color)
    {
        var t = new GTextField();
        parent.AddChild(t);
        t.SetXY(x, y);
        t.SetSize(w, size + 10);
        t.textFormat.size = size; // 先设格式再加文本（TextFormat 是 class，直接改字段不触发重排）
        t.color = color;
        t.text = text;
        return t;
    }

    /// <summary>纯代码按钮：底色矩形 + 居中文本；背景图存进 data 供改色高亮</summary>
    static GComponent MakeButton(GComponent parent, string title, float x, float y, float w, float h, Action onClick)
    {
        var btn = new GComponent();
        parent.AddChild(btn);
        btn.SetXY(x, y);
        btn.SetSize(w, h);

        var bg = new GGraph();
        btn.AddChild(bg);
        bg.SetSize(w, h);
        bg.DrawRect(w, h, 1, new Color(0.5f, 0.58f, 0.66f, 1f), new Color(0.2f, 0.24f, 0.29f, 1f));
        btn.data = bg;

        var t = new GTextField();
        btn.AddChild(t);
        t.SetSize(w, h);
        t.textFormat.size = 13;
        t.color = new Color(0.9f, 0.92f, 0.95f);
        t.align = AlignType.Center;
        t.verticalAlign = VertAlignType.Middle;
        t.text = title;

        btn.onClick.Add(() => onClick());
        return btn;
    }

    static void SetButtonFill(GComponent btn, Color fill)
    {
        var bg = btn != null ? btn.data as GGraph : null;
        if (bg == null) return;
        bg.DrawRect(bg.width, bg.height, 1, new Color(0.5f, 0.58f, 0.66f, 1f), fill);
    }

    static GTextInput MakeInput(GComponent parent, string text, float x, float y, float w, float h)
    {
        AddRect(parent, x, y, w, h, new Color(0.05f, 0.06f, 0.08f, 0.95f), new Color(0.35f, 0.4f, 0.46f, 1f));
        var input = new GTextInput();
        parent.AddChild(input);
        input.SetXY(x + 4, y);
        input.SetSize(w - 8, h);
        input.textFormat.size = 13;
        input.color = new Color(0.9f, 0.92f, 0.95f);
        input.verticalAlign = VertAlignType.Middle;
        input.text = text;
        return input;
    }
}
