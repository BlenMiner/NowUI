using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>One selectable line: a rect plus the range of the source text it shows.</summary>
    public struct NowTextSelectionLine
    {
        public NowRect rect;

        public int start;

        public int length;
    }

    /// <summary>
    /// Browser-style text selection over caller-positioned lines: press and drag
    /// selects, double-click selects a word, Ctrl/Cmd+A selects all within the
    /// region, Ctrl/Cmd+C copies through <see cref="copyToClipboard"/>. Selection
    /// state keys off the id in <see cref="NowUIControlState"/>; focus integration
    /// clears the selection when the user clicks elsewhere. Call every frame from
    /// the owner's draw, before the text itself, so highlights render underneath.
    /// Hit testing and highlight metrics use per-codepoint advances (the span
    /// path), so shaped ligature clusters may select as units of their codepoints.
    /// </summary>
    public static class NowTextSelection
    {
        /// <summary>Receives the selected text on Ctrl/Cmd+C; defaults to the system clipboard.</summary>
        public static System.Action<string> copyToClipboard = static text => GUIUtility.systemCopyBuffer = text;

        /// <summary>
        /// Runs selection interaction for the region and draws the highlight
        /// rects. Returns true while a selection exists.
        /// </summary>
        /// <summary>
        /// Runs selection interaction for the region and draws the highlight
        /// rects; returns true while a selection exists. Presses that start
        /// inside <paramref name="exclusion"/> are left for the overlapping
        /// control (a copy button on top of the region, for example).
        /// </summary>
        public static bool Draw(
            int id,
            string text,
            List<NowTextSelectionLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            Vector4 highlightColor,
            NowRect exclusion = default)
        {
            if (string.IsNullOrEmpty(text) || lines == null || lines.Count == 0 || font == null)
                return false;

            NowRect bounds = lines[0].rect;

            for (int i = 1; i < lines.Count; ++i)
                bounds = bounds.Union(lines[i].rect);

            var snapshot = NowUIInput.current;
            bool pressExcluded = !exclusion.isEmpty &&
                snapshot.primaryPressed &&
                exclusion.Contains(snapshot.pointerPosition);

            if (pressExcluded)
            {
                NowUIFocus.Register(id, bounds);
                ref var excludedState = ref NowUIControlState.Get<NowTextEditState>(id);
                DrawHighlights(text, lines, font, fontSize, fontStyle, highlightColor, ref excludedState);
                return excludedState.hasSelection;
            }

            var interaction = NowUIInput.Interact(id, bounds);
            NowUIFocus.Register(id, bounds);
            ref var state = ref NowUIControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);

            if (interaction.pressed)
            {
                NowUIFocus.Focus(id);
                int hit = HitTest(text, lines, font, fontSize, fontStyle, interaction.pointerPosition);

                if (NowUIControlState.DetectDoubleClick(id, true))
                {
                    NowTextEdit.SelectWord(ref state, text, hit);
                }
                else
                {
                    state.caret = hit;
                    state.anchor = hit;
                }
            }
            else if (interaction.dragging)
            {
                state.caret = HitTest(text, lines, font, fontSize, fontStyle, interaction.pointerPosition);
                NowUIControlState.RequestRepaint();
            }

            bool focused = NowUIFocus.IsFocused(id);

            if (!focused && !interaction.held && state.hasSelection)
                state.anchor = state.caret;

            if (focused && !NowUIInput.isPassive)
            {
                var frame = NowUITextInput.current;

                if (frame.selectAllPressed)
                    NowTextEdit.SelectAll(ref state, text);

                if (frame.copyPressed && state.hasSelection)
                    copyToClipboard?.Invoke(NowTextEdit.GetSelection(text, state));
            }

            return DrawHighlights(text, lines, font, fontSize, fontStyle, highlightColor, ref state);
        }

        static bool DrawHighlights(
            string text,
            List<NowTextSelectionLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            Vector4 highlightColor,
            ref NowTextEditState state)
        {
            if (!state.hasSelection)
                return false;

            int selectionMin = state.selectionMin;
            int selectionMax = state.selectionMax;

            for (int i = 0; i < lines.Count; ++i)
            {
                var line = lines[i];
                int lineEnd = line.start + line.length;

                if (selectionMax <= line.start || selectionMin >= lineEnd)
                    continue;

                int from = Mathf.Max(selectionMin, line.start);
                int to = Mathf.Min(selectionMax, lineEnd);
                float x0 = line.rect.x + Advance(text, line.start, from - line.start, font, fontSize, fontStyle);
                float x1 = line.rect.x + Advance(text, line.start, to - line.start, font, fontSize, fontStyle);

                if (selectionMax > lineEnd && to == lineEnd)
                    x1 += fontSize * 0.35f;

                Now.Rectangle(new NowRect(x0, line.rect.y, Mathf.Max(x1 - x0, 1f), line.rect.height))
                    .SetColor(highlightColor)
                    .Draw();
            }

            return true;
        }

        static float Advance(string text, int start, int count, NowFontAsset font, float fontSize, NowFontStyle style)
        {
            return count <= 0 ? 0f : font.MeasureText(text, start, count, fontSize, style).x;
        }

        static int HitTest(string text, List<NowTextSelectionLine> lines, NowFontAsset font, float fontSize, NowFontStyle style, Vector2 position)
        {
            int lineIndex = lines.Count - 1;

            for (int i = 0; i < lines.Count; ++i)
            {
                if (position.y < lines[i].rect.yMax)
                {
                    lineIndex = i;
                    break;
                }
            }

            var line = lines[lineIndex];
            float x = position.x - line.rect.x;

            if (x <= 0f)
                return line.start;

            int index = line.start;
            int end = line.start + line.length;
            float advance = 0f;

            while (index < end)
            {
                int next = NowTextEdit.NextIndex(text, index);
                float glyphWidth = Advance(text, index, next - index, font, fontSize, style);

                if (advance + glyphWidth * 0.5f >= x)
                    return index;

                advance += glyphWidth;
                index = next;
            }

            return end;
        }
    }
}
