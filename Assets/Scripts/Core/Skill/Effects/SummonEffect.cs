using UnityEngine;

/// <summary>
/// 召唤效果器：目前支持敌人类召唤小怪。
/// Data: UnitId, Count, Range
/// </summary>
public class SummonEffect : ISkillEffect
{
    public string Name => "召唤";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;
        if (!(context.Caster is Units.敌人 parent)) return;

        var data = node.Data;
        var unitId = data.GetStr("UnitId");
        if (string.IsNullOrEmpty(unitId)) return;

        var count = data.GetInt("Count", 1);
        var range = data.GetFloat("Range", 0f);

        var waveInfo = JsonHelper.Clone(parent.WaveData);
        waveInfo.sUnitId = unitId;

        float xMin = parent.GridPos.x - 0.5f;
        float xMax = parent.GridPos.x + 0.5f;
        float yMin = parent.GridPos.y - 0.5f;
        float yMax = parent.GridPos.y + 0.5f;

        if (parent.Position2.x - range > xMin) xMin = parent.Position2.x - range;
        if (parent.Position2.x + range < xMax) xMax = parent.Position2.x + range;
        if (parent.Position2.y - range > yMin) yMin = parent.Position2.y - range;
        if (parent.Position2.y + range < yMax) yMax = parent.Position2.y + range;

        for (int i = 0; i < count; i++)
        {
            var unit = parent.Battle.CreateEnemy(waveInfo);
            if (unit == null) continue;
            unit.Position = new Vector3(parent.Battle.NextFloat(xMin, xMax), parent.Position.y, parent.Battle.NextFloat(yMin, yMax));
            unit.currentPathIndex = parent.currentPathIndex;
            unit.Parent = parent;
        }
    }
}
