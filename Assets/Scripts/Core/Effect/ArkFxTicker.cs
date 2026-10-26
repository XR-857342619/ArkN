// ============================================================
// ArkFxTicker - ArkFx 的全局计时器（P2）
//
// 为什么需要它：
//   Torappu.Fx.FxDelay 会把自己所在的 GameObject 整个 SetActive(false)。
//   而实测 145 个 FxDelay 里有 144 个挂在特效 prefab 的【根节点】上，
//   根节点被隐藏后，挂在根上的 ArkFxEffectHost 的 Update 也会停
//   → 延时永远走不完，特效再也出不来。
//
//   原版靠游戏自己的全局调度器驱动，所以没问题；我们用一个
//   DontDestroyOnLoad 的隐藏对象来提供同样的"不受激活状态影响的时钟"。
//
// 使用：ArkFxDelay 在开始等待时 Register，结束时 Unregister。
// ============================================================
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArkFxTicker : MonoBehaviour
{
    static ArkFxTicker _instance;
    readonly List<ArkFxDelay> _delays = new List<ArkFxDelay>();

    public static void Register(ArkFxDelay delay)
    {
        if (delay == null) return;
        Ensure();
        if (_instance == null) return;
        if (!_instance._delays.Contains(delay)) _instance._delays.Add(delay);
    }

    public static void Unregister(ArkFxDelay delay)
    {
        if (_instance == null || delay == null) return;
        _instance._delays.Remove(delay);
    }

    static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("[ArkFxTicker]");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<ArkFxTicker>();
    }

    void Update()
    {
        if (_delays.Count == 0) return;
        float dt = Time.deltaTime;
        for (int i = _delays.Count - 1; i >= 0; i--)
        {
            var d = _delays[i];
            if (d == null) { _delays.RemoveAt(i); continue; }   // 特效被销毁了
            d.TickerUpdate(dt);
        }
    }

    void OnDestroy()
    {
        _instance = null;
    }
}
