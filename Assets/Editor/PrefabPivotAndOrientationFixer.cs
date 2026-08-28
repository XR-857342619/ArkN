using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键修正预制体轴心点与空间朝向（执行时无需输入任何参数）。
///
/// 处理流程（按顺序执行）：
///   1. 实例化选中的预制体，遍历所有子节点收集 Renderer（MeshRenderer 等），
///      用 Bounds 计算包围所有模型几何体的世界空间视觉中心 centerPos。
///      组合预制体（含 ≥2 个有几何的子物体）可弹窗勾选仅部分子物体参与计算；
///   2. 计算视觉中心应对齐的网格锚点 targetGridPos：
///      X / Y / Z 三轴统一四舍五入到网格（默认 0.5）的倍数（Mathf.Round(v / 0.5f) * 0.5f），
///      Z 不再特判归零；
///   3. 新建父级空物体作为新的预制体根节点（即新轴心），位置设为 targetGridPos
///      （网格点），rotation 保持 (0, 0, 0) 不变；
///   4. 原模型【整体】设为子物体：测量几何中心在模型本地坐标系中的位置 localCenter，
///      将固定旋转（默认 (90, 0, 0)）直接烘焙进原模型自身的 localRotation，
///      并令 localPosition = -旋转 * localCenter，使视觉中心与父节点 Pivot 完全重合。
///      旋转作用于整个原模型（含未参与中心计算的子物体），整体结构不因勾选而改变；
///   5. 通过 PrefabUtility 将新结构写回原预制体资源；
///   6. 全程注册 Undo，批量处理时显示可取消的进度条。
///
/// 使用方式：在 Project 窗口选中一个或多个 .prefab（也支持选中文件夹批量），
/// 点击菜单 Tools/自动修正预制体轴心与视角。
/// - 选中单个组合预制体时，弹窗勾选参与视觉中心计算的子物体（默认全选）；
/// - 选中多个 / 文件夹时，按默认（全部子物体）批量处理。
/// 参数（固定旋转角度、网格对齐单位）可在 Tools/自动修正预制体-参数设置 中调整。
/// </summary>
public class PrefabPivotAndOrientationFixer : EditorWindow
{
    // ==================== 可调整参数入口（主程可改） ====================

    // 固定旋转默认值：烘焙进原预制体（子物体）自身的 localRotation，
    // 父节点 rotation 恒保持 (0, 0, 0)
    private static readonly Vector3 DefaultFixedRotation = new Vector3(90f, 0f, 0f);

    // 网格对齐单位（视觉中心 X/Y/Z 三轴四舍五入到该值的倍数，得到父节点网格锚点）
    private const float DefaultGridSize = 0.5f;

    // v2：默认旋转由 (-90,0,0) 改为 (90,0,0) 且烘焙位置改为子物体，
    // 换用新 EditorPrefs 键，避免读到旧版本保存的过时参数
    private const string PrefKeyRotation = "PrefabPivotFixer.FixedRotation.v2";
    private const string PrefKeyGrid = "PrefabPivotFixer.GridSize";

