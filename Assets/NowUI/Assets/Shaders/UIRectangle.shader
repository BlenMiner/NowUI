Shader "NowUI/UI Rectangle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv = i.uv;
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

                fixed4 col = tex2D(_MainTex, i.uv) * color;

                // For simplicity, convert UV to pixel coordinates
                float2 position = (uv - 0.5) * size;
                float2 halfSize = size * 0.5;

                // Signed distance field calculation
                float dist = sdRoundedBox(position, halfSize, rad);
                float delta = fwidth(dist);

                // Calculate the different masks based on the SDF
                float graphicAlpha = 1 - smoothstep(-delta - blur, 0, dist);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outline - delta, - outline, dist);

                col = lerp(col, i.outlineColor, outlineAlpha);

                clip(col.a - 0.01);
                col.a *= graphicAlpha;

                return col;
            }
            ENDCG
        }
    }
}
