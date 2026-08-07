# Performance and abuse report — 2026-08-06

## Outcome

The initial baseline's clearest limit was continuously refreshed model
previews. A batch of 24 static thumbnails cost 2.31–2.63 ms, while 96 cost
10.05–11.21 ms. The same-day optimization pass below brought the `RenderMesh`
path to 2.24 ms for 24 and 9.80 ms for 96, but the conclusion is unchanged:
pool previews, update them manually/on demand, and render only visible
thumbnails. Changing backends does not remove the scaling cost.

The next largest baseline CPU costs were a full node-graph canvas (about 5 ms)
and 1,000 interactive controls rebuilt in one scroll view (3.82 ms). They fell
to 4.19–4.83 ms and 2.91 ms respectively. Large control walls should still be
virtualized or paged, and static retained hosts should only rebuild when dirty.
The graph result measures canvas drawing, not evaluator topology.

Text and batching are the next practical pressure points:

- 200 three-line labels cost 1.73 ms.
- 200 deliberately interleaved draw items cost 1.20 ms and produce 160
  batches.
- 1,000 non-virtualized label rows cost 0.87 ms.
- A stable, visible 330-line code editor viewport costs 0.66 ms. Large edits or
  replacing the source string every frame were not measured in this pass.

All ten canonical steady-state allocation probes reported 0 B/frame after
representative warmup. The model-preview managed-allocation benchmark was
ignored because this Mono runtime exposes the allocation API without reporting
usable byte counts.

The mask follow-up also removed CPU depth scaling for stable scopes. In the
new 1,000-draw stress case, 32 nested hard rectangles and eight analytic masks
now finish within 0.10 ms of the 0.56 ms unmasked path. Sixteen unchanged SDF
masks reuse their rasterized coverage and fell from 0.194 to 0.025 ms of CPU
submission. Fragment cost still scales with covered pixels and active analytic
or texture masks; these Editor timings are not GPU measurements.

## Same-day optimization follow-up

Four conservative changes target the hot paths found by the baseline:

- Idle button ripples no longer create/touch retained state or sample Unity's
  realtime clock until an animation is actually triggered.
- Fully clipped rectangles are rejected before material/batch work, and fully
  clipped lines before curve flattening, dash, arrow, and stroke tessellation.
  The line bound includes transformed width, AA, the emitter's maximum miter,
  caps, and arrowheads.
- A node canvas with no selection uses one ordered node pass instead of two
  passes with per-node selection hash probes.
- `RenderMesh` previews cache immutable `RenderParams` state and refresh only
  camera, rendering-layer mask, and world bounds for each submission.

The table compares the median of three original launch medians with three
final-code launch medians. Lower is better.

| Workload | Baseline | Final | Change |
| --- | ---: | ---: | ---: |
| 1,000 controls | 3.8176 ms | 2.9075 ms | -23.8% |
| 100-node canvas | 4.9703 ms | 4.1914 ms | -15.7% |
| 400-node canvas | 5.3268 ms | 4.8281 ms | -9.4% |
| One static `RenderMesh` preview refresh | 0.1068 ms | 0.0951 ms | -11.0% |
| 24 static `RenderMesh` thumbnails | 2.3149 ms | 2.2370 ms | -3.4% |
| 96 static `RenderMesh` thumbnails | 11.2149 ms | 9.7958 ms | -12.7% |

The visible 1,000-rectangle guard moved from 0.6183 to 0.6235 ms (+0.8%), and
the 1,000-label scroll guard from 0.8665 to 0.8868 ms (+2.3%). Those small
movements are within the observed launch variation; all final canonical
allocation probes remained at 0 B/frame. The runs were repeated but not
interleaved, and unrelated benchmarks also moved, so treat the percentages as
directional rather than hardware-independent guarantees. This caveat matters
most for model preview, whose camera/render submission floor remains dominant.

## Mask abuse follow-up

The mask fixture keeps 1,000 rounded rectangles, 4,000 vertices, and one batch
constant while changing only a stable ambient mask stack. It measures Editor
CPU frame construction and command-buffer recording. The built-in shaders are
not executed by this timing.

