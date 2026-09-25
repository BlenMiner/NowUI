# Lines

`NowLine` draws anti-aliased strokes through the same immediate-mode pipeline as
rectangles, text, and Lottie. Use it for straight segments, cubic Beziers,
arrow heads, separators, graph edges, and selection/annotation overlays.
`NowPolyline` and `NowArc` apply the same stroke styling to sampled paths,
closed loops, circular arcs, and rings.

## Straight Lines

Use `Now.Line(...)` with top-left UI coordinates.

```csharp
Now.Line(new Vector2(24, 40), new Vector2(260, 40))
    .SetWidth(2f)
    .SetColor(Color.white)
    .Draw();
```

Caps are butt by default. Use `SetCap` for rounded or square ends.

```csharp
Now.Line(24, 64, 260, 64)
    .SetWidth(8f)
    .SetCap(NowLineCap.Round)
    .SetColor(new Color(0.1f, 0.55f, 1f, 1f))
    .Draw();
```

## Connected Polylines

Use `Now.DrawPolyline(...)` for charts, sparklines, and other sampled paths.
It emits one connected stroke with shared joins. Do not draw each adjacent pair
as a separate butt-capped `Now.Line`: independent segment ends can leave tiny
anti-aliasing seams at bends.

```csharp
Span<Vector2> samples = stackalloc Vector2[]
{
    new Vector2(24, 112),
    new Vector2(72, 76),
    new Vector2(138, 88),
    new Vector2(220, 42)
};

Now.DrawPolyline(
    samples,
    width: 2.5f,
    cap: NowLineCap.Round,
    color: new Color(0.2f, 0.9f, 0.6f, 1f));
```

`cap` affects only the first and last point; interior points use connected
mitered joins. Consecutive duplicate points are ignored. The span is consumed
immediately, so stack-allocated sample buffers remain allocation-free.

`DrawPolyline` is a one-call solid stroke. Prefer the `Now.Polyline` builder
below when the path needs a gradient, dashes, arrow heads, a closed loop, or
early rejection when the whole path is outside the mask.

## Polylines With Styling

`Now.Polyline(points)` is the builder form of a connected stroke. It takes the
same `SetWidth`, `SetColor`, `SetGradient`, `SetCap`, `SetDash`/`SetSolid`,
`SetArrow`, and `SetMask` calls as `Now.Line`.

```csharp
readonly Vector2[] _samples = new Vector2[64];

void DrawTrend(NowRect plot)
{
    for (int i = 0; i < _samples.Length; ++i)
    {
        float x = plot.x + plot.width * i / (_samples.Length - 1);
        _samples[i] = new Vector2(x, plot.center.y + Mathf.Sin(i * 0.3f) * plot.height * 0.4f);
    }

    Now.Polyline(_samples)
        .SetWidth(3f)
        .SetCap(NowLineCap.Round)
        .SetGradient(new Color(0.05f, 0.86f, 0.67f, 1f), new Color(0.92f, 0.24f, 0.58f, 1f))
        .SetArrow(NowLineArrow.End)
        .Draw();
}
```

Overloads accept `Vector2[]` or `List<Vector2>`, optionally with a count or a
start and count, so callers own and reuse the storage. The builder keeps a
reference to that storage and reads the points when `Draw()` runs, not when
`Now.Polyline(...)` is called. Nothing is retained after `Draw()`. There is no
`params` overload because it would allocate at every call site.

`SetGradient(from, to)` blends by distance along the whole path. `SetClosed()`
joins the last point back to the first with the same mitered join as any other
corner; a repeated closing point is optional. Closed paths ignore caps and
arrow heads, and their dash pattern continues across the seam instead of
restarting there.

```csharp
Now.Polyline(_starPoints)
    .SetWidth(3f)
    .SetDash(14f, 7f, Time.time * 20f)
    .SetClosed()
    .SetColor(new Color(1f, 0.72f, 0.16f, 1f))
    .Draw();
```

A closed path needs three distinct points; with fewer it draws as an open
stroke. Points follow the active `Now.Transform` individually, like
`DrawPolyline`. Joins use the same miter limit as `DrawPolyline`, so very
sharp corners such as star tips are blunted rather than drawn as long spikes.

## Bezier Curves

`Now.Bezier` draws a cubic Bezier stroke. The curve is flattened with a small
screen-space tolerance, then emitted as anti-aliased stroke geometry.

```csharp
Now.Bezier(
        new Vector2(24, 120),
        new Vector2(90, 32),
        new Vector2(190, 208),
        new Vector2(260, 120))
    .SetWidth(5f)
    .SetCap(NowLineCap.Round)
    .SetColor(new Color(0.05f, 0.86f, 0.67f, 1f))
    .Draw();
```

## Arcs And Rings

`Now.Arc(center, radius, startAngle, sweep)` strokes a circular arc with the
same styling calls, plus `SetSegments`. Angles are radians and match the SDF
`Arc` primitive: `0` points right, and a positive sweep turns clockwise on
screen because UI space is y-down. A sweep of a full turn (`Mathf.PI * 2f`) or
more draws one closed ring with no caps or seam; a zero sweep or radius draws
nothing.

