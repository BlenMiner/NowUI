# Production Gates

NowUI targets Unity `6000.4` or newer. The package is developed under
`Assets/NowUI` as `com.blenminer.nowui`; Asset Store UPM product setup is
handled in the publisher portal.

## Local Validation

Close any open Unity editor for this project before running batchmode tests.
Unity refuses to open the same project twice.

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode EditMode
pwsh -File Tools/NowUI-Harness.ps1 -Mode PlayMode
```

The harness requires PowerShell 7, resolves the project relative to its own
location, and finds the Unity Hub editor version recorded in
`ProjectSettings/ProjectVersion.txt`. Set `UNITY_EDITOR` or pass
`-UnityEditor` for a nonstandard install; an invalid override fails instead of
silently selecting another editor. Test runs fail on invalid XML, failed tests,
or zero discovered tests, and write logs/results under `artifacts/local`.

For CI, `.github/workflows/unity-tests.yml` runs the same commands through
`.github/scripts/Run-UnityTests.ps1` on a self-hosted Windows runner with Unity
`6000.4.0f1`. Do not pass `-quit` to Unity test runs here; the Unity Test
Framework exits batchmode after writing results. The script also fails the job
if Unity exits without producing the expected XML. CI passes
`-CleanScriptAssemblies` so stale generated assemblies from a reused workspace
cannot pollute logs.

## Visual Validation

`.github/workflows/visual-smoke.yml` runs the editor visual harness as a
separate rendering gate:

- Windows, macOS, and Linux self-hosted runners execute
  `Tools/NowUI-Harness.ps1 -Mode Visual`, producing PNG captures and a
  `manifest.json`.
- `Tools/Assert-NowUIVisualArtifacts.ps1` validates the manifest, unique capture
  names, required scenario names, PNG headers, dimensions, file sizes, and
  nonzero batch/vertex counts. CI requires the desktop and compact `Now` and
  `NowLayout` landing-page captures by name, so reducing the total scenario
  count cannot silently remove their coverage.
- The Windows runner also executes `-Mode Golden` to compare canonical captures
  against `Assets/NowUI/Tests/Baselines/Visual`. Landing-page scenarios use a
  stricter per-scenario mismatch ceiling than the general harness to catch a
  missing small control.
- All captures are uploaded as workflow artifacts for inspection.

The cross-OS jobs require Unity `6000.4.0f1` on self-hosted runners with the
standard GitHub runner OS labels (`Windows`, `macOS`, `Linux`). Linux visual
runners must provide a graphics-capable session or virtual display; do not run
the visual harness with `-nographics`.

## Render-Pipeline Validation

The repository remains a Built-in Render Pipeline project so optional SRP
dependencies do not become mandatory for package users.
`.github/workflows/render-pipeline-validation.yml` creates disposable URP and
HDRP validation workspaces instead:

- `.github/scripts/Set-RenderPipelineValidation.ps1` installs the matching
  `17.4.x` package for Unity `6000.4` and removes the other SRP package.
- Each job requires `NowUI.URP` or `NowUI.HDRP` to compile and load. The
  `NOWUI_EXPECT_RENDER_PIPELINE` assertion fails if an optional assembly is
  absent, so package resolution cannot turn into an ignored success.
- The URP job also renders model previews and a renderer-feature frame and
  checks the resulting pixels. The HDRP job currently provides compile/load
  compatibility coverage for its thin custom-pass wrapper; a deterministic
  HDRP frame fixture remains future work.

The workflow runs for pull requests that touch SRP integration, shared model or
pipeline rendering, play-mode coverage, packages, or its own automation. It can
also be dispatched manually.

## Player-Build Matrix

`.github/workflows/build-verification.yml` builds the full first-class player
matrix every Sunday and supports manual per-target runs:

| Target | Runner | What the build verifies |
| --- | --- | --- |
| Windows x64 | Windows | IL2CPP player and Windows native plugin import |
| Linux x64 | Linux | IL2CPP player and Linux native plugin import |
| macOS | macOS | IL2CPP player and universal native plugin import |
| Android arm64 | Windows | ARM64 IL2CPP APK and Android `.so` packaging/linking |
| iOS arm64 | macOS | Xcode project generation plus unsigned device `xcodebuild` static link |
| WebGL | Windows | WebAssembly player and static wasm linker compatibility |

The matrix planner schedules only the requested manual target instead of
allocating no-op jobs on every runner. Every selected job verifies the exact
Unity editor and playback-engine module before building and fails if either is
missing. Provision all listed Unity `6000.4.0f1` modules before relying on the
scheduled full-matrix signal.

## Allocation Bar

Normal frame paths must allocate no managed memory after explicit warmup:

- Use `NowDrawList.Warmup(...)` or `NowRenderer.Warmup(...)` with a
  representative frame before measuring steady state.
- Use the input-aware warmup overloads when the representative frame includes
  controls that depend on `NowInput`; they install the same provider/surface
  shape the measured frame will use.
- For data-backed controls with known stable ids, call
  `NowControlState.Warmup<T>(id)` during initialization to create the slot
  outside the first interactive frame.
- Warm dynamic font glyphs, Lottie geometry, effect render textures, material
  batches, world-space material instances, and any user-owned buffers.
- When glass diagnostics are enabled, call
  `NowGlassSettings.ReserveDiagnostics(maxPanesPerFrame)` during initialization
  and read entries with `TryGetLastFrameDiagnostic` or
  `CopyLastFrameDiagnosticsTo` into caller-owned storage.

## Asset Store Prep

- Keep customer examples under `Samples~`; avoid shipping internal tests as
  imported sample content.
- Validate with Unity's Asset Store Publishing Tools before upload.
- Keep third-party notices current for bundled fonts, emoji/Lottie assets, and
  native plugin dependencies.
- The current repo intentionally does not rename `Assets/NowUI` to the package
  technical name during this pass; account for that in publisher-portal upload
  or a separate export step.
