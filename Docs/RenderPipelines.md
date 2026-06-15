# Render Pipeline Integrations

NowUI supports three non-retained output paths.

- Built-in Render Pipeline: call `Now.StartUI()` and `Now.FlushUI()` from
  camera callbacks such as `OnPostRender`.
- UGUI: derive from `NowGraphic` and render into `CanvasRenderer`.
- UI Toolkit: place `NowVisualElement` in UXML/UI Builder and render into a
  cached `RenderTexture` drawn by the element.
- SRP: derive from `NowPipelineGraphic` and use the URP or HDRP wrapper.

For nameplates, hover tooltips, and diegetic panels that should exist as scene
geometry, use `NowWorldGraphic` instead. It renders through a normal
`MeshRenderer`, so it does not need a pipeline feature or custom pass; see
[WorldSpace](WorldSpace.md).

## Shared SRP Source

`NowPipelineGraphic` is the shared source component for URP and HDRP. Attach a
derived component to a GameObject, assign a `NowFont` or other draw data, then
override `DrawNowUI`.

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

## UI Toolkit

`NowVisualElement` is a UI Toolkit `VisualElement` exposed to UXML and UI
Builder. It keeps UI Toolkit responsible for retained layout, focus, clipping
and authoring, while NowUI owns the drawing inside the element's content rect.
The bridge renders into a cached `RenderTexture`, so rectangles, MSDF text,
effects, Lottie and custom materials behave the same as the other NowUI hosts.

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

The generic `NowVisualElement` can be authored in UXML, but UXML does not
contain immediate NowUI draw commands. Put draw code in a callback or subclass.
Call `MarkDirty()` when retained data changes. Enable `Rebuild Every Frame`
only for continuously animated content; controls request repaint automatically
while hover, press, caret blink, scrolling or other transient state is active.

## URP

URP support lives in the optional `NowUI.URP` assembly. It is enabled only when
the `com.unity.render-pipelines.universal` package is installed.

1. Install or enable URP in the project.
2. Add `NowUniversalRendererFeature` to the active URP Renderer asset.
3. Choose the render pass event, usually `After Rendering Post Processing`.
4. Add one or more `NowPipelineGraphic` components in the scene.

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
5. Add one or more `NowPipelineGraphic` components in the scene.

The custom pass uses the `CommandBuffer` from `CustomPassContext` and draws the
same `NowDrawList` format as URP and RenderTexture output. It exposes the
same `UI Scale` / `Scale By Display Density` options as the URP feature.

## Wrapper Shape

Future pipeline targets should remain thin wrappers.

```csharp
if (NowPipelineGraphic.BuildDrawList(camera, drawList))
    NowRenderer.Draw(commandBuffer, drawList);
```

The draw-list layer owns immediate-mode capture. Pipeline wrappers should only
choose cameras, injection points, command buffers, and render targets.
