using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using NowUI.Internal;

/// <summary>
/// The Burst tessellator must be a line-for-line port of the scalar one, so these
/// tests run identical inputs through both and compare every vertex, color, index,
/// and the bounds. Any divergence is a porting bug, not acceptable drift.
/// </summary>
public class NowLottieBurstTessellatorTests
{
    const float Tolerance = 0.25f;
    const float AaWidth = 0.75f;

    [TearDown]
    public void ResetFlags()
    {
        NowLottieBurstTessellator.forceScalar = false;
    }

    static void AppendContour(NowLottieContourSet set, bool closed, params (Vector2 p, Vector2 tin, Vector2 tout)[] points)
    {
        set.data.EnsureCapacity(2 + points.Length * 6);
        var array = set.data.array;
        int cursor = set.data.count;

        array[cursor++] = points.Length;
        array[cursor++] = closed ? 1f : 0f;

        foreach (var (p, tin, tout) in points)
        {
            array[cursor++] = p.x;
            array[cursor++] = p.y;
            array[cursor++] = tin.x;
            array[cursor++] = tin.y;
            array[cursor++] = tout.x;
            array[cursor++] = tout.y;
        }

        set.data.count = cursor;
        ++set.contourCount;
    }

    /// <summary>Circle as four cubic segments (kappa tangents), optionally reversed (hole).</summary>
    static void AppendCircle(NowLottieContourSet set, Vector2 center, float radius, bool reversed = false)
    {
        float k = radius * 0.5523f;
        var points = new (Vector2, Vector2, Vector2)[]
        {
            (center + new Vector2(radius, 0), new Vector2(0, k), new Vector2(0, -k)),
            (center + new Vector2(0, -radius), new Vector2(k, 0), new Vector2(-k, 0)),
            (center + new Vector2(-radius, 0), new Vector2(0, -k), new Vector2(0, k)),
            (center + new Vector2(0, radius), new Vector2(-k, 0), new Vector2(k, 0))
        };

        if (reversed)
        {
            System.Array.Reverse(points);

            for (int i = 0; i < points.Length; ++i)
                points[i] = (points[i].Item1, points[i].Item3, points[i].Item2);
        }

        AppendContour(set, true, points);
    }

    static void AppendOpenWave(NowLottieContourSet set)
    {
        AppendContour(set, false,
            (new Vector2(5, 50), Vector2.zero, new Vector2(15, -30)),
            (new Vector2(50, 50), new Vector2(-15, 30), new Vector2(15, -30)),
            (new Vector2(95, 50), new Vector2(-15, 30), Vector2.zero));
    }

    static List<NowLottiePolyline> MakeClipSquare()
    {
        var polyline = new NowLottiePolyline { closed = true };
        polyline.points.Add(new Vector2(10, 10));
        polyline.points.Add(new Vector2(80, 10));
        polyline.points.Add(new Vector2(80, 80));
        polyline.points.Add(new Vector2(10, 80));
        return new List<NowLottiePolyline> { polyline };
    }

    static NowLottiePaint MakeGradientPaint()
    {
        return new NowLottiePaint
        {
            isGradient = true,
            color = Vector4.one,
            gradientType = 1,
            gradientStart = new Vector2(0, 0),
            gradientEnd = new Vector2(100, 0),
            gradientStops = new float[]
            {
                0f, 1f, 0f, 0f,
                0.5f, 0f, 1f, 0f,
                1f, 0f, 0f, 1f,
                0f, 1f,
                1f, 0.5f
            },
            gradientStopDataLength = 16,
            colorStopCount = 3,
            alphaMultiplier = 1f
        };
    }

    static NowLottieDrawBuffer ScalarFill(
        NowLottieContourSet set,
        List<NowLottiePolyline> clip,
        bool clipInvert,
        bool evenOdd,
        in NowLottiePaint paint,
        float gradientSpan)
    {
        var buffer = new NowLottieDrawBuffer();
        buffer.AddVertex(new Vector2(-999, -999), Vector4.zero); // base-vertex offset check

        var polylines = new List<NowLottiePolyline>();
        NowLottieTessellator.FlattenPackedContours(set, Tolerance, polylines);

        if (polylines.Count > 0)
        {
            NowLottieTessellator.TessellateFill(polylines, clip, clipInvert, evenOdd, paint, buffer, AaWidth, gradientSpan);
            NowLottieTessellator.EmitFillFringe(polylines, clip, clipInvert, paint, buffer, AaWidth);
        }

        NowLottiePolylinePool.ReleaseAll(polylines);
        return buffer;
    }

