using System.Collections.Generic;
using System.Text;

namespace NowUI.Markdown
{
    /// <summary>
    /// Inline phase: code spans, backslash escapes, links and images
    /// (images render as linked alt text), http/https/www autolinks, hard and
    /// soft breaks, and CommonMark delimiter-run emphasis for * _ and GFM ~~.
    /// </summary>
    public static class NowMarkdownInlineParser
    {
        sealed class Item
        {
            public NowMarkdownInline node;
            public char delimiter;
            public int count;
            public bool canOpen;
            public bool canClose;
        }

        public static List<NowMarkdownInline> Parse(string text)
        {
            var result = new List<NowMarkdownInline>();

            if (string.IsNullOrEmpty(text))
                return result;

            var items = Tokenize(text);
            ProcessEmphasis(items);

            foreach (var item in items)
            {
                if (item.node != null)
                    result.Add(item.node);
                else if (item.count > 0)
                    result.Add(NowMarkdownInline.Leaf(NowMarkdownInlineType.Text, new string(item.delimiter, item.count)));
            }

            MergeAdjacentText(result);
            return result;
        }

        static List<Item> Tokenize(string text)
        {
            var items = new List<Item>();
            var plain = new StringBuilder();

            void FlushPlain()
            {
                if (plain.Length == 0)
                    return;

                items.Add(new Item { node = NowMarkdownInline.Leaf(NowMarkdownInlineType.Text, plain.ToString()) });
                plain.Length = 0;
            }

            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length && IsAsciiPunctuation(text[i + 1]))
                {
                    plain.Append(text[i + 1]);
                    ++i;
                    continue;
                }

                if (c == '\n')
                {
                    bool hard = false;
                    int trail = plain.Length;

                    while (trail > 0 && plain[trail - 1] == ' ')
                        --trail;

                    if (plain.Length - trail >= 2)
                        hard = true;

                    plain.Length = trail;

                    if (trail > 0 && plain[trail - 1] == '\\')
                    {
                        plain.Length = trail - 1;
                        hard = true;
                    }

                    FlushPlain();
                    items.Add(new Item
                    {
                        node = NowMarkdownInline.Leaf(hard ? NowMarkdownInlineType.HardBreak : NowMarkdownInlineType.SoftBreak)
                    });
                    continue;
                }

                if (c == '`')
                {
                    int run = CountRun(text, i, '`');
                    int close = FindCodeSpanClose(text, i + run, run);

                    if (close >= 0)
                    {
                        FlushPlain();
                        string code = text.Substring(i + run, close - (i + run));
                        code = NormalizeCodeSpan(code);
                        items.Add(new Item { node = NowMarkdownInline.Leaf(NowMarkdownInlineType.Code, code) });
                        i = close + run - 1;
                        continue;
                    }

                    plain.Append(text, i, run);
                    i += run - 1;
                    continue;
                }

                if (c == '[' || (c == '!' && i + 1 < text.Length && text[i + 1] == '['))
                {
                    int open = c == '!' ? i + 1 : i;

                    if (TryParseLink(text, open, out var link, out int consumedEnd))
                    {
                        FlushPlain();
                        items.Add(new Item { node = link });
                        i = consumedEnd - 1;
                        continue;
                    }

                    plain.Append(c);
                    continue;
                }

                if (c == '*' || c == '_' || c == '~')
                {
                    int run = CountRun(text, i, c);

                    if (c == '~' && run < 2)
                    {
                        plain.Append(text, i, run);
                        i += run - 1;
                        continue;
                    }

                    char before = i > 0 ? text[i - 1] : ' ';
                    char after = i + run < text.Length ? text[i + run] : ' ';
                    GetFlanking(c, before, after, out bool canOpen, out bool canClose);

                    FlushPlain();
                    items.Add(new Item { delimiter = c, count = run, canOpen = canOpen, canClose = canClose });
                    i += run - 1;
                    continue;
                }

                if ((c == 'h' || c == 'w') && TryParseAutolink(text, i, plain, out var autolink, out int autoEnd))
                {
                    FlushPlain();
                    items.Add(new Item { node = autolink });
                    i = autoEnd - 1;
                    continue;
                }

