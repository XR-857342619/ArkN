using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按 SkillEffectTrigger 分组管理 EffectNode，并在对应生命周期派发。
/// <para>
/// 除了同步的 <see cref="Dispatch"/> 之外，还负责维护实现了 <see cref="ISkillEffectTick"/>
/// 的“持续型效果”：在激活时机调用 OnStart 纳入运行列表，之后由宿主（JsonSkill.Update）
/// 每帧调用 <see cref="Tick"/>，持续结束/打断/销毁时调用 <see cref="StopAll"/>。
/// </para>
/// </summary>
public class EffectDispatcher
{
    private readonly Dictionary<SkillEffectTrigger, List<EffectNode>> _triggerMap =
        new Dictionary<SkillEffectTrigger, List<EffectNode>>();

    private readonly Dictionary<string, ISkillEffect> _effectCache =
        new Dictionary<string, ISkillEffect>();

    private readonly List<TickEntry> _runningTicks = new List<TickEntry>();

    private class TickEntry
    {
        public ISkillEffectTick TickEffect;
        public SkillEffectRuntime Runtime;
    }

    /// <summary>当前是否有运行中的持续效果。</summary>
    public bool HasRunningTicks => _runningTicks.Count > 0;

    public void Build(List<EffectNode> effects)
    {
        // 重新编译时先停止旧节点上运行的持续效果
        StopAll();
        _triggerMap.Clear();

        if (effects == null) return;

        foreach (var node in effects)
        {
            if (node == null) continue;

            if (!SkillJsonValidator.TryParseTrigger(node.Trigger, out var trigger))
            {
                Debug.LogWarning($"EffectDispatcher 忽略无法解析的 Trigger: {node.Trigger}");
                continue;
            }

            if (!_triggerMap.TryGetValue(trigger, out var list))
            {
                list = new List<EffectNode>();
                _triggerMap[trigger] = list;
            }

            list.Add(node);
        }

        foreach (var kv in _triggerMap)
        {
            kv.Value.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }

    /// <summary>
    /// 派发某个触发时机的效果。deltaTime 仅在逐帧时机（OnLoopTick 等）有意义。
    /// </summary>
    public void Dispatch(SkillEffectTrigger trigger, SkillContext context, float deltaTime = 0f)
    {
        if (!_triggerMap.TryGetValue(trigger, out var list)) return;
        if (list == null || list.Count == 0) return;

        foreach (var node in list)
        {
            var effect = GetOrCreateEffect(node.Type);
            if (effect == null)
            {
                Debug.LogWarning($"EffectDispatcher 未找到效果器: {node.Type}");
                continue;
            }

            try
            {
                effect.Execute(context, node);

                // 在“激活类”时机，把需要逐帧更新的效果纳入运行列表
                if (effect is ISkillEffectTick tickEffect && CanActivateTick(trigger))
                {
                    StartTickEffect(effect, tickEffect, node, context);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"效果器 {node.Type} 执行失败: {e}");
            }
        }
    }

    /// <summary>
    /// 推进所有运行中的持续效果。返回是否仍有持续效果在运行。
    /// </summary>
    public bool Tick(float deltaTime)
    {
        if (_runningTicks.Count == 0) return false;

        // 先快照，避免 OnTick 内部触发 StopAll / 新增效果导致集合在遍历中被修改
        var snapshot = _runningTicks.ToArray();
        foreach (var entry in snapshot)
        {
            if (entry?.Runtime == null || entry.Runtime.Stopped) continue;

            bool keep;
            try
            {
                keep = entry.TickEffect.OnTick(entry.Runtime, deltaTime);
            }
            catch (Exception e)
            {
                Debug.LogError($"持续效果器 {entry.Runtime.Node?.Type} OnTick 失败: {e}");
                keep = false;
            }

            if (!keep) RemoveEntry(entry);
        }

        return _runningTicks.Count > 0;
    }

    /// <summary>
    /// 停止所有运行中的持续效果（技能持续结束 / 被打断 / 技能销毁时调用）。
    /// </summary>
    public void StopAll()
    {
        var snapshot = _runningTicks.ToArray();
        foreach (var entry in snapshot)
        {
            RemoveEntry(entry);
        }
    }

    public bool HasTrigger(SkillEffectTrigger trigger)
    {
        return _triggerMap.TryGetValue(trigger, out var list) && list != null && list.Count > 0;
    }

    private void StartTickEffect(ISkillEffect effect, ISkillEffectTick tickEffect, EffectNode node, SkillContext context)
    {
        // 同一节点同一时间只允许一个持续实例，避免重复施法时状态互相覆盖
        for (int i = 0; i < _runningTicks.Count; i++)
        {
            var running = _runningTicks[i];
            if (running.Runtime == null) continue;
            if (running.Runtime.Node == node && ReferenceEquals(running.TickEffect, tickEffect))
            {
                Debug.LogWarning($"EffectDispatcher 持续效果 {node.Type} 已在运行，跳过重复启动");
                return;
            }
        }

        var runtime = new SkillEffectRuntime
        {
            Effect = effect,
            Node = node,
            Context = context?.Clone(),
        };

        bool keep;
        try
        {
            keep = tickEffect.OnStart(runtime);
        }
        catch (Exception e)
        {
            Debug.LogError($"持续效果器 {node.Type} OnStart 失败: {e}");
            return;
        }

        if (!keep) return;

        _runningTicks.Add(new TickEntry
        {
            TickEffect = tickEffect,
            Runtime = runtime,
        });
    }

    private void RemoveEntry(TickEntry entry)
    {
        if (entry == null) return;

        int index = _runningTicks.IndexOf(entry);
        if (index >= 0) _runningTicks.RemoveAt(index);

        if (entry.Runtime == null || entry.Runtime.Stopped) return;
        entry.Runtime.Stopped = true;

        try
        {
            entry.TickEffect?.OnStop(entry.Runtime);
        }
        catch (Exception e)
        {
            Debug.LogError($"持续效果器 {entry.Runtime.Node?.Type} OnStop 失败: {e}");
        }
    }

    /// <summary>
    /// 哪些触发时机可以作为“持续效果”的起点。
    /// OnLoopTick 本身就是逐帧派发，OnLoopEnd/OnEnd/OnBreak 是结束时机，都不再额外注册。
    /// </summary>
    private static bool CanActivateTick(SkillEffectTrigger trigger)
    {
        switch (trigger)
        {
            case SkillEffectTrigger.OnLoopTick:
            case SkillEffectTrigger.OnLoopEnd:
            case SkillEffectTrigger.OnEnd:
            case SkillEffectTrigger.OnBreak:
                return false;
            default:
                return true;
        }
    }

    private ISkillEffect GetOrCreateEffect(string effectType)
    {
        if (string.IsNullOrEmpty(effectType)) return null;

        if (_effectCache.TryGetValue(effectType, out var cached))
        {
            return cached;
        }

        var effect = SkillEffectFactory.Create(effectType);
        if (effect != null)
        {
            _effectCache[effectType] = effect;
        }

        return effect;
    }
}
