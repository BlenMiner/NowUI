# Gradients

`Now.Gradient(...)` draws CSS-inspired linear, radial, and conic paints without
adding gradient state to `NowRectangle`.

```csharp
Now.Gradient(rect, new Color(0.12f, 0.48f, 1f), new Color(0.72f, 0.2f, 0.95f))
    .SetLinear(135f)
    .SetRadius(14f)
    .Draw();
```

The default mapping is a top-to-bottom linear gradient. Angles follow CSS:
zero points up, 90 points right, and positive angles rotate clockwise. Named
directions are available when an angle would be less readable.

```csharp
Now.Gradient(rect)
    .SetColors(Color.cyan, Color.blue)
    .SetLinear(NowGradientDirection.ToBottomRight)
    .Draw();
```

## Radial And Conic Geometry

Normalized gradient coordinates use a top-left origin. A centered ellipse with
radius `(0.5, 0.5)` reaches the middle of each rectangle edge.

```csharp
Now.Gradient(rect, inner, outer)
    .SetRadial(
        center: new Vector2(0.5f, 0.42f),
        radius: new Vector2(0.5f, 0.62f))
    .SetRadius(18f)
    .Draw();
```

The scalar radial overload draws a screen-space circle. Its radius is relative
to the rectangle's smaller dimension.

```csharp
Now.Gradient(rect, glow, Color.clear)
    .SetRadial(new Vector2(0.5f, 0.5f), radius: 0.5f)
    .Draw();
```

Conic gradients sweep clockwise around their center. The start angle uses the
same CSS convention as linear gradients.

```csharp
Now.Gradient(rect, hueRamp)
    .SetConic(new Vector2(0.5f, 0.5f), startAngle: -90f)
    .SetRadius(rect.height * 0.5f)
    .Draw();
```

## Unity Gradient Ramps

Pass a Unity `Gradient` to use all of its color keys, alpha keys, and gradient
mode.

```csharp
[SerializeField] Gradient statusRamp;

void DrawStatus(NowRect rect)
{
    Now.Gradient(rect, statusRamp)
        .SetLinear(NowGradientDirection.ToRight)
        .Draw();
}
```

Ramp textures are cached by `Gradient` object identity so steady-state drawing
does not read Unity's allocating `colorKeys` and `alphaKeys` properties. When
code mutates the same `Gradient` instance in place, either increment a caller-
owned revision passed to `Now.Gradient(rect, ramp, revision)` / `SetRamp`, or
invalidate it explicitly:

```csharp
statusRamp.SetKeys(colors, alphas);
Now.InvalidateGradient(statusRamp);
```

`Now.GradientField(...).Draw(ref statusRamp)` invalidates the rendering cache
automatically when its editor applies changes.

## Spread And Repetition

Positions outside the ramp can clamp, repeat, or mirror. `SetRepetitions`
scales the position before applying the spread mode.

```csharp
Now.Gradient(rect, stripeA, stripeB)
    .SetLinear(90f)
    .SetSpread(NowGradientSpread.Mirror)
    .SetRepetitions(8f)
    .Draw();
```

For radial gradients, repetitions reduce the effective radius and create
concentric bands. For conic gradients they create repeated angular sectors.

## Shape Styling And Performance

Gradient paints support the rectangle geometry features that remain meaningful
for a generated fill: mask, position, padding, corner radius, edge blur, tint,
and a solid outline.

All gradient kinds use one quad and one shared gradient material. Color ramps
occupy rows in a shared 256-row atlas, so consecutive gradients with different
ramps and geometry can batch together. Row zero is reserved for diagnostics;
reuse color pairs and `Gradient` instances rather than creating unbounded
unique ramps. First use and the first use of a new ramp may allocate or upload
texture data; repeated draws are allocation-free after representative warmup.

`NowGradient` intentionally does not expose rectangle textures, sprites, or
custom materials. Compose those as separate draws when needed.
