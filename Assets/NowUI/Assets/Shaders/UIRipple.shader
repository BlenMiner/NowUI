Shader "NowUI/UI Ripple"
{
    Properties
    {
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
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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
                o.color = v.color;
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

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                float2 centered = (rawUV - 0.5) * rect.zw;
                float shapeDist = sdRoundedBox(centered, rect.zw * 0.5, i.radius);
                float shapeDelta = max(fwidth(shapeDist), 0.0001);
                float shapeAlpha = 1.0 - smoothstep(0.0, shapeDelta, shapeDist);

                float2 screenPos = float2(pos.x, -pos.y);
                float circleDist = length(screenPos - i.extras.xy) - i.extras.w;
                float circleDelta = max(fwidth(circleDist), 0.0001);
                float circleAlpha = 1.0 - smoothstep(-circleDelta, circleDelta, circleDist);

                float alpha = i.color.a * shapeAlpha * circleAlpha;
                fixed4 col;
                col.rgb = i.color.rgb * alpha;
                col.a = alpha;
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
