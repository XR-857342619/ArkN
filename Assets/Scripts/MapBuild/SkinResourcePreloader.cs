using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 地块皮肤资源预载器（R5）。
///
/// 背景：换皮功能之前是"用到才同步加载"——`MapManager.Build` 逐格
/// `ResHelper.Instantiate(key)` → `Addressables.InstantiateAsync(key).WaitForCompletion()`，
/// 单张图最多一百多格皮肤，进战斗时会在主线程上连续做一百多次阻塞加载，表现为明显卡顿。
///
/// 做法：进战斗前（加载界面里）先把本图**所有**皮肤 prefab 异步预载进 Addressables 缓存，
/// 之后逐格实例化就只做"缓存命中 + 实例化"，不再有 IO 等待。
/// 预载进度通过回调抛给加载界面，玩家能看到"正在预载地块皮肤 (12/45)"。
///
/// 释放：预载持有的是**资产**级引用，实例化出来的对象各自持有自己的引用，
/// 所以场景搭好之后调用 <see cref="ReleaseAll"/> 释放资产引用即可，不会影响已实例化的皮肤。
/// </summary>
public static class SkinResourcePreloader
{
    static readonly List<AsyncOperationHandle> _handles = new List<AsyncOperationHandle>();

    /// <summary>本图需要预载的皮肤种类数（去重后）。加载界面拿它决定要不要显示这一步。</summary>
    public static int Count(MapInfo map)
    {
        return CollectInfos(map).Count;
    }

    /// <summary>
    /// 异步预载本图用到的全部皮肤资源。
    /// onProgress(已完成数, 总数, 当前资源名)：total == 0 表示本图没有皮肤。
    /// 单个资源失败只告警、不中断（缺皮的那一格留给运行时兜底逻辑处理）。
    /// </summary>
    public static async Task PrepareAsync(MapInfo map, Action<int, int, string> onProgress = null)
    {
        var infos = CollectInfos(map);
        int total = infos.Count;
        if (total == 0)
        {
            onProgress?.Invoke(0, 0, null);
            return;
        }

        var loadedKeys = new HashSet<string>();
        for (int i = 0; i < total; i++)
        {
            var info = infos[i];
            string name = string.IsNullOrEmpty(info.SkinName) ? "?" : info.SkinName;
            onProgress?.Invoke(i, total, name);

            // 与 GridSkinData 用同一套键规则（原生键 + 去扩展名兜底），
            // 所以预载命中的键就是运行时真正会用的那个。
            foreach (var key in GridSkinData.BuildLoadKeys(info))
            {
                if (loadedKeys.Contains(key)) break;  // 本图前面的皮肤已经把这个键载进来了
                if (await TryLoadAsync(key))
                {
                    loadedKeys.Add(key);
                    break;
                }
            }

            onProgress?.Invoke(i + 1, total, name);
        }
        Debug.Log($"[SkinPreload] 皮肤预载完成：{total} 种，持有句柄 {_handles.Count} 个");
    }

    /// <summary>释放预载持有的资产引用（场景里的皮肤实例各自持有引用，不受影响）</summary>
    public static void ReleaseAll()
    {
        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
        _handles.Clear();
    }

    static async Task<bool> TryLoadAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        AsyncOperationHandle<GameObject> handle = default(AsyncOperationHandle<GameObject>);
        try
        {
            handle = Addressables.LoadAssetAsync<GameObject>(key);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                _handles.Add(handle);
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SkinPreload] 预载失败：{key}（{e.GetType().Name}: {e.Message}）");
        }
        if (handle.IsValid()) Addressables.Release(handle);
        return false;
    }

    /// <summary>收集本图全部皮肤条目（主皮肤 + 叠加层），按 地址/名字 去重</summary>
    static List<SkinInfo> CollectInfos(MapInfo map)
    {
        var list = new List<SkinInfo>();
        var seen = new HashSet<string>();
        if (map == null || map.GridInfos == null) return list;

        foreach (var gi in map.GridInfos)
        {
            if (gi == null) continue;
            AddInfo(gi.Skin, list, seen);
            if (gi.SkinExtras == null) continue;
            for (int i = 0; i < gi.SkinExtras.Count; i++)
                AddInfo(gi.SkinExtras[i], list, seen);
        }
        return list;
    }

    static void AddInfo(SkinInfo info, List<SkinInfo> list, HashSet<string> seen)
    {
        if (info == null || string.IsNullOrEmpty(info.SkinName)) return;
        string key = (info.SkinAddress ?? "") + "|" + info.SkinName;
        if (!seen.Add(key)) return;
        list.Add(info);
    }
}
