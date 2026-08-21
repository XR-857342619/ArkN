using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 寻路流程 Debug 工具。
/// 用法：
/// 1. 运行时在 Inspector 中通过 PathDebugger.Enabled 开关；
/// 2. 在 Scene 视图中查看 DrawPath / DrawPoint 绘制的路径；
/// 3. 通过 Log / LogPath 输出路径点到 Console。
/// </summary>
public static class PathDebugger
{
    /// <summary>总开关。关闭后所有 Debug 绘制与日志都不执行。</summary>
    public static bool Enabled = true;

    /// <summary>是否输出路径点日志。日志较多，建议按需打开。</summary>
    public static bool LogEnabled = true;

    /// <summary>是否使用 Gizmos 绘制（Scene 视图可见）。</summary>
    public static bool DrawEnabled = true;

    /// <summary>在 Scene 视图绘制一条折线路径。</summary>
    public static void DrawPath(List<Vector3> path, Color color, float duration = 0f)
    {
        if (!Enabled || !DrawEnabled || path == null || path.Count < 2) return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Debug.DrawLine(path[i] + Vector3.up * 0.05f, path[i + 1] + Vector3.up * 0.05f, color, duration);
        }
    }

    /// <summary>在 Scene 视图绘制一个点（十字线）。</summary>
    public static void DrawPoint(Vector3 position, Color color, float size = 0.2f, float duration = 0f)
    {
        if (!Enabled || !DrawEnabled) return;

        Vector3 p = position + Vector3.up * 0.05f;
        Debug.DrawLine(p - Vector3.right * size, p + Vector3.right * size, color, duration);
        Debug.DrawLine(p - Vector3.forward * size, p + Vector3.forward * size, color, duration);
    }

    /// <summary>输出一条带标题的日志。</summary>
    public static void Log(string title, string message)
    {
        if (!Enabled || !LogEnabled) return;
        Debug.Log($"[PathDebugger] {title}: {message}");
    }

    /// <summary>输出完整路径点日志。</summary>
    public static void LogPath(string title, string section, List<Vector3> path)
    {
        if (!Enabled || !LogEnabled || path == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (Vector3 p in path)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(p.ToString());
        }
        Debug.Log($"[PathDebugger] {title} - {section}: [{sb}]");
    }
}
