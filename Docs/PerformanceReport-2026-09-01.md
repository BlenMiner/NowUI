# Performance baseline — 2026-09-01

## Outcome

This is a re-baseline of the canonical, specialist, and mask benchmark
filters at commit `143226c` (v1.10.6), compared against the final
2026-08-06 numbers. No code was changed. Three findings matter:

1. **Two regressions landed since August 6, both bisected to single commits.**
   `3e73447` (adaptable font margins, 2026-08-30) made the codepoint text
   paths 15–25% slower. `4895db2` (auto-ID rework, 2026-08-22) added roughly
   10–15% to every control and label path. Yesterday's VR blur commit
   (`3d8cf0f`) did not move any canonical benchmark. The apparent doubling of
   the docs Glass demo rebuild turned out to be a benchmark fix, not a
   regression (see the optimization pass section at the end).
2. **The node graph canvas is dominated by batch churn, not drawing.** A
   100-node frame emits 692 batches because shapes and text alternate per
   node and only the immediately previous mesh page is reused. Grid and
   shadows cost nothing measurable; links cost about 1.4 ms of the 4.4 ms
   because the analytic bezier fast path is disabled under a transform.
3. **The core paths carry avoidable fixed cost per draw**, mostly large
   struct copies, repeated style resolution, and a nine-stream vertex
   layout that is written and then repacked. Roughly 2× on rectangles,
   labels, and controls looks realistic without changing the public API.

## Baseline (median of three launch medians, ms)

| Workload | 2026-08-06 | 2026-09-01 | Change |
| --- | ---: | ---: | ---: |
| NodeGraphCanvas400 | 4.830 | 4.810 | -0.4% |
| NodeGraphCanvas100 | 4.190 | 4.370 | +4.3% |
| ManyControlsStressFrameBuild | 2.910 | 3.150 | +8.2% |
| ActualDocsGlassDemoGraphicRebuild | 0.670 | 1.870 | +179% |
| LabelPipelineMultilineStrings | 1.710 | 1.830 | +7.0% |
| ActualDocsOverviewGraphicRebuild | 1.370 | 1.760 | +28.5% |
| DocsTextRowsFrameBuild | 1.140 | 1.170 | +2.6% |
| ScrollViewThousandRowsFrameBuild | 0.890 | 0.990 | +11.2% |
| MarkdownParseCold | 0.850 | 0.890 | +4.7% |
| BatchHeavyMeshUpload | 0.900 | 0.880 | -2.2% |
| MarkdownCodeFenceHeavy | 0.760 | 0.870 | +14.5% |
| CodeEditorRepaint300Lines | 0.640 | 0.820 | +28.1% |
| LabelPipelineRepeatedStrings | 0.760 | 0.810 | +6.6% |
| LabelPipelineUniqueStrings | 0.720 | 0.780 | +8.3% |
| EffectModifierFrameBuild | 0.730 | 0.730 | 0.0% |
| MarkdownRelayoutStress | 0.580 | 0.680 | +17.2% |
| RectangleFrameBuild | 0.620 | 0.550 | -11.3% |
| LargeTextAreaRedraw | 0.420 | 0.540 | +28.6% |
| ThemeResolutionWithoutScope | 0.500 | 0.540 | +8.0% |
| TextFrameBuild | 0.390 | 0.420 | +7.7% |
| InteractionRepaintTrackingStress | 0.350 | 0.390 | +11.4% |
| InteractIdHashingStress | 0.170 | 0.220 | +29.4% |
| LayoutEngineStressRebuild | 0.180 | 0.210 | +16.7% |
| TransitionTimingStress | 0.100 | 0.120 | +20.0% |

Between-launch spread on the three runs was 1–3% for almost every test, so
differences above about 5% are real. Specialist (model preview, font
baking, Lottie) and mask filters were within ±5% of August and are not
listed; their artifacts are under `artifacts/local/baseline-20260901-*`.

## Regression bisect

Each checkpoint is one launch of the canonical filter in a detached
worktree with a copied Library. Values in ms.

