# Text Gradients And Animation

`NowText` supports gradient fills and built-in glyph animations directly on the
text builder. They compose with the existing font, outline, mask, shaping, and
Effects APIs; no texture capture or separate mask draw is required.

```csharp
float elapsed = Time.unscaledTime - titleStartedAt;

Now.Text(titleRect)
    .SetFontSize(48f)
    .SetGradient(
        new Color(0.12f, 0.58f, 1f),
        new Color(0.82f, 0.22f, 0.94f))
    .SetGradientLinear(100f)
    .SetAnimation(NowTextAnimations.FadeUp(
        distance: 12f,
        duration: 0.4f,
        stagger: 0.035f))
    .SetTime(elapsed)
    .Draw("Welcome");
```

## Gradient Fills

`SetGradient(from, to)` enables a two-color, top-to-bottom fill. Linear angles
follow the same CSS convention as `Now.Gradient`: zero points up, 90 points
right, and positive angles rotate clockwise.

```csharp
Now.Text(rect)
    .SetFontSize(36f)
    .SetGradient(Color.cyan, new Color(0.55f, 0.2f, 1f))
    .SetGradientLinear(90f)
    .Draw("Horizontal gradient");
```

The mapping is evaluated across the text rectangle rather than restarting for
each glyph. Switch geometry with `SetGradientRadial()` or
`SetGradientConic()`. Spread and repetition use the existing gradient enums:

```csharp
Now.Text(rect)
    .SetFontSize(42f)
    .SetGradient(stripeA, stripeB)
    .SetGradientLinear(90f)
    .SetGradientSpread(NowGradientSpread.Mirror)
    .SetGradientRepetitions(6f)
    .Draw("STRIPES");
```

Radial and conic centers use normalized text-bound coordinates: `(0, 0)` is
the top-left and `(1, 1)` is the bottom-right. Ellipse radii are normalized to
width and height; circle radii use the smaller dimension. Use
`SetGradientBounds(bounds)` to pin the mapping to a stable rectangle when
several draws or runs should share one continuous fill. `NowTextWrap` and
`NowRichText` do this automatically across their generated runs.

Use a Unity `Gradient` when the ramp needs more than two color or alpha keys.
The optional revision follows `Now.Gradient` cache semantics: increment it
when the same `Gradient` instance is edited in place, or invalidate the ramp
explicitly.

```csharp
[SerializeField] Gradient titleRamp;
int titleRampRevision;

void DrawTitle(NowRect rect)
{
    Now.Text(rect)
        .SetFontSize(40f)
        .SetGradientRamp(titleRamp, titleRampRevision)
        .SetGradientConic()
        .Draw("Spectrum");
}

void ReplaceRampKeys(GradientColorKey[] colors, GradientAlphaKey[] alphas)
{
    titleRamp.SetKeys(colors, alphas);
    Now.InvalidateGradient(titleRamp);
}
```

Call `ClearGradient()` to return the builder to its solid fill, then use
`SetColor` normally. Solid text color leaves the authored RGB of RGBA/color-font
glyphs intact and applies only its alpha, so emoji keep their original colors
while still fading with the surrounding label. Enabling a text gradient
intentionally replaces the glyph RGB with the sampled ramp while retaining the
glyph's alpha coverage.

## Text Outlines

The text outline is controlled by `SetOutline` and `SetOutlineColor`.
`SetOutline` uses em units; `SetOutlinePixels` converts pixels using the font
size at the point it is called, so call `SetFontSize` first. This example keeps
the exaggerated width serialized so it can be scrubbed directly in the
Inspector:

```csharp
[SerializeField] NowFont font;
[SerializeField, Range(0f, 100f)] float largeOutlinePixels = 32f;

void DrawOutlineDemo(NowRect thinRect, NowRect largeRect)
{
    const float fontSize = 80f;

    Now.Text(thinRect, font)
        .SetFontSize(fontSize)
        .SetColor(Color.white)
        .SetOutlinePixels(2f)
        .SetOutlineColor(Color.black)
        .Draw("Thin outline");

    Now.Text(largeRect, font)
        .SetFontSize(fontSize)
        .SetColor(Color.white)
        .SetOutlinePixels(largeOutlinePixels)
        .SetOutlineColor(Color.black)
        .Draw("Inspector-driven outline");
}
```

Scrub `largeOutlinePixels` up to 100 px. This source-backed 0–100 demo remains
exact while the font reuses hidden backing tiers instead of creating an atlas
variant for every slider value. Each previously unseen glyph at a tier may bake
and upload once; warmed glyphs reuse it. The 100 px upper bound belongs to the
demo control, not the runtime. Dynamic capacity still has a terminal tier set by
atlas geometry and the private cache budget, so no range or cache setting is
required from the caller.

