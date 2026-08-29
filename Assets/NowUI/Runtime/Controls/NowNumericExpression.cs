using System;
using System.Globalization;
using System.Numerics;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Strict, bounded gateway to Unity's numeric expression evaluator.
    /// Numeric fields keep their ordinary invariant literal parsing as the fast
    /// path and use this helper only for arithmetic input.
    /// </summary>
    internal static class NowNumericExpression
    {
        const int MaxLength = 256;
        const int MaxParenthesisDepth = 32;
        const int MaxExactBits = 8192;
        const int MaxDecimalScale = 2048;
        const int MaxExactPower = 4096;

        /// <summary>
        /// Evaluates a complete arithmetic expression containing decimal
        /// numbers, +, -, *, /, %, ^ and parentheses. Invalid, incomplete and
        /// non-finite expressions fail without changing the default output.
        /// </summary>
        internal static bool TryEvaluate(string text, out double value)
        {
            value = 0d;

            if (string.IsNullOrEmpty(text) || text.Length > MaxLength)
                return false;

            var syntax = new SyntaxParser(text);

            if (!syntax.TryParse())
                return false;

            double evaluated = 0d;
            bool unityEvaluated = false;

            try
            {
                unityEvaluated = ExpressionEvaluator.Evaluate(text, out evaluated);
            }
            catch (Exception)
            {
                // ExpressionEvaluator is fed user-authored text. The strict,
                // bounded parser above excludes malformed input. Engine
                // versions still differ on scientific notation, so fall back
                // to the bounded evaluator below rather than surfacing an
                // exception from user-authored text.
            }

            if (!unityEvaluated)
            {
                var fallback = new DoubleParser(text);

                if (!fallback.TryParse(out evaluated))
                    return false;
            }

            if (double.IsNaN(evaluated) || double.IsInfinity(evaluated))
                return false;

            value = evaluated;
            return true;
        }

        /// <summary>
        /// Evaluates an arithmetic expression without passing integer values
        /// through <see cref="double"/>. This preserves every <see cref="long"/>
        /// value, including values above 2^53, and truncates a fractional final
        /// result toward zero just like a numeric cast.
        /// </summary>
        internal static bool TryEvaluateLong(string text, out long value)
        {
            value = 0L;

            if (string.IsNullOrEmpty(text) || text.Length > MaxLength)
                return false;

            // Keep the public expression grammar identical for floating-point
            // and integer fields before entering the exact evaluator.
            var syntax = new SyntaxParser(text);

            if (!syntax.TryParse())
                return false;

            var exact = new ExactParser(text);

            if (exact.TryParse(out ExactNumber evaluated))
                return evaluated.TryToLong(out value);

            // Irrational/fractional powers cannot generally be represented as
            // an exact rational. Preserve the prior behavior only inside the
            // range where double represents every integer exactly.
            if (!exact.canApproximatePower ||
                !TryEvaluate(text, out double approximate) ||
                Math.Abs(approximate) > 9007199254740992d)
            {
                return false;
            }

            const double LongUpperExclusive = 9223372036854775808d;
            const double LongLowerInclusive = -9223372036854775808d;

            if (approximate < LongLowerInclusive || approximate >= LongUpperExclusive)
                return false;

            value = (long)Math.Truncate(approximate);
            return true;
        }

        ref struct SyntaxParser
        {
            readonly string _text;
            int _position;
            int _parenthesisDepth;

            public SyntaxParser(string text)
            {
                _text = text;
                _position = 0;
                _parenthesisDepth = 0;
            }

            public bool TryParse()
            {
                SkipWhitespace();

                if (_position >= _text.Length || !ParseExpression())
                    return false;

                SkipWhitespace();
                return _position == _text.Length;
            }

            bool ParseExpression()
            {
                if (!ParseTerm())
                    return false;

                while (TryConsume('+') || TryConsume('-'))
                {
                    if (!ParseTerm())
                        return false;
                }

                return true;
            }

            bool ParseTerm()
            {
                if (!ParseUnary())
                    return false;

                while (TryConsume('*') || TryConsume('/') || TryConsume('%'))
                {
                    if (!ParseUnary())
                        return false;
                }

                return true;
            }

            bool ParsePower()
            {
                if (!ParsePrimary())
                    return false;

                if (TryConsume('^'))
                {
                    if (!ParseUnary())
                        return false;
                }

                return true;
            }

            bool ParseUnary()
            {
                while (TryConsume('+') || TryConsume('-'))
                {
                }

                return ParsePower();
            }

            bool ParsePrimary()
            {
                SkipWhitespace();

                if (TryConsume('('))
                {
                    if (++_parenthesisDepth > MaxParenthesisDepth)
                    {
                        --_parenthesisDepth;
                        return false;
                    }

                    bool parsed = ParseExpression() && TryConsume(')');
                    --_parenthesisDepth;
                    return parsed;
                }

                return ParseNumber();
            }

            bool ParseNumber()
            {
                SkipWhitespace();
                int digits = 0;

                while (_position < _text.Length && IsDigit(_text[_position]))
                {
                    ++_position;
                    ++digits;
                }

                if (_position < _text.Length && _text[_position] == '.')
                {
                    ++_position;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        ++_position;
                        ++digits;
                    }
                }

                if (digits == 0)
                    return false;

                if (_position < _text.Length &&
                    (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    ++_position;

                    if (_position < _text.Length &&
                        (_text[_position] == '+' || _text[_position] == '-'))
                    {
                        ++_position;
                    }

                    int exponentDigits = 0;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        ++_position;
                        ++exponentDigits;
                    }

                    if (exponentDigits == 0)
                        return false;
                }

                return true;
            }

            bool TryConsume(char expected)
            {
                SkipWhitespace();

                if (_position >= _text.Length || _text[_position] != expected)
                    return false;

                ++_position;
                return true;
            }

            void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                    ++_position;
            }

            static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }
        }

        /// <summary>
        /// Bounded fallback for Unity versions whose ExpressionEvaluator does
        /// not accept scientific notation in every operand position. Its
        /// precedence and right-associative power grammar match Unity's
        /// evaluator; it is used only after the engine evaluator declines the
        /// already validated expression.
        /// </summary>
        ref struct DoubleParser
        {
            readonly string _text;
            int _position;
            int _parenthesisDepth;

            internal DoubleParser(string text)
            {
                _text = text;
                _position = 0;
                _parenthesisDepth = 0;
            }

            internal bool TryParse(out double value)
            {
                SkipWhitespace();

                if (_position >= _text.Length || !ParseExpression(out value))
                {
                    value = 0d;
                    return false;
                }

                SkipWhitespace();

                if (_position != _text.Length || !IsFinite(value))
                {
                    value = 0d;
                    return false;
                }

                return true;
            }

            bool ParseExpression(out double value)
            {
                if (!ParseTerm(out value))
                    return false;

                while (true)
                {
                    SkipWhitespace();

                    if (_position >= _text.Length ||
                        (_text[_position] != '+' && _text[_position] != '-'))
                    {
                        return true;
                    }

                    char operation = _text[_position++];

                    if (!ParseTerm(out double right))
                        return false;

                    value = operation == '+' ? value + right : value - right;

                    if (!IsFinite(value))
                        return false;
                }
            }

            bool ParseTerm(out double value)
            {
                if (!ParseUnary(out value))
                    return false;

                while (true)
                {
                    SkipWhitespace();

                    if (_position >= _text.Length ||
                        (_text[_position] != '*' &&
                         _text[_position] != '/' &&
                         _text[_position] != '%'))
                    {
                        return true;
                    }

                    char operation = _text[_position++];

                    if (!ParseUnary(out double right))
                        return false;

                    switch (operation)
                    {
                        case '*':
                            value *= right;
                            break;
                        case '/':
                            value /= right;
                            break;
                        default:
                            value %= right;
                            break;
                    }

                    if (!IsFinite(value))
                        return false;
                }
            }

            bool ParseUnary(out double value)
            {
                bool negative = false;

                while (true)
                {
                    if (TryConsume('+'))
                        continue;

                    if (TryConsume('-'))
                    {
                        negative = !negative;
                        continue;
                    }

                    break;
                }

                if (!ParsePower(out value))
                    return false;

                if (negative)
                    value = -value;

                return IsFinite(value);
            }

            bool ParsePower(out double value)
            {
                if (!ParsePrimary(out value))
                    return false;

                if (!TryConsume('^'))
                    return true;

                if (!ParseUnary(out double exponent))
                    return false;

                value = Math.Pow(value, exponent);
                return IsFinite(value);
            }

            bool ParsePrimary(out double value)
            {
                SkipWhitespace();

                if (TryConsume('('))
                {
                    if (++_parenthesisDepth > MaxParenthesisDepth)
                    {
                        --_parenthesisDepth;
                        value = 0d;
                        return false;
                    }

                    bool parsed = ParseExpression(out value) && TryConsume(')');
                    --_parenthesisDepth;
                    return parsed;
                }

                return ParseNumber(out value);
            }

            bool ParseNumber(out double value)
            {
                SkipWhitespace();
                int start = _position;
                int digits = 0;

                while (_position < _text.Length && IsDigit(_text[_position]))
                {
                    ++_position;
                    ++digits;
                }

                if (_position < _text.Length && _text[_position] == '.')
                {
                    ++_position;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        ++_position;
                        ++digits;
                    }
                }

                if (digits == 0)
                {
                    value = 0d;
                    return false;
                }

                if (_position < _text.Length &&
                    (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    ++_position;

                    if (_position < _text.Length &&
                        (_text[_position] == '+' || _text[_position] == '-'))
                    {
                        ++_position;
                    }

                    int exponentDigits = 0;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        ++_position;
                        ++exponentDigits;
                    }

                    if (exponentDigits == 0)
                    {
                        value = 0d;
                        return false;
                    }
                }

                string literal = _text.Substring(start, _position - start);
                return double.TryParse(
                        literal,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value) &&
                    IsFinite(value);
            }

            bool TryConsume(char expected)
            {
                SkipWhitespace();

                if (_position >= _text.Length || _text[_position] != expected)
                    return false;

                ++_position;
                return true;
            }

            void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                    ++_position;
            }

            static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }

            static bool IsFinite(double value)
            {
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
        }

        readonly struct ExactNumber
        {
            internal readonly BigInteger numerator;
            internal readonly BigInteger denominator;

            ExactNumber(BigInteger numerator, BigInteger denominator)
            {
                this.numerator = numerator;
                this.denominator = denominator;
            }

            internal static ExactNumber Zero => new ExactNumber(BigInteger.Zero, BigInteger.One);
            internal static ExactNumber One => new ExactNumber(BigInteger.One, BigInteger.One);

            internal static bool TryFromDecimal(
                BigInteger digits,
                int decimalScale,
                out ExactNumber value)
            {
                if (digits.IsZero)
                {
                    value = Zero;
                    return true;
                }

                if (decimalScale < -MaxDecimalScale || decimalScale > MaxDecimalScale)
                {
                    value = default;
                    return false;
                }

                if (decimalScale > 0)
                {
                    return TryCreate(
                        digits,
                        BigInteger.Pow(10, decimalScale),
                        out value);
                }

                return TryCreate(
                    digits * BigInteger.Pow(10, -decimalScale),
                    BigInteger.One,
                    out value);
            }

            internal static bool TryCreate(
                BigInteger numerator,
                BigInteger denominator,
                out ExactNumber value)
            {
                if (denominator.IsZero)
                {
                    value = default;
                    return false;
                }

                if (numerator.IsZero)
                {
                    value = Zero;
                    return true;
                }

                if (denominator.Sign < 0)
                {
                    numerator = BigInteger.Negate(numerator);
                    denominator = BigInteger.Negate(denominator);
                }

                BigInteger divisor = BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(numerator),
                    denominator);

                numerator /= divisor;
                denominator /= divisor;

                if (BitLength(numerator) > MaxExactBits ||
                    BitLength(denominator) > MaxExactBits)
                {
                    value = default;
                    return false;
                }

                value = new ExactNumber(numerator, denominator);
                return true;
            }

            internal static bool TryNegate(ExactNumber source, out ExactNumber value)
            {
                value = new ExactNumber(BigInteger.Negate(source.numerator), source.denominator);
                return true;
            }

            internal static bool TryAdd(
                ExactNumber left,
                ExactNumber right,
                out ExactNumber value)
            {
                BigInteger common = BigInteger.GreatestCommonDivisor(
                    left.denominator,
                    right.denominator);
                BigInteger leftScale = right.denominator / common;
                BigInteger rightScale = left.denominator / common;

                return TryCreate(
                    left.numerator * leftScale + right.numerator * rightScale,
                    left.denominator * leftScale,
                    out value);
            }

            internal static bool TrySubtract(
                ExactNumber left,
                ExactNumber right,
                out ExactNumber value)
            {
                return TryCreate(
                    left.numerator * right.denominator - right.numerator * left.denominator,
                    left.denominator * right.denominator,
                    out value);
            }

            internal static bool TryMultiply(
                ExactNumber left,
                ExactNumber right,
                out ExactNumber value)
            {
                BigInteger leftNumerator = left.numerator;
                BigInteger leftDenominator = left.denominator;
                BigInteger rightNumerator = right.numerator;
                BigInteger rightDenominator = right.denominator;

                BigInteger cross = BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(leftNumerator),
                    rightDenominator);
                leftNumerator /= cross;
                rightDenominator /= cross;

                cross = BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(rightNumerator),
                    leftDenominator);
                rightNumerator /= cross;
                leftDenominator /= cross;

                return TryCreate(
                    leftNumerator * rightNumerator,
                    leftDenominator * rightDenominator,
                    out value);
            }

            internal static bool TryDivide(
                ExactNumber left,
                ExactNumber right,
                out ExactNumber value)
            {
                if (right.numerator.IsZero)
                {
                    value = default;
                    return false;
                }

                if (!TryCreate(right.denominator, right.numerator, out ExactNumber reciprocal))
                {
                    value = default;
                    return false;
                }

                return TryMultiply(left, reciprocal, out value);
            }

            internal static bool TryRemainder(
                ExactNumber left,
                ExactNumber right,
                out ExactNumber value)
            {
                if (right.numerator.IsZero)
                {
                    value = default;
                    return false;
                }

                BigInteger ratioNumerator = left.numerator * right.denominator;
                BigInteger ratioDenominator = left.denominator * right.numerator;
                BigInteger quotient = ratioNumerator / ratioDenominator;

                if (!TryCreate(quotient, BigInteger.One, out ExactNumber whole) ||
                    !TryMultiply(whole, right, out ExactNumber consumed))
                {
                    value = default;
                    return false;
                }

                return TrySubtract(left, consumed, out value);
            }

            internal static bool TryPower(
                ExactNumber source,
                ExactNumber exponent,
                out ExactNumber value,
                out bool requiresApproximation)
            {
                requiresApproximation = false;

                if (exponent.denominator != BigInteger.One)
                {
                    value = default;
                    requiresApproximation = true;
                    return false;
                }

                BigInteger exactExponent = exponent.numerator;

                if (exactExponent.IsZero)
                {
                    value = One;
                    return true;
                }

                if (source.numerator.IsZero)
                {
                    value = Zero;
                    return exactExponent.Sign > 0;
                }

                if (source.numerator == source.denominator)
                {
                    value = One;
                    return true;
                }

                if (source.numerator == BigInteger.Negate(source.denominator))
                {
                    value = exactExponent.IsEven
                        ? One
                        : new ExactNumber(BigInteger.MinusOne, BigInteger.One);
                    return true;
                }

                BigInteger magnitude = BigInteger.Abs(exactExponent);

                if (magnitude > MaxExactPower)
                {
                    value = default;
                    return false;
                }

                int remaining = (int)magnitude;
                ExactNumber result = One;
                ExactNumber factor = source;

                while (remaining > 0)
                {
                    if ((remaining & 1) != 0 && !TryMultiply(result, factor, out result))
                    {
                        value = default;
                        return false;
                    }

                    remaining >>= 1;

                    if (remaining != 0 && !TryMultiply(factor, factor, out factor))
                    {
                        value = default;
                        return false;
                    }
                }

                if (exactExponent.Sign < 0)
                {
                    if (!TryCreate(result.denominator, result.numerator, out result))
                    {
                        value = default;
                        return false;
                    }
                }

                value = result;
                return true;
            }

            internal bool TryToLong(out long value)
            {
                BigInteger truncated = numerator / denominator;

                if (truncated < long.MinValue || truncated > long.MaxValue)
                {
                    value = 0L;
                    return false;
                }

                value = (long)truncated;
                return true;
            }

            internal bool TryToDouble(out double value)
            {
                if (numerator.IsZero)
                {
                    value = 0d;
                    return true;
                }

                BigInteger absoluteNumerator = BigInteger.Abs(numerator);
                int numeratorBits = BitLength(absoluteNumerator);
                int denominatorBits = BitLength(denominator);
                int numeratorShift = Math.Max(0, numeratorBits - 53);
                int denominatorShift = Math.Max(0, denominatorBits - 53);
                double topNumerator = (double)(absoluteNumerator >> numeratorShift);
                double topDenominator = (double)(denominator >> denominatorShift);
                double scale = Math.Pow(2d, numeratorShift - denominatorShift);

                value = topNumerator / topDenominator * scale;

                if (numerator.Sign < 0)
                    value = -value;

                return !double.IsNaN(value) && !double.IsInfinity(value);
            }

            static int BitLength(BigInteger value)
            {
                if (value.IsZero)
                    return 0;

                byte[] bytes = BigInteger.Abs(value).ToByteArray();
                int last = bytes.Length - 1;

                while (last > 0 && bytes[last] == 0)
                    --last;

                int bits = last * 8;
                byte mostSignificant = bytes[last];

                while (mostSignificant != 0)
                {
                    ++bits;
                    mostSignificant >>= 1;
                }

                return bits;
            }
        }

        ref struct ExactParser
        {
            readonly string _text;
            int _position;
            int _parenthesisDepth;
            bool _canApproximatePower;

            internal ExactParser(string text)
            {
                _text = text;
                _position = 0;
                _parenthesisDepth = 0;
                _canApproximatePower = false;
            }

            internal bool canApproximatePower => _canApproximatePower;

            internal bool TryParse(out ExactNumber value)
            {
                SkipWhitespace();

                if (_position >= _text.Length || !ParseExpression(out value))
                {
                    value = default;
                    return false;
                }

                SkipWhitespace();

                if (_position != _text.Length)
                {
                    value = default;
                    return false;
                }

                return true;
            }

            bool ParseExpression(out ExactNumber value)
            {
                if (!ParseTerm(out value))
                    return false;

                while (true)
                {
                    SkipWhitespace();

                    if (_position >= _text.Length ||
                        (_text[_position] != '+' && _text[_position] != '-'))
                    {
                        return true;
                    }

                    char operation = _text[_position++];

                    if (!ParseTerm(out ExactNumber right))
                        return false;

                    bool succeeded = operation == '+'
                        ? ExactNumber.TryAdd(value, right, out ExactNumber result)
                        : ExactNumber.TrySubtract(value, right, out result);

                    if (!succeeded)
                        return false;

                    value = result;
                }
            }

            bool ParseTerm(out ExactNumber value)
            {
                if (!ParseUnary(out value))
                    return false;

                while (true)
                {
                    SkipWhitespace();

                    if (_position >= _text.Length ||
                        (_text[_position] != '*' &&
                         _text[_position] != '/' &&
                         _text[_position] != '%'))
                    {
                        return true;
                    }

                    char operation = _text[_position++];

                    if (!ParseUnary(out ExactNumber right))
                        return false;

                    bool succeeded;
                    ExactNumber result;

                    switch (operation)
                    {
                        case '*':
                            succeeded = ExactNumber.TryMultiply(value, right, out result);
                            break;
                        case '/':
                            succeeded = ExactNumber.TryDivide(value, right, out result);
                            break;
                        default:
                            succeeded = ExactNumber.TryRemainder(value, right, out result);
                            break;
                    }

                    if (!succeeded)
                        return false;

                    value = result;
                }
            }

            bool ParsePower(out ExactNumber value)
            {
                if (!ParsePrimary(out value))
                    return false;

                if (!TryConsume('^'))
                    return true;

                if (!ParseUnary(out ExactNumber exponent))
                    return false;

                if (!ExactNumber.TryPower(
                    value,
                    exponent,
                    out ExactNumber powered,
                    out bool requiresApproximation))
                {
                    _canApproximatePower = requiresApproximation;
                    return false;
                }

                value = powered;
                return true;
            }

            bool ParseUnary(out ExactNumber value)
            {
                bool negative = false;

                while (true)
                {
                    if (TryConsume('+'))
                        continue;

                    if (TryConsume('-'))
                    {
                        negative = !negative;
                        continue;
                    }

                    break;
                }

                if (!ParsePower(out value))
                    return false;

                return !negative || ExactNumber.TryNegate(value, out value);
            }

            bool ParsePrimary(out ExactNumber value)
            {
                SkipWhitespace();

                if (TryConsume('('))
                {
                    if (++_parenthesisDepth > MaxParenthesisDepth)
                    {
                        --_parenthesisDepth;
                        value = default;
                        return false;
                    }

                    bool parsed = ParseExpression(out value) && TryConsume(')');
                    --_parenthesisDepth;
                    return parsed;
                }

                return ParseNumber(out value);
            }

            bool ParseNumber(out ExactNumber value)
            {
                SkipWhitespace();
                BigInteger digitsValue = BigInteger.Zero;
                int digits = 0;
                int fractionalDigits = 0;

                while (_position < _text.Length && IsDigit(_text[_position]))
                {
                    digitsValue = digitsValue * 10 + (_text[_position] - '0');
                    ++_position;
                    ++digits;
                }

                if (_position < _text.Length && _text[_position] == '.')
                {
                    ++_position;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        digitsValue = digitsValue * 10 + (_text[_position] - '0');
                        ++_position;
                        ++digits;
                        ++fractionalDigits;
                    }
                }

                if (digits == 0)
                {
                    value = default;
                    return false;
                }

                int exponent = 0;

                if (_position < _text.Length &&
                    (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    ++_position;
                    bool negativeExponent = false;

                    if (_position < _text.Length &&
                        (_text[_position] == '+' || _text[_position] == '-'))
                    {
                        negativeExponent = _text[_position] == '-';
                        ++_position;
                    }

                    int exponentDigits = 0;

                    while (_position < _text.Length && IsDigit(_text[_position]))
                    {
                        if (exponent <= MaxDecimalScale)
                        {
                            exponent = exponent * 10 + (_text[_position] - '0');

                            if (exponent > MaxDecimalScale)
                                exponent = MaxDecimalScale + 1;
                        }

                        ++_position;
                        ++exponentDigits;
                    }

                    if (exponentDigits == 0)
                    {
                        value = default;
                        return false;
                    }

                    if (negativeExponent)
                        exponent = -exponent;
                }

                long scale = (long)fractionalDigits - exponent;

                if (scale < int.MinValue || scale > int.MaxValue)
                {
                    value = default;
                    return false;
                }

                return ExactNumber.TryFromDecimal(digitsValue, (int)scale, out value);
            }

            bool TryConsume(char expected)
            {
                SkipWhitespace();

                if (_position >= _text.Length || _text[_position] != expected)
                    return false;

                ++_position;
                return true;
            }

            void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                    ++_position;
            }

            static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }
        }
    }
}
