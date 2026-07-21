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

Prefer information in this order:

1. Documentation and package metadata beside this file.
2. XML comments and public source in the same installed package.
3. Packaged samples and tests from the same installed package.
4. Documentation for the exact installed tag or revision.

Do not use documentation from GitHub `main` to guess APIs in an older cached
revision. Do not invent a symbol from a feature name; search the installed
source when an exact signature is uncertain.

When consuming NowUI, treat `Library/PackageCache` as read-only. Put project
scripts, markup, themes, and other authored assets under the project's `Assets`
directory. Only edit NowUI itself when the task explicitly targets an embedded
package or the NowUI source repository.

## Workflow

1. Confirm `com.blenminer.nowui` is the selected UI package for the task.
2. Choose the rendering host from the table below.
3. Choose explicit `Now` placement, measured `NowLayout`, or a mixture.
4. Read the routed topic guide and a nearby public example before coding.
5. Implement outside PackageCache, compile, and address analyzer diagnostics.

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
| Runtime IMGUI | `NowGUI` / `NowGUILayout` | Same helper scope | Helper |
| Editor IMGUI | `NowEditorGUI` / `NowEditorGUILayout` | Same helper scope | Helper |

Read [Render Pipeline Integrations](RenderPipelines.md) before creating UGUI,
UI Toolkit, URP, or HDRP integration. Read [World Space](WorldSpace.md) for
mesh surfaces and ray-mapped input, and [IMGUI](EditorGUI.md) for `OnGUI`.

Host lifecycle rules:

- Do not call `Now.StartUI` inside a host's `DrawNowUI`; the host owns it.
- Dedicated layout hosts own the exact measure/draw cycle. Do not call
  `NowLayout.RunMeasured` inside them.
- A manual host must wrap drawing in `using (Now.StartUI(...))` or the
  appropriate `NowRenderer`/IMGUI helper scope.
- Use `NowLayout.RunMeasured` only when a manual host needs `NowLayout`.
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
| Supported public assemblies and types | Runtime and extension namespaces | [Public API](API.md) |
| Rows, columns, sizing, measurement | `NowLayout` and layout hosts | [Layout](Layout.md) |
| Buttons, fields, pickers, lists, dialogs, inspection | `Now` / `NowLayout` controls | [Controls](Controls.md) |
| New or restyled controls | Control builders and interaction primitives | [Custom Controls](CustomControls.md) |
| Themes and reusable style tokens | `NowThemeAsset` | [Styles and Themes](StylesAndThemes.md) |
| Lines, Beziers, dashes, arrows | `Now.Line`, `Now.Bezier` | [Lines](Lines.md) |
| Circles, triangles, polygons | Shape builders | [Shapes](Shapes.md) |
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
| Warmup and allocation expectations | Warmup APIs | [Performance](Performance.md) |

## Correctness rules

- Builders are inert until consumed. End drawing builders with `.Draw()` and
  container/control scopes with `.Begin()`; place returned scopes in `using`.
- Treat `NOWUI001` and `NOWUI002` as correctness warnings, not style warnings.
- Id-less controls are suitable for fixed one-off call sites. Prefer stable
  non-zero integer IDs for data-backed, conditional, repeated, or reorderable
  controls.
- IDs are local to the active host and ID scope. Use `NowId.Resolved(...)` only
  for an identity that is already resolved or composed.
- Preserve draw order. Glass samples prior content, and material changes can
  flush the current batch.
- Use the input provider established by the host. Scope a custom
  `INowInputProvider` for RenderTextures, remote input, or tests.
- Add explicit assembly references such as `NowUI.Runtime` or a
  `NowUI.Extensions.*` assembly when consumer code lives in its own asmdef.
- Verify host-specific support before claiming all surfaces behave identically,
  especially for backdrop glass and render-pipeline integration.

## Performance and ownership

"Allocation-free" means steady state after representative warmup. First use,
new IDs, new glyphs, material batches, Lottie geometry, effect textures,
diagnostics capacity, and buffer growth can allocate.

- Warm the actual UI shape with `NowDrawList.Warmup(...)` or
  `NowRenderer.Warmup(...)` before measuring.
- For retained hosts, exercise the representative host state and interactions
  before profiling; a separate synthetic draw list may not warm host-owned
  buffers.
- Use input-aware warmup when controls consume pointer or focus state.
- Warm known control-state IDs with `NowControlState.Warmup<T>(id)`.
- Reserve glass diagnostic capacity before recording diagnostics.
- Dispose caller-owned `NowRenderer`, command buffers, model previews, and
  other documented disposable resources.
- Do not assume native plugins are required everywhere. Consult the installed
  text and Lottie documentation for backend-specific behavior and limitations.

See [Performance](Performance.md) for the focused checklist.

## Markup generation

`NowUI.Markup` is constrained XML-like UI markup, not HTML. Before generating
markup, read [Markup](Markup.md), especially **What AI Should Not Emit**.

Do not emit browser JavaScript, arbitrary HTML/CSS, remote executable content,
or unsupported tags and properties. Give interactive and stateful elements
stable IDs/state keys, and connect application behavior through the documented
event/state APIs.

## Verification

1. Search the installed package source for every uncertain type or method.
2. Confirm the selected host owns the lifecycle expected by the implementation.
3. Compile the Unity project and fix all C# errors.
4. Resolve `NOWUI001` and `NOWUI002` warnings.
5. Run relevant project tests or a focused scene/play-mode check.
6. For performance-sensitive work, warm representative state before measuring.
7. Review the diff and confirm no file under `Library/PackageCache` changed.

When validation requires maintainer-only NowUI harnesses, first confirm the task
is running in the NowUI source checkout. Consumer projects should use their own
compile, tests, and scene checks rather than repository-only commands.