| Stable workload | Baseline | Final | Change |
| --- | ---: | ---: | ---: |
| No mask | 0.6210 ms | 0.5564 ms | -10.4% |
| 1 hard rectangle | 0.7113 ms | 0.6187 ms | -13.0% |
| 32 nested hard rectangles | 1.4125 ms | 0.6506 ms | -53.9% |
| 1 analytic mask | 0.8360 ms | 0.6181 ms | -26.1% |
| 4 analytic masks | 1.3002 ms | 0.6246 ms | -52.0% |
| 8 analytic masks | 1.9844 ms | 0.6082 ms | -69.4% |
| 1 texture mask | 0.8031 ms | 0.6307 ms | -21.5% |
| 2 texture masks | 0.9189 ms | 0.6397 ms | -30.4% |
| 16 static 256x256 SDF masks | 0.1939 ms | 0.0247 ms | -87.3% |
| 16 SDF masks alternating 256/257 width | 2.9108 ms | 2.7376 ms | -5.9% |

The resized result overlaps its launch variance and should be treated as
unchanged, not as a claimed speedup. It remains about 111 times the cached
static CPU cost because every call discards and recreates the targets.

Stable analytic/texture descriptors are now captured once per stack change,
returned by reference to material selection, compared by snapshot identity,
and reused when populating the shared property block. Rect-only stacks avoid
shader-state work entirely. The SDF path hashes its local coverage inputs and
skips rerasterization while content, effective tint, source texture version,
and target size remain unchanged.

State churn remains the deliberate counterexample. Repeating one analytic
push/draw/pop state moved from 0.2698 to 0.2646 ms (-1.9%) while preserving one
batch. Alternating two analytic states moved from 0.6478 to 0.6766 ms (+4.4%)
and still produced 256 ordered batches. Alternating hard rectangles improved
from 0.2441 to 0.2219 ms and remained one batch. Share an analytic or texture
scope around its children instead of changing it per draw; unique shader-mask
states still require unique ordered submissions.

Every mask benchmark reported 0 B/frame through the Editor allocation counter,
and batch counts were unchanged. The SDF cases execute command buffers without
a GPU fence: they measure CPU enqueue and render-target churn, not completed
raster time. Their scenes contain one rounded box each, and the scopes are
sequential and empty, so they do not measure child sampling or complex-scene
GPU cost. The one-pixel resize case intentionally forces worst-case target
recreation every invocation.

## Baseline practical limits

The percentages below compare the median of three launch medians with the full
CPU frame budgets of 16.67 ms at 60 Hz and 8.33 ms at 120 Hz. They are useful
for scale, not additive budgeting: the benchmarks isolate different workloads
and do not include player, render-thread, or GPU time.

| Workload | Median | 60 Hz budget | 120 Hz budget | Guidance |
| --- | ---: | ---: | ---: | --- |
| 96 static model thumbnails, `RenderMesh` | 11.215 ms | 67.3% | 134.6% | Do not refresh an entire large browser every frame. |
| 96 static model thumbnails, `RendererClone` | 10.045 ms | 60.3% | 120.5% | Same limit; backend choice is secondary to update frequency. |
| 400-node canvas, ~1.5 links/node | 5.327 ms | 32.0% | 63.9% | Rebuild only when graph/view state changes. |
| 100-node canvas, ~1.5 links/node | 4.970 ms | 29.8% | 59.6% | Absolute canvas overhead is already material. |
| 1,000 controls: 500 buttons, 250 checkboxes, 250 sliders | 3.818 ms | 22.9% | Virtualize/page large control collections. |
| 24 static model thumbnails, `RendererClone` | 2.631 ms | 15.8% | Reasonable only when this much budget is intentional. |
| 24 static model thumbnails, `RenderMesh` | 2.315 ms | 13.9% | Prefer visibility-based/manual refresh. |
| 200 three-line labels | 1.732 ms | 10.4% | Avoid dense multiline walls; clip or virtualize them. |
| 200 alternating draw items, 160 batches | 1.196 ms | 7.2% | Group compatible draw kinds/materials/textures. |
| Cold parse of a unique ~30 KB Markdown document | 0.895 ms | 5.4% | Parse once and retain the document. |
| 1,000 non-virtualized unique label rows | 0.867 ms | 5.2% | Fine at this scale when budgeted; larger lists need virtualization. |
| Stable 330-line code-editor repaint | 0.657 ms | 3.9% | Stable repaint is acceptable; edit/rebuild scaling remains unmeasured. |
| 1,000 rectangles | 0.618 ms | 3.7% | Primitive frame building is not a leading concern at this count. |
| 100 shaped text labels | 0.401 ms | 2.4% | Warm glyphs and avoid unnecessary text churn. |

