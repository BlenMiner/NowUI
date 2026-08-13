Shader "NowUI/SDF Scene"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _NowSdfAbiVersion ("Now SDF ABI Version", Float) = 2
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

            #include "NowSdfShaderV2.cginc"
            ENDCG
        }
    }
}
