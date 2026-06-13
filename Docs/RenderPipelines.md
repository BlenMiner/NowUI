# Render Pipeline Integrations

NowUI supports three non-retained output paths.

- Built-in Render Pipeline: call `Now.StartUI()` and `Now.FlushUI()` from
  camera callbacks such as `OnPostRender`.
- UGUI: derive from `NowGraphic` and render into `CanvasRenderer`.
- SRP: derive from `NowPipelineGraphic` and use the URP or HDRP wrapper.

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
