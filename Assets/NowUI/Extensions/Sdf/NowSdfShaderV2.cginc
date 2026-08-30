#ifndef NOW_SDF_SHADER_V2_INCLUDED
#define NOW_SDF_SHADER_V2_INCLUDED

// Versioned shader implementation for NowSdf.MaterialAbiVersion == 2.
// Define NOW_SDF_CUSTOM_FINAL_SHADE before including this file to replace
// final straight-alpha shading while retaining NowUI clipping and mask output.

#include "UnityCG.cginc"
#include "UnityUI.cginc"
#include "../../Assets/Shaders/NowUIMask.cginc"

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
    float4 sceneMapping : TEXCOORD4;
};

sampler2D _MainTex;
float _NowCanvasLayout;
float _SdfShapeCount;
float _SdfLayerCount;
float _SdfFeather;
float _SdfTextEffectLimit;
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
float _SdfMaskOutput;
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
        radius = p.y < 0.0 ? r.z : r.w;
    else
        radius = p.y < 0.0 ? r.x : r.y;

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

// Chamfered box and triangle SDFs adapted from Inigo Quilez's 2D distance
// functions (MIT). https://iquilezles.org/articles/distfunctions2d/
// Copyright © 2019 Inigo Quilez. See THIRD_PARTY_LICENSES.md.
float NowSdfChamferedBoxDistanceV2(float2 p, float2 halfSize, float chamfer)
{
    halfSize = max(halfSize, 0.0001);
    chamfer = clamp(chamfer, 0.0, min(halfSize.x, halfSize.y));
    float2 q = abs(p) - halfSize;

    if (q.y > q.x)
        q = q.yx;

    q.y += chamfer;
    const float diagonalScale = 0.70710678118;
    const float diagonalBias = 1.0 - 1.41421356237;

    if (q.y < 0.0 && q.y + q.x * diagonalBias < 0.0)
        return q.x;

    if (q.x < q.y)
        return (q.x + q.y) * diagonalScale;

    return length(q);
}

float2 NowSdfNodePivotV2(float type, float4 data1, float4 data2)
{
    if (type > 3.5 && type < 4.5)
        return data1.xy * 0.5 + data1.zw * 0.5;

    if (type > 8.5 && type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        float2 a = data1.xy;
        float2 b = a + data1.zw * scale;
        float2 c = a + data2.xy * scale;
        float2 minPoint = min(a, min(b, c));
        float2 maxPoint = max(a, max(b, c));
        return minPoint + (maxPoint - minPoint) * 0.5;
    }

    return data1.xy;
}

float2 NowSdfInverseRotateRelativeV2(
    float2 scenePos,
    float2 pivot,
    float2 rotation,
    float rotationLengthSquared)
{
    float2 p = scenePos - pivot;

    return float2(
        p.x * rotation.x + p.y * rotation.y,
        -p.x * rotation.y + p.y * rotation.x) / rotationLengthSquared;
}

float NowSdfSegmentDistanceSquaredV2(float2 p, float2 a, float2 b)
{
    float2 edge = b - a;
    float denominator = dot(edge, edge);

    if (denominator <= 0.0)
        return dot(p - a, p - a);

    float t = saturate(dot(p - a, edge) / denominator);
    float2 nearest = p - a - edge * t;
    return dot(nearest, nearest);
}

float NowSdfTriangleDistanceV2(float2 p, float2 b, float2 c, float orientationSign)
{
    const float2 a = float2(0.0, 0.0);
    float distanceSquared = min(
        NowSdfSegmentDistanceSquaredV2(p, a, b),
        min(
            NowSdfSegmentDistanceSquaredV2(p, b, c),
            NowSdfSegmentDistanceSquaredV2(p, c, a)));
    // A collapsed or numerically near-collinear triangle has no interior, but
    // remains a useful segment/point field instead of producing an unstable sign.
    if (abs(orientationSign) < 0.5)
        return sqrt(max(distanceSquared, 0.0));

    float ab = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    float bc = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
    float ca = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
    bool inside = orientationSign > 0.0
        ? (ab >= 0.0 && bc >= 0.0 && ca >= 0.0)
        : (ab <= 0.0 && bc <= 0.0 && ca <= 0.0);
    float distance = sqrt(max(distanceSquared, 0.0));
    return inside ? -distance : distance;
}

float2 NowSdfRotateRadialV2(float2 p, float2 rotation)
{
    return float2(
        p.x * rotation.x - p.y * rotation.y,
        p.x * rotation.y + p.y * rotation.x);
}

// Arc and pie SDFs adapted from Inigo Quilez's 2D distance functions (MIT).
// https://iquilezles.org/articles/distfunctions2d/
// Copyright © 2019 Inigo Quilez. See THIRD_PARTY_LICENSES.md.
float NowSdfArcDistanceV2(float2 p, float2 sc, float ra, float rb)
{
    p.x = abs(p.x);
    return ((sc.y * p.x > sc.x * p.y) ? length(p - sc * ra) : abs(length(p) - ra)) - rb;
}

float NowSdfPieDistanceV2(float2 p, float2 sc, float r)
{
    p.x = abs(p.x);
    float l = length(p) - r;
    float m = length(p - sc * clamp(dot(p, sc), 0.0, r));
    return max(l, m * sign(sc.y * p.x - sc.x * p.y));
}

float median(float r, float g, float b)
{
    return max(min(r, g), min(max(r, g), b));
}

