using System;
using System.Collections.Generic;

namespace NowUI.CodeEditor
{
    /// <summary>
    /// XML-like markup profile for NowUI markup, HTML-ish snippets and XML
    /// fences. It highlights tags, attributes, quoted values, comments,
    /// entities and style blocks, and validates balanced non-void tags.
    /// </summary>
    public sealed class NowMarkupCodeLanguage : NowCodeLanguage
    {
        public static readonly NowMarkupCodeLanguage instance = new NowMarkupCodeLanguage();

        const int NormalState = 0;

        const int CommentState = 1;

        const int StyleState = 2;

        const int StyleCommentState = 3;

        static readonly string[] Aliases = { "nowui", "xml", "html", "uxml" };

        static readonly NowCodeAutoPair[] Pairs =
        {
            new NowCodeAutoPair('<', '>'),
            new NowCodeAutoPair('"', '"'),
            new NowCodeAutoPair('\'', '\''),
            new NowCodeAutoPair('(', ')'),
            new NowCodeAutoPair('[', ']'),
            new NowCodeAutoPair('{', '}'),
        };

        static readonly HashSet<string> VoidTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link",
            "meta", "param", "source", "track", "wbr",
            "space", "flex", "slider", "textfield", "progress"
        };

        static readonly List<string> TagStackScratch = new List<string>(16);

        public override string name => "markup";

        public override IReadOnlyList<string> aliases => Aliases;

        public override IReadOnlyList<NowCodeAutoPair> autoPairs => Pairs;

        public override bool IsIndentOpener(char c) => c == '>';

        public override bool IsIndentCloser(char c) => c == '<';

        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            int end = start + length;
            int i = start;

            if (state == CommentState)
            {
                int close = IndexOf(text, "-->", i, end);
                int commentEnd = close >= 0 ? close + 3 : end;
                Add(tokens, i, commentEnd - i, NowCodeTokenKind.Comment);

                if (close < 0)
                    return CommentState;

                i = commentEnd;
                state = NormalState;
            }
            else if (state == StyleCommentState)
            {
                int close = IndexOf(text, "*/", i, end);
                int commentEnd = close >= 0 ? close + 2 : end;
                Add(tokens, i, commentEnd - i, NowCodeTokenKind.Comment);

                if (close < 0)
                    return StyleCommentState;

                i = commentEnd;
                state = StyleState;
            }

            while (i < end)
            {
                if (state == StyleState)
                {
                    int closeStyle = IndexOfIgnoreCase(text, "</style", i, end);
                    int cssEnd = closeStyle >= 0 ? closeStyle : end;
                    int cssState = TokenizeCss(text, i, cssEnd, tokens);

                    if (cssState == StyleCommentState)
                        return StyleCommentState;

                    if (closeStyle < 0)
                        return StyleState;

                    i = closeStyle;
                    state = NormalState;
                }

                if (StartsWith(text, i, "<!--", end))
                {
                    int close = IndexOf(text, "-->", i + 4, end);
                    int commentEnd = close >= 0 ? close + 3 : end;
                    Add(tokens, i, commentEnd - i, NowCodeTokenKind.Comment);

                    if (close < 0)
                        return CommentState;

                    i = commentEnd;
                    continue;
                }

                if (text[i] == '<')
                {
                    int next = TokenizeTag(text, i, end, tokens, out bool opensStyle);

                    if (next > i + 1)
                    {
                        i = next;

                        if (opensStyle)
                            state = StyleState;

                        continue;
                    }
                }

                if (text[i] == '&')
                {
                    int semi = text.IndexOf(';', i + 1, end - i - 1);

                    if (semi > i)
                    {
                        Add(tokens, i, semi + 1 - i, NowCodeTokenKind.Keyword);
                        i = semi + 1;
                        continue;
                    }
                }

                ++i;
            }

