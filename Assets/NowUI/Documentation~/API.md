# Public API

NowUI's supported API surface is the `NowUI` namespace exposed by the runtime,
extension, editor, URP, and HDRP assemblies in the installed package.

UGUI host types are compiled when Unity resolves `com.unity.ugui`; UI Toolkit
host types are compiled when it resolves `com.unity.modules.uielements`. These
packages can be direct or transitive dependencies, and no manual scripting
define is required.

Input System integration is also optional and is detected as a direct or
transitive `com.unity.inputsystem` dependency. The default provider prefers it
when installed and enabled, otherwise falling back to the legacy Input Manager
when enabled. No manual NowUI input define is required.

## Primary Runtime Surface

- `Now`: immediate drawing factories, frame lifecycle, ambient mask/font/color
  scopes, and explicit free-form controls. `Now.Mask(NowRect)` preserves exact
  rectangular clipping, while `Now.Mask(NowMaskShape)` applies an analytic
  clip to all supported built-in draws in the scope.
- `NowMaskShape`: value-type analytic rectangle, rounded-rectangle, circle,
  ellipse, and capsule masks with physical-pixel feathering and conservative
  bounds. See [Masks](Masks.md).
- `NowMaskTexture` and `NowMaskTextureChannel`: caller-owned alpha- or
  red-channel texture coverage mapped to an authored rect, with optional
  inversion and conservative rectangle input. See [Masks](Masks.md).
- `NowRenderer` and `NowDrawList`: retained/offscreen draw-list construction,
  command-buffer rendering, RenderTexture rendering, and explicit warmup.
- `NowPipelineGraphic` and `NowWorldGraphic`: one-pass, explicit-rect host
  integrations for render pipelines and world-space meshes. Their
  `NowPipelineLayoutGraphic` and `NowWorldLayoutGraphic` counterparts own exact
  `NowLayout` measure/draw cycles.
- When `com.unity.ugui` is resolved, `NowGraphic`, `NowLayoutGraphic`,
  `NowLottieGraphic`, and `NowUGUINavigationProxy` provide the UGUI integration.
  `NowGraphic` exposes `hasFocusedControl`; the navigation proxy represents its
  internal controls as one UGUI `Selectable` and yields directional or Tab
  navigation only at the host boundary.
- When `com.unity.modules.uielements` is resolved, `NowVisualElement` and
  `NowLayoutVisualElement` provide the UI Toolkit integration.
- `NowLayout`: fluent `Row`/`Horizontal` and `Column`/`Vertical` container
  aliases, growth, justification, `ReserveRect` bridging, manual-host
  `RunMeasured`, content measurement, labels, controls, Lottie reservations,
  and content rect caching. The lower-level immediate-scope forms are named
  `HorizontalScope` and `VerticalScope`; code using their former directional
  names must migrate to the explicit `Scope` suffix.
- `NowInput`, `INowInputProvider`, `INowSurfaceToScreenMapper`, `NowFocus`,
  `NowControls`, `NowControlState`,
  `NowFilePicker`, `NowViewStack`, `INowView`, `NowViews`, and control
  builders: immediate interaction, navigation, focus, reusable control state,
  optional surface-to-screen projection for IME candidate placement, file
  picker overlays, retained view navigation, and dialogs, including
  `NowControlState.Warmup<T>(id)` for known-id first-frame allocation control.
- When `com.unity.inputsystem` is resolved, `NowKeyBindingField`,
  `NowKeyInput`, and `NowKeyNames` provide keyboard-binding capture and display
  names over `UnityEngine.InputSystem.Key`. These types are not compiled into
  configurations without that package.
- `NowText`, `NowFontAsset`, `NowFont`, `NowTextWrap`,
  `NowTextSelection`, `NowTextEdit`, `NowTextArea`, `NowTextFieldResult`, and rich-text types:
  text rendering, shaping, editing, wrapping, selection, and parser hooks.
- `NowGlass`, `NowGlassSettings`, and diagnostics structs: backdrop pane
  drawing, quality selection, and non-alloc diagnostic reporting.
- `NowGradient`, `NowGradientKind`, `NowGradientDirection`,
  `NowGradientShape`, and `NowGradientSpread`: CSS-inspired linear, radial,
  and conic paints backed by two-color or Unity `Gradient` ramps.
- `NowModel`, `NowModelPreview`, `NowModelPreviewSourceMode`, and
  `NowModelPreviewUpdateMode`: isolated raw-mesh or caller-owned scene-object
  model-to-texture previews
  drawing, explicit preview resource ownership, deferred refresh scheduling,
  framing, and resolution control.

## Extension Surface

- `NowUI.Markdown`: parser, document cache, syntax, image state, and
  builder APIs.
- `NowUI.Markup`: constrained XML-like markup parsing, state binding, and
  hot-reloadable document rendering.
- `NowUI.CodeEditor`: editor builder, language registry, tokens,
  diagnostics, completion hooks, and bundled JSON/Markdown/Markup profiles.
- `NowUI.Docking`: dock-space builder and retained docking state.
- `NowUI.NodeGraph`: node-graph data, ports, links, and graph view drawing.
- `NowUI.Sdf`: SDF graph and scene-builder APIs. A scene can end in `Draw()`
  or `BeginMask()`; the latter returns an ambient `NowMaskScope` backed by
  cached, single-channel SDF coverage. `SetMaskResolutionScale(scale)` can
  reduce that coverage target's resolution without changing its authored
  bounds. `RotateNext(angleDegrees)` explicitly targets one following analytic
  primitive or complete SDF `Text` call, while balanced
  `PushRotation(angleDegrees)` / `PopRotation()` calls apply compositional
  relative rotation to runs of both without steady-state allocation. Positive
  degrees rotate clockwise in UI space. Text glyphs rotate rigidly around the
  center of the axis-aligned bounds of the compatible glyph quads actually
  emitted by that call. `RotateNext` is consumed once even when the text is
  empty or emits no compatible glyphs, and it composes with a pushed rotation.
  These APIs do not transform `Graph` or `Morph` operands directly. Any
  nonidentity per-node rotation requires material ABI v2.
  `SetMaterial(Material[, bool])` selects a compatible, compiled HLSL material
  template; `NowSdf.MaterialAbiVersion` and
  `NowSdf.MaterialAbiProperty` describe the current ABI-v2 declaration, while
  `NowSdf.MinimumMaterialAbiVersion` identifies the oldest legacy-only ABI the
  runtime accepts. The cache owns direct and mask material clones per distinct
  template, while the caller retains the templates. Static templates preserve
  upload and mask reuse; per-frame synchronization recopies template
  properties and rerasterizes custom masks.
  There is no C# distance-function delegate or runtime shader injection.
  `NowSdf.Release(id)` releases one explicit stable-id cache;
  `NowSdf.Reset()` releases them all. Both invalidate builders backed by a
  released cache, whose consumer calls then throw `ObjectDisposedException`.
  See [SDF Shapes](SDF.md) and [Masks](Masks.md).

## Runtime guarantees

- APIs used inside a frame must avoid hidden managed allocation after warmup.
- Debug and diagnostics APIs must use caller-owned buffers or indexed access.
- Warmup APIs may allocate while preparing state, but must clear captured
  geometry before returning so the next measured frame starts from a clean draw
  list.
- String IDs are allowed for convenience, but examples should prefer stable
  integer or data-backed `NowId` values in repeated/dynamic UI. Both forms are
  host/id-scope local; `NowId.Resolved(...)` is reserved for already-composed
  identities.
