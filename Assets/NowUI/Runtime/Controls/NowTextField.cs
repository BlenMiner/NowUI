using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Single-line text field. <see cref="Draw(ref string)"/> edits the caller's
    /// string in place and returns true when it changed:
    /// <code>NowLayout.TextField().SetPlaceholder("Name...").Draw(ref playerName);</code>
    /// Click to place the caret (shaped-text cluster aware), drag to select,
    /// standard keyboard editing with key repeat, copy/cut/paste/select-all,
    /// double-click selects a word, triple-click selects the line. IME composition renders inline at the caret
    /// (underlined) and owns the editing keys until committed. Mobile opens
    /// the on-screen keyboard while focused.
    /// </summary>
    [NowBuilder]
    public struct NowTextField
    {
        NowId _id;
        readonly int _site;
        string _placeholder;
        NowFocusNavigation _navigation;
        NowLayoutOptions _options;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowTextStyle _textPreset;
        bool _hasNumberRange;
        float _numberMin;
        float _numberMax;
        string _numberFormat;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        struct NumberEditState
        {
            public string text;

            public bool editing;
        }

        internal NowTextField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _placeholder = null;
            _navigation = default;
            _options = default;
            _rect = default;
            _hasRect = false;
            _textPreset = NowTextStyle.Body;
            _hasNumberRange = false;
            _numberMin = 0f;
            _numberMax = 0f;
            _numberFormat = null;
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

        /// <summary>Clamp numeric Draw overloads to an inclusive range.</summary>
        public NowTextField SetRange(float min, float max)
        {
            if (max < min)
                (min, max) = (max, min);

            _hasNumberRange = true;
            _numberMin = min;
            _numberMax = max;
            return this;
        }

        /// <summary>Clamp integer Draw overloads to an inclusive range.</summary>
        public NowTextField SetRange(int min, int max)
        {
            return SetRange((float)min, max);
        }

        /// <summary>Format used when numeric Draw overloads sync the field from the value.</summary>
        public NowTextField SetFormat(string format) { _numberFormat = string.IsNullOrEmpty(format) ? null : format; return this; }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTextField SetId(NowId id) { _id = id; return this; }

        /// <summary>Explicit directional/Tab focus targets for this control.</summary>
        public NowTextField SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>
        /// Numeric text field helper. The caller owns the float; the control keeps
        /// an edit buffer only while focused so partial text can be typed naturally.
        /// </summary>
        public bool Draw(ref float value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawFloat(ref value, id, _numberFormat ?? "G7");
        }

        /// <summary>Numeric text field helper with a one-off format override.</summary>
        public bool Draw(ref float value, string format)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawFloat(ref value, id, string.IsNullOrEmpty(format) ? "G7" : format);
        }

        /// <summary>
        /// Integer text field helper. Invalid partial text is kept while focused
        /// and discarded on blur.
        /// </summary>
        public bool Draw(ref int value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawInt(ref value, id);
        }

        public bool Draw(ref string text)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawText(ref text, id);
        }

        bool DrawFloat(ref float value, int id, string format)
        {
            float previous = value;

            if (_hasNumberRange)
                value = Mathf.Clamp(value, _numberMin, _numberMax);

            ref var numberState = ref NowControlState.Get<NumberEditState>(id, "number");

            if (!numberState.editing || numberState.text == null)
                numberState.text = FormatFloat(value, format);

            string text = numberState.text;
            DrawText(ref text, id);

            bool focused = NowFocus.IsFocused(id);

            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                if (_hasNumberRange)
                    parsed = Mathf.Clamp(parsed, _numberMin, _numberMax);

                value = parsed;
                numberState.text = focused ? text : FormatFloat(value, format);
            }
            else
            {
                numberState.text = focused ? text : FormatFloat(value, format);
            }

            numberState.editing = focused;
            return !Mathf.Approximately(previous, value);
        }

        bool DrawInt(ref int value, int id)
        {
            int previous = value;

            if (_hasNumberRange)
                value = Mathf.Clamp(value, Mathf.CeilToInt(_numberMin), Mathf.FloorToInt(_numberMax));

            ref var numberState = ref NowControlState.Get<NumberEditState>(id, "number");

            if (!numberState.editing || numberState.text == null)
                numberState.text = value.ToString(CultureInfo.InvariantCulture);

            string text = numberState.text;
            DrawText(ref text, id);

            bool focused = NowFocus.IsFocused(id);

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                if (_hasNumberRange)
                    parsed = Mathf.Clamp(parsed, Mathf.CeilToInt(_numberMin), Mathf.FloorToInt(_numberMax));

                value = parsed;
                numberState.text = focused ? text : value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                numberState.text = focused ? text : value.ToString(CultureInfo.InvariantCulture);
            }

            numberState.editing = focused;
            return previous != value;
        }

        static string FormatFloat(float value, string format)
        {
            return value.ToString(string.IsNullOrEmpty(format) ? "G7" : format, CultureInfo.InvariantCulture);
        }

        bool DrawText(ref string text, int id)
        {
            text ??= string.Empty;
            string original = text;

            var theme = NowTheme.themeAsset;
            var renderer = theme.controlRenderer;

            var textStyle = NowControls.Text(theme, _textPreset);
            var fontAsset = textStyle.font;
            float fontSize = textStyle.fontSize;
            float lineHeight = fontAsset != null ? fontAsset.GetLineHeight() * fontSize : fontSize * 1.2f;

            NowRect rect = NowControls.ReserveRect(_hasRect, _rect, _options, renderer.MeasureTextField(theme, lineHeight));
            var inner = renderer.TextFieldInnerRect(theme, rect, lineHeight);

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var gesture = ref NowControlState.Get<NowTextSelectionGesture>(id, "selection-gesture");

            // Focus gained without a click (tab/gamepad/programmatic): caret to end.
            ref byte hadFocus = ref NowControlState.Get<byte>(id, "hadfocus");

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
                int hit = HitTest(fontAsset, resolvedFont, text, interaction.pointerPosition.x - inner.x + state.scrollX, fontSize);
                NowTextEdit.DragSelectionGesture(ref state, text, in gesture, hit);
                NowControlState.RequestRepaint();
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

                    if (NowControlState.Repeat(id, "bs", frame.backspaceHeld))
                        NowTextEdit.Backspace(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(id, "del", frame.deleteHeld))
                        NowTextEdit.Delete(ref text, ref state, frame.command);

                    if (NowControlState.Repeat(id, "left", frame.leftHeld))
                        NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                    if (NowControlState.Repeat(id, "right", frame.rightHeld))
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

            ref float blinkAnchor = ref NowControlState.Get<float>(id, "blink");
            ref int lastCaret = ref NowControlState.Get<int>(id, "lastcaret");

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

            renderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused));

            using (Now.Mask(inner))
            {
                float textX = inner.x - state.scrollX;

                if (focused && state.hasSelection && composition == null)
                {
                    float selectionMin = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMin, fontSize);
                    float selectionMax = PrefixAdvance(fontAsset, resolvedFont, text, state.selectionMax, fontSize);
                    renderer.DrawSelection(theme, new NowRect(textX + selectionMin, inner.y, selectionMax - selectionMin, inner.height));
                }

                if (display.Length > 0)
                {
                    textStyle.rect = new NowRect(textX, inner.y, totalWidth + 4f, inner.height);
                    textStyle.Draw(display);
                }
                else if (!focused && !string.IsNullOrEmpty(_placeholder))
                {
                    var placeholder = NowControls.Text(theme, NowTextStyle.Muted);
                    placeholder.rect = new NowRect(inner.x, inner.y, inner.width, inner.height);
                    placeholder.SetFontSize(fontSize).Draw(_placeholder);
                }

                if (composition != null)
                {
                    float compositionX = PrefixAdvance(fontAsset, resolvedFont, display, state.caret, fontSize);

                    float underlineHeight = theme.controlStyles.compositionUnderlineHeight;
                    renderer.DrawCompositionUnderline(theme, new NowRect(
                        textX + compositionX,
                        inner.yMax - underlineHeight,
                        Mathf.Max(caretX - compositionX, 1f),
                        underlineHeight));
                }

                if (focused && NowControlState.Blink(1f, blinkAnchor))
                {
                    renderer.DrawCaret(theme, new NowRect(textX + caretX, inner.y, theme.controlStyles.caretWidth, inner.height));
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
