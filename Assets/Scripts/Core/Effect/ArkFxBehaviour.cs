// ============================================================
// ArkFxBehaviour - ArkFx 行为组件基类（P0）
//
// 对应原版 Torappu.Battle.Effects.Effect 的骨架时序：
//     OnPreload -> OnPlay -> (OnPause / OnPausedUpdated) -> OnFinish -> OnRecycle
//
// 原版每个特效 prefab 上的脚本（FxUVTweener / FxDelay / OnPlayEmitter ...）
// 都是这套时序的参与者。ArkFx 的替身组件统一继承本类，
// 由 ArkFxEffectHost 负责按顺序调用，从而让「发射器 / 延时 / 切换」这类
// 依赖时序的组件有地方生根。
//
// 与 ArkFxLoader 的关系：
//   - ArkFxLoader.BakePrefab / SpawnEffect 里调用 ArkFxEffectHost.Ensure(root)
//   - Kick() 改成调用 host.Play()，不再自己 Play 粒子
// ============================================================
using UnityEngine;

public abstract class ArkFxBehaviour : MonoBehaviour
{
    /// <summary>播放倍速（由 ArkFxEffectHost 统一下发，对应原版 playbackSpeed）。</summary>
    [System.NonSerialized] public float PlaybackSpeed = 1f;

    /// <summary>包加载后、实例化前的预处理（本工程里多为空实现）。</summary>
    public virtual void OnPreload() { }

    /// <summary>开始播放。对应原版 OnPlay / OnEnable 的驱动时机。</summary>
    public virtual void OnPlay() { }

    /// <summary>自然结束（所有粒子死亡且没有组件还在等待）。</summary>
    public virtual void OnFinish() { }

    /// <summary>暂停/恢复。原版由战斗暂停、PauseEffectIf 等触发。</summary>
    public virtual void OnPause(bool paused) { }

    /// <summary>回对象池前的清理。</summary>
    public virtual void OnRecycle() { }

    /// <summary>
    /// 是否还在"等待中"（例如 FxDelay 的延时、发射器的 preDelay）。
    /// Host 判定特效是否结束时会把这里算进去，避免延时节点被误判为已结束。
    /// </summary>
    public virtual bool IsBusy { get { return false; } }
}
