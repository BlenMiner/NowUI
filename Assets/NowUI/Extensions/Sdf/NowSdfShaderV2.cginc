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
    {
        float2 size = max(data1.zw, 0.0001);
        float2 halfSize = size * 0.5;
        float2 glyphUv = relativeScenePos / size + 0.5;
        float boundsDist = sdBox(relativeScenePos, halfSize);

        if (glyphUv.x < 0.0 || glyphUv.y < 0.0 || glyphUv.x > 1.0 || glyphUv.y > 1.0)
            return max(data2.x, 1.0) + max(boundsDist, 0.0);

        float4 uvRect = _SdfUvs[index];
        float2 atlasUv = uvRect.xy + float2(glyphUv.x, 1.0 - glyphUv.y) * uvRect.zw;
        float4 msd = tex2D(_MainTex, atlasUv);
        return (0.5 - median(msd.r, msd.g, msd.b)) * max(data2.x, 0.0001);
    }

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

float shapeDistance(int index, float type, float4 data1, float4 data2, float2 scenePos)
{
    float2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    // The exact zero pair is the canonical identity sentinel. Keep that path on
    // the original evaluator so unrotated nodes retain their existing arithmetic.
    if (rotationLengthSquared == 0.0)
        return NowSdfUnrotatedShapeDistanceV2(index, type, data1, data2, scenePos);

    float2 pivot = NowSdfNodePivotV2(type, data1, data2);
    float2 relativeScenePos = NowSdfInverseRotateRelativeV2(
        scenePos,
        pivot,
        rotation,
        rotationLengthSquared);
    return NowSdfRotatedShapeDistanceV2(
        index,
        type,
        data1,
        data2,
        relativeScenePos,
        pivot) *
        sqrt(rotationLengthSquared);
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

void decodeGraphRange(float packedRange, out int start, out int count)
{
    int total = min(max((int)_SdfShapeCount, 0), NOW_SDF_MAX_SHAPES);
    float packed = max(packedRange, 0.0);
    float startValue = floor(packed * (1.0 / 128.0));
    start = min(max((int)startValue, 0), total);
    count = min(max((int)(packed - startValue * 128.0 + 0.5), 0), total - start);
}

void evalGraph(float packedRange, float2 scenePos, float4 tint, out float dist, out float4 fill)
{
    int start;
    int count;
    decodeGraphRange(packedRange, start, count);
    dist = 100000.0;
    fill = 0.0;

    if (count <= 0)
        return;

    int first = start;
    float4 data0 = _SdfData0[first];
    float4 data1 = _SdfData1[first];
    float4 data2 = _SdfData2[first];
    dist = shapeDistance(first, data0.x, data1, data2, scenePos);
    fill = shapeFill(first, data0.x, data1, data2, scenePos, tint);

    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];
        data1 = _SdfData1[index];
        data2 = _SdfData2[index];
        float shapeDist = shapeDistance(index, data0.x, data1, data2, scenePos);
        float4 nextFill = shapeFill(index, data0.x, data1, data2, scenePos, tint);
        combine(dist, fill, shapeDist, nextFill, data0.y, data0.z);
    }
}

void evalLayer(int index, float2 scenePos, float4 tint, out float dist, out float4 fill)
{
    // Initialize at this boundary as well as inside evalGraph. Some cross
    // compilers do not prove that out parameters are written through the
    // non-morph call before the early return.
    dist = 100000.0;
    fill = 0.0;
    float4 layer0 = _SdfLayerData0[index];
    float4 layer1 = _SdfLayerData1[index];

    if (layer0.w < 0.5)
    {
        evalGraph(layer1.z, scenePos, tint, dist, fill);
        return;
    }

    float aDist = 0;
    float bDist = 0;
    float4 aFill = 0;
    float4 bFill = 0;
    evalGraph(layer1.z, scenePos, tint, aDist, aFill);
    evalGraph(layer1.w, scenePos, tint, bDist, bFill);
    float t = saturate(layer1.y);
    dist = lerp(aDist, bDist, t);
    fill = lerp(aFill, bFill, t);
}

void evalGraphDistance(float packedRange, float2 scenePos, out float dist)
{
    int start;
    int count;
    decodeGraphRange(packedRange, start, count);
    dist = 100000.0;

    if (count <= 0)
        return;

    int first = start;
    float4 data0 = _SdfData0[first];
    dist = shapeDistance(first, data0.x, _SdfData1[first], _SdfData2[first], scenePos);

    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];
        float shapeDist = shapeDistance(index, data0.x, _SdfData1[index], _SdfData2[index], scenePos);
        combineDistance(dist, shapeDist, data0.y, data0.z);
    }
}

void evalLayerDistance(int index, float2 scenePos, out float dist)
{
    dist = 100000.0;
    float4 layer0 = _SdfLayerData0[index];
    float4 layer1 = _SdfLayerData1[index];

    if (layer0.w < 0.5)
    {
        evalGraphDistance(layer1.z, scenePos, dist);
        return;
    }

    float aDist = 100000.0;
    float bDist = 100000.0;
    evalGraphDistance(layer1.z, scenePos, aDist);
    evalGraphDistance(layer1.w, scenePos, bDist);
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

// Supported ABI-v2 helpers for custom final-shading hooks. The position passed
// to NowSdfEvaluateDistanceV2 is the unwarped, top-left/y-down scene position.
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
    float2 scenePosBase = float2(quadPos.x, i.rect.w - quadPos.y);
    float2 scenePos = warpScenePos(scenePosBase);
    float2 meshPos = i.rect.xy + quadPos;
    float2 uiPosition = float2(meshPos.x, -meshPos.y);
    float4 mask = i.mask;

    NowUIClipLegacyRect(uiPosition, mask);

    float dist = 100000.0;
    float4 fill = 0.0;
    evalScene(scenePos, i.tint, dist, fill);

    float pixelWidth = max(length(float2(ddx(dist), ddy(dist))), 0.0001);
    float edge = pixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    float effectDist = dist;
    float effectPixelWidth = pixelWidth;
    float effectEdge = edge;
    float coverage = smoothstep(edge, -edge, dist);
    float4 col = 0.0;

    if (_SdfShadowColor.a > 0.0)
    {
        float shadowDist;
        evalSceneDistance(warpScenePos(scenePosBase - _SdfShadow.xy), shadowDist);
        float shadowPixelWidth = max(length(float2(ddx(shadowDist), ddy(shadowDist))), 0.0001);
        float shadowEdge = shadowPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
        float shadowEffectDist = shadowDist - _SdfShadow.w;
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
        float innerPixelWidth = max(length(float2(ddx(innerDist), ddy(innerDist))), 0.0001);
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
        i.rect.zw,
        dist,
        coverage,
        effectPixelWidth,
        effectEdge);
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
