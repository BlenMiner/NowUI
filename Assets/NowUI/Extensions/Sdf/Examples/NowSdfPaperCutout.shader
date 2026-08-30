Shader "NowUI/SDF Examples/Paper Cutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _NowSdfAbiVersion ("Now SDF ABI Version", Float) = 2
        _PaperColor ("Paper", Color) = (0.95, 0.8, 0.48, 1)
        _PaperBackColor ("Edge Shade", Color) = (0.38, 0.12, 0.12, 1)
        [HDR] _PaperHighlightColor ("Edge Highlight", Color) = (1.3, 0.9, 0.52, 0.8)
        _PaperShadowColor ("Custom Shadow", Color) = (0.02, 0.01, 0.04, 0.72)
        _PaperLightDirection ("Light Direction", Vector) = (-0.65, -0.75, 0, 0)
        _PaperBevel ("Bevel Width", Float) = 10
        _PaperShadowOffset ("Shadow Offset", Vector) = (10, 12, 0, 0)
        _PaperShadowSoftness ("Shadow Softness", Float) = 10
        _PaperShadowSpread ("Shadow Spread", Float) = 1
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
        _NowCanvasLayout ("Now Canvas Layout", Float) = 0
        _SdfShapeCount ("Shape Count", Float) = 0
        _SdfLayerCount ("Layer Count", Float) = 0
        _SdfFeather ("Feather", Float) = 0
        _SdfOutline ("Outline", Vector) = (0, 0, 0, 0)
        _SdfOutlineColor ("Outline Color", Color) = (0, 0, 0, 0)
        _SdfGlow ("Glow", Vector) = (0, 1, 0, 0)
        _SdfGlowColor ("Glow Color", Color) = (0, 0, 0, 0)
        _SdfShadow ("Shadow", Vector) = (0, 0, 0, 0)
        _SdfShadowColor ("Shadow Color", Color) = (0, 0, 0, 0)
        _SdfInnerShadow ("Inner Shadow", Vector) = (0, 0, 0, 0)
        _SdfInnerShadowColor ("Inner Shadow Color", Color) = (0, 0, 0, 0)
        _SdfEmboss ("Emboss", Vector) = (0, 0, 1, 0)
        _SdfContour ("Contour", Vector) = (1, 0, 0, 0)
        _SdfContourColor ("Contour Color", Color) = (0, 0, 0, 0)
        _SdfContourMask ("Contour Mask", Vector) = (0, 0, 0, 0)
        _SdfWarp ("Warp", Vector) = (0, 1, 0, 0)
        [HideInInspector] _SdfMaskOutput ("SDF Mask Output", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _UIMaskSoftnessX ("UI Mask Softness X", Float) = 1
        [HideInInspector] _UIMaskSoftnessY ("UI Mask Softness Y", Float) = 1
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            float4 _PaperColor;
            float4 _PaperBackColor;
            float4 _PaperHighlightColor;
            float4 _PaperShadowColor;
            float4 _PaperLightDirection;
            float4 _PaperShadowOffset;
            float _PaperBevel;
            float _PaperShadowSoftness;
            float _PaperShadowSpread;

            #define NOW_SDF_CUSTOM_FINAL_SHADE NowSdfPaperCutoutShadeV2
            #include "../NowSdfShaderV2.cginc"

            float4 NowSdfPaperCutoutShadeV2(
                float4 stockColor,
                float4 fill,
                float4 tint,
                float2 quadUv,
                float2 scenePosition,
                float2 sourceScenePosition,
                float2 sceneSize,
                float signedDistance,
                float coverage,
                float pixelWidth,
                float edge)
            {
                float2 gradient = float2(ddx(signedDistance), ddy(signedDistance));
                float2 normal2 = normalize(gradient + 0.0001);
                float2 light = normalize(_PaperLightDirection.xy + 0.0001);
                float facing = dot(normal2, light) * 0.5 + 0.5;
                float bevel = 1.0 - smoothstep(0.0, max(_PaperBevel, pixelWidth), abs(signedDistance));

                float3 paper = lerp(_PaperBackColor.rgb, _PaperColor.rgb, 0.56 + facing * 0.44);
                paper *= lerp(1.0, saturate(fill.rgb), 0.22);
                paper += _PaperHighlightColor.rgb * pow(saturate(facing), 5.0) * bevel * _PaperHighlightColor.a;
                float4 inside = float4(saturate(paper), _PaperColor.a * fill.a * coverage);

                float shadowDistance = NowSdfEvaluateEffectDistanceV2(
                    sourceScenePosition - _PaperShadowOffset.xy) - _PaperShadowSpread;
                float shadowAlpha = smoothstep(
                    max(_PaperShadowSoftness, pixelWidth) + edge,
                    -edge,
                    shadowDistance) * (1.0 - coverage);
                float4 shadow = _PaperShadowColor;
                shadow.a *= shadowAlpha * tint.a;

                return NowSdfAlphaOverV2(shadow, inside);
            }
            ENDCG
        }
    }
}