float2 NowSdfGlyphSamplesV2(float4 sample, float encoding)
{
    // Managed dynamic pages keep a scalar SDF in two RGBA8 channels. The high
    // byte is repeated in R/G/A so legacy consumers retain a useful 8-bit field;
    // B carries the low byte for NowUI-aware shaders. Native MTSDF pages use
    // median RGB at the fill edge and their true-distance alpha for exterior
    // effects, where median RGB is not stable far from corners.
    if (encoding > 0.5)
    {
        float packedSample = (sample.r * 256.0 + sample.b) / 257.0;
        return float2(packedSample, packedSample);
    }

    return float2(median(sample.r, sample.g, sample.b), sample.a);
}

float NowSdfGlyphSampleV2(float4 sample, float encoding)
{
    return NowSdfGlyphSamplesV2(sample, encoding).x;
}

float NowSdfShapeCodeStepV2(float type, float4 data2)
{
    return type > 4.5 && type < 5.5 ? max(data2.z, 0.0) : 0.0;
}

float2 NowSdfGlyphLocalDistancesV2(
    float2 local,
    float2 size,
    float4 data2,
    float4 uvRect)
{
    size = max(size, 0.0001);
    float2 halfSize = size * 0.5;
    float2 glyphUv = local / size + 0.5;
    float boundsDist = sdBox(local, halfSize);

    if (glyphUv.x < 0.0 || glyphUv.y < 0.0 || glyphUv.x > 1.0 || glyphUv.y > 1.0)
    {
        // The encoded field spans +/- half of its full range. Continue from
        // that saturated exterior value so ddx/ddy cannot turn the glyph-quad
        // boundary into a false antialiased outline.
        float outsideDistance = 0.5 * max(data2.x, 0.0001) + max(boundsDist, 0.0);
        return float2(outsideDistance, outsideDistance);
    }

    float2 atlasUv = uvRect.xy + float2(glyphUv.x, 1.0 - glyphUv.y) * uvRect.zw;
    float4 msd = tex2D(_MainTex, atlasUv);
    return (0.5 - NowSdfGlyphSamplesV2(msd, data2.y)) * max(data2.x, 0.0001);
}

float2 sdGlyphDistances(float2 scenePos, float4 data1, float4 data2, float4 uvRect)
{
    return NowSdfGlyphLocalDistancesV2(scenePos - data1.xy, data1.zw, data2, uvRect);
}

float sdGlyph(float2 scenePos, float4 data1, float4 data2, float4 uvRect)
{
    return sdGlyphDistances(scenePos, data1, data2, uvRect).x;
}

float NowSdfUnrotatedShapeDistanceV2(
    int index,
    float type,
    float4 data1,
    float4 data2,
    float2 scenePos)
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

    if (type < 5.5)
        return sdGlyph(scenePos, data1, data2, _SdfUvs[index]);

    if (type < 7.5)
    {
        float2 radial = scenePos - data1.xy;

        // A zero rotation vector is the explicit full-turn sentinel. Bypass the
        // aperture formula so exact and clamped full pies cannot develop a sign seam.
        if (dot(data2.zw, data2.zw) < 0.5)
        {
            if (type < 6.5)
                return abs(length(radial) - data1.z) - data1.w;

            return length(radial) - data1.z;
        }

        float2 q = NowSdfRotateRadialV2(radial, data2.zw);

        if (type < 6.5)
            return NowSdfArcDistanceV2(q, data2.xy, data1.z, data1.w);

        return NowSdfPieDistanceV2(q, data2.xy, data1.z);
    }

    if (type < 8.5)
    {
        return NowSdfChamferedBoxDistanceV2(
            scenePos - data1.xy,
            max(data1.zw * 0.5, 0.0001),
            data2.x);
    }

    if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        float2 normalizedPosition = (scenePos - data1.xy) / scale;
        return NowSdfTriangleDistanceV2(
            normalizedPosition,
            data1.zw,
            data2.xy,
            data2.z) * scale;
    }

    return 100000.0;
}

float NowSdfRotatedShapeDistanceV2(
    int index,
    float type,
    float4 data1,
    float4 data2,
    float2 relativeScenePos,
    float2 pivot)
{
    if (type < 0.5)
        return length(relativeScenePos) - data1.z;

    if (type < 1.5)
        return sdBox(relativeScenePos, max(data1.zw * 0.5, 0.0001));

    if (type < 2.5)
        return sdRoundBox(relativeScenePos, max(data1.zw * 0.5, 0.0001), data2);

    if (type < 3.5)
        return sdEllipse(relativeScenePos, max(data1.zw * 0.5, 0.0001));

    if (type < 4.5)
    {
        return sdCapsule(
            relativeScenePos,
            data1.xy - pivot,
            data1.zw - pivot,
            data2.x);
    }

    // Text rotates a run rigidly by moving glyph centers around one shared
    // CPU-side pivot, then rotating each glyph locally around its moved center.
    // Keep evaluation pivot-relative to avoid rebuilding a large absolute
    // coordinate from pivot + relativeScenePos.
    if (type < 5.5)
        return NowSdfGlyphLocalDistancesV2(
            relativeScenePos,
            data1.zw,
            data2,
            _SdfUvs[index]).x;

    if (type < 7.5)
    {
        // A zero rotation vector is the explicit full-turn sentinel. Bypass the
        // aperture formula so exact and clamped full pies cannot develop a sign seam.
        if (dot(data2.zw, data2.zw) < 0.5)
        {
            if (type < 6.5)
                return abs(length(relativeScenePos) - data1.z) - data1.w;

            return length(relativeScenePos) - data1.z;
        }

        float2 q = NowSdfRotateRadialV2(relativeScenePos, data2.zw);

        if (type < 6.5)
            return NowSdfArcDistanceV2(q, data2.xy, data1.z, data1.w);

        return NowSdfPieDistanceV2(q, data2.xy, data1.z);
    }

    if (type < 8.5)
    {
        return NowSdfChamferedBoxDistanceV2(
            relativeScenePos,
            max(data1.zw * 0.5, 0.0001),
            data2.x);
    }

    if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        float2 normalizedPosition = (relativeScenePos + (pivot - data1.xy)) / scale;
        return NowSdfTriangleDistanceV2(
            normalizedPosition,
            data1.zw,
            data2.xy,
            data2.z) * scale;
    }

    return 100000.0;
}

