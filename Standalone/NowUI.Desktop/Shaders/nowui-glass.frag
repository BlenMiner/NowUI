#version 300 es
// ===========================================================================
// nowui-glass.frag
//
// GLSL ES 3.00 port of sdRoundedBox + `frag` from
// Assets/NowUI/Assets/Shaders/UIGlass.shader (shader "NowUI/UI Glass").
// Every block cites the HLSL line it came from.
//
// COMPOSITION: see nowui-glass.vert's header.
//
// ---------------------------------------------------------------------------
// THE BLEND MODE IS DIFFERENT FROM EVERY OTHER NOWUI SHADER. READ THIS FIRST.
// ---------------------------------------------------------------------------
// UIGlass.shader:35 declares
//     Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
// -- SEPARATE colour and alpha blending, and the colour half is the classic
// NON-premultiplied source-over. Every other ported program uses
//     Blend One OneMinusSrcAlpha
// and emits premultiplied colour. This shader emits STRAIGHT (un-premultiplied)
// rgb with coverage in alpha, because the backdrop it composites over is an
// opaque image and dividing by coverage (:257) is how it keeps the outline and
// the tint separable. The GL state is therefore
//     gl.blendEquation(gl.FUNC_ADD);
//     gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA,   // rgb
//                          gl.ONE,       gl.ONE_MINUS_SRC_ALPHA);  // alpha
// and nowui-gl.js carries it as this program's `blend` entry so the state and
// the shader travel together. Drawing glass with the shared premultiplied
// blend produces a panel that is too dark by exactly a factor of its own
// alpha -- plausible, and wrong.
//
// ---------------------------------------------------------------------------
// WHAT IS PORTED, WHAT IS NOT, AND WHY. Nothing here is guessed; each claim
// names the file that decides it.
// ---------------------------------------------------------------------------
// PORTED:
//   * the rounded-box SDF, the AA band, the outline ring, the tint composite
//     and the mask coverage -- i.e. everything that runs when there is no
//     backdrop. This is the ONLY configuration the browser host can reach
//     today: Now.StartUI drives the immediate path, and Now.cs:1155-1157 calls
//     NowGlassRenderer.DisableBackdropGlobal() for every NowMeshKind.Glass
//     mesh on that path, which sets _NowGlassUseBackdrop to 0 and records a
//     NowGlassFallbackReason.LegacyImmediatePath. So glass in the browser is
//     currently a translucent tinted rounded rect with an outline, exactly as
//     Unity's own immediate path draws it -- not a bug, and not a stub.
//   * the flat 2D backdrop branch (:196-201, :246-249). Unreachable today, but
//     it is eight lines and it is what makes this shader complete the day the
//     backend grows render textures and the retained NowRenderer path is wired
//     up. It has been compiled and linked, not exercised; see the report.
//
// NOT PORTED, with the reason each is unreachable rather than merely hard:
//
//   1. THE MATERIAL-BACKDROP HALF (_NowMaterialGlassMode and every
//      _NowMaterial* uniform, :92-102, :165-249). Every one of those is
//      written from NowWorldGraphic.cs (:70-77, :1422, :1511) and
//      NowWorldGraphic.cs is on NowUI.Runtime.csproj's exclude list. In the
//      standalone build _NowMaterialGlassMode is therefore the constant 0 and
//      the `useMaterialBackdrop` ternary picks the _NowGlass* half every time.
//      The uniform is still declared and still tested below, so a non-zero
//      value becomes a visible marker instead of a silent wrong picture.
//
//   2. THE STEREO / TEXTURE-ARRAY BRANCH (:180-195, UNITY_DECLARE_TEX2DARRAY,
//      NowGlassBackdropSlice, unity_StereoEyeIndex). GLSL ES 3.00 does have
//      sampler2DArray, so this one is expressible -- it is not ported because
//      it is unreachable: NowGlassRenderer.cs:260 sets
//      _NowGlassUseStereoBackdrop to 1 only when the capture target's
//      descriptor says TextureDimension.Tex2DArray, which is an XR eye texture.
//      The browser host has no XR path and allocates no array targets. Porting
//      it would cost two more texture units that must be bound to complete
//      array textures on every draw, to serve a branch nothing can select.
//      The `_NowGlassUseStereoBackdrop` test is kept, as a marker.
//
//   3. THE SCENE-DEPTH BRANCH (:202-244) and _CameraDepthTexture. It lives
//      behind `#if defined(NOWUI_GLASS_SCENE_DEPTH)`, a shader keyword enabled
//      from exactly one place, NowWorldGraphic.cs:1543 -- excluded from the
//      standalone build. It also needs a camera depth texture and
//      LinearEyeDepth's _ZBufferParams, neither of which exists without a
//      Unity camera. This is Unity-only, not merely unported. Its consumer
//      _NowGlassSharpBackdropTex is dropped with it rather than left declared
//      and unbound (M2-ShaderPort.md section 4.5, condition 2).
//
// The two unported branches are marked UNCONDITIONALLY (a magenta fragment),
// not behind a #define. nowui-text.frag's gradient marker is opt-in; this one
// cannot be, because guarding it lets the compiler eliminate the very uniforms
// it is meant to watch. The full reasoning is at the marker itself.
//
// PRECISION -- read before "tidying". Unity's fixed4 is a legacy precision
// alias that would map to lowp. Do NOT translate it that way: `delta` is
// `length(vec2(dFdx(dist), dFdy(dist)))`, a difference of nearly equal
// numbers, and the SDF works in UI units that run to the hundreds. The
// fragment language has no default float precision, so declaring it is
// mandatory rather than decorative; `highp sampler2D` matters because the
// fragment default for a sampler is LOWP and this stage samples three of them.
// ===========================================================================

