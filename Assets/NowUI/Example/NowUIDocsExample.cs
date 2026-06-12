using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using NowUI;
using NowUI.Markdown;

/// <summary>
/// Browses the repository's Docs/ folder: a side menu of pages, the selected
/// page rendered through the markdown extension, and a live demo page running
/// the code from Docs/CustomControls.md. Relative .md links navigate between
/// pages; external links open in the browser.
/// </summary>
[AddComponentMenu("NowUI/Examples/NowUI Docs Browser")]
public class NowUIDocsExample : NowUIGraphic
{
    struct Page
    {
        public string title;
        public string file;
    }

    static readonly Page[] Pages =
    {
        new Page { title = "Overview", file = "README.md" },
        new Page { title = "Features", file = "Features.md" },
        new Page { title = "Layout", file = "Layout.md" },
        new Page { title = "Controls", file = "Controls.md" },
        new Page { title = "Custom controls", file = "CustomControls.md" },
        new Page { title = "Styles & themes", file = "StylesAndThemes.md" },
        new Page { title = "Markdown", file = "Markdown.md" },
        new Page { title = "Lottie", file = "Lottie.md" },
        new Page { title = "Mobile", file = "Mobile.md" },
        new Page { title = "Render pipelines", file = "RenderPipelines.md" },
        new Page { title = "Editor GUI", file = "EditorGUI.md" },
        new Page { title = "Live demo", file = null },
    };

    [SerializeField] NowFontAsset _font;

    int _selected;
    int _rating = 3;
    int _builderRating = 2;
    int _clicks;
    readonly Dictionary<string, string> _docs = new Dictionary<string, string>();

    string LoadDoc(string file)
    {
        if (_docs.TryGetValue(file, out string text))
            return text;

        try
        {
            text = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Docs", file));
        }
        catch (System.Exception exception)
        {
            text = $"# {file}\n\nCould not read `Docs/{file}` (the browser loads the repository's" +
                $" Docs folder, so it only works inside the project):\n\n```\n{exception.Message}\n```";
        }

        _docs[file] = text;
        return text;
    }

    void NavigateLink(string link)
    {
        string target = Path.GetFileName(link.Split('#')[0]);

        for (int i = 0; i < Pages.Length; ++i)
        {
            if (Pages[i].file == target)
            {
                _selected = i;
                return;
            }
        }

        Application.OpenURL(link);
    }

    protected override void DrawNowUI(NowRect rect)
    {
        if (_font == null)
            return;

        Now.defaultFont = _font;
        var theme = NowControls.theme;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        theme.Rectangle(bounds, NowRectangleStyle.Surface).SetRadius(14).Draw();

        var menuRect = new NowRect(bounds.x + 12, bounds.y + 12, 180, bounds.height - 24);
        var contentRect = new NowRect(menuRect.xMax + 12, bounds.y + 12, bounds.xMax - menuRect.xMax - 24, bounds.height - 24);

        using (NowLayout.Area(menuRect))
        {
            NowLayout.Label("NowUI Docs").SetFontSize(13)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();

            for (int i = 0; i < Pages.Length; ++i)
            {
                var style = i == _selected ? NowRectangleStyle.Accent : NowRectangleStyle.Muted;

                if (NowLayout.Button(Pages[i].title).SetId($"doc-{i}").SetStyle(style).SetStretchWidth().Draw())
                    _selected = i;
            }
        }

        using (NowLayout.Area(contentRect))
        using (NowLayout.ScrollView($"docs-scroll-{_selected}").Begin())
        {
            if (Pages[_selected].file == null)
            {
                DrawLiveDemo(theme);
            }
            else
            {
                var result = NowMarkdown.Draw(LoadDoc(Pages[_selected].file));

                if (result.clickedLink != null)
                    NavigateLink(result.clickedLink);
            }
        }
    }

    void DrawLiveDemo(NowUITheme theme)
    {
        NowMarkdown.Draw("# Live demo\n\nThe controls below run the code from" +
            " [CustomControls.md](CustomControls.md) — a wrapped variant, a reshaped" +
            " round button, and a from-scratch rating control.\n\n## Wrap: `DangerButton`");

        if (GuideControls.DangerButton("Delete save").Draw())
            ++_clicks;

        NowMarkdown.Draw("## Reshape: `RoundButton`");

        if (GuideControls.RoundButton("+"))
            ++_clicks;

        NowMarkdown.Draw("## Build: `Rating`");

        GuideControls.Rating(ref _rating);

        NowMarkdown.Draw("## Builder form: `MyControls.Rating()`");

        GuideControls.Rating().SetMax(7).Draw(ref _builderRating);

        NowLayout.Label($"Clicks: {_clicks}   Rating: {_rating}   Builder rating: {_builderRating}")
            .SetFontSize(12).SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
    }
}

