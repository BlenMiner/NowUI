# Native C# apps and capture

The native CLI builds a .NET 9 C# scene and runs the same `Now` / `NowLayout`
API used in Unity. This is the default workflow for agent-authored mockups,
interactive previews and animations. Optional `publish --target web` and
`preview --target web` deploy the same C# scene through the
[browser target](../../Assets/NowUI/Documentation~/BrowserDeployment.md).
The previous JavaScript authoring API, separate browser demo bridge and
Editor web-preview tools remain retired.

## Create and run

From the source checkout:

```powershell
./Tools/NowUI-Native.ps1 init NowUI/apps/Demo
./Tools/NowUI-Native.ps1 preview NowUI/apps/Demo/Preview.csproj
```

Installed Unity packages include `Native~/nowui.ps1`, which accepts the same
arguments. It verifies and installs the bundled .NET tool into the project's
`NowUI/.tools` directory. It neither changes global tools nor writes into the
package. The bundle includes the renderer, stock resources and native font
plugins. Maintain it with `Tools/Build-NowUINativeBundle.ps1` after native code,
resources or shaders change. The SDK and an OpenGL 3.3 desktop session are required.

`init` creates `Preview.csproj`, `Scene.cs` and a local ignore file. Generated
projects reference the installed tool's shared assemblies; the CLI overrides
their `NowUIHome` path when a newer bundle is used. Existing source-checkout
projects can instead reference `Standalone/NowUI.Hosting/NowUI.Hosting.csproj`.

The preview watches C# and project assets by default. Successful builds restart
the scene; compiler errors leave the last working app interactive and print the
diagnostics. Reload resets scene state. `--no-watch` disables watching.
`--frames 3` bounds a smoke run; `--output last-frame.png` captures normal closure.

The native window uses the shared NowUI input and control contracts, including
`Now.KeyBindingField`, all five pointer buttons, clipboard and Unicode editing.
Coordinates account for framebuffer density and UI scale. The host preserves
`NowInput.navigationKeys`, including its shared WASD/arrow/Tab defaults.

| Input capability | Windows | macOS / Linux |
| --- | --- | --- |
| Mouse, wheel, keyboard, clipboard, committed Unicode | Implemented | Implemented through GLFW; not runtime-validated here |
| Physical key capture, F1–F24, keypad and modifiers | Implemented | Implemented through GLFW |
| Gamepad stick/D-pad, South/Start submit, East/Back cancel | Implemented through GLFW mappings | Same portable path |
| IME pre-edit, commit and candidate placement | Per-window IMM bridge | Composition bridge not implemented |
| Primary touch, stable drag ownership and cancellation | Per-window Windows pointer messages | Native touch bridge not implemented |
| Touch keyboard show/hide | Per-window Windows 10+ InputPane requests | No native software keyboard bridge |

Gamepad stick processing uses the Unity Input System's default radial deadzone
(.125–.925), combines stick/D-pad/keyboard navigation, and preserves button
edges. A second touch cannot steal an active drag; release is delivered before
another contact takes over. As in NowUI's built-in device provider, this is a
primary-pointer interface, with no automatic pinch/rotation gesture handling.

Key values use the existing `UnityEngine.InputSystem.Key` enum. Native names are
cached from the keyboard layout, with readable enum names when unavailable;
Windows layout changes refresh the cache, and key events refresh individual
names on other platforms. Media Play/Pause, Next and Previous commands are
captured on Windows only. Reserved OEM3–OEM5 identities have no GLFW key mapping;
OEM1/OEM2 depend on the keyboard and platform.

Touching an editable control requests the Windows keyboard through
`IInputPaneInterop.GetForWindow`; leaving text capture or window focus hides a
keyboard requested by that window. Its text arrives through normal Unicode/IME
events, so `CaptureHost.touchKeyboard` remains null rather than starting a second
mobile text session. Windows can decline a show request. `DesktopInput` reports
`SupportsImeComposition`, `SupportsTouch`, `SupportsOnScreenKeyboard` and
`SupportsGamepadNavigation`; these describe attached bridges, not hardware presence.

Tests exercise the actual shared controls with synthetic device events, plus a
real hidden Windows window's IME/media/cancellation messages and InputPane
availability. Physical touchscreen/gamepad delivery, on-screen typing and human
IME candidate selection are not yet validated. GLFW's public API exposes
committed characters but no composition/touch callbacks; completing macOS/Linux
IME requires Cocoa/X11/Wayland-specific integration beyond this adapter.

## Scene contract and assets

Implement `INowScene.Draw(NowRect view)` with a public parameterless constructor.
The host installs its graphics and resources before construction and owns
`Now.StartUI`, timing and input. Implement `IDisposable` for scene-owned resources.
Keep reusable drawing code separate from the adapter so Unity can call it too.
Unity components, cameras, editor APIs and scene objects are not portable.

Supported Unity project assets load directly through `Resources.Load<T>`: ordinary
Resources aliases, `Assets/...` paths, and `image.png#spriteName` selectors. No
export step is needed. The enclosing project is discovered automatically; use
`--unity-project <directory>` for scenes outside it. See [Native assets](NativeAssets.md)
for image import settings, fonts, themes, Lottie animations, references and object
ownership. Lottie JSON, `.lottie` archives and serialized `NowLottieAsset` files
load through the shared Lottie parser. Existing URL-based Lottie and Markdown
drawing APIs use the native HTTP transport and retain their shared fetch policies.