| Test | 81d20fc Aug 7 | 01624a9 | 4895db2 ID rework | 9c58111 | 3e73447 font margins | ca794ba | HEAD |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ActualDocsGlassDemoGraphicRebuild | 0.72 | 0.73 | 0.73 | 0.78 | **1.85** | 1.89 | 1.89 |
| CodeEditorRepaint300Lines | 0.63 | 0.64 | 0.64 | 0.64 | **0.80** | 0.84 | 0.82 |
| LargeTextAreaRedraw | 0.42 | 0.44 | 0.44 | 0.44 | **0.55** | 0.55 | 0.55 |
| MarkdownRelayoutStress | 0.56 | 0.58 | 0.59 | 0.58 | **0.68** | 0.69 | 0.68 |
| ActualDocsOverviewGraphicRebuild | 1.33 | 1.67 | 1.58 | 1.66 | **1.78** | 1.75 | 1.81 |
| ManyControlsStressFrameBuild | 2.82 | 4.98 | **3.20** | 3.21 | 3.20 | 3.28 | 3.27 |
| InteractIdHashingStress | 0.16 | 0.17 | **0.22** | 0.22 | 0.22 | 0.22 | 0.22 |
| LabelPipelineUniqueStrings | 0.72 | 2.65 | **0.82** | 0.82 | 0.82 | 0.89 | 0.96 |
| ScrollViewThousandRowsFrameBuild | 0.87 | 1.75 | **0.98** | 0.99 | 0.99 | 0.99 | 1.00 |

`01624a9` (the commit before the ID rework) was a temporary spike that the
rework itself repaired, but the rework still left controls, labels, and id
hashing 10–37% above the August 7 level.

What `3e73447` changed on the per-frame path, from reading the diff:
`pixelRange` became part of every glyph and prepared-run cache key; text
with an outline is now drawn in two passes with a `Prewarm*` lookup per
draw; `ClampTextOutlineToRange` (NaN/Infinity checks plus clamp) runs per
glyph inside `NowMesh.AddTextGlyph*`, which alone is consistent with the
0.1–0.2 ms added to the 13k-glyph code editor and text area cases. The
actual mechanism, found later by instrumenting the glyph lookup, is that
`NowFont.GetGlyph` re-derived the atlas size, the pixel-range tier, and the
base-atlas support check for every glyph; see the optimization pass below.
The Glass demo jump in the same commit is explained there as well.

## Node graph partition (100 nodes, one launch, ms)

| Variant | Median | Batches |
| --- | ---: | ---: |
| Full | 4.57 | 692 |
| Grid off | 4.56 | 692 |
| Grid and shadows off | 4.54 | 692 |
| Grid, shadows, and links off | 3.14 | 692 |

Only 80 of the 100 nodes are inside the 1920×1080 viewport, so the
per-visible-node cost is about 39 µs with links removed. The batch count
does not change with any toggle: it comes from the draw order inside each
node (body rects, title text, toggle line, then four port circles each
followed by a port label), which forces a new mesh page on every
shape/text switch. Every page costs a `NowMeshBatch` record (about 700
bytes because it embeds the mask shader state), a `SetSubMesh` call, and
an index append.

## Where the time goes (code reading, not profiled)

These are the hot spots found by tracing the four heaviest paths. They are
ranked by estimated share and should be confirmed with the profiler before
being optimized in bulk.

### Shared by every draw

- `NowMaskShaderState.Equals` takes its ~650-byte argument by value, and
  `Now.UseMaterial` calls it on every rectangle, glyph run, and line. An
  `in` parameter or an identity compare removes the copy.
- Fluent builders return large structs by value from every setter:
  `NowText` (~260 B, copied 12–16× per label through the draw chain),
  `NowRectangle` (~176 B, 4–6×), `NowButton` (~300 B), `NowFocusNavigation`
  (~152 B, 4–5× per control). `NowText` setters lack `AggressiveInlining`.
- Vertex data is written as nine SoA streams (528 bytes per quad, seven of
  them replicated four times) and then repacked into an interleaved buffer
  at frame end, so every frame's geometry is written twice. Mesh buffer
  params are re-declared every frame and the mesh is cleared twice.
- `Now.DrawRect` performs six `UnityEngine.Object` null comparisons and
  four `Mathf.RoundToInt` (double-precision) calls per rectangle, plus a
  duplicate `IsOutsideMask` test.

