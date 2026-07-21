# Render Pipeline Integrations

NowUI supports the following host paths. In every retained host, choose the
base type for explicit `Now` rects and the `Layout` type for `NowLayout`:

- Built-in Render Pipeline: wrap explicit drawing in `using (Now.StartUI())`
  from a camera callback such as `OnPostRender`. For layout in this manual
  host, call `NowLayout.RunMeasured(...)` inside that frame scope.
- UGUI: derive from `NowGraphic` for explicit rects or `NowLayoutGraphic` for
  layout; both render into `CanvasRenderer`.
- UI Toolkit: place `NowVisualElement` for explicit rects or
  `NowLayoutVisualElement` for layout in UXML/UI Builder. The element draws a
  cached `RenderTexture`.
- SRP: derive from `NowPipelineGraphic` for explicit rects or
  `NowPipelineLayoutGraphic` for layout and use the URP or HDRP wrapper.

For nameplates, hover tooltips, and diegetic panels that should exist as scene
geometry, use `NowWorldGraphic` for explicit rects or `NowWorldLayoutGraphic`
for layout. Both render through a normal `MeshRenderer`, so they do not need a
pipeline feature or custom pass; see [WorldSpace](WorldSpace.md).

`Now.Glass(...)` uses true backdrop blur when the host renders NowUI into a
known command-buffer or `RenderTexture` target. URP/HDRP overlays,
`NowRenderer.Render(...)`, IMGUI, and UI Toolkit can blur content already drawn
into that target. UGUI automatically uses replay-backed glass when a
`NowGraphic` contains glass: it replays earlier NowUI batches into a blurred
texture for the glass material, and can start that replay from
`uguiBackdropSourceTexture` or a canvas camera `targetTexture`. World-space mesh
rendering can copy/blur camera color and optionally replay other
`NowWorldGraphic` meshes behind each requester through
`NowWorldGraphic.glassBackdropMode`. Blurred world glass automatically requests
camera depth and samples a sharp backdrop where opaque scene geometry is in
front of the pane.
Built-in `Now.StartUI()` screen rendering falls back to the same rounded
tint/outline appearance without sampling the target behind it.

## Shared SRP Source

`NowPipelineGraphic` is the one-pass source component for explicit-rect URP
and HDRP overlays. Attach a derived component to a GameObject, assign a
`NowFont` or other draw data, then override `DrawNowUI`.

```csharp
using UnityEngine;

public sealed class MySrpOverlay : NowPipelineGraphic
{
    [SerializeField] NowFont font;

    protected override void DrawNowUI(Camera camera, Rect rect)
    {
        Now.Rectangle(new Vector4(24, 24, 260, 72))
            .SetColor(new Color(0f, 0f, 0f, 0.75f))
            .SetRadius(10)
            .Draw();

        Now.Text(new Vector4(42, 38, 220, 32), font)
            .SetFontSize(20)
            .SetColor(Color.white)
            .Draw("NowUI SRP");
    }
}
```

Use the component fields to choose which cameras it draws into.

- `Target Camera`: if assigned, only that camera receives the draw calls.
- `Render Game Cameras`: enables normal game cameras.
- `Render Scene View`: enables the editor scene view.
- `Render Preview Cameras`: enables preview cameras.
- `Order`: controls draw order when multiple pipeline graphics target the same
  camera.

The included `NowPipelineOverlayExample` demonstrates this component.

For an overlay that uses `NowLayout`, derive from
`NowPipelineLayoutGraphic` instead. It owns that graphic's exact measure/draw
cycle; other graphics targeting the same camera keep their own one-pass or
layout-host behavior. Use ordinary layout scopes inside `DrawNowUI` and do not
add `RunMeasured`.

## UI Toolkit

`NowVisualElement` is the one-pass UI Toolkit host for explicit-rect drawing;
`NowLayoutVisualElement` is its exact-measure counterpart for `NowLayout`.
Both are exposed to UXML and UI Builder. They keep UI Toolkit responsible for
retained layout, focus, clipping and authoring, while NowUI owns the drawing
inside the element's content rect. The bridge renders into a cached
`RenderTexture`, so rectangles, MSDF text, effects, Lottie and custom
materials behave the same as the other NowUI hosts.