                plain.Append(c);
            }

            FlushPlain();
            return items;
        }

        static bool IsAsciiPunctuation(char c)
        {
            return (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
        }

        static int CountRun(string text, int index, char c)
        {
            int count = 0;

            while (index + count < text.Length && text[index + count] == c)
                ++count;

            return count;
        }

        static int FindCodeSpanClose(string text, int from, int run)
        {
            for (int i = from; i < text.Length; ++i)
            {
                if (text[i] != '`')
                    continue;

                int closeRun = CountRun(text, i, '`');

                if (closeRun == run)
                    return i;

                i += closeRun - 1;
            }

            return -1;
        }

        static string NormalizeCodeSpan(string code)
        {
            string collapsed = code.Replace('\n', ' ');

            if (collapsed.Length >= 2 && collapsed[0] == ' ' && collapsed[collapsed.Length - 1] == ' ')
            {
                bool allSpaces = true;

                for (int i = 0; i < collapsed.Length; ++i)
                {
                    if (collapsed[i] != ' ')
                    {
                        allSpaces = false;
                        break;
                    }
                }

                if (!allSpaces)
                    return collapsed.Substring(1, collapsed.Length - 2);
            }

            return collapsed;
        }

        static void GetFlanking(char delimiter, char before, char after, out bool canOpen, out bool canClose)
        {
            bool beforeWhite = char.IsWhiteSpace(before);
            bool afterWhite = char.IsWhiteSpace(after);
            bool beforePunct = IsAsciiPunctuation(before);
            bool afterPunct = IsAsciiPunctuation(after);

            bool leftFlanking = !afterWhite && (!afterPunct || beforeWhite || beforePunct);
            bool rightFlanking = !beforeWhite && (!beforePunct || afterWhite || afterPunct);

            if (delimiter == '_')
            {
                canOpen = leftFlanking && (!rightFlanking || beforePunct);
                canClose = rightFlanking && (!leftFlanking || afterPunct);
                return;
            }

            canOpen = leftFlanking;
            canClose = rightFlanking;
        }

        static bool TryParseLink(string text, int open, out NowMarkdownInline link, out int end)
        {
            link = null;
            end = 0;

            int depth = 0;
            int close = -1;

            for (int i = open; i < text.Length; ++i)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length)
                {
                    ++i;
                    continue;
                }

                if (c == '[')
                {
                    ++depth;
                }
                else if (c == ']')
                {
                    --depth;

                    if (depth == 0)
                    {
                        close = i;
                        break;
                    }
                }
            }

            if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
                return false;

            int destStart = close + 2;

            while (destStart < text.Length && (text[destStart] == ' ' || text[destStart] == '\t'))
                ++destStart;

            int parens = 0;
            int destEnd = -1;
            int closeParen = -1;

            for (int i = destStart; i < text.Length; ++i)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length)
                {
                    ++i;
                    continue;
                }

                if (c == '(')
                {
                    ++parens;
                }
                else if (c == ')')
                {
                    if (parens == 0)
                    {
                        closeParen = i;

                        if (destEnd < 0)
                            destEnd = i;

                        break;
                    }

                    --parens;
                }
                else if ((c == ' ' || c == '\t') && destEnd < 0)
                {
                    destEnd = i;
                }
            }

            if (closeParen < 0)
                return false;

            string url = text.Substring(destStart, destEnd - destStart);

            if (url.Length >= 2 && url[0] == '<' && url[url.Length - 1] == '>')
                url = url.Substring(1, url.Length - 2);

            var node = NowMarkdownInline.Container(NowMarkdownInlineType.Link);
            node.url = url;
            node.children.AddRange(Parse(text.Substring(open + 1, close - open - 1)));
            link = node;
            end = closeParen + 1;
            return true;
        }

        static bool TryParseAutolink(string text, int index, StringBuilder plain, out NowMarkdownInline link, out int end)
        {
            link = null;
            end = 0;

            if (plain.Length > 0)
            {
                char prev = plain[plain.Length - 1];

                if (!char.IsWhiteSpace(prev) && prev != '(' && prev != '*' && prev != '_' && prev != '~')
                    return false;
            }

            int length = 0;

            if (Matches(text, index, "https://"))
                length = 8;
            else if (Matches(text, index, "http://"))
                length = 7;
            else if (Matches(text, index, "www."))
                length = 4;

            if (length == 0)
                return false;

            int i = index + length;

            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '<')
                ++i;

            while (i > index + length)
            {
                char tail = text[i - 1];

                if (tail == '.' || tail == ',' || tail == ':' || tail == ';' || tail == '!' || tail == '?' ||
                    tail == '\'' || tail == '"' || tail == '*' || tail == '_' || tail == '~')
                {
                    --i;
                    continue;
                }

                if (tail == ')')
                {
                    int opens = 0;
                    int closes = 0;

                    for (int j = index; j < i; ++j)
                    {
                        if (text[j] == '(') ++opens;
                        if (text[j] == ')') ++closes;
                    }

                    if (closes > opens)
                    {
                        --i;
                        continue;
                    }
                }

                break;
            }

            if (i <= index + length)
                return false;

            string visible = text.Substring(index, i - index);
            string destination = visible[0] == 'w' ? "http://" + visible : visible;

            var node = NowMarkdownInline.Container(NowMarkdownInlineType.Link);
            node.url = destination;
            node.children.Add(NowMarkdownInline.Leaf(NowMarkdownInlineType.Text, visible));
            link = node;
            end = i;
            return true;
        }

        static bool Matches(string text, int index, string prefix)
        {
            if (index + prefix.Length > text.Length)
                return false;

            for (int i = 0; i < prefix.Length; ++i)
            {
                if (text[index + i] != prefix[i])
                    return false;
            }

            return true;
        }

        static void ProcessEmphasis(List<Item> items)
        {
            int closer = 0;

            while (closer < items.Count)
            {
                var close = items[closer];

                if (close.node != null || !close.canClose || close.count == 0)
                {
                    ++closer;
                    continue;
                }

                int opener = -1;

                for (int i = closer - 1; i >= 0; --i)
                {
                    var candidate = items[i];

                    if (candidate.node != null || !candidate.canOpen || candidate.count == 0)
                        continue;

                    if (candidate.delimiter != close.delimiter)
                        continue;

                    if (close.delimiter == '~' && candidate.count < 2)
                        continue;

                    opener = i;
                    break;
                }

                if (opener < 0)
                {
                    ++closer;
                    continue;
                }

                var open = items[opener];
                NowMarkdownInlineType type;
                int use;

                if (close.delimiter == '~')
                {
                    type = NowMarkdownInlineType.Strikethrough;
                    use = 2;
                }
                else if (open.count >= 2 && close.count >= 2)
                {
                    type = NowMarkdownInlineType.Strong;
                    use = 2;
                }
                else
                {
                    type = NowMarkdownInlineType.Emphasis;
                    use = 1;
                }

                var container = NowMarkdownInline.Container(type);

                for (int i = opener + 1; i < closer; ++i)
                {
                    var inner = items[i];

                    if (inner.node != null)
                        container.children.Add(inner.node);
                    else if (inner.count > 0)
                        container.children.Add(NowMarkdownInline.Leaf(NowMarkdownInlineType.Text, new string(inner.delimiter, inner.count)));
                }

                MergeAdjacentText(container.children);

                items.RemoveRange(opener + 1, closer - opener - 1);
                int containerIndex = opener + 1;
                items.Insert(containerIndex, new Item { node = container });

                open.count -= use;
                close.count -= use;

                if (close.count == 0)
                    items.RemoveAt(containerIndex + 1);

                if (open.count == 0)
                {
                    items.RemoveAt(opener);
                    --containerIndex;
                }

                closer = containerIndex;
            }
        }

        static void MergeAdjacentText(List<NowMarkdownInline> nodes)
        {
            for (int i = nodes.Count - 1; i > 0; --i)
            {
                if (nodes[i].type == NowMarkdownInlineType.Text && nodes[i - 1].type == NowMarkdownInlineType.Text)
                {
                    nodes[i - 1].text += nodes[i].text;
                    nodes.RemoveAt(i);
                }
            }
        }
    }
}
