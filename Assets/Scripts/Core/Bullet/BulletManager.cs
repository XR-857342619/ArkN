using System.Collections.Generic;
using UnityEngine;

public class BulletManager
{
    public static BulletManager Instance => instance == null ? instance = new BulletManager() : instance;
    private static BulletManager instance;

    Pool<BulletModel> Pool = new Pool<BulletModel>();
    Pool<PullLineModel> Pool1 = new Pool<PullLineModel>();

    readonly Dictionary<string, BulletModel> bulletPrefabs = new Dictionary<string, BulletModel>();
    readonly Dictionary<string, PullLineModel> linePrefabs = new Dictionary<string, PullLineModel>();

    // 比例导引追踪子弹的帧间状态缓存，key 为 Bullet 对象，避免把追踪字段侵入 Bullet 基类。
    readonly Dictionary<Bullet, HomingTrackingData> homingData = new Dictionary<Bullet, HomingTrackingData>();

    public BulletModel Get(string model)
    {
        if (!bulletPrefabs.TryGetValue(model, out var prefab) || prefab == null)
        {
            var go = ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + model);
            prefab = go != null ? go.GetComponent<BulletModel>() : null;
            if (prefab == null)
            {
                Debug.LogError($"子弹模型加载失败或缺少 BulletModel: {PathHelper.EffectPath + model}");
                return null;
            }
            bulletPrefabs[model] = prefab;
        }

        return Pool.Spawn(prefab, Vector3.zero);
    }

    public void Return(BulletModel bulletModel)
    {
        if (bulletModel == null) return;
        bulletModel.GetComponent<Effect>()?.ResetEffect();
        Pool.Despawn(bulletModel);
    }

    public PullLineModel GetLine(string model)
    {
        if (!linePrefabs.TryGetValue(model, out var prefab) || prefab == null)
        {
            var go = ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + model);
            prefab = go != null ? go.GetComponent<PullLineModel>() : null;
            if (prefab == null)
            {
                Debug.LogError($"拉线模型加载失败或缺少 PullLineModel: {PathHelper.EffectPath + model}");
                return null;
            }
            linePrefabs[model] = prefab;
        }

        return Pool1.Spawn(prefab, Vector3.zero);
    }

    public void Return(PullLineModel bulletModel)
    {
        if (bulletModel == null) return;
        Pool1.Despawn(bulletModel);
    }

    public HomingTrackingData GetOrCreateHomingData(Bullet bullet)
    {
        if (bullet == null) return null;

        if (!homingData.TryGetValue(bullet, out var data))
        {
            data = new HomingTrackingData();
            homingData[bullet] = data;
        }
        return data;
    }

    public void ReleaseHomingData(Bullet bullet)
    {
        if (bullet == null) return;
        homingData.Remove(bullet);
    }

    public void ClearHomingData()
    {
        homingData.Clear();
    }

    public void ReturnAll()
    {
        Pool.DespawnAll();
        Pool1.DespawnAll();
        homingData.Clear();
    }
}
