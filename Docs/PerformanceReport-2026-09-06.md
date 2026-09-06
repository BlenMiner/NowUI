# Optimization pass — 2026-09-06

## Measured outcome

Labels take 12–15% less CPU time, the 1,000-row scroll workload takes 17%
less, and the 1,000-control frame plus batch-heavy upload take about 8%
less. All ten recorded steady-state allocation counters remain at 0 B/frame.
All 52 before/after visual captures are byte-identical, with unchanged batch
and vertex counts.

Times below are milliseconds, using the median of three launch medians:

| Workload | Before | After | Change |
| --- | ---: | ---: | ---: |
| ManyControlsStressFrameBuild | 2.7421 | 2.5298 | -7.7% |
| ScrollViewThousandRowsFrameBuild | 0.9850 | 0.8148 | -17.3% |
| LabelPipelineRepeatedStrings | 0.7745 | 0.6608 | -14.7% |
| LabelPipelineUniqueStrings | 0.7311 | 0.6273 | -14.2% |
| LabelPipelineMultilineStrings | 1.7327 | 1.5275 | -11.8% |
| TextFrameBuild | 0.3895 | 0.3405 | -12.6% |
| DocsTextRowsFrameBuild | 1.1345 | 0.9950 | -12.3% |
| DocsTextRowsWithoutShapingFrameBuild | 1.1851 | 0.9720 | -18.0% |
| BatchHeavyMeshUpload | 0.8917 | 0.8244 | -7.5% |
| InteractionRepaintTrackingStress | 0.3898 | 0.3601 | -7.6% |
| DocsCompositeGraphicRebuild | 0.7596 | 0.7003 | -7.8% |
| ActualDocsGlassDemoGraphicRebuild | 1.8756 | 1.7645 | -5.9% |
| ActualDocsOverviewGraphicRebuild | 1.7203 | 1.6403 | -4.6% |
| NodeGraphCanvas100 | 3.4663 | 3.2941 | -5.0% |
| NodeGraphCanvas400 | 3.9345 | 3.8330 | -2.6% |
| RectangleFrameBuild | 0.5042 | 0.4970 | -1.4% |

The smaller node-graph and rectangle changes should not be treated as robust
wins: the 400-node and rectangle launch ranges overlap. Unchanged cold
Markdown/Markup parsing moved +4.3%/+3.5%, illustrating residual launch
variation. The main control, label, scroll, and upload results have separated
before/after launch ranges. These numbers measure the combined pass, not the
isolated contribution of each individual change.

## Changes

- Interned call sites now retain the full 64-bit file/line hash. Resolution
  combines that payload with the current owner, scope, and domain without
  rescanning the caller's file path. The identity hash format, occurrence
  handling, and deferred validation remain unchanged.
- Control rendering reads the required style fields without repeatedly
  copying the entire style set. Values used across user callbacks retain
  their original snapshot semantics. Private interaction-repaint state uses
  its already isolated control ID without deriving an extra child ID.
- Direct font and font-family hits skip the fallback traversal HashSet in
  font resolution, line-height lookup, and ascender lookup. Fallback misses
  retain virtual selection, cycle guards, style order, and exception cleanup.
- Captured meshes append indices directly in the selected 16- or 32-bit
  upload format and upload their ordered submesh descriptors in one call.
  Material ordering, geometry, bounds, canvas pages, and the index-format
  boundary are preserved.
- The performance XML parser uses Unity's full-precision structured timing
  records instead of the rounded display summary, and excludes failed tests.

The larger interleaved-vertex rewrite, transformed analytic curves, and
shader changes remain outside this pass.

## Measurement method

The baseline is commit `ed1ca4c`. Measurements use Unity `6000.4.0f1`, an AMD
Ryzen 9 7900X, the Ultimate Performance power plan, and Direct3D 11 on an
NVIDIA RTX 4080 SUPER. Runs use the existing 54-test canonical filter, with
three independent Unity launches for each implementation. Reported times are
the median of the three launch medians. They measure Unity Editor CPU work;
they are not player, mobile, IL2CPP, or GPU frame-time claims.

The initial runs failed to load existing font/theme assets because Unity's
generated script-to-type mapping was stale. Using the physical project path
and reimporting assets alone did not repair it; rebuilding `Library` did.
The first successful runs were noisy while a game was running. The comparison
uses a fresh baseline captured after the user closed the game, with all
launches using the physical `D:/wkspaces/unity/Now-UI` path rather than its
`C:/wkspaces` junction.
The previous generated cache remains at
`artifacts/optimization-20260906/Library-before-reimport` for rollback.

Artifacts are under `artifacts/optimization-20260906`:

- `baseline-quiet1` through `baseline-quiet3`: comparable original-code runs.
- `candidate-quiet1` through `candidate-quiet3`: final implementation runs;
  54/54 tests pass in every baseline and candidate launch.
- `baseline-visual`: 52 captures of the original implementation.
- `candidate-visual` and `visual-comparison.json`: 52 byte-identical final
  captures.
- `candidate-play`: 144 passed, 4 ignored, 0 failed.
- `final-edit-verified`: 1,749 passed, 2 ignored, and the 3 existing failures
  listed below. All 27 new regression cases pass.
- `baseline-existing-failures`: reproduction of existing validation failures
  with every runtime optimization reverted.
- `comparison.json`: full-precision per-launch timing and allocation data.

```powershell
./Tools/NowUI-Harness.ps1 -Mode EditMode `
  -ProjectPath D:/wkspaces/unity/Now-UI `
  -Filter 'NowPerformanceTests|NowRuntimePerformanceTests|NowExtensionsPerformanceTests' `
  -ArtifactsPath artifacts/optimization-20260906/<run>
```

## Correctness and allocation validation

- The 27 new cases cover call-site hash compatibility and deferred validation,
  font styles/fallbacks/cycles/destroyed objects/virtual dispatch, theme
  mutation during user callbacks, ordered mixed-geometry submeshes,
  native/managed packing, nonzero offsets, the 16-/32-bit index boundary,
  and large-to-small-to-empty rebuilds.
- All 54 canonical tests pass in all six comparable benchmark launches.
  Every recorded `GC.Alloc` sample remains zero, and the existing allocation
  assertions pass.
- All 52 PNGs match byte-for-byte. Batch counts, vertex counts, and image
  dimensions also match for every capture.
- PlayMode: 144 passed, 4 ignored, no failures.
- Full EditMode: 1,749 passed, 2 ignored, 3 pre-existing failures. The same
  three failures reproduce against the original code; this is not a clean
  full-suite release gate.
- `Tools/Assert-NowUIPackageBoundary.ps1` and `git diff --check` pass.

## Existing validation failures

Three EditMode failures reproduce with the original runtime and test source:

- `NowHarnessAnimationTests.ReadmeShowcasesAreRegisteredAsFourSecondLoops`
  still expects three four-second animations. The catalogue contains six,
  including the five-second music player.
- `NowShaderStereoTests.EveryPackageShaderVertexProgramSupportsStereoInstancing`
  does not account for the shared `CGINCLUDE` that supplies `UnityCG.vert_img`
  to the SDF image-field baking shader.
- `NowShaderStereoTests.XrRenderedGeometryProgramsGenerateInstancingVariants`
  treats that offscreen blit shader as an XR geometry shader; the existing
  flat-blit exemptions predate its addition.

These tests and the implicated showcase/shader sources are unchanged by this
optimization pass.
