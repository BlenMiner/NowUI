# Performance and warmup

NowUI's normal frame paths are designed to avoid managed allocation after
representative warmup. First use is not the same as steady state.

## Warmup checklist

- Use `NowDrawList.Warmup(...)` or `NowRenderer.Warmup(...)` with a
  representative frame before measuring steady state.
- Use the input-aware warmup overload when controls depend on `NowInput`; it
  installs the same provider and surface shape the measured frame will use.
- For retained hosts such as `NowGraphic`/`NowLayoutGraphic`, exercise the
  representative visible state and interactions through the real host before
  profiling it. Do not assume a smaller caller-owned draw-list warmup also
  warms the host's private buffers and control state.
- For data-backed controls with known stable IDs, call
  `NowControlState.Warmup<T>(id)` during initialization to create the slot
  outside the first interactive frame.
- Warm dynamic font glyphs, Lottie geometry, effect render textures, SDF mask
  coverage textures, material batches, world-space material instances, and
  caller-owned buffers used by the real frame.
- When glass diagnostics are enabled, call
  `NowGlassSettings.ReserveDiagnostics(maxPanesPerFrame)` during initialization
  and read entries with `TryGetLastFrameDiagnostic` or
  `CopyLastFrameDiagnosticsTo` into caller-owned storage.

Measure the actual host and feature combination after warmup. A smaller
synthetic frame can miss glyph, ID, material, input, or effect state used by the
real interface.

SDF scenes upload each distinct graph as a contiguous range and pack its start
and count into existing layer metadata. A layer evaluates only the shapes in
its graph rather than scanning every uploaded scene shape, and repeated graph
references reuse one uploaded range. Morphs still evaluate both ranges, while
drop and inner shadows repeat scene-distance evaluation when enabled. Profile
covered pixel area together with the number of referenced shapes, morphs, and
effects; a one-quad scene does not imply constant fragment cost.

SDF masks cache one linear, single-channel coverage target per stable scene id.
First use and a transformed physical-size change can allocate or resize it;
stable ids, dimensions, transforms, and UI scale reuse it. Warm the actual
`BeginMask()` scene before measuring. Retained hosts only rebuild it when the
host is dirtied, so call `MarkDirty()` when animated field data changes.
`NowSdf.Reset()` releases these extension-owned targets together with the SDF
material cache.

`NowSdfBuilder.SetMaskResolutionScale(scale)` trades SDF-mask capture quality
for rasterization cost and persistent texture memory. The default `1` captures
at transformed physical resolution; `0.5` reduces both axes by half and thus
uses roughly one quarter of the pixels. It does not reduce the child draw's one
mask sample per output fragment, and unchanged static masks already skip the
capture pass. Profile dynamic, frequently dirty, or large masks on target
hardware; keep each stable id at a stable scale to avoid resize churn. Direct
SDF `Draw()` scenes have no intermediate texture and are unaffected.

Each distinct custom SDF material template allocates cache-owned direct and
mask clones on first use, so warm the actual material and host path and reuse a
bounded template set. The default `SetMaterial(material)` mode snapshots a
static template and preserves the normal unchanged-scene upload and mask
caches. Use
`SetMaterial(material, syncPerFrame: true)` only for changing project-defined
properties or time-varying custom coverage: each use recopies the template,
forces the standard SDF arrays to be uploaded again, and prevents a custom mask
capture from being reused. `NowSdf.Release(id)` and `NowSdf.Reset()` release
these owned clones; the caller-owned template remains alive.
