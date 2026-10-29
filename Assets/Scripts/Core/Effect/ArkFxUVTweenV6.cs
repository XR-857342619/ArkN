// ============================================================
// ArkFxUVTweenV6 - Torappu.Fx.FxUVTweener 的运行时替身（v6，IDA 实证版）
//
// 依据：GameAssembly_old.dll 反编译 Torappu.Fx.FxUVTweener 的
//       Awake / OnEnable / Update，见 Docs/开发者文档/arkfx_re/
//
// 原版行为（逐帧）：
//   OnEnable:  m_v2        = keepInitOffset ? material.mainTextureOffset : Vector2.zero
//              m_secondST  = keepInitOffset ? material.GetVector(secondMapName + "_ST") : 0
//   Update:    m_v2.x += Time.deltaTime * xspeed * 2f          // ← 系数 2 是汇编实锤
//              m_v2.y += Time.deltaTime * yspeed * 2f
//              if (protectMainUV) m_v2 -= floor(m_v2)          // 包裹到 [0,1)
//              material.mainTextureOffset = m_v2
//              m_secondST.z += dt * secondXSpeed * 2f
//              m_secondST.w += dt * secondYSpeed * 2f
//              if (protectSecondUV) m_secondST.zw -= floor(...)
//              material.SetVector(secondMapName + "_ST", m_secondST)
//
// 与 v5 的关键差异（v5 的 _UVTween 理论已被推翻，不要再参考）：
//   1. 速度系数是 2（v5 是 1）
//   2. 主贴图走 mainTextureOffset，不是自定义 _UVTween 向量
//   3. protectMainUV 的语义是"取小数包裹"，不是"优先向量通道"
//   4. 第二贴图写 <贴图名>_ST 向量
//   5. useSharedMaterial 在原版走 MaterialManager 的共享实例缓存；
//      这里一律用 renderer.material（实例），避免污染 AB 里的共享材质
// ============================================================
using UnityEngine;

public class ArkFxUVTweenV6 : ArkFxBehaviour
{
    [Header("主贴图（_MainTex）")]
    [Tooltip("原版 protectMainUV：每帧把累加偏移取小数，包裹到 [0,1)")]
    public bool protectMainUV;
    [Tooltip("每秒 UV 偏移量（原版内部会再乘 2）")]
    public Vector2 mainSpeed;

    [Header("第二贴图")]
    public bool useSecondMap;
    [Tooltip("贴图属性名，向量属性名为 <该名>_ST")]
    public string secondMapName = "_DisturTex";
    public bool protectSecondUV;
    public Vector2 secondSpeed;

    [Header("初始化")]
    [Tooltip("开启时以材质当前的 ST 为起点；关闭时从 0 开始")]
    public bool keepInitOffset = true;
    [Tooltip("保留字段（原版走 MaterialManager 共享材质；本实现统一用实例材质）")]
    public bool useSharedMaterial;

    const float SpeedScale = 2f;   // 原版汇编：mulss + addss xmm0,xmm0

    Renderer _renderer;
    Material _mat;
    Vector2 _mainOffset;
    Vector4 _secondST;             // x,y = scale ; z,w = offset
    string _secondSTProp;

    // 对象池复用时的“初始材质状态”快照。
    // 只在实例第一次 Awake 时采集；之后 OnEnable / BindMaterial 都从快照重新起算，
    // 避免把上一轮结束时的 offset / ST 当成新初值，造成每复用一次就漂移。
    Vector2 _initialMainOffset;
    Vector4 _initialSecondST = new Vector4(1f, 1f, 0f, 0f);
    bool _capturedInitial;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        CaptureInitialMaterial();
    }

    void CaptureInitialMaterial()
    {
        if (_capturedInitial) return;
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;

        var mat = _renderer.material;   // 实例材质：克隆时就是 prefab 的初始值
        if (mat == null) return;

        _initialMainOffset = mat.mainTextureOffset;
        if (useSecondMap && !string.IsNullOrEmpty(secondMapName))
        {
            string prop = secondMapName + "_ST";
            _initialSecondST = mat.HasProperty(prop)
                ? (Vector4)mat.GetVector(prop)
                : new Vector4(1f, 1f, 0f, 0f);
        }
        _capturedInitial = true;
    }

    void OnEnable()
    {
        // 延迟节点被重新激活时会再走一次，与原版 OnEnable 时机一致
        BindMaterial();
    }

    void BindMaterial()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;

        if (!_capturedInitial) CaptureInitialMaterial();
        _mat = _renderer.material;             // 实例材质，避免污染共享材质
        if (_mat == null) return;

        _mainOffset = keepInitOffset ? _initialMainOffset : Vector2.zero;

        if (useSecondMap && !string.IsNullOrEmpty(secondMapName))
        {
            _secondSTProp = secondMapName + "_ST";
            _secondST = keepInitOffset
                ? _initialSecondST
                : new Vector4(1f, 1f, 0f, 0f);
        }
    }

    void Update()
    {
        if (_mat == null) return;
        float dt = Time.deltaTime * PlaybackSpeed;

        if (mainSpeed.x != 0f || mainSpeed.y != 0f)
        {
            _mainOffset.x += dt * mainSpeed.x * SpeedScale;
            _mainOffset.y += dt * mainSpeed.y * SpeedScale;
            if (protectMainUV)
            {
                _mainOffset.x -= Mathf.Floor(_mainOffset.x);
                _mainOffset.y -= Mathf.Floor(_mainOffset.y);
            }
            _mat.mainTextureOffset = _mainOffset;
        }

        if (useSecondMap && (secondSpeed.x != 0f || secondSpeed.y != 0f) && !string.IsNullOrEmpty(_secondSTProp))
        {
            _secondST.z += dt * secondSpeed.x * SpeedScale;
            _secondST.w += dt * secondSpeed.y * SpeedScale;
            if (protectSecondUV)
            {
                _secondST.z -= Mathf.Floor(_secondST.z);
                _secondST.w -= Mathf.Floor(_secondST.w);
            }
            _mat.SetVector(_secondSTProp, _secondST);
        }
    }

    /// <summary>把当前偏移复位（对应原版 OnEnable 时的重取初值）。</summary>
    public void ResetOffset()
    {
        BindMaterial();
    }

    /// <summary>
    /// 回对象池前复位材质/运行时偏移。
    /// 宿主 ArkFxEffectHost.Recycle 会广播到本组件；若不在这里复位，
    /// 下次 OnEnable 的 keepInitOffset 会读取“上一轮结束值”，导致池化复用逐次漂移。
    /// </summary>
    public override void OnRecycle()
    {
        Vector2 main = keepInitOffset ? _initialMainOffset : Vector2.zero;
        Vector4 second = keepInitOffset ? _initialSecondST : new Vector4(1f, 1f, 0f, 0f);

        _mainOffset = main;
        _secondST = second;

        if (_mat != null)
        {
            if (mainSpeed.x != 0f || mainSpeed.y != 0f)
                _mat.mainTextureOffset = main;
            if (useSecondMap && !string.IsNullOrEmpty(_secondSTProp))
                _mat.SetVector(_secondSTProp, second);
        }
    }
}
