using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using NowUI;
using NowUI.Sdf;
using Object = UnityEngine.Object;

/// <summary>
/// Play-mode tests that render real frames and assert on the pixels that come
/// back: the GPU draw path, shader output, font atlas uploads, and Lottie
/// tessellation — everything the edit-mode capture tests cannot see.
/// </summary>
public class NowRenderingPlayModeTests
{
    const int Side = 128;

    RenderTexture _target;
    NowRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _target = new RenderTexture(Side, Side, 0, RenderTextureFormat.ARGB32);
        _target.Create();
        _renderer = new NowRenderer();
        NowSdf.Reset();
        NowFontCompiler.forceManagedCompiler = false;
    }

    [TearDown]
    public void TearDown()
    {
        NowFontCompiler.forceManagedCompiler = false;
        _renderer.Dispose();
        NowSdf.Reset();
        _target.Release();
        Object.DestroyImmediate(_target);
    }

    static Color32[] ReadPixels(RenderTexture target)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;

        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();

        RenderTexture.active = previous;

        var pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);
        return pixels;
    }

    static int CountPixels(Color32[] pixels, System.Predicate<Color32> predicate)
    {
        int count = 0;

        for (int i = 0; i < pixels.Length; ++i)
        {
            if (predicate(pixels[i]))
                ++count;
        }

        return count;
    }

    static Color32 PixelAtUi(Color32[] pixels, int x, int y)
    {
        return pixels[(Side - 1 - y) * Side + x];
    }

    static void AssertAlphaArraysNear(
        Color32[] expected,
        Color32[] actual,
        int channelTolerance,
        int allowedMismatchCount,
        string message)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        int mismatches = 0;
        int largestDifference = 0;

        for (int i = 0; i < expected.Length; ++i)
        {
            int difference = Mathf.Abs(expected[i].a - actual[i].a);
            largestDifference = Mathf.Max(largestDifference, difference);

            if (difference > channelTolerance)
                ++mismatches;
        }

        Assert.LessOrEqual(
            mismatches,
            allowedMismatchCount,
            $"{message} {mismatches} alpha samples differed beyond {channelTolerance}; largest difference was {largestDifference}.");
    }

    static void AssertColorArraysNear(
        Color32[] expected,
        Color32[] actual,
        int channelTolerance,
        int allowedMismatchCount,
        string message)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        int mismatches = 0;
        int largestDifference = 0;

        for (int i = 0; i < expected.Length; ++i)
        {
            int difference = Mathf.Max(
                Mathf.Abs(expected[i].r - actual[i].r),
                Mathf.Abs(expected[i].g - actual[i].g),
                Mathf.Abs(expected[i].b - actual[i].b),
                Mathf.Abs(expected[i].a - actual[i].a));
            largestDifference = Mathf.Max(largestDifference, difference);

            if (difference > channelTolerance)
                ++mismatches;
        }

        Assert.LessOrEqual(
            mismatches,
            allowedMismatchCount,
            $"{message} {mismatches} pixels differed beyond {channelTolerance}; largest difference was {largestDifference}.");
    }

    static RectInt FindAlphaBounds(Color32[] pixels, byte minimumAlpha = 12)
    {
        int xMin = Side;
        int yMin = Side;
        int xMax = -1;
        int yMax = -1;

        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                if (PixelAtUi(pixels, x, y).a <= minimumAlpha)
                    continue;

                xMin = Mathf.Min(xMin, x);
                yMin = Mathf.Min(yMin, y);
                xMax = Mathf.Max(xMax, x);
                yMax = Mathf.Max(yMax, y);
            }
        }

        return xMax >= xMin && yMax >= yMin
            ? new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1)
            : default;
    }

    static RectInt FindRawAlphaBounds(
        Color32[] pixels,
        int width,
        int height,
        byte minimumAlpha = 12)
    {
        int xMin = width;
        int yMin = height;
        int xMax = -1;
        int yMax = -1;

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                if (pixels[y * width + x].a <= minimumAlpha)
                    continue;

                xMin = Mathf.Min(xMin, x);
                yMin = Mathf.Min(yMin, y);
                xMax = Mathf.Max(xMax, x);
                yMax = Mathf.Max(yMax, y);
            }
        }

        return xMax >= xMin && yMax >= yMin
            ? new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1)
            : default;
    }

    static GameObject CreateSkinnedCube(
        Material material,
        out Transform animatedBone,
        out Mesh ownedMesh)
    {
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cubeMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);

        var root = new GameObject("Skinned Preview Pixel Source");
        var boneRoot = new GameObject("Bone Root").transform;
        boneRoot.SetParent(root.transform, false);
        var boneTip = new GameObject("Bone Tip").transform;
        boneTip.SetParent(boneRoot, false);
        boneTip.localPosition = Vector3.up * 0.5f;
        var rendererObject = new GameObject("Skinned Cube");
        rendererObject.transform.SetParent(root.transform, false);
        var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();

        ownedMesh = Object.Instantiate(cubeMesh);
        ownedMesh.name = "Now Preview Pixel Skinned Cube";
        var vertices = ownedMesh.vertices;
        var weights = new BoneWeight[vertices.Length];

        for (int i = 0; i < vertices.Length; ++i)
        {
            weights[i] = new BoneWeight
            {
                boneIndex0 = vertices[i].y > 0f ? 1 : 0,
                weight0 = 1f
            };
        }

        ownedMesh.boneWeights = weights;
        ownedMesh.bindposes = new[]
        {
            boneRoot.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
            boneTip.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix
        };
        ownedMesh.RecalculateBounds();
        renderer.sharedMesh = ownedMesh;
        renderer.sharedMaterial = material;
        renderer.bones = new[] { boneRoot, boneTip };
        renderer.rootBone = boneRoot;
        renderer.localBounds = ownedMesh.bounds;
        renderer.updateWhenOffscreen = true;
        animatedBone = boneTip;
        return root;
    }

    static void AssertTargetOpaque(Color32[] pixels, string message)
    {
        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                var pixel = pixels[y * Side + x];
                Assert.GreaterOrEqual(pixel.a, 250, $"{message} at {x},{y}.");
            }
        }
    }

    static NowFont ResolveDefaultNowFont()
    {
        var fallback = Now.defaultFont;
        Assert.NotNull(fallback, "Default font resource is missing.");
        Assert.IsTrue(
            fallback.TryResolveGlyph('A', 32f, NowFontStyle.Regular, out NowFont resolved, out _, out _),
            "Default font could not resolve a glyph.");
        return resolved;
    }

    static NowFont CreateSolidColorFont(Color32 authoredColor)
    {
        var template = Resources.Load<Material>("NowUI/TxtMaterialRGBA");
        Assert.NotNull(template, "RGBA text material resource is missing.");

        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = "Synthetic Color Font Atlas",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        atlas.SetPixels32(new[] { authoredColor, authoredColor, authoredColor, authoredColor });
        atlas.Apply(false, false);

        var material = new Material(template)
        {
            name = "Synthetic Color Font Material",
            mainTexture = atlas
        };

        var font = ScriptableObject.CreateInstance<NowFont>();
        font.name = "Synthetic Color Font";
        font.atlas = atlas;
        font.material = material;
        font.atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = NowFont.ATLAS_TYPE_RGBA,
                size = 2,
                width = 2,
                height = 2,
                yOrigin = "bottom"
            },
            metrics = new NowFontAtlasInfo.Metrics
            {
                emSize = 1f,
                lineHeight = 1f,
                ascender = 1f
            },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1f,
                    planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 1f,
                        top = 1f
                    },
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 2f,
                        top = 2f
                    }
                }
            }
        };
        return font;
    }

    static NowFont CreateSyntheticSdfFont()
    {
        const int AtlasSide = 16;
        const int DistanceRange = 4;
        var template = Resources.Load<Material>("NowUI/TxtMaterial");
        Assert.NotNull(template, "MTSDF text material resource is missing.");

        var atlas = new Texture2D(AtlasSide, AtlasSide, TextureFormat.RGBA32, false, true)
        {
            name = "Synthetic SDF Font Atlas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[AtlasSide * AtlasSide];
        Vector2 center = new Vector2(AtlasSide * 0.5f, AtlasSide * 0.5f);

        for (int y = 0; y < AtlasSide; ++y)
        {
            for (int x = 0; x < AtlasSide; ++x)
            {
                float signedDistance = 4f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                byte encoded = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(0.5f + signedDistance / DistanceRange) * 255f);
                pixels[y * AtlasSide + x] = new Color32(encoded, encoded, encoded, encoded);
            }
        }

        atlas.SetPixels32(pixels);
        atlas.Apply(false, false);

        var material = new Material(template)
        {
            name = "Synthetic SDF Font Material",
            mainTexture = atlas
        };
        var font = ScriptableObject.CreateInstance<NowFont>();
        font.name = "Synthetic SDF Font";
        font.atlas = atlas;
        font.material = material;
        font.atlasInfo = new NowFontAtlasInfo
        {
            atlas = new NowFontAtlasInfo.Atlas
            {
                type = NowFont.ATLAS_TYPE_MTSDF,
                distanceRange = DistanceRange,
                size = AtlasSide,
                width = AtlasSide,
                height = AtlasSide,
                yOrigin = "bottom"
            },
            metrics = new NowFontAtlasInfo.Metrics
            {
                emSize = 1f,
                lineHeight = 1f,
                ascender = 1f
            },
            glyphs = new[]
            {
                new NowFontAtlasInfo.Glyph
                {
                    unicode = 'A',
                    advance = 1f,
                    planeBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = 1f,
                        top = 1f
                    },
                    atlasBounds = new NowFontAtlasInfo.Bounds
                    {
                        right = AtlasSide,
                        top = AtlasSide
                    }
                }
            }
        };
        return font;
    }

    static void DestroySyntheticFont(NowFont font)
    {
        if (font == null)
            return;

        if (font.material != null)
            Object.DestroyImmediate(font.material);

        if (font.atlas != null)
            Object.DestroyImmediate(font.atlas);

        Object.DestroyImmediate(font);
    }

    [Test]
    public void RectangleRendersExpectedCoverage()
    {
        // Centered square so the assertion is immune to platform UV flips.
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(32, 32, 64, 64))
                .SetColor(Color.red)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int red = CountPixels(pixels, p => p.r > 200 && p.g < 50 && p.a > 200);

        Assert.GreaterOrEqual(red, 64 * 64 - 300, "Rectangle covered fewer pixels than expected.");
        Assert.LessOrEqual(red, 64 * 64 + 300, "Rectangle covered more pixels than expected.");

        Assert.Greater(pixels[(Side / 2) * Side + Side / 2].r, 200);
        Assert.AreEqual(0, pixels[4 * Side + 4].a);
    }

    [Test]
    public void AnalyticCircleMaskProducesAntialiasedCoverage()
    {
        var pixels = RenderWhiteCircleMask(feather: 0f);

        Assert.Greater(pixels[(Side / 2) * Side + Side / 2].a, 250, "Mask center was not opaque.");
        Assert.Less(pixels[4 * Side + 4].a, 5, "Pixels far outside the mask were not transparent.");

        int partial = CountMaskTransitionPixels(pixels);
        Assert.Greater(partial, 40, "The analytic boundary produced no measurable anti-aliasing ramp.");
    }

    [Test]
    public void AdditionalAnalyticMaskFeatherWidensTransition()
    {
        var crisp = RenderWhiteCircleMask(feather: 0f);
        var soft = RenderWhiteCircleMask(feather: 6f);

        int crispPartial = CountMaskTransitionPixels(crisp);
        int softPartial = CountMaskTransitionPixels(soft);

        Assert.Greater(crispPartial, 40, "The default derivative anti-aliasing ramp was missing.");
        Assert.Greater(
            softPartial,
            crispPartial * 2,
            $"Six additional screen pixels of feather did not widen the transition ({crispPartial} -> {softPartial}).");
    }

    [Test]
    public void RenderToTextureModifierDoesNotApplyAnalyticFeatherTwice()
    {
        const float feather = 6f;
        var direct = RenderWhiteCircleMask(feather);
        _renderer.Clear();
        var replayed = RenderWhiteCircleMaskThroughTextureModifier(feather);
        long directTransitionAlpha = 0;
        long replayedTransitionAlpha = 0;
        int transitionPixels = 0;

        for (int i = 0; i < direct.Length; ++i)
        {
            byte directAlpha = direct[i].a;
            if (directAlpha < 24 || directAlpha > 231)
                continue;

            directTransitionAlpha += directAlpha;
            replayedTransitionAlpha += replayed[i].a;
            ++transitionPixels;
        }

        Assert.Greater(transitionPixels, 40, "The direct mask had no stable feather samples to compare.");
        Assert.Greater(
            replayedTransitionAlpha,
            directTransitionAlpha * 0.82f,
            "Render-to-texture replay reduced the feather as if its coverage was multiplied a second time.");
        Assert.Greater(replayed[(Side / 2) * Side + Side / 2].a, 245, "Modifier replay lost opaque center coverage.");
        Assert.Less(replayed[4 * Side + 4].a, 5, "Modifier capture failed to bake the ambient analytic mask.");
    }

    [Test]
    public void SiblingAnalyticMasksSplitBatchesAndKeepCommandPropertiesIsolated()
    {
        var fullSurface = new NowRect(0f, 0f, Side, Side);
        var leftCenter = new Vector2(32f, Side * 0.5f);
        var rightCenter = new Vector2(Side - 32f, Side * 0.5f);

        using (_renderer.Begin(_target))
        {
            using (Now.Mask(NowMaskShape.Circle(leftCenter, 18f)))
            {
                Now.Rectangle(fullSurface)
                    .SetColor(Color.red)
                    .Draw();
            }

            using (Now.Mask(NowMaskShape.Circle(rightCenter, 18f)))
            {
                // This is the same rectangle material and geometry as the first
                // draw. Only vertex color and ambient analytic mask state differ.
                Now.Rectangle(fullSurface)
                    .SetColor(Color.blue)
                    .Draw();
            }
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        Color32 left = pixels[(Side / 2) * Side + (int)leftCenter.x];
        Color32 right = pixels[(Side / 2) * Side + (int)rightCenter.x];
        Color32 gap = pixels[(Side / 2) * Side + Side / 2];

        Assert.Greater(left.r, 240, $"First batch lost its red mask state: {left}.");
        Assert.Less(left.g, 12, $"First batch picked up an unexpected green channel: {left}.");
        Assert.Less(left.b, 12, $"First batch reused the blue sibling's properties: {left}.");
        Assert.Greater(left.a, 240, $"First circle center was not opaque: {left}.");

        Assert.Less(right.r, 12, $"Second batch reused the red sibling's properties: {right}.");
        Assert.Less(right.g, 12, $"Second batch picked up an unexpected green channel: {right}.");
        Assert.Greater(right.b, 240, $"Second batch lost its blue mask state: {right}.");
        Assert.Greater(right.a, 240, $"Second circle center was not opaque: {right}.");

        Assert.Less(gap.a, 8, $"Sibling circle masks leaked into their disjoint gap: {gap}.");
    }

    [Test]
    public void SdfMaskRendersSubtractiveFieldWithAntialiasedCoverage()
    {
        var surface = new NowRect(0f, 0f, Side, Side);
        var center = surface.center;

        using (_renderer.Begin(_target))
        using (NowSdf.Scene(surface, "playmode-sdf-donut-mask")
            .Circle(center, 40f)
            .Subtract()
            .Circle(center, 16f)
            .BeginMask())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        Color32 hole = PixelAtUi(pixels, Side / 2, Side / 2);
        Color32 ring = PixelAtUi(pixels, 92, Side / 2);
        Color32 outside = PixelAtUi(pixels, 8, Side / 2);

        Assert.Less(hole.a, 8, $"The subtracted SDF hole was not transparent: {hole}.");
        Assert.Greater(ring.a, 245, $"The composed SDF ring was not opaque: {ring}.");
        Assert.Less(outside.a, 8, $"The SDF mask leaked beyond its outer field: {outside}.");
        Assert.Greater(
            CountMaskTransitionPixels(pixels),
            100,
            "The rasterized SDF mask lost its antialiased inner or outer boundary.");
    }

    [Test]
    public void SdfGraphRangesRenderFillAndEffectDistancePasses()
    {
        var surface = new NowRect(0f, 0f, Side, Side);
        var graphA = NowSdf.Graph()
            .SetColor(Color.red)
            .Circle(new Vector2(32f, 36f), 12f)
            .Circle(new Vector2(32f, 92f), 10f);
        var graphB = NowSdf.Graph()
            .SetColor(Color.blue)
            .Box(new NowRect(78f, 24f, 24f, 24f))
            .Ellipse(new NowRect(82f, 62f, 20f, 24f))
            .Capsule(new NowRect(78f, 96f, 28f, 12f));

        void Draw(bool effects)
        {
            using (_renderer.Begin(_target))
            {
                var scene = NowSdf.Scene(surface, "playmode-sdf-packed-graph-ranges");

                if (effects)
                {
                    scene = scene
                        .SetShadow(new Vector2(12f, 0f), 2f, Color.white)
                        .SetInnerShadow(new Vector2(-4f, 0f), 3f, new Color(0f, 0f, 0f, 0.8f));
                }

                scene
                    .Graph(graphA)
                    .Union()
                    .Graph(graphB)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
        }

        Draw(effects: false);
        var plain = ReadPixels(_target);
        Color32 red = PixelAtUi(plain, 32, 36);
        Color32 blue = PixelAtUi(plain, 90, 36);
        Color32 gap = PixelAtUi(plain, 60, 60);

        Assert.Greater(red.r, 240, $"The first packed graph lost its red fill: {red}.");
        Assert.Less(red.b, 12, $"The first packed graph sampled the second graph's fill: {red}.");
        Assert.Greater(blue.b, 240, $"The second packed graph lost its blue fill: {blue}.");
        Assert.Less(blue.r, 12, $"The second packed graph sampled the first graph's fill: {blue}.");
        Assert.Less(gap.a, 8, $"Disjoint packed graph ranges leaked into their gap: {gap}.");

        _renderer.Clear();
        Draw(effects: true);
        var effected = ReadPixels(_target);
        Color32 shadow = PixelAtUi(effected, 48, 36);

        Assert.Greater(
            shadow.a,
            200,
            $"The shadow distance pass did not evaluate the packed graph range: {shadow}.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfRadialPrimitivesHonorSignedSweepDirection(bool arc)
    {
        var positive = RenderSdfRadial(arc, Mathf.PI * 0.5f);
        var negative = RenderSdfRadial(arc, -Mathf.PI * 0.5f);
        int offset = arc ? 25 : 20;
        var downRight = new Vector2Int(64 + offset, 64 + offset);
        var upRight = new Vector2Int(64 + offset, 64 - offset);

        Assert.Greater(PixelAtUi(positive, downRight.x, downRight.y).a, 220,
            "A positive sweep from zero radians must turn clockwise in UI space.");
        Assert.Less(PixelAtUi(positive, upRight.x, upRight.y).a, 16);
        Assert.Greater(PixelAtUi(negative, upRight.x, upRight.y).a, 220,
            "A negative sweep from zero radians must turn counter-clockwise.");
        Assert.Less(PixelAtUi(negative, downRight.x, downRight.y).a, 16);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfRadialPrimitivesClampOverTurnsToASeamlessFullTurn(bool arc)
    {
        var full = RenderSdfRadial(arc, Mathf.PI * 2f);
        var positiveOverTurn = RenderSdfRadial(arc, Mathf.PI * 4f);
        var negativeOverTurn = RenderSdfRadial(arc, -Mathf.PI * 4f);

        CollectionAssert.AreEqual(full, positiveOverTurn,
            "A positive over-turn must render exactly like one full turn.");
        CollectionAssert.AreEqual(full, negativeOverTurn,
            "A negative over-turn must render exactly like one full turn.");

        var samples = arc
            ? new[]
            {
                new Vector2Int(100, 64), new Vector2Int(28, 64),
                new Vector2Int(64, 100), new Vector2Int(64, 28)
            }
            : new[]
            {
                new Vector2Int(64, 64), new Vector2Int(88, 64),
                new Vector2Int(40, 64), new Vector2Int(64, 88), new Vector2Int(64, 40)
            };

        foreach (var sample in samples)
        {
            Assert.Greater(PixelAtUi(full, sample.x, sample.y).a, 220,
                $"Full-turn {(arc ? "Arc" : "Pie")} has a seam or missing quadrant at {sample}.");
        }

        Assert.Less(PixelAtUi(full, 116, 64).a, 16,
            "A full-turn radial shape leaked beyond its outer radius.");

        if (arc)
            Assert.Less(PixelAtUi(full, 64, 64).a, 16, "A full Arc filled its hollow center.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfRadialPrimitivesSampleTextureAcrossConservativeBounds(bool arc)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            var red = new Color32(255, 0, 0, 255);
            var blue = new Color32(0, 0, 255, 255);
            texture.SetPixels32(new[] { red, blue, red, blue });
            texture.Apply(false, false);

            var surface = new NowRect(0f, 0f, Side, Side);
            using (_renderer.Begin(_target))
            {
                var scene = NowSdf.Scene(surface, "playmode-sdf-radial-texture")
                    .SetTexture(texture);
                scene = arc
                    ? scene.Arc(surface.center, 36f, 8f, 0f, Mathf.PI * 2f)
                    : scene.Pie(surface.center, 36f, 0f, Mathf.PI * 2f);
                scene.Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            int leftX = arc ? 28 : 48;
            int rightX = arc ? 100 : 80;
            Color32 left = PixelAtUi(pixels, leftX, 64);
            Color32 right = PixelAtUi(pixels, rightX, 64);

            Assert.Greater(left.r, 220, $"The left side lost the texture's red texel: {left}.");
            Assert.Less(left.b, 16, $"The left side sampled the texture's blue texel: {left}.");
            Assert.Greater(right.b, 220, $"The right side lost the texture's blue texel: {right}.");
            Assert.Less(right.r, 16, $"The right side sampled the texture's red texel: {right}.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SdfTriangleHonorsBothWindingOrders()
    {
        var clockwise = RenderSdfTriangle(reverseWinding: false);
        var counterClockwise = RenderSdfTriangle(reverseWinding: true);

        AssertAlphaArraysNear(
            clockwise,
            counterClockwise,
            channelTolerance: 2,
            allowedMismatchCount: 8,
            "Reversing Triangle winding changed its rendered field.");

        Assert.Greater(PixelAtUi(clockwise, 64, 64).a, 240, "The clockwise Triangle lost its interior.");
        Assert.Greater(PixelAtUi(counterClockwise, 64, 64).a, 240, "The counter-clockwise Triangle lost its interior.");
        Assert.Less(PixelAtUi(clockwise, 16, 64).a, 16, "The clockwise Triangle leaked outside its edge.");
        Assert.Less(PixelAtUi(counterClockwise, 16, 64).a, 16, "The counter-clockwise Triangle leaked outside its edge.");
    }

    [Test]
    public void SdfDegenerateTriangleIsAnUnsignedLocalizedEdge()
    {
        var pixels = RenderSdfDegenerateTriangle();

        Assert.Greater(PixelAtUi(pixels, 64, 64).a, 200,
            "The degenerate Triangle lost its edge field.");
        Assert.Less(PixelAtUi(pixels, 64, 48).a, 16,
            "The degenerate Triangle falsely filled above its collapsed edge.");
        Assert.Less(PixelAtUi(pixels, 64, 80).a, 16,
            "The degenerate Triangle falsely filled below its collapsed edge.");
    }

    [Test]
    public void SdfChamferedBoxClipsCornersAndZeroChamferMatchesBox()
    {
        var chamfered = RenderSdfChamferedBox(14f, useBox: false);

        Assert.Less(PixelAtUi(chamfered, 23, 27).a, 16, "ChamferedBox did not clip its top-left corner.");
        Assert.Greater(PixelAtUi(chamfered, 36, 28).a, 240, "ChamferedBox clipped past its diagonal corner edge.");
        Assert.Greater(PixelAtUi(chamfered, 64, 64).a, 240, "ChamferedBox lost its center.");

        var zeroChamfer = RenderSdfChamferedBox(0f, useBox: false);
        var box = RenderSdfChamferedBox(0f, useBox: true);
        AssertAlphaArraysNear(
            box,
            zeroChamfer,
            channelTolerance: 1,
            allowedMismatchCount: 0,
            "A zero-chamfer ChamferedBox did not render like Box.");
    }

    [Test]
    public void SdfRotateNextBoxQuarterTurnMatchesSwappedBox()
    {
        var rotated = RenderSdfBoxQuarterTurn(rotated: true);
        var swappedBox = RenderSdfBoxQuarterTurn(rotated: false);

        AssertAlphaArraysNear(
            swappedBox,
            rotated,
            channelTolerance: 1,
            allowedMismatchCount: 0,
            "A Box rotated 90 degrees did not match its swapped axis-aligned bounds.");
    }

    [Test]
    public void SdfRotateNextBoxPositiveAnglesClockwise()
    {
        var clockwise = RenderSdfRotatedBox(30f);
        var counterClockwise = RenderSdfRotatedBox(-30f);

        Assert.Greater(PixelAtUi(clockwise, 87, 77).a, 240,
            "The positive-angle Box lost its clockwise major axis.");
        Assert.Less(PixelAtUi(clockwise, 87, 51).a, 16,
            "The positive-angle Box rotated counter-clockwise.");
        Assert.Greater(PixelAtUi(counterClockwise, 87, 51).a, 240,
            "The negative-angle Box lost its counter-clockwise major axis.");
        Assert.Less(PixelAtUi(counterClockwise, 87, 77).a, 16,
            "The negative-angle Box rotated clockwise.");
    }

    [Test]
    public void SdfRotateNextTextUsesOneSharedRunPivot()
    {
        const string Value = "Hi!";
        const float FontSize = 36f;
        var font = ResolveDefaultNowFont();
        var surface = new NowRect(0f, 0f, Side, Side);
        Vector2 position = surface.center - SdfTextRunPivot(
            font,
            Vector2.zero,
            Value,
            FontSize,
            out int glyphCount);

        Assert.AreEqual(Value.Length, glyphCount,
            "The shared-pivot fixture requires every character to emit one SDF glyph.");

        // Correct once for float rounding in the positioned glyph rectangles so
        // the run pivot lands on the render target's pixel-grid quarter-turn.
        position += surface.center - SdfTextRunPivot(
            font,
            position,
            Value,
            FontSize,
            out glyphCount);
        Vector2 pivot = SdfTextRunPivot(font, position, Value, FontSize, out glyphCount);

        Assert.AreEqual(Value.Length, glyphCount);
        Assert.AreEqual(surface.center.x, pivot.x, 0.001f);
        Assert.AreEqual(surface.center.y, pivot.y, 0.001f);

        var unrotated = RenderSdfTextRun(font, position, Value, FontSize, rotate: false);
        var rotated = RenderSdfTextRun(font, position, Value, FontSize, rotate: true);
        var expected = RotateAlphaQuarterTurnClockwise(unrotated, pivot);

        Assert.Greater(CountPixels(unrotated, pixel => pixel.a > 128), 120,
            "The SDF text fixture produced too little ink for a useful rotation comparison.");
        AssertAlphaArraysNear(
            expected,
            rotated,
            channelTolerance: 12,
            allowedMismatchCount: 96,
            "RotateNext did not rotate the glyph arrangement rigidly around the rendered run AABB center.");
    }

    [Test]
    public void DirectSdfDrawUniformTransformScalesAnalyticContent()
    {
        var pixels = RenderTransformedSdfCircle(
            new NowRect(0f, 0f, 48f, 48f),
            new Vector2(16f, 16f),
            6f,
            new Vector2(2f, 2f),
            new Vector2(8f, 8f),
            "playmode-sdf-direct-uniform-transform");
        RectInt bounds = FindAlphaBounds(pixels);

        Assert.Greater(PixelAtUi(pixels, 40, 40).a, 240,
            $"The transformed circle lost its expected centre; visible bounds were {bounds}.");
        Assert.Greater(PixelAtUi(pixels, 50, 40).a, 240,
            "The 2x transform did not double the circle radius.");
        Assert.Less(PixelAtUi(pixels, 54, 40).a, 8,
            "The uniformly transformed circle extended beyond its expected radius.");
        Assert.Less(PixelAtUi(pixels, 24, 24).a, 8,
            "The circle stayed at its unscaled local-domain position inside an enlarged quad.");
        Assert.That(bounds.xMin, Is.InRange(27, 29));
        Assert.That(bounds.yMin, Is.InRange(27, 29));
        Assert.That(bounds.width, Is.InRange(23, 26));
        Assert.That(bounds.height, Is.InRange(23, 26));
    }

    [Test]
    public void DirectSdfDrawMirroredNonUniformTransformScalesAnalyticContent()
    {
        var pixels = RenderTransformedSdfCircle(
            new NowRect(0f, 0f, 40f, 80f),
            new Vector2(12f, 40f),
            8f,
            new Vector2(-2f, 0.5f),
            new Vector2(96f, 20f),
            "playmode-sdf-direct-mirrored-transform");
        RectInt bounds = FindAlphaBounds(pixels);

        Assert.Greater(PixelAtUi(pixels, 72, 40).a, 240,
            $"The mirrored ellipse lost its expected centre; visible bounds were {bounds}.");
        Assert.Greater(PixelAtUi(pixels, 84, 40).a, 240,
            "The nonuniform transform did not expand the circle horizontally.");
        Assert.Greater(PixelAtUi(pixels, 72, 42).a, 220,
            "The nonuniform transform collapsed the circle's compressed vertical interior.");
        Assert.Less(PixelAtUi(pixels, 90, 40).a, 8,
            "The nonuniformly transformed circle extended beyond its horizontal radius.");
        Assert.Less(PixelAtUi(pixels, 72, 46).a, 8,
            "The nonuniformly transformed circle extended beyond its vertical radius.");
        Assert.Less(PixelAtUi(pixels, 40, 40).a, 8,
            "The negative X scale lost its sign and placed the circle on the unmirrored side.");
        Assert.That(bounds.xMin, Is.InRange(55, 57));
        Assert.That(bounds.yMin, Is.InRange(35, 37));
        Assert.That(bounds.width, Is.InRange(31, 34));
        Assert.That(bounds.height, Is.InRange(7, 10));
    }

    [Test]
    public void DirectSdfDrawUniformTransformMatchesAuthoredGlyphScale()
    {
        const string Value = "O";
        const float FontSize = 32f;
        const float Outline = 3f;
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        try
        {
            _renderer.Clear();
            using (_renderer.Begin(_target))
            using (Now.Transform(2f))
            {
                NowSdf.Scene(
                        new NowRect(0f, 0f, Side * 0.5f, Side * 0.5f),
                        "playmode-sdf-direct-transformed-glyph")
                    .SetColor(Color.white)
                    .SetOutline(Outline, Color.white)
                    .Text(new Vector2(14f, 10f), Value, font, FontSize)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            Color32[] transformed = ReadPixels(_target);

            _renderer.Clear();
            using (_renderer.Begin(_target))
            {
                NowSdf.Scene(
                        new NowRect(0f, 0f, Side, Side),
                        "playmode-sdf-direct-authored-glyph-scale")
                    .SetColor(Color.white)
                    .SetOutline(Outline * 2f, Color.white)
                    .Text(new Vector2(28f, 20f), Value, font, FontSize * 2f)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            Color32[] authored = ReadPixels(_target);

            int authoredCoverage = CountPixels(authored, pixel => pixel.a > 128);
            int transformedCoverage = CountPixels(transformed, pixel => pixel.a > 128);
            RectInt authoredBounds = FindAlphaBounds(authored);
            RectInt transformedBounds = FindAlphaBounds(transformed);

            Assert.Greater(authoredCoverage, 700,
                "The authored glyph-scale reference produced too little useful coverage.");
            Assert.That(
                Mathf.Abs(transformedCoverage - authoredCoverage),
                Is.LessThanOrEqualTo(32),
                "The direct transform changed the glyph's high-alpha coverage materially.");
            Assert.That(Mathf.Abs(transformedBounds.xMin - authoredBounds.xMin), Is.LessThanOrEqualTo(1));
            Assert.That(Mathf.Abs(transformedBounds.yMin - authoredBounds.yMin), Is.LessThanOrEqualTo(1));
            Assert.That(Mathf.Abs(transformedBounds.width - authoredBounds.width), Is.LessThanOrEqualTo(1));
            Assert.That(Mathf.Abs(transformedBounds.height - authoredBounds.height), Is.LessThanOrEqualTo(1));
            AssertAlphaArraysNear(
                authored,
                transformed,
                channelTolerance: 4,
                allowedMismatchCount: 256,
                "A direct 2x SDF transform did not match the equivalent authored glyph size.");
        }
        finally
        {
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [TestCase(false, false, TestName = "SdfLargeOutlineMatchesOrdinaryTextGeometry_Managed_AbiV2")]
    [TestCase(false, true, TestName = "SdfLargeOutlineMatchesOrdinaryTextGeometry_Managed_AbiV1")]
    [TestCase(true, false, TestName = "SdfLargeOutlineMatchesOrdinaryTextGeometry_Native_AbiV2")]
    [TestCase(true, true, TestName = "SdfLargeOutlineMatchesOrdinaryTextGeometry_Native_AbiV1")]
    public void SdfLargeOutlineMatchesOrdinaryTextGeometry(bool nativeCompiler, bool legacyAbi)
    {
        const int Width = 256;
        const int Height = 256;
        const float FontSize = 80f;
        const float Outline = 32f;
        const string Value = "A";
        var position = new Vector2(88f, 80f);
        NowFont font = null;
        NowFontCompiler.DynamicSession nativeProbe = null;
        RenderTexture target = null;
        CommandBuffer commandBuffer = null;
        Material legacyMaterial = null;

        try
        {
            var source = ResolveDefaultNowFont();
            Assert.IsTrue(
                source.TryGetSourceBytes(out byte[] bytes),
                "Default font has no embedded source.");

            NowFontCompiler.forceManagedCompiler = !nativeCompiler;
            NowFontCompiler.forceNativeCompiler = nativeCompiler;

            if (nativeCompiler)
            {
                bool nativeAvailable = NowFontCompiler.DynamicSession.TryCreate(
                    bytes,
                    64,
                    16,
                    512,
                    out nativeProbe,
                    out string nativeProbeError) &&
                    !nativeProbe.isManaged;

                if (!nativeAvailable)
                {
                    nativeProbe?.Dispose();
                    nativeProbe = null;
                    Assert.Ignore($"Native font compiler unavailable: {nativeProbeError}");
                }

                nativeProbe.Dispose();
                nativeProbe = null;
            }

            Assert.IsTrue(
                NowFontCompiler.TryCompile(bytes, out font, out string error),
                error);

            if (legacyAbi)
            {
                Shader legacyShader = Shader.Find("Hidden/NowUI Tests/SDF ABI V1");
                Assert.NotNull(legacyShader, "The ABI-v1 SDF test shader was not found.");
                legacyMaterial = new Material(legacyShader);
            }

            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            commandBuffer = new CommandBuffer
            {
                name = "SDF large-outline text parity"
            };

            Color32[] Render(bool sdf, out float uploadedScreenRange)
            {
                NowSdf.Reset();
                font.ClearDynamicCache();

                using var drawList = new NowDrawList();
                using (drawList.Begin(new Vector2(Width, Height)))
                {
                    if (sdf)
                    {
                        NowSdfBuilder scene = NowSdf.Scene(
                                new NowRect(0f, 0f, Width, Height),
                                $"playmode-sdf-large-outline-parity-{nativeCompiler}-{legacyAbi}");

                        if (legacyAbi)
                            scene = scene.SetMaterial(legacyMaterial);

                        scene
                            .SetColor(Color.white)
                            .SetOutline(Outline, Color.white)
                            .Text(position, Value, font, FontSize)
                            .Draw();
                    }
                    else
                    {
                        Now.Text(new NowRect(position.x, position.y, 96f, 96f), font)
                            .SetFontSize(FontSize)
                            .SetColor(Color.white)
                            .SetOutlinePixels(Outline)
                            .SetOutlineColor(Color.white)
                            .Draw(Value);
                    }
                }

                uploadedScreenRange = 0f;

                if (sdf)
                {
                    for (int batchIndex = 0; batchIndex < drawList.batchCount; ++batchIndex)
                    {
                        Material material = drawList.batches[batchIndex].material;
                        int shapeCount = Mathf.RoundToInt(material.GetFloat("_SdfShapeCount"));
                        Vector4[] data0 = material.GetVectorArray("_SdfData0");
                        Vector4[] data2 = material.GetVectorArray("_SdfData2");

                        for (int shapeIndex = 0; shapeIndex < shapeCount; ++shapeIndex)
                        {
                            if (Mathf.Approximately(data0[shapeIndex].x, 5f))
                            {
                                uploadedScreenRange = Mathf.Max(
                                    uploadedScreenRange,
                                    data2[shapeIndex].x);
                            }
                        }
                    }
                }
                else
                {
                    var extras = new System.Collections.Generic.List<Vector4>();
                    drawList.mesh.GetUVs(5, extras);

                    for (int vertexIndex = 0; vertexIndex < extras.Count; ++vertexIndex)
                    {
                        uploadedScreenRange = Mathf.Max(
                            uploadedScreenRange,
                            Mathf.Abs(extras[vertexIndex].y));
                    }
                }

                Assert.Greater(
                    uploadedScreenRange,
                    0f,
                    "The parity fixture could not read the uploaded glyph range.");

                commandBuffer.Clear();
                NowRenderer.PopulateCommandBuffer(
                    commandBuffer,
                    drawList,
                    target,
                    true,
                    Color.clear);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return ReadPixels(target);
            }

            Color32[] direct = Render(sdf: false, out float directScreenRange);
            Color32[] sdf = Render(sdf: true, out float sdfScreenRange);
            var sdfCoverage = new Color32[sdf.Length];
            int directOpaqueCoverage = 0;
            int sdfOpaqueCoverage = 0;
            int intersection = 0;
            int union = 0;

            for (int i = 0; i < direct.Length; ++i)
            {
                // The SDF material uses straight-alpha blending, so target alpha
                // is squared on a transparent RT. Its white RGB channel retains
                // the actual shader coverage and is the parity signal.
                sdfCoverage[i] = new Color32(255, 255, 255, sdf[i].r);
                bool directOpaque = direct[i].a > 128;
                bool sdfOpaque = sdf[i].r > 128;

                if (directOpaque) ++directOpaqueCoverage;
                if (sdfOpaque) ++sdfOpaqueCoverage;
                if (directOpaque && sdfOpaque) ++intersection;
                if (directOpaque || sdfOpaque) ++union;
            }

            RectInt directBounds = FindRawAlphaBounds(direct, Width, Height, 128);
            RectInt sdfBounds = FindRawAlphaBounds(sdfCoverage, Width, Height, 128);
            float intersectionOverUnion = union > 0 ? (float)intersection / union : 1f;

            Assert.AreEqual(
                directScreenRange,
                sdfScreenRange,
                0.001f,
                "Now.Text and NowSdf did not bind the same distance-field range.");
            Assert.Greater(directOpaqueCoverage, 8000, "The ordinary text fixture rendered too little ink.");
            Assert.Greater(sdfOpaqueCoverage, 8000, "The SDF text fixture rendered too little ink.");
            string boundsMessage = $"Ordinary bounds {directBounds}; SDF bounds {sdfBounds}.";
            Assert.That(
                Mathf.Abs(sdfBounds.xMin - directBounds.xMin),
                Is.LessThanOrEqualTo(1),
                boundsMessage);
            Assert.That(
                Mathf.Abs(sdfBounds.yMin - directBounds.yMin),
                Is.LessThanOrEqualTo(1),
                boundsMessage);
            Assert.That(
                Mathf.Abs(sdfBounds.width - directBounds.width),
                Is.LessThanOrEqualTo(2),
                boundsMessage);
            Assert.That(
                Mathf.Abs(sdfBounds.height - directBounds.height),
                Is.LessThanOrEqualTo(2),
                boundsMessage);
            Assert.That(
                Mathf.Abs(sdfOpaqueCoverage - directOpaqueCoverage),
                Is.LessThanOrEqualTo(96),
                "The SDF outline changed the opaque glyph area materially.");
            Assert.GreaterOrEqual(
                intersectionOverUnion,
                0.99f,
                $"The SDF outline geometry diverged from ordinary text (IoU {intersectionOverUnion:F6}).");
        }
        finally
        {
            nativeProbe?.Dispose();
            NowFontCompiler.forceManagedCompiler = false;
            NowFontCompiler.forceNativeCompiler = false;
            commandBuffer?.Release();

            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }

            if (font != null)
            {
                font.ClearDynamicCache();
                Object.DestroyImmediate(font);
            }

            if (legacyMaterial != null)
                Object.DestroyImmediate(legacyMaterial);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfRotateNextKeepsTextureInPrimitiveLocalSpace(bool circle)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            var red = new Color32(255, 0, 0, 255);
            var blue = new Color32(0, 0, 255, 255);
            texture.SetPixels32(new[] { red, blue, red, blue });
            texture.Apply(false, false);

            var surface = new NowRect(0f, 0f, Side, Side);
            using (_renderer.Begin(_target))
            {
                var scene = NowSdf.Scene(surface, "playmode-sdf-rotated-primitive-texture")
                    .SetTexture(texture)
                    .RotateNext(90f);
                scene = circle
                    ? scene.Circle(new Vector2(64f, 64f), 42f)
                    : scene.ChamferedBox(new NowRect(24f, 48f, 80f, 32f), 6f);
                scene.Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            Color32 top = PixelAtUi(pixels, 64, 32);
            Color32 bottom = PixelAtUi(pixels, 64, 96);
            string primitive = circle ? "Circle" : "ChamferedBox";

            Assert.Greater(top.r, 220, $"The rotated {primitive} local-left side lost the red texel: {top}.");
            Assert.Less(top.b, 16, $"The rotated {primitive} local-left side sampled blue: {top}.");
            Assert.Greater(bottom.b, 220, $"The rotated {primitive} local-right side lost the blue texel: {bottom}.");
            Assert.Less(bottom.r, 16, $"The rotated {primitive} local-right side sampled red: {bottom}.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SdfLineMatchesCapsuleWithHalfItsWidthAsRadius()
    {
        var line = RenderSdfLine(useCapsule: false);
        var capsule = RenderSdfLine(useCapsule: true);

        CollectionAssert.AreEqual(capsule, line,
            "Line(from, to, width) must render exactly like Capsule(from, to, width / 2).");
        Assert.Greater(PixelAtUi(line, 64, 64).a, 240, "Line lost its center stroke coverage.");
        Assert.Less(PixelAtUi(line, 64, 42).a, 16, "Line coverage exceeded its full-width contract.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SdfPlanarPrimitivesSampleTextureAcrossConservativeBounds(bool triangle)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        try
        {
            var red = new Color32(255, 0, 0, 255);
            var blue = new Color32(0, 0, 255, 255);
            texture.SetPixels32(new[] { red, blue, red, blue });
            texture.Apply(false, false);

            var surface = new NowRect(0f, 0f, Side, Side);
            using (_renderer.Begin(_target))
            {
                var scene = NowSdf.Scene(surface, "playmode-sdf-planar-texture")
                    .SetTexture(texture);
                scene = triangle
                    ? scene.Triangle(new Vector2(20f, 100f), new Vector2(108f, 100f), new Vector2(64f, 20f))
                    : scene.ChamferedBox(new NowRect(20f, 28f, 88f, 72f), 12f);
                scene.Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            Color32 left = PixelAtUi(pixels, 40, 72);
            Color32 right = PixelAtUi(pixels, 88, 72);

            Assert.Greater(left.r, 220, $"The {(triangle ? "Triangle" : "ChamferedBox")} left side lost the texture's red texel: {left}.");
            Assert.Less(left.b, 16, $"The left side sampled the texture's blue texel: {left}.");
            Assert.Greater(right.b, 220, $"The {(triangle ? "Triangle" : "ChamferedBox")} right side lost the texture's blue texel: {right}.");
            Assert.Less(right.r, 16, $"The right side sampled the texture's red texel: {right}.");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void DownsampledSdfMaskStillMapsAcrossFullAuthoredRect()
    {
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (NowSdf.Scene(surface, "playmode-sdf-quarter-resolution-mask")
            .SetMaskResolutionScale(0.25f)
            .Box(new NowRect(16f, 24f, 96f, 80f))
            .BeginMask())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        Assert.Greater(PixelAtUi(pixels, 32, 40).a, 245, "The downsampled mask lost its upper-left interior.");
        Assert.Greater(PixelAtUi(pixels, 96, 88).a, 245, "The downsampled mask lost its lower-right interior.");
        Assert.Less(PixelAtUi(pixels, 8, 64).a, 8, "The downsampled mask leaked left of its authored field.");
        Assert.Less(PixelAtUi(pixels, 120, 64).a, 8, "The downsampled mask leaked right of its authored field.");
        Assert.Less(PixelAtUi(pixels, 64, 8).a, 8, "The downsampled mask leaked above its authored field.");
        Assert.Less(PixelAtUi(pixels, 64, 116).a, 8, "The downsampled mask leaked below its authored field.");
    }

    [Test]
    public void SdfMaskFeatherWidensTransitionWithoutSquaringCoverage()
    {
        var analytic = RenderWhiteCircleMask(feather: 6f);
        var crisp = RenderWhiteSdfCircleMask(feather: 0f, "playmode-sdf-crisp-mask");
        var soft = RenderWhiteSdfCircleMask(feather: 6f, "playmode-sdf-soft-mask");

        int crispPartial = CountMaskTransitionPixels(crisp);
        int softPartial = CountMaskTransitionPixels(soft);
        long analyticTransitionAlpha = 0;
        long sdfTransitionAlpha = 0;
        int comparableSamples = 0;

        for (int i = 0; i < analytic.Length; ++i)
        {
            byte alpha = analytic[i].a;
            if (alpha < 24 || alpha > 231)
                continue;

            analyticTransitionAlpha += alpha;
            sdfTransitionAlpha += soft[i].a;
            ++comparableSamples;
        }

        Assert.Greater(crispPartial, 40, "The default SDF mask AA ramp was missing.");
        Assert.Greater(
            softPartial,
            crispPartial * 2,
            $"Six SDF feather pixels did not widen the texture-mask transition ({crispPartial} -> {softPartial}).");
        Assert.Greater(comparableSamples, 40, "The analytic reference had too few transition samples.");
        Assert.Greater(
            sdfTransitionAlpha,
            analyticTransitionAlpha * 0.78f,
            "The SDF mask capture darkened its transition as if coverage alpha had been multiplied by itself.");
    }

    [Test]
    public void SdfMaskClipsOrdinaryTextMaterial()
    {
        var font = ResolveDefaultNowFont();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (NowSdf.Scene(surface, "playmode-sdf-text-mask")
            .Box(new NowRect(48f, 0f, 32f, Side))
            .BeginMask())
        {
            Now.Text(new NowRect(0f, 24f, Side, 80f), font)
                .SetFontSize(56f)
                .SetColor(Color.white)
                .Draw("MMMM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        int insideInk = 0;
        int leakedInk = 0;

        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                if (pixels[y * Side + x].a <= 12)
                    continue;

                if (x >= 48 && x < 80)
                    ++insideInk;
                else if (x < 45 || x >= 83)
                    ++leakedInk;
            }
        }

        Assert.Greater(insideInk, 80, "Text produced no visible ink inside the SDF mask.");
        Assert.AreEqual(0, leakedInk, "The ordinary text shader leaked beyond the SDF coverage texture.");
    }

    [Test]
    public void SdfCircleMaskClipsOversizedGlowFromProgressBar()
    {
        var surface = new NowRect(0f, 0f, Side, Side);
        var bar = new NowRect(14f, 56f, 100f, 16f);
        var fill = new NowRect(bar.x, bar.y, bar.width * 0.74f, bar.height);
        var cyan = new Color(0.08f, 0.86f, 1f, 1f);

        Color32[] RenderProgress(bool clipToCircle)
        {
            using (_renderer.Begin(_target))
            {
                if (clipToCircle)
                {
                    using (NowSdf.Scene(surface, "playmode-sdf-progress-parent")
                        .SetColor(Color.white)
                        .Circle(surface.center, 46f)
                        .BeginMask())
                    {
                        DrawProgress();
                    }
                }
                else
                {
                    DrawProgress();
                }
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            return ReadPixels(_target);
        }

        void DrawProgress()
        {
            Now.Rectangle(bar)
                .SetColor(new Color(0.08f, 0.12f, 0.20f, 0.82f))
                .SetRadius(bar.height * 0.5f)
                .Draw();
            NowSdf.Scene(surface, "playmode-sdf-progress-halo")
                .SetColor(cyan)
                .SetGlow(28f, new Color(0.02f, 0.78f, 1f, 0.90f), 1.1f)
                .RoundedBox(fill, fill.height * 0.5f)
                .Draw();
        }

        var unmasked = RenderProgress(clipToCircle: false);
        _renderer.Clear();
        var masked = RenderProgress(clipToCircle: true);
        Color32 unmaskedHalo = PixelAtUi(unmasked, 10, 64);
        Color32 clippedHalo = PixelAtUi(masked, 10, 64);
        Color32 filledCore = PixelAtUi(masked, 64, 64);

        Assert.Greater(
            unmaskedHalo.a,
            80,
            $"The reference progress halo did not reach beyond the parent circle: {unmaskedHalo}.");
        Assert.Less(
            clippedHalo.a,
            8,
            $"The glowing progress bar leaked outside its SDF circle parent: {clippedHalo}.");
        Assert.Greater(
            filledCore.a,
            240,
            $"The SDF circle mask removed the progress fill inside its parent: {filledCore}.");
    }

    [Test]
    public void TwoSdfMasksIntersectWithAnalyticAndHardMasks()
    {
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (Now.Mask(new NowRect(32f, 0f, 80f, Side)))
        using (Now.Mask(NowMaskShape.Circle(surface.center, 40f)))
        using (NowSdf.Scene(surface, "playmode-sdf-left-mask")
            .Box(new NowRect(0f, 0f, 72f, Side))
            .BeginMask())
        using (NowSdf.Scene(surface, "playmode-sdf-bottom-mask")
            .Box(new NowRect(0f, 56f, Side, 72f))
            .BeginMask())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        Assert.Greater(PixelAtUi(pixels, 60, 64).a, 240, "The four-way mask intersection lost its opaque interior.");
        Assert.Less(PixelAtUi(pixels, 84, 64).a, 8, "The first SDF mask did not reject its outside region.");
        Assert.Less(PixelAtUi(pixels, 60, 40).a, 8, "The second SDF mask did not reject its outside region.");
        Assert.Less(PixelAtUi(pixels, 40, 100).a, 8, "The analytic mask did not intersect the SDF masks.");
        Assert.Less(PixelAtUi(pixels, 30, 64).a, 8, "The hard rectangle did not intersect the SDF masks.");
    }

    [Test]
    public void SiblingSdfMasksKeepCoverageTexturesIsolated()
    {
        var surface = new NowRect(0f, 0f, Side, Side);
        var leftCenter = new Vector2(32f, Side * 0.5f);
        var rightCenter = new Vector2(Side - 32f, Side * 0.5f);

        using (_renderer.Begin(_target))
        {
            using (NowSdf.Scene(surface, "playmode-sdf-sibling-left")
                .Circle(leftCenter, 18f)
                .BeginMask())
            {
                Now.Rectangle(surface)
                    .SetColor(Color.red)
                    .Draw();
            }

            using (NowSdf.Scene(surface, "playmode-sdf-sibling-right")
                .Circle(rightCenter, 18f)
                .BeginMask())
            {
                Now.Rectangle(surface)
                    .SetColor(Color.blue)
                    .Draw();
            }
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        Color32 left = PixelAtUi(pixels, (int)leftCenter.x, (int)leftCenter.y);
        Color32 right = PixelAtUi(pixels, (int)rightCenter.x, (int)rightCenter.y);
        Color32 gap = PixelAtUi(pixels, Side / 2, Side / 2);

        Assert.Greater(left.r, 240, $"The first SDF batch lost its red coverage texture: {left}.");
        Assert.Less(left.b, 12, $"The first SDF batch reused its blue sibling's state: {left}.");
        Assert.Greater(right.b, 240, $"The second SDF batch lost its blue coverage texture: {right}.");
        Assert.Less(right.r, 12, $"The second SDF batch reused its red sibling's state: {right}.");
        Assert.Less(gap.a, 8, $"Sibling SDF masks leaked into their disjoint gap: {gap}.");
    }

    [Test]
    public void SdfMaskPreservesMirroredTransformMapping()
    {
        var localSurface = new NowRect(0f, 0f, 40f, 40f);
        var scale = new Vector2(-2f, 1.5f);
        var origin = new Vector2(100f, 20f);
        NowMaskScope mask = default;

        using (_renderer.Begin(_target))
        {
            var transform = Now.Transform(scale, origin);
            NowRect screenSurface;

            try
            {
                screenSurface = Now.TransformScreenRect(localSurface);
                mask = NowSdf.Scene(localSurface, "playmode-sdf-mirrored-mask")
                    .Circle(new Vector2(10f, 20f), 8f)
                    .BeginMask();
            }
            finally
            {
                // Keep the captured mask active but draw an ordinary screen-space
                // quad. This isolates signed texture-mask sampling from the older
                // mirrored-quad emission path used by drawing primitives.
                transform.Dispose();
            }

            using (mask)
            {
                Now.Rectangle(screenSurface)
                    .SetColor(Color.white)
                    .Draw();
            }
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        RectInt alphaBounds = FindAlphaBounds(pixels);

        Assert.Greater(
            PixelAtUi(pixels, 80, 50).a,
            240,
            $"The mirrored SDF mask lost its transformed center; visible UI bounds were {alphaBounds}.");
        Assert.Less(PixelAtUi(pixels, 40, 50).a, 8, "The SDF mask ignored the sign of its captured horizontal transform.");
        Assert.Less(PixelAtUi(pixels, 80, 20).a, 8, "The SDF mask ignored its nonuniform vertical scale.");
    }

    [Test]
    public void RenderToTextureModifierDoesNotApplySdfMaskTwice()
    {
        const float feather = 6f;
        var direct = RenderWhiteSdfCircleMask(feather, "playmode-sdf-direct-replay-reference");
        _renderer.Clear();
        var replayed = RenderWhiteSdfCircleMaskThroughTextureModifier(
            feather,
            "playmode-sdf-texture-modifier-mask");
        long directTransitionAlpha = 0;
        long replayedTransitionAlpha = 0;
        int transitionPixels = 0;

        for (int i = 0; i < direct.Length; ++i)
        {
            byte directAlpha = direct[i].a;
            if (directAlpha < 24 || directAlpha > 231)
                continue;

            directTransitionAlpha += directAlpha;
            replayedTransitionAlpha += replayed[i].a;
            ++transitionPixels;
        }

        Assert.Greater(transitionPixels, 40, "The direct SDF mask had no stable feather samples to compare.");
        Assert.Greater(
            replayedTransitionAlpha,
            directTransitionAlpha * 0.82f,
            "Render-to-texture replay reduced SDF coverage as if the mask was multiplied a second time.");
        Assert.Greater(PixelAtUi(replayed, Side / 2, Side / 2).a, 245, "Modifier replay lost the SDF mask center.");
        Assert.Less(replayed[4 * Side + 4].a, 5, "Modifier capture failed to bake the SDF mask.");
    }

    Color32[] RenderSdfRadial(bool arc, float sweep)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-radial")
                .SetColor(Color.white)
                .SetFeather(0f);
            scene = arc
                ? scene.Arc(surface.center, 36f, 10f, 0f, sweep)
                : scene.Pie(surface.center, 36f, 0f, sweep);
            scene.Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfTriangle(bool reverseWinding)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);
        var a = new Vector2(20f, 100f);
        var b = new Vector2(108f, 100f);
        var c = new Vector2(64f, 20f);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-triangle-winding")
                .SetColor(Color.white)
                .SetFeather(0f);
            scene = reverseWinding ? scene.Triangle(a, c, b) : scene.Triangle(a, b, c);
            scene.Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfDegenerateTriangle()
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        {
            NowSdf.Scene(surface, "playmode-sdf-degenerate-triangle")
                .SetColor(Color.white)
                .SetFeather(0f)
                .SetOutline(2f, Color.white)
                .Triangle(
                    new Vector2(20f, 64f),
                    new Vector2(64f, 64f),
                    new Vector2(108f, 64f))
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfChamferedBox(float chamfer, bool useBox)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);
        var box = new NowRect(20f, 24f, 88f, 80f);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-chamfered-box")
                .SetColor(Color.white)
                .SetFeather(0f);
            scene = useBox ? scene.Box(box) : scene.ChamferedBox(box, chamfer);
            scene.Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfBoxQuarterTurn(bool rotated)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-box-quarter-turn")
                .SetColor(Color.white)
                .SetFeather(0f);
            scene = rotated
                ? scene.RotateNext(90f).Box(new NowRect(20f, 40f, 88f, 48f))
                : scene.Box(new NowRect(40f, 20f, 48f, 88f));
            scene.Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfRotatedBox(float angleDegrees)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        {
            NowSdf.Scene(surface, "playmode-sdf-box-angle")
                .SetColor(Color.white)
                .SetFeather(0f)
                .RotateNext(angleDegrees)
                .Box(new NowRect(28f, 54f, 72f, 20f))
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderSdfTextRun(
        NowFontAsset font,
        Vector2 position,
        string value,
        float fontSize,
        bool rotate)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-text-run-rotation")
                .SetColor(Color.white)
                .SetFeather(0f);

            if (rotate)
                scene = scene.RotateNext(90f);

            scene.Text(position, value, font, fontSize).Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderTransformedSdfCircle(
        NowRect surface,
        Vector2 center,
        float radius,
        Vector2 scale,
        Vector2 origin,
        NowId id)
    {
        _renderer.Clear();

        using (_renderer.Begin(_target))
        using (Now.Transform(scale, origin))
        {
            NowSdf.Scene(surface, id)
                .SetColor(Color.white)
                .SetFeather(0f)
                .Circle(center, radius)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    static Vector2 SdfTextRunPivot(
        NowFontAsset font,
        Vector2 position,
        string value,
        float fontSize,
        out int glyphCount)
    {
        font.EnsureGlyphs(value, fontSize);

        float baseline = font.GetAscender() * fontSize;
        float lineHeight = font.GetLineHeight() * fontSize;
        float left = position.x;
        float x = position.x;
        float y = position.y;
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        Texture atlas = null;
        glyphCount = 0;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            if (codepoint == '\n')
            {
                x = left;
                y += lineHeight;
                continue;
            }

            if (codepoint == '\t')
            {
                if (font.TryResolveGlyph(' ', fontSize, NowFontStyle.Regular, out _, out var space, out _))
                    x += space.advance * fontSize * 4f;

                continue;
            }

            if (!font.TryResolveGlyph(
                    codepoint,
                    fontSize,
                    NowFontStyle.Regular,
                    out var resolvedFont,
                    out var glyph,
                    out var material))
            {
                continue;
            }

            Texture glyphAtlas = material != null ? material.mainTexture : null;
            bool canEmit = resolvedFont != null &&
                !resolvedFont.isColor &&
                !Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right) &&
                glyphAtlas != null &&
                (atlas == null || ReferenceEquals(atlas, glyphAtlas));

            if (canEmit)
            {
                atlas ??= glyphAtlas;
                float glyphLeft = glyph.planeBounds.left * fontSize;
                float glyphRight = glyph.planeBounds.right * fontSize;
                float glyphBottom = glyph.planeBounds.bottom * fontSize;
                float glyphTop = glyph.planeBounds.top * fontSize;
                var rect = new NowRect(
                    x + glyphLeft,
                    y + baseline - glyphTop,
                    glyphRight - glyphLeft,
                    glyphTop - glyphBottom);
                float centerX = rect.x + rect.width * 0.5f;
                float centerY = rect.y + rect.height * 0.5f;
                double halfWidth = System.Math.Max((double)rect.width * 0.5d, 0.0001d);
                double halfHeight = System.Math.Max((double)rect.height * 0.5d, 0.0001d);
                minX = System.Math.Min(minX, centerX - halfWidth);
                minY = System.Math.Min(minY, centerY - halfHeight);
                maxX = System.Math.Max(maxX, centerX + halfWidth);
                maxY = System.Math.Max(maxY, centerY + halfHeight);
                ++glyphCount;
            }

            x += glyph.advance * fontSize;
        }

        Assert.Greater(glyphCount, 0, "The SDF text fixture emitted no glyphs.");
        return new Vector2(
            (float)(minX * 0.5d + maxX * 0.5d),
            (float)(minY * 0.5d + maxY * 0.5d));
    }

    static Color32[] RotateAlphaQuarterTurnClockwise(Color32[] source, Vector2 pivot)
    {
        var rotated = new Color32[source.Length];

        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                // Inverse-map the destination pixel center through a clockwise
                // quarter turn in NowUI's top-left-origin coordinate system.
                float sourceCenterX = pivot.x + (y + 0.5f - pivot.y);
                float sourceCenterY = pivot.y - (x + 0.5f - pivot.x);
                int sourceX = Mathf.RoundToInt(sourceCenterX - 0.5f);
                int sourceY = Mathf.RoundToInt(sourceCenterY - 0.5f);

                if (sourceX < 0 || sourceX >= Side || sourceY < 0 || sourceY >= Side)
                    continue;

                rotated[(Side - 1 - y) * Side + x] = PixelAtUi(source, sourceX, sourceY);
            }
        }

        return rotated;
    }

    Color32[] RenderSdfLine(bool useCapsule)
    {
        _renderer.Clear();
        var surface = new NowRect(0f, 0f, Side, Side);
        var from = new Vector2(28f, 58f);
        var to = new Vector2(100f, 70f);

        using (_renderer.Begin(_target))
        {
            var scene = NowSdf.Scene(surface, "playmode-sdf-line-alias")
                .SetColor(Color.white)
                .SetFeather(0f);
            scene = useCapsule ? scene.Capsule(from, to, 8f) : scene.Line(from, to, 16f);
            scene.Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderWhiteCircleMask(float feather)
    {
        using (_renderer.Begin(_target))
        using (Now.Mask(NowMaskShape.Circle(new Vector2(Side * 0.5f, Side * 0.5f), 32f).SetFeather(feather)))
        {
            Now.Rectangle(new NowRect(0f, 0f, Side, Side))
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderWhiteCircleMaskThroughTextureModifier(float feather)
    {
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (Now.Mask(NowMaskShape.Circle(surface.center, 32f).SetFeather(feather)))
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
            .SetRenderToTexture()
            .SetSourceRect(surface)
            .Begin())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderWhiteSdfCircleMask(float feather, NowId id)
    {
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (NowSdf.Scene(surface, id)
            .SetFeather(feather)
            .Circle(surface.center, 32f)
            .BeginMask())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderWhiteSdfCircleMaskThroughTextureModifier(float feather, NowId id)
    {
        var surface = new NowRect(0f, 0f, Side, Side);

        using (_renderer.Begin(_target))
        using (NowSdf.Scene(surface, id)
            .SetFeather(feather)
            .Circle(surface.center, 32f)
            .BeginMask())
        using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
            .SetId("playmode-sdf-mask-texture-modifier")
            .SetRenderToTexture()
            .SetSourceRect(surface)
            .Begin())
        {
            Now.Rectangle(surface)
                .SetColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    static int CountMaskTransitionPixels(Color32[] pixels)
    {
        int count = 0;

        // Ignore the render-target boundary so only the centered circle's edge
        // contributes partially covered pixels.
        for (int y = 8; y < Side - 8; ++y)
        {
            for (int x = 8; x < Side - 8; ++x)
            {
                byte alpha = pixels[y * Side + x].a;
                if (alpha >= 8 && alpha <= 247)
                    ++count;
            }
        }

        return count;
    }

    [Test]
    public void PolylineTurnRendersWithoutAnOuterJoinGap()
    {
        var points = new[]
        {
            new Vector2(16f, 80f),
            new Vector2(64f, 32f),
            new Vector2(112f, 80f)
        };

        using (_renderer.Begin(_target))
            Now.DrawPolyline(points, 8f, NowLineCap.Butt, Color.white);

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        // RenderTexture orientation differs between graphics APIs. Sample both
        // possible rows at the outside of the V's shared miter; the opposite row
        // is empty for this path. Independent butt-capped segments leave both
        // candidates transparent here.
        int uiY = 28;
        int flippedY = Side - 1 - uiY;
        byte joinAlpha = 0;

        for (int x = 63; x <= 65; ++x)
        {
            joinAlpha = System.Math.Max(joinAlpha, pixels[uiY * Side + x].a);
            joinAlpha = System.Math.Max(joinAlpha, pixels[flippedY * Side + x].a);
        }

        Assert.Greater(joinAlpha, 180,
            "The connected stroke did not cover the outside of its shared turn join.");
    }

    [Test]
    public void AuthoredUiColorsRenderConsistentlyAcrossProjectColorSpaces()
    {
        var authoredSrgb = new Color(15f / 255f, 23f / 255f, 42f / 255f, 1f);

        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(16f, 16f, 96f, 96f))
                .SetColor(authoredSrgb)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        Color32 sample = ReadPixels(_target)[64 * Side + 64];

        Assert.AreEqual(15, sample.r, 3, $"Authored sRGB red changed in {QualitySettings.activeColorSpace} space.");
        Assert.AreEqual(23, sample.g, 3, $"Authored sRGB green changed in {QualitySettings.activeColorSpace} space.");
        Assert.AreEqual(42, sample.b, 3, $"Authored sRGB blue changed in {QualitySettings.activeColorSpace} space.");
        Assert.GreaterOrEqual(sample.a, 250);
    }

    [Test]
    public void GradientKindsRenderExpectedColorRegions()
    {
        var rect = new NowRect(16f, 16f, 96f, 96f);

        using (_renderer.Begin(_target))
        {
            Now.Gradient(rect, Color.red, Color.blue)
                .SetLinear(90f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        Color32 linearLeft = pixels[64 * Side + 24];
        Color32 linearRight = pixels[64 * Side + 104];
        Assert.Greater(linearLeft.r, linearLeft.b + 80, $"Linear start was {linearLeft}.");
        Assert.Greater(linearRight.b, linearRight.r + 80, $"Linear end was {linearRight}.");

        _renderer.Clear();
        using (_renderer.Begin(_target))
        {
            Now.Gradient(rect, Color.white, Color.black)
                .SetRadial(NowGradientShape.Circle)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        pixels = ReadPixels(_target);
        Color32 radialCenter = pixels[64 * Side + 64];
        Color32 radialEdge = pixels[64 * Side + 18];
        Assert.Greater(radialCenter.r, radialEdge.r + 100,
            $"Radial center {radialCenter} was not brighter than edge {radialEdge}.");

        _renderer.Clear();
        using (_renderer.Begin(_target))
        {
            Now.Gradient(rect, Color.red, Color.blue)
                .SetConic()
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        pixels = ReadPixels(_target);
        Color32 conicTop = pixels[104 * Side + 64];
        Color32 conicBottom = pixels[24 * Side + 64];
        Assert.Greater(
            Mathf.Abs(conicTop.r - conicBottom.r) + Mathf.Abs(conicTop.b - conicBottom.b),
            100,
            $"Conic samples did not vary: {conicTop} and {conicBottom}.");

        _renderer.Clear();
        using (_renderer.Begin(_target))
        {
            Now.Gradient(rect, Color.clear, Color.clear)
                .SetTint(Color.clear)
                .SetRadius(10f)
                .SetOutline(6f, Color.green)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        pixels = ReadPixels(_target);
        Color32 outlineEdge = pixels[64 * Side + 18];
        Color32 outlineCenter = pixels[64 * Side + 64];
        Assert.Greater(outlineEdge.g, 180, $"Gradient outline edge was {outlineEdge}.");
        Assert.Less(outlineCenter.a, 20, $"Transparent gradient center was {outlineCenter}.");
    }

    [Test]
    public void TextGradientRendersAcrossGlyphsWithoutMaskComposition()
    {
        var font = ResolveDefaultNowFont();

        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(4f, 30f, 120f, 64f), font)
                .SetFontSize(42f)
                .SetGradient(Color.red, Color.blue)
                .SetGradientLinear(90f)
                .Draw("MMMM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        int redInk = 0;
        int blueInk = 0;

        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                Color32 pixel = PixelAtUi(pixels, x, y);

                if (pixel.a < 40)
                    continue;

                if (x < Side / 2 && pixel.r > pixel.b + 24)
                    ++redInk;
                else if (x >= Side / 2 && pixel.b > pixel.r + 24)
                    ++blueInk;
            }
        }

        Assert.Greater(redInk, 80, "The left glyphs did not sample the red side of the text gradient.");
        Assert.Greater(blueInk, 80, "The right glyphs did not sample the blue side of the text gradient.");
    }

    [Test]
    public void AnalyticCapsuleMaskClipsGradientMaterial()
    {
        using (_renderer.Begin(_target))
        using (Now.Mask(NowMaskShape.Capsule(
            new Vector2(30f, Side * 0.5f),
            new Vector2(Side - 30f, Side * 0.5f),
            18f).SetFeather(2f)))
        {
            Now.Gradient(new NowRect(0f, 0f, Side, Side), Color.red, Color.blue)
                .SetLinear(90f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        Assert.Greater(pixels[(Side / 2) * Side + Side / 2].a, 240, "Gradient vanished inside the capsule.");
        Assert.Less(pixels[12 * Side + Side / 2].a, 8, "Gradient leaked outside the capsule.");
        Assert.Greater(CountMaskTransitionPixels(pixels), 40, "Gradient shader did not preserve soft mask coverage.");
    }

    [Test]
    public void PremultipliedTextureAvoidsMultiplyingAlphaTwice()
    {
        var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);

        try
        {
            source.SetPixel(0, 0, new Color(0.5f, 0f, 0f, 0.5f));
            source.Apply(false, false);

            using (_renderer.Begin(_target))
            {
                Now.Rectangle(new NowRect(8f, 32f, 48f, 64f))
                    .SetTexture(source, premultipliedAlpha: false)
                    .Draw();
                Now.Rectangle(new NowRect(72f, 32f, 48f, 64f))
                    .SetTexture(source, premultipliedAlpha: true)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            Color32 straight = pixels[64 * Side + 32];
            Color32 premultiplied = pixels[64 * Side + 96];

            Assert.AreEqual(straight.a, premultiplied.a, 4);
            Assert.Greater(
                premultiplied.r,
                straight.r + 30,
                $"PMA branch did not preserve RGB (straight {straight}, PMA {premultiplied}).");
        }
        finally
        {
            Now.ReleaseTextureMaterials(source);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewRendersThroughTextureBackedEffect()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var preview = new NowModelPreview(source)
                .SetFixedResolution(64, 64);

            Assert.IsTrue(preview.RenderNow(), "The preview camera request did not render.");
            yield return null;

            using (_renderer.Begin(_target))
                Now.Model(new NowRect(32f, 32f, 64f, 64f), preview).Draw();

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            yield return null;

            var direct = ReadPixels(_target);
            Assert.Greater(direct[64 * Side + 64].a, 150);
            Assert.Greater(
                CountPixels(direct, pixel => pixel.a < 20),
                Side * Side / 2,
                $"Direct model draw did not preserve transparency; corners were " +
                $"{direct[0]}, {direct[Side - 1]}, {direct[(Side - 1) * Side]}, {direct[^1]}.");
            _renderer.Clear();

            using (_renderer.Begin(_target))
            {
                using (NowEffects.Modifier(NowDeformers.Wave(0f, 0f, 32f))
                    .SetRenderToTexture()
                    .SetSourceRect(new NowRect(32f, 32f, 64f, 64f))
                    .Begin())
                {
                    Now.Model(new NowRect(32f, 32f, 64f, 64f), preview).Draw();
                }
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            yield return null;

            var output = ReadPixels(_target);
            Assert.Greater(output[64 * Side + 64].a, 150);
            Assert.Greater(CountPixels(output, pixel => pixel.a < 20), Side * Side / 2);
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

#if NOWUI_ANIMATION && NOWUI_PARTICLE_SYSTEM && NOWUI_PHYSICS2D && NOWUI_PHYSICS
    [UnityTest]
    public IEnumerator IsolatedRenderMeshMatchesCloneBaselineCoverage()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.transform.localPosition = new Vector3(3f, -2f, 5f);
        source.transform.localRotation = Quaternion.Euler(17f, 31f, -8f);
        source.transform.localScale = new Vector3(2.5f, 0.8f, 1.4f);

        try
        {
            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);

            Assert.IsTrue(clone.RenderNow(), "The clone preview did not render.");
            Assert.IsTrue(raw.RenderNow(), "The RenderMesh preview did not render.");
            yield return null;

            var clonePixels = ReadPixels(clone.texture);
            var rawPixels = ReadPixels(raw.texture);
            int cloneCoverage = CountPixels(clonePixels, pixel => pixel.a > 32);
            int rawCoverage = CountPixels(rawPixels, pixel => pixel.a > 32);
            Color32 cloneCenter = clonePixels[48 * 96 + 48];
            Color32 rawCenter = rawPixels[48 * 96 + 48];

            Assert.Greater(rawCoverage, 500, "RenderMesh produced no useful model coverage.");
            Assert.AreEqual(cloneCoverage, rawCoverage, 96,
                "RenderMesh and clone framing diverged by more than about one scanline.");
            Assert.AreEqual(cloneCenter.r, rawCenter.r, 12);
            Assert.AreEqual(cloneCenter.g, rawCenter.g, 12);
            Assert.AreEqual(cloneCenter.b, rawCenter.b, 12);
            Assert.AreEqual(cloneCenter.a, rawCenter.a, 4);
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }
#endif

    [UnityTest]
    public IEnumerator IsolatedRenderMeshPreservesIndexedMaterialPropertyBlock()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = null;

        try
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.NotNull(shader);
            material = new Material(shader) { color = Color.white };
            var renderer = source.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", new Color(0.9f, 0.1f, 0.05f, 1f));
            renderer.SetPropertyBlock(properties, 0);

            using var preview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black);

            Assert.IsTrue(preview.RenderNow(), "The RenderMesh preview did not render.");
            yield return null;

            Color32 center = ReadPixels(preview.texture)[32 * 64 + 32];
            Assert.Greater(center.r, center.g + 80,
                $"The cached indexed property block did not tint the preview red: {center}.");
            Assert.Greater(center.r, center.b + 80,
                $"The cached indexed property block did not tint the preview red: {center}.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }
    }

    [UnityTest]
    public IEnumerator IsolatedRenderMeshSubmissionIsRestrictedToItsPreviewCamera()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cameraObject = new GameObject("RenderMesh Leak Probe Camera");
        var gameTarget = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            gameTarget.Create();

            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(raw.RenderNow(), "The RenderMesh preview did not render.");

            var gameCamera = cameraObject.AddComponent<Camera>();
            gameCamera.CopyFrom(raw.camera);
            gameCamera.cameraType = CameraType.Game;
            gameCamera.scene = default;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = Color.magenta;
            gameCamera.targetTexture = gameTarget;
            gameCamera.Render();
            yield return null;

            var pixels = ReadPixels(gameTarget);
            int changed = CountPixels(
                pixels,
                pixel => pixel.r < 240 || pixel.b < 240 || pixel.g > 20 || pixel.a < 240);

            Assert.Less(changed, 32,
                "A camera-targeted RenderMesh submission became visible to a gameplay camera.");
        }
        finally
        {
            var gameCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;

            if (gameCamera != null)
                gameCamera.targetTexture = null;

            gameTarget.Release();
            Object.DestroyImmediate(gameTarget);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator SceneObjectPreviewBorrowsInPlaceAndLeavesGameCameraCullingToCaller()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Caller-Owned Scene Preview";
        source.layer = 30;
        source.transform.position = new Vector3(123f, 7f, -41f);
        var sourceRenderer = source.GetComponent<Renderer>();
        var originalPosition = source.transform.position;
        var cameraObject = new GameObject("Scene Preview Culling Probe");
        var gameTarget = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            gameTarget.Create();

            using (var preview = NowModelPreview.FromSceneObject(source, 1 << 30)
                .SetFixedResolution(96, 96))
            {
                Assert.IsTrue(preview.RenderNow(), "The borrowed scene-object preview did not render.");
                yield return null;

                int previewCoverage = CountPixels(ReadPixels(preview.texture), pixel => pixel.a > 32);
                Assert.Greater(previewCoverage, 500, "The scene object produced no useful preview coverage.");
                Assert.AreEqual(originalPosition, source.transform.position);
                Assert.AreEqual(30, source.layer);
                Assert.IsFalse(sourceRenderer.forceRenderingOff);

                var gameCamera = cameraObject.AddComponent<Camera>();
                gameCamera.CopyFrom(preview.camera);
                gameCamera.cameraType = CameraType.Game;
                gameCamera.scene = default;
                gameCamera.cullingMask = 0;
                gameCamera.clearFlags = CameraClearFlags.SolidColor;
                gameCamera.backgroundColor = Color.magenta;
                gameCamera.targetTexture = gameTarget;
                gameCamera.Render();
                yield return null;

                var pixels = ReadPixels(gameTarget);
                int changed = CountPixels(
                    pixels,
                    pixel => pixel.r < 240 || pixel.b < 240 || pixel.g > 20 || pixel.a < 240);
                Assert.Less(changed, 32,
                    "The caller's game-camera culling mask should remain authoritative.");
            }

            Assert.IsTrue(source != null);
            Assert.AreEqual(originalPosition, source.transform.position);
            Assert.AreEqual(30, source.layer);
            Assert.IsFalse(sourceRenderer.forceRenderingOff);
        }
        finally
        {
            var gameCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;

            if (gameCamera != null)
                gameCamera.targetTexture = null;

            gameTarget.Release();
            Object.DestroyImmediate(gameTarget);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(source);
        }
    }

#if NOWUI_ANIMATION && NOWUI_PARTICLE_SYSTEM && NOWUI_PHYSICS2D && NOWUI_PHYSICS
    [UnityTest]
    public IEnumerator IsolatedSkinnedSnapshotMatchesCloneBaselineCoverage()
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        Assert.NotNull(shader);
        var material = new Material(shader) { color = Color.white };
        var source = CreateSkinnedCube(material, out var bone, out var mesh);

        try
        {
            bone.localRotation = Quaternion.Euler(0f, 0f, 18f);

            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var snapshot = new NowModelPreview(source)
                .SetFixedResolution(96, 96);

            Assert.AreEqual(1, snapshot.ownedBakedMeshCount);
            Assert.AreEqual(0, snapshot.unsupportedRendererCount);
            Assert.IsTrue(clone.RenderNow());
            Assert.IsTrue(snapshot.RenderNow());
            yield return null;

            int cloneCoverage = CountPixels(ReadPixels(clone.texture), pixel => pixel.a > 32);
            int snapshotCoverage = CountPixels(ReadPixels(snapshot.texture), pixel => pixel.a > 32);

            Assert.Greater(snapshotCoverage, 500, "The CPU-baked snapshot rendered no visible mesh.");
            Assert.AreEqual(cloneCoverage, snapshotCoverage, Mathf.CeilToInt(cloneCoverage * 0.15f),
                "The baked pose uses its exact mesh bounds while SkinnedMeshRenderer framing uses localBounds; " +
                "coverage should remain comparable even though those bounds are not pixel-identical.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewSourceModesRenderUnderInstalledUrp()
    {
        var assetType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var rendererDataType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.ScriptableRendererData, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);

        if (assetType == null || rendererDataType == null)
        {
            Assert.Ignore("URP is not installed in this validation project.");
            yield break;
        }

        var create = assetType.GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { rendererDataType },
            null);
        Assert.NotNull(create, "URP no longer exposes its runtime pipeline-asset factory.");

        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { null });
        GameObject source = null;
        Material material = null;

        try
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            yield return null;
            yield return null;
            Assert.AreSame(pipeline, GraphicsSettings.currentRenderPipeline);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.NotNull(shader, "URP Lit shader was not available after installing URP.");
            material = new Material(shader) { color = Color.white };
            source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.GetComponent<Renderer>().sharedMaterial = material;

            using var clone = new NowModelPreview(
                    source,
                    31,
                    NowModelPreviewBackend.RendererClone)
                .SetFixedResolution(96, 96);
            using var raw = new NowModelPreview(source)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(clone.RenderNow(), "URP rejected the clone preview request.");
            Assert.IsTrue(raw.RenderNow(), "URP rejected the RenderMesh preview request.");
            yield return null;

            int cloneCoverage = CountPixels(ReadPixels(clone.texture), pixel => pixel.a > 32);
            int rawCoverage = CountPixels(ReadPixels(raw.texture), pixel => pixel.a > 32);
            Assert.Greater(rawCoverage, 500, "URP rendered no camera-targeted mesh coverage.");
            Assert.AreEqual(cloneCoverage, rawCoverage, 128,
                "URP clone and RenderMesh framing diverged.");

            source.layer = 30;
            using var scene = NowModelPreview.FromSceneObject(source, 1 << 30)
                .SetFixedResolution(96, 96);
            Assert.IsTrue(scene.RenderNow(), "URP rejected the borrowed scene-object request.");
            yield return null;
            int sceneCoverage = CountPixels(ReadPixels(scene.texture), pixel => pixel.a > 32);
            Assert.Greater(sceneCoverage, 500, "URP rendered no borrowed scene-object coverage.");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pipeline);
        }
    }
#endif

    [UnityTest]
    public IEnumerator InstalledUrpRendererFeatureDrawsNowUi()
    {
        var assetType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var rendererDataType = System.Type.GetType(
            "UnityEngine.Rendering.Universal.ScriptableRendererData, " +
            "Unity.RenderPipelines.Universal.Runtime",
            throwOnError: false);
        var featureType = System.Type.GetType(
            "NowUI.NowUniversalRendererFeature, NowUI.URP",
            throwOnError: false);

        if (assetType == null || rendererDataType == null || featureType == null)
        {
            Assert.Ignore("URP and NowUI.URP are not installed in this validation project.");
            yield break;
        }

        var create = assetType.GetMethod(
            "Create",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { rendererDataType },
            null);
        Assert.NotNull(create);

        var previousDefault = GraphicsSettings.defaultRenderPipeline;
        var previousQuality = QualitySettings.renderPipeline;
        var pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { null });
        var rendererDataField = assetType.GetField(
            "m_RendererDataList",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(rendererDataField);
        var rendererDataArray = (System.Array)rendererDataField.GetValue(pipeline);
        var rendererData = (ScriptableObject)rendererDataArray.GetValue(0);
        var feature = (ScriptableObject)ScriptableObject.CreateInstance(featureType);
        var features = (System.Collections.IList)rendererDataType
            .GetProperty("rendererFeatures")
            .GetValue(rendererData);
        features.Add(feature);
        rendererDataType.GetMethod("SetDirty").Invoke(rendererData, null);
        var cameraObject = new GameObject("URP NowUI Feature Test Camera");
        var graphicObject = new GameObject("URP NowUI Feature Test Graphic");
        var target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);

        try
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            yield return null;
            yield return null;

            target.Create();
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 0;
            var graphic = graphicObject.AddComponent<NowPipelineGraphic>();
            graphic.targetCamera = camera;
            graphic.rebuildNowUI += (_, _, rect) =>
                Now.Rectangle(rect).SetColor(Color.green).Draw();

            var request = new RenderPipeline.StandardRequest
            {
                destination = target,
                mipLevel = 0,
                slice = 0,
                face = CubemapFace.Unknown
            };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request));
            RenderPipeline.SubmitRenderRequest(camera, request);
            yield return null;

            var pixels = ReadPixels(target);
            int green = CountPixels(
                pixels,
                pixel => pixel.g > 180 && pixel.r < 80 && pixel.b < 80 && pixel.a > 180);
            Assert.Greater(green, 96 * 96 - 500,
                "The installed NowUI URP renderer feature did not composite its RenderGraph pass.");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(graphicObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(feature);
            Object.DestroyImmediate(rendererData);
            Object.DestroyImmediate(pipeline);
        }
    }

    [UnityTest]
    public IEnumerator ModelPreviewSceneLightingIsOptIn()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var sceneLightObject = new GameObject("Scene Directional Light");
        var sceneLight = sceneLightObject.AddComponent<Light>();
        sceneLight.type = LightType.Directional;
        sceneLight.color = Color.red;
        sceneLight.intensity = 4f;
        sceneLight.cullingMask = 1 << 31;
        sceneLight.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        sceneLight.enabled = false;

        try
        {
            using var preview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.identity, 0f, Color.white);

            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 noSceneLight = ReadPixels(preview.texture)[32 * 64 + 32];

            sceneLight.enabled = true;
            preview.MarkDirty();
            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 isolated = ReadPixels(preview.texture)[32 * 64 + 32];

            preview.SetSceneLightingEnabled();
            Assert.IsTrue(preview.RenderNow());
            yield return null;
            Color32 sceneLit = ReadPixels(preview.texture)[32 * 64 + 32];

            Assert.AreEqual(
                noSceneLight.r,
                isolated.r,
                4,
                $"A scene directional crossed into the private preview scene ({noSceneLight} -> {isolated}).");
            Assert.Greater(
                sceneLit.r,
                isolated.r + 40,
                $"Scene light did not stay behind the opt-in (isolated {isolated}, scene {sceneLit}).");
            Assert.Greater(sceneLit.r, sceneLit.g + 40);
        }
        finally
        {
            Object.DestroyImmediate(sceneLightObject);
            Object.DestroyImmediate(source);
        }
    }

    [UnityTest]
    public IEnumerator SharedPreviewLightReappliesSettingsWhenPreviewsAlternate()
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            using var redPreview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.Euler(35f, 145f, 0f), 3f, Color.red);
            using var bluePreview = new NowModelPreview(source)
                .SetFixedResolution(64, 64)
                .SetBackground(Color.black)
                .SetLight(Quaternion.Euler(35f, 145f, 0f), 3f, Color.blue);

            Assert.IsTrue(redPreview.RenderNow());
            yield return null;
            Color32 firstRed = ReadPixels(redPreview.texture)[32 * 64 + 32];

            Assert.IsTrue(bluePreview.RenderNow());
            yield return null;
            Color32 blue = ReadPixels(bluePreview.texture)[32 * 64 + 32];

            Assert.IsTrue(redPreview.RenderNow());
            yield return null;
            Color32 secondRed = ReadPixels(redPreview.texture)[32 * 64 + 32];

            Assert.Greater(firstRed.r, firstRed.b + 20, $"First red render was {firstRed}.");
            Assert.Greater(blue.b, blue.r + 20, $"Blue render inherited another preview's light: {blue}.");
            Assert.Greater(secondRed.r, secondRed.b + 20,
                $"Alternating back to red did not restore its light settings: {secondRed}.");
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

#if NOWUI_PARTICLE_SYSTEM
    [Test]
    public void ModelPreviewNeverOwnsCallerParticleSimulation()
    {
        var source = new GameObject("Particle Preview Source");
        var sourceParticles = source.AddComponent<ParticleSystem>();
        var main = sourceParticles.main;
        main.playOnAwake = true;
        sourceParticles.Play();
        bool originallyPlaying = sourceParticles.isPlaying;

        try
        {
            using (var preview = new NowModelPreview(source)
                .SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame))
            {
                preview.Prepare(32f, 32f);
                preview.SetRenderingEnabled(false);
                Assert.AreEqual(originallyPlaying, sourceParticles.isPlaying);
            }

            using (var preview = NowModelPreview.FromSceneObject(source)
                .SetUpdateMode(NowModelPreviewUpdateMode.EveryFrame))
            {
                preview.Prepare(32f, 32f);
                preview.SetRenderingEnabled(false);
                Assert.AreEqual(originallyPlaying, sourceParticles.isPlaying);
            }
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }
#endif

    [Test]
    public void RoundedCornersClipPixels()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(32, 32, 64, 64))
                .SetColor(Color.white)
                .SetRadius(24)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int filled = CountPixels(pixels, p => p.a > 200);

        int square = 64 * 64;
        int expectedRemoved = Mathf.RoundToInt(24 * 24 * (4f - Mathf.PI));

        Assert.Less(filled, square - expectedRemoved / 2, "Corners do not appear rounded.");
        Assert.Greater(filled, square - expectedRemoved * 2, "Far too few pixels for the rounded rect.");
    }

    [Test]
    public void RectangleZeroOutlineDoesNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            // Overscan the opaque backdrop so the rectangle's intentional
            // half-pixel AA edge lies outside the render target.
            Now.Rectangle(new NowRect(-1, -1, Side + 2, Side + 2))
                .SetColor(new Color(0.18f, 0.2f, 0.24f, 1f))
                .Draw();
            Now.Rectangle(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetColor(new Color(0.12f, 0.66f, 0.95f, 0.36f))
                .SetOutline(0f)
                .SetOutlineColor(Color.black)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        AssertTargetOpaque(ReadPixels(_target), "Zero-outline rectangle reduced target alpha");
    }

    [Test]
    public void RectangleOutlineVariantsStayOpaqueAndVisible()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side + 2, Side + 2))
                .SetColor(new Color(0.18f, 0.2f, 0.24f, 1f))
                .Draw();
            Now.Rectangle(new NowRect(24, 24, 80, 80))
                .SetRadius(18f)
                .SetColor(new Color(1f, 1f, 1f, 0f))
                .SetOutline(4f)
                .SetOutlineColor(Color.green)
                .Draw();
            Now.Rectangle(new NowRect(44, 44, 40, 40))
                .SetRadius(10f)
                .SetColor(new Color(1f, 0.26f, 0.32f, 0.34f))
                .SetOutline(1f)
                .SetOutlineColor(Color.white)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        AssertTargetOpaque(pixels, "Outlined rectangle variant reduced target alpha");
        Assert.Greater(
            CountPixels(pixels, p => p.g > 160 && p.r < 80 && p.b < 120),
            120,
            "Outline-only rectangle did not render a visible green outline.");
    }

    [Test]
    public void GlassBlursPreviouslyRenderedRenderTextureContent()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(48, 16, 32, 96))
                .SetBlurRadius(24f)
                .SetTint(new Color(1f, 1f, 1f, 0f))
                .SetVibrancy(1f, 1f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        Color32 redOutside = pixels[(Side / 2) * Side + 32];
        Color32 blueOutside = pixels[(Side / 2) * Side + 96];
        Color32 blurredInside = pixels[(Side / 2) * Side + 56];

        Assert.Greater(redOutside.r, 180, "Left background should stay red outside the glass pane.");
        Assert.Less(redOutside.b, 60, "Left background should stay sharp outside the glass pane.");
        Assert.Greater(blueOutside.b, 180, "Right background should stay blue outside the glass pane.");
        Assert.Less(blueOutside.r, 60, "Right background should stay sharp outside the glass pane.");
        Assert.Greater(blurredInside.r, 80, "Glass should retain red contribution from the backdrop.");
        Assert.Greater(blurredInside.b, 25, "Glass should blur blue contribution across the boundary.");
    }

    [Test]
    public void GlassZeroOutlineDoesNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(0, 0, Side / 2f, Side))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, 0, Side / 2f, Side))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetBlurRadius(16f)
                .SetTint(new Color(1f, 1f, 1f, 0.15f))
                .SetOutline(0f)
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        for (int y = 24; y < 104; ++y)
        {
            for (int x = 32; x < 96; ++x)
            {
                var pixel = pixels[y * Side + x];
                Assert.GreaterOrEqual(pixel.a, 250, $"Glass edge reduced target alpha at {x},{y}.");
            }
        }
    }

    [Test]
    public void GlassOutlineVariantsDoNotCutAlphaFromOpaqueBackdrop()
    {
        using (_renderer.Begin(_target))
        {
            Now.Rectangle(new NowRect(-1, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.red)
                .Draw();
            Now.Rectangle(new NowRect(Side / 2f, -1, Side / 2f + 1, Side + 2))
                .SetColor(Color.blue)
                .Draw();
            Now.Glass(new NowRect(32, 24, 64, 80))
                .SetRadius(18f)
                .SetBlurRadius(16f)
                .SetTint(new Color(1f, 1f, 1f, 0f))
                .SetOutline(3f)
                .SetOutlineColor(new Color(1f, 0.74f, 0.24f, 0.9f))
                .Draw();
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        AssertTargetOpaque(pixels, "Outlined glass variant reduced target alpha");
        Assert.Greater(
            CountPixels(pixels, p => p.r > 180 && p.g > 120 && p.b < 120),
            100,
            "Glass outline variant did not render a visible warm outline.");
    }

    [Test]
    public void TextRendersInkWithNativeCompiler()
    {
        AssertTextRendersInk(ResolveDefaultNowFont());
    }

    [Test]
    public void SolidTextColorDoesNotTintColorFontGlyphRgb()
    {
        var authored = new Color32(224, 112, 32, 255);
        var font = CreateSolidColorFont(authored);

        try
        {
            using (_renderer.Begin(_target))
            {
                Now.Text(new NowRect(24f, 24f, 80f, 80f), font)
                    .SetFontSize(64f)
                    .SetColor(Color.blue)
                    .Draw("A");
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            var center = PixelAtUi(pixels, 56, 56);

            Assert.That((int)center.r, Is.InRange(authored.r - 4, authored.r + 4));
            Assert.That((int)center.g, Is.InRange(authored.g - 4, authored.g + 4));
            Assert.That((int)center.b, Is.InRange(authored.b - 4, authored.b + 4));
            Assert.GreaterOrEqual(center.a, 250);
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void SolidTextAlphaFadesColorFontGlyphWithoutChangingItsHue()
    {
        var authored = new Color32(224, 112, 32, 255);
        var font = CreateSolidColorFont(authored);

        try
        {
            using (_renderer.Begin(_target))
            {
                Now.Text(new NowRect(24f, 24f, 80f, 80f), font)
                    .SetFontSize(64f)
                    .SetColor(new Color(0f, 0f, 1f, 0.5f))
                    .Draw("A");
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);
            var center = PixelAtUi(pixels, 56, 56);
            Color authoredSrgb = authored;
            Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? (authoredSrgb.linear * 0.5f).gamma
                : authoredSrgb * 0.5f;
            Color32 expectedBytes = expected;

            Assert.That((int)center.r, Is.InRange(expectedBytes.r - 4, expectedBytes.r + 4));
            Assert.That((int)center.g, Is.InRange(expectedBytes.g - 4, expectedBytes.g + 4));
            Assert.That((int)center.b, Is.InRange(expectedBytes.b - 4, expectedBytes.b + 4));
            Assert.That((int)center.a, Is.InRange(124, 132));
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void TextCoverageKeepsAnOpaqueTargetOpaque()
    {
        var font = ResolveDefaultNowFont();

        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(8f, 24f, Side - 16f, 80f), font)
                .SetFontSize(56f)
                .SetColor(Color.white)
                .Draw("HM");
        }

        _renderer.Render(
            _target,
            clear: true,
            clearColor: new Color(0.18f, 0.18f, 0.18f, 1f));
        var pixels = ReadPixels(_target);

        AssertTargetOpaque(
            pixels,
            "Text antialiasing reduced alpha on an opaque target");
        Assert.Greater(
            CountPixels(pixels, pixel => pixel.r > 180),
            500,
            "The opacity assertion did not exercise visible glyph coverage.");
    }

    [Test]
    public void TransparentWhiteTextOutputIsPremultiplied()
    {
        var font = ResolveDefaultNowFont();

        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(8f, 24f, Side - 16f, 80f), font)
                .SetFontSize(56f)
                .SetColor(Color.white)
                .Draw("HM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        int antialiasedPixels = 0;

        for (int i = 0; i < pixels.Length; ++i)
        {
            Color32 pixel = pixels[i];

            if (pixel.a <= 8 || pixel.a >= 247)
                continue;

            ++antialiasedPixels;
            Assert.LessOrEqual(
                pixel.r,
                pixel.a + 4,
                $"Text pixel {i} is not premultiplied ({pixel}).");
            Assert.AreEqual(pixel.r, pixel.g, 2, $"Text pixel {i} changed white's RGB balance.");
            Assert.AreEqual(pixel.r, pixel.b, 2, $"Text pixel {i} changed white's RGB balance.");
        }

        Assert.Greater(
            antialiasedPixels,
            80,
            "The premultiplication assertion did not exercise antialiased glyph pixels.");
    }

    [Test]
    public void PositiveOutlineLayersDoNotDoubleBlendTranslucentFill()
    {
        var font = ResolveDefaultNowFont();
        var singlePass = RenderTranslucentText(font, outlined: false);
        var layered = RenderTranslucentText(font, outlined: true);

        Assert.Greater(
            CountPixels(singlePass, pixel => pixel.a > 48 && pixel.a < 180),
            100,
            "The outline compositing fixture produced too little translucent fill.");
        AssertAlphaArraysNear(
            singlePass,
            layered,
            channelTolerance: 2,
            allowedMismatchCount: 4,
            "The outline-first path blended the translucent fill more than once.");
    }

    [Test]
    public void PositiveOutlineRingDoesNotFillTranslucentGlyphInterior()
    {
        var font = CreateSyntheticSdfFont();

        try
        {
            using (_renderer.Begin(_target))
            {
                Now.Text(new NowRect(32f, 32f, 64f, 64f), font)
                    .SetFontSize(64f)
                    .SetColor(new Color(0f, 0f, 1f, 0.5f))
                    .SetOutlinePixels(6f)
                    .SetOutlineColor(Color.red)
                    .Draw("A ");
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            Color32 centre = PixelAtUi(ReadPixels(_target), 64, 64);

            Assert.That((int)centre.a, Is.InRange(124, 132),
                $"The outline ring changed the translucent fill alpha: {centre}.");
            Assert.Less(centre.r, 8, $"Outline colour leaked across the glyph interior: {centre}.");
            Assert.Greater(centre.b, 100, $"The translucent blue fill did not render: {centre}.");
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void LayeredPositiveOutlinePreservesFillAndVisibleRing()
    {
        var font = CreateSyntheticSdfFont();

        try
        {
            var pixels = RenderOpaqueTextWithVisibleOutline(font, "A");
            Color32 centre = PixelAtUi(pixels, 64, 64);
            Color32 ring = PixelAtUi(pixels, 84, 64);
            Color32 outside = PixelAtUi(pixels, 92, 64);

            Assert.Greater(centre.b, 240, $"The face layer did not restore the blue glyph interior: {centre}.");
            Assert.Less(centre.r, 12, $"The outline ring leaked into the glyph interior: {centre}.");
            Assert.Greater(ring.r, 220, $"The outline-only layer did not preserve the visible red ring: {ring}.");
            Assert.Less(ring.b, 24, $"The fill layer leaked beyond the glyph face: {ring}.");
            Assert.Less(outside.a, 12, $"The outline extended beyond its expected ranged edge: {outside}.");
            Assert.Greater(
                CountPixels(pixels, pixel => pixel.r > 180 && pixel.b < 40 && pixel.a > 180),
                300,
                "The outline-ring fixture produced too little independent ring coverage.");
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void PositiveOutlineLayerPreservesColorGlyphOpacity()
    {
        var font = CreateSolidColorFont(new Color32(224, 112, 32, 255));

        try
        {
            using (_renderer.Begin(_target))
            {
                Now.Text(new NowRect(0f, 24f, Side, 80f), font)
                    .SetFontSize(64f)
                    .SetColor(new Color(1f, 1f, 1f, 0.5f))
                    .SetOutlinePixels(6f)
                    .SetOutlineColor(Color.white)
                    .Draw("AA");
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            Color32 centre = PixelAtUi(ReadPixels(_target), 32, 56);

            Assert.That((int)centre.a, Is.InRange(124, 132),
                $"The outline layer changed the RGBA glyph opacity: {centre}.");
            Assert.Greater(centre.r, centre.g, $"The authored colour glyph hue was lost: {centre}.");
            Assert.Greater(centre.g, centre.b, $"The authored colour glyph hue was lost: {centre}.");
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void OversizedStaticFontOutlineDoesNotExposeAtlasCellCorners()
    {
        var font = CreateSyntheticSdfFont();

        try
        {
            using (_renderer.Begin(_target))
            {
                Now.Text(new NowRect(32f, 32f, 64f, 64f), font)
                    .SetFontSize(64f)
                    .SetColor(Color.white)
                    .SetOutlinePixels(100f)
                    .SetOutlineColor(Color.white)
                    .Draw("A");
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);

            Assert.Greater(PixelAtUi(pixels, 64, 64).a, 240, "The synthetic glyph did not render its centre.");
            Assert.Less(PixelAtUi(pixels, 33, 33).a, 8, "The outline exposed the glyph atlas cell's top-left corner.");
            Assert.Less(PixelAtUi(pixels, 94, 33).a, 8, "The outline exposed the glyph atlas cell's top-right corner.");
            Assert.Less(PixelAtUi(pixels, 33, 94).a, 8, "The outline exposed the glyph atlas cell's bottom-left corner.");
            Assert.Less(PixelAtUi(pixels, 94, 94).a, 8, "The outline exposed the glyph atlas cell's bottom-right corner.");
        }
        finally
        {
            DestroySyntheticFont(font);
        }
    }

    [Test]
    public void TextRendersInkWithManagedCompiler()
    {
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont managedFont, out string error), error);

        try
        {
            AssertTextRendersInk(managedFont);
        }
        finally
        {
            Object.DestroyImmediate(managedFont);
        }
    }

    [Test]
    public void ManagedDynamicFontRendersOneHundredPixelOutlineEndToEnd()
    {
        const int LargeSide = 512;
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        var target = new RenderTexture(LargeSide, LargeSide, 0, RenderTextureFormat.ARGB32);
        target.Create();
        var renderer = new NowRenderer();

        try
        {
            var fillOnly = RenderManagedLargeOutlineFixture(renderer, target, font, outlined: false);
            var outlined = RenderManagedLargeOutlineFixture(renderer, target, font, outlined: true);
            Assert.IsTrue(font.GetGlyph('A', 80f, 100f / 80f, out _, out var effectMaterial));
            Assert.AreEqual(1f, effectMaterial.GetFloat("_NowUITextSdfEncoding"), 0.001f);

            Color32[] quantizedControl;

            try
            {
                // The packed page retains its legacy high byte in median RGB.
                // Disabling the decoder therefore gives a deterministic 8-bit
                // control without rebaking or changing any geometry.
                effectMaterial.SetFloat("_NowUITextSdfEncoding", 0f);
                quantizedControl = RenderManagedLargeOutlineFixture(renderer, target, font, outlined: true);
            }
            finally
            {
                effectMaterial.SetFloat("_NowUITextSdfEncoding", 1f);
            }

            RectInt fillBounds = FindRawAlphaBounds(fillOnly, LargeSide, LargeSide);
            RectInt outlineBounds = FindRawAlphaBounds(outlined, LargeSide, LargeSide);

            int packedTransitionBins = CountStablePartialAlphaBins(outlined);
            int quantizedTransitionBins = CountStablePartialAlphaBins(quantizedControl);
            Assert.GreaterOrEqual(packedTransitionBins, 96,
                $"The 100-pixel edge collapsed to too few stable AA levels ({packedTransitionBins}).");
            Assert.GreaterOrEqual(packedTransitionBins, quantizedTransitionBins * 2,
                $"Packed distance decoding did not materially improve edge precision " +
                $"({packedTransitionBins} levels versus {quantizedTransitionBins} for the 8-bit control).");

            Assert.Greater(fillBounds.width, 40, "The dynamic fill fixture produced no useful glyph coverage.");
            Assert.GreaterOrEqual(fillBounds.xMin - outlineBounds.xMin, 90,
                "The rendered stroke did not expand close to 100 pixels on the left.");
            Assert.GreaterOrEqual(outlineBounds.xMax - fillBounds.xMax, 90,
                "The rendered stroke did not expand close to 100 pixels on the right.");
            Assert.GreaterOrEqual(fillBounds.yMin - outlineBounds.yMin, 90,
                "The rendered stroke did not expand close to 100 pixels below the glyphs.");
            Assert.GreaterOrEqual(outlineBounds.yMax - fillBounds.yMax, 90,
                "The rendered stroke did not expand close to 100 pixels above the glyphs.");

            int fillInterior = 0;
            int preservedLeftFace = 0;
            int preservedRightFace = 0;
            int redStroke = 0;
            int fillMidpoint = fillBounds.xMin + fillBounds.width / 2;

            for (int y = 0; y < LargeSide; ++y)
            {
                for (int x = 0; x < LargeSide; ++x)
                {
                    int index = y * LargeSide + x;
                    Color32 fillPixel = fillOnly[index];
                    Color32 outlinePixel = outlined[index];

                    if (fillPixel.g > 220 && fillPixel.a > 240)
                    {
                        ++fillInterior;

                        if (outlinePixel.g > 180 && outlinePixel.r < 80 && outlinePixel.a > 220)
                        {
                            if (x < fillMidpoint)
                                ++preservedLeftFace;
                            else
                                ++preservedRightFace;
                        }
                    }

                    if (fillPixel.a < 8 &&
                        outlinePixel.r > 180 &&
                        outlinePixel.g < 80 &&
                        outlinePixel.a > 128)
                    {
                        ++redStroke;
                    }
                }
            }

            Assert.Greater(fillInterior, 500, "The fill comparison did not exercise both glyph faces.");
            Assert.Greater(preservedLeftFace, 150, "The first glyph face was not restored above overlapping outlines.");
            Assert.Greater(preservedRightFace, 150, "The second glyph face was not restored above overlapping outlines.");
            Assert.Greater(redStroke, 3000, "The 100-pixel dynamic outline produced too little visible stroke.");

            Assert.Less(outlined[(outlineBounds.yMin + 2) * LargeSide + outlineBounds.xMin + 2].a, 16,
                "The ranged atlas exposed its bottom-left cell corner.");
            Assert.Less(outlined[(outlineBounds.yMin + 2) * LargeSide + outlineBounds.xMax - 2].a, 16,
                "The ranged atlas exposed its bottom-right cell corner.");
            Assert.Less(outlined[(outlineBounds.yMax - 2) * LargeSide + outlineBounds.xMin + 2].a, 16,
                "The ranged atlas exposed its top-left cell corner.");
            Assert.Less(outlined[(outlineBounds.yMax - 2) * LargeSide + outlineBounds.xMax - 2].a, 16,
                "The ranged atlas exposed its top-right cell corner.");
        }
        finally
        {
            renderer.Dispose();
            target.Release();
            Object.DestroyImmediate(target);
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void ManagedSdfLargeOutlinesDoNotExposeGlyphPlaneSeams()
    {
        const int Width = 768;
        const int Height = 310;
        const float FontSize = 80f;
        const string Value = "NowUI";
        float[] outlineWidths = { 32f, 64f, 100f };
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
        target.Create();
        var commandBuffer = new CommandBuffer { name = "SDF glyph-plane seam regression" };

        try
        {
            for (int outlineIndex = 0; outlineIndex < outlineWidths.Length; ++outlineIndex)
            {
                float outline = outlineWidths[outlineIndex];
                var surface = new NowRect(0f, 0f, Width, Height);
                Vector2 textSize = font.MeasureText(Value, FontSize);
                var position = new Vector2(
                    surface.width * 0.5f - textSize.x * 0.5f,
                    surface.height * 0.5f - textSize.y * 0.5f);

                using var drawList = new NowDrawList();
                using (drawList.Begin(new Vector2(Width, Height)))
                {
                    NowSdf.Scene(surface, $"playmode-sdf-glyph-plane-seam-{outline}")
                        .SetColor(Color.white)
                        .SetOutline(outline, Color.white)
                        .Text(position, Value, font, FontSize)
                        .Draw();
                }

                Assert.AreEqual(1, drawList.batchCount);
                Material material = drawList.batches[0].material;
                int shapeCount = Mathf.RoundToInt(material.GetFloat("_SdfShapeCount"));
                Vector4[] data0 = material.GetVectorArray("_SdfData0");
                Vector4[] data1 = material.GetVectorArray("_SdfData1");
                float minX = float.PositiveInfinity;
                float minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float maxY = float.NegativeInfinity;
                int glyphCount = 0;

                for (int i = 0; i < shapeCount; ++i)
                {
                    if (!Mathf.Approximately(data0[i].x, 5f))
                        continue;

                    Vector4 glyphRect = data1[i];
                    minX = Mathf.Min(minX, glyphRect.x - glyphRect.z * 0.5f);
                    maxX = Mathf.Max(maxX, glyphRect.x + glyphRect.z * 0.5f);
                    minY = Mathf.Min(minY, glyphRect.y - glyphRect.w * 0.5f);
                    maxY = Mathf.Max(maxY, glyphRect.y + glyphRect.w * 0.5f);
                    ++glyphCount;
                }

                Assert.Greater(glyphCount, 0, "The SDF seam fixture uploaded no glyphs.");

                commandBuffer.Clear();
                NowRenderer.PopulateCommandBuffer(commandBuffer, drawList, target, true, Color.black);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                Color32[] pixels = ReadPixels(target);
                int[] edgeXs =
                {
                    Mathf.RoundToInt(minX),
                    Mathf.RoundToInt(maxX)
                };
                int[] cornerYs =
                {
                    Mathf.Clamp(Mathf.CeilToInt(minY) + 2, 2, Height - 3),
                    Mathf.Clamp(Mathf.FloorToInt(maxY) - 2, 2, Height - 3)
                };
                int brightestSeamSample = 0;

                for (int edgeIndex = 0; edgeIndex < edgeXs.Length; ++edgeIndex)
                {
                    for (int dx = -1; dx <= 1; ++dx)
                    {
                        int x = edgeXs[edgeIndex] + dx;
                        if (x < 0 || x >= Width)
                            continue;

                        for (int cornerIndex = 0; cornerIndex < cornerYs.Length; ++cornerIndex)
                        {
                            int y = cornerYs[cornerIndex];
                            Color32 pixel = pixels[(Height - 1 - y) * Width + x];
                            brightestSeamSample = Mathf.Max(
                                brightestSeamSample,
                                Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
                        }
                    }
                }

                Assert.LessOrEqual(
                    brightestSeamSample,
                    2,
                    $"The {outline}px SDF outline exposed a glyph-plane AA seam " +
                    $"(brightest perimeter sample {brightestSeamSample}/255).");
            }
        }
        finally
        {
            commandBuffer.Release();
            target.Release();
            Object.DestroyImmediate(target);
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void SdfOpaqueFillAndOutlineHaveNoBoundaryCoverageGap()
    {
        const int BaseSide = 192;
        const float BaseFontSize = 64f;
        const float BaseOutline = 6f;
        const string Value = "O";
        int[] scales = { 1, 4 };
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        var commandBuffer = new CommandBuffer { name = "SDF fill-outline boundary regression" };

        try
        {
            for (int scaleIndex = 0; scaleIndex < scales.Length; ++scaleIndex)
            {
                int scale = scales[scaleIndex];
                int side = BaseSide * scale;
                float fontSize = BaseFontSize * scale;
                float outline = BaseOutline * scale;
                var target = new RenderTexture(side, side, 0, RenderTextureFormat.ARGB32);
                target.Create();

                try
                {
                    var surface = new NowRect(0f, 0f, side, side);
                    Vector2 textSize = font.MeasureText(Value, fontSize);
                    var position = new Vector2(
                        surface.width * 0.5f - textSize.x * 0.5f,
                        surface.height * 0.5f - textSize.y * 0.5f);
                    var renders = new Color32[3][];

                    for (int pass = 0; pass < renders.Length; ++pass)
                    {
                        bool outlined = pass != 0;
                        Color fill = pass == 2 ? Color.clear : Color.white;

                        using var drawList = new NowDrawList();
                        using (drawList.Begin(new Vector2(side, side)))
                        {
                            NowSdfBuilder scene = NowSdf.Scene(
                                    surface,
                                    $"playmode-sdf-fill-outline-boundary-{scale}-{pass}")
                                .SetColor(fill);

                            if (outlined)
                                scene.SetOutline(outline, Color.white);

                            scene.Text(position, Value, font, fontSize).Draw();
                        }

                        Assert.AreEqual(1, drawList.batchCount,
                            $"The {scale}x boundary fixture did not produce one SDF batch.");

                        commandBuffer.Clear();
                        NowRenderer.PopulateCommandBuffer(
                            commandBuffer,
                            drawList,
                            target,
                            true,
                            Color.clear);
                        Graphics.ExecuteCommandBuffer(commandBuffer);
                        renders[pass] = ReadPixels(target);
                    }

                    Color32[] fillOnly = renders[0];
                    Color32[] outlinedPixels = renders[1];
                    Color32[] ringOnly = renders[2];
                    int candidateCount = 0;
                    int gapCount = 0;
                    int ringCoverageErrorCount = 0;
                    float worstRingCoverageError = 0f;
                    int lowestOutlinedAlpha = 255;
                    int lowestOutlinedIndex = -1;

                    for (int i = 0; i < fillOnly.Length; ++i)
                    {
                        // Partial fill coverage locates the glyph boundary without
                        // depending on its exact subpixel position. Six outline
                        // pixels put that boundary well inside the opaque stroke.
                        if (fillOnly[i].a < 24 || fillOnly[i].a > 231)
                            continue;

                        ++candidateCount;
                        int alpha = outlinedPixels[i].a;

                        if (alpha < lowestOutlinedAlpha)
                        {
                            lowestOutlinedAlpha = alpha;
                            lowestOutlinedIndex = i;
                        }

                        if (alpha < 245)
                            ++gapCount;

                        // With a transparent face, the exterior ring owns the
                        // complement of the fill's geometric coverage. The
                        // render target stores alpha squared under SrcAlpha
                        // blending, so recover each source coverage first.
                        float fillCoverage = Mathf.Sqrt(fillOnly[i].a / 255f);
                        float ringCoverage = Mathf.Sqrt(ringOnly[i].a / 255f);
                        float ringCoverageError = Mathf.Abs(
                            fillCoverage + ringCoverage - 1f);
                        worstRingCoverageError = Mathf.Max(
                            worstRingCoverageError,
                            ringCoverageError);

                        if (ringCoverageError > 0.04f)
                            ++ringCoverageErrorCount;
                    }

                    int minimumCandidates = 20 * scale;
                    Assert.GreaterOrEqual(
                        candidateCount,
                        minimumCandidates,
                        $"The {scale}x fill-only control exposed only {candidateCount} useful boundary samples.");

                    int lowestX = lowestOutlinedIndex >= 0 ? lowestOutlinedIndex % side : -1;
                    int lowestY = lowestOutlinedIndex >= 0 ? lowestOutlinedIndex / side : -1;
                    Assert.AreEqual(
                        0,
                        gapCount,
                        $"The {scale}x opaque fill/outline boundary contained {gapCount}/{candidateCount} " +
                        $"samples below 245 alpha; the lowest was {lowestOutlinedAlpha}/255 at raw " +
                        $"pixel {lowestX},{lowestY}.");
                    Assert.AreEqual(
                        0,
                        ringCoverageErrorCount,
                        $"The {scale}x transparent-fill outline hardened its inner AA edge at " +
                        $"{ringCoverageErrorCount}/{candidateCount} samples; worst complementary " +
                        $"coverage error was {worstRingCoverageError:F4}.");
                }
                finally
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
            }
        }
        finally
        {
            commandBuffer.Release();
            NowSdf.Reset();
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

    [Test]
    public void ManagedSdfCapacityFallbackDoesNotExpandGlyphPlanesIntoBoxes()
    {
        const int Width = 768;
        const int Height = 310;
        const float FontSize = 80f;
        const float Outline = 100f;
        const long SealedBasePageBudget = 4L * 1024L * 1024L;
        const string Value = "N";
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
        target.Create();
        var commandBuffer = new CommandBuffer { name = "SDF capacity-fallback box regression" };

        try
        {
            // Publish and seal one base page before requesting the large effect
            // tier. This reproduces a deterministic lower-range capacity fallback.
            font.EnsureGlyphs(Value, FontSize);
            font.dynamicCacheBudgetBytesOverride = SealedBasePageBudget;

            int basePixelRange = font.GetDynamicPixelRange(0f, FontSize);
            int requestedPixelRange = font.GetDynamicPixelRange(Outline / FontSize, FontSize);
            float baseScreenRange = FontSize / font.GetDynamicGlyphSize(FontSize) * basePixelRange;
            float requestedScreenRange = FontSize / font.GetDynamicGlyphSize(FontSize) * requestedPixelRange;
            Assert.Greater(requestedPixelRange, basePixelRange,
                "The capacity fixture must request an extended range tier.");
            Assert.AreEqual(20f, baseScreenRange, 0.001f,
                "The managed base tier must expose the deterministic 20-pixel screen field.");
            Assert.AreEqual(320f, requestedScreenRange, 0.001f,
                "The 100-pixel fixture must request the deterministic 320-pixel screen field.");

            using var drawList = new NowDrawList();
            using (drawList.Begin(new Vector2(Width, Height)))
            {
                NowSdf.Scene(
                        new NowRect(0f, 0f, Width, Height),
                        "playmode-sdf-capacity-fallback-box")
                    .SetColor(Color.white)
                    .SetOutline(Outline, Color.white)
                    .Text(new Vector2(180f, 100f), Value, font, FontSize)
                    .Circle(new Vector2(640f, 155f), 12f)
                    .Draw();
            }

            Assert.AreEqual(1, drawList.batchCount);
            Material material = drawList.batches[0].material;
            int shapeCount = Mathf.RoundToInt(material.GetFloat("_SdfShapeCount"));
            Vector4[] data0 = material.GetVectorArray("_SdfData0");
            Vector4[] data1 = material.GetVectorArray("_SdfData1");
            Vector4[] data2 = material.GetVectorArray("_SdfData2");
            float textEffectLimit = material.GetFloat("_SdfTextEffectLimit");
            int glyphCount = 0;

            Assert.AreEqual(Outline, material.GetVector("_SdfOutline").x, 0.0001f,
                "The scene must retain the authored outline for non-text SDF shapes.");
            Assert.AreEqual(9f, textEffectLimit, 0.001f,
                "A 20-pixel fallback field must reserve one pixel and expose only nine safe effect pixels.");
            Assert.IsTrue(
                font.IsDynamicGlyphCapacityBlocked(
                    Value[0],
                    font.GetDynamicGlyphSize(FontSize),
                    requestedPixelRange),
                "The fixture did not exercise the requested tier's capacity fallback.");

            for (int i = 0; i < shapeCount; ++i)
            {
                if (!Mathf.Approximately(data0[i].x, 5f))
                    continue;

                Assert.AreEqual(baseScreenRange, data2[i].x, 0.001f,
                    $"Glyph {glyphCount} did not fall back to the deterministic base field.");
                Assert.Less(data2[i].x, requestedScreenRange,
                    $"Glyph {glyphCount} unexpectedly allocated the blocked extended tier.");
                ++glyphCount;
            }

            Assert.AreEqual(Value.Length, glyphCount, "The fallback fixture did not upload every glyph.");

            commandBuffer.Clear();
            NowRenderer.PopulateCommandBuffer(commandBuffer, drawList, target, true, Color.black);
            Graphics.ExecuteCommandBuffer(commandBuffer);
            Color32[] pixels = ReadPixels(target);
            int brightestPlaneCorner = 0;

            for (int i = 0; i < shapeCount; ++i)
            {
                if (!Mathf.Approximately(data0[i].x, 5f))
                    continue;

                Vector4 rect = data1[i];
                int[] xs =
                {
                    Mathf.Clamp(Mathf.CeilToInt(rect.x - rect.z * 0.5f) + 2, 0, Width - 1),
                    Mathf.Clamp(Mathf.FloorToInt(rect.x + rect.z * 0.5f) - 2, 0, Width - 1)
                };
                int[] ys =
                {
                    Mathf.Clamp(Mathf.CeilToInt(rect.y - rect.w * 0.5f) + 2, 0, Height - 1),
                    Mathf.Clamp(Mathf.FloorToInt(rect.y + rect.w * 0.5f) - 2, 0, Height - 1)
                };

                for (int x = 0; x < xs.Length; ++x)
                {
                    for (int y = 0; y < ys.Length; ++y)
                    {
                        Color32 pixel = pixels[(Height - 1 - ys[y]) * Width + xs[x]];
                        brightestPlaneCorner = Mathf.Max(
                            brightestPlaneCorner,
                            Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
                    }
                }
            }

            Assert.Greater(
                CountPixels(pixels, pixel => pixel.r > 200 && pixel.g > 200 && pixel.b > 200),
                100,
                "The fallback fixture rendered no useful text coverage.");
            Color32 analyticOutline = pixels[(Height - 1 - 155) * Width + 700];
            Assert.Greater(
                Mathf.Max(analyticOutline.r, Mathf.Max(analyticOutline.g, analyticOutline.b)),
                200,
                "The glyph field limit incorrectly clamped the mixed scene's analytic circle outline.");
            Assert.LessOrEqual(
                brightestPlaneCorner,
                2,
                $"The capacity fallback expanded glyph planes into a box " +
                $"(brightest inset corner {brightestPlaneCorner}/255).");
        }
        finally
        {
            commandBuffer.Release();
            target.Release();
            Object.DestroyImmediate(target);
            NowSdf.Reset();
            font.dynamicCacheBudgetBytesOverride = 0;
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }

#if NOWUI_UGUI
    sealed class ManagedLargeOutlineGraphic : NowGraphic
    {
        public NowFont font;

        public void RebuildForTest()
        {
            UpdateGeometry();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Text(new NowRect(180f, 180f, 180f, 100f), font)
                .SetFontSize(80f)
                .SetColor(Color.green)
                .SetOutlinePixels(100f)
                .SetOutlineColor(Color.red)
                .Draw("AA");
        }
    }

    sealed class UGUIPageRenderDriver : MonoBehaviour
    {
        public CanvasRenderer sourceRenderer;
        public Mesh mesh;
        public float side;
        public bool drew;

        void OnPostRender()
        {
            if (sourceRenderer == null || mesh == null)
                return;

            GL.PushMatrix();

            try
            {
                // Batch-mode Camera.Render does not reliably composite a CanvasRenderer.
                // Replay its uploaded page with the exact materials it bound so this still
                // exercises NowGraphic's UGUI clone and the real UGUI shader pass.
                GL.LoadIdentity();
                GL.LoadProjectionMatrix(Matrix4x4.Ortho(
                    -side * 0.5f,
                    side * 0.5f,
                    -side * 0.5f,
                    side * 0.5f,
                    -1f,
                    100f));

                int subMeshCount = Mathf.Min(mesh.subMeshCount, sourceRenderer.materialCount);

                for (int i = 0; i < subMeshCount; ++i)
                {
                    var material = sourceRenderer.GetMaterial(i);

                    if (material == null || !material.SetPass(0))
                        continue;

                    Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
                    drew = true;
                }
            }
            finally
            {
                GL.PopMatrix();
            }
        }
    }

    [UnityTest]
    public IEnumerator ManagedLargeOutlineKeepsPackedPrecisionThroughUGUIShader()
    {
        const int LargeSide = 512;
        var source = ResolveDefaultNowFont();
        Assert.IsTrue(source.TryGetSourceBytes(out byte[] bytes), "Default font has no embedded source.");

        NowFontCompiler.forceManagedCompiler = true;
        Assert.IsTrue(NowFontCompiler.TryCompile(bytes, out NowFont font, out string error), error);

        var target = new RenderTexture(LargeSide, LargeSide, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var cameraObject = new GameObject("Packed SDF UGUI Camera");
        var canvasObject = new GameObject("Packed SDF UGUI Canvas", typeof(Canvas));
        var graphicObject = new GameObject("Packed SDF UGUI Graphic", typeof(ManagedLargeOutlineGraphic));

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = LargeSide * 0.5f;
            camera.targetTexture = target;
            camera.cullingMask = 0;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(LargeSide, LargeSide);
            canvasRect.localScale = Vector3.one;

            graphicObject.transform.SetParent(canvasObject.transform, false);
            var graphicRect = graphicObject.GetComponent<RectTransform>();
            graphicRect.anchorMin = Vector2.zero;
            graphicRect.anchorMax = Vector2.one;
            graphicRect.pivot = new Vector2(0.5f, 0.5f);
            graphicRect.offsetMin = Vector2.zero;
            graphicRect.offsetMax = Vector2.zero;
            var graphic = graphicObject.GetComponent<ManagedLargeOutlineGraphic>();
            graphic.font = font;
            graphic.MarkDirty();

            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            graphic.RebuildForTest();
            Assert.Greater(graphic.canvasPageCount, 0, "The UGUI fixture did not build a canvas page.");
            var mesh = graphic.GetCanvasPageMesh(0);
            Assert.Greater(mesh.vertexCount, 0, "The UGUI fixture built an empty mesh.");
            Assert.Greater(graphic.canvasRenderer.materialCount, 0, "The UGUI fixture did not bind a material.");

            for (int i = 0; i < graphic.canvasRenderer.materialCount; ++i)
            {
                var material = graphic.canvasRenderer.GetMaterial(i);
                Assert.NotNull(material, $"The UGUI fixture material {i} is missing.");
                Assert.AreEqual("NowUI/Text Renderer UGUI", material.shader.name,
                    $"Canvas material {i} did not use the UGUI text shader.");
            }

            var driver = cameraObject.AddComponent<UGUIPageRenderDriver>();
            driver.sourceRenderer = graphic.canvasRenderer;
            driver.mesh = mesh;
            driver.side = LargeSide;
            camera.Render();
            Assert.IsTrue(driver.drew, "The UGUI fixture did not execute its render pass.");

            var pixels = ReadPixels(target);
            Assert.Greater(
                CountPixels(pixels, pixel => pixel.r > 180 && pixel.g < 80 && pixel.a > 128),
                3000,
                "The UGUI text shader produced too little 100-pixel outline coverage.");
            Assert.Greater(
                CountPixels(pixels, pixel => pixel.g > 180 && pixel.r < 80 && pixel.a > 220),
                500,
                "The UGUI text shader did not preserve the managed glyph faces.");
            Assert.GreaterOrEqual(CountStablePartialAlphaBins(pixels), 96,
                "The UGUI packed decoder collapsed the large outline edge back to 8-bit precision.");
        }
        finally
        {
            Object.DestroyImmediate(graphicObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
            font.ClearDynamicCache();
            Object.DestroyImmediate(font);
        }
    }
#endif

    [Test]
    public void AnalyticMaskClipsTextMaterial()
    {
        var font = ResolveDefaultNowFont();

        using (_renderer.Begin(_target))
        using (Now.Mask(NowMaskShape.Rectangle(new NowRect(48f, 0f, 32f, Side))))
        {
            Now.Text(new NowRect(0f, 24f, Side, 80f), font)
                .SetFontSize(56f)
                .SetColor(Color.white)
                .Draw("MMMM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);
        int insideInk = 0;
        int leakedInk = 0;

        for (int y = 0; y < Side; ++y)
        {
            for (int x = 0; x < Side; ++x)
            {
                if (pixels[y * Side + x].a <= 12)
                    continue;

                if (x >= 48 && x < 80)
                    ++insideInk;
                else if (x < 45 || x >= 83)
                    ++leakedInk;
            }
        }

        Assert.Greater(insideInk, 80, "Text produced no visible ink inside the analytic mask.");
        Assert.AreEqual(0, leakedInk, "Text shader leaked well beyond the analytic mask feather.");
    }

    void AssertTextRendersInk(NowFontAsset font)
    {
        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(8, 24, Side - 16, 80), font)
                .SetFontSize(56)
                .SetColor(Color.white)
                .Draw("HM");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        var pixels = ReadPixels(_target);

        int ink = CountPixels(pixels, p => p.a > 128);

        Assert.Greater(ink, 500, "Text produced almost no ink.");
        Assert.Less(ink, Side * Side / 2, "Text ink coverage is implausibly large.");
    }

    Color32[] RenderTranslucentText(NowFontAsset font, bool outlined)
    {
        _renderer.Clear();

        using (_renderer.Begin(_target))
        {
            NowText text = Now.Text(new NowRect(24f, 24f, 80f, 80f), font)
                .SetFontSize(64f)
                .SetColor(new Color(1f, 1f, 1f, 0.5f));

            if (outlined)
                text = text.SetOutlinePixels(6f).SetOutlineColor(Color.clear);

            text.Draw("M");
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    Color32[] RenderOpaqueTextWithVisibleOutline(NowFontAsset font, string value)
    {
        _renderer.Clear();

        using (_renderer.Begin(_target))
        {
            Now.Text(new NowRect(32f, 32f, 64f, 64f), font)
                .SetFontSize(64f)
                .SetColor(Color.blue)
                .SetOutlinePixels(6f)
                .SetOutlineColor(Color.red)
                .Draw(value);
        }

        _renderer.Render(_target, clear: true, clearColor: Color.clear);
        return ReadPixels(_target);
    }

    static Color32[] RenderManagedLargeOutlineFixture(
        NowRenderer renderer,
        RenderTexture target,
        NowFont font,
        bool outlined)
    {
        renderer.Clear();

        using (renderer.Begin(target))
        {
            NowText text = Now.Text(new NowRect(180f, 180f, 180f, 100f), font)
                .SetFontSize(80f)
                .SetColor(Color.green)
                .SetOutlineColor(Color.red);

            if (outlined)
                text = text.SetOutlinePixels(100f);

            text.Draw("AA");
        }

        renderer.Render(target, clear: true, clearColor: Color.clear);
        return ReadPixels(target);
    }

    static int CountStablePartialAlphaBins(Color32[] pixels)
    {
        var counts = new int[256];

        for (int i = 0; i < pixels.Length; ++i)
        {
            int alpha = pixels[i].a;

            if (alpha >= 4 && alpha <= 251)
                ++counts[alpha];
        }

        int occupied = 0;

        for (int alpha = 4; alpha <= 251; ++alpha)
        {
            if (counts[alpha] >= 2)
                ++occupied;
        }

        return occupied;
    }

    [Test]
    public void LottieRendersGeometry()
    {
        const string RedSquare = @"{
            ""v"": ""5.5.0"", ""fr"": 30, ""ip"": 0, ""op"": 30, ""w"": 100, ""h"": 100,
            ""layers"": [{
                ""ddd"": 0, ""ty"": 4, ""ip"": 0, ""op"": 30, ""st"": 0,
                ""ks"": {
                    ""o"": { ""a"": 0, ""k"": 100 },
                    ""p"": { ""a"": 0, ""k"": [50, 50] },
                    ""a"": { ""a"": 0, ""k"": [0, 0] },
                    ""s"": { ""a"": 0, ""k"": [100, 100] },
                    ""r"": { ""a"": 0, ""k"": 0 }
                },
                ""shapes"": [{
                    ""ty"": ""gr"",
                    ""it"": [
                        { ""ty"": ""rc"", ""p"": { ""a"": 0, ""k"": [0, 0] }, ""s"": { ""a"": 0, ""k"": [60, 60] }, ""r"": { ""a"": 0, ""k"": 0 } },
                        { ""ty"": ""fl"", ""c"": { ""a"": 0, ""k"": [1, 0, 0, 1] }, ""o"": { ""a"": 0, ""k"": 100 } },
                        {
                            ""ty"": ""tr"",
                            ""p"": { ""a"": 0, ""k"": [0, 0] }, ""a"": { ""a"": 0, ""k"": [0, 0] },
                            ""s"": { ""a"": 0, ""k"": [100, 100] }, ""r"": { ""a"": 0, ""k"": 0 }, ""o"": { ""a"": 0, ""k"": 100 }
                        }
                    ]
                }]
            }]
        }";

        var asset = ScriptableObject.CreateInstance<NowLottieAsset>();

        try
        {
            asset.SetSource(RedSquare);

            using (_renderer.Begin(_target))
            {
                Now.Lottie(new NowRect(14, 14, 100, 100), asset)
                    .SetTime(0f)
                    .Draw();
            }

            _renderer.Render(_target, clear: true, clearColor: Color.clear);
            var pixels = ReadPixels(_target);

            int red = CountPixels(pixels, p => p.r > 150 && p.g < 80 && p.a > 128);

            Assert.Greater(red, 2000, "Lottie shape produced too few red pixels.");
            Assert.Less(red, 5000, "Lottie shape produced implausibly many red pixels.");
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

#if NOWUI_UGUI
    [UnityTest]
    public IEnumerator UGUIElementsOccludeNowUIPointer()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var hostObject = new GameObject("Host", typeof(NowGraphic));
        hostObject.transform.SetParent(canvasObject.transform, false);
        var hostRect = hostObject.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.zero;
        hostRect.pivot = Vector2.zero;
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.sizeDelta = new Vector2(200, 200);
        var host = hostObject.GetComponent<NowGraphic>();

        var blockerObject = new GameObject("Blocker", typeof(UnityEngine.UI.Image));
        blockerObject.transform.SetParent(canvasObject.transform, false);
        var blockerRect = blockerObject.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.zero;
        blockerRect.pivot = Vector2.zero;
        blockerRect.anchoredPosition = Vector2.zero;
        blockerRect.sizeDelta = new Vector2(100, 100);

        try
        {
            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, new Vector2(50, 50)),
                "A UGUI element above the host must occlude the pointer.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, new Vector2(150, 150)),
                "The pointer over the host itself must pass.");
        }
        finally
        {
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator HostOwnedOverlayExtendingPastHostAllowsOnlyLowerUGUI()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var backgroundObject = new GameObject("Background", typeof(UnityEngine.UI.Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.zero;
        backgroundRect.pivot = Vector2.zero;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(300, 300);

        var hostObject = new GameObject("Host", typeof(NowGraphic));
        hostObject.transform.SetParent(canvasObject.transform, false);
        var hostRect = hostObject.GetComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.zero;
        hostRect.pivot = Vector2.zero;
        hostRect.anchoredPosition = Vector2.zero;
        hostRect.sizeDelta = new Vector2(100, 100);
        var host = hostObject.GetComponent<NowGraphic>();

        GameObject blockerObject = null;

        try
        {
            yield return null;

            var popupPointOutsideHost = new Vector2(150, 50);

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost),
                "Strict gating still requires the EventSystem hit to be the host.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "Host-owned overlays may receive input over lower UGUI behind the host.");

            blockerObject = new GameObject("Higher Blocker", typeof(UnityEngine.UI.Image));
            blockerObject.transform.SetParent(canvasObject.transform, false);
            var blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.zero;
            blockerRect.pivot = Vector2.zero;
            blockerRect.anchoredPosition = new Vector2(125, 25);
            blockerRect.sizeDelta = new Vector2(50, 50);

            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "UGUI drawn above the host must still occlude host-owned overlays.");
        }
        finally
        {
            if (blockerObject != null)
                Object.DestroyImmediate(blockerObject);

            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(backgroundObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator HostOwnedWorldOverlayIgnoresSeparateCanvasBehindHost()
    {
        NowOverlay.Reset();

        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var cameraObject = new GameObject("Event Camera");
        var behindCanvasObject = new GameObject("Behind Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var behindObject = new GameObject("Behind", typeof(NowGraphic));
        var hostCanvasObject = new GameObject("Host Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var hostObject = new GameObject("Host", typeof(NowGraphic));
        GameObject frontCanvasObject = null;
        GameObject frontObject = null;

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.pixelRect = new Rect(0, 0, 400, 400);
            cameraObject.transform.position = new Vector3(0, 0, -10);

            var behindCanvas = behindCanvasObject.GetComponent<Canvas>();
            behindCanvas.renderMode = RenderMode.WorldSpace;
            behindCanvas.worldCamera = camera;
            behindCanvasObject.transform.position = new Vector3(0, 0, 1f);

            behindObject.transform.SetParent(behindCanvasObject.transform, false);
            var behindRect = behindObject.GetComponent<RectTransform>();
            behindRect.anchorMin = new Vector2(0.5f, 0.5f);
            behindRect.anchorMax = new Vector2(0.5f, 0.5f);
            behindRect.pivot = new Vector2(0.5f, 0.5f);
            behindRect.anchoredPosition = Vector2.zero;
            behindRect.sizeDelta = new Vector2(4f, 4f);
            var behindHost = behindObject.GetComponent<NowGraphic>();

            var hostCanvas = hostCanvasObject.GetComponent<Canvas>();
            hostCanvas.renderMode = RenderMode.WorldSpace;
            hostCanvas.worldCamera = camera;
            hostCanvasObject.transform.position = Vector3.zero;

            hostObject.transform.SetParent(hostCanvasObject.transform, false);
            var hostRect = hostObject.GetComponent<RectTransform>();
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.anchoredPosition = Vector2.zero;
            hostRect.sizeDelta = new Vector2(1f, 1f);
            var host = hostObject.GetComponent<NowGraphic>();

            yield return null;

            var popupPointOutsideHost = (Vector2)camera.WorldToScreenPoint(new Vector3(1.5f, 0f, 0f));

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost),
                "Strict gating still requires the world-space hit to be the host.");
            Assert.IsTrue(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "Host-owned overlays must ignore separate world-space canvases behind the NowUI host.");

            using (NowOverlay.Host(host, hostRect, camera))
                NowOverlay.DeferScreen(new NowRect(1.75f, 0.25f, 0.5f, 0.5f), 606, _ => { });

            Assert.IsFalse(
                NowOverlay.IsPointerBlockedByForeignOverlay(host, popupPointOutsideHost),
                "A host-owned overlay must not block its own host.");
            Assert.IsTrue(
                NowOverlay.IsPointerBlockedByForeignOverlay(behindHost, popupPointOutsideHost),
                "A host-owned overlay on the front canvas must block NowUI hosts behind it.");

            frontCanvasObject = new GameObject("Front Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            var frontCanvas = frontCanvasObject.GetComponent<Canvas>();
            frontCanvas.renderMode = RenderMode.WorldSpace;
            frontCanvas.worldCamera = camera;
            frontCanvasObject.transform.position = new Vector3(0, 0, -1f);

            frontObject = new GameObject("Front", typeof(UnityEngine.UI.Image));
            frontObject.transform.SetParent(frontCanvasObject.transform, false);
            var frontRect = frontObject.GetComponent<RectTransform>();
            frontRect.anchorMin = new Vector2(0.5f, 0.5f);
            frontRect.anchorMax = new Vector2(0.5f, 0.5f);
            frontRect.pivot = new Vector2(0.5f, 0.5f);
            frontRect.anchoredPosition = Vector2.zero;
            frontRect.sizeDelta = new Vector2(4f, 4f);

            yield return null;

            Assert.IsFalse(
                NowRaycastGate.IsPointerAllowed(host, popupPointOutsideHost, allowHostOwnedOverlay: true),
                "World-space UGUI in front of the NowUI host must still occlude host-owned overlays.");
        }
        finally
        {
            NowOverlay.Reset();

            if (frontObject != null)
                Object.DestroyImmediate(frontObject);

            if (frontCanvasObject != null)
                Object.DestroyImmediate(frontCanvasObject);

            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(hostCanvasObject);
            Object.DestroyImmediate(behindObject);
            Object.DestroyImmediate(behindCanvasObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator ScreenSpaceUGUIOccludesWorldGraphicPointer()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
        var blockerObject = new GameObject("Screen UI Blocker", typeof(UnityEngine.UI.Image));
        var cameraObject = new GameObject("Now World Camera");
        var panelObject = new GameObject("Now World Panel");

        try
        {
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            blockerObject.transform.SetParent(canvasObject.transform, false);
            var blockerRect = blockerObject.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.zero;
            blockerRect.pivot = Vector2.zero;
            blockerRect.anchoredPosition = Vector2.zero;
            blockerRect.sizeDelta = new Vector2(200, 200);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.pixelRect = new Rect(0, 0, 200, 200);
            cameraObject.transform.position = new Vector3(0, 0, -5);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.facingMode = NowWorldFacingMode.None;
            panel.targetCamera = camera;
            panel.size = new Vector2(100f, 50f);
            panel.pixelsPerUnit = 100f;
            panel.pivot = new Vector2(0.5f, 0.5f);

            yield return null;

            var provider = panel.GetWorldInputProviderForTest();
            var surface = new NowInputSurface(panel.size);
            var press = new NowMouseInput
            {
                hasPointer = true,
                screenPosition = new Vector2(100, 100),
                pointerButtonsDown = NowPointerButtons.Primary,
                pointerButtonsPressed = NowPointerButtons.Primary
            };

            Assert.IsTrue(provider.TryGetSnapshot(surface, press, out var blockedSnapshot));
            Assert.IsFalse(blockedSnapshot.hasPointer, "Screen-space UGUI must block world-space NowUI input beneath it.");

            panel.blockedWhenPointerOverUGUI = false;
            provider = panel.GetWorldInputProviderForTest();
            provider.ResetPosition();

            Assert.IsTrue(provider.TryGetSnapshot(surface, press, out var allowedSnapshot));
            Assert.IsTrue(allowedSnapshot.hasPointer, "World-space NowUI can opt out of screen-space UGUI blocking.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [UnityTest]
    public IEnumerator EventSystemSelectionSuspendsNowFocus()
    {
        var eventSystemObject = new GameObject("TestEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var selectable = new GameObject("Selected");

        try
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Assert.NotNull(eventSystem, "EventSystem must be live in play mode.");

            NowResolvedId firstFocus = NowResolvedId
                .CreateOwnerRoot(0x52454E444552464FUL)
                .Child(7);
            NowResolvedId registeredFocus = firstFocus.Child(1);
            NowResolvedId secondFocus = firstFocus.Child(9);

            NowFocus.Focus(firstFocus);
            eventSystem.SetSelectedGameObject(selectable);
            Assert.NotNull(eventSystem.currentSelectedGameObject);

            yield return null;
            NowFocus.Register(registeredFocus, new NowRect(0, 0, 10, 10));

            Assert.AreEqual(
                NowResolvedId.None,
                NowFocus.focusedResolvedId,
                "UGUI selection must clear NowUI focus.");

            eventSystem.SetSelectedGameObject(selectable);
            NowFocus.Focus(secondFocus);
            Assert.IsNull(eventSystem.currentSelectedGameObject, "NowUI focus must deselect the EventSystem.");
            Assert.AreEqual(secondFocus, NowFocus.focusedResolvedId);
        }
        finally
        {
            NowFocus.Reset();
            Object.DestroyImmediate(selectable);
            Object.DestroyImmediate(eventSystemObject);
        }
    }
#endif

    /// <summary>Draws through the legacy GL/DrawMeshNow camera path on OnPostRender.</summary>
    class GLPathDriver : MonoBehaviour
    {
        public bool drew;

        void OnPostRender()
        {
            using (Now.StartUI(new NowRect(0, 0, Side, Side)))
            {
                Now.Rectangle(new NowRect(32, 32, 64, 64))
                    .SetColor(Color.green)
                    .Draw();
            }

            drew = true;
        }
    }

    [UnityTest]
    public IEnumerator CameraGLPathRendersThroughOnPostRender()
    {
        var go = new GameObject("Now GL Test Camera");

        try
        {
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 0;
            camera.targetTexture = _target;

            var driver = go.AddComponent<GLPathDriver>();

            camera.Render();
            yield return null;

            Assert.IsTrue(driver.drew, "OnPostRender did not run.");

            var pixels = ReadPixels(_target);
            int green = CountPixels(pixels, p => p.g > 150 && p.a > 128);

            Assert.Greater(green, 64 * 64 - 600, "GL path rendered too few pixels.");
            Assert.Greater(pixels[(Side / 2) * Side + Side / 2].g, 150);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    class WorldPanelDriver : NowWorldGraphic
    {
        public Color color = Color.red;

        public NowWorldInputProvider GetWorldInputProviderForTest()
        {
            return (NowWorldInputProvider)GetInputProvider();
        }

        protected override void DrawNowUI(NowRect rect)
        {
            Now.Rectangle(rect)
                .SetColor(color)
                .Draw();
        }
    }

    [UnityTest]
    public IEnumerator WorldGraphicRendersThroughCamera()
    {
        var cameraObject = new GameObject("Now World Test Camera");
        var panelObject = new GameObject("Now World Test Panel");

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = _target;
            cameraObject.transform.position = new Vector3(0f, 0f, -2f);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.targetCamera = camera;
            panel.size = new Vector2(64f, 64f);
            panel.pixelsPerUnit = 100f;
            panel.depthMode = NowWorldDepthMode.AlwaysVisible;
            panel.RebuildNowUI();

            camera.Render();
            yield return null;

            var pixels = ReadPixels(_target);
            int red = CountPixels(pixels, p => p.r > 180 && p.g < 60 && p.a > 180);

            Assert.Greater(red, 1000, "World-space NowUI produced too few red pixels.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [UnityTest]
    public IEnumerator WorldGraphicDepthModeCanRespectSceneDepth()
    {
        var cameraObject = new GameObject("Now World Depth Camera");
        var panelObject = new GameObject("Now World Depth Panel");
        var blockerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.targetTexture = _target;
            cameraObject.transform.position = new Vector3(0f, 0f, -2f);

            blockerObject.name = "Now World Depth Blocker";
            blockerObject.transform.position = new Vector3(0f, 0f, -0.2f);
            blockerObject.transform.localScale = new Vector3(1.4f, 1.4f, 0.1f);

            var panel = panelObject.AddComponent<WorldPanelDriver>();
            panel.targetCamera = camera;
            panel.size = new Vector2(64f, 64f);
            panel.pixelsPerUnit = 100f;

            panel.depthMode = NowWorldDepthMode.SceneOccluded;
            panel.RebuildNowUI();
            camera.Render();
            yield return null;

            int occludedRed = CountPixels(ReadPixels(_target), p => p.r > 180 && p.g < 60 && p.a > 180);

            panel.depthMode = NowWorldDepthMode.AlwaysVisible;
            panel.RebuildNowUI();
            camera.Render();
            yield return null;

            int visibleRed = CountPixels(ReadPixels(_target), p => p.r > 180 && p.g < 60 && p.a > 180);

            Assert.Less(occludedRed, 200, "Scene-occluded world UI should be hidden by nearer geometry.");
            Assert.Greater(visibleRed, 1000, "Always-visible world UI should draw over nearer geometry.");
        }
        finally
        {
            Object.DestroyImmediate(blockerObject);
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
