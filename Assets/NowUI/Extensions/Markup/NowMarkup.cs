using System.Collections.Generic;

namespace NowUI.Markup
{
    [NowBuilder]
    public struct NowMarkupBuilder
    {
        readonly string _markup;
        NowMarkupState _state;

        internal NowMarkupBuilder(string markup)
        {
            _markup = markup ?? string.Empty;
            _state = null;
        }

        public NowMarkupBuilder SetState(NowMarkupState state)
        {
            _state = state;
            return this;
        }

        /// <summary>Draws into the current NowLayout group.</summary>
        [NowConsumer]
        public NowMarkupResult Draw()
        {
            return NowMarkup.GetCached(_markup).Draw(_state);
        }

        /// <summary>Draws into the current NowLayout group with caller-owned state.</summary>
        [NowConsumer]
        public NowMarkupResult Draw(NowMarkupState state)
        {
            return NowMarkup.GetCached(_markup).Draw(state);
        }

        /// <summary>Draws inside an explicit rect by opening a root layout area.</summary>
        [NowConsumer]
        public NowMarkupResult Draw(NowRect rect)
        {
            return NowMarkup.GetCached(_markup).Draw(rect, _state);
        }

        /// <summary>Draws inside an explicit rect with caller-owned state.</summary>
        [NowConsumer]
        public NowMarkupResult Draw(NowRect rect, NowMarkupState state)
        {
            return NowMarkup.GetCached(_markup).Draw(rect, state);
        }
    }

    /// <summary>
    /// Entry point for constrained NowUI markup. The language maps known tags to
    /// NowUI layout, text, and controls; it does not interpret browser HTML/CSS.
    /// </summary>
    public static class NowMarkup
    {
        /// <summary>
        /// When true (default), result queries such as <c>Clicked("save")</c>
        /// warn once in the editor and development builds if the document never
        /// declares the queried id, key, or action. Release players skip the
        /// check entirely.
        /// </summary>
        public static bool validateQueries = true;

        const int CacheLimit = 64;

        const int RecentSlots = 4;

        struct RecentSlot
        {
            public string markup;
            public NowMarkupDocument document;
        }

        static readonly Dictionary<string, NowMarkupDocument> _cache =
            new Dictionary<string, NowMarkupDocument>(16);

        static readonly List<string> _cacheOrder = new List<string>(16);

        static readonly Dictionary<string, NowMarkupFile> _files =
            new Dictionary<string, NowMarkupFile>(8);

        static readonly RecentSlot[] _recent = new RecentSlot[RecentSlots];

        static int _nextRecent;

        public static NowMarkupBuilder Document(string markup)
        {
            return new NowMarkupBuilder(markup);
        }

        public static NowMarkupDocument Parse(string markup)
        {
            return NowMarkupDocument.Parse(markup ?? string.Empty);
        }

        /// <summary>
        /// Returns a cached disk-backed markup source. Draw/Refresh reparses when
        /// the file's timestamp or length changes.
        /// </summary>
        public static NowMarkupFile File(string path)
        {
            path ??= string.Empty;
            string key = System.IO.Path.IsPathRooted(path)
                ? System.IO.Path.GetFullPath(path)
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), path));

            if (!_files.TryGetValue(key, out var source))
            {
                source = new NowMarkupFile(path);
                _files[key] = source;
            }

            return source;
        }

        internal static NowMarkupDocument GetCached(string markup)
        {
            markup ??= string.Empty;

            for (int i = 0; i < _recent.Length; ++i)
            {
                if (ReferenceEquals(_recent[i].markup, markup))
                    return _recent[i].document;
            }

            if (!_cache.TryGetValue(markup, out var document))
            {
                if (_cache.Count >= CacheLimit)
                    EvictOldestHalf();

                document = NowMarkupDocument.Parse(markup);
                _cache[markup] = document;
                _cacheOrder.Add(markup);
            }

            _recent[_nextRecent] = new RecentSlot
            {
                markup = markup,
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

            foreach (var source in _files.Values)
                source.Dispose();

            _files.Clear();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
