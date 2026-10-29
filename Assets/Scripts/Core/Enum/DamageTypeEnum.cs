using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum DamageTypeEnum
{
    Normal,
    Magic,
    Real,
    Element,
    general,
    Heal,
    LoseHP,
}

public enum DamageBase
{
    UnitAttack,
    TargetAttack,
    UnitMaxHp,
    TargetMaxHp,
    UnitHp,
    TargetHp,
    Fixed,
    ParentAttack,
}
