using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


public class UnitModel:MonoBehaviour
{
    public Unit Unit;

    public virtual void Init(Unit unit)
    {
        transform.position = unit.Position;
    }

    /// <summary>
    /// 根据单位所在地块模型的高度，自动对齐模型 Y 轴，避免与地块穿模。
    /// 参照 NormalModel.AlignHeight() 的实现，默认由 Unit.CreateModel 与单位位置变更时调用。
    /// </summary>
    public virtual void AlignHeight()
    {
        if (Unit == null || Unit.Battle == null || Unit.Battle.Map == null) return;

        int x = Unit.GridPos.x;
        int y = Unit.GridPos.y;
        if (x < 0 || y < 0 || x >= Unit.Battle.Map.Tiles.GetLength(0) || y >= Unit.Battle.Map.Tiles.GetLength(1)) return;

        Tile tile = Unit.Battle.Map.Tiles[x, y];
        if (tile == null || tile.MapGrid == null) return;

        float offsetY = 0f;
        GameObject tileModel = tile.MapGrid.go;
        if (tileModel != null)
        {
            offsetY = tileModel.transform.position.y + tileModel.transform.localScale.y / 2f;
        }

        transform.position = new Vector3(transform.position.x, offsetY + 0.01f, transform.position.z);
        Unit.Position = new Vector3(Unit.Position.x, transform.position.y, Unit.Position.z);
    }

    public virtual Vector3 GetModelPositon()
    {
        return transform.position;
    }

    public virtual Vector3 GetPoint(string name)
    {
        return transform.position;
    }
    public virtual void SetModelPositon(Vector3 position)
    {
        transform.position = position;
    }

    public virtual void BreakAnimation()
    {

    }

    public virtual float GetSkillDelay(string[] animationName, string[] lastState, out float fullDuration, out float beginDuration)
    {
        fullDuration = 0;
        beginDuration = 0;
        return 0;
    }

    public virtual float GetAnimationDuration(string animationName)
    {
        return 0;
    }

    public virtual void SetColor(Color color)
    {

    }

    public virtual void ResetColor()
    {

    }

    public virtual bool isOriginalColor()
    {
        return false;
    }

    public void ShowCrit(DamageInfo damage)
    {
        BattleUI.UI_Battle.Instance.ShowDamageText(damage, 0, transform.position.WorldToUI());
    }

    public void ShowHeal(DamageInfo heal)
    {
        BattleUI.UI_Battle.Instance.ShowDamageText(heal, 1, transform.position.WorldToUI());
    }
    public void ShowMiss()
    {
        BattleUI.UI_Battle.Instance.ShowDamageText("Miss", 2, transform.position.WorldToUI());
    }
    public void ShowPower(float count)
    {
        BattleUI.UI_Battle.Instance.ShowDamageText(count.ToString("F0"), 3, transform.position.WorldToUI());
    }
    public virtual void ChangeToEnd()
    {

    }
    public virtual void hideModel()
    {
        
    }

    public virtual void showModel()
    {
        
    }

    public virtual void hideShadow()
    {
        
    }

    public virtual void showShadow()
    {

    }
}

