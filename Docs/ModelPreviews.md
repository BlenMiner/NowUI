# Model Previews

`Now.Model(...)` renders reusable 3D content into a transparent
`RenderTexture`, then draws that result through the normal rectangle path.
The default path submits meshes directly with `Graphics.RenderMesh`: it does
not instantiate, move, relayer, or destroy the caller's source. Create the
heavyweight preview outside the UI callback and dispose it with its owner.

```csharp
[SerializeField] GameObject characterPrefab;

NowModelPreview preview;

void OnEnable()
{
    preview = new NowModelPreview(characterPrefab);
}

void OnDisable()
{
    preview?.Dispose();
    preview = null;
}

void DrawCharacter(NowRect rect)
{
    Now.Model(rect, preview)
        .SetRadius(16f)
        .SetOutline(1f, new Color(1f, 1f, 1f, 0.2f))
        .Draw();
}
```

A complete component lifecycle is available in
`Assets/NowUI/Example/NowModelPreviewExample.cs`. The example avoids creating a
rig until a source model is assigned.

For a live comparison, open **Rendering > Model previews demo** in
`NowDocsExample`. It shares one procedural preview across direct, masked/styled,
and texture-effect cards, with controls for update mode, pause, resolution
scale, rotation, manual refresh, wave amplitude, scene lighting, and
post-processing.

There are two explicit source modes:

- `new NowModelPreview(source)` is the default isolated mode. It caches
  `MeshRenderer` draws, chooses LOD 0, CPU-bakes each visible
  `SkinnedMeshRenderer` when the preview refreshes, and submits only those
  meshes to the preview camera. The source may be a prefab reference or a
  caller-owned object; NowUI never instantiates or mutates it.
- `NowModelPreview.FromSceneObject(sceneObject)` borrows an already loaded
  scene hierarchy and lets Unity render it normally. This keeps GPU skinning,
  Animator, particles, LOD selection, dressing, and renderer-specific behavior
  under caller control. NowUI owns only the preview rig and texture.

Use the isolated default for ordinary inventory icons and static portraits.
Use a scene object when the normal Renderer path or a fully dressed live
character matters more than strict scene isolation.

## Updating

`WhenDirty` is the default. Once visible, it renders after target resizing or
an explicit pixel change:

```csharp
preview.SetRotation(Quaternion.Euler(0f, angle, 0f)); // marks dirty
preview.MarkDirty();                                  // pose/material changed
preview.Reframe();                                    // bounds/fit changed
```

`MarkDirty()` and `SetRotation(...)` keep the camera stable. Rotation is an
isolated-mode presentation transform and uses a stored bounding sphere, so a
rotating item cannot grow out of its original frame. Scene-object previews
never rotate the caller's hierarchy; rotate that object yourself and call
`Reframe()`. `Reframe()` deliberately does the more expensive work: it
rediscovers the source hierarchy, refreshes cached mesh/material draws where
needed, and recomputes the fit from active renderer bounds.

Use `EveryFrame` when a caller-owned source changes continuously:

```csharp
preview.SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame);
```

An every-frame preview continues rendering after its first draw, including in
retained hosts whose geometry is not rebuilt each frame. Visibility is not
inferred from an individual draw because the same preview can be shared by
several hosts. Pause a hidden panel explicitly with
`SetRenderingEnabled(false)`, or dispose the preview with its owner. `Manual`
renders only after `RequestRender()` or `RenderNow()`.

The shared late-update driver sleeps when no preview has pending work. A clean
grid of `WhenDirty` or `Manual` previews therefore has no per-frame camera or
manager pass. If another preview is actively using `EveryFrame`, the shared
driver is awake and checks the registered previews.

NowUI never starts, stops, pauses, or reconfigures animation and particle
components on either source mode. `EveryFrame` controls preview refreshes, not
source simulation. Isolated mode CPU-bakes the current skinned pose on each
refresh; scene-object mode uses Unity's ordinary renderer and GPU skinning.
Pause or cull the caller-owned presentation object separately when it should
stop simulating.