/// <summary>The example code from Docs/CustomControls.md, compiled and runnable.</summary>
public static class GuideControls
{
    public static NowButton DangerButton(
        string label,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return NowLayout.Button(label, file, line)
            .SetStyle(NowRectangleStyle.Outline);
    }

    public static bool RoundButton(
        string label,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var theme = NowControls.theme;
        int id = NowControls.GetControlId(NowControls.SiteId(file, line));

        NowRect rect = NowLayout.Rect(new NowLayoutOptions().SetSize(44f, 44f));
        var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

        float radius = rect.width * 0.5f;
        bool inCircle = (interaction.pointerPosition - rect.center).sqrMagnitude <= radius * radius;
        bool clicked = (interaction.clicked && inCircle) || submitted;

        float hoverT = NowUIControlState.Transition(
            NowUIInput.GetId(id, "hover"), interaction.hovered && inCircle);

        var circle = theme.Rectangle(rect, NowRectangleStyle.Accent);
        circle.radius = Vector4.one * radius;
        circle.color = NowControls.StateTint(circle.color, hoverT, interaction.held && inCircle);

        if (focused)
        {
            circle.outline = 2f;
            circle.outlineColor = theme.GetColor(NowColorToken.Text, Color.black);
        }

        circle.Draw();

        var text = theme.ResolveText(NowTextStyle.Button);
        Vector2 size = text.Measure(label);
        text.rect = new NowRect(
            rect.center.x - size.x * 0.5f, rect.center.y - size.y * 0.5f,
            size.x + 2f, size.y);
        text.Draw(label);

        return clicked;
    }

    public static bool Rating(
        ref int value, int max = 5,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return DrawRating(NowControls.GetControlId(NowControls.SiteId(file, line)), ref value, max);
    }

    public static MyRating Rating(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return new MyRating(NowControls.SiteId(file, line));
    }

    internal static bool DrawRating(int id, ref int value, int max)
    {
        const float Dot = 18f, Gap = 6f;

        var theme = NowControls.theme;

        NowRect rect = NowLayout.Rect(new NowLayoutOptions()
            .SetSize(max * Dot + (max - 1) * Gap, Dot));
        var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

        int hoveredIndex = interaction.hovered
            ? Mathf.Clamp(Mathf.FloorToInt((interaction.pointerPosition.x - rect.x) / (Dot + Gap)), 0, max - 1)
            : -1;

        int previous = value;

        if (interaction.clicked && hoveredIndex >= 0)
            value = hoveredIndex + 1 == value ? 0 : hoveredIndex + 1;

        if (submitted)
            value = value % max + 1;

        Color lit = theme.GetColor(NowColorToken.Accent, Color.yellow);
        Color unlit = theme.GetColor(NowColorToken.Border, Color.gray);

        for (int i = 0; i < max; ++i)
        {
            var dotRect = new NowRect(rect.x + i * (Dot + Gap), rect.y, Dot, Dot);
            Color color = i < value ? lit : unlit;

            if (hoveredIndex >= 0 && i <= hoveredIndex && i >= value)
                color = Color.Lerp(unlit, lit, 0.45f);

            Now.Rectangle(dotRect).SetColor(color).SetRadius(Dot * 0.5f).Draw();
        }

        if (focused)
        {
            var ring = Now.Rectangle(rect.Outset(4f, 4f));
            ring.color = Color.clear;
            ring.outline = 2f;
            ring.outlineColor = theme.GetColor(NowColorToken.Accent, Color.blue);
            ring.radius = Vector4.one * (Dot * 0.5f + 4f);
            ring.Draw();
        }

        return value != previous;
    }
}

/// <summary>The builder-form rating from Docs/CustomControls.md.</summary>
[NowBuilder]
public struct MyRating
{
    readonly int _site;
    string _id;
    int _max;

    internal MyRating(int site)
    {
        _site = site;
        _id = null;
        _max = 5;
    }

    public MyRating SetId(string id) { _id = id; return this; }

    public MyRating SetMax(int max) { _max = max; return this; }

    public bool Draw(ref int value)
    {
        int id = _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);
        return GuideControls.DrawRating(id, ref value, _max);
    }
}
