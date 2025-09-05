using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public class MapGrid : MonoBehaviour, IPointerClickHandler
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
    /// <summary>
    /// 能否移动
    /// </summary>
    public bool CanMove;

    public string MapUnitId;
    public GameObject go;
    public string Tag;
    public float ActiveTime;

    private Color _color;
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
        _color = Renderer.material.color == null ? Color.white : Renderer.material.color;
    }

    public void AutoBuild()
    {
        transform.position = new Vector3(X, FarAttackGrid ? 0.4f : 0, Y);
        if (transform.childCount > 0) Destroy(transform.GetChild(0).gameObject);
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
                go = ResHelper.Instantiate("tiles_canntset");
            else
                go = ResHelper.Instantiate("tiles_canntset");
        }
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0, -(go.GetComponent<BoxCollider>().center.y + go.GetComponent<BoxCollider>().size.y / 2) * go.transform.localScale.y, 0);
        BoxCollider = GetComponent<BoxCollider>();
        Renderer = GetComponentInChildren<Renderer>();
    }

    public void ChangeHighLight(bool bo)
    {
        if (Renderer != null)
            Renderer.material.color = bo ? new Color(0.458f, 1, 0.42f) : _color;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!FairyGUI.Stage.isTouchOnUI && BattleUI.UI_Battle.Instance != null)
        {
            if (Tile == null) return;
            if (Tile.Units.Count > 0)
                BattleUI.UI_Battle.Instance.ChooseUnit(Tile.Units.Last());
            else if (Tile.MidUnit != null)
                BattleUI.UI_Battle.Instance.ChooseUnit(Tile.MidUnit);
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
