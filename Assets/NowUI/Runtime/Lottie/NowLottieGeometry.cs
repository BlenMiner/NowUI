using System.Collections.Generic;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>Row-major 2D affine matrix: x' = a*x + c*y + tx, y' = b*x + d*y + ty.</summary>
    public struct NowMatrix2D
    {
        public float a, b, c, d, tx, ty;

        public static readonly NowMatrix2D identity = new NowMatrix2D { a = 1f, d = 1f };

        public static NowMatrix2D Translate(float x, float y)
        {
            return new NowMatrix2D { a = 1f, d = 1f, tx = x, ty = y };
        }

        public static NowMatrix2D Scale(float x, float y)
        {
            return new NowMatrix2D { a = x, d = y };
        }

        public static NowMatrix2D Rotate(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new NowMatrix2D { a = cos, c = -sin, b = sin, d = cos };
        }

        public static NowMatrix2D SkewFromAxis(float skewDegrees, float axisDegrees)
        {
            var shear = identity;
            shear.c = Mathf.Tan(skewDegrees * Mathf.Deg2Rad);
            return Mul(Mul(Rotate(axisDegrees), shear), Rotate(-axisDegrees));
        }

        /// <summary>Returns the matrix that applies <paramref name="second"/> first, then <paramref name="first"/>.</summary>
        public static NowMatrix2D Mul(in NowMatrix2D first, in NowMatrix2D second)
        {
            NowMatrix2D result;
            result.a = first.a * second.a + first.c * second.b;
            result.c = first.a * second.c + first.c * second.d;
            result.tx = first.a * second.tx + first.c * second.ty + first.tx;
            result.b = first.b * second.a + first.d * second.b;
            result.d = first.b * second.c + first.d * second.d;
            result.ty = first.b * second.tx + first.d * second.ty + first.ty;
            return result;
        }

        public Vector2 Transform(Vector2 point)
        {
            return new Vector2(
                a * point.x + c * point.y + tx,
                b * point.x + d * point.y + ty);
        }

        /// <summary>Transforms a direction (tangent), ignoring translation.</summary>
        public Vector2 TransformVector(Vector2 vector)
        {
            return new Vector2(
                a * vector.x + c * vector.y,
                b * vector.x + d * vector.y);
        }

        public float MeanScale()
        {
            float scaleX = Mathf.Sqrt(a * a + b * b);
            float scaleY = Mathf.Sqrt(c * c + d * d);
            return (scaleX + scaleY) * 0.5f;
        }
    }

    /// <summary>Evaluated trim path parameters, normalized to 0..1 fractions.</summary>
    public struct NowLottieTrimInfo
    {
        public bool active;

        public float start;

        public float end;

        public float offset;

        public bool individual;
    }

    /// <summary>
    /// Cubic bezier contours transformed into final pixel space and packed into a
    /// flat float stream — the wire format shared by the native tessellator and the
    /// managed fallback. Per contour: [pointCount, closed, {px,py,inX,inY,outX,outY}*].
    /// </summary>
    public sealed class NowLottieContourSet
    {
        public StaticList<float> data = new StaticList<float>(256);

        public int contourCount;

        public bool isEmpty => contourCount == 0;

        public void Clear()
        {
            data.Clear();
            contourCount = 0;
        }

        public void Pack(NowLottieBezierData bezier, in NowMatrix2D matrix)
        {
            int pointCount = bezier.count;

            if (pointCount <= 0)
                return;

            data.EnsureCapacity(2 + pointCount * 6);

            var array = data.array;
            int cursor = data.count;

            array[cursor++] = pointCount;
            array[cursor++] = bezier.closed ? 1f : 0f;

            for (int i = 0; i < pointCount; ++i)
            {
                var point = matrix.Transform(bezier.vertices[i]);
                var tangentIn = matrix.TransformVector(bezier.tangentsIn[i]);
                var tangentOut = matrix.TransformVector(bezier.tangentsOut[i]);

                array[cursor++] = point.x;
                array[cursor++] = point.y;
                array[cursor++] = tangentIn.x;
                array[cursor++] = tangentIn.y;
                array[cursor++] = tangentOut.x;
                array[cursor++] = tangentOut.y;
            }

            data.count = cursor;
            ++contourCount;
        }
    }

    /// <summary>A flattened contour in final pixel space.</summary>
    public sealed class NowLottiePolyline
    {
        public readonly List<Vector2> points = new List<Vector2>(64);

        public bool closed;

        public void Clear()
        {
            points.Clear();
            closed = false;
        }

        public float Length()
        {
            float length = 0f;

            for (int i = 1; i < points.Count; ++i)
                length += Vector2.Distance(points[i - 1], points[i]);

            if (closed && points.Count > 1)
                length += Vector2.Distance(points[points.Count - 1], points[0]);

            return length;
        }
    }

    public static class NowLottiePolylinePool
    {
        static readonly Stack<NowLottiePolyline> _pool = new Stack<NowLottiePolyline>(32);

        public static NowLottiePolyline Get()
        {
            var result = _pool.Count > 0 ? _pool.Pop() : new NowLottiePolyline();
            result.Clear();
            return result;
        }

        public static void Release(NowLottiePolyline polyline)
        {
            if (polyline != null)
                _pool.Push(polyline);
        }

        public static void ReleaseAll(List<NowLottiePolyline> polylines)
        {
            for (int i = 0; i < polylines.Count; ++i)
                Release(polylines[i]);

            polylines.Clear();
        }
    }

    /// <summary>Tessellated triangles for one animation evaluation, in UI pixel space (y down).</summary>
    public sealed class NowLottieDrawBuffer
    {
        public StaticList<Vector2> positions = new StaticList<Vector2>(1024);

        public StaticList<Vector4> colors = new StaticList<Vector4>(1024);

        public StaticList<int> indices = new StaticList<int>(2048);

        public Vector2 boundsMin;

        public Vector2 boundsMax;

        public bool hasBounds;

        public void Clear()
        {
            positions.Clear();
            colors.Clear();
            indices.Clear();
            hasBounds = false;
            boundsMin = boundsMax = Vector2.zero;
        }

        public int AddVertex(Vector2 position, Vector4 color)
        {
            positions.EnsureCapacity(1);
            colors.EnsureCapacity(1);

            int index = positions.count;
            positions.array[positions.count++] = position;
            colors.array[colors.count++] = color;

            if (!hasBounds)
            {
                boundsMin = boundsMax = position;
                hasBounds = true;
            }
            else
            {
                boundsMin = Vector2.Min(boundsMin, position);
                boundsMax = Vector2.Max(boundsMax, position);
            }

            return index;
        }

        public void AddTriangle(int a, int b, int c)
        {
            indices.EnsureCapacity(3);
            indices.array[indices.count++] = a;
            indices.array[indices.count++] = b;
            indices.array[indices.count++] = c;
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }
    }

    /// <summary>Solid color or gradient evaluated per tessellated vertex.</summary>
    public struct NowLottiePaint
    {
        public bool isGradient;

        public Vector4 color;

        public int gradientType; // 1 linear, 2 radial

        public Vector2 gradientStart;

        public Vector2 gradientEnd;

        public float[] gradientStops;

        /// <summary>Number of valid floats in gradientStops (the array may be pooled and larger).</summary>
        public int gradientStopDataLength;

        public int colorStopCount;

        public float alphaMultiplier;

        public static NowLottiePaint Solid(Vector4 color)
        {
            return new NowLottiePaint { color = color, alphaMultiplier = 1f };
        }

        public Vector4 ColorAt(Vector2 position)
        {
            if (!isGradient)
                return color;

            float t;

            if (gradientType == 2)
            {
                float radius = Vector2.Distance(gradientStart, gradientEnd);
                t = radius > 0.0001f ? Vector2.Distance(position, gradientStart) / radius : 0f;
            }
            else
            {
                Vector2 direction = gradientEnd - gradientStart;
                float lengthSquared = Vector2.Dot(direction, direction);
                t = lengthSquared > 0.0001f
                    ? Vector2.Dot(position - gradientStart, direction) / lengthSquared
                    : 0f;
            }

            t = Mathf.Clamp01(t);

            var result = EvaluateColorStops(t);
            result.w *= EvaluateAlphaStops(t) * alphaMultiplier;
            result.x *= color.x;
            result.y *= color.y;
            result.z *= color.z;
            result.w *= color.w;
            return result;
        }

        Vector4 EvaluateColorStops(float t)
        {
            var stops = gradientStops;
            int count = colorStopCount;

            if (stops == null || count <= 0 || gradientStopDataLength < 4)
                return Vector4.one;

            count = Mathf.Min(count, gradientStopDataLength / 4);

            if (t <= stops[0])
                return new Vector4(stops[1], stops[2], stops[3], 1f);

            for (int i = 1; i < count; ++i)
            {
                int offset = i * 4;
                float position = stops[offset];

                if (t <= position)
                {
                    int previousOffset = offset - 4;
                    float previousPosition = stops[previousOffset];
                    float segment = position - previousPosition;
                    float blend = segment > 0.00001f ? (t - previousPosition) / segment : 0f;

                    return new Vector4(
                        Mathf.Lerp(stops[previousOffset + 1], stops[offset + 1], blend),
                        Mathf.Lerp(stops[previousOffset + 2], stops[offset + 2], blend),
                        Mathf.Lerp(stops[previousOffset + 3], stops[offset + 3], blend),
                        1f);
                }
            }

            int lastOffset = (count - 1) * 4;
            return new Vector4(stops[lastOffset + 1], stops[lastOffset + 2], stops[lastOffset + 3], 1f);
        }

        float EvaluateAlphaStops(float t)
        {
            var stops = gradientStops;
            int colorFloats = colorStopCount * 4;

            if (stops == null || gradientStopDataLength <= colorFloats + 1)
                return 1f;

            int alphaCount = (gradientStopDataLength - colorFloats) / 2;

            if (alphaCount <= 0)
                return 1f;

            if (t <= stops[colorFloats])
                return stops[colorFloats + 1];

            for (int i = 1; i < alphaCount; ++i)
            {
                int offset = colorFloats + i * 2;
                float position = stops[offset];

                if (t <= position)
                {
                    int previousOffset = offset - 2;
                    float previousPosition = stops[previousOffset];
                    float segment = position - previousPosition;
                    float blend = segment > 0.00001f ? (t - previousPosition) / segment : 0f;
                    return Mathf.Lerp(stops[previousOffset + 1], stops[offset + 1], blend);
                }
            }

            return stops[colorFloats + (alphaCount - 1) * 2 + 1];
        }
    }

    /// <summary>
    /// CPU vector rasterization helpers: bezier flattening, robust scanline fill
    /// tessellation (nonzero/even-odd, compound paths, matte clipping), analytic
    /// anti-alias fringes and stroke expansion. Main-thread only, like the rest of Now.
    /// </summary>
    public static class NowLottieTessellator
    {
        const float EPSILON = 0.0001f;

        // ------------------------------------------------------------------
        // Bezier flattening
        // ------------------------------------------------------------------

        /// <summary>
        /// Flattens a packed contour set (see <see cref="NowLottieContourSet"/>) into
        /// pooled polylines. This is the managed fallback for the native tessellator.
        /// </summary>
        public static void FlattenPackedContours(
            NowLottieContourSet set,
            float tolerance,
            List<NowLottiePolyline> output)
        {
            var data = set.data.array;
            int cursor = 0;

            for (int contour = 0; contour < set.contourCount; ++contour)
            {
                int pointCount = (int)data[cursor++];
                bool closed = data[cursor++] != 0f;

                int baseOffset = cursor;
                cursor += pointCount * 6;

                if (pointCount <= 0)
                    continue;

                var polyline = NowLottiePolylinePool.Get();
                polyline.closed = closed;

                var points = polyline.points;
                points.Add(new Vector2(data[baseOffset], data[baseOffset + 1]));

                int segmentCount = closed ? pointCount : pointCount - 1;

                for (int i = 0; i < segmentCount; ++i)
                {
                    int next = (i + 1) % pointCount;
                    int io = baseOffset + i * 6;
                    int no = baseOffset + next * 6;

                    var p0 = new Vector2(data[io], data[io + 1]);
                    var c1 = p0 + new Vector2(data[io + 4], data[io + 5]);
                    var p1 = new Vector2(data[no], data[no + 1]);
                    var c2 = p1 + new Vector2(data[no + 2], data[no + 3]);

                    FlattenCubic(p0, c1, c2, p1, tolerance, points, 0);
                }

                // The closing segment re-emits the first point; drop it to keep the contour clean.
                if (closed && points.Count > 1 &&
                    (points[points.Count - 1] - points[0]).sqrMagnitude < EPSILON * EPSILON)
                {
                    points.RemoveAt(points.Count - 1);
                }

                if (points.Count >= 2)
                    output.Add(polyline);
                else
                    NowLottiePolylinePool.Release(polyline);
            }
        }

        static void FlattenCubic(
            Vector2 p0,
            Vector2 c1,
            Vector2 c2,
            Vector2 p1,
            float tolerance,
            List<Vector2> output,
            int depth)
        {
            if (depth >= 18 || IsFlat(p0, c1, c2, p1, tolerance))
            {
                output.Add(p1);
                return;
            }

            // De Casteljau split at t = 0.5.
            Vector2 p01 = (p0 + c1) * 0.5f;
            Vector2 p12 = (c1 + c2) * 0.5f;
            Vector2 p23 = (c2 + p1) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 mid = (p012 + p123) * 0.5f;

            FlattenCubic(p0, p01, p012, mid, tolerance, output, depth + 1);
            FlattenCubic(mid, p123, p23, p1, tolerance, output, depth + 1);
        }

        static bool IsFlat(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, float tolerance)
        {
            Vector2 chord = p1 - p0;
            float chordLengthSquared = chord.sqrMagnitude;

            if (chordLengthSquared < EPSILON * EPSILON)
            {
                return (c1 - p0).magnitude + (c2 - p0).magnitude < tolerance;
            }

            float d1 = Mathf.Abs((c1.x - p0.x) * chord.y - (c1.y - p0.y) * chord.x);
            float d2 = Mathf.Abs((c2.x - p0.x) * chord.y - (c2.y - p0.y) * chord.x);
            float distanceSum = (d1 + d2) / Mathf.Sqrt(chordLengthSquared);
            return distanceSum <= tolerance;
        }

        // ------------------------------------------------------------------
        // Scanline fill tessellation
        // ------------------------------------------------------------------

        struct ScanEdge
        {
            public float yTop, yBottom, xTop, slope;

            public int winding;

            public bool isClip;

            public float XAt(float y)
            {
                return xTop + (y - yTop) * slope;
            }
        }

        /// <summary>Per-slab snapshot of an active edge with its x range precomputed.</summary>
        struct SlabEdge
        {
            public float xTop, xBottom, xMiddle;

            public int winding;

            public bool isClip;
        }

        static StaticList<ScanEdge> _edges = new StaticList<ScanEdge>(256);

        static StaticList<float> _slabYs = new StaticList<float>(256);

        static StaticList<float> _edgeSortKeys = new StaticList<float>(256);

        static StaticList<int> _edgeSortOrder = new StaticList<int>(256);

        static StaticList<int> _activeEdges = new StaticList<int>(64);

        static StaticList<SlabEdge> _slabEdges = new StaticList<SlabEdge>(64);

        public static void TessellateFill(
            List<NowLottiePolyline> contours,
            List<NowLottiePolyline> clipContours,
            bool clipInvert,
            bool evenOdd,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth,
            float gradientSpan)
        {
            _edges.Clear();
            _slabYs.Clear();

            for (int i = 0; i < contours.Count; ++i)
                CollectEdges(contours[i], false);

            bool hasClip = clipContours != null;

            if (hasClip)
            {
                for (int i = 0; i < clipContours.Count; ++i)
                    CollectEdges(clipContours[i], true);
            }

            if (_edges.count == 0)
                return;

            // Sort and deduplicate slab boundaries (in-place quicksort, no allocations).
            SortFloats(_slabYs.array, 0, _slabYs.count - 1);

            int uniqueYs = 0;

            for (int i = 0; i < _slabYs.count; ++i)
            {
                if (uniqueYs == 0 || _slabYs.array[i] - _slabYs.array[uniqueYs - 1] > EPSILON)
                    _slabYs.array[uniqueYs++] = _slabYs.array[i];
            }

            _slabYs.count = uniqueYs;

            // Sort edge indices by top y using a parallel key array (primitive sort).
            int edgeCount = _edges.count;
            _edgeSortKeys.Clear();
            _edgeSortOrder.Clear();
            _edgeSortKeys.EnsureCapacity(edgeCount);
            _edgeSortOrder.EnsureCapacity(edgeCount);

            for (int i = 0; i < edgeCount; ++i)
            {
                _edgeSortKeys.array[i] = _edges.array[i].yTop;
                _edgeSortOrder.array[i] = i;
            }

            _edgeSortKeys.count = edgeCount;
            _edgeSortOrder.count = edgeCount;
            SortFloatsWithIndices(_edgeSortKeys.array, _edgeSortOrder.array, 0, edgeCount - 1);

            _activeEdges.Clear();
            int nextEdge = 0;
            float maxSlabHeight = paint.isGradient && gradientSpan > 0f ? gradientSpan : float.MaxValue;

            for (int slab = 0; slab + 1 < _slabYs.count; ++slab)
            {
                float slabTop = _slabYs.array[slab];
                float slabBottom = _slabYs.array[slab + 1];

                if (slabBottom - slabTop < EPSILON)
                    continue;

                // Admit edges starting at or above this slab, retire finished ones.
                while (nextEdge < edgeCount && _edgeSortKeys.array[nextEdge] <= slabTop + EPSILON)
                {
                    _activeEdges.EnsureCapacity(1);
                    _activeEdges.array[_activeEdges.count++] = _edgeSortOrder.array[nextEdge];
                    ++nextEdge;
                }

                int activeCount = 0;

                for (int i = 0; i < _activeEdges.count; ++i)
                {
                    int edgeIndex = _activeEdges.array[i];

                    if (_edges.array[edgeIndex].yBottom > slabTop + EPSILON)
                        _activeEdges.array[activeCount++] = edgeIndex;
                }

                _activeEdges.count = activeCount;

                if (activeCount == 0)
                    continue;

                // Snapshot the active edges with their x range over this slab so the
                // sort and span walk never recompute intersections.
                _slabEdges.Clear();
                _slabEdges.EnsureCapacity(activeCount);

                for (int i = 0; i < activeCount; ++i)
                {
                    ref var edge = ref _edges.array[_activeEdges.array[i]];

                    SlabEdge slabEdge;
                    slabEdge.xTop = edge.XAt(slabTop);
                    slabEdge.xBottom = edge.XAt(slabBottom);
                    slabEdge.xMiddle = (slabEdge.xTop + slabEdge.xBottom) * 0.5f;
                    slabEdge.winding = edge.winding;
                    slabEdge.isClip = edge.isClip;
                    _slabEdges.array[_slabEdges.count++] = slabEdge;
                }

                SortSlabEdges();

                int verticalChunks = Mathf.Max(1, Mathf.CeilToInt((slabBottom - slabTop) / maxSlabHeight));
                float inverseChunks = 1f / verticalChunks;

                for (int chunk = 0; chunk < verticalChunks; ++chunk)
                {
                    float fractionTop = chunk * inverseChunks;
                    float fractionBottom = chunk == verticalChunks - 1 ? 1f : fractionTop + inverseChunks;
                    float ya = Mathf.Lerp(slabTop, slabBottom, fractionTop);
                    float yb = Mathf.Lerp(slabTop, slabBottom, fractionBottom);
                    EmitSlabSpans(ya, yb, fractionTop, fractionBottom, hasClip, clipInvert, evenOdd, paint, buffer, gradientSpan);
                }
            }
        }

        static void EmitSlabSpans(
            float ya,
            float yb,
            float fractionTop,
            float fractionBottom,
            bool hasClip,
            bool clipInvert,
            bool evenOdd,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float gradientSpan)
        {
            int shapeWinding = 0;
            int clipWinding = 0;
            bool spanOpen = false;
            float spanTopX = 0f;
            float spanBottomX = 0f;

            for (int i = 0; i < _slabEdges.count; ++i)
            {
                ref var edge = ref _slabEdges.array[i];
                bool insideBefore = IsInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

                if (edge.isClip)
                    clipWinding += edge.winding;
                else
                    shapeWinding += edge.winding;

                bool insideAfter = IsInside(shapeWinding, clipWinding, hasClip, clipInvert, evenOdd);

                if (insideBefore == insideAfter)
                    continue;

                float edgeTopX = Mathf.Lerp(edge.xTop, edge.xBottom, fractionTop);
                float edgeBottomX = Mathf.Lerp(edge.xTop, edge.xBottom, fractionBottom);

                if (insideAfter)
                {
                    spanTopX = edgeTopX;
                    spanBottomX = edgeBottomX;
                    spanOpen = true;
                }
                else if (spanOpen)
                {
                    EmitTrapezoid(spanTopX, edgeTopX, spanBottomX, edgeBottomX, ya, yb, paint, buffer, gradientSpan);
                    spanOpen = false;
                }
            }
        }

        /// <summary>In-place quicksort with insertion sort for small ranges. Used instead
        /// of Array.Sort to stay allocation free on all Unity scripting backends.</summary>
        static void SortFloats(float[] values, int low, int high)
        {
            while (low < high)
            {
                if (high - low < 12)
                {
                    for (int i = low + 1; i <= high; ++i)
                    {
                        float current = values[i];
                        int j = i - 1;

                        while (j >= low && values[j] > current)
                        {
                            values[j + 1] = values[j];
                            --j;
                        }

                        values[j + 1] = current;
                    }

                    return;
                }

                // Median-of-three pivot.
                int middle = low + (high - low) / 2;
                if (values[middle] < values[low]) (values[low], values[middle]) = (values[middle], values[low]);
                if (values[high] < values[low]) (values[low], values[high]) = (values[high], values[low]);
                if (values[high] < values[middle]) (values[middle], values[high]) = (values[high], values[middle]);

                float pivot = values[middle];
                int left = low;
                int right = high;

                while (left <= right)
                {
                    while (values[left] < pivot) ++left;
                    while (values[right] > pivot) --right;

                    if (left <= right)
                    {
                        (values[left], values[right]) = (values[right], values[left]);
                        ++left;
                        --right;
                    }
                }

                // Recurse into the smaller half, loop on the larger.
                if (right - low < high - left)
                {
                    SortFloats(values, low, right);
                    low = left;
                }
                else
                {
                    SortFloats(values, left, high);
                    high = right;
                }
            }
        }

        static void SortFloatsWithIndices(float[] keys, int[] values, int low, int high)
        {
            while (low < high)
            {
                if (high - low < 12)
                {
                    for (int i = low + 1; i <= high; ++i)
                    {
                        float currentKey = keys[i];
                        int currentValue = values[i];
                        int j = i - 1;

                        while (j >= low && keys[j] > currentKey)
                        {
                            keys[j + 1] = keys[j];
                            values[j + 1] = values[j];
                            --j;
                        }

                        keys[j + 1] = currentKey;
                        values[j + 1] = currentValue;
                    }

                    return;
                }

                int middle = low + (high - low) / 2;
                if (keys[middle] < keys[low]) SwapKeyValue(keys, values, low, middle);
                if (keys[high] < keys[low]) SwapKeyValue(keys, values, low, high);
                if (keys[high] < keys[middle]) SwapKeyValue(keys, values, middle, high);

                float pivot = keys[middle];
                int left = low;
                int right = high;

                while (left <= right)
                {
                    while (keys[left] < pivot) ++left;
                    while (keys[right] > pivot) --right;

                    if (left <= right)
                    {
                        SwapKeyValue(keys, values, left, right);
                        ++left;
                        --right;
                    }
                }

                if (right - low < high - left)
                {
                    SortFloatsWithIndices(keys, values, low, right);
                    low = left;
                }
                else
                {
                    SortFloatsWithIndices(keys, values, left, high);
                    high = right;
                }
            }
        }

        static void SwapKeyValue(float[] keys, int[] values, int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }

        static void SortSlabEdges()
        {
            // Insertion sort by x: the active set is small and mostly sorted already.
            var edges = _slabEdges.array;
            int count = _slabEdges.count;

            for (int i = 1; i < count; ++i)
            {
                var current = edges[i];
                int j = i - 1;

                while (j >= 0 && edges[j].xMiddle > current.xMiddle)
                {
                    edges[j + 1] = edges[j];
                    --j;
                }

                edges[j + 1] = current;
            }
        }

        static bool IsInside(int shapeWinding, int clipWinding, bool hasClip, bool clipInvert, bool evenOdd)
        {
            bool shapeInside = evenOdd ? (shapeWinding & 1) != 0 : shapeWinding != 0;

            if (!shapeInside)
                return false;

            if (!hasClip)
                return true;

            bool clipInside = clipWinding != 0;
            return clipInvert ? !clipInside : clipInside;
        }

        static void EmitTrapezoid(
            float topLeft,
            float topRight,
            float bottomLeft,
            float bottomRight,
            float ya,
            float yb,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float gradientSpan)
        {
            float topWidth = topRight - topLeft;
            float bottomWidth = bottomRight - bottomLeft;

            if (topWidth < EPSILON && bottomWidth < EPSILON)
                return;

            int chunks = 1;

            if (paint.isGradient && gradientSpan > 0f)
            {
                float width = Mathf.Max(topWidth, bottomWidth);
                chunks = Mathf.Clamp(Mathf.CeilToInt(width / gradientSpan), 1, 256);
            }

            for (int chunk = 0; chunk < chunks; ++chunk)
            {
                float t0 = (float)chunk / chunks;
                float t1 = (float)(chunk + 1) / chunks;

                var a = new Vector2(Mathf.Lerp(topLeft, topRight, t0), ya);
                var b = new Vector2(Mathf.Lerp(topLeft, topRight, t1), ya);
                var c = new Vector2(Mathf.Lerp(bottomLeft, bottomRight, t1), yb);
                var d = new Vector2(Mathf.Lerp(bottomLeft, bottomRight, t0), yb);

                int ia = buffer.AddVertex(a, paint.ColorAt(a));
                int ib = buffer.AddVertex(b, paint.ColorAt(b));
                int ic = buffer.AddVertex(c, paint.ColorAt(c));
                int id = buffer.AddVertex(d, paint.ColorAt(d));
                buffer.AddQuad(ia, ib, ic, id);
            }
        }

        static void CollectEdges(NowLottiePolyline polyline, bool isClip)
        {
            var points = polyline.points;
            int count = points.Count;

            if (count < 2)
                return;

            // Fills always treat contours as closed.
            for (int i = 0; i < count; ++i)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % count];

                if (Mathf.Abs(from.y - to.y) < EPSILON)
                    continue;

                ScanEdge edge;

                if (from.y < to.y)
                {
                    edge.yTop = from.y;
                    edge.yBottom = to.y;
                    edge.xTop = from.x;
                    edge.winding = 1;
                }
                else
                {
                    edge.yTop = to.y;
                    edge.yBottom = from.y;
                    edge.xTop = to.x;
                    edge.winding = -1;
                }

                edge.slope = (to.x - from.x) / (to.y - from.y);
                edge.isClip = isClip;

                _edges.EnsureCapacity(1);
                _edges.array[_edges.count++] = edge;

                _slabYs.EnsureCapacity(2);
                _slabYs.array[_slabYs.count++] = edge.yTop;
                _slabYs.array[_slabYs.count++] = edge.yBottom;
            }
        }

        // ------------------------------------------------------------------
        // Anti-alias fringe for fills
        // ------------------------------------------------------------------

        static readonly List<Vector2> _fringeNormals = new List<Vector2>(128);

        static readonly List<bool> _fringeInside = new List<bool>(128);

        public static void EmitFillFringe(
            List<NowLottiePolyline> contours,
            List<NowLottiePolyline> clipContours,
            bool clipInvert,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth)
        {
            if (aaWidth <= 0f)
                return;

            bool hasClip = clipContours != null && clipContours.Count > 0;

            for (int contourIndex = 0; contourIndex < contours.Count; ++contourIndex)
            {
                var contour = contours[contourIndex];
                var points = contour.points;

                if (points.Count < 3)
                    continue;

                float area = SignedArea(points);

                if (Mathf.Abs(area) < EPSILON)
                    continue;

                // Even containment depth means the contour bounds filled area from the
                // outside; odd depth means it is a hole. The fringe always feathers away
                // from the filled side.
                int depth = ContainmentDepth(contours, contourIndex);
                float direction = Mathf.Sign(area) * ((depth & 1) == 0 ? 1f : -1f);

                _fringeNormals.Clear();
                int count = points.Count;

                for (int i = 0; i < count; ++i)
                {
                    Vector2 previous = points[(i - 1 + count) % count];
                    Vector2 current = points[i];
                    Vector2 next = points[(i + 1) % count];

                    Vector2 inEdge = current - previous;
                    Vector2 outEdge = next - current;

                    Vector2 inNormal = Normalize(new Vector2(inEdge.y, -inEdge.x));
                    Vector2 outNormal = Normalize(new Vector2(outEdge.y, -outEdge.x));
                    Vector2 average = Normalize(inNormal + outNormal);

                    if (average == Vector2.zero)
                        average = inNormal;

                    float miter = Vector2.Dot(average, outNormal);
                    float scale = 1f / Mathf.Max(0.35f, miter);
                    _fringeNormals.Add(average * (scale * direction * aaWidth));
                }

                // When the fill is matte-clipped, only feather edges that lie inside
                // the matte so the fringe doesn't trace clipped-away geometry.
                _fringeInside.Clear();

                for (int i = 0; i < count; ++i)
                {
                    bool inside = true;

                    if (hasClip)
                    {
                        inside = WindingAt(clipContours, points[i]) != 0;

                        if (clipInvert)
                            inside = !inside;
                    }

                    _fringeInside.Add(inside);
                }

                int firstInner = -1;
                int previousInner = -1;
                int previousOuter = -1;

                for (int i = 0; i < count; ++i)
                {
                    Vector2 position = points[i];
                    Vector4 innerColor = paint.ColorAt(position);
                    Vector4 outerColor = innerColor;
                    outerColor.w = 0f;

                    int inner = buffer.AddVertex(position, innerColor);
                    int outer = buffer.AddVertex(position + _fringeNormals[i], outerColor);

                    if (i == 0)
                    {
                        firstInner = inner;
                    }
                    else if (_fringeInside[i - 1] && _fringeInside[i])
                    {
                        buffer.AddQuad(previousInner, previousOuter, outer, inner);
                    }

                    previousInner = inner;
                    previousOuter = outer;
                }

                // Closing quad back to the first pair (first outer is firstInner + 1).
                if (_fringeInside[count - 1] && _fringeInside[0])
                    buffer.AddQuad(previousInner, previousOuter, firstInner + 1, firstInner);
            }
        }

        static float SignedArea(List<Vector2> points)
        {
            float area = 0f;

            for (int i = 0; i < points.Count; ++i)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }

        static int ContainmentDepth(List<NowLottiePolyline> contours, int contourIndex)
        {
            var probe = contours[contourIndex].points[0];
            int depth = 0;

            for (int i = 0; i < contours.Count; ++i)
            {
                if (i == contourIndex)
                    continue;

                if (WindingNumber(contours[i].points, probe) != 0)
                    ++depth;
            }

            return depth;
        }

        static int WindingNumber(List<Vector2> points, Vector2 probe)
        {
            int winding = 0;
            int count = points.Count;

            for (int i = 0; i < count; ++i)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % count];

                if (from.y <= probe.y)
                {
                    if (to.y > probe.y && Cross(to - from, probe - from) > 0f)
                        ++winding;
                }
                else if (to.y <= probe.y && Cross(to - from, probe - from) < 0f)
                {
                    --winding;
                }
            }

            return winding;
        }

        public static int WindingAt(List<NowLottiePolyline> contours, Vector2 probe)
        {
            int winding = 0;

            for (int i = 0; i < contours.Count; ++i)
                winding += WindingNumber(contours[i].points, probe);

            return winding;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        static Vector2 Normalize(Vector2 value)
        {
            float magnitude = value.magnitude;
            return magnitude > EPSILON ? value / magnitude : Vector2.zero;
        }

        // ------------------------------------------------------------------
        // Strokes
        // ------------------------------------------------------------------

        static readonly List<Vector2> _strokePoints = new List<Vector2>(256);

        static readonly List<Vector2> _strokeNormals = new List<Vector2>(256);

        public static void EmitStroke(
            List<NowLottiePolyline> polylines,
            float width,
            int cap,
            int join,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth)
        {
            float halfWidth = width * 0.5f;

            if (halfWidth <= 0f)
                return;

            for (int i = 0; i < polylines.Count; ++i)
                EmitStrokePolyline(polylines[i], halfWidth, cap, join, paint, buffer, aaWidth);
        }

        static void EmitStrokePolyline(
            NowLottiePolyline polyline,
            float halfWidth,
            int cap,
            int join,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth)
        {
            // Deduplicate consecutive points so segment directions are well defined.
            _strokePoints.Clear();
            var source = polyline.points;

            for (int i = 0; i < source.Count; ++i)
            {
                if (_strokePoints.Count == 0 ||
                    (source[i] - _strokePoints[_strokePoints.Count - 1]).sqrMagnitude > EPSILON * EPSILON)
                {
                    _strokePoints.Add(source[i]);
                }
            }

            bool closed = polyline.closed;

            if (closed && _strokePoints.Count > 1 &&
                (_strokePoints[_strokePoints.Count - 1] - _strokePoints[0]).sqrMagnitude < EPSILON * EPSILON)
            {
                _strokePoints.RemoveAt(_strokePoints.Count - 1);
            }

            int count = _strokePoints.Count;

            if (count < 2)
                return;

            // Square caps extend the line by half the stroke width.
            if (!closed && cap == 3)
            {
                Vector2 startDirection = Normalize(_strokePoints[0] - _strokePoints[1]);
                Vector2 endDirection = Normalize(_strokePoints[count - 1] - _strokePoints[count - 2]);
                _strokePoints[0] += startDirection * halfWidth;
                _strokePoints[count - 1] += endDirection * halfWidth;
            }

            float innerWidth = Mathf.Max(halfWidth - aaWidth, 0f);
            float outerWidth = halfWidth + aaWidth;
            float coreAlpha = halfWidth < aaWidth ? halfWidth / aaWidth : 1f;

            _strokeNormals.Clear();

            for (int i = 0; i < count; ++i)
            {
                Vector2 inDirection;
                Vector2 outDirection;

                if (closed)
                {
                    inDirection = Normalize(_strokePoints[i] - _strokePoints[(i - 1 + count) % count]);
                    outDirection = Normalize(_strokePoints[(i + 1) % count] - _strokePoints[i]);
                }
                else
                {
                    inDirection = i > 0 ? Normalize(_strokePoints[i] - _strokePoints[i - 1]) : Vector2.zero;
                    outDirection = i < count - 1 ? Normalize(_strokePoints[i + 1] - _strokePoints[i]) : Vector2.zero;

                    if (inDirection == Vector2.zero) inDirection = outDirection;
                    if (outDirection == Vector2.zero) outDirection = inDirection;
                }

                Vector2 inNormal = new Vector2(inDirection.y, -inDirection.x);
                Vector2 outNormal = new Vector2(outDirection.y, -outDirection.x);
                Vector2 average = Normalize(inNormal + outNormal);

                if (average == Vector2.zero)
                    average = inNormal;

                float miter = Vector2.Dot(average, outNormal);
                float scale = 1f / Mathf.Max(0.35f, Mathf.Abs(miter));
                _strokeNormals.Add(average * scale);
            }

            int segmentCount = closed ? count : count - 1;
            int firstRing = -1;
            int previousRing = -1;

            for (int i = 0; i < count; ++i)
            {
                Vector2 position = _strokePoints[i];
                Vector2 normal = _strokeNormals[i];

                Vector4 coreColor = paint.ColorAt(position);
                coreColor.w *= coreAlpha;
                Vector4 edgeColor = coreColor;
                edgeColor.w = 0f;

                int ring = buffer.AddVertex(position + normal * outerWidth, edgeColor);
                buffer.AddVertex(position + normal * innerWidth, coreColor);
                buffer.AddVertex(position - normal * innerWidth, coreColor);
                buffer.AddVertex(position - normal * outerWidth, edgeColor);

                if (i == 0)
                    firstRing = ring;

                if (i > 0)
                    ConnectStrokeRings(buffer, previousRing, ring);

                previousRing = ring;
            }

            if (closed)
                ConnectStrokeRings(buffer, previousRing, firstRing);

            if (!closed && cap == 2)
            {
                Vector2 startDirection = Normalize(_strokePoints[0] - _strokePoints[1]);
                Vector2 endDirection = Normalize(_strokePoints[count - 1] - _strokePoints[count - 2]);
                EmitRoundCap(_strokePoints[0], startDirection, innerWidth, outerWidth, coreAlpha, paint, buffer);
                EmitRoundCap(_strokePoints[count - 1], endDirection, innerWidth, outerWidth, coreAlpha, paint, buffer);
            }
        }

        static void ConnectStrokeRings(NowLottieDrawBuffer buffer, int ringA, int ringB)
        {
            // Each ring is 4 consecutive vertices: outer+, inner+, inner-, outer-.
            buffer.AddQuad(ringA + 0, ringB + 0, ringB + 1, ringA + 1);
            buffer.AddQuad(ringA + 1, ringB + 1, ringB + 2, ringA + 2);
            buffer.AddQuad(ringA + 2, ringB + 2, ringB + 3, ringA + 3);
        }

        static void EmitRoundCap(
            Vector2 center,
            Vector2 direction,
            float innerWidth,
            float outerWidth,
            float coreAlpha,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer)
        {
            Vector2 normal = new Vector2(direction.y, -direction.x);

            Vector4 coreColor = paint.ColorAt(center);
            coreColor.w *= coreAlpha;
            Vector4 edgeColor = coreColor;
            edgeColor.w = 0f;

            int segments = Mathf.Clamp(Mathf.CeilToInt(outerWidth * 0.6f) + 3, 4, 24);
            int centerIndex = buffer.AddVertex(center, coreColor);

            int previousInner = -1;
            int previousOuter = -1;

            for (int i = 0; i <= segments; ++i)
            {
                // Sweep 180 degrees from +normal through +direction to -normal.
                float angle = Mathf.PI * i / segments;
                Vector2 radial = normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle);

                int inner = buffer.AddVertex(center + radial * innerWidth, coreColor);
                int outer = buffer.AddVertex(center + radial * outerWidth, edgeColor);

                if (i > 0)
                {
                    buffer.AddTriangle(centerIndex, previousInner, inner);
                    buffer.AddQuad(previousInner, previousOuter, outer, inner);
                }

                previousInner = inner;
                previousOuter = outer;
            }
        }

        // ------------------------------------------------------------------
        // Trim paths
        // ------------------------------------------------------------------

        static readonly List<NowLottiePolyline> _trimScratch = new List<NowLottiePolyline>(16);

        /// <summary>
        /// Replaces the polylines in place with their trimmed versions. start/end/offset
        /// are normalized fractions (start and end already divided by 100, offset by 360).
        /// </summary>
        public static void ApplyTrim(List<NowLottiePolyline> polylines, float start, float end, float offset, bool individually)
        {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);

            if (end < start)
            {
                (start, end) = (end, start);
            }

            float trimStart = start + offset;
            float trimEnd = end + offset;

            if (end - start >= 1f)
                return; // nothing trimmed

            _trimScratch.Clear();

            if (individually)
            {
                for (int i = 0; i < polylines.Count; ++i)
                {
                    float length = polylines[i].Length();
                    ExtractWrappedRange(polylines[i], trimStart * length, trimEnd * length, length, _trimScratch);
                }
            }
            else
            {
                float totalLength = 0f;

                for (int i = 0; i < polylines.Count; ++i)
                    totalLength += polylines[i].Length();

                float globalStart = trimStart * totalLength;
                float globalEnd = trimEnd * totalLength;
                float walked = 0f;

                // Handle wrap-around by evaluating both the primary range and, when the
                // range wraps past the total length, its continuation from zero.
                for (int pass = 0; pass < 2; ++pass)
                {
                    float rangeStart = pass == 0 ? globalStart : globalStart - totalLength;
                    float rangeEnd = pass == 0 ? globalEnd : globalEnd - totalLength;

                    if (pass == 1 && globalEnd <= totalLength)
                        break;

                    walked = 0f;

                    for (int i = 0; i < polylines.Count; ++i)
                    {
                        float length = polylines[i].Length();
                        float localStart = Mathf.Max(rangeStart - walked, 0f);
                        float localEnd = Mathf.Min(rangeEnd - walked, length);

                        if (localEnd > localStart && localEnd > 0f && localStart < length)
                            ExtractRange(polylines[i], localStart, localEnd, _trimScratch);

                        walked += length;
                    }
                }
            }

            NowLottiePolylinePool.ReleaseAll(polylines);
            polylines.AddRange(_trimScratch);
            _trimScratch.Clear();
        }

        static void ExtractWrappedRange(NowLottiePolyline polyline, float start, float end, float length, List<NowLottiePolyline> output)
        {
            if (length <= EPSILON)
                return;

            float span = Mathf.Min(end - start, length);

            if (span <= EPSILON)
                return;

            // Normalize the start into [0, length) and keep the span, wrapping if needed.
            start = Mathf.Repeat(start, length);
            end = start + span;

            if (end <= length)
            {
                ExtractRange(polyline, start, end, output);
                return;
            }

            ExtractRange(polyline, start, length, output);
            ExtractRange(polyline, 0f, end - length, output);
        }

        static void ExtractRange(NowLottiePolyline polyline, float start, float end, List<NowLottiePolyline> output)
        {
            var points = polyline.points;
            int count = points.Count;

            if (count < 2 || end - start < EPSILON)
                return;

            var result = NowLottiePolylinePool.Get();
            result.closed = false;

            float walked = 0f;
            int segmentCount = polyline.closed ? count : count - 1;

            for (int i = 0; i < segmentCount; ++i)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % count];
                float segmentLength = Vector2.Distance(from, to);

                if (segmentLength < EPSILON)
                    continue;

                float segmentStart = walked;
                float segmentEnd = walked + segmentLength;

                if (segmentEnd > start && segmentStart < end)
                {
                    float fromT = Mathf.Clamp01((start - segmentStart) / segmentLength);
                    float toT = Mathf.Clamp01((end - segmentStart) / segmentLength);

                    Vector2 clippedFrom = Vector2.Lerp(from, to, fromT);
                    Vector2 clippedTo = Vector2.Lerp(from, to, toT);

                    if (result.points.Count == 0)
                        result.points.Add(clippedFrom);

                    result.points.Add(clippedTo);
                }

                walked = segmentEnd;

                if (walked >= end)
                    break;
            }

            if (result.points.Count >= 2)
                output.Add(result);
            else
                NowLottiePolylinePool.Release(result);
        }

        // ------------------------------------------------------------------
        // Polyline clipping against matte contours (used for strokes)
        // ------------------------------------------------------------------

        static readonly List<float> _clipCrossings = new List<float>(16);

        public static void ClipPolylines(
            List<NowLottiePolyline> polylines,
            List<NowLottiePolyline> clipContours,
            bool clipInvert,
            List<NowLottiePolyline> output)
        {
            for (int polylineIndex = 0; polylineIndex < polylines.Count; ++polylineIndex)
            {
                var polyline = polylines[polylineIndex];
                var points = polyline.points;
                int count = points.Count;
                int segmentCount = polyline.closed ? count : count - 1;

                NowLottiePolyline current = null;

                for (int i = 0; i < segmentCount; ++i)
                {
                    Vector2 from = points[i];
                    Vector2 to = points[(i + 1) % count];

                    _clipCrossings.Clear();
                    _clipCrossings.Add(0f);
                    CollectCrossings(from, to, clipContours, _clipCrossings);
                    _clipCrossings.Add(1f);
                    _clipCrossings.Sort();

                    for (int piece = 0; piece + 1 < _clipCrossings.Count; ++piece)
                    {
                        float t0 = _clipCrossings[piece];
                        float t1 = _clipCrossings[piece + 1];

                        if (t1 - t0 < EPSILON)
                            continue;

                        Vector2 middle = Vector2.Lerp(from, to, (t0 + t1) * 0.5f);
                        bool inside = WindingAt(clipContours, middle) != 0;

                        if (clipInvert)
                            inside = !inside;

                        if (!inside)
                        {
                            if (current != null && current.points.Count >= 2)
                                output.Add(current);
                            else if (current != null)
                                NowLottiePolylinePool.Release(current);

                            current = null;
                            continue;
                        }

                        Vector2 pieceFrom = Vector2.Lerp(from, to, t0);
                        Vector2 pieceTo = Vector2.Lerp(from, to, t1);

                        if (current == null)
                        {
                            current = NowLottiePolylinePool.Get();
                            current.closed = false;
                            current.points.Add(pieceFrom);
                        }

                        current.points.Add(pieceTo);
                    }
                }

                if (current != null && current.points.Count >= 2)
                    output.Add(current);
                else if (current != null)
                    NowLottiePolylinePool.Release(current);
            }
        }

        static void CollectCrossings(Vector2 from, Vector2 to, List<NowLottiePolyline> clipContours, List<float> crossings)
        {
            Vector2 direction = to - from;

            for (int contourIndex = 0; contourIndex < clipContours.Count; ++contourIndex)
            {
                var contour = clipContours[contourIndex].points;
                int count = contour.Count;

                for (int i = 0; i < count; ++i)
                {
                    Vector2 edgeFrom = contour[i];
                    Vector2 edgeTo = contour[(i + 1) % count];
                    Vector2 edgeDirection = edgeTo - edgeFrom;

                    float denominator = Cross(direction, edgeDirection);

                    if (Mathf.Abs(denominator) < EPSILON)
                        continue;

                    Vector2 delta = edgeFrom - from;
                    float t = Cross(delta, edgeDirection) / denominator;
                    float u = Cross(delta, direction) / denominator;

                    if (t > 0f && t < 1f && u >= 0f && u <= 1f)
                        crossings.Add(t);
                }
            }
        }
    }
}
