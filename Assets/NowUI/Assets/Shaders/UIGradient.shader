Shader "NowUI/UI Gradient"
{
    Properties
    {
        _MainTex ("Ramp Atlas", 2D) = "white" {}
        _ZTest ("ZTest", Float) = 8
    }
    SubShader
    {
        Tags
        {
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 packedTint : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 gradient : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 packedTint : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 gradient : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float2 rawUV : TEXCOORD7;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            float2 decodePair8(float packed)
            {
                packed = floor(packed + 0.5);
                float first = floor(packed / 256.0);
                float second = packed - first * 256.0;
                return float2(first, second) / 255.0;
            }

            float applySpread(float t, float spread)
            {
                if (spread < 0.5)
                    return saturate(t);

                if (spread < 1.5)
                    return frac(t);

                return 1.0 - abs(frac(t * 0.5) * 2.0 - 1.0);
            }

            float gradientPosition(float2 uv, float2 size, float4 data, float kind, float circle)
            {
                if (kind < 0.5)
                {
                    float2 direction = data.xy;
                    float directionLength = max(length(direction), 0.0001);
                    direction /= directionLength;
                    float2 local = (uv - 0.5) * size;
                    float extent = max(
                        abs(direction.x) * size.x * 0.5 + abs(direction.y) * size.y * 0.5,
                        0.0001);
                    return (0.5 + dot(local, direction) / (2.0 * extent)) * max(abs(data.z), 0.0001);
                }

                if (kind < 1.5)
                {
                    float2 delta = uv - data.xy;

                    if (circle > 0.5)
                    {
                        float radius = max(abs(data.z) * min(size.x, size.y), 0.0001);
                        return length(delta * size) / radius;
                    }

                    return length(delta / max(abs(data.zw), 0.0001));
                }

                float2 delta = uv - data.xy;
                float turns = atan2(delta.x, -delta.y) / 6.28318530718;
                return frac(turns - data.z) * max(abs(data.w), 0.0001);
            }

            float4 sampleRamp(float t, float encodedRamp)
            {
                float row = floor(encodedRamp);
                float flags = floor(frac(encodedRamp) * 256.0);
                flags = floor(flags / 4.0);
                float spread = fmod(flags, 4.0);
                flags = floor(flags / 8.0);
                float fixedMode = fmod(flags, 2.0);

                t = applySpread(t, spread);
                float x;

                if (fixedMode > 0.5)
                {
                    float index = floor(t * (_MainTex_TexelSize.z - 1.0) + 0.5);
                    x = (index + 0.5) * _MainTex_TexelSize.x;
                }
                else
                {
                    x = lerp(
                        0.5 * _MainTex_TexelSize.x,
                        1.0 - 0.5 * _MainTex_TexelSize.x,
                        t);
                }

                float y = (row + 0.5) * _MainTex_TexelSize.y;
                return tex2D(_MainTex, float2(x, y));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.packedTint = v.packedTint;
                o.rect = v.rect;
                o.radius = v.radius;
                o.gradient = v.gradient;
                o.outlineColor = v.outlineColor;
                o.extras = v.extras;
                o.mask = v.mask;
                o.rawUV = v.rawUV.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pos = i.rect.xy + i.rawUV * i.rect.zw;

                clip(min(
                    min(pos.x - i.mask.x, (i.mask.x + i.mask.z) - pos.x),
                    min(-pos.y - i.mask.y, (i.mask.y + i.mask.w) + pos.y)));

                float2 uiUV = float2(i.rawUV.x, 1.0 - i.rawUV.y);
                float2 position = (i.rawUV - 0.5) * i.rect.zw;
                float dist = sdRoundedBox(position, i.rect.zw * 0.5, i.radius);
                float delta = max(length(float2(ddx(dist), ddy(dist))), 0.0001);
                float aa = 0.5 * delta;
                float graphicAlpha = 1.0 - smoothstep(-aa, aa + max(i.extras.x, 0.0), dist);

                float outlineWidth = max(i.extras.y, delta);
                float outlineAlpha = i.extras.y == 0.0
                    ? 0.0
                    : smoothstep(-outlineWidth - aa, -outlineWidth + aa, dist);

                float flags = floor(frac(i.extras.w) * 256.0);
                float kind = fmod(flags, 4.0);
                float circle = fmod(floor(flags / 16.0), 2.0);
                float t = gradientPosition(uiUV, i.rect.zw, i.gradient, kind, circle);
                float4 ramp = sampleRamp(t, i.extras.w);
                float2 rg = decodePair8(i.packedTint.x);
                float2 ba = decodePair8(i.packedTint.y);
                float4 tint = float4(rg.x, rg.y, ba.x, ba.y);

                float outlineCoverage = i.outlineColor.a * outlineAlpha * graphicAlpha;
                float fillCoverage = ramp.a * tint.a * graphicAlpha;
                half3 fillColor = ramp.rgb * tint.rgb * fillCoverage;

                half4 col;
                col.rgb = i.outlineColor.rgb * outlineCoverage + fillColor * (1.0 - outlineCoverage);
                col.a = outlineCoverage + fillCoverage * (1.0 - outlineCoverage);
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
