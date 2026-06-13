using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>One visual line of an editable text area: a range into the source text.</summary>
    public struct NowTextAreaLine
    {
        public int start;

        public int length;
    }

    /// <summary>
    /// Multi-line text area. <see cref="Draw(ref string)"/> edits the caller's
    /// string in place and returns true when it changed:
    /// <code>NowLayout.TextArea().SetPlaceholder("Notes...").Draw(ref notes);</code>
    /// Word-wrapped editing with every character preserved, caret up/down with a
    /// pixel goal column, Home/End (Ctrl for document start/end), shift-selection
    /// on every movement, click/drag/double-click selection, Enter inserts a
    /// newline (Escape blurs), triple-click selects the line, IME composition
    /// rendered inline at the caret (underlined, editing keys suppressed until
    /// committed), Ctrl+Backspace/Delete word ops, multi-line
    /// clipboard through <see cref="NowClipboard"/>, auto-growing height
    /// between min and max lines with scroll-to-caret and wheel scrolling, and a
    /// multiline on-screen keyboard on mobile. Rendering uses per-codepoint
    /// metrics so the caret always matches the glyphs.
    /// </summary>
    [NowBuilder]
    public struct NowTextArea
    {
        readonly string _id;
        readonly int _site;
        string _placeholder;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        int _minLines;
        int _maxLines;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        internal NowTextArea(string id, int site)
        {
            _id = id;
            _site = site;
            _placeholder = null;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
            _minLines = 3;
            _maxLines = 8;
        }

        internal NowTextArea(NowRect rect, string id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTextArea SetPlaceholder(string placeholder) { _placeholder = placeholder; return this; }

        public NowTextArea SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowTextArea SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowTextArea SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowTextArea SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Height bounds in visual lines; the area grows with content between them.</summary>
        public NowTextArea SetLines(int minLines, int maxLines)
        {
            _minLines = Mathf.Max(1, minLines);
            _maxLines = Mathf.Max(_minLines, maxLines);
            return this;
        }

        struct AreaState
        {
            public float scrollY;
            public float goalX;
            public int lastCaret;
            public float blinkAnchor;
            public byte hadFocus;
        }

        static readonly List<NowTextLine> _lineScratch = new List<NowTextLine>(16);

        static readonly List<NowTextLine> _compatLineScratch = new List<NowTextLine>(16);

        public bool Draw(ref string text)
        {
            text ??= string.Empty;
            string original = text;

            var theme = NowControls.theme;
            int id = _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);

            var textStyle = theme.Text(default, _textPreset);
            var fontAsset = textStyle.font;
            float fontSize = textStyle.fontSize;
            float lineHeight = fontAsset != null ? fontAsset.GetLineHeight(textStyle.fontStyle) * fontSize : fontSize * 1.2f;

            ref int lastLineCount = ref NowControlState.Get<int>(NowInput.GetId(id, "lines"));
            int visualLines = Mathf.Clamp(Mathf.Max(lastLineCount, 1), _minLines, _maxLines);
            float boxHeight = visualLines * lineHeight + 12f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(200f, boxHeight));
            var inner = rect.Inset(8f, 6f, 8f, 6f);

            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var area = ref NowControlState.Get<AreaState>(NowInput.GetId(id, "area"));

            if (fontAsset == null)
                return false;

            var lines = _lineScratch;
            LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

            if (lastLineCount != lines.Count)
            {
                lastLineCount = lines.Count;
                NowControlState.RequestRepaint();
            }

            if (focused && area.hadFocus == 0)
            {
                NowTextInput.setImeEnabled?.Invoke(true);

                if (!interaction.pressed)
                    NowTextEdit.MoveEnd(ref state, text, false);
            }
            else if (!focused && area.hadFocus == 1)
            {
                NowTextInput.setImeEnabled?.Invoke(false);
            }

            area.hadFocus = focused ? (byte)1 : (byte)0;

            bool verticalMove = false;
            string composition = null;

            if (interaction.pressed)
            {
                int hit = NowTextMetrics.HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, 0f, area.scrollY);
                int streak = NowControlState.ClickStreak(id, true);

                if (streak >= 3)
                {
                    NowTextEdit.SelectLine(ref state, text, hit);
                }
                else if (streak == 2)
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
                state.caret = NowTextMetrics.HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, 0f, area.scrollY);
                NowControlState.RequestRepaint();
            }

            if (focused && !NowInput.isPassive)
            {
                NowFocus.LockNavigation();
                var frame = NowTextInput.current;
                composition = string.IsNullOrEmpty(frame.composition) ? null : frame.composition;

                if (!string.IsNullOrEmpty(frame.characters))
                    NowTextEdit.Insert(ref text, ref state, frame.characters);

                // While composing the IME owns the editing keys.
                if (composition == null)
                {
                    if (frame.escapePressed)
                        NowFocus.Clear();

                    if (frame.selectAllPressed)
                        NowTextEdit.SelectAll(ref state, text);

                    if (frame.copyPressed && state.hasSelection)
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));

                    if (frame.cutPressed && state.hasSelection)
                    {
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));
                        NowTextEdit.DeleteSelection(ref text, ref state);
                    }

                    if (frame.pastePressed)
                    {
                        string buffer = NowClipboard.Paste();

                        if (!string.IsNullOrEmpty(buffer))
                            NowTextEdit.Insert(ref text, ref state, buffer.Replace("\r\n", "\n").Replace('\r', '\n'));
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "enter"), frame.enterHeld))
                        NowTextEdit.Insert(ref text, ref state, "\n");

                    if (NowControlState.Repeat(NowInput.GetId(id, "bs"), frame.backspaceHeld))
                        NowTextEdit.Backspace(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "del"), frame.deleteHeld))
                        NowTextEdit.Delete(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "left"), frame.leftHeld))
                        NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "right"), frame.rightHeld))
                        NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.command);

                    if (text != original)
                        LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

                    if (NowControlState.Repeat(NowInput.GetId(id, "up"), frame.upHeld))
                    {
                        MoveVertical(ref state, text, lines, fontAsset, fontSize,
                            textStyle.fontStyle, area.goalX, -1, frame.shift);
                        verticalMove = true;
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "down"), frame.downHeld))
                    {
                        MoveVertical(ref state, text, lines, fontAsset, fontSize,
                            textStyle.fontStyle, area.goalX, 1, frame.shift);
                        verticalMove = true;
                    }

                    if (frame.homePressed)
                    {
                        if (frame.command)
                        {
                            NowTextEdit.MoveHome(ref state, frame.shift);
                        }
                        else
                        {
                            int line = NowTextMetrics.LineOf(text, lines, state.caret);
                            state.caret = lines[line].start;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                    }

                    if (frame.endPressed)
                    {
                        if (frame.command)
                        {
                            NowTextEdit.MoveEnd(ref state, text, frame.shift);
                        }
                        else
                        {
                            int line = NowTextMetrics.LineOf(text, lines, state.caret);
                            state.caret = lines[line].start + lines[line].length;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                    }
                }

                SyncTouchKeyboard(id, ref text, ref state);
            }
            else if (s_touchKeyboardId == id)
            {
                CloseTouchKeyboard();
            }

            string display = text;
            int displayCaret = state.caret;

            if (composition != null)
            {
                display = text.Insert(state.caret, composition);
                displayCaret += composition.Length;
            }

            if (text != original || composition != null)
                LayoutLines(display, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

            int caretLine = NowTextMetrics.LineOf(display, lines, displayCaret);
            float caretX = NowTextMetrics.Advance(display, fontAsset, fontSize, textStyle.fontStyle,
                lines[caretLine].start, displayCaret - lines[caretLine].start);

            if (state.caret != area.lastCaret || text != original || interaction.pressed)
            {
                area.lastCaret = state.caret;
                area.blinkAnchor = Time.realtimeSinceStartup;

                if (!verticalMove)
                    area.goalX = caretX;
            }

            float contentHeight = lines.Count * lineHeight;
            float maxScroll = Mathf.Max(0f, contentHeight - inner.height);

            if (!NowInput.isPassive && interaction.hovered && maxScroll > 0f)
            {
                float wheel = NowInput.current.scrollDelta.y;

                if (wheel != 0f)
                {
                    area.scrollY -= wheel * lineHeight * 2f;
                    NowControlState.RequestRepaint();
                }
            }

            if (focused)
            {
                float caretTop = caretLine * lineHeight;

                if (caretTop < area.scrollY)
                    area.scrollY = caretTop;

                if (caretTop + lineHeight > area.scrollY + inner.height)
                    area.scrollY = caretTop + lineHeight - inner.height;
            }

            area.scrollY = Mathf.Clamp(area.scrollY, 0f, maxScroll);

            if (focused && !NowInput.isPassive)
                NowTextInput.setCompositionCursor?.Invoke(new Vector2(
                    inner.x + caretX,
                    inner.y + caretLine * lineHeight - area.scrollY + lineHeight));

            var box = theme.Rectangle(rect, NowRectangleStyle.Outline);

            if (focused)
            {
                box.outline = 2f;
                box.outlineColor = theme.GetColor(NowColorToken.Accent, Color.blue);
            }

            box.Draw();

            using (Now.Mask(inner))
            {
                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(area.scrollY / lineHeight));
                int lastVisible = Mathf.Min(lines.Count - 1, Mathf.CeilToInt((area.scrollY + inner.height) / lineHeight));

                if (focused && state.hasSelection && composition == null)
                    DrawSelection(theme, text, lines, fontAsset, fontSize, textStyle.fontStyle,
                        inner, lineHeight, area.scrollY, state, firstVisible, lastVisible);

                if (display.Length == 0 && !focused && !string.IsNullOrEmpty(_placeholder))
                {
                    var placeholder = theme.Text(new NowRect(inner.x, inner.y, inner.width, lineHeight), NowTextStyle.Muted);
                    placeholder.SetFontSize(fontSize).Draw(_placeholder);
                }

                for (int i = firstVisible; i <= lastVisible && i < lines.Count; ++i)
                {
                    var line = lines[i];

                    if (line.length == 0)
                        continue;

                    var lineStyle = textStyle;
                    lineStyle.rect = new NowRect(inner.x, inner.y + i * lineHeight - area.scrollY, inner.width + 2f, lineHeight);
                    lineStyle.Draw(System.MemoryExtensions.AsSpan(display, line.start, line.length));
                }

                if (composition != null)
                    DrawCompositionUnderline(theme, display, lines, fontAsset, fontSize, textStyle.fontStyle,
                        inner, lineHeight, area.scrollY, state.caret, displayCaret, firstVisible, lastVisible);

                if (focused && NowControlState.Blink(1f, area.blinkAnchor))
                {
                    Now.Rectangle(new NowRect(
                            inner.x + caretX,
                            inner.y + caretLine * lineHeight - area.scrollY,
                            2f,
                            lineHeight))
                        .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }
            }

            if (maxScroll > 0f)
            {
                float trackHeight = inner.height;
                float thumbHeight = Mathf.Max(18f, trackHeight * (inner.height / contentHeight));
                float travel = trackHeight - thumbHeight;
                float normalized = maxScroll > 0f ? area.scrollY / maxScroll : 0f;

                Now.Rectangle(new NowRect(rect.xMax - 5f, inner.y + travel * normalized, 3f, thumbHeight))
                    .SetColor(theme.GetColor(NowColorToken.Border, Color.gray))
                    .SetRadius(1.5f)
                    .Draw();
            }

            if (focused)
                NowControlState.RequestRepaint();

            return text != original;
        }

        /// <summary>
        /// Breaks the text into visual lines covering every character: wrap at
        /// word boundaries, hard-split words wider than the width, '\n' always
        /// ends a line (a trailing '\n' yields an empty final line so the caret
        /// can sit there). Public so custom editors can reuse the exact metrics.
        /// </summary>
        public static void LayoutLines(string text, NowFontAsset font, float fontSize, NowFontStyle style, float width, List<NowTextAreaLine> lines)
        {
            lines.Clear();
            NowTextMetrics.LayoutWrappedLines(text, font, fontSize, style, width, _compatLineScratch);

            for (int i = 0; i < _compatLineScratch.Count; ++i)
                lines.Add(new NowTextAreaLine { start = _compatLineScratch[i].start, length = _compatLineScratch[i].length });
        }

        internal static void LayoutLines(string text, NowFontAsset font, float fontSize, NowFontStyle style, float width, List<NowTextLine> lines)
        {
            NowTextMetrics.LayoutWrappedLines(text, font, fontSize, style, width, lines);
        }

        /// <summary>The line containing the caret index (wrap boundaries belong to the next line).</summary>
        public static int LineOf(string text, List<NowTextAreaLine> lines, int index)
        {
            for (int i = 0; i < lines.Count; ++i)
            {
                int end = lines[i].start + lines[i].length;
                bool hardEnd = end >= text.Length || text[end] == '\n';

                if (index < end || (hardEnd && index == end))
                    return i;
            }

            return lines.Count - 1;
        }

        static void MoveVertical(ref NowTextEditState state, string text, List<NowTextLine> lines,
            NowFontAsset font, float fontSize, NowFontStyle style, float goalX, int direction, bool select)
        {
            if (!select && state.hasSelection)
            {
                state.caret = direction < 0 ? state.selectionMin : state.selectionMax;
                state.anchor = state.caret;
            }

            int line = NowTextMetrics.LineOf(text, lines, state.caret);
            int target = line + direction;

            if (target < 0)
                state.caret = 0;
            else if (target >= lines.Count)
                state.caret = text.Length;
            else
                state.caret = NowTextMetrics.HitIndex(text, lines[target], font, fontSize, style, goalX);

            if (!select)
                state.anchor = state.caret;
        }

        static void DrawSelection(NowTheme theme, string text, List<NowTextLine> lines, NowFontAsset font,
            float fontSize, NowFontStyle style, NowRect inner, float lineHeight, float scrollY,
            in NowTextEditState state, int firstVisible, int lastVisible)
        {
            Color highlight = theme.GetColor(NowColorToken.Accent, Color.blue);
            highlight.a = 0.3f;
            int selectionMin = state.selectionMin;
            int selectionMax = state.selectionMax;

            for (int i = firstVisible; i <= lastVisible && i < lines.Count; ++i)
            {
                var line = lines[i];
                int lineEnd = line.start + line.length;

                if (selectionMax <= line.start || selectionMin > lineEnd)
                    continue;

                int from = Mathf.Max(selectionMin, line.start);
                int to = Mathf.Min(selectionMax, lineEnd);
                float x0 = NowTextMetrics.Advance(text, font, fontSize, style, line.start, from - line.start);
                float x1 = NowTextMetrics.Advance(text, font, fontSize, style, line.start, to - line.start);

                if (selectionMax > lineEnd)
                    x1 += fontSize * 0.35f;

                Now.Rectangle(new NowRect(
                        inner.x + x0,
                        inner.y + i * lineHeight - scrollY,
                        Mathf.Max(x1 - x0, 1f),
                        lineHeight))
                    .SetColor(highlight)
                    .Draw();
            }
        }

        static void DrawCompositionUnderline(NowTheme theme, string display, List<NowTextLine> lines,
            NowFontAsset font, float fontSize, NowFontStyle style, NowRect inner, float lineHeight, float scrollY,
            int from, int to, int firstVisible, int lastVisible)
        {
            Color underline = theme.GetColor(NowColorToken.Text, Color.black);

            for (int i = firstVisible; i <= lastVisible && i < lines.Count; ++i)
            {
                var line = lines[i];
                int lineEnd = line.start + line.length;

                if (to <= line.start || from >= lineEnd)
                    continue;

                int rangeFrom = Mathf.Max(from, line.start);
                int rangeTo = Mathf.Min(to, lineEnd);
                float x0 = NowTextMetrics.Advance(display, font, fontSize, style, line.start, rangeFrom - line.start);
                float x1 = NowTextMetrics.Advance(display, font, fontSize, style, line.start, rangeTo - line.start);

                Now.Rectangle(new NowRect(
                        inner.x + x0,
                        inner.y + (i + 1) * lineHeight - scrollY - 1f,
                        Mathf.Max(x1 - x0, 1f),
                        1f))
                    .SetColor(underline)
                    .Draw();
            }
        }

        void SyncTouchKeyboard(int id, ref string text, ref NowTextEditState state)
        {
            if (!TouchScreenKeyboard.isSupported)
                return;

            if (s_touchKeyboardId != id || s_touchKeyboard == null)
            {
                CloseTouchKeyboard();
                s_touchKeyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default, false, true);
                s_touchKeyboardId = id;
                return;
            }

            if (s_touchKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                if (s_touchKeyboard.text != text)
                {
                    text = s_touchKeyboard.text;
                    NowTextEdit.MoveEnd(ref state, text, false);
                }
            }
            else
            {
                if (s_touchKeyboard.status == TouchScreenKeyboard.Status.Done && s_touchKeyboard.text != text)
                {
                    text = s_touchKeyboard.text;
                    NowTextEdit.MoveEnd(ref state, text, false);
                }

                NowFocus.Clear();
                CloseTouchKeyboard();
            }
        }

        static void CloseTouchKeyboard()
        {
            if (s_touchKeyboard != null && s_touchKeyboard.active)
                s_touchKeyboard.active = false;

            s_touchKeyboard = null;
            s_touchKeyboardId = 0;
        }
    }
}
