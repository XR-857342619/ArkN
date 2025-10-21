using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using FairyGUI;
using UnityEngine.UI;

public class ExtextureLoader : MonoBehaviour
{
    private static readonly object _lock = new object();
    public static ExtextureLoader Instance
    {
        get
        {
            if (instance == null)
            {
                lock (_lock)  // 加锁防止并发创建
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

    public void LoadLocalTexture(GLoader loader, string localFileName, Action onSuccess = null, Action onFailed = null)
    {
        string key = $"{localFileName}_{loader.GetHashCode()}";  // 唯一标识
        if (loaderDict.ContainsKey(key))
        {
            loaderDict.Remove(key);  // 移除旧引用
        }
        loaderDict.Add(key, loader);
        // 拼出正确的本地路径（不同平台路径规则不一样，这么写通用）
        string localPath;
#if UNITY_ANDROID && !UNITY_EDITOR // 安卓手机上的路径
    localPath = "file://" + Application.streamingAssetsPath + "/" + localFileName;
#else // Windows/Mac/Unity编辑器里的路径
        localPath = Application.streamingAssetsPath + "/" + localFileName;
#endif

        // 调用加载协程（下面会写）
        Debug.Log("加载本地资源: " + localPath);
        StartCoroutine(LoadTextureFromPath(loader, localPath, key, onSuccess, onFailed));
    }

    /// <summary>
    /// 通用加载协程（从路径/网址加载图片，给Loader显示）
    /// </summary>
    private IEnumerator LoadTextureFromPath(GLoader imgLoader, string pathOrUrl, string key, Action onSuccess, Action onFailed)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(pathOrUrl))
        {
            yield return webRequest.SendWebRequest();

            // 检查 loader 是否已被销毁或移除
            if (!loaderDict.TryGetValue(key, out var currentLoader) || currentLoader != imgLoader)
            {
                Debug.LogWarning("加载目标已失效，终止处理");
                yield break;  // 加载目标已失效，终止处理
            }
            loaderDict.Remove(key);  // 清理引用

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                SetTexture(imgLoader, pathOrUrl);
                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"加载失败: {webRequest.error}，路径: {pathOrUrl}");
                onFailed?.Invoke();  // 通知外部处理失败
                                     // imgLoader.texture = 错误默认图;  // 建议设置默认图
            }
        }
    }

    private IEnumerator SetTexture(GLoader imgLoader, string pathOrUrl)
    {
        // 第一步：显示“加载中”（用之前设的默认图，或者手动设一张）
        //imgLoader.icon = UIConfig.defaultLoadingIcon; // FairyGUI自带的加载图标

        // 第二步：用Unity的工具加载图片（本地/网络都能用）
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(pathOrUrl))
        {
            yield return webRequest.SendWebRequest(); // 等待加载完成

            // 第三步：判断加载成功还是失败
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Log.Debug("加载成功: " + pathOrUrl);
                // 加载成功：把Unity的纹理转换成FairyGUI能识别的格式
                Texture2D unityTexture = DownloadHandlerTexture.GetContent(webRequest);
                if (unityTexture != null)
                {
                    // 在场景中创建一个RawImage用于预览（仅调试用）
                    GameObject debugImage = new GameObject("DebugTexturePreview");
                    RawImage rawImage = debugImage.AddComponent<RawImage>();
                    rawImage.texture = unityTexture;
                    rawImage.rectTransform.sizeDelta = new Vector2(200, 200); // 固定大小
                    debugImage.transform.SetParent(Camera.main.transform, false); // 显示在相机前
                    debugImage.transform.localPosition = new Vector3(0, 0, 5);
                }
                NTexture fairyTexture = new NTexture(unityTexture); // 转格式

                // 把转换好的纹理给Loader显示
                imgLoader.texture = fairyTexture;

                // （可选）让Loader适应图片大小（不然图片可能被拉伸）
                //imgLoader.SetSize(unityTexture.width, unityTexture.height);
            }
            else
            {
                // 加载失败：显示错误提示（比如一张“加载失败”的图）
                Log.Error("加载图片失败：" + webRequest.error);
                //imgLoader.icon = UIConfig.defaultErrorIcon; // FairyGUI自带的错误图标
            }
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();  // 停止所有未完成的加载协程
        loaderDict.Clear();
    }
}