A progress arc with rounded ends over a dim track:

```csharp
float progress = 0.72f;

Now.Arc(center, 48f, 0f, Mathf.PI * 2f)
    .SetWidth(10f)
    .SetColor(new Color(1f, 1f, 1f, 0.08f))
    .Draw();

Now.Arc(center, 48f, -Mathf.PI * 0.5f, Mathf.PI * 2f * progress)
    .SetWidth(10f)
    .SetCap(NowLineCap.Round)
    .SetGradient(new Color(0.38f, 0.75f, 1f, 1f), new Color(1f, 0.45f, 0.75f, 1f))
    .Draw();
```

A dashed orbit ring that rotates by animating the start angle. Dash lengths
are measured along the true circle and a ring's pattern continues across its
seam, so a circumference that is a whole number of `dash + gap` patterns keeps
every dash evenly spaced:

```csharp
float radius = 80f;
float pattern = Mathf.PI * 2f * radius / 12f;

Now.Arc(center, radius, Time.time * 0.8f, Mathf.PI * 2f)
    .SetWidth(4f)
    .SetDash(pattern * 0.6f, pattern * 0.4f)
    .SetColor(new Color(0.38f, 0.75f, 1f, 1f))
    .Draw();
```

Gradients run from the start angle along the sweep; a gradient ring changes
color abruptly at its seam, where it returns to the start color. Caps and
arrow heads apply to partial arcs only.

The default segment count keeps chord error within 0.2 screen pixels after
the active transform and UI scale. `SetSegments(n)` overrides it for the
whole sweep, which is useful for a deliberately faceted look. Arc points are
sampled first and then transformed, so a non-uniform `Now.Transform` scale
draws an ellipse. Stroke width scales by the larger transform axis, as it does
for lines.

## Gradients

`SetGradient(from, to)` blends the stroke color from the start of the line to
its end. Lines, Beziers, polylines, and arcs interpolate by distance along the
stroke;
dashes pick up the slice of the gradient that matches their position, and
arrow heads take their endpoint's color. `SetColor` switches back to a solid
stroke.

```csharp
Now.Bezier(from, c1, c2, to)
    .SetWidth(3f)
    .SetCap(NowLineCap.Round)
    .SetGradient(new Color(0.38f, 0.75f, 1f, 1f), new Color(1f, 0.62f, 0.3f, 1f))
    .Draw();
```

## Dashes

`SetDash(dash, gap, offset)` splits a stroke by distance along the flattened
path. The offset is useful for animated flow indicators. The same call works
on `Now.Polyline` and `Now.Arc`; on closed polylines and rings the pattern
continues across the seam.

```csharp
Now.Bezier(
        new Vector2(24, 160),
        new Vector2(90, 92),
        new Vector2(190, 228),
        new Vector2(260, 160))
    .SetWidth(3f)
    .SetDash(12f, 8f, Time.time * 32f)
    .SetColor(new Color(1f, 0.72f, 0.16f, 1f))
    .Draw();
```

Call `SetSolid()` to clear a dash pattern on a reused builder value.

## Arrow Heads

Arrow heads are drawn at the start, end, or both ends of the path.

```csharp
Now.Line(new Vector2(24, 220), new Vector2(260, 220))
    .SetWidth(4f)
    .SetArrow(NowLineArrow.End)
    .SetColor(new Color(0.92f, 0.24f, 0.58f, 1f))
    .Draw();
```

Pass explicit head dimensions when the default `width`-scaled head is not
enough.

```csharp
Now.Line(new Vector2(260, 250), new Vector2(24, 250))
    .SetWidth(3f)
    .SetArrow(NowLineArrow.Both, length: 18f, width: 14f)
    .Draw();
```

Arrow heads are independent of the dash pattern, so dashed connector lines can
still have a solid head at the endpoint.

## Masks And Performance

Use `SetMask` or an ambient `using (Now.Mask(rect))` scope to clip line
geometry like other NowUI draws. `Now.Line`, `Now.Bezier`, `Now.Polyline`, and
`Now.Arc` skip tessellation entirely when a stroke's padded bounds miss the
mask.

For an anti-aliased rounded, circular, elliptical, or capsule boundary, wrap
the line in `Now.Mask(NowMaskShape)`; see [Masks](Masks.md).

```csharp
using (Now.Mask(viewport))
{
    Now.Bezier(start, c1, c2, end)
        .SetWidth(3f)
        .SetDash(10f, 6f)
        .SetMask(viewport)
        .Draw();
}
```

Lines, polylines, and arcs batch with the default NowUI material and append to
the existing mesh streams. Anti-aliasing, Bezier flattening, and arc
segmentation are measured in screen pixels:
`Now.StartUI(uiScale)`, `NowPipelineGraphic.BuildDrawList(..., uiScale)`, and
UGUI `NowGraphic` canvas scale convert those pixels back into local UI units
automatically. Widths, dash lengths, and arrow dimensions remain UI units, so
they still scale with the rest of the canvas.

The first large frame may grow reusable buffers; steady-state drawing is
managed-allocation-free when capacities are already warm.
