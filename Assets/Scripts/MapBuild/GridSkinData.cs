using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Newtonsoft.Json;

/// <summary>
/// 皮肤锚点语义：决定"皮肤子物体的原点要对齐到地块的哪个位置"。
///
/// 背景（2026-10 实测）：白膜 tiles_ground 是 1×0.6×1 的盒子，AutoBuild 把它下移 0.3，
/// 使**顶面**正好落在格子平面 y=0 上；也就是说白膜的 localPosition.y = -0.3 是它的**几何中心**，
/// 而不是格子表面。旧实现直接拿白膜 localPosition 当锚点，等于把每个皮肤都摆到"地块中心"，
/// 于是所有皮肤系统性下沉 0.3（高台 0.4，且高台白膜自身还有 0.1 的偏差）。
/// 实证：8 张存量地图的美术数据一律写 Offset.y ≈ +0.3 来抵消这次下沉 —— 即真实需求是"表面"。
///
/// 因此这里把锚点显式化：
/// - 新画笔默认 TileSurface（皮肤站在格子表面，Offset 为 0 即正确）；
/// - 旧存档字段缺失 → 反序列化为 TileCenter(0)，视觉与修复前完全一致，零破坏；
/// - 需要把老图统一到新语义时，用 Tools/地图换皮/皮肤锚点迁移（可保持外观，也可整体上移）。
/// </summary>
public enum SkinAnchor
{
    /// <summary>旧语义：白膜几何中心（= 白膜 localPosition）。兼容旧存档，勿改枚举值。</summary>
    TileCenter = 0,

    /// <summary>白膜顶面 = 格子可视表面（新画笔默认）。按渲染器包围盒实测，对任意白膜 prefab 成立。</summary>
    TileSurface = 1,

    /// <summary>格子原点（MapGrid 自身原点，不含白膜偏移）。</summary>
    GridOrigin = 2,
}

/// <summary>
/// 皮肤数据（可 JSON 序列化，随 MapInfo/GridInfos 存档一起保存）。
/// </summary>
[System.Serializable]
public class SkinInfo
{
    /// <summary>编辑器侧预制体 GUID（撤销/定位用，运行时兜底加载用）</summary>
    [JsonProperty("SkinGuid")]
    public string SkinGuid;
    /// <summary>Addressables 地址（运行时加载首选键；编辑器笔刷自动写入，未注册的 Prefab 会自动注册）</summary>
    [JsonProperty("SkinAddress")]
    public string SkinAddress;
    /// <summary>预制体名（显示用；运行时加载的备用键）</summary>
    [JsonProperty("SkinName")]
    public string SkinName;
    /// <summary>皮肤相对白膜局部位置的偏移</summary>
    [JsonProperty("Offset")]
    public Vector3 Offset;
    /// <summary>皮肤局部欧拉角（相对模型原始朝向的增量）</summary>
    [JsonProperty("Rotation")]
    public Vector3 Rotation;
    /// <summary>相对模型自身原始缩放的倍率（全零视为 1，兼容旧数据）</summary>
    [JsonProperty("Scale")]
    public Vector3 Scale;
    /// <summary>笔刷换皮时是否隐藏了白膜（false = 保留原模型可见，用于对照/组合）</summary>
    [JsonProperty("TileHidden")]
    public bool TileHidden;
    /// <summary>
    /// 锚点语义（见 <see cref="SkinAnchor"/>）。
    /// 旧存档无此字段 → 反序列化为 TileCenter(0)，与修复前的摆放公式逐位一致，老图不会位移。
    /// </summary>
    [JsonProperty("Anchor")]
    public SkinAnchor Anchor = SkinAnchor.TileCenter;

