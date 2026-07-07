Shader "NowUI/UI Rectangle UGUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
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
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
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
                float4 uiMask : TEXCOORD6;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float3 radiusXYZ : TEXCOORD4;
                float4 outlineColor : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

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

                o.uv = float4(TRANSFORM_TEX(v.uv.xy, _MainTex), v.uv.zw);
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
                float2 rawUV = float2(i.uv.w, i.extras.z);
                float2 pos = rect.xy + rawUV * rect.zw;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                float4 rad = float4(i.radiusXYZ, i.uv.z);
                float blur = i.extras.x;
                float outline = i.extras.y;
                half4 graphic = tex2D(_MainTex, i.uv.xy) * i.color;

                float2 position = (rawUV - 0.5) * rect.zw;
                float2 halfSize = rect.zw * 0.5;
                float dist = sdRoundedBox(position, halfSize, rad);
                float delta = max(length(float2(ddx(dist), ddy(dist))), 0.0001);
                // Half-pixel AA band centered on the true edge: alpha crosses
                // 0.5 exactly at dist == 0, so shapes neither grow nor halo.
                float aa = 0.5 * delta;
                float graphicAlpha = 1 - smoothstep(-aa, aa + max(blur, 0), dist);

                // An outline thinner than one AA width would sit entirely inside
                // the edge fade and render as a washed-out sliver; draw at least
                // one AA width of it so thin outlines stay solid. The inner
                // transition centers on -outlineWidth so the ring renders at its
                // requested thickness instead of one AA width fatter.
                float outlineWidth = max(outline, delta);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outlineWidth - aa, -outlineWidth + aa, dist);

                // Premultiplied compositing avoids color leaking through partially
                // transparent pixels while keeping the existing inside-outline
                // behavior.
                float outlineCoverage = i.outlineColor.a * outlineAlpha * graphicAlpha;
                float fillCoverage = graphic.a * graphicAlpha;

                half4 col;
                col.rgb = i.outlineColor.rgb * outlineCoverage
                    + graphic.rgb * fillCoverage * (1 - outlineCoverage);
                col.a = outlineCoverage + fillCoverage * (1 - outlineCoverage);

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
