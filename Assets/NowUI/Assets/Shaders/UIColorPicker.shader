Shader "NowUI/Color Picker"
{
    Properties
    {
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
        _Mode ("Mode", Float) = 0
        _ZTest ("ZTest", Float) = 8
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
        ZTest [_ZTest]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "NowUIColorSpace.cginc"
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 rect : TEXCOORD0;
                float4 color : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 rawUV : TEXCOORD3;
            };

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
                o.rect = v.rect;
                o.color = v.color;
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = saturate(i.rawUV.xy);
                float2 pos = rect.xy + rawUV * rect.zw;
                float2 uiPosition = float2(pos.x, -pos.y);

                NowUIClipLegacyRect(uiPosition, mask);

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

                fixed4 col = fixed4(NowUIColorToWorkingSpace(rgb), 1.0);
                col *= NowUIMaskCoverage(uiPosition);
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
