using NowUI;
using UnityEngine;

/// <summary>
/// A Google-inspired search landing page drawn with explicit NowRect placement.
/// Use this rendering style when the design already gives you concrete bounds.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Landing Page (Explicit Rects)")]
public sealed class NowLandingPageExample : NowGraphic
{
    [SerializeField] string _query = "";

    protected override void DrawNowUI(NowRect view)
    {
        NowLandingPageStyle.DrawExplicit(view, ref _query);
    }
}

internal static class NowLandingPageStyle
{
    internal static readonly Color Page = new Color32(255, 255, 255, 255);
    internal static readonly Color Text = new Color32(32, 33, 36, 255);
    internal static readonly Color MutedText = new Color32(95, 99, 104, 255);
    internal static readonly Color Footer = new Color32(242, 242, 242, 255);
    internal static readonly Color Border = new Color32(218, 220, 224, 255);
    internal static readonly Color Focus = new Color32(66, 133, 244, 255);
    internal const float SearchButtonWidth = 132f;
    internal const float LuckyButtonWidth = 156f;
    internal const float ButtonHeight = 38f;
    internal const float ButtonGap = 12f;

    static readonly Color[] WordmarkColors =
    {
        new Color32(66, 133, 244, 255),
        new Color32(234, 67, 53, 255),
        new Color32(251, 188, 5, 255),
        new Color32(66, 133, 244, 255),
        new Color32(52, 168, 83, 255),
        new Color32(234, 67, 53, 255)
    };

    static readonly string[] WordmarkLetters = { "N", "o", "w", "g", "l", "e" };

    internal static void DrawExplicit(NowRect view, ref string query)
    {
        Now.Rectangle(view).SetColor(Page).Draw();

        NowRect header = view.TakeTop(64f);
        NowRect footer = view.TakeBottom(52f);
        NowRect content = new NowRect(view.x, header.yMax, view.width, footer.y - header.yMax);

        DrawHeader(header);
        DrawFooter(footer);

        NowRect hero = content.Centered(620f, 350f).Offset(0f, -16f);
        DrawWordmark(hero.TakeTop(102f));

        NowRect search = new NowRect(hero.x + 18f, hero.y + 124f, hero.width - 36f, 50f);
        Now.TextField(search, "landing-search")
            .SetPlaceholder("Search NowUI or type a URL")
            .SetRadius(NowRadiusToken.Pill)
            .SetBackgroundColor(Color.white)
            .SetBorderColor(Border)
            .SetFocusColor(Focus)
            .SetTextColor(Text)
            .SetPlaceholderColor(MutedText)
            .SetPadding(48f, 12f, 20f, 12f)
            .SetOutlineWidth(1f, 2f)
            .SetElevation(NowElevationToken.Raised)
            .Draw(ref query);

        DrawSearchIcon(search);

        float buttonsWidth = SearchButtonWidth + LuckyButtonWidth + ButtonGap;
        float buttonsX = hero.center.x - buttonsWidth * 0.5f;
        float buttonsY = search.yMax + 28f;

        Now.Button(new NowRect(buttonsX, buttonsY, SearchButtonWidth, ButtonHeight), "Now Search")
            .SetStyle(NowRectangleStyle.Muted)
            .Draw();
        Now.Button(new NowRect(
                buttonsX + SearchButtonWidth + ButtonGap,
                buttonsY,
                LuckyButtonWidth,
                ButtonHeight), "I'm Feeling Lucky")
            .SetStyle(NowRectangleStyle.Muted)
            .Draw();

        DrawCenteredText(
            "Built entirely from text, colors, controls, and rectangles.",
            new NowRect(hero.x, buttonsY + 68f, hero.width, 24f),
            13f,
            MutedText);
    }

    internal static void DrawHeader(NowRect rect)
    {
        float right = rect.xMax - 24f;
        Now.Circle(new Vector2(right - 16f, rect.center.y), 16f)
            .SetColor(Focus)
            .Draw();
        DrawCenteredText("N", new NowRect(right - 32f, rect.center.y - 16f, 32f, 32f), 14f, Color.white, NowFontStyle.Bold);

        right -= 56f;
        DrawCenteredText("Images", new NowRect(right - 62f, rect.y, 62f, rect.height), 13f, Text);
        right -= 76f;
        DrawCenteredText("Docs", new NowRect(right - 46f, rect.y, 46f, rect.height), 13f, Text);
    }

    internal static void DrawFooter(NowRect rect)
    {
        Now.Rectangle(rect).SetColor(Footer).Draw();
        Now.Line(rect.x, rect.y, rect.xMax, rect.y).SetColor(Border).SetWidth(1f).Draw();

        DrawCenteredText("About", new NowRect(rect.x + 30f, rect.y, 56f, rect.height), 13f, MutedText);
        DrawCenteredText("How it works", new NowRect(rect.x + 98f, rect.y, 92f, rect.height), 13f, MutedText);
        DrawCenteredText("Privacy", new NowRect(rect.xMax - 166f, rect.y, 58f, rect.height), 13f, MutedText);
        DrawCenteredText("Settings", new NowRect(rect.xMax - 96f, rect.y, 66f, rect.height), 13f, MutedText);
    }

    internal static void DrawWordmark(NowRect rect)
    {
        const float fontSize = 82f;
        const NowFontStyle style = NowFontStyle.Bold;

        float width = 0f;
        for (int i = 0; i < WordmarkLetters.Length; ++i)
            width += Now.font.MeasureText(WordmarkLetters[i], fontSize, style).x;

        float x = rect.center.x - width * 0.5f;

        for (int i = 0; i < WordmarkLetters.Length; ++i)
        {
            Vector2 size = Now.font.MeasureText(WordmarkLetters[i], fontSize, style);
            var glyphRect = new NowRect(x, rect.center.y - size.y * 0.5f, size.x + 2f, size.y);
            Now.Text(glyphRect)
                .SetFontSize(fontSize)
                .SetFontStyle(style)
                .SetColor(WordmarkColors[i])
                .Draw(WordmarkLetters[i]);
            x += size.x;
        }
    }

    internal static void DrawLayoutWordmark()
    {
        using (NowLayout.Row()
            .Height(102f)
            .AlignChildren(NowLayoutAlign.Center)
            .Begin())
        {
            for (int i = 0; i < WordmarkLetters.Length; ++i)
            {
                NowLayout.Label(WordmarkLetters[i])
                    .SetFontSize(82f)
                    .SetFontStyle(NowFontStyle.Bold)
                    .SetColor(WordmarkColors[i])
                    .Draw();
            }
        }
    }

    internal static void DrawSearchIcon(NowRect search)
    {
        var center = new Vector2(search.x + 25f, search.center.y - 1f);
        Now.Circle(center, 7f)
            .SetColor(Color.clear)
            .SetOutline(1.8f, MutedText)
            .Draw();
        Now.Line(center + new Vector2(5f, 5f), center + new Vector2(10f, 10f))
            .SetColor(MutedText)
            .SetWidth(1.8f)
            .Draw();
    }

    internal static void DrawCenteredText(
        string value,
        NowRect rect,
        float fontSize,
        Color color,
        NowFontStyle style = NowFontStyle.Regular)
    {
        Vector2 size = Now.font.MeasureText(value, fontSize, style);
        Now.Text(rect.Centered(size))
            .SetFontSize(fontSize)
            .SetFontStyle(style)
            .SetColor(color)
            .Draw(value);
    }
}
