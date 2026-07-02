# Changelog

All notable changes to the NowUI package are documented here. The format is
based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- **Project-wide DX/UX/performance pass.** Text editing follows platform
  conventions: shift-click extends the selection, text fields and areas gain
  undo/redo, Escape reverts a text field to its focus-time value while Enter
  commits, placeholders stay visible while an empty field is focused, AltGr
  characters are no longer swallowed on Windows/Linux layouts, and macOS uses
  Option/Cmd word- and line-navigation (Ctrl on Windows/Linux, as before).
  Popups behave like macOS menus: the press that dismisses a dropdown-family
  popup is consumed instead of activating the control beneath, Esc closes
  exactly one layer when popups nest, dropdowns are fully keyboard-navigable
  (arrows, Return, type-to-select), date pickers support arrow-key day
  navigation, popups clamp to short views and scroll, the combo box filter
  stays typable while its popup is open, and a key-binding capture cancelled
  with Esc no longer also closes the enclosing popup. Time picker edits
  preserve seconds; split-view dividers keep the grab offset. Steady-state
  per-frame allocations were removed across the runtime (multi-line text
  segmentation, numeric field formatting, gradient/curve fields, markdown
  with styles, rich text layout/hit-testing, markup rendering, docking ids,
  glass batch keys), scroll input is normalized across UI Toolkit/IMGUI
  hosts, the drag threshold scales with DPI on touch devices, and
  `NowInput.navigationKeys` can disable WASD/space/tab/enter UI navigation
  for games that need those keys. Scroll views expose `scrollOffset` /
  `ScrollToEnd()`; `NowChip.Draw` now reports removal only via its out
  parameter; effect contexts take caller-passed time via `SetTime` (no hidden
  clock); `NowCornerRadius.FromPacked` round-trips correctly.

- **Full visual redesign of the default theme.** New slate-neutral light and
  dark palettes (WCAG-contrast checked), a typographic scale
  (`Display`/`Heading`/`Subheading`/`BodyStrong`/`Label`/`Caption` join the
  existing text styles, with per-preset font weight), retuned spacing/radius
  scales, and a two-layer elevation shadow system
  (`NowElevationToken.Raised/Overlay/Modal`). The default control renderer
  absorbs the former HeroUI polish: soft shadows, line-drawn chevrons and
  check marks, offset focus rings, pressed scale, and token-driven
  hover/pressed states that stay visible on dark surfaces.
- `NowColorToken` grows from 8 to 27 roles: surface/accent state variants,
  `SurfaceElevated`, `BorderStrong`, `FocusRing`, `Success`/`Warning`/`Danger`
  triads, `Shadow` and `Scrim`. Themes authored against the old 8 colors keep
  working — missing roles derive automatically on load, and
  `NowThemeAsset.MigrateDerivedRoles()`/`RegenerateDerivedRoles()` bake them.
  Color lookups now hit a flat cache instead of a switch.
- `NowRectangleStyle` gains `Elevated`, `AccentSoft`, `Danger`, `Ghost`.
- Built-in themes are now `Default`/`DefaultDark` and
  `Material`/`MaterialDark`, cross-linked as light/dark counterparts;
  `NowTheme.preferDark = systemDarkMode` switches the whole UI. The theme
  generator writes the full extended role set.
- Wheel scrolling a text area now works over its padding, not just the inner
  text rect.
- Glass backdrop render-texture and derived-material lifecycles are shared
  between the UGUI replay path and the world camera-backdrop path
  (`NowGlassBackdropSurface`), so the RT descriptor and play/edit destroy
  handling cannot drift apart. The capture pipelines stay per-host.

- Input internals reorganized into `Runtime/Input/`: providers
  (`NowScreenInputProvider`, `NowIMGUIInputProvider`,
  `NowRectTransformInputProvider`, `NowWorldInputProvider`,
  `NowUIToolkitInputProvider`) are now standalone files instead of being
  embedded in `NowInput.cs`/`NowWorldGraphic.cs`/`NowVisualElement.cs`.
  Namespaces and type names are unchanged.
- `NowGraphic` and `NowWorldGraphic` share one interaction-repaint watcher
  (`NowInteractionRepaintTracker`) instead of duplicating input-change
  tracking per host.
- UGUI raycast-gate queries are cached per frame and pointer position: many
  NowUI hosts in a scene now cost one `EventSystem.RaycastAll` per frame
  instead of one per host.

### Fixed

- Per-corner radii now round the corners they name. `NowCornerRadius.packed`
  was vertically flipped against the shader's corner decode, so
  `NowCornerRadius.Top(...)` rounded the bottom corners (node title bars,
  file picker headers, and the context menu scroll strips all drew mirrored).
  Code that unpacked a packed radius `Vector4` by component must switch to
  the new order: `x`=topRight, `y`=bottomRight, `z`=topLeft, `w`=bottomLeft.
