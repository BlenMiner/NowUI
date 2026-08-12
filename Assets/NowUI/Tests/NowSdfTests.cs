using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using NowUI.Sdf;

public class NowSdfTests
{
    NowDrawList _drawList;
    float _previousUiScale;

    [SetUp]
    public void SetUp()
    {
        _previousUiScale = Now.uiScale;
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
    public void SdfSceneRejectsMaterialsWithoutTheCurrentAbi()
    {
        var stock = Resources.Load<Material>("NowUI/SdfMaterial");
        var uiShader = Shader.Find("UI/Default");
        Assert.NotNull(stock);
        Assert.NotNull(uiShader);

        var missingAbi = new Material(uiShader);
        var wrongAbi = new Material(stock);
        wrongAbi.SetFloat(NowSdf.MaterialAbiProperty, NowSdf.MaterialAbiVersion + 1);

        try
        {
            Assert.Throws<System.ArgumentException>(() =>
                NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "missing-sdf-abi")
                    .SetMaterial(missingAbi));
            Assert.Throws<System.ArgumentException>(() =>
                NowSdf.Scene(new NowRect(0f, 0f, 32f, 32f), "wrong-sdf-abi")
                    .SetMaterial(wrongAbi));

            Assert.AreEqual(0, _drawList.batchCount);
            Assert.AreEqual(0, NowSdf.maskTextureCount);
            Assert.AreEqual(0, NowSdf.maskRasterizationCount);
        }
        finally
        {
            Object.DestroyImmediate(missingAbi);
            Object.DestroyImmediate(wrongAbi);
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

        Material Draw(bool? syncPerFrame)
        {
            using (_drawList.Begin(rect.size))
            {
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

            Assert.IsTrue(NowSdf.Release(id));
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

        try
        {
            using (_drawList.Begin(rect.size))
            {
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

            Assert.IsTrue(NowSdf.Release(id));
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
        RenderTexture target;

        using (_drawList.Begin(surface.size))
        using (NowControls.IdScope("release-owner"))
        using (NowSdf.Scene(surface, id)
            .Ellipse(surface)
            .BeginMask())
        {
            Now.Rectangle(surface).SetColor(Color.white).Draw();
        }

        target = _drawList.batches[0].maskState.GetTexture(0).texture as RenderTexture;

        Assert.IsTrue(target);
        Assert.AreEqual(1, NowSdf.cacheCount);
        Assert.AreEqual(1, NowSdf.maskTextureCount);
        Assert.AreEqual((long)target.width * target.height, NowSdf.cachedMaskPixels);

        using (NowControls.IdScope("release-owner"))
        {
            Assert.IsTrue(NowSdf.Release(id));
            Assert.IsFalse(NowSdf.Release(id));
        }

        Assert.AreEqual(0, NowSdf.cacheCount);
        Assert.AreEqual(0, NowSdf.maskTextureCount);
        Assert.AreEqual(0, NowSdf.cachedMaskPixels);
        Assert.IsFalse(target, "Releasing an explicit SDF cache did not destroy its mask target.");
        Assert.Throws<System.ArgumentException>(() => NowSdf.Release(default));
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
}
