# NowUI Project Docs

This directory holds project-level documentation for NowUI features and usage
patterns. Keep these docs code-forward: each feature should show the shortest
practical example first, then call out the constraints that matter while using
it.

## Current Docs

- [Feature Usage](Features.md): core drawing lifecycle, rectangles, text, UGUI,
  and font compilation examples.
- [Public API](API.md): supported runtime/extension surfaces, compatibility
  rules, and allocation expectations for public APIs.
- [Lines](Lines.md): anti-aliased straight lines, cubic Beziers, dashed
  strokes, arrow heads, clipping, and performance notes.
- [Shapes](Shapes.md): filled or outlined circles, ellipses, triangles, and
  reusable array/list-backed polygons.
- [Glass](Glass.md): backdrop blur panes, draw order, builder options, host
  support, and the replay-backed UGUI demo.
- [Custom Materials](CustomMaterials.md): rectangle-level custom shader
  materials, UGUI material variants, texture handling, shader inputs, and the
  live frost material demo.
- [Effects](Effects.md): scoped mesh and texture-backed visual modifiers,
  deformers, subdivision, snapshots, and performance notes.
- [World Space](WorldSpace.md): direct-mesh nameplates, hover tooltips,
  diegetic panels, ray-mapped input, depth modes, and surface deformers.
- [Controls](Controls.md): buttons, checkboxes, radios, switches, sliders,
  progress bars, badges/chips, tooltips, text fields, dropdowns, combo boxes,
  mask/layer-mask fields, key binding fields, date/time pickers, file pickers,
  tabs, split views, tree views, foldouts, scroll views, view stacks/dialogs,
  the reflection-driven inspector, and the toolkit for building custom
  controls.
- [Custom Controls](CustomControls.md): the how-to guide — restyling through
  themes, wrapping variants, custom shapes on standard interaction, and
  building new controls from scratch (with a live demo in the docs browser
  scene).
- [Layout](Layout.md): flexbox-style groups, sizing options, flexible space,
  and the two measurement modes.
- [Lottie](Lottie.md): importing `.lottie` assets, the `Now.Lottie` builder,
  layout/UGUI integration, and tessellation performance notes.
- [Mobile](Mobile.md): density-scaled UI (`Now.StartUI(uiScale)`), safe areas,
  touch input, and the Android/iOS native plugin story.
- [IMGUI](EditorGUI.md): static APIs for drawing NowUI inside runtime or editor
  `OnGUI` without inheriting a NowUI-specific base class.
- [Render Pipeline Integrations](RenderPipelines.md): Built-in, UGUI, URP, and
  HDRP integration patterns.
- [Production Gates](Production.md): local/CI validation commands,
  allocation-warmup rules, diagnostics storage, and Asset Store prep notes.
- [Styles And Themes](StylesAndThemes.md): ScriptableObject themes, palette
  tokens, spacing/radius tokens, rectangle presets, text presets, and preview
  workflow.
- [Code Editor](CodeEditor.md): the embeddable code editor extension —
  syntax highlighting, validation squiggles, auto-pairs, auto-indent,
  undo/redo; JSON and markdown profiles with a registry for adding more.
- [Docking](Docking.md): dockable tabbed windows with side splits, splitter
  resizing, floating windows, tab-drag dock guides, and retained layout state.
- [Node Graph](NodeGraph.md): shader-style and visual-scripting-style graph
  data, typed ports, Bezier links, draggable nodes, pan/zoom, and link editing.
- [SDF Shapes](SDF.md): composable signed-distance-field circles, boxes,
  rounded boxes, ellipses and capsules with union/subtract/intersect operations,
  smooth blends, colors, and texture fills.
- [Rich Text](RichText.md): spans, default tag parsing, custom inline tags,
  Lottie tags, and link/tag hit testing.
- [Markup](Markup.md): constrained XML-like layout/control markup for
  AI-authored interfaces, with inline style, style blocks, state, and events.
- [Markdown](Markdown.md): GitHub-flavored Markdown rendered through
  `Now`/`NowLayout` primitives — layout-flow, explicit-rect, and retained
  document forms.

All of these render in-engine: open `Assets/Scenes/DocsScene.unity` for a
browsable version with a scrollable side menu, powered by the markdown
extension.

## Documentation Guidelines

- Prefer small copyable C# snippets over long explanations.
- Name the runtime type or asset involved in each feature.
- Include whether the snippet is for camera callbacks, UGUI, editor setup, or
  runtime setup.
- Keep retained/composite UI components separate from low-level drawing docs
  once those features exist.
