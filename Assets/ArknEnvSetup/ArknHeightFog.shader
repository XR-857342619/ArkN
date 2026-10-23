Shader "Arkn/HeightFog"
{
    // ============================================================================
    //  高度雾 v3 —— 沿视线的光学厚度积分（替代 v1/v2 的"整屏贴色"）
    //
    //  官方语义（Torappu.Rendering.SceneEffectProfile，全库 150 个 ae_* 统计得出）：
    //     密度 σ(y) = clamp((fogBaseline - y) / (fogBaseline - fogTop), 0, 1)
    //     - y >= fogBaseline  → 0（雾顶，无雾）
    //     - y <= fogTop       → 1（浓端，满密度）
    //     - 中间线性过渡（带宽中位数 10.8 世界单位）
    //     实测 40 个 heightFog=1 的 profile 中 98% 满足 fogBaseline > fogTop。
    //
    //  为什么必须积分而不是贴色：可见雾量 = 1 - exp(-∫σ ds)，沿视线累计。
    //  于是同一屏像素：脚下/近处几何光学厚度≈0 → 清晰；远处/低处 → 雾多。
    //  这正是"从楼顶往楼底看一片雾蒙蒙、镜头前却不过一层膜"的物理来源。
    //
    //  带内线性段的积分是解析解（梯形精确），不需要循环或多次采样。
    // ============================================================================
    Properties
    {
        // 必须声明：Graphics.Blit 把源画面绑定到 _MainTex；不声明会静默绑定失败（DX11 上采样到 0.5 灰）
        _MainTex ("Texture", any) = "white" {}
        _FogColor("Fog Color (官方 fogColor)", Color) = (0.228, 0.502, 0.173, 1)
        _FogBaseline("Fog Baseline 雾顶高度(无雾)", Float) = 5.8
        _FogTop("Fog Top 浓端高度(满密度)", Float) = 2.0
        _Density("Density 消光系数/单位", Float) = 0.05
        _MaxOpacity("Max Opacity", Range(0,1)) = 1.0

        _UseNoise("Use Noise", Float) = 0
        _NoiseAmount("Noise Amount", Range(0,1)) = 0.25
        _NoiseScale("Noise Scale", Vector) = (1, 1, 0, 0)
        _NoiseSpeed("Noise Speed", Float) = 1

        _UseCenterMask("Use Center Mask", Float) = 0
        _MaskAmount("Mask Amount", Range(0,1)) = 1
        _MaskRadius("Mask Radius", Float) = 0.35
        _MaskSoftness("Mask Softness", Float) = 0.25

        _FogSky("Fog Sky/Far", Float) = 1
        _SkyDistance("Sky Distance", Float) = 200

        _DebugFull("Debug Full Screen", Float) = 0
        _DebugRamp("Debug Show Density", Float) = 0
        _DebugDepth("Debug Show Depth", Float) = 0
        _AxisZ("Height Axis = Z (experiment)", Float) = 0
        _DistancePow("Optical Thickness Power (1=exp, 2=exp2-like)", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            fixed4 _FogColor;
            float _FogBaseline, _FogTop, _Density, _MaxOpacity;
            float _UseNoise, _NoiseAmount, _NoiseSpeed;
            float _UseCenterMask, _MaskAmount, _MaskRadius, _MaskSoftness;
            float _FogSky, _SkyDistance, _DebugFull, _DebugRamp, _DebugDepth, _AxisZ, _DistancePow;
            float4 _NoiseScale;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 ray : TEXCOORD1;
            };

            float3 RayFromUV(float2 uv)
            {
                float2 ndc = uv * 2 - 1;
                float4 clip = float4(ndc, 0.5, 1);
                float4 vpos = mul(unity_CameraInvProjection, clip);
                float3 viewDir = mul(unity_CameraToWorld, float4(vpos.xyz / vpos.w, 0)).xyz - _WorldSpaceCameraPos;
                return viewDir;
            }

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.ray = RayFromUV(v.texcoord);
                return o;
            }

            // ∫₀^d clamp(a + b*t, 0, 1) dt  —— 带内线性段的解析积分
            float IntegrateClamp(float a, float b, float d)
            {
                if (abs(b) < 1e-5)
                    return clamp(a, 0.0, 1.0) * d;

                float t0 = -a / b;          // 密度 = 0 处
                float t1 = (1.0 - a) / b;   // 密度 = 1 处
                float ta = min(t0, t1);
                float tb = max(t0, t1);

                // t < ta 一侧的常量值：b>0 时该侧密度为 0（雾顶），b<0 时为 1（浓端）
                float lowConst = (b > 0.0) ? 0.0 : 1.0;
                float highConst = 1.0 - lowConst;

                float total = 0.0;

                float segA = clamp(min(d, ta), 0.0, d);
                total += lowConst * max(segA, 0.0);

                float bs = clamp(ta, 0.0, d);
                float be = clamp(tb, 0.0, d);
                if (be > bs)
                    total += ((a + b * bs) + (a + b * be)) * 0.5 * (be - bs);   // 线性段：梯形即精确

                float cs = clamp(tb, 0.0, d);
                if (d > cs)
                    total += highConst * (d - cs);

                return total;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float eye = LinearEyeDepth(raw);

                if (_DebugDepth > 0.5)
                {
                    // 调试：把"深度距离 eye"按 log2 分 10 档显示成灰阶（每档一倍距离）。
                    //   正常俯视（相机高 10~30）地面应在中档（4~6 档）且近远有差别；
                    //   整屏同一档且接近最白（≈2^10 米级）→ 深度纹理没数据，所有像素被当成超远 —— 石锤。
                    float dl = log2(max(eye, 0.01));
                    float qd = floor(saturate((dl + 2.0) / 12.0) * 10.0) / 10.0;
                    return fixed4(qd.xxx, 1.0);
                }

                float3 dir = normalize(i.ray);

                float isSky = (raw >= 0.9999) ? 1.0 : 0.0;
                float dist = eye;
                if (isSky > 0.5)
                {
                    if (_FogSky < 0.5)
                    {
                        col.rgb = lerp(col.rgb, _FogColor.rgb, saturate(_DebugFull) * _MaxOpacity);
                        return col;
                    }
                    dist = _SkyDistance;
                }

                // ---- 沿视线积分官方高度密度曲线 ----
                // 高度轴：_AxisZ=0 用世界 Y（Unity 标准，官方数据已验证）；=1 用 Z（Z-up 假设对照实验）
                float y0 = lerp(_WorldSpaceCameraPos.y, _WorldSpaceCameraPos.z, _AxisZ);
                float dy = lerp(dir.y, dir.z, _AxisZ);
                float S = max(_FogBaseline - _FogTop, 0.001);
                float a = (_FogBaseline - y0) / S;
                float b = -dy / S;

                float thick = IntegrateClamp(a, b, dist);   // 单位：世界长度
                float sigma = _Density;

                if (_UseNoise > 0.5 && _NoiseAmount > 0.001)
                {
                    // 在视线中段采样噪声（跟世界坐标绑定），避免变成屏幕空间图案
                    float tm = min(dist * 0.5, 40.0);
                    float3 wp = _WorldSpaceCameraPos + dir * tm;
                    float n = vnoise(wp.xz * _NoiseScale.xy + float2(0.0, _Time.y * _NoiseSpeed * 0.1));
                    sigma *= lerp(1.0 - _NoiseAmount, 1.0 + _NoiseAmount, n);
                }

                float od = sigma * thick;                   // 光学厚度
                // 官方曲线实验旋钮：p=1 标准 Beer-Lambert；p=2 ≈ exp2（近处更清、远处更陡）
                od = pow(od, max(_DistancePow, 0.01));
                float amt = 1.0 - exp(-od);                 // 可见雾量

                if (_DebugRamp > 0.5)
                {
                    // 调试：直接显示"最终雾量 amt"，按 5% 一档量化成灰阶条带（纯灰阶，不掺雾色）：
                    //   有梯度条带（近处 1~4 档灰、远处 8~10 档白）= 积分与深度纹理都正常；
                    //   整屏同一档、且接近最白 = 深度纹理没数据（所有像素被当成超远/天空）。
                    float q = floor(saturate(amt) * 20.0) / 20.0;
                    return fixed4(q.xxx, 1.0);
                }

                if (_UseCenterMask > 0.5)
                {
                    float2 dc = (i.uv - 0.5) * float2(_ScreenParams.x / max(_ScreenParams.y, 1.0), 1.0);
                    float r = length(dc);
                    float mask = smoothstep(_MaskRadius, _MaskRadius + max(_MaskSoftness, 0.001), r);
                    amt *= lerp(1.0, mask, saturate(_MaskAmount));
                }

                if (_DebugFull > 0.5)
                    amt = 1.0;

                col.rgb = lerp(col.rgb, _FogColor.rgb, saturate(amt) * _MaxOpacity);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
