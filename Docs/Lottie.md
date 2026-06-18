# Lottie Vector Animation

NowUI plays Lottie animations as live vector geometry. The JSON is parsed into
a runtime model and tessellated on the CPU (through the `nowui-vg` native
plugin when available, with a managed fallback), so animations scale losslessly
at any size and never allocate textures.

## Importing

Rename a Lottie JSON file to use the `.lottie` extension and drop it in the
project; `NowLottieImporter` turns it into a `NowLottieAsset`. Both plain
Lottie JSON and real dotLottie archives are accepted. Assets can also be
created at runtime from JSON, bytes, or an http/https URL:

```csharp
var asset = ScriptableObject.CreateInstance<NowLottieAsset>();
asset.SetSource(lottieJson); // throws on invalid documents

yield return NowLottieAsset.LoadFromUrl(
    "https://example.com/spinner.lottie",
    loaded => spinner = loaded,
    Debug.LogError);
```

For immediate layout, pass an http/https URL directly. NowUI downloads it on
the first draw and reuses the cached asset afterwards:

```csharp
NowLayout.Lottie("https://example.com/spinner.lottie")
    .SetHeight(32)
    .SetTime(Time.time)
    .Draw();
```

## Drawing

`Now.Lottie` follows the same fluent builder pattern as rectangles and text.
Use it inside a `Now.StartUI()` scope, or inside any capture path (UGUI, SRP,
RenderTexture).

```csharp
[SerializeField] NowLottieAsset spinner;

void OnPostRender()
{
    using (Now.StartUI())
    {
        Now.Lottie(new NowRect(20, 20, 96, 96), spinner)
            .SetTime(Time.time)
            .Draw();
    }
}
```

Useful options:

- `SetTime(seconds)`, `SetNormalizedTime(0..1)`, `SetFrame(frame)`: playback
  position; `SetLoop(bool)` controls wrapping (defaults to looping).
- `SetColor(color)`: tint multiplier, including alpha fades.
- `SetPreserveAspect(bool)`: defaults to true; false stretches to the rect.
- `SetMask(rect)`: clips the animation like other NowUI draws.
- `SetPlaybackFrameRate(fps)`: caps how often the animation re-tessellates.
  15-20 fps is indistinguishable for small chat-emoji style loops and shares
  far more frames through the cache.

## Layout integration

`NowLayout.Lottie` sizes the animation from its native dimensions and
participates in flex sizing like a label:

```csharp
using (NowLayout.Area(NowScreen.safeArea))
using (NowLayout.Horizontal())
{
    NowLayout.Lottie(spinner).SetHeight(32).Draw();
    NowLayout.Label("Loading...").Draw();
}
```

## UGUI

`NowLottieGraphic` renders an animation through a `CanvasRenderer`; add it to
a `RectTransform` like any other graphic. Assign either an imported Animation
asset or an Animation Url. At runtime, a non-empty URL downloads a transient
`NowLottieAsset` and supports both plain Lottie JSON and dotLottie archives.

## Performance notes

- Tessellation results are cached per composition + frame + size (32-entry
  LRU). Paused or low-frame-rate animations are nearly free after the first
  frame; rapidly scrubbing many large animations is the expensive case.
- Frames are quantized to whole composition frames before hitting the cache.
- Oversized rects tessellate at a capped resolution and scale up; configure
  with `NowLottieRenderer.maxRenderSize`. The cap is measured in screen pixels
  after applying the active `Now.uiScale` or UGUI canvas scale.
- `NowLottieNative.forceManagedTessellation` switches to the managed
  tessellator for profiling comparisons. Platforms without the native plugin
  fall back automatically, except WebGL and iOS where the plugin links
  statically and must be present at build time.
- The managed fallback runs fills and strokes through Burst-compiled jobs
  (`NowLottieBurstTessellator`) with output identical to the scalar
  tessellator; trim paths and matte-clipped strokes use the scalar route.
  Editor timings show parity with the scalar path (editor collection safety
  checks mask the SIMD gains); player builds compile those checks out.
- Emoji sequence shaping and image layers are not supported; animations using
  unsupported features render their supported subset.
