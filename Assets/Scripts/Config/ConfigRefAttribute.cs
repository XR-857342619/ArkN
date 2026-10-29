using System;

/// <summary>
/// 标记一个 string/string[] 字段是“指向另一张配置表的稳定 Id 引用”。
/// 例如 BuffData.LastingEffectId 指向 EffectData.Id。
///
/// 兼容策略：
///   - 新导出的配置同时保留旧 int 索引字段（LastingEffect）和新的 string Id 字段（LastingEffectId）；
///   - Database 加载完成后，会把 string Id 解析回索引并回填旧 int 字段，
///     因此旧代码仍可继续用 int 读取，但实际以字符串 Id 为准；
///   - 老格式数据没有 *Id 字段时，保留原 int 索引行为。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ConfigRefAttribute : Attribute
{
    public readonly string Table;

    public ConfigRefAttribute(string table)
    {
        Table = table;
    }
}