    /// <summary>
    /// 是否启用"按地块类型自动贴合"（笔刷刷皮时写入）。
    /// 开启后由 <see cref="GridSkinData.RecomputeAutoFit"/> 按包围盒自动对齐：
    /// 地面块 → 包围盒**顶面**贴白膜顶面；高台块 → 包围盒**底面**贴地面平面。详见 Doc 25。
    /// </summary>
    [JsonProperty("AutoFit")]
    public bool AutoFit;
    /// <summary>
    /// 自动贴合算出的 Y 补偿（本地空间）。单独存放，**不污染** Offset，
    /// 这样美术的手工微调（Offset）与自动对齐互不干扰，重新贴合也不会覆盖手调值。
    /// </summary>
    [JsonProperty("AutoFitY")]
    public float AutoFitY;
    /// <summary>
    /// 是否启用**水平纠错**（笔刷刷皮时写入）：把皮肤包围盒中心对齐到格子中心。
    /// 用于纠正"素材导入时 prefab 根 transform 不是 (0,0,0)"导致的地块水平偏移。
    /// 旧存档无此字段 → false，不影响老图。
    /// </summary>
    [JsonProperty("AutoFitXZ")]
    public bool AutoFitXZ;
    /// <summary>水平纠错算出的 X 补偿（本地空间，独立于 Offset）</summary>
    [JsonProperty("AutoFitX")]
    public float AutoFitX;
    /// <summary>水平纠错算出的 Z 补偿（本地空间，独立于 Offset）</summary>
    [JsonProperty("AutoFitZ")]
    public float AutoFitZ;
    /// <summary>
    /// 生效的贴合补偿向量（受 AutoFit / AutoFitXZ 开关控制）。
    /// 关闭开关时补偿**不参与**摆放，但数值仍保留 —— 再打开即恢复，不会被清零。
    /// </summary>
    public Vector3 GetFitOffset()
    {
        return new Vector3(
            AutoFitXZ ? AutoFitX : 0f,
            AutoFit ? AutoFitY : 0f,
            AutoFitXZ ? AutoFitZ : 0f);
    }

    // ======== 模型原始变换基准（换皮必须保留，否则大模型被压扁/错位） ========
    // 记录 Prefab 实例化时的自带局部变换；应用变换 = 基准 + 用户增量，
    // 绝不套用白膜的 localScale / localRotation
    //（白膜如 tiles_ground 自带 m_LocalScale = (1, 0.6, 1) 是压扁的地块）。

