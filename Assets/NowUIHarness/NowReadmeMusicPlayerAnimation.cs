using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// README showcase: a "now playing" card where the SDF image shapes carry the
    /// design. The album art is a transparent sprite whose shadow and accent glow
    /// follow its silhouette, and it morphs into the next track's art on skip.
    /// A vinyl spins behind it with the same art rotating on its label, the
    /// equalizer is one smooth-unioned field whose bar colors blend, the
    /// play/pause icon morphs between a triangle and two bars, and the heart
    /// fills with a glow burst. A cursor drives every interaction, and the
    /// timeline returns to its starting state so the loop wraps seamlessly.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        const int PlayerFrames = 120;

        static readonly NowSdfGraph PlayerArtA = NowSdf.Graph();
        static readonly NowSdfGraph PlayerArtB = NowSdf.Graph();
        static readonly NowSdfGraph PlayerLabelA = NowSdf.Graph();
        static readonly NowSdfGraph PlayerLabelB = NowSdf.Graph();
        static readonly NowSdfGraph PlayerPlayIcon = NowSdf.Graph();
        static readonly NowSdfGraph PlayerPauseIcon = NowSdf.Graph();

        static readonly Color PlayerAccentA = new Color(1f, 0.44f, 0.62f, 1f);
        static readonly Color PlayerAccentB = new Color(0.26f, 0.86f, 1f, 1f);
        static readonly Color PlayerHeart = new Color(1f, 0.32f, 0.52f, 1f);

        static void DrawMusicPlayer(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float u = frame.normalizedTime;
            float f = frame.index;
            Texture2D flower = NowHarnessScenarios.GetSdfImageSprite();
            Texture2D cat = GetImageEffectsLogo();

            // Beats. Track A plays, the heart is clicked, skip morphs to track B,
            // pause then play, and a final skip morphs back to A at the wrap.
            float like = Smooth(Mathf.InverseLerp(18f, 23f, f));
            float trackB = Smooth(Mathf.InverseLerp(50f, 64f, f)) * (1f - Smooth(Mathf.InverseLerp(108f, PlayerFrames, f)));
            float paused = Smooth(Mathf.InverseLerp(80f, 85f, f)) * (1f - Smooth(Mathf.InverseLerp(100f, 105f, f)));
            float heartPress = SmoothPulse(f, 16f, 18f, 19f, 22f);
            float nextPress = Mathf.Max(SmoothPulse(f, 48f, 50f, 51f, 54f), SmoothPulse(f, 106f, 108f, 109f, 112f));
            float playPress = Mathf.Max(SmoothPulse(f, 78f, 80f, 81f, 84f), SmoothPulse(f, 98f, 100f, 101f, 104f));
            float heartBurst = SmoothPulse(f, 18f, 21f, 24f, 34f);
            float likedNow = like * (1f - Smooth(Mathf.InverseLerp(50f, 54f, f)));
            Color accent = Color.Lerp(PlayerAccentA, PlayerAccentB, trackB);
            Vector2 cursor = PlayerCursorPath(f);

            float spin = PlayerSpin(f);
            float playing = 1f - paused;
            float elapsed = PlayerElapsed(f);

            var background = new Color(0.020f, 0.022f, 0.045f, 1f);
            Now.Rectangle(rect).SetColor(background).Draw();
            DrawAnimatedBackdrop(rect, u, accent, new Color(0.46f, 0.30f, 1f, 1f), PlayerAccentA);
            DrawGrid(rect, 48f, new Color(0.60f, 0.72f, 1f, 0.035f));

            var card = new NowRect(100f, 92f, 760f, 356f);
            Now.Rectangle(new NowRect(card.x - 14f, card.y + 18f, card.width + 28f, card.height + 14f))
                .SetColor(new Color(0f, 0f, 0f, 0.40f))
                .SetRadius(34f)
                .SetBlur(28f)
                .Draw();
            Now.Rectangle(card)
                .SetColor(new Color(0.055f, 0.055f, 0.10f, 0.94f))
                .SetRadius(28f)
                .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.22f))
                .Draw();
            // Ambient color bleed from the artwork, like a blurred cover behind the card.
            Now.Gradient(card, new Color(accent.r, accent.g, accent.b, 0.22f), new Color(accent.r, accent.g, accent.b, 0f))
                .SetRadial(new Vector2(0.24f, 0.42f), 0.62f)
                .SetRadius(28f)
                .Draw();

            DrawPlayerVinyl(new Vector2(card.x + 268f, card.y + 176f), spin, playing, accent, flower, cat, trackB);
            DrawPlayerArt(new NowRect(card.x + 34f, card.y + 40f, 236f, 236f), flower, cat, trackB, accent, u);

            float textX = card.x + 380f;
            float columnWidth = card.xMax - textX - 28f;
            DrawPlayerTitles(new NowRect(textX, card.y + 34f, columnWidth, 70f), trackB, likedNow, accent);
            DrawPlayerEqualizer(new NowRect(textX, card.y + 112f, columnWidth, 74f), f, playing, accent);
            DrawPlayerProgress(new NowRect(textX, card.y + 214f, columnWidth, 30f), elapsed, accent);
            DrawPlayerControls(new NowRect(textX, card.y + 260f, columnWidth, 70f), paused, playPress, nextPress, heartPress, likedNow, heartBurst, accent);

            DrawCursor(cursor);
            DrawRenderedTag(new NowRect(760f, 506f, 160f, 24f));
        }

        static void DrawPlayerArt(NowRect rect, Texture2D flower, Texture2D cat, float trackB, Color accent, float u)
        {
            float bob = Mathf.Sin(u * FullTurn * 2f) * 3f;
            var scene = new NowRect(rect.x - 40f, rect.y - 40f + bob, rect.width + 80f, rect.height + 80f);
            var artRect = new NowRect(40f, 40f, rect.width, rect.height);

            var artA = PlayerArtA.Clear()
                .SetColor(Color.white)
                .RotateNext(-6f + Mathf.Sin(u * FullTurn) * 2f)
                .Image(artRect, flower);
            var artB = PlayerArtB.Clear()
                .SetColor(new Color(0.92f, 0.96f, 1f, 1f))
                .RotateNext(4f + Mathf.Sin(u * FullTurn) * 2f)
                .Image(artRect, cat);

            NowSdf.Scene(scene, "readme-player-art")
                .SetFeather(1f)
                .SetShadow(new Vector2(0f, 14f), 22f, new Color(0f, 0f, 0f, 0.55f), 2f)
                .SetGlow(26f, new Color(accent.r, accent.g, accent.b, 0.34f), 1.4f)
                .SetOutline(1.5f, new Color(1f, 1f, 1f, 0.55f), 0.6f)
                .SetEmboss(new Vector2(-0.5f, -0.85f), 0.12f, 6f)
                .Morph(artA, artB, trackB)
                .Draw();
        }

        static void DrawPlayerVinyl(Vector2 center, float spin, float playing, Color accent, Texture2D flower, Texture2D cat, float trackB)
        {
            const float radius = 112f;
            var scene = new NowRect(center.x - radius - 24f, center.y - radius - 24f, (radius + 24f) * 2f, (radius + 24f) * 2f);
            var local = new Vector2(radius + 24f, radius + 24f);
            float spinDegrees = spin * 360f;

            // Grooves are distance contours of the disc itself; a fixed contour
            // spotlight acts as the light reflection while the label art spins.
            var labelA = PlayerLabelA.Clear()
                .SetColor(Color.white)
                .RotateNext(spinDegrees)
                .Image(new NowRect(local.x - 31f, local.y - 31f, 62f, 62f), flower);
            var labelB = PlayerLabelB.Clear()
                .SetColor(Color.white)
                .RotateNext(spinDegrees)
                .Image(new NowRect(local.x - 31f, local.y - 31f, 62f, 62f), cat);

            NowSdf.Scene(scene, "readme-player-vinyl")
                .SetFeather(1f)
                .SetShadow(new Vector2(6f, 10f), 18f, new Color(0f, 0f, 0f, 0.5f), 1f)
                .SetContours(6f, 0.9f, new Color(1f, 1f, 1f, 0.06f + playing * 0.05f), 0f, 0)
                .SetContourMask(local + new Vector2(-38f, -64f), 96f, 60f)
                .SetColor(new Color(0.06f, 0.06f, 0.09f, 1f)).UseColor()
                .Circle(local, radius)
                .Draw();

            // The label sits on top of the disc as separate draws: inside one
            // field a smaller shape unioned into a larger one is simply interior.
            Now.Circle(center, 42f).SetColor(accent).SetOutline(1f, new Color(1f, 1f, 1f, 0.28f)).Draw();
            NowSdf.Scene(scene, "readme-player-label")
                .SetFeather(1f)
                .SetShadow(new Vector2(0f, 2f), 5f, new Color(0f, 0f, 0f, 0.35f))
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.45f), 0.5f)
                .Morph(labelA, labelB, trackB)
                .Subtract()
                .Circle(local, 5f)
                .Draw();
            Now.Circle(center, 4f).SetColor(new Color(0.05f, 0.05f, 0.08f, 1f)).Draw();
        }

        static void DrawPlayerTitles(NowRect rect, float trackB, float liked, Color accent)
        {
            // Sequential crossfade with a slide: the old title leaves upward in
            // the first half of the morph, the new one arrives from below.
            float a = 1f - Mathf.Clamp01(trackB * 2f);
            float b = Mathf.Clamp01(trackB * 2f - 1f);
            float aRise = (1f - a) * -10f;
            float bRise = (1f - b) * 10f;
            var subtle = new Color(0.70f, 0.76f, 0.92f, 1f);
            DrawText(new NowRect(rect.x, rect.y + aRise, rect.width, 32f), "Petal Drift", 24f, WithAlpha(Color.white, a), true);
            DrawText(new NowRect(rect.x, rect.y + bRise, rect.width, 32f), "Purrfect Storm", 24f, WithAlpha(Color.white, b), true);
            DrawText(new NowRect(rect.x, rect.y + 34f + aRise, rect.width, 20f), "Nyx & the Pixels  ·  Bloom", 13f, WithAlpha(subtle, a));
            DrawText(new NowRect(rect.x, rect.y + 34f + bRise, rect.width, 20f), "Mochi  ·  Night Shift", 13f, WithAlpha(subtle, b));
            DrawMetricChip(new NowRect(rect.xMax - 96f, rect.y + 4f, 78f, 24f), liked > 0.5f ? "LIKED" : "LOSSLESS", liked > 0.5f ? PlayerHeart : accent);
        }

        /// <summary>
        /// Twenty capsules in one field, smooth-unioned so adjacent bars fuse
        /// into peaks and their colors blend across the fillets.
        /// </summary>
        static void DrawPlayerEqualizer(NowRect rect, float f, float playing, Color accent)
        {
            const int bars = 20;
            float step = rect.width / bars;
            var builder = NowSdf.Scene(rect, "readme-player-eq")
                .SetFeather(1f)
                .SetGlow(9f, new Color(accent.r, accent.g, accent.b, 0.22f), 1.4f)
                .SetShadow(new Vector2(0f, 3f), 6f, new Color(0f, 0f, 0f, 0.35f));

            for (int i = 0; i < bars; ++i)
            {
                float t = i / (bars - 1f);
                float phase = f / PlayerFrames * FullTurn;
                // Integer loop multiples keep every bar periodic over the capture.
                float wave = 0.55f
                    + 0.30f * Mathf.Sin(phase * 3f + i * 0.9f)
                    + 0.25f * Mathf.Sin(phase * 5f - i * 0.5f)
                    + 0.15f * Mathf.Sin(phase * 8f + i * 1.7f);
                float height = Mathf.Lerp(4f, rect.height - 6f, Mathf.Clamp01(wave) * playing + (1f - playing) * 0.06f);
                float x = step * (i + 0.5f);
                Color color = Color.Lerp(accent, new Color(0.62f, 0.40f, 1f, 1f), t);
                if (i > 0)
                    builder.SmoothUnion(7f);
                builder
                    .SetColor(color).UseColor()
                    .Capsule(new Vector2(x, rect.height - 3f), new Vector2(x, rect.height - 3f - height), 4.2f);
            }

            builder.Draw();
        }

        static void DrawPlayerProgress(NowRect rect, float elapsed, Color accent)
        {
            const float trackLength = 214f;
            float progress = Mathf.Repeat(elapsed, trackLength) / trackLength;
            var track = new NowRect(rect.x, rect.y + 10f, rect.width, 6f);
            Now.Rectangle(track).SetColor(new Color(1f, 1f, 1f, 0.10f)).SetRadius(3f).Draw();
            Now.Rectangle(new NowRect(track.x, track.y, track.width * progress, track.height))
                .SetColor(accent)
                .SetRadius(3f)
                .Draw();
            var knob = new Vector2(track.x + track.width * progress, track.center.y);
            Now.Circle(knob, 9f).SetColor(new Color(accent.r, accent.g, accent.b, 0.25f)).Draw();
            Now.Circle(knob, 5f).SetColor(Color.white).Draw();

            int seconds = Mathf.FloorToInt(progress * trackLength);
            DrawText(new NowRect(rect.x, rect.y + 20f, 60f, 16f), $"{seconds / 60}:{seconds % 60:00}", 10f, new Color(0.70f, 0.76f, 0.92f, 1f));
            DrawText(new NowRect(rect.xMax - 40f, rect.y + 20f, 40f, 16f), "3:34", 10f, new Color(0.70f, 0.76f, 0.92f, 1f));
        }

        static void DrawPlayerControls(NowRect rect, float paused, float playPress, float nextPress, float heartPress, float liked, float burst, Color accent)
        {
            float cy = rect.y + 34f;
            float prevX = rect.x + 58f;
            float playX = rect.x + 140f;
            float nextX = rect.x + 222f;
            float heartX = rect.xMax - 44f;
            var dim = new Color(0.86f, 0.88f, 0.96f, 1f);

            DrawPlayerSkipIcon(new Vector2(prevX, cy), -1f, dim, 0f);
            DrawPlayerSkipIcon(new Vector2(nextX, cy), 1f, dim, nextPress);

            // Play / pause: the field morphs between a triangle and two bars.
            float pressScale = 1f - playPress * 0.08f;
            float buttonRadius = 30f * pressScale;
            var buttonScene = new NowRect(playX - 48f, cy - 48f, 96f, 96f);
            var c = new Vector2(48f, 48f);
            var play = PlayerPlayIcon.Clear()
                .SetColor(new Color(0.04f, 0.04f, 0.08f, 1f)).UseColor()
                .Triangle(c + new Vector2(-8f, -13f), c + new Vector2(14f, 0f), c + new Vector2(-8f, 13f));
            var pause = PlayerPauseIcon.Clear()
                .SetColor(new Color(0.04f, 0.04f, 0.08f, 1f)).UseColor()
                .RoundedBox(new NowRect(c.x - 12f, c.y - 13f, 9f, 26f), 2.5f)
                .Union()
                .RoundedBox(new NowRect(c.x + 3f, c.y - 13f, 9f, 26f), 2.5f);
            NowSdf.Scene(buttonScene, "readme-player-play")
                .SetFeather(1f)
                .SetShadow(new Vector2(0f, 5f), 12f, new Color(0f, 0f, 0f, 0.45f), 1f)
                .SetGlow(10f + (1f - paused) * 6f, new Color(accent.r, accent.g, accent.b, 0.35f), 1.3f)
                .SetColor(Color.Lerp(Color.white, accent, 0.18f)).UseColor()
                .Circle(c, buttonRadius)
                .Subtract()
                .Morph(play, pause, 1f - paused)
                .Draw();

            // Heart: two circles and a rotated box fused into one field. Liking
            // fills it, scales it, and fires a glow burst that decays.
            float heartScale = 1f + burst * 0.28f - heartPress * 0.08f;
            var heartScene = new NowRect(heartX - 64f, cy - 64f, 128f, 128f);
            var h = new Vector2(64f, 65f);
            Color heartFill = Color.Lerp(new Color(0.30f, 0.32f, 0.42f, 1f), PlayerHeart, liked);
            NowSdf.Scene(heartScene, "readme-player-heart")
                .SetFeather(1f)
                .SetGlow(4f + burst * 26f + liked * 6f, new Color(PlayerHeart.r, PlayerHeart.g, PlayerHeart.b, 0.12f + burst * 0.5f + liked * 0.18f), 1.3f)
                .SetShadow(new Vector2(0f, 3f), 6f, new Color(0f, 0f, 0f, 0.35f))
                .SetColor(heartFill).UseColor()
                .Circle(h + new Vector2(-7f, -6f) * heartScale, 8.5f * heartScale)
                .SmoothUnion(3f)
                .Circle(h + new Vector2(7f, -6f) * heartScale, 8.5f * heartScale)
                .SmoothUnion(3f)
                .RotateNext(45f)
                .RoundedBox(new NowRect(h.x - 8.6f * heartScale, h.y - 8.6f * heartScale + 1f, 17.2f * heartScale, 17.2f * heartScale), 1.5f)
                .Draw();

            DrawText(new NowRect(rect.x, rect.yMax - 6f, 200f, 14f), "SHUFFLE  ·  REPEAT", 8.5f, new Color(0.50f, 0.56f, 0.72f, 1f), true);
        }

        static void DrawPlayerSkipIcon(Vector2 center, float direction, Color color, float press)
        {
            float s = 1f - press * 0.12f;
            Now.Triangle(
                    center + new Vector2(-9f * direction, -9f) * s,
                    center + new Vector2(3f * direction, 0f) * s,
                    center + new Vector2(-9f * direction, 9f) * s)
                .SetColor(color)
                .Draw();
            Now.Rectangle(new NowRect(center.x + 4f * direction * s - 1.5f, center.y - 9f * s, 3f, 18f * s))
                .SetColor(color)
                .SetRadius(1.5f)
                .Draw();
        }

        /// <summary>
        /// Integrates the play speed over the loop so the disc stops while
        /// paused yet completes whole turns by the last frame.
        /// </summary>
        static float PlayerSpin(float frame)
        {
            float total = 0f;
            float untilNow = 0f;

            for (int i = 0; i < PlayerFrames; ++i)
            {
                float speed = 1f - Smooth(Mathf.InverseLerp(80f, 85f, i)) * (1f - Smooth(Mathf.InverseLerp(100f, 105f, i)));
                total += speed;
                if (i < frame)
                    untilNow += speed;
            }

            return untilNow / total * 2f;
        }

        static float PlayerElapsed(float frame)
        {
            // Seconds of track time, scrubbing at a cinematic rate and resetting
            // on each skip; it restarts at zero on the wrap-around skip too.
            // Frames before the first skip continue the wrap-around skip at 108,
            // so the timer reads the same value on the last and first frames.
            float sinceSkip = frame < 50f ? frame + (PlayerFrames - 108f) : (frame < 108f ? frame - 50f : frame - 108f);
            float pausedSpan = Mathf.Clamp(frame - 82f, 0f, 20f);
            if (frame < 82f)
                pausedSpan = 0f;
            if (frame >= 108f)
                pausedSpan = 0f;
            return (sinceSkip - pausedSpan) * 1.8f;
        }

        static Vector2 PlayerCursorPath(float frame)
        {
            var heart = new Vector2(790f, 384f);
            var next = new Vector2(708f, 386f);
            var play = new Vector2(628f, 392f);
            var start = new Vector2(700f, 300f);

            if (frame < 14f)
                return Vector2.Lerp(start, heart, Smooth(Mathf.InverseLerp(0f, 14f, frame)));
            if (frame < 26f)
                return heart;
            if (frame < 44f)
                return Vector2.Lerp(heart, next, Smooth(Mathf.InverseLerp(26f, 44f, frame)));
            if (frame < 60f)
                return next;
            if (frame < 74f)
                return Vector2.Lerp(next, play, Smooth(Mathf.InverseLerp(60f, 74f, frame)));
            if (frame < 102f)
                return play;
            if (frame < 106f)
                return Vector2.Lerp(play, next, Smooth(Mathf.InverseLerp(102f, 106f, frame)));
            if (frame < 112f)
                return next;
            return Vector2.Lerp(next, start, Smooth(Mathf.InverseLerp(112f, PlayerFrames, frame)));
        }
    }
}
