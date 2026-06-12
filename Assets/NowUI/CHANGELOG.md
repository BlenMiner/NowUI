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
- GitHub-flavored Markdown extension (`NowUI.Extensions.Markdown`, its own
  assembly): headings, emphasis/strong/strikethrough, inline code, fenced
  code blocks with syntax highlighting (csharp/json/C-like), quotes,
  nested + task lists, pipe tables with alignment, links and autolinks,
  and images (downloaded asynchronously, aspect-scaled to width) — rendered
  through Now/NowLayout with theme colors, word wrap, width-cached layout
  (steady-state draws allocate nothing) and clickable links reported via
  `NowMarkdownResult`. No HTML, no JavaScript. See Docs/Markdown.md.
- Extension-author DX round (lessons from building the markdown extension):
  `NowTextWrap` brings word wrap to the core (layout once into positioned
  runs measured straight off the source string, draw for many frames);
  `theme.ResolveText(style)` resolves a themed text style with the ambient
  font and no rect/mask — the safe starting point for custom drawing;
  `MeasureText(string, start, length)` measures ranges without substring
  allocation; `NowUIInput.CombineId(a, b)` is the blessed sub-element id
  mint, and a new id-less `NowUIInput.Interact(rect)` overload derives
  identity from the call site with per-frame occurrence salting (loops over
  sub-elements need no ids at all); `NowLayout.ContentRect()` codifies the
  frame-late reserve-draw-measure-repaint pattern for content whose height
  depends on its width, with the last height stored per call site so the
  caller manages no state (`var c = NowLayout.ContentRect(); ...;
  c.End(measuredHeight);`).
- Image and sprite rendering on rectangles:
  `Now.Rectangle(rect).SetTexture(texture)` draws any texture with the full
  rectangle feature set (rounded corners, tint, outline, masks) in both
  render paths; `SetSprite(sprite)` resolves atlas sub-rects;
  `SetSprite(sprite, sliced: true)` draws a 9-slice from the sprite border
  (corners pixel-fixed, edges/center stretched, seam-free, collapsing
  gracefully when the rect is smaller than the borders); `SetUV(rect)`
  samples a sub-region; `SetPreserveAspect()` letterboxes instead of
  stretching.
- Context menus in the core: `NowUIContextMenu` (overlay-layer popup, one
  open at a time, closes on selection/outside-press/cancel) with an
  immediate-mode Open/Begin/Item/End API. `NowTextSelection` reports
  right-clicks (selection preserved) and gained `SelectAll`/`GetSelection`
  by id; markdown wires Copy and Select All onto right-click for both code
  blocks and paragraphs.
- Markdown paragraphs and headings are selectable, not just code blocks:
  styled words register as selection segments over a flattened plain text
  (copy gives readable text with spaces and line breaks), with hit testing
  resolving segments by row and x, and highlights bridging the gaps
  between words.
- Text selection in the core: `NowTextSelection` gives any text region
  browser-style selection — press/drag selects, double-click selects a
  word, Ctrl/Cmd+A selects all, Ctrl/Cmd+C copies (replaceable handler) —
  over caller-positioned lines, with focus integration so clicking
  elsewhere deselects, plus `NowTextEdit.SelectWord`. Markdown code blocks
  are selectable with it, like a website: drag to copy parts, the Copy
  button still takes the whole block.
- Markdown: multi-word links behave as one link — all words share a single
  interaction (press one word, release on another, still a click), hovering
  any word highlights the whole link, and underline/strikethrough
  decorations merge across spaces into continuous lines (separate
  strikethroughs never bridge).
- Markdown: code blocks have a Copy button (hover-tinted, "Copied!"
  feedback; handler replaceable via `NowMarkdownDocument.copyToClipboard`),
  images inside links are clickable, and non-http image paths load from
  `Resources` so bundled art works offline.
- Span text APIs for zero-GC dynamic text: `NowUIText.Draw(ReadOnlySpan<char>)`
  and `Measure(ReadOnlySpan<char>)` (plus `NowFontAsset.MeasureText` span
  overload) — format counters/timers into a reusable char buffer and draw
  the span; no string is ever created. Span draws use the per-codepoint
  path (shaping is keyed by string). The zoo's FPS counter demonstrates
  the pattern.
- Dropdowns no longer allocate every frame while closed: the popup's
  deferred closure lived in Draw's scope, so its display class was
  allocated at method entry even on the early-return path; it now lives in
  a method that only runs while the popup is open.
- Per-frame GC eliminated in the text pipeline: the shaped draw and
  measure paths copied the string (`Substring`) once or twice per call
  even for single-segment text — the common case now passes the original
  string through untouched. Constants are truly zero-alloc end to end.
- Fully masked content is culled before the work, not after: text scrolled
  out of a viewport skips shaping lookups, glyph resolution and advance
  math entirely, and fully masked Lottie draws skip tessellation. Hidden
  scroll content now costs ~nothing instead of "everything but the quads".
- Profiler instrumentation: `NowUIProfiler` exposes "NowUI.*"
  `ProfilerMarker`s around every subsystem — graphic rebuild, measure pass
  vs draw pass, mesh upload, CanvasRenderer page assignment, per-string
  text drawing, HarfBuzz shaping, glyph baking, Lottie tessellation,
  overlay flush, and the screen path's GL submission. Markers compile out
  of non-development builds. Also removed the last per-frame string
  allocation in the library (dropdown popup item ids).
- `NowUIGraphic` implements `ILayoutElement`: with Drive Layout Size (on by
  default) it reports the measured NowLayout content extent as its
  preferred size, so it participates in UGUI LayoutGroups and
  ContentSizeFitters like any built-in graphic. Frame-late, like all
  NowLayout measurement; `measuredContentSize` exposes the value.
- Control interaction polish: clicking empty space clears focus; text
  fields lock spatial navigation while editing (arrows move the caret and
  WASD types instead of moving focus), reset the caret blink on every
  caret move so a moving caret stays visible, draw a full-height caret,
  and support Ctrl+Backspace / Ctrl+Delete word deletion
  (`NowTextEdit.Backspace`/`Delete` gained a word flag). Scroll wheel
  ticks are normalized (Windows reports ±120 per notch through the input
  system, which made scroll views jump to the edges), and the scrollbar's
  whole track is now the grab target with jump-to-click. Rectangle pixel
  snapping rounds edges instead of truncating origin and size, which had
  been shifting nested glyphs (radio dots, checkmarks) visibly off-center
  at fractional layout positions.
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
