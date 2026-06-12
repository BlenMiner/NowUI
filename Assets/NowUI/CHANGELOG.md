# Changelog

All notable changes to the NowUI package are documented here. The format is
based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-06-11

First versioned release; everything below is the state of the package at the
point it became installable through UPM.

### Added

- Immediate-mode drawing API (`Now.Rectangle`, `Now.Text`, `Now.Lottie`) with
  per-material mesh batching.
- MSDF text rendering with dynamic atlas pages, runtime font compilation, a
  contextual font stack (`Now.Font`), and color-font (emoji) import.
- Flexbox-style layout system (`NowLayout`) with deferred and same-frame
  measurement modes.
- Pointer, touch, keyboard, and gamepad input with an immediate-mode
  interaction API (`NowUIInput.Interact`).
- Lottie vector animation playback with a native tessellator and managed
  fallback.
- UGUI integration (`NowUIGraphic`), render-pipeline integration for URP and
  HDRP, and RenderTexture/IMGUI bridges.
- Theme tokens and presets (`NowUITheme`).
- UI scaling for high-DPI displays (`Now.StartUI(float uiScale)`,
  `NowUIScreen.recommendedUIScale`) and safe-area helpers
  (`NowUIScreen.safeArea`).
- Native plugins (font compiler + vector tessellator) for Windows x64,
  Linux x64, macOS x64/arm64, WebGL, Android arm64-v8a, and iOS arm64, built
  by a single auto-triggering CI workflow.
- Managed font compiler fallback: a pure-C# TrueType parser plus a
  Burst-compiled SDF baker that activates automatically on platforms without
  the native plugin (and via `NowFontCompiler.forceManagedCompiler`), so text
  renders on every Unity platform. TrueType outlines only; CFF and color
  emoji fonts still require the native compiler.
- Burst-compiled managed Lottie tessellation: fills and strokes on the
  managed fallback path run as Burst jobs with output identical to the
  scalar tessellator (verified element-by-element in tests). Trim paths and
  matte-clipped strokes keep the scalar route.
- Play-mode rendering test suite reading back real rendered pixels for
  rectangles, text (both font compilers), Lottie, and the GL camera path.
- Controls library: Button, Checkbox, Radio, Slider, TextField, Dropdown and
  ScrollView in the fluent builder style — `NowLayout.*` for layout flow,
  `Now.*` for explicit rects, and `Begin()` content scopes on Button,
  Checkbox and Radio for custom content (icons, sub-labels) with results
  readable inside the scope — theme-integrated with enum styles
  (`NowRectangleStyle`/`NowTextStyle`/`NowColorToken`/...; string preset ids
  remain the low-level theme layer), with
  keyboard/gamepad focus (`NowUIFocus`: spatial navigation,
  submit activation), per-control ephemeral state and timing helpers
  (`NowUIControlState`), id scopes, ambient clipping (`Now.Mask`), an
  overlay layer with input occlusion (`NowUIOverlay`), frame-sampled text
  editing input (`NowUITextInput`) and a headless single-line editing
  engine (`NowTextEdit`, surrogate-safe, shaped-cluster caret hit-testing).
  Custom controls build on the same public primitives — see
  Docs/Controls.md. UGUI hosting is first-class: `NowUIGraphic`
  auto-rebuilds while hovered or when a control requests a repaint,
  staying fully retained otherwise.
- Mutual UGUI pointer occlusion: UGUI drawn above NowUI now blocks NowUI
  hovers and clicks (EventSystem raycast gating in both the UGUI-hosted and
  screen input providers, drags preserved), completing the existing
  raycastTarget blocking in the other direction. EventSystem selection and
  NowUI focus are also mutually exclusive (`NowUIFocus.respectEventSystem`).
- Bundled Roslyn analyzer (`Runtime/Analyzers/NowUI.Analyzers.dll`, applies
  to every assembly referencing NowUI): NOWUI001 warns when a builder is
  discarded as a bare statement (`NowLayout.Label("Hi");` without `.Draw()`),
  NOWUI002 warns when a using-only scope (`Now.Mask`, `Begin()` scopes, ...)
  is discarded and can therefore never be disposed. Both rules are exact —
  no heuristics — and attribute-driven (`[NowBuilder]`, `[NowConsumer]`,
  `[NowScope]`) so custom controls get the same checks. Sources in
  `Assets/NowUI/Analyzers~`.
- HarfBuzz text shaping (`Now.textShaping`, on by default): ligatures,
  kerning, and complex-script forms through the nowui-msdf v4 plugin, with
  shaped glyphs baked by the managed compiler. Measurement uses the same
  shaped runs as drawing. Segments the shaper cannot fully cover (missing
  glyphs, color emoji, platforms without the plugin) automatically render
  through the per-codepoint path with font fallbacks.
