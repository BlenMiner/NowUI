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
    }

    [NowBuilder]
    public struct NowRichText
    {
        const float DefaultLineHeight = 1.25f;

        readonly string _value;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;

        string _id;
        NowText _style;
        NowLayoutOptions _options;
        IReadOnlyList<NowRichTextSpan> _spans;
        NowRichTextParser _parser;
        float _lineHeight;
        bool _wrap;

        static readonly NowRichTextLayout SharedLayout = new NowRichTextLayout();
        static readonly NowRichTextDocument SharedDocument = new NowRichTextDocument();

        struct State
        {
            public NowRichTextLayout layout;
        }

        int ResolveControlId() => _id != null ? NowControls.GetControlId(_id) : NowControls.GetControlId(_site);

        internal NowRichText(string value, NowText style, int site)
        {
            _value = value ?? string.Empty;
            _site = site;
            _rect = default;
            _hasRect = false;
            _id = null;
            _style = style;
            _options = default;
            _spans = null;
            _parser = null;
            _lineHeight = 0f;
            _wrap = true;
        }

        internal NowRichText(NowRect rect, string value, NowText style, int site) : this(value, style, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowRichText SetId(string id) { _id = id; return this; }

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

        public NowRichText ParseTags(NowRichTextParser parser)
        {
            _parser = parser;
            return this;
        }

        public NowRichText SetWrap(bool wrap = true) { _wrap = wrap; return this; }

        public NowRichText SetLineHeight(float lineHeight) { _lineHeight = lineHeight; return this; }

        [NowConsumer]
        public NowRichTextResult Draw()
        {
            int id = ResolveControlId();
            float lineHeight = _lineHeight > 0f ? _lineHeight : _style.fontSize * DefaultLineHeight;
            var document = PrepareDocument();
            NowRect rect = Reserve(lineHeight, document);
            var interaction = NowInput.Interact(id, rect);
            ref var state = ref NowControlState.Get<State>(id);

            if (state.layout == null)
                state.layout = new NowRichTextLayout();

            state.layout.Clear();
            BuildLayout(state.layout, rect, lineHeight, document);
            state.layout.CompleteLines();

            if (!NowLayout.isMeasurePass)
                DrawRuns(state.layout, rect);

            var result = new NowRichTextResult
            {
                rect = rect,
                hovered = interaction.hovered,
                pressed = interaction.pressed,
                clicked = interaction.clicked,
                layout = state.layout
            };

            if (interaction.hovered)
                state.layout.TryHit(interaction.pointerPosition, out result.pointerHit);

            return result;
        }

        NowRichTextDocument PrepareDocument()
        {
            var baseStyle = new NowRichTextStyle(_style.fontSize, _style.fontStyle).SetColor(_style.color);

            if (_parser != null)
            {
                _parser.Parse(_value, baseStyle, SharedDocument);
                return SharedDocument;
            }

            SharedDocument.Clear();
            SharedDocument.text = _value;

            if (_spans != null)
            {
                for (int i = 0; i < _spans.Count; ++i)
                    SharedDocument.spans.Add(_spans[i]);
            }

            return SharedDocument;
        }

        NowRect Reserve(float lineHeight, NowRichTextDocument document)
        {
            if (_hasRect)
                return _rect;

            float width = ResolveLayoutWidth(document.text);
            float height = MeasureHeight(width, lineHeight, document);
            return NowControls.ReserveRect(false, default, _options, new Vector2(width, height));
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
            return SharedLayout.lines.Count > 0 ? SharedLayout.lines.Count * lineHeight : lineHeight;
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
                    run.drawInline?.Invoke(run, mask);
                    continue;
                }

                if (run.length <= 0 || run.start < 0)
                    continue;

                var style = _style
                    .SetPosition(run.rect)
                    .SetMask(mask)
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
    }

    public static partial class Now
    {
        public static NowRichText RichText(
            NowRect rect,
            string value,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowRichText(rect, value, NowControls.theme.ResolveText(NowTextStyle.Body), NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowRichText RichText(
            string value,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowRichText(value, labelStyle, NowControls.SiteId(file, line));
        }
    }
}
