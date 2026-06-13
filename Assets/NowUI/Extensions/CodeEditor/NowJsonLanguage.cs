using System.Collections.Generic;

namespace NowUI.CodeEditor
{
    /// <summary>
    /// JSON profile: property names, strings, numbers and the three keyword
    /// literals highlight distinctly; comments color as comments but the
    /// validator flags them (JSON has none). Validation is a full parse with
    /// positioned, human messages — the first problem becomes the squiggle.
    /// </summary>
    public sealed class NowJsonLanguage : NowCodeLanguage
    {
        public static readonly NowJsonLanguage instance = new NowJsonLanguage();

        static readonly NowCodeAutoPair[] Pairs =
        {
            new NowCodeAutoPair('{', '}'),
            new NowCodeAutoPair('[', ']'),
            new NowCodeAutoPair('"', '"'),
        };

        public override string name => "json";

        public override IReadOnlyList<NowCodeAutoPair> autoPairs => Pairs;

        public override bool IsIndentOpener(char c) => c == '{' || c == '[';

        public override bool IsIndentCloser(char c) => c == '}' || c == ']';

        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            int end = start + length;
            int i = start;

            while (i < end)
            {
                char c = text[i];

                if (c == '"')
                {
                    int stringStart = i;
                    ++i;

                    while (i < end && text[i] != '"')
                        i += text[i] == '\\' && i + 1 < end ? 2 : 1;

                    if (i < end)
                        ++i;

                    int lookahead = i;

                    while (lookahead < end && (text[lookahead] == ' ' || text[lookahead] == '\t'))
                        ++lookahead;

                    bool isProperty = lookahead < end && text[lookahead] == ':';
                    tokens.Add(new NowCodeToken
                    {
                        start = stringStart,
                        length = i - stringStart,
                        kind = isProperty ? NowCodeTokenKind.Property : NowCodeTokenKind.String,
                    });
                    continue;
                }

                if (char.IsDigit(c) || (c == '-' && i + 1 < end && char.IsDigit(text[i + 1])))
                {
                    int numberStart = i;
                    ++i;

                    while (i < end && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == 'e' || text[i] == 'E' ||
                        text[i] == '+' || text[i] == '-'))
                        ++i;

                    tokens.Add(new NowCodeToken { start = numberStart, length = i - numberStart, kind = NowCodeTokenKind.Number });
                    continue;
                }

                if (char.IsLetter(c))
                {
                    int wordStart = i;

                    while (i < end && char.IsLetter(text[i]))
                        ++i;

                    int wordLength = i - wordStart;
                    bool keyword = IsWord(text, wordStart, wordLength, "true") ||
                        IsWord(text, wordStart, wordLength, "false") ||
                        IsWord(text, wordStart, wordLength, "null");

                    tokens.Add(new NowCodeToken
                    {
                        start = wordStart,
                        length = wordLength,
                        kind = keyword ? NowCodeTokenKind.Keyword : NowCodeTokenKind.Error,
                    });
                    continue;
                }

                if (c == '/' && i + 1 < end && (text[i + 1] == '/' || text[i + 1] == '*'))
                {
                    tokens.Add(new NowCodeToken { start = i, length = end - i, kind = NowCodeTokenKind.Comment });
                    break;
                }

                if (c == '{' || c == '}' || c == '[' || c == ']' || c == ':' || c == ',')
                    tokens.Add(new NowCodeToken { start = i, length = 1, kind = NowCodeTokenKind.Punctuation });

                ++i;
            }

