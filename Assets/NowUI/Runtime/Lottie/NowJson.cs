using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NowUIInternal
{
    public enum NowJsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// Minimal read-only JSON DOM used for Lottie parsing. Lottie documents are too
    /// polymorphic for JsonUtility (values switch between scalars, arrays and objects),
    /// and the project intentionally has no external JSON dependency.
    /// Missing keys/indices resolve to a shared Null value so call sites can chain safely.
    /// </summary>
    public sealed class NowJsonValue
    {
        public static readonly NowJsonValue Null = new NowJsonValue();

        NowJsonKind _kind = NowJsonKind.Null;

        double _number;

        bool _bool;

        string _string;

        List<NowJsonValue> _array;

        Dictionary<string, NowJsonValue> _object;

        public NowJsonKind kind => _kind;

        public bool isNull => _kind == NowJsonKind.Null;

        public int count => _array?.Count ?? 0;

        public NowJsonValue this[int index]
        {
            get
            {
                if (_array == null || index < 0 || index >= _array.Count)
                    return Null;

                return _array[index];
            }
        }

        public NowJsonValue this[string key]
        {
            get
            {
                if (_object == null || key == null)
                    return Null;

                return _object.TryGetValue(key, out var value) ? value : Null;
            }
        }

        public bool Has(string key)
        {
            return _object != null && _object.ContainsKey(key);
        }

        public float AsFloat(float fallback = 0f)
        {
            switch (_kind)
            {
                case NowJsonKind.Number:
                    return (float)_number;
                case NowJsonKind.Bool:
                    return _bool ? 1f : 0f;
                case NowJsonKind.String:
                    return float.TryParse(_string, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : fallback;
                case NowJsonKind.Array:
                    // Lottie frequently wraps scalars in single element arrays.
                    return count > 0 ? _array[0].AsFloat(fallback) : fallback;
                default:
                    return fallback;
            }
        }

        public int AsInt(int fallback = 0)
        {
            return _kind == NowJsonKind.Null ? fallback : (int)AsFloat(fallback);
        }

        public bool AsBool(bool fallback = false)
        {
            switch (_kind)
            {
                case NowJsonKind.Bool:
                    return _bool;
                case NowJsonKind.Number:
                    return _number != 0;
                default:
                    return fallback;
            }
        }

        public string AsString(string fallback = null)
        {
            return _kind == NowJsonKind.String ? _string : fallback;
        }

        public Dictionary<string, NowJsonValue>.KeyCollection ObjectKeys()
        {
            return _object?.Keys;
        }

        public static NowJsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new FormatException("JSON text is empty.");

            int position = 0;
            var result = ParseValue(text, ref position);
            SkipWhitespace(text, ref position);

            if (position < text.Length)
                throw new FormatException($"Unexpected trailing JSON content at character {position}.");

            return result;
        }

        static NowJsonValue ParseValue(string text, ref int position)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length)
                throw new FormatException("Unexpected end of JSON.");

            char c = text[position];

            switch (c)
            {
                case '{':
                    return ParseObject(text, ref position);
                case '[':
                    return ParseArray(text, ref position);
                case '"':
                    return new NowJsonValue { _kind = NowJsonKind.String, _string = ParseString(text, ref position) };
                case 't':
                    ExpectLiteral(text, ref position, "true");
                    return new NowJsonValue { _kind = NowJsonKind.Bool, _bool = true };
                case 'f':
                    ExpectLiteral(text, ref position, "false");
                    return new NowJsonValue { _kind = NowJsonKind.Bool, _bool = false };
                case 'n':
                    ExpectLiteral(text, ref position, "null");
                    return Null;
                default:
                    return ParseNumber(text, ref position);
            }
        }

        static NowJsonValue ParseObject(string text, ref int position)
        {
            ++position; // consume '{'
            var result = new NowJsonValue
            {
                _kind = NowJsonKind.Object,
                _object = new Dictionary<string, NowJsonValue>(8)
            };

            SkipWhitespace(text, ref position);

            if (position < text.Length && text[position] == '}')
            {
                ++position;
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref position);

                if (position >= text.Length || text[position] != '"')
                    throw new FormatException($"Expected object key at character {position}.");

                string key = ParseString(text, ref position);
                SkipWhitespace(text, ref position);

                if (position >= text.Length || text[position] != ':')
                    throw new FormatException($"Expected ':' at character {position}.");

                ++position;
                result._object[key] = ParseValue(text, ref position);
                SkipWhitespace(text, ref position);

                if (position >= text.Length)
                    throw new FormatException("Unterminated JSON object.");

                char c = text[position];

                if (c == ',')
                {
                    ++position;
                    continue;
                }

                if (c == '}')
                {
                    ++position;
                    return result;
                }

                throw new FormatException($"Expected ',' or '}}' at character {position}.");
            }
        }

        static NowJsonValue ParseArray(string text, ref int position)
        {
            ++position; // consume '['
            var result = new NowJsonValue
            {
                _kind = NowJsonKind.Array,
                _array = new List<NowJsonValue>(8)
            };

            SkipWhitespace(text, ref position);

            if (position < text.Length && text[position] == ']')
            {
                ++position;
                return result;
            }

            while (true)
            {
                result._array.Add(ParseValue(text, ref position));
                SkipWhitespace(text, ref position);

                if (position >= text.Length)
                    throw new FormatException("Unterminated JSON array.");

                char c = text[position];

                if (c == ',')
                {
                    ++position;
                    continue;
                }

                if (c == ']')
                {
                    ++position;
                    return result;
                }

                throw new FormatException($"Expected ',' or ']' at character {position}.");
            }
        }

        static string ParseString(string text, ref int position)
        {
            ++position; // consume '"'
            int start = position;

            // Fast path: no escapes.
            while (position < text.Length)
            {
                char c = text[position];

                if (c == '"')
                {
                    string simple = text.Substring(start, position - start);
                    ++position;
                    return simple;
                }

                if (c == '\\')
                    break;

                ++position;
            }

            if (position >= text.Length)
                throw new FormatException("Unterminated JSON string.");

            var builder = new StringBuilder(32);
            builder.Append(text, start, position - start);

            while (position < text.Length)
            {
                char c = text[position];

                if (c == '"')
                {
                    ++position;
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    ++position;
                    continue;
                }

                ++position;

                if (position >= text.Length)
                    break;

                char escape = text[position++];

                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                    {
                        if (position + 4 > text.Length)
                            throw new FormatException("Invalid unicode escape in JSON string.");

                        int code = 0;

                        for (int i = 0; i < 4; ++i)
                        {
                            code = (code << 4) | HexDigit(text[position + i]);
                        }

                        position += 4;
                        builder.Append((char)code);
                        break;
                    }
                    default:
                        throw new FormatException($"Invalid escape '\\{escape}' in JSON string.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new FormatException($"Invalid hex digit '{c}' in JSON string.");
        }

        static NowJsonValue ParseNumber(string text, ref int position)
        {
            int start = position;

            if (position < text.Length && (text[position] == '-' || text[position] == '+'))
                ++position;

            while (position < text.Length)
            {
                char c = text[position];

                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    ++position;
                else
                    break;
            }

            if (position == start)
                throw new FormatException($"Invalid JSON value at character {position}.");

            var span = text.AsSpan(start, position - start);

            if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"Invalid JSON number '{span.ToString()}'.");

            return new NowJsonValue { _kind = NowJsonKind.Number, _number = value };
        }

        static void ExpectLiteral(string text, ref int position, string literal)
        {
            if (position + literal.Length > text.Length ||
                string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"Invalid JSON literal at character {position}.");
            }

            position += literal.Length;
        }

        static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length)
            {
                char c = text[position];

                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    ++position;
                else
                    break;
            }
        }
    }
}
