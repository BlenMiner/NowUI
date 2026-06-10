using System.Collections.Generic;
using UnityEngine;

namespace NowUIInternal
{
    /// <summary>
    /// Evaluates a Lottie composition at a point in time and tessellates it into
    /// triangles. Everything stays vector: shapes are transformed into final pixel
    /// space first and then flattened/tessellated, so output is crisp at any scale.
    ///
    /// The managed side evaluates the animation (keyframes, transforms, paints) and
    /// packs transformed bezier contours; the heavy geometry work (flatten, trim,
    /// scanline fill, AA fringes, strokes, matte clipping) runs in the nowui-vg
    /// native library when present, with a managed fallback otherwise.
    /// Main-thread only.
    /// </summary>
    public static class NowLottieRenderer
    {
        const float FLATTEN_TOLERANCE = 0.2f;

        const float AA_WIDTH = 0.75f;

        const float CIRCLE_TANGENT = 0.5519151f;

        /// <summary>
        /// Mesh subdivision step for per-vertex gradient evaluation. Scales with the
        /// gradient's extent: the piecewise-linear color error is relative to the
        /// gradient length, so large gradients don't need a fine absolute grid.
        /// </summary>
        static float GradientSpan(in NowLottiePaint paint)
        {
            if (!paint.isGradient)
                return 0f;

            float extent = Vector2.Distance(paint.gradientStart, paint.gradientEnd);
            return Mathf.Clamp(extent / 24f, 8f, 64f);
        }

        static readonly NowLottieBezierData _bezierScratch = new NowLottieBezierData();

        static readonly Stack<List<NowLottiePolyline>> _listPool = new Stack<List<NowLottiePolyline>>(8);

        static readonly Stack<NowLottieContourSet> _contourSetPool = new Stack<NowLottieContourSet>(8);

        static float[] _gradientScratch = new float[16];

        static bool _useNative;

        static List<NowLottiePolyline> GetPolylineList()
        {
            return _listPool.Count > 0 ? _listPool.Pop() : new List<NowLottiePolyline>(16);
        }

        static void ReleasePolylineList(List<NowLottiePolyline> list)
        {
            NowLottiePolylinePool.ReleaseAll(list);
            _listPool.Push(list);
        }

        static NowLottieContourSet GetContourSet()
        {
            var result = _contourSetPool.Count > 0 ? _contourSetPool.Pop() : new NowLottieContourSet();
            result.Clear();
            return result;
        }

        static void ReleaseContourSet(NowLottieContourSet set)
        {
            if (set != null)
                _contourSetPool.Push(set);
        }

        // ------------------------------------------------------------------
        // Tessellation cache
        //
        // Tessellation is position independent (geometry is built at origin and
        // offset on mesh append), so identical (animation, frame, size, tint) draws
        // reuse the buffer: paused animations, duplicated icons and displays running
        // faster than the animation cost almost nothing.
        // ------------------------------------------------------------------

        sealed class CacheEntry
        {
            public NowLottieComposition composition;

            public float frame, width, height;

            public bool preserveAspect;

            public Vector4 tint;

            public readonly NowLottieDrawBuffer buffer = new NowLottieDrawBuffer();

            public int stamp = -1;
        }

        const int CACHE_SIZE = 8;

        static readonly CacheEntry[] _cache = CreateCache();

        static int _cacheStamp;

        static CacheEntry[] CreateCache()
        {
            var cache = new CacheEntry[CACHE_SIZE];

            for (int i = 0; i < CACHE_SIZE; ++i)
                cache[i] = new CacheEntry();

            return cache;
        }