### Controls (~5 µs per button, of which glyphs are under 1 µs)

- The label style is resolved from theme tokens two times per button and
  three times per checkbox (`NowControls.Text` in measure and in draw), with
  no cache; `NowLayout.labelStyle` already shows the memo pattern to copy.
- Five to six `NowIdHash` derivations and three to four dictionary probes
  per idle control: control id, repaint seed, state key (twice), and the
  Material renderer's `"button-ripple"` string hash every frame.
- `NowControlState.Transition` materializes a persistent entry and reads
  `Time.realtimeSinceStartup` for controls that have never been hovered.
- `NowControls.SiteToken` hashes the full caller file path on every factory
  call, then discards the result when `SetId` is used.
- `NowMaterialControlRenderer` copies the ~488-byte `NowControlStyleSet`
  three times per button to read two floats.

### Labels (~0.8 µs fixed per label plus ~0.1 µs per glyph)

- Shaping and prepared runs are cached correctly; the residual is three
  string-keyed dictionary lookups per label (measure, bounds for the mask,
  prepared run), around six `HashSet` clear/add cycles for font fallback
  resolution, and `HasShapedControlCharacters` rescanning the string.
- `LabelMask` forces a second measure pass purely for an outset mask.

### Node graph (~39 µs per visible node)

- Batch churn as measured above; a two-pass emission (all shapes, then all
  text) per node or per canvas would collapse ~700 pages to a handful.
- Every text is measured and then drawn (five per node), and port labels
  are only `A/B/X/Y`, so a per-frame or per-port measure cache is trivial.
- Links: `Now.DrawLine` disables the analytic cubic path when a transform
  or mesh capture is active, so every link is flattened, mitered, and given
  two round caps on the CPU each frame, and endpoint lookups run before the
  cull. Allowing the shader path under affine transforms would make links
  four vertices each.
- `PortControlId` rebuilds six hashed id segments per port per frame, and
  the ~384-byte `NowNodeGraphStyle` is passed by value through 68
  signatures.

## Suggested order of work

1. Recover the two regressions (`3e73447` per-glyph work and Glass demo
   path; `4895db2` id derivation overhead). This is the cheapest 10–30% on
   text, controls, and labels and needs no design change.
2. `NowMaskShaderState` by-`in`/identity compare and the builder struct
   inlining. Small, mechanical, benefits every benchmark.
3. Node graph draw ordering plus link fast path; expect the 100-node case to
   drop by half or more.
4. Control style memo and state-key caching for the control pipeline.
5. Single interleaved vertex stream. Largest change, largest payoff on
   rectangles and text, but touches the native packer and every emitter.

## Method

- Commit `143226c`, Unity 6000.4.0f1, Windows 11 Pro 10.0.26200, AMD Ryzen 9
  7900X, 64 GiB, Ultimate Performance plan. Another Unity editor was open
  on an unrelated project at ~15% total CPU load; the tight spread across
  launches suggests it did not affect the medians.
- Canonical filter ran three times (54 passed each), specialists once (68
  passed, 1 ignored), masks once (13 passed). Artifacts:
  `artifacts/local/baseline-20260901-{canonical-run1,canonical-run2,canonical-run3,specialists-run1,mask-run1}`.
- Bisect artifacts: `artifacts/local/bisect-20260901-*`. Node graph
  partition: `artifacts/local/tmp-nodegraph-partition-20260901` (the probe
  test was temporary and is not in the tree).
- Same harness command as the August report:

```powershell
.\Tools\NowUI-Harness.ps1 -Mode EditMode `
  -Filter 'NowPerformanceTests|NowRuntimePerformanceTests|NowExtensionsPerformanceTests' `
  -ArtifactsPath artifacts/local/<name>
```

## Optimization pass (same day)

The suggested order of work was carried out in the working tree (not
committed). Two corrections to the analysis above came out of it first:

- **The Glass demo "regression" was a benchmark fix, not a code regression.**
  Before `3e73447` the perf test selected the docs page by raw index 26,
  which had drifted to the Shapes demo as pages were added. That commit
  switched the harness to select by title, so 1.87 ms is the first correct
  Glass demo measurement. Older Glass demo numbers are not comparable.
