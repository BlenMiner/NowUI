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
        public static NowCodeEditor Editor(NowCodeLanguage language, string id = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCodeEditor(language, id, NowControls.SiteId(file, line));
        }

        public static NowCodeEditor Editor(NowRect rect, NowCodeLanguage language, string id = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowCodeEditor(rect, language, id, NowControls.SiteId(file, line));
        }
    }

    /// <summary>
    /// A code editor: syntax highlighting through a <see cref="NowCodeLanguage"/>
    /// profile, validation squiggles with hover tooltips and a status bar
    /// (click the error to jump to it), bracket/quote auto-close, skip-over and
    /// selection wrapping, Enter auto-indent with brace expansion, Tab
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

        readonly string _id;
        readonly int _site;
        readonly NowCodeLanguage _language;
        readonly NowRect _rect;
        readonly bool _hasRect;
        float _height;
        float _width;
        float _fontSize;
        bool _hideLineNumbers;
        bool _hideStatusBar;

        internal NowCodeEditor(NowCodeLanguage language, string id, int site)
        {
            _language = language;
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _height = 240f;
            _width = 0f;
            _fontSize = 14f;
            _hideLineNumbers = false;
            _hideStatusBar = false;
        }

        internal NowCodeEditor(NowRect rect, NowCodeLanguage language, string id, int site) : this(language, id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowCodeEditor SetHeight(float height) { _height = height; return this; }

        public NowCodeEditor SetWidth(float width) { _width = width; return this; }

        public NowCodeEditor SetFontSize(float fontSize) { _fontSize = fontSize; return this; }

        public NowCodeEditor SetLineNumbers(bool show) { _hideLineNumbers = !show; return this; }

        public NowCodeEditor SetStatusBar(bool show) { _hideStatusBar = !show; return this; }

        struct LineSpan
        {
            public int start;
            public int length;
        }

        struct UndoEntry
        {
            public string text;
            public int caret;
            public int anchor;
        }

        sealed class EditorCache
        {
            public string text;
            public NowCodeLanguage language;
            public readonly List<LineSpan> lines = new List<LineSpan>(64);
            public readonly List<int> lineStates = new List<int>(64);
            public readonly List<NowCodeDiagnostic> diagnostics = new List<NowCodeDiagnostic>(4);
            public float contentWidth;
            public readonly List<UndoEntry> undo = new List<UndoEntry>(32);
            public readonly List<UndoEntry> redo = new List<UndoEntry>(8);
            public float lastEditTime;
            public bool lastWasTyping;
            public string statusMessage;
            public string positionText;
            public int positionLine = -1;
            public int positionColumn = -1;
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

            var theme = NowControls.theme;
            int id = _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);

            var textStyle = theme.Text(default, NowTextStyle.Body).SetFontSize(_fontSize);
            var font = textStyle.font;

            if (font == null)
                return result;

            float lineHeight = font.GetLineHeight(textStyle.fontStyle) * _fontSize;
            float statusHeight = _hideStatusBar ? 0f : StatusHeight;

            NowRect rect = _hasRect
                ? _rect
                : NowLayout.Rect(width: _width, height: _height, stretchWidth: _width <= 0f);

            ref var state = ref NowUIControlState.Get<NowTextEditState>(id);
            NowTextEdit.Clamp(ref state, text);
            ref var editor = ref NowUIControlState.Get<EditorState>(NowUIInput.GetId(id, "editor"));

            var cache = GetCache(id, _language);

            if (!ReferenceEquals(cache.text, text))
                Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

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
            if (!NowUIInput.isPassive)
            {
                Scrollbars(cache, rect, textRect, statusHeight, lineHeight, editor,
                    out float maxX, out float maxY, out _, out var vThumb, out _, out var hThumb);

                if (maxY > 0f)
                {
                    var vDrag = NowUIInput.Interact(NowUIInput.GetId(id, "vscroll"), vThumb);

                    if (vDrag.pressed)
                        NowUIFocus.Focus(id);

                    if (vDrag.dragging)
                    {
                        editor.scrollY += vDrag.dragDelta.y / Mathf.Max(textRect.height - vThumb.height, 1f) * maxY;
                        NowUIControlState.RequestRepaint();
                    }
                }

                if (maxX > 0f)
                {
                    var hDrag = NowUIInput.Interact(NowUIInput.GetId(id, "hscroll"), hThumb);

                    if (hDrag.pressed)
                        NowUIFocus.Focus(id);

                    if (hDrag.dragging)
                    {
                        editor.scrollX += hDrag.dragDelta.x / Mathf.Max(textRect.width - hThumb.width, 1f) * maxX;
                        NowUIControlState.RequestRepaint();
                    }
                }
            }

            var interaction = NowControls.Interact(id, rect, out bool focused, out _);

            if (focused && editor.hadFocus == 0)
            {
                NowUITextInput.setImeEnabled?.Invoke(true);

                if (!interaction.pressed)
                    NowTextEdit.MoveEnd(ref state, text, false);
            }
            else if (!focused && editor.hadFocus == 1)
            {
                NowUITextInput.setImeEnabled?.Invoke(false);
            }

            editor.hadFocus = focused ? (byte)1 : (byte)0;

            bool verticalMove = false;

            if (interaction.pressed)
            {
                bool onStatusBar = statusHeight > 0f && interaction.pointerPosition.y >= rect.yMax - statusHeight - 1f;
                bool onGutter = !onStatusBar && interaction.pointerPosition.x < textRect.x - 2f;

                if (onStatusBar)
                {
                    if (cache.diagnostics.Count > 0)
                    {
                        state.caret = Mathf.Clamp(cache.diagnostics[0].start, 0, text.Length);
                        state.anchor = state.caret;
                    }
                }
                else
                {
                    int hit = HitTest(text, cache, font, _fontSize, textStyle.fontStyle,
                        interaction.pointerPosition, textRect, lineHeight, editor.scrollX, editor.scrollY);
                    int streak = NowUIControlState.ClickStreak(id, true);

                    if (onGutter || streak >= 3)
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
            }
            else if (interaction.dragging)
            {
                state.caret = HitTest(text, cache, font, _fontSize, textStyle.fontStyle,
                    interaction.pointerPosition, textRect, lineHeight, editor.scrollX, editor.scrollY);
                NowUIControlState.RequestRepaint();
            }

            string composition = null;

            if (focused && !NowUIInput.isPassive)
            {
                NowUIFocus.LockNavigation();
                var frame = NowUITextInput.current;
                composition = string.IsNullOrEmpty(frame.composition) ? null : frame.composition;

                if (!string.IsNullOrEmpty(frame.characters))
                {
                    PushUndo(cache, text, in state, typing: true);

                    for (int i = 0; i < frame.characters.Length; ++i)
                        HandleCharacter(frame.characters[i], ref text, ref state, _language);
                }

                // While composing the IME owns the editing keys.
                if (composition == null)
                {
                    if (frame.escapePressed)
                        NowUIFocus.Clear();

                    if (frame.undoPressed)
                        Undo(cache, ref text, ref state);

                    if (frame.redoPressed)
                        Redo(cache, ref text, ref state);

                    if (frame.selectAllPressed)
                        NowTextEdit.SelectAll(ref state, text);

                    // Copy/cut with no selection act on the whole line, like an IDE.
                    if (frame.copyPressed)
                        NowUIClipboard.Copy(state.hasSelection
                            ? NowTextEdit.GetSelection(text, state)
                            : CurrentLine(text, state.caret));

                    if (frame.cutPressed)
                    {
                        PushUndo(cache, text, in state, typing: false);

                        if (state.hasSelection)
                        {
                            NowUIClipboard.Copy(NowTextEdit.GetSelection(text, state));
                            NowTextEdit.DeleteSelection(ref text, ref state);
                        }
                        else
                        {
                            CutLine(ref text, ref state);
                        }
                    }

                    if (frame.duplicatePressed)
                    {
                        PushUndo(cache, text, in state, typing: false);
                        DuplicateLines(ref text, ref state);
                    }

                    if (frame.pastePressed)
                    {
                        string buffer = NowUIClipboard.Paste();

                        if (!string.IsNullOrEmpty(buffer))
                        {
                            PushUndo(cache, text, in state, typing: false);
                            NowTextEdit.Insert(ref text, ref state, buffer.Replace("\r\n", "\n").Replace('\r', '\n'));
                        }
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "enter"), frame.enterHeld))
                    {
                        PushUndo(cache, text, in state, typing: true);
                        InsertNewlineWithIndent(ref text, ref state, _language);
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "tab"), frame.tabHeld))
                    {
                        if (!ReferenceEquals(cache.text, text))
                            Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

                        PushUndo(cache, text, in state, typing: true);
                        HandleTab(ref text, ref state, cache, frame.shift);
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "bs"), frame.backspaceHeld))
                    {
                        PushUndo(cache, text, in state, typing: true);

                        if (!PairBackspace(ref text, ref state, _language))
                            NowTextEdit.Backspace(ref text, ref state, frame.command);
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "del"), frame.deleteHeld))
                    {
                        PushUndo(cache, text, in state, typing: true);
                        NowTextEdit.Delete(ref text, ref state, frame.command);
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "left"), frame.leftHeld))
                        NowTextEdit.MoveCaret(ref state, text, -1, frame.shift, frame.command);

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "right"), frame.rightHeld))
                        NowTextEdit.MoveCaret(ref state, text, 1, frame.shift, frame.command);

                    if (!ReferenceEquals(cache.text, text))
                        Rebuild(cache, text, font, _fontSize, textStyle.fontStyle);

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "up"), frame.upHeld))
                    {
                        MoveVertical(ref state, ref editor, text, cache, font, _fontSize, textStyle.fontStyle, -1, frame.shift);
                        verticalMove = true;
                    }

                    if (NowUIControlState.Repeat(NowUIInput.GetId(id, "down"), frame.downHeld))
                    {
                        MoveVertical(ref state, ref editor, text, cache, font, _fontSize, textStyle.fontStyle, 1, frame.shift);
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
                            SmartHome(text, cache, ref state, frame.shift);
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

            if (state.caret != editor.lastCaret || result.changed || interaction.pressed)
            {
                editor.lastCaret = state.caret;
                editor.blinkAnchor = Time.realtimeSinceStartup;

                if (!verticalMove)
                    editor.goalX = caretX;
            }

            float contentHeight = cache.lines.Count * lineHeight;
            float maxScrollY = Mathf.Max(0f, contentHeight - textRect.height);
            float maxScrollX = Mathf.Max(0f, cache.contentWidth + 24f - textRect.width);

            if (!NowUIInput.isPassive && interaction.hovered)
            {
                Vector2 wheel = NowUIInput.current.scrollDelta;

                if (wheel != Vector2.zero)
                {
                    editor.scrollY -= wheel.y * lineHeight * 2f;
                    editor.scrollX += wheel.x * lineHeight * 2f;
                    NowUIControlState.RequestRepaint();
                }
            }

            if (focused)
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

            if (focused && !NowUIInput.isPassive)
                NowUITextInput.setCompositionCursor?.Invoke(new Vector2(
                    textRect.x + caretX - editor.scrollX,
                    textRect.y + caretLine * lineHeight - editor.scrollY + lineHeight));

            DrawVisuals(theme, textStyle, font, rect, textRect, gutterWidth, statusHeight, lineHeight,
                text, cache, in state, ref editor, focused, composition, caretLine, caretX,
                interaction.pointerPosition, interaction.hovered);

            if (focused)
                NowUIControlState.RequestRepaint();

            return result;
        }

        void DrawVisuals(NowUITheme theme, NowUIText textStyle, NowFontAsset font, NowRect rect, NowRect textRect,
            float gutterWidth, float statusHeight, float lineHeight, string text, EditorCache cache,
            in NowTextEditState state, ref EditorState editor, bool focused, string composition,
            int caretLine, float caretX, Vector2 pointer, bool hovered)
        {
            float fontSize = _fontSize;
            var fontStyle = textStyle.fontStyle;

            Vector4 cornerRadius = theme.Rectangle(rect, NowRectangleStyle.Outline).radius;

            theme.Rectangle(rect, NowRectangleStyle.Surface).SetRadius(cornerRadius).Draw();

            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(editor.scrollY / lineHeight));
            int lastVisible = Mathf.Min(cache.lines.Count - 1, Mathf.CeilToInt((editor.scrollY + textRect.height) / lineHeight));

            if (gutterWidth > 0f)
            {
                float gutterBottomLeft = statusHeight > 0f ? 0f : cornerRadius.z;
                Now.Rectangle(new NowRect(rect.x, rect.y, gutterWidth, rect.height - statusHeight))
                    .SetColor(theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f)))
                    .SetRadius(Corners(cornerRadius.w, 0f, 0f, gutterBottomLeft))
                    .Draw();

                using (Now.Mask(new NowRect(rect.x, textRect.y, gutterWidth, textRect.height)))
                {
                    var numberStyle = theme.Text(default, NowTextStyle.Muted).SetFontSize(fontSize - 2f);

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
                    Color lineTint = theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f));
                    lineTint.a *= 0.6f;
                    Now.Rectangle(new NowRect(textRect.x, textRect.y + caretLine * lineHeight - editor.scrollY, textRect.width, lineHeight))
                        .SetColor(lineTint)
                        .Draw();
                }

                if (focused && state.hasSelection && composition == null)
                    DrawSelection(theme, text, cache, font, fontSize, fontStyle, textRect, lineHeight, in state, ref editor, firstVisible, lastVisible);

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
                                x += DrawSegment(textStyle, theme, text, segmentStart, segmentEnd - segmentStart,
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
                            x += DrawSegment(textStyle, theme, text, segmentStart, segmentEnd - segmentStart,
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
                            .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                            .Draw();
                    }
                }

                DrawSquiggles(text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor, firstVisible, lastVisible);

                if (focused && NowUIControlState.Blink(1f, editor.blinkAnchor))
                {
                    Now.Rectangle(new NowRect(
                            originX + caretX,
                            textRect.y + caretLine * lineHeight - editor.scrollY,
                            2f,
                            lineHeight))
                        .SetColor(theme.GetColor(NowColorToken.Text, Color.black))
                        .Draw();
                }
            }

            if (statusHeight > 0f)
                DrawStatusBar(theme, font, fontSize, fontStyle, rect, statusHeight, text, cache, in state, caretLine, cornerRadius);

            DrawScrollbars(theme, cache, rect, textRect, statusHeight, lineHeight, ref editor, hovered || focused);

            // The border is drawn last with a transparent fill, so it covers
            // every seam between the gutter, body, status bar and scrollbars
            // with one clean rounded outline.
            var border = theme.Rectangle(rect, NowRectangleStyle.Outline);
            border.color = new Vector4(0f, 0f, 0f, 0f);
            border.SetRadius(cornerRadius);
            border.outline = focused ? 2f : 1f;
            border.outlineColor = focused
                ? theme.GetColor(NowColorToken.Accent, Color.blue)
                : theme.GetColor(NowColorToken.Border, Color.gray);
            border.Draw();

            if (hovered)
                DrawDiagnosticTooltip(theme, text, cache, font, fontSize, fontStyle, textRect, lineHeight, ref editor, pointer);
        }

        static Vector4 Corners(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            // The rounded-rect SDF packs corners as (BR, TR, BL, TL).
            return new Vector4(bottomRight, topRight, bottomLeft, topLeft);
        }

        static void Scrollbars(EditorCache cache, NowRect rect, NowRect textRect, float statusHeight, float lineHeight,
            in EditorState editor, out float maxScrollX, out float maxScrollY,
            out NowRect vTrack, out NowRect vThumb, out NowRect hTrack, out NowRect hThumb)
        {
            float contentHeight = cache.lines.Count * lineHeight;
            float contentWidth = cache.contentWidth + 24f;
            maxScrollY = Mathf.Max(0f, contentHeight - textRect.height);
            maxScrollX = Mathf.Max(0f, contentWidth - textRect.width);

            vTrack = new NowRect(rect.xMax - ScrollbarThickness - 3f, textRect.y, ScrollbarThickness, textRect.height);
            float vThumbHeight = maxScrollY > 0f ? Mathf.Max(28f, vTrack.height * (textRect.height / contentHeight)) : 0f;
            float vNormalized = maxScrollY > 0f ? Mathf.Clamp01(editor.scrollY / maxScrollY) : 0f;
            vThumb = new NowRect(vTrack.x, vTrack.y + (vTrack.height - vThumbHeight) * vNormalized, ScrollbarThickness, vThumbHeight);

            hTrack = new NowRect(textRect.x, rect.yMax - statusHeight - ScrollbarThickness - 3f, textRect.width, ScrollbarThickness);
            float hThumbWidth = maxScrollX > 0f ? Mathf.Max(28f, hTrack.width * (textRect.width / contentWidth)) : 0f;
            float hNormalized = maxScrollX > 0f ? Mathf.Clamp01(editor.scrollX / maxScrollX) : 0f;
            hThumb = new NowRect(hTrack.x + (hTrack.width - hThumbWidth) * hNormalized, hTrack.y, hThumbWidth, ScrollbarThickness);
        }

        void DrawScrollbars(NowUITheme theme, EditorCache cache, NowRect rect, NowRect textRect, float statusHeight,
            float lineHeight, ref EditorState editor, bool active)
        {
            Scrollbars(cache, rect, textRect, statusHeight, lineHeight, editor,
                out float maxScrollX, out float maxScrollY, out var vTrack, out var vThumb, out var hTrack, out var hThumb);

            Color trackColor = theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f));
            trackColor.a *= 0.5f;
            Color thumbColor = theme.GetColor(NowColorToken.Border, Color.gray);
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

        float DrawSegment(NowUIText textStyle, NowUITheme theme, string text, int start, int length,
            NowCodeTokenKind kind, NowFontAsset font, float fontSize, NowFontStyle fontStyle,
            float x, float y, float lineHeight)
        {
            float width = Advance(text, font, fontSize, fontStyle, start, length);
            var style = textStyle;
            style.rect = new NowRect(x, y, width + 4f, lineHeight);
            style.color = KindColor(theme, kind);
            style.Draw(System.MemoryExtensions.AsSpan(text, start, length));
            return width;
        }

        static Vector4 KindColor(NowUITheme theme, NowCodeTokenKind kind)
        {
            switch (kind)
            {
                case NowCodeTokenKind.Property:
                case NowCodeTokenKind.Heading:
                case NowCodeTokenKind.Link:
                case NowCodeTokenKind.ListMarker:
                    return theme.GetColor(NowColorToken.Accent, Color.blue);
                case NowCodeTokenKind.String:
                case NowCodeTokenKind.CodeSpan:
                    return new Vector4(0.16f, 0.52f, 0.26f, 1f);
                case NowCodeTokenKind.Number:
                case NowCodeTokenKind.Emphasis:
                    return new Vector4(0.55f, 0.27f, 0.68f, 1f);
                case NowCodeTokenKind.Keyword:
                case NowCodeTokenKind.Strong:
                    return new Vector4(0.80f, 0.42f, 0.13f, 1f);
                case NowCodeTokenKind.Comment:
                case NowCodeTokenKind.Punctuation:
                case NowCodeTokenKind.Quote:
                case NowCodeTokenKind.Fence:
                    return theme.GetColor(NowColorToken.TextMuted, Color.gray);
                case NowCodeTokenKind.Error:
                    return new Vector4(0.86f, 0.24f, 0.24f, 1f);
                default:
                    return theme.GetColor(NowColorToken.Text, Color.black);
            }
        }

        void DrawSelection(NowUITheme theme, string text, EditorCache cache, NowFontAsset font, float fontSize,
            NowFontStyle fontStyle, NowRect textRect, float lineHeight, in NowTextEditState state,
            ref EditorState editor, int firstVisible, int lastVisible)
        {
            Color highlight = theme.GetColor(NowColorToken.Accent, Color.blue);
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

        void DrawStatusBar(NowUITheme theme, NowFontAsset font, float fontSize, NowFontStyle fontStyle,
            NowRect rect, float statusHeight, string text, EditorCache cache, in NowTextEditState state, int caretLine,
            Vector4 cornerRadius)
        {
            var statusRect = new NowRect(rect.x, rect.yMax - statusHeight, rect.width, statusHeight);

            Now.Rectangle(statusRect)
                .SetColor(theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.95f, 0.96f, 0.97f, 1f)))
                .SetRadius(Corners(0f, 0f, cornerRadius.x, cornerRadius.z))
                .Draw();

            var line = cache.lines[caretLine];
            int column = state.caret - line.start;

            if (cache.positionLine != caretLine || cache.positionColumn != column)
            {
                cache.positionLine = caretLine;
                cache.positionColumn = column;
                cache.positionText = $"Ln {caretLine + 1}, Col {column + 1}";
            }

            var positionStyle = theme.Text(
                new NowRect(statusRect.x + 8f, statusRect.y, 220f, statusRect.height), NowTextStyle.Muted);
            positionStyle.SetFontSize(11f).Draw(cache.positionText);

            string message = cache.statusMessage ?? string.Empty;
            Vector4 messageColor = cache.diagnostics.Count == 0
                ? theme.GetColor(NowColorToken.TextMuted, Color.gray)
                : new Vector4(0.86f, 0.24f, 0.24f, 1f);

            float messageWidth = Advance(message, font, 11f, fontStyle);
            var messageRect = new NowRect(statusRect.xMax - messageWidth - 12f, statusRect.y, messageWidth + 8f, statusRect.height);

            var messageStyle = theme.Text(messageRect, NowTextStyle.Muted);
            messageStyle.color = messageColor;
            messageStyle.SetFontSize(11f).Draw(message);
        }

        void DrawDiagnosticTooltip(NowUITheme theme, string text, EditorCache cache, NowFontAsset font,
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

                string message = diagnostic.message;
                float width = Advance(message, font, 12f, fontStyle) + 16f;
                var tooltipRect = new NowRect(pointer.x + 12f, pointer.y + 18f, width, 24f);

                NowUIOverlay.Defer(default, () =>
                {
                    var background = NowControls.theme.Rectangle(tooltipRect, NowRectangleStyle.Surface);
                    background.outline = 1f;
                    background.outlineColor = NowControls.theme.GetColor(NowColorToken.Border, Color.gray);
                    background.SetRadius(4f).Draw();

                    var tooltipStyle = NowControls.theme.Text(
                        new NowRect(tooltipRect.x + 8f, tooltipRect.y, tooltipRect.width, tooltipRect.height),
                        NowTextStyle.Body);
                    tooltipStyle.SetFontSize(12f).Draw(message);
                });
                return;
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
                cache.redo.Clear();
            }

            return cache;
        }

        static void Rebuild(EditorCache cache, string text, NowFontAsset font, float fontSize, NowFontStyle fontStyle)
        {
            cache.text = text;
            cache.lines.Clear();
            cache.lineStates.Clear();
            cache.diagnostics.Clear();
            cache.contentWidth = 0f;

            int lineStart = 0;

            for (int i = 0; i <= text.Length; ++i)
            {
                if (i < text.Length && text[i] != '\n')
                    continue;

                cache.lines.Add(new LineSpan { start = lineStart, length = i - lineStart });
                lineStart = i + 1;
            }

            int state = 0;

            for (int i = 0; i < cache.lines.Count; ++i)
            {
                cache.lineStates.Add(state);
                _tokenScratch.Clear();
                state = cache.language.TokenizeLine(text, cache.lines[i].start, cache.lines[i].length, state, _tokenScratch);

                float width = Advance(text, font, fontSize, fontStyle, cache.lines[i].start, cache.lines[i].length);

                if (width > cache.contentWidth)
                    cache.contentWidth = width;
            }

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
            return count <= 0 ? 0f : font.MeasureText(text, start, count, fontSize, style).x;
        }

        static float Advance(string text, NowFontAsset font, float fontSize, NowFontStyle style)
        {
            return Advance(text, font, fontSize, style, 0, text.Length);
        }

        static int HitIndex(string text, in LineSpan line, NowFontAsset font, float fontSize, NowFontStyle style, float x)
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

        static int HitTest(string text, EditorCache cache, NowFontAsset font, float fontSize, NowFontStyle style,
            Vector2 pointer, NowRect textRect, float lineHeight, float scrollX, float scrollY)
        {
            float localY = pointer.y - textRect.y + scrollY;
            int lineIndex = Mathf.Clamp(Mathf.FloorToInt(localY / lineHeight), 0, cache.lines.Count - 1);
            return HitIndex(text, cache.lines[lineIndex], font, fontSize, style, pointer.x - textRect.x + scrollX);
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
            {
                state.caret = 0;
            }
            else if (target >= cache.lines.Count)
            {
                state.caret = text.Length;
            }
            else
            {
                state.caret = HitIndex(text, cache.lines[target], font, fontSize, style, editor.goalX);
            }

            if (!select)
                state.anchor = state.caret;
        }

        /// <summary>The newline-delimited line containing <paramref name="index"/> (newline excluded).</summary>
        static void LineBounds(string text, int index, out int start, out int end)
        {
            start = Mathf.Clamp(index, 0, text.Length);

            while (start > 0 && text[start - 1] != '\n')
                --start;

            end = Mathf.Clamp(index, 0, text.Length);

            while (end < text.Length && text[end] != '\n')
                ++end;
        }

        static string CurrentLine(string text, int caret)
        {
            LineBounds(text, caret, out int start, out int end);
            return text.Substring(start, end - start) + "\n";
        }

        static void CutLine(ref string text, ref NowTextEditState state)
        {
            LineBounds(text, state.caret, out int start, out int end);
            NowUIClipboard.Copy(text.Substring(start, end - start) + "\n");

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
            LineBounds(text, from, out int start, out _);
            LineBounds(text, to, out _, out int end);

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
                --lastLine;

            bool blockOperation = shift || (state.hasSelection && lastLine > firstLine);

            if (!blockOperation)
            {
                NowTextEdit.Insert(ref text, ref state, IndentUnit);
                return;
            }

            int delta = 0;
            int firstLineDelta = 0;

            for (int i = lastLine; i >= firstLine; --i)
            {
                int lineStart = cache.lines[i].start;

                if (shift)
                {
                    int remove = 0;

                    while (remove < IndentUnit.Length && lineStart + remove < text.Length && text[lineStart + remove] == ' ')
                        ++remove;

                    if (remove > 0)
                    {
                        text = text.Remove(lineStart, remove);
                        delta -= remove;

                        if (i == firstLine)
                            firstLineDelta = -remove;
                    }
                }
                else
                {
                    text = text.Insert(lineStart, IndentUnit);
                    delta += IndentUnit.Length;

                    if (i == firstLine)
                        firstLineDelta = IndentUnit.Length;
                }
            }

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

        static void PushUndo(EditorCache cache, string text, in NowTextEditState state, bool typing)
        {
            float now = Time.realtimeSinceStartup;

            if (typing && cache.lastWasTyping && now - cache.lastEditTime < 0.75f && cache.undo.Count > 0)
            {
                cache.lastEditTime = now;
                return;
            }

            cache.undo.Add(new UndoEntry { text = text, caret = state.caret, anchor = state.anchor });

            if (cache.undo.Count > 200)
                cache.undo.RemoveAt(0);

            cache.redo.Clear();
            cache.lastEditTime = now;
            cache.lastWasTyping = typing;
        }

        static void Undo(EditorCache cache, ref string text, ref NowTextEditState state)
        {
            if (cache.undo.Count == 0)
                return;

            cache.redo.Add(new UndoEntry { text = text, caret = state.caret, anchor = state.anchor });
            var entry = cache.undo[^1];
            cache.undo.RemoveAt(cache.undo.Count - 1);
            text = entry.text;
            state.caret = entry.caret;
            state.anchor = entry.anchor;
            cache.lastWasTyping = false;
        }

        static void Redo(EditorCache cache, ref string text, ref NowTextEditState state)
        {
            if (cache.redo.Count == 0)
                return;

            cache.undo.Add(new UndoEntry { text = text, caret = state.caret, anchor = state.anchor });
            var entry = cache.redo[^1];
            cache.redo.RemoveAt(cache.redo.Count - 1);
            text = entry.text;
            state.caret = entry.caret;
            state.anchor = entry.anchor;
            cache.lastWasTyping = false;
        }
    }
}
