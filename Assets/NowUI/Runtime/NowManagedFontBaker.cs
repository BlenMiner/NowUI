using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NowUI.Internal
{
    /// <summary>
    /// One glyph's raster request for <see cref="NowSdfBakeJob"/>: a slice of the
    /// shared segment buffer plus the cell dimensions and where the cell's RGBA
    /// pixels start in the job output buffer.
    /// </summary>
    internal struct NowSdfGlyphCell
    {
        public int segmentStart;
        public int segmentCount;
        public int width;
        public int height;
        public int outputOffset;
    }

    /// <summary>
    /// Burst-compiled single-channel SDF rasterizer. Each job index bakes one glyph
    /// cell: per pixel, the unsigned distance to the nearest outline segment with the
    /// sign from the nonzero winding rule, encoded like msdfgen (0.5 at the edge,
    /// <see cref="distanceRange"/> pixels across the 0..1 ramp). R, G, and alpha
    /// carry the high byte of a 16-bit normalized distance; B carries the low byte.
    /// Median RGB and the common R/G/alpha single-channel readers therefore remain a valid legacy 8-bit
    /// SDF if a host cannot forward the packed-decoding flag, while NowUI's text
    /// material reconstructs the full value from RB. The extra precision prevents
    /// large adaptive ranges from turning one stored distance step into multiple
    /// screen pixels.
    ///
    /// The field saturates half a range away from the outline, so a pixel only needs
    /// the segments that can be nearest to it within that reach. The cell is split
    /// into <see cref="TILE"/>-pixel tiles, each keeping only the segments whose
    /// distance to the tile center is within two half-diagonals of the nearest one
    /// (and within the saturation reach). The pixel's true nearest segment always
    /// survives, so the output is identical to testing every segment, while large
    /// cells evaluate a handful of segments per pixel instead of the whole outline.
    /// The sign comes from one +x ray crossing pass per row.
    /// </summary>
    [BurstCompile]
    internal struct NowSdfBakeJob : IJobParallelFor
    {
        const int TILE = 8;

        /// <summary>Line segments (x0, y0, x1, y1) in cell-local pixels, y up.</summary>
        [ReadOnly] public NativeArray<float4> segments;

        [ReadOnly] public NativeArray<NowSdfGlyphCell> cells;

        public float distanceRange;

        /// <summary>Nonzero packs a 16-bit scalar into RB; zero preserves replicated legacy RGBA8.</summary>
        public int packedSdf16;

        /// <summary>Per-cell RGBA32 pixels, rows bottom-up, packed per <see cref="NowSdfGlyphCell.outputOffset"/>.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<byte> output;

        static float SegmentDistanceSq(float2 p, float4 segment)
        {
            var a = new float2(segment.x, segment.y);
            var b = new float2(segment.z, segment.w);
            float2 e = b - a;
            float2 w = p - a;
            float lengthSq = math.dot(e, e);
            float t = lengthSq > 1e-12f ? math.saturate(math.dot(w, e) / lengthSq) : 0f;
            float2 d = w - e * t;
            return math.dot(d, d);
        }

        public void Execute(int index)
        {
            NowSdfGlyphCell cell = cells[index];
            float invRange = 1f / distanceRange;
            int segmentStart = cell.segmentStart;
            int segmentCount = cell.segmentCount;
            int segmentEnd = segmentStart + segmentCount;
            int tilesX = (cell.width + TILE - 1) / TILE;
            int tilesY = (cell.height + TILE - 1) / TILE;
            int tileCount = tilesX * tilesY;
            float halfDiagonal = TILE * 0.70710678f;
            // A segment farther than this from a tile's center is more than half a
            // range (plus a pixel of margin) from every pixel in the tile, where the
            // field is saturated whichever segment is nearest.
            float saturationReach = distanceRange * 0.5f + 1f + halfDiagonal;

            var tileStart = new NativeArray<int>(tileCount + 1, Allocator.Temp);
            var centerDistance = new NativeArray<float>(math.max(1, segmentCount), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var tileSegments = new NativeList<int>(math.max(16, segmentCount * 4), Allocator.Temp);

            for (int ty = 0; ty < tilesY; ++ty)
            {
                for (int tx = 0; tx < tilesX; ++tx)
                {
                    var center = new float2(tx * TILE + TILE * 0.5f, ty * TILE + TILE * 0.5f);
                    float nearest = float.MaxValue;

                    for (int s = 0; s < segmentCount; ++s)
                    {
                        float distance = math.sqrt(SegmentDistanceSq(center, segments[segmentStart + s]));
                        centerDistance[s] = distance;
                        nearest = math.min(nearest, distance);
                    }

                    // For a pixel p in the tile, its nearest segment is at most
                    // nearest + halfDiagonal away, so that segment is at most
                    // nearest + 2 * halfDiagonal from the center.
                    float keep = math.min(nearest + 2f * halfDiagonal, saturationReach);
                    tileStart[ty * tilesX + tx] = tileSegments.Length;

                    for (int s = 0; s < segmentCount; ++s)
                    {
                        if (centerDistance[s] <= keep)
                            tileSegments.Add(segmentStart + s);
                    }
                }
            }

            tileStart[tileCount] = tileSegments.Length;

            var crossingX = new NativeArray<float>(math.max(1, segmentCount), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var crossingDirection = new NativeArray<int>(math.max(1, segmentCount), Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int y = 0; y < cell.height; ++y)
            {
                float py = y + 0.5f;
                int row = cell.outputOffset + y * cell.width * 4;
                int crossings = 0;

                // Nonzero winding via a +x ray with half-open spans so shared contour
                // vertices are never counted twice. Every pixel of the row shares the
                // ray's crossings; only which of them lie to its right differs.
                for (int s = segmentStart; s < segmentEnd; ++s)
                {
                    float4 segment = segments[s];
                    var a = new float2(segment.x, segment.y);
                    var b = new float2(segment.z, segment.w);

                    if (a.y <= py ? b.y > py : b.y <= py)
                    {
                        float2 e = b - a;
                        crossingX[crossings] = a.x + (py - a.y) / (b.y - a.y) * e.x;
                        crossingDirection[crossings] = b.y > a.y ? 1 : -1;
                        ++crossings;
                    }
                }

                int tileRow = (y / TILE) * tilesX;

                for (int x = 0; x < cell.width; ++x)
                {
                    float px = x + 0.5f;
                    var p = new float2(px, py);
                    int winding = 0;

                    for (int c = 0; c < crossings; ++c)
                    {
                        if (crossingX[c] > px)
                            winding += crossingDirection[c];
                    }

                    int tile = tileRow + x / TILE;
                    float minDistSq = float.MaxValue;

                    for (int i = tileStart[tile], end = tileStart[tile + 1]; i < end; ++i)
                        minDistSq = math.min(minDistSq, SegmentDistanceSq(p, segments[tileSegments[i]]));

                    float sd = math.sqrt(minDistSq);

                    if (winding == 0)
                        sd = -sd;

                    float normalized = math.saturate(sd * invRange + 0.5f);
                    int offset = row + x * 4;

                    if (packedSdf16 != 0)
                    {
                        uint value = (uint)(normalized * 65535f + 0.5f);
                        byte high = (byte)(value >> 8);
                        byte low = (byte)value;
                        output[offset] = high;
                        output[offset + 1] = high;
                        output[offset + 2] = low;
                        output[offset + 3] = high;
                    }
                    else
                    {
                        byte value = (byte)(normalized * 255f + 0.5f);
                        output[offset] = value;
                        output[offset + 1] = value;
                        output[offset + 2] = value;
                        output[offset + 3] = value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts decoded TrueType outlines into line segments for the SDF job:
    /// reconstructs the quadratic path (implied on-curve midpoints between
    /// consecutive off-curve points) and flattens curves adaptively.
    /// </summary>
    internal static class NowManagedFontBaker
    {
        /// <summary>Maximum chord deviation in pixels when flattening quadratics.</summary>
        const float FLATTEN_TOLERANCE = 0.08f;

        const int MAX_FLATTEN_STEPS = 32;

        /// <summary>
        /// Appends the outline's segments to <paramref name="segments"/> as
        /// (x0, y0, x1, y1), transformed by <paramref name="scale"/> then offset by
        /// -<paramref name="originPx"/> into cell-local pixel coordinates.
        /// </summary>
        public static void Flatten(NowGlyphOutline outline, float scale, Vector2 originPx, List<Vector4> segments)
        {
            int contourStart = 0;

            for (int contour = 0; contour < outline.contourEnds.Count; ++contour)
            {
                int contourEnd = outline.contourEnds[contour];
                FlattenContour(outline, contourStart, contourEnd, scale, originPx, segments);
                contourStart = contourEnd;
            }
        }

        /// <summary>Emits line/quad segments for one TrueType contour. Starts at the first
        /// on-curve point; with none, the contour starts at the midpoint between the last
        /// and first control points (TrueType convention). Two consecutive off-curve points
        /// imply an on-curve midpoint.</summary>
        static void FlattenContour(
            NowGlyphOutline outline,
            int start,
            int end,
            float scale,
            Vector2 originPx,
            List<Vector4> segments)
        {
            int count = end - start;

            if (count < 2)
                return;

            int firstOn = -1;

            for (int i = start; i < end; ++i)
            {
                if (outline.onCurve[i])
                {
                    firstOn = i;
                    break;
                }
            }

            Vector2 Point(int i)
            {
                int wrapped = start + (i - start + count) % count;
                return outline.points[wrapped] * scale - originPx;
            }

            Vector2 startPoint;
            int cursor;

            if (firstOn >= 0)
            {
                startPoint = Point(firstOn);
                cursor = firstOn + 1;
            }
            else
            {
                startPoint = (Point(end - 1) + Point(start)) * 0.5f;
                cursor = start;
            }

            Vector2 current = startPoint;
            Vector2? pendingControl = null;
            int remaining = count;

            while (remaining-- > 0)
            {
                int index = start + (cursor - start + count) % count;
                Vector2 p = Point(index);
                bool on = outline.onCurve[index];
                ++cursor;

                if (on)
                {
                    if (pendingControl.HasValue)
                    {
                        AppendQuad(current, pendingControl.Value, p, segments);
                        pendingControl = null;
                    }
                    else
                    {
                        AppendLine(current, p, segments);
                    }

                    current = p;
                }
                else if (pendingControl.HasValue)
                {
                    Vector2 implied = (pendingControl.Value + p) * 0.5f;
                    AppendQuad(current, pendingControl.Value, implied, segments);
                    current = implied;
                    pendingControl = p;
                }
                else
                {
                    pendingControl = p;
                }
            }

            if (pendingControl.HasValue)
                AppendQuad(current, pendingControl.Value, startPoint, segments);
            else
                AppendLine(current, startPoint, segments);
        }

        static void AppendLine(Vector2 a, Vector2 b, List<Vector4> segments)
        {
            if ((b - a).sqrMagnitude > 1e-12f)
                segments.Add(new Vector4(a.x, a.y, b.x, b.y));
        }

        /// <summary>Flattens a quadratic Bézier; the step count comes from the maximum
        /// deviation between the curve and its chord, |p0 - 2c + p1| / 4.</summary>
        static void AppendQuad(Vector2 p0, Vector2 control, Vector2 p1, List<Vector4> segments)
        {
            Vector2 deviationVector = p0 - 2f * control + p1;
            float deviation = deviationVector.magnitude * 0.25f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(deviation / FLATTEN_TOLERANCE)), 1, MAX_FLATTEN_STEPS);

            Vector2 previous = p0;

            for (int i = 1; i <= steps; ++i)
            {
                float t = (float)i / steps;
                float u = 1f - t;
                Vector2 point = u * u * p0 + 2f * u * t * control + t * t * p1;
                AppendLine(previous, point, segments);
                previous = point;
            }
        }
    }
}
