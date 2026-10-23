using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 皮肤数据（可 JSON 序列化，随 MapInfo/GridInfos 存档一起保存）。
/// </summary>
[System.Serializable]
public class SkinInfo
{
    /// <summary>编辑器侧预制体 GUID（撤销/定位用，运行时兜底加载用）</summary>
    public string SkinGuid;

    /// <summary>Addressables 地址（运行时加载首选键；编辑器笔刷自动写入，未注册的 Prefab 会自动注册）</summary>
    public string SkinAddress;

    /// <summary>预制体名（显示用；运行时加载的备用键）</summary>
    public string SkinName;

    /// <summary>皮肤相对白膜局部位置的偏移</summary>
    public Vector3 Offset;

    /// <summary>皮肤局部欧拉角（相对模型原始朝向的增量）</summary>
    public Vector3 Rotation;

    /// <summary>相对模型自身原始缩放的倍率（全零视为 1，兼容旧数据）</summary>
    public Vector3 Scale;

    /// <summary>笔刷换皮时是否隐藏了白膜（false = 保留原模型可见，用于对照/组合）</summary>
    public bool TileHidden;

    // ======== 模型原始变换基准（换皮必须保留，否则大模型被压扁/错位） ========
    // 记录 Prefab 实例化时的自带局部变换；应用变换 = 基准 + 用户增量，
    // 绝不套用白膜的 localScale / localRotation
    //（白膜如 tiles_ground 自带 m_LocalScale = (1, 0.6, 1) 是压扁的地块）。

    /// <summary>模型 Prefab 自带的局部位置（通常为 0）</summary>
    public Vector3 BaseLocalPos;

    /// <summary>模型 Prefab 自带的局部欧拉角（FBX 常见 -90/90，必须保留）</summary>
    public Vector3 BaseRotation;

    /// <summary>模型 Prefab 自带的局部缩放（FBX 常见 0.01/100，必须保留；全零视为 1）</summary>
    public Vector3 BaseScale;

    /// <summary>基准是否已捕获（旧存档为 false：按单位变换处理，可用笔刷的"修复尺寸"按钮补全）</summary>
    public bool BaseCaptured;

    public SkinInfo Clone()
    {
        return new SkinInfo
        {
            SkinGuid = SkinGuid,
            SkinAddress = SkinAddress,
            SkinName = SkinName,
            Offset = Offset,
            Rotation = Rotation,
            Scale = Scale,
            TileHidden = TileHidden,
            BaseLocalPos = BaseLocalPos,
            BaseRotation = BaseRotation,
            BaseScale = BaseScale,
            BaseCaptured = BaseCaptured,
        };
    }

    /// <summary>从实例读取自带的局部变换作为基准（换皮实例化后立即调用）</summary>
    public void CaptureBaseTransform(Transform t)
    {
        if (t == null) return;
        BaseLocalPos = t.localPosition;
        BaseRotation = t.localEulerAngles;
        BaseScale = t.localScale;
        BaseCaptured = true;
    }

    /// <summary>生效的基准局部位置（未捕获按 0）</summary>
    public Vector3 GetBaseLocalPos()
    {
        return BaseCaptured ? BaseLocalPos : Vector3.zero;
    }

    /// <summary>生效的基准欧拉角（未捕获按 0）</summary>
    public Vector3 GetBaseRotation()
    {
        return BaseCaptured ? BaseRotation : Vector3.zero;
    }

    /// <summary>基准缩放（未捕获或全零按 1 处理，兼容旧存档）</summary>
    public Vector3 GetBaseScale()
    {
        if (!BaseCaptured) return Vector3.one;
        return BaseScale == Vector3.zero ? Vector3.one : BaseScale;
    }

    /// <summary>用户缩放倍率（缺失按 1 处理）</summary>
    public Vector3 GetScaleMultiplier()
    {
        return Scale == Vector3.zero ? Vector3.one : Scale;
    }
}

/// <summary>
/// 白膜（MapGrid）的皮肤数据组件。
/// - 编辑器：皮肤笔刷（Tools/地图换皮/皮肤笔刷）写入数据并挂载皮肤子物体；
///   支持组合模式——同一格叠加多层皮肤（主皮肤 Skin_Model + 叠加层 Skin_Model_1..n）
/// - 运行时：负责还原全部皮肤层——按数据隐藏白膜 Renderer（保留 Collider，判定不变）
///
/// 机制判定字段（CanMove / FarAttackGrid / PassCost 等）全部在 MapGrid 上，
/// 本组件不参与任何判定，只做视觉。
/// 层级规则：层序 = 挂载顺序（主皮肤最先，后叠加的在后）；
/// 叠加层的 MeshRenderer 设置递增 sortingOrder，保证后叠加的显示在上。
/// </summary>
public class GridSkinData : MonoBehaviour
{
    /// <summary>主皮肤（第 0 层）子物体名</summary>
    public const string SkinChildName = "Skin_Model";

