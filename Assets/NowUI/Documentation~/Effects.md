# Effects

`NowEffects.Modifier(...)` applies visual effects to ordinary NowUI draw calls.
The modifier is a scope: draw normally inside the `using` block, and the scope
captures the submitted geometry before appending a deformed version to the
current frame.

## Mesh Modifier

Mesh capture is the default backend. It keeps text, rectangles, lines, glyph
quads, and shape geometry as real vertices, then passes each vertex through the
deformer.

```csharp
NowRect target = new NowRect(520, 320, 72, 40);
float progress = Mathf.PingPong(Time.time * 0.5f, 1f);

using (NowEffects.Modifier(NowDeformers.Genie(target, progress))
    .SetSubdivision(4)
    .Begin())
{
    DrawWindow();
}
```

Use mesh modifiers for crisp vector/text effects such as wobble, bend, pull,
and highlight deformation. Built-in deformers are value types, and custom
deformers only need to implement `INowVertexDeformer`.

```csharp
readonly struct BulgeDeformer : INowVertexDeformer
{
    public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
    {
        Vector2 fromCenter = vertex.normalized - new Vector2(0.5f, 0.5f);
        float falloff = 1f - Mathf.Clamp01(fromCenter.magnitude * 2f);
        return vertex.position + fromCenter * falloff * 24f;
    }
}
```

## Coordinate Space

`NowEffectVertex.position` is in top-left-origin UI units with y pointing
down. It already includes the active `Now.Transform` (and any `Now.Rotate`
scope closed inside the modifier), but not the host's UI scale, so it is not a
physical-pixel coordinate. Return a position in the same space.

`NowEffectContext.sourceRect` is in that space too. By default it is the bounds
of every captured vertex, which include each shape's anti-aliasing padding.
Call `SetSourceRect(rect)` to pin it to a known region, such as a card's
authored rect, so a pivot or `vertex.normalized` does not drift with padding,
shadows, or animated content. The rect is authored in the current coordinate
space and mapped through the transform that is active when the modifier
begins.

```csharp
using (NowEffects.Modifier(new TiltDeformer(angle))
    .SetSourceRect(card)
    .SetSubdivision(6)
    .Begin())
{
    DrawCard(card);
}
```

