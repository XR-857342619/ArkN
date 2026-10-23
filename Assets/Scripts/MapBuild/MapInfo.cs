using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapInfo
{
    public string MapName;
    public Vector3 CameraPos;
    public string Description;
    public string Scene;
    public int InitHp;
    public int InitCost;
    public int MaxBuildCount;
    public int MaxCost;
    //public string MapModel;
    public List<string> Contracts;
    public GridInfo[,] GridInfos;
    public List<UnitInfo> UnitInfos;
    public List<PathInfo> PathInfos;
    public List<WaveInfo> WaveInfos;
    public List<OverwriteUnitInfo> UnitOvDatas;
    public bool NoBuildLimit;
    public int BoxCount;

}

public class GridInfo
{
    public int X, Y;
    public bool CanBuildUnit;
    public bool FarAttack;
    public bool CanMove;
    /// <summary>皮肤数据（皮肤笔刷写入，随 JSON 存档；旧存档该字段为 null，自动兼容）</summary>
    public SkinInfo Skin;
    /// <summary>组合模式叠加的皮肤数据（主皮肤在 Skin；旧存档该字段为 null，自动兼容）</summary>
    public List<SkinInfo> SkinExtras;
}

public class UnitInfo
{
    public string UnitId;
    public int X, Y;
    public string Tag;
    public float ActiveTime;
    public Vector2 Direction;
    public float LifeTime;
}

public class WaveInfo
{
    public int? UnitId;
    public string sUnitId;
    public float Delay;
    public float GapTime;
    public int Count;
    public string Path;
    public float OffsetX;
    public float OffetsetY;
    public int CheckPoint;
    public string Tag;
    public bool UnAppear;
}

public class PathInfo
{
    public string Name;
    public bool FlyPath;
    public List<PathPoint> Path;
    //public List<PathPoint> fixedPath;
    //public List<float> distances;
    //public float time;
    public float length;
    //public Dictionary<Vector3, float> waitTime;
}
public class OverwriteUnitInfo
{
    public string UnitId;
    public int Atk, Hp, Def, MagDef;
    public float Speed, Agi;
}

public class PreviewInfos
{
    public List<(float,float)> TimePoints;
    public List<List<PathPoint>> Points;
}