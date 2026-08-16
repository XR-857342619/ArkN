using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class 终点距离排序 : ISortStrategy
{
    public string Name => "终点距离排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.distanceToFinal();
}