            return 0;
        }

        static bool IsWord(string text, int start, int length, string word)
        {
            if (length != word.Length)
                return false;

            for (int i = 0; i < length; ++i)
            {
                if (text[start + i] != word[i])
                    return false;
            }

            return true;
        }

        public override void Validate(string text, List<NowCodeDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(text))
                return;

            int i = 0;
            SkipWhitespace(text, ref i);

            if (i >= text.Length)
                return;

            if (!ParseValue(text, ref i, 0, diagnostics))
                return;

            SkipWhitespace(text, ref i);

            if (i < text.Length)
                diagnostics.Add(new NowCodeDiagnostic
                {
                    start = i,
                    length = System.Math.Min(text.Length - i, 16),
                    message = "Unexpected content after the JSON value",
                });
        }

        static void SkipWhitespace(string text, ref int i)
        {
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t' || text[i] == '\n' || text[i] == '\r'))
                ++i;
        }

        static bool Fail(List<NowCodeDiagnostic> diagnostics, int start, int length, string message)
        {
            diagnostics.Add(new NowCodeDiagnostic { start = start, length = System.Math.Max(length, 1), message = message });
            return false;
        }

        static bool ParseValue(string text, ref int i, int depth, List<NowCodeDiagnostic> diagnostics)
        {
            if (depth > 128)
                return Fail(diagnostics, i, 1, "Too deeply nested");

            SkipWhitespace(text, ref i);

            if (i >= text.Length)
                return Fail(diagnostics, System.Math.Max(text.Length - 1, 0), 1, "Expected a value");

            char c = text[i];

            if (c == '/')
                return Fail(diagnostics, i, 2, "Comments are not valid JSON");

            if (c == '{')
                return ParseObject(text, ref i, depth, diagnostics);

            if (c == '[')
                return ParseArray(text, ref i, depth, diagnostics);

            if (c == '"')
                return ParseString(text, ref i, diagnostics);

            if (c == '-' || char.IsDigit(c))
                return ParseNumber(text, ref i, diagnostics);

            if (MatchLiteral(text, ref i, "true") || MatchLiteral(text, ref i, "false") || MatchLiteral(text, ref i, "null"))
                return true;

            if (c == '\'')
                return Fail(diagnostics, i, 1, "Strings use double quotes in JSON");

            return Fail(diagnostics, i, 1, $"Unexpected character '{c}'");
        }

        static bool MatchLiteral(string text, ref int i, string literal)
        {
            if (i + literal.Length > text.Length)
                return false;

            for (int k = 0; k < literal.Length; ++k)
            {
                if (text[i + k] != literal[k])
                    return false;
            }

            if (i + literal.Length < text.Length && char.IsLetterOrDigit(text[i + literal.Length]))
                return false;

            i += literal.Length;
            return true;
        }

        static bool ParseString(string text, ref int i, List<NowCodeDiagnostic> diagnostics)
        {
            int stringStart = i;
            ++i;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '"')
                {
                    ++i;
                    return true;
                }

                if (c == '\n')
                    break;

                if (c == '\\')
                {
                    if (i + 1 >= text.Length)
                        break;

                    char escape = text[i + 1];

                    if (escape == 'u')
                    {
                        for (int k = 0; k < 4; ++k)
                        {
                            int hexIndex = i + 2 + k;

                            if (hexIndex >= text.Length || !Hex.IsHexDigit(text[hexIndex]))
                                return Fail(diagnostics, i, 2 + k, "Invalid \\u escape: expected four hex digits");
                        }

                        i += 6;
                        continue;
                    }

                    if (escape != '"' && escape != '\\' && escape != '/' && escape != 'b' &&
                        escape != 'f' && escape != 'n' && escape != 'r' && escape != 't')
                        return Fail(diagnostics, i, 2, $"Invalid escape sequence '\\{escape}'");

                    i += 2;
                    continue;
                }

                ++i;
            }

            return Fail(diagnostics, stringStart, System.Math.Min(i - stringStart, 24), "Unterminated string");
        }

        static bool ParseNumber(string text, ref int i, List<NowCodeDiagnostic> diagnostics)
        {
            int numberStart = i;

            if (text[i] == '-')
                ++i;

            if (i >= text.Length || !char.IsDigit(text[i]))
                return Fail(diagnostics, numberStart, 1, "Invalid number");

            if (text[i] == '0' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
                return Fail(diagnostics, numberStart, 2, "Numbers cannot have leading zeros");

            while (i < text.Length && char.IsDigit(text[i]))
                ++i;

            if (i < text.Length && text[i] == '.')
            {
                ++i;

                if (i >= text.Length || !char.IsDigit(text[i]))
                    return Fail(diagnostics, numberStart, i - numberStart, "Expected digits after the decimal point");

                while (i < text.Length && char.IsDigit(text[i]))
                    ++i;
            }

            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                ++i;

                if (i < text.Length && (text[i] == '+' || text[i] == '-'))
                    ++i;

                if (i >= text.Length || !char.IsDigit(text[i]))
                    return Fail(diagnostics, numberStart, i - numberStart, "Invalid exponent");

                while (i < text.Length && char.IsDigit(text[i]))
                    ++i;
            }

            return true;
        }

        static bool ParseObject(string text, ref int i, int depth, List<NowCodeDiagnostic> diagnostics)
        {
            ++i;
            SkipWhitespace(text, ref i);

            if (i < text.Length && text[i] == '}')
            {
                ++i;
                return true;
            }

            while (true)
            {
                SkipWhitespace(text, ref i);

                if (i >= text.Length)
                    return Fail(diagnostics, text.Length - 1, 1, "Unclosed object: expected '}'");

                if (text[i] != '"')
                    return Fail(diagnostics, i, 1, "Expected a property name in double quotes");

                if (!ParseString(text, ref i, diagnostics))
                    return false;

                SkipWhitespace(text, ref i);

                if (i >= text.Length || text[i] != ':')
                    return Fail(diagnostics, System.Math.Min(i, text.Length - 1), 1, "Expected ':' after the property name");

                ++i;

                if (!ParseValue(text, ref i, depth + 1, diagnostics))
                    return false;

                SkipWhitespace(text, ref i);

                if (i >= text.Length)
                    return Fail(diagnostics, text.Length - 1, 1, "Unclosed object: expected '}'");

                if (text[i] == '}')
                {
                    ++i;
                    return true;
                }

                if (text[i] != ',')
                    return Fail(diagnostics, i, 1, "Expected ',' or '}'");

                ++i;
                SkipWhitespace(text, ref i);

                if (i < text.Length && text[i] == '}')
                    return Fail(diagnostics, i, 1, "Trailing commas are not valid JSON");
            }
        }

        static bool ParseArray(string text, ref int i, int depth, List<NowCodeDiagnostic> diagnostics)
        {
            ++i;
            SkipWhitespace(text, ref i);

            if (i < text.Length && text[i] == ']')
            {
                ++i;
                return true;
            }

            while (true)
            {
                if (!ParseValue(text, ref i, depth + 1, diagnostics))
                    return false;

                SkipWhitespace(text, ref i);

                if (i >= text.Length)
                    return Fail(diagnostics, text.Length - 1, 1, "Unclosed array: expected ']'");

                if (text[i] == ']')
                {
                    ++i;
                    return true;
                }

                if (text[i] != ',')
                    return Fail(diagnostics, i, 1, "Expected ',' or ']'");

                ++i;
                SkipWhitespace(text, ref i);

                if (i < text.Length && text[i] == ']')
                    return Fail(diagnostics, i, 1, "Trailing commas are not valid JSON");
            }
        }

        static class Hex
        {
            public static bool IsHexDigit(char c)
            {
                return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            }
        }
    }
}