        /// <summary>
        /// Returns a tessellated buffer for the requested frame, reusing a cached one
        /// when an identical draw happened recently. The buffer is built at origin —
        /// offset it when appending to a mesh. The returned buffer is owned by the
        /// cache and only valid until the next RenderCached call.
        /// </summary>
        public static NowLottieDrawBuffer RenderCached(
            NowLottieComposition composition,
            float frame,
            float width,
            float height,
            bool preserveAspect,
            Vector4 tint)
        {
            ++_cacheStamp;

            CacheEntry oldest = _cache[0];

            for (int i = 0; i < CACHE_SIZE; ++i)
            {
                var entry = _cache[i];

                if (entry.stamp >= 0 &&
                    ReferenceEquals(entry.composition, composition) &&
                    entry.frame == frame &&
                    entry.width == width &&
                    entry.height == height &&
                    entry.preserveAspect == preserveAspect &&
                    entry.tint == tint)
                {
                    entry.stamp = _cacheStamp;
                    return entry.buffer;
                }

                if (entry.stamp < oldest.stamp)
                    oldest = entry;
            }

            oldest.composition = composition;
            oldest.frame = frame;
            oldest.width = width;
            oldest.height = height;
            oldest.preserveAspect = preserveAspect;
            oldest.tint = tint;
            oldest.stamp = _cacheStamp;

            Render(composition, frame, new Vector4(0f, 0f, width, height), preserveAspect, tint, oldest.buffer);
            return oldest.buffer;
        }

        /// <summary>Drops all cached tessellations (e.g. after asset reimports).</summary>
        public static void ClearCache()
        {
            for (int i = 0; i < CACHE_SIZE; ++i)
            {
                _cache[i].composition = null;
                _cache[i].stamp = -1;
                _cache[i].buffer.Clear();
            }

            _cacheStamp = 0;
        }

        public static float TimeToFrame(NowLottieComposition composition, float time, bool loop)
        {
            float durationFrames = composition.durationFrames;

            if (durationFrames <= 0f)
                return composition.inPoint;

            float frame = time * composition.frameRate;

            frame = loop
                ? Mathf.Repeat(frame, durationFrames)
                : Mathf.Clamp(frame, 0f, durationFrames - 0.0001f);

            return composition.inPoint + frame;
        }

        /// <summary>
        /// Tessellates the composition at <paramref name="frame"/> fitted into
        /// <paramref name="rect"/> (x, y, width, height in UI space, y down).
        /// </summary>
        public static bool Render(
            NowLottieComposition composition,
            float frame,
            Vector4 rect,
            bool preserveAspect,
            Vector4 tint,
            NowLottieDrawBuffer buffer)
        {
            buffer.Clear();

            if (composition == null || composition.width <= 0f || composition.height <= 0f)
                return false;

            if (rect.z <= 0f || rect.w <= 0f || tint.w <= 0f)
                return false;

            float scaleX = rect.z / composition.width;
            float scaleY = rect.w / composition.height;
            float offsetX = rect.x;
            float offsetY = rect.y;

            if (preserveAspect)
            {
                float scale = Mathf.Min(scaleX, scaleY);
                offsetX += (rect.z - composition.width * scale) * 0.5f;
                offsetY += (rect.w - composition.height * scale) * 0.5f;
                scaleX = scaleY = scale;
            }

            var rootMatrix = NowMatrix2D.Mul(
                NowMatrix2D.Translate(offsetX, offsetY),
                NowMatrix2D.Scale(scaleX, scaleY));

            frame = Mathf.Clamp(frame, composition.inPoint, Mathf.Max(composition.inPoint, composition.outPoint - 0.0001f));

            _useNative = NowLottieNative.available;

            if (_useNative)
                NowLottieNative.Begin(FLATTEN_TOLERANCE, AA_WIDTH);

            RenderLayerList(composition, composition.layers, frame, rootMatrix, tint, 1f, buffer, null, null, false, 0);

            if (_useNative)
                return NowLottieNative.Finish(buffer);

            return buffer.indices.count > 0;
        }

        // ------------------------------------------------------------------
        // Layers
        // ------------------------------------------------------------------

