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
        Fence,
        Tag,
        Attribute,
        Constant,
        DocComment,
        DocTag
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

    /// <summary>
    /// A language-specific edit produced from one typed character. The edit
    /// replaces text around the caret, then places the caret inside or after
    /// the inserted text.
    /// </summary>
    public struct NowCodeCompletion
    {
        public int removeBeforeCaret;

        public int removeAfterCaret;

        public string text;

        public int caretOffset;

        public NowCodeCompletion(string text, int caretOffset, int removeBeforeCaret = 0, int removeAfterCaret = 0)
        {
            this.text = text;
            this.caretOffset = caretOffset;
            this.removeBeforeCaret = removeBeforeCaret;
            this.removeAfterCaret = removeAfterCaret;
        }
    }

    /// <summary>
    /// One completion candidate: the label is shown and prefix-matched against
    /// the word being typed, the insert text (label when empty) replaces the
    /// word on accept, and the detail renders as a muted right-aligned hint.
    /// </summary>
    public struct NowCodeCompletionItem
    {
        public string label;

        public string insertText;

        public string detail;
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
    public abstract class NowCodeLanguage : INowTextSyntaxProfile
    {
        static readonly NowCodeAutoPair[] DefaultPairs =
        {
            new NowCodeAutoPair('{', '}'),
            new NowCodeAutoPair('[', ']'),
            new NowCodeAutoPair('(', ')'),
            new NowCodeAutoPair('"', '"'),
        };

        static readonly List<NowCodeToken> TokenAdapterScratch = new List<NowCodeToken>(32);

        static readonly List<NowCodeDiagnostic> DiagnosticAdapterScratch = new List<NowCodeDiagnostic>(4);

        static readonly string[] NoAliases = System.Array.Empty<string>();

        /// <summary>Registry key and status-bar label, e.g. "json".</summary>
        public abstract string name { get; }

        /// <summary>Optional alternate registry keys, e.g. "md" or "nowui".</summary>
        public virtual IReadOnlyList<string> aliases => NoAliases;

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

        /// <summary>
        /// Lets a language consume a typed character with a richer edit than
        /// simple pair insertion. Tag languages use this for closing-tag and
        /// self-closing completions.
        /// </summary>
        public virtual bool TryComplete(char c, string text, in NowTextEditState state, out NowCodeCompletion completion)
        {
            completion = default;
            return false;
        }

        /// <summary>
        /// Fills completion candidates for the caret position and reports where
        /// the word being completed starts — accepted items replace
        /// [replaceStart, caret). The editor opens the popup when this returns
        /// candidates and filters them locally as the user keeps typing, so
        /// languages only get queried when a word or trigger begins. Default: none.
        /// </summary>
        public virtual bool TryGetCompletions(string text, int caret, List<NowCodeCompletionItem> items, out int replaceStart)
        {
            replaceStart = caret;
            return false;
        }

        /// <summary>Characters that open completions the moment they are typed ('.' in C#).</summary>
        public virtual bool IsCompletionTrigger(char c) => false;

        /// <summary>Line comment prefix ("//" in C#), or null when the language has none — enables toggle-comment.</summary>
        public virtual string lineCommentPrefix => null;

        /// <summary>
        /// One-line quick-info for the symbol at a position — shown in the
        /// hover tooltip after a short dwell. Default: none.
        /// </summary>
        public virtual bool TryGetHoverInfo(string text, int position, out string info)
        {
            info = null;
            return false;
        }

        /// <summary>
        /// Every span referring to the same symbol as the identifier at a
        /// position (declaration included), in document order — powers rename.
        /// Token kind is ignored; spans carry positions only. Default: none.
        /// </summary>
        public virtual bool TryGetRenameSpans(string text, int position, List<NowCodeToken> spans)
        {
            return false;
        }

        /// <summary>
        /// Monotonic version for validators that finish asynchronously: bump it
        /// when fresh diagnostics are ready and the editor re-runs
        /// <see cref="Validate"/> without waiting for a text change.
        /// </summary>
        public virtual int validationVersion => 0;

        /// <summary>True while an async validation pass runs; the editor keeps repainting until it lands.</summary>
        public virtual bool validationPending => false;

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
            Register(NowPlainLanguage.instance);
            Register(NowJsonLanguage.instance);
            Register(NowMarkupCodeLanguage.instance);
            Register(NowMarkdownCodeLanguage.instance);
            Register(NowCSharpLanguage.instance);
        }

        /// <summary>Registers a profile so other languages (markdown fences) can find it by name.</summary>
        public static void Register(NowCodeLanguage language)
        {
            EnsureRegistry();

            if (language == null || string.IsNullOrEmpty(language.name) || _registry.ContainsKey(language.name))
                return;

            _registry[language.name] = language;
            _registered.Add(language);

            var aliases = language.aliases;

            for (int i = 0; aliases != null && i < aliases.Count; ++i)
            {
                string alias = aliases[i];

                if (!string.IsNullOrEmpty(alias) && !_registry.ContainsKey(alias))
                    _registry[alias] = language;
            }
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

        int INowTextSyntaxProfile.TokenizeLine(string text, int start, int length, int state, List<NowTextToken> tokens)
        {
            TokenAdapterScratch.Clear();
            int next = TokenizeLine(text, start, length, state, TokenAdapterScratch);

            for (int i = 0; i < TokenAdapterScratch.Count; ++i)
            {
                var token = TokenAdapterScratch[i];
                tokens.Add(new NowTextToken
                {
                    start = token.start,
                    length = token.length,
                    kind = (NowTextTokenKind)token.kind
                });
            }

            return next;
        }

        void INowTextSyntaxProfile.Validate(string text, List<NowTextDiagnostic> diagnostics)
        {
            DiagnosticAdapterScratch.Clear();
            Validate(text, DiagnosticAdapterScratch);

            for (int i = 0; i < DiagnosticAdapterScratch.Count; ++i)
            {
                var diagnostic = DiagnosticAdapterScratch[i];
                diagnostics.Add(new NowTextDiagnostic
                {
                    start = diagnostic.start,
                    length = diagnostic.length,
                    message = diagnostic.message
                });
            }
        }
    }

    /// <summary>
    /// Plain-text profile: no highlighting, no diagnostics — every editor
    /// feature (editing, undo, search, line numbers) still works. Registered as
    /// "text" (alias "plain") so markdown fences can target it, and used as the
    /// fallback when <see cref="NowCode.Editor(NowCodeLanguage, NowId, string, int)"/>
    /// receives a null language.
    /// </summary>
    public sealed class NowPlainLanguage : NowCodeLanguage
    {
        public static readonly NowPlainLanguage instance = new NowPlainLanguage();

        static readonly string[] Aliases = { "plain", "txt" };

        public override string name => "text";

        public override IReadOnlyList<string> aliases => Aliases;

        public override int TokenizeLine(string text, int start, int length, int state, List<NowCodeToken> tokens)
        {
            return 0;
        }
    }
}
