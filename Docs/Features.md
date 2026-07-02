# Feature Usage

NowUI is an immediate-mode renderer for Unity. The current public surface is
small: open a draw pass scope, configure lightweight value structs, draw
rectangles, lines, shapes and text, then let the scope flush the frame.

## Draw Lifecycle

Use this path from a camera render callback such as `OnPostRender`.

```csharp
using UnityEngine;

public sealed class OverlayExample : MonoBehaviour
{
    [SerializeField] NowFont font;

    void OnPostRender()
    {
        using (Now.StartUI())
        {
            Now.Rectangle(new Vector4(20, 20, 260, 80))
                .SetColor(new Color(0f, 0f, 0f, 0.8f))
                .SetRadius(10)
                .Draw();

            Now.Text(new Vector4(36, 26, 220, 64), font)
                .SetFontSize(32)
                .SetColor(Color.white)
                .Draw("Hello NowUI");
        }
    }
}
```

Coordinates use `Vector4(x, y, width, height)` in UI units with a top-left
origin. By default one unit is one pixel; pass a scale to
`Now.StartUI(NowScreen.recommendedUIScale)` to draw in density-independent
units on high-DPI screens (see [Mobile](Mobile.md)). For automatic sizing and
nesting instead of hand-placed rects, see [Layout](Layout.md); for vector
animation, see [Lottie](Lottie.md).

## Interaction And Input

NowUI keeps input separate from rendering through `INowInputProvider`.
Providers normalize their source into the current NowUI surface coordinates, so
controls can use the same `NowInput.Interact(...)` calls in screen rendering,
IMGUI, UGUI, world-space meshes, RenderTexture, SRP overlays, and tests.

```csharp
var rect = new NowRect(20, 20, 160, 44);
var state = NowInput.Interact(rect);

Now.Rectangle(rect)
    .SetColor(state.hovered ? Color.white : Color.gray)
    .SetRadius(6)
    .Draw();

if (state.clicked)
    Save();
```

`NowInteraction` reports `hovered`, `pressed`, `held`, `released`, `clicked`,
`dragging`, `dragStarted`, and `dragEnded`. Primary mouse/button interaction is
the default, and the same API can target right click, middle click, and common
mouse navigation buttons.

```csharp
var context = NowInput.Interact(panelRect, NowPointerButton.Secondary);

if (context.clicked)
    OpenContextMenu();
```

Call-site identity is enough for one-off controls and fixed draw-order loops.
Explicit ids use `NowId`, which accepts strings or non-zero integers. Prefer
integer ids when you already have stable data ids and the items can appear,
disappear, or reorder. `NowInput.current.navigation` carries keyboard/gamepad
navigation as a `Vector2`, while `submit*` and `cancel*` fields track action
buttons.

The built-in render paths set up input where they already own a surface:

- `Now.StartUI(...)` uses `NowInput.defaultProvider`, which reads screen-space
  mouse and touch input from the Unity Input System (the package is a
  required dependency). When no Input System devices are present, it returns
  no pointer. The provider reads primary, secondary, middle, back, and
  forward mouse buttons, maps the active touch to the primary pointer while
  a finger is in contact (surviving multi-finger handoffs), and reads
  keyboard arrows/WASD, gamepad left stick/D-pad, submit, and cancel.
  `NowInput.navigationKeys` disables individual keyboard bindings (WASD,
  arrows, Tab focus, space/enter submit) for games that need those keys, and
  `NowInput.dragThreshold` overrides the default click-vs-drag distance,
  which scales with screen DPI on touch devices.
- `NowGUI.Auto(...)` and `NowGUILayout.Auto(...)` use IMGUI events.
- `NowGraphic` uses a `RectTransform` mouse provider.
- `NowWorldGraphic` uses a ray-to-surface provider for world-space meshes.
- `NowPipelineGraphic.BuildDrawList(...)` maps screen mouse input into the
  camera pixel rect.

For RenderTexture previews, remote input, or tests, scope a custom provider
around the draw code.

```csharp
using (NowInput.Begin(myInputProvider, new Vector2(target.width, target.height)))
using (renderer.Begin(target))
{
    var state = NowInput.Interact(new Rect(8, 8, 120, 32));
}
```

Mock providers only need to return a `NowInputSnapshot`, which makes immediate
mode controls testable without Unity's live input devices.

## Rectangles

`NowRectangle` is a value struct. Configure it fluently and call `Draw()`.

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

The rectangle API covers fill color, radius, padding, outline, outline color,
blur, mask, position, textures, sprites, and custom materials.

## Glass

`Now.Glass(...)` draws a rounded translucent pane that samples and blurs what
has already been rendered into the current target. Draw background content
first, then the glass pane, then foreground labels or icons.

