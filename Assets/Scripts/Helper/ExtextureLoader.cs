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

    // 本地贴图缓存：避免待部署区每次刷新都重新从磁盘/包体加载同一张图。
    readonly Dictionary<string, NTexture> textureCache = new Dictionary<string, NTexture>();

    public void LoadLocalTexture(GLoader loader, string localFileName, Action onSuccess = null, Action onFailed = null)
    {
        if (loader == null || string.IsNullOrEmpty(localFileName))
        {
            onFailed?.Invoke();
            return;
        }

        string cacheKey = localFileName.TrimStart('/');

        // 缓存命中：直接使用，不再发 UnityWebRequest
        if (textureCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            loader.url = "";
            loader.texture = cached;
            onSuccess?.Invoke();
            return;
        }

        // 先清空旧显示，避免异步加载期间残留上一个单位的外部贴图
        loader.url = "";
        loader.texture = null;

        string key = $"{cacheKey}_{loader.GetHashCode()}";

        if (loaderDict.ContainsKey(key))
        {
            loaderDict.Remove(key);
        }
        loaderDict.Add(key, loader);

        // 统一规范化相对路径，避免不同平台拼接差异
        string relativePath = "Icon/" + cacheKey;
        string normalizedPath = PathHelper.NormalizeAppPath(relativePath);

        // UnityWebRequest 使用 file:/// 形式的本地文件 URI
        string localPath = "file:///" + normalizedPath.TrimStart('/');

        StartCoroutine(LoadTextureFromPath(loader, localPath, key, cacheKey, onSuccess, onFailed));
    }

    private IEnumerator LoadTextureFromPath(
        GLoader imgLoader,
        string pathOrUrl,
        string key,
        string cacheKey,
        Action onSuccess,
        Action onFailed)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(pathOrUrl))
        {
            yield return webRequest.SendWebRequest();

            // 检查 loader 是否已被销毁或移除
            if (!loaderDict.TryGetValue(key, out var currentLoader) || currentLoader != imgLoader)
            {
                Debug.LogWarning("加载目标已失效，终止处理");
                yield break;
            }
            loaderDict.Remove(key);

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D unityTexture = DownloadHandlerTexture.GetContent(webRequest);
                if (unityTexture != null)
                {
                    NTexture fairyTexture = new NTexture(unityTexture);

                    // 写入缓存，后续同文件直接复用
                    if (!textureCache.ContainsKey(cacheKey))
                        textureCache[cacheKey] = fairyTexture;

                    imgLoader.texture = fairyTexture;
                    onSuccess?.Invoke();
                }
                else
                {
                    Debug.LogError("加载成功但纹理为空: " + pathOrUrl);
                    imgLoader.url = "ui://Res/missing";
                    onFailed?.Invoke();
                }
            }
            else
            {
                Debug.LogError($"加载失败: {webRequest.error}，路径: {pathOrUrl}");
                    imgLoader.url = "ui://Res/missing";
                onFailed?.Invoke();
            }
        }
    }

    /// <summary>
    /// 清空本地贴图缓存，一般在切场景/热更后调用。
    /// </summary>
    public void ClearCache()
    {
        foreach (var kv in textureCache)
        {
            kv.Value?.Dispose();
        }
        textureCache.Clear();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        loaderDict.Clear();
        ClearCache();
    }
}
