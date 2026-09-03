using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

//TODO 资源池
public class ResHelper
{
    private static readonly object preloadLock = new object();

    // 预加载句柄缓存：路径 -> Addressables Handle
    private static readonly Dictionary<string, AsyncOperationHandle> assetHandles = new Dictionary<string, AsyncOperationHandle>();

    // 正在加载中的任务，避免同一资源并发重复加载
    private static readonly Dictionary<string, Task> pendingAssetTasks = new Dictionary<string, Task>();

    private static readonly HashSet<int> units = new HashSet<int>();
    private static readonly HashSet<int> skills = new HashSet<int>();

    public static T GetAsset<T>(string path)
    {
        var op = Addressables.LoadAssetAsync<T>(path);
        op.WaitForCompletion();
        if (op.Status == AsyncOperationStatus.Succeeded)
        {
            return op.Result;
        }
        throw new Exception($"Addressables 加载失败: {path}, Status={op.Status}, Error={op.OperationException}");
    }

    public static async Task<T> GetAssetAsync<T>(string path)
    {
        var op = Addressables.LoadAssetAsync<T>(path);
        await op.Task;
        if (op.Status == AsyncOperationStatus.Succeeded)
        {
            return op.Result;
        }
        throw new Exception($"Addressables 加载失败: {path}, Status={op.Status}, Error={op.OperationException}");
    }

    public static GameObject Instantiate(string path)
    {
        var op = Addressables.InstantiateAsync(path);
        op.WaitForCompletion();
        return op.Result;
    }

    public static async Task<GameObject> InstantiateAsyncImmediate(string path)
    {
        var g = await Addressables.LoadAssetAsync<GameObject>(path).Task;
        var r = await Addressables.InstantiateAsync(path).Task;
        return r;
    }

    public static void Return(GameObject go)
    {
        Addressables.ReleaseInstance(go);
    }

    public static void Return<T>(T go)
    {
        Addressables.Release(go);
    }

    // ===== 预加载 =====

