using System.Collections.Generic;
using System.Text;

namespace NowUI.Markdown
{
    /// <summary>
    /// GitHub-flavored Markdown parser covering the app-content subset: ATX and
    /// setext headings, paragraphs, fenced code blocks, block quotes, nested
    /// bullet/ordered/task lists, thematic breaks, pipe tables, emphasis, strong,
    /// strikethrough, inline code, links and http/www autolinks, hard and soft
    /// breaks, and backslash escapes. Deliberately out of scope: raw HTML
    /// (blocks and inline), indented code blocks, link reference definitions,
    /// entity references, and images (rendered as their alt text linking to the
    /// destination).
    /// </summary>
    public static class NowMarkdownParser
    {
        public static NowMarkdownBlock Parse(string text)
        {
            var document = new NowMarkdownBlock { type = NowMarkdownBlockType.Document };

            if (string.IsNullOrEmpty(text))
                return document;

            var lines = SplitLines(text);
            ParseBlocks(lines, 0, lines.Count, document);
            return document;
        }

        static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            int start = 0;

            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];

                if (c == '\n' || c == '\r')
                {
                    lines.Add(text.Substring(start, i - start));

                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        ++i;

                    start = i + 1;
                }
            }

            if (start <= text.Length)
                lines.Add(text.Substring(start));

            return lines;
        }

        static void ParseBlocks(List<string> lines, int from, int to, NowMarkdownBlock parent)
        {
            int i = from;

            while (i < to)
            {
                string line = lines[i];

                if (IsBlank(line))
                {
                    ++i;
                    continue;
                }

                if (TryParseAtxHeading(line, parent))
                {
                    ++i;
                    continue;
                }

                if (TryParseFence(lines, ref i, to, parent))
                    continue;

                if (IsQuoteLine(line))
                {
                    i = ParseQuote(lines, i, to, parent);
                    continue;
                }

                if (IsThematicBreak(line))
                {
                    parent.children.Add(new NowMarkdownBlock { type = NowMarkdownBlockType.ThematicBreak });
                    ++i;
                    continue;
                }

                if (TryGetListMarker(line, out _, out _, out _, out _))
                {
                    i = ParseList(lines, i, to, parent);
                    continue;
                }

                if (TryParseTable(lines, ref i, to, parent))
                    continue;

                i = ParseParagraph(lines, i, to, parent);
            }
        }

        static bool IsBlank(string line)
        {
            for (int i = 0; i < line.Length; ++i)
            {
                if (line[i] != ' ' && line[i] != '\t')
                    return false;
            }

            return true;
        }

        static int LeadingSpaces(string line)
        {
            int count = 0;

            while (count < line.Length && line[count] == ' ')
                ++count;

            return count;
        }

        static bool TryParseAtxHeading(string line, NowMarkdownBlock parent)
        {
            int indent = LeadingSpaces(line);

            if (indent > 3)
                return false;

            int i = indent;
            int level = 0;

            while (i < line.Length && line[i] == '#' && level < 7)
            {
                ++level;
                ++i;
            }

            if (level == 0 || level > 6)
                return false;

            if (i < line.Length && line[i] != ' ' && line[i] != '\t')
                return false;

            string content = i < line.Length ? line.Substring(i).Trim() : string.Empty;
            content = TrimClosingHashes(content);

            var heading = new NowMarkdownBlock
            {
                type = NowMarkdownBlockType.Heading,
                level = level,
                inlines = NowMarkdownInlineParser.Parse(content)
            };
            parent.children.Add(heading);
            return true;
        }

        static string TrimClosingHashes(string content)
        {
            int end = content.Length;

            while (end > 0 && content[end - 1] == '#')
                --end;

            if (end == content.Length)
                return content;

            if (end == 0)
                return string.Empty;

            if (content[end - 1] == ' ' || content[end - 1] == '\t')
                return content.Substring(0, end).TrimEnd();

            return content;
        }

        static bool IsThematicBreak(string line)
        {
            int indent = LeadingSpaces(line);

            if (indent > 3)
                return false;

            char marker = '\0';
            int count = 0;

            for (int i = indent; i < line.Length; ++i)
            {
                char c = line[i];

                if (c == ' ' || c == '\t')
                    continue;

                if (c != '-' && c != '*' && c != '_')
                    return false;

                if (marker == '\0')
                    marker = c;
                else if (c != marker)
                    return false;

                ++count;
            }

            return count >= 3;
        }

        static bool TryParseFence(List<string> lines, ref int index, int to, NowMarkdownBlock parent)
        {
            string line = lines[index];
            int indent = LeadingSpaces(line);

            if (indent > 3)
                return false;

            char fence = '\0';
            int run = 0;
            int i = indent;

            while (i < line.Length && (line[i] == '`' || line[i] == '~'))
            {
                if (fence == '\0')
                    fence = line[i];
                else if (line[i] != fence)
                    break;

                ++run;
                ++i;
            }

            if (run < 3)
                return false;

            string info = line.Substring(i).Trim();

            if (fence == '`' && info.IndexOf('`') >= 0)
                return false;

            var content = new StringBuilder();
            int j = index + 1;

            for (; j < to; ++j)
            {
                string body = lines[j];

                if (IsClosingFence(body, fence, run))
                    break;

                if (content.Length > 0)
                    content.Append('\n');

                content.Append(DedentUpTo(body, indent));
            }

            parent.children.Add(new NowMarkdownBlock
            {
                type = NowMarkdownBlockType.CodeBlock,
                literal = content.ToString(),
                info = info
            });

            index = j < to ? j + 1 : to;
            return true;
        }

        static bool IsClosingFence(string line, char fence, int openRun)
        {
            int indent = LeadingSpaces(line);

            if (indent > 3)
                return false;

            int run = 0;
            int i = indent;

            while (i < line.Length && line[i] == fence)
            {
                ++run;
                ++i;
            }

            if (run < openRun)
                return false;

            for (; i < line.Length; ++i)
            {
                if (line[i] != ' ' && line[i] != '\t')
                    return false;
            }

            return true;
        }

        static string DedentUpTo(string line, int spaces)
        {
            int strip = 0;

            while (strip < spaces && strip < line.Length && line[strip] == ' ')
                ++strip;

            return strip > 0 ? line.Substring(strip) : line;
        }

        static bool IsQuoteLine(string line)
        {
            int indent = LeadingSpaces(line);
            return indent <= 3 && indent < line.Length && line[indent] == '>';
        }

        static int ParseQuote(List<string> lines, int from, int to, NowMarkdownBlock parent)
        {
            var inner = new List<string>();
            int i = from;

            for (; i < to; ++i)
            {
                string line = lines[i];

                if (IsQuoteLine(line))
                {
                    int marker = LeadingSpaces(line);
                    int contentStart = marker + 1;

                    if (contentStart < line.Length && line[contentStart] == ' ')
                        ++contentStart;

                    inner.Add(contentStart <= line.Length ? line.Substring(contentStart) : string.Empty);
                    continue;
                }

                if (IsBlank(line))
                    break;

                if (inner.Count > 0 && !StartsNewBlock(line))
                {
                    inner.Add(line);
                    continue;
                }

                break;
            }

            var quote = new NowMarkdownBlock { type = NowMarkdownBlockType.Quote };
            ParseBlocks(inner, 0, inner.Count, quote);
            parent.children.Add(quote);
            return i;
        }

        static bool StartsNewBlock(string line)
        {
            if (TryGetListMarker(line, out _, out _, out _, out _))
                return true;

            if (IsThematicBreak(line) || IsQuoteLine(line))
                return true;

            int indent = LeadingSpaces(line);

            if (indent <= 3 && indent < line.Length && line[indent] == '#')
                return true;

            return false;
        }

        static bool TryGetListMarker(string line, out int indent, out int contentStart, out bool ordered, out int number)
        {
            indent = LeadingSpaces(line);
            contentStart = 0;
            ordered = false;
            number = 1;

            if (indent > 3 + IndentBase(line) || indent >= line.Length)
                return false;

            int i = indent;
            char c = line[i];

            if (c == '-' || c == '+' || c == '*')
            {
                ++i;
            }
            else if (c >= '0' && c <= '9')
            {
                int digits = 0;
                number = 0;

                while (i < line.Length && line[i] >= '0' && line[i] <= '9' && digits < 9)
                {
                    number = number * 10 + (line[i] - '0');
                    ++i;
                    ++digits;
                }

                if (i >= line.Length || (line[i] != '.' && line[i] != ')'))
                    return false;

                ++i;
                ordered = true;
            }
            else
            {
                return false;
            }

            if (i >= line.Length)
            {
                contentStart = i;
                return true;
            }

            if (line[i] != ' ' && line[i] != '\t')
                return false;

            ++i;
            contentStart = i;
            return true;
        }

        static int IndentBase(string line)
        {
            return 0;
        }

        static int ParseList(List<string> lines, int from, int to, NowMarkdownBlock parent)
        {
            TryGetListMarker(lines[from], out int listIndent, out _, out bool ordered, out int start);

            var list = new NowMarkdownBlock
            {
                type = NowMarkdownBlockType.List,
                ordered = ordered,
                start = start
            };

            int i = from;

            while (i < to)
            {
                string line = lines[i];

                if (IsBlank(line))
                {
                    if (i + 1 < to && BelongsToList(lines[i + 1], listIndent, ordered))
                    {
                        ++i;
                        continue;
                    }

                    break;
                }

                if (!TryGetListMarker(line, out int indent, out int contentStart, out bool itemOrdered, out _) ||
                    indent > listIndent + 1 ||
                    itemOrdered != ordered)
                {
                    break;
                }

                var itemLines = new List<string> { line.Substring(contentStart) };
                int continuationIndent = contentStart;
                ++i;

                while (i < to)
                {
                    string next = lines[i];

                    if (IsBlank(next))
                    {
                        if (i + 1 < to && (ContinuesItem(lines[i + 1], continuationIndent) ||
                            BelongsToList(lines[i + 1], listIndent, ordered)))
                        {
                            if (ContinuesItem(lines[i + 1], continuationIndent))
                            {
                                itemLines.Add(string.Empty);
                                ++i;
                                continue;
                            }
                        }

                        break;
                    }

                    if (ContinuesItem(next, continuationIndent))
                    {
                        itemLines.Add(DedentUpTo(next, continuationIndent));
                        ++i;
                        continue;
                    }

                    if (TryGetListMarker(next, out int nextIndent, out _, out _, out _) && nextIndent <= listIndent + 1)
                        break;

                    if (StartsNewBlock(next))
                        break;

                    itemLines.Add(next.TrimStart());
                    ++i;
                }

                list.children.Add(ParseListItem(itemLines));
            }

            parent.children.Add(list);
            return i;
        }

        static bool BelongsToList(string line, int listIndent, bool ordered)
        {
            return TryGetListMarker(line, out int indent, out _, out bool itemOrdered, out _) &&
                indent <= listIndent + 1 &&
                itemOrdered == ordered;
        }

        static bool ContinuesItem(string line, int continuationIndent)
        {
            if (IsBlank(line))
                return false;

            int spaces = LeadingSpaces(line);
            return spaces >= continuationIndent || (spaces >= 2 && TryGetListMarker(line, out int indent, out _, out _, out _) && indent >= 2);
        }

        static NowMarkdownBlock ParseListItem(List<string> itemLines)
        {
            var item = new NowMarkdownBlock { type = NowMarkdownBlockType.ListItem };

            if (itemLines.Count > 0)
            {
                string first = itemLines[0];

                if (first.Length >= 3 && first[0] == '[' && first[2] == ']' &&
                    (first[1] == ' ' || first[1] == 'x' || first[1] == 'X') &&
                    (first.Length == 3 || first[3] == ' '))
                {
                    item.isTask = true;
                    item.isChecked = first[1] != ' ';
                    itemLines[0] = first.Length > 4 ? first.Substring(4) : string.Empty;
                }
            }

            ParseBlocks(itemLines, 0, itemLines.Count, item);
            return item;
        }

        static bool TryParseTable(List<string> lines, ref int index, int to, NowMarkdownBlock parent)
        {
            if (index + 1 >= to)
                return false;

            string header = lines[index];
            string delimiter = lines[index + 1];

            if (header.IndexOf('|') < 0 || !TryParseDelimiterRow(delimiter, out var aligns))
                return false;

            var headerCells = SplitTableRow(header);

            if (headerCells.Count != aligns.Count)
                return false;

            var table = new NowMarkdownBlock
            {
                type = NowMarkdownBlockType.Table,
                tableAligns = aligns,
                tableRows = new List<List<List<NowMarkdownInline>>>()
            };

            table.tableRows.Add(ParseTableCells(headerCells, aligns.Count));

            int i = index + 2;

            for (; i < to; ++i)
            {
                string line = lines[i];

                if (IsBlank(line) || line.IndexOf('|') < 0)
                    break;

                table.tableRows.Add(ParseTableCells(SplitTableRow(line), aligns.Count));
            }

            parent.children.Add(table);
            index = i;
            return true;
        }

        static bool TryParseDelimiterRow(string line, out List<NowMarkdownAlign> aligns)
        {
            aligns = null;
            var cells = SplitTableRow(line);

            if (cells.Count == 0)
                return false;

            var result = new List<NowMarkdownAlign>(cells.Count);

            for (int i = 0; i < cells.Count; ++i)
            {
                string cell = cells[i].Trim();

                if (cell.Length == 0)
                    return false;

                bool left = cell[0] == ':';
                bool right = cell[cell.Length - 1] == ':';
                int from = left ? 1 : 0;
                int until = cell.Length - (right ? 1 : 0);

                if (until - from < 1)
                    return false;

                for (int c = from; c < until; ++c)
                {
                    if (cell[c] != '-')
                        return false;
                }

                if (left && right)
                    result.Add(NowMarkdownAlign.Center);
                else if (right)
                    result.Add(NowMarkdownAlign.Right);
                else if (left)
                    result.Add(NowMarkdownAlign.Left);
                else
                    result.Add(NowMarkdownAlign.None);
            }

            aligns = result;
            return true;
        }

        static List<string> SplitTableRow(string line)
        {
            var cells = new List<string>();
            int start = 0;
            int end = line.Length;

            while (start < end && (line[start] == ' ' || line[start] == '\t'))
                ++start;

            while (end > start && (line[end - 1] == ' ' || line[end - 1] == '\t'))
                --end;

            if (start < end && line[start] == '|')
                ++start;

            if (end > start && line[end - 1] == '|' && (end - 2 < start || line[end - 2] != '\\'))
                --end;

            var cell = new StringBuilder();

            for (int i = start; i < end; ++i)
            {
                char c = line[i];

                if (c == '\\' && i + 1 < end && line[i + 1] == '|')
                {
                    cell.Append('|');
                    ++i;
                    continue;
                }

                if (c == '|')
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }

                cell.Append(c);
            }

            cells.Add(cell.ToString());
            return cells;
        }

        static List<List<NowMarkdownInline>> ParseTableCells(List<string> cells, int columnCount)
        {
            var row = new List<List<NowMarkdownInline>>(columnCount);

            for (int i = 0; i < columnCount; ++i)
                row.Add(NowMarkdownInlineParser.Parse(i < cells.Count ? cells[i].Trim() : string.Empty));

            return row;
        }

        static int ParseParagraph(List<string> lines, int from, int to, NowMarkdownBlock parent)
        {
            var content = new StringBuilder();
            int i = from;

            for (; i < to; ++i)
            {
                string line = lines[i];

                if (IsBlank(line))
                    break;

                if (content.Length > 0)
                {
                    if (TryGetSetextLevel(line, out int level))
                    {
                        parent.children.Add(new NowMarkdownBlock
                        {
                            type = NowMarkdownBlockType.Heading,
                            level = level,
                            inlines = NowMarkdownInlineParser.Parse(content.ToString())
                        });
                        return i + 1;
                    }

                    if (StartsNewBlock(line) || LooksLikeTableStart(lines, i, to))
                        break;

                    content.Append('\n');
                }

                content.Append(line.Trim());
            }

            parent.children.Add(new NowMarkdownBlock
            {
                type = NowMarkdownBlockType.Paragraph,
                inlines = NowMarkdownInlineParser.Parse(content.ToString())
            });
            return i;
        }

        static bool LooksLikeTableStart(List<string> lines, int index, int to)
        {
            return index + 1 < to &&
                lines[index].IndexOf('|') >= 0 &&
                TryParseDelimiterRow(lines[index + 1], out _);
        }

        static bool TryGetSetextLevel(string line, out int level)
        {
            level = 0;
            int indent = LeadingSpaces(line);

            if (indent > 3 || indent >= line.Length)
                return false;

            char marker = line[indent];

            if (marker != '=' && marker != '-')
                return false;

            for (int i = indent; i < line.Length; ++i)
            {
                char c = line[i];

                if (c == ' ' || c == '\t')
                {
                    for (int j = i; j < line.Length; ++j)
                    {
                        if (line[j] != ' ' && line[j] != '\t')
                            return false;
                    }

                    break;
                }

                if (c != marker)
                    return false;
            }

            level = marker == '=' ? 1 : 2;
            return true;
        }
    }
}
