using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NowUI.Markup
{
    internal sealed class NowMarkupStyleMap
    {
        readonly Dictionary<string, Entry> _values = new Dictionary<string, Entry>(16);

        struct Entry
        {
            public string value;
            public int specificity;
            public int order;
        }

        public void Set(string name, string value, int specificity = 0, int order = 0)
        {
            name = NormalizeProperty(name);

            if (name.Length == 0)
                return;

            if (_values.TryGetValue(name, out var existing) &&
                (existing.specificity > specificity ||
                    (existing.specificity == specificity && existing.order > order)))
            {
                return;
            }

            _values[name] = new Entry
            {
                value = value?.Trim() ?? string.Empty,
                specificity = specificity,
                order = order
            };
        }

        public bool TryGet(string name, out string value)
        {
            if (_values.TryGetValue(NormalizeProperty(name), out var entry))
            {
                value = entry.value;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryGetAny(out string value, params string[] names)
        {
            for (int i = 0; i < names.Length; ++i)
            {
                if (TryGet(names[i], out value))
                    return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            return TryGet(name, out var raw) &&
                float.TryParse(TrimUnit(raw), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetBool(string name, out bool value)
        {
            value = false;
            return TryGet(name, out var raw) && NowMarkupState.TryParseBool(raw, out value);
        }

        public static NowMarkupStyleMap ParseInline(string style, int specificity = 1000, int order = int.MaxValue)
        {
            var map = new NowMarkupStyleMap();
            ParseDeclarations(style, (name, value) => map.Set(name, value, specificity, order));
            return map;
        }

        public static void ParseDeclarations(string declarations, Action<string, string> add)
        {
            if (string.IsNullOrWhiteSpace(declarations) || add == null)
                return;

            int start = 0;

            while (start < declarations.Length)
            {
                int semi = declarations.IndexOf(';', start);
                int end = semi >= 0 ? semi : declarations.Length;
                string part = declarations.Substring(start, end - start);
                int colon = part.IndexOf(':');

                if (colon > 0)
                    add(part.Substring(0, colon).Trim(), part.Substring(colon + 1).Trim());

                if (semi < 0)
                    break;

                start = semi + 1;
            }
        }

        public static string NormalizeProperty(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant().Replace("_", "-");
        }

        static string TrimUnit(string value)
        {
            value = value?.Trim() ?? string.Empty;

            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - 2).Trim();

            return value;
        }
    }

    internal sealed class NowMarkupStyleSheet
    {
        readonly List<Rule> _rules = new List<Rule>(8);

        struct Rule
        {
            public string selector;
            public int specificity;
            public int order;
            public List<KeyValuePair<string, string>> declarations;
        }

        public static NowMarkupStyleSheet FromDocument(NowMarkupNode root)
        {
            var sheet = new NowMarkupStyleSheet();
            CollectStyleBlocks(root, sheet);
            return sheet;
        }

        public NowMarkupStyleMap Resolve(NowMarkupNode node)
        {
            var map = new NowMarkupStyleMap();

            for (int i = 0; i < _rules.Count; ++i)
            {
                var rule = _rules[i];

                if (!Matches(node, rule.selector))
                    continue;

                for (int d = 0; d < rule.declarations.Count; ++d)
                {
                    var declaration = rule.declarations[d];
                    map.Set(declaration.Key, declaration.Value, rule.specificity, rule.order);
                }
            }

            foreach (var attribute in node.attributes)
            {
                if (IsStyleAttribute(attribute.Key))
                    map.Set(attribute.Key, attribute.Value, 900, int.MaxValue - 1);
            }

            if (node.TryAttribute("style", out var inlineStyle))
            {
                NowMarkupStyleMap.ParseDeclarations(
                    inlineStyle,
                    (name, value) => map.Set(name, value, 1000, int.MaxValue));
            }

            return map;
        }

        static bool IsStyleAttribute(string name)
        {
            switch (NowMarkupStyleMap.NormalizeProperty(name))
            {
                case "width":
                case "height":
                case "min-width":
                case "max-width":
                case "min-height":
                case "max-height":
                case "stretch":
                case "stretch-width":
                case "stretch-height":
                case "grow":
                case "grow-x":
                case "grow-y":
                case "padding":
                case "pad":
                case "gap":
                case "spacing":
                case "align":
                case "align-items":
                case "font-size":
                case "size":
                case "color":
                case "font-style":
                case "selectable":
                case "variant":
                case "rect-style":
                case "text-style":
                case "background":
                case "background-color":
                case "bg":
                case "radius":
                case "border-radius":
                case "controls":
                    return true;
                default:
                    return false;
            }
        }

        void AddCss(string css)
        {
            if (string.IsNullOrWhiteSpace(css))
                return;

            int cursor = 0;
            int order = _rules.Count;

            while (cursor < css.Length)
            {
                int open = css.IndexOf('{', cursor);

                if (open < 0)
                    break;

                int close = css.IndexOf('}', open + 1);

                if (close < 0)
                    break;

                string selectors = css.Substring(cursor, open - cursor);
                string declarations = css.Substring(open + 1, close - open - 1);
                var parsedDeclarations = new List<KeyValuePair<string, string>>();
                NowMarkupStyleMap.ParseDeclarations(
                    declarations,
                    (name, value) => parsedDeclarations.Add(new KeyValuePair<string, string>(name, value)));

                if (parsedDeclarations.Count > 0)
                {
                    var selectorParts = selectors.Split(',');

                    for (int i = 0; i < selectorParts.Length; ++i)
                    {
                        string selector = selectorParts[i].Trim().ToLowerInvariant();

                        if (selector.Length == 0)
                            continue;

                        _rules.Add(new Rule
                        {
                            selector = selector,
                            specificity = Specificity(selector),
                            order = order++,
                            declarations = parsedDeclarations
                        });
                    }
                }

                cursor = close + 1;
            }
        }

        static void CollectStyleBlocks(NowMarkupNode node, NowMarkupStyleSheet sheet)
        {
            if (node == null)
                return;

            if (node.name == "style")
            {
                sheet.AddCss(TextContent(node));
                return;
            }

            for (int i = 0; i < node.children.Count; ++i)
                CollectStyleBlocks(node.children[i], sheet);
        }

        static string TextContent(NowMarkupNode node)
        {
            if (node.isText)
                return node.text ?? string.Empty;

            var builder = new System.Text.StringBuilder();

            for (int i = 0; i < node.children.Count; ++i)
                builder.Append(TextContent(node.children[i]));

            return builder.ToString();
        }

        static bool Matches(NowMarkupNode node, string selector)
        {
            if (node == null || node.isText || string.IsNullOrEmpty(selector))
                return false;

            string id = node.Attribute("id");
            string classes = node.Attribute("class");

            if (selector[0] == '#')
                return string.Equals(id, selector.Substring(1), StringComparison.Ordinal);

            if (selector[0] == '.')
                return HasClass(classes, selector.Substring(1));

            int dot = selector.IndexOf('.');

            if (dot > 0)
            {
                string tag = selector.Substring(0, dot);
                string cls = selector.Substring(dot + 1);
                return node.name == tag && HasClass(classes, cls);
            }

            return node.name == selector;
        }

        static int Specificity(string selector)
        {
            selector = selector?.Trim() ?? string.Empty;

            if (selector.StartsWith("#", StringComparison.Ordinal))
                return 100;

            if (selector.StartsWith(".", StringComparison.Ordinal))
                return 10;

            return selector.IndexOf('.') >= 0 ? 11 : 1;
        }

        static bool HasClass(string classes, string cls)
        {
            if (string.IsNullOrWhiteSpace(classes) || string.IsNullOrWhiteSpace(cls))
                return false;

            var parts = classes.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; ++i)
            {
                if (string.Equals(parts[i], cls, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    internal static class NowMarkupStyleHelpers
    {
        public static NowLayoutOptions LayoutOptions(NowMarkupStyleMap style)
        {
            var options = default(NowLayoutOptions);

            if (style.TryGetFloat("width", out float width))
                options = options.SetWidth(width);

            if (style.TryGetFloat("height", out float height))
                options = options.SetHeight(height);

            if (style.TryGetFloat("min-width", out float minWidth))
                options = options.SetMinWidth(minWidth);

            if (style.TryGetFloat("max-width", out float maxWidth))
                options = options.SetMaxWidth(maxWidth);

            if (style.TryGetFloat("min-height", out float minHeight))
                options = options.SetMinHeight(minHeight);

            if (style.TryGetFloat("max-height", out float maxHeight))
                options = options.SetMaxHeight(maxHeight);

            if (style.TryGetAny(out var stretchWidth, "stretch-width", "grow-x") && TryParseStretch(stretchWidth, out float sx))
                options = options.SetStretchWidth(sx);

            if (style.TryGetAny(out var stretchHeight, "stretch-height", "grow-y") && TryParseStretch(stretchHeight, out float sy))
                options = options.SetStretchHeight(sy);

            if (style.TryGetAny(out var stretch, "stretch", "grow") && TryParseStretch(stretch, out float s))
                options = options.SetStretchWidth(s);

            if (style.TryGetAny(out var padding, "padding", "pad") && TryParseSpacing(padding, out var spacing))
                options = options.SetPadding(spacing);

            if (style.TryGetAny(out var gap, "gap", "spacing") &&
                float.TryParse(TrimUnit(gap), NumberStyles.Float, CultureInfo.InvariantCulture, out float gapValue))
            {
                options = options.SetSpacing(gapValue);
            }

            if (style.TryGet("align", out var align) && TryParseAlign(align, out var parsedAlign))
                options = options.SetAlign(parsedAlign);

            if (style.TryGet("align-items", out var alignItems) && TryParseAlign(alignItems, out var parsedAlignItems))
                options = options.SetAlignItems(parsedAlignItems);

            return options;
        }

        public static bool TryParseAlign(string value, out NowLayoutAlign align)
        {
            align = NowLayoutAlign.Start;

            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "start":
                case "left":
                case "top":
                    align = NowLayoutAlign.Start;
                    return true;
                case "center":
                case "middle":
                    align = NowLayoutAlign.Center;
                    return true;
                case "end":
                case "right":
                case "bottom":
                    align = NowLayoutAlign.End;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryParseSpacing(string value, out Vector4 spacing)
        {
            spacing = default;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var numbers = new List<float>(4);

            for (int i = 0; i < parts.Length; ++i)
            {
                if (!float.TryParse(TrimUnit(parts[i]), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    return false;

                numbers.Add(parsed);
            }

            switch (numbers.Count)
            {
                case 1:
                    spacing = new Vector4(numbers[0], numbers[0], numbers[0], numbers[0]);
                    return true;
                case 2:
                    spacing = new Vector4(numbers[1], numbers[0], numbers[1], numbers[0]);
                    return true;
                case 4:
                    spacing = new Vector4(numbers[0], numbers[1], numbers[2], numbers[3]);
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryParseColor(string value, out Color color)
        {
            color = default;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                color = Color.clear;
                return true;
            }

            if (value.Equals("white", StringComparison.OrdinalIgnoreCase))
            {
                color = Color.white;
                return true;
            }

            if (value.Equals("black", StringComparison.OrdinalIgnoreCase))
            {
                color = Color.black;
                return true;
            }

            if (!value.StartsWith("#", StringComparison.Ordinal))
                value = "#" + value;

            return ColorUtility.TryParseHtmlString(value, out color);
        }

        public static bool TryParseRectangleStyle(string value, out NowRectangleStyle style)
        {
            return Enum.TryParse(value, true, out style);
        }

        public static bool TryParseTextStyle(string value, out NowTextStyle style)
        {
            return Enum.TryParse(value, true, out style);
        }

        public static bool TryParseFontStyle(string value, out NowFontStyle fontStyle)
        {
            fontStyle = NowFontStyle.Regular;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Split(new[] { ' ', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; ++i)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "regular":
                    case "normal":
                        break;
                    case "bold":
                        fontStyle |= NowFontStyle.Bold;
                        break;
                    case "italic":
                        fontStyle |= NowFontStyle.Italic;
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        static bool TryParseStretch(string value, out float weight)
        {
            weight = 1f;

            if (NowMarkupState.TryParseBool(value, out bool boolValue))
            {
                if (!boolValue)
                    return false;

                weight = 1f;
                return true;
            }

            return float.TryParse(TrimUnit(value), NumberStyles.Float, CultureInfo.InvariantCulture, out weight) &&
                weight > 0f;
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
