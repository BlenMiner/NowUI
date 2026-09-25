using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using UnityEngine;

public class NowRotateTests
{
    static readonly Vector2 Surface = new Vector2(320f, 240f);

    NowDrawList _drawList;
    NowDrawList _reference;

    [SetUp]
    public void SetUp()
    {
        _drawList = new NowDrawList();
        _reference = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        _reference.Dispose();
    }

    // Mesh positions are stored with y negated; tests reason in UI space.
    static List<Vector2> UiVertices(NowDrawList drawList)
    {
        var positions = new List<Vector3>();
        drawList.mesh.GetVertices(positions);
        var result = new List<Vector2>();
        foreach (var vertex in positions)
            result.Add(new Vector2(vertex.x, -vertex.y));
        return result;
    }

    static Vector2 RotateUi(Vector2 point, Vector2 pivot, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        Vector2 d = point - pivot;
        return pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
    }

    static void AssertRotated(List<Vector2> reference, List<Vector2> actual, Vector2 pivot, float degrees, int start = 0)
    {
        Assert.AreEqual(reference.Count, actual.Count);

        for (int i = 0; i < reference.Count; i++)
        {
            Vector2 expected = i < start ? reference[i] : RotateUi(reference[i], pivot, degrees);
            Assert.AreEqual(expected.x, actual[i].x, 0.01f, $"vertex {i} x");
            Assert.AreEqual(expected.y, actual[i].y, 0.01f, $"vertex {i} y");
        }
    }

    static void DrawScene()
    {
        Now.Rectangle(new NowRect(40f, 30f, 80f, 40f)).SetColor(Color.white).SetRadius(6f).Draw();
        Now.Text(new NowRect(40f, 90f, 200f, 40f)).SetFontSize(20f).SetColor(Color.white).Draw("Spin");
        Now.Line(new Vector2(40f, 160f), new Vector2(200f, 180f)).SetWidth(3f).SetColor(Color.white).Draw();
    }

    [Test]
    public void RotateTurnsEveryVertexAroundThePivotClockwise()
    {
        var pivot = new Vector2(100f, 80f);

        using (_reference.Begin(Surface))
            DrawScene();

        using (_drawList.Begin(Surface))
        using (Now.Rotate(30f, pivot))
            DrawScene();

        AssertRotated(UiVertices(_reference), UiVertices(_drawList), pivot, 30f);
    }

    [Test]
    public void PositiveAngleMovesAPointRightOfThePivotDownward()
    {
        using (_drawList.Begin(Surface))
        using (Now.Rotate(90f, new Vector2(50f, 50f)))
            Now.Rectangle(new NowRect(60f, 49f, 2f, 2f)).SetColor(Color.white).Draw();

        Vector2 centroid = Vector2.zero;
        var vertices = UiVertices(_drawList);
        foreach (var vertex in vertices)
            centroid += vertex;
        centroid /= vertices.Count;

        // (61, 50) is right of the pivot; a clockwise quarter turn on screen puts it below.
        Assert.AreEqual(50f, centroid.x, 0.01f);
        Assert.AreEqual(61f, centroid.y, 0.01f);
    }

    [Test]
    public void OnlyVerticesEmittedInsideTheScopeRotate()
    {
        var pivot = new Vector2(160f, 120f);

        using (_reference.Begin(Surface))
        {
            Now.Rectangle(new NowRect(0f, 0f, 20f, 20f)).SetColor(Color.white).Draw();
            Now.Rectangle(new NowRect(40f, 30f, 80f, 40f)).SetColor(Color.white).Draw();
            Now.Rectangle(new NowRect(200f, 200f, 20f, 20f)).SetColor(Color.white).Draw();
        }

        using (_drawList.Begin(Surface))
        {
            Now.Rectangle(new NowRect(0f, 0f, 20f, 20f)).SetColor(Color.white).Draw();
            using (Now.Rotate(45f, pivot))
                Now.Rectangle(new NowRect(40f, 30f, 80f, 40f)).SetColor(Color.white).Draw();
            Now.Rectangle(new NowRect(200f, 200f, 20f, 20f)).SetColor(Color.white).Draw();
        }

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);
        Assert.AreEqual(12, actual.Count);

