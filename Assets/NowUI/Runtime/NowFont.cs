using NowUI.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NowUI
{
    [Serializable]
    public struct NowFontAtlasInfo
    {
        [Serializable]
        public struct Atlas
        {
            public string type;
            public int distanceRange;
            public int size;
            public int width;
            public int height;
            public string yOrigin;
        }

        [Serializable]
        public struct Metrics
        {
            public float emSize;
            public float lineHeight;
            public float ascender;
            public float descender;
            public float underlineY;
            public float underlineThickness;
        }

        [Serializable]
        public struct Bounds
        {
            public float left;
            public float bottom;
            public float right;
            public float top;
        }

        [Serializable]
        public struct Glyph
        {
            public int unicode;
            public float advance;
            public Bounds planeBounds;
            public Bounds atlasBounds;
        }

        public Atlas atlas;

        public Metrics metrics;

        public Glyph[] glyphs;
    }

    [Flags]
    public enum NowFontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        BoldItalic = Bold | Italic
    }

    public abstract class NowFontAsset : ScriptableObject
    {
        [SerializeField]
        NowFontAsset[] _fallbacks;

        [NonSerialized]
        HashSet<NowFontAsset> _visitCache;

        public IReadOnlyList<NowFontAsset> fallbacks => _fallbacks;

        /// <summary>
        /// Bumped whenever cached glyph layout data (advances, shaping) may have
        /// changed, so layout memos keyed on this asset can invalidate.
        /// </summary>
        internal virtual int layoutDataVersion => 0;

        protected abstract bool TryGetOwnFont(NowFontStyle style, out NowFont font);

        HashSet<NowFontAsset> GetVisitCache()
        {
            _visitCache ??= new HashSet<NowFontAsset>();
            _visitCache.Clear();
            return _visitCache;
        }

        /// <summary>Resolves the font for a style. A styled variant (bold/italic) nobody
        /// provides falls back to Regular, so it renders as regular text, not as nothing.</summary>
        public bool TryResolveFont(NowFontStyle style, out NowFont font)
        {
            var visited = GetVisitCache();

            try
            {
                if (TryResolveFont(style, visited, out font))
                    return true;

                if (style == NowFontStyle.Regular)
                    return false;

                visited.Clear();
                return TryResolveFont(NowFontStyle.Regular, visited, out font);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal bool SupportsOutlineOnlyPass(NowFontStyle style)
        {
            var visited = GetVisitCache();
            bool foundFont = false;

            try
            {
                if (!SupportsOutlineOnlyPass(style, visited, ref foundFont))
                    return false;

                if (style != NowFontStyle.Regular)
                {
                    visited.Clear();

                    if (!SupportsOutlineOnlyPass(NowFontStyle.Regular, visited, ref foundFont))
                        return false;
                }

                return foundFont;
            }
            finally
            {
                visited.Clear();
            }
        }

        bool SupportsOutlineOnlyPass(
            NowFontStyle style,
            HashSet<NowFontAsset> visited,
            ref bool foundFont)
        {
            if (this == null || !visited.Add(this))
                return true;

            if (TryGetOwnFont(style, out var ownFont) && ownFont != null)
            {
                foundFont = true;

                if (!ownFont.supportsOutlineOnlyPass)
                    return false;
            }

            if (_fallbacks == null)
                return true;

            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null &&
                    !fallback.SupportsOutlineOnlyPass(style, visited, ref foundFont))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryResolveFont(NowFontStyle style, HashSet<NowFontAsset> visited, out NowFont font)
        {
            font = null;

            if (this == null || !visited.Add(this))
                return false;

            if (TryGetOwnFont(style, out font) && font != null)
                return true;

            if (_fallbacks == null)
                return false;

            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null && fallback.TryResolveFont(style, visited, out font))
                    return true;
            }

            return false;
        }

        public bool TryResolveGlyph(
            int unicode,
            float fontSize,
            NowFontStyle style,
            out NowFont font,
            out NowFontAtlasInfo.Glyph glyph,
            out Material material)
        {
            var visited = GetVisitCache();

            try
            {
                if (TryResolveGlyph(unicode, fontSize, style, visited, out font, out glyph, out material))
                    return true;

                if (style == NowFontStyle.Regular)
                    return false;

                visited.Clear();
                return TryResolveGlyph(unicode, fontSize, NowFontStyle.Regular, visited, out font, out glyph, out material);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal bool TryResolveGlyph(
            int unicode,
            float fontSize,
            float outline,
            NowFontStyle style,
            out NowFont font,
            out NowFontAtlasInfo.Glyph glyph,
            out Material material)
        {
            var visited = GetVisitCache();

            try
            {
                if (TryResolveGlyph(unicode, fontSize, outline, style, visited, out font, out glyph, out material))
                    return true;

                if (style == NowFontStyle.Regular)
                    return false;

                visited.Clear();
                return TryResolveGlyph(
                    unicode,
                    fontSize,
                    outline,
                    NowFontStyle.Regular,
                    visited,
                    out font,
                    out glyph,
                    out material);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal bool TryResolveGlyph(
            int unicode,
            float fontSize,
            NowFontStyle style,
            HashSet<NowFontAsset> visited,
            out NowFont font,
            out NowFontAtlasInfo.Glyph glyph,
            out Material material)
        {
            return TryResolveGlyph(unicode, fontSize, 0f, style, visited, out font, out glyph, out material);
        }

        internal bool TryResolveGlyph(
            int unicode,
            float fontSize,
            float outline,
            NowFontStyle style,
            HashSet<NowFontAsset> visited,
            out NowFont font,
            out NowFontAtlasInfo.Glyph glyph,
            out Material material)
        {
            font = null;
            glyph = default;
            material = null;

            if (this == null || !visited.Add(this))
                return false;

            if (TryGetOwnFont(style, out var ownFont) &&
                ownFont != null &&
                ownFont.GetGlyph(unicode, fontSize, outline, out glyph, out material))
            {
                font = ownFont;
                return true;
            }

            if (_fallbacks == null)
                return false;

            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null &&
                    fallback.TryResolveGlyph(unicode, fontSize, outline, style, visited, out font, out glyph, out material))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void EnsureGlyphs(string value, float fontSize, NowFontStyle style = NowFontStyle.Regular)
        {
            EnsureGlyphs(value, fontSize, 0f, style);
        }

        internal void EnsureGlyphs(string value, float fontSize, float outline, NowFontStyle style)
        {
            var visited = GetVisitCache();

            try
            {
                EnsureGlyphs(value, fontSize, outline, style, visited);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal void EnsureGlyphs(
            string value,
            float fontSize,
            NowFontStyle style,
            HashSet<NowFontAsset> visited)
        {
            EnsureGlyphs(value, fontSize, 0f, style, visited);
        }

        internal void EnsureGlyphs(
            string value,
            float fontSize,
            float outline,
            NowFontStyle style,
            HashSet<NowFontAsset> visited)
        {
            if (string.IsNullOrEmpty(value) || fontSize <= 0)
                return;

            if (this == null || !visited.Add(this))
                return;

            if (TryGetOwnFont(style, out var font) && font != null)
                font.EnsureGlyphs(value, fontSize, outline);

            if (_fallbacks == null)
                return;

            for (int i = 0; i < _fallbacks.Length; ++i)
            {
                var fallback = _fallbacks[i];

                if (fallback != null)
                    fallback.EnsureGlyphs(value, fontSize, outline, style, visited);
            }
        }

        public float GetLineHeight(NowFontStyle style = NowFontStyle.Regular)
        {
            var visited = GetVisitCache();

            try
            {
                return GetLineHeight(style, visited);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal float GetLineHeight(NowFontStyle style, HashSet<NowFontAsset> visited)
        {
            if (this == null || !visited.Add(this))
                return 1;

            if (TryGetOwnFont(style, out var font) && font != null)
                return font.GetLineHeight();

            if (_fallbacks != null)
            {
                for (int i = 0; i < _fallbacks.Length; ++i)
                {
                    var fallback = _fallbacks[i];

                    if (fallback != null)
                        return fallback.GetLineHeight(style, visited);
                }
            }

            return 1;
        }

        /// <summary>
        /// Distance from the top of a line box to its baseline, in em units. Used to
        /// position the first baseline so ascent and descent both fit inside the
        /// measured line height.
        /// </summary>
        public float GetAscender(NowFontStyle style = NowFontStyle.Regular)
        {
            var visited = GetVisitCache();

            try
            {
                return GetAscender(style, visited);
            }
            finally
            {
                visited.Clear();
            }
        }

        internal float GetAscender(NowFontStyle style, HashSet<NowFontAsset> visited)
        {
            if (this == null || !visited.Add(this))
                return 1;

            if (TryGetOwnFont(style, out var font) && font != null)
                return font.GetAscender();

            if (_fallbacks != null)
            {
                for (int i = 0; i < _fallbacks.Length; ++i)
                {
                    var fallback = _fallbacks[i];

                    if (fallback != null)
                        return fallback.GetAscender(style, visited);
                }
            }

            return 1;
        }

        public virtual Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
        {
            return MeasureText(value, fontSize, NowFontStyle.Regular, tabSpaces);
        }

        /// <summary>
        /// Cached \n/\t split of a measured string, plus a one-slot memo of the
        /// widest-line advance sum for the last (font, size, tab width) it was
        /// measured with. Repeated measures neither allocate substrings nor re-sum
        /// glyph advances; the memo stores the exact loop output, so hits are
        /// bit-identical to a recompute.
        /// </summary>
        sealed class ShapedMeasureSegmentation
        {
            public string[] segments;

            public char[] controls;

            public int lineCount;

            public int generation;

            public NowFont memoFont;

            public int memoFontVersion;

            public float memoFontSize;

            public int memoTabSpaces;

            public float memoMaxWidth;

            public bool hasMemo;

            public NowFont boundsMemoFont;

            public int boundsMemoFontVersion;

            public float boundsMemoFontSize;

            public NowFontStyle boundsMemoStyle;

            public int boundsMemoTabSpaces;

            public Vector4 boundsMemo;

            public bool hasBoundsMemo;
        }

        const int MEASURE_SEGMENT_CACHE_LIMIT = 2048;

        static readonly Dictionary<string, ShapedMeasureSegmentation> _measureSegmentCache =
            new Dictionary<string, ShapedMeasureSegmentation>(64);

        static readonly List<string> _measureSegmentEvictScratch = new List<string>();

        static int _measureSegmentGeneration;

        static ShapedMeasureSegmentation GetShapedMeasureSegmentation(string value)
        {
            if (_measureSegmentCache.TryGetValue(value, out var segmentation))
            {
                segmentation.generation = _measureSegmentGeneration;
                return segmentation;
            }

            int controlCount = 1;
            int lineCount = 1;

            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];

                if (character == '\n')
                {
                    ++controlCount;
                    ++lineCount;
                }
                else if (character == '\t' || character == '\r')
                {
                    ++controlCount;
                }
            }

            segmentation = new ShapedMeasureSegmentation
            {
                segments = new string[controlCount],
                controls = new char[controlCount],
                lineCount = lineCount,
                generation = _measureSegmentGeneration
            };

            int segmentStart = 0;
            int index = 0;

            for (int i = 0; i <= value.Length; ++i)
            {
                char control = i < value.Length ? value[i] : '\0';

                if (i < value.Length && control != '\n' && control != '\r' && control != '\t')
                    continue;

                if (i > segmentStart)
                {
                    segmentation.segments[index] = segmentStart == 0 && i == value.Length
                        ? value
                        : value.Substring(segmentStart, i - segmentStart);
                }

                segmentation.controls[index] = control;
                ++index;
                segmentStart = i + 1;
            }

            if (_measureSegmentCache.Count >= MEASURE_SEGMENT_CACHE_LIMIT)
                EvictStaleMeasureSegmentations();

            _measureSegmentCache[value] = segmentation;
            return segmentation;
        }

        /// <summary>Second-chance eviction: entries untouched since the previous sweep are
        /// dropped so hot strings survive the size cap instead of re-preparing in a burst
        /// when one dynamic string churns the cache.</summary>
        static void EvictStaleMeasureSegmentations()
        {
            _measureSegmentEvictScratch.Clear();

            foreach (var entry in _measureSegmentCache)
            {
                if (entry.Value.generation != _measureSegmentGeneration)
                    _measureSegmentEvictScratch.Add(entry.Key);
            }

            for (int i = 0; i < _measureSegmentEvictScratch.Count; ++i)
                _measureSegmentCache.Remove(_measureSegmentEvictScratch[i]);

            _measureSegmentEvictScratch.Clear();

            if (_measureSegmentCache.Count >= MEASURE_SEGMENT_CACHE_LIMIT)
                _measureSegmentCache.Clear();

            ++_measureSegmentGeneration;
        }

        /// <summary>
        /// Measures through the same shaped runs the draw path uses, so kerning and
        /// ligatures affect layout consistently. Advances only — nothing is baked.
        /// Returns false when any segment cannot shape; the caller then measures
        /// per codepoint, matching the draw path's fallback decision.
        /// </summary>
        internal bool TryMeasureShapedText(string value, float fontSize, NowFontStyle style, int tabSpaces, out Vector2 size)
        {
            size = default;

            if (!TryResolveFont(style, out var font) || font == null)
                return false;

            var segmentation = GetShapedMeasureSegmentation(value);

            if (segmentation.hasMemo &&
                ReferenceEquals(segmentation.memoFont, font) &&
                segmentation.memoFontVersion == font.shapedDataVersion &&
                segmentation.memoFontSize == fontSize &&
                segmentation.memoTabSpaces == tabSpaces)
            {
                size = new Vector2(segmentation.memoMaxWidth, GetLineHeight(style) * fontSize * segmentation.lineCount);
                return true;
            }

            float lineWidth = 0f;
            float maxWidth = 0f;
            float tabAdvance = -1f;
            var segments = segmentation.segments;
            var controls = segmentation.controls;

            for (int s = 0; s < segments.Length; ++s)
            {
                var segment = segments[s];

                if (segment != null)
                {
                    if (!font.TryGetShapedRun(segment, out var run))
                        return false;

                    for (int g = 0; g < run.Length; ++g)
                        lineWidth += run[g].xAdvance * fontSize;
                }

                char control = controls[s];

                if (control == '\n')
                {
                    if (lineWidth > maxWidth)
                        maxWidth = lineWidth;

                    lineWidth = 0f;
                }
                else if (control == '\t')
                {
                    if (tabAdvance < 0f)
                    {
                        if (!font.TryGetShapedRun(" ", out var spaceRun))
                            return false;

                        tabAdvance = 0f;

                        for (int g = 0; g < spaceRun.Length; ++g)
                            tabAdvance += spaceRun[g].xAdvance;

                        tabAdvance *= fontSize * tabSpaces;
                    }

                    lineWidth += tabAdvance;
                }
            }

            if (lineWidth > maxWidth)
                maxWidth = lineWidth;

            segmentation.memoFont = font;
            segmentation.memoFontVersion = font.shapedDataVersion;
            segmentation.memoFontSize = fontSize;
            segmentation.memoTabSpaces = tabSpaces;
            segmentation.memoMaxWidth = maxWidth;
            segmentation.hasMemo = true;

            size = new Vector2(maxWidth, GetLineHeight(style) * fontSize * segmentation.lineCount);
            return true;
        }

        public virtual Vector2 MeasureText(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
        {
            if (string.IsNullOrEmpty(value) || fontSize <= 0)
                return default;

            if (Now.textShaping && TryMeasureShapedText(value, fontSize, style, tabSpaces, out var shapedSize))
                return shapedSize;

            if (TryResolveFont(style, out var preparedFont) &&
                preparedFont != null &&
                preparedFont.TryGetPreparedCodepointRun(value, fontSize, style, tabSpaces, out var preparedRun))
            {
                return preparedFont.MeasurePreparedCodepointRun(preparedRun, fontSize, style);
            }

            float lineWidth = 0;
            float maxWidth = 0;
            int lineCount = 1;
            var visited = GetVisitCache();

            EnsureGlyphs(value, fontSize, style, visited);
            visited.Clear();

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = NowFont.ReadCodepoint(value, ref i);

                if (codepoint == '\n')
                {
                    if (lineWidth > maxWidth)
                        maxWidth = lineWidth;

                    lineWidth = 0;
                    ++lineCount;
                    continue;
                }

                if (codepoint == '\r')
                    continue;

                if (codepoint == '\t')
                {
                    visited.Clear();

                    if (TryResolveGlyph(' ', fontSize, style, visited, out _, out var space, out _))
                        lineWidth += space.advance * fontSize * tabSpaces;

                    continue;
                }

                visited.Clear();

                if (TryResolveGlyph(codepoint, fontSize, style, visited, out _, out var glyph, out _))
                    lineWidth += glyph.advance * fontSize;
            }

            if (lineWidth > maxWidth)
                maxWidth = lineWidth;

            visited.Clear();
            float lineHeight = GetLineHeight(style, visited);
            visited.Clear();
            return new Vector2(maxWidth, lineHeight * fontSize * lineCount);
        }

        /// <summary>
        /// Measures a range of the string without allocating a substring —
        /// word-wrap engines measure candidate words straight off the source
        /// text. Per-codepoint advances, like the span overload.
        /// </summary>
        public Vector2 MeasureText(string value, int start, int length, float fontSize, NowFontStyle style = NowFontStyle.Regular, int tabSpaces = 4)
        {
            if (string.IsNullOrEmpty(value) || start < 0 || length <= 0 || start + length > value.Length)
                return default;

            return MeasureText(System.MemoryExtensions.AsSpan(value, start, length), fontSize, style, tabSpaces);
        }

        /// <summary>
        /// Span measure for dynamic text (counters, timers) without allocating a
        /// string. Per-codepoint advances only — shaping does not apply, matching
        /// the span draw path. A plain <see cref="NowFont"/> with no fallbacks
        /// resolves glyphs directly, skipping the visited-set walk per codepoint.
        /// </summary>
        public Vector2 MeasureText(System.ReadOnlySpan<char> value, float fontSize, NowFontStyle style = NowFontStyle.Regular, int tabSpaces = 4)
        {
            if (value.IsEmpty || fontSize <= 0)
                return default;

            var directFont = this as NowFont;
            bool direct = directFont != null && (_fallbacks == null || _fallbacks.Length == 0);

            float lineWidth = 0;
            float maxWidth = 0;
            int lineCount = 1;
            var visited = direct ? null : GetVisitCache();

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = NowFont.ReadCodepoint(value, ref i);

                if (codepoint == '\n')
                {
                    if (lineWidth > maxWidth)
                        maxWidth = lineWidth;

                    lineWidth = 0;
                    ++lineCount;
                    continue;
                }

                if (codepoint == '\r')
                    continue;

                if (codepoint == '\t')
                {
                    if (direct)
                    {
                        if (directFont.GetGlyph(' ', fontSize, out var space, out Material _))
                            lineWidth += space.advance * fontSize * tabSpaces;
                    }
                    else
                    {
                        visited.Clear();

                        if (TryResolveGlyph(' ', fontSize, style, visited, out _, out var space, out _))
                            lineWidth += space.advance * fontSize * tabSpaces;
                    }

                    continue;
                }

                if (direct)
                {
                    if (directFont.GetGlyph(codepoint, fontSize, out var glyph, out Material _))
                        lineWidth += glyph.advance * fontSize;

                    continue;
                }

                visited.Clear();

                if (TryResolveGlyph(codepoint, fontSize, style, visited, out _, out var glyph2, out _))
                    lineWidth += glyph2.advance * fontSize;
            }

            if (lineWidth > maxWidth)
                maxWidth = lineWidth;

            float lineHeight;

            if (direct)
            {
                lineHeight = directFont.GetLineHeight();
            }
            else
            {
                visited.Clear();
                lineHeight = GetLineHeight(style, visited);
                visited.Clear();
            }

            return new Vector2(maxWidth, lineHeight * fontSize * lineCount);
        }

        /// <summary>
        /// Advance of one codepoint in scaled units — exactly what the span
        /// measure path adds for it. Tabs advance by the space advance times
        /// <paramref name="tabSpaces"/>; newlines and unresolvable codepoints
        /// are 0. Allocation-free, for callers stepping through dynamic text
        /// that would otherwise measure one-character strings.
        /// </summary>
        internal float GetCodepointAdvance(int codepoint, float fontSize, NowFontStyle style = NowFontStyle.Regular, int tabSpaces = 4)
        {
            if (fontSize <= 0 || codepoint <= 0 || codepoint == '\n' || codepoint == '\r')
                return 0f;

            bool isTab = codepoint == '\t';
            int resolved = isTab ? ' ' : codepoint;
            var directFont = this as NowFont;

            if (directFont != null && (_fallbacks == null || _fallbacks.Length == 0))
            {
                if (!directFont.GetGlyph(resolved, fontSize, out var directGlyph, out Material _))
                    return 0f;

                float directAdvance = directGlyph.advance * fontSize;
                return isTab ? directAdvance * tabSpaces : directAdvance;
            }

            var visited = GetVisitCache();

            try
            {
                if (!TryResolveGlyph(resolved, fontSize, style, visited, out _, out var glyph, out _))
                    return 0f;

                float advance = glyph.advance * fontSize;
                return isTab ? advance * tabSpaces : advance;
            }
            finally
            {
                visited.Clear();
            }
        }

        public virtual Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
        {
            return MeasureTextBounds(value, fontSize, NowFontStyle.Regular, tabSpaces);
        }

        /// <summary>
        /// Shaped equivalent of <see cref="MeasureTextBounds(string,float,NowFontStyle,int)"/>;
        /// bakes the shaped records (like the codepoint path bakes via glyph
        /// resolution) so plane bounds are available.
        /// </summary>
        internal bool TryMeasureShapedTextBounds(string value, float fontSize, NowFontStyle style, int tabSpaces, out Vector4 bounds)
        {
            bounds = default;

            if (!TryResolveFont(style, out var font) || font == null)
                return false;

            var segmentation = GetShapedMeasureSegmentation(value);

            if (segmentation.hasBoundsMemo &&
                ReferenceEquals(segmentation.boundsMemoFont, font) &&
                segmentation.boundsMemoFontVersion == font.shapedDataVersion &&
                segmentation.boundsMemoFontSize == fontSize &&
                segmentation.boundsMemoStyle == style &&
                segmentation.boundsMemoTabSpaces == tabSpaces)
            {
                bounds = segmentation.boundsMemo;
                return true;
            }

            float cursorX = 0f;
            float lineY = 0f;
            float lineHeight = GetLineHeight(style) * fontSize;
            float baseline = GetAscender(style) * fontSize;
            float tabAdvance = -1f;
            float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;
            bool hasBounds = false;
            var segments = segmentation.segments;
            var controls = segmentation.controls;

            for (int s = 0; s < segments.Length; ++s)
            {
                var segment = segments[s];

                if (segment != null)
                {
                    if (!font.TryGetShapedRun(segment, out var run) || !font.EnsureShapedGlyphs(run, fontSize))
                        return false;

                    for (int g = 0; g < run.Length; ++g)
                    {
                        var shaped = run[g];

                        if (!font.TryGetShapedGlyph((int)shaped.glyphIndex, fontSize, out var glyph, out _))
                            return false;

                        if (glyph.atlasBounds.left != glyph.atlasBounds.right)
                        {
                            float penX = cursorX + shaped.xOffset * fontSize;
                            float penY = lineY - shaped.yOffset * fontSize;
                            float glyphLeft = penX + glyph.planeBounds.left * fontSize;
                            float glyphRight = penX + glyph.planeBounds.right * fontSize;
                            float glyphTop = penY + baseline - glyph.planeBounds.top * fontSize;
                            float glyphBottom = penY + baseline - glyph.planeBounds.bottom * fontSize;

                            if (!hasBounds)
                            {
                                minX = glyphLeft;
                                minY = glyphTop;
                                maxX = glyphRight;
                                maxY = glyphBottom;
                                hasBounds = true;
                            }
                            else
                            {
                                if (glyphLeft < minX) minX = glyphLeft;
                                if (glyphTop < minY) minY = glyphTop;
                                if (glyphRight > maxX) maxX = glyphRight;
                                if (glyphBottom > maxY) maxY = glyphBottom;
                            }
                        }

                        cursorX += shaped.xAdvance * fontSize;
                    }
                }

                char control = controls[s];

                if (control == '\n')
                {
                    cursorX = 0f;
                    lineY += lineHeight;
                }
                else if (control == '\t')
                {
                    if (tabAdvance < 0f)
                    {
                        if (!font.TryGetShapedRun(" ", out var spaceRun))
                            return false;

                        tabAdvance = 0f;

                        for (int g = 0; g < spaceRun.Length; ++g)
                            tabAdvance += spaceRun[g].xAdvance;

                        tabAdvance *= fontSize * tabSpaces;
                    }

                    cursorX += tabAdvance;
                }
            }

            bounds = hasBounds ? new Vector4(minX, minY, maxX - minX, maxY - minY) : default;
            segmentation.boundsMemoFont = font;
            segmentation.boundsMemoFontVersion = font.shapedDataVersion;
            segmentation.boundsMemoFontSize = fontSize;
            segmentation.boundsMemoStyle = style;
            segmentation.boundsMemoTabSpaces = tabSpaces;
            segmentation.boundsMemo = bounds;
            segmentation.hasBoundsMemo = true;
            return true;
        }

        public virtual Vector4 MeasureTextBounds(string value, float fontSize, NowFontStyle style, int tabSpaces = 4)
        {
            if (string.IsNullOrEmpty(value) || fontSize <= 0)
                return default;

            if (Now.textShaping && TryMeasureShapedTextBounds(value, fontSize, style, tabSpaces, out var shapedBounds))
                return shapedBounds;

            if (TryResolveFont(style, out var preparedFont) &&
                preparedFont != null &&
                preparedFont.TryGetPreparedCodepointRun(value, fontSize, style, tabSpaces, out var preparedRun))
            {
                return preparedFont.MeasurePreparedCodepointRunBounds(preparedRun, fontSize, style);
            }

            float cursorX = 0;
            float lineY = 0;
            float lineHeight = 0;
            float baseline = 0;
            float minX = 0;
            float minY = 0;
            float maxX = 0;
            float maxY = 0;
            bool hasBounds = false;
            var visited = GetVisitCache();

            EnsureGlyphs(value, fontSize, style, visited);
            visited.Clear();

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = NowFont.ReadCodepoint(value, ref i);

                if (codepoint == '\n')
                {
                    if (lineHeight <= 0)
                    {
                        visited.Clear();
                        lineHeight = GetLineHeight(style, visited) * fontSize;
                        visited.Clear();
                        baseline = GetAscender(style, visited) * fontSize;
                    }

                    cursorX = 0;
                    lineY += lineHeight;
                    continue;
                }

                if (codepoint == '\r')
                    continue;

                if (codepoint == '\t')
                {
                    visited.Clear();

                    if (TryResolveGlyph(' ', fontSize, style, visited, out _, out var space, out _))
                        cursorX += space.advance * fontSize * tabSpaces;

                    continue;
                }

                visited.Clear();

                if (!TryResolveGlyph(codepoint, fontSize, style, visited, out _, out var glyph, out _))
                    continue;

                if (lineHeight <= 0)
                {
                    visited.Clear();
                    lineHeight = GetLineHeight(style, visited) * fontSize;
                    visited.Clear();
                    baseline = GetAscender(style, visited) * fontSize;
                }

                if (glyph.atlasBounds.left != glyph.atlasBounds.right)
                {
                    float glyphLeft = cursorX + glyph.planeBounds.left * fontSize;
                    float glyphRight = cursorX + glyph.planeBounds.right * fontSize;
                    float glyphTop = lineY + baseline - glyph.planeBounds.top * fontSize;
                    float glyphBottom = lineY + baseline - glyph.planeBounds.bottom * fontSize;

                    if (!hasBounds)
                    {
                        minX = glyphLeft;
                        minY = glyphTop;
                        maxX = glyphRight;
                        maxY = glyphBottom;
                        hasBounds = true;
                    }
                    else
                    {
                        if (glyphLeft < minX)
                            minX = glyphLeft;

                        if (glyphTop < minY)
                            minY = glyphTop;

                        if (glyphRight > maxX)
                            maxX = glyphRight;

                        if (glyphBottom > maxY)
                            maxY = glyphBottom;
                    }
                }

                cursorX += glyph.advance * fontSize;
            }

            visited.Clear();
            return hasBounds ? new Vector4(minX, minY, maxX - minX, maxY - minY) : default;
        }
    }

    [PreferBinarySerialization]
    public class NowFont : NowFontAsset
    {
        public const string ATLAS_TYPE_MTSDF = "mtsdf";
        public const string ATLAS_TYPE_RGBA = "rgba";
        public const int DEFAULT_DYNAMIC_ATLAS_SIZE = 64;
        public const int DEFAULT_DYNAMIC_PIXEL_RANGE = 16;
        public const int DEFAULT_DYNAMIC_PAGE_SIZE = 1024;
        public const int DEFAULT_DYNAMIC_MAX_ATLAS_SIZE = 2048;
        public const int DEFAULT_DYNAMIC_MAX_ATLAS_BYTES = 16 * 1024 * 1024;
        internal const long DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES = 64L * 1024 * 1024;
        internal const float MAX_OUTLINE_RANGE_FRACTION = 0.45f;

        internal static float GetSafeSdfEffectReach(float screenPixelRange)
        {
            if (float.IsNaN(screenPixelRange) ||
                float.IsInfinity(screenPixelRange) ||
                screenPixelRange <= 0f)
            {
                return 0f;
            }

            // Half a coverage unit is the mathematical minimum at 1:1. Keep at
            // least one full unit and 5% of the encoded field for filtering,
            // antialiasing, and scaled draws.
            float guard = Mathf.Max(
                1f,
                screenPixelRange * (0.5f - MAX_OUTLINE_RANGE_FRACTION));
            return Mathf.Max(0f, screenPixelRange * 0.5f - guard);
        }

        float ScreenPixelRange(float fontSize, NowFontAtlasInfo.Atlas fontAtlas)
        {
            int atlasSize = fontAtlas.size > 0 ? fontAtlas.size : DEFAULT_DYNAMIC_ATLAS_SIZE;
            int pixelRange = fontAtlas.distanceRange > 0
                ? fontAtlas.distanceRange
                : dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE;
            return fontSize / atlasSize * pixelRange;
        }
        static readonly int SDF_ENCODING_PROPERTY = Shader.PropertyToID("_NowUITextSdfEncoding");
        static readonly int OUTLINE_ONLY_PASS_PROPERTY = Shader.PropertyToID("_NowUITextOutlineOnlyPass");
        const uint OPENTYPE_TTC_TAG = 0x74746366;
        const int DYNAMIC_GLYPH_PADDING = 1;
        const int MAX_CMAP_ENCODING_RECORDS = 1024;
        const int MAX_DYNAMIC_SOURCE_CMAP_CODEPOINTS = 200000;

        [HideInInspector]
        public Texture2D atlas;

        [HideInInspector]
        public NowFontAtlasInfo atlasInfo;

        [HideInInspector]
        public Material material;

        [SerializeField, HideInInspector]
        byte[] _fontBytes;

        public int dynamicAtlasSize = DEFAULT_DYNAMIC_ATLAS_SIZE;

        public int dynamicPixelRange = DEFAULT_DYNAMIC_PIXEL_RANGE;

        public int dynamicPageSize = DEFAULT_DYNAMIC_PAGE_SIZE;

        public int dynamicMaxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;

        public int dynamicMaxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

        public bool isColor => atlasInfo.atlas.type == ATLAS_TYPE_RGBA;

        class DynamicAtlasPage
        {
            public NowFont font;
            public HashSet<int> codepoints;
            public int atlasSize;
            public int pixelRange;
            public int cursorX;
            public int cursorY;
            public int rowHeight;
            public int materialId = -1;
            /// <summary>Pages owned by a native baking session are repacked and re-uploaded wholesale from
            /// native atlas storage; the legacy cursor-based packer must never write into them.</summary>
            public bool sessionOwned;
        }

        sealed class DynamicGlyphAppendBatch
        {
            readonly Dictionary<DynamicAtlasPage, List<NowFontAtlasInfo.Glyph>> _pendingGlyphs = new Dictionary<DynamicAtlasPage, List<NowFontAtlasInfo.Glyph>>();
            readonly HashSet<DynamicAtlasPage> _dirtyPages = new HashSet<DynamicAtlasPage>();

            public void AddGlyph(DynamicAtlasPage page, NowFontAtlasInfo.Glyph glyph)
            {
                if (!_pendingGlyphs.TryGetValue(page, out var glyphs))
                {
                    glyphs = new List<NowFontAtlasInfo.Glyph>();
                    _pendingGlyphs[page] = glyphs;
                }

                glyphs.Add(glyph);
            }

            public void MarkTextureDirty(DynamicAtlasPage page)
            {
                _dirtyPages.Add(page);
            }

            public void Commit()
            {
                foreach (var entry in _pendingGlyphs)
                {
                    var page = entry.Key;

                    if (!IsDynamicPageValid(page))
                        continue;

                    var fontAtlasInfo = page.font.atlasInfo;
                    AppendGlyphs(ref fontAtlasInfo.glyphs, entry.Value);
                    page.font.atlasInfo = fontAtlasInfo;
                    page.font.ClearGlyphCache();
                }

                foreach (var page in _dirtyPages)
                {
                    if (IsDynamicPageValid(page))
                        page.font.atlas.Apply(page.font.isColor, false);
                }
            }
        }

        readonly struct DynamicGlyphKey : IEquatable<DynamicGlyphKey>
        {
            readonly int _unicode;
            readonly int _atlasSize;
            readonly int _pixelRange;

            public DynamicGlyphKey(int unicode, int atlasSize, int pixelRange)
            {
                _unicode = unicode;
                _atlasSize = atlasSize;
                _pixelRange = pixelRange;
            }

            public bool Equals(DynamicGlyphKey other)
            {
                return _unicode == other._unicode &&
                    _atlasSize == other._atlasSize &&
                    _pixelRange == other._pixelRange;
            }

            public override bool Equals(object obj)
            {
                return obj is DynamicGlyphKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (_unicode * 397) ^ _atlasSize;
                    return (hash * 397) ^ _pixelRange;
                }
            }
        }

        readonly struct DynamicAtlasVariant : IEquatable<DynamicAtlasVariant>
        {
            public readonly int atlasSize;
            public readonly int pixelRange;

            public DynamicAtlasVariant(int atlasSize, int pixelRange)
            {
                this.atlasSize = atlasSize;
                this.pixelRange = pixelRange;
            }

            public bool Equals(DynamicAtlasVariant other)
            {
                return atlasSize == other.atlasSize && pixelRange == other.pixelRange;
            }

            public override bool Equals(object obj)
            {
                return obj is DynamicAtlasVariant other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (atlasSize * 397) ^ pixelRange;
            }
        }

        sealed class DynamicSessionState
        {
            public NowFontCompiler.DynamicSession session;
            public DynamicAtlasPage page;
            public bool failed;
            public int minimumPageSide;
            public long reservedPageBytes;
            public long lastUse;
        }

        [NonSerialized]
        NowFontAtlasInfo.Glyph[] _denseGlyphTable;

        [NonSerialized]
        Dictionary<int, NowFontAtlasInfo.Glyph> _sparseGlyphTable;

        [NonSerialized]
        Material _dynamicMaterialTemplate;

        [NonSerialized]
        HashSet<DynamicGlyphKey> _dynamicMisses;

        [NonSerialized]
        HashSet<DynamicGlyphKey> _dynamicCapacityMisses;

        [NonSerialized]
        List<DynamicAtlasPage> _dynamicPages;

        [NonSerialized]
        Dictionary<DynamicGlyphKey, DynamicAtlasPage> _dynamicGlyphPages;

        [NonSerialized]
        Dictionary<DynamicGlyphKey, DynamicAtlasPage> _dynamicFallbackGlyphPages;

        [NonSerialized]
        Dictionary<int, NowFontAtlasInfo.Glyph> _dynamicColorLayoutGlyphs;

        [NonSerialized]
        NowFontAtlasInfo.Metrics _dynamicColorLayoutMetrics;

        [NonSerialized]
        bool _hasDynamicColorLayoutMetrics;

        [NonSerialized]
        int[] _dynamicColorBitmapSizes;

        [NonSerialized]
        bool _didReadDynamicColorBitmapSizes;

        [NonSerialized]
        HashSet<int> _dynamicSourceCodepoints;

        [NonSerialized]
        bool _didReadDynamicSourceCodepoints;

        [NonSerialized]
        HashSet<int> _dynamicCodepointScratch;

        [NonSerialized]
        StringBuilder _dynamicStringBuilder;

        [NonSerialized]
        int[] _dynamicCompileCodepoints;

        [NonSerialized]
        Dictionary<DynamicAtlasVariant, DynamicSessionState> _dynamicSessions;

        [NonSerialized]
        long _dynamicSessionUseClock;

        [NonSerialized]
        internal long dynamicCacheBudgetBytesOverride;

        [NonSerialized]
        bool? _dynamicSourceIsColor;

        [NonSerialized]
        List<NowFontAtlasInfo.Glyph> _dynamicSessionGlyphScratch;

        [NonSerialized]
        int[] _dynamicSessionChunkScratch;

        [NonSerialized]
        HashSet<int> _dynamicSessionReturnedScratch;

        static bool s_dynamicSessionUnsupported;

        [NonSerialized]
        int _glyphTableOffset;

        [NonSerialized]
        public int materialId = -1;

        const int MAX_DENSE_GLYPH_RANGE = 4096;

        byte[] DynamicFontBytes => _fontBytes != null && _fontBytes.Length > 0 ? _fontBytes : null;

        public bool HasEmbeddedSource => DynamicFontBytes != null;

        internal bool supportsOutlineOnlyPass
        {
            get
            {
                var textMaterial = material != null ? material : _dynamicMaterialTemplate;

                if (textMaterial == null && HasEmbeddedSource)
                    textMaterial = Now.LoadRequiredResource<Material>("NowUI/TxtMaterial");

                return textMaterial != null &&
                    textMaterial.HasProperty(OUTLINE_ONLY_PASS_PROPERTY) &&
                    textMaterial.GetFloat(OUTLINE_ONLY_PASS_PROPERTY) > 0.5f;
            }
        }

        public int GetSourceByteCount()
        {
            return DynamicFontBytes?.Length ?? 0;
        }

        public bool TryGetSourceBytes(out byte[] fontBytes)
        {
            var source = DynamicFontBytes;

            if (source == null)
            {
                fontBytes = null;
                return false;
            }

            fontBytes = (byte[])source.Clone();
            return true;
        }

        public int GetCachedDynamicPageCount()
        {
            return _dynamicPages?.Count ?? 0;
        }

        public int GetCachedDynamicGlyphCount()
        {
            return _dynamicGlyphPages?.Count ?? 0;
        }

        public void GetCachedDynamicAtlasTextures(List<Texture2D> atlases)
        {
            if (atlases == null)
                return;

            atlases.Clear();

            if (_dynamicPages == null)
                return;

            for (int i = 0; i < _dynamicPages.Count; ++i)
            {
                var page = _dynamicPages[i];

                if (IsDynamicPageValid(page))
                    atlases.Add(page.font.atlas);
            }
        }

        internal long GetDynamicCacheBudgetBytes()
        {
            return dynamicCacheBudgetBytesOverride > 0
                ? dynamicCacheBudgetBytesOverride
                : DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES;
        }

        static long GetRgbaTexturePayloadBytes(int width, int height, int mipCount)
        {
            long bytes = 0;
            int mipWidth = Mathf.Max(1, width);
            int mipHeight = Mathf.Max(1, height);
            int levels = Mathf.Max(1, mipCount);

            for (int mip = 0; mip < levels; ++mip)
            {
                bytes += (long)mipWidth * mipHeight * 4;

                if (mipWidth == 1 && mipHeight == 1)
                    break;

                mipWidth = Mathf.Max(1, mipWidth >> 1);
                mipHeight = Mathf.Max(1, mipHeight >> 1);
            }

            return bytes;
        }

        static int GetFullMipCount(int width, int height)
        {
            int levels = 1;
            int mipWidth = Mathf.Max(1, width);
            int mipHeight = Mathf.Max(1, height);

            while (mipWidth > 1 || mipHeight > 1)
            {
                mipWidth = Mathf.Max(1, mipWidth >> 1);
                mipHeight = Mathf.Max(1, mipHeight >> 1);
                ++levels;
            }

            return levels;
        }

        static long GetRgbaTexturePayloadBytes(Texture2D texture)
        {
            return texture != null
                ? GetRgbaTexturePayloadBytes(texture.width, texture.height, texture.mipmapCount)
                : 0;
        }

        /// <summary>Conservative retained-memory accounting for the internal dynamic
        /// font budget. Published GPU pages count once; readable texture storage adds
        /// another copy; every live compiler session reserves both its atlas and one
        /// atlas-sized working buffer.</summary>
        internal long GetEstimatedDynamicCacheResidentBytes()
        {
            long bytes = 0;

            if (_dynamicPages != null)
            {
                for (int i = 0; i < _dynamicPages.Count; ++i)
                {
                    var texture = _dynamicPages[i]?.font != null
                        ? _dynamicPages[i].font.atlas
                        : null;

                    if (texture == null)
                        continue;

                    long payload = GetRgbaTexturePayloadBytes(texture);
                    bytes += payload;

                    if (texture.isReadable)
                        bytes += payload;
                }
            }

            if (_dynamicSessions != null)
            {
                foreach (var state in _dynamicSessions.Values)
                {
                    if (state?.session != null)
                    {
                        long payload = GetRgbaTexturePayloadBytes(
                            state.session.AtlasSide,
                            state.session.AtlasSide,
                            1);
                        bytes += payload * 2;
                    }

                    bytes += state?.reservedPageBytes ?? 0;
                }
            }

            return bytes;
        }

        internal bool IsDynamicGlyphCapacityBlocked(int unicode, int atlasSize, int pixelRange)
        {
            return _dynamicCapacityMisses != null &&
                _dynamicCapacityMisses.Contains(new DynamicGlyphKey(unicode, atlasSize, pixelRange));
        }

        internal bool IsDynamicGlyphMissing(int unicode, int atlasSize, int pixelRange)
        {
            return _dynamicMisses != null &&
                _dynamicMisses.Contains(new DynamicGlyphKey(unicode, atlasSize, pixelRange));
        }

        protected override bool TryGetOwnFont(NowFontStyle style, out NowFont font)
        {
            font = this;
            return font != null;
        }

        void OnDisable()
        {
            ResetDynamicSessions(true);
        }

        void OnDestroy()
        {
            // Runtime pages are owned Unity objects rather than serialized
            // sub-assets. Destroying their parent font must release them even
            // when the caller did not explicitly clear the cache first.
            ClearDynamicCache();
            _textShaper?.Dispose();
            _textShaper = null;
        }

        public void ClearDynamicCache()
        {
            ++_shapedDataVersion;
            ResetDynamicSessions(false);
            ClearPreparedShapeCache();
            ClearPreparedCodepointCache();

            if (_dynamicPages != null)
            {
                for (int i = 0; i < _dynamicPages.Count; ++i)
                    DestroyDynamicPage(_dynamicPages[i]);
            }

            _dynamicMisses = null;
            _dynamicCapacityMisses = null;
            _dynamicPages = null;
            _dynamicGlyphPages = null;
            _dynamicFallbackGlyphPages = null;
            _dynamicColorLayoutGlyphs = null;
            _dynamicColorLayoutMetrics = default;
            _hasDynamicColorLayoutMetrics = false;
            _dynamicSessionGlyphScratch = null;
            _dynamicSessionChunkScratch = null;
            _dynamicSessionReturnedScratch = null;
            _dynamicSessionUseClock = 0;
            materialId = -1;
        }

        public static int ReadCodepoint(string value, ref int index)
        {
            if (string.IsNullOrEmpty(value) || index < 0 || index >= value.Length)
                return -1;

            char character = value[index];

            if (char.IsHighSurrogate(character) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                ++index;
                return char.ConvertToUtf32(character, value[index]);
            }

            return character;
        }

        public static int ReadCodepoint(System.ReadOnlySpan<char> value, ref int index)
        {
            if (index < 0 || index >= value.Length)
                return -1;

            char character = value[index];

            if (char.IsHighSurrogate(character) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                ++index;
                return char.ConvertToUtf32(character, value[index]);
            }

            return character;
        }

        static void NormalizeGlyphAtlasBounds(ref NowFontAtlasInfo.Glyph glyph, Texture2D atlas)
        {
            glyph.atlasBounds.left /= atlas.width;
            glyph.atlasBounds.right /= atlas.width;
            glyph.atlasBounds.top /= atlas.height;
            glyph.atlasBounds.bottom /= atlas.height;
        }

        internal static string CodepointToString(int codepoint)
        {
            return codepoint <= char.MaxValue
                ? ((char)codepoint).ToString()
                : char.ConvertFromUtf32(codepoint);
        }

        static void AppendCodepoint(StringBuilder builder, int codepoint)
        {
            if (builder == null || !IsValidUnicodeScalar(codepoint))
                return;

            if (codepoint <= char.MaxValue)
            {
                builder.Append((char)codepoint);
                return;
            }

            int scalar = codepoint - 0x10000;
            builder.Append((char)((scalar >> 10) + 0xd800));
            builder.Append((char)((scalar & 0x3ff) + 0xdc00));
        }

        static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                ((uint)data[offset + 1] << 16) |
                ((uint)data[offset + 2] << 8) |
                data[offset + 3];
        }

        static bool CanRead(byte[] data, int offset, int length)
        {
            return data != null &&
                offset >= 0 &&
                length >= 0 &&
                offset <= data.Length &&
                length <= data.Length - offset;
        }

        static bool TryGetOpenTypeTable(byte[] fontData, string tag, out int tableOffset, out int tableLength)
        {
            tableOffset = 0;
            tableLength = 0;

            if (fontData == null || fontData.Length < 12 || string.IsNullOrEmpty(tag) || tag.Length != 4)
                return false;

            int sfntOffset = 0;

            if (ReadUInt32BigEndian(fontData, 0) == OPENTYPE_TTC_TAG)
            {
                if (!CanRead(fontData, 0, 16) || ReadUInt32BigEndian(fontData, 8) == 0)
                    return false;

                uint firstFontOffset = ReadUInt32BigEndian(fontData, 12);

                if (firstFontOffset > int.MaxValue)
                    return false;

                sfntOffset = (int)firstFontOffset;
            }

            if (!CanRead(fontData, sfntOffset, 12))
                return false;

            int tableCount = ReadUInt16BigEndian(fontData, sfntOffset + 4);
            int recordsOffset = sfntOffset + 12;
            int directoryEnd = recordsOffset + tableCount * 16;

            if (tableCount <= 0 || directoryEnd < recordsOffset || directoryEnd > fontData.Length)
                return false;

            byte tag0 = (byte)tag[0];
            byte tag1 = (byte)tag[1];
            byte tag2 = (byte)tag[2];
            byte tag3 = (byte)tag[3];

            for (int i = 0; i < tableCount; ++i)
            {
                int offset = recordsOffset + i * 16;

                if (fontData[offset] != tag0 ||
                    fontData[offset + 1] != tag1 ||
                    fontData[offset + 2] != tag2 ||
                    fontData[offset + 3] != tag3)
                {
                    continue;
                }

                uint rawOffset = ReadUInt32BigEndian(fontData, offset + 8);
                uint rawLength = ReadUInt32BigEndian(fontData, offset + 12);

                if (rawOffset > int.MaxValue || rawLength > int.MaxValue)
                    return false;

                tableOffset = (int)rawOffset;
                tableLength = (int)rawLength;

                return tableOffset >= 0 &&
                    tableLength >= 0 &&
                    tableOffset <= fontData.Length &&
                    tableLength <= fontData.Length - tableOffset;
            }

            return false;
        }

        static bool TryReadDynamicSourceCodepoints(byte[] fontData, out HashSet<int> codepoints)
        {
            codepoints = null;

            if (!TryGetOpenTypeTable(fontData, "cmap", out var cmapOffset, out var cmapLength))
                return false;

            if (!CanRead(fontData, cmapOffset, 4))
                return false;

            int cmapEnd = cmapOffset + cmapLength;
            int encodingCount = ReadUInt16BigEndian(fontData, cmapOffset + 2);
            int recordsEnd = cmapOffset + 4 + encodingCount * 8;

            if (encodingCount <= 0 ||
                encodingCount > MAX_CMAP_ENCODING_RECORDS ||
                recordsEnd < cmapOffset ||
                recordsEnd > cmapEnd)
            {
                return false;
            }

            int bestScore = 0;
            int bestSubtableOffset = 0;
            int bestSubtableLength = 0;

            for (int i = 0; i < encodingCount; ++i)
            {
                int recordOffset = cmapOffset + 4 + i * 8;
                int platformId = ReadUInt16BigEndian(fontData, recordOffset);
                int encodingId = ReadUInt16BigEndian(fontData, recordOffset + 2);
                uint subtableRelativeOffset = ReadUInt32BigEndian(fontData, recordOffset + 4);

                if (subtableRelativeOffset > int.MaxValue)
                    continue;

                int subtableOffset = cmapOffset + (int)subtableRelativeOffset;

                if (subtableOffset < cmapOffset || subtableOffset >= cmapEnd || !CanRead(fontData, subtableOffset, 2))
                    continue;

                int format = ReadUInt16BigEndian(fontData, subtableOffset);
                int score = GetCmapCoverageScore(format, platformId, encodingId);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestSubtableOffset = subtableOffset;
                bestSubtableLength = cmapEnd - subtableOffset;
            }

            return bestScore > 0 &&
                TryReadCmapCoverageSubtable(fontData, bestSubtableOffset, bestSubtableLength, out codepoints);
        }

        static int GetCmapCoverageScore(int format, int platformId, int encodingId)
        {
            int score;

            switch (format)
            {
                case 12:
                    score = 500;
                    break;
                case 13:
                    score = 490;
                    break;
                case 4:
                    score = 400;
                    break;
                case 6:
                    score = 200;
                    break;
                case 0:
                    score = 100;
                    break;
                default:
                    return 0;
            }

            if (platformId == 3 && encodingId == 10)
                score += 90;
            else if (platformId == 0)
                score += 80;
            else if (platformId == 3 && encodingId == 1)
                score += 60;
            else if (platformId == 3)
                score += 20;

            return score;
        }

        static bool TryReadCmapCoverageSubtable(
            byte[] fontData,
            int offset,
            int maxLength,
            out HashSet<int> codepoints)
        {
            codepoints = null;

            if (maxLength < 2 || !CanRead(fontData, offset, 2))
                return false;

            int format = ReadUInt16BigEndian(fontData, offset);

            switch (format)
            {
                case 0:
                    return TryReadCmapFormat0Coverage(fontData, offset, maxLength, out codepoints);
                case 4:
                    return TryReadCmapFormat4Coverage(fontData, offset, maxLength, out codepoints);
                case 6:
                    return TryReadCmapFormat6Coverage(fontData, offset, maxLength, out codepoints);
                case 12:
                    return TryReadCmapFormat12Coverage(fontData, offset, maxLength, false, out codepoints);
                case 13:
                    return TryReadCmapFormat12Coverage(fontData, offset, maxLength, true, out codepoints);
                default:
                    return false;
            }
        }

        static bool TryReadCmapFormat0Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
        {
            codepoints = null;

            if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 262)
                return false;

            var coverage = new HashSet<int>();

            for (int codepoint = 0; codepoint < 256; ++codepoint)
            {
                int glyphIndex = fontData[offset + 6 + codepoint];

                if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, codepoint))
                    return false;
            }

            codepoints = coverage;
            return true;
        }

        static bool TryReadCmapFormat4Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
        {
            codepoints = null;

            if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 16)
                return false;

            int segCount = ReadUInt16BigEndian(fontData, offset + 6) / 2;

            if (segCount <= 0)
                return false;

            int endCodeOffset = offset + 14;
            int startCodeOffset = endCodeOffset + segCount * 2 + 2;
            int idDeltaOffset = startCodeOffset + segCount * 2;
            int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
            int tableEnd = offset + length;

            if (idRangeOffsetOffset + segCount * 2 > tableEnd)
                return false;

            var coverage = new HashSet<int>();

            for (int segment = 0; segment < segCount; ++segment)
            {
                int endCode = ReadUInt16BigEndian(fontData, endCodeOffset + segment * 2);
                int startCode = ReadUInt16BigEndian(fontData, startCodeOffset + segment * 2);
                int idDelta = unchecked((short)ReadUInt16BigEndian(fontData, idDeltaOffset + segment * 2));
                int idRangeOffsetAddress = idRangeOffsetOffset + segment * 2;
                int idRangeOffset = ReadUInt16BigEndian(fontData, idRangeOffsetAddress);

                if (startCode > endCode)
                    continue;

                for (int codepoint = startCode; codepoint <= endCode; ++codepoint)
                {
                    if (codepoint == 0xffff)
                        continue;

                    int glyphIndex;

                    if (idRangeOffset == 0)
                    {
                        glyphIndex = (codepoint + idDelta) & 0xffff;
                    }
                    else
                    {
                        int glyphIndexAddress = idRangeOffsetAddress + idRangeOffset + (codepoint - startCode) * 2;

                        if (!CanRead(fontData, glyphIndexAddress, 2) || glyphIndexAddress + 2 > tableEnd)
                            continue;

                        glyphIndex = ReadUInt16BigEndian(fontData, glyphIndexAddress);

                        if (glyphIndex != 0)
                            glyphIndex = (glyphIndex + idDelta) & 0xffff;
                    }

                    if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, codepoint))
                        return false;
                }
            }

            codepoints = coverage;
            return true;
        }

        static bool TryReadCmapFormat6Coverage(byte[] fontData, int offset, int maxLength, out HashSet<int> codepoints)
        {
            codepoints = null;

            if (!TryGetUInt16CmapLength(fontData, offset, maxLength, out var length) || length < 10)
                return false;

            int firstCode = ReadUInt16BigEndian(fontData, offset + 6);
            int entryCount = ReadUInt16BigEndian(fontData, offset + 8);
            int glyphsOffset = offset + 10;
            int glyphsLength = entryCount * 2;

            if (glyphsOffset + glyphsLength > offset + length)
                return false;

            var coverage = new HashSet<int>();

            for (int i = 0; i < entryCount; ++i)
            {
                int glyphIndex = ReadUInt16BigEndian(fontData, glyphsOffset + i * 2);

                if (glyphIndex > 0 && !TryAddCmapCodepoint(coverage, firstCode + i))
                    return false;
            }

            codepoints = coverage;
            return true;
        }

        static bool TryReadCmapFormat12Coverage(
            byte[] fontData,
            int offset,
            int maxLength,
            bool constantGlyphIndex,
            out HashSet<int> codepoints)
        {
            codepoints = null;

            if (!CanRead(fontData, offset, 16))
                return false;

            uint rawLength = ReadUInt32BigEndian(fontData, offset + 4);

            if (rawLength < 16 || rawLength > int.MaxValue || rawLength > maxLength)
                return false;

            int length = (int)rawLength;

            if (!CanRead(fontData, offset, length))
                return false;

            uint groupCount = ReadUInt32BigEndian(fontData, offset + 12);

            if (groupCount > 100000)
                return false;

            int groupsOffset = offset + 16;
            int tableEnd = offset + length;

            if (groupsOffset + groupCount * 12 > tableEnd)
                return false;

            var coverage = new HashSet<int>();

            for (int i = 0; i < groupCount; ++i)
            {
                int groupOffset = groupsOffset + i * 12;
                uint start = ReadUInt32BigEndian(fontData, groupOffset);
                uint end = ReadUInt32BigEndian(fontData, groupOffset + 4);
                uint startGlyph = ReadUInt32BigEndian(fontData, groupOffset + 8);

                if (start > end || end > 0x10ffff)
                    continue;

                for (uint codepoint = start; codepoint <= end; ++codepoint)
                {
                    ulong glyphIndex = constantGlyphIndex
                        ? startGlyph
                        : (ulong)startGlyph + codepoint - start;

                    if (glyphIndex > 0 &&
                        glyphIndex <= int.MaxValue &&
                        !TryAddCmapCodepoint(coverage, unchecked((int)codepoint)))
                    {
                        return false;
                    }
                }
            }

            codepoints = coverage;
            return true;
        }

        static bool TryGetUInt16CmapLength(byte[] fontData, int offset, int maxLength, out int length)
        {
            length = 0;

            if (!CanRead(fontData, offset, 4))
                return false;

            length = ReadUInt16BigEndian(fontData, offset + 2);
            return length <= maxLength && CanRead(fontData, offset, length);
        }

        static bool TryAddCmapCodepoint(HashSet<int> codepoints, int codepoint)
        {
            if (!IsValidUnicodeScalar(codepoint))
                return true;

            if (codepoints.Contains(codepoint))
                return true;

            if (codepoints.Count >= MAX_DYNAMIC_SOURCE_CMAP_CODEPOINTS)
                return false;

            codepoints.Add(codepoint);
            return true;
        }

        static bool IsValidUnicodeScalar(int value)
        {
            return value > 0 && value <= 0x10ffff && (value < 0xd800 || value > 0xdfff);
        }

        static int[] ReadColorBitmapSizes(byte[] fontData)
        {
            if (!TryGetOpenTypeTable(fontData, "CBLC", out var tableOffset, out var tableLength))
                return null;

            if (tableLength < 8)
                return null;

            uint sizeCount = ReadUInt32BigEndian(fontData, tableOffset + 4);

            if (sizeCount == 0 || sizeCount > 4096)
                return null;

            int recordsOffset = tableOffset + 8;
            int recordsLength = checked((int)sizeCount * 48);

            if (recordsLength > tableLength - 8)
                return null;

            var sizes = new List<int>((int)sizeCount);

            for (int i = 0; i < sizeCount; ++i)
            {
                int offset = recordsOffset + i * 48;
                int ppemX = fontData[offset + 44];
                int ppemY = fontData[offset + 45];
                int ppem = Mathf.Max(ppemX, ppemY);

                if (ppem <= 0 || sizes.Contains(ppem))
                    continue;

                sizes.Add(ppem);
            }

            if (sizes.Count == 0)
                return null;

            sizes.Sort();
            return sizes.ToArray();
        }

        static bool IsAtlasWithinLimit(NowFont font, int maxAtlasSize, int maxAtlasBytes)
        {
            if (font == null || font.atlas == null)
                return false;

            if (maxAtlasSize > 0 && (font.atlas.width > maxAtlasSize || font.atlas.height > maxAtlasSize))
                return false;

            int byteCount = font.atlas.width * font.atlas.height * 4;
            return maxAtlasBytes <= 0 || byteCount <= maxAtlasBytes;
        }

        static void DestroyDynamicPage(DynamicAtlasPage page)
        {
            if (page == null || page.font == null)
                return;

            DestroyDynamicFont(page.font, true);
            page.font = null;
        }

        static void DestroyDynamicFont(NowFont font, bool releaseHostMaterials = false)
        {
            if (font == null)
                return;

            if (releaseHostMaterials)
            {
#if NOWUI_UGUI
                NowGraphic.ReleaseCachedMaterial(font.material);
#endif
                NowWorldGraphic.ReleaseCachedMaterial(font.material);
            }

            DestroyDynamicObject(font.material);
            DestroyDynamicObject(font.atlas);
            DestroyDynamicObject(font);
        }

        static void DestroyDynamicObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        static bool IsDynamicPageValid(DynamicAtlasPage page)
        {
            return page != null &&
                page.font &&
                page.font.atlas &&
                page.font.material;
        }

        void RemoveDynamicGlyphMappings(DynamicAtlasPage page)
        {
            if (page == null || _dynamicGlyphPages == null)
                return;

            if (page.codepoints != null)
            {
                foreach (int unicode in page.codepoints)
                {
                    var key = new DynamicGlyphKey(unicode, page.atlasSize, page.pixelRange);

                    if (_dynamicGlyphPages.TryGetValue(key, out var mappedPage) && ReferenceEquals(mappedPage, page))
                        _dynamicGlyphPages.Remove(key);
                }
            }
        }

        void RemoveDynamicPageAt(int index)
        {
            if (_dynamicPages == null || index < 0 || index >= _dynamicPages.Count)
                return;

            ClearPreparedShapeCache();
            ClearPreparedCodepointCache();
            var page = _dynamicPages[index];
            RemoveDynamicGlyphMappings(page);
            _dynamicPages.RemoveAt(index);
            DestroyDynamicPage(page);
            _dynamicCapacityMisses?.Clear();
            _dynamicFallbackGlyphPages?.Clear();
        }

        public float GetLineHeight()
        {
            if (atlasInfo.metrics.lineHeight > 0)
                return atlasInfo.metrics.lineHeight;

            if (_hasDynamicColorLayoutMetrics && _dynamicColorLayoutMetrics.lineHeight > 0)
                return _dynamicColorLayoutMetrics.lineHeight;

            if (_dynamicPages == null)
                return 1;

            for (int i = 0; i < _dynamicPages.Count; ++i)
            {
                var font = _dynamicPages[i].font;

                if (font != null && font.atlasInfo.metrics.lineHeight > 0)
                    return font.atlasInfo.metrics.lineHeight;
            }

            return 1;
        }

        public float GetAscender()
        {
            if (atlasInfo.metrics.ascender > 0)
                return atlasInfo.metrics.ascender;

            if (_hasDynamicColorLayoutMetrics && _dynamicColorLayoutMetrics.ascender > 0)
                return _dynamicColorLayoutMetrics.ascender;

            if (_dynamicPages != null)
            {
                for (int i = 0; i < _dynamicPages.Count; ++i)
                {
                    var font = _dynamicPages[i].font;

                    if (font != null && font.atlasInfo.metrics.ascender > 0)
                        return font.atlasInfo.metrics.ascender;
                }
            }

            return GetLineHeight();
        }

        void ClearGlyphCache()
        {
            _glyphTierFontSize = float.NaN;
            _denseGlyphTable = null;
            _sparseGlyphTable = null;
            _glyphTableOffset = 0;
            ClearPreparedCodepointCache();
        }

        void BuildGlyphCache()
        {
            var glyphs = atlasInfo.glyphs;
            int first = glyphs[0].unicode;
            int last = glyphs[0].unicode;

            for (int i = 1; i < glyphs.Length; ++i)
            {
                int unicode = glyphs[i].unicode;

                if (unicode < first)
                    first = unicode;
                else if (unicode > last)
                    last = unicode;
            }

            int range = last - first + 1;
            bool useDenseTable = first > 0 && range <= MAX_DENSE_GLYPH_RANGE && range <= glyphs.Length * 4;

            if (useDenseTable)
            {
                _glyphTableOffset = first;
                _denseGlyphTable = new NowFontAtlasInfo.Glyph[range];

                for (int i = 0; i < glyphs.Length; ++i)
                {
                    var glyphValue = glyphs[i];
                    NormalizeGlyphAtlasBounds(ref glyphValue, atlas);
                    _denseGlyphTable[glyphValue.unicode - _glyphTableOffset] = glyphValue;
                }

                return;
            }

            _sparseGlyphTable = new Dictionary<int, NowFontAtlasInfo.Glyph>(glyphs.Length);

            for (int i = 0; i < glyphs.Length; ++i)
            {
                var glyphValue = glyphs[i];
                NormalizeGlyphAtlasBounds(ref glyphValue, atlas);
                _sparseGlyphTable[glyphValue.unicode] = glyphValue;
            }
        }

        bool TryGetCachedGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
        {
            glyph = default;

            if (!atlas || atlasInfo.glyphs == null || atlasInfo.glyphs.Length == 0)
                return false;

            if (_denseGlyphTable == null && _sparseGlyphTable == null)
                BuildGlyphCache();

            if (_denseGlyphTable != null)
            {
                int idx = unicode - _glyphTableOffset;

                if (idx < 0 || idx >= _denseGlyphTable.Length)
                    return false;

                glyph = _denseGlyphTable[idx];
                return glyph.unicode == unicode;
            }

            return _sparseGlyphTable != null && _sparseGlyphTable.TryGetValue(unicode, out glyph);
        }

        static bool AtlasSupportsPixelRange(
            NowFontAtlasInfo.Atlas atlas,
            int requestedAtlasSize,
            int requestedPixelRange)
        {
            if (atlas.type == ATLAS_TYPE_RGBA)
                return true;

            if (atlas.type != ATLAS_TYPE_MTSDF || atlas.distanceRange <= 0)
                return false;

            int atlasSize = atlas.size > 0 ? atlas.size : requestedAtlasSize;

            if (atlasSize <= 0 || requestedAtlasSize <= 0)
                return atlas.distanceRange >= requestedPixelRange;

            // Distance range is stored in atlas pixels. Compare its em-relative
            // coverage so a base atlas baked at a different size is not mistaken
            // for a range-compatible dynamic variant.
            return (long)atlas.distanceRange * requestedAtlasSize >=
                (long)requestedPixelRange * atlasSize;
        }

        public int GetDynamicGlyphSize(float fontSize)
        {
            return GetBaseDynamicGlyphSize();
        }

        /// <summary>
        /// Selects a distance-field range large enough to retain the requested
        /// em-relative outline. The exact authored width remains draw data; only
        /// backing field capacity rounds upward through hidden doubling tiers so
        /// arbitrary, animated, and Inspector-driven values share a logarithmic
        /// set of atlas variants.
        /// </summary>
        internal int GetDynamicPixelRange(float outline, float fontSize)
        {
            int baseRange = dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE;

            if (!HasEmbeddedSource || float.IsNaN(outline) || float.IsInfinity(outline) || Mathf.Approximately(outline, 0f))
                return baseRange;

            _dynamicSourceIsColor ??= NowFontCompiler.IsColorFont(DynamicFontBytes);

            // RGBA glyph shaders preserve authored color and do not implement
            // distance-field outlines, so extra range tiers would only duplicate
            // the same bitmap glyphs and atlas memory.
            if (_dynamicSourceIsColor.Value)
                return baseRange;

            int atlasSize = GetBaseDynamicGlyphSize();
            float outlineAtlasPixels = Mathf.Abs(outline) * atlasSize;
            float uiPixelGuardInAtlas = fontSize > 0f && !float.IsNaN(fontSize) && !float.IsInfinity(fontSize)
                ? atlasSize / fontSize
                : 1f;
            float absoluteGuardRange = 2f *
                (outlineAtlasPixels + Mathf.Max(1f, uiPixelGuardInAtlas));
            float proportionalGuardRange =
                outlineAtlasPixels / MAX_OUTLINE_RANGE_FRACTION;
            // Match NowMesh's runtime clamp: the chosen tier must leave both one
            // UI/atlas pixel and 5% of the full field outside the threshold.
            float requested = Mathf.Max(absoluteGuardRange, proportionalGuardRange);

            if (requested <= baseRange)
                return baseRange;

            int maxRange = GetMaximumDynamicPixelRange(atlasSize, baseRange);
            int requestedRange = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(requested, maxRange)), baseRange, maxRange);
            int bucket = baseRange;

            while (bucket < requestedRange && bucket < maxRange)
            {
                long doubled = (long)bucket * 2;
                bucket = doubled >= maxRange ? maxRange : (int)doubled;
            }

            return bucket;
        }

        int GetBaseDynamicGlyphSize()
        {
            int baseSize = dynamicAtlasSize > 0 ? dynamicAtlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE;
            return TryGetLargestColorBitmapSize(out var colorBitmapSize) ? colorBitmapSize : baseSize;
        }

        int[] GetColorBitmapSizes()
        {
            if (!_didReadDynamicColorBitmapSizes)
            {
                _dynamicColorBitmapSizes = ReadColorBitmapSizes(DynamicFontBytes);
                _didReadDynamicColorBitmapSizes = true;
            }

            return _dynamicColorBitmapSizes;
        }

        HashSet<int> GetDynamicCodepointScratch()
        {
            _dynamicCodepointScratch ??= new HashSet<int>();
            _dynamicCodepointScratch.Clear();
            return _dynamicCodepointScratch;
        }

        StringBuilder GetDynamicStringBuilder()
        {
            _dynamicStringBuilder ??= new StringBuilder();
            _dynamicStringBuilder.Length = 0;
            return _dynamicStringBuilder;
        }

        int[] GetDynamicCompileCodepoints(string value, out int count)
        {
            count = 0;

            if (string.IsNullOrEmpty(value))
                return null;

            var uniqueCodepoints = GetDynamicCodepointScratch();

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = ReadCodepoint(value, ref i);

                if (!IsValidUnicodeScalar(codepoint) || !uniqueCodepoints.Add(codepoint))
                    continue;

                if (_dynamicCompileCodepoints == null || count >= _dynamicCompileCodepoints.Length)
                {
                    int currentCapacity = _dynamicCompileCodepoints?.Length ?? 0;
                    int nextCapacity = Mathf.Max(count + 1, currentCapacity > 0 ? currentCapacity * 2 : 8);
                    Array.Resize(ref _dynamicCompileCodepoints, nextCapacity);
                }

                _dynamicCompileCodepoints[count++] = codepoint;
            }

            uniqueCodepoints.Clear();
            return count > 0 ? _dynamicCompileCodepoints : null;
        }

        bool DynamicSourceContainsCodepoint(int unicode)
        {
            if (!IsValidUnicodeScalar(unicode))
                return false;

            if (!_didReadDynamicSourceCodepoints)
            {
                if (!TryReadDynamicSourceCodepoints(DynamicFontBytes, out _dynamicSourceCodepoints))
                    _dynamicSourceCodepoints = null;

                _didReadDynamicSourceCodepoints = true;
            }

            return _dynamicSourceCodepoints == null || _dynamicSourceCodepoints.Contains(unicode);
        }

        bool TryGetLargestColorBitmapSize(out int bitmapSize)
        {
            bitmapSize = 0;
            var sizes = GetColorBitmapSizes();

            if (sizes == null || sizes.Length == 0)
                return false;

            bitmapSize = sizes[^1];
            return true;
        }

        bool TryGetDynamicCachedGlyph(int unicode, int atlasSize, int pixelRange, out NowFontAtlasInfo.Glyph glyph)
        {
            return TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph, out _);
        }

        /// <summary>Single-probe variant that also returns the page owning the glyph, so
        /// per-character callers resolve the page material without a second dictionary lookup.</summary>
        bool TryGetDynamicCachedGlyph(
            int unicode,
            int atlasSize,
            int pixelRange,
            out NowFontAtlasInfo.Glyph glyph,
            out DynamicAtlasPage page)
        {
            glyph = default;
            page = null;
            var key = new DynamicGlyphKey(unicode, atlasSize, pixelRange);

            if (_dynamicGlyphPages == null || !_dynamicGlyphPages.TryGetValue(key, out var mappedPage))
                return false;

            if (!IsDynamicPageValid(mappedPage))
            {
                int pageIndex = _dynamicPages?.IndexOf(mappedPage) ?? -1;

                if (pageIndex >= 0)
                    RemoveDynamicPageAt(pageIndex);
                else
                    _dynamicGlyphPages.Remove(key);

                return false;
            }

            if (mappedPage.font.GetGlyph(unicode, out glyph))
            {
                page = mappedPage;
                return true;
            }

            _dynamicGlyphPages.Remove(key);
            return false;
        }

        bool TryCompileDynamicPage(string characters, int atlasSize, int pixelRange, out NowFont font)
        {
            font = null;
            var fontData = DynamicFontBytes;

            if (fontData == null || fontData.Length == 0 || string.IsNullOrEmpty(characters))
                return false;

            var codepoints = GetDynamicCompileCodepoints(characters, out var codepointCount);

            if (codepointCount <= 0)
                return false;

            if (!NowFontCompiler.TryCompilePage(
                fontData,
                atlasSize > 0 ? atlasSize : DEFAULT_DYNAMIC_ATLAS_SIZE,
                pixelRange > 0 ? pixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE,
                codepoints,
                codepointCount,
                _dynamicMaterialTemplate,
                out font,
                out _))
            {
                return false;
            }

            if (IsAtlasWithinLimit(
                font,
                dynamicMaxAtlasSize > 0 ? dynamicMaxAtlasSize : DEFAULT_DYNAMIC_MAX_ATLAS_SIZE,
                dynamicMaxAtlasBytes > 0 ? dynamicMaxAtlasBytes : DEFAULT_DYNAMIC_MAX_ATLAS_BYTES))
            {
                return true;
            }

            DestroyDynamicFont(font);
            font = null;
            return false;
        }

        int GetDynamicMaxAtlasSide()
        {
            int maxAtlasSize = dynamicMaxAtlasSize > 0 ? dynamicMaxAtlasSize : DEFAULT_DYNAMIC_MAX_ATLAS_SIZE;
            int maxAtlasBytes = dynamicMaxAtlasBytes > 0 ? dynamicMaxAtlasBytes : DEFAULT_DYNAMIC_MAX_ATLAS_BYTES;

            if (maxAtlasBytes > 0)
            {
                int maxSizeByBytes = Mathf.FloorToInt(Mathf.Sqrt(maxAtlasBytes / 4f));
                if (maxSizeByBytes > 0)
                    maxAtlasSize = Mathf.Min(maxAtlasSize, maxSizeByBytes);
            }

            return maxAtlasSize;
        }

        int GetDynamicPageSize(int requiredSize)
        {
            int pageSize = dynamicPageSize > 0 ? dynamicPageSize : DEFAULT_DYNAMIC_PAGE_SIZE;
            pageSize = Mathf.Max(pageSize, requiredSize);
            return Mathf.Min(pageSize, GetDynamicMaxAtlasSide());
        }

        /// <summary>
        /// Base text benefits from a large long-lived page because most interfaces use it.
        /// Adaptive effect tiers are usually sparse, especially while an Inspector value is
        /// being explored, so start them at enough room for roughly a small row/grid of their
        /// padded glyphs instead of eagerly committing a full default page. A tier remains
        /// writable and simply spills to another immutable page when its session fills. An
        /// explicitly configured non-default page size remains authoritative.
        /// </summary>
        int GetDynamicRangePageSize(int requiredSize, int pixelRange)
        {
            int pageSize = GetDynamicPageSize(requiredSize);
            int baseRange = dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE;
            int configuredPageSize = dynamicPageSize > 0 ? dynamicPageSize : DEFAULT_DYNAMIC_PAGE_SIZE;

            if (pixelRange <= baseRange ||
                configuredPageSize != DEFAULT_DYNAMIC_PAGE_SIZE ||
                pageSize <= 512)
            {
                return pageSize;
            }

            long desiredWorkingSide = (long)Mathf.Max(1, requiredSize) * 2;
            int sparseSide = 512;

            while (sparseSide < desiredWorkingSide && sparseSide < pageSize)
                sparseSide = sparseSide > int.MaxValue / 2 ? pageSize : sparseSide * 2;

            return Mathf.Min(pageSize, sparseSide);
        }

        int GetMaximumDynamicPixelRange(int atlasSize, int baseRange)
        {
            long geometricLimit = (long)GetDynamicMaxAtlasSide() -
                (long)atlasSize * 2 -
                DYNAMIC_GLYPH_PADDING * 2L;
            int geometricMax = geometricLimit <= baseRange
                ? baseRange
                : geometricLimit >= int.MaxValue ? int.MaxValue : (int)geometricLimit;

            if (geometricMax <= baseRange || baseRange == int.MaxValue)
                return baseRange;

            // A user-facing outlined draw prepares its face first. The effect
            // session therefore has to fit beside one sealed base page, not only
            // inside an otherwise empty 64 MiB cache. Session pages retain four
            // atlas payloads while writable (GPU, readable texture, session atlas,
            // and conservative compiler work storage).
            int baseRequiredSide = (int)Math.Min(
                int.MaxValue,
                (long)atlasSize + baseRange + DYNAMIC_GLYPH_PADDING * 2L);
            int basePageSide = GetDynamicRangePageSize(baseRequiredSide, baseRange);
            long availableForEffect = DEFAULT_DYNAMIC_CACHE_BUDGET_BYTES -
                GetRgbaTexturePayloadBytes(basePageSide, basePageSide, 1);

            if (availableForEffect <= 0)
                return baseRange;

            bool FitsEffectWorkingSet(int range)
            {
                int requiredSide = (int)Math.Min(
                    int.MaxValue,
                    (long)atlasSize + range + DYNAMIC_GLYPH_PADDING * 2L);
                int pageSide = GetDynamicRangePageSize(requiredSide, range);
                long workingBytes = GetRgbaTexturePayloadBytes(pageSide, pageSide, 1) * 4;
                return workingBytes <= availableForEffect;
            }

            int firstEffectRange = baseRange + 1;

            if (!FitsEffectWorkingSet(firstEffectRange))
                return baseRange;

            int low = firstEffectRange;
            int high = geometricMax;

            while (low < high)
            {
                int middle = low + (int)(((long)high - low + 1) / 2);

                if (FitsEffectWorkingSet(middle))
                    low = middle;
                else
                    high = middle - 1;
            }

            return low;
        }

        static bool TryGetGlyphSourceRect(NowFont font, NowFontAtlasInfo.Glyph glyph, out RectInt rect)
        {
            rect = default;

            if (!font || !font.atlas)
                return false;

            int left = Mathf.FloorToInt(Mathf.Min(glyph.atlasBounds.left, glyph.atlasBounds.right));
            int right = Mathf.CeilToInt(Mathf.Max(glyph.atlasBounds.left, glyph.atlasBounds.right));
            int bottom = Mathf.FloorToInt(Mathf.Min(glyph.atlasBounds.bottom, glyph.atlasBounds.top));
            int top = Mathf.CeilToInt(Mathf.Max(glyph.atlasBounds.bottom, glyph.atlasBounds.top));

            left = Mathf.Clamp(left, 0, font.atlas.width);
            right = Mathf.Clamp(right, 0, font.atlas.width);
            bottom = Mathf.Clamp(bottom, 0, font.atlas.height);
            top = Mathf.Clamp(top, 0, font.atlas.height);

            rect = new RectInt(left, bottom, right - left, top - bottom);
            return rect is { width: >= 0, height: >= 0 };
        }

        DynamicAtlasPage CreateDynamicPage(
            NowFont glyphFont,
            int requiredSize,
            int pageIndex,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;

            if (!glyphFont || !glyphFont.atlas || !glyphFont.material)
                return null;

            bool isColorPage = glyphFont.isColor;
            int pageSize = isColorPage
                ? GetDynamicPageSize(requiredSize)
                : GetDynamicRangePageSize(requiredSize, pixelRange);

            if (pageSize < requiredSize)
                return null;

            int mipCount = isColorPage ? GetFullMipCount(pageSize, pageSize) : 1;
            long pageResidentBytes = GetRgbaTexturePayloadBytes(pageSize, pageSize, mipCount) * 2;

            if (!TryMakeDynamicCacheRoom(pageResidentBytes, null))
            {
                budgetExceeded = true;
                return null;
            }

            var pageTexture = new Texture2D(pageSize, pageSize, TextureFormat.RGBA32, isColorPage, !isColorPage)
            {
                name = isColorPage ? $"Now Color Page {pageIndex}" : $"Now Font Page {pageIndex}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            pageTexture.GetRawTextureData<Color32>().AsSpan().Clear();
            pageTexture.Apply(isColorPage, false);

            var pageMaterial = new Material(glyphFont.material)
            {
                name = glyphFont.material.name + " Page",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = pageTexture
            };

            var pageFont = CreateInstance<NowFont>();
            pageFont.name = isColorPage ? "Now Runtime Color Font Page" : "Now Runtime Font Page";
            pageFont.hideFlags = HideFlags.HideAndDontSave;
            pageFont.atlas = pageTexture;
            pageFont.material = pageMaterial;
            pageFont.atlasInfo = glyphFont.atlasInfo;
            pageFont.atlasInfo.atlas.width = pageSize;
            pageFont.atlasInfo.atlas.height = pageSize;

            if (isColorPage && _hasDynamicColorLayoutMetrics)
                pageFont.atlasInfo.metrics = _dynamicColorLayoutMetrics;

            pageFont.atlasInfo.glyphs = Array.Empty<NowFontAtlasInfo.Glyph>();

            return new DynamicAtlasPage
            {
                font = pageFont,
                codepoints = new HashSet<int>(),
                atlasSize = glyphFont.atlasInfo.atlas.size > 0 ? glyphFont.atlasInfo.atlas.size : requiredSize,
                pixelRange = pixelRange
            };
        }

        static bool IsSameDynamicPageType(DynamicAtlasPage page, NowFont glyphFont, int atlasSize, int pixelRange)
        {
            return page != null &&
                page.font != null &&
                glyphFont != null &&
                page.atlasSize == atlasSize &&
                page.pixelRange == pixelRange &&
                page.font.atlasInfo.atlas.type == glyphFont.atlasInfo.atlas.type;
        }

        bool TryAllocateGlyphRect(DynamicAtlasPage page, RectInt sourceRect, out RectInt targetRect)
        {
            targetRect = default;

            if (page == null || page.font == null || page.font.atlas == null)
                return false;

            if (sourceRect.width <= 0 || sourceRect.height <= 0)
                return true;

            int paddedWidth = sourceRect.width + DYNAMIC_GLYPH_PADDING;
            int paddedHeight = sourceRect.height + DYNAMIC_GLYPH_PADDING;
            int pageWidth = page.font.atlas.width;
            int pageHeight = page.font.atlas.height;

            if (sourceRect.width > pageWidth || sourceRect.height > pageHeight)
                return false;

            if (page.cursorX + sourceRect.width > pageWidth)
            {
                page.cursorX = 0;
                page.cursorY += page.rowHeight;
                page.rowHeight = 0;
            }

            if (page.cursorY + sourceRect.height > pageHeight)
                return false;

            targetRect = new RectInt(page.cursorX, page.cursorY, sourceRect.width, sourceRect.height);
            page.cursorX += paddedWidth;
            page.rowHeight = Mathf.Max(page.rowHeight, paddedHeight);
            return true;
        }

        static void AppendGlyph(ref NowFontAtlasInfo.Glyph[] glyphs, NowFontAtlasInfo.Glyph glyph)
        {
            int length = glyphs?.Length ?? 0;
            var nextGlyphs = new NowFontAtlasInfo.Glyph[length + 1];

            if (length > 0)
                Array.Copy(glyphs!, nextGlyphs, length);

            nextGlyphs[length] = glyph;
            glyphs = nextGlyphs;
        }

        static void AppendGlyphs(ref NowFontAtlasInfo.Glyph[] glyphs, List<NowFontAtlasInfo.Glyph> additions)
        {
            if (additions == null || additions.Count == 0)
                return;

            int length = glyphs?.Length ?? 0;
            var nextGlyphs = new NowFontAtlasInfo.Glyph[length + additions.Count];

            if (length > 0)
                Array.Copy(glyphs!, nextGlyphs, length);

            additions.CopyTo(nextGlyphs, length);
            glyphs = nextGlyphs;
        }

        static bool TryGetRawGlyph(NowFont font, int unicode, out NowFontAtlasInfo.Glyph glyph)
        {
            glyph = default;

            if (!font || font.atlasInfo.glyphs == null)
                return false;

            var glyphs = font.atlasInfo.glyphs;

            for (int i = 0; i < glyphs.Length; ++i)
            {
                if (glyphs[i].unicode != unicode)
                    continue;

                glyph = glyphs[i];
                return true;
            }

            return false;
        }

        static Dictionary<int, NowFontAtlasInfo.Glyph> BuildRawGlyphMap(NowFont font)
        {
            if (!font || font.atlasInfo.glyphs == null || font.atlasInfo.glyphs.Length == 0)
                return null;

            var glyphs = font.atlasInfo.glyphs;
            var map = new Dictionary<int, NowFontAtlasInfo.Glyph>(glyphs.Length);

            for (int i = 0; i < glyphs.Length; ++i)
                map[glyphs[i].unicode] = glyphs[i];

            return map;
        }

        void StoreColorLayoutGlyphs(NowFont font, string characters)
        {
            if (!font || !font.isColor || string.IsNullOrEmpty(characters))
                return;

            if (!_hasDynamicColorLayoutMetrics)
            {
                _dynamicColorLayoutMetrics = font.atlasInfo.metrics;
                _hasDynamicColorLayoutMetrics = true;
            }

            _dynamicColorLayoutGlyphs ??= new Dictionary<int, NowFontAtlasInfo.Glyph>();

            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);

                if (_dynamicColorLayoutGlyphs.ContainsKey(unicode))
                    continue;

                if (TryGetRawGlyph(font, unicode, out var glyph))
                    _dynamicColorLayoutGlyphs[unicode] = glyph;
            }
        }

        string GetMissingColorLayoutCharacters(string characters)
        {
            if (string.IsNullOrEmpty(characters))
                return null;

            StringBuilder builder = null;

            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);

                if (_dynamicColorLayoutGlyphs != null && _dynamicColorLayoutGlyphs.ContainsKey(unicode))
                    continue;

                builder ??= GetDynamicStringBuilder();
                AppendCodepoint(builder, unicode);
            }

            if (builder == null)
                return null;

            string result = builder.ToString();
            builder.Length = 0;
            return result;
        }

        void EnsureColorLayoutGlyphs(string characters, int atlasSize, NowFont glyphFont)
        {
            if (!glyphFont || !glyphFont.isColor || string.IsNullOrEmpty(characters))
                return;

            int baseAtlasSize = GetBaseDynamicGlyphSize();

            if (atlasSize == baseAtlasSize)
            {
                StoreColorLayoutGlyphs(glyphFont, characters);
                return;
            }

            string missingCharacters = GetMissingColorLayoutCharacters(characters);

            if (string.IsNullOrEmpty(missingCharacters))
                return;

            if (!TryCompileDynamicPage(
                missingCharacters,
                baseAtlasSize,
                dynamicPixelRange > 0 ? dynamicPixelRange : DEFAULT_DYNAMIC_PIXEL_RANGE,
                out var layoutFont))
            {
                StoreColorLayoutGlyphs(glyphFont, missingCharacters);
                return;
            }

            try
            {
                StoreColorLayoutGlyphs(layoutFont, missingCharacters);
            }
            finally
            {
                DestroyDynamicFont(layoutFont);
            }
        }

        static void ApplyColorLayoutGlyph(ref NowFontAtlasInfo.Glyph glyph, NowFontAtlasInfo.Glyph layoutGlyph)
        {
            glyph.advance = layoutGlyph.advance;
            glyph.planeBounds = layoutGlyph.planeBounds;
        }

        bool TryAppendDynamicGlyph(
            DynamicAtlasPage page,
            NowFont glyphFont,
            int unicode,
            int atlasSize,
            int pixelRange,
            NowFontAtlasInfo.Glyph glyph,
            RectInt sourceRect,
            DynamicGlyphAppendBatch batch = null)
        {
            if (!IsSameDynamicPageType(page, glyphFont, atlasSize, pixelRange))
                return false;

            if (!TryAllocateGlyphRect(page, sourceRect, out var targetRect))
                return false;

            if (sourceRect.width > 0 && sourceRect.height > 0)
            {
                var sourceData = glyphFont.atlas.GetRawTextureData<Color32>().AsSpan();
                var targetData = page.font.atlas.GetRawTextureData<Color32>().AsSpan();
                int sourceWidth = glyphFont.atlas.width;
                int targetWidth = page.font.atlas.width;

                for (int y = 0; y < sourceRect.height; ++y)
                {
                    int sourceIndex = (sourceRect.y + y) * sourceWidth + sourceRect.x;
                    int targetIndex = (targetRect.y + y) * targetWidth + targetRect.x;
                    sourceData.Slice(sourceIndex, sourceRect.width).CopyTo(targetData.Slice(targetIndex, sourceRect.width));
                }

                if (batch != null)
                    batch.MarkTextureDirty(page);
                else
                    page.font.atlas.Apply(page.font.isColor, false);

                glyph.atlasBounds.left = targetRect.x + (glyph.atlasBounds.left - sourceRect.x);
                glyph.atlasBounds.right = targetRect.x + (glyph.atlasBounds.right - sourceRect.x);
                glyph.atlasBounds.bottom = targetRect.y + (glyph.atlasBounds.bottom - sourceRect.y);
                glyph.atlasBounds.top = targetRect.y + (glyph.atlasBounds.top - sourceRect.y);
            }
            else
            {
                glyph.atlasBounds = default;
            }

            if (glyphFont.isColor &&
                _dynamicColorLayoutGlyphs != null &&
                _dynamicColorLayoutGlyphs.TryGetValue(unicode, out var layoutGlyph))
            {
                ApplyColorLayoutGlyph(ref glyph, layoutGlyph);
            }

            if (batch != null)
            {
                batch.AddGlyph(page, glyph);
            }
            else
            {
                var fontAtlasInfo = page.font.atlasInfo;
                AppendGlyph(ref fontAtlasInfo.glyphs, glyph);
                page.font.atlasInfo = fontAtlasInfo;
                page.font.ClearGlyphCache();
            }

            page.codepoints ??= new HashSet<int>();
            page.codepoints.Add(unicode);
            _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();
            _dynamicGlyphPages[new DynamicGlyphKey(unicode, atlasSize, pixelRange)] = page;
            InvalidateDynamicFallbackSelections();
            return true;
        }

        void AddDynamicMiss(DynamicGlyphKey key)
        {
            _dynamicMisses ??= new HashSet<DynamicGlyphKey>();
            _dynamicMisses.Add(key);
        }

        void AddDynamicCapacityMiss(DynamicGlyphKey key)
        {
            _dynamicCapacityMisses ??= new HashSet<DynamicGlyphKey>();
            _dynamicCapacityMisses.Add(key);
        }

        void InvalidateDynamicFallbackSelections()
        {
            if (_dynamicFallbackGlyphPages == null || _dynamicFallbackGlyphPages.Count == 0)
                return;

            _dynamicFallbackGlyphPages.Clear();
            ClearPreparedShapeCache();
            ClearPreparedCodepointCache();
        }

        void MarkDynamicCapacityMisses(string characters, int atlasSize, int pixelRange)
        {
            if (string.IsNullOrEmpty(characters))
                return;

            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);
                AddDynamicCapacityMiss(new DynamicGlyphKey(unicode, atlasSize, pixelRange));
            }
        }

        bool TryGetDynamicCapacityFallbackGlyph(
            int unicode,
            int atlasSize,
            int pixelRange,
            out NowFontAtlasInfo.Glyph glyph,
            out DynamicAtlasPage page)
        {
            glyph = default;
            page = null;
            var requestedKey = new DynamicGlyphKey(unicode, atlasSize, pixelRange);

            if (_dynamicCapacityMisses == null || !_dynamicCapacityMisses.Contains(requestedKey))
                return false;

            if (_dynamicFallbackGlyphPages != null &&
                _dynamicFallbackGlyphPages.TryGetValue(requestedKey, out var cachedPage))
            {
                if (IsDynamicPageValid(cachedPage) && cachedPage.font.GetGlyph(unicode, out glyph))
                {
                    page = cachedPage;
                    return true;
                }

                _dynamicFallbackGlyphPages.Remove(requestedKey);
            }

            int bestRange = int.MinValue;

            if (_dynamicPages != null)
            {
                for (int i = 0; i < _dynamicPages.Count; ++i)
                {
                    var candidate = _dynamicPages[i];

                    if (!IsDynamicPageValid(candidate) ||
                        candidate.atlasSize != atlasSize ||
                        candidate.pixelRange >= pixelRange ||
                        candidate.pixelRange <= bestRange ||
                        !candidate.font.GetGlyph(unicode, out var candidateGlyph))
                    {
                        continue;
                    }

                    bestRange = candidate.pixelRange;
                    glyph = candidateGlyph;
                    page = candidate;
                }
            }

            if (page == null)
                return false;

            _dynamicFallbackGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();
            _dynamicFallbackGlyphPages[requestedKey] = page;
            return true;
        }

        bool ShouldCompileDynamicGlyph(int unicode, int atlasSize, int pixelRange)
        {
            if (DynamicFontBytes == null || unicode <= 0)
                return false;

            var key = new DynamicGlyphKey(unicode, atlasSize, pixelRange);

            if (_dynamicCapacityMisses != null && _dynamicCapacityMisses.Contains(key))
                return false;

            if (_dynamicMisses != null && _dynamicMisses.Contains(key))
                return false;

            if ((TryGetCachedGlyph(unicode, out _) && AtlasSupportsPixelRange(atlasInfo.atlas, atlasSize, pixelRange)) ||
                TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out _))
            {
                return false;
            }

            if (!DynamicSourceContainsCodepoint(unicode))
            {
                AddDynamicMiss(key);
                return false;
            }

            return true;
        }

        string GetMissingDynamicCharacters(string value, int atlasSize, int pixelRange)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            HashSet<int> uniqueCodepoints = null;
            StringBuilder builder = null;

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = ReadCodepoint(value, ref i);

                if (codepoint == '\n' || codepoint == '\r')
                    continue;

                if (codepoint == '\t')
                    codepoint = ' ';

                if (!ShouldCompileDynamicGlyph(codepoint, atlasSize, pixelRange))
                    continue;

                uniqueCodepoints ??= GetDynamicCodepointScratch();

                if (!uniqueCodepoints.Add(codepoint))
                    continue;

                builder ??= GetDynamicStringBuilder();
                AppendCodepoint(builder, codepoint);
            }

            uniqueCodepoints?.Clear();

            if (builder == null)
                return null;

            string result = builder.ToString();
            builder.Length = 0;
            return result;
        }

        bool TryCacheCompiledGlyph(
            NowFont glyphFont,
            int unicode,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded,
            DynamicGlyphAppendBatch batch = null,
            Dictionary<int, NowFontAtlasInfo.Glyph> rawGlyphs = null)
        {
            budgetExceeded = false;

            if (!glyphFont ||
                glyphFont.atlasInfo.glyphs == null ||
                glyphFont.atlasInfo.glyphs.Length == 0)
            {
                return false;
            }

            NowFontAtlasInfo.Glyph compiledGlyph;

            if (rawGlyphs != null)
            {
                if (!rawGlyphs.TryGetValue(unicode, out compiledGlyph))
                    return false;
            }
            else if (!TryGetRawGlyph(glyphFont, unicode, out compiledGlyph))
            {
                return false;
            }

            if (!TryGetGlyphSourceRect(glyphFont, compiledGlyph, out var sourceRect))
                return false;

            int requiredPageSize = Mathf.Max(sourceRect.width, sourceRect.height);
            var key = new DynamicGlyphKey(unicode, atlasSize, pixelRange);
            _dynamicPages ??= new List<DynamicAtlasPage>();

            for (int i = _dynamicPages.Count - 1; i >= 0; --i)
            {
                var page = _dynamicPages[i];

                if (!IsDynamicPageValid(page))
                {
                    RemoveDynamicPageAt(i);
                    continue;
                }

                if (page.sessionOwned || !IsSameDynamicPageType(page, glyphFont, atlasSize, pixelRange))
                    continue;

                if (page.codepoints != null && page.codepoints.Contains(unicode))
                {
                    if (page.font.GetGlyph(unicode, out _))
                    {
                        _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();
                        _dynamicGlyphPages[key] = page;
                        InvalidateDynamicFallbackSelections();
                        return true;
                    }

                    continue;
                }

                if (TryAppendDynamicGlyph(page, glyphFont, unicode, atlasSize, pixelRange, compiledGlyph, sourceRect, batch))
                    return true;
            }

            var newPage = CreateDynamicPage(
                glyphFont,
                requiredPageSize,
                _dynamicPages.Count,
                pixelRange,
                out budgetExceeded);

            if (newPage != null && TryAppendDynamicGlyph(newPage, glyphFont, unicode, atlasSize, pixelRange, compiledGlyph, sourceRect, batch))
            {
                _dynamicPages.Add(newPage);
                return true;
            }

            return false;
        }

        void TryCompileMissingGlyphsIndividually(string characters, int atlasSize, int pixelRange)
        {
            for (int i = 0; i < characters.Length; ++i)
            {
                int unicode = ReadCodepoint(characters, ref i);
                TryCompileMissingGlyph(unicode, atlasSize, pixelRange);
            }
        }

        /// <summary>Releases the native session (parsed font, atlas storage); already-baked
        /// pages keep working and the session is recreated lazily on the next missing glyph.</summary>
        DynamicSessionState GetDynamicSessionState(int atlasSize, int pixelRange)
        {
            _dynamicSessions ??= new Dictionary<DynamicAtlasVariant, DynamicSessionState>();
            var variant = new DynamicAtlasVariant(atlasSize, pixelRange);

            if (!_dynamicSessions.TryGetValue(variant, out var state))
            {
                state = new DynamicSessionState();
                _dynamicSessions[variant] = state;
            }

            state.lastUse = ++_dynamicSessionUseClock;

            return state;
        }

        bool TryMakeDynamicCacheRoom(long additionalBytes, DynamicSessionState protectedState)
        {
            long budget = GetDynamicCacheBudgetBytes();

            if (additionalBytes < 0 || additionalBytes > budget)
                return false;

            while (GetEstimatedDynamicCacheResidentBytes() + additionalBytes > budget)
            {
                DynamicSessionState oldest = null;

                if (_dynamicSessions != null)
                {
                    foreach (var candidate in _dynamicSessions.Values)
                    {
                        if (candidate == null ||
                            ReferenceEquals(candidate, protectedState) ||
                            candidate.session == null)
                        {
                            continue;
                        }

                        if (oldest == null || candidate.lastUse < oldest.lastUse)
                            oldest = candidate;
                    }
                }

                if (oldest == null)
                    return false;

                ResetDynamicSession(oldest);
            }

            return true;
        }

        static void SealDynamicSessionPage(DynamicSessionState state)
        {
            var texture = state?.page?.font != null ? state.page.font.atlas : null;

            if (texture != null && texture.isReadable)
                texture.Apply(false, true);
        }

        static void ResetDynamicSession(DynamicSessionState state, bool sealPage = true)
        {
            if (state == null)
                return;

            if (sealPage)
                SealDynamicSessionPage(state);

            state.session?.Dispose();
            state.session = null;
            state.page = null;
            state.reservedPageBytes = 0;
        }

        bool TryGrowEmptyDynamicSession(DynamicSessionState state)
        {
            if (state?.session == null || state.page != null)
                return false;

            int currentSide = state.session.AtlasSide;
            int maxSide = GetDynamicMaxAtlasSide();

            if (currentSide <= 0 || currentSide >= maxSide)
                return false;

            int nextSide = (int)Math.Min((long)maxSide, (long)currentSide * 2);
            ResetDynamicSession(state);
            state.minimumPageSide = Mathf.Max(state.minimumPageSide, nextSide);
            return true;
        }

        void ResetDynamicSessions(bool sealPages)
        {
            if (_dynamicSessions == null)
                return;

            foreach (var state in _dynamicSessions.Values)
                ResetDynamicSession(state, sealPages);

            _dynamicSessions.Clear();
        }

        bool TryEnsureDynamicSession(
            byte[] fontData,
            int atlasSize,
            int pixelRange,
            DynamicSessionState state,
            out bool budgetExceeded)
        {
            budgetExceeded = false;

            if (state.session != null)
                return true;

            if (state.failed)
                return false;

            try
            {
                int requiredSide = atlasSize + pixelRange + DYNAMIC_GLYPH_PADDING * 2;
                int pageSide = GetDynamicRangePageSize(requiredSide, pixelRange);

                if (state.minimumPageSide > pageSide)
                    pageSide = Mathf.Min(state.minimumPageSide, GetDynamicMaxAtlasSide());

                long pagePayload = GetRgbaTexturePayloadBytes(pageSide, pageSide, 1);
                long futureResidentBytes = pagePayload * 4;

                if (!TryMakeDynamicCacheRoom(futureResidentBytes, state))
                {
                    budgetExceeded = true;
                    return false;
                }

                // Reserve the future readable Texture2D plus GPU page while the
                // session exists without a published page. The live session itself
                // is counted separately as atlas storage plus opaque work space.
                state.reservedPageBytes = pagePayload * 2;

                var encodingMaterial = _dynamicMaterialTemplate;

                if (encodingMaterial == null)
                    encodingMaterial = Now.LoadRequiredResource<Material>("NowUI/TxtMaterial");

                bool usePackedManagedSdf16 =
                    encodingMaterial != null && encodingMaterial.HasProperty(SDF_ENCODING_PROPERTY);

                if (!NowFontCompiler.DynamicSession.TryCreate(
                    fontData,
                    atlasSize,
                    pixelRange,
                    pageSide,
                    usePackedManagedSdf16,
                    out state.session,
                    out _))
                {
                    state.reservedPageBytes = 0;
                    state.failed = true;
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                state.reservedPageBytes = 0;
                s_dynamicSessionUnsupported = true;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                state.reservedPageBytes = 0;
                s_dynamicSessionUnsupported = true;
                return false;
            }
            catch (BadImageFormatException)
            {
                state.reservedPageBytes = 0;
                s_dynamicSessionUnsupported = true;
                return false;
            }

            return true;
        }

        DynamicAtlasPage CreateDynamicSessionPage(
            DynamicSessionState state,
            int side,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;
            var session = state.session;

            if (session == null)
                return null;

            long pageResidentBytes = GetRgbaTexturePayloadBytes(side, side, 1) * 2;

            if (state.reservedPageBytes < pageResidentBytes &&
                !TryMakeDynamicCacheRoom(pageResidentBytes - state.reservedPageBytes, state))
            {
                budgetExceeded = true;
                return null;
            }

            var materialTemplate = _dynamicMaterialTemplate;

            if (materialTemplate == null)
                materialTemplate = Now.LoadRequiredResource<Material>("NowUI/TxtMaterial");

            if (materialTemplate == null)
                return null;

            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false, true)
            {
                name = $"Now Font Page {_dynamicPages?.Count ?? 0}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var material = new Material(materialTemplate)
            {
                name = materialTemplate.name + " Page",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture
            };

            // Managed SDF pages preserve a 16-bit normalized distance in the
            // existing RGBA32 payload (high byte in R/G/A, low byte in B).
            // Red-, green-, alpha-, and median-based legacy readers therefore
            // degrade to the old 8-bit field if the private flag is unavailable.
            // Set both states explicitly because callers may reuse a template
            // across managed and native sessions.
            if (material.HasProperty(SDF_ENCODING_PROPERTY))
                material.SetFloat(SDF_ENCODING_PROPERTY, session.usesPackedSdf16 ? 1f : 0f);

            var pageFont = CreateInstance<NowFont>();
            pageFont.name = "Now Runtime Font Page";
            pageFont.hideFlags = HideFlags.HideAndDontSave;
            pageFont.atlas = texture;
            pageFont.material = material;
            pageFont.atlasInfo = new NowFontAtlasInfo
            {
                atlas = new NowFontAtlasInfo.Atlas
                {
                    type = ATLAS_TYPE_MTSDF,
                    distanceRange = Mathf.RoundToInt(session.DistanceRange),
                    size = Mathf.RoundToInt(session.Size),
                    width = side,
                    height = side,
                    yOrigin = "bottom"
                },
                metrics = session.Metrics,
                glyphs = Array.Empty<NowFontAtlasInfo.Glyph>()
            };

            return new DynamicAtlasPage
            {
                font = pageFont,
                codepoints = new HashSet<int>(),
                atlasSize = atlasSize,
                pixelRange = pixelRange,
                sessionOwned = true
            };
        }

        bool TryCommitSessionGlyphs(
            DynamicSessionState state,
            List<NowFontAtlasInfo.Glyph> glyphs,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;
            var session = state.session;

            if (session == null)
                return false;

            int side = session.AtlasSide;

            if (side <= 0)
                return false;

            var page = state.page;
            bool createdPage = page == null;

            if (createdPage)
            {
                page = CreateDynamicSessionPage(
                    state,
                    side,
                    atlasSize,
                    pixelRange,
                    out budgetExceeded);

                if (page == null)
                    return false;
            }

            var texture = page.font.atlas;

            // Fixed-size sessions never resize their atlas — page textures and glyph UVs must
            // stay valid for meshes built in earlier frames. A mismatch means the session and
            // page are out of sync; refuse to touch the page rather than corrupt it.
            if (texture.width != side || texture.height != side || !texture.isReadable)
            {
                if (createdPage)
                {
                    DestroyDynamicFont(page.font);
                    page.font = null;
                }

                return false;
            }

            NativeArray<byte> textureData = texture.GetRawTextureData<byte>();

            if (!session.TryCopyAtlas(textureData, out _))
            {
                if (createdPage)
                {
                    DestroyDynamicFont(page.font);
                    page.font = null;
                }

                return false;
            }

            texture.Apply(false, false);

            if (createdPage)
            {
                state.page = page;
                _dynamicPages ??= new List<DynamicAtlasPage>();
                _dynamicPages.Add(page);
                state.reservedPageBytes = 0;
            }

            var fontAtlasInfo = page.font.atlasInfo;
            AppendGlyphs(ref fontAtlasInfo.glyphs, glyphs);
            page.font.atlasInfo = fontAtlasInfo;
            page.font.ClearGlyphCache();

            page.codepoints ??= new HashSet<int>();
            _dynamicGlyphPages ??= new Dictionary<DynamicGlyphKey, DynamicAtlasPage>();

            for (int i = 0; i < glyphs.Count; ++i)
            {
                int unicode = glyphs[i].unicode;
                page.codepoints.Add(unicode);
                _dynamicGlyphPages[new DynamicGlyphKey(unicode, atlasSize, pixelRange)] = page;
            }

            InvalidateDynamicFallbackSelections();

            return true;
        }

        void MarkSessionMisses(
            int[] codepoints,
            int codepointCount,
            HashSet<int> returned,
            int atlasSize,
            int pixelRange)
        {
            if (returned.Count >= codepointCount)
                return;

            for (int i = 0; i < codepointCount; ++i)
            {
                if (!returned.Contains(codepoints[i]))
                    AddDynamicMiss(new DynamicGlyphKey(codepoints[i], atlasSize, pixelRange));
            }
        }

        bool CommitAndFailDynamicSession(
            DynamicSessionState state,
            List<NowFontAtlasInfo.Glyph> results,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;

            if (results.Count > 0 &&
                !TryCommitSessionGlyphs(
                    state,
                    results,
                    atlasSize,
                    pixelRange,
                    out budgetExceeded))
            {
                ResetDynamicSession(state);

                if (!budgetExceeded)
                    state.failed = true;

                return false;
            }

            ResetDynamicSession(state);
            state.failed = true;
            return false;
        }

        /// <summary>
        /// Bakes the missing characters through the persistent native session. Session atlases
        /// have a fixed size, so baked glyph UVs and page textures stay valid forever; when a
        /// page fills up it is sealed and a fresh session/page takes over, mirroring the legacy
        /// multi-page behavior. Returns true when the request was handled (glyphs baked and/or
        /// misses recorded); false means the caller should use the legacy per-page compiler
        /// (color fonts, old native plugins, failures).
        /// </summary>
        bool TryAddGlyphsToSession(
            string characters,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;
            const int SESSION_ADD_CHUNK = 64;

            if (s_dynamicSessionUnsupported)
                return false;

            var fontData = DynamicFontBytes;

            if (fontData == null || string.IsNullOrEmpty(characters))
                return false;

            using var profile = NowProfiler.FontBake.Auto();
            _dynamicSourceIsColor ??= NowFontCompiler.IsColorFont(fontData);

            if (_dynamicSourceIsColor.Value)
                return false;

            var state = GetDynamicSessionState(atlasSize, pixelRange);

            if (state.failed)
                return false;

            if (state.page != null && !IsDynamicPageValid(state.page))
                ResetDynamicSession(state);

            var codepoints = GetDynamicCompileCodepoints(characters, out int codepointCount);

            if (codepointCount <= 0)
                return TryEnsureDynamicSession(
                    fontData,
                    atlasSize,
                    pixelRange,
                    state,
                    out budgetExceeded);

            var results = _dynamicSessionGlyphScratch ??= new List<NowFontAtlasInfo.Glyph>();
            var returned = _dynamicSessionReturnedScratch ??= new HashSet<int>();
            var chunkCodepoints = _dynamicSessionChunkScratch ??= new int[SESSION_ADD_CHUNK];
            results.Clear();
            returned.Clear();

            int offset = 0;
            int chunkLimit = SESSION_ADD_CHUNK;

            while (offset < codepointCount)
            {
                if (!TryEnsureDynamicSession(
                    fontData,
                    atlasSize,
                    pixelRange,
                    state,
                    out bool ensureBudgetExceeded))
                {
                    if (results.Count > 0)
                    {
                        TryCommitSessionGlyphs(
                            state,
                            results,
                            atlasSize,
                            pixelRange,
                            out bool commitBudgetExceeded);
                        budgetExceeded |= commitBudgetExceeded;
                    }

                    budgetExceeded |= ensureBudgetExceeded;
                    return false;
                }

                int chunk = Mathf.Min(chunkLimit, codepointCount - offset);
                Array.Copy(codepoints, offset, chunkCodepoints, 0, chunk);

                int resultsBefore = results.Count;
                var status = state.session.TryAddGlyphs(chunkCodepoints, chunk, results, out _);

                if (status == NowFontCompiler.DynamicSession.AddResult.Ok)
                {
                    for (int i = resultsBefore; i < results.Count; ++i)
                        returned.Add(results[i].unicode);

                    offset += chunk;
                    chunkLimit = SESSION_ADD_CHUNK;
                    continue;
                }

                if (status == NowFontCompiler.DynamicSession.AddResult.AtlasFull)
                {
                    if (results.Count > 0)
                    {
                        bool committed = TryCommitSessionGlyphs(
                            state,
                            results,
                            atlasSize,
                            pixelRange,
                            out bool commitBudgetExceeded);
                        results.Clear();

                        if (!committed)
                        {
                            ResetDynamicSession(state);

                            if (commitBudgetExceeded)
                                budgetExceeded = true;
                            else
                                state.failed = true;

                            return false;
                        }
                    }

                    if (state.page == null)
                    {
                        if (chunk <= 1)
                        {
                            if (TryGrowEmptyDynamicSession(state))
                                continue;

                            // Let the tight legacy compiler try the actual glyph bounds
                            // before treating a conservative fixed-session estimate as a miss.
                            return false;
                        }

                        chunkLimit = Mathf.Max(1, chunk / 2);
                        continue;
                    }

                    ResetDynamicSession(state);
                    continue;
                }

                return CommitAndFailDynamicSession(
                    state,
                    results,
                    atlasSize,
                    pixelRange,
                    out budgetExceeded);
            }

            if (results.Count > 0 &&
                !TryCommitSessionGlyphs(
                    state,
                    results,
                    atlasSize,
                    pixelRange,
                    out bool finalCommitBudgetExceeded))
            {
                ResetDynamicSession(state);
                budgetExceeded = finalCommitBudgetExceeded;

                if (!budgetExceeded)
                    state.failed = true;

                return false;
            }

            MarkSessionMisses(codepoints, codepointCount, returned, atlasSize, pixelRange);
            results.Clear();
            returned.Clear();
            return true;
        }

        void TryCompileMissingGlyphs(string characters, int atlasSize, int pixelRange)
        {
            if (DynamicFontBytes == null || string.IsNullOrEmpty(characters)) return;

            if (TryAddGlyphsToSession(characters, atlasSize, pixelRange, out bool budgetExceeded))
                return;

            if (budgetExceeded)
            {
                characters = GetMissingDynamicCharacters(characters, atlasSize, pixelRange);
                MarkDynamicCapacityMisses(characters, atlasSize, pixelRange);
                return;
            }

            // A session failure may occur after earlier chunks were committed. Compile
            // only what remains so the fallback never duplicates published mappings.
            characters = GetMissingDynamicCharacters(characters, atlasSize, pixelRange);

            if (string.IsNullOrEmpty(characters))
                return;

            if (!TryCompileDynamicPage(characters, atlasSize, pixelRange, out var glyphFont))
            {
                TryCompileMissingGlyphsIndividually(characters, atlasSize, pixelRange);
                return;
            }

            try
            {
                EnsureColorLayoutGlyphs(characters, atlasSize, glyphFont);

                var batch = new DynamicGlyphAppendBatch();
                var rawGlyphs = BuildRawGlyphMap(glyphFont);

                for (int i = 0; i < characters.Length; ++i)
                {
                    int unicode = ReadCodepoint(characters, ref i);
                    var key = new DynamicGlyphKey(unicode, atlasSize, pixelRange);

                    if (TryCacheCompiledGlyph(
                        glyphFont,
                        unicode,
                        atlasSize,
                        pixelRange,
                        out bool glyphBudgetExceeded,
                        batch,
                        rawGlyphs))
                    {
                        continue;
                    }

                    if (glyphBudgetExceeded)
                        AddDynamicCapacityMiss(key);
                    else
                        AddDynamicMiss(key);
                }

                batch.Commit();
            }
            finally
            {
                DestroyDynamicFont(glyphFont);
            }
        }

        bool TryCompileMissingGlyph(int unicode, int atlasSize, int pixelRange)
        {
            if (!ShouldCompileDynamicGlyph(unicode, atlasSize, pixelRange))
                return false;

            var key = new DynamicGlyphKey(unicode, atlasSize, pixelRange);
            string character = CodepointToString(unicode);

            if (TryAddGlyphsToSession(character, atlasSize, pixelRange, out bool budgetExceeded))
                return TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out _);

            if (budgetExceeded)
            {
                AddDynamicCapacityMiss(key);
                return false;
            }

            if (!TryCompileDynamicPage(character, atlasSize, pixelRange, out var glyphFont))
            {
                AddDynamicMiss(key);
                return false;
            }

            try
            {
                EnsureColorLayoutGlyphs(character, atlasSize, glyphFont);

                var batch = new DynamicGlyphAppendBatch();

                if (TryCacheCompiledGlyph(
                    glyphFont,
                    unicode,
                    atlasSize,
                    pixelRange,
                    out bool glyphBudgetExceeded,
                    batch))
                {
                    batch.Commit();
                    return true;
                }

                if (glyphBudgetExceeded)
                    AddDynamicCapacityMiss(key);
                else
                    AddDynamicMiss(key);

                return false;
            }
            finally
            {
                DestroyDynamicFont(glyphFont);
            }
        }

        bool TryCompileMissingShapedGlyph(int encodedGlyphIndex, int atlasSize, int pixelRange)
        {
            int glyphIndex = -1 - encodedGlyphIndex;

            if (DynamicFontBytes == null || glyphIndex <= 0)
                return false;

            var key = new DynamicGlyphKey(encodedGlyphIndex, atlasSize, pixelRange);

            if ((_dynamicCapacityMisses != null && _dynamicCapacityMisses.Contains(key)) ||
                (_dynamicMisses != null && _dynamicMisses.Contains(key)))
            {
                return false;
            }

            if (TryGetDynamicCachedGlyph(encodedGlyphIndex, atlasSize, pixelRange, out _))
                return true;

            var missing = _shapedMissingScratch ??= new List<int>(32);
            missing.Clear();
            missing.Add(glyphIndex);

            bool baked;
            bool budgetExceeded;

            try
            {
                baked = TryBakeShapedGlyphs(missing, atlasSize, pixelRange, out budgetExceeded);
            }
            finally
            {
                missing.Clear();
            }

            if ((baked || budgetExceeded) &&
                TryGetDynamicCachedGlyph(encodedGlyphIndex, atlasSize, pixelRange, out _))
            {
                return true;
            }

            if (budgetExceeded)
                AddDynamicCapacityMiss(key);
            else
                AddDynamicMiss(key);

            return false;
        }

        internal void InitializeDynamicSource(
            byte[] fontData,
            int atlasSize = 64,
            int pixelRange = 16,
            int maxAtlasSize = DEFAULT_DYNAMIC_MAX_ATLAS_SIZE,
            int maxAtlasBytes = DEFAULT_DYNAMIC_MAX_ATLAS_BYTES,
            Material materialTemplate = null)
        {
            atlas = null;
            atlasInfo = default;
            material = null;
            _fontBytes = fontData;
            dynamicAtlasSize = atlasSize;
            dynamicPixelRange = pixelRange;
            dynamicPageSize = DEFAULT_DYNAMIC_PAGE_SIZE;
            dynamicMaxAtlasSize = maxAtlasSize;
            dynamicMaxAtlasBytes = maxAtlasBytes;
            _dynamicMaterialTemplate = materialTemplate;
            _dynamicColorBitmapSizes = null;
            _didReadDynamicColorBitmapSizes = false;
            _dynamicSourceCodepoints = null;
            _didReadDynamicSourceCodepoints = false;
            _dynamicSourceIsColor = null;
            _textShaper?.Dispose();
            _textShaper = null;
            _textShaperFailed = false;
            _shapeCache = null;
            ++_shapedDataVersion;
            ClearDynamicCache();
            ClearGlyphCache();
        }

        public void EnsureGlyphs(string value, float fontSize)
        {
            EnsureGlyphs(value, fontSize, 0f);
        }

        internal void EnsureGlyphs(string value, float fontSize, float outline)
        {
            if (DynamicFontBytes == null || string.IsNullOrEmpty(value) || fontSize <= 0)
                return;

            int atlasSize = GetDynamicGlyphSize(fontSize);
            int pixelRange = GetDynamicPixelRange(outline, fontSize);
            string missingCharacters = GetMissingDynamicCharacters(value, atlasSize, pixelRange);

            if (!string.IsNullOrEmpty(missingCharacters))
                TryCompileMissingGlyphs(missingCharacters, atlasSize, pixelRange);
        }

        public bool GetGlyph(char c, out NowFontAtlasInfo.Glyph glyph)
        {
            return GetGlyph((int)c, out glyph);
        }

        public bool GetGlyph(char c, float fontSize, out NowFontAtlasInfo.Glyph glyph)
        {
            return GetGlyph((int)c, fontSize, out glyph);
        }

        public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph)
        {
            return GetGlyph(unicode, dynamicAtlasSize, out glyph);
        }

        public bool GetGlyph(int unicode, float fontSize, out NowFontAtlasInfo.Glyph glyph)
        {
            return GetGlyph(unicode, fontSize, 0f, out glyph);
        }

        [NonSerialized] float _glyphTierFontSize = float.NaN;
        [NonSerialized] float _glyphTierOutline = float.NaN;
        [NonSerialized] int _glyphTierDynamicAtlasSize;
        [NonSerialized] int _glyphTierDynamicPixelRange;
        [NonSerialized] int _glyphTierDynamicMaxAtlasSize;
        [NonSerialized] int _glyphTierDynamicMaxAtlasBytes;
        [NonSerialized] bool _glyphTierHasSource;
        [NonSerialized] string _glyphTierAtlasType;
        [NonSerialized] int _glyphTierAtlasDistanceRange;
        [NonSerialized] int _glyphTierAtlasSize;
        [NonSerialized] int _glyphTierResolvedAtlasSize;
        [NonSerialized] int _glyphTierResolvedPixelRange;
        [NonSerialized] bool _glyphTierBaseSupportsRange;

        /// <summary>
        /// Resolves the dynamic atlas size, the distance-range tier, and whether
        /// the baked base atlas already satisfies that tier for one (font size,
        /// outline) pair. Codepoint draws ask this once per glyph, so the answer
        /// is memoized against every input it reads; a run of glyphs at one size
        /// pays the derivation once instead of per character.
        /// </summary>
        void ResolveGlyphTier(
            float fontSize,
            float outline,
            out int atlasSize,
            out int pixelRange,
            out bool baseSupportsRange)
        {
            bool hasSource = HasEmbeddedSource;

            if (fontSize == _glyphTierFontSize &&
                outline == _glyphTierOutline &&
                hasSource == _glyphTierHasSource &&
                dynamicAtlasSize == _glyphTierDynamicAtlasSize &&
                dynamicPixelRange == _glyphTierDynamicPixelRange &&
                dynamicMaxAtlasSize == _glyphTierDynamicMaxAtlasSize &&
                dynamicMaxAtlasBytes == _glyphTierDynamicMaxAtlasBytes &&
                ReferenceEquals(atlasInfo.atlas.type, _glyphTierAtlasType) &&
                atlasInfo.atlas.distanceRange == _glyphTierAtlasDistanceRange &&
                atlasInfo.atlas.size == _glyphTierAtlasSize)
            {
                atlasSize = _glyphTierResolvedAtlasSize;
                pixelRange = _glyphTierResolvedPixelRange;
                baseSupportsRange = _glyphTierBaseSupportsRange;
                return;
            }

            atlasSize = GetDynamicGlyphSize(fontSize);
            pixelRange = GetDynamicPixelRange(outline, fontSize);
            baseSupportsRange = !hasSource || AtlasSupportsPixelRange(atlasInfo.atlas, atlasSize, pixelRange);

            _glyphTierFontSize = fontSize;
            _glyphTierOutline = outline;
            _glyphTierHasSource = hasSource;
            _glyphTierDynamicAtlasSize = dynamicAtlasSize;
            _glyphTierDynamicPixelRange = dynamicPixelRange;
            _glyphTierDynamicMaxAtlasSize = dynamicMaxAtlasSize;
            _glyphTierDynamicMaxAtlasBytes = dynamicMaxAtlasBytes;
            _glyphTierAtlasType = atlasInfo.atlas.type;
            _glyphTierAtlasDistanceRange = atlasInfo.atlas.distanceRange;
            _glyphTierAtlasSize = atlasInfo.atlas.size;
            _glyphTierResolvedAtlasSize = atlasSize;
            _glyphTierResolvedPixelRange = pixelRange;
            _glyphTierBaseSupportsRange = baseSupportsRange;
        }

        internal bool GetGlyph(int unicode, float fontSize, float outline, out NowFontAtlasInfo.Glyph glyph)
        {
            ResolveGlyphTier(fontSize, outline, out int atlasSize, out int pixelRange, out bool baseSupportsRange);
            bool hasBaseGlyph = TryGetCachedGlyph(unicode, out glyph);

            if (hasBaseGlyph && baseSupportsRange)
                return true;

            if (TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph))
                return true;

            if (TryCompileMissingGlyph(unicode, atlasSize, pixelRange) &&
                TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph))
            {
                return true;
            }

            if (TryGetDynamicCapacityFallbackGlyph(
                unicode,
                atlasSize,
                pixelRange,
                out glyph,
                out _))
            {
                return true;
            }

            return hasBaseGlyph;
        }

        public bool GetGlyph(int unicode, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
        {
            return GetGlyph(unicode, dynamicAtlasSize, out glyph, out glyphMaterial);
        }

        public bool GetGlyph(int unicode, float fontSize, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
        {
            return GetGlyph(unicode, fontSize, 0f, out glyph, out glyphMaterial);
        }

        internal bool GetGlyph(
            int unicode,
            float fontSize,
            float outline,
            out NowFontAtlasInfo.Glyph glyph,
            out Material glyphMaterial)
        {
            ResolveGlyphTier(fontSize, outline, out _, out int pixelRange, out bool baseSupportsRange);

            if (baseSupportsRange && TryGetCachedGlyph(unicode, out glyph))
            {
                glyphMaterial = material;
                return true;
            }

            return GetGlyphForPixelRange(unicode, fontSize, pixelRange, out glyph, out glyphMaterial);
        }

        /// <summary>
        /// Resolves one glyph against an already-selected hidden dynamic range
        /// tier. Internal multi-glyph consumers use this to keep every glyph
        /// sampling one atlas variant even when their displayed font sizes differ.
        /// </summary>
        internal bool GetGlyphForPixelRange(
            int unicode,
            float fontSize,
            int pixelRange,
            out NowFontAtlasInfo.Glyph glyph,
            out Material glyphMaterial)
        {
            int atlasSize = GetDynamicGlyphSize(fontSize);
            pixelRange = Mathf.Max(1, pixelRange);
            bool hasBaseGlyph = TryGetCachedGlyph(unicode, out glyph);

            if (hasBaseGlyph &&
                (!HasEmbeddedSource || AtlasSupportsPixelRange(atlasInfo.atlas, atlasSize, pixelRange)))
            {
                glyphMaterial = material;
                return true;
            }

            if (TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph, out var page) ||
                (TryCompileMissingGlyph(unicode, atlasSize, pixelRange) &&
                    TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph, out page)))
            {
                glyphMaterial = page.font.material;
                return true;
            }

            if (TryGetDynamicCapacityFallbackGlyph(
                unicode,
                atlasSize,
                pixelRange,
                out glyph,
                out page))
            {
                glyphMaterial = page.font.material;
                return glyphMaterial != null;
            }

            if (hasBaseGlyph)
            {
                glyphMaterial = material;
                return true;
            }

            glyphMaterial = null;
            return false;
        }

        /// <summary>
        /// Resolves only the requested raw distance-range tier. Unlike the normal
        /// glyph getter, this never substitutes a lower cached range: scene-level
        /// consumers use failure to choose one coherent fallback tier themselves.
        /// </summary>
        internal bool GetGlyphForExactPixelRange(
            int unicode,
            float fontSize,
            int pixelRange,
            out NowFontAtlasInfo.Glyph glyph,
            out Material glyphMaterial,
            out float screenPixelRange)
        {
            int atlasSize = GetDynamicGlyphSize(fontSize);
            pixelRange = Mathf.Max(1, pixelRange);

            if (TryGetCachedGlyph(unicode, out glyph) &&
                AtlasSupportsPixelRange(atlasInfo.atlas, atlasSize, pixelRange))
            {
                glyphMaterial = material;
                screenPixelRange = ScreenPixelRange(fontSize, atlasInfo.atlas);
                return true;
            }

            if (TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph, out var page) ||
                ((unicode < 0
                        ? TryCompileMissingShapedGlyph(unicode, atlasSize, pixelRange)
                        : TryCompileMissingGlyph(unicode, atlasSize, pixelRange)) &&
                    TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out glyph, out page)))
            {
                glyphMaterial = page.font.material;
                screenPixelRange = ScreenPixelRange(fontSize, page.font.atlasInfo.atlas);
                return glyphMaterial != null;
            }

            glyph = default;
            glyphMaterial = null;
            screenPixelRange = 0f;
            return false;
        }

        internal bool HasGlyphForExactPixelRange(int unicode, float fontSize, int pixelRange)
        {
            int atlasSize = GetDynamicGlyphSize(fontSize);
            pixelRange = Mathf.Max(1, pixelRange);

            return (TryGetCachedGlyph(unicode, out _) &&
                    AtlasSupportsPixelRange(atlasInfo.atlas, atlasSize, pixelRange)) ||
                TryGetDynamicCachedGlyph(unicode, atlasSize, pixelRange, out _);
        }

        [NonSerialized]
        NowTextShaper _textShaper;

        [NonSerialized]
        bool _textShaperFailed;

        [NonSerialized]
        Dictionary<string, ShapedRunCacheEntry> _shapeCache;

        [NonSerialized]
        List<NowTextShaper.ShapedGlyph> _shapeScratch;

        [NonSerialized]
        List<int> _shapedMissingScratch;

        [NonSerialized]
        int[] _shapedBakeIndexScratch;

        [NonSerialized]
        Dictionary<PreparedShapeKey, PreparedShapedRun> _preparedShapeCache;

        [NonSerialized]
        Dictionary<PreparedCodepointRunKey, PreparedCodepointRun> _preparedCodepointCache;

        [NonSerialized]
        int _shapedDataVersion;

        [NonSerialized]
        int _shapeCacheGeneration;

        [NonSerialized]
        List<string> _shapeCacheEvictScratch;

        [NonSerialized]
        int _preparedShapeGeneration;

        [NonSerialized]
        List<PreparedShapeKey> _preparedShapeEvictScratch;

        [NonSerialized]
        int _preparedCodepointGeneration;

        [NonSerialized]
        List<PreparedCodepointRunKey> _preparedCodepointEvictScratch;

        /// <summary>Bumped when the embedded font source is replaced, so string-keyed
        /// measure memos tied to this font's shaped advances invalidate.</summary>
        internal int shapedDataVersion => _shapedDataVersion;

        internal override int layoutDataVersion => _shapedDataVersion;

        /// <summary>Shape-cache slot; a null run is a cached negative (the segment cannot shape).</summary>
        sealed class ShapedRunCacheEntry
        {
            public NowTextShaper.ShapedGlyph[] run;

            public int generation;
        }

        const int SHAPE_CACHE_LIMIT = 2048;

        internal readonly struct PreparedCodepointGlyph
        {
            public readonly int codepoint;

            public readonly float advance;

            public readonly NowFontAtlasInfo.Glyph glyph;

            public readonly Material material;

            public readonly bool visible;

            public readonly bool lineBreak;

            public readonly PreparedTextGlyph textGlyph;

            public PreparedCodepointGlyph(
                int codepoint,
                float advance,
                in NowFontAtlasInfo.Glyph glyph,
                Material material,
                bool visible,
                bool lineBreak)
            {
                this.codepoint = codepoint;
                this.advance = advance;
                this.glyph = glyph;
                this.material = material;
                this.visible = visible;
                this.lineBreak = lineBreak;
                textGlyph = new PreparedTextGlyph(glyph, advance, visible);
            }
        }

        internal sealed class PreparedCodepointRun
        {
            public readonly PreparedCodepointGlyph[] glyphs;

            public readonly PreparedTextGlyph[] textGlyphs;

            public int generation;

            float _measuredFontSize;

            float _measuredMaxWidth;

            int _measuredLineCount;

            bool _hasMeasuredSize;

            float _measuredBoundsFontSize;

            Vector4 _measuredBounds;

            bool _hasMeasuredBounds;

            public int length => glyphs.Length;

            public PreparedCodepointRun(PreparedCodepointGlyph[] glyphs)
            {
                this.glyphs = glyphs;
                textGlyphs = new PreparedTextGlyph[glyphs.Length];

                for (int i = 0; i < glyphs.Length; ++i)
                    textGlyphs[i] = glyphs[i].textGlyph;
            }

            /// <summary>One-slot memo of the advance-sum loop's exact output for the last
            /// font size measured, so repeat measures are O(1) and bit-identical. The run's
            /// advances are immutable, so only the size keys the memo.</summary>
            public bool TryGetMeasuredSize(float fontSize, out float maxWidth, out int lineCount)
            {
                maxWidth = _measuredMaxWidth;
                lineCount = _measuredLineCount;
                return _hasMeasuredSize && _measuredFontSize == fontSize;
            }

            public void StoreMeasuredSize(float fontSize, float maxWidth, int lineCount)
            {
                _measuredFontSize = fontSize;
                _measuredMaxWidth = maxWidth;
                _measuredLineCount = lineCount;
                _hasMeasuredSize = true;
            }

            public bool TryGetMeasuredBounds(float fontSize, out Vector4 bounds)
            {
                bounds = _measuredBounds;
                return _hasMeasuredBounds && _measuredBoundsFontSize == fontSize;
            }

            public void StoreMeasuredBounds(float fontSize, Vector4 bounds)
            {
                _measuredBoundsFontSize = fontSize;
                _measuredBounds = bounds;
                _hasMeasuredBounds = true;
            }
        }

        internal readonly struct PreparedShapedGlyph
        {
            public readonly uint glyphIndex;

            /// <summary>UTF-16 cluster index in the shaped source segment.</summary>
            public readonly uint cluster;

            /// <summary>Logical animation unit for this cluster.</summary>
            public readonly int animationUnit;

            public readonly int encodedKey;

            public readonly float xAdvance;

            public readonly float xOffset;

            public readonly float yOffset;

            public readonly NowFontAtlasInfo.Glyph glyph;

            public readonly Material material;

            public readonly bool visible;

            public readonly PreparedTextGlyph textGlyph;

            public PreparedShapedGlyph(
                in NowTextShaper.ShapedGlyph shaped,
                NowFontAtlasInfo.Glyph glyph,
                Material material,
                int animationUnit)
            {
                glyphIndex = shaped.glyphIndex;
                cluster = shaped.cluster;
                this.animationUnit = animationUnit;
                encodedKey = EncodeGlyphIndexKey((int)shaped.glyphIndex);
                xAdvance = shaped.xAdvance;
                xOffset = shaped.xOffset;
                yOffset = shaped.yOffset;
                this.glyph = glyph;
                this.material = material;
                visible = !Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right);
                textGlyph = new PreparedTextGlyph(shaped, glyph, visible);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal readonly struct PreparedTextGlyph
        {
            public readonly float planeLeft;

            public readonly float planeBottom;

            public readonly float planeRight;

            public readonly float planeTop;

            public readonly float atlasLeft;

            public readonly float atlasBottom;

            public readonly float atlasRight;

            public readonly float atlasTop;

            public readonly float xAdvance;

            public readonly float xOffset;

            public readonly float yOffset;

            public readonly float visible;

            public PreparedTextGlyph(
                in NowTextShaper.ShapedGlyph shaped,
                in NowFontAtlasInfo.Glyph glyph,
                bool visible)
            {
                planeLeft = glyph.planeBounds.left;
                planeBottom = glyph.planeBounds.bottom;
                planeRight = glyph.planeBounds.right;
                planeTop = glyph.planeBounds.top;
                atlasLeft = glyph.atlasBounds.left;
                atlasBottom = glyph.atlasBounds.bottom;
                atlasRight = glyph.atlasBounds.right;
                atlasTop = glyph.atlasBounds.top;
                xAdvance = shaped.xAdvance;
                xOffset = shaped.xOffset;
                yOffset = shaped.yOffset;
                this.visible = visible ? 1f : 0f;
            }

            public PreparedTextGlyph(
                in NowFontAtlasInfo.Glyph glyph,
                float advance,
                bool visible)
            {
                planeLeft = glyph.planeBounds.left;
                planeBottom = glyph.planeBounds.bottom;
                planeRight = glyph.planeBounds.right;
                planeTop = glyph.planeBounds.top;
                atlasLeft = glyph.atlasBounds.left;
                atlasBottom = glyph.atlasBounds.bottom;
                atlasRight = glyph.atlasBounds.right;
                atlasTop = glyph.atlasBounds.top;
                xAdvance = advance;
                xOffset = 0f;
                yOffset = 0f;
                this.visible = visible ? 1f : 0f;
            }
        }

        internal sealed class PreparedShapedRun
        {
            public readonly PreparedShapedGlyph[] glyphs;

            public readonly PreparedTextGlyph[] textGlyphs;

            public int generation;

            public readonly int animationUnitCount;

            public int length => glyphs.Length;

            public PreparedShapedRun(PreparedShapedGlyph[] glyphs)
            {
                this.glyphs = glyphs;
                textGlyphs = new PreparedTextGlyph[glyphs.Length];
                int maxAnimationUnit = -1;

                for (int i = 0; i < glyphs.Length; ++i)
                {
                    textGlyphs[i] = glyphs[i].textGlyph;
                    maxAnimationUnit = Mathf.Max(maxAnimationUnit, glyphs[i].animationUnit);
                }

                animationUnitCount = maxAnimationUnit + 1;
            }
        }

        readonly struct PreparedShapeKey : IEquatable<PreparedShapeKey>
        {
            readonly string _segment;

            readonly int _atlasSize;

            readonly int _pixelRange;

            public PreparedShapeKey(string segment, int atlasSize, int pixelRange)
            {
                _segment = segment;
                _atlasSize = atlasSize;
                _pixelRange = pixelRange;
            }

            public bool Equals(PreparedShapeKey other)
            {
                return _atlasSize == other._atlasSize &&
                    _pixelRange == other._pixelRange &&
                    string.Equals(_segment, other._segment, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PreparedShapeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ((_segment != null ? _segment.GetHashCode() : 0) * 397) ^ _atlasSize;
                    return (hash * 397) ^ _pixelRange;
                }
            }
        }

        readonly struct PreparedCodepointRunKey : IEquatable<PreparedCodepointRunKey>
        {
            readonly string _value;

            readonly int _atlasSize;

            readonly int _pixelRange;

            readonly NowFontStyle _style;

            readonly int _tabSpaces;

            public PreparedCodepointRunKey(
                string value,
                int atlasSize,
                int pixelRange,
                NowFontStyle style,
                int tabSpaces)
            {
                _value = value;
                _atlasSize = atlasSize;
                _pixelRange = pixelRange;
                _style = style;
                _tabSpaces = tabSpaces;
            }

            public bool Equals(PreparedCodepointRunKey other)
            {
                return _atlasSize == other._atlasSize &&
                    _pixelRange == other._pixelRange &&
                    _style == other._style &&
                    _tabSpaces == other._tabSpaces &&
                    string.Equals(_value, other._value, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PreparedCodepointRunKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _value != null ? _value.GetHashCode() : 0;
                    hash = (hash * 397) ^ _atlasSize;
                    hash = (hash * 397) ^ _pixelRange;
                    hash = (hash * 397) ^ (int)_style;
                    hash = (hash * 397) ^ _tabSpaces;
                    return hash;
                }
            }
        }

        void ClearPreparedShapeCache()
        {
            _preparedShapeCache?.Clear();
        }

        void ClearPreparedCodepointCache()
        {
            _preparedCodepointCache?.Clear();
        }

        /// <summary>Second-chance eviction shared by the text caches: entries untouched since
        /// the previous sweep (and cached negatives) are dropped, so hot strings survive the
        /// size cap instead of every string re-shaping and re-preparing in a burst when one
        /// dynamic string marches the cache to its limit.</summary>
        void EvictStaleShapeCacheEntries()
        {
            var stale = _shapeCacheEvictScratch ??= new List<string>();
            stale.Clear();

            foreach (var entry in _shapeCache)
            {
                if (entry.Value.generation != _shapeCacheGeneration)
                    stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; ++i)
                _shapeCache.Remove(stale[i]);

            stale.Clear();

            if (_shapeCache.Count >= SHAPE_CACHE_LIMIT)
                _shapeCache.Clear();

            ++_shapeCacheGeneration;
        }

        void EvictStalePreparedShapeEntries()
        {
            var stale = _preparedShapeEvictScratch ??= new List<PreparedShapeKey>();
            stale.Clear();

            foreach (var entry in _preparedShapeCache)
            {
                if (entry.Value == null || entry.Value.generation != _preparedShapeGeneration)
                    stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; ++i)
                _preparedShapeCache.Remove(stale[i]);

            stale.Clear();

            if (_preparedShapeCache.Count >= SHAPE_CACHE_LIMIT)
                _preparedShapeCache.Clear();

            ++_preparedShapeGeneration;
        }

        void EvictStalePreparedCodepointEntries()
        {
            var stale = _preparedCodepointEvictScratch ??= new List<PreparedCodepointRunKey>();
            stale.Clear();

            foreach (var entry in _preparedCodepointCache)
            {
                if (entry.Value == null || entry.Value.generation != _preparedCodepointGeneration)
                    stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; ++i)
                _preparedCodepointCache.Remove(stale[i]);

            stale.Clear();

            if (_preparedCodepointCache.Count >= SHAPE_CACHE_LIMIT)
                _preparedCodepointCache.Clear();

            ++_preparedCodepointGeneration;
        }

        internal bool TryGetPreparedCodepointRun(
            string value,
            float fontSize,
            NowFontStyle style,
            int tabSpaces,
            out PreparedCodepointRun run)
        {
            return TryGetPreparedCodepointRun(value, fontSize, 0f, style, tabSpaces, out run);
        }

        internal bool TryGetPreparedCodepointRun(
            string value,
            float fontSize,
            float outline,
            NowFontStyle style,
            int tabSpaces,
            out PreparedCodepointRun run)
        {
            run = null;

            if (string.IsNullOrEmpty(value) || fontSize <= 0)
                return false;

            ResolveGlyphTier(fontSize, outline, out int atlasSize, out int pixelRange, out _);
            var key = new PreparedCodepointRunKey(value, atlasSize, pixelRange, style, tabSpaces);
            _preparedCodepointCache ??= new Dictionary<PreparedCodepointRunKey, PreparedCodepointRun>(64);

            if (_preparedCodepointCache.TryGetValue(key, out run))
            {
                if (run == null)
                    return false;

                run.generation = _preparedCodepointGeneration;
                return true;
            }

            EnsureGlyphs(value, fontSize, outline);

            float tabAdvance = 0f;
            bool hasTabAdvance = false;
            var prepared = new List<PreparedCodepointGlyph>(value.Length);

            for (int i = 0; i < value.Length; ++i)
            {
                int codepoint = ReadCodepoint(value, ref i);

                if (codepoint == '\n')
                {
                    prepared.Add(new PreparedCodepointGlyph(codepoint, 0f, default, null, false, true));
                    continue;
                }

                if (codepoint == '\r')
                {
                    prepared.Add(new PreparedCodepointGlyph(codepoint, 0f, default, null, false, false));
                    continue;
                }

                if (codepoint == '\t')
                {
                    if (!hasTabAdvance)
                    {
                        tabAdvance = GetGlyph(' ', fontSize, outline, out var space, out _)
                            ? space.advance * tabSpaces
                            : 0f;
                        hasTabAdvance = true;
                    }

                    prepared.Add(new PreparedCodepointGlyph(codepoint, tabAdvance, default, null, false, false));
                    continue;
                }

                if (!GetGlyph(codepoint, fontSize, outline, out var glyph, out var glyphMaterial))
                {
                    _preparedCodepointCache[key] = null;
                    prepared.Clear();
                    return false;
                }

                bool visible = !Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right);
                prepared.Add(new PreparedCodepointGlyph(codepoint, glyph.advance, glyph, glyphMaterial, visible, false));
            }

            if (_preparedCodepointCache.Count >= SHAPE_CACHE_LIMIT)
                EvictStalePreparedCodepointEntries();

            run = new PreparedCodepointRun(prepared.ToArray()) { generation = _preparedCodepointGeneration };
            _preparedCodepointCache[key] = run;
            return true;
        }

        internal Vector2 MeasurePreparedCodepointRun(PreparedCodepointRun run, float fontSize, NowFontStyle style)
        {
            if (run == null || run.length == 0)
                return default;

            if (run.TryGetMeasuredSize(fontSize, out float memoWidth, out int memoLines))
                return new Vector2(memoWidth, GetLineHeight(style) * fontSize * memoLines);

            float lineWidth = 0f;
            float maxWidth = 0f;
            int lineCount = 1;
            var glyphs = run.glyphs;

            for (int i = 0; i < run.length; ++i)
            {
                ref readonly var prepared = ref glyphs[i];

                if (prepared.lineBreak)
                {
                    if (lineWidth > maxWidth)
                        maxWidth = lineWidth;

                    lineWidth = 0f;
                    ++lineCount;
                    continue;
                }

                lineWidth += prepared.advance * fontSize;
            }

            if (lineWidth > maxWidth)
                maxWidth = lineWidth;

            run.StoreMeasuredSize(fontSize, maxWidth, lineCount);
            return new Vector2(maxWidth, GetLineHeight(style) * fontSize * lineCount);
        }

        internal Vector4 MeasurePreparedCodepointRunBounds(PreparedCodepointRun run, float fontSize, NowFontStyle style)
        {
            if (run == null || run.length == 0)
                return default;

            if (run.TryGetMeasuredBounds(fontSize, out var memoBounds))
                return memoBounds;

            float cursorX = 0f;
            float lineY = 0f;
            float lineHeight = GetLineHeight(style) * fontSize;
            float baseline = GetAscender(style) * fontSize;
            float minX = 0f;
            float minY = 0f;
            float maxX = 0f;
            float maxY = 0f;
            bool hasBounds = false;
            var glyphs = run.glyphs;

            for (int i = 0; i < run.length; ++i)
            {
                ref readonly var prepared = ref glyphs[i];

                if (prepared.lineBreak)
                {
                    cursorX = 0f;
                    lineY += lineHeight;
                    continue;
                }

                if (prepared.visible)
                {
                    ref readonly var glyph = ref prepared.glyph;
                    float glyphLeft = cursorX + glyph.planeBounds.left * fontSize;
                    float glyphRight = cursorX + glyph.planeBounds.right * fontSize;
                    float glyphTop = lineY + baseline - glyph.planeBounds.top * fontSize;
                    float glyphBottom = lineY + baseline - glyph.planeBounds.bottom * fontSize;

                    if (!hasBounds)
                    {
                        minX = glyphLeft;
                        minY = glyphTop;
                        maxX = glyphRight;
                        maxY = glyphBottom;
                        hasBounds = true;
                    }
                    else
                    {
                        if (glyphLeft < minX) minX = glyphLeft;
                        if (glyphTop < minY) minY = glyphTop;
                        if (glyphRight > maxX) maxX = glyphRight;
                        if (glyphBottom > maxY) maxY = glyphBottom;
                    }
                }

                cursorX += prepared.advance * fontSize;
            }

            var bounds = hasBounds ? new Vector4(minX, minY, maxX - minX, maxY - minY) : default;
            run.StoreMeasuredBounds(fontSize, bounds);
            return bounds;
        }

        /// <summary>Glyph indices map into the negative key space so codepoint and
        /// shaped records share pages, materials and miss tracking without collisions.</summary>
        internal static int EncodeGlyphIndexKey(int glyphIndex)
        {
            return -1 - glyphIndex;
        }

        bool EnsureTextShaper()
        {
            if (_textShaper != null)
                return true;

            if (_textShaperFailed || !NowTextShaper.supported)
                return false;

            var bytes = DynamicFontBytes;

            if (bytes == null)
            {
                _textShaperFailed = true;
                return false;
            }

            _dynamicSourceIsColor ??= NowFontCompiler.IsColorFont(bytes);

            if (_dynamicSourceIsColor.Value || !NowTextShaper.TryCreate(bytes, out _textShaper, out _))
            {
                _textShaperFailed = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shapes one segment (no newlines/tabs) through HarfBuzz, cached per string.
        /// Returns false when shaping is unavailable or when the shaped run contains
        /// .notdef glyphs — those segments use the codepoint path so font fallbacks
        /// keep resolving the characters this font lacks.
        /// </summary>
        internal bool TryGetShapedRun(string segment, out NowTextShaper.ShapedGlyph[] run)
        {
            run = null;

            if (string.IsNullOrEmpty(segment) || !EnsureTextShaper())
                return false;

            _shapeCache ??= new Dictionary<string, ShapedRunCacheEntry>(64);

            if (_shapeCache.TryGetValue(segment, out var entry))
            {
                entry.generation = _shapeCacheGeneration;
                run = entry.run;
                return run != null;
            }

            var scratch = _shapeScratch ??= new List<NowTextShaper.ShapedGlyph>(64);
            scratch.Clear();

            bool usable = _textShaper.TryShape(segment, scratch, out _) && scratch.Count > 0;

            for (int i = 0; usable && i < scratch.Count; ++i)
            {
                if (scratch[i].glyphIndex == 0)
                    usable = false;
            }

            run = usable ? scratch.ToArray() : null;

            if (_shapeCache.Count >= SHAPE_CACHE_LIMIT)
                EvictStaleShapeCacheEntries();

            _shapeCache[segment] = new ShapedRunCacheEntry { run = run, generation = _shapeCacheGeneration };
            scratch.Clear();
            return run != null;
        }

        /// <summary>
        /// Bakes any glyphs of the shaped run that are not yet in a dynamic page.
        /// Returns true when every glyph in the run has a record afterwards.
        /// </summary>
        internal bool EnsureShapedGlyphs(NowTextShaper.ShapedGlyph[] run, float fontSize)
        {
            return EnsureShapedGlyphs(run, fontSize, 0f);
        }

        internal bool EnsureShapedGlyphs(NowTextShaper.ShapedGlyph[] run, float fontSize, float outline)
        {
            return EnsureShapedGlyphs(run, fontSize, outline, visibleOnly: false);
        }

        bool EnsureShapedGlyphs(
            NowTextShaper.ShapedGlyph[] run,
            float fontSize,
            float outline,
            bool visibleOnly)
        {
            if (run == null || run.Length == 0)
                return false;

            int atlasSize = GetDynamicGlyphSize(fontSize);
            int pixelRange = GetDynamicPixelRange(outline, fontSize);
            var missing = _shapedMissingScratch ??= new List<int>(32);
            missing.Clear();

            for (int i = 0; i < run.Length; ++i)
            {
                int glyphIndex = (int)run[i].glyphIndex;

                if (visibleOnly)
                {
                    if (!TryGetShapedGlyph(glyphIndex, fontSize, 0f, out var baseGlyph, out _))
                        return false;

                    if (Mathf.Approximately(baseGlyph.atlasBounds.left, baseGlyph.atlasBounds.right))
                        continue;
                }

                int encoded = EncodeGlyphIndexKey(glyphIndex);
                var key = new DynamicGlyphKey(encoded, atlasSize, pixelRange);

                if (_dynamicMisses != null && _dynamicMisses.Contains(key))
                    return false;

                if (TryGetDynamicCachedGlyph(encoded, atlasSize, pixelRange, out _))
                    continue;

                if (_dynamicCapacityMisses != null && _dynamicCapacityMisses.Contains(key))
                {
                    if (!TryGetDynamicCapacityFallbackGlyph(
                        encoded,
                        atlasSize,
                        pixelRange,
                        out _,
                        out _))
                    {
                        return false;
                    }

                    continue;
                }

                if (!missing.Contains(glyphIndex))
                    missing.Add(glyphIndex);
            }

            if (missing.Count == 0)
                return true;

            if (!TryBakeShapedGlyphs(missing, atlasSize, pixelRange, out bool budgetExceeded))
            {
                if (!budgetExceeded)
                    return false;

                for (int i = 0; i < missing.Count; ++i)
                {
                    int encoded = EncodeGlyphIndexKey(missing[i]);

                    if (!TryGetDynamicCachedGlyph(encoded, atlasSize, pixelRange, out _))
                    {
                        AddDynamicCapacityMiss(new DynamicGlyphKey(
                            encoded,
                            atlasSize,
                            pixelRange));
                    }
                }
            }

            for (int i = 0; i < run.Length; ++i)
            {
                int glyphIndex = (int)run[i].glyphIndex;

                if (visibleOnly)
                {
                    if (!TryGetShapedGlyph(glyphIndex, fontSize, 0f, out var baseGlyph, out _))
                        return false;

                    if (Mathf.Approximately(baseGlyph.atlasBounds.left, baseGlyph.atlasBounds.right))
                        continue;
                }

                int encoded = EncodeGlyphIndexKey(glyphIndex);

                if (!TryGetDynamicCachedGlyph(encoded, atlasSize, pixelRange, out _) &&
                    !TryGetDynamicCapacityFallbackGlyph(
                        encoded,
                        atlasSize,
                        pixelRange,
                        out _,
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryGetPreparedShapedRun(string segment, float fontSize, out PreparedShapedRun run)
        {
            return TryGetPreparedShapedRun(segment, fontSize, 0f, out run);
        }

        internal bool TryGetPreparedShapedRun(string segment, float fontSize, float outline, out PreparedShapedRun run)
        {
            run = null;

            if (string.IsNullOrEmpty(segment))
                return false;

            ResolveGlyphTier(fontSize, outline, out int atlasSize, out int pixelRange, out _);
            var key = new PreparedShapeKey(segment, atlasSize, pixelRange);
            _preparedShapeCache ??= new Dictionary<PreparedShapeKey, PreparedShapedRun>(64);

            if (_preparedShapeCache.TryGetValue(key, out run))
            {
                if (run == null)
                    return false;

                run.generation = _preparedShapeGeneration;
                return true;
            }

            if (!TryGetShapedRun(segment, out var shapedRun))
                return false;

            int basePixelRange = GetDynamicPixelRange(0f, fontSize);
            bool useBaseForInvisibleGlyphs = pixelRange != basePixelRange;

            if (useBaseForInvisibleGlyphs)
            {
                if (!EnsureShapedGlyphs(shapedRun, fontSize, 0f) ||
                    !EnsureShapedGlyphs(shapedRun, fontSize, outline, visibleOnly: true))
                {
                    return false;
                }
            }
            else if (!EnsureShapedGlyphs(shapedRun, fontSize, outline))
            {
                return false;
            }

            var prepared = new PreparedShapedGlyph[shapedRun.Length];
            int animationUnitCount = CountShapedAnimationUnits(shapedRun);

            bool logicalOrderMatchesVisual =
                shapedRun.Length < 2 || shapedRun[0].cluster <= shapedRun[shapedRun.Length - 1].cluster;
            int visualAnimationUnit = -1;
            uint previousCluster = uint.MaxValue;

            for (int i = 0; i < shapedRun.Length; ++i)
            {
                var shaped = shapedRun[i];
                NowFontAtlasInfo.Glyph glyph;
                Material glyphMaterial;

                if (useBaseForInvisibleGlyphs)
                {
                    if (!TryGetShapedGlyph(
                            (int)shaped.glyphIndex,
                            fontSize,
                            0f,
                            out var baseGlyph,
                            out var baseMaterial))
                    {
                        return false;
                    }

                    if (Mathf.Approximately(baseGlyph.atlasBounds.left, baseGlyph.atlasBounds.right))
                    {
                        glyph = baseGlyph;
                        glyphMaterial = baseMaterial;
                    }
                    else if (!TryGetShapedGlyph(
                                 (int)shaped.glyphIndex,
                                 fontSize,
                                 outline,
                                 out glyph,
                                 out glyphMaterial))
                    {
                        return false;
                    }
                }
                else if (!TryGetShapedGlyph(
                             (int)shaped.glyphIndex,
                             fontSize,
                             outline,
                             out glyph,
                             out glyphMaterial))
                {
                    return false;
                }

                if (i == 0 || shaped.cluster != previousCluster)
                    ++visualAnimationUnit;

                previousCluster = shaped.cluster;
                int animationUnit = logicalOrderMatchesVisual
                    ? visualAnimationUnit
                    : animationUnitCount - visualAnimationUnit - 1;
                prepared[i] = new PreparedShapedGlyph(shaped, glyph, glyphMaterial, animationUnit);
            }

            if (_preparedShapeCache.Count >= SHAPE_CACHE_LIMIT)
                EvictStalePreparedShapeEntries();

            run = new PreparedShapedRun(prepared) { generation = _preparedShapeGeneration };
            _preparedShapeCache[key] = run;
            return true;
        }

        /// <summary>
        /// HarfBuzz's default monotone-grapheme cluster level keeps equal cluster
        /// values adjacent in visual output, including RTL runs. Count those groups
        /// without baking atlas glyphs so wrapped animation can precompute exact
        /// sequence offsets without populating offscreen font pages.
        /// </summary>
        internal static int CountShapedAnimationUnits(NowTextShaper.ShapedGlyph[] shapedRun)
        {
            if (shapedRun == null || shapedRun.Length == 0)
                return 0;

            int count = 1;

            for (int i = 1; i < shapedRun.Length; ++i)
            {
                if (shapedRun[i].cluster != shapedRun[i - 1].cluster)
                    ++count;
            }

            return count;
        }

        bool TryBakeShapedGlyphs(
            List<int> glyphIndices,
            int atlasSize,
            int pixelRange,
            out bool budgetExceeded)
        {
            budgetExceeded = false;
            const int SESSION_ADD_CHUNK = 64;

            if (s_dynamicSessionUnsupported)
                return false;

            var fontData = DynamicFontBytes;

            if (fontData == null)
                return false;

            var state = GetDynamicSessionState(atlasSize, pixelRange);

            if (state.failed)
                return false;

            if (state.page != null && !IsDynamicPageValid(state.page))
                ResetDynamicSession(state);

            using var profile = NowProfiler.FontBake.Auto();
            var results = _dynamicSessionGlyphScratch ??= new List<NowFontAtlasInfo.Glyph>();
            results.Clear();

            int indexCount = glyphIndices.Count;

            if (_shapedBakeIndexScratch == null || _shapedBakeIndexScratch.Length < indexCount)
                _shapedBakeIndexScratch = new int[Mathf.NextPowerOfTwo(Mathf.Max(indexCount, 16))];

            var indices = _shapedBakeIndexScratch;

            for (int i = 0; i < indexCount; ++i)
                indices[i] = glyphIndices[i];

            var chunkIndices = _dynamicSessionChunkScratch ??= new int[SESSION_ADD_CHUNK];
            int offset = 0;
            int chunkLimit = SESSION_ADD_CHUNK;
            bool allBaked = true;

            while (offset < indexCount)
            {
                if (!TryEnsureDynamicSession(
                    fontData,
                    atlasSize,
                    pixelRange,
                    state,
                    out budgetExceeded))
                {
                    return false;
                }

                if (!state.session.supportsGlyphIndexBaking)
                    return false;

                int chunk = Mathf.Min(chunkLimit, indexCount - offset);
                Array.Copy(indices, offset, chunkIndices, 0, chunk);
                int resultsBefore = results.Count;
                var status = state.session.TryAddGlyphsByIndex(chunkIndices, chunk, results, out _);

                if (status == NowFontCompiler.DynamicSession.AddResult.Ok)
                {
                    for (int i = resultsBefore; i < results.Count; ++i)
                    {
                        var record = results[i];
                        record.unicode = EncodeGlyphIndexKey(record.unicode);
                        results[i] = record;
                    }

                    offset += chunk;
                    chunkLimit = SESSION_ADD_CHUNK;
                    continue;
                }

                if (status == NowFontCompiler.DynamicSession.AddResult.AtlasFull)
                {
                    if (results.Count > 0)
                    {
                        bool committed = TryCommitSessionGlyphs(
                            state,
                            results,
                            atlasSize,
                            pixelRange,
                            out bool commitBudgetExceeded);
                        results.Clear();

                        if (!committed)
                        {
                            ResetDynamicSession(state);

                            if (commitBudgetExceeded)
                                budgetExceeded = true;
                            else
                                state.failed = true;

                            return false;
                        }
                    }

                    if (state.page == null)
                    {
                        if (chunk <= 1)
                        {
                            if (TryGrowEmptyDynamicSession(state))
                                continue;

                            AddDynamicMiss(new DynamicGlyphKey(
                                EncodeGlyphIndexKey(indices[offset]),
                                atlasSize,
                                pixelRange));
                            ++offset;
                            chunkLimit = SESSION_ADD_CHUNK;
                            allBaked = false;
                            continue;
                        }

                        chunkLimit = Mathf.Max(1, chunk / 2);
                        continue;
                    }

                    ResetDynamicSession(state);
                    continue;
                }

                return CommitAndFailDynamicSession(
                    state,
                    results,
                    atlasSize,
                    pixelRange,
                    out budgetExceeded);
            }

            if (results.Count > 0 &&
                !TryCommitSessionGlyphs(
                    state,
                    results,
                    atlasSize,
                    pixelRange,
                    out bool finalCommitBudgetExceeded))
            {
                results.Clear();
                ResetDynamicSession(state);
                budgetExceeded = finalCommitBudgetExceeded;

                if (!budgetExceeded)
                    state.failed = true;

                return false;
            }

            results.Clear();
            return allBaked;
        }

        /// <summary>Resolves a baked shaped glyph record and its page material.</summary>
        internal bool TryGetShapedGlyph(int glyphIndex, float fontSize, out NowFontAtlasInfo.Glyph glyph, out Material glyphMaterial)
        {
            return TryGetShapedGlyph(glyphIndex, fontSize, 0f, out glyph, out glyphMaterial);
        }

        internal bool TryGetShapedGlyph(
            int glyphIndex,
            float fontSize,
            float outline,
            out NowFontAtlasInfo.Glyph glyph,
            out Material glyphMaterial)
        {
            int encoded = EncodeGlyphIndexKey(glyphIndex);
            int atlasSize = GetDynamicGlyphSize(fontSize);
            int pixelRange = GetDynamicPixelRange(outline, fontSize);

            if (TryGetDynamicCachedGlyph(encoded, atlasSize, pixelRange, out glyph, out var page))
            {
                glyphMaterial = page.font.material;
                return glyphMaterial != null;
            }

            if (TryGetDynamicCapacityFallbackGlyph(
                encoded,
                atlasSize,
                pixelRange,
                out glyph,
                out page))
            {
                glyphMaterial = page.font.material;
                return glyphMaterial != null;
            }

            glyph = default;
            glyphMaterial = null;
            return false;
        }

        public int GetMaterialId(int unicode)
        {
            return GetMaterialId(unicode, dynamicAtlasSize);
        }

        public int GetMaterialId(int unicode, float fontSize)
        {
            int pixelRange = GetDynamicPixelRange(0f, fontSize);

            if (_dynamicGlyphPages != null &&
                _dynamicGlyphPages.TryGetValue(
                    new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize), pixelRange),
                    out var page) &&
                page != null)
            {
                return page.materialId;
            }

            return materialId;
        }

        public void SetMaterialId(int unicode, int value)
        {
            SetMaterialId(unicode, dynamicAtlasSize, value);
        }

        public void SetMaterialId(int unicode, float fontSize, int value)
        {
            int pixelRange = GetDynamicPixelRange(0f, fontSize);

            if (_dynamicGlyphPages != null &&
                _dynamicGlyphPages.TryGetValue(
                    new DynamicGlyphKey(unicode, GetDynamicGlyphSize(fontSize), pixelRange),
                    out var page) &&
                page != null)
            {
                page.materialId = value;
                return;
            }

            materialId = value;
        }

        public Material GetMaterial(int unicode)
        {
            return GetMaterial(unicode, dynamicAtlasSize);
        }

        public Material GetMaterial(int unicode, float fontSize)
        {
            return GetMaterial(unicode, fontSize, 0f);
        }

        internal Material GetMaterial(int unicode, float fontSize, float outline)
        {
            int atlasSize = GetDynamicGlyphSize(fontSize);
            int pixelRange = GetDynamicPixelRange(outline, fontSize);

            if (_dynamicGlyphPages != null &&
                _dynamicGlyphPages.TryGetValue(
                    new DynamicGlyphKey(unicode, atlasSize, pixelRange),
                    out var page) &&
                page != null &&
                page.font != null)
            {
                return page.font.material;
            }

            if (TryGetDynamicCapacityFallbackGlyph(
                unicode,
                atlasSize,
                pixelRange,
                out _,
                out page))
            {
                return page.font.material;
            }

            return material;
        }

        internal bool IsColorGlyph(int unicode, float fontSize, float outline)
        {
            int atlasSize = GetDynamicGlyphSize(fontSize);
            int pixelRange = GetDynamicPixelRange(outline, fontSize);

            if (_dynamicGlyphPages != null &&
                _dynamicGlyphPages.TryGetValue(
                    new DynamicGlyphKey(unicode, atlasSize, pixelRange),
                    out var page) &&
                IsDynamicPageValid(page))
            {
                return page.font.isColor;
            }

            if (TryGetDynamicCapacityFallbackGlyph(
                unicode,
                atlasSize,
                pixelRange,
                out _,
                out page))
            {
                return page.font.isColor;
            }

            return isColor;
        }

        /// <summary>Distance-field range in local units; the text shaders convert it to
        /// screen pixels per-fragment (and floor it there) so canvas/transform scale does
        /// not soften or alias the glyph edges.</summary>
        public float GetScreenPixelRange(int unicode, float fontSize)
        {
            return GetScreenPixelRange(unicode, fontSize, 0f);
        }

        internal float GetScreenPixelRange(int unicode, float fontSize, float outline)
        {
            ResolveGlyphTier(fontSize, outline, out _, out int pixelRange, out _);
            return GetScreenPixelRangeForPixelRange(unicode, fontSize, pixelRange);
        }

        internal float GetScreenPixelRangeForPixelRange(int unicode, float fontSize, int pixelRange)
        {
            var fontAtlas = atlasInfo.atlas;
            int atlasSize = GetDynamicGlyphSize(fontSize);
            pixelRange = Mathf.Max(1, pixelRange);

            if (_dynamicGlyphPages != null &&
                _dynamicGlyphPages.TryGetValue(
                    new DynamicGlyphKey(unicode, atlasSize, pixelRange),
                    out var page) &&
                IsDynamicPageValid(page))
            {
                fontAtlas = page.font.atlasInfo.atlas;
            }
            else if (TryGetDynamicCapacityFallbackGlyph(
                unicode,
                atlasSize,
                pixelRange,
                out _,
                out page))
            {
                fontAtlas = page.font.atlasInfo.atlas;
            }

            return ScreenPixelRange(fontSize, fontAtlas);
        }

        public override Vector2 MeasureText(string value, float fontSize, int tabSpaces = 4)
        {
            return base.MeasureText(value, fontSize, NowFontStyle.Regular, tabSpaces);
        }

        public override Vector4 MeasureTextBounds(string value, float fontSize, int tabSpaces = 4)
        {
            return base.MeasureTextBounds(value, fontSize, NowFontStyle.Regular, tabSpaces);
        }
    }
}
