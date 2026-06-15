using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Single-line text field. <see cref="Draw(ref string)"/> edits the caller's
    /// string in place and returns true when it changed:
    /// <code>NowLayout.TextField("name").SetPlaceholder("Name...").Draw(ref playerName);</code>
    /// Click to place the caret (shaped-text cluster aware), drag to select,
    /// standard keyboard editing with key repeat, copy/cut/paste/select-all,
    /// double-click selects all. IME composition renders inline at the caret
    /// (underlined) and owns the editing keys until committed. Mobile opens
    /// the on-screen keyboard while focused.
    /// </summary>
    [NowBuilder]
    public struct NowTextField
    {
        readonly NowId _id;
        readonly int _site;
        string _placeholder;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        internal NowTextField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _placeholder = null;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
        }

        internal NowTextField(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowTextField SetPlaceholder(string placeholder) { _placeholder = placeholder; return this; }

        public NowTextField SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowTextField SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowTextField SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowTextField SetTextStyle(NowTextStyle style) { _textPreset = style; return this; }

        public bool Draw(ref string text)
        {
            text ??= string.Empty;
            string original = text;

            var theme = NowTheme.themeAsset;
            int id = NowControls.GetControlId(_id, _site);

            var textStyle = theme.Text(default, _textPreset);
            var fontAsset = textStyle.font;
            float fontSize = textStyle.fontSize;
            float lineHeight = fontAsset != null ? fontAsset.GetLineHeight() * fontSize : fontSize * 1.2f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(200f, lineHeight + 12f));
            var inner = rect.Inset(8f, (rect.height - lineHeight) * 0.5f, 8f, (rect.height - lineHeight) * 0.5f);

            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);

            // Focus gained without a click (tab/gamepad/programmatic): caret to end.
            ref byte hadFocus = ref NowControlState.Get<byte>(NowInput.GetId(id, "hadfocus"));

            if (focused && hadFocus == 0)
            {
                NowTextInput.DiscardPending();
                NowTextInput.setImeEnabled?.Invoke(true);

                if (!interaction.pressed)
                    NowTextEdit.MoveEnd(ref state, text, false);
            }
            else if (!focused && hadFocus == 1)
            {
                NowTextInput.setImeEnabled?.Invoke(false);
            }

            hadFocus = focused ? (byte)1 : (byte)0;

            NowFont resolvedFont = null;
            fontAsset?.TryResolveFont(NowFontStyle.Regular, out resolvedFont);

            if (interaction.pressed)
            {
                int hit = HitTest(fontAsset, resolvedFont, text, interaction.pointerPosition.x - inner.x + state.scrollX, fontSize);

                if (NowControlState.DetectDoubleClick(id, true))
                {
                    NowTextEdit.SelectAll(ref state, text);
                }
                else
                {
                    state.caret = hit;
                    state.anchor = hit;
                }
            }
            else if (interaction.dragging)
            {
                state.caret = HitTest(fontAsset, resolvedFont, text, interaction.pointerPosition.x - inner.x + state.scrollX, fontSize);
            }

            string composition = null;

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
                    if (frame.enterPressed || frame.escapePressed)
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
                            NowTextEdit.Insert(ref text, ref state, buffer.Replace("\n", " ").Replace("\r", string.Empty));
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "bs"), frame.backspaceHeld))
                        NowTextEdit.Backspace(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "del"), frame.deleteHeld))
                        NowTextEdit.Delete(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "left"), frame.leftHeld))
                        NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                    if (NowControlState.Repeat(NowInput.GetId(id, "right"), frame.rightHeld))
                        NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.command);

                    if (frame.homePressed)
                        NowTextEdit.MoveHome(ref state, frame.shift);

                    if (frame.endPressed)
                        NowTextEdit.MoveEnd(ref state, text, frame.shift);
                }

                SyncTouchKeyboard(id, ref text, ref state);
            }
            else if (s_touchKeyboardId == id)
            {
                CloseTouchKeyboard();
            }

            ref float blinkAnchor = ref NowControlState.Get<float>(NowInput.GetId(id, "blink"));
            ref int lastCaret = ref NowControlState.Get<int>(NowInput.GetId(id, "lastcaret"));

            if (state.caret != lastCaret || text != original || interaction.pressed)
            {
                lastCaret = state.caret;
                blinkAnchor = Time.realtimeSinceStartup;
            }

            string display = text;
            int displayCaret = state.caret;

            if (composition != null)
            {
                display = text.Insert(state.caret, composition);
                displayCaret += composition.Length;
            }

            float caretX = PrefixAdvance(fontAsset, resolvedFont, display, displayCaret, fontSize);
            float totalWidth = PrefixAdvance(fontAsset, resolvedFont, display, display.Length, fontSize);

            if (caretX - state.scrollX > inner.width)
                state.scrollX = caretX - inner.width;

            if (caretX < state.scrollX)
                state.scrollX = caretX;

            state.scrollX = Mathf.Clamp(state.scrollX, 0f, Mathf.Max(0f, totalWidth - inner.width));

            if (focused && !NowInput.isPassive)
                NowTextInput.setCompositionCursor?.Invoke(new Vector2(inner.x - state.scrollX + caretX, inner.yMax));

            var box = theme.Rectangle(rect, NowRectangleStyle.Outline);

            if (focused)
            {
                box.outline = 2f;
                box.outlineColor = theme.GetColor(NowColorToken.Accent, Color.blue);
            }

            box.Draw();

            using (Now.Mask(inner))
            {
                float textX = inner.x - state.scrollX;

                if (focused && state.hasSelection && composition == null)
                {
                    float selectionMin = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMin, fontSize);
                    float selectionMax = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMax, fontSize);
                    Color selectionColor = theme.GetColor(NowColorToken.Accent, Color.blue);
                    selectionColor.a = 0.35f;

                    Now.Rectangle(new NowRect(textX + selectionMin, inner.y, selectionMax - selectionMin, inner.height))
                        .SetColor(selectionColor)
                        .Draw();
                }

                if (display.Length > 0)
                {
                    textStyle.rect = new NowRect(textX, inner.y, totalWidth + 4f, inner.height);
                    textStyle.Draw(display);
                }
                else if (!focused && !string.IsNullOrEmpty(_placeholder))
                {
                    var placeholder = theme.Text(new NowRect(inner.x, inner.y, inner.width, inner.height), NowTextStyle.Muted);
                    placeholder.SetFontSize(fontSize).Draw(_placeholder);
                }

                if (composition != null)
                {
                    float compositionX = PrefixAdvance(fontAsset, resolvedFont, display, state.caret, fontSize);

                    Now.Rectangle(new NowRect(textX + compositionX, inner.yMax - 1f, Mathf.Max(caretX - compositionX, 1f), 1f))
                        .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }

                if (focused && NowControlState.Blink(1f, blinkAnchor))
                {
                    Now.Rectangle(new NowRect(textX + caretX, inner.y, 2f, inner.height))
                        .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }
            }

            if (focused && !NowInput.isPassive)
                NowControlState.RequestRepaint();

            return text != original;
        }

        void SyncTouchKeyboard(int id, ref string text, ref NowTextEditState state)
        {
            if (!TouchScreenKeyboard.isSupported)
                return;

            if (s_touchKeyboardId != id || s_touchKeyboard == null)
            {
                CloseTouchKeyboard();
                s_touchKeyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default);
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

        /// <summary>
        /// Advance width of the text before <paramref name="index"/>: shaped clusters
        /// when available, codepoints otherwise.
        /// </summary>
        internal static float PrefixAdvance(NowFontAsset asset, NowFont font, string text, int index, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || index <= 0 || asset == null)
                return 0f;

            if (Now.textShaping && font != null && font.TryGetShapedRun(text, out var run))
            {
                float shaped = 0f;

                for (int i = 0; i < run.Length; ++i)
                {
                    if (run[i].cluster < index)
                        shaped += run[i].xAdvance;
                }

                return shaped * fontSize;
            }

            float advance = 0f;

            for (int i = 0; i < text.Length && i < index; ++i)
            {
                int codepoint = NowFont.ReadCodepoint(text, ref i);

                if (asset.TryResolveGlyph(codepoint, fontSize, NowFontStyle.Regular, out _, out var glyph, out _))
                    advance += glyph.advance * fontSize;
            }

            return advance;
        }

        internal static int HitTest(NowFontAsset asset, NowFont font, string text, float x, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || asset == null)
                return 0;

            float best = Mathf.Abs(x);
            int bestIndex = 0;

            if (Now.textShaping && font != null && font.TryGetShapedRun(text, out var run))
            {
                float advance = 0f;

                for (int i = 0; i < run.Length; ++i)
                {
                    advance += run[i].xAdvance;
                    int boundary = i + 1 < run.Length ? (int)run[i + 1].cluster : text.Length;
                    float distance = Mathf.Abs(x - advance * fontSize);

                    if (distance < best)
                    {
                        best = distance;
                        bestIndex = boundary;
                    }
                }

                return bestIndex;
            }

            float cursor = 0f;

            for (int i = 0; i < text.Length; ++i)
            {
                int codepoint = NowFont.ReadCodepoint(text, ref i);

                if (asset.TryResolveGlyph(codepoint, fontSize, NowFontStyle.Regular, out _, out var glyph, out _))
                    cursor += glyph.advance * fontSize;

                float distance = Mathf.Abs(x - cursor);

                if (distance < best)
                {
                    best = distance;
                    bestIndex = i + 1;
                }
            }

            return bestIndex;
        }
    }
}
