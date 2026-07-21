using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;
using NowUI;
using NowUI.CodeEditor;
using NowUI.Docking;
using NowUI.Markup;
using NowUI.Markdown;
using NowUI.Sdf;

/// <summary>
/// Browses the package's Documentation~/ folder: a tree menu of pages, the selected
/// page rendered through the markdown extension, and live demo pages for
/// extension/runtime examples. Relative .md links navigate between
/// pages; external links open in the browser.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Docs Browser")]
public class NowDocsExample : NowLayoutGraphic
{
    enum PageId
    {
        Overview,
        Features,
        Api,
        Performance,
        Layout,
        Controls,
        ControlsGallery,
        FilePickerDemo,
        ViewStackDemo,
        CustomControls,
        CustomControlsDemo,
        StylesThemes,
        Mobile,
        EditorGui,
        RichText,
        RichTextDemo,
        Markdown,
        Markup,
        MarkupDemo,
        CodeEditor,
        CodeEditorDemo,
        Lottie,
        LottieDemo,
        Lines,
        LinesDemo,
        Shapes,
        ShapesDemo,
        Glass,
        GlassDemo,
        CustomMaterials,
        CustomMaterialsDemo,
        Effects,
        EffectsDemo,
        ModelPreviews,
        ModelPreviewsDemo,
        WorldSpace,
        RenderPipelines,
        Docking,
        DockingDemo,
        NodeGraph,
        Sdf,
        SdfDemo,
    }

    enum PageKind
    {
        [UsedImplicitly]
        Markdown,
        ControlsDemo,
        ControlsGalleryDemo,
        LottieDemo,
        CodeEditorDemo,
        MarkupDemo,
        RichTextDemo,
        SdfDemo,
        DockingDemo,
        LinesDemo,
        ShapesDemo,
        CustomMaterialsDemo,
        GlassDemo,
        EffectsDemo,
        ModelPreviewsDemo,
        ViewStackDemo,
        FilePickerDemo,
    }

    struct Page
    {
        public string title;
        public string file;
        public string icon;
        public PageKind kind;
    }

    struct NavEntry
    {
        public string title;
        public string icon;
        public int pageIndex;
        public int depth;
        public bool section;
    }

    static readonly Page[] Pages =
    {
        new Page { title = "Overview", file = "README.md", icon = "🏠" },
        new Page { title = "Features", file = "Features.md", icon = "✨" },
        new Page { title = "Public API", file = "API.md", icon = "🧩" },
        new Page { title = "Performance", file = "Performance.md", icon = "🚢" },
        new Page { title = "Layout", file = "Layout.md", icon = "📐" },
        new Page { title = "Controls", file = "Controls.md", icon = "🎛️" },
        new Page { title = "Controls gallery", icon = "🧪", kind = PageKind.ControlsGalleryDemo },
        new Page { title = "File picker demo", icon = "📁", kind = PageKind.FilePickerDemo },
        new Page { title = "View stack demo", icon = "🧭", kind = PageKind.ViewStackDemo },
        new Page { title = "Custom controls", file = "CustomControls.md", icon = "🧰" },
        new Page { title = "Custom controls demo", icon = "🧪", kind = PageKind.ControlsDemo },
        new Page { title = "Styles & themes", file = "StylesAndThemes.md", icon = "🎨" },
        new Page { title = "Mobile", file = "Mobile.md", icon = "📱" },
        new Page { title = "IMGUI", file = "EditorGUI.md", icon = "🖥️" },
        new Page { title = "Rich text", file = "RichText.md", icon = "🔤" },
        new Page { title = "Rich text demo", icon = "🧪", kind = PageKind.RichTextDemo },
        new Page { title = "Markdown", file = "Markdown.md", icon = "📝" },
        new Page { title = "Markup", file = "Markup.md", icon = "🧱" },
        new Page { title = "Markup demo", icon = "🧪", kind = PageKind.MarkupDemo },
        new Page { title = "Code editor", file = "CodeEditor.md", icon = "👩‍💻" },
        new Page { title = "Editor demo", icon = "🧪", kind = PageKind.CodeEditorDemo },
        new Page { title = "Lottie", file = "Lottie.md", icon = "🎞️" },
        new Page { title = "Lottie demo", icon = "🧪", kind = PageKind.LottieDemo },
        new Page { title = "Lines", file = "Lines.md", icon = "➖" },
        new Page { title = "Lines demo", icon = "🧪", kind = PageKind.LinesDemo },
        new Page { title = "Shapes", file = "Shapes.md", icon = "🔺" },
        new Page { title = "Shapes demo", icon = "🧪", kind = PageKind.ShapesDemo },
        new Page { title = "Glass", file = "Glass.md", icon = "🧊" },
        new Page { title = "Glass demo", icon = "🧪", kind = PageKind.GlassDemo },
        new Page { title = "Custom materials", file = "CustomMaterials.md", icon = "🖼️" },
        new Page { title = "Custom material demo", icon = "🧪", kind = PageKind.CustomMaterialsDemo },
        new Page { title = "Effects", file = "Effects.md", icon = "🌊" },
        new Page { title = "Effects demo", icon = "🧪", kind = PageKind.EffectsDemo },
        new Page { title = "Model previews", file = "ModelPreviews.md", icon = "🧍" },
        new Page { title = "Model previews demo", icon = "🧪", kind = PageKind.ModelPreviewsDemo },
        new Page { title = "World space", file = "WorldSpace.md", icon = "🌍" },
        new Page { title = "Render pipelines", file = "RenderPipelines.md", icon = "🎛️" },
        new Page { title = "Docking", file = "Docking.md", icon = "🧲" },
        new Page { title = "Docking demo", icon = "🧪", kind = PageKind.DockingDemo },
        new Page { title = "Node graph", file = "NodeGraph.md", icon = "🕸️" },
        new Page { title = "SDF shapes", file = "SDF.md", icon = "⚫" },
        new Page { title = "SDF demo", icon = "🧪", kind = PageKind.SdfDemo },
    };

    static readonly NavEntry[] Navigation =
    {
        Section("Start", "🚀"),
        Link(PageId.Overview),
        Link(PageId.Features),
        Link(PageId.Api),
        Link(PageId.Performance),

        Section("Core UI", "🧱"),
        Link(PageId.Layout),
        Link(PageId.Controls),
        Link(PageId.ControlsGallery, 1),
        Link(PageId.FilePickerDemo, 1),
        Link(PageId.ViewStackDemo, 1),
        Link(PageId.CustomControls),
        Link(PageId.CustomControlsDemo, 1),
        Link(PageId.StylesThemes),
        Link(PageId.Mobile),
        Link(PageId.EditorGui),

        Section("Text & Content", "✏️"),
        Link(PageId.RichText),
        Link(PageId.RichTextDemo, 1),
        Link(PageId.Markdown),
        Link(PageId.Markup),
        Link(PageId.MarkupDemo, 1),
        Link(PageId.CodeEditor),
        Link(PageId.CodeEditorDemo, 1),
        Link(PageId.Lottie),
        Link(PageId.LottieDemo, 1),

        Section("Rendering", "🎬"),
        Link(PageId.Lines),
        Link(PageId.LinesDemo, 1),
        Link(PageId.Shapes),
        Link(PageId.ShapesDemo, 1),
        Link(PageId.Glass),
        Link(PageId.GlassDemo, 1),
        Link(PageId.CustomMaterials),
        Link(PageId.CustomMaterialsDemo, 1),
        Link(PageId.Effects),
        Link(PageId.EffectsDemo, 1),
        Link(PageId.ModelPreviews),
        Link(PageId.ModelPreviewsDemo, 1),
        Link(PageId.WorldSpace),
        Link(PageId.RenderPipelines),

        Section("Extensions", "🧩"),
        Link(PageId.Docking),
        Link(PageId.DockingDemo, 1),
        Link(PageId.NodeGraph),
        Link(PageId.Sdf),
        Link(PageId.SdfDemo, 1),
    };

    static NavEntry Section(string title, string icon)
    {
        return new NavEntry { title = title, icon = icon, pageIndex = -1, section = true };
    }

    static NavEntry Link(PageId page, int depth = 0)
    {
        return new NavEntry { pageIndex = (int)page, depth = depth };
    }

    static readonly int FrostTintId = Shader.PropertyToID("_FrostTint");
    static readonly int FrostEdgeId = Shader.PropertyToID("_FrostEdge");
    static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");
    static readonly int FrostNoiseScaleId = Shader.PropertyToID("_NoiseScale");
    static readonly int FrostTimeScaleId = Shader.PropertyToID("_TimeScale");

    [SerializeField] NowThemeAsset _themeAsset;
    [SerializeField] NowFontAsset _font;
    [SerializeField] NowLottieAsset[] _lotties;

