Shader "NowUI/UI Bezier"
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
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
                float4 cp01    : TEXCOORD1; // p0.xy, p1.xy
                float4 cp23    : TEXCOORD2; // p2.xy, p3.xy
                float4 color   : TEXCOORD3;
                float4 unused1 : TEXCOORD4;
                float4 params  : TEXCOORD5; // halfWidth, aaWidth, t, 0
                float4 mask    : TEXCOORD6;
                float4 pixel   : TEXCOORD7; // UI-space position (xy)
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 cp01   : TEXCOORD1;
                float4 cp23   : TEXCOORD2;
                float4 color  : TEXCOORD3;
                float4 params : TEXCOORD5;
                float4 mask   : TEXCOORD6;
                float2 pixel  : TEXCOORD7;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.cp01 = v.cp01;
                o.cp23 = v.cp23;
                o.color = v.color;
                o.params = v.params;
                o.mask = v.mask;
                o.pixel = v.pixel.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p0 = i.cp01.xy;
                float2 p1 = i.cp01.zw;
                float2 p2 = i.cp23.xy;
                float2 p3 = i.cp23.zw;

                // Power basis: B(t) = a*t^3 + b*t^2 + c*t + d
                float2 d = p0;
                float2 c = 3.0 * (p1 - p0);
                float2 b = 3.0 * (p0 - 2.0 * p1 + p2);
                float2 a = p3 - 3.0 * p2 + 3.0 * p1 - p0;

                float halfWidth = i.params.x;
                float aaWidth   = i.params.y;
                float t = i.params.z;
                float2 pixel = i.pixel;

                // Newton iteration on f(t) = dot(B(t) - pixel, B'(t)) = 0 to find
                // the closest point on the true cubic. The vertex-supplied t is a
                // good initial guess (interpolated along the segment quad).
                [unroll]
                for (int k = 0; k < 4; ++k)
                {
                    float t2 = t * t;
                    float2 Bt  = a * t2 * t + b * t2 + c * t + d;
                    float2 B1  = 3.0 * a * t2 + 2.0 * b * t + c;
                    float2 B2  = 6.0 * a * t + 2.0 * b;
                    float2 diff = Bt - pixel;
                    float f = dot(diff, B1);
                    float fp = dot(B1, B1) + dot(diff, B2);
                    t -= f / (fp + 1e-5);
                    t = clamp(t, 0.0, 1.0);
                }

                float2 closest = ((a * t + b) * t + c) * t + d;
                float dist = length(closest - pixel);

                // Solid core up to (halfWidth - aaWidth), fade out over the AA band.
                float coverage = saturate((halfWidth + aaWidth - dist) / (2.0 * aaWidth));

                // Mask clip (UI space, y-down): mask = (x, y, width, height).
                float4 mask = i.mask;
                clip(min(min(pixel.x - mask.x, mask.x + mask.z - pixel.x),
                         min(pixel.y - mask.y, mask.y + mask.w - pixel.y)));

                clip(coverage - 0.001);

                float alpha = i.color.a * coverage;
                fixed4 col;
                col.rgb = i.color.rgb * alpha;
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
}
