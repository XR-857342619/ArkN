using System.Collections.Generic;

/// <summary>
/// JSON 技能的目标选择器节点。
/// Type 可以是 ISelectorStrategy（主动产生候选目标）或 IFilterStrategy（在现有目标上筛选）。
/// Data 为参数字典，反射绑定到对应策略构造函数。
/// </summary>
public class SelectorNode
{
    public string Type;
    public Dictionary<string, object> Data;
}
