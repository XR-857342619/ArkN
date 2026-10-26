// ============================================================
// ArkFxUVRotation - Torappu.Fx.FxUVRotation 的运行时替身（P1，IDA 实证版）
//
// 依据：反编译 Torappu.Fx.FxUVRotation 的
//       Awake / LateUpdate / _ApplyRotationProperties / _CalculateMatrix / _Initialize
//
// 原版行为（LateUpdate）：
//   _ApplyRotationProperties(1.0f)
//     renderer.GetPropertyBlock(mpb)
//     mpb.SetFloat("_HG_UV_ROTATION", 1.0f)
//     mpb.SetVector(_Rotation0, rotateTex1 ? Matrix(angle1) : IDENTITY)
//     mpb.SetVector(_Rotation1, rotateTex2 ? Matrix(angle2) : IDENTITY)
//     mpb.SetVector(_Rotation2, rotateTex3 ? Matrix(angle3) : IDENTITY)
//     mpb.SetVector(_Rotation3, rotateTex4 ? Matrix(angle4) : IDENTITY)
//     renderer.SetPropertyBlock(mpb)
//
//   Matrix(a) = Vector4(cos, -sin, sin, cos)     // 2x2 旋转矩阵打包成 float4
//   IDENTITY  = Vector4(1, 0, 0, 1)
//
// 属性名来源（从 IDB 的 StringLiteral 注释取出）：
//   float 开关 = "_HG_UV_ROTATION"（固定）
//   四个向量 = _propertyName + "0" / "1" / "2" / "3"，_propertyName 默认 "_Rotation"
//
// 注意：原版用 MaterialPropertyBlock（不改材质本身），同一材质的多个实例可以各自旋转，
//       本实现保持一致。
// ============================================================
using UnityEngine;

public class ArkFxUVRotation : ArkFxBehaviour
{
    [Header("属性名")]
    [Tooltip("默认 _Rotation；四个向量属性为 <该名>0..3")]
    public string propertyName = "_Rotation";

    [Header("四张贴图是否参与旋转")]
    public bool rotateTex1 = true;
    public bool rotateTex2 = true;
    public bool rotateTex3;
    public bool rotateTex4;

    [Header("角度（度）")]
    public float angle1 = 90f;
    public float angle2 = 90f;
    public float angle3;
    public float angle4;

    [Header("调试")]
    [Tooltip("反编译得到的矩阵是 (cos,-sin,sin,cos)；若发现旋转方向相反可勾选此项")]
    public bool flipRotationSign;

    static readonly int UvRotationId = Shader.PropertyToID("_HG_UV_ROTATION");
    static readonly Vector4 Identity = new Vector4(1f, 0f, 0f, 1f);

    Renderer _renderer;
    MaterialPropertyBlock _mpb;
    int[] _rotationIds = new int[4];

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        RebuildIds();
    }

    void OnEnable()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        RebuildIds();
    }

    void RebuildIds()
    {
        for (int i = 0; i < 4; i++)
            _rotationIds[i] = Shader.PropertyToID((propertyName ?? "_Rotation") + i);
    }

    void LateUpdate()
    {
        if (_renderer == null || string.IsNullOrEmpty(propertyName)) return;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(UvRotationId, 1f);
        _mpb.SetVector(_rotationIds[0], rotateTex1 ? Matrix(angle1) : Identity);
        _mpb.SetVector(_rotationIds[1], rotateTex2 ? Matrix(angle2) : Identity);
        _mpb.SetVector(_rotationIds[2], rotateTex3 ? Matrix(angle3) : Identity);
        _mpb.SetVector(_rotationIds[3], rotateTex4 ? Matrix(angle4) : Identity);
        _renderer.SetPropertyBlock(_mpb);
    }

    /// <summary>原版 _CalculateMatrix：把 2x2 旋转矩阵打包进 float4。</summary>
    public Vector4 Matrix(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        if (flipRotationSign) s = -s;
        return new Vector4(c, -s, s, c);
    }
}
