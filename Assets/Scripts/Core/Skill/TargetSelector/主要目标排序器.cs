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

public class 距离排序 : ISortStrategy
{
    private readonly SkillContext _context;

    public 距离排序(SkillContext context)
    {
        _context = context;
    }

    public string Name => "距离排序";
    public Func<Unit, IComparable> GetKeySelector() => u => (u.Position - _context.Caster.Position).sqrMagnitude;
}

public class 距离施法者排序 : ISortStrategy
{
    private readonly SkillContext _context;

    public 距离施法者排序(SkillContext context)
    {
        _context = context;
    }

    public string Name => "距离施法者排序";
    public Func<Unit, IComparable> GetKeySelector() => u => (u.Position - _context.Caster.Position).sqrMagnitude;
}

public class 仇恨排序 : ISortStrategy
{
    public string Name => "仇恨排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.Hatre;
}

public class 生命值排序 : ISortStrategy
{
    public string Name => "生命值排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.Hp;
}

public class 最大生命值排序 : ISortStrategy
{
    public string Name => "最大生命值排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.MaxHp;
}

public class 攻击力排序 : ISortStrategy
{
    public string Name => "攻击力排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.Attack;
}

public class 防御力排序 : ISortStrategy
{
    public string Name => "防御力排序";
    public Func<Unit, IComparable> GetKeySelector() => u => u.Defence;
}
