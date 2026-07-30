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
  wheel input requests the editor repaint that makes scrolling visible.
- Empty primary presses clear focus by default. Call `NowFocus.RetainFocus()`
  only for overlays that must preserve focus-owned state while selecting or
  dismissing them.
- Custom focused keyboard consumers that read `NowTextInput.current` must call
  `NowTextInput.ClaimActivity()` so one-shot characters and shortcuts are not
  replayed by another IMGUI pass in the same Unity frame.
- Treat a consuming project as a reproduction fixture when diagnosing NowUI.
  If documented public usage exposes a bug, fix the package and leave the
  consumer unchanged. Change consumer code only when it violates a documented
  host/control contract, and identify that correction separately.
- Supply theme and builder colors as authored display/sRGB values. Do not
  pre-convert them with `.linear`; NowUI's render paths perform their own
  working-space conversion.
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
- Preserve IMGUI wheel-event consumption, `GUI.changed`, and editor repaint
  propagation when changing input handling.
- Finalize unclaimed primary presses at input-scope completion so empty-space
  clicks clear focus even when several IMGUI events share one Unity frame;
  preserve explicit focus claims and `NowFocus.RetainFocus()`.
- Keep one-shot text and shortcut delivery input-pass-scoped. Do not rely on
  `Time.frameCount` alone because editor IMGUI can run several input/draw
  passes before it advances.
- Coalesce and rate-limit editor repaint requests until the current IMGUI
  dispatch completes, and let immediate-mode controls forward tracked animation
  demand. Do not pair that bridge with an unconditional editor-update repaint
  loop unless the host displays genuinely live data; bound live refresh rates.
- Keep color-space conversion in the renderer/shader path rather than theme
  accessors so direct, RenderTexture, world-space, and IMGUI output agree.
- Preserve readable popup selection contrast independently of accent color
  choices.
- Update the package-local documentation and samples with public behavior.
- Keep unshipped proposals, benchmarks, release procedures, and maintainer
  notes under the repository-level `Docs` directory, not `Documentation~`.
- Use `Docs/Production.md` for source-checkout validation and release gates.