Use it directly from code:

```csharp
using NowUI;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class MyDocument : MonoBehaviour
{
    [SerializeField] UIDocument document;

    void OnEnable()
    {
        var now = document.rootVisualElement.Q<NowVisualElement>("preview");

        now.rebuildNowUI += (element, rect) =>
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.08f, 0.1f, 0.14f, 1f))
                .SetRadius(10f)
                .Draw();

            Now.Text(new NowRect(16, 14, rect.width - 32, 28))
                .SetFontSize(18f)
                .SetColor(Color.white)
                .Draw("NowUI in UI Toolkit");
        };
    }
}
```

Or place it in UXML/UI Builder and bind drawing from a controller:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:now="NowUI">
    <now:NowVisualElement name="preview"
                          rebuild-every-frame="false"
                          auto-rebuild-on-interaction="true" />
</ui:UXML>
```

For reusable UI Builder controls, derive a concrete element and override
`DrawNowUI`:

```csharp
using NowUI;
using UnityEngine;

[UxmlElement]
public partial class NowStatusBadge : NowVisualElement
{
    [UxmlAttribute] public string label { get; set; } = "Ready";

    protected override void DrawNowUI(NowRect rect)
    {
        Now.Rectangle(rect)
            .SetColor(new Color(0.12f, 0.55f, 0.32f, 1f))
            .SetRadius(rect.height * 0.5f)
            .Draw();

        Now.Text(new NowRect(12f, 4f, rect.width - 24f, rect.height - 8f))
            .SetFontSize(14f)
            .SetColor(Color.white)
            .Draw(label);
    }
}
```

The generic `NowVisualElement` and `NowLayoutVisualElement` can be authored in
UXML, but UXML does not contain immediate NowUI draw commands. Put draw code in
a callback or subclass. Use the layout type when that draw code calls
`NowLayout`; it owns measurement, so do not call `RunMeasured` inside it. Call
`MarkDirty()` when retained data changes. Enable `Rebuild Every Frame` only for
continuously animated content; controls request repaint automatically while
hover, press, caret blink, scrolling or other transient state is active.

## URP

URP support lives in the optional `NowUI.URP` assembly. It is enabled only when
the `com.unity.render-pipelines.universal` package is installed.

1. Install or enable URP in the project.
2. Add `NowUniversalRendererFeature` to the active URP Renderer asset.
3. Choose the render pass event, usually `After Rendering Post Processing`.
4. Add one or more `NowPipelineGraphic` components for explicit drawing or
   `NowPipelineLayoutGraphic` components for layout.

The renderer feature builds a `NowDrawList` for each camera that has matching
pipeline graphics, then appends the draw commands to URP's command buffer.

The feature's `UI Scale` field sets pixels per UI unit, and
`Scale By Display Density` follows `NowScreen.recommendedUIScale` instead so
UI keeps a consistent physical size on phones. See [Mobile](Mobile.md).

## HDRP

HDRP support lives in the optional `NowUI.HDRP` assembly. It is enabled only when
the `com.unity.render-pipelines.high-definition` package is installed.

1. Install or enable HDRP in the project.
2. Add a Custom Pass Volume.
3. Add `NowHighDefinitionCustomPass` to the volume's custom pass list.
4. Pick an injection point such as `After Post Process`.
5. Add one or more `NowPipelineGraphic` components for explicit drawing or
   `NowPipelineLayoutGraphic` components for layout.

The custom pass uses the `CommandBuffer` from `CustomPassContext` and draws the
same `NowDrawList` format as URP and RenderTexture output. It exposes the
same `UI Scale` / `Scale By Display Density` options as the URP feature.

## Wrapper Shape

Future pipeline targets should remain thin wrappers.

```csharp
if (NowPipelineGraphic.BuildDrawList(camera, drawList))
    NowRenderer.Draw(
        commandBuffer,
        drawList,
        BuiltinRenderTextureType.CameraTarget,
        camera.pixelWidth,
        camera.pixelHeight);
```

The draw-list layer owns immediate-mode capture. Pipeline wrappers should only
choose cameras, injection points, command buffers, and render targets.
