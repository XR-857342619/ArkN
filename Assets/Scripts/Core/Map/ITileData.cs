using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITileData
{
    bool Passable { get; } // 是否可通行
    float PassCost { get; } // 移动代价
}
