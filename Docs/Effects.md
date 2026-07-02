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

## Time

`NowEffectContext.time` is caller-driven, like every clock in NowUI: it defaults
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
long-lived cached entries. Prefer stable non-zero integer ids for data-backed
effects; string ids remain available for named one-offs.

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
    .SetTexture(snapshot.texture)
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
