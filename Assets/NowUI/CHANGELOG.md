# Changelog

All notable changes to the NowUI package are documented here. The format is
based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-06-11

First versioned release; everything below is the state of the package at the
point it became installable through UPM.

### Added

- Code editor line shortcuts and key repeat: Ctrl+D duplicates the current
  line (or selected lines), and Ctrl+C / Ctrl+X with no selection copy / cut
  the whole line. Newlines and Tab now repeat while held, like characters
  (the text input frame gained `enterHeld` / `tabHeld`, plus a
  `duplicatePressed` chord). TextArea's Enter repeats while held too.
- Code editor rendering: rounded corners no longer fight the gutter and
  status bar. The editor now draws a rounded surface fill, gives the gutter
  and status bar matching rounded outer corners, and lays the outline border
  on top so every inner seam is covered. Draggable scrollbars (vertical and
  horizontal) appear when the content overflows, with thumbs sized to the
  viewport; dragging a thumb scrolls without moving the caret.
- Immediate-mode drawing API (`Now.Rectangle`, `Now.Text`, `Now.Lottie`) with
  per-material mesh batching.
- MSDF text rendering with dynamic atlas pages, runtime font compilation, a
  contextual font stack (`Now.Font`), and color-font (emoji) import.
- Flexbox-style layout system (`NowLayout`) with deferred and same-frame
  measurement modes.
- Pointer, touch, keyboard, and gamepad input with an immediate-mode
  interaction API (`NowInput.Interact`).
- Lottie vector animation playback with a native tessellator and managed
  fallback.
- UGUI integration (`NowGraphic`), render-pipeline integration for URP and
  HDRP, and RenderTexture/IMGUI bridges.
- Theme tokens and presets (`NowTheme`).
- UI scaling for high-DPI displays (`Now.StartUI(float uiScale)`,
  `NowScreen.recommendedUIScale`) and safe-area helpers
  (`NowScreen.safeArea`).
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
  keyboard/gamepad focus (`NowFocus`: spatial navigation,
  submit activation), per-control ephemeral state and timing helpers
  (`NowControlState`), id scopes, ambient clipping (`Now.Mask`), an
  overlay layer with input occlusion (`NowOverlay`), frame-sampled text
  editing input (`NowTextInput`) and a headless single-line editing
  engine (`NowTextEdit`, surrogate-safe, shaped-cluster caret hit-testing).
  Custom controls build on the same public primitives — see
  Docs/Controls.md. UGUI hosting is first-class: `NowGraphic`
  auto-rebuilds while hovered or when a control requests a repaint,
  staying fully retained otherwise.
- Multi-line text editing: `NowLayout.TextArea()` / `Now.TextArea(rect)` with
  modern editor ergonomics — word wrap that preserves every character (words
  wider than the area hard-split), caret up/down with a pixel goal column,
  Home/End per visual line and Ctrl+Home/End per document, shift-selection on
  every movement, click/drag and double-click word selection, Enter inserts a
  newline while Escape blurs, Ctrl+Backspace/Delete word deletion, multi-line
  copy/cut/paste (CRLF normalized) through `NowClipboard`, height that
  grows with content between `SetLines(min, max)` plus scroll-to-caret, wheel
  scrolling and a slim scroll indicator, and the multiline on-screen keyboard
  on mobile. The line layout (`NowTextArea.LayoutLines`) is public for custom
  editors, and the input frame gained up/down arrow keys.
- Code editor extension (`NowUI.Extensions.CodeEditor`, its own assembly):
  an embeddable editor with syntax highlighting (line tokenizers with
  cross-line state), validation squiggles with hover tooltips and a status
  bar whose error message jumps the caret on click, auto-closing pairs
  (insert/skip-over/wrap-selection/backspace-both), Enter auto-indent with
  brace expansion, Tab indent / Shift+Tab dedent (multi-line with a
  selection), smart Home, undo/redo with typing coalescing, line numbers,
  current-line highlight, two-axis scrolling with caret-into-view, and the
  standard selection/clipboard/IME/focus conventions. Ships with a JSON
  profile (full validating parser with positioned human messages) and a
  markdown-source profile that delegates fenced blocks to the registered
  language of their info string — one system, two languages, extensible via
  `NowCodeLanguage.Register`. The text input frame gained Tab and
  undo/redo keys. See Docs/CodeEditor.md and the docs browser's
  "Code editor" page.
- Layout groups and rects take their common settings as optional parameters:
  `NowLayout.Horizontal(spacing: 8, alignItems: NowLayoutAlign.Center)`,
  `Vertical(spacing: 10, stretchWidth: true)`,
  `Area(rect, padding: 16, spacing: 8)` (scope, callback and id-keyed forms)
  and `Rect(height: 22, stretchWidth: true)` — no more
  `new NowLayoutOptions().Set...` chains inside `using` headers. The
  `NowLayoutOptions` overloads remain for everything beyond the common set.