            return state;
        }

        public override bool TryComplete(char c, string text, in NowTextEditState state, out NowCodeCompletion completion)
        {
            completion = default;

            if (state.hasSelection)
                return false;

            if (c == '>')
                return TryCompleteAngle(text, state.caret, out completion);

            if (c == '/')
                return TryCompleteSlash(text, state.caret, out completion);

            return false;
        }

        public override void Validate(string text, List<NowCodeDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(text))
                return;

            TagStackScratch.Clear();
            var startStack = new List<int>(16);

            int i = 0;

            while (i < text.Length)
            {
                int tagStart = text.IndexOf('<', i);

                if (tagStart < 0)
                    break;

                if (StartsWith(text, tagStart, "<!--", text.Length))
                {
                    int commentEnd = IndexOf(text, "-->", tagStart + 4, text.Length);

                    if (commentEnd < 0)
                    {
                        diagnostics.Add(new NowCodeDiagnostic
                        {
                            start = tagStart,
                            length = Math.Min(text.Length - tagStart, 24),
                            message = "Unclosed markup comment",
                        });
                        return;
                    }

                    i = commentEnd + 3;
                    continue;
                }

                int tagEnd = FindTagEnd(text, tagStart + 1, text.Length);

                if (tagEnd < 0)
                {
                    diagnostics.Add(new NowCodeDiagnostic
                    {
                        start = tagStart,
                        length = Math.Min(text.Length - tagStart, 24),
                        message = "Unclosed markup tag",
                    });
                    return;
                }

                if (!TryReadTagHeader(text, tagStart, tagEnd, out string name, out bool closing, out bool selfClosing))
                {
                    i = tagEnd + 1;
                    continue;
                }

                if (closing)
                {
                    if (TagStackScratch.Count == 0)
                    {
                        diagnostics.Add(new NowCodeDiagnostic
                        {
                            start = tagStart,
                            length = tagEnd + 1 - tagStart,
                            message = $"Unexpected closing tag </{name}>",
                        });
                        return;
                    }

                    string expected = TagStackScratch[TagStackScratch.Count - 1];

                    if (!StringEquals(expected, name))
                    {
                        diagnostics.Add(new NowCodeDiagnostic
                        {
                            start = tagStart,
                            length = tagEnd + 1 - tagStart,
                            message = $"Expected </{expected}> before </{name}>",
                        });
                        return;
                    }

                    TagStackScratch.RemoveAt(TagStackScratch.Count - 1);
                    startStack.RemoveAt(startStack.Count - 1);
                }
                else if (!selfClosing && !IsVoidTag(name))
                {
                    if (StringEquals(name, "style"))
                    {
                        int closeStyle = IndexOfIgnoreCase(text, "</style", tagEnd + 1, text.Length);

                        if (closeStyle < 0)
                        {
                            diagnostics.Add(new NowCodeDiagnostic
                            {
                                start = tagStart,
                                length = tagEnd + 1 - tagStart,
                                message = "Unclosed <style> tag",
                            });
                            return;
                        }

                        int closeStyleEnd = FindTagEnd(text, closeStyle + 2, text.Length);

                        if (closeStyleEnd < 0)
                        {
                            diagnostics.Add(new NowCodeDiagnostic
                            {
                                start = closeStyle,
                                length = text.Length - closeStyle,
                                message = "Unclosed </style> tag",
                            });
                            return;
                        }

                        i = closeStyleEnd + 1;
                        continue;
                    }

                    TagStackScratch.Add(name);
                    startStack.Add(tagStart);
                }

                i = tagEnd + 1;
            }

            if (TagStackScratch.Count > 0)
            {
                string open = TagStackScratch[TagStackScratch.Count - 1];
                int openStart = startStack[startStack.Count - 1];
                diagnostics.Add(new NowCodeDiagnostic
                {
                    start = openStart,
                    length = Math.Min(text.Length - openStart, open.Length + 2),
                    message = $"Unclosed <{open}> tag",
                });
            }
        }

        static int TokenizeTag(string text, int start, int end, List<NowCodeToken> tokens, out bool opensStyle)
        {
            opensStyle = false;

            int close = FindTagEnd(text, start + 1, end);
            int bodyEnd = close >= 0 ? close : end;
            int i = start + 1;
            Add(tokens, start, 1, NowCodeTokenKind.Punctuation);

            if (i >= bodyEnd)
            {
                if (close >= 0)
                    Add(tokens, close, 1, NowCodeTokenKind.Punctuation);

                return close >= 0 ? close + 1 : end;
            }

            bool closing = false;

            if (text[i] == '/')
            {
                closing = true;
                Add(tokens, i, 1, NowCodeTokenKind.Punctuation);
                ++i;
            }

            if (i < bodyEnd && (text[i] == '!' || text[i] == '?'))
            {
                Add(tokens, i, bodyEnd - i, NowCodeTokenKind.Punctuation);

                if (close >= 0)
                    Add(tokens, close, 1, NowCodeTokenKind.Punctuation);

                return close >= 0 ? close + 1 : end;
            }

            int nameStart = i;

            while (i < bodyEnd && IsNameChar(text[i]))
                ++i;

            if (i == nameStart)
                return start + 1;

            int nameEnd = i;
            Add(tokens, nameStart, i - nameStart, NowCodeTokenKind.Tag);

            bool selfClosing = false;

            while (i < bodyEnd)
            {
                char c = text[i];

                if (char.IsWhiteSpace(c))
                {
                    ++i;
                    continue;
                }

                if (c == '/')
                {
                    selfClosing = true;
                    Add(tokens, i, 1, NowCodeTokenKind.Punctuation);
                    ++i;
                    continue;
                }

                if (c == '=')
                {
                    Add(tokens, i, 1, NowCodeTokenKind.Punctuation);
                    ++i;
                    continue;
                }

                int attrStart = i;

                while (i < bodyEnd && IsAttributeChar(text[i]))
                    ++i;

                if (i > attrStart)
                {
                    Add(tokens, attrStart, i - attrStart, NowCodeTokenKind.Attribute);

                    while (i < bodyEnd && char.IsWhiteSpace(text[i]))
                        ++i;

                    if (i < bodyEnd && text[i] == '=')
                    {
                        Add(tokens, i, 1, NowCodeTokenKind.Punctuation);
                        ++i;

                        while (i < bodyEnd && char.IsWhiteSpace(text[i]))
                            ++i;

                        i = TokenizeAttributeValue(text, i, bodyEnd, tokens);
                    }

                    continue;
                }

                Add(tokens, i, 1, NowCodeTokenKind.Punctuation);
                ++i;
            }

            if (close >= 0)
                Add(tokens, close, 1, NowCodeTokenKind.Punctuation);

            opensStyle = !closing && !selfClosing && RegionEqualsIgnoreCase(text, nameStart, nameEnd - nameStart, "style");
            return close >= 0 ? close + 1 : end;
        }

        static int TokenizeAttributeValue(string text, int start, int end, List<NowCodeToken> tokens)
        {
            if (start >= end)
                return start;

            char quote = text[start];

            if (quote == '"' || quote == '\'')
            {
                int i = start + 1;

                while (i < end && text[i] != quote)
                    ++i;

                if (i < end)
                    ++i;

                Add(tokens, start, i - start, NowCodeTokenKind.String);
                return i;
            }

            int valueStart = start;

            while (start < end && !char.IsWhiteSpace(text[start]) && text[start] != '/' && text[start] != '>')
                ++start;

            Add(tokens, valueStart, start - valueStart, NowCodeTokenKind.String);
            return start;
        }

        static int TokenizeCss(string text, int start, int end, List<NowCodeToken> tokens)
        {
            int i = start;

            while (i < end)
            {
                if (StartsWith(text, i, "/*", end))
                {
                    int close = IndexOf(text, "*/", i + 2, end);
                    int commentEnd = close >= 0 ? close + 2 : end;
                    Add(tokens, i, commentEnd - i, NowCodeTokenKind.Comment);

                    if (close < 0)
                        return StyleCommentState;

                    i = commentEnd;
                    continue;
                }

                char c = text[i];

                if (c == '"' || c == '\'')
                {
                    int stringStart = i++;

                    while (i < end && text[i] != c)
                        i += text[i] == '\\' && i + 1 < end ? 2 : 1;

                    if (i < end)
                        ++i;

                    Add(tokens, stringStart, i - stringStart, NowCodeTokenKind.String);
                    continue;
                }

                if (char.IsDigit(c) || c == '#')
                {
                    int numberStart = i++;

                    while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] == '.' || text[i] == '%' || text[i] == '-'))
                        ++i;

                    Add(tokens, numberStart, i - numberStart, NowCodeTokenKind.Number);
                    continue;
                }

                if (IsCssIdentifierStart(c))
                {
                    int wordStart = i++;

                    while (i < end && IsCssIdentifierChar(text[i]))
                        ++i;

                    int lookahead = i;

                    while (lookahead < end && char.IsWhiteSpace(text[lookahead]))
                        ++lookahead;

                    Add(tokens, wordStart, i - wordStart,
                        lookahead < end && text[lookahead] == ':' ? NowCodeTokenKind.Property : NowCodeTokenKind.Keyword);
                    continue;
                }

                if (c == '{' || c == '}' || c == ':' || c == ';' || c == ',' || c == '.')
                    Add(tokens, i, 1, NowCodeTokenKind.Punctuation);

                ++i;
            }

            return StyleState;
        }

        static bool TryCompleteAngle(string text, int caret, out NowCodeCompletion completion)
        {
            completion = default;

            if (!TryReadCurrentOpenTag(text, caret, out string name, out _, out bool closing, out bool selfClosing))
                return false;

            int removeAfter = caret < text.Length && text[caret] == '>' ? 1 : 0;

            if (closing || selfClosing || IsVoidTag(name) || IsFollowedByClosingTag(text, caret + removeAfter, name))
            {
                completion = new NowCodeCompletion(">", 1, removeAfterCaret: removeAfter);
                return true;
            }

            string suffix = "</" + name + ">";
            completion = new NowCodeCompletion(">" + suffix, 1, removeAfterCaret: removeAfter);
            return true;
        }

        static bool TryCompleteSlash(string text, int caret, out NowCodeCompletion completion)
        {
            completion = default;

            if (caret > 0 && text[caret - 1] == '<')
            {
                string tag = FindNearestOpenTag(text, caret - 1);

                if (!string.IsNullOrEmpty(tag))
                {
                    int removeAfter = caret < text.Length && text[caret] == '>' ? 1 : 0;
                    string inserted = "/" + tag + ">";
                    completion = new NowCodeCompletion(inserted, inserted.Length, removeAfterCaret: removeAfter);
                    return true;
                }

                return false;
            }

            if (!TryReadCurrentOpenTag(text, caret, out _, out int tagStart, out bool closing, out bool selfClosing))
                return false;

            if (closing || selfClosing || caret >= text.Length || text[caret] != '>')
                return false;

            int previous = PreviousNonWhitespace(text, caret - 1, tagStart);
            string selfClose = previous >= 0 && !char.IsWhiteSpace(text[previous]) ? " />" : "/>";
            completion = new NowCodeCompletion(selfClose, selfClose.Length, removeAfterCaret: 1);
            return true;
        }

        static bool TryReadCurrentOpenTag(
            string text,
            int caret,
            out string name,
            out int tagStart,
            out bool closing,
            out bool selfClosing)
        {
            name = null;
            tagStart = -1;
            closing = false;
            selfClosing = false;

            int search = Math.Min(caret - 1, text.Length - 1);

            for (int i = search; i >= 0; --i)
            {
                if (text[i] == '>')
                    return false;

                if (text[i] == '<')
                {
                    tagStart = i;
                    break;
                }
            }

            if (tagStart < 0)
                return false;

            int iName = tagStart + 1;

            if (iName < caret && iName < text.Length && text[iName] == '/')
            {
                closing = true;
                ++iName;
            }

            if (iName >= caret || iName >= text.Length || text[iName] == '!' || text[iName] == '?')
                return false;

            int nameStart = iName;

            while (iName < caret && iName < text.Length && IsNameChar(text[iName]))
                ++iName;

            if (iName == nameStart)
                return false;

            name = text.Substring(nameStart, iName - nameStart);
            int previous = PreviousNonWhitespace(text, caret - 1, tagStart);
            selfClosing = previous >= 0 && text[previous] == '/';
            return true;
        }

        static string FindNearestOpenTag(string text, int beforeIndex)
        {
            TagStackScratch.Clear();
            int i = 0;

            while (i < beforeIndex)
            {
                int tagStart = text.IndexOf('<', i);

                if (tagStart < 0 || tagStart >= beforeIndex)
                    break;

                if (StartsWith(text, tagStart, "<!--", beforeIndex))
                {
                    int commentEnd = IndexOf(text, "-->", tagStart + 4, beforeIndex);
                    i = commentEnd >= 0 ? commentEnd + 3 : beforeIndex;
                    continue;
                }

                int tagEnd = FindTagEnd(text, tagStart + 1, beforeIndex);

                if (tagEnd < 0)
                    break;

                if (TryReadTagHeader(text, tagStart, tagEnd, out string name, out bool closing, out bool selfClosing))
                {
                    if (closing)
                    {
                        for (int s = TagStackScratch.Count - 1; s >= 0; --s)
                        {
                            if (!StringEquals(TagStackScratch[s], name))
                                continue;

                            for (int remove = TagStackScratch.Count - 1; remove >= s; --remove)
                                TagStackScratch.RemoveAt(remove);

                            break;
                        }
                    }
                    else if (!selfClosing && !IsVoidTag(name))
                    {
                        TagStackScratch.Add(name);
                    }
                }

                i = tagEnd + 1;
            }

            return TagStackScratch.Count > 0 ? TagStackScratch[TagStackScratch.Count - 1] : null;
        }

        static bool TryReadTagHeader(
            string text,
            int tagStart,
            int tagEnd,
            out string name,
            out bool closing,
            out bool selfClosing)
        {
            name = null;
            closing = false;
            selfClosing = false;

            int i = tagStart + 1;

            if (i >= tagEnd)
                return false;

            if (text[i] == '/')
            {
                closing = true;
                ++i;
            }

            if (i >= tagEnd || text[i] == '!' || text[i] == '?')
                return false;

            int nameStart = i;

            while (i < tagEnd && IsNameChar(text[i]))
                ++i;

            if (i == nameStart)
                return false;

            name = text.Substring(nameStart, i - nameStart);
            int previous = PreviousNonWhitespace(text, tagEnd - 1, tagStart);
            selfClosing = previous >= 0 && text[previous] == '/';
            return true;
        }

        static bool IsFollowedByClosingTag(string text, int index, string name)
        {
            if (index + name.Length + 3 > text.Length)
                return false;

            if (text[index] != '<' || text[index + 1] != '/')
                return false;

            for (int i = 0; i < name.Length; ++i)
            {
                if (!CharEqualsIgnoreCase(text[index + 2 + i], name[i]))
                    return false;
            }

            return text[index + 2 + name.Length] == '>';
        }

        static int FindTagEnd(string text, int start, int end)
        {
            char quote = '\0';

            for (int i = start; i < end; ++i)
            {
                char c = text[i];

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

        static int PreviousNonWhitespace(string text, int start, int limit)
        {
            for (int i = start; i > limit; --i)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Ordinal search bounded to [start, end): unlike string.IndexOf with a
        /// StringComparison, the scan never walks past <paramref name="end"/>,
        /// keeping per-line tokenization O(line) instead of O(document).
        /// </summary>
        static int IndexOf(string text, string value, int start, int end)
        {
            int limit = end - value.Length;
            char first = value[0];

            for (int i = start; i <= limit; ++i)
            {
                if (text[i] != first)
                    continue;

                int k = 1;

                while (k < value.Length && text[i + k] == value[k])
                    ++k;

                if (k == value.Length)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Case-insensitive counterpart of <see cref="IndexOf"/>, using the
        /// same per-char invariant comparison as ordinal-ignore-case instead of
        /// culture-table search.
        /// </summary>
        static int IndexOfIgnoreCase(string text, string value, int start, int end)
        {
            int limit = end - value.Length;

            for (int i = start; i <= limit; ++i)
            {
                int k = 0;

                while (k < value.Length && CharEqualsIgnoreCase(text[i + k], value[k]))
                    ++k;

                if (k == value.Length)
                    return i;
            }

            return -1;
        }

        static bool RegionEqualsIgnoreCase(string text, int start, int length, string value)
        {
            if (length != value.Length)
                return false;

            for (int i = 0; i < length; ++i)
            {
                if (!CharEqualsIgnoreCase(text[start + i], value[i]))
                    return false;
            }

            return true;
        }

        static bool StartsWith(string text, int index, string value, int end)
        {
            if (index < 0 || index + value.Length > end)
                return false;

            for (int i = 0; i < value.Length; ++i)
            {
                if (text[index + i] != value[i])
                    return false;
            }

            return true;
        }

        static bool IsNameChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.';
        }

        static bool IsAttributeChar(char c)
        {
            return IsNameChar(c) || c == '@';
        }

        static bool IsCssIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '-' || c == '_';
        }

        static bool IsCssIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '_';
        }

        static bool IsVoidTag(string name)
        {
            return !string.IsNullOrEmpty(name) && VoidTags.Contains(name);
        }

        static bool StringEquals(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static bool CharEqualsIgnoreCase(char a, char b)
        {
            return char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
        }

        static void Add(List<NowCodeToken> tokens, int start, int length, NowCodeTokenKind kind)
        {
            if (length > 0)
                tokens.Add(new NowCodeToken { start = start, length = length, kind = kind });
        }
    }
}
