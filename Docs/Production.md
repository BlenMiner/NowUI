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
  -logFile 'Temp\NowUI-EditMode.log' `
  -quit
```

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'C:\wkspaces\unity\Now-UI' `
  -runTests `
  -testPlatform PlayMode `
  -testResults 'Temp\NowUI-PlayModeResults.xml' `
  -logFile 'Temp\NowUI-PlayMode.log' `
  -quit
```

For CI, `.github/workflows/unity-tests.yml` runs the same commands through
`.github/scripts/Run-UnityTests.ps1` on a self-hosted Windows runner with Unity
`6000.4.0f1`.

## Allocation Bar

Normal frame paths must allocate no managed memory after explicit warmup:

- Use `NowDrawList.Warmup(...)` or `NowRenderer.Warmup(...)` with a
  representative frame before measuring steady state.
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
