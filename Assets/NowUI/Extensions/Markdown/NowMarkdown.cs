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

        public static NowMarkdownDocument Parse(string markdown)
        {
            return NowMarkdownDocument.Parse(markdown ?? string.Empty);
        }

        /// <summary>
        /// Draws the markdown in the active layout group, stretching to the
        /// available width. Height settles one frame late, like all scope-form
        /// layout measurement.
        /// </summary>
        public static NowMarkdownResult Draw(string markdown)
        {
            var document = GetCached(markdown);
            float height = Mathf.Max(document.lastHeight, 4f);
            NowRect rect = NowLayout.Rect(new NowLayoutOptions().SetStretchWidth().SetHeight(height));
            var result = document.Draw(rect);

            if (Mathf.Abs(result.height - height) > 0.5f)
                NowUIControlState.RequestRepaint();

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

            if (_cache.TryGetValue(markdown, out var document))
                return document;

            if (_cache.Count >= CacheLimit)
                _cache.Clear();

            document = NowMarkdownDocument.Parse(markdown);
            _cache[markdown] = document;
            return document;
        }

        public static void Reset()
        {
            _cache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
