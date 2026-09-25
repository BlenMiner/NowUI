#version 300 es
// ===========================================================================
// nowui-sdf.frag
//
// GLSL ES 3.00 port of the fragment half of
// Assets/NowUI/Extensions/Sdf/NowSdfShaderV2.cginc (1725 lines), the shader
// behind "NowUI/SDF Scene". Line citations below are that file.
//
// COMPOSITION: see nowui-sdf.vert's header. This stage includes
// nowui-mask.glsl.
//
// ---------------------------------------------------------------------------
// WHAT THIS PROGRAM IS
//
// Every other NowUI shader draws ONE shape whose parameters arrive per vertex.
// This one is an INTERPRETER. The C# side (NowSdfCache.Upload,
// NowSdf.cs:4835-4906) packs a whole scene graph -- up to 64 shape nodes and
// 16 layers -- into nine uniform vec4 arrays, and this fragment stage walks
// them per pixel: evaluate every node's signed distance, fold them together
// with the per-node boolean operation, fold the layers together the same way,
// then shade the resulting field with outline, glow, shadow, inner shadow,
// emboss and contour bands.
//
// ---------------------------------------------------------------------------
// THE FIVE THINGS MOST LIKELY TO BE GOT WRONG HERE
//
// 1. ARRAY CAPACITIES ARE STRUCTURAL, NOT ADVISORY. NowSdf.MaxShapes is 64 and
//    NowSdf.MaxLayers is 16 (NowSdf.cs:2343-2344), the scratch arrays are
//    allocated at exactly those lengths (NowSdf.cs:3326-3334), and
//    Material.SetVectorArray copies the WHOLE array regardless of how many
//    entries the scene filled. So what reaches the backend is always
//    Vector4[64] / Vector4[16]. Declare [64] and [16] here and never size
//    either from _SdfShapeCount or _SdfLayerCount -- exactly the rule
//    NowUIMask.cginc's [8]/[2] already established (M2-ShaderPort.md 5.2).
//
// 2. LOOP BOUNDS ARE COMPILE-TIME CONSTANTS AND THE COUNT IS A `break`. The
//    HLSL already has this shape (:974-976, :1291-1293, :1348-1352) so the
//    port is a transcription rather than a restructuring -- but it is worth
//    naming, because GLSL ES 3.00 is stricter than the HLSL is and a
//    `i < count` bound would be the natural thing to write.
//
// 3. `sample` IS A RESERVED WORD in GLSL ES 3.00. NowSdfGlyphSamplesV2's first
//    parameter is called exactly that (:238). Renamed to `texel` here; there
//    is no way to keep the HLSL name.
//
// 4. `distance` IS A BUILT-IN FUNCTION. The HLSL uses it as a local variable
//    name in four places. Shadowing is legal GLSL, but the built-in is then
//    unreachable in that scope, so the locals are renamed (`signedDist`,
//    `fieldDistance`) rather than left as a trap for the next edit.
//
// 5. THE BLEND IS *NOT* THE SHARED PREMULTIPLIED ONE. NowSdf.shader:58 says
//    `Blend SrcAlpha OneMinusSrcAlpha`, and this stage returns STRAIGHT alpha
//    (:1719 returns `col` with no `col.rgb *= col.a`). Drawn with the
//    backend's shared ONE / ONE_MINUS_SRC_ALPHA the whole scene comes out
//    darkened by its own coverage -- a plausible-looking picture. The state is
//    recorded in BLEND_STATES / BLEND_MODES so it cannot drift.
//
// ---------------------------------------------------------------------------
// WHAT IS NOT PORTED, AND WHY IT CANNOT SILENTLY PASS
//
//   * `NOW_SDF_CUSTOM_FINAL_SHADE` (:1508-1524, :1697-1710). A hook for
//     project-authored materials (the three under Extensions/Sdf/Examples use
//     it). Those are separate shader programs with their own names, so a
//     material that needs the hook never resolves to THIS program -- it
//     resolves to "NowUI/SDF Examples/Aurora" and the backend refuses it by
//     name. The hook is therefore unreachable here rather than stubbed.
//     Its three public helpers go with it -- NowSdfAlphaOverV2,
//     NowSdfEvaluateDistanceV2 and NowSdfEvaluateEffectDistanceV2 (:1454-1476)
//     are one-line wrappers that exist only for a custom include to call. What
//     they wrap -- alphaOver, evalSceneDistance and
//     evalSceneEffectDistanceAndCodeStep -- is all ported below, so restoring
//     them is three lines if a later slice ports one of the example shaders.
//   * `UNITY_UI_CLIP_RECT` / `UNITY_UI_ALPHACLIP` (:1712-1717). UGUI keyword
//     variants; the `_` variant is what every material this host builds
//     selects. See nowui-sdf.vert's omission note.
//   * The IMAGE node type (type 10) evaluates here in full, but its atlases
//     are produced by "Hidden/NowUI/SDF Image Field", which is NOT ported.
//     _SdfImageField / _SdfImageColor therefore resolve to the 1x1 opaque
//     BLACK fallback, which is what the shader's own Properties defaults say
//     ("black", NowSdf.shader:6-7) and what NowSdfCache.Upload substitutes
//     when there is no atlas (NowSdf.cs:4881-4882). A field of 0 makes
//     NowSdfImageLocalDistanceV2 return 0 inside the padded rect -- the shape
//     reads as a filled rectangle. The backend logs once when an Image node is
//     uploaded, so this cannot pass as a rendering result.
//
// ---------------------------------------------------------------------------
// PRECISION. highp throughout, and not decoratively: `dist` runs to 1e5 (the
// empty-field sentinel) while the antialiasing band it is compared against is
// a fraction of a scene unit, and mediump's 10-bit mantissa cannot hold both.
// The glyph branch reconstructs a 16-bit distance as `r * 256.0 + b`, the same
// arithmetic mediump destroys in nowui-text.frag. `precision highp sampler2D`
// matters because the fragment default for a sampler is lowp and this stage
// declares five of them.
// ===========================================================================

precision highp float;
precision highp int;
precision highp sampler2D;

//#include "nowui-mask.glsl"
//#include "nowui-colorspace.glsl"
//#include "nowui-text-gradient.glsl"

// ---------------------------------------------------------------------------
// HLSL intrinsics with no GLSL twin. `saturate` appears 30+ times in the
// cginc; writing clamp(x, 0.0, 1.0) at each site would make the port harder to
// diff against its source than it already is.
// ---------------------------------------------------------------------------

float saturate(float x) { return clamp(x, 0.0, 1.0); }
vec2 saturate(vec2 x) { return clamp(x, vec2(0.0), vec2(1.0)); }
vec3 saturate(vec3 x) { return clamp(x, vec3(0.0), vec3(1.0)); }
vec4 saturate(vec4 x) { return clamp(x, vec4(0.0), vec4(1.0)); }

// ---------------------------------------------------------------------------
// Capacities. :12-13.
// ---------------------------------------------------------------------------

#define NOW_SDF_MAX_SHAPES 64
#define NOW_SDF_MAX_LAYERS 16

// ---------------------------------------------------------------------------
// Varyings -- must match nowui-sdf.vert's `out` block exactly.
// ---------------------------------------------------------------------------

in highp vec2 vRawUV;
in highp vec4 vRect;
in highp vec4 vMask;
in highp vec4 vTint;
in highp vec4 vSceneMapping;

out highp vec4 fragColor;

// ---------------------------------------------------------------------------
// Uniforms. :43-81, in the cginc's own order.
//
// _MainTex is the fill texture for SetTexture() nodes AND the font atlas for
// Text() nodes -- one sampler serving both, which is why image nodes were
// given their own pair (:44-48).
// ---------------------------------------------------------------------------

uniform highp sampler2D _MainTex;
uniform highp sampler2D _SdfImageField;
uniform highp sampler2D _SdfImageColor;
uniform highp vec4 _SdfImageAtlasSize;

uniform highp float _SdfShapeCount;
uniform highp float _SdfLayerCount;
uniform highp float _SdfFeather;

