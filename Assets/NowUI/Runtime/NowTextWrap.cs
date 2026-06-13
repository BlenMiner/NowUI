using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// One word of wrapped text: a range into the source string positioned
    /// relative to the layout origin. <see cref="text"/> is materialized lazily
    /// by <see cref="NowTextWrap.Draw"/> (once per layout) so shaped rendering
    /// and its caches apply.
    /// </summary>
    public struct NowTextRun
    {
        public int start;

        public int length;

        public float x;

        public float y;

        public float width;

        public string text;
    }

    /// <summary>
    /// Core word wrap: lay a string out once as positioned runs within a width,
    /// then draw those runs for many frames.
    /// <code>
    /// var runs = new List&lt;NowTextRun&gt;();
    /// Vector2 size = NowTextWrap.Layout(style, text, width, runs);
    /// NowTextWrap.Draw(style, text, runs, origin);
    /// </code>
    /// Layout measures ranges of the source string directly (no substrings).
    /// Runs of whitespace collapse to a single space, '\n' forces a line break,
    /// and a single word wider than the width overflows rather than splitting.
    /// </summary>
    public static class NowTextWrap
    {
        public static Vector2 Layout(in NowText style, string text, float width, List<NowTextRun> runs)
        {
            runs.Clear();

            var fontAsset = style.font != null ? style.font : Now.font;

            if (fontAsset == null || string.IsNullOrEmpty(text))
                return default;

            float fontSize = style.fontSize;
            float lineHeight = fontAsset.GetLineHeight(style.fontStyle) * fontSize;
            float spaceWidth = fontAsset.MeasureText(" ", fontSize, style.fontStyle).x;

            float x = 0f;
            float y = 0f;
            float maxWidth = 0f;
            bool pendingSpace = false;
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '\n')
                {
                    if (x > maxWidth)
                        maxWidth = x;

                    x = 0f;
                    y += lineHeight;
                    pendingSpace = false;
                    ++i;
                    continue;
                }

                if (c == ' ' || c == '\t' || c == '\r')
                {
                    pendingSpace = x > 0f;
                    ++i;
                    continue;
                }

                int wordStart = i;

                while (i < text.Length && text[i] != ' ' && text[i] != '\t' && text[i] != '\n' && text[i] != '\r')
                    ++i;

                int wordLength = i - wordStart;
                float wordWidth = fontAsset.MeasureText(text, wordStart, wordLength, fontSize, style.fontStyle).x;
                float startX = x + (pendingSpace ? spaceWidth : 0f);

                if (x > 0f && startX + wordWidth > width)
                {
                    if (x > maxWidth)
                        maxWidth = x;

                    x = 0f;
                    y += lineHeight;
                    startX = 0f;
                }

                runs.Add(new NowTextRun
                {
                    start = wordStart,
                    length = wordLength,
                    x = startX,
                    y = y,
                    width = wordWidth
                });

                x = startX + wordWidth;
                pendingSpace = false;
            }

            if (x > maxWidth)
                maxWidth = x;

            return new Vector2(maxWidth, y + lineHeight);
        }

        /// <summary>
        /// Draws runs produced by <see cref="Layout"/>, offset by
        /// <paramref name="origin"/>. The style's mask passes through (empty
        /// means unmasked); its rect is replaced per run.
        /// </summary>
        public static void Draw(in NowText style, string text, List<NowTextRun> runs, Vector2 origin)
        {
            var runStyle = style;
            runStyle.font = style.font != null ? style.font : Now.font;

            if (runStyle.font == null || string.IsNullOrEmpty(text))
                return;

            float lineHeight = runStyle.font.GetLineHeight(runStyle.fontStyle) * runStyle.fontSize;

            for (int i = 0; i < runs.Count; ++i)
            {
                var run = runs[i];

                if (run.text == null)
                {
                    run.text = text.Substring(run.start, run.length);
                    runs[i] = run;
                }

                runStyle.rect = new NowRect(origin.x + run.x, origin.y + run.y, run.width + 1f, lineHeight);
                runStyle.Draw(run.text);
            }
        }
    }
}