        static void RenderLayerList(
            NowLottieComposition composition,
            List<NowLottieLayer> layers,
            float compFrame,
            in NowMatrix2D rootMatrix,
            Vector4 tint,
            float alpha,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert,
            int depth)
        {
            if (depth > 8)
                return;

            // Lottie stores the top-most layer first; render bottom-up.
            for (int i = layers.Count - 1; i >= 0; --i)
            {
                var layer = layers[i];

                if (layer.isMatteSource || !layer.IsActive(compFrame))
                    continue;

                if (layer.type != NowLottieLayerType.Shape &&
                    layer.type != NowLottieLayerType.Solid &&
                    layer.type != NowLottieLayerType.Precomp)
                {
                    continue;
                }

                float localFrame = layer.ToLocalFrame(compFrame);
                float layerAlpha = alpha * layer.transform.EvaluateOpacity(localFrame);

                if (layerAlpha <= 0.0005f)
                    continue;

                var matrix = ResolveLayerMatrix(layers, layer, compFrame, rootMatrix);

                // Track mattes: the matte source is the previous layer in the array.
                var layerClipSet = clipSet;
                var layerClipPolylines = clipPolylines;
                bool layerClipInvert = clipInvert;
                NowLottieContourSet matteSet = null;
                List<NowLottiePolyline> mattePolylines = null;
                bool skipLayer = false;

                if (layer.matteMode > 0 && i > 0)
                {
                    var matteLayer = layers[i - 1];
                    bool inverted = layer.matteMode == 2 || layer.matteMode == 4;

                    if (matteLayer.IsActive(compFrame))
                    {
                        matteSet = GetContourSet();
                        var matteMatrix = ResolveLayerMatrix(layers, matteLayer, compFrame, rootMatrix);
                        CollectLayerContours(matteLayer, matteLayer.ToLocalFrame(compFrame), matteMatrix, matteSet);

                        if (!matteSet.isEmpty)
                        {
                            // Nested mattes are rare; the innermost matte wins.
                            layerClipSet = matteSet;
                            layerClipInvert = inverted;

                            if (!_useNative)
                            {
                                mattePolylines = GetPolylineList();
                                NowLottieTessellator.FlattenPackedContours(matteSet, FLATTEN_TOLERANCE, mattePolylines);
                                layerClipPolylines = mattePolylines;
                            }
                        }
                        else if (!inverted)
                        {
                            skipLayer = true;
                        }
                    }
                    else if (!inverted)
                    {
                        skipLayer = true; // alpha matte with nothing in it hides the layer
                    }
                }

                if (!skipLayer)
                {
                    switch (layer.type)
                    {
                        case NowLottieLayerType.Shape:
                            if (layer.shapes != null)
                                RenderShapeItems(layer.shapes, localFrame, matrix, tint, layerAlpha, buffer, layerClipSet, layerClipPolylines, layerClipInvert);
                            break;

                        case NowLottieLayerType.Solid:
                            RenderSolid(layer, matrix, tint, layerAlpha, buffer, layerClipSet, layerClipPolylines, layerClipInvert);
                            break;

                        case NowLottieLayerType.Precomp:
                            if (!string.IsNullOrEmpty(layer.refId) &&
                                composition.precomps.TryGetValue(layer.refId, out var precompLayers))
                            {
                                float precompFrame = localFrame;

                                if (layer.timeRemap != null)
                                    precompFrame = layer.timeRemap.EvaluateFloat(localFrame) * composition.frameRate;

                                RenderLayerList(
                                    composition,
                                    precompLayers,
                                    precompFrame,
                                    matrix,
                                    tint,
                                    layerAlpha,
                                    buffer,
                                    layerClipSet,
                                    layerClipPolylines,
                                    layerClipInvert,
                                    depth + 1);
                            }
                            break;
                    }
                }

                if (mattePolylines != null)
                    ReleasePolylineList(mattePolylines);

                if (matteSet != null)
                    ReleaseContourSet(matteSet);
            }
        }

