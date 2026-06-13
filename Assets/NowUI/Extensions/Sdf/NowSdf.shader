Shader "NowUI/SDF Scene"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _NowCanvasLayout ("Now Canvas Layout", Float) = 0
        _SdfShapeCount ("Shape Count", Float) = 0
        _SdfLayerCount ("Layer Count", Float) = 0
        _SdfFeather ("Feather", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _UIMaskSoftnessX ("UI Mask Softness X", Float) = 1
        [HideInInspector] _UIMaskSoftnessY ("UI Mask Softness Y", Float) = 1
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
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
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #define NOW_SDF_MAX_SHAPES 64
            #define NOW_SDF_MAX_LAYERS 16

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 canvasColor : COLOR;
                float4 uv : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 data2 : TEXCOORD2;
                float4 data3 : TEXCOORD3;
                float4 data4 : TEXCOORD4;
                float4 data5 : TEXCOORD5;
                float4 data6 : TEXCOORD6;
                float4 data7 : TEXCOORD7;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uiMask : TEXCOORD6;
                float2 rawUV : TEXCOORD0;
                float4 rect : TEXCOORD1;
                float4 mask : TEXCOORD2;
                float4 tint : TEXCOORD3;
            };

            sampler2D _MainTex;
            float _NowCanvasLayout;
            float _SdfShapeCount;
            float _SdfLayerCount;
            float _SdfFeather;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            float4 _SdfData0[NOW_SDF_MAX_SHAPES];
            float4 _SdfData1[NOW_SDF_MAX_SHAPES];
            float4 _SdfData2[NOW_SDF_MAX_SHAPES];
            float4 _SdfShapeMeta[NOW_SDF_MAX_SHAPES];
            float4 _SdfColors[NOW_SDF_MAX_SHAPES];
            float4 _SdfUvs[NOW_SDF_MAX_SHAPES];
            float4 _SdfLayerData0[NOW_SDF_MAX_LAYERS];
            float4 _SdfLayerData1[NOW_SDF_MAX_LAYERS];

            float sdBox(float2 p, float2 b)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            float sdRoundBox(float2 p, float2 b, float4 r)
            {
                float radius;

                if (p.x < 0.0)
                    radius = p.y < 0.0 ? r.x : r.w;
                else
                    radius = p.y < 0.0 ? r.y : r.z;

                radius = min(radius, min(b.x, b.y));
                float2 q = abs(p) - b + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            float sdEllipse(float2 p, float2 radius)
            {
                radius = max(radius, 0.0001);
                return (length(p / radius) - 1.0) * min(radius.x, radius.y);
            }

            float sdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                return length(pa - ba * h) - r;
            }

            float shapeDistance(float type, float4 data1, float4 data2, float2 scenePos)
            {
                if (type < 0.5)
                    return length(scenePos - data1.xy) - data1.z;

                if (type < 1.5)
                    return sdBox(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001));

                if (type < 2.5)
                    return sdRoundBox(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001), data2);

                if (type < 3.5)
                    return sdEllipse(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001));

                return sdCapsule(scenePos, data1.xy, data1.zw, data2.x);
            }

            float2 shapeUv(float type, float4 data1, float4 data2, float2 scenePos)
            {
                float2 minPoint;
                float2 maxPoint;

                if (type < 0.5)
                {
                    minPoint = data1.xy - data1.zz;
                    maxPoint = data1.xy + data1.zz;
                }
                else if (type < 3.5)
                {
                    float2 halfSize = data1.zw * 0.5;
                    minPoint = data1.xy - halfSize;
                    maxPoint = data1.xy + halfSize;
                }
                else
                {
                    minPoint = min(data1.xy, data1.zw) - data2.xx;
                    maxPoint = max(data1.xy, data1.zw) + data2.xx;
                }

                return saturate((scenePos - minPoint) / max(maxPoint - minPoint, 0.0001));
            }

            float4 shapeFill(int index, float type, float4 data1, float4 data2, float2 scenePos, float4 tint)
            {
                float4 color = _SdfColors[index] * tint;

                if (_SdfShapeMeta[index].y < 0.5)
                    return color;

                float2 uv = shapeUv(type, data1, data2, scenePos);
                float4 uvRect = _SdfUvs[index];
                uv = uvRect.xy + uv * uvRect.zw;
                return tex2D(_MainTex, uv) * color;
            }

            void combine(inout float dist, inout float4 fill, float shapeDist, float4 nextFill, float operation, float smoothing)
            {
                if (operation < 0.5)
                {
                    if (shapeDist < dist)
                    {
                        dist = shapeDist;
                        fill = nextFill;
                    }

                    return;
                }

                if (operation < 1.5)
                {
                    dist = max(dist, -shapeDist);
                    return;
                }

                if (operation < 2.5)
                {
                    if (shapeDist > dist)
                        fill = nextFill;

                    dist = max(dist, shapeDist);
                    return;
                }

                smoothing = max(smoothing, 0.0001);

                if (operation < 3.5)
                {
                    float h = saturate(0.5 + 0.5 * (shapeDist - dist) / smoothing);
                    dist = lerp(shapeDist, dist, h) - smoothing * h * (1.0 - h);
                    fill = lerp(nextFill, fill, h);
                    return;
                }

                if (operation < 4.5)
                {
                    float h = saturate(0.5 - 0.5 * (shapeDist + dist) / smoothing);
                    dist = lerp(dist, -shapeDist, h) + smoothing * h * (1.0 - h);
                    return;
                }

                {
                    float h = saturate(0.5 - 0.5 * (shapeDist - dist) / smoothing);
                    dist = lerp(shapeDist, dist, h) + smoothing * h * (1.0 - h);
                    fill = lerp(nextFill, fill, h);
                }
            }

            void evalGraph(float graphId, float2 scenePos, float4 tint, out float dist, out float4 fill)
            {
                int count = min((int)_SdfShapeCount, NOW_SDF_MAX_SHAPES);
                bool found = false;
                dist = 100000.0;
                fill = 0.0;

                for (int n = 0; n < NOW_SDF_MAX_SHAPES; ++n)
                {
                    if (n >= count)
                        break;

                    if (abs(_SdfShapeMeta[n].x - graphId) > 0.5)
                        continue;

                    float4 data0 = _SdfData0[n];
                    float4 data1 = _SdfData1[n];
                    float4 data2 = _SdfData2[n];
                    float shapeDist = shapeDistance(data0.x, data1, data2, scenePos);
                    float4 nextFill = shapeFill(n, data0.x, data1, data2, scenePos, tint);

                    if (!found)
                    {
                        dist = shapeDist;
                        fill = nextFill;
                        found = true;
                    }
                    else
                    {
                        combine(dist, fill, shapeDist, nextFill, data0.y, data0.z);
                    }
                }
            }

            void evalLayer(int index, float2 scenePos, float4 tint, out float dist, out float4 fill)
            {
                float4 layer0 = _SdfLayerData0[index];
                float4 layer1 = _SdfLayerData1[index];

                if (layer0.w < 0.5)
                {
                    evalGraph(layer0.x, scenePos, tint, dist, fill);
                    return;
                }

                float aDist;
                float bDist;
                float4 aFill;
                float4 bFill;
                evalGraph(layer0.x, scenePos, tint, aDist, aFill);
                evalGraph(layer1.x, scenePos, tint, bDist, bFill);
                float t = saturate(layer1.y);
                dist = lerp(aDist, bDist, t);
                fill = lerp(aFill, bFill, t);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                float isCanvas = step(0.5, _NowCanvasLayout);
                o.rawUV = lerp(v.data7.xy, v.uv.xy, isCanvas);
                o.rect = v.rect;
                o.mask = lerp(v.data6, v.data2, isCanvas);
                o.tint = lerp(v.data3, v.canvasColor, isCanvas);

                float2 pixelSize = o.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.uiMask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 scenePos = i.rawUV * i.rect.zw;
                float2 meshPos = i.rect.xy + scenePos;
                float4 mask = i.mask;

                clip(min(
                    min(meshPos.x - mask.x, (mask.x + mask.z) - meshPos.x),
                    min(-meshPos.y - mask.y, (mask.y + mask.w) + meshPos.y)
                ));

                int layerCount = min((int)_SdfLayerCount, NOW_SDF_MAX_LAYERS);
                float dist = 100000.0;
                float4 fill = 0.0;
                bool found = false;

                for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
                {
                    if (layer >= layerCount)
                        break;

                    float layerDist;
                    float4 layerFill;
                    evalLayer(layer, scenePos, i.tint, layerDist, layerFill);

                    if (!found)
                    {
                        dist = layerDist;
                        fill = layerFill;
                        found = true;
                    }
                    else
                    {
                        combine(dist, fill, layerDist, layerFill, _SdfLayerData0[layer].y, _SdfLayerData0[layer].z);
                    }
                }

                float pixelWidth = max(fwidth(dist), 0.0001);
                float edge = pixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
                float coverage = smoothstep(edge, -edge, dist);
                fixed4 col = fill;
                col.a *= coverage;

                #ifdef UNITY_UI_CLIP_RECT
                float2 uiMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
                col.a *= uiMask.x * uiMask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
