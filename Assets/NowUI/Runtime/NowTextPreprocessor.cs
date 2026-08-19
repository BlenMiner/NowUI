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

        /// <summary>
        /// Context-aware variant of <see cref="NowTextPreprocessor"/>:
        /// <paramref name="context"/> is the innermost
        /// <see cref="TextContext"/> scope active where the string is drawn
        /// (null outside any scope), so the same source string can resolve
        /// differently per situation — "Play" in a menu vs on a radio.
        /// </summary>
        public delegate string NowContextTextPreprocessor(string value, string context);

        static NowTextPreprocessor _textPreprocessor;
        static NowContextTextPreprocessor _contextTextPreprocessor;

        static readonly Dictionary<(string context, string value), string> _textPreprocessorMemo =
            new Dictionary<(string, string), string>(256);

        static readonly List<string> _textContexts = new List<string>();

        static int _textPreprocessorRevision;

        /// <summary>
        /// Per-frame dynamic strings can flood the memo; past this size it
        /// resets and rebuilds, trading a burst of re-processing for a bound.
        /// </summary>
        const int TextPreprocessorMemoLimit = 8192;

        internal static int textPreprocessorRevision => _textPreprocessorRevision;

        /// <summary>
        /// The innermost active <see cref="TextContext"/> scope, or null.
        /// Captured when a string is preprocessed — in immediate mode that is
        /// during your draw code, so wrapping draws in a scope is enough.
        /// </summary>
        public static string currentTextContext =>
            _textContexts.Count > 0 ? _textContexts[_textContexts.Count - 1] : null;

        /// <summary>
        /// Scopes the strings drawn inside it to a named situation for the
        /// context-aware text preprocessor:
        /// <code>
        /// using (Now.TextContext("radio"))
        ///     NowLayout.Label("Play").Draw();
        /// </code>
        /// Scopes nest; the innermost wins. Memoization is keyed per
        /// (context, value), so the same string may resolve differently in
        /// different scopes.
        /// </summary>
        public static TextContextScope TextContext(string context)
        {
            _textContexts.Add(context);
            return new TextContextScope();
        }

        public readonly struct TextContextScope : IDisposable
        {
            public void Dispose()
            {
                if (_textContexts.Count > 0)
                    _textContexts.RemoveAt(_textContexts.Count - 1);
            }
        }

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
            _contextTextPreprocessor = null;
            InvalidateTextPreprocessor();
        }

        /// <summary>
        /// Registers a context-aware hook: like
        /// <see cref="SetTextPreprocessor(NowTextPreprocessor)"/>, but the
        /// hook also receives the innermost <see cref="TextContext"/> scope
        /// active where the string is drawn. Passing null unregisters.
        /// </summary>
        public static void SetTextPreprocessor(NowContextTextPreprocessor preprocessor)
        {
            _contextTextPreprocessor = preprocessor;
            _textPreprocessor = null;
            InvalidateTextPreprocessor();
        }

        /// <summary>Unregisters the text preprocessor; strings render verbatim again.</summary>
        public static void ClearTextPreprocessor()
        {
            SetTextPreprocessor((NowTextPreprocessor)null);
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
            if (string.IsNullOrEmpty(value))
                return value;

            if (_textPreprocessor == null && _contextTextPreprocessor == null)
                return value;

            // Context only participates when the hook can see it, so legacy
            // hooks keep their exact pre-context behavior and memo footprint.
            var context = _contextTextPreprocessor != null ? currentTextContext : null;

            if (_textPreprocessorMemo.TryGetValue((context, value), out string resolved))
                return resolved;

            try
            {
                resolved = _contextTextPreprocessor != null
                    ? _contextTextPreprocessor(value, context) ?? value
                    : _textPreprocessor(value) ?? value;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                resolved = value;
            }

            if (_textPreprocessorMemo.Count >= TextPreprocessorMemoLimit)
                _textPreprocessorMemo.Clear();

            _textPreprocessorMemo[(context, value)] = resolved;
            return resolved;
        }
    }
}
