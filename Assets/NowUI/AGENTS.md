# NowUI agent instructions

These instructions apply inside the NowUI package. For consumer projects, use
the packaged skill or project snippet described in [README](README.md#agent-integration).

## Start here

For NowUI usage or behavior changes, read [AI Guide](Documentation~/AI_GUIDE.md),
then only the feature guides relevant to the task. Confirm uncertain signatures
against this package's public source and XML comments. If source and docs
disagree, use the installed implementation and report or correct the stale docs;
GitHub `main` may describe a different revision.

## Scope

- Treat `Library/PackageCache` as read-only. Put consumer scripts and assets
  under the project's `Assets` directory.
- A source checkout or embedded/local package is editable when the task calls
  for package changes. Its location alone does not make consumer work a package
  contribution. Preserve unrelated changes.
- When diagnosing a package bug, keep valid consumer usage as the reproduction.
  Correct consumer code when it violates the public contract; otherwise fix an
  editable package source when in scope, or report the package defect and a
  supported workaround. Do not patch PackageCache.

## Package contributions

- Preserve frame and measure/draw ownership, stable identity, draw order, and
  resource disposal. Read [Layout](Documentation~/Layout.md),
  [Identity](Documentation~/Identity.md), or [Render Pipelines](Documentation~/RenderPipelines.md)
  when changing those contracts.
- For controls and input, read [Custom Controls](Documentation~/CustomControls.md)
  and [Identity](Documentation~/Identity.md). Preserve child hit exclusions,
  pointer-press ownership, cancellation, focus, modal wheel handling, and the
  input/host/identity context captured by deferred overlays.
- For IMGUI or editor host changes, read [IMGUI](Documentation~/EditorGUI.md).
  Keep each panel's state, capture, focus, and repaint scheduling isolated to
  its owning window and native control ID. Use input passes rather than Unity
  frame counts for one-shot events; preserve native event consumption, idle
  behavior, bounded registries, and transactional overlay registration.
- Keep public hot paths free of hidden managed allocation after representative
  warmup. Follow [Performance](Documentation~/Performance.md) for measurement
  and ownership. Keep color conversion in rendering paths; theme and builder
  colors are authored display/sRGB values.
- Update feature docs and examples when public behavior changes. Keep detailed
  contracts in their feature guide rather than repeating them in every AI
  entry point.
- In the source repository, tests live in `Assets/NowUITests`; validation and
  release gates are in repository-level `Docs/Production.md`. Keep maintainer
  notes, proposals, benchmarks, and release procedures under `Docs`, outside
  the shipped `Documentation~` tree.

For code changes, compile and run focused validation for the affected behavior.
Treat `NOWUI001` and `NOWUI002` as correctness issues. Report what was checked
and any checks unavailable in the current environment; documentation-only
changes need link and API review, not the full Unity release matrix.
