Shader "NowUI/Text Renderer UGUI"
{
    Properties
    {
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
        [HideInInspector] _NowUITextSdfEncoding ("Now UI Text SDF Encoding", Float) = 0
        [HideInInspector] _NowUITextOutlineOnlyPass ("Now UI Text Outline-Only Pass", Float) = 1
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "NowUITextGradient.cginc"
            #include "NowUIMask.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float4 outlineColor : TANGENT;
                float3 gradientPayload : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uiMask : TEXCOORD5;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float3 gradientPayload : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _NowUITextSdfEncoding;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
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
                o.outlineColor = v.outlineColor;
                o.gradientPayload = v.gradientPayload;
                return o;
            }

            float median(float r, float g, float b)
            {
                return max(min(r, g), min(max(r, g), b));
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = i.uv.zw;
                float2 pos = rect.xy + rawUV * rect.zw;
                float2 uiPosition = float2(pos.x, -pos.y);

                NowUIClipLegacyRect(uiPosition, mask);

                float outline = i.extras.x;
                float4 msd = tex2D(_MainTex, i.uv.xy);

                // extras.y is the distance-field range in local units; convert it to
                // actual screen pixels so canvas scale / transform scale keeps text crisp.
                float2 gradX = float2(ddx(pos.x), ddy(pos.x));
                float2 gradY = float2(ddx(pos.y), ddy(pos.y));
                float unitsPerPixel = max(0.5 * (length(gradX) + length(gradY)), 1e-5);
                bool outlineOnly = i.extras.y < 0.0;
                float screenPxRange = max(abs(i.extras.y) / unitsPerPixel, 1.0);
                bool packedSdf16 = _NowUITextSdfEncoding > 0.5;
                float sd = packedSdf16
                    ? (msd.r * 256.0 + msd.b) / 257.0
                    : median(msd.r, msd.g, msd.b);

                float screenPxDistance = screenPxRange * (sd - 0.5);
                // MTSDF alpha is the true signed distance. It stays stable far
                // from corners where median RGB is optimized for the fill edge.
                float outlineSd = outline == 0 || packedSdf16 ? sd : msd.a;
                float screenPxDistanceOutline =
                    screenPxRange * (outlineSd - 0.5) + outline / unitsPerPixel;
                float distanceCodeCount = packedSdf16 ? 65535.0 : 255.0;
                float aaWidth = max(1.0, screenPxRange / distanceCodeCount);
                float opacity = clamp(screenPxDistance / aaWidth + 0.5, 0.0, 1.0);
                float outlineOp = clamp(screenPxDistanceOutline / aaWidth + 0.5, 0.0, 1.0);
                float4 fillColor = i.color;

                if (i.extras.w > 0.0)
                {
                    float4 gradientPayload = float4(i.gradientPayload, i.extras.z);
                    fillColor *= NowUITextGradientSample(uiPosition, gradientPayload, i.extras.w);
                }

                float4 color;

                if (outlineOnly)
                {
                    float remainingFill = max(1.0 - opacity, 1e-5);
                    float ringCoverage = saturate((outlineOp - opacity) / remainingFill);
                    color = i.outlineColor;
                    color.a *= ringCoverage;
                }
                else
                {
                    color = outline == 0 ? fillColor : lerp(i.outlineColor, fillColor, outline < 0 ? outlineOp : opacity);
                    color.a *= max(opacity, outlineOp);
                }

                color.a *= NowUIMaskCoverage(uiPosition);

                #ifdef UNITY_UI_CLIP_RECT
                float2 uiMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
                color.a *= uiMask.x * uiMask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
