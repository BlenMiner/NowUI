using System.IO;
using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// README showcase: one transparent PNG (the PurrNet logo) drawn as an SDF
    /// image shape. Every scene effect follows the alpha silhouette through the
    /// GPU-baked distance field, and the source texture is deliberately loaded
    /// without CPU read access to demonstrate that no import flag is required.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        const string ImageEffectsLogoPath = "Docs/media/readme/purrnet-logo.png";

        static readonly NowSdfGraph SdfImageLogo = NowSdf.Graph();
        static readonly NowSdfGraph SdfImagePaw = NowSdf.Graph();

        static Texture2D _imageEffectsLogo;

        static Texture2D GetImageEffectsLogo()
        {
            if (_imageEffectsLogo != null)
                return _imageEffectsLogo;

            string path = Path.Combine(NowHarnessScenarios.ProjectPath(), ImageEffectsLogoPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "README PurrNet Logo",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!File.Exists(path) || !texture.LoadImage(File.ReadAllBytes(path), markNonReadable: true))
                throw new FileNotFoundException($"README image-effects logo missing or unreadable at '{path}'.");

            _imageEffectsLogo = texture;
            return texture;
        }

        static void DrawSdfImageEffects(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float u = frame.normalizedTime;
            float angle = u * FullTurn;
            float pulse = Mathf.Sin(angle) * 0.5f + 0.5f;
            var background = new Color(0.010f, 0.015f, 0.035f, 1f);
            var orange = new Color(1f, 0.62f, 0.10f, 1f);
            var amber = new Color(1f, 0.82f, 0.32f, 1f);
            var violet = new Color(0.62f, 0.34f, 1f, 1f);
            var pink = new Color(1f, 0.30f, 0.56f, 1f);
            Texture2D logo = GetImageEffectsLogo();

            Now.Rectangle(rect).SetColor(background).Draw();
            DrawAnimatedBackdrop(rect, u, orange, violet, pink);
            DrawGrid(rect, 48f, new Color(1f, 0.72f, 0.40f, 0.045f));

            Now.Text(new NowRect(40f, 28f, rect.width - 80f, 54f))
                .SetFontSize(40f)
                .SetBold()
                .SetGradient(amber, pink)
                .SetGradientLinear(90f)
                .SetAnimation(NowTextAnimations.Wave(2.2f, 7f, 0.5f))
                .SetTime(frame.timeSeconds)
                .Draw("SDF IMAGE EFFECTS");
            DrawText(
                new NowRect(43f, 82f, 690f, 26f),
                "One transparent PNG. Every effect follows its alpha silhouette.",
                15f,
                new Color(0.94f, 0.82f, 0.66f, 1f));

            DrawMetricChip(new NowRect(704f, 30f, 116f, 28f), "GPU BAKED", amber);
            DrawMetricChip(new NowRect(826f, 30f, 94f, 28f), "NO R/W", pink);

            var stage = new NowRect(154f, 116f, 652f, 332f);
            Now.Rectangle(new NowRect(stage.x - 16f, stage.y + 16f, stage.width + 32f, stage.height + 18f))
                .SetColor(new Color(0f, 0f, 0f, 0.34f))
                .SetRadius(32f)
                .SetBlur(24f)
                .Draw();
            Now.Rectangle(stage)
                .SetColor(new Color(0.045f, 0.030f, 0.060f, 0.86f))
                .SetRadius(28f)
                .SetOutline(1f, new Color(1f, 0.72f, 0.40f, 0.18f))
                .Draw();

            // The untouched source sprite, for reference, drawn as an ordinary
            // textured rectangle beside the stage.
            var sourceRect = new NowRect(34f, 300f, 96f, 96f);
            Now.Rectangle(sourceRect.Inset(-10f))
                .SetColor(new Color(0.05f, 0.04f, 0.08f, 0.9f))
                .SetRadius(18f)
                .SetOutline(1f, new Color(1f, 0.72f, 0.40f, 0.22f))
                .Draw();
            Now.Rectangle(sourceRect).SetColor(Color.white).SetTexture(logo).Draw();
            DrawText(new NowRect(18f, 404f, 128f, 18f), "SOURCE PNG", 11f, new Color(0.86f, 0.74f, 0.58f, 1f), true);
            DrawText(new NowRect(18f, 420f, 128f, 18f), "alpha silhouette", 11f, new Color(0.62f, 0.55f, 0.50f, 1f));

            var scene = stage.Inset(22f);
            float w = scene.width;
            float h = scene.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            var lightDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 shadowOffset = -lightDirection * 14f;
            // Every moving value completes whole turns per loop so the encoded
            // GIF wraps seamlessly from the last frame back to the first.
            var contourCenter = center + new Vector2(
                Mathf.Cos(angle + 1.2f) * w * 0.30f,
                Mathf.Sin(angle * 2f + 0.6f) * h * 0.30f);

            const float logoSize = 236f;
            var logoRect = new NowRect(center.x - logoSize * 0.5f, center.y - logoSize * 0.5f + 4f, logoSize, logoSize);
            float biteAngle = -angle;
            var biteCenter = center + new Vector2(Mathf.Cos(biteAngle) * 108f, Mathf.Sin(biteAngle) * 96f);

            // The image is an ordinary graph operand, so it morphs like any
            // analytic field: the logo's silhouette flows into a paw print and
            // back while every effect keeps tracking the blended distance.
            var logoGraph = SdfImageLogo.Clear()
                .SetColor(Color.white)
                .RotateNext(Mathf.Sin(angle) * 7f)
                .Image(logoRect, logo)
                .SmoothSubtract(10f)
                .Circle(biteCenter, 26f + pulse * 10f);

            var paw = SdfImagePaw.Clear()
                .SetColor(new Color(1f, 0.62f, 0.10f, 1f)).UseColor()
                .Ellipse(new NowRect(center.x - 64f, center.y - 4f, 128f, 104f))
                .SetColor(new Color(1f, 0.80f, 0.28f, 1f)).UseColor()
                .SmoothUnion(16f)
                .Circle(center + new Vector2(-78f, -46f), 28f)
                .SmoothUnion(16f)
                .Circle(center + new Vector2(-28f, -84f), 30f)
                .SmoothUnion(16f)
                .Circle(center + new Vector2(30f, -84f), 30f)
                .SmoothUnion(16f)
                .Circle(center + new Vector2(80f, -46f), 28f);

            // Hold the logo, morph into the paw, hold, and morph back.
            float phase = Mathf.Repeat(u * 2f, 1f);
            float morph = Smooth(Mathf.Clamp01((phase - 0.35f) / 0.35f));
            bool toPaw = u < 0.5f;
            NowSdfGraph from = toPaw ? logoGraph : paw;
            NowSdfGraph to = toPaw ? paw : logoGraph;

            NowSdf.Scene(scene, "readme-sdf-image-effects")
                .SetFeather(1.2f)
                .SetShadow(shadowOffset, 22f, new Color(0f, 0f, 0f, 0.55f), 2f)
                .SetGlow(18f + pulse * 14f, new Color(1f, 0.58f, 0.14f, 0.30f), 1.35f)
                .SetOutline(2.2f, new Color(1f, 0.90f, 0.72f, 0.85f), 0.6f)
                .SetInnerShadow(-lightDirection * 6f, 12f, new Color(0f, 0f, 0f, 0.22f), 1f)
                .SetEmboss(lightDirection, 0.26f, 8f)
                .SetContours(16f, 1.1f, new Color(1f, 0.92f, 0.78f, 0.20f), u * 16f, 4)
                .SetContourMask(contourCenter, 104f, 48f)
                .Morph(from, to, morph)
                .Draw();

            Vector2 marker = scene.position + contourCenter;
            Now.Circle(marker, 16f).SetColor(new Color(1f, 0.80f, 0.40f, 0.10f)).Draw();
            Now.Circle(marker, 4f).SetColor(new Color(1f, 0.95f, 0.80f, 0.94f)).Draw();

            DrawText(new NowRect(42f, 472f, 876f, 22f),
                "IMAGE  /  MORPH  /  SHADOW  /  GLOW  /  OUTLINE  /  EMBOSS  /  CONTOURS  /  BOOLEAN CUT",
                13f,
                new Color(0.90f, 0.76f, 0.58f, 1f),
                true);
            DrawRenderedTag(new NowRect(760f, 500f, 160f, 24f));
        }
    }
}
