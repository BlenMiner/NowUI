using System;
using System.Collections.Generic;
using System.Text;

namespace NowUI.Markup
{
    /// <summary>
    /// Small forgiving XML-like parser for AI-authored NowUI markup. It is not an
    /// HTML parser: unknown tags are preserved as nodes, style blocks are raw
    /// text, and malformed tags fall back to literal text.
    /// </summary>
    public static class NowMarkupParser
    {
        static readonly HashSet<string> VoidTags = new HashSet<string>
        {
            "br", "hr", "space", "flex", "slider", "textfield", "input", "progress"
        };

        public static NowMarkupNode Parse(string source)
        {
            source ??= string.Empty;

            var root = new NowMarkupNode
            {
                type = NowMarkupNodeType.Element,
                name = "document"
            };

            var stack = new List<NowMarkupNode> { root };
            int i = 0;

            while (i < source.Length)
            {
                int tagStart = source.IndexOf('<', i);

                if (tagStart < 0)
                {
                    AddText(stack[stack.Count - 1], DecodeEntities(source.Substring(i)), i);
                    break;
                }

                if (tagStart > i)
                    AddText(stack[stack.Count - 1], DecodeEntities(source.Substring(i, tagStart - i)), i);

                if (StartsWith(source, tagStart, "<!--"))
                {
                    int endComment = source.IndexOf("-->", tagStart + 4, StringComparison.Ordinal);
                    i = endComment >= 0 ? endComment + 3 : source.Length;
                    continue;
                }

                if (tagStart + 1 < source.Length && source[tagStart + 1] == '/')
                {
                    int close = source.IndexOf('>', tagStart + 2);

                    if (close < 0)
                    {
                        AddText(stack[stack.Count - 1], DecodeEntities(source.Substring(tagStart)), tagStart);
                        break;
                    }

                    string name = NormalizeName(source.Substring(tagStart + 2, close - tagStart - 2));
                    Close(stack, name);
                    i = close + 1;
                    continue;
                }

                if (tagStart + 1 < source.Length && (source[tagStart + 1] == '!' || source[tagStart + 1] == '?'))
                {
                    int close = source.IndexOf('>', tagStart + 1);
                    i = close >= 0 ? close + 1 : source.Length;
                    continue;
                }

                if (!TryReadOpenTag(source, tagStart, out var node, out int tagLength, out bool selfClosing))
                {
                    AddText(stack[stack.Count - 1], "<", tagStart);
                    i = tagStart + 1;
                    continue;
                }

                stack[stack.Count - 1].children.Add(node);
                i = tagStart + tagLength;

                if (node.name == "style" && !selfClosing)
                {
                    int close = IndexOfIgnoreCase(source, "</style>", i);
                    int styleEnd = close >= 0 ? close : source.Length;
                    string css = source.Substring(i, styleEnd - i);
                    AddText(node, css, i);
                    i = close >= 0 ? close + "</style>".Length : source.Length;
                    continue;
                }

                if (!selfClosing && !VoidTags.Contains(node.name))
                    stack.Add(node);
            }

            return root;
        }

        static void AddText(NowMarkupNode parent, string text, int sourceIndex)
        {
            if (string.IsNullOrEmpty(text))
                return;

            parent.children.Add(new NowMarkupNode
            {
                type = NowMarkupNodeType.Text,
                name = "#text",
                text = text,
                sourceIndex = sourceIndex
            });
        }

