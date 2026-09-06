# Benchmark coverage and measurement

Use `Benchmark` mode to compare feature costs and scaling. It runs Unity
performance tests and produces a report with separate timing, allocation and
workload metrics. The new `NowUI.Overview` category adds SDF scaling, document
updates, graph evaluation, cache pressure and completed rendering to the
existing core and extension benchmarks.

The overview contains 90 feature cases and one allocation-counter calibration
case. The calibration deliberately allocates a retained 1,024-byte array in
each sample and verifies that the selected counter detects it; its allocations
are instrumentation work, not NowUI's allocation footprint.

The tables below describe coverage, not measured rankings. Keep results from
each machine and revision with the generated artifacts.

## Running the suite

The harness requires PowerShell 7, Python 3 and the Unity version recorded in
`ProjectSettings/ProjectVersion.txt`. Python reporting uses the standard
library. Set `UNITY_EDITOR` or pass `-UnityEditor` for a nonstandard editor
installation; pass `-Python` for a specific Python executable.

Close this project's interactive Unity editor before batch runs. Use the
physical project directory consistently: on this workstation it is
`D:\wkspaces\unity\Now-UI`; `C:\wkspaces\unity\Now-UI` points there through a
filesystem link. On another machine, replace `-ProjectPath` with its physical
checkout path. Imports and startup are outside the individual benchmark timers,
but competing disk activity, compilation and background CPU/GPU work can still
disturb measurements. Pause games and other substantial workloads, let imports
finish, and use the same power settings for comparisons.

Run three independent launches per platform for the expanded overview:

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode Benchmark `
  -ProjectPath D:\wkspaces\unity\Now-UI `
  -Category NowUI.Overview -BenchmarkRuns 3 -BenchmarkPlatform Both `
  -ArtifactsPath artifacts/benchmarks/overview
```

Omit `-Category` to include all tests tagged `Performance`, including the
existing core, extension, mask and model-preview suites:

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode Benchmark `
  -ProjectPath D:\wkspaces\unity\Now-UI `
  -BenchmarkRuns 3 -BenchmarkPlatform Both `
  -ArtifactsPath artifacts/benchmarks/all-features
```

`-BenchmarkPlatform` accepts `Both`, `EditMode` or `PlayMode`; `Both` is the
default. `-BenchmarkRuns` defaults to one and accepts 1–10. `-Filter` narrows
the selected category using Unity's test filter, for example:

```powershell
pwsh -File Tools/NowUI-Harness.ps1 -Mode Benchmark `
  -ProjectPath D:\wkspaces\unity\Now-UI `
  -Category NowUI.Overview -Filter NowSdfPerformanceTests `
  -BenchmarkRuns 3 -BenchmarkPlatform EditMode `
  -ArtifactsPath artifacts/benchmarks/sdf-cpu
```

Select the matching platform when filtering a fixture; the harness rejects
zero discovered tests. Rendering, SDF masks and image-field baking need a
graphics device. Keep graphics enabled rather than adding `-nographics`.

Each repetition writes its XML and log beneath
`<ArtifactsPath>/runN/EditMode` and/or `<ArtifactsPath>/runN/PlayMode`. The
combined report is `<ArtifactsPath>/overview.md`, with machine-readable data
and raw metric samples in `overview.json`. `environment.json` records the revision,
worktree status, launch options and available hardware details. Use distinct artifact directories
for baseline and candidate runs; reusing a directory replaces its run files.
Structured Unity run metadata is retained per XML, including the actual
rendering backend and execution settings. Pass `--metadata environment.json`
when regenerating a report to include the harness context too.

Regenerate or combine reports without launching Unity by passing explicit XML
paths. The output argument is a filename prefix:

```powershell
python Tools/perf/benchmark_report.py `
  --output artifacts/benchmarks/sdf-cpu/review `
  artifacts/benchmarks/sdf-cpu/run1/EditMode/NowUI-EditMode-results.xml `
  artifacts/benchmarks/sdf-cpu/run2/EditMode/NowUI-EditMode-results.xml `
  artifacts/benchmarks/sdf-cpu/run3/EditMode/NowUI-EditMode-results.xml
```

