using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>A visual text line: a range into a source string.</summary>
    public struct NowTextLine
    {
        public int start;

        public int length;
    }

    /// <summary>Shared text measurement, visual-line layout and hit testing.</summary>
    public static class NowTextMetrics
    {
        public static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style, int start, int count)
        {
            return count <= 0 || font == null ? 0f : font.MeasureText(text, start, count, fontSize, style).x;
        }

        public static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style)
        {
            return string.IsNullOrEmpty(text) ? 0f : Advance(text, font, fontSize, style, 0, text.Length);
        }

        /// <summary>
        /// Builds newline-delimited lines, preserving a trailing empty line when
        /// the text ends with '\n'.
        /// </summary>
        public static void LayoutHardLines(string text, List<NowTextLine> lines)
        {
            lines.Clear();
            text ??= string.Empty;

            int lineStart = 0;

            for (int i = 0; i <= text.Length; ++i)
            {
                if (i < text.Length && text[i] != '\n')
                    continue;

                lines.Add(new NowTextLine { start = lineStart, length = i - lineStart });
                lineStart = i + 1;
            }
        }

        /// <summary>
        /// Builds word-wrapped visual lines covering every character. Newlines
        /// always terminate the current line, and oversized words hard-split.
        /// </summary>
        public static void LayoutWrappedLines(
            string text,
            NowFontAsset font,
            float fontSize,
            NowFontStyle style,
            float width,
            List<NowTextLine> lines)
        {
            lines.Clear();

            if (string.IsNullOrEmpty(text))
            {
                lines.Add(new NowTextLine { start = 0, length = 0 });
                return;
            }

            int lineStart = 0;
            int lastBreak = -1;
            float x = 0f;
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '\n')
                {
                    lines.Add(new NowTextLine { start = lineStart, length = i - lineStart });
                    ++i;
                    lineStart = i;
                    lastBreak = -1;
                    x = 0f;
                    continue;
                }

                int next = NowTextEdit.NextIndex(text, i);
                float advance = Advance(text, font, fontSize, style, i, next - i);

                if (x + advance > width && i > lineStart && c != ' ')
                {
                    int breakAt = lastBreak > lineStart ? lastBreak : i;
                    lines.Add(new NowTextLine { start = lineStart, length = breakAt - lineStart });
                    lineStart = breakAt;
                    lastBreak = -1;
                    i = breakAt;
                    x = 0f;
                    continue;
                }

                x += advance;

                if (c == ' ')
                    lastBreak = next;

                i = next;
            }

            lines.Add(new NowTextLine { start = lineStart, length = text.Length - lineStart });
        }

        /// <summary>The visual line containing the caret index.</summary>
        public static int LineOf(string text, IReadOnlyList<NowTextLine> lines, int index)
        {
            if (lines == null || lines.Count == 0)
                return 0;

            text ??= string.Empty;

            for (int i = 0; i < lines.Count; ++i)
            {
                int end = lines[i].start + lines[i].length;
                bool hardEnd = end >= text.Length || text[end] == '\n';

                if (index < end || (hardEnd && index == end))
                    return i;
            }

            return lines.Count - 1;
        }

        public static int HitIndex(
            string text,
            in NowTextLine line,
            NowFontAsset font,
            float fontSize,
            NowFontStyle style,
            float x)
        {
            if (x <= 0f)
                return line.start;

            int index = line.start;
            int end = line.start + line.length;
            float advance = 0f;

            while (index < end)
            {
                int next = NowTextEdit.NextIndex(text, index);
                float glyph = Advance(text, font, fontSize, style, index, next - index);

                if (advance + glyph * 0.5f >= x)
                    return index;

                advance += glyph;
                index = next;
            }

            return end;
        }

        public static int HitTest(
            string text,
            IReadOnlyList<NowTextLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle style,
            Vector2 pointer,
            NowRect rect,
            float lineHeight,
            float scrollX,
            float scrollY)
        {
            if (lines == null || lines.Count == 0)
                return 0;

            float localY = pointer.y - rect.y + scrollY;
            int lineIndex = Mathf.Clamp(Mathf.FloorToInt(localY / lineHeight), 0, lines.Count - 1);
            return HitIndex(text, lines[lineIndex], font, fontSize, style, pointer.x - rect.x + scrollX);
        }

        public static void LineBounds(string text, int index, out int start, out int end)
        {
            text ??= string.Empty;
            start = Mathf.Clamp(index, 0, text.Length);

            while (start > 0 && text[start - 1] != '\n')
                --start;

            end = Mathf.Clamp(index, 0, text.Length);

            while (end < text.Length && text[end] != '\n')
                ++end;
        }
    }
}
