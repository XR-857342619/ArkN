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

    void Awake() { _renderer = GetComponent<Renderer>(); }

    void OnEnable()
    {
        BindMaterial();
        _time = 0f;
    }

    void BindMaterial()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;
        _mat = _renderer.material;
        if (_mat == null) return;

        _mainOffset = keepInitOffset ? _mat.mainTextureOffset : Vector2.zero;

        if (useSecondMap && !string.IsNullOrEmpty(secondMapName))
        {
            _secondSTProp = secondMapName + "_ST";
            _secondST = keepInitOffset && _mat.HasProperty(_secondSTProp)
                ? (Vector4)_mat.GetVector(_secondSTProp)
                : new Vector4(1f, 1f, 0f, 0f);
        }

        // 额外贴图的 ST 以材质现有值为起点（原版 OnEnable 同一套逻辑）
        if (extraMaps != null)
        {
            for (int i = 0; i < extraMaps.Count; i++)
            {
                var m = extraMaps[i];
                if (m == null || string.IsNullOrEmpty(m.mapName)) continue;
                string prop = m.mapName + "_ST";
                if (keepInitOffset && _mat.HasProperty(prop)) m.st = _mat.GetVector(prop);
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
}
