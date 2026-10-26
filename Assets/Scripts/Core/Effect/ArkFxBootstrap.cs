// ============================================================
// ArkFxBootstrap - 启动引导：游戏一启动就加载全部特效依赖
// 放进工程后无需手动挂到任何场景对象上（RuntimeInitializeOnLoadMethod 自动生效）
// ============================================================
using System.IO;
using UnityEngine;

public static class ArkFxBootstrap
{
    // ab/ 目录随游戏分发的位置：StreamingAssets/arkfx/
    /// <summary>同 ArkFxLoader.AbDir：移动端指向 persistentDataPath 下的拷贝。</summary>
    public static string AbDir =>
        Path.Combine(PathHelper.AppHotfixResPath, "arkfx");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (!Directory.Exists(AbDir))
        {
            // 移动端首次启动时 StreamingAssets 还在拷贝中（见 Init.cs 2.5/2.6），属正常情况
            if (Application.isMobilePlatform)
                Debug.Log($"[ArkFx] 特效目录尚未就绪（等待 StreamingAssets 拷贝）: {AbDir}");
            else
                Debug.LogWarning($"[ArkFx] 找不到特效目录 {AbDir}，特效功能不可用");
            return;
        }
        ArkFxLoader.LoadAll(AbDir);
    }
}