precision highp float;
precision highp int;
precision highp sampler2D;

//#include "nowui-mask.glsl"

// ---------------------------------------------------------------------------
// Uniforms.
//
// UIGlass.shader:79-102 declares twenty. Four are ported, one is kept purely
// as an assertion, and fifteen belong to the three unreachable branches above.
//
//   PORTED
//     :79  sampler2D _NowBackdropTex        the blurred backdrop, bound by
//                                           NowGlassRenderer.cs:265/350/640 with
//                                           SetGlobalTexture -- so it arrives via
//                                           NowRuntime.globals, NOT via any
//                                           material bag (M2-ShaderPort.md 7.3
//                                           step 3). Texture unit 4.
//     :83  float     _NowGlassUseBackdrop   0 on every path the browser host can
//                                           reach today. GL's zero default is
//                                           therefore also the CORRECT value,
//                                           which is what makes glass render
//                                           sensibly before the bridge exists.
//     :84  float     _NowGlassUseStereoBackdrop  kept only to mark branch 2.
//     :86  float4    _NowBackdropUVTransform (scaleX, scaleY, offsetX, offsetY)
//                                           applied to the screen UV.
//                                           **The all-zero GL default is NOT
//                                           safe here**: it collapses every
//                                           backdrop sample to texel (0,0), a
//                                           flat colour that looks like a
//                                           deliberate tint. The backend must
//                                           fall back to (1, 1, 0, 0), which is
//                                           both the Properties-block default
//                                           (:15) and what
//                                           NowGlassRenderer.DisableBackdropGlobal
//                                           writes (:632).
//     :92  float     _NowMaterialGlassMode  kept as an assertion; see branch 1.
//
//   NOT DECLARED (each named with the branch it belongs to, so the omission is
//   searchable rather than silent):
//     :80  _NowGlassSharpBackdropTex              branch 3
//     :81  _NowBackdropArrayTex                   branch 2
//     :82  _NowGlassSharpBackdropArrayTex         branches 2+3
//     :85  _NowGlassBackdropSliceCount            branch 2
//     :88  _CameraDepthTexture                    branch 3 (Unity-only)
//     :90  _NowGlassUseSceneDepth                 branch 3
//     :91  _NowGlassDepthEpsilon                  branch 3
//     :93-102  every _NowMaterial* uniform        branch 1
//
// _ZTest is in the Properties block (:9) but is RENDER STATE, not a uniform.
// It is 8 (CompareFunction.Always) in GlassMaterial.mat and nothing in the
// standalone build writes it. Never bind it to a location.
//
// Texture units are fixed for the life of the backend: 0 _MainTex,
// 1 _NowUITextureMask0, 2 _NowUITextureMask1, 3 reserved for
// _NowGradientRampTexture, 4 _NowBackdropTex (M2-ShaderPort.md section 7.4).
// _NowBackdropTex must ALWAYS be bound to something complete -- the 1x1 opaque
// black fallback, matching the Properties-block default "black" on :11 -- even
// while _NowGlassUseBackdrop is 0. A sampler pointing at an incomplete texture
// makes the whole draw invalid in WebGL2 regardless of dynamic control flow.
// ---------------------------------------------------------------------------
uniform highp sampler2D _NowBackdropTex;
uniform highp float _NowGlassUseBackdrop;
uniform highp float _NowGlassUseStereoBackdrop;
uniform highp vec4 _NowBackdropUVTransform;
uniform highp float _NowMaterialGlassMode;

