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

        bool fromAb = false;
        if (!effectPrefabs.TryGetValue(id, out var prefab) || prefab == null)
        {
            var go = ArkFxLoader.ForceAbSource
                ? null
                : ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + config.Prefab);
            if (go == null)
            {
                // AB 特效兜底：工程内没有该 prefab 时，从 arkfx 运行时包加载并补挂驱动组件
                go = ArkFxLoader.LoadPrefabByName(config.Prefab);
                if (go != null) { go.AddComponent<Effect>(); fromAb = true; }
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
        // 必须先清理池化残留（内部会调 ArkFxEffectHost.Recycle），再 Kick。
        // 反过来的话，ResetEffect 会把 ArkFxDelay 刚隐藏的根节点重新激活，延迟被抵消。
        result.ResetEffect();
        // AB 来源的特效：实例化后踢动效（粒子/动画器）；驱动组件已烘焙进 prefab，克隆自带不会重复
        if (fromAb) ArkFxLoader.Kick(result.gameObject, config.Prefab);
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
