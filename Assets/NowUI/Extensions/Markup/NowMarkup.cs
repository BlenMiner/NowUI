using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        const int CacheLimit = 64;

        static readonly Dictionary<string, NowMarkupDocument> _cache =
            new Dictionary<string, NowMarkupDocument>(16);

        static readonly Dictionary<string, NowMarkupFile> _files =
            new Dictionary<string, NowMarkupFile>(8);

        static string _lastMarkup;
        static NowMarkupDocument _lastDocument;

        public static NowMarkupBuilder Document(
            string markup,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
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

            if (ReferenceEquals(markup, _lastMarkup))
                return _lastDocument;

            if (!_cache.TryGetValue(markup, out var document))
            {
                if (_cache.Count >= CacheLimit)
                    _cache.Clear();

                document = NowMarkupDocument.Parse(markup);
                _cache[markup] = document;
            }

            _lastMarkup = markup;
            _lastDocument = document;
            return document;
        }

        public static void Reset()
        {
            _cache.Clear();

            foreach (var source in _files.Values)
                source.Dispose();

            _files.Clear();
            _lastMarkup = null;
            _lastDocument = null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
