using System.Collections.Generic;
using System.Linq;
using Units;
using UnityEngine;

/// <summary>
/// 召唤效果器（JsonSkill 版）：召唤逻辑与 Skills.召唤 保持一致。
/// <para>召唤位置由 <see cref="SkillContext.TargetPositions"/> 提供，不再根据“召唤位置”字段自行选点。</para>
/// <para>“位移”部署模式实现 <see cref="ISkillEffectTick"/>，在 loop 阶段逐帧把施法者移动到目标点。</para>
/// Data: 召唤物ID(UnitId)、数量(Count)、范围(Range)、部署模式、位移速度
/// </summary>
public class SummonEffect : ISkillEffect, ISkillEffectTick
{
    public string Name => "召唤";

    private class SummonParams
    {
        public string UnitId;
        public int Count;
        public float Range;
        public string SetMod;
        public float Speed;
    }

    private class DashState
    {
        public Unit Unit;
        public Vector3 Target;
        public Vector3 Direction;
        public float Speed;
        public float Elapsed;
    }

    private static SummonParams Parse(EffectNode node)
    {
        var data = node.Data;
        var p = new SummonParams
        {
            UnitId = data.GetStr("召唤物ID"),
            Range = data.GetFloat("范围", data.GetFloat("Range", 0f)),
            Count = data.GetInt("数量", data.GetInt("Count", 1)),
            SetMod = data.GetStr("部署模式", "追加"),
            Speed = data.GetFloat("位移速度", 0f),
        };
        if (string.IsNullOrEmpty(p.UnitId))
            p.UnitId = data.GetStr("UnitId");
        if (p.SetMod == "位移")
            p.Count = 1;
        return p;
    }

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context?.Caster == null || node?.Data == null) return;

        var p = Parse(node);
        if (string.IsNullOrEmpty(p.UnitId) && p.SetMod != "位移") return;

        // 位移由 OnStart / OnTick 负责，不在 Execute 里生成单位
        if (p.SetMod == "位移") return;

        Unit caster = context.Caster;
        WaveInfo waveInfo = BuildWaveInfo(caster, p.UnitId);

        List<Vector2> posList = GetTargetPositions(context);
        if (posList.Count == 0)
        {
            Debug.LogWarning($"SummonEffect：TargetPositions 为空，技能 {context.Skill?.Id} 无法召唤 {p.UnitId}");
            return;
        }

        // 与 Skills.召唤.SpSkillEffect 一致：每个位置召唤 count 个，并在 range 内随机偏移
        for (int i = 0; i < posList.Count; i++)
        {
            for (int j = 0; j < p.Count; j++)
            {
                SpawnEnemy(caster, p.UnitId, p.SetMod, GetRandomPosition(caster, posList[i], p.Range), waveInfo);
            }
        }
    }

    public bool OnStart(SkillEffectRuntime runtime)
    {
        if (runtime?.Node?.Data == null) return false;

        var p = Parse(runtime.Node);
        if (p.SetMod != "位移" || p.Speed <= 0f) return false;

        Unit caster = runtime.Context?.Caster;
        if (caster == null || !caster.Alive()) return false;

        List<Vector2> posList = GetTargetPositions(runtime.Context);
        if (posList.Count == 0) return false;

        // 与 Skills.召唤 一致：多个目标时最终以最后一个位置为准
        Vector2 dest = posList[posList.Count - 1];
        Vector3 target = new Vector3(dest.x, caster.Position.y, dest.y);
        Vector3 delta = target - caster.Position;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return false;

        runtime.State = new DashState
        {
            Unit = caster,
            Target = target,
            Direction = delta.normalized,
            Speed = p.Speed,
        };
        return true;
    }

    public bool OnTick(SkillEffectRuntime runtime, float deltaTime)
    {
        if (!(runtime?.State is DashState state) || state.Unit == null) return false;

        // 目标/施法者已失效则结束
        if (!state.Unit.Alive())
        {
            runtime.State = null;
            return false;
        }

        float step = state.Speed * deltaTime;
        float remain = (state.Target - state.Unit.Position).magnitude;
        if (step >= remain)
        {
            state.Unit.Position = state.Target;
            if (state.Unit is Units.干员 op) op.ResetAttackPoint();
            runtime.State = null;
            return false;
        }

        state.Unit.Position += state.Direction * step;
        state.Elapsed += deltaTime;
        return true;
    }

    public void OnStop(SkillEffectRuntime runtime)
    {
        if (runtime != null) runtime.State = null;
    }

    private static WaveInfo BuildWaveInfo(Unit caster, string unitId)
    {
        WaveInfo waveInfo;
        if (caster is Units.敌人 parent && parent.WaveData != null)
            waveInfo = JsonHelper.Clone(parent.WaveData);
        else
            waveInfo = new WaveInfo();
        waveInfo.sUnitId = unitId;
        return waveInfo;
    }

    /// <summary>读取 SkillContext.TargetPositions，并转换为地图平面坐标(x, z)。</summary>
    private static List<Vector2> GetTargetPositions(SkillContext context)
    {
        var result = new List<Vector2>();
        if (context?.TargetPositions == null) return result;

        foreach (var pos in context.TargetPositions)
        {
            result.Add(new Vector2(pos.x, pos.z));
        }
        return result;
    }

    /// <summary>与 Skills.召唤.GetRandomPositions 一致：在目标点周围 range 范围内随机取点。</summary>
    private static Vector2 GetRandomPosition(Unit caster, Vector2 center, float range)
    {
        float x = caster.Battle.NextFloat(center.x - range, center.x + range);
        float y = caster.Battle.NextFloat(center.y - range, center.y + range);
        return new Vector2(x, y);
    }

    /// <summary>与 Skills.召唤.SpawnEnemy 一致：处理“替换/追加”生成。</summary>
    private static void SpawnEnemy(Unit caster, string unitId, string setMod, Vector2 pos, WaveInfo waveInfo)
    {
        Vector2Int gridPos = pos.ToV2Int();
        if (gridPos.x < 0 || gridPos.y < 0 ||
            gridPos.x >= caster.Battle.Map.Tiles.GetLength(0) ||
            gridPos.y >= caster.Battle.Map.Tiles.GetLength(1))
        {
            Debug.LogWarning($"SummonEffect：召唤位置越界，已忽略 {pos}");
            return;
        }

        if (setMod == "替换")
        {
            // 与 Skills.召唤 一致：使用 FindAll 检索，可命中空中单位
            Unit old = caster.Battle.FindAll(gridPos, 2)
                .FirstOrDefault(x => x is Units.敌人 && x.UnitData.Id == unitId);
            if (old != null)
            {
                old.Finish(true);
            }
        }

        Units.敌人 unit = caster.Battle.CreateEnemy(waveInfo);
        if (unit == null) return;

        // 与 Skills.召唤 一致：CreateEnemy 只触发了“出场”，这里补发“入场/自己入场”
        caster.Battle.TriggerDatas.Push(new TriggerData()
        {
            Target = unit,
        });
        try
        {
            caster.Battle.Trigger(TriggerEnum.入场);
            unit.Trigger(TriggerEnum.自己入场);
        }
        finally
        {
            caster.Battle.TriggerDatas.Pop();
        }

        unit.Position = new Vector3(pos.x, caster.Position.y, pos.y);
        if (caster is Units.敌人 parent)
        {
            unit.currentPathIndex = parent.currentPathIndex;
        }
        unit.Parent = caster;
    }
}
