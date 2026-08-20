using System.Collections.Generic;

/// <summary>
/// JSON 技能配置数据，每行一个 JSON 对象。
/// 通过 Database.Add&lt;SkillJsonData&gt;("SkillJson") 加载。
/// </summary>
public class SkillJsonData : IConfig
{
    public string Id { get; set; }
    public string Name;
    public string Description;
    public string Icon;

    public SkillBaseConfig Base;
    public List<SelectorNode> Selectors;
    public List<SorterNode> Sorters;
    public List<EffectNode> Effects;
}
