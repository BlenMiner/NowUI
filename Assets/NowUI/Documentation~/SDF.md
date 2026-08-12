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
- `Arc(center, radius, thickness, from, sweep)`
- `Pie(center, radius, from, sweep)`

Arc and pie angles are radians. An angle of `0` points right; positive sweeps
turn clockwise in NowUI's top-left-origin UI space, and negative sweeps turn
counter-clockwise. A zero sweep adds no primitive. Sweeps whose absolute value
is at least `2 * Mathf.PI` are clamped to one seamless full ring or disc rather
than wrapped back toward zero. Arc `thickness` is the half-width around its
ring radius, so the complete band is twice that value. Radial arguments must be
finite; negative radii and thicknesses are clamped to zero.

Texture fills use the same axis-aligned planar mapping as other primitives.
Arc UVs cover the conservative outer-ring square and pie UVs cover the full
disc square, even when only part of that area lies inside the angular sweep.
Inputs must also produce finite, representable conservative bounds.

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

## Use A Scene As A Mask

Finish a scene with `BeginMask()` instead of `Draw()` when its composited alpha
should clip ordinary NowUI content:

```csharp
var mask = NowSdf.Scene(cardRect, "status-card-mask")
    .SetMaskResolutionScale(0.5f)
    .SetFeather(1f)
    .Circle(new Vector2(44f, 48f), 38f)
    .SmoothUnion(10f)
    .RoundedBox(new NowRect(38f, 14f, 150f, 68f), 22f)
    .Subtract()
    .Circle(new Vector2(162f, 48f), 13f);

using (mask.BeginMask())
{
    Now.Gradient(cardRect, Color.cyan, Color.blue)
        .SetLinear(90f)
        .Draw();

    Now.Text(cardRect.Inset(14f))
        .SetColor(Color.white)
        .Draw("Composed SDF mask");
}
```

`BeginMask()` requires the `Scene(rect, id)` form. If the builder was created
without a rect, pass the resolved rect to `BeginMask(rect)`. Prefer reserving a
layout rect explicitly before building the scene: during a measure pass mask
creation performs no GPU work and installs no ambient mask.

The mask uses final output alpha, not shape distance alone. RGB is ignored;
shape and tint alpha, sampled texture alpha, SDF glyphs, outlines, shadows,
glows, contours, and warp all contribute. The extension writes that coverage
unsquared into a linear red-channel texture. An empty scene binds no coverage
texture and clips all content in its scope. A previously warmed cache may retain
its unused target until a later resize or `NowSdf.Reset()`.

By default the coverage target uses one texel per transformed physical pixel.
`SetMaskResolutionScale(scale)` changes only the target rasterized by
`BeginMask()`: `0.5` uses approximately one quarter as many texels and capture
fragments, while values above `1` supersample. The scale is applied after the
active UI and transform scales; target dimensions are rounded up and clamped
to at least one texel and the device texture limit. The target remains mapped
over the full authored rect and is sampled bilinearly. Derivative AA and
feather are evaluated at the capture resolution, so a scale below `1` widens
their visible transition roughly in inverse proportion to the scale. Because
the texture stores final coverage rather than distance, low scales can also
remove thin strokes, holes, contours, and glyph details. `Draw()` is unaffected
because a direct scene has no intermediate texture to resize.

The coverage target is cached by the scene's `NowId`. Use a stable, unique id
for each mask in a repeated or reorderable collection. When scene operations,
effects, effective tint, local mask, source texture version, and physical size
are unchanged, `BeginMask()` reuses the already-rasterized coverage. Translation
and mirroring alone can reuse it. A shape/effect/tint/mask change,
`Texture2D.Apply()`, physical size change, or mask-resolution change that
produces different target dimensions rerasterizes; nonzero-speed warp,
`RenderTexture` fills, and synchronized custom materials rerasterize every
call. Switching a custom material template also invalidates the cached
coverage. Keep the resolution scale stable for a stable id to avoid target
resize churn.

A resolved id owns exactly one coverage target. Do not call `BeginMask()` for
two different captures with the same resolved id while batches consuming the
first capture are still queued: the later capture overwrites that target. Use
distinct ids for concurrently queued masks, including masks that select
different custom material templates.

