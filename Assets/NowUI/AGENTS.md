# NowUI Agent Instructions

These instructions apply to work performed inside the NowUI package tree. A
consumer project can copy `AI~/AGENTS.snippet.md` into its root `AGENTS.md` or
install the packaged `nowui` skill for automatic task routing.

## Determine the operating mode

- If this package is under `Library/PackageCache`, treat it as a read-only
  dependency. Implement consumer code under the project's `Assets` directory.
- If this is the NowUI source checkout, edit package code only when the task
  asks for a package change. Preserve unrelated worktree changes.
- If the package was embedded under `Packages/com.blenminer.nowui`, do not
  assume that modifying it is desired; distinguish package work from consumer
  work first.

## Start with the installed documentation

Read `Documentation~/AI_GUIDE.md` before designing or changing NowUI usage.
Then read only the feature guides relevant to the task. Use this precedence:

1. This installed package's documentation.
2. XML documentation and public source in this installed package.
3. Packaged samples and tests.
4. Version-matched remote documentation only when local material is missing.

Do not use GitHub `main` as authority for a different installed revision and do
not infer APIs from feature names or internal design notes.

## Consumer rules

- Choose the rendering host before choosing drawing or control APIs.
- Use `Now` for known rectangles and `NowLayout` for measured arrangement.
- Never call `Now.StartUI` inside a host's `DrawNowUI`; hosts own their frame.
- Use `NowLayout.RunMeasured` only with a manual host, never a layout host.
- Finish builders with `.Draw()` or `.Begin()` and use scopes with `using`.
- Wrap every reusable composite control body in
  `NowControls.ControlScope(id, file, line)`. Public wrappers must forward
  caller-file and caller-line information, and dynamic or reorderable
  instances should also receive stable non-zero explicit IDs.
- Use `NowEditorGUI` or `NowEditorGUILayout` in editor IMGUI hosts so consumed
  wheel input requests the editor repaint that makes scrolling visible and
  each panel is isolated under its owning `EditorWindow`. The panel owns native
  pointer capture and its own focus/Tab registry. Do not call `NowGUI` or
  `NowGUILayout` directly from editor hosts: those runtime/legacy paths have no
  owning-window identity and cannot isolate GUIViews. Treat
  `NowInteraction.cancelled` and `dragCancelled` as aborted gestures; do not
  commit them or wait for a later MouseUp after capture or host focus is lost.
- Let modal popups own wheel input while the pointer is over them, including at
  their scroll limits. Ordinary nested scroll views may leave a wheel tick
  unclaimed at an edge so an enclosing scroll view can move.
- Empty primary presses clear focus by default. Call `NowFocus.RetainFocus()`
  only for overlays that must preserve focus-owned state while selecting or
  dismissing them.
- Enter/Return submits and blurs a single-line `TextField`; `TextArea` keeps
  Enter as a newline. Tab and Shift+Tab use the registered focus order.
- Custom focused keyboard consumers must read `NowTextInput.current` before
  calling `NowTextInput.ClaimActivity()`, and claim only activity they own.
  Use `NowTextInput.RequestTextCapture(claimActivity: false)` for that flow;
  the parameterless overload retains its legacy capture-and-claim behavior.
  A claim prevents one-shot characters and shortcuts from replaying in another
  IMGUI pass in the same Unity frame. Use
  `NowInput.current.inputPass`, not `Time.frameCount`, for custom one-pass
  guards in IMGUI. Claim handled key and pointer activity through NowUI so the
  native IMGUI event is consumed instead of reaching a sibling panel/control.
- Treat a consuming project as a reproduction fixture when diagnosing NowUI.
  If documented public usage exposes a bug, fix the package and leave the
  consumer unchanged. Change consumer code only when it violates a documented
  host/control contract, and identify that correction separately.
- Supply theme and builder colors as authored display/sRGB values. Do not
  pre-convert them with `.linear`; NowUI's render paths perform their own
  working-space conversion.
- Draw charts and sampled paths with one `Now.DrawPolyline(...)` call. Repeated
  butt-capped `Now.Line` calls are independent strokes and can leave
  anti-aliasing seams where adjacent segments turn.
- When customizing popup rendering, keep selected-row text and background
  roles distinct and readable. The built-in renderer uses `Text` over
  `AccentMuted`; a custom palette is responsible for equivalent contrast.
