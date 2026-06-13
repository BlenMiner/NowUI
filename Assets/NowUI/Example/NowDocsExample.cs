using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;
using NowUI;
using NowUI.CodeEditor;
using NowUI.Markdown;
using NowUI.Sdf;

/// <summary>
/// Browses the repository's Docs/ folder: a side menu of pages, the selected
/// page rendered through the markdown extension, and live demo pages for
/// extension/runtime examples. Relative .md links navigate between
/// pages; external links open in the browser.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Docs Browser")]
public class NowDocsExample : NowGraphic
{
    enum PageKind
    {
        [UsedImplicitly]
        Markdown,
        ControlsDemo,
        LottieDemo,
        CodeEditorDemo,
        RichTextDemo,
        SdfDemo,
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
        new Page { title = "IMGUI", file = "EditorGUI.md" },
        new Page { title = "Code editor", file = "CodeEditor.md" },
        new Page { title = "Rich text", file = "RichText.md" },
        new Page { title = "SDF shapes", file = "SDF.md" },
        new Page { title = "Rich text demo", kind = PageKind.RichTextDemo },
        new Page { title = "SDF demo", kind = PageKind.SdfDemo },
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
    int _richTextLinkClicks;
    string _richTextLastLink = "none";
    NowRichTextParser _richTextDemoParser;
    readonly NowRichTextSpan[] _richTextDemoSpans = new NowRichTextSpan[3];
    Texture2D _sdfDemoTexture;
    readonly NowSdfGraph _sdfIntersectLeft = NowSdf.Graph();
    readonly NowSdfGraph _sdfIntersectRight = NowSdf.Graph();
    readonly NowSdfGraph _sdfMorphA = NowSdf.Graph();
    readonly NowSdfGraph _sdfMorphB = NowSdf.Graph();
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
        if (!_font)
            return;

        Now.defaultFont = _font;
        var theme = NowControls.theme;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        theme.Rectangle(bounds, NowRectangleStyle.Surface).SetRadius(14).Draw();

        var menuRect = new NowRect(bounds.x + 12, bounds.y + 12, 180, bounds.height - 24);
        var contentRect = new NowRect(menuRect.xMax + 12, bounds.y + 12, bounds.xMax - menuRect.xMax - 24, bounds.height - 24);

        var menuTitleRect = new NowRect(menuRect.x, menuRect.y, menuRect.width, 24f);
        var menuListRect = new NowRect(menuRect.x, menuTitleRect.yMax + 8f, menuRect.width, menuRect.yMax - menuTitleRect.yMax - 8f);

        using (NowLayout.Area(menuTitleRect))
        {
            NowLayout.Label("Now Docs").SetFontSize(13)
                .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
        }

