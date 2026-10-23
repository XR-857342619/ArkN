/// <summary>
/// 全局功能开关（集中管理"默认关闭"的实验性功能）。
/// 想启用/停用某个功能，只改这里的一个字段即可，不需要去翻各个调用点。
/// 注意：这些字段是运行时静态字段，改完重新编译即生效；不参与存档。
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// 【皮肤笔刷】造图界面里的换皮/叠加皮肤功能，以及进战斗时按存档还原皮肤。
    /// 默认关闭：
    ///   - 关闭时不会安装笔刷面板（按 T 不再出现），不会采集/写入 GridInfo.Skin / SkinExtras；
    ///   - 关闭时 MapManager.ApplySkins 直接返回，不会按存档重建皮肤；
    ///   - 存档里的 Skin 字段仍会被原样保留（不丢失），重新打开后即可继续使用。
    /// 目前已知问题：笔刷面板的 UI 布局与刷点位置存在偏移，待修复后再打开。
    /// </summary>
    public static bool SkinBrush = false;

    /// <summary>
    /// 【高台深度遮挡】让高台格子（MapGrid.FarAttackGrid == true）参与 Spine 的深度写入，
    /// 使 Spine 单位能被高台地块真实遮挡（不再只是"永远浮在地图最上层"）。
    /// 实现方式：给高台地块生成只写深度的代理网格（high 层），由 SpineOverlayCameraUtil
    /// 的 DeviceDepthCamera 渲染；不改动原有地块的 Layer / Collider / 鼠标拾取射线。
    /// 关闭后高台格子不再遮挡 Spine，恢复"单位永远在最上层"的旧表现。
    /// </summary>
    public static bool HighTileDepthOcclusion = true;
}