`-Mode Perf` remains the visual smoke runner. Its timer includes GPU readback,
PNG encoding and file writes. Use `Benchmark` for feature-cost comparisons;
use the visual/golden gates separately to check appearance. `-Mode All` retains
its existing validation and smoke sequence and does not invoke `Benchmark`.

## Expanded coverage

| Fixture | Workloads | What the checks and counters establish |
| --- | --- | --- |
| [SDF CPU](../Assets/NowUITests/NowSdfPerformanceTests.cs) | 1/16/64 shapes with reused or animated graphs; 1/16 graph layers; static/animated 16-to-16 morphs and combined effects; 16-shape masks with 16 child rectangles, stable/animated/resize and 0.5×/1×/2× resolution; cached 1/8 image fields, changed source and cold baking | Shape/layer counts, scene identity, submitted geometry, mask dimensions/pixels/rasterizations, image fields/bakes/atlas pixels |
| [Documents](../Assets/NowUITests/NowDocumentPerformanceTests.cs) | Code editor at 300/3,000/10,000 lines: steady repaint, equal-content replacement and edits at the beginning; 32/256/2,048 explicit rich-text spans: whole-document steady/width-relayout and explicitly clipped stable/scrolling views | Whole-document initialization, tokenized-line/validation counts, retained rich-text length/runs/lines, emitted geometry, clip-overlapping runs and untimed pixel parity against cropped full-document rendering |
| [Graph evaluation](../Assets/NowUITests/NowGraphEvaluationPerformanceTests.cs) | Dependency chains of 16/128/512 nodes; a shared 64-node chain feeding 4/16/64 roots; paired live/indexed lookups with and without shared output memoization | Exact handler counts and changing-source checksums; nodes, links, roots and index mode. Index construction/disposal are timed. The 512-node case explicitly raises the depth limit so it evaluates the entire chain |
| [Caches](../Assets/NowUITests/NowCachePerformanceTests.cs) | Idle/warm/new press-state slots and a 12–15 second cleanup soak; gradient hits, revision rebakes, admission and a full atlas; Lottie URL-cache hits/eviction and 32/40-frame geometry working sets | Retained/live/evicted state, rejected gradient requests, atlas footprint, cache occupancy, Lottie output vertices and native tessellation availability |
| [Rendering](../Assets/NowUITests/PlayMode/NowRenderPerformanceTests.cs) | Clear target, 256 rectangles and 100 text labels; SDF shape count 1/16/64, target sizes 256/512/1,024 and eight overlapping scenes; animated morph/effects/masks, half/full mask scale, one/eight glass panes with distinct blur radii | CPU build/submission, synchronized completion, optional GPU marker, allocation, target pixels, geometry, full-target pixel validation and glass blur-pass/copy diagnostics |

The SDF CPU layer cases stay within one scene; the rendering overlap case
draws eight scenes across the same target. They measure different forms of
complexity. Shape count and target size have separate rendering cases so
increasing covered pixels can be distinguished from increasing graph work.

Glass panes use different stable blur radii to force separate ordered captures
and blur work. An untimed replay checks the pane count, executed blur passes,
copied pixels and absence of fallback rendering. Every render workload also
receives a full-target pixel check outside the timing and allocation windows;
mask checks include rejected corner pixels. These checks keep missing draws,
empty geometry and cheap fallback paths from producing misleading timings.

Document fixtures use an 800×600 surface and a 760×560 draw rect, except the
rich-text relayout case alternates widths of 380 and 760. The editor manages
its own scrolling viewport. The original `RichTextCpuFrame` cases have no
explicit clip or scroll scope: their rect establishes placement and wrapping,
and the frame surface is not an ambient clip. All source strings, replacement
strings, span arrays, IDs and graph topology are prepared outside timing. Code edits
are caller-supplied replacements, so the result measures document invalidation
and rebuilding without charging the benchmark's string construction to NowUI.
The C# counting profile inherits the built-in language; its validation hook is
currently empty, so the validation count records invalidation rather than the
cost of a compiler or language server.

