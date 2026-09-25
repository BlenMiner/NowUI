using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using UnityEngine;

/// <summary>
/// Geometry contracts for the styled stroke builders (<see cref="NowPolyline"/>,
/// <see cref="NowArc"/>) and the closed-path dasher they share with
/// <see cref="NowLine"/>. Geometry is read back from a <see cref="NowDrawList"/>
/// render-layout mesh: positions are UI space with y flipped, and TexCoord3
/// carries the vertex color.
/// </summary>
public class NowStrokeTests
{
    static readonly Vector2 Surface = new Vector2(320f, 240f);

    // EmitLineStroke's anti-aliasing fringe at uiScale 1.
    const float AaWidth = 0.75f;

    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/UIMaterial"));
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
    }

    [Test]
    public void SolidPolylineMatchesDrawPolyline()
    {
        var points = new[]
        {
            new Vector2(12f, 90f),
            new Vector2(60f, 30f),
            new Vector2(60f, 30f),
            new Vector2(130f, 70f),
            new Vector2(200f, 20f)
        };
        var color = new Vector4(0.2f, 0.9f, 0.6f, 1f);

        using var expected = new NowDrawList();

        using (expected.Begin(Surface))
            Now.DrawPolyline(points, 5f, NowLineCap.Round, color);

        using (_drawList.Begin(Surface))
        {
            Now.Polyline(points)
                .SetWidth(5f)
                .SetCap(NowLineCap.Round)
                .SetColor(color)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        CollectionAssert.AreEqual(ReadPositions(expected), ReadPositions(_drawList));
        CollectionAssert.AreEqual(ReadColors(expected), ReadColors(_drawList));
        CollectionAssert.AreEqual(expected.mesh.GetTriangles(0), _drawList.mesh.GetTriangles(0));
    }

    [Test]
    public void PolylineListRangeMatchesArrayRange()
    {
        var array = new[]
        {
            new Vector2(-50f, -50f),
            new Vector2(10f, 10f),
            new Vector2(80f, 60f),
            new Vector2(150f, 20f),
            new Vector2(-50f, -50f)
        };
        var list = new List<Vector2>(array);

        using var expected = new NowDrawList();

        using (expected.Begin(Surface))
            Now.Polyline(array, 1, 3).SetWidth(3f).Draw();

        using (_drawList.Begin(Surface))
            Now.Polyline(list, 1, 3).SetWidth(3f).Draw();

        Assert.AreEqual(12, _drawList.mesh.vertexCount);
        CollectionAssert.AreEqual(ReadPositions(expected), ReadPositions(_drawList));
    }

    [Test]
    public void PolylineGradientRunsFromFirstToLastPoint()
    {
        var points = new[]
        {
            new Vector2(20f, 100f),
            new Vector2(80f, 40f),
            new Vector2(140f, 100f),
            new Vector2(200f, 40f)
        };

        using (_drawList.Begin(Surface))
        {
            Now.Polyline(points)
                .SetWidth(4f)
                .SetGradient(Color.red, Color.blue)
                .Draw();
        }

        var colors = ReadColors(_drawList);
        var positions = ReadPositions(_drawList);
        Assert.AreEqual(points.Length * 4, colors.Count, "A butt-capped solid polyline emits one four-vertex ring per point.");

        for (int i = 0; i < 4; ++i)
        {
            AssertRgb(Color.red, colors[i], "first ring");
            AssertRgb(Color.blue, colors[colors.Count - 4 + i], "last ring");
        }

        // Interior rings interpolate by distance: the middle point sits halfway.
        var middle = colors[2 * 4 + 1];
        Assert.That(middle.x, Is.EqualTo(1f / 3f).Within(0.01f));
        Assert.That(middle.z, Is.EqualTo(2f / 3f).Within(0.01f));
        Assert.That(Vector2.Distance(points[0], positions[1]), Is.LessThan(3f));
    }

    [Test]
    public void DashedPolylineEmitsOnePiecePerDash()
    {
        var straight = new[] { new Vector2(10f, 20f), new Vector2(110f, 20f) };

        using (_drawList.Begin(Surface))
            Now.Polyline(straight).SetWidth(3f).SetDash(10f, 10f).Draw();

        Assert.AreEqual(5, CountPieces(_drawList), "[0,10] [20,30] [40,50] [60,70] [80,90]");

        // Offset 15 starts in a gap: dashes at [5,15] [25,35] [45,55] [65,75]
        // [85,95]. The third crosses the corner and must stay one mitered piece.
        var bent = new[] { new Vector2(20f, 20f), new Vector2(70f, 20f), new Vector2(70f, 70f) };

        using (_drawList.Begin(Surface))
            Now.Polyline(bent).SetWidth(3f).SetDash(10f, 10f, 15f).Draw();

        Assert.AreEqual(5, CountPieces(_drawList));

        using (_drawList.Begin(Surface))
            Now.Polyline(bent).SetWidth(3f).SetDash(10f, 10f, 15f).SetSolid().Draw();

        Assert.AreEqual(1, CountPieces(_drawList), "SetSolid clears the dash pattern.");
    }

    [Test]
    public void ClosedPolylineJoinsItsSeam()
    {
        var square = new[]
        {
            new Vector2(40f, 40f),
            new Vector2(140f, 40f),
            new Vector2(140f, 140f),
            new Vector2(40f, 140f)
        };

        using (_drawList.Begin(Surface))
            Now.Polyline(square).SetWidth(4f).Draw();

        Assert.AreEqual(16, _drawList.mesh.vertexCount);
        Assert.AreEqual(3 * 18, _drawList.mesh.GetTriangles(0).Length, "Open: three connected segments.");

        using (_drawList.Begin(Surface))
            Now.Polyline(square).SetWidth(4f).SetCap(NowLineCap.Round).SetArrow(NowLineArrow.Both).SetClosed(true).Draw();

        Assert.AreEqual(16, _drawList.mesh.vertexCount, "Closed paths ignore caps and arrows.");
        Assert.AreEqual(4 * 18, _drawList.mesh.GetTriangles(0).Length, "Closed: the fourth segment joins last to first.");
        Assert.AreEqual(1, CountPieces(_drawList));

        // The seam is a mitered join like any other corner: the first ring's
        // outer vertex sits on the diagonal outside the corner.
        var positions = ReadPositions(_drawList);
        var outer = positions[0] - square[0];
        Assert.That(Mathf.Abs(outer.x), Is.EqualTo(Mathf.Abs(outer.y)).Within(0.001f));
        Assert.Less(outer.x, 0f);
        Assert.Less(outer.y, 0f);

        // An explicit closing point is equivalent.
        var explicitClose = new[] { square[0], square[1], square[2], square[3], square[0] };
        var expected = ReadPositions(_drawList);

        using (_drawList.Begin(Surface))
            Now.Polyline(explicitClose).SetWidth(4f).SetClosed().Draw();

        CollectionAssert.AreEqual(expected, ReadPositions(_drawList));
    }

    [Test]
    public void ClosedDashedPolylineContinuesThePatternAcrossTheSeam()
    {
        var square = new[]
        {
            new Vector2(40f, 40f),
            new Vector2(140f, 40f),
            new Vector2(140f, 140f),
            new Vector2(40f, 140f)
        };

        // Perimeter 400, pattern 50: eight dashes. Offset 15 splits one dash
        // over the seam ([385,400] + [0,15]), which must still be one piece.
        using (_drawList.Begin(Surface))
            Now.Polyline(square).SetWidth(3f).SetDash(30f, 20f, 15f).SetClosed().Draw();

        Assert.AreEqual(8, CountPieces(_drawList));

        using (_drawList.Begin(Surface))
            Now.Polyline(square).SetWidth(3f).SetDash(30f, 20f, 15f).Draw();

        Assert.AreEqual(7, CountPieces(_drawList), "Open: the closing edge is not walked, so [0,15] ... [285,300].");
    }

    [Test]
    public void ArcGeometryStaysWithinHalfWidthOfTheRadius()
    {
        var center = new Vector2(120f, 110f);
        const float radius = 60f;
        const float width = 8f;

        using (_drawList.Begin(Surface))
        {
            Now.Arc(center, radius, 0.3f, 2.2f)
                .SetWidth(width)
                .SetCap(NowLineCap.Round)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        var positions = ReadPositions(_drawList);
        var colors = ReadColors(_drawList);
        float slack = 0.05f;

        for (int i = 0; i < positions.Count; ++i)
        {
            float distance = Vector2.Distance(positions[i], center);
            // Opaque vertices are the stroke core; transparent ones the fringe.
            float reach = colors[i].w > 0.5f ? width * 0.5f : width * 0.5f + AaWidth;
            Assert.That(distance, Is.InRange(radius - reach - slack, radius + reach + slack), "vertex " + i);
        }
    }

    [Test]
    public void PositiveSweepTurnsClockwiseOnScreen()
    {
        var center = new Vector2(150f, 120f);
        const float radius = 50f;

        using (_drawList.Begin(Surface))
        {
            Now.Arc(center, radius, 0f, Mathf.PI * 0.5f)
                .SetWidth(4f)
                .SetGradient(Color.red, Color.blue)
                .Draw();
        }

        var positions = ReadPositions(_drawList);
        var colors = ReadColors(_drawList);
        Vector2 start = Vector2.zero;
        Vector2 end = Vector2.zero;
        int startCount = 0;
        int endCount = 0;

        for (int i = 0; i < positions.Count; ++i)
        {
            // y-down UI space: a clockwise quarter from 0 stays below the center.
            Assert.GreaterOrEqual(positions[i].y, center.y - 3f);
            Assert.GreaterOrEqual(positions[i].x, center.x - 3f);

            if (colors[i].x > 0.999f)
            {
                start += positions[i];
                ++startCount;
            }
            else if (colors[i].z > 0.999f)
            {
                end += positions[i];
                ++endCount;
            }
        }

        Assert.AreEqual(4, startCount);
        Assert.AreEqual(4, endCount);
        Assert.That(Vector2.Distance(start / startCount, center + new Vector2(radius, 0f)), Is.LessThan(0.01f));
        Assert.That(Vector2.Distance(end / endCount, center + new Vector2(0f, radius)), Is.LessThan(0.01f));
    }

    [TestCase(Mathf.PI * 2f)]
    [TestCase(-Mathf.PI * 2f)]
    [TestCase(Mathf.PI * 3f)]
    public void FullSweepDrawsOneClosedRing(float sweep)
    {
        using (_drawList.Begin(Surface))
        {
            Now.Arc(new Vector2(160f, 120f), 70f, 1f, sweep)
                .SetWidth(6f)
                .SetCap(NowLineCap.Round)
                .SetArrow(NowLineArrow.Both)
                .Draw();
        }

        int vertices = _drawList.mesh.vertexCount;
        Assert.Greater(vertices, 0);
        Assert.AreEqual(0, vertices % 4, "A ring has only stroke rings: no caps and no arrows.");
        Assert.AreEqual(vertices / 4 * 18, _drawList.mesh.GetTriangles(0).Length,
            "Every ring connects to the next, including last to first.");
        Assert.AreEqual(1, CountPieces(_drawList));

        using (_drawList.Begin(Surface))
            Now.Arc(new Vector2(160f, 120f), 70f, 1f, sweep).SetWidth(6f).SetSegments(12).Draw();

        Assert.AreEqual(12 * 4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void DashedRingSpacesDashesEvenlyAcrossTheSeam()
    {
        var center = new Vector2(160f, 120f);
        const float radius = 60f;
        const int dashCount = 12;
        float pattern = Mathf.PI * 2f * radius / dashCount;
        float dash = pattern * 0.6f;

        // The half-dash offset puts one dash across the seam at angle 0.
        using (_drawList.Begin(Surface))
        {
            Now.Arc(center, radius, 0f, Mathf.PI * 2f)
                .SetWidth(4f)
                .SetDash(dash, pattern - dash, dash * 0.5f)
                .Draw();
        }

        var pieces = CollectPieces(_drawList);
        Assert.AreEqual(dashCount, pieces.Count, "The seam dash must be one piece, not two halves.");

        // Measure each dash by the angular extent of its vertices: its middle
        // and its span, both independent of where the circle samples fall.
        var positions = ReadPositions(_drawList);
        var middles = new List<float>();
        float expectedSpan = dash / radius;

        foreach (var piece in pieces)
        {
            Vector2 sum = Vector2.zero;

            foreach (int vertex in piece)
                sum += positions[vertex] - center;

            float reference = Mathf.Atan2(sum.y, sum.x);
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (int vertex in piece)
            {
                Vector2 offset = positions[vertex] - center;
                float delta = Mathf.DeltaAngle(reference * Mathf.Rad2Deg, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                min = Mathf.Min(min, delta);
                max = Mathf.Max(max, delta);
            }

            Assert.That(max - min, Is.EqualTo(expectedSpan).Within(0.01f), "dash span");
            middles.Add(Mathf.Repeat(reference + (min + max) * 0.5f + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
        }

        middles.Sort();
        float step = Mathf.PI * 2f / dashCount;
        bool seamDash = false;

        for (int i = 0; i < middles.Count; ++i)
        {
            float next = i + 1 < middles.Count ? middles[i + 1] : middles[0] + Mathf.PI * 2f;
            Assert.That(next - middles[i], Is.EqualTo(step).Within(0.01f), "dash " + i);
            seamDash |= Mathf.Abs(middles[i]) < 0.01f;
        }

        Assert.IsTrue(seamDash, "One dash is centered on the seam at angle 0.");
    }

    [Test]
    public void TransformScalesArcsIntoEllipsesAndPolylinesPointwise()
    {
        using (_drawList.Begin(Surface))
        using (Now.Transform(new Vector2(2f, 1f), new Vector2(10f, 20f)))
        {
            Now.Arc(new Vector2(50f, 50f), 20f, 0f, Mathf.PI * 2f)
                .SetWidth(1f)
                .Draw();
        }

        GetBounds(ReadPositions(_drawList), out var min, out var max);
        // Width scales by the larger axis (2), so the ring is 2 units thick.
        float reach = 1f + AaWidth + 0.05f;
        Assert.That(min.x, Is.InRange(70f - reach, 70f));
        Assert.That(max.x, Is.InRange(150f, 150f + reach));
        Assert.That(min.y, Is.InRange(50f - reach, 50f));
        Assert.That(max.y, Is.InRange(90f, 90f + reach));

        var points = new[] { new Vector2(0f, 0f), new Vector2(40f, 0f) };

        using (_drawList.Begin(Surface))
        using (Now.Transform(new Vector2(2f, 3f), new Vector2(10f, 20f)))
            Now.Polyline(points).SetWidth(2f).Draw();

        var positions = ReadPositions(_drawList);
        Assert.AreEqual(8, positions.Count);
        // Ring 0 is at (10, 20) and ring 1 at (90, 20). Width scales by the
        // larger axis (3) to 6, so the opaque core reaches 3 - AA either side.
        float core = 3f - AaWidth;
        Assert.That(Vector2.Distance(positions[1], new Vector2(10f, 20f - core)), Is.LessThan(0.001f));
        Assert.That(Vector2.Distance(positions[6], new Vector2(90f, 20f + core)), Is.LessThan(0.001f));
    }

    [Test]
    public void MaskedStrokesAreCulledBeforeTessellation()
    {
        var points = new[] { new Vector2(200f, 150f), new Vector2(260f, 200f), new Vector2(300f, 160f) };
        var farAway = new NowRect(0f, 0f, 30f, 30f);

        using (_drawList.Begin(Surface))
        {
            using (Now.Mask(farAway))
            {
                Now.Polyline(points).SetWidth(4f).SetArrow(NowLineArrow.Both).Draw();
                Now.Arc(new Vector2(200f, 150f), 40f, 0f, Mathf.PI).SetWidth(4f).Draw();
            }

            Now.Polyline(points).SetWidth(4f).SetMask(farAway).Draw();
            Now.Arc(new Vector2(200f, 150f), 40f, 0f, Mathf.PI * 2f).SetWidth(4f).SetMask(farAway).Draw();
        }

        Assert.IsFalse(_drawList.hasGeometry);
        Assert.AreEqual(0, _drawList.mesh.vertexCount);

        // A ring whose stroke crosses the mask survives the coarse cull.
        using (_drawList.Begin(Surface))
        using (Now.Mask(new NowRect(0f, 0f, 30f, 30f)))
            Now.Arc(new Vector2(60f, 60f), 40f, 0f, Mathf.PI * 2f).SetWidth(4f).Draw();

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void DegenerateArcsAndPolylinesDrawNothing()
    {
        var single = new[] { new Vector2(10f, 10f) };
        var repeated = new[] { new Vector2(10f, 10f), new Vector2(10f, 10f) };

        using (_drawList.Begin(Surface))
        {
            Now.Arc(new Vector2(100f, 100f), 40f, 1f, 0f).SetWidth(4f).Draw();
            Now.Arc(new Vector2(100f, 100f), 0f, 0f, Mathf.PI).SetWidth(4f).Draw();
            Now.Arc(new Vector2(100f, 100f), -5f, 0f, Mathf.PI).SetWidth(4f).Draw();
            Now.Arc(new Vector2(100f, 100f), 40f, 0f, Mathf.PI).SetWidth(0f).Draw();
            Now.Arc(new Vector2(100f, 100f), 40f, 0f, float.NaN).SetWidth(4f).Draw();
            Now.Polyline(single).SetWidth(4f).Draw();
            Now.Polyline(repeated).SetWidth(4f).Draw();
            Now.Polyline((Vector2[])null).SetWidth(4f).Draw();
            Now.Polyline(new[] { new Vector2(0f, 0f), new Vector2(40f, 0f) }, 5, 2).SetWidth(4f).Draw();
        }

        Assert.IsFalse(_drawList.hasGeometry);
    }

    [Test]
    public void SteadyStateStrokeBuildersDoNotAllocate()
    {
        var star = new Vector2[10];
        var samples = new List<Vector2>(32);

        for (int i = 0; i < star.Length; ++i)
        {
            float angle = Mathf.PI * 2f * i / star.Length - Mathf.PI * 0.5f;
            float radius = i % 2 == 0 ? 50f : 22f;
            star[i] = new Vector2(80f + Mathf.Cos(angle) * radius, 80f + Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < 32; ++i)
            samples.Add(new Vector2(10f + i * 9f, 200f - Mathf.Sin(i * 0.4f) * 20f));

        void DrawFrame()
        {
            using (_drawList.Begin(Surface))
            {
                Now.Polyline(star).SetWidth(3f).SetDash(8f, 5f, 3f).SetClosed().SetGradient(Color.red, Color.blue).Draw();
                Now.Polyline(samples).SetWidth(2f).SetCap(NowLineCap.Round).SetArrow(NowLineArrow.Both).Draw();
                Now.Arc(new Vector2(220f, 80f), 50f, 0.4f, Mathf.PI * 2f).SetWidth(4f).SetDash(10f, 6f, 2f).Draw();
                Now.Arc(new Vector2(220f, 80f), 36f, -Mathf.PI * 0.5f, 4f)
                    .SetWidth(6f)
                    .SetCap(NowLineCap.Round)
                    .SetGradient(Color.green, Color.yellow)
                    .SetArrow(NowLineArrow.End)
                    .Draw();
            }
        }

        DrawFrame();
        DrawFrame();

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        DrawFrame();
        long allocated = allocations.End();
        allocations.AssertZero(allocated, "steady-state polyline and arc strokes must not allocate");
    }

    static List<Vector2> ReadPositions(NowDrawList drawList)
    {
        var vertices = new List<Vector3>();
        drawList.mesh.GetVertices(vertices);
        var positions = new List<Vector2>(vertices.Count);

        foreach (var vertex in vertices)
            positions.Add(new Vector2(vertex.x, -vertex.y));

        return positions;
    }

    static List<Vector4> ReadColors(NowDrawList drawList)
    {
        var colors = new List<Vector4>();
        drawList.mesh.GetUVs(3, colors);
        return colors;
    }

    static void AssertRgb(Color expected, Vector4 actual, string message)
    {
        Assert.That(actual.x, Is.EqualTo(expected.r).Within(0.001f), message);
        Assert.That(actual.y, Is.EqualTo(expected.g).Within(0.001f), message);
        Assert.That(actual.z, Is.EqualTo(expected.b).Within(0.001f), message);
    }

    static void GetBounds(List<Vector2> positions, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        foreach (var position in positions)
        {
            min = Vector2.Min(min, position);
            max = Vector2.Max(max, position);
        }
    }

    static int CountPieces(NowDrawList drawList)
    {
        return CollectPieces(drawList).Count;
    }

    /// <summary>
    /// Groups vertices into triangle-connected pieces. Each dash, and each arrow
    /// head stroke, is tessellated as its own piece.
    /// </summary>
    static List<List<int>> CollectPieces(NowDrawList drawList)
    {
        int vertexCount = drawList.mesh.vertexCount;
        var parent = new int[vertexCount];

        for (int i = 0; i < vertexCount; ++i)
            parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
                x = parent[x] = parent[parent[x]];

            return x;
        }

        var used = new bool[vertexCount];

        for (int subMesh = 0; subMesh < drawList.mesh.subMeshCount; ++subMesh)
        {
            int[] triangles = drawList.mesh.GetTriangles(subMesh);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                used[a] = used[triangles[i + 1]] = used[triangles[i + 2]] = true;
                parent[Find(triangles[i + 1])] = Find(a);
                parent[Find(triangles[i + 2])] = Find(a);
            }
        }

        var byRoot = new Dictionary<int, List<int>>();

        for (int i = 0; i < vertexCount; ++i)
        {
            if (!used[i])
                continue;

            int root = Find(i);

            if (!byRoot.TryGetValue(root, out var piece))
                byRoot[root] = piece = new List<int>();

            piece.Add(i);
        }

        return new List<List<int>>(byRoot.Values);
    }
}
