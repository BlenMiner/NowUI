# NowUI Project Docs

This directory holds project-level documentation for NowUI features and usage
patterns. Keep these docs code-forward: each feature should show the shortest
practical example first, then call out the constraints that matter while using
it.

## Current Docs

- [Feature Usage](Features.md): core drawing lifecycle, rectangles, text, UGUI,
  and font compilation examples.
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
- [Styles And Themes](StylesAndThemes.md): ScriptableObject themes, palette
  tokens, spacing/radius tokens, rectangle presets, text presets, and preview
  workflow.

## Documentation Guidelines

- Prefer small copyable C# snippets over long explanations.
- Name the runtime type or asset involved in each feature.
- Include whether the snippet is for camera callbacks, UGUI, editor setup, or
  runtime setup.
- Keep retained/composite UI components separate from low-level drawing docs
  once those features exist.
