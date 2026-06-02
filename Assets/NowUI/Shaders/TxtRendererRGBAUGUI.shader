Shader "NowUI/Text Renderer RGBA UGUI"
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float4(TRANSFORM_TEX(v.uv.xy, _MainTex), v.uv.zw);
                o.rect = v.rect;
                o.mask = v.mask;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pos = i.rect.xy + i.uv.zw * i.rect.zw;
                float4 mask = i.mask;

                clip(min(
                    min(pos.x - mask.x, (mask.x + mask.z) - pos.x),
                    min(-pos.y - mask.y, (mask.y + mask.w) + pos.y)
                ));

                fixed4 col = tex2D(_MainTex, i.uv.xy) * i.color;
                col.rgb = col.a > 0 ? saturate(col.rgb / col.a) : col.rgb;
                clip(col.a - 0.01);
                return col;
            }
            ENDCG
        }
    }
}
