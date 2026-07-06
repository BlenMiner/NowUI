using System.Collections.Generic;

namespace NowUI.CodeEditor
{
    /// <summary>
    /// C# profile: keywords, strings (including verbatim and interpolated),
    /// character literals, numbers with prefixes and suffixes, line and block
    /// comments, and preprocessor directives highlight distinctly; PascalCase
    /// identifiers color as type/member names for a two-tone reading. Block
    /// comments and verbatim strings carry across lines through the tokenizer
    /// state. Validation is intentionally open: syntax checking a C# text
    /// needs a real compiler, so hosts with Roslyn available subclass this and
    /// override <see cref="NowCodeLanguage.Validate"/>.
    /// </summary>
    public class NowCSharpLanguage : NowCodeLanguage
    {
        public static readonly NowCSharpLanguage instance = new NowCSharpLanguage();

        const int StateBlockComment = 1;

        const int StateVerbatimString = 2;

        static readonly string[] Aliases = { "cs", "c#" };

        /// <summary>Keyword set shared with subclasses (e.g. for keyword completions).</summary>
        protected static readonly HashSet<string> Keywords = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while",
            // Contextual keywords common enough to read as keywords.
            "var", "async", "await", "yield", "nameof", "when", "get", "set", "init", "value",
            "record", "with", "partial", "dynamic", "global", "where", "required",
        };

        public override string name => "csharp";

        public override IReadOnlyList<string> aliases => Aliases;

        public override bool IsIndentOpener(char c) => c == '{' || c == '(' || c == '[';

        public override bool IsIndentCloser(char c) => c == '}' || c == ')' || c == ']';

        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            int end = start + length;
            int i = start;

            if (state == StateBlockComment && !TryCloseBlockComment(text, ref i, end, tokens))
                return StateBlockComment;

            if (state == StateVerbatimString && !TryCloseVerbatimString(text, ref i, end, tokens))
                return StateVerbatimString;

            while (i < end)
            {
                char c = text[i];

                if (c == ' ' || c == '\t')
                {
                    ++i;
                    continue;
                }

                if (c == '#' && IsLineStart(text, start, i))
                {
                    tokens.Add(new NowCodeToken { start = i, length = end - i, kind = NowCodeTokenKind.Keyword });
                    break;
                }

                if (c == '/' && i + 1 < end && text[i + 1] == '/')
                {
                    tokens.Add(new NowCodeToken { start = i, length = end - i, kind = NowCodeTokenKind.Comment });
                    break;
                }

                if (c == '/' && i + 1 < end && text[i + 1] == '*')
                {
                    int commentStart = i;
                    i += 2;

                    if (!TryCloseBlockComment(text, ref i, end, tokens, commentStart))
                        return StateBlockComment;

                    continue;
                }

                if (IsVerbatimStringStart(text, i, end, out int verbatimPrefix))
                {
                    int stringStart = i;
                    i += verbatimPrefix;

                    if (!TryCloseVerbatimString(text, ref i, end, tokens, stringStart))
                        return StateVerbatimString;

                    continue;
                }

                if (c == '"' || (c == '$' && i + 1 < end && text[i + 1] == '"'))
                {
                    ScanString(text, ref i, end, tokens);
                    continue;
                }

                if (c == '\'')
                {
                    ScanCharLiteral(text, ref i, end, tokens);
                    continue;
                }

                if (char.IsDigit(c))
                {
                    ScanNumber(text, ref i, end, tokens);
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '@')
                {
                    ScanIdentifier(text, ref i, end, tokens);
                    continue;
                }

                if (IsPunctuation(c))
                    tokens.Add(new NowCodeToken { start = i, length = 1, kind = NowCodeTokenKind.Punctuation });

                ++i;
            }