    static NowLottieDrawBuffer BurstFill(
        NowLottieContourSet set,
        List<NowLottiePolyline> clip,
        bool clipInvert,
        bool evenOdd,
        in NowLottiePaint paint,
        float gradientSpan)
    {
        var buffer = new NowLottieDrawBuffer();
        buffer.AddVertex(new Vector2(-999, -999), Vector4.zero);

        Assert.IsTrue(
            NowLottieBurstTessellator.TryFill(set, clip, clipInvert, evenOdd, paint, buffer, AaWidth, gradientSpan, Tolerance),
            "Burst fill path declined to run.");
        return buffer;
    }

    static void AssertBuffersIdentical(NowLottieDrawBuffer expected, NowLottieDrawBuffer actual)
    {
        Assert.Greater(expected.positions.count, 1, "Scalar reference produced no geometry; test input is broken.");
        Assert.AreEqual(expected.positions.count, actual.positions.count, "Vertex counts differ.");
        Assert.AreEqual(expected.indices.count, actual.indices.count, "Index counts differ.");

        for (int i = 0; i < expected.positions.count; ++i)
        {
            Assert.AreEqual(expected.positions.array[i].x, actual.positions.array[i].x, 0.001f, $"Vertex {i} x");
            Assert.AreEqual(expected.positions.array[i].y, actual.positions.array[i].y, 0.001f, $"Vertex {i} y");

            Vector4 expectedColor = expected.colors.array[i];
            Vector4 actualColor = actual.colors.array[i];
            Assert.AreEqual(expectedColor.x, actualColor.x, 0.001f, $"Vertex {i} color r");
            Assert.AreEqual(expectedColor.w, actualColor.w, 0.001f, $"Vertex {i} color a");
        }

        for (int i = 0; i < expected.indices.count; ++i)
            Assert.AreEqual(expected.indices.array[i], actual.indices.array[i], $"Index {i}");

        Assert.AreEqual(expected.boundsMin.x, actual.boundsMin.x, 0.001f);
        Assert.AreEqual(expected.boundsMax.y, actual.boundsMax.y, 0.001f);
    }

    [Test]
    public void FillMatchesScalarForCompoundPath()
    {
        var set = new NowLottieContourSet();
        AppendCircle(set, new Vector2(50, 50), 40f);
        AppendCircle(set, new Vector2(50, 50), 15f, reversed: true); // hole

        var paint = NowLottiePaint.Solid(new Vector4(1f, 0.5f, 0.25f, 0.9f));

        AssertBuffersIdentical(
            ScalarFill(set, null, false, false, paint, 0f),
            BurstFill(set, null, false, false, paint, 0f));
    }

    [Test]
    public void FillMatchesScalarWithEvenOddRule()
    {
        var set = new NowLottieContourSet();
        AppendCircle(set, new Vector2(50, 50), 40f);
        AppendCircle(set, new Vector2(60, 50), 30f); // overlapping, same winding

        var paint = NowLottiePaint.Solid(Vector4.one);

        AssertBuffersIdentical(
            ScalarFill(set, null, false, true, paint, 0f),
            BurstFill(set, null, false, true, paint, 0f));
    }

    [Test]
    public void FillMatchesScalarWithClipAndGradient()
    {
        var set = new NowLottieContourSet();
        AppendCircle(set, new Vector2(50, 50), 40f);

        var clip = MakeClipSquare();
        var paint = MakeGradientPaint();

        AssertBuffersIdentical(
            ScalarFill(set, clip, false, false, paint, 8f),
            BurstFill(set, clip, false, false, paint, 8f));

        AssertBuffersIdentical(
            ScalarFill(set, clip, true, false, paint, 8f),
            BurstFill(set, clip, true, false, paint, 8f));
    }

