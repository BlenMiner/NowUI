# Lines

`NowLine` draws anti-aliased strokes through the same immediate-mode pipeline as
rectangles, text, and Lottie. Use it for straight segments, cubic Beziers,
arrow heads, separators, graph edges, and selection/annotation overlays.

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

## Dashes

`SetDash(dash, gap, offset)` splits a stroke by distance along the flattened
path. The offset is useful for animated flow indicators.

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
geometry like other NowUI draws.

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

Lines batch with the default NowUI material and append to the existing mesh
streams. Anti-aliasing and Bezier flattening are measured in screen pixels:
`Now.StartUI(uiScale)`, `NowPipelineGraphic.BuildDrawList(..., uiScale)`, and
UGUI `NowGraphic` canvas scale convert those pixels back into local UI units
automatically. Widths, dash lengths, and arrow dimensions remain UI units, so
they still scale with the rest of the canvas.

The first large frame may grow reusable buffers; steady-state drawing is
managed-allocation-free when capacities are already warm.
