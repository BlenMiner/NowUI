# Transforms, Rotation, And Opacity

Four stack-backed scopes change how everything drawn inside them lands on
screen. They compose with each other and with every stock builder, so a group
of shapes, text, images and SDF scenes can be moved, scaled, turned and faded
as a unit.

| Scope | Changes | Hit testing |
| --- | --- | --- |
| `Now.Transform(scale, origin)` / `Now.TransformAround(scale, pivot)` | Scale and translation | Follows the transform |
| `Now.Rotate(degrees, pivot)` | Rotation around a pivot | Unchanged (visual only) |
| `Now.Opacity(alpha)` | Alpha of every draw | Unchanged |
| `Now.Tint(color)` | RGBA of every draw | Unchanged |

Each call returns a scope; place it in `using`. Scopes must end in reverse
order: disposing an outer scope while an inner one is still open throws
`InvalidOperationException`. A copied or already-disposed handle is harmless.
None of them allocate after warmup.

## Scale And Translate

`Now.Transform` scales and pans drawing and input together, which suits zoomable
canvases and hosts that author at a design resolution:

```csharp
float scale = Mathf.Min(view.width / 960f, view.height / 540f);

using (Now.Transform(scale, view.position))
    DrawDesign(new NowRect(0f, 0f, 960f, 540f));
```

`Now.TransformAround(scale, pivot)` keeps `pivot` fixed, which is the usual way
to pulse, squash or stretch an element in place:

```csharp
float pulse = 1f + 0.06f * Mathf.Sin(time * 6f);

using (Now.TransformAround(new Vector2(pulse, 2f - pulse), button.center))
    DrawButton(button);
```

Nested transforms compose. Font sizes, outline widths, radii and blur scale with
the transform; masks and controls hit-test in transformed space.

## Rotate

`Now.Rotate(degrees, pivot)` turns everything drawn inside it around `pivot`.
Positive angles turn clockwise on screen, the same convention as SDF
`RotateNext`. The pivot is authored in the current coordinate space, so the
active `Now.Transform` applies to it.

```csharp
using (Now.Rotate(Mathf.Sin(time * 3f) * 8f, card.center))
{
    Now.Rectangle(card).SetColor(cardColor).SetRadius(16f).Draw();
    Now.Text(card.Inset(16f)).SetFontSize(22f).SetColor(Color.white).Draw("Rotated card");
}
```

Every stock draw turns rigidly: rectangles keep their radius, outline and blur,
text keeps its glyph shapes, gradients rotate with their shape, lines and arrow
heads stay attached, SDF scenes keep their field and effects, and glass samples
the backdrop that is actually beneath its rotated footprint. Rotations nest and
compose with transforms in either order.

The rotation is applied to the geometry the scope emitted when it ends, so it
is cheap: one pass over those vertices with no extra draw calls. That also
defines its limits:

- Hit testing and control interaction stay in the unrotated space. Rotate
  decorative or animated content, or hit-test a rotated control yourself by
  rotating the pointer into its local space.
- Masks opened inside the scope rotate with the content. Masks opened outside
  it clip the content's unrotated footprint, so keep rotated content well
  inside an outer mask or open the mask inside the rotation.
- Deferred overlays (popups, tooltips) queued inside the scope are not
  rotated.
- A capture that starts inside the scope (a `NowDrawList`, `NowRenderer` or
  other independent capture) is not rotated, just as it does not inherit the
  transform stack. Effects modifiers are the exception: their output is
  re-emitted into the current frame and turns with the scope.

For a perspective turn rather than a flat rotation, use
`NowDeformers.Perspective` with an [effects modifier](Effects.md#perspective).

## Opacity And Tint

`Now.Opacity(alpha)` multiplies the alpha of every draw inside it; `Now.Tint(color)`
multiplies RGBA. Nested scopes multiply, and `Now.currentTint` reports the
combined value, including a host tint such as a `NowGraphic` color.

```csharp
float fade = NowEase.OutCubic(NowEase.Progress(time, 0.2f, 0.6f));

using (Now.Opacity(fade))
    DrawPanel(panel);
```

Text, rectangles, images, lines, shapes, gradients, ripples, Lottie, SDF scenes
and glass all honor it. Glass fades as a whole pane, blurred backdrop included,
while `Now.Tint` still multiplies its tint color. Deferred overlays replay with
the opacity that was active when they were queued. Independent captures started
inside the scope begin untinted, like their transform and mask stacks, and a
render-to-texture effects modifier fades its captured content once rather than
again when drawing the flattened surface.

Opacity is applied to each draw on its own: overlapping shapes inside one scope
remain visible through each other, as they would with each color's alpha
lowered. To fade a group as one flattened layer, draw it inside
`NowEffects.Modifier(...).SetRenderToTexture()` within the opacity scope.

Supply tint colors as display/sRGB values, like every NowUI color. Alpha is
clamped to 0..1; NaN channels are ignored.
