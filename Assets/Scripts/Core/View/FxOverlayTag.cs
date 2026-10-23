using UnityEngine;

/// <summary>
/// 挂在 MapBuilder 手动摆放的特效 prefab 上：Awake 时自动把整棵子树切到 FxOverlay 层，
/// 由 SpineOverlayCamera 一并渲染、与 Spine 按 3D 深度排序（层未创建时自动跳过，特效仍由主相机渲染）。
/// 用法：特效根物体挂本组件即可，无需手动在 Inspector 里改 Layer。
/// </summary>
[DisallowMultipleComponent]
public class FxOverlayTag : MonoBehaviour
{
    void Awake()
    {
        SpineOverlayCameraUtil.ApplyFx(gameObject);
    }
}
