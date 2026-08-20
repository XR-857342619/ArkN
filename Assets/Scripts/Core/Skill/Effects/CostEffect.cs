/// <summary>
/// 费用变化效果器：增加或扣除费用。
/// Data: CostCount
/// </summary>
public class CostEffect : ISkillEffect
{
    public string Name => "费用";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster?.Battle == null || node?.Data == null) return;

        var count = node.Data.GetInt("CostCount", 0);
        context.Caster.Battle.Cost += count;
    }
}