The target persists until resize, `NowSdf.Release(id)`, or `NowSdf.Reset()`;
callers do not own it. Caches are not automatically evicted because retained
batches may still reference them. Never generate a new id every frame. Release
a departed dynamic item's explicit id under the same host/`IdScope`, after
rebuilding or discarding retained batches that sample it. A retained host must
still call `MarkDirty()` before changed mask code runs.
`Release(id)` and `Reset()` also invalidate existing builders for the released
cache. Their `Measure()`, `Draw()`, and `BeginMask()` consumers throw
`ObjectDisposedException`; obtain a fresh builder from `NowSdf.Scene(...)`.

Two SDF or other texture-backed masks may nest and intersect. Their limit is
independent of hard rectangles and eight analytic `NowMaskShape` masks. A third
texture-backed mask throws `InvalidOperationException` before doing GPU work.

Pointer input is conservatively clipped to the scene rect, not to sampled SDF
alpha. This avoids synchronous GPU readback and stays deterministic for
textures, glyphs, warp, and effects. Add an analytic interaction boundary when
controls require exact non-rectangular hit testing.

`BeginMask()` captures `Now.Transform`. The persistent target is pixel-snapped
at the active physical UI scale and accounts for nonuniform scale; signed
mirroring is preserved while sampling. Outer masks are not baked into it, so
nesting does not apply an outer soft edge twice.

## Effects

Effects are applied to the final composed scene field. They work on primitives,
graphs, morphs, and SDF text together:

```csharp
var blob = NowSdf.Graph()
    .SetColor(new Color(0.08f, 0.78f, 0.68f, 1f))
    .Circle(new Vector2(74f, 76f), 42f)
    .SetColor(new Color(0.24f, 0.45f, 1f, 1f))
    .SmoothUnion(18f)
    .Circle(new Vector2(116f, 66f), 36f)
    .SetColor(new Color(1f, 0.58f, 0.18f, 1f))
    .SmoothUnion(14f)
    .Capsule(new NowRect(82f, 88f, 86f, 28f));

NowSdf.Scene(new NowRect(20f, 20f, 220f, 170f))
    .SetShadow(new Vector2(8f, 12f), 18f, new Color(0f, 0f, 0f, 0.28f), 2f)
    .SetGlow(28f, new Color(0.08f, 0.72f, 1f, 0.32f), 1.4f)
    .SetOutline(4f, new Color(0.02f, 0.04f, 0.09f, 0.82f), 1f)
    .SetInnerShadow(new Vector2(-5f, -6f), 12f, new Color(0f, 0f, 0f, 0.22f))
    .SetEmboss(new Vector2(-0.6f, -0.8f), 0.32f, 9f)
    .SetContours(18f, 1.2f, new Color(1f, 1f, 1f, 0.16f), Time.time * 10f, bandCount: 2)
    .SetContourMask(new Vector2(116f, 76f), 72f, 18f)
    .SetWarp(2.5f, 52f, 0.18f)
    .Graph(blob)
    .Draw();
```

Available scene effects:

- `SetOutline(width, color, softness = 0)` draws an outer stroke.
- `SetShadow(offset, softness, color, spread = 0)` draws a soft drop shadow.
- `SetInnerShadow(offset, softness, color, spread = 0)` darkens inside edges.
- `SetGlow(radius, color, power = 1)` draws an outside halo.
- `SetEmboss(lightDirection, strength = 0.35, size = 6)` lights the edge band.
- `SetContours(spacing, width, color, offset = 0, bandCount = 0)` draws
  distance rings. `bandCount` limits the rings to the nearest edge bands;
  `0` keeps the old repeating contour field.
- `SetContourMask(center, radius, softness = 0)` reveals contours around a
  scene-local point, which works well for pointer-focused field inspection.
- `SetWarp(amplitude, scale, speed = 0, seed = 0)` bends the distance domain
  before the scene is evaluated.

Outlines, shadows, and glows can only render inside the scene quad and mask. If
an effect should extend beyond a shape, give the scene rect enough empty space
around the drawn primitives.

An SDF draw also consumes ambient hard, analytic, and texture-backed mask
scopes after its own scene and contour masks are evaluated. This lets the
composited result share clipping with ordinary NowUI content; see
[Masks](Masks.md).

Scene effects measure against a locally normalized field distance, so stroke,
shadow, emboss, and contour sizes stay close to scene-pixel units even through
smooth blends, morphs, and warped organic fields.

## Custom SDF Materials

`SetMaterial(...)` replaces the final SDF shader for one scene. Use it for a
project-specific fill, lighting model, contour treatment, or shadow strategy
while retaining the graph builder and one-quad submission path:

