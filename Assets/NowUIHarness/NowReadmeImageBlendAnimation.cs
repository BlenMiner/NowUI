using NowUI.Sdf;
using UnityEngine;

namespace NowUI.Editor
{
    /// <summary>
    /// README showcase: three unrelated textures and an analytic shape share one
    /// distance field. A satellite morphs between two sprites while it orbits
    /// the PurrNet logo, smooth unions bridge the silhouettes, and the fill
    /// colors blend exactly where the geometry does.
    /// </summary>
    internal static partial class NowHarnessAnimationScenarios
    {
        static readonly NowSdfGraph SdfBlendLogo = NowSdf.Graph();
        static readonly NowSdfGraph SdfBlendFlower = NowSdf.Graph();
        static readonly NowSdfGraph SdfBlendRing = NowSdf.Graph();

        static void DrawSdfImageBlend(NowRect rect, NowHarnessAnimationFrame frame)
        {
            float u = frame.normalizedTime;
            float angle = u * FullTurn;
            float pulse = Mathf.Sin(angle) * 0.5f + 0.5f;
            var background = new Color(0.010f, 0.015f, 0.035f, 1f);
            var orange = new Color(1f, 0.62f, 0.10f, 1f);
            var amber = new Color(1f, 0.82f, 0.32f, 1f);
            var violet = new Color(0.62f, 0.34f, 1f, 1f);
            var cyan = new Color(0.20f, 0.90f, 1f, 1f);
            Texture2D logo = GetImageEffectsLogo();
            Texture2D flower = NowHarnessScenarios.GetSdfImageSprite();
            Texture2D ring = NowHarnessScenarios.GetSdfImageRing();

            Now.Rectangle(rect).SetColor(background).Draw();
            DrawAnimatedBackdrop(rect, u, cyan, violet, orange);
            DrawGrid(rect, 48f, new Color(0.60f, 0.72f, 1f, 0.045f));

            Now.Text(new NowRect(40f, 28f, rect.width - 80f, 54f))
                .SetFontSize(40f)
                .SetBold()
                .SetGradient(cyan, amber)
                .SetGradientLinear(90f)
                .SetAnimation(NowTextAnimations.Wave(2.2f, 7f, 0.5f))
                .SetTime(frame.timeSeconds)
                .Draw("SDF IMAGE BLENDING");
            DrawText(
                new NowRect(43f, 82f, 690f, 26f),
                "Three textures and a shape in one field. Colors blend where the geometry does.",
                15f,
                new Color(0.72f, 0.84f, 0.96f, 1f));

            DrawMetricChip(new NowRect(690f, 30f, 130f, 28f), "3 TEXTURES", cyan);
            DrawMetricChip(new NowRect(826f, 30f, 94f, 28f), "1 QUAD", amber);

            var stage = new NowRect(154f, 116f, 652f, 332f);
            Now.Rectangle(new NowRect(stage.x - 16f, stage.y + 16f, stage.width + 32f, stage.height + 18f))
                .SetColor(new Color(0f, 0f, 0f, 0.34f))
                .SetRadius(32f)
                .SetBlur(24f)
                .Draw();
            Now.Rectangle(stage)
                .SetColor(new Color(0.030f, 0.035f, 0.075f, 0.86f))
                .SetRadius(28f)
                .SetOutline(1f, new Color(0.46f, 0.72f, 1f, 0.18f))
                .Draw();

            var scene = stage.Inset(22f);
            var center = new Vector2(scene.width * 0.5f - 40f, scene.height * 0.5f);
            var lightDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // Whole turns per loop keep the encoded GIF seamless.
            var satelliteCenter = center + new Vector2(Mathf.Cos(angle) * 210f, Mathf.Sin(angle) * 78f);
            var blobCenter = center + new Vector2(Mathf.Cos(angle + Mathf.PI) * 150f, Mathf.Sin(angle * 2f) * 84f);
            float satelliteMorph = 0.5f - 0.5f * Mathf.Cos(angle);
            float bridge = 26f + pulse * 18f;

            const float logoSize = 196f;
            var logoGraph = SdfBlendLogo.Clear()
                .SetColor(Color.white)
                .RotateNext(Mathf.Sin(angle) * 5f)
                .Image(new NowRect(center.x - logoSize * 0.5f, center.y - logoSize * 0.5f + 4f, logoSize, logoSize), logo);

            const float flowerSize = 150f;
            var flowerGraph = SdfBlendFlower.Clear()
                .SetColor(Color.white)
                .RotateNext(-angle * Mathf.Rad2Deg)
                .Image(new NowRect(satelliteCenter.x - flowerSize * 0.5f, satelliteCenter.y - flowerSize * 0.5f, flowerSize, flowerSize), flower);

            const float ringSize = 124f;
            var ringGraph = SdfBlendRing.Clear()
                .SetColor(Color.white)
                .Image(new NowRect(satelliteCenter.x - ringSize * 0.5f, satelliteCenter.y - ringSize * 0.5f, ringSize, ringSize), ring);

            NowSdf.Scene(scene, "readme-sdf-image-blend")
                .SetFeather(1.2f)
                .SetShadow(new Vector2(0f, 12f), 22f, new Color(0f, 0f, 0f, 0.5f), 2f)
                .SetGlow(14f, new Color(0.30f, 0.70f, 1f, 0.22f), 1.3f)
                .SetOutline(2f, new Color(0.90f, 0.96f, 1f, 0.8f), 0.6f)
                .SetEmboss(lightDirection, 0.22f, 8f)
                .Graph(logoGraph)
                .SmoothUnion(bridge)
                .Morph(flowerGraph, ringGraph, satelliteMorph)
                .SetColor(violet).UseColor()
                .SmoothUnion(34f)
                .Circle(blobCenter, 40f + pulse * 10f)
                .Draw();

            DrawText(new NowRect(42f, 472f, 876f, 22f),
                "IMAGE + IMAGE SMOOTH UNION  /  IMAGE TO IMAGE MORPH  /  SHAPE + IMAGE BLEND  /  SHARED EFFECTS",
                13f,
                new Color(0.66f, 0.78f, 0.94f, 1f),
                true);
            DrawRenderedTag(new NowRect(760f, 500f, 160f, 24f));
        }
    }
}
