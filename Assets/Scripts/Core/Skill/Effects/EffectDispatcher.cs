using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 按 SkillEffectTrigger 分组管理 EffectNode，并在对应生命周期派发。
/// </summary>
public class EffectDispatcher
{
    private readonly Dictionary<SkillEffectTrigger, List<EffectNode>> _triggerMap =
        new Dictionary<SkillEffectTrigger, List<EffectNode>>();

    private readonly Dictionary<string, ISkillEffect> _effectCache =
        new Dictionary<string, ISkillEffect>();

    public void Build(List<EffectNode> effects)
    {
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

    public void Dispatch(SkillEffectTrigger trigger, SkillContext context)
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
            }
            catch (Exception e)
            {
                Debug.LogError($"效果器 {node.Type} 执行失败: {e}");
            }
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


    public bool HasTrigger(SkillEffectTrigger trigger)
    {
        return _triggerMap.TryGetValue(trigger, out var list) && list != null && list.Count > 0;
    }
}
