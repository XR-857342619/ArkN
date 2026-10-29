using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject gameObject = new GameObject("EffectManager");
                DontDestroyOnLoad(gameObject);
                instance = gameObject.AddComponent<EffectManager>();
            }
            return instance;
        }
    }
    private static EffectManager instance;

    public Pool<Effect> pool = new Pool<Effect>();

    // 按 EffectData.Id 缓存 Prefab，避免每次 GetEffect 都同步加载 Addressable。
    readonly Dictionary<int, Effect> effectPrefabs = new Dictionary<int, Effect>();

    void Start()
    {
    }

    void Update()
    {
    }

    /// <summary>
    /// 稳定字符串 Id 入口。新配置优先走这里；内部仍复用 int 缓存/池化路径。
    /// </summary>
    public Effect GetEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId))
        {
            Debug.LogError("[EffectManager] 特效 Id 为空");
            return null;
        }

        var config = Database.Instance.Get<EffectData>(effectId);
        if (config == null)
        {
            Debug.LogError($"[EffectManager] 特效配置不存在: {effectId}");
            return null;
        }

        int index = Database.Instance.GetIndex(config);
        if (index < 0)
        {
            Debug.LogError($"[EffectManager] 特效配置索引解析失败: {effectId}");
            return null;
        }
        return GetEffect(index);
    }

    public Effect GetEffect(int id)
    {
        var config = Database.Instance.Get<EffectData>(id);
        if (config == null)
        {
            Debug.LogError($"特效配置不存在: {id}");
            return null;
        }

        if (!effectPrefabs.TryGetValue(id, out var prefab) || prefab == null)
        {
            // 来源优先级：默认「原版 AB 优先」，AB 里没有才回落工程内 prefab
            // （开关见 ArkFxLoader.PreferAbSource / 菜单 Tools/ArkFx/0）
            GameObject go = null;
            if (ArkFxLoader.PreferAbSource)
            {
                go = ArkFxLoader.LoadPrefabByName(config.Prefab);
                // 同一个 AB prefab 可能被多条 EffectData 引用；必须判重，
                // 否则第二次加载会在 prefab 资产上重复 AddComponent<Effect>()，
                // 池化实例上会出现多个 Update / 多次 ReturnEffect。
                if (go != null && go.GetComponent<Effect>() == null)
                    go.AddComponent<Effect>();
            }
            if (go == null)
            {
                go = ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + config.Prefab);
            }
            if (go == null)
            {
                Debug.LogError($"特效 Prefab 加载失败: {PathHelper.EffectPath + config.Prefab}");
                return null;
            }
            prefab = go.GetComponent<Effect>();
            if (prefab == null)
            {
                Debug.LogError($"特效 Prefab 上没有 Effect 组件: {PathHelper.EffectPath + config.Prefab}");
                return null;
            }
            effectPrefabs[id] = prefab;
        }

        var result = pool.Spawn(prefab, Vector3.zero, Quaternion.identity);
        if (result == null) return null;

        result.Id = id;
        result.SetLifeTime(config.LifeTime);
        // 先清理池化残留（内部会调 ArkFxEffectHost.Recycle）。
        // 这里【不 Kick】：调用方随后通常要执行 Init() 设置位置/挂点/朝向，
        // 而 OnPlayEmitter 与 FxDelay 必须在最终位置确定后启动。
        // 真正启动由 Effect.Play() 在第一次调用时完成（Init() 末尾会调用 Play）。
        result.ResetEffect();
        result.PrefabName = config.Prefab;
        return result;
    }

    public void ReturnEffect(Effect ps)
    {
        if (ps == null) return;
        ps.ResetEffect();
        pool.Despawn(ps);
    }

    public void ReturnAll()
    {
        pool.DespawnAll();
    }
}
