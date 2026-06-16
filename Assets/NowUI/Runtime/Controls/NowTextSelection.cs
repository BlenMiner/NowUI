using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// One selectable line segment: a rect plus the range of the source text it
    /// shows. <see cref="fontSize"/> overrides the call-level size when set, so
    /// regions mixing sizes (headings, code) hit-test correctly.
    /// </summary>
    public struct NowTextSelectionLine
    {
        public NowRect rect;

        public int start;

        public int length;

        public float fontSize;
    }

    public struct NowTextSelectionResult
    {
        public bool hasSelection;

        /// <summary>A secondary-button press landed in the region this frame.</summary>
        public bool rightClicked;

        public Vector2 rightClickPosition;
    }

    /// <summary>
    /// Browser-style text selection over caller-positioned line segments:
    /// press and drag selects (across everything the segment list covers, like
    /// dragging over a webpage), double-click selects a word, triple-click a
    /// line, Ctrl/Cmd+A selects all, Ctrl/Cmd+C copies through
    /// <see cref="NowClipboard"/>. Selection
    /// state keys off the id in <see cref="NowControlState"/>; focus
    /// integration clears the selection when the user clicks elsewhere.
    /// <see cref="Interact"/> runs the input once for a whole document;
    /// <see cref="DrawHighlights"/> renders any slice of its segments, so
    /// highlights can layer correctly between region backgrounds and text.
    /// <see cref="Draw"/> combines both for single-region content. Hit testing
    /// uses per-codepoint advances (the span path).
    /// </summary>
    public static class NowTextSelection
    {
        static readonly List<NowRect> _singleExclusion = new List<NowRect>(1);

        /// <summary>Single-region convenience: <see cref="Interact"/> + <see cref="DrawHighlights"/>.</summary>
        public static NowTextSelectionResult Draw(
            int id,
            string text,
            List<NowTextSelectionLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            Vector4 highlightColor,
            NowRect exclusion = default)
        {
            _singleExclusion.Clear();

            if (!exclusion.isEmpty)
                _singleExclusion.Add(exclusion);

            var result = Interact(id, text, lines, font, fontSize, fontStyle, _singleExclusion);
            result.hasSelection = DrawHighlights(id, text, lines, font, fontSize, fontStyle, highlightColor);
            return result;
        }

        /// <summary>
        /// Runs selection input for the whole segment list: press/drag, word
        /// select, keyboard shortcuts and right-click reporting. Presses starting
        /// inside any of <paramref name="exclusions"/> are left for overlapping
        /// controls (copy buttons).
        /// </summary>
        public static NowTextSelectionResult Interact(
            int id,
            string text,
            List<NowTextSelectionLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            IReadOnlyList<NowRect> exclusions = null)
        {
            var result = default(NowTextSelectionResult);

            if (string.IsNullOrEmpty(text) || lines == null || lines.Count == 0 || font == null)
                return result;

            NowRect bounds = lines[0].rect;

            for (int i = 1; i < lines.Count; ++i)
                bounds = bounds.Union(lines[i].rect);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);

            var snapshot = NowInput.current;

            if (!NowInput.isPassive &&
                snapshot.hasPointer &&
                bounds.Contains(snapshot.pointerPosition) &&
                Now.IsInsideAmbientMask(snapshot.pointerPosition) &&
                (snapshot.pointerButtonsPressed & NowPointerButtons.Secondary) != 0)
            {
                NowFocus.Focus(id);
                result.rightClicked = true;
                result.rightClickPosition = snapshot.pointerPosition;
            }

            bool pressExcluded = false;

            if (snapshot.primaryPressed && exclusions != null)
            {
                for (int i = 0; i < exclusions.Count; ++i)
                {
                    if (!exclusions[i].isEmpty && exclusions[i].Contains(snapshot.pointerPosition))
                    {
                        pressExcluded = true;
                        break;
                    }
                }
            }

            if (pressExcluded)
            {
                NowFocus.Register(id, bounds);
                result.hasSelection = state.hasSelection;
                return result;
            }

            var interaction = NowInput.Interact(id, bounds);
            NowFocus.Register(id, bounds);
            ref var gesture = ref NowControlState.Get<NowTextSelectionGesture>(NowInput.GetId(id, "selection-gesture"));

            if (interaction.pressed)
            {
                NowFocus.Focus(id);
                int hit = HitTest(text, lines, font, fontSize, fontStyle, interaction.pointerPosition);
                int streak = NowControlState.ClickStreak(id, true, interaction.pointerPosition);

                if (streak >= 3)
                {
                    NowTextEdit.SelectLine(ref state, text, hit);
                    NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Line, in state);
                }
                else if (streak == 2)
                {
                    NowTextEdit.SelectWord(ref state, text, hit);
                    NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Word, in state);
                }
                else
                {
                    state.caret = hit;
                    state.anchor = hit;
                    NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Character, in state);
                }
            }
            else if (interaction.dragging)
            {
                int hit = HitTest(text, lines, font, fontSize, fontStyle, interaction.pointerPosition);
                NowTextEdit.DragSelectionGesture(ref state, text, in gesture, hit);
                NowControlState.RequestRepaint();
            }

            bool focused = NowFocus.IsFocused(id);

            if (!focused && !interaction.held && state.hasSelection)
                state.anchor = state.caret;

            if (focused && !NowInput.isPassive)
            {
                var frame = NowTextInput.current;

                if (frame.selectAllPressed)
                    NowTextEdit.SelectAll(ref state, text);

                if (frame.copyPressed && state.hasSelection)
                    NowClipboard.Copy(NowTextEdit.GetSelection(text, state));

                NowControlState.RequestRepaint();
            }

            result.hasSelection = state.hasSelection;
            return result;
        }

        /// <summary>
        /// Draws the highlight rects for a slice of the segments (one region of a
        /// larger selection); returns true while a selection exists.
        /// </summary>
        public static bool DrawHighlights(
            int id,
            string text,
            List<NowTextSelectionLine> lines,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            Vector4 highlightColor)
        {
            if (string.IsNullOrEmpty(text) || lines == null || font == null)
                return false;

            ref var state = ref NowControlState.Get<NowTextEditState>(id);

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

                float size = line.fontSize > 0f ? line.fontSize : fontSize;
                int from = Mathf.Max(selectionMin, line.start);
                int to = Mathf.Min(selectionMax, lineEnd);
                float x0 = line.rect.x + Advance(text, line.start, from - line.start, font, size, fontStyle);
                float x1 = line.rect.x + Advance(text, line.start, to - line.start, font, size, fontStyle);

                if (selectionMax > lineEnd && to == lineEnd)
                {
                    bool bridged = i + 1 < lines.Count &&
                        Mathf.Abs(lines[i + 1].rect.y - line.rect.y) < 0.5f &&
                        lines[i + 1].rect.x > x1;

                    x1 = bridged ? lines[i + 1].rect.x : x1 + size * 0.35f;
                }

                Now.Rectangle(new NowRect(x0, line.rect.y, Mathf.Max(x1 - x0, 1f), line.rect.height))
                    .SetColor(highlightColor)
                    .Draw();
            }

            return true;
        }

        /// <summary>Selects the whole region's text (for context menus and shortcuts).</summary>
        public static void SelectAll(int id, string text)
        {
            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.SelectAll(ref state, text ?? string.Empty);
            NowFocus.Focus(id);
            NowControlState.RequestRepaint();
        }

        /// <summary>The selected text of a region, or empty.</summary>
        public static string GetSelection(int id, string text)
        {
            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            return NowTextEdit.GetSelection(text ?? string.Empty, state);
        }

        static float Advance(string text, int start, int count, NowFontAsset font, float fontSize, NowFontStyle style)
        {
            return count <= 0 ? 0f : font.MeasureText(text, start, count, fontSize, style).x;
        }

        static int HitTest(string text, List<NowTextSelectionLine> lines, NowFontAsset font, float fontSize, NowFontStyle style, Vector2 position)
        {
            float rowY = lines[lines.Count - 1].rect.y;

            for (int i = 0; i < lines.Count; ++i)
            {
                if (position.y < lines[i].rect.yMax)
                {
                    rowY = lines[i].rect.y;
                    break;
                }
            }

            int previousEnd = -1;

            for (int i = 0; i < lines.Count; ++i)
            {
                var segment = lines[i];

                if (Mathf.Abs(segment.rect.y - rowY) > 0.5f)
                    continue;

                if (position.x < segment.rect.x)
                    return previousEnd >= 0 ? previousEnd : segment.start;

                if (position.x <= segment.rect.xMax)
                {
                    float size = segment.fontSize > 0f ? segment.fontSize : fontSize;
                    float x = position.x - segment.rect.x;
                    int index = segment.start;
                    int end = segment.start + segment.length;
                    float advance = 0f;

                    while (index < end)
                    {
                        int next = NowTextEdit.NextIndex(text, index);
                        float glyphWidth = Advance(text, index, next - index, font, size, style);

                        if (advance + glyphWidth * 0.5f >= x)
                            return index;

                        advance += glyphWidth;
                        index = next;
                    }

                    return end;
                }

                previousEnd = segment.start + segment.length;
            }

            return previousEnd >= 0 ? previousEnd : lines[0].start;
        }
    }
}