```csharp
NowSdf.Scene(rect, "lit-logo")
    .SetMaterial(litSdfMaterial)
    .SetColor(Color.white)
    .RoundedBox(new NowRect(12f, 12f, 132f, 72f), 18f)
    .Subtract()
    .Circle(new Vector2(78f, 48f), 20f)
    .Draw();
```

An SDF material is not an arbitrary rectangle material. Its shader must keep
the built-in scene arrays, packed graph ranges, vertex streams, UGUI
canvas-layout switch, ambient mask handling, and mask-output behavior. The
supported way to retain that plumbing while changing the pixels is the
versioned [`NowSdfShaderV1.cginc`](../Extensions/Sdf/NowSdfShaderV1.cginc)
implementation. Start from a complete example below: its ShaderLab properties,
render state, stencil block, pragmas, and include are part of the contract.

The current material ABI is version 1. `NowSdf.MaterialAbiVersion` exposes the
numeric version, and `NowSdf.MaterialAbiProperty` exposes the required shader
property name, `_NowSdfAbiVersion`. A compatible shader declares it in its
`Properties` block:

```shaderlab
[HideInInspector] _NowSdfAbiVersion ("Now SDF ABI Version", Float) = 1
```

`SetMaterial` throws `ArgumentException` when this property is absent or does
not equal the current ABI version. Declaring it is a compatibility assertion;
the shader still has to include the matching implementation and keep its
required ShaderLab declarations.

Define `NOW_SDF_CUSTOM_FINAL_SHADE` to the name of an HLSL function before the
include. The include declares the function and calls it later, so define the
body after the include. This is the smallest useful callback block to copy into
one of the complete example shaders (also add `_StripeColor` to `Properties`):

```hlsl
float4 _StripeColor;

#define NOW_SDF_CUSTOM_FINAL_SHADE ProjectStripeShadeV1
#include "Packages/com.blenminer.nowui/Extensions/Sdf/NowSdfShaderV1.cginc"

float4 ProjectStripeShadeV1(
    float4 stockColor,
    float4 fill,
    float4 tint,
    float2 quadUv,
    float2 scenePosition,
    float2 sourceScenePosition,
    float2 sceneSize,
    float signedDistance,
    float coverage,
    float pixelWidth,
    float edge)
{
    float stripe = step(0.5, frac(sourceScenePosition.x / 12.0));
    float4 overlay = _StripeColor;
    overlay.a *= fill.a * coverage * stripe;
    return NowSdfAlphaOverV1(stockColor, overlay);
}
```

That include path is for a normal UPM installation. If the package was copied
or checked out at `Assets/NowUI`, use
`Assets/NowUI/Extensions/Sdf/NowSdfShaderV1.cginc`. Shaders living beside the
packaged examples can use their relative `../NowSdfShaderV1.cginc` path. Do not
mix an ABI-v1 property with an implementation from another installed package
version.

The callback receives:

| Input | Meaning |
| --- | --- |
| `stockColor` | Straight-alpha result after the stock drop shadow, glow, outline, fill/emboss, inner shadow, and contours have been composed. Return it unchanged to keep stock rendering. |
| `fill` | Evaluated scene fill before coverage and emboss; it already includes shape color, scene tint, and the scene's sampled `_MainTex` where applicable. |
| `tint` | Effective per-draw scene tint. |
| `quadUv` | Raw 0..1 scene-quad UV. |
| `scenePosition` | Warped scene-local position used by the stock distance evaluation. |
| `sourceScenePosition` | Unwarped, top-left-origin/y-down scene-local position. |
| `sceneSize` | Scene-quad size in local scene units. |
| `signedDistance` | Final composed distance at `scenePosition`; negative is inside. |
| `coverage` | Stock feathered shape coverage before any visual effects. |
| `pixelWidth` | Screen derivative width of `signedDistance`. |
| `edge` | Stock AA/feather half-width derived from `pixelWidth`. |

The hook runs after stock effects and contours but before Unity UI clipping,
ambient NowUI masks, `_SdfMaskOutput`, and alpha clipping. Consequently its
returned alpha contributes to `BeginMask()`, while later masks can still clip
it. Output must remain straight alpha because the pass uses
`Blend SrcAlpha OneMinusSrcAlpha`. `NowSdfAlphaOverV1(base, top)` composes two
straight-alpha colors without changing that convention.