float2 NowSdfUnrotatedShapeDistancesV2(
    int index,
    float type,
    float4 data1,
    float4 data2,
    float2 scenePos)
{
    if (type > 4.5 && type < 5.5)
        return sdGlyphDistances(scenePos, data1, data2, _SdfUvs[index]);

    float distance = NowSdfUnrotatedShapeDistanceV2(index, type, data1, data2, scenePos);
    return float2(distance, distance);
}

float2 NowSdfRotatedShapeDistancesV2(
    int index,
    float type,
    float4 data1,
    float4 data2,
    float2 relativeScenePos,
    float2 pivot)
{
    if (type > 4.5 && type < 5.5)
    {
        return NowSdfGlyphLocalDistancesV2(
            relativeScenePos,
            data1.zw,
            data2,
            _SdfUvs[index]);
    }

    float distance = NowSdfRotatedShapeDistanceV2(
        index,
        type,
        data1,
        data2,
        relativeScenePos,
        pivot);
    return float2(distance, distance);
}

float2 shapeDistances(int index, float type, float4 data1, float4 data2, float2 scenePos)
{
    float2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    // The exact zero pair is the canonical identity sentinel. Keep that path on
    // the original evaluator so unrotated nodes retain their existing arithmetic.
    if (rotationLengthSquared == 0.0)
        return NowSdfUnrotatedShapeDistancesV2(index, type, data1, data2, scenePos);

    float2 pivot = NowSdfNodePivotV2(type, data1, data2);
    float2 relativeScenePos = NowSdfInverseRotateRelativeV2(
        scenePos,
        pivot,
        rotation,
        rotationLengthSquared);
    return NowSdfRotatedShapeDistancesV2(
        index,
        type,
        data1,
        data2,
        relativeScenePos,
        pivot) *
        sqrt(rotationLengthSquared);
}

float shapeDistance(int index, float type, float4 data1, float4 data2, float2 scenePos)
{
    return shapeDistances(index, type, data1, data2, scenePos).x;
}

float NowSdfTransformedShapeCodeStepV2(int index, float type, float4 data2)
{
    float codeStep = NowSdfShapeCodeStepV2(type, data2);
    float2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);
    return rotationLengthSquared == 0.0
        ? codeStep
        : codeStep * sqrt(rotationLengthSquared);
}

float2 NowSdfRotatedShapeUvV2(
    float type,
    float4 data1,
    float4 data2,
    float2 relativeScenePos,
    float2 pivot)
{
    float2 minPoint;
    float2 maxPoint;

    if (type < 0.5)
    {
        minPoint = -data1.zz;
        maxPoint = data1.zz;
    }
    else if (type < 3.5)
    {
        float2 halfSize = data1.zw * 0.5;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 4.5)
    {
        float2 a = data1.xy - pivot;
        float2 b = data1.zw - pivot;
        minPoint = min(a, b) - data2.xx;
        maxPoint = max(a, b) + data2.xx;
    }
    else if (type < 7.5)
    {
        float extent = type < 6.5 ? data1.z + data1.w : data1.z;
        minPoint = -float2(extent, extent);
        maxPoint = float2(extent, extent);
    }
    else if (type < 8.5)
    {
        float2 halfSize = data1.zw * 0.5;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        float2 normalizedPosition = (relativeScenePos + (pivot - data1.xy)) / scale;
        float2 minNormalized = min(float2(0.0, 0.0), min(data1.zw, data2.xy));
        float2 maxNormalized = max(float2(0.0, 0.0), max(data1.zw, data2.xy));
        float2 span = maxNormalized - minNormalized;
        float2 uv;
        uv.x = span.x > 0.0
            ? saturate((normalizedPosition.x - minNormalized.x) / span.x)
            : 0.5;
        uv.y = span.y > 0.0
            ? saturate((normalizedPosition.y - minNormalized.y) / span.y)
            : 0.5;
        return float2(uv.x, 1.0 - uv.y);
    }
    else
    {
        return float2(0.5, 0.5);
    }

    float2 uv = saturate(
        (relativeScenePos - minPoint) /
        max(maxPoint - minPoint, 0.0001));
    return float2(uv.x, 1.0 - uv.y);
}

