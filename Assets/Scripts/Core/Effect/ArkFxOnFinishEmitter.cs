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
}
