#ifndef NOW_UI_MASK_INCLUDED
#define NOW_UI_MASK_INCLUDED

// Analytic masks are supplied in NowUI coordinates (top-left origin, y down).
// The fixed-size arrays keep the contract compatible with material properties,
// shader globals, and render paths which cannot bind structured buffers.
#define NOW_UI_ANALYTIC_MASK_CAPACITY 8
#define NOW_UI_TEXTURE_MASK_CAPACITY 2

// Per-entry packing:
// Rects      = local-space (x, y, width, height)
// Data       = packed radii (TR, BR, TL, BL) for rounded rectangles, or
//              capsule endpoints (start.x, start.y, end.x, end.y)
// Params     = (kind, additional feather pixels, capsule radius, unused)
// Transforms = (screen origin.x, screen origin.y, signed scale.x, signed scale.y)
// Kinds: 0 = rectangle, 1 = rounded rectangle, 2 = ellipse, 3 = capsule.
float _NowUIMaskCount;
float4 _NowUIMaskRects[NOW_UI_ANALYTIC_MASK_CAPACITY];
float4 _NowUIMaskData[NOW_UI_ANALYTIC_MASK_CAPACITY];
float4 _NowUIMaskParams[NOW_UI_ANALYTIC_MASK_CAPACITY];
float4 _NowUIMaskTransforms[NOW_UI_ANALYTIC_MASK_CAPACITY];

// Texture mask packing:
// Rects      = authored local-space (x, y, width, height)
// Params     = (channel, inverted, valid texture, unused), channel 0 = alpha, 1 = red
// Transforms = (screen origin.x, screen origin.y, signed scale.x, signed scale.y)
// The samplers are explicit instead of dynamically indexed for SM3 compatibility.
float _NowUITextureMaskCount;
sampler2D _NowUITextureMask0;
sampler2D _NowUITextureMask1;
float4 _NowUITextureMaskRects[NOW_UI_TEXTURE_MASK_CAPACITY];
float4 _NowUITextureMaskParams[NOW_UI_TEXTURE_MASK_CAPACITY];
float4 _NowUITextureMaskTransforms[NOW_UI_TEXTURE_MASK_CAPACITY];

// Returns positive values inside the legacy axis-aligned clip rect. Keeping
// this as a hard clip preserves the existing NowRect mask contract exactly;
// analytic masks below add anti-aliased or feathered coverage on top.
inline float NowUILegacyRectDistance(float2 position, float4 rect)
{
    return min(
        min(position.x - rect.x, rect.x + rect.z - position.x),
        min(position.y - rect.y, rect.y + rect.w - position.y));
}

inline void NowUIClipLegacyRect(float2 position, float4 rect)
{
    clip(NowUILegacyRectDistance(position, rect));
}

// Signed-distance convention for analytic masks: negative is inside.
inline float NowUIRectMaskDistance(float2 position, float4 rect)
{
    float2 halfSize = max(abs(rect.zw) * 0.5, 0.00001);
    float2 center = rect.xy + rect.zw * 0.5;
    float2 q = abs(position - center) - halfSize;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
}