float2 shapeUv(int index, float type, float4 data1, float4 data2, float2 scenePos)
{
    float2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    if (rotationLengthSquared != 0.0)
    {
        float2 pivot = NowSdfNodePivotV2(type, data1, data2);
        float2 relativeScenePos = NowSdfInverseRotateRelativeV2(
            scenePos,
            pivot,
            rotation,
            rotationLengthSquared);
        return NowSdfRotatedShapeUvV2(
            type,
            data1,
            data2,
            relativeScenePos,
            pivot);
    }

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
    else if (type < 4.5)
    {
        minPoint = min(data1.xy, data1.zw) - data2.xx;
        maxPoint = max(data1.xy, data1.zw) + data2.xx;
    }
    else if (type < 7.5)
    {
        float extent = type < 6.5 ? data1.z + data1.w : data1.z;
        minPoint = data1.xy - float2(extent, extent);
        maxPoint = data1.xy + float2(extent, extent);
    }
    else if (type < 8.5)
    {
        float2 halfSize = data1.zw * 0.5;
        scenePos -= data1.xy;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        float2 normalizedPosition = (scenePos - data1.xy) / scale;
        float2 minNormalized = min(float2(0.0, 0.0), min(data1.zw, data2.xy));
        float2 maxNormalized = max(float2(0.0, 0.0), max(data1.zw, data2.xy));
        float2 span = maxNormalized - minNormalized;
        float2 uv;
        uv.x = span.x > 0.0
            ? saturate((normalizedPosition.x - minNormalized.x) / span.x)
            : 0.5;
        uv.y = span.y > 0.0
            ? saturate((normalizedPosition.y - minNormalized.y) / span.y)
            : 0.5;
        return float2(uv.x, 1.0 - uv.y);
    }
    else
    {
        return float2(0.5, 0.5);
    }

    float2 uv = saturate((scenePos - minPoint) / max(maxPoint - minPoint, 0.0001));
    return float2(uv.x, 1.0 - uv.y);
}

float4 shapeFill(int index, float type, float4 data1, float4 data2, float2 scenePos, float4 tint)
{
    float4 color = _SdfColors[index] * tint;

    if ((type > 4.5 && type < 5.5) || _SdfShapeMeta[index].y < 0.5)
        return color;

    float2 uv = shapeUv(index, type, data1, data2, scenePos);
    float4 uvRect = _SdfUvs[index];
    uv = uvRect.xy + uv * uvRect.zw;
    return tex2D(_MainTex, uv) * color;
}

void combine(
    inout float dist,
    inout float4 fill,
    inout float codeStep,
    float shapeDist,
    float4 nextFill,
    float shapeCodeStep,
    float operation,
    float smoothing)
{
    if (operation < 0.5)
    {
        if (shapeDist < dist)
        {
            dist = shapeDist;
            fill = nextFill;
            codeStep = shapeCodeStep;
        }
        else if (shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }

        return;
    }

    if (operation < 1.5)
    {
        if (-shapeDist > dist)
        {
            dist = -shapeDist;
            codeStep = shapeCodeStep;
        }
        else if (-shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }
        return;
    }

    if (operation < 2.5)
    {
        if (shapeDist > dist)
        {
            fill = nextFill;
            codeStep = shapeCodeStep;
        }
        else if (shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }

        dist = max(dist, shapeDist);
        return;
    }

    smoothing = max(smoothing, 0.0001);

    if (operation < 3.5)
    {
        float h = saturate(0.5 + 0.5 * (shapeDist - dist) / smoothing);
        dist = lerp(shapeDist, dist, h) - smoothing * h * (1.0 - h);
        fill = lerp(nextFill, fill, h);
        codeStep = lerp(shapeCodeStep, codeStep, h);
        return;
    }

    if (operation < 4.5)
    {
        float h = saturate(0.5 - 0.5 * (shapeDist + dist) / smoothing);
        dist = lerp(dist, -shapeDist, h) + smoothing * h * (1.0 - h);
        codeStep = lerp(codeStep, shapeCodeStep, h);
        return;
    }

    {
        float h = saturate(0.5 - 0.5 * (shapeDist - dist) / smoothing);
        dist = lerp(shapeDist, dist, h) + smoothing * h * (1.0 - h);
        fill = lerp(nextFill, fill, h);
        codeStep = lerp(shapeCodeStep, codeStep, h);
    }
}

void combineDistance(
    inout float dist,
    inout float codeStep,
    float shapeDist,
    float shapeCodeStep,
    float operation,
    float smoothing)
{
    if (operation < 0.5)
    {
        if (shapeDist < dist)
        {
            dist = shapeDist;
            codeStep = shapeCodeStep;
        }
        else if (shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }
        return;
    }

    if (operation < 1.5)
    {
        if (-shapeDist > dist)
        {
            dist = -shapeDist;
            codeStep = shapeCodeStep;
        }
        else if (-shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }
        return;
    }

    if (operation < 2.5)
    {
        if (shapeDist > dist)
        {
            dist = shapeDist;
            codeStep = shapeCodeStep;
        }
        else if (shapeDist == dist)
        {
            codeStep = max(codeStep, shapeCodeStep);
        }
        return;
    }

    smoothing = max(smoothing, 0.0001);

    if (operation < 3.5)
    {
        float h = saturate(0.5 + 0.5 * (shapeDist - dist) / smoothing);
        dist = lerp(shapeDist, dist, h) - smoothing * h * (1.0 - h);
        codeStep = lerp(shapeCodeStep, codeStep, h);
        return;
    }

    if (operation < 4.5)
    {
        float h = saturate(0.5 - 0.5 * (shapeDist + dist) / smoothing);
        dist = lerp(dist, -shapeDist, h) + smoothing * h * (1.0 - h);
        codeStep = lerp(codeStep, shapeCodeStep, h);
        return;
    }

    {
        float h = saturate(0.5 - 0.5 * (shapeDist - dist) / smoothing);
        dist = lerp(shapeDist, dist, h) + smoothing * h * (1.0 - h);
        codeStep = lerp(shapeCodeStep, codeStep, h);
    }
}

