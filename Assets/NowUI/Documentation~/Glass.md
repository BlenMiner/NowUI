# Glass

`Now.Glass(...)` draws a rounded backdrop pane. It copies what has already been
rendered into the current command-buffer or `RenderTexture` target, blurs that
copy, then samples it through the pane's rounded-rect mask.

```csharp
DrawWallpaper();

Now.Glass(new NowRect(24, 24, 320, 140))
    .SetBlurRadius(18f)
    .SetBlurQuality(NowGlassBlurQuality.Balanced)
    .SetTint(new Color(1f, 1f, 1f, 0.22f))
    .SetVibrancy(1.15f, 1f)
    .SetRadius(24f)
    .SetOutline(1f)
    .SetOutlineColor(new Color(1f, 1f, 1f, 0.35f))
    .Draw();

Now.Text(new NowRect(48, 48, 260, 32))
    .SetColor(Color.white)
    .SetFontSize(22f)
    .Draw("Glass panel");
```

Draw order matters: submit the content to blur first, draw the glass pane, then
draw foreground text, icons, and controls on top of it.

## Builder

- `SetBlurRadius(float)`: blur radius in UI units. The default is `16`.
- `SetBlurQuality(NowGlassBlurQuality)`: choose `Auto`, `Fast`, `Balanced`,
  `High`, or `Ultra`. `Auto` uses the active host/default
  quality; the global default is `Balanced`.
- `SetTint(Color/Vector4)`: color composited over the blurred backdrop. The
  default is translucent white.
- `SetVibrancy(float saturation, float brightness)`: adjusts sampled backdrop
  color before tinting. The default is `1.15, 1`.
- `SetRadius(float/Vector4)`: rounded-rect radius, including per-corner radii.
- `SetMask(NowRect)`: clips the pane to a NowUI mask.
- `SetOutline(float)` and `SetOutlineColor(...)`: draws a border over the pane.

## Host Support

Every NowUI host renders `Now.Glass(...)`; the backdrop source depends on the
host:

| Host | Glass behavior |
| --- | --- |
| `NowRenderer.Render(RenderTexture, ...)` | True blur of earlier content in a flat 2D `RenderTexture`. |
| `NowRenderer.PopulateCommandBuffer(...)` | True blur of earlier content in a flat 2D target. |
| `NowRenderer.Draw(commandBuffer, drawList, target, descriptor)` | True blur of earlier content while preserving XR texture-array layout and eye slices. Use this overload for active single-pass-instanced or multiview targets. Arbitrary non-XR array-layer replication is not provided. |
| `NowRenderer.Draw(commandBuffer, drawList, target, width, height)` | True blur of earlier content in a flat 2D target. |
| `NowRenderer.Draw(commandBuffer, drawList)` | Replay-backed blur of earlier NowUI batches into a temporary camera-target backdrop. |
| IMGUI (`NowGUI`/`NowGUILayout`) | True blur inside the cached IMGUI `RenderTexture`. |
| UI Toolkit (`NowVisualElement`) | True blur inside the cached UI Toolkit `RenderTexture`. |
| UGUI (`NowGraphic`) | Replay-backed blur of earlier NowUI batches, with optional external/camera source. |
| URP/HDRP pipeline overlays | True blur at the renderer feature/custom pass point. |
| `NowWorldGraphic` | Camera/world capture according to `glassBackdropMode`, with automatic foreground protection for blurred backdrops; explicit tint-only host mode available. |
| Built-in `Now.StartUI()` screen path | Replay-backed blur of earlier NowUI batches using temporary render textures. |

## XR Stereo Support

`NowWorldGraphic` camera/world glass and the URP/HDRP pipeline integrations
preserve the XR camera target shape through capture, blur, and material sampling.
Single-pass instanced and multiview targets keep and independently process each
texture-array eye slice; MultiPass independently processes each flat per-eye
target. When the XR camera color target is multisampled, NowUI explicitly
resolves `Texture2DMS` or `Texture2DMSArray` samples before the ordinary blur
passes, avoiding multisampled-texture/sampler mismatches. The resolve shader
queries the bound resource for its actual dimensions and sample count instead
of dividing by the XR provider descriptor's sample count.

In the Built-in pipeline, the XR provider target and the live logical
`CameraTarget` are not always the same attachment. MultiPass can draw through a
flat bound-MS intermediate before resolving into a single-sampled provider
target, so NowUI combines the provider's dimensions with the camera's MSAA
state and rebuilds the capture commands for each eye. Single-pass-instanced
targets retain the provider's texture-array sampling layout; they only use the
MS-array resolve when that descriptor explicitly exposes a bound-MS array.

Built-in XR may also expose `_CameraDepthTexture` as an unresolved MSAA surface
without identifying a safe sampler view. In that case world-panel glass uses
the camera depth buffer's fixed-function `ZTest` for foreground protection and
compiles the depth texture sampler out of the material variant. This keeps
foreground geometry sharp without binding an incompatible depth sampler. The
fallback applies to the panel glass fill; overlay glass remains always visible.