- Deferred overlay draws (context menus, dropdown popups, tooltips, dialogs)
  now capture the ambient theme scope at declare time and re-apply it when
  the overlay queue flushes. Previously popups rendered with whatever theme
  was active at end-of-frame — outside every `NowTheme.Scope`, so scoped
  themes never styled them.
- The built-in fallback light theme (used when no theme asset is scoped or
  assigned) is now fully initialized; it previously shipped blank style
  constants, leaving popups square-cornered and unpadded.
- Context menu scroll strips are drawn as the popup's own rounded shape
  clipped to the edge band, so they follow the menu's rounded silhouette
  exactly instead of overpainting the corners with a square rect (a plain
  strip rect cannot round correctly once the corner radius exceeds half the
  strip height).
- A context menu now belongs to the input surface that opened it: two
  surfaces sharing a menu id no longer both draw the menu (and the wrong one
  can no longer consume the item click).
- Context menus opened from inside another popup (the animation curve
  editor's tangent menu, dropdowns in dialogs) now win the pointer over the
  popup content beneath them: modal blocks apply between overlay layers, with
  only the modal's own overlay subtree exempt. Previously all overlay content
  bypassed pointer blocks, so the popup underneath kept claiming presses —
  menu items would highlight but clicks fell through (e.g. double-click added
  a curve key instead of picking a tangent option).
- Context menu submenus no longer snap shut when the pointer crosses a
  sibling row diagonally on its way into the submenu: switching away from an
  open submenu now waits for a short hover-intent delay (timed from the input
  snapshot, no hidden clock).
- World-space popups fitted to the camera view stay interactive beyond their
  surface rect: the ray-to-surface resolver and the world input provider now
  treat a surface's own overlays as part of its hit area.
- Overlay pointer blocks are scoped to the host that registered them. A
  context menu on one world nameplate no longer freezes every other NowUI
  surface (their local coordinates were being compared against foreign block
  rects); each surface is modal only to itself.
### Removed

- `NowControls.StateTint` (brightness-multiplier hover/press states —
  invisible on dark surfaces). Use `NowControls.StateColor` or the explicit
  `SurfaceHover`/`AccentHover`/`*Pressed` tokens.
- `NowHeroUIControlRenderer` and the `White`/`Dark`/`Night`/`HeroUI`/
  `HeroUIDark` theme assets: the HeroUI look is now the built-in default.
- Legacy Input Manager fallback. The Input System package has always been a
  hard dependency of NowUI; the dead `ENABLE_LEGACY_INPUT_MANAGER` code path
  (used only when the Input System reported zero devices) is gone. Projects
  with Active Input Handling set to "Both" and no Input System devices must
  switch to the Input System.

### Added

- **Scrollable context menus.** Menus taller than the visible view clamp
  their height and scroll — mouse wheel, or OS-style top/bottom hover strips —
  so every option stays reachable instead of overflowing the viewport.
  Submenus clamp and scroll independently, anchor to their scrolled row, and
  close when their row scrolls out of view. Scrolling over an open menu
  scrolls it; scrolling elsewhere still closes it. World-space hosts clamp
  against the popup's screen-space projection, so tilted surfaces clamp
  correctly (`INowPopupFitProvider.ClampPopupRectToView`,
  `NowOverlay.ClampScreenToView`).
- Menu Lab pages for hands-on edge-case testing: a "Menus" tab in the control
  gallery example (60-item playground menu, 80-item submenu, deep nesting,
  screen-corner and 100-item menus) and a right-click stress menu on the
  world graphic example (40 overflow options, 50-item submenu, deep chain).

- **Central pointer-ownership arbitration** (`NowPointerArbiter`): every input
  surface (screen path, canvas graphics, world graphics, UI Toolkit hosts)
  registers a per-frame claim — layering tier, depth, whether it has content
  under the pointer, and held buttons — and exactly one surface owns the
  pointer, resolved one frame late so the result never depends on host update
  order. Ownership latches during drags. Surfaces that never claim (single-
  surface apps, custom providers, tests) keep today's behavior. This replaces
  the tick-order-dependent behavior that let clicks land on surfaces behind
  popups and menus.

- **A wave of new controls**, all following the standard dual
  `Now.*`/`NowLayout.*` factories, caller-owned values, theme tokens,
  focus/keyboard navigation, and zero steady-state allocations: `Switch`,
  `ProgressBar` (determinate + caller-timed indeterminate), `Badge`, `Chip`
  (selectable/removable), `NowTooltip.For(...)` (hover/long-press, passive
  overlay), numeric text field spinner buttons (`SetSpinner`),
  `TabBar`/`TabView`, `SplitView` (draggable focusable divider), `TreeView`
  (caller-owned expansion/selection state), `ComboBox` (searchable dropdown),
  `DatePicker` (calendar popup, range clamping, caller-passed today),
  `TimePicker` (12/24h), `Foldout` (collapsible section header, caller-owned
  or control-owned expansion), `MaskField`/`LayerMaskField` (multi-select
  bit-flag dropdown with Nothing/Everything rows; the LayerMask twin edits
  the project's named layers), `KeyBindingField` (click-to-capture key
  binder, with `NowKeyNames.GetName` exposing the display names), and
  `Inspector` (reflection-driven Unity-style property rows for any
  serializable type).
- `NowOverlay.DeferPassive` for overlays that draw on top without blocking the
  pointer.

- `NowDrawList.Warmup(...)` and `NowRenderer.Warmup(...)` to run a
  representative initialization frame and clear the result before measuring
  allocation-sensitive steady-state rendering.
- Input-aware `NowDrawList.Warmup(...)` / `NowRenderer.Warmup(...)` overloads
  and `NowControlState.Warmup<T>(id)` for prewarming interactive, known-id
  control state outside measured frames.
- Glass diagnostics now support caller-owned, allocation-free entry reads via
  `NowGlassSettings.TryGetLastFrameDiagnostic(...)`,
  `CopyLastFrameDiagnosticsTo(...)`, and
  `ReserveDiagnostics(maxPanesPerFrame)`.
- UPM `Samples~` quick-start sample and Unity test CI workflow for edit-mode
  and play-mode validation on a self-hosted Windows Unity runner.
- World-space rendering: `NowWorldGraphic` hosts NowUI directly on a
  `MeshFilter`/`MeshRenderer` for billboards, nameplates, hover tooltips and
  diegetic panels, with ray-mapped pointer input, per-instance control id
  scoping, configurable always-visible vs scene-occluded depth, and an
  optional `NowWorldDeformer` hook for curved or wrapped surfaces.
- SDF shape extension (`NowUI.Extensions.Sdf`, its own assembly): composable
  circles, boxes, rounded boxes, ellipses and capsules drawn as one
  material-backed quad, with union/subtract/intersect operations, smooth
  blends, reusable graph operands, distance-field morphs between graphs,
  per-shape colors, scene texture fills, explicit-rect and layout-flow builders,
  and UGUI capture support through the standard NowUI batcher.
- Runtime Lottie URL loading via `NowLayout.Lottie(url)`,
  `NowLottieCache`, `NowLottieAsset.LoadFromUrl(...)`,
  `SetSourceFromUrl(...)`, byte-based source assignment, and the
  `NowLottieGraphic` Animation Url field.
- Id-less `NowInput.Interact(...)` overload coverage now matches common
  free-form usage: Unity `Rect` callers and non-primary pointer buttons can
  use call-site identity without minting throwaway numeric ids.
- `NowControls.Interact(...)` can now resolve call-site identity directly, or
  combine an optional `NowId` with a captured fallback identity, so custom
  controls do not need a separate `SiteId` / `GetControlId` step before
  running the standard pointer/focus/submit bundle.
- `NowLayout.Area(...)`, `Horizontal(...)`, and `Vertical(...)` now accept
  `Vector4` padding overloads for per-side padding without constructing a
  `NowLayoutOptions` value.
- `NowInteraction` now exposes `GetId(...)` and `State<T>(...)` helpers for
  deriving per-control sub-state keys without spelling out
  `NowInput.GetId(interaction.id, ...)`.
- `NowControlState.Repeat(id, key, ...)` now handles named repeat timers
  without manually deriving sub-ids.
- `NowControlState.Transition(...)`, `Repeat(...)`, and `PressAnimation(...)`
  now accept `NowInteraction` directly for common custom-control animation
  state.
- `NowControlState.Get<T>(id, key)` and `Warmup<T>(id, key, ...)` now cover
  named sub-state slots without manually deriving ids first.

### Changed

- Breaking: `NowGlassFrameDiagnostics` no longer exposes an allocated
  `entries` array. Use the new indexed/copy APIs on `NowGlassSettings`
  instead; aggregate frame totals remain on `lastFrameDiagnostics`.
- `NowWorldGraphic.glassBackdropMode` now uses three host choices:
  `TintOnly`, `Camera`, and `CameraAndWorld`. Blur is controlled by the
  `Now.Glass(...)` blur requests, and blurred world glass protects
  foreground scene depth automatically. The old world glass depth-mode property
  is obsolete and no longer changes rendering.
- Context menus now fit themselves inside the active NowUI surface when opened
  near the right or bottom edge.

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
- Theme tokens and presets (`NowThemeAsset`).
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
- Profiler instrumentation: `NowProfiler` exposes "Now.*"
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
