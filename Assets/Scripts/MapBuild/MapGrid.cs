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

    private void Awake()
    {
        BoxCollider = GetComponent<BoxCollider>();
        Renderer = GetComponentInChildren<Renderer>();
    }
    private void Start()
    {
        if (Renderer != null)
        {
            _material = Renderer.material; // 仅克隆一次材质实例，后续高亮复用
            _color = _material.color;
        }
    }

    public void AutoBuild()
    {
        transform.position = new Vector3(X, FarAttackGrid ? 0.4f : 0, Y);
        if (transform.childCount > 0)
        {
            var oldChild = transform.GetChild(0).gameObject;
            if (Application.isPlaying) Destroy(oldChild);
            else DestroyImmediate(oldChild);
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
        BoxCollider = GetComponent<BoxCollider>();
        // 直接缓存新实例化物体的 Renderer，避免 Destroy 旧子物体但尚未销毁时拿到旧 Renderer
        Renderer = go.GetComponentInChildren<Renderer>();
        _material = Renderer != null ? Renderer.material : null;
        if (_material != null) _color = _material.color;
    }

    public void ChangeHighLight(bool bo)
    {
        if (_material != null)
            _material.color = bo ? new Color(0.458f, 1, 0.42f) : _color;
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
