using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Tile
{
    public Battle Battle => Map.Battle;
    public Map Map;
    public MapGrid MapGrid;
    public Vector3 Pos;
    public int X, Y;
    /// <summary>
    /// 能否造单位
    /// </summary>
    public bool CanBuildUnit;
    /// <summary>
    /// 远程格子
    /// </summary>
    public bool FarAttackGrid;

    public List<Unit> Units = new List<Unit>();
    public List<Unit> MidUnits = new List<Unit>();
    /// <summary>
    /// 广搜临时数据
    /// </summary>
    public Tile PreGrid;

    public string Tag;
    public float ActiveTime;

    public virtual void Update()
    {
        //CanSet(unit);
    }

    public virtual void Init(Map map, MapGrid mapGrid)
    {
        this.MapGrid = mapGrid;
        mapGrid.Tile = this;
        this.Map = map;
        this.MapGrid = mapGrid;
        this.Pos = mapGrid.GetPos();
        this.X = mapGrid.X;
        this.Y = mapGrid.Y;
        this.CanBuildUnit = mapGrid.CanBuildUnit;
        this.FarAttackGrid = mapGrid.FarAttackGrid;
        this.Tag = mapGrid.Tag;
    }

    public bool CanSet(UnitData unitData)
    {
        //if (Units.Any(x => x.UnitData.Id == unitData.Id)) return true;
        if (BattleManager.Instance.IsNoLimitBuild) return true;
        else
        {
            int tileUnitsCount = this.Units?.Count(X => X.UnitData.NotUseTile == true)?? 0;
            bool buildcount = this.Units.Count >= tileUnitsCount + 1;
            bool tileUnits = this.Units.Count > 0 ? !this.Units.Any(X => X.UnitData.NotUseTile == true) : false;
            if (buildcount && tileUnits && !unitData.NotUseTile) return false;
            if (CanBuildUnit)
            {
                if (Battle.MapData.NoBuildLimit) return true;
                if (FarAttackGrid)
                {
                    return unitData.CanSetHigh;
                }
                else
                    return unitData.CanSetGround;
            }
            return false;
        }
    }

    public void ChangeToDefault()
    {
        Pos = new Vector3(X, 0, Y);
    }
}