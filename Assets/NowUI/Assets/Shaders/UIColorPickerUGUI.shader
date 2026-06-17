Shader "NowUI/Color Picker UGUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Mode ("Mode", Float) = 0
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
            };

            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float _Mode;

            float3 HsvToRgb(float h, float s, float v)
            {
                float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(float3(h, h, h) + k.xyz) * 6.0 - k.www);
                return v * lerp(k.xxx, saturate(p - k.xxx), s);
            }

            float3 Checker(float2 rawUV, float2 size)
            {
                float2 pixel = rawUV * size;
                float checker = fmod(floor(pixel.x / 5.0) + floor(pixel.y / 5.0), 2.0);
                return lerp(float3(0.88, 0.90, 0.93), float3(0.68, 0.72, 0.78), checker);
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
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = saturate(float2(i.uv.w, i.extras.z));
                float2 pos = rect.xy + rawUV * rect.zw;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                int mode = (int)(_Mode + 0.5);
                float3 rgb;

                if (mode == 1)
                {
                    rgb = HsvToRgb(1.0 - rawUV.y, 1.0, 1.0);
                }
                else if (mode == 2)
                {
                    float alpha = rawUV.x;
                    rgb = lerp(Checker(rawUV, rect.zw), i.color.rgb, alpha);
                }
                else
                {
                    rgb = HsvToRgb(i.color.r, rawUV.x, rawUV.y);
                }

                fixed4 col = fixed4(rgb, 1.0);

                #ifdef UNITY_UI_CLIP_RECT
                float2 uiMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
                col.a *= uiMask.x * uiMask.y;
                col.rgb *= col.a;
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
