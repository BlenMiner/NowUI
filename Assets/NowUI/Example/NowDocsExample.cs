using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;
using NowUI;
using NowUI.CodeEditor;
using NowUI.Docking;
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
        DockingDemo,
        LinesDemo,
        ShapesDemo,
        CustomMaterialsDemo,
        GlassDemo,
        EffectsDemo,
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
        new Page { title = "Lines", file = "Lines.md" },
        new Page { title = "Shapes", file = "Shapes.md" },
        new Page { title = "Glass", file = "Glass.md" },
        new Page { title = "Custom materials", file = "CustomMaterials.md" },
        new Page { title = "Effects", file = "Effects.md" },
        new Page { title = "Custom controls", file = "CustomControls.md" },
        new Page { title = "Styles & themes", file = "StylesAndThemes.md" },
        new Page { title = "Markdown", file = "Markdown.md" },
        new Page { title = "Lottie", file = "Lottie.md" },
        new Page { title = "Mobile", file = "Mobile.md" },
        new Page { title = "Render pipelines", file = "RenderPipelines.md" },
        new Page { title = "IMGUI", file = "EditorGUI.md" },
        new Page { title = "Code editor", file = "CodeEditor.md" },
        new Page { title = "Docking", file = "Docking.md" },
        new Page { title = "Rich text", file = "RichText.md" },
        new Page { title = "SDF shapes", file = "SDF.md" },
        new Page { title = "Rich text demo", kind = PageKind.RichTextDemo },
        new Page { title = "SDF demo", kind = PageKind.SdfDemo },
        new Page { title = "Docking demo", kind = PageKind.DockingDemo },
        new Page { title = "Lines demo", kind = PageKind.LinesDemo },
        new Page { title = "Shapes demo", kind = PageKind.ShapesDemo },
        new Page { title = "Custom material demo", kind = PageKind.CustomMaterialsDemo },
        new Page { title = "Glass demo", kind = PageKind.GlassDemo },
        new Page { title = "Effects demo", kind = PageKind.EffectsDemo },
        new Page { title = "Live demo", kind = PageKind.ControlsDemo },
        new Page { title = "Lottie demo", kind = PageKind.LottieDemo },
        new Page { title = "Editor demo", kind = PageKind.CodeEditorDemo },
    };

    static readonly int FrostTintId = Shader.PropertyToID("_FrostTint");
    static readonly int FrostEdgeId = Shader.PropertyToID("_FrostEdge");
    static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");
    static readonly int FrostNoiseScaleId = Shader.PropertyToID("_NoiseScale");
    static readonly int FrostTimeScaleId = Shader.PropertyToID("_TimeScale");

    [SerializeField] NowThemeAsset _themeAsset;
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset[] _lotties;

    int _selected;
    int _rating = 3;
    int _builderRating = 2;
    int _clicks;
    bool _lottieScrub;
    float _lottieProgress = 0.35f;
    bool _sdfDemoLocked;
    readonly NowDockSpace _dockDemo = new NowDockSpace();
    bool _dockDemoLayoutApplied;
    bool _dockDemoGrid = true;
    float _dockDemoExposure = 0.62f;
    int _dockDemoSelection = 1;
    int _dockDemoMessages;
    bool _effectsDemoTexture;
    bool _effectsDemoAuto = true;
    bool _effectsDemoWave = true;
    float _effectsDemoProgress = 0.55f;
    float _effectsDemoSubdivision = 4f;
    bool _customMaterialAnimate = true;
    float _customMaterialFrost = 0.72f;
    float _customMaterialRadius = 28f;
    bool _glassDemoAnimate = true;
    float _glassDemoBlur = 8f;
    float _glassDemoTint = 0.06f;
    float _glassDemoRadius = 28f;
    float _glassDemoOutline = 1.5f;
    float _glassDemoSaturation = 1.15f;
    bool _glassDemoDebug = true;
    int _glassDemoQuality = 0;
    int _richTextLinkClicks;
    string _richTextLastLink = "none";
    NowRichTextParser _richTextDemoParser;
    readonly NowRichTextSpan[] _richTextDemoSpans = new NowRichTextSpan[3];
    Texture2D _sdfDemoTexture;
    Texture2D _customMaterialTexture;
    Material _customMaterialCanvas;
    readonly NowSdfGraph _sdfDemoIdle = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoActive = NowSdf.Graph();
    static readonly string[] GlassQualityLabels = { "Auto", "Fast", "Balanced", "High", "Ultra" };
    readonly NowSdfGraph _sdfDemoGlow = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoStreaks = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoOrbit = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoTextIdle = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoTextActive = NowSdf.Graph();
    readonly Vector2[] _shapeDemoPolygon = new Vector2[6];
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

        using var _ = NowTheme.Scope(_themeAsset);

        Now.defaultFont = _font;
        var theme = NowTheme.themeAsset;
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

                case PageKind.DockingDemo:
                    DrawDockingDemo(theme);
                    break;

                case PageKind.LinesDemo:
                    DrawLinesDemo(theme);
                    break;

                case PageKind.ShapesDemo:
                    DrawShapesDemo(theme);
                    break;

                case PageKind.CustomMaterialsDemo:
                    DrawCustomMaterialsDemo(theme);
                    break;

                case PageKind.GlassDemo:
                    DrawGlassDemo(theme);
                    break;

                case PageKind.EffectsDemo:
                    DrawEffectsDemo(theme);
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

    static readonly string[] DockDemoObjects =
    {
        "Main Camera",
        "Key Light",
        "Docking Canvas",
        "Player Controller"
    };

    void DrawDockingDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Docking demo\n\nDrag tabs onto a pane edge to split, across the tab bar to merge or reorder, or outside the dockspace to float. A drop guide shows where the tab will land, and the layout commits when you release. The layout below is a retained `NowDockSpace`, while each panel's content is still submitted every frame.").Draw();

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            if (NowLayout.Button("Reset layout").Draw())
            {
                _dockDemo.ClearLayout();
                _dockDemoLayoutApplied = false;
            }

            if (NowLayout.Button("Add log").Draw())
                ++_dockDemoMessages;

            NowLayout.FlexibleSpace();
            NowLayout.Checkbox("Grid").Draw(ref _dockDemoGrid);
        }

        var panel = NowLayout.Rect(height: 430f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(8f).Draw();

        SubmitDockDemoWindows();

        if (!_dockDemoLayoutApplied)
        {
            _dockDemoLayoutApplied = true;
            _dockDemo.Dock("Hierarchy", "Scene", NowDockSide.Left);
            _dockDemo.Dock("Inspector", "Scene", NowDockSide.Right);
            _dockDemo.Dock("Console", "Scene", NowDockSide.Bottom);
        }

        NowDock.Space(_dockDemo, panel.Inset(10f), 3001)
            .SetMinPaneSize(120f)
            .SetContentPadding(8f)
            .Draw();
    }

    void SubmitDockDemoWindows()
    {
        _dockDemo.Window("Scene", DrawDockDemoScene, id: "Scene");
        _dockDemo.Window("Hierarchy", DrawDockDemoHierarchy, id: "Hierarchy");
        _dockDemo.Window("Inspector", DrawDockDemoInspector, id: "Inspector");
        _dockDemo.Window("Console", DrawDockDemoConsole, id: "Console");
    }

    void DrawDockDemoScene(NowRect rect)
    {
        Now.Rectangle(rect)
            .SetColor(new Color(0.07f, 0.08f, 0.10f, 1f))
            .SetRadius(3f)
            .Draw();

        if (_dockDemoGrid)
        {
            for (float x = rect.x + 18f; x < rect.xMax; x += 26f)
                Now.Rectangle(new NowRect(x, rect.y, 1f, rect.height)).SetColor(new Color(1f, 1f, 1f, 0.055f)).Draw();

            for (float y = rect.y + 18f; y < rect.yMax; y += 26f)
                Now.Rectangle(new NowRect(rect.x, y, rect.width, 1f)).SetColor(new Color(1f, 1f, 1f, 0.055f)).Draw();
        }

        NowLayout.Label("Scene View").SetFontSize(20f).Draw();
        NowLayout.Label("Dock this tab into another panel or split the workspace.").SetFontSize(12f).Draw();
        NowLayout.FlexibleSpace();

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Exposure").SetWidth(72f).Draw();
            NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _dockDemoExposure);
        }

        NowControlState.RequestRepaint();
    }

    void DrawDockDemoHierarchy(NowRect rect)
    {
        NowLayout.Label("Objects").SetFontSize(16f).Draw();

        for (int i = 0; i < DockDemoObjects.Length; ++i)
        {
            if (NowLayout.Radio(DockDemoObjects[i], _dockDemoSelection == i).Draw())
                _dockDemoSelection = i;
        }
    }

    void DrawDockDemoInspector(NowRect rect)
    {
        NowLayout.Label("Inspector").SetFontSize(16f).Draw();
        NowLayout.Label(DockDemoObjects[_dockDemoSelection]).SetFontSize(13f).Draw();
        NowLayout.Checkbox("Show scene grid").Draw(ref _dockDemoGrid);

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Exposure").SetWidth(72f).Draw();
            NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _dockDemoExposure);
        }
    }

    void DrawDockDemoConsole(NowRect rect)
    {
        NowLayout.Label("Console").SetFontSize(16f).Draw();
        NowLayout.Label("Docking initialized").SetFontSize(12f).Draw();
        NowLayout.Label($"Messages: {_dockDemoMessages}").SetFontSize(12f).Draw();
    }

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

    void DrawSdfDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# SDF demo\n\nHover the scene to steer the field and light the SDF text. Click it to morph into cut mode.").Draw();

        DrawSdfPlaygroundPanel(themeAsset);
        NowControlState.RequestRepaint();
    }

    void DrawSdfPlaygroundPanel(NowThemeAsset themeAsset)
    {
        var panel = ReserveSdfPanel(themeAsset, 318f);

        if (panel.isEmpty)
            return;

        int id = NowControls.GetControlId("docs-sdf-playground");
        var interaction = NowControls.Interact(id, panel, out bool focused, out bool submitted);

        if (interaction.clicked || submitted)
            _sdfDemoLocked = !_sdfDemoLocked;

        var scene = panel.Inset(18f, 14f, 18f, 40f);
        float hoverT = NowControlState.Transition(NowInput.GetId(id, "hover"), interaction.hovered || focused, 8f);
        float pressT = NowControlState.Transition(NowInput.GetId(id, "press"), interaction.held, 18f);
        float lockT = NowControlState.Transition(NowInput.GetId(id, "lock"), _sdfDemoLocked, 7f);
        float autoX = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(Time.time * 0.18f, 1f));
        float autoY = Mathf.Sin(Time.time * 0.7f) * 0.5f + 0.5f;
        float w = scene.width;
        float h = scene.height;
        var autoCursor = new Vector2(
            Mathf.Lerp(w * 0.16f, w * 0.84f, autoX),
            Mathf.Lerp(h * 0.24f, h * 0.76f, autoY));
        var cursor = autoCursor;

        if (interaction.hasPointer && (interaction.hovered || interaction.held))
        {
            cursor = new Vector2(
                Mathf.Clamp(interaction.pointerPosition.x - scene.x, 0f, w),
                Mathf.Clamp(interaction.pointerPosition.y - scene.y, 0f, h));
        }

        float steerX = Mathf.Clamp01(cursor.x / Mathf.Max(w, 1f));
        float steerY = Mathf.Clamp01(cursor.y / Mathf.Max(h, 1f));
        float pulse = Mathf.Sin(Time.time * 5.2f) * 0.5f + 0.5f;
        float morphT = Mathf.Clamp01(0.18f + hoverT * 0.46f + lockT * 0.28f + pulse * 0.08f);

        var glow = _sdfDemoGlow.Clear()
            .SetColor(new Color(0.12f, 0.55f, 1f, 0.15f + hoverT * 0.12f))
            .UseColor()
            .Ellipse(new NowRect(w * 0.08f, h * 0.20f, w * 0.84f, h * 0.56f))
            .SetColor(new Color(0.92f, 0.24f, 0.58f, 0.12f + lockT * 0.18f))
            .UseColor()
            .SmoothUnion(34f)
            .Circle(new Vector2(w * (0.24f + steerX * 0.1f), h * 0.34f), h * 0.24f);

        var idle = _sdfDemoIdle.Clear()
            .SetColor(new Color(0.06f, 0.52f, 0.98f, 1f))
            .UseColor()
            .Capsule(new NowRect(w * 0.13f, h * 0.33f, w * 0.74f, h * 0.34f))
            .SetColor(new Color(0.05f, 0.86f, 0.67f, 1f))
            .UseColor()
            .SmoothUnion(24f)
            .Circle(new Vector2(w * 0.23f, h * 0.46f), h * 0.19f)
            .SetColor(new Color(1f, 0.62f, 0.16f, 1f))
            .UseColor()
            .SmoothUnion(22f)
            .Circle(new Vector2(w * 0.77f, h * 0.54f), h * 0.17f)
            .SetColor(new Color(0.85f, 0.23f, 0.95f, 1f))
            .UseColor()
            .SmoothUnion(18f)
            .RoundedBox(new NowRect(w * 0.42f, h * 0.25f, w * 0.20f, h * 0.50f), h * 0.08f);

        var active = _sdfDemoActive.Clear()
            .SetColor(Color.Lerp(new Color(0.16f, 0.39f, 0.96f, 1f), new Color(0.9f, 0.18f, 0.52f, 1f), lockT))
            .UseColor()
            .RoundedBox(new NowRect(w * 0.15f, h * 0.24f, w * 0.70f, h * 0.52f), h * 0.18f)
            .SetColor(Color.Lerp(new Color(0.1f, 0.94f, 0.74f, 1f), new Color(1f, 0.74f, 0.16f, 1f), lockT))
            .UseColor()
            .SmoothUnion(22f)
            .Circle(cursor, h * (0.13f + hoverT * 0.035f + lockT * 0.05f + pressT * 0.02f))
            .SetColor(new Color(1f, 1f, 1f, 0.42f))
            .UseColor()
            .SmoothUnion(18f)
            .Capsule(new NowRect(w * 0.26f, h * (0.69f - steerY * 0.10f), w * 0.48f, h * 0.07f));

        var streaks = _sdfDemoStreaks.Clear()
            .SetColor(new Color(1f, 1f, 1f, 0.24f + hoverT * 0.22f))
            .UseColor()
            .Capsule(new NowRect(w * (0.19f + steerX * 0.16f), h * 0.21f, w * 0.32f, h * 0.055f))
            .SetColor(new Color(0.05f, 0.95f, 0.78f, 0.30f + lockT * 0.18f))
            .UseColor()
            .SmoothUnion(10f)
            .Capsule(new NowRect(w * 0.51f, h * (0.18f + steerY * 0.12f), w * 0.25f, h * 0.06f));

        var orbit = _sdfDemoOrbit.Clear()
            .SetColor(new Color(1f, 0.82f, 0.22f, 0.82f))
            .UseColor()
            .Circle(new Vector2(w * (0.17f + Mathf.Sin(Time.time * 1.7f) * 0.035f), h * 0.72f), h * 0.045f)
            .SetColor(new Color(0.08f, 0.9f, 0.72f, 0.78f))
            .UseColor()
            .SmoothUnion(8f)
            .Circle(new Vector2(w * (0.84f + Mathf.Cos(Time.time * 1.3f) * 0.028f), h * 0.28f), h * 0.038f);

        var sdfFont = _font != null ? _font : Now.font;
        const string idleText = "SDF";
        const string activeText = "CUT";
        float textSize = Mathf.Min(116f, h * 0.39f);
        float textLift = Mathf.Sin(Time.time * 2.8f) * (hoverT + lockT) * 2.5f;
        var textPush = new Vector2(
            (steerX - 0.5f) * hoverT * 22f,
            (steerY - 0.5f) * hoverT * 12f + textLift);
        Vector2 idleSize = sdfFont != null
            ? sdfFont.MeasureText(idleText, textSize, NowFontStyle.Bold)
            : new Vector2(w * 0.33f, textSize);
        Vector2 activeSize = sdfFont != null
            ? sdfFont.MeasureText(activeText, textSize, NowFontStyle.Bold)
            : new Vector2(w * 0.34f, textSize);
        var idlePosition = new Vector2(
            (w - idleSize.x) * 0.5f,
            h * 0.48f - idleSize.y * 0.5f) + textPush;
        var activePosition = new Vector2(
            (w - activeSize.x) * 0.5f,
            h * 0.48f - activeSize.y * 0.5f) + textPush;
        string currentText = lockT > 0.5f ? activeText : idleText;
        Vector2 currentSize = lockT > 0.5f ? activeSize : idleSize;
        Vector2 currentPosition = lockT > 0.5f ? activePosition : idlePosition;
        float sweepX = Mathf.Repeat(Time.time * (0.23f + hoverT * 0.15f) + steerX * 0.22f, 1f);
        var sweepRect = new NowRect(
            Mathf.Lerp(-w * 0.12f, w * 0.92f, sweepX),
            currentPosition.y + currentSize.y * 0.22f,
            w * 0.20f,
            currentSize.y * 0.38f);
        float textMorphT = Mathf.Clamp01(lockT + pressT * 0.12f);

        var textIdle = _sdfDemoTextIdle.Clear()
            .SetColor(new Color(0.06f, 0.08f, 0.13f, 1f))
            .UseColor()
            .Text(idlePosition, idleText, sdfFont, textSize, NowFontStyle.Bold);
        var textActive = _sdfDemoTextActive.Clear()
            .SetColor(new Color(0.08f, 0.07f, 0.12f, 1f))
            .UseColor()
            .Text(activePosition, activeText, sdfFont, textSize, NowFontStyle.Bold);

        NowSdf.Scene(scene, "docs-sdf-playground-main")
            .Graph(glow)
            .SmoothUnion(28f)
            .Morph(idle, active, morphT)
            .SmoothUnion(10f)
            .Graph(streaks)
            .SmoothUnion(8f)
            .Graph(orbit)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-text")
            .Morph(textIdle, textActive, textMorphT)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-text-spotlight")
            .SetColor(Color.Lerp(new Color(0.03f, 0.92f, 0.78f, 0.56f), new Color(1f, 0.72f, 0.18f, 0.68f), lockT))
            .Text(currentPosition, currentText, sdfFont, textSize, NowFontStyle.Bold)
            .SmoothIntersect(16f)
            .Circle(cursor, h * (0.21f + hoverT * 0.06f + pressT * 0.04f))
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-text-sweep")
            .SetColor(new Color(1f, 1f, 0.82f, 0.42f + hoverT * 0.24f))
            .Text(currentPosition, currentText, sdfFont, textSize, NowFontStyle.Bold)
            .SmoothIntersect(7f)
            .Capsule(sweepRect)
            .Draw();

        var textureRect = new NowRect(scene.x + scene.width * 0.055f, scene.y + scene.height * 0.68f, scene.width * 0.22f, scene.height * 0.19f);
        NowSdf.Scene(textureRect, "docs-sdf-playground-texture")
            .SetTexture(GetSdfDemoTexture())
            .RoundedBox(new NowRect(0f, 0f, textureRect.width, textureRect.height), textureRect.height * 0.32f)
            .SmoothSubtract(6f)
            .Circle(new Vector2(textureRect.width * (0.30f + steerX * 0.32f), textureRect.height * 0.5f), textureRect.height * 0.28f)
            .Draw();

        DrawSdfPanelLabel(
            themeAsset,
            new NowRect(panel.x, panel.y + panel.height - 29f, panel.width, 22f),
            _sdfDemoLocked ? "cut mode locked" : hoverT > 0.5f ? "click to morph the text" : "hover to steer the field",
            themeAsset.GetColor(NowColorToken.TextMuted, Color.gray));
    }

    void DrawSdfPanelLabel(NowThemeAsset themeAsset, NowRect rect, string value, Color color)
    {
        var text = themeAsset.Text(default, NowTextStyle.Muted)
            .SetFontSize(12f)
            .SetColor(color);
        Vector2 size = text.Measure(value);
        text.rect = new NowRect(
            rect.x + (rect.width - size.x) * 0.5f,
            rect.y + (rect.height - size.y) * 0.5f,
            size.x + 1f,
            size.y + 1f);
        text.SetMask(rect.Outset(2f, 4f)).Draw(value);
    }

    NowRect ReserveSdfPanel(NowThemeAsset themeAsset, float height)
    {
        var panel = NowLayout.Rect(height: height, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();
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

        if (_customMaterialTexture != null)
        {
            if (Application.isPlaying)
                Destroy(_customMaterialTexture);
            else
                DestroyImmediate(_customMaterialTexture);

            _customMaterialTexture = null;
        }

        if (_customMaterialCanvas != null)
        {
            if (Application.isPlaying)
                Destroy(_customMaterialCanvas);
            else
                DestroyImmediate(_customMaterialCanvas);

            _customMaterialCanvas = null;
        }

        base.OnDestroy();
    }

    void DrawRichTextDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Rich text demo\n\nRich text has two entry points: explicit spans for generated content, and tag parsing for small hand-authored UI strings.").Draw();

        var result = NowLayout.RichText("This label has <b>bold</b>, <i>italic</i>, <u>underline</u>, <s>strike</s>, <color=#ffcc00>color</color>, and a <link=\"docs/rich-text\">clickable link</link>.")
            .ParseDefaultTags()
            .SetStretchWidth()
            .Draw();

        if (result.clicked && result.TryGetHitTag(out var hitTag) && hitTag.name == "link")
        {
            ++_richTextLinkClicks;
            _richTextLastLink = hitTag.value;
        }

        NowLayout.Label($"Link clicks: {_richTextLinkClicks}   Last link: {_richTextLastLink}")
            .SetFontSize(12)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw();

        NowMarkdown.Document("## Explicit spans").Draw();

        _richTextDemoSpans[0] = new NowRichTextSpan(0, 5, new NowRichTextStyle(15f, NowFontStyle.Bold).SetColor(themeAsset.GetColor(NowColorToken.Accent, Color.blue)));
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

    void DrawLinesDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Lines demo\n\nStraight strokes, cubic Beziers, dash patterns, masks, and arrow heads are all immediate-mode draw calls.").Draw();

        var panel = NowLayout.Rect(height: 280f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();

        var area = panel.Inset(24f, 18f);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color warm = new Color(1f, 0.72f, 0.16f, 1f);
        Color pink = new Color(0.92f, 0.24f, 0.58f, 1f);

        using (Now.Mask(panel))
        {
            Now.Line(new Vector2(area.x, area.y + 38f), new Vector2(area.xMax, area.y + 38f))
                .SetWidth(2f)
                .SetDash(12f, 8f, Time.time * 32f)
                .SetColor(muted)
                .Draw();

            Now.Bezier(
                    new Vector2(area.x + 6f, area.y + 132f),
                    new Vector2(area.x + area.width * 0.25f, area.y - 10f),
                    new Vector2(area.x + area.width * 0.72f, area.y + 230f),
                    new Vector2(area.xMax - 8f, area.y + 112f))
                .SetWidth(6f)
                .SetCap(NowLineCap.Round)
                .SetColor(accent)
                .Draw();

            Now.Bezier(
                    new Vector2(area.x + 4f, area.y + 198f),
                    new Vector2(area.x + area.width * 0.24f, area.y + 132f),
                    new Vector2(area.x + area.width * 0.72f, area.y + 262f),
                    new Vector2(area.xMax - 8f, area.y + 190f))
                .SetWidth(3f)
                .SetDash(16f, 10f, -Time.time * 44f)
                .SetArrow(NowLineArrow.End, 20f, 16f)
                .SetColor(warm)
                .Draw();

            Now.Line(new Vector2(area.x + 28f, area.y + 236f), new Vector2(area.xMax - 28f, area.y + 236f))
                .SetWidth(4f)
                .SetArrow(NowLineArrow.Both, 18f, 14f)
                .SetColor(pink)
                .Draw();
        }

        Now.Text(new NowRect(area.x + 8f, area.y + 6f, area.width - 16f, 22f))
            .SetFontSize(12f)
            .SetColor(text)
            .Draw("Dashed separators, round Beziers, and arrowed connectors share the default mesh batch.");

        NowControlState.RequestRepaint();
    }

    void DrawShapesDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Shapes demo\n\nCore shapes are filled or outlined geometry submitted through the same mesh batch as other NowUI draws.").Draw();

        var panel = NowLayout.Rect(height: 280f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();

        var area = panel.Inset(24f, 18f);
        Color accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color green = new Color(0.05f, 0.86f, 0.67f, 1f);
        Color warm = new Color(1f, 0.72f, 0.16f, 1f);
        Color pink = new Color(0.92f, 0.24f, 0.58f, 1f);
        float pulse = Mathf.Sin(Time.time * 2.4f) * 0.5f + 0.5f;

        Vector2 polygonCenter = new Vector2(area.x + area.width * 0.72f, area.y + 132f);

        for (int i = 0; i < _shapeDemoPolygon.Length; ++i)
        {
            float angle = Mathf.PI * 2f * i / _shapeDemoPolygon.Length - Mathf.PI * 0.5f;
            float radius = (i & 1) == 0 ? 58f : 36f + pulse * 8f;
            _shapeDemoPolygon[i] = polygonCenter + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        using (Now.Mask(panel))
        {
            Now.Circle(new Vector2(area.x + 70f, area.y + 82f), 42f)
                .SetColor(green)
                .SetOutline(3f)
                .SetOutlineColor(new Color(1f, 1f, 1f, 0.88f))
                .Draw();

            Now.Circle(new Vector2(area.x + 176f, area.y + 82f), 36f)
                .SetFill(false)
                .SetOutline(5f)
                .SetOutlineColor(accent)
                .Draw();

            Now.Ellipse(new NowRect(area.x + 34f, area.y + 166f, 128f, 62f))
                .SetColor(warm)
                .SetOutline(2f)
                .SetOutlineColor(new Color(0f, 0f, 0f, 0.22f))
                .Draw();

            Now.Triangle(
                    new Vector2(area.x + 198f, area.y + 226f),
                    new Vector2(area.x + 268f, area.y + 226f),
                    new Vector2(area.x + 232f, area.y + 156f))
                .SetColor(pink)
                .SetOutline(2f)
                .SetOutlineColor(new Color(1f, 1f, 1f, 0.8f))
                .Draw();

            Now.Polygon(_shapeDemoPolygon)
                .SetColor(new Color(accent.r, accent.g, accent.b, 0.78f))
                .SetOutline(3f)
                .SetOutlineColor(text)
                .Draw();
        }

        Now.Text(new NowRect(area.x + 8f, area.y + 6f, area.width - 16f, 22f))
            .SetFontSize(12f)
            .SetColor(muted)
            .Draw("Circles, ellipses, triangles, and reusable-buffer polygons.");

        NowControlState.RequestRepaint();
    }

    void DrawCustomMaterialsDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Custom material demo\n\nThis page is rendered by `Assets/Scenes/DocsScene.unity`. The large panel below is an ordinary `Now.Rectangle` using a generated noise texture and a custom UGUI material via `SetCanvasMaterial(...)`.").Draw();

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Animate").Draw(ref _customMaterialAnimate);

            NowLayout.Label("Frost").SetWidth(42f).Draw();
            NowLayout.Slider(0f, 1f).SetWidth(140f).Draw(ref _customMaterialFrost);

            NowLayout.Label("Radius").SetWidth(48f).Draw();
            NowLayout.Slider(0f, 44f).SetWidth(150f).Draw(ref _customMaterialRadius);
        }

        var panel = NowLayout.Rect(height: 330f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();

        var area = panel.Inset(24f, 20f);
        var mat = GetCustomMaterialCanvas(themeAsset);
        var texture = GetCustomMaterialTexture();
        float frost = Mathf.Clamp01(_customMaterialFrost);
        float radius = Mathf.Clamp(_customMaterialRadius, 0f, 44f);

        DrawCustomMaterialBackdrop(area, themeAsset);

        var glass = new NowRect(area.x + area.width * 0.12f, area.y + 58f, area.width * 0.76f, 170f);
        var highlight = glass.Inset(20f, 18f, 20f, glass.height * 0.56f);

        Now.Rectangle(glass)
            .SetTexture(texture)
            .SetColor(new Color(1f, 1f, 1f, 0.92f))
            .SetRadius(radius)
            .SetOutline(1.5f)
            .SetOutlineColor(new Color(1f, 1f, 1f, 0.52f))
            .SetCanvasMaterial(mat)
            .Draw();

        Now.Rectangle(highlight)
            .SetColor(new Color(1f, 1f, 1f, 0.18f + frost * 0.12f))
            .SetRadius(Mathf.Max(6f, radius * 0.45f))
            .Draw();

        Now.Text(new NowRect(glass.x + 28f, glass.y + 44f, glass.width - 56f, 40f))
            .SetFontSize(28f)
            .SetBold()
            .SetColor(new Color(0.04f, 0.08f, 0.12f, 0.84f))
            .Draw("Frost material");

        Now.Text(new NowRect(glass.x + 30f, glass.y + 94f, glass.width - 60f, 44f))
            .SetFontSize(13f)
            .SetColor(new Color(0.04f, 0.08f, 0.12f, 0.68f))
            .Draw("Same rectangle geometry, custom shader pass, UGUI material override.");

        var codeRect = new NowRect(area.x + 16f, area.yMax - 48f, area.width - 32f, 28f);
        Now.Rectangle(codeRect)
            .SetColor(new Color(0f, 0f, 0f, 0.22f))
            .SetRadius(7f)
            .Draw();

        Now.Text(codeRect.Inset(10f, 6f))
            .SetFontSize(12f)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw("Now.Rectangle(rect).SetTexture(noise).SetCanvasMaterial(frostUGUIMaterial).Draw();");

        if (_customMaterialAnimate)
            NowControlState.RequestRepaint();
    }

    void DrawCustomMaterialBackdrop(NowRect area, NowThemeAsset themeAsset)
    {
        Color accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);

        for (int i = 0; i < 6; ++i)
        {
            float t = i / 5f;
            float x = Mathf.Lerp(area.x + 12f, area.xMax - 180f, t);
            float y = area.y + 38f + Mathf.Sin(t * Mathf.PI * 2f + Time.time * 0.25f) * 10f;

            Now.Rectangle(new NowRect(x, y, 132f, 26f))
                .SetColor(new Color(accent.r, accent.g, accent.b, 0.08f + t * 0.08f))
                .SetRadius(13f)
                .Draw();
        }

        for (int i = 0; i < 4; ++i)
        {
            float x = area.x + 30f + i * 118f;
            Now.Line(new Vector2(x, area.y + 42f), new Vector2(x + 84f, area.yMax - 72f))
                .SetWidth(2f)
                .SetDash(10f, 8f, Time.time * 20f)
                .SetColor(new Color(muted.r, muted.g, muted.b, 0.22f))
                .Draw();
        }

        Now.Circle(new Vector2(area.xMax - 88f, area.y + 74f), 36f)
            .SetColor(new Color(0.05f, 0.86f, 0.67f, 0.38f))
            .Draw();

        Now.Triangle(
                new Vector2(area.x + 70f, area.yMax - 76f),
                new Vector2(area.x + 158f, area.yMax - 62f),
                new Vector2(area.x + 102f, area.yMax - 144f))
            .SetColor(new Color(1f, 0.72f, 0.16f, 0.34f))
            .Draw();

        Now.Text(new NowRect(area.x + 18f, area.y + 10f, area.width - 36f, 24f))
            .SetFontSize(12f)
            .SetColor(new Color(text.r, text.g, text.b, 0.72f))
            .Draw("The shader runs on the foreground rectangle; everything behind it is normal NowUI.");
    }

    Material GetCustomMaterialCanvas(NowThemeAsset themeAsset)
    {
        if (_customMaterialCanvas == null)
        {
            var shader = Shader.Find("NowUI/Examples/Frost Rectangle UGUI");
            var fallback = Resources.Load<Material>("NowUI/UIMaterialUGUI");

            _customMaterialCanvas = shader != null
                ? new Material(shader)
                : fallback != null ? new Material(fallback) : null;

            if (_customMaterialCanvas != null)
            {
                _customMaterialCanvas.name = "Now Docs Frost Rectangle UGUI";
                _customMaterialCanvas.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        if (_customMaterialCanvas == null)
            return null;

        Color accent = themeAsset.GetColor(NowColorToken.Accent, new Color(0.13f, 0.48f, 0.92f, 1f));
        _customMaterialCanvas.SetColor(FrostTintId, Color.Lerp(new Color(0.78f, 0.96f, 1f, 0.84f), accent, 0.18f));
        _customMaterialCanvas.SetColor(FrostEdgeId, new Color(1f, 1f, 1f, 0.95f));
        _customMaterialCanvas.SetFloat(FrostAmountId, Mathf.Clamp01(_customMaterialFrost));
        _customMaterialCanvas.SetFloat(FrostNoiseScaleId, 2.8f);
        _customMaterialCanvas.SetFloat(FrostTimeScaleId, _customMaterialAnimate ? 1f : 0f);
        return _customMaterialCanvas;
    }

    Texture2D GetCustomMaterialTexture()
    {
        if (_customMaterialTexture != null)
            return _customMaterialTexture;

        const int Size = 96;
        _customMaterialTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "Now Docs Frost Noise",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < Size; ++y)
        {
            for (int x = 0; x < Size; ++x)
            {
                float nx = x / (float)Size;
                float ny = y / (float)Size;
                float grain = Mathf.PerlinNoise(nx * 9.5f, ny * 9.5f);
                float veins = Mathf.PerlinNoise(nx * 25f + 17.4f, ny * 11f + 3.2f);
                float frost = Mathf.Clamp01(grain * 0.72f + veins * 0.28f);
                _customMaterialTexture.SetPixel(x, y, new Color(frost, grain, veins, 1f));
            }
        }

        _customMaterialTexture.Apply(false, true);
        return _customMaterialTexture;
    }

    void DrawGlassDemo(NowThemeAsset themeAsset)
    {
        NowGlassSettings.diagnosticsEnabled = _glassDemoDebug;
        NowMarkdown.Document("# Glass demo\n\nThis page is rendered by the UGUI docs browser. UGUI glass automatically uses the expensive backdrop path when a `Now.Glass` batch is present: it replays the NowUI batches behind each glass pane into a texture, blurs that texture, then keeps the foreground labels as sharp UGUI geometry.").Draw();

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Animate").Draw(ref _glassDemoAnimate);

            NowLayout.Label("Blur").SetWidth(34f).Draw();
            NowLayout.Slider(0f, 36f).SetWidth(120f).Draw(ref _glassDemoBlur);

            NowLayout.Label("Tint").SetWidth(34f).Draw();
            NowLayout.Slider(0f, 0.5f).SetWidth(120f).Draw(ref _glassDemoTint);

            NowLayout.Label("Quality").SetWidth(54f).Draw();
            NowLayout.Dropdown(GlassQualityLabels).SetWidth(118f).Draw(ref _glassDemoQuality);
        }

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Radius").SetWidth(48f).Draw();
            NowLayout.Slider(0f, 44f).SetWidth(120f).Draw(ref _glassDemoRadius);

            NowLayout.Label("Outline").SetWidth(50f).Draw();
            NowLayout.Slider(0f, 4f).SetWidth(110f).Draw(ref _glassDemoOutline);

            NowLayout.Label("Sat").SetWidth(28f).Draw();
            NowLayout.Slider(0.5f, 1.8f).SetWidth(120f).Draw(ref _glassDemoSaturation);

            NowLayout.Checkbox("Debug RTs").Draw(ref _glassDemoDebug);
        }

        float debugHeight = _glassDemoDebug ? 214f : 0f;
        var panel = NowLayout.Rect(height: 360f + debugHeight, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();

        var preview = panel.Inset(22f, 20f, 22f, 54f + debugHeight);
        DrawGlassDemoScene(preview, themeAsset);

        if (_glassDemoDebug)
        {
            var debugRect = new NowRect(panel.x + 22f, preview.yMax + 12f, panel.width - 44f, debugHeight - 20f);
            DrawGlassDemoDebug(debugRect, themeAsset);
        }

        var codeRect = new NowRect(panel.x + 30f, panel.yMax - 42f, panel.width - 60f, 26f);
        Now.Rectangle(codeRect)
            .SetColor(new Color(0f, 0f, 0f, 0.20f))
            .SetRadius(7f)
            .Draw();

        Now.Text(codeRect.Inset(10f, 5f))
            .SetFontSize(12f)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw("Now.Glass(rect).SetBlurRadius(blur).SetBlurQuality(quality).SetTint(tint).SetVibrancy(saturation, 1).Draw();");

        if (_glassDemoAnimate || _glassDemoDebug)
            NowControlState.RequestRepaint();
    }

    void DrawGlassDemoScene(NowRect rect, NowThemeAsset themeAsset)
    {
        Color accent = themeAsset.GetColor(NowColorToken.Accent, new Color(0.12f, 0.48f, 0.95f, 1f));
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        float time = _glassDemoAnimate ? Time.time : 0f;
        var glassWidth = Mathf.Max(1f, Mathf.Min(420f, rect.width - 48f));
        var glass = new NowRect(rect.x + (rect.width - glassWidth) * 0.5f, rect.y + 54f, glassWidth, 178f);

        using (Now.Mask(rect))
        {
            Now.Rectangle(rect)
                .SetColor(new Color(0.035f, 0.045f, 0.065f, 1f))
                .SetRadius(12f)
                .Draw();

            DrawGlassDemoBackdrop(rect, glass, accent, muted, time);
        }

        Now.Glass(glass)
            .SetBlurRadius(_glassDemoBlur)
            .SetBlurQuality(GetGlassDemoQuality())
            .SetTint(new Color(1f, 1f, 1f, Mathf.Clamp01(_glassDemoTint)))
            .SetVibrancy(_glassDemoSaturation, 1f)
            .SetRadius(Mathf.Clamp(_glassDemoRadius, 0f, 44f))
            .SetOutline(_glassDemoOutline)
            .SetOutlineColor(new Color(1f, 1f, 1f, 0.42f))
            .Draw();

        Now.Rectangle(new NowRect(glass.x + 24f, glass.y + 26f, 76f, 76f))
            .SetColor(new Color(1f, 1f, 1f, 0.28f))
            .SetRadius(22f)
            .Draw();

        Now.Circle(new Vector2(glass.x + 62f, glass.y + 64f), 21f)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.72f))
            .Draw();

        Now.Text(new NowRect(glass.x + 124f, glass.y + 34f, glass.width - 152f, 44f))
            .SetFontSize(30f)
            .SetBold()
            .SetColor(text)
            .Draw("Glass panel");

        Now.Text(new NowRect(glass.x + 126f, glass.y + 86f, glass.width - 154f, 54f))
            .SetFontSize(13f)
            .SetColor(new Color(text.r, text.g, text.b, 0.78f))
            .Draw("Backdrop replay is blurred behind this pane.");

        var meter = new NowRect(glass.x + 126f, glass.yMax - 42f, glass.width - 170f, 8f);
        Now.Rectangle(meter)
            .SetColor(new Color(1f, 1f, 1f, 0.20f))
            .SetRadius(4f)
            .Draw();
        Now.Rectangle(new NowRect(meter.x, meter.y, meter.width * Mathf.InverseLerp(0f, 36f, _glassDemoBlur), meter.height))
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.80f))
            .SetRadius(4f)
            .Draw();
    }

    NowGlassBlurQuality GetGlassDemoQuality()
    {
        return Mathf.Clamp(_glassDemoQuality, 0, GlassQualityLabels.Length - 1) switch
        {
            1 => NowGlassBlurQuality.Fast,
            2 => NowGlassBlurQuality.Balanced,
            3 => NowGlassBlurQuality.High,
            4 => NowGlassBlurQuality.Ultra,
            _ => NowGlassBlurQuality.Auto
        };
    }

    void DrawGlassDemoDebug(NowRect rect, NowThemeAsset themeAsset)
    {
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = themeAsset.GetColor(NowColorToken.Accent, new Color(0.12f, 0.48f, 0.95f, 1f));

        Now.Rectangle(rect)
            .SetColor(new Color(0f, 0f, 0f, 0.22f))
            .SetRadius(8f)
            .Draw();

        int count = uguiGlassDebugTextureCount;
        Now.Text(new NowRect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 20f))
            .SetFontSize(12f)
            .SetBold()
            .SetColor(text)
            .Draw($"UGUI glass debug: {count} backdrop set(s), previous build");

        if (count == 0 || !TryGetUGUIGlassDebugInfo(0, out var info))
        {
            Now.Text(new NowRect(rect.x + 12f, rect.y + 38f, rect.width - 24f, rect.height - 46f))
                .SetFontSize(12f)
                .SetColor(muted)
                .Draw("No UGUI glass textures have been built yet. Keep Debug RTs enabled for one repaint; this panel should populate automatically.");
            return;
        }

        float previewGap = 10f;
        float previewW = Mathf.Min(260f, (rect.width - 340f) * 0.5f);
        previewW = Mathf.Max(120f, previewW);
        float previewH = Mathf.Max(72f, rect.height - 50f);
        var sourceRect = new NowRect(rect.x + 12f, rect.y + 34f, previewW, previewH);
        var blurredRect = new NowRect(sourceRect.xMax + previewGap, sourceRect.y, previewW, previewH);
        var infoRect = new NowRect(blurredRect.xMax + 16f, sourceRect.y, rect.xMax - blurredRect.xMax - 28f, previewH);

        DrawGlassDebugTexturePreview(sourceRect, "Baked source", GetUGUIGlassDebugSourceTexture(0), text, muted);
        DrawGlassDebugTexturePreview(blurredRect, "Blurred sample", GetUGUIGlassDebugBlurredTexture(0), text, muted);

        var frameDiagnostics = NowGlassSettings.lastFrameDiagnostics;
        string details =
            $"RT {info.width}x{info.height} | glass batch {info.batchIndex} | prefix batches {info.replayBatchCount}\n" +
            $"quality {info.blurQuality} | fallback {info.fallbackReason} | frame {info.frame}\n" +
            $"blur {info.blurRadius:0.##} px | ds x{info.blurDownsample} | passes {info.blurIterations} | step {info.blurStep:0.##}\n" +
            $"crop {info.captureRect.x:0.#},{info.captureRect.y:0.#} {info.captureRect.width:0.#}x{info.captureRect.height:0.#} | external source {(info.hasExternalSource ? "yes" : "no")}\n" +
            $"frame totals panes {frameDiagnostics.paneCount} | copied px {frameDiagnostics.copiedPixels} | blurred px {frameDiagnostics.blurredPixels} | passes {frameDiagnostics.blurPasses}\n" +
            "Source should show sharp backdrop content. Blurred should show the same content softened. If both are gray, replay/blur is wrong; if these look right but the pane is wrong, composite binding is wrong.";

        Now.Rectangle(infoRect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.08f))
            .SetRadius(6f)
            .Draw();

        Now.Text(infoRect.Inset(10f, 8f))
            .SetFontSize(11f)
            .SetColor(new Color(text.r, text.g, text.b, 0.84f))
            .Draw(details);
    }

    static void DrawGlassDebugTexturePreview(NowRect rect, string label, Texture texture, Color text, Color muted)
    {
        Now.Rectangle(rect)
            .SetColor(new Color(0f, 0f, 0f, 0.34f))
            .SetRadius(6f)
            .Draw();

        var imageRect = rect.Inset(6f, 22f, 6f, 6f);

        if (texture != null)
        {
            Now.Rectangle(imageRect)
                .SetTexture(texture)
                .SetColor(Color.white)
                .SetRadius(4f)
                .Draw();
        }
        else
        {
            Now.Rectangle(imageRect)
                .SetColor(new Color(0.35f, 0.05f, 0.05f, 0.9f))
                .SetRadius(4f)
                .Draw();

            Now.Text(imageRect.Inset(8f, 8f))
                .SetFontSize(11f)
                .SetColor(text)
                .Draw("null texture");
        }

        Now.Text(new NowRect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 16f))
            .SetFontSize(11f)
            .SetBold()
            .SetColor(texture != null ? text : muted)
            .Draw(label);
    }

    static void DrawGlassDemoBackdrop(NowRect rect, NowRect glass, Color accent, Color muted, float time)
    {
        Color warm = new Color(1f, 0.66f, 0.18f, 1f);
        Color teal = new Color(0.04f, 0.84f, 0.68f, 1f);
        Color pink = new Color(0.95f, 0.22f, 0.62f, 1f);

        for (int i = 0; i < 13; ++i)
        {
            float x = rect.x - 60f + i * 62f + Mathf.Sin(time * 0.55f + i * 0.6f) * 16f;
            Color color = i % 3 == 0 ? accent : i % 3 == 1 ? warm : teal;

            Now.Rectangle(new NowRect(x, rect.y + 24f, 28f, rect.height - 48f))
                .SetColor(new Color(color.r, color.g, color.b, 0.86f))
                .SetRadius(14f)
                .Draw();
        }

        for (int i = 0; i < 6; ++i)
        {
            float y = rect.y + 42f + i * 44f;
            Now.Line(new Vector2(rect.x + 18f, y), new Vector2(rect.xMax - 18f, y + Mathf.Sin(time + i) * 10f))
                .SetWidth(2f)
                .SetDash(16f, 12f, time * 24f + i * 7f)
                .SetColor(new Color(muted.r, muted.g, muted.b, 0.55f))
                .Draw();
        }

        Now.Circle(new Vector2(glass.x + 88f, glass.y + 92f), 86f)
            .SetColor(new Color(teal.r, teal.g, teal.b, 0.92f))
            .Draw();

        Now.Circle(new Vector2(glass.xMax - 74f, glass.y + 88f), 94f)
            .SetColor(new Color(warm.r, warm.g, warm.b, 0.90f))
            .Draw();

        Now.Rectangle(new NowRect(glass.x + 44f, glass.y + 74f, glass.width - 88f, 42f))
            .SetColor(new Color(1f, 1f, 1f, 0.82f))
            .SetRadius(21f)
            .Draw();

        Now.Text(new NowRect(glass.x + 60f, glass.y + 78f, glass.width - 120f, 38f))
            .SetFontSize(28f)
            .SetBold()
            .SetColor(new Color(0.04f, 0.05f, 0.07f, 0.92f))
            .Draw("BEHIND GLASS");

        Now.Text(new NowRect(rect.x + 32f, rect.y + 26f, 250f, 30f))
            .SetFontSize(14f)
            .SetColor(new Color(1f, 1f, 1f, 0.78f))
            .Draw("Sharp backdrop content");

        Now.Circle(new Vector2(rect.xMax - 92f, rect.y + 82f), 42f)
            .SetColor(new Color(pink.r, pink.g, pink.b, 0.80f))
            .Draw();

        Now.Triangle(
                new Vector2(rect.x + 64f, rect.yMax - 62f),
                new Vector2(rect.x + 166f, rect.yMax - 48f),
                new Vector2(rect.x + 110f, rect.yMax - 146f))
            .SetColor(new Color(warm.r, warm.g, warm.b, 0.78f))
            .Draw();
    }

    void DrawEffectsDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Effects demo\n\n`NowEffects.Modifier(...)` captures ordinary draw commands inside a scope, then deforms the captured vertices. Mesh mode keeps text and geometry crisp; texture mode flattens the scope first and deforms one textured surface.").Draw();

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Texture backend").Draw(ref _effectsDemoTexture);
            NowLayout.Checkbox("Wave").Draw(ref _effectsDemoWave);
            NowLayout.Checkbox("Auto").Draw(ref _effectsDemoAuto);
        }

        using (NowLayout.Horizontal(spacing: 10f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Progress").SetWidth(74f).Draw();
            if (!_effectsDemoAuto)
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _effectsDemoProgress);
            else
                NowLayout.Label("Animated").SetFontSize(12f).SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
        }

        using (NowLayout.Horizontal(spacing: 10f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Subdivision").SetWidth(74f).Draw();
            NowLayout.Slider(1f, 10f).SetStretchWidth().Draw(ref _effectsDemoSubdivision);
            NowLayout.Label(Mathf.RoundToInt(_effectsDemoSubdivision).ToString()).SetWidth(28f).Draw();
        }

        var panel = NowLayout.Rect(height: 360f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).SetRadius(10f).Draw();

        float progress = _effectsDemoAuto
            ? Mathf.PingPong(Time.time * 0.38f, 1f)
            : Mathf.Clamp01(_effectsDemoProgress);
        int subdivisions = Mathf.Max(1, Mathf.RoundToInt(_effectsDemoSubdivision));
        var area = panel.Inset(26f, 24f);
        var windowRect = new NowRect(area.x + 20f, area.y + 34f, Mathf.Min(360f, area.width * 0.62f), 210f);
        var targetRect = new NowRect(area.xMax - 92f, area.yMax - 64f, 70f, 38f);

        using (Now.Mask(panel))
        {
            DrawEffectsDemoBackdrop(area, targetRect, progress, themeAsset);

            if (_effectsDemoWave)
            {
                var modifier = NowEffects.Modifier(NowDeformers.Wave(Time.time * 0.45f, 9f, 44f, NowWaveAxis.Y))
                    .SetId(2001)
                    .SetSubdivision(subdivisions);

                if (_effectsDemoTexture)
                    modifier = modifier.SetRenderToTexture();

                using (modifier.Begin())
                    DrawEffectsDemoWindow(windowRect, themeAsset);
            }
            else
            {
                var modifier = NowEffects.Modifier(NowDeformers.Genie(targetRect, progress, NowEffectDirection.Bottom))
                    .SetId(2001)
                    .SetSubdivision(subdivisions);

                if (_effectsDemoTexture)
                    modifier = modifier.SetRenderToTexture();

                using (modifier.Begin())
                    DrawEffectsDemoWindow(windowRect, themeAsset);
            }
        }

        var labelRect = new NowRect(area.x + 20f, area.yMax - 28f, area.width - 40f, 20f);
        string backend = _effectsDemoTexture ? "RenderTexture backend" : "Mesh backend";
        string effect = _effectsDemoWave ? "wave vertex deformation" : "genie target deformation";

        Now.Text(labelRect)
            .SetFontSize(12f)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw($"{backend}, {effect}, {subdivisions} x {subdivisions} subdivision");

        NowControlState.RequestRepaint();
    }

    static void DrawEffectsDemoBackdrop(NowRect area, NowRect targetRect, float progress, NowThemeAsset themeAsset)
    {
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);

        for (int i = 0; i < 5; ++i)
        {
            float y = area.y + 44f + i * 42f;
            Now.Line(new Vector2(area.x + 8f, y), new Vector2(area.xMax - 8f, y))
                .SetWidth(1f)
                .SetColor(new Color(muted.r, muted.g, muted.b, 0.18f))
                .Draw();
        }

        Now.Rectangle(targetRect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.18f + progress * 0.16f))
            .SetOutline(1f)
            .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.7f))
            .SetRadius(8f)
            .Draw();

        Now.Text(targetRect.Inset(6f, 9f))
            .SetFontSize(10f)
            .SetColor(accent)
            .Draw("target");
    }

    static void DrawEffectsDemoWindow(NowRect rect, NowThemeAsset themeAsset)
    {
        Color surface = themeAsset.GetColor(NowColorToken.Surface, new Color(0.12f, 0.14f, 0.18f, 1f));
        Color panel = themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.16f, 0.18f, 0.23f, 1f));
        Color border = themeAsset.GetColor(NowColorToken.Border, Color.gray);
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = themeAsset.GetColor(NowColorToken.Accent, Color.blue);

        Now.Rectangle(rect)
            .SetColor(surface)
            .SetOutline(1f)
            .SetOutlineColor(border)
            .SetRadius(10f)
            .Draw();

        var title = new NowRect(rect.x, rect.y, rect.width, 40f);
        Now.Rectangle(title)
            .SetColor(panel)
            .SetRadius(new Vector4(10f, 10f, 0f, 0f))
            .Draw();

        for (int i = 0; i < 3; ++i)
        {
            Now.Circle(new Vector2(title.x + 20f + i * 18f, title.y + 20f), 5f)
                .SetColor(i == 0 ? new Color(1f, 0.38f, 0.34f, 1f) : i == 1 ? new Color(1f, 0.74f, 0.25f, 1f) : new Color(0.21f, 0.82f, 0.46f, 1f))
                .Draw();
        }

        Now.Text(new NowRect(title.x + 78f, title.y + 11f, title.width - 96f, 18f))
            .SetFontSize(13f)
            .SetColor(text)
            .Draw("Modifier scope");

        var body = rect.Inset(18f, 56f, 18f, 18f);

        Now.Text(new NowRect(body.x, body.y, body.width, 24f))
            .SetFontSize(20f)
            .SetColor(text)
            .Draw("Normal draw commands");

        Now.Text(new NowRect(body.x, body.y + 34f, body.width, 34f))
            .SetFontSize(12f)
            .SetColor(muted)
            .Draw("Rectangles, text, lines and shapes are captured visually; input remains passive.");

        float cardWidth = (body.width - 18f) / 2f;
        var left = new NowRect(body.x, body.y + 86f, cardWidth, 52f);
        var right = new NowRect(left.xMax + 18f, left.y, cardWidth, 52f);

        Now.Rectangle(left).SetColor(new Color(accent.r, accent.g, accent.b, 0.22f)).SetRadius(8f).Draw();
        Now.Rectangle(right).SetColor(new Color(0.05f, 0.86f, 0.67f, 0.18f)).SetRadius(8f).Draw();

        Now.Text(left.Inset(10f, 9f)).SetFontSize(12f).SetColor(text).Draw("Mesh: crisp glyph quads");
        Now.Text(right.Inset(10f, 9f)).SetFontSize(12f).SetColor(text).Draw("Texture: flattened group");

        Now.Line(new Vector2(body.x, body.yMax - 14f), new Vector2(body.xMax, body.yMax - 14f))
            .SetWidth(3f)
            .SetDash(12f, 8f, Time.time * 28f)
            .SetColor(accent)
            .Draw();
    }

    void DrawLottieDemo(NowThemeAsset themeAsset)
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
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(themeAsset.GetColor(NowColorToken.Accent, Color.blue)).Draw();
            NowLayout.Lottie(_lotties[0]).SetTime(Time.time).SetHeight(56).SetColor(new Color(1f, 1f, 1f, 0.35f)).Draw();
        }
    }

    void DrawLiveDemo(NowThemeAsset themeAsset)
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
            .SetFontSize(12).SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)).Draw();
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
        var theme = NowTheme.themeAsset;
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

        var theme = NowTheme.themeAsset;

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
    NowId _id;
    int _max;

    internal MyRating(int site)
    {
        _site = site;
        _id = default;
        _max = 5;
    }

    public MyRating SetId(NowId id) { _id = id; return this; }

    public MyRating SetMax(int max) { _max = max; return this; }

    public bool Draw(ref int value)
    {
        int id = NowControls.GetControlId(_id, _site);
        return GuideControls.DrawRating(id, ref value, _max);
    }
}
