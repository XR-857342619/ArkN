using System;
using UnityEngine;
using System.IO;

public static class PathHelper
{     /// <summary>
      ///应用程序外部资源路径存放路径(热更新资源路径)
      /// </summary>
    public static string AppHotfixResPath
    {
        get
        {
            string game = Application.productName;
            string path = AppResPath;
            if (Application.isMobilePlatform)
            {
                path = $"{Application.persistentDataPath}/{game}/";
            }
            return path;
        }

    }

    /// <summary>
    /// 应用程序内部资源路径存放路径
    /// </summary>
    public static string AppResPath
    {
        get
        {
            return Application.streamingAssetsPath;
        }
    }

    public static string MapResPath
    {
        get
        {
#if UNITY_EDITOR
                return Application.dataPath+"/Bundles/Data";
#else
            return AppHotfixResPath;
#endif
        }
    }
    public static string ExcelResPath
    {
        get
        {
#if UNITY_EDITOR
            return Path.GetDirectoryName(Application.dataPath);
#else
            return AppHotfixResPath;
#endif
        }
    }
    /// <summary>
    /// 应用程序内部资源路径存放路径(www/webrequest专用)
    /// </summary>
    public static string AppResPath4Web
    {
        get
        {
#if UNITY_IOS || UNITY_STANDALONE_OSX
                return $"file://{Application.streamingAssetsPath}";
#else
            return Application.streamingAssetsPath;
#endif

        }
    }

    /// <summary>
    /// 将资源路径规范化为绝对路径。
    /// 兼容 Android 上 data.sav 中可能缺少 /storage/emulated/0 前缀的情况。
    /// </summary>
    public static string NormalizeAppPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        string result = path.Replace('\\', '/');

#if UNITY_ANDROID
        // Android 上常见缺少 /storage/emulated/0 前缀的路径（如 Android/data/... 或 data/...）
        if (!result.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase)
            && (result.StartsWith("Android/", StringComparison.OrdinalIgnoreCase)
                || result.StartsWith("data/", StringComparison.OrdinalIgnoreCase)))
        {
            result = "/storage/emulated/0/" + result;
        }
#endif

        if (!Path.IsPathRooted(result))
        {
            result = Path.Combine(AppHotfixResPath, result);
        }

        try
        {
            result = Path.GetFullPath(result);
        }
        catch
        {
            // 保留原路径
        }

        return result.Replace('\\', '/');
    }

    public static string DataPath = "Assets/Bundles/Data/";
    public static string UIPath = "Assets/Bundles/UI/";
    public static string SpritePath = "Assets/Bundles/Image/";
    public static string StandPicPath = "Assets/Bundles/Image/StandPic/";
    public static string OtherPath = "Assets/Bundles/Other/";
    public static string UnitPath = "Assets/Bundles/Units/";
    public static string BulletPath = "Assets/Bundles/Bullet/";
    public static string EffectPath = "Assets/Bundles/Effect/";
    public static string AudioPath = "Assets/Bundles/Audio/";
    public static string DungeonGridPath = "Assets/Bundles/DungeonTile/";
    //public static string TimelinePath = "Bundles/Effect/Timeline/";
}
