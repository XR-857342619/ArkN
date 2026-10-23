// ============================================================
// ArkFxUVTween - Torappu.Fx.FxUVTweener / FxUVTweenerAdvance 的运行时替身 v4
//
// 参数来源：AB 内嵌 typetree 可直接读出原版精确字段值（见 Tools/dump_fx_params_v2.py），
// ArkFxLoader 从 fxparams.json 按 prefab+节点路径取参数后配置本组件。
//
// v4 关键发现（从 shader 反汇编属性声明实锤）：
//   Torappu/Particles/Disturb 系 shader 不读材质的 tiling/offset，
//   而是读 _UVTween 向量：xy = 主贴图 UV 偏移，zw = _DisturTex UV 偏移。
//   因此检测到材质带 _UVTween 属性时，滚动偏移写入该向量；
//   其他 shader（或附加贴图）仍走传统的 SetTextureOffset 通道。
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class ArkFxUVTween : MonoBehaviour
{
    [System.Serializable]
    public class ExtraMap
    {
        public string mapName;
        public Vector2 speed;
        public Vector4 st; // x,y=scale  z,w=offset
    }

    [Header("主贴图（_MainTex）")]
    public bool protectMainUV;
    public Vector2 mainSpeed;

    [Header("第二贴图")]
    public bool useSecondMap;
    public string secondMapName = "_DisturTex";
    public bool protectSecondUV;
    public Vector2 secondSpeed;

    [Header("附加贴图（Advance）")]
    public List<ExtraMap> extras = new List<ExtraMap>();

    [Header("行为")]
    public bool keepInitOffset = true;
    public bool useSharedMaterial;
    [Tooltip(">0 = 只滚动该秒数（Advance 的 tweenAnmTime）；<=0 = 无限")]
    public float duration;

    Material _mat;
    bool _useUVTween;   // 材质带 _UVTween 属性 → 偏移写入向量通道
    Vector4 _tween;     // xy=主贴图偏移 zw=_DisturTex 偏移
    // channel: 0=传统 ST 通道, 1=_UVTween.xy(主), 2=_UVTween.zw(扰动)
    readonly List<(string prop, Vector2 init, int channel)> _slots = new List<(string, Vector2, int)>();
    readonly Dictionary<string, Vector2> _speeds = new Dictionary<string, Vector2>();
    float _t;
    bool _stopped;

    void Start()
    {
        var r = GetComponent<Renderer>();
        if (r == null) { enabled = false; return; }
        _mat = useSharedMaterial ? r.sharedMaterial : r.material;
        _useUVTween = _mat.HasProperty("_UVTween");

        // protect*UV 的真实语义：=1 表示“优先 _UVTween 向量通道，不动材质 ST”。
        // 但这是有前提的——shader 必须真的有 _UVTween 属性（Disturb 系才有）。
        // v5 修正（enemy_smedzi_buff_01/flow_3 系实锤）：Dissolve 系 shader 没有
        // _UVTween，原版 FxUVTweener 此时回退到传统 ST 滚动（xspeed=-0.1 是美术
        // 实填数据，原版里烟雾就是飘的）；v4 的“没有 _UVTween 就跳过”会让这些
        // 节点完全静止。因此：有向量通道走向量，没有一律回退 ST。
        AddSlot("_MainTex", mainSpeed, _useUVTween ? 1 : 0);
        if (useSecondMap && !string.IsNullOrEmpty(secondMapName)
            && _mat.HasProperty(secondMapName))
        {
            int ch = (_useUVTween && secondMapName == "_DisturTex") ? 2 : 0;
            AddSlot(secondMapName, secondSpeed, ch);
        }
        foreach (var ex in extras)
        {
            if (ex == null || string.IsNullOrEmpty(ex.mapName)) continue;
            if (_mat.HasProperty(ex.mapName))
            {
                // 同主槽原则：没有 _UVTween 属性的材质一律走传统 ST 通道
                int ch;
                if (!_useUVTween) ch = 0;
                else if (ex.mapName == "_DisturTex") ch = 2;
                else if (ex.mapName == "_MainTex") ch = 1;
                else ch = 0;
                AddSlot(ex.mapName, ex.speed, ch, ex.st);
            }
        }
    }

    void AddSlot(string prop, Vector2 speed, int channel, Vector4 st = default)
    {
        // 静止的贴图一律不碰：原版 speed=0 等于"什么都不做"
        bool noSpeed = speed.sqrMagnitude < 1e-10f;
        bool noSt = (st.x == 0f && st.y == 0f && st.z == 0f && st.w == 0f)
                 || (st.x == 1f && st.y == 1f && st.z == 0f && st.w == 0f);
        if (noSpeed && noSt) return;

        var tex = _mat.HasProperty(prop) ? _mat.GetTexture(prop) : null;
        if (tex != null && tex.wrapMode != TextureWrapMode.Repeat)
        {
            // UV 要滚动，采样必须平铺：Clamp 贴图滚出边界 = 采样到边缘透明像素
            tex.wrapMode = TextureWrapMode.Repeat;
        }

        var baseOff = keepInitOffset ? _mat.GetTextureOffset(prop) : Vector2.zero;
        // st: 乘 scale、加 offset（注意 st 未传时是全零 default，必须视为"没有 st"）
        if (!noSt)
        {
            var cur = _mat.GetTextureScale(prop);
            _mat.SetTextureScale(prop, new Vector2(cur.x * st.x, cur.y * st.y));
            baseOff = new Vector2(baseOff.x * st.x + st.z, baseOff.y * st.y + st.w);
        }
        if (noSpeed) return; // 只有 st 有值：一次性应用即可，无需进动画列表
        _slots.Add((prop, baseOff, channel));
        _speeds[prop] = speed;
    }

    void Update()
    {
        if (_mat == null || _stopped) return;
        _t += Time.deltaTime;
        if (duration > 0f && _t >= duration) { _stopped = true; return; }
        bool vectorDirty = false;
        foreach (var s in _slots)
        {
            var sp = _speeds.TryGetValue(s.prop, out var v) ? v : Vector2.zero;
            switch (s.channel)
            {
                case 1:
                    // _UVTween 通道写“恒定速度”，shader 内部自行乘 _Time.y 积分：
                    // 写累计偏移会导致 偏移×时间 的二次加速（越滚越快）
                    _tween.x = sp.x; _tween.y = sp.y;
                    vectorDirty = true;
                    break;
                case 2:
                    _tween.z = sp.x; _tween.w = sp.y;
                    vectorDirty = true;
                    break;
                default:
                    // 传统 ST 通道才需要自己累计偏移
                    var off = s.init + sp * _t;
                    off.x = Mathf.Repeat(off.x, 1f);
                    off.y = Mathf.Repeat(off.y, 1f);
                    _mat.SetTextureOffset(s.prop, off);
                    break;
            }
        }
        if (_useUVTween && vectorDirty)
            _mat.SetVector("_UVTween", _tween);
    }
}
