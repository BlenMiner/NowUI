#version 300 es
// ===========================================================================
// nowui-gradient.frag
//
// GLSL ES 3.00 port of sdRoundedBox, decodePair8, applySpread,
// gradientPosition, sampleRamp and `frag` from
// Assets/NowUI/Assets/Shaders/UIGradient.shader (shader "NowUI/UI Gradient").
// Every block cites the HLSL line it came from.
//
// COMPOSITION: see nowui-gradient.vert's header. This file includes BOTH
// shared fragments -- the mask, like every other NowUI fragment stage, and the
// colour space, which the other three fragment stages do not need. UIGradient
// is the one shader that converts colours in the FRAGMENT stage
// (UIGradient.shader:212-215), because the ramp is sampled and the tint is
// unpacked here rather than interpolated from the vertex stage.
//
// Nothing is stubbed. The ramp atlas is a real texture bound to _MainTex and
// every one of the three gradient kinds, three spread modes and two ramp
// stepping modes is reachable from NowUI's public API.
//
// PRECISION -- read before "tidying". Two things here break at mediump, both
// silently:
//   * decodePair8 divides values up to 65535 by 256 and subtracts the
//     reconstructed high byte back off. mediump cannot represent 65535
//     exactly, so the tint comes out wrong.
//   * `delta = length(vec2(dFdx(dist), dFdy(dist)))` is a difference of two
//     nearly equal numbers; at mediump the AA band width becomes noise.
// `precision highp sampler2D` is not decoration either: the fragment default
// for a sampler is LOWP, which would quantise the ramp on the way in.
// ===========================================================================

precision highp float;
precision highp int;
precision highp sampler2D;

//#include "nowui-colorspace.glsl"
//#include "nowui-mask.glsl"

// ---------------------------------------------------------------------------
// Uniforms. UIGradient.shader:70-71 declares two, and both are real:
//   :70 sampler2D _MainTex                  -- the RAMP ATLAS, not a fill
//                                              texture. NowGradientMaterials
//                                              assigns it as material.mainTexture
//                                              from NowGradientRampCache.
//   :71 float4 _NowGradientRampTexelSize    -- (1/w, 1/h, w, h) of that atlas,
//                                              written alongside it by
//                                              NowGradientMaterials.TryGet.
//
// Fallbacks the backend must supply (M2-ShaderPort.md section 7.3 step 5):
//   _MainTex                   -> the 1x1 opaque WHITE texture. That makes
//                                 every ramp sample (1,1,1,1), i.e. a flat
//                                 white gradient -- plausible-looking and
//                                 wrong, so the backend also says so out loud
//                                 once rather than letting it pass.
//   _NowGradientRampTexelSize  -> (0.00390625, 0.00390625, 256, 256), the
//                                 Properties-block default and the value
//                                 GradientMaterial.mat actually stores.
//
// NEVER leave that uniform at GL's all-zero default, and note exactly why --
// the failure is the quiet kind, measured rather than assumed:
//   * `y = (row + 0.5) * .y` becomes 0, so EVERY gradient samples ROW 0 of the
//     atlas instead of the row it owns. Each gradient still renders a smooth,
//     plausible sweep; it is simply the wrong ramp, and with one gradient on
//     screen it is indistinguishable from a correct picture.
//   * the fixed-step branch additionally collapses to column 0, because
//     `index = floor(t * (.z - 1) + 0.5)` goes negative and `x = (index + 0.5)
//     * .x` is 0 whatever the index was.
//   * the smooth branch's x survives by accident -- `mix(0, 1, t)` is still t
//     -- which is exactly what makes the row error easy to miss.
//
// _ZTest is in the Properties block but is RENDER STATE, not a uniform (it is
// 8 = CompareFunction.Always here). Never bind it to a location.
// Texture units are fixed: 0 _MainTex, 1 _NowUITextureMask0,
// 2 _NowUITextureMask1 (M2-ShaderPort.md section 7.4).
// ---------------------------------------------------------------------------
uniform highp sampler2D _MainTex;
uniform highp vec4 _NowGradientRampTexelSize;

// Varyings -- must match nowui-gradient.vert's `out` block exactly.
in highp vec2 vPackedTint;
in highp vec4 vRect;
in highp vec4 vRadius;
in highp vec4 vGradient;
in highp vec4 vOutlineColor;
in highp vec4 vExtras;
in highp vec4 vMask;
in highp vec2 vRawUV;

// UIGradient.shader:174 `fixed4 frag(...) : SV_Target` becomes an explicit out.
out highp vec4 fragColor;

