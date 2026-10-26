// ============================================================
// ArkFxEffectHost - ArkFx 特效实例的生命周期宿主（P0）
//
// 挂在每个 AB 特效实例的根节点上，负责：
//   1. Play()      : 按顺序驱动所有 ArkFxBehaviour.OnPlay()，并把粒子踢起来
//   2. 结束判定     : 全部粒子死亡 + 没有组件在等待 -> OnFinish()
//   3. 倍速下发     : SetPlaybackSpeed() 同步到粒子和行为组件
//   4. 暂停/回收    : OnPause / OnRecycle 广播
//
// 设计原则：
//   - 只做"时序广播"，不接管对象池与销毁（那仍然是 Effect.cs / EffectManager 的事）
//   - 没有粒子系统的特效（纯 Animator / 纯网格）不自动判定结束，
//     交给 Effect.cs 的 EffectData.LifeTime 兜底，避免误杀
// ============================================================
using UnityEngine;

[DisallowMultipleComponent]
public class ArkFxEffectHost : MonoBehaviour
{
    public bool Finished { get; private set; }
    public bool Paused { get; private set; }
    public float PlaybackSpeed { get; private set; } = 1f;

    ArkFxBehaviour[] _behaviours = new ArkFxBehaviour[0];
    ParticleSystem[] _particles = new ParticleSystem[0];
    bool _played;

    /// <summary>取（没有就加）宿主组件。ArkFxLoader 在实例化/烘焙时调用。</summary>
    public static ArkFxEffectHost Ensure(GameObject root)
    {
        if (root == null) return null;
        var host = root.GetComponent<ArkFxEffectHost>();
        if (host == null) host = root.AddComponent<ArkFxEffectHost>();
        return host;
    }

    /// <summary>重新扫描子节点上的行为组件与粒子系统（组件可能在运行时才动态挂上）。</summary>
    public void Refresh()
    {
        _behaviours = GetComponentsInChildren<ArkFxBehaviour>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    /// <summary>开始播放。重复调用无副作用（对应原版 Effect.Play 的幂等性）。</summary>
    public void Play()
    {
        if (_played && !Finished) return;
        Refresh();
        _played = true;
        Finished = false;

        for (int i = 0; i < _behaviours.Length; i++)
        {
            var b = _behaviours[i];
            if (b == null) continue;
            b.PlaybackSpeed = PlaybackSpeed;
            b.OnPreload();
            b.OnPlay();
        }

        // 把 playOnAwake=false 的粒子也踢起来（原版效果机的等价行为）
        for (int i = 0; i < _particles.Length; i++)
        {
            var ps = _particles[i];
            if (ps != null && !ps.isPlaying) ps.Play(true);
        }
    }

    public void SetPlaybackSpeed(float speed)
    {
        PlaybackSpeed = speed <= 0f ? 1f : speed;
        for (int i = 0; i < _particles.Length; i++)
        {
            var ps = _particles[i];
            if (ps == null) continue;
            var main = ps.main;
            main.simulationSpeed = PlaybackSpeed;
        }
        for (int i = 0; i < _behaviours.Length; i++)
            if (_behaviours[i] != null) _behaviours[i].PlaybackSpeed = PlaybackSpeed;
    }

    public void SetPaused(bool paused)
    {
        if (Paused == paused) return;
        Paused = paused;
        for (int i = 0; i < _behaviours.Length; i++)
            if (_behaviours[i] != null) _behaviours[i].OnPause(paused);
    }

    /// <summary>回池前的清理广播（对象池复用时调用）。</summary>
    public void Recycle()
    {
        for (int i = 0; i < _behaviours.Length; i++)
            if (_behaviours[i] != null) _behaviours[i].OnRecycle();
        _played = false;
        Finished = false;
        Paused = false;
    }

    /// <summary>是否还有行为组件在等待（FxDelay 的延时、发射器的 preDelay 等）。</summary>
    public bool IsBusy
    {
        get
        {
            for (int i = 0; i < _behaviours.Length; i++)
            {
                var b = _behaviours[i];
                if (b != null && b.enabled && b.IsBusy) return true;
            }
            return false;
        }
    }

    /// <summary>手动宣告结束（例如 Effect.cs 的 LifeTime 到期时）。</summary>
    public void ForceFinish()
    {
        if (Finished) return;
        Finished = true;
        for (int i = 0; i < _behaviours.Length; i++)
            if (_behaviours[i] != null) _behaviours[i].OnFinish();
    }

    void Update()
    {
        if (!_played || Finished) return;

        // 还有组件在等待（比如 FxDelay 的延时、发射器的 preDelay）-> 不算结束
        for (int i = 0; i < _behaviours.Length; i++)
        {
            var b = _behaviours[i];
            if (b != null && b.enabled && b.IsBusy) return;
        }

        // 没有任何粒子系统 -> 不自动判死，交给 Effect.cs 的 LifeTime 兜底
        if (_particles.Length == 0) return;

        for (int i = 0; i < _particles.Length; i++)
        {
            var ps = _particles[i];
            if (ps != null && ps.IsAlive(true)) return;
        }
        ForceFinish();
    }
}
