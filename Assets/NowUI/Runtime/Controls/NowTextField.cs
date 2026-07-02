using System.Globalization;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Single-line text field. <see cref="Draw(ref string)"/> edits the caller's
    /// string in place and returns true when it changed:
    /// <code>NowLayout.TextField().SetPlaceholder("Name...").Draw(ref playerName);</code>
    /// Click to place the caret (shaped-text cluster aware), drag to select,
    /// shift-click extends the selection, standard keyboard editing with key
    /// repeat, copy/cut/paste/select-all, undo/redo, double-click selects a
    /// word, triple-click selects the line. Word and line caret movement
    /// follow the platform convention (Option/Command on macOS, Ctrl
    /// elsewhere). Enter commits and blurs; Escape reverts to the text the
    /// field had when it gained focus, then blurs. IME composition renders
    /// inline at the caret (underlined) and owns the editing keys until
    /// committed. Mobile opens the on-screen keyboard while focused.
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
        bool _spinner;
        float _spinnerStep;
        int _spinnerTicks;

        const int SpinnerUpSeed = 0x4e545355;
        const int SpinnerDownSeed = 0x4e545344;

        static TouchScreenKeyboard s_touchKeyboard;
        static int s_touchKeyboardId;

        struct NumberEditState
        {
            public string text;

            public bool editing;

            public double lastValue;

            public long lastLong;

            public string lastFormat;

            public string formatted;

            public double revert;

            public long revertLong;
        }

        struct RevertState
        {
            public string text;
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
            _spinner = false;
            _spinnerStep = 1f;
            _spinnerTicks = 0;
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

        /// <summary>
        /// Adds increment/decrement buttons to the numeric Draw overloads:
        /// click steps once, press-and-hold repeats, and up/down navigation steps
        /// while the field is focused.
        /// </summary>
        public NowTextField SetSpinner(float step = 1f)
        {
            _spinner = true;
            _spinnerStep = Mathf.Max(0.0001f, step);
            return this;
        }

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

        /// <summary>
        /// Double-precision numeric text field helper, mirroring
        /// <see cref="Draw(ref float)"/> with a wider default format.
        /// </summary>
        public bool Draw(ref double value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawDouble(ref value, id, _numberFormat ?? "G15");
        }

        /// <summary>
        /// Long integer text field helper, mirroring <see cref="Draw(ref int)"/>.
        /// </summary>
        public bool Draw(ref long value)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawLong(ref value, id);
        }

        public bool Draw(ref string text)
        {
            int id = NowControls.GetControlId(_id, _site);
            return DrawText(ref text, id, out _);
        }

        bool DrawFloat(ref float value, int id, string format)
        {
            float previous = value;

            if (_hasNumberRange)
                value = Mathf.Clamp(value, _numberMin, _numberMax);

            ref var numberState = ref NowControlState.Get<NumberEditState>(id, "number");

            if (!numberState.editing || numberState.text == null)
                numberState.text = FormatFloat(ref numberState, value, format);

            if (!numberState.editing)
                numberState.revert = value;

            string text = numberState.text;
            DrawText(ref text, id, out bool reverted);

            bool focused = NowFocus.IsFocused(id);

            if (reverted)
            {
                value = (float)numberState.revert;
                numberState.text = FormatFloat(ref numberState, value, format);
                numberState.editing = focused;
                return false;
            }

            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                if (_hasNumberRange)
                    parsed = Mathf.Clamp(parsed, _numberMin, _numberMax);

                value = parsed;
            }

            numberState.text = focused ? text : FormatFloat(ref numberState, value, format);

            if (_spinner && _spinnerTicks != 0)
            {
                value += _spinnerTicks * _spinnerStep;

                if (_hasNumberRange)
                    value = Mathf.Clamp(value, _numberMin, _numberMax);

                numberState.text = FormatFloat(ref numberState, value, format);
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
                numberState.text = FormatLong(ref numberState, value);

            if (!numberState.editing)
                numberState.revertLong = value;

            string text = numberState.text;
            DrawText(ref text, id, out bool reverted);

            bool focused = NowFocus.IsFocused(id);

            if (reverted)
            {
                value = (int)numberState.revertLong;
                numberState.text = FormatLong(ref numberState, value);
                numberState.editing = focused;
                return false;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                if (_hasNumberRange)
                    parsed = Mathf.Clamp(parsed, Mathf.CeilToInt(_numberMin), Mathf.FloorToInt(_numberMax));

                value = parsed;
            }

            numberState.text = focused ? text : FormatLong(ref numberState, value);

            if (_spinner && _spinnerTicks != 0)
            {
                value += _spinnerTicks * Mathf.Max(1, Mathf.RoundToInt(_spinnerStep));

                if (_hasNumberRange)
                    value = Mathf.Clamp(value, Mathf.CeilToInt(_numberMin), Mathf.FloorToInt(_numberMax));

                numberState.text = FormatLong(ref numberState, value);
            }

            numberState.editing = focused;
            return previous != value;
        }

        bool DrawDouble(ref double value, int id, string format)
        {
            double previous = value;

            if (_hasNumberRange)
                value = ClampDouble(value, _numberMin, _numberMax);

            ref var numberState = ref NowControlState.Get<NumberEditState>(id, "number");

            if (!numberState.editing || numberState.text == null)
                numberState.text = FormatDouble(ref numberState, value, format);

            if (!numberState.editing)
                numberState.revert = value;

            string text = numberState.text;
            DrawText(ref text, id, out bool reverted);

            bool focused = NowFocus.IsFocused(id);

            if (reverted)
            {
                value = numberState.revert;
                numberState.text = FormatDouble(ref numberState, value, format);
                numberState.editing = focused;
                return false;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                if (_hasNumberRange)
                    parsed = ClampDouble(parsed, _numberMin, _numberMax);

                value = parsed;
            }

            numberState.text = focused ? text : FormatDouble(ref numberState, value, format);

            if (_spinner && _spinnerTicks != 0)
            {
                value += _spinnerTicks * (double)_spinnerStep;

                if (_hasNumberRange)
                    value = ClampDouble(value, _numberMin, _numberMax);

                numberState.text = FormatDouble(ref numberState, value, format);
            }

            numberState.editing = focused;
            return previous != value;
        }

        bool DrawLong(ref long value, int id)
        {
            long previous = value;

            if (_hasNumberRange)
                value = ClampLong(value, _numberMin, _numberMax);

            ref var numberState = ref NowControlState.Get<NumberEditState>(id, "number");

            if (!numberState.editing || numberState.text == null)
                numberState.text = FormatLong(ref numberState, value);

            if (!numberState.editing)
                numberState.revertLong = value;

            string text = numberState.text;
            DrawText(ref text, id, out bool reverted);

            bool focused = NowFocus.IsFocused(id);

            if (reverted)
            {
                value = numberState.revertLong;
                numberState.text = FormatLong(ref numberState, value);
                numberState.editing = focused;
                return false;
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                if (_hasNumberRange)
                    parsed = ClampLong(parsed, _numberMin, _numberMax);

                value = parsed;
            }

            numberState.text = focused ? text : FormatLong(ref numberState, value);

            if (_spinner && _spinnerTicks != 0)
            {
                value += _spinnerTicks * (long)Mathf.Max(1, Mathf.RoundToInt(_spinnerStep));

                if (_hasNumberRange)
                    value = ClampLong(value, _numberMin, _numberMax);

                numberState.text = FormatLong(ref numberState, value);
            }

            numberState.editing = focused;
            return previous != value;
        }

        static double ClampDouble(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        static long ClampLong(long value, float min, float max)
        {
            double low = System.Math.Ceiling((double)min);
            double high = System.Math.Floor((double)max);

            if (value < low)
                return low <= long.MinValue ? long.MinValue : (long)low;

            if (value > high)
                return high >= long.MaxValue ? long.MaxValue : (long)high;

            return value;
        }

        static string FormatFloat(float value, string format)
        {
            return value.ToString(string.IsNullOrEmpty(format) ? "G7" : format, CultureInfo.InvariantCulture);
        }

        static string FormatDouble(double value, string format)
        {
            return value.ToString(string.IsNullOrEmpty(format) ? "G15" : format, CultureInfo.InvariantCulture);
        }

        /// <summary>Cached formatting so unfocused numeric fields never allocate while the value is stable.</summary>
        static string FormatFloat(ref NumberEditState state, float value, string format)
        {
            if (state.formatted == null || state.lastValue != value || state.lastFormat != format)
            {
                state.lastValue = value;
                state.lastFormat = format;
                state.formatted = FormatFloat(value, format);
            }

            return state.formatted;
        }

        /// <summary>Cached formatting so unfocused numeric fields never allocate while the value is stable.</summary>
        static string FormatDouble(ref NumberEditState state, double value, string format)
        {
            if (state.formatted == null || state.lastValue != value || state.lastFormat != format)
            {
                state.lastValue = value;
                state.lastFormat = format;
                state.formatted = FormatDouble(value, format);
            }

            return state.formatted;
        }

        /// <summary>Cached formatting so unfocused integer fields never allocate while the value is stable.</summary>
        static string FormatLong(ref NumberEditState state, long value)
        {
            if (state.formatted == null || state.lastLong != value)
            {
                state.lastLong = value;
                state.formatted = value.ToString(CultureInfo.InvariantCulture);
            }

            return state.formatted;
        }

        bool DrawText(ref string text, int id, out bool reverted)
        {
            reverted = false;
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

            NowInteraction spinnerUp = default;
            NowInteraction spinnerDown = default;
            NowRect spinnerUpRect = default;
            NowRect spinnerDownRect = default;

            if (_spinner)
            {
                inner = new NowRect(inner.x, inner.y, Mathf.Max(0f, inner.width - theme.controlStyles.spinnerButtonWidth), inner.height);

                float buttonWidth = theme.controlStyles.spinnerButtonWidth;
                float half = rect.height * 0.5f;
                spinnerUpRect = new NowRect(rect.xMax - buttonWidth - 1f, rect.y + 1f, buttonWidth, half - 1f);
                spinnerDownRect = new NowRect(rect.xMax - buttonWidth - 1f, rect.y + half, buttonWidth, half - 1f);
                spinnerUp = NowInput.Interact(NowInput.CombineId(id, SpinnerUpSeed), spinnerUpRect);
                spinnerDown = NowInput.Interact(NowInput.CombineId(id, SpinnerDownSeed), spinnerDownRect);
            }

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var gesture = ref NowControlState.Get<NowTextSelectionGesture>(id, "selection-gesture");

            // Focus gained without a click (tab/gamepad/programmatic): caret to end.
            ref byte hadFocus = ref NowControlState.Get<byte>(id, "hadfocus");
            ref var revert = ref NowControlState.Get<RevertState>(id, "revert");

            if (focused && hadFocus == 0)
            {
                NowTextInput.DiscardPending();
                NowTextInput.setImeEnabled?.Invoke(true);
                revert.text = text;

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
                int hit = HitTest(fontAsset, resolvedFont, text, interaction.pointerPosition.x - inner.x + state.scrollX, fontSize);
                NowTextEdit.DragSelectionGesture(ref state, text, in gesture, hit);
                NowControlState.RequestRepaint();
            }

            string composition = null;

            if (focused && !NowInput.isPassive)
            {
                NowFocus.LockNavigation();
                var frame = NowTextInput.current;
                var undo = NowTextUndoRegistry.Get(id);
                composition = string.IsNullOrEmpty(frame.composition) ? null : frame.composition;

                if (!string.IsNullOrEmpty(frame.characters))
                {
                    undo.Push(text, in state, typing: true);
                    NowTextEdit.Insert(ref text, ref state, frame.characters);
                }

                // While composing the IME owns the editing keys.
                if (composition == null)
                {
                    if (frame.enterPressed)
                        NowFocus.Clear();

                    if (frame.escapePressed)
                    {
                        text = revert.text ?? original;
                        NowTextEdit.Clamp(ref state, text);
                        reverted = true;
                        NowFocus.Clear();
                    }

                    if (frame.undoPressed)
                        undo.Undo(ref text, ref state);

                    if (frame.redoPressed)
                        undo.Redo(ref text, ref state);

                    if (frame.selectAllPressed)
                        NowTextEdit.SelectAll(ref state, text);

                    if (frame.copyPressed && state.hasSelection)
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));

                    if (frame.cutPressed && state.hasSelection)
                    {
                        undo.Push(text, in state, typing: false);
                        NowClipboard.Copy(NowTextEdit.GetSelection(text, state));
                        NowTextEdit.DeleteSelection(ref text, ref state);
                    }

                    if (frame.pastePressed)
                    {
                        string buffer = NowClipboard.Paste();

                        if (!string.IsNullOrEmpty(buffer))
                        {
                            undo.Push(text, in state, typing: false);
                            NowTextEdit.Insert(ref text, ref state, buffer.Replace("\n", " ").Replace("\r", string.Empty));
                        }
                    }

                    if (NowControlState.Repeat(id, "bs", frame.backspaceHeld))
                    {
                        undo.Push(text, in state, typing: true);

                        if (frame.lineModifier)
                            NowTextEdit.DeleteToLineStart(ref text, ref state);
                        else
                            NowTextEdit.Backspace(ref text, ref state, frame.wordModifier);
                    }

                    if (NowControlState.Repeat(id, "del", frame.deleteHeld))
                    {
                        undo.Push(text, in state, typing: true);
                        NowTextEdit.Delete(ref text, ref state, frame.wordModifier);
                    }

                    if (NowControlState.Repeat(id, "left", frame.leftHeld))
                    {
                        if (frame.lineModifier)
                            NowTextEdit.MoveHome(ref state, frame.shift);
                        else
                            NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.wordModifier);
                    }

                    if (NowControlState.Repeat(id, "right", frame.rightHeld))
                    {
                        if (frame.lineModifier)
                            NowTextEdit.MoveEnd(ref state, text, frame.shift);
                        else
                            NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.wordModifier);
                    }

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
                else if (!string.IsNullOrEmpty(_placeholder))
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

            if (_spinner)
            {
                int ticks = 0;

                if (NowControlState.Repeat(id, "spin-up", spinnerUp.held, 0.35f, 0.06f))
                    ++ticks;

                if (NowControlState.Repeat(id, "spin-down", spinnerDown.held, 0.35f, 0.06f))
                    --ticks;

                if (focused && !NowInput.isPassive)
                {
                    float navY = NowInput.current.navigation.y;

                    if (NowControlState.Repeat(id, "spin-nav", Mathf.Abs(navY) > 0.55f, 0.35f, 0.08f))
                        ticks += navY > 0f ? 1 : -1;
                }

                _spinnerTicks = ticks;
                renderer.DrawSpinnerButtons(new NowSpinnerRenderContext(
                    theme, rect, spinnerUpRect, spinnerDownRect,
                    spinnerUp.hovered, spinnerUp.held, spinnerDown.hovered, spinnerDown.held, focused));
            }

            if (focused && !NowInput.isPassive)
                NowControlState.RequestRepaint();

            return !reverted && text != original;
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
