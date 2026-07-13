using NowUI;
using UnityEngine;

/// <summary>
/// The same search page expressed as layout intent. NowLayoutGraphic owns the
/// exact measure/draw cycle, so ordinary Row/Column scopes are all this needs.
/// Control events are passive during measurement: side effects guarded by
/// clicked/submitted results run only once, during the real draw pass.
/// Reusable colors and small drawing helpers live in NowLandingPageShared.cs.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Landing Page (NowLayout)")]
public sealed class NowLayoutLandingPageExample : NowLayoutGraphic
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

        using (NowLayout.Column(view).Begin())
        {
            if (!minimalChrome)
                DrawHeader(compact, shortView, view.width);

            DrawHero(compact, shortView, minimalChrome, stackButtons, view.width, view.height);

            if (!minimalChrome)
                DrawFooter(compact);
        }
    }

    void DrawHeader(bool compact, bool shortView, float viewWidth)
    {
        using (NowLayout.Row()
            .FillWidth()
            .Height(shortView ? 52f : 64f)
            .Padding(compact ? 16f : 24f, 6f)
            .Gap(compact ? 8f : 12f)
            .AlignChildren(NowLayoutAlign.Center)
            .Begin())
        {
            NowLayout.Spacer();

            // Ghost buttons communicate that these labels are interactive links.
            if (viewWidth >= 250f)
            {
                if (NowLayout.Button("Docs")
                    .SetId("landing-docs")
                    .SetWidth(52f)
                    .SetHeight(40f)
                    .SetStyle(NowRectangleStyle.Ghost)
                    .SetTextStyle(NowTextStyle.Body)
                    .Draw())
                    SelectLink("Docs");

                if (NowLayout.Button("Images")
                    .SetId("landing-images")
                    .SetWidth(64f)
                    .SetHeight(40f)
                    .SetStyle(NowRectangleStyle.Ghost)
                    .SetTextStyle(NowTextStyle.Body)
                    .Draw())
                    SelectLink("Images");
            }

            NowRect avatar = NowLayout.ReserveRect(32f, 32f, align: NowLayoutAlign.Center);
            Now.Circle(avatar.center, 16f).SetColor(NowLandingPageShared.Focus).Draw();
            NowLandingPageShared.DrawCenteredText("N", avatar, 14f, Color.white, NowFontStyle.Bold);
        }
    }

    void DrawHero(
        bool compact,
        bool shortView,
        bool minimalChrome,
        bool stackButtons,
        float viewWidth,
        float viewHeight)
    {
        bool fieldOnly = minimalChrome && viewHeight < 120f;
        bool primaryActionOnly = minimalChrome && viewHeight < 200f;
        float horizontalPadding = Mathf.Min(compact ? 16f : 24f, Mathf.Max(0f, viewWidth * 0.1f));
        float verticalPadding = minimalChrome
            ? Mathf.Min(8f, Mathf.Max(0f, viewHeight * 0.1f))
            : shortView ? 12f : 24f;

        // Symmetric padding plus Center makes the vertical intent explicit; there
        // is no compensating bottom-padding offset to reverse-engineer.
        using (NowLayout.Column()
            .Grow()
            .Padding(horizontalPadding, verticalPadding)
            .AlignChildren(NowLayoutAlign.Center)
            .Justify(NowLayoutJustify.Center)
            .Begin())
        {
            if (!minimalChrome)
            {
                float fontSize = compact || shortView ? 58f : 82f;
                NowLandingPageShared.DrawLayoutWordmark(fontSize, compact || shortView ? 72f : 102f);
                NowLayout.Space(compact || shortView ? 10f : 22f);
            }

            float searchHeight = compact || shortView ? 46f : 50f;
            if (fieldOnly)
                searchHeight = Mathf.Min(searchHeight, Mathf.Max(0f, viewHeight - verticalPadding * 2f));

            var search = NowLayout.TextField("landing-search")
                .SetStretchWidth()
                .SetMaxWidth(584f)
                .SetHeight(searchHeight)
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

            // Layout controls return their resolved geometry, so decorations do
            // not require a separate ReserveRect call or a duplicate layout slot.
            NowLandingPageShared.DrawSearchIcon(search.rect);
            if (search.submitted)
                Search();

            if (fieldOnly)
                return;

            if (primaryActionOnly)
            {
                NowLayout.Space(8f);
                using (NowLayout.Column().FillWidth(NowLandingPageShared.SearchButtonWidth).Begin())
                {
                    if (NowLayout.Button("Now Search")
                        .SetStretchWidth()
                        .SetHeight(NowLandingPageShared.ButtonHeight)
                        .SetStyle(NowRectangleStyle.Muted)
                        .Draw())
                        Search();
                }
                return;
            }

            NowLayout.Space(shortView ? 12f : 24f);
            DrawActions(stackButtons);
            NowLayout.Space(shortView ? 10f : 24f);

            string status = compact && _status == NowLandingPageShared.DefaultStatus
                ? NowLandingPageShared.CompactDefaultStatus
                : _status;
            NowLayout.Label(status)
                .SetMaxWidth(584f)
                .SetFontSize(13f)
                .SetColor(NowLandingPageShared.MutedText)
                .Draw();
        }
    }

    void DrawActions(bool stacked)
    {
        if (stacked)
        {
            using (NowLayout.Column()
                .FillWidth(280f)
                .Gap(NowLandingPageShared.ButtonGap)
                .Begin())
            {
                if (NowLayout.Button("Now Search")
                    .SetStretchWidth()
                    .SetHeight(NowLandingPageShared.ButtonHeight)
                    .SetStyle(NowRectangleStyle.Muted)
                    .Draw())
                    Search();

                if (NowLayout.Button("I'm Feeling Lucky")
                    .SetStretchWidth()
                    .SetHeight(NowLandingPageShared.ButtonHeight)
                    .SetStyle(NowRectangleStyle.Muted)
                    .Draw())
                    FeelLucky();
            }
            return;
        }

        using (NowLayout.Row()
            .Width(NowLandingPageShared.SearchButtonWidth + NowLandingPageShared.ButtonGap + NowLandingPageShared.LuckyButtonWidth)
            .Gap(NowLandingPageShared.ButtonGap)
            .Begin())
        {
            // NowLayoutGraphic measures first, but button results are false during
            // that passive pass. These state changes therefore execute once.
            if (NowLayout.Button("Now Search")
                .SetWidth(NowLandingPageShared.SearchButtonWidth)
                .SetHeight(NowLandingPageShared.ButtonHeight)
                .SetStyle(NowRectangleStyle.Muted)
                .Draw())
                Search();

            if (NowLayout.Button("I'm Feeling Lucky")
                .SetWidth(NowLandingPageShared.LuckyButtonWidth)
                .SetHeight(NowLandingPageShared.ButtonHeight)
                .SetStyle(NowRectangleStyle.Muted)
                .Draw())
                FeelLucky();
        }
    }

    void DrawFooter(bool compact)
    {
        if (compact)
        {
            using (var footer = NowLayout.Column()
                .FillWidth()
                .Height(76f)
                .Padding(8f, 5f)
                .Gap(2f)
                .Begin())
            {
                DrawFooterBackground(footer.rect);
                DrawFooterRow("About", "How it works");
                DrawFooterRow("Privacy", "Settings");
            }
            return;
        }

        using (var footer = NowLayout.Row()
            .FillWidth()
            .Height(52f)
            .Padding(22f, 6f)
            .AlignChildren(NowLayoutAlign.Center)
            .Justify(NowLayoutJustify.SpaceBetween)
            .Begin())
        {
            DrawFooterBackground(footer.rect);

            using (NowLayout.Row().Gap(4f).Begin())
            {
                DrawFooterLink("About", "landing-about", 64f);
                DrawFooterLink("How it works", "landing-how", 108f);
            }

            using (NowLayout.Row().Gap(0f).Begin())
            {
                DrawFooterLink("Privacy", "landing-privacy", 74f);
                DrawFooterLink("Settings", "landing-settings", 82f);
            }
        }
    }

    void DrawFooterRow(string left, string right)
    {
        using (NowLayout.Row()
            .FillWidth()
            .Height(30f)
            .Gap(4f)
            .Begin())
        {
            if (NowLayout.Button(left)
                .SetId($"landing-{left}")
                .SetStretchWidth()
                .SetHeight(30f)
                .SetStyle(NowRectangleStyle.Ghost)
                .SetTextStyle(NowTextStyle.Muted)
                .Draw())
                SelectLink(left);

            if (NowLayout.Button(right)
                .SetId($"landing-{right}")
                .SetStretchWidth()
                .SetHeight(30f)
                .SetStyle(NowRectangleStyle.Ghost)
                .SetTextStyle(NowTextStyle.Muted)
                .Draw())
                SelectLink(right);
        }
    }

    void DrawFooterLink(string label, NowId id, float width)
    {
        if (NowLayout.Button(label)
            .SetId(id)
            .SetWidth(width)
            .SetHeight(40f)
            .SetStyle(NowRectangleStyle.Ghost)
            .SetTextStyle(NowTextStyle.Muted)
            .Draw())
            SelectLink(label);
    }

    static void DrawFooterBackground(NowRect rect)
    {
        Now.Rectangle(rect).SetColor(NowLandingPageShared.Footer).Draw();
        Now.Line(rect.x, rect.y, rect.xMax, rect.y)
            .SetColor(NowLandingPageShared.Border)
            .SetWidth(1f)
            .Draw();
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