        using (NowLayout.Area(menuListRect))
        using (NowLayout.ScrollView("docs-menu").Begin())
        {
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
                    DrawCodeEditorDemo();
                    break;

                case PageKind.RichTextDemo:
                    DrawRichTextDemo(theme);
                    break;

                case PageKind.SdfDemo:
                    DrawSdfDemo(theme);
                    break;

                default:
                    var result = NowMarkdown.Document(LoadDoc(Pages[_selected].file)).Draw();

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

    void DrawCodeEditorDemo()
    {
        NowMarkdown.Document("# Code editor\n\n`NowUI.Extensions.CodeEditor` — syntax highlighting, validation" +
            " squiggles (hover them, click the status error to jump), bracket/quote auto-close, Enter" +
            " auto-indent, Tab indent/dedent, smart Home, undo/redo, line numbers. Break the JSON below" +
            " and watch the squiggle and status bar.\n\n## JSON").Draw();

        var json = NowCode.Editor(NowJsonLanguage.instance, "demo-json").SetHeight(220).Draw(ref _jsonText);

        if (!json.isValid)
            NowLayout.Label("Invalid JSON — the status bar shows where.").SetFontSize(11)
                .SetColor(new Color(0.86f, 0.24f, 0.24f)).Draw();

        NowMarkdown.Document("## Markdown\n\nThe same editor, different language profile — markdown source" +
            " highlights inline and ```json fences highlight as JSON inside it.").Draw();

        NowLayout.Checkbox("Preview").Draw(ref _markdownPreview);

        if (_markdownPreview)
            NowMarkdown.Document(_markdownText).Draw();
        else
            NowCode.Editor(NowMarkdownCodeLanguage.instance, "demo-md").SetHeight(260).Draw(ref _markdownText);
    }

    void DrawSdfDemo(NowTheme theme)
    {
        NowMarkdown.Document("# SDF demo\n\nThe first panel combines reusable graphs with scene-level operations and a generated texture. The second panel morphs between two graphs every frame.").Draw();

        NowMarkdown.Document("## Graph operations").Draw();
        DrawSdfOperationsPanel(theme);

        NowMarkdown.Document("## Morph").Draw();
        DrawSdfMorphPanel(theme);

        NowControlState.RequestRepaint();
    }

    void DrawSdfOperationsPanel(NowTheme theme)
    {
        var scene = ReserveSdfPanel(theme, 172f);

        if (scene.isEmpty)
            return;

        float gap = 18f;
        float width = Mathf.Max(1f, (scene.width - gap * 2f) / 3f);
        float height = scene.height;
        var unionRect = new NowRect(scene.x, scene.y, width, height);
        var subtractRect = new NowRect(unionRect.xMax + gap, scene.y, width, height);
        var intersectRect = new NowRect(subtractRect.xMax + gap, scene.y, width, height);

        float w = unionRect.width;
        float h = unionRect.height;

        NowSdf.Scene(unionRect, "docs-sdf-op-union")
            .SetTexture(GetSdfDemoTexture())
            .RoundedBox(new NowRect(w * 0.08f, h * 0.22f, w * 0.64f, h * 0.56f), h * 0.16f)
            .SetColor(new Color(1f, 0.34f, 0.18f, 1f))
            .UseColor()
            .SmoothUnion(14f)
            .Circle(new Vector2(w * 0.72f, h * 0.5f), h * 0.29f)
            .Draw();

        w = subtractRect.width;
        h = subtractRect.height;

        NowSdf.Scene(subtractRect, "docs-sdf-op-subtract")
            .SetColor(new Color(0.16f, 0.48f, 0.95f, 1f))
            .RoundedBox(new NowRect(w * 0.12f, h * 0.22f, w * 0.76f, h * 0.56f), h * 0.18f)
            .SmoothSubtract(9f)
            .Circle(new Vector2(w * 0.5f, h * 0.5f), h * 0.2f)
            .Draw();

        w = intersectRect.width;
        h = intersectRect.height;
        var left = _sdfIntersectLeft.Clear()
            .SetColor(new Color(0.12f, 0.82f, 0.68f, 1f))
            .UseColor()
            .Circle(new Vector2(w * 0.42f, h * 0.5f), h * 0.31f);
        var right = _sdfIntersectRight.Clear()
            .SetColor(new Color(0.82f, 0.42f, 1f, 1f))
            .UseColor()
            .Circle(new Vector2(w * 0.58f, h * 0.5f), h * 0.31f);

        NowSdf.Scene(intersectRect, "docs-sdf-op-intersect")
            .Graph(left)
            .SmoothIntersect(12f)
            .Graph(right)
            .Draw();
    }

    void DrawSdfMorphPanel(NowTheme theme)
    {
        var scene = ReserveSdfPanel(theme, 172f);

        if (scene.isEmpty)
            return;

        float width = scene.width;
        float height = scene.height;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time * 0.55f, 1f));

        var sceneA = _sdfMorphA.Clear()
            .SetColor(new Color(0.14f, 0.86f, 0.95f, 1f))
            .UseColor()
            .Circle(new Vector2(width * 0.34f, height * 0.5f), height * 0.28f)
            .SetColor(new Color(0.42f, 0.6f, 1f, 1f))
            .UseColor()
            .SmoothUnion(14f)
            .Circle(new Vector2(width * 0.56f, height * 0.5f), height * 0.28f)
            .SetColor(new Color(0.16f, 0.95f, 0.62f, 1f))
            .UseColor()
            .SmoothUnion(14f)
            .Circle(new Vector2(width * 0.45f, height * 0.34f), height * 0.22f);

