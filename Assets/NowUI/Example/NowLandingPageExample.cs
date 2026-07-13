using NowUI;
using UnityEngine;

/// <summary>
/// A Google-inspired search landing page drawn with explicit NowRect placement.
/// Use this rendering style when the design already gives you concrete bounds.
/// Reusable colors and small drawing helpers live in NowLandingPageShared.cs.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Landing Page (Explicit Rects)")]
public sealed class NowLandingPageExample : NowGraphic
{
    [SerializeField] string _query = "";

    string _status = NowLandingPageShared.DefaultStatus;

    protected override void DrawNowUI(NowRect view)
    {
        Now.Rectangle(view).SetColor(NowLandingPageShared.Page).Draw();

        bool compact = view.width < 600f;
        bool shortView = view.height < 520f;
        bool minimalChrome = view.height < 360f;
        bool stackButtons = view.width < 360f;

        // Always slice from the previous remainder. Even a tiny view therefore
        // produces empty/clamped regions instead of a negative-height content rect.
        float headerHeight = minimalChrome ? 0f : shortView ? 52f : 64f;
        float footerHeight = minimalChrome ? 0f : compact ? 76f : 52f;
        NowRect header = view.TakeTop(headerHeight, out NowRect body);
        NowRect footer = body.TakeBottom(footerHeight, out NowRect content);

        if (!header.isEmpty)
            DrawHeader(header, compact);

        if (!footer.isEmpty)
            DrawFooter(footer, compact);

        DrawHero(content, compact, shortView, minimalChrome, stackButtons);
    }

    void DrawHero(
        NowRect content,
        bool compact,
        bool shortView,
        bool minimalChrome,
        bool stackButtons)
    {
        if (content.isEmpty)
            return;

        bool fieldOnly = minimalChrome && content.height < 120f;
        bool primaryActionOnly = minimalChrome && content.height < 200f;
        string status = compact && _status == NowLandingPageShared.DefaultStatus
            ? NowLandingPageShared.CompactDefaultStatus
            : _status;
        float sideMargin = Mathf.Min(compact ? 16f : 24f, content.width * 0.1f);
        float heroWidth = Mathf.Min(620f, Mathf.Max(0f, content.width - sideMargin * 2f));
        if (heroWidth <= 0f)
            return;

        bool showWordmark = !minimalChrome;
        float wordmarkHeight = showWordmark ? (shortView || compact ? 72f : 102f) : 0f;
        float wordmarkGap = showWordmark ? (shortView || compact ? 10f : 22f) : 0f;
        float searchHeight = compact || shortView ? 46f : 50f;

        if (fieldOnly)
        {
            float verticalPadding = Mathf.Min(8f, Mathf.Max(0f, content.height * 0.1f));
            searchHeight = Mathf.Min(searchHeight, Mathf.Max(0f, content.height - verticalPadding * 2f));
        }

        float actionGap = primaryActionOnly ? 8f : shortView ? 12f : 24f;
        float stackedButtonHeight = NowLandingPageShared.ButtonHeight * 2f + NowLandingPageShared.ButtonGap;
        float actionHeight = stackButtons ? stackedButtonHeight : NowLandingPageShared.ButtonHeight;
        float statusGap = shortView ? 10f : 24f;
        float statusHeight = Now.font.MeasureText(status, 13f, NowFontStyle.Regular).y;

        float heroHeight = wordmarkHeight + wordmarkGap + searchHeight;
        if (!fieldOnly)
        {
            heroHeight += actionGap + (primaryActionOnly
                ? NowLandingPageShared.ButtonHeight
                : actionHeight + statusGap + statusHeight);
        }

        heroHeight = Mathf.Min(heroHeight, content.height);
        NowRect hero = content.Centered(heroWidth, heroHeight);
        NowRect remaining = hero;

        if (showWordmark)
        {
            NowRect wordmark = remaining.TakeTop(wordmarkHeight, out remaining);
            NowLandingPageShared.DrawWordmark(wordmark, shortView || compact ? 58f : 82f);
            remaining.TakeTop(wordmarkGap, out remaining);
        }

        searchHeight = Mathf.Min(searchHeight, remaining.height);
        NowRect search = remaining.TakeTop(searchHeight, out remaining);
        DrawSearchField(search);

        if (fieldOnly)
            return;

        remaining.TakeTop(actionGap, out remaining);

        if (primaryActionOnly)
        {
            DrawPrimaryAction(remaining.TakeTop(Mathf.Min(NowLandingPageShared.ButtonHeight, remaining.height)));
            return;
        }

        float buttonAreaHeight = Mathf.Min(actionHeight, remaining.height);
        NowRect buttons = remaining.TakeTop(buttonAreaHeight, out remaining);
        DrawActions(buttons, stackButtons);

        if (!remaining.isEmpty)
        {
            remaining.TakeTop(statusGap, out remaining);
            NowRect statusRect = remaining.TakeTop(Mathf.Min(statusHeight, remaining.height));
            NowLandingPageShared.DrawCenteredText(
                status,
                statusRect,
                13f,
                NowLandingPageShared.MutedText);
        }
    }

