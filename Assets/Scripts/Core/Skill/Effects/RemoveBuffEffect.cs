using System.Collections.Generic;

/// <summary>
/// 移除 Buff 效果器。
/// Data: BuffIds (数组) 或 BuffId (单个)
/// </summary>
public class RemoveBuffEffect : ISkillEffect
{
    public string Name => "移除Buff";

    public void Execute(SkillContext context, EffectNode node)
    {
        if (context == null || node?.Data == null) return;

        var data = node.Data;
        var buffIds = new List<int>();

        var array = data.GetArray("BuffIds");
        if (array != null && array.Length > 0)
        {
            foreach (var item in array)
            {
                var id = EffectUtil.ToConfigId<BuffData>(item);
                if (id > 0) buffIds.Add(id);
            }
        }

        if (data.TryGetValue("BuffId", out var single))
        {
            var id = EffectUtil.ToConfigId<BuffData>(single);
            if (id > 0) buffIds.Add(id);
        }

        if (buffIds.Count == 0) return;

        foreach (var target in EffectUtil.GetTargets(context))
        {
            if (target == null) continue;

            foreach (var buffId in buffIds)
            {
                var buff = target.Buffs.Find(x => x.Id == buffId);
                if (buff != null) buff.Finish();
            }
        }
    }
}
