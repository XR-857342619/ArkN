using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class Modify
{
    public int Id;

    public ModifyData ModifyData => Database.Instance.Get<ModifyData>(Id);

    public Skill Skill;

    public int orderCode;

    public int OrderCode { get { return orderCode; } }
    protected Unit Unit => Skill.Unit;

    protected Battle Battle => Skill.Unit.Battle;

    public virtual void Init()
    {
        orderCode = ModifyData.Data.GetInt("OrderCode", 0);
    }
}

public interface ISelfDamageModify
{
    void Modify(DamageInfo damageInfo);
}

public interface IDamageModify
{
    void Modify(DamageInfo damageInfo);
}
public interface IBulletDamageModify
{
    void Modify(DamageInfo damageInfo, Bullet bullet);
}
public interface ITargetModify
{
    int Modify(int count, Unit unit);
}

public interface ISkillModify
{
    void Modify(Skill skill);
}

public interface IUnitModify
{
    void Modify(Unit unit);
}