        var sceneB = _sdfMorphB.Clear()
            .SetColor(new Color(0.96f, 0.34f, 0.58f, 1f))
            .UseColor()
            .Capsule(new NowRect(width * 0.2f, height * 0.3f, width * 0.6f, height * 0.4f))
            .SetColor(new Color(1f, 0.78f, 0.22f, 1f))
            .UseColor()
            .SmoothUnion(18f)
            .Circle(new Vector2(width * 0.5f, height * 0.5f), height * 0.24f);

        NowSdf.Scene(scene, "docs-sdf-morph")
            .Morph(sceneA, sceneB, t)
            .Draw();
    }

    NowRect ReserveSdfPanel(NowTheme theme, float height)
    {
        var panel = NowLayout.Rect(height: height, stretchWidth: true);
        theme.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();
        return panel.Inset(14f, 14f);
    }

    Texture2D GetSdfDemoTexture()
    {
        if (_sdfDemoTexture != null)
            return _sdfDemoTexture;

        const int Size = 64;
        _sdfDemoTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "Now Docs SDF Demo Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };

        var a = new Color(0.12f, 0.48f, 0.95f, 1f);
        var b = new Color(1f, 0.78f, 0.24f, 1f);
        var c = new Color(0.1f, 0.82f, 0.68f, 1f);

        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                float stripe = ((x / 8 + y / 8) & 1) == 0 ? 0.25f : 0.75f;
                float wave = Mathf.Sin((x + y) * 0.22f) * 0.5f + 0.5f;
                _sdfDemoTexture.SetPixel(x, y, Color.Lerp(Color.Lerp(a, b, stripe), c, wave * 0.35f));
            }
        }

        _sdfDemoTexture.Apply(false, true);
        return _sdfDemoTexture;
    }

    protected override void OnDestroy()
    {
        if (_sdfDemoTexture != null)
        {
            if (Application.isPlaying)
                Destroy(_sdfDemoTexture);
            else
                DestroyImmediate(_sdfDemoTexture);

            _sdfDemoTexture = null;
        }

        base.OnDestroy();
    }

    void DrawRichTextDemo(NowTheme theme)
    {
        NowMarkdown.Document("# Rich text demo\n\nRich text has two entry points: explicit spans for generated content, and tag parsing for small hand-authored UI strings.").Draw();

        var result = NowLayout.RichText("This label has <b>bold</b>, <i>italic</i>, <u>underline</u>, <s>strike</s>, <color=#ffcc00>color</color>, and a <link=\"docs/rich-text\">clickable link</link>.")
            .ParseDefaultTags()
            .SetStretchWidth()
            .Draw();

        if (result.clicked && result.TryGetHitTag(out var tag) && tag.name == "link")
        {
            ++_richTextLinkClicks;
            _richTextLastLink = tag.value;
        }

        NowLayout.Label($"Link clicks: {_richTextLinkClicks}   Last link: {_richTextLastLink}")
            .SetFontSize(12)
            .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw();

        NowMarkdown.Document("## Explicit spans").Draw();

        _richTextDemoSpans[0] = new NowRichTextSpan(0, 5, new NowRichTextStyle(15f, NowFontStyle.Bold).SetColor(theme.GetColor(NowColorToken.Accent, Color.blue)));
        _richTextDemoSpans[1] = new NowRichTextSpan(6, 3, new NowRichTextStyle(15f).SetUnderline());
        _richTextDemoSpans[2] = new NowRichTextSpan(31, 6, new NowRichTextStyle(15f).SetStrikethrough());

        NowLayout.RichText("Spans are useful for generated ranges.")
            .SetSpans(_richTextDemoSpans)
            .SetStretchWidth()
            .Draw();

        NowMarkdown.Document("## Selectable text").Draw();

        NowLayout.RichText("Drag across this sentence to select plain text. Ctrl/Cmd+C copies it, and right-click opens Copy / Select All.")
            .SetSelectable()
            .SetStretchWidth()
            .Draw();

        NowMarkdown.Document("## Custom inline tags").Draw();

        if (_lotties != null && _lotties.Length > 0 && _lotties[0] != null)
        {
            NowLayout.RichText("Loading <lottie id=\"0\" size=\"22\"/> inline with text.")
                .UseParser(RichTextDemoParser())
                .SetStretchWidth()
                .Draw();
        }
        else
        {
            NowMarkdown.Document("Assign a Lottie asset to the docs component to see the `<lottie />` rich-text tag render inline.").Draw();
        }
    }

    NowRichTextParser RichTextDemoParser()
    {
        return _richTextDemoParser ??= NowRichTextParser.Default.WithTag("lottie", ParseDemoLottieTag);
    }

    bool ParseDemoLottieTag(in NowRichTextTagContext context, out NowRichTextTagResult result)
    {
        result = default;

        if (_lotties == null || _lotties.Length == 0)
            return false;

        string id = context.Attribute("id", "0");
        NowLottieAsset asset = null;

        if (int.TryParse(id, out int index) && index >= 0 && index < _lotties.Length)
            asset = _lotties[index];

        if (asset == null)
        {
            for (int i = 0; i < _lotties.Length; ++i)
            {
                if (_lotties[i] != null && _lotties[i].name == id)
                {
                    asset = _lotties[i];
                    break;
                }
            }
        }

        if (asset == null)
            return false;

        float size = context.FloatAttribute("size", context.style.fontSize);
        result = NowRichTextTagResult.Inline(new NowRichTextInline
        {
            width = context.FloatAttribute("width", size),
            height = context.FloatAttribute("height", size),
            payload = asset,
            draw = DrawDemoLottieInline
        });
        return true;
    }

    static void DrawDemoLottieInline(in NowRichTextRun run, NowRect mask)
    {
        if (run.payload is not NowLottieAsset asset)
            return;

        Now.Lottie(run.rect, asset)
            .SetMask(mask)
            .SetTime(Time.time)
            .Draw();
        NowControlState.RequestRepaint();
    }

    void DrawLottieDemo(NowTheme theme)
    {
        NowMarkdown.Document("# Lottie demo\n\nVector animations drawn through `NowLayout.Lottie` —" +
            " tessellated at runtime, no textures. Add assets to the **Lotties** array on the" +
            " `NowDocsExample` component and they show up here.").Draw();

        if (_lotties == null || _lotties.Length == 0)
        {
            NowMarkdown.Document("*No Lottie assets assigned.*").Draw();
            return;
        }

        // Time-driven content: ask retained hosts for the next frame.
        NowControlState.RequestRepaint();

        NowMarkdown.Document("## Gallery").Draw();

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.Center))
        {
            for (int i = 0; i < _lotties.Length; ++i)
            {
                if (_lotties[i] != null)
                    NowLayout.Lottie(_lotties[i]).SetTime(Time.time).SetHeight(64).Draw();
            }
        }

        NowMarkdown.Document("## Playback\n\n`SetTime(Time.time)` plays; `SetNormalizedTime` pins a frame for scrubbing.").Draw();

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

        NowMarkdown.Document("## Sizes\n\nThe same asset scales with the layout box — geometry, not pixels.").Draw();

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.End))
        {
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(24).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(48).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(96).Draw();
        }

        NowMarkdown.Document("## Tinting\n\n`SetColor` multiplies the animation's own colors.").Draw();

        using (NowLayout.Horizontal(spacing: 16, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(theme.GetColor(NowColorToken.Accent, Color.blue)).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(new Color(1f, 1f, 1f, 0.35f)).Draw();
        }
    }

    void DrawLiveDemo(NowTheme theme)
    {
        NowMarkdown.Document("# Live demo\n\nThe controls below run the code from" +
            " [CustomControls.md](CustomControls.md) — a wrapped variant, a reshaped" +
            " round button, and a from-scratch rating control.\n\n## Wrap: `DangerButton`").Draw();

        if (GuideControls.DangerButton("Delete save").Draw())
            ++_clicks;

        NowMarkdown.Document("## Reshape: `RoundButton`").Draw();

        if (GuideControls.RoundButton("+"))
            ++_clicks;

        NowMarkdown.Document("## Build: `Rating`").Draw();

        GuideControls.Rating(ref _rating);

        NowMarkdown.Document("## Builder form: `MyControls.Rating()`").Draw();

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
