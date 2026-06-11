# Feature Usage

NowUI is an immediate-mode renderer for Unity. The current public surface is
small: start a draw pass, configure lightweight value structs, draw rectangles
and text, then flush.

## Draw Lifecycle

Use this path from a camera render callback such as `OnPostRender`.

```csharp
using UnityEngine;

public sealed class OverlayExample : MonoBehaviour
{
    [SerializeField] NowFont font;

    void OnPostRender()
    {
        Now.StartUI();

        Now.Rectangle(new Vector4(20, 20, 260, 80))
            .SetColor(new Color(0f, 0f, 0f, 0.8f))
            .SetRadius(10)
            .Draw();

        Now.Text(new Vector4(36, 26, 220, 64), font)
            .SetFontSize(32)
            .SetColor(Color.white)
            .Draw("Hello NowUI");

        Now.FlushUI();
    }
}
```

Coordinates use `Vector4(x, y, width, height)` in UI units with a top-left
origin. By default one unit is one pixel; pass a scale to
`Now.StartUI(NowUIScreen.recommendedUIScale)` to draw in density-independent
units on high-DPI screens (see [Mobile](Mobile.md)). For automatic sizing and
nesting instead of hand-placed rects, see [Layout](Layout.md); for vector
animation, see [Lottie](Lottie.md).

## Interaction And Input

NowUI keeps input separate from rendering through `INowUIInputProvider`.
Providers normalize their source into the current NowUI surface coordinates, so
controls can use the same `NowUIInput.Interact(...)` calls in screen rendering,
IMGUI, UGUI, RenderTexture, SRP overlays, and tests.

```csharp
var rect = new Vector4(20, 20, 160, 44);
var state = NowUIInput.Interact("save-button", rect);

Now.Rectangle(rect)
    .SetColor(state.hovered ? Color.white : Color.gray)
    .SetRadius(6)
    .Draw();

if (state.clicked)
    Save();
```

`NowUIInteraction` reports `hovered`, `pressed`, `held`, `released`, `clicked`,
`dragging`, `dragStarted`, and `dragEnded`. Primary mouse/button interaction is
the default, and the same API can target right click, middle click, and common
mouse navigation buttons.

```csharp
var context = NowUIInput.Interact("row-menu", rowRect, NowUIPointerButton.Secondary);

if (context.clicked)
    OpenContextMenu();
```

Control id strings are hashed with a stable internal hash; pass an integer id
when you already have stable ids. `NowUIInput.current.navigation` carries
keyboard/gamepad navigation as a `Vector2`, while `submit*` and `cancel*` fields
track action buttons.

The built-in render paths set up input where they already own a surface:

- `Now.StartUI(...)` uses `NowUIInput.defaultProvider`, which defaults to
  screen-space mouse and touch input from the Unity Input System. It falls
  back to legacy `UnityEngine.Input` only when the legacy input manager is
  enabled. If neither source is available, it returns no pointer instead of
  touching a disabled input API. The default provider reads primary,
  secondary, middle, back, and forward mouse buttons where the active input
  backend exposes them, and maps the primary touch to the primary pointer
  while a finger is in contact. With the Input System it also reads keyboard
  arrows/WASD, gamepad left stick/D-pad, submit, and cancel. Legacy fallback
  covers touch, mouse buttons 0-4, arrows/WASD, enter/space, escape, and the
  first two joystick buttons.
- `NowUIGUI.Auto(...)` and `NowUIGUILayout.Auto(...)` use IMGUI events.
- `NowUIGraphic` uses a `RectTransform` mouse provider.
- `NowUIPipelineGraphic.BuildDrawList(...)` maps screen mouse input into the
  camera pixel rect.

For RenderTexture previews, world-space quads, remote input, or tests, scope a
custom provider around the draw code.

```csharp
using (NowUIInput.Begin(myInputProvider, new Vector2(target.width, target.height)))
using (renderer.Begin(target))
{
    var state = NowUIInput.Interact(42, new Rect(8, 8, 120, 32));
}
```

