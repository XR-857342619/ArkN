using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using UnityEngine;

/// <summary>
/// 存档 `$type` 解析兜底（R7）。
///
/// 背景（2026-10 实测，用 Newtonsoft 复现）：
/// - 地图存档走 `JsonHelper.ToJson`（TypeNameHandling.None），`$type` **完全被忽略**——
///   即使 `$type` 指向不存在的类型/程序集也照样能读出来，所以地图不会因为类改名而损坏；
/// - 但 `data.sav` 走 `JsonHelper.ToJsonWithType`（TypeNameHandling.Auto），`$type` **会被解析**，
///   解析失败直接抛 `JsonSerializationException`，整份存档读不出来。
///
/// 这个 Binder 因此在**两条路径上都注册**（None 路径下无副作用），解析顺序：
///   ① 程序集限定名（正常路径）
///   ② 不带程序集的全名
///   ③ 扫描已加载程序集：先按 FullName、再按 Name（兼容"挪程序集 / 挪命名空间"）
///   ④ 改名别名表（真改名时在这里登记一条即可救回老存档）
/// 都失败则返回 null，由 Newtonsoft 抛明确异常（不静默吞掉）。
///
/// 写入侧：`BindToName` 只写**简单程序集名**（不带版本号），避免换 Unity 版本后
/// 程序集版本号变化导致老存档解析失败。
/// </summary>
public class JsonTypeBinder : ISerializationBinder
{
    public static readonly JsonTypeBinder Instance = new JsonTypeBinder();

    /// <summary>
    /// 类型**真改名**时的迁移别名：旧简单名 → 当前类型。
    /// 只在"按名字完全找不到"时才会用到；纯挪程序集/挪命名空间不需要登记。
    /// 例：{ "OldGridInfo", typeof(GridInfo) }
    /// </summary>
    public static readonly Dictionary<string, Type> Aliases = new Dictionary<string, Type>(StringComparer.Ordinal);

    static Dictionary<string, Type> _byFullName;
    static Dictionary<string, Type> _bySimpleName;
    static readonly object _lock = new object();

    public Type BindToType(string assemblyName, string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        var type = Resolve(assemblyName, typeName);
        if (type == null)
            Debug.LogWarning($"[JsonTypeBinder] 无法解析存档类型 '{typeName}'（程序集 '{assemblyName}'）——"
                             + "可能有类被改名或删除；如是改名，请在 JsonTypeBinder.Aliases 里登记 旧名→新类型");
        return type;
    }

    public void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        if (serializedType == null) { assemblyName = null; typeName = null; return; }
        assemblyName = serializedType.Assembly.GetName().Name;   // 只写简单程序集名，不写版本号
        typeName = serializedType.FullName;
    }

    static Type Resolve(string assemblyName, string typeName)
    {
        // ① 程序集限定名（最常见、也是原来的行为）
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var t = Type.GetType(typeName + ", " + assemblyName, false);
            if (t != null) return t;
        }

        // ② 不带程序集
        var t2 = Type.GetType(typeName, false);
        if (t2 != null) return t2;

        BuildIndex();

        // ③ 全名命中（程序集被挪动）
        if (_byFullName.TryGetValue(typeName, out var t3)) return t3;

        // 简单名命中（命名空间被挪动）。泛型/数组限定名太复杂，这里只处理简单名。
        if (typeName.IndexOf('`') < 0 && typeName.IndexOf('[') < 0)
        {
            string simple = typeName;
            int dot = simple.LastIndexOf('.');
            if (dot >= 0) simple = simple.Substring(dot + 1);
            if (_bySimpleName.TryGetValue(simple, out var t4)) return t4;

            // ④ 改名别名
            if (Aliases.TryGetValue(simple, out var t5)) return t5;
        }

        return null;
    }

    static void BuildIndex()
    {
        lock (_lock)
        {
            if (_byFullName != null) return;
            _byFullName = new Dictionary<string, Type>(StringComparer.Ordinal);
            _bySimpleName = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }
                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null || t.FullName == null) continue;
                    if (!_byFullName.ContainsKey(t.FullName)) _byFullName[t.FullName] = t;

                    // 简单名冲突时优先业务程序集，避免系统类型抢先
                    if (_bySimpleName.TryGetValue(t.Name, out var exist))
                    {
                        if (exist.Assembly == asm && asm.GetName().Name != "Assembly-CSharp") continue;
                        if (asm.GetName().Name != "Assembly-CSharp") continue;
                    }
                    _bySimpleName[t.Name] = t;
                }
            }
        }
    }
}