// Radii use NowCornerRadius.packed / the rectangle shader order:
// top-right, bottom-right, top-left, bottom-left. Each radius is clamped so
// malformed data cannot invert the SDF.
inline float NowUIRoundedRectMaskDistance(float2 position, float4 rect, float4 radii)
{
    float2 halfSize = max(abs(rect.zw) * 0.5, 0.00001);
    float2 local = position - (rect.xy + rect.zw * 0.5);
    float radius;

    if (local.x < 0.0)
        radius = local.y < 0.0 ? radii.z : radii.w;
    else
        radius = local.y < 0.0 ? radii.x : radii.y;

    radius = clamp(radius, 0.0, min(halfSize.x, halfSize.y));
    float2 q = abs(local) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// This normalized ellipse distance has the exact zero contour and sign. Its
// magnitude is an approximation away from the edge, which is sufficient here:
// derivative normalization below produces stable screen-pixel coverage.
inline float NowUIEllipseMaskDistance(float2 position, float4 rect)
{
    float2 halfSize = max(abs(rect.zw) * 0.5, 0.00001);
    float2 local = position - (rect.xy + rect.zw * 0.5);
    return (length(local / halfSize) - 1.0) * min(halfSize.x, halfSize.y);
}

inline float NowUICapsuleMaskDistance(float2 position, float4 endpoints, float radius)
{
    float2 from = endpoints.xy;
    float2 to = endpoints.zw;
    float2 segment = to - from;
    float segmentLengthSquared = max(dot(segment, segment), 0.00001);
    float t = saturate(dot(position - from, segment) / segmentLengthSquared);
    return length(position - (from + segment * t)) - max(radius, 0.0);
}

inline float NowUIAnalyticMaskDistance(
    float2 position,
    float4 rect,
    float4 data,
    float4 parameters)
{
    float shapeKind = parameters.x;

    if (shapeKind < 0.5)
        return NowUIRectMaskDistance(position, rect);

    if (shapeKind < 1.5)
        return NowUIRoundedRectMaskDistance(position, rect, data);

    if (shapeKind < 2.5)
        return NowUIEllipseMaskDistance(position, rect);

    return NowUICapsuleMaskDistance(position, data, parameters.z);
}

inline float2 NowUIMaskLocalPosition(float2 position, float4 maskTransform)
{
    // xy is the translation and zw is the signed scale captured when the mask
    // was pushed. Preserve the scale sign so mirrored scopes select the right
    // rounded-rectangle corners. A degenerate scale is clamped away from zero
    // to keep malformed/culling-edge data finite.
    float2 signedScale = maskTransform.zw;
    float2 safeScale = float2(
        signedScale.x < 0.0 ? min(signedScale.x, -0.00001) : max(signedScale.x, 0.00001),
        signedScale.y < 0.0 ? min(signedScale.y, -0.00001) : max(signedScale.y, 0.00001));
    return (position - maskTransform.xy) / safeScale;
}

inline float NowUIAnalyticMaskEdgeCoverage(float signedDistance, float featherPixels)
{
    // A zero feather still gets one screen pixel of derivative AA. Feather is
    // additional screen-pixel softness, matching the public SDF convention.
    float distancePerPixel = max(fwidth(signedDistance), 0.00001);
    float transitionPixels = 1.0 + max(featherPixels, 0.0);
    float halfBand = 0.5 * transitionPixels * distancePerPixel;
    return 1.0 - smoothstep(-halfBand, halfBand, signedDistance);
}

inline float NowUIAnalyticMaskCoverage(float2 position)
{
    float coverage = 1.0;
    int maskCount = (int)clamp(floor(_NowUIMaskCount + 0.5), 0.0, (float)NOW_UI_ANALYTIC_MASK_CAPACITY);

    [unroll]
    for (int maskIndex = 0; maskIndex < NOW_UI_ANALYTIC_MASK_CAPACITY; ++maskIndex)
    {
        if (maskIndex >= maskCount)
            break;

        float4 parameters = _NowUIMaskParams[maskIndex];
        float2 localPosition = NowUIMaskLocalPosition(
            position,
            _NowUIMaskTransforms[maskIndex]);
        float signedDistance = NowUIAnalyticMaskDistance(
            localPosition,
            _NowUIMaskRects[maskIndex],
            _NowUIMaskData[maskIndex],
            parameters);
        float shapeCoverage = NowUIAnalyticMaskEdgeCoverage(signedDistance, parameters.y);
        coverage = min(coverage, shapeCoverage);
    }

    return coverage;
}

inline float NowUITextureMaskSampleCoverage(
    float2 position,
    float4 rect,
    float4 parameters,
    float4 maskTransform,
    sampler2D coverageTexture)
{
    // Missing/destroyed textures are bound to black as well as marked invalid.
    // Check validity before inversion so an empty source always remains empty.
    if (parameters.z < 0.5 || rect.z <= 0.0 || rect.w <= 0.0)
        return 0.0;

    float2 localPosition = NowUIMaskLocalPosition(position, maskTransform);
    float2 normalized = (localPosition - rect.xy) / max(rect.zw, 0.00001);
    float inside =
        step(0.0, normalized.x) * step(normalized.x, 1.0) *
        step(0.0, normalized.y) * step(normalized.y, 1.0);

    // NowUI is top-left/y-down while texture UVs are bottom-left/y-up.
    float2 uv = saturate(float2(normalized.x, 1.0 - normalized.y));
    float4 sampleValue = tex2D(coverageTexture, uv);
    float channelCoverage = parameters.x < 0.5 ? sampleValue.a : sampleValue.r;
    channelCoverage = parameters.y > 0.5 ? 1.0 - channelCoverage : channelCoverage;
    return saturate(channelCoverage) * inside;
}

inline float NowUITextureMaskCoverage(float2 position)
{
    int maskCount = (int)clamp(
        floor(_NowUITextureMaskCount + 0.5),
        0.0,
        (float)NOW_UI_TEXTURE_MASK_CAPACITY);

    if (maskCount <= 0)
        return 1.0;

    float coverage = NowUITextureMaskSampleCoverage(
        position,
        _NowUITextureMaskRects[0],
        _NowUITextureMaskParams[0],
        _NowUITextureMaskTransforms[0],
        _NowUITextureMask0);

    if (maskCount > 1)
    {
        coverage = min(
            coverage,
            NowUITextureMaskSampleCoverage(
                position,
                _NowUITextureMaskRects[1],
                _NowUITextureMaskParams[1],
                _NowUITextureMaskTransforms[1],
                _NowUITextureMask1));
    }

    return coverage;
}

inline float NowUIMaskCoverage(float2 position)
{
    if (_NowUIMaskCount < 0.5 && _NowUITextureMaskCount < 0.5)
        return 1.0;

    return min(
        NowUIAnalyticMaskCoverage(position),
        NowUITextureMaskCoverage(position));
}

#endif
