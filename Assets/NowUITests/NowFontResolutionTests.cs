using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NowUI;
using UnityEngine;
using Object = UnityEngine.Object;

public class NowFontResolutionTests
{
    // Inheriting NowFont also guards against bypassing virtual font selection
    // merely because an asset is itself a NowFont.
    sealed class TrackingFont : NowFont
    {
        public NowFont regularFont;
        public NowFont boldFont;
        public bool throwOnResolve;
        public readonly List<NowFontStyle> requests = new List<NowFontStyle>();

        protected override bool TryGetOwnFont(NowFontStyle style, out NowFont font)
        {
            requests.Add(style);

            if (throwOnResolve)
                throw new InvalidOperationException("Font resolution failed.");

            font = style == NowFontStyle.Regular ? regularFont : boldFont;
            return !ReferenceEquals(font, null);
        }
    }

    readonly List<NowFontAsset> _assets = new List<NowFontAsset>();
    NowFont _first;
    NowFont _second;

    [SetUp]
    public void SetUp()
    {
        _first = CreateFont(1.5f, 1.25f);
        _second = CreateFont(2.5f, 2.25f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets)
        {
            if (asset != null)
                Object.DestroyImmediate(asset);
        }

        _assets.Clear();
    }

    T Create<T>() where T : NowFontAsset
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _assets.Add(asset);
        return asset;
    }

    NowFont CreateFont(float lineHeight, float ascender)
    {
        var font = Create<NowFont>();
        font.atlasInfo = new NowFontAtlasInfo
        {
            metrics = new NowFontAtlasInfo.Metrics { lineHeight = lineHeight, ascender = ascender }
        };
        return font;
    }

    static void SetFallbacks(NowFontAsset asset, params NowFontAsset[] fallbacks)
    {
        typeof(NowFontAsset).GetField("_fallbacks", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(asset, fallbacks);
    }

    static void SetFamilyFont(NowFontFamily family, string field, NowFont font)
    {
        typeof(NowFontFamily).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(family, font);
    }

    static void AssertResolved(NowFontAsset asset, NowFontStyle style, NowFont expected)
    {
        Assert.IsTrue(asset.TryResolveFont(style, out var resolved));
        Assert.AreSame(expected, resolved);
        Assert.AreEqual(expected.GetLineHeight(), asset.GetLineHeight(style));
        Assert.AreEqual(expected.GetAscender(), asset.GetAscender(style));
    }

    [TestCase(NowFontStyle.Regular, "_regular")]
    [TestCase(NowFontStyle.Bold, "_bold")]
    [TestCase(NowFontStyle.Italic, "_italic")]
    [TestCase(NowFontStyle.BoldItalic, "_boldItalic")]
    public void FamilyResolvesOwnStyleBeforeFallbacks(NowFontStyle style, string field)
    {
        var family = Create<NowFontFamily>();
        SetFamilyFont(family, "_regular", _first);
        SetFamilyFont(family, field, _second);
        SetFallbacks(family, _first, family);

        AssertResolved(family, style, _second);
    }

    [Test]
    public void OwnFontResolutionUsesVirtualSelectionOnceWithoutVisitingFallbacks()
    {
        var asset = Create<TrackingFont>();
        var fallback = Create<TrackingFont>();
        asset.regularFont = _first;
        fallback.regularFont = _second;
        SetFallbacks(asset, asset, fallback);

        AssertResolved(asset, NowFontStyle.Regular, _first);

        CollectionAssert.AreEqual(new[]
        {
            NowFontStyle.Regular, NowFontStyle.Regular, NowFontStyle.Regular
        }, asset.requests);
        Assert.IsEmpty(fallback.requests);
    }

    [Test]
    public void RequestedStyleInFallbackPrecedesOwnRegularFont()
    {
        var asset = Create<TrackingFont>();
        var fallback = Create<TrackingFont>();
        asset.regularFont = _first;
        fallback.boldFont = _second;
        SetFallbacks(asset, fallback);

        AssertResolved(asset, NowFontStyle.Bold, _second);

        var expectedRequests = new[] { NowFontStyle.Bold, NowFontStyle.Bold, NowFontStyle.Bold };
        CollectionAssert.AreEqual(expectedRequests, asset.requests);
        CollectionAssert.AreEqual(expectedRequests, fallback.requests);
    }

    [Test]
    public void MissingRequestedStyleRetriesRegularAfterCyclicFallbackTraversal()
    {
        var asset = Create<TrackingFont>();
        var fallback = Create<TrackingFont>();
        asset.regularFont = _first;
        fallback.regularFont = _second;
        SetFallbacks(asset, fallback);
        SetFallbacks(fallback, asset);

        Assert.IsTrue(asset.TryResolveFont(NowFontStyle.Bold, out var resolved));
        Assert.AreSame(_first, resolved);
        CollectionAssert.AreEqual(new[] { NowFontStyle.Bold, NowFontStyle.Regular }, asset.requests);
        CollectionAssert.AreEqual(new[] { NowFontStyle.Bold }, fallback.requests);
    }

    [Test]
    public void CyclicFallbacksPreserveFontAndMetricTraversalOrder()
    {
        var asset = Create<TrackingFont>();
        var fallback = Create<TrackingFont>();
        SetFallbacks(asset, fallback, _second);
        SetFallbacks(fallback, asset);

        Assert.IsTrue(asset.TryResolveFont(NowFontStyle.Regular, out var resolved));
        Assert.AreSame(_second, resolved);
        // Metrics follow the first live fallback, including its cycle default;
        // they do not search later siblings for a different metric source.
        Assert.AreEqual(1f, asset.GetLineHeight(NowFontStyle.Regular));
        Assert.AreEqual(1f, asset.GetAscender(NowFontStyle.Regular));
        Assert.AreEqual(3, asset.requests.Count);
        Assert.AreEqual(3, fallback.requests.Count);
    }

    [Test]
    public void DestroyedOwnFontAndFallbackAreSkipped()
    {
        var asset = Create<TrackingFont>();
        var destroyedFallback = Create<TrackingFont>();
        asset.regularFont = _first;
        SetFallbacks(asset, destroyedFallback, _second);
        Object.DestroyImmediate(_first);
        Object.DestroyImmediate(destroyedFallback);

        AssertResolved(asset, NowFontStyle.Regular, _second);
        Assert.IsEmpty(destroyedFallback.requests);
        Assert.AreEqual(3, asset.requests.Count);
    }

    [Test]
    public void DestroyedRootReturnsDefaultsWithoutVirtualSelection()
    {
        var asset = Create<TrackingFont>();
        asset.regularFont = _first;
        SetFallbacks(asset, _second);
        Object.DestroyImmediate(asset);

        Assert.IsFalse(asset.TryResolveFont(NowFontStyle.Regular, out var resolved));
        Assert.IsNull(resolved);
        Assert.AreEqual(1f, asset.GetLineHeight(NowFontStyle.Regular));
        Assert.AreEqual(1f, asset.GetAscender(NowFontStyle.Regular));
        Assert.IsEmpty(asset.requests);
    }

    [Test]
    public void FamilyFontAndMetricChangesAreObservedOnTheNextCall()
    {
        var family = Create<NowFontFamily>();
        SetFamilyFont(family, "_regular", _first);
        AssertResolved(family, NowFontStyle.Regular, _first);

        SetFamilyFont(family, "_regular", _second);
        AssertResolved(family, NowFontStyle.Regular, _second);

        SetFamilyFont(family, "_regular", null);
        SetFallbacks(family, _first);
        AssertResolved(family, NowFontStyle.Regular, _first);
    }

    [Test]
    public void ThrowingFallbackDoesNotPoisonLaterResolution()
    {
        var asset = Create<TrackingFont>();
        var fallback = Create<TrackingFont>();
        fallback.throwOnResolve = true;
        SetFallbacks(asset, fallback);
        SetFallbacks(fallback, asset, _second);

        Assert.Throws<InvalidOperationException>(() => asset.TryResolveFont(NowFontStyle.Regular, out _));
        Assert.Throws<InvalidOperationException>(() => asset.GetLineHeight(NowFontStyle.Regular));
        Assert.Throws<InvalidOperationException>(() => asset.GetAscender(NowFontStyle.Regular));

        fallback.throwOnResolve = false;
        fallback.regularFont = _second;
        AssertResolved(asset, NowFontStyle.Regular, _second);
    }
}
