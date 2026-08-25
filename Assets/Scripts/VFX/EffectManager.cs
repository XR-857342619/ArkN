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
            var go = ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + config.Prefab);
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
        result.ResetEffect();
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
