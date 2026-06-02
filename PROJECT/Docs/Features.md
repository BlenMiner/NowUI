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
        NowUI.StartUI();

        NowUI.Rectangle(new Vector4(20, 20, 260, 80))
            .SetColor(new Color(0f, 0f, 0f, 0.8f))
            .SetRadius(10)
            .Draw();

        NowUI.Text(new Vector4(36, 26, 220, 64), font)
            .SetFontSize(32)
            .SetColor(Color.white)
            .Draw("Hello NowUI");

        NowUI.FlushUI();
    }
}
```

Coordinates use `Vector4(x, y, width, height)` in screen pixels. The origin is
the top-left of the active screen mask.

## Rectangles

`NowUIRectangle` is a value struct. Configure it fluently and call `Draw()`.

```csharp
NowUI.Rectangle(new Vector4(10, 10, 220, 120))
    .SetColor(Color.white)
    .SetRadius(8)
    .SetOutline(2)
    .SetOutlineColor(Color.black)
    .SetBlur(4)
    .Draw();
```

Per-corner radius and per-side padding are supported with `Vector4`.

```csharp
NowUI.Rectangle(new Vector4(24, 24, 180, 48))
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
NowUI.Text(new Vector4(24, 24, 360, 60), font)
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

NowUI.Text(new Vector4(20, 20, advance.x, bounds.w), font)
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

        NowUI.Rectangle(bounds)
            .SetColor(new Color(0.08f, 0.1f, 0.14f, 0.92f))
            .SetRadius(12)
            .SetMask(bounds)
            .Draw();

        NowUI.Text(new Vector4(16, 14, rect.width - 32, 30), font)
            .SetFontSize(20)
            .SetColor(Color.white)
            .SetMask(bounds)
            .Draw("NowUI Graphic");
    }
}
```

Call `MarkDirty()` when retained component state changes. Enable `Rebuild Every
Frame` only for animated graphics or continuously changing data.

## RenderTexture And Command Buffers

Use `NowUIRenderer` when NowUI should render outside Canvas and outside the
legacy `OnPostRender` path. It captures immediate NowUI calls into a mesh, then
draws that mesh through a `CommandBuffer`.

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
        renderer.Render(
            target,
            new Vector2(target.width, target.height),
            rect =>
            {
                NowUI.Rectangle(new Vector4(0, 0, rect.width, rect.height))
                    .SetColor(new Color(0.08f, 0.1f, 0.14f, 1f))
                    .Draw();

                NowUI.Text(new Vector4(24, 22, rect.width - 48, 40), font)
                    .SetFontSize(24)
                    .SetColor(Color.white)
                    .Draw("Rendered to a texture");
            },
            clear: true,
            clearColor: Color.clear);
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
        renderer.Build(new Vector2(camera.pixelWidth, camera.pixelHeight), rect =>
        {
            NowUI.Rectangle(new Vector4(20, 20, 220, 72))
                .SetColor(new Color(0f, 0f, 0f, 0.75f))
                .SetRadius(8)
                .Draw();
        });

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

`NowUIRenderer.Draw(commandBuffer)` draws into the command buffer's current
render target. `PopulateCommandBuffer(commandBuffer, target, clear, clearColor)`
sets an explicit target first.

## Font Compilation

Editor font assets are created from `.ttf` files.

1. Select one or more `.ttf` assets in Unity.
2. Run `Assets > NowUI > Compile Font`.
3. Assign the generated `NowFont` asset to scripts that draw text.

Runtime font compilation accepts byte arrays.

```csharp
byte[] fontBytes = LoadFontBytes();

if (!NowFontCompiler.TryCompile(fontBytes, out NowFont runtimeFont, out string error))
{
    Debug.LogError(error);
    return;
}

NowUI.Text(new Vector4(20, 20, 320, 48), runtimeFont)
    .SetFontSize(24)
    .SetColor(Color.white)
    .Draw("Runtime font");
```

Pass extra characters when the atlas must include glyphs beyond the default
starter set.

```csharp
NowFontCompiler.TryCompile(
    fontBytes,
    "Player \U0001F600",
    out NowFont runtimeFont,
    out string error);
```

## Example Scenes And Scripts

Current example scripts live under `Assets/NowUI/Example`.

- `ShapedRectangles.cs`: stress-tests rectangle radius, outline, blur, and
  padding.
- `TextTests.cs`: exercises text rendering behavior.
- `NowUIGraphicExample.cs`: demonstrates UGUI mesh capture.
- `MailClientMockup.cs`: demonstrates a larger immediate-mode layout with
  responsive panels, rows, labels, masks, and truncation.