// ---------------------------------------------------------------------------
// HLSL fmod is NOT GLSL mod.
//
// HLSL's fmod truncates toward zero and keeps the sign of the DIVIDEND; GLSL's
// mod floors and keeps the sign of the DIVISOR. They agree only for
// non-negative operands. Every call in this file happens to be non-negative --
// the flags are decoded from a fract() and floor()ed -- so `mod` would give
// the same answers today. It is still written out, because the next shader
// ported will have a negative operand and reaching for `mod` there is a bug
// that produces a plausible picture (M2-ShaderPort.md section 4.5).
// ---------------------------------------------------------------------------
highp float hlslFmod(highp float x, highp float y)
{
    return x - y * trunc(x / y);
}

// ---------------------------------------------------------------------------
// UIGradient.shader:73-79. Byte-identical to the copy in UIRectangle.shader
// and UIRipple.shader. See nowui-rectangle.frag for the full derivation of the
// quadrant selection; the short version is that p.y > 0 is the UI TOP because
// p is built from rawUV, so radius component .x is the top-right corner.
// ---------------------------------------------------------------------------
highp float sdRoundedBox(highp vec2 p, highp vec2 b, highp vec4 r)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x  = (p.y > 0.0) ? r.x  : r.y;
    highp vec2 q = abs(p) - b + r.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
}

// ---------------------------------------------------------------------------
// UIGradient.shader:81-88. Unpacks two 8-bit values that were packed into one
// float as `high * 256 + low`. The +0.5/floor is the standard defence against
// an interpolated float landing at 16383.9999 instead of 16384: every vertex
// of the quad carries the SAME packed value, so interpolation is a no-op in
// exact arithmetic and this only has to survive rounding.
//
// The result is divided by 255, not 256: the two bytes are colour channels in
// [0, 255] mapping onto [0, 1], so 255 must land on exactly 1.0.
// ---------------------------------------------------------------------------
highp vec2 decodePair8(highp float packedPair)
{
    packedPair = floor(packedPair + 0.5);
    highp float first = floor(packedPair / 256.0);
    highp float second = packedPair - first * 256.0;
    return vec2(first, second) / 255.0;
}

// ---------------------------------------------------------------------------
// UIGradient.shader:90-100. How a gradient coordinate outside [0, 1] behaves.
//   0 (Clamp)  -- hold the end stops.
//   1 (Repeat) -- saw tooth: fract(t).
//   2 (Mirror) -- triangle wave of period 2, so the ramp reflects rather than
//                 jumping at every repeat boundary.
// `saturate` -> `clamp(x, 0.0, 1.0)`, `frac` -> `fract`.
// ---------------------------------------------------------------------------
highp float applySpread(highp float t, highp float spread)
{
    if (spread < 0.5)
        return clamp(t, 0.0, 1.0);

    if (spread < 1.5)
        return fract(t);

    return 1.0 - abs(fract(t * 0.5) * 2.0 - 1.0);
}

