Shader "NowUI/UI Glass UGUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _NowUGUIBackdropTex ("Backdrop", 2D) = "black" {}
        [HideInInspector] _NowUGUIBackdropSize ("Backdrop Size", Vector) = (1, 1, 0, 0)
        [HideInInspector] _NowUGUIBackdropOrigin ("Backdrop Origin", Vector) = (0, 0, 0, 0)
        [HideInInspector] _NowUGUIGlassUseBackdrop ("Use UGUI Backdrop", Float) = 0
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
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float3 radiusXYZ : NORMAL;
                float4 outlineColor : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uiMask : TEXCOORD0;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD1;
                float4 rect : TEXCOORD2;
                float4 mask : TEXCOORD3;
                float4 extras : TEXCOORD4;
                float3 radiusXYZ : TEXCOORD5;
                float4 outlineColor : TEXCOORD6;
            };

            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            sampler2D _MainTex;
            sampler2D _NowUGUIBackdropTex;
            float4 _NowUGUIBackdropSize;
            float4 _NowUGUIBackdropOrigin;
            float _NowUGUIGlassUseBackdrop;

            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                float2 pixelSize = o.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.uiMask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                o.uv = v.uv;
                o.rect = v.rect;
                o.mask = v.mask;
                o.extras = v.extras;
                o.color = v.color;
                o.radiusXYZ = v.radiusXYZ;
                o.outlineColor = v.outlineColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = i.uv.xy;
                float2 pos = rect.xy + rawUV * rect.zw;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                float4 rad = float4(i.radiusXYZ, i.uv.z);
                float2 position = (rawUV - 0.5) * rect.zw;
                float dist = sdRoundedBox(position, rect.zw * 0.5, rad);
                float delta = fwidth(dist);
                float graphicAlpha = 1 - smoothstep(-delta, 0, dist);

                float outline = i.extras.y;
                float outlineWidth = max(outline, delta);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outlineWidth - delta, -outlineWidth, dist);
                float outlineCoverage = i.outlineColor.a * outlineAlpha;
                float3 fillRgb = i.color.rgb;
                float fillCoverage = i.color.a;

                UNITY_BRANCH
                if (_NowUGUIGlassUseBackdrop > 0.5)
                {
                    float2 backdropPos = float2(pos.x, -pos.y) - _NowUGUIBackdropOrigin.xy;
                    float backdropY = 1.0 - backdropPos.y / max(_NowUGUIBackdropSize.y, 0.0001);
                    float2 backdropUV = float2(
                        backdropPos.x / max(_NowUGUIBackdropSize.x, 0.0001),
                        backdropY);
                    float4 backdrop = tex2D(_NowUGUIBackdropTex, saturate(backdropUV));
                    float saturation = i.extras.z;
                    float brightness = i.extras.w;
                    float backdropCoverage = saturate(backdrop.a);
                    float hasBackdrop = smoothstep(0.001, 0.02, backdropCoverage);
                    float3 backdropRgb = saturate(backdrop.rgb);
                    float luminance = dot(backdropRgb, float3(0.299, 0.587, 0.114));
                    backdropRgb = lerp(luminance.xxx, backdropRgb, saturation) * brightness;
                    float tintCoverage = saturate(i.color.a);
                    float3 backdropFill = lerp(backdropRgb, i.color.rgb, tintCoverage);
                    fillCoverage = lerp(tintCoverage, 1.0, hasBackdrop);
                    fillRgb = lerp(i.color.rgb, backdropFill, hasBackdrop);
                }

                float coverage = outlineCoverage + fillCoverage * (1 - outlineCoverage);
                float3 rgb = fillRgb;

                if (coverage > 0.0001)
                    rgb = (i.outlineColor.rgb * outlineCoverage + fillRgb * fillCoverage * (1 - outlineCoverage)) / coverage;

                fixed4 col = fixed4(rgb, coverage * graphicAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                float2 uiMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
                col.a *= uiMask.x * uiMask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
