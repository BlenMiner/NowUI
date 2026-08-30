using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using NowUI.Sdf;

public class NowSdfTests
{
    const string DynamicFontAssetPath =
        "Assets/NowUI/Assets/Fonts/NotoSans/NotoSans-Regular.ttf.asset";
    const string RawDynamicFontPath =
        "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

    NowDrawList _drawList;
    float _previousUiScale;
    bool _previousForceManagedCompiler;
    bool _previousForceNativeCompiler;

    [SetUp]
    public void SetUp()
    {
        _previousUiScale = Now.uiScale;
        _previousForceManagedCompiler = NowFontCompiler.forceManagedCompiler;
        _previousForceNativeCompiler = NowFontCompiler.forceNativeCompiler;
        Now.SetUIScale(1f);
        NowSdf.Reset();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowSdf.Reset();
        Now.SetUIScale(_previousUiScale);
        NowFontCompiler.forceManagedCompiler = _previousForceManagedCompiler;
        NowFontCompiler.forceNativeCompiler = _previousForceNativeCompiler;
    }

    [Test]
    public void SdfSceneEmitsSingleMaterialQuad()
    {
        Assert.NotNull(Resources.Load<Material>("NowUI/SdfMaterial"));

        using (_drawList.Begin(new Vector2(160, 100)))
        {
            NowSdf.Scene(new NowRect(0, 0, 160, 100))
                .SetColor(Color.red)
                .Circle(new Vector2(48, 50), 34)
                .SetColor(Color.cyan)
                .SmoothUnion(12)
                .RoundedBox(new NowRect(44, 22, 92, 56), 14)
                .Subtract()
                .Circle(new Vector2(88, 50), 16)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(NowMeshKind.Sdf, _drawList.batches[0].kind);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void SdfRadialPrimitivesPackAnglesAndConservativeBounds()
    {
        const float invSqrtTwo = 0.70710678f;
        var scene = NowSdf.Scene("sdf-radial-packing")
            .Arc(new Vector2(20f, 24f), 10f, 3f, 0f, Mathf.PI * 0.5f)
            .Pie(new Vector2(50f, 18f), 8f, 0f, -Mathf.PI * 0.5f);

        Assert.AreEqual(new Vector2(58f, 37f), scene.Measure(),
            "Arc bounds must include its half-width; Pie bounds include its radius.");

        using (_drawList.Begin(new Vector2(64f, 48f)))
            scene.Draw(new NowRect(0f, 0f, 64f, 48f));

        var material = _drawList.batches[0].material;
        var data0 = material.GetVectorArray("_SdfData0");
        var data1 = material.GetVectorArray("_SdfData1");
        var data2 = material.GetVectorArray("_SdfData2");

        Assert.AreEqual((float)NowSdfShapeType.Arc, data0[0].x);
        Assert.AreEqual((float)NowSdfShapeType.Pie, data0[1].x);
        Assert.AreEqual(new Vector4(20f, 24f, 10f, 3f), data1[0]);
        Assert.AreEqual(new Vector4(50f, 18f, 8f, 0f), data1[1]);

        Assert.AreEqual(invSqrtTwo, data2[0].x, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[0].y, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[0].z, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[0].w, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[1].x, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[1].y, 0.00001f);
        Assert.AreEqual(-invSqrtTwo, data2[1].z, 0.00001f);
        Assert.AreEqual(invSqrtTwo, data2[1].w, 0.00001f);
    }

    [Test]
    public void SdfRadialFullTurnsClampAndZeroSweepConsumesOperation()
    {
        var graph = NowSdf.Graph()
            .Circle(new Vector2(8f, 8f), 4f)
            .Subtract()
            .Arc(new Vector2(20f, 20f), 12f, 3f, 0f, 0f)
            .Circle(new Vector2(16f, 8f), 4f)
            .Pie(new Vector2(24f, 24f), 10f, 1f, Mathf.PI * 4f)
            .Arc(new Vector2(48f, 24f), 9f, 2f, -1f, -Mathf.PI * 4f);

        Assert.AreEqual(4, graph.nodes.Count, "A zero sweep must not consume a shape slot.");
        Assert.AreEqual(NowSdfOperation.Union, graph.nodes[1].operation,
            "A skipped radial primitive must not leak its pending operation.");
        Assert.AreEqual(new Vector4(0f, -1f, 0f, 0f), graph.nodes[2].data2,
            "A positive over-turn must pack the explicit full-turn sentinel.");
        Assert.AreEqual(new Vector4(0f, -1f, 0f, 0f), graph.nodes[3].data2,
            "A negative over-turn must pack the same full-turn sentinel.");
    }

    [Test]
    public void SdfRadialPrimitivesRejectNonFiniteArgumentsWithoutMutation()
    {
        var graph = NowSdf.Graph().Circle(new Vector2(8f, 8f), 4f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Arc(Vector2.zero, 12f, 3f, float.PositiveInfinity, Mathf.PI));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Pie(new Vector2(float.NaN, 0f), 12f, 0f, Mathf.PI));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Arc(Vector2.zero, float.MaxValue, float.MaxValue, 0f, Mathf.PI));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Pie(Vector2.zero, float.MaxValue, 0f, Mathf.PI));

