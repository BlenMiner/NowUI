using NowUI;
using UnityEngine;

/// <summary>
/// Presentation shared by the explicit-rect and NowLayout landing-page examples.
/// The page structure and responsive decisions intentionally remain in each example.
/// </summary>
internal static class NowLandingPageShared
{
    internal static readonly Color Page = new Color32(255, 255, 255, 255);
    internal static readonly Color Text = new Color32(32, 33, 36, 255);
    internal static readonly Color MutedText = new Color32(95, 99, 104, 255);
    internal static readonly Color Footer = new Color32(242, 242, 242, 255);
    internal static readonly Color Border = new Color32(218, 220, 224, 255);
    internal static readonly Color Focus = new Color32(66, 133, 244, 255);

    internal const string DefaultStatus = "Built entirely from text, colors, controls, and rectangles.";
    internal const string CompactDefaultStatus = "Ready to search.";
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

    internal static void DrawWordmark(NowRect rect, float fontSize)
    {
        const NowFontStyle style = NowFontStyle.Bold;

        float width = 0f;
        for (int i = 0; i < WordmarkLetters.Length; ++i)
            width += Now.font.MeasureText(WordmarkLetters[i], fontSize, style).x;

        float x = rect.center.x - width * 0.5f;
        for (int i = 0; i < WordmarkLetters.Length; ++i)
        {
            Vector2 size = Now.font.MeasureText(WordmarkLetters[i], fontSize, style);
            Vector4 bounds = Now.font.MeasureTextBounds(WordmarkLetters[i], fontSize, style);
            var glyphRect = new NowRect(x, rect.center.y - size.y * 0.5f, size.x + 2f, size.y);
            NowRect glyphMask = bounds.z > 0f && bounds.w > 0f
                ? glyphRect.Union(new NowRect(
                    glyphRect.x + bounds.x,
                    glyphRect.y + bounds.y,
                    bounds.z,
                    bounds.w)).Outset(4f)
                : glyphRect.Outset(4f);
            Now.Text(glyphRect)
                // The glyph's visual bounds can extend beyond its advance box.
                // Include them in the mask while keeping every letter on one baseline.
                .SetMask(glyphMask.Intersect(rect.Outset(4f)))
                .SetFontSize(fontSize)
                .SetFontStyle(style)
                .SetColor(WordmarkColors[i])
                .Draw(WordmarkLetters[i]);
            x += size.x;
        }
    }

    internal static void DrawLayoutWordmark(float fontSize, float height)
    {
        using (NowLayout.Row()
            .Height(height)
            .AlignChildren(NowLayoutAlign.Center)
            .Begin())
        {
            for (int i = 0; i < WordmarkLetters.Length; ++i)
            {
                NowLayout.Label(WordmarkLetters[i])
                    .SetFontSize(fontSize)
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

    internal static bool DrawLink(
        NowRect rect,
        string label,
        NowId id,
        NowTextStyle textStyle = NowTextStyle.Muted)
    {
        return Now.Button(rect, label)
            .SetId(id)
            .SetStyle(NowRectangleStyle.Ghost)
            .SetTextStyle(textStyle)
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
        Vector4 bounds = Now.font.MeasureTextBounds(value, fontSize, style);
        NowRect textRect = rect.Centered(size);
        NowRect mask = bounds.z > 0f && bounds.w > 0f
            ? textRect.Union(new NowRect(
                textRect.x + bounds.x,
                textRect.y + bounds.y,
                bounds.z,
                bounds.w)).Outset(4f)
            : textRect.Outset(4f);

        Now.Text(textRect)
            .SetMask(mask)
            .SetFontSize(fontSize)
            .SetFontStyle(style)
            .SetColor(color)
            .Draw(value);
    }
}