            return 0;
        }

        static bool IsLineStart(string text, int lineStart, int index)
        {
            for (int i = lineStart; i < index; ++i)
            {
                if (text[i] != ' ' && text[i] != '\t')
                    return false;
            }

            return true;
        }

        static bool IsVerbatimStringStart(string text, int i, int end, out int prefixLength)
        {
            prefixLength = 0;

            if (text[i] == '@' && i + 1 < end && text[i + 1] == '"')
                prefixLength = 2;
            else if (text[i] == '@' && i + 2 < end && text[i + 1] == '$' && text[i + 2] == '"')
                prefixLength = 3;
            else if (text[i] == '$' && i + 2 < end && text[i + 1] == '@' && text[i + 2] == '"')
                prefixLength = 3;

            return prefixLength > 0;
        }

        static bool TryCloseBlockComment(string text, ref int i, int end, List<NowCodeToken> tokens, int commentStart = -1)
        {
            int tokenStart = commentStart >= 0 ? commentStart : i;

            while (i < end)
            {
                if (text[i] == '*' && i + 1 < end && text[i + 1] == '/')
                {
                    i += 2;
                    tokens.Add(new NowCodeToken { start = tokenStart, length = i - tokenStart, kind = NowCodeTokenKind.Comment });
                    return true;
                }

                ++i;
            }

            if (end > tokenStart)
                tokens.Add(new NowCodeToken { start = tokenStart, length = end - tokenStart, kind = NowCodeTokenKind.Comment });

            return false;
        }

        static bool TryCloseVerbatimString(string text, ref int i, int end, List<NowCodeToken> tokens, int stringStart = -1)
        {
            int tokenStart = stringStart >= 0 ? stringStart : i;

            while (i < end)
            {
                if (text[i] == '"')
                {
                    // "" is an escaped quote inside a verbatim string.
                    if (i + 1 < end && text[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    ++i;
                    tokens.Add(new NowCodeToken { start = tokenStart, length = i - tokenStart, kind = NowCodeTokenKind.String });
                    return true;
                }

                ++i;
            }

            if (end > tokenStart)
                tokens.Add(new NowCodeToken { start = tokenStart, length = end - tokenStart, kind = NowCodeTokenKind.String });

            return false;
        }

        static void ScanString(string text, ref int i, int end, List<NowCodeToken> tokens)
        {
            int stringStart = i;

            if (text[i] == '$')
                ++i;

            ++i;

            while (i < end && text[i] != '"')
                i += text[i] == '\\' && i + 1 < end ? 2 : 1;

            if (i < end)
                ++i;

            tokens.Add(new NowCodeToken { start = stringStart, length = i - stringStart, kind = NowCodeTokenKind.String });
        }

        static void ScanCharLiteral(string text, ref int i, int end, List<NowCodeToken> tokens)
        {
            int charStart = i;
            ++i;

            while (i < end && text[i] != '\'')
                i += text[i] == '\\' && i + 1 < end ? 2 : 1;

            if (i < end)
                ++i;

            tokens.Add(new NowCodeToken { start = charStart, length = i - charStart, kind = NowCodeTokenKind.String });
        }

        static void ScanNumber(string text, ref int i, int end, List<NowCodeToken> tokens)
        {
            int numberStart = i;

            if (text[i] == '0' && i + 1 < end &&
                (text[i + 1] == 'x' || text[i + 1] == 'X' || text[i + 1] == 'b' || text[i + 1] == 'B'))
            {
                i += 2;

                while (i < end && (IsHexDigit(text[i]) || text[i] == '_'))
                    ++i;
            }
            else
            {
                while (i < end && (char.IsDigit(text[i]) || text[i] == '_'))
                    ++i;

                if (i + 1 < end && text[i] == '.' && char.IsDigit(text[i + 1]))
                {
                    ++i;

                    while (i < end && (char.IsDigit(text[i]) || text[i] == '_'))
                        ++i;
                }

                if (i < end && (text[i] == 'e' || text[i] == 'E'))
                {
                    ++i;

                    if (i < end && (text[i] == '+' || text[i] == '-'))
                        ++i;

                    while (i < end && char.IsDigit(text[i]))
                        ++i;
                }
            }

            while (i < end && IsNumberSuffix(text[i]))
                ++i;

            tokens.Add(new NowCodeToken { start = numberStart, length = i - numberStart, kind = NowCodeTokenKind.Number });
        }

        static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        static bool IsNumberSuffix(char c)
        {
            return c == 'f' || c == 'F' || c == 'd' || c == 'D' || c == 'm' || c == 'M' ||
                c == 'u' || c == 'U' || c == 'l' || c == 'L';
        }

        static void ScanIdentifier(string text, ref int i, int end, List<NowCodeToken> tokens)
        {
            int wordStart = i;

            if (text[i] == '@')
                ++i;

            int nameStart = i;

            while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                ++i;

            int wordLength = i - wordStart;
            string word = text.Substring(wordStart, wordLength);

            if (Keywords.Contains(word))
            {
                tokens.Add(new NowCodeToken { start = wordStart, length = wordLength, kind = NowCodeTokenKind.Keyword });
                return;
            }

            // Convention-based identifier tones, IDE style: a leading underscore
            // reads as a field, PascalCase followed by '(' as a method call, and
            // any other PascalCase as a type name.
            if (nameStart < end && text[nameStart] == '_' && wordLength > 1)
            {
                tokens.Add(new NowCodeToken { start = wordStart, length = wordLength, kind = NowCodeTokenKind.Emphasis });
                return;
            }

            if (nameStart < i && char.IsUpper(text[nameStart]))
            {
                int lookahead = i;

                while (lookahead < end && text[lookahead] == ' ')
                    ++lookahead;

                bool isCall = lookahead < end && text[lookahead] == '(';
                tokens.Add(new NowCodeToken
                {
                    start = wordStart,
                    length = wordLength,
                    kind = isCall ? NowCodeTokenKind.Attribute : NowCodeTokenKind.Property,
                });
            }
        }

        static bool IsPunctuation(char c)
        {
            switch (c)
            {
                case '{': case '}': case '(': case ')': case '[': case ']':
                case ';': case ',': case '.': case ':': case '?':
                case '+': case '-': case '*': case '/': case '%':
                case '=': case '!': case '<': case '>':
                case '&': case '|': case '^': case '~':
                    return true;
                default:
                    return false;
            }
        }
    }
}
