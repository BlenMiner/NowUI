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
