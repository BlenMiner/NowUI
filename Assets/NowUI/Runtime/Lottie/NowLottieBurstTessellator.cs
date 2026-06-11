using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// Burst-compiled port of <see cref="NowLottieTessellator"/> used when the native
    /// nowui-vg plugin is unavailable. Each paint operation runs as one Burst job
    /// (flatten + tessellate + fringe, or flatten + stroke) and must produce output
    /// identical to the scalar tessellator — the algorithms are ported line for line
    /// and a unit test compares both paths element by element.
    ///
    /// Trim paths and matte-clipped strokes still use the scalar path: they splice
    /// polylines on the managed side and are rare enough not to matter.
    /// </summary>
    internal static class NowLottieBurstTessellator
    {
        /// <summary>
        /// Forces the scalar managed tessellator even though the Burst path is
        /// available. Intended for profiling comparisons and debugging.
        /// </summary>
        public static bool forceScalar;

        const float EPSILON = 0.0001f;

        // ------------------------------------------------------------------
        // Job-friendly paint
        // ------------------------------------------------------------------

        struct BurstPaint
        {
            public bool isGradient;
            public Vector4 color;
            public int gradientType;
            public Vector2 gradientStart;
            public Vector2 gradientEnd;
            public int gradientStopDataLength;
            public int colorStopCount;
            public float alphaMultiplier;
        }

        static BurstPaint ToBurstPaint(in NowLottiePaint paint)
        {
            return new BurstPaint
            {
                isGradient = paint.isGradient,
                color = paint.color,
                gradientType = paint.gradientType,
                gradientStart = paint.gradientStart,
                gradientEnd = paint.gradientEnd,
                gradientStopDataLength = paint.gradientStopDataLength,
                colorStopCount = paint.colorStopCount,
                alphaMultiplier = paint.alphaMultiplier
            };
        }

        static NativeArray<float> CopyStops(in NowLottiePaint paint)
        {
            int length = paint.gradientStops != null ? Mathf.Min(paint.gradientStopDataLength, paint.gradientStops.Length) : 0;
            var stops = new NativeArray<float>(Mathf.Max(1, length), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < length; ++i)
                stops[i] = paint.gradientStops[i];

            return stops;
        }

        static Vector4 ColorAt(in BurstPaint paint, in NativeArray<float> stops, Vector2 position)
        {
            if (!paint.isGradient)
                return paint.color;

            float t;

            if (paint.gradientType == 2)
            {
                float radius = Vector2.Distance(paint.gradientStart, paint.gradientEnd);
                t = radius > 0.0001f ? Vector2.Distance(position, paint.gradientStart) / radius : 0f;
            }
            else
            {
                Vector2 direction = paint.gradientEnd - paint.gradientStart;
                float lengthSquared = Vector2.Dot(direction, direction);
                t = lengthSquared > 0.0001f
                    ? Vector2.Dot(position - paint.gradientStart, direction) / lengthSquared
                    : 0f;
            }

            t = Mathf.Clamp01(t);

            var result = EvaluateColorStops(paint, stops, t);
            result.w *= EvaluateAlphaStops(paint, stops, t) * paint.alphaMultiplier;
            result.x *= paint.color.x;
            result.y *= paint.color.y;
            result.z *= paint.color.z;
            result.w *= paint.color.w;
            return result;
        }

        static Vector4 EvaluateColorStops(in BurstPaint paint, in NativeArray<float> stops, float t)
        {
            int count = paint.colorStopCount;

            if (count <= 0 || paint.gradientStopDataLength < 4)
                return Vector4.one;

            count = Mathf.Min(count, paint.gradientStopDataLength / 4);

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

        static float EvaluateAlphaStops(in BurstPaint paint, in NativeArray<float> stops, float t)
        {
            int colorFloats = paint.colorStopCount * 4;

            if (paint.gradientStopDataLength <= colorFloats + 1)
                return 1f;

            int alphaCount = (paint.gradientStopDataLength - colorFloats) / 2;

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

        // ------------------------------------------------------------------
        // Shared job pieces: polylines + flattening + output
        // ------------------------------------------------------------------

        /// <summary>Start index and count into a shared point buffer, plus the closed flag.</summary>
        struct PolyRange
        {
            public int start;
            public int count;
            public byte closed;
        }

        struct Outputs
        {
            public NativeList<Vector2> positions;
            public NativeList<Vector4> colors;
            public NativeList<int> indices;
            public NativeArray<Vector2> bounds; // [min, max]
            public NativeArray<int> hasBounds;  // [0] != 0 when bounds valid
            public int baseVertex;

            public int AddVertex(Vector2 position, Vector4 color)
            {
                int index = positions.Length;
                positions.Add(position);
                colors.Add(color);

                if (hasBounds[0] == 0)
                {
                    bounds[0] = position;
                    bounds[1] = position;
                    hasBounds[0] = 1;
                }
                else
                {
                    bounds[0] = Vector2.Min(bounds[0], position);
                    bounds[1] = Vector2.Max(bounds[1], position);
                }

                return baseVertex + index;
            }

            public void AddTriangle(int a, int b, int c)
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }

            public void AddQuad(int a, int b, int c, int d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }
        }

        struct CubicFrame
        {
            public Vector2 p0, c1, c2, p1;
            public int depth;
        }

        /// <summary>
        /// Iterative port of the recursive De Casteljau flattening; pushes the second
        /// half first so points are emitted in curve order, and honors the same
        /// 18-level depth limit.
        /// </summary>
        static void FlattenCubic(
            Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1,
            float tolerance,
            NativeList<Vector2> output,
            NativeArray<CubicFrame> stack)
        {
            int top = 0;
            stack[top++] = new CubicFrame { p0 = p0, c1 = c1, c2 = c2, p1 = p1, depth = 0 };

            while (top > 0)
            {
                CubicFrame frame = stack[--top];

                if (frame.depth >= 18 || IsFlat(frame.p0, frame.c1, frame.c2, frame.p1, tolerance))
                {
                    output.Add(frame.p1);
                    continue;
                }

                Vector2 p01 = (frame.p0 + frame.c1) * 0.5f;
                Vector2 p12 = (frame.c1 + frame.c2) * 0.5f;
                Vector2 p23 = (frame.c2 + frame.p1) * 0.5f;
                Vector2 p012 = (p01 + p12) * 0.5f;
                Vector2 p123 = (p12 + p23) * 0.5f;
                Vector2 mid = (p012 + p123) * 0.5f;

                stack[top++] = new CubicFrame { p0 = mid, c1 = p123, c2 = p23, p1 = frame.p1, depth = frame.depth + 1 };
                stack[top++] = new CubicFrame { p0 = frame.p0, c1 = p01, c2 = p012, p1 = mid, depth = frame.depth + 1 };
            }
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

        /// <summary>Port of FlattenPackedContours over the packed contour stream.</summary>
        static void FlattenPacked(
            in NativeArray<float> data,
            int contourCount,
            float tolerance,
            NativeList<Vector2> points,
            NativeList<PolyRange> ranges)
        {
            var stack = new NativeArray<CubicFrame>(40, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int cursor = 0;

            for (int contour = 0; contour < contourCount; ++contour)
            {
                int pointCount = (int)data[cursor++];
                bool closed = data[cursor++] != 0f;

                int baseOffset = cursor;
                cursor += pointCount * 6;

                if (pointCount <= 0)
                    continue;

                int start = points.Length;
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

                    FlattenCubic(p0, c1, c2, p1, tolerance, points, stack);
                }

                int count = points.Length - start;

                if (closed && count > 1 &&
                    (points[points.Length - 1] - points[start]).sqrMagnitude < EPSILON * EPSILON)
                {
                    points.RemoveAt(points.Length - 1);
                    --count;
                }

                if (count >= 2)
                {
                    ranges.Add(new PolyRange { start = start, count = count, closed = closed ? (byte)1 : (byte)0 });
                }
                else
                {
                    points.ResizeUninitialized(start);
                }
            }
        }

        // ------------------------------------------------------------------
        // Fill job
        // ------------------------------------------------------------------

        struct ScanEdge
        {
            public float yTop, yBottom, xTop, slope;
            public int winding;
            public byte isClip;

            public float XAt(float y)
            {
                return xTop + (y - yTop) * slope;
            }
        }

        struct SlabEdge
        {
            public float xTop, xBottom, xMiddle;
            public int winding;
            public byte isClip;
        }

        [BurstCompile]
        struct FillJob : IJob
        {
            [ReadOnly] public NativeArray<float> packedContours;
            public int contourCount;
            public float tolerance;

            [ReadOnly] public NativeArray<Vector2> clipPoints;
            [ReadOnly] public NativeArray<PolyRange> clipRanges;
            public bool hasClip;
            public bool clipInvert;
            public bool evenOdd;

            public BurstPaint paint;
            [ReadOnly] public NativeArray<float> gradientStops;
            public float aaWidth;
            public float gradientSpan;

            public NativeList<Vector2> outPositions;
            public NativeList<Vector4> outColors;
            public NativeList<int> outIndices;
            public NativeArray<Vector2> outBounds;
            public NativeArray<int> outHasBounds;
            public int baseVertex;

            public void Execute()
            {
                var points = new NativeList<Vector2>(256, Allocator.Temp);
                var ranges = new NativeList<PolyRange>(8, Allocator.Temp);
                FlattenPacked(packedContours, contourCount, tolerance, points, ranges);

                if (ranges.Length == 0)
                    return;

                var outputs = new Outputs
                {
                    positions = outPositions,
                    colors = outColors,
                    indices = outIndices,
                    bounds = outBounds,
                    hasBounds = outHasBounds,
                    baseVertex = baseVertex
                };

                TessellateFill(points, ranges, ref outputs);
                EmitFillFringe(points, ranges, ref outputs);
            }

            void TessellateFill(NativeList<Vector2> points, NativeList<PolyRange> ranges, ref Outputs outputs)
            {
                var edges = new NativeList<ScanEdge>(256, Allocator.Temp);
                var slabYs = new NativeList<float>(256, Allocator.Temp);

                for (int i = 0; i < ranges.Length; ++i)
                    CollectEdges(points, ranges[i], false, edges, slabYs);

                if (hasClip)
                {
                    for (int i = 0; i < clipRanges.Length; ++i)
                        CollectEdges(clipPoints, clipRanges[i], true, edges, slabYs);
                }

                if (edges.Length == 0)
                    return;

                SortFloats(slabYs, 0, slabYs.Length - 1);

                int uniqueYs = 0;

                for (int i = 0; i < slabYs.Length; ++i)
                {
                    if (uniqueYs == 0 || slabYs[i] - slabYs[uniqueYs - 1] > EPSILON)
                        slabYs[uniqueYs++] = slabYs[i];
                }

                slabYs.Length = uniqueYs;

                int edgeCount = edges.Length;
                var edgeSortKeys = new NativeArray<float>(edgeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var edgeSortOrder = new NativeArray<int>(edgeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < edgeCount; ++i)
                {
                    edgeSortKeys[i] = edges[i].yTop;
                    edgeSortOrder[i] = i;
                }

                SortFloatsWithIndices(edgeSortKeys, edgeSortOrder, 0, edgeCount - 1);

                var activeEdges = new NativeList<int>(64, Allocator.Temp);
                var slabEdges = new NativeList<SlabEdge>(64, Allocator.Temp);
                int nextEdge = 0;
                float maxSlabHeight = paint.isGradient && gradientSpan > 0f ? gradientSpan : float.MaxValue;

                for (int slab = 0; slab + 1 < slabYs.Length; ++slab)
                {
                    float slabTop = slabYs[slab];
                    float slabBottom = slabYs[slab + 1];

                    if (slabBottom - slabTop < EPSILON)
                        continue;

                    while (nextEdge < edgeCount && edgeSortKeys[nextEdge] <= slabTop + EPSILON)
                    {
                        activeEdges.Add(edgeSortOrder[nextEdge]);
                        ++nextEdge;
                    }

                    int activeCount = 0;

                    for (int i = 0; i < activeEdges.Length; ++i)
                    {
                        int edgeIndex = activeEdges[i];

                        if (edges[edgeIndex].yBottom > slabTop + EPSILON)
                            activeEdges[activeCount++] = edgeIndex;
                    }

                    activeEdges.Length = activeCount;

                    if (activeCount == 0)
                        continue;

                    slabEdges.Clear();

                    for (int i = 0; i < activeCount; ++i)
                    {
                        ScanEdge edge = edges[activeEdges[i]];

                        SlabEdge slabEdge;
                        slabEdge.xTop = edge.XAt(slabTop);
                        slabEdge.xBottom = edge.XAt(slabBottom);
                        slabEdge.xMiddle = (slabEdge.xTop + slabEdge.xBottom) * 0.5f;
                        slabEdge.winding = edge.winding;
                        slabEdge.isClip = edge.isClip;
                        slabEdges.Add(slabEdge);
                    }

                    SortSlabEdges(slabEdges);

                    int verticalChunks = Mathf.Max(1, Mathf.CeilToInt((slabBottom - slabTop) / maxSlabHeight));
                    float inverseChunks = 1f / verticalChunks;

                    for (int chunk = 0; chunk < verticalChunks; ++chunk)
                    {
                        float fractionTop = chunk * inverseChunks;
                        float fractionBottom = chunk == verticalChunks - 1 ? 1f : fractionTop + inverseChunks;
                        float ya = Mathf.Lerp(slabTop, slabBottom, fractionTop);
                        float yb = Mathf.Lerp(slabTop, slabBottom, fractionBottom);
                        EmitSlabSpans(slabEdges, ya, yb, fractionTop, fractionBottom, ref outputs);
                    }
                }
            }

            void EmitSlabSpans(
                NativeList<SlabEdge> slabEdges,
                float ya,
                float yb,
                float fractionTop,
                float fractionBottom,
                ref Outputs outputs)
            {
                int shapeWinding = 0;
                int clipWinding = 0;
                bool spanOpen = false;
                float spanTopX = 0f;
                float spanBottomX = 0f;

                for (int i = 0; i < slabEdges.Length; ++i)
                {
                    SlabEdge edge = slabEdges[i];
                    bool insideBefore = IsInside(shapeWinding, clipWinding);

                    if (edge.isClip != 0)
                        clipWinding += edge.winding;
                    else
                        shapeWinding += edge.winding;

                    bool insideAfter = IsInside(shapeWinding, clipWinding);

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
                        EmitTrapezoid(spanTopX, edgeTopX, spanBottomX, edgeBottomX, ya, yb, ref outputs);
                        spanOpen = false;
                    }
                }
            }

            bool IsInside(int shapeWinding, int clipWinding)
            {
                bool shapeInside = evenOdd ? (shapeWinding & 1) != 0 : shapeWinding != 0;

                if (!shapeInside)
                    return false;

                if (!hasClip)
                    return true;

                bool clipInside = clipWinding != 0;
                return clipInvert ? !clipInside : clipInside;
            }

            void EmitTrapezoid(
                float topLeft,
                float topRight,
                float bottomLeft,
                float bottomRight,
                float ya,
                float yb,
                ref Outputs outputs)
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

                    int ia = outputs.AddVertex(a, ColorAt(paint, gradientStops, a));
                    int ib = outputs.AddVertex(b, ColorAt(paint, gradientStops, b));
                    int ic = outputs.AddVertex(c, ColorAt(paint, gradientStops, c));
                    int id = outputs.AddVertex(d, ColorAt(paint, gradientStops, d));
                    outputs.AddQuad(ia, ib, ic, id);
                }
            }

            static void CollectEdges<TPoints>(
                TPoints points,
                PolyRange range,
                bool isClip,
                NativeList<ScanEdge> edges,
                NativeList<float> slabYs)
                where TPoints : struct, IReadOnlyPoints
            {
                int count = range.count;

                if (count < 2)
                    return;

                for (int i = 0; i < count; ++i)
                {
                    Vector2 from = points.Get(range.start + i);
                    Vector2 to = points.Get(range.start + (i + 1) % count);

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
                    edge.isClip = isClip ? (byte)1 : (byte)0;

                    edges.Add(edge);
                    slabYs.Add(edge.yTop);
                    slabYs.Add(edge.yBottom);
                }
            }

            static void CollectEdges(NativeList<Vector2> points, PolyRange range, bool isClip, NativeList<ScanEdge> edges, NativeList<float> slabYs)
            {
                CollectEdges(new ListPoints { list = points }, range, isClip, edges, slabYs);
            }

            static void CollectEdges(NativeArray<Vector2> points, PolyRange range, bool isClip, NativeList<ScanEdge> edges, NativeList<float> slabYs)
            {
                CollectEdges(new ArrayPoints { array = points }, range, isClip, edges, slabYs);
            }

            void EmitFillFringe(NativeList<Vector2> points, NativeList<PolyRange> ranges, ref Outputs outputs)
            {
                if (aaWidth <= 0f)
                    return;

                bool fringeHasClip = hasClip && clipRanges.Length > 0;
                var fringeNormals = new NativeList<Vector2>(128, Allocator.Temp);
                var fringeInside = new NativeList<byte>(128, Allocator.Temp);

                for (int contourIndex = 0; contourIndex < ranges.Length; ++contourIndex)
                {
                    PolyRange range = ranges[contourIndex];

                    if (range.count < 3)
                        continue;

                    float area = SignedArea(points, range);

                    if (Mathf.Abs(area) < EPSILON)
                        continue;

                    int depth = ContainmentDepth(points, ranges, contourIndex);
                    float direction = Mathf.Sign(area) * ((depth & 1) == 0 ? 1f : -1f);

                    fringeNormals.Clear();
                    int count = range.count;

                    for (int i = 0; i < count; ++i)
                    {
                        Vector2 previous = points[range.start + (i - 1 + count) % count];
                        Vector2 current = points[range.start + i];
                        Vector2 next = points[range.start + (i + 1) % count];

                        Vector2 inEdge = current - previous;
                        Vector2 outEdge = next - current;

                        Vector2 inNormal = Normalize(new Vector2(inEdge.y, -inEdge.x));
                        Vector2 outNormal = Normalize(new Vector2(outEdge.y, -outEdge.x));
                        Vector2 average = Normalize(inNormal + outNormal);

                        if (average == Vector2.zero)
                            average = inNormal;

                        float miter = Vector2.Dot(average, outNormal);
                        float scale = 1f / Mathf.Max(0.35f, miter);
                        fringeNormals.Add(average * (scale * direction * aaWidth));
                    }

                    fringeInside.Clear();

                    for (int i = 0; i < count; ++i)
                    {
                        bool inside = true;

                        if (fringeHasClip)
                        {
                            inside = ClipWindingAt(points[range.start + i]) != 0;

                            if (clipInvert)
                                inside = !inside;
                        }

                        fringeInside.Add(inside ? (byte)1 : (byte)0);
                    }

                    int firstInner = -1;
                    int previousInner = -1;
                    int previousOuter = -1;

                    for (int i = 0; i < count; ++i)
                    {
                        Vector2 position = points[range.start + i];
                        Vector4 innerColor = ColorAt(paint, gradientStops, position);
                        Vector4 outerColor = innerColor;
                        outerColor.w = 0f;

                        int inner = outputs.AddVertex(position, innerColor);
                        int outer = outputs.AddVertex(position + fringeNormals[i], outerColor);

                        if (i == 0)
                        {
                            firstInner = inner;
                        }
                        else if (fringeInside[i - 1] != 0 && fringeInside[i] != 0)
                        {
                            outputs.AddQuad(previousInner, previousOuter, outer, inner);
                        }

                        previousInner = inner;
                        previousOuter = outer;
                    }

                    if (fringeInside[count - 1] != 0 && fringeInside[0] != 0)
                        outputs.AddQuad(previousInner, previousOuter, firstInner + 1, firstInner);
                }
            }

            int ClipWindingAt(Vector2 probe)
            {
                int winding = 0;

                for (int i = 0; i < clipRanges.Length; ++i)
                    winding += WindingNumber(clipPoints, clipRanges[i], probe);

                return winding;
            }

            static float SignedArea(NativeList<Vector2> points, PolyRange range)
            {
                float area = 0f;

                for (int i = 0; i < range.count; ++i)
                {
                    Vector2 current = points[range.start + i];
                    Vector2 next = points[range.start + (i + 1) % range.count];
                    area += current.x * next.y - next.x * current.y;
                }

                return area * 0.5f;
            }

            static int ContainmentDepth(NativeList<Vector2> points, NativeList<PolyRange> ranges, int contourIndex)
            {
                Vector2 probe = points[ranges[contourIndex].start];
                int depth = 0;

                for (int i = 0; i < ranges.Length; ++i)
                {
                    if (i == contourIndex)
                        continue;

                    if (WindingNumber(points, ranges[i], probe) != 0)
                        ++depth;
                }

                return depth;
            }
        }

        // Burst can't use interfaces with boxing, but generic struct constraints are fine.
        interface IReadOnlyPoints
        {
            Vector2 Get(int index);
        }

        struct ListPoints : IReadOnlyPoints
        {
            public NativeList<Vector2> list;

            public Vector2 Get(int index) => list[index];
        }

        struct ArrayPoints : IReadOnlyPoints
        {
            public NativeArray<Vector2> array;

            public Vector2 Get(int index) => array[index];
        }

        static int WindingNumber(NativeList<Vector2> points, PolyRange range, Vector2 probe)
        {
            return WindingNumber(new ListPoints { list = points }, range, probe);
        }

        static int WindingNumber(NativeArray<Vector2> points, PolyRange range, Vector2 probe)
        {
            return WindingNumber(new ArrayPoints { array = points }, range, probe);
        }

        static int WindingNumber<TPoints>(TPoints points, PolyRange range, Vector2 probe)
            where TPoints : struct, IReadOnlyPoints
        {
            int winding = 0;
            int count = range.count;

            for (int i = 0; i < count; ++i)
            {
                Vector2 from = points.Get(range.start + i);
                Vector2 to = points.Get(range.start + (i + 1) % count);

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
        // Sorts (ports of the scalar in-place sorts)
        // ------------------------------------------------------------------

        static void SortFloats(NativeList<float> values, int low, int high)
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

        static void SortFloatsWithIndices(NativeArray<float> keys, NativeArray<int> values, int low, int high)
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

        static void SwapKeyValue(NativeArray<float> keys, NativeArray<int> values, int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }

        static void SortSlabEdges(NativeList<SlabEdge> edges)
        {
            int count = edges.Length;

            for (int i = 1; i < count; ++i)
            {
                SlabEdge current = edges[i];
                int j = i - 1;

                while (j >= 0 && edges[j].xMiddle > current.xMiddle)
                {
                    edges[j + 1] = edges[j];
                    --j;
                }

                edges[j + 1] = current;
            }
        }

        // ------------------------------------------------------------------
        // Stroke job
        // ------------------------------------------------------------------

        [BurstCompile]
        struct StrokeJob : IJob
        {
            [ReadOnly] public NativeArray<float> packedContours;
            public int contourCount;
            public float tolerance;

            public float halfWidth;
            public int cap;

            public BurstPaint paint;
            [ReadOnly] public NativeArray<float> gradientStops;
            public float aaWidth;

            public NativeList<Vector2> outPositions;
            public NativeList<Vector4> outColors;
            public NativeList<int> outIndices;
            public NativeArray<Vector2> outBounds;
            public NativeArray<int> outHasBounds;
            public int baseVertex;

            public void Execute()
            {
                if (halfWidth <= 0f)
                    return;

                var points = new NativeList<Vector2>(256, Allocator.Temp);
                var ranges = new NativeList<PolyRange>(8, Allocator.Temp);
                FlattenPacked(packedContours, contourCount, tolerance, points, ranges);

                var outputs = new Outputs
                {
                    positions = outPositions,
                    colors = outColors,
                    indices = outIndices,
                    bounds = outBounds,
                    hasBounds = outHasBounds,
                    baseVertex = baseVertex
                };

                var strokePoints = new NativeList<Vector2>(256, Allocator.Temp);
                var strokeNormals = new NativeList<Vector2>(256, Allocator.Temp);

                for (int i = 0; i < ranges.Length; ++i)
                    EmitStrokePolyline(points, ranges[i], strokePoints, strokeNormals, ref outputs);
            }

            void EmitStrokePolyline(
                NativeList<Vector2> source,
                PolyRange range,
                NativeList<Vector2> strokePoints,
                NativeList<Vector2> strokeNormals,
                ref Outputs outputs)
            {
                strokePoints.Clear();

                for (int i = 0; i < range.count; ++i)
                {
                    Vector2 point = source[range.start + i];

                    if (strokePoints.Length == 0 ||
                        (point - strokePoints[strokePoints.Length - 1]).sqrMagnitude > EPSILON * EPSILON)
                    {
                        strokePoints.Add(point);
                    }
                }

                bool closed = range.closed != 0;

                if (closed && strokePoints.Length > 1 &&
                    (strokePoints[strokePoints.Length - 1] - strokePoints[0]).sqrMagnitude < EPSILON * EPSILON)
                {
                    strokePoints.RemoveAt(strokePoints.Length - 1);
                }

                int count = strokePoints.Length;

                if (count < 2)
                    return;

                if (!closed && cap == 3)
                {
                    Vector2 startDirection = Normalize(strokePoints[0] - strokePoints[1]);
                    Vector2 endDirection = Normalize(strokePoints[count - 1] - strokePoints[count - 2]);
                    strokePoints[0] += startDirection * halfWidth;
                    strokePoints[count - 1] += endDirection * halfWidth;
                }

                float innerWidth = Mathf.Max(halfWidth - aaWidth, 0f);
                float outerWidth = halfWidth + aaWidth;
                float coreAlpha = halfWidth < aaWidth ? halfWidth / aaWidth : 1f;

                strokeNormals.Clear();

                for (int i = 0; i < count; ++i)
                {
                    Vector2 inDirection;
                    Vector2 outDirection;

                    if (closed)
                    {
                        inDirection = Normalize(strokePoints[i] - strokePoints[(i - 1 + count) % count]);
                        outDirection = Normalize(strokePoints[(i + 1) % count] - strokePoints[i]);
                    }
                    else
                    {
                        inDirection = i > 0 ? Normalize(strokePoints[i] - strokePoints[i - 1]) : Vector2.zero;
                        outDirection = i < count - 1 ? Normalize(strokePoints[i + 1] - strokePoints[i]) : Vector2.zero;

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
                    strokeNormals.Add(average * scale);
                }

                int firstRing = -1;
                int previousRing = -1;

                for (int i = 0; i < count; ++i)
                {
                    Vector2 position = strokePoints[i];
                    Vector2 normal = strokeNormals[i];

                    Vector4 coreColor = ColorAt(paint, gradientStops, position);
                    coreColor.w *= coreAlpha;
                    Vector4 edgeColor = coreColor;
                    edgeColor.w = 0f;

                    int ring = outputs.AddVertex(position + normal * outerWidth, edgeColor);
                    outputs.AddVertex(position + normal * innerWidth, coreColor);
                    outputs.AddVertex(position - normal * innerWidth, coreColor);
                    outputs.AddVertex(position - normal * outerWidth, edgeColor);

                    if (i == 0)
                        firstRing = ring;

                    if (i > 0)
                        ConnectStrokeRings(ref outputs, previousRing, ring);

                    previousRing = ring;
                }

                if (closed)
                    ConnectStrokeRings(ref outputs, previousRing, firstRing);

                if (!closed && cap == 2)
                {
                    Vector2 startDirection = Normalize(strokePoints[0] - strokePoints[1]);
                    Vector2 endDirection = Normalize(strokePoints[count - 1] - strokePoints[count - 2]);
                    EmitRoundCap(strokePoints[0], startDirection, innerWidth, outerWidth, coreAlpha, ref outputs);
                    EmitRoundCap(strokePoints[count - 1], endDirection, innerWidth, outerWidth, coreAlpha, ref outputs);
                }
            }

            static void ConnectStrokeRings(ref Outputs outputs, int ringA, int ringB)
            {
                outputs.AddQuad(ringA + 0, ringB + 0, ringB + 1, ringA + 1);
                outputs.AddQuad(ringA + 1, ringB + 1, ringB + 2, ringA + 2);
                outputs.AddQuad(ringA + 2, ringB + 2, ringB + 3, ringA + 3);
            }

            void EmitRoundCap(
                Vector2 center,
                Vector2 direction,
                float innerWidth,
                float outerWidth,
                float coreAlpha,
                ref Outputs outputs)
            {
                Vector2 normal = new Vector2(direction.y, -direction.x);

                Vector4 coreColor = ColorAt(paint, gradientStops, center);
                coreColor.w *= coreAlpha;
                Vector4 edgeColor = coreColor;
                edgeColor.w = 0f;

                int segments = Mathf.Clamp(Mathf.CeilToInt(outerWidth * 0.6f) + 3, 4, 24);
                int centerIndex = outputs.AddVertex(center, coreColor);

                int previousInner = -1;
                int previousOuter = -1;

                for (int i = 0; i <= segments; ++i)
                {
                    float angle = Mathf.PI * i / segments;
                    Vector2 radial = normal * Mathf.Cos(angle) + direction * Mathf.Sin(angle);

                    int inner = outputs.AddVertex(center + radial * innerWidth, coreColor);
                    int outer = outputs.AddVertex(center + radial * outerWidth, edgeColor);

                    if (i > 0)
                    {
                        outputs.AddTriangle(centerIndex, previousInner, inner);
                        outputs.AddQuad(previousInner, previousOuter, outer, inner);
                    }

                    previousInner = inner;
                    previousOuter = outer;
                }
            }
        }

        // ------------------------------------------------------------------
        // Managed wrappers
        // ------------------------------------------------------------------

        public static bool TryFill(
            NowLottieContourSet contours,
            List<NowLottiePolyline> clipPolylines,
            bool clipInvert,
            bool evenOdd,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth,
            float gradientSpan,
            float tolerance)
        {
            if (forceScalar || contours.isEmpty)
                return false;

            var packed = CopyPacked(contours);
            var (clipPoints, clipRanges, hasClip) = CopyClip(clipPolylines);
            var stops = CopyStops(paint);
            var outputs = AllocateOutputs();

            try
            {
                new FillJob
                {
                    packedContours = packed,
                    contourCount = contours.contourCount,
                    tolerance = tolerance,
                    clipPoints = clipPoints,
                    clipRanges = clipRanges,
                    hasClip = hasClip,
                    clipInvert = clipInvert,
                    evenOdd = evenOdd,
                    paint = ToBurstPaint(paint),
                    gradientStops = stops,
                    aaWidth = aaWidth,
                    gradientSpan = gradientSpan,
                    outPositions = outputs.positions,
                    outColors = outputs.colors,
                    outIndices = outputs.indices,
                    outBounds = outputs.bounds,
                    outHasBounds = outputs.hasBounds,
                    baseVertex = buffer.positions.count
                }.Run();

                CopyOutputs(outputs, buffer);
                return true;
            }
            finally
            {
                DisposeOutputs(outputs);
                packed.Dispose();
                clipPoints.Dispose();
                clipRanges.Dispose();
                stops.Dispose();
            }
        }

        public static bool TryStroke(
            NowLottieContourSet contours,
            float width,
            int cap,
            int join,
            in NowLottiePaint paint,
            NowLottieDrawBuffer buffer,
            float aaWidth,
            float tolerance)
        {
            _ = join; // joins are folded into the shared miter logic, matching the scalar path

            if (forceScalar || contours.isEmpty || width * 0.5f <= 0f)
                return false;

            var packed = CopyPacked(contours);
            var stops = CopyStops(paint);
            var outputs = AllocateOutputs();

            try
            {
                new StrokeJob
                {
                    packedContours = packed,
                    contourCount = contours.contourCount,
                    tolerance = tolerance,
                    halfWidth = width * 0.5f,
                    cap = cap,
                    paint = ToBurstPaint(paint),
                    gradientStops = stops,
                    aaWidth = aaWidth,
                    outPositions = outputs.positions,
                    outColors = outputs.colors,
                    outIndices = outputs.indices,
                    outBounds = outputs.bounds,
                    outHasBounds = outputs.hasBounds,
                    baseVertex = buffer.positions.count
                }.Run();

                CopyOutputs(outputs, buffer);
                return true;
            }
            finally
            {
                DisposeOutputs(outputs);
                packed.Dispose();
                stops.Dispose();
            }
        }

        struct OutputBuffers
        {
            public NativeList<Vector2> positions;
            public NativeList<Vector4> colors;
            public NativeList<int> indices;
            public NativeArray<Vector2> bounds;
            public NativeArray<int> hasBounds;
        }

        static OutputBuffers AllocateOutputs()
        {
            return new OutputBuffers
            {
                positions = new NativeList<Vector2>(1024, Allocator.TempJob),
                colors = new NativeList<Vector4>(1024, Allocator.TempJob),
                indices = new NativeList<int>(2048, Allocator.TempJob),
                bounds = new NativeArray<Vector2>(2, Allocator.TempJob),
                hasBounds = new NativeArray<int>(1, Allocator.TempJob)
            };
        }

        static void DisposeOutputs(OutputBuffers outputs)
        {
            outputs.positions.Dispose();
            outputs.colors.Dispose();
            outputs.indices.Dispose();
            outputs.bounds.Dispose();
            outputs.hasBounds.Dispose();
        }

        static NativeArray<float> CopyPacked(NowLottieContourSet contours)
        {
            var packed = new NativeArray<float>(Mathf.Max(1, contours.data.count), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float>.Copy(contours.data.array, packed, contours.data.count);
            return packed;
        }

        static (NativeArray<Vector2>, NativeArray<PolyRange>, bool) CopyClip(List<NowLottiePolyline> clipPolylines)
        {
            if (clipPolylines == null || clipPolylines.Count == 0)
            {
                return (
                    new NativeArray<Vector2>(1, Allocator.TempJob),
                    new NativeArray<PolyRange>(1, Allocator.TempJob),
                    false);
            }

            int totalPoints = 0;

            for (int i = 0; i < clipPolylines.Count; ++i)
                totalPoints += clipPolylines[i].points.Count;

            var points = new NativeArray<Vector2>(Mathf.Max(1, totalPoints), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var ranges = new NativeArray<PolyRange>(clipPolylines.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int cursor = 0;

            for (int i = 0; i < clipPolylines.Count; ++i)
            {
                var polyline = clipPolylines[i];

                ranges[i] = new PolyRange
                {
                    start = cursor,
                    count = polyline.points.Count,
                    closed = polyline.closed ? (byte)1 : (byte)0
                };

                for (int p = 0; p < polyline.points.Count; ++p)
                    points[cursor++] = polyline.points[p];
            }

            return (points, ranges, true);
        }

        static void CopyOutputs(OutputBuffers outputs, NowLottieDrawBuffer buffer)
        {
            int vertexCount = outputs.positions.Length;
            int indexCount = outputs.indices.Length;

            if (vertexCount == 0)
                return;

            buffer.positions.EnsureCapacity(vertexCount);
            buffer.colors.EnsureCapacity(vertexCount);
            buffer.indices.EnsureCapacity(indexCount);

            NativeArray<Vector2>.Copy(outputs.positions.AsArray(), 0, buffer.positions.array, buffer.positions.count, vertexCount);
            NativeArray<Vector4>.Copy(outputs.colors.AsArray(), 0, buffer.colors.array, buffer.colors.count, vertexCount);
            NativeArray<int>.Copy(outputs.indices.AsArray(), 0, buffer.indices.array, buffer.indices.count, indexCount);

            buffer.positions.count += vertexCount;
            buffer.colors.count += vertexCount;
            buffer.indices.count += indexCount;

            if (outputs.hasBounds[0] != 0)
            {
                if (!buffer.hasBounds)
                {
                    buffer.boundsMin = outputs.bounds[0];
                    buffer.boundsMax = outputs.bounds[1];
                    buffer.hasBounds = true;
                }
                else
                {
                    buffer.boundsMin = Vector2.Min(buffer.boundsMin, outputs.bounds[0]);
                    buffer.boundsMax = Vector2.Max(buffer.boundsMax, outputs.bounds[1]);
                }
            }
        }
    }
}
