using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 技能/Buff 与 BattleUnit 进度条之间的注册中心。
/// Skill 和 Buff 基类在初始化时通过这里注册绑定需求，
/// UI_BattleUnit 只从这里读取绑定信息并刷新对应进度条。
/// </summary>
public class UnitProgressBarManager
{
    private static UnitProgressBarManager instance;

    public static UnitProgressBarManager Instance
    {
        get
        {
            if (instance == null) instance = new UnitProgressBarManager();
            return instance;
        }
    }

    private readonly Dictionary<Unit, List<ProgressBarBinding>> bindings =
        new Dictionary<Unit, List<ProgressBarBinding>>();

    public IReadOnlyList<ProgressBarBinding> GetBindings(Unit unit)
    {
        if (unit == null) return null;

        if (bindings.TryGetValue(unit, out var list)) return list;

        return null;
    }

    public void RegisterSkill(Unit unit, Skill skill, string barType)
    {
        if (unit == null || skill == null || string.IsNullOrEmpty(barType)) return;

        List<ProgressBarBinding> list = GetOrCreateList(unit);

        ProgressBarBinding existing = list.FirstOrDefault(b => b.IsSkill && ReferenceEquals(b.Source, skill));
        if (existing != null)
        {
            existing.BarType = barType;
            return;
        }

        list.Add(new ProgressBarBinding
        {
            Unit = unit,
            IsSkill = true,
            Source = skill,
            BarType = barType,
        });
    }

    public void RegisterBuff(Unit unit, Buff buff, string barType)
    {
        if (unit == null || buff == null || string.IsNullOrEmpty(barType)) return;

        List<ProgressBarBinding> list = GetOrCreateList(unit);

        ProgressBarBinding existing = list.FirstOrDefault(b => !b.IsSkill && ReferenceEquals(b.Source, buff));
        if (existing != null)
        {
            existing.BarType = barType;
            existing.BuffMax = GetBuffMax(buff);
            return;
        }

        // 相同 Buff 再次添加时，复用原绑定槽位并重置数据源。
        ProgressBarBinding sameBuff = list.FirstOrDefault(b => !b.IsSkill && b.Source is Buff oldBuff && oldBuff.Id == buff.Id);
        if (sameBuff != null)
        {
            sameBuff.Source = buff;
            sameBuff.BarType = barType;
            sameBuff.BuffMax = GetBuffMax(buff);
            return;
        }

        list.Add(new ProgressBarBinding
        {
            Unit = unit,
            IsSkill = false,
            Source = buff,
            BarType = barType,
            BuffMax = GetBuffMax(buff),
        });
    }

    public void ClearUnit(Unit unit)
    {
        if (unit == null) return;
        bindings.Remove(unit);
    }

    private List<ProgressBarBinding> GetOrCreateList(Unit unit)
    {
        if (!bindings.TryGetValue(unit, out var list))
        {
            list = new List<ProgressBarBinding>();
            bindings.Add(unit, list);
        }

        return list;
    }

    private static float GetBuffMax(Buff buff)
    {
        if (buff.LastTime > 0) return buff.LastTime;

        if (buff.BuffData != null && buff.BuffData.LastTime > 0) return buff.BuffData.LastTime;

        if (buff.Duration.value > 0) return buff.Duration.value;

        return 1f;
    }
}

public class ProgressBarBinding
{
    public Unit Unit;
    public bool IsSkill;
    public object Source;
    public string BarType;

    /// <summary>Buff 进度条使用的最大值缓存。Buff 结束后进度清空但仍保留该最大值。</summary>
    public float BuffMax;
}
