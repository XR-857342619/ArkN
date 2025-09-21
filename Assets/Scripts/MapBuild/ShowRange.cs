using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 地块技能范围显示组件（支持多实例共存，适配单位坐标偏移）
/// </summary>
public class ShowRange : MonoBehaviour
{
    // 单位相关配置（每个组件对应一个单位的范围，实现多单位区分）
    [Header("单位关联配置")]
    public int unitUniqueIndex; // 单位唯一标识（用于区分不同单位的范围组件）

    // 位置与范围核心配置
    [Header("位置与范围配置")]
    public Vector2 unitWorldPos; // 单位世界坐标（需要偏移时使用）
    public Vector2Int unitGridPos; // 单位网格坐标（无需偏移时使用）
    public bool useGridPos = false; // 是否使用网格坐标（true=无需偏移，false=需计算偏移）
    public float rangeRadius; // 圆形范围半径（>0时显示圆形）
    public List<Vector2> polygonRange; // 多边形范围点集合（圆形无效时生效）

    // 范围显示UI引用（子物体Sprite）
    [Header("范围显示引用")]
    public GameObject targetObject;
    public float angle;
    public GameObject LineT;
    public GameObject LineL;
    public GameObject LineR;
    public GameObject LineB;
    public GameObject LineC; // 圆形范围线
    public GameObject PointTR;
    public GameObject PointBR;
    public GameObject PointBL;
    public GameObject PointTL;

    // 颜色自定义
    [Header("颜色自定义")]
    public string colorHex = "#6385FF"; // GRB十六进制颜色
    public float alpha = 0.5f; // 透明度（0~1）
    private Color rangeColor;

    public void Init()
    {
        // 解析十六进制颜色
        if (ColorUtility.TryParseHtmlString(colorHex, out rangeColor))
        {
            if (alpha >= 0 && alpha <= 1) rangeColor.a = alpha;
            else TipManager.Instance.ShowTip("自定义技能范围颜色透明度设置错误！");
            SetRangeObjectsColor(rangeColor);
        }
        else
            TipManager.Instance.ShowTip("自定义技能范围颜色解析失败！");
        // 计算并应用偏移
        ApplyUnitOffset();
        // 初始化范围显示
        InitRangeDisplay();
    }

    /// <summary>
    /// 计算单位与地块中心的偏移并应用
    /// </summary>
    private void ApplyUnitOffset()
    {
        if (targetObject == null) return;

        // 获取地块中心世界坐标（假设targetObject为地块根节点）
        Vector2 plotCenterPos = new Vector2(targetObject.transform.position.x, targetObject.transform.position.z);
        // 确定单位最终世界坐标
        Vector2 unitFinalPos = useGridPos ? (Vector2)unitGridPos : unitWorldPos;
        // 计算偏移量（单位到地块中心的向量）
        Vector2 offset = unitFinalPos - plotCenterPos;

        // 对所有范围显示子物体应用偏移
        ApplyOffsetToSingleObject(LineT, offset);
        ApplyOffsetToSingleObject(LineL, offset);
        ApplyOffsetToSingleObject(LineR, offset);
        ApplyOffsetToSingleObject(LineB, offset);
        ApplyOffsetToSingleObject(LineC, offset);
        ApplyOffsetToSingleObject(PointTR, offset);
        ApplyOffsetToSingleObject(PointBR, offset);
        ApplyOffsetToSingleObject(PointBL, offset);
        ApplyOffsetToSingleObject(PointTL, offset);
    }

    /// <summary>
    /// 给单个范围显示物体应用偏移
    /// </summary>
    private void ApplyOffsetToSingleObject(GameObject obj, Vector2 offset)
    {
        if (obj != null)
        {
            Transform objTrans = obj.transform;
            objTrans.localPosition = new Vector3(
                objTrans.localPosition.x + offset.x,
                objTrans.localPosition.y,
                objTrans.localPosition.z + offset.y
            );
        }
    }

    /// <summary>
    /// 初始化范围显示（圆形/多边形）
    /// </summary>
    private void InitRangeDisplay()
    {
        // 隐藏所有显示物体，避免初始混乱
        //HideAllRangeObjects();

        // 1. 圆形范围逻辑（优先级：圆形>多边形）
        if (rangeRadius > 0.01f)
        {
            if (LineC != null)
            {
                LineC.SetActive(true);
                LineC.transform.localScale = new Vector3(rangeRadius, rangeRadius, 1);
                HideAllRangeObjects();
            }
        }
        // 2. 多边形范围逻辑
        else if (polygonRange != null && polygonRange.Count > 0)
        {
            // 根据多边形范围点，显示对应的线和点
            if (polygonRange.Contains(GetRelativePos(0, 1))) LineT.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(-1, 1))) PointTL.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(1, 1))) PointTR.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(0, -1))) LineB.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(-1, -1))) PointBL.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(1, -1))) PointBR.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(-1, 0))) LineL.SetActive(false);
            if (polygonRange.Contains(GetRelativePos(1, 0))) LineR.SetActive(false);
        }
    }

    /// <summary>
    /// 获取“相对于地块中心”的范围点（统一多边形范围判断基准）
    /// </summary>
    private Vector2 GetRelativePos(int xOffset, int yOffset)
    {
        Vector3 plotCenter = targetObject.transform.position;
        return new Vector2(plotCenter.x + xOffset, plotCenter.z + yOffset);
    }

    /// <summary>
    /// 隐藏所有范围显示物体
    /// </summary>
    private void HideAllRangeObjects()
    {
        LineT?.SetActive(false);
        LineL?.SetActive(false);
        LineR?.SetActive(false);
        LineB?.SetActive(false);
        //LineC?.SetActive(false);
        PointTR?.SetActive(false);
        PointBR?.SetActive(false);
        PointBL?.SetActive(false);
        PointTL?.SetActive(false);
    }

    /// <summary>
    /// 设置所有范围显示物体的颜色
    /// </summary>
    private void SetRangeObjectsColor(Color color)
    {
        SetObjectColor(LineT, color);
        SetObjectColor(LineL, color);
        SetObjectColor(LineR, color);
        SetObjectColor(LineB, color);
        SetObjectColor(LineC, color);
        SetObjectColor(PointTR, color);
        SetObjectColor(PointBR, color);
        SetObjectColor(PointBL, color);
        SetObjectColor(PointTL, color);
    }

    /// <summary>
    /// 设置单个物体的Sprite颜色
    /// </summary>
    private void SetObjectColor(GameObject obj, Color color)
    {
        if (obj != null)
        {
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
            
        }
    }

    /// <summary>
    /// 外部更新范围（如单位移动后）
    /// </summary>
    public void UpdateRange(Vector2 newUnitPos, float newRadius = -1, List<Vector2> newPolygon = null)
    {
        if (!useGridPos) unitWorldPos = newUnitPos;
        if (newRadius > 0.01f) rangeRadius = newRadius;
        if (newPolygon != null) polygonRange = newPolygon;

        ApplyUnitOffset();
        InitRangeDisplay();
    }
}