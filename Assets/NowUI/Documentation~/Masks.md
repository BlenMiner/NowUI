# Masks

NowUI has four public entry points across three complementary clipping
backends:

- `Now.Mask(NowRect)` is the existing exact, axis-aligned rectangular clip.
  Keep using it for scroll viewports and other hard rectangular boundaries.
- `Now.Mask(NowMaskShape)` clips to an analytic rectangle, rounded rectangle,
  circle, ellipse, or capsule. Analytic masks anti-alias their boundary and can
  widen that transition with a screen-pixel feather.
- `Now.Mask(NowMaskTexture)` maps a caller-owned coverage texture to an
  authored UI rect. Use it for an existing alpha or red-channel mask.
- `NowSdf.Scene(rect, id)...BeginMask()` from the optional `NowUI.Sdf`
  extension is a texture-mask convenience that rasterizes a composed SDF scene
  into cached coverage. Use it for unions, subtractions, glyphs, warped fields,
  and effect-shaped boundaries that cannot be represented by one analytic
  primitive.

All four entry points create ambient scopes. Every supported built-in draw
submitted inside the scope is clipped, including rectangles and images,
gradients, lines and core shapes, text, Lottie, glass, ripples, color pickers,
and model preview surfaces. Draws from the optional `NowUI.Sdf` extension
participate as well.

## Basic Use

Create a shape, optionally widen its feather, then keep the returned mask scope
in a `using` statement.

```csharp
var portraitMask = NowMaskShape.Circle(card.center, 48f)
    .SetFeather(1.5f);

using (Now.Mask(portraitMask))
{
    Now.Rectangle(card)
        .SetTexture(portrait)
        .Draw();

    Now.Text(card.Inset(12f))
        .SetFontSize(14f)
        .SetColor(Color.white)
        .Draw("Online");
}
```

The mask affects draws only while the scope is alive. Calling `Now.Mask(...)`
without disposing its result leaks the pushed state through the remainder of
the frame and is reported by analyzer diagnostic `NOWUI002`.

## Texture Masks

`NowMaskTexture` maps the full texture to `bounds`; samples outside that rect
have zero coverage. The constructor and `Alpha(...)` factory sample alpha by
default. Use `Red(...)` for a linear, single-channel coverage texture:

```csharp
var alphaMask = NowMaskTexture.Alpha(paintedMask, bounds);

using (Now.Mask(alphaMask))
{
    DrawMaskedContent();
}
```

Call `SetInverted()` to invert coverage inside the authored rect. The outside
remains uncovered, and a null or destroyed texture clips everything even when
inverted. NowUI does not take ownership of textures passed to
`NowMaskTexture`; the caller must keep them alive for every retained draw list
that samples them and release them when no longer needed.

Texture masks use conservative rectangle input and share the two-active-mask
limit described below. They capture `Now.Transform` when pushed, including
signed mirroring and nonuniform scale.

## SDF Masks

SDF shape coordinates are local to the scene rect. Build the scene, then keep
the scope returned by `BeginMask()` alive around its child draws:

```csharp
using NowUI.Sdf;

var mask = NowSdf.Scene(card, "profile-cutout-mask")
    .SetFeather(1f)
    .Circle(new Vector2(52f, 52f), 46f)
    .SmoothUnion(12f)
    .RoundedBox(new NowRect(42f, 18f, 150f, 68f), 22f)
    .Subtract()
    .Circle(new Vector2(168f, 52f), 14f);

using (mask.BeginMask())
{
    DrawProfileCard(card);
}
```

Give repeated or reordered masks a stable, unique `NowId`; each id owns its
cached coverage target. `BeginMask()` requires an explicit scene rect. The
`BeginMask(rect)` overload is available when a rect was not supplied to
`Scene(...)`. During a `NowLayout` measure pass `BeginMask` returns a default
scope, does no GPU work, and installs no ambient mask, so reserve the rect first
when the same drawing method also performs measurement.

