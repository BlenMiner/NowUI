# World Space

`NowWorldGraphic` renders NowUI directly into a `MeshRenderer`, so the same
rectangles, text, layout, controls, overlays, and effects can be used for
nameplates, hover tooltips, and diegetic panels without a `RenderTexture`.

Attach a component derived from `NowWorldGraphic` to a GameObject and override
`DrawNowUI`:

```csharp
using UnityEngine;
using NowUI;

public sealed class Nameplate : NowWorldGraphic
{
    [SerializeField] string playerName = "Player";

    protected override void DrawNowUI(NowRect rect)
    {
        int id = NowControls.GetControlId(1);
        var hover = NowInput.Interact(id, rect);

        Now.Rectangle(rect)
            .SetColor(hover.hovered ? new Color(0.08f, 0.12f, 0.18f, 0.94f) : new Color(0.04f, 0.06f, 0.09f, 0.84f))
            .SetRadius(12f)
            .Draw();

        Now.Text(new NowRect(16, 14, rect.width - 32, 28))
            .SetFontSize(20f)
            .SetColor(Color.white)
            .Draw(playerName);
    }
}
```

The component owns:

- `Size`: the NowUI surface size in UI units.
- `Pixels Per Unit`: how many UI units map to one local Unity unit.
- `Pivot`: Unity-style pivot; `(0.5, 0.5)` centers the mesh.
- `Facing Mode`: fixed panel, full billboard, or yaw-only billboard.
- `Depth Mode`: always-readable labels or scene-occluded panels.
- `Glass Backdrop Mode`: how `Now.Glass(...)` samples the camera backdrop.
- `Accept Navigation`: whether this surface consumes keyboard/gamepad submit,
  cancel, and navigation input.

Pointer input is ray-mapped from the target camera onto the surface plane and
arrives through normal `NowInput` snapshots. Built-in controls work unchanged,
and the host automatically wraps drawing in an integer `NowControls.IdScope`
keyed by the component instance, so repeated nameplate prefabs do not share
button, hover, focus, or animation state.

Call `MarkDirty()` when retained data changes, or enable `Rebuild Every Frame`
for continuously changing labels. Camera-facing rotation updates in
`LateUpdate` without rebuilding the mesh. After the draw-list, mesh, material,
and vertex buffers are warm, stable rebuilds are intended to avoid managed
allocations unless the panel grows past an existing capacity or introduces a
new material batch.

## Depth

`AlwaysVisible` is the default because nameplates and hover prompts usually
need to stay readable. `SceneOccluded` sets the cloned NowUI materials to
`ZTest LessEqual`, so world panels can be hidden by nearer scene geometry.
The source package materials are never mutated.

## Glass Backdrops

`NowWorldGraphic` can draw `Now.Glass(...)` panes by sampling a camera backdrop
captured before transparent rendering:

- `TintOnly`: no camera capture; glass renders as its rounded tint/outline.
- `Camera`: copies the camera color and samples it.
- `CameraAndWorld`: copies camera color, then draws eligible
  `NowWorldGraphic` meshes behind each glass requester into that requester's
  copy.

Blur is controlled by the blur radius requested by `Now.Glass(...)` batches.
`CameraAndWorld` is the default and only runs when the world graphic contains a
glass batch.

Built-in render pipeline cameras get an automatic `BeforeForwardAlpha` command
buffer. In URP, add `NowUniversalRendererFeature`; it enqueues a
pre-transparent world-glass pass when needed. In HDRP, use
`NowHighDefinitionCustomPass` at an injection point before the transparent
world UI draws.

The world-contributor modes build a separate backdrop for each glass requester,
including other `NowWorldGraphic` meshes behind that requester. Glass submeshes
from those contributors are skipped to avoid recursive backdrop sampling. Other
transparent scene objects rendered after the backdrop capture are not included.

Blurred world glass automatically requests a camera depth texture and samples a
sharp backdrop where opaque scene geometry is closer than the pane. This keeps
foreground cubes/walls sharp instead of blurring them into the pane. Depth can
only protect pixels the camera depth texture knows about; it does not
reconstruct hidden background behind an occluding object, and it does not detect
transparent objects that do not write depth.

## Deforming Surfaces

Add a `NowWorldDeformer` and assign it to the graphic to remap vertices after
NowUI has built its mesh:

```csharp
public sealed class WrapAroundY : NowWorldDeformer
{
    public float radius = 0.6f;

    public override Vector3 Deform(in NowWorldVertex vertex)
    {
        float angle = (vertex.normalized.x - 0.5f) * Mathf.PI;
        return new Vector3(
            Mathf.Sin(angle) * radius,
            vertex.localPosition.y,
            Mathf.Cos(angle) * radius - radius);
    }
}
```

The shader data for masks, rounded rectangles, text SDFs, and colors remains in
UI coordinates, so deformers only need to return the local 3D position.

`Assets/NowUI/Example/NowWorldGraphicExample.cs` shows a small billboard label
that expands its text on hover and logs clicks.