- Call `MarkDirty()` when retained host state changes.
- Dispose caller-owned renderers, command buffers, model previews, and similar
  resources.
- Treat `NOWUI001` and `NOWUI002` analyzer diagnostics as correctness issues.
- Compile the consuming project and fix errors against the installed API.

## Contributor rules

- Keep public hot paths free of hidden managed allocation after documented
  warmup.
- Preserve host lifecycle, ID scoping, draw order, and ownership contracts.
- Preserve editor panel identity as owning `EditorWindow` plus native control
  ID (with the native GUI context only as a non-window fallback). A docked
  HostView can display different windows, and each cached panel needs its own
  control-state, input-provider, and focus/Tab host.
- Preserve every cached panel while its live editor context is wholly idle.
  Give a resumed context one cleanup interval to refresh later-drawn visible
  siblings; after that sustained activity, reclaim sibling native control IDs
  that remain unused past the cache lifetime, disposing their renderer,
  RenderTexture, focus host, input provider, and repaint deadline together.
- Preserve IMGUI wheel-event consumption, `GUI.changed`, and editor repaint
  propagation when changing input handling.
- Preserve the IMGUI host's native capture lifecycle: route drags and releases
  to the panel that captured the press, and cancel active interactions without
  synthesizing a click when native capture is lost. Losing editor-window or
  application focus must also clear panel focus plus held pointer, navigation,
  submit, and cancel latches so input cannot resume on refocus.
- Preserve popup wheel ownership. Deferred modal popup content must get first
  refusal over enclosing scroll views and contain an unhandled tick at its own
  edge; ordinary nested scroll edges may still fall through.
- Finalize unclaimed primary presses at input-scope completion so empty-space
  clicks clear focus even when several IMGUI events share one Unity frame;
  preserve explicit focus claims and `NowFocus.RetainFocus()`.
- Keep one-shot text, raw-key, cancel, and shortcut delivery
  input-pass-scoped. Never use `Time.frameCount` as IMGUI event identity:
  editor IMGUI can run several keyboard, pointer, layout, and repaint passes
  before it advances. Copy each panel's native KeyDown/KeyUp text semantics and
  raw binding key into its provider packet before any control can use the
  event; do not recover editor keys from a later global Input System sample.
  Unity's filtered
  `GetTypeForControl` route governs pointer capture, while focused NowUI
  ownership governs keyboard delivery. Sample a packet before claiming it,
  keep text/IME capture separate from ownership, and consume claimed KeyDown
  plus handled pointer events so native IMGUI cannot replay them elsewhere.
- Keep each IMGUI panel's focus registration plus popup and pointer registries
  bounded across repeated event passes. Replace a host's registrations for the
  current pass instead of appending until `Time.frameCount` changes. Process
  and consume Tab or Shift+Tab while its native key event is current. Overlay
  registration is transactional: if deferred drawing throws, discard the
  failed pass's provisional footprint and retain that owner's last completed
  footprint as the authoritative hit region.
- Coalesce and rate-limit editor repaint requests until the current IMGUI
  dispatch completes, and let immediate-mode controls forward tracked animation
  demand without marking temporal-only work as `GUI.changed`. Static open
  popups must converge to idle; do not request repaint merely because a popup
  remains open. Route both immediate and deadline repaint requests to the
  provider's owning `EditorWindow`; never redirect them to whichever window is
  focused or under the mouse. Focused carets schedule their next blink-phase
  deadline instead of continuously repainting. Retained UI Toolkit hosts must
  honor that deadline with a one-shot schedule and keep their 16 ms loop paused
  unless continuous rebuilding was explicitly requested. Auto-scroll must
  request the final repaint that first reaches a clamp, then stop requesting
  frames while motion only pushes against that clamp. Do not pair the bridge
  with an unconditional editor-update repaint loop unless the host displays
  genuinely live data; bound live refresh rates.
- Keep color-space conversion in the renderer/shader path rather than theme
  accessors so direct, RenderTexture, world-space, and IMGUI output agree.
- Preserve readable popup selection contrast independently of accent color
  choices.
- Update the package-local documentation and samples with public behavior.
- Keep unshipped proposals, benchmarks, release procedures, and maintainer
  notes under the repository-level `Docs` directory, not `Documentation~`.
- Use `Docs/Production.md` for source-checkout validation and release gates.
