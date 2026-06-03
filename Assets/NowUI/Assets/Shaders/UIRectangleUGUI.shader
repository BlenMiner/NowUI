Shader "NowUI/UI Rectangle UGUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float3 radiusXYZ : NORMAL;
                float4 outlineColor : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 extras : TEXCOORD3;
                float3 radiusXYZ : TEXCOORD4;
                float4 outlineColor : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

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
                o.uv = float4(TRANSFORM_TEX(v.uv.xy, _MainTex), v.uv.zw);
                o.rect = v.rect;
                o.mask = v.mask;
                o.extras = v.extras;
                o.color = v.color;
                o.radiusXYZ = v.radiusXYZ;
                o.outlineColor = v.outlineColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;
                float2 rawUV = i.uv.xy;
                float2 pos = rect.xy + rawUV * rect.zw;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                float4 rad = float4(i.radiusXYZ, i.uv.z);
                float blur = i.extras.x;
                float outline = i.extras.y;
                fixed4 col = tex2D(_MainTex, i.uv.xy) * i.color;

                float2 position = (i.uv.xy - 0.5) * rect.zw;
                float2 halfSize = rect.zw * 0.5;
                float dist = sdRoundedBox(position, halfSize, rad);
                float delta = fwidth(dist);
                float graphicAlpha = 1 - smoothstep(-delta - blur, 0, dist);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outline - delta, -outline, dist);

                col = lerp(col, i.outlineColor, outlineAlpha);

                clip(col.a - 0.01);
                col.a *= graphicAlpha;

                return col;
            }
            ENDCG
        }
    }
}
