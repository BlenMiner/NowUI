using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.CodeEditor
{
    public struct NowCodeEditorResult
    {
        public bool changed;

        /// <summary>True when the language's validator reported no diagnostics.</summary>
        public bool isValid;

        public int diagnosticCount;
    }

    /// <summary>Factories for <see cref="NowCodeEditor"/>, mirroring the core control factories.</summary>
    public static class NowCode
    {
        public static NowCodeEditor Editor(NowCodeLanguage language, NowId id = default,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCodeEditor(language, id, NowControls.SiteId(file, line));
        }

        public static NowCodeEditor Editor(NowRect rect, NowCodeLanguage language, NowId id = default,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCodeEditor(rect, language, id, NowControls.SiteId(file, line));
        }
    }

    /// <summary>
    /// A code editor: syntax highlighting through a <see cref="NowCodeLanguage"/>
    /// profile, validation squiggles with hover tooltips and a status bar
    /// (click the error to jump to it), bracket/quote auto-close, language
    /// completions, skip-over and selection wrapping, Enter auto-indent, Tab
    /// indent/dedent (multi-line with a selection), smart Home, undo/redo
    /// (Ctrl+Z / Ctrl+Y), line numbers, a current-line highlight, two-axis
    /// scrolling with caret-into-view, double/triple-click selection, IME
    /// composition, and the standard clipboard/focus integration:
    /// <code>NowCode.Editor(NowJsonLanguage.instance).SetHeight(280).Draw(ref json);</code>
    /// </summary>
    [NowBuilder]
    public struct NowCodeEditor
    {
        const float Padding = 6f;

        const float StatusHeight = 22f;

        const float ScrollbarThickness = 8f;

        const string IndentUnit = "    ";

        const int MaxVisibleCompletions = 8;

        const float CompletionPadding = 4f;

        /// <summary>Payload of the last no-selection line copy/cut, so paste can re-insert it as a whole line.</summary>
        static string s_lineClipboard;

        NowId _id;
        readonly int _site;
        readonly NowCodeLanguage _language;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowFocusNavigation _navigation;
        float _height;
        float _width;
        float _fontSize;
        bool _hideLineNumbers;
        bool _hideStatusBar;

        internal NowCodeEditor(NowCodeLanguage language, NowId id, int site)
        {
            _language = language;
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _navigation = default;
            _height = 240f;
            _width = 0f;
            _fontSize = 14f;
            _hideLineNumbers = false;
            _hideStatusBar = false;
        }

        internal NowCodeEditor(NowRect rect, NowCodeLanguage language, NowId id, int site) : this(language, id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowCodeEditor SetHeight(float height) { _height = height; return this; }

        public NowCodeEditor SetWidth(float width) { _width = width; return this; }

        public NowCodeEditor SetFontSize(float fontSize) { _fontSize = fontSize; return this; }

        public NowCodeEditor SetLineNumbers(bool show) { _hideLineNumbers = !show; return this; }

        public NowCodeEditor SetStatusBar(bool show) { _hideStatusBar = !show; return this; }

        public NowCodeEditor SetId(NowId id) { _id = id; return this; }

        public NowCodeEditor SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        sealed class EditorCache
        {
            public string text;
            public NowCodeLanguage language;
            public readonly List<NowTextLine> lines = new List<NowTextLine>(64);
            public readonly List<int> lineStates = new List<int>(64);
            public readonly List<float> lineWidths = new List<float>(64);
            public readonly List<NowCodeDiagnostic> diagnostics = new List<NowCodeDiagnostic>(4);
            public float contentWidth;
            public NowFontAsset measureFont;
            public float measureFontSize;
            public NowFontStyle measureFontStyle;
            public readonly NowTextUndoStack undo = new NowTextUndoStack();
            public string statusMessage;
            public string positionText;
            public int positionLine = -1;
            public int positionColumn = -1;
            public string tooltipMessage;
            public NowRect tooltipRect;
            public int validatedVersion;
            public readonly List<NowCodeCompletionItem> completionItems = new List<NowCodeCompletionItem>(64);
            public readonly List<int> completionVisible = new List<int>(64);
            public int completionReplaceStart = -1;
            public int completionSelected;
            public int completionWindow;
            public NowRect completionPopupRect;
            public float completionRowHeight;
        }

        struct EditorState
        {
            public float scrollX;
            public float scrollY;
            public float goalX;
            public int lastCaret;
            public float blinkAnchor;
            public byte hadFocus;
        }

        static readonly Dictionary<int, EditorCache> _caches = new Dictionary<int, EditorCache>(8);

        static readonly List<NowCodeToken> _tokenScratch = new List<NowCodeToken>(32);

        static readonly List<string> _numberStrings = new List<string>(128);

        static readonly System.Text.StringBuilder _blockEditScratch = new System.Text.StringBuilder(256);

        static readonly List<int> _oldStateScratch = new List<int>(64);

        static readonly List<int> _oldStartScratch = new List<int>(64);

        static readonly List<float> _oldWidthScratch = new List<float>(64);

        /// <summary>Clears retained caches (undo stacks, line tables); used by tests and domain reloads.</summary>
        public static void ResetCaches()
        {
            _caches.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            ResetCaches();
        }

        public NowCodeEditorResult Draw(ref string text)
        {
            text ??= string.Empty;
            string original = text;
            var result = default(NowCodeEditorResult);

            if (_language == null)
                return result;

            var theme = NowTheme.themeAsset;
            int id = NowControls.GetControlId(_id, _site);

            var textStyle = theme.Text(default, NowTextStyle.Body).SetFontSize(_fontSize);
            var font = textStyle.font;

            if (font == null)
                return result;

            float lineHeight = font.GetLineHeight(textStyle.fontStyle) * _fontSize;
            float statusHeight = _hideStatusBar ? 0f : StatusHeight;

            NowRect rect = _hasRect
                ? _rect
                : NowLayout.Rect(width: _width, height: _height, stretchWidth: _width <= 0f);

            ref var state = ref NowControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var editor = ref NowControlState.Get<EditorState>(NowInput.GetId(id, "editor"));
            ref var gesture = ref NowControlState.Get<NowTextSelectionGesture>(NowInput.GetId(id, "selection-gesture"));

            var cache = GetCache(id, _language);

            if (!ReferenceEquals(cache.text, text))
                Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);
            else if (cache.validatedVersion != _language.validationVersion)
                Revalidate(cache, text);

            // Async validators report pending work; keep repainting so their
            // results land without waiting for the next interaction.
            if (_language.validationPending && !NowInput.isPassive)
                NowControlState.RequestRepaint();

            float gutterWidth = _hideLineNumbers
                ? 0f
                : Advance(NumberString(cache.lines.Count), font, _fontSize, textStyle.fontStyle) + 18f;

            var textRect = new NowRect(
                rect.x + gutterWidth + Padding,
                rect.y + Padding,
                rect.width - gutterWidth - Padding * 2f - 4f,
                rect.height - Padding * 2f - statusHeight);

            // Scrollbars claim their press before the text body, so dragging a
            // thumb scrolls instead of moving the caret (first interaction wins).
            if (!NowInput.isPassive)
            {
                Scrollbars(cache, rect, textRect, statusHeight, lineHeight, editor,
                    out float maxX, out float maxY, out _, out var vThumb, out _, out var hThumb);

                if (maxY > 0f)
                {
                    var vDrag = NowInput.Interact(NowInput.GetId(id, "vscroll"), vThumb);

                    if (vDrag.pressed)
                        NowFocus.Focus(id);

                    if (vDrag.dragging)
                    {
                        editor.scrollY += vDrag.dragDelta.y / Mathf.Max(textRect.height - vThumb.height, 1f) * maxY;
                        NowControlState.RequestRepaint();
                    }
                }

                if (maxX > 0f)
                {
                    var hDrag = NowInput.Interact(NowInput.GetId(id, "hscroll"), hThumb);

                    if (hDrag.pressed)
                        NowFocus.Focus(id);

                    if (hDrag.dragging)
                    {
                        editor.scrollX += hDrag.dragDelta.x / Mathf.Max(textRect.width - hThumb.width, 1f) * maxX;
                        NowControlState.RequestRepaint();
                    }
                }
            }

            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out _);
            bool verticalMove = false;
            bool revealCaret = false;

            if (focused && editor.hadFocus == 0)
            {
                NowTextInput.DiscardPending();
                NowTextInput.setImeEnabled?.Invoke(true);

                if (!interaction.pressed)
                {
                    NowTextEdit.MoveEnd(ref state, text, false);
                    revealCaret = true;
                }
            }
            else if (!focused && editor.hadFocus == 1)
            {
                NowTextInput.setImeEnabled?.Invoke(false);
            }

            editor.hadFocus = focused ? (byte)1 : (byte)0;

            if (interaction.pressed && CompletionsOpen(cache) &&
                cache.completionPopupRect.Contains(interaction.pointerPosition))
            {
                int row = cache.completionWindow + (int)((interaction.pointerPosition.y -
                    cache.completionPopupRect.y - CompletionPadding) / Mathf.Max(cache.completionRowHeight, 1f));

                if (row >= 0 && row < cache.completionVisible.Count)
                {
                    revealCaret = true;
                    cache.undo.Push(text, in state, typing: false);
                    cache.completionSelected = row;
                    AcceptCompletion(cache, ref text, ref state);
                }
            }
            else if (interaction.pressed)
            {
                revealCaret = true;
                CloseCompletions(cache);
                NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Character, in state);

                bool onStatusBar = statusHeight > 0f && interaction.pointerPosition.y >= rect.yMax - statusHeight - 1f;
                bool onGutter = !onStatusBar && interaction.pointerPosition.x < textRect.x - 2f;

                if (onStatusBar)
                {
                    if (cache.diagnostics.Count > 0)
                    {
                        state.caret = Mathf.Clamp(cache.diagnostics[0].start, 0, text.Length);
                        state.anchor = state.caret;
                        NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Character, in state);
                    }
                }
                else
                {
                    int hit = HitTest(text, cache, font, _fontSize, textStyle.fontStyle,
                        interaction.pointerPosition, textRect, lineHeight, editor.scrollX, editor.scrollY);
                    int streak = NowControlState.ClickStreak(id, true, interaction.pointerPosition);

                    if (onGutter || streak >= 3)
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

                        // Shift+click extends the selection from the existing anchor.
                        if (!NowTextInput.current.shift)
                            state.anchor = hit;

                        NowTextEdit.BeginSelectionGesture(ref gesture, NowTextSelectionGranularity.Character, in state);
                    }
                }
            }
            else if (interaction.dragging)
            {
                revealCaret = true;
                int hit = HitTest(text, cache, font, _fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, textRect, lineHeight, editor.scrollX, editor.scrollY);
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
                {
                    revealCaret = true;
                    cache.undo.Push(text, in state, typing: true);

                    for (int i = 0; i < frame.characters.Length; ++i)
                    {
                        char c = frame.characters[i];
                        HandleCharacter(c, ref text, ref state, _language);

                        if (_language.IsIndentCloser(c))
                            ReindentCloser(c, ref text, ref state, _language, cache);

                        // Identifier characters open or narrow the completion
                        // popup; trigger characters ('.') query the language
                        // directly; anything else dismisses it.
                        if (_language.IsCompletionTrigger(c) || IsIdentifierChar(c))
                            OpenOrRefreshCompletions(cache, _language, text, state.caret);
                        else
                            CloseCompletions(cache);
                    }
                }

                if (composition != null)
                    revealCaret = true;

                // While composing the IME owns the editing keys.
                if (composition == null)
                {
                    if (frame.escapePressed)
                    {
                        if (CompletionsOpen(cache))
                            CloseCompletions(cache);
                        else
                            NowFocus.Clear();
                    }

                    if (frame.undoPressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        cache.undo.Undo(ref text, ref state);
                    }

                    if (frame.redoPressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        cache.undo.Redo(ref text, ref state);
                    }

                    if (frame.selectAllPressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        NowTextEdit.SelectAll(ref state, text);
                    }

                    // Copy/cut with no selection act on the whole line, like an IDE.
                    if (frame.copyPressed)
                    {
                        if (state.hasSelection)
                        {
                            s_lineClipboard = null;
                            NowClipboard.Copy(NowTextEdit.GetSelection(text, state));
                        }
                        else
                        {
                            s_lineClipboard = CurrentLine(text, state.caret);
                            NowClipboard.Copy(s_lineClipboard);
                        }
                    }

                    if (frame.cutPressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        cache.undo.Push(text, in state, typing: false);

                        if (state.hasSelection)
                        {
                            s_lineClipboard = null;
                            NowClipboard.Copy(NowTextEdit.GetSelection(text, state));
                            NowTextEdit.DeleteSelection(ref text, ref state);
                        }
                        else
                        {
                            CutLine(ref text, ref state);
                        }
                    }

                    if (frame.duplicatePressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        cache.undo.Push(text, in state, typing: false);
                        DuplicateLines(ref text, ref state);
                    }

                    if (frame.pastePressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        string buffer = NowClipboard.Paste();

                        if (!string.IsNullOrEmpty(buffer))
                        {
                            cache.undo.Push(text, in state, typing: false);
                            buffer = buffer.Replace("\r\n", "\n").Replace('\r', '\n');

                            // A whole-line copy pastes as a whole line above the
                            // caret's line (the IDE convention), instead of
                            // splicing mid-line at the caret.
                            if (!state.hasSelection && buffer == s_lineClipboard && buffer.EndsWith("\n"))
                            {
                                NowTextMetrics.LineBounds(text, state.caret, out int lineStart, out _);
                                text = text.Insert(lineStart, buffer);
                                state.caret += buffer.Length;
                                state.anchor = state.caret;
                            }
                            else
                            {
                                NowTextEdit.Insert(ref text, ref state, buffer);
                            }
                        }
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "enter"), frame.enterHeld))
                    {
                        revealCaret = true;

                        if (CompletionsOpen(cache))
                        {
                            cache.undo.Push(text, in state, typing: false);
                            AcceptCompletion(cache, ref text, ref state);
                        }
                        else
                        {
                            cache.undo.Push(text, in state, typing: true);
                            InsertNewlineWithIndent(ref text, ref state, _language);
                        }
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "tab"), frame.tabHeld))
                    {
                        revealCaret = true;

                        if (CompletionsOpen(cache))
                        {
                            cache.undo.Push(text, in state, typing: false);
                            AcceptCompletion(cache, ref text, ref state);
                        }
                        else
                        {
                            if (!ReferenceEquals(cache.text, text))
                                Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

                            cache.undo.Push(text, in state, typing: true);
                            HandleTab(ref text, ref state, cache, frame.shift);
                        }
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "bs"), frame.backspaceHeld))
                    {
                        revealCaret = true;
                        cache.undo.Push(text, in state, typing: true);

                        if (frame.lineModifier)
                        {
                            CloseCompletions(cache);
                            NowTextEdit.DeleteToLineStart(ref text, ref state);
                        }
                        else if (!PairBackspace(ref text, ref state, _language) &&
                            (frame.wordModifier || !IndentBackspace(ref text, ref state)))
                        {
                            NowTextEdit.Backspace(ref text, ref state, frame.wordModifier);
                        }

                        if (CompletionsOpen(cache))
                            RefreshCompletionFilter(cache, text, state.caret);
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "del"), frame.deleteHeld))
                    {
                        revealCaret = true;
                        CloseCompletions(cache);
                        cache.undo.Push(text, in state, typing: true);
                        NowTextEdit.Delete(ref text, ref state, frame.wordModifier);
                    }

                    if (!ReferenceEquals(cache.text, text))
                        Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

                    if (NowControlState.Repeat(NowInput.GetId(id, "left"), frame.leftHeld))
                    {
                        revealCaret = true;
                        CloseCompletions(cache);

                        if (frame.lineModifier)
                            SmartHome(text, cache, ref state, frame.shift);
                        else
                            NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.wordModifier);
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "right"), frame.rightHeld))
                    {
                        revealCaret = true;
                        CloseCompletions(cache);

                        if (frame.lineModifier)
                        {
                            var caretLineSpan = cache.lines[LineOf(cache, state.caret)];
                            state.caret = caretLineSpan.start + caretLineSpan.length;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                        else
                        {
                            NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.wordModifier);
                        }
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "up"), frame.upHeld))
                    {
                        if (CompletionsOpen(cache))
                        {
                            cache.completionSelected = Mathf.Max(cache.completionSelected - 1, 0);
                            NowControlState.RequestRepaint();
                        }
                        else
                        {
                            revealCaret = true;

                            if (frame.lineModifier)
                            {
                                NowTextEdit.MoveHome(ref state, frame.shift);
                            }
                            else
                            {
                                MoveVertical(ref state, ref editor, text, cache, font, _fontSize, textStyle.fontStyle, -1, frame.shift);
                                verticalMove = true;
                            }
                        }
                    }

                    if (NowControlState.Repeat(NowInput.GetId(id, "down"), frame.downHeld))
                    {
                        if (CompletionsOpen(cache))
                        {
                            cache.completionSelected = Mathf.Min(cache.completionSelected + 1, cache.completionVisible.Count - 1);
                            NowControlState.RequestRepaint();
                        }
                        else
                        {
                            revealCaret = true;

                            if (frame.lineModifier)
                            {
                                NowTextEdit.MoveEnd(ref state, text, frame.shift);
                            }
                            else
                            {
                                MoveVertical(ref state, ref editor, text, cache, font, _fontSize, textStyle.fontStyle, 1, frame.shift);
                                verticalMove = true;
                            }
                        }
                    }

                    if (frame.homePressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);

                        if (frame.command)
                        {
                            NowTextEdit.MoveHome(ref state, frame.shift);
                        }
                        else
                        {
                            SmartHome(text, cache, ref state, frame.shift);
                        }
                    }

                    if (frame.endPressed)
                    {
                        revealCaret = true;
                        CloseCompletions(cache);

                        if (frame.command)
                        {
                            NowTextEdit.MoveEnd(ref state, text, frame.shift);
                        }
                        else
                        {
                            var line = cache.lines[LineOf(cache, state.caret)];
                            state.caret = line.start + line.length;

                            if (!frame.shift)
                                state.anchor = state.caret;
                        }
                    }
                }
            }

            if (!ReferenceEquals(cache.text, text))
                Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

            result.changed = text != original;
            result.diagnosticCount = cache.diagnostics.Count;
            result.isValid = cache.diagnostics.Count == 0;

            int caretLine = LineOf(cache, state.caret);
            var caretSpan = cache.lines[caretLine];
            float caretX = Advance(text, font, _fontSize, textStyle.fontStyle, caretSpan.start, state.caret - caretSpan.start);

            if (composition != null)
                caretX += Advance(composition, font, _fontSize, textStyle.fontStyle);

            revealCaret |= state.caret != editor.lastCaret || result.changed;

            if (revealCaret)
            {
                editor.lastCaret = state.caret;
                editor.blinkAnchor = Time.realtimeSinceStartup;

                if (!verticalMove)
                    editor.goalX = caretX;
            }

            float contentHeight = cache.lines.Count * lineHeight;
            float maxScrollY = Mathf.Max(0f, contentHeight - textRect.height);
            float maxScrollX = Mathf.Max(0f, cache.contentWidth + 24f - textRect.width);

            editor.scrollY = Mathf.Clamp(editor.scrollY, 0f, maxScrollY);
            editor.scrollX = Mathf.Clamp(editor.scrollX, 0f, maxScrollX);

            Vector2 pendingWheel = NowInput.current.scrollDelta;
            bool canWheelScroll = WouldWheelMove(editor.scrollX, editor.scrollY, maxScrollX, maxScrollY, pendingWheel, lineHeight);

            if (interaction.hovered && canWheelScroll)
            {
                Vector2 wheel = NowInput.ConsumeScrollDelta(rect);

                if (wheel != Vector2.zero)
                {
                    editor.scrollY -= wheel.y * lineHeight * 2f;
                    editor.scrollX += wheel.x * lineHeight * 2f;
                    NowControlState.RequestRepaint();
                }
            }

            if (focused && revealCaret)
            {
                float caretTop = caretLine * lineHeight;

                if (caretTop < editor.scrollY)
                    editor.scrollY = caretTop;

                if (caretTop + lineHeight > editor.scrollY + textRect.height)
                    editor.scrollY = caretTop + lineHeight - textRect.height;

                if (caretX < editor.scrollX)
                    editor.scrollX = Mathf.Max(0f, caretX - 24f);

                if (caretX > editor.scrollX + textRect.width - 8f)
                    editor.scrollX = caretX - textRect.width + 24f;
            }

            editor.scrollY = Mathf.Clamp(editor.scrollY, 0f, maxScrollY);
            editor.scrollX = Mathf.Clamp(editor.scrollX, 0f, maxScrollX);

            if (!focused)
                CloseCompletions(cache);
            else if (CompletionsOpen(cache))
                LayoutCompletionPopup(id, cache, text, font, _fontSize, textStyle.fontStyle, textRect, lineHeight, in editor, caretLine);

            if (focused && !NowInput.isPassive)
                NowTextInput.setCompositionCursor?.Invoke(new Vector2(
                    textRect.x + caretX - editor.scrollX,
                    textRect.y + caretLine * lineHeight - editor.scrollY + lineHeight));

            DrawVisuals(id, theme, textStyle, font, rect, textRect, gutterWidth, statusHeight, lineHeight,
                text, cache, in state, ref editor, focused, composition, caretLine, caretX,
                interaction.pointerPosition, interaction.hovered);

            if (focused)
                NowControlState.RequestRepaint();

            return result;
        }

        static bool WouldWheelMove(float scrollX, float scrollY, float maxScrollX, float maxScrollY, Vector2 wheel,
            float lineHeight)
        {
            if (wheel == Vector2.zero)
                return false;

            float nextY = Mathf.Clamp(scrollY - wheel.y * lineHeight * 2f, 0f, maxScrollY);
            float nextX = Mathf.Clamp(scrollX + wheel.x * lineHeight * 2f, 0f, maxScrollX);

            return !Mathf.Approximately(nextY, scrollY) || !Mathf.Approximately(nextX, scrollX);
        }

        void DrawVisuals(int id, NowThemeAsset themeAsset, NowText textStyle, NowFontAsset font, NowRect rect, NowRect textRect,
            float gutterWidth, float statusHeight, float lineHeight, string text, EditorCache cache,
            in NowTextEditState state, ref EditorState editor, bool focused, string composition,
            int caretLine, float caretX, Vector2 pointer, bool hovered)
        {
            float fontSize = _fontSize;
            var fontStyle = textStyle.fontStyle;

            Vector4 cornerRadius = themeAsset.Rectangle(rect, NowRectangleStyle.Outline).radius;

            // The code canvas sits on the Background token — the darkest surface
            // in IDE-style dark themes — while keeping the Surface preset's
            // outline so the editor still reads as a bounded panel.
            themeAsset.Rectangle(rect, NowRectangleStyle.Surface)
                .SetColor(themeAsset.GetColor(NowColorToken.Background, Color.white))
                .SetRadius(cornerRadius)
                .Draw();

            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(editor.scrollY / lineHeight));
            int lastVisible = Mathf.Min(cache.lines.Count - 1, Mathf.CeilToInt((editor.scrollY + textRect.height) / lineHeight));

            if (gutterWidth > 0f)
            {
                float gutterBottomLeft = statusHeight > 0f ? 0f : cornerRadius.w;
                Now.Rectangle(new NowRect(rect.x, rect.y, gutterWidth, rect.height - statusHeight))
                    .SetColor(themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f)))
                    .SetRadius(cornerRadius.z, 0f, 0f, gutterBottomLeft)
                    .Draw();

                using (Now.Mask(new NowRect(rect.x, textRect.y, gutterWidth, textRect.height)))
                {
                    var numberStyle = themeAsset.Text(default, NowTextStyle.Muted).SetFontSize(fontSize - 2f);

                    for (int i = firstVisible; i <= lastVisible; ++i)
                    {
                        string number = NumberString(i + 1);
                        float numberWidth = Advance(number, font, fontSize - 2f, fontStyle);
                        numberStyle.rect = new NowRect(
                            rect.x + gutterWidth - numberWidth - 8f,
                            textRect.y + i * lineHeight - editor.scrollY,
                            numberWidth + 4f,
                            lineHeight);
                        numberStyle.Draw(number);
                    }
                }
            }

            using (Now.Mask(textRect))
            {
                if (focused && caretLine >= firstVisible && caretLine <= lastVisible)
                {
                    Color lineTint = themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f));
                    lineTint.a *= 0.6f;
                    Now.Rectangle(new NowRect(textRect.x, textRect.y + caretLine * lineHeight - editor.scrollY, textRect.width, lineHeight))
                        .SetColor(lineTint)
                        .Draw();
                }

                if (focused && composition == null)
                    DrawOccurrenceHighlights(themeAsset, text, cache, font, fontSize, fontStyle, textRect, lineHeight, in state, ref editor, firstVisible, lastVisible);

                if (focused && state.hasSelection && composition == null)
                    DrawSelection(themeAsset, text, cache, font, fontSize, fontStyle, textRect, lineHeight, in state, ref editor, firstVisible, lastVisible);

                if (focused && !state.hasSelection && composition == null)
                    DrawBracketMatch(themeAsset, text, cache, font, fontSize, fontStyle, textRect, lineHeight, in state, ref editor, firstVisible, lastVisible);

                float originX = textRect.x - editor.scrollX;

                for (int i = firstVisible; i <= lastVisible; ++i)
                {
                    var line = cache.lines[i];
                    float y = textRect.y + i * lineHeight - editor.scrollY;

                    _tokenScratch.Clear();
                    _language.TokenizeLine(text, line.start, line.length, cache.lineStates[i], _tokenScratch);

                    float x = originX;
                    int cursor = line.start;
                    int lineEnd = line.start + line.length;

                    for (int t = 0; t <= _tokenScratch.Count; ++t)
                    {
                        int segmentStart, segmentEnd;
                        NowCodeTokenKind kind;

                        if (t < _tokenScratch.Count)
                        {
                            var token = _tokenScratch[t];
                            segmentStart = cursor;
                            segmentEnd = Mathf.Min(token.start, lineEnd);

                            if (segmentEnd > segmentStart)
                                x += DrawSegment(textStyle, themeAsset, text, segmentStart, segmentEnd - segmentStart,
                                    NowCodeTokenKind.Plain, font, fontSize, fontStyle, x, y, lineHeight);

                            segmentStart = token.start;
                            segmentEnd = Mathf.Min(token.start + token.length, lineEnd);
                            kind = token.kind;
                        }
                        else
                        {
                            segmentStart = cursor;
                            segmentEnd = lineEnd;
                            kind = NowCodeTokenKind.Plain;
                        }

                        if (segmentEnd > segmentStart)
                            x += DrawSegment(textStyle, themeAsset, text, segmentStart, segmentEnd - segmentStart,
                                kind, font, fontSize, fontStyle, x, y, lineHeight);

                        cursor = Mathf.Max(cursor, segmentEnd);
                    }

                    if (composition != null && i == caretLine)
                    {
                        float compositionStart = originX + Advance(text, font, fontSize, fontStyle,
                            line.start, Mathf.Clamp(state.caret - line.start, 0, line.length));
                        var compositionStyle = textStyle;
                        compositionStyle.rect = new NowRect(compositionStart, y, textRect.width, lineHeight);
                        compositionStyle.Draw(composition);

                        float compositionWidth = Advance(composition, font, fontSize, fontStyle);
                        Now.Rectangle(new NowRect(compositionStart, y + lineHeight - 1f, Mathf.Max(compositionWidth, 1f), 1f))
                            .SetColor(themeAsset.GetColor(NowColorToken.Text, Color.black))
                            .Draw();
                    }
                }

                DrawSquiggles(text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor, firstVisible, lastVisible);

                if (focused && NowControlState.Blink(1f, editor.blinkAnchor))
                {
                    Now.Rectangle(new NowRect(
                            originX + caretX,
                            textRect.y + caretLine * lineHeight - editor.scrollY,
                            2f,
                            lineHeight))
                        .SetColor(themeAsset.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }
            }

            if (statusHeight > 0f)
                DrawStatusBar(themeAsset, font, fontSize, fontStyle, rect, statusHeight, text, cache, in state, caretLine, cornerRadius);

            DrawScrollbars(themeAsset, cache, rect, textRect, statusHeight, lineHeight, ref editor, hovered || focused);

            // The border is drawn last with a transparent fill, so it covers
            // every seam between the gutter, body, status bar and scrollbars
            // with one clean rounded outline.
            var border = themeAsset.Rectangle(rect, NowRectangleStyle.Outline);
            border.color = new Vector4(0f, 0f, 0f, 0f);
            border = border.SetRadius(cornerRadius);
            border.outline = focused ? 2f : 1f;
            border.outlineColor = focused
                ? themeAsset.GetColor(NowColorToken.Accent, Color.blue)
                : themeAsset.GetColor(NowColorToken.Border, Color.gray);
            border.Draw();

            if (hovered)
                DrawDiagnosticTooltip(id, themeAsset, text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor, pointer);
        }

        static void Scrollbars(EditorCache cache, NowRect rect, NowRect textRect, float statusHeight, float lineHeight,
            in EditorState editor, out float maxScrollX, out float maxScrollY,
            out NowRect vTrack, out NowRect vThumb, out NowRect hTrack, out NowRect hThumb)
        {
            float contentHeight = cache.lines.Count * lineHeight;
            float contentWidth = cache.contentWidth + 24f;

            vTrack = new NowRect(rect.xMax - ScrollbarThickness - 3f, textRect.y, ScrollbarThickness, textRect.height);
            var vMetrics = NowScrollbar.Calculate(NowScrollbarAxis.Vertical, vTrack, textRect.height,
                contentHeight, editor.scrollY, 28f);
            maxScrollY = vMetrics.maxValue;
            vThumb = vMetrics.visible ? vMetrics.thumb : default;

            hTrack = new NowRect(textRect.x, rect.yMax - statusHeight - ScrollbarThickness - 3f, textRect.width, ScrollbarThickness);
            var hMetrics = NowScrollbar.Calculate(NowScrollbarAxis.Horizontal, hTrack, textRect.width,
                contentWidth, editor.scrollX, 28f);
            maxScrollX = hMetrics.maxValue;
            hThumb = hMetrics.visible ? hMetrics.thumb : default;
        }

        void DrawScrollbars(NowThemeAsset themeAsset, EditorCache cache, NowRect rect, NowRect textRect, float statusHeight,
            float lineHeight, ref EditorState editor, bool active)
        {
            Scrollbars(cache, rect, textRect, statusHeight, lineHeight, editor,
                out float maxScrollX, out float maxScrollY, out var vTrack, out var vThumb, out var hTrack, out var hThumb);

            Color trackColor = themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f));
            trackColor.a *= 0.5f;
            Color thumbColor = themeAsset.GetColor(NowColorToken.Border, Color.gray);
            thumbColor.a *= active ? 0.95f : 0.55f;

            if (maxScrollY > 0f)
            {
                Now.Rectangle(vTrack).SetColor(trackColor).SetRadius(ScrollbarThickness * 0.5f).Draw();
                Now.Rectangle(vThumb).SetColor(thumbColor).SetRadius(ScrollbarThickness * 0.5f).Draw();
            }

            if (maxScrollX > 0f)
            {
                Now.Rectangle(hTrack).SetColor(trackColor).SetRadius(ScrollbarThickness * 0.5f).Draw();
                Now.Rectangle(hThumb).SetColor(thumbColor).SetRadius(ScrollbarThickness * 0.5f).Draw();
            }
        }

        float DrawSegment(NowText textStyle, NowThemeAsset themeAsset, string text, int start, int length,
            NowCodeTokenKind kind, NowFontAsset font, float fontSize, NowFontStyle fontStyle,
            float x, float y, float lineHeight)
        {
            float width = Advance(text, font, fontSize, fontStyle, start, length);
            var style = textStyle;
            style.rect = new NowRect(x, y, width + 4f, lineHeight);
            style.color = KindColor(themeAsset, kind);
            style.Draw(System.MemoryExtensions.AsSpan(text, start, length));
            return width;
        }

        static Vector4 KindColor(NowThemeAsset themeAsset, NowCodeTokenKind kind)
        {
            // Fixed literal colors come in a light and a dark variant (the dark
            // ones matching JetBrains Rider's dark scheme: blue keywords, tan
            // types and strings, green calls, violet fields, teal numbers) so
            // syntax stays readable on both modes; everything else resolves
            // through theme tokens.
            bool dark = themeAsset.isDark;

            switch (kind)
            {
                case NowCodeTokenKind.Heading:
                case NowCodeTokenKind.Link:
                case NowCodeTokenKind.ListMarker:
                    return themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                case NowCodeTokenKind.Property:
                    return dark
                        ? new Vector4(0.757f, 0.569f, 1f, 1f)
                        : themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                case NowCodeTokenKind.Attribute:
                    return dark
                        ? new Vector4(0.224f, 0.80f, 0.56f, 1f)
                        : themeAsset.GetColor(NowColorToken.Accent, Color.blue);
                case NowCodeTokenKind.String:
                case NowCodeTokenKind.CodeSpan:
                    return dark
                        ? new Vector4(0.79f, 0.64f, 0.43f, 1f)
                        : new Vector4(0.16f, 0.52f, 0.26f, 1f);
                case NowCodeTokenKind.Number:
                    return dark
                        ? new Vector4(0.93f, 0.58f, 0.75f, 1f)
                        : new Vector4(0.55f, 0.27f, 0.68f, 1f);
                case NowCodeTokenKind.Emphasis:
                    return dark
                        ? new Vector4(0.71f, 0.58f, 0.94f, 1f)
                        : new Vector4(0.55f, 0.27f, 0.68f, 1f);
                case NowCodeTokenKind.Constant:
                    return dark
                        ? new Vector4(0.40f, 0.76f, 0.80f, 1f)
                        : new Vector4(0.05f, 0.45f, 0.50f, 1f);
                case NowCodeTokenKind.Keyword:
                case NowCodeTokenKind.Strong:
                case NowCodeTokenKind.Tag:
                    return dark
                        ? new Vector4(0.42f, 0.58f, 0.92f, 1f)
                        : new Vector4(0.80f, 0.42f, 0.13f, 1f);
                case NowCodeTokenKind.Comment:
                case NowCodeTokenKind.Quote:
                case NowCodeTokenKind.Fence:
                    return themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
                case NowCodeTokenKind.Punctuation:
                    // Rider renders operators and delimiters in the plain text
                    // color on dark schemes; light schemes keep them muted.
                    return dark
                        ? themeAsset.GetColor(NowColorToken.Text, Color.white)
                        : themeAsset.GetColor(NowColorToken.TextMuted, Color.gray);
                case NowCodeTokenKind.Error:
                    return new Vector4(0.86f, 0.24f, 0.24f, 1f);
                default:
                    return themeAsset.GetColor(NowColorToken.Text, Color.black);
            }
        }

        /// <summary>
        /// Highlights every visible occurrence of the identifier under the
        /// caret (or of an exact-word selection), IDE style. Matching is
        /// textual with word boundaries; keywords, literals and comments are
        /// excluded by asking the tokenizer what the caret word is.
        /// </summary>
        void DrawOccurrenceHighlights(NowThemeAsset themeAsset, string text, EditorCache cache, NowFontAsset font,
            float fontSize, NowFontStyle fontStyle, NowRect textRect, float lineHeight, in NowTextEditState state,
            ref EditorState editor, int firstVisible, int lastVisible)
        {
            const int MaxWordLength = 64;
            int wordStart;
            int wordEnd;

            if (state.hasSelection)
            {
                wordStart = state.selectionMin;
                wordEnd = state.selectionMax;

                if (wordEnd - wordStart > MaxWordLength)
                    return;

                for (int i = wordStart; i < wordEnd; ++i)
                {
                    if (!IsIdentifierChar(text[i]))
                        return;
                }

                bool wholeWord = (wordStart == 0 || !IsIdentifierChar(text[wordStart - 1])) &&
                    (wordEnd >= text.Length || !IsIdentifierChar(text[wordEnd]));

                if (!wholeWord)
                    return;
            }
            else
            {
                wordStart = state.caret;
                wordEnd = state.caret;

                while (wordStart > 0 && IsIdentifierChar(text[wordStart - 1]))
                    --wordStart;

                while (wordEnd < text.Length && IsIdentifierChar(text[wordEnd]))
                    ++wordEnd;
            }

            int wordLength = wordEnd - wordStart;

            if (wordLength <= 0 || wordLength > MaxWordLength)
                return;

            // Only identifiers get occurrence highlights: retokenize the word's
            // line and bail when the word is a keyword, literal or comment.
            int wordLine = LineOf(cache, wordStart);
            var wordLineSpan = cache.lines[wordLine];
            _tokenScratch.Clear();
            _language.TokenizeLine(text, wordLineSpan.start, wordLineSpan.length, cache.lineStates[wordLine], _tokenScratch);

            for (int t = 0; t < _tokenScratch.Count; ++t)
            {
                var token = _tokenScratch[t];

                if (token.start > wordStart)
                    break;

                if (token.start + token.length <= wordStart)
                    continue;

                switch (token.kind)
                {
                    case NowCodeTokenKind.Keyword:
                    case NowCodeTokenKind.String:
                    case NowCodeTokenKind.Number:
                    case NowCodeTokenKind.Comment:
                    case NowCodeTokenKind.Punctuation:
                        return;
                }

                break;
            }

            string word = text.Substring(wordStart, wordLength);
            Color highlight = themeAsset.GetColor(NowColorToken.AccentMuted, new Color(0.4f, 0.5f, 0.7f, 1f));
            highlight.a *= 0.9f;

            for (int i = firstVisible; i <= lastVisible; ++i)
            {
                var line = cache.lines[i];
                int lineEnd = line.start + line.length;
                int searchFrom = line.start;

                while (searchFrom + wordLength <= lineEnd)
                {
                    int found = text.IndexOf(word, searchFrom, lineEnd - searchFrom, System.StringComparison.Ordinal);

                    if (found < 0)
                        break;

                    bool boundary = (found == 0 || !IsIdentifierChar(text[found - 1])) &&
                        (found + wordLength >= text.Length || !IsIdentifierChar(text[found + wordLength]));

                    if (boundary)
                    {
                        float x = textRect.x + Advance(text, font, fontSize, fontStyle, line.start, found - line.start) - editor.scrollX;
                        float width = Advance(text, font, fontSize, fontStyle, found, wordLength);

                        Now.Rectangle(new NowRect(x - 1f, textRect.y + i * lineHeight - editor.scrollY, width + 2f, lineHeight))
                            .SetColor(highlight)
                            .SetRadius(3f)
                            .Draw();
                    }

                    searchFrom = found + wordLength;
                }
            }
        }

        /// <summary>
        /// Highlights the bracket at the caret and its match — the character
        /// before the caret wins over the one after, quotes are skipped (their
        /// pair is directionless), and the match search is the same depth scan
        /// the dedent rule uses.
        /// </summary>
        void DrawBracketMatch(NowThemeAsset themeAsset, string text, EditorCache cache, NowFontAsset font,
            float fontSize, NowFontStyle fontStyle, NowRect textRect, float lineHeight, in NowTextEditState state,
            ref EditorState editor, int firstVisible, int lastVisible)
        {
            int index = FindBracketAtCaret(text, state.caret, out char open, out char close, out bool isOpen);

            if (index < 0)
                return;

            int match = isOpen
                ? ScanForwardForClose(text, index, open, close)
                : ScanBackwardForOpen(text, index, open, close);

            if (match < 0)
                return;

            Color highlight = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
            highlight.a = 0.30f;

            DrawBracketHighlight(text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor,
                index, firstVisible, lastVisible, highlight);
            DrawBracketHighlight(text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor,
                match, firstVisible, lastVisible, highlight);
        }

        int FindBracketAtCaret(string text, int caret, out char open, out char close, out bool isOpen)
        {
            open = '\0';
            close = '\0';
            isOpen = false;
            var pairs = _language.autoPairs;

            for (int side = 0; side < 2; ++side)
            {
                int index = side == 0 ? caret - 1 : caret;

                if (index < 0 || index >= text.Length)
                    continue;

                char c = text[index];

                for (int i = 0; i < pairs.Count; ++i)
                {
                    if (pairs[i].open == pairs[i].close)
                        continue;

                    if (pairs[i].open == c)
                    {
                        open = pairs[i].open;
                        close = pairs[i].close;
                        isOpen = true;
                        return index;
                    }

                    if (pairs[i].close == c)
                    {
                        open = pairs[i].open;
                        close = pairs[i].close;
                        isOpen = false;
                        return index;
                    }
                }
            }

            return -1;
        }

        static int ScanForwardForClose(string text, int openIndex, char open, char close)
        {
            int depth = 0;

            for (int i = openIndex + 1; i < text.Length; ++i)
            {
                if (text[i] == open)
                {
                    ++depth;
                }
                else if (text[i] == close)
                {
                    if (depth == 0)
                        return i;

                    --depth;
                }
            }

            return -1;
        }

        static int ScanBackwardForOpen(string text, int closeIndex, char open, char close)
        {
            int depth = 0;

            for (int i = closeIndex - 1; i >= 0; --i)
            {
                if (text[i] == close)
                {
                    ++depth;
                }
                else if (text[i] == open)
                {
                    if (depth == 0)
                        return i;

                    --depth;
                }
            }

            return -1;
        }

        void DrawBracketHighlight(string text, EditorCache cache, NowFontAsset font, float fontSize,
            NowFontStyle fontStyle, NowRect textRect, float lineHeight, ref EditorState editor,
            int index, int firstVisible, int lastVisible, Color color)
        {
            int line = LineOf(cache, index);

            if (line < firstVisible || line > lastVisible)
                return;

            var span = cache.lines[line];
            float x = textRect.x + Advance(text, font, fontSize, fontStyle, span.start, index - span.start) - editor.scrollX;
            float width = Mathf.Max(Advance(text, font, fontSize, fontStyle, index, 1), 4f);

            Now.Rectangle(new NowRect(x - 1f, textRect.y + line * lineHeight - editor.scrollY, width + 2f, lineHeight))
                .SetColor(color)
                .SetRadius(3f)
                .Draw();
        }

        void DrawSelection(NowThemeAsset themeAsset, string text, EditorCache cache, NowFontAsset font, float fontSize,
            NowFontStyle fontStyle, NowRect textRect, float lineHeight, in NowTextEditState state,
            ref EditorState editor, int firstVisible, int lastVisible)
        {
            Color highlight = themeAsset.GetColor(NowColorToken.Accent, Color.blue);
            highlight.a = 0.3f;
            int selectionMin = state.selectionMin;
            int selectionMax = state.selectionMax;

            for (int i = firstVisible; i <= lastVisible; ++i)
            {
                var line = cache.lines[i];
                int lineEnd = line.start + line.length;

                if (selectionMax <= line.start || selectionMin > lineEnd)
                    continue;

                int from = Mathf.Max(selectionMin, line.start);
                int to = Mathf.Min(selectionMax, lineEnd);
                float x0 = Advance(text, font, fontSize, fontStyle, line.start, from - line.start);
                float x1 = Advance(text, font, fontSize, fontStyle, line.start, to - line.start);

                if (selectionMax > lineEnd)
                    x1 += fontSize * 0.35f;

                Now.Rectangle(new NowRect(
                        textRect.x - editor.scrollX + x0,
                        textRect.y + i * lineHeight - editor.scrollY,
                        Mathf.Max(x1 - x0, 1f),
                        lineHeight))
                    .SetColor(highlight)
                    .Draw();
            }
        }

        void DrawSquiggles(string text, EditorCache cache, NowFontAsset font, float fontSize, NowFontStyle fontStyle,
            NowRect textRect, float lineHeight, ref EditorState editor, int firstVisible, int lastVisible)
        {
            var color = new Vector4(0.86f, 0.24f, 0.24f, 1f);

            for (int d = 0; d < cache.diagnostics.Count; ++d)
            {
                var diagnostic = cache.diagnostics[d];
                int diagnosticEnd = diagnostic.start + diagnostic.length;

                for (int i = firstVisible; i <= lastVisible; ++i)
                {
                    var line = cache.lines[i];
                    int lineEnd = line.start + line.length;

                    if (diagnosticEnd <= line.start || diagnostic.start > lineEnd)
                        continue;

                    int from = Mathf.Max(diagnostic.start, line.start);
                    int to = Mathf.Min(diagnosticEnd, lineEnd);
                    float x0 = textRect.x - editor.scrollX + Advance(text, font, fontSize, fontStyle, line.start, from - line.start);
                    float x1 = textRect.x - editor.scrollX + Advance(text, font, fontSize, fontStyle, line.start, to - line.start);
                    float y = textRect.y + (i + 1) * lineHeight - editor.scrollY - 2f;

                    if (x1 - x0 < 6f)
                        x1 = x0 + 6f;

                    int step = 0;

                    for (float x = x0; x < x1; x += 3f, ++step)
                    {
                        Now.Rectangle(new NowRect(x, y + (step % 2 == 0 ? 0f : 1.5f), 2f, 1.5f))
                            .SetColor(color)
                            .Draw();
                    }
                }
            }
        }

        void DrawStatusBar(NowThemeAsset themeAsset, NowFontAsset font, float fontSize, NowFontStyle fontStyle,
            NowRect rect, float statusHeight, string text, EditorCache cache, in NowTextEditState state, int caretLine,
            Vector4 cornerRadius)
        {
            var statusRect = new NowRect(rect.x, rect.yMax - statusHeight, rect.width, statusHeight);

            Now.Rectangle(statusRect)
                .SetColor(themeAsset.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f)))
                .SetRadius(0f, 0f, cornerRadius.y, cornerRadius.w)
                .Draw();

            var line = cache.lines[caretLine];
            int column = state.caret - line.start;

            if (cache.positionLine != caretLine || cache.positionColumn != column)
            {
                cache.positionLine = caretLine;
                cache.positionColumn = column;
                cache.positionText = $"Ln {caretLine + 1}, Col {column + 1}";
            }

            var positionStyle = themeAsset.Text(
                new NowRect(statusRect.x + 8f, statusRect.y, 220f, statusRect.height), NowTextStyle.Muted);
            positionStyle.SetFontSize(11f).Draw(cache.positionText);

            string message = cache.statusMessage ?? string.Empty;
            Vector4 messageColor = cache.diagnostics.Count == 0
                ? themeAsset.GetColor(NowColorToken.TextMuted, Color.gray)
                : new Vector4(0.86f, 0.24f, 0.24f, 1f);

            float messageWidth = Advance(message, font, 11f, fontStyle);
            var messageRect = new NowRect(statusRect.xMax - messageWidth - 12f, statusRect.y, messageWidth + 8f, statusRect.height);

            var messageStyle = themeAsset.Text(messageRect, NowTextStyle.Muted);
            messageStyle.color = messageColor;
            messageStyle.SetFontSize(11f).Draw(message);
        }

        void DrawDiagnosticTooltip(int id, NowThemeAsset themeAsset, string text, EditorCache cache, NowFontAsset font,
            float fontSize, NowFontStyle fontStyle, NowRect textRect, float lineHeight, ref EditorState editor, Vector2 pointer)
        {
            if (cache.diagnostics.Count == 0 || !textRect.Contains(pointer))
                return;

            int hoverLine = Mathf.FloorToInt((pointer.y - textRect.y + editor.scrollY) / lineHeight);

            if (hoverLine < 0 || hoverLine >= cache.lines.Count)
                return;

            var line = cache.lines[hoverLine];
            int hoverIndex = HitIndex(text, line, font, fontSize, fontStyle, pointer.x - textRect.x + editor.scrollX);

            for (int d = 0; d < cache.diagnostics.Count; ++d)
            {
                var diagnostic = cache.diagnostics[d];

                if (hoverIndex < diagnostic.start || hoverIndex > diagnostic.start + diagnostic.length)
                    continue;

                string message = diagnostic.message ?? string.Empty;
                float width = Advance(message, font, 12f, fontStyle) + 16f;
                cache.tooltipMessage = message;
                cache.tooltipRect = new NowRect(pointer.x + 12f, pointer.y + 18f, width, 24f);

                // Passive: a capturing overlay would open a focus layer, making the
                // editor read as unfocused while the tooltip shows — on dismissal it
                // "regains" focus and jumps the caret to the end of the text.
                NowOverlay.DeferPassive(id, DrawDiagnosticTooltipOverlay);
                return;
            }
        }

        static void DrawDiagnosticTooltipOverlay(int id)
        {
            if (!_caches.TryGetValue(id, out var cache) || string.IsNullOrEmpty(cache.tooltipMessage))
                return;

            var tooltipRect = cache.tooltipRect;
            var theme = NowTheme.themeAsset;
            var background = theme.Rectangle(tooltipRect, NowRectangleStyle.Surface);
            background.outline = 1f;
            background.outlineColor = theme.GetColor(NowColorToken.Border, Color.gray);
            background.SetRadius(4f).Draw();

            var tooltipStyle = theme.Text(
                new NowRect(tooltipRect.x + 8f, tooltipRect.y, tooltipRect.width, tooltipRect.height),
                NowTextStyle.Body);
            tooltipStyle.SetFontSize(12f).Draw(cache.tooltipMessage);
            cache.tooltipMessage = null;
        }

        static bool CompletionsOpen(EditorCache cache)
        {
            return cache.completionReplaceStart >= 0 && cache.completionVisible.Count > 0;
        }

        static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        static void CloseCompletions(EditorCache cache)
        {
            cache.completionReplaceStart = -1;
            cache.completionItems.Clear();
            cache.completionVisible.Clear();
            cache.completionSelected = 0;
            cache.completionWindow = 0;
        }

        /// <summary>
        /// Narrows an open popup against the word at the caret, or asks the
        /// language for fresh candidates when no popup is open (word start or
        /// trigger character). The language is only queried once per word; the
        /// filter handles every following keystroke.
        /// </summary>
        static void OpenOrRefreshCompletions(EditorCache cache, NowCodeLanguage language, string text, int caret)
        {
            if (cache.completionReplaceStart >= 0)
            {
                RefreshCompletionFilter(cache, text, caret);

                if (cache.completionReplaceStart >= 0)
                    return;
            }

            cache.completionItems.Clear();
            cache.completionVisible.Clear();

            if (!language.TryGetCompletions(text, caret, cache.completionItems, out int replaceStart) ||
                cache.completionItems.Count == 0)
            {
                CloseCompletions(cache);
                return;
            }

            cache.completionReplaceStart = Mathf.Clamp(replaceStart, 0, caret);
            cache.completionSelected = 0;
            cache.completionWindow = 0;
            RefreshCompletionFilter(cache, text, caret);
        }

        static void RefreshCompletionFilter(EditorCache cache, string text, int caret)
        {
            int start = cache.completionReplaceStart;

            if (start < 0)
                return;

            if (caret < start || caret > text.Length)
            {
                CloseCompletions(cache);
                return;
            }

            for (int i = start; i < caret; ++i)
            {
                if (!IsIdentifierChar(text[i]))
                {
                    CloseCompletions(cache);
                    return;
                }
            }

            cache.completionVisible.Clear();
            int wordLength = caret - start;

            for (int i = 0; i < cache.completionItems.Count; ++i)
            {
                string label = cache.completionItems[i].label;

                if (string.IsNullOrEmpty(label) || label.Length < wordLength)
                    continue;

                if (wordLength == 0 ||
                    string.Compare(label, 0, text, start, wordLength, System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    cache.completionVisible.Add(i);
                }
            }

            if (cache.completionVisible.Count == 0)
            {
                CloseCompletions(cache);
                return;
            }

            cache.completionSelected = Mathf.Clamp(cache.completionSelected, 0, cache.completionVisible.Count - 1);
        }

        static void AcceptCompletion(EditorCache cache, ref string text, ref NowTextEditState state)
        {
            if (!CompletionsOpen(cache))
                return;

            var item = cache.completionItems[cache.completionVisible[cache.completionSelected]];
            string insert = string.IsNullOrEmpty(item.insertText) ? item.label : item.insertText;
            int start = cache.completionReplaceStart;
            int end = Mathf.Clamp(state.caret, start, text.Length);

            text = text.Remove(start, end - start).Insert(start, insert);
            state.caret = start + insert.Length;
            state.anchor = state.caret;
            CloseCompletions(cache);
        }

        static void LayoutCompletionPopup(int id, EditorCache cache, string text, NowFontAsset font, float fontSize,
            NowFontStyle fontStyle, NowRect textRect, float lineHeight, in EditorState editor, int caretLine)
        {
            int count = cache.completionVisible.Count;
            int rows = Mathf.Min(MaxVisibleCompletions, count);
            float rowHeight = Mathf.Ceil(fontSize + 8f);

            // Keep the selection inside the visible window.
            if (cache.completionSelected < cache.completionWindow)
                cache.completionWindow = cache.completionSelected;

            if (cache.completionSelected >= cache.completionWindow + rows)
                cache.completionWindow = cache.completionSelected - rows + 1;

            cache.completionWindow = Mathf.Clamp(cache.completionWindow, 0, Mathf.Max(0, count - rows));

            float width = 160f;

            for (int r = 0; r < rows; ++r)
            {
                var item = cache.completionItems[cache.completionVisible[cache.completionWindow + r]];
                float rowWidth = 20f + Advance(item.label, font, fontSize, fontStyle);

                if (!string.IsNullOrEmpty(item.detail))
                    rowWidth += 24f + Advance(item.detail, font, fontSize * 0.85f, fontStyle);

                width = Mathf.Max(width, rowWidth);
            }

            width = Mathf.Min(width, 440f);

            var anchorLine = cache.lines[LineOf(cache, Mathf.Min(cache.completionReplaceStart, text.Length))];
            float anchorX = Advance(text, font, fontSize, fontStyle, anchorLine.start,
                Mathf.Max(cache.completionReplaceStart - anchorLine.start, 0));
            float height = rows * rowHeight + CompletionPadding * 2f;

            float x = Mathf.Clamp(textRect.x + anchorX - editor.scrollX, textRect.x, Mathf.Max(textRect.x, textRect.xMax - width));
            float caretTop = textRect.y + caretLine * lineHeight - editor.scrollY;
            float y = caretTop + lineHeight + 2f;

            // Flip above the caret line when there is no room below.
            if (y + height > textRect.yMax && caretTop - height - 2f >= textRect.y)
                y = caretTop - height - 2f;

            cache.completionPopupRect = new NowRect(x, y, width, height);
            cache.completionRowHeight = rowHeight;
            NowOverlay.DeferPassive(id, DrawCompletionOverlay);
        }

        static void DrawCompletionOverlay(int id)
        {
            if (!_caches.TryGetValue(id, out var cache) ||
                cache.completionReplaceStart < 0 ||
                cache.completionVisible.Count == 0)
            {
                return;
            }

            var theme = NowTheme.themeAsset;
            var rect = cache.completionPopupRect;
            var background = theme.Rectangle(rect, NowRectangleStyle.Surface);
            background.outline = 1f;
            background.outlineColor = theme.GetColor(NowColorToken.Border, Color.gray);
            background.SetRadius(4f).Draw();

            var font = cache.measureFont;
            float fontSize = cache.measureFontSize;
            float rowHeight = cache.completionRowHeight;
            int rows = Mathf.Min(MaxVisibleCompletions, cache.completionVisible.Count - cache.completionWindow);

            for (int r = 0; r < rows; ++r)
            {
                int index = cache.completionWindow + r;
                var item = cache.completionItems[cache.completionVisible[index]];
                var rowRect = new NowRect(rect.x + 2f, rect.y + CompletionPadding + r * rowHeight, rect.width - 4f, rowHeight);

                if (index == cache.completionSelected)
                {
                    Now.Rectangle(rowRect)
                        .SetColor(theme.palette.surfaceHover)
                        .SetRadius(3f)
                        .Draw();
                }

                var labelStyle = theme.Text(new NowRect(rowRect.x + 8f, rowRect.y, rowRect.width - 16f, rowRect.height), NowTextStyle.Body);
                labelStyle.SetFontSize(fontSize).Draw(item.label);

                if (!string.IsNullOrEmpty(item.detail))
                {
                    float detailWidth = Advance(item.detail, font, fontSize * 0.85f, cache.measureFontStyle);
                    var detailStyle = theme.Text(
                        new NowRect(rowRect.xMax - detailWidth - 8f, rowRect.y, detailWidth + 4f, rowRect.height),
                        NowTextStyle.Muted);
                    detailStyle.SetFontSize(fontSize * 0.85f).Draw(item.detail);
                }
            }
        }

        static string NumberString(int value)
        {
            while (_numberStrings.Count < value)
                _numberStrings.Add((_numberStrings.Count + 1).ToString());

            return _numberStrings[value - 1];
        }

        static EditorCache GetCache(int id, NowCodeLanguage language)
        {
            if (!_caches.TryGetValue(id, out var cache))
            {
                cache = new EditorCache();
                _caches[id] = cache;
            }

            if (cache.language != language)
            {
                cache.language = language;
                cache.text = null;
                cache.undo.Clear();
            }

            return cache;
        }

        /// <summary>
        /// Refreshes the line table, per-line tokenizer states, widths and
        /// diagnostics for <paramref name="text"/>. Retokenization is
        /// incremental: unchanged lines before the edit reuse their cached
        /// state, and it stops as soon as the carry state re-synchronizes with
        /// the cached state of a line inside the unchanged tail.
        /// </summary>
        static void Rebuild(EditorCache cache, string text, NowFontAsset font, float fontSize, NowFontStyle fontStyle)
        {
            string oldText = cache.text;
            int oldCount = cache.lines.Count;
            bool incremental = oldText != null &&
                cache.lineStates.Count == oldCount &&
                cache.lineWidths.Count == oldCount &&
                ReferenceEquals(cache.measureFont, font) &&
                cache.measureFontSize == fontSize &&
                cache.measureFontStyle == fontStyle;

            cache.measureFont = font;
            cache.measureFontSize = fontSize;
            cache.measureFontStyle = fontStyle;

            _oldStateScratch.Clear();
            _oldStartScratch.Clear();
            _oldWidthScratch.Clear();

            if (incremental)
            {
                for (int i = 0; i < oldCount; ++i)
                {
                    _oldStateScratch.Add(cache.lineStates[i]);
                    _oldStartScratch.Add(cache.lines[i].start);
                    _oldWidthScratch.Add(cache.lineWidths[i]);
                }
            }

            cache.text = text;
            cache.lines.Clear();
            cache.lineStates.Clear();
            cache.lineWidths.Clear();
            cache.diagnostics.Clear();
            NowTextMetrics.LayoutHardLines(text, cache.lines);

            int prefix = 0;
            int suffix = 0;

            if (incremental)
            {
                int shared = Mathf.Min(oldText.Length, text.Length);

                while (prefix < shared && oldText[prefix] == text[prefix])
                    ++prefix;

                int maxSuffix = shared - prefix;

                while (suffix < maxSuffix && oldText[oldText.Length - 1 - suffix] == text[text.Length - 1 - suffix])
                    ++suffix;
            }

            int newCount = cache.lines.Count;
            int shift = oldCount - newCount;
            int suffixStart = text.Length - suffix;

            int line = 0;

            while (incremental && line < newCount - 1 && line < oldCount && cache.lines[line + 1].start <= prefix)
            {
                cache.lineStates.Add(_oldStateScratch[line]);
                cache.lineWidths.Add(_oldWidthScratch[line]);
                ++line;
            }

            int state = incremental && line < _oldStateScratch.Count ? _oldStateScratch[line] : 0;

            int startShift = (oldText?.Length ?? 0) - text.Length;

            for (; line < newCount; ++line)
            {
                int oldLine = line + shift;

                if (incremental && oldLine >= 0 && oldLine < oldCount &&
                    cache.lines[line].start >= prefix &&
                    cache.lines[line].start >= suffixStart &&
                    _oldStartScratch[oldLine] == cache.lines[line].start + startShift &&
                    state == _oldStateScratch[oldLine])
                {
                    for (int k = line; k < newCount; ++k)
                    {
                        cache.lineStates.Add(_oldStateScratch[k + shift]);
                        cache.lineWidths.Add(_oldWidthScratch[k + shift]);
                    }

                    break;
                }

                cache.lineStates.Add(state);
                _tokenScratch.Clear();
                state = cache.language.TokenizeLine(text, cache.lines[line].start, cache.lines[line].length, state, _tokenScratch);
                cache.lineWidths.Add(Advance(text, font, fontSize, fontStyle, cache.lines[line].start, cache.lines[line].length));
            }

            cache.contentWidth = 0f;

            for (int i = 0; i < cache.lineWidths.Count; ++i)
            {
                if (cache.lineWidths[i] > cache.contentWidth)
                    cache.contentWidth = cache.lineWidths[i];
            }

            Revalidate(cache, text);
        }

        /// <summary>
        /// Re-runs validation without retokenizing — on every edit, and again
        /// when an async validator bumps its version for the same text.
        /// </summary>
        static void Revalidate(EditorCache cache, string text)
        {
            cache.validatedVersion = cache.language.validationVersion;
            cache.diagnostics.Clear();
            cache.language.Validate(text, cache.diagnostics);

            if (cache.diagnostics.Count == 0)
            {
                cache.statusMessage = $"{cache.language.name} — no problems";
            }
            else
            {
                var diagnostic = cache.diagnostics[0];
                cache.statusMessage = $"Line {LineOf(cache, diagnostic.start) + 1}: {diagnostic.message}";
            }
        }

        static int LineOf(EditorCache cache, int index)
        {
            int low = 0;
            int high = cache.lines.Count - 1;

            while (low < high)
            {
                int mid = (low + high + 1) >> 1;

                if (cache.lines[mid].start <= index)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style, int start, int count)
        {
            return NowTextMetrics.Advance(text, font, fontSize, style, start, count);
        }

        static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style)
        {
            return Advance(text, font, fontSize, style, 0, text.Length);
        }

        static int HitIndex(string text, in NowTextLine line, NowFontAsset font, float fontSize, NowFontStyle style, float x)
        {
            return NowTextMetrics.HitIndex(text, line, font, fontSize, style, x);
        }

        static int HitTest(string text, EditorCache cache, NowFontAsset font, float fontSize, NowFontStyle style,
            Vector2 pointer, NowRect textRect, float lineHeight, float scrollX, float scrollY)
        {
            return NowTextMetrics.HitTest(text, cache.lines, font, fontSize, style, pointer, textRect,
                lineHeight, scrollX, scrollY);
        }

        static void MoveVertical(ref NowTextEditState state, ref EditorState editor, string text, EditorCache cache,
            NowFontAsset font, float fontSize, NowFontStyle style, int direction, bool select)
        {
            if (!select && state.hasSelection)
            {
                state.caret = direction < 0 ? state.selectionMin : state.selectionMax;
                state.anchor = state.caret;
            }

            int line = LineOf(cache, state.caret);
            int target = line + direction;

            if (target < 0)
                state.caret = 0;
            else if (target >= cache.lines.Count)
                state.caret = text.Length;
            else
                state.caret = HitIndex(text, cache.lines[target], font, fontSize, style, editor.goalX);

            if (!select)
                state.anchor = state.caret;
        }

        static string CurrentLine(string text, int caret)
        {
            NowTextMetrics.LineBounds(text, caret, out int start, out int end);
            return text.Substring(start, end - start) + "\n";
        }

        static void CutLine(ref string text, ref NowTextEditState state)
        {
            NowTextMetrics.LineBounds(text, state.caret, out int start, out int end);
            s_lineClipboard = text.Substring(start, end - start) + "\n";
            NowClipboard.Copy(s_lineClipboard);

            int removeStart = start;
            int removeEnd = end;

            if (end < text.Length)
                removeEnd = end + 1;
            else if (start > 0)
                removeStart = start - 1;

            text = text.Remove(removeStart, removeEnd - removeStart);
            state.caret = Mathf.Clamp(removeStart, 0, text.Length);
            state.anchor = state.caret;
        }

        static void DuplicateLines(ref string text, ref NowTextEditState state)
        {
            int from = state.hasSelection ? state.selectionMin : state.caret;
            int to = state.hasSelection ? state.selectionMax : state.caret;
            NowTextMetrics.LineBounds(text, from, out int start, out _);
            NowTextMetrics.LineBounds(text, to, out _, out int end);

            string block = text.Substring(start, end - start);
            text = text.Insert(end, "\n" + block);

            int delta = block.Length + 1;
            state.caret += delta;
            state.anchor += delta;
        }

        static void SmartHome(string text, EditorCache cache, ref NowTextEditState state, bool select)
        {
            var line = cache.lines[LineOf(cache, state.caret)];
            int firstNonWhitespace = line.start;
            int lineEnd = line.start + line.length;

            while (firstNonWhitespace < lineEnd && text[firstNonWhitespace] == ' ')
                ++firstNonWhitespace;

            state.caret = state.caret == firstNonWhitespace ? line.start : firstNonWhitespace;

            if (!select)
                state.anchor = state.caret;
        }

        static void HandleCharacter(char c, ref string text, ref NowTextEditState state, NowCodeLanguage language)
        {
            var pairs = language.autoPairs;

            if (state.hasSelection)
            {
                for (int i = 0; i < pairs.Count; ++i)
                {
                    if (pairs[i].open != c)
                        continue;

                    int min = state.selectionMin;
                    int max = state.selectionMax;
                    text = text.Insert(max, pairs[i].close.ToString()).Insert(min, pairs[i].open.ToString());
                    state.anchor = min + 1;
                    state.caret = max + 1;
                    return;
                }
            }

            if (!state.hasSelection && language.TryComplete(c, text, in state, out var completion))
            {
                ApplyCompletion(ref text, ref state, in completion);
                return;
            }
            else if (state.caret < text.Length && text[state.caret] == c)
            {
                for (int i = 0; i < pairs.Count; ++i)
                {
                    if (pairs[i].close != c)
                        continue;

                    ++state.caret;
                    state.anchor = state.caret;
                    return;
                }
            }

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].open != c)
                    continue;

                bool symmetric = pairs[i].open == pairs[i].close;

                if (symmetric)
                {
                    char next = state.caret < text.Length ? text[state.caret] : '\0';
                    char previous = state.caret > 0 ? text[state.caret - 1] : '\0';
                    bool nextBlocks = char.IsLetterOrDigit(next);
                    bool previousBlocks = char.IsLetterOrDigit(previous) || previous == '\\' || previous == c;

                    if (nextBlocks || previousBlocks)
                        break;
                }

                NowTextEdit.Insert(ref text, ref state, PairText(pairs[i]));
                --state.caret;
                state.anchor = state.caret;
                return;
            }

            NowTextEdit.Insert(ref text, ref state, c.ToString());
        }

        static void ApplyCompletion(ref string text, ref NowTextEditState state, in NowCodeCompletion completion)
        {
            string insert = completion.text ?? string.Empty;
            int removeBefore = Mathf.Max(0, completion.removeBeforeCaret);
            int removeAfter = Mathf.Max(0, completion.removeAfterCaret);
            int start = Mathf.Clamp(state.caret - removeBefore, 0, text.Length);
            int end = Mathf.Clamp(state.caret + removeAfter, start, text.Length);

            text = text.Remove(start, end - start).Insert(start, insert);
            state.caret = Mathf.Clamp(start + completion.caretOffset, start, start + insert.Length);
            state.anchor = state.caret;
        }

        static readonly Dictionary<int, string> _pairTexts = new Dictionary<int, string>(8);

        static string PairText(NowCodeAutoPair pair)
        {
            int key = (pair.open << 16) | pair.close;

            if (!_pairTexts.TryGetValue(key, out string value))
            {
                value = new string(new[] { pair.open, pair.close });
                _pairTexts[key] = value;
            }

            return value;
        }

        static bool PairBackspace(ref string text, ref NowTextEditState state, NowCodeLanguage language)
        {
            if (state.hasSelection || state.caret <= 0 || state.caret >= text.Length)
                return false;

            var pairs = language.autoPairs;

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (text[state.caret - 1] != pairs[i].open || text[state.caret] != pairs[i].close)
                    continue;

                state.anchor = state.caret - 1;
                state.caret = state.caret + 1;
                NowTextEdit.DeleteSelection(ref text, ref state);
                return true;
            }

            return false;
        }

        static bool IndentBackspace(ref string text, ref NowTextEditState state)
        {
            if (state.hasSelection || state.caret <= 0)
                return false;

            NowTextMetrics.LineBounds(text, state.caret, out int lineStart, out _);
            int column = state.caret - lineStart;

            if (column <= 0)
                return false;

            int remove = column % IndentUnit.Length;

            if (remove == 0)
                remove = IndentUnit.Length;

            if (state.caret - remove < lineStart)
                return false;

            for (int i = state.caret - remove; i < state.caret; ++i)
            {
                if (text[i] != ' ')
                    return false;
            }

            text = text.Remove(state.caret - remove, remove);
            state.caret -= remove;
            state.anchor = state.caret;
            return true;
        }

        /// <summary>
        /// Formatting on close: when a closing bracket is typed onto an
        /// otherwise-whitespace line, the line dedents to match the line that
        /// opened the pair, and every enclosed line re-indents by bracket depth
        /// — so wrapping existing code in a new scope indents it properly.
        /// Lines inside multi-line strings or comments are left untouched.
        /// </summary>
        static void ReindentCloser(char closer, ref string text, ref NowTextEditState state, NowCodeLanguage language, EditorCache cache)
        {
            int closerIndex = state.caret - 1;

            if (closerIndex < 0 || closerIndex >= text.Length || text[closerIndex] != closer)
                return;

            int lineStart = closerIndex;

            while (lineStart > 0 && text[lineStart - 1] != '\n')
                --lineStart;

            for (int i = lineStart; i < closerIndex; ++i)
            {
                if (text[i] != ' ' && text[i] != '\t')
                    return;
            }

            char opener = '\0';
            var pairs = language.autoPairs;

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].close == closer)
                {
                    opener = pairs[i].open;
                    break;
                }
            }

            if (opener == '\0' || opener == closer)
                return;

            // Scan back for the matching opener (a heuristic — strings and
            // comments are not skipped, like most lightweight editors).
            int depth = 0;
            int scan = closerIndex - 1;

            while (scan >= 0)
            {
                char c = text[scan];

                if (c == closer)
                {
                    ++depth;
                }
                else if (c == opener)
                {
                    if (depth == 0)
                        break;

                    --depth;
                }

                --scan;
            }

            if (scan < 0)
                return;

            int openerLineStart = scan;

            while (openerLineStart > 0 && text[openerLineStart - 1] != '\n')
                --openerLineStart;

            int indentEnd = openerLineStart;

            while (indentEnd < text.Length && text[indentEnd] == ' ')
                ++indentEnd;

            int currentIndent = closerIndex - lineStart;
            int targetIndent = indentEnd - openerLineStart;
            string baseIndent = text.Substring(openerLineStart, targetIndent);

            if (targetIndent != currentIndent)
            {
                text = text.Remove(lineStart, currentIndent).Insert(lineStart, baseIndent);
                state.caret += targetIndent - currentIndent;
                state.anchor = state.caret;
            }

            // The edit above happened at the closer's own line, so every index
            // before lineStart — including the opener — is still valid.
            ReindentEnclosedBlock(ref text, ref state, language, cache, scan, baseIndent, lineStart);
        }

        /// <summary>
        /// Re-indents every line between a bracket pair to
        /// <paramref name="baseIndent"/> plus one indent unit per nesting level,
        /// with closer-led lines dedenting one level. Blank lines lose trailing
        /// whitespace; blocks touching multi-line strings or comments are left
        /// alone (their layout is content, not formatting).
        /// </summary>
        static void ReindentEnclosedBlock(ref string text, ref NowTextEditState state, NowCodeLanguage language,
            EditorCache cache, int openerIndex, string baseIndent, int closerLineStart)
        {
            int firstLineStart = openerIndex;

            while (firstLineStart < closerLineStart && text[firstLineStart] != '\n')
                ++firstLineStart;

            ++firstLineStart;

            if (firstLineStart >= closerLineStart)
                return;

            // The cache predates this keystroke, but the typed characters add no
            // newline, so line indices still map; any interior line starting
            // inside a multi-line construct vetoes the reformat.
            if (cache.text != null && cache.lineStates.Count == cache.lines.Count)
            {
                int cachedLength = cache.text.Length;
                int firstLine = LineOf(cache, Mathf.Min(firstLineStart, cachedLength));
                int lastLine = LineOf(cache, Mathf.Min(closerLineStart, cachedLength));

                for (int i = firstLine; i <= lastLine && i < cache.lineStates.Count; ++i)
                {
                    if (cache.lineStates[i] != 0)
                        return;
                }
            }

            var builder = _blockEditScratch;
            builder.Clear();
            builder.Append(text, 0, firstLineStart);

            int depth = 1;
            int pos = firstLineStart;

            while (pos < closerLineStart)
            {
                int lineEnd = pos;

                while (text[lineEnd] != '\n')
                    ++lineEnd;

                int contentStart = pos;

                while (contentStart < lineEnd && (text[contentStart] == ' ' || text[contentStart] == '\t'))
                    ++contentStart;

                if (contentStart == lineEnd)
                {
                    builder.Append('\n');
                }
                else
                {
                    int lineDepth = language.IsIndentCloser(text[contentStart]) ? Mathf.Max(depth - 1, 0) : depth;
                    builder.Append(baseIndent);

                    for (int d = 0; d < lineDepth; ++d)
                        builder.Append(IndentUnit);

                    builder.Append(text, contentStart, lineEnd - contentStart);
                    builder.Append('\n');

                    for (int k = contentStart; k < lineEnd; ++k)
                    {
                        if (language.IsIndentOpener(text[k]))
                            ++depth;
                        else if (language.IsIndentCloser(text[k]))
                            depth = Mathf.Max(depth - 1, 0);
                    }
                }

                pos = lineEnd + 1;
            }

            int delta = builder.Length - closerLineStart;

            if (delta == 0 && builder.Length == closerLineStart)
            {
                bool unchanged = true;

                for (int i = firstLineStart; i < closerLineStart && unchanged; ++i)
                    unchanged = builder[i] == text[i];

                if (unchanged)
                    return;
            }

            builder.Append(text, closerLineStart, text.Length - closerLineStart);
            state.caret += delta;
            state.anchor = state.caret;
            text = builder.ToString();
        }

        static void InsertNewlineWithIndent(ref string text, ref NowTextEditState state, NowCodeLanguage language)
        {
            if (state.hasSelection)
                NowTextEdit.DeleteSelection(ref text, ref state);

            int lineStart = state.caret;

            while (lineStart > 0 && text[lineStart - 1] != '\n')
                --lineStart;

            int indentEnd = lineStart;

            while (indentEnd < state.caret && indentEnd < text.Length && text[indentEnd] == ' ')
                ++indentEnd;

            string indent = text.Substring(lineStart, indentEnd - lineStart);
            char previous = state.caret > 0 ? text[state.caret - 1] : '\0';
            char next = state.caret < text.Length ? text[state.caret] : '\0';

            if (language.IsIndentOpener(previous) && language.IsIndentCloser(next))
            {
                NowTextEdit.Insert(ref text, ref state, "\n" + indent + IndentUnit + "\n" + indent);
                state.caret -= indent.Length + 1;
                state.anchor = state.caret;
            }
            else if (language.IsIndentOpener(previous))
            {
                NowTextEdit.Insert(ref text, ref state, "\n" + indent + IndentUnit);
            }
            else
            {
                NowTextEdit.Insert(ref text, ref state, "\n" + indent);
            }
        }

        static void HandleTab(ref string text, ref NowTextEditState state, EditorCache cache, bool shift)
        {
            int firstLine = LineOf(cache, state.selectionMin);
            int lastLine = LineOf(cache, state.selectionMax);

            if (state.hasSelection && state.selectionMax > state.selectionMin &&
                lastLine > firstLine && cache.lines[lastLine].start == state.selectionMax)
            {
                --lastLine;
            }

            bool blockOperation = shift || (state.hasSelection && lastLine > firstLine);

            if (!blockOperation)
            {
                NowTextEdit.Insert(ref text, ref state, IndentUnit);
                return;
            }

            int delta = 0;
            int firstLineDelta = 0;

            _blockEditScratch.Clear();
            _blockEditScratch.EnsureCapacity(text.Length + (shift ? 0 : (lastLine - firstLine + 1) * IndentUnit.Length));
            int cursor = 0;

            for (int i = firstLine; i <= lastLine; ++i)
            {
                int lineStart = cache.lines[i].start;
                _blockEditScratch.Append(text, cursor, lineStart - cursor);
                cursor = lineStart;

                if (shift)
                {
                    int remove = 0;

                    while (remove < IndentUnit.Length && lineStart + remove < text.Length && text[lineStart + remove] == ' ')
                        ++remove;

                    if (remove > 0)
                    {
                        cursor = lineStart + remove;
                        delta -= remove;

                        if (i == firstLine)
                            firstLineDelta = -remove;
                    }
                }
                else
                {
                    _blockEditScratch.Append(IndentUnit);
                    delta += IndentUnit.Length;

                    if (i == firstLine)
                        firstLineDelta = IndentUnit.Length;
                }
            }

            _blockEditScratch.Append(text, cursor, text.Length - cursor);

            if (delta != 0)
                text = _blockEditScratch.ToString();

            if (state.hasSelection)
            {
                int min = Mathf.Max(cache.lines[firstLine].start, state.selectionMin + firstLineDelta);
                int max = Mathf.Clamp(state.selectionMax + delta, min, text.Length);
                bool caretAtEnd = state.caret >= state.anchor;
                state.anchor = caretAtEnd ? min : max;
                state.caret = caretAtEnd ? max : min;
            }
            else
            {
                state.caret = Mathf.Clamp(state.caret + firstLineDelta, 0, text.Length);
                state.anchor = state.caret;
            }
        }

    }
}
