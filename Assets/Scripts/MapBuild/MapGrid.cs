using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public class MapGrid : MonoBehaviour, IPointerClickHandler, ITileData
{
    public int X, Y;
    /// <summary>
    /// 能否造单位
    /// </summary>
    public bool CanBuildUnit;
    /// <summary>
    /// 远程格子
    /// </summary>
    public bool FarAttackGrid;
    public bool Passable { get => !FarAttackGrid; }
    /// <summary>
    /// 能否移动
    /// </summary>
    public bool CanMove;

    public float PassCost { get => passCost; }
    public float passCost = 0;

    /// <summary>
    /// 高台格子（FarAttackGrid）相对地面平面的抬升量（AutoBuild 摆位用）。
    /// 皮肤"自动贴合"也用它反推高台格子的地面平面：地面平面世界Y = transform.position.y - 抬升量。
    /// </summary>
    public const float HighTileLift = 0.4f;

    /// <summary>
    /// 本格"地面平面"的世界 Y：
    /// - 地面块 → 就是自身平面（白膜顶面所在平面）；
    /// - 高台块 → 抬升量之下，即相邻地面块顶面所在平面。
    /// </summary>
    public float GroundPlaneWorldY
    {
        get { return FarAttackGrid ? transform.position.y - HighTileLift : transform.position.y; }
    }

    public string MapUnitId;
    public GameObject go;
    public string Tag;
    public float ActiveTime;

    private Color _color;
    private Material _material;
    //public TileTypeEnum TileType;

    //public int ConfigId;

    BoxCollider BoxCollider;

    Renderer Renderer;
    [HideInInspector]
    public Tile Tile;

    /// <summary>可部署范围高亮色（白膜染色与皮肤染色共用）</summary>
    static readonly Color HighlightColor = new Color(0.458f, 1f, 0.42f, 1f);

    /// <summary>皮肤高亮可写的材质颜色属性名候选（不同 shader 命名不同）</summary>
    static readonly string[] HighlightColorProps = { "_Color", "_BaseColor", "_TintColor" };

    MaterialPropertyBlock _skinBlock;
    bool _skinHighlightOn;

    private void Awake()
    {
        BoxCollider = GetComponent<BoxCollider>();
        CacheTileRenderer();
    }

    /// <summary>
    /// 缓存**白膜**的 Renderer（ChangeHighLight 染色用）。
    /// 旧实现是 GetComponentInChildren&lt;Renderer&gt;()（只找 active）：一旦皮肤把白膜 Renderer 关闭，
    /// 就会抓到皮肤自己的 Renderer，高亮变成染皮肤材质，且白膜恢复后颜色错乱。
    /// </summary>
    void CacheTileRenderer()
    {
        var skin = GetComponent<GridSkinData>();
        var tile = skin != null ? skin.FindTileModel() : go;
        if (tile == null) tile = gameObject;
        Renderer = tile.GetComponentInChildren<Renderer>(true);
    }
    private void Start()
    {
        if (Renderer == null) CacheTileRenderer(); // 场景手工摆的格子 Awake 时可能还没白膜
        if (Renderer != null)
        {
            _material = Renderer.material; // 仅克隆一次材质实例，后续高亮复用
            _color = _material.color;
        }
        TagDepthOccluder();
    }

    public void AutoBuild()
    {
        transform.position = new Vector3(X, FarAttackGrid ? HighTileLift : 0, Y);
        // === 皮肤笔刷补丁：销毁旧白膜时跳过皮肤子物体（Skin_Model / Skin_Model_N 叠加层），避免误删换皮 ===
        Transform oldChild = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (GridSkinData.IsSkinChild(c)) continue;
            oldChild = c;
            break;
        }
        if (oldChild != null)
        {
            if (Application.isPlaying) Destroy(oldChild.gameObject);
            else DestroyImmediate(oldChild.gameObject);
        }
        if (CanBuildUnit)
        {
            if (FarAttackGrid)
                go = ResHelper.Instantiate("tiles_high");
            else
                go = ResHelper.Instantiate("tiles_ground");
        }
        else
        {
            if (FarAttackGrid)
                go = ResHelper.Instantiate("tiles_canntset_high");
            else
                go = ResHelper.Instantiate("tiles_canntset_ground");
        }
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0, -(go.GetComponent<BoxCollider>().center.y + go.GetComponent<BoxCollider>().size.y / 2) * go.transform.localScale.y + go.transform.localPosition.y, 0);
        // === 皮肤笔刷补丁：白膜固定在 sibling 0，皮肤层保持挂载顺序（层序=兄弟顺序）不受重建影响 ===
        go.transform.SetSiblingIndex(0);
        BoxCollider = GetComponent<BoxCollider>();
        // 直接缓存新实例化物体的 Renderer，避免 Destroy 旧子物体但尚未销毁时拿到旧 Renderer
        Renderer = go.GetComponentInChildren<Renderer>();
        _material = Renderer != null ? Renderer.material : null;
        if (_material != null) _color = _material.color;

        // === 皮肤笔刷补丁：白膜重建后恢复皮肤状态（重新隐藏显示、按数据复位对位），不影响任何判定 ===
        var skinData = GetComponent<GridSkinData>();
        if (skinData != null) skinData.OnTileRebuilt();

        // === 高台深度遮挡：给高台格子生成只写深度的代理网格，让 Spine 被高台真实遮挡 ===
        TagDepthOccluder();
    }

    /// <summary>
    /// 高台格子（FarAttackGrid）生成「只写深度」的代理网格（high 层），由
    /// SpineOverlayCameraUtil 的 DeviceDepthCamera 渲染 —— 于是 Spine 单位能被高台真实遮挡。
    /// 代理只带 MeshFilter/MeshRenderer、没有 Collider，且挂在 go 之下，
    /// 因此不影响原有 Layer、物理碰撞与鼠标拾取射线。
    /// 总开关：FeatureFlags.HighTileDepthOcclusion。
    /// </summary>
    void TagDepthOccluder()
    {
        if (!FarAttackGrid) return;
        // 跑过 AutoBuild 的格子用 go；场景里手工摆的格子 go 可能为空，兜底用 MapGrid 自身
        SpineDepthOccluderUtil.Build(go != null ? go : gameObject);
    }

    /// <summary>
    /// 可部署范围高亮。
    /// 1) 白膜：改材质实例颜色（原逻辑，Renderer 已由 CacheTileRenderer 保证是白膜）；
    /// 2) 白膜被皮肤隐藏时：白膜染色看不见，此时把高亮转移到**皮肤各层**，
    ///    用 MaterialPropertyBlock 只改渲染状态、不产生材质实例（不会像 Renderer.material 那样克隆材质）。
    /// 白膜可见时只染白膜，避免"白膜+皮肤"双重叠色。
    /// </summary>
    public void ChangeHighLight(bool bo)
    {
        if (_material != null)
            _material.color = bo ? HighlightColor : _color;
        ApplySkinHighlight(bo);
    }

    /// <summary>皮肤高亮：仅在白膜被皮肤隐藏时生效；关闭时清空材质属性块</summary>
    void ApplySkinHighlight(bool on)
    {
        var data = GetComponent<GridSkinData>();
        var children = data != null ? data.GetSkinChildren() : null;
        if (children == null || children.Count == 0)
        {
            _skinHighlightOn = false;
            return;
        }

        bool need = on && data.Data != null && data.Data.TileHidden;
        if (!need && !_skinHighlightOn) return; // 无变化，跳过（每格只在切换时写一次）
        _skinHighlightOn = need;

        for (int i = 0; i < children.Count; i++)
        {
            var go = children[i];
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!need) { r.SetPropertyBlock(null); continue; }

                var mat = r.sharedMaterial;
                if (mat == null) continue;
                if (_skinBlock == null) _skinBlock = new MaterialPropertyBlock();
                r.GetPropertyBlock(_skinBlock); // 保留渲染器上已有的属性块内容
                bool any = false;
                for (int k = 0; k < HighlightColorProps.Length; k++)
                {
                    var prop = HighlightColorProps[k];
                    if (!mat.HasProperty(prop)) continue;
                    _skinBlock.SetColor(prop, HighlightColor);
                    any = true;
                }
                // shader 没有可写的颜色属性（如某些自定义/Spine 材质）→ 跳过，不影响其它层
                r.SetPropertyBlock(any ? _skinBlock : null);
            }
        }
    }

    /// <summary>
    /// 高亮/范围贴片的落点：GetPos() 再把 Y 抬到**可视顶面**（白膜顶面与皮肤顶面取高者）。
    /// 皮肤（尤其高台块）可能远高于白膜，直接用 GetPos() 会把贴片埋进模型里。
    /// </summary>
    public Vector3 GetHighlightPos()
    {
        var pos = GetPos();
        var skin = GetComponent<GridSkinData>();
        if (skin != null && skin.SkinLayerCount > 0)
            pos.y = skin.GetVisualTopWorldY() + 0.02f;
        return pos;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!FairyGUI.Stage.isTouchOnUI && BattleUI.UI_Battle.Instance != null)
        {
            if (Tile == null) return;
            if (Tile.Units.Count > 0)
                BattleUI.UI_Battle.Instance.ChooseUnit(Tile.Units);
            else if (Tile.MidUnits.Count > 0)
                BattleUI.UI_Battle.Instance.ChooseUnit(Tile.MidUnits);
        }
    }

    public Vector3 GetPos()
    {
        if (BoxCollider != null)
        {
            //var result = BoxCollider.center + transform.position;
            //result.y += BoxCollider.bounds.size.y / 2;
            return transform.TransformPoint(BoxCollider.center + new Vector3(0, BoxCollider.size.y / 2, 0));
        }
        return transform.position;
    }
}