        Assert.AreEqual(1, graph.nodes.Count, "Invalid radial inputs mutated the graph.");
    }

    [Test]
    public void SdfPlanarPrimitivesPackDataAndConservativeBounds()
    {
        var scene = NowSdf.Scene("sdf-planar-packing")
            .ChamferedBox(new NowRect(4f, 6f, 20f, 14f), 3f)
            .Triangle(
                new Vector2(30f, 4f),
                new Vector2(54f, 10f),
                new Vector2(38f, 34f))
            .Line(new Vector2(8f, 44f), new Vector2(42f, 56f), 10f);

        Assert.AreEqual(new Vector2(54f, 61f), scene.Measure(),
            "Triangle bounds must include every vertex and Line bounds must include half its width.");

        using (_drawList.Begin(new Vector2(64f, 64f)))
            scene.Draw(new NowRect(0f, 0f, 64f, 64f));

        var material = _drawList.batches[0].material;
        var data0 = material.GetVectorArray("_SdfData0");
        var data1 = material.GetVectorArray("_SdfData1");
        var data2 = material.GetVectorArray("_SdfData2");

        Assert.AreEqual(8, (int)NowSdfShapeType.ChamferedBox,
            "New shape opcodes must be appended after the released Arc and Pie opcodes.");
        Assert.AreEqual(9, (int)NowSdfShapeType.Triangle,
            "New shape opcodes must remain stable for the shader ABI.");
        Assert.AreEqual((float)NowSdfShapeType.ChamferedBox, data0[0].x);
        Assert.AreEqual((float)NowSdfShapeType.Triangle, data0[1].x);
        Assert.AreEqual((float)NowSdfShapeType.Capsule, data0[2].x,
            "Line should remain a Capsule alias rather than consuming another opcode.");

        Assert.AreEqual(new Vector4(14f, 13f, 20f, 14f), data1[0]);
        Assert.AreEqual(new Vector4(3f, 0f, 0f, 0f), data2[0]);
        Assert.AreEqual(30f, data1[1].x);
        Assert.AreEqual(4f, data1[1].y);
        Assert.AreEqual(0.8f, data1[1].z, 0.0001f);
        Assert.AreEqual(0.2f, data1[1].w, 0.0001f);
        Assert.AreEqual(8f / 30f, data2[1].x, 0.0001f);
        Assert.AreEqual(1f, data2[1].y, 0.0001f);
        Assert.AreEqual(1f, data2[1].z);
        Assert.AreEqual(30f, data2[1].w);
        Assert.AreEqual(new Vector4(8f, 44f, 42f, 56f), data1[2]);
        Assert.AreEqual(new Vector4(5f, 0f, 0f, 0f), data2[2],
            "Line width is a full stroke width, while Capsule stores its radius.");
    }

    [Test]
    public void SdfPlanarPrimitiveNegativeSizesClampToZero()
    {
        var graph = NowSdf.Graph()
            .ChamferedBox(new NowRect(2f, 3f, 20f, 12f), -4f)
            .Line(new Vector2(4f, 24f), new Vector2(28f, 24f), -6f);

        Assert.AreEqual(2, graph.nodes.Count);
        Assert.AreEqual(0f, graph.nodes[0].data2.x,
            "A negative chamfer must behave like a zero-chamfer box.");
        Assert.AreEqual(0f, graph.nodes[1].data2.x,
            "A negative Line width must clamp to a zero-radius Capsule.");
    }

    [Test]
    public void SdfRotateNextCanonicalizesNodeMetadataAndResetsAfterOnePrimitive()
    {
        var rect = new NowRect(4f, 6f, 20f, 14f);
        var graph = NowSdf.Graph()
            .RotateNext(90f)
            .ChamferedBox(rect, 3f)
            .RotateNext(450f)
            .Box(rect)
            .RotateNext(-90f)
            .Ellipse(rect)
            .RotateNext(360f)
            .RoundedBox(rect, 2f);

        Assert.AreEqual(new Vector4(3f, 0f, 0f, 0f), graph.nodes[0].data2,
            "ChamferedBox no longer owns a shape-specific rotation payload.");
        Assert.AreEqual(new Vector2(0f, 1f), graph.nodes[0].rotation);
        Assert.AreEqual(graph.nodes[0].rotation, graph.nodes[1].rotation,
            "Angles must wrap before packing their rotation.");
        Assert.AreEqual(new Vector2(0f, -1f), graph.nodes[2].rotation);
        Assert.AreEqual(Vector2.zero, graph.nodes[3].rotation,
            "Full turns should use the compact identity sentinel.");
        AssertRectApproximately(
            new NowRect(7f, 3f, 14f, 20f),
            graph.nodes[0].bounds,
            0.002f,
            "A quarter turn must swap the authored half-extents around rect.center.");
        Assert.AreEqual(rect, graph.nodes[3].bounds);

        var nextOnly = NowSdf.Graph()
            .RotateNext(90f)
            .Box(rect)
            .Box(rect);
        Assert.AreEqual(new Vector2(0f, 1f), nextOnly.nodes[0].rotation);
        Assert.AreEqual(Vector2.zero, nextOnly.nodes[1].rotation,
            "RotateNext must apply to exactly one primitive.");

        var lastWins = NowSdf.Graph()
            .RotateNext(30f)
            .RotateNext(180f)
            .Box(rect);
        Assert.AreEqual(new Vector2(-1f, 0f), lastWins.nodes[0].rotation,
            "A later RotateNext call must replace the pending angle.");

        var cleared = NowSdf.Graph()
            .Circle(new Vector2(8f, 8f), 4f)
            .RotateNext(90f);
        cleared.Clear().Box(rect);
        Assert.AreEqual(1, cleared.nodes.Count);
        Assert.AreEqual(Vector2.zero, cleared.nodes[0].rotation,
            "Clear must discard pending node transforms.");
    }

    [Test]
    public void SdfRotateNextPassesThroughStyleAndOperationAndSkippedShapeConsumesIt()
    {
        var rect = new NowRect(4f, 6f, 20f, 14f);
        var graph = NowSdf.Graph()
            .Circle(new Vector2(8f, 8f), 4f)
            .RotateNext(90f)
            .SetColor(Color.red)
            .SmoothSubtract(3f)
            .Box(rect)
            .Circle(new Vector2(32f, 8f), 4f);

        Assert.AreEqual(new Vector2(0f, 1f), graph.nodes[1].rotation);
        Assert.AreEqual(NowSdfOperation.SmoothSubtract, graph.nodes[1].operation);
        Assert.AreEqual(3f, graph.nodes[1].smoothing);
        Assert.AreEqual((Vector4)Color.red, graph.nodes[1].color);
        Assert.AreEqual(Vector2.zero, graph.nodes[2].rotation);
        Assert.AreEqual(NowSdfOperation.Union, graph.nodes[2].operation);

        var skipped = NowSdf.Graph()
            .Circle(new Vector2(8f, 8f), 4f)
            .RotateNext(90f)
            .Subtract()
            .ChamferedBox(new NowRect(4f, 5f, 0f, 9f), 2f)
            .Box(rect);

        Assert.AreEqual(2, skipped.nodes.Count);
        Assert.AreEqual(Vector2.zero, skipped.nodes[1].rotation,
            "A valid-but-empty primitive must consume pending rotation.");
        Assert.AreEqual(NowSdfOperation.Union, skipped.nodes[1].operation,
            "A valid-but-empty primitive must consume its pending operation.");
    }

    [Test]
    public void SdfRotateNextRejectsNonFiniteAnglesWithoutChangingPendingState()
    {
        var graph = NowSdf.Graph().RotateNext(90f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => graph.RotateNext(float.NaN));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => graph.RotateNext(float.PositiveInfinity));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => graph.RotateNext(float.NegativeInfinity));
        Assert.AreEqual(0, graph.nodes.Count);

        graph.Box(new NowRect(4f, 6f, 20f, 14f));
        Assert.AreEqual(new Vector2(0f, 1f), graph.nodes[0].rotation,
            "A rejected angle must leave the previous pending rotation intact.");
    }

    [Test]
    public void SdfRotateNextProducesConservativeBoundsForEveryPivotKind()
    {
        var box = NowSdf.Graph()
            .RotateNext(30f)
            .Box(new NowRect(10f, 20f, 80f, 24f));
        AssertRectApproximately(
            new NowRect(9.358984f, 1.607695f, 81.282032f, 60.78461f),
            box.nodes[0].bounds,
            0.002f);

        var graph = NowSdf.Graph()
            .RotateNext(90f)
            .Ellipse(new NowRect(4f, 6f, 20f, 14f))
            .RotateNext(90f)
            .Capsule(new Vector2(10f, 20f), new Vector2(30f, 20f), 4f)
            .RotateNext(90f)
            .Triangle(
                new Vector2(10f, 10f),
                new Vector2(30f, 10f),
                new Vector2(10f, 20f));

        AssertRectApproximately(
            new NowRect(7f, 3f, 14f, 20f),
            graph.nodes[0].bounds,
            0.002f);
        AssertRectApproximately(
            new NowRect(16f, 6f, 8f, 28f),
            graph.nodes[1].bounds,
            0.002f,
            "Capsule rotation must pivot around the endpoint midpoint.");
        AssertRectApproximately(
            new NowRect(15f, 5f, 10f, 20f),
            graph.nodes[2].bounds,
            0.002f,
            "Triangle rotation must pivot around its packed vertex AABB center.");

        var degenerate = NowSdf.Graph()
            .RotateNext(45f)
            .Line(new Vector2(0f, 0f), new Vector2(20f, 0f), 0f)
            .RotateNext(45f)
            .Triangle(
                new Vector2(0f, 20f),
                new Vector2(20f, 20f),
                new Vector2(10f, 20f));

        Assert.Greater(degenerate.nodes[0].bounds.width, 0f);
        Assert.Greater(degenerate.nodes[0].bounds.height, 0f,
            "A rotated zero-width Line must acquire a two-dimensional conservative AABB.");
        Assert.Greater(degenerate.nodes[1].bounds.width, 0f);
        Assert.Greater(degenerate.nodes[1].bounds.height, 0f,
            "A rotated collinear Triangle must acquire a two-dimensional conservative AABB.");
    }

    [Test]
    public void SdfRotateNextBoundsContainAdversarialPackedGeometry()
    {
        var circle = NowSdf.Graph()
            .RotateNext(120f)
            .Circle(new Vector2(-1.25f, 0f), 1.42f)
            .nodes[0];
        double circleScale = ShaderReferenceScale(circle.rotation);
        Assert.GreaterOrEqual(
            (double)circle.bounds.xMax,
            circle.data1.x + circle.data1.z * circleScale,
            "Radial bounds must use the payload center and shader-effective rotation scale.");

        var box = NowSdf.Graph()
            .RotateNext(141f)
            .Box(new NowRect(-89.8f, 189.3f, 32.9f, 164.7f))
            .nodes[0];
        var boxPivot = new Vector2(box.data1.x, box.data1.y);
        var boxHalf = new Vector2(box.data1.z, box.data1.w) * 0.5f;
        AssertRotatedBoundsContain(box, boxPivot, boxPivot + new Vector2(-boxHalf.x, -boxHalf.y));
        AssertRotatedBoundsContain(box, boxPivot, boxPivot + new Vector2(-boxHalf.x, boxHalf.y));
        AssertRotatedBoundsContain(box, boxPivot, boxPivot + new Vector2(boxHalf.x, -boxHalf.y));
        AssertRotatedBoundsContain(box, boxPivot, boxPivot + new Vector2(boxHalf.x, boxHalf.y));

        var triangle = NowSdf.Graph()
            .RotateNext(135f)
            .Triangle(
                new Vector2(-1f, -1f),
                new Vector2(33554432f, 33554432f),
                new Vector2(-1f, 33554432f))
            .nodes[0];
        float scale = triangle.data2.w;
        var a = new Vector2(triangle.data1.x, triangle.data1.y);
        Vector2 b = a + new Vector2(triangle.data1.z, triangle.data1.w) * scale;
        Vector2 c = a + new Vector2(triangle.data2.x, triangle.data2.y) * scale;
        Vector2 trianglePivot = Vector2.Min(a, Vector2.Min(b, c)) +
            (Vector2.Max(a, Vector2.Max(b, c)) - Vector2.Min(a, Vector2.Min(b, c))) * 0.5f;
        AssertRotatedBoundsContain(triangle, trianglePivot, a);
        AssertRotatedBoundsContain(triangle, trianglePivot, b);
        AssertRotatedBoundsContain(triangle, trianglePivot, c);

        var cardinal = NowSdf.Graph()
            .RotateNext(90f)
            .Box(new NowRect(-4.0001f, -6f, 0.0002f, 4f))
            .nodes[0];
        float oneFloatBeyondIdeal = NextRepresentableFloat(-2f);
        Assert.AreEqual(2f, oneFloatBeyondIdeal - cardinal.data1.x,
            "The shader subtraction must reproduce the cardinal cancellation case.");
        Assert.GreaterOrEqual(cardinal.bounds.xMax, oneFloatBeyondIdeal,
            "Rotated bounds must cover float cancellation even at exact cardinal angles.");
    }

    [Test]
    public void SdfBuilderRotateNextMeasuresBoundsAndUploadsPerNodeMetadata()
    {
        var rect = new NowRect(4f, 6f, 20f, 14f);
        var scene = NowSdf.Scene("sdf-generic-rotation-upload")
            .RotateNext(90f)
            .Box(rect)
            .Circle(rect.center, 2f);

        Assert.AreEqual(21f, scene.Measure().x, 0.002f,
            "Builder measurement must use the rotated primitive bounds.");
        Assert.AreEqual(23f, scene.Measure().y, 0.002f,
            "Builder measurement must use the rotated primitive bounds.");

        using (_drawList.Begin(new Vector2(32f, 32f)))
            scene.Draw(new NowRect(0f, 0f, 32f, 32f));

        var shapeMeta = _drawList.batches[0].material.GetVectorArray("_SdfShapeMeta");
        Assert.AreEqual(0f, shapeMeta[0].z);
        Assert.AreEqual(1f, shapeMeta[0].w);
        Assert.AreEqual(0f, shapeMeta[1].z);
        Assert.AreEqual(0f, shapeMeta[1].w,
            "Builder rotation must reset after the first primitive.");
    }

    [Test]
    public void SdfRotateNextTextUsesSharedRunCenterAndConsumesOnce()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);

        var position = new Vector2(18f, 20f);
        var followingRect = new NowRect(2f, 3f, 4f, 5f);
        const string value = "AB\nC";
        var baseline = NowSdf.Graph()
            .Text(position, value, font, 28f, NowFontStyle.Bold);
        Assert.GreaterOrEqual(baseline.nodes.Count, 3,
            "The shared-pivot fixture requires glyphs on multiple lines.");

        Vector2 pivot = TextGlyphBoundsCenter(baseline, baseline.nodes.Count);
        var rotated = NowSdf.Graph()
            .RotateNext(90f)
            .Text(position, value, font, 28f, NowFontStyle.Bold)
            .Box(followingRect);

        int glyphCount = baseline.nodes.Count;
        Assert.AreEqual(glyphCount + 1, rotated.nodes.Count);

        for (int i = 0; i < glyphCount; ++i)
        {
            NowSdfNode source = baseline.nodes[i];
            NowSdfNode actual = rotated.nodes[i];
            var sourceCenter = new Vector2(source.data1.x, source.data1.y);
            var expectedCenter = new Vector2(
                pivot.x - (sourceCenter.y - pivot.y),
                pivot.y + (sourceCenter.x - pivot.x));
            var actualCenter = new Vector2(actual.data1.x, actual.data1.y);

            Assert.AreEqual(NowSdfShapeType.Glyph, actual.type);
            Assert.AreEqual(expectedCenter.x, actualCenter.x, 0.002f,
                "Every glyph center must orbit one shared text-run pivot.");
            Assert.AreEqual(expectedCenter.y, actualCenter.y, 0.002f,
                "Every glyph center must orbit one shared text-run pivot.");
            Assert.AreEqual(source.data1.z, actual.data1.z);
            Assert.AreEqual(source.data1.w, actual.data1.w);
            Assert.AreEqual(source.data2, actual.data2);
            Assert.AreEqual(source.uv, actual.uv,
                "Text rotation must preserve each glyph's atlas rectangle.");
            AssertRotation(actual.rotation, 90f);

            float halfWidth = Mathf.Max(actual.data1.z * 0.5f, 0.0001f);
            float halfHeight = Mathf.Max(actual.data1.w * 0.5f, 0.0001f);
            AssertRotatedBoundsContain(actual, actualCenter, actualCenter + new Vector2(-halfWidth, -halfHeight));
            AssertRotatedBoundsContain(actual, actualCenter, actualCenter + new Vector2(-halfWidth, halfHeight));
            AssertRotatedBoundsContain(actual, actualCenter, actualCenter + new Vector2(halfWidth, -halfHeight));
            AssertRotatedBoundsContain(actual, actualCenter, actualCenter + new Vector2(halfWidth, halfHeight));
        }

        Assert.AreEqual(Vector2.zero, rotated.nodes[glyphCount].rotation,
            "RotateNext must be consumed by the complete Text call, not leak past its glyphs.");

        double measuredMaxX = double.NegativeInfinity;
        double measuredMaxY = double.NegativeInfinity;
        for (int i = 0; i < rotated.nodes.Count; ++i)
        {
            measuredMaxX = System.Math.Max(measuredMaxX, rotated.nodes[i].bounds.xMax);
            measuredMaxY = System.Math.Max(measuredMaxY, rotated.nodes[i].bounds.yMax);
        }
        Assert.AreEqual((float)measuredMaxX, rotated.measureSize.x, 0.0001f,
            "Graph measurement must be rebuilt from the moved glyph-node bounds.");
        Assert.AreEqual((float)measuredMaxY, rotated.measureSize.y, 0.0001f,
            "Graph measurement must be rebuilt from the moved glyph-node bounds.");

        var scene = NowSdf.Scene("sdf-shared-pivot-text-upload")
            .RotateNext(90f)
            .Text(position, value, font, 28f, NowFontStyle.Bold)
            .Box(followingRect);
        Assert.AreEqual(rotated.measureSize.x, scene.Measure().x, 0.002f);
        Assert.AreEqual(rotated.measureSize.y, scene.Measure().y, 0.002f,
            "Cached-scene measurement must include the rotated text bounds.");

        using (_drawList.Begin(new Vector2(96f, 96f)))
            scene.Draw(new NowRect(0f, 0f, 96f, 96f));

        var shapeMeta = _drawList.batches[0].material.GetVectorArray("_SdfShapeMeta");
        for (int i = 0; i < glyphCount; ++i)
            AssertRotation(new Vector2(shapeMeta[i].z, shapeMeta[i].w), 90f);
        Assert.AreEqual(Vector2.zero,
            new Vector2(shapeMeta[glyphCount].z, shapeMeta[glyphCount].w));
    }

    [Test]
    public void SdfTextRotationScopesComposePerRun()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);

        const float fontSize = 18f;
        int firstCount = NowSdf.Graph().Text(new Vector2(4f, 4f), "AB", font, fontSize).nodes.Count;
        int nextCount = NowSdf.Graph().Text(new Vector2(28f, 4f), "CD", font, fontSize).nodes.Count;
        int scopedCount = NowSdf.Graph().Text(new Vector2(52f, 4f), "EF", font, fontSize).nodes.Count;
        int finalCount = NowSdf.Graph().Text(new Vector2(76f, 4f), "G", font, fontSize).nodes.Count;
        Assert.Greater(firstCount, 0);
        Assert.Greater(nextCount, 0);
        Assert.Greater(scopedCount, 0);
        Assert.Greater(finalCount, 0);

        var graph = NowSdf.Graph()
            .PushRotation(20f)
                .Text(new Vector2(4f, 4f), "AB", font, fontSize)
                .RotateNext(10f)
                .Text(new Vector2(28f, 4f), "CD", font, fontSize)
                .Text(new Vector2(52f, 4f), "EF", font, fontSize)
            .PopRotation()
            .Text(new Vector2(76f, 4f), "G", font, fontSize);

        int cursor = 0;
        AssertRotationRange(graph, cursor, firstCount, 20f);
        cursor += firstCount;
        AssertRotationRange(graph, cursor, nextCount, 30f,
            "RotateNext must compose with the pushed angle for one complete text run.");
        cursor += nextCount;
        AssertRotationRange(graph, cursor, scopedCount, 20f,
            "RotateNext must be consumed while the pushed rotation remains active.");
        cursor += scopedCount;
        AssertRotationRange(graph, cursor, finalCount, 0f,
            "PopRotation must restore identity for following text runs.");
        cursor += finalCount;
        Assert.AreEqual(cursor, graph.nodes.Count);
    }

    [Test]
    public void SdfEmptyTextConsumesRotateNextAndPreservesPushedRotation()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);
        var rect = new NowRect(8f, 10f, 24f, 12f);

        var graph = NowSdf.Graph()
            .PushRotation(15f)
                .RotateNext(10f)
                .Text(new Vector2(4f, 4f), string.Empty, font, 20f)
                .Box(rect)
            .PopRotation();

        Assert.AreEqual(1, graph.nodes.Count);
        AssertRotation(graph.nodes[0].rotation, 15f,
            "An empty Text call must consume RotateNext without clearing its pushed rotation.");

        var noInk = NowSdf.Graph()
            .RotateNext(90f)
            .Text(new Vector2(4f, 4f), "\n\t", font, 20f)
            .Box(rect);
        Assert.AreEqual(1, noInk.nodes.Count);
        Assert.AreEqual(Vector2.zero, noInk.nodes[0].rotation,
            "A text run that emits no glyphs must still consume RotateNext.");

        var emptyScene = NowSdf.Scene(
                new NowRect(0f, 0f, 48f, 32f),
                "sdf-empty-rotated-text")
            .RotateNext(90f)
            .Text(new Vector2(4f, 4f), string.Empty, font, 20f);
        Assert.AreEqual(Vector2.zero, emptyScene.Measure());

        using (_drawList.Begin(new Vector2(48f, 32f)))
            emptyScene.Draw();

        Assert.AreEqual(0, _drawList.batchCount,
            "A rotated empty text run must remain an empty scene.");
    }

    [Test]
    public void SdfRotationStackComposesAndRotateNextTargetsOnePrimitive()
    {
        var rect = new NowRect(4f, 6f, 20f, 14f);
        var graph = NowSdf.Graph()
            .PushRotation(20f)
            .Box(rect)
            .RotateNext(10f)
            .Triangle(Vector2.zero, Vector2.right * 20f, Vector2.up * 20f)
            .Circle(new Vector2(32f, 32f), 4f)
            .PushRotation(-5f)
            .Ellipse(rect)
            .PopRotation()
            .PopRotation()
            .RoundedBox(rect, 2f);

        AssertRotation(graph.nodes[0].rotation, 20f);
        AssertRotation(graph.nodes[1].rotation, 30f);
        AssertRotation(graph.nodes[2].rotation, 20f,
            "RotateNext must be consumed without changing the pushed rotation.");
        AssertRotation(graph.nodes[3].rotation, 15f,
            "Nested PushRotation calls must compose relative angles.");
        Assert.AreEqual(Vector2.zero, graph.nodes[4].rotation,
            "PopRotation must restore the parent rotation and eventually identity.");

        var skipped = NowSdf.Graph()
            .PushRotation(20f)
            .RotateNext(10f)
            .ChamferedBox(new NowRect(0f, 0f, 0f, 10f), 2f)
            .Box(rect)
            .PopRotation();
        AssertRotation(skipped.nodes[0].rotation, 20f,
            "A skipped primitive must consume RotateNext but preserve pushed rotation.");

        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        var cancelledForText = NowSdf.Graph()
            .PushRotation(20f)
            .RotateNext(-20f)
            .Text(new Vector2(2f, 2f), "R", font, 12f)
            .Box(rect)
            .PopRotation();
        AssertRotation(cancelledForText.nodes[cancelledForText.nodes.Count - 1].rotation, 20f,
            "An identity-cancelled Text operand must consume RotateNext without clearing its pushed rotation.");

        var skippedText = NowSdf.Graph()
            .PushRotation(20f)
            .RotateNext(-20f)
            .Text(Vector2.zero, string.Empty, 16f)
            .Box(rect)
            .PopRotation();
        AssertRotation(skippedText.nodes[0].rotation, 20f,
            "A skipped identity-cancelled Text operand must also consume RotateNext.");

        using (_drawList.Begin(new Vector2(64f, 64f)))
        {
            NowSdf.Scene(new NowRect(0f, 0f, 64f, 64f), "sdf-rotation-stack-upload")
                .PushRotation(20f)
                    .Box(rect)
                    .RotateNext(10f)
                    .Circle(new Vector2(32f, 32f), 4f)
                .PopRotation()
                .Box(rect)
                .Draw();
        }

        var shapeMeta = _drawList.batches[0].material.GetVectorArray("_SdfShapeMeta");
        AssertRotation(new Vector2(shapeMeta[0].z, shapeMeta[0].w), 20f);
        AssertRotation(new Vector2(shapeMeta[1].z, shapeMeta[1].w), 30f);
        Assert.AreEqual(Vector2.zero, new Vector2(shapeMeta[2].z, shapeMeta[2].w));
    }

    [Test]
    public void SdfRotationStackValidatesBalanceAndPreservesTransactionalState()
    {
        var rect = new NowRect(4f, 6f, 20f, 14f);
        var graph = NowSdf.Graph().PushRotation(20f).RotateNext(10f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => graph.PushRotation(float.NaN));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Triangle(Vector2.zero, new Vector2(float.PositiveInfinity, 0f), Vector2.one));
        graph.Box(rect);
        AssertRotation(graph.nodes[0].rotation, 30f,
            "Rejected input must retain both pushed and next-primitive rotation state.");
        graph.PopRotation();
        Assert.Throws<System.InvalidOperationException>(() => graph.PopRotation());

        var openGraph = NowSdf.Graph().PushRotation(15f).Box(rect);
        var scene = NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "sdf-open-rotation-graph");
        Assert.Throws<System.InvalidOperationException>(() => scene.Graph(openGraph));
        openGraph.PopRotation();
        scene = scene.Graph(openGraph);

        var openScene = NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "sdf-open-rotation-scene")
            .PushRotation(15f)
            .Box(rect);
        Assert.Throws<System.InvalidOperationException>(() => openScene.Draw());
        openScene = openScene.PopRotation();

        using (_drawList.Begin(new Vector2(32f, 32f)))
            openScene.Draw();

        graph.Clear().Box(rect);
        Assert.AreEqual(Vector2.zero, graph.nodes[0].rotation,
            "Clear must discard pushed and next-primitive rotation state.");
    }

    [Test]
    public void SdfRotationStackDoesNotAllocateAfterWarmup()
    {
        var graph = NowSdf.Graph();
        var rect = new NowRect(4f, 6f, 20f, 14f);

        for (int i = 0; i < 8; ++i)
        {
            graph.Clear()
                .PushRotation(20f)
                .PushRotation(-5f)
                .RotateNext(10f)
                .Box(rect)
                .PopRotation()
                .PopRotation();
        }

        long before;
        try
        {
            before = System.GC.GetAllocatedBytesForCurrentThread();
        }
        catch (System.MissingMethodException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return;
        }

        for (int i = 0; i < 128; ++i)
        {
            graph.Clear()
                .PushRotation(20f)
                .PushRotation(-5f)
                .RotateNext(10f)
                .Box(rect)
                .PopRotation()
                .PopRotation();
        }

        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0, allocated,
            "A warmed SDF rotation stack must not allocate managed memory.");

        var sceneId = new NowId(0x5DF20);
        Vector2 measured = default;
        for (int i = 0; i < 8; ++i)
        {
            measured = NowSdf.Scene(sceneId)
                .PushRotation(20f)
                .PushRotation(-5f)
                .RotateNext(10f)
                .Box(rect)
                .PopRotation()
                .PopRotation()
                .Measure();
        }

        before = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; ++i)
        {
            measured = NowSdf.Scene(sceneId)
                .PushRotation(20f)
                .PushRotation(-5f)
                .RotateNext(10f)
                .Box(rect)
                .PopRotation()
                .PopRotation()
                .Measure();
        }

        allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Greater(measured.x, 0f);
        Assert.AreEqual(0, allocated,
            "A warmed cached-scene rotation stack must not allocate managed memory.");
    }

    [Test]
    public void SdfRotatedTextDoesNotAllocateAfterWarmup()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);
        const string value = "AB\nCD";
        const float fontSize = 20f;
        var position = new Vector2(12f, 10f);
        var graph = NowSdf.Graph();
        Vector2 measured = default;

        for (int i = 0; i < 8; ++i)
        {
            graph.Clear()
                .PushRotation(15f)
                .RotateNext(10f)
                .Text(position, value, font, fontSize)
                .PopRotation();
            measured = graph.measureSize;
        }

        long before;
        try
        {
            before = System.GC.GetAllocatedBytesForCurrentThread();
        }
        catch (System.MissingMethodException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return;
        }

        for (int i = 0; i < 128; ++i)
        {
            graph.Clear()
                .PushRotation(15f)
                .RotateNext(10f)
                .Text(position, value, font, fontSize)
                .PopRotation();
            measured = graph.measureSize;
        }

        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Greater(measured.x, 0f);
        Assert.Greater(measured.y, 0f);
        Assert.AreEqual(0, allocated,
            "Rebuilding a warmed rotated text graph must not allocate managed memory.");

        var sceneId = new NowId(0x5DF21);
        for (int i = 0; i < 8; ++i)
        {
            measured = NowSdf.Scene(sceneId)
                .PushRotation(15f)
                .RotateNext(10f)
                .Text(position, value, font, fontSize)
                .PopRotation()
                .Measure();
        }

        before = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; ++i)
        {
            measured = NowSdf.Scene(sceneId)
                .PushRotation(15f)
                .RotateNext(10f)
                .Text(position, value, font, fontSize)
                .PopRotation()
                .Measure();
        }

        allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Greater(measured.x, 0f);
        Assert.Greater(measured.y, 0f);
        Assert.AreEqual(0, allocated,
            "Rebuilding a warmed cached scene with rotated text must not allocate managed memory.");
    }

    [Test]
    public void SdfPlanarPrimitiveBoundaryContractsAreStable()
    {
        var graph = NowSdf.Graph()
            .ChamferedBox(new NowRect(2f, 3f, 20f, 12f), 100f)
            .Subtract()
            .ChamferedBox(new NowRect(4f, 5f, 0f, 9f), 2f)
            .Circle(new Vector2(20f, 20f), 3f)
            .Triangle(
                new Vector2(0f, 40f),
                new Vector2(1000f, 40f),
                new Vector2(1000f, 40.001f));

        Assert.AreEqual(3, graph.nodes.Count, "An empty ChamferedBox must not add a node.");
        Assert.AreEqual(6f, graph.nodes[0].data2.x,
            "Chamfer must clamp to half the shorter side.");
        Assert.AreEqual(NowSdfOperation.Union, graph.nodes[1].operation,
            "A skipped primitive must consume its pending boolean operation.");
        Assert.AreEqual(0f, graph.nodes[2].data2.z,
            "A numerically near-collinear Triangle must use an unsigned edge field.");
        Assert.AreEqual(1000f, graph.nodes[2].data2.w,
            "Triangle normalization must retain its component span for shader distances.");
    }

    [Test]
    public void SdfTriangleDegeneracyCutoffIsScaleRelativeAndWindingAware()
    {
        var a = Vector2.zero;
        var b = new Vector2(1024f, 0f);
        var graph = NowSdf.Graph()
            .Triangle(a, b, new Vector2(1024f, 1f / 256f))
            .Triangle(a, b, new Vector2(1024f, 1f / 128f))
            .Triangle(a, b, new Vector2(1024f, 1f / 64f))
            .Triangle(a, new Vector2(1024f, 1f / 64f), b)
            .Triangle(a, a, a);

        Assert.AreEqual(0f, graph.nodes[0].data2.z,
            "A triangle below the relative-area cutoff must be unsigned.");
        Assert.AreEqual(0f, graph.nodes[1].data2.z,
            "The inclusive relative-area cutoff must be deterministic.");
        Assert.AreEqual(1f, graph.nodes[2].data2.z);
        Assert.AreEqual(-1f, graph.nodes[3].data2.z);
        Assert.AreEqual(0f, graph.nodes[4].data2.z);
        Assert.AreEqual(1f, graph.nodes[4].data2.w,
            "A coincident Triangle must retain a finite normalization scale.");

        const float largeScale = 1099511627776f; // 2^40
        var scaled = NowSdf.Graph()
            .Triangle(
                a,
                b * largeScale,
                new Vector2(1024f, 1f / 64f) * largeScale);
        Assert.AreEqual(graph.nodes[2].data1.z, scaled.nodes[0].data1.z, 0.000001f);
        Assert.AreEqual(graph.nodes[2].data1.w, scaled.nodes[0].data1.w, 0.000001f);
        Assert.AreEqual(graph.nodes[2].data2.x, scaled.nodes[0].data2.x, 0.000001f);
        Assert.AreEqual(graph.nodes[2].data2.y, scaled.nodes[0].data2.y, 0.000001f);
        Assert.AreEqual(graph.nodes[2].data2.z, scaled.nodes[0].data2.z);
    }

    [Test]
    public void SdfTriangleBoundsIncludeShaderReconstructedVertices()
    {
        var graph = NowSdf.Graph()
            .Triangle(
                new Vector2(-500.123f, 200.456f),
                new Vector2(619.6848f, -24.779821f),
                new Vector2(0f, 2000f))
            .Triangle(
                new Vector2(-744115.375f, 915255.5f),
                new Vector2(-503228.75f, 624424f),
                new Vector2(785268.9375f, 257079.71875f));

        for (int i = 0; i < graph.nodes.Count; ++i)
        {
            var node = graph.nodes[i];
            var a = new Vector2(node.data1.x, node.data1.y);
            Vector2 reconstructedB = a + new Vector2(node.data1.z, node.data1.w) * node.data2.w;
            Vector2 reconstructedC = a + new Vector2(node.data2.x, node.data2.y) * node.data2.w;

            Assert.LessOrEqual(node.bounds.x, Mathf.Min(reconstructedB.x, reconstructedC.x));
            Assert.LessOrEqual(node.bounds.y, Mathf.Min(reconstructedB.y, reconstructedC.y));
            Assert.GreaterOrEqual(node.bounds.xMax, Mathf.Max(reconstructedB.x, reconstructedC.x),
                "Packed Triangle reconstruction can move a vertex one ULP beyond its authored AABB.");
            Assert.GreaterOrEqual(node.bounds.yMax, Mathf.Max(reconstructedB.y, reconstructedC.y));
        }

        Assert.GreaterOrEqual(graph.nodes[1].bounds.xMax, 785268.9375f,
            "NowRect size reconstruction must not round the authored maximum inward.");
    }

    [Test]
    public void SdfRotateNextCapsuleMidpointDoesNotOverflowForFiniteEndpoints()
    {
        float large = float.MaxValue;
        var graph = NowSdf.Graph()
            .RotateNext(180f)
            .Line(
                new Vector2(large * 0.6f, 0f),
                new Vector2(large * 0.5f, 0f),
                0f);

        Assert.AreEqual(1, graph.nodes.Count,
            "Computing the Capsule pivot must not add its finite endpoints before halving them.");
        Assert.IsFalse(float.IsNaN(graph.nodes[0].bounds.x));
        Assert.IsFalse(float.IsInfinity(graph.nodes[0].bounds.x));
        Assert.IsFalse(float.IsNaN(graph.nodes[0].bounds.xMax));
        Assert.IsFalse(float.IsInfinity(graph.nodes[0].bounds.xMax));
    }

    [Test]
    public void SdfPlanarPrimitiveOverflowDoesNotMutateGraph()
    {
        var graph = NowSdf.Graph().Circle(new Vector2(8f, 8f), 4f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Line(
                new Vector2(-float.MaxValue, 0f),
                new Vector2(float.MaxValue, 0f),
                0f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Triangle(
                new Vector2(-float.MaxValue, 0f),
                new Vector2(float.MaxValue, 0f),
                Vector2.zero));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.RotateNext(45f).Box(
                new NowRect(
                    float.MaxValue * 0.75f,
                    0f,
                    float.MaxValue * 0.125f,
                    float.MaxValue * 0.5f)));

        Assert.AreEqual(1, graph.nodes.Count, "Overflowing planar bounds mutated the graph.");
        Assert.AreEqual(new Vector2(12f, 12f), graph.measureSize,
            "A rejected rotated bound must not mutate measured graph bounds.");

        graph.Box(new NowRect(2f, 3f, 20f, 12f));
        Assert.AreEqual(2, graph.nodes.Count);
        Assert.AreEqual(Mathf.Sqrt(0.5f), graph.nodes[1].rotation.x, 0.000001f);
        Assert.AreEqual(Mathf.Sqrt(0.5f), graph.nodes[1].rotation.y, 0.000001f,
            "A bounds exception must leave the pending rotation intact.");
    }

    [Test]
    public void SdfPlanarPrimitivesRejectNonFiniteArgumentsWithoutMutation()
    {
        var graph = NowSdf.Graph().Circle(new Vector2(8f, 8f), 4f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.ChamferedBox(new NowRect(float.NaN, 0f, 12f, 8f), 2f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.ChamferedBox(new NowRect(0f, 0f, 12f, 8f), float.PositiveInfinity));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Triangle(Vector2.zero, new Vector2(float.PositiveInfinity, 0f), Vector2.one));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Line(Vector2.zero, new Vector2(float.NaN, 0f), 3f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            graph.Line(Vector2.zero, Vector2.one, float.PositiveInfinity));

        Assert.AreEqual(1, graph.nodes.Count, "Invalid planar inputs mutated the graph.");
    }

    [Test]
    public void SdfMaskCaptureGeometryPreservesSubpixelBounds()
    {
        var material = Resources.Load<Material>("NowUI/SdfMaterial");
        Assert.NotNull(material);

        var rect = new NowRect(0f, 0f, 0.25f, 0.375f);

        using (_drawList.Begin(rect.size))
            Now.DrawSdfUnsnapped(rect, rect, material, Vector4.one);

        Assert.AreEqual(4, _drawList.mesh.vertexCount);
        var vertices = _drawList.mesh.vertices;
        Assert.AreEqual(new Vector3(0f, -rect.height, 0f), vertices[0]);
        Assert.AreEqual(new Vector3(0f, 0f, 0f), vertices[1]);
        Assert.AreEqual(new Vector3(rect.width, 0f, 0f), vertices[2]);
        Assert.AreEqual(new Vector3(rect.width, -rect.height, 0f), vertices[3]);
    }

    [Test]
    public void SdfSceneBindsTextureToMaterial()
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);

        try
        {
            using (_drawList.Begin(new Vector2(80, 80)))
            {
                NowSdf.Scene(new NowRect(0, 0, 80, 80))
                    .SetTexture(texture)
                    .RoundedBox(new NowRect(8, 8, 64, 64), 12)
                    .Draw();
            }

            Assert.AreSame(texture, _drawList.batches[0].material.mainTexture);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SdfSceneUploadsEffectSettings()
    {
        using (_drawList.Begin(new Vector2(120, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 120, 90))
                .SetColor(Color.white)
                .SetOutline(4f, new Color(0f, 0f, 0f, 0.75f), 1.5f)
                .SetGlow(18f, new Color(0.2f, 0.7f, 1f, 0.45f), 2f)
                .SetShadow(new Vector2(6f, 8f), 12f, new Color(0f, 0f, 0f, 0.35f), 2f)
                .SetInnerShadow(new Vector2(-3f, -4f), 7f, new Color(0f, 0f, 0f, 0.28f), 1f)
                .SetEmboss(new Vector2(-1f, -1f), 0.4f, 5f)
                .SetContours(10f, 1.5f, new Color(1f, 1f, 1f, 0.2f), 3f, 2)
                .SetContourMask(new Vector2(48f, 36f), 22f, 6f)
                .SetWarp(3f, 42f, 0.6f, 9f)
                .RoundedBox(new NowRect(16, 18, 88, 54), 18)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(new Vector4(4f, 1.5f, 0f, 0f), material.GetVector("_SdfOutline"));
        Assert.AreEqual(new Vector4(18f, 2f, 0f, 0f), material.GetVector("_SdfGlow"));
        Assert.AreEqual(new Vector4(6f, 8f, 12f, 2f), material.GetVector("_SdfShadow"));
        Assert.AreEqual(new Vector4(-3f, -4f, 7f, 1f), material.GetVector("_SdfInnerShadow"));
        Assert.AreEqual(new Vector4(10f, 1.5f, 3f, 2f), material.GetVector("_SdfContour"));
        Assert.AreEqual(new Vector4(48f, 36f, 22f, 6f), material.GetVector("_SdfContourMask"));
        Assert.AreEqual(new Vector4(3f, 42f, 0.6f, 9f), material.GetVector("_SdfWarp"));

        var emboss = material.GetVector("_SdfEmboss");
        Assert.AreEqual(5f, emboss.z, 0.0001f);
        Assert.AreEqual(0.4f, emboss.w, 0.0001f);
        Assert.AreEqual(1f, new Vector2(emboss.x, emboss.y).magnitude, 0.0001f);
    }

    [Test]
    public void SdfSceneCanUseTextAsOperationOperand()
    {
        var font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(font);

        using (_drawList.Begin(new Vector2(180, 96)))
        {
            NowSdf.Scene(new NowRect(0, 0, 180, 96))
                .SetColor(Color.blue)
                .RoundedBox(new NowRect(16, 18, 148, 60), 18)
                .SmoothSubtract(3f)
                .Text(new Vector2(48, 28), "SDF", font, 32, NowFontStyle.Bold)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(1, _drawList.batchCount);

        var material = _drawList.batches[0].material;
        Assert.NotNull(material.mainTexture);
        Assert.AreEqual(1f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(4f, material.GetFloat("_SdfShapeCount"), 0.0001f);

        var shapeData = material.GetVectorArray("_SdfData0");
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[1].y, 0.0001f);
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[2].y, 0.0001f);
        Assert.AreEqual((float)NowSdfOperation.SmoothSubtract, shapeData[3].y, 0.0001f);
    }

    [Test]
    public void SdfTextEffectSettersAllocateOnlyAtTerminal()
    {
        var font = CreateManagedDynamicFont();

        try
        {
            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 240f, 120f),
                    new NowId("sdf-text-lazy-range"))
                .Text(new Vector2(16f, 16f), "A", font, 80f);

            int pagesBeforeSetters = font.GetCachedDynamicPageCount();
            int glyphsBeforeSetters = font.GetCachedDynamicGlyphCount();
            long bytesBeforeSetters = font.GetEstimatedDynamicCacheResidentBytes();
            Assert.Greater(pagesBeforeSetters, 0, "Text construction must establish the base fixture page.");

            scene = scene.SetOutline(100f, Color.black);
            scene = scene.SetTextDistanceMargin(120f);

            Assert.AreEqual(pagesBeforeSetters, font.GetCachedDynamicPageCount(),
                "Effect setters must not allocate an atlas variant before a terminal operation.");
            Assert.AreEqual(glyphsBeforeSetters, font.GetCachedDynamicGlyphCount(),
                "Effect setters must not resolve glyphs before a terminal operation.");
            Assert.AreEqual(bytesBeforeSetters, font.GetEstimatedDynamicCacheResidentBytes(),
                "Inspector-style setter changes must remain allocation-free until measure or draw.");
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfLargeTextReachSelectsPackedExtendedRangeAtTerminal(bool useManualMargin)
    {
        const float fontSize = 80f;
        const float reach = 100f;
        var font = CreateManagedDynamicFont();

        try
        {
            var source = NowSdf.Graph()
                .Text(new Vector2(16f, 16f), "AB", font, fontSize);
            Assert.AreEqual(2, source.nodes.Count);

            int basePixelRange = font.GetDynamicPixelRange(0f, fontSize);
            int requestedPixelRange = font.GetDynamicPixelRange(reach / fontSize, fontSize);
            Assert.Greater(requestedPixelRange, basePixelRange,
                "The fixture must select a range above the base tier.");

            float baseRangeA = source.nodes[0].data2.x;
            float baseRangeB = source.nodes[1].data2.x;
            Texture baseTexture = source.texture;
            int pagesBeforeTerminal = font.GetCachedDynamicPageCount();

            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 280f, 140f),
                    new NowId(useManualMargin
                        ? "sdf-text-large-manual-margin"
                        : "sdf-text-large-outline"))
                .Graph(source);
            scene = useManualMargin
                ? scene.SetTextDistanceMargin(reach)
                : scene.SetOutline(reach, Color.black);

            Assert.AreEqual(pagesBeforeTerminal, font.GetCachedDynamicPageCount(),
                "Selecting the requested reach must remain lazy until draw.");

            using (_drawList.Begin(new Vector2(280f, 140f)))
                scene.Draw();

            Assert.AreEqual(1, _drawList.batchCount);
            var material = _drawList.batches[0].material;
            var shapeData = material.GetVectorArray("_SdfData0");
            var textData = material.GetVectorArray("_SdfData2");

            Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f);
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[0].x);
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[1].x);
            Assert.Greater(textData[0].x, baseRangeA,
                "The first glyph must upload the extended screen-space field range.");
            Assert.Greater(textData[1].x, baseRangeB,
                "The second glyph must upload the extended screen-space field range.");
            Assert.AreEqual(textData[0].x, textData[1].x, 0.0001f,
                "Equal-size glyphs must share the selected scene range.");
            Assert.AreEqual(1f, textData[0].y, 0.0001f,
                "Managed dynamic glyphs must upload packed-distance encoding.");
            Assert.AreEqual(1f, textData[1].y, 0.0001f,
                "Every glyph on the shared page must use packed-distance encoding.");
            Assert.NotNull(material.mainTexture);
            Assert.AreNotSame(baseTexture, material.mainTexture,
                "A distinct extended-range fixture must not keep sampling the base page.");
            Assert.Greater(font.GetCachedDynamicPageCount(), pagesBeforeTerminal,
                "The terminal draw must allocate the requested extended-range page.");
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfTextDistanceMarginIsOrderIndependent()
    {
        const float fontSize = 64f;
        const float margin = 48f;
        var font = CreateManagedDynamicFont();

        try
        {
            Assert.Greater(
                font.GetDynamicPixelRange(margin / fontSize, fontSize),
                font.GetDynamicPixelRange(0f, fontSize),
                "The fixture must select a non-base range.");

            var marginBeforeText = NowSdf.Scene(
                    new NowRect(0f, 0f, 180f, 96f),
                    new NowId("sdf-margin-before-text"))
                .SetTextDistanceMargin(margin)
                .Text(new Vector2(16f, 12f), "A", font, fontSize);

            using (_drawList.Begin(new Vector2(180f, 96f)))
                marginBeforeText.Draw();

            var firstMaterial = _drawList.batches[0].material;
            Vector4 firstTextData = firstMaterial.GetVectorArray("_SdfData2")[0];
            Texture firstTexture = firstMaterial.mainTexture;

            _drawList.Clear();

            var marginAfterText = NowSdf.Scene(
                    new NowRect(0f, 0f, 180f, 96f),
                    new NowId("sdf-margin-after-text"))
                .Text(new Vector2(16f, 12f), "A", font, fontSize)
                .SetTextDistanceMargin(margin);

            using (_drawList.Begin(new Vector2(180f, 96f)))
                marginAfterText.Draw();

            var secondMaterial = _drawList.batches[0].material;
            Vector4 secondTextData = secondMaterial.GetVectorArray("_SdfData2")[0];

            Assert.AreEqual(firstTextData.x, secondTextData.x, 0.0001f,
                "Setting the semantic distance margin before or after Text must select the same range.");
            Assert.AreEqual(1f, firstTextData.y, 0.0001f);
            Assert.AreEqual(1f, secondTextData.y, 0.0001f);
            Assert.AreSame(firstTexture, secondMaterial.mainTexture,
                "Equivalent setter order must resolve the same cached atlas variant.");
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfTerminalRangePreparationDoesNotMutateReusableTextGraph()
    {
        const float fontSize = 72f;
        const float margin = 60f;
        var font = CreateManagedDynamicFont();

        try
        {
            var source = NowSdf.Graph()
                .Text(new Vector2(18f, 14f), "A", font, fontSize);
            Assert.AreEqual(1, source.nodes.Count);

            NowSdfNode sourceNode = source.nodes[0];
            Texture sourceTexture = source.texture;
            Vector2 sourceMeasure = source.measureSize;

            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 220f, 120f),
                    new NowId("sdf-reusable-text-range-copy"))
                .Graph(source)
                .SetTextDistanceMargin(margin);

            using (_drawList.Begin(new Vector2(220f, 120f)))
                scene.Draw();

            var material = _drawList.batches[0].material;
            Vector4 uploadedTextData = material.GetVectorArray("_SdfData2")[0];

            Assert.Greater(uploadedTextData.x, sourceNode.data2.x,
                "The scene copy must use the requested extended range.");
            Assert.AreEqual(1f, uploadedTextData.y, 0.0001f);
            Assert.AreSame(sourceTexture, source.texture,
                "Terminal preparation must not replace a reusable graph's texture.");
            Assert.AreEqual(sourceMeasure, source.measureSize,
                "Terminal preparation must not rebuild reusable source bounds in place.");
            Assert.AreEqual(1, source.nodes.Count);
            Assert.AreEqual(sourceNode.type, source.nodes[0].type);
            Assert.AreEqual(sourceNode.data1, source.nodes[0].data1);
            Assert.AreEqual(sourceNode.data2, source.nodes[0].data2,
                "The reusable graph must retain its base range and encoding data.");
            Assert.AreEqual(sourceNode.uv, source.nodes[0].uv);
            AssertRectApproximately(sourceNode.bounds, source.nodes[0].bounds, 0f);
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfMixedFontSizesUseOneSharedExtendedTextRange()
    {
        const float smallSize = 24f;
        const float largeSize = 96f;
        const float margin = 24f;
        var font = CreateManagedDynamicFont();

        try
        {
            int sharedPixelRange = Mathf.Max(
                font.GetDynamicPixelRange(margin / smallSize, smallSize),
                font.GetDynamicPixelRange(margin / largeSize, largeSize));
            Assert.Greater(sharedPixelRange, font.GetDynamicPixelRange(0f, smallSize),
                "The mixed-size fixture must select an extended range.");

            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 320f, 150f),
                    new NowId("sdf-mixed-font-size-shared-range"))
                .SetTextDistanceMargin(margin)
                .Text(new Vector2(14f, 16f), "A", font, smallSize)
                .Text(new Vector2(92f, 16f), "B", font, largeSize);

            using (_drawList.Begin(new Vector2(320f, 150f)))
                scene.Draw();

            Assert.AreEqual(1, _drawList.batchCount,
                "All font sizes in the SDF scene must remain on one texture batch.");
            var material = _drawList.batches[0].material;
            var shapeData = material.GetVectorArray("_SdfData0");
            var textData = material.GetVectorArray("_SdfData2");

            Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f,
                "Neither glyph may disappear while reconciling a shared atlas range.");
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[0].x);
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[1].x);
            Assert.NotNull(material.mainTexture);
            Assert.AreEqual(
                font.GetScreenPixelRangeForPixelRange('A', smallSize, sharedPixelRange),
                textData[0].x,
                0.0001f,
                "The small glyph must be resolved against the scene's shared raw range.");
            Assert.AreEqual(
                font.GetScreenPixelRangeForPixelRange('B', largeSize, sharedPixelRange),
                textData[1].x,
                0.0001f,
                "The large glyph must be resolved against the same shared raw range.");
            Assert.Greater(textData[0].x, 0f);
            Assert.Greater(textData[1].x, 0f);
            Assert.AreEqual(1f, textData[0].y, 0.0001f);
            Assert.AreEqual(1f, textData[1].y, 0.0001f);
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfTextGraphClearThenTextStillAdaptsAtTerminal()
    {
        const float fontSize = 64f;
        const float margin = 40f;
        var font = CreateManagedDynamicFont();

        try
        {
            var graph = NowSdf.Graph()
                .Text(new Vector2(12f, 12f), "A", font, fontSize);

            graph.Clear()
                .Text(new Vector2(20f, 16f), "B", font, fontSize);

            Assert.AreEqual(1, graph.nodes.Count,
                "Clear followed by Text must leave only the replacement glyph source.");
            Assert.AreEqual(NowSdfShapeType.Glyph, graph.nodes[0].type);
            float baseScreenRange = graph.nodes[0].data2.x;

            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 180f, 100f),
                    new NowId("sdf-clear-then-text-range"))
                .Graph(graph)
                .SetTextDistanceMargin(margin);

            using (_drawList.Begin(new Vector2(180f, 100f)))
                scene.Draw();

            var material = _drawList.batches[0].material;
            var shapeData = material.GetVectorArray("_SdfData0");
            var textData = material.GetVectorArray("_SdfData2");
            Assert.AreEqual(1f, material.GetFloat("_SdfShapeCount"), 0.0001f);
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[0].x);
            Assert.Greater(textData[0].x, baseScreenRange,
                "The replacement glyph must participate in adaptive terminal preparation.");
            Assert.AreEqual(1f, textData[0].y, 0.0001f);
            Assert.NotNull(material.mainTexture);
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfDrawObservesReusableTextGraphMutationAfterMeasure()
    {
        const float fontSize = 64f;
        const float margin = 40f;
        var font = CreateManagedDynamicFont();

        try
        {
            var source = NowSdf.Graph()
                .Text(new Vector2(14f, 12f), "A", font, fontSize);
            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 220f, 110f),
                    new NowId("sdf-measure-then-mutate-text-graph"))
                .Graph(source)
                .SetTextDistanceMargin(margin);

            Assert.Greater(scene.Measure().sqrMagnitude, 0f);
            Assert.AreEqual(1, source.nodes.Count);

            source.Clear()
                .Text(new Vector2(22f, 16f), "BB", font, fontSize);
            Assert.AreEqual(2, source.nodes.Count,
                "The external graph mutation must replace the measured source content.");

            using (_drawList.Begin(new Vector2(220f, 110f)))
                scene.Draw();

            Assert.AreEqual(1, _drawList.batchCount);
            var material = _drawList.batches[0].material;
            var shapeData = material.GetVectorArray("_SdfData0");
            var textData = material.GetVectorArray("_SdfData2");
            Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f,
                "Draw must invalidate the measured clone and upload the replacement glyphs.");
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[0].x);
            Assert.AreEqual((float)NowSdfShapeType.Glyph, shapeData[1].x);
            Assert.Greater(textData[0].x, source.nodes[0].data2.x);
            Assert.Greater(textData[1].x, source.nodes[1].data2.x);
            Assert.AreEqual(1f, textData[0].y, 0.0001f);
            Assert.AreEqual(1f, textData[1].y, 0.0001f);
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfDrawRefreshesTextAtlasAfterCacheClearFollowingMeasure()
    {
        const float fontSize = 64f;
        const float margin = 40f;
        var font = CreateManagedDynamicFont();

        try
        {
            int requestedPixelRange = font.GetDynamicPixelRange(margin / fontSize, fontSize);
            var source = NowSdf.Graph()
                .Text(new Vector2(14f, 12f), "A", font, fontSize);
            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 200f, 100f),
                    new NowId("sdf-measure-then-clear-font-cache"))
                .Graph(source)
                .SetTextDistanceMargin(margin);

            Assert.Greater(scene.Measure().sqrMagnitude, 0f);
            Assert.IsTrue(font.GetGlyphForPixelRange(
                'A',
                fontSize,
                requestedPixelRange,
                out _,
                out Material measuredGlyphMaterial));
            Texture measuredTexture = measuredGlyphMaterial.mainTexture;
            Assert.NotNull(measuredTexture);
            Assert.Greater(font.GetCachedDynamicPageCount(), 0);

            font.ClearDynamicCache();

            Assert.AreEqual(0, font.GetCachedDynamicPageCount());
            Assert.IsTrue(measuredTexture == null,
                "Clearing the owner cache must destroy the atlas retained by the measured clone.");

            using (_drawList.Begin(new Vector2(200f, 100f)))
                scene.Draw();

            Assert.AreEqual(1, _drawList.batchCount);
            var material = _drawList.batches[0].material;
            var textData = material.GetVectorArray("_SdfData2")[0];
            Assert.IsTrue(material.mainTexture != null,
                "Draw must refresh the prepared clone to a live atlas texture.");
            Assert.IsFalse(ReferenceEquals(measuredTexture, material.mainTexture),
                "The destroyed atlas object must not be rebound after cache recreation.");
            Assert.Greater(font.GetCachedDynamicPageCount(), 0);
            Assert.AreEqual(
                font.GetScreenPixelRangeForPixelRange('A', fontSize, requestedPixelRange),
                textData.x,
                0.0001f);
            Assert.AreEqual(1f, textData.y, 0.0001f);
            Assert.AreEqual(textData.x / 65535f, textData.z, 0.0000001f);
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfGlyphUploadUsesEncodingSpecificDistanceCodeStep()
    {
        const float fontSize = 64f;
        var legacyFont = CreateNativeDynamicFont("A", fontSize);
        var managedFont = CreateManagedDynamicFont();

        try
        {
            using (_drawList.Begin(new Vector2(160f, 90f)))
            {
                NowSdf.Scene(
                        new NowRect(0f, 0f, 160f, 90f),
                        new NowId("sdf-legacy-distance-code-step"))
                    .Text(new Vector2(16f, 12f), "A", legacyFont, fontSize)
                    .Draw();
            }

            Vector4 legacyTextData =
                _drawList.batches[0].material.GetVectorArray("_SdfData2")[0];
            Assert.AreEqual(0f, legacyTextData.y, 0.0001f,
                "The native fixture must exercise legacy 8-bit distance encoding.");
            Assert.Greater(legacyTextData.x, 0f);
            Assert.AreEqual(
                legacyTextData.x / 255f,
                legacyTextData.z,
                0.0000001f,
                "Legacy/native glyph upload must expose one 8-bit distance-code step.");

            _drawList.Clear();

            using (_drawList.Begin(new Vector2(160f, 90f)))
            {
                NowSdf.Scene(
                        new NowRect(0f, 0f, 160f, 90f),
                        new NowId("sdf-managed-distance-code-step"))
                    .Text(new Vector2(16f, 12f), "A", managedFont, fontSize)
                    .Draw();
            }

            Vector4 managedTextData =
                _drawList.batches[0].material.GetVectorArray("_SdfData2")[0];
            Assert.AreEqual(1f, managedTextData.y, 0.0001f,
                "The managed fixture must exercise packed 16-bit distance encoding.");
            Assert.Greater(managedTextData.x, 0f);
            Assert.AreEqual(
                managedTextData.x / 65535f,
                managedTextData.z,
                0.0000001f,
                "Managed glyph upload must expose one packed 16-bit distance-code step.");
        }
        finally
        {
            DestroyManagedDynamicFont(managedFont);
            DestroyManagedDynamicFont(legacyFont);
        }
    }

    [Test]
    public void SdfFiniteContourPositiveOffsetSelectsAbsoluteReachRange()
    {
        const float fontSize = 80f;
        const float spacing = 10f;
        const float width = 2f;
        const float offset = 80f;
        const int bandCount = 1;
        const float contourReach = offset + (bandCount - 0.5f) * spacing + width * 0.5f;
        var font = CreateManagedDynamicFont();

        try
        {
            int basePixelRange = font.GetDynamicPixelRange(0f, fontSize);
            int contourPixelRange = font.GetDynamicPixelRange(
                contourReach / fontSize,
                fontSize);
            Assert.Greater(contourPixelRange, basePixelRange,
                "The positive-offset contour fixture must require an extended range.");

            var source = NowSdf.Graph()
                .Text(new Vector2(16f, 14f), "A", font, fontSize);
            float baseScreenRange = source.nodes[0].data2.x;
            var scene = NowSdf.Scene(
                    new NowRect(0f, 0f, 260f, 140f),
                    new NowId("sdf-positive-contour-absolute-reach"))
                .Graph(source)
                .SetContours(
                    spacing,
                    width,
                    Color.white,
                    offset,
                    bandCount);

            using (_drawList.Begin(new Vector2(260f, 140f)))
                scene.Draw();

            var material = _drawList.batches[0].material;
            Vector4 textData = material.GetVectorArray("_SdfData2")[0];
            Assert.Greater(textData.x, baseScreenRange,
                "A large positive contour offset must not collapse to the base field range.");
            Assert.AreEqual(
                font.GetScreenPixelRangeForPixelRange('A', fontSize, contourPixelRange),
                textData.x,
                0.0001f,
                "Finite contour reach must include the absolute authored offset.");
            Assert.AreEqual(1f, textData.y, 0.0001f);
            Assert.AreEqual(textData.x / 65535f, textData.z, 0.0000001f);
            Assert.AreEqual(
                new Vector4(spacing, width, offset, bandCount),
                material.GetVector("_SdfContour"));
        }
        finally
        {
            DestroyManagedDynamicFont(font);
        }
    }

    [Test]
    public void SdfPendingRotationRejectsGraphAndMorphOperands()
    {
        var from = NowSdf.Graph().Circle(new Vector2(16f, 16f), 8f);
        var to = NowSdf.Graph().Box(new NowRect(8f, 8f, 16f, 16f));
        var graphScene = NowSdf.Scene("sdf-rotate-graph-guard").RotateNext(90f);
        var morphScene = NowSdf.Scene("sdf-rotate-morph-guard").RotateNext(90f);

        Assert.Throws<System.InvalidOperationException>(() => graphScene.Graph(from));
        Assert.Throws<System.InvalidOperationException>(() => morphScene.Morph(from, to, 0.5f));

        var pushedGraphScene = NowSdf.Scene("sdf-pushed-rotation-graph-guard")
            .PushRotation(10f);
        var pushedMorphScene = NowSdf.Scene("sdf-pushed-rotation-morph-guard")
            .PushRotation(10f);
        var graphError = Assert.Throws<System.InvalidOperationException>(() =>
            pushedGraphScene.Graph(from));
        var morphError = Assert.Throws<System.InvalidOperationException>(() =>
            pushedMorphScene.Morph(from, to, 0.5f));
        StringAssert.Contains("layer or group transform", graphError.Message);
        StringAssert.Contains("layer or group transform", morphError.Message);
    }

    [Test]
    public void SdfSceneComposesReusableGraphs()
    {
        var a = NowSdf.Graph()
            .SetColor(Color.red)
            .Circle(new Vector2(42, 42), 34);
        var b = NowSdf.Graph()
            .SetColor(Color.blue)
            .RoundedBox(new NowRect(24, 18, 74, 48), 12);

        using (_drawList.Begin(new Vector2(120, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 120, 90))
                .Graph(a)
                .Subtract()
                .Graph(b)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(2f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void SdfScenePacksContiguousGraphRangesAndReusesThem()
    {
        var a = NowSdf.Graph()
            .Circle(new Vector2(24f, 24f), 16f)
            .Circle(new Vector2(46f, 24f), 12f);
        var b = NowSdf.Graph()
            .Box(new NowRect(10f, 12f, 22f, 18f))
            .Ellipse(new NowRect(34f, 10f, 24f, 22f))
            .Capsule(new NowRect(62f, 12f, 28f, 18f));

        using (_drawList.Begin(new Vector2(110f, 56f)))
        {
            NowSdf.Scene(new NowRect(0f, 0f, 110f, 56f))
                .Graph(a)
                .Union()
                .Graph(b)
                .Intersect()
                .Graph(a)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(5f, material.GetFloat("_SdfShapeCount"));
        Assert.AreEqual(3f, material.GetFloat("_SdfLayerCount"));

        var layer0 = material.GetVectorArray("_SdfLayerData0");
        var layer1 = material.GetVectorArray("_SdfLayerData1");
        Assert.AreEqual(0f, layer0[0].x, "The first graph keeps graph id zero.");
        Assert.AreEqual(1f, layer0[1].x, "The second graph keeps graph id one.");
        Assert.AreEqual(0f, layer0[2].x, "A repeated graph reuses its original id.");
        Assert.AreEqual(2f, layer1[0].z, "Graph A packs start 0 and count 2.");
        Assert.AreEqual(259f, layer1[1].z, "Graph B packs start 2 and count 3.");
        Assert.AreEqual(2f, layer1[2].z, "The repeated graph reuses its original range.");

        var shapeMeta = material.GetVectorArray("_SdfShapeMeta");
        Assert.AreEqual(0f, shapeMeta[0].x);
        Assert.AreEqual(0f, shapeMeta[1].x);
        Assert.AreEqual(1f, shapeMeta[2].x);
        Assert.AreEqual(1f, shapeMeta[3].x);
        Assert.AreEqual(1f, shapeMeta[4].x);
    }

    [Test]
    public void SdfScenePackedGraphRangesPreserveTruncationOrder()
    {
        var a = NowSdf.Graph();
        var b = NowSdf.Graph();
        var c = NowSdf.Graph();

        for (int i = 0; i < 63; ++i)
            a.Circle(new Vector2(i + 0.5f, 8f), 0.4f);

        for (int i = 0; i < 3; ++i)
            b.Circle(new Vector2(i + 0.5f, 16f), 0.4f);

        c.Circle(new Vector2(0.5f, 24f), 0.4f);

        using (_drawList.Begin(new Vector2(64f, 32f)))
        {
            NowSdf.Scene(new NowRect(0f, 0f, 64f, 32f))
                .Graph(a)
                .Union()
                .Graph(b)
                .Union()
                .Graph(c)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        var layerData = material.GetVectorArray("_SdfLayerData1");
        Assert.AreEqual((float)NowSdf.MaxShapes, material.GetFloat("_SdfShapeCount"));
        Assert.AreEqual(63f, layerData[0].z, "Graph A should retain its complete 63-shape range.");
        Assert.AreEqual(8065f, layerData[1].z, "Graph B should retain only its first uploaded shape.");
        Assert.AreEqual(8192f, layerData[2].z, "Graph C should retain an empty range at capacity.");
    }

    [Test]
    public void SdfSceneMorphUploadsBothGraphsAsOneLayer()
    {
        var from = NowSdf.Graph()
            .SetColor(Color.magenta)
            .Circle(new Vector2(45, 45), 36);
        var to = NowSdf.Graph()
            .SetColor(Color.yellow)
            .Capsule(new NowRect(16, 24, 92, 42));

        using (_drawList.Begin(new Vector2(128, 90)))
        {
            NowSdf.Scene(new NowRect(0, 0, 128, 90))
                .Morph(from, to, 0.5f)
                .Draw();
        }

        var material = _drawList.batches[0].material;
        Assert.AreEqual(1f, material.GetFloat("_SdfLayerCount"), 0.0001f);
        Assert.AreEqual(2f, material.GetFloat("_SdfShapeCount"), 0.0001f);
        var layerData = material.GetVectorArray("_SdfLayerData1");
        Assert.AreEqual(1f, layerData[0].z, "The source graph packs start 0 and count 1.");
        Assert.AreEqual(129f, layerData[0].w, "The target graph packs start 1 and count 1.");
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
    }

    [Test]
    public void BuiltInSdfMaterialDeclaresCurrentAbi()
    {
        var material = Resources.Load<Material>("NowUI/SdfMaterial");

        Assert.NotNull(material);
        Assert.AreEqual(2, NowSdf.MaterialAbiVersion,
            "ChamferedBox and Triangle require the material ABI-v2 decoder.");
        Assert.AreEqual(1, NowSdf.MinimumMaterialAbiVersion);
        Assert.IsTrue(material.HasProperty(NowSdf.MaterialAbiProperty));
        Assert.AreEqual(
            NowSdf.MaterialAbiVersion,
            material.GetFloat(NowSdf.MaterialAbiProperty),
            0.0001f);
        AssertShaderHasNoErrors(material.shader);
    }

    [TestCase("Aurora", "NowUI/SDF Examples/Aurora")]
    [TestCase("Topographic", "NowUI/SDF Examples/Topographic")]
    [TestCase("PaperCutout", "NowUI/SDF Examples/Paper Cutout")]
    public void SdfExampleMaterialsAreLoadableAndDeclareCurrentAbi(
        string materialName,
        string shaderName)
    {
        var material = Resources.Load<Material>($"NowUI/SdfExamples/{materialName}");

        Assert.NotNull(material, $"The {materialName} SDF example material was not found.");
        Assert.NotNull(material.shader);
        Assert.AreEqual(shaderName, material.shader.name);
        Assert.IsTrue(material.HasProperty(NowSdf.MaterialAbiProperty));
        Assert.AreEqual(
            NowSdf.MaterialAbiVersion,
            material.GetFloat(NowSdf.MaterialAbiProperty),
            0.0001f);
        AssertShaderHasNoErrors(material.shader);
    }

    static void AssertShaderHasNoErrors(Shader shader)
    {
        if (!ShaderUtil.ShaderHasError(shader))
            return;

        var messages = ShaderUtil.GetShaderMessages(shader);
        var details = System.Array.ConvertAll(
            messages,
            message => $"{message.severity}: {message.message} ({message.file}:{message.line})");
        Assert.Fail($"Shader '{shader.name}' has compiler errors:\n{string.Join("\n", details)}");
    }

    [Test]
    public void SdfSceneRejectsMaterialsWithoutASupportedAbi()
    {
        var stock = Resources.Load<Material>("NowUI/SdfMaterial");
        var uiShader = Shader.Find("UI/Default");
        Assert.NotNull(stock);
        Assert.NotNull(uiShader);

        var missingAbi = new Material(uiShader);
        var wrongAbi = new Material(stock);
        wrongAbi.SetFloat(NowSdf.MaterialAbiProperty, NowSdf.MaterialAbiVersion + 1);
        var fractionalAbi = new Material(stock);
        fractionalAbi.SetFloat(NowSdf.MaterialAbiProperty, 1.5f);

        try
        {
            Assert.Throws<System.ArgumentException>(() =>
                NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "missing-sdf-abi")
                    .SetMaterial(missingAbi));
            Assert.Throws<System.ArgumentException>(() =>
                NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "wrong-sdf-abi")
                    .SetMaterial(wrongAbi));
            Assert.Throws<System.ArgumentException>(() =>
                NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "fractional-sdf-abi")
                    .SetMaterial(fractionalAbi));

            Assert.AreEqual(0, _drawList.batchCount);
            Assert.AreEqual(0, NowSdf.maskTextureCount);
            Assert.AreEqual(0, NowSdf.maskRasterizationCount);
        }
        finally
        {
            Object.DestroyImmediate(missingAbi);
            Object.DestroyImmediate(wrongAbi);
            Object.DestroyImmediate(fractionalAbi);
        }
    }

    [Test]
    public void SdfAbiOneMaterialsAcceptIdentityButRejectRotatedLegacyAndV2Shapes()
    {
        var legacyShader = Shader.Find("Hidden/NowUI Tests/SDF ABI V1");
        Assert.NotNull(legacyShader);
        AssertShaderHasNoErrors(legacyShader);
        var legacy = new Material(legacyShader);

        try
        {
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                NowSdf.Scene(new NowRect(0f, 0f, 64f, 64f), "legacy-sdf-circle")
                    .SetMaterial(legacy)
                    .Circle(new Vector2(32f, 32f), 18f)
                    .Line(new Vector2(10f, 52f), new Vector2(54f, 52f), 4f)
                    .Draw();
            }

            Assert.AreEqual(1, _drawList.batchCount,
                "ABI-v1 materials should keep rendering the released primitive set.");

            _drawList.Clear();
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                NowSdf.Scene(new NowRect(0f, 0f, 64f, 64f), "legacy-sdf-full-turn")
                    .SetMaterial(legacy)
                    .RotateNext(360f)
                    .Box(new NowRect(12f, 18f, 40f, 28f))
                    .Draw();
            }

            Assert.AreEqual(1, _drawList.batchCount,
                "A canonical full turn must remain ABI-v1-compatible identity metadata.");

            _drawList.Clear();
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                var incompatibleRotation = NowSdf.Scene(
                        new NowRect(0f, 0f, 64f, 64f),
                        "legacy-sdf-rotation")
                    .SetMaterial(legacy)
                    .RotateNext(90f)
                    .Box(new NowRect(12f, 18f, 40f, 28f));
                Assert.Throws<System.InvalidOperationException>(() => incompatibleRotation.Draw());
            }

            Assert.AreEqual(0, _drawList.batchCount,
                "ABI-v1 materials must reject rotation metadata on legacy opcodes.");

            _drawList.Clear();
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                var incompatible = NowSdf.Scene(
                        new NowRect(0f, 0f, 64f, 64f),
                        "legacy-sdf-planar")
                    .SetMaterial(legacy)
                    .ChamferedBox(new NowRect(8f, 8f, 48f, 48f), 8f);
                Assert.Throws<System.InvalidOperationException>(() => incompatible.Draw());
            }

            Assert.AreEqual(0, _drawList.batchCount);
            Assert.AreEqual(0, NowSdf.maskTextureCount);

            _drawList.Clear();
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                var incompatibleMask = NowSdf.Scene(
                        new NowRect(0f, 0f, 64f, 64f),
                        "legacy-sdf-mask")
                    .SetMaterial(legacy)
                    .Triangle(
                        new Vector2(12f, 52f),
                        new Vector2(52f, 52f),
                        new Vector2(32f, 10f));
                Assert.Throws<System.InvalidOperationException>(() =>
                    incompatibleMask.BeginMask().Dispose());
            }

            Assert.AreEqual(0, _drawList.batchCount);
            Assert.AreEqual(0, NowSdf.maskTextureCount,
                "ABI rejection must happen before allocating a mask target.");

            _drawList.Clear();
            var from = NowSdf.Graph().Circle(new Vector2(32f, 32f), 16f);
            var to = NowSdf.Graph().Triangle(
                new Vector2(12f, 52f),
                new Vector2(52f, 52f),
                new Vector2(32f, 10f));
            using (_drawList.Begin(new Vector2(64f, 64f)))
            {
                var incompatibleMorph = NowSdf.Scene(
                        new NowRect(0f, 0f, 64f, 64f),
                        "legacy-sdf-morph")
                    .SetMaterial(legacy)
                    .Morph(from, to, 0.5f);
                Assert.Throws<System.InvalidOperationException>(() => incompatibleMorph.Draw());
            }

            Assert.AreEqual(0, _drawList.batchCount);
            Assert.AreEqual(0, NowSdf.maskTextureCount);
        }
        finally
        {
            Object.DestroyImmediate(legacy);
        }
    }

    [Test]
    public void SdfSceneOwnsAndSynchronizesCustomMaterialClones()
    {
        var stock = Resources.Load<Material>("NowUI/SdfMaterial");
        Assert.NotNull(stock);

        var template = new Material(stock);
        template.SetFloat("_ColorMask", 7f);
        template.SetFloat("_SdfShapeCount", 23f);
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("custom-sdf-material");
        NowResolvedId resolvedId = NowResolvedId.None;

        Material Draw(bool? syncPerFrame)
        {
            using (_drawList.Begin(rect.size))
            {
                resolvedId = NowControls.GetControlId(id);
                var scene = NowSdf.Scene(rect, id);
                scene = syncPerFrame.HasValue
                    ? scene.SetMaterial(template, syncPerFrame.Value)
                    : scene.SetMaterial(template);
                scene.Circle(rect.center, 12f).Draw();
            }

            return _drawList.batches[0].material;
        }

        try
        {
            var clone = Draw(null);
            Assert.AreNotSame(template, clone);
            Assert.AreEqual(7f, clone.GetFloat("_ColorMask"));
            Assert.AreEqual(1f, clone.GetFloat("_SdfShapeCount"));
            Assert.AreEqual(0f, clone.GetFloat("_SdfMaskOutput"));
            Assert.AreEqual(23f, template.GetFloat("_SdfShapeCount"), "The caller's template was mutated.");

            template.SetFloat("_ColorMask", 3f);
            var staticClone = Draw(false);
            Assert.AreSame(clone, staticClone);
            Assert.AreEqual(7f, staticClone.GetFloat("_ColorMask"), "A static template was recopied.");

            var synchronizedClone = Draw(true);
            Assert.AreSame(clone, synchronizedClone);
            Assert.AreEqual(3f, synchronizedClone.GetFloat("_ColorMask"));
            Assert.AreEqual(1f, synchronizedClone.GetFloat("_SdfShapeCount"), "ABI arrays were not restored after property synchronization.");
            Assert.AreEqual(
                1f,
                synchronizedClone.GetVectorArray("_SdfLayerData1")[0].z,
                "Packed graph ranges were not restored after property synchronization.");
            Assert.AreEqual(23f, template.GetFloat("_SdfShapeCount"), "Synchronization mutated the template.");

            using (_drawList.Begin(rect.size))
            {
                NowSdf.Scene(rect, id)
                    .SetMaterial(null)
                    .Circle(rect.center, 12f)
                    .Draw();
            }

            Assert.IsTrue(clone, "Switching templates destroyed a clone still usable by queued batches.");
            Assert.IsTrue(template, "Switching materials destroyed the caller's template.");
            Assert.AreNotSame(template, _drawList.batches[0].material);

            Assert.IsTrue(NowSdf.Release(resolvedId));
            Assert.IsFalse(clone, "Releasing the scene cache did not destroy its owned clone.");
            Assert.IsTrue(template, "Releasing the scene cache destroyed the caller's template.");
        }
        finally
        {
            Object.DestroyImmediate(template);
        }
    }

    [Test]
    public void SdfSceneRetainsMaterialClonesReferencedByQueuedBatches()
    {
        var stock = Resources.Load<Material>("NowUI/SdfMaterial");
        Assert.NotNull(stock);

        var templateA = new Material(stock);
        var templateB = new Material(stock);
        templateA.SetFloat("_ColorMask", 7f);
        templateB.SetFloat("_ColorMask", 3f);
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("queued-sdf-material-switch");
        NowResolvedId resolvedId = NowResolvedId.None;

        try
        {
            using (_drawList.Begin(rect.size))
            {
                resolvedId = NowControls.GetControlId(id);
                var scene = NowSdf.Scene(rect, id)
                    .SetMaterial(templateA)
                    .Circle(rect.center, 12f);

                scene.Draw();
                scene.SetMaterial(templateB).Draw();
                scene.SetMaterial(templateA).Draw();
            }

            Assert.AreEqual(3, _drawList.batchCount);
            var first = _drawList.batches[0].material;
            var second = _drawList.batches[1].material;
            var reused = _drawList.batches[2].material;
            Assert.IsTrue(first);
            Assert.IsTrue(second);
            Assert.AreNotSame(first, second);
            Assert.AreSame(first, reused, "Returning to a template did not reuse its retained clone.");
            Assert.AreEqual(7f, first.GetFloat("_ColorMask"));
            Assert.AreEqual(3f, second.GetFloat("_ColorMask"));

            Assert.IsTrue(NowSdf.Release(resolvedId));
            Assert.IsFalse(first);
            Assert.IsFalse(second);
            Assert.IsTrue(templateA);
            Assert.IsTrue(templateB);
        }
        finally
        {
            Object.DestroyImmediate(templateA);
            Object.DestroyImmediate(templateB);
        }
    }

    [Test]
    public void SdfCustomMaterialSynchronizationControlsMaskReuse()
    {
        var stock = Resources.Load<Material>("NowUI/SdfMaterial");
        Assert.NotNull(stock);

        var template = new Material(stock);
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("custom-sdf-mask-material");

        void Capture(bool syncPerFrame)
        {
            using (_drawList.Begin(rect.size))
            using (NowSdf.Scene(rect, id)
                .SetMaterial(template, syncPerFrame)
                .Circle(rect.center, 12f)
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }
        }

        try
        {
            Capture(false);
            Capture(false);
            Assert.AreEqual(1, NowSdf.maskRasterizationCount, "An immutable custom template disabled mask reuse.");

            Capture(true);
            Capture(true);
            Assert.AreEqual(3, NowSdf.maskRasterizationCount, "A synchronized custom template reused stale mask coverage.");

            NowSdf.Reset();
            Assert.IsTrue(template, "Reset destroyed the caller's custom material template.");
        }
        finally
        {
            Object.DestroyImmediate(template);
        }
    }

    [Test]
    public void SdfMaskCapturesLinearRedCoverageTextureInChildBatch()
    {
        var rect = new NowRect(12f, 18f, 64f, 32f);

        using (_drawList.Begin(new Vector2(128f, 96f)))
        using (NowSdf.Scene(rect, "sdf-mask-state")
            .Circle(new Vector2(32f, 16f), 14f)
            .BeginMask())
        {
            Now.Rectangle(rect)
                .SetColor(Color.white)
                .Draw();
        }

        Assert.AreEqual(1, _drawList.batchCount);
        var state = _drawList.batches[0].maskState;
        Assert.AreEqual(0, state.count);
        Assert.AreEqual(1, state.textureCount);

        var descriptor = state.GetTexture(0);
        var target = descriptor.texture as RenderTexture;
        Assert.NotNull(target);
        Assert.AreEqual(64, target.width);
        Assert.AreEqual(32, target.height);
        Assert.IsFalse(target.sRGB, "SDF mask coverage must be sampled in linear space.");
        Assert.AreEqual(FilterMode.Bilinear, target.filterMode);
        Assert.AreEqual(TextureWrapMode.Clamp, target.wrapMode);
        Assert.IsFalse(target.useMipMap);
        Assert.IsTrue(
            target.format == RenderTextureFormat.R8 || target.format == RenderTextureFormat.ARGB32,
            $"Unexpected SDF mask target format: {target.format}.");
        Assert.AreEqual(new Vector4(rect.x, rect.y, rect.width, rect.height), descriptor.rect);
        Assert.AreEqual((float)NowMaskTextureChannel.Red, descriptor.parameters.x);
        Assert.AreEqual(0f, descriptor.parameters.y);
        Assert.AreEqual(1f, descriptor.parameters.z);
        Assert.AreEqual(new Vector4(0f, 0f, 1f, 1f), descriptor.transform);
    }

    [TestCase(0.001f, 1, 1)]
    [TestCase(0.5f, 33, 17)]
    [TestCase(2f, 130, 66)]
    public void SdfMaskResolutionScaleControlsCachedCoverageTexture(
        float resolutionScale,
        int expectedWidth,
        int expectedHeight)
    {
        var rect = new NowRect(0f, 0f, 65f, 33f);

        using (_drawList.Begin(new Vector2(96f, 64f)))
        using (NowSdf.Scene(rect, "sdf-mask-scaled-resolution")
            .SetMaskResolutionScale(resolutionScale)
            .Circle(rect.center, 14f)
            .BeginMask())
        {
            Now.Rectangle(rect)
                .SetColor(Color.white)
                .Draw();
        }

        var target = _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        Assert.NotNull(target);
        Assert.AreEqual(expectedWidth, target.width);
        Assert.AreEqual(expectedHeight, target.height);
        Assert.AreEqual((long)expectedWidth * expectedHeight, NowSdf.cachedMaskPixels);

        var descriptor = _drawList.batches[0].maskState.GetTexture(0);
        Assert.AreEqual(new Vector4(rect.x, rect.y, rect.width, rect.height), descriptor.rect);
    }

    [Test]
    public void SdfMaskResolutionScaleMustBePositiveAndFinite()
    {
        var rect = new NowRect(0f, 0f, 64f, 32f);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            NowSdf.Scene(rect, "sdf-mask-zero-resolution")
                .SetMaskResolutionScale(0f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            NowSdf.Scene(rect, "sdf-mask-negative-resolution")
                .SetMaskResolutionScale(-0.5f));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            NowSdf.Scene(rect, "sdf-mask-nan-resolution")
                .SetMaskResolutionScale(float.NaN));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            NowSdf.Scene(rect, "sdf-mask-infinite-resolution")
                .SetMaskResolutionScale(float.PositiveInfinity));
    }

    [Test]
    public void SdfMaskResolutionScaleComposesWithTransformAndInvalidatesOnResize()
    {
        var rect = new NowRect(0f, 0f, 31f, 21f);
        var id = new NowId("sdf-mask-transformed-resolution");

        RenderTexture Capture(float? resolutionScale)
        {
            using (_drawList.Begin(new Vector2(160f, 120f)))
            using (Now.Transform(new Vector2(-2f, 3f), new Vector2(90f, 7f)))
            {
                var mask = NowSdf.Scene(rect, id)
                    .Circle(rect.center, 9f);

                if (resolutionScale.HasValue)
                    mask = mask.SetMaskResolutionScale(resolutionScale.Value);

                using (mask.BeginMask())
                {
                    Now.Rectangle(rect)
                        .SetColor(Color.white)
                        .Draw();
                }
            }

            return _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        }

        var half = Capture(0.5f);
        Assert.AreEqual(31, half.width);
        Assert.AreEqual(32, half.height);

        var reused = Capture(0.5f);
        Assert.AreSame(half, reused);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount);

        var quarter = Capture(0.25f);
        Assert.AreNotSame(reused, quarter);
        Assert.AreEqual(16, quarter.width);
        Assert.AreEqual(16, quarter.height);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount);

        var defaultScale = Capture(null);
        Assert.AreNotSame(quarter, defaultScale);
        Assert.AreEqual(62, defaultScale.width);
        Assert.AreEqual(63, defaultScale.height);
        Assert.AreEqual(3, NowSdf.maskRasterizationCount);
        Assert.AreEqual(
            new Vector4(90f, 7f, -2f, 3f),
            _drawList.batches[0].maskState.GetTexture(0).transform);
    }

    [Test]
    public void MaskResolutionScaleDoesNotTextureBackDirectSdfDraw()
    {
        var rect = new NowRect(0f, 0f, 64f, 32f);

        using (_drawList.Begin(new Vector2(96f, 64f)))
        {
            NowSdf.Scene(rect, "direct-sdf-resolution-scale")
                .SetMaskResolutionScale(0.25f)
                .Circle(rect.center, 14f)
                .Draw();
        }

        Assert.AreEqual(0, NowSdf.maskTextureCount);
        Assert.AreEqual(0, NowSdf.cachedMaskPixels);
        Assert.AreEqual(NowMeshKind.Sdf, _drawList.batches[0].kind);
    }

    [Test]
    public void SdfMaskReusesStableTargetAndResizesForCapturedTransform()
    {
        var rect = new NowRect(0f, 0f, 30f, 20f);
        var id = new NowId("sdf-mask-reuse");

        RenderTexture Capture(Vector2 scale)
        {
            using (_drawList.Begin(new Vector2(160f, 120f)))
            using (Now.Transform(scale, new Vector2(100f, 10f)))
            using (NowSdf.Scene(rect, id)
                .RoundedBox(new NowRect(2f, 2f, 26f, 16f), 6f)
                .BeginMask())
            {
                Now.Rectangle(rect)
                    .SetColor(Color.white)
                    .Draw();
            }

            return _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        }

        var first = Capture(Vector2.one);
        var reused = Capture(Vector2.one);

        Assert.NotNull(first);
        Assert.AreSame(first, reused, "A stable mask id and transformed size should reuse its coverage target.");
        Assert.AreEqual(1, NowSdf.maskRasterizationCount, "An identical stable mask was rasterized again.");
        Assert.AreEqual(30, reused.width);
        Assert.AreEqual(20, reused.height);

        var resized = Capture(new Vector2(-2f, 3f));

        Assert.NotNull(resized);
        Assert.AreNotSame(reused, resized);
        Assert.AreEqual(60, resized.width);
        Assert.AreEqual(60, resized.height);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount, "A physical target resize did not rerasterize coverage.");

        var descriptor = _drawList.batches[0].maskState.GetTexture(0);
        Assert.AreEqual(new Vector4(100f, 10f, -2f, 3f), descriptor.transform);
    }

    [Test]
    public void StableSdfMaskReusesCoverageAcrossTranslationAndMirroring()
    {
        var id = new NowId("sdf-mask-position-independent-reuse");

        RenderTexture Capture(NowRect rect, Vector2 scale, Vector2 origin)
        {
            using (_drawList.Begin(new Vector2(160f, 120f)))
            using (Now.Transform(scale, origin))
            using (NowSdf.Scene(rect, id)
                .Circle(new Vector2(15f, 10f), 8f)
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }

            return _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        }

        var first = Capture(new NowRect(10f, 12f, 30f, 20f), Vector2.one, Vector2.zero);
        var mirrored = Capture(
            new NowRect(50f, 20f, 30f, 20f),
            new Vector2(-1f, 1f),
            new Vector2(100f, 0f));

        Assert.AreSame(first, mirrored);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount);
        Assert.AreEqual(
            new Vector4(100f, 0f, -1f, 1f),
            _drawList.batches[0].maskState.GetTexture(0).transform);
    }

    [Test]
    public void SdfMaskRerasterizesForShapeTintAndLocalMaskChanges()
    {
        var rect = new NowRect(8f, 12f, 64f, 40f);
        var id = new NowId("sdf-mask-signature-inputs");

        void Capture(float radius, float alpha, NowRect mask)
        {
            using (_drawList.Begin(new Vector2(96f, 72f)))
            using (NowSdf.Scene(rect, id)
                .SetTint(new Vector4(1f, 1f, 1f, alpha))
                .SetMask(mask)
                .Circle(new Vector2(32f, 20f), radius)
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }
        }

        Capture(14f, 1f, rect);
        Capture(14f, 1f, rect);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount);

        Capture(15f, 1f, rect);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount, "Shape content was omitted from the coverage signature.");

        Capture(15f, 0.5f, rect);
        Assert.AreEqual(3, NowSdf.maskRasterizationCount, "Effective tint was omitted from the coverage signature.");

        Capture(15f, 0.5f, new NowRect(rect.x + 4f, rect.y, rect.width - 4f, rect.height));
        Assert.AreEqual(4, NowSdf.maskRasterizationCount, "The local capture mask was omitted from the coverage signature.");
    }

    [Test]
    public void SdfMaskCanonicalizesRotateNextAngleInItsCoverageSignature()
    {
        var rect = new NowRect(0f, 0f, 64f, 64f);
        var id = new NowId("sdf-mask-generic-rotation-angle");

        void Capture(float angleDegrees)
        {
            using (_drawList.Begin(rect.size))
            using (NowSdf.Scene(rect, id)
                .RotateNext(angleDegrees)
                .Box(new NowRect(12f, 22f, 40f, 20f))
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }
        }

        Capture(0f);
        Capture(360f);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount,
            "Equivalent full-turn angles produced different mask signatures.");

        Capture(30f);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount,
            "Changing node rotation did not rerasterize its mask.");

        Capture(390f);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount,
            "Wrapped equivalent angles produced different mask signatures.");
    }

    [Test]
    public void SdfMaskSignatureIncludesAmbientColorMultiplier()
    {
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("sdf-mask-ambient-tint");

        void Capture(float alpha)
        {
            Now.BeginColorMultiplier(new Color(1f, 1f, 1f, alpha));

            try
            {
                using (_drawList.Begin(new Vector2(64f, 48f)))
                using (NowSdf.Scene(rect, id)
                    .Circle(rect.center, 12f)
                    .BeginMask())
                {
                    Now.Rectangle(rect).SetColor(Color.white).Draw();
                }
            }
            finally
            {
                Now.EndColorMultiplier();
            }
        }

        Capture(1f);
        Capture(1f);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount);

        Capture(0.5f);
        Assert.AreEqual(2, NowSdf.maskRasterizationCount);
    }

    [Test]
    public void SdfMaskRerasterizesWhenSourceTextureIsApplied()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var rect = new NowRect(0f, 0f, 40f, 32f);
        var id = new NowId("sdf-mask-source-texture-version");

        try
        {
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; ++i)
                pixels[i] = new Color32(255, 255, 255, 255);

            texture.SetPixels32(pixels);
            texture.Apply();

            void Capture()
            {
                using (_drawList.Begin(new Vector2(64f, 48f)))
                using (NowSdf.Scene(rect, id)
                    .SetTexture(texture)
                    .Box(rect)
                    .BeginMask())
                {
                    Now.Rectangle(rect).SetColor(Color.white).Draw();
                }
            }

            Capture();
            Capture();
            Assert.AreEqual(1, NowSdf.maskRasterizationCount);

            uint previousUpdateCount = texture.updateCount;
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            Assert.Greater(texture.updateCount, previousUpdateCount);

            Capture();
            Assert.AreEqual(2, NowSdf.maskRasterizationCount);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void AnimatedWarpRerasterizesWhileStaticWarpReusesCoverage()
    {
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("sdf-mask-animated-warp");

        void Capture(float speed)
        {
            using (_drawList.Begin(new Vector2(64f, 48f)))
            using (NowSdf.Scene(rect, id)
                .SetWarp(3f, 20f, speed, 7f)
                .Circle(rect.center, 12f)
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }
        }

        Capture(0f);
        Capture(0f);
        Assert.AreEqual(1, NowSdf.maskRasterizationCount);

        Capture(1f);
        Capture(1f);
        Assert.AreEqual(3, NowSdf.maskRasterizationCount);
    }

    [Test]
    public void LostSdfMaskTargetIsRecreatedAndRerasterized()
    {
        var rect = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("sdf-mask-lost-target");

        RenderTexture Capture()
        {
            using (_drawList.Begin(new Vector2(64f, 48f)))
            using (NowSdf.Scene(rect, id)
                .Ellipse(rect)
                .BeginMask())
            {
                Now.Rectangle(rect).SetColor(Color.white).Draw();
            }

            return _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        }

        var target = Capture();
        Assert.IsTrue(target.IsCreated());
        target.Release();
        Assert.IsFalse(target.IsCreated());

        var recreated = Capture();

        Assert.AreSame(target, recreated);
        Assert.IsTrue(recreated.IsCreated());
        Assert.AreEqual(2, NowSdf.maskRasterizationCount);
    }

    [Test]
    public void TwoSdfMasksAreIndependentOfAnalyticAndHardMaskLimits()
    {
        var surface = new NowRect(0f, 0f, 100f, 100f);

        using (_drawList.Begin(surface.size))
        using (Now.Mask(surface))
        using (Now.Mask(NowMaskShape.Circle(surface.center, 48f)))
        {
            var first = NowSdf.Scene(surface, "sdf-mask-capacity-first")
                .Circle(surface.center, 46f)
                .BeginMask();

            try
            {
                var second = NowSdf.Scene(surface, "sdf-mask-capacity-second")
                    .RoundedBox(new NowRect(4f, 4f, 92f, 92f), 12f)
                    .BeginMask();

                try
                {
                    Assert.Throws<System.InvalidOperationException>(() =>
                    {
                        var overflow = NowSdf.Scene(surface, "sdf-mask-capacity-overflow")
                            .Box(surface)
                            .BeginMask();
                        overflow.Dispose();
                    });

                    Now.Rectangle(new NowRect(45f, 45f, 4f, 4f)).SetColor(Color.red).Draw();
                }
                finally
                {
                    second.Dispose();
                }

                Now.Rectangle(new NowRect(50f, 45f, 4f, 4f)).SetColor(Color.green).Draw();
            }
            finally
            {
                first.Dispose();
            }

            Now.Rectangle(new NowRect(55f, 45f, 4f, 4f)).SetColor(Color.blue).Draw();
        }

        Assert.AreEqual(3, _drawList.batchCount);
        Assert.AreEqual(1, _drawList.batches[0].maskState.count);
        Assert.AreEqual(2, _drawList.batches[0].maskState.textureCount);
        Assert.AreEqual(1, _drawList.batches[1].maskState.count);
        Assert.AreEqual(1, _drawList.batches[1].maskState.textureCount);
        Assert.AreEqual(1, _drawList.batches[2].maskState.count);
        Assert.AreEqual(0, _drawList.batches[2].maskState.textureCount);
    }

    [Test]
    public void SdfMaskInputUsesConservativeSceneRect()
    {
        var surface = new NowRect(10f, 20f, 100f, 80f);

        using (_drawList.Begin(new Vector2(160f, 140f)))
        using (NowSdf.Scene(surface, "sdf-mask-input-bounds")
            .Circle(new Vector2(50f, 40f), 10f)
            .BeginMask())
        {
            Assert.IsTrue(
                Now.IsInsideAmbientMask(new Vector2(14f, 24f)),
                "A point inside the scene rect remains eligible even when its SDF coverage is transparent.");
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(9f, 24f)));
            Assert.IsFalse(Now.IsInsideAmbientMask(new Vector2(14f, 101f)));
        }

        Assert.IsTrue(Now.IsInsideAmbientMask(new Vector2(9f, 24f)), "Disposing the SDF mask did not restore input bounds.");
    }

    [Test]
    public void EmptySdfMaskAllocatesNoTextureAndCullsAllInput()
    {
        var surface = new NowRect(10f, 20f, 60f, 40f);

        using (_drawList.Begin(new Vector2(100f, 80f)))
        using (NowSdf.Scene(surface, "empty-sdf-mask").BeginMask())
        {
            Assert.IsFalse(Now.IsInsideAmbientMask(surface.center));

            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        Assert.AreEqual(1, _drawList.batchCount);
        var state = _drawList.batches[0].maskState;
        Assert.AreEqual(1, state.textureCount);
        var descriptor = state.GetTexture(0);
        Assert.IsNull(descriptor.texture);
        Assert.AreEqual(0f, descriptor.parameters.z, "A missing coverage texture must use the shader's cull-all path.");
    }

    [Test]
    public void ParameterlessBeginMaskRequiresExplicitSceneRect()
    {
        var exception = Assert.Throws<System.InvalidOperationException>(() =>
        {
            var scope = NowSdf.Scene("rectless-sdf-mask")
                .Circle(new Vector2(20f, 20f), 16f)
                .BeginMask();
            scope.Dispose();
        });

        StringAssert.Contains("explicit scene rect", exception.Message);
        StringAssert.Contains("BeginMask(rect)", exception.Message);
    }

    [Test]
    public void BeginMaskIsNoOpDuringMeasuredLayoutPass()
    {
        var surface = new NowRect(0f, 0f, 80f, 48f);
        int measureMaskCount = -1;
        int drawMaskCount = -1;

        using (_drawList.Begin(new Vector2(100f, 64f)))
        {
            NowLayout.RunMeasured(surface, () =>
            {
                using (NowSdf.Scene(surface, "measured-sdf-mask")
                    .Circle(surface.center, 20f)
                    .BeginMask())
                {
                    if (NowLayout.isMeasurePass)
                    {
                        measureMaskCount = Now.ambientMaskCount;
                        return;
                    }

                    drawMaskCount = Now.ambientMaskCount;
                    Now.Rectangle(surface).SetColor(Color.white).Draw();
                }
            });
        }

        Assert.AreEqual(0, measureMaskCount, "The measure pass installed a real ambient texture mask.");
        Assert.AreEqual(1, drawMaskCount, "The draw pass did not install the SDF mask.");
        Assert.AreEqual(1, _drawList.batches[0].maskState.textureCount);
        Assert.NotNull(_drawList.batches[0].maskState.GetTexture(0).texture);
    }

    [Test]
    public void ResetReleasesSdfMaskCoverageTexture()
    {
        var surface = new NowRect(0f, 0f, 48f, 32f);

        using (_drawList.Begin(surface.size))
        using (NowSdf.Scene(surface, "sdf-mask-reset")
            .Ellipse(surface)
            .BeginMask())
        {
            Now.Rectangle(surface).SetColor(Color.white).Draw();
        }

        var target = _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;
        Assert.IsTrue(target);

        NowSdf.Reset();

        Assert.IsFalse(target, "NowSdf.Reset() did not destroy its cache-owned mask target.");
    }

    [Test]
    public void ReleaseDropsOneExplicitScopedSdfCache()
    {
        var surface = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("released-sdf-mask");
        NowResolvedId resolvedId = NowResolvedId.None;
        RenderTexture target;

        using (_drawList.Begin(surface.size))
        using (NowControls.IdScope("release-owner"))
        using (NowSdf.Scene(surface, id)
            .Ellipse(surface)
            .BeginMask())
        {
            resolvedId = NowControls.GetControlId(id);
            Now.Rectangle(surface).SetColor(Color.white).Draw();
        }

        target = _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;

        Assert.IsTrue(target);
        Assert.AreEqual(1, NowSdf.cacheCount);
        Assert.AreEqual(1, NowSdf.maskTextureCount);
        Assert.AreEqual((long)target.width * target.height, NowSdf.cachedMaskPixels);

        Assert.IsTrue(NowSdf.Release(resolvedId));
        Assert.IsFalse(NowSdf.Release(resolvedId));

        Assert.AreEqual(0, NowSdf.cacheCount);
        Assert.AreEqual(0, NowSdf.maskTextureCount);
        Assert.AreEqual(0, NowSdf.cachedMaskPixels);
        Assert.IsFalse(target, "Releasing an explicit SDF cache did not destroy its mask target.");
        Assert.Throws<System.ArgumentException>(() => NowSdf.Release(default(NowId)));
    }

    [Test]
    public void ReleaseInvalidatesOutstandingBuilderWithoutOrphaningResources()
    {
        var surface = new NowRect(0f, 0f, 48f, 32f);
        var id = new NowId("released-builder");
        NowSdfBuilder builder;

        using (NowControls.IdScope("release-builder-owner"))
        {
            builder = NowSdf.Scene(surface, id)
                .Ellipse(surface);

            Assert.AreEqual(1, NowSdf.cacheCount);
            Assert.IsTrue(NowSdf.Release(id));
        }

        Assert.AreEqual(0, NowSdf.cacheCount);
        Assert.AreEqual(0, NowSdf.maskTextureCount);
        Assert.AreEqual(0, NowSdf.cachedMaskPixels);

        Assert.Throws<System.ObjectDisposedException>(() => builder.Measure());
        Assert.Throws<System.ObjectDisposedException>(() => builder.Draw(surface));
        Assert.Throws<System.ObjectDisposedException>(() =>
        {
            var scope = builder.BeginMask();
            scope.Dispose();
        });

        Assert.AreEqual(0, NowSdf.cacheCount);
        Assert.AreEqual(0, NowSdf.maskTextureCount);
        Assert.AreEqual(0, NowSdf.cachedMaskPixels);
    }

    static NowFont CreateManagedDynamicFont()
    {
        var source = AssetDatabase.LoadAssetAtPath<NowFont>(DynamicFontAssetPath);
        byte[] fontBytes;

        if (source != null && source.TryGetSourceBytes(out fontBytes))
        {
            Assert.IsNotEmpty(fontBytes);
        }
        else
        {
            Assert.IsTrue(File.Exists(RawDynamicFontPath),
                $"Test font source not found at {DynamicFontAssetPath} or {RawDynamicFontPath}");
            fontBytes = File.ReadAllBytes(RawDynamicFontPath);
        }

        NowFontCompiler.forceNativeCompiler = false;
        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(
            NowFontCompiler.TryCompile(fontBytes, out NowFont font, out string error),
            error);
        return font;
    }

    static NowFont CreateNativeDynamicFont(string characters, float fontSize)
    {
        Assert.IsTrue(File.Exists(RawDynamicFontPath),
            $"Native test font source not found at {RawDynamicFontPath}");
        byte[] fontBytes = File.ReadAllBytes(RawDynamicFontPath);
        NowFontCompiler.forceManagedCompiler = false;
        NowFontCompiler.forceNativeCompiler = true;
        Assert.IsTrue(
            NowFontCompiler.TryCompile(fontBytes, out NowFont font, out string error),
            error);
        font.EnsureGlyphs(characters, fontSize);
        return font;
    }

    static void DestroyManagedDynamicFont(NowFont font)
    {
        if (font == null)
            return;

        font.ClearDynamicCache();
        Object.DestroyImmediate(font);
    }

    static Vector2 TextGlyphBoundsCenter(NowSdfGraph graph, int glyphCount)
    {
        Assert.Greater(glyphCount, 0);
        Assert.GreaterOrEqual(graph.nodes.Count, glyphCount);
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        for (int i = 0; i < glyphCount; ++i)
        {
            NowSdfNode node = graph.nodes[i];
            Assert.AreEqual(NowSdfShapeType.Glyph, node.type);
            double halfWidth = System.Math.Max((double)node.data1.z * 0.5d, 0.0001d);
            double halfHeight = System.Math.Max((double)node.data1.w * 0.5d, 0.0001d);
            minX = System.Math.Min(minX, (double)node.data1.x - halfWidth);
            minY = System.Math.Min(minY, (double)node.data1.y - halfHeight);
            maxX = System.Math.Max(maxX, (double)node.data1.x + halfWidth);
            maxY = System.Math.Max(maxY, (double)node.data1.y + halfHeight);
        }

        return new Vector2(
            (float)(minX * 0.5d + maxX * 0.5d),
            (float)(minY * 0.5d + maxY * 0.5d));
    }

    static void AssertRotationRange(
        NowSdfGraph graph,
        int start,
        int count,
        float angleDegrees,
        string message = null)
    {
        Assert.Greater(count, 0);
        Assert.LessOrEqual(start + count, graph.nodes.Count);
        float normalized = angleDegrees % 360f;

        for (int i = start; i < start + count; ++i)
        {
            Assert.AreEqual(NowSdfShapeType.Glyph, graph.nodes[i].type);
            if (normalized == 0f)
                Assert.AreEqual(Vector2.zero, graph.nodes[i].rotation, message);
            else
                AssertRotation(graph.nodes[i].rotation, angleDegrees, message);
        }
    }

    static void AssertRotation(Vector2 actual, float angleDegrees, string message = null)
    {
        double radians = angleDegrees * System.Math.PI / 180d;
        Assert.AreEqual((float)System.Math.Cos(radians), actual.x, 0.000001f, message);
        Assert.AreEqual((float)System.Math.Sin(radians), actual.y, 0.000001f, message);
    }

    static void AssertRectApproximately(
        NowRect expected,
        NowRect actual,
        float tolerance,
        string message = null)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, message);
        Assert.AreEqual(expected.y, actual.y, tolerance, message);
        Assert.AreEqual(expected.width, actual.width, tolerance, message);
        Assert.AreEqual(expected.height, actual.height, tolerance, message);
    }

    static float NextRepresentableFloat(float value)
    {
        int bits = System.BitConverter.SingleToInt32Bits(value);
        return System.BitConverter.Int32BitsToSingle(value >= 0f ? bits + 1 : bits - 1);
    }

    static double ShaderReferenceScale(Vector2 rotation)
    {
        double squaredLength =
            (double)rotation.x * rotation.x +
            (double)rotation.y * rotation.y;
        float separateDot = rotation.x * rotation.x + rotation.y * rotation.y;
        float fusedDot = (float)squaredLength;
        return System.Math.Max(separateDot, fusedDot) / System.Math.Sqrt(squaredLength);
    }

    static void AssertRotatedBoundsContain(
        NowSdfNode node,
        Vector2 pivot,
        Vector2 point)
    {
        double squaredLength =
            (double)node.rotation.x * node.rotation.x +
            (double)node.rotation.y * node.rotation.y;
        float separateDot =
            node.rotation.x * node.rotation.x +
            node.rotation.y * node.rotation.y;
        float fusedDot = (float)squaredLength;
        double factor = System.Math.Max(separateDot, fusedDot) / squaredLength;
        double x = point.x - (double)pivot.x;
        double y = point.y - (double)pivot.y;
        double transformedX = pivot.x +
            (node.rotation.x * x - node.rotation.y * y) * factor;
        double transformedY = pivot.y +
            (node.rotation.y * x + node.rotation.x * y) * factor;

        Assert.LessOrEqual((double)node.bounds.x, transformedX);
        Assert.LessOrEqual((double)node.bounds.y, transformedY);
        Assert.GreaterOrEqual((double)node.bounds.xMax, transformedX);
        Assert.GreaterOrEqual((double)node.bounds.yMax, transformedY);
    }
}
