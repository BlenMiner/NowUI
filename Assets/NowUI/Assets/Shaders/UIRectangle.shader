Shader "NowUI/UI Rectangle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _NowPremultipliedTexture ("Premultiplied Texture", Float) = 0
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
                float2 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _NowPremultipliedTexture;

            float sdRoundedBox(float2 p, float2 b, float4 r )
            {
                r.xy = (p.x>0.0)?r.xy : r.zw;
                r.x  = (p.y>0.0)?r.x  : r.y;
                float2 q = abs(p)-b+r.x;
                return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rect = v.rect;
                o.radius = v.radius;
                o.color = v.color;
                o.outlineColor = v.outlineColor;
                o.extras = v.extras;
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;

                float2 size = rect.zw;
                float2 pos = rect.xy + i.rawUV * rect.zw;

                // Mask
                clip(min(
                    min(pos.x - mask.x,
                        (mask.x + mask.z) - pos.x
                    ),
                    min(
                        -pos.y - mask.y,
                        (mask.y + mask.w) + pos.y
                    )
                ));

                float4 rad = i.radius;
                float4 color = i.color;
                float4 data = i.extras;
                float blur = data.x;
                float outline = data.y;

                half4 textureSample = tex2D(_MainTex, i.uv);

                // Texture UVs can point into an atlas; the shape SDF must stay in
                // full-quad space so sprites and custom UVs keep the same corners.
                float2 position = (i.rawUV.xy - 0.5) * size;
                float2 halfSize = size * 0.5;

                // Signed distance field calculation
                float dist = sdRoundedBox(position, halfSize, rad);
                float delta = max(length(float2(ddx(dist), ddy(dist))), 0.0001);

                // Half-pixel AA band centered on the true edge: alpha crosses
                // 0.5 exactly at dist == 0, so shapes neither grow nor halo.
                float aa = 0.5 * delta;
                float graphicAlpha = 1 - smoothstep(-aa, aa + max(blur, 0), dist);

                // An outline thinner than one AA width would sit entirely inside
                // the edge fade and render as a washed-out sliver; draw at least
                // one AA width of it so thin outlines stay solid. The inner
                // transition centers on -outlineWidth so the ring renders at its
                // requested thickness instead of one AA width fatter.
                float outlineWidth = max(outline, delta);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outlineWidth - aa, -outlineWidth + aa, dist);

                // Premultiplied compositing avoids color leaking through partially
                // transparent pixels while keeping the existing inside-outline
                // behavior.
                float outlineCoverage = i.outlineColor.a * outlineAlpha * graphicAlpha;
                float fillCoverage = textureSample.a * color.a * graphicAlpha;
                half3 fillColor = _NowPremultipliedTexture > 0.5
                    ? textureSample.rgb * color.rgb * color.a * graphicAlpha
                    : textureSample.rgb * color.rgb * fillCoverage;

                half4 col;
                col.rgb = i.outlineColor.rgb * outlineCoverage
                    + fillColor * (1 - outlineCoverage);
                col.a = outlineCoverage + fillCoverage * (1 - outlineCoverage);
                clip(col.a - 0.001);

                return col;
            }
            ENDCG
        }
    }
}
