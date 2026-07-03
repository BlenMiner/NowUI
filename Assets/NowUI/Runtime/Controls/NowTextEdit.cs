using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Caret and selection state for a single-line editor. The caller owns the
    /// text; this travels alongside it (typically in <see cref="NowControlState"/>).
    /// </summary>
    public struct NowTextEditState
    {
        /// <summary>Caret position as a UTF-16 index into the text.</summary>
        public int caret;

        /// <summary>Selection anchor; equals <see cref="caret"/> when nothing is selected.</summary>
        public int anchor;

        /// <summary>Horizontal scroll of the field's content, in pixels.</summary>
        public float scrollX;

        public bool hasSelection => caret != anchor;

        public int selectionMin => Mathf.Min(caret, anchor);

        public int selectionMax => Mathf.Max(caret, anchor);
    }

    internal enum NowTextSelectionGranularity : byte
    {
        Character,
        Word,
        Line
    }

    internal struct NowTextSelectionGesture
    {
        public NowTextSelectionGranularity granularity;

        public int originStart;

        public int originEnd;
    }

    /// <summary>
    /// Pure single-line text editing logic — no rendering, no input polling, fully
    /// testable headless and reusable by custom editors. Indices are UTF-16;
    /// movement and deletion are codepoint-aware (surrogate pairs never split).
    /// </summary>
    public static class NowTextEdit
    {
        public static void Clamp(ref NowTextEditState state, string text)
        {
            int length = text?.Length ?? 0;
            state.caret = Mathf.Clamp(state.caret, 0, length);
            state.anchor = Mathf.Clamp(state.anchor, 0, length);
        }

        /// <summary>Next codepoint boundary after <paramref name="index"/>.</summary>
        public static int NextIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index >= text.Length)
                return text?.Length ?? 0;

            ++index;

            if (index < text.Length && char.IsLowSurrogate(text[index]))
                ++index;

            return index;
        }

        /// <summary>Previous codepoint boundary before <paramref name="index"/>.</summary>
        public static int PrevIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
                return 0;

            --index;

            if (index > 0 && char.IsLowSurrogate(text[index]))
                --index;

            return index;
        }

        static int NextWord(string text, int index)
        {
            int length = text.Length;

            while (index < length && char.IsWhiteSpace(text[index]))
                index = NextIndex(text, index);

            while (index < length && !char.IsWhiteSpace(text[index]))
                index = NextIndex(text, index);

            return index;
        }

        static int PrevWord(string text, int index)
        {
            while (index > 0 && char.IsWhiteSpace(text[index - 1]))
                index = PrevIndex(text, index);

            while (index > 0 && !char.IsWhiteSpace(text[index - 1]))
                index = PrevIndex(text, index);

            return index;
        }

        static bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        static void WordBounds(string text, int index, out int start, out int end)
        {
            text ??= string.Empty;
            int length = text.Length;
            int caret = Mathf.Clamp(index, 0, length);
            start = caret;
            end = caret;

            if (length == 0)
                return;

            int at;

            if (caret < length && IsWordCharacter(text[caret]))
            {
                at = caret;
            }
            else if (caret > 0 && IsWordCharacter(text[caret - 1]))
            {
                at = PrevIndex(text, caret);
            }
            else if (caret < length)
            {
                at = caret;
            }
            else
            {
                at = PrevIndex(text, caret);
            }

            if (char.IsWhiteSpace(text[at]))
            {
                start = at;

                while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                    start = PrevIndex(text, start);

                end = at;

                while (end < length && char.IsWhiteSpace(text[end]))
                    end = NextIndex(text, end);

                return;
            }

            if (IsWordCharacter(text[at]))
            {
                start = at;

                while (start > 0 && IsWordCharacter(text[start - 1]))
                    start = PrevIndex(text, start);

                end = at;

                while (end < length && IsWordCharacter(text[end]))
                    end = NextIndex(text, end);

                return;
            }

            start = at;
            end = NextIndex(text, at);
        }

        internal static void SelectionBounds(string text, NowTextSelectionGranularity granularity, int index, out int start, out int end)
        {
            switch (granularity)
            {
                case NowTextSelectionGranularity.Word:
                    WordBounds(text, index, out start, out end);
                    return;
                case NowTextSelectionGranularity.Line:
                    NowTextMetrics.LineBounds(text, index, out start, out end);
                    return;
                default:
                    text ??= string.Empty;
                    start = Mathf.Clamp(index, 0, text.Length);
                    end = start;
                    return;
            }
        }

        internal static void BeginSelectionGesture(
            ref NowTextSelectionGesture gesture,
            NowTextSelectionGranularity granularity,
            in NowTextEditState state)
        {
            gesture.granularity = granularity;
            gesture.originStart = state.selectionMin;
            gesture.originEnd = state.selectionMax;
        }

        internal static void DragSelectionGesture(
            ref NowTextEditState state,
            string text,
            in NowTextSelectionGesture gesture,
            int index)
        {
            text ??= string.Empty;

            if (gesture.granularity == NowTextSelectionGranularity.Character)
            {
                state.anchor = Mathf.Clamp(gesture.originStart, 0, text.Length);
                state.caret = Mathf.Clamp(index, 0, text.Length);
                return;
            }

            int originStart = Mathf.Clamp(Mathf.Min(gesture.originStart, gesture.originEnd), 0, text.Length);
            int originEnd = Mathf.Clamp(Mathf.Max(gesture.originStart, gesture.originEnd), originStart, text.Length);
            SelectionBounds(text, gesture.granularity, index, out int targetStart, out int targetEnd);

            if (targetStart < originStart)
            {
                state.anchor = originEnd;
                state.caret = targetStart;
            }
            else
            {
                state.anchor = originStart;
                state.caret = targetEnd;
            }
        }

        /// <summary>Inserts (replacing any selection); returns true when the text changed.</summary>
        public static bool Insert(ref string text, ref NowTextEditState state, string characters)
        {
            if (string.IsNullOrEmpty(characters))
                return false;

            text ??= string.Empty;
            Clamp(ref state, text);
            DeleteSelection(ref text, ref state);

            text = text.Insert(state.caret, characters);
            state.caret += characters.Length;
            state.anchor = state.caret;
            return true;
        }

        /// <summary>Deletes backward — one codepoint, or a whole word when <paramref name="word"/> is set.</summary>
        public static bool Backspace(ref string text, ref NowTextEditState state, bool word = false)
        {
            text ??= string.Empty;
            Clamp(ref state, text);

            if (DeleteSelection(ref text, ref state))
                return true;

            if (state.caret <= 0)
                return false;

            int previous = word ? PrevWord(text, state.caret) : PrevIndex(text, state.caret);
            text = text.Remove(previous, state.caret - previous);
            state.caret = previous;
            state.anchor = previous;
            return true;
        }

        /// <summary>Deletes forward — one codepoint, or a whole word when <paramref name="word"/> is set.</summary>
        public static bool Delete(ref string text, ref NowTextEditState state, bool word = false)
        {
            text ??= string.Empty;
            Clamp(ref state, text);

            if (DeleteSelection(ref text, ref state))
                return true;

            if (state.caret >= text.Length)
                return false;

            int next = word ? NextWord(text, state.caret) : NextIndex(text, state.caret);
            text = text.Remove(state.caret, next - state.caret);
            state.anchor = state.caret;
            return true;
        }

        /// <summary>
        /// Deletes from the start of the caret's hard line to the caret
        /// (macOS Cmd+Backspace). A selection is deleted instead when present.
        /// </summary>
        public static bool DeleteToLineStart(ref string text, ref NowTextEditState state)
        {
            text ??= string.Empty;
            Clamp(ref state, text);

            if (DeleteSelection(ref text, ref state))
                return true;

            int start = state.caret;

            while (start > 0 && text[start - 1] != '\n')
                --start;

            if (start == state.caret)
                return false;

            text = text.Remove(start, state.caret - start);
            state.caret = start;
            state.anchor = start;
            return true;
        }

        /// <summary>
        /// Moves the caret one codepoint (or word) left/right. Without
        /// <paramref name="select"/>, an existing selection collapses to its edge
        /// instead of moving, matching platform conventions.
        /// </summary>
        public static void MoveCaret(ref NowTextEditState state, string text, int direction, bool select, bool word = false)
        {
            text ??= string.Empty;
            Clamp(ref state, text);

            if (!select && state.hasSelection)
            {
                state.caret = direction < 0 ? state.selectionMin : state.selectionMax;
                state.anchor = state.caret;
                return;
            }

            state.caret = direction < 0
                ? (word ? PrevWord(text, state.caret) : PrevIndex(text, state.caret))
                : (word ? NextWord(text, state.caret) : NextIndex(text, state.caret));

            if (!select)
                state.anchor = state.caret;
        }

        public static void MoveHome(ref NowTextEditState state, bool select)
        {
            state.caret = 0;

            if (!select)
                state.anchor = 0;
        }

        public static void MoveEnd(ref NowTextEditState state, string text, bool select)
        {
            state.caret = text?.Length ?? 0;

            if (!select)
                state.anchor = state.caret;
        }

        public static void SelectAll(ref NowTextEditState state, string text)
        {
            state.anchor = 0;
            state.caret = text?.Length ?? 0;
        }

        /// <summary>Selects the word, whitespace run or single punctuation mark containing <paramref name="index"/>.</summary>
        public static void SelectWord(ref NowTextEditState state, string text, int index)
        {
            text ??= string.Empty;
            WordBounds(text, index, out int start, out int end);
            state.anchor = start;
            state.caret = end;
        }

        /// <summary>Selects the hard line (newline-delimited) containing <paramref name="index"/>, without the newline.</summary>
        public static void SelectLine(ref NowTextEditState state, string text, int index)
        {
            text ??= string.Empty;
            int at = Mathf.Clamp(index, 0, text.Length);
            int start = at;

            while (start > 0 && text[start - 1] != '\n')
                --start;

            int end = at;

            while (end < text.Length && text[end] != '\n')
                ++end;

            state.anchor = start;
            state.caret = end;
        }

        public static string GetSelection(string text, in NowTextEditState state)
        {
            if (string.IsNullOrEmpty(text) || !state.hasSelection)
                return string.Empty;

            int min = Mathf.Clamp(state.selectionMin, 0, text.Length);
            int max = Mathf.Clamp(state.selectionMax, 0, text.Length);
            return text.Substring(min, max - min);
        }

        public static bool DeleteSelection(ref string text, ref NowTextEditState state)
        {
            if (text == null || !state.hasSelection)
                return false;

            Clamp(ref state, text);
            int min = state.selectionMin;
            int max = state.selectionMax;
            text = text.Remove(min, max - min);
            state.caret = min;
            state.anchor = min;
            return true;
        }
    }
}
