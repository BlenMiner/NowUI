# Production Gates

NowUI targets Unity `6000.4` or newer. The package is developed under
`Assets/NowUI` as `com.blenminer.nowui`; Asset Store UPM product setup is
handled in the publisher portal.

## Local Validation

Close any open Unity editor for this project before running batchmode tests.
Unity refuses to open the same project twice.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'C:\wkspaces\unity\Now-UI' `
  -runTests `
  -testPlatform EditMode `
  -testResults 'Temp\NowUI-EditModeResults.xml' `
  -logFile 'Temp\NowUI-EditMode.log'
```

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'C:\wkspaces\unity\Now-UI' `
  -runTests `
  -testPlatform PlayMode `
  -testResults 'Temp\NowUI-PlayModeResults.xml' `
  -logFile 'Temp\NowUI-PlayMode.log'
```

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
- `Tools/Assert-NowUIVisualArtifacts.ps1` validates the manifest, PNG headers,
  dimensions, file sizes, and nonzero batch/vertex counts.
- The Windows runner also executes `-Mode Golden` to compare canonical captures
  against `Assets/NowUI/Tests/Baselines/Visual`.
- All captures are uploaded as workflow artifacts for inspection.

The cross-OS jobs require Unity `6000.4.0f1` on self-hosted runners with the
standard GitHub runner OS labels (`Windows`, `macOS`, `Linux`). Linux visual
runners must provide a graphics-capable session or virtual display; do not run
the visual harness with `-nographics`.

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