Dynamic SDF fonts with embedded source automatically choose distance-field
capacity for the requested outline up to that terminal tier. Authored widths
remain exact within the representable range while backing glyphs share hidden
doubling tiers up to the cap. Effect tiers use fixed-size sparse pages: a live
page is append-only, existing glyph UVs remain stable, and a full page seals and
spills into another page rather than resizing. A private 64 MiB logical-payload
budget bounds each font's generated dynamic resources. Its accounting includes
GPU page payloads, readable CPU copies, live baking atlases, and a conservative
working reserve; it is not a total process-memory ceiling and does not include
serialized/base font assets or ordinary Unity object overhead. Under pressure
NowUI seals old writable sessions without destroying published glyphs. If a new
page still cannot fit, the glyph uses its best cached lower-range variant and
clamps safely until the cache is cleared. Wider outlines still bake larger glyph
cells and can allocate more atlas pages, increasing first-use bake time and
texture memory.

With NowUI's bundled, two-pass-capable SDF materials, positive outlined runs
submit an outline layer followed by a fill layer so neighboring strokes do not
paint over earlier glyph faces. This roughly doubles visible SDF glyph geometry,
including single-character and span draws, and can add a material batch. Legacy
custom materials without that capability keep their combined-pass behavior;
RGBA/color glyphs do not receive an SDF outline layer. Managed effect pages
retain 16-bit distance precision inside RGBA32, so the precision upgrade itself
does not increase bytes per texel (larger cells and pages still increase total
memory).
A static-only SDF atlas cannot grow beyond its authored range, and RGBA/color-font
glyphs do not support SDF outlines. Widths beyond the available/configured atlas
range are clamped inside the field instead of exposing a rectangular cell edge.
An explicit `SetMask` remains a hard clip; outset it when the stroke should
extend past its bounds.

## Built-In Text Animations

Animations are value configurations created by `NowTextAnimations` and applied
with `SetAnimation`. The built-in presets cover the common entrance, reveal,
and continuous-motion cases:

- `Typewriter(charactersPerSecond)` reveals text at the requested rate.
- `FadeIn(duration, stagger)` fades glyphs in with an optional delay between
  successive glyphs.
- `FadeUp(distance, duration, stagger)` combines an upward entrance with a
  fade.
- `ScaleIn(startScale, duration, stagger)` grows glyphs from the supplied
  scale.
- `Wave(amplitude, wavelength, speed)` applies continuous vertical motion.

Timing advances in text units, not raw UTF-16 code units. Shaped string draws
use HarfBuzz clusters, so ligatures and combining sequences reveal and move
atomically. Unshaped span and rich-text runs use allocation-free grapheme
grouping within each run for common combining marks, emoji modifiers, ZWJ
sequences, and flags. That fallback is intentionally not a complete UAX #29
implementation; prefer shaped string draws for complex-script animation.

Animation values are immutable. `SetDelay` offsets playback; entrance presets
also support `SetDuration`, `SetStagger`, and `SetEasing` with
`NowTextAnimationEasing.Linear`, `EaseIn`, `EaseOut`, or `EaseInOut`.

```csharp
Now.Text(messageRect)
    .SetFontSize(22f)
    .SetAnimation(NowTextAnimations.Typewriter(charactersPerSecond: 28f))
    .SetTime(Time.unscaledTime - messageStartedAt)
    .Draw(message);
```

`SetTime` always receives caller-owned elapsed seconds. `NowText` does not
start a hidden clock, which keeps rendering deterministic and makes animations
easy to pause, scrub, restart, or test. Store the start time when an entrance
begins and subtract it each frame. To replay an animation deliberately, wrap
the elapsed time yourself:

```csharp
float replayTime = Mathf.Repeat(Time.unscaledTime, 2.5f);

Now.Text(titleRect)
    .SetAnimation(NowTextAnimations.ScaleIn(
        startScale: 0.7f,
        duration: 0.35f,
        stagger: 0.04f))
    .SetTime(replayTime)
    .Draw("Replay");
```

One-shot presets remain at their completed appearance after their timeline
ends. `Wave` uses time as a continuous phase. `ClearAnimation()` restores
ordinary static text without changing its gradient or other styling.

Animation changes glyph presentation, not text measurement. Layout can reserve
the final text size once while a typewriter or entrance animation runs inside
that stable rectangle. Default text masks expand for bounded FadeUp and Wave
motion; a mask supplied explicitly with `SetMask` remains exact by design.

`NowLayout.Label(...)` and `Now.RichText(...)` forward the same gradient,
animation, time, and clear methods. Wrapped and rich text keep one sequence and
one gradient mapping across their generated runs instead of restarting each
word or style span.

## Hosts, Effects, And Performance

Immediate frame owners such as `OnPostRender` naturally redraw while their
camera renders. When `SetTime` is present, unfinished one-shot animations and
live waves request the next retained-host repaint through `NowControlState`;
requests stop once a finite animation settles. The caller still owns and
advances the clock. `SetNormalizedTime(progress)` supports externally driven
scrubbing without starting a repaint loop by itself.

Dedicated text animation and `NowEffects.Modifier(...)` solve different
problems and can be composed. Use `NowTextAnimations` for glyph reveal,
stagger, opacity, scale, and baseline motion; use Effects when an entire text
draw or mixed-content region should bend, pull, or flatten to a texture.

Gradient ramps and text materials may allocate or upload data on first use.
Warm representative fonts, glyphs, ramps, animation presets, and the actual
rendering host before measuring steady state. Reuse `Gradient` instances rather
than constructing a new ramp every frame.
