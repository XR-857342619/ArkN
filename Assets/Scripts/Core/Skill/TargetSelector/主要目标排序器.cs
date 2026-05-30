using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ÷’µ„æ‡¿Î≈≈–Ú : ISortStrategy
{
    public string Name => "÷’µ„æ‡¿Î≈≈–Ú";
    public Func<Unit, IComparable> GetKeySelector() => u => u.distanceToFinal();
}