The original rich-text vertex counts establish whole-document emission under
that usage; they do not establish a culling bug in a clipped viewport. The six
`RichTextClippedCpuFrame` companions keep the same 760-pixel document width,
text and spans inside a fixed 380×280 `Now.Mask` viewport. Stable cases hold a
fractional horizontal offset. Scrolling cases cycle three translations, with
horizontal scrolling for every size and vertical scrolling when the document
exceeds the viewport height. Translation preserves the full retained layout;
these cases measure content drawing rather than scrollbar controls or input.

The companions check that geometry is bounded by mask-overlapping runs and
smaller than the full document. Outside timing and allocation windows, they
render both masked geometry and the complete unmasked contents. Cropping the
reference pixels independently verifies the final image, including glyphs
crossing a clip boundary. The existing text draw path already rejects runs
that do not overlap an active ambient mask; compare these results before
choosing any further culling optimization. These cases require graphics for
their correctness check even though their timed metric remains CPU frame build.

Cache measurements report one whole batch. Read `ControlsPerBatch`,
`RampRequestsPerBatch`, `Cache.*PerBatch` and `Lottie.RequestsPerBatch` alongside
the timing. The full-gradient-atlas case intentionally measures rejected
requests: a fast result there does not indicate successful gradient rendering.
The Lottie URL cases use preloaded assets and include no downloads. The state
soak paces logical batches with sleep outside timing, ages abandoned IDs through
the real cache lifetime, and verifies that live slots survive cleanup.

Existing suites remain useful for rectangles, text/shaping/layout, controls,
scrolling, curves, effects, glass replay, host rebuilding, Markdown/Markup
parsing and drawing, node-canvas rendering, docking, masks and model-preview
backends. They are included by the default `Performance` selection. The new
overview category supplements that coverage rather than changing those older
workloads or their measurement conventions.

## Reading timing and memory metrics

| Metric | Timed work and limits |
| --- | --- |
| `CPU.FrameBuild` | Document control drawing and draw-list mesh upload. No final rendering or completion wait |
| `CPU.BuildAndRecord` | SDF frame construction and renderer command-buffer recording. Mask capture and image baking may issue internal GPU commands; this is CPU/driver cost, not completed GPU time |
| `CPU.Evaluate` | A complete set of graph-root evaluations; includes source-value mutation, the small diagnostic counter increments, and index construction/disposal in indexed cases |
| `CPU.Batch` | A complete cache workload batch, including admissions, evictions, rebakes or uploads when specified. Fixture setup/reset and soak pacing are outside timing |
| `CPU.Build` | PlayMode graph updates, UI declaration and renderer mesh construction |
| `CPU.Submit` | Command-buffer preparation, renderer draw recording and `Graphics.ExecuteCommandBuffer`; driver blocking may contribute |
| `GPU.FinalDraw` | Optional hardware timing for the command-buffer marker, including target clear and final drawing. Excludes readback and preparatory work executed before the marker, such as mask capture during building |
| `Frame.Completion` | Wall-clock time from build start through submission and a synchronous one-pixel readback: CPU work, queue/driver waits, GPU completion and readback latency |

No new benchmark times PNG encoding or file I/O. The rendering completion
measurement deliberately serializes each sample with `ReadPixels` of one
pixel. It exposes completed workload latency; it is not an ordinary pipelined
game frame, screen presentation time, or pure GPU time. The clear-target case
provides context for synchronization overhead. Do not obtain a GPU estimate
by subtracting CPU metrics from `Frame.Completion` or subtracting the clear
case from every workload.

GPU recorder results arrive after Unity advances frames. `GPU.ValidSamples`
shows how many valid timestamps were captured. A missing `GPU.FinalDraw` group or
zero valid samples means that hardware timing was unavailable, not that the
GPU did no work. The remaining completion metrics are still labeled with their
broader scope. Graphics-device-dependent tests may be skipped when unavailable;
check test status and metric presence before comparing results.