ABI v1 also exposes
`NowSdfEvaluateDistanceV1(sourceScenePosition)`. It applies the configured warp
and evaluates the complete scene distance at another unwarped point, which is
useful for a displaced shadow. It repeats the scene-distance work, so prefer
the supplied `signedDistance` when one sample is sufficient.

Three complete shaders demonstrate different tradeoffs:

- [Aurora](../Extensions/Sdf/Examples/NowSdfAurora.shader) uses scene position
  and `_Time` for animated color bands plus a custom outside halo.
- [Topographic](../Extensions/Sdf/Examples/NowSdfTopographic.shader) draws
  arithmetic distance contours on both sides of the boundary without an extra
  scene evaluation.
- [Paper Cutout](../Extensions/Sdf/Examples/NowSdfPaperCutout.shader) derives a
  bevel normal and performs one extra displaced distance evaluation for its
  shadow.

In a source checkout, the
[repository gallery helper](https://github.com/BlenMiner/NowUI/blob/main/Assets/NowUI/Example/NowSdfShaderExamples.cs)
loads the corresponding materials and draws the same graph with all three.
Open `Assets/Scenes/DocsScene.unity`, enter Play mode, then select
**Extensions > SDF demo** in the docs browser. The repository-only example and
test harness are not included in the packed UPM package. From the repository
root, a repeatable off-screen preview can also be generated with:

```powershell
pwsh Tools/NowUI-Harness.ps1 -Mode Visual
```

The local, git-ignored gallery image is written to
`artifacts/local/visual/sdf-custom-shaders.png`.

### Custom-shader costs and boundaries

The builder still submits one quad, but the callback runs for fragments across
that quad, including its transparent padding. A larger scene rect, more custom
texture samples, loops, and extra calls to `NowSdfEvaluateDistanceV1` therefore
increase GPU work. Stock effects are evaluated before the callback; leave
effects disabled when the custom shader replaces them. Different material
templates also split draw batches. Reuse a small, stable material set and keep
scene rects only as large as their shapes plus the padding needed by outside
halos, contours, or shadows.

The hook changes shading, not geometry. It cannot draw outside the scene quad
or the builder's explicit mask. `_MainTex` is reserved for the scene's source
texture or SDF glyph atlas and is overwritten by NowUI; declare a separate
sampler such as `_ProjectNoiseTex` for custom textures.

Custom distance and shading functions are ordinary HLSL compiled into the
project shader. There is no C# per-pixel delegate, function registry, runtime
shader-source injection, or custom graph-node opcode in ABI v1. New reusable
primitive kinds still require a future node contract.

The passed material is a caller-owned template. A resolved scene cache lazily
creates its own direct-draw clone and, when needed, a separate mask clone for
each distinct template it uses. Clones remain alive until `NowSdf.Release(id)`
or `NowSdf.Reset()` so queued direct SDF batches survive a later template
switch; those calls destroy the clones but never the templates. Mask capture
still follows the one-coverage-target-per-id rule above. Keep templates alive
while scenes use them, reuse a bounded template set, and do not construct a new
material every frame for a stable scene id. The one-argument
`SetMaterial(material)` overload treats its properties as immutable. Do not
mutate that template after first use: direct and mask clones are created lazily,
and each captures the template when its rendering path is first used. Later
changes are not synchronized into an existing clone. This keeps unchanged
material and mask uploads reusable.

Use `SetMaterial(material, syncPerFrame: true)` when project-defined material
properties change. Also use it when a custom shader clock changes output alpha
captured by `BeginMask()`; this forces that cached coverage to rerasterize.
It recopies template properties before each draw, then reuploads the SDF ABI
data that the copy replaced. Direct `Draw()` has no intermediate mask texture,
so `_Time` animation does not itself require a property copy, but the host must
still repaint. Any retained host must be dirtied before changed builder or
template state can run again. Synchronization makes custom masks rerasterize on
every `BeginMask()` because arbitrary shader properties have no reliable change
version.

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

Each distinct graph is uploaded as one contiguous shape range, and repeated
references reuse that range. The layer metadata packs the range's start and
count into components that were already present, so this optimization adds no
uniform array. On the GPU, a layer now loops only over its referenced graph's
shapes instead of scanning every shape in the scene and skipping unrelated
graph ids. Morphs evaluate their source and target ranges; enabled drop and
inner shadows still perform their additional scene-distance evaluations. GPU
cost therefore still grows with covered pixels, referenced shapes, morphs,
and effects, but multi-graph scenes avoid the former all-shapes-per-graph scan.

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
