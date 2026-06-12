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
- An empty mask now means "no mask": styles built from a default rect carried
  a zero-size mask that clipped everything they drew (text fields' content
  was invisible). Ambient `Now.Mask` scopes still clip such draws normally.
- Missing bold/italic font variants now fall back to the regular face
  instead of rendering nothing (`SetBold` on a single-face font previously
  produced invisible text).
- Thin rectangle outlines render solid: an outline narrower than one
  anti-aliasing width used to sit entirely inside the edge fade and came out
  as a washed-out, corner-glitchy sliver; the shader now draws at least one
  AA width of it.
- Default masks now leave breathing room: a rectangle/text/Lottie draw whose
  mask was never set explicitly (it defaults to the element's own rect) is
  outset by the SDF falloff — plus blur/outline for rectangles, glyph
  overhang for text — so anti-aliasing is no longer clipped hard at the
  bounds. Explicit `SetMask` rects stay exact.
- Fixed control labels drawn through `NowControls.DrawLeftLabel` (checkbox,
  radio, dropdown values, popup items) rendering nothing: the style carried
  a zero-size mask from its default-rect construction, which clips
  everything. They now clip to the label area.
- Zoo example (`NowUIZooExample`): one component exercising every feature —
  styled buttons, content scopes, toggles, radios, sliders, text fields,
  dropdowns, a scroll-view event log, theme swatches, Lottie, masks, and
  align-items.
- Automatic control identity from the call site: the control factories
  capture `[CallerFilePath]`/`[CallerLineNumber]`, so every textual call
  site is its own control and labels are purely visual — duplicate labels
  never collide, renaming a label keeps state, sliders need no manual ids,
  and `Begin()` content scopes need no identity string at all
  (`NowLayout.Button().Begin()`). Loop iterations over one site are salted
  by per-frame draw order; `SetId`, the optional string ids on
  TextField/Dropdown/ScrollView, and `NowControls.IdScope` anchor identity
  to data when items can reorder or one control draws from several code
  paths. Custom controls opt in via `NowControls.SiteId` +
  `NowControls.GetControlId(int)`. Ids are session-scoped and never
  persisted.
- Group-level cross-axis alignment: `NowLayoutOptions.SetAlignItems`
  (flexbox `align-items`) sets the default alignment for a group's children,
  with per-child `SetAlign` overriding; exposed on the `Begin()` controls
  (`NowLayout.Button("id").SetAlignItems(NowLayoutAlign.Center).Begin()`)
  to vertically center mixed-height content. Also fixed auto-sized
  containers measuring a cross-stretched child group's allocation instead
  of its actual content, which locked `Begin()` controls at their
  first-frame width.
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
