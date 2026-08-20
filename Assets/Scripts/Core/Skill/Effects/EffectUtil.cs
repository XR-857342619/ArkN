using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 效果器公共工具。
/// </summary>
public static class EffectUtil
{
    public static List<Unit> GetTargets(SkillContext context)
    {
        return context?.Targets ?? new List<Unit>();
    }

    public static DamageTypeEnum ParseDamageType(object value, DamageTypeEnum defaultValue = DamageTypeEnum.Normal)
    {
        if (value == null) return defaultValue;

        if (value is DamageTypeEnum typeEnum) return typeEnum;

        var str = Convert.ToString(value);
        if (string.IsNullOrEmpty(str)) return defaultValue;

        switch (str.Trim())
        {
            case "物理":
            case "Normal":
            case "normal":
                return DamageTypeEnum.Normal;
            case "法术":
            case "魔法":
            case "Magic":
            case "magic":
                return DamageTypeEnum.Magic;
            case "真实":
            case "Real":
            case "real":
                return DamageTypeEnum.Real;
            case "元素":
            case "Element":
            case "element":
                return DamageTypeEnum.Element;
            case "治疗":
            case "Heal":
            case "heal":
                return DamageTypeEnum.Heal;
            case "流失":
            case "LoseHP":
            case "LoseHp":
                return DamageTypeEnum.LoseHP;
            default:
                if (Enum.TryParse(str, true, out DamageTypeEnum parsed))
                {
                    return parsed;
                }
                return defaultValue;
        }
    }

    /// <summary>
    /// 把 JSON 中的 id 转成 Database 使用的 int 索引。
    /// 支持 int 直接使用；支持 string 时按配置 Id 查找索引。
    /// </summary>
    public static int ToConfigId<T>(object value, int defaultValue = 0) where T : class, IConfig
    {
        if (value == null) return defaultValue;

        if (value is int i) return i;
        if (value is long l) return (int)l;

        var str = Convert.ToString(value);
        if (string.IsNullOrEmpty(str)) return defaultValue;

        if (int.TryParse(str, out int direct))
        {
            return direct;
        }

        var index = Database.Instance.GetIndex<T>(str);
        return index >= 0 ? index : defaultValue;
    }
}