// NOT in NowSdf.shader's Properties block -- NowSdfCache.Upload sets it
// directly (NowSdf.cs:4876). Its zero default is WRONG rather than merely
// unset: `hasFiniteTextEffectLimit` is `_SdfTextEffectLimit < 100000.0`, so a
// zero here claims every exterior effect must fade at distance 0 and erases
// outline, glow, shadow and contours from an analytic scene. The backend
// substitutes 100000 and says so once; see WebGL2Backend's SDF block.
uniform highp float _SdfTextEffectLimit;

uniform highp vec4 _SdfOutline;
uniform highp vec4 _SdfOutlineColor;
uniform highp vec4 _SdfGlow;
uniform highp vec4 _SdfGlowColor;
uniform highp vec4 _SdfShadow;
uniform highp vec4 _SdfShadowColor;
uniform highp vec4 _SdfShadow2;
uniform highp vec4 _SdfShadow2Color;
uniform highp vec4 _SdfInnerShadow;
uniform highp vec4 _SdfInnerShadowColor;
uniform highp vec4 _SdfEmboss;
uniform highp vec4 _SdfContour;
uniform highp vec4 _SdfContourColor;
uniform highp vec4 _SdfContourMask;
uniform highp vec4 _SdfWarp;
uniform highp float _SdfMaskOutput;

// Unity's `_Time.y` (:1414), the only built-in this stage reads. Renamed
// because the backend supplies it explicitly rather than through a Unity
// global block. Read ONLY by warpScenePos, whose guard is `_SdfWarp.x <= 0.0`
// -- and _SdfWarp defaults to (0, 1, 0, 0), so the whole clock dependency is
// dormant unless a scene calls SetWarp.
uniform highp float nowui_Time;

