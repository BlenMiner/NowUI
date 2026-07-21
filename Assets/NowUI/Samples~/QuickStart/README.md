# NowUI Quick Start Sample

Import this sample from Package Manager to get small, self-contained scripts
that show the production setup paths:

- `NowUIQuickStartOverlay.cs`: built-in render pipeline `OnPostRender` overlay
  with measured layout, a CSS-style gradient paint, and a button.
  It logs a warning when added to a URP/HDRP project — on those pipelines use
  the renderer feature / custom pass or a `NowGraphic` under a Canvas instead
  (see the RenderPipelines doc below).
- `NowUIRenderTextureGlassSample.cs`: allocation-conscious `NowRenderer` usage
  with explicit warmup and glass diagnostics recording.

The version-matched docs ship in this package under `Documentation~` and are
also available online:
<https://github.com/BlenMiner/NowUI/tree/main/Assets/NowUI/Documentation~>.