    /// <summary>模型 Prefab 自带的局部位置（通常为 0）</summary>
    [JsonProperty("BaseLocalPos")]
    public Vector3 BaseLocalPos;
    /// <summary>模型 Prefab 自带的局部欧拉角（FBX 常见 -90/90，必须保留）</summary>
    [JsonProperty("BaseRotation")]
    public Vector3 BaseRotation;
    /// <summary>模型 Prefab 自带的局部缩放（FBX 常见 0.01/100，必须保留；全零视为 1）</summary>
    [JsonProperty("BaseScale")]
    public Vector3 BaseScale;
    /// <summary>基准是否已捕获（旧存档为 false：按单位变换处理，可用笔刷的"修复尺寸"按钮补全）</summary>
    [JsonProperty("BaseCaptured")]
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
            Anchor = Anchor,
            AutoFit = AutoFit,
            AutoFitY = AutoFitY,
            AutoFitXZ = AutoFitXZ,
            AutoFitX = AutoFitX,
            AutoFitZ = AutoFitZ,
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
        ApplyLocal(skin.transform, info, ResolveAnchorLocal(info));
    }

    /// <summary>核心摆放：位置 = 锚点 + 模型基准 + 用户偏移；旋转/缩放 = 基准 × 用户增量</summary>
    static void ApplyLocal(Transform skin, SkinInfo info, Vector3 anchor)
    {
        // 基准未捕获（旧存档）时按单位变换处理：不会被白膜缩放压扁；
        // 需要精确恢复 FBX 自带缩放的老图，可用笔刷面板的"修复尺寸"按钮补全基准
        skin.localPosition = anchor + info.GetBaseLocalPos() + info.Offset + info.GetFitOffset();
        skin.localRotation = Quaternion.Euler(info.GetBaseRotation()) * Quaternion.Euler(info.Rotation);
        skin.localScale = Vector3.Scale(info.GetBaseScale(), info.GetScaleMultiplier());
    }

    /// <summary>按数据摆放全部皮肤层（children 只取一次，避免每层重复遍历）</summary>
    public void ApplySkinTransform()
    {
        var children = GetSkinChildren();
        if (children.Count == 0) return;
        for (int i = 0; i < children.Count; i++)
        {
            var info = GetSkinInfoAt(i);
            if (children[i] != null && info != null)
                ApplyLocal(children[i].transform, info, ResolveAnchorLocal(info));
        }
    }

    // ================= 锚点 =================

    /// <summary>
    /// 解析某一层数据在本格 MapGrid 局部空间下的锚点位置（不含模型基准与用户偏移）。
    /// 三种语义见 <see cref="SkinAnchor"/>；旧数据（TileCenter）返回白膜 localPosition，
    /// 与修复前的公式逐位一致，保证老存档视觉不变。
    /// </summary>
    public Vector3 ResolveAnchorLocal(SkinInfo info)
    {
        var tile = FindTileModel();
        if (info == null) return tile != null ? tile.transform.localPosition : Vector3.zero;

        switch (info.Anchor)
        {
            case SkinAnchor.GridOrigin:
                return Vector3.zero;

            case SkinAnchor.TileSurface:
                if (tile == null) return Vector3.zero;
                var p = tile.transform.localPosition;
                // 只用实测顶面替换 Y，X/Z 沿用白膜（当前恒为 0，保留以兼容被移动过的白膜）
                return new Vector3(p.x, GetTileTopLocalY(tile.transform, transform), p.z);

            default: // SkinAnchor.TileCenter：旧语义
                return tile != null ? tile.transform.localPosition : Vector3.zero;
        }
    }

    /// <summary>
    /// 白膜**顶面**在 MapGrid 局部空间下的 Y（= 格子可视表面）。
    /// 用渲染器世界包围盒实测：对任意白膜 prefab（含旋转/非均匀缩放/多子网格）都成立，
    /// 也不会被 AutoBuild 里那段 localPosition 计算（含高台 0.1 偏差）带偏。
    /// 渲染器缺失时退回 Collider，再退回白膜 localPosition。
    /// </summary>
    public static float GetTileTopLocalY(Transform tile, Transform gridRoot)
    {
        if (tile == null || gridRoot == null) return 0f;

        var renderers = tile.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.sqrMagnitude > 0f)
            {
                var worldTop = new Vector3(gridRoot.position.x, b.max.y, gridRoot.position.z);
                return gridRoot.InverseTransformPoint(worldTop).y;
            }
        }

        var box = tile.GetComponent<BoxCollider>();
        if (box != null)
            return tile.localPosition.y + (box.center.y + box.size.y * 0.5f) * tile.localScale.y;

        return tile.localPosition.y;
    }

    /// <summary>
    /// 把 Anchor 从 from 切到 to 时，为了让皮肤**停在原地**，Offset.y 需要补偿的差值。
    /// （migration 用：oldPos = from + base + offset；newPos = to + base + offset' ⇒ offset' = offset + from - to）
    /// </summary>
    public float GetAnchorRebaseDeltaY(SkinAnchor from, SkinAnchor to)
    {
        var probe = new SkinInfo { Anchor = from };
        var a = ResolveAnchorLocal(probe);
        probe.Anchor = to;
        var b = ResolveAnchorLocal(probe);
        return a.y - b.y;
    }

    // ================= 自动贴合（按地块类型） =================

    /// <summary>本格是否为高台（FarAttackGrid）。高台皮肤按"底面贴地面平面"处理。</summary>
    public bool IsHighTile
    {
        get { var mg = GetComponent<MapGrid>(); return mg != null && mg.FarAttackGrid; }
    }

    /// <summary>地面平面在本格局部空间的 Y（地面块 = 0；高台块 = -抬升量）。</summary>
    public float GroundPlaneLocalY
    {
        get
        {
            var mg = GetComponent<MapGrid>();
            float world = mg != null ? mg.GroundPlaneWorldY : transform.position.y;
            var p = transform.InverseTransformPoint(new Vector3(transform.position.x, world, transform.position.z));
            return p.y;
        }
    }

    /// <summary>白膜顶面（= 地块可视表面）在本格局部空间的 Y。</summary>
    public float TileSurfaceLocalY
    {
        get
        {
            var tile = FindTileModel();
            return tile != null ? GetTileTopLocalY(tile.transform, transform) : 0f;
        }
    }

    /// <summary>
    /// 按地块类型把该层皮肤的**包围盒**对齐到白膜/地面平面（只动 Y，不动 X/Z）：
    ///
    /// - 地面块（FarAttackGrid=false）：包围盒**顶面**对齐白膜顶面（= 地面块顶面 y=0）
    ///   → 皮肤填满"表面以下"的体积，表面高度与白膜一致；
    /// - 高台块（FarAttackGrid=true）：包围盒**底面**对齐地面平面（= 相邻地面块顶面 y=0）
    ///   → 皮肤从地面平面向上生长，与相邻地面块在 y=0 处严丝合缝、不会出现空隙。
    ///
    /// 结果累加进 AutoFitY。**Offset.y 不参与贴合**：它是"贴合之上"的手工微调，
    /// 所以 AutoFitY 里要把 Offset.y 补回来，保证重复贴合幂等、也不会吃掉手调值。
    /// 包围盒在平移下大小不变，所以一次迭代即精确收敛。
    /// 无渲染体的皮肤（例如用来"只隐藏白膜"的空 prefab）直接跳过。
    /// </summary>
    public bool RecomputeAutoFit(int layer)
    {
        var info = GetSkinInfoAt(layer);
        if (info == null || (!info.AutoFit && !info.AutoFitXZ)) return false;
        if (!TryGetLayerWorldBounds(layer, out var b)) return false;

        Vector3 g = transform.position;
        bool changed = false;

        // ---- Y：按地块类型贴合（地面=顶面贴地块顶面；高台=底面贴地面平面）----
        if (info.AutoFit)
        {
            float localBottom = transform.InverseTransformPoint(new Vector3(g.x, b.min.y, g.z)).y;
            float localTop = transform.InverseTransformPoint(new Vector3(g.x, b.max.y, g.z)).y;

            bool high = IsHighTile;
            float target = high ? GroundPlaneLocalY : TileSurfaceLocalY;
            float delta = high ? target - localBottom : target - localTop;

            // + Offset.y：把手工微调从贴合基准里剔除（否则按一次"重新贴合"就把手调吃掉）；
            // 同时用它做幂等判定：已贴合 + 手调不变时 change==0，不会重复重算。
            float change = delta + info.Offset.y;
            if (Mathf.Abs(change) >= 1e-4f)
            {
                info.AutoFitY += change;
                changed = true;
            }
        }

        // ---- XZ：水平纠错（包围盒中心对齐格子中心）----
        // 素材导入时 prefab 根 transform 常不是 (0,0,0)，会让皮肤整体在水平面上偏移；
        // 这里按包围盒实测中心纠回来，与 Offset 解耦（Offset 仍是"纠错之上"的手工微调）。
        if (info.AutoFitXZ)
        {
            var tileCenter = GetTileCenterLocalXZ();
            var c = transform.InverseTransformPoint(new Vector3(b.center.x, g.y, b.center.z));
            float dx = tileCenter.x - c.x + info.Offset.x;
            float dz = tileCenter.y - c.z + info.Offset.z;
            if (Mathf.Abs(dx) >= 1e-4f) { info.AutoFitX += dx; changed = true; }
            if (Mathf.Abs(dz) >= 1e-4f) { info.AutoFitZ += dz; changed = true; }
        }

        if (changed) ApplySkinTransformAt(layer);
        return changed;
    }

    /// <summary>对全部皮肤层重跑自动贴合，返回实际发生调整的层数</summary>
    public int RecomputeAutoFitAll()
    {
        int n = 0;
        int layers = SkinLayerCount;
        for (int i = 0; i < layers; i++)
            if (RecomputeAutoFit(i)) n++;
        return n;
    }

    // ================= 诊断（锚点体检） =================

    /// <summary>格子表面的世界 Y（白膜顶面）；无白膜时为格子原点世界 Y</summary>
    public float GetTileSurfaceWorldY()
    {
        var tile = FindTileModel();
        if (tile == null) return transform.position.y;
        return transform.TransformPoint(new Vector3(0, GetTileTopLocalY(tile.transform, transform), 0)).y;
    }

    /// <summary>渲染器世界包围盒（无渲染器返回 false）</summary>
    public static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default(Bounds);
        if (go == null) return false;
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;
        bounds = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds);
        return true;
    }

    /// <summary>白膜包围盒中心在本格局部空间的 XZ（= 格子中心，正常情况接近 0）。</summary>
    public Vector2 GetTileCenterLocalXZ()
    {
        var tile = FindTileModel();
        if (tile == null || !TryGetWorldBounds(tile, out var b)) return Vector2.zero;
        var p = transform.InverseTransformPoint(new Vector3(b.center.x, transform.position.y, b.center.z));
        return new Vector2(p.x, p.z);
    }

    /// <summary>本格地面平面的世界 Y（高台 = 抬升量之下；地面 = 自身平面）</summary>
    public float GetGroundPlaneWorldY()
    {
        var mg = GetComponent<MapGrid>();
        return mg != null ? mg.GroundPlaneWorldY : transform.position.y;
    }

    /// <summary>
    /// 按"地块类型规则"计算的贴合偏差（世界单位，0 = 完美贴合）：
    /// 地面块比包围盒**顶面**与白膜顶面；高台块比包围盒**底面**与地面平面。
    /// 正 = 偏高，负 = 偏低。
    /// </summary>
    public float GetAutoFitDelta(int layer)
    {
        if (!TryGetLayerWorldBounds(layer, out var b)) return 0f;
        // 手工微调（Offset.y）是"贴合之上"的位移，比较时先剔除，否则会误报成没贴合
        var info = GetSkinInfoAt(layer);
        float manual = (info != null ? info.Offset.y : 0f) * transform.lossyScale.y;
        return IsHighTile
            ? b.min.y - GetGroundPlaneWorldY() - manual
            : b.max.y - GetTileSurfaceWorldY() - manual;
    }

    /// <summary>
    /// 水平纠错偏差（本地空间的 X/Z，0 = 包围盒中心与格子中心重合）：
    /// 正 = 皮肤偏右/偏前。已剔除手工微调 Offset.x/z。
    /// </summary>
    public Vector2 GetAutoFitDeltaXZ(int layer)
    {
        if (!TryGetLayerWorldBounds(layer, out var b)) return Vector2.zero;
        var info = GetSkinInfoAt(layer);
        var tileCenter = GetTileCenterLocalXZ();
        var c = transform.InverseTransformPoint(new Vector3(b.center.x, transform.position.y, b.center.z));
        float mx = info != null ? info.Offset.x : 0f;
        float mz = info != null ? info.Offset.z : 0f;
        return new Vector2(c.x - tileCenter.x - mx, c.z - tileCenter.y - mz);
    }

    /// <summary>某一层皮肤的渲染器世界包围盒；无渲染器返回 false</summary>
    public bool TryGetLayerWorldBounds(int layer, out Bounds bounds)
    {
        return TryGetWorldBounds(GetSkinChildAt(layer), out bounds);
    }

    /// <summary>
    /// 某一层皮肤底面相对格子表面的竖直偏差（世界单位）：正 = 浮空，负 = 下沉。
    /// 这是"皮肤 GO 偏离原地块、主要在 Y 轴"的可量化指标。
    /// </summary>
    public float GetLayerBottomDeltaToSurface(int layer)
    {
        if (!TryGetLayerWorldBounds(layer, out var b)) return 0f;
        return b.min.y - GetTileSurfaceWorldY();
    }

    /// <summary>
    /// 本格的**可视顶面**世界 Y：白膜顶面与全部皮肤包围盒顶面的较大者。
    /// 部署高亮/技能范围等"贴在地块表面"的贴片应该用它 ——
    /// 皮肤（尤其高台块）可能远高于白膜，用白膜顶面会把贴片埋进模型里。
    /// </summary>
    public float GetVisualTopWorldY()
    {
        float top = GetTileSurfaceWorldY();
        int layers = SkinLayerCount;
        for (int i = 0; i < layers; i++)
            if (TryGetLayerWorldBounds(i, out var b) && b.max.y > top) top = b.max.y;
        return top;
    }

    /// <summary>单层可读诊断文本（编辑器体检窗口 / 运行时诊断按钮共用）</summary>
    public string DescribeLayer(int layer)
    {
        var info = GetSkinInfoAt(layer);
        var skin = GetSkinChildAt(layer);
        if (info == null || skin == null)
            return $"第 {layer} 层：数据或实例缺失";

        var anchor = ResolveAnchorLocal(info);
        var tile = FindTileModel();
        float tileTopLocal = tile != null ? GetTileTopLocalY(tile.transform, transform) : 0f;
        bool high = IsHighTile;
        string boundsText = TryGetLayerWorldBounds(layer, out var b)
            ? $"boundsY=[{b.min.y:F3}, {b.max.y:F3}] 贴合Δ={(high ? b.min.y - GetGroundPlaneWorldY() : b.max.y - GetTileSurfaceWorldY()):+0.000;-0.000}({(high ? "底-地面平面" : "顶-地块顶面")})"
            : "boundsY=无渲染器";

        var dxz = GetAutoFitDeltaXZ(layer);
        return $"第 {layer} 层「{info.SkinName}」  地块={(high ? "高台" : "地面")}\n"
               + $"    锚点={info.Anchor}  锚点localY={anchor.y:F3}  白膜局部位置Y={(tile != null ? tile.transform.localPosition.y : 0f):F3}  白膜顶面localY={tileTopLocal:F3}\n"
               + $"    Base={info.GetBaseLocalPos()}  Offset={info.Offset}  Fit(Y {info.AutoFit}/{info.AutoFitY:F3}, XZ {info.AutoFitXZ}/{info.AutoFitX:F3},{info.AutoFitZ:F3})\n"
               + $"    水平偏差ΔXZ=({dxz.x:+0.000;-0.000}, {dxz.y:+0.000;-0.000})  合成localPos={skin.transform.localPosition}  {boundsText}";
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
        RecomputeAutoFitAll(); // 地块类型（地面/高台）可能已被改过，按新类型重贴
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
        // 键候选含"去扩展名"变体：Addressables 地址约定是资产路径去扩展名，
        // 而历史上运行时笔刷曾把带 .prefab 的资源路径写进 SkinAddress —— 原键在打包环境必然失败。
        foreach (var k in BuildLoadKeys(info))
        {
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

    /// <summary>
    /// 生成皮肤加载键候选（按优先级）：SkinAddress → SkinAddress 去扩展名 → SkinName → SkinName 去扩展名。
    /// 去重后返回，避免对同一键重复发起 Addressables 请求。
    /// </summary>
    public static IEnumerable<string> BuildLoadKeys(SkinInfo info)
    {
        if (info == null) yield break;
        var seen = new HashSet<string>();
        foreach (var raw in new[] { info.SkinAddress, info.SkinName })
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var key = raw.Replace('\\', '/');
            if (seen.Add(key)) yield return key;

            var ext = System.IO.Path.GetExtension(key);
            if (!string.IsNullOrEmpty(ext))
            {
                var stripped = key.Substring(0, key.Length - ext.Length);
                if (seen.Add(stripped)) yield return stripped;
            }
        }
    }

    /// <summary>
    /// 销毁一个皮肤实例（**唯一入口**）。
    /// Addressables 实例必须走 Addressables.ReleaseInstance：
    /// 直接 Destroy 会让 Addressables 内部的 instance→handle 映射永远不释放，
    /// 反复进图 / 反复刷皮会持续累积资源引用（R4）。
    /// 非 Addressables 实例（编辑器 GUID 兜底、PrefabUtility 实例化、场景模板）会返回 false，此时再 Destroy。
    /// 先摘出父级再销毁：避免父级同帧销毁连带，也让 GetSkinChildren 立刻反映真实层数。
    /// </summary>
    public static void DestroySkinInstance(GameObject go)
    {
        if (go == null) return;
        go.transform.SetParent(null, false);
        if (Application.isPlaying)
        {
            if (!Addressables.ReleaseInstance(go)) Destroy(go);
        }
        else DestroyImmediate(go);
    }

    /// <summary>运行时按数据还原主皮肤（第 0 层）</summary>
    public void ReapplyFromData()
    {
        if (string.IsNullOrEmpty(SkinLoadKey())) return;

        var old = GetSkinChild();
        if (old != null)
        {
            // v3.5：Destroy 是延迟到帧末的，旧实例在本帧内仍会被 GetSkinChildren 计入，
            // 紧接着的 ReapplyExtras/AddExtraFromData 会把"将死的旧主皮肤"当成有效层，
            // 导致层序错位/叠加层漏建。DestroySkinInstance 会先摘出层级树。
            // R4：同时保证 Addressables 实例走 ReleaseInstance，不泄漏引用。
            DestroySkinInstance(old);
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
            RecomputeAutoFitAll(); // 自动贴合：运行时按实际包围盒校正一次
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
