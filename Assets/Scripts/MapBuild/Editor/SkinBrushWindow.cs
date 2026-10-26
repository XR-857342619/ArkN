using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 地图换皮·皮肤笔刷
///
/// 功能模式（独立开关；同时开时优先级：初始化 > 特殊替换 > 组合 > 默认）：
/// - 特殊替换模式：Scene 原生点击选中图中已有模型（黄色线框高亮），用当前笔刷替换之；
///   保留位置/旋转/缩放/兄弟层级/子物体（原图连接关系），自动更新图节点数据；
///   选中白膜本体不可替换（机制载体）；非格子模型走通用替换。
/// - 组合模式：点击已换皮格子时不替换，而是叠加一层新皮肤（Skin_Model_1..n）；
///   层级顺序=挂载顺序，后叠加的显示在上（sibling 置顶 + MeshRenderer sortingOrder 递增）。
/// - 初始化模式：点击任意格子清空该格全部皮肤（含叠加层）并恢复白膜显示。
/// - 默认（模式都关）：点击替换该格主皮肤。
///
/// 其他能力：笔刷预设、从场上任意预制体取笔刷、隐藏白膜开关、
/// 多层微调（层选择 Popup + 手柄/输入框）、Undo 完整可逆、Addressables 自动注册、
/// 存档同步与自愈（含叠加层）、FBX 自动纳入笔刷列表。
/// </summary>
public class SkinBrushWindow : EditorWindow
{
    [MenuItem("Tools/地图换皮/皮肤笔刷")]
    static void Open()
    {
        var w = GetWindow<SkinBrushWindow>("皮肤笔刷");
        w.minSize = new Vector2(340, 560);
        w.Show();
    }

    /// <summary>皮肤笔刷默认关闭：关掉时菜单灰显，避免误用（见 FeatureFlags.SkinBrush）。</summary>
    [MenuItem("Tools/地图换皮/皮肤笔刷", true)]
    static bool OpenValidate() => FeatureFlags.SkinBrush;

    // ================= 窗口状态 =================

    string folder;
    List<GameObject> prefabs = new List<GameObject>();
    GameObject brush;                     // 预制体资源笔刷
    GameObject sceneBrushTemplate;        // 场景模板笔刷（非预制体资源时，仅当前会话有效）
    Vector2 scroll;
    bool adjustMode = false;              // 调整模式（选中格子用 Scene 手柄/面板微调）
    bool deleteMode = false;              // 删除模式（点击图中物体即删除）
    bool stripColliders = true;
    bool hideOriginal = true;
    /// <summary>新画笔的锚点语义（见 SkinAnchor）。默认 TileSurface：皮肤站在格子表面，Offset 为 0 即正确。</summary>
    SkinAnchor brushAnchor = SkinAnchor.TileSurface;
    /// <summary>
    /// 自动贴合（默认开）：刷完立刻按地块类型对齐包围盒 ——
    /// 地面块：包围盒顶面贴地面块顶面；高台块：包围盒底面贴地面平面（与地面同高，衔接无缝）。
    /// </summary>
    bool autoFit = true;
    /// <summary>
    /// 水平纠错（默认开）：把皮肤包围盒中心对齐到格子中心，纠正"素材 prefab 根 transform 非 (0,0,0)"的水平偏移。
    /// </summary>
    bool autoFitXZ = true;
    bool combineMode = false;             // 组合模式
    bool initMode = false;                // 初始化模式
    bool replaceMode = false;             // 特殊替换模式
    string status = "";

    // 笔刷预设（批量刷皮时统一套用）
    Vector3 presetOffset;
    Vector3 presetRotation;
    Vector3 presetScale = Vector3.one;

    // 多层微调当前层
    int selectedLayer;

    // 笔画（一次拖动）状态
    readonly HashSet<MapGrid> paintedThisStroke = new HashSet<MapGrid>();
    int strokeUndoGroup = -1;

    // 特殊替换高亮线框端点缓存（避免每帧分配）
    static readonly Vector3[] s_HighlightCorners = new Vector3[24];

    void OnEnable()
    {
        folder = EditorPrefs.GetString("SkinBrush.Folder", "Assets/Bundles/DungeonTile");
        stripColliders = EditorPrefs.GetBool("SkinBrush.StripColliders", true);
        hideOriginal = EditorPrefs.GetBool("SkinBrush.HideOriginal", true);
        brushAnchor = (SkinAnchor)EditorPrefs.GetInt("SkinBrush.Anchor", (int)SkinAnchor.TileSurface);
        autoFit = EditorPrefs.GetBool("SkinBrush.AutoFit", true);
        autoFitXZ = EditorPrefs.GetBool("SkinBrush.AutoFitXZ", true);
        combineMode = EditorPrefs.GetBool("SkinBrush.CombineMode", false);
        initMode = EditorPrefs.GetBool("SkinBrush.InitMode", false);
        replaceMode = EditorPrefs.GetBool("SkinBrush.ReplaceMode", false);
        adjustMode = EditorPrefs.GetBool("SkinBrush.AdjustMode", false);
        deleteMode = EditorPrefs.GetBool("SkinBrush.DeleteMode", false);
        presetOffset = LoadV3("Offset", Vector3.zero);
        presetRotation = LoadV3("Rotation", Vector3.zero);
        presetScale = LoadV3("Scale", Vector3.one);
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        RefreshList();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (strokeUndoGroup >= 0) EndStroke();
    }