Mock providers only need to return a `NowUIInputSnapshot`, which makes immediate
mode controls testable without Unity's live input devices.

## Rectangles

`NowUIRectangle` is a value struct. Configure it fluently and call `Draw()`.

```csharp
Now.Rectangle(new Vector4(10, 10, 220, 120))
    .SetColor(Color.white)
    .SetRadius(8)
    .SetOutline(2)
    .SetOutlineColor(Color.black)
    .SetBlur(4)
    .Draw();
```

Per-corner radius and per-side padding are supported with `Vector4`.

```csharp
Now.Rectangle(new Vector4(24, 24, 180, 48))
    .SetRadius(new Vector4(12, 12, 4, 4))
    .SetPadding(new Vector4(8, 4, 8, 4))
    .SetColor(new Color(0.1f, 0.45f, 0.95f, 1f))
    .Draw();
```

The rectangle API currently covers fill color, radius, padding, outline,
outline color, blur, mask, and position.

## Text

`NowUIText` draws MSDF glyphs from a compiled `NowFont` asset.

```csharp
Now.Text(new Vector4(24, 24, 360, 60), font)
    .SetFontSize(42)
    .SetColor(Color.white)
    .SetOutline(1)
    .SetOutlineColor(Color.black)
    .Draw("Score: 1200");
```

Use measurement when layout needs to reserve space, align labels, or truncate
text before drawing.

```csharp
string label = "Inbox";
float fontSize = 18f;
Vector2 advance = font.MeasureText(label, fontSize);
Vector4 bounds = font.MeasureTextBounds(label, fontSize);

Now.Text(new Vector4(20, 20, advance.x, bounds.w), font)
    .SetFontSize(fontSize)
    .SetColor(Color.white)
    .Draw(label);
```

Text supports tabs, newlines, and Unicode code point reading for
supplementary-plane glyphs when the font atlas contains those glyphs.

## UGUI Rendering

Use `NowUIGraphic` when NowUI should render into a UGUI `CanvasRenderer`.
Override `DrawNowUI(Rect rect)` and do not call `StartUI()` or `FlushUI()`.

```csharp
using UnityEngine;

public sealed class MyPanel : NowUIGraphic
{
    [SerializeField] NowFont font;

    protected override void DrawNowUI(Rect rect)
    {
        Vector4 bounds = new Vector4(0, 0, rect.width, rect.height);

        Now.Rectangle(bounds)
            .SetColor(new Color(0.08f, 0.1f, 0.14f, 0.92f))
            .SetRadius(12)
            .SetMask(bounds)
            .Draw();

        Now.Text(new Vector4(16, 14, rect.width - 32, 30), font)
            .SetFontSize(20)
            .SetColor(Color.white)
            .SetMask(bounds)
            .Draw("NowUI Graphic");
    }
}
```

Call `MarkDirty()` when retained component state changes. Enable `Rebuild Every
Frame` only for animated graphics or continuously changing data.

## IMGUI

Use `NowUIGUI.Auto(rect)` or `NowUIGUILayout.Auto(...)` from runtime
`OnGUI` code. In editor code, `NowUIEditorGUI` and `NowUIEditorGUILayout` are
aliases that add editor pixel-density handling. These helpers render NowUI into
a cached `RenderTexture` and draw it with IMGUI.

```csharp
using (var ui = NowUIGUILayout.Auto(96))
{
    Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

## RenderTexture And Command Buffers

Use `NowUIRenderer` when NowUI should render outside Canvas and outside the
legacy `OnPostRender` path. Internally this uses `NowUIDrawList`, the shared
capture container that turns immediate NowUI calls into a mesh plus material
batches. Pipeline integrations should stay thin: build a draw list, then call
`NowUIRenderer.Draw(commandBuffer, drawList)` or set the target with
`NowUIRenderer.PopulateCommandBuffer(...)`.

Render directly into a pure `RenderTexture`.

```csharp
using UnityEngine;