New method benchmarks generally use five warmups and 64 measurements with one
operation per measurement. Rendering warms each workload for at least one second
and eight frames before its 64 measured frames, then drains delayed GPU timestamps.
Measured animation phases restart consistently after warmup. The cleanup soak
records batches over its bounded duration. Assertions
and reporting sit outside the timed operation. Setup for intentionally cold
cases is also outside timing, while the resulting cache miss or bake is inside.

Allocation fixtures probe instrumentation with a known allocation before
using it. Some Unity Mono configurations return zero from
`GC.GetAllocatedBytesForCurrentThread()` even for that allocation. A zero from
an unverified counter cannot establish allocation-free behavior.

Read `GC.Bytes.Available` and `GC.Calls.Available` first. When the byte counter
passes its probe, `GC.Alloc` reports managed bytes allocated on the current
thread. Otherwise a supported Unity profiler `GC.Alloc` recorder, filtered to
the current thread, reports `GC.Alloc.Calls`: allocation-event counts, not
bytes. Call counts do not reveal object sizes and cannot be compared numerically
with byte measurements. If neither counter is available, the allocation metric
is omitted; missing instrumentation is never represented as zero allocation.

The operation is a document frame or graph evaluation set, an SDF submission,
or an entire cache batch. Documents/graphs report the mean of eight separate
warm operations; SDF reports the mean of sixteen; cache tests retain per-batch
observations. Rendering allocation windows cover the operation through readback.
Existing fixtures retain different sampling conventions, including totals over
multiple frames, but now use the same verified counter for allocation metrics
and zero-allocation gates. Byte-budget gates require verified bytes and skip
explicitly when unavailable. Consult fixture source before comparing results,
and treat unverified historical zero-byte values as inconclusive until rerun
with working instrumentation. None of these metrics is
total process allocation, native allocation, resident memory or GPU memory
consumption.

Retained counts, mask/atlas pixel counts and Unity-reported texture footprints
help explain capacity and growth. A texture's reported bytes are a limited
observation, not an inventory of all driver or native resources. The state
soak tests cleanup behavior for its chosen working set, not long-term stability
of an entire application.

## Comparing runs

The report retains every metric group and its raw samples. Timing units are
normalized to milliseconds. Per-run statistics include median, p95, p99 and
maximum; the combined view uses the median of the run medians and the worst
run p95/p99. This preserves launch variation rather than hiding it by pooling
all samples into one distribution. With only 64 samples, tail percentiles are
coarse observations close to the maximum; use longer targeted measurements
before making a firm hitch-frequency claim.

Compare the same test, parameters and metric with unchanged workload counters.
Inspect individual launch medians and tails before interpreting small changes.
Record the revision, Unity version, CPU/GPU, graphics API/driver, power settings
and native-backend availability with the artifact directory. Repeat baseline
and candidate under the same conditions; do not mix hardware configurations
into one aggregate. Correctness failures, changed shape counts, rejected work
or missing GPU samples must be resolved or explained before claiming a speedup.

## Remaining gaps

- Built-player and IL2CPP measurements, mobile devices, XR stereo rendering and
  representative URP/HDRP host execution. The new PlayMode fixture uses portable
  runtime APIs, but this harness runs PlayMode inside the Editor; it does not
  build or benchmark a player.
- Actual typing, selection, paste, undo/redo, completion, diagnostics and async
  language-service work in large code editors; rich-text scrollbar/input handling,
  tag parsing and selectable-document interaction at comparable scale.
- Dynamic glyph/fallback-font cache churn and multilingual shaping working
  sets; SDF text, templates and custom material/shader combinations.
- Full native/GPU memory accounting and longer application soaks, including
  retained host lifetimes, render-thread contention, real idle/typing/dragging
  rebuild cadence and presentation latency.

Choose follow-up workloads from these gaps when a feature's real usage differs
from the synthetic cases. Keep the completed workload and its scale visible in
the result so a cache hit, truncated graph or missing draw cannot masquerade as
an optimization.