The mask is the scene's final composited alpha. RGB is ignored; per-shape and
tint alpha, textures, SDF glyphs, outlines, shadows, glows, contours, and warp
all participate. An empty scene binds no coverage texture and clips all
children. A previously warmed cache may retain its unused target until a later
resize, `NowSdf.Release(id)`, or `NowSdf.Reset()`.

An unchanged static scene reuses the coverage already in that target. The
scene operations and effects, effective tint, local scene and mask geometry,
source texture version, and transformed physical target size must all match.
Translation and mirroring alone can reuse coverage when the local content and
physical size stay the same. Shape/effect/tint/mask changes and
`Texture2D.Apply()` rerasterize it. A nonzero-speed warp or a
`RenderTexture`-backed fill remains live and rerasterizes on every
`BeginMask()` call.

SDF caches are not evicted automatically because retained batches may still
sample their targets. Do not generate a fresh id every frame. When an item with
a dynamic explicit id leaves, call `NowSdf.Release(id)` under the same host and
`NowControls.IdScope`; rebuild or discard any retained batch that sampled it
first. `NowSdf.Reset()` remains the whole-extension cleanup operation.
Builders backed by a released cache are invalid and throw
`ObjectDisposedException` from `Measure()`, `Draw()`, or `BeginMask()`; create a
fresh scene builder before drawing that id again.

## Analytic Shapes

`NowMaskShape` is a value type. Its factories do not require a texture or a
caller-owned point buffer.

| Factory | Boundary |
| --- | --- |
| `Rectangle(rect)` | Axis-aligned analytic rectangle |
| `RoundedRect(rect, radius)` | One radius for all four corners |
| `RoundedRect(rect, corners)` | Named per-corner radii from `NowCornerRadius` |
| `Circle(center, radius)` | Circle around `center` |
| `Ellipse(rect)` | Ellipse fitted to `rect` |
| `Capsule(from, to, radius)` | Capsule around a line segment |
| `Capsule(rect)` | Capsule fitted to `rect` |

Use `shape.bounds` when surrounding layout or conservative culling needs the
axis-aligned extent of a shape. The original value is unchanged when
`SetFeather(...)` returns the configured copy.

Use `NowCornerRadius` when the corners differ so call sites remain readable:

```csharp
var corners = new NowCornerRadius(
    topLeft: 24f,
    topRight: 24f,
    bottomRight: 8f,
    bottomLeft: 8f);
var mask = NowMaskShape.RoundedRect(card, corners);
```

The lower-level `Vector4` overload, if needed for already-packed data, uses
`NowCornerRadius.packed` order: `(topRight, bottomRight, topLeft, bottomLeft)`.
Prefer `NowCornerRadius` in authored code.

## Soft Edges

`SetFeather(pixels)` controls additional edge softness in physical screen
pixels:

- `SetFeather(0)` keeps the default derivative-based anti-aliasing ramp,
  normally about one physical pixel.
- Positive values add approximately that many physical pixels of softness to
  the default ramp. For example, `SetFeather(1)` produces roughly a two-pixel
  transition in total.
- Negative and non-finite values fall back to zero.

Feather is coverage at the clipping boundary, not a blur of the child draw.
Content well inside the shape remains unchanged, content outside remains
transparent, and only the edge transitions between them. Because the value is
in physical pixels, `Now.StartUI(uiScale)`, pipeline UI scale, and UGUI canvas
scale do not make a high-density display look more aliased.

## Nesting And Input

Rectangular, analytic, and SDF mask scopes can nest. Nested masks intersect: a
fragment must be covered by every active mask. This makes a hard scroll
viewport around a soft portrait, pill, or composed field safe and predictable.

At most eight analytic masks may be active at once. Exceeding that limit throws
an `InvalidOperationException`; legacy `Now.Mask(NowRect)` scopes do not count
toward it. Prefer one mask that describes the final boundary when deeply nested
composites would otherwise approach the limit.

At most two texture-backed masks, including SDF masks, may be active at once.
This limit is independent of the eight analytic masks and hard rectangles.
Exceeding it throws `InvalidOperationException`. Prefer composing another SDF
operation when a boundary would otherwise need more texture masks.