        static bool TryReadOpenTag(
            string source,
            int start,
            out NowMarkupNode node,
            out int length,
            out bool selfClosing)
        {
            node = null;
            length = 0;
            selfClosing = false;

            int close = FindTagEnd(source, start + 1);

            if (close < 0)
                return false;

            string inner = source.Substring(start + 1, close - start - 1).Trim();

            if (inner.Length == 0 || inner[0] == '/')
                return false;

            selfClosing = inner.EndsWith("/", StringComparison.Ordinal);

            if (selfClosing)
                inner = inner.Substring(0, inner.Length - 1).TrimEnd();

            int cursor = 0;

            while (cursor < inner.Length && !char.IsWhiteSpace(inner[cursor]) && inner[cursor] != '=')
                ++cursor;

            if (cursor == 0)
                return false;

            string name = NormalizeName(inner.Substring(0, cursor));

            node = new NowMarkupNode
            {
                type = NowMarkupNodeType.Element,
                name = name,
                sourceIndex = start
            };

            while (cursor < inner.Length)
            {
                while (cursor < inner.Length && char.IsWhiteSpace(inner[cursor]))
                    ++cursor;

                if (cursor >= inner.Length)
                    break;

                int keyStart = cursor;

                while (cursor < inner.Length && !char.IsWhiteSpace(inner[cursor]) && inner[cursor] != '=')
                    ++cursor;

                if (cursor == keyStart)
                    break;

                string key = NormalizeName(inner.Substring(keyStart, cursor - keyStart));
                string value = "true";

                while (cursor < inner.Length && char.IsWhiteSpace(inner[cursor]))
                    ++cursor;

                if (cursor < inner.Length && inner[cursor] == '=')
                {
                    ++cursor;

                    while (cursor < inner.Length && char.IsWhiteSpace(inner[cursor]))
                        ++cursor;

                    value = ReadAttributeValue(inner, ref cursor);
                }

                if (key.Length > 0)
                    node.attributes[key] = DecodeEntities(value);
            }

            length = close - start + 1;
            selfClosing = selfClosing || VoidTags.Contains(name);
            return true;
        }

        static int FindTagEnd(string source, int cursor)
        {
            char quote = '\0';

            for (int i = cursor; i < source.Length; ++i)
            {
                char c = source[i];

                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }

                if (c == '>')
                    return i;
            }

            return -1;
        }

        static string ReadAttributeValue(string inner, ref int cursor)
        {
            if (cursor >= inner.Length)
                return string.Empty;

            char quote = inner[cursor];

            if (quote == '"' || quote == '\'')
            {
                ++cursor;
                int start = cursor;

                while (cursor < inner.Length && inner[cursor] != quote)
                    ++cursor;

                string value = inner.Substring(start, cursor - start);

                if (cursor < inner.Length)
                    ++cursor;

                return value;
            }

            int valueStart = cursor;

            while (cursor < inner.Length && !char.IsWhiteSpace(inner[cursor]))
                ++cursor;

            return inner.Substring(valueStart, cursor - valueStart);
        }

        static void Close(List<NowMarkupNode> stack, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            for (int i = stack.Count - 1; i > 0; --i)
            {
                if (stack[i].name != name)
                    continue;

                for (int s = stack.Count - 1; s >= i; --s)
                    stack.RemoveAt(s);

                return;
            }
        }

        static string DecodeEntities(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('&') < 0)
                return value ?? string.Empty;

            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length;)
            {
                if (value[i] == '&' && TryReadEntity(value, i, out string entity, out int length))
                {
                    builder.Append(entity);
                    i += length;
                    continue;
                }

                builder.Append(value[i]);
                ++i;
            }

            return builder.ToString();
        }

        static bool TryReadEntity(string value, int index, out string entity, out int length)
        {
            entity = null;
            length = 0;

            if (StartsWith(value, index, "&lt;")) { entity = "<"; length = 4; return true; }
            if (StartsWith(value, index, "&gt;")) { entity = ">"; length = 4; return true; }
            if (StartsWith(value, index, "&amp;")) { entity = "&"; length = 5; return true; }
            if (StartsWith(value, index, "&quot;")) { entity = "\""; length = 6; return true; }
            if (StartsWith(value, index, "&apos;")) { entity = "'"; length = 6; return true; }

            return false;
        }

        static bool StartsWith(string value, int index, string probe)
        {
            if (index < 0 || index + probe.Length > value.Length)
                return false;

            for (int i = 0; i < probe.Length; ++i)
            {
                if (value[index + i] != probe[i])
                    return false;
            }

            return true;
        }

        static int IndexOfIgnoreCase(string source, string value, int start)
        {
            return source.IndexOf(value, start, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : name.Trim().ToLowerInvariant();
        }
    }
}
