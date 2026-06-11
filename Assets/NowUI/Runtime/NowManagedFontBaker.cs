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
    /// <see cref="distanceRange"/> pixels across the 0..1 ramp) and replicated into
    /// RGBA. The text shader resolves median(R, G, B), and median(s, s, s) = s, so the
    /// output renders identically to native MTSDF atlases minus sharp-corner
    /// preservation at extreme magnification.
    /// </summary>
    [BurstCompile]
    internal struct NowSdfBakeJob : IJobParallelFor
    {
        /// <summary>Line segments (x0, y0, x1, y1) in cell-local pixels, y up.</summary>
        [ReadOnly] public NativeArray<float4> segments;

        [ReadOnly] public NativeArray<NowSdfGlyphCell> cells;

        public float distanceRange;

        /// <summary>Per-cell RGBA32 pixels, rows bottom-up, packed per <see cref="NowSdfGlyphCell.outputOffset"/>.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<byte> output;

        public void Execute(int index)
        {
            NowSdfGlyphCell cell = cells[index];
            float invRange = 1f / distanceRange;
            int segmentEnd = cell.segmentStart + cell.segmentCount;

            for (int y = 0; y < cell.height; ++y)
            {
                float py = y + 0.5f;
                int row = cell.outputOffset + y * cell.width * 4;

                for (int x = 0; x < cell.width; ++x)
                {
                    float px = x + 0.5f;
                    var p = new float2(px, py);

                    float minDistSq = float.MaxValue;
                    int winding = 0;

                    for (int s = cell.segmentStart; s < segmentEnd; ++s)
                    {
                        float4 seg = segments[s];
                        var a = new float2(seg.x, seg.y);
                        var b = new float2(seg.z, seg.w);
                        float2 e = b - a;
                        float2 w = p - a;

                        float lengthSq = math.dot(e, e);
                        float t = lengthSq > 1e-12f ? math.saturate(math.dot(w, e) / lengthSq) : 0f;
                        float2 d = w - e * t;
                        minDistSq = math.min(minDistSq, math.dot(d, d));

                        // Nonzero winding via a +x ray with half-open spans so shared
                        // contour vertices are never counted twice.
                        if (a.y <= py ? b.y > py : b.y <= py)
                        {
                            float ix = a.x + (py - a.y) / (b.y - a.y) * e.x;

                            if (ix > px)
                                winding += b.y > a.y ? 1 : -1;
                        }
                    }

                    float sd = math.sqrt(minDistSq);

                    if (winding == 0)
                        sd = -sd;

                    byte value = (byte)(math.saturate(sd * invRange + 0.5f) * 255f + 0.5f);
                    int offset = row + x * 4;
                    output[offset] = value;
                    output[offset + 1] = value;
                    output[offset + 2] = value;
                    output[offset + 3] = value;
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

            // Find a starting on-curve point; with none, the contour starts at the
            // midpoint of the first two control points (TrueType convention).
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
                // All points are control points: the contour starts at the midpoint
                // between the last and first ones.
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
                    // Two consecutive off-curve points imply an on-curve midpoint.
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

            // Close the contour back to the start point.
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

        static void AppendQuad(Vector2 p0, Vector2 control, Vector2 p1, List<Vector4> segments)
        {
            // Max deviation between the curve and its chord is |p0 - 2c + p1| / 4.
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
