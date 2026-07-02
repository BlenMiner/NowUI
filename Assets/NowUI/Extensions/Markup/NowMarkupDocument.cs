using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NowUI.Markup
{
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
        readonly List<string> _optionsScratch = new List<string>(8);

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

        /// <summary>Draws into the current NowLayout group.</summary>
        public NowMarkupResult Draw(NowMarkupState state = null)
        {
            BeginFrame();
            RenderChildren(_root, state ?? _ownedState);
            return new NowMarkupResult(_events);
        }

        /// <summary>Draws inside an explicit rect by opening a root layout area.</summary>
        public NowMarkupResult Draw(NowRect rect, NowMarkupState state = null)
        {
            BeginFrame();

            using (NowLayout.Area(rect))
                RenderChildren(_root, state ?? _ownedState);

            return new NowMarkupResult(_events);
        }

        void BeginFrame()
        {
            _events.Clear();
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
                string text = CleanText(node.text);

                if (!string.IsNullOrEmpty(text))
                    NowLayout.RichText(text).ParseDefaultTags().Draw();

                return;
            }

            if (node.name == "style")
                return;

            var style = _styleSheet.Resolve(node);

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
                    RenderGroup(node, state, style, vertical: true);
                    break;
                case "row":
                case "hstack":
                    RenderGroup(node, state, style, vertical: false);
                    break;
                case "if":
                case "show":
                    RenderChildren(node, state);
                    break;
                case "scroll":
                case "scrollview":
                    RenderScroll(node, state, style);
                    break;
                case "gallery":
                    RenderGallery(node, state, style);
                    break;
                case "slide":
                case "item":
                    RenderChildren(node, state);
                    break;
                case "text":
                case "label":
                case "p":
                case "richtext":
                    RenderText(node, style);
                    break;
                case "button":
                    RenderButton(node, state, style);
                    break;
                case "checkbox":
                    RenderCheckbox(node, state, style);
                    break;
                case "slider":
                    RenderSlider(node, state, style);
                    break;
                case "textfield":
                case "input":
                    RenderTextField(node, state, style);
                    break;
                case "textarea":
                    RenderTextArea(node, state, style);
                    break;
                case "dropdown":
                case "select":
                    RenderDropdown(node, state, style);
                    break;
                case "space":
                    RenderSpace(node, style, flexible: false);
                    break;
                case "flex":
                case "flexspace":
                    RenderSpace(node, style, flexible: true);
                    break;
                case "br":
                    NowLayout.Space(ReadFloat(node, style, "height", "size", 8f));
                    break;
                default:
                    RenderChildren(node, state);
                    break;
            }
        }

        void RenderGroup(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style, bool vertical)
        {
            var options = NowMarkupStyleHelpers.LayoutOptions(style);
            NowLayoutScope scope = HasId(node)
                ? (vertical ? NowLayout.Vertical(new NowId(NodeId(node)), options) : NowLayout.Horizontal(new NowId(NodeId(node)), options))
                : (vertical ? NowLayout.Vertical(options) : NowLayout.Horizontal(options));

            using (scope)
            {
                DrawBackground(node, style, scope.rect);
                RenderChildren(node, state);
            }
        }

        void RenderScroll(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            var options = NowMarkupStyleHelpers.LayoutOptions(style);
            string id = NodeId(node);

            using (NowLayout.ScrollView(new NowId(id)).SetOptions(options).Begin())
                RenderChildren(node, state);
        }

        void RenderGallery(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = FirstAttribute(node, "index", "state");

            if (string.IsNullOrEmpty(key))
                key = id + ".index";

            var slides = CollectRenderableChildren(node);
            int count = slides.Count;

            if (count == 0)
                return;

            int previous = state.GetInt(key);
            int index = Mathf.Clamp(previous, 0, count - 1);

            if (index != previous)
                state.SetInt(key, index);

            var options = NowMarkupStyleHelpers.LayoutOptions(style);

            using (var scope = NowLayout.Vertical(new NowId(id), options))
            {
                DrawBackground(node, style, scope.rect);

                var slide = slides[index];

                if (slide.name == "slide" || slide.name == "item")
                    RenderChildren(slide, state);
                else
                    RenderNode(slide, state);

                if (ReadBool(node, style, "controls", false))
                {
                    using (NowLayout.Horizontal(spacing: 8f))
                    {
                        if (NowLayout.Button("Prev").SetId(new NowId(id + ".prev")).SetStyle(NowRectangleStyle.Outline).Draw())
                        {
                            int next = state.StepInt(key, -1, count);
                            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id + ".prev"));
                            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, next.ToString(CultureInfo.InvariantCulture)));
                        }

                        NowLayout.FlexibleSpace();

                        NowLayout.Label($"{index + 1} / {count}").Draw();

                        NowLayout.FlexibleSpace();

                        if (NowLayout.Button("Next").SetId(new NowId(id + ".next")).SetStyle(NowRectangleStyle.Outline).Draw())
                        {
                            int next = state.StepInt(key, 1, count);
                            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id + ".next"));
                            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, next.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                }
            }
        }

        void RenderText(NowMarkupNode node, NowMarkupStyleMap style)
        {
            string value = node.TryAttribute("value", out var attrValue)
                ? attrValue
                : RichTextContent(node).Trim();

            if (value.Length == 0)
                return;

            var text = NowLayout.RichText(value)
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style))
                .ParseDefaultTags();

            if (HasId(node))
                text = text.SetId(new NowId(NodeId(node)));

            ApplyTextStyle(ref text, style);
            text.Draw();
        }

        void RenderButton(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string label = node.TryAttribute("text", out var text)
                ? text
                : PlainTextContent(node).Trim();

            if (string.IsNullOrEmpty(label))
                label = id;

            var button = NowLayout.Button(label)
                .SetId(new NowId(id))
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (style.TryGetAny(out var rectStyle, "variant", "rect-style", "style") &&
                NowMarkupStyleHelpers.TryParseRectangleStyle(rectStyle, out var parsedRectStyle))
            {
                button = button.SetStyle(parsedRectStyle);
            }

            if (style.TryGet("text-style", out var textStyle) &&
                NowMarkupStyleHelpers.TryParseTextStyle(textStyle, out var parsedTextStyle))
            {
                button = button.SetTextStyle(parsedTextStyle);
            }

            if (!button.Draw())
                return;

            Record(new NowMarkupEvent(NowMarkupEventKind.Click, id));
            ExecuteActions(FirstAttribute(node, "on-click", "onclick", "action"), node, state, id);
        }

        void RenderCheckbox(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = ControlKey(node, id);
            bool fallback = ReadAttributeBool(node, "checked", false);
            bool value = state.GetBool(key, fallback);
            string label = node.TryAttribute("text", out var text)
                ? text
                : PlainTextContent(node).Trim();

            var checkbox = NowLayout.Checkbox(label)
                .SetId(new NowId(id))
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (style.TryGet("text-style", out var textStyle) &&
                NowMarkupStyleHelpers.TryParseTextStyle(textStyle, out var parsedTextStyle))
            {
                checkbox = checkbox.SetTextStyle(parsedTextStyle);
            }

            if (!checkbox.Draw(ref value))
                return;

            state.SetBool(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value ? "true" : "false"));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderSlider(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = ControlKey(node, id);
            float min = ReadFloat(node, style, "min", "minimum", 0f);
            float max = ReadFloat(node, style, "max", "maximum", 1f);
            float value = state.GetFloat(key, ReadFloat(node, style, "value", "default", min));

            var slider = NowLayout.Slider(min, max)
                .SetId(new NowId(id))
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (TryReadFloat(node, style, "step", out float step))
                slider = slider.SetStep(step);

            if (!slider.Draw(ref value))
                return;

            state.SetFloat(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value.ToString(CultureInfo.InvariantCulture)));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderTextField(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = ControlKey(node, id);
            string value = state.GetString(key, node.Attribute("value"));

            var field = NowLayout.TextField(new NowId(id))
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (node.TryAttribute("placeholder", out var placeholder))
                field = field.SetPlaceholder(placeholder);

            if (style.TryGet("text-style", out var textStyle) &&
                NowMarkupStyleHelpers.TryParseTextStyle(textStyle, out var parsedTextStyle))
            {
                field = field.SetTextStyle(parsedTextStyle);
            }

            if (!field.Draw(ref value))
                return;

            state.SetString(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderTextArea(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = ControlKey(node, id);
            string value = state.GetString(key, node.Attribute("value", PlainTextContent(node).Trim()));

            var area = NowLayout.TextArea(new NowId(id))
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (node.TryAttribute("placeholder", out var placeholder))
                area = area.SetPlaceholder(placeholder);

            int minLines = Mathf.Max(1, ReadInt(node, "min-lines", 3));
            int maxLines = Mathf.Max(minLines, ReadInt(node, "max-lines", 8));
            area = area.SetLines(minLines, maxLines);

            if (style.TryGet("text-style", out var textStyle) &&
                NowMarkupStyleHelpers.TryParseTextStyle(textStyle, out var parsedTextStyle))
            {
                area = area.SetTextStyle(parsedTextStyle);
            }

            if (!area.Draw(ref value))
                return;

            state.SetString(key, value);
            Record(new NowMarkupEvent(NowMarkupEventKind.Change, id, key, value));
            ExecuteActions(FirstAttribute(node, "on-change", "onchange"), node, state, id);
        }

        void RenderDropdown(NowMarkupNode node, NowMarkupState state, NowMarkupStyleMap style)
        {
            string id = NodeId(node);
            string key = ControlKey(node, id);
            BuildOptions(node, _optionsScratch);

            int selected = state.GetInt(key, ReadInt(node, "selected", 0));

            var dropdown = NowLayout.Dropdown(new NowId(id), _optionsScratch)
                .SetOptions(NowMarkupStyleHelpers.LayoutOptions(style));

            if (!dropdown.Draw(ref selected))
                return;

            state.SetInt(key, selected);
            string value = selected >= 0 && selected < _optionsScratch.Count ? _optionsScratch[selected] : string.Empty;
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

        void DrawBackground(NowMarkupNode node, NowMarkupStyleMap style, NowRect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            bool hasFill = style.TryGetAny(out var colorValue, "background", "background-color", "bg");
            bool hasRectStyle = style.TryGetAny(out var rectStyleValue, "rect-style", "surface", "panel-style");

            if (!hasFill && !hasRectStyle)
                return;

            NowRectangle rectangle;

            if (hasRectStyle && NowMarkupStyleHelpers.TryParseRectangleStyle(rectStyleValue, out var rectStyle))
                rectangle = NowTheme.themeAsset.Rectangle(rect, rectStyle);
            else
                rectangle = Now.Rectangle(rect);

            if (hasFill && NowMarkupStyleHelpers.TryParseColor(colorValue, out var color))
                rectangle = rectangle.SetColor(color);

            if (style.TryGetAny(out var radiusValue, "radius", "border-radius") &&
                float.TryParse(TrimUnit(radiusValue), NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
            {
                rectangle = rectangle.SetRadius(radius);
            }

            rectangle.Draw();
        }

        void ApplyTextStyle(ref NowRichText text, NowMarkupStyleMap style)
        {
            if (style.TryGetAny(out var sizeValue, "font-size", "size") &&
                float.TryParse(TrimUnit(sizeValue), NumberStyles.Float, CultureInfo.InvariantCulture, out float size))
            {
                text = text.SetFontSize(size);
            }

            if (style.TryGet("color", out var colorValue) &&
                NowMarkupStyleHelpers.TryParseColor(colorValue, out var color))
            {
                text = text.SetColor(color);
            }

            if (style.TryGet("font-style", out var fontStyleValue) &&
                NowMarkupStyleHelpers.TryParseFontStyle(fontStyleValue, out var fontStyle))
            {
                text = text.SetFontStyle(fontStyle);
            }

            if (style.TryGetBool("selectable", out bool selectable) && selectable)
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

        string NodeId(NowMarkupNode node)
        {
            string id = FirstAttribute(node, "id", "name");

            if (!string.IsNullOrEmpty(id))
                return id;

            return "markup:" + node.name + ":" + node.sourceIndex.ToString(CultureInfo.InvariantCulture);
        }

        static bool HasId(NowMarkupNode node)
        {
            return node.HasAttribute("id") || node.HasAttribute("name");
        }

        static string ControlKey(NowMarkupNode node, string fallback)
        {
            string key = FirstAttribute(node, "state", "bind", "value-key");
            return string.IsNullOrEmpty(key) ? fallback : key;
        }

        static string FirstAttribute(NowMarkupNode node, params string[] names)
        {
            for (int i = 0; i < names.Length; ++i)
            {
                if (node.TryAttribute(names[i], out var value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
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
