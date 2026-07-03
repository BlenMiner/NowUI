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
    /// on every movement, click/drag/double-click selection, shift-click extends
    /// the selection, Enter inserts a newline (Escape blurs), triple-click
    /// selects the line, IME composition rendered inline at the caret
    /// (underlined, editing keys suppressed until committed), word and line
    /// caret movement following the platform convention (Option/Command on
    /// macOS, Ctrl elsewhere), undo/redo, multi-line clipboard through
    /// <see cref="NowClipboard"/>, auto-growing height between min and max
    /// lines with scroll-to-caret, wheel scrolling and a draggable themed
    /// scrollbar, and a multiline on-screen keyboard on mobile. Rendering uses
    /// per-codepoint metrics so the caret always matches the glyphs.
    /// </summary>
    [NowBuilder]
    public struct NowTextArea
    {
        NowId _id;
        readonly int _site;
        string _placeholder;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        int _minLines;
        int _maxLines;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        internal NowTextArea(NowId id, int site)
        {
            _id = id;
            _site = site;
            _placeholder = null;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
            _minLines = 3;
            _maxLines = 8;
        }

        internal NowTextArea(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTextArea SetPlaceholder(string placeholder) { _placeholder = placeholder; return this; }

        public NowTextArea SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowTextArea SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowTextArea SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowTextArea SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTextArea SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowTextArea SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

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

            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;
            int id = NowControls.GetControlId(_id, _site);

            var textStyle = NowControls.Text(theme, _textPreset);
            var fontAsset = textStyle.font;
            float fontSize = textStyle.fontSize;
            float lineHeight = fontAsset != null ? fontAsset.GetLineHeight(textStyle.fontStyle) * fontSize : fontSize * 1.2f;

            ref int lastLineCount = ref NowControlState.Get<int>(id, "lines");
            int visualLines = Mathf.Clamp(Mathf.Max(lastLineCount, 1), _minLines, _maxLines);

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, renderer.MeasureTextArea(theme, lineHeight, visualLines));
            var inner = renderer.TextAreaInnerRect(theme, rect);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var area = ref NowControlState.Get<AreaState>(id, "area");
            ref var gesture = ref NowControlState.Get<NowTextSelectionGesture>(id, "selection-gesture");

            if (fontAsset == null)
                return false;

            var lines = _lineScratch;
            LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

            if (lastLineCount != lines.Count)
            {
                lastLineCount = lines.Count;
                NowControlState.RequestRepaint();
            }

            // The scrollbar claims its press before the text body, so dragging
            // the thumb scrolls instead of moving the caret (first interaction wins).
            bool scrollbarDragging = false;

            if (!NowInput.isPassive)
            {
                var preMetrics = ScrollbarMetrics(theme, rect, inner, lines.Count * lineHeight, area.scrollY);
                scrollbarDragging = NowScrollbar.Interact(
                    NowInput.GetId(id, "vscroll"), NowScrollbarAxis.Vertical, in preMetrics, ref area.scrollY);
            }

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);
            bool verticalMove = false;
            bool revealCaret = false;

            if (focused && area.hadFocus == 0)
            {
                NowTextInput.DiscardPending();
                NowTextInput.setImeEnabled?.Invoke(true);

                if (!interaction.pressed)
                {
                    NowTextEdit.MoveEnd(ref state, text, false);
                    revealCaret = true;
                }
            }
            else if (!focused && area.hadFocus == 1)
            {
                NowTextInput.setImeEnabled?.Invoke(false);
            }

            area.hadFocus = focused ? (byte)1 : (byte)0;

            string composition = null;

            if (interaction.pressed)
            {
                revealCaret = true;

                int hit = NowTextMetrics.HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, 0f, area.scrollY);

                if (NowTextInput.current.shift)
                {
                    state.caret = hit;
                    var anchorOrigin = new NowTextEditState { caret = state.anchor, anchor = state.anchor };
                    NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Character, in anchorOrigin);
                }
                else
                {
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
            }
            else if (interaction.dragging)
            {
                revealCaret = true;
                int hit = NowTextMetrics.HitTest(text, lines, fontAsset, fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, inner, lineHeight, 0f, area.scrollY);
                NowTextEdit.DragSelectionGesture(ref state, text, in gesture, hit);
                NowControlState.RequestRepaint();
            }

            if (focused && !NowInput.isPassive)
            {
                NowFocus.LockNavigation();
                var frame = NowTextInput.current;
                var undo = NowTextUndoRegistry.Get(id);
                composition = string.IsNullOrEmpty(frame.composition) ? null : frame.composition;

                if (!string.IsNullOrEmpty(frame.characters))
                {
                    revealCaret = true;
                    undo.Push(text, in state, typing: true);
                    NowTextEdit.Insert(ref text, ref state, frame.characters);
                }

                if (composition != null)
                    revealCaret = true;

                // While composing the IME owns the editing keys.
                if (composition == null)
                {
                    if (frame.escapePressed)
                        NowFocus.Clear();

                    if (frame.undoPressed)
                    {
                        revealCaret = true;
                        undo.Undo(ref text, ref state);
                    }

                    if (frame.redoPressed)
                    {
                        revealCaret = true;
                        undo.Redo(ref text, ref state);
                    }

                    if (frame.selectAllPressed)
                    {
                        revealCaret = true;
                        NowTextEdit.SelectAll(ref state, text);
                    }

                    if (frame.copyPressed && state.hasSelection)
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));

                    if (frame.cutPressed && state.hasSelection)
                    {
                        revealCaret = true;
                        undo.Push(text, in state, typing: false);
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));
                        NowTextEdit.DeleteSelection(ref text, ref state);
                    }

                    if (frame.pastePressed)
                    {
                        revealCaret = true;
                        string buffer = NowClipboard.Paste();

                        if (!string.IsNullOrEmpty(buffer))
                        {
                            undo.Push(text, in state, typing: false);
                            NowTextEdit.Insert(ref text, ref state, buffer.Replace("\r\n", "\n").Replace('\r', '\n'));
                        }
                    }

                    if (NowControlState.Repeat(id, "enter", frame.enterHeld))
                    {
                        revealCaret = true;
                        undo.Push(text, in state, typing: true);
                        NowTextEdit.Insert(ref text, ref state, "\n");
                    }

                    if (NowControlState.Repeat(id, "bs", frame.backspaceHeld))
                    {
                        revealCaret = true;
                        undo.Push(text, in state, typing: true);

                        if (frame.lineModifier)
                            NowTextEdit.DeleteToLineStart(ref text, ref state);
                        else
                            NowTextEdit.Backspace(ref text, ref state, frame.wordModifier);
                    }

                    if (NowControlState.Repeat(id, "del", frame.deleteHeld))
                    {
                        revealCaret = true;
                        undo.Push(text, in state, typing: true);
                        NowTextEdit.Delete(ref text, ref state, frame.wordModifier);
                    }

                    if (text != original)
                        LayoutLines(text, fontAsset, fontSize, textStyle.fontStyle, inner.width, lines);

                    if (NowControlState.Repeat(id, "left", frame.leftHeld))
                    {
                        revealCaret = true;

                        if (frame.lineModifier)
                        {
                            int line = NowTextMetrics.LineOf(text, lines, state.caret);
                            state.caret = lines[line].start;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                        else
                        {
                            NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.wordModifier);
                        }
                    }

                    if (NowControlState.Repeat(id, "right", frame.rightHeld))
                    {
                        revealCaret = true;

                        if (frame.lineModifier)
                        {
                            int line = NowTextMetrics.LineOf(text, lines, state.caret);
                            state.caret = lines[line].start + lines[line].length;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                        else
                        {
                            NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.wordModifier);
                        }
                    }

                    if (NowControlState.Repeat(id, "up", frame.upHeld))
                    {
                        revealCaret = true;

                        if (frame.lineModifier)
                        {
                            NowTextEdit.MoveHome(ref state, frame.shift);
                        }
                        else
                        {
                            MoveVertical(ref state, text, lines, fontAsset, fontSize,
                                textStyle.fontStyle, area.goalX, -1, frame.shift);
                            verticalMove = true;
                        }
                    }

                    if (NowControlState.Repeat(id, "down", frame.downHeld))
                    {
                        revealCaret = true;

                        if (frame.lineModifier)
                        {
                            NowTextEdit.MoveEnd(ref state, text, frame.shift);
                        }
                        else
                        {
                            MoveVertical(ref state, text, lines, fontAsset, fontSize,
                                textStyle.fontStyle, area.goalX, 1, frame.shift);
                            verticalMove = true;
                        }
                    }

                    if (frame.homePressed)
                    {
                        revealCaret = true;

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
                        revealCaret = true;

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

            revealCaret |= state.caret != area.lastCaret || text != original;

            if (revealCaret)
            {
                area.lastCaret = state.caret;
                area.blinkAnchor = Time.realtimeSinceStartup;

                if (!verticalMove)
                    area.goalX = caretX;
            }

            float contentHeight = lines.Count * lineHeight;
            float maxScroll = Mathf.Max(0f, contentHeight - inner.height);

            float pendingWheel = NowInput.current.scrollDelta.y;
            float wheelStep = theme.controlStyles.scrollWheelStep;

            if (interaction.hovered && maxScroll > 0f && WouldWheelMove(area.scrollY, maxScroll, pendingWheel, wheelStep))
            {
                float wheel = NowInput.ConsumeScrollDelta(rect).y;

                if (wheel != 0f)
                {
                    area.scrollY -= wheel * wheelStep;
                    NowControlState.RequestRepaint();
                }
            }

            if (focused && revealCaret)
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

            renderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused));

            using (Now.Mask(inner))
            {
                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(area.scrollY / lineHeight));
                int lastVisible = Mathf.Min(lines.Count - 1, Mathf.CeilToInt((area.scrollY + inner.height) / lineHeight));

                if (focused && state.hasSelection && composition == null)
                    DrawSelection(theme, renderer, text, lines, fontAsset, fontSize, textStyle.fontStyle,
                        inner, lineHeight, area.scrollY, state, firstVisible, lastVisible);

                if (display.Length == 0 && !string.IsNullOrEmpty(_placeholder))
                {
                    var placeholder = NowControls.Text(theme, NowTextStyle.Muted);
                    placeholder.rect = new NowRect(inner.x, inner.y, inner.width, lineHeight);
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
                    renderer.DrawCaret(theme, new NowRect(
                            inner.x + caretX,
                            inner.y + caretLine * lineHeight - area.scrollY,
                            theme.controlStyles.caretWidth,
                            lineHeight));
                }
            }

            if (maxScroll > 0f)
            {
                int thumbId = NowInput.GetId(id, "vscroll");
                var metrics = ScrollbarMetrics(theme, rect, inner, contentHeight, area.scrollY);
                float hoverT = NowControlState.Transition(thumbId, NowInput.IsHovered(metrics.track.Outset(4f, 2f)));
                renderer.DrawScrollbar(new NowScrollbarRenderContext(
                    theme, NowScrollbarAxis.Vertical, metrics, scrollbarDragging, hoverT));
            }

            if (focused)
                NowControlState.RequestRepaint();

            return text != original;
        }

        static NowScrollbarMetrics ScrollbarMetrics(NowThemeAsset theme, NowRect rect, NowRect inner, float contentHeight, float scrollY)
        {
            float barWidth = theme.controlStyles.scrollbarWidth;
            var track = new NowRect(
                rect.xMax - barWidth - 2f,
                inner.y + 2f,
                barWidth,
                Mathf.Max(0f, inner.height - 4f));

            return NowScrollbar.Calculate(
                NowScrollbarAxis.Vertical,
                track,
                inner.height,
                contentHeight,
                scrollY,
                theme.controlStyles.scrollbarMinThumbSize);
        }

        static bool WouldWheelMove(float scrollY, float maxScroll, float wheel, float step)
        {
            if (wheel == 0f)
                return false;

            float next = Mathf.Clamp(scrollY - wheel * step, 0f, maxScroll);
            return !Mathf.Approximately(next, scrollY);
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

        static void DrawSelection(NowThemeAsset themeAsset, NowControlRenderer renderer, string text, List<NowTextLine> lines, NowFontAsset font,
            float fontSize, NowFontStyle style, NowRect inner, float lineHeight, float scrollY,
            in NowTextEditState state, int firstVisible, int lastVisible)
        {
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

                renderer.DrawSelection(themeAsset, new NowRect(
                        inner.x + x0,
                        inner.y + i * lineHeight - scrollY,
                        Mathf.Max(x1 - x0, 1f),
                        lineHeight));
            }
        }

        static void DrawCompositionUnderline(NowThemeAsset themeAsset, string display, List<NowTextLine> lines,
            NowFontAsset font, float fontSize, NowFontStyle style, NowRect inner, float lineHeight, float scrollY,
            int from, int to, int firstVisible, int lastVisible)
        {
            var renderer = themeAsset.controlRenderer;
            float underlineHeight = themeAsset.controlStyles.compositionUnderlineHeight;

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

                renderer.DrawCompositionUnderline(themeAsset, new NowRect(
                        inner.x + x0,
                        inner.y + (i + 1) * lineHeight - scrollY - underlineHeight,
                        Mathf.Max(x1 - x0, 1f),
                        underlineHeight));
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