```csharp
Now.Rectangle(new NowRect(0, 0, 420, 220))
    .SetTexture(wallpaper)
    .Draw();

Now.Glass(new NowRect(24, 24, 260, 96))
    .SetBlurRadius(18)
    .SetBlurQuality(NowGlassBlurQuality.Balanced)
    .SetTint(new Color(1, 1, 1, 0.22f))
    .SetRadius(18)
    .SetOutline(1)
    .SetOutlineColor(new Color(1, 1, 1, 0.35f))
    .Draw();

Now.Text(new NowRect(44, 44, 220, 40))
    .SetColor(Color.white)
    .Draw("Frosted panel");
```

Backdrop blur is available on command-buffer or RenderTexture-backed hosts:
`NowRenderer.Render(...)`, `PopulateCommandBuffer(...)`, IMGUI, UI Toolkit,
and pipeline overlays. UGUI automatically uses expensive replay-backed blur
when a `NowGraphic` contains glass; it blurs NowUI content from the same
graphic and can include camera content when `uguiBackdropSourceTexture` or the
canvas camera's `targetTexture` supplies a source. World-space mesh rendering
supports camera/world backdrop modes. `NowRenderer.Draw(commandBuffer,
drawList)` and the legacy GL flush path fall back to replaying earlier NowUI
batches into temporary textures, so glass still blurs NowUI when no readable
screen target was provided. Use `SetBlurQuality(...)`, host `glassBlurQuality`,
or `NowGlassSettings.defaultBlurQuality` to trade cost for quality; quality
levels do not disable blur. See [Glass](Glass.md) for the full builder API and
the docs-scene demo.

Custom rectangle materials draw the same quad geometry as the built-in
rectangle shader:

```csharp
Now.Rectangle(new NowRect(24, 96, 180, 72))
    .SetTexture(frostTexture)
    .SetMaterial(frostMaterial)
    .Draw();
```

If the shader used inside UGUI needs different stencil, clipping, or vertex
layout support, pass a UGUI variant as the second material:

```csharp
Now.Rectangle(panel)
    .SetMaterial(frostMaterial, frostUGUIMaterial)
    .Draw();
```

`SetCanvasMaterial(...)` can override only the UGUI material while non-UGUI
hosts keep the normal rectangle material. A custom material receives NowUI's
rectangle vertex streams; for UGUI it should include Unity UI stencil and clip
properties if it needs to work under `Mask`, `RectMask2D`, or material
modifiers. See [Custom Materials](CustomMaterials.md) for the full shader
contract and the live docs-scene demo.

## Lines

`NowLine` draws anti-aliased strokes, cubic Beziers, dashes, caps and arrow
heads. See [Lines](Lines.md) for the full stroke API.

```csharp
Now.Bezier(
        new Vector2(24, 96),
        new Vector2(88, 24),
        new Vector2(180, 168),
        new Vector2(244, 96))
    .SetWidth(4f)
    .SetCap(NowLineCap.Round)
    .SetDash(12f, 8f)
    .SetArrow(NowLineArrow.End)
    .SetColor(new Color(0.05f, 0.86f, 0.67f, 1f))
    .Draw();
```

## Shapes

`NowCircle`, `NowTriangle`, and `NowPolygon` draw simple filled or outlined
geometry. Polygons use caller-owned `Vector2[]` or `List<Vector2>` point
storage. See [Shapes](Shapes.md) for the full shape API.

```csharp
Now.Circle(new Vector2(72, 72), 32f)
    .SetColor(new Color(0.1f, 0.55f, 1f, 1f))
    .SetOutline(2f)
    .SetOutlineColor(Color.white)
    .Draw();

Now.Triangle(
        new Vector2(140, 104),
        new Vector2(196, 104),
        new Vector2(168, 48))
    .SetColor(new Color(0.92f, 0.24f, 0.58f, 1f))
    .Draw();
```

## Effects

`NowEffects.Modifier(...)` captures ordinary draw calls in a scope and appends a
deformed version of that geometry. Mesh capture is the default; call
`SetRenderToTexture()` when the scoped content should flatten into a texture
first. See [Effects](Effects.md) for subdivision, custom deformers, snapshots,
and GC notes.

```csharp
using (NowEffects.Modifier(NowDeformers.Wave(Time.time, 6f, 48f))
    .SetSubdivision(4)
    .Begin())
{
    Now.Rectangle(new NowRect(24, 24, 220, 96))
        .SetColor(new Color(0.1f, 0.55f, 1f, 1f))
        .SetRadius(10f)
        .Draw();

    Now.Text(new NowRect(42, 52, 180, 26))
        .SetFontSize(18f)
        .SetColor(Color.white)
        .Draw("Deformed draw calls");
}
```

## Text

`NowText` draws MSDF glyphs from a compiled `NowFont` asset.

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

Use `NowGraphic` when NowUI should render into a UGUI `CanvasRenderer`.
Override `DrawNowUI(NowRect rect)` and do not call `StartUI()`; the host opens
and finalizes the draw pass for you.

