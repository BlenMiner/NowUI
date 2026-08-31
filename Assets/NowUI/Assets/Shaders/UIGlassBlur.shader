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
            // UnityCG's vert_img already carries the stereo instance/output plumbing.
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

        // Texture-array blur is deliberately serialized one slice at a time.
        // C# binds the destination slice explicitly, so this pass must not emit
        // SV_RenderTargetArrayIndex like a stereo geometry pass would.
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex NowGlassBlurArrayVertex
            #pragma fragment NowGlassBlurArrayFragment

            #include "UnityCG.cginc"

            struct NowGlassBlurArrayInput
            {
                uint vertexID : SV_VertexID;
            };

            struct NowGlassBlurArrayVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            UNITY_DECLARE_TEX2DARRAY(_NowBlurSourceArrayTex);
            float4 _NowBlurTexelSize;
            float4 _NowBlurSourceScaleOffset;
            float2 _NowBlurDirection;
            float _NowBlurSourceSlice;

            NowGlassBlurArrayVaryings NowGlassBlurArrayVertex(NowGlassBlurArrayInput input)
            {
                NowGlassBlurArrayVaryings output;
                float2 positionUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.position = float4(positionUV * 2.0 - 1.0, 0.0, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.uv = float2(positionUV.x, 1.0 - positionUV.y);
                #else
                output.uv = positionUV;
                #endif

                return output;
            }

            float4 SampleArray(float2 uv)
            {
                return UNITY_SAMPLE_TEX2DARRAY(
                    _NowBlurSourceArrayTex,
                    float3(uv, _NowBlurSourceSlice));
            }

            fixed4 NowGlassBlurArrayFragment(NowGlassBlurArrayVaryings input) : SV_Target
            {
                float2 uv = input.uv * _NowBlurSourceScaleOffset.xy + _NowBlurSourceScaleOffset.zw;
                float2 step = _NowBlurDirection * _NowBlurTexelSize.xy;
                float4 col = SampleArray(uv) * 0.1031526189;

                col += SampleArray(uv + step * 1.4765796511) * 0.1910108131;
                col += SampleArray(uv - step * 1.4765796511) * 0.1910108131;
                col += SampleArray(uv + step * 3.4455295350) * 0.1404289078;
                col += SampleArray(uv - step * 3.4455295350) * 0.1404289078;
                col += SampleArray(uv + step * 5.4148988458) * 0.0807154625;
                col += SampleArray(uv - step * 5.4148988458) * 0.0807154625;
                col += SampleArray(uv + step * 7.3849121445) * 0.0362685072;
                col += SampleArray(uv - step * 7.3849121445) * 0.0362685072;
                return col;
            }
            ENDCG
        }

        // XR eye targets can be both texture arrays and multisampled. Resolve
        // them explicitly before any ordinary array sampler sees the image.
        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma require msaatex
            #pragma vertex NowGlassBlurArrayVertex
            #pragma fragment NowGlassResolveMSAAArrayFragment

            #include "UnityCG.cginc"

            struct NowGlassBlurArrayInput
            {
                uint vertexID : SV_VertexID;
            };

            struct NowGlassBlurArrayVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Texture2DMSArray<float4> _NowBlurSourceMSAAArrayTex;
            float4 _NowBlurSourceScaleOffset;
            float _NowBlurSourceSlice;

            NowGlassBlurArrayVaryings NowGlassBlurArrayVertex(NowGlassBlurArrayInput input)
            {
                NowGlassBlurArrayVaryings output;
                float2 positionUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.position = float4(positionUV * 2.0 - 1.0, 0.0, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.uv = float2(positionUV.x, 1.0 - positionUV.y);
                #else
                output.uv = positionUV;
                #endif

                return output;
            }

            fixed4 NowGlassResolveMSAAArrayFragment(NowGlassBlurArrayVaryings input) : SV_Target
            {
                float2 sourceUV = saturate(
                    input.uv * _NowBlurSourceScaleOffset.xy +
                    _NowBlurSourceScaleOffset.zw);
                uint sourceWidth;
                uint sourceHeight;
                uint sourceSlices;
                uint sourceSamples;
                _NowBlurSourceMSAAArrayTex.GetDimensions(
                    sourceWidth,
                    sourceHeight,
                    sourceSlices,
                    sourceSamples);
                int2 sourceSize = max(int2(1, 1), int2(sourceWidth, sourceHeight));
                int2 sourcePixel = min(int2(sourceUV * sourceSize), sourceSize - 1);
                int sampleCount = max(1, (int)sourceSamples);
                float4 color = 0.0;

                [loop]
                for (int sampleIndex = 0; sampleIndex < sampleCount; ++sampleIndex)
                {
                    color += _NowBlurSourceMSAAArrayTex.Load(
                        int3(sourcePixel, (int)_NowBlurSourceSlice),
                        sampleIndex);
                }

                return color / sampleCount;
            }
            ENDCG
        }

        // Multi-pass XR uses one flat multisampled target per eye. Resolve that
        // Texture2DMS explicitly instead of sending it through a sampler2D blit.
        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma require msaatex
            #pragma vertex NowGlassBlurMSAAVertex
            #pragma fragment NowGlassResolveMSAAFragment

            #include "UnityCG.cginc"

            struct NowGlassBlurMSAAInput
            {
                uint vertexID : SV_VertexID;
            };

            struct NowGlassBlurMSAAVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Texture2DMS<float4> _NowBlurSourceMSAATex;
            float4 _NowBlurSourceScaleOffset;

            NowGlassBlurMSAAVaryings NowGlassBlurMSAAVertex(NowGlassBlurMSAAInput input)
            {
                NowGlassBlurMSAAVaryings output;
                float2 positionUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.position = float4(positionUV * 2.0 - 1.0, 0.0, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.uv = float2(positionUV.x, 1.0 - positionUV.y);
                #else
                output.uv = positionUV;
                #endif

                return output;
            }

            fixed4 NowGlassResolveMSAAFragment(NowGlassBlurMSAAVaryings input) : SV_Target
            {
                float2 sourceUV = saturate(
                    input.uv * _NowBlurSourceScaleOffset.xy +
                    _NowBlurSourceScaleOffset.zw);
                uint sourceWidth;
                uint sourceHeight;
                uint sourceSamples;
                _NowBlurSourceMSAATex.GetDimensions(
                    sourceWidth,
                    sourceHeight,
                    sourceSamples);
                int2 sourceSize = max(int2(1, 1), int2(sourceWidth, sourceHeight));
                int2 sourcePixel = min(int2(sourceUV * sourceSize), sourceSize - 1);
                int sampleCount = max(1, (int)sourceSamples);
                float4 color = 0.0;

                [loop]
                for (int sampleIndex = 0; sampleIndex < sampleCount; ++sampleIndex)
                    color += _NowBlurSourceMSAATex.Load(sourcePixel, sampleIndex);

                return color / sampleCount;
            }
            ENDCG
        }
    }
}
