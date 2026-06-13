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

        public NowMarkdownDocument Parse()
        {
            return GetDocument();
        }

        NowMarkdownDocument GetDocument()
        {
            return _hasStyle
                ? NowMarkdownDocument.Parse(_markdown, _style)
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