// ---------------------------------------------------------------------------
// UIGradient.shader:102-136. Turns a normalised quad coordinate into the
// gradient's 1-D parameter, before spread is applied.
//
//   uv     -- uiUV, i.e. (rawUV.x, 1 - rawUV.y): y DOWN, matching the UI
//             coordinate system the gradient's parameters were authored in.
//   size   -- rect.zw, always positive.
//   data   -- the gradient payload (vGradient), meaning per kind:
//               linear:  .xy direction (unnormalised), .z repetitions
//               radial:  .xy centre in uv space, .zw radii (or .z radius when
//                        `circle` is set),
//               angular: .xy centre, .z start turn offset, .w repetitions
//   kind   -- 0 linear, 1 radial, 2 angular (decoded from extras.w)
//   circle -- radial only: force a circular falloff sized by the SHORTER edge
//             instead of an ellipse that follows the rect's aspect.
//
// GLSL notes:
//   * `max(abs(data.zw), 0.0001)` is a float2-by-scalar max in HLSL, which
//     broadcasts. GLSL will not: it must be written `vec2(0.0001)`. This is
//     the one line in the file that changes meaning if translated literally.
//   * `atan2(y, x)` -> `atan(y, x)`: same argument order, different name.
// ---------------------------------------------------------------------------
highp float gradientPosition(highp vec2 uv, highp vec2 size, highp vec4 data,
                             highp float kind, highp float circle)
{
    if (kind < 0.5)
    {
        // UIGradient.shader:104-116 -- linear.
        // `extent` is the half-width of the rect measured ALONG the gradient
        // direction (the support function of an axis-aligned box), so the
        // parameter reaches 0 and 1 exactly at the rect's silhouette however
        // the direction is angled. The 1e-4 floors keep a degenerate direction
        // or a zero-area rect finite rather than producing NaN.
        highp vec2 direction = data.xy;
        highp float directionLength = max(length(direction), 0.0001);
        direction /= directionLength;
        highp vec2 local = (uv - 0.5) * size;
        highp float extent = max(
            abs(direction.x) * size.x * 0.5 + abs(direction.y) * size.y * 0.5,
            0.0001);
        return (0.5 + dot(local, direction) / (2.0 * extent)) * max(abs(data.z), 0.0001);
    }

    if (kind < 1.5)
    {
        // UIGradient.shader:118-128 -- radial.
        highp vec2 delta = uv - data.xy;

        if (circle > 0.5)
        {
            // Circular: distance measured in PIXELS (delta * size), divided by
            // a radius scaled off the shorter edge, so the ring stays round in
            // a non-square rect.
            highp float radius = max(abs(data.z) * min(size.x, size.y), 0.0001);
            return length(delta * size) / radius;
        }

        // Elliptical: distance measured in NORMALISED uv, divided per axis, so
        // the ring follows the rect's aspect.
        // HLSL broadcasts the scalar into the float2 max; GLSL will not.
        return length(delta / max(abs(data.zw), vec2(0.0001)));
    }

    // UIGradient.shader:130-135 -- angular (conic).
    highp vec2 delta = uv - data.xy;

    // atan2(delta.x, -delta.y) puts turn 0 at "up" (negative y in this y-down
    // space) and increases clockwise. Divided by 2*pi it is a turn count.
    //
    // ONE DELIBERATE DIVERGENCE FROM A LITERAL TRANSLATION: HLSL's atan2(0, 0)
    // is defined to return 0, while GLSL's atan(y, x) is UNDEFINED when both
    // arguments are zero and is free to produce a NaN -- which would propagate
    // through fract() and paint one garbage pixel at the exact centre of every
    // conic gradient. Guarding it makes this port CLOSER to Unity's result,
    // not further from it, and cannot change any case Unity defines.
    highp vec2 d = vec2(delta.x, -delta.y);
    highp float turns = (d.x == 0.0 && d.y == 0.0)
        ? 0.0
        : atan(d.x, d.y) / 6.28318530718;

    return fract(turns - data.z) * max(abs(data.w), 0.0001);
}

// ---------------------------------------------------------------------------
// UIGradient.shader:138-160. Reads one texel out of the shared 256x256 ramp
// atlas. Each gradient owns one ROW of that atlas; `encodedRamp` packs the row
// index in its integer part and the mode flags in its fraction.
//
// The flag decode is a running shift and is easy to get subtly wrong -- note
// that the second `floor(flags / 8.0)` divides the ALREADY-SHIFTED value, so
// the bit positions are not what the literals suggest in isolation:
//
//   flags  = floor(fract(encodedRamp) * 256.0)   the whole flag byte
//   flags  = floor(flags / 4.0)                  drop the low 2 bits
//   spread = fmod(flags, 4.0)                    bits 2..3 of the byte
//   flags  = floor(flags / 8.0)                  drop 3 more
//   fixed  = fmod(flags, 2.0)                    bit 5 of the byte
//
// The fragment stage decodes the SAME byte differently for kind and circle
// (bits 0..1 and bit 4), from the unshifted value. Both decodes are reproduced
// exactly where the HLSL puts them rather than unified, because unifying them
// is precisely how the shift order gets lost.
// ---------------------------------------------------------------------------
highp vec4 sampleRamp(highp float t, highp float encodedRamp)
{
    highp float row = floor(encodedRamp);
    highp float flags = floor(fract(encodedRamp) * 256.0);
    flags = floor(flags / 4.0);
    highp float spread = hlslFmod(flags, 4.0);
    flags = floor(flags / 8.0);
    highp float fixedMode = hlslFmod(flags, 2.0);

    t = applySpread(t, spread);
    highp float x;

    if (fixedMode > 0.5)
    {
        // Hard stops: snap to the nearest of the atlas's .z columns and sample
        // its centre, so the ramp reads as bands rather than a blend.
        highp float index = floor(t * (_NowGradientRampTexelSize.z - 1.0) + 0.5);
        x = (index + 0.5) * _NowGradientRampTexelSize.x;
    }
    else
    {
        // Smooth: span the row from the centre of its first texel to the centre
        // of its last, so LINEAR filtering never bleeds in the neighbouring
        // row's edge texel and t == 0 / t == 1 land exactly on the end stops.
        x = mix(
            0.5 * _NowGradientRampTexelSize.x,
            1.0 - 0.5 * _NowGradientRampTexelSize.x,
            t);
    }

    highp float y = (row + 0.5) * _NowGradientRampTexelSize.y;
    return texture(_MainTex, vec2(x, y));
}