public sealed class NowUITextureExample : MonoBehaviour
{
    [SerializeField] RenderTexture target;
    [SerializeField] NowFont font;

    readonly NowUIRenderer renderer = new NowUIRenderer();

    void Update()
    {
        using (renderer.Begin(target))
        {
            var rect = new Rect(0, 0, target.width, target.height);

            Now.Rectangle(new Vector4(0, 0, rect.width, rect.height))
                .SetColor(new Color(0.08f, 0.1f, 0.14f, 1f))
                .Draw();

            Now.Text(new Vector4(24, 22, rect.width - 48, 40), font)
                .SetFontSize(24)
                .SetColor(Color.white)
                .Draw("Rendered to a texture");
        }

        renderer.Render(target, clear: true, clearColor: Color.clear);
    }

    void OnDestroy()
    {
        renderer.Dispose();
    }
}
```

Populate a command buffer for SRP integration points such as
`RenderPipelineManager.endCameraRendering`, a URP render pass, or an HDRP custom
pass.

```csharp
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUISrpExample : MonoBehaviour
{
    readonly NowUIRenderer renderer = new NowUIRenderer();
    readonly CommandBuffer commandBuffer = new CommandBuffer { name = "NowUI" };

    void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += DrawNowUI;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= DrawNowUI;
    }

    void DrawNowUI(ScriptableRenderContext context, Camera camera)
    {
        var size = new Vector2(camera.pixelWidth, camera.pixelHeight);

        using (renderer.Begin(size))
        {
            Now.Rectangle(new Vector4(20, 20, 220, 72))
                .SetColor(new Color(0f, 0f, 0f, 0.75f))
                .SetRadius(8)
                .Draw();
        }

        commandBuffer.Clear();
        renderer.Draw(commandBuffer);
        context.ExecuteCommandBuffer(commandBuffer);
    }

    void OnDestroy()
    {
        commandBuffer.Release();
        renderer.Dispose();
    }
}
```

`NowUIRenderer.Begin(...)` builds the renderer's current draw list.
`NowUIRenderer.Draw(commandBuffer)` draws that current draw list into the
command buffer's current render target.
`NowUIRenderer.Draw(commandBuffer, drawList)` does the same for an external
draw list. `PopulateCommandBuffer(...)` sets an explicit target first.

## Font Compilation

Editor font assets are created from `.ttf` files as source-only `NowFont`
assets.

1. Select one or more `.ttf` assets in Unity.
2. Run `Assets > NowUI > Compile Font`.
3. Assign the generated `NowFont` asset to scripts that draw text.

Runtime font compilation accepts byte arrays and creates the same source-only
dynamic font object.

```csharp
byte[] fontBytes = LoadFontBytes();

if (!NowFontCompiler.TryCompile(fontBytes, out NowFont runtimeFont, out string error))
{
    Debug.LogError(error);
    return;
}

Now.Text(new Vector4(20, 20, 320, 48), runtimeFont)
    .SetFontSize(24)
    .SetColor(Color.white)
    .Draw("Runtime font");
```

Glyphs are compiled into dynamic atlas pages the first time text asks for them,
including emoji and other non-ASCII codepoints.

The generated `NowFont` stores the source font bytes directly and does not keep
a reference to the original `.ttf` asset or create a baked atlas texture subasset.

## Example Scenes And Scripts

Current example scripts live under `Assets/NowUI/Example`.

- `ShapedRectangles.cs`: stress-tests rectangle radius, outline, blur, and
  padding.
- `TextTests.cs`: exercises text rendering behavior.
- `NowUIGraphicExample.cs`: demonstrates UGUI mesh capture.
- `NowUIRenderTextureExample.cs`: renders NowUI into a `RenderTexture` and
  applies it to a scene `Renderer`.
- `NowUIPipelineOverlayExample.cs`: demonstrates an SRP overlay source for URP
  and HDRP wrappers.
- `MailClientMockup.cs`: demonstrates a larger immediate-mode layout with
  responsive panels, rows, labels, masks, and truncation.