void decodeGraphRange(float packedRange, out int start, out int count)
{
    int total = min(max((int)_SdfShapeCount, 0), NOW_SDF_MAX_SHAPES);
    float packed = max(packedRange, 0.0);
    float startValue = floor(packed * (1.0 / 128.0));
    start = min(max((int)startValue, 0), total);
    count = min(max((int)(packed - startValue * 128.0 + 0.5), 0), total - start);
}

void evalGraphFields(
    float packedRange,
    float2 scenePos,
    float4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out float4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    int start;
    int count;
    decodeGraphRange(packedRange, start, count);
    dist = 100000.0;
    effectDist = 100000.0;
    fill = 0.0;
    codeStep = 0.0;
    effectCodeStep = 0.0;

    if (count <= 0)
        return;

    int first = start;
    float4 data0 = _SdfData0[first];
    float4 data1 = _SdfData1[first];
    float4 data2 = _SdfData2[first];
    float2 firstDistances = shapeDistances(first, data0.x, data1, data2, scenePos);
    dist = firstDistances.x;
    effectDist = useDistinctEffectField ? firstDistances.y : firstDistances.x;
    fill = shapeFill(first, data0.x, data1, data2, scenePos, tint);
    codeStep = NowSdfTransformedShapeCodeStepV2(first, data0.x, data2);
    effectCodeStep = codeStep;

    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];
        data1 = _SdfData1[index];
        data2 = _SdfData2[index];
        float2 shapeFieldDistances = shapeDistances(index, data0.x, data1, data2, scenePos);
        float4 nextFill = shapeFill(index, data0.x, data1, data2, scenePos, tint);
        float shapeCodeStep = NowSdfTransformedShapeCodeStepV2(index, data0.x, data2);
        combine(
            dist,
            fill,
            codeStep,
            shapeFieldDistances.x,
            nextFill,
            shapeCodeStep,
            data0.y,
            data0.z);
        UNITY_BRANCH
        if (useDistinctEffectField)
        {
            combineDistance(
                effectDist,
                effectCodeStep,
                shapeFieldDistances.y,
                shapeCodeStep,
                data0.y,
                data0.z);
        }
    }

    if (!useDistinctEffectField)
    {
        effectDist = dist;
        effectCodeStep = codeStep;
    }
}

void evalGraph(
    float packedRange,
    float2 scenePos,
    float4 tint,
    out float dist,
    out float4 fill,
    out float codeStep)
{
    float effectDist;
    float effectCodeStep;
    evalGraphFields(
        packedRange,
        scenePos,
        tint,
        false,
        dist,
        effectDist,
        fill,
        codeStep,
        effectCodeStep);
}

void evalLayerFields(
    int index,
    float2 scenePos,
    float4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out float4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    // Initialize at this boundary as well as inside evalGraphFields. Some cross
    // compilers do not prove that out parameters are written through the
    // non-morph call before the early return.
    dist = 100000.0;
    effectDist = 100000.0;
    fill = 0.0;
    codeStep = 0.0;
    effectCodeStep = 0.0;
    float4 layer0 = _SdfLayerData0[index];
    float4 layer1 = _SdfLayerData1[index];

    if (layer0.w < 0.5)
    {
        evalGraphFields(
            layer1.z,
            scenePos,
            tint,
            useDistinctEffectField,
            dist,
            effectDist,
            fill,
            codeStep,
            effectCodeStep);
        return;
    }

    float aDist = 0;
    float bDist = 0;
    float aEffectDist = 0;
    float bEffectDist = 0;
    float4 aFill = 0;
    float4 bFill = 0;
    float aCodeStep = 0;
    float bCodeStep = 0;
    float aEffectCodeStep = 0;
    float bEffectCodeStep = 0;
    evalGraphFields(
        layer1.z,
        scenePos,
        tint,
        useDistinctEffectField,
        aDist,
        aEffectDist,
        aFill,
        aCodeStep,
        aEffectCodeStep);
    evalGraphFields(
        layer1.w,
        scenePos,
        tint,
        useDistinctEffectField,
        bDist,
        bEffectDist,
        bFill,
        bCodeStep,
        bEffectCodeStep);
    float t = saturate(layer1.y);
    dist = lerp(aDist, bDist, t);
    fill = lerp(aFill, bFill, t);
    codeStep = lerp(aCodeStep, bCodeStep, t);
    if (useDistinctEffectField)
    {
        effectDist = lerp(aEffectDist, bEffectDist, t);
        effectCodeStep = lerp(aEffectCodeStep, bEffectCodeStep, t);
    }
    else
    {
        effectDist = dist;
        effectCodeStep = codeStep;
    }
}

void evalLayer(
    int index,
    float2 scenePos,
    float4 tint,
    out float dist,
    out float4 fill,
    out float codeStep)
{
    float effectDist;
    float effectCodeStep;
    evalLayerFields(
        index,
        scenePos,
        tint,
        false,
        dist,
        effectDist,
        fill,
        codeStep,
        effectCodeStep);
}

