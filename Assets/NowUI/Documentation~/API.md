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
- `NowId`, `NowResolvedId`, `NowCallSiteId`, `NowControlIdentity`,
  `NowInteractionRegion`, `NowContextAction`, `NowContextTrigger`, `NowInput`,
  `INowInputProvider`, `INowSurfaceToScreenMapper`, `NowFocus`, `NowControls`,
  `NowControlState`,
  `NowFilePicker`, `NowFilePickerView`, `NowViewStack`, `INowView`, `NowViews`, and control
  builders: immediate interaction, navigation, focus, reusable control state,
  optional surface-to-screen projection for IME candidate placement, file
  picker overlays, retained view navigation, and dialogs, including
  `NowControlState.Warmup<T>(id)` for known-id first-frame allocation control.
- When `com.unity.inputsystem` is resolved, `NowKeyBindingField`,
  `NowKeyInput`, and `NowKeyNames` provide keyboard-binding capture and display
  names over `UnityEngine.InputSystem.Key`. These types are not compiled into
  configurations without that package.
- `NowText`, `NowTextAnimation`, `NowTextAnimationKind`,
  `NowTextAnimationEasing`, `NowTextAnimations`, `NowFontAsset`, `NowFont`, `NowTextWrap`,
  `NowTextSelection`, `NowTextSelectionResult`, `NowTextEdit`, `NowTextArea`,
  `NowTextFieldResult`, and rich-text types:
  text rendering, gradient fills, caller-timed glyph animation, shaping,
  editing, wrapping, selection, and parser hooks.
- `NowTextAlign`, `NowTextVerticalAlign`, `NowFontMetrics`,
  `INowTextGlyphAnimator`, and `NowTextGlyphState`: text alignment inside a
  rect, optical cap-height centering, letter spacing, vertical font metrics,
  per-unit layout boxes (`NowText.GetUnitRects`), and caller-defined per-glyph
  animation (`NowTextAnimations.Custom`). See
  [Text Layout, Gradients, And Animation](TextStyling.md).
- `NowEase` and `NowEasing`: stateless, allocation-free easing curves (Sine,
  polynomial, Expo, Circ, Back, Elastic, Bounce, Spring, Smoothstep, and CSS
  `CubicBezier`), plus `Progress` for caller-owned time windows and `Window`
  for fade-in/hold/fade-out envelopes. `NowKey<T>`, `NowKey.At`, and
  `NowKeyframes` evaluate sorted keyframe tracks of `float`, `Vector2`,
  `Vector3`, and `Color`. See [Easing](Easing.md).
- `NowShadow` (`Now.Shadow(rect)`): a soft drop shadow for any surface, with
  explicit offset/blur/spread/color or the theme's elevation presets. See
  [Feature Usage](Features.md#rectangles).
- `NowColor`: `WithAlpha`, `MultiplyAlpha`, `Lighten`, `Darken`, `MixRgb`, and
  `Luminance` extension methods for authored colors.
- `NowGridLines` (`Now.GridLines(rect, spacing)`): hairline grids with spacing,
  offset, thickness and axis selection. `NowRectangle.SetFill(false)` draws
  outline-only rectangles.
- `NowSweep`: arc and pie ranges in named conventions, `NowSweep.Clock`
  (degrees from 12 o'clock) or `NowSweep.Radians`, accepted by SDF `Arc`/`Pie`
  and `Now.Arc`.
- SDF scenes and graphs: `SetArcCap`, `PushTransform`/`PushTransformAround`/
  `PopTransform`, nested `Graph`/`Morph` inside a graph, `AddShadow`, and on the
  scene builder `UseUiCoordinates` and `DrawAndBeginMask`. See
  [SDF Shapes](SDF.md#group-transforms-and-ui-coordinates).
- SDF gradient fills: `SetGradient`, `SetGradientLinear`, `SetGradientRadial`,
  `SetGradientConic`, `SetGradientSpread`, `SetGradientRepetitions`, and
  `UseGradient` on `NowSdfGraph` and the scene builder lay a ramp over each
  shape's box. See [SDF Shapes](SDF.md#gradient-fills).
- `NowTextTransition` and `NowTextTransitionKind`: fade, slide and odometer
  roll transitions for `NowText.DrawValue` and `NowText.DrawTransition`. See
  [Text Layout, Gradients, And Animation](TextStyling.md#value-transitions).
- Control builders' `Measure()` returns the size a button, toggle, slider,
  switch, badge or chip takes, for sizing explicit rects. `NowRect.TakeTop`
  (and siblings) accept a gap, and `SplitColumns`/`SplitRows` divide a rect
  into equal or weighted cells.
- `NowGlass`, `NowGlassSettings`, and diagnostics structs: backdrop pane
  drawing, quality selection, and non-alloc diagnostic reporting.
- `NowLine`, `NowPolyline`, `NowArc`, `NowLineCap`, and `NowLineArrow`:
  anti-aliased strokes for segments, cubic Beziers, sampled or closed paths,
  circular arcs, and rings, sharing width, cap, gradient, dash, arrow, and
  mask styling. `Now.DrawPolyline` is the one-call solid-stroke form. See
  [Lines](Lines.md).
- `NowSubdivision` (`Auto`, `None`, `Fixed`, `MaxCellSize`): how effect
  modifiers split captured quads; `Auto`, the default, lets built-in deformers
  choose. See [Effects](Effects.md#subdivision).
- `Now.Transform`, `Now.TransformAround`, `Now.Rotate`, `Now.Opacity`, and
  `Now.Tint` with their `NowTransformScope`, `NowRotationScope`, and
  `NowTintScope` values, plus `Now.currentTint`: stack-backed scopes that scale,
  pan, rotate, fade, or tint everything drawn inside them. See
  [Transforms](Transforms.md).
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
  `Image(rect, texture[, uvRect][, threshold])` and
  `Sprite(rect, sprite[, threshold])` add a texture's alpha silhouette as a
  shape, so every scene effect, boolean operation, and morph follows the opaque
  outline; the distance field is baked on the GPU without read/write texture
  access. A scene packs any number of images from unrelated textures into
  private field and color atlases, leaving `_MainTex` to text and
  `SetTexture` fills. Image shapes require material ABI v2.
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
- Strings and integers (including zero) are authored through `NowId`; both remain
  host/id-scope local. Repeated data should use `NowControls.KeyedItem` or
  `KeyedItemIn`. `NowResolvedId` is the opaque, host-owned runtime result and
  derives sub-controls with `.Child(...)`; it is never persisted or wrapped
  back into `NowId`. `NowControls.SiteId(...)` returns an opaque
  `NowCallSiteId` fallback, not an authored or resolved identity. Raw-integer
  resolved-identity adapters are compile-time errors. See
  [Identity](Identity.md).
- Context-menu entries and submenus require stable authored IDs. Menu roots and
  named overlays take `NowResolvedId`; positional menu entries and raw integer
  menu IDs are compile-time errors.
- Deferred overlay callbacks run under their captured provider/input-pass,
  surface, host, and identity context. Anonymous non-capturing overlay overloads
  treat `int state` as callback payload only; named overloads take a separate
  resolved source ID.
