# Feature benchmark overview — 2026-09-06

The [follow-up pass](BenchmarkFollowup-2026-09-06.md) adds indexed graph
evaluation and clipped rich-text comparisons, hardens the older allocation
gates, and resolves the three test failures recorded below. This report
preserves the original expansion measurements.

The expanded suite adds **75 feature cases and one allocation-counter calibration**:
24 SDF CPU, 15 document, 9 graph-evaluation, 12 cache, and 15 rendering cases.
Together with the existing suite, the final report contains **157 measured cases**
across three launches per platform. One existing allocation-byte test is skipped
in each launch because this Unity Mono runtime does not implement its byte counter.

The strongest follow-up targets are deep graph evaluation, large rich-text
layout/drawing, gradient rebaking, and working sets that overflow the Lottie
geometry cache. SDF CPU construction is comparatively small in these scenes;
its GPU cost grows substantially with shape count, covered pixels and overlap.
No runtime implementation changed during this benchmark expansion.

## Reproduce and inspect

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode Benchmark -BenchmarkRuns 3 `
  -ProjectPath D:\wkspaces\unity\Now-UI `
  -ArtifactsPath artifacts/benchmarks/all-features
```

See the [benchmark guide](Benchmarks.md) for narrower filters and measurement
boundaries. The measured checkout is `a74f3e3` plus this benchmark expansion:
Unity 6000.4.0f1, Windows 11, Ryzen 9 7900X, RTX 4080 SUPER, Direct3D 11,
Mono, Editor batchmode. Unity reports single-threaded rendering. The physical
project path is on D:, with the C: workspace path resolving through a link.

Final artifacts are under `artifacts/benchmark-overview-20260906/final`:

- [Complete tables](../artifacts/benchmark-overview-20260906/final/overview.md).
- [Raw samples, per-run statistics and aggregates](../artifacts/benchmark-overview-20260906/final/overview.json).
- [Revision, hardware and launch metadata](../artifacts/benchmark-overview-20260906/final/environment.json).

CPU XML/logs come from `verified/run1` through `run3`; rendering XML/logs come
from `render-warmed/run1` through `run3`. Earlier rendering runs are superseded.
Each final rendering case warms for at least one second and eight frames before
64 samples. This materially reduced launch variation compared with eight frames
alone. Animation phases restart consistently after warmup. GPU clocks are still
not locked, and these are machine-specific measurements, not player budgets.

Times below are milliseconds, using the median of three launch medians. Rows
have different workload sizes; compare a feature's scaling cases rather than
ranking unrelated rows. Full reports include launch ranges, worst per-run
p95/p99, maxima, geometry and cache counters. With 64 samples, p99 is the maximum;
it does not establish the frequency of rare hitches.

## Documents and graph evaluation

| Workload | CPU median | Allocation events/operation |
| --- | ---: | ---: |
| Code editor, 300 lines, steady | 0.7290 | 0 |
| Code editor, 10,000 lines, steady | 0.7299 | 0 |
| Code editor, 10,000 lines, equal-content replacement | 1.8675 | 1 |
| Code editor, 10,000 lines, edit at start | 1.9570 | 1 |
| Rich text, 32 spans, steady | 0.1129 | 0 |
| Rich text, 256 spans, steady | 0.8905 | 0 |
| Rich text, 2,048 spans, steady | 7.2535 | 0 |
| Rich text, 2,048 spans, width relayout | 10.4779 | 1 |
| Graph dependency chain, 16 nodes | 0.0092 | 0 |
| Graph dependency chain, 128 nodes | 0.4212 | 0 |
| Graph dependency chain, 512 nodes | 7.0096 | 0 |
| Shared 64-node chain, 64 roots, separate evaluations | 8.1448 | 0 |
| Shared 64-node chain, 64 roots, `BeginBatch` | 0.4615 | 0 |

The editor's steady draw stays around 0.73 ms because the visible viewport is
fixed. Caller-provided replacement strings expose document invalidation costs;
these cases do not simulate keyboard, selection or undo processing.

Rich text emits 40,960 vertices at 2,048 spans. This fixture uses a fixed
800×600 surface and a draw rect, but no explicit clip or scroll scope; the
rich-text draw rect sets placement and wrapping rather than a clipping
viewport. The result establishes whole-document emission cost, not a viewport
culling bug. Add an explicitly masked or scrolling companion and compare
geometry, rendered output and timing before choosing a culling optimization;
the text draw path already rejects runs outside an active ambient mask.
Relayout launch medians span 10.471–10.635 ms, with worst-run p99 of 12.153 ms.

Increasing graph depth from 128 to 512 nodes multiplies time by about 16.6 while
node count rises fourfold. The evaluator's list scans are a plausible cause to
profile next. Batching shared roots cuts handler calls from 4,160 to 128 and
reduces time about 17.6×; this is an existing API behavior that the old canvas
drawing benchmarks did not measure.

## Cache pressure and cold work

| Workload | CPU median | Allocation events/batch or submission |
| --- | ---: | ---: |
| 256 stable press-state slots | 0.0239 | 0 |
| 256 newly admitted press-state slots | 0.0471 | 256 median |
| 64 gradient cache hits | 0.0018 | 0 |
| 64 gradient revisions, rebake and upload | 1.8804 | 0 |
| Lottie geometry, 32 cached frame keys | 0.0009 | 0 |
| Lottie geometry, 40 keys overflowing the 32-entry cache | 2.7371 | 0 |
| SDF image, one cached field | 0.0058 | 0 |
| SDF image, source invalidated | 0.0860 | 22 |
| SDF image, cold scene/field/atlas caches | 0.4389 | 267 |
| SDF mask, alternating target width | 0.1009 | 27 |

Lottie batches request 32 or 40 frames respectively, with native tessellation
available. This is a cache-capacity cliff, not a same-sized-workload speed ratio.
Gradient revision p95 reaches 5.648 ms; batching uploads is a useful profiling
candidate. The full-atlas test separately verifies rejected requests, so its
very short time cannot be mistaken for successful gradient rendering.

The press-state soak retains all 256 live slots, observes cleanup, and drops
from peaks of 11,280–11,424 slots to 3,280–3,400 while new slots continue to
arrive. Its CPU batch median is 0.0766 ms and worst-run p95 is 0.1188 ms. Sleep
and the approximately 12-second elapsed soak duration are outside CPU samples.

SDF source uploads and cache resets happen outside timers; the resulting field
bake, cache admission and internal GPU submission happen inside. These CPU
figures do not include waiting for completed GPU work or the cost of generating
the source image itself.

## Completed rendering and GPU draw cost

| Workload | GPU median | GPU launch medians (min-max) | Completion median |
| --- | ---: | ---: | ---: |
| Clear 512x512 | Unavailable | - | 0.0673 |
| 256 rectangles, 512x512 | 0.0041 | 0.0041 - 0.0041 | 0.2540 |
| 100 text labels, 512x512 | 0.0041 | 0.0041 - 0.0041 | 0.3145 |
| SDF 16 shapes, 256x256 | 0.0307 | 0.0307 - 0.0307 | 0.1464 |
| SDF 1 shape, 512x512 | 0.0143 | 0.0143 - 0.0143 | 0.1333 |
| SDF 16 shapes, 512x512 | 0.0737 | 0.0727 - 0.0737 | 0.1960 |
| SDF 64 shapes, 512x512 | 0.2232 | 0.2232 - 0.2232 | 0.3491 |
| SDF 16 shapes, 1024x1024 | 0.2028 | 0.2028 - 0.2038 | 0.3330 |
| SDF 16 shapes x 8 overlapping scenes, 512x512 | 0.4024 | 0.3994 - 0.4035 | 0.5924 |
| SDF animated morph, 512x512 | 0.1157 | 0.1157 - 0.1157 | 0.2395 |
| SDF animated effects, 512x512 | 0.0922 | 0.0911 - 0.0932 | 0.2162 |
| SDF animated full-resolution mask, 512x512 | 0.0061 | 0.0061 - 0.0061 | 0.2287 |
| SDF animated half-resolution mask, 512x512 | 0.0072 | 0.0061 - 0.0072 | 0.1903 |
| Glass, 1 capture, 512x512 | 0.0778 | 0.0768 - 0.0973 | 0.3862 |
| Glass, 8 captures, 512x512 | 0.4188 | 0.4178 - 0.4188 | 1.0403 |

`GPU.FinalDraw` times the target clear and final command buffer. Mask captures
submitted during build are outside that marker. `Frame.Completion` includes UI
construction, submission, GPU/driver waiting and a synchronous one-pixel readback.
No PNG encoding or disk writes are timed. Neither metric measures presentation
latency or an ordinary pipelined game frame.

All 14 content workloads have 64 valid GPU samples in each launch. The clear-only
control produced zero positive GPU timestamps in two launches and one in the
third; its GPU aggregate is unusable, so the table reports it as unavailable.
The raw report preserves that observation and flags incomplete optional data.
The clear control's completion latency provides context, not a value to subtract
from every workload.

The shape-count and resolution cases now expose work that CPU construction
alone cannot describe. Eight glass panes use distinct blur keys and verify eight
captures, rather than accidentally sharing one blur. Glass with eight captures
still has a worst-run GPU p95 of 0.727 ms; inspect the tails as well as medians.

## Allocation instrumentation correction

This runtime returns zero from `GC.GetAllocatedBytesForCurrentThread()` even
after an explicit 4 KiB allocation. The new helper validates that API, falls back
to a current-thread `GC.Alloc` profiler event counter, and records support flags.
Calibration checks allocating, empty, and three-allocation regions without
advancing a frame, verifying that samples reset and scale correctly.

The new numbers above are **allocation events, not bytes or GC collections**.
Steady SDF, document, graph and rendering cases recorded no events in their
measured regions. Admissions, image rebakes and document relayouts expose real
events. This does not measure allocations on other threads or native/GPU memory.

Older byte metrics without a known-allocation probe are labeled unverified in
the report. Their zeros cannot establish allocation-free execution. The earlier
[optimization report](PerformanceReport-2026-09-06.md) now carries this correction;
its timing and visual evidence remains separate from the invalid byte readings.

## Validation and remaining gaps

- Three benchmark launches per platform: 471 passed executions; the same existing
  byte-allocation test skipped three times. All 75 new feature cases and the
  calibration pass. The clear-only GPU metric is explicitly incomplete.
- Full EditMode: 1,810 passed, 2 ignored, 3 previously known failures in animation
  showcase expectations and shader stereo source checks. No new failures.
- Full PlayMode: 159 passed, 4 ignored, no failures.
- Eleven Python report tests pass; PowerShell parsing, package-boundary checks
  and whitespace checks pass. Benchmark code remains outside the shipped package.

Built players/IL2CPP, mobile, XR, URP/HDRP hosts, actual editing gestures,
multilingual glyph-cache pressure, SDF text/templates, native memory and longer
application soaks remain gaps. The [guide](Benchmarks.md#remaining-gaps) lists
them explicitly. These results identify where to profile next; they do not
replace measurements on the library's target hardware and host configurations.

