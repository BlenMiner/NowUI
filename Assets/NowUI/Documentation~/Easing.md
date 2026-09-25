# Easing

`NowEase` is a stateless set of easing curves for shaping motion: panel
entrances, hover growth, progress fills, or any value you interpolate from a
caller-owned clock. It replaces hand-written easing helpers in motion-heavy
scenes and is the same math NowUI uses internally for text animation easing
and view-stack transitions.

Every function takes normalized time `t`, clamps it to `[0, 1]` (NaN is treated
as `0`), and returns eased progress with `f(0) == 0` and `f(1) == 1` exactly.
Back, Elastic, and Spring curves may leave `[0, 1]` between the endpoints.

## Curves

| Family | Functions | Notes |
| --- | --- | --- |
| Linear | `Linear` | Clamped `t`. |
| Polynomial | `InQuad`/`OutQuad`/`InOutQuad`, `InCubic`/`OutCubic`/`InOutCubic`, `InQuart`/`OutQuart`/`InOutQuart`, `InQuint`/`OutQuint`/`InOutQuint` | Monotonic. |
| Sine, Expo, Circ | `InSine`..., `InExpo`..., `InCirc`... with `Out` and `InOut` variants | Monotonic; Expo endpoints are exact. |
| Back | `InBack(t, overshoot)`, `OutBack`, `InOutBack` | Default overshoot `1.70158f` (about 10%). |
| Elastic | `InElastic(t, amplitude, period)`, `OutElastic`, `InOutElastic` | Default amplitude `1`, period `0.3` (`0.45` for InOut). |
| Bounce | `InBounce`, `OutBounce`, `InOutBounce` | Classic decaying bounces. |
| Spring | `Spring(t, damping, frequency)` | Damping ratio `0..1` (default `0.5`), undamped oscillations per interval (default `2`). Lands exactly on `1` without a jump. |
| Bezier | `CubicBezier(x1, y1, x2, y2, t)` | CSS `cubic-bezier()` semantics. |

The `NowEasing` enum names every curve above except `CubicBezier`, using the
default parameters, for data-driven choices such as inspector fields or
markup attributes: `NowEase.Evaluate(NowEasing.OutBack, t)`.

## Driving motion from your own clock

NowUI never reads a hidden clock for easing. Store when a motion starts and
convert the current time into progress with `NowEase.Progress(time, start,
end)`, or evaluate a curve over the window directly:

```csharp
float openedAt; // set when the panel opens

void DrawPanel(NowRect target, float now)
{
    float t = NowEase.Evaluate(NowEasing.OutBack, now, openedAt, openedAt + 0.35f);
    float fade = NowEase.Progress(now, openedAt, openedAt + 0.2f);

    NowRect rect = new NowRect(
        target.x,
        target.y + (1f - t) * 24f,
        target.width,
        target.height);

    Now.Rectangle(rect)
        .SetColor(new Color(0.12f, 0.14f, 0.2f), fade)
        .SetRadius(12f)
        .Draw();
}
```

`Progress` clamps to `[0, 1]`. For an empty or inverted window
(`end <= start`) it returns `1` once `time` reaches `end` and `0` before, so a
zero-length animation snaps instead of dividing by zero. Pass the same clock
you use elsewhere, such as `Time.unscaledTime` in Unity or the frame time of a
native preview, so pausing and scrubbing stay deterministic. While a motion
is running in a host that repaints on demand, call
`NowControlState.RequestRepaint()` so the next frame is drawn.

`NowControlState.Transition` already returns an animated linear `0..1` value
for hover and press states; shape it with any curve, for example
`NowEase.OutCubic(hoverT)`. See [Custom Controls](CustomControls.md).

## CSS cubic-bezier

`CubicBezier` matches CSS `cubic-bezier(x1, y1, x2, y2)`: the curve runs from
`(0, 0)` to `(1, 1)`, `x1` and `x2` are clamped to `[0, 1]`, and `y1`/`y2` may
exceed that range to overshoot.

```csharp
float ease      = NowEase.CubicBezier(0.25f, 0.1f, 0.25f, 1f, t); // CSS ease
float easeInOut = NowEase.CubicBezier(0.42f, 0f, 0.58f, 1f, t);   // CSS ease-in-out
float pop       = NowEase.CubicBezier(0.34f, 1.56f, 0.64f, 1f, t);
```

The solver is accurate to about `1e-4` in `x` and is shared with Lottie
keyframe easing.

## Allocation and cost

All `NowEase` members are static, take and return `float`s, and never allocate
or cache state, so they are safe in per-frame and per-glyph hot paths. The
polynomial curves are a few multiplies; Sine, Expo, Elastic, Spring, and
`CubicBezier` call trigonometric, power, or iterative solvers and cost more,
but still no managed allocation.

For text entrances, `NowTextAnimationEasing` remains the easing option of
`NowTextAnimations`; see [Text Gradients And Animation](TextStyling.md).
