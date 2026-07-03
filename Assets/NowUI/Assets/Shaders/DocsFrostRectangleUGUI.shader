Shader "NowUI/Examples/Frost Rectangle UGUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise Texture", 2D) = "white" {}
        _FrostTint ("Frost Tint", Color) = (0.78, 0.96, 1, 0.88)
        _FrostEdge ("Frost Edge", Color) = (1, 1, 1, 0.95)
        _FrostAmount ("Frost Amount", Range(0, 1)) = 0.7
        _NoiseScale ("Noise Scale", Float) = 2.8
        _TimeScale ("Time Scale", Float) = 1
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
            float4 _FrostTint;
            float4 _FrostEdge;
            float _FrostAmount;
            float _NoiseScale;
            float _TimeScale;

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
                float2 pos = rect.xy + i.uv.xy * rect.zw;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                float4 radius = float4(i.radiusXYZ, i.uv.z);
                float2 centered = (i.uv.xy - 0.5) * rect.zw;
                float dist = sdRoundedBox(centered, rect.zw * 0.5, radius);
                float delta = length(float2(ddx(dist), ddy(dist)));
                float shapeAlpha = 1 - smoothstep(-delta - i.extras.x, 0, dist);
                float outlineWidth = max(i.extras.y, delta);
                float outlineAlpha = i.extras.y == 0 ? 0 : smoothstep(-outlineWidth - delta, -outlineWidth, dist);

                float2 flow = _Time.y * _TimeScale * float2(0.035, -0.022);
                float2 uv = i.uv.xy * _NoiseScale + flow;
                float4 noise = tex2D(_MainTex, uv);
                float grain = noise.r;
                float veins = tex2D(_MainTex, uv * 1.85 + float2(0.37, 0.11) - flow * 0.7).b;
                float crack = smoothstep(0.54, 0.84, abs(grain - veins) + veins * 0.18);
                float sparkle = smoothstep(0.78, 0.97, grain) * (0.45 + 0.55 * sin(_Time.y * 3.2 + grain * 8.0));

                float amount = saturate(_FrostAmount);
                float3 frost = lerp(_FrostTint.rgb * (0.82 + grain * 0.28), _FrostEdge.rgb, crack * amount);
                frost += sparkle * amount * 0.18;
                frost = lerp(frost, _FrostTint.rgb, 0.18);

                float edge = smoothstep(8.0 + amount * 10.0, 0.0, abs(dist));
                float alpha = lerp(0.42, 0.82, amount) + crack * 0.12 + edge * 0.10;

                fixed4 col;
                col.rgb = frost * i.color.rgb;
                col.a = saturate(alpha * _FrostTint.a * i.color.a);

                float outlineCoverage = i.outlineColor.a * outlineAlpha;
                float coverage = outlineCoverage + col.a * (1 - outlineCoverage);
                if (coverage > 0.0001)
                    col.rgb = (i.outlineColor.rgb * outlineCoverage + col.rgb * col.a * (1 - outlineCoverage)) / coverage;

                col.a = coverage * shapeAlpha;

                #ifdef UNITY_UI_CLIP_RECT
                float2 clipMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
                col.a *= clipMask.x * clipMask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                clip(col.a - 0.01);
                return col;
            }
            ENDCG
        }
    }
}
