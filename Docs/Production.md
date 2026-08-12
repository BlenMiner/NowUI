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

Unity editor validation is a local release gate. The repository does not queue
Unity jobs in GitHub Actions because standard GitHub-hosted runners do not
provide the licensed editor and platform modules required by these tests. The
`.github/scripts/Run-UnityTests.ps1` helper remains available for local or
externally provisioned automation. Do not pass `-quit` to Unity test runs; the
Unity Test Framework exits batchmode after writing results.

## Visual Validation

Run the editor visual harness locally as a separate rendering gate:

- `Tools/NowUI-Harness.ps1 -Mode Visual` produces PNG captures and a
  `manifest.json` under `artifacts/local/visual`.
- `Tools/Assert-NowUIVisualArtifacts.ps1` validates the manifest, unique capture
  names, required scenario names, PNG headers, dimensions, file sizes, and
  nonzero batch/vertex counts.
- `Tools/NowUI-Harness.ps1 -Mode Golden` compares canonical captures
  against `Assets/NowUITests/Baselines/Visual`. Landing-page scenarios use a
  stricter per-scenario mismatch ceiling than the general harness to catch a
  missing small control.

Cross-OS visual validation requires Unity `6000.4.0f1` and a graphics-capable
session or virtual display. Do not run the visual harness with `-nographics`.

## Render-Pipeline Validation

The repository remains a Built-in Render Pipeline project so optional SRP
dependencies do not become mandatory for package users. For local release
validation, use disposable URP and HDRP workspaces:

- `.github/scripts/Set-RenderPipelineValidation.ps1` installs the matching
  `17.4.x` package for Unity `6000.4` and removes the other SRP package.
- Each job requires `NowUI.URP` or `NowUI.HDRP` to compile and load. The
  `NOWUI_EXPECT_RENDER_PIPELINE` assertion fails if an optional assembly is
  absent, so package resolution cannot turn into an ignored success.
- The URP job also renders model previews and a renderer-feature frame and
  checks the resulting pixels. The HDRP job currently provides compile/load
  compatibility coverage for its thin custom-pass wrapper; a deterministic
  HDRP frame fixture remains future work.

## Player-Build Matrix

Player builds are a local release gate and require the matching Unity editor
platform modules:

| Target | Runner | What the build verifies |
| --- | --- | --- |
| Windows x64 | Windows | IL2CPP player and Windows native plugin import |
| Linux x64 | Linux | IL2CPP player and Linux native plugin import |
| macOS | macOS | IL2CPP player and universal native plugin import |
| Android arm64 | Windows | ARM64 IL2CPP APK and Android `.so` packaging/linking |
| iOS arm64 | macOS | Xcode project generation plus unsigned device `xcodebuild` static link |
| WebGL | Windows | WebAssembly player and static wasm linker compatibility |

Use `NowUI.EditorCI.NowBuildVerification.Build` in batch mode for each target.
Provision all listed Unity `6000.4.0f1` modules before relying on the result.

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

- Run `pwsh -File Tools/Assert-NowUIPackageBoundary.ps1` to verify repository
  tests and harness code remain outside `Assets/NowUI`, the npm manifest, and
  the configured `.unitypackage` root. GitHub runs this on pull requests and
  again before semantic-release creates a release.
- Keep customer examples under `Samples~`; avoid shipping internal tests as
  imported sample content.
- Validate with Unity's Asset Store Publishing Tools before upload.
- Keep third-party notices current for bundled fonts, emoji/Lottie assets, and
  native plugin dependencies.
- The current repo intentionally does not rename `Assets/NowUI` to the package
  technical name during this pass; account for that in publisher-portal upload
  or a separate export step.