    void DrawPrimaryAction(NowRect rect)
    {
        if (rect.isEmpty)
            return;

        float width = Mathf.Min(NowLandingPageShared.SearchButtonWidth, rect.width);
        if (Now.Button(rect.Centered(width, rect.height), "Now Search")
            .SetStyle(NowRectangleStyle.Muted)
            .Draw())
            Search();
    }

    void DrawSearchField(NowRect search)
    {
        var result = Now.TextField(search, "landing-search")
            .SetPlaceholder("Search NowUI or type a URL")
            .SetRadius(NowRadiusToken.Pill)
            .SetBackgroundColor(Color.white)
            .SetBorderColor(NowLandingPageShared.Border)
            .SetFocusColor(NowLandingPageShared.Focus)
            .SetTextColor(NowLandingPageShared.Text)
            .SetPlaceholderColor(NowLandingPageShared.MutedText)
            .SetPadding(48f, 12f, 20f, 12f)
            .SetOutlineWidth(1f, 2f)
            .SetElevation(NowElevationToken.Raised)
            .Draw(ref _query);

        // Use the control's resolved geometry so this decoration follows the
        // same result-based pattern as the NowLayout version.
        NowLandingPageShared.DrawSearchIcon(result.rect);
        if (result.submitted)
            Search();
    }

    void DrawActions(NowRect rect, bool stacked)
    {
        if (rect.isEmpty)
            return;

        if (stacked)
        {
            NowRect searchButton = rect.TakeTop(NowLandingPageShared.ButtonHeight, out NowRect remaining);
            remaining.TakeTop(NowLandingPageShared.ButtonGap, out remaining);
            NowRect luckyButton = remaining.TakeTop(NowLandingPageShared.ButtonHeight);

            if (Now.Button(searchButton, "Now Search").SetStyle(NowRectangleStyle.Muted).Draw())
                Search();
            if (Now.Button(luckyButton, "I'm Feeling Lucky").SetStyle(NowRectangleStyle.Muted).Draw())
                FeelLucky();
            return;
        }

        float gap = Mathf.Min(NowLandingPageShared.ButtonGap, rect.width * 0.04f);
        float available = Mathf.Max(0f, rect.width - gap);
        float searchWidth = Mathf.Min(NowLandingPageShared.SearchButtonWidth, available * 0.46f);
        float luckyWidth = Mathf.Min(NowLandingPageShared.LuckyButtonWidth, available - searchWidth);
        float buttonsWidth = searchWidth + gap + luckyWidth;
        float x = rect.center.x - buttonsWidth * 0.5f;

        if (Now.Button(new NowRect(x, rect.y, searchWidth, rect.height), "Now Search")
            .SetStyle(NowRectangleStyle.Muted)
            .Draw())
            Search();

        if (Now.Button(new NowRect(x + searchWidth + gap, rect.y, luckyWidth, rect.height), "I'm Feeling Lucky")
            .SetStyle(NowRectangleStyle.Muted)
            .Draw())
            FeelLucky();
    }

