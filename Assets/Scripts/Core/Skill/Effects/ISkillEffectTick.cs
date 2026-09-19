/// <summary>
/// 可选接口：效果器需要在技能持续阶段逐帧更新时实现。
/// <para>生命周期：</para>
/// <list type="number">
/// <item>EffectDispatcher 执行 <see cref="ISkillEffect.Execute"/> 后，若触发时机允许持续，创建 <see cref="SkillEffectRuntime"/>；</item>
/// <item>调用 <see cref="OnStart"/>：返回 true 纳入运行列表，之后每帧调用 <see cref="OnTick"/>；返回 false 表示本次不需要逐帧；</item>
/// <item><see cref="OnTick"/> 返回 false 表示本次持续自行结束，随后调用 <see cref="OnStop"/>；</item>
/// <item>技能持续结束 / 被打断 / 技能销毁时，由 EffectDispatcher.StopAll 调用 <see cref="OnStop"/>。</item>
/// </list>
/// <para>
/// 注意：效果器实例是共享缓存的，本次激活的状态请保存在 runtime.State。
/// </para>
/// </summary>
public interface ISkillEffectTick
{
    /// <summary>效果器被激活时调用一次。返回 false 表示不进入逐帧阶段。</summary>
    bool OnStart(SkillEffectRuntime runtime);

    /// <summary>持续期间每帧调用。返回 false 表示本次持续结束。</summary>
    bool OnTick(SkillEffectRuntime runtime, float deltaTime);

    /// <summary>持续结束或被打断时调用，用于清理本次激活状态。</summary>
    void OnStop(SkillEffectRuntime runtime);
}