    void OnSelectionChanged()
    {
        // 选中皮肤子物体时自动定位到对应层（否则默认主层，改数值会作用到别的层上）
        selectedLayer = 0;
        panelError = null; // 切换选中时重置面板异常状态
        try
        {
            var sel = Selection.activeGameObject;
            var grid = sel != null ? sel.GetComponentInParent<MapGrid>() : null;
            if (grid != null)
            {
                var data = grid.GetComponent<GridSkinData>();
                if (data != null)
                {
                    var children = data.GetSkinChildren();
                    int idx = children.FindIndex(c => c != null && (sel == c || sel.transform.IsChildOf(c.transform)));
                    if (idx >= 0) selectedLayer = idx;

                    // 事件驱动的双向同步：选中变化时才以场景实际变换为准回写数据。
                    // 不放在 OnGUI 每帧执行——否则会与面板字段输入同帧竞争，把刚输入的数值打回
                    SkinPainter.SyncTransformsFromChildren(data);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        Repaint();
    }

    void OnUndoRedo()
    {
        // 撤销/重做后：先以场景实际变换为准回写数据（避免旧数据把场景打回原形），
        // 再把场景皮肤状态同步进 MapInfo（仅运行中的地图编辑器会话）
        try { SyncAllTransformsFromChildren(); }
        catch (Exception ex) { Debug.LogException(ex); }
        if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
        Repaint();
    }

    /// <summary>全图同步：每格皮肤以子物体实际变换为准回写数据（撤销/重做后调用）</summary>
    void SyncAllTransformsFromChildren()
    {
        var mm = MapManager.Instance;
        var grids = mm != null
            ? mm.GetComponentsInChildren<MapGrid>(true)
            : UnityEngine.Object.FindObjectsOfType<MapGrid>();
        foreach (var g in grids)
        {
            if (g == null) continue;
            var d = g.GetComponent<GridSkinData>();
            if (d != null && d.GetSkinChildren().Count > 0)
                SkinPainter.SyncTransformsFromChildren(d);
        }
    }

    // ================= 面板 =================

    void OnGUI()
    {
        EditorGUILayout.Space(4);

        // ---- 功能模式 ----
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("功能模式", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            combineMode = EditorGUILayout.Toggle(
                new GUIContent("组合模式", "点击已换皮格子：不替换，而是叠加一层新皮肤"),
                combineMode);
            initMode = EditorGUILayout.Toggle(
                new GUIContent("初始化模式", "点击任意格子：清空该格全部皮肤并恢复白膜"),
                initMode);
            replaceMode = EditorGUILayout.Toggle(
                new GUIContent("特殊替换模式", "在 Scene 中点击选中图中已有模型（黄色线框高亮），\n再用当前笔刷替换它：保留位置/旋转/缩放/层级与子物体，更新图节点数据"),
                replaceMode);
            adjustMode = EditorGUILayout.Toggle(
                new GUIContent("调整模式", "选中已换皮格子，用 Scene 手柄或面板输入框微调皮肤局部变换；\n多个皮肤层时可在面板切换调整层"),
                adjustMode);
            deleteMode = EditorGUILayout.Toggle(
                new GUIContent("删除模式", "Scene 左键点击物体即删除（整笔 = 一次撤销）。\n皮肤子物体连数据一起删；白膜与格子本体受保护；其他自由物体直接删除"),
                deleteMode);
            if (EditorGUI.EndChangeCheck())
            {
                // 调整/删除模式独占场景行为：勾选其一即退出其他模式，防止同一手势两种语义
                if (adjustMode)
                {
                    deleteMode = combineMode = initMode = replaceMode = false;
                }
                else if (deleteMode)
                {
                    adjustMode = combineMode = initMode = replaceMode = false;
                }
                else if (combineMode || initMode || replaceMode)
                {
                    adjustMode = deleteMode = false;
                }
                EditorPrefs.SetBool("SkinBrush.CombineMode", combineMode);
                EditorPrefs.SetBool("SkinBrush.InitMode", initMode);
                EditorPrefs.SetBool("SkinBrush.ReplaceMode", replaceMode);
                EditorPrefs.SetBool("SkinBrush.AdjustMode", adjustMode);
                EditorPrefs.SetBool("SkinBrush.DeleteMode", deleteMode);
            }

            string hint;
            if (initMode)
                hint = "初始化模式优先生效（点击=清空该格全部皮肤）。";
            else if (replaceMode)
                hint = "当前生效：特殊替换模式。Scene 左键原生点击选中模型（不再笔刷绘制），下方确认替换。";
            else if (combineMode)
                hint = "当前生效：组合模式（点击=叠加皮肤层）。";
            else if (deleteMode)
                hint = "当前生效：删除模式（左键点击/拖动物体即删除，整笔=一次撤销）。";
            else if (adjustMode)
                hint = "当前生效：调整模式。选中已换皮格子后用 Scene 手柄或面板微调；多个皮肤层时可在下方面板切换调整层。";
            else
                hint = "当前生效：默认替换（点击=替换该格主皮肤）。";
            EditorGUILayout.HelpBox(hint, initMode || deleteMode ? MessageType.Warning : MessageType.Info);

            if (replaceMode) DrawReplacePanel();
        }

        // ---- 笔刷选择 ----
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("笔刷来源", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从选中物体取笔刷")) PickBrushFromSelection();
            }
            brush = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("预制体笔刷", "任意 Prefab 资源；不限于四种基础地块"),
                brush, typeof(GameObject), false);
            if (sceneBrushTemplate != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("场景模板", sceneBrushTemplate.name);
                    if (GUILayout.Button("清除", GUILayout.Width(44))) sceneBrushTemplate = null;
                }
                EditorGUILayout.HelpBox("场景模板笔刷只复制当前物体，无资源地址：战斗内按存档重建时无法还原，正式地图请使用预制体笔刷。", MessageType.Warning);
            }

            EditorGUILayout.LabelField("皮肤 Prefab / FBX 文件夹");
            using (new EditorGUILayout.HorizontalScope())
            {
                var newFolder = EditorGUILayout.DelayedTextField(folder);
                if (newFolder != folder) { folder = newFolder; RefreshList(); }
                if (GUILayout.Button("浏览", GUILayout.Width(44))) BrowseFolder();
                if (GUILayout.Button("刷新", GUILayout.Width(44))) RefreshList();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(180));
            if (prefabs.Count == 0)
                EditorGUILayout.HelpBox("该文件夹下没有找到 Prefab。", MessageType.Warning);
            foreach (var p in prefabs)
            {
                var style = p == brush ? (GUIStyle)"flow node 3" : (GUIStyle)"Button";
                var rect = GUILayoutUtility.GetRect(new GUIContent(p.name), style, GUILayout.Height(24));
                if (GUI.Button(rect, p.name, style)) { brush = p; sceneBrushTemplate = null; Repaint(); }
            }
            EditorGUILayout.EndScrollView();
        }

        // ---- 笔刷预设 ----
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("笔刷预设（批量刷皮统一套用）", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            presetOffset = EditorGUILayout.Vector3Field("位置偏移（局部）", presetOffset);
            presetRotation = EditorGUILayout.Vector3Field("旋转角度（局部）", presetRotation);
            presetScale = EditorGUILayout.Vector3Field("缩放倍率", presetScale);
            if (EditorGUI.EndChangeCheck())
            {
                SaveV3("Offset", presetOffset);
                SaveV3("Rotation", presetRotation);
                SaveV3("Scale", presetScale);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从选中皮肤读取预设")) ReadPresetFromSelection();
                if (GUILayout.Button("清零预设", GUILayout.Width(80)))
                {
                    presetOffset = Vector3.zero;
                    presetRotation = Vector3.zero;
                    presetScale = Vector3.one;
                    SaveV3("Offset", presetOffset);
                    SaveV3("Rotation", presetRotation);
                    SaveV3("Scale", presetScale);
                }
            }
        }

        // ---- 选项 ----
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("笔刷选项", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            stripColliders = EditorGUILayout.Toggle(
                new GUIContent("剥离皮肤碰撞体", "白膜保留的 Collider 是唯一点击判定来源，皮肤碰撞体建议剥离"),
                stripColliders);
            // 隐藏白膜开关：仅影响"替换/组合首层"时是否隐藏白膜；组合叠加层不动白膜显示
            hideOriginal = EditorGUILayout.Toggle(
                new GUIContent("隐藏白膜", "首层换皮时隐藏原模型 Renderer（Collider 保留）；关闭可保留原模型用于组合对照"),
                hideOriginal);
            brushAnchor = (SkinAnchor)EditorGUILayout.EnumPopup(
                new GUIContent("皮肤锚点",
                    "TileSurface(推荐)=皮肤原点对齐格子可视表面（白膜顶面，实测计算）；\n" +
                    "TileCenter=旧语义，对齐白膜几何中心（存量老图的取值，皮肤会整体下沉约 0.3）；\n" +
                    "GridOrigin=对齐 MapGrid 原点，不含白膜偏移。"),
                brushAnchor);
            autoFit = EditorGUILayout.Toggle(
                new GUIContent("自动贴合（按地块类型）",
                    "开：刷完立刻按包围盒对齐 ——" + "\n" +
                    "  地面块 → 包围盒【顶面】对齐地面块顶面 (y=0)；" + "\n" +
                    "  高台块 → 包围盒【底面】对齐地面平面 (y=0)，与相邻地面块衔接无缝。" + "\n" +
                    "关：只按锚点+基准+预设摆放，完全手工。"),
                autoFit);
            autoFitXZ = EditorGUILayout.Toggle(
                new GUIContent("水平纠错（包围盒居中）",
                    "开：把皮肤包围盒中心对齐到格子中心（只动 X/Z）。" + "\n" +
                    "用于纠正素材导入时 prefab 根 transform 不是 (0,0,0) 造成的水平偏移；" + "\n" +
                    "关：X/Z 只按 模型基准 + 预设偏移 摆放。"),
                autoFitXZ);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt("SkinBrush.Anchor", (int)brushAnchor);
                EditorPrefs.SetBool("SkinBrush.AutoFit", autoFit);
                EditorPrefs.SetBool("SkinBrush.AutoFitXZ", autoFitXZ);
                EditorPrefs.SetBool("SkinBrush.StripColliders", stripColliders);
                EditorPrefs.SetBool("SkinBrush.HideOriginal", hideOriginal);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("按地块类型重新贴合",
                    "对选中格子（无选中则全图）重跑一次自动贴合；地块类型被改过（地面<->高台）时用它修正")))
                {
                    var selGrid = Selection.activeGameObject != null
                        ? Selection.activeGameObject.GetComponentInParent<MapGrid>() : null;
                    var gs = selGrid != null ? new[] { selGrid } : CollectAllGrids().ToArray();
                    int n = 0, cells = 0;
                    Undo.IncrementCurrentGroup();
                    int grp = Undo.GetCurrentGroup();
                    foreach (var g in gs)
                    {
                        var d = g.GetComponent<GridSkinData>();
                        if (d == null) continue;
                        Undo.RecordObject(d, "重新贴合皮肤");
                        for (int i = 0; i < d.SkinLayerCount; i++)
                        {
                            var li = d.GetSkinInfoAt(i);
                            if (li == null) continue;
                            li.AutoFit = autoFit;
                            li.AutoFitXZ = autoFitXZ;
                        }
                        int k = d.RecomputeAutoFitAll();
                        if (k > 0) { n += k; cells++; EditorUtility.SetDirty(d); }
                    }
                    Undo.CollapseUndoOperations(grp);
                    if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
                    status = n > 0 ? $"已重新贴合 {n} 层（{cells} 格）。" : "没有需要调整的皮肤层。";
                }
            }
        }

        // ---- 微调面板 ----
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("皮肤微调（选中已换皮格子）", EditorStyles.boldLabel);
            DrawAdjustPanel();
        }

        // ---- 全局操作 ----
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("同步皮肤数据到 MapInfo"))
                status = SkinPainter.TrySyncToMapInfo()
                    ? "已同步到当前地图编辑会话的 MapInfo。"
                    : "未找到运行中的地图编辑器（需在运行中的 MapBuilder 界面使用）。";
            if (GUILayout.Button("修复存档皮肤数据"))
            {
                var n = SkinPainter.FixSavedMapSkins();
                status = n < 0
                    ? "需在运行中的游戏进程里执行（要访问 Database.Maps）。"
                    : $"已修复 {n} 条皮肤数据（GUID/地址补全并自动注册 Addressables）。请在地图编辑器里重新保存地图。";
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("清除全图皮肤"))
            {
                SkinPainter.ClearAll();
                status = "已清除全图皮肤（可 Ctrl+Z 撤销）。";
            }
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // ================= 锚点体检 / 迁移（菜单入口） =================

    static List<MapGrid> CollectAllGrids()
    {
        var mm = MapManager.Instance;
        var grids = mm != null
            ? mm.GetComponentsInChildren<MapGrid>(true)
            : UnityEngine.Object.FindObjectsOfType<MapGrid>();
        return grids.Where(g => g != null).OrderBy(g => g.X).ThenBy(g => g.Y).ToList();
    }

    /// <summary>
    /// 只读体检：逐格逐层打印"皮肤原点 / 锚点 / 白膜顶面 / 皮肤包围盒 / 竖直偏差"，
    /// 用来量化"皮肤 GO 偏离原地块、主要在 Y 轴"到底偏多少、往哪个方向偏。
    /// 报告写到 Temp/SkinAnchorReport.txt（Temp 已被 .gitignore 忽略）。
    /// 不修改任何数据，可随时运行。
    /// </summary>
    [MenuItem("Tools/地图换皮/皮肤锚点体检（只读报告）")]
    static void AuditAnchors()
    {
        var grids = CollectAllGrids();
        var lines = new List<string>();
        var layerCountByAnchor = new Dictionary<SkinAnchor, int>();
        var prefabStat = new Dictionary<string, KeyValuePair<int, float>>(); // prefab -> (层数, Δ合计)
        int skinnedGrids = 0, totalLayers = 0, sunk = 0, floating = 0, okCount = 0;
        float maxAbsDelta = 0f;

        foreach (var g in grids)
        {
            var data = g.GetComponent<GridSkinData>();
            if (data == null) continue;
            var children = data.GetSkinChildren();
            if (children.Count == 0) continue;
            skinnedGrids++;

            var tile = data.FindTileModel();
            float tileLocalY = tile != null ? tile.transform.localPosition.y : 0f;
            float topLocalY = tile != null ? GridSkinData.GetTileTopLocalY(tile.transform, data.transform) : 0f;
            lines.Add($"== 格子 ({g.X},{g.Y})  地块={(data.IsHighTile ? "高台" : "地面")}  白膜={tile?.name ?? "(无)"}  白膜localY={tileLocalY:F3}  顶面localY={topLocalY:F3}  表面世界Y={data.GetTileSurfaceWorldY():F3}  地面平面世界Y={data.GetGroundPlaneWorldY():F3}");

            for (int layer = 0; layer < children.Count; layer++)
            {
                var info = data.GetSkinInfoAt(layer);
                if (info == null) continue;
                totalLayers++;
                int n; layerCountByAnchor.TryGetValue(info.Anchor, out n);
                layerCountByAnchor[info.Anchor] = n + 1;

                // 贴合偏差按地块类型规则算：地面块比"包围盒顶面 vs 白膜顶面"，高台块比"包围盒底面 vs 地面平面"
                float delta = data.GetAutoFitDelta(layer);
                if (Mathf.Abs(delta) <= 0.02f) okCount++;
                else if (delta < 0) sunk++;
                else floating++;
                if (Mathf.Abs(delta) > Mathf.Abs(maxAbsDelta)) maxAbsDelta = delta;

                var key = $"{info.SkinName} [{info.Anchor}]";
                int pn; float pd;
                if (prefabStat.TryGetValue(key, out var kv)) { pn = kv.Key; pd = kv.Value; } else { pn = 0; pd = 0f; }
                prefabStat[key] = new KeyValuePair<int, float>(pn + 1, pd + delta);

                var dxz = data.GetAutoFitDeltaXZ(layer);
                if (Mathf.Abs(dxz.x) > 0.02f || Mathf.Abs(dxz.y) > 0.02f)
                    lines.Add($"   [水平偏差] ΔXZ=({dxz.x:+0.000;-0.000}, {dxz.y:+0.000;-0.000}) —— 建议开「水平纠错」后重新贴合");
                lines.Add("   " + data.DescribeLayer(layer)
                    + $"  Δ={delta:+0.000;-0.000}{(Mathf.Abs(delta) <= 0.02f ? " (贴合)" : (delta < 0 ? " (下沉)" : " (浮空)"))}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("==== 皮肤锚点体检报告 ====");
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"FeatureFlags.SkinBrush = {FeatureFlags.SkinBrush}（关闭时本次会话不会还原皮肤，体检需在开启后于 Play 中运行）");
        sb.AppendLine($"有皮肤的格子：{skinnedGrids}    皮肤层总数：{totalLayers}");
        sb.AppendLine($"贴合(|Δ|<=0.02)：{okCount}    下沉：{sunk}    浮空：{floating}    最大偏差：{maxAbsDelta:+0.000;-0.000}");
        sb.AppendLine("锚点分布：");
        foreach (var kv in layerCountByAnchor)
            sb.AppendLine($"    {kv.Key} : {kv.Value} 层");
        sb.AppendLine("按 笔刷[锚点] 汇总（Δ = 按地块类型的贴合偏差，0=完美贴合 / 正=偏高 / 负=偏低）：");
        foreach (var kv in prefabStat.OrderByDescending(x => x.Value.Key))
            sb.AppendLine($"    {kv.Key} : {kv.Value.Key} 层, Δ平均 {(kv.Value.Key > 0 ? kv.Value.Value / kv.Value.Key : 0f):+0.000;-0.000}");
        sb.AppendLine();
        sb.AppendLine("---- 明细 ----");
        foreach (var l in lines) sb.AppendLine(l);

        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, "SkinAnchorReport.txt");
            System.IO.File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[皮肤体检] 已写出报告：{file}\n" + sb.ToString().Substring(0, Mathf.Min(1200, sb.Length)));
        }
        catch (Exception e)
        {
            Debug.LogError("[皮肤体检] 写报告失败：" + e);
        }
    }

    /// <summary>
    /// 锚点迁移：把选中格子（选中 MapGrid 或其皮肤子物体）或全图的皮肤层 Anchor 统一成新语义。
    /// 两种模式：
    ///   1) 保持外观 —— 同时按差值改写 Offset.y（纯重定基，画面不动，仅让数据语义统一）；
    ///   2) 只改语义 —— 不改 Offset，画面整体位移（把老图从"地块中心"抬到"格子表面"）。
    /// </summary>
    [MenuItem("Tools/地图换皮/皮肤锚点迁移")]
    static void MigrateAnchors()
    {
        var selGrid = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<MapGrid>() : null;
        var grids = selGrid != null ? new List<MapGrid> { selGrid } : CollectAllGrids();
        if (grids.Count == 0) { EditorUtility.DisplayDialog("皮肤锚点迁移", "场景里没有 MapGrid。", "好"); return; }

        var targets = grids
            .Select(g => new { g, d = g.GetComponent<GridSkinData>() })
            .Where(x => x.d != null && x.d.GetSkinChildren().Count > 0)
            .ToList();
        if (targets.Count == 0) { EditorUtility.DisplayDialog("皮肤锚点迁移", "选中范围内没有已换皮的格子。", "好"); return; }

        int choice = EditorUtility.DisplayDialogComplex("皮肤锚点迁移",
            $"范围：{(selGrid != null ? "选中格子 (" + selGrid.X + "," + selGrid.Y + ")" : "全图 " + targets.Count + " 格")}\n" +
            "目标锚点：TileSurface（格子可视表面 = 白膜顶面）\n\n" +
            "【保持外观】在改写 Anchor 的同时补偿 Offset.y，画面完全不动，只让数据语义统一（推荐先做）。\n" +
            "【只改语义】不改 Offset，皮肤会整体抬升到格子表面 —— 这才是修复\"皮肤下沉\"的那一步。",
            "保持外观", "取消", "只改语义");
        if (choice == 1) return;
        bool keepLook = choice == 0;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        int changed = 0;
        float minDy = float.MaxValue, maxDy = float.MinValue;

        foreach (var t in targets)
        {
            var data = t.d;
            Undo.RecordObject(data, "皮肤锚点迁移");
            int layers = data.SkinLayerCount;
            for (int layer = 0; layer < layers; layer++)
            {
                var info = data.GetSkinInfoAt(layer);
                if (info == null) continue;
                if (info.Anchor == SkinAnchor.TileSurface) continue;

                float dy = data.GetAnchorRebaseDeltaY(info.Anchor, SkinAnchor.TileSurface);
                info.Anchor = SkinAnchor.TileSurface;
                if (keepLook) info.Offset.y += dy;
                changed++;
                if (dy < minDy) minDy = dy;
                if (dy > maxDy) maxDy = dy;
            }
            EditorUtility.SetDirty(data);
            data.ApplySkinTransform();
        }
        Undo.CollapseUndoOperations(group);

        if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
        string msg = $"锚点迁移完成：{changed} 层 → TileSurface（{(keepLook ? "保持外观，Offset.y 补偿 " + minDy.ToString("+0.000;-0.000") + "~" + maxDy.ToString("+0.000;-0.000") : "视觉整体上移")}），可 Ctrl+Z 撤销。";
        Debug.Log("[皮肤锚点迁移] " + msg);
        EditorUtility.DisplayDialog("皮肤锚点迁移", msg, "好");
    }

    void PickBrushFromSelection()
    {
        var sel = Selection.activeGameObject;
        if (sel == null)
        {
            status = "请先在 Scene / Hierarchy 中选中一个物体。";
            return;
        }

        var src = PrefabUtility.GetCorrespondingObjectFromSource(sel) as GameObject;
        if (src != null && AssetDatabase.Contains(src))
        {
            brush = src;
            sceneBrushTemplate = null;
            status = $"笔刷：{src.name}（预制体资源，战斗内可还原）";
        }
        else if (sel.GetComponentInParent<MapGrid>() == null)
        {
            sceneBrushTemplate = sel;
            brush = null;
            status = $"笔刷：{sel.name}（场景模板，仅当前会话有效）";
        }
        else
        {
            status = "选中的是白膜本身。请选中要作为笔刷的模型物体（或已放置的皮肤）。";
        }
        Repaint();
    }

    void ReadPresetFromSelection()
    {
        var grid = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<MapGrid>()
            : null;
        var data = grid != null ? grid.GetComponent<GridSkinData>() : null;
        if (data == null || data.SkinLayerCount == 0)
        {
            status = "选中的格子没有皮肤，无法读取预设。";
            return;
        }
        int layer = Mathf.Clamp(selectedLayer, 0, data.SkinLayerCount - 1);
        var info = data.GetSkinInfoAt(layer);
        presetOffset = info.Offset;
        presetRotation = info.Rotation;
        presetScale = info.Scale == Vector3.zero ? Vector3.one : info.Scale;
        SaveV3("Offset", presetOffset);
        SaveV3("Rotation", presetRotation);
        SaveV3("Scale", presetScale);
        status = $"已读取格子 ({grid.X},{grid.Y}) 第 {layer} 层皮肤参数为预设。";
    }

    void BrowseFolder()
    {
        var abs = EditorUtility.OpenFolderPanel("选择皮肤 Prefab 文件夹", folder, "");
        if (string.IsNullOrEmpty(abs)) return;
        var rel = FileUtil.GetProjectRelativePath(abs);
        if (string.IsNullOrEmpty(rel) || !rel.StartsWith("Assets"))
        {
            status = "请选择项目 Assets 目录内的文件夹。";
            return;
        }
        folder = rel;
        RefreshList();
    }

    void RefreshList()
    {
        prefabs.Clear();
        if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
        {
            // 自动纳入文件夹内的 Prefab 与 FBX 模型（t:Prefab/t:Model 先过滤类型，避免空过滤器扫全量资产再逐个加载）
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Concat(AssetDatabase.FindAssets("t:Model", new[] { folder }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                            || p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
                .Where(p => p != null)
                .ToList();
        }
        EditorPrefs.SetString("SkinBrush.Folder", folder);
        Repaint();
    }

    static Vector3 LoadV3(string key, Vector3 def)
    {
        var s = EditorPrefs.GetString("SkinBrush.Preset." + key, "");
        if (string.IsNullOrEmpty(s)) return def;
        var v = s.Split(',');
        if (v.Length == 3
            && float.TryParse(v[0], out var x)
            && float.TryParse(v[1], out var y)
            && float.TryParse(v[2], out var z))
            return new Vector3(x, y, z);
        return def;
    }

    static void SaveV3(string key, Vector3 v)
    {
        EditorPrefs.SetString("SkinBrush.Preset." + key, v.x + "," + v.y + "," + v.z);
    }

    // ================= 特殊替换模式 =================

    /// <summary>
    /// 解析当前选中物体是否为可替换目标：
    /// - 皮肤层（挂在 MapGrid 下的 Skin_Model / Skin_Model_N）→ 返回格子、层数据
    /// - 白膜本身（tiles/MapGrid 根）→ 不可替换（机制载体）
    /// - 其他场景模型 → 通用替换（不涉及图节点）
    /// </summary>
    static MapGrid GetReplaceTargetContext(GameObject sel, out GridSkinData data, out int layer)
    {
        data = null;
        layer = -1;
        if (sel == null) return null;
        var grid = sel.GetComponentInParent<MapGrid>();
        if (grid == null) return null;

        data = grid.GetComponent<GridSkinData>();
        if (data != null)
        {
            var children = data.GetSkinChildren();
            // 皮肤层（或其子物体）→ 可替换；白膜根 / tiles 模型 → layer 保持 -1，不可替换
            layer = children.FindIndex(c => c != null && (sel == c || sel.transform.IsChildOf(c.transform)));
        }
        return grid;
    }

    void DrawReplacePanel()
    {
        EditorGUILayout.Space(2);
        var sel = Selection.activeGameObject;
        if (sel == null)
        {
            EditorGUILayout.HelpBox("在 Scene 视图中点击选中要替换的模型（皮肤模型为黄色线框高亮）。", MessageType.None);
            return;
        }

        var grid = GetReplaceTargetContext(sel, out var data, out int layer);
        if (grid != null && layer >= 0)
        {
            var info = data.GetSkinInfoAt(layer);
            EditorGUILayout.LabelField("替换目标", $"格子 ({grid.X},{grid.Y}) 第 {layer} 层：{info?.SkinName ?? sel.name}");
        }
        else if (grid != null)
        {
            EditorGUILayout.HelpBox("选中的是白膜/格子本体（机制载体，不可替换）。请选中其皮肤模型 Skin_Model。", MessageType.Warning);
            return;
        }
        else
        {
            EditorGUILayout.LabelField("替换目标", $"场景物体：{sel.name}（非格子模型，做通用替换，保留父级与变换）");
        }

        if (brush == null && sceneBrushTemplate == null)
        {
            EditorGUILayout.HelpBox("请先在上方选择替换用的笔刷模型。", MessageType.Warning);
            return;
        }

        if (GUILayout.Button($"用「{(brush != null ? brush.name : sceneBrushTemplate.name)}」替换选中模型"))
        {
            if (grid != null && layer >= 0)
            {
                SkinPainter.ReplaceSkinModel(grid, layer, brush, sceneBrushTemplate, stripColliders);
                status = $"已替换格子 ({grid.X},{grid.Y}) 第 {layer} 层模型（可 Ctrl+Z 撤销）。";
            }
            else
            {
                SkinPainter.ReplaceSceneObject(sel, brush, sceneBrushTemplate, stripColliders);
                status = $"已替换场景物体 {sel.name}（可 Ctrl+Z 撤销）。";
            }
            if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
            Repaint();
        }
    }

    void DrawReplaceHighlight()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return;
        var grid = GetReplaceTargetContext(sel, out _, out int layer);
        if (grid == null || layer < 0) return; // 仅高亮可替换的皮肤模型

        var r = sel.GetComponentInChildren<Renderer>();
        if (r == null) return;

        // 黄色 AABB 线框高亮（区别于 Unity 原生选中轮廓），端点写入静态缓存避免每帧分配
        var b = r.bounds;
        var oldColor = Handles.color;
        Handles.color = new Color(1f, 0.85f, 0.1f);
        var p0 = b.min; var p1 = b.max;
        var c = s_HighlightCorners;
        c[0]  = new Vector3(p0.x,p0.y,p0.z); c[1]  = new Vector3(p1.x,p0.y,p0.z);
        c[2]  = new Vector3(p1.x,p0.y,p0.z); c[3]  = new Vector3(p1.x,p0.y,p1.z);
        c[4]  = new Vector3(p1.x,p0.y,p1.z); c[5]  = new Vector3(p0.x,p0.y,p1.z);
        c[6]  = new Vector3(p0.x,p0.y,p1.z); c[7]  = new Vector3(p0.x,p0.y,p0.z);
        c[8]  = new Vector3(p0.x,p1.y,p0.z); c[9]  = new Vector3(p1.x,p1.y,p0.z);
        c[10] = new Vector3(p1.x,p1.y,p0.z); c[11] = new Vector3(p1.x,p1.y,p1.z);
        c[12] = new Vector3(p1.x,p1.y,p1.z); c[13] = new Vector3(p0.x,p1.y,p1.z);
        c[14] = new Vector3(p0.x,p1.y,p1.z); c[15] = new Vector3(p0.x,p1.y,p0.z);
        c[16] = new Vector3(p0.x,p0.y,p0.z); c[17] = new Vector3(p0.x,p1.y,p0.z);
        c[18] = new Vector3(p1.x,p0.y,p0.z); c[19] = new Vector3(p1.x,p1.y,p0.z);
        c[20] = new Vector3(p1.x,p0.y,p1.z); c[21] = new Vector3(p1.x,p1.y,p1.z);
        c[22] = new Vector3(p0.x,p0.y,p1.z); c[23] = new Vector3(p0.x,p1.y,p1.z);
        Handles.DrawLines(c);
        Handles.color = oldColor;
        Handles.Label(b.center + Vector3.up * (b.extents.y + 0.3f),
            $"可替换：({grid.X},{grid.Y}) 第 {layer} 层");
    }

    // ================= 微调面板（支持多层） =================

    /// <summary>微调面板异常信息（渲染异常会冻结 IMGUI 输入，必须显式暴露而不是静默）</summary>
    string panelError;

    void DrawAdjustPanel()
    {
        if (panelError != null)
        {
            EditorGUILayout.HelpBox($"微调面板渲染异常（输入会失效）：{panelError}\n完整堆栈见 Console。通常由场景状态与皮肤数据不一致引起，可尝试重刷该格皮肤。", MessageType.Error);
            return;
        }
        try
        {
            DrawAdjustPanelInner();
        }
        catch (Exception ex)
        {
            // IMGUI 每帧重绘中抛异常会让面板控件冻结（表现为无法输入/点击），
            // 记录一次并显式提示，避免静默失效
            panelError = ex.Message;
            Debug.LogException(ex);
            Repaint();
        }
    }

    void DrawAdjustPanelInner()
    {
        var grid = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<MapGrid>()
            : null;
        if (grid == null)
        {
            EditorGUILayout.HelpBox("在 Scene / Hierarchy 中选中一个白膜或其皮肤子物体。", MessageType.None);
            return;
        }

        var data = grid.GetComponent<GridSkinData>();
        var children = data != null ? data.GetSkinChildren() : null;
        if (data == null || children == null || children.Count == 0)
        {
            EditorGUILayout.HelpBox($"格子 ({grid.X}, {grid.Y}) 尚未放置皮肤。", MessageType.None);
            return;
        }

        // 撤销/重做残留自愈：数据与实际子物体对齐后再进入调整
        if (SkinPainter.NeedsReconcile(data))
        {
            Undo.RecordObject(data, "整理皮肤层");
            SkinPainter.Reconcile(data);
            children = data.GetSkinChildren();
        }
        // 注：子物体实际变换→数据的同步已移到 OnSelectionChanged/OnUndoRedo 事件中执行，
        // 不在每帧 OnGUI 里跑——否则会与字段输入同帧竞争（输入被立即打回旧值）

        selectedLayer = Mathf.Clamp(selectedLayer, 0, children.Count - 1);

        if (children.Count > 1)
        {
            var options = children.Select((c, i) =>
            {
                var info = data.GetSkinInfoAt(i);
                return $"{i}: {info?.SkinName ?? "(?)"}";
            }).ToArray();
            selectedLayer = EditorGUILayout.Popup("调整层", selectedLayer, options);
        }

        var skin = children[selectedLayer];
        var infoAt = data.GetSkinInfoAt(selectedLayer);
        if (skin == null || infoAt == null)
        {
            EditorGUILayout.HelpBox("该层皮肤数据异常，请重新刷皮。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("格子", $"({grid.X}, {grid.Y})    第 {selectedLayer} 层：{infoAt.SkinName}    隐藏白膜：{(data.Data.TileHidden ? "是" : "否")}");

        EditorGUI.BeginChangeCheck();
        var offset = EditorGUILayout.Vector3Field("位置偏移（局部）", infoAt.Offset);
        var rot = EditorGUILayout.Vector3Field("旋转角度（局部）", infoAt.Rotation);
        var scale = EditorGUILayout.Vector3Field("缩放倍率", infoAt.Scale);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(skin.transform, "调整皮肤");
            Undo.RecordObject(data, "调整皮肤");
            infoAt.Offset = offset;
            infoAt.Rotation = rot;
            infoAt.Scale = scale;
            data.ApplySkinTransformAt(selectedLayer);
            EditorUtility.SetDirty(data);
            // 改参立即进存档数据（否则保存的是旧参数）
            if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("重置变换"))
            {
                Undo.RecordObject(skin.transform, "重置皮肤变换");
                Undo.RecordObject(data, "重置皮肤变换");
                infoAt.Offset = Vector3.zero;
                infoAt.Rotation = Vector3.zero;
                infoAt.Scale = Vector3.one;
                data.ApplySkinTransformAt(selectedLayer);
                EditorUtility.SetDirty(data);
            }
            if (selectedLayer > 0 && GUILayout.Button("移除该层"))
            {
                Undo.RecordObject(data, "移除皮肤层");
                Undo.DestroyObjectImmediate(skin);
                data.ExtraSkins.RemoveAt(selectedLayer - 1);
                EditorUtility.SetDirty(data);
                selectedLayer = 0;
                if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
            }
            if (GUILayout.Button("清除该格皮肤"))
            {
                SkinPainter.Clear(grid);
                if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("修复尺寸", "按原始模型重新捕获变换基准，修正被压缩/拉伸的皮肤")))
            {
                var n = SkinPainter.FixSkinBaseTransforms(grid);
                status = n > 0 ? $"已修复 {n} 层皮肤的尺寸基准（可 Ctrl+Z 撤销）。" : "没有可修复的皮肤层（或找不到原始模型）。";
            }
            if (GUILayout.Button(new GUIContent("校验尺寸", "对比皮肤与原始模型的尺寸，确认未压缩/拉伸")))
            {
                status = SkinPainter.ValidateSkinSize(grid, selectedLayer);
            }
            if (GUILayout.Button(new GUIContent("全图修复", "对全图所有皮肤层执行尺寸基准修复"), GUILayout.Width(72)))
            {
                var n = SkinPainter.FixSkinBaseTransforms(null);
                status = n > 0 ? $"全图已修复 {n} 层皮肤的尺寸基准（可 Ctrl+Z 撤销）。" : "全图没有可修复的皮肤层。";
            }
        }
    }

    // ================= Scene 视图 =================

    void OnSceneGUI(SceneView sv)
    {
        if (adjustMode)
        {
            DrawAdjustHandles();
            return;
        }
        if (deleteMode)
        {
            HandleDeleteStroke();
            return;
        }
        // 特殊替换模式：不占用鼠标（保留 Unity 原生点击选择，皮肤模型可被 mesh 拾取选中），
        // 仅对当前可替换目标绘制高亮
        if (replaceMode)
        {
            DrawReplaceHighlight();
            return;
        }
        // 初始化模式不需要笔刷；其他模式需要选好笔刷
        if (!initMode && brush == null && sceneBrushTemplate == null) return;

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        // 占住默认控制：绘制模式下左键不再触发 Hierarchy 选择，避免误改 Selection
        HandleUtility.AddDefaultControl(id);

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && !e.alt)
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                    BeginStroke();
                    PaintAt(e.mousePosition);
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0 && GUIUtility.hotControl == id)
                {
                    e.Use();
                    PaintAt(e.mousePosition);
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                    EndStroke();
                }
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Escape && strokeUndoGroup >= 0)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                    EndStroke();
                }
                break;
        }
    }

    void BeginStroke()
    {
        paintedThisStroke.Clear();
        Undo.IncrementCurrentGroup();
        strokeUndoGroup = Undo.GetCurrentGroup();
    }

    void EndStroke()
    {
        if (strokeUndoGroup >= 0)
        {
            // 整笔拖动合并为一次撤销
            Undo.CollapseUndoOperations(strokeUndoGroup);
            strokeUndoGroup = -1;
        }
        paintedThisStroke.Clear();
        // 运行中的地图编辑器：自动把皮肤写入 MapInfo（存档数据）
        if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
    }

    // ---------- 删除模式（编辑器级拾取，不走物理射线——皮肤无碰撞体也能点中） ----------

    void HandleDeleteStroke()
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        // 占住默认控制：删除模式下左键不再触发 Hierarchy 选择
        HandleUtility.AddDefaultControl(id);

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && !e.alt)
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                    BeginStroke();
                    DeleteAt(e.mousePosition);
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0 && GUIUtility.hotControl == id)
                {
                    e.Use();
                    DeleteAt(e.mousePosition);
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                    EndStroke();
                }
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Escape && strokeUndoGroup >= 0)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                    EndStroke();
                }
                break;
        }
    }

    /// <summary>
    /// 删除模式：按渲染结果拾取命中物体（HandleUtility.PickGameObject，无 collider 限制）。
    /// 分类处理——
    /// 皮肤子物体（含其内部任意后代节点，按 IsChildOf 归属层）：
    ///   数据与实例一起删（只删实例会被"以数据为真值"的保存链路复活）；
    ///   主皮肤层：只删主层本身；若有叠加层，首个叠加层升为主层（配对规则保持）；
    ///   无叠加层时等价清整格（SkinPainter.Clear）。白膜/格子本体：保护跳过；
    /// 其他自由物体（装饰等）：直接 Undo 删除。
    /// 注意：PickGameObject 返回最深的渲染节点，其名字可能不带 Skin_Model 前缀
    /// （如皮肤 prefab 内部的 mesh 节点），所以不能用 IsSkinChild(名字) 判定，
    /// 必须按后代关系找所属皮肤层（与 GetReplaceTargetContext 同一套口径）。
    /// </summary>
    void DeleteAt(Vector2 mousePos)
    {
        var go = HandleUtility.PickGameObject(mousePos, false);
        if (go == null) return;

        var grid = go.GetComponentInParent<MapGrid>();
        if (grid != null)
        {
            // 格子本体：地基本体，绝不删
            if (go == grid.gameObject)
            {
                status = "跳过：格子本体不可删除";
                return;
            }

            var data = grid.GetComponent<GridSkinData>();

            // 皮肤层判定：拾取节点自身或其任意祖先是皮肤子物体 → 归属该层
            int skinLayer = -1;
            var children = data != null ? data.GetSkinChildren() : null;
            if (children != null)
            {
                skinLayer = children.FindIndex(c => c != null && (go == c || go.transform.IsChildOf(c.transform)));
            }

            if (skinLayer >= 0)
            {
                Undo.RecordObject(data, "删除皮肤层");
                if (skinLayer > 0)
                {
                    // 叠加层：数据与实例一起删
                    Undo.DestroyObjectImmediate(children[skinLayer]);
                    data.ExtraSkins.RemoveAt(skinLayer - 1);
                    status = $"已删除叠加层 {skinLayer}：{grid.name}";
                }
                else
                {
                    // 主皮肤层：只删主层本身，不牵连上方叠加层
                    if (data.ExtraSkins.Count > 0 && children.Count > 1)
                    {
                        // 白膜显隐跟"格子里是否还有皮肤"走：升主后仍有皮肤层 → 不恢复白膜。
                        // 隐藏状态记账搬进升主的数据（原叠加层记的 TileHidden=false 不可直接沿用）
                        bool tileWasHidden = data.Data != null && data.Data.TileHidden;

                        // 先销毁旧主层实例（否则格子里出现两个 Skin_Model，
                        // 子物体数与数据脱节 → 组合模式触发 Reconcile 把配对搅乱）
                        Undo.DestroyObjectImmediate(children[0]);

                        // 首个叠加层升为主层：数据搬进 Data、子物体改名，
                        // 保持层配对规则（child[0] ↔ Data，child[i] ↔ ExtraSkins[i-1]）不乱
                        var promoted = children[1];
                        data.Data = data.ExtraSkins[0];
                        data.ExtraSkins.RemoveAt(0);
                        Undo.RecordObject(promoted, "提升为主皮肤");
                        promoted.name = GridSkinData.SkinChildName;
                        if (data.Data != null) data.Data.TileHidden = tileWasHidden;
                        status = $"已删除主皮肤层（原叠加层 1 升为主层）：{grid.name}";
                    }
                    else
                    {
                        // 没有叠加层：等价清整格（连带移除空数据组件，可撤销）；
                        // 格子已无皮肤 → Clear 内部按 TileHidden 记账恢复白膜
                        SkinPainter.Clear(grid);
                        status = $"已清除整格皮肤：{grid.name}";
                        return;
                    }
                }
                EditorUtility.SetDirty(data);
                return;
            }

            // 白膜：数据地基，绝不删
            var tile = data != null ? data.FindTileModel() : null;
            if (tile != null && (go == tile || go.transform.IsChildOf(tile.transform)))
            {
                status = "跳过：白膜不可删除（地图地基）";
                return;
            }
        }

        // 自由物体（场景装饰等）
        string freeName = go.name;   // 先取名字再销毁：销毁后访问 go.name 会抛 MissingReferenceException
        Undo.DestroyObjectImmediate(go);
        status = $"已删除自由物体：{freeName}";
    }

    void PaintAt(Vector2 mousePos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        if (!Physics.Raycast(ray, out var hit, Mathf.Infinity)) return;

        var grid = hit.collider.GetComponentInParent<MapGrid>();
        if (grid == null) return;
        if (paintedThisStroke.Contains(grid)) return;   // 同一笔内同一格子去重
        paintedThisStroke.Add(grid);

        // 优先级：初始化模式 > 组合模式 > 默认替换
        if (initMode)
        {
            if (grid.GetComponent<GridSkinData>() == null) return;
            SkinPainter.Clear(grid);
            status = $"已初始化（清空皮肤）：{paintedThisStroke.Count} 格";
            return;
        }

        if (combineMode)
        {
            if (SkinPainter.HasAnySkinLayer(grid, brush)) return; // 该格已有同款皮肤层则跳过
            SkinPainter.CombinePaint(grid, brush, sceneBrushTemplate, stripColliders, hideOriginal,
                presetOffset, presetRotation, presetScale, brushAnchor, autoFit, autoFitXZ);
            status = AnchorStatus($"已叠加皮肤 {paintedThisStroke.Count} 格", grid);
            return;
        }

        if (SkinPainter.HasSameSkin(grid, brush, hideOriginal)) return;
        SkinPainter.Paint(grid, brush, sceneBrushTemplate, stripColliders, hideOriginal,
            presetOffset, presetRotation, presetScale, brushAnchor, autoFit, autoFitXZ);
        status = AnchorStatus($"已换皮 {paintedThisStroke.Count} 格", grid);
    }

    /// <summary>
    /// 落点公式实时显示：皮肤位置 = 白膜位置 + 模型基准 + 预设偏移。
    /// 锚点恒为白膜；画面出现偏移时看这两个加数哪个非零——
    /// 预设偏移非零 = 笔刷面板残留预设（点"重置"归零）；
    /// 模型基准非零 = 笔刷 prefab 根自带 localPosition（prefab 没归零）。
    /// </summary>
    string AnchorStatus(string prefix, MapGrid grid)
    {
        var data = grid != null ? grid.GetComponent<GridSkinData>() : null;
        var last = data != null && data.SkinLayerCount > 0 ? data.GetSkinInfoAt(data.SkinLayerCount - 1) : null;
        if (last == null) return prefix;
        return $"{prefix} | 锚点=白膜 + 模型基准{last.GetBaseLocalPos()} + 预设偏移{last.Offset}";
    }

    void DrawAdjustHandles()
    {
        var grid = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<MapGrid>()
            : null;
        if (grid == null) return;

        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return;
        var children = data.GetSkinChildren();
        if (children.Count == 0) return;

        int layer = Mathf.Clamp(selectedLayer, 0, children.Count - 1);
        var skin = children[layer];
        if (skin == null) return;

        Handles.Label(skin.transform.position + Vector3.up * 0.6f,
            $"皮肤微调：({grid.X},{grid.Y}) 第 {layer} 层");

        // 位置手柄：只改皮肤子物体的世界位置，白膜根节点与碰撞体不动
        EditorGUI.BeginChangeCheck();
        var pos = Handles.PositionHandle(skin.transform.position, skin.transform.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(skin.transform, "移动皮肤");
            Undo.RecordObject(data, "移动皮肤");
            skin.transform.position = pos;
            SkinPainter.SyncTransformToData(data, layer);
            // v3.5：与面板输入路径对齐——运行中的编辑器会话立即同步进 MapInfo，
            // 否则手柄调整只落在 GridSkinData，进战斗/保存前读 MapInfo 的路径全是旧值
            if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
        }

        // 旋转手柄
        EditorGUI.BeginChangeCheck();
        var rot = Handles.FreeRotateHandle(skin.transform.rotation, skin.transform.position,
            HandleUtility.GetHandleSize(skin.transform.position) * 0.6f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(skin.transform, "旋转皮肤");
            Undo.RecordObject(data, "旋转皮肤");
            skin.transform.rotation = rot;
            SkinPainter.SyncTransformToData(data, layer);
            if (Application.isPlaying) SkinPainter.TrySyncToMapInfo();
        }
    }
}

