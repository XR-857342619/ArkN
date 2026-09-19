/// <summary>
/// 一次“持续型效果”的运行时句柄。
/// 由 EffectDispatcher 在效果器被激活时创建，并在整个持续期间保持不变。
/// <para>
/// 效果器实例由工厂缓存、可能在多次激活间共享，因此任何“本次激活”的状态必须保存在
/// <see cref="State"/> 上，而不是效果器自己的字段里。
/// </para>
/// </summary>
public class SkillEffectRuntime
{
    /// <summary>触发本次持续的效果器实例。</summary>
    public ISkillEffect Effect;

    /// <summary>触发本次持续的配置节点。</summary>
    public EffectNode Node;

    /// <summary>激活时刻的上下文快照（Targets / TargetPositions 不会随后续 Clear 失效）。</summary>
    public SkillContext Context;

    /// <summary>效果器自用的本次激活状态。</summary>
    public object State;

    /// <summary>本次持续是否已经停止。</summary>
    public bool Stopped;
}
