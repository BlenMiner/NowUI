# Performance report — 2026-07-13

## Outcome

The two optimized paths are faster in new isolated benchmarks:

- Repaint-state tracking for 1,000 inactive controls improved 12.0%, from a
  0.3509 ms median of run medians to 0.3088 ms.
- Updating 1,000 settled transitions improved 6.1%, from 0.1382 to 0.1298 ms.

Each launch used 10 warmups and 50 measurements; each side used three separate
Unity launches and no launch was discarded. Steady-state GC allocation remained
0 bytes.

The broader 1,000-control frame-build benchmark was inconclusive on this busy
desktop: baseline medians were 3.47 and 3.51 ms, while exact-final runs
ranged from 3.33 to 3.75 ms as unrelated control timings varied with machine
load. No end-to-end or suite-wide speedup is claimed from those results.

## Optimization

- `NowControlState.Transition` now uses one realtime sample for state lookup,
  lifetime tracking, and animation advancement. Previously each non-passive
  call sampled Unity's realtime clock twice.
- `NowControls.Interact` does not materialize `InteractionRepaintState` until a
  control is hovered, held, or focused. It resets the state on the final
  active-to-idle edge, while still requesting the repaint retained hosts need
  for that edge. The cleared entry remains cached for allocation-free reuse.
- With the pointer outside all 1,000 controls, the new isolated benchmark avoids
  populating the repaint-state dictionary with 1,000
  `Entry<InteractionRepaintState>` objects and avoids updating them every frame.

The transition state itself remains materialized while idle. An earlier lazy
version measured a larger end-to-end gain but delayed the first hover fade by
one frame, so it was rejected. An experimental bulk submesh submission change
was also rejected because its isolated benchmark did not improve repeatably.

## Method

Baseline runtime commit: `f5d01471b822d413f684b322573d8e5b7cb105c0`

Environment:

- Unity 6000.4.0f1 (`8cf496087c8f`)
- Windows 11 Pro 10.0.26200, build 26200
- AMD Ryzen 9 7900X, 24 logical processors
- 63.2 GiB RAM
- Ultimate Performance power plan

`InteractionRepaintTrackingStress` isolates repaint-state tracking from
rendering, layout, transitions, and string hashing. `TransitionTimingStress`
isolates settled transition state lookup and clock sampling. The test code was
held constant while each affected runtime path was switched between its exact
baseline and final implementation. Each side ran in three separate Unity
launches. The reported value is the median within each launch, followed by the
median of those three launch medians. This keeps all runs while limiting the
influence of desktop scheduling outliers.

The final canonical filter now contains 54 performance/allocation tests and 46
timed metrics:

```powershell
.\Tools\NowUI-Harness.ps1 `
  -Mode EditMode `
  -Filter 'NowPerformanceTests|NowRuntimePerformanceTests|NowExtensionsPerformanceTests' `
  -ArtifactsPath <output-directory>

python .\Tools\perf\parse_perf_xml.py `
  <output-directory>\EditMode\NowUI-EditMode-results.xml `
  <output-directory>\perf-tests.json
```

Isolated A/B artifacts:

- Repaint baseline: `artifacts/benchmark-optimize-20260713/isolated-baseline1`
  through `isolated-baseline3`
- Repaint candidate: `artifacts/benchmark-optimize-20260713/isolated-candidate1`
  through `isolated-candidate3`
- Transition baseline: `artifacts/benchmark-optimize-20260713/transition-baseline1`
  through `transition-baseline3`
- Transition candidate: `artifacts/benchmark-optimize-20260713/transition-candidate1`
  through `transition-candidate3`

## Results

| Metric | Baseline run medians | Candidate run medians | Median of run medians | Change |
| --- | ---: | ---: | ---: | ---: |
| `InteractionRepaintTrackingStress` | 0.3469, 0.5605, 0.3509 ms | 0.3224, 0.3088, 0.3068 ms | 0.3509 → 0.3088 ms | -12.0% |
| `TransitionTimingStress` | 0.1333, 0.1572, 0.1382 ms | 0.1298, 0.0981, 0.1348 ms | 0.1382 → 0.1298 ms | -6.1% |
| Steady-state `GC.Alloc` | 0 B | 0 B | 0 → 0 B | unchanged |

The baseline's slower second launch and a 45.02 ms individual sample in the
first candidate launch are both retained. Fifty-sample launch medians and the
three-launch aggregate prevent either outlier from controlling the result.
These results apply to the isolated state paths, not to a complete rendered UI
frame.

## Validation

- Runtime and test assembly compile check: passed.
- Canonical performance filter: 54 passed, 0 failed.
- Full EditMode: 864 passed, 0 failed, 1 ignored. The ignored test requires an
  `EventSystem.current`, which is unavailable in the batch environment.
- Full PlayMode: 18 passed, 0 failed.
- Added regression coverage for immediate idle-to-active transition timing,
  lazy first activation, cached state reuse, and the isolated performance path.

Final suite artifacts are in `final-54-canonical`, `final-54-editmode`, and
`final-54-playmode` under `artifacts/benchmark-optimize-20260713`.