/// <summary>
/// 换皮操作核心（含 Undo 登记）。所有方法只做视觉层操作：
/// 按需隐藏白膜 Renderer（保留 Collider）+ 挂皮肤子物体 + 写 GridSkinData。
/// </summary>
static class SkinPainter
{
    // ---------- 实例化辅助 ----------

    static GameObject InstantiateSkin(Transform parent, GameObject brushAsset, GameObject sceneTemplate)
    {
        if (brushAsset != null)
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(brushAsset, parent);
            if (s == null)
                s = (GameObject)UnityEngine.Object.Instantiate(brushAsset, parent, false);
            return s;
        }
        return (GameObject)UnityEngine.Object.Instantiate(sceneTemplate, parent, false);
    }

    static SkinInfo BuildInfo(GameObject brushAsset, GameObject sceneTemplate, Transform instance,
        Vector3 offset, Vector3 rotation, Vector3 scale, bool tileHidden, SkinAnchor anchor, bool autoFit, bool autoFitXZ)
    {
        var path = brushAsset != null ? AssetDatabase.GetAssetPath(brushAsset) : null;
        var info = new SkinInfo
        {
            SkinGuid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path),
            SkinAddress = ResolveAddress(path),
            SkinName = brushAsset != null ? brushAsset.name : (sceneTemplate != null ? sceneTemplate.name : ""),
            Offset = offset,
            Rotation = rotation,
            Scale = scale,
            TileHidden = tileHidden,
            Anchor = anchor,
            AutoFit = autoFit,
            AutoFitXZ = autoFitXZ,
        };
        // 记录模型自带的局部变换作为基准：应用时以"基准 + 用户增量"计算，
        // 不再套用白膜的 localScale（tiles_ground 自带 y=0.6，是大型模型被压扁的根因）
        info.CaptureBaseTransform(instance);
        return info;
    }

    /// <summary>编辑器侧翻转白膜显隐（只为状态确实变化的 renderer 登记 Undo，Paint/Clear 共用）</summary>
    internal static void SetTileRenderers(GridSkinData data, bool visible, string undoName)
    {
        var tile = data.FindTileModel();
        if (tile == null) return;
        foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (r.enabled == visible) continue;
            Undo.RecordObject(r, undoName);
            r.enabled = visible;
        }
    }

    // ---------- 绘制（默认替换主皮肤） ----------

    public static void Paint(MapGrid grid, GameObject brushAsset, GameObject sceneTemplate,
        bool stripColliders, bool hideOriginal,
        Vector3 offset, Vector3 rotation, Vector3 scale, SkinAnchor anchor, bool autoFit, bool autoFitXZ)
    {
        var root = grid.gameObject;

        var data = root.GetComponent<GridSkinData>();
        if (data == null)
            data = Undo.AddComponent<GridSkinData>(root);
        else
            Undo.RecordObject(data, "换皮");

        // 1) 清除旧皮肤（含叠加层），撤销时可恢复
        foreach (var old in data.GetSkinChildren())
            Undo.DestroyObjectImmediate(old);
        data.ExtraSkins.Clear();

        // 2) 白膜显示状态按开关设置（仅在状态确实改变时登记 Undo）
        data.Data.TileHidden = hideOriginal;
        SetTileRenderers(data, !hideOriginal, hideOriginal ? "隐藏白膜" : "显示白膜");

        // 3) 实例化新皮肤为白膜子物体
        var skin = InstantiateSkin(root.transform, brushAsset, sceneTemplate);
        Undo.RegisterCreatedObjectUndo(skin, "换皮");

        if (stripColliders)
            foreach (var c in skin.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(c);

        // 4) 写入数据（含预设参数、模型原始变换基准与 Addressables 地址）
        data.Data = BuildInfo(brushAsset, sceneTemplate, skin.transform, offset, rotation, scale, hideOriginal, anchor, autoFit, autoFitXZ);

        // 5) 编辑器侧手动配置主皮肤（与运行时 AttachSkin 一致，但保留 undo 登记）
        skin.name = GridSkinData.SkinChildName;
        skin.transform.SetSiblingIndex(Mathf.Min(1, root.transform.childCount - 1));
        data.ApplySkinTransform();
        // Y 贴合与 XZ 水平纠错任一开启都要重算（RecomputeAutoFit 内部按各自的 flag 执行）
        if (autoFit || autoFitXZ) data.RecomputeAutoFit(0);
        EditorUtility.SetDirty(data);
    }

    // ---------- 组合模式（叠加新层） ----------

    public static void CombinePaint(MapGrid grid, GameObject brushAsset, GameObject sceneTemplate,
        bool stripColliders, bool hideOriginal,
        Vector3 offset, Vector3 rotation, Vector3 scale, SkinAnchor anchor, bool autoFit, bool autoFitXZ)
    {
        var root = grid.gameObject;
        var data = root.GetComponent<GridSkinData>();

        // 无主皮肤：退化为普通换皮（首层换皮受 hideOriginal 控制）
        if (data == null || data.GetSkinChild() == null)
        {
            Paint(grid, brushAsset, sceneTemplate, stripColliders, hideOriginal, offset, rotation, scale, anchor, autoFit, autoFitXZ);
            return;
        }

        Undo.RecordObject(data, "叠加皮肤");

        // 撤销/重做后数据与子物体可能脱节：先按实际子物体自愈整理，
        // 再用存储数据复位全部已有层（修正可能的坐标漂移，保证新层基准正确）
        if (NeedsReconcile(data)) Reconcile(data);
        var existing = data.GetSkinChildren();
        for (int i = 0; i < existing.Count; i++)
            data.ApplySkinTransformAt(i);

        int layer = existing.Count; // 新层 = 当前层总数（落在兄弟顺序末尾）

        var skin = InstantiateSkin(root.transform, brushAsset, sceneTemplate);
        Undo.RegisterCreatedObjectUndo(skin, "叠加皮肤");

        if (stripColliders)
            foreach (var c in skin.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(c);

        var info = BuildInfo(brushAsset, sceneTemplate, skin.transform, offset, rotation, scale, tileHidden: false, anchor: anchor, autoFit: autoFit, autoFitXZ: autoFitXZ);
        data.ExtraSkins.Add(info);

        // 配置层级（命名/置顶/sortingOrder），层序由兄弟节点顺序保证
        data.ConfigureExtra(skin, layer);
        data.ApplySkinTransformAt(layer);
        if (autoFit || autoFitXZ) data.RecomputeAutoFit(layer);
        EditorUtility.SetDirty(data);
    }

    /// <summary>数据层条数与实际皮肤子物体是否脱节（撤销/重做残留检测）</summary>
    public static bool NeedsReconcile(GridSkinData data)
    {
        return data.ExtraSkins.Count != Mathf.Max(0, data.GetSkinChildren().Count - 1);
    }

    /// <summary>
    /// 把 ExtraSkins 重建为与实际皮肤子物体一一对应（撤销/重做后数据与场景脱节时的自愈）：
    /// 缺数据、或数据与子物体 Prefab 源不匹配的层，按其 Prefab 源重建信息，
    /// 并以该子物体当前变换为基准反推偏移/旋转/缩放（杜绝坐标漂移）。
    /// 注意：调用前必须已完成 Undo.RecordObject(data) 登记。
    /// </summary>
    public static void Reconcile(GridSkinData data)
    {
        var children = data.GetSkinChildren();
        var list = new List<SkinInfo>();

        for (int i = 1; i < children.Count; i++)
        {
            var child = children[i];
            var info = i - 1 < data.ExtraSkins.Count ? data.ExtraSkins[i - 1] : null;

            // 判定该层数据是否可信：缺失、或与子物体的 Prefab 源不符（撤销部分恢复的典型残留）
            bool stale = info == null || string.IsNullOrEmpty(info.SkinName);
            if (!stale)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(child) as GameObject;
                if (src != null && src.name != info.SkinName) stale = true;
            }

            if (stale)
            {
                // 新补出来的层沿用本格主皮肤的锚点语义（老图= TileCenter，新图= TileSurface），
                // 否则会按枚举默认值 0(TileCenter) 摆放，与新图整体错位 0.3。
                info = new SkinInfo { Anchor = data.Data != null ? data.Data.Anchor : SkinAnchor.TileCenter };
                var src = PrefabUtility.GetCorrespondingObjectFromSource(child) as GameObject;
                if (src != null && AssetDatabase.Contains(src))
                {
                    var path = AssetDatabase.GetAssetPath(src);
                    info.SkinGuid = AssetDatabase.AssetPathToGUID(path);
                    info.SkinAddress = ResolveAddress(path);
                    info.SkinName = src.name;
                }
                else
                {
                    info.SkinName = child.name; // 场景模板等无源物体：仅记名（无法运行时还原）
                }
                // 以子物体当前变换为基准反推：基准取"当前变换剥离白膜对齐分量"，
                // 用户增量归零 —— 之后应用变换恢复出的就是当前外观（不会漂移）
                var t = child.transform;
                info.BaseLocalPos = t.localPosition - data.ResolveAnchorLocal(info);
                info.BaseRotation = t.localEulerAngles;
                info.BaseScale = t.localScale;
                info.BaseCaptured = true;
                info.Offset = Vector3.zero;
                info.Rotation = Vector3.zero;
                info.Scale = Vector3.one;
            }
            list.Add(info);
        }
        data.ExtraSkins = list;
        EditorUtility.SetDirty(data);
    }

    /// <summary>
    /// 解析 Prefab 的 Addressables 地址；未注册的资源自动加入 Default Local Group，
    /// 地址遵循项目惯例（资源路径去扩展名），保证战斗内 ResHelper.Instantiate 可按地址加载。
    /// </summary>
    static string ResolveAddress(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets")) return null;
        try
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            if (entry != null) return entry.address;

            var group = settings.groups.FirstOrDefault(g => g != null && g.Name == "Default Local Group")
                        ?? settings.DefaultGroup;
            if (group == null) return null;

            entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = System.IO.Path.ChangeExtension(assetPath, null);
            settings.SetDirty(
                UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ModificationEvent.EntryAdded,
                entry, true);
            QueueAddressableSave(); // 延迟合并保存：批量刷皮时不逐个全量 SaveAssets
            Debug.Log($"[SkinBrush] 已自动注册 Addressable：{assetPath} → 地址 {entry.address}");
            return entry.address;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SkinBrush] 解析/注册 Addressables 地址失败（{assetPath}）：{e.Message}");
            return null;
        }
    }

    static bool _addressableSavePending;

    /// <summary>延迟合并保存 Addressables 设置：同一帧/同一笔内的多次注册只触发一次全量保存</summary>
    static void QueueAddressableSave()
    {
        if (_addressableSavePending) return;
        _addressableSavePending = true;
        EditorApplication.delayCall += () =>
        {
            _addressableSavePending = false;
            AssetDatabase.SaveAssets();
        };
    }

    // ---------- 清除 ----------

    /// <summary>清空该格全部皮肤（主 + 叠加层）并按需恢复白膜显示</summary>
    public static void Clear(MapGrid grid)
    {
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return;

        foreach (var skin in data.GetSkinChildren())
            Undo.DestroyObjectImmediate(skin);

        // 只有笔刷隐藏过的白膜才恢复显示
        if (data.Data.TileHidden)
            SetTileRenderers(data, true, "恢复白膜");

        // 数据组件一并移除（撤销可恢复）
        Undo.DestroyObjectImmediate(data);
    }

    public static void ClearAll()
    {
        var mm = MapManager.Instance;
        var grids = mm != null
            ? mm.GetComponentsInChildren<MapGrid>(true)
            : UnityEngine.Object.FindObjectsOfType<MapGrid>();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        int count = 0;
        foreach (var g in grids)
        {
            if (g.GetComponent<GridSkinData>() == null) continue;
            Clear(g);
            count++;
        }
        Undo.CollapseUndoOperations(group); // 全图清除 = 一次撤销

        if (count > 0 && Application.isPlaying) TrySyncToMapInfo();
    }

    // ---------- 辅助 ----------

    /// <summary>该格主皮肤已是当前笔刷同款 Prefab 且隐藏设置一致时返回 true（拖动时跳过，避免反复重建）</summary>
    public static bool HasSameSkin(MapGrid grid, GameObject brushAsset, bool hideOriginal)
    {
        if (brushAsset == null) return false; // 场景模板笔刷总是重画
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return false;
        var skin = data.GetSkinChild();
        if (skin == null) return false;
        if (data.Data.TileHidden != hideOriginal) return false;
        var src = PrefabUtility.GetCorrespondingObjectFromSource(skin);
        return src == brushAsset;
    }

    /// <summary>该格任意层皮肤已是当前笔刷同款 Prefab 时返回 true（组合模式拖动时跳过，避免重复叠加）</summary>
    public static bool HasAnySkinLayer(MapGrid grid, GameObject brushAsset)
    {
        if (brushAsset == null) return false;
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return false;
        foreach (var skin in data.GetSkinChildren())
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(skin);
            if (src == brushAsset) return true;
        }
        return false;
    }

    /// <summary>
    /// 把指定层皮肤当前局部变换同步回数据（增量 = 当前变换相对"白膜位置 + 模型原始基准"的差值）。
    /// 缩放增量 = 当前缩放 / 模型原始缩放，因此不会把模型自带缩放（FBX 常见 0.01/100）算进倍率。
    /// </summary>
    public static void SyncTransformToData(GridSkinData data, int layer)
    {
        var skin = data.GetSkinChildAt(layer);
        var info = data.GetSkinInfoAt(layer);
        if (skin == null || info == null) return;

        // 锚点按该层自身的 Anchor 解析（TileCenter 老数据仍是白膜 localPosition，行为不变）
        info.Offset = skin.transform.localPosition - data.ResolveAnchorLocal(info) - info.GetBaseLocalPos()
            - info.GetFitOffset();
        info.Rotation = (Quaternion.Inverse(Quaternion.Euler(info.GetBaseRotation())) * skin.transform.localRotation).eulerAngles;

        var baseScale = info.GetBaseScale();
        var ls = skin.transform.localScale;
        info.Scale = new Vector3(
            baseScale.x != 0 ? ls.x / baseScale.x : 1,
            baseScale.y != 0 ? ls.y / baseScale.y : 1,
            baseScale.z != 0 ? ls.z / baseScale.z : 1);
        EditorUtility.SetDirty(data);
    }

    /// <summary>
    /// 双向同步入口：检测选中格子的每层皮肤"实际变换"与"数据期望值"是否一致，
    /// 不一致（用户用移动/旋转工具或 Inspector 直接摆过子物体）时以实际变换为准回写数据。
    /// 这样直接摆位置不会被后续 ApplySkinTransform 打回原形，面板数值也保持一致。
    /// </summary>
    public static void SyncTransformsFromChildren(GridSkinData data)
    {
        var children = data.GetSkinChildren();
        if (children.Count == 0) return;

        for (int layer = 0; layer < children.Count; layer++)
        {
            var info = data.GetSkinInfoAt(layer);
            if (info == null) continue;
            var t = children[layer].transform;

            Vector3 expPos = data.ResolveAnchorLocal(info) + info.GetBaseLocalPos() + info.Offset + info.GetFitOffset();
            Quaternion expRot = Quaternion.Euler(info.GetBaseRotation()) * Quaternion.Euler(info.Rotation);
            Vector3 expScale = Vector3.Scale(info.GetBaseScale(), info.GetScaleMultiplier());

            bool mismatch = (t.localPosition - expPos).sqrMagnitude > 1e-6f
                || Quaternion.Angle(t.localRotation, expRot) > 0.05f
                || (t.localScale - expScale).sqrMagnitude > 1e-6f;
            if (mismatch)
            {
                Undo.RecordObject(data, "同步皮肤变换");
                SyncTransformToData(data, layer);
            }
        }
    }

    // ---------- 特殊替换模式 ----------

    /// <summary>
    /// 替换指定格子的皮肤层模型：新模型继承旧模型的位置/旋转/缩放/兄弟层级/子物体
    /// （保留原图连接关系），并同步更新 GridSkinData 图节点数据。整次替换为一次撤销。
    /// </summary>
    public static void ReplaceSkinModel(MapGrid grid, int layer, GameObject brushAsset, GameObject sceneTemplate, bool stripColliders)
    {
        var data = grid.GetComponent<GridSkinData>();
        if (data == null) return;
        var children = data.GetSkinChildren();
        if (layer < 0 || layer >= children.Count) return;
        var old = children[layer];

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var skin = InstantiateSkin(grid.transform, brushAsset, sceneTemplate);
        Undo.RegisterCreatedObjectUndo(skin, "替换模型");

        // 保留原图连接关系：旧模型的子物体全部转移到新模型下（保持世界变换）。
        // 必须走 Undo.SetTransformParent——裸 SetParent 会让撤销销毁新模型时连带丢掉子物体
        for (int i = old.transform.childCount - 1; i >= 0; i--)
            Undo.SetTransformParent(old.transform.GetChild(i), skin.transform, "替换模型");

        if (stripColliders)
            foreach (var c in skin.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(c);

        // 层级顺序与命名：保持旧模型的兄弟位置（不置顶，层序 = 兄弟顺序）
        int sibling = old.transform.GetSiblingIndex();
        skin.name = old.transform.name == GridSkinData.SkinChildName && layer == 0
            ? GridSkinData.SkinChildName
            : old.transform.name;
        Undo.DestroyObjectImmediate(old);
        // 兄弟顺序序列化在父 Transform 的 m_Children 上：登记父级才能撤销层序变化
        Undo.RecordObject(grid.transform, "替换模型");
        skin.transform.SetSiblingIndex(sibling);

        // 恢复该层 sortingOrder（后叠加的显示在上层）
        foreach (var rd in skin.GetComponentsInChildren<Renderer>(true))
            if (rd is MeshRenderer || rd is SkinnedMeshRenderer)
                rd.sortingOrder = 10 * Mathf.Max(0, layer);

        // 更新图节点数据：资源标识换成新模型；基准换成新模型自带的局部变换，
        // 用户增量（偏移/旋转/倍率）沿用 —— 新模型按自身原始尺寸摆放，不继承旧模型的缩放
        Undo.RecordObject(data, "替换模型");
        var info = data.GetSkinInfoAt(layer);
        if (info != null)
        {
            var path = brushAsset != null ? AssetDatabase.GetAssetPath(brushAsset) : null;
            info.SkinGuid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
            info.SkinAddress = ResolveAddress(path);
            info.SkinName = brushAsset != null ? brushAsset.name
                : (sceneTemplate != null ? sceneTemplate.name : info.SkinName);
            info.CaptureBaseTransform(skin.transform);
        }
        data.ApplySkinTransformAt(layer); // 用新基准 + 沿用增量重新摆放（尺寸/形状完整保留）
        data.RecomputeAutoFit(layer);     // 新模型尺寸不同 → 按地块类型重新贴合
        EditorUtility.SetDirty(data);

        Undo.CollapseUndoOperations(group); // 替换 = 一次撤销（新模型创建 + 旧模型销毁 + 数据更新）
    }

    /// <summary>
    /// 通用场景物体替换（目标不在任何 MapGrid 下）：保留父级/变换/兄弟位/子物体，不涉及图节点。
    /// </summary>
    public static void ReplaceSceneObject(GameObject old, GameObject brushAsset, GameObject sceneTemplate, bool stripColliders)
    {
        if (old == null) return;
        var parent = old.transform.parent;
        if (parent == null) return; // 根级物体不替换，避免误操作场景根

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var skin = InstantiateSkin(parent, brushAsset, sceneTemplate);
        Undo.RegisterCreatedObjectUndo(skin, "替换模型");

        skin.transform.localPosition = old.transform.localPosition;
        skin.transform.localRotation = old.transform.localRotation;
        skin.transform.localScale = old.transform.localScale;

        // 子物体转移必须走 Undo.SetTransformParent，否则撤销会连带丢失（同 ReplaceSkinModel）
        for (int i = old.transform.childCount - 1; i >= 0; i--)
            Undo.SetTransformParent(old.transform.GetChild(i), skin.transform, "替换模型");

        if (stripColliders)
            foreach (var c in skin.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(c);

        skin.name = old.name;
        int sibling = old.transform.GetSiblingIndex();
        Undo.DestroyObjectImmediate(old);
        // 兄弟顺序登记父级后才能被撤销
        Undo.RecordObject(parent, "替换模型");
        skin.transform.SetSiblingIndex(sibling);

        Undo.CollapseUndoOperations(group);
    }

    // ---------- 尺寸修复与校验 ----------

    /// <summary>
    /// 按原始 Prefab 资产重新捕获每层皮肤的变换基准，修正历史数据导致的压缩/变形。
    /// grid 为 null 时处理全图。返回修复层数。
    /// </summary>
    public static int FixSkinBaseTransforms(MapGrid grid = null)
    {
        var grids = grid != null
            ? new[] { grid }
            : (MapManager.Instance != null
                ? MapManager.Instance.GetComponentsInChildren<MapGrid>()
                : UnityEngine.Object.FindObjectsOfType<MapGrid>());

        int fixedCount = 0;
        foreach (var g in grids)
        {
            if (g == null) continue;
            var data = g.GetComponent<GridSkinData>();
            if (data == null || data.GetSkinChildren().Count == 0) continue;

            Undo.RecordObject(data, "修复皮肤尺寸");
            var children = data.GetSkinChildren(); // 只取一次，避免每层重复遍历
            for (int layer = 0; layer < children.Count; layer++)
            {
                var info = data.GetSkinInfoAt(layer);
                if (info == null || string.IsNullOrEmpty(info.SkinName)) continue;

                var prefab = FindPrefab(info);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SkinBrush] 修复尺寸跳过：找不到原始模型 {info.SkinName}（格子 {g.name} 第 {layer} 层）");
                    continue;
                }
                // 场景里的实际变换也要登记 Undo——只记 data 会出现"数据回滚了、场景没动"的假撤销
                Undo.RecordObject(children[layer].transform, "修复皮肤尺寸");
                // 资产根的局部变换即模型导入后的原始变换（含 FBX 单位换算结果）
                info.CaptureBaseTransform(prefab.transform);
                data.ApplySkinTransformAt(layer);
                data.RecomputeAutoFit(layer); // 基准变了，自动贴合需要跟着重算
                fixedCount++;
            }
            EditorUtility.SetDirty(data);
        }

        if (fixedCount > 0 && Application.isPlaying) TrySyncToMapInfo();
        return fixedCount;
    }

    /// <summary>
    /// 校验皮肤尺寸与原始模型是否一致：比较"皮肤在单位父级下的尺寸"与"原始模型尺寸 × 用户倍率"。
    /// 比值接近 1 说明未压缩/拉伸。返回可读的校验结果文本。
    /// </summary>
    public static string ValidateSkinSize(MapGrid grid, int layer)
    {
        if (grid == null) return "未选中格子";
        var data = grid.GetComponent<GridSkinData>();
        var children = data != null ? data.GetSkinChildren() : null;
        if (data == null || children == null || layer < 0 || layer >= children.Count)
            return "该格没有对应皮肤层";

        var info = data.GetSkinInfoAt(layer);
        var skin = children[layer];
        if (info == null || skin == null) return "该层皮肤数据异常";

        var prefab = FindPrefab(info);
        if (prefab == null) return $"找不到原始模型资源：{info.SkinName}";

        // 皮肤尺寸：世界尺寸 ÷ 父级累计缩放（剥离地图整体缩放的影响）
        Vector3 skinWorld = GetBoundsSize(skin);
        Vector3 parentLossy = skin.transform.parent != null ? skin.transform.parent.lossyScale : Vector3.one;
        Vector3 skinNormalized = new Vector3(
            Mathf.Abs(parentLossy.x) > 1e-6f ? skinWorld.x / Mathf.Abs(parentLossy.x) : skinWorld.x,
            Mathf.Abs(parentLossy.y) > 1e-6f ? skinWorld.y / Mathf.Abs(parentLossy.y) : skinWorld.y,
            Mathf.Abs(parentLossy.z) > 1e-6f ? skinWorld.z / Mathf.Abs(parentLossy.z) : skinWorld.z);

        // 原始模型尺寸（临时实例化后测量，立即销毁）
        var temp = UnityEngine.Object.Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        Vector3 prefabSize = GetBoundsSize(temp);
        UnityEngine.Object.DestroyImmediate(temp);

        Vector3 mult = info.GetScaleMultiplier();
        Vector3 expected = Vector3.Scale(prefabSize, mult);
        Vector3 ratio = new Vector3(
            expected.x > 1e-6f ? skinNormalized.x / expected.x : 1,
            expected.y > 1e-6f ? skinNormalized.y / expected.y : 1,
            expected.z > 1e-6f ? skinNormalized.z / expected.z : 1);

        bool ok = Mathf.Abs(ratio.x - 1) < 0.02f && Mathf.Abs(ratio.y - 1) < 0.02f && Mathf.Abs(ratio.z - 1) < 0.02f;
        string verdict = ok ? "✅ 尺寸一致（未压缩/拉伸）" : "⚠️ 尺寸不一致，建议点“修复尺寸”";
        if (!info.BaseCaptured) verdict += "（该层未记录模型原始变换）";

        return $"{verdict}\n" +
               $"皮肤尺寸(单位父级) : {skinNormalized.x:F3}, {skinNormalized.y:F3}, {skinNormalized.z:F3}\n" +
               $"原始模型×倍率    : {expected.x:F3}, {expected.y:F3}, {expected.z:F3}\n" +
               $"比值(应≈1)       : {ratio.x:F3}, {ratio.y:F3}, {ratio.z:F3}";
    }

    static Vector3 GetBoundsSize(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return Vector3.zero;
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size;
    }

    // ---------- 存档修复 ----------

    /// <summary>
    /// 遍历 Database.Maps 中所有已存地图，为缺失 GUID / Addressables 地址的皮肤数据补全
    /// （含主皮肤与组合叠加层）。修复后需在地图编辑器中重新保存对应地图才能落盘。
    /// 返回修复条数；非运行状态返回 -1。
    /// </summary>
    public static int FixSavedMapSkins()
    {
        if (!Application.isPlaying) return -1;
        var maps = Database.Instance?.Maps;
        if (maps == null) return -1;

        int fixedCount = 0;
        foreach (var map in maps.Values)
        {
            if (map == null || map.GridInfos == null) continue;
            foreach (var gi in map.GridInfos)
            {
                if (gi == null) continue;
                if (gi.Skin != null && !string.IsNullOrEmpty(gi.Skin.SkinName)
                    && (string.IsNullOrEmpty(gi.Skin.SkinAddress) || string.IsNullOrEmpty(gi.Skin.SkinGuid)))
                {
                    var prefab = FindPrefab(gi.Skin);
                    if (prefab != null)
                    {
                        var path = AssetDatabase.GetAssetPath(prefab);
                        gi.Skin.SkinGuid = AssetDatabase.AssetPathToGUID(path);
                        gi.Skin.SkinAddress = ResolveAddress(path);
                        if (!string.IsNullOrEmpty(gi.Skin.SkinAddress)) fixedCount++;
                    }
                }
                if (gi.SkinExtras != null)
                {
                    foreach (var ex in gi.SkinExtras)
                    {
                        if (ex == null || string.IsNullOrEmpty(ex.SkinName)) continue;
                        if (!string.IsNullOrEmpty(ex.SkinAddress) && !string.IsNullOrEmpty(ex.SkinGuid)) continue;
                        var prefab = FindPrefab(ex);
                        if (prefab == null) continue;
                        var path = AssetDatabase.GetAssetPath(prefab);
                        ex.SkinGuid = AssetDatabase.AssetPathToGUID(path);
                        ex.SkinAddress = ResolveAddress(path);
                        if (!string.IsNullOrEmpty(ex.SkinAddress)) fixedCount++;
                    }
                }
            }
        }
        return fixedCount;
    }

    static GameObject FindPrefab(SkinInfo skin)
    {
        // 优先按 GUID
        if (!string.IsNullOrEmpty(skin.SkinGuid))
        {
            var p = AssetDatabase.GUIDToAssetPath(skin.SkinGuid);
            if (!string.IsNullOrEmpty(p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab != null) return prefab;
            }
        }
        // 其次按名字精确匹配（FindAssets 是模糊搜索，需过滤同名）
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab " + skin.SkinName))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == skin.SkinName)
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    /// <summary>
    /// 把场景中所有格子的皮肤状态（含叠加层）同步进运行中的地图编辑器 MapInfo.GridInfos。
    /// UI_MapBuilder 为 internal，通过反射访问；失败返回 false 并输出原因（不再静默）。
    /// </summary>
    public static bool TrySyncToMapInfo()
    {
        try
        {
            var uiType = Type.GetType("MapBuilderUI.UI_MapBuilder, Assembly-CSharp");
            if (uiType == null)
            {
                Debug.LogWarning("[SkinBrush] 同步失败：找不到 MapBuilderUI.UI_MapBuilder 类型");
                return false;
            }

            var inst = uiType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null)
            {
                Debug.LogWarning("[SkinBrush] 同步失败：UI_MapBuilder.Instance 为空（地图编辑器未打开？）");
                return false;
            }

            var mapInfo = uiType.GetField("MapInfo", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            if (mapInfo == null)
            {
                Debug.LogWarning("[SkinBrush] 同步失败：UI_MapBuilder.MapInfo 为空（尚未加载地图？）");
                return false;
            }

            var gridInfos = mapInfo.GetType().GetField("GridInfos")?.GetValue(mapInfo) as GridInfo[,];
            if (gridInfos == null)
            {
                Debug.LogWarning("[SkinBrush] 同步失败：MapInfo.GridInfos 为空（尚未进入地图编辑页？）");
                return false;
            }

            // 与 UI_MapBuilder 读取格子保持一致：只取激活格子（排除 Destroy 延迟销毁的僵尸格子）
            var mm = MapManager.Instance;
            var grids = mm != null
                ? mm.GetComponentsInChildren<MapGrid>()
                : UnityEngine.Object.FindObjectsOfType<MapGrid>();

            int n = GridSkinData.SyncSkinsToGridInfos(gridInfos, grids);
            Debug.Log($"[SkinBrush] 已同步 {n} 格皮肤数据到 MapInfo。");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SkinBrush] 同步皮肤数据到 MapInfo 异常：{e}");
            return false;
        }
    }
}