Custom command-buffer hosts must call the `NowRenderer.Draw` overload that takes
a `RenderTextureDescriptor` so NowUI knows the target dimension, volume depth,
VR usage, and source MSAA sample count. The legacy width/height overload
intentionally describes a flat 2D target.

Replay-backed UGUI, IMGUI, UI Toolkit, and built-in `Now.StartUI()` glass remain
2D host-local effects. They continue to work for flat host textures, but do not
turn a shared replay texture into an independently captured per-eye camera
backdrop. Use world glass or a descriptor-aware pipeline integration when the
backdrop must come from the XR camera.

`NowGraphic.uguiBackdropSourceTexture` must be a flat 2D texture; the replay
path does not consume an XR camera `Texture2DArray`.

## Quality And Diagnostics

`NowGlassSettings.defaultBlurQuality` controls the default quality for panes
that use `Auto`. Hosts that own a retained renderer also expose a
`glassBlurQuality` override, and individual panes can override both with
`SetBlurQuality(...)`.

`Fast` lowers pass count and downsamples earlier, `Balanced` is the default,
and `High`/`Ultra` keep more blur work at full resolution. Quality levels do
not disable blur; if a host cannot copy the real target, it should replay and
blur earlier NowUI batches where possible.

When `NowGlassSettings.diagnosticsEnabled` is true,
`NowGlassSettings.lastFrameDiagnostics` reports pane count, copied pixels,
blurred pixels, pass count, resolved quality, capture rects, and fallback
reasons. Per-pane entries are read without allocation through
`NowGlassSettings.TryGetLastFrameDiagnostic(index, out entry)` or copied into a
caller-owned array with `CopyLastFrameDiagnosticsTo(...)`. Call
`NowGlassSettings.ReserveDiagnostics(maxPanesPerFrame)` during initialization if
you need every pane entry retained; extra entries are counted in
`droppedEntryCount` instead of growing storage during a frame. The docs scene
uses this for the **Debug RTs** panel.

UGUI uses an expensive replay-backed path automatically when a `NowGraphic`
contains a `Now.Glass(...)` batch. `NowGraphic` keeps its normal CanvasRenderer
mesh, replays the NowUI batches before each glass batch into an offscreen
texture, blurs that texture, and binds it to the UGUI glass material. Content
drawn after the glass remains sharp UGUI geometry. If a UGUI graphic does not
contain glass, this replay path is skipped. Replay textures are cropped to the
glass pane's expanded bounds when possible instead of always using the full
graphic size.

The UGUI replay path blurs NowUI content from the same `NowGraphic`. To include
camera/world content behind the canvas, assign `NowGraphic.uguiBackdropSourceTexture`
to a captured camera color texture. If the canvas camera renders into
`Camera.targetTexture`, NowUI uses that texture automatically as the base layer.
Screen Space Overlay has no readable camera color target, so it can only blur
the replayed NowUI prefix unless you provide a source texture.

World-space mesh rendering uses `NowWorldGraphic.glassBackdropMode`:

- `TintOnly`: rounded tint/outline fallback.
- `Camera`: sample copied camera color.
- `CameraAndWorld`: sample copied camera color plus eligible
  `NowWorldGraphic` meshes behind each glass requester.

Blur is controlled by the blur radius requested by `Now.Glass(...)` batches, not
by the host backdrop mode. `CameraAndWorld` is the default and only requests the
camera capture when a world graphic contains a glass batch.

Built-in render pipeline cameras get an automatic `BeforeForwardAlpha` command
buffer. URP requires `NowUniversalRendererFeature`; HDRP requires
`NowHighDefinitionCustomPass` at an injection point before the transparent world
UI draws. World-contributor modes draw other `NowWorldGraphic` meshes behind
each glass requester into that requester's backdrop before sampling/blur. They
skip glass submeshes from those contributors to avoid recursive sampling, and
they do not include arbitrary transparent scene objects rendered after the
capture.

Blurred world glass automatically requests a camera depth texture and samples a
sharp backdrop where opaque scene geometry is closer than the pane, so
foreground scene objects do not smear into the blur. This is not
reconstruction: the system cannot recover background color hidden behind an
opaque foreground object, and transparent objects that do not write depth are
not detected.

The built-in `Now.StartUI()` GL path preserves frame draw order and replays
earlier NowUI batches into temporary textures for glass panes. It still cannot
blur arbitrary scene or native UI content already on the screen.

## Example

The packaged [docs browser source](../Example/NowDocsExample.cs) contains a
UGUI replay-backed glass example. It blurs NowUI content drawn before the pane
while keeping later labels sharp and exposes the diagnostic render targets,
quality, fallback reason, and per-frame pixel/pass counts.
