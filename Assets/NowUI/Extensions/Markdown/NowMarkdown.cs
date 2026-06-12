using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Markdown
{
    /// <summary>
    /// Immediate-mode entry points for rendering GitHub-flavored Markdown.
    /// <code>
    /// NowMarkdown.Draw(changelogText);                  // flows in the active layout
    /// NowMarkdown.Draw(rect, readmeText);               // explicit rect
    /// var doc = NowMarkdownDocument.Parse(text);        // retained, for many draws
    /// </code>
    /// Drawn documents are parsed once and cached by text; layout re-runs only
    /// when the width or font changes, so steady-state drawing allocates nothing.
    /// Links report back through <see cref="NowMarkdownResult"/> — opening them
    /// is the caller's decision.
    /// </summary>
    public static class NowMarkdown
    {
        const int CacheLimit = 64;

        static readonly Dictionary<string, NowMarkdownDocument> _cache =
            new Dictionary<string, NowMarkdownDocument>(16);

        static string _lastMarkdown;

        static NowMarkdownDocument _lastDocument;

        public static NowMarkdownDocument Parse(string markdown)
        {
            return NowMarkdownDocument.Parse(markdown ?? string.Empty);
        }

        /// <summary>
        /// Draws the markdown in the active layout group, stretching to the
        /// available width. Height settles one frame late, like all scope-form
        /// layout measurement. Identity comes from the call site, so several
        /// blocks can interleave with other layout content.
        /// </summary>
        public static NowMarkdownResult Draw(
            string markdown,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            var document = GetCached(markdown);
            var content = NowLayout.ContentRect(default, file, line);
            var result = document.Draw(content.rect);
            content.End(result.height);
            return result;
        }

        /// <summary>Draws the markdown wrapped to the rect's width.</summary>
        public static NowMarkdownResult Draw(NowRect rect, string markdown)
        {
            return GetCached(markdown).Draw(rect);
        }

        static NowMarkdownDocument GetCached(string markdown)
        {
            markdown ??= string.Empty;

            if (ReferenceEquals(markdown, _lastMarkdown))
                return _lastDocument;

            if (!_cache.TryGetValue(markdown, out var document))
            {
                if (_cache.Count >= CacheLimit)
                    _cache.Clear();

                document = NowMarkdownDocument.Parse(markdown);
                _cache[markdown] = document;
            }

            _lastMarkdown = markdown;
            _lastDocument = document;
            return document;
        }

        public static void Reset()
        {
            _cache.Clear();
            _lastMarkdown = null;
            _lastDocument = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
