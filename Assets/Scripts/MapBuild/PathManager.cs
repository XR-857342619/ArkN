using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

public class PathManager : MonoBehaviour
{

}

[System.Serializable]
public class PathPoint
{
    [JsonProperty("Pos")]
    public Vector3 Pos;
    [JsonProperty("Delay")]
    public float Delay;
    //public bool DirectMove;
    [JsonProperty("CheckPoint")]
    public bool CheckPoint;
    [JsonProperty("HideMove")]
    public bool HideMove;
    [JsonProperty("IsArrive")]
    public bool IsArrive;
    [JsonProperty("IsTemp")]
    public bool IsTemp;
    //public PathPoint nextPoint;
}
