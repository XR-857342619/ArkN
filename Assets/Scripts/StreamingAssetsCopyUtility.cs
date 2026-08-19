using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Collections.Generic;

public static class StreamingAssetsCopyUtility
{
    private const string FIRST_LAUNCH_KEY = "FirstLaunchCopyDone";

    /// <summary>
    /// 检查是否首次启动，如果是则开始复制。
    /// 需在 MonoBehaviour 中通过 StartCoroutine 调用。
    /// </summary>
    public static IEnumerator CopyOnFirstLaunch()
    {
        // 检查是否已经复制过
        if (PlayerPrefs.GetInt(FIRST_LAUNCH_KEY, 0) == 1)
        {
            Debug.Log("Already copied streaming assets, skip.");
            yield break;
        }

        Debug.Log("First launch, copying streaming assets to persistent path...");

        // 1. 读取文件清单
        string listUrl = Path.Combine(Application.streamingAssetsPath, "filelist.txt");
        string listContent = null;

        using (UnityWebRequest request = UnityWebRequest.Get(listUrl))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load filelist.txt: {request.error}");
                yield break;
            }
            listContent = request.downloadHandler.text;
        }

        if (string.IsNullOrEmpty(listContent))
        {
            Debug.LogError("filelist.txt is empty or not found.");
            yield break;
        }

        // 2. 解析文件列表
        string[] relativePaths = listContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        // 3. 目标根目录
        string targetRoot = PathHelper.AppHotfixResPath;
        if (!Directory.Exists(targetRoot))
            Directory.CreateDirectory(targetRoot);

        // 4. 逐个复制
        foreach (string relPath in relativePaths)
        {
            string sourceUrl = Path.Combine(Application.streamingAssetsPath, relPath).Replace("\\", "/");
            string targetPath = Path.Combine(targetRoot, relPath);

            // 创建目标文件的目录
            string targetDir = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // 下载源文件（UnityWebRequest）
            using (UnityWebRequest req = UnityWebRequest.Get(sourceUrl))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to copy {relPath}: {req.error}");
                    continue; // 跳过此文件，继续下一个
                }

                // 写入目标文件
                File.WriteAllBytes(targetPath, req.downloadHandler.data);
                Debug.Log($"Copied: {relPath} -> {targetPath}");
            }
        }

        // 5. 标记已完成
        PlayerPrefs.SetInt(FIRST_LAUNCH_KEY, 1);
        PlayerPrefs.Save();
        Debug.Log("Copy completed.");
    }
}