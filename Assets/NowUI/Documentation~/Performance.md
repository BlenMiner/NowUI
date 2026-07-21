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
- Warm dynamic font glyphs, Lottie geometry, effect render textures, material
  batches, world-space material instances, and caller-owned buffers used by the
  real frame.
- When glass diagnostics are enabled, call
  `NowGlassSettings.ReserveDiagnostics(maxPanesPerFrame)` during initialization
  and read entries with `TryGetLastFrameDiagnostic` or
  `CopyLastFrameDiagnosticsTo` into caller-owned storage.

Measure the actual host and feature combination after warmup. A smaller
synthetic frame can miss glyph, ID, material, input, or effect state used by the
real interface.
