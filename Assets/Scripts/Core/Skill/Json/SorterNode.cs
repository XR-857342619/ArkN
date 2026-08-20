using System.Collections.Generic;

/// <summary>
/// JSON 技能的目标排序器节点。
/// </summary>
public class SorterNode
{
    public string Type;
    public SortDirection Direction;
    public Dictionary<string, object> Data;
}
