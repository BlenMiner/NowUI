# Shapes

Core shapes cover simple immediate-mode geometry without pulling in the SDF
extension: filled or outlined circles, ellipses, triangles, and polygons. They
render through the same draw-list mesh path as rectangles, lines, text, and
Lottie.

## Circles And Ellipses

`Now.Circle` creates a filled circle by default.

```csharp
Now.Circle(new Vector2(80, 80), 36f)
    .SetColor(new Color(0.05f, 0.86f, 0.67f, 1f))
    .Draw();
```

Use `SetOutline` and `SetOutlineColor` to add a stroked edge. Use
`SetFill(false)` for an outline-only circle.

```csharp
Now.Circle(new Vector2(180, 80), 34f)
    .SetFill(false)
    .SetOutline(4f)
    .SetOutlineColor(Color.white)
    .Draw();
```

Ellipses use either a bounding rect or center/radius pair.

```csharp
Now.Ellipse(new NowRect(240, 46, 96, 68))
    .SetColor(new Color(1f, 0.72f, 0.16f, 1f))
    .SetOutline(2f)
    .SetOutlineColor(new Color(0f, 0f, 0f, 0.3f))
    .Draw();
```

Segments are chosen from the radius. Call `SetSegments` when you need an exact
vertex budget or a deliberately faceted look.

## Triangles

Triangles do not require an array allocation.

```csharp
Now.Triangle(
        new Vector2(60, 160),
        new Vector2(120, 160),
        new Vector2(90, 104))
    .SetColor(new Color(0.92f, 0.24f, 0.58f, 1f))
    .Draw();
```

Outlined triangles use the same stroke expansion as closed line paths.

```csharp
Now.Triangle(a, b, c)
    .SetFill(false)
    .SetOutline(3f)
    .SetOutlineColor(Color.white)
    .Draw();
```

## Polygons

Polygons accept `Vector2[]` or `List<Vector2>` so callers can own and reuse the
point storage. There is intentionally no `params Vector2[]` overload because
that would allocate for inline call sites.

```csharp
readonly Vector2[] _points = new Vector2[5];

void DrawBadge(Vector2 center)
{
    for (int i = 0; i < _points.Length; ++i)
    {
        float angle = Mathf.PI * 2f * i / _points.Length - Mathf.PI * 0.5f;
        _points[i] = center + new Vector2(Mathf.Cos(angle) * 42f, Mathf.Sin(angle) * 32f);
    }

    Now.Polygon(_points)
        .SetColor(new Color(0.1f, 0.55f, 1f, 1f))
        .SetOutline(2f)
        .SetOutlineColor(Color.white)
        .Draw();
}
```

The fill uses nonzero winding and supports concave simple polygons. Avoid
self-intersecting point sets unless that winding behavior is what you want.

## Masks And Performance

`SetMask` clips shapes, and ambient `Now.Mask(...)` scopes apply as usual.

```csharp
using (Now.Mask(viewport))
{
    Now.Polygon(points)
        .SetMask(viewport)
        .SetColor(Color.cyan)
        .Draw();
}
```

The first frame that exceeds the reusable buffer capacities may allocate while
buffers grow. Fill and stroke anti-aliasing is measured in screen pixels:
`Now.StartUI(uiScale)`, `NowPipelineGraphic.BuildDrawList(..., uiScale)`, and
UGUI `NowGraphic` canvas scale convert that feather back into local UI units
automatically. Shape radii and outline widths remain UI units, so they scale
with the canvas.

Steady-state draws are managed-allocation-free when callers reuse polygon
storage and capacities are warm.