    public static async Task Prepare(int unitId, int mainSkillIndex = -1)
    {
#if UNITY_EDITOR
        return;
#endif
        Debug.Log("Prepare " + unitId);
        if (unitId == -1) unitId = 256;

        lock (preloadLock)
        {
            if (units.Contains(unitId)) return;
            units.Add(unitId);
        }

        UnitData unitData = Database.Instance.Get<UnitData>(unitId);
        if (unitData == null) return;

        // 外部 Spine 模型直接走 SpineImportHelper 预加载
        PrepareSpineModel(unitData);

        // 普通 Prefab 模型走 Addressables 预加载
        if (!SpineImportHelper.Instance.loadedSkeletons.ContainsKey(unitData.Model))
        {
            await PreloadAssetAsync(PathHelper.UnitPath + unitData.Model);
        }

        var tasks = new List<Task>();

        if (unitData.Skills != null)
        {
            foreach (var skillId in unitData.Skills)
                tasks.Add(PrepareSkill(skillId));
        }

        if (mainSkillIndex >= 0 && unitData.MainSkill != null && unitData.MainSkill.Length > mainSkillIndex)
        {
            tasks.Add(PrepareSkill(unitData.MainSkill[mainSkillIndex]));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 预加载外部 Spine 资源（如果 UnitData.Model 对应 SpineData）。
    /// </summary>
    public static void PrepareSpineModel(UnitData unitData)
    {
        if (unitData == null || string.IsNullOrEmpty(unitData.Model)) return;

        if (SpineImportHelper.Instance.loadedSkeletons.ContainsKey(unitData.Model)) return;

        SpineData spineData = Database.Instance.Get<SpineData>(unitData.Model);
        if (spineData == null) return;

        string pathHead = spineData.UseAppHotfixResPath ? PathHelper.AppHotfixResPath : "";
        SpineImportHelper.Instance.LoadSpineAssets(
            spineData.Id,
            pathHead + spineData.FrontPngPath,
            pathHead + spineData.FrontAtlasPath,
            pathHead + spineData.FrontSkelPath);

        if (!spineData.OnlyFront)
        {
            SpineImportHelper.Instance.LoadSpineAssets(
                spineData.Id + "_back",
                pathHead + spineData.BackPngPath,
                pathHead + spineData.BackAtlasPath,
                pathHead + spineData.BackSkelPath);
        }
    }

    protected static async Task PrepareSkill(int skillId)
    {
        lock (preloadLock)
        {
            if (skills.Contains(skillId)) return;
            skills.Add(skillId);
        }

        SkillData skillData = Database.Instance.Get<SkillData>(skillId);
        if (skillData == null) return;

        var tasks = new List<Task>
        {
            PrepareEffect(skillData.ReadyEffect),
            PrepareEffect(skillData.StartEffect),
            PrepareEffect(skillData.CastEffect),
            PrepareEffect(skillData.HitEffect),
            PrepareEffect(skillData.EffectEffect),
            PrepareEffect(skillData.GatherEffect),
            PrepareEffect(skillData.LoopStartEffect),
            PrepareEffect(skillData.LoopCastEffect)
        };

        if (skillData.Buffs != null)
        {
            foreach (var buff in skillData.Buffs)
                tasks.Add(PrepareBuff(buff));
        }

        if (skillData.Bullet != null)
        {
            var b = Database.Instance.Get<BulletData>(skillData.Bullet.Value);
            if (b != null)
            {
                if (!string.IsNullOrEmpty(b.Line))
                    tasks.Add(PreloadAssetAsync(PathHelper.EffectPath + b.Line));
                if (!string.IsNullOrEmpty(b.Model))
                    tasks.Add(PreloadAssetAsync(PathHelper.EffectPath + b.Model));
            }
        }

        if (skillData.Skills != null)
        {
            foreach (var sk in skillData.Skills)
                tasks.Add(PrepareSkill(sk));
        }

        if (skillData.ExSkills != null)
        {
            foreach (var sk in skillData.ExSkills)
                tasks.Add(PrepareSkill(sk));
        }

        await Task.WhenAll(tasks);
    }

    static async Task PrepareBuff(int buffId)
    {
        var buffData = Database.Instance.Get<BuffData>(buffId);
        if (buffData == null) return;
        await PrepareEffect(buffData.LastingEffect);
    }

    static async Task PrepareEffect(int[] effectIds)
    {
        if (effectIds == null) return;

        var tasks = new List<Task>();
        foreach (var effectId in effectIds)
        {
            var effectData = Database.Instance.Get<EffectData>(effectId);
            if (effectData == null || string.IsNullOrEmpty(effectData.Prefab)) continue;
            tasks.Add(PreloadAssetAsync(PathHelper.EffectPath + effectData.Prefab));
        }

        await Task.WhenAll(tasks);
    }

    static async Task PrepareEffect(int? effectId)
    {
        if (effectId == null) return;

        var effectData = Database.Instance.Get<EffectData>(effectId.Value);
        if (effectData == null || string.IsNullOrEmpty(effectData.Prefab)) return;

        await PreloadAssetAsync(PathHelper.EffectPath + effectData.Prefab);
    }

    /// <summary>
    /// 按路径去重并缓存 Addressables 句柄。
    /// </summary>
    private static async Task PreloadAssetAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        Task pending;
        lock (preloadLock)
        {
            if (assetHandles.ContainsKey(path)) return;
            if (pendingAssetTasks.TryGetValue(path, out pending))
            {
                // 已有相同资源正在加载，直接等待
            }
            else
            {
                pending = null;
            }
        }

        if (pending != null)
        {
            await pending;
            return;
        }

        var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(path);
        var task = handle.Task;

        lock (preloadLock)
        {
            pendingAssetTasks[path] = task;
        }

        try
        {
            await task;
            lock (preloadLock)
            {
                if (!assetHandles.ContainsKey(path))
                    assetHandles[path] = handle;
                pendingAssetTasks.Remove(path);
            }
        }
        catch
        {
            lock (preloadLock)
            {
                pendingAssetTasks.Remove(path);
            }
            if (handle.IsValid()) Addressables.Release(handle);
            throw;
        }
    }

    /// <summary>
    /// 释放所有预加载句柄，并清空去重集合。战斗结束/切场景时调用。
    /// </summary>
    public static void ReleasePreloadedAssets()
    {
        lock (preloadLock)
        {
            foreach (var kv in assetHandles)
            {
                if (kv.Value.IsValid())
                    Addressables.Release(kv.Value);
            }

            assetHandles.Clear();
            pendingAssetTasks.Clear();
            units.Clear();
            skills.Clear();
        }
    }
}
