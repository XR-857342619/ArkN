using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators; // 引入 IResourceLocator 所需的命名空间[reference:6]

public class SpineResourceManager : MonoBehaviour
{
    public static SpineResourceManager Instance { get; private set; }

    public List<string> AllSpineKeys { get; private set; } = new List<string>();
    public bool IsLoaded { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadAllSpineResources();
    }

    public void LoadAllSpineResources()
    {
        if (IsLoaded) return;

        var keys = new List<string>();

        // 遍历所有已注册的 Resource Locator
        foreach (var locator in Addressables.ResourceLocators)
        {
            // 每个 locator 的 Keys 属性包含了它所管理的所有资源 Key[reference:7]
            foreach (var key in locator.Keys)
            {
                // 尝试将 key 转换为字符串，并检查资源类型是否为 GameObject
                if (key is string keyStr &&
                   keyStr.StartsWith("Assets/Bundles/Units") &&
                   locator.Locate(key, typeof(GameObject), out var locations))
                {
                    // 进一步确保有 GameObject 类型的位置
                    bool hasGameObject = false;
                    foreach (var loc in locations)
                    {
                        if (loc.ResourceType == typeof(GameObject))
                        {
                            hasGameObject = true;
                            break;
                        }
                    }
                    if (hasGameObject && !keys.Contains(keyStr))
                    {
                        keys.Add(keyStr);
                    }
                }
            }
        }

        AllSpineKeys = keys;
        IsLoaded = true;
        Debug.Log($"通过 Locator 获取到 {AllSpineKeys.Count} 个可用 Spine 资源");
        OnListLoaded?.Invoke(AllSpineKeys);
    }

    public event System.Action<List<string>> OnListLoaded;
}