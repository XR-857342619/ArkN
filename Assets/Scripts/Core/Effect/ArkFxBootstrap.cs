// ============================================================
// ArkFxBootstrap - 启动引导：游戏一启动就加载全部特效依赖
// 放进工程后无需手动挂到任何场景对象上（RuntimeInitializeOnLoadMethod 自动生效）
// ============================================================
using System.IO;
using UnityEngine;

public static class ArkFxBootstrap
{
    // ab/ 目录随游戏分发的位置：StreamingAssets/arkfx/
    public static string AbDir =>
        Path.Combine(Application.streamingAssetsPath, "arkfx");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (!Directory.Exists(AbDir))
        {
            Debug.LogWarning($"[ArkFx] 找不到特效目录 {AbDir}，特效功能不可用");
            return;
        }
        ArkFxLoader.LoadAll(AbDir);
    }
}