    static NowLottieDrawBuffer ScalarStroke(NowLottieContourSet set, float width, int cap, int join, in NowLottiePaint paint)
    {
        var buffer = new NowLottieDrawBuffer();
        buffer.AddVertex(new Vector2(-999, -999), Vector4.zero);

        var polylines = new List<NowLottiePolyline>();
        NowLottieTessellator.FlattenPackedContours(set, Tolerance, polylines);
        NowLottieTessellator.EmitStroke(polylines, width, cap, join, paint, buffer, AaWidth);
        NowLottiePolylinePool.ReleaseAll(polylines);
        return buffer;
    }

    static NowLottieDrawBuffer BurstStroke(NowLottieContourSet set, float width, int cap, int join, in NowLottiePaint paint)
    {
        var buffer = new NowLottieDrawBuffer();
        buffer.AddVertex(new Vector2(-999, -999), Vector4.zero);

        Assert.IsTrue(
            NowLottieBurstTessellator.TryStroke(set, width, cap, join, paint, buffer, AaWidth, Tolerance),
            "Burst stroke path declined to run.");
        return buffer;
    }

    [Test]
    public void StrokeMatchesScalarForClosedContour()
    {
        var set = new NowLottieContourSet();
        AppendCircle(set, new Vector2(50, 50), 35f);

        var paint = NowLottiePaint.Solid(new Vector4(0.2f, 0.6f, 1f, 1f));

        AssertBuffersIdentical(
            ScalarStroke(set, 6f, 1, 1, paint),
            BurstStroke(set, 6f, 1, 1, paint));
    }

    [Test]
    public void StrokeMatchesScalarForOpenPathWithCaps()
    {
        foreach (int cap in new[] { 1, 2, 3 }) // butt, round, square
        {
            var set = new NowLottieContourSet();
            AppendOpenWave(set);

            var paint = NowLottiePaint.Solid(Vector4.one);

            AssertBuffersIdentical(
                ScalarStroke(set, 4f, cap, 1, paint),
                BurstStroke(set, 4f, cap, 1, paint));
        }
    }

    [Test]
    public void ForceScalarDisablesBurstPath()
    {
        var set = new NowLottieContourSet();
        AppendCircle(set, new Vector2(50, 50), 40f);

        NowLottieBurstTessellator.forceScalar = true;

        var buffer = new NowLottieDrawBuffer();
        Assert.IsFalse(NowLottieBurstTessellator.TryFill(
            set, null, false, false, NowLottiePaint.Solid(Vector4.one), buffer, AaWidth, 0f, Tolerance));
        Assert.AreEqual(0, buffer.positions.count);
    }

    [Test, Performance]
    public void ScalarFillBaseline()
    {
        MeasureFill(useBurst: false);
    }

    [Test, Performance]
    public void BurstFillBaseline()
    {
        MeasureFill(useBurst: true);
    }

    void MeasureFill(bool useBurst)
    {
        var set = new NowLottieContourSet();

        for (int i = 0; i < 12; ++i)
            AppendCircle(set, new Vector2(20 + i * 14, 50 + (i % 3) * 20), 18f + (i % 4) * 6f, reversed: i % 2 == 1);

        var paint = MakeGradientPaint();
        var buffer = new NowLottieDrawBuffer();

        Measure.Method(() =>
            {
                buffer.Clear();

                if (useBurst)
                {
                    NowLottieBurstTessellator.TryFill(set, null, false, false, paint, buffer, AaWidth, 8f, Tolerance);
                }
                else
                {
                    var polylines = new List<NowLottiePolyline>();
                    NowLottieTessellator.FlattenPackedContours(set, Tolerance, polylines);
                    NowLottieTessellator.TessellateFill(polylines, null, false, false, paint, buffer, AaWidth, 8f);
                    NowLottieTessellator.EmitFillFringe(polylines, null, false, paint, buffer, AaWidth);
                    NowLottiePolylinePool.ReleaseAll(polylines);
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();
    }
}
