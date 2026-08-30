using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.Sdf
{
    public enum NowSdfOperation
    {
        Union = 0,
        Subtract = 1,
        Intersect = 2,
        SmoothUnion = 3,
        SmoothSubtract = 4,
        SmoothIntersect = 5
    }

    enum NowSdfShapeType
    {
        Circle = 0,
        Box = 1,
        RoundedBox = 2,
        Ellipse = 3,
        Capsule = 4,
        Glyph = 5,
        Arc = 6,
        Pie = 7,
        ChamferedBox = 8,
        Triangle = 9
    }

    enum NowSdfLayerKind
    {
        Graph = 0,
        Morph = 1
    }

    struct NowSdfNode
    {
        public NowSdfShapeType type;
        public NowSdfOperation operation;
        public float smoothing;
        public Vector4 data1;
        public Vector4 data2;
        public Vector4 color;
        public Vector4 uv;
        public Vector2 rotation;
        public bool useTexture;
        public NowRect bounds;
    }

    struct NowSdfGlyphSource
    {
        public int nodeIndex;
        public int codepoint;
        public NowFontAsset font;
        public NowFont owner;
        public int ownerVersion;
        public float fontSize;
        public NowFontStyle fontStyle;
        public float x;
        public float y;
        public float baseline;
        public Vector2 rotation;
        public Vector2 pivot;
    }

    struct NowSdfResolvedGlyph
    {
        public NowFont font;
        public NowFontAtlasInfo.Glyph glyph;
        public Material material;
    }

    struct NowSdfLayer
    {
        public NowSdfLayerKind kind;
        public NowSdfOperation operation;
        public float smoothing;
        public NowSdfGraph graph;
        public NowSdfGraph targetGraph;
        public float morph;
    }

    /// <summary>
    /// Reusable signed-distance-field primitive graph. Graphs are pure shape
    /// content; compose them at draw time with <see cref="NowSdfBuilder.Graph"/>
    /// and <see cref="NowSdfBuilder.Morph"/>.
    /// </summary>
    public sealed class NowSdfGraph
    {
        const float FullTurnRadians = Mathf.PI * 2f;
        const double TriangleRelativeAreaTolerance = 1d / 131072d;
        const double FloatMinNormal = 1.1754943508222875E-38d;
        const double FloatUnitRoundoff = 1d / 16777216d;
        const double RotationBoundsGamma32 =
            (32d * FloatUnitRoundoff) / (1d - 32d * FloatUnitRoundoff);

        readonly List<NowSdfNode> _nodes = new List<NowSdfNode>(8);
        readonly List<NowSdfGlyphSource> _glyphSources = new List<NowSdfGlyphSource>(8);
        readonly List<NowSdfResolvedGlyph> _resolvedGlyphs = new List<NowSdfResolvedGlyph>(8);
        readonly List<float> _rotationStack = new List<float>(4);

        static readonly int _textSdfEncodingProp = Shader.PropertyToID("_NowUITextSdfEncoding");

        Vector4 _color = Vector4.one;
        Vector4 _textureUv = new Vector4(0f, 0f, 1f, 1f);
        Texture _texture;
        bool _textureFromGlyph;
        bool _useTexture;
        NowSdfOperation _operation = NowSdfOperation.Union;
        float _smoothing;
        float _nextRotationDegrees;
        int _textPixelRange;
        int _failedTextPixelRange;
        int _failedTextFontVersion = -1;
        int _contentRevision;
        int _requiredMaterialAbi = 1;
        NowRect _bounds;
        bool _hasBounds;

        internal IReadOnlyList<NowSdfNode> nodes => _nodes;

        internal Texture texture => _texture;

        internal bool hasNodes => _nodes.Count > 0;

        internal bool hasText => _glyphSources.Count > 0;

        internal int contentRevision => _contentRevision;

        internal int requiredMaterialAbi => _requiredMaterialAbi;

        public Vector2 measureSize => _hasBounds ? new Vector2(_bounds.xMax, _bounds.yMax) : Vector2.zero;

        public NowSdfGraph Clear()
        {
            AdvanceContentRevision();
            _nodes.Clear();
            _glyphSources.Clear();
            _resolvedGlyphs.Clear();

            if (_textureFromGlyph)
            {
                _texture = null;
                _textureFromGlyph = false;
            }

            _operation = NowSdfOperation.Union;
            _smoothing = 0f;
            _nextRotationDegrees = 0f;
            _textPixelRange = 0;
            _failedTextPixelRange = 0;
            _failedTextFontVersion = -1;
            _rotationStack.Clear();
            _requiredMaterialAbi = 1;
            _bounds = default;
            _hasBounds = false;
            return this;
        }

        internal NowSdfGraph ResetForReuse()
        {
            Clear();
            _color = Vector4.one;
            _textureUv = new Vector4(0f, 0f, 1f, 1f);
            _texture = null;
            _textureFromGlyph = false;
            _useTexture = false;
            return this;
        }

        public NowSdfGraph SetColor(Color color)
        {
            _color = color;
            return this;
        }

        public NowSdfGraph SetColor(Vector4 color)
        {
            _color = color;
            return this;
        }

        public NowSdfGraph UseColor()
        {
            _useTexture = false;
            return this;
        }

        public NowSdfGraph SetTexture(Texture texture)
        {
            AdvanceContentRevision();
            _texture = texture;
            _textureFromGlyph = false;
            _useTexture = texture != null;
            return this;
        }

        public NowSdfGraph SetTexture(Texture texture, Vector4 uvRect)
        {
            SetTexture(texture);
            SetTextureUV(uvRect);
            return this;
        }

        public NowSdfGraph UseTexture()
        {
            _useTexture = _texture != null;
            return this;
        }

        public NowSdfGraph UseTexture(Vector4 uvRect)
        {
            SetTextureUV(uvRect);
            return UseTexture();
        }

        public NowSdfGraph SetTextureUV(Vector4 uvRect)
        {
            if (Mathf.Approximately(uvRect.z, 0f) && Mathf.Approximately(uvRect.w, 0f))
                uvRect = new Vector4(0f, 0f, 1f, 1f);

            _textureUv = uvRect;
            return this;
        }

        public NowSdfGraph SetOperation(NowSdfOperation operation, float smoothing = 0f)
        {
            _operation = operation;
            _smoothing = Mathf.Max(0f, smoothing);
            return this;
        }

        public NowSdfGraph Union(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothUnion : NowSdfOperation.Union, smoothing);
        }

        public NowSdfGraph Subtract(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothSubtract : NowSdfOperation.Subtract, smoothing);
        }

        public NowSdfGraph Intersect(float smoothing = 0f)
        {
            return SetOperation(smoothing > 0f ? NowSdfOperation.SmoothIntersect : NowSdfOperation.Intersect, smoothing);
        }

        public NowSdfGraph SmoothUnion(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothUnion, smoothing);
        }

        public NowSdfGraph SmoothSubtract(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothSubtract, smoothing);
        }

        public NowSdfGraph SmoothIntersect(float smoothing)
        {
            return SetOperation(NowSdfOperation.SmoothIntersect, smoothing);
        }

        /// <summary>
        /// Rotates the next analytic primitive around its natural center, or
        /// the next text run around the center of its emitted glyph bounds,
        /// relative to any pushed rotation. Angles are degrees and positive
        /// values rotate clockwise in UI space.
        /// </summary>
        public NowSdfGraph RotateNext(float angleDegrees)
        {
            ValidateFinite(angleDegrees, nameof(angleDegrees));
            _nextRotationDegrees = NormalizeRotationDegrees(angleDegrees);
            return this;
        }

        /// <summary>
        /// Pushes a persistent relative rotation for analytic primitives and
        /// text runs. Nested pushes compose, and <see cref="PopRotation"/>
        /// restores the parent rotation.
        /// </summary>
        public NowSdfGraph PushRotation(float angleDegrees)
        {
            ValidateFinite(angleDegrees, nameof(angleDegrees));
            float parent = _rotationStack.Count > 0
                ? _rotationStack[_rotationStack.Count - 1]
                : 0f;
            _rotationStack.Add(NormalizeRotationDegrees(parent + angleDegrees));
            return this;
        }

        /// <summary>Restores the rotation active before the matching push.</summary>
        public NowSdfGraph PopRotation()
        {
            if (_rotationStack.Count == 0)
                throw new InvalidOperationException("SDF PopRotation requires a matching PushRotation.");

            _rotationStack.RemoveAt(_rotationStack.Count - 1);
            return this;
        }

        internal void ThrowIfRotationScopesOpen(string operation)
        {
            if (_rotationStack.Count == 0)
                return;

            throw new InvalidOperationException(
                $"SDF {operation} requires every PushRotation to have a matching PopRotation.");
        }

        public NowSdfGraph Circle(Vector2 center, float radius)
        {
            radius = Mathf.Max(0f, radius);
            Add(
                NowSdfShapeType.Circle,
                new Vector4(center.x, center.y, radius, 0f),
                default,
                new NowRect(center.x - radius, center.y - radius, radius * 2f, radius * 2f));
            return this;
        }

        public NowSdfGraph Circle(Vector2 center, float radius, Color color)
        {
            SetColor(color);
            UseColor();
            return Circle(center, radius);
        }

        public NowSdfGraph Box(NowRect rect)
        {
            Add(NowSdfShapeType.Box, RectData(rect), default, rect);
            return this;
        }

        public NowSdfGraph Box(NowRect rect, Color color)
        {
            SetColor(color);
            UseColor();
            return Box(rect);
        }

        public NowSdfGraph Rectangle(NowRect rect)
        {
            return Box(rect);
        }

        public NowSdfGraph RoundedBox(NowRect rect, float radius)
        {
            return RoundedBox(rect, new Vector4(radius, radius, radius, radius));
        }

        public NowSdfGraph RoundedBox(NowRect rect, Vector4 radius)
        {
            radius.x = Mathf.Max(0f, radius.x);
            radius.y = Mathf.Max(0f, radius.y);
            radius.z = Mathf.Max(0f, radius.z);
            radius.w = Mathf.Max(0f, radius.w);
            Add(NowSdfShapeType.RoundedBox, RectData(rect), radius, rect);
            return this;
        }

        public NowSdfGraph RoundedBox(NowRect rect, float radius, Color color)
        {
            SetColor(color);
            UseColor();
            return RoundedBox(rect, radius);
        }

        public NowSdfGraph RoundRect(NowRect rect, float radius)
        {
            return RoundedBox(rect, radius);
        }

        /// <summary>
        /// Adds a box whose corners are clipped by straight 45-degree edges.
        /// </summary>
        /// <param name="chamfer">
        /// Distance removed along each adjoining edge. Negative values clamp to zero and
        /// values larger than half the shorter side clamp to that half-side.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the rectangle or chamfer is not finite or cannot produce
        /// representable bounds.
        /// </exception>
        public NowSdfGraph ChamferedBox(NowRect rect, float chamfer)
        {
            ValidateFiniteRect(rect, nameof(rect));
            ValidateFinite(chamfer, nameof(chamfer));

            if (rect.isEmpty)
                return SkipPrimitive();

            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            chamfer = Mathf.Clamp(chamfer, 0f, Mathf.Min(halfWidth, halfHeight));
            Vector4 data = RectData(rect);
            Add(
                NowSdfShapeType.ChamferedBox,
                data,
                new Vector4(chamfer, 0f, 0f, 0f),
                rect);
            return this;
        }

        public NowSdfGraph ChamferedBox(NowRect rect, float chamfer, Color color)
        {
            SetColor(color);
            UseColor();
            return ChamferedBox(rect, chamfer);
        }

        public NowSdfGraph Ellipse(NowRect rect)
        {
            Add(NowSdfShapeType.Ellipse, RectData(rect), default, rect);
            return this;
        }

        public NowSdfGraph Ellipse(NowRect rect, Color color)
        {
            SetColor(color);
            UseColor();
            return Ellipse(rect);
        }

        public NowSdfGraph Capsule(Vector2 from, Vector2 to, float radius)
        {
            radius = Mathf.Max(0f, radius);
            var min = Vector2.Min(from, to) - new Vector2(radius, radius);
            var max = Vector2.Max(from, to) + new Vector2(radius, radius);
            Add(
                NowSdfShapeType.Capsule,
                new Vector4(from.x, from.y, to.x, to.y),
                new Vector4(radius, 0f, 0f, 0f),
                new NowRect(min.x, min.y, max.x - min.x, max.y - min.y));
            return this;
        }

        /// <summary>
        /// Adds a round-capped line segment. <paramref name="width"/> is the full
        /// stroke width, matching <c>Now.Line(...).SetWidth(...)</c>.
        /// </summary>
        public NowSdfGraph Line(Vector2 from, Vector2 to, float width)
        {
            ValidateFinite(from, nameof(from));
            ValidateFinite(to, nameof(to));
            ValidateFinite(width, nameof(width));
            float radius = Mathf.Max(0f, width) * 0.5f;
            Vector2 extent = new Vector2(radius, radius);
            ValidateBounds(Vector2.Min(from, to) - extent, Vector2.Max(from, to) + extent, nameof(width));
            return Capsule(from, to, radius);
        }

        /// <summary>Adds a filled triangle. Vertex winding does not affect the field.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a vertex is not finite or the bounds cannot be represented.
        /// </exception>
        public NowSdfGraph Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            ValidateFinite(a, nameof(a));
            ValidateFinite(b, nameof(b));
            ValidateFinite(c, nameof(c));

            Vector2 min = Vector2.Min(a, Vector2.Min(b, c));
            Vector2 max = Vector2.Max(a, Vector2.Max(b, c));
            ValidateBounds(min, max, nameof(a));
            double abx = (double)b.x - a.x;
            double aby = (double)b.y - a.y;
            double acx = (double)c.x - a.x;
            double acy = (double)c.y - a.y;
            double orientation = abx * acy - aby * acx;
            double bcx = (double)c.x - b.x;
            double bcy = (double)c.y - b.y;
            double scale = Math.Max(
                Math.Max(Math.Abs(abx), Math.Abs(aby)),
                Math.Max(Math.Abs(acx), Math.Abs(acy)));
            scale = Math.Max(scale, Math.Max(Math.Abs(bcx), Math.Abs(bcy)));
            // At float shader precision, triangles thinner than 64 ULPs relative
            // to their largest component span have no stable filled interior.
            // Treat them as an unsigned edge field, like an exact collapse.
            double degeneracyThreshold = scale * scale * TriangleRelativeAreaTolerance;
            float orientationSign = Math.Abs(orientation) <= degeneracyThreshold
                ? 0f
                : orientation > 0d ? 1f : -1f;
            float packedScale = scale > 0d ? (float)Math.Max(scale, FloatMinNormal) : 1f;
            var packedB = new Vector2(
                (float)(abx / packedScale),
                (float)(aby / packedScale));
            var packedC = new Vector2(
                (float)(acx / packedScale),
                (float)(acy / packedScale));
            Vector2 reconstructedB = a + packedB * packedScale;
            Vector2 reconstructedC = a + packedC * packedScale;
            min = Vector2.Min(min, Vector2.Min(reconstructedB, reconstructedC));
            max = Vector2.Max(max, Vector2.Max(reconstructedB, reconstructedC));
            ValidateBounds(min, max, nameof(a));
            var bounds = new NowRect(min.x, min.y, max.x - min.x, max.y - min.y);

            // NowRect derives xMax/yMax by adding the stored float size back to
            // its origin. Opposite-sign or widely separated coordinates can make
            // that addition round one ULP inward even though min/max are finite.
            if (bounds.xMax < max.x)
                bounds.width = NextFloat(bounds.width);
            if (bounds.yMax < max.y)
                bounds.height = NextFloat(bounds.height);

            ValidateFiniteRect(bounds, nameof(a));
            Add(
                NowSdfShapeType.Triangle,
                new Vector4(a.x, a.y, packedB.x, packedB.y),
                new Vector4(packedC.x, packedC.y, orientationSign, packedScale),
                bounds);
            return this;
        }

        public NowSdfGraph Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            SetColor(color);
            UseColor();
            return Triangle(a, b, c);
        }

        public NowSdfGraph Capsule(NowRect rect)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 from;
            Vector2 to;

            if (rect.width >= rect.height)
            {
                from = new Vector2(rect.x + radius, rect.y + rect.height * 0.5f);
                to = new Vector2(rect.xMax - radius, rect.y + rect.height * 0.5f);
            }
            else
            {
                from = new Vector2(rect.x + rect.width * 0.5f, rect.y + radius);
                to = new Vector2(rect.x + rect.width * 0.5f, rect.yMax - radius);
            }

            return Capsule(from, to, radius);
        }

        /// <summary>
        /// Adds a circular band over a signed angular sweep. Angles are radians;
        /// zero points right and positive sweeps turn clockwise in UI space.
        /// </summary>
        /// <param name="thickness">Half-width of the band around <paramref name="radius"/>.</param>
        /// <param name="from">Start angle in radians.</param>
        /// <param name="sweep">Signed sweep in radians, clamped to one full turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when an argument is not finite or the resulting bounds cannot be represented.
        /// </exception>
        public NowSdfGraph Arc(Vector2 center, float radius, float thickness, float from, float sweep)
        {
            ValidateFinite(center, nameof(center));
            ValidateFinite(radius, nameof(radius));
            ValidateFinite(thickness, nameof(thickness));
            ValidateFinite(from, nameof(from));
            ValidateFinite(sweep, nameof(sweep));

            radius = Mathf.Max(0f, radius);
            thickness = Mathf.Max(0f, thickness);
            float outer = radius + thickness;
            ValidateArcBounds(center, radius, thickness, outer);

            sweep = Mathf.Clamp(sweep, -FullTurnRadians, FullTurnRadians);
            if (sweep == 0f)
                return SkipPrimitive();

            Add(
                NowSdfShapeType.Arc,
                new Vector4(center.x, center.y, radius, thickness),
                RadialData(from, sweep),
                new NowRect(center.x - outer, center.y - outer, outer * 2f, outer * 2f));
            return this;
        }

        /// <summary>
        /// Adds a filled circular sector over a signed angular sweep. Angles are
        /// radians; zero points right and positive sweeps turn clockwise in UI space.
        /// </summary>
        /// <param name="from">Start angle in radians.</param>
        /// <param name="sweep">Signed sweep in radians, clamped to one full turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when an argument is not finite or the resulting bounds cannot be represented.
        /// </exception>
        public NowSdfGraph Pie(Vector2 center, float radius, float from, float sweep)
        {
            ValidateFinite(center, nameof(center));
            ValidateFinite(radius, nameof(radius));
            ValidateFinite(from, nameof(from));
            ValidateFinite(sweep, nameof(sweep));

            radius = Mathf.Max(0f, radius);
            ValidatePieBounds(center, radius);

            sweep = Mathf.Clamp(sweep, -FullTurnRadians, FullTurnRadians);
            if (sweep == 0f)
                return SkipPrimitive();

            Add(
                NowSdfShapeType.Pie,
                new Vector4(center.x, center.y, radius, 0f),
                RadialData(from, sweep),
                new NowRect(center.x - radius, center.y - radius, radius * 2f, radius * 2f));
            return this;
        }

        public NowSdfGraph Text(Vector2 position, string value, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            return Text(position, value, Now.font, fontSize, fontStyle, tabSpaces);
        }

        public NowSdfGraph Text(Vector2 position, string value, NowFontAsset font, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            AddText(position, value, font != null ? font : Now.font, fontSize, fontStyle, tabSpaces);
            return this;
        }

        public NowSdfGraph Text(NowRect rect, string value, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            return Text(rect.position, value, Now.font, fontSize, fontStyle, tabSpaces);
        }

        public NowSdfGraph Text(NowRect rect, string value, NowFontAsset font, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            return Text(rect.position, value, font, fontSize, fontStyle, tabSpaces);
        }

        internal void CopyStyleFrom(NowSdfGraph source)
        {
            _color = source._color;
            _textureUv = source._textureUv;
            _texture = source._textureFromGlyph ? null : source._texture;
            _textureFromGlyph = false;
            _useTexture = source._useTexture;
        }

        internal void CopyFrom(NowSdfGraph source)
        {
            _nodes.Clear();
            _nodes.AddRange(source._nodes);
            _glyphSources.Clear();
            _glyphSources.AddRange(source._glyphSources);
            _resolvedGlyphs.Clear();
            _rotationStack.Clear();
            _rotationStack.AddRange(source._rotationStack);
            _color = source._color;
            _textureUv = source._textureUv;
            _texture = source._texture;
            _textureFromGlyph = source._textureFromGlyph;
            _useTexture = source._useTexture;
            _operation = source._operation;
            _smoothing = source._smoothing;
            _nextRotationDegrees = source._nextRotationDegrees;
            _textPixelRange = source._textPixelRange;
            _failedTextPixelRange = source._failedTextPixelRange;
            _failedTextFontVersion = source._failedTextFontVersion;
            _contentRevision = source._contentRevision;
            _requiredMaterialAbi = source._requiredMaterialAbi;
            _bounds = source._bounds;
            _hasBounds = source._hasBounds;
        }

        void Add(NowSdfShapeType type, Vector4 data1, Vector4 data2, NowRect bounds)
        {
            Add(type, data1, data2, bounds, _textureUv, _useTexture, _operation, _smoothing, true);
        }

        void Add(
            NowSdfShapeType type,
            Vector4 data1,
            Vector4 data2,
            NowRect bounds,
            Vector4 uv,
            bool useTexture,
            NowSdfOperation operation,
            float smoothing,
            bool resetPendingModifiers)
        {
            Vector2 rotation = EffectiveRotation();
            if (rotation != Vector2.zero)
                bounds = RotatedShapeBounds(type, data1, data2, bounds, rotation, nameof(bounds));

            AppendNode(type, data1, data2, bounds, uv, useTexture, operation, smoothing, rotation, true);

            if (resetPendingModifiers)
            {
                _operation = NowSdfOperation.Union;
                _smoothing = 0f;
                _nextRotationDegrees = 0f;
            }
        }

        void AddText(
            Vector2 position,
            string value,
            NowFontAsset font,
            float fontSize,
            NowFontStyle fontStyle,
            int tabSpaces)
        {
            if (font == null || string.IsNullOrEmpty(value) || fontSize <= 0f)
            {
                SkipPrimitive();
                return;
            }

            int firstGlyph = _nodes.Count;
            int firstGlyphSource = _glyphSources.Count;
            int previousRequiredMaterialAbi = _requiredMaterialAbi;
            Texture previousTexture = _texture;
            bool previousTextureFromGlyph = _textureFromGlyph;
            int previousTextPixelRange = _textPixelRange;
            int previousFailedTextPixelRange = _failedTextPixelRange;
            int previousFailedTextFontVersion = _failedTextFontVersion;
            int previousContentRevision = _contentRevision;
            NowRect previousBounds = _bounds;
            bool previouslyHadBounds = _hasBounds;

            try
            {
                float lineHeight = font.GetLineHeight(fontStyle) * fontSize;
                float baseline = font.GetAscender(fontStyle) * fontSize;
                float left = position.x;
                float x = position.x;
                float y = position.y;
                int spaces = Mathf.Max(1, tabSpaces);
                var glyphOperation = _nodes.Count == 0 ? NowSdfOperation.Union : _operation;
                float glyphSmoothing = _nodes.Count == 0 ? 0f : _smoothing;
                Vector2 textRotation = EffectiveRotation();
                double textMinX = double.PositiveInfinity;
                double textMinY = double.PositiveInfinity;
                double textMaxX = double.NegativeInfinity;
                double textMaxY = double.NegativeInfinity;

                for (int i = 0; i < value.Length; ++i)
                {
                    int codepoint = NowFont.ReadCodepoint(value, ref i);

                    if (codepoint == '\n')
                    {
                        x = left;
                        y += lineHeight;
                        continue;
                    }

                    if (codepoint == '\t')
                    {
                        if (font.TryResolveGlyph(' ', fontSize, fontStyle, out _, out var space, out _))
                            x += space.advance * fontSize * spaces;

                        continue;
                    }

                    if (!font.TryResolveGlyph(
                        codepoint,
                        fontSize,
                        fontStyle,
                        out var resolvedFont,
                        out var glyph,
                        out var material))
                    {
                        continue;
                    }

                    if (resolvedFont != null &&
                        !resolvedFont.isColor &&
                        !Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right) &&
                        material != null &&
                        material.mainTexture != null &&
                        TryBindTexture(material.mainTexture))
                    {
                        var rect = GlyphRect(x, y, baseline, fontSize, glyph);
                        var uv = new Vector4(
                            glyph.atlasBounds.left,
                            glyph.atlasBounds.bottom,
                            glyph.atlasBounds.right - glyph.atlasBounds.left,
                            glyph.atlasBounds.top - glyph.atlasBounds.bottom);
                        float range = resolvedFont.GetScreenPixelRange(codepoint, fontSize);
                        float encoding = GetSdfEncoding(material);
                        int nodeIndex = _nodes.Count;
                        AppendNode(
                            NowSdfShapeType.Glyph,
                            RectData(rect),
                            new Vector4(
                                range,
                                encoding,
                                GetSdfDistanceCodeStep(range, encoding),
                                0f),
                            rect,
                            uv,
                            false,
                            glyphOperation,
                            glyphSmoothing,
                            textRotation,
                            textRotation == Vector2.zero);
                        _glyphSources.Add(new NowSdfGlyphSource
                        {
                            nodeIndex = nodeIndex,
                            codepoint = codepoint,
                            font = font,
                            owner = resolvedFont,
                            ownerVersion = resolvedFont.layoutDataVersion,
                            fontSize = fontSize,
                            fontStyle = fontStyle,
                            x = x,
                            y = y,
                            baseline = baseline,
                            rotation = textRotation
                        });
                        _textPixelRange = Mathf.Max(
                            _textPixelRange,
                            resolvedFont.GetDynamicPixelRange(0f, fontSize));

                        var node = _nodes[_nodes.Count - 1];
                        double halfWidth = Math.Abs((double)node.data1.z) * 0.5d;
                        double halfHeight = Math.Abs((double)node.data1.w) * 0.5d;
                        textMinX = Math.Min(textMinX, (double)node.data1.x - halfWidth);
                        textMinY = Math.Min(textMinY, (double)node.data1.y - halfHeight);
                        textMaxX = Math.Max(textMaxX, (double)node.data1.x + halfWidth);
                        textMaxY = Math.Max(textMaxY, (double)node.data1.y + halfHeight);
                    }

                    x += glyph.advance * fontSize;
                }

                if (textRotation != Vector2.zero && _nodes.Count > firstGlyph)
                {
                    var textPivot = new Vector2(
                        (float)(textMinX * 0.5d + textMaxX * 0.5d),
                        (float)(textMinY * 0.5d + textMaxY * 0.5d));
                    ValidateFinite(textPivot, nameof(position));

                    _bounds = previousBounds;
                    _hasBounds = previouslyHadBounds;

                    for (int i = firstGlyph; i < _nodes.Count; ++i)
                    {
                        var node = _nodes[i];
                        var glyphCenter = new Vector2(node.data1.x, node.data1.y);
                        Vector2 transformedCenter = RotatePointAroundPivot(
                            glyphCenter,
                            textPivot,
                            textRotation,
                            nameof(position));
                        node.data1.x = transformedCenter.x;
                        node.data1.y = transformedCenter.y;
                        var transformedRect = new NowRect(
                            (float)((double)transformedCenter.x - (double)node.data1.z * 0.5d),
                            (float)((double)transformedCenter.y - (double)node.data1.w * 0.5d),
                            node.data1.z,
                            node.data1.w);
                        ValidateFiniteRect(transformedRect, nameof(position));
                        node.bounds = RotatedShapeBounds(
                            node.type,
                            node.data1,
                            node.data2,
                            transformedRect,
                            node.rotation,
                            nameof(position));
                        _nodes[i] = node;
                        Encapsulate(node.bounds);
                    }

                    for (int i = firstGlyphSource; i < _glyphSources.Count; ++i)
                    {
                        var source = _glyphSources[i];
                        source.pivot = textPivot;
                        _glyphSources[i] = source;
                    }
                }

            }
            catch
            {
                if (_nodes.Count > firstGlyph)
                    _nodes.RemoveRange(firstGlyph, _nodes.Count - firstGlyph);

                if (_glyphSources.Count > firstGlyphSource)
                    _glyphSources.RemoveRange(firstGlyphSource, _glyphSources.Count - firstGlyphSource);

                _requiredMaterialAbi = previousRequiredMaterialAbi;
                _texture = previousTexture;
                _textureFromGlyph = previousTextureFromGlyph;
                _textPixelRange = previousTextPixelRange;
                _failedTextPixelRange = previousFailedTextPixelRange;
                _failedTextFontVersion = previousFailedTextFontVersion;
                _contentRevision = previousContentRevision;
                _bounds = previousBounds;
                _hasBounds = previouslyHadBounds;
                throw;
            }

            SkipPrimitive();
        }

        internal int RequiredTextPixelRange(float effectBudget)
        {
            effectBudget = SanitizeEffectBudget(effectBudget);
            int pixelRange = _textPixelRange;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                NowSdfGlyphSource source = _glyphSources[i];

                if (source.owner != null && source.fontSize > 0f)
                {
                    pixelRange = Mathf.Max(
                        pixelRange,
                        source.owner.GetDynamicPixelRange(
                            effectBudget / source.fontSize,
                            source.fontSize));
                }
            }

            return pixelRange;
        }

        internal int BaseTextPixelRange()
        {
            int pixelRange = 0;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                NowSdfGlyphSource source = _glyphSources[i];

                if (source.owner != null)
                {
                    pixelRange = Mathf.Max(
                        pixelRange,
                        source.owner.GetDynamicPixelRange(0f, source.fontSize));
                }
            }

            return pixelRange;
        }

        internal bool TryEnsureTextPixelRange(
            int pixelRange,
            Texture requiredTexture,
            out bool changed,
            bool allowDowngrade = false)
        {
            changed = false;
            pixelRange = allowDowngrade
                ? Mathf.Max(1, pixelRange)
                : Mathf.Max(pixelRange, _textPixelRange);

            if (_glyphSources.Count == 0)
                return true;

            if (!_textureFromGlyph)
                return false;

            NowFont owner = _glyphSources[0].owner;
            int fontVersion = owner != null ? owner.layoutDataVersion : -1;

            // Published dynamic pages survive ordinary budget pressure, but an
            // explicit font-cache clear destroys their Unity texture. Re-resolve
            // in that exceptional case instead of retaining a stale graph atlas.
            if (pixelRange == _textPixelRange && TextAtlasIsCurrent())
            {
                return requiredTexture == null || ReferenceEquals(requiredTexture, _texture);
            }

            if (_failedTextPixelRange == pixelRange && _failedTextFontVersion == fontVersion)
                return false;

            _resolvedGlyphs.Clear();
            Texture resolvedTexture = null;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                var source = _glyphSources[i];

                if (source.owner == null ||
                    !source.owner.GetGlyphForPixelRange(
                        source.codepoint,
                        source.fontSize,
                        pixelRange,
                        out var glyph,
                        out var material) ||
                    source.owner.isColor ||
                    material == null ||
                    material.mainTexture == null)
                {
                    _resolvedGlyphs.Clear();
                    RecordTextRangeFailure(pixelRange, fontVersion);
                    return false;
                }

                if (resolvedTexture == null)
                    resolvedTexture = material.mainTexture;
                else if (!ReferenceEquals(resolvedTexture, material.mainTexture))
                {
                    // SDF scenes intentionally expose one source texture. Keep the
                    // current, internally consistent glyph tier if a font fallback
                    // or a full dynamic page would split this graph across atlases.
                    _resolvedGlyphs.Clear();
                    RecordTextRangeFailure(pixelRange, fontVersion);
                    return false;
                }

                _resolvedGlyphs.Add(new NowSdfResolvedGlyph
                {
                    font = source.owner,
                    glyph = glyph,
                    material = material
                });
            }

            if (requiredTexture != null && !ReferenceEquals(requiredTexture, resolvedTexture))
            {
                _resolvedGlyphs.Clear();
                RecordTextRangeFailure(pixelRange, fontVersion);
                return false;
            }

            _texture = resolvedTexture;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                NowSdfGlyphSource source = _glyphSources[i];
                NowSdfResolvedGlyph resolved = _resolvedGlyphs[i];
                NowSdfNode node = _nodes[source.nodeIndex];
                NowRect rect = GlyphRect(
                    source.x,
                    source.y,
                    source.baseline,
                    source.fontSize,
                    resolved.glyph);

                if (source.rotation != Vector2.zero)
                {
                    Vector2 center = RotatePointAroundPivot(
                        rect.center,
                        source.pivot,
                        source.rotation,
                        nameof(pixelRange));
                    rect = new NowRect(
                        center.x - rect.width * 0.5f,
                        center.y - rect.height * 0.5f,
                        rect.width,
                        rect.height);
                }

                node.data1 = RectData(rect);
                node.data2.x = resolved.font.GetScreenPixelRangeForPixelRange(
                    source.codepoint,
                    source.fontSize,
                    pixelRange);
                node.data2.y = GetSdfEncoding(resolved.material);
                node.data2.z = GetSdfDistanceCodeStep(node.data2.x, node.data2.y);
                node.uv = new Vector4(
                    resolved.glyph.atlasBounds.left,
                    resolved.glyph.atlasBounds.bottom,
                    resolved.glyph.atlasBounds.right - resolved.glyph.atlasBounds.left,
                    resolved.glyph.atlasBounds.top - resolved.glyph.atlasBounds.bottom);
                node.bounds = source.rotation == Vector2.zero
                    ? rect
                    : RotatedShapeBounds(
                        node.type,
                        node.data1,
                        node.data2,
                        rect,
                        source.rotation,
                        nameof(pixelRange));
                _nodes[source.nodeIndex] = node;
                source.owner = resolved.font;
                source.ownerVersion = resolved.font.layoutDataVersion;
                _glyphSources[i] = source;
            }

            _resolvedGlyphs.Clear();
            _textPixelRange = pixelRange;
            _failedTextPixelRange = 0;
            _failedTextFontVersion = -1;
            RebuildBounds();
            changed = true;
            return true;
        }

        void RecordTextRangeFailure(int pixelRange, int fontVersion)
        {
            _failedTextPixelRange = pixelRange;
            _failedTextFontVersion = fontVersion;
        }

        internal bool UsesTextOwner(NowFont owner)
        {
            if (owner == null || _glyphSources.Count == 0)
                return false;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                if (!ReferenceEquals(_glyphSources[i].owner, owner))
                    return false;
            }

            return true;
        }

        internal bool TryGetTextOwner(out NowFont owner)
        {
            owner = _glyphSources.Count > 0 ? _glyphSources[0].owner : null;
            return UsesTextOwner(owner);
        }

        internal bool TextAtlasIsCurrent()
        {
            if (_glyphSources.Count == 0)
                return true;

            if (!_textureFromGlyph || _texture == null)
                return false;

            for (int i = 0; i < _glyphSources.Count; ++i)
            {
                NowSdfGlyphSource source = _glyphSources[i];

                if (source.owner == null || source.ownerVersion != source.owner.layoutDataVersion)
                    return false;
            }

            return true;
        }

        static float SanitizeEffectBudget(float effectBudget)
        {
            return float.IsNaN(effectBudget) || float.IsInfinity(effectBudget)
                ? 0f
                : Mathf.Max(0f, effectBudget);
        }

        static float GetSdfEncoding(Material material)
        {
            return material != null &&
                material.HasProperty(_textSdfEncodingProp) &&
                material.GetFloat(_textSdfEncodingProp) > 0.5f
                    ? 1f
                    : 0f;
        }

        static float GetSdfDistanceCodeStep(float screenPixelRange, float encoding)
        {
            float codeCount = encoding > 0.5f ? 65535f : 255f;
            return Mathf.Max(0f, screenPixelRange) / codeCount;
        }

        void RebuildBounds()
        {
            _bounds = default;
            _hasBounds = false;

            for (int i = 0; i < _nodes.Count; ++i)
                Encapsulate(_nodes[i].bounds);
        }

        void AppendNode(
            NowSdfShapeType type,
            Vector4 data1,
            Vector4 data2,
            NowRect bounds,
            Vector4 uv,
            bool useTexture,
            NowSdfOperation operation,
            float smoothing,
            Vector2 rotation,
            bool encapsulate)
        {
            AdvanceContentRevision();
            operation = _nodes.Count == 0 ? NowSdfOperation.Union : operation;
            _nodes.Add(new NowSdfNode
            {
                type = type,
                operation = operation,
                smoothing = smoothing,
                data1 = data1,
                data2 = data2,
                color = _color,
                uv = uv,
                rotation = rotation,
                useTexture = useTexture,
                bounds = bounds
            });

            if (type == NowSdfShapeType.ChamferedBox ||
                type == NowSdfShapeType.Triangle ||
                rotation != Vector2.zero)
            {
                _requiredMaterialAbi = 2;
            }

            if (encapsulate)
                Encapsulate(bounds);
        }

        void AdvanceContentRevision()
        {
            unchecked
            {
                ++_contentRevision;
            }
        }

        static Vector2 RotatePointAroundPivot(
            Vector2 point,
            Vector2 pivot,
            Vector2 rotation,
            string parameterName)
        {
            double x = (double)point.x - pivot.x;
            double y = (double)point.y - pivot.y;
            var result = new Vector2(
                (float)((double)pivot.x + (double)rotation.x * x - (double)rotation.y * y),
                (float)((double)pivot.y + (double)rotation.y * x + (double)rotation.x * y));
            ValidateFinite(result, parameterName);
            return result;
        }

        bool TryBindTexture(Texture texture)
        {
            if (texture == null)
                return false;

            if (_texture == null)
            {
                _texture = texture;
                _textureFromGlyph = true;
                return true;
            }

            return ReferenceEquals(_texture, texture);
        }

        static NowRect GlyphRect(float x, float y, float baseline, float fontSize, NowFontAtlasInfo.Glyph glyph)
        {
            var plane = glyph.planeBounds;
            float left = plane.left * fontSize;
            float right = plane.right * fontSize;
            float bottom = plane.bottom * fontSize;
            float top = plane.top * fontSize;
            return new NowRect(x + left, y + baseline - top, right - left, top - bottom);
        }

        static Vector4 RectData(NowRect rect)
        {
            return new Vector4(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, rect.width, rect.height);
        }

        internal NowSdfGraph SetNextRotationDegrees(float angleDegrees)
        {
            _nextRotationDegrees = angleDegrees;
            return this;
        }

        internal static Vector2 RotationDegrees(float angleDegrees)
        {
            ValidateFinite(angleDegrees, nameof(angleDegrees));
            float normalized = NormalizeRotationDegrees(angleDegrees);

            // Preserve exact identity and quarter-turn values. Besides producing
            // tighter bounds, this avoids tiny trigonometric drift at common angles.
            if (normalized == 0f)
                return Vector2.zero;
            if (normalized == 90f)
                return new Vector2(0f, 1f);
            if (normalized == 180f)
                return new Vector2(-1f, 0f);
            if (normalized == 270f)
                return new Vector2(0f, -1f);

            double radians = normalized * Math.PI / 180d;
            return new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));
        }

        internal static float NormalizeRotationDegrees(float angleDegrees)
        {
            float normalized = angleDegrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        Vector2 EffectiveRotation()
        {
            float scoped = _rotationStack.Count > 0
                ? _rotationStack[_rotationStack.Count - 1]
                : 0f;
            return RotationDegrees(scoped + _nextRotationDegrees);
        }

        static NowRect RotatedShapeBounds(
            NowSdfShapeType type,
            Vector4 data1,
            Vector4 data2,
            NowRect authoredBounds,
            Vector2 rotation,
            string parameterName)
        {
            if (type == NowSdfShapeType.Triangle)
                return RotatedTriangleBounds(data1, data2, authoredBounds, rotation, parameterName);

            Vector2 pivot = RotationPivot(type, data1, data2);
            double authoredHalfWidth = AuthoredHalfExtent(
                authoredBounds.x,
                authoredBounds.width,
                authoredBounds.xMax,
                pivot.x);
            double authoredHalfHeight = AuthoredHalfExtent(
                authoredBounds.y,
                authoredBounds.height,
                authoredBounds.yMax,
                pivot.y);

            if (type == NowSdfShapeType.Circle ||
                type == NowSdfShapeType.Arc ||
                type == NowSdfShapeType.Pie)
            {
                double outer = type == NowSdfShapeType.Arc
                    ? (double)data1.z + data1.w
                    : data1.z;
                outer = Math.Max(outer, Math.Max(authoredHalfWidth, authoredHalfHeight));
                return RotatedRadialBounds(pivot, outer, rotation, parameterName);
            }

            double halfWidth = authoredHalfWidth;
            double halfHeight = authoredHalfHeight;

            if (type == NowSdfShapeType.Capsule)
            {
                double radius = Math.Max(0d, data2.x);
                halfWidth = Math.Max(
                    halfWidth,
                    Math.Max(
                        Math.Abs((double)data1.x - pivot.x),
                        Math.Abs((double)data1.z - pivot.x)) + radius);
                halfHeight = Math.Max(
                    halfHeight,
                    Math.Max(
                        Math.Abs((double)data1.y - pivot.y),
                        Math.Abs((double)data1.w - pivot.y)) + radius);
            }
            else
            {
                // The v2 shader floors rectangle-family half sizes so collapsed
                // inputs still have a stable analytic distance field.
                halfWidth = Math.Max(halfWidth, Math.Max((double)data1.z * 0.5d, 0.0001d));
                halfHeight = Math.Max(halfHeight, Math.Max((double)data1.w * 0.5d, 0.0001d));
            }

            return RotatedAabbBounds(pivot, halfWidth, halfHeight, rotation, parameterName);
        }

        static NowRect RotatedTriangleBounds(
            Vector4 data1,
            Vector4 data2,
            NowRect authoredBounds,
            Vector2 rotation,
            string parameterName)
        {
            float scale = Mathf.Max(data2.w, (float)FloatMinNormal);
            var a = new Vector2(data1.x, data1.y);
            var separateB = a + new Vector2(data1.z, data1.w) * scale;
            var separateC = a + new Vector2(data2.x, data2.y) * scale;
            var fusedB = new Vector2(
                (float)((double)a.x + (double)data1.z * scale),
                (float)((double)a.y + (double)data1.w * scale));
            var fusedC = new Vector2(
                (float)((double)a.x + (double)data2.x * scale),
                (float)((double)a.y + (double)data2.y * scale));
            Vector2 separatePivot = TrianglePivot(a, separateB, separateC, false);
            Vector2 fusedPivot = TrianglePivot(a, fusedB, fusedC, true);

            NowRect separateBounds = RotatedTriangleCandidateBounds(
                authoredBounds,
                a,
                separateB,
                separateC,
                fusedB,
                fusedC,
                separatePivot,
                rotation,
                parameterName);
            NowRect fusedBounds = RotatedTriangleCandidateBounds(
                authoredBounds,
                a,
                separateB,
                separateC,
                fusedB,
                fusedC,
                fusedPivot,
                rotation,
                parameterName);
            return UnionConservativeBounds(separateBounds, fusedBounds, parameterName);
        }

        static NowRect RotatedTriangleCandidateBounds(
            NowRect authoredBounds,
            Vector2 a,
            Vector2 separateB,
            Vector2 separateC,
            Vector2 fusedB,
            Vector2 fusedC,
            Vector2 pivot,
            Vector2 rotation,
            string parameterName)
        {
            double halfWidth = AuthoredHalfExtent(
                authoredBounds.x,
                authoredBounds.width,
                authoredBounds.xMax,
                pivot.x);
            double halfHeight = AuthoredHalfExtent(
                authoredBounds.y,
                authoredBounds.height,
                authoredBounds.yMax,
                pivot.y);
            ExpandHalfExtents(a, pivot, ref halfWidth, ref halfHeight);
            ExpandHalfExtents(separateB, pivot, ref halfWidth, ref halfHeight);
            ExpandHalfExtents(separateC, pivot, ref halfWidth, ref halfHeight);
            ExpandHalfExtents(fusedB, pivot, ref halfWidth, ref halfHeight);
            ExpandHalfExtents(fusedC, pivot, ref halfWidth, ref halfHeight);
            return RotatedAabbBounds(pivot, halfWidth, halfHeight, rotation, parameterName);
        }

        static void ExpandHalfExtents(
            Vector2 point,
            Vector2 pivot,
            ref double halfWidth,
            ref double halfHeight)
        {
            halfWidth = Math.Max(halfWidth, Math.Abs((double)point.x - pivot.x));
            halfHeight = Math.Max(halfHeight, Math.Abs((double)point.y - pivot.y));
        }

        static Vector2 TrianglePivot(Vector2 a, Vector2 b, Vector2 c, bool fusedMidpoint)
        {
            Vector2 min = Vector2.Min(a, Vector2.Min(b, c));
            Vector2 max = Vector2.Max(a, Vector2.Max(b, c));
            Vector2 span = max - min;
            if (!fusedMidpoint)
                return min + span * 0.5f;

            return new Vector2(
                (float)((double)min.x + (double)span.x * 0.5d),
                (float)((double)min.y + (double)span.y * 0.5d));
        }

        static NowRect RotatedAabbBounds(
            Vector2 pivot,
            double halfWidth,
            double halfHeight,
            Vector2 rotation,
            string parameterName)
        {
            double factor = ShaderRotationUpperFactor(rotation, out bool exactCardinal);
            double extentX = (
                Math.Abs((double)rotation.x) * halfWidth +
                Math.Abs((double)rotation.y) * halfHeight) * factor;
            double extentY = (
                Math.Abs((double)rotation.y) * halfWidth +
                Math.Abs((double)rotation.x) * halfHeight) * factor;

            if (!exactCardinal)
            {
                extentX = NextFloat(RoundUpToFloat(extentX));
                extentY = NextFloat(RoundUpToFloat(extentY));
            }

            double pad = RotationArithmeticPad(
                pivot,
                halfWidth,
                halfHeight,
                extentX,
                extentY);

            return BoundsFromExtrema(
                (double)pivot.x - extentX - pad,
                (double)pivot.y - extentY - pad,
                (double)pivot.x + extentX + pad,
                (double)pivot.y + extentY + pad,
                true,
                parameterName);
        }

        static NowRect RotatedRadialBounds(
            Vector2 pivot,
            double outer,
            Vector2 rotation,
            string parameterName)
        {
            double squaredLength =
                (double)rotation.x * rotation.x +
                (double)rotation.y * rotation.y;
            double dotUpper = ShaderRotationDotUpper(rotation, out bool exactCardinal);
            double extent = outer * dotUpper / Math.Sqrt(squaredLength);
            if (!exactCardinal)
                extent = NextFloat(RoundUpToFloat(extent));

            double pad = RotationArithmeticPad(
                pivot,
                outer,
                outer,
                extent,
                extent);

            return BoundsFromExtrema(
                (double)pivot.x - extent - pad,
                (double)pivot.y - extent - pad,
                (double)pivot.x + extent + pad,
                (double)pivot.y + extent + pad,
                true,
                parameterName);
        }

        static double RotationArithmeticPad(
            Vector2 pivot,
            double halfWidth,
            double halfHeight,
            double extentX,
            double extentY)
        {
            // A rotated node is evaluated through several float subtract,
            // multiply, add, divide, and distance operations. Bound that error
            // as a short IEEE-754 operation chain, including cancellation when
            // the authored pivot is much larger than the primitive itself.
            double magnitude =
                2d * (Math.Abs((double)pivot.x) + Math.Abs((double)pivot.y)) +
                halfWidth + halfHeight + extentX + extentY;
            return RotationBoundsGamma32 * magnitude + 32d * float.Epsilon;
        }

        static double ShaderRotationUpperFactor(Vector2 rotation, out bool exactCardinal)
        {
            double squaredLength =
                (double)rotation.x * rotation.x +
                (double)rotation.y * rotation.y;
            return ShaderRotationDotUpper(rotation, out exactCardinal) / squaredLength;
        }

        static double ShaderRotationDotUpper(Vector2 rotation, out bool exactCardinal)
        {
            exactCardinal =
                (rotation.x == 0f && Math.Abs(rotation.y) == 1f) ||
                (rotation.y == 0f && Math.Abs(rotation.x) == 1f);
            if (exactCardinal)
                return 1d;

            float xSquaredUpper = RoundUpToFloat((double)rotation.x * rotation.x);
            float ySquaredUpper = RoundUpToFloat((double)rotation.y * rotation.y);
            return RoundUpToFloat((double)xSquaredUpper + ySquaredUpper);
        }

        static double AuthoredHalfExtent(float start, float size, float roundedMax, float pivot)
        {
            return Math.Max(
                Math.Abs((double)start - pivot),
                Math.Max(
                    Math.Abs((double)start + size - pivot),
                    Math.Abs((double)roundedMax - pivot)));
        }

        static NowRect UnionConservativeBounds(NowRect a, NowRect b, string parameterName)
        {
            return BoundsFromExtrema(
                Math.Min(a.x, b.x),
                Math.Min(a.y, b.y),
                Math.Max((double)a.x + a.width, (double)b.x + b.width),
                Math.Max((double)a.y + a.height, (double)b.y + b.height),
                false,
                parameterName);
        }

        static NowRect BoundsFromExtrema(
            double minX,
            double minY,
            double maxX,
            double maxY,
            bool guardOutput,
            string parameterName)
        {
            float roundedMinX = RoundDownToFloat(minX);
            float roundedMinY = RoundDownToFloat(minY);
            float roundedMaxX = RoundUpToFloat(maxX);
            float roundedMaxY = RoundUpToFloat(maxY);

            if (guardOutput)
            {
                roundedMinX = PreviousFloat(roundedMinX);
                roundedMinY = PreviousFloat(roundedMinY);
                roundedMaxX = NextFloat(roundedMaxX);
                roundedMaxY = NextFloat(roundedMaxY);
            }

            var min = new Vector2(roundedMinX, roundedMinY);
            var max = new Vector2(roundedMaxX, roundedMaxY);
            ValidateBounds(min, max, parameterName);
            var result = new NowRect(
                min.x,
                min.y,
                max.x - min.x,
                max.y - min.y);

            if (result.xMax < max.x)
                result.width = NextFloat(result.width);
            if (result.yMax < max.y)
                result.height = NextFloat(result.height);

            ValidateFiniteRect(result, parameterName);
            return result;
        }

        static Vector2 RotationPivot(NowSdfShapeType type, Vector4 data1, Vector4 data2)
        {
            if (type == NowSdfShapeType.Capsule)
            {
                return new Vector2(
                    data1.x * 0.5f + data1.z * 0.5f,
                    data1.y * 0.5f + data1.w * 0.5f);
            }

            if (type == NowSdfShapeType.Triangle)
            {
                float scale = Mathf.Max(data2.w, (float)FloatMinNormal);
                Vector2 a = new Vector2(data1.x, data1.y);
                Vector2 b = a + new Vector2(data1.z, data1.w) * scale;
                Vector2 c = a + new Vector2(data2.x, data2.y) * scale;
                return TrianglePivot(a, b, c, false);
            }

            return new Vector2(data1.x, data1.y);
        }

        static float RoundDownToFloat(double value)
        {
            float rounded = (float)value;
            return IsFinite(rounded) && rounded > value ? PreviousFloat(rounded) : rounded;
        }

        static float RoundUpToFloat(double value)
        {
            float rounded = (float)value;
            return IsFinite(rounded) && rounded < value ? NextFloat(rounded) : rounded;
        }

        static float PreviousFloat(float value)
        {
            if (value == 0f)
                return -float.Epsilon;

            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(value > 0f ? bits - 1 : bits + 1);
        }

        static float NextFloat(float value)
        {
            if (value == 0f)
                return float.Epsilon;

            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(value > 0f ? bits + 1 : bits - 1);
        }

        static Vector4 RadialData(float from, float sweep)
        {
            if (Mathf.Abs(sweep) >= FullTurnRadians)
                return new Vector4(0f, -1f, 0f, 0f);

            from %= FullTurnRadians;
            float half = Mathf.Abs(sweep) * 0.5f;
            float rotation = Mathf.PI * 0.5f - (from + sweep * 0.5f);
            return new Vector4(Mathf.Sin(half), Mathf.Cos(half), Mathf.Cos(rotation), Mathf.Sin(rotation));
        }

        NowSdfGraph SkipPrimitive()
        {
            _operation = NowSdfOperation.Union;
            _smoothing = 0f;
            _nextRotationDegrees = 0f;
            return this;
        }

        static void ValidateArcBounds(Vector2 center, float radius, float thickness, float outer)
        {
            if (!IsFinite(radius * 2f))
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "Arc radius is too large to produce representable bounds.");

            if (!IsFinite(outer) || !IsFinite(outer * 2f))
                throw new ArgumentOutOfRangeException(
                    nameof(thickness),
                    "Arc radius and thickness are too large to produce representable bounds.");

            ValidateRadialPlacement(center, outer);
        }

        static void ValidatePieBounds(Vector2 center, float radius)
        {
            if (!IsFinite(radius * 2f))
                throw new ArgumentOutOfRangeException(
                    nameof(radius),
                    "Pie radius is too large to produce representable bounds.");

            ValidateRadialPlacement(center, radius);
        }

        static void ValidateRadialPlacement(Vector2 center, float extent)
        {
            if (IsFinite(center.x - extent) && IsFinite(center.x + extent) &&
                IsFinite(center.y - extent) && IsFinite(center.y + extent))
                return;

            throw new ArgumentOutOfRangeException(
                nameof(center),
                "Radial shape center and extent must produce finite bounds.");
        }

        static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
                throw new ArgumentOutOfRangeException(parameterName, "SDF shape coordinates must be finite.");
        }

        static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
                throw new ArgumentOutOfRangeException(parameterName, "SDF shape values must be finite.");
        }

        static void ValidateFiniteRect(NowRect rect, string parameterName)
        {
            if (!IsFinite(rect.x) || !IsFinite(rect.y) ||
                !IsFinite(rect.width) || !IsFinite(rect.height) ||
                !IsFinite(rect.xMax) || !IsFinite(rect.yMax))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "SDF shape bounds must be finite and representable.");
            }
        }

        static void ValidateBounds(Vector2 min, Vector2 max, string parameterName)
        {
            if (IsFinite(min.x) && IsFinite(min.y) && IsFinite(max.x) && IsFinite(max.y) &&
                IsFinite(max.x - min.x) && IsFinite(max.y - min.y))
                return;

            throw new ArgumentOutOfRangeException(
                parameterName,
                "SDF shape bounds must be finite and representable.");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void Encapsulate(NowRect rect)
        {
            if (rect.isEmpty)
                return;

            _bounds = _hasBounds ? _bounds.Union(rect) : rect;
            _hasBounds = true;
        }
    }

    /// <summary>
    /// Entry points for signed-distance-field shape composition.
    /// <code>
    /// var cutout = NowSdf.Graph().Circle(new Vector2(92, 52), 18);
    ///
    /// NowSdf.Scene(rect)
    ///     .SetColor(Color.red).Circle(new Vector2(44, 44), 36)
    ///     .SetColor(Color.cyan).SmoothUnion(10).RoundedBox(new NowRect(38, 20, 120, 70), 16)
    ///     .Subtract().Graph(cutout)
    ///     .Draw();
    /// </code>
    /// Shape coordinates are local to the scene rect. Operations apply to the
    /// next primitive or graph, then reset to Union.
    /// </summary>
    public static class NowSdf
    {
        public const int MaxShapes = 64;
        public const int MaxLayers = 16;

        /// <summary>
        /// Version of the material/shader data contract consumed by
        /// <see cref="NowSdfBuilder.SetMaterial(Material, bool)"/>.
        /// </summary>
        public const int MaterialAbiVersion = 2;

        /// <summary>Oldest material ABI accepted for scenes that only use legacy primitives.</summary>
        public const int MinimumMaterialAbiVersion = 1;

        /// <summary>Shader property that declares the supported SDF material ABI.</summary>
        public const string MaterialAbiProperty = "_NowSdfAbiVersion";

        static readonly Dictionary<NowResolvedId, NowSdfCache> _caches =
            new Dictionary<NowResolvedId, NowSdfCache>(16);

        static int _maskRasterizationCount;

        internal static int cacheCount => _caches.Count;

        internal static int maskRasterizationCount => _maskRasterizationCount;

        internal static int maskTextureCount
        {
            get
            {
                int count = 0;

                foreach (var cache in _caches.Values)
                {
                    if (cache.hasMaskTexture)
                        ++count;
                }

                return count;
            }
        }

        internal static long cachedMaskPixels
        {
            get
            {
                long pixels = 0;

                foreach (var cache in _caches.Values)
                    pixels += cache.maskTexturePixels;

                return pixels;
            }
        }

        public static NowSdfGraph Graph()
        {
            return new NowSdfGraph();
        }

        public static NowSdfBuilder Scene(
            NowRect rect,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), rect, true, default);
        }

        public static NowSdfBuilder Scene(NowRect rect, NowResolvedId id)
        {
            return new NowSdfBuilder(GetCache(id), rect, true, default);
        }

        public static NowSdfBuilder Scene(
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, default);
        }

        public static NowSdfBuilder Scene(NowResolvedId id)
        {
            return new NowSdfBuilder(GetCache(id), default, false, default);
        }

        public static NowSdfBuilder Scene(
            float width,
            float height,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            var options = new NowLayoutOptions().SetSize(width, height);
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, options);
        }

        public static NowSdfBuilder Scene(float width, float height, NowResolvedId id)
        {
            var options = new NowLayoutOptions().SetSize(width, height);
            return new NowSdfBuilder(GetCache(id), default, false, options);
        }

        public static NowSdfBuilder Scene(
            NowLayoutOptions options,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSdfBuilder(GetCache(ControlId(id, file, line)), default, false, options);
        }

        public static NowSdfBuilder Scene(NowLayoutOptions options, NowResolvedId id)
        {
            return new NowSdfBuilder(GetCache(id), default, false, options);
        }

        /// <summary>
        /// Releases the cache owned by an explicit stable id in the current host
        /// and <see cref="NowControls.IdScope(string)"/>. Use this when dynamically
        /// generated ids leave a long-lived collection so their materials and mask
        /// render texture do not remain cached until <see cref="Reset"/>.
        /// Any retained batch still sampling this cache's mask texture becomes
        /// invalid, just as it does after <see cref="Reset"/>; rebuild or discard
        /// those batches before releasing the id. Builders previously returned for
        /// this id are invalidated and throw <see cref="ObjectDisposedException"/>
        /// when measured, drawn, or used as a mask.
        /// </summary>
        /// <returns>True when a cache existed and was released.</returns>
        public static bool Release(NowId id)
        {
            if (!id.hasValue)
                throw new ArgumentException("NowSdf.Release requires an explicit stable NowId.", nameof(id));

            return Release(NowControls.GetControlId(id));
        }

        /// <summary>Releases a cache using the resolved identity captured while drawing its host.</summary>
        public static bool Release(NowResolvedId id)
        {
            if (!id.hasValue)
                throw new ArgumentException("NowSdf.Release requires a resolved scene id.", nameof(id));

            if (!_caches.TryGetValue(id, out var cache))
                return false;

            _caches.Remove(id);
            cache.Release();
            return true;
        }

        public static void Reset()
        {
            foreach (var cache in _caches.Values)
                cache.Release();

            _caches.Clear();
            _maskRasterizationCount = 0;
        }

        internal static void RecordMaskRasterization()
        {
            ++_maskRasterizationCount;
        }

        static NowResolvedId ControlId(NowId id, string file, int line)
        {
            return NowControls.GetControlId(id, NowControls.SiteId(file, line));
        }

        static NowSdfCache GetCache(NowResolvedId id)
        {
            if (!id.hasValue)
                throw new ArgumentException("A resolved SDF scene id is required.", nameof(id));

            if (!_caches.TryGetValue(id, out var cache))
            {
                cache = new NowSdfCache();
                _caches[id] = cache;
            }

            cache.Begin();
            return cache;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    [NowBuilder]
    public struct NowSdfBuilder
    {
        readonly NowSdfCache _cache;
        readonly bool _hasRect;
        readonly NowRect _rect;
        NowLayoutOptions _options;
        NowRect _mask;
        bool _hasMask;
        float _maskResolutionScale;
        Vector4 _tint;

        internal NowSdfBuilder(NowSdfCache cache, NowRect rect, bool hasRect, NowLayoutOptions options)
        {
            _cache = cache;
            _rect = rect;
            _hasRect = hasRect;
            _options = options;
            _mask = default;
            _hasMask = false;
            _maskResolutionScale = 1f;
            _tint = Vector4.one;
        }

        public NowSdfBuilder SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowSdfBuilder SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowSdfBuilder SetHeight(float height) { _options = _options.SetHeight(height); return this; }

        public NowSdfBuilder SetLayoutSize(float width, float height) { _options = _options.SetSize(width, height); return this; }

        public NowSdfBuilder SetMinWidth(float minWidth) { _options = _options.SetMinWidth(minWidth); return this; }

        public NowSdfBuilder SetMaxWidth(float maxWidth) { _options = _options.SetMaxWidth(maxWidth); return this; }

        public NowSdfBuilder SetMinHeight(float minHeight) { _options = _options.SetMinHeight(minHeight); return this; }

        public NowSdfBuilder SetMaxHeight(float maxHeight) { _options = _options.SetMaxHeight(maxHeight); return this; }

        public NowSdfBuilder SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowSdfBuilder SetStretchHeight(float weight = 1f) { _options = _options.SetStretchHeight(weight); return this; }

        public NowSdfBuilder SetAlign(NowLayoutAlign align) { _options = _options.SetAlign(align); return this; }

        public NowSdfBuilder SetMask(NowRect mask)
        {
            _mask = mask;
            _hasMask = true;
            return this;
        }

        /// <summary>
        /// Scales the cached coverage texture rasterized by <see cref="BeginMask()"/>.
        /// The default is 1, matching the scene's transformed physical pixel size.
        /// Values below 1 reduce mask rasterization work and persistent texture memory,
        /// while values above 1 supersample up to the device texture-size limit. Because the
        /// target stores final coverage, downsampling widens its minimum AA ramp and can
        /// remove fine detail. Keep the scale stable for each stable scene id to avoid
        /// target resize churn.
        /// This setting does not affect <see cref="Draw()"/>, which evaluates the SDF
        /// directly at the destination resolution.
        /// </summary>
        /// <param name="scale">Positive, finite linear scale applied to both target axes.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="scale"/> is not finite and greater than zero.
        /// </exception>
        public NowSdfBuilder SetMaskResolutionScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scale),
                    scale,
                    "SDF mask resolution scale must be finite and greater than zero.");
            }

            _maskResolutionScale = scale;
            return this;
        }

        /// <summary>
        /// Uses a caller-provided SDF material as a template for this scene. The
        /// template must declare <see cref="NowSdf.MaterialAbiProperty"/> with a
        /// supported integer ABI version and implement that version's scene-array
        /// and vertex-stream contract. ABI 1 remains valid for legacy primitives;
        /// ChamferedBox, Triangle, and nonidentity node rotation require
        /// <see cref="NowSdf.MaterialAbiVersion"/>.
        /// NowUI creates and owns separate direct and mask clones as needed for
        /// each distinct template in a resolved scene cache; it never mutates or
        /// destroys <paramref name="material"/>. This overload treats the template
        /// as an immutable snapshot so static upload and mask caches remain reusable.
        /// </summary>
        /// <param name="material">Compatible template, or null to use the built-in material.</param>
        public NowSdfBuilder SetMaterial(Material material)
        {
            return SetMaterial(material, syncPerFrame: false);
        }

        /// <summary>
        /// Uses a compatible caller-provided material template and explicitly
        /// controls whether its project-defined properties are recopied each frame.
        /// </summary>
        /// <param name="material">Compatible template, or null to use the built-in material.</param>
        /// <param name="syncPerFrame">
        /// When true, copy template properties before each draw. A synchronized
        /// custom mask rerasterizes because arbitrary shader properties cannot be
        /// versioned reliably.
        /// </param>
        public NowSdfBuilder SetMaterial(Material material, bool syncPerFrame)
        {
            _cache.SetMaterial(material, syncPerFrame);
            return this;
        }

        public NowSdfBuilder SetTint(Color color)
        {
            _tint = color;
            return this;
        }

        public NowSdfBuilder SetTint(Vector4 color)
        {
            _tint = color;
            return this;
        }

        public NowSdfBuilder SetColor(Color color)
        {
            _cache.SetColor(color);
            return this;
        }

        public NowSdfBuilder SetColor(Vector4 color)
        {
            _cache.SetColor(color);
            return this;
        }

        public NowSdfBuilder UseColor()
        {
            _cache.UseColor();
            return this;
        }

        public NowSdfBuilder SetTexture(Texture texture)
        {
            _cache.SetTexture(texture);
            return this;
        }

        public NowSdfBuilder SetTexture(Texture texture, Vector4 uvRect)
        {
            _cache.SetTexture(texture);
            _cache.SetTextureUV(uvRect);
            return this;
        }

        public NowSdfBuilder UseTexture()
        {
            _cache.UseTexture();
            return this;
        }

        public NowSdfBuilder UseTexture(Vector4 uvRect)
        {
            _cache.SetTextureUV(uvRect);
            _cache.UseTexture();
            return this;
        }

        public NowSdfBuilder SetTextureUV(Vector4 uvRect)
        {
            _cache.SetTextureUV(uvRect);
            return this;
        }

        public NowSdfBuilder SetFeather(float feather)
        {
            _cache.SetFeather(feather);
            return this;
        }

        /// <summary>
        /// Reserves at least this much scene-local signed-distance reach around
        /// font glyphs, without drawing an outline or other visible effect. Use
        /// it when text participates in smooth SDF operations that need more
        /// source field outside the glyph edge. The range is selected lazily
        /// when the scene is measured or drawn and remains subject to the font's
        /// generated-resource cap.
        /// </summary>
        public NowSdfBuilder SetTextDistanceMargin(float margin)
        {
            _cache.SetTextDistanceMargin(margin);
            return this;
        }

        public NowSdfBuilder SetOutline(float width, Color color, float softness = 0f)
        {
            _cache.SetOutline(width, color, softness);
            return this;
        }

        public NowSdfBuilder SetOutline(float width, Vector4 color, float softness = 0f)
        {
            _cache.SetOutline(width, color, softness);
            return this;
        }

        public NowSdfBuilder SetGlow(float radius, Color color, float power = 1f)
        {
            _cache.SetGlow(radius, color, power);
            return this;
        }

        public NowSdfBuilder SetGlow(float radius, Vector4 color, float power = 1f)
        {
            _cache.SetGlow(radius, color, power);
            return this;
        }

        public NowSdfBuilder SetShadow(Vector2 offset, float softness, Color color, float spread = 0f)
        {
            _cache.SetShadow(offset, softness, color, spread);
            return this;
        }

        public NowSdfBuilder SetShadow(Vector2 offset, float softness, Vector4 color, float spread = 0f)
        {
            _cache.SetShadow(offset, softness, color, spread);
            return this;
        }

        public NowSdfBuilder SetInnerShadow(Vector2 offset, float softness, Color color, float spread = 0f)
        {
            _cache.SetInnerShadow(offset, softness, color, spread);
            return this;
        }

        public NowSdfBuilder SetInnerShadow(Vector2 offset, float softness, Vector4 color, float spread = 0f)
        {
            _cache.SetInnerShadow(offset, softness, color, spread);
            return this;
        }

        public NowSdfBuilder SetEmboss(Vector2 lightDirection, float strength = 0.35f, float size = 6f)
        {
            _cache.SetEmboss(lightDirection, strength, size);
            return this;
        }

        public NowSdfBuilder SetContours(float spacing, float width, Color color, float offset = 0f, int bandCount = 0)
        {
            _cache.SetContours(spacing, width, color, offset, bandCount);
            return this;
        }

        public NowSdfBuilder SetContours(float spacing, float width, Vector4 color, float offset = 0f, int bandCount = 0)
        {
            _cache.SetContours(spacing, width, color, offset, bandCount);
            return this;
        }

        public NowSdfBuilder SetContourMask(Vector2 center, float radius, float softness = 0f)
        {
            _cache.SetContourMask(center, radius, softness);
            return this;
        }

        public NowSdfBuilder SetWarp(float amplitude, float scale, float speed = 0f, float seed = 0f)
        {
            _cache.SetWarp(amplitude, scale, speed, seed);
            return this;
        }

        public NowSdfBuilder SetOperation(NowSdfOperation operation, float smoothing = 0f)
        {
            _cache.SetOperation(operation, smoothing);
            return this;
        }

        public NowSdfBuilder Union(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothUnion : NowSdfOperation.Union, smoothing);
            return this;
        }

        public NowSdfBuilder Subtract(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothSubtract : NowSdfOperation.Subtract, smoothing);
            return this;
        }

        public NowSdfBuilder Intersect(float smoothing = 0f)
        {
            _cache.SetOperation(smoothing > 0f ? NowSdfOperation.SmoothIntersect : NowSdfOperation.Intersect, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothUnion(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothUnion, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothSubtract(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothSubtract, smoothing);
            return this;
        }

        public NowSdfBuilder SmoothIntersect(float smoothing)
        {
            _cache.SetOperation(NowSdfOperation.SmoothIntersect, smoothing);
            return this;
        }

        /// <summary>
        /// Rotates the next analytic primitive around its natural center, or
        /// the next text run around the center of its emitted glyph bounds,
        /// relative to any pushed rotation. Angles are degrees and positive
        /// values rotate clockwise in UI space.
        /// </summary>
        public NowSdfBuilder RotateNext(float angleDegrees)
        {
            _cache.SetNextRotation(angleDegrees);
            return this;
        }

        /// <summary>
        /// Pushes a persistent relative rotation for following analytic
        /// primitives and text runs. Nested pushes compose until their matching
        /// <see cref="PopRotation"/>.
        /// </summary>
        public NowSdfBuilder PushRotation(float angleDegrees)
        {
            _cache.PushRotation(angleDegrees);
            return this;
        }

        /// <summary>Restores the rotation active before the matching push.</summary>
        public NowSdfBuilder PopRotation()
        {
            _cache.PopRotation();
            return this;
        }

        public NowSdfBuilder Graph(NowSdfGraph graph)
        {
            _cache.Graph(graph);
            return this;
        }

        public NowSdfBuilder Morph(NowSdfGraph from, NowSdfGraph to, float t)
        {
            _cache.Morph(from, to, t);
            return this;
        }

        public NowSdfBuilder Lerp(NowSdfGraph from, NowSdfGraph to, float t)
        {
            return Morph(from, to, t);
        }

        public NowSdfBuilder Circle(Vector2 center, float radius)
        {
            _cache.Circle(center, radius);
            return this;
        }

        public NowSdfBuilder Circle(Vector2 center, float radius, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Circle(center, radius);
            return this;
        }

        public NowSdfBuilder Box(NowRect rect)
        {
            _cache.Box(rect);
            return this;
        }

        public NowSdfBuilder Box(NowRect rect, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Box(rect);
            return this;
        }

        public NowSdfBuilder Rectangle(NowRect rect)
        {
            return Box(rect);
        }

        public NowSdfBuilder RoundedBox(NowRect rect, float radius)
        {
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundedBox(NowRect rect, Vector4 radius)
        {
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundedBox(NowRect rect, float radius, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.RoundedBox(rect, radius);
            return this;
        }

        public NowSdfBuilder RoundRect(NowRect rect, float radius)
        {
            return RoundedBox(rect, radius);
        }

        /// <summary>
        /// Adds a box with straight 45-degree corner cuts.
        /// Chamfer is measured along each adjoining edge and clamps to the
        /// available half-side.
        /// </summary>
        public NowSdfBuilder ChamferedBox(NowRect rect, float chamfer)
        {
            _cache.ChamferedBox(rect, chamfer);
            return this;
        }

        public NowSdfBuilder ChamferedBox(NowRect rect, float chamfer, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.ChamferedBox(rect, chamfer);
            return this;
        }

        public NowSdfBuilder Ellipse(NowRect rect)
        {
            _cache.Ellipse(rect);
            return this;
        }

        public NowSdfBuilder Ellipse(NowRect rect, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Ellipse(rect);
            return this;
        }

        public NowSdfBuilder Capsule(Vector2 from, Vector2 to, float radius)
        {
            _cache.Capsule(from, to, radius);
            return this;
        }

        public NowSdfBuilder Capsule(NowRect rect)
        {
            _cache.Capsule(rect);
            return this;
        }

        /// <summary>
        /// Adds a round-capped line segment. <paramref name="width"/> is the full
        /// stroke width, matching <c>Now.Line(...).SetWidth(...)</c>.
        /// </summary>
        public NowSdfBuilder Line(Vector2 from, Vector2 to, float width)
        {
            _cache.Line(from, to, width);
            return this;
        }

        /// <summary>Adds a filled triangle. Vertex winding does not affect the field.</summary>
        public NowSdfBuilder Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            _cache.Triangle(a, b, c);
            return this;
        }

        public NowSdfBuilder Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            _cache.SetColor(color);
            _cache.UseColor();
            _cache.Triangle(a, b, c);
            return this;
        }

        /// <summary>
        /// Adds a circular band. Angles are radians; zero points right, positive
        /// sweeps turn clockwise in UI space, and sweeps clamp to one full turn.
        /// </summary>
        /// <param name="thickness">Half-width of the band around <paramref name="radius"/>.</param>
        public NowSdfBuilder Arc(Vector2 center, float radius, float thickness, float from, float sweep)
        {
            _cache.Arc(center, radius, thickness, from, sweep);
            return this;
        }

        /// <summary>
        /// Adds a filled circular sector. Angles are radians; zero points right,
        /// positive sweeps turn clockwise in UI space, and sweeps clamp to one full turn.
        /// </summary>
        public NowSdfBuilder Pie(Vector2 center, float radius, float from, float sweep)
        {
            _cache.Pie(center, radius, from, sweep);
            return this;
        }

        public NowSdfBuilder Text(Vector2 position, string value, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            _cache.Text(position, value, Now.font, fontSize, fontStyle, tabSpaces);
            return this;
        }

        public NowSdfBuilder Text(Vector2 position, string value, NowFontAsset font, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            _cache.Text(position, value, font != null ? font : Now.font, fontSize, fontStyle, tabSpaces);
            return this;
        }

        public NowSdfBuilder Text(NowRect rect, string value, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            return Text(rect.position, value, fontSize, fontStyle, tabSpaces);
        }

        public NowSdfBuilder Text(NowRect rect, string value, NowFontAsset font, float fontSize, NowFontStyle fontStyle = NowFontStyle.Regular, int tabSpaces = 4)
        {
            return Text(rect.position, value, font, fontSize, fontStyle, tabSpaces);
        }

        public Vector2 Measure()
        {
            _cache.ThrowIfReleased();
            _cache.PrepareForTerminal();
            return _cache.measureSize;
        }

        [NowConsumer]
        public NowSdfBuilder Draw()
        {
            return Draw(_hasRect ? _rect : ReserveLayoutRect());
        }

        [NowConsumer]
        public NowSdfBuilder Draw(NowRect rect)
        {
            _cache.Draw(rect, _hasMask ? _mask : rect, _tint);
            return this;
        }

        /// <summary>
        /// Rasterizes this SDF scene into a cache-owned coverage texture and pushes
        /// it as an ambient mask. The scene must have been created with an explicit
        /// rect; layout callers should reserve a rect first or use
        /// <see cref="BeginMask(NowRect)"/>.
        /// </summary>
        [NowConsumer]
        public NowMaskScope BeginMask()
        {
            _cache.ThrowIfReleased();

            if (!_hasRect)
            {
                throw new InvalidOperationException(
                    "NowSdfBuilder.BeginMask() requires an explicit scene rect. " +
                    "Reserve a layout rect first and pass it to BeginMask(rect), or create the scene with NowSdf.Scene(rect).");
            }

            return BeginMask(_rect);
        }

        /// <summary>
        /// Rasterizes this SDF scene into a cache-owned coverage texture and pushes
        /// it as an ambient mask over <paramref name="rect"/>. Dispose the returned
        /// scope to restore the previous ambient mask.
        /// </summary>
        [NowConsumer]
        public NowMaskScope BeginMask(NowRect rect)
        {
            _cache.ThrowIfReleased();

            // Measured layout invokes drawing code once with all rendering
            // suppressed. Do not allocate or execute a render texture in that pass;
            // the real pass rebuilds this call-site cache before using it.
            if (NowLayout.isMeasurePass)
                return default;

            return _cache.BeginMask(rect, _hasMask ? _mask : rect, _tint, _maskResolutionScale);
        }

        NowRect ReserveLayoutRect()
        {
            var options = _options;
            _cache.PrepareForTerminal();
            Vector2 size = _cache.measureSize;

            if (!options.Has(NowLayoutOptions.Field.Width) && size.x > 0f)
                options = options.SetWidth(size.x);

            if (!options.Has(NowLayoutOptions.Field.Height) && size.y > 0f)
                options = options.SetHeight(size.y);

            return NowLayout.ReserveRect(options);
        }
    }

    sealed class NowSdfCache
    {
        readonly struct GraphUpload
        {
            public readonly int id;
            public readonly int start;
            public readonly int count;

            public GraphUpload(int id, int start, int count)
            {
                this.id = id;
                this.start = start;
                this.count = count;
            }
        }

        readonly struct OwnedMaterial
        {
            public readonly Material source;
            public readonly Material material;

            public OwnedMaterial(Material source, Material material)
            {
                this.source = source;
                this.material = material;
            }
        }

        readonly struct MaskRenderSignature : IEquatable<MaskRenderSignature>
        {
            readonly ulong _sceneHash;
            readonly Texture _sourceTexture;
            readonly uint _sourceTextureUpdateCount;
            readonly Material _materialTemplate;
            readonly Vector4 _effectiveTint;
            readonly Vector2 _localSize;
            readonly NowRect _localMask;
            readonly int _targetWidth;
            readonly int _targetHeight;

            public MaskRenderSignature(
                ulong sceneHash,
                Texture sourceTexture,
                uint sourceTextureUpdateCount,
                Material materialTemplate,
                Vector4 effectiveTint,
                Vector2 localSize,
                NowRect localMask,
                int targetWidth,
                int targetHeight)
            {
                _sceneHash = sceneHash;
                _sourceTexture = sourceTexture;
                _sourceTextureUpdateCount = sourceTextureUpdateCount;
                _materialTemplate = materialTemplate;
                _effectiveTint = effectiveTint;
                _localSize = localSize;
                _localMask = localMask;
                _targetWidth = targetWidth;
                _targetHeight = targetHeight;
            }

            public bool Equals(MaskRenderSignature other)
            {
                return _sceneHash == other._sceneHash &&
                    ReferenceEquals(_sourceTexture, other._sourceTexture) &&
                    _sourceTextureUpdateCount == other._sourceTextureUpdateCount &&
                    ReferenceEquals(_materialTemplate, other._materialTemplate) &&
                    _effectiveTint.Equals(other._effectiveTint) &&
                    _localSize.Equals(other._localSize) &&
                    _localMask == other._localMask &&
                    _targetWidth == other._targetWidth &&
                    _targetHeight == other._targetHeight;
            }

            public override bool Equals(object obj)
            {
                return obj is MaskRenderSignature other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _sceneHash.GetHashCode();
                    hash = hash * 397 ^ (!ReferenceEquals(_sourceTexture, null)
                        ? RuntimeHelpers.GetHashCode(_sourceTexture)
                        : 0);
                    hash = hash * 397 ^ _sourceTextureUpdateCount.GetHashCode();
                    hash = hash * 397 ^ (!ReferenceEquals(_materialTemplate, null)
                        ? RuntimeHelpers.GetHashCode(_materialTemplate)
                        : 0);
                    hash = hash * 397 ^ _effectiveTint.GetHashCode();
                    hash = hash * 397 ^ _localSize.GetHashCode();
                    hash = hash * 397 ^ _localMask.GetHashCode();
                    hash = hash * 397 ^ _targetWidth;
                    hash = hash * 397 ^ _targetHeight;
                    return hash;
                }
            }
        }

        static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");
        static readonly int _materialAbiProp = Shader.PropertyToID(NowSdf.MaterialAbiProperty);
        static readonly int _shapeCountProp = Shader.PropertyToID("_SdfShapeCount");
        static readonly int _layerCountProp = Shader.PropertyToID("_SdfLayerCount");
        static readonly int _featherProp = Shader.PropertyToID("_SdfFeather");
        static readonly int _canvasLayoutProp = Shader.PropertyToID("_NowCanvasLayout");
        static readonly int _data0Prop = Shader.PropertyToID("_SdfData0");
        static readonly int _data1Prop = Shader.PropertyToID("_SdfData1");
        static readonly int _data2Prop = Shader.PropertyToID("_SdfData2");
        static readonly int _shapeMetaProp = Shader.PropertyToID("_SdfShapeMeta");
        static readonly int _colorsProp = Shader.PropertyToID("_SdfColors");
        static readonly int _uvsProp = Shader.PropertyToID("_SdfUvs");
        static readonly int _layerData0Prop = Shader.PropertyToID("_SdfLayerData0");
        static readonly int _layerData1Prop = Shader.PropertyToID("_SdfLayerData1");
        static readonly int _outlineProp = Shader.PropertyToID("_SdfOutline");
        static readonly int _outlineColorProp = Shader.PropertyToID("_SdfOutlineColor");
        static readonly int _glowProp = Shader.PropertyToID("_SdfGlow");
        static readonly int _glowColorProp = Shader.PropertyToID("_SdfGlowColor");
        static readonly int _shadowProp = Shader.PropertyToID("_SdfShadow");
        static readonly int _shadowColorProp = Shader.PropertyToID("_SdfShadowColor");
        static readonly int _innerShadowProp = Shader.PropertyToID("_SdfInnerShadow");
        static readonly int _innerShadowColorProp = Shader.PropertyToID("_SdfInnerShadowColor");
        static readonly int _embossProp = Shader.PropertyToID("_SdfEmboss");
        static readonly int _contourProp = Shader.PropertyToID("_SdfContour");
        static readonly int _contourColorProp = Shader.PropertyToID("_SdfContourColor");
        static readonly int _contourMaskProp = Shader.PropertyToID("_SdfContourMask");
        static readonly int _warpProp = Shader.PropertyToID("_SdfWarp");
        static readonly int _maskOutputProp = Shader.PropertyToID("_SdfMaskOutput");

        static Material _builtInMaterialTemplate;

        readonly Vector4[] _data0 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _data1 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _data2 = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _shapeMeta = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _colors = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _uvs = new Vector4[NowSdf.MaxShapes];
        readonly Vector4[] _layerData0 = new Vector4[NowSdf.MaxLayers];
        readonly Vector4[] _layerData1 = new Vector4[NowSdf.MaxLayers];

        readonly List<NowSdfLayer> _layers = new List<NowSdfLayer>(4);
        readonly List<NowSdfGraph> _inlineGraphs = new List<NowSdfGraph>(4);
        readonly List<float> _rotationStack = new List<float>(4);
        readonly Dictionary<NowSdfGraph, GraphUpload> _graphUploads =
            new Dictionary<NowSdfGraph, GraphUpload>(8);
        readonly Dictionary<NowSdfGraph, NowSdfGraph> _preparedTextGraphs =
            new Dictionary<NowSdfGraph, NowSdfGraph>(8);
        readonly List<OwnedMaterial> _ownedMaterials = new List<OwnedMaterial>(2);
        readonly List<OwnedMaterial> _ownedMaskMaterials = new List<OwnedMaterial>(2);

        Material _material;
        Material _maskMaterial;
        Material _materialSource;
        Material _maskMaterialSource;
        Material _materialTemplate;
        int _materialTemplateAbi = NowSdf.MaterialAbiVersion;
        bool _syncMaterialTemplate;
        NowRenderer _maskRenderer;
        RenderTexture _maskTexture;
        ulong _uploadedHash;
        bool _hasUploadedHash;
        ulong _maskUploadedHash;
        bool _hasMaskUploadedHash;
        MaskRenderSignature _maskRenderSignature;
        bool _hasMaskRenderSignature;
        NowSdfGraph _activeGraph;
        int _inlineGraphCursor;
        NowSdfOperation _pendingOperation;
        float _pendingSmoothing;
        float _nextRotationDegrees;
        NowSdfOperation _activeLayerOperation;
        float _activeLayerSmoothing;
        float _feather;
        float _textDistanceMargin;
        Vector4 _outline;
        Vector4 _outlineColor;
        Vector4 _glow;
        Vector4 _glowColor;
        Vector4 _shadow;
        Vector4 _shadowColor;
        Vector4 _innerShadow;
        Vector4 _innerShadowColor;
        Vector4 _emboss;
        Vector4 _contour;
        Vector4 _contourColor;
        Vector4 _contourMask;
        Vector4 _warp;
        Texture _texture;
        NowSdfGraph _textureSourceGraph;
        bool _texturePinned;
        NowRect _bounds;
        bool _hasBounds;
        bool _terminalPrepared;

        bool _released;

        internal bool hasMaskTexture => _maskTexture != null;

        internal long maskTexturePixels => _maskTexture != null
            ? (long)_maskTexture.width * _maskTexture.height
            : 0L;

        public Vector2 measureSize => _hasBounds
            ? new Vector2(_bounds.xMax, _bounds.yMax)
            : _activeGraph != null
                ? _activeGraph.measureSize
                : Vector2.zero;

        public void Begin()
        {
            ThrowIfReleased();
            _layers.Clear();
            _graphUploads.Clear();
            _preparedTextGraphs.Clear();

            // Stable scene caches may have needed more terminal clones in an
            // earlier frame. Clear every retained slot so surplus graphs do not
            // keep font assets, dynamic atlases, or glyph metadata alive.
            for (int i = 0; i < _inlineGraphs.Count; ++i)
                _inlineGraphs[i].ResetForReuse();

            _inlineGraphCursor = 0;
            _activeGraph = RentInlineGraph();
            _pendingOperation = NowSdfOperation.Union;
            _pendingSmoothing = 0f;
            _nextRotationDegrees = 0f;
            _rotationStack.Clear();
            _activeLayerOperation = NowSdfOperation.Union;
            _activeLayerSmoothing = 0f;
            _feather = 0f;
            _textDistanceMargin = 0f;
            _outline = default;
            _outlineColor = default;
            _glow = default;
            _glowColor = default;
            _shadow = default;
            _shadowColor = default;
            _innerShadow = default;
            _innerShadowColor = default;
            _emboss = default;
            _contour = default;
            _contourColor = default;
            _contourMask = default;
            _warp = default;
            _texture = null;
            _textureSourceGraph = null;
            _texturePinned = false;
            _materialTemplate = null;
            _materialTemplateAbi = NowSdf.MaterialAbiVersion;
            _syncMaterialTemplate = true;
            _bounds = default;
            _hasBounds = false;
            _terminalPrepared = false;
        }

        NowSdfGraph RentInlineGraph()
        {
            if (_inlineGraphCursor == _inlineGraphs.Count)
                _inlineGraphs.Add(new NowSdfGraph());

            return _inlineGraphs[_inlineGraphCursor++].ResetForReuse();
        }

        public void Release()
        {
            if (_released)
                return;

            _released = true;
            _maskRenderer?.Dispose();
            _maskRenderer = null;
            ReleaseMaskTexture();

            ReleaseMaterials(_ownedMaskMaterials);
            ReleaseMaterials(_ownedMaterials);
            _maskMaterial = null;
            _maskMaterialSource = null;
            _material = null;
            _materialSource = null;
            _materialTemplate = null;
            _hasUploadedHash = false;
            _hasMaskUploadedHash = false;
            _layers.Clear();
            _graphUploads.Clear();
            _preparedTextGraphs.Clear();

            for (int i = 0; i < _inlineGraphs.Count; ++i)
                _inlineGraphs[i].ResetForReuse();

            _inlineGraphs.Clear();
            _activeGraph = null;
            _texture = null;
            _textureSourceGraph = null;
        }

        internal void ThrowIfReleased()
        {
            if (_released)
            {
                throw new ObjectDisposedException(
                    nameof(NowSdfBuilder),
                    "This SDF builder's cache was released. Create a new builder with NowSdf.Scene(...).");
            }
        }

        public void SetMaterial(Material material, bool syncPerFrame)
        {
            int abi = NowSdf.MaterialAbiVersion;
            if (material != null)
            {
                float declaredAbi = material.HasProperty(_materialAbiProp)
                    ? material.GetFloat(_materialAbiProp)
                    : 0f;
                bool finiteAbi = !float.IsNaN(declaredAbi) && !float.IsInfinity(declaredAbi);
                abi = finiteAbi ? Mathf.RoundToInt(declaredAbi) : 0;
                if (!finiteAbi || declaredAbi != abi ||
                    abi < NowSdf.MinimumMaterialAbiVersion ||
                    abi > NowSdf.MaterialAbiVersion)
                {
                    throw new ArgumentException(
                        $"SDF material '{material.name}' does not implement a supported material ABI. " +
                        $"Declare {NowSdf.MaterialAbiProperty} with an integer value from " +
                        $"{NowSdf.MinimumMaterialAbiVersion} through {NowSdf.MaterialAbiVersion} and " +
                        "implement that version's SDF scene data contract.",
                        nameof(material));
                }
            }

            _materialTemplate = material;
            _materialTemplateAbi = abi;
            _syncMaterialTemplate = syncPerFrame;
        }

        static void ReleaseMaterials(List<OwnedMaterial> materials)
        {
            for (int i = 0; i < materials.Count; ++i)
                ReleaseMaterial(materials[i].material);

            materials.Clear();
        }

        static void ReleaseMaterial(Material material)
        {
            if (material == null)
                return;

#if NOWUI_UGUI
            NowGraphic.ReleaseCachedMaterial(material);
#endif
            NowWorldGraphic.ReleaseCachedMaterial(material);

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);
        }

        public void SetColor(Vector4 color)
        {
            InvalidateTerminalPreparation();
            _activeGraph.SetColor(color);
        }

        public void UseColor()
        {
            InvalidateTerminalPreparation();
            _activeGraph.UseColor();
        }

        public void SetTexture(Texture texture)
        {
            InvalidateTerminalPreparation();

            if (_texture == null && texture != null)
            {
                _texture = texture;
                _textureSourceGraph = null;
                _texturePinned = true;
            }

            _activeGraph.SetTexture(texture);
        }

        public void UseTexture()
        {
            InvalidateTerminalPreparation();
            _activeGraph.UseTexture();
        }

        public void SetTextureUV(Vector4 uvRect)
        {
            InvalidateTerminalPreparation();
            _activeGraph.SetTextureUV(uvRect);
        }

        public void SetFeather(float feather)
        {
            InvalidateTerminalPreparation();
            _feather = Mathf.Max(0f, feather);
        }

        public void SetTextDistanceMargin(float margin)
        {
            InvalidateTerminalPreparation();
            _textDistanceMargin = float.IsNaN(margin) || float.IsInfinity(margin)
                ? 0f
                : Mathf.Max(0f, margin);
        }

        public void SetOutline(float width, Vector4 color, float softness)
        {
            InvalidateTerminalPreparation();
            _outline = new Vector4(Mathf.Max(0f, width), Mathf.Max(0f, softness), 0f, 0f);
            _outlineColor = color;
        }

        public void SetGlow(float radius, Vector4 color, float power)
        {
            InvalidateTerminalPreparation();
            _glow = new Vector4(Mathf.Max(0f, radius), Mathf.Max(0.0001f, power), 0f, 0f);
            _glowColor = color;
        }

        public void SetShadow(Vector2 offset, float softness, Vector4 color, float spread)
        {
            InvalidateTerminalPreparation();
            _shadow = new Vector4(offset.x, offset.y, Mathf.Max(0f, softness), Mathf.Max(0f, spread));
            _shadowColor = color;
        }

        public void SetInnerShadow(Vector2 offset, float softness, Vector4 color, float spread)
        {
            InvalidateTerminalPreparation();
            _innerShadow = new Vector4(offset.x, offset.y, Mathf.Max(0f, softness), Mathf.Max(0f, spread));
            _innerShadowColor = color;
        }

        public void SetEmboss(Vector2 lightDirection, float strength, float size)
        {
            InvalidateTerminalPreparation();

            if (lightDirection.sqrMagnitude <= 0.0001f)
                lightDirection = new Vector2(-0.55f, -0.8f);

            lightDirection.Normalize();
            _emboss = new Vector4(lightDirection.x, lightDirection.y, Mathf.Max(0.0001f, size), Mathf.Max(0f, strength));
        }

        public void SetContours(float spacing, float width, Vector4 color, float offset, int bandCount)
        {
            InvalidateTerminalPreparation();
            _contour = new Vector4(
                Mathf.Max(0.0001f, spacing),
                Mathf.Max(0f, width),
                offset,
                Mathf.Max(0, bandCount));
            _contourColor = color;
        }

        public void SetContourMask(Vector2 center, float radius, float softness)
        {
            _contourMask = new Vector4(center.x, center.y, Mathf.Max(0f, radius), Mathf.Max(0f, softness));
        }

        public void SetWarp(float amplitude, float scale, float speed, float seed)
        {
            _warp = new Vector4(Mathf.Max(0f, amplitude), Mathf.Max(0.0001f, scale), speed, seed);
        }

        public void SetOperation(NowSdfOperation operation, float smoothing)
        {
            _pendingOperation = operation;
            _pendingSmoothing = Mathf.Max(0f, smoothing);
        }

        public void SetNextRotation(float angleDegrees)
        {
            if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
                throw new ArgumentOutOfRangeException(nameof(angleDegrees), "SDF rotation angles must be finite.");

            _nextRotationDegrees = NowSdfGraph.NormalizeRotationDegrees(angleDegrees);
        }

        public void PushRotation(float angleDegrees)
        {
            if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
                throw new ArgumentOutOfRangeException(nameof(angleDegrees), "SDF rotation angles must be finite.");

            float parent = _rotationStack.Count > 0
                ? _rotationStack[_rotationStack.Count - 1]
                : 0f;
            _rotationStack.Add(NowSdfGraph.NormalizeRotationDegrees(parent + angleDegrees));
        }

        public void PopRotation()
        {
            if (_rotationStack.Count == 0)
                throw new InvalidOperationException("SDF PopRotation requires a matching PushRotation.");

            _rotationStack.RemoveAt(_rotationStack.Count - 1);
        }

        public void Graph(NowSdfGraph graph)
        {
            InvalidateTerminalPreparation();
            ThrowIfPendingRotationCannotApplyTo("Graph");

            if (graph == null || !graph.hasNodes)
                return;

            graph.ThrowIfRotationScopesOpen("Graph");

            FlushActiveGraph();
            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Graph,
                operation = ConsumePendingOperation(),
                smoothing = ConsumePendingSmoothing(),
                graph = graph
            });
        }

        public void Morph(NowSdfGraph from, NowSdfGraph to, float t)
        {
            InvalidateTerminalPreparation();
            ThrowIfPendingRotationCannotApplyTo("Morph");

            if (from == null || to == null || !from.hasNodes || !to.hasNodes)
                return;

            from.ThrowIfRotationScopesOpen("Morph");
            to.ThrowIfRotationScopesOpen("Morph");

            FlushActiveGraph();
            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Morph,
                operation = ConsumePendingOperation(),
                smoothing = ConsumePendingSmoothing(),
                graph = from,
                targetGraph = to,
                morph = Mathf.Clamp01(t)
            });
        }

        public void Circle(Vector2 center, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Circle(center, radius);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Box(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Box(rect);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void RoundedBox(NowRect rect, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).RoundedBox(rect, radius);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void RoundedBox(NowRect rect, Vector4 radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).RoundedBox(rect, radius);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void ChamferedBox(NowRect rect, float chamfer)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).ChamferedBox(rect, chamfer);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Ellipse(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Ellipse(rect);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Capsule(Vector2 from, Vector2 to, float radius)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Capsule(from, to, radius);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Capsule(NowRect rect)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Capsule(rect);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Line(Vector2 from, Vector2 to, float width)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Line(from, to, width);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Triangle(a, b, c);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Arc(Vector2 center, float radius, float thickness, float from, float sweep)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Arc(center, radius, thickness, from, sweep);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Pie(Vector2 center, float radius, float from, float sweep)
        {
            PrepareActivePrimitive();
            _activeGraph.SetOperation(_pendingOperation, _pendingSmoothing).SetNextRotationDegrees(EffectiveRotationDegrees()).Pie(center, radius, from, sweep);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Text(Vector2 position, string value, NowFontAsset font, float fontSize, NowFontStyle fontStyle, int tabSpaces)
        {
            PrepareActivePrimitive();
            _activeGraph
                .SetOperation(_pendingOperation, _pendingSmoothing)
                .SetNextRotationDegrees(EffectiveRotationDegrees())
                .Text(position, value, font, fontSize, fontStyle, tabSpaces);
            ResetPendingPrimitiveModifiers();
            Encapsulate(_activeGraph.measureSize);
        }

        public void Draw(NowRect rect, NowRect mask, Vector4 tint)
        {
            ThrowIfReleased();
            ThrowIfRotationScopesOpen("Draw");
            PrepareForTerminal();
            FlushActiveGraph();

            if (_layers.Count == 0)
                return;

            EnsureMaterialSupportsScene();

            var material = GetMaterial();

            if (material == null)
                return;

            Upload(material, ref _uploadedHash, ref _hasUploadedHash);
            Now.DrawSdf(rect, mask, material, tint);
        }

        public NowMaskScope BeginMask(NowRect rect, NowRect mask, Vector4 tint, float resolutionScale)
        {
            ThrowIfReleased();
            ThrowIfRotationScopesOpen("BeginMask");

            // Fail before material creation or RT execution when the ambient
            // texture-mask stack is already full.
            Now.EnsureCanPushTextureMask();
            PrepareForTerminal();
            FlushActiveGraph();

            if (_layers.Count == 0 || !IsFiniteRect(rect) || rect.isEmpty)
                return EmptyMask(rect);

            EnsureMaterialSupportsScene();

            var material = GetMaskMaterial();
            if (material == null)
                return EmptyMask(rect);

            NowRect transformedRect = Now.TransformScreenRect(rect);
            if (!IsFiniteRect(transformedRect) || transformedRect.isEmpty)
                return EmptyMask(rect);

            int width = PhysicalSize(transformedRect.width, resolutionScale);
            int height = PhysicalSize(transformedRect.height, resolutionScale);
            var target = GetMaskTexture(width, height);
            if (target == null)
                return EmptyMask(rect);

            ulong sceneHash = Upload(material, ref _maskUploadedHash, ref _hasMaskUploadedHash);
            var localRect = new NowRect(0f, 0f, rect.width, rect.height);
            var localMask = new NowRect(
                mask.x - rect.x,
                mask.y - rect.y,
                mask.width,
                mask.height);
            Texture sourceTexture = _texture ? _texture : null;
            uint sourceTextureUpdateCount = sourceTexture != null ? sourceTexture.updateCount : 0u;
            var signature = new MaskRenderSignature(
                sceneHash,
                sourceTexture,
                sourceTextureUpdateCount,
                _materialTemplate != null ? _materialTemplate : null,
                Now.ApplyCurrentColorMultiplier(tint),
                localRect.size,
                localMask,
                width,
                height);

            // RenderTexture-backed fills can be updated by pending GPU work which
            // has not necessarily advanced Texture.updateCount yet. Animated warp
            // reads shader _Time. Both cases must remain live rather than reusing
            // an apparently identical coverage image.
            bool dynamicCoverage = sourceTexture is RenderTexture ||
                (_warp.x > 0f && _warp.z != 0f) ||
                (_materialTemplate != null && _syncMaterialTemplate);
            bool reuseCoverage = !dynamicCoverage &&
                _hasMaskRenderSignature &&
                _maskRenderSignature.Equals(signature);

            if (!reuseCoverage)
            {
                _maskRenderer ??= new NowRenderer();

                // Capture without inherited context or flushing the caller's deferred
                // overlays. The SDF texture contains only this scene's coverage;
                // enclosing masks remain attached to child batches and are not
                // multiplied twice.
                using (_maskRenderer.Begin(localRect.size, flushOverlays: false))
                    Now.DrawSdfUnsnapped(localRect, localMask, material, tint);

                // Do not publish the signature before rendering succeeds. A failed
                // command buffer execution leaves the target contents undefined and
                // the next BeginMask must try again.
                InvalidateMaskCoverage();
                _maskRenderer.Render(target, clear: true, clearColor: Color.clear);
                _maskRenderSignature = signature;
                _hasMaskRenderSignature = true;
                NowSdf.RecordMaskRasterization();
            }

            return Now.Mask(NowMaskTexture.Red(target, rect));
        }

        static NowMaskScope EmptyMask(NowRect rect)
        {
            return Now.Mask(NowMaskTexture.Empty(IsFiniteRect(rect) ? rect : default));
        }

        void PrepareActivePrimitive()
        {
            InvalidateTerminalPreparation();

            if (_activeGraph.hasNodes || _layers.Count == 0)
                return;

            _activeLayerOperation = ConsumePendingOperation();
            _activeLayerSmoothing = ConsumePendingSmoothing();
        }

        void FlushActiveGraph()
        {
            if (!_activeGraph.hasNodes)
                return;

            AddLayer(new NowSdfLayer
            {
                kind = NowSdfLayerKind.Graph,
                operation = _layers.Count == 0 ? NowSdfOperation.Union : _activeLayerOperation,
                smoothing = _layers.Count == 0 ? 0f : _activeLayerSmoothing,
                graph = _activeGraph
            });

            var next = RentInlineGraph();
            next.CopyStyleFrom(_activeGraph);
            _activeGraph = next;
            _activeLayerOperation = NowSdfOperation.Union;
            _activeLayerSmoothing = 0f;
        }

        void AddLayer(NowSdfLayer layer)
        {
            if (_layers.Count >= NowSdf.MaxLayers)
                return;

            _layers.Add(layer);
            Encapsulate(layer.graph.measureSize);

            if (layer.targetGraph != null)
                Encapsulate(layer.targetGraph.measureSize);

            ClaimTexture(layer.graph);
            ClaimTexture(layer.targetGraph);

            ResetPendingPrimitiveModifiers();
        }

        void InvalidateTerminalPreparation()
        {
            if (_preparedTextGraphs.Count > 0)
                RestoreOriginalTextGraphReferences();

            _terminalPrepared = false;
            _preparedTextGraphs.Clear();
        }

        void RestoreOriginalTextGraphReferences()
        {
            for (int i = 0; i < _layers.Count; ++i)
            {
                NowSdfLayer layer = _layers[i];
                layer.graph = OriginalTextGraph(layer.graph);
                layer.targetGraph = OriginalTextGraph(layer.targetGraph);
                _layers[i] = layer;
            }

            _activeGraph = OriginalTextGraph(_activeGraph);
            _textureSourceGraph = OriginalTextGraph(_textureSourceGraph);

            if (!_texturePinned)
                ReconcileTexture();
        }

        NowSdfGraph OriginalTextGraph(NowSdfGraph graph)
        {
            if (graph == null)
                return null;

            foreach (var pair in _preparedTextGraphs)
            {
                if (ReferenceEquals(pair.Value, graph))
                    return pair.Key;
            }

            return graph;
        }

        float GetTextEffectBudget()
        {
            float budget = _textDistanceMargin;

            if (_outlineColor.w > 0f && _outline.x > 0f)
                budget = Mathf.Max(budget, _outline.x + _outline.y);

            if (_glowColor.w > 0f && _glow.x > 0f)
                budget = Mathf.Max(budget, _glow.x);

            if (_shadowColor.w > 0f)
                budget = Mathf.Max(budget, _shadow.z + _shadow.w);

            if (_innerShadowColor.w > 0f)
                budget = Mathf.Max(budget, _innerShadow.z + _innerShadow.w);

            if (_emboss.w > 0f)
                budget = Mathf.Max(budget, _emboss.z);

            // A finite contour stack has finite outward reach. Repeating
            // contours deliberately cover the complete scene and therefore have
            // no atlas-independent bound; leave them on the best field selected
            // by the other effects instead of forcing every text graph to its cap.
            if (_contourColor.w > 0f && _contour.y > 0f && _contour.w > 0f)
            {
                // contourDistance = fieldDistance + offset and the finite band
                // cutoff is symmetric about zero. Reserve both the deepest
                // inside and farthest outside endpoint; a signed subtraction of
                // offset would miss large positive offsets entirely.
                float contourReach =
                    Mathf.Abs(_contour.z) +
                    (_contour.w - 0.5f) * _contour.x +
                    _contour.y * 0.5f;
                budget = Mathf.Max(budget, contourReach);
            }

            if (budget > 0f)
            {
                // GetDynamicPixelRange already reserves one local pixel around
                // the requested reach. Feather values above one widen the shader
                // edge beyond that built-in guard.
                budget += Mathf.Max(0f, (_feather - 1f) * 0.5f);
            }

            return budget;
        }

        internal void PrepareForTerminal()
        {
            if (_terminalPrepared)
            {
                if (PreparedTextGraphsAreCurrent())
                    return;

                InvalidateTerminalPreparation();
            }

            PrepareTextGraphCopies();

            float budget = GetTextEffectBudget();
            NowFont textOwner = GetSceneTextOwner();
            int pixelRange = RequiredTextPixelRange(_activeGraph, textOwner, budget);

            for (int i = 0; i < _layers.Count; ++i)
            {
                NowSdfLayer layer = _layers[i];
                pixelRange = Mathf.Max(
                    pixelRange,
                    RequiredTextPixelRange(layer.graph, textOwner, budget));
                pixelRange = Mathf.Max(
                    pixelRange,
                    RequiredTextPixelRange(layer.targetGraph, textOwner, budget));
            }

            if (textOwner != null && pixelRange > 0)
            {
                int baseRange = BaseTextPixelRange(_activeGraph, textOwner);
                for (int i = 0; i < _layers.Count; ++i)
                {
                    baseRange = Mathf.Max(
                        baseRange,
                        BaseTextPixelRange(_layers[i].graph, textOwner));
                    baseRange = Mathf.Max(
                        baseRange,
                        BaseTextPixelRange(_layers[i].targetGraph, textOwner));
                }

                int attemptRange = pixelRange;
                bool prepared = false;

                while (attemptRange >= baseRange && attemptRange > 0)
                {
                    if (TryPrepareTextRange(
                        textOwner,
                        attemptRange,
                        attemptRange < pixelRange))
                    {
                        prepared = true;
                        break;
                    }

                    RestorePreparedTextGraphs();

                    if (attemptRange <= baseRange)
                        break;

                    attemptRange = PreviousTextPixelRange(attemptRange, baseRange);
                }

                if (!prepared)
                    RestorePreparedTextGraphs();
            }

            ReconcileTexture();
            RebuildSceneBounds();
            _terminalPrepared = true;
        }

        bool PreparedTextGraphsAreCurrent()
        {
            foreach (var pair in _preparedTextGraphs)
            {
                if (pair.Key.contentRevision != pair.Value.contentRevision ||
                    !pair.Value.TextAtlasIsCurrent())
                {
                    return false;
                }
            }

            return true;
        }

        static int PreviousTextPixelRange(int current, int baseRange)
        {
            int previous = Mathf.Max(1, baseRange);
            int tier = previous;

            while (tier < current)
            {
                previous = tier;
                long doubled = (long)tier * 2L;

                if (doubled >= current)
                    break;

                tier = doubled > int.MaxValue ? int.MaxValue : (int)doubled;
            }

            return Mathf.Max(baseRange, previous);
        }

        void PrepareTextGraphCopies()
        {
            NowSdfGraph textureSource = _textureSourceGraph;

            for (int i = 0; i < _layers.Count; ++i)
            {
                NowSdfLayer layer = _layers[i];
                layer.graph = PrepareTextGraph(layer.graph);
                layer.targetGraph = PrepareTextGraph(layer.targetGraph);
                _layers[i] = layer;
            }

            _activeGraph = PrepareTextGraph(_activeGraph);

            if (textureSource != null &&
                _preparedTextGraphs.TryGetValue(textureSource, out var preparedSource))
            {
                _textureSourceGraph = preparedSource;
            }
        }

        NowSdfGraph PrepareTextGraph(NowSdfGraph graph)
        {
            if (graph == null || !graph.hasText)
                return graph;

            if (_preparedTextGraphs.TryGetValue(graph, out var prepared))
                return prepared;

            prepared = RentInlineGraph();
            prepared.CopyFrom(graph);
            _preparedTextGraphs.Add(graph, prepared);
            return prepared;
        }

        void RestorePreparedTextGraphs()
        {
            foreach (var pair in _preparedTextGraphs)
                pair.Value.CopyFrom(pair.Key);
        }

        bool TryPrepareTextRange(NowFont owner, int pixelRange, bool allowDowngrade)
        {
            NowSdfGraph primary = GetPrimaryTextGraph(owner);

            if (primary == null)
                return true;

            if (!primary.TryEnsureTextPixelRange(
                pixelRange,
                null,
                out _,
                allowDowngrade))
            {
                return false;
            }

            Texture candidate = primary.texture;
            if (candidate == null)
                return false;

            if (!TryPrepareTextRange(
                    _activeGraph,
                    primary,
                    owner,
                    pixelRange,
                    candidate,
                    allowDowngrade))
            {
                return false;
            }

            for (int i = 0; i < _layers.Count; ++i)
            {
                if (!TryPrepareTextRange(
                        _layers[i].graph,
                        primary,
                        owner,
                        pixelRange,
                        candidate,
                        allowDowngrade) ||
                    !TryPrepareTextRange(
                        _layers[i].targetGraph,
                        primary,
                        owner,
                        pixelRange,
                        candidate,
                        allowDowngrade))
                {
                    return false;
                }
            }

            return true;
        }

        static bool TryPrepareTextRange(
            NowSdfGraph graph,
            NowSdfGraph primary,
            NowFont owner,
            int pixelRange,
            Texture requiredTexture,
            bool allowDowngrade)
        {
            if (graph == null ||
                ReferenceEquals(graph, primary) ||
                !graph.UsesTextOwner(owner))
            {
                return true;
            }

            return graph.TryEnsureTextPixelRange(
                pixelRange,
                requiredTexture,
                out _,
                allowDowngrade);
        }

        NowSdfGraph GetPrimaryTextGraph(NowFont owner)
        {
            if (_textureSourceGraph != null && _textureSourceGraph.UsesTextOwner(owner))
                return _textureSourceGraph;

            for (int i = 0; i < _layers.Count; ++i)
            {
                if (_layers[i].graph.UsesTextOwner(owner))
                    return _layers[i].graph;

                if (_layers[i].targetGraph != null &&
                    _layers[i].targetGraph.UsesTextOwner(owner))
                {
                    return _layers[i].targetGraph;
                }
            }

            return _activeGraph != null && _activeGraph.UsesTextOwner(owner)
                ? _activeGraph
                : null;
        }

        NowFont GetSceneTextOwner()
        {
            if (_texturePinned)
                return null;

            if (_textureSourceGraph != null)
            {
                return _textureSourceGraph.TryGetTextOwner(out var sourceOwner)
                    ? sourceOwner
                    : null;
            }

            for (int i = 0; i < _layers.Count; ++i)
            {
                if (_layers[i].graph.TryGetTextOwner(out var owner))
                    return owner;

                if (_layers[i].targetGraph != null &&
                    _layers[i].targetGraph.TryGetTextOwner(out owner))
                {
                    return owner;
                }
            }

            return _activeGraph != null && _activeGraph.TryGetTextOwner(out var activeOwner)
                ? activeOwner
                : null;
        }

        static int RequiredTextPixelRange(NowSdfGraph graph, NowFont owner, float budget)
        {
            return graph != null && graph.UsesTextOwner(owner)
                ? graph.RequiredTextPixelRange(budget)
                : 0;
        }

        static int BaseTextPixelRange(NowSdfGraph graph, NowFont owner)
        {
            return graph != null && graph.UsesTextOwner(owner)
                ? graph.BaseTextPixelRange()
                : 0;
        }

        void ClaimTexture(NowSdfGraph graph)
        {
            if (_texture != null || graph == null || graph.texture == null)
                return;

            _texture = graph.texture;
            _textureSourceGraph = graph;
            _texturePinned = false;
        }

        void ReconcileTexture()
        {
            if (_texturePinned && _texture != null)
                return;

            if (_textureSourceGraph != null && _textureSourceGraph.texture != null)
            {
                _texture = _textureSourceGraph.texture;
                return;
            }

            _texture = null;
            _textureSourceGraph = null;
            _texturePinned = false;

            for (int i = 0; i < _layers.Count && _texture == null; ++i)
            {
                ClaimTexture(_layers[i].graph);
                ClaimTexture(_layers[i].targetGraph);
            }

            ClaimTexture(_activeGraph);
        }

        void RebuildSceneBounds()
        {
            _bounds = default;
            _hasBounds = false;

            for (int i = 0; i < _layers.Count; ++i)
            {
                Encapsulate(_layers[i].graph.measureSize);

                if (_layers[i].targetGraph != null)
                    Encapsulate(_layers[i].targetGraph.measureSize);
            }

            if (_activeGraph != null && _activeGraph.hasNodes)
                Encapsulate(_activeGraph.measureSize);
        }

        void EnsureMaterialSupportsScene()
        {
            int requiredAbi = NowSdf.MinimumMaterialAbiVersion;
            for (int i = 0; i < _layers.Count; ++i)
            {
                var layer = _layers[i];
                requiredAbi = Math.Max(requiredAbi, layer.graph.requiredMaterialAbi);

                if (layer.targetGraph != null)
                    requiredAbi = Math.Max(requiredAbi, layer.targetGraph.requiredMaterialAbi);
            }

            if (_materialTemplateAbi >= requiredAbi)
                return;

            string materialName = _materialTemplate != null ? _materialTemplate.name : "built-in";
            throw new InvalidOperationException(
                $"SDF material '{materialName}' declares ABI {_materialTemplateAbi}, but this scene " +
                $"requires ABI {requiredAbi}. Update {NowSdf.MaterialAbiProperty} and include the " +
                $"matching NowSdfShaderV{requiredAbi}.cginc implementation.");
        }

        void ThrowIfRotationScopesOpen(string operation)
        {
            if (_rotationStack.Count > 0)
            {
                throw new InvalidOperationException(
                    $"SDF {operation} requires every PushRotation to have a matching PopRotation.");
            }

            _activeGraph?.ThrowIfRotationScopesOpen(operation);

            for (int i = 0; i < _layers.Count; ++i)
            {
                _layers[i].graph.ThrowIfRotationScopesOpen(operation);
                _layers[i].targetGraph?.ThrowIfRotationScopesOpen(operation);
            }
        }

        NowSdfOperation ConsumePendingOperation()
        {
            var operation = _layers.Count == 0 ? NowSdfOperation.Union : _pendingOperation;
            return operation;
        }

        float ConsumePendingSmoothing()
        {
            return _layers.Count == 0 ? 0f : _pendingSmoothing;
        }

        void ResetPendingPrimitiveModifiers()
        {
            _pendingOperation = NowSdfOperation.Union;
            _pendingSmoothing = 0f;
            _nextRotationDegrees = 0f;
        }

        float EffectiveRotationDegrees()
        {
            float scoped = _rotationStack.Count > 0
                ? _rotationStack[_rotationStack.Count - 1]
                : 0f;
            return NowSdfGraph.NormalizeRotationDegrees(scoped + _nextRotationDegrees);
        }

        void ThrowIfPendingRotationCannotApplyTo(string operand)
        {
            if (NowSdfGraph.RotationDegrees(EffectiveRotationDegrees()) == Vector2.zero)
                return;

            throw new InvalidOperationException(
                $"SDF rotation applies to analytic primitives and text runs; {operand} requires a layer or group transform.");
        }

        void Encapsulate(Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f)
                return;

            Encapsulate(new NowRect(0f, 0f, size.x, size.y));
        }

        void Encapsulate(NowRect rect)
        {
            if (rect.isEmpty)
                return;

            _bounds = _hasBounds ? _bounds.Union(rect) : rect;
            _hasBounds = true;
        }

        Material GetMaterial()
        {
            return GetMaterial(
                ref _material,
                ref _materialSource,
                _ownedMaterials,
                false,
                ref _hasUploadedHash);
        }

        Material GetMaskMaterial()
        {
            return GetMaterial(
                ref _maskMaterial,
                ref _maskMaterialSource,
                _ownedMaskMaterials,
                true,
                ref _hasMaskUploadedHash);
        }

        Material GetMaterial(
            ref Material material,
            ref Material source,
            List<OwnedMaterial> ownedMaterials,
            bool maskOutput,
            ref bool hasUploadedHash)
        {
            bool customTemplate = _materialTemplate != null;
            Material template = customTemplate
                ? _materialTemplate
                : GetBuiltInMaterialTemplate();
            bool sourceChanged = !ReferenceEquals(source, template);

            if (material != null &&
                !sourceChanged &&
                (!customTemplate || !_syncMaterialTemplate))
            {
                return material;
            }

            if (material == null || sourceChanged)
            {
                Material resolved = FindOwnedMaterial(ownedMaterials, template);
                bool retained = resolved != null;

                if (resolved == null && template != null)
                {
                    resolved = new Material(template);
                }
                else if (resolved == null)
                {
                    var shader = Shader.Find("NowUI/SDF Scene");

                    if (shader == null)
                        return null;

                    resolved = new Material(shader);
                }

                if (!retained)
                    ownedMaterials.Add(new OwnedMaterial(template, resolved));

                material = resolved;
                source = template;
                hasUploadedHash = false;

                if (maskOutput)
                    InvalidateMaskCoverage();
            }
            if (customTemplate && _syncMaterialTemplate)
            {
                // The template can contain arbitrary project-defined properties.
                // Copying all of them also overwrites the SDF arrays, so force the
                // standard ABI upload to run immediately afterwards.
                material.CopyPropertiesFromMaterial(template);
                hasUploadedHash = false;
            }

            material.name = maskOutput ? "Now SDF Mask" : "Now SDF Scene";
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetFloat(_maskOutputProp, maskOutput ? 1f : 0f);
            return material;
        }

        static Material FindOwnedMaterial(List<OwnedMaterial> materials, Material source)
        {
            for (int i = 0; i < materials.Count; ++i)
            {
                var entry = materials[i];

                if (!ReferenceEquals(entry.source, source))
                    continue;

                if (entry.material != null)
                    return entry.material;

                materials.RemoveAt(i);
                return null;
            }

            return null;
        }

        static Material GetBuiltInMaterialTemplate()
        {
            if (_builtInMaterialTemplate == null)
                _builtInMaterialTemplate = Resources.Load<Material>("NowUI/SdfMaterial");

            return _builtInMaterialTemplate;
        }

        RenderTexture GetMaskTexture(int width, int height)
        {
            if (_maskTexture != null && _maskTexture.width == width && _maskTexture.height == height)
            {
                if (_maskTexture.IsCreated())
                    return _maskTexture;

                // Render textures lose their contents across device/context loss.
                // Recreate the same object when possible so already-captured
                // descriptors keep a valid reference, but never trust its pixels.
                InvalidateMaskCoverage();

                if (_maskTexture.Create())
                    return _maskTexture;
            }

            ReleaseMaskTexture();

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;

            _maskTexture = new RenderTexture(
                width,
                height,
                0,
                format,
                RenderTextureReadWrite.Linear)
            {
                name = "Now SDF Mask Coverage",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };

            if (!_maskTexture.Create())
            {
                ReleaseMaskTexture();
                return null;
            }

            return _maskTexture;
        }

        void ReleaseMaskTexture()
        {
            InvalidateMaskCoverage();

            if (_maskTexture == null)
            {
                // Unity's destroyed-object null can leave a managed reference in
                // the field. Clear it before a replacement is assigned.
                _maskTexture = null;
                return;
            }

            Now.ReleaseTextureMaterials(_maskTexture);
            _maskTexture.Release();

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_maskTexture);
            else
                UnityEngine.Object.DestroyImmediate(_maskTexture);

            _maskTexture = null;
        }

        void InvalidateMaskCoverage()
        {
            _maskRenderSignature = default;
            _hasMaskRenderSignature = false;
        }

        static int PhysicalSize(float transformedUiSize, float resolutionScale)
        {
            float pixels = Now.UiUnitsToScreenPixels(transformedUiSize);
            if (float.IsNaN(pixels) || pixels <= 0f)
                return 1;

            int maximum = Mathf.Max(1, SystemInfo.maxTextureSize);
            double scaledPixels = (double)pixels * resolutionScale;

            if (double.IsNaN(scaledPixels) || scaledPixels <= 0d)
                return 1;

            if (double.IsPositiveInfinity(scaledPixels) || scaledPixels >= maximum)
                return maximum;

            return Mathf.Clamp((int)Math.Ceiling(scaledPixels), 1, maximum);
        }

        static bool IsFiniteRect(NowRect rect)
        {
            return IsFinite(rect.x) && IsFinite(rect.y) &&
                IsFinite(rect.width) && IsFinite(rect.height) &&
                IsFinite(rect.xMax) && IsFinite(rect.yMax);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        ulong Upload(Material material, ref ulong uploadedHash, ref bool hasUploadedHash)
        {
            // Upload can target both the normal and mask material for the same
            // built scene. Rebuild this per-upload lookup so a prior material
            // upload cannot make the second one report zero shapes.
            _graphUploads.Clear();
            int shapeCount = 0;
            int layerCount = Mathf.Min(_layers.Count, NowSdf.MaxLayers);

            for (int i = 0; i < layerCount; ++i)
            {
                var layer = _layers[i];
                GraphUpload graph = GetGraphUpload(layer.graph, ref shapeCount);
                GraphUpload target = layer.targetGraph != null
                    ? GetGraphUpload(layer.targetGraph, ref shapeCount)
                    : new GraphUpload(-1, -1, 0);

                _layerData0[i] = new Vector4(
                    graph.id,
                    i == 0 ? (float)NowSdfOperation.Union : (float)layer.operation,
                    i == 0 ? 0f : layer.smoothing,
                    (float)layer.kind);
                _layerData1[i] = new Vector4(
                    target.id,
                    layer.morph,
                    PackGraphRange(graph),
                    PackGraphRange(target));
            }

            ulong contentHash = ComputeUploadHash(shapeCount, layerCount);

            if (hasUploadedHash && contentHash == uploadedHash)
                return contentHash;

            uploadedHash = contentHash;
            hasUploadedHash = true;

            material.SetFloat(_shapeCountProp, shapeCount);
            material.SetFloat(_layerCountProp, layerCount);
            material.SetFloat(_featherProp, _feather);
            material.SetFloat(_canvasLayoutProp, 0f);
            material.SetTexture(_mainTexProp, _texture != null ? _texture : Texture2D.whiteTexture);
            material.SetVectorArray(_data0Prop, _data0);
            material.SetVectorArray(_data1Prop, _data1);
            material.SetVectorArray(_data2Prop, _data2);
            material.SetVectorArray(_shapeMetaProp, _shapeMeta);
            material.SetVectorArray(_colorsProp, _colors);
            material.SetVectorArray(_uvsProp, _uvs);
            material.SetVectorArray(_layerData0Prop, _layerData0);
            material.SetVectorArray(_layerData1Prop, _layerData1);
            material.SetVector(_outlineProp, _outline);
            material.SetVector(_outlineColorProp, _outlineColor);
            material.SetVector(_glowProp, _glow);
            material.SetVector(_glowColorProp, _glowColor);
            material.SetVector(_shadowProp, _shadow);
            material.SetVector(_shadowColorProp, _shadowColor);
            material.SetVector(_innerShadowProp, _innerShadow);
            material.SetVector(_innerShadowColorProp, _innerShadowColor);
            material.SetVector(_embossProp, _emboss);
            material.SetVector(_contourProp, _contour);
            material.SetVector(_contourColorProp, _contourColor);
            material.SetVector(_contourMaskProp, _contourMask);
            material.SetVector(_warpProp, _warp);
            return contentHash;
        }

        // Start and count are both in the inclusive 0..64 range. Packing them
        // into one small integer-valued float keeps the existing graph-id ABI and
        // uses the two previously-empty layer-vector components instead of adding
        // another uniform array. All possible values are represented exactly by
        // an IEEE-754 float.
        static float PackGraphRange(GraphUpload graph)
        {
            return graph.id >= 0 ? graph.start * 128 + graph.count : 0f;
        }

        /// <summary>
        /// 64-bit FNV-1a over everything Upload writes to the material: counts,
        /// the used range of the shape and layer arrays, the effect vectors and
        /// the texture identity. When it matches the last uploaded hash the
        /// material already holds identical values (each cache owns its own
        /// material instance and nothing else writes to it), so static scenes
        /// skip all SetVectorArray/SetVector traffic.
        /// </summary>
        ulong ComputeUploadHash(int shapeCount, int layerCount)
        {
            ulong hash = 1469598103934665603UL;
            hash = HashValue(hash, shapeCount);
            hash = HashValue(hash, layerCount);
            hash = HashValue(hash, _feather);
            hash = HashValue(hash, _texture != null ? _texture.GetEntityId().GetHashCode() : 0);

            for (int i = 0; i < shapeCount; ++i)
            {
                hash = HashValue(hash, _data0[i]);
                hash = HashValue(hash, _data1[i]);
                hash = HashValue(hash, _data2[i]);
                hash = HashValue(hash, _shapeMeta[i]);
                hash = HashValue(hash, _colors[i]);
                hash = HashValue(hash, _uvs[i]);
            }

            for (int i = 0; i < layerCount; ++i)
            {
                hash = HashValue(hash, _layerData0[i]);
                hash = HashValue(hash, _layerData1[i]);
            }

            hash = HashValue(hash, _outline);
            hash = HashValue(hash, _outlineColor);
            hash = HashValue(hash, _glow);
            hash = HashValue(hash, _glowColor);
            hash = HashValue(hash, _shadow);
            hash = HashValue(hash, _shadowColor);
            hash = HashValue(hash, _innerShadow);
            hash = HashValue(hash, _innerShadowColor);
            hash = HashValue(hash, _emboss);
            hash = HashValue(hash, _contour);
            hash = HashValue(hash, _contourColor);
            hash = HashValue(hash, _contourMask);
            hash = HashValue(hash, _warp);
            return hash;
        }

        static ulong HashValue(ulong hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 0x100000001B3UL;
            }
        }

        static ulong HashValue(ulong hash, float value)
        {
            unchecked
            {
                return (hash ^ (uint)value.GetHashCode()) * 0x100000001B3UL;
            }
        }

        static ulong HashValue(ulong hash, Vector4 value)
        {
            hash = HashValue(hash, value.x);
            hash = HashValue(hash, value.y);
            hash = HashValue(hash, value.z);
            hash = HashValue(hash, value.w);
            return hash;
        }

        GraphUpload GetGraphUpload(NowSdfGraph graph, ref int shapeCount)
        {
            if (_graphUploads.TryGetValue(graph, out GraphUpload upload))
                return upload;

            int graphId = _graphUploads.Count;
            int start = shapeCount;
            AppendGraph(graph, graphId, ref shapeCount);
            upload = new GraphUpload(graphId, start, shapeCount - start);
            _graphUploads[graph] = upload;
            return upload;
        }

        void AppendGraph(NowSdfGraph graph, int graphId, ref int shapeCount)
        {
            var nodes = graph.nodes;
            _texture ??= graph.texture;
            bool graphTextureCompatible =
                graph.texture != null &&
                _texture != null &&
                ReferenceEquals(graph.texture, _texture);

            for (int i = 0; i < nodes.Count && shapeCount < NowSdf.MaxShapes; ++i)
            {
                var node = nodes[i];

                // A scene exposes one _MainTex. Text construction already omits
                // incompatible fallback atlases within a graph; apply the same
                // rule across reusable graph layers instead of sampling another
                // graph's glyph UVs from the wrong page.
                if ((node.type == NowSdfShapeType.Glyph || node.useTexture) &&
                    !graphTextureCompatible)
                {
                    continue;
                }

                _data0[shapeCount] = new Vector4((float)node.type, (float)node.operation, node.smoothing, 0f);
                _data1[shapeCount] = node.data1;
                _data2[shapeCount] = node.data2;
                _shapeMeta[shapeCount] = new Vector4(
                    graphId,
                    node.useTexture ? 1f : 0f,
                    node.rotation.x,
                    node.rotation.y);
                _colors[shapeCount] = node.color;
                _uvs[shapeCount] = node.uv;
                ++shapeCount;
            }
        }
    }
}
