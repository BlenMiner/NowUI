Shader "NowUI/UI Ripple"
{
    Properties
    {
        [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
        [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
        [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
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
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 rect : TEXCOORD0;
                float4 radius : TEXCOORD1;
                float4 color : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float4 mask : TEXCOORD4;
                float4 rawUV : TEXCOORD5;
            };

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
                o.rect = v.rect;
                o.radius = v.radius;
                o.color = NowUIColorToWorkingSpace(v.color);
                o.extras = v.extras;
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = i.rawUV.xy;
                float2 pos = rect.xy + rawUV * rect.zw;
                float2 uiPosition = float2(pos.x, -pos.y);

                NowUIClipLegacyRect(uiPosition, mask);

                float2 centered = (rawUV - 0.5) * rect.zw;
                float shapeDist = sdRoundedBox(centered, rect.zw * 0.5, i.radius);
                float shapeDelta = max(length(float2(ddx(shapeDist), ddy(shapeDist))), 0.0001);
                float shapeAlpha = 1.0 - smoothstep(-0.5 * shapeDelta, 0.5 * shapeDelta, shapeDist);

                float circleDist = length(uiPosition - i.extras.xy) - i.extras.w;
                float circleDelta = max(length(float2(ddx(circleDist), ddy(circleDist))), 0.0001);
                float circleAlpha = 1.0 - smoothstep(-0.5 * circleDelta, 0.5 * circleDelta, circleDist);

                float alpha = i.color.a * shapeAlpha * circleAlpha;
                fixed4 col;
                col.rgb = i.color.rgb * alpha;
                col.a = alpha;
                col *= NowUIMaskCoverage(uiPosition);
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
