using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public class MapInfo
{
    [JsonProperty("MapName")]
    public string MapName;
    [JsonProperty("CameraPos")]
    public Vector3 CameraPos;
    [JsonProperty("Description")]
    public string Description;
    [JsonProperty("Scene")]
    public string Scene;
    /// <summary>地图使用的 BGM：存 Addressables 完整地址（打了 BGM 标签的音频资源）。</summary>
    [JsonProperty("BGM")]
    public string BGM;
    [JsonProperty("InitHp")]
    public int InitHp;
    [JsonProperty("InitCost")]
    public int InitCost;
    [JsonProperty("MaxBuildCount")]
    public int MaxBuildCount;
    [JsonProperty("MaxCost")]
    public int MaxCost;
    //public string MapModel;
    [JsonProperty("Contracts")]
    public List<string> Contracts;
    [JsonProperty("GridInfos")]
    public GridInfo[,] GridInfos;
    [JsonProperty("UnitInfos")]
    public List<UnitInfo> UnitInfos;
    [JsonProperty("PathInfos")]
    public List<PathInfo> PathInfos;
    [JsonProperty("WaveInfos")]
    public List<WaveInfo> WaveInfos;
    [JsonProperty("UnitOvDatas")]
    public List<OverwriteUnitInfo> UnitOvDatas;
    [JsonProperty("NoBuildLimit")]
    public bool NoBuildLimit;
    [JsonProperty("BoxCount")]
    public int BoxCount;
}

public class GridInfo
{
    [JsonProperty("X")]
    public int X;
    [JsonProperty("Y")]
    public int Y;
    [JsonProperty("CanBuildUnit")]
    public bool CanBuildUnit;
    [JsonProperty("FarAttack")]
    public bool FarAttack;
    [JsonProperty("CanMove")]
    public bool CanMove;
    /// <summary>皮肤数据（皮肤笔刷写入，随 JSON 存档；旧存档该字段为 null，自动兼容）</summary>
    [JsonProperty("Skin")]
    public SkinInfo Skin;
    /// <summary>组合模式叠加的皮肤数据（主皮肤在 Skin；旧存档该字段为 null，自动兼容）</summary>
    [JsonProperty("SkinExtras")]
    public List<SkinInfo> SkinExtras;
}

public class UnitInfo
{
    [JsonProperty("UnitId")]
    public string UnitId;
    [JsonProperty("X")]
    public int X;
    [JsonProperty("Y")]
    public int Y;
    [JsonProperty("Tag")]
    public string Tag;
    [JsonProperty("ActiveTime")]
    public float ActiveTime;
    [JsonProperty("Direction")]
    public Vector2 Direction;
    [JsonProperty("LifeTime")]
    public float LifeTime;
}

public class WaveInfo
{
    [JsonProperty("UnitId")]
    public int? UnitId;
    [JsonProperty("sUnitId")]
    public string sUnitId;
    [JsonProperty("Delay")]
    public float Delay;
    [JsonProperty("GapTime")]
    public float GapTime;
    [JsonProperty("Count")]
    public int Count;
    [JsonProperty("Path")]
    public string Path;
    [JsonProperty("OffsetX")]
    public float OffsetX;
    [JsonProperty("OffetsetY")]
    public float OffetsetY;
    [JsonProperty("CheckPoint")]
    public int CheckPoint;
    [JsonProperty("Tag")]
    public string Tag;
    [JsonProperty("UnAppear")]
    public bool UnAppear;
}

public class PathInfo
{
    [JsonProperty("Name")]
    public string Name;
    [JsonProperty("FlyPath")]
    public bool FlyPath;
    [JsonProperty("Path")]
    public List<PathPoint> Path;
    //public List<PathPoint> fixedPath;
    //public List<float> distances;
    //public float time;
    [JsonProperty("length")]
    public float length;
    //public Dictionary<Vector3, float> waitTime;
}
public class OverwriteUnitInfo
{
    [JsonProperty("UnitId")]
    public string UnitId;
    [JsonProperty("Atk")]
    public int Atk;
    [JsonProperty("Hp")]
    public int Hp;
    [JsonProperty("Def")]
    public int Def;
    [JsonProperty("MagDef")]
    public int MagDef;
    [JsonProperty("Speed")]
    public float Speed;
    [JsonProperty("Agi")]
    public float Agi;
}

public class PreviewInfos
{
    [JsonProperty("TimePoints")]
    public List<(float,float)> TimePoints;
    [JsonProperty("Points")]
    public List<List<PathPoint>> Points;
}