Ambient masks also constrain NowUI pointer interaction. Analytic masks test the
geometric interior of the shape; feather changes rendered coverage only and
does not enlarge the interactive region. A point outside the shape cannot
hover or press a child merely because it lies in the visual feather.

Texture-backed masks use their authored bounds as a conservative input proxy.
A point outside those bounds is rejected, but a point inside remains
input-eligible even when the SDF field is transparent there. Exact GPU-alpha
hit testing would require a readback and would disagree with texture, glyph,
warp, and effect-based fields. When an exact non-rectangular interaction edge
matters, pair the visual SDF mask with an analytic interaction boundary.

An analytic mask captures the current `Now.Transform` when it is pushed. Author
the shape in the same local coordinates as its children and place both scopes
in the natural order:

```csharp
using (Now.Transform(zoom, pan))
using (Now.Mask(NowMaskShape.Circle(localCenter, localRadius)))
{
    DrawZoomedContent();
}
```

Analytic-mask sampling and hit testing preserve signed nonuniform scale,
including mirrored coordinates; the feather still measures physical screen
pixels.

An SDF mask likewise captures the active transform when `BeginMask()` runs.
Its linear red-channel coverage texture is pixel-snapped and sized from the
absolute transformed size at the active physical UI scale; signed mirroring is
preserved while sampling. Outer masks are intersected with child content rather
than baked into this texture, avoiding a duplicated soft edge.

Builder-level `SetMask(NowRect)` remains the explicit rectangular clip for an
individual draw. Use an ambient `Now.Mask(NowMaskShape)` scope when the clip is
non-rectangular or soft, or when several child draws should share it.

## Materials And Hosts

Packaged NowUI materials evaluate analytic and texture-backed masks in the
normal renderer and their UGUI variants. Unity `Mask` and `RectMask2D`
components remain separate UGUI clipping layers and intersect with the NowUI
mask.

Caller-provided custom materials are opt-in. The existing per-vertex mask rect
documented in [Custom Materials](CustomMaterials.md) remains the legacy
rectangular clip; reading that stream alone does not implement analytic or
texture-backed masks. Mirror the shared mask handling from the corresponding
packaged shader when a custom material must participate. The optional
`NowUI.Sdf` extension still has its own contour and scene-quad mask controls,
and its final output consumes every ambient NowUI mask kind.

UI Toolkit and IMGUI render through a cached `RenderTexture`, while Built-in,
URP, HDRP, RenderTexture, UGUI, and world-space hosts submit the same built-in
material families. The same mask scope can therefore stay in shared drawing
code across those hosts.

## Performance

Stable mask stacks are captured once when the stack changes and reused by all
compatible child draws. The remaining costs differ by backend:

| Path | Cheap pattern | Remaining limit |
| --- | --- | --- |
| Hard rectangle | Deep shared viewport/layout clipping | Nested rectangles collapse into one per-vertex clip; scope setup still scales with depth. |
| Analytic | One stable scope around many draws | Each active mask adds a fragment SDF/AA evaluation; at most eight. |
| Texture | One stable texture state around many draws | Each active mask adds a texture sample; at most two. |
| SDF | Stable id, static content, fixed physical size | One persistent target per id; dirty coverage must rasterize, then samples as a texture mask. |

Hard rectangles do not add fragment work as nesting grows and do not split an
otherwise compatible batch. Analytic and texture states also stay cheap on the
CPU while one scope is shared across many draws, but alternating different
states can create ordered batches. Each analytic mask still adds shader work
for covered fragments, and each texture-backed mask still adds a sample; CPU
benchmarks cannot make either cost disappear.

An SDF mask renders into a persistent, usually single-channel target owned by
its stable-id cache. First use and transformed pixel-size changes can allocate
or resize it; unchanged static coverage skips the render entirely. Continuously
changing the physical size is the expensive path, so prefer a fixed capture
rect and animate shapes inside it. Retained hosts must still call `MarkDirty()`
before changed mask code can run. Warm representative content before measuring,
and measure GPU time on target hardware for large/full-screen masks.