// Varyings -- must match nowui-glass.vert's `out` block exactly.
in highp vec4 vScreenPos;
in highp vec4 vRect;
in highp vec4 vRadius;
in highp vec4 vColor;
in highp vec4 vOutlineColor;
in highp vec4 vExtras;
in highp vec4 vMask;
in highp vec4 vRawUV;

// UIGlass.shader:137 `fixed4 frag(...) : SV_Target` becomes an explicit out.
out highp vec4 fragColor;

// ---------------------------------------------------------------------------
// UIGlass.shader:104-110. Byte-identical to the copy in UIRectangle.shader,
// UIGradient.shader and UIRipple.shader -- the four files each carry their
// own, so the ports do too rather than inventing a fifth shared include the
// HLSL does not have.
//
//   p = fragment position relative to the box CENTRE, in y-UP space
//   b = half size
//   r = corner radii packed (TR, BR, TL, BL)
//
// p.y > 0 is the TOP because p is built from rawUV, and rawUV.y == 1 is the UI
// top edge (M2-ShaderPort.md section 1.3). :107 reads r.x/r.y AFTER :106 has
// overwritten them -- the order matters, do not reorder.
// ---------------------------------------------------------------------------
highp float sdRoundedBox(highp vec2 p, highp vec2 b, highp vec4 r)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x  = (p.y > 0.0) ? r.x  : r.y;
    highp vec2 q = abs(p) - b + r.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
}

