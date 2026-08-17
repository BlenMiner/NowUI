using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public static partial class Now
    {
        /// <summary>
        /// Transforms one source string into the string that is measured and
        /// drawn (localization, terminology, casing). Runs for every unique
        /// visible string; results are memoized until
        /// <see cref="InvalidateTextPreprocessor"/>. Return the input (or null)
        /// to leave a string unchanged.
        /// </summary>
        public delegate string NowTextPreprocessor(string value);

        static NowTextPreprocessor _textPreprocessor;

        static readonly Dictionary<string, string> _textPreprocessorMemo =
            new Dictionary<string, string>(256, StringComparer.Ordinal);

        static int _textPreprocessorRevision;

        /// <summary>
        /// Per-frame dynamic strings can flood the memo; past this size it
        /// resets and rebuilds, trading a burst of re-processing for a bound.
        /// </summary>
        const int TextPreprocessorMemoLimit = 8192;

        internal static int textPreprocessorRevision => _textPreprocessorRevision;

        /// <summary>
        /// Registers a hook through which every UI string passes before it is
        /// measured or drawn — labels, buttons, menus, tooltips, wrapped text,
        /// rich text, and markdown all resolve through it, so layout always
        /// sizes the transformed text. Editable content (text fields, text
        /// areas) and numeric/span draws bypass it by design; opt any single
        /// draw out with <see cref="NowText.SetRaw"/>.
        /// <code>
        /// Now.SetTextPreprocessor(value => Localize(value));
        /// Now.InvalidateTextPreprocessor();  // after a language switch
        /// </code>
        /// Results are memoized per unique string, so the hook sees each new
        /// string once, not once per frame. Passing null unregisters.
        /// </summary>
        public static void SetTextPreprocessor(NowTextPreprocessor preprocessor)
        {
            _textPreprocessor = preprocessor;
            InvalidateTextPreprocessor();
        }

        /// <summary>Unregisters the text preprocessor; strings render verbatim again.</summary>
        public static void ClearTextPreprocessor()
        {
            SetTextPreprocessor(null);
        }

        /// <summary>
        /// Drops all memoized preprocessor results and repaints, so every
        /// string re-resolves through the hook. Call when its output changes —
        /// typically a language switch. There is no hidden change detection;
        /// this call is the only trigger.
        /// </summary>
        public static void InvalidateTextPreprocessor()
        {
            _textPreprocessorMemo.Clear();
            ++_textPreprocessorRevision;
            NowControlState.RequestRepaint();
        }

        /// <summary>
        /// Applies the registered text preprocessor to one string, memoized:
        /// the same source instance or content returns the same output instance
        /// until <see cref="InvalidateTextPreprocessor"/>, which keeps
        /// downstream shaping caches keyed by string warm. Identity when no
        /// preprocessor is registered. Useful directly when composing text that
        /// is later drawn as spans or through <see cref="NowText.SetRaw"/>.
        /// </summary>
        public static string PreprocessText(string value)
        {
            if (_textPreprocessor == null || string.IsNullOrEmpty(value))
                return value;

            if (_textPreprocessorMemo.TryGetValue(value, out string resolved))
                return resolved;

            try
            {
                resolved = _textPreprocessor(value) ?? value;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                resolved = value;
            }

            if (_textPreprocessorMemo.Count >= TextPreprocessorMemoLimit)
                _textPreprocessorMemo.Clear();

            _textPreprocessorMemo[value] = resolved;
            return resolved;
        }
    }
}
