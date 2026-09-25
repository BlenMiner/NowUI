using NowUI;
using UnityEngine;

namespace NowUI.Sdf
{
    /// <summary>
    /// Line placement for aligned SDF text, shared with <c>Now.Text</c> so both
    /// resolve alignment, cap-height centering, and line breaks identically.
    /// </summary>
    internal readonly struct NowSdfTextAlignment
    {
        readonly NowText _style;
        readonly float _top;
        readonly float _lineHeight;

        public readonly int lineCount;

        NowSdfTextAlignment(NowText style, float top, float lineHeight, int lineCount)
        {
            _style = style;
            _top = top;
            _lineHeight = lineHeight;
            this.lineCount = lineCount;
        }

        public static NowSdfTextAlignment Begin(
            NowRect rect,
            string value,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            NowTextAlign align,
            NowTextVerticalAlign verticalAlign,
            out string[] lines)
        {
            var style = new NowText(rect, font);
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.align = align;
            style.verticalAlign = verticalAlign;

            lines = value.IndexOf('\n') >= 0 ? Now.GetTextLines(value) : null;
            int count = lines != null ? lines.Length : 1;
            float lineHeight = font.GetLineHeight(fontStyle) * fontSize;
            return new NowSdfTextAlignment(style, Now.AlignedTextTop(style, count), lineHeight, count);
        }

        /// <summary>Top-left of line <paramref name="index"/>, as the positional <c>Text</c> overloads expect.</summary>
        public Vector2 LinePosition(string line, int index)
        {
            float width = Now.MeasureTextLineWidth(_style, line);
            return new Vector2(Now.AlignedLineLeft(_style, width), _top + index * _lineHeight);
        }
    }
}
