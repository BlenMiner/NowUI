using System.Collections.Generic;

namespace NowUI.CodeEditor
{
    public enum NowCodeTokenKind : byte
    {
        Plain,
        Keyword,
        String,
        Number,
        Comment,
        Punctuation,
        Property,
        Error,
        Heading,
        Strong,
        Emphasis,
        CodeSpan,
        Link,
        Quote,
        ListMarker,
        Fence
    }

    /// <summary>One highlighted range within a line; ranges index into the full text.</summary>
    public struct NowCodeToken
    {
        public int start;

        public int length;

        public NowCodeTokenKind kind;
    }

    /// <summary>A validation problem: a range into the text plus a human message.</summary>
    public struct NowCodeDiagnostic
    {
        public int start;

        public int length;

        public string message;
    }

    /// <summary>A bracket/quote pair the editor auto-closes, skips over and wraps selections with.</summary>
    public struct NowCodeAutoPair
    {
        public char open;

        public char close;

        public NowCodeAutoPair(char open, char close)
        {
            this.open = open;
            this.close = close;
        }
    }

    /// <summary>
    /// A language profile for <see cref="NowCodeEditor"/>: line tokenization
    /// with an integer state carried across lines (so multi-line constructs
    /// highlight correctly), whole-text validation producing positioned
    /// diagnostics, auto-close pairs, and indentation hints. Profiles register
    /// by name so languages can embed each other (markdown fences delegate to
    /// the registered language of their info string).
    /// </summary>
    public abstract class NowCodeLanguage
    {
        static readonly NowCodeAutoPair[] DefaultPairs =
        {
            new NowCodeAutoPair('{', '}'),
            new NowCodeAutoPair('[', ']'),
            new NowCodeAutoPair('(', ')'),
            new NowCodeAutoPair('"', '"'),
        };

        /// <summary>Registry key and status-bar label, e.g. "json".</summary>
        public abstract string name { get; }

        /// <summary>
        /// Tokenizes one line (no trailing newline). <paramref name="state"/> is
        /// the value returned by the previous line (0 for the first); the return
        /// value carries into the next line. Tokens must be emitted in order and
        /// may be sparse — uncovered ranges render as plain text.
        /// </summary>
        public abstract int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens);

        /// <summary>Validates the whole text, appending diagnostics. Default: everything is fine.</summary>
        public virtual void Validate(string text, List<NowCodeDiagnostic> diagnostics)
        {
        }

        public virtual IReadOnlyList<NowCodeAutoPair> autoPairs => DefaultPairs;

        /// <summary>Characters that increase indentation when a newline follows them.</summary>
        public virtual bool IsIndentOpener(char c) => false;

        /// <summary>Characters that close an indentation level.</summary>
        public virtual bool IsIndentCloser(char c) => false;

        static Dictionary<string, NowCodeLanguage> _registry;

        static List<NowCodeLanguage> _registered;

        static void EnsureRegistry()
        {
            if (_registry != null)
                return;

            _registry = new Dictionary<string, NowCodeLanguage>(System.StringComparer.OrdinalIgnoreCase);
            _registered = new List<NowCodeLanguage>();
            Register(NowJsonLanguage.instance);
            Register(NowMarkdownCodeLanguage.instance);
        }

        /// <summary>Registers a profile so other languages (markdown fences) can find it by name.</summary>
        public static void Register(NowCodeLanguage language)
        {
            EnsureRegistry();

            if (language == null || _registry.ContainsKey(language.name))
                return;

            _registry[language.name] = language;
            _registered.Add(language);
        }

        public static NowCodeLanguage Find(string name)
        {
            EnsureRegistry();
            return !string.IsNullOrEmpty(name) && _registry.TryGetValue(name, out var language) ? language : null;
        }

        /// <summary>Stable index of a registered language, for packing into tokenizer states.</summary>
        internal static int IndexOf(NowCodeLanguage language)
        {
            EnsureRegistry();
            return _registered.IndexOf(language);
        }

        internal static NowCodeLanguage AtIndex(int index)
        {
            EnsureRegistry();
            return index >= 0 && index < _registered.Count ? _registered[index] : null;
        }
    }
}