```csharp
using UnityEngine;

public sealed class MyPanel : NowGraphic
{
    [SerializeField] NowFont font;

    protected override void DrawNowUI(NowRect rect)
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

Use `NowGUI.Auto(rect)` or `NowGUILayout.Auto(...)` from runtime
`OnGUI` code. In editor code, `NowEditorGUI` and `NowEditorGUILayout` are
aliases that add editor pixel-density handling. These helpers render NowUI into
a cached `RenderTexture` and draw it with IMGUI.

```csharp
using (var ui = NowGUILayout.Auto(96))
{
    Now.Rectangle(new Vector4(0, 0, ui.width, ui.height))
        .SetColor(Color.black)
        .SetRadius(8)
        .Draw();
}
```

## RenderTexture And Command Buffers

Use `NowRenderer` when NowUI should render outside Canvas and outside the
legacy `OnPostRender` path. Internally this uses `NowDrawList`, the shared
capture container that turns immediate NowUI calls into a mesh plus material
batches. Pipeline integrations should stay thin: build a draw list, then call
`NowRenderer.Draw(commandBuffer, drawList)` or set the target with
`NowRenderer.PopulateCommandBuffer(...)`.

Render directly into a pure `RenderTexture`.

```csharp
using UnityEngine;

public sealed class NowRenderTextureExample : MonoBehaviour
{
    [SerializeField] RenderTexture target;
    [SerializeField] NowFont font;

    readonly NowRenderer renderer = new NowRenderer();

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

public sealed class NowSrpExample : MonoBehaviour
{
    readonly NowRenderer renderer = new NowRenderer();
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

`NowRenderer.Begin(...)` builds the renderer's current draw list.
`NowRenderer.Draw(commandBuffer)` draws that current draw list into the
command buffer's current render target.
`NowRenderer.Draw(commandBuffer, drawList)` does the same for an external
draw list. `PopulateCommandBuffer(...)` sets an explicit target first.

## World Space

Use `NowWorldGraphic` when NowUI should live on a world-space mesh: nameplates
over characters, item hover labels, and diegetic panels. It captures the same
draw calls into a `MeshFilter`/`MeshRenderer`, ray-maps pointer input from a
camera into surface coordinates, and can either face the camera or stay fixed
in the scene.

```csharp
public sealed class Nameplate : NowWorldGraphic
{
    protected override void DrawNowUI(NowRect rect)
    {
        var hover = NowInput.Interact(rect);

        Now.Rectangle(rect)
            .SetColor(hover.hovered ? Color.white : new Color(0f, 0f, 0f, 0.75f))
            .SetRadius(12)
            .Draw();

        Now.Text(new NowRect(16, 14, rect.width - 32, 28))
            .SetFontSize(20)
            .SetColor(Color.white)
            .Draw("Player");
    }
}
```

Set **Depth Mode** to `AlwaysVisible` for readable billboards or
`SceneOccluded` for panels that should hide behind world geometry. Assign a
`NowWorldDeformer` to bend the captured mesh after layout, for effects like
curved labels or wrapped text. See [WorldSpace](WorldSpace.md).

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

Glyph baking uses the managed compiler by default (pure-C# TrueType parsing
plus a Burst-compiled SDF rasterizer — measured faster than the native
plugin); the native `nowui-msdf` plugin handles what the managed parser
declines: CFF-flavored OpenType and color emoji fonts.
`NowFontCompiler.forceNativeCompiler` and `forceManagedCompiler` pin a
backend for profiling. Managed output is a single-channel SDF, which renders
through the same shader and materials (the shader's `median(r, g, b)`
resolves it unchanged) at the cost of slightly rounded corners at extreme
magnification.

Text is shaped through HarfBuzz when the native plugin is present
(`Now.textShaping`, on by default): ligatures, kerning, and complex-script
forms, with measurement using the same shaped runs as drawing so layout stays
exact. Segments containing glyphs the font lacks — and platforms without the
plugin — automatically use the per-codepoint path, where font fallbacks
resolve missing characters. Shaped glyphs bake through the managed compiler,
so HarfBuzz is the only native dependency in the shaped path.

## Example Scenes And Scripts

Current example scripts live under `Assets/NowUI/Example`.

- `ShapedRectangles.cs`: stress-tests rectangle radius, outline, blur, and
  padding.
- `TextTests.cs`: exercises text rendering behavior.
- `NowGraphicExample.cs`: demonstrates UGUI mesh capture.
- `NowRenderTextureExample.cs`: renders NowUI into a `RenderTexture` and
  applies it to a scene `Renderer`.
- `NowWorldGraphicExample.cs`: renders a ray-interactive world-space label
  directly through a `MeshRenderer`.
- `NowPipelineOverlayExample.cs`: demonstrates an SRP overlay source for URP
  and HDRP wrappers.
- `MailClientMockup.cs`: demonstrates a larger immediate-mode layout with
  responsive panels, rows, labels, masks, and truncation.
