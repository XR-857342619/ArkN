using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Tile: ITileData
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
    public bool UnitNotUseTile;

    public List<Unit> Units = new List<Unit>();
    public List<Unit> MidUnits = new List<Unit>();
    /// <summary>
    /// 广搜临时数据
    /// </summary>
    public Tile PreGrid;
    public bool Passable {get => passable; set => passable = value;}
    public bool passable;

    public float PassCost { get => passCost; set => passCost = value; }
    public float passCost;

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
        passable = !FarAttackGrid;
        passCost = FarAttackGrid ? 1000 : 0;
    }

    public bool CanSet(UnitData unitData)
    {
        //if (Units.Any(x => x.UnitData.Id == unitData.Id)) return true;
        UnitNotUseTile = unitData.NotUseTile;
        if (BattleManager.Instance.IsNoLimitBuild) return true;
        else
        {
            int tileUnitsCount = this.Units?.Count(X => X.UnitData.NotUseTile == true)?? 0;
            bool buildcount = this.Units.Count >= tileUnitsCount + 1;
            bool tileUnits = this.Units.Count > 0 ? !this.Units.Any(X => X.UnitData.NotUseTile == true) : false;
            //if (buildcount && tileUnits && !UnitNotUseTile) return false;
            if (CanBuildUnit)
            {
                if (Battle.MapData.NoBuildLimit) return true;
                return GetUnitCanSetData(unitData);
            }
            if (buildcount && tileUnits && !UnitNotUseTile) return false;
            return false;
        }
    }

    public bool GetUnitCanSetData(UnitData unitData)    
    {
        List<string> CanSetTags = unitData.CanSetPos.ToList();
        
        bool CanSetHigh = CanSetTags.Contains("高台位") || CanSetTags.Contains("全地形");
        bool CanSetGround = CanSetTags.Contains("地面位") || CanSetTags.Contains("全地形");

        if (FarAttackGrid && !CanSetHigh) return false;
        if (!FarAttackGrid && !CanSetGround) return false;

        bool CanSetNoUnit = CanSetTags.Contains("仅无单位");
        bool CanSetNoEnemy = CanSetTags.Contains("仅无敌人");
        bool CanSetNoCommon = CanSetTags.Contains("仅无中立");
        bool CanSetNoOperator = CanSetTags.Contains("仅无干员");

        bool CanSetHaveUnit = CanSetTags.Contains("仅有单位");
        bool CanSetHaveEnemy = CanSetTags.Contains("仅有敌人");
        bool CanSetHaveCommon = CanSetTags.Contains("仅有中立");
        bool CanSetHaveOperator = CanSetTags.Contains("仅有干员");

        UnitNotUseTile = CanSetHaveOperator || CanSetHaveCommon;

        //bool UseTrapMode = CanSetNoUnit || CanSetNoEnemy || CanSetNoOperator || CanSetNoCommon || CanSetHaveUnit || CanSetHaveEnemy || CanSetHaveOperator || CanSetHaveCommon;

        if (CanSetNoUnit || CanSetNoEnemy || CanSetNoOperator || CanSetNoCommon || CanSetHaveUnit || CanSetHaveEnemy || CanSetHaveOperator || CanSetHaveCommon)
        {
            HashSet<Unit> TileUnits = Battle.FindAll(new Vector2Int(X, Y), 15);

            bool HaveUnit = TileUnits.Count > 0;
            //bool HaveOperator = TileUnits.Any(x => x.UnitData.Team == 0);
            bool HaveOperator = Units.Any(x => x.UnitData.Team == 0);
            bool HaveEnemy = TileUnits.Any(x => x.UnitData.Team == 1);
            bool HaveCommon = Units.Any(x => x.UnitData.Team == 2);

            //if (HaveUnit && (CanSetNoUnit || !CanSetHaveUnit)) return false;
            if (HaveUnit ? CanSetNoUnit : CanSetHaveUnit) return false;
            //if (!HaveUnit && CanSetHaveUnit) return false;
            if (HaveOperator ? CanSetNoOperator : CanSetHaveOperator) return false;
            if (HaveEnemy ? CanSetNoEnemy : CanSetHaveEnemy) return false;
            if (HaveCommon ? CanSetNoCommon : CanSetHaveCommon) return false;
        };

        //bool UseTokenMode = CanSetTags.Any(x => x.StartsWith("仅在单位攻击范围内:"));
        string UnitName = CanSetTags.FirstOrDefault(x => x.StartsWith("仅在单位攻击范围内:"));
        UnitName = !string.IsNullOrEmpty(UnitName) ? UnitName.Split(':')[1] : null;

        bool UseTokenMode = !string.IsNullOrEmpty(UnitName);
    
        if (UseTokenMode) 
        {
            List<Unit> Targets = Battle.AllUnits.FindAll(x => x.UnitData.Name == UnitName);
            List<Vector2Int> TargetAtkPos = new List<Vector2Int>();
            Vector2Int thisTilePos = new Vector2Int(X, Y);
            foreach (Unit target in Targets)
            {
                TargetAtkPos.Add(target.GridPos);

                if (target is null) return false;

                var sk = target.GetNowUseingSkill();
                if (sk == null)
                    sk = target.Skills[0];
                if (sk != null && sk.AttackPoints != null)
                    TargetAtkPos.AddRange(sk.AttackPoints);
            }
            if (!TargetAtkPos.Contains(thisTilePos)) return false;
        };

        return true;
    }

    public void ChangeToDefault()
    {
        Pos = new Vector3(X, 0, Y);
    }
}