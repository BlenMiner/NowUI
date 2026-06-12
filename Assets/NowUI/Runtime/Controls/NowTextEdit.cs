using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Caret and selection state for a single-line editor. The caller owns the
    /// text; this travels alongside it (typically in <see cref="NowUIControlState"/>).
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

        /// <summary>Selects the word (or whitespace run) containing <paramref name="index"/>.</summary>
        public static void SelectWord(ref NowTextEditState state, string text, int index)
        {
            text ??= string.Empty;
            state.caret = Mathf.Clamp(index, 0, text.Length);
            state.anchor = state.caret;

            if (text.Length == 0)
                return;

            int at = Mathf.Min(state.caret, text.Length - 1);

            if (char.IsWhiteSpace(text[at]))
            {
                int wsStart = at;

                while (wsStart > 0 && char.IsWhiteSpace(text[wsStart - 1]))
                    --wsStart;

                int wsEnd = at;

                while (wsEnd < text.Length && char.IsWhiteSpace(text[wsEnd]))
                    ++wsEnd;

                state.anchor = wsStart;
                state.caret = wsEnd;
                return;
            }

            int start = at;

            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
                start = PrevIndex(text, start);

            int end = at;

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
                end = NextIndex(text, end);

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