void evalGraphDistanceField(
    float packedRange,
    float2 scenePos,
    float effectField,
    out float dist,
    out float codeStep)
{
    int start;
    int count;
    decodeGraphRange(packedRange, start, count);
    dist = 100000.0;
    codeStep = 0.0;

    if (count <= 0)
        return;

    int first = start;
    float4 data0 = _SdfData0[first];
    float4 firstData2 = _SdfData2[first];
    float2 firstDistances = shapeDistances(first, data0.x, _SdfData1[first], firstData2, scenePos);
    dist = lerp(firstDistances.x, firstDistances.y, effectField);
    codeStep = NowSdfTransformedShapeCodeStepV2(first, data0.x, firstData2);

    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];
        float4 data2 = _SdfData2[index];
        float2 shapeFieldDistances = shapeDistances(index, data0.x, _SdfData1[index], data2, scenePos);
        float shapeDist = lerp(shapeFieldDistances.x, shapeFieldDistances.y, effectField);
        float shapeCodeStep = NowSdfTransformedShapeCodeStepV2(index, data0.x, data2);
        combineDistance(dist, codeStep, shapeDist, shapeCodeStep, data0.y, data0.z);
    }
}

void evalGraphDistance(
    float packedRange,
    float2 scenePos,
    out float dist,
    out float codeStep)
{
    evalGraphDistanceField(packedRange, scenePos, 0.0, dist, codeStep);
}

void evalGraphEffectDistance(
    float packedRange,
    float2 scenePos,
    out float dist,
    out float codeStep)
{
    evalGraphDistanceField(packedRange, scenePos, 1.0, dist, codeStep);
}

void evalLayerDistanceField(
    int index,
    float2 scenePos,
    float effectField,
    out float dist,
    out float codeStep)
{
    dist = 100000.0;
    codeStep = 0.0;
    float4 layer0 = _SdfLayerData0[index];
    float4 layer1 = _SdfLayerData1[index];

    if (layer0.w < 0.5)
    {
        evalGraphDistanceField(layer1.z, scenePos, effectField, dist, codeStep);
        return;
    }

    float aDist = 100000.0;
    float bDist = 100000.0;
    float aCodeStep = 0.0;
    float bCodeStep = 0.0;
    evalGraphDistanceField(layer1.z, scenePos, effectField, aDist, aCodeStep);
    evalGraphDistanceField(layer1.w, scenePos, effectField, bDist, bCodeStep);
    float t = saturate(layer1.y);
    dist = lerp(aDist, bDist, t);
    codeStep = lerp(aCodeStep, bCodeStep, t);
}

void evalLayerDistance(int index, float2 scenePos, out float dist, out float codeStep)
{
    evalLayerDistanceField(index, scenePos, 0.0, dist, codeStep);
}

void evalLayerEffectDistance(int index, float2 scenePos, out float dist, out float codeStep)
{
    evalLayerDistanceField(index, scenePos, 1.0, dist, codeStep);
}

void evalSceneFields(
    float2 scenePos,
    float4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out float4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    int layerCount = min((int)_SdfLayerCount, NOW_SDF_MAX_LAYERS);
    bool found = false;
    dist = 100000.0;
    effectDist = 100000.0;
    fill = 0.0;
    codeStep = 0.0;
    effectCodeStep = 0.0;

    for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
    {
        if (layer >= layerCount)
            break;

        float layerDist;
        float layerEffectDist;
        float4 layerFill;
        float layerCodeStep;
        float layerEffectCodeStep;
        evalLayerFields(
            layer,
            scenePos,
            tint,
            useDistinctEffectField,
            layerDist,
            layerEffectDist,
            layerFill,
            layerCodeStep,
            layerEffectCodeStep);

        if (!found)
        {
            dist = layerDist;
            effectDist = layerEffectDist;
            fill = layerFill;
            codeStep = layerCodeStep;
            effectCodeStep = layerEffectCodeStep;
            found = true;
        }
        else
        {
            combine(
                dist,
                fill,
                codeStep,
                layerDist,
                layerFill,
                layerCodeStep,
                _SdfLayerData0[layer].y,
                _SdfLayerData0[layer].z);
            UNITY_BRANCH
            if (useDistinctEffectField)
            {
                combineDistance(
                    effectDist,
                    effectCodeStep,
                    layerEffectDist,
                    layerEffectCodeStep,
                    _SdfLayerData0[layer].y,
                    _SdfLayerData0[layer].z);
            }
        }
    }

    if (!useDistinctEffectField)
    {
        effectDist = dist;
        effectCodeStep = codeStep;
    }
}

void evalScene(
    float2 scenePos,
    float4 tint,
    out float dist,
    out float4 fill,
    out float codeStep)
{
    float effectDist;
    float effectCodeStep;
    evalSceneFields(
        scenePos,
        tint,
        false,
        dist,
        effectDist,
        fill,
        codeStep,
        effectCodeStep);
}

void evalSceneDistanceAndCodeStepField(
    float2 scenePos,
    float effectField,
    out float dist,
    out float codeStep)
{
    int layerCount = min((int)_SdfLayerCount, NOW_SDF_MAX_LAYERS);
    bool found = false;
    dist = 100000.0;
    codeStep = 0.0;

    for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
    {
        if (layer >= layerCount)
            break;

        float layerDist;
        float layerCodeStep;
        evalLayerDistanceField(layer, scenePos, effectField, layerDist, layerCodeStep);

        if (!found)
        {
            dist = layerDist;
            codeStep = layerCodeStep;
            found = true;
        }
        else
        {
            combineDistance(
                dist,
                codeStep,
                layerDist,
                layerCodeStep,
                _SdfLayerData0[layer].y,
                _SdfLayerData0[layer].z);
        }
    }
}