        for (int i = 0; i < 12; i++)
        {
            Vector2 expected = i >= 4 && i < 8 ? RotateUi(reference[i], pivot, 45f) : reference[i];
            Assert.AreEqual(expected.x, actual[i].x, 0.01f, $"vertex {i}");
            Assert.AreEqual(expected.y, actual[i].y, 0.01f, $"vertex {i}");
        }
    }

    [Test]
    public void NestedRotationsCompose()
    {
        var pivot = new Vector2(100f, 80f);

        using (_reference.Begin(Surface))
        using (Now.Rotate(90f, pivot))
            DrawScene();

        using (_drawList.Begin(Surface))
        using (Now.Rotate(30f, pivot))
        using (Now.Rotate(60f, pivot))
            DrawScene();

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);

        for (int i = 0; i < reference.Count; i++)
            Assert.AreEqual(0f, (reference[i] - actual[i]).magnitude, 0.01f, $"vertex {i}");
    }

    [Test]
    public void PivotIsInTheActiveTransformSpace()
    {
        var pivot = new Vector2(50f, 40f);

        using (_reference.Begin(Surface))
        using (Now.Transform(2f, new Vector2(10f, 20f)))
            DrawScene();

        using (_drawList.Begin(Surface))
        using (Now.Transform(2f, new Vector2(10f, 20f)))
        using (Now.Rotate(-40f, pivot))
            DrawScene();

        AssertRotated(UiVertices(_reference), UiVertices(_drawList), pivot * 2f + new Vector2(10f, 20f), -40f);
    }

    [Test]
    public void BatchBoundsCoverRotatedGeometry()
    {
        using (_drawList.Begin(Surface))
        using (Now.Rotate(45f, new Vector2(160f, 120f)))
            Now.Rectangle(new NowRect(110f, 115f, 100f, 10f)).SetColor(Color.white).Draw();

        var bounds = _drawList.batches[0].bounds;
        Assert.Greater(bounds.height, 70f, "A 45 degree bar is taller than its unrotated height.");

        foreach (var vertex in UiVertices(_drawList))
        {
            Assert.GreaterOrEqual(vertex.x, bounds.x - 0.01f);
            Assert.LessOrEqual(vertex.x, bounds.xMax + 0.01f);
            Assert.GreaterOrEqual(vertex.y, bounds.y - 0.01f);
            Assert.LessOrEqual(vertex.y, bounds.yMax + 0.01f);
        }
    }

    [Test]
    public void IndependentCaptureInsideTheScopeIsNotRotated()
    {
        using (_reference.Begin(Surface))
            DrawScene();

        using (_drawList.Begin(Surface))
        using (Now.Rotate(30f, new Vector2(100f, 80f)))
        {
            var inner = new NowDrawList();

            try
            {
                using (inner.Begin(Surface))
                    DrawScene();

                var reference = UiVertices(_reference);
                var actual = UiVertices(inner);

                for (int i = 0; i < reference.Count; i++)
                    Assert.AreEqual(0f, (reference[i] - actual[i]).magnitude, 0.001f);
            }
            finally
            {
                inner.Dispose();
            }
        }
    }

    [Test]
    public void ZeroRotationLeavesGeometryUnchanged()
    {
        using (_reference.Begin(Surface))
            DrawScene();

        using (_drawList.Begin(Surface))
        using (Now.Rotate(0f, new Vector2(12f, 34f)))
            DrawScene();

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);

        for (int i = 0; i < reference.Count; i++)
            Assert.AreEqual(reference[i], actual[i]);
    }

    [Test]
    public void NonFiniteArgumentsThrowAndMisorderedDisposeThrows()
    {
        using (_drawList.Begin(Surface))
        {
            Assert.Throws<ArgumentException>(() => Now.Rotate(float.NaN, Vector2.zero));
            Assert.Throws<ArgumentException>(() => Now.Rotate(10f, new Vector2(float.PositiveInfinity, 0f)));

            var outer = Now.Rotate(10f, Vector2.zero);
            var inner = Now.Rotate(10f, Vector2.zero);
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            inner.Dispose();
            outer.Dispose();
            outer.Dispose();
            default(NowRotationScope).Dispose();
        }
    }

    [Test]
    public void RotationScopesAreAllocationFreeAfterWarmup()
    {
        void Frame()
        {
            using (_drawList.Begin(Surface))
            using (Now.Rotate(25f, new Vector2(100f, 80f)))
            using (Now.Rotate(-10f, new Vector2(60f, 60f)))
                Now.Rectangle(new NowRect(40f, 30f, 80f, 40f)).SetColor(Color.white).Draw();
        }

        Frame();
        Frame();
        Frame();

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        Frame();
        long allocated = allocations.End();
        allocations.AssertZero(allocated, "rotation scopes must not allocate");
    }
}