A flat rotation does not need a deformer: `Now.Rotate(degrees, pivot)` turns
any drawing without capturing it. See [Transforms](Transforms.md#rotate).

## Perspective

`NowDeformers.Perspective(yaw, pitch)` turns captured content in 3D around the
center of the source rect and projects it back. Positive yaw moves the right
edge away from the viewer; positive pitch moves the top edge away. An overload
takes the camera distance (in multiples of the source rect's larger side,
default 3) and a normalized pivot.

```csharp
float flip = NowEase.OutBack(NowEase.Progress(time, 0f, 0.6f));

using (NowEffects.Modifier(NowDeformers.Perspective((1f - flip) * -75f, 0f))
    .SetSourceRect(card)
    .SetSubdivision(6)
    .Begin())
{
    DrawCard(card);
}
```

Subdivide the modifier for perspective: an undivided quad is only two
triangles, so a large rectangle, gradient, or SDF scene would fold along its
diagonal. Rectangles, textures, gradients, ripples, and SDF scenes subdivide;
text subdivides with `SetSubdivideText()`.

## Time

`NowEffectContext.time` is caller-driven, like NowUI's other animation clocks
(an SDF warp `speed` reads the shader clock unless `NowSdfBuilder.SetTime`
supplies one; see [SDF](SDF.md#effects)): it defaults
to `0` and only changes when the modifier is given a time explicitly with
`SetTime(...)`. Built-in deformers such as `NowDeformers.Wave(time, ...)` take
their time as a constructor argument instead; `SetTime` exists for custom
deformers that read `context.time`.

```csharp
using (NowEffects.Modifier(new PulseDeformer())
    .SetTime(Time.time)
    .Begin())
{
    DrawBadge();
}
```

## Texture Modifier

Call `SetRenderToTexture()` when the whole scoped region should behave as one
flattened surface. The same modifier/deformer path is used after the content is
captured into a cached `RenderTexture`.

```csharp
using (NowEffects.Modifier(NowDeformers.Genie(target, progress))
    .SetRenderToTexture()
    .SetSubdivision(6)
    .Begin())
{
    DrawWindow();
}
```

Texture mode is useful for expensive content, pixel/material-style effects, or
cases where internal text and geometry should flatten before the deformation.
It does not expose texture ownership; the texture is an internal backend detail
cached by effect id and size.

The capture texture is sized in physical pixels from the active NowUI scale:
`Now.StartUI(uiScale)`, UGUI `Canvas.scaleFactor`, and SRP
`NowPipelineGraphic.BuildDrawList(..., uiScale)` all feed the same conversion.
The texture bounds are snapped outward to the pixel grid and sampled with clamp
wrapping, so switching a panel from normal drawing to texture-backed drawing has
enough source pixels to line up with the original content.

## Subdivision

Subdivision is explicit. No automatic backend or tessellation switching happens
in v1.

```csharp
// A fixed 4 x 4 grid for each quad-like rectangle or texture surface.
.SetSubdivision(4)

// Keep original vertices.
.SetSubdivision(NowSubdivision.None)

// Adapt by size, useful for large rectangles or texture surfaces.
.SetSubdivision(NowSubdivision.MaxCellSize(18f))
```

Text glyph quads are not subdivided by default because large text blocks can
produce thousands of tiny quads. They still deform as glyph quads. Opt in only
when a deformer needs to bend inside each glyph.

```csharp
using (NowEffects.Modifier(NowDeformers.Wave(Time.time, 3f, 36f))
    .SetSubdivision(3)
    .SetSubdivideText()
    .Begin())
{
    DrawLargeHeading();
}
```

Texture mode subdivides the flattened textured surface, which is usually much
cheaper than subdividing every draw command in a text-heavy scope.

## Identity

Modifier ids are automatic from the caller file and line, matching the rest of
NowUI's call-site id pattern. Add `SetId(NowId)` for loops, reordered data, or
long-lived cached entries. Prefer stable model keys for data-backed effects;
string and integer ids are both valid. If a custom composite
already owns a `NowResolvedId`, pass it to the resolved `SetId` overload rather
than converting or re-resolving it. See [Identity](Identity.md).

```csharp
foreach (var card in cards)
{
    using (NowEffects.Modifier(NowDeformers.Wave(Time.time, 4f, 48f))
        .SetId(card.id)
        .SetSubdivision(3)
        .Begin())
    {
        DrawCard(card);
    }
}
```

## Snapshot

Most effects should use `SetRenderToTexture()` on `Modifier`. Use
`NowEffects.Snapshot(...)` only when caller code needs direct access to the
captured texture handle.

```csharp
var snapshot = NowEffects.Snapshot(sourceRect).Begin();

using (snapshot)
{
    DrawPreview();
}

Now.Rectangle(previewRect)
    .SetTexture(snapshot.texture, premultipliedAlpha: true)
    .Draw();
```

## Input And Performance

Captured scopes are visual-only. While the modifier captures draw commands,
NowUI input runs in passive mode, so clicks, focus, scroll, and text input are
not remapped through the deformation.

The first use, new ids, buffer growth, and `RenderTexture` size changes can
allocate. Warmed steady state is tested to be managed-allocation-free for mesh
modifiers and for texture modifiers whose id, source bounds, and render target
size stay stable.

Subdivision multiplies vertices quickly: fixed `4` creates 25 vertices and 96
indices per subdivided quad. Prefer `NowSubdivision.MaxCellSize(...)` for large
or dynamic surfaces, keep text subdivision off unless necessary, and use
`SetRenderToTexture()` when a text-heavy panel should deform as one surface.
