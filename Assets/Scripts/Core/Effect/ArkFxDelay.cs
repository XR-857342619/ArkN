// ============================================================
// ArkFxDelay - Torappu.Fx.FxDelay 的运行时替身（P2，IDA 实证版）
//
// 依据：反编译 Torappu.Fx.FxDelay 的 OnEnable / _DelayFunc / ForceToEnd
//
// 原版行为：
//   OnEnable:   if (m_isWaiting) return;
//               m_isWaiting = true;
//               gameObject.SetActive(false);                     // 把自己所在的 GameObject 整个隐藏
//               Invoke("_DelayFunc", _delayTime / playbackSpeed);
//   _DelayFunc: gameObject.SetActive(true); m_isWaiting = false;
//   ForceToEnd: 若仍隐藏 -> CancelInvoke -> 立即激活
//
// 语义：该节点下的粒子、子节点、Animator 全部被延后启动，
//       到点后整棵子树重新激活（实现"分层延时"的观感）。
//
// 与"照抄 OnEnable + Invoke"的三处必须的偏离：
//   1. 不用 OnEnable 触发：BakePrefab 会把组件加到 AB 的 prefab 资产上，
//      在 OnEnable 里 SetActive(false) 会把资产里的节点永久置为未激活。
//      → 改为由 ArkFxEffectHost 在 Play() 时调用 OnPlay() 上膛。
//   2. 不用 Invoke：节点被隐藏后自身的 Invoke/协程不会推进。
//      → 改为由 ArkFxTicker（DontDestroyOnLoad 的全局时钟）驱动 TickerUpdate。
//   3. 实测 144/145 个 FxDelay 挂在特效根节点上，隐藏根节点会让
//      ArkFxEffectHost 的 Update 一起停掉，所以计时必须在宿主之外。
// ============================================================
using UnityEngine;

public class ArkFxDelay : ArkFxBehaviour
{
    [Tooltip("延迟秒数（原版 _delayTime）。受 PlaybackSpeed 缩放")]
    public float delayTime;

    bool _armed;      // 是否已上膛（防止重复上膛）
    bool _waiting;    // 是否正在等待
    float _elapsed;

    /// <summary>由 ArkFxEffectHost 在特效开播时调用：隐藏自己并开始计时。</summary>
    public override void OnPlay()
    {
        if (_armed) return;
        _armed = true;
        _elapsed = 0f;

        if (delayTime <= 0f) { _waiting = false; return; }   // 没有延迟，什么都不做

        _waiting = true;
        ArkFxTicker.Register(this);
        gameObject.SetActive(false);                          // 整棵子树延后
    }

    /// <summary>由 ArkFxTicker 每帧驱动（节点被隐藏时依然会调用）。</summary>
    public void TickerUpdate(float deltaTime)
    {
        if (!_waiting) return;

        _elapsed += deltaTime * (PlaybackSpeed > 0f ? PlaybackSpeed : 1f);
        if (_elapsed < delayTime) return;

        Reveal();
    }

    void Reveal()
    {
        _waiting = false;
        ArkFxTicker.Unregister(this);

        gameObject.SetActive(true);

        // 重新激活后，playOnAwake=false 的粒子不会自己播放，这里补一次
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            if (ps != null && !ps.isPlaying) ps.Play(true);
    }

    /// <summary>对应原版 ForceToEnd：外部可强制立即结束延迟。</summary>
    public void ForceToEnd()
    {
        if (_waiting) Reveal();
    }

    public override bool IsBusy { get { return _waiting; } }

    public override void OnRecycle()
    {
        // 回池：复位，下次 Play 重新上膛
        ArkFxTicker.Unregister(this);
        _armed = false;
        _waiting = false;
        _elapsed = 0f;
        // 不在这里强制 SetActive(true)：回池后的激活由 Pool.Spawn 负责。
        // 之前在这里激活会把"刚被 OnPlay 隐藏"的节点立刻点亮，抵消掉延迟。
    }
}
