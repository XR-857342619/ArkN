using System;

/// <summary>
/// JSON 技能效果器的触发时机。
/// 与旧 TriggerEnum 解耦，JsonSkill 在对应生命周期钩子中转换为该枚举并派发。
/// </summary>
public enum SkillEffectTrigger
{
    None = 0,
    OnInit,
    OnStart,
    OnCast,
    OnAttack,
    OnHit,
    OnLoopStart,
    OnLoopTick,
    OnLoopEnd,
    OnEnd,
    OnBreak,
    OnKill,
    OnDeath,
}
