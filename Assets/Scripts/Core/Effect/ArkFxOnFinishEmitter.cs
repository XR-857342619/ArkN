// ============================================================
// ArkFxOnFinishEmitter - Torappu.Battle.Effects.OnFinishEmitter 的替身（P3）
//
// 原版：不响应 OnPlay，在特效 OnFinish 时发射 _effects 列表。
// ============================================================
using UnityEngine;

public class ArkFxOnFinishEmitter : ArkFxEmitterBase
{
    [Tooltip("要发射的子特效名（原版 _effects）")]
    public string[] effects;

    public override void OnPlay()
    {
        armed = true;      // 标记已上膛，但不发射
    }

    protected override void DoEmit() { }

    protected override void OnFinishEmit()
    {
        if (effects == null) return;
        for (int i = 0; i < effects.Length; i++)
            Emit(effects[i]);
    }

    /// <summary>
    /// 原版 OnFinishEmitter 只有 _effects / _useEffectPosition / _useFaceVector，
    /// 没有 _clearWhenFinish 语义；它发射的是「父特效结束后的尾段特效」，必须继续存活。
    /// 基类 OnFinish 会先 OnFinishEmit() 再按 clearWhenFinish 清空 spawned，
    /// 若沿用会把刚发射的尾段特效同帧 Destroy。这里单独覆盖：只发射，不清理。
    /// 真正的清理仍在 OnRecycle（回对象池）里做。
    /// </summary>
    public override void OnFinish()
    {
        OnFinishEmit();
    }
}
