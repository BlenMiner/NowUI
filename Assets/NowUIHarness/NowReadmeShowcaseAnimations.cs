using System.Collections.Generic;
using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// Short, deterministic loops intended for the repository README. Every
    /// moving value is derived from the harness frame rather than Unity time so
    /// rerunning the capture produces the same sequence.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        const float FullTurn = Mathf.PI * 2f;

        static readonly NowSdfGraph SdfBlob = NowSdf.Graph();
        static readonly NowSdfGraph SdfTicket = NowSdf.Graph();
        static readonly NowSdfGraph SdfPrism = NowSdf.Graph();

        static readonly Vector2[] CursorTriangle = new Vector2[3];
        static readonly Vector2[] CursorShadowTriangle = new Vector2[3];
        static readonly string[] SidebarLabels =
        {
            "Recents", "Projects", "Documents", "Downloads"
        };

        static readonly string[] GalleryLabels =
        {
            "Shader Lab", "Motion", "World UI", "Typography", "Themes", "Prototypes"
        };

        static readonly Color[] GalleryColors =
        {
            new Color(0.28f, 0.70f, 1f, 1f),
            new Color(0.70f, 0.40f, 1f, 1f),
            new Color(0.18f, 0.88f, 0.72f, 1f),
            new Color(1f, 0.50f, 0.64f, 1f),
            new Color(1f, 0.68f, 0.22f, 1f),
            new Color(0.34f, 0.82f, 1f, 1f)
        };

        static readonly Color[] DockColors =
        {
            new Color(0.20f, 0.66f, 1f, 1f),
            new Color(0.65f, 0.36f, 1f, 1f),
            new Color(1f, 0.38f, 0.42f, 1f),
            new Color(0.14f, 0.84f, 0.66f, 1f),
            new Color(1f, 0.66f, 0.16f, 1f),
            new Color(0.34f, 0.50f, 1f, 1f),
            new Color(0.94f, 0.36f, 0.76f, 1f)
        };

        static partial void Populate(List<NowHarnessAnimationScenario> scenarios)
        {
            scenarios.Add(new NowHarnessAnimationScenario(
                "sdf-metamorphosis",
                960,
                540,
                96,
                24,
                DrawSdfMetamorphosis));

            scenarios.Add(new NowHarnessAnimationScenario(
                "desktop-fidelity",
                960,
                540,
                96,
                24,
                DrawDesktopFidelity));
        }

        static void DrawSdfMetamorphosis(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float u = frame.normalizedTime;
            float angle = u * FullTurn;
            float pulse = Mathf.Sin(angle) * 0.5f + 0.5f;
            var background = new Color(0.010f, 0.015f, 0.035f, 1f);
            var cyan = new Color(0.12f, 0.92f, 1f, 1f);
            var violet = new Color(0.68f, 0.30f, 1f, 1f);
            var pink = new Color(1f, 0.24f, 0.62f, 1f);

            Now.Rectangle(rect).SetColor(background).Draw();
            DrawAnimatedBackdrop(rect, u, cyan, violet, pink);
            DrawGrid(rect, 48f, new Color(0.40f, 0.65f, 1f, 0.045f));

            Now.Text(new NowRect(40f, 28f, rect.width - 80f, 54f))
                .SetFontSize(40f)
                .SetBold()
                .SetGradient(cyan, pink)
                .SetGradientLinear(90f)
                .SetAnimation(NowTextAnimations.Wave(2.2f, 7f, 0.5f))
                .SetTime(frame.timeSeconds)
                .Draw("SDF METAMORPHOSIS");
            DrawText(
                new NowRect(43f, 82f, 690f, 26f),
                "A real distance-field transition — not a crossfade.",
                15f,
                new Color(0.70f, 0.79f, 0.94f, 1f));

            DrawMetricChip(new NowRect(744f, 30f, 76f, 28f), "1 QUAD", cyan);
            DrawMetricChip(new NowRect(826f, 30f, 94f, 28f), "LIVE FIELD", pink);

            var stage = new NowRect(154f, 116f, 652f, 332f);
            Now.Rectangle(new NowRect(stage.x - 16f, stage.y + 16f, stage.width + 32f, stage.height + 18f))
                .SetColor(new Color(0f, 0f, 0f, 0.34f))
                .SetRadius(32f)
                .SetBlur(24f)
                .Draw();
            Now.Rectangle(stage)
                .SetColor(new Color(0.025f, 0.035f, 0.075f, 0.86f))
                .SetRadius(28f)
                .SetOutline(1f, new Color(0.46f, 0.72f, 1f, 0.18f))
                .Draw();

            var scene = stage.Inset(22f);
            float w = scene.width;
            float h = scene.height;
            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            Vector2 drift = new Vector2(Mathf.Cos(angle) * 12f, Mathf.Sin(angle * 2f) * 8f);

            var blob = SdfBlob.Clear()
                .SetColor(new Color(0.05f, 0.82f, 0.92f, 1f)).UseColor()
                .Circle(center + new Vector2(-72f, -4f) + drift, 92f)
                .SetColor(new Color(0.28f, 0.42f, 1f, 1f)).UseColor()
                .SmoothUnion(34f)
                .Circle(center + new Vector2(58f, -20f) - drift * 0.45f, 84f)
                .SetColor(new Color(0.94f, 0.28f, 0.68f, 1f)).UseColor()
                .SmoothUnion(28f)
                .Capsule(new NowRect(center.x - 112f, center.y + 48f, 232f, 66f))
                .SmoothSubtract(14f)
                .Circle(center + new Vector2(8f, 2f), 38f + pulse * 8f);

            var ticket = SdfTicket.Clear()
                .SetColor(new Color(0.66f, 0.28f, 1f, 1f)).UseColor()
                .RoundedBox(new NowRect(center.x - 154f, center.y - 96f, 308f, 192f), 54f)
                .SmoothSubtract(8f)
                .Circle(new Vector2(center.x - 154f, center.y), 31f)
                .SmoothSubtract(8f)
                .Circle(new Vector2(center.x + 154f, center.y), 31f)
                .SetColor(new Color(1f, 0.34f, 0.62f, 1f)).UseColor()
                .SmoothSubtract(12f)
                .RotateNext(45f + Mathf.Sin(angle) * 8f)
                .RoundedBox(new NowRect(center.x - 43f, center.y - 43f, 86f, 86f), 20f);

            var prism = SdfPrism.Clear()
                .SetColor(new Color(1f, 0.42f, 0.56f, 1f)).UseColor()
                .RotateNext(Mathf.Sin(angle) * 12f)
                .Triangle(
                    center + new Vector2(0f, -124f),
                    center + new Vector2(132f, 98f),
                    center + new Vector2(-132f, 98f))
                .SetColor(new Color(1f, 0.74f, 0.18f, 1f)).UseColor()
                .SmoothUnion(22f)
                .Circle(center + new Vector2(0f, 30f), 72f)
                .SmoothSubtract(12f)
                .Circle(center + new Vector2(0f, 22f), 34f)
                .SetColor(new Color(0.12f, 0.94f, 0.84f, 1f)).UseColor()
                .SmoothUnion(14f)
                .Capsule(new Vector2(center.x - 106f, center.y + 55f), new Vector2(center.x + 106f, center.y + 55f), 15f);

            ResolveMorph(u, blob, ticket, prism, out NowSdfGraph from, out NowSdfGraph to, out float morph);

            var contourCenter = center + new Vector2(
                Mathf.Cos(angle) * w * 0.29f,
                Mathf.Sin(angle) * h * 0.28f);
            var lightDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            NowSdf.Scene(scene, "readme-sdf-metamorphosis")
                .SetFeather(1.4f)
                .SetShadow(new Vector2(0f, 13f), 24f, new Color(0f, 0f, 0f, 0.44f), 2f)
                .SetGlow(24f + pulse * 12f, new Color(0.18f, 0.72f, 1f, 0.22f), 1.35f)
                .SetOutline(2f, new Color(0.82f, 0.96f, 1f, 0.70f), 0.65f)
                .SetInnerShadow(new Vector2(-5f, -7f), 13f, new Color(0f, 0f, 0f, 0.16f), 1f)
                .SetEmboss(lightDirection, 0.19f, 10f)
                .SetContours(18f, 1.1f, new Color(1f, 1f, 1f, 0.16f), u * 18f, 4)
                .SetContourMask(contourCenter, 112f, 52f)
                .SetWarp(3.2f + pulse * 2.2f, 58f, 0f, Mathf.Sin(angle) * 0.72f)
                .Morph(from, to, morph)
                .Draw();

            Vector2 marker = scene.position + contourCenter;
            Now.Circle(marker, 16f).SetColor(new Color(0.30f, 0.88f, 1f, 0.10f)).Draw();
            Now.Circle(marker, 4f).SetColor(new Color(0.80f, 0.98f, 1f, 0.94f)).Draw();

            DrawText(new NowRect(42f, 472f, 876f, 22f),
                "MORPH  /  BOOLEAN OPS  /  WARP  /  EMBOSS  /  GLOW  /  CONTOURS",
                13f,
                new Color(0.62f, 0.75f, 0.94f, 1f),
                true);
            DrawRenderedTag(new NowRect(760f, 500f, 160f, 24f));
        }

        static void ResolveMorph(
            float normalizedTime,
            NowSdfGraph a,
            NowSdfGraph b,
            NowSdfGraph c,
            out NowSdfGraph from,
            out NowSdfGraph to,
            out float morph)
        {
            float cycle = normalizedTime * 3f;
            int segment = Mathf.Min(2, Mathf.FloorToInt(cycle));
            float local = cycle - segment;
            morph = Smooth(local);

            if (segment == 0)
            {
                from = a;
                to = b;
            }
            else if (segment == 1)
            {
                from = b;
                to = c;
            }
            else
            {
                from = c;
                to = a;
            }
        }

        static void DrawDesktopFidelity(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float u = frame.normalizedTime;
            float angle = u * FullTurn;
            Vector2 cursor = DesktopCursorPath(u);

            DrawDesktopWallpaper(rect, u);
            DrawMenuBar(rect);

            var window = new NowRect(92f, 54f, 776f, 406f);
            DrawDesktopWindow(window, u, cursor);

            float controlCenter = SmoothPulse(u, 0.19f, 0.29f, 0.53f, 0.64f);
            DrawControlCenter(new NowRect(710f, Mathf.Lerp(18f, 38f, controlCenter), 220f, 238f), controlCenter, u);
            DrawDesktopDock(rect, cursor, u);
            DrawCursor(cursor);
            DrawRenderedTag(new NowRect(770f, 504f, 160f, 24f));
        }

        static void DrawDesktopWallpaper(NowRect rect, float u)
        {
            float angle = u * FullTurn;
            float radialUnit = Mathf.Min(rect.width, rect.height);
            Now.Gradient(rect, new Color(0.035f, 0.055f, 0.15f, 1f), new Color(0.11f, 0.025f, 0.17f, 1f))
                .SetLinear(145f)
                .Draw();

            Now.Gradient(
                    rect,
                    new Color(0.10f, 0.72f, 1f, 0.82f),
                    new Color(0.10f, 0.42f, 1f, 0f))
                .SetRadial(
                    new Vector2(
                        (160f + Mathf.Cos(angle) * 34f) / rect.width,
                        (122f + Mathf.Sin(angle) * 24f) / rect.height),
                    360f / radialUnit)
                .Draw();
            Now.Gradient(
                    rect,
                    new Color(0.92f, 0.18f, 0.70f, 0.72f),
                    new Color(0.52f, 0.12f, 0.88f, 0f))
                .SetRadial(
                    new Vector2(
                        (790f + Mathf.Sin(angle) * 38f) / rect.width,
                        (360f + Mathf.Cos(angle) * 22f) / rect.height),
                    390f / radialUnit)
                .Draw();
            Now.Gradient(
                    rect,
                    new Color(1f, 0.56f, 0.16f, 0.32f),
                    new Color(1f, 0.30f, 0.12f, 0f))
                .SetRadial(
                    new Vector2(
                        520f / rect.width,
                        (610f + Mathf.Sin(angle) * 25f) / rect.height),
                    370f / radialUnit)
                .Draw();
        }

        static void DrawMenuBar(NowRect rect)
        {
            var bar = new NowRect(0f, 0f, rect.width, 30f);
            Now.Glass(bar)
                .SetBlurRadius(18f)
                .SetTint(new Color(0.08f, 0.10f, 0.16f, 0.48f))
                .SetVibrancy(1.25f, 0.92f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.10f))
                .Draw();

            DrawText(new NowRect(18f, 7f, 62f, 17f), "NowUI", 12f, Color.white, true);
            DrawText(new NowRect(86f, 7f, 34f, 17f), "File", 12f, new Color(1f, 1f, 1f, 0.88f));
            DrawText(new NowRect(128f, 7f, 34f, 17f), "View", 12f, new Color(1f, 1f, 1f, 0.88f));
            DrawText(new NowRect(170f, 7f, 54f, 17f), "Window", 12f, new Color(1f, 1f, 1f, 0.88f));

            DrawWifi(new Vector2(842f, 15f), new Color(1f, 1f, 1f, 0.88f));
            DrawBattery(new NowRect(866f, 10f, 24f, 11f), new Color(1f, 1f, 1f, 0.88f));
            DrawText(new NowRect(902f, 7f, 42f, 17f), "9:41", 12f, new Color(1f, 1f, 1f, 0.90f), true);
        }

        static void DrawDesktopWindow(NowRect window, float u, Vector2 cursor)
        {
            Now.Rectangle(new NowRect(window.x - 14f, window.y + 12f, window.width + 28f, window.height + 20f))
                .SetColor(new Color(0f, 0f, 0f, 0.34f))
                .SetRadius(28f)
                .SetBlur(30f)
                .Draw();
            Now.Glass(window)
                .SetBlurRadius(28f)
                .SetTint(new Color(0.055f, 0.070f, 0.12f, 0.77f))
                .SetVibrancy(1.18f, 0.92f)
                .SetRadius(16f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.20f))
                .Draw();

            float titleHeight = 48f;
            float sidebarWidth = 174f;
            var sidebar = new NowRect(window.x, window.y + titleHeight, sidebarWidth, window.height - titleHeight);
            Now.Rectangle(sidebar)
                .SetColor(new Color(0.025f, 0.035f, 0.065f, 0.36f))
                .SetRadius(0f, 0f, 0f, 16f)
                .Draw();
            Now.Rectangle(new NowRect(window.x, window.y + titleHeight, window.width, 1f))
                .SetColor(new Color(1f, 1f, 1f, 0.10f))
                .Draw();
            Now.Rectangle(new NowRect(window.x + sidebarWidth, window.y + titleHeight, 1f, window.height - titleHeight))
                .SetColor(new Color(1f, 1f, 1f, 0.09f))
                .Draw();

            DrawTrafficLights(new Vector2(window.x + 20f, window.y + 19f));
            DrawToolbar(window);
            DrawSidebar(sidebar, u);
            DrawGallery(new NowRect(window.x + sidebarWidth, window.y + titleHeight, window.width - sidebarWidth, window.height - titleHeight), u, cursor);
        }

        static void DrawTrafficLights(Vector2 origin)
        {
            Now.Circle(origin, 6f).SetColor(new Color(1f, 0.36f, 0.34f, 1f)).Draw();
            Now.Circle(origin + new Vector2(20f, 0f), 6f).SetColor(new Color(1f, 0.75f, 0.20f, 1f)).Draw();
            Now.Circle(origin + new Vector2(40f, 0f), 6f).SetColor(new Color(0.20f, 0.82f, 0.34f, 1f)).Draw();
        }

        static void DrawToolbar(NowRect window)
        {
            DrawChevron(new Vector2(window.x + 102f, window.y + 23f), -1f);
            DrawChevron(new Vector2(window.x + 128f, window.y + 23f), 1f);
            DrawText(new NowRect(window.x + 176f, window.y + 14f, 220f, 22f), "NowUI Gallery", 14f, Color.white, true);

            var search = new NowRect(window.xMax - 188f, window.y + 10f, 164f, 28f);
            Now.Rectangle(search)
                .SetColor(new Color(0.01f, 0.02f, 0.04f, 0.26f))
                .SetRadius(8f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.11f))
                .Draw();
            Now.Circle(new Vector2(search.x + 15f, search.y + 13f), 5f)
                .SetColor(Color.clear)
                .SetOutline(1.5f, new Color(1f, 1f, 1f, 0.55f))
                .Draw();
            Now.Line(new Vector2(search.x + 19f, search.y + 17f), new Vector2(search.x + 23f, search.y + 21f))
                .SetWidth(1.5f)
                .SetColor(new Color(1f, 1f, 1f, 0.55f))
                .Draw();
            DrawText(new NowRect(search.x + 31f, search.y + 7f, 118f, 16f), "Search", 11f, new Color(1f, 1f, 1f, 0.46f));
        }

        static void DrawSidebar(NowRect sidebar, float u)
        {
            DrawText(new NowRect(sidebar.x + 18f, sidebar.y + 18f, sidebar.width - 36f, 18f),
                "FAVORITES",
                10f,
                new Color(1f, 1f, 1f, 0.42f),
                true);

            float selection = Mathf.Sin(u * FullTurn - Mathf.PI * 0.5f) * 0.5f + 0.5f;
            float selectionY = Mathf.Lerp(sidebar.y + 44f, sidebar.y + 80f, Smooth(selection));
            Now.Rectangle(new NowRect(sidebar.x + 9f, selectionY, sidebar.width - 18f, 30f))
                .SetColor(new Color(0.36f, 0.58f, 1f, 0.20f))
                .SetRadius(7f)
                .Draw();

            for (int i = 0; i < SidebarLabels.Length; ++i)
            {
                float y = sidebar.y + 44f + i * 36f;
                Color icon = i == 0
                    ? new Color(0.28f, 0.72f, 1f, 1f)
                    : i == 1
                        ? new Color(0.72f, 0.46f, 1f, 1f)
                        : i == 2
                            ? new Color(0.20f, 0.88f, 0.68f, 1f)
                            : new Color(1f, 0.60f, 0.24f, 1f);
                DrawSidebarIcon(new NowRect(sidebar.x + 20f, y + 6f, 17f, 17f), i, icon);
                DrawText(new NowRect(sidebar.x + 47f, y + 7f, 104f, 18f), SidebarLabels[i], 12f, new Color(1f, 1f, 1f, 0.82f));
            }

            DrawText(new NowRect(sidebar.x + 18f, sidebar.y + 208f, sidebar.width - 36f, 18f),
                "TAGS",
                10f,
                new Color(1f, 1f, 1f, 0.42f),
                true);
            DrawTagRow(sidebar.x + 20f, sidebar.y + 238f, new Color(1f, 0.32f, 0.44f, 1f), "Showcase");
            DrawTagRow(sidebar.x + 20f, sidebar.y + 270f, new Color(0.28f, 0.78f, 1f, 1f), "In progress");
        }

        static void DrawGallery(NowRect content, float u, Vector2 cursor)
        {
            DrawText(new NowRect(content.x + 24f, content.y + 18f, 250f, 27f), "Recents", 20f, Color.white, true);
            DrawText(new NowRect(content.x + 24f, content.y + 47f, 360f, 18f),
                "Everything here is immediate-mode geometry.",
                11f,
                new Color(1f, 1f, 1f, 0.50f));

            const float cardWidth = 126f;
            const float cardHeight = 106f;
            const float gapX = 18f;
            const float gapY = 24f;
            float startX = content.x + 24f;
            float startY = content.y + 80f;
            float floatOffset = Mathf.Sin(u * FullTurn) * 2f;

            for (int i = 0; i < GalleryLabels.Length; ++i)
            {
                int col = i % 3;
                int row = i / 3;
                var card = new NowRect(
                    startX + col * (cardWidth + gapX),
                    startY + row * (cardHeight + gapY) + (col - 1) * floatOffset,
                    cardWidth,
                    cardHeight);
                float distance = Vector2.Distance(cursor, card.center);
                float hover = Mathf.Clamp01(1f - distance / 92f);
                DrawGalleryCard(card, GalleryLabels[i], GalleryColors[i], hover);
            }

            var preview = new NowRect(content.xMax - 144f, content.y + 80f, 120f, 236f);
            Now.Rectangle(preview)
                .SetColor(new Color(0.015f, 0.025f, 0.055f, 0.34f))
                .SetRadius(12f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.10f))
                .Draw();
            DrawPreviewArtwork(new NowRect(preview.x + 12f, preview.y + 12f, preview.width - 24f, 112f), u);
            DrawText(new NowRect(preview.x + 12f, preview.y + 140f, preview.width - 24f, 20f), "NowUI", 14f, Color.white, true);
            DrawText(new NowRect(preview.x + 12f, preview.y + 164f, preview.width - 24f, 42f),
                "Glass, gradients, text, masks and motion.",
                10f,
                new Color(1f, 1f, 1f, 0.54f));
            DrawMetricChip(new NowRect(preview.x + 12f, preview.yMax - 38f, 80f, 24f), "LIVE", new Color(0.16f, 0.90f, 0.70f, 1f));
        }

        static void DrawGalleryCard(NowRect card, string label, Color accent, float hover)
        {
            Now.Rectangle(new NowRect(card.x - 2f, card.y + 5f, card.width + 4f, card.height + 2f))
                .SetColor(new Color(0f, 0f, 0f, 0.18f + hover * 0.10f))
                .SetRadius(13f)
                .SetBlur(10f)
                .Draw();
            Now.Rectangle(card)
                .SetColor(new Color(0.08f + accent.r * 0.04f, 0.09f + accent.g * 0.04f, 0.14f + accent.b * 0.04f, 0.72f))
                .SetRadius(12f)
                .SetOutline(1f + hover, new Color(accent.r, accent.g, accent.b, 0.18f + hover * 0.55f))
                .Draw();

            var folder = new NowRect(card.x + 30f, card.y + 18f - hover * 3f, 66f, 48f + hover * 3f);
            DrawFolder(folder, accent);
            DrawText(new NowRect(card.x + 10f, card.yMax - 26f, card.width - 20f, 18f), label, 11f, new Color(1f, 1f, 1f, 0.84f), true);
        }

        static void DrawFolder(NowRect rect, Color accent)
        {
            Now.Gradient(
                    new NowRect(rect.x, rect.y + 8f, rect.width, rect.height - 8f),
                    new Color(Mathf.Min(1f, accent.r + 0.18f), Mathf.Min(1f, accent.g + 0.18f), Mathf.Min(1f, accent.b + 0.18f), 1f),
                    accent)
                .SetLinear(150f)
                .SetRadius(7f)
                .Draw();
            Now.Rectangle(new NowRect(rect.x + 6f, rect.y + 2f, rect.width * 0.42f, 15f))
                .SetColor(new Color(Mathf.Min(1f, accent.r + 0.12f), Mathf.Min(1f, accent.g + 0.12f), Mathf.Min(1f, accent.b + 0.12f), 1f))
                .SetRadius(6f, 6f, 0f, 0f)
                .Draw();
            Now.Rectangle(new NowRect(rect.x + 8f, rect.y + 14f, rect.width - 16f, 1f))
                .SetColor(new Color(1f, 1f, 1f, 0.28f))
                .Draw();
        }

        static void DrawPreviewArtwork(NowRect rect, float u)
        {
            float angle = u * FullTurn;
            Now.Gradient(rect, new Color(0.08f, 0.62f, 1f, 1f), new Color(0.70f, 0.20f, 0.88f, 1f))
                .SetLinear(135f)
                .SetRadius(10f)
                .Draw();
            Now.Circle(rect.center + new Vector2(Mathf.Cos(angle) * 14f, Mathf.Sin(angle) * 10f), 22f)
                .SetColor(new Color(1f, 0.82f, 0.28f, 0.94f))
                .Draw();
            Now.Triangle(
                    new Vector2(rect.x + 4f, rect.yMax - 10f),
                    new Vector2(rect.center.x - 2f, rect.y + 46f),
                    new Vector2(rect.center.x + 16f, rect.yMax - 10f))
                .SetColor(new Color(0.05f, 0.22f, 0.42f, 0.84f))
                .Draw();
            Now.Triangle(
                    new Vector2(rect.center.x - 8f, rect.yMax - 10f),
                    new Vector2(rect.xMax - 28f, rect.y + 54f),
                    new Vector2(rect.xMax - 4f, rect.yMax - 10f))
                .SetColor(new Color(0.06f, 0.34f, 0.46f, 0.88f))
                .Draw();
        }

        static void DrawControlCenter(NowRect panel, float visibility, float u)
        {
            if (visibility <= 0.001f)
                return;

            float alpha = Smooth(visibility);
            Now.Rectangle(new NowRect(panel.x - 10f, panel.y + 10f, panel.width + 20f, panel.height + 16f))
                .SetColor(new Color(0f, 0f, 0f, 0.28f * alpha))
                .SetRadius(24f)
                .SetBlur(20f)
                .Draw();
            Now.Glass(panel)
                .SetBlurRadius(30f * alpha)
                .SetTint(new Color(0.11f, 0.13f, 0.20f, 0.82f * alpha))
                .SetVibrancy(Mathf.Lerp(1f, 1.25f, alpha), Mathf.Lerp(1f, 0.98f, alpha))
                .SetRadius(18f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.24f * alpha))
                .Draw();

            DrawText(new NowRect(panel.x + 16f, panel.y + 14f, panel.width - 32f, 22f), "Control Center", 14f, WithAlpha(Color.white, alpha), true);
            DrawControlToggle(new NowRect(panel.x + 14f, panel.y + 46f, 92f, 58f), "Wi-Fi", new Color(0.24f, 0.62f, 1f, alpha), alpha);
            DrawControlToggle(new NowRect(panel.x + 114f, panel.y + 46f, 92f, 58f), "Focus", new Color(0.68f, 0.38f, 1f, alpha), alpha);

            DrawText(new NowRect(panel.x + 16f, panel.y + 119f, 100f, 17f), "Display", 11f, WithAlpha(Color.white, alpha * 0.76f), true);
            var slider = new NowRect(panel.x + 16f, panel.y + 143f, panel.width - 32f, 9f);
            Now.Rectangle(slider).SetColor(new Color(1f, 1f, 1f, 0.16f * alpha)).SetRadius(5f).Draw();
            float level = 0.42f + (Mathf.Sin(u * FullTurn) * 0.5f + 0.5f) * 0.38f;
            Now.Rectangle(new NowRect(slider.x, slider.y, slider.width * level, slider.height))
                .SetColor(new Color(1f, 1f, 1f, 0.86f * alpha))
                .SetRadius(5f)
                .Draw();
            Now.Circle(new Vector2(slider.x + slider.width * level, slider.center.y), 8f)
                .SetColor(new Color(1f, 1f, 1f, alpha))
                .Draw();

            DrawText(new NowRect(panel.x + 16f, panel.y + 176f, 100f, 17f), "Now playing", 11f, WithAlpha(Color.white, alpha * 0.76f), true);
            DrawText(new NowRect(panel.x + 16f, panel.y + 198f, 142f, 18f), "Immediate Motion", 12f, WithAlpha(Color.white, alpha), true);
            Now.Circle(new Vector2(panel.xMax - 34f, panel.y + 199f), 16f)
                .SetColor(new Color(1f, 1f, 1f, 0.14f * alpha))
                .Draw();
            Now.Triangle(
                    new Vector2(panel.xMax - 38f, panel.y + 191f),
                    new Vector2(panel.xMax - 38f, panel.y + 207f),
                    new Vector2(panel.xMax - 26f, panel.y + 199f))
                .SetColor(new Color(1f, 1f, 1f, 0.90f * alpha))
                .Draw();
        }

        static void DrawControlToggle(NowRect rect, string label, Color accent, float alpha)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(1f, 1f, 1f, 0.09f * alpha))
                .SetRadius(12f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.10f * alpha))
                .Draw();
            Now.Circle(new Vector2(rect.x + 22f, rect.y + 22f), 13f).SetColor(accent).Draw();
            Now.Circle(new Vector2(rect.x + 22f, rect.y + 22f), 4f).SetColor(new Color(1f, 1f, 1f, alpha)).Draw();
            DrawText(new NowRect(rect.x + 42f, rect.y + 14f, rect.width - 48f, 18f), label, 11f, WithAlpha(Color.white, alpha), true);
            DrawText(new NowRect(rect.x + 42f, rect.y + 31f, rect.width - 48f, 14f), "On", 9f, WithAlpha(Color.white, alpha * 0.52f));
        }

        static void DrawDesktopDock(NowRect rect, Vector2 cursor, float u)
        {
            var dock = new NowRect(rect.width * 0.5f - 190f, rect.height - 66f, 380f, 58f);
            Now.Glass(dock)
                .SetBlurRadius(26f)
                .SetTint(new Color(0.08f, 0.10f, 0.16f, 0.58f))
                .SetVibrancy(1.30f, 0.96f)
                .SetRadius(18f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.24f))
                .Draw();

            float start = dock.x + 22f;
            const float spacing = 50f;
            float dockAttention = Mathf.Clamp01((cursor.y - 414f) / 72f);
            for (int i = 0; i < DockColors.Length; ++i)
            {
                float centerX = start + i * spacing + 20f;
                float proximity = Mathf.Clamp01(1f - Mathf.Abs(cursor.x - centerX) / 82f) * dockAttention;
                float bounce = i == 3 ? Mathf.Max(0f, Mathf.Sin(u * FullTurn * 2f)) * 2f : 0f;
                float size = 38f + proximity * 14f;
                var icon = new NowRect(centerX - size * 0.5f, dock.y + 10f - proximity * 10f - bounce, size, size);
                DrawDockIcon(icon, DockColors[i], i);
            }
        }

        static void DrawDockIcon(NowRect rect, Color accent, int index)
        {
            Color light = new Color(
                Mathf.Min(1f, accent.r + 0.24f),
                Mathf.Min(1f, accent.g + 0.24f),
                Mathf.Min(1f, accent.b + 0.24f),
                1f);
            Now.Rectangle(new NowRect(rect.x - 1f, rect.y + 3f, rect.width + 2f, rect.height + 2f))
                .SetColor(new Color(0f, 0f, 0f, 0.25f))
                .SetRadius(rect.width * 0.24f)
                .SetBlur(6f)
                .Draw();
            Now.Gradient(rect, light, accent)
                .SetLinear(145f)
                .SetRadius(rect.width * 0.24f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.34f))
                .Draw();

            Vector2 center = rect.center;
            float unit = rect.width / 38f;
            if (index % 3 == 0)
            {
                Now.Circle(center, 8f * unit).SetColor(new Color(1f, 1f, 1f, 0.88f)).Draw();
                Now.Circle(center, 3f * unit).SetColor(accent).Draw();
            }
            else if (index % 3 == 1)
            {
                Now.Rectangle(new NowRect(center.x - 8f * unit, center.y - 6f * unit, 16f * unit, 12f * unit))
                    .SetColor(new Color(1f, 1f, 1f, 0.88f))
                    .SetRadius(3f * unit)
                    .Draw();
            }
            else
            {
                Now.Triangle(
                        center + new Vector2(0f, -9f * unit),
                        center + new Vector2(9f * unit, 8f * unit),
                        center + new Vector2(-9f * unit, 8f * unit))
                    .SetColor(new Color(1f, 1f, 1f, 0.88f))
                    .Draw();
            }
        }

        static Vector2 DesktopCursorPath(float u)
        {
            Vector2 a = new Vector2(318f, 205f);
            Vector2 b = new Vector2(820f, 18f);
            Vector2 c = new Vector2(610f, 488f);
            float cycle = u * 3f;
            int segment = Mathf.Min(2, Mathf.FloorToInt(cycle));
            float t = Smooth(cycle - segment);

            if (segment == 0)
                return Vector2.Lerp(a, b, t);
            if (segment == 1)
                return Vector2.Lerp(b, c, t);
            return Vector2.Lerp(c, a, t);
        }

        static void DrawCursor(Vector2 position)
        {
            CursorTriangle[0] = position;
            CursorTriangle[1] = position + new Vector2(2f, 22f);
            CursorTriangle[2] = position + new Vector2(15f, 15f);
            CursorShadowTriangle[0] = CursorTriangle[0] + new Vector2(2f, 3f);
            CursorShadowTriangle[1] = CursorTriangle[1] + new Vector2(2f, 3f);
            CursorShadowTriangle[2] = CursorTriangle[2] + new Vector2(2f, 3f);
            Now.Polygon(CursorShadowTriangle)
                .SetColor(new Color(0f, 0f, 0f, 0.62f))
                .Draw();
            Now.Polygon(CursorTriangle)
                .SetColor(Color.white)
                .SetOutline(1f, new Color(0.04f, 0.05f, 0.09f, 0.92f))
                .Draw();
        }

        static void DrawSidebarIcon(NowRect rect, int index, Color color)
        {
            if ((index & 1) == 0)
            {
                Now.Circle(rect.center, rect.width * 0.48f).SetColor(new Color(color.r, color.g, color.b, 0.22f)).Draw();
                Now.Circle(rect.center, rect.width * 0.22f).SetColor(color).Draw();
            }
            else
            {
                Now.Rectangle(rect).SetColor(new Color(color.r, color.g, color.b, 0.22f)).SetRadius(4f).Draw();
                Now.Rectangle(rect.Inset(5f)).SetColor(color).SetRadius(2f).Draw();
            }
        }

        static void DrawTagRow(float x, float y, Color color, string label)
        {
            Now.Circle(new Vector2(x + 7f, y + 7f), 6f).SetColor(color).Draw();
            DrawText(new NowRect(x + 22f, y, 112f, 17f), label, 11f, new Color(1f, 1f, 1f, 0.70f));
        }

        static void DrawChevron(Vector2 center, float direction)
        {
            Color color = new Color(1f, 1f, 1f, 0.56f);
            Now.Line(center + new Vector2(3f * direction, -5f), center + new Vector2(-2f * direction, 0f))
                .SetWidth(1.6f).SetColor(color).Draw();
            Now.Line(center + new Vector2(-2f * direction, 0f), center + new Vector2(3f * direction, 5f))
                .SetWidth(1.6f).SetColor(color).Draw();
        }

        static void DrawWifi(Vector2 center, Color color)
        {
            Now.Bezier(
                    center + new Vector2(-9f, -1f),
                    center + new Vector2(-5f, -7f),
                    center + new Vector2(5f, -7f),
                    center + new Vector2(9f, -1f))
                .SetWidth(1.5f).SetColor(color).Draw();
            Now.Bezier(
                    center + new Vector2(-5f, 2f),
                    center + new Vector2(-3f, -1f),
                    center + new Vector2(3f, -1f),
                    center + new Vector2(5f, 2f))
                .SetWidth(1.5f).SetColor(color).Draw();
            Now.Circle(center + new Vector2(0f, 5f), 1.5f).SetColor(color).Draw();
        }

        static void DrawBattery(NowRect rect, Color color)
        {
            Now.Rectangle(rect).SetColor(Color.clear).SetRadius(3f).SetOutline(1f, color).Draw();
            Now.Rectangle(new NowRect(rect.xMax + 2f, rect.y + 3f, 2f, rect.height - 6f)).SetColor(color).SetRadius(1f).Draw();
            Now.Rectangle(rect.Inset(2.5f)).SetColor(new Color(color.r, color.g, color.b, 0.82f)).SetRadius(1.5f).Draw();
        }

        static void DrawAnimatedBackdrop(NowRect rect, float u, Color cyan, Color violet, Color pink)
        {
            float angle = u * FullTurn;
            float radialUnit = Mathf.Min(rect.width, rect.height);
            Now.Gradient(rect, new Color(cyan.r, cyan.g, cyan.b, 0.16f), new Color(cyan.r, cyan.g, cyan.b, 0f))
                .SetRadial(
                    new Vector2(
                        (160f + Mathf.Cos(angle) * 60f) / rect.width,
                        (180f + Mathf.Sin(angle) * 40f) / rect.height),
                    360f / radialUnit)
                .Draw();
            Now.Gradient(rect, new Color(violet.r, violet.g, violet.b, 0.18f), new Color(violet.r, violet.g, violet.b, 0f))
                .SetRadial(
                    new Vector2(
                        (800f + Mathf.Sin(angle) * 70f) / rect.width,
                        (400f + Mathf.Cos(angle) * 40f) / rect.height),
                    390f / radialUnit)
                .Draw();
            Now.Gradient(rect, new Color(pink.r, pink.g, pink.b, 0.10f), new Color(pink.r, pink.g, pink.b, 0f))
                .SetRadial(
                    new Vector2(
                        520f / rect.width,
                        (520f + Mathf.Sin(angle) * 24f) / rect.height),
                    300f / radialUnit)
                .Draw();
        }

        static void DrawGrid(NowRect rect, float spacing, Color color)
        {
            for (float x = spacing; x < rect.width; x += spacing)
                Now.Rectangle(new NowRect(x, 0f, 1f, rect.height)).SetColor(color).Draw();
            for (float y = spacing; y < rect.height; y += spacing)
                Now.Rectangle(new NowRect(0f, y, rect.width, 1f)).SetColor(color).Draw();
        }

        static void DrawMetricChip(NowRect rect, string label, Color accent)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(accent.r, accent.g, accent.b, 0.10f))
                .SetRadius(rect.height * 0.5f)
                .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.42f))
                .Draw();
            DrawText(new NowRect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 15f), label, 9f, accent, true);
        }

        static void DrawRenderedTag(NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.01f, 0.02f, 0.05f, 0.62f))
                .SetRadius(rect.height * 0.5f)
                .SetOutline(1f, new Color(1f, 1f, 1f, 0.16f))
                .Draw();
            float y = rect.center.y;
            Now.Line(new Vector2(rect.x + 10f, y), new Vector2(rect.x + 16f, y)).SetWidth(1.5f).SetColor(new Color(0.55f, 0.62f, 1f, 1f)).Draw();
            Now.Line(new Vector2(rect.x + 16f, y), new Vector2(rect.x + 20f, y - 5f)).SetWidth(1.5f).SetColor(new Color(0.55f, 0.62f, 1f, 1f)).Draw();
            Now.Line(new Vector2(rect.x + 20f, y - 5f), new Vector2(rect.x + 25f, y + 5f)).SetWidth(1.5f).SetColor(new Color(0.55f, 0.62f, 1f, 1f)).Draw();
            Now.Line(new Vector2(rect.x + 25f, y + 5f), new Vector2(rect.x + 30f, y)).SetWidth(1.5f).SetColor(new Color(0.55f, 0.62f, 1f, 1f)).Draw();
            DrawText(new NowRect(rect.x + 38f, rect.y + 5f, rect.width - 44f, 16f), "Rendered with NowUI", 10f, new Color(1f, 1f, 1f, 0.74f));
        }

        static void DrawText(NowRect rect, string value, float size, Color color, bool bold = false)
        {
            var text = Now.Text(rect).SetFontSize(size).SetColor(color);
            if (bold)
                text = text.SetBold();
            text.Draw(value);
        }

        static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static float SmoothPulse(float value, float inStart, float inEnd, float outStart, float outEnd)
        {
            float fadeIn = Smooth(Mathf.InverseLerp(inStart, inEnd, value));
            float fadeOut = 1f - Smooth(Mathf.InverseLerp(outStart, outEnd, value));
            return Mathf.Min(fadeIn, fadeOut);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
