#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public static class BuildFileList
{
    [MenuItem("Tools/生成StreamingAssets文件列表")]
    public static void Generate()
    {
        // 统一转换为正斜杠绝对路径，避免 Windows 反斜杠导致 Replace 不生效
        string streamingPath = Path.GetFullPath(Application.dataPath + "/StreamingAssets").Replace('\\', '/');

        string[] allFiles = Directory.GetFiles(streamingPath, "*", SearchOption.AllDirectories)
                                     .Where(p => !p.EndsWith("filelist.txt") && !p.EndsWith(".meta")) // 排除清单本身和.meta文件
                                     .Select(p =>
                                     {
                                         string full = Path.GetFullPath(p).Replace('\\', '/');
                                         // 移除 StreamingAssets 根路径前缀，得到相对路径
                                         return full.Substring(streamingPath.Length + 1);
                                     })
                                     .ToArray();

        string listPath = streamingPath + "/filelist.txt";
        File.WriteAllLines(listPath, allFiles);
        AssetDatabase.Refresh();
        Debug.Log($"File list generated at {listPath}, found {allFiles.Length} files.");
    }
}
#endif