void main()
{
    // UIGlass.shader:140-144. rect.xy is the UI BOTTOM-LEFT corner expressed
    // in negated-y mesh space and rect.zw is always positive, so negating
    // pos.y recovers UI space (y down, positive).
    //
    // The HLSL writes `float2 rawUV = i.rawUV.xy;` explicitly, so unlike
    // UIRectangle there is no float4*float2 truncation to translate.
    highp vec4 rect = vRect;
    highp vec4 mask = vMask;
    highp vec2 rawUV = vRawUV.xy;
    highp vec2 pos = rect.xy + rawUV * rect.zw;
    highp vec2 uiPosition = vec2(pos.x, -pos.y);

    // UIGlass.shader:146 -- the legacy hard clip. Unconditional, no AA, before
    // any other work. May discard.
    NowUIClipLegacyRect(uiPosition, mask);

    // UIGlass.shader:148-153. The shape SDF, in FULL-QUAD space. Do NOT clamp
    // rawUV -- geometry padding legitimately pushes it outside [0,1] and the
    // SDF handles that by construction.
    //
    // NOTE the difference from UIRectangle: `delta` here has NO
    // max(..., 0.0001) floor. That is faithful, not an oversight -- UIGlass
    // simply does not clamp it. On a degenerate quad where both derivatives
    // are exactly zero the following smoothstep gets a == b, which GLSL leaves
    // undefined (as does HLSL). Do not "fix" it here: fixing it would make the
    // browser and Unity disagree on the one input where they currently agree
    // to be undefined together.
    highp vec2 size = rect.zw;
    highp vec2 position = (rawUV - 0.5) * size;
    highp float dist = sdRoundedBox(position, size * 0.5, vRadius);
    highp float delta = length(vec2(dFdx(dist), dFdy(dist)));
    highp float aa = 0.5 * delta;
    highp float graphicAlpha = 1.0 - smoothstep(-aa, aa, dist);

    // UIGlass.shader:155-157. The outline ring. Never thinner than one AA
    // width, or a hairline outline would sit entirely inside the edge fade and
    // wash out. `outline == 0.0` is an EXACT test in the HLSL; keep it exact.
    highp float outline = vExtras.y;
    highp float outlineWidth = max(outline, delta);
    highp float outlineAlpha = (outline == 0.0)
        ? 0.0
        : smoothstep(-outlineWidth - aa, -outlineWidth + aa, dist);

    // UIGlass.shader:159-168. extras.z/.w are the backdrop's saturation and
    // brightness; extras.x is the ambient Now.Opacity/Now.Tint alpha, applied
    // once to the whole pane below (the blur radius travels in the batch key).
    highp float saturation = vExtras.z;
    highp float brightness = vExtras.w;
    highp vec4 tint = vColor;
    highp vec3 fillRgb = tint.rgb;
    highp float fillCoverage = tint.a;
    highp vec2 screenUV = vScreenPos.xy / vScreenPos.w;

    // UIGlass.shader:165-168. useMaterialBackdrop is branch 1 -- unreachable
    // in this build because NowWorldGraphic.cs, its only writer, is excluded
    // from NowUI.Runtime.csproj. The test is kept so that a non-zero value
    // announces itself instead of silently selecting an unported half.
    bool useMaterialBackdrop = _NowMaterialGlassMode > 0.5;
    bool useBackdrop = _NowGlassUseBackdrop > 0.5;

    // UIGlass.shader:170-250. UNITY_BRANCH is a compiler hint with no GLSL
    // equivalent and no meaning here; dropped.
    if (useBackdrop)
    {
        // UIGlass.shader:173-177.
        highp vec4 uvTransform = _NowBackdropUVTransform;
        highp vec2 backdropUV = screenUV * uvTransform.xy + uvTransform.zw;
        highp vec2 clampedBackdropUV = clamp(backdropUV, 0.0, 1.0);

        // UIGlass.shader:196-201, the flat-2D arm of the three-way sample.
        highp vec4 backdrop = texture(_NowBackdropTex, clampedBackdropUV);

        // UIGlass.shader:246-249. Rec.601 luma, then a saturation lerp toward
        // it and a brightness scale. `luminance.xxx` in HLSL is a scalar
        // swizzle, which GLSL does not have -- vec3(luminance) is the same
        // value and is the only way to write it.
        highp float luminance = dot(backdrop.rgb, vec3(0.299, 0.587, 0.114));
        backdrop.rgb = mix(vec3(luminance), backdrop.rgb, saturation) * brightness;
        fillRgb = mix(backdrop.rgb, tint.rgb, tint.a);
        fillCoverage = 1.0;
    }

    // ---------------------------------------------------------------------
    // MARKER for the two branches that are tested but not ported. Neither can
    // fire in this build -- _NowMaterialGlassMode and _NowGlassUseStereoBackdrop
    // are both the constant 0, for the reasons in this file's header -- so this
    // costs two comparisons and never changes a pixel. If one ever DOES fire,
    // the result is a magenta panel rather than a plausible one drawn from the
    // wrong half of the shader.
    //
    // THIS IS DELIBERATELY NOT BEHIND A #ifdef, unlike nowui-text.frag's
    // gradient marker, and the reason is a measured one rather than a
    // preference. Guarding it means `useMaterialBackdrop` and
    // `_NowGlassUseStereoBackdrop` are written and never read in the default
    // build, so the compiler eliminates both uniforms: getUniformLocation
    // returns null for them, the backend has nothing to bind, and the
    // "assertion" asserts nothing. That was observed, not assumed -- the
    // compile-check harness reported exactly those two as MISSING while the
    // #ifdef was in place. An assertion that disappears in the configuration
    // it is meant to guard is worse than no assertion.
    //
    // When a later slice ports either branch, delete this block as part of
    // porting it -- it sits here, immediately above the composite, so that it
    // cannot be missed.
    // ---------------------------------------------------------------------
    if (useMaterialBackdrop || _NowGlassUseStereoBackdrop > 0.5)
    {
        fragColor = vec4(1.0, 0.0, 1.0, 1.0);
        return;
    }

    // UIGlass.shader:252-257. Composite the outline over the fill, then divide
    // the accumulated colour back out by the coverage so the result is
    // STRAIGHT rgb rather than premultiplied -- which is what the separate
    // blend mode at the top of this file expects. The 0.0001 guard is the
    // HLSL's own; below it `rgb` keeps the fill colour it was seeded with.
    highp float outlineCoverage = vOutlineColor.a * outlineAlpha;
    highp float coverage = outlineCoverage + fillCoverage * (1.0 - outlineCoverage);
    highp vec3 rgb = fillRgb;

    if (coverage > 0.0001)
    {
        rgb = (vOutlineColor.rgb * outlineCoverage +
               fillRgb * fillCoverage * (1.0 - outlineCoverage)) / coverage;
    }

    // UIGlass.shader:259-262.
    //
    // Note the asymmetry with UIRectangle and UIRipple, which multiply an
    // already-premultiplied float4 by the mask coverage. This shader applies
    // it to ALPHA ONLY, because its rgb is straight. Both are correct for
    // their own shader; do not unify them.
    highp vec4 col = vec4(rgb, coverage * graphicAlpha * vExtras.x);
    col.a *= NowUIMaskCoverage(uiPosition);

    // UIGlass.shader:261 -- clip(col.a - 0.001). HLSL clip discards on
    // strictly negative, so alpha exactly 0.001 survives.
    if (col.a - 0.001 < 0.0)
        discard;

    // UIGlass.shader:262
    fragColor = col;
}