Preview jobs normally run together in a late-update pass, before game cameras.
This avoids recursive camera renders from UGUI rebuilds, exact layout's measure
pass, and URP/HDRP render callbacks. `RenderNow()` is intended for `Update`,
initialization, or tooling code outside a camera callback; when a camera is
already rendering, or rendering is paused, it queues the work and returns
`false`. Before the first draw, provide a size explicitly:

```csharp
preview.RenderNow(pixelWidth: 256, pixelHeight: 256);
```

The parameterless overload needs a target prepared by an earlier draw or
`SetFixedResolution(...)`.

## Resolution And Framing

Automatic resolution follows the model's physical draw size, including
`Now.uiScale`, buckets its longest edge in 8-pixel increments, and caps it at
1024 pixels. Responsive resizing reconfigures the same `RenderTexture` object,
so retained batches and material caches remain stable. Changing the scale or
cap resizes a prepared target immediately; fluent setter order is irrelevant.

```csharp
preview
    .SetResolutionScale(0.75f)
    .SetMaxResolution(768);
```

Inventory thumbnails can request a fixed target to make memory and update cost
predictable within the configured and device limits:

```csharp
preview.SetFixedResolution(256, 256);
```

`SetMaxResolution(...)` and `SystemInfo.maxTextureSize` still cap a requested
fixed size proportionally when necessary.

Isolated-source bounds are normalized once. Scene objects stay at their caller-
owned world transform. `Reframe()` fits the camera to the current renderer
hierarchy in either mode. Framing does not chase animated bounds every frame,
avoiding camera pumping. Camera controls are explicit:

```csharp
preview
    .SetViewDirection(new Vector3(0.35f, 0.12f, 1f))
    .SetFieldOfView(28f)
    .SetFramingPadding(1.15f);
```

Use `SetOrthographic()` for icon-like presentation. The `camera` property is an
escape hatch for settings not covered by the fluent helpers.

## Masks, Materials, And Effects

The flattened result retains rectangle tint, padding, rounded corners, outline,
blur, explicit `SetMask`, and ambient `Now.Mask` behavior. It also participates
in mesh and texture-backed effect scopes:

```csharp
using (NowEffects.Modifier(NowDeformers.Wave(Time.time, 3f, 32f))
    .SetSubdivision(5)
    .Begin())
{
    Now.Model(rect, preview).SetRadius(18f).Draw();
}
```

Camera render targets have premultiplied color at transparent filtered edges.
NowUI marks model textures accordingly, preventing the dark silhouette fringe
caused by multiplying alpha twice. Custom rectangle shaders used with
`SetMaterial(...)` should accept `_NowPremultipliedTexture`, or otherwise treat
the preview texture as premultiplied input.

Opaque and alpha-clipped model materials produce the expected premultiplied
silhouette. A transparent model shader must also write compatible RGB and alpha
(typically separate color/alpha blend factors). Arbitrary additive shaders or
the common `Blend SrcAlpha OneMinusSrcAlpha` applied to both RGB and alpha can
write zero or squared alpha and cannot be corrected after flattening.

For another premultiplied render target, use the explicit texture overload:

```csharp
Now.Rectangle(rect).SetTexture(renderTarget, premultipliedAlpha: true).Draw();
```

Effects run after flattening. They can transform or shade the model's pixels,
but an effect that needs model-space vertices, normals, or scene depth requires
its own auxiliary render targets. Mesh effects keep the live preview texture.
Texture-backed effects and snapshots copy it; NowUI tracks that dependency and
automatically rebuilds the retained UGUI, UI Toolkit, or world host after a
deferred preview refresh. In an immediate or pipeline host, the normal next
frame redraw performs the copy.

## Source Ownership And Isolation

The default isolated path caches draw packets and submits them only to its
registered preview camera. It does not create a presentation GameObject, and a
game camera cannot observe those camera-targeted submissions. The camera and
key light live in NowUI's shared private Unity scene, which excludes loaded-
scene geometry and environment by default. SRPs isolate direct lights with a
dedicated rendering-layer bit. Built-in rendering caches directionals after
scene changes, then temporarily removes only the preview-layer bit during an
isolated refresh. A clean `WhenDirty` preview has no idle camera or scene-scan
cost. Opt into loaded-scene lighting when an isolated preview should
deliberately match the current level:

```csharp
preview.SetSceneLightingEnabled(true);
```

NowUI's private, shadowless key light still remains available. Configure it
with `SetLight(...)`, including an intensity of zero when scene lights should be
the only direct-light source. Scene-load changes are tracked automatically. If
a game creates or destroys a Built-in directional while previews are already
alive, refresh the allocation-bearing cache explicitly rather than paying for
discovery on every render:

```csharp
preview.RefreshSceneLighting();
```

URP/HDRP volume post-processing is a separate opt-in. It is disabled by
default, including a zero volume mask, so a menu preview does not unexpectedly
inherit global volumes:

```csharp
preview.SetPostProcessingEnabled(true, 1 << presentationVolumeLayer);
```

The setting configures URP/HDRP additional camera data only when that pipeline
is present and only when the option changes or the active pipeline changes.
HDRP disables post-processing for `CameraType.Preview`, so an HDRP post opt-in
uses `CameraType.Game`; the camera remains identifiable with
`NowModelPreview.IsPreviewCamera(camera)`. Built-in or third-party image-effect
components can be configured directly on `preview.camera`. Renderer features
and custom passes are not the same as volume post-processing; project-specific
passes should use `IsPreviewCamera` when they must continue excluding these
cameras after HDRP post is enabled.

Raw submissions use layer 31 by default. Pass an explicit layer when the
project reserves it:

```csharp
preview = new NowModelPreview(characterPrefab, previewLayer: 27);
```

`MeshRenderer` and `SkinnedMeshRenderer` are supported by isolated submission.
LOD groups contribute their highest-detail level. Renderer-specific systems
such as particles are skipped and reported by `unsupportedRendererCount`.
Materials, property blocks, rigid child transforms, and dressing are cached on
construction and `Reframe()`; the skinned pose itself is baked on every actual
refresh. Normalized object matrices and world bounds are cached until rotation
or hierarchy framing changes, so a static refresh only walks the compact draw
packets. This keeps dirty static inventory previews cheap and predictable.

For the normal Unity Renderer path, borrow a loaded scene object:

```csharp
// Put presentationCharacter on layers excluded by gameplay cameras.
preview = NowModelPreview.FromSceneObject(
    presentationCharacter,
    presentationCameraMask);
```

The one-argument overload derives an initial mask from all renderer layers in
the hierarchy. The explicit overload is safer for production. The preview
camera can render every object on that mask, not only the referenced hierarchy,
so use dedicated presentation layers and placement. NowUI never changes the
object's layer, `forceRenderingOff`, transform, Animator, particles, materials,
or lifetime. The caller is responsible for excluding those layers from game
cameras and for hiding or pausing the presentation object when appropriate.
Because the object remains in a loaded scene, scene lighting and environment
are inherent to this mode; choosing `FromSceneObject` is the opt-in.

Preview cameras use `CameraType.Preview`, are always excluded from
`NowPipelineGraphic`, and can be identified by project features with
`NowModelPreview.IsPreviewCamera(camera)`. Post-processing remains a separate
opt-in in both source modes.

The isolated key light and raw submissions additionally use a separate
rendering-layer bit where the active pipeline supports light layers. A borrowed
scene object keeps its existing renderer/light-layer configuration. In HDRP,
the normalized key-light intensity is mapped to directional lux and additional
light data is initialized before the first request.

Each refresh is a real camera render. Prefer `WhenDirty` for grids of inventory
items, cap their resolution, share static lighting/material assets, and reserve
`EveryFrame` for the small number of previews that truly animate.

Each live preview retains one hidden Camera GameObject. The shadowless key
light, SRP request, directional-light scratch storage, and raw hierarchy-
discovery buffers are shared because preview renders and source mutations are
serialized on Unity's main thread. Disposing the final preview releases that
shared private scene and its light.

In HDRP, transparent camera output also depends on the HDRP asset supporting an
alpha-capable color buffer. Use an alpha-capable HDRP color buffer when the
default opaque preview background is not acceptable.