    int _selected;
    string _docsSearch = "";
    int _rating = 3;
    int _builderRating = 2;
    int _clicks;
    int _gallerySaves;
    bool _galleryShadows = true;
    bool _galleryNotifications = true;
    bool _galleryChipSelected = true;
    bool _galleryAdvanced;
    int _galleryQuality = 1;
    float _galleryVolume = 0.72f;
    float _gallerySpeed = 24f;
    int _galleryLives = 3;
    Vector3 _gallerySpawn = new Vector3(0f, 1.5f, 4f);
    Color _galleryTint = new Color(0.36f, 0.62f, 1f, 1f);
    Gradient _galleryRamp;
    AnimationCurve _galleryFalloff;
    string _galleryName = "";
    string _galleryNotes = "";
    int _galleryResolution = 1;
    int _galleryTab;
    int _galleryCountry;
    int _galleryChannels = 3;
    LayerMask _galleryLayers = 1;
    GalleryQuality _galleryQualityLevel = GalleryQuality.High;
    GalleryChannels _galleryChannelFlags = GalleryChannels.Music | GalleryChannels.Effects;
    System.DateTime _galleryDate;
    System.TimeSpan _galleryAlarm = new System.TimeSpan(7, 30, 0);
    UnityEngine.InputSystem.Key _galleryJumpKey = UnityEngine.InputSystem.Key.Space;
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
    bool _modelPreviewDemoAutoRotate = true;
    bool _modelPreviewDemoPaused;
    bool _modelPreviewDemoSceneLighting;
    bool _modelPreviewDemoPostProcessing;
    int _modelPreviewDemoUpdateMode;
    float _modelPreviewDemoAngle = 18f;
    float _modelPreviewDemoResolutionScale = 0.8f;
    float _modelPreviewDemoWaveAmplitude = 6f;
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
    readonly NowViewStack _viewStackDemo = new NowViewStack();
    DocsViewStackHomeView _viewStackHomeView;
    DocsViewStackDetailsView _viewStackDetailsView;
    DocsViewStackPopupView _viewStackPopupView;
    int _viewStackDemoOpens;
    int _viewStackDemoConfirms;
    float _viewStackDemoProgress = 0.64f;
    bool _viewStackDemoSync = true;
    string _filePickerOpenPath = "";
    string _filePickerSavePath = "";
    string _filePickerDirectoryPath = "";
    bool _filePickerShowHidden;
    int _filePickerChanges;
    readonly NowMarkupState _markupDemoState = new NowMarkupState();
    int _markupDemoClicks;
    string _markupDemoLastEvent = "none";
    Texture2D _sdfDemoTexture;
    Texture2D _customMaterialTexture;
    Material _customMaterialCanvas;
    NowModelPreviewDemoRig _modelPreviewDemoRig;
    string _modelPreviewDemoError;
    readonly NowSdfGraph _sdfDemoIdle = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoActive = NowSdf.Graph();
    static readonly string[] GlassQualityLabels = { "Auto", "Fast", "Balanced", "High", "Ultra" };
    static readonly string[] ModelPreviewUpdateLabels = { "When dirty", "Every frame", "Manual" };
    readonly NowSdfGraph _sdfDemoGlow = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoStreaks = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoOrbit = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoTextIdle = NowSdf.Graph();
    readonly NowSdfGraph _sdfDemoTextActive = NowSdf.Graph();
    readonly Vector2[] _shapeDemoPolygon = new Vector2[6];
    readonly Dictionary<string, string> _docs = new Dictionary<string, string>();
    static bool _documentationRootResolved;
    static string _documentationRoot;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureModelPreviewDemo();
    }

    protected override void OnDisable()
    {
        ReleaseModelPreviewDemo();
        base.OnDisable();
    }

    void EnsureModelPreviewDemo()
    {
        if (_modelPreviewDemoRig != null || !string.IsNullOrEmpty(_modelPreviewDemoError))
            return;

        try
        {
            _modelPreviewDemoRig = new NowModelPreviewDemoRig();
            _modelPreviewDemoRig.preview
                .SetMaxResolution(512)
                .SetBackground(new Color(0.012f, 0.028f, 0.052f, 1f));
        }
        catch (System.Exception exception)
        {
            _modelPreviewDemoError = exception.Message;
            Debug.LogException(exception, this);
        }
    }

    void ReleaseModelPreviewDemo()
    {
        _modelPreviewDemoRig?.Dispose();
        _modelPreviewDemoRig = null;
    }

    internal void ConfigureModelPreviewsDemoHarness(NowThemeAsset theme, NowFontAsset font)
    {
        _themeAsset = theme;
        _font = font;
        _selected = (int)PageId.ModelPreviewsDemo;
        _modelPreviewDemoAutoRotate = false;
        _modelPreviewDemoPaused = false;
        _modelPreviewDemoSceneLighting = false;
        _modelPreviewDemoPostProcessing = false;
        _modelPreviewDemoUpdateMode = (int)NowModelPreviewUpdateMode.WhenDirty;
        _modelPreviewDemoAngle = 18f;
        _modelPreviewDemoResolutionScale = 0.8f;
        _modelPreviewDemoWaveAmplitude = 6f;
        EnsureModelPreviewDemo();
    }

    internal bool RenderModelPreviewsDemoNowForHarness()
    {
        return _modelPreviewDemoRig?.preview.RenderNow() ?? false;
    }

    string LoadDoc(string file)
    {
        if (_docs.TryGetValue(file, out string text))
            return text;

        try
        {
            string root = ResolveDocumentationRoot();
            if (string.IsNullOrEmpty(root))
                throw new DirectoryNotFoundException("Could not locate the installed NowUI Documentation~ directory.");

            text = File.ReadAllText(Path.Combine(root, file));
        }
        catch (System.Exception exception)
        {
            text = $"# {file}\n\nCould not read the installed package's" +
                $" `Documentation~/{file}` file:\n\n```\n{exception.Message}\n```";
        }

        _docs[file] = text;
        return text;
    }

    static string ResolveDocumentationRoot()
    {
        if (_documentationRootResolved)
            return _documentationRoot;

        _documentationRootResolved = true;

#if UNITY_EDITOR
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Now).Assembly);
        if (package != null)
        {
            string resolved = Path.Combine(package.resolvedPath, "Documentation~");
            if (Directory.Exists(resolved))
                return _documentationRoot = resolved;
        }
