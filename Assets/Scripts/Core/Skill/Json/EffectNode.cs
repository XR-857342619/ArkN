using System.Collections.Generic;

/// <summary>
/// JSON 技能的效果器节点。
/// </summary>
public class EffectNode
{
    public string Type;
    public string Trigger;
    public int Priority;
    public Dictionary<string, object> Data;
}
