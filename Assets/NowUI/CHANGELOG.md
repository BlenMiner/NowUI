# Changelog

All notable changes to the NowUI package are documented here. The format is
based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-06-11

First versioned release; everything below is the state of the package at the
point it became installable through UPM.

### Added

- Immediate-mode drawing API (`Now.Rectangle`, `Now.Text`, `Now.Lottie`) with
  per-material mesh batching.
- MSDF text rendering with dynamic atlas pages, runtime font compilation, a
  contextual font stack (`Now.Font`), and color-font (emoji) import.
- Flexbox-style layout system (`NowLayout`) with deferred and same-frame
  measurement modes.
- Pointer, touch, keyboard, and gamepad input with an immediate-mode
  interaction API (`NowUIInput.Interact`).
- Lottie vector animation playback with a native tessellator and managed
  fallback.
- UGUI integration (`NowUIGraphic`), render-pipeline integration for URP and
  HDRP, and RenderTexture/IMGUI bridges.
- Theme tokens and presets (`NowUITheme`).
- UI scaling for high-DPI displays (`Now.StartUI(float uiScale)`,
  `NowUIScreen.recommendedUIScale`) and safe-area helpers
  (`NowUIScreen.safeArea`).
- Native plugins (font compiler + vector tessellator) for Windows x64,
  Linux x64, macOS x64/arm64, WebGL, Android arm64-v8a, and iOS arm64, built
  by a single auto-triggering CI workflow.
- Managed font compiler fallback: a pure-C# TrueType parser plus a
  Burst-compiled SDF baker that activates automatically on platforms without
  the native plugin (and via `NowFontCompiler.forceManagedCompiler`), so text
  renders on every Unity platform. TrueType outlines only; CFF and color
  emoji fonts still require the native compiler.
