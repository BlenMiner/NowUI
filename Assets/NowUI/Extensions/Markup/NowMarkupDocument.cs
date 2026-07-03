using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NowUI.Markup
{
    /// <summary>
    /// Per-node render data resolved once from the immutable document and its
    /// stylesheet, so steady-state drawing does not re-derive styles, ids, or
    /// content strings.
    /// </summary>
    internal sealed class NowMarkupNodeCache
    {
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

            if (!ShouldRender(node, state))
                return;

            switch (node.name)
            {
                case "column":
                case "vstack":
                case "div":
                case "section":
                case "panel":
                case "card":
                    RenderGroup(node, state, cache, vertical: true);
                    break;
                case "row":
                case "hstack":
                    RenderGroup(node, state, cache, vertical: false);
                    break;
                case "if":
                case "show":
                    RenderChildren(node, state);
                    break;
                case "scroll":
                case "scrollview":
                    RenderScroll(node, state, cache);
                    break;
                case "gallery":
                    RenderGallery(node, state, cache);
                    break;
                case "slide":
                case "item":
                    RenderChildren(node, state);
                    break;
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
                    RenderText(cache);
                    break;
                case "hr":
                case "divider":
                    RenderDivider(cache);
                    break;
                case "ul":
                case "ol":
                    RenderList(node, state, cache);
                    break;
                case "details":
                    RenderDetails(node, state, cache);
                    break;
                case "tabs":
                case "tabview":
                    RenderTabs(node, state, cache);
                    break;
                case "button":
                    RenderButton(node, state, cache);
                    break;
                case "checkbox":
                    RenderCheckbox(node, state, cache);
                    break;
                case "switch":
                case "toggle":
                    RenderSwitch(node, state, cache);
                    break;
                case "radio":
                    RenderRadio(node, state, cache);
                    break;
                case "progress":
                case "progressbar":
                    RenderProgress(node, state, cache);
                    break;
                case "badge":
                    RenderBadge(node, cache);
                    break;
                case "chip":
                    RenderChip(node, state, cache);
                    break;
                case "slider":
                    RenderSlider(node, state, cache);
                    break;
                case "textfield":
                case "input":
                    RenderTextField(node, state, cache);
                    break;
                case "textarea":
                    RenderTextArea(node, state, cache);
                    break;
                case "dropdown":
                case "select":
                    RenderDropdown(node, state, cache);
                    break;
                case "space":
                    RenderSpace(node, cache.style, flexible: false);
                    break;
                case "flex":
                case "flexspace":
                    RenderSpace(node, cache.style, flexible: true);
                    break;
                case "br":
                    NowLayout.Space(ReadFloat(node, cache.style, "height", "size", 8f));
                    break;
                default:
                    RenderChildren(node, state);
                    break;
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

            node.renderCache = cache;
            return cache;
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

                if (ReadBool(node, cache.style, "controls", false))
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

                    if (!ShouldRender(child, state))
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

                if (ReadBool(node, cache.style, "stretch-tabs", false))
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
            bool fallback = ReadAttributeBool(node, "checked", ReadAttributeBool(node, "on", false));
            bool value = state.GetBool(key, fallback);
            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;

            var control = NowLayout.Switch(label)
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

            if (ReadAttributeBool(node, "checked", false) && !state.Has(key))
            {
                if (cache.radioHasIntValue)
                    state.SetInt(key, cache.radioIntValue);
                else
                    state.SetString(key, cache.radioValue);
            }

            bool isOn = cache.radioHasIntValue
                ? state.GetInt(key, -1) == cache.radioIntValue
                : string.Equals(state.GetString(key), cache.radioValue, StringComparison.Ordinal);

            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;

            var radio = NowLayout.Radio(label, isOn)
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
            float max = ReadFloat(node, cache.style, "max", null, 1f);
            bool hasValue = TryReadFloat(node, cache.style, "value", out float raw);

            if (cache.hasStateKey)
            {
                raw = state.GetFloat(cache.controlKey, hasValue ? raw : 0f);
                hasValue = true;
            }

            bool indeterminate = ReadBool(node, cache.style, "indeterminate", false) || !hasValue;
            float value01 = max > 0f ? Mathf.Clamp01(raw / max) : 0f;
            var bar = NowLayout.ProgressBar(value01).SetOptions(cache.options);

            if (cache.hasExplicitId)
                bar = bar.SetId(new NowId(cache.nodeId));

            if (indeterminate)
            {
                bar = bar.SetIndeterminate();

                if (node.TryAttribute("time", out var timeKey))
                    bar = bar.SetTime(state.GetFloat(timeKey));
            }

            bar.Draw();
        }

        void RenderBadge(NowMarkupNode node, NowMarkupNodeCache cache)
        {
            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;

            var badge = NowLayout.Badge(label).SetOptions(cache.options);

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
            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;
            bool selected = cache.hasStateKey
                ? state.GetBool(key, ReadAttributeBool(node, "selected", false))
                : ReadAttributeBool(node, "selected", false);

            var chip = NowLayout.Chip(label)
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
            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;

            if (string.IsNullOrEmpty(label))
                label = id;

            var button = NowLayout.Button(label)
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
            bool fallback = ReadAttributeBool(node, "checked", false);
            bool value = state.GetBool(key, fallback);
            string label = node.TryAttribute("text", out var text)
                ? text
                : cache.plainTextContent;

            var checkbox = NowLayout.Checkbox(label)
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
            float min = ReadFloat(node, cache.style, "min", "minimum", 0f);
            float max = ReadFloat(node, cache.style, "max", "maximum", 1f);
            float value = state.GetFloat(key, ReadFloat(node, cache.style, "value", "default", min));

            var slider = NowLayout.Slider(min, max)
                .SetId(new NowId(id))
                .SetOptions(cache.options);

            if (TryReadFloat(node, cache.style, "step", out float step))
                slider = slider.SetStep(step);

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
            string value = state.GetString(key, node.Attribute("value"));

            var field = NowLayout.TextField(new NowId(id))
                .SetOptions(cache.options);

            if (node.TryAttribute("placeholder", out var placeholder))
                field = field.SetPlaceholder(placeholder);

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
            string value = state.GetString(key, node.Attribute("value", cache.plainTextContent));

            var area = NowLayout.TextArea(new NowId(id))
                .SetOptions(cache.options);

            if (node.TryAttribute("placeholder", out var placeholder))
                area = area.SetPlaceholder(placeholder);

            int minLines = Mathf.Max(1, ReadInt(node, "min-lines", 3));
            int maxLines = Mathf.Max(minLines, ReadInt(node, "max-lines", 8));
            area = area.SetLines(minLines, maxLines);

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

            int fallback = ReadInt(node, "selected", cache.dropdownDefaultIndex >= 0 ? cache.dropdownDefaultIndex : 0);
            int selected = state.GetInt(key, fallback);

            var dropdown = NowLayout.Dropdown(new NowId(id), options)
                .SetOptions(cache.options);

            if (!dropdown.Draw(ref selected))
                return;

            state.SetInt(key, selected);
            string value = selected >= 0 && selected < options.Count ? options[selected] : string.Empty;
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderSpace(NowMarkupNode node, NowMarkupStyleMap style, bool flexible)
        {
            if (flexible)
            {
                NowLayout.FlexibleSpace(ReadFloat(node, style, "weight", "stretch", 1f));
                return;
            }

            NowLayout.Space(ReadFloat(node, style, "size", "height", 8f));
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

        bool ShouldRender(NowMarkupNode node, NowMarkupState state)
        {
            if (TryVisibility(node, state, "if", false, out bool visible))
                return visible;

            if (TryVisibility(node, state, "when", false, out visible))
                return visible;

            if (TryVisibility(node, state, "visible", false, out visible))
                return visible;

            if (TryVisibility(node, state, "show", false, out visible))
                return visible;

            if (TryVisibility(node, state, "hidden", false, out bool hidden))
                return !hidden;

            return true;
        }

        bool TryVisibility(NowMarkupNode node, NowMarkupState state, string attribute, bool fallback, out bool value)
        {
            value = fallback;

            if (!node.TryAttribute(attribute, out var expression))
                return false;

            value = EvalBool(expression, state, fallback);
            return true;
        }

        bool EvalBool(string expression, NowMarkupState state, bool fallback)
        {
            expression = expression?.Trim() ?? string.Empty;

            if (expression.Length == 0)
                return fallback;

            bool invert = false;

            while (expression.StartsWith("!", StringComparison.Ordinal))
            {
                invert = !invert;
                expression = expression.Substring(1).TrimStart();
            }

            bool value = NowMarkupState.TryParseBool(expression, out bool literal)
                ? literal
                : state.GetBool(expression, fallback);

            return invert ? !value : value;
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