- **Goldens are stale at HEAD.** `Tools/NowUI-Harness.ps1 -Mode Golden` fails
  on a clean checkout of `143226c` with 18 scenes 1–3% of pixels off. The
  captures produced with and without the changes below are byte-identical
  for 21 of 22 scenes from the golden runs, and a direct visual capture of
  the remaining scene (`world-multi-surface-overlap`) with and without the
  changes is byte-identical too.

Because Rocket League was running during the afternoon, the comparison
below is against a control run of unmodified HEAD taken under the same
conditions (three launches each, medians of medians).

| Test | Control | Optimized | Change |
| --- | ---: | ---: | ---: |
| NodeGraphCanvas100 | 4.670 | 3.580 | -23.3% |
| NodeGraphCanvas400 | 5.230 | 4.020 | -23.1% |
| ManyControlsStressFrameBuild | 3.180 | 2.740 | -13.8% |
| ThemeResolutionWithoutScope | 0.540 | 0.480 | -11.1% |
| ThemeResolutionNestedScopes | 0.550 | 0.480 | -12.7% |
| CodeEditorRepaint300Lines | 0.820 | 0.730 | -11.0% |
| MarkdownRelayoutStress | 0.700 | 0.620 | -11.4% |
| MarkdownCodeFenceHeavy | 0.930 | 0.830 | -10.8% |
| DocsTextRowsFrameBuild | 1.260 | 1.150 | -8.7% |
| RectangleFrameBuild | 0.550 | 0.500 | -9.1% |
| LargeTextAreaRedraw | 0.550 | 0.510 | -7.3% |
| DocsCompositeGraphicRebuild | 0.810 | 0.750 | -7.4% |
| TextWrapTooltipRelayout | 0.270 | 0.250 | -7.4% |
| ActualDocsOverviewGraphicRebuild | 1.810 | 1.710 | -5.5% |
| LabelPipelineRepeatedStrings | 0.800 | 0.760 | -5.0% |
| InteractIdHashingStress | 0.230 | 0.220 | -4.3% |
| BatchHeavyMeshUpload | 0.920 | 0.880 | -4.3% |

No test got slower beyond launch noise. Full EditMode (1,719 passed, 2
ignored) and PlayMode (144 passed, 4 ignored) suites pass, and the node
graph tests (120) pass.

Changes, in the order of the plan:

1. `NowFont.ResolveGlyphTier` memoizes atlas size, pixel-range tier, and
   base-atlas support per (font size, outline). `3e73447` had made every
   codepoint lookup re-derive these, including a string compare on the atlas
   type and a scan of color bitmap sizes.
2. ID path: `NowControls.CurrentOwnerRoot` caches its answer until
   `NowInput.Update` changes the provider (was a locking
   `ConditionalWeakTable` lookup per control); `NowIdHash` tabulates domain
   and segment salts; `SiteToken` answers repeat call sites through a
   reference-keyed cache instead of hashing the full file path.
3. `NowMaskShaderState.Equals(in ...)` with an empty-state fast path, and
   `in` parameters wherever the ~650-byte state is passed into meshes and
   batches. `NowText`, `NowLabel` setters and `NowRectangle.Draw` are marked
   for aggressive inlining. `Now.DrawRect` checks references before asking
   Unity whether a texture or material is alive.
4. Node graph: the stock renderer now draws in two phases per node (surface
   and port circles, then title and port labels) through new
   `DrawNodeSurface`/`DrawNodeTitle`/`DrawPortShape`/`DrawPortLabel` virtuals;
   `DrawNode`/`DrawPort` still call both halves, and custom renderers keep
   the original single-call order. Port ids derive from a per-node id
   instead of re-hashing six segments per port.
5. `NowThemeAsset` memoizes resolved text and rectangle presets per style,
   keyed on `contentVersion` and the ambient font, so a control no longer
   resolves its tokens two or three times per draw.

Not done: the single interleaved vertex stream (largest change, deferred)
and the node-graph link fast path, which the line renderer disables under
transforms and mesh capture by design because it needs its own shader.
