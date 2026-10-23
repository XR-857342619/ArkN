// 手动烘焙光照 shader：绕开引擎 GI 的 RGBM 解码黑盒，按官方公式 rgb×alpha×5 精确解码 comp_light。
// 用于场景还原：本场景无实时灯，光照 100% 来自官方烘焙 comp_light（Shadowmask 口径全量光照），
// Unlit 化乘法即可复刻官方观感。uv2 = 官方图集绝对坐标（bin 直造已写入）。
// 支持：主贴图 ×_Color、法线、自发光、alphatest（Cutout）。Fade/Transparent 材质保持 Standard 不走此路。
// 双模式（2026-09-25）：默认乘烘焙（还原路）；勾选 _NoBake = 纯实时光（模块化地块，暗部可被实时灯/环境照亮）。
Shader "Arkn/LightmapBaked" {
Properties {
    _MainTex ("MainTex", 2D) = "white" {}
    _Color ("Color", Color) = (1,1,1,1)
    _LightmapTex ("Baked Lightmap (RGBM)", 2D) = "black" {}
    _LightmapScale ("Lightmap RGBM Scale", Float) = 5.0
    _BumpMap ("Normal Map", 2D) = "bump" {}
    _EmissionMap ("Emission", 2D) = "black" {}
    _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    [Toggle(_NOBAKE)] _NoBake ("纯实时光（不乘烘焙，模块化地块用）", Float) = 0
}
SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
    LOD 200

    CGPROGRAM
    #pragma surface surf Standard alphatest:_Cutoff
    #pragma target 3.0
    #pragma shader_feature_local _NOBAKE

    sampler2D _MainTex, _LightmapTex, _BumpMap, _EmissionMap;
    fixed4 _Color, _EmissionColor;
    half _LightmapScale;

    struct Input {
        float2 uv_MainTex;
        float2 uv2_LightmapTex;     // 官方 UV2（图集绝对坐标）
        float2 uv_BumpMap;
        float2 uv_EmissionMap;
    };

    void surf (Input IN, inout SurfaceOutputStandard o) {
        fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        fixed3 baked = fixed3(1, 1, 1);
#ifndef _NOBAKE
        // 烘焙路：Albedo 预乘官方 comp_light（只适合固定摆位的场景还原）
        fixed4 lm = tex2D(_LightmapTex, IN.uv2_LightmapTex);
        baked = lm.rgb * lm.a * _LightmapScale;   // 官方 RGBM 解码：M = alpha*5
#endif
        // 纯实时路（_NOBAKE）：Albedo=原贴图，明暗全由实时灯+环境探针决定，暗部可被照亮
        o.Albedo = albedo.rgb * baked;
        o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
        o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb * _EmissionColor.rgb;
        o.Metallic = 0;
        o.Smoothness = 0;
        o.Alpha = albedo.a;
    }
    ENDCG
}
FallBack "Diffuse"
}
