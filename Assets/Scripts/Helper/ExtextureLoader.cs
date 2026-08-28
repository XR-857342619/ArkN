using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using FairyGUI;

public class ExtextureLoader : MonoBehaviour
{
    private static readonly object _lock = new object();

    public static ExtextureLoader Instance
    {
        get
        {
            if (instance == null)
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        GameObject gameObject = new GameObject("ExtextureManager");
                        DontDestroyOnLoad(gameObject);
                        instance = gameObject.AddComponent<ExtextureLoader>();
                    }
                }
            }
            return instance;
        }
    }
    private static ExtextureLoader instance;

    public Dictionary<string, GLoader> loaderDict = new Dictionary<string, GLoader>();

    // FairyGUI 贴图缓存：供 GLoader 直接使用。
    readonly Dictionary<string, NTexture> textureCache = new Dictionary<string, NTexture>();

    // 原始 Texture2D 缓存：供其他脚本直接获取/使用。
    readonly Dictionary<string, Texture2D> texture2DCache = new Dictionary<string, Texture2D>();

    // 相同路径并发请求合并，避免同一纹理被重复下载。
    readonly Dictionary<string, List<Action<Texture2D>>> pendingSuccesses = new Dictionary<string, List<Action<Texture2D>>>();
    readonly Dictionary<string, List<Action<string>>> pendingFailures = new Dictionary<string, List<Action<string>>>();

    /// <summary>
    /// 从外部路径加载 Texture2D（支持本地相对/绝对路径，以及 http/https/file URI）。
    /// 加载完成后会写入缓存，其他脚本可通过相同路径直接复用。
    /// </summary>
    public void LoadTexture2D(string pathOrUrl, Action<Texture2D> onSuccess, Action<string> onFailed = null)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            onFailed?.Invoke("path is null or empty");
            return;
        }

        string cacheKey = GetCacheKey(pathOrUrl);

        // 缓存命中：直接返回，不再发 UnityWebRequest
        if (texture2DCache.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
        {
            onSuccess?.Invoke(cached);
            return;
        }

        // 已有相同路径正在加载：合并回调，避免重复请求
        if (pendingSuccesses.TryGetValue(cacheKey, out List<Action<Texture2D>> existing))
        {
            if (onSuccess != null)
                existing.Add(onSuccess);

            if (onFailed != null)
            {
                if (!pendingFailures.TryGetValue(cacheKey, out List<Action<string>> fails))
                {
                    fails = new List<Action<string>>();
                    pendingFailures[cacheKey] = fails;
                }
                fails.Add(onFailed);
            }
            return;
        }

        pendingSuccesses[cacheKey] = new List<Action<Texture2D>>();
        if (onSuccess != null)
            pendingSuccesses[cacheKey].Add(onSuccess);

        if (onFailed != null)
            pendingFailures[cacheKey] = new List<Action<string>> { onFailed };

        StartCoroutine(LoadTexture2DFromPath(pathOrUrl, cacheKey));
    }

    /// <summary>
    /// 同步查询缓存中是否已有指定外部路径对应的 Texture2D。
    /// </summary>
    public bool TryGetCachedTexture2D(string pathOrUrl, out Texture2D texture)
    {
        texture = null;
        if (string.IsNullOrEmpty(pathOrUrl))
            return false;

        string cacheKey = GetCacheKey(pathOrUrl);
        return texture2DCache.TryGetValue(cacheKey, out texture) && texture != null;
    }

    /// <summary>
    /// 为 FairyGUI GLoader 加载本地外部贴图；内部复用 Texture2D 缓存。
    /// </summary>
    public void LoadLocalTexture(GLoader loader, string localFileName, Action onSuccess = null, Action onFailed = null)
    {
        if (loader == null || string.IsNullOrEmpty(localFileName))
        {
            onFailed?.Invoke();
            return;
        }

        string relativePath = "Icon/" + localFileName.TrimStart('/');
        string cacheKey = GetCacheKey(relativePath);

        // 缓存命中：直接使用，不再发 UnityWebRequest
        if (textureCache.TryGetValue(cacheKey, out NTexture cachedN) && cachedN != null)
        {
            loader.url = "";
            loader.texture = cachedN;
            onSuccess?.Invoke();
            return;
        }

        if (texture2DCache.TryGetValue(cacheKey, out Texture2D cachedT) && cachedT != null)
        {
            NTexture nTexture = CreateAndCacheNTexture(cacheKey, cachedT);
            loader.url = "";
            loader.texture = nTexture;
            onSuccess?.Invoke();
            return;
        }

        // 先清空旧显示，避免异步加载期间残留上一个单位的外部贴图
        loader.url = "";
        loader.texture = null;

        string key = $"{cacheKey}_{loader.GetHashCode()}";
        if (loaderDict.ContainsKey(key))
            loaderDict.Remove(key);
        loaderDict.Add(key, loader);

        LoadTexture2D(relativePath,
            texture =>
            {
                if (!loaderDict.TryGetValue(key, out GLoader currentLoader) || currentLoader != loader)
                {
                    Debug.LogWarning("加载目标已失效，终止处理");
                    return;
                }
                loaderDict.Remove(key);

                NTexture fairyTexture = CreateAndCacheNTexture(cacheKey, texture);
                loader.url = "";
                loader.texture = fairyTexture;
                onSuccess?.Invoke();
            },
            error =>
            {
                if (loaderDict.TryGetValue(key, out GLoader currentLoader) && currentLoader == loader)
                    loaderDict.Remove(key);

                Debug.LogError($"加载失败: {error}，路径: {relativePath}");
                loader.url = "ui://Res/missing";
                onFailed?.Invoke();
            });
    }

    private IEnumerator LoadTexture2DFromPath(string pathOrUrl, string cacheKey)
    {
        string requestPath = ToRequestPath(pathOrUrl);
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(requestPath))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null)
                {
                    CacheTexture2D(cacheKey, texture);
                    CompletePendingSuccess(cacheKey, texture);
                }
                else
                {
                    CompletePendingFailure(cacheKey, $"加载成功但纹理为空: {requestPath}");
                }
            }
            else
            {
                CompletePendingFailure(cacheKey, $"{webRequest.error}，路径: {requestPath}");
            }
        }
    }

    private void CacheTexture2D(string cacheKey, Texture2D texture)
    {
        if (!texture2DCache.ContainsKey(cacheKey))
            texture2DCache[cacheKey] = texture;
    }

    private NTexture CreateAndCacheNTexture(string cacheKey, Texture2D texture)
    {
        if (textureCache.TryGetValue(cacheKey, out NTexture cached) && cached != null)
            return cached;

        // NTexture 不负责销毁底层 Texture2D，统一由 texture2DCache 管理生命周期。
        NTexture fairyTexture = new NTexture(texture)
        {
            destroyMethod = DestroyMethod.None
        };
        textureCache[cacheKey] = fairyTexture;
        return fairyTexture;
    }

    private void CompletePendingSuccess(string cacheKey, Texture2D texture)
    {
        if (pendingSuccesses.TryGetValue(cacheKey, out List<Action<Texture2D>> callbacks))
        {
            var snapshot = new List<Action<Texture2D>>(callbacks);
            foreach (Action<Texture2D> callback in snapshot)
                callback?.Invoke(texture);
        }

        pendingSuccesses.Remove(cacheKey);
        pendingFailures.Remove(cacheKey);
    }

    private void CompletePendingFailure(string cacheKey, string error)
    {
        if (pendingFailures.TryGetValue(cacheKey, out List<Action<string>> callbacks))
        {
            var snapshot = new List<Action<string>>(callbacks);
            foreach (Action<string> callback in snapshot)
                callback?.Invoke(error);
        }

        pendingSuccesses.Remove(cacheKey);
        pendingFailures.Remove(cacheKey);
    }

    private static string GetCacheKey(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
            return pathOrUrl;

        if (IsUrl(pathOrUrl))
            return pathOrUrl;

        return PathHelper.NormalizeAppPath(pathOrUrl).TrimStart('/');
    }

    private static string ToRequestPath(string pathOrUrl)
    {
        if (IsUrl(pathOrUrl))
            return pathOrUrl;

        string normalizedPath = PathHelper.NormalizeAppPath(pathOrUrl);
        return "file:///" + normalizedPath.TrimStart('/');
    }

    private static bool IsUrl(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 清空本地贴图缓存，一般在切场景/热更后调用。
    /// </summary>
    public void ClearCache()
    {
        foreach (KeyValuePair<string, NTexture> kv in textureCache)
        {
            kv.Value?.Dispose();
        }
        textureCache.Clear();

        foreach (KeyValuePair<string, Texture2D> kv in texture2DCache)
        {
            if (kv.Value == null)
                continue;

            if (Application.isPlaying)
                Destroy(kv.Value);
            else
                DestroyImmediate(kv.Value);
        }
        texture2DCache.Clear();

        pendingSuccesses.Clear();
        pendingFailures.Clear();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        loaderDict.Clear();
        ClearCache();
    }
}