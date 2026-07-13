using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Markdown
{
    /// <summary>
    /// Everything an embed renderer needs to draw one fenced block live inside
    /// a rendered markdown document.
    /// </summary>
    public readonly struct NowMarkdownEmbedContext
    {
        /// <summary>
        /// Reserved rect: width is final; height comes from the current
        /// measure/draw cycle in an exact layout host or the previous measurement
        /// in a one-pass host.
        /// </summary>
        public readonly NowRect rect;

        /// <summary>Verbatim fence content.</summary>
        public readonly string source;

        /// <summary>Full fence info string (first word selected the renderer; the rest is renderer-defined).</summary>
        public readonly string info;

        /// <summary>Ordinal of this embed within its document, stable across frames.</summary>
        public readonly int index;

        /// <summary>Stable id of the parsed document instance hosting the embed.</summary>
        public readonly int documentId;

        internal NowMarkdownEmbedContext(NowRect rect, string source, string info, int index, int documentId)
        {
            this.rect = rect;
            this.source = source;
            this.info = info;
            this.index = index;
            this.documentId = documentId;
        }

        /// <summary>Identity unique to this embed instance — the key for any retained per-embed state.</summary>
        public int embedId => NowInput.CombineId(documentId, index);
    }

    /// <summary>
    /// Draws one embedded block during markdown drawing and returns the measured
    /// content height in UI units (0 while a first measurement is still
    /// settling). Runs live every frame, so interaction works; the reserved
    /// height converges one frame after the returned height changes.
    /// </summary>
    public delegate float NowMarkdownEmbedRenderer(in NowMarkdownEmbedContext context);

    /// <summary>
    /// Maps fenced-code info strings to live renderers, turning those fences
    /// into embedded content instead of highlighted code. Caller-owned: build
    /// one, keep it in a field, and pass it to
    /// <see cref="NowMarkdownBuilder.SetEmbeds(NowMarkdownEmbedSet)"/> — without
    /// one, every fence renders as a code block, so documents degrade
    /// gracefully wherever embeds are not wired up.
    /// <code>
    /// static readonly NowMarkdownEmbedSet Embeds = new NowMarkdownEmbedSet()
    ///     .Add("chart", DrawChartEmbed);
    ///
    /// NowMarkdown.Document(text).SetEmbeds(Embeds).Draw();
    /// </code>
    /// </summary>
    public class NowMarkdownEmbedSet
    {
        readonly Dictionary<string, NowMarkdownEmbedRenderer> _renderers =
            new Dictionary<string, NowMarkdownEmbedRenderer>(4, System.StringComparer.OrdinalIgnoreCase);

        int _version;

        internal int version => _version;

        /// <summary>
        /// Registers a renderer for fences whose info string starts with
        /// <paramref name="info"/> (case-insensitive); registering the same name
        /// again replaces the renderer.
        /// </summary>
        public NowMarkdownEmbedSet Add(string info, NowMarkdownEmbedRenderer renderer)
        {
            if (string.IsNullOrWhiteSpace(info))
                throw new System.ArgumentException("Embed info strings cannot be null or empty.", nameof(info));

            if (renderer == null)
                throw new System.ArgumentNullException(nameof(renderer));

            _renderers[info.Trim()] = renderer;
            ++_version;
            return this;
        }

        internal bool TryGet(string fenceInfo, out NowMarkdownEmbedRenderer renderer)
        {
            renderer = null;

            if (_renderers.Count == 0 || string.IsNullOrEmpty(fenceInfo))
                return false;

            int end = 0;

            while (end < fenceInfo.Length && !char.IsWhiteSpace(fenceInfo[end]))
                ++end;

            if (end == 0)
                return false;

            string word = end == fenceInfo.Length ? fenceInfo : fenceInfo.Substring(0, end);
            return _renderers.TryGetValue(word, out renderer);
        }
    }

    [NowBuilder]
    public struct NowMarkdownBuilder
    {
        readonly string _markdown;
        readonly string _file;
        readonly int _line;

        NowLayoutOptions _options;
        NowMarkdownStyle _style;
        bool _hasStyle;
        NowMarkdownEmbedSet _embeds;

        internal NowMarkdownBuilder(string markdown, string file, int line)
        {
            _markdown = markdown ?? string.Empty;
            _file = file;
            _line = line;
            _options = default;
            _style = default;
            _hasStyle = false;
            _embeds = null;
        }

        public NowMarkdownBuilder SetOptions(NowLayoutOptions options)
        {
            _options = options;
            return this;
        }

        public NowMarkdownBuilder SetWidth(float width)
        {
            _options = _options.SetWidth(width);
            return this;
        }

        public NowMarkdownBuilder SetHeight(float height)
        {
            _options = _options.SetHeight(height);
            return this;
        }

        public NowMarkdownBuilder SetStretchWidth(float weight = 1f)
        {
            _options = _options.SetStretchWidth(weight);
            return this;
        }

        public NowMarkdownBuilder SetStyle(NowMarkdownStyle style)
        {
            _style = style;
            _hasStyle = true;
            return this;
        }

        public NowMarkdownBuilder SetFontSize(float fontSize)
        {
            _style = _hasStyle ? _style : NowMarkdownStyle.Default;
            _style.fontSize = fontSize;
            _hasStyle = true;
            return this;
        }

        /// <summary>
        /// Turns fenced code blocks whose info string matches a registered embed
        /// into live embedded content (see <see cref="NowMarkdownEmbedSet"/>).
        /// Without this, every fence renders as a highlighted code block.
        /// </summary>
        public NowMarkdownBuilder SetEmbeds(NowMarkdownEmbedSet embeds)
        {
            _embeds = embeds;
            return this;
        }

        /// <summary>Draws in the active layout group.</summary>
        [NowConsumer]
        public NowMarkdownResult Draw()
        {
            var document = GetDocument();
            var content = NowLayout.ContentRect(_options, _file, _line);
            var result = document.Draw(content.rect, _embeds);
            content.End(result.height);
            return result;
        }

        /// <summary>Draws into an explicit rect without consuming layout space.</summary>
        [NowConsumer]
        public NowMarkdownResult Draw(NowRect rect)
        {
            return GetDocument().Draw(rect, _embeds);
        }

        NowMarkdownDocument GetDocument()
        {
            return _hasStyle
                ? NowMarkdown.GetCached(_markdown, _style)
                : NowMarkdown.GetCached(_markdown);
        }
    }

    /// <summary>
    /// Immediate-mode entry points for rendering GitHub-flavored Markdown.
    /// <code>
    /// NowMarkdown.Document(changelogText).Draw();       // flows in the active layout
    /// NowMarkdown.Document(readmeText).Draw(rect);      // explicit rect
    /// var doc = NowMarkdown.Parse(text);                // retained, for many draws
    /// </code>
    /// Drawn documents are parsed once and cached by text and style; layout
    /// re-runs only when the width or font changes, so steady-state drawing
    /// allocates nothing.
    /// Links report back through <see cref="NowMarkdownResult"/> — opening them
    /// is the caller's decision.
    /// </summary>
    public static class NowMarkdown
    {
        const int CacheLimit = 64;

        const int RecentSlots = 4;

        readonly struct CacheKey : System.IEquatable<CacheKey>
        {
            public readonly string markdown;
            public readonly float fontSize;

            public CacheKey(string markdown, NowMarkdownStyle style)
            {
                this.markdown = markdown;
                fontSize = style.fontSize;
            }

            public bool Equals(CacheKey other)
            {
                return fontSize == other.fontSize &&
                    string.Equals(markdown, other.markdown, System.StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((markdown != null ? markdown.GetHashCode() : 0) * 397) ^ fontSize.GetHashCode();
                }
            }
        }

        struct RecentSlot
        {
            public string markdown;
            public float fontSize;
            public NowMarkdownDocument document;
        }

        static readonly Dictionary<CacheKey, NowMarkdownDocument> _cache =
            new Dictionary<CacheKey, NowMarkdownDocument>(16);

        static readonly List<CacheKey> _cacheOrder = new List<CacheKey>(16);

        static readonly RecentSlot[] _recent = new RecentSlot[RecentSlots];

        static int _nextRecent;

        public static NowMarkdownBuilder Document(
            string markdown,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return new NowMarkdownBuilder(markdown, file, line);
        }

        public static NowMarkdownDocument Parse(string markdown)
        {
            return NowMarkdownDocument.Parse(markdown ?? string.Empty);
        }

        public static NowMarkdownDocument Parse(string markdown, NowMarkdownStyle style)
        {
            return NowMarkdownDocument.Parse(markdown ?? string.Empty, style);
        }

        internal static NowMarkdownDocument GetCached(string markdown)
        {
            return GetCached(markdown, NowMarkdownStyle.Default);
        }

        internal static NowMarkdownDocument GetCached(string markdown, NowMarkdownStyle style)
        {
            markdown ??= string.Empty;

            for (int i = 0; i < _recent.Length; ++i)
            {
                if (ReferenceEquals(_recent[i].markdown, markdown) && _recent[i].fontSize == style.fontSize)
                    return _recent[i].document;
            }

            for (int i = 0; i < _recent.Length; ++i)
            {
                if (_recent[i].markdown != null &&
                    _recent[i].fontSize == style.fontSize &&
                    _recent[i].markdown.Length == markdown.Length &&
                    string.Equals(_recent[i].markdown, markdown, System.StringComparison.Ordinal))
                {
                    _recent[i].markdown = markdown;
                    return _recent[i].document;
                }
            }

            var key = new CacheKey(markdown, style);

            if (!_cache.TryGetValue(key, out var document))
            {
                if (_cache.Count >= CacheLimit)
                    EvictOldestHalf();

                document = NowMarkdownDocument.Parse(markdown, style);
                _cache[key] = document;
                _cacheOrder.Add(key);
            }

            _recent[_nextRecent] = new RecentSlot
            {
                markdown = markdown,
                fontSize = style.fontSize,
                document = document
            };
            _nextRecent = (_nextRecent + 1) % _recent.Length;
            return document;
        }

        static void EvictOldestHalf()
        {
            int evict = _cacheOrder.Count / 2;

            for (int i = 0; i < evict; ++i)
                _cache.Remove(_cacheOrder[i]);

            _cacheOrder.RemoveRange(0, evict);
            System.Array.Clear(_recent, 0, _recent.Length);
            _nextRecent = 0;
        }

        public static void Reset()
        {
            _cache.Clear();
            _cacheOrder.Clear();
            System.Array.Clear(_recent, 0, _recent.Length);
            _nextRecent = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

}
