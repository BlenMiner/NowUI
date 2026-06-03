# Now-UI

Now-UI is a lightweight immediate-mode UI renderer for Unity. It batches
rectangles and MSDF text into meshes and draws them during camera rendering with
`GL`/`Graphics.DrawMeshNow`.

The project is intentionally small: no GameObject hierarchy, no layout engine,
and no retained UI tree. You call the drawing API each frame, then flush.

## Requirements

- Unity `6000.4.0f1`
- Built-in render pipeline APIs used by `GL` and `Graphics.DrawMeshNow`
- The bundled `Assets/NowUI/Resources/NowUI` materials

## Quick Start

Draw from a camera render callback, usually `OnPostRender`.

```csharp
using UnityEngine;

public class OverlayExample : MonoBehaviour
{
    [SerializeField] NowFont font;

    void OnPostRender()
    {
        NowUI.StartUI();

        NowUI.Rectangle(new Vector4(20, 20, 260, 80))
            .SetColor(new Color(0, 0, 0, 0.8f))
            .SetRadius(10)
            .Draw();

        NowUI.Text(new Vector4(36, 26, 220, 64), font)
            .SetFontSize(32)
            .SetColor(Color.white)
            .Draw("Hello Now-UI");

        NowUI.FlushUI();
    }
}
```

Rectangles use `Vector4(x, y, width, height)` in screen pixels. The origin is the
top-left of the current screen mask.

## Rectangles

```csharp
NowUI.Rectangle(new Vector4(10, 10, 200, 120))
    .SetColor(Color.white)
    .SetRadius(8)
    .SetOutline(2)
    .SetOutlineColor(Color.black)
    .SetBlur(4)
    .Draw();
```

Useful rectangle options:

- `SetColor(Color)` or `SetColor(Vector4)`
- `SetRadius(float)`
- `SetPadding(float)`
- `SetOutline(float)`
- `SetOutlineColor(Color)` or `SetOutlineColor(Vector4)`
- `SetBlur(float)`
- `SetMask(Vector4)`

## Text

Text rendering uses `NowFont` instances generated from TrueType font data.

```csharp
NowUI.Text(new Vector4(24, 24, 300, 60), font)
    .SetFontSize(42)
    .SetColor(Color.white)
    .SetOutline(1)
    .SetOutlineColor(Color.black)
    .Draw("Score: 1200");
```

Tabs advance by four spaces. Newlines reset x to the starting position and move
down by the font line height.

Strings are read as Unicode code points, so supplementary-plane glyphs such as
simple emoji are not split into UTF-16 surrogate halves. The compiler detects
color font tables such as `sbix`, `CBDT`/`CBLC`, `COLR`/`CPAL`, and `SVG `.
Outline fonts are imported as MTSDF atlases; color fonts are imported through the
native FreeType color-glyph path into an RGBA atlas. Color font imports are
filtered to the requested characters by default because importing every emoji in
a large color font can produce a very large RGBA atlas.

The color path imports individual glyphs exposed by the font cmap. Emoji
sequence shaping, such as ZWJ families, skin tone composition, and flags, will
need the next HarfBuzz shaping layer.

Use `NowFont.MeasureText(text, fontSize)` or `NowUIText.Measure(text)` when a
layout needs advance-based size for reserving space or truncating labels. Use
`NowFont.MeasureTextBounds(text, fontSize)` or `NowUIText.MeasureBounds(text)`
when a mask or alignment needs the actual drawn glyph bounds.

## UGUI

`NowUIGraphic` renders NowUI draw calls into a UGUI `CanvasRenderer`. Add a
component derived from `NowUIGraphic` to a `RectTransform` and override
`DrawNowUI(Rect rect)`:

```csharp
public class MyPanel : NowUIGraphic
{
    [SerializeField] NowFont font;

    protected override void DrawNowUI(Rect rect)
    {
        Vector4 bounds = new Vector4(0, 0, rect.width, rect.height);

        NowUI.Rectangle(bounds)
            .SetColor(Color.black)
            .SetRadius(8)
            .Draw();

        NowUI.Text(new Vector4(16, 12, rect.width - 32, 32), font)
            .SetFontSize(18)
            .SetColor(Color.white)
            .SetMask(bounds)
            .Draw("UGUI NowUI");
    }
}
```

