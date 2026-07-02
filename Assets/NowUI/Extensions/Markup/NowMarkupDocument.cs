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
        public List<string> dropdownOptions;
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
        readonly NowMarkupState _ownedState = new NowMarkupState();
        readonly List<NowMarkupEvent> _events = new List<NowMarkupEvent>(8);
        int _eventsVersion;

        public NowMarkupNode root => _root;

        public NowMarkupDocument(NowMarkupNode root)
        {
            _root = root ?? NowMarkupParser.Parse(string.Empty);
            _styleSheet = NowMarkupStyleSheet.FromDocument(_root);
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
                    RenderText(cache);
                    break;
                case "button":
                    RenderButton(node, state, cache);
                    break;
                case "checkbox":
                    RenderCheckbox(node, state, cache);
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
                BuildOptions(node, cache.dropdownOptions);
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

            int selected = state.GetInt(key, ReadInt(node, "selected", 0));

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

        static void BuildOptions(NowMarkupNode node, List<string> options)
        {
            options.Clear();

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

                if (label.Length > 0)
                    options.Add(label);
            }
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

            builder.Append('<').Append(node.name);

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

            builder.Append("</").Append(node.name).Append('>');
        }

        static bool IsRichTextInline(string name)
        {
            switch (name)
            {
                case "b":
                case "i":
                case "u":
                case "s":
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
