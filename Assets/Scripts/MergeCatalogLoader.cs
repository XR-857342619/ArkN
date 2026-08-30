using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

public class MergeCatalogLoader : MonoBehaviour
{
    [Header("Catalog 路径（支持绝对路径 / 相对 StreamingAssets / HTTP URL）")]
    [Tooltip("留空则不自动加载，可通过代码调用 LoadCatalog()")]
    public string catalogPath = "D:/临时存储/catlog/catalog.json";

    // 记录已加载路径，避免重复加载
    private static HashSet<string> loadedCatalogs = new HashSet<string>();

    void Start()
    {
        if (!string.IsNullOrEmpty(catalogPath))
        {
            StartCoroutine(LoadCatalogCoroutine(catalogPath));
        }
    }

    /// <summary>
    /// 外部调用接口：加载指定路径的 Catalog
    /// </summary>
    public void LoadCatalog(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            UnityEngine.Debug.LogWarning("Catalog 路径为空，取消加载。");
            return;
        }
        StartCoroutine(LoadCatalogCoroutine(path));
    }

    private IEnumerator LoadCatalogCoroutine(string path)
    {
        // 1. 检查是否已加载
        if (loadedCatalogs.Contains(path))
        {
            UnityEngine.Debug.Log($"Catalog 已加载，跳过重复加载: {path}");
            yield break;
        }

        // 2. 解析实际路径
        string fullPath = ResolvePath(path);
        UnityEngine.Debug.Log($"正在加载 Catalog: {fullPath}");

        // 3. 开始异步加载
        var handle = Addressables.LoadContentCatalogAsync(fullPath, false);
        yield return handle;

        // 4. 处理结果
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            IResourceLocator locator = handle.Result;
            loadedCatalogs.Add(path); // 用原始路径作为 key
            UnityEngine.Debug.Log($"✅ Catalog 加载成功！包含 {locator.Keys.Count()} 个资源键。");
            // 加载后，可以在此处触发后续资源加载逻辑
            foreach (var key in locator.Keys)
            {
                string keyStr = key.ToString();
                if (keyStr.Contains("enemy_white") || keyStr.Contains("selfHarm"))
                    Debug.Log($"找到相关 Key: {keyStr}");
            }
        }
        else
        {
            UnityEngine.Debug.LogError($"❌ Catalog 加载失败: {handle.OperationException}");
        }
    }

    private string ResolvePath(string path)
    {
        // 如果以 http:// 或 https:// 开头，视为远程 URL
        if (path.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        // 如果是绝对路径（Windows 或 Unix），直接使用
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        // 否则视为相对 StreamingAssets 的路径
        return System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, path);
    }
}