The graphic handles mesh capture internally; do not call `NowUI.StartUI()` or
`NowUI.FlushUI()` inside `DrawNowUI`. Call `MarkDirty()` when retained state
changes, or enable `Rebuild Every Frame` for animated graphics.

## Compiling Fonts

### Editor Assets

1. Select one or more `.ttf` font assets in Unity.
2. Run `Assets > NowUI > Compile Font`.
3. A `.asset` file is generated next to each selected font.
4. Assign the generated `NowFont` asset to scripts that draw text.

The compiler creates a source-only `NowFont` asset. It embeds the source font
bytes directly in the asset, hides those bytes from the inspector, and does not
create a `Font Atlas Texture` or material subasset. The original `.ttf` is not
referenced by the generated font, so it can be excluded from builds after
compilation.

### Runtime Fonts

Fonts can also be compiled from runtime byte arrays without writing atlas files:

```csharp
byte[] fontBytes = GetFontBytes();

if (!NowFontCompiler.TryCompile(fontBytes, out NowFont font, out string error))
    Debug.LogError(error);
```

Generated and runtime-created `NowFont` instances store source font bytes
directly in the `NowFont` asset/object and resolve every glyph on demand into
dynamic atlas pages. Pages are filled until they exceed the configured atlas cap,
then a new page/material is created.

When text later references a missing glyph, NowUI compiles it into an existing
dynamic page when it fits or creates another page when the current page is full.
This keeps color emoji fonts from importing the full glyph set up front.

This path calls `nowui_compile_font_from_memory_with_codepoints` or
`nowui_compile_color_font_from_memory_with_codepoints` only when glyphs are
requested. In WebGL builds, Unity links
`Assets/NowUI/Plugins/WebGL/nowui-msdf.bc` into the generated WebAssembly module
and the C# binding uses `__Internal`.

## Native Compiler Artifacts

The repository includes a manual GitHub Actions workflow for building the
Unity-facing `nowui-msdf` native font compiler plugin, plus its
`msdf-atlas-gen` sidecar libraries:

```text
.github/workflows/build-msdf-atlas-gen-libraries.yml
```

Run `Build MSDF Atlas Gen Libraries` from the Actions tab to produce Windows
x64, Linux x64, macOS x64, macOS arm64, and WebGL artifacts. Each artifact
contains an `Assets/NowUI/Plugins/...` folder intended to be merged into the
project. The WebGL artifact contains a single merged Emscripten bitcode library:

```text
Assets/NowUI/Plugins/WebGL/nowui-msdf.bc
```

The workflow defaults to Emscripten `3.1.38`, matching Unity 2023.2+/Unity 6
WebGL toolchains. Rebuild the WebGL artifact with the Emscripten version that
matches your Unity editor if Unity changes its bundled toolchain.

## Project Layout

- `Assets/NowUI/Runtime`: runtime drawing API, mesh buffers, font assets
- `Assets/NowUI/Editor`: font compiler menu item and native compiler interop
- `Assets/NowUI/Plugins/Native`: native wrapper source used by the CI workflow
- `Assets/NowUI/Plugins`: native compiler source and libraries for editor/player platforms
- `Assets/NowUI/Shaders`: rectangle and text shaders
- `Assets/NowUI/Resources`: default materials and compiler resources
- `Assets/NowUI/Example`: sample scenes/scripts and compiled example fonts
- `Assets/NowUI/Tests`: edit-mode tests for low-level runtime behavior

The examples include `MailClientMockup`, a Gmail-like inbox layout that draws a
toolbar, sidebar, message list, and reader pane using immediate NowUI calls.
`NowUIGraphicExample` shows the same drawing API rendered through UGUI.

## Notes

- Call `NowUI.StartUI()` before drawing and `NowUI.FlushUI()` after drawing.
- Draw order is preserved. Switching materials flushes the active mesh.
- Mesh buffers start small and grow on demand, so large UI bursts may allocate
  during the first heavy frame.
- This is not a replacement for Unity UI Toolkit or UGUI; it is a compact
  immediate renderer for overlays, debug UI, and custom lightweight tools.

## License

See `LICENSE`.