    /// <summary>固定旋转角度（Euler），烘焙到子物体（原预制体）上。默认 (90, 0, 0)，可在参数设置窗口调整。</summary>
    private static Vector3 FixedRotation
    {
        get
        {
            string s = EditorPrefs.GetString(PrefKeyRotation, string.Empty);
            if (!string.IsNullOrEmpty(s))
            {
                string[] p = s.Split(',');
                if (p.Length == 3 &&
                    float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    return new Vector3(x, y, z);
                }
            }
            return DefaultFixedRotation;
        }
        set
        {
            EditorPrefs.SetString(PrefKeyRotation,
                string.Join(",",
                    value.x.ToString(CultureInfo.InvariantCulture),
                    value.y.ToString(CultureInfo.InvariantCulture),
                    value.z.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>网格对齐单位。默认 0.5，可在参数设置窗口调整。</summary>
    private static float GridSize
    {
        get
        {
            float g = EditorPrefs.GetFloat(PrefKeyGrid, DefaultGridSize);
            return g <= 0f ? DefaultGridSize : g;
        }
        set { EditorPrefs.SetFloat(PrefKeyGrid, Mathf.Max(0.01f, value)); }
    }

    private const string MenuPath = "Tools/自动修正预制体轴心与视角";
    private const string MenuPathSettings = "Tools/自动修正预制体-参数设置";

    // ==================== 主入口 ====================

    [MenuItem(MenuPath)]
    private static void FixSelectedPrefabs()
    {
        List<string> paths = CollectPrefabPaths();
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("自动修正预制体",
                "请先在 Project 窗口选中一个或多个预制体（.prefab），\n" +
                "也可以直接选中文件夹，将批量处理其中所有预制体。", "确定");
            return;
        }

        // 单个预制体：若是组合预制体（≥2 个含几何的子物体），弹窗让用户勾选参与计算的子物体
        if (paths.Count == 1)
        {
            string path = paths[0];
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var compositeChildren = GetChildrenWithRenderers(src);
            if (compositeChildren.Count >= 2)
            {
                PrefabChildPickerWindow.Open(path, compositeChildren,
                    (p, sel) => RunOne(p, sel));
                return;
            }
            // 非组合预制体：直接处理（全部子物体参与）
            RunOne(path, null);
            return;
        }

        // 批量：默认全部子物体参与，进度条
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("自动修正预制体轴心与视角");

        int done = 0;
        bool cancelled = false;

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                string name = Path.GetFileNameWithoutExtension(path);

                if (EditorUtility.DisplayCancelableProgressBar("自动修正预制体轴心与视角",
                        $"({i + 1}/{paths.Count}) 正在处理：{name}",
                        (float)(i + 1) / paths.Count))
                {
                    cancelled = true;
                    break;
                }

                try
                {
                    ProcessPrefab(path, null);
                    done++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PrefabPivotFixer] 处理失败：{path}\n{e}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (cancelled)
            Debug.LogWarning($"[PrefabPivotFixer] 已取消。本次共修正 {done}/{paths.Count} 个预制体。");
        else
            Debug.Log($"[PrefabPivotFixer] 全部完成：{done}/{paths.Count} 个预制体已修正。\n" +
                      $"当前参数：固定旋转 {FixedRotation}，网格对齐 {GridSize}。");
    }

    // 菜单校验：只有选中了有效预制体/文件夹时菜单才可用
    [MenuItem(MenuPath, true)]
    private static bool ValidateFixSelectedPrefabs()
    {
        return CollectPrefabPaths().Count > 0;
    }

    /// <summary>处理单个预制体（带 Undo 组、保存刷新），供单选直处理与弹窗回调共用。</summary>
    private static void RunOne(string prefabPath, HashSet<int> selectedChildIndices)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("自动修正预制体轴心与视角");
        try
        {
            ProcessPrefab(prefabPath, selectedChildIndices);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PrefabPivotFixer] 处理失败：{prefabPath}\n{e}");
        }
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 返回预制体直接子物体中“自身或子树含 Renderer”的 (index, name) 列表。
    /// 调用方据此判断是否为组合预制体并作为弹窗数据源。
    /// </summary>
    private static List<(int index, string name)> GetChildrenWithRenderers(GameObject src)
    {
        var list = new List<(int, string)>();
        if (src == null) return list;
        for (int i = 0; i < src.transform.childCount; i++)
        {
            var child = src.transform.GetChild(i);
            if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
                list.Add((i, child.name));
        }
        return list;
    }

    // ==================== 单个预制体处理 ====================

    /// <param name="selectedChildIndices">
    /// 参与视觉中心计算的“直接子物体”索引集合；null 表示全部参与。
    /// 仅影响 Bounds 计算，不影响旋转/包裹（旋转作用于整个原模型）。
    /// </param>
    private static void ProcessPrefab(string prefabPath, HashSet<int> selectedChildIndices)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (source == null)
            throw new Exception("无法加载预制体资源。");

        // 预制体变体结构特殊，跳过避免误处理
        if (PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.Variant)
        {
            Debug.LogWarning($"[PrefabPivotFixer] {prefabPath} 是预制体变体（Variant），已跳过。");
            return;
        }

        // ---------- 1. 实例化预制体到场景（PrefabUtility API） ----------
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        if (instance == null)
            throw new Exception("实例化预制体失败。");
        Undo.RegisterCreatedObjectUndo(instance, "修正预制体轴心与视角");

        GameObject newRoot = null;
        try
        {
            // 断开与原资源的连接，否则保存回同一路径时会产生自引用嵌套
            PrefabUtility.UnpackPrefabInstance(instance,
                PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            // ---------- 2. 收集 Renderer（按勾选的子物体过滤），计算世界空间视觉中心 ----------
            Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            List<Renderer> validRenderers = new List<Renderer>();
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                if (selectedChildIndices != null)
                {
                    Transform directChild = FindDirectChildAncestor(r.transform, instance.transform);
                    // 直接挂在根上的 Renderer 不属于任何“子物体”，始终参与；
                    // 属于某子物体时，仅当该子物体被勾选才参与计算
                    if (directChild != null && !selectedChildIndices.Contains(directChild.GetSiblingIndex()))
                        continue;
                }
                validRenderers.Add(r);
            }
            if (validRenderers.Count == 0)
            {
                Debug.LogWarning($"[PrefabPivotFixer] {prefabPath} 未找到任何 Renderer，已跳过。");
                return;
            }

            Bounds bounds = validRenderers[0].bounds;
            for (int i = 1; i < validRenderers.Count; i++)
                bounds.Encapsulate(validRenderers[i].bounds);

            Vector3 centerPos = bounds.center; // 世界空间视觉中心

            // 模型几何中心在模型自身本地坐标系中的位置
            // （必须在重置根节点旋转/缩放之前测量，InverseTransformPoint
            //   已自动包含原根节点位置/旋转/缩放的影响）
            Vector3 localCenter = instance.transform.InverseTransformPoint(centerPos);

            // ---------- 3. 网格锚点（X/Y/Z 三轴统一对齐，不再对 Z 特判） ----------
            float grid = GridSize;
            Vector3 targetGridPos = new Vector3(
                Mathf.Round(centerPos.x / grid) * grid,
                Mathf.Round(centerPos.y / grid) * grid,
                Mathf.Round(centerPos.z / grid) * grid
            );

            // ---------- 4. 新建父级空物体（rotation 保持 0，不承载旋转） ----------
            newRoot = new GameObject(source.name);
            Undo.RegisterCreatedObjectUndo(newRoot, "修正预制体轴心与视角");
            newRoot.transform.SetParent(instance.transform.parent, false);
            newRoot.transform.SetSiblingIndex(instance.transform.GetSiblingIndex());

            // 父级空物体直接移到网格点上，rotation 参数恒为 (0,0,0)
            newRoot.transform.position = targetGridPos;
            newRoot.transform.localScale = Vector3.one;
            newRoot.transform.rotation = Quaternion.identity;

            // ---------- 5. 原模型【整体】设为子物体，旋转烘焙进原模型自身参数 ----------
            // 注：旋转作用于整个 instance（含未参与中心计算的子物体），
            //     组合预制体的整体结构不因勾选而改变（需求 3）。
            Undo.SetTransformParent(instance.transform, newRoot.transform, "修正预制体轴心与视角");
            Undo.RecordObject(instance.transform, "修正预制体轴心与视角");

            // 旋转不放在父节点上（父节点 rotation = 0），
            // 而是直接写入原预制体（子物体）自身的 localRotation ——
            // 父物体在视觉上相较于子物体相差该固定旋转角度，但 rotation 参数为 0。
            Quaternion bakeRotation = Quaternion.Euler(FixedRotation);

            // 子物体本地偏移 = -bakeRotation * localCenter。
            // 最终视觉中心的世界坐标：
            //   父节点位置 + 父旋转 * (localPosition + 子旋转 * localCenter)
            // = 父节点位置 + (-bakeRotation * localCenter + bakeRotation * localCenter)
            // = 父节点位置（即 Pivot）→ 视觉中心与轴心【完全重合】。
            Vector3 correctedLocalOffset = -(bakeRotation * localCenter);

            // 应用偏移与烘焙旋转，并重置缩放
            instance.transform.localPosition = correctedLocalOffset;
            instance.transform.localRotation = bakeRotation;
            instance.transform.localScale = Vector3.one;

            // ---------- 6. 将新结构写回预制体资源 ----------
            // 新结构根节点（newRoot）是普通 GameObject 而非预制体实例，
            // 因此不能直接调用 PrefabUtility.ApplyPrefabInstance 写回；
            // SaveAsPrefabAsset 是官方推荐 API（旧版 ApplyPrefabInstance /
            // ReplacePrefab 写回语义的现代等价物），会以新结构覆盖原 .prefab
            // 资源并保留原 GUID —— 场景与其他预制体对它的引用不会丢失。
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(newRoot, prefabPath);
            if (saved == null)
                throw new Exception("保存预制体资源失败。");

            string selDesc = selectedChildIndices == null
                ? "全部子物体"
                : ("勾选索引: " + string.Join(",", selectedChildIndices.OrderBy(x => x)));
            Debug.Log($"[PrefabPivotFixer] 已修正：{prefabPath}\n" +
                      $"参与视觉中心计算：{selDesc}\n" +
                      $"视觉中心 centerPos = {centerPos}\n" +
                      $"网格锚点（新轴心）targetGridPos = {targetGridPos}\n" +
                      $"模型本地几何中心 localCenter = {localCenter}\n" +
                      $"子物体局部偏移 correctedLocalOffset = {correctedLocalOffset}\n" +
                      $"子物体烘焙旋转 localRotation = {FixedRotation}（父节点 rotation = 0）");
        }
        finally
        {
            // ---------- 7. 清理临时场景对象（支持撤销） ----------
            if (newRoot != null)
                Undo.DestroyObjectImmediate(newRoot); // 子物体（原模型实例）会一并销毁
            else
                Undo.DestroyObjectImmediate(instance);
        }
    }

    /// <summary>
    /// 找到 r 所属的 instance 根的直接子物体 Transform；若 r 直接挂在 instance 根上则返回 null。
    /// </summary>
    private static Transform FindDirectChildAncestor(Transform r, Transform instanceRoot)
    {
        if (r == null || r == instanceRoot) return null;
        Transform t = r;
        while (t.parent != null && t.parent != instanceRoot)
            t = t.parent;
        return (t.parent == instanceRoot) ? t : null;
    }

    // ==================== 选中项收集 ====================

    private static List<string> CollectPrefabPaths()
    {
        var set = new HashSet<string>();
        foreach (UnityEngine.Object obj in Selection.objects)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(path);
            }
            else if (AssetDatabase.IsValidFolder(path))
            {
                // 选中文件夹时，递归查找其中所有预制体
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    set.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        return set.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ==================== 参数设置窗口（主程可调参数入口） ====================

    [MenuItem(MenuPathSettings)]
    private static void OpenSettings()
    {
        PrefabPivotAndOrientationFixer win =
            GetWindow<PrefabPivotAndOrientationFixer>(true, "预制体修正参数设置", true);
        win.minSize = new Vector2(340f, 190f);
    }

    private Vector3 _guiRotation;
    private float _guiGrid;

    private void OnEnable()
    {
        _guiRotation = FixedRotation;
        _guiGrid = GridSize;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("参数设置（主程可调）", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        _guiRotation = EditorGUILayout.Vector3Field("固定旋转角度 (Euler)", _guiRotation);
        _guiGrid = EditorGUILayout.FloatField("网格对齐单位", _guiGrid);
        EditorGUILayout.HelpBox(
            "固定旋转默认 (90, 0, 0)：烘焙到子物体（原预制体）的 localRotation 上，\n" +
            "父节点 rotation 参数保持 (0, 0, 0)，视觉上父子相差该旋转角度。\n" +
            "网格对齐单位默认 0.5：父节点锚点的 X/Y/Z 三轴统一四舍五入到该值的倍数。",
            MessageType.Info);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("保存", GUILayout.Height(26f)))
            {
                FixedRotation = _guiRotation;
                GridSize = _guiGrid;
                _guiGrid = GridSize;
                ShowNotification(new GUIContent("参数已保存"));
            }
            if (GUILayout.Button("恢复默认", GUILayout.Height(26f)))
            {
                _guiRotation = DefaultFixedRotation;
                _guiGrid = DefaultGridSize;
                FixedRotation = DefaultFixedRotation;
                GridSize = DefaultGridSize;
                ShowNotification(new GUIContent("已恢复默认参数"));
            }
        }
    }
}

// ==================== 组合预制体子物体勾选窗口 ====================

/// <summary>
/// 当用户选中单个组合预制体时弹出，让用户勾选哪些直接子物体参与视觉中心计算。
/// 默认全选；点击“执行”后回调主处理器，按勾选结果处理。
/// </summary>
public class PrefabChildPickerWindow : EditorWindow
{
    private string _prefabPath;
    private List<(int index, string name)> _children;
    private bool[] _toggles;
    private Action<string, HashSet<int>> _onExecute;
    private Vector2 _scroll;

