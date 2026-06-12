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
    /// newline (Escape blurs), Ctrl+Backspace/Delete word ops, multi-line
    /// clipboard through <see cref="NowUIClipboard"/>, auto-growing height
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

        static readonly List<NowTextAreaLine> _lineScratch = new List<NowTextAreaLine>(16);

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

            ref int lastLineCount = ref NowUIControlState.Get<int>(NowUIInput.GetId(id, "lines"));
            int visualLines = Mathf.Clamp(Mathf.Max(lastLineCount, 1), _minLines, _maxLines);
            float boxHeight = visualLines * lineHeight + 12f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(200f, boxHeight));
            var inner = rect.Inset(8f, 6f, 8f, 6f);

            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            ref var state = ref NowUIControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var area = ref NowUIControlState.Get<AreaState>(NowUIInput.GetId(id, "area"));

            if (fontAsset == null)
                return false;

            var lines = _lineScratch;
            LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

            if (lastLineCount != lines.Count)
            {
                lastLineCount = lines.Count;
                NowUIControlState.RequestRepaint();
            }

            if (focused && area.hadFocus == 0 && !interaction.pressed)
                NowTextEdit.MoveEnd(ref state, text, false);

            area.hadFocus = focused ? (byte)1 : (byte)0;

            bool verticalMove = false;

            if (interaction.pressed)
            {
                int hit = HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, area.scrollY);

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
                state.caret = HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, area.scrollY);
                NowUIControlState.RequestRepaint();
            }

            if (focused && !NowUIInput.isPassive)
            {
                NowUIFocus.LockNavigation();
                var frame = NowUITextInput.current;

                if (frame.escapePressed)
                    NowUIFocus.Clear();

                if (frame.selectAllPressed)
                    NowTextEdit.SelectAll(ref state, text);

                if (frame.copyPressed && state.hasSelection)
                    NowUIClipboard.Copy(NowTextEdit.GetSelection(text, state));

                if (frame.cutPressed && state.hasSelection)
                {
                    NowUIClipboard.Copy(NowTextEdit.GetSelection(text, state));
                    NowTextEdit.DeleteSelection(ref text, ref state);
                }

                if (frame.pastePressed)
                {
                    string buffer = NowUIClipboard.Paste();

                    if (!string.IsNullOrEmpty(buffer))
                        NowTextEdit.Insert(ref text, ref state, buffer.Replace("\r\n", "\n").Replace('\r', '\n'));
                }

                if (frame.enterPressed)
                    NowTextEdit.Insert(ref text, ref state, "\n");

                if (!string.IsNullOrEmpty(frame.characters))
                    NowTextEdit.Insert(ref text, ref state, frame.characters);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "bs"), frame.backspaceHeld))
                    NowTextEdit.Backspace(ref text, ref state, frame.command);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "del"), frame.deleteHeld))
                    NowTextEdit.Delete(ref text, ref state, frame.command);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "left"), frame.leftHeld))
                    NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "right"), frame.rightHeld))
                    NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.command);

                if (text != original)
                    LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "up"), frame.upHeld))
                {
                    MoveVertical(ref state, ref area, text, lines, fontAsset, fontSize, textStyle.fontStyle, -1, frame.shift);
                    verticalMove = true;
                }

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "down"), frame.downHeld))
                {
                    MoveVertical(ref state, ref area, text, lines, fontAsset, fontSize, textStyle.fontStyle, 1, frame.shift);
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
                        int line = LineOf(text, lines, state.caret);
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
                        int line = LineOf(text, lines, state.caret);
                        state.caret = lines[line].start + lines[line].length;

                        if (!frame.shift)
                            state.anchor = state.caret;
                    }
                }

                SyncTouchKeyboard(id, ref text, ref state);
            }
            else if (s_touchKeyboardId == id)
            {
                CloseTouchKeyboard();
            }

            if (text != original)
                LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

            int caretLine = LineOf(text, lines, state.caret);
            float caretX = Advance(text, fontAsset, fontSize, textStyle.fontStyle, lines[caretLine].start, state.caret - lines[caretLine].start);

            if (state.caret != area.lastCaret || text != original || interaction.pressed)
            {
                area.lastCaret = state.caret;
                area.blinkAnchor = Time.realtimeSinceStartup;

                if (!verticalMove)
                    area.goalX = caretX;
            }

            float contentHeight = lines.Count * lineHeight;
            float maxScroll = Mathf.Max(0f, contentHeight - inner.height);

            if (!NowUIInput.isPassive && interaction.hovered && maxScroll > 0f)
            {
                float wheel = NowUIInput.current.scrollDelta.y;

                if (wheel != 0f)
                {
                    area.scrollY -= wheel * lineHeight * 2f;
                    NowUIControlState.RequestRepaint();
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

                if (focused && state.hasSelection)
                    DrawSelection(theme, text, lines, fontAsset, fontSize, textStyle.fontStyle,
                        inner, lineHeight, area.scrollY, state, firstVisible, lastVisible);

                if (text.Length == 0 && !focused && !string.IsNullOrEmpty(_placeholder))
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
                    lineStyle.Draw(System.MemoryExtensions.AsSpan(text, line.start, line.length));
                }

                if (focused && NowUIControlState.Blink(1f, area.blinkAnchor))
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
                NowUIControlState.RequestRepaint();

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

            if (string.IsNullOrEmpty(text))
            {
                lines.Add(new NowTextAreaLine { start = 0, length = 0 });
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
                    lines.Add(new NowTextAreaLine { start = lineStart, length = i - lineStart });
                    ++i;
                    lineStart = i;
                    lastBreak = -1;
                    x = 0f;
                    continue;
                }

                int next = NowTextEdit.NextIndex(text, i);
                float advance = font.MeasureText(text, i, next - i, fontSize, style).x;

                if (x + advance > width && i > lineStart && c != ' ')
                {
                    int breakAt = lastBreak > lineStart ? lastBreak : i;
                    lines.Add(new NowTextAreaLine { start = lineStart, length = breakAt - lineStart });
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

            lines.Add(new NowTextAreaLine { start = lineStart, length = text.Length - lineStart });
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

        static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style, int start, int count)
        {
            return count <= 0 ? 0f : font.MeasureText(text, start, count, fontSize, style).x;
        }

        static int HitIndex(string text, in NowTextAreaLine line, NowFontAsset font, float fontSize, NowFontStyle style, float x)
        {
            if (x <= 0f)
                return line.start;

            int index = line.start;
            int end = line.start + line.length;
            float advance = 0f;

            while (index < end)
            {
                int next = NowTextEdit.NextIndex(text, index);
                float glyph = font.MeasureText(text, index, next - index, fontSize, style).x;

                if (advance + glyph * 0.5f >= x)
                    return index;

                advance += glyph;
                index = next;
            }

            return end;
        }

        static int HitTest(string text, List<NowTextAreaLine> lines, NowFontAsset font, float fontSize, NowFontStyle style,
            Vector2 pointer, NowRect inner, float lineHeight, float scrollY)
        {
            float localY = pointer.y - inner.y + scrollY;
            int lineIndex = Mathf.Clamp(Mathf.FloorToInt(localY / lineHeight), 0, lines.Count - 1);
            return HitIndex(text, lines[lineIndex], font, fontSize, style, pointer.x - inner.x);
        }

        static void MoveVertical(ref NowTextEditState state, ref AreaState area, string text, List<NowTextAreaLine> lines,
            NowFontAsset font, float fontSize, NowFontStyle style, int direction, bool select)
        {
            if (!select && state.hasSelection)
            {
                state.caret = direction < 0 ? state.selectionMin : state.selectionMax;
                state.anchor = state.caret;
            }

            int line = LineOf(text, lines, state.caret);
            int target = line + direction;

            if (target < 0)
            {
                state.caret = 0;
            }
            else if (target >= lines.Count)
            {
                state.caret = text.Length;
            }
            else
            {
                state.caret = HitIndex(text, lines[target], font, fontSize, style, area.goalX);
            }

            if (!select)
                state.anchor = state.caret;
        }

        static void DrawSelection(NowUITheme theme, string text, List<NowTextAreaLine> lines, NowFontAsset font,
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
                float x0 = Advance(text, font, fontSize, style, line.start, from - line.start);
                float x1 = Advance(text, font, fontSize, style, line.start, to - line.start);

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

                NowUIFocus.Clear();
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
