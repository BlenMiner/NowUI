# Mobile

NowUI is mobile-forward: touch is part of the default input provider, UI can be
sized by display density instead of raw pixels, and safe areas are exposed in
UI coordinates. Native plugins ship for Android (arm64-v8a) and iOS (arm64).

## Density-scaled UI

Raw pixels make UI microscopic on phone screens. Pass a scale to
`Now.StartUI` and draw in density-independent units instead:

```csharp
void OnPostRender()
{
    using (Now.StartUI(NowScreen.recommendedUIScale))
    {
        // 1 unit is now roughly 1/160 inch; a 44-unit button is finger-sized
        // on every screen. Now.screenMask reflects the logical size.
        Now.Rectangle(new NowRect(20, 20, 220, 44)).SetRadius(8).Draw();
    }
}
```

- `NowScreen.recommendedUIScale` is `Screen.dpi / NowScreen.referenceDpi`
  (reference defaults to 160, Android's dp), clamped so it never shrinks UI
  below 1:1 on low-dpi desktops.
- Pointer input is converted into the same units automatically; `Now.uiScale`
  exposes the current frame's scale.
- For URP and HDRP, `NowUniversalRendererFeature` and
  `NowHighDefinitionCustomPass` have a `UI Scale` field plus a
  `Scale By Display Density` toggle, and
  `NowPipelineGraphic.BuildDrawList(camera, drawList, uiScale)` accepts the
  scale directly.

## Safe areas

`NowScreen.safeArea` returns `Screen.safeArea` converted to NowUI
coordinates (top-left origin, current UI scale). Use it as the root layout
rect to stay clear of notches, punch-holes, and rounded corners:

```csharp
using (NowLayout.Column(NowScreen.safeArea).Begin())
{
    // content
}
```

## Touch input

The default input provider reads the primary touch as the primary pointer —
press, drag, and release map onto the same `NowInput.Interact` states as a
mouse. The touchscreen only acts as a pointer while a finger is in contact, so
hover states do not stick to the last touch position. Touch arrives through
the Input System (`Touchscreen.current`); multi-touch gestures (pinch, rotate)
are not interpreted — read additional touches from the input device directly
if you need them.

## Native plugins on mobile

| Capability | Android (arm64-v8a) | iOS (arm64) |
| --- | --- | --- |
| Runtime font compilation (`nowui-msdf`) | Shipped, dynamic `.so` | Shipped, static `.a` |
| Lottie native tessellation (`nowui-vg`) | Shipped, dynamic `.so` | Shipped, static `.a` |

iOS links plugins statically (`__Internal`), so the `.a` files must be present
when building. On every other platform a missing plugin degrades gracefully:
Lottie falls back to a managed tessellator, and font compilation falls back to
the managed Burst-compiled SDF compiler — so consoles and other platforms
without prebuilt binaries still render text and animation. The managed font
path covers TrueType (glyf) fonts; CFF outlines and color emoji need native.
Binaries are produced by the `Build Native Libraries` workflow, which also
commits Unity `.meta` files with the correct platform import settings. 32-bit
Android (armeabi-v7a) is not currently built.

## Practical notes

- Test density scaling in the editor with `Now.StartUI(2f)` or the Device
  Simulator package.
- Rectangle positions snap to whole UI units; at scale 2 that is 2 physical
  pixels, which keeps edges crisp but means hairlines should be drawn at
  scale-aware sizes.
- Lottie tessellation is CPU work on the main thread; cap busy animations with
  `SetPlaybackFrameRate(15)` on phones.
