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
    /// 历史问题（2026-10-27 v1.1 起）：
    ///   - "皮肤比地块低 0.3/0.4（Y 轴偏移）"已修：锚点语义显式化，新画笔默认 TileSurface
    ///     （皮肤站在格子可视表面），旧存档按 TileCenter 保持原样，见 Docs/开发者文档/25-地块皮肤笔刷系统.md。
    ///   - 仍待处理：运行时面板的布局偏移（panel 坐标只在 Init 时算一次、无 Relation，见同文档 R2）。
    /// </summary>
    public static bool SkinBrush = true;

    /// <summary>
    /// 【高台深度遮挡】让高台格子（MapGrid.FarAttackGrid == true）参与 Spine 的深度写入，
    /// 使 Spine 单位能被高台地块真实遮挡（不再只是"永远浮在地图最上层"）。
    /// 实现方式：给高台地块生成只写深度的代理网格（high 层），由 SpineOverlayCameraUtil
    /// 的 DeviceDepthCamera 渲染；不改动原有地块的 Layer / Collider / 鼠标拾取射线。
    /// 关闭后高台格子不再遮挡 Spine，恢复"单位永远在最上层"的旧表现。
    /// </summary>
    public static bool HighTileDepthOcclusion = true;
}
