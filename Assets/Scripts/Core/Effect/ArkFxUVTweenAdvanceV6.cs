// ============================================================
// ArkFxUVTweenAdvanceV6 - Torappu.Fx.FxUVTweenerAdvance 的运行时替身（v6，IDA 实证版）
//
// 依据：反编译 Torappu.Fx.FxUVTweenerAdvance 的 Awake / OnEnable / Update，
//      以及 UVTweenerMapSetting.get_mapSTProp。
//
// 与原版 FxUVTweener 的区别：
//   1. 没有 protectMainUV / protectSecondUV
//   2. 有 tweenRepeat(bool)：
//         true  -> 无限循环，且每帧把累加值取小数（包裹到 [0,1)）
//         false -> 只跑 tweenAnmTime 秒，到点后整体停住
//   3. 有 extraMapSettings 列表：每项 { enabled, xSpeed, ySpeed, st(Vector4), mapName }
//      写 <mapName>_ST
//
// Update 的原版判断是： if (tweenRepeat || m_time <= tweenAnmTime) { m_time += dt; ... }
// 即 tweenAnmTime 那一帧仍会执行，之后彻底停下。
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class ArkFxUVTweenAdvanceV6 : ArkFxBehaviour
{
    [System.Serializable]
    public class MapSetting
    {
        public bool enabled = true;
        public string mapName;
        public Vector2 speed;
        public Vector4 st = new Vector4(1f, 1f, 0f, 0f);   // x,y=scale z,w=offset
    }

    [Header("主贴图（_MainTex）")]
    public Vector2 mainSpeed;

    [Header("第二贴图")]
    public bool useSecondMap;
    public string secondMapName = "_DisturTex";
    public Vector2 secondSpeed;

    [Header("附加贴图")]
    public List<MapSetting> extraMaps = new List<MapSetting>();

    [Header("时间")]
    [Tooltip("true = 无限循环（每帧包裹 [0,1)）；false = 只跑 tweenAnmTime 秒")]
    public bool tweenRepeat = true;
    [Tooltip("非循环时的持续秒数（原版 tweenAnmTime）")]
    public float tweenAnmTime = 1f;

    [Header("初始化")]
    public bool keepInitOffset = true;
    public bool useSharedMaterial;

    const float SpeedScale = 2f;   // 原版同样乘 2

    Renderer _renderer;
    Material _mat;
    Vector2 _mainOffset;
    Vector4 _secondST;
    string _secondSTProp;
    float _time;

    // 对象池复用初始快照：见 ArkFxUVTweenV6 同名注释。
    Vector2 _initialMainOffset;
    Vector4 _initialSecondST = new Vector4(1f, 1f, 0f, 0f);
    List<Vector4> _initialExtraST = new List<Vector4>();
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

        var mat = _renderer.material;
        if (mat == null) return;

        _initialMainOffset = mat.mainTextureOffset;
        if (useSecondMap && !string.IsNullOrEmpty(secondMapName))
        {
            string prop = secondMapName + "_ST";
            _initialSecondST = mat.HasProperty(prop)
                ? (Vector4)mat.GetVector(prop)
                : new Vector4(1f, 1f, 0f, 0f);
        }

        _initialExtraST = new List<Vector4>();
        if (extraMaps != null)
        {
            for (int i = 0; i < extraMaps.Count; i++)
            {
                var m = extraMaps[i];
                if (m == null || string.IsNullOrEmpty(m.mapName))
                {
                    _initialExtraST.Add(new Vector4(1f, 1f, 0f, 0f));
                    continue;
                }

                string prop = m.mapName + "_ST";
                _initialExtraST.Add(mat.HasProperty(prop)
                    ? (Vector4)mat.GetVector(prop)
                    : m.st);
            }
        }
        _capturedInitial = true;
    }

    void OnEnable()
    {
        BindMaterial();
        _time = 0f;
    }

    void BindMaterial()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;
        if (!_capturedInitial) CaptureInitialMaterial();
        _mat = _renderer.material;
        if (_mat == null) return;

        _mainOffset = keepInitOffset ? _initialMainOffset : Vector2.zero;

        if (useSecondMap && !string.IsNullOrEmpty(secondMapName))
        {
            _secondSTProp = secondMapName + "_ST";
            _secondST = keepInitOffset
                ? _initialSecondST
                : new Vector4(1f, 1f, 0f, 0f);
        }

        // 额外贴图的 ST 从实例首次快照起算；不要读“上一轮结束时”的材质值。
        if (extraMaps != null)
        {
            for (int i = 0; i < extraMaps.Count; i++)
            {
                var m = extraMaps[i];
                if (m == null || string.IsNullOrEmpty(m.mapName)) continue;
                if (i < _initialExtraST.Count)
                    m.st = _initialExtraST[i];
            }
        }
    }

    void Update()
    {
        if (_mat == null) return;
        if (!tweenRepeat && _time > tweenAnmTime) return;

        float dt = Time.deltaTime * PlaybackSpeed;
        _time += dt;

        if (mainSpeed.x != 0f || mainSpeed.y != 0f)
        {
            _mainOffset.x += dt * mainSpeed.x * SpeedScale;
            _mainOffset.y += dt * mainSpeed.y * SpeedScale;
            if (tweenRepeat) Wrap(ref _mainOffset);
            _mat.mainTextureOffset = _mainOffset;
        }

        if (useSecondMap && (secondSpeed.x != 0f || secondSpeed.y != 0f) && !string.IsNullOrEmpty(_secondSTProp))
        {
            _secondST.z += dt * secondSpeed.x * SpeedScale;
            _secondST.w += dt * secondSpeed.y * SpeedScale;
            if (tweenRepeat) Wrap(ref _secondST);
            _mat.SetVector(_secondSTProp, _secondST);
        }

        if (extraMaps == null) return;
        for (int i = 0; i < extraMaps.Count; i++)
        {
            var m = extraMaps[i];
            if (m == null || !m.enabled) continue;
            if (string.IsNullOrEmpty(m.mapName)) continue;
            if (m.speed.x == 0f && m.speed.y == 0f) continue;

            var st = m.st;
            st.z += dt * m.speed.x * SpeedScale;
            st.w += dt * m.speed.y * SpeedScale;
            if (tweenRepeat) Wrap(ref st);
            m.st = st;

            string prop = m.mapName + "_ST";
            if (_mat.HasProperty(prop)) _mat.SetVector(prop, st);
        }
    }

    static void Wrap(ref Vector2 v)
    {
        v.x -= Mathf.Floor(v.x);
        v.y -= Mathf.Floor(v.y);
    }

    static void Wrap(ref Vector4 v)
    {
        v.z -= Mathf.Floor(v.z);
        v.w -= Mathf.Floor(v.w);
    }

    /// <summary>
    /// 回对象池前复位；避免 keepInitOffset 在下一次 OnEnable 时吃到上一轮结束值，
    /// 也避免序列化在组件上的 extraMaps[i].st 逐次漂移。
    /// </summary>
    public override void OnRecycle()
    {
        _time = 0f;

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

        if (extraMaps == null) return;
        for (int i = 0; i < extraMaps.Count; i++)
        {
            var m = extraMaps[i];
            if (m == null || string.IsNullOrEmpty(m.mapName)) continue;
            if (i < _initialExtraST.Count)
                m.st = _initialExtraST[i];

            if (_mat != null && _mat.HasProperty(m.mapName + "_ST"))
                _mat.SetVector(m.mapName + "_ST", m.st);
        }
    }
}