// :74-82. Nine arrays, seven at shape capacity and two at layer capacity.
// See "THE FIVE THINGS", point 1.
uniform highp vec4 _SdfData0[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfData1[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfData2[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfShapeMeta[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfColors[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfUvs[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfImageUvs[NOW_SDF_MAX_SHAPES];
uniform highp vec4 _SdfLayerData0[NOW_SDF_MAX_LAYERS];
uniform highp vec4 _SdfLayerData1[NOW_SDF_MAX_LAYERS];

// ---------------------------------------------------------------------------
// Primitive distance functions. :84-116.
// ---------------------------------------------------------------------------

float sdBox(vec2 p, vec2 b)
{
    vec2 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
}

// :89. THE Y SENSE, derived rather than guessed. SCENE space is TOP-LEFT /
// Y-DOWN -- the cginc says so at :1450 ("unwarped, top-left/y-down scene
// positions") and main() below builds it that way: rawUV.y == 1 is the UI TOP
// (M2-ShaderPort.md 1.3), and `sceneSize.y - sceneQuadPos.y` turns that into
// scene y == 0. So `p.y < 0.0` is the TOP half, and the selector reads
// left/top -> r.z, left/bottom -> r.w, right/top -> r.x, right/bottom -> r.y,
// i.e. the (TR, BR, TL, BL) packing NowUIMask.cginc:12-13 documents. It agrees
// with the mask include's rounded-rect selector, which works in the same
// y-down sense, and DISAGREES with UIRectangle's sdRoundedBox, which works in
// a y-up local space and therefore tests `p.y > 0` for the same corner.
// Getting it backwards rotates rounded corners 180 degrees in y, which a
// symmetric shape hides completely.
float sdRoundBox(vec2 p, vec2 b, vec4 r)
{
    float radius;

    if (p.x < 0.0)
        radius = p.y < 0.0 ? r.z : r.w;
    else
        radius = p.y < 0.0 ? r.x : r.y;

    radius = min(radius, min(b.x, b.y));
    vec2 q = abs(p) - b + radius;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
}

float sdEllipse(vec2 p, vec2 radius)
{
    radius = max(radius, 0.0001);
    return (length(p / radius) - 1.0) * min(radius.x, radius.y);
}

float sdCapsule(vec2 p, vec2 a, vec2 b, float r)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
    return length(pa - ba * h) - r;
}

// :118-141. Chamfered box and the triangle below adapted from Inigo Quilez's
// 2D distance functions (MIT), https://iquilezles.org/articles/distfunctions2d/
// Copyright (c) 2019 Inigo Quilez. See THIRD_PARTY_LICENSES.md.
float NowSdfChamferedBoxDistanceV2(vec2 p, vec2 halfSize, float chamfer)
{
    halfSize = max(halfSize, 0.0001);
    chamfer = clamp(chamfer, 0.0, min(halfSize.x, halfSize.y));
    vec2 q = abs(p) - halfSize;

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

// :143-159. The point a rotated node turns about. Capsules (type 4) turn about
// their segment midpoint and triangles (type 9) about their bounding-box
// centre; everything else turns about data1.xy.
vec2 NowSdfNodePivotV2(float type, vec4 data1, vec4 data2)
{
    if (type > 3.5 && type < 4.5)
        return data1.xy * 0.5 + data1.zw * 0.5;

    if (type > 8.5 && type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        vec2 a = data1.xy;
        vec2 b = a + data1.zw * scale;
        vec2 c = a + data2.xy * scale;
        vec2 minPoint = min(a, min(b, c));
        vec2 maxPoint = max(a, max(b, c));
        return minPoint + (maxPoint - minPoint) * 0.5;
    }

    return data1.xy;
}

// :161-174. The rotation vector carries SCALE as well as angle; dividing by
// its squared length is what undoes both at once.
vec2 NowSdfInverseRotateRelativeV2(
    vec2 scenePos,
    vec2 pivot,
    vec2 rotation,
    float rotationLengthSquared)
{
    vec2 p = scenePos - pivot;

    return vec2(
        p.x * rotation.x + p.y * rotation.y,
        -p.x * rotation.y + p.y * rotation.x) / rotationLengthSquared;
}

float NowSdfSegmentDistanceSquaredV2(vec2 p, vec2 a, vec2 b)
{
    vec2 edge = b - a;
    float denominator = dot(edge, edge);

    if (denominator <= 0.0)
        return dot(p - a, p - a);

    float t = saturate(dot(p - a, edge) / denominator);
    vec2 nearest = p - a - edge * t;
    return dot(nearest, nearest);
}

// :188. `distance` renamed to `signedDist`: see "THE FIVE THINGS", point 4.
float NowSdfTriangleDistanceV2(vec2 p, vec2 b, vec2 c, float orientationSign)
{
    const vec2 a = vec2(0.0, 0.0);
    float distanceSquared = min(
        NowSdfSegmentDistanceSquaredV2(p, a, b),
        min(
            NowSdfSegmentDistanceSquaredV2(p, b, c),
            NowSdfSegmentDistanceSquaredV2(p, c, a)));
    // A collapsed or numerically near-collinear triangle has no interior, but
    // remains a useful segment/point field instead of producing an unstable
    // sign.
    if (abs(orientationSign) < 0.5)
        return sqrt(max(distanceSquared, 0.0));

    float ab = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    float bc = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
    float ca = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
    bool inside = orientationSign > 0.0
        ? (ab >= 0.0 && bc >= 0.0 && ca >= 0.0)
        : (ab <= 0.0 && bc <= 0.0 && ca <= 0.0);
    float signedDist = sqrt(max(distanceSquared, 0.0));
    return inside ? -signedDist : signedDist;
}

vec2 NowSdfRotateRadialV2(vec2 p, vec2 rotation)
{
    return vec2(
        p.x * rotation.x - p.y * rotation.y,
        p.x * rotation.y + p.y * rotation.x);
}

// :213-224. Arc and pie, also from Inigo Quilez (MIT).
float NowSdfArcDistanceV2(vec2 p, vec2 sc, float ra, float rb)
{
    p.x = abs(p.x);
    return ((sc.y * p.x > sc.x * p.y) ? length(p - sc * ra) : abs(length(p) - ra)) - rb;
}

// Butt caps end the band flat along the radius at each end; square caps add a
// half-width box beyond each flat end. cap: 0 round, 1 butt, 2 square.
float NowSdfCappedArcDistanceV2(vec2 p, vec2 sc, float ra, float rb, float cap)
{
    if (cap < 0.5)
        return NowSdfArcDistanceV2(p, sc, ra, rb);

    p.x = abs(p.x);
    // Positive past the end face, negative inside the sweep.
    float beyond = sc.y * p.x - sc.x * p.y;
    float d = beyond > 0.0
        ? length(p - sc * clamp(dot(p, sc), ra - rb, ra + rb))
        : max(abs(length(p) - ra) - rb, beyond);

    if (cap > 1.5)
    {
        vec2 tangent = vec2(sc.y, -sc.x);
        vec2 local = p - sc * ra;
        vec2 q = abs(vec2(dot(local, tangent) - rb * 0.5, dot(local, sc))) - vec2(rb * 0.5, rb);
        d = min(d, length(max(q, 0.0)) + min(max(q.x, q.y), 0.0));
    }

    return d;
}

float NowSdfPieDistanceV2(vec2 p, vec2 sc, float r)
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

// :238. `sample` -> `texel`: see "THE FIVE THINGS", point 3.
//
// Managed dynamic pages keep a scalar SDF in two RGBA8 channels. The high byte
// is repeated in R/G/A so legacy consumers retain a useful 8-bit field; B
// carries the low byte for NowUI-aware shaders. Native MTSDF pages use median
// RGB at the fill edge and their true-distance alpha for exterior effects,
// where median RGB is not stable far from corners.
vec2 NowSdfGlyphSamplesV2(vec4 texel, float encoding)
{
    if (encoding > 0.5)
    {
        float packedSample = (texel.r * 256.0 + texel.b) / 257.0;
        return vec2(packedSample, packedSample);
    }

    return vec2(median(texel.r, texel.g, texel.b), texel.a);
}

float NowSdfGlyphSampleV2(vec4 texel, float encoding)
{
    return NowSdfGlyphSamplesV2(texel, encoding).x;
}

// :264. Image fields store float distances, so quantization is negligible; a
// small nonzero step still marks the field as finite for exterior-effect
// fading.
#define NOW_SDF_IMAGE_CODE_STEP 0.002

float NowSdfShapeCodeStepV2(float type, vec4 data2)
{
    if (type > 4.5 && type < 5.5)
        return max(data2.z, 0.0);

    if (type > 9.5 && type < 10.5)
        return max(min(data2.x, data2.y), 0.0001) * NOW_SDF_IMAGE_CODE_STEP;

    return 0.0;
}

// :278-286. Maps a clamped 0..1 uv (y up) onto an atlas entry's texel rect,
// staying at least half a texel inside the entry so bilinear filtering never
// reads the gutter around it.
vec2 NowSdfAtlasUvV2(vec2 uv, vec4 texelRect, vec2 atlasSize)
{
    // HLSL's clamp broadcasts a scalar bound against a vector one; GLSL's does
    // not -- `clamp(genType, float, float)` and `clamp(genType, genType,
    // genType)` are the only two overloads, so the 0.5 has to become vec2(0.5)
    // to sit beside a vec2 upper bound. This is the one HLSL implicit-broadcast
    // rule in this file that GLSL refuses outright, which is the good outcome.
    vec2 texel = clamp(uv * texelRect.zw, vec2(0.5), max(texelRect.zw - 0.5, 0.5));
    return (texelRect.xy + texel) / max(atlasSize, 1.0);
}

// :288-307. data1.zw: image rect size, data2.xy: scene units per source texel,
// data2.z: field padding in texels, fieldRect: texel rect in the field atlas.
// The field covers the image rect plus the padding on every side; beyond it
// the distance continues from the clamped border sample so the padded quad
// boundary cannot become a false edge.
float NowSdfImageLocalDistanceV2(vec2 local, vec2 size, vec4 data2, vec4 fieldRect)
{
    vec2 texelScale = max(data2.xy, 0.0001);
    float pad = max(data2.z, 0.0);
    vec2 fieldSize = max(size + 2.0 * pad * texelScale, 0.0001);
    vec2 uv = saturate(local / fieldSize + 0.5);
    vec2 atlasUv = NowSdfAtlasUvV2(vec2(uv.x, 1.0 - uv.y), fieldRect, _SdfImageAtlasSize.xy);
    // tex2Dlod(s, float4(uv, 0, 0)) -> textureLod(s, uv, 0.0). An explicit LOD
    // rather than `texture`, because this call sits under dynamic control flow
    // where implicit derivatives are undefined -- which is exactly why the
    // HLSL asks for lod 0 too.
    float texelDistance = textureLod(_SdfImageField, atlasUv, 0.0).r;
    float signedDist = texelDistance * min(texelScale.x, texelScale.y);
    float boundsDist = sdBox(local, fieldSize * 0.5);
    return boundsDist > 0.0 ? max(signedDist, 0.0) + boundsDist : signedDist;
}

// :309-334. The two returned distances differ ONLY for native MTSDF glyph
// pages: .x is the fill-edge field and .y the true-distance field the exterior
// effects want. Under the packed SDF16 encoding this build bakes, they are the
// same number.
vec2 NowSdfGlyphLocalDistancesV2(
    vec2 local,
    vec2 size,
    vec4 data2,
    vec4 uvRect)
{
    size = max(size, 0.0001);
    vec2 halfSize = size * 0.5;
    vec2 glyphUv = local / size + 0.5;
    float boundsDist = sdBox(local, halfSize);

    if (glyphUv.x < 0.0 || glyphUv.y < 0.0 || glyphUv.x > 1.0 || glyphUv.y > 1.0)
    {
        // The encoded field spans +/- half of its full range. Continue from
        // that saturated exterior value so dFdx/dFdy cannot turn the glyph-quad
        // boundary into a false antialiased outline.
        float outsideDistance = 0.5 * max(data2.x, 0.0001) + max(boundsDist, 0.0);
        return vec2(outsideDistance, outsideDistance);
    }

    vec2 atlasUv = uvRect.xy + vec2(glyphUv.x, 1.0 - glyphUv.y) * uvRect.zw;
    vec4 msd = texture(_MainTex, atlasUv);
    return (0.5 - NowSdfGlyphSamplesV2(msd, data2.y)) * max(data2.x, 0.0001);
}

vec2 sdGlyphDistances(vec2 scenePos, vec4 data1, vec4 data2, vec4 uvRect)
{
    return NowSdfGlyphLocalDistancesV2(scenePos - data1.xy, data1.zw, data2, uvRect);
}

float sdGlyph(vec2 scenePos, vec4 data1, vec4 data2, vec4 uvRect)
{
    return sdGlyphDistances(scenePos, data1, data2, uvRect).x;
}

// ---------------------------------------------------------------------------
// The node dispatch. :341-405 (unrotated) and :407-... (rotated).
//
// `type` is data0.x, a float holding a small integer, and every test is a
// half-open float comparison rather than an integer switch -- kept exactly as
// written, because the encoding is the ABI. NowSdfShapeType order:
//   0 circle, 1 box, 2 rounded box, 3 ellipse, 4 capsule, 5 glyph,
//   6 arc, 7 pie, 8 chamfered box, 9 triangle, 10 image.
// ---------------------------------------------------------------------------

float NowSdfUnrotatedShapeDistanceV2(
    int index,
    float type,
    vec4 data1,
    vec4 data2,
    vec2 scenePos)
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
        vec2 radial = scenePos - data1.xy;

        // A zero rotation vector is the explicit full-turn sentinel. Bypass the
        // aperture formula so exact and clamped full pies cannot develop a sign
        // seam.
        if (dot(data2.zw, data2.zw) < 0.5)
        {
            if (type < 6.5)
                return abs(length(radial) - data1.z) - data1.w;

            return length(radial) - data1.z;
        }

        vec2 q = NowSdfRotateRadialV2(radial, data2.zw);

        if (type < 6.5)
            return NowSdfCappedArcDistanceV2(q, data2.xy, data1.z, data1.w, floor(_SdfShapeMeta[index].y * 0.5));

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
        vec2 normalizedPosition = (scenePos - data1.xy) / scale;
        return NowSdfTriangleDistanceV2(
            normalizedPosition,
            data1.zw,
            data2.xy,
            data2.z) * scale;
    }

    if (type < 10.5)
        return NowSdfImageLocalDistanceV2(scenePos - data1.xy, data1.zw, data2, _SdfImageUvs[index]);

    return 100000.0;
}

float NowSdfRotatedShapeDistanceV2(
    int index,
    float type,
    vec4 data1,
    vec4 data2,
    vec2 relativeScenePos,
    vec2 pivot)
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
        if (dot(data2.zw, data2.zw) < 0.5)
        {
            if (type < 6.5)
                return abs(length(relativeScenePos) - data1.z) - data1.w;

            return length(relativeScenePos) - data1.z;
        }

        vec2 q = NowSdfRotateRadialV2(relativeScenePos, data2.zw);

        if (type < 6.5)
            return NowSdfCappedArcDistanceV2(q, data2.xy, data1.z, data1.w, floor(_SdfShapeMeta[index].y * 0.5));

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
        vec2 normalizedPosition = (relativeScenePos + (pivot - data1.xy)) / scale;
        return NowSdfTriangleDistanceV2(
            normalizedPosition,
            data1.zw,
            data2.xy,
            data2.z) * scale;
    }

    if (type < 10.5)
        return NowSdfImageLocalDistanceV2(relativeScenePos, data1.zw, data2, _SdfImageUvs[index]);

    return 100000.0;
}

vec2 NowSdfUnrotatedShapeDistancesV2(
    int index,
    float type,
    vec4 data1,
    vec4 data2,
    vec2 scenePos)
{
    if (type > 4.5 && type < 5.5)
        return sdGlyphDistances(scenePos, data1, data2, _SdfUvs[index]);

    float signedDist = NowSdfUnrotatedShapeDistanceV2(index, type, data1, data2, scenePos);
    return vec2(signedDist, signedDist);
}

vec2 NowSdfRotatedShapeDistancesV2(
    int index,
    float type,
    vec4 data1,
    vec4 data2,
    vec2 relativeScenePos,
    vec2 pivot)
{
    if (type > 4.5 && type < 5.5)
    {
        return NowSdfGlyphLocalDistancesV2(
            relativeScenePos,
            data1.zw,
            data2,
            _SdfUvs[index]);
    }

    float signedDist = NowSdfRotatedShapeDistanceV2(
        index,
        type,
        data1,
        data2,
        relativeScenePos,
        pivot);
    return vec2(signedDist, signedDist);
}

// :528. The exact zero rotation pair is the canonical identity sentinel; that
// path keeps the original evaluator so unrotated nodes retain their existing
// arithmetic bit for bit.
vec2 shapeDistances(int index, float type, vec4 data1, vec4 data2, vec2 scenePos)
{
    vec2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    if (rotationLengthSquared == 0.0)
        return NowSdfUnrotatedShapeDistancesV2(index, type, data1, data2, scenePos);

    vec2 pivot = NowSdfNodePivotV2(type, data1, data2);
    vec2 relativeScenePos = NowSdfInverseRotateRelativeV2(
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

float shapeDistance(int index, float type, vec4 data1, vec4 data2, vec2 scenePos)
{
    return shapeDistances(index, type, data1, data2, scenePos).x;
}

float NowSdfTransformedShapeCodeStepV2(int index, float type, vec4 data2)
{
    float codeStep = NowSdfShapeCodeStepV2(type, data2);
    vec2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);
    return rotationLengthSquared == 0.0
        ? codeStep
        : codeStep * sqrt(rotationLengthSquared);
}

// ---------------------------------------------------------------------------
// Per-node UV, for textured fills. :565-731.
// ---------------------------------------------------------------------------

vec2 NowSdfRotatedShapeUvV2(
    float type,
    vec4 data1,
    vec4 data2,
    vec2 relativeScenePos,
    vec2 pivot)
{
    vec2 minPoint;
    vec2 maxPoint;

    if (type < 0.5)
    {
        minPoint = -data1.zz;
        maxPoint = data1.zz;
    }
    else if (type < 3.5)
    {
        vec2 halfSize = data1.zw * 0.5;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 4.5)
    {
        vec2 a = data1.xy - pivot;
        vec2 b = data1.zw - pivot;
        minPoint = min(a, b) - data2.xx;
        maxPoint = max(a, b) + data2.xx;
    }
    else if (type < 7.5)
    {
        float extent = type < 6.5 ? data1.z + data1.w : data1.z;
        minPoint = -vec2(extent, extent);
        maxPoint = vec2(extent, extent);
    }
    else if (type < 8.5)
    {
        vec2 halfSize = data1.zw * 0.5;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        vec2 normalizedPosition = (relativeScenePos + (pivot - data1.xy)) / scale;
        vec2 minNormalized = min(vec2(0.0, 0.0), min(data1.zw, data2.xy));
        vec2 maxNormalized = max(vec2(0.0, 0.0), max(data1.zw, data2.xy));
        vec2 span = maxNormalized - minNormalized;
        vec2 uv;
        uv.x = span.x > 0.0
            ? saturate((normalizedPosition.x - minNormalized.x) / span.x)
            : 0.5;
        uv.y = span.y > 0.0
            ? saturate((normalizedPosition.y - minNormalized.y) / span.y)
            : 0.5;
        return vec2(uv.x, 1.0 - uv.y);
    }
    else if (type < 10.5)
    {
        vec2 halfSize = data1.zw * 0.5;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else
    {
        return vec2(0.5, 0.5);
    }

    vec2 uv = saturate(
        (relativeScenePos - minPoint) /
        max(maxPoint - minPoint, 0.0001));
    return vec2(uv.x, 1.0 - uv.y);
}

vec2 shapeUv(int index, float type, vec4 data1, vec4 data2, vec2 scenePos)
{
    vec2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    if (rotationLengthSquared != 0.0)
    {
        vec2 pivot = NowSdfNodePivotV2(type, data1, data2);
        vec2 relativeScenePos = NowSdfInverseRotateRelativeV2(
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

    vec2 minPoint;
    vec2 maxPoint;

    if (type < 0.5)
    {
        minPoint = data1.xy - data1.zz;
        maxPoint = data1.xy + data1.zz;
    }
    else if (type < 3.5)
    {
        vec2 halfSize = data1.zw * 0.5;
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
        minPoint = data1.xy - vec2(extent, extent);
        maxPoint = data1.xy + vec2(extent, extent);
    }
    else if (type < 8.5)
    {
        vec2 halfSize = data1.zw * 0.5;
        scenePos -= data1.xy;
        minPoint = -halfSize;
        maxPoint = halfSize;
    }
    else if (type < 9.5)
    {
        float scale = max(data2.w, 1.17549435e-38);
        vec2 normalizedPosition = (scenePos - data1.xy) / scale;
        vec2 minNormalized = min(vec2(0.0, 0.0), min(data1.zw, data2.xy));
        vec2 maxNormalized = max(vec2(0.0, 0.0), max(data1.zw, data2.xy));
        vec2 span = maxNormalized - minNormalized;
        vec2 uv;
        uv.x = span.x > 0.0
            ? saturate((normalizedPosition.x - minNormalized.x) / span.x)
            : 0.5;
        uv.y = span.y > 0.0
            ? saturate((normalizedPosition.y - minNormalized.y) / span.y)
            : 0.5;
        return vec2(uv.x, 1.0 - uv.y);
    }
    else if (type < 10.5)
    {
        vec2 halfSize = data1.zw * 0.5;
        minPoint = data1.xy - halfSize;
        maxPoint = data1.xy + halfSize;
    }
    else
    {
        return vec2(0.5, 0.5);
    }

    vec2 uv = saturate((scenePos - minPoint) / max(maxPoint - minPoint, 0.0001));
    return vec2(uv.x, 1.0 - uv.y);
}

// NowSdfGradientFillV2. _SdfData0.w packs the ramp atlas row and flags (0
// means no gradient) and _SdfImageUvs, which only Image nodes use otherwise,
// the payload, both resolved by the CPU against the node's unrotated box in
// scene units. The ramp stays in the authored color space, like _SdfColors, so
// NowUITextGradientSample (which converts to working space) is not used.
vec4 NowSdfGradientFillV2(int index, float type, vec4 data1, vec4 data2, vec2 scenePos, float encodedRamp)
{
    vec2 rotation = _SdfShapeMeta[index].zw;
    float rotationLengthSquared = dot(rotation, rotation);

    if (rotationLengthSquared != 0.0)
    {
        vec2 pivot = NowSdfNodePivotV2(type, data1, data2);
        scenePos = pivot + NowSdfInverseRotateRelativeV2(
            scenePos,
            pivot,
            rotation,
            rotationLengthSquared);
    }

    float row = floor(encodedRamp);
    float flags = NowUITextGradientFlags(encodedRamp);
    float spread = hlslFmod(floor(flags / 4.0), 4.0);
    float fixedMode = hlslFmod(floor(flags / 32.0), 2.0);
    float t = NowUITextGradientApplySpread(
        NowUITextGradientPosition(scenePos, _SdfImageUvs[index], flags),
        spread);
    float rampIndex = fixedMode > 0.5 ? floor(t * 255.0 + 0.5) : t * 255.0;
    vec2 rampUv = vec2((rampIndex + 0.5) / 256.0, (row + 0.5) / 256.0);
    return textureLod(_NowGradientRampTexture, rampUv, 0.0);
}

// :733-753. Straight-alpha fill for one node.
vec4 shapeFill(int index, float type, vec4 data1, vec4 data2, vec2 scenePos, vec4 tint)
{
    vec4 color = _SdfColors[index] * tint;
    float encodedRamp = _SdfData0[index].w;

    if (encodedRamp >= 1.0)
        return NowSdfGradientFillV2(index, type, data1, data2, scenePos, encodedRamp) * tint;

    // Image nodes sample their own pixels from the scene's color atlas, so
    // they never compete with text or SetTexture fills for _MainTex.
    if (type > 9.5 && type < 10.5)
    {
        vec2 imageUv = shapeUv(index, type, data1, data2, scenePos);
        vec2 atlasUv = NowSdfAtlasUvV2(imageUv, _SdfUvs[index], _SdfImageAtlasSize.zw);
        return textureLod(_SdfImageColor, atlasUv, 0.0) * color;
    }

    // Glyph nodes never take a texture fill (their _SdfUvs entry is the atlas
    // rect, not a fill rect), and _SdfShapeMeta.y is the "has texture" flag.
    if ((type > 4.5 && type < 5.5) || mod(_SdfShapeMeta[index].y, 2.0) < 0.5)
        return color;

    vec2 uv = shapeUv(index, type, data1, data2, scenePos);
    vec4 uvRect = _SdfUvs[index];
    uv = uvRect.xy + uv * uvRect.zw;
    return texture(_MainTex, uv) * color;
}

// :755-770. Blends two straight-alpha fills with weight h on `a`. Color is
// weighted by each fill's alpha so a transparent contributor (an image sampled
// outside its pixels inside a smooth fillet or morph) cannot wash out its
// neighbor, and opacity follows the dominant weighted contributor instead of
// diluting.
vec4 NowSdfBlendFillV2(vec4 a, vec4 b, float h)
{
    float weightA = h * a.a;
    float weightB = (1.0 - h) * b.a;
    float weightSum = weightA + weightB;
    vec3 rgb = weightSum > 0.0
        ? (a.rgb * weightA + b.rgb * weightB) / weightSum
        : mix(b.rgb, a.rgb, h);
    float alpha = max(weightA, weightB) / max(max(h, 1.0 - h), 0.0001);
    return vec4(rgb, alpha);
}

// ---------------------------------------------------------------------------
// The boolean operations. :772-... `operation` is data0.y:
//   0 union, 1 subtract, 2 intersect, 3 smooth union, 4 smooth subtract,
//   5 smooth intersect.
//
// `codeStep` is carried alongside the distance the whole way. It is the
// quantization step of whichever node won -- zero for an analytic shape,
// nonzero for a glyph or image whose field came out of a texture -- and it
// becomes the floor of the antialiasing width, so a coarse field does not get
// a sub-quantum edge it cannot actually resolve.
// ---------------------------------------------------------------------------

// Scene-unit tolerance for draw-order fill ties in combine(); far below one pixel.
#define NOW_SDF_FILL_TIE 0.001

void combine(
    inout float dist,
    inout vec4 fill,
    inout float codeStep,
    float shapeDist,
    vec4 nextFill,
    float shapeCodeStep,
    float operation,
    float smoothing)
{
    if (operation < 0.5)
    {
        // Coincident primitives resolve in draw order: a later shape within
        // NOW_SDF_FILL_TIE of the accumulated distance takes the fill, so exactly
        // overlapping shapes (a progress arc over its track) never pick a fill
        // per pixel from rounding noise. The distance itself stays the exact min.
        if (shapeDist <= dist + NOW_SDF_FILL_TIE)
            fill = nextFill;

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
        // Same draw-order tie rule as the union above.
        if (shapeDist >= dist - NOW_SDF_FILL_TIE)
            fill = nextFill;

        if (shapeDist > dist)
        {
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
        dist = mix(shapeDist, dist, h) - smoothing * h * (1.0 - h);
        fill = NowSdfBlendFillV2(fill, nextFill, h);
        codeStep = mix(shapeCodeStep, codeStep, h);
        return;
    }

    if (operation < 4.5)
    {
        float h = saturate(0.5 - 0.5 * (shapeDist + dist) / smoothing);
        dist = mix(dist, -shapeDist, h) + smoothing * h * (1.0 - h);
        codeStep = mix(codeStep, shapeCodeStep, h);
        return;
    }

    {
        float h = saturate(0.5 - 0.5 * (shapeDist - dist) / smoothing);
        dist = mix(shapeDist, dist, h) + smoothing * h * (1.0 - h);
        fill = NowSdfBlendFillV2(fill, nextFill, h);
        codeStep = mix(shapeCodeStep, codeStep, h);
    }
}

// The fill-less twin, for the effect field. NOTE the one place it is NOT a
// copy of `combine`: the intersect branch assigns `dist = shapeDist` inside
// the `>` test rather than `dist = max(dist, shapeDist)` after it. The two are
// equivalent; the HLSL writes them differently and so does this.
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
        dist = mix(shapeDist, dist, h) - smoothing * h * (1.0 - h);
        codeStep = mix(shapeCodeStep, codeStep, h);
        return;
    }

    if (operation < 4.5)
    {
        float h = saturate(0.5 - 0.5 * (shapeDist + dist) / smoothing);
        dist = mix(dist, -shapeDist, h) + smoothing * h * (1.0 - h);
        codeStep = mix(codeStep, shapeCodeStep, h);
        return;
    }

    {
        float h = saturate(0.5 - 0.5 * (shapeDist - dist) / smoothing);
        dist = mix(shapeDist, dist, h) + smoothing * h * (1.0 - h);
        codeStep = mix(shapeCodeStep, codeStep, h);
    }
}

// ---------------------------------------------------------------------------
// Graph, layer and scene evaluation. :926-1390.
//
// A LAYER names one graph (or two, for a morph) by a PACKED RANGE into the
// shared shape arrays: start * 128 + count, both in 0..64
// (NowSdf.PackGraphRange, NowSdf.cs:4946-4949). All possible values are exact
// in a float, which is why one component carries both.
// ---------------------------------------------------------------------------

void decodeGraphRange(float packedRange, out int start, out int count)
{
    int total = min(max(int(_SdfShapeCount), 0), NOW_SDF_MAX_SHAPES);
    float encodedRange = max(packedRange, 0.0);
    float startValue = floor(encodedRange * (1.0 / 128.0));
    start = min(max(int(startValue), 0), total);
    count = min(max(int(encodedRange - startValue * 128.0 + 0.5), 0), total - start);
}

void evalGraphFields(
    float packedRange,
    vec2 scenePos,
    vec4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out vec4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    int start;
    int count;
    decodeGraphRange(packedRange, start, count);
    dist = 100000.0;
    effectDist = 100000.0;
    fill = vec4(0.0);
    codeStep = 0.0;
    effectCodeStep = 0.0;

    if (count <= 0)
        return;

    // Nested groups (types 11-13, one level deep): the accumulator is saved at
    // a group's begin marker and the group folds from empty; a morph split
    // saves the first half; the end marker blends a morph, then combines the
    // group into the saved accumulator with the group's own operation.
    float outerDist = 100000.0;
    float outerEffectDist = 100000.0;
    vec4 outerFill = vec4(0.0);
    float outerCodeStep = 0.0;
    float outerEffectCodeStep = 0.0;
    float morphDist = 100000.0;
    float morphEffectDist = 100000.0;
    vec4 morphFill = vec4(0.0);
    float morphCodeStep = 0.0;
    float morphEffectCodeStep = 0.0;

    int first = start;
    vec4 data0 = _SdfData0[first];
    vec4 data1 = _SdfData1[first];
    vec4 data2 = _SdfData2[first];

    // Only a group's begin marker can come first; the accumulator and the saved
    // outer state are then both empty already.
    if (data0.x < 10.5)
    {
        vec2 firstDistances = shapeDistances(first, data0.x, data1, data2, scenePos);
        dist = firstDistances.x;
        effectDist = useDistinctEffectField ? firstDistances.y : firstDistances.x;
        fill = shapeFill(first, data0.x, data1, data2, scenePos, tint);
        codeStep = NowSdfTransformedShapeCodeStepV2(first, data0.x, data2);
        effectCodeStep = codeStep;
    }

    // CONSTANT BOUND, dynamic break -- see "THE FIVE THINGS", point 2.
    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];
        data1 = _SdfData1[index];
        data2 = _SdfData2[index];

        if (data0.x > 10.5)
        {
            if (data0.x < 11.5)
            {
                outerDist = dist; outerEffectDist = effectDist; outerFill = fill;
                outerCodeStep = codeStep; outerEffectCodeStep = effectCodeStep;
            }
            else if (data0.x < 12.5)
            {
                morphDist = dist; morphEffectDist = effectDist; morphFill = fill;
                morphCodeStep = codeStep; morphEffectCodeStep = effectCodeStep;
            }
            else
            {
                if (data1.x >= 0.0)
                {
                    dist = mix(morphDist, dist, data1.x);
                    effectDist = mix(morphEffectDist, effectDist, data1.x);
                    fill = NowSdfBlendFillV2(morphFill, fill, 1.0 - data1.x);
                    codeStep = mix(morphCodeStep, codeStep, data1.x);
                    effectCodeStep = mix(morphEffectCodeStep, effectCodeStep, data1.x);
                }

                float groupDist = dist;
                float groupEffectDist = effectDist;
                vec4 groupFill = fill;
                float groupCodeStep = codeStep;
                float groupEffectCodeStep = effectCodeStep;
                dist = outerDist; effectDist = outerEffectDist; fill = outerFill;
                codeStep = outerCodeStep; effectCodeStep = outerEffectCodeStep;
                combine(dist, fill, codeStep, groupDist, groupFill, groupCodeStep, data0.y, data0.z);

                if (useDistinctEffectField)
                    combineDistance(effectDist, effectCodeStep, groupEffectDist, groupEffectCodeStep, data0.y, data0.z);
            }

            if (data0.x < 12.5)
            {
                dist = 100000.0; effectDist = 100000.0; fill = vec4(0.0);
                codeStep = 0.0; effectCodeStep = 0.0;
            }

            continue;
        }

        vec2 shapeFieldDistances = shapeDistances(index, data0.x, data1, data2, scenePos);
        vec4 nextFill = shapeFill(index, data0.x, data1, data2, scenePos, tint);
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

        // UNITY_BRANCH dropped: it is a D3D compiler hint asking for a real
        // branch instead of a flattened select, and GLSL has no equivalent.
        // `useDistinctEffectField` is uniform across the draw, so every
        // fragment agrees and the branch is coherent either way.
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
    vec2 scenePos,
    vec4 tint,
    out float dist,
    out vec4 fill,
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
    vec2 scenePos,
    vec4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out vec4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    // Initialize at this boundary as well as inside evalGraphFields. Some
    // cross compilers do not prove that out parameters are written through the
    // non-morph call before the early return.
    dist = 100000.0;
    effectDist = 100000.0;
    fill = vec4(0.0);
    codeStep = 0.0;
    effectCodeStep = 0.0;
    vec4 layer0 = _SdfLayerData0[index];
    vec4 layer1 = _SdfLayerData1[index];

    // layer0.w is the layer KIND: 0 = a plain graph, 1 = a morph between two.
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

    float aDist = 0.0;
    float bDist = 0.0;
    float aEffectDist = 0.0;
    float bEffectDist = 0.0;
    vec4 aFill = vec4(0.0);
    vec4 bFill = vec4(0.0);
    float aCodeStep = 0.0;
    float bCodeStep = 0.0;
    float aEffectCodeStep = 0.0;
    float bEffectCodeStep = 0.0;
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
    // The morph is a straight lerp of the two FIELDS, not of two rasters --
    // which is the whole reason the shape system is distance-based.
    float t = saturate(layer1.y);
    dist = mix(aDist, bDist, t);
    fill = NowSdfBlendFillV2(aFill, bFill, 1.0 - t);
    codeStep = mix(aCodeStep, bCodeStep, t);
    if (useDistinctEffectField)
    {
        effectDist = mix(aEffectDist, bEffectDist, t);
        effectCodeStep = mix(aEffectCodeStep, bEffectCodeStep, t);
    }
    else
    {
        effectDist = dist;
        effectCodeStep = codeStep;
    }
}

void evalLayer(
    int index,
    vec2 scenePos,
    vec4 tint,
    out float dist,
    out vec4 fill,
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
    vec2 scenePos,
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

    // Nested groups, as in evalGraphFields.
    float outerDist = 100000.0;
    float outerCodeStep = 0.0;
    float morphDist = 100000.0;
    float morphCodeStep = 0.0;

    int first = start;
    vec4 data0 = _SdfData0[first];

    if (data0.x < 10.5)
    {
        vec4 firstData2 = _SdfData2[first];
        vec2 firstDistances = shapeDistances(first, data0.x, _SdfData1[first], firstData2, scenePos);
        dist = mix(firstDistances.x, firstDistances.y, effectField);
        codeStep = NowSdfTransformedShapeCodeStepV2(first, data0.x, firstData2);
    }

    for (int localIndex = 1; localIndex < NOW_SDF_MAX_SHAPES; ++localIndex)
    {
        if (localIndex >= count)
            break;

        int index = start + localIndex;
        data0 = _SdfData0[index];

        if (data0.x > 10.5)
        {
            if (data0.x < 11.5)
            {
                outerDist = dist;
                outerCodeStep = codeStep;
            }
            else if (data0.x < 12.5)
            {
                morphDist = dist;
                morphCodeStep = codeStep;
            }
            else
            {
                float morphT = _SdfData1[index].x;

                if (morphT >= 0.0)
                {
                    dist = mix(morphDist, dist, morphT);
                    codeStep = mix(morphCodeStep, codeStep, morphT);
                }

                float groupDist = dist;
                float groupCodeStep = codeStep;
                dist = outerDist;
                codeStep = outerCodeStep;
                combineDistance(dist, codeStep, groupDist, groupCodeStep, data0.y, data0.z);
            }

            if (data0.x < 12.5)
            {
                dist = 100000.0;
                codeStep = 0.0;
            }

            continue;
        }

        vec4 data2 = _SdfData2[index];
        vec2 shapeFieldDistances = shapeDistances(index, data0.x, _SdfData1[index], data2, scenePos);
        float shapeDist = mix(shapeFieldDistances.x, shapeFieldDistances.y, effectField);
        float shapeCodeStep = NowSdfTransformedShapeCodeStepV2(index, data0.x, data2);
        combineDistance(dist, codeStep, shapeDist, shapeCodeStep, data0.y, data0.z);
    }
}

void evalGraphDistance(
    float packedRange,
    vec2 scenePos,
    out float dist,
    out float codeStep)
{
    evalGraphDistanceField(packedRange, scenePos, 0.0, dist, codeStep);
}

void evalGraphEffectDistance(
    float packedRange,
    vec2 scenePos,
    out float dist,
    out float codeStep)
{
    evalGraphDistanceField(packedRange, scenePos, 1.0, dist, codeStep);
}

void evalLayerDistanceField(
    int index,
    vec2 scenePos,
    float effectField,
    out float dist,
    out float codeStep)
{
    dist = 100000.0;
    codeStep = 0.0;
    vec4 layer0 = _SdfLayerData0[index];
    vec4 layer1 = _SdfLayerData1[index];

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
    dist = mix(aDist, bDist, t);
    codeStep = mix(aCodeStep, bCodeStep, t);
}

void evalLayerDistance(int index, vec2 scenePos, out float dist, out float codeStep)
{
    evalLayerDistanceField(index, scenePos, 0.0, dist, codeStep);
}

void evalLayerEffectDistance(int index, vec2 scenePos, out float dist, out float codeStep)
{
    evalLayerDistanceField(index, scenePos, 1.0, dist, codeStep);
}

void evalSceneFields(
    vec2 scenePos,
    vec4 tint,
    bool useDistinctEffectField,
    out float dist,
    out float effectDist,
    out vec4 fill,
    out float codeStep,
    out float effectCodeStep)
{
    int layerCount = min(int(_SdfLayerCount), NOW_SDF_MAX_LAYERS);
    bool found = false;
    dist = 100000.0;
    effectDist = 100000.0;
    fill = vec4(0.0);
    codeStep = 0.0;
    effectCodeStep = 0.0;

    for (int layer = 0; layer < NOW_SDF_MAX_LAYERS; ++layer)
    {
        if (layer >= layerCount)
            break;

        float layerDist;
        float layerEffectDist;
        vec4 layerFill;
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
    vec2 scenePos,
    vec4 tint,
    out float dist,
    out vec4 fill,
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
    vec2 scenePos,
    float effectField,
    out float dist,
    out float codeStep)
{
    int layerCount = min(int(_SdfLayerCount), NOW_SDF_MAX_LAYERS);
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

void evalSceneDistanceAndCodeStep(vec2 scenePos, out float dist, out float codeStep)
{
    evalSceneDistanceAndCodeStepField(scenePos, 0.0, dist, codeStep);
}

void evalSceneEffectDistanceAndCodeStep(vec2 scenePos, out float dist, out float codeStep)
{
    evalSceneDistanceAndCodeStepField(scenePos, 1.0, dist, codeStep);
}

void evalSceneDistance(vec2 scenePos, out float dist)
{
    float codeStep;
    evalSceneDistanceAndCodeStep(scenePos, dist, codeStep);
}

// ---------------------------------------------------------------------------
// Domain warp. :1392-1420. Dormant unless _SdfWarp.x > 0, which the material
// default (0, 1, 0, 0) is not.
// ---------------------------------------------------------------------------

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise21(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

vec2 warpScenePos(vec2 scenePos)
{
    if (_SdfWarp.x <= 0.0)
        return scenePos;

    float scale = max(_SdfWarp.y, 0.0001);
    // _Time.y -> nowui_Time. The ONLY clock this program reads.
    float t = nowui_Time * _SdfWarp.z + _SdfWarp.w;
    vec2 p = scenePos / scale;
    vec2 n = vec2(noise21(p + t), noise21(p + t + 37.23)) * 2.0 - 1.0;
    return scenePos + n * _SdfWarp.x;
}

// ---------------------------------------------------------------------------
// Shading helpers. :1422-1455.
// ---------------------------------------------------------------------------

vec4 effectColor(vec4 color, vec4 tint)
{
    return color * tint;
}

// `distance` renamed to `fieldDistance`. Analytic distances have no finite
// glyph field, so codeStep is 0 and this returns 1. Glyph and image distances
// fade out before the uploaded field's range exposes its rectangular fallback.
float exteriorEffectValidity(float fieldDistance, float codeStep, float edge)
{
    float isGlyphDistance = sign(max(codeStep, 0.0));
    float glyphValidity = 1.0 - smoothstep(
        max(_SdfTextEffectLimit, 0.0) - edge,
        max(_SdfTextEffectLimit, 0.0) + edge,
        fieldDistance);
    return mix(1.0, glyphValidity, isGlyphDistance);
}

// Source-over applies the remaining fill coverage again. Condition the
// exterior layer so its geometric ring survives authored fill opacity.
float exclusiveEffectCoverage(float effectCoverage, float fillCoverage, float fillOpacity)
{
    float remainingFill = max(1.0 - fillCoverage * saturate(fillOpacity), 0.0001);
    return saturate((effectCoverage - fillCoverage) / remainingFill);
}

// Straight-alpha source-over. NOT the premultiplied composite the other NowUI
// fragment stages build: this whole program works in straight alpha and hands
// straight alpha to a SrcAlpha/OneMinusSrcAlpha blend.
vec4 alphaOver(vec4 baseColor, vec4 topColor)
{
    float a = topColor.a + baseColor.a * (1.0 - topColor.a);
    vec3 rgb = (topColor.rgb * topColor.a + baseColor.rgb * baseColor.a * (1.0 - topColor.a)) / max(a, 0.0001);
    return vec4(rgb, a);
}

// ---------------------------------------------------------------------------
// main. :1526-1721.
// ---------------------------------------------------------------------------

// One drop shadow: the scene field evaluated again at an offset, softened and
// spread. Drawn beneath the fill and the other exterior effects.
vec4 NowSdfDropShadowV2(vec2 scenePosBase, vec4 shadow, vec4 shadowColorValue, float coverage, float fillAlpha, vec4 tint)
{
    float shadowDist;
    float shadowCodeStep;
    evalSceneEffectDistanceAndCodeStep(
        warpScenePos(scenePosBase - shadow.xy),
        shadowDist,
        shadowCodeStep);
    float shadowPixelWidth = max(
        max(length(vec2(dFdx(shadowDist), dFdy(shadowDist))), shadowCodeStep),
        0.0001);
    float shadowEdge = shadowPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    float shadowEffectDist = shadowDist - shadow.w;
    float shadowCoverage = smoothstep(max(shadow.z, shadowPixelWidth) + shadowEdge, -shadowEdge, shadowEffectDist);
    float shadowAlpha = exclusiveEffectCoverage(shadowCoverage, coverage, fillAlpha);
    shadowAlpha *= exteriorEffectValidity(shadowDist, shadowCodeStep, shadowEdge);
    vec4 shadowColor = effectColor(shadowColorValue, tint);
    shadowColor.a *= shadowAlpha;
    return shadowColor;
}

void main()
{
    // :1529. quadPos is in mesh units; scenePos is in AUTHORED scene units,
    // which differ whenever a transform scaled the quad. The scene mapping
    // (aExtras) carries the authored size and the sign of each axis so the
    // field is evaluated in the space the shapes were authored in.
    vec2 quadPos = vRawUV * vRect.zw;

    // :1532. Older/generated meshes may not carry the source mapping yet.
    // Treat an empty payload as identity so custom shader includes remain
    // compatible.
    float hasSceneMapping = step(0.0001, abs(vSceneMapping.x) + abs(vSceneMapping.y));
    vec2 sceneSize = max(mix(vRect.zw, abs(vSceneMapping.xy), hasSceneMapping), 0.0001);
    vec2 sceneDirection = mix(vec2(1.0, 1.0), vSceneMapping.zw, hasSceneMapping);
    vec2 sourceUv = 0.5 + (vRawUV - 0.5) * sceneDirection;
    vec2 sceneQuadPos = sourceUv * sceneSize;

    // The flip that turns the y-UP quad coordinate into the y-DOWN, top-left
    // scene coordinate the C# side authors shapes in: rawUV.y == 1 is the UI
    // top, so this maps the UI top edge to scene y == 0. See sdRoundBox above.
    vec2 scenePosBase = vec2(sceneQuadPos.x, sceneSize.y - sceneQuadPos.y);
    vec2 scenePos = warpScenePos(scenePosBase);
    vec2 meshPos = vRect.xy + quadPos;
    vec2 uiPosition = vec2(meshPos.x, -meshPos.y);
    vec4 mask = vMask;

    // NowUIClipLegacyRect. The mask include shared with every other ported
    // program exposes the DISTANCE; the discard is written out here because
    // that is the only form both copies of the include agree on.
    if (NowUILegacyRectDistance(uiPosition, mask) < 0.0)
        discard;

    // :1545-1554. Both of these depend ONLY on material uniforms, so every
    // fragment in the draw takes the same path -- which is what makes the
    // second (expensive) field evaluation affordable when it is needed and
    // free when it is not.
    bool hasFiniteTextEffectLimit = _SdfTextEffectLimit < 100000.0;
    bool hasStockDistanceEffect =
        (_SdfOutlineColor.a > 0.0 && _SdfOutline.x > 0.0) ||
        (_SdfGlowColor.a > 0.0 && _SdfGlow.x > 0.0) ||
        _SdfShadowColor.a > 0.0 ||
        _SdfShadow2Color.a > 0.0 ||
        _SdfInnerShadowColor.a > 0.0 ||
        (_SdfContourColor.a > 0.0 && _SdfContour.x > 0.0 && _SdfContour.y > 0.0);
    bool useDistinctEffectField = hasFiniteTextEffectLimit && hasStockDistanceEffect;

    float dist = 100000.0;
    float effectDist = 100000.0;
    vec4 fill = vec4(0.0);
    float distanceCodeStep = 0.0;
    float effectCodeStep = 0.0;
    evalSceneFields(
        scenePos,
        vTint,
        useDistinctEffectField,
        dist,
        effectDist,
        fill,
        distanceCodeStep,
        effectCodeStep);

    // :1568. The antialiasing band, in field units per screen pixel, floored
    // by the field's own quantization step.
    float pixelWidth = max(
        max(length(vec2(dFdx(dist), dFdy(dist))), distanceCodeStep),
        0.0001);
    float edge = pixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    float effectPixelWidth = pixelWidth;
    float effectEdge = edge;

    if (useDistinctEffectField)
    {
        effectPixelWidth = max(
            max(length(vec2(dFdx(effectDist), dFdy(effectDist))), effectCodeStep),
            0.0001);
        effectEdge = effectPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
    }

    float coverage = smoothstep(edge, -edge, dist);
    float exteriorValidity = 1.0;

    if (useDistinctEffectField)
        exteriorValidity = exteriorEffectValidity(effectDist, effectCodeStep, effectEdge);

    vec4 col = vec4(0.0);

    // ---- drop shadow. A SECOND full field evaluation, at an offset. :1590.
    // The second shadow (AddShadow) sits beneath the first, like a CSS
    // box-shadow list.
    if (_SdfShadow2Color.a > 0.0)
        col = alphaOver(col, NowSdfDropShadowV2(scenePosBase, _SdfShadow2, _SdfShadow2Color, coverage, fill.a, vTint));

    if (_SdfShadowColor.a > 0.0)
        col = alphaOver(col, NowSdfDropShadowV2(scenePosBase, _SdfShadow, _SdfShadowColor, coverage, fill.a, vTint));

    // ---- glow. :1611.
    if (_SdfGlowColor.a > 0.0 && _SdfGlow.x > 0.0)
    {
        float glowT = saturate(1.0 - max(effectDist, 0.0) / max(_SdfGlow.x, 0.0001));
        float glowCoverage = pow(glowT, max(_SdfGlow.y, 0.0001));
        float glowAlpha = exclusiveEffectCoverage(glowCoverage, coverage, fill.a) * exteriorValidity;
        vec4 glowColor = effectColor(_SdfGlowColor, vTint);
        glowColor.a *= glowAlpha;
        col = alphaOver(col, glowColor);
    }

    // ---- outline. :1621.
    if (_SdfOutlineColor.a > 0.0 && _SdfOutline.x > 0.0)
    {
        float outlineCoverage = smoothstep(_SdfOutline.x + _SdfOutline.y + effectEdge, _SdfOutline.x - effectEdge, effectDist);
        float outlineAlpha = exclusiveEffectCoverage(outlineCoverage, coverage, fill.a) * exteriorValidity;
        vec4 outlineColor = effectColor(_SdfOutlineColor, vTint);
        outlineColor.a *= outlineAlpha;
        col = alphaOver(col, outlineColor);
    }

    vec4 fillColor = fill;

    // ---- emboss. :1631. The gradient of the field IS the surface normal.
    if (_SdfEmboss.w > 0.0)
    {
        // Mirrors the HLSL: the field gradient comes from scene-space differences
        // mapped to screen through the scene position's derivatives, so sharp inside
        // corners get a clean bevel crease instead of a stair-stepped 2x2 artifact.
        vec2 sceneDx = dFdx(scenePosBase);
        vec2 sceneDy = dFdy(scenePosBase);
        float embossStep = max(max(length(sceneDx), length(sceneDy)), 0.0001);
        float embossDistX;
        float embossDistY;
        evalSceneDistance(warpScenePos(scenePosBase + vec2(embossStep, 0.0)), embossDistX);
        evalSceneDistance(warpScenePos(scenePosBase + vec2(0.0, embossStep)), embossDistY);
        vec2 sceneGrad = vec2(embossDistX - dist, embossDistY - dist) / embossStep;
        vec2 grad = vec2(dot(sceneGrad, sceneDx), dot(sceneGrad, sceneDy));
        vec2 normal2 = normalize(grad + 0.0001);
        vec2 light = normalize(_SdfEmboss.xy + 0.0001);
        float band = 1.0 - smoothstep(0.0, max(_SdfEmboss.z, pixelWidth), abs(dist));
        float shade = dot(normal2, light) * _SdfEmboss.w * band;
        fillColor.rgb = saturate(fillColor.rgb + shade);
    }

    fillColor.a *= coverage;
    col = alphaOver(col, fillColor);

    // ---- inner shadow. A THIRD field evaluation. :1644.
    if (_SdfInnerShadowColor.a > 0.0)
    {
        float innerDist;
        float innerCodeStep;
        evalSceneEffectDistanceAndCodeStep(
            warpScenePos(scenePosBase - _SdfInnerShadow.xy),
            innerDist,
            innerCodeStep);
        float innerPixelWidth = max(
            max(length(vec2(dFdx(innerDist), dFdy(innerDist))), innerCodeStep),
            0.0001);
        float innerEdge = innerPixelWidth * max(0.5 + _SdfFeather * 0.5, 0.5);
        float innerEffectDist = innerDist + _SdfInnerShadow.w;
        float innerShape = smoothstep(max(_SdfInnerShadow.z, innerPixelWidth) + innerEdge, -innerEdge, innerEffectDist);
        float innerAlpha = coverage * (1.0 - innerShape);
        vec4 innerShadowColor = effectColor(_SdfInnerShadowColor, vTint);
        innerShadowColor.a *= innerAlpha;
        col = alphaOver(col, innerShadowColor);
    }

    // ---- contour bands. :1664.
    if (_SdfContourColor.a > 0.0 && _SdfContour.x > 0.0 && _SdfContour.y > 0.0)
    {
        float spacing = max(_SdfContour.x, 0.0001);
        float halfWidth = _SdfContour.y * 0.5;
        float contourDistance = effectDist + _SdfContour.z;
        // frac -> fract. Both operands are non-negative here, so the
        // HLSL-truncates / GLSL-floors difference cannot bite -- but the
        // difference is real and is why this is not a mechanical rename
        // everywhere.
        float nearest = abs(fract(contourDistance / spacing + 0.5) - 0.5) * spacing;
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
        vec4 contourColor = effectColor(_SdfContourColor, vTint);
        contourColor.a *= contourAlpha;
        col = alphaOver(col, contourColor);
    }

    // NOW_SDF_CUSTOM_FINAL_SHADE would hook in here. Unreachable through this
    // program; see the header.

    // UNITY_UI_CLIP_RECT would apply here. Keyword off; see the header.

    // :1714. Note the asymmetry with UIRectangle, which multiplies an already
    // premultiplied colour: this stage scales ALPHA ONLY, because col is
    // straight alpha all the way to the blend.
    col.a *= NowUIMaskCoverage(uiPosition);

    // :1719. Force source alpha to one so the SrcAlpha blend writes the
    // composed coverage to RGB without squaring soft edges. The mask cache
    // samples red from its linear R8/ARGB target.
    if (_SdfMaskOutput > 0.5)
    {
        fragColor = vec4(col.a, col.a, col.a, 1.0);
        return;
    }

    // UNITY_UI_ALPHACLIP would add a second, identical clip here. Keyword off.

    // :1721. clip(x) discards on STRICTLY negative, so x == 0 survives.
    if (col.a - 0.001 < 0.0)
        discard;

    fragColor = col;
}
