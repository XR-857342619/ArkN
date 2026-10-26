// ============================================================
// ArkFxOnPlayEmitter - Torappu.Battle.Effects.OnPlayEmitter 的替身（P3）
//
// 原版：OnPlay 时按 _effects 列表逐个 CreateEffect；_instant 为真则立即，
//       否则协程等 _preDelay（见 ArkFxEmitterBase.BeginEmit）。
// ============================================================
using UnityEngine;

public class ArkFxOnPlayEmitter : ArkFxEmitterBase
{
    [Tooltip("要发射的子特效名（原版 _effects）")]
    public string[] effects;

    [Tooltip("原版 _instant：为真则忽略 preDelay 立即发射")]
    public bool instant;

    [Tooltip("原版 _useFaceVector：朝向跟随（当前未实现，仅记录）")]
    public bool useFaceVector;

    public override void OnPlay()
    {
        armed = true;
        if (!gameObject.activeInHierarchy) { pending = true; return; }
        if (instant) DoEmit();
        else BeginEmit();
    }

    protected override void DoEmit()
    {
        if (effects == null) return;
        for (int i = 0; i < effects.Length; i++)
            Emit(effects[i]);
    }
}
