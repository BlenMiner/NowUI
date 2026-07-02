using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Markdown
{
    [NowBuilder]
    public struct NowMarkdownBuilder
    {
        readonly string _markdown;
        readonly string _file;
        readonly int _line;

        NowLayoutOptions _options;
        NowMarkdownStyle _style;
        bool _hasStyle;

        internal NowMarkdownBuilder(string markdown, string file, int line)
        {
            _markdown = markdown ?? string.Empty;
            _file = file;
            _line = line;
            _options = default;
            _style = default;
            _hasStyle = false;
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

        /// <summary>Draws in the active layout group.</summary>
        [NowConsumer]
        public NowMarkdownResult Draw()
        {
            var document = GetDocument();
            var content = NowLayout.ContentRect(_options, _file, _line);
            var result = document.Draw(content.rect);
            content.End(result.height);
            return result;
        }

        /// <summary>Draws into an explicit rect without consuming layout space.</summary>
        [NowConsumer]
        public NowMarkdownResult Draw(NowRect rect)
        {
            return GetDocument().Draw(rect);
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