## Capture and replay

Animations can remain interactive apps, using `Time.time`, `Time.deltaTime` and
the regular NowUI animation APIs. Capture stills or a reproducible frame sequence:

```powershell
./Tools/NowUI-Native.ps1 render NowUI/apps/Demo/Preview.csproj --time 0.5 --output NowUI/captures/demo.png
./Tools/NowUI-Native.ps1 animate NowUI/apps/Demo/Preview.csproj --duration 2 --fps 30 --output NowUI/captures/demo-frames
./Tools/Encode-NowUINativeAnimation.ps1 -Frames NowUI/captures/demo-frames -Output NowUI/captures/demo.webp
```

The encoding helper requires Python 3 with Pillow and produces an opaque looping
WebP. Both source encoder entry points forward to the implementation shipped in
`Assets/NowUI/Native~`; installed consumers use `Native~/encode-animation.ps1`
without this checkout. The native app and PNG capture do not depend on Python. `animate` writes
`frame-000000.png` onward plus `animation.json` with size, rate, frame count,
duration, color space and filename pattern. The output directory must be new;
it is published atomically after all frames succeed. Still failures preserve
an earlier output file. Capture returns a nonzero exit code on failure.

Two zero-time warmup frames settle resources and layout. `render --time` advances
at 60 Hz. `animate` captures from time zero at the requested frame rate; use host
time rather than wall-clock time for deterministic results. `--width` and
`--height` default to 960 × 540 (16:9), which is also the preview window's starting
size, with limits of 8192 per axis and 16,777,216 pixels.
Choose `--color-space gamma|linear` to match the Unity project (default Gamma).
PNG output is straight alpha with a transparent clear color.

Captures wait for remote assets without advancing the animation clock. Use
`--load-timeout <seconds>` to change the 30-second loading limit (1–600 seconds).
Failed or timed-out captures preserve earlier output. Interactive previews load
remote assets asynchronously while the app continues running.

Both capture commands accept `--input replay.json`, a JSON array ordered by
time in seconds. For example:

```json
[
  { "time": 0.2, "type": "click", "x": 60, "y": 80 },
  { "time": 0.4, "type": "type", "text": "Hello" },
  { "time": 0.6, "type": "scroll", "x": 400, "y": 300, "deltaY": -2 }
]
```

Other event types are `move`, `down`, `up`, `keyDown` and `keyUp`. Keys use GLFW
names such as `Tab`, `End`, `F24` and `KeyPadEnter`; hold modifier keys explicitly
(for example `LeftShift` down, `Tab` down/up, `LeftShift` up). Mouse buttons are
0 = left, 1 = right, 2 = middle, 3 = back, 4 = forward. Coordinates are capture pixels;
positive scroll Y means up in native wheel notches. Clicks release on the next
frame. Events must fall within captured times, with a release frame for clicks.
See the [interactive sample](../../Standalone/Samples/NativePreview/README.md)
and its `demo-input.json` for a complete recording.

Use `--scene` for a full or unambiguous short class name when a project has
multiple scenes. `--no-build` reuses its current assembly; `--configuration Debug`
selects Debug. Run `--help` for the complete command contract.

## Rendering and validation

`NowUI.Hosting` supplies scenes, project resources and PNG output. `NowUI.Desktop`
owns the native GLSL shaders and OpenGL backend. `NowUI.Cli` builds and loads scenes
in collectible contexts, drives input/time and publishes captures. Stock paths
include rectangles, images, text, masks, gradients, Bezier, ripples, color pickers,
glass and SDF scenes with sprite distance fields. Gamma and Linear rendering,
float data textures and HarfBuzz shaping are supported. See the
[renderer contract](../../Standalone/NowUI.Desktop/README.md) for format limits.

Custom Unity shaders, depth/MSAA/array targets and Unity world content remain
outside the native host. Native vector tessellation is enabled; other jobs use
the standalone scalar implementation. See [native performance](NativePerformance.md)
for measured frame costs, allocations and the Unity comparison. Shaping tests cover ligatures,
combining characters, kerning and Arabic forms; they do not establish a complete
international text editor. Actual graphics validation has run on Windows/NVIDIA.

```powershell
dotnet build Standalone/NowUI.Standalone.sln -c Debug
dotnet test Standalone/Tests -c Debug --no-build --filter 'Category!=NowUI.Overview'
dotnet test Standalone/NowUI.Engine.Tests -c Debug --no-build
$env:NOWUI_TEST_NATIVE_GRAPHICS = '1'
dotnet test Standalone/NowUI.Native.Tests -c Debug --no-build
Remove-Item Env:NOWUI_TEST_NATIVE_GRAPHICS
```

Native tests cover direct assets, input/composition, scene loading/reload,
scaffolding, deterministic replay/recording, failure preservation and actual GPU
pixels for both color spaces, glass, SDF and text. Graphics tests skip unless
explicitly enabled. The [Unity rendering comparison](NativeRenderingComparison.md)
records the pixel alignment investigation and optional baseline snapping.

For animation encoder changes, run `python Tools/Standalone/test_animation_encoder.py`
with Python/Pillow and PowerShell 7. These tests copy the packaged scripts outside
the checkout and check frame timing, opaque looping output, compatibility wrappers
and preservation of an existing output after a failed encode.