    void DrawHeader(NowRect rect, bool compact)
    {
        float right = rect.xMax - (compact ? 16f : 24f);
        Now.Circle(new Vector2(right - 16f, rect.center.y), 16f)
            .SetColor(NowLandingPageShared.Focus)
            .Draw();
        NowLandingPageShared.DrawCenteredText(
            "N",
            new NowRect(right - 32f, rect.center.y - 16f, 32f, 32f),
            14f,
            Color.white,
            NowFontStyle.Bold);

        // Ghost buttons look like links but retain hover, focus and click affordances.
        if (rect.width >= 250f)
        {
            float gap = compact ? 8f : 12f;
            right -= 32f + gap;
            if (NowLandingPageShared.DrawLink(
                new NowRect(right - 64f, rect.y + 8f, 64f, rect.height - 16f),
                "Images",
                "landing-images",
                NowTextStyle.Body))
                SelectLink("Images");
            right -= 64f + gap;
            if (NowLandingPageShared.DrawLink(
                new NowRect(right - 52f, rect.y + 8f, 52f, rect.height - 16f),
                "Docs",
                "landing-docs",
                NowTextStyle.Body))
                SelectLink("Docs");
        }
    }

    void DrawFooter(NowRect rect, bool compact)
    {
        Now.Rectangle(rect).SetColor(NowLandingPageShared.Footer).Draw();
        Now.Line(rect.x, rect.y, rect.xMax, rect.y)
            .SetColor(NowLandingPageShared.Border)
            .SetWidth(1f)
            .Draw();

        if (compact)
        {
            NowRect remaining = rect.Inset(8f, 5f);
            NowRect firstRow = remaining.TakeTop(30f, out remaining);
            remaining.TakeTop(2f, out remaining);
            NowRect secondRow = remaining.TakeTop(30f);
            DrawFooterPair(firstRow, "About", "How it works");
            DrawFooterPair(secondRow, "Privacy", "Settings");
            return;
        }

        if (NowLandingPageShared.DrawLink(new NowRect(rect.x + 22f, rect.y + 6f, 64f, rect.height - 12f), "About", "landing-about"))
            SelectLink("About");
        if (NowLandingPageShared.DrawLink(new NowRect(rect.x + 90f, rect.y + 6f, 108f, rect.height - 12f), "How it works", "landing-how"))
            SelectLink("How it works");
        if (NowLandingPageShared.DrawLink(new NowRect(rect.xMax - 178f, rect.y + 6f, 74f, rect.height - 12f), "Privacy", "landing-privacy"))
            SelectLink("Privacy");
        if (NowLandingPageShared.DrawLink(new NowRect(rect.xMax - 104f, rect.y + 6f, 82f, rect.height - 12f), "Settings", "landing-settings"))
            SelectLink("Settings");
    }

    void DrawFooterPair(NowRect row, string leftLabel, string rightLabel)
    {
        float half = Mathf.Max(0f, (row.width - 4f) * 0.5f);
        NowRect left = row.TakeLeft(half, out NowRect remaining);
        remaining.TakeLeft(Mathf.Min(4f, remaining.width), out remaining);
        NowRect right = remaining.TakeLeft(half);

        if (NowLandingPageShared.DrawLink(left, leftLabel, $"landing-{leftLabel}"))
            SelectLink(leftLabel);
        if (NowLandingPageShared.DrawLink(right, rightLabel, $"landing-{rightLabel}"))
            SelectLink(rightLabel);
    }

    void Search()
    {
        _status = string.IsNullOrWhiteSpace(_query)
            ? "Type something to search for."
            : $"Search requested for “{ShortQuery(_query)}”.";
        MarkDirty();
    }

    void FeelLucky()
    {
        _query = "NowUI examples";
        _status = "Lucky pick: NowUI examples.";
        MarkDirty();
    }

    void SelectLink(string label)
    {
        _status = $"{label} selected.";
        MarkDirty();
    }

    static string ShortQuery(string value)
    {
        const int maxLength = 28;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
    }
}
