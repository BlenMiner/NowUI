using NowUI;
using UnityEngine;

/// <summary>
/// The same search page expressed as layout intent. NowLayoutGraphic owns the
/// exact measure/draw cycle, so ordinary Row/Column scopes are all this needs.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Landing Page (NowLayout)")]
public sealed class NowLayoutLandingPageExample : NowLayoutGraphic
{
    [SerializeField] string _query = "";

    protected override void DrawNowUI(NowRect view)
    {
        Now.Rectangle(view).SetColor(NowLandingPageStyle.Page).Draw();

        using (NowLayout.Column(view).Begin())
        {
            using (NowLayout.Row()
                .FillWidth()
                .Height(64f)
                .Padding(24f, 14f)
                .Gap(14f)
                .AlignChildren(NowLayoutAlign.Center)
                .Begin())
            {
                NowLayout.Spacer();
                NowLayout.Label("Docs").SetFontSize(13f).SetColor(NowLandingPageStyle.Text).Draw();
                NowLayout.Label("Images").SetFontSize(13f).SetColor(NowLandingPageStyle.Text).Draw();
                NowRect avatar = NowLayout.ReserveRect(32f, 32f, align: NowLayoutAlign.Center);
                Now.Circle(avatar.center, 16f).SetColor(NowLandingPageStyle.Focus).Draw();
                NowLandingPageStyle.DrawCenteredText("N", avatar, 14f, Color.white, NowFontStyle.Bold);
            }

            using (NowLayout.Column()
                .Grow()
                .Padding(0f, 0f, 0f, 95f)
                .AlignChildren(NowLayoutAlign.Center)
                .Justify(NowLayoutJustify.Center)
                .Begin())
            {
                NowLandingPageStyle.DrawLayoutWordmark();
                NowLayout.Space(22f);

                NowRect search = NowLayout.ReserveRect(default(NowLayoutOptions)
                    .SetStretchWidth()
                    .SetMaxWidth(584f)
                    .SetHeight(50f));
                Now.TextField(search, "landing-search")
                    .SetPlaceholder("Search NowUI or type a URL")
                    .SetRadius(NowRadiusToken.Pill)
                    .SetBackgroundColor(Color.white)
                    .SetBorderColor(NowLandingPageStyle.Border)
                    .SetFocusColor(NowLandingPageStyle.Focus)
                    .SetTextColor(NowLandingPageStyle.Text)
                    .SetPlaceholderColor(NowLandingPageStyle.MutedText)
                    .SetPadding(48f, 12f, 20f, 12f)
                    .SetOutlineWidth(1f, 2f)
                    .SetElevation(NowElevationToken.Raised)
                    .Draw(ref _query);
                NowLandingPageStyle.DrawSearchIcon(search);
                NowLayout.Space(28f);

                using (NowLayout.Row()
                    .FillWidth()
                    .Gap(NowLandingPageStyle.ButtonGap)
                    .Justify(NowLayoutJustify.Center)
                    .Begin())
                {
                    NowLayout.Button("Now Search")
                        .SetWidth(NowLandingPageStyle.SearchButtonWidth)
                        .SetHeight(NowLandingPageStyle.ButtonHeight)
                        .SetStyle(NowRectangleStyle.Muted)
                        .Draw();
                    NowLayout.Button("I'm Feeling Lucky")
                        .SetWidth(NowLandingPageStyle.LuckyButtonWidth)
                        .SetHeight(NowLandingPageStyle.ButtonHeight)
                        .SetStyle(NowRectangleStyle.Muted)
                        .Draw();
                }

                NowLayout.Space(30f);
                NowLayout.Label("Built entirely from text, colors, controls, and rectangles.")
                    .SetFontSize(13f)
                    .SetColor(NowLandingPageStyle.MutedText)
                    .Draw();
            }

            using (var footer = NowLayout.Row()
                .FillWidth()
                .Height(52f)
                .Padding(30f, 14f)
                .AlignChildren(NowLayoutAlign.Center)
                .Justify(NowLayoutJustify.SpaceBetween)
                .Begin())
            {
                Now.Rectangle(footer.rect).SetColor(NowLandingPageStyle.Footer).Draw();
                Now.Line(footer.rect.x, footer.rect.y, footer.rect.xMax, footer.rect.y)
                    .SetColor(NowLandingPageStyle.Border)
                    .SetWidth(1f)
                    .Draw();

                using (NowLayout.Row().Gap(20f).Begin())
                {
                    NowLayout.Label("About").SetFontSize(13f).SetColor(NowLandingPageStyle.MutedText).Draw();
                    NowLayout.Label("How it works").SetFontSize(13f).SetColor(NowLandingPageStyle.MutedText).Draw();
                }

                using (NowLayout.Row().Gap(20f).Begin())
                {
                    NowLayout.Label("Privacy").SetFontSize(13f).SetColor(NowLandingPageStyle.MutedText).Draw();
                    NowLayout.Label("Settings").SetFontSize(13f).SetColor(NowLandingPageStyle.MutedText).Draw();
                }
            }
        }
    }
}
