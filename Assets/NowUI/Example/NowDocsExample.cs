using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using NowUI;
using NowUI.CodeEditor;
using NowUI.Markdown;

/// <summary>
/// Browses the repository's Docs/ folder: a side menu of pages, the selected
/// page rendered through the markdown extension, and a live demo page running
/// the code from Docs/CustomControls.md. Relative .md links navigate between
/// pages; external links open in the browser.
/// </summary>
[AddComponentMenu("NowUI/Examples/NowUI Docs Browser")]
public class NowDocsExample : NowGraphic
{
    enum PageKind
    {
        Markdown,
        ControlsDemo,
        LottieDemo,
        CodeEditorDemo,
    }

    struct Page
    {
        public string title;
        public string file;
        public PageKind kind;
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
        new Page { title = "Code editor", file = "CodeEditor.md" },
        new Page { title = "Live demo", kind = PageKind.ControlsDemo },
        new Page { title = "Lottie demo", kind = PageKind.LottieDemo },
        new Page { title = "Editor demo", kind = PageKind.CodeEditorDemo },
    };

    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset[] _lotties;

    int _selected;
    int _rating = 3;
    int _builderRating = 2;
    int _clicks;
    bool _lottieScrub;
    float _lottieProgress = 0.35f;
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
                bool selected = i == _selected;
                var style = selected ? NowRectangleStyle.Accent : NowRectangleStyle.Muted;
                var textStyle = selected ? NowTextStyle.Button : NowTextStyle.Body;

                if (NowLayout.Button(Pages[i].title).SetId($"doc-{i}").SetStyle(style).SetTextStyle(textStyle).SetStretchWidth().SetHeight(30f).Draw())
                    _selected = i;
            }
        }

        using (NowLayout.Area(contentRect))
        using (NowLayout.ScrollView($"docs-scroll-{_selected}").Begin())
        {
            switch (Pages[_selected].kind)
            {
                case PageKind.ControlsDemo:
                    DrawLiveDemo(theme);
                    break;

                case PageKind.LottieDemo:
                    DrawLottieDemo(theme);
                    break;

                case PageKind.CodeEditorDemo:
                    DrawCodeEditorDemo(theme);
                    break;

                default:
                    var result = NowMarkdown.Draw(LoadDoc(Pages[_selected].file));

                    if (result.clickedLink != null)
                        NavigateLink(result.clickedLink);
                    break;
            }
        }
    }

    const string JsonSample = "{\n  \"name\": \"NowUI\",\n  \"version\": \"0.1.0\",\n  \"mobileForward\": true,\n  \"platforms\": [\"windows\", \"android\", \"ios\", \"webgl\"],\n  \"dependencies\": null,\n  \"stars\": 5\n}";

    const string MarkdownSample = "# Editing markdown\n\nThis is **markdown source** with **live highlighting** — headings, *emphasis*,\n`inline code`, [links](https://example.com) and fenced blocks:\n\n```json\n{ \"fences\": \"highlight as JSON\" }\n```\n\n- toggle the preview to render it\n- same selection, undo and clipboard as the JSON editor\n";

    string _jsonText = JsonSample;
    string _markdownText = MarkdownSample;
    bool _markdownPreview;

    void DrawCodeEditorDemo(NowTheme theme)
    {
        NowMarkdown.Draw("# Code editor\n\n`NowUI.Extensions.CodeEditor` — syntax highlighting, validation" +
            " squiggles (hover them, click the status error to jump), bracket/quote auto-close, Enter" +
            " auto-indent, Tab indent/dedent, smart Home, undo/redo, line numbers. Break the JSON below" +
            " and watch the squiggle and status bar.\n\n## JSON");

        var json = NowCode.Editor(NowJsonLanguage.instance, "demo-json").SetHeight(220).Draw(ref _jsonText);

        if (!json.isValid)
            NowLayout.Label("Invalid JSON — the status bar shows where.").SetFontSize(11)
                .SetColor(new Color(0.86f, 0.24f, 0.24f)).Draw();

        NowMarkdown.Draw("## Markdown\n\nThe same editor, different language profile — markdown source" +
            " highlights inline and ```json fences highlight as JSON inside it.");

        NowLayout.Checkbox("Preview").Draw(ref _markdownPreview);

        if (_markdownPreview)
            NowMarkdown.Draw(_markdownText);
        else
            NowCode.Editor(NowMarkdownCodeLanguage.instance, "demo-md").SetHeight(260).Draw(ref _markdownText);
    }

    void DrawLottieDemo(NowTheme theme)
    {
        NowMarkdown.Draw("# Lottie demo\n\nVector animations drawn through `NowLayout.Lottie` —" +
            " tessellated at runtime, no textures. Add assets to the **Lotties** array on the" +
            " `NowUIDocsExample` component and they show up here.");

        if (_lotties == null || _lotties.Length == 0)
        {
            NowMarkdown.Draw("*No Lottie assets assigned.*");
            return;
        }

        // Time-driven content: ask retained hosts for the next frame.
        NowControlState.RequestRepaint();

        NowMarkdown.Draw("## Gallery");

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.Center))
        {
            for (int i = 0; i < _lotties.Length; ++i)
            {
                if (_lotties[i] != null)
                    NowLayout.Lottie(_lotties[i]).SetTime(Time.time).SetHeight(64).Draw();
            }
        }

        NowMarkdown.Draw("## Playback\n\n`SetTime(Time.time)` plays; `SetNormalizedTime` pins a frame for scrubbing.");

        using (NowLayout.Horizontal(spacing: 12, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Scrub").Draw(ref _lottieScrub);

            if (_lottieScrub)
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _lottieProgress);
        }

        var featured = NowLayout.Lottie(_lotties[0]).SetHeight(140);

        featured = _lottieScrub
            ? featured.SetNormalizedTime(_lottieProgress)
            : featured.SetTime(Time.time);

        featured.Draw();

        NowMarkdown.Draw("## Sizes\n\nThe same asset scales with the layout box — geometry, not pixels.");

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.End))
        {
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(24).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(48).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(96).Draw();
        }

        NowMarkdown.Draw("## Tinting\n\n`SetColor` multiplies the animation's own colors.");

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(theme.GetColor(NowColorToken.Accent, Color.blue)).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(new Color(1f, 1f, 1f, 0.35f)).Draw();
        }
    }

    void DrawLiveDemo(NowTheme theme)
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

        NowRect rect = NowLayout.Rect(44f, 44f);
        var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

        float radius = rect.width * 0.5f;
        bool inCircle = (interaction.pointerPosition - rect.center).sqrMagnitude <= radius * radius;
        bool clicked = (interaction.clicked && inCircle) || submitted;

        float hoverT = NowControlState.Transition(
            NowInput.GetId(id, "hover"), interaction.hovered && inCircle);

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

        NowRect rect = NowLayout.Rect(max * Dot + (max - 1) * Gap, Dot);
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