        static NowMatrix2D ResolveLayerMatrix(
            List<NowLottieLayer> layers,
            NowLottieLayer layer,
            float compFrame,
            in NowMatrix2D rootMatrix)
        {
            var matrix = layer.transform.EvaluateMatrix(layer.ToLocalFrame(compFrame));

            int parentIndex = layer.parent;
            int guard = 0;

            while (parentIndex >= 0 && guard++ < 64)
            {
                NowLottieLayer parent = null;

                for (int i = 0; i < layers.Count; ++i)
                {
                    if (layers[i].index == parentIndex)
                    {
                        parent = layers[i];
                        break;
                    }
                }

                if (parent == null)
                    break;

                matrix = NowMatrix2D.Mul(parent.transform.EvaluateMatrix(parent.ToLocalFrame(compFrame)), matrix);
                parentIndex = parent.parent;
            }

            return NowMatrix2D.Mul(rootMatrix, matrix);
        }

        static void RenderSolid(
            NowLottieLayer layer,
            in NowMatrix2D matrix,
            Vector4 tint,
            float alpha,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert)
        {
            if (layer.solidWidth <= 0f || layer.solidHeight <= 0f)
                return;

            BuildRect(
                new Vector2(layer.solidWidth * 0.5f, layer.solidHeight * 0.5f),
                new Vector2(layer.solidWidth, layer.solidHeight),
                0f,
                _bezierScratch);

            var set = GetContourSet();
            set.Pack(_bezierScratch, matrix);

            var color = layer.solidColor;
            var paint = NowLottiePaint.Solid(new Vector4(
                color.x * tint.x,
                color.y * tint.y,
                color.z * tint.z,
                color.w * alpha * tint.w));

            FillContours(set, paint, false, default, buffer, clipSet, clipPolylines, clipInvert);
            ReleaseContourSet(set);
        }

        // ------------------------------------------------------------------
        // Shape items
        // ------------------------------------------------------------------

        static void RenderShapeItems(
            List<NowLottieShapeItem> items,
            float frame,
            in NowMatrix2D matrix,
            Vector4 tint,
            float alpha,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert)
        {
            // Items are listed top-most first; render bottom-up so earlier items
            // composite on top, and apply each style to the geometry above it.
            // Styles usually share the exact same geometry (fill + stroke pairs), so
            // the packed contours are cached between them.
            NowLottieContourSet contourSet = null;
            int contourSourceCount = -1;
            var trim = default(NowLottieTrimInfo);
            bool trimEvaluated = false;

            for (int i = items.Count - 1; i >= 0; --i)
            {
                var item = items[i];

                if (item.hidden)
                    continue;

                if (item is NowLottieGroup group)
                {
                    var groupMatrix = matrix;
                    float groupAlpha = alpha;

                    if (group.transform != null)
                    {
                        groupMatrix = NowMatrix2D.Mul(matrix, group.transform.EvaluateMatrix(frame));
                        groupAlpha *= group.transform.EvaluateOpacity(frame);
                    }

                    if (groupAlpha > 0.0005f)
                        RenderShapeItems(group.items, frame, groupMatrix, tint, groupAlpha, buffer, clipSet, clipPolylines, clipInvert);

                    continue;
                }

                bool isFill = item is NowLottieFill || item is NowLottieGradientFill;
                bool isStroke = item is NowLottieStroke || item is NowLottieGradientStroke;

                if (!isFill && !isStroke)
                    continue;

                int sourceCount = CountGeometrySources(items, i);

                if (contourSet == null || sourceCount != contourSourceCount)
                {
                    if (contourSet != null)
                        ReleaseContourSet(contourSet);

                    contourSet = GetContourSet();
                    CollectContours(items, i, frame, matrix, contourSet);
                    contourSourceCount = sourceCount;
                }

                if (!trimEvaluated)
                {
                    trim = EvaluateTrim(items, frame);
                    trimEvaluated = true;
                }

                if (!contourSet.isEmpty)
                {
                    if (isFill)
                        PaintFill(item, contourSet, trim, frame, matrix, tint, alpha, buffer, clipSet, clipPolylines, clipInvert);
                    else
                        PaintStroke(item, contourSet, trim, frame, matrix, tint, alpha, buffer, clipSet, clipPolylines, clipInvert);
                }
            }

            if (contourSet != null)
                ReleaseContourSet(contourSet);
        }