    /// <summary>组合叠加皮肤（第 1..n 层）子物体名前缀</summary>
    public const string ExtraSkinPrefix = "Skin_Model_";

    /// <summary>主皮肤数据（第 0 层；兼容旧存档与所有既有逻辑）</summary>
    public SkinInfo Data = new SkinInfo();

    /// <summary>组合模式叠加的皮肤数据（第 1..n 层）</summary>
    public List<SkinInfo> ExtraSkins = new List<SkinInfo>();

    // ================= 查找 =================

    /// <summary>
    /// 白膜视觉子物体（tiles_xxx）。
    /// 优先取 MapGrid.go，否则回退到第一个非皮肤的带 Renderer/Collider 的子物体。
    /// </summary>
    public GameObject FindTileModel()
    {
        var grid = GetComponent<MapGrid>();
        if (grid != null && grid.go != null && grid.go.transform.IsChildOf(transform))
            return grid.go;

        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (IsSkinChild(c)) continue;
            if (c.GetComponentInChildren<Renderer>(true) != null || c.GetComponent<Collider>() != null)
                return c.gameObject;
        }
        return null;
    }

    public static bool IsSkinChild(Transform t)
    {
        // 名字中的数字仅作标识，不参与层序计算（层序 = 兄弟节点顺序 = 挂载顺序）
        return t.name == SkinChildName || t.name.StartsWith(ExtraSkinPrefix);
    }

    /// <summary>取主皮肤子物体</summary>
    public GameObject GetSkinChild()
    {
        var t = transform.Find(SkinChildName);
        return t != null ? t.gameObject : null;
    }

    /// <summary>
    /// 取全部皮肤子物体，按兄弟节点顺序（= 挂载顺序）排列。
    /// 主皮肤最先挂载，后叠加的在后；撤销/重做系统按层级整体恢复，顺序天然可靠。
    /// </summary>
    public List<GameObject> GetSkinChildren()
    {
        var list = new List<GameObject>();
        foreach (Transform c in transform)
            if (IsSkinChild(c)) list.Add(c.gameObject);
        return list;
    }

    /// <summary>按层序取皮肤子物体（0 = 主皮肤）</summary>
    public GameObject GetSkinChildAt(int layer)
    {
        var children = GetSkinChildren();
        return layer >= 0 && layer < children.Count ? children[layer] : null;
    }

    /// <summary>按层序取皮肤数据（0 = 主皮肤 Data）</summary>
    public SkinInfo GetSkinInfoAt(int layer)
    {
        if (layer == 0) return Data;
        return layer - 1 < ExtraSkins.Count ? ExtraSkins[layer - 1] : null;
    }

    /// <summary>皮肤层总数（以实际子物体为准：主皮肤 + 叠加层）</summary>
    public int SkinLayerCount
    {
        get { return GetSkinChildren().Count; }
    }

    // ================= 白膜显示 =================

    /// <summary>
    /// 隐藏白膜全部 Renderer。Collider 保留，MapManager 的射线点击、
    /// OnPointerClick、笔刷选择等判定完全不受影响。
    /// 是否隐藏由 Data.TileHidden 决定（主皮肤第一笔的"隐藏白膜"开关）。
    /// </summary>
    public void HideTileRenderers()
    {
        var tile = FindTileModel();
        if (tile == null) return;
        foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
    }

    /// <summary>恢复白膜显示（清除全部皮肤时使用）</summary>
    public void ShowTileRenderers()
    {
        var tile = FindTileModel();
        if (tile == null) return;
        foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }

    // ================= 变换 =================

    /// <summary>
    /// 按指定层的数据摆放该层皮肤（只改皮肤子物体局部变换，不碰白膜根节点）。
    /// 位置：白膜局部位置（对齐基准）+ 模型自带局部位置 + 用户偏移；
    /// 旋转：模型自带旋转 × 用户旋转增量；
    /// 缩放：模型自带缩放 × 用户倍率 —— 绝不套用白膜缩放，
    /// 否则（如白膜 y=0.6）大模型会被整体压扁、骨骼塌缩。
    /// </summary>
    public void ApplySkinTransformAt(int layer)
    {
        var skin = GetSkinChildAt(layer);
        var info = GetSkinInfoAt(layer);
        if (skin == null || info == null) return;
        var tile = FindTileModel();
        Vector3 tilePos = tile != null ? tile.transform.localPosition : Vector3.zero;
        ApplyLocal(skin.transform, info, tilePos);
    }

    /// <summary>核心摆放：位置 = 白膜局部位置 + 模型基准 + 用户偏移；旋转/缩放 = 基准 × 用户增量</summary>
    static void ApplyLocal(Transform skin, SkinInfo info, Vector3 tilePos)
    {
        // 基准未捕获（旧存档）时按单位变换处理：不会被白膜缩放压扁；
        // 需要精确恢复 FBX 自带缩放的老图，可用笔刷面板的"修复尺寸"按钮补全基准
        skin.localPosition = tilePos + info.GetBaseLocalPos() + info.Offset;
        skin.localRotation = Quaternion.Euler(info.GetBaseRotation()) * Quaternion.Euler(info.Rotation);
        skin.localScale = Vector3.Scale(info.GetBaseScale(), info.GetScaleMultiplier());
    }

    /// <summary>按数据摆放全部皮肤层（children 只取一次，避免每层重复遍历）</summary>
    public void ApplySkinTransform()
    {
        var children = GetSkinChildren();
        if (children.Count == 0) return;
        var tile = FindTileModel();
        Vector3 tilePos = tile != null ? tile.transform.localPosition : Vector3.zero;
        for (int i = 0; i < children.Count; i++)
        {
            var info = GetSkinInfoAt(i);
            if (children[i] != null && info != null) ApplyLocal(children[i].transform, info, tilePos);
        }
    }

    // ================= 重建/还原 =================

    /// <summary>
    /// 白膜被 AutoBuild() 重建后调用（AutoBuild 末尾已挂接）：
    /// 按数据恢复隐藏状态与全部皮肤层对位。
    /// </summary>
    public void OnTileRebuilt()
    {
        if (GetSkinChildren().Count == 0)
        {
            // 场景中皮肤实例不存在（如战斗内整图重建），按数据还原全部层
            ReapplyFromData();
            ReapplyExtras();
            return;
        }
        if (Data.TileHidden) HideTileRenderers();
        ApplySkinTransform();
    }

    string SkinLoadKey()
    {
        return !string.IsNullOrEmpty(Data.SkinAddress) ? Data.SkinAddress : Data.SkinName;
    }

    /// <summary>加载皮肤实例：优先 Addressables 地址，其次名字；编辑器内再按 GUID 兜底</summary>
    GameObject LoadSkinInstance(SkinInfo info)
    {
        if (info == null) return null;

        GameObject instance = null;
        var keys = new[] { info.SkinAddress, info.SkinName };
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            try { instance = ResHelper.Instantiate(k); }
            catch { /* 无效地址，试下一个键 */ }
            if (instance != null) break;
        }

