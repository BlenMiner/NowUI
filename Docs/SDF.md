# SDF Shapes

`NowUI.Extensions.Sdf` draws several signed-distance-field primitives as one
material-backed quad. Shape coordinates are local to the scene rect, and each
shape merges with the accumulated field using the operation selected before it.

```csharp
using NowUI;
using NowUI.Sdf;
using UnityEngine;

NowSdf.Scene(new NowRect(20, 20, 180, 120))
    .SetColor(new Color(1f, 0.25f, 0.18f, 1f))
    .Circle(new Vector2(54, 60), 42)
    .SetColor(new Color(0.1f, 0.85f, 1f, 1f))
    .SmoothUnion(14)
    .RoundedBox(new NowRect(48, 26, 112, 68), 18)
    .Subtract()
    .Circle(new Vector2(104, 60), 20)
    .Draw();
```

Available primitives:

- `Circle(center, radius)`
- `Box(rect)` / `Rectangle(rect)`
- `RoundedBox(rect, radius)` / `RoundRect(rect, radius)`
- `Ellipse(rect)`
- `Capsule(from, to, radius)` or `Capsule(rect)`

Operations apply to the next primitive only, then reset to `Union`:

```csharp
.Union()
.Subtract()
.Intersect()
.SmoothUnion(12)
.SmoothSubtract(10)
.SmoothIntersect(10)
```

Edges are anti-aliased in screen space. `SetFeather(0)` gives the crisp default
one-pixel ramp; `SetFeather(1)` widens that transition by roughly one extra
screen pixel, independent of Canvas Scaler changes.

## Effects

Effects are applied to the final composed scene field. They work on primitives,
graphs, morphs, and SDF text together:

```csharp
NowSdf.Scene(new NowRect(20, 20, 220, 140))
    .SetColor(new Color(0.15f, 0.95f, 0.8f, 1f))
    .SetOutline(4f, new Color(0.02f, 0.04f, 0.08f, 0.85f), 1f)
    .SetShadow(new Vector2(8f, 10f), 16f, new Color(0f, 0f, 0f, 0.28f), 2f)
    .SetGlow(24f, new Color(0.2f, 0.75f, 1f, 0.35f), 1.6f)
    .SetEmboss(new Vector2(-0.6f, -0.8f), 0.35f, 7f)
    .SetContours(14f, 1.5f, new Color(1f, 1f, 1f, 0.18f), Time.time * 12f)
    .SetWarp(3f, 48f, 0.25f)
    .SmoothUnion(18f)
    .Circle(new Vector2(78, 72), 42)
    .RoundedBox(new NowRect(76, 38, 104, 68), 20)
    .Draw();
```

Available scene effects:

- `SetOutline(width, color, softness = 0)` draws an outer stroke.
- `SetShadow(offset, softness, color, spread = 0)` draws a soft drop shadow.
- `SetInnerShadow(offset, softness, color, spread = 0)` darkens inside edges.
- `SetGlow(radius, color, power = 1)` draws an outside halo.
- `SetEmboss(lightDirection, strength = 0.35, size = 6)` lights the edge band.
- `SetContours(spacing, width, color, offset = 0)` draws distance rings.
- `SetWarp(amplitude, scale, speed = 0, seed = 0)` bends the distance domain
  before the scene is evaluated.

Outlines, shadows, and glows can only render inside the scene quad and mask. If
an effect should extend beyond a shape, give the scene rect enough empty space
around the drawn primitives.

## Reusable Graphs

Use `NowSdf.Graph()` when a shape set should be reused or combined as one
scene-level operand.

```csharp
var badge = NowSdf.Graph()
    .SetColor(new Color(0.95f, 0.18f, 0.22f, 1f))
    .Circle(new Vector2(56, 56), 46)
    .SetColor(new Color(1f, 0.55f, 0.12f, 1f))
    .SmoothUnion(12)
    .RoundedBox(new NowRect(46, 26, 108, 60), 18);

var hole = NowSdf.Graph()
    .Circle(new Vector2(96, 56), 22);

NowSdf.Scene(new NowRect(20, 20, 180, 120))
    .Graph(badge)
    .Subtract()
    .Graph(hole)
    .Draw();
```

For animated graphs, keep the graph instance and call `Clear()` before
rebuilding it. That keeps steady-state frames free of graph object allocation:

```csharp
readonly NowSdfGraph _blob = NowSdf.Graph();

void Draw(NowRect rect)
{
    _blob.Clear()
        .SetColor(Color.cyan)
        .UseColor()
        .Circle(new Vector2(Mathf.PingPong(Time.time * 40f, 120f), 48f), 32f);

    NowSdf.Scene(rect)
        .Graph(_blob)
        .Draw();
}
```

The same pattern works inside custom controls: keep `NowControls.Interact` on a
stable rect, then animate the graph content from hover/focus transitions.

The same scene operations work on graph layers:

```csharp
NowSdf.Scene(rect)
    .Graph(sceneA)
    .SmoothUnion(16)
    .Graph(sceneB)
    .Intersect()
    .Graph(mask)
    .Draw();
```

## Morphs

`Morph(a, b, t)` evaluates both graphs and linearly interpolates their
distances and fills. It is a real distance-field transition, not a crossfade,
so unrelated topologies can produce interesting intermediate fields.

```csharp
var circle = NowSdf.Graph()
    .SetColor(Color.cyan)
    .Circle(new Vector2(72, 60), 44);

var pill = NowSdf.Graph()
    .SetColor(Color.magenta)
    .Capsule(new NowRect(24, 30, 116, 60));

NowSdf.Scene(new NowRect(20, 20, 160, 120))
    .Morph(circle, pill, Mathf.PingPong(Time.time, 1f))
    .Draw();
```

Colors are per shape. One scene texture can be bound and used by subsequent
shapes; switch back to solid fills with `UseColor()`.

```csharp
NowSdf.Scene(rect)
    .SetTexture(noiseTexture)
    .RoundedBox(new NowRect(0, 0, 180, 120), 20)
    .UseColor()
    .SetColor(Color.white)
    .Subtract()
    .Circle(new Vector2(90, 60), 28)
    .Draw();
```

For layout flow, omit the rect and give the builder a size:

```csharp
NowSdf.Scene(180, 120)
    .SetStretchWidth()
    .SetColor(Color.magenta)
    .Ellipse(new NowRect(20, 20, 140, 80))
    .Draw();
```
