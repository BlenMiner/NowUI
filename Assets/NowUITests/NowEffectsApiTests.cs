using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using NowUI.Sdf;
using UnityEngine;

public class NowEffectsApiTests
{
    static readonly Vector2 Surface = new Vector2(320f, 240f);

    readonly struct CollapseToSourceCenter : INowVertexDeformer
    {
        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            return context.sourceRect.center;
        }
    }

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
        NowSdf.Reset();
    }

    static List<Vector2> UiVertices(NowDrawList drawList)
    {
        var positions = new List<Vector3>();
        drawList.mesh.GetVertices(positions);
        var result = new List<Vector2>();
        foreach (var vertex in positions)
            result.Add(new Vector2(vertex.x, -vertex.y));
        return result;
    }

    static readonly NowRect Card = new NowRect(100f, 60f, 120f, 80f);

    static void DrawCard()
    {
        Now.Rectangle(Card).SetColor(Color.white).Draw();
    }

    static float VerticalSpan(List<Vector2> vertices, bool rightSide)
    {
        float centerX = 0f;
        foreach (var vertex in vertices)
            centerX += vertex.x;
        centerX /= vertices.Count;

        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (var vertex in vertices)
        {
            if (vertex.x > centerX != rightSide)
                continue;

            min = Mathf.Min(min, vertex.y);
            max = Mathf.Max(max, vertex.y);
        }

        return max - min;
    }

    static float HorizontalSpan(List<Vector2> vertices, bool bottomSide)
    {
        float centerY = 0f;
        foreach (var vertex in vertices)
            centerY += vertex.y;
        centerY /= vertices.Count;

        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (var vertex in vertices)
        {
            if (vertex.y > centerY != bottomSide)
                continue;

            min = Mathf.Min(min, vertex.x);
            max = Mathf.Max(max, vertex.x);
        }

        return max - min;
    }

    [Test]
    public void PerspectiveWithoutTurnLeavesGeometryInPlace()
    {
        using (_reference.Begin(Surface))
            DrawCard();

        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Perspective(0f, 0f)).Begin())
            DrawCard();

        var reference = UiVertices(_reference);
        var actual = UiVertices(_drawList);
        Assert.AreEqual(reference.Count, actual.Count);

        for (int i = 0; i < reference.Count; i++)
            Assert.AreEqual(0f, (reference[i] - actual[i]).magnitude, 0.001f);
    }

    [Test]
    public void PositiveYawMovesTheRightEdgeAway()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Perspective(40f, 0f)).Begin())
            DrawCard();

        var vertices = UiVertices(_drawList);
        Assert.Less(VerticalSpan(vertices, rightSide: true), VerticalSpan(vertices, rightSide: false));
    }

    [Test]
    public void PositivePitchMovesTheTopEdgeAway()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Perspective(0f, 40f)).Begin())
            DrawCard();

        var vertices = UiVertices(_drawList);
        Assert.Less(HorizontalSpan(vertices, bottomSide: false), HorizontalSpan(vertices, bottomSide: true));
    }

    [Test]
    public void ExplicitSourceRectFollowsTheActiveTransform()
    {
        var source = new NowRect(0f, 0f, 40f, 20f);

        using (_drawList.Begin(Surface))
        using (Now.Transform(2f, new Vector2(10f, 5f)))
        using (NowEffects.Modifier(new CollapseToSourceCenter()).SetSourceRect(source).Begin())
            Now.Rectangle(new NowRect(5f, 5f, 10f, 10f)).SetColor(Color.white).Draw();

        // The authored center (20, 10) maps to (10 + 20 * 2, 5 + 10 * 2).
        foreach (var vertex in UiVertices(_drawList))
            Assert.AreEqual(0f, (vertex - new Vector2(50f, 25f)).magnitude, 0.001f);
    }

    [Test]
    public void SubdividedModifierSplitsSdfScenes()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Perspective(30f, 0f)).SetSubdivision(4).Begin())
        {
            NowSdf.Scene(new NowRect(40f, 40f, 120f, 120f), "effects-api-sdf")
                .SetColor(Color.white)
                .Circle(new Vector2(60f, 60f), 40f)
                .Draw();
        }

        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(NowMeshKind.Sdf, _drawList.batches[0].kind);
        Assert.AreEqual(25, _drawList.mesh.vertexCount, "A 4 x 4 grid keeps perspective from folding the field.");
    }
}