- Measure passes resolve the same control ids as the real pass: occurrence
  salting now counts in a per-pass table instead of being skipped while
  passive, so loop-salted controls and `NowLayout.ContentRect` reservations
  no longer collide during layout measurement. `NowMarkdown.Document(string).Draw()`
  also takes identity from its caller, so several markdown blocks can
  interleave with other layout content (this is what the docs browser's
  live demo page does).
- Custom-controls guide (Docs/CustomControls.md): restyle via themes, wrap
  variants (forwarding caller info so identity stays per call site), reshape
  with custom hit shapes on the standard interaction bundle, and build from
  scratch — plus a docs browser scene (`Assets/Scenes/DocsScene.unity`) that
  renders the Docs folder through the markdown extension with a side menu,
  relative-link navigation, a live demo page running the guide's code, and
  a Lottie demo page (gallery, scrubbing via `SetNormalizedTime`, sizes,
  tinting) fed by an inspector-assignable asset array.
- IME composition in TextField and TextArea: the pre-edit text renders
  inline at the caret (underlined) without touching the value, editing keys
  belong to the IME until commit, and committed characters insert normally.
  The IME is enabled on focus and the caret position is reported for the
  candidate window each frame (`NowTextInput.setImeEnabled` /
  `setCompositionCursor` are replaceable for non-screen hosts). Both
  keyboard backends feed `NowTextInputFrame.composition`.
- Triple-click selects a line: in TextArea (the hard, newline-delimited
  line) and in `NowTextSelection` regions including markdown documents.
  Built on `NowControlState.ClickStreak` (consecutive-click counter) and
  `NowTextEdit.SelectLine`, both public for custom controls.
- Mutual UGUI pointer occlusion: UGUI drawn above NowUI now blocks NowUI
  hovers and clicks (EventSystem raycast gating in both the UGUI-hosted and
  screen input providers, drags preserved), completing the existing
  raycastTarget blocking in the other direction. EventSystem selection and
  NowUI focus are also mutually exclusive (`NowFocus.respectEventSystem`).
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
  allocation; `NowInput.CombineId(a, b)` is the blessed sub-element id
  mint, and a new id-less `NowInput.Interact(rect)` overload derives
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
- Context menus in the core: `NowContextMenu` (overlay-layer popup, one
  open at a time, closes on selection/outside-press/cancel) with an
  immediate-mode Open/Begin/Item/End API. Open menus are modal: everything
  beneath is pointer-blocked (no hover, clicks, or wheel — the anchor
  position stays meaningful) and an attempted scroll dismisses the menu,
  matching browser behavior. `NowTextSelection` reports
  right-clicks (selection preserved) and gained `SelectAll`/`GetSelection`
  by id; markdown wires Copy and Select All onto right-click for both code
  blocks and paragraphs.
- Right-clicking a markdown image offers "Copy image address" (Unity has
  no managed image-clipboard API, so a bitmap copy that other programs
  could paste is not possible — no fake affordance is shown). Image rects
  are excluded from text-selection presses.
- One clipboard hook: `NowClipboard` (`setText`/`getText`, default
  system clipboard) now backs every copy/paste path — selection Ctrl+C,
  text field copy/cut/paste, markdown copy buttons and context menus.
  Replace it once per platform and everything follows; the previously
  environment-skipped clipboard round-trip test is now deterministic
  through it.
- Document-wide selection: dragging selects across every selectable block
  — paragraphs, headings and code blocks in one sweep, like a webpage —
  over a document-flattened text (blocks separated by blank lines).
  `NowTextSelection` splits into `Interact` (one input pass for the whole
  document, exclusion-rect list for copy buttons) and `DrawHighlights`
  (per-region slices so highlights layer between panel fills and text),
  with per-segment font sizes so headings and code hit-test exactly.
  Right-click menus act on the document selection; Copy only appears when
  something is selected. Also fixed a stale overlay block: queries roll
  the block registry forward, so a closed context menu releases the
  pointer (scroll/hover no longer stay dead).
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
- Span text APIs for zero-GC dynamic text: `NowText.Draw(ReadOnlySpan<char>)`
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
- Profiler instrumentation: `NowProfiler` exposes "NowUI.*"
  `ProfilerMarker`s around every subsystem — graphic rebuild, measure pass
  vs draw pass, mesh upload, CanvasRenderer page assignment, per-string
  text drawing, HarfBuzz shaping, glyph baking, Lottie tessellation,
  overlay flush, and the screen path's GL submission. Markers compile out
  of non-development builds. Also removed the last per-frame string
  allocation in the library (dropdown popup item ids).
- `NowGraphic` implements `ILayoutElement`: with Drive Layout Size (on by
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
- Zoo example (`NowZooExample`): one component exercising every feature —
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