#if UNITY_EDITOR
        // 编辑器兜底：Play Mode 用 Packed 脚本时走的是上次构建的目录，
        // 新注册的地址查不到；直接按 GUID 从 AssetDatabase 加载，保证编辑器调试不受构建模式影响
        if (instance == null && !string.IsNullOrEmpty(info.SkinGuid))
        {
            var prefabPath = UnityEditor.AssetDatabase.GUIDToAssetPath(info.SkinGuid);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    instance = Instantiate(prefab);
                    Debug.Log($"[GridSkinData] 编辑器兜底加载皮肤：{prefabPath}");
                }
            }
        }
#endif

        if (instance == null)
            Debug.LogError($"[GridSkinData] 皮肤加载失败（该格子物体将缺失）：{info.SkinName}（地址：{(string.IsNullOrEmpty(info.SkinAddress) ? "无" : info.SkinAddress)}，GUID：{info.SkinGuid}，格子 {name}）。\n" +
                "排查：① 编辑器内 → 菜单 Tools/地图换皮/修复存档皮肤数据（FixSavedMapSkins）后重新保存地图；\n" +
                "② 打包环境 → 确认该 Prefab 已注册 Addressables 并重新 Build（Groups 窗口），否则运行时无 GUID 兜底必然缺皮。");
        return instance;
    }

    /// <summary>运行时按数据还原主皮肤（第 0 层）</summary>
    public void ReapplyFromData()
    {
        if (string.IsNullOrEmpty(SkinLoadKey())) return;

        var old = GetSkinChild();
        if (old != null)
        {
            if (Application.isPlaying)
            {
                // v3.5：Destroy 是延迟到帧末的，旧实例在本帧内仍会被 GetSkinChildren 计入，
                // 紧接着的 ReapplyExtras/AddExtraFromData 会把"将死的旧主皮肤"当成有效层，
                // 导致层序错位/叠加层漏建。先摘出层级树再延迟销毁。
                old.transform.SetParent(null);
                Destroy(old);
            }
            else DestroyImmediate(old);
        }

        var instance = LoadSkinInstance(Data);
        if (instance == null) return;
        AttachSkin(instance);
    }

    /// <summary>按存档数据追加一层叠加皮肤（组合模式还原用）</summary>
    public void AddExtraFromData(SkinInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.SkinName)) return;
        ExtraSkins.Add(info.Clone());
        ReapplyExtras();
    }

    /// <summary>
    /// 裁掉数据中超出实际子物体层数的冗余条目（撤销残留防护，运行时安全）。
    /// 场景里没有的层不还原，避免重复加载。
    /// </summary>
    public void TrimStaleExtras()
    {
        int extraChildren = Mathf.Max(0, GetSkinChildren().Count - 1);
        while (ExtraSkins.Count > extraChildren) ExtraSkins.RemoveAt(ExtraSkins.Count - 1);
    }

    /// <summary>
    /// 确保所有叠加层都有实例：已有的复位对位，缺失的按数据加载补建。
    /// 层序 = 兄弟节点顺序，与 ExtraSkins 下标一一对应。
    /// </summary>
    public void ReapplyExtras()
    {
        var children = GetSkinChildren();
        // 已有实例的层：复位对位
        for (int i = 1; i < children.Count; i++)
            ApplySkinTransformAt(i);
        // 缺实例的层：按数据加载补建（SetAsLastSibling 保证落在正确的层序位置）
        // 起点钳到 0：主皮肤加载失败时 children 为空，Count-1 = -1 会负下标越界（2026-09-26 实修）
        for (int i = Mathf.Max(0, children.Count - 1); i < ExtraSkins.Count; i++)
        {
            var inst = LoadSkinInstance(ExtraSkins[i]);
            if (inst == null) continue;
            AttachExtra(inst, i + 1);
        }
    }

    /// <summary>把（已实例化的）主皮肤挂到白膜下：改名、去碰撞体、按数据对位</summary>
    public void AttachSkin(GameObject instance)
    {
        instance.name = SkinChildName;
        instance.transform.SetParent(transform, false);

        // 剥离皮肤自带碰撞体：白膜保留的 Collider 是唯一射线判定来源
        foreach (var c in instance.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        instance.transform.SetSiblingIndex(1); // 紧跟白膜（白膜为 child 0）
        if (Data.TileHidden) HideTileRenderers();
        ApplySkinTransform();
    }

    /// <summary>配置叠加皮肤实例的命名、父级、层级顺序、sortingOrder（不含碰撞体剥离与数据写入）</summary>
    public void ConfigureExtra(GameObject instance, int layer)
    {
        // 命名取不冲突的最小序号；层序由挂载顺序（sibling 位置）决定，名字仅作标识
        int n = layer;
        while (transform.Find(ExtraSkinPrefix + n) != null) n++;
        instance.name = ExtraSkinPrefix + n;

        instance.transform.SetParent(transform, false);
        // 层级顺序：后叠加的排在兄弟节点最后、sortingOrder 递增 → 显示在最上层
        instance.transform.SetAsLastSibling();
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
                r.sortingOrder = 10 * n;
    }

    /// <summary>把（已实例化的）叠加皮肤挂到白膜下：去碰撞体、配置层级、按数据对位</summary>
    public void AttachExtra(GameObject instance, int layer)
    {
        foreach (var c in instance.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
        ConfigureExtra(instance, layer);
        ApplySkinTransformAt(layer);
    }

    // ================= 运行时钩子 =================

#if UNITY_EDITOR
    /// <summary>
    /// 编辑模式：在 Inspector 中直接修改 Data / ExtraSkins / TileHidden 后，
    /// 自动把数据应用到皮肤子物体与白膜显隐（否则 Inspector 改参数无任何效果）。
    /// OnValidate 处于序列化回调中不能直接改其他对象，故用 delayCall 延迟执行。
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !gameObject.activeInHierarchy) return;
            var children = GetSkinChildren();
            if (children.Count == 0) return; // 无皮肤实例，无需应用

            SetTileRenderersWithUndo(Data.TileHidden);
            ApplySkinTransform();
        };
    }
