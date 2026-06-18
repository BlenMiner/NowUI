Shader "NowUI/SDF Scene"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _NowCanvasLayout ("Now Canvas Layout", Float) = 0
        _SdfShapeCount ("Shape Count", Float) = 0
        _SdfLayerCount ("Layer Count", Float) = 0
        _SdfFeather ("Feather", Float) = 0
        _SdfOutline ("Outline", Vector) = (0, 0, 0, 0)
        _SdfOutlineColor ("Outline Color", Color) = (0, 0, 0, 0)
        _SdfGlow ("Glow", Vector) = (0, 1, 0, 0)
        _SdfGlowColor ("Glow Color", Color) = (0, 0, 0, 0)
        _SdfShadow ("Shadow", Vector) = (0, 0, 0, 0)
        _SdfShadowColor ("Shadow Color", Color) = (0, 0, 0, 0)
        _SdfInnerShadow ("Inner Shadow", Vector) = (0, 0, 0, 0)
        _SdfInnerShadowColor ("Inner Shadow Color", Color) = (0, 0, 0, 0)
        _SdfEmboss ("Emboss", Vector) = (0, 0, 1, 0)
        _SdfContour ("Contour", Vector) = (1, 0, 0, 0)
        _SdfContourColor ("Contour Color", Color) = (0, 0, 0, 0)
        _SdfContourMask ("Contour Mask", Vector) = (0, 0, 0, 0)
        _SdfWarp ("Warp", Vector) = (0, 1, 0, 0)
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
            float4 _SdfOutline;
            float4 _SdfOutlineColor;
            float4 _SdfGlow;
            float4 _SdfGlowColor;
            float4 _SdfShadow;
            float4 _SdfShadowColor;
            float4 _SdfInnerShadow;
            float4 _SdfInnerShadowColor;
            float4 _SdfEmboss;
            float4 _SdfContour;
            float4 _SdfContourColor;
            float4 _SdfContourMask;
            float4 _SdfWarp;
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

            float median(float r, float g, float b)
            {
                return max(min(r, g), min(max(r, g), b));
            }

            float sdGlyph(float2 scenePos, float4 data1, float4 data2, float4 uvRect)
            {
                float2 size = max(data1.zw, 0.0001);
                float2 halfSize = size * 0.5;
                float2 local = scenePos - data1.xy;
                float2 glyphUv = local / size + 0.5;
                float boundsDist = sdBox(local, halfSize);

                if (glyphUv.x < 0.0 || glyphUv.y < 0.0 || glyphUv.x > 1.0 || glyphUv.y > 1.0)
                    return max(data2.x, 1.0) + max(boundsDist, 0.0);

                float2 atlasUv = uvRect.xy + float2(glyphUv.x, 1.0 - glyphUv.y) * uvRect.zw;
                float4 msd = tex2D(_MainTex, atlasUv);
                return (0.5 - median(msd.r, msd.g, msd.b)) * max(data2.x, 0.0001);
            }

            float shapeDistance(int index, float type, float4 data1, float4 data2, float2 scenePos)
            {
                if (type < 0.5)
                    return length(scenePos - data1.xy) - data1.z;

                if (type < 1.5)
                    return sdBox(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001));

                if (type < 2.5)
                    return sdRoundBox(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001), data2);

                if (type < 3.5)
                    return sdEllipse(scenePos - data1.xy, max(data1.zw * 0.5, 0.0001));

                if (type < 4.5)
                    return sdCapsule(scenePos, data1.xy, data1.zw, data2.x);

                return sdGlyph(scenePos, data1, data2, _SdfUvs[index]);
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

                float2 uv = saturate((scenePos - minPoint) / max(maxPoint - minPoint, 0.0001));
                return float2(uv.x, 1.0 - uv.y);
            }

            float4 shapeFill(int index, float type, float4 data1, float4 data2, float2 scenePos, float4 tint)
            {
                float4 color = _SdfColors[index] * tint;

                if (type > 4.5 || _SdfShapeMeta[index].y < 0.5)
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

            void combineDistance(inout float dist, float shapeDist, float operation, float smoothing)
            {
                if (operation < 0.5)
                {
                    dist = min(dist, shapeDist);
                    return;
                }

                if (operation < 1.5)
                {
                    dist = max(dist, -shapeDist);
                    return;
                }

                if (operation < 2.5)
                {
                    dist = max(dist, shapeDist);
                    return;
                }

                smoothing = max(smoothing, 0.0001);

                if (operation < 3.5)
                {
                    float h = saturate(0.5 + 0.5 * (shapeDist - dist) / smoothing);
                    dist = lerp(shapeDist, dist, h) - smoothing * h * (1.0 - h);
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
                    float shapeDist = shapeDistance(n, data0.x, data1, data2, scenePos);
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

            void evalGraphDistance(float graphId, float2 scenePos, out float dist)
            {
                int count = min((int)_SdfShapeCount, NOW_SDF_MAX_SHAPES);
                bool found = false;
                dist = 100000.0;

                for (int n = 0; n < NOW_SDF_MAX_SHAPES; ++n)
                {
                    if (n >= count)
                        break;

                    if (abs(_SdfShapeMeta[n].x - graphId) > 0.5)
                        continue;

                    float4 data0 = _SdfData0[n];
                    float shapeDist = shapeDistance(n, data0.x, _SdfData1[n], _SdfData2[n], scenePos);

                    if (!found)
                    {
                        dist = shapeDist;
                        found = true;
                    }
                    else
                    {
                        combineDistance(dist, shapeDist, data0.y, data0.z);
                    }
                }
            }

            void evalLayerDistance(int index, float2 scenePos, out float dist)
            {
                float4 layer0 = _SdfLayerData0[index];
                float4 layer1 = _SdfLayerData1[index];

                if (layer0.w < 0.5)
                {
                    evalGraphDistance(layer0.x, scenePos, dist);
                    return;
                }

                float aDist;
                float bDist;
                evalGraphDistance(layer0.x, scenePos, aDist);
                evalGraphDistance(layer1.x, scenePos, bDist);
                dist = lerp(aDist, bDist, saturate(layer1.y));
            }

            void evalScene(float2 scenePos, float4 tint, out float dist, out float4 fill)
            {
                int layerCount = min((int)_SdfLayerCount, NOW_SDF_MAX_LAYERS);
                bool found = false;
                dist = 100000.0;
                fill = 0.0;

                for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
                {
                    if (layer >= layerCount)
                        break;

                    float layerDist;
                    float4 layerFill;
                    evalLayer(layer, scenePos, tint, layerDist, layerFill);

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
            }

            void evalSceneDistance(float2 scenePos, out float dist)
            {
                int layerCount = min((int)_SdfLayerCount, NOW_SDF_MAX_LAYERS);
                bool found = false;
                dist = 100000.0;

                for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
                {
                    if (layer >= layerCount)
                        break;

                    float layerDist;
                    evalLayerDistance(layer, scenePos, layerDist);

                    if (!found)
                    {
                        dist = layerDist;
                        found = true;
                    }
                    else
                    {
                        combineDistance(dist, layerDist, _SdfLayerData0[layer].y, _SdfLayerData0[layer].z);
                    }
                }
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 warpScenePos(float2 scenePos)
            {
                if (_SdfWarp.x <= 0.0)
                    return scenePos;

                float scale = max(_SdfWarp.y, 0.0001);
                float t = _Time.y * _SdfWarp.z + _SdfWarp.w;
                float2 p = scenePos / scale;
                float2 n = float2(noise21(p + t), noise21(p + t + 37.23)) * 2.0 - 1.0;
                return scenePos + n * _SdfWarp.x;
            }

            float4 effectColor(float4 color, float4 tint)
            {
                return color * tint;
            }

            float4 alphaOver(float4 baseColor, float4 topColor)
            {
                float a = topColor.a + baseColor.a * (1.0 - topColor.a);
                float3 rgb = (topColor.rgb * topColor.a + baseColor.rgb * baseColor.a * (1.0 - topColor.a)) / max(a, 0.0001);
                return float4(rgb, a);
            }

            float fieldMetric(float dist, float2 scenePos)
            {
                float scenePixel = max(max(length(ddx(scenePos)), length(ddy(scenePos))), 0.0001);
                float fieldPixel = length(float2(ddx(dist), ddy(dist)));
                return clamp(fieldPixel / scenePixel, 0.35, 3.0);
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
                float2 quadPos = i.rawUV * i.rect.zw;
                float2 scenePosBase = float2(quadPos.x, i.rect.w - quadPos.y);
                float2 scenePos = warpScenePos(scenePosBase);
                float2 meshPos = i.rect.xy + quadPos;
                float4 mask = i.mask;

                clip(min(
                    min(meshPos.x - mask.x, (mask.x + mask.z) - meshPos.x),
                    min(-meshPos.y - mask.y, (mask.y + mask.w) + meshPos.y)
                ));

                float dist = 100000.0;
                float4 fill = 0.0;
                evalScene(scenePos, i.tint, dist, fill);

                float pixelWidth = max(fwidth(dist), 0.0001);
                float edge = pixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
                float metric = fieldMetric(dist, scenePosBase);
                float effectDist = dist / metric;
                float effectPixelWidth = max(pixelWidth / metric, 0.0001);
                float effectEdge = effectPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
                float coverage = smoothstep(edge, -edge, dist);
                float4 col = 0.0;

                if (_SdfShadowColor.a > 0.0)
                {
                    float shadowDist;
                    evalSceneDistance(warpScenePos(scenePosBase - _SdfShadow.xy), shadowDist);
                    float shadowMetric = fieldMetric(shadowDist, scenePosBase);
                    float shadowPixelWidth = max(fwidth(shadowDist) / shadowMetric, 0.0001);
                    float shadowEdge = shadowPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
                    float shadowEffectDist = shadowDist / shadowMetric - _SdfShadow.w;
                    float shadowAlpha = smoothstep(max(_SdfShadow.z, shadowPixelWidth) + shadowEdge, -shadowEdge, shadowEffectDist) * (1.0 - coverage);
                    float4 shadowColor = effectColor(_SdfShadowColor, i.tint);
                    shadowColor.a *= shadowAlpha;
                    col = alphaOver(col, shadowColor);
                }

                if (_SdfGlowColor.a > 0.0 && _SdfGlow.x > 0.0)
                {
                    float glowT = saturate(1.0 - max(effectDist, 0.0) / max(_SdfGlow.x, 0.0001));
                    float glowAlpha = pow(glowT, max(_SdfGlow.y, 0.0001)) * (1.0 - coverage);
                    float4 glowColor = effectColor(_SdfGlowColor, i.tint);
                    glowColor.a *= glowAlpha;
                    col = alphaOver(col, glowColor);
                }

                if (_SdfOutlineColor.a > 0.0 && _SdfOutline.x > 0.0)
                {
                    float outlineAlpha = smoothstep(_SdfOutline.x + _SdfOutline.y + effectEdge, _SdfOutline.x - effectEdge, effectDist) * (1.0 - coverage);
                    float4 outlineColor = effectColor(_SdfOutlineColor, i.tint);
                    outlineColor.a *= outlineAlpha;
                    col = alphaOver(col, outlineColor);
                }

                float4 fillColor = fill;

                if (_SdfEmboss.w > 0.0)
                {
                    float2 grad = float2(ddx(dist), ddy(dist));
                    float2 normal2 = normalize(grad + 0.0001);
                    float2 light = normalize(_SdfEmboss.xy + 0.0001);
                    float band = 1.0 - smoothstep(0.0, max(_SdfEmboss.z, effectPixelWidth), abs(effectDist));
                    float shade = dot(normal2, light) * _SdfEmboss.w * band;
                    fillColor.rgb = saturate(fillColor.rgb + shade);
                }

                fillColor.a *= coverage;
                col = alphaOver(col, fillColor);

                if (_SdfInnerShadowColor.a > 0.0)
                {
                    float innerDist;
                    evalSceneDistance(warpScenePos(scenePosBase - _SdfInnerShadow.xy), innerDist);
                    float innerMetric = fieldMetric(innerDist, scenePosBase);
                    float innerPixelWidth = max(fwidth(innerDist) / innerMetric, 0.0001);
                    float innerEdge = innerPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
                    float innerEffectDist = innerDist / innerMetric + _SdfInnerShadow.w;
                    float innerShape = smoothstep(max(_SdfInnerShadow.z, innerPixelWidth) + innerEdge, -innerEdge, innerEffectDist);
                    float innerAlpha = coverage * (1.0 - innerShape);
                    float4 innerShadowColor = effectColor(_SdfInnerShadowColor, i.tint);
                    innerShadowColor.a *= innerAlpha;
                    col = alphaOver(col, innerShadowColor);
                }

                if (_SdfContourColor.a > 0.0 && _SdfContour.x > 0.0 && _SdfContour.y > 0.0)
                {
                    float spacing = max(_SdfContour.x, 0.0001);
                    float halfWidth = _SdfContour.y * 0.5;
                    float contourDistance = effectDist + _SdfContour.z;
                    float nearest = abs(frac(contourDistance / spacing + 0.5) - 0.5) * spacing;
                    float contourAlpha = smoothstep(halfWidth + effectEdge, halfWidth - effectEdge, nearest);
                    if (_SdfContour.w > 0.0)
                    {
                        float bandIndex = floor(abs(contourDistance / spacing) + 0.5);
                        contourAlpha *= 1.0 - step(_SdfContour.w, bandIndex);
                    }
                    if (_SdfContourMask.z > 0.0)
                    {
                        float maskDist = length(scenePosBase - _SdfContourMask.xy);
                        float maskSoftness = max(_SdfContourMask.w, edge);
                        contourAlpha *= smoothstep(_SdfContourMask.z + maskSoftness, _SdfContourMask.z - edge, maskDist);
                    }
                    float4 contourColor = effectColor(_SdfContourColor, i.tint);
                    contourColor.a *= contourAlpha;
                    col = alphaOver(col, contourColor);
                }

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
