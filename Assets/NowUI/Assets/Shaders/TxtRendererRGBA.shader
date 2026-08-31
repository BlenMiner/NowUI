Shader "NowUI/Text Renderer RGBA"
{
    Properties
    {
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
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
                float4 color : TEXCOORD2;
                float4 mask : TEXCOORD3;
                float4 rawUV : TEXCOORD4;
                float4 gradientPayload : TEXCOORD5;
                float gradientEncodedRamp : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rect = v.rect;
                o.color = NowUIColorToWorkingSpace(v.color);
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                o.gradientPayload = float4(v.radius.xyz, v.extras.z);
                o.gradientEncodedRamp = v.extras.w;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 pos = i.rect.xy + i.rawUV * i.rect.zw;
                float4 mask = i.mask;
                float2 uiPosition = float2(pos.x, -pos.y);

                NowUIClipLegacyRect(uiPosition, mask);

                float4 col;

                if (i.gradientEncodedRamp > 0.0)
                {
                    float4 glyph = tex2D(_MainTex, i.uv);
                    float4 fillColor =
                        NowUITextGradientSample(uiPosition, i.gradientPayload, i.gradientEncodedRamp) *
                        i.color;
                    col = float4(fillColor.rgb, glyph.a * fillColor.a);
                }
                else
                {
                    // Intrinsically colored glyphs keep their RGB; solid text color supplies opacity.
                    float4 glyph = tex2D(_MainTex, i.uv);
                    float3 originalRgb = glyph.a > 0.0
                        ? saturate(glyph.rgb / glyph.a)
                        : float3(0.0, 0.0, 0.0);
                    col = float4(originalRgb, glyph.a * i.color.a);
                }

                col.a *= NowUIMaskCoverage(uiPosition);
                clip(col.a - 0.01);
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
}
