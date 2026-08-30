using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ExtraCatalogLoader : MonoBehaviour
{
    [Tooltip("相对于 Addressables.RuntimePath 的附加 catalog 文件名")]
    public string extraCatalogFileName = "extra_catalog.json";

    [Tooltip("可选：加载附加 catalog 后自动测试加载的一个 branch1 独有 address")]
    public string testAddress = "";

    IEnumerator Start()
    {
        // 等待主 Addressables 初始化完成
        yield return Addressables.InitializeAsync();

        var catalogPath = Addressables.RuntimePath + "/" + extraCatalogFileName;
        var handle = Addressables.LoadContentCatalogAsync(catalogPath, true);
        yield return handle;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("[ExtraCatalog] load failed: " + handle.OperationException);
            yield break;
        }

        Debug.Log("[ExtraCatalog] loaded: " + catalogPath);

        if (string.IsNullOrEmpty(testAddress))
            yield break;

        var load = Addressables.LoadAssetAsync<Object>(testAddress);
        yield return load;

        if (load.Status == AsyncOperationStatus.Succeeded)
            Debug.Log("[ExtraCatalog] test load ok: " + load.Result.name);
        else
            Debug.LogError("[ExtraCatalog] test load failed: " + load.OperationException);
    }
}