The 100-node and 400-node graph results are close because the benchmark has a
fixed 1920×1080 viewport and the canvas culls most off-screen content. It does
not prove that graph evaluation or fully visible 400-node scenes scale this
well.

## Other measured boundaries

### Font and Lottie cold work

Baking the 95 printable ASCII glyphs through a fresh dynamic-font session cost
4.881 ms with the managed compiler and 23.508 ms with the native compiler on
this machine. This is initialization work, not a steady frame path. Warm the
actual fonts, styles, sizes, and glyphs before interaction instead of allowing
atlas growth to cause an input-frame hitch.

For the 12-contour gradient-fill stress case, Burst tessellation took 1.094 ms
versus 3.455 ms for the scalar path, a 3.16× speedup. The animated-emoji frame
benchmark itself cost 0.147 ms for one forced cache miss. Do not extrapolate
that single asset to a large animation wall without a multi-instance test.

### Cheap CPU paths after warmup

| Workload | Median |
| --- | ---: |
| ~1,000 pure layout reservations in 75 nested groups | 0.188 ms |
| 1,000 string-ID interaction/hash calls | 0.169 ms |
| 1,000 inactive repaint-tracking interactions | 0.374 ms |
| 1,000 settled transition updates | 0.099 ms |
| 100 SDF shapes across 10 stable scenes | 0.056 ms |
| Glass replay, 1 pane | 0.068 ms |
| Glass replay, 16 panes | 0.080 ms |

The glass and SDF numbers only cover main-thread construction and command
recording in the tested editor path. They do not measure blur bandwidth,
overdraw, render-target allocation, SDF mask generation, or GPU time. They are
not evidence that glass panes or dynamically resized masks are safe to spam.

### Model-preview detail

For one 12-mesh static preview, refresh medians were 0.087 ms
(`RendererClone`), 0.107 ms (`RenderMesh`), and 0.154 ms (`SceneObject`). The
`RenderMesh` backend retained no presentation clone objects, one staging object,
12 mesh sources, and 12 submesh draws; the clone backend retained 13
presentation objects. Use `RenderMesh` when the lower retained object count is
valuable, but use update frequency and visibility to control CPU cost.

## Change since the July report

The environment, Unity revision, and canonical test shapes match the
2026-07-13 run. The current median of three launch medians is directionally
better on the core rendering/text cases:

| Metric | 2026-07-13 | 2026-08-06 | Change |
| --- | ---: | ---: | ---: |
| 1,000 rectangles | 0.980 ms | 0.618 ms | -36.9% |
| 100 shaped labels | 1.090 ms | 0.401 ms | -63.2% |
| 200 three-line labels | 4.150 ms | 1.732 ms | -58.3% |
| Effect modifier frame build | 0.930 ms | 0.766 ms | -17.7% |
| 1,000 controls | 3.660 ms | 3.818 ms | +4.3% |
| 1,000 scroll rows | 0.870 ms | 0.867 ms | -0.4% |
| 1,000 inactive repaint states | 0.360 ms | 0.374 ms | +3.9% |
| 1,000 settled transitions | 0.100 ms | 0.099 ms | -0.8% |

The July canonical artifact contains one suite launch, while this report uses
three current launches. Treat these deltas as directional rather than as a
controlled A/B optimization result. In particular, the 1,000-control path has
historically varied by several tenths of a millisecond on this desktop.

## Blind spots worth benchmarking next

This run does not establish safe limits for several source-level scaling risks:

1. Code-editor rebuilds at 3,000–10,000 lines, especially fresh equal-content
   string references and edits near the beginning of the file.
2. Node-graph evaluation of long chains/fan-out DAGs, including separate root
   evaluation versus `BeginBatch`.
3. Never-reused control IDs and retained control-state cleanup.
4. Complex or RenderTexture-backed SDF masks, unique-id growth, retained mask
   memory, and synchronized GPU cost on desktop/mobile targets.
5. Rich text with thousands of spans/inline objects and per-frame relayout.
6. Per-frame creation of unique gradient objects and approach to the 255-ramp
   atlas limit.

These are better next targets than adding more ordinary rectangle/control
microbenchmarks.

## Method

Source and environment:

- Commit `455198f1d69a98c48f0fe71b245b15a4f07898ff`
- Unity 6000.4.0f1 (`8cf496087c8f`)
- Windows 11 Pro 10.0.26200, build 26200
- AMD Ryzen 9 7900X, 24 logical processors
- 63.2 GiB RAM
- Ultimate Performance power plan

Each filter ran in three separate Unity launches. No launch was discarded. A
reported value is the median within each launch followed by the median of those
three medians.

Canonical filter: 54 passed, 0 failed in every launch.

```powershell
.\Tools\NowUI-Harness.ps1 `
  -Mode EditMode `
  -Filter 'NowPerformanceTests|NowRuntimePerformanceTests|NowExtensionsPerformanceTests' `
  -ArtifactsPath <output-directory>
```

Specialist filter: 38 passed, 0 failed, 1 ignored in every launch. The class
filter also runs the fixtures' functional tests. The ignored test is
`ManagedAllocationFootprint`, for the Mono allocation-counter reason described
above.

```powershell
.\Tools\NowUI-Harness.ps1 `
  -Mode EditMode `
  -Filter 'NowModelPreviewBackendPerformanceTests|NowManagedFontCompilerTests|NowLottieBurstTessellatorTests' `
  -ArtifactsPath <output-directory>
```

Mask filter: 13 passed, 0 failed in each of three baseline and three final
launches. The 16-mask SDF baseline was recaptured separately after amplification
put the measurement above timer/reporting noise.

```powershell
.\Tools\NowUI-Harness.ps1 `
  -Mode EditMode `
  -Filter 'NowMaskPerformanceTests' `
  -ArtifactsPath <output-directory>
```

Artifacts are under `artifacts/benchmark-abuse-20260806`:

- Canonical: `canonical`, `canonical-run2`, `canonical-run3`
- Specialist: `specialists`, `specialists-run2`, `specialists-run3`

Follow-up artifacts are under `artifacts/benchmark-improvements-20260806`:

- Final canonical: `canonical-final`, `canonical-final-run2`,
  `canonical-final-run3`
- Model-preview specialist: `specialists`, `specialists-run2`,
  `specialists-run3`
- Final focused correctness: `final-focused-editmode`, `focused-playmode`
- Final isolated node confirmation: `node-edge-final`,
  `node-edge-final-run2`, `node-edge-final-run3`
- Complete validation before the mask follow-up: `full-editmode` (1,235
  passed, 2 ignored) and `full-playmode` (97 passed, 2 ignored), with no
  failures

Mask artifacts are under `artifacts/mask-abuse-20260806`:

- Initial core baselines: `baseline-run1`, `baseline-run2`, `baseline-run3`
- Amplified SDF baselines: `baseline-sdf16-run1`,
  `baseline-sdf16-run2`, `baseline-sdf16-run3`
- Intermediate comparison: `candidate-run1`, `candidate-run2`,
  `candidate-run3`
- Final comparison: `final-run1`, `final-run2`, `final-run3`
- Complete post-review validation: `full-post-review-editmode` (1,260 passed,
  2 ignored) and `full-post-review-playmode` (97 passed, 2 ignored), with no
  failures

## Scope

These are Unity Editor main-thread benchmarks. They do not measure a built
player, IL2CPP, mobile hardware, render-thread cost, GPU time, memory peaks, or
thermal behavior. Hardware-specific absolute numbers should be used for local
budgeting and relative comparisons, not universal limits.
