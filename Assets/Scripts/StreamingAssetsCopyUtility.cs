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

        // 编辑器 / 桌面平台下，热更目录就是 StreamingAssets 本身
        // （见 PathHelper.AppHotfixResPath：仅移动端切换到 persistentDataPath）。
        // 源路径与目标路径相同，复制没有意义，直接标记完成并跳过，
        // 同时避免 StreamingAssets 中没有 filelist.txt 时每次启动都报 404。
        bool sameRoot = string.Equals(
            PathHelper.AppHotfixResPath.Replace('\\', '/').TrimEnd('/'),
            Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/'),
            System.StringComparison.OrdinalIgnoreCase);
        if (sameRoot)
        {
            Debug.Log("Hotfix path equals StreamingAssets (editor/desktop), skip copy.");
            MarkDone();
            yield break;
        }

        Debug.Log("First launch, copying streaming assets to persistent path...");

        // 1. 读取文件清单
        // Android 上 streamingAssetsPath 是 jar:file:// URI，可直接请求；
        // iOS / OSX 需要 file:// 前缀（复用 PathHelper.AppResPath4Web）
        string listUrl = PathHelper.AppResPath4Web.TrimEnd('/') + "/filelist.txt";
        string listContent = null;

        using (UnityWebRequest request = UnityWebRequest.Get(listUrl))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                // 清单缺失不视为致命错误：降级为警告，不标记完成（下次启动自愈重试）。
                // 清单由编辑器菜单 Tools/生成StreamingAssets文件列表 生成，打包前务必执行。
                Debug.LogWarning($"filelist.txt not found ({request.error}), skip copying this launch. " +
                                 "请先执行菜单 Tools/生成StreamingAssets文件列表 后再打包。");
                yield break;
            }
            listContent = request.downloadHandler.text;
        }

        if (string.IsNullOrEmpty(listContent))
        {
            // 空清单是合法状态（无文件需要复制），标记完成
            Debug.LogWarning("filelist.txt is empty, nothing to copy.");
            MarkDone();
            yield break;
        }

        // 2. 解析文件列表
        string[] relativePaths = listContent.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        // 3. 目标根目录
        string targetRoot = PathHelper.AppHotfixResPath;
        if (!Directory.Exists(targetRoot))
            Directory.CreateDirectory(targetRoot);

        string sourceRoot = PathHelper.AppResPath4Web.TrimEnd('/'); // Android 为 jar URI，iOS/OSX 带 file:// 前缀

        // 4. 逐个复制
        foreach (string relPath in relativePaths)
        {
            string rel = relPath.Replace('\\', '/').TrimStart('/');
            string sourceUrl = sourceRoot + "/" + rel;
            string targetPath = Path.Combine(targetRoot, rel.Replace('/', Path.DirectorySeparatorChar));

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
                    Debug.LogWarning($"Failed to copy {rel}: {req.error}");
                    continue; // 跳过此文件，继续下一个
                }

                // 写入目标文件
                File.WriteAllBytes(targetPath, req.downloadHandler.data);
                Debug.Log($"Copied: {rel} -> {targetPath}");
            }
        }

        // 5. 标记已完成
        MarkDone();
        Debug.Log("Copy completed.");
    }

    private static void MarkDone()
    {
        PlayerPrefs.SetInt(FIRST_LAUNCH_KEY, 1);
        PlayerPrefs.Save();
    }
}
