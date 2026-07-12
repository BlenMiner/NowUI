using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NowUI.Markup
{
    /// <summary>
    /// Render dispatch kind resolved once from a node's tag name, so per-frame
    /// dispatch switches on an enum instead of a tag-string switch.
    /// </summary>
    internal enum NowMarkupNodeKind : byte
    {
        PassThrough,
        GroupVertical,
        GroupHorizontal,
        Scroll,
        Gallery,
        Text,
        Divider,
        List,
        Details,
        Tabs,
        Button,
        Checkbox,
        Switch,
        Radio,
        Progress,
        Badge,
        Chip,
        Slider,
        TextField,
        TextArea,
        Dropdown,
        Space,
        Flex,
        Break
    }

    /// <summary>
    /// How a node's precomputed visibility resolves each frame: always visible,
    /// a constant, or a state key looked up live.
    /// </summary>
    internal enum NowMarkupVisibility : byte
    {
        None,
        Literal,
        StateKey
    }

    /// <summary>
    /// Per-node render data resolved once from the immutable document and its
    /// stylesheet, so steady-state drawing does not re-derive styles, ids, or
    /// content strings.
    /// </summary>
    internal sealed class NowMarkupNodeCache
    {
        public NowMarkupNodeKind kind;
        public NowMarkupVisibility visibility;
        public bool visibilityInvert;
        public bool visibilityLiteral;
        public string visibilityKey;
        public string controlLabel;
        public bool checkedDefault;
        public bool chipSelectedDefault;
        public float sliderMin;
        public float sliderMax;
        public float sliderDefault;
        public bool sliderHasStep;
        public float sliderStep;
        public float progressMax;
        public bool progressHasValue;
        public float progressValue;
        public bool progressIndeterminate;
        public bool progressHasTime;
        public string progressTimeKey;
        public string textDefault;
        public bool hasPlaceholder;
        public string placeholder;
        public int textMinLines;
        public int textMaxLines;
        public int dropdownFallback;
        public bool galleryControls;
        public bool tabsStretch;
        public float spaceSize;
        public NowMarkupStyleMap style;
        public NowLayoutOptions options;
        public string text;
        public string nodeId;
        public bool hasExplicitId;
        public string controlKey;
        public string richTextContent;
        public string plainTextContent;
        public List<NowMarkupNode> renderableChildren;
        public bool hasStateKey;
        public List<string> dropdownOptions;
        public int dropdownDefaultIndex;
        public List<string> tabLabels;
        public string tabsBarId;
        public string summaryLabel;
        public List<NowMarkupNode> detailChildren;
        public bool detailsOpenDefault;
        public string radioValue;
        public bool radioHasIntValue;
        public int radioIntValue;
        public string listMarker;
        public bool inlineOnly;
        public NowLayoutOptions dividerOptions;
        public string galleryKey;
        public string galleryPrevId;
        public string galleryNextId;
        public int galleryLabelIndex;
        public int galleryLabelCount;
        public string galleryLabel;
        public bool hasFontSize;
        public float fontSize;
        public bool hasTextColor;
        public Color textColor;
        public bool hasFontStyle;
        public NowFontStyle fontStyle;
        public bool selectable;
        public bool hasBackground;
        public bool hasFillColor;
        public Color fillColor;
        public bool hasSurfaceStyle;
        public NowRectangleStyle surfaceStyle;
        public bool hasRadius;
        public float radius;
        public bool hasControlRectStyle;
        public NowRectangleStyle controlRectStyle;
        public bool hasControlTextStyle;
        public NowTextStyle controlTextStyle;
    }

    /// <summary>
    /// Parsed NowUI markup document. Parse once, draw every frame. Supply a
    /// persistent <see cref="NowMarkupState"/> when controls should keep values
    /// outside the document cache.
    /// </summary>
    public sealed class NowMarkupDocument
    {
        readonly NowMarkupNode _root;
        readonly NowMarkupStyleSheet _styleSheet;
        readonly NowMarkupManifest _manifest;
        readonly NowMarkupState _ownedState = new NowMarkupState();
        readonly List<NowMarkupEvent> _events = new List<NowMarkupEvent>(8);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        HashSet<string> _warnedQueries;
#endif
        int _eventsVersion;

        public NowMarkupNode root => _root;

        /// <summary>Ids, state keys, and action names this document declares.</summary>
        public NowMarkupManifest manifest => _manifest;

        public NowMarkupDocument(NowMarkupNode root)
        {
            _root = root ?? NowMarkupParser.Parse(string.Empty);
            _styleSheet = NowMarkupStyleSheet.FromDocument(_root);
            _manifest = NowMarkupManifest.FromDocument(_root);
        }

        public static NowMarkupDocument Parse(string source)
        {
            return new NowMarkupDocument(NowMarkupParser.Parse(source));
        }

        /// <summary>
        /// Draws into the current NowLayout group. The returned result borrows
        /// the document's event buffer and stays valid only until this document
        /// draws again; read it before the next draw.
        /// </summary>
        public NowMarkupResult Draw(NowMarkupState state = null)
        {
            BeginFrame();
            RenderChildren(_root, state ?? _ownedState);
            return new NowMarkupResult(this, _events, _eventsVersion);
        }

        /// <summary>
        /// Draws inside an explicit rect by opening a root layout area. The
        /// returned result borrows the document's event buffer and stays valid
        /// only until this document draws again; read it before the next draw.
        /// </summary>
        public NowMarkupResult Draw(NowRect rect, NowMarkupState state = null)
        {
            BeginFrame();

            using (NowLayout.Area(rect))
                RenderChildren(_root, state ?? _ownedState);

            return new NowMarkupResult(this, _events, _eventsVersion);
        }

        internal bool IsResultCurrent(int version)
        {
            return version == _eventsVersion;
        }

        /// <summary>
        /// Editor/development-build safety net for hard-coded lookup strings:
        /// warns once when a result query names an id, key, or action the
        /// document never declares, so typos surface instead of silently
        /// returning false forever.
        /// </summary>
        internal void ValidateQuery(NowMarkupEventKind kind, string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!NowMarkup.validateQueries || string.IsNullOrEmpty(name))
                return;

            bool known = kind == NowMarkupEventKind.Action
                ? _manifest.DeclaresAction(name)
                : _manifest.DeclaresId(name) || _manifest.DeclaresKey(name);

            if (known)
                return;

            _warnedQueries ??= new HashSet<string>(StringComparer.Ordinal);

            if (!_warnedQueries.Add(kind + ":" + name))
                return;

            if (kind == NowMarkupEventKind.Action)
            {
                Debug.LogWarning(
                    $"NowMarkup: queried action \"{name}\", but the document never emits it. " +
                    $"Declared actions: {DescribeNames(_manifest.actions)}.");
            }
            else
            {
                string verb = kind == NowMarkupEventKind.Click ? "click" : "change";
                Debug.LogWarning(
                    $"NowMarkup: queried {verb} \"{name}\", but the document declares no matching id or state key. " +
                    $"Declared ids: {DescribeNames(_manifest.ids)}; state keys: {DescribeNames(_manifest.keys)}.");
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeNames(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return "(none)";

            var builder = new StringBuilder();
            int count = Math.Min(values.Count, 12);

            for (int i = 0; i < count; ++i)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(values[i]);
            }

            if (values.Count > count)
                builder.Append(", …");

            return builder.ToString();
        }
#endif

        void BeginFrame()
        {
            _events.Clear();
            ++_eventsVersion;
        }

        void RenderChildren(NowMarkupNode node, NowMarkupState state)
        {
            for (int i = 0; i < node.children.Count; ++i)
                RenderNode(node.children[i], state);
        }

        void RenderNode(NowMarkupNode node, NowMarkupState state)
        {
            if (node == null)
                return;

            if (node.isText)
            {
                string text = Cache(node).text;

                if (!string.IsNullOrEmpty(text))
                    NowLayout.RichText(text).ParseDefaultTags().Draw();

                return;
            }

            if (node.name == "style")
                return;

            var cache = Cache(node);

            if (!ShouldRender(cache, state))
                return;

            switch (cache.kind)
            {
                case NowMarkupNodeKind.GroupVertical:
                    RenderGroup(node, state, cache, vertical: true);
                    break;
                case NowMarkupNodeKind.GroupHorizontal:
                    RenderGroup(node, state, cache, vertical: false);
                    break;
                case NowMarkupNodeKind.Scroll:
                    RenderScroll(node, state, cache);
                    break;
                case NowMarkupNodeKind.Gallery:
                    RenderGallery(node, state, cache);
                    break;
                case NowMarkupNodeKind.Text:
                    RenderText(cache);
                    break;
                case NowMarkupNodeKind.Divider:
                    RenderDivider(cache);
                    break;
                case NowMarkupNodeKind.List:
                    RenderList(node, state, cache);
                    break;
                case NowMarkupNodeKind.Details:
                    RenderDetails(node, state, cache);
                    break;
                case NowMarkupNodeKind.Tabs:
                    RenderTabs(node, state, cache);
                    break;
                case NowMarkupNodeKind.Button:
                    RenderButton(node, state, cache);
                    break;
                case NowMarkupNodeKind.Checkbox:
                    RenderCheckbox(node, state, cache);
                    break;
                case NowMarkupNodeKind.Switch:
                    RenderSwitch(node, state, cache);
                    break;
                case NowMarkupNodeKind.Radio:
                    RenderRadio(node, state, cache);
                    break;
                case NowMarkupNodeKind.Progress:
                    RenderProgress(node, state, cache);
                    break;
                case NowMarkupNodeKind.Badge:
                    RenderBadge(cache);
                    break;
                case NowMarkupNodeKind.Chip:
                    RenderChip(node, state, cache);
                    break;
                case NowMarkupNodeKind.Slider:
                    RenderSlider(node, state, cache);
                    break;
                case NowMarkupNodeKind.TextField:
                    RenderTextField(node, state, cache);
                    break;
                case NowMarkupNodeKind.TextArea:
                    RenderTextArea(node, state, cache);
                    break;
                case NowMarkupNodeKind.Dropdown:
                    RenderDropdown(node, state, cache);
                    break;
                case NowMarkupNodeKind.Space:
                    RenderSpace(cache, flexible: false);
                    break;
                case NowMarkupNodeKind.Flex:
                    RenderSpace(cache, flexible: true);
                    break;
                case NowMarkupNodeKind.Break:
                    NowLayout.Space(cache.spaceSize);
                    break;
                default:
                    RenderChildren(node, state);
                    break;
            }
        }

        static NowMarkupNodeKind KindOf(string name)
        {
            switch (name)
            {
                case "column":
                case "vstack":
                case "div":
                case "section":
                case "panel":
                case "card":
                    return NowMarkupNodeKind.GroupVertical;
                case "row":
                case "hstack":
                    return NowMarkupNodeKind.GroupHorizontal;
                case "scroll":
                case "scrollview":
                    return NowMarkupNodeKind.Scroll;
                case "gallery":
                    return NowMarkupNodeKind.Gallery;
                case "text":
                case "label":
                case "p":
                case "richtext":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    return NowMarkupNodeKind.Text;
                case "hr":
                case "divider":
                    return NowMarkupNodeKind.Divider;
                case "ul":
                case "ol":
                    return NowMarkupNodeKind.List;
                case "details":
                    return NowMarkupNodeKind.Details;
                case "tabs":
                case "tabview":
                    return NowMarkupNodeKind.Tabs;
                case "button":
                    return NowMarkupNodeKind.Button;
                case "checkbox":
                    return NowMarkupNodeKind.Checkbox;
                case "switch":
                case "toggle":
                    return NowMarkupNodeKind.Switch;
                case "radio":
                    return NowMarkupNodeKind.Radio;
                case "progress":
                case "progressbar":
                    return NowMarkupNodeKind.Progress;
                case "badge":
                    return NowMarkupNodeKind.Badge;
                case "chip":
                    return NowMarkupNodeKind.Chip;
                case "slider":
                    return NowMarkupNodeKind.Slider;
                case "textfield":
                case "input":
                    return NowMarkupNodeKind.TextField;
                case "textarea":
                    return NowMarkupNodeKind.TextArea;
                case "dropdown":
                case "select":
                    return NowMarkupNodeKind.Dropdown;
                case "space":
                    return NowMarkupNodeKind.Space;
                case "flex":
                case "flexspace":
                    return NowMarkupNodeKind.Flex;
                case "br":
                    return NowMarkupNodeKind.Break;
                default:
                    return NowMarkupNodeKind.PassThrough;
            }
        }

        NowMarkupNodeCache Cache(NowMarkupNode node)
        {
            var cache = node.renderCache;

            if (cache != null)
                return cache;

            cache = new NowMarkupNodeCache();

            if (node.isText)
            {
                cache.text = CleanText(node.text);
                node.renderCache = cache;
                return cache;
            }

            var style = _styleSheet.Resolve(node);
            cache.kind = KindOf(node.name);
            BuildVisibility(node, cache);
            cache.style = style;
            cache.options = NowMarkupStyleHelpers.LayoutOptions(style);
            cache.hasExplicitId = HasId(node);
            string id = FirstAttribute(node, "id", "name");
            cache.nodeId = !string.IsNullOrEmpty(id)
                ? id
                : "markup:" + node.name + ":" + node.sourceIndex.ToString(CultureInfo.InvariantCulture);
            string key = FirstAttribute(node, "state", "bind", "value-key");

            if (node.name == "radio" &&
                node.TryAttribute("group", out var group) &&
                !string.IsNullOrWhiteSpace(group))
            {
                key = group.Trim();
            }

            cache.hasStateKey = !string.IsNullOrEmpty(key);
            cache.controlKey = string.IsNullOrEmpty(key) ? cache.nodeId : key;
            cache.richTextContent = node.TryAttribute("value", out var value)
                ? value ?? string.Empty
                : RichTextContent(node).Trim();
            cache.plainTextContent = PlainTextContent(node).Trim();
            cache.renderableChildren = CollectRenderableChildren(node);

            if (node.name == "gallery")
            {
                string galleryKey = FirstAttribute(node, "index", "state");
                cache.galleryKey = string.IsNullOrEmpty(galleryKey) ? cache.nodeId + ".index" : galleryKey;
                cache.galleryPrevId = cache.nodeId + ".prev";
                cache.galleryNextId = cache.nodeId + ".next";
                cache.galleryLabelIndex = -1;
                cache.galleryLabelCount = -1;
            }

            if (node.name == "dropdown" || node.name == "select")
            {
                cache.dropdownOptions = new List<string>(4);
                cache.dropdownDefaultIndex = BuildOptions(node, cache.dropdownOptions);
            }

            switch (node.name)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    ApplyHeadingDefaults(cache, node.name[1] - '0');
                    break;
                case "hr":
                case "divider":
                    cache.dividerOptions = DividerOptions(style, cache.options);
                    break;
                case "details":
                    BuildDetails(node, cache);
                    break;
                case "tabs":
                case "tabview":
                    BuildTabs(node, cache);
                    break;
                case "radio":
                    BuildRadio(node, cache);
                    break;
                case "ul":
                case "ol":
                    AssignListMarkers(cache, ordered: node.name == "ol");
                    break;
                case "li":
                    cache.inlineOnly = HasInlineContentOnly(node);
                    break;
            }

            if (style.TryGetAny(out var sizeValue, "font-size", "size") &&
                float.TryParse(TrimUnit(sizeValue), NumberStyles.Float, CultureInfo.InvariantCulture, out float fontSize))
            {
                cache.hasFontSize = true;
                cache.fontSize = fontSize;
            }

            if (style.TryGet("color", out var colorValue) &&
                NowMarkupStyleHelpers.TryParseColor(colorValue, out var textColor))
            {
                cache.hasTextColor = true;
                cache.textColor = textColor;
            }

            if (style.TryGet("font-style", out var fontStyleValue) &&
                NowMarkupStyleHelpers.TryParseFontStyle(fontStyleValue, out var fontStyle))
            {
                cache.hasFontStyle = true;
                cache.fontStyle = fontStyle;
            }

            cache.selectable = style.TryGetBool("selectable", out bool selectable) && selectable;

            bool hasFill = style.TryGetAny(out var fillValue, "background", "background-color", "bg");
            bool hasSurface = style.TryGetAny(out var surfaceValue, "rect-style", "surface", "panel-style");
            cache.hasBackground = hasFill || hasSurface;

            if (hasSurface && NowMarkupStyleHelpers.TryParseRectangleStyle(surfaceValue, out var surfaceStyle))
            {
                cache.hasSurfaceStyle = true;
                cache.surfaceStyle = surfaceStyle;
            }

            if (hasFill && NowMarkupStyleHelpers.TryParseColor(fillValue, out var fillColor))
            {
                cache.hasFillColor = true;
                cache.fillColor = fillColor;
            }

            if (style.TryGetAny(out var radiusValue, "radius", "border-radius") &&
                float.TryParse(TrimUnit(radiusValue), NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
            {
                cache.hasRadius = true;
                cache.radius = radius;
            }

            if (style.TryGetAny(out var rectStyleValue, "variant", "rect-style", "style") &&
                NowMarkupStyleHelpers.TryParseRectangleStyle(rectStyleValue, out var controlRectStyle))
            {
                cache.hasControlRectStyle = true;
                cache.controlRectStyle = controlRectStyle;
            }

            if (style.TryGet("text-style", out var textStyleValue) &&
                NowMarkupStyleHelpers.TryParseTextStyle(textStyleValue, out var controlTextStyle))
            {
                cache.hasControlTextStyle = true;
                cache.controlTextStyle = controlTextStyle;
            }

            BuildControlDefaults(node, cache, style);

            node.renderCache = cache;
            return cache;
        }

        /// <summary>
        /// Precomputes every attribute a control reads per frame; documents and
        /// stylesheets are immutable after parse, so only state lookups stay live.
        /// </summary>
        static void BuildControlDefaults(NowMarkupNode node, NowMarkupNodeCache cache, NowMarkupStyleMap style)
        {
            switch (cache.kind)
            {
                case NowMarkupNodeKind.Gallery:
                    cache.galleryControls = ReadBool(node, style, "controls", false);
                    break;
                case NowMarkupNodeKind.Tabs:
                    cache.tabsStretch = ReadBool(node, style, "stretch-tabs", false);
                    break;
                case NowMarkupNodeKind.Slider:
                    cache.sliderMin = ReadFloat(node, style, "min", "minimum", 0f);
                    cache.sliderMax = ReadFloat(node, style, "max", "maximum", 1f);
                    cache.sliderDefault = ReadFloat(node, style, "value", "default", cache.sliderMin);
                    cache.sliderHasStep = TryReadFloat(node, style, "step", out cache.sliderStep);
                    break;
                case NowMarkupNodeKind.Progress:
                    cache.progressMax = ReadFloat(node, style, "max", null, 1f);
                    cache.progressHasValue = TryReadFloat(node, style, "value", out cache.progressValue);
                    cache.progressIndeterminate = ReadBool(node, style, "indeterminate", false);
                    cache.progressHasTime = node.TryAttribute("time", out cache.progressTimeKey);
                    break;
                case NowMarkupNodeKind.TextField:
                    cache.textDefault = node.Attribute("value");
                    cache.hasPlaceholder = node.TryAttribute("placeholder", out cache.placeholder);
                    break;
                case NowMarkupNodeKind.TextArea:
                    cache.textDefault = node.Attribute("value", cache.plainTextContent);
                    cache.hasPlaceholder = node.TryAttribute("placeholder", out cache.placeholder);
                    cache.textMinLines = Mathf.Max(1, ReadInt(node, "min-lines", 3));
                    cache.textMaxLines = Mathf.Max(cache.textMinLines, ReadInt(node, "max-lines", 8));
                    break;
                case NowMarkupNodeKind.Dropdown:
                    cache.dropdownFallback = ReadInt(node, "selected",
                        cache.dropdownDefaultIndex >= 0 ? cache.dropdownDefaultIndex : 0);
                    break;
                case NowMarkupNodeKind.Checkbox:
                    cache.checkedDefault = ReadAttributeBool(node, "checked", false);
                    cache.controlLabel = ControlLabel(node, cache);
                    break;
                case NowMarkupNodeKind.Switch:
                    cache.checkedDefault = ReadAttributeBool(node, "checked", ReadAttributeBool(node, "on", false));
                    cache.controlLabel = ControlLabel(node, cache);
                    break;
                case NowMarkupNodeKind.Radio:
                    cache.checkedDefault = ReadAttributeBool(node, "checked", false);
                    cache.controlLabel = ControlLabel(node, cache);
                    break;
                case NowMarkupNodeKind.Chip:
                    cache.chipSelectedDefault = ReadAttributeBool(node, "selected", false);
                    cache.controlLabel = ControlLabel(node, cache);
                    break;
                case NowMarkupNodeKind.Badge:
                    cache.controlLabel = ControlLabel(node, cache);
                    break;
                case NowMarkupNodeKind.Button:
                    cache.controlLabel = ControlLabel(node, cache);

                    if (string.IsNullOrEmpty(cache.controlLabel))
                        cache.controlLabel = cache.nodeId;

                    break;
                case NowMarkupNodeKind.Space:
                    cache.spaceSize = ReadFloat(node, style, "size", "height", 8f);
                    break;
                case NowMarkupNodeKind.Flex:
                    cache.spaceSize = ReadFloat(node, style, "weight", "stretch", 1f);
                    break;
                case NowMarkupNodeKind.Break:
                    cache.spaceSize = ReadFloat(node, style, "height", "size", 8f);
                    break;
            }
        }

        static string ControlLabel(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            return node.TryAttribute("text", out var text) ? text : cache.plainTextContent;
        }

        /// <summary>
        /// Resolves the visibility attributes (if/when/visible/show/hidden) once:
        /// literal expressions collapse to a constant, state-key expressions keep
        /// only the live lookup, and bang prefixes fold into one invert flag.
        /// </summary>
        static void BuildVisibility(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            bool hiddenSemantics = false;

            if (!node.TryAttribute("if", out var expression) &&
                !node.TryAttribute("when", out expression) &&
                !node.TryAttribute("visible", out expression) &&
                !node.TryAttribute("show", out expression))
            {
                if (!node.TryAttribute("hidden", out expression))
                {
                    cache.visibility = NowMarkupVisibility.None;
                    return;
                }

                hiddenSemantics = true;
            }

            expression = expression?.Trim() ?? string.Empty;

            if (expression.Length == 0)
            {
                cache.visibility = NowMarkupVisibility.Literal;
                cache.visibilityLiteral = hiddenSemantics;
                return;
            }

            bool invert = hiddenSemantics;

            while (expression.StartsWith("!", StringComparison.Ordinal))
            {
                invert = !invert;
                expression = expression.Substring(1).TrimStart();
            }

            if (NowMarkupState.TryParseBool(expression, out bool literal))
            {
                cache.visibility = NowMarkupVisibility.Literal;
                cache.visibilityLiteral = invert ? !literal : literal;
                return;
            }

            cache.visibility = NowMarkupVisibility.StateKey;
            cache.visibilityKey = expression;
            cache.visibilityInvert = invert;
        }

        void RenderGroup(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache, bool vertical)
        {
            var options = cache.options;
            NowLayoutScope scope = cache.hasExplicitId
                ? (vertical ? NowLayout.Vertical(new NowId(cache.nodeId), options) : NowLayout.Horizontal(new NowId(cache.nodeId), options))
                : (vertical ? NowLayout.Vertical(options) : NowLayout.Horizontal(options));

            using (scope)
            {
                DrawBackground(cache, scope.rect);
                RenderChildren(node, state);
            }
        }

        void RenderScroll(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            using (NowLayout.ScrollView(new NowId(cache.nodeId)).SetOptions(cache.options).Begin())
                RenderChildren(node, state);
        }

        void RenderGallery(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.galleryKey;
            var slides = cache.renderableChildren;
            int count = slides.Count;

            if (count == 0)
                return;

            int previous = state.GetInt(key);
            int index = Mathf.Clamp(previous, 0, count - 1);

            if (index != previous)
                state.SetInt(key, index);

            using (var scope = NowLayout.Vertical(new NowId(id), cache.options))
            {
                DrawBackground(cache, scope.rect);

                var slide = slides[index];

                if (slide.name == "slide" || slide.name == "item")
                    RenderChildren(slide, state);
                else
                    RenderNode(slide, state);

                if (cache.galleryControls)
                {
                    using (NowLayout.Horizontal(spacing: 8f))
                    {
                        if (NowLayout.Button("Prev").SetId(new NowId(cache.galleryPrevId)).SetStyle(NowRectangleStyle.Outline).Draw())
                        {
                            int next = state.StepInt(key, -1, count);
                            Record(new NowMarkupEvent(NowMarkupEventKind.Click, cache.galleryPrevId));
                            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, next.ToString(CultureInfo.InvariantCulture)));
                        }

                        NowLayout.FlexibleSpace();

                        NowLayout.Label(GalleryLabel(cache, index, count)).Draw();

                        NowLayout.FlexibleSpace();

                        if (NowLayout.Button("Next").SetId(new NowId(cache.galleryNextId)).SetStyle(NowRectangleStyle.Outline).Draw())
                        {
                            int next = state.StepInt(key, 1, count);
                            Record(new NowMarkupEvent(NowMarkupEventKind.Click, cache.galleryNextId));
                            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, next.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                }
            }
        }

        static string GalleryLabel(NowMarkupNodeCache cache, int index, int count)
        {
            if (cache.galleryLabel == null || cache.galleryLabelIndex != index || cache.galleryLabelCount != count)
            {
                cache.galleryLabelIndex = index;
                cache.galleryLabelCount = count;
                cache.galleryLabel = (index + 1).ToString(CultureInfo.InvariantCulture) + " / " +
                    count.ToString(CultureInfo.InvariantCulture);
            }

            return cache.galleryLabel;
        }

        static readonly float[] HeadingSizes = { 32f, 26f, 22f, 18f, 16f, 14f };

        static readonly NowLayoutOptions ListContentOptions = default(NowLayoutOptions).SetStretchWidth();

        static void ApplyHeadingDefaults(NowMarkupNodeCache cache, int level)
        {
            cache.hasFontSize = true;
            cache.fontSize = HeadingSizes[Mathf.Clamp(level, 1, HeadingSizes.Length) - 1];
            cache.hasFontStyle = true;
            cache.fontStyle = NowFontStyle.Bold;
        }

        static NowLayoutOptions DividerOptions(NowMarkupStyleMap style, NowLayoutOptions options)
        {
            if (!style.TryGet("height", out _))
                options = options.SetHeight(1f);

            if (!style.TryGet("width", out _) &&
                !style.TryGetAny(out _, "stretch", "grow", "stretch-width", "grow-x"))
            {
                options = options.SetStretchWidth();
            }

            return options;
        }

        void BuildDetails(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            cache.detailsOpenDefault = ReadAttributeBool(node, "open", false);
            cache.detailChildren = new List<NowMarkupNode>(cache.renderableChildren.Count);
            string label = FirstAttribute(node, "summary", "title");

            for (int i = 0; i < cache.renderableChildren.Count; ++i)
            {
                var child = cache.renderableChildren[i];

                if (!child.isText && child.name == "summary")
                {
                    if (label.Length == 0)
                        label = PlainTextContent(child);

                    continue;
                }

                cache.detailChildren.Add(child);
            }

            cache.summaryLabel = label.Length > 0 ? label : "Details";
        }

        void BuildTabs(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            string key = FirstAttribute(node, "index", "state");
            cache.galleryKey = string.IsNullOrEmpty(key) ? cache.nodeId + ".index" : key;
            cache.tabsBarId = cache.nodeId + ".bar";
            cache.tabLabels = new List<string>(cache.renderableChildren.Count);

            for (int i = 0; i < cache.renderableChildren.Count; ++i)
            {
                var child = cache.renderableChildren[i];
                string label = child.isText ? string.Empty : FirstAttribute(child, "title", "label");

                cache.tabLabels.Add(label.Length > 0
                    ? label
                    : "Tab " + (i + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        static void BuildRadio(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            string value = FirstAttribute(node, "value", "option");

            if (value.Length == 0)
                value = cache.plainTextContent;

            if (value.Length == 0)
                value = cache.nodeId;

            cache.radioValue = value;
            cache.radioHasIntValue = int.TryParse(
                value, NumberStyles.Integer, CultureInfo.InvariantCulture, out cache.radioIntValue);
        }

        void AssignListMarkers(NowMarkupNodeCache cache, bool ordered)
        {
            int number = 0;

            for (int i = 0; i < cache.renderableChildren.Count; ++i)
            {
                var child = cache.renderableChildren[i];

                if (child.isText || child.name != "li")
                    continue;

                Cache(child).listMarker = ordered
                    ? (++number).ToString(CultureInfo.InvariantCulture) + "."
                    : "•";
            }
        }

        static bool HasInlineContentOnly(NowMarkupNode node)
        {
            bool hasContent = false;

            for (int i = 0; i < node.children.Count; ++i)
            {
                var child = node.children[i];

                if (child.isText)
                {
                    hasContent |= !string.IsNullOrWhiteSpace(child.text);
                    continue;
                }

                if (!IsRichTextInline(child.name))
                    return false;

                hasContent = true;
            }

            return hasContent;
        }

        void RenderDivider(NowMarkupNodeCache cache)
        {
            var rect = NowLayout.Rect(cache.dividerOptions);

            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Color color = cache.hasFillColor
                ? cache.fillColor
                : cache.hasTextColor
                    ? cache.textColor
                    : NowTheme.themeAsset.GetColor(NowColorToken.Border);

            var rectangle = Now.Rectangle(rect).SetColor(color);

            if (cache.hasRadius)
                rectangle = rectangle.SetRadius(cache.radius);

            rectangle.Draw();
        }

        void RenderList(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            var scope = cache.hasExplicitId
                ? NowLayout.Vertical(new NowId(cache.nodeId), cache.options)
                : NowLayout.Vertical(cache.options);

            using (scope)
            {
                DrawBackground(cache, scope.rect);

                var children = cache.renderableChildren;

                for (int i = 0; i < children.Count; ++i)
                {
                    var child = children[i];

                    if (child.isText || child.name != "li")
                    {
                        RenderNode(child, state);
                        continue;
                    }

                    if (!ShouldRender(Cache(child), state))
                        continue;

                    RenderListItem(child, state);
                }
            }
        }

        void RenderListItem(NowMarkupNode node, NowMarkupState state)
        {
            var cache = Cache(node);

            using (NowLayout.Horizontal(spacing: 8f))
            {
                NowLayout.Label(cache.listMarker ?? "•").Draw();

                using (NowLayout.Vertical(ListContentOptions))
                {
                    if (cache.inlineOnly)
                        RenderText(cache);
                    else
                        RenderChildren(node, state);
                }
            }
        }

        void RenderDetails(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string key = cache.controlKey;
            bool open = state.GetBool(key, cache.detailsOpenDefault);
            var foldout = NowLayout.Foldout(cache.summaryLabel, new NowId(cache.nodeId))
                .SetOptions(cache.options);

            if (foldout.Draw(ref open))
            {
                state.SetBool(key, open);
                Record(new NowMarkupEvent(NowMarkupEventKind.Change, cache.nodeId, key, open ? "true" : "false"));
                ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, cache.nodeId);
            }

            if (!open)
                return;

            for (int i = 0; i < cache.detailChildren.Count; ++i)
                RenderNode(cache.detailChildren[i], state);
        }

        void RenderTabs(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            var labels = cache.tabLabels;
            var panes = cache.renderableChildren;

            if (labels.Count == 0)
                return;

            string key = cache.galleryKey;
            int previous = state.GetInt(key);
            int index = Mathf.Clamp(previous, 0, labels.Count - 1);

            if (index != previous)
                state.SetInt(key, index);

            using (var scope = NowLayout.Vertical(new NowId(cache.nodeId), cache.options))
            {
                DrawBackground(cache, scope.rect);

                var bar = NowLayout.TabBar(labels).SetId(new NowId(cache.tabsBarId));

                if (cache.tabsStretch)
                    bar = bar.SetStretchTabs();

                if (bar.Draw(ref index))
                {
                    state.SetInt(key, index);
                    Record(new NowMarkupEvent(
                        NowMarkupEventKind.Change, cache.nodeId, key, index.ToString(CultureInfo.InvariantCulture)));
                    ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, cache.nodeId);
                }

                var pane = panes[index];

                if (!pane.isText && (pane.name == "tab" || pane.name == "page"))
                    RenderChildren(pane, state);
                else
                    RenderNode(pane, state);
            }
        }

        void RenderSwitch(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            bool value = state.GetBool(key, cache.checkedDefault);

            var control = NowLayout.Switch(cache.controlLabel)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasControlTextStyle)
                control = control.SetTextStyle(cache.controlTextStyle);

            if (!control.Draw(ref value))
                return;

            state.SetBool(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value ? "true" : "false"));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderRadio(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;

            if (cache.checkedDefault && !state.Has(key))
            {
                if (cache.radioHasIntValue)
                    state.SetInt(key, cache.radioIntValue);
                else
                    state.SetString(key, cache.radioValue);
            }

            bool isOn = cache.radioHasIntValue
                ? state.GetInt(key, -1) == cache.radioIntValue
                : string.Equals(state.GetString(key), cache.radioValue, StringComparison.Ordinal);

            var radio = NowLayout.Radio(cache.controlLabel, isOn)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasControlTextStyle)
                radio = radio.SetTextStyle(cache.controlTextStyle);

            if (!radio.Draw())
                return;

            if (cache.radioHasIntValue)
                state.SetInt(key, cache.radioIntValue);
            else
                state.SetString(key, cache.radioValue);

            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id));
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, cache.radioValue));
            ExecuteActions(FirstAttribute(node, "on-click", "onclick"), node, state, id);
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderProgress(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            float max = cache.progressMax;
            bool hasValue = cache.progressHasValue;
            float raw = cache.progressValue;

            if (cache.hasStateKey)
            {
                raw = state.GetFloat(cache.controlKey, hasValue ? raw : 0f);
                hasValue = true;
            }

            bool indeterminate = cache.progressIndeterminate || !hasValue;
            float value01 = max > 0f ? Mathf.Clamp01(raw / max) : 0f;
            var bar = NowLayout.ProgressBar(value01).SetOptions(cache.options);

            if (cache.hasExplicitId)
                bar = bar.SetId(new NowId(cache.nodeId));

            if (indeterminate)
            {
                bar = bar.SetIndeterminate();

                if (cache.progressHasTime)
                    bar = bar.SetTime(state.GetFloat(cache.progressTimeKey));
            }

            bar.Draw();
        }

        void RenderBadge(NowMarkupNodeCache cache)
        {
            var badge = NowLayout.Badge(cache.controlLabel).SetOptions(cache.options);

            if (cache.hasControlRectStyle)
                badge = badge.SetStyle(cache.controlRectStyle);

            if (cache.hasControlTextStyle)
                badge = badge.SetTextStyle(cache.controlTextStyle);

            badge.Draw();
        }

        void RenderChip(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            bool selected = cache.hasStateKey
                ? state.GetBool(key, cache.chipSelectedDefault)
                : cache.chipSelectedDefault;

            var chip = NowLayout.Chip(cache.controlLabel)
                .SetId(new NowId(id))
                .SetOptions(cache.options)
                .SetSelected(selected);

            if (cache.hasControlTextStyle)
                chip = chip.SetTextStyle(cache.controlTextStyle);

            if (!chip.Draw())
                return;

            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id));

            if (cache.hasStateKey)
            {
                selected = !selected;
                state.SetBool(key, selected);
                Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, selected ? "true" : "false"));
                ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
            }

            ExecuteActions(FirstAttribute(node, "on-click", "onclick", "action"), node, state, id);
        }

        void RenderText(NowMarkupNodeCache cache)
        {
            string value = cache.richTextContent;

            if (value.Length == 0)
                return;

            var text = NowLayout.RichText(value)
                .SetOptions(cache.options)
                .ParseDefaultTags();

            if (cache.hasExplicitId)
                text = text.SetId(new NowId(cache.nodeId));

            ApplyTextStyle(ref text, cache);
            text.Draw();
        }

        void RenderButton(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;

            var button = NowLayout.Button(cache.controlLabel)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasControlRectStyle)
                button = button.SetStyle(cache.controlRectStyle);

            if (cache.hasControlTextStyle)
                button = button.SetTextStyle(cache.controlTextStyle);

            if (!button.Draw())
                return;

            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id));
            ExecuteActions(FirstAttribute(node, "on-click", "onclick", "action"), node, state, id);
        }

        void RenderCheckbox(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            bool value = state.GetBool(key, cache.checkedDefault);

            var checkbox = NowLayout.Checkbox(cache.controlLabel)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasControlTextStyle)
                checkbox = checkbox.SetTextStyle(cache.controlTextStyle);

            if (!checkbox.Draw(ref value))
                return;

            state.SetBool(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value ? "true" : "false"));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderSlider(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            float value = state.GetFloat(key, cache.sliderDefault);

            var slider = NowLayout.Slider(cache.sliderMin, cache.sliderMax)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (cache.sliderHasStep)
                slider = slider.SetStep(cache.sliderStep);

            if (!slider.Draw(ref value))
                return;

            state.SetFloat(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value.ToString(CultureInfo.InvariantCulture)));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderTextField(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            string value = state.GetString(key, cache.textDefault);

            var field = NowLayout.TextField(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasPlaceholder)
                field = field.SetPlaceholder(cache.placeholder);

            if (cache.hasControlTextStyle)
                field = field.SetTextStyle(cache.controlTextStyle);

            if (!field.Draw(ref value))
                return;

            state.SetString(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderTextArea(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            string value = state.GetString(key, cache.textDefault);

            var area = NowLayout.TextArea(new NowId(id))
                .SetOptions(cache.options);

            if (cache.hasPlaceholder)
                area = area.SetPlaceholder(cache.placeholder);

            area = area.SetLines(cache.textMinLines, cache.textMaxLines);

            if (cache.hasControlTextStyle)
                area = area.SetTextStyle(cache.controlTextStyle);

            if (!area.Draw(ref value))
                return;

            state.SetString(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderDropdown(NowMarkupNode node, NowMarkupState state, NowMarkupNodeCache cache)
        {
            string id = cache.nodeId;
            string key = cache.controlKey;
            var options = cache.dropdownOptions;
            int selected = state.GetInt(key, cache.dropdownFallback);

            var dropdown = NowLayout.Dropdown(new NowId(id), options)
                .SetOptions(cache.options);

            if (!dropdown.Draw(ref selected))
                return;

            state.SetInt(key, selected);
            string value = selected >= 0 && selected < options.Count ? options[selected] : string.Empty;
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        static void RenderSpace(NowMarkupNodeCache cache, bool flexible)
        {
            if (flexible)
            {
                NowLayout.FlexibleSpace(cache.spaceSize);
                return;
            }

            NowLayout.Space(cache.spaceSize);
        }

        static void DrawBackground(NowMarkupNodeCache cache, NowRect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f || !cache.hasBackground)
                return;

            NowRectangle rectangle = cache.hasSurfaceStyle
                ? NowTheme.themeAsset.Rectangle(rect, cache.surfaceStyle)
                : Now.Rectangle(rect);

            if (cache.hasFillColor)
                rectangle = rectangle.SetColor(cache.fillColor);

            if (cache.hasRadius)
                rectangle = rectangle.SetRadius(cache.radius);

            rectangle.Draw();
        }

        static void ApplyTextStyle(ref NowRichText text, NowMarkupNodeCache cache)
        {
            if (cache.hasFontSize)
                text = text.SetFontSize(cache.fontSize);

            if (cache.hasTextColor)
                text = text.SetColor(cache.textColor);

            if (cache.hasFontStyle)
                text = text.SetFontStyle(cache.fontStyle);

            if (cache.selectable)
                text = text.SetSelectable();
        }

        static bool ShouldRender(NowMarkupNodeCache cache, NowMarkupState state)
        {
            switch (cache.visibility)
            {
                case NowMarkupVisibility.Literal:
                    return cache.visibilityLiteral;
                case NowMarkupVisibility.StateKey:
                {
                    bool value = state.GetBool(cache.visibilityKey, false);
                    return cache.visibilityInvert ? !value : value;
                }
                default:
                    return true;
            }
        }

        void ExecuteActions(string actions, NowMarkupNode node, NowMarkupState state, string sourceId)
        {
            if (string.IsNullOrWhiteSpace(actions))
                return;

            var commands = actions.Split(';');

            for (int i = 0; i < commands.Length; ++i)
            {
                string command = commands[i].Trim();

                if (command.Length == 0)
                    continue;

                ExecuteAction(command, node, state, sourceId);
            }
        }

        void ExecuteAction(string command, NowMarkupNode node, NowMarkupState state, string sourceId)
        {
            string name = command;
            string args = string.Empty;
            int open = command.IndexOf('(');

            if (open >= 0 && command.EndsWith(")", StringComparison.Ordinal))
            {
                name = command.Substring(0, open).Trim();
                args = command.Substring(open + 1, command.Length - open - 2);
            }
            else
            {
                int colon = command.IndexOf(':');

                if (colon > 0)
                {
                    name = command.Substring(0, colon).Trim();
                    args = command.Substring(colon + 1);
                }
            }

            var parts = SplitArgs(args);
            name = name.Trim().ToLowerInvariant();

            switch (name)
            {
                case "emit":
                    if (parts.Count > 0)
                        Record(new NowMarkupEvent(NowMarkupEventKind.Action, sourceId, parts[0]));
                    break;
                case "toggle":
                    if (parts.Count > 0)
                        state.Toggle(parts[0]);
                    break;
                case "show":
                    if (parts.Count > 0)
                        state.SetBool(parts[0], true);
                    break;
                case "hide":
                    if (parts.Count > 0)
                        state.SetBool(parts[0], false);
                    break;
                case "set":
                    if (parts.Count >= 2)
                        SetStateValue(state, parts[0], parts[1]);
                    break;
                case "add":
                    if (parts.Count >= 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float delta))
                        state.SetFloat(parts[0], state.GetFloat(parts[0]) + delta);
                    break;
                case "next":
                    if (parts.Count > 0)
                        state.StepInt(parts[0], 1, parts.Count > 1 ? ParseInt(parts[1], 0) : 0);
                    break;
                case "prev":
                case "previous":
                    if (parts.Count > 0)
                        state.StepInt(parts[0], -1, parts.Count > 1 ? ParseInt(parts[1], 0) : 0);
                    break;
            }
        }

        static void SetStateValue(NowMarkupState state, string key, string value)
        {
            if (NowMarkupState.TryParseBool(value, out bool boolValue))
            {
                state.SetBool(key, boolValue);
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                state.SetInt(key, intValue);
                return;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            {
                state.SetFloat(key, floatValue);
                return;
            }

            state.SetString(key, value);
        }

        static List<string> SplitArgs(string args)
        {
            var parts = new List<string>(4);

            if (string.IsNullOrWhiteSpace(args))
                return parts;

            int start = 0;

            while (start <= args.Length)
            {
                int comma = args.IndexOf(',', start);
                int end = comma >= 0 ? comma : args.Length;
                string part = args.Substring(start, end - start).Trim().Trim('"', '\'');

                if (part.Length > 0)
                    parts.Add(part);

                if (comma < 0)
                    break;

                start = comma + 1;
            }

            return parts;
        }

        void Record(NowMarkupEvent item)
        {
            _events.Add(item);
        }

        static bool HasId(NowMarkupNode node)
        {
            return node.HasAttribute("id") || node.HasAttribute("name");
        }

        static string FirstAttribute(NowMarkupNode node, string first, string second)
        {
            if (node.TryAttribute(first, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();

            if (node.TryAttribute(second, out value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();

            return string.Empty;
        }

        static string FirstAttribute(NowMarkupNode node, string first, string second, string third)
        {
            string value = FirstAttribute(node, first, second);

            if (value.Length > 0)
                return value;

            return node.TryAttribute(third, out var raw) && !string.IsNullOrWhiteSpace(raw)
                ? raw.Trim()
                : string.Empty;
        }

        static bool ReadAttributeBool(NowMarkupNode node, string name, bool fallback)
        {
            return node.TryAttribute(name, out var value) && NowMarkupState.TryParseBool(value, out bool parsed)
                ? parsed
                : fallback;
        }

        static bool ReadBool(NowMarkupNode node, NowMarkupStyleMap style, string name, bool fallback)
        {
            if (node.TryAttribute(name, out var attr) && NowMarkupState.TryParseBool(attr, out bool attrBool))
                return attrBool;

            if (style.TryGetBool(name, out bool styleBool))
                return styleBool;

            return fallback;
        }

        static int ReadInt(NowMarkupNode node, string name, int fallback)
        {
            return node.TryAttribute(name, out var value) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        static float ReadFloat(NowMarkupNode node, NowMarkupStyleMap style, string primary, string secondary, float fallback)
        {
            if (TryReadFloat(node, style, primary, out float value))
                return value;

            if (!string.IsNullOrEmpty(secondary) && TryReadFloat(node, style, secondary, out value))
                return value;

            return fallback;
        }

        static bool TryReadFloat(NowMarkupNode node, NowMarkupStyleMap style, string name, out float value)
        {
            value = 0f;

            if (node.TryAttribute(name, out var attr) &&
                float.TryParse(TrimUnit(attr), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return style.TryGetFloat(name, out value);
        }

        static int BuildOptions(NowMarkupNode node, List<string> options)
        {
            options.Clear();
            int selected = -1;

            if (node.TryAttribute("options", out var optionText))
            {
                var parts = optionText.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < parts.Length; ++i)
                    options.Add(parts[i].Trim());
            }

            for (int i = 0; i < node.children.Count; ++i)
            {
                var child = node.children[i];

                if (child.isText || child.name != "option")
                    continue;

                string label = child.TryAttribute("label", out var labelAttr)
                    ? labelAttr
                    : PlainTextContent(child).Trim();

                if (label.Length == 0)
                    continue;

                if (ReadAttributeBool(child, "selected", false))
                    selected = options.Count;

                options.Add(label);
            }

            return selected;
        }

        static List<NowMarkupNode> CollectRenderableChildren(NowMarkupNode node)
        {
            var result = new List<NowMarkupNode>(node.children.Count);

            for (int i = 0; i < node.children.Count; ++i)
            {
                var child = node.children[i];

                if (child.isText && string.IsNullOrWhiteSpace(child.text))
                    continue;

                if (child.name == "style")
                    continue;

                result.Add(child);
            }

            return result;
        }

        static string PlainTextContent(NowMarkupNode node)
        {
            var builder = new StringBuilder();
            AppendPlainText(node, builder);
            return CleanText(builder.ToString());
        }

        static void AppendPlainText(NowMarkupNode node, StringBuilder builder)
        {
            if (node.isText)
            {
                builder.Append(node.text);
                return;
            }

            if (node.name == "br")
                builder.Append('\n');

            for (int i = 0; i < node.children.Count; ++i)
                AppendPlainText(node.children[i], builder);
        }

        static string RichTextContent(NowMarkupNode node)
        {
            var builder = new StringBuilder();

            for (int i = 0; i < node.children.Count; ++i)
                AppendRichText(node.children[i], builder);

            return CleanText(builder.ToString());
        }

        static void AppendRichText(NowMarkupNode node, StringBuilder builder)
        {
            if (node.isText)
            {
                AppendEscapedRichText(node.text, builder);
                return;
            }

            if (!IsRichTextInline(node.name))
            {
                AppendPlainText(node, builder);
                return;
            }

            if (node.name == "br")
            {
                builder.Append("<br/>");
                return;
            }

            string name = MapInlineName(node.name);
            builder.Append('<').Append(name);

            foreach (var attribute in node.attributes)
            {
                if (attribute.Key == "style" || attribute.Key == "class" || attribute.Key == "id")
                    continue;

                builder.Append(' ').Append(attribute.Key).Append("=\"");
                AppendEscapedAttribute(attribute.Value, builder);
                builder.Append('"');
            }

            builder.Append('>');

            for (int i = 0; i < node.children.Count; ++i)
                AppendRichText(node.children[i], builder);

            builder.Append("</").Append(name).Append('>');
        }

        static bool IsRichTextInline(string name)
        {
            switch (name)
            {
                case "b":
                case "strong":
                case "i":
                case "em":
                case "u":
                case "s":
                case "del":
                case "strike":
                case "strikethrough":
                case "color":
                case "size":
                case "link":
                case "noparse":
                case "br":
                    return true;
                default:
                    return false;
            }
        }

        static string MapInlineName(string name)
        {
            switch (name)
            {
                case "strong":
                    return "b";
                case "em":
                    return "i";
                case "del":
                case "strike":
                case "strikethrough":
                    return "s";
                default:
                    return name;
            }
        }

        static void AppendEscapedRichText(string value, StringBuilder builder)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; ++i)
            {
                switch (value[i])
                {
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '&':
                        builder.Append("&amp;");
                        break;
                    default:
                        builder.Append(value[i]);
                        break;
                }
            }
        }

        static void AppendEscapedAttribute(string value, StringBuilder builder)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; ++i)
            {
                if (value[i] == '"')
                    builder.Append("&quot;");
                else
                    builder.Append(value[i]);
            }
        }

        static string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim();
        }

        static string TrimUnit(string value)
        {
            value = value?.Trim() ?? string.Empty;

            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - 2).Trim();

            return value;
        }
    }
}
