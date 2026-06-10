Shader "NowUI/Text Renderer"
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

            float median(float r, float g, float b) {
                return max(min(r, g), min(max(r, g), b));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 rect = i.rect;
                float4 mask = i.mask;

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


                float outline = i.extras.x;
                fixed4 msd = tex2D(_MainTex, i.uv);

                // extras.y is the distance-field range in local units; convert it to
                // actual screen pixels so canvas scale / transform scale keeps text crisp.
                float2 gradX = float2(ddx(pos.x), ddy(pos.x));
                float2 gradY = float2(ddx(pos.y), ddy(pos.y));
                float unitsPerPixel = max(0.5 * (length(gradX) + length(gradY)), 1e-5);
                float screenPxRange = max(i.extras.y / unitsPerPixel, 1.0);

                float sd = median(msd.r, msd.g, msd.b);

                float screenPxDistance = screenPxRange * (sd - 0.5);
                float screenPxDistanceOutline = screenPxDistance + outline / unitsPerPixel;

                float opacity = clamp(screenPxDistance + 0.5, 0.0, 1.0);
                float outlineOp = clamp(screenPxDistanceOutline + 0.5, 0.0, 1.0);

                float4 color = outline == 0 ? i.color : lerp(i.outlineColor, i.color, outline < 0 ? outlineOp : opacity);

                color.a *= max(opacity, outlineOp);

                return color;
            }
            ENDCG
        }
    }
}
