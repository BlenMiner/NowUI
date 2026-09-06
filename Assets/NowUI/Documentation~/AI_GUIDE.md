# Using NowUI from an AI coding agent

This guide is the task router for the installed NowUI revision. Use it to
choose the correct host and API family, then open only the detailed guides
needed for the task.

## Contents

- [Source of truth](#source-of-truth)
- [Workflow](#workflow)
- [Choose a host](#choose-a-host)
- [Choose placement](#choose-placement)
- [Feature router](#feature-router)
- [Correctness rules](#correctness-rules)
- [Performance and ownership](#performance-and-ownership)
- [Markup generation](#markup-generation)
- [Verification](#verification)

## Source of truth

Start with the documentation and package metadata beside this file. Confirm
uncertain signatures in the same package's public source and XML comments; if
they disagree with the docs, use the installed implementation and report or
correct the discrepancy. Use packaged examples when helpful, and remote docs
only for the installed tag/revision when local material is missing. GitHub
`main` and model memory may describe a different API.

When consuming NowUI, treat `Library/PackageCache` as read-only. Put project
scripts, markup, themes, and other authored assets under the project's `Assets`
directory. Edit NowUI itself only when package changes are in scope, using an
editable source checkout or embedded/local dependency.

## Workflow

1. Confirm `com.blenminer.nowui` is the selected UI package for the task.
2. Choose the rendering host from the table below.
3. Choose explicit `Now` placement, measured `NowLayout`, or a mixture.
4. Read the routed topic guide; use a nearby public example when needed.
5. Implement and validate the affected behavior as described below.

If NowUI is merely installed but the user or project has chosen a different UI
framework, preserve that choice. Do not rewrite an established UI system
without an explicit reason.

## Choose a host

| Context | Explicit placement | Measured layout | Frame owner |
| --- | --- | --- | --- |
| UGUI Canvas | `NowGraphic` | `NowLayoutGraphic` | Host |
| UI Toolkit/UXML | `NowVisualElement` | `NowLayoutVisualElement` | Host |
| URP/HDRP overlay | `NowPipelineGraphic` | `NowPipelineLayoutGraphic` | Host plus pipeline integration |
| World-space mesh | `NowWorldGraphic` | `NowWorldLayoutGraphic` | Host |
| Built-in `OnPostRender` | `Now.StartUI(...)` | `Now.StartUI(...)` plus `NowLayout.RunMeasured(...)` | Caller |
| RenderTexture/command buffer | `NowRenderer.Begin(...)` | `NowRenderer.Begin(...)` plus `NowLayout.RunMeasured(...)` | Caller |
| Runtime IMGUI | `NowGUI.Auto(...)` / `NowGUILayout.Auto(...)` | Helper scope plus `NowLayout.RunMeasured(...)` | Helper |
| Editor IMGUI | `NowEditorGUI.Auto(...)` / `NowEditorGUILayout.Auto(...)` | Helper scope plus `NowLayout.RunMeasured(...)` | Helper |

UGUI requires resolved `com.unity.ugui`; UI Toolkit requires
`com.unity.modules.uielements`. NowUI detects direct and transitive dependencies
with assembly version defines; do not set `NOWUI_UGUI`, `NOWUI_UITOOLKIT`, or
input defines manually. Add a missing host dependency when needed for the task.

Input System support is optional. The default provider prefers it when resolved
and enabled, then falls back to an enabled legacy Input Manager. Reliable default
gamepad navigation requires the Input System because legacy mappings are
project-defined. `KeyBindingField`, `NowKeyInput`, and `NowKeyNames` require
resolved `com.unity.inputsystem`; their public API uses its `Key` type.

Read [Render Pipeline Integrations](RenderPipelines.md) before creating UGUI,
UI Toolkit, URP, or HDRP integration. Read [World Space](WorldSpace.md) for
mesh surfaces and ray-mapped input, and [IMGUI](EditorGUI.md) for `OnGUI`.

Host lifecycle rules:

- Do not call `Now.StartUI` inside a host's `DrawNowUI`; the host owns it.
- Dedicated layout hosts own the exact measure/draw cycle. Do not call
  `NowLayout.RunMeasured` inside them.
- A manual host must wrap drawing in `using (Now.StartUI(...))` or the
  appropriate `NowRenderer`/IMGUI helper scope.
- `NowRenderer.Begin(...)` only captures drawing. Dispose that scope before
  calling `Render(target)`, or recording `Draw(...)` into a command buffer the
  caller executes; see [Feature Usage](Features.md#rendertexture-and-command-buffers).
- Use `NowLayout.RunMeasured` only when a manual host needs `NowLayout`.
- Editor IMGUI must use the editor wrappers so each panel's state, capture,
  focus, and repaint requests belong to its owning window.
- Call `MarkDirty()` when retained component state changes. Rebuild every frame
  only for continuously changing content.

## Choose placement

- Use `Now` when the task already has resolved rectangles or needs exact
  free-form placement.
- Use `NowLayout` for responsive rows, columns, gaps, padding, growth,
  alignment, and intrinsic measurement.
- Use `NowLayout.ReserveRect(...)` to allocate a measured slot and draw a
  free-form `Now` primitive into the returned rectangle.
- Keep state mutations out of the measure pass. If code must distinguish the
  pass, use `NowLayout.isMeasurePass` as documented in [Layout](Layout.md).

## Feature router

| Need | Start with | Read |
| --- | --- | --- |
| Frame lifecycle, rectangles, input, text, fonts, renderer | `Now`, `NowInput`, `NowRenderer` | [Feature Usage](Features.md) |
| Text outlines, gradients, reveals, and glyph animation | `Now.Text`, `NowTextAnimations` | [Text Gradients And Animation](TextStyling.md) |
| Localization and shared text transformations | `Now.SetTextPreprocessor` | [Text Preprocessor](TextPreprocessor.md) |
| Supported public assemblies and types | Runtime and extension namespaces | [Public API](API.md) |
| Rows, columns, sizing, measurement | `NowLayout` and layout hosts | [Layout](Layout.md) |
| Buttons, fields, pickers, lists, dialogs, inspection | `Now` / `NowLayout` controls | [Controls](Controls.md) |
| New or restyled controls | Control builders and interaction primitives | [Custom Controls](CustomControls.md) |
| Authored, resolved, repeated, or composite identity | `NowId`, `NowResolvedId`, `KeyedItem` | [Identity](Identity.md) |
| Themes and reusable style tokens | `NowThemeAsset` | [Styles and Themes](StylesAndThemes.md) |
| Lines, sampled paths, Beziers, dashes, arrows | `Now.Line`, `Now.DrawPolyline`, `Now.Bezier` | [Lines](Lines.md) |
| Linear, radial, and conic fills | `Now.Gradient` | [Gradients](Gradients.md) |
| Circles, triangles, polygons | Shape builders | [Shapes](Shapes.md) |
| Non-rectangular or soft clipping | `NowMaskShape` and `Now.Mask` | [Masks](Masks.md) |
| Backdrop blur panes | `Now.Glass` | [Glass](Glass.md) |
| Custom rectangle shaders/materials | `SetMaterial` | [Custom Materials](CustomMaterials.md) |
| Mesh or texture visual modifiers | `NowEffects` | [Effects](Effects.md) |
| 3D object previews | `NowModelPreview` | [Model Previews](ModelPreviews.md) |
| Rich spans, inline tags, selectable content | Rich-text builders and parsers | [Rich Text](RichText.md) |
| GitHub-flavored Markdown | `NowUI.Markdown` | [Markdown](Markdown.md) |
| AI-friendly XML-like UI documents | `NowUI.Markup` | [Markup](Markup.md) |
| Embedded code editor | `NowUI.CodeEditor` | [Code Editor](CodeEditor.md) |
| Lottie vector animation | `Now.Lottie` / `NowLayout.Lottie` | [Lottie](Lottie.md) |
| Dockable windows and tab splits | `NowUI.Docking` | [Docking](Docking.md) |
| Visual node graphs | `NowUI.NodeGraph` | [Node Graph](NodeGraph.md) |
| Composable SDF graphics | `NowUI.Sdf` | [SDF Shapes](SDF.md) |
| Mobile scale, safe areas, touch | `NowScreen`, `NowInput` | [Mobile](Mobile.md) |
| World-space panels and input | World graphic hosts | [World Space](WorldSpace.md) |
| UGUI, UI Toolkit, Built-in, URP, HDRP | Host and pipeline types | [Render Pipelines](RenderPipelines.md) |
| Runtime or editor `OnGUI` | `NowGUI`, `NowEditorGUI` | [IMGUI](EditorGUI.md) |
| Inspector preview of a UI host | `NowPreviewEditor`, `INowPreviewHost` | [IMGUI](EditorGUI.md) |
| Warmup and allocation expectations | Warmup APIs | [Performance](Performance.md) |

## Correctness rules

- Builders are inert until consumed. End drawing builders with `.Draw()` and
  container/control scopes with `.Begin()`; place returned scopes in `using`.
- Treat `NOWUI001` and `NOWUI002` as correctness warnings, not style warnings.
- Id-less controls are suitable for fixed one-off call sites. Use
  `NowControls.KeyedItem` with a stable data key for conditional, repeated, or
  reorderable items.
- Wrap composite custom-control bodies in `NowControls.ControlScope(...)` so
  their local child IDs resolve within the invocation instead of sharing focus
  and state with another instance. Reusable wrappers must forward caller-file
  and caller-line information; repeated instances also need stable explicit
  keys. Read [Custom Controls](CustomControls.md) and [Identity](Identity.md)
  before implementing wrappers or custom builders.
- Keep authored local keys as `NowId` and already-resolved runtime paths as
  `NowResolvedId`. Pass resolved values directly and derive sub-controls with
  `.Child(...)`; never convert them back into authored IDs. Integer zero is a
  valid authored key. `NowControls.SiteId(...)` returns an opaque
  `NowCallSiteId` fallback for a custom builder, not an authored or resolved
  control identity. Consult [Identity](Identity.md) for typed fallback and
  migration examples.
- Parent controls containing independent child controls must exclude child hit
  rectangles with `NowInteractionRegion` before parent interaction.
- Use `NowContextAction.Resolve(...)` when either a secondary pointer or an
  action button can open a menu. Give every item and submenu a stable explicit
  `id:`. Raw-integer resolved-identity overloads and positional menu-entry
  overloads are source-blocked with compiler errors.
- In anonymous `NowOverlay.Defer`, `DeferScreen`, and `DeferPassive` callback
  overloads, `int state` is payload only. Pass a separate `NowResolvedId`
  source to the named overload when the overlay needs identity. Deferred draws
  run with the input provider/pass/surface and host/id context captured when
  they were queued.
- For custom keyboard, focus, drag, or popup behavior, follow
  [Custom Controls](CustomControls.md) and, in IMGUI, [IMGUI](EditorGUI.md).
  Handle cancellation as an aborted gesture and claim only input you handle.
- Preserve draw order. Glass samples prior content, and material changes can
  flush the current batch.
- Supply theme and builder colors as display/sRGB values; do not pre-convert
  them with `.linear`. Keep custom popup selection text readable against its
  background; see [Styles and Themes](StylesAndThemes.md).
- Use the input provider established by the host. Scope a custom
  `INowInputProvider` for RenderTextures, remote input, or tests.
- Add explicit assembly references such as `NowUI.Runtime` or a
  `NowUI.Extensions.*` assembly when consumer code lives in its own asmdef.
- Verify host-specific support before claiming all surfaces behave identically,
  especially for backdrop glass and render-pipeline integration.

## Performance and ownership

"Allocation-free" means steady state after representative warmup. New IDs,
glyphs, geometry, textures, and growing buffers can allocate. Before profiling,
read [Performance](Performance.md) and warm the actual host state and input
interactions; a separate draw list may not warm retained host buffers.

Dispose caller-owned `NowRenderer`, command buffers, model previews, and other
documented disposable resources. Consult the relevant text or Lottie guide for
native backend requirements rather than assuming a plugin is always needed.

## Markup generation

`NowUI.Markup` is constrained XML-like UI markup, not HTML. Before generating
markup, read [Markup](Markup.md), especially **What AI Should Not Emit**.

Do not emit browser JavaScript, arbitrary HTML/CSS, remote executable content,
or unsupported tags and properties. Give interactive and stateful elements
stable IDs/state keys, and connect application behavior through the documented
event/state APIs.

## Verification

For code changes, compile against the installed package, fix C# errors and
`NOWUI001`/`NOWUI002` diagnostics, then run relevant tests or a focused scene or
editor check. Check the affected behavior: for example, first-frame layout,
stable focus after row reordering, or editor scrolling and gesture cancellation.
Review the diff for accidental PackageCache edits.

Use maintainer harnesses only in the NowUI source repository; consumer projects
use their own compile, tests, and scene checks. For documentation-only changes,
check links and API claims. Report what was actually verified and any checks
that could not run in the current environment.
