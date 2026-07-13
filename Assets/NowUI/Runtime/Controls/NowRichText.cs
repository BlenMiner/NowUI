using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    public struct NowRichTextSpan
    {
        public int start;
        public int length;
        public NowRichTextStyle style;
        public int tag;

        public NowRichTextSpan(int start, int length, NowRichTextStyle style, int tag = 0)
        {
            this.start = start;
            this.length = length;
            this.style = style;
            this.tag = tag;
        }
    }

    public struct NowRichTextResult
    {
        public NowRect rect;
        public bool hovered;
        public bool pressed;
        public bool clicked;
        public NowRichTextHit pointerHit;
        public NowRichTextLayout layout;
        public bool hasSelection;

        public bool TryHit(out NowRichTextHit hit)
        {
            hit = pointerHit;
            return hit.valid;
        }

        public bool TryHit(Vector2 position, out NowRichTextHit hit)
        {
            if (layout == null)
            {
                hit = default;
                return false;
            }

            return layout.TryHit(position, out hit);
        }

        public bool TryGetTag(int tag, out NowRichTextTagPayload payload)
        {
            if (layout == null)
            {
                payload = default;
                return false;
            }

            return layout.TryGetTag(tag, out payload);
        }

        public bool TryGetHitTag(out NowRichTextTagPayload payload)
        {
            if (!pointerHit.valid || pointerHit.tag == 0)
            {
                payload = default;
                return false;
            }

            return TryGetTag(pointerHit.tag, out payload);
        }
    }

    [NowBuilder]
    public struct NowRichText
    {
        const float DefaultLineHeight = 1.25f;

        readonly string _value;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;

        NowId _id;
        NowText _style;
        NowLayoutOptions _options;
        IReadOnlyList<NowRichTextSpan> _spans;
        NowRichTextParser _parser;
        float _lineHeight;
        bool _wrap;
        bool _selectable;
        bool _plainText;

        static readonly NowRichTextLayout SharedLayout = new NowRichTextLayout();

        struct LayoutInputs
        {
            public int documentRevision;
            public NowFontAsset font;
            public float fontSize;
            public NowFontStyle fontStyle;
            public Vector4 color;
            public float lineHeight;
            public bool wrap;

            public bool Matches(in LayoutInputs other)
            {
                return documentRevision == other.documentRevision &&
                    ReferenceEquals(font, other.font) &&
                    fontSize == other.fontSize &&
                    fontStyle == other.fontStyle &&
                    color == other.color &&
                    lineHeight == other.lineHeight &&
                    wrap == other.wrap;
            }
        }

        struct State
        {
            public NowRichTextLayout layout;
            public NowRichTextDocument document;
            public string parsedValue;
            public NowRichTextParser parsedParser;
            public float parsedFontSize;
            public NowFontStyle parsedFontStyle;
            public Vector4 parsedColor;
            public Vector4 parsedAccentColor;
            public float contentHeight;
            public int documentRevision;
            public bool hasLayout;
            public LayoutInputs layoutInputs;
            public NowRect layoutRect;
            public bool hasMeasuredHeight;
            public LayoutInputs measuredInputs;
            public float measuredWidth;
            public float measuredHeight;
        }

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowRichText(string value, NowText style, int site)
        {
            _value = value ?? string.Empty;
            _site = site;
            _rect = default;
            _hasRect = false;
            _id = default;
            _style = style;
            _options = default;
            _spans = null;
            _parser = null;
            _lineHeight = 0f;
            _wrap = true;
            _selectable = false;
            _plainText = false;
        }

        internal NowRichText(NowRect rect, string value, NowText style, int site) : this(value, style, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowRichText SetId(NowId id) { _id = id; return this; }

        public NowRichText SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowRichText SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowRichText SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowRichText SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowRichText SetStretchHeight(float weight = 1f) { _options = _options.SetStretchHeight(weight); return this; }

        public NowRichText SetFont(NowFontAsset font) { _style = _style.SetFont(font); return this; }

        public NowRichText SetFontSize(float fontSize) { _style = _style.SetFontSize(fontSize); return this; }

        public NowRichText SetFontStyle(NowFontStyle fontStyle) { _style = _style.SetFontStyle(fontStyle); return this; }

        public NowRichText SetColor(Color color) { _style = _style.SetColor(color); return this; }

        public NowRichText SetColor(Vector4 color) { _style = _style.SetColor(color); return this; }

        public NowRichText SetSpans(IReadOnlyList<NowRichTextSpan> spans) { _spans = spans; return this; }

        /// <summary>
        /// Enables the built-in tag set: <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>,
        /// <c>&lt;u&gt;</c>, <c>&lt;s&gt;</c>/<c>&lt;strikethrough&gt;</c>,
        /// <c>&lt;color=...&gt;</c> (3/4/6/8-digit hex with optional '#', or an
        /// HTML color name like <c>red</c>), <c>&lt;size=18&gt;</c>,
        /// <c>&lt;link=id&gt;</c>, <c>&lt;br/&gt;</c> and <c>&lt;noparse&gt;</c>.
        /// Unrecognized tags render literally; add custom ones with
        /// <see cref="ParseTag(string, NowRichTextTagHandler)"/>.
        /// </summary>
        public NowRichText ParseDefaultTags()
        {
            _parser = (_parser ?? NowRichTextParser.Empty).WithDefaultTags();
            return this;
        }

        public NowRichText ParseTag(string name, NowRichTextTagHandler handler)
        {
            _parser = (_parser ?? NowRichTextParser.Empty).WithTag(name, handler);
            return this;
        }

        public NowRichText UseParser(NowRichTextParser parser)
        {
            _parser = parser;
            return this;
        }

        /// <summary>
        /// Declares the value deliberately plain: no tag parsing, and the
        /// editor/dev-build hint that fires when an unparsed value contains
        /// markup-looking tags is suppressed. Use for generated or user text
        /// that may legitimately contain angle brackets.
        /// </summary>
        public NowRichText SetPlainText()
        {
            _parser = null;
            _plainText = true;
            return this;
        }

        public NowRichText SetWrap(bool wrap = true) { _wrap = wrap; return this; }

        public NowRichText SetLineHeight(float lineHeight) { _lineHeight = lineHeight; return this; }

        public NowRichText SetSelectable(bool selectable = true) { _selectable = selectable; return this; }

        [NowConsumer]
        public NowRichTextResult Draw()
        {
            int id = ResolveControlId();
            float lineHeight = _lineHeight > 0f ? _lineHeight : _style.fontSize * DefaultLineHeight;
            ref var state = ref NowControlState.Get<State>(id);
            var document = PrepareDocument(ref state);
            var inputs = new LayoutInputs
            {
                documentRevision = state.documentRevision,
                font = _style.font,
                fontSize = _style.fontSize,
                fontStyle = _style.fontStyle,
                color = _style.color,
                lineHeight = lineHeight,
                wrap = _wrap
            };
            NowRect rect = Reserve(lineHeight, document, ref state, inputs);
            var interaction = _selectable ? default : NowInput.Interact(id, rect);

            if (state.layout == null)
            {
                state.layout = new NowRichTextLayout();
                state.hasLayout = false;
            }

            if (!state.hasLayout || state.layoutRect != rect || !state.layoutInputs.Matches(inputs))
            {
                state.layout.Clear();
                BuildLayout(state.layout, rect, lineHeight, document);
                state.layout.CompleteLines();
                state.hasLayout = true;
                state.layoutInputs = inputs;
                state.layoutRect = rect;
            }

            UpdateReservedHeight(ref state, lineHeight);

            bool hovered = _selectable ? NowInput.IsHovered(rect) : interaction.hovered;
            bool hasSelection = false;

            if (!NowLayout.isMeasurePass)
            {
                if (_selectable)
                    hasSelection = DrawSelection(id, state.layout);

                DrawRuns(state.layout, rect);
            }

            var result = new NowRichTextResult
            {
                rect = rect,
                hovered = hovered,
                pressed = interaction.pressed,
                clicked = interaction.clicked,
                layout = state.layout,
                hasSelection = hasSelection
            };

            if (hovered)
            {
                Vector2 pointer = _selectable ? NowInput.current.pointerPosition : interaction.pointerPosition;
                state.layout.TryHit(pointer, out result.pointerHit);
            }

            return result;
        }

        bool DrawSelection(int id, NowRichTextLayout layout)
        {
            string text = layout.text;

            if (string.IsNullOrEmpty(text) || layout.selectionLines.Count == 0 || _style.font == null)
                return false;

            int selectionId = NowInput.GetId(id, "selection");
            var selection = NowTextSelection.Interact(
                selectionId,
                text,
                layout.selectionLines,
                _style.font,
                _style.fontSize,
                _style.fontStyle);

            int menuId = NowInput.GetId(selectionId, "menu");

            if (selection.rightClicked)
                NowContextMenu.Open(menuId, selection.rightClickPosition);

            if (NowContextMenu.Begin(menuId))
            {
                if (selection.hasSelection && NowContextMenu.Item("Copy"))
                    NowClipboard.Copy(NowTextSelection.GetSelection(selectionId, text));

                if (NowContextMenu.Item("Select All"))
                    NowTextSelection.SelectAll(selectionId, text);

                NowContextMenu.End();
            }

            Color highlight = NowTheme.themeAsset.GetColor(NowColorToken.Accent);
            highlight.a = 0.25f;

            return NowTextSelection.DrawHighlights(
                selectionId,
                text,
                layout.selectionLines,
                _style.font,
                _style.fontSize,
                _style.fontStyle,
                highlight);
        }

        NowRichTextDocument PrepareDocument(ref State state)
        {
            var baseStyle = new NowRichTextStyle(_style.fontSize, _style.fontStyle).SetColor(_style.color);
            Vector4 accentColor = NowTheme.themeAsset.GetColor(NowColorToken.Accent);

            if (state.document == null)
                state.document = new NowRichTextDocument();

            if (_parser != null)
            {
                if (!string.Equals(state.parsedValue, _value) ||
                    !ReferenceEquals(state.parsedParser, _parser) ||
                    !Mathf.Approximately(state.parsedFontSize, baseStyle.fontSize) ||
                    state.parsedFontStyle != baseStyle.fontStyle ||
                    state.parsedColor != baseStyle.color ||
                    state.parsedAccentColor != accentColor)
                {
                    _parser.Parse(_value, baseStyle, state.document);
                    state.parsedValue = _value;
                    state.parsedParser = _parser;
                    state.parsedFontSize = baseStyle.fontSize;
                    state.parsedFontStyle = baseStyle.fontStyle;
                    state.parsedColor = baseStyle.color;
                    state.parsedAccentColor = accentColor;
                    ++state.documentRevision;
                }

                return state.document;
            }

            if (state.parsedParser != null ||
                !string.Equals(state.parsedValue, _value) ||
                !SpansMatch(state.document.spans, _spans))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_spans == null && !_plainText && ContainsDefaultTag(_value) && s_warnedUnparsedTagSites.Add(_site))
                    Debug.LogWarning($"NowUI: RichText value contains markup-looking tags but no parser is attached, so they render literally. Chain .ParseDefaultTags() to parse them, or .SetPlainText() if the angle brackets are intentional. Value: \"{(_value.Length > 60 ? _value.Substring(0, 60) + "…" : _value)}\"");
#endif

                state.parsedValue = _value;
                state.parsedParser = null;
                state.document.Clear();
                state.document.text = _value;

                if (_spans != null)
                {
                    for (int i = 0; i < _spans.Count; ++i)
                        state.document.spans.Add(_spans[i]);
                }

                ++state.documentRevision;
            }

            return state.document;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<int> s_warnedUnparsedTagSites = new HashSet<int>();

        /// <summary>
        /// Cheap dev-build scan for tags the default parser would recognize
        /// (&lt;b&gt;, &lt;color=...&gt;, closing forms, ...) so the unparsed-tag
        /// hint only fires on values that were plausibly meant as markup.
        /// </summary>
        static bool ContainsDefaultTag(string value)
        {
            for (int i = 0; i < value.Length - 1; ++i)
            {
                if (value[i] != '<')
                    continue;

                int start = i + 1;

                if (start < value.Length && value[start] == '/')
                    ++start;

                int end = start;

                while (end < value.Length && char.IsLetter(value[end]))
                    ++end;

                if (end == start || end >= value.Length)
                    continue;

                char terminator = value[end];

                if (terminator != '>' && terminator != '=' && terminator != ' ' && terminator != '/')
                    continue;

                switch (value.Substring(start, end - start).ToLowerInvariant())
                {
                    case "b":
                    case "i":
                    case "u":
                    case "s":
                    case "strikethrough":
                    case "color":
                    case "size":
                    case "link":
                    case "br":
                    case "noparse":
                        return true;
                }
            }

            return false;
        }
#endif

        static bool SpansMatch(List<NowRichTextSpan> current, IReadOnlyList<NowRichTextSpan> requested)
        {
            int count = requested?.Count ?? 0;

            if (current.Count != count)
                return false;

            for (int i = 0; i < count; ++i)
            {
                var a = current[i];
                var b = requested[i];

                if (a.start != b.start || a.length != b.length || a.tag != b.tag ||
                    !StylesMatch(a.style, b.style))
                {
                    return false;
                }
            }

            return true;
        }

        static bool StylesMatch(in NowRichTextStyle a, in NowRichTextStyle b)
        {
            return a.fontSize == b.fontSize &&
                a.fontStyle == b.fontStyle &&
                a.hasColor == b.hasColor &&
                a.color == b.color &&
                a.underline == b.underline &&
                a.strikethrough == b.strikethrough;
        }

        NowRect Reserve(float lineHeight, NowRichTextDocument document, ref State state, in LayoutInputs inputs)
        {
            if (_hasRect)
                return _rect;

            if (_options.Has(NowLayoutOptions.Field.StretchWidth))
            {
                var options = _options;

                if (!options.Has(NowLayoutOptions.Field.Height) &&
                    !options.Has(NowLayoutOptions.Field.StretchHeight))
                {
                    options = options.SetHeight(Mathf.Max(state.contentHeight, lineHeight));
                }

                return NowLayout.ReserveRect(options);
            }

            float width = ResolveLayoutWidth(document.text);

            if (!state.hasMeasuredHeight || state.measuredWidth != width || !state.measuredInputs.Matches(inputs))
            {
                state.measuredHeight = MeasureHeight(width, lineHeight, document);
                state.measuredWidth = width;
                state.measuredInputs = inputs;
                state.hasMeasuredHeight = true;
            }

            return NowControls.ReserveRect(false, default, _options, new Vector2(width, state.measuredHeight));
        }

        void UpdateReservedHeight(ref State state, float lineHeight)
        {
            if (_hasRect ||
                !_options.Has(NowLayoutOptions.Field.StretchWidth) ||
                _options.Has(NowLayoutOptions.Field.Height) ||
                _options.Has(NowLayoutOptions.Field.StretchHeight))
            {
                return;
            }

            float height = state.layout.bounds.height > 0f
                ? state.layout.bounds.height
                : state.layout.lines.Count > 0 ? state.layout.lines.Count * lineHeight : lineHeight;

            if (Mathf.Abs(state.contentHeight - height) > 0.25f)
            {
                state.contentHeight = height;
                NowControlState.RequestRepaint();
            }
        }

        float ResolveLayoutWidth(string text)
        {
            if (_options.Has(NowLayoutOptions.Field.Width))
                return _options.width;

            if (_options.Has(NowLayoutOptions.Field.StretchWidth))
                return Mathf.Max(_style.Measure(text).x + 1f, 1f);

            return Mathf.Max(_style.Measure(text).x + 1f, 1f);
        }

        float MeasureHeight(float width, float lineHeight, NowRichTextDocument document)
        {
            SharedLayout.Clear();
            BuildLayout(SharedLayout, new NowRect(0f, 0f, width, float.MaxValue), lineHeight, document);
            SharedLayout.CompleteLines();
            return SharedLayout.bounds.height > 0f
                ? SharedLayout.bounds.height
                : SharedLayout.lines.Count > 0 ? SharedLayout.lines.Count * lineHeight : lineHeight;
        }

        void BuildLayout(NowRichTextLayout layout, NowRect rect, float lineHeight, NowRichTextDocument document)
        {
            var cursor = layout.Cursor(rect.x, rect.y, rect.xMax, lineHeight);
            string text = document.text ?? string.Empty;
            layout.AddTagPayloads(document.tags);

            if (document.spans.Count == 0)
            {
                AddTextRange(layout, ref cursor, text, 0, text.Length, new NowRichTextStyle(_style.fontSize, _style.fontStyle)
                    .SetColor(_style.color), 0, document.inlines);
                return;
            }

            int index = 0;

            for (int i = 0; i < document.spans.Count; ++i)
            {
                var span = document.spans[i];
                int spanStart = Mathf.Clamp(Mathf.Max(span.start, index), 0, text.Length);
                int spanEnd = Mathf.Clamp(span.start + span.length, spanStart, text.Length);

                if (spanEnd <= index)
                    continue;

                if (index < spanStart)
                {
                    AddTextRange(layout, ref cursor, text, index, spanStart - index,
                        new NowRichTextStyle(_style.fontSize, _style.fontStyle).SetColor(_style.color), 0, document.inlines);
                }

                var style = ResolveSpanStyle(span.style);
                AddTextRange(layout, ref cursor, text, spanStart, spanEnd - spanStart, style, span.tag, document.inlines);
                index = spanEnd;
            }

            if (index < text.Length)
            {
                AddTextRange(layout, ref cursor, text, index, text.Length - index,
                    new NowRichTextStyle(_style.fontSize, _style.fontStyle).SetColor(_style.color), 0, document.inlines);
            }
        }

        NowRichTextStyle ResolveSpanStyle(NowRichTextStyle span)
        {
            if (span.fontSize <= 0f)
                span.fontSize = _style.fontSize;

            if (!span.hasColor)
                span = span.SetColor(_style.color);

            return span;
        }

        void AddTextRange(
            NowRichTextLayout layout,
            ref NowRichTextCursor cursor,
            string text,
            int start,
            int length,
            NowRichTextStyle style,
            int tag,
            IReadOnlyList<NowRichTextInline> inlines)
        {
            int end = start + length;
            int segmentStart = start;

            for (int i = start; i <= end; ++i)
            {
                char c = i < end ? text[i] : '\0';
                bool flush = i == end || c == '\n' || c == ' ' || c == '\uFFFC';

                if (!flush)
                    continue;

                if (c == ' ')
                {
                    while (i + 1 < end && text[i + 1] == ' ')
                        ++i;
                }

                int segmentEnd = c == '\n' || c == '\uFFFC' ? i : Mathf.Min(i + 1, end);

                if (segmentEnd > segmentStart)
                {
                    layout.AddRun(ref cursor, text, segmentStart, segmentEnd - segmentStart, _style.font,
                        style, _wrap, tag, separate: false);
                }

                if (c == '\uFFFC')
                {
                    if (TryFindInline(inlines, i, out var inline))
                    {
                        layout.AddInline(ref cursor, inline.width, inline.height, inline.tag,
                            inline.payload, inline.draw, _wrap);
                    }

                    segmentStart = i + 1;
                    continue;
                }

                if (c == '\n')
                {
                    layout.AppendText("\n");
                    NowRichTextFlow.NewLine(ref cursor);
                    segmentStart = i + 1;
                    continue;
                }

                segmentStart = i + 1;
            }
        }

        static bool TryFindInline(IReadOnlyList<NowRichTextInline> inlines, int index, out NowRichTextInline inline)
        {
            if (inlines != null)
            {
                for (int i = 0; i < inlines.Count; ++i)
                {
                    if (inlines[i].index == index)
                    {
                        inline = inlines[i];
                        return true;
                    }
                }
            }

            inline = default;
            return false;
        }

        void DrawRuns(NowRichTextLayout layout, NowRect mask)
        {
            for (int i = 0; i < layout.runs.Count; ++i)
            {
                var run = layout.runs[i];

                if (run.isInline)
                {
                    run.drawInline?.Invoke(run, RunMask(run.rect, mask));
                    continue;
                }

                if (run.length <= 0 || run.start < 0)
                    continue;

                var style = _style
                    .SetPosition(run.rect)
                    .SetMask(RunMask(run.rect, mask))
                    .SetFontSize(run.fontSize)
                    .SetFontStyle(run.fontStyle);

                if (run.hasColor)
                    style = style.SetColor(run.color);

                style.Draw(System.MemoryExtensions.AsSpan(layout.text, run.start, run.length));

                if (run.underline)
                    Now.Rectangle(new NowRect(run.rect.x, run.rect.yMax - 2f, run.rect.width - 1f, 1f))
                        .SetColor(style.color)
                        .Draw();

                if (run.strikethrough)
                    Now.Rectangle(new NowRect(run.rect.x, run.rect.y + run.rect.height * 0.55f, run.rect.width - 1f, 1f))
                        .SetColor(style.color)
                        .Draw();
            }
        }

        static NowRect RunMask(NowRect runRect, NowRect regionMask)
        {
            return regionMask.Union(runRect).Outset(4f);
        }
    }

    public static partial class Now
    {
        /// <summary>
        /// Rich text at an explicit rect. Tag parsing is opt-in: chain
        /// <see cref="NowRichText.ParseDefaultTags"/> to interpret
        /// <c>&lt;b&gt;</c>-style markup, or <see cref="NowRichText.SetSpans"/>
        /// to style generated ranges — a bare value renders as plain
        /// (selectable) text.
        /// </summary>
        public static NowRichText RichText(
            NowRect rect,
            string value,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowRichText(rect, value, NowTheme.themeAsset.ResolveText(NowTextStyle.Body), NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        /// <summary>
        /// Rich text in layout flow. Tag parsing is opt-in: chain
        /// <see cref="NowRichText.ParseDefaultTags"/> to interpret
        /// <c>&lt;b&gt;</c>-style markup, or <see cref="NowRichText.SetSpans"/>
        /// to style generated ranges — a bare value renders as plain
        /// (selectable) text.
        /// </summary>
        public static NowRichText RichText(
            string value,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowRichText(value, NowTheme.themeAsset.ResolveText(NowTextStyle.Body), NowControls.SiteId(file, line));
        }
    }
}