void main()
{
    // UIGradient.shader:177-178. vRawUV is already a vec2 here (the HLSL v2f
    // narrows it), so unlike UIRectangle there is no float4*float2 truncation
    // to translate. rect.xy is the UI bottom-left corner in negated-y mesh
    // space and rect.zw is always positive, so negating pos.y recovers UI
    // space (y down, positive).
    highp vec2 pos = vRect.xy + vRawUV * vRect.zw;
    highp vec2 uiPosition = vec2(pos.x, -pos.y);

    // UIGradient.shader:180 -- the legacy hard clip. Unconditional, no AA,
    // before any other work. May discard.
    NowUIClipLegacyRect(uiPosition, vMask);

    // UIGradient.shader:182. The gradient's own coordinate is y-DOWN, because
    // the gradient parameters (centres, directions) are authored in UI space.
    // This is NOT the same as `position` two lines below, which is the y-UP
    // SDF space. Both are correct; they are different spaces.
    highp vec2 uiUV = vec2(vRawUV.x, 1.0 - vRawUV.y);

    // UIGradient.shader:183-190. Shape coverage, identical in form to
    // UIRectangle: full-quad space, half-pixel band centred on the true edge,
    // `extras.x` (blur) widening only the outer side. Do NOT clamp rawUV --
    // geometry padding legitimately pushes it outside [0,1].
    highp vec2 position = (vRawUV - 0.5) * vRect.zw;
    highp float dist = sdRoundedBox(position, vRect.zw * 0.5, vRadius);
    highp float delta = max(length(vec2(dFdx(dist), dFdy(dist))), 0.0001);
    highp float aa = 0.5 * delta;
    highp float graphicAlpha = 1.0 - smoothstep(-aa, aa + max(vExtras.x, 0.0), dist);

    // UIGradient.shader:192-195. An outline thinner than one AA width would sit
    // entirely inside the edge fade and wash out, so it is never drawn thinner
    // than `delta`. `extras.y == 0.0` is an EXACT equality test in the HLSL and
    // stays one here: it is a disable switch, not a threshold.
    highp float outlineWidth = max(vExtras.y, delta);
    highp float outlineAlpha = vExtras.y == 0.0
        ? 0.0
        : smoothstep(-outlineWidth - aa, -outlineWidth + aa, dist);

    // UIGradient.shader:197-200. The OTHER decode of the flag byte: kind from
    // bits 0..1, circle from bit 4, both from the unshifted value. See
    // sampleRamp's header for why this is not shared with the shift chain there.
    highp float flags = floor(fract(vExtras.w) * 256.0);
    highp float kind = hlslFmod(flags, 4.0);
    highp float circle = hlslFmod(floor(flags / 16.0), 2.0);
    highp float t = gradientPosition(uiUV, vRect.zw, vGradient, kind, circle);
    highp vec4 ramp = sampleRamp(t, vExtras.w);

    // UIGradient.shader:201-203. The per-vertex tint, unpacked from two floats
    // into four 8-bit channels. This is the multiplier NowUI applies for
    // opacity and colour modulation on top of the ramp.
    highp vec2 rg = decodePair8(vPackedTint.x);
    highp vec2 ba = decodePair8(vPackedTint.y);
    highp vec4 tint = vec4(rg.x, rg.y, ba.x, ba.y);

    // UIGradient.shader:205-215. Premultiplied compositing of outline over
    // fill, matching UIRectangle. The colour-space conversion happens HERE, on
    // both the sampled ramp and the unpacked tint, because neither existed in
    // the vertex stage. Identity under Gamma; kept so the linear branch is one
    // #define away.
    highp float outlineCoverage = vOutlineColor.a * outlineAlpha * graphicAlpha;
    highp float fillCoverage = ramp.a * tint.a * graphicAlpha;
    highp vec3 fillColor =
        NowUIColorToWorkingSpace(ramp.rgb) *
        NowUIColorToWorkingSpace(tint.rgb) *
        fillCoverage;

    // UIGradient.shader:217-220. Output is PREMULTIPLIED; the pipeline blends
    // with gl.blendFunc(ONE, ONE_MINUS_SRC_ALPHA).
    highp vec4 col;
    col.rgb = vOutlineColor.rgb * outlineCoverage + fillColor * (1.0 - outlineCoverage);
    col.a = outlineCoverage + fillCoverage * (1.0 - outlineCoverage);

    // UIGradient.shader:221. HLSL `col *= x` on a float4 by a scalar scales rgb
    // AND a alike, which is correct for a premultiplied colour.
    col *= NowUIMaskCoverage(uiPosition);

    // UIGradient.shader:222 -- clip(col.a - 0.001). HLSL clip discards on
    // strictly negative, so alpha exactly 0.001 survives.
    if (col.a - 0.001 < 0.0)
        discard;

    // UIGradient.shader:223
    fragColor = col;
}
