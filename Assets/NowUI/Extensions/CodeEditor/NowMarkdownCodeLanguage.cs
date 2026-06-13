using System.Collections.Generic;

namespace NowUI.CodeEditor
{
    /// <summary>
    /// Markdown source profile: headings, emphasis, inline code, links, quotes
    /// and list markers highlight while editing raw markdown. Fenced code
    /// blocks delegate their lines to the registered language of the fence's
    /// info string (```json highlights as JSON), carrying that language's own
    /// state — the embedding the registry exists for. Validation warns on
    /// unclosed fences.
    /// </summary>
    public sealed class NowMarkdownCodeLanguage : NowCodeLanguage
    {
        public static readonly NowMarkdownCodeLanguage instance = new NowMarkdownCodeLanguage();

        static readonly NowCodeAutoPair[] Pairs =
        {
            new NowCodeAutoPair('(', ')'),
            new NowCodeAutoPair('[', ']'),
            new NowCodeAutoPair('`', '`'),
        };

        const int FenceStateShift = 8;

        const int PlainFence = -1;

        public override string name => "markdown";

        public override IReadOnlyList<NowCodeAutoPair> autoPairs => Pairs;

        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            int end = start + length;
            int contentStart = start;

            while (contentStart < end && (text[contentStart] == ' ' || text[contentStart] == '\t'))
                ++contentStart;

            bool isFenceLine = contentStart + 3 <= end &&
                text[contentStart] == '`' && text[contentStart + 1] == '`' && text[contentStart + 2] == '`';

            if (state != 0)
            {
                if (isFenceLine)
                {
                    tokens.Add(new NowCodeToken { start = start, length = length, kind = NowCodeTokenKind.Fence });
                    return 0;
                }

                int languageIndex = (state >> FenceStateShift) - 2;

                if (languageIndex >= 0)
                {
                    var embedded = AtIndex(languageIndex);

                    if (embedded != null)
                    {
                        int innerState = state & 0xFF;
                        int nextInner = embedded.TokenizeLine(text, start, length, innerState, tokens) & 0xFF;
                        return ((languageIndex + 2) << FenceStateShift) | nextInner;
                    }
                }

                return state;
            }

            if (isFenceLine)
            {
                tokens.Add(new NowCodeToken { start = start, length = length, kind = NowCodeTokenKind.Fence });

                int infoStart = contentStart + 3;

                while (infoStart < end && text[infoStart] == '`')
                    ++infoStart;

                int infoEnd = infoStart;

                while (infoEnd < end && !char.IsWhiteSpace(text[infoEnd]))
                    ++infoEnd;

                var fenceLanguage = infoEnd > infoStart ? Find(text.Substring(infoStart, infoEnd - infoStart)) : null;
                int languageIndex = fenceLanguage != null && fenceLanguage != this ? IndexOf(fenceLanguage) : PlainFence;
                return (languageIndex + 2) << FenceStateShift;
            }

            if (contentStart < end && text[contentStart] == '#')
            {
                int hashes = contentStart;

                while (hashes < end && text[hashes] == '#')
                    ++hashes;

                if (hashes - contentStart <= 6 && (hashes >= end || text[hashes] == ' '))
                {
                    tokens.Add(new NowCodeToken { start = start, length = length, kind = NowCodeTokenKind.Heading });
                    return 0;
                }
            }

            int inlineStart = contentStart;

            if (contentStart < end && text[contentStart] == '>')
            {
                tokens.Add(new NowCodeToken { start = contentStart, length = 1, kind = NowCodeTokenKind.Quote });
                inlineStart = contentStart + 1;
            }
            else if (contentStart < end && (text[contentStart] == '-' || text[contentStart] == '*' || text[contentStart] == '+') &&
                contentStart + 1 < end && text[contentStart + 1] == ' ')
            {
                tokens.Add(new NowCodeToken { start = contentStart, length = 1, kind = NowCodeTokenKind.ListMarker });
                inlineStart = contentStart + 1;
            }
            else if (contentStart < end && char.IsDigit(text[contentStart]))
            {
                int digits = contentStart;

                while (digits < end && char.IsDigit(text[digits]))
                    ++digits;

                if (digits < end && text[digits] == '.' && digits + 1 < end && text[digits + 1] == ' ')
                {
                    tokens.Add(new NowCodeToken { start = contentStart, length = digits + 1 - contentStart, kind = NowCodeTokenKind.ListMarker });
                    inlineStart = digits + 1;
                }
            }

            TokenizeInline(text, inlineStart, end, tokens);
            return 0;
        }

        static void TokenizeInline(string text, int start, int end, List<NowCodeToken> tokens)
        {
            int i = start;

            while (i < end)
            {
                char c = text[i];

                if (c == '`')
                {
                    int close = text.IndexOf('`', i + 1, end - i - 1);

                    if (close > 0)
                    {
                        tokens.Add(new NowCodeToken { start = i, length = close + 1 - i, kind = NowCodeTokenKind.CodeSpan });
                        i = close + 1;
                        continue;
                    }
                }

                if (c == '*' && i + 1 < end)
                {
                    bool strong = text[i + 1] == '*';
                    int delimiter = strong ? 2 : 1;
                    int close = FindEmphasisClose(text, i + delimiter, end, strong);

                    if (close > 0)
                    {
                        tokens.Add(new NowCodeToken
                        {
                            start = i,
                            length = close + delimiter - i,
                            kind = strong ? NowCodeTokenKind.Strong : NowCodeTokenKind.Emphasis,
                        });
                        i = close + delimiter;
                        continue;
                    }
                }

                if (c == '[')
                {
                    int closeBracket = text.IndexOf(']', i + 1, end - i - 1);

                    if (closeBracket > 0 && closeBracket + 1 < end && text[closeBracket + 1] == '(')
                    {
                        int closeParen = text.IndexOf(')', closeBracket + 2, end - closeBracket - 2);

                        if (closeParen > 0)
                        {
                            tokens.Add(new NowCodeToken { start = i, length = closeParen + 1 - i, kind = NowCodeTokenKind.Link });
                            i = closeParen + 1;
                            continue;
                        }
                    }
                }

                ++i;
            }
        }

        static int FindEmphasisClose(string text, int from, int end, bool strong)
        {
            for (int i = from; i < end; ++i)
            {
                if (text[i] != '*')
                    continue;

                if (!strong)
                    return i > from ? i : -1;

                if (i + 1 < end && text[i + 1] == '*' && i > from)
                    return i;
            }

            return -1;
        }

        public override void Validate(string text, List<NowCodeDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(text))
                return;

            int openFenceStart = -1;
            int openFenceLength = 0;
            int lineStart = 0;

            for (int i = 0; i <= text.Length; ++i)
            {
                if (i < text.Length && text[i] != '\n')
                    continue;

                int contentStart = lineStart;

                while (contentStart < i && (text[contentStart] == ' ' || text[contentStart] == '\t'))
                    ++contentStart;

                if (contentStart + 3 <= i && text[contentStart] == '`' && text[contentStart + 1] == '`' && text[contentStart + 2] == '`')
                {
                    if (openFenceStart < 0)
                    {
                        openFenceStart = lineStart;
                        openFenceLength = i - lineStart;
                    }
                    else
                    {
                        openFenceStart = -1;
                    }
                }

                lineStart = i + 1;
            }

            if (openFenceStart >= 0)
                diagnostics.Add(new NowCodeDiagnostic
                {
                    start = openFenceStart,
                    length = System.Math.Max(openFenceLength, 3),
                    message = "Unclosed code fence",
                });
        }
    }
}
