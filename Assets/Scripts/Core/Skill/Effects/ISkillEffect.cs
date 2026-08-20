/// <summary>
/// 技能效果器接口。所有 JsonSkill 效果器实现此接口，并由 SkillEffectFactory 自动扫描注册。
/// </summary>
public interface ISkillEffect
{
    string Name { get; }

    void Execute(SkillContext context, EffectNode node);
}