void evalSceneDistanceAndCodeStep(float2 scenePos, out float dist, out float codeStep)
{
    evalSceneDistanceAndCodeStepField(scenePos, 0.0, dist, codeStep);
}

void evalSceneEffectDistanceAndCodeStep(float2 scenePos, out float dist, out float codeStep)
{
    evalSceneDistanceAndCodeStepField(scenePos, 1.0, dist, codeStep);
}

// Keep the original helper signature available to custom material includes.
void evalSceneDistance(float2 scenePos, out float dist)
{
    float codeStep;
    evalSceneDistanceAndCodeStep(scenePos, dist, codeStep);
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

float exteriorEffectValidity(float distance, float codeStep, float edge)
{
    // Analytic distances have no finite glyph field. Glyph distances fade out
    // before the uploaded SDF range can expose its rectangular fallback.
    float isGlyphDistance = sign(max(codeStep, 0.0));
    float glyphValidity = 1.0 - smoothstep(
        max(_SdfTextEffectLimit, 0.0) - edge,
        max(_SdfTextEffectLimit, 0.0) + edge,
        distance);
    return lerp(1.0, glyphValidity, isGlyphDistance);
}

float exclusiveEffectCoverage(float effectCoverage, float fillCoverage, float fillOpacity)
{
    // Source-over applies the remaining fill coverage again. Condition the
    // exterior layer so its geometric ring survives authored fill opacity.
    float remainingFill = max(1.0 - fillCoverage * saturate(fillOpacity), 0.0001);
    return saturate((effectCoverage - fillCoverage) / remainingFill);
}

float4 alphaOver(float4 baseColor, float4 topColor)
{
    float a = topColor.a + baseColor.a * (1.0 - topColor.a);
    float3 rgb = (topColor.rgb * topColor.a + baseColor.rgb * baseColor.a * (1.0 - topColor.a)) / max(a, 0.0001);
    return float4(rgb, a);
}

// Supported ABI-v2 helpers for custom final-shading hooks. Positions passed to
// the distance-evaluation helpers are unwarped, top-left/y-down scene positions.
float4 NowSdfAlphaOverV2(float4 baseColor, float4 topColor)
{
    return alphaOver(baseColor, topColor);
}

float NowSdfEvaluateDistanceV2(float2 sourceScenePosition)
{
    float distance;
    evalSceneDistance(warpScenePos(sourceScenePosition), distance);
    return distance;
}

float NowSdfEvaluateEffectDistanceV2(float2 sourceScenePosition)
{
    float distance;
    float codeStep;
    evalSceneEffectDistanceAndCodeStep(
        warpScenePos(sourceScenePosition),
        distance,
        codeStep);
    return distance;
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
    // Immediate meshes carry SDF scene mapping in UV5. Canvas meshes repack the
    // same source data into UV3 because UGUI exposes fewer vertex channels.
    o.sceneMapping = lerp(v.data5, v.data3, isCanvas);

    float2 pixelSize = o.vertex.w;
    pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    o.uiMask = float4(
        v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
        0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

    return o;
}

#ifdef NOW_SDF_CUSTOM_FINAL_SHADE
float4 NOW_SDF_CUSTOM_FINAL_SHADE(
    float4 stockColor,
    float4 fill,
    float4 tint,
    float2 quadUv,
    float2 scenePosition,
    float2 sourceScenePosition,
    float2 sceneSize,
    float signedDistance,
    float coverage,
    float pixelWidth,
    float edge);
#endif

fixed4 frag(v2f i) : SV_Target
{
    float2 quadPos = i.rawUV * i.rect.zw;
    // Older/generated meshes may not carry the source mapping yet. Treat an
    // empty payload as identity so custom shader includes remain compatible.
    float hasSceneMapping = step(0.0001, abs(i.sceneMapping.x) + abs(i.sceneMapping.y));
    float2 sceneSize = max(lerp(i.rect.zw, abs(i.sceneMapping.xy), hasSceneMapping), 0.0001);
    float2 sceneDirection = lerp(float2(1.0, 1.0), i.sceneMapping.zw, hasSceneMapping);
    float2 sourceUv = 0.5 + (i.rawUV - 0.5) * sceneDirection;
    float2 sceneQuadPos = sourceUv * sceneSize;
    float2 scenePosBase = float2(sceneQuadPos.x, sceneSize.y - sceneQuadPos.y);
    float2 scenePos = warpScenePos(scenePosBase);
    float2 meshPos = i.rect.xy + quadPos;
    float2 uiPosition = float2(meshPos.x, -meshPos.y);
    float4 mask = i.mask;

    NowUIClipLegacyRect(uiPosition, mask);

    // The CPU uploads 100000 for analytic-only scenes. This flag depends only
    // on material uniforms, so every fragment in the draw takes the same path.
    bool hasFiniteTextEffectLimit = _SdfTextEffectLimit < 100000.0;
    bool hasStockDistanceEffect =
        (_SdfOutlineColor.a > 0.0 && _SdfOutline.x > 0.0) ||
        (_SdfGlowColor.a > 0.0 && _SdfGlow.x > 0.0) ||
        _SdfShadowColor.a > 0.0 ||
        _SdfInnerShadowColor.a > 0.0 ||
        (_SdfContourColor.a > 0.0 && _SdfContour.x > 0.0 && _SdfContour.y > 0.0);
    bool useDistinctEffectField = hasFiniteTextEffectLimit && hasStockDistanceEffect;

    float dist = 100000.0;
    float effectDist = 100000.0;
    float4 fill = 0.0;
    float distanceCodeStep = 0.0;
    float effectCodeStep = 0.0;
    evalSceneFields(
        scenePos,
        i.tint,
        useDistinctEffectField,
        dist,
        effectDist,
        fill,
        distanceCodeStep,
        effectCodeStep);

    float pixelWidth = max(
        max(length(float2(ddx(dist), ddy(dist))), distanceCodeStep),
        0.0001);
    float edge = pixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    float effectPixelWidth = pixelWidth;
    float effectEdge = edge;
    UNITY_BRANCH
    if (useDistinctEffectField)
    {
        effectPixelWidth = max(
            max(length(float2(ddx(effectDist), ddy(effectDist))), effectCodeStep),
            0.0001);
        effectEdge = effectPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    }
    float coverage = smoothstep(edge, -edge, dist);
    float exteriorValidity = 1.0;
    UNITY_BRANCH
    if (useDistinctEffectField)
        exteriorValidity = exteriorEffectValidity(effectDist, effectCodeStep, effectEdge);
    float4 col = 0.0;

    if (_SdfShadowColor.a > 0.0)
    {
        float shadowDist;
        float shadowCodeStep;
        evalSceneEffectDistanceAndCodeStep(
            warpScenePos(scenePosBase - _SdfShadow.xy),
            shadowDist,
            shadowCodeStep);
        float shadowPixelWidth = max(
            max(length(float2(ddx(shadowDist), ddy(shadowDist))), shadowCodeStep),
            0.0001);
        float shadowEdge = shadowPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
        float shadowEffectDist = shadowDist - _SdfShadow.w;
        float shadowCoverage = smoothstep(max(_SdfShadow.z, shadowPixelWidth) + shadowEdge, -shadowEdge, shadowEffectDist);
        float shadowAlpha = exclusiveEffectCoverage(shadowCoverage, coverage, fill.a);
        shadowAlpha *= exteriorEffectValidity(shadowDist, shadowCodeStep, shadowEdge);
        float4 shadowColor = effectColor(_SdfShadowColor, i.tint);
        shadowColor.a *= shadowAlpha;
        col = alphaOver(col, shadowColor);
    }

    if (_SdfGlowColor.a > 0.0 && _SdfGlow.x > 0.0)
    {
        float glowT = saturate(1.0 - max(effectDist, 0.0) / max(_SdfGlow.x, 0.0001));
        float glowCoverage = pow(glowT, max(_SdfGlow.y, 0.0001));
        float glowAlpha = exclusiveEffectCoverage(glowCoverage, coverage, fill.a) * exteriorValidity;
        float4 glowColor = effectColor(_SdfGlowColor, i.tint);
        glowColor.a *= glowAlpha;
        col = alphaOver(col, glowColor);
    }

    if (_SdfOutlineColor.a > 0.0 && _SdfOutline.x > 0.0)
    {
        float outlineCoverage = smoothstep(_SdfOutline.x + _SdfOutline.y + effectEdge, _SdfOutline.x - effectEdge, effectDist);
        float outlineAlpha = exclusiveEffectCoverage(outlineCoverage, coverage, fill.a) * exteriorValidity;
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
        float band = 1.0 - smoothstep(0.0, max(_SdfEmboss.z, pixelWidth), abs(dist));
        float shade = dot(normal2, light) * _SdfEmboss.w * band;
        fillColor.rgb = saturate(fillColor.rgb + shade);
    }

    fillColor.a *= coverage;
    col = alphaOver(col, fillColor);

    if (_SdfInnerShadowColor.a > 0.0)
    {
        float innerDist;
        float innerCodeStep;
        evalSceneEffectDistanceAndCodeStep(
            warpScenePos(scenePosBase - _SdfInnerShadow.xy),
            innerDist,
            innerCodeStep);
        float innerPixelWidth = max(
            max(length(float2(ddx(innerDist), ddy(innerDist))), innerCodeStep),
            0.0001);
        float innerEdge = innerPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
        float innerEffectDist = innerDist + _SdfInnerShadow.w;
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
        contourAlpha *= exteriorValidity;
        float4 contourColor = effectColor(_SdfContourColor, i.tint);
        contourColor.a *= contourAlpha;
        col = alphaOver(col, contourColor);
    }

    #ifdef NOW_SDF_CUSTOM_FINAL_SHADE
    col = NOW_SDF_CUSTOM_FINAL_SHADE(
        col,
        fill,
        i.tint,
        i.rawUV,
        scenePos,
        scenePosBase,
        sceneSize,
        dist,
        coverage,
        pixelWidth,
        edge);
    #endif

    #ifdef UNITY_UI_CLIP_RECT
    float2 uiMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.uiMask.xy)) * i.uiMask.zw);
    col.a *= uiMask.x * uiMask.y;
    #endif

    col.a *= NowUIMaskCoverage(uiPosition);

    // Force source alpha to one so the existing SrcAlpha blend writes
    // the final composed coverage to RGB without squaring soft edges.
    // The cache samples red from its linear R8/ARGB target.
    if (_SdfMaskOutput > 0.5)
        return float4(col.a, col.a, col.a, 1.0);

    #ifdef UNITY_UI_ALPHACLIP
    clip(col.a - 0.001);
    #endif

    clip(col.a - 0.001);
    return col;
}

#endif