#endif

    private void Start()
    {
        // 场景自带皮肤实例（场景地图）：确保隐藏状态与对位正确
        if (GetSkinChild() != null)
        {
            TrimStaleExtras();          // 撤销残留防护：数据以实际子物体为准
            if (Data.TileHidden) HideTileRenderers();
            ApplySkinTransform();
            ReapplyExtras(); // 补齐缺失的叠加层
        }
        else if (!string.IsNullOrEmpty(SkinLoadKey()))
        {
            ReapplyFromData();
            ReapplyExtras();
        }
    }

    // ================= 存档同步 =================

    /// <summary>
    /// 把场景中各格皮肤数据同步进 GridInfo[,]（保存地图前的强制同步入口）。
    /// 逐格容错：单格异常只跳过该格并告警，绝不中断其他格子的同步。
    /// gridInfos 为 null 时直接返回 0。返回成功写入的格子数。
    /// </summary>
    public static int SyncSkinsToGridInfos(GridInfo[,] gridInfos, IEnumerable<MapGrid> grids)
    {
        if (gridInfos == null || grids == null) return 0;
        int w = gridInfos.GetLength(0), h = gridInfos.GetLength(1);
        int written = 0;

        foreach (var grid in grids)
        {
            if (grid == null) continue;
            try
            {
                if (grid.X < 0 || grid.Y < 0 || grid.X >= w || grid.Y >= h) continue;
                var gi = gridInfos[grid.X, grid.Y];
                if (gi == null) continue;

                var data = grid.GetComponent<GridSkinData>();
                var children = data != null ? data.GetSkinChildren() : null;
                // v3.5：以「数据」为存档依据，而不是场景实例。
                // 实例可能因加载失败（Addressables 目录过期）/撤销残缺而暂时缺失；
                // 旧逻辑 children.Count==0 就清空 gi.Skin，等于在保存时把"数据还在、实例暂时没了"
                // 的皮肤洗掉——保存本身成了丢皮肤的元凶，进战斗自然缺子物体（偶现）。
                // 清除皮肤走 Clear()（组件整体移除），不存在"实例没了数据还在"的合法清除路径，
                // 所以数据非空时必须保留。
                bool hasMainData = data != null && data.Data != null && !string.IsNullOrEmpty(data.Data.SkinName);
                bool hasExtraData = data != null && data.ExtraSkins != null
                    && data.ExtraSkins.Any(x => x != null && !string.IsNullOrEmpty(x.SkinName));
                if (data != null && (hasMainData || hasExtraData || (children != null && children.Count > 0)))
                {
                    gi.Skin = data.Data != null ? data.Data.Clone() : null;
                    gi.SkinExtras = data.ExtraSkins != null && data.ExtraSkins.Count > 0
                        ? data.ExtraSkins.Where(x => x != null).Select(x => x.Clone()).ToList()
                        : null;
                }
                else
                {
                    gi.Skin = null;
                    gi.SkinExtras = null;
                }
                written++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GridSkinData] 同步格子 ({grid?.X},{grid?.Y}) 皮肤数据失败，已跳过：{e.Message}");
            }
        }
        return written;
    }

#if UNITY_EDITOR
    /// <summary>编辑器内翻转白膜显隐（逐 renderer 登记 Undo，保证撤销链完整）</summary>
    void SetTileRenderersWithUndo(bool visible)
    {
        var tile = FindTileModel();
        if (tile == null) return;
        foreach (var r in tile.GetComponentsInChildren<Renderer>(true))
        {
            if (r.enabled == visible) continue;
            UnityEditor.Undo.RecordObject(r, visible ? "显示白膜" : "隐藏白膜");
            r.enabled = visible;
        }
    }
#endif

    private void OnTransformChildrenChanged()
    {
        // AutoBuild() 重建白膜（销毁旧 tile、实例化新 tile）会触发本回调，
        // 仅在主皮肤"隐藏白膜"模式下恢复隐藏状态
        if (Data.TileHidden && (GetSkinChildren().Count > 0 || !string.IsNullOrEmpty(SkinLoadKey())))
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) { SetTileRenderersWithUndo(false); return; }
#endif
            HideTileRenderers();
        }
    }
}
