using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spine 三相机叠加渲染方案：
///   1. 主相机（Camera.main，挂在 MapBuilder 下）：渲染常规 3D 世界（含特效以外的部分），
///      仅剔除 Spine3D 与 FxOverlay 层；
///   2. DeviceDepthCamera（Depth = 主相机+0.25，Clear = Depth Only）：只渲染 Device 层，
///      并以 SetReplacementShader(DeviceDepthOnly) 只写深度不写颜色——为 Spine 提供"装置深度"；
///   3. SpineOverlayCamera（Depth = 主相机+0.5，Clear = Nothing）：渲染 Spine3D 层 + FxOverlay 层。
///      深度缓冲此时只含"装置深度"（世界深度已被清除）→
///      Spine/特效 与装置重合时正常深度遮挡，而相对其他 3D 模型永远优先显示；
///      特效与 Spine 在同一相机内按真实 3D 深度排序，特效不再被 Spine 完全盖住。
///      注意深度值必须低于 FairyGUI StageCamera（源码写死 depth=1），否则 Spine 会盖住暂停等 UI。
///
/// 使用：
///   1. 编辑器菜单 Tools/Spine引用修复/0-创建 Spine3D/Device/FxOverlay 渲染层（一次性）；
///   2. Spine 模型实例化后调用 SpineOverlayCameraUtil.Apply(gameObject)（SpineModel.Awake 已内置）；
///   3. 中立单位（装置）实例化后调用 SpineOverlayCameraUtil.ApplyDevice(gameObject)
///      （Unit.CreateModel 与 UI_MidPage.UpdatePoints 已内置）；
///   4. 特效实例化后调用 SpineOverlayCameraUtil.ApplyFx(gameObject)（Effect.Awake 已内置）。
/// </summary>
public static class SpineOverlayCameraUtil
{
    public const string LayerName = "Spine3D";
    public const string DeviceLayerName = "Device";
    public const string FxLayerName = "FxOverlay";
    /// <summary>
    /// 高台地块「深度代理」所在层（复用工程里一直空着的 high 层）。
    /// 该层被主相机剔除（不参与着色），只被 DeviceDepthCamera 用替换着色器渲染
    /// —— 只写深度不写颜色，因此高台地块能遮挡 Spine，却不影响物理与鼠标拾取。
    /// 详见 SpineDepthOccluderUtil。
    /// </summary>
    public const string OccluderLayerName = "high";
    const string SpineCamName = "SpineOverlayCamera";
    const string DeviceCamName = "DeviceDepthCamera";
    const string DepthShaderName = "Custom/DeviceDepthOnly";

    static Camera _spineCam;
    static Camera _deviceCam;
    static bool _warned;
    static bool _warnedDepthShader;
    static bool _configLogged;

    /// <summary>Spine 单位：整体切到 Spine3D 层（名为 "Shadow" 的地面阴影子物体除外）。</summary>
    public static void Apply(GameObject spineRoot)
    {
        if (spineRoot == null) return;
        if (!EnsureLayers()) return;
        EnsureCameras();
        SetLayerRecursive(spineRoot.transform, LayerMask.NameToLayer(LayerName), "Shadow");
    }

    /// <summary>中立单位（装置）：整体切到 Device 层（名为 "Shadow" 的地面阴影子物体除外）。</summary>
    public static void ApplyDevice(GameObject deviceRoot)
    {
        if (deviceRoot == null) return;
        if (!EnsureLayers()) return;
        EnsureCameras();
        SetLayerRecursive(deviceRoot.transform, LayerMask.NameToLayer(DeviceLayerName), "Shadow");
    }

    /// <summary>特效：整体切到 FxOverlay 层，由 SpineOverlayCamera 一并渲染、与 Spine 按深度排序。
    /// FxOverlay 层未创建时静默跳过（特效仍由主相机渲染，行为同旧版）。</summary>
    public static void ApplyFx(GameObject fxRoot)
    {
        if (fxRoot == null) return;
        int fxLayer = LayerMask.NameToLayer(FxLayerName);
        if (fxLayer == -1) return;
        EnsureCameras();
        SetLayerRecursive(fxRoot.transform, fxLayer, null);
    }