    public static void Open(string prefabPath, List<(int index, string name)> children,
        Action<string, HashSet<int>> onExecute)
    {
        var win = GetWindow<PrefabChildPickerWindow>(true, "勾选参与视觉中心的子物体", true);
        win._prefabPath = prefabPath;
        win._children = children;
        win._onExecute = onExecute;
        win._toggles = new bool[children.Count];
        for (int i = 0; i < win._toggles.Length; i++) win._toggles[i] = true; // 默认全选
        win.minSize = new Vector2(360f, 360f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            $"组合预制体：{Path.GetFileNameWithoutExtension(_prefabPath)}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "勾选参与视觉中心计算的直接子物体（默认全选）。" +
            "未勾选的子物体不参与中心计算，但仍会随整体一同旋转。",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("全选")) SetAll(true);
            if (GUILayout.Button("全不选")) SetAll(false);
            if (GUILayout.Button("反选"))
                for (int i = 0; i < _toggles.Length; i++) _toggles[i] = !_toggles[i];
        }
        EditorGUILayout.Space();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _children.Count; i++)
        {
            _toggles[i] = EditorGUILayout.ToggleLeft(
                $"[{_children[i].index}] {_children[i].name}", _toggles[i]);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("执行", GUILayout.Height(28f)))
            {
                var sel = new HashSet<int>();
                for (int i = 0; i < _toggles.Length; i++)
                    if (_toggles[i]) sel.Add(_children[i].index);
                if (sel.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "至少需要勾选一个子物体。", "确定");
                    return;
                }
                var cb = _onExecute;
                Close();
                cb?.Invoke(_prefabPath, sel);
            }
            if (GUILayout.Button("取消", GUILayout.Height(28f)))
            {
                Close();
            }
        }
    }

    private void SetAll(bool value)
    {
        for (int i = 0; i < _toggles.Length; i++) _toggles[i] = value;
    }
}
