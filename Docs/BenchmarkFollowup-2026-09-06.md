# Benchmark follow-up — 2026-09-06

The follow-up adds an opt-in indexed graph evaluation scope, makes the older
allocation gates reject unsupported counters, and fixes the three outstanding
test failures. Six clipped rich-text cases establish viewport costs alongside
the existing whole-document measurements. The overview now covers 90 feature
cases plus allocation calibration.

## Indexed graph evaluation

`NowNodeGraphEvaluator<T>.BeginIndexedBatch(graph)` builds node-ID and input-link
indexes once per outer scope and shares output memoization across its roots.
It reuses storage after warmup. The measured operation includes rebuilding and
clearing the index; graph construction and string-ID setup are outside timing.

The graph exposes mutable lists and IDs. Callers must keep topology, IDs and
ports stable within an indexed scope. Direct edits, list replacement and
reordering take effect at the next scope. Duplicate IDs and input links retain
first-entry resolution. Existing `Evaluate` and `BeginBatch` keep their live
lookup behavior, including handlers that edit topology while evaluating.
See the [public API guide](../Assets/NowUI/Documentation~/NodeGraph.md) for usage.

Paired workloads ran in the same build across three separate Unity launches.
Times are milliseconds, expressed as the median of launch medians; each launch
collects 64 timing samples per case.

| Workload | Live lookup | Indexed lookup | Speed ratio |
| --- | ---: | ---: | ---: |
| Dependency chain, 16 nodes | 0.00940 | 0.00620 | 1.5x |
| Dependency chain, 128 nodes | 0.42975 | 0.05150 | 8.3x |
| Dependency chain, 512 nodes | 7.06360 | 0.21745 | 32.5x |
| Shared 64-node chain, 64 roots, separate evaluations | 8.25270 | 2.11775 | 3.9x |
| Shared 64-node chain, 64 roots, shared memoization | 0.46945 | 0.05680 | 8.3x |

The 512-node indexed launch medians range from 0.21550 to 0.23355 ms,
versus 7.04640 to 7.14415 ms for live lookup. Worst per-run p95 is 0.2721 ms
indexed and 7.2324 ms live. All graph cases report zero verified current-thread
allocation events after warmup, including index construction and disposal.

Handler counts and changing source values verify that both lookup modes do
the same work. The 512-node stress case explicitly raises the default depth
limit. Shared memoization still reduces handler calls from 4,160 to 128 for
64 roots; the new index improves lookup cost within either memoization mode.
Thirteen correctness cases cover mutations between scopes, live handler
mutations, duplicate resolution, ports, fallbacks, cycles, depth limits,
nested scopes, mode guards, exception recovery and warmed allocations.

## Rich text with an explicit viewport

The six companion cases use the original text, spans and 760-pixel document
width inside a 380x280 mask. Stable and scrolling variants preserve the full
retained layout. Scrolling cycles fractional translations and includes partial
glyph clipping. Full-image parity checks run outside timing against a cropped
render of the complete document.

| Steady document | Whole-document CPU | Clipped CPU | Clipped scrolling CPU | Whole / clipped vertices |
| --- | ---: | ---: | ---: | ---: |
| 32 spans | 0.11500 | 0.09730 | 0.10205 | 640 / 376 |
| 256 spans | 0.90145 | 0.69490 | 0.72130 | 5,120 / 2,712 |
| 2,048 spans | 7.40475 | 2.01805 | 2.10240 | 40,960 / 3,008 |

Vertex counts in the last column describe the stable clipped frame; scrolling
can expose different runs. All steady and scrolling cases report zero verified
allocation events. The 2,048-span clipped launch medians range from 2.01755 to
2.02680 ms; scrolling ranges from 2.10230 to 2.10670 ms.

This is evidence for the existing clipping behavior, not a runtime rich-text
optimization. The whole-document fixture had no ambient clip. The new results
show substantially less geometry in a viewport while preserving boundary
pixels. Clipped CPU cost still grows with retained document size, so reducing
traversal or layout cost remains a possible future profiling task.

## Allocation gates and test corrections

All legacy direct-byte gates and allocation metrics now use the verified
helper. A deliberate allocation must be detected before a zero is accepted.
When the byte API fails its probe, a current-thread profiler recorder must
detect one allocation and then an empty region correctly. Its output is
`GC.Alloc.Calls`, never a byte estimate. Zero-allocation assertions skip when
neither backend works; byte budgets require verified bytes and explicitly skip
when those are unavailable.

The capture smoke runner has its own byte probe without depending on a test
assembly. It reports `allocationBytesAvailable: false` and
`allocatedBytes: null` on this Mono runtime. Regression tests distinguish this
from an available counter reporting a real zero. Historical zero-byte readings
remain inconclusive.

Enabling real allocation detection exposed three fixture issues: the overlay
test created a delegate inside the measured frame, and two Markdown tests
warmed different caller-line control identities than they measured. The
fixtures now prepare the delegate and warm the same callsite across frames;
their zero-allocation assertions remain intact.

The three previously known failures are resolved:

- Animation expectations now match all six declared showcases, including the
  five-second music-player loop, with frame-count and endpoint checks.
- Shader checks resolve shared `CGINCLUDE`/`HLSLINCLUDE` code and Unity's
  `vert_img` helper while preventing declarations from leaking between sibling
  scopes.
- The five flat image-field baking passes are recognized as offscreen blits;
  XR geometry still requires stereo and instancing support.

## Validation and artifacts

- Full EditMode: **1,851 passed, zero failed, two skipped**. The skips are an
  unavailable EventSystem and the unsupported allocation-byte budget.
- Full PlayMode: **159 passed, zero failed, four skipped**. Two require URP;
  two require an XR display that produces render passes in this environment.
- Repeated graph/document benchmarks: **39 cases x three launches, all 117
  executions passed**, with no missing-data issues.
- Eleven Python report tests, PowerShell parsing, package-boundary validation
  and whitespace checks pass.

Measured environment: `a74f3e3` plus the benchmark expansion and this follow-up,
Unity 6000.4.0f1, Windows 11, Ryzen 9 7900X, RTX 4080 SUPER, Direct3D 11,
Mono Editor batchmode with single-threaded rendering. Runs use the physical
`D:\wkspaces\unity\Now-UI` path. These CPU timers exclude startup, imports and
disk I/O. Results are specific to this machine and runtime; player/IL2CPP and
other host/device gaps from the [benchmark guide](Benchmarks.md) remain.

- [Three-run tables](../artifacts/benchmark-followup-20260906/benchmarks/overview.md)
  and [raw samples](../artifacts/benchmark-followup-20260906/benchmarks/overview.json).
- [Environment and launch metadata](../artifacts/benchmark-followup-20260906/benchmarks/environment.json).
- [Full EditMode results](../artifacts/benchmark-followup-20260906/final-validation/EditMode/NowUI-EditMode-results.xml)
  and [full PlayMode results](../artifacts/benchmark-followup-20260906/final-validation/PlayMode/NowUI-PlayMode-results.xml).

Reproduce the focused comparison with:

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode Benchmark `
  -ProjectPath D:\wkspaces\unity\Now-UI `
  -BenchmarkPlatform EditMode -BenchmarkRuns 3 `
  -Filter 'NowGraphEvaluationPerformanceTests|NowDocumentPerformanceTests' `
  -ArtifactsPath artifacts/benchmarks/graph-document-followup
```