#endif

        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] directCandidates =
            {
                Path.Combine(projectRoot, "Assets", "NowUI", "Documentation~"),
                Path.Combine(projectRoot, "Packages", "com.blenminer.nowui", "Documentation~"),
            };

            for (int i = 0; i < directCandidates.Length; ++i)
            {
                if (Directory.Exists(directCandidates[i]))
                    return _documentationRoot = directCandidates[i];
            }

            string cacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
            if (Directory.Exists(cacheRoot))
            {
                string[] packageRoots = Directory.GetDirectories(cacheRoot, "com.blenminer.nowui@*");
                for (int i = 0; i < packageRoots.Length; ++i)
                {
                    string candidate = Path.Combine(packageRoots[i], "Documentation~");
                    if (Directory.Exists(candidate))
                        return _documentationRoot = candidate;
                }
            }
        }
        catch (System.Exception exception) when (
            exception is IOException ||
            exception is System.UnauthorizedAccessException ||
            exception is System.ArgumentException ||
            exception is System.NotSupportedException)
        {
        }

        return null;
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

    void DrawDocsSidebarHeader(NowThemeAsset theme, NowRect rect)
    {
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = theme.GetColor(NowColorToken.Accent, new Color(0.10f, 0.45f, 0.95f, 1f));
        Color accentText = theme.GetColor(NowColorToken.AccentText, Color.white);

        using (NowLayout.Area(610001, rect, spacing: 9f, padding: 0f, alignItems: NowLayoutAlign.Start))
        {
            using (NowLayout.Horizontal(height: 30f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 9f))
            {
                NowRect badge = NowLayout.ReserveRect(30f, 30f);
                Now.Rectangle(badge)
                    .SetColor(accent)
                    .SetRadius(9f)
                    .Draw();
                DrawCenteredText(theme, badge, "N", 16f, accentText, bold: true);

                using (NowLayout.Vertical(spacing: 0f, stretchWidth: true))
                {
                    NowLayout.Label("NowUI")
                        .SetFontSize(15f)
                        .SetHeight(17f)
                        .SetBold()
                        .SetColor(text)
                        .Draw();

                    NowLayout.Label("Immediate-mode UI docs")
                        .SetFontSize(10f)
                        .SetHeight(13f)
                        .SetColor(muted)
                        .Draw();
                }
            }

            NowLayout.TextField("docs-search")
                .SetPlaceholder("Search docs...")
                .SetStretchWidth()
                .Draw(ref _docsSearch);
        }
    }

    /// <summary>Draws a single string centered inside a rect, outside any layout group.</summary>
    static void DrawCenteredText(NowThemeAsset theme, NowRect rect, string value, float fontSize, Color color, bool bold = false)
    {
        var text = theme.Text(default, NowTextStyle.Body)
            .SetFontSize(fontSize)
            .SetColor(color);

        if (bold)
            text = text.SetBold();

        Vector2 size = text.Measure(value);
        text.rect = new NowRect(
            rect.x + (rect.width - size.x) * 0.5f,
            rect.y + (rect.height - size.y) * 0.5f,
            size.x + 1f,
            size.y + 1f);
        text.Draw(value);
    }

    void DrawDocsNavigation(NowThemeAsset theme, NowRect rect)
    {
        using (NowLayout.Area(rect))
        using (NowLayout.ScrollView("docs-menu").Begin())
        using (NowLayout.Vertical(spacing: 3f, padding: 2f))
        {
            bool drewAny = false;

            for (int i = 0; i < Navigation.Length; ++i)
            {
                var entry = Navigation[i];

                if (entry.section)
                {
                    if (!SectionHasVisiblePage(i))
                        continue;

                    if (drewAny)
                        NowLayout.Space(8f);

                    DrawDocsNavSection(theme, entry);
                    drewAny = true;
                    continue;
                }

                if (!PageMatchesSearch(entry.pageIndex))
                    continue;

                DrawDocsNavPage(entry);
                drewAny = true;
            }

            if (!drewAny)
            {
                NowLayout.Space(8f);

                NowLayout.Label("No matching pages")
                    .SetFontSize(12f)
                    .SetBold()
                    .SetColor(theme.GetColor(NowColorToken.Text, Color.white))
                    .Draw();

                NowLayout.Label($"Nothing matches \"{_docsSearch.Trim()}\".")
                    .SetFontSize(11f)
                    .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
                    .SetStretchWidth()
                    .Draw();
            }
        }
    }

    void DrawDocsNavSection(NowThemeAsset theme, NavEntry entry)
    {
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        using (NowLayout.Horizontal(height: 20f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 7f))
        {
            NowLayout.Label(entry.icon)
                .SetFontSize(9f)
                .SetColor(Color.white)
                .Draw();

            NowLayout.Label(entry.title.ToUpperInvariant())
                .SetFontSize(10f)
                .SetBold()
                .SetColor(muted)
                .Draw();

            NowRect rule = NowLayout.ReserveRect(height: 1f, stretchWidth: true);
            Now.Rectangle(rule)
                .SetColor(new Color(muted.r, muted.g, muted.b, 0.16f))
                .Draw();
        }
    }

    void DrawDocsNavPage(NavEntry entry)
    {
        int pageIndex = Mathf.Clamp(entry.pageIndex, 0, Pages.Length - 1);
        var page = Pages[pageIndex];
        bool selected = pageIndex == _selected;
        var theme = NowTheme.themeAsset;
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = theme.GetColor(NowColorToken.Accent, new Color(0.10f, 0.45f, 0.95f, 1f));
        Color accentText = theme.GetColor(NowColorToken.AccentText, Color.white);
        NowRect rect = NowLayout.ReserveRect(height: 28f, stretchWidth: true);
        NowRect row = rect.Inset(1f, 0f);
        int id = NowControls.GetControlId($"doc-{pageIndex}");
        var interaction = NowControls.Interact(id, row, out bool focused, out bool submitted);

        if (interaction.clicked || submitted)
            _selected = pageIndex;

        float hoverT = NowControlState.Transition(interaction, "hover", interaction.hovered || focused, 14f);

        if (selected)
        {
            Now.Rectangle(row)
                .SetColor(theme.GetColor(NowColorToken.AccentMuted, new Color(accent.r, accent.g, accent.b, 0.18f)))
                .SetRadius(7f)
                .Draw();

            Now.Rectangle(new NowRect(row.x + 3f, row.y + 7f, 3f, row.height - 14f))
                .SetColor(accent)
                .SetRadius(1.5f)
                .Draw();
        }
        else if (hoverT > 0.004f)
        {
            Now.Rectangle(row)
                .SetColor(new Color(accent.r, accent.g, accent.b, 0.09f * hoverT))
                .SetRadius(7f)
                .Draw();
        }

        float indent = 8f + entry.depth * 18f;
        float badgeX = row.x + indent;
        float centerY = row.y + row.height * 0.5f;

        if (entry.depth > 0)
        {
            Color branch = new Color(muted.r, muted.g, muted.b, selected ? 0.38f : 0.26f);
            float lineX = row.x + indent - 11f;
            Now.Line(new Vector2(lineX, row.y + 4f), new Vector2(lineX, row.yMax - 4f))
                .SetColor(branch)
                .SetWidth(1f)
                .Draw();
            Now.Line(new Vector2(lineX, centerY), new Vector2(badgeX - 4f, centerY))
                .SetColor(branch)
                .SetWidth(1f)
                .Draw();
        }

        var badgeRect = new NowRect(badgeX, row.y + 3f, 24f, 22f);

        Now.Text(badgeRect)
            .SetFontSize(14f)
            .SetColor(Color.white)
            .Draw(page.icon);

        var title = Now.Text(new NowRect(badgeRect.xMax + 8f, row.y + 5f, row.xMax - badgeRect.xMax - 14f, row.height - 8f))
            .SetFontSize(12f)
            .SetColor(selected ? text : Color.Lerp(text, accentText, hoverT * 0.2f));

        if (selected)
            title = title.SetBold();

        title.Draw(page.title);
    }

    bool SectionHasVisiblePage(int sectionIndex)
    {
        if (string.IsNullOrWhiteSpace(_docsSearch))
            return true;

        for (int i = sectionIndex + 1; i < Navigation.Length; ++i)
        {
            var entry = Navigation[i];

            if (entry.section)
                return false;

            if (PageMatchesSearch(entry.pageIndex))
                return true;
        }

        return false;
    }

    bool PageMatchesSearch(int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(_docsSearch))
            return true;

        if (pageIndex < 0 || pageIndex >= Pages.Length)
            return false;

        string query = _docsSearch.Trim();
        var page = Pages[pageIndex];

        return ContainsSearch(page.title, query) ||
            ContainsSearch(page.file, query);
    }

    static bool ContainsSearch(string value, string query)
    {
        return !string.IsNullOrEmpty(value) &&
            value.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    protected override void DrawNowUI(NowRect rect)
    {
        if (!_font)
            return;

        using var _ = NowTheme.Scope(_themeAsset);

        Now.defaultFont = _font;
        var theme = NowTheme.themeAsset;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        theme.Rectangle(bounds, NowRectangleStyle.Surface).Draw();

        var menuRect = new NowRect(bounds.x + 12, bounds.y + 12, 292, bounds.height - 24);
        theme.Rectangle(menuRect, NowRectangleStyle.Elevated).Draw();

        var menuInner = menuRect.Inset(12f);
        var menuTitleRect = new NowRect(menuInner.x, menuInner.y, menuInner.width, 76f);
        var menuListRect = new NowRect(menuInner.x, menuTitleRect.yMax + 10f, menuInner.width, menuInner.yMax - menuTitleRect.yMax - 10f);

        Color separator = theme.GetColor(NowColorToken.Border, Color.gray);
        Now.Rectangle(new NowRect(menuInner.x, menuTitleRect.yMax + 4f, menuInner.width, 1f))
            .SetColor(new Color(separator.r, separator.g, separator.b, 0.45f))
            .Draw();

        DrawDocsSidebarHeader(theme, menuTitleRect);
        DrawDocsNavigation(theme, menuListRect);

        float contentAvailable = bounds.xMax - menuRect.xMax - 24f;
        float scrollGutter = theme.controlStyles.scrollbarWidth + theme.controlStyles.scrollbarPadding;
        float contentWidth = Mathf.Max(1f, Mathf.Min(contentAvailable - scrollGutter, 940f));
        var contentRect = new NowRect(menuRect.xMax + 12f, bounds.y + 12, contentAvailable, bounds.height - 24);

        using (NowLayout.Area(contentRect))
        using (NowLayout.ScrollView($"docs-scroll-{_selected}").Begin())
        using (NowLayout.Horizontal(stretchWidth: true))
        {
            NowLayout.FlexibleSpace();

            using (NowLayout.Vertical(width: contentWidth, spacing: 0f))
            {
                DrawDocsBreadcrumb(theme);
                DrawDocsPageContent(theme);
                DrawDocsPager(theme);
            }

            NowLayout.FlexibleSpace();
        }
    }

    void DrawDocsPageContent(NowThemeAsset theme)
    {
        switch (Pages[_selected].kind)
        {
            case PageKind.ControlsDemo:
                DrawLiveDemo(theme);
                break;

            case PageKind.ControlsGalleryDemo:
                DrawControlsGalleryDemo(theme);
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

            case PageKind.MarkupDemo:
                DrawMarkupDemo(theme);
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

            case PageKind.ModelPreviewsDemo:
                DrawModelPreviewsDemo(theme);
                break;

            case PageKind.ViewStackDemo:
                DrawViewStackDemo(theme);
                break;

            case PageKind.FilePickerDemo:
                DrawFilePickerDemo(theme);
                break;

            default:
                var result = NowMarkdown.Document(LoadDoc(Pages[_selected].file)).Draw();

                if (result.clickedLink != null)
                    NavigateLink(result.clickedLink);
                break;
        }
    }

    /// <summary>Draws the muted "Section / Page" trail above the page content.</summary>
    void DrawDocsBreadcrumb(NowThemeAsset theme)
    {
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);
        Color accent = theme.GetColor(NowColorToken.Accent, Color.blue);

        using (NowLayout.Horizontal(height: 16f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 6f))
        {
            NowLayout.Label(SectionTitleFor(_selected).ToUpperInvariant())
                .SetFontSize(10f)
                .SetBold()
                .SetColor(muted)
                .Draw();

            NowLayout.Label("/")
                .SetFontSize(10f)
                .SetColor(new Color(muted.r, muted.g, muted.b, 0.55f))
                .Draw();

            NowLayout.Label(Pages[_selected].title.ToUpperInvariant())
                .SetFontSize(10f)
                .SetBold()
                .SetColor(accent)
                .Draw();
        }

        NowLayout.Space(4f);
    }

    /// <summary>Draws previous/next page cards at the bottom of the content column.</summary>
    void DrawDocsPager(NowThemeAsset theme)
    {
        int position = NavigationPositionOf(_selected);
        int previous = AdjacentPage(position, -1);
        int next = AdjacentPage(position, +1);

        if (previous < 0 && next < 0)
            return;

        Color border = theme.GetColor(NowColorToken.Border, Color.gray);

        NowLayout.Space(28f);
        NowRect rule = NowLayout.ReserveRect(height: 1f, stretchWidth: true);
        Now.Rectangle(rule)
            .SetColor(new Color(border.r, border.g, border.b, 0.45f))
            .Draw();
        NowLayout.Space(12f);

        var rect = NowLayout.ReserveRect(height: 58f, stretchWidth: true);
        float half = (rect.width - 12f) * 0.5f;

        if (previous >= 0)
            DrawDocsPagerCard(theme, new NowRect(rect.x, rect.y, half, rect.height), previous, forward: false);

        if (next >= 0)
            DrawDocsPagerCard(theme, new NowRect(rect.xMax - half, rect.y, half, rect.height), next, forward: true);

        NowLayout.Space(8f);
    }

    void DrawDocsPagerCard(NowThemeAsset theme, NowRect rect, int pageIndex, bool forward)
    {
        var page = Pages[pageIndex];
        var interaction = NowControls.Interact(forward ? "docs-pager-next" : "docs-pager-prev", rect, out bool focused, out bool submitted);

        if (interaction.clicked || submitted)
            _selected = pageIndex;

        float hoverT = NowControlState.Transition(interaction, "hover", interaction.hovered || focused, 14f);
        Color accent = theme.GetColor(NowColorToken.Accent, Color.blue);
        Color border = theme.GetColor(NowColorToken.Border, Color.gray);
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        Now.Rectangle(rect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.04f + hoverT * 0.07f))
            .SetOutline(1f)
            .SetOutlineColor(Color.Lerp(new Color(border.r, border.g, border.b, 0.8f), accent, hoverT))
            .SetRadius(9f)
            .Draw();

        var mask = rect.Inset(12f, 0f);
        DrawPagerCardLine(theme, rect, mask, forward, forward ? "NEXT" : "PREVIOUS", 9f, muted, bold: true, y: rect.y + 11f);
        DrawPagerCardLine(theme, rect, mask, forward, forward ? $"{page.icon} {page.title}  →" : $"←  {page.icon} {page.title}", 13f,
            Color.Lerp(text, accent, hoverT * 0.6f), bold: false, y: rect.y + 28f);
    }

    static void DrawPagerCardLine(NowThemeAsset theme, NowRect rect, NowRect mask, bool alignRight, string value, float fontSize, Color color, bool bold, float y)
    {
        var line = theme.Text(default, NowTextStyle.Body)
            .SetFontSize(fontSize)
            .SetColor(color);

        if (bold)
            line = line.SetBold();

        Vector2 size = line.Measure(value);
        float x = alignRight ? rect.xMax - 14f - size.x : rect.x + 14f;
        line.rect = new NowRect(x, y, size.x + 2f, size.y + 2f);
        line.SetMask(mask).Draw(value);
    }

    /// <summary>Title of the navigation section that contains the given page.</summary>
    static string SectionTitleFor(int pageIndex)
    {
        string section = "";

        for (int i = 0; i < Navigation.Length; ++i)
        {
            var entry = Navigation[i];

            if (entry.section)
                section = entry.title;
            else if (entry.pageIndex == pageIndex)
                return section;
        }

        return section;
    }

    static int NavigationPositionOf(int pageIndex)
    {
        for (int i = 0; i < Navigation.Length; ++i)
        {
            if (!Navigation[i].section && Navigation[i].pageIndex == pageIndex)
                return i;
        }

        return -1;
    }

    /// <summary>Page index of the nearest non-section navigation entry in the given direction, or -1.</summary>
    static int AdjacentPage(int navigationIndex, int direction)
    {
        if (navigationIndex < 0)
            return -1;

        for (int i = navigationIndex + direction; i >= 0 && i < Navigation.Length; i += direction)
        {
            if (!Navigation[i].section)
                return Navigation[i].pageIndex;
        }

        return -1;
    }

    const string JsonSample = "{\n  \"name\": \"NowUI\",\n  \"version\": \"0.1.0\",\n  \"mobileForward\": true,\n  \"platforms\": [\"windows\", \"android\", \"ios\", \"webgl\"],\n  \"dependencies\": null,\n  \"stars\": 5\n}";

    const string MarkdownSample = "# Editing markdown\n\nThis is **markdown source** with **live highlighting** — headings, *emphasis*,\n`inline code`, [links](https://example.com) and fenced blocks:\n\n```json\n{ \"fences\": \"highlight as JSON\" }\n```\n\n- toggle the preview to render it\n- same selection, undo and clipboard as the JSON editor\n";

    const string MarkupEditorSample =
        "<style>\n" +
        "  .panel { padding: 14; gap: 9; rect-style: Muted; radius: 8; stretch: 1; }\n" +
        "  .row { gap: 8; align-items: center; }\n" +
        "</style>\n" +
        "<column class=\"panel\">\n" +
        "  <h3>Markup editor preview</h3>\n" +
        "  <text color=\"#8fa4bd\">Type tags in the editor, then inspect the live NowUI render.</text>\n" +
        "  <hr />\n" +
        "  <row class=\"row\">\n" +
        "    <text style=\"width: 74\">Name</text>\n" +
        "    <textfield id=\"editor-name\" state=\"name\" placeholder=\"Display name\" stretch=\"1\" />\n" +
        "  </row>\n" +
        "  <row class=\"row\">\n" +
        "    <text style=\"width: 74\">Volume</text>\n" +
        "    <slider id=\"editor-volume\" state=\"volume\" min=\"0\" max=\"1\" step=\"0.05\" stretch=\"1\" />\n" +
        "  </row>\n" +
        "  <progress state=\"volume\" />\n" +
        "  <row class=\"row\">\n" +
        "    <badge>Live</badge>\n" +
        "    <button id=\"editor-save\" on-click=\"emit(save)\">Save</button>\n" +
        "  </row>\n" +
        "</column>";

    const string MarkupDemoSample =
        "<style>\n" +
        "  .card { padding: 16; gap: 10; rect-style: Surface; radius: 10; stretch: 1; }\n" +
        "  .row { gap: 8; align-items: center; }\n" +
        "  button.primary { variant: Accent; width: 120; }\n" +
        "  button.secondary { variant: Outline; width: 120; }\n" +
        "</style>\n" +
        "<column class=\"card\">\n" +
        "  <h3>AI-authored panel</h3>\n" +
        "  <text color=\"#8fa4bd\">This UI is rendered from NowUI markup, not C# controls.</text>\n" +
        "  <hr/>\n" +
        "  <row class=\"row\">\n" +
        "    <text style=\"width: 80\">Name</text>\n" +
        "    <textfield id=\"profile-name\" state=\"name\" placeholder=\"Display name\" stretch=\"1\" />\n" +
        "  </row>\n" +
        "  <row class=\"row\">\n" +
        "    <text style=\"width: 80\">Volume</text>\n" +
        "    <slider id=\"volume\" state=\"volume\" min=\"0\" max=\"1\" step=\"0.05\" stretch=\"1\" />\n" +
        "  </row>\n" +
        "  <progress state=\"volume\" />\n" +
        "  <row class=\"row\">\n" +
        "    <radio group=\"quality\" value=\"0\" checked=\"true\">Low</radio>\n" +
        "    <radio group=\"quality\" value=\"1\">Medium</radio>\n" +
        "    <radio group=\"quality\" value=\"2\">High</radio>\n" +
        "  </row>\n" +
        "  <switch id=\"advanced-toggle\" state=\"advanced\">Advanced settings</switch>\n" +
        "  <details id=\"advanced-details\" state=\"advanced\" summary=\"Advanced\">\n" +
        "    <column gap=\"8\" padding=\"10\" rect-style=\"Muted\" radius=\"8\">\n" +
        "      <text><b>Advanced</b> is just a state key.</text>\n" +
        "      <row class=\"row\">\n" +
        "        <button id=\"prev-photo\" class=\"secondary\" on-click=\"prev(photo,3)\">Previous</button>\n" +
        "        <button id=\"next-photo\" class=\"secondary\" on-click=\"next(photo,3)\">Next</button>\n" +
        "      </row>\n" +
        "    </column>\n" +
        "  </details>\n" +
        "  <gallery id=\"photos\" index=\"photo\" controls=\"true\" gap=\"8\" padding=\"10\" rect-style=\"Muted\" radius=\"8\">\n" +
        "    <slide><text>Gallery item <b>one</b></text></slide>\n" +
        "    <slide><text>Gallery item <b>two</b></text></slide>\n" +
        "    <slide><text>Gallery item <b>three</b></text></slide>\n" +
        "  </gallery>\n" +
        "  <row class=\"row\">\n" +
        "    <flex />\n" +
        "    <button id=\"save-profile\" class=\"primary\" on-click=\"emit(save)\">Save</button>\n" +
        "  </row>\n" +
        "</column>";

    string _jsonText = JsonSample;
    string _markdownText = MarkdownSample;
    string _markupEditorText = MarkupEditorSample;
    bool _markdownPreview;
    readonly NowMarkupState _markupEditorState = new NowMarkupState();
    bool _markupEditorStateReady;

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

        var panel = NowLayout.ReserveRect(height: 430f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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
            " squiggles (hover them, click the status error to jump), bracket/quote auto-close, language" +
            " completions, Enter auto-indent, Tab indent/dedent, smart Home, undo/redo, line numbers." +
            " Break the JSON below and watch the squiggle and status bar.\n\n## JSON").Draw();

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

        NowMarkdown.Document("## Markup\n\nThe markup profile highlights tags, attributes, comments, entities" +
            " and style blocks. Type `<column>` or `<button>` and the editor completes the matching close tag;" +
            " type `/` before a generated `>` to make a self-closing tag. The preview below is rendered through" +
            " `NowMarkup` in the same NowUI docs scene.").Draw();

        var markup = NowCode.Editor(NowMarkupCodeLanguage.instance, "demo-markup").SetHeight(300).Draw(ref _markupEditorText);

        if (!markup.isValid)
            NowLayout.Label("Markup has a balancing issue — check the editor status bar.")
                .SetFontSize(11)
                .SetColor(new Color(0.86f, 0.24f, 0.24f))
                .Draw();

        NowMarkdown.Document("### Rendered preview").Draw();
        EnsureMarkupEditorState();
        NowMarkup.Document(_markupEditorText).Draw(_markupEditorState);
    }

    void EnsureMarkupEditorState()
    {
        if (_markupEditorStateReady)
            return;

        _markupEditorStateReady = true;
        _markupEditorState.SetString("name", "NowUI");
        _markupEditorState.SetFloat("volume", 0.65f);
    }

    void DrawSdfDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# SDF demo\n\nHover the specimen to steer the light, warp, and contour field. Click it to switch into cutaway mode.").Draw();

        DrawSdfPlaygroundPanel(themeAsset);
        NowControlState.RequestRepaint();
    }

    void DrawSdfPlaygroundPanel(NowThemeAsset themeAsset)
    {
        var panel = ReserveSdfPanel(themeAsset, 342f);

        if (panel.isEmpty)
            return;

        var interaction = NowControls.Interact("docs-sdf-playground", panel, out bool focused, out bool submitted);

        if (interaction.clicked || submitted)
            _sdfDemoLocked = !_sdfDemoLocked;

        var scene = panel.Inset(18f, 16f, 18f, 40f);
        float hoverT = NowControlState.Transition(interaction, "hover", interaction.hovered || focused, 8f);
        float pressT = NowControlState.Transition(interaction, "press", interaction.held, 18f);
        float lockT = NowControlState.Transition(interaction, "lock", _sdfDemoLocked, 7f);
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
        float morphT = Mathf.Clamp01(lockT + pressT * 0.18f + hoverT * 0.06f);
        float textMorphT = Mathf.Clamp01(lockT + pressT * 0.15f);
        var sdfFont = _font != null ? _font : Now.font;
        const string idleText = "SDF";
        const string activeText = "CUT";
        float textSize = Mathf.Min(86f, h * 0.30f);
        Vector2 idleSize = sdfFont != null
            ? sdfFont.MeasureText(idleText, textSize, NowFontStyle.Bold)
            : new Vector2(w * 0.30f, textSize);
        Vector2 activeSize = sdfFont != null
            ? sdfFont.MeasureText(activeText, textSize, NowFontStyle.Bold)
            : new Vector2(w * 0.30f, textSize);
        var textPush = new Vector2((steerX - 0.5f) * hoverT * 8f, (steerY - 0.5f) * hoverT * 5f);
        var idlePosition = new Vector2(w * 0.5f - idleSize.x * 0.5f, h * 0.50f - idleSize.y * 0.52f) + textPush;
        var activePosition = new Vector2(w * 0.5f - activeSize.x * 0.5f, h * 0.50f - activeSize.y * 0.52f) + textPush;

        var aura = _sdfDemoGlow.Clear()
            .SetColor(new Color(0.08f, 0.55f, 1f, 0.12f + hoverT * 0.08f))
            .UseColor()
            .Ellipse(new NowRect(w * 0.12f, h * 0.17f, w * 0.76f, h * 0.66f))
            .SetColor(new Color(0.9f, 0.22f, 0.56f, 0.10f + lockT * 0.12f))
            .UseColor()
            .SmoothUnion(h * 0.10f)
            .Circle(new Vector2(w * (0.32f + steerX * 0.16f), h * (0.34f + steerY * 0.06f)), h * 0.22f);

        var idle = _sdfDemoIdle.Clear()
            .SetColor(Color.Lerp(new Color(0.05f, 0.75f, 0.65f, 1f), new Color(0.10f, 0.55f, 1f, 1f), hoverT * 0.55f))
            .UseColor()
            .Circle(new Vector2(w * (0.40f + steerX * 0.035f), h * (0.50f - steerY * 0.025f)), h * 0.23f)
            .SetColor(new Color(0.18f, 0.38f, 1f, 1f))
            .UseColor()
            .SmoothUnion(h * 0.08f)
            .Circle(new Vector2(w * 0.56f, h * 0.45f), h * 0.20f)
            .SetColor(new Color(1f, 0.60f, 0.18f, 1f))
            .UseColor()
            .SmoothUnion(h * 0.06f)
            .Capsule(new NowRect(w * 0.37f, h * 0.58f, w * 0.30f, h * 0.12f));

        var active = _sdfDemoActive.Clear()
            .SetColor(Color.Lerp(new Color(0.22f, 0.42f, 1f, 1f), new Color(0.88f, 0.18f, 0.54f, 1f), lockT))
            .UseColor()
            .RoundedBox(new NowRect(w * 0.28f, h * 0.29f, w * 0.44f, h * 0.40f), h * 0.14f)
            .SetColor(new Color(0.08f, 0.92f, 0.72f, 1f))
            .UseColor()
            .SmoothUnion(h * 0.075f)
            .Circle(new Vector2(w * (0.62f - steerX * 0.04f), h * (0.40f + steerY * 0.04f)), h * 0.17f)
            .SetColor(new Color(1f, 0.74f, 0.18f, 1f))
            .UseColor()
            .SmoothUnion(h * 0.05f)
            .Capsule(new NowRect(w * 0.32f, h * 0.61f, w * 0.38f, h * 0.09f))
            .SmoothSubtract(h * 0.035f)
            .Circle(cursor, h * (0.15f + pressT * 0.03f));

        var streaks = _sdfDemoStreaks.Clear()
            .SetColor(new Color(1f, 1f, 1f, 0.22f + hoverT * 0.12f))
            .UseColor()
            .Capsule(new NowRect(w * (0.33f + steerX * 0.06f), h * 0.36f, w * 0.28f, h * 0.045f))
            .SetColor(new Color(1f, 0.96f, 0.64f, 0.20f + lockT * 0.12f))
            .UseColor()
            .SmoothUnion(8f)
            .Capsule(new NowRect(w * 0.42f, h * (0.54f + steerY * 0.06f), w * 0.24f, h * 0.04f));

        Vector2 orbitCenter = new Vector2(w * (0.22f + Mathf.Sin(Time.time * 1.1f) * 0.025f), h * 0.72f);
        var orbit = _sdfDemoOrbit.Clear()
            .SetColor(new Color(1f, 0.78f, 0.2f, 0.88f))
            .UseColor()
            .Circle(orbitCenter, h * 0.055f)
            .Subtract()
            .Circle(orbitCenter, h * 0.038f)
            .SetColor(new Color(0.05f, 0.9f, 0.74f, 0.78f))
            .UseColor()
            .SmoothUnion(6f)
            .Circle(new Vector2(w * (0.78f + Mathf.Cos(Time.time * 1.2f) * 0.025f), h * 0.25f), h * 0.035f);

        var textIdle = _sdfDemoTextIdle.Clear()
            .SetColor(new Color(1f, 1f, 1f, 0.76f))
            .Text(idlePosition + new Vector2(1f, -1f), idleText, sdfFont, textSize, NowFontStyle.Bold);
        var textActive = _sdfDemoTextActive.Clear()
            .SetColor(new Color(1f, 0.92f, 0.72f, 0.82f))
            .Text(activePosition + new Vector2(1f, -1f), activeText, sdfFont, textSize, NowFontStyle.Bold);

        NowSdf.Scene(scene, "docs-sdf-playground-aura")
            .SetFeather(2.5f)
            .SetWarp(1.2f + hoverT * 1.3f, 80f, 0.08f, 2.4f)
            .Graph(aura)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-main")
            .SetShadow(new Vector2(6f, 9f), 16f, new Color(0f, 0f, 0f, 0.18f), 1f)
            .SetGlow(18f + hoverT * 10f, Color.Lerp(new Color(0.05f, 0.62f, 1f, 0.18f), new Color(1f, 0.34f, 0.72f, 0.22f), lockT), 1.5f)
            .SetOutline(1.6f, new Color(0.02f, 0.035f, 0.08f, 0.38f), 0.8f)
            .SetInnerShadow(new Vector2(-3f - steerX * 3f, -4f - steerY * 2f), 9f, new Color(0f, 0f, 0f, 0.10f))
            .SetEmboss(new Vector2(0.35f - steerX, 0.22f - steerY), 0.12f + hoverT * 0.04f, 8f)
            .SetContours(18f - hoverT * 5f, 0.8f, new Color(1f, 1f, 1f, 0.04f + hoverT * 0.04f), Time.time * (4f + hoverT * 6f), 2)
            .SetContourMask(cursor, h * (0.24f + hoverT * 0.10f), h * 0.08f)
            .SetWarp(1.0f + hoverT * 2.2f, Mathf.Lerp(72f, 48f, hoverT), 0.12f + hoverT * 0.15f, 4.7f)
            .Morph(idle, active, morphT)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-ridges")
            .Morph(idle, active, morphT)
            .SmoothIntersect(7f)
            .Graph(streaks)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-text-sheen")
            .SetShadow(new Vector2(2f, 3f), 7f, new Color(0f, 0f, 0f, 0.28f), 1f)
            .SetGlow(7f, new Color(1f, 1f, 1f, 0.12f), 1.4f)
            .Morph(textIdle, textActive, textMorphT)
            .Draw();

        NowSdf.Scene(scene, "docs-sdf-playground-orbit")
            .SetGlow(12f, new Color(1f, 0.76f, 0.22f, 0.22f), 1.2f)
            .SetWarp(0.8f, 38f, 0.18f, 8.2f)
            .Graph(orbit)
            .Draw();

        var textureRect = new NowRect(scene.x + scene.width * 0.075f, scene.y + scene.height * 0.74f, scene.width * 0.22f, scene.height * 0.15f);
        NowSdf.Scene(textureRect, "docs-sdf-playground-texture")
            .SetTexture(GetSdfDemoTexture())
            .SetShadow(new Vector2(4f, 5f), 10f, new Color(0f, 0f, 0f, 0.22f), 1f)
            .SetOutline(1.5f, new Color(1f, 1f, 1f, 0.35f), 0.5f)
            .RoundedBox(new NowRect(0f, 0f, textureRect.width, textureRect.height), textureRect.height * 0.32f)
            .SmoothSubtract(6f)
            .Circle(new Vector2(textureRect.width * (0.33f + steerX * 0.30f), textureRect.height * 0.5f), textureRect.height * 0.26f)
            .Draw();

        DrawSdfPanelLabel(
            themeAsset,
            new NowRect(panel.x, panel.y + panel.height - 29f, panel.width, 22f),
            _sdfDemoLocked ? "cutaway specimen" : hoverT > 0.5f ? "live field lighting" : "organic SDF specimen",
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
        var panel = NowLayout.ReserveRect(height: height, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();
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
        ReleaseModelPreviewDemo();

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

    void DrawMarkupDemo(NowThemeAsset themeAsset)
    {
        EnsureMarkupDemoState();

        NowMarkdown.Document("# Markup demo\n\nThe panel below is rendered from a constrained XML-like markup string. Controls write to `NowMarkupState`, actions mutate state keys, and click/change/action events are returned to the host each frame.").Draw();

        using (NowLayout.Horizontal(spacing: 12f, stretchWidth: true))
        {
            using (NowLayout.Vertical(width: 430f, spacing: 8f))
            {
                var result = NowMarkup.Document(MarkupDemoSample).Draw(_markupDemoState);
                ConsumeMarkupDemoEvents(result);
            }

            using (NowLayout.Vertical(spacing: 8f, stretchWidth: true))
            {
                NowMarkdown.Document("## State").Draw();

                DrawMarkupDemoStateLine(themeAsset, "name", _markupDemoState.GetString("name", ""));
                DrawMarkupDemoStateLine(themeAsset, "volume", $"{Mathf.RoundToInt(_markupDemoState.GetFloat("volume") * 100f)}%");
                DrawMarkupDemoStateLine(themeAsset, "advanced", _markupDemoState.GetBool("advanced").ToString());
                DrawMarkupDemoStateLine(themeAsset, "quality", _markupDemoState.GetInt("quality").ToString());
                DrawMarkupDemoStateLine(themeAsset, "photo", (_markupDemoState.GetInt("photo") + 1).ToString());
                DrawMarkupDemoStateLine(themeAsset, "save events", _markupDemoClicks.ToString());
                DrawMarkupDemoStateLine(themeAsset, "last event", _markupDemoLastEvent);

                NowMarkdown.Document("## Hot reload\n\nUse `NowMarkup.File(\"Assets/UI/main.nowui\")` to render a disk file. It uses `FileSystemWatcher` to mark the source dirty and reparses on the main thread during `Draw()`.").Draw();

                NowMarkdown.Document("## Source\n\n```xml\n" + MarkupDemoSample + "\n```")
                    .SetFontSize(13f)
                    .Draw();
            }
        }
    }

    void EnsureMarkupDemoState()
    {
        if (!_markupDemoState.Has("name"))
            _markupDemoState.SetString("name", "NowUI");

        if (!_markupDemoState.Has("volume"))
            _markupDemoState.SetFloat("volume", 0.65f);

        if (!_markupDemoState.Has("photo"))
            _markupDemoState.SetInt("photo", 0);
    }

    void ConsumeMarkupDemoEvents(NowMarkupResult result)
    {
        if (result.events == null)
            return;

        for (int i = 0; i < result.events.Count; ++i)
        {
            var item = result.events[i];
            _markupDemoLastEvent = item.ToString();

            if (item.kind == NowMarkupEventKind.Action && item.name == "save")
                ++_markupDemoClicks;
        }
    }

    static void DrawMarkupDemoStateLine(NowThemeAsset themeAsset, string key, string value)
    {
        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label(key)
                .SetWidth(86f)
                .SetFontSize(13f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw();

            NowLayout.Label(value)
                .SetFontSize(13f)
                .SetStretchWidth()
                .Draw();
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

        var panel = NowLayout.ReserveRect(height: 280f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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

        var panel = NowLayout.ReserveRect(height: 280f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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
        NowMarkdown.Document("# Custom material demo\n\nThis demo is rendered through the active NowUI host. The large panel below is an ordinary `Now.Rectangle` using a generated noise texture and a custom UGUI material via `SetCanvasMaterial(...)`.").Draw();

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Animate").Draw(ref _customMaterialAnimate);

            NowLayout.Label("Frost").SetWidth(42f).Draw();
            NowLayout.Slider(0f, 1f).SetWidth(140f).Draw(ref _customMaterialFrost);

            NowLayout.Label("Radius").SetWidth(48f).Draw();
            NowLayout.Slider(0f, 44f).SetWidth(150f).Draw(ref _customMaterialRadius);
        }

        var panel = NowLayout.ReserveRect(height: 330f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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
        var panel = NowLayout.ReserveRect(height: 360f + debugHeight, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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

        var panel = NowLayout.ReserveRect(height: 360f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

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
            .SetRadius(NowCornerRadius.Top(10f))
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

    void DrawModelPreviewsDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document(
            "# Model previews demo\n\n" +
            "One isolated raw-mesh preview is shared by three ordinary `Now.Model` draws. " +
            "Change its scheduling and resolution policy here, then compare a direct texture, " +
            "rectangle styling, and a texture-backed modifier. No presentation GameObject is instantiated.").Draw();

        EnsureModelPreviewDemo();

        if (_modelPreviewDemoRig == null)
        {
            NowLayout.Label("The procedural preview could not be created.")
                .SetFontSize(14f)
                .SetBold()
                .SetColor(themeAsset.GetColor(NowColorToken.Danger, Color.red))
                .Draw();
            NowLayout.Label(_modelPreviewDemoError ?? "No presentation shader is available.")
                .SetFontSize(12f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .SetStretchWidth()
                .Draw();
            return;
        }

        bool queueRender = false;

        using (NowLayout.Horizontal(spacing: 12f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Auto rotate").Draw(ref _modelPreviewDemoAutoRotate);
            NowLayout.Checkbox("Pause rendering").Draw(ref _modelPreviewDemoPaused);
            NowLayout.Label("Update").SetWidth(52f).Draw();
            NowLayout.Dropdown(ModelPreviewUpdateLabels)
                .SetWidth(132f)
                .Draw(ref _modelPreviewDemoUpdateMode);

            if (NowLayout.Button("Queue render").SetWidth(112f).Draw())
                queueRender = true;
        }

        using (NowLayout.Horizontal(spacing: 10f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Label("Angle").SetWidth(46f).Draw();

            if (_modelPreviewDemoAutoRotate)
            {
                NowLayout.Label("Driven by the docs repaint loop")
                    .SetFontSize(12f)
                    .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                    .SetWidth(190f)
                    .Draw();
            }
            else
            {
                NowLayout.Slider(-180f, 180f)
                    .SetWidth(190f)
                    .Draw(ref _modelPreviewDemoAngle);
            }

            NowLayout.Label("Resolution").SetWidth(72f).Draw();
            NowLayout.Slider(0.25f, 1f)
                .SetWidth(130f)
                .Draw(ref _modelPreviewDemoResolutionScale);
            NowLayout.Label($"{_modelPreviewDemoResolutionScale:0.00}x").SetWidth(40f).Draw();

            NowLayout.Label("Wave").SetWidth(42f).Draw();
            NowLayout.Slider(0f, 12f)
                .SetStretchWidth()
                .Draw(ref _modelPreviewDemoWaveAmplitude);
        }

        using (NowLayout.Horizontal(spacing: 14f, alignItems: NowLayoutAlign.Center))
        {
            NowLayout.Checkbox("Use scene lighting").Draw(ref _modelPreviewDemoSceneLighting);
            NowLayout.Checkbox("Post processing").Draw(ref _modelPreviewDemoPostProcessing);
            NowLayout.Label("Both are opt-in; the default preview renders in a private scene.")
                .SetFontSize(12f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .SetStretchWidth()
                .Draw();
        }

        var preview = _modelPreviewDemoRig.preview;
        var updateMode = (NowModelPreviewUpdateMode)Mathf.Clamp(
            _modelPreviewDemoUpdateMode,
            (int)NowModelPreviewUpdateMode.WhenDirty,
            (int)NowModelPreviewUpdateMode.Manual);

        preview
            .SetUpdateMode(updateMode)
            .SetResolutionScale(_modelPreviewDemoResolutionScale)
            .SetSceneLightingEnabled(_modelPreviewDemoSceneLighting)
            .SetPostProcessingEnabled(_modelPreviewDemoPostProcessing)
            .SetRenderingEnabled(!_modelPreviewDemoPaused);

        if (_modelPreviewDemoAutoRotate)
            _modelPreviewDemoAngle = Mathf.Repeat(Time.time * 28f, 360f) - 180f;

        if (updateMode != NowModelPreviewUpdateMode.Manual || queueRender)
        {
            preview.SetRotation(Quaternion.Euler(-6f, _modelPreviewDemoAngle, 0f));
        }

        if (queueRender)
            preview.RequestRender();

        var panel = NowLayout.ReserveRect(height: 338f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted)
            .SetRadius(18f)
            .Draw();

        var area = panel.Inset(18f);
        const float Gap = 14f;
        float cardWidth = Mathf.Max(1f, (area.width - Gap * 2f) / 3f);
        var directCard = new NowRect(area.x, area.y, cardWidth, area.height);
        var styledCard = new NowRect(directCard.xMax + Gap, area.y, cardWidth, area.height);
        var effectCard = new NowRect(styledCard.xMax + Gap, area.y, cardWidth, area.height);

        DrawModelPreviewDemoCard(
            themeAsset,
            directCard,
            preview,
            "DIRECT TEXTURE",
            "Live RenderTexture binding",
            new Color(0.08f, 0.62f, 1f, 1f),
            styled: false,
            textureEffect: false,
            _modelPreviewDemoWaveAmplitude);
        DrawModelPreviewDemoCard(
            themeAsset,
            styledCard,
            preview,
            "MASK + STYLE",
            "Tint, radius and outline",
            new Color(0.2f, 0.82f, 0.66f, 1f),
            styled: true,
            textureEffect: false,
            _modelPreviewDemoWaveAmplitude);
        DrawModelPreviewDemoCard(
            themeAsset,
            effectCard,
            preview,
            "TEXTURE EFFECT",
            "Captured, then deformed",
            new Color(1f, 0.32f, 0.08f, 1f),
            styled: true,
            textureEffect: true,
            _modelPreviewDemoWaveAmplitude);

        string targetSize = preview.texture != null
            ? $"{preview.texture.width} × {preview.texture.height}"
            : "waiting for first visible draw";
        string schedule = _modelPreviewDemoPaused
            ? "paused; the current texture is retained"
            : updateMode == NowModelPreviewUpdateMode.Manual
                ? "manual; pose changes wait for Queue render"
                : updateMode == NowModelPreviewUpdateMode.EveryFrame
                    ? "continuous; follows a caller-owned live source"
                    : "dirty-driven; idle camera cost is zero";

        NowLayout.Label($"Target: {targetSize}   •   {schedule}")
            .SetFontSize(12f)
            .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
            .SetStretchWidth()
            .Draw();

        NowMarkdown.Document(
            "## Core pattern\n\n" +
            "```csharp\n" +
            "preview = new NowModelPreview(characterPrefab)\n" +
            "    .SetMaxResolution(512)\n" +
            "    .SetSceneLightingEnabled(false) // default: private preview scene\n" +
            "    .SetUpdateMode(NowModelPreviewUpdateMode.WhenDirty);\n\n" +
            "Now.Model(rect, preview).SetRadius(24f).Draw();\n" +
            "```\n\n" +
            "For Unity's normal Renderer path, borrow a dressed scene object and keep its " +
            "presentation layer excluded from gameplay cameras:\n\n" +
            "```csharp\n" +
            "preview = NowModelPreview.FromSceneObject(\n" +
            "    presentationCharacter,\n" +
            "    presentationCameraMask);\n" +
            "```\n\n" +
            "## Texture-backed effects\n\n" +
            "```csharp\n" +
            "using (NowEffects.Modifier(NowDeformers.Wave(phase, 6f, 52f))\n" +
            "    .SetRenderToTexture()\n" +
            "    .SetSourceRect(rect)\n" +
            "    .Begin())\n" +
            "{\n" +
            "    Now.Model(rect, preview).SetRadius(24f).Draw();\n" +
            "}\n" +
            "```\n\n" +
            "The preview is intentionally shared across all three cards. Dispose it with the " +
            "screen or inventory item that owns it; pause hidden animated panels explicitly.").Draw();

        if (!_modelPreviewDemoPaused &&
            (_modelPreviewDemoAutoRotate || updateMode == NowModelPreviewUpdateMode.EveryFrame))
        {
            NowControlState.RequestRepaint();
        }
    }

    static void DrawModelPreviewDemoCard(
        NowThemeAsset themeAsset,
        NowRect cardRect,
        NowModelPreview preview,
        string title,
        string subtitle,
        Color accent,
        bool styled,
        bool textureEffect,
        float waveAmplitude)
    {
        Color text = themeAsset.GetColor(NowColorToken.Text, Color.white);
        Color muted = themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
        Color border = themeAsset.GetColor(NowColorToken.Border, Color.gray);

        themeAsset.Rectangle(cardRect, NowRectangleStyle.Surface)
            .SetRadius(16f)
            .SetOutline(1f, new Color(border.r, border.g, border.b, 0.6f))
            .Draw();
        Now.Circle(new Vector2(cardRect.x + 20f, cardRect.y + 22f), 4f)
            .SetColor(accent)
            .Draw();
        Now.Text(new NowRect(cardRect.x + 32f, cardRect.y + 12f, cardRect.width - 44f, 20f))
            .SetFontSize(12f)
            .SetBold()
            .SetColor(text)
            .Draw(title);
        Now.Text(new NowRect(cardRect.x + 14f, cardRect.y + 36f, cardRect.width - 28f, 18f))
            .SetFontSize(11f)
            .SetColor(muted)
            .Draw(subtitle);

        var modelRect = cardRect.Inset(14f, 62f, 14f, 14f);

        using (Now.Mask(cardRect.Inset(8f)))
        {
            if (textureEffect)
            {
                using (NowEffects.Modifier(NowDeformers.Wave(Time.time * 0.35f, waveAmplitude, 52f, NowWaveAxis.Y))
                    .SetId(3001)
                    .SetSubdivision(10)
                    .SetRenderToTexture()
                    .SetSourceRect(modelRect)
                    .Begin())
                {
                    DrawPreview();
                }
            }
            else
            {
                DrawPreview();
            }
        }

        void DrawPreview()
        {
            var model = Now.Model(modelRect, preview);

            if (styled)
            {
                model = model
                    .SetColor(new Color(0.82f, 0.94f, 1f, 1f))
                    .SetRadius(28f)
                    .SetOutline(1.5f, new Color(accent.r, accent.g, accent.b, 0.82f));
            }

            model.Draw();
        }
    }

    void DrawFilePickerDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# File picker demo\n\nFile picker fields are built-in overlay controls." +
            " The field owns the browser UI and returns the selected path; your code still owns the" +
            " file read, write, import, or export operation.").Draw();

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            if (NowLayout.Button("Clear paths").SetWidth(104f).Draw())
            {
                _filePickerOpenPath = "";
                _filePickerSavePath = "";
                _filePickerDirectoryPath = "";
            }

            NowLayout.Checkbox("Show hidden").Draw(ref _filePickerShowHidden);
            NowLayout.FlexibleSpace();
            NowLayout.Label($"Committed selections: {_filePickerChanges}")
                .SetFontSize(12f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw();
        }

        var panel = NowLayout.ReserveRect(height: 430f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

        var body = panel.Inset(16f);
        string docsDirectory = ResolveDocumentationRoot() ?? FilePickerDemoDirectory("Assets");
        string assetsDirectory = FilePickerDemoDirectory("Assets");
        string saveDirectory = FilePickerDemoDirectory("Library");

        using (NowLayout.Area(650101, body, spacing: 10f, padding: 0f, alignItems: NowLayoutAlign.Start))
        {
            NowLayout.Label("Overlay fields")
                .SetFontSize(18f)
                .SetBold()
                .Draw();

            NowLayout.Label("Open, save, and directory modes share one builder. Filters and start directories are configured per field.")
                .SetFontSize(12f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .SetStretchWidth()
                .Draw();

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
            {
                NowLayout.Label("Open").SetWidth(74f).Draw();

                if (NowLayout.OpenFileField("docs-file-picker-open")
                    .SetTitle("Open project document")
                    .SetStartDirectory(docsDirectory)
                    .SetFilters(
                        new NowFileFilter("Markdown", "md"),
                        new NowFileFilter("Text", "txt", "json"),
                        new NowFileFilter("All files", "*"))
                    .SetShowHidden(_filePickerShowHidden)
                    .SetPopupSize(780f, 480f)
                    .SetStretchWidth()
                    .Draw(ref _filePickerOpenPath))
                {
                    ++_filePickerChanges;
                }
            }

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
            {
                NowLayout.Label("Save").SetWidth(74f).Draw();

                if (NowLayout.SaveFileField("docs-file-picker-save")
                    .SetTitle("Save settings")
                    .SetStartDirectory(saveDirectory)
                    .SetDefaultFileName("settings")
                    .SetDefaultExtension("json")
                    .SetFilter("Json", "json")
                    .SetShowHidden(_filePickerShowHidden)
                    .SetPopupSize(780f, 480f)
                    .SetStretchWidth()
                    .Draw(ref _filePickerSavePath))
                {
                    ++_filePickerChanges;
                }
            }

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
            {
                NowLayout.Label("Folder").SetWidth(74f).Draw();

                if (NowLayout.DirectoryField("docs-file-picker-directory")
                    .SetTitle("Choose output directory")
                    .SetStartDirectory(assetsDirectory)
                    .SetShowHidden(_filePickerShowHidden)
                    .SetPopupSize(780f, 480f)
                    .SetStretchWidth()
                    .Draw(ref _filePickerDirectoryPath))
                {
                    ++_filePickerChanges;
                }
            }

            var summary = NowLayout.ReserveRect(height: 150f, stretchWidth: true);
            DrawFilePickerSummary(summary, themeAsset);
        }
    }

    void DrawFilePickerSummary(NowRect rect, NowThemeAsset themeAsset)
    {
        float gap = 10f;
        float rowHeight = (rect.height - gap * 2f) / 3f;

        DrawFilePickerSummaryRow(
            new NowRect(rect.x, rect.y, rect.width, rowHeight),
            "Open file",
            _filePickerOpenPath,
            themeAsset.GetColor(NowColorToken.Accent, Color.blue));
        DrawFilePickerSummaryRow(
            new NowRect(rect.x, rect.y + rowHeight + gap, rect.width, rowHeight),
            "Save path",
            _filePickerSavePath,
            new Color(0.08f, 0.72f, 0.50f, 1f));
        DrawFilePickerSummaryRow(
            new NowRect(rect.x, rect.y + (rowHeight + gap) * 2f, rect.width, rowHeight),
            "Directory",
            _filePickerDirectoryPath,
            new Color(0.92f, 0.58f, 0.16f, 1f));
    }

    static void DrawFilePickerSummaryRow(NowRect rect, string label, string path, Color accent)
    {
        var theme = NowTheme.themeAsset;
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        Now.Rectangle(rect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.10f))
            .SetOutline(1f)
            .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.36f))
            .SetRadius(7f)
            .Draw();

        Now.Text(new NowRect(rect.x + 12f, rect.y + 8f, 92f, rect.height - 16f))
            .SetFontSize(11f)
            .SetColor(muted)
            .Draw(label);

        Now.Text(new NowRect(rect.x + 108f, rect.y + 8f, rect.width - 120f, rect.height - 16f))
            .SetFontSize(12f)
            .SetColor(string.IsNullOrEmpty(path) ? muted : text)
            .Draw(string.IsNullOrEmpty(path) ? "No selection" : ShortPath(path, 76));
    }

    static string FilePickerDemoDirectory(string projectRelative)
    {
        try
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelative));

            if (Directory.Exists(path))
                return path;
        }
        catch (System.Exception exception) when (
            exception is IOException ||
            exception is System.UnauthorizedAccessException ||
            exception is System.ArgumentException ||
            exception is System.NotSupportedException)
        {
        }

        return Application.dataPath;
    }

    static string ShortPath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        int keep = Mathf.Max(8, (maxLength - 3) / 2);
        int tail = Mathf.Max(8, maxLength - 3 - keep);
        return path.Substring(0, keep) + "..." + path.Substring(path.Length - tail, tail);
    }

    DocsViewStackHomeView ViewStackHomeView => _viewStackHomeView ??= new DocsViewStackHomeView(this);

    DocsViewStackDetailsView ViewStackDetailsView => _viewStackDetailsView ??= new DocsViewStackDetailsView(this);

    DocsViewStackPopupView ViewStackPopupView => _viewStackPopupView ??= new DocsViewStackPopupView(this);

    void DrawViewStackDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# View stack demo\n\nA `NowViewStack` keeps larger navigation flows retained" +
            " while each view still draws immediate-mode UI. The panel below owns one stack, pushes a" +
            " full-screen detail view, and opens a modal popup on top.").Draw();

        EnsureViewStackDemoHome();

        using (NowLayout.Horizontal(spacing: 8f, alignItems: NowLayoutAlign.Center))
        {
            if (NowLayout.Button("Reset").SetWidth(86f).Draw())
                ResetViewStackDemo();

            if (NowLayout.Button("Open details").SetWidth(128f).Draw())
                OpenViewStackDetails(_viewStackDemo);

            NowLayout.FlexibleSpace();
            NowLayout.Label($"Stack entries: {_viewStackDemo.count}")
                .SetFontSize(12f)
                .SetColor(themeAsset.GetColor(NowColorToken.TextMuted, Color.gray))
                .Draw();
        }

        var panel = NowLayout.ReserveRect(height: 430f, stretchWidth: true);
        themeAsset.Rectangle(panel, NowRectangleStyle.Muted).Draw();

        var surface = panel.Inset(12f);
        EnsureViewStackDemoHome();
        _viewStackDemo.Draw(surface);
    }

    void EnsureViewStackDemoHome()
    {
        if (_viewStackDemo.ContainsKey("docs-view-stack-home"))
            return;

        _viewStackDemo.Push("docs-view-stack-home", ViewStackHomeView,
            NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f)
                .SetCloseOnCancel(false));
    }

    void ResetViewStackDemo()
    {
        _viewStackDemo.Clear();
        _viewStackDemoProgress = 0.64f;
        _viewStackDemoSync = true;
    }

    void OpenViewStackDetails(NowViewStack stack)
    {
        ++_viewStackDemoOpens;

        stack.PushOrReplace("docs-view-stack-details", ViewStackDetailsView,
            NowViewOptions.FullScreen(NowViewTransitionPreset.SlideFromRight, 0.18f));
    }

    void OpenViewStackPopup(NowViewContext context)
    {
        var popup = CenterRect(context.rect, 360f, 190f);

        context.stack.Push("docs-view-stack-popup", ViewStackPopupView,
            NowViewOptions.Popup(popup, NowViewTransitionPreset.ScaleFade, 0.14f)
                .SetScrim(new Color(0f, 0f, 0f, 0.34f)));
    }

    void DrawViewStackHome(NowViewContext context)
    {
        var theme = NowTheme.themeAsset;
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        DrawViewStackChrome(context.rect, "Home", "Push a screen or popup from the active view.");

        var body = context.rect.Inset(22f, 66f, 22f, 22f);

        using (NowLayout.Area(640101, body, spacing: 10f, padding: 0f, alignItems: NowLayoutAlign.Start))
        {
            NowLayout.Label("Only the top stack entry receives live input. Covered views keep drawing passively for transitions and background context.")
                .SetStretchWidth()
                .SetColor(muted)
                .SetFontSize(12f)
                .Draw();

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                if (NowLayout.Button("Open details").SetWidth(128f).Draw())
                    OpenViewStackDetails(context.stack);

                if (NowLayout.Button("Open popup").SetWidth(120f).Draw())
                    OpenViewStackPopup(context);

                NowLayout.FlexibleSpace();
                NowLayout.Label(context.isTop ? "top view" : "passive")
                    .SetFontSize(12f)
                    .SetColor(muted)
                    .Draw();
            }

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                NowLayout.Checkbox("Sync enabled").Draw(ref _viewStackDemoSync);
                NowLayout.Label("Progress").SetWidth(58f).SetColor(muted).SetFontSize(12f).Draw();
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _viewStackDemoProgress);
            }

            var metrics = NowLayout.ReserveRect(height: 78f, stretchWidth: true);
            DrawViewStackMetrics(metrics, theme);
        }
    }

    void DrawViewStackDetails(NowViewContext context)
    {
        var theme = NowTheme.themeAsset;
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        DrawViewStackChrome(context.rect, "Details", "Full-screen entry with a slide transition.");

        var body = context.rect.Inset(22f, 66f, 22f, 22f);

        using (NowLayout.Area(640102, body, spacing: 10f, padding: 0f, alignItems: NowLayoutAlign.Start))
        {
            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                if (NowLayout.Button("Back").SetWidth(86f).SetStyle(NowRectangleStyle.Surface).Draw())
                    context.Close();

                if (NowLayout.Button("Confirm").SetWidth(108f).Draw())
                    OpenViewStackPopup(context);

                NowLayout.FlexibleSpace();
                NowLayout.Label($"visibleT {context.visibleT:0.00}")
                    .SetFontSize(12f)
                    .SetColor(muted)
                    .Draw();
            }

            NowLayout.Label("This view was pushed with a stable key. The toolbar can call PushOrReplace with the same key without duplicating it.")
                .SetStretchWidth()
                .SetColor(muted)
                .SetFontSize(12f)
                .Draw();

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                NowLayout.Label("Shared progress").SetWidth(112f).SetColor(muted).SetFontSize(12f).Draw();
                NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _viewStackDemoProgress);
            }

            NowLayout.Checkbox("Sync enabled").Draw(ref _viewStackDemoSync);

            var details = NowLayout.ReserveRect(height: 86f, stretchWidth: true);
            DrawViewStackDetailCard(details, theme);
        }
    }

    void DrawViewStackPopup(NowViewContext context)
    {
        var theme = NowTheme.themeAsset;
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        theme.Rectangle(context.rect, NowRectangleStyle.Surface).Draw();

        using (NowLayout.Area(640103, context.rect, spacing: 10f, padding: 18f, alignItems: NowLayoutAlign.Start))
        {
            NowLayout.Label("Modal popup").SetFontSize(18f).SetBold().SetColor(text).Draw();
            NowLayout.Label("Popups use the same INowView contract. Outside click or cancel closes this one automatically.")
                .SetStretchWidth()
                .SetHeight(48f)
                .SetFontSize(12f)
                .SetColor(muted)
                .Draw();

            using (NowLayout.Horizontal(height: 34f, stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 8f))
            {
                NowLayout.FlexibleSpace();

                if (NowLayout.Button("Cancel").SetWidth(92f).SetStyle(NowRectangleStyle.Surface).Draw())
                    context.Close();

                if (NowLayout.Button("Confirm").SetWidth(104f).Draw())
                {
                    ++_viewStackDemoConfirms;
                    context.Close();
                }
            }
        }
    }

    static void DrawViewStackChrome(NowRect rect, string title, string subtitle)
    {
        var theme = NowTheme.themeAsset;
        Color surface = theme.GetColor(NowColorToken.Surface, new Color(0.12f, 0.14f, 0.18f, 1f));
        Color panel = theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.16f, 0.18f, 0.23f, 1f));
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        Now.Rectangle(rect).SetColor(surface).SetRadius(8f).Draw();
        Now.Rectangle(new NowRect(rect.x, rect.y, rect.width, 52f))
            .SetColor(panel)
            .SetRadius(NowCornerRadius.Top(8f))
            .Draw();

        Now.Text(new NowRect(rect.x + 18f, rect.y + 10f, rect.width - 36f, 22f))
            .SetFontSize(17f)
            .SetBold()
            .SetColor(text)
            .Draw(title);

        Now.Text(new NowRect(rect.x + 18f, rect.y + 31f, rect.width - 36f, 16f))
            .SetFontSize(11f)
            .SetColor(muted)
            .Draw(subtitle);
    }

    void DrawViewStackMetrics(NowRect rect, NowThemeAsset theme)
    {
        float gap = 10f;
        float width = Mathf.Max(1f, (rect.width - gap * 2f) / 3f);

        DrawViewStackMetric(
            new NowRect(rect.x, rect.y, width, rect.height),
            "Pushes",
            _viewStackDemoOpens.ToString(),
            theme.GetColor(NowColorToken.Accent, Color.blue));
        DrawViewStackMetric(
            new NowRect(rect.x + width + gap, rect.y, width, rect.height),
            "Confirmations",
            _viewStackDemoConfirms.ToString(),
            new Color(0.08f, 0.72f, 0.50f, 1f));
        DrawViewStackMetric(
            new NowRect(rect.x + (width + gap) * 2f, rect.y, width, rect.height),
            "Progress",
            $"{Mathf.RoundToInt(_viewStackDemoProgress * 100f)}%",
            new Color(0.92f, 0.58f, 0.16f, 1f));
    }

    static void DrawViewStackMetric(NowRect rect, string label, string value, Color accent)
    {
        var theme = NowTheme.themeAsset;
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        Now.Rectangle(rect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.13f))
            .SetOutline(1f)
            .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.46f))
            .SetRadius(8f)
            .Draw();

        Now.Text(new NowRect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 18f))
            .SetFontSize(11f)
            .SetColor(muted)
            .Draw(label);

        Now.Text(new NowRect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 30f))
            .SetFontSize(22f)
            .SetBold()
            .SetColor(text)
            .Draw(value);
    }

    void DrawViewStackDetailCard(NowRect rect, NowThemeAsset theme)
    {
        Color accent = theme.GetColor(NowColorToken.Accent, Color.blue);
        Color text = theme.GetColor(NowColorToken.Text, Color.white);
        Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);

        Now.Rectangle(rect)
            .SetColor(new Color(accent.r, accent.g, accent.b, 0.10f))
            .SetOutline(1f)
            .SetOutlineColor(new Color(accent.r, accent.g, accent.b, 0.42f))
            .SetRadius(8f)
            .Draw();

        Now.Text(new NowRect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 20f))
            .SetFontSize(15f)
            .SetBold()
            .SetColor(text)
            .Draw(_viewStackDemoSync ? "Synchronized settings" : "Local changes paused");

        Now.Text(new NowRect(rect.x + 14f, rect.y + 40f, rect.width - 28f, 32f))
            .SetFontSize(12f)
            .SetColor(muted)
            .Draw($"Progress is shared with the home view: {Mathf.RoundToInt(_viewStackDemoProgress * 100f)}%");
    }

    static NowRect CenterRect(NowRect surface, float width, float height)
    {
        width = Mathf.Min(width, Mathf.Max(1f, surface.width - 24f));
        height = Mathf.Min(height, Mathf.Max(1f, surface.height - 24f));

        return new NowRect(
            surface.x + (surface.width - width) * 0.5f,
            surface.y + (surface.height - height) * 0.5f,
            width,
            height);
    }

    sealed class DocsViewStackHomeView : INowView
    {
        readonly NowDocsExample _owner;

        public DocsViewStackHomeView(NowDocsExample owner)
        {
            _owner = owner;
        }

        public void Draw(NowViewContext context)
        {
            _owner.DrawViewStackHome(context);
        }
    }

    sealed class DocsViewStackDetailsView : INowView
    {
        readonly NowDocsExample _owner;

        public DocsViewStackDetailsView(NowDocsExample owner)
        {
            _owner = owner;
        }

        public void Draw(NowViewContext context)
        {
            _owner.DrawViewStackDetails(context);
        }
    }

    sealed class DocsViewStackPopupView : INowView
    {
        readonly NowDocsExample _owner;

        public DocsViewStackPopupView(NowDocsExample owner)
        {
            _owner = owner;
        }

        public void Draw(NowViewContext context)
        {
            _owner.DrawViewStackPopup(context);
        }
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

    enum GalleryQuality
    {
        Low,
        Medium,
        High,
        Ultra
    }

    [System.Flags]
    enum GalleryChannels
    {
        None = 0,
        Music = 1,
        Effects = 2,
        Voice = 4,
        Ambient = 8
    }

    static readonly string[] GalleryResolutions = { "1280 x 720", "1920 x 1080", "2560 x 1440", "3840 x 2160" };
    static readonly string[] GalleryTabs = { "General", "Audio", "Video" };
    static readonly string[] GalleryCountries = { "Portugal", "Japan", "Brazil", "Norway", "Canada" };
    static readonly string[] GalleryChannelNames = { "Music", "Effects", "Voice", "Ambient" };

    void EnsureGalleryState()
    {
        if (_galleryRamp == null)
        {
            _galleryRamp = new Gradient();
            _galleryRamp.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.16f, 0.47f, 0.96f), 0f),
                    new GradientColorKey(new Color(1f, 0.72f, 0.16f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        }

        _galleryFalloff ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (_galleryDate == default)
            _galleryDate = System.DateTime.Today;
    }

    static void DrawGalleryLabel(NowThemeAsset theme, string text)
    {
        NowLayout.Label(text)
            .SetWidth(96f)
            .SetFontSize(12f)
            .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw();
    }

    void DrawControlsGalleryDemo(NowThemeAsset theme)
    {
        EnsureGalleryState();

        using (NowLayout.Vertical(spacing: 8f, stretchWidth: true))
            DrawControlsGalleryContent(theme);

        NowControlState.RequestRepaint();
    }

    void DrawControlsGalleryContent(NowThemeAsset theme)
    {
        var intro = NowMarkdown.Document("# Controls gallery\n\nEvery control below is a built-in from" +
            " [Controls](Controls.md), drawn with its themed default look. Values live on the docs" +
            " component and are passed by ref each frame — nothing here is retained.\n\n## Buttons & status").Draw();

        if (intro.clickedLink != null)
            NavigateLink(intro.clickedLink);

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            if (NowLayout.Button("Save").Draw())
                ++_gallerySaves;

            NowLayout.Badge(_gallerySaves.ToString()).SetStyle(NowRectangleStyle.Danger).Draw();

            if (NowLayout.Chip("Filter: Active").SetSelected(_galleryChipSelected).Draw())
                _galleryChipSelected = !_galleryChipSelected;

            NowLayout.FlexibleSpace();
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Determinate");
            NowLayout.ProgressBar(_galleryVolume).SetStretchWidth().Draw();
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Indeterminate");
            NowLayout.ProgressBar().SetIndeterminate().SetTime(Time.time).SetStretchWidth().Draw();
        }

        NowMarkdown.Document("## Toggles").Draw();

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 16f))
        {
            NowLayout.Checkbox("Enable shadows").Draw(ref _galleryShadows);
            NowLayout.Switch("Notifications").Draw(ref _galleryNotifications);
            NowLayout.FlexibleSpace();
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 16f))
        {
            if (NowLayout.Radio("Low", _galleryQuality == 0).Draw()) _galleryQuality = 0;
            if (NowLayout.Radio("Medium", _galleryQuality == 1).Draw()) _galleryQuality = 1;
            if (NowLayout.Radio("High", _galleryQuality == 2).Draw()) _galleryQuality = 2;
            NowLayout.FlexibleSpace();
        }

        NowMarkdown.Document("## Sliders & numbers").Draw();

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Volume");
            NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref _galleryVolume);
            NowLayout.Label($"{Mathf.RoundToInt(_galleryVolume * 100f)}%").SetWidth(40f).SetFontSize(12f).Draw();
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Speed");
            NowLayout.FloatField().SetRange(0f, 100f).SetSpinner(0.5f).SetStretchWidth().Draw(ref _gallerySpeed);
            DrawGalleryLabel(theme, "Lives");
            NowLayout.IntField().SetRange(0, 99).SetSpinner().SetStretchWidth().Draw(ref _galleryLives);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Spawn");
            NowLayout.Vector3Field().SetStretchWidth().Draw(ref _gallerySpawn);
        }

        NowMarkdown.Document("## Text").Draw();

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Name");
            NowLayout.TextField("gallery-name").SetPlaceholder("Player name...").SetStretchWidth().Draw(ref _galleryName);
        }

        NowLayout.TextArea("gallery-notes").SetPlaceholder("Notes...").SetLines(3, 6).SetStretchWidth().Draw(ref _galleryNotes);

        NowMarkdown.Document("## Selection").Draw();

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Resolution");
            NowLayout.Dropdown(GalleryResolutions).SetStretchWidth().Draw(ref _galleryResolution);
            DrawGalleryLabel(theme, "Quality");
            NowLayout.EnumDropdown<GalleryQuality>().SetStretchWidth().Draw(ref _galleryQualityLevel);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Country");
            NowLayout.ComboBox(GalleryCountries).SetStretchWidth().Draw(ref _galleryCountry);
            DrawGalleryLabel(theme, "Channels");
            NowLayout.MaskField(GalleryChannelNames, "gallery-channels").SetStretchWidth().Draw(ref _galleryChannels);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Tabs");
            NowLayout.TabBar(GalleryTabs).SetStretchWidth().Draw(ref _galleryTab);
        }

        NowLayout.Label($"Active tab: {GalleryTabs[_galleryTab]}")
            .SetFontSize(12f)
            .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
            .Draw();

        NowMarkdown.Document("## Pickers").Draw();

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Tint");
            NowLayout.ColorPicker("gallery-tint").SetShowAlpha(false).SetStretchWidth().Draw(ref _galleryTint);
            DrawGalleryLabel(theme, "Ramp");
            NowLayout.GradientField("gallery-ramp").SetStretchWidth().Draw(ref _galleryRamp);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Falloff");
            NowLayout.AnimationCurveField("gallery-falloff").SetTimeRange(0f, 1f).SetStretchWidth().Draw(ref _galleryFalloff);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Due date");
            NowLayout.DatePicker().SetStretchWidth().Draw(ref _galleryDate);
            DrawGalleryLabel(theme, "Alarm");
            NowLayout.TimePicker().SetStretchWidth().Draw(ref _galleryAlarm);
        }

        using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
        {
            DrawGalleryLabel(theme, "Jump key");
            NowLayout.KeyBindingField("gallery-jump").SetStretchWidth().Draw(ref _galleryJumpKey);
        }

        NowMarkdown.Document("## Disclosure").Draw();

        NowLayout.Foldout("Advanced").Draw(ref _galleryAdvanced);

        if (_galleryAdvanced)
        {
            using (NowLayout.Vertical(spacing: 8f, padding: 10f, stretchWidth: true))
            {
                using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
                {
                    DrawGalleryLabel(theme, "Flags");
                    NowLayout.EnumFlags<GalleryChannels>().SetStretchWidth().Draw(ref _galleryChannelFlags);
                }

                using (NowLayout.Horizontal(stretchWidth: true, alignItems: NowLayoutAlign.Center, spacing: 10f))
                {
                    DrawGalleryLabel(theme, "Hit layers");
                    NowLayout.LayerMaskField("gallery-layers").SetStretchWidth().Draw(ref _galleryLayers);
                }
            }
        }

        NowLayout.Space(6f);
        NowLayout.Label($"Saves: {_gallerySaves}   Name: {(string.IsNullOrEmpty(_galleryName) ? "—" : _galleryName)}   Due: {_galleryDate:yyyy-MM-dd}   Key: {_galleryJumpKey}")
            .SetFontSize(12f)
            .SetColor(theme.GetColor(NowColorToken.TextMuted, Color.gray))
            .SetStretchWidth()
            .Draw();
    }

    void DrawLiveDemo(NowThemeAsset themeAsset)
    {
        NowMarkdown.Document("# Custom controls demo\n\nThe controls below run the code from" +
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

/// <summary>The example code from Documentation~/CustomControls.md, compiled and runnable.</summary>
public static class GuideControls
{
    public static NowButton DangerButton(
        string label,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return NowLayout.Button(label, file, line)
            .SetStyle(NowRectangleStyle.Outline)
            .SetTextStyle(NowTextStyle.Body);
    }

    public static bool RoundButton(
        string label,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var theme = NowTheme.themeAsset;

        NowRect rect = NowLayout.ReserveRect(44f, 44f);
        var interaction = NowControls.Interact(rect, out bool focused, out bool submitted, file, line);

        float radius = rect.width * 0.5f;
        bool inCircle = (interaction.pointerPosition - rect.center).sqrMagnitude <= radius * radius;
        bool clicked = (interaction.clicked && inCircle) || submitted;

        float hoverT = NowControlState.Transition(
            interaction, "hover", interaction.hovered && inCircle);

        var circle = theme.Rectangle(rect, NowRectangleStyle.Accent);
        circle.radius = Vector4.one * radius;
        circle.color = NowControls.StateColor(circle.color, hoverT, interaction.held && inCircle);

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
        return DrawRating(default, NowControls.SiteId(file, line), ref value, max);
    }

    public static MyRating Rating(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return new MyRating(NowControls.SiteId(file, line));
    }

    internal static bool DrawRating(NowId id, int fallbackIdentity, ref int value, int max)
    {
        const float Dot = 18f, Gap = 6f;

        var theme = NowTheme.themeAsset;

        NowRect rect = NowLayout.ReserveRect(max * Dot + (max - 1) * Gap, Dot);
        var interaction = NowControls.Interact(id, fallbackIdentity, rect, out bool focused, out bool submitted);

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

/// <summary>The builder-form rating from Documentation~/CustomControls.md.</summary>
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
        return GuideControls.DrawRating(_id, _site, ref value, _max);
    }
}
