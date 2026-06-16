Shader "Hidden/NowUI/GlassBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _NowBlurSourceTex;
            float4 _NowBlurTexelSize;
            float4 _NowBlurSourceScaleOffset;
            float2 _NowBlurDirection;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv * _NowBlurSourceScaleOffset.xy + _NowBlurSourceScaleOffset.zw;
                float2 step = _NowBlurDirection * _NowBlurTexelSize.xy;
                // Bilinear-optimized 17-tap Gaussian, applied separably by C#.
                float4 col = tex2D(_NowBlurSourceTex, uv) * 0.1031526189;

                col += tex2D(_NowBlurSourceTex, uv + step * 1.4765796511) * 0.1910108131;
                col += tex2D(_NowBlurSourceTex, uv - step * 1.4765796511) * 0.1910108131;
                col += tex2D(_NowBlurSourceTex, uv + step * 3.4455295350) * 0.1404289078;
                col += tex2D(_NowBlurSourceTex, uv - step * 3.4455295350) * 0.1404289078;
                col += tex2D(_NowBlurSourceTex, uv + step * 5.4148988458) * 0.0807154625;
                col += tex2D(_NowBlurSourceTex, uv - step * 5.4148988458) * 0.0807154625;
                col += tex2D(_NowBlurSourceTex, uv + step * 7.3849121445) * 0.0362685072;
                col += tex2D(_NowBlurSourceTex, uv - step * 7.3849121445) * 0.0362685072;
                return col;
            }
            ENDCG
        }
    }
}
