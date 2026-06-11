# Now-UI

Now-UI is an immediate-mode UI renderer for Unity. You call the drawing API
each frame and flush — no GameObject hierarchy, no retained UI tree — but the
toolbox is complete: batched rectangles, MSDF text with runtime font
compilation, a flexbox-style layout system, pointer/touch/gamepad interaction,
themes, and Lottie vector animation.

It renders through the built-in pipeline (`GL`/`Graphics.DrawMeshNow`), URP,
HDRP, a UGUI `CanvasRenderer`, a `RenderTexture`, or IMGUI — same drawing code
everywhere.

## Installation

Install as a UPM package via git URL (Window > Package Manager > `+` >
*Install package from git URL*):

```text
https://github.com/BlenMiner/NowUI.git?path=Assets/NowUI
```

Or clone the repository and open it as a Unity project to work on Now-UI
itself.

Requirements:

- Unity `6000.4` or newer
- Dependencies (installed automatically): Input System, UGUI, Collections

## Quick Start

Draw from a camera render callback, usually `OnPostRender` (built-in
pipeline). For URP/HDRP and UGUI, see
[Docs/RenderPipelines.md](Docs/RenderPipelines.md).

```csharp
using UnityEngine;
using NowUI;

public class OverlayExample : MonoBehaviour
{
    void OnPostRender()
    {
        Now.StartUI(NowUIScreen.recommendedUIScale);

        using (NowLayout.Area(NowUIScreen.safeArea))
        using (NowLayout.Vertical(new NowLayoutOptions().SetPadding(16).SetSpacing(8)))
        {
            NowLayout.Label("Hello Now-UI", 32).Draw();

            NowRect button = NowLayout.Rect(NowLayout.Size(160, 44));
            var state = NowUIInput.Interact("ok-button", button);

            Now.Rectangle(button)
                .SetColor(state.hovered ? Color.white : Color.gray)
                .SetRadius(8)
                .Draw();
        }

        Now.FlushUI();
    }
}
```

Free-form drawing works the same way without layout: rects are
`(x, y, width, height)` in UI units from the top-left.

```csharp
Now.Rectangle(new NowRect(20, 20, 260, 80))
    .SetColor(new Color(0, 0, 0, 0.8f))
    .SetRadius(10)
    .Draw();

Now.Text(new NowRect(36, 26, 220, 64))
    .SetFontSize(32)
    .SetColor(Color.white)
    .Draw("Score: 1200");

Now.Lottie(new NowRect(280, 20, 64, 64), spinnerAsset)
    .SetTime(Time.time)
    .Draw();
```

## Features

- **Rectangles** — rounded corners (per-corner radii), outlines, blur,
  padding, masks. [Docs/Features.md](Docs/Features.md)
- **Text** — MTSDF atlases compiled from TrueType fonts in the editor or at
  runtime from raw bytes; glyphs bake on demand into dynamic atlas pages;
  color emoji fonts (sbix, CBDT/CBLC, COLR/CPAL, SVG) import through
  FreeType; contextual font stack via `using (Now.Font(...))`.
- **Layout** — flexbox-style horizontal/vertical groups with fixed, min/max,
  and weighted-stretch sizing. [Docs/Layout.md](Docs/Layout.md)
- **Input** — immediate-mode `NowUIInput.Interact` with hover, press, drag,
  and click across mouse, touch, keyboard, and gamepad; pluggable providers
  for RenderTextures, tests, and remote input.
- **Lottie** — vector animations tessellated live on the CPU, never
  rasterized to textures. [Docs/Lottie.md](Docs/Lottie.md)
- **Themes** — ScriptableObject color/spacing/radius tokens and presets.
  [Docs/StylesAndThemes.md](Docs/StylesAndThemes.md)
- **Mobile** — density-scaled units (`Now.StartUI(uiScale)`), safe-area
  helpers, touch input, Android/iOS native plugins.
  [Docs/Mobile.md](Docs/Mobile.md)

## Platform support

Native plugins (the `nowui-msdf` font compiler and `nowui-vg` Lottie
tessellator) are prebuilt and committed under `Assets/NowUI/Plugins`:

| Platform | Font compilation | Lottie tessellation |
| --- | --- | --- |
| Windows x64 | native | native |
| Linux x64 | native | native |
| macOS x64 / arm64 | native | native |
| Android arm64-v8a | native | native |
| iOS arm64 | native (static) | native (static) |
| WebGL | native (static) | native (static) |
| Everything else (consoles, tvOS, ...) | managed fallback (Burst) | managed fallback |

Native plugins are a performance upgrade, never a requirement: on platforms
without binaries, fonts bake through a Burst-compiled managed SDF compiler and
Lottie falls back to a managed tessellator, so NowUI runs anywhere Unity does.
The managed font fallback covers TrueType (glyf) outlines; CFF-flavored
OpenType and color emoji fonts still need the native compiler. WebGL and iOS
link the plugins statically, so those files must be present at build time.

## Native plugin CI

A single workflow, `.github/workflows/build-native-libraries.yml`, builds both
plugins for every platform and commits the artifacts back to the repository.
It runs automatically when anything under `Assets/NowUI/Plugins/Native/`
changes and can also be dispatched manually with custom `msdf-atlas-gen` and
Emscripten versions. The WebGL artifacts are pinned to Emscripten `3.1.38` to
match Unity 6's WebGL toolchain; rebuild with a matching version if Unity
changes its bundled toolchain.

## Project Layout

- `Assets/NowUI` — the UPM package (runtime, editor, tests, examples, native
  plugin sources and binaries)
- `Assets/NowUI/Runtime` — drawing API, layout, input, text, Lottie, themes
- `Assets/NowUI/Editor` — font compiler menu, `.lottie` importer
- `Assets/NowUI/Plugins/Native` — native wrapper sources built by CI
- `Assets/NowUI/Example` — sample scripts, including `MailClientMockup`, a
  Gmail-like inbox drawn entirely with immediate NowUI calls
- `Docs` — feature guides

## Notes

- Call `Now.StartUI()` before drawing and `Now.FlushUI()` after. Draw order is
  preserved; switching materials flushes the active mesh.
- The hot path is allocation-free; mesh buffers start small and grow on
  demand, so the first heavy frame may allocate.
- Emoji sequence shaping (ZWJ families, skin tones, flags) needs a future
  HarfBuzz shaping layer; single-glyph emoji render today.

## License

Not yet licensed — all rights reserved while distribution (Asset Store or an
open-source license) is being decided. Third-party notices for the native
plugins are in
[Assets/NowUI/THIRD_PARTY_LICENSES.md](Assets/NowUI/THIRD_PARTY_LICENSES.md).
