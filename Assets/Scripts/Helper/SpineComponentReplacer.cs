using UnityEngine;
using Spine.Unity;
using System.Collections;
using System.IO;
using System.Collections.Generic;

public class SpineComponentReplacer : MonoBehaviour
{
    // 预加载资源列表
    [System.Serializable]
    public class SpineResource
    {
        public string resourceKey;
        public string texturePath;
        public string atlasPath;
        public string skeletonPath;
    }

    [Header("预加载资源列表")]
    public List<SpineResource> preloadResources = new List<SpineResource>();

    // 预加载完成标记
    public bool IsPreloadComplete { get; private set; } = false;

    private void Start()
    {
        // 游戏开始时预加载所有资源
        StartCoroutine(PreloadAllSpineResources());
    }

    /// <summary>
    /// 预加载所有配置的Spine资源
    /// </summary>
    public IEnumerator PreloadAllSpineResources()
    {
        IsPreloadComplete = false;
        int loadedCount = 0;

        foreach (var resource in preloadResources)
        {
            // 检查是否已加载
            if (SpineImportHelper.Instance.loadedSkeletons.ContainsKey(resource.resourceKey))
            {
                loadedCount++;
                Debug.Log($"资源 {resource.resourceKey} 已在缓存中，跳过加载");
                continue;
            }

            // 检查文件完整性
            if (!File.Exists(resource.texturePath) ||
                !File.Exists(resource.atlasPath) ||
                !File.Exists(resource.skeletonPath))
            {
                Debug.LogError($"预加载失败: 资源文件不完整 - {resource.resourceKey}");
                continue;
            }

            // 加载资源
            yield return StartCoroutine(LoadExternalSpineData(
                resource.resourceKey,
                resource.texturePath,
                resource.atlasPath,
                resource.skeletonPath,
                data =>
                {
                    if (data != null)
                    {
                        loadedCount++;
                        Debug.Log($"预加载完成: {resource.resourceKey} ({loadedCount}/{preloadResources.Count})");
                    }
                }
            ));
        }

        IsPreloadComplete = true;
        Debug.Log($"所有Spine资源预加载完成，成功加载 {loadedCount}/{preloadResources.Count} 个资源");
    }

    /// <summary>
    /// 实例化预制件并替换其中的Spine动画组件（使用预加载的资源）
    /// </summary>
    public IEnumerator ReplaceSpineInPrefab(
        GameObject prefab,
        string resourceKey,
        bool isFront = true,
        bool isBack = false,
        Transform parent = null,
        System.Action<GameObject> onComplete = null)
    {
        // 等待预加载完成
        if (!IsPreloadComplete)
        {
            Debug.Log($"等待资源预加载完成... 正在加载: {resourceKey}");
            yield return new WaitUntil(() => IsPreloadComplete);
        }

        // 从缓存获取资源
        if (!SpineImportHelper.Instance.loadedSkeletons.TryGetValue(resourceKey, out var externalData))
        {
            Debug.LogError($"缓存中找不到资源: {resourceKey}，无法替换失败失败");
            onComplete?.Invoke(null);
            yield break;
        }

        // 实例化预制件
        GameObject instance = Instantiate(prefab, parent);
        instance.name = $"{prefab.name}_{resourceKey}";

        // 替换Spine组件
        ReplaceSkeletonComponents(instance, externalData, isFront, isBack);

        // 完成回调
        onComplete?.Invoke(instance);
    }

    /// <summary>
    /// 替换现有游戏对象中的Spine组件
    /// </summary>
    public void ReplaceSkeletonComponents(GameObject targetObject, SkeletonDataAsset newData,
                                         bool replaceFront = true, bool replaceBack = false)
    {
        // 保留原始缩放值
        float originalScale = newData.scale;

        // 遍历目标对象的子节点
        for (int i = 0; i < targetObject.transform.childCount; i++)
        {
            Transform child = targetObject.transform.GetChild(i);
            SkeletonAnimation sa = child.GetComponent<SkeletonAnimation>();

            if (sa == null) continue;

            // 根据子节点索引判断是否为正面/背面
            bool isFrontChild = (i == 1);
            bool isBackChild = (i == 2);

            // 只替换指定的动画组件
            if ((isFrontChild && replaceFront) || (isBackChild && replaceBack))
            {
                // 保存原始组件的关键属性
                bool wasLooping = sa.loop;
                int sortingOrder = sa.GetComponent<Renderer>().sortingOrder;
                float timeScale = sa.timeScale;
                string currentAnim = sa.state.GetCurrent(0)?.Animation?.Name;

                // 从缓存替换骨骼数据
                sa.skeletonDataAsset = newData;
                sa.Initialize(true);

                // 恢复原始属性
                sa.loop = wasLooping;
                sa.timeScale = timeScale;
                sa.GetComponent<Renderer>().sortingOrder = sortingOrder;

                // 恢复播放状态
                if (!string.IsNullOrEmpty(currentAnim) && sa.skeleton.Data.FindAnimation(currentAnim) != null)
                {
                    sa.state.SetAnimation(0, currentAnim, wasLooping);
                }

                Debug.Log($"已替换子节点 {child.name} 的Spine动画资源: {newData.name}");
            }
        }
    }

    /// <summary>
    /// 加载外部Spine资源的协程
    /// </summary>
    private IEnumerator LoadExternalSpineData(string key, string texturePath,
                                             string atlasPath, string skeletonPath,
                                             System.Action<SkeletonDataAsset> onLoaded)
    {
        // 使用SpineImportHelper加载资源
        SkeletonDataAsset data = SpineImportHelper.Instance.LoadSpineAssets(
            key, texturePath, atlasPath, skeletonPath);

        // 等待一帧确保资源初始化完成
        yield return null;

        onLoaded?.Invoke(data);
    }

    /// <summary>
    /// 手动触发资源预加载（可在需要时调用）
    /// </summary>
    public void TriggerPreload()
    {
        if (!IsPreloadComplete)
        {
            StartCoroutine(PreloadAllSpineResources());
        }
        else
        {
            Debug.Log("所有资源已预加载完成");
        }
    }
}
