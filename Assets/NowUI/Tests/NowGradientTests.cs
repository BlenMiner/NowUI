using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;
using UnityEngine;

public class NowGradientTests
{
    static readonly Vector2 Surface = new Vector2(320f, 240f);

    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowGradientMaterials.Reset();
        NowGradientRampCache.Reset();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowGradientMaterials.Reset();
        NowGradientRampCache.Reset();
    }

    [Test]
    public void GradientEmitsOneQuadWithDedicatedMaterials()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Gradient(new NowRect(10f, 12f, 120f, 64f), Color.red, Color.blue)
                .SetRadius(9f)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
        Assert.AreEqual(4, _drawList.mesh.vertexCount);
        Assert.AreEqual(1, _drawList.batchCount);

        var batch = _drawList.batches[0];
        Assert.AreEqual(NowMeshKind.Gradient, batch.kind);
        Assert.NotNull(batch.material);
        Assert.NotNull(batch.canvasMaterial);
        Assert.AreEqual("NowUI/UI Gradient", batch.material.shader.name);
        Assert.AreEqual("NowUI/UI Gradient UGUI", batch.canvasMaterial.shader.name);
        Assert.IsInstanceOf<Texture2D>(batch.material.mainTexture);
        Assert.AreSame(batch.material.mainTexture, batch.canvasMaterial.mainTexture);
    }

    [Test]
    public void DifferentGradientKindsAndRampsShareOneBatch()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Gradient(new NowRect(8f, 8f, 80f, 48f), Color.red, Color.blue)
                .SetLinear(45f)
                .Draw();
            Now.Gradient(new NowRect(96f, 8f, 80f, 48f), Color.green, Color.yellow)
                .SetRadial(NowGradientShape.Circle)
                .Draw();
            Now.Gradient(new NowRect(184f, 8f, 80f, 48f), Color.cyan, Color.magenta)
                .SetConic()
                .Draw();
        }

        Assert.AreEqual(1, _drawList.batchCount);
        Assert.AreEqual(12, _drawList.mesh.vertexCount);
        Assert.AreEqual(NowMeshKind.Gradient, _drawList.batches[0].kind);
    }

    [Test]
    public void GeometryKindsArePackedPerQuad()
    {
        using (_drawList.Begin(Surface))
        {
            Now.Gradient(new NowRect(8f, 8f, 80f, 48f))
                .SetLinear(NowGradientDirection.ToRight)
                .SetSpread(NowGradientSpread.Mirror)
                .SetRepetitions(3f)
                .Draw();
            Now.Gradient(new NowRect(96f, 8f, 80f, 48f))
                .SetRadial(new Vector2(0.25f, 0.75f), 0.4f)
                .Draw();
            Now.Gradient(new NowRect(184f, 8f, 80f, 48f))
                .SetConic(new Vector2(0.4f, 0.6f), 90f)
                .Draw();
        }

        var parameters = new List<Vector4>();
        var extras = new List<Vector4>();
        _drawList.mesh.GetUVs(3, parameters);
        _drawList.mesh.GetUVs(5, extras);

        Assert.AreEqual(12, parameters.Count);
        Assert.AreEqual(1f, parameters[0].x, 0.0001f);
        Assert.AreEqual(0f, parameters[0].y, 0.0001f);
        Assert.AreEqual(3f, parameters[0].z, 0.0001f);
        Assert.AreEqual((int)NowGradientKind.Linear, DecodeKind(extras[0].w));
        Assert.AreEqual((int)NowGradientSpread.Mirror, DecodeSpread(extras[0].w));

        Assert.AreEqual(0.25f, parameters[4].x, 0.0001f);
        Assert.AreEqual(0.75f, parameters[4].y, 0.0001f);
        Assert.AreEqual((int)NowGradientKind.Radial, DecodeKind(extras[4].w));
        Assert.IsTrue(DecodeCircle(extras[4].w));

        Assert.AreEqual(0.25f, parameters[8].z, 0.0001f);
        Assert.AreEqual((int)NowGradientKind.Conic, DecodeKind(extras[8].w));
    }

    [Test]
    public void UnityGradientRampRebakesInPlaceWhenInvalidated()
    {
        var ramp = CreateRamp(Color.black, Color.white);

        using (_drawList.Begin(Surface))
            Now.Gradient(new NowRect(8f, 8f, 120f, 48f), ramp).Draw();

        var extras = new List<Vector4>();
        _drawList.mesh.GetUVs(5, extras);
        int row = Mathf.FloorToInt(extras[0].w);
        var atlas = _drawList.batches[0].material.mainTexture as Texture2D;
        Assert.NotNull(atlas);
        Color before = atlas.GetPixel(0, row);

        ramp.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.blue, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        Now.InvalidateGradient(ramp);

        using (_drawList.Begin(Surface))
            Now.Gradient(new NowRect(8f, 8f, 120f, 48f), ramp).Draw();

        extras.Clear();
        _drawList.mesh.GetUVs(5, extras);
        Assert.AreEqual(row, Mathf.FloorToInt(extras[0].w), "invalidation should update the retained atlas row");
        Color after = atlas.GetPixel(0, row);
        Assert.Less(before.r, 0.05f);
        Assert.Greater(after.r, 0.95f);
        Assert.Less(after.g, 0.05f);
        Assert.Less(after.b, 0.05f);
    }

    [Test]
    public void FixedUnityGradientMarksRampForPointSampling()
    {
        var ramp = CreateRamp(Color.red, Color.blue);
        ramp.mode = GradientMode.Fixed;

        using (_drawList.Begin(Surface))
            Now.Gradient(new NowRect(8f, 8f, 120f, 48f), ramp).Draw();

        var extras = new List<Vector4>();
        _drawList.mesh.GetUVs(5, extras);
        Assert.IsTrue(DecodeFixed(extras[0].w));
    }

    [Test]
    public void CanvasCaptureUsesGradientCanvasMaterialAndRawUvPacking()
    {
        using var canvasList = new NowDrawList(NowMeshLayout.Canvas, "Now Gradient Canvas Test");

        using (canvasList.Begin(Surface))
        {
            Now.Gradient(new NowRect(12f, 14f, 100f, 54f), Color.red, Color.blue)
                .SetRadial()
                .SetRadius(8f)
                .Draw();
        }

        Assert.AreEqual(1, canvasList.batchCount);
        Assert.AreEqual(NowMeshKind.Gradient, canvasList.batches[0].kind);
        Assert.AreEqual("NowUI/UI Gradient UGUI", canvasList.batches[0].canvasMaterial.shader.name);

        var uv0 = new List<Vector4>();
        var uv3 = new List<Vector4>();
        canvasList.mesh.GetUVs(0, uv0);
        canvasList.mesh.GetUVs(3, uv3);
        Assert.AreEqual(4, uv0.Count);
        Assert.Less(uv0[0].w, 0f, "geometry padding should preserve extrapolated raw x");
        Assert.Less(uv3[0].z, 0f, "geometry padding should preserve extrapolated raw y");
    }

    [Test]
    public void ModifierSubdivisionSplitsGradientQuad()
    {
        using (_drawList.Begin(Surface))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 16f))
                   .SetSubdivision(4)
                   .Begin())
        {
            Now.Gradient(new NowRect(8f, 8f, 120f, 48f), Color.red, Color.blue)
                .Draw();
        }

        Assert.AreEqual(25, _drawList.mesh.vertexCount);
        Assert.AreEqual(NowMeshKind.Gradient, _drawList.batches[0].kind);
        Assert.AreEqual("NowUI/UI Gradient", _drawList.batches[0].material.shader.name);
    }

    [Test]
    public void GradientFrameBuildIsAllocationFreeAfterWarmup()
    {
        var ramp = CreateRamp(Color.red, Color.blue);

        void DrawFrame()
        {
            using (_drawList.Begin(Surface))
            {
                for (int i = 0; i < 48; ++i)
                {
                    Now.Gradient(new NowRect((i * 7) % 280, (i * 11) % 200, 32f, 20f), ramp)
                        .SetLinear(i * 17f)
                        .SetRadius(4f)
                        .Draw();
                }
            }
        }

        DrawFrame();
        DrawFrame();
        DrawFrame();

        long before = AllocatedBytesOrIgnore();
        DrawFrame();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0, allocated, "steady-state gradient draw-list build must not allocate");
    }

    static Gradient CreateRamp(Color from, Color to)
    {
        var ramp = new Gradient();
        ramp.SetKeys(
            new[]
            {
                new GradientColorKey(from, 0f),
                new GradientColorKey(to, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return ramp;
    }

    static int DecodeFlags(float encoded)
    {
        return Mathf.FloorToInt(Mathf.Repeat(encoded, 1f) * 256f);
    }

    static int DecodeKind(float encoded)
    {
        return DecodeFlags(encoded) & 0x3;
    }

    static int DecodeSpread(float encoded)
    {
        return DecodeFlags(encoded) >> 2 & 0x3;
    }

    static bool DecodeCircle(float encoded)
    {
        return (DecodeFlags(encoded) & 1 << 4) != 0;
    }

    static bool DecodeFixed(float encoded)
    {
        return (DecodeFlags(encoded) & 1 << 5) != 0;
    }

    static long AllocatedBytesOrIgnore()
    {
        try
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return 0;
        }
    }
}
