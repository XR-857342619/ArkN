using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JsonSkill 的基础通用配置。
/// 只保留 Skill 基类负责的通用机制字段，具体行为交给 Selectors/Sorters/Effects。
/// </summary>
public class SkillBaseConfig
{
    public int SkillCost;
    public int MaxPower;
    public int StartPower;
    public int PowerCount;
    public PowerRecoverTypeEnum PowerType;
    public PowerRecoverTypeEnum PowerUseType;
    public SkillUseTypeEnum UseType;
    public string ReadyType;
    public float Cooldown;
    public float OpenTime;
    public int BurstCount;
    public float BurstDelay;
    public bool BurstFind;
    public int TargetTeam;
    public bool DeadFind;
    public float AttackRange;
    public List<Vector2Int> AttackPoints;
    public bool AttackAreaWithMain;
    public string[] ModelAnimation;
    public string[] OverwriteAnimation;
    public string ShootPoint;
    public bool AutoUse;
    public bool NoTargetAlsoUse;
    public bool RegetTarget;
    public bool StopBreak;
    public bool CanStop;
    public int MaxUseCount;
    public float? AnimationTime;
    public AttackModeEnum AttackMode;
}
