// ============================================================
// ArkFxEmitterBase - 发射器替身基类（P3）
//
// 对应原版 Torappu.Battle.Effects 下的 OnPlayEmitter / OnFinishEmitter / RandomEmitter。
// 原版通过 BattleController.CreateEffect(name, pos, owner) 生成子特效；
// 这里等价实现为：ArkFxLoader.LoadPrefabByName(name) -> Instantiate -> Kick。
//
// 两个关键时序点（实测数据驱动）：
//   1. 50 个发射器全部挂在特效根节点，且其中 2 个 prefab 同时带 FxDelay。
//      FxDelay 会把根节点隐藏，所以发射器不能在 OnPlay 时无条件发射，
//      要先判断 activeInHierarchy；若被隐藏则等 OnEnable（= 被重新激活）再发。
//      —— 用 armed 标志保证烘焙到 prefab 资产时的 OnEnable 不会误触发。
//   2. 子特效是独立根对象，不随父特效销毁，必须自己登记并在结束/回池时清理。
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ArkFxEmitterBase : ArkFxBehaviour
{
    [Tooltip("结束时是否销毁已发射的子特效")]
    public bool clearWhenFinish = true;
    [Tooltip("发射前延迟（秒）")]
    public float preDelay;

    protected readonly List<GameObject> spawned = new List<GameObject>();
    protected bool armed;     // OnPlay 过（烘焙期的 OnEnable 不算）
    protected bool pending;   // 被隐藏了，等激活后补发

    public override void OnPlay()
    {
        armed = true;
        if (!gameObject.activeInHierarchy) { pending = true; return; }
        BeginEmit();
    }

    protected virtual void OnEnable()
    {
        if (armed && pending)
        {
            pending = false;
            BeginEmit();
        }
    }

    protected void BeginEmit()
    {
        if (preDelay > 0f) StartCoroutine(EmitAfterDelay());
        else DoEmit();
    }

    IEnumerator EmitAfterDelay()
    {
        yield return new WaitForSeconds(preDelay);
        DoEmit();
    }

    protected abstract void DoEmit();

    /// <summary>发射一个子特效（按名字从 arkfx 包里加载）。</summary>
    protected GameObject Emit(string effectName)
    {
        return Emit(effectName, transform.position, transform.rotation);
    }

    protected GameObject Emit(string effectName, Vector3 pos, Quaternion rot)
    {
        if (string.IsNullOrEmpty(effectName)) return null;

        var prefab = ArkFxLoader.LoadPrefabByName(effectName);
        if (prefab == null)
        {
            // 源资产里就没有这个子特效（客户端版本差异），跳过即可，不崩
            Debug.LogWarning($"[ArkFx] 子特效缺失（arkfx 里找不到）: {effectName}");
            return null;
        }

        var go = Instantiate(prefab, pos, rot);
        go.name = effectName;
        ArkFxLoader.Kick(go, effectName);
        spawned.Add(go);
        return go;
    }

    /// <summary>子类在特效结束时需要执行的发射（OnFinishEmitter 用）。</summary>
    protected virtual void OnFinishEmit() { }

    public override void OnFinish()
    {
        OnFinishEmit();
        if (clearWhenFinish) ClearSpawned();
    }

    protected void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();
    }

    /// <summary>已发射且仍存活的子特效数量（诊断用）。</summary>
    public int SpawnedCount { get { return spawned.Count; } }

    /// <summary>是否在等节点重新激活（被 FxDelay 隐藏时为真）。</summary>
    public bool IsPending { get { return pending; } }

    public override void OnRecycle()
    {
        StopAllCoroutines();
        armed = false;
        pending = false;
        ClearSpawned();
    }
}
