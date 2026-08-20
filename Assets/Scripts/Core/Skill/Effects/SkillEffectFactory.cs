using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 效果器反射工厂：启动时扫描所有实现 ISkillEffect 的类，按 Name 注册。
/// </summary>
public static class SkillEffectFactory
{
    private static readonly Dictionary<string, Type> _effectMap = new Dictionary<string, Type>();

    static SkillEffectFactory()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(ISkillEffect).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in types)
        {
            try
            {
                var instance = (ISkillEffect)Activator.CreateInstance(type);
                if (instance == null || string.IsNullOrEmpty(instance.Name)) continue;
                _effectMap[instance.Name] = type;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SkillEffectFactory 注册 {type.Name} 失败: {e.Message}");
            }
        }
    }

    public static ISkillEffect Create(string effectType)
    {
        if (string.IsNullOrEmpty(effectType)) return null;

        if (_effectMap.TryGetValue(effectType, out var type))
        {
            try
            {
                return (ISkillEffect)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SkillEffectFactory 创建 {effectType} 失败: {e.Message}");
                return null;
            }
        }

        return null;
    }

    public static bool Contains(string effectType)
    {
        return !string.IsNullOrEmpty(effectType) && _effectMap.ContainsKey(effectType);
    }

    public static string[] GetAllNames()
    {
        return _effectMap.Keys.ToArray();
    }
}
