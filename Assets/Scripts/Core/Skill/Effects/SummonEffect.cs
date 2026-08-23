using System.Collections.Generic;
using System.Linq;
using Units;
using UnityEngine;

/// <summary>
/// 召唤效果器：可从任意单位召唤敌人类小怪。
/// 当施法者没有波次信息时，会创建无路径信息的 WaveInfo，使被召唤敌人原地不动、不会破门。
/// Data: UnitId, Count, Range, 召唤位置, 部署模式
/// </summary>
public class SummonEffect : ISkillEffect
{
    public string Name => "召唤";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var data = node.Data;
        string unitId = data.GetStr("UnitId");
        if (string.IsNullOrEmpty(unitId)) return;

        int count = data.GetInt("Count", 1);
        float range = data.GetFloat("Range", 0f);
        string targetPos = data.GetStr("召唤位置", "");
        string setMod = data.GetStr("部署模式", "追加");

        WaveInfo waveInfo;
        if (context.Caster is Units.敌人 parent && parent.WaveData != null)
        {
            waveInfo = JsonHelper.Clone(parent.WaveData);
        }
        else
        {
            waveInfo = new WaveInfo();
        }
        waveInfo.sUnitId = unitId;

        List<Vector2Int> posList = GetPosList(context, context.Caster, targetPos, range);
        if (posList.Count == 0) return;

        for (int i = 0; i < posList.Count && i < count; i++)
        {
            SpawnEnemy(context.Caster, unitId, setMod, posList[i], waveInfo);
        }
    }

    private List<Vector2Int> GetPosList(SkillContext context, Unit caster, string targetPos, float range)
    {
        switch (targetPos)
        {
            case "使用自身位置":
                return new List<Vector2Int> { caster.GridPos };

            case "使用本技能索敌位置":
                return context.Targets.Select(x => x.GridPos).ToList();

            case "使用附加技能索敌位置":
                if (context.Skill?.SkillData?.Skills != null && context.Skill.SkillData.Skills.Length > 0)
                {
                    var skill = caster.LearnSkill(context.Skill.SkillData.Skills[0]);
                    skill.Init();
                    return skill.GetAttackTarget().Select(x => x.GridPos).ToList();
                }
                return new List<Vector2Int>();

            case "使用干员攻击范围位置":
                return context.Skill?.AttackPoints != null
                    ? new List<Vector2Int>(context.Skill.AttackPoints)
                    : new List<Vector2Int>();

            default:
                return GetRandomPositions(caster, range);
        }
    }

    private List<Vector2Int> GetRandomPositions(Unit caster, float range)
    {
        var result = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            float x = caster.Battle.NextFloat(caster.GridPos.x - range, caster.GridPos.x + range);
            float y = caster.Battle.NextFloat(caster.GridPos.y - range, caster.GridPos.y + range);
            result.Add(new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y)));
        }
        return result;
    }

    private void SpawnEnemy(Unit caster, string unitId, string setMod, Vector2Int pos, WaveInfo waveInfo)
    {
        Tile tile = caster.Battle.Map.Tiles[pos.x, pos.y];

        if (setMod == "替换")
        {
            Unit old = tile.Units.FirstOrDefault(x => x is Units.敌人 && x.UnitData.Id == unitId);
            if (old != null)
            {
                old.Finish(true);
            }
        }
        else if (setMod == "位移")
        {
            var enemyCaster = caster as Units.敌人;
            Unit existing;
            if (enemyCaster != null)
            {
                existing = caster.Battle.AllUnits.FirstOrDefault(x => x.UnitData.Id == unitId && x.Parent == enemyCaster);
            }
            else
            {
                existing = caster.Battle.AllUnits.FirstOrDefault(x => x.UnitData.Id == unitId && x is Units.敌人 e && string.IsNullOrEmpty(e.WaveData?.Path));
            }

            if (existing is Units.敌人 existingEnemy)
            {
                existingEnemy.Position = new Vector3(pos.x, existingEnemy.Position.y, pos.y);
                existingEnemy.NeedResetPath = true;
                return;
            }
        }

        var unit = caster.Battle.CreateEnemy(waveInfo);
        if (unit == null) return;

        unit.Position = new Vector3(pos.x, tile.Pos.y, pos.y);
        if (caster is Units.敌人 parent)
        {
            unit.currentPathIndex = parent.currentPathIndex;
            unit.Parent = parent;
        }
    }
}