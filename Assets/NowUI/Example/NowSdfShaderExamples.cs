using System.Runtime.CompilerServices;
using NowUI;
using NowUI.Sdf;
using UnityEngine;

/// <summary>
/// Live gallery for the packaged SDF final-shade examples. Every card renders
/// the same five-shape graph so the material is the only visual variable.
/// </summary>
public static class NowSdfShaderExamples
{
    public const float PreferredHeight = 334f;

    const float PanelPadding = 14f;
    const float CardGap = 14f;
    const float CardHeaderHeight = 58f;
    const float PreviewInset = 10f;

    const string AuroraResource = "NowUI/SdfExamples/Aurora";
    const string TopographicResource = "NowUI/SdfExamples/Topographic";
    const string PaperCutoutResource = "NowUI/SdfExamples/PaperCutout";

    const string AuroraTitle = "AURORA";
    const string AuroraSubtitle = "Animated bands + halo";
    const string TopographicTitle = "TOPOGRAPHIC";
    const string TopographicSubtitle = "Inside / outside contours";
    const string PaperTitle = "PAPER CUTOUT";
    const string PaperSubtitle = "Bevel + sampled shadow";
    const string MissingMaterial = "Material asset unavailable";

    static readonly NowSdfGraph SharedGraph = NowSdf.Graph();

    static Material _auroraMaterial;
    static Material _topographicMaterial;
    static Material _paperCutoutMaterial;
    static float _graphWidth = -1f;
    static float _graphHeight = -1f;

    /// <summary>The packaged animated aurora material, loaded on first use.</summary>
    public static Material AuroraMaterial =>
        _auroraMaterial != null
            ? _auroraMaterial
            : _auroraMaterial = Resources.Load<Material>(AuroraResource);

    /// <summary>The packaged signed-distance contour material, loaded on first use.</summary>
    public static Material TopographicMaterial =>
        _topographicMaterial != null
            ? _topographicMaterial
            : _topographicMaterial = Resources.Load<Material>(TopographicResource);

    /// <summary>The packaged bevel and custom-shadow material, loaded on first use.</summary>
    public static Material PaperCutoutMaterial =>
        _paperCutoutMaterial != null
            ? _paperCutoutMaterial
            : _paperCutoutMaterial = Resources.Load<Material>(PaperCutoutResource);

    /// <summary>
    /// Draws three cards using a shared five-shape graph. Pass a stable id when
    /// the gallery is repeated or reordered; child scene ids remain local to it.
    /// The helper performs no managed allocation per frame after resources and
    /// the graph/material caches have warmed.
    /// </summary>
    public static void DrawGallery(
        NowRect rect,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        DrawGallery(rect, NowControls.themeAsset, default, file, line);
    }

    /// <summary>Draws the gallery with an explicit theme and optional stable id.</summary>
    public static void DrawGallery(
        NowRect rect,
        NowThemeAsset themeAsset,
        NowId id = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (rect.isEmpty)
            return;

        themeAsset = themeAsset != null ? themeAsset : NowControls.themeAsset;

        using (NowControls.ControlScope(id, file, line))
        {
            Color border = themeAsset.GetColor(NowColorToken.Border, Color.gray);
            Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
            Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);

            themeAsset.Rectangle(rect, NowRectangleStyle.Muted)
                .SetRadius(18f)
                .SetOutline(1f, WithAlpha(border, 0.42f))
                .Draw();

            var area = rect.Inset(PanelPadding);
            float cardWidth = Mathf.Max(1f, (area.width - CardGap * 2f) / 3f);
            var auroraCard = new NowRect(area.x, area.y, cardWidth, area.height);
            var topographicCard = new NowRect(auroraCard.xMax + CardGap, area.y, cardWidth, area.height);
            var paperCard = new NowRect(topographicCard.xMax + CardGap, area.y, cardWidth, area.height);

            var preview = PreviewRect(auroraCard);
            EnsureSharedGraph(preview.width, preview.height);

            DrawCard(
                themeAsset,
                auroraCard,
                AuroraTitle,
                AuroraSubtitle,
                new Color(0.14f, 0.82f, 1f, 1f),
                AuroraMaterial,
                "aurora",
                text,
                muted,
                border);
            DrawCard(
                themeAsset,
                topographicCard,
                TopographicTitle,
                TopographicSubtitle,
                new Color(0.3f, 1f, 0.72f, 1f),
                TopographicMaterial,
                "topographic",
                text,
                muted,
                border);
            DrawCard(
                themeAsset,
                paperCard,
                PaperTitle,
                PaperSubtitle,
                new Color(1f, 0.62f, 0.2f, 1f),
                PaperCutoutMaterial,
                "paper-cutout",
                text,
                muted,
                border);
        }

