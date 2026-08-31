Shader "NowUI/SDF Examples/Topographic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _NowSdfAbiVersion ("Now SDF ABI Version", Float) = 2
        [HDR] _TopoInsideColor ("Inside Field", Color) = (0.03, 0.3, 0.42, 0.72)
        [HDR] _TopoOutsideColor ("Outside Field", Color) = (0.45, 0.04, 0.5, 0.38)
        [HDR] _TopoLineColor ("Contour Lines", Color) = (0.3, 1.4, 1.1, 0.95)
        _TopoSpacing ("Contour Spacing", Float) = 10
        _TopoLineWidth ("Contour Width", Float) = 1.25
        _TopoRange ("Visible Field Range", Float) = 36
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
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            float4 _TopoInsideColor;
            float4 _TopoOutsideColor;
            float4 _TopoLineColor;
            float _TopoSpacing;
            float _TopoLineWidth;
            float _TopoRange;

            #define NOW_SDF_CUSTOM_FINAL_SHADE NowSdfTopographicShadeV2
            #include "../NowSdfShaderV2.cginc"

            float4 NowSdfTopographicShadeV2(
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
                float spacing = max(_TopoSpacing, 0.0001);
                float range = max(_TopoRange, spacing);
                float field = saturate(1.0 - abs(signedDistance) / range);
                float nearest = abs(frac(signedDistance / spacing + 0.5) - 0.5) * spacing;
                float contourLine = 1.0 - smoothstep(
                    max(_TopoLineWidth - pixelWidth, 0.0),
                    _TopoLineWidth + pixelWidth,
                    nearest);

                float outside = step(0.0, signedDistance);
                float4 baseColor = lerp(_TopoInsideColor, _TopoOutsideColor, outside);
                baseColor.rgb *= lerp(1.0, saturate(fill.rgb), coverage * 0.25);
                baseColor.a *= field * tint.a * lerp(0.72, 1.0, coverage);

                float4 lineColor = _TopoLineColor;
                lineColor.a *= contourLine * field * tint.a;
                return NowSdfAlphaOverV2(baseColor, lineColor);
            }
            ENDCG
        }
    }
}
