Shader "NowUI/Text Renderer"
{
    Properties
    {
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
        [HideInInspector] _NowUITextSdfEncoding ("Now UI Text SDF Encoding", Float) = 0
        [HideInInspector] _NowUITextOutlineOnlyPass ("Now UI Text Outline-Only Pass", Float) = 1
        _MainTex ("Texture", 2D) = "white" {}
        _ZTest ("ZTest", Float) = 8
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
        ZTest [_ZTest]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "NowUIColorSpace.cginc"
            #include "NowUITextGradient.cginc"
            #include "NowUIMask.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _NowUITextSdfEncoding;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rect = v.rect;
                o.radius = v.radius;
                o.color = NowUIColorToWorkingSpace(v.color);
                o.outlineColor = NowUIColorToWorkingSpace(v.outlineColor);
                o.extras = v.extras;
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                return o;
            }

            float median(float r, float g, float b) {
                return max(min(r, g), min(max(r, g), b));
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 rect = i.rect;
                float4 mask = i.mask;

                float2 pos = rect.xy + i.rawUV * rect.zw;
                float2 uiPosition = float2(pos.x, -pos.y);

                // Mask
                NowUIClipLegacyRect(uiPosition, mask);


                float outline = i.extras.x;
                float4 msd = tex2D(_MainTex, i.uv);

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

                // A large RGBA8 field can represent more than one screen pixel
                // per stored distance code. Widen its coverage ramp to at least
                // one code step; packed managed pages retain the normal 1px ramp.
                float distanceCodeCount = packedSdf16 ? 65535.0 : 255.0;
                float aaWidth = max(1.0, screenPxRange / distanceCodeCount);
                float opacity = clamp(screenPxDistance / aaWidth + 0.5, 0.0, 1.0);
                float outlineOp = clamp(screenPxDistanceOutline / aaWidth + 0.5, 0.0, 1.0);

                float4 fillColor = i.color;

                if (i.extras.w > 0.0)
                {
                    float4 gradientPayload = float4(i.radius.xyz, i.extras.z);
                    fillColor *= NowUITextGradientSample(uiPosition, gradientPayload, i.extras.w);
                }

                float4 color;

                if (outlineOnly)
                {
                    // Coverage which, after the fill pass uses source-over, keeps
                    // the opaque union equal to outlineOp without painting the face.
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
                color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }
}
