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

The text outline remains controlled by `SetOutline` and `SetOutlineColor`.
Call `ClearGradient()` to return the builder to its solid fill, then use
`SetColor` normally.

For RGBA/color-font glyphs, enabling a text gradient intentionally replaces
the glyph RGB with the sampled ramp while retaining the glyph's alpha coverage.

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
