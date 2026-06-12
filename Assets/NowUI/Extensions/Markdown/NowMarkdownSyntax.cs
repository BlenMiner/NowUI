using System.Collections.Generic;

namespace NowUI.Markdown
{
    public enum NowMarkdownTokenKind : byte
    {
        Plain,
        Keyword,
        String,
        Number,
        Comment
    }

    public struct NowMarkdownToken
    {
        public int start;
        public int length;
        public NowMarkdownTokenKind kind;
    }

    /// <summary>
    /// Carry-over lexer state between the lines of one code block (block
    /// comments and C# verbatim strings span lines).
    /// </summary>
    public struct NowMarkdownSyntaxState
    {
        public bool inBlockComment;
        public bool inVerbatimString;
    }

    /// <summary>
    /// Line tokenizer for fenced code blocks: C-style comments, strings with
    /// escapes (plus C# verbatim/interpolated), numbers and per-language
    /// keywords. Fence info strings map csharp/cs, json, and a C-like generic
    /// for js/ts/c/cpp/java; anything else stays plain.
    /// </summary>
    public static class NowMarkdownSyntax
    {
        public enum Language
        {
            None,
            CSharp,
            Json,
            CLike
        }

        static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "var", "virtual", "void", "volatile", "while", "yield", "async", "await",
            "record", "init", "nameof", "when", "where", "get", "set", "partial"
        };

        static readonly HashSet<string> CLikeKeywords = new HashSet<string>
        {
            "break", "case", "catch", "class", "const", "continue", "default", "delete", "do",
            "else", "enum", "export", "extends", "extern", "false", "finally", "for", "function",
            "if", "import", "in", "inline", "instanceof", "interface", "let", "namespace", "new",
            "null", "of", "private", "protected", "public", "return", "sizeof", "static", "struct",
            "super", "switch", "template", "this", "throw", "true", "try", "typedef", "typeof",
            "undefined", "union", "unsigned", "var", "virtual", "void", "while", "yield", "async",
            "await", "int", "float", "double", "char", "bool", "long", "short", "auto", "type"
        };

        static readonly HashSet<string> JsonKeywords = new HashSet<string>
        {
            "true", "false", "null"
        };

        public static Language GetLanguage(string info)
        {
            if (string.IsNullOrEmpty(info))
                return Language.None;

            int end = 0;

            while (end < info.Length && info[end] != ' ' && info[end] != '\t')
                ++end;

            string id = info.Substring(0, end).ToLowerInvariant();

            switch (id)
            {
                case "cs":
                case "csharp":
                case "c#":
                    return Language.CSharp;
                case "json":
                    return Language.Json;
                case "js":
                case "jsx":
                case "javascript":
                case "ts":
                case "tsx":
                case "typescript":
                case "c":
                case "h":
                case "cpp":
                case "hpp":
                case "java":
                    return Language.CLike;
                default:
                    return Language.None;
            }
        }

        /// <summary>
        /// Tokenizes one line into contiguous tokens covering the whole line,
        /// carrying multiline comment/string state in <paramref name="state"/>.
        /// </summary>
        public static void TokenizeLine(string line, Language language, ref NowMarkdownSyntaxState state, List<NowMarkdownToken> tokens)
        {
            tokens.Clear();

            if (string.IsNullOrEmpty(line))
                return;

            if (language == Language.None)
            {
                Emit(tokens, 0, line.Length, NowMarkdownTokenKind.Plain);
                return;
            }

            var keywords = language == Language.CSharp ? CSharpKeywords :
                language == Language.Json ? JsonKeywords : CLikeKeywords;
            bool allowComments = language != Language.Json;

            int i = 0;

            while (i < line.Length)
            {
                if (state.inBlockComment)
                {
                    int close = line.IndexOf("*/", i, System.StringComparison.Ordinal);

                    if (close < 0)
                    {
                        Emit(tokens, i, line.Length - i, NowMarkdownTokenKind.Comment);
                        return;
                    }

                    Emit(tokens, i, close + 2 - i, NowMarkdownTokenKind.Comment);
                    state.inBlockComment = false;
                    i = close + 2;
                    continue;
                }

                if (state.inVerbatimString)
                {
                    int end = ScanVerbatimTail(line, i, out bool closed);
                    Emit(tokens, i, end - i, NowMarkdownTokenKind.String);
                    state.inVerbatimString = !closed;
                    i = end;
                    continue;
                }

                char c = line[i];

                if (allowComments && c == '/' && i + 1 < line.Length)
                {
                    if (line[i + 1] == '/')
                    {
                        Emit(tokens, i, line.Length - i, NowMarkdownTokenKind.Comment);
                        return;
                    }

                    if (line[i + 1] == '*')
                    {
                        int close = line.IndexOf("*/", i + 2, System.StringComparison.Ordinal);

                        if (close < 0)
                        {
                            Emit(tokens, i, line.Length - i, NowMarkdownTokenKind.Comment);
                            state.inBlockComment = true;
                            return;
                        }

                        Emit(tokens, i, close + 2 - i, NowMarkdownTokenKind.Comment);
                        i = close + 2;
                        continue;
                    }
                }

                if (language == Language.CSharp && (c == '@' || c == '$') && IsVerbatimStart(line, i))
                {
                    int prefix = 1;

                    while (i + prefix < line.Length && (line[i + prefix] == '@' || line[i + prefix] == '$'))
                        ++prefix;

                    int start = i;
                    i += prefix + 1;
                    int end = ScanVerbatimTail(line, i, out bool closed);
                    Emit(tokens, start, end - start, NowMarkdownTokenKind.String);
                    state.inVerbatimString = !closed && line[start + prefix - 1] != '$';
                    i = end;
                    continue;
                }

                if (c == '"' || (c == '\'' && language != Language.Json))
                {
                    int start = i;
                    ++i;

                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length)
                        {
                            i += 2;
                            continue;
                        }

                        if (line[i] == c)
                        {
                            ++i;
                            break;
                        }

                        ++i;
                    }

                    Emit(tokens, start, i - start, NowMarkdownTokenKind.String);
                    continue;
                }

                if (c >= '0' && c <= '9')
                {
                    int start = i;

                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_'))
                        ++i;

                    Emit(tokens, start, i - start, NowMarkdownTokenKind.Number);
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;

                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        ++i;

                    string word = line.Substring(start, i - start);
                    Emit(tokens, start, i - start,
                        keywords.Contains(word) ? NowMarkdownTokenKind.Keyword : NowMarkdownTokenKind.Plain);
                    continue;
                }

                int plainStart = i;
                ++i;
                Emit(tokens, plainStart, 1, NowMarkdownTokenKind.Plain);
            }
        }

        static bool IsVerbatimStart(string line, int i)
        {
            int j = i;

            while (j < line.Length && (line[j] == '@' || line[j] == '$'))
                ++j;

            return j > i && j < line.Length && line[j] == '"';
        }

        static int ScanVerbatimTail(string line, int from, out bool closed)
        {
            int i = from;

            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    closed = true;
                    return i + 1;
                }

                ++i;
            }

            closed = false;
            return line.Length;
        }

        static void Emit(List<NowMarkdownToken> tokens, int start, int length, NowMarkdownTokenKind kind)
        {
            if (length <= 0)
                return;

            if (tokens.Count > 0)
            {
                var last = tokens[tokens.Count - 1];

                if (last.kind == kind && last.start + last.length == start)
                {
                    last.length += length;
                    tokens[tokens.Count - 1] = last;
                    return;
                }
            }

            tokens.Add(new NowMarkdownToken { start = start, length = length, kind = kind });
        }
    }
}
