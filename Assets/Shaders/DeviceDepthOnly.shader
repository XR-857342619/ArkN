Shader "Custom/DeviceDepthOnly"
{
    // 供 DeviceDepthCamera 以 SetReplacementShader 方式使用：
    // 只写深度、不写颜色（ColorMask 0），为 Spine 层提供"装置深度缓冲"。
    // 不透明装置：整面写深度（正常遮挡 Spine）；
    // 镂空/透明装置：alpha 低于阈值的像素不写深度 → Spine 可从透明区域被看见。
    // 属性按名称从原材质拷贝（_MainTex/_Color/_Cutoff 是 Unity 惯用名）。
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass { ColorMask 0 ZWrite On }
    }
    SubShader
    {
        Tags { "RenderType" = "Geometry" "Queue" = "Geometry" }
        Pass { ColorMask 0 ZWrite On }
    }
    SubShader
    {
        // 镂空装置：按原材质 _Cutoff 做 alpha 裁剪
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            ColorMask 0
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed _Cutoff;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a * _Color.a;
                clip(a - _Cutoff);
                return 1;
            }
            ENDCG
        }
    }
    SubShader
    {
        // 半透明装置：alpha <= 0.1（近全透明）的像素不写深度，Spine 可被看见；
        // 较实的部分仍遮挡 Spine。
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            ColorMask 0
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a * _Color.a;
                clip(a - 0.1);
                return 1;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" }
        Pass { ColorMask 0 ZWrite On }
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" }
        Pass { ColorMask 0 ZWrite On }
    }
    SubShader
    {
        Tags { "RenderType" = "" }
        Pass { ColorMask 0 ZWrite On }
    }
    Fallback Off
}
