using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// README showcase: a circular "how many months" slider in the style of
    /// the Airbnb stay-length dial. The track and the progress arc are single
    /// SDF arc primitives whose fills are textures, so the progress color
    /// sweeps around the dial with a conic ramp while inner shadow, emboss,
    /// and drop shadow follow the round-capped geometry. A cursor grabs the
    /// knob, drags it around the dial in both directions, and the month count
    /// ticks over with a slide transition. The loop returns to its starting
    /// state so it wraps seamlessly.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        const int MonthFrames = 120;
        const float MonthRingRadius = 138f;
        const float MonthRingHalfWidth = 30f;
        const float MonthDiscRadius = 104f;
        const float MonthKnobRadius = 27f;
        const float MonthSceneReach = 210f;

        static readonly Vector2 MonthDialCenter = new Vector2(480f, 268f);
        static readonly Color MonthInk = new Color(0.07f, 0.07f, 0.09f, 1f);

        static Texture2D _monthProgressRamp;
        static Texture2D _monthTrackRamp;

        static void DrawMonthSlider(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float f = frame.index;
            float angle = MonthKnobAngle(f);
            float held = MonthHeld(f);
            Vector2 cursor = MonthCursorPath(f, angle);

            // Paper-white backdrop with a soft radial lift under the dial so
            // the raised disc's shadow has something to fall on.
            Now.Rectangle(rect).SetColor(new Color(0.965f, 0.963f, 0.970f, 1f)).Draw();
            Now.Gradient(rect, Color.white, new Color(1f, 1f, 1f, 0f))
                .SetRadial(new Vector2(MonthDialCenter.x / rect.width, MonthDialCenter.y / rect.height), 0.62f)
                .Draw();
            Now.Gradient(rect, new Color(1f, 0.36f, 0.50f, 0.07f), new Color(1f, 0.36f, 0.50f, 0f))
                .SetRadial(new Vector2(0.78f, 0.18f), 0.55f)
                .Draw();

            DrawMonthTrack();
            DrawMonthProgress(angle);
            DrawMonthDisc();
            DrawMonthLabel(f);
            DrawMonthKnob(angle, held);

            DrawCursor(cursor);
            DrawRenderedTag(new NowRect(760f, 506f, 160f, 24f));
        }

        static NowRect MonthScene()
        {
            return new NowRect(
                MonthDialCenter.x - MonthSceneReach,
                MonthDialCenter.y - MonthSceneReach,
                MonthSceneReach * 2f,
                MonthSceneReach * 2f);
        }

        static Vector2 MonthLocalCenter()
        {
            return new Vector2(MonthSceneReach, MonthSceneReach);
        }

        static Vector2 MonthPointOnRing(float angleDegrees)
        {
            float radians = (angleDegrees - 90f) * Mathf.Deg2Rad;
            return MonthDialCenter + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * MonthRingRadius;
        }

        /// <summary>
        /// The idle track: one full-ring arc with a diagonal gray ramp as its
        /// fill, a faint inner shadow so it reads as a groove, and a dot at
        /// every month stop.
        /// </summary>
        static void DrawMonthTrack()
        {
            Vector2 c = MonthLocalCenter();
            NowSdf.Scene(MonthScene(), "readme-month-track")
                .SetFeather(1f)
                .SetInnerShadow(new Vector2(2f, 3f), 9f, new Color(0f, 0f, 0f, 0.10f))
                .SetShadow(new Vector2(0f, 2f), 6f, new Color(0f, 0f, 0f, 0.05f))
                .SetTexture(GetMonthTrackRamp())
                .Arc(c, MonthRingRadius, MonthRingHalfWidth, 0f, FullTurn)
                .Draw();

            for (int month = 1; month <= 12; ++month)
            {
                Vector2 dot = MonthPointOnRing(month * 30f);
                Now.Circle(dot, 2.2f).SetColor(new Color(0.28f, 0.28f, 0.32f, 0.85f)).Draw();
            }
        }

        /// <summary>
        /// The progress arc. Its fill is a conic ramp texture, so one primitive
        /// carries the rose-to-crimson sweep no matter how far the knob is
        /// dragged; the inner shadow and emboss give it a rounded tube feel.
        /// </summary>
        static void DrawMonthProgress(float angleDegrees)
        {
            float sweep = Mathf.Clamp(angleDegrees, 0.5f, 359.5f) * Mathf.Deg2Rad;
            Vector2 c = MonthLocalCenter();
            // The bar throws a wide rose glow and a soft shadow onto the track,
            // and an analytic circle mask keeps both inside the dial: the
            // ring's outer edge stays crisp against the page.
            using (Now.Mask(MonthDialMask()))
            {
                NowSdf.Scene(MonthScene(), "readme-month-progress")
                    .SetFeather(1f)
                    .SetGlow(58f, new Color(1f, 0.22f, 0.44f, 0.62f), 1.15f)
                    .SetShadow(new Vector2(0f, 10f), 22f, new Color(0.45f, 0.02f, 0.16f, 0.40f), 2f)
                    .SetInnerShadow(new Vector2(3f, 4f), 12f, new Color(0.35f, 0f, 0.10f, 0.42f))
                    .SetEmboss(new Vector2(-0.55f, -0.85f), 0.16f, 9f)
                    .SetTexture(GetMonthProgressRamp())
                    .Arc(c, MonthRingRadius, MonthRingHalfWidth, -Mathf.PI * 0.5f, sweep)
                    .Draw();
            }
        }

        /// <summary>The dial's outer silhouette, used to keep glows and shadows inside the ring.</summary>
        static NowMaskShape MonthDialMask()
        {
            return NowMaskShape.Circle(MonthDialCenter, MonthRingRadius + MonthRingHalfWidth).SetFeather(1f);
        }

        /// <summary>The raised white disc that carries the count.</summary>
        static void DrawMonthDisc()
        {
            Vector2 c = MonthLocalCenter();
            NowSdf.Scene(MonthScene(), "readme-month-disc")
                .SetFeather(1f)
                .SetShadow(new Vector2(0f, 12f), 28f, new Color(0.10f, 0.08f, 0.12f, 0.30f), 4f)
                .SetEmboss(new Vector2(-0.4f, -0.9f), 0.05f, 10f)
                .SetColor(Color.white).UseColor()
                .Circle(c, MonthDiscRadius)
                .Draw();
            // A tight contact shadow so the disc sits on the track rather than
            // floating above it.
            Now.Circle(MonthDialCenter + new Vector2(0f, 2f), MonthDiscRadius + 2f)
                .SetColor(Color.clear)
                .SetOutline(2f, new Color(0f, 0f, 0f, 0.05f))
                .Draw();
        }

        /// <summary>
        /// Draws the month count with a slide transition whenever it changes.
        /// The outgoing number rises and fades while the new one arrives from
        /// below; the loop's first frame reads the same count as its last.
        /// </summary>
        static void DrawMonthLabel(float frame)
        {
            int month = MonthCount(frame);
            int previous = month;
            int age = MonthFrames;
            for (int i = 1; i < MonthFrames; ++i)
            {
                int candidate = MonthCount(Mathf.Repeat(frame - i, MonthFrames));
                if (candidate != month)
                {
                    previous = candidate;
                    age = i;
                    break;
                }
            }

            const float slideFrames = 6f;
            float t = Smooth((age - 1) / slideFrames);
            Vector2 numberOrigin = MonthDialCenter + new Vector2(0f, -30f);
            Vector2 unitOrigin = MonthDialCenter + new Vector2(0f, 44f);

            if (t < 1f)
            {
                float leave = 1f - t;
                DrawMonthCenteredText(numberOrigin + new Vector2(0f, -22f * t), previous.ToString(), 92f, WithAlpha(MonthInk, leave), true);
                DrawMonthCenteredText(unitOrigin + new Vector2(0f, -10f * t), previous == 1 ? "month" : "months", 22f, WithAlpha(MonthInk, leave), true);
            }

            DrawMonthCenteredText(numberOrigin + new Vector2(0f, 22f * (1f - t)), month.ToString(), 92f, WithAlpha(MonthInk, t), true);
            DrawMonthCenteredText(unitOrigin + new Vector2(0f, 10f * (1f - t)), month == 1 ? "month" : "months", 22f, WithAlpha(MonthInk, t), true);
        }

        static void DrawMonthCenteredText(Vector2 center, string value, float size, Color color, bool bold)
        {
            var probe = Now.Text(new NowRect(0f, 0f, 400f, size * 1.4f)).SetFontSize(size);
            if (bold)
                probe = probe.SetBold();
            Vector2 measured = probe.Measure(value);
            var rect = new NowRect(center.x - measured.x * 0.5f, center.y - measured.y * 0.5f, measured.x + 4f, measured.y + 4f);
            DrawText(rect, value, size, color, bold);
        }

        /// <summary>
        /// The drag handle. Holding it lifts the knob: it scales up, its shadow
        /// spreads, and a soft rose halo fades in underneath.
        /// </summary>
        static void DrawMonthKnob(float angleDegrees, float held)
        {
            Vector2 knob = MonthPointOnRing(angleDegrees);
            float scale = 1f + held * 0.10f;
            float radius = MonthKnobRadius * scale;
            const float reach = 64f;
            var scene = new NowRect(knob.x - reach, knob.y - reach, reach * 2f, reach * 2f);
            var local = new Vector2(reach, reach);

            // The knob sits within the ring band, so the same dial mask clips
            // only its shadow and halo.
            using (Now.Mask(MonthDialMask()))
            {
                NowSdf.Scene(scene, "readme-month-knob")
                    .SetFeather(1f)
                    .SetShadow(new Vector2(3f + held * 2f, 6f + held * 6f), 12f + held * 10f, new Color(0.25f, 0.02f, 0.10f, 0.32f + held * 0.10f), 1f)
                    .SetGlow(4f + held * 14f, new Color(1f, 0.30f, 0.46f, 0.10f + held * 0.30f), 1.4f)
                    .SetEmboss(new Vector2(-0.5f, -0.85f), 0.12f, 7f)
                    .SetColor(Color.white).UseColor()
                    .Circle(local, radius)
                    .Draw();
            }
            Now.Circle(knob, radius - 7f)
                .SetColor(new Color(1f, 1f, 1f, 0f))
                .SetOutline(1f, new Color(0f, 0f, 0f, 0.05f))
                .Draw();
        }

        static int MonthCount(float frame)
        {
            return Mathf.Clamp(Mathf.RoundToInt(MonthKnobAngle(frame) / 30f), 1, 12);
        }

        /// <summary>
        /// Knob angle in degrees clockwise from the top. Idle at four months,
        /// dragged out to nine, back to two, then home to four before the wrap.
        /// </summary>
        static float MonthKnobAngle(float frame)
        {
            if (frame < 18f)
                return 120f;
            if (frame < 52f)
                return Mathf.Lerp(120f, 270f, Smooth(Mathf.InverseLerp(18f, 52f, frame)));
            if (frame < 62f)
                return 270f;
            if (frame < 96f)
                return Mathf.Lerp(270f, 60f, Smooth(Mathf.InverseLerp(62f, 96f, frame)));
            if (frame < 102f)
                return 60f;
            if (frame < 114f)
                return Mathf.Lerp(60f, 120f, Smooth(Mathf.InverseLerp(102f, 114f, frame)));
            return 120f;
        }

        static float MonthHeld(float frame)
        {
            return SmoothPulse(frame, 14f, 17f, 114f, 118f);
        }

        static Vector2 MonthCursorPath(float frame, float angleDegrees)
        {
            var start = new Vector2(760f, 120f);
            // The pointer tip rests just inside the knob so the arrow reads as
            // holding it, matching the reference.
            Vector2 grip = MonthPointOnRing(angleDegrees) + new Vector2(-4f, -3f);

            if (frame < 14f)
                return Vector2.Lerp(start, MonthPointOnRing(120f) + new Vector2(-4f, -3f), Smooth(Mathf.InverseLerp(0f, 14f, frame)));
            if (frame < 116f)
                return grip;
            return Vector2.Lerp(MonthPointOnRing(120f) + new Vector2(-4f, -3f), start, Smooth(Mathf.InverseLerp(116f, MonthFrames, frame)));
        }

        /// <summary>
        /// Conic ramp: rose at the top, vivid pink a third of the way round,
        /// deepening to crimson by the end of the turn. Sampled by the arc's
        /// conservative-square UVs, so texture center is dial center.
        /// </summary>
        static Texture2D GetMonthProgressRamp()
        {
            if (_monthProgressRamp != null)
                return _monthProgressRamp;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "README Month Slider Progress Ramp",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var rose = new Color(0.96f, 0.42f, 0.52f, 1f);
            var pink = new Color(1f, 0.16f, 0.40f, 1f);
            var crimson = new Color(0.78f, 0.02f, 0.30f, 1f);
            float half = size * 0.5f;

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    // Texture rows run bottom-up while the dial is measured
                    // clockwise from the top in screen space.
                    float dx = x + 0.5f - half;
                    float dyScreen = half - (y + 0.5f);
                    float turn = Mathf.Atan2(dx, -dyScreen) / FullTurn;
                    if (turn < 0f)
                        turn += 1f;
                    // The last slice eases back to rose so the round start cap,
                    // which reaches a few degrees counter-clockwise past the
                    // top, matches the arc's beginning instead of its end.
                    Color color = turn < 0.33f
                        ? Color.Lerp(rose, pink, turn / 0.33f)
                        : turn < 0.90f
                            ? Color.Lerp(pink, crimson, (turn - 0.33f) / 0.57f)
                            : Color.Lerp(crimson, rose, Mathf.SmoothStep(0f, 1f, (turn - 0.90f) / 0.10f));
                    // A slight radial lift toward the outer edge sells the tube.
                    float radial = Mathf.Clamp01(new Vector2(dx, dyScreen).magnitude / half);
                    color = Color.Lerp(color, Color.white, Mathf.SmoothStep(0f, 1f, radial) * 0.12f);
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _monthProgressRamp = texture;
            return texture;
        }

        /// <summary>Diagonal ramp for the idle track: gray at the top-left, near white at the bottom-right.</summary>
        static Texture2D GetMonthTrackRamp()
        {
            if (_monthTrackRamp != null)
                return _monthTrackRamp;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "README Month Slider Track Ramp",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var dark = new Color(0.80f, 0.80f, 0.82f, 1f);
            var light = new Color(0.975f, 0.975f, 0.98f, 1f);

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float u = (x + 0.5f) / size;
                    float v = 1f - (y + 0.5f) / size;
                    float t = Mathf.Clamp01((u + v) * 0.5f);
                    pixels[y * size + x] = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, t));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _monthTrackRamp = texture;
            return texture;
        }
    }
}