        // Aurora reads shader _Time, so retained hosts need another frame.
        NowControlState.RequestRepaint();
    }

    static void DrawCard(
        NowThemeAsset themeAsset,
        NowRect card,
        string title,
        string subtitle,
        Color accent,
        Material material,
        NowId sceneId,
        Color text,
        Color muted,
        Color border)
    {
        themeAsset.Rectangle(card, NowRectangleStyle.Surface)
            .SetRadius(15f)
            .SetOutline(1f, WithAlpha(border, 0.58f))
            .Draw();

        Now.Rectangle(new NowRect(card.x + 14f, card.y + 13f, 4f, 30f))
            .SetRadius(2f)
            .SetColor(accent)
            .Draw();
        themeAsset.Text(new NowRect(card.x + 27f, card.y + 10f, card.width - 39f, 18f), NowTextStyle.Label)
            .SetFontSize(11f)
            .SetBold()
            .SetColor(text)
            .SetMask(card.Inset(8f))
            .Draw(title);
        themeAsset.Text(new NowRect(card.x + 27f, card.y + 29f, card.width - 39f, 17f), NowTextStyle.Caption)
            .SetFontSize(10f)
            .SetColor(muted)
            .SetMask(card.Inset(8f))
            .Draw(subtitle);

        var preview = PreviewRect(card);
        Now.Rectangle(preview)
            .SetRadius(11f)
            .SetColor(new Color(0.018f, 0.027f, 0.055f, 0.94f))
            .SetOutline(1f, new Color(accent.r, accent.g, accent.b, 0.20f))
            .Draw();

        if (material == null)
        {
            themeAsset.Text(preview.Inset(12f), NowTextStyle.Caption)
                .SetFontSize(10f)
                .SetColor(muted)
                .Draw(MissingMaterial);
            return;
        }

        NowSdf.Scene(preview, sceneId)
            .SetMaterial(material)
            .SetFeather(0.65f)
            .Graph(SharedGraph)
            .Draw();
    }

    static NowRect PreviewRect(NowRect card)
    {
        return card.Inset(PreviewInset, CardHeaderHeight, PreviewInset, PreviewInset);
    }

    static void EnsureSharedGraph(float width, float height)
    {
        if (Mathf.Approximately(width, _graphWidth) && Mathf.Approximately(height, _graphHeight))
            return;

        _graphWidth = width;
        _graphHeight = height;

        // The largest example field reaches 36 px outside the edge. Keeping a
        // 42 px scene margin prevents its halo/contours/shadow from clipping.
        float margin = Mathf.Min(42f, Mathf.Min(width, height) * 0.24f);
        float bodyWidth = Mathf.Max(8f, width - margin * 2f);
        float bodyHeight = Mathf.Max(8f, height - margin * 2f);
        float radius = Mathf.Min(22f, bodyHeight * 0.24f);
        float small = Mathf.Min(width, height);

        SharedGraph.Clear()
            .SetColor(new Color(0.9f, 0.96f, 1f, 1f))
            .UseColor()
            .RoundedBox(new NowRect(margin, margin, bodyWidth, bodyHeight), radius)
            .SetColor(new Color(0.28f, 0.76f, 1f, 1f))
            .UseColor()
            .SmoothUnion(12f)
            .Circle(new Vector2(width * 0.69f, height * 0.36f), small * 0.16f)
            .SmoothSubtract(8f)
            .Circle(new Vector2(width * 0.38f, height * 0.49f), small * 0.095f)
            .SmoothSubtract(5f)
            .Capsule(new Vector2(width * 0.46f, height * 0.68f), new Vector2(width * 0.68f, height * 0.68f), small * 0.045f)
            .SetColor(new Color(1f, 0.52f, 0.32f, 1f))
            .UseColor()
            .SmoothUnion(7f)
            .Circle(new Vector2(width * 0.31f, height * 0.70f), small * 0.07f);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }
}
