Shader "NowUI/UI Glass"
{
    Properties
    {
        _ZTest ("ZTest", Float) = 8
        [HideInInspector] _NowMaterialGlassMode ("Material Glass Mode", Float) = 0
        [HideInInspector] _NowMaterialBackdropTex ("Material Backdrop", 2D) = "black" {}
        [HideInInspector] _NowMaterialGlassSharpBackdropTex ("Material Sharp Backdrop", 2D) = "black" {}
        [HideInInspector] _NowMaterialBackdropUVTransform ("Material Backdrop UV Transform", Vector) = (1, 1, 0, 0)
        [HideInInspector] _NowMaterialGlassUseBackdrop ("Material Use Backdrop", Float) = 0
        [HideInInspector] _NowMaterialGlassUseSceneDepth ("Material Use Scene Depth", Float) = 0
        [HideInInspector] _NowMaterialGlassDepthEpsilon ("Material Depth Epsilon", Float) = 0.02
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
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

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
                float4 screenPos : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 radius : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 outlineColor : TEXCOORD4;
                float4 extras : TEXCOORD5;
                float4 mask : TEXCOORD6;
                float4 rawUV : TEXCOORD7;
                float eyeDepth : TEXCOORD8;
            };

            sampler2D _NowBackdropTex;
            sampler2D _NowGlassSharpBackdropTex;
            float _NowGlassUseBackdrop;
            float4 _NowBackdropUVTransform;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _NowGlassUseSceneDepth;
            float _NowGlassDepthEpsilon;
            float _NowMaterialGlassMode;
            sampler2D _NowMaterialBackdropTex;
            sampler2D _NowMaterialGlassSharpBackdropTex;
            float4 _NowMaterialBackdropUVTransform;
            float _NowMaterialGlassUseBackdrop;
            float _NowMaterialGlassUseSceneDepth;
            float _NowMaterialGlassDepthEpsilon;

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
                o.screenPos = ComputeScreenPos(o.vertex);
                o.rect = v.rect;
                o.radius = v.radius;
                o.color = v.color;
                o.outlineColor = v.outlineColor;
                o.extras = v.extras;
                o.mask = v.mask;
                o.rawUV = v.rawUV;
                COMPUTE_EYEDEPTH(o.eyeDepth);
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

                float2 size = rect.zw;
                float2 position = (rawUV - 0.5) * size;
                float dist = sdRoundedBox(position, size * 0.5, i.radius);
                float delta = length(float2(ddx(dist), ddy(dist)));
                float graphicAlpha = 1 - smoothstep(-delta, 0, dist);

                float outline = i.extras.y;
                float outlineWidth = max(outline, delta);
                float outlineAlpha = outline == 0 ? 0 : smoothstep(-outlineWidth - delta, -outlineWidth, dist);

                float saturation = i.extras.z;
                float brightness = i.extras.w;
                float4 tint = i.color;
                float3 fillRgb = tint.rgb;
                float fillCoverage = tint.a;
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                bool useMaterialBackdrop = _NowMaterialGlassMode > 0.5;
                bool useBackdrop = useMaterialBackdrop
                    ? _NowMaterialGlassUseBackdrop > 0.5
                    : _NowGlassUseBackdrop > 0.5;

                UNITY_BRANCH
                if (useBackdrop)
                {
                    float4 uvTransform = useMaterialBackdrop
                        ? _NowMaterialBackdropUVTransform
                        : _NowBackdropUVTransform;
                    float2 backdropUV = screenUV * uvTransform.xy + uvTransform.zw;
                    float2 clampedBackdropUV = saturate(backdropUV);
                    float4 backdrop = useMaterialBackdrop
                        ? tex2D(_NowMaterialBackdropTex, clampedBackdropUV)
                        : tex2D(_NowBackdropTex, clampedBackdropUV);
                    float useSceneDepth = useMaterialBackdrop
                        ? _NowMaterialGlassUseSceneDepth
                        : _NowGlassUseSceneDepth;
                    float depthEpsilon = useMaterialBackdrop
                        ? _NowMaterialGlassDepthEpsilon
                        : _NowGlassDepthEpsilon;

                    UNITY_BRANCH
                    if (useSceneDepth > 0.5)
                    {
                        float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                        float sceneEyeDepth = LinearEyeDepth(sceneRawDepth);
                        bool foregroundScene = sceneEyeDepth < i.eyeDepth - depthEpsilon;

                        UNITY_BRANCH
                        if (foregroundScene)
                            backdrop = useMaterialBackdrop
                                ? tex2D(_NowMaterialGlassSharpBackdropTex, clampedBackdropUV)
                                : tex2D(_NowGlassSharpBackdropTex, clampedBackdropUV);
                    }

                    float luminance = dot(backdrop.rgb, float3(0.299, 0.587, 0.114));
                    backdrop.rgb = lerp(luminance.xxx, backdrop.rgb, saturation) * brightness;
                    fillRgb = lerp(backdrop.rgb, tint.rgb, tint.a);
                    fillCoverage = 1.0;
                }

                float outlineCoverage = i.outlineColor.a * outlineAlpha;
                float coverage = outlineCoverage + fillCoverage * (1 - outlineCoverage);
                float3 rgb = fillRgb;

                if (coverage > 0.0001)
                    rgb = (i.outlineColor.rgb * outlineCoverage + fillRgb * fillCoverage * (1 - outlineCoverage)) / coverage;

                fixed4 col = fixed4(rgb, coverage * graphicAlpha);
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