        static int CountGeometrySources(List<NowLottieShapeItem> items, int beforeIndex)
        {
            int count = 0;

            for (int i = 0; i < beforeIndex; ++i)
            {
                var item = items[i];

                if (item.hidden)
                    continue;

                if (item is NowLottiePathShape || item is NowLottieEllipse ||
                    item is NowLottieRectShape || item is NowLottiePolystar ||
                    item is NowLottieGroup)
                {
                    ++count;
                }
            }

            return count;
        }

        static void CollectContours(
            List<NowLottieShapeItem> items,
            int beforeIndex,
            float frame,
            in NowMatrix2D matrix,
            NowLottieContourSet output)
        {
            for (int i = 0; i < beforeIndex; ++i)
            {
                var item = items[i];

                if (item.hidden)
                    continue;

                switch (item)
                {
                    case NowLottiePathShape path:
                        path.shape.Evaluate(frame, _bezierScratch);
                        output.Pack(_bezierScratch, matrix);
                        break;

                    case NowLottieEllipse ellipse:
                        BuildEllipse(ellipse.position.EvaluateVector2(frame), ellipse.size.EvaluateVector2(frame), _bezierScratch);
                        output.Pack(_bezierScratch, matrix);
                        break;

                    case NowLottieRectShape rectShape:
                    {
                        float roundness = rectShape.roundness?.EvaluateFloat(frame) ?? 0f;
                        BuildRect(rectShape.position.EvaluateVector2(frame), rectShape.size.EvaluateVector2(frame), roundness, _bezierScratch);
                        output.Pack(_bezierScratch, matrix);
                        break;
                    }

                    case NowLottiePolystar polystar:
                        BuildPolystar(polystar, frame, _bezierScratch);
                        output.Pack(_bezierScratch, matrix);
                        break;

                    case NowLottieGroup group:
                    {
                        var groupMatrix = matrix;

                        if (group.transform != null)
                            groupMatrix = NowMatrix2D.Mul(matrix, group.transform.EvaluateMatrix(frame));

                        CollectContours(group.items, group.items.Count, frame, groupMatrix, output);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates the group's trim path. Multiple trims in one group are extremely
        /// rare; only the first one is honored.
        /// </summary>
        static NowLottieTrimInfo EvaluateTrim(List<NowLottieShapeItem> items, float frame)
        {
            for (int i = 0; i < items.Count; ++i)
            {
                if (items[i] is NowLottieTrim trim && !trim.hidden)
                {
                    var result = new NowLottieTrimInfo
                    {
                        active = true,
                        start = trim.start.EvaluateFloat(frame) * 0.01f,
                        end = trim.end.EvaluateFloat(frame) * 0.01f,
                        offset = (trim.offset?.EvaluateFloat(frame) ?? 0f) / 360f,
                        individual = trim.mode == 2
                    };

                    // A full-range trim is a no-op; skip it so fills keep their fringe
                    // and the native call stays trivial.
                    if (result.start <= 0f && result.end >= 1f && result.offset == 0f)
                        result.active = false;

                    return result;
                }
            }

            return default;
        }

        // ------------------------------------------------------------------
        // Painting
        // ------------------------------------------------------------------

        static void PaintFill(
            NowLottieShapeItem item,
            NowLottieContourSet contours,
            in NowLottieTrimInfo trim,
            float frame,
            in NowMatrix2D matrix,
            Vector4 tint,
            float alpha,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert)
        {
            NowLottiePaint paint;
            bool evenOdd;

            if (item is NowLottieFill fill)
            {
                float opacity = (fill.opacity?.EvaluateFloat(frame) ?? 100f) * 0.01f;
                var color = NormalizeColor(fill.color.EvaluateVector4(frame));
                evenOdd = fill.fillRule == 2;

                paint = NowLottiePaint.Solid(new Vector4(
                    color.x * tint.x,
                    color.y * tint.y,
                    color.z * tint.z,
                    color.w * Mathf.Clamp01(opacity) * alpha * tint.w));
            }
            else
            {
                var gradientFill = (NowLottieGradientFill)item;
                float opacity = (gradientFill.opacity?.EvaluateFloat(frame) ?? 100f) * 0.01f;
                evenOdd = gradientFill.fillRule == 2;
                paint = BuildGradientPaint(gradientFill.gradient, frame, matrix, tint, Mathf.Clamp01(opacity) * alpha);
            }

            if (PaintAlphaIsZero(paint))
                return;

            FillContours(contours, paint, evenOdd, trim, buffer, clipSet, clipPolylines, clipInvert);
        }

        static void FillContours(
            NowLottieContourSet contours,
            in NowLottiePaint paint,
            bool evenOdd,
            in NowLottieTrimInfo trim,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert)
        {
            if (contours.isEmpty)
                return;

            float gradientSpan = GradientSpan(paint);

            if (_useNative)
            {
                NowLottieNative.Fill(contours, clipSet, clipInvert, paint, gradientSpan, evenOdd, trim);
                return;
            }

            var polylines = GetPolylineList();
            NowLottieTessellator.FlattenPackedContours(contours, FLATTEN_TOLERANCE, polylines);

            if (trim.active)
                NowLottieTessellator.ApplyTrim(polylines, trim.start, trim.end, trim.offset, trim.individual);

            if (polylines.Count > 0)
            {
                NowLottieTessellator.TessellateFill(polylines, clipPolylines, clipInvert, evenOdd, paint, buffer, AA_WIDTH, gradientSpan);
                NowLottieTessellator.EmitFillFringe(polylines, clipPolylines, clipInvert, paint, buffer, AA_WIDTH);
            }

            ReleasePolylineList(polylines);
        }

        static void PaintStroke(
            NowLottieShapeItem item,
            NowLottieContourSet contours,
            in NowLottieTrimInfo trim,
            float frame,
            in NowMatrix2D matrix,
            Vector4 tint,
            float alpha,
            NowLottieDrawBuffer buffer,
            NowLottieContourSet clipSet,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert)
        {
            NowLottiePaint paint;
            float width;
            int cap;
            int join;

            if (item is NowLottieStroke stroke)
            {
                float opacity = (stroke.opacity?.EvaluateFloat(frame) ?? 100f) * 0.01f;
                var color = NormalizeColor(stroke.color.EvaluateVector4(frame));

                paint = NowLottiePaint.Solid(new Vector4(
                    color.x * tint.x,
                    color.y * tint.y,
                    color.z * tint.z,
                    color.w * Mathf.Clamp01(opacity) * alpha * tint.w));

                width = stroke.width.EvaluateFloat(frame);
                cap = stroke.cap;
                join = stroke.join;
            }
            else
            {
                var gradientStroke = (NowLottieGradientStroke)item;
                float opacity = (gradientStroke.opacity?.EvaluateFloat(frame) ?? 100f) * 0.01f;
                paint = BuildGradientPaint(gradientStroke.gradient, frame, matrix, tint, Mathf.Clamp01(opacity) * alpha);
                width = gradientStroke.width.EvaluateFloat(frame);
                cap = gradientStroke.cap;
                join = gradientStroke.join;
            }

            width *= matrix.MeanScale();

            if (width <= 0f || PaintAlphaIsZero(paint))
                return;

            if (_useNative)
            {
                NowLottieNative.Stroke(contours, clipSet, clipInvert, paint, width, cap, join, trim);
                return;
            }

            var polylines = GetPolylineList();
            NowLottieTessellator.FlattenPackedContours(contours, FLATTEN_TOLERANCE, polylines);

            if (trim.active)
                NowLottieTessellator.ApplyTrim(polylines, trim.start, trim.end, trim.offset, trim.individual);

            if (clipPolylines != null)
            {
                var clipped = GetPolylineList();
                NowLottieTessellator.ClipPolylines(polylines, clipPolylines, clipInvert, clipped);
                NowLottieTessellator.EmitStroke(clipped, width, cap, join, paint, buffer, AA_WIDTH);
                ReleasePolylineList(clipped);
            }
            else
            {
                NowLottieTessellator.EmitStroke(polylines, width, cap, join, paint, buffer, AA_WIDTH);
            }

            ReleasePolylineList(polylines);
        }

        static NowLottiePaint BuildGradientPaint(
            NowLottieGradient gradient,
            float frame,
            in NowMatrix2D matrix,
            Vector4 tint,
            float alphaMultiplier)
        {
            var stopsProperty = gradient.stops;
            int dimensions = Mathf.Max(stopsProperty?.dimensions ?? 0, gradient.colorStopCount * 4);

            if (_gradientScratch.Length < dimensions)
                _gradientScratch = new float[Mathf.NextPowerOfTwo(dimensions)];

            stopsProperty?.EvaluateInto(frame, _gradientScratch);

            return new NowLottiePaint
            {
                isGradient = true,
                color = tint,
                gradientType = gradient.type,
                gradientStart = matrix.Transform(gradient.start.EvaluateVector2(frame)),
                gradientEnd = matrix.Transform(gradient.end.EvaluateVector2(frame)),
                gradientStops = _gradientScratch,
                gradientStopDataLength = dimensions,
                colorStopCount = gradient.colorStopCount > 0 ? gradient.colorStopCount : dimensions / 4,
                alphaMultiplier = alphaMultiplier
            };
        }

        static bool PaintAlphaIsZero(in NowLottiePaint paint)
        {
            if (paint.isGradient)
                return paint.alphaMultiplier * paint.color.w <= 0.0005f;

            return paint.color.w <= 0.0005f;
        }

        static Vector4 NormalizeColor(Vector4 color)
        {
            // Legacy exports store colors as 0..255.
            if (color.x > 1.001f || color.y > 1.001f || color.z > 1.001f)
                return new Vector4(color.x / 255f, color.y / 255f, color.z / 255f, color.w > 1.001f ? color.w / 255f : color.w);

            return color;
        }

        // ------------------------------------------------------------------
        // Matte geometry
        // ------------------------------------------------------------------

        static void CollectLayerContours(
            NowLottieLayer layer,
            float localFrame,
            in NowMatrix2D matrix,
            NowLottieContourSet output)
        {
            if (layer.type == NowLottieLayerType.Solid)
            {
                if (layer.solidWidth <= 0f || layer.solidHeight <= 0f)
                    return;

                BuildRect(
                    new Vector2(layer.solidWidth * 0.5f, layer.solidHeight * 0.5f),
                    new Vector2(layer.solidWidth, layer.solidHeight),
                    0f,
                    _bezierScratch);

                output.Pack(_bezierScratch, matrix);
                return;
            }

            if (layer.shapes != null)
                CollectContours(layer.shapes, layer.shapes.Count, localFrame, matrix, output);
        }

        // ------------------------------------------------------------------
        // Parametric shapes
        // ------------------------------------------------------------------

        static void BuildEllipse(Vector2 center, Vector2 size, NowLottieBezierData destination)
        {
            float radiusX = size.x * 0.5f;
            float radiusY = size.y * 0.5f;
            float tangentX = radiusX * CIRCLE_TANGENT;
            float tangentY = radiusY * CIRCLE_TANGENT;

            destination.EnsureCapacity(4);
            destination.count = 4;
            destination.closed = true;

            destination.vertices[0] = center + new Vector2(0f, -radiusY);
            destination.tangentsIn[0] = new Vector2(-tangentX, 0f);
            destination.tangentsOut[0] = new Vector2(tangentX, 0f);

            destination.vertices[1] = center + new Vector2(radiusX, 0f);
            destination.tangentsIn[1] = new Vector2(0f, -tangentY);
            destination.tangentsOut[1] = new Vector2(0f, tangentY);

            destination.vertices[2] = center + new Vector2(0f, radiusY);
            destination.tangentsIn[2] = new Vector2(tangentX, 0f);
            destination.tangentsOut[2] = new Vector2(-tangentX, 0f);

            destination.vertices[3] = center + new Vector2(-radiusX, 0f);
            destination.tangentsIn[3] = new Vector2(0f, tangentY);
            destination.tangentsOut[3] = new Vector2(0f, -tangentY);
        }

        static void BuildRect(Vector2 center, Vector2 size, float roundness, NowLottieBezierData destination)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float radius = Mathf.Min(roundness, Mathf.Min(halfWidth, halfHeight));

            destination.closed = true;

            if (radius <= 0.0001f)
            {
                destination.EnsureCapacity(4);
                destination.count = 4;

                destination.vertices[0] = center + new Vector2(halfWidth, -halfHeight);
                destination.vertices[1] = center + new Vector2(halfWidth, halfHeight);
                destination.vertices[2] = center + new Vector2(-halfWidth, halfHeight);
                destination.vertices[3] = center + new Vector2(-halfWidth, -halfHeight);

                for (int i = 0; i < 4; ++i)
                {
                    destination.tangentsIn[i] = Vector2.zero;
                    destination.tangentsOut[i] = Vector2.zero;
                }

                return;
            }

            float tangent = radius * CIRCLE_TANGENT;

            destination.EnsureCapacity(8);
            destination.count = 8;

            // Clockwise from the end of the top edge (matching AE's draw direction).
            destination.vertices[0] = center + new Vector2(halfWidth - radius, -halfHeight);
            destination.tangentsIn[0] = Vector2.zero;
            destination.tangentsOut[0] = new Vector2(tangent, 0f);

            destination.vertices[1] = center + new Vector2(halfWidth, -halfHeight + radius);
            destination.tangentsIn[1] = new Vector2(0f, -tangent);
            destination.tangentsOut[1] = Vector2.zero;

            destination.vertices[2] = center + new Vector2(halfWidth, halfHeight - radius);
            destination.tangentsIn[2] = Vector2.zero;
            destination.tangentsOut[2] = new Vector2(0f, tangent);

            destination.vertices[3] = center + new Vector2(halfWidth - radius, halfHeight);
            destination.tangentsIn[3] = new Vector2(tangent, 0f);
            destination.tangentsOut[3] = Vector2.zero;

            destination.vertices[4] = center + new Vector2(-halfWidth + radius, halfHeight);
            destination.tangentsIn[4] = Vector2.zero;
            destination.tangentsOut[4] = new Vector2(-tangent, 0f);

            destination.vertices[5] = center + new Vector2(-halfWidth, halfHeight - radius);
            destination.tangentsIn[5] = new Vector2(0f, tangent);
            destination.tangentsOut[5] = Vector2.zero;

            destination.vertices[6] = center + new Vector2(-halfWidth, -halfHeight + radius);
            destination.tangentsIn[6] = Vector2.zero;
            destination.tangentsOut[6] = new Vector2(0f, -tangent);

            destination.vertices[7] = center + new Vector2(-halfWidth + radius, -halfHeight);
            destination.tangentsIn[7] = new Vector2(-tangent, 0f);
            destination.tangentsOut[7] = Vector2.zero;
        }

        static void BuildPolystar(NowLottiePolystar polystar, float frame, NowLottieBezierData destination)
        {
            int pointCount = Mathf.Max(2, Mathf.RoundToInt(polystar.points.EvaluateFloat(frame)));
            Vector2 center = polystar.position.EvaluateVector2(frame);
            float rotation = (polystar.rotation?.EvaluateFloat(frame) ?? 0f) * Mathf.Deg2Rad - Mathf.PI * 0.5f;
            float outerRadius = polystar.outerRadius.EvaluateFloat(frame);
            bool isStar = polystar.starType == 1;
            float innerRadius = isStar ? polystar.innerRadius?.EvaluateFloat(frame) ?? outerRadius * 0.5f : 0f;

            int vertexCount = isStar ? pointCount * 2 : pointCount;

            destination.EnsureCapacity(vertexCount);
            destination.count = vertexCount;
            destination.closed = true;

            for (int i = 0; i < vertexCount; ++i)
            {
                float radius = isStar && (i & 1) == 1 ? innerRadius : outerRadius;
                float angle = rotation + Mathf.PI * 2f * i / vertexCount;

                destination.vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                destination.tangentsIn[i] = Vector2.zero;
                destination.tangentsOut[i] = Vector2.zero;
            }
        }
    }
}
