using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NowUI
{
    public delegate bool NowRichTextTagHandler(in NowRichTextTagContext context, out NowRichTextTagResult result);

    public readonly struct NowRichTextTagContext
    {
        public readonly string name;
        public readonly IReadOnlyDictionary<string, string> attributes;
        public readonly NowRichTextStyle style;

        public NowRichTextTagContext(string name, IReadOnlyDictionary<string, string> attributes, NowRichTextStyle style)
        {
            this.name = name;
            this.attributes = attributes;
            this.style = style;
        }

        public string Attribute(string key, string fallback = "")
        {
            return attributes != null && attributes.TryGetValue(key, out var value) ? value : fallback;
        }

        public bool TryAttribute(string key, out string value)
        {
            if (attributes != null && attributes.TryGetValue(key, out value))
                return true;

            value = string.Empty;
            return false;
        }

        public float FloatAttribute(string key, float fallback, float min = 0f)
        {
            string value = Attribute(key);

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? Mathf.Max(parsed, min)
                : fallback;
        }
    }

    public struct NowRichTextTagResult
    {
        public bool hasInline;
        public NowRichTextInline inline;

        public static NowRichTextTagResult Inline(NowRichTextInline inline)
        {
            return new NowRichTextTagResult
            {
                hasInline = true,
                inline = inline
            };
        }
    }

    public sealed class NowRichTextDocument
    {
        public string text = string.Empty;
        public readonly List<NowRichTextSpan> spans = new List<NowRichTextSpan>(16);
        public readonly List<NowRichTextInline> inlines = new List<NowRichTextInline>(4);
        public readonly List<NowRichTextTagPayload> tags = new List<NowRichTextTagPayload>(4);

        public void Clear()
        {
            text = string.Empty;
            spans.Clear();
            inlines.Clear();
            tags.Clear();
        }
    }

    public sealed class NowRichTextParser
    {
        struct StackEntry
        {
            public string name;
            public int start;
            public NowRichTextStyle style;
            public int tag;
            public bool noParse;
        }

        readonly struct DerivedTagKey : System.IEquatable<DerivedTagKey>
        {
            public readonly string name;
            public readonly NowRichTextTagHandler handler;

            public DerivedTagKey(string name, NowRichTextTagHandler handler)
            {
                this.name = name;
                this.handler = handler;
            }

            public bool Equals(DerivedTagKey other)
            {
                return string.Equals(name, other.name, System.StringComparison.Ordinal) &&
                    Equals(handler, other.handler);
            }

            public override bool Equals(object obj)
            {
                return obj is DerivedTagKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((name != null ? name.GetHashCode() : 0) * 397) ^
                        (handler != null ? handler.GetHashCode() : 0);
                }
            }
        }

        const int DerivedCacheLimit = 32;

        readonly bool _defaultTags;
        readonly Dictionary<string, NowRichTextTagHandler> _handlers;
        readonly StringBuilder _output = new StringBuilder(128);
        readonly List<StackEntry> _stack = new List<StackEntry>(8);
        Dictionary<DerivedTagKey, NowRichTextParser> _derived;
        NowRichTextParser _defaultTagsVariant;

        static readonly Dictionary<string, string> EmptyAttributes = new Dictionary<string, string>(0);

        public static NowRichTextParser Empty { get; } = new NowRichTextParser(false, null);

        public static NowRichTextParser Default { get; } = new NowRichTextParser(true, null);

        NowRichTextParser(bool defaultTags, Dictionary<string, NowRichTextTagHandler> handlers)
        {
            _defaultTags = defaultTags;
            _handlers = handlers;
        }

        /// <summary>
        /// Returns this parser extended with the built-in tags. Derived parsers
        /// are memoized, so repeated calls on the same parser return the same
        /// instance — stable inline builder chains reuse cached parse results.
        /// </summary>
        public NowRichTextParser WithDefaultTags()
        {
            if (_defaultTags)
                return this;

            if (_handlers == null || _handlers.Count == 0)
                return Default;

            return _defaultTagsVariant ??= new NowRichTextParser(true, CloneHandlers());
        }

        /// <summary>
        /// Returns this parser extended with a custom tag handler. Derived
        /// parsers are memoized per (name, handler), so repeated calls on the
        /// same parser return the same instance — stable inline builder chains
        /// reuse cached parse results instead of reparsing every frame.
        /// </summary>
        public NowRichTextParser WithTag(string name, NowRichTextTagHandler handler)
        {
            string normalized = NormalizeName(name);
            var key = new DerivedTagKey(normalized, handler);
            _derived ??= new Dictionary<DerivedTagKey, NowRichTextParser>(4);

            if (_derived.TryGetValue(key, out var derived))
                return derived;

            if (_derived.Count >= DerivedCacheLimit)
                _derived.Clear();

            var handlers = CloneHandlers();
            handlers[normalized] = handler;
            derived = new NowRichTextParser(_defaultTags, handlers);
            _derived[key] = derived;
            return derived;
        }

        Dictionary<string, NowRichTextTagHandler> CloneHandlers()
        {
            return _handlers != null
                ? new Dictionary<string, NowRichTextTagHandler>(_handlers)
                : new Dictionary<string, NowRichTextTagHandler>();
        }

        public NowRichTextDocument Parse(string source, NowRichTextStyle baseStyle)
        {
            var document = new NowRichTextDocument();
            Parse(source, baseStyle, document);
            return document;
        }

        public void Parse(string source, NowRichTextStyle baseStyle, NowRichTextDocument document)
        {
            document.Clear();
            source ??= string.Empty;

            var output = _output;
            var stack = _stack;
            output.Length = 0;
            stack.Clear();
            stack.Add(new StackEntry { name = string.Empty, start = 0, style = baseStyle });

            int i = 0;

            while (i < source.Length)
            {
                if (CurrentNoParse(stack))
                {
                    int close = IndexOfIgnoreCase(source, "</noparse>", i);

                    if (close < 0)
                    {
                        AppendDecoded(source, i, source.Length - i, output, document, stack[stack.Count - 1]);
                        i = source.Length;
                        break;
                    }

                    AppendDecoded(source, i, close - i, output, document, stack[stack.Count - 1]);
                    CloseTag(stack, "noparse");
                    i = close + "</noparse>".Length;
                    continue;
                }

                char c = source[i];

                if (c == '&' && TryEntity(source, i, out char entity, out int entityLength))
                {
                    AppendStyled(entity, output, document, stack[stack.Count - 1]);
                    i += entityLength;
                    continue;
                }

                if (c != '<' || !TryReadTag(source, i, out var tag, out int tagLength))
                {
                    AppendStyled(c, output, document, stack[stack.Count - 1]);
                    ++i;
                    continue;
                }

                if (tag.closing)
                {
                    if (!CloseTag(stack, tag.name))
                        AppendStyled(source, i, tagLength, output, document, stack[stack.Count - 1]);

                    i += tagLength;
                    continue;
                }

                if (TryDefaultTag(tag, stack[stack.Count - 1], document, output, out var entry))
                {
                    if (!tag.selfClosing && !string.IsNullOrEmpty(entry.name))
                        stack.Add(entry);

                    i += tagLength;
                    continue;
                }

                if (_handlers != null && _handlers.TryGetValue(tag.name, out var handler))
                {
                    var context = new NowRichTextTagContext(tag.name, tag.attributes, stack[stack.Count - 1].style);

                    if (handler(context, out var result) && result.hasInline)
                    {
                        var inline = result.inline;
                        inline.index = output.Length;
                        output.Append('\uFFFC');
                        document.inlines.Add(inline);
                        i += tagLength;
                        continue;
                    }
                }

                AppendStyled(source, i, tagLength, output, document, stack[stack.Count - 1]);
                i += tagLength;
            }

            document.text = output.ToString();
        }

        bool TryDefaultTag(
            in ParsedTag tag,
            in StackEntry current,
            NowRichTextDocument document,
            StringBuilder output,
            out StackEntry entry)
        {
            entry = default;

            if (!_defaultTags)
                return false;

            switch (tag.name)
            {
                case "br":
                    AppendStyled('\n', output, document, current);
                    return true;
                case "b":
                    entry = Open(tag.name, output.Length, current, current.style.fontStyle | NowFontStyle.Bold, current.tag);
                    return true;
                case "i":
                    entry = Open(tag.name, output.Length, current, current.style.fontStyle | NowFontStyle.Italic, current.tag);
                    return true;
                case "u":
                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.style = current.style.SetUnderline();
                    return true;
                case "s":
                case "strikethrough":
                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.style = current.style.SetStrikethrough();
                    return true;
                case "color":
                    if (!TryParseColor(TagValue(tag), out var color))
                        return false;

                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.style = current.style.SetColor(color);
                    return true;
                case "size":
                    if (!float.TryParse(TagValue(tag), NumberStyles.Float, CultureInfo.InvariantCulture, out float size))
                        return false;

                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.style.fontSize = Mathf.Max(size, 1f);
                    return true;
                case "link":
                {
                    string value = TagValue(tag);
                    int id = document.tags.Count + 1;
                    document.tags.Add(new NowRichTextTagPayload { name = "link", value = value });
                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.style = current.style
                        .SetColor(NowTheme.themeAsset.GetColor(NowColorToken.Accent))
                        .SetUnderline();
                    entry.tag = id;
                    return true;
                }
                case "noparse":
                    entry = current;
                    entry.name = tag.name;
                    entry.start = output.Length;
                    entry.noParse = true;
                    return true;
                default:
                    return false;
            }
        }

        static StackEntry Open(string name, int start, in StackEntry current, NowFontStyle fontStyle, int tag)
        {
            var entry = current;
            entry.name = name;
            entry.start = start;
            entry.style.fontStyle = fontStyle;
            entry.tag = tag;
            return entry;
        }

        static bool CloseTag(List<StackEntry> stack, string name)
        {
            name = NormalizeName(name);

            for (int i = stack.Count - 1; i > 0; --i)
            {
                if (stack[i].name != name)
                    continue;

                for (int s = stack.Count - 1; s >= i; --s)
                    stack.RemoveAt(s);

                return true;
            }

            return false;
        }

        static void AppendStyled(char value, StringBuilder output, NowRichTextDocument document, in StackEntry entry)
        {
            int start = output.Length;
            output.Append(value);
            AddSpan(document, entry, start, 1);
        }

        static void AppendStyled(
            string value,
            int start,
            int length,
            StringBuilder output,
            NowRichTextDocument document,
            in StackEntry entry)
        {
            if (length <= 0)
                return;

            int outputStart = output.Length;
            output.Append(value, start, length);
            AddSpan(document, entry, outputStart, length);
        }

        static void AddSpan(NowRichTextDocument document, in StackEntry entry, int start, int length)
        {
            if (length <= 0)
                return;

            if (document.spans.Count > 0)
            {
                int lastIndex = document.spans.Count - 1;
                var last = document.spans[lastIndex];

                if (last.start + last.length == start &&
                    last.tag == entry.tag &&
                    SameStyle(last.style, entry.style))
                {
                    last.length += length;
                    document.spans[lastIndex] = last;
                    return;
                }
            }

            document.spans.Add(new NowRichTextSpan(start, length, entry.style, entry.tag));
        }

        static bool SameStyle(in NowRichTextStyle a, in NowRichTextStyle b)
        {
            return Mathf.Approximately(a.fontSize, b.fontSize) &&
                a.fontStyle == b.fontStyle &&
                a.hasColor == b.hasColor &&
                (!a.hasColor || a.color == b.color) &&
                a.underline == b.underline &&
                a.strikethrough == b.strikethrough;
        }

        static bool CurrentNoParse(List<StackEntry> stack)
        {
            return stack.Count > 1 && stack[stack.Count - 1].noParse;
        }

        static void AppendDecoded(
            string source,
            int start,
            int length,
            StringBuilder output,
            NowRichTextDocument document,
            in StackEntry entry)
        {
            int end = start + length;

            for (int i = start; i < end;)
            {
                if (source[i] == '&' && TryEntity(source, i, out char entity, out int entityLength))
                {
                    AppendStyled(entity, output, document, entry);
                    i += entityLength;
                    continue;
                }

                AppendStyled(source[i], output, document, entry);
                ++i;
            }
        }

        static bool TryEntity(string source, int index, out char value, out int length)
        {
            value = default;
            length = 0;

            if (Matches(source, index, "&lt;"))
            {
                value = '<';
                length = 4;
                return true;
            }

            if (Matches(source, index, "&gt;"))
            {
                value = '>';
                length = 4;
                return true;
            }

            if (Matches(source, index, "&amp;"))
            {
                value = '&';
                length = 5;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a color tag value: 3/4/6/8-digit hex (with or without '#') or
        /// any HTML color name Unity's <see cref="ColorUtility"/> understands
        /// (red, orange, grey, ...). Results are memoized with the parsed
        /// document, so the named-color fallback never runs per frame.
        /// </summary>
        static bool TryParseColor(string value, out Vector4 color)
        {
            color = default;

            if (string.IsNullOrEmpty(value))
                return false;

            value = value.Trim();

            if (value.Length == 0)
                return false;

            string hex = value[0] == '#' ? value.Substring(1) : value;

            if (TryParseHexColor(hex, out color))
                return true;

            if (ColorUtility.TryParseHtmlString(value.ToLowerInvariant(), out Color named))
            {
                color = named;
                return true;
            }

            return false;
        }

        static bool TryParseHexColor(string value, out Vector4 color)
        {
            color = default;

            if (value.Length != 3 && value.Length != 4 && value.Length != 6 && value.Length != 8)
                return false;

            if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
                return false;

            switch (value.Length)
            {
                case 3:
                    color = new Vector4(
                        ((parsed >> 8) & 0xf) / 15f,
                        ((parsed >> 4) & 0xf) / 15f,
                        (parsed & 0xf) / 15f,
                        1f);
                    return true;
                case 4:
                    color = new Vector4(
                        ((parsed >> 12) & 0xf) / 15f,
                        ((parsed >> 8) & 0xf) / 15f,
                        ((parsed >> 4) & 0xf) / 15f,
                        (parsed & 0xf) / 15f);
                    return true;
                case 6:
                    color = new Vector4(
                        ((parsed >> 16) & 0xff) / 255f,
                        ((parsed >> 8) & 0xff) / 255f,
                        (parsed & 0xff) / 255f,
                        1f);
                    return true;
                default:
                    color = new Vector4(
                        ((parsed >> 24) & 0xff) / 255f,
                        ((parsed >> 16) & 0xff) / 255f,
                        ((parsed >> 8) & 0xff) / 255f,
                        (parsed & 0xff) / 255f);
                    return true;
            }
        }

        static string TagValue(in ParsedTag tag)
        {
            if (tag.attributes.TryGetValue("value", out var value))
                return value;

            return tag.attributes.TryGetValue(string.Empty, out value) ? value : string.Empty;
        }

        static bool TryReadTag(string source, int index, out ParsedTag tag, out int length)
        {
            tag = default;
            length = 0;

            int close = source.IndexOf('>', index + 1);

            if (close < 0)
                return false;

            string inner = source.Substring(index + 1, close - index - 1).Trim();

            if (inner.Length == 0)
                return false;

            tag.closing = inner[0] == '/';

            if (tag.closing)
                inner = inner.Substring(1).TrimStart();

            tag.selfClosing = inner.EndsWith("/");

            if (tag.selfClosing)
                inner = inner.Substring(0, inner.Length - 1).TrimEnd();

            int cursor = 0;

            while (cursor < inner.Length && !char.IsWhiteSpace(inner[cursor]) && inner[cursor] != '=')
                ++cursor;

            if (cursor == 0)
                return false;

            tag.name = NormalizeName(inner.Substring(0, cursor));
            tag.attributes = new Dictionary<string, string>();

            while (cursor < inner.Length)
            {
                while (cursor < inner.Length && char.IsWhiteSpace(inner[cursor]))
                    ++cursor;

                if (cursor >= inner.Length)
                    break;

                if (inner[cursor] == '=')
                {
                    ++cursor;
                    tag.attributes[string.Empty] = ReadAttributeValue(inner, ref cursor);
                    continue;
                }

                int keyStart = cursor;

                while (cursor < inner.Length && !char.IsWhiteSpace(inner[cursor]) && inner[cursor] != '=')
                    ++cursor;

                string key = NormalizeName(inner.Substring(keyStart, cursor - keyStart));

                while (cursor < inner.Length && char.IsWhiteSpace(inner[cursor]))
                    ++cursor;

                if (cursor < inner.Length && inner[cursor] == '=')
                {
                    ++cursor;
                    tag.attributes[key] = ReadAttributeValue(inner, ref cursor);
                }
                else
                {
                    tag.attributes[key] = string.Empty;
                }
            }

            if (tag.attributes.Count == 0)
                tag.attributes = EmptyAttributes;

            length = close - index + 1;
            return true;
        }

        static string ReadAttributeValue(string source, ref int cursor)
        {
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
                ++cursor;

            if (cursor >= source.Length)
                return string.Empty;

            char quote = source[cursor];

            if (quote == '"' || quote == '\'')
            {
                ++cursor;
                int start = cursor;

                while (cursor < source.Length && source[cursor] != quote)
                    ++cursor;

                string value = source.Substring(start, cursor - start);

                if (cursor < source.Length)
                    ++cursor;

                return value;
            }

            int valueStart = cursor;

            while (cursor < source.Length && !char.IsWhiteSpace(source[cursor]))
                ++cursor;

            return source.Substring(valueStart, cursor - valueStart);
        }

        static int IndexOfIgnoreCase(string text, string value, int start)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                text,
                value,
                start,
                CompareOptions.IgnoreCase);
        }

        static bool Matches(string text, int index, string value)
        {
            if (index + value.Length > text.Length)
                return false;

            for (int i = 0; i < value.Length; ++i)
            {
                if (text[index + i] != value[i])
                    return false;
            }

            return true;
        }

        static string NormalizeName(string name)
        {
            name ??= string.Empty;

            for (int i = 0; i < name.Length; ++i)
            {
                char c = name[i];

                if (char.IsWhiteSpace(c) || char.IsUpper(c))
                    return name.Trim().ToLowerInvariant();
            }

            return name;
        }

        struct ParsedTag
        {
            public string name;
            public Dictionary<string, string> attributes;
            public bool closing;
            public bool selfClosing;
        }
    }

    public static class NowLottieRichTextTag
    {
        public static bool Parse(in NowRichTextTagContext context, out NowRichTextTagResult result)
        {
            result = default;
            string id = context.Attribute("id", context.Attribute("name"));

            if (string.IsNullOrEmpty(id))
                return false;

            float size = context.FloatAttribute("size", context.style.fontSize);
            float width = context.FloatAttribute("width", size);
            float height = context.FloatAttribute("height", size);
            var asset = Resources.Load<NowLottieAsset>($"Lottie/{id}") ?? Resources.Load<NowLottieAsset>(id);

            if (asset == null)
                return false;

            result = NowRichTextTagResult.Inline(new NowRichTextInline
            {
                width = width,
                height = height,
                payload = asset,
                draw = Draw
            });
            return true;
        }

        static void Draw(in NowRichTextRun run, NowRect mask)
        {
            if (run.payload is not NowLottieAsset asset)
                return;

            Now.Lottie(run.rect, asset)
                .SetMask(mask)
                .SetTime(Time.time)
                .Draw();
            NowControlState.RequestRepaint();
        }
    }
}
