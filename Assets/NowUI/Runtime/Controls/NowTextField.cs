using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Single-line text field. <see cref="Draw(ref string)"/> edits the caller's
    /// string in place and returns true when it changed:
    /// <code>NowLayout.TextField("name").SetPlaceholder("Name...").Draw(ref playerName);</code>
    /// Click to place the caret (shaped-text cluster aware), drag to select,
    /// standard keyboard editing with key repeat, copy/cut/paste/select-all,
    /// double-click selects all. Mobile opens the on-screen keyboard while focused.
    /// </summary>
    [NowBuilder]
    public struct NowTextField
    {
        readonly string _id;
        string _placeholder;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        internal NowTextField(string id)
        {
            _id = id ?? "textfield";
            _placeholder = null;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
        }

        internal NowTextField(NowRect rect, string id) : this(id)
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

            var theme = NowControls.theme;
            int id = NowControls.GetControlId(_id);

            var textStyle = theme.Text(default, _textPreset);
            var fontAsset = textStyle.font;
            float fontSize = textStyle.fontSize;
            float lineHeight = fontAsset != null ? fontAsset.GetLineHeight() * fontSize : fontSize * 1.2f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(200f, lineHeight + 12f));
            var inner = rect.Inset(8f, (rect.height - lineHeight) * 0.5f, 8f, (rect.height - lineHeight) * 0.5f);

            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            ref var state = ref NowUIControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);

            // Focus gained without a click (tab/gamepad/programmatic): caret to end.
            ref byte hadFocus = ref NowUIControlState.Get<byte>(NowUIInput.GetId(id, "hadfocus"));

            if (focused && hadFocus == 0 && !interaction.pressed)
                NowTextEdit.MoveEnd(ref state, text, false);

            hadFocus = focused ? (byte)1 : (byte)0;

            NowFont resolvedFont = null;
            fontAsset?.TryResolveFont(NowFontStyle.Regular, out resolvedFont);

            // Pointer: caret placement and drag selection.
            if (interaction.pressed)
            {
                int hit = HitTest(fontAsset, resolvedFont, text, interaction.pointerPosition.x - inner.x + state.scrollX, fontSize);

                if (NowUIControlState.DetectDoubleClick(id, true))
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

            // Keyboard editing while focused.
            if (focused && !NowUIInput.isPassive)
            {
                var frame = NowUITextInput.current;

                if (frame.enterPressed || frame.escapePressed)
                    NowUIFocus.Clear();

                if (frame.selectAllPressed)
                    NowTextEdit.SelectAll(ref state, text);

                if (frame.copyPressed && state.hasSelection)
                    GUIUtility.systemCopyBuffer = NowTextEdit.GetSelection(text, state);

                if (frame.cutPressed && state.hasSelection)
                {
                    GUIUtility.systemCopyBuffer = NowTextEdit.GetSelection(text, state);
                    NowTextEdit.DeleteSelection(ref text, ref state);
                }

                if (frame.pastePressed)
                {
                    string buffer = GUIUtility.systemCopyBuffer;

                    if (!string.IsNullOrEmpty(buffer))
                        NowTextEdit.Insert(ref text, ref state, buffer.Replace("\n", " ").Replace("\r", string.Empty));
                }

                if (!string.IsNullOrEmpty(frame.characters))
                    NowTextEdit.Insert(ref text, ref state, frame.characters);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "bs"), frame.backspaceHeld))
                    NowTextEdit.Backspace(ref text, ref state);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "del"), frame.deleteHeld))
                    NowTextEdit.Delete(ref text, ref state);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "left"), frame.leftHeld))
                    NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                if (NowUIControlState.Repeat(NowUIInput.GetId(id, "right"), frame.rightHeld))
                    NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.command);

                if (frame.homePressed)
                    NowTextEdit.MoveHome(ref state, frame.shift);

                if (frame.endPressed)
                    NowTextEdit.MoveEnd(ref state, text, frame.shift);

                SyncTouchKeyboard(id, ref text, ref state);
            }
            else if (s_touchKeyboardId == id)
            {
                CloseTouchKeyboard();
            }

            // Keep the caret visible inside the inner rect.
            float caretX = PrefixAdvance(fontAsset, resolvedFont, text, state.caret, fontSize);
            float totalWidth = PrefixAdvance(fontAsset, resolvedFont, text, text.Length, fontSize);

            if (caretX - state.scrollX > inner.width)
                state.scrollX = caretX - inner.width;

            if (caretX < state.scrollX)
                state.scrollX = caretX;

            state.scrollX = Mathf.Clamp(state.scrollX, 0f, Mathf.Max(0f, totalWidth - inner.width));

            // Visuals.
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

                if (focused && state.hasSelection)
                {
                    float selectionMin = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMin, fontSize);
                    float selectionMax = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMax, fontSize);
                    Color selectionColor = theme.GetColor(NowColorToken.Accent, Color.blue);
                    selectionColor.a = 0.35f;

                    Now.Rectangle(new NowRect(textX + selectionMin, inner.y, selectionMax - selectionMin, inner.height))
                        .SetColor(selectionColor)
                        .Draw();
                }

                if (text.Length > 0)
                {
                    textStyle.rect = new NowRect(textX, inner.y, totalWidth + 4f, inner.height);
                    textStyle.Draw(text);
                }
                else if (!focused && !string.IsNullOrEmpty(_placeholder))
                {
                    var placeholder = theme.Text(new NowRect(inner.x, inner.y, inner.width, inner.height), NowTextStyle.Muted);
                    placeholder.SetFontSize(fontSize).Draw(_placeholder);
                }

                if (focused && NowUIControlState.Blink())
                {
                    Now.Rectangle(new NowRect(textX + caretX, inner.y + 1f, 1.5f, inner.height - 2f))
                        .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }
            }

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

        // ------------------------------------------------------------------
        // Text metrics: shaped clusters when available, codepoints otherwise.
        // ------------------------------------------------------------------

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
