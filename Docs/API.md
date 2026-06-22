# Public API

NowUI's supported API surface is the `NowUI` namespace exposed by the runtime,
extension, editor, URP, and HDRP assemblies under `Assets/NowUI`.

## Primary Runtime Surface

- `Now`: immediate drawing factories, frame lifecycle, ambient mask/font/color
  scopes, and explicit free-form controls.
- `NowRenderer` and `NowDrawList`: retained/offscreen draw-list construction,
  command-buffer rendering, RenderTexture rendering, and explicit warmup.
- `NowGraphic`, `NowVisualElement`, `NowPipelineGraphic`, and
  `NowWorldGraphic`: host integrations for UGUI, UI Toolkit, render pipelines,
  and world-space meshes.
- `NowLayout`: layout areas, horizontal/vertical groups, content measurement,
  labels, controls, Lottie reservations, and content rect caching.
- `NowInput`, `NowFocus`, `NowControls`, `NowControlState`,
  `NowFilePicker`, `NowViewStack`, `INowView`, `NowViews`, and control
  builders: immediate interaction, navigation, focus, reusable control state,
  file picker overlays, retained view navigation, and dialogs, including
  `NowControlState.Warmup<T>(id)` for known-id first-frame allocation control.
- `NowText`, `NowFontAsset`, `NowFont`, `NowTextWrap`,
  `NowTextSelection`, `NowTextEdit`, `NowTextArea`, and rich-text types:
  text rendering, shaping, editing, wrapping, selection, and parser hooks.
- `NowGlass`, `NowGlassSettings`, and diagnostics structs: backdrop pane
  drawing, quality selection, and non-alloc diagnostic reporting.

## Extension Surface

- `NowUI.Extensions.Markdown`: parser, document cache, syntax, image state, and
  builder APIs.
- `NowUI.Extensions.CodeEditor`: editor builder, language registry, tokens,
  diagnostics, and bundled JSON/Markdown profiles.
- `NowUI.Extensions.Docking`: dock-space builder and retained docking state.
- `NowUI.Extensions.Sdf`: SDF graph and builder APIs.

## Compatibility Rules

- Public fluent builders should remain value types unless there is a concrete
  ownership reason to switch.
- APIs used inside a frame must avoid hidden managed allocation after warmup.
- Debug and diagnostics APIs must use caller-owned buffers or indexed access.
- Warmup APIs may allocate while preparing state, but must clear captured
  geometry before returning so the next measured frame starts from a clean draw
  list.
- String IDs are allowed for convenience, but examples should prefer stable
  integer or data-backed `NowId` values in repeated/dynamic UI.
- Breaking changes are allowed before the next tagged release only when they
  remove ambiguous behavior, accidental allocation, or inconsistent naming; the
  changelog must call them out explicitly.
