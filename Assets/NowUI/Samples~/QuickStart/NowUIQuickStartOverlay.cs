using NowUI;
using NowUI.Sdf;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUIQuickStartOverlay : MonoBehaviour
{
    void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            Debug.LogWarning("NowUIQuickStartOverlay draws from OnPostRender, which only runs on the Built-in Render Pipeline. On URP/HDRP use the NowUniversalRendererFeature / HDRP custom pass with a NowPipelineLayoutGraphic, or a NowLayoutGraphic under a Canvas — see the package's Documentation~/RenderPipelines.md guide.", this);

        if (!TryGetComponent<Camera>(out var cam) || !cam.enabled)
            Debug.LogWarning("NowUIQuickStartOverlay must live on an enabled Camera for OnPostRender to fire.", this);
    }

    void OnPostRender()
    {
        using (Now.StartUI(NowScreen.recommendedUIScale))
            NowLayout.RunMeasured(
                NowScreen.safeArea,
                this,
                static self => self.DrawOverlay(),
                spacing: 8f,
                padding: 18f);
    }

    void DrawOverlay()
    {
        var titleRect = NowLayout.ReserveRect(width: 180f, height: 38f);
        float titleTime = Mathf.Repeat(Time.unscaledTime, 2.5f);

        Now.Text(titleRect)
            .SetFontSize(28f)
            .SetGradient(
                new Color(0.12f, 0.58f, 1f),
                new Color(0.82f, 0.22f, 0.94f))
            .SetGradientLinear(90f)
            .SetAnimation(NowTextAnimations.FadeUp(
                distance: 10f,
                duration: 0.45f,
                stagger: 0.06f))
            .SetTime(titleTime)
            .Draw("NowUI");

        var gradientRect = NowLayout.ReserveRect(width: 180f, height: 36f);
        Now.Gradient(
                gradientRect,
                new Color(0.12f, 0.5f, 1f),
                new Color(0.72f, 0.22f, 0.95f))
            .SetLinear(110f)
            .SetRadius(10f)
            .Draw();

        var maskRect = NowLayout.ReserveRect(width: 180f, height: 44f);
        var softMask = NowMaskShape.Capsule(maskRect).SetFeather(1f);
        using (Now.Mask(softMask))
        {
            Now.Gradient(
                    new NowRect(maskRect.x - 24f, maskRect.y, maskRect.width + 48f, maskRect.height),
                    new Color(0.1f, 0.72f, 0.62f),
                    new Color(0.08f, 0.28f, 0.55f))
                .SetLinear(90f)
                .Draw();

            Now.Text(new NowRect(maskRect.x + 14f, maskRect.y + 8f, maskRect.width - 28f, 28f))
                .SetFontSize(18f)
                .SetColor(Color.white)
                .Draw("Soft capsule mask");
        }

        var sdfMaskRect = NowLayout.ReserveRect(width: 180f, height: 44f);
        var sdfMask = NowSdf.Scene(sdfMaskRect, 4101)
            .SetMaskResolutionScale(0.5f)
            .SetFeather(1f)
            .Circle(new Vector2(30f, 22f), 21f)
            .SmoothUnion(10f)
            .RoundedBox(new NowRect(28f, 2f, 146f, 40f), 18f)
            .Subtract()
            .Circle(new Vector2(154f, 22f), 8f);

        using (sdfMask.BeginMask())
        {
            Now.Gradient(
                    sdfMaskRect,
                    new Color(0.96f, 0.42f, 0.18f),
                    new Color(0.68f, 0.16f, 0.76f))
                .SetLinear(90f)
                .Draw();

            Now.Text(new NowRect(sdfMaskRect.x + 14f, sdfMaskRect.y + 8f, sdfMaskRect.width - 28f, 28f))
                .SetFontSize(18f)
                .SetColor(Color.white)
                .Draw("SDF cutout mask");
        }

        var buttonRect = NowLayout.ReserveRect(width: 180f, height: 44f);
        bool clicked = Now.Button(buttonRect, "Sample Button").Draw();

        NowLayout.Label(clicked ? "Clicked" : "Ready", 16f).Draw();
    }
}