    /// <summary>强制重算相机配置（新增/移除层或深度代理后调用，用于立即刷新 cullingMask）。</summary>
    public static void Refresh()
    {
        EnsureCameras();
    }

    static bool EnsureLayers()
    {
        bool ok = LayerMask.NameToLayer(LayerName) != -1 && LayerMask.NameToLayer(DeviceLayerName) != -1;
        if (!ok && !_warned)
        {
            _warned = true;
            Debug.LogWarning($"[SpineOverlayCameraUtil] 缺少渲染层「{LayerName}」或「{DeviceLayerName}」，请先执行编辑器菜单 Tools/Spine引用修复/0-创建 Spine3D/Device 渲染层。本次仍按原层渲染。");
        }
        return ok;
    }

    static void SetLayerRecursive(Transform t, int layer, string exceptName)
    {
        if (t.name != exceptName) t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer, exceptName);
    }

    static void EnsureCameras()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            // 相机尚未激活：销毁缓存的叠加相机，等下一次 Apply 再建
            if (_spineCam != null) { Object.Destroy(_spineCam.gameObject); _spineCam = null; }
            if (_deviceCam != null) { Object.Destroy(_deviceCam.gameObject); _deviceCam = null; }
            return;
        }

        int spineLayer = LayerMask.NameToLayer(LayerName);
        int deviceLayer = LayerMask.NameToLayer(DeviceLayerName);
        int fxLayer = LayerMask.NameToLayer(FxLayerName); // 可能为 -1（层未创建）：特效仍走主相机
        int occluderLayer = LayerMask.NameToLayer(OccluderLayerName); // 可能为 -1（高台深度代理层缺失时不影响其它功能）
        Shader depthShader = Shader.Find(DepthShaderName);

        // ---- DeviceDepthCamera：只写装置深度，不写颜色 ----
        if (_deviceCam == null)
        {
            Transform old = main.transform.Find(DeviceCamName);
            _deviceCam = old != null ? old.GetComponent<Camera>() : CreateChildCamera(main, DeviceCamName);
        }
        if (_deviceCam != null)
        {
            if (depthShader == null)
            {
                // 【重要】拿不到替换着色器时绝不能让它继续渲染！
                // 没有替换着色器 = 这台相机会用物体自己的彩色材质再画一遍，而它的深度缓冲刚被
                // Clear = Depth 清空（里面没有地形）→ 高台/装置会整块盖到画面上，埋在地下的部分
                // 也不再被地面遮挡。典型触发条件：打包时 shader 被剥离，Shader.Find 返回 null。
                // 正确做法是直接停用，退回“Spine 永远在最上层”的旧表现。
                _deviceCam.cullingMask = 0;
                _deviceCam.enabled = false;
                if (!_warnedDepthShader)
                {
                    _warnedDepthShader = true;
                    Debug.LogError($"[SpineOverlayCameraUtil] 找不到着色器 {DepthShaderName}（多半是打包时被剥离）。" +
                                   "请加入 Project Settings → Graphics → Always Included Shaders。" +
                                   "已停用 DeviceDepthCamera，退回旧渲染，不会出现地形重影。");
                }
            }
            else
            {
                _deviceCam.enabled = true;
                _deviceCam.cullingMask = 1 << deviceLayer;
                if (occluderLayer != -1) _deviceCam.cullingMask |= 1 << occluderLayer; // 高台深度代理：只写深度
                _deviceCam.clearFlags = CameraClearFlags.Depth;
                // 必须低于 FairyGUI StageCamera（depth=1），否则 UI 之后仍会被本方案相机覆盖
                _deviceCam.depth = main.depth + 0.25f;
                _deviceCam.SetReplacementShader(depthShader, "RenderType");
                EnsureSync(_deviceCam.gameObject);
            }
        }

        // ---- SpineOverlayCamera：Clear = Nothing（保留装置深度），渲染 Spine + 特效 ----
        if (_spineCam == null)
        {
            Transform old = main.transform.Find(SpineCamName);
            _spineCam = old != null ? old.GetComponent<Camera>() : CreateChildCamera(main, SpineCamName);
        }
        if (_spineCam != null)
        {
            _spineCam.cullingMask = fxLayer != -1 ? (1 << spineLayer) | (1 << fxLayer) : (1 << spineLayer);
            // 正常模式：Clear = Nothing，复用 DeviceDepthCamera 写的装置/高台深度（画面颜色也保留）。
            // 降级模式（替换着色器缺失、装置深度相机已停用）：必须自己清深度，否则会继承主相机
            // 的完整世界深度，单位会被脚下地板/前景地形切脚 —— 等于退回“永远在最上层”的旧表现。
            _spineCam.clearFlags = depthShader != null ? CameraClearFlags.Nothing : CameraClearFlags.Depth;
            _spineCam.depth = main.depth + 0.5f; // 同样必须低于 FairyGUI StageCamera（depth=1）
            EnsureSync(_spineCam.gameObject);
        }

        // 主相机：剔除 Spine 层与 FxOverlay 层（避免双重绘制）；确保 Device 层仍在主相机渲染
        if ((main.cullingMask & (1 << spineLayer)) != 0)
            main.cullingMask &= ~(1 << spineLayer);
        if (fxLayer != -1 && (main.cullingMask & (1 << fxLayer)) != 0)
            main.cullingMask &= ~(1 << fxLayer);
        if (occluderLayer != -1 && (main.cullingMask & (1 << occluderLayer)) != 0)
            main.cullingMask &= ~(1 << occluderLayer); // 高台代理只走深度通道，主相机不着色
        if ((main.cullingMask & (1 << deviceLayer)) == 0)
            main.cullingMask |= 1 << deviceLayer;

        if (!_configLogged)
        {
            _configLogged = true;
            Debug.Log($"[SpineOverlayCameraUtil] 配置就绪：main.depth={main.depth}, DeviceDepthCamera=({_deviceCam != null}, depth={(_deviceCam ? _deviceCam.depth : -99)}, mask={(_deviceCam ? _deviceCam.cullingMask : 0)}), " +
                      $"SpineOverlayCamera=({_spineCam != null}, depth={(_spineCam ? _spineCam.depth : -99)}, mask={(_spineCam ? _spineCam.cullingMask : 0)}, clear={(_spineCam ? _spineCam.clearFlags : (CameraClearFlags)(-1))}), " +
                      $"替换着色器={(depthShader != null ? "OK" : "缺失")}, Fx层={(fxLayer != -1 ? "已接入" : "未创建(特效走主相机)")}, " +
                      $"主相机mask含Device={((main.cullingMask & (1 << deviceLayer)) != 0)}, 含Spine3D={((main.cullingMask & (1 << spineLayer)) != 0)}, 含FxOverlay={(fxLayer != -1 && (main.cullingMask & (1 << fxLayer)) != 0)}");
        }
    }

    static Camera CreateChildCamera(Camera main, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(main.transform, false);
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main); // 继承投影参数
        var listener = cam.GetComponent<AudioListener>();
        if (listener != null) Object.Destroy(listener);
        return cam;
    }

    static void EnsureSync(GameObject go)
    {
        if (go.GetComponent<SpineOverlaySync>() == null)
            go.AddComponent<SpineOverlaySync>();
    }
}

/// <summary>每帧同步主相机的位置/旋转/投影参数（主相机由 MapBuilder 拖动、缩放时叠加相机保持一致）。</summary>
public class SpineOverlaySync : MonoBehaviour
{
    Camera _main;
    Camera _self;

    void LateUpdate()
    {
        if (_self == null) _self = GetComponent<Camera>();
        if (_main == null || !_main.isActiveAndEnabled)
        {
            _main = Camera.main;
            if (_main == null) return;
        }

        Transform t = _main.transform;
        if (transform.position != t.position) transform.position = t.position;
        if (transform.rotation != t.rotation) transform.rotation = t.rotation;
        if (_self.orthographic != _main.orthographic) _self.orthographic = _main.orthographic;
        if (_self.orthographic) _self.orthographicSize = _main.orthographicSize;
        else if (!Mathf.Approximately(_self.fieldOfView, _main.fieldOfView)) _self.fieldOfView = _main.fieldOfView;
        if (_self.nearClipPlane != _main.nearClipPlane) _self.nearClipPlane = _main.nearClipPlane;
        if (_self.farClipPlane != _main.farClipPlane) _self.farClipPlane = _main.farClipPlane;
    }
}
