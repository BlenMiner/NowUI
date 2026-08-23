using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowIdentityIntegrationTests
{
    sealed class Provider : INowInputProvider
    {
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            snapshot = default;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(640f, 480f);

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowControls.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowInput.Reset();
        NowControls.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
    }

    [Test]
    public void InputProvidersOwnIndependentIdentityRoots()
    {
        var firstProvider = new Provider();
        var secondProvider = new Provider();

        NowResolvedId first = Resolve(firstProvider, "shared");
        NowResolvedId repeated = Resolve(firstProvider, "shared");
        NowResolvedId second = Resolve(secondProvider, "shared");

        Assert.AreEqual(first, repeated);
        Assert.AreNotEqual(first, second);
    }

    [Test]
    public void ExplicitKeyedListSurvivesReordering()
    {
        var provider = new Provider();
        var first = ResolveItems(provider, "alpha", "beta", "gamma");
        var reordered = ResolveItems(provider, "gamma", "alpha", "beta");

        Assert.AreEqual(first["alpha"], reordered["alpha"]);
        Assert.AreEqual(first["beta"], reordered["beta"]);
        Assert.AreEqual(first["gamma"], reordered["gamma"]);
    }

    [Test]
    public void AutomaticKeyedListsAreIsolatedByCallSite()
    {
        var provider = new Provider();
        NowResolvedId first;
        NowResolvedId second;

        using (NowInput.Begin(provider, Surface))
        {
            using (NowControls.KeyedItem("same-key", file: "first-list.cs", line: 10))
                first = NowControls.GetControlId("row");

            using (NowControls.KeyedItem("same-key", file: "second-list.cs", line: 10))
                second = NowControls.GetControlId("row");
        }

        Assert.AreNotEqual(first, second);
    }

    [Test]
    public void ExplicitKeyedListMatchesAcrossHelpersAndPassiveReplay()
    {
        var provider = new Provider();
        NowResolvedId first;
        NowResolvedId helper;
        NowResolvedId passive;

        using (NowInput.Begin(provider, Surface))
        {
            first = ResolveExplicitItem("inventory", "sword");
            helper = ResolveExplicitItemFromHelper("inventory", "sword");

            NowInput.BeginPassive();

            try
            {
                passive = ResolveExplicitItem("inventory", "sword");
            }
            finally
            {
                NowInput.EndPassive();
            }
        }

        Assert.AreEqual(first, helper);
        Assert.AreEqual(first, passive);
    }

    [Test]
    public void KeyedItemsRejectMissingDomainKeys()
    {
        var provider = new Provider();

        using (NowInput.Begin(provider, Surface))
        {
            Assert.Throws<ArgumentException>(() => NowControls.KeyedItem(NowId.None));
            Assert.Throws<ArgumentException>(() => NowControls.KeyedItemIn(NowId.None, "item"));
            Assert.Throws<ArgumentException>(() => NowControls.KeyedItemIn("list", NowId.None));
        }
    }

    [Test]
    public void TreeSemanticKeysAreStableAndHierarchical()
    {
        NowTreeNodeKey root = NowTreeNodeKey.From("root");
        NowTreeNodeKey child = root.Child("child");

        Assert.AreEqual(NowTreeNodeKey.From("root"), root);
        Assert.AreEqual(NowTreeNodeKey.From("root").Child("child"), child);
        Assert.AreNotEqual(NowTreeNodeKey.From("child").Child("root"), child);
        Assert.AreNotEqual(root, child);
    }

    static NowResolvedId Resolve(Provider provider, string id)
    {
        using (NowInput.Begin(provider, Surface))
            return NowControls.GetControlId(id);
    }

    static Dictionary<string, NowResolvedId> ResolveItems(Provider provider, params string[] keys)
    {
        var result = new Dictionary<string, NowResolvedId>(keys.Length);

        using (NowInput.Begin(provider, Surface))
        {
            for (int i = 0; i < keys.Length; ++i)
            {
                string key = keys[i];

                using (NowControls.KeyedItemIn("inventory", key))
                    result[key] = NowControls.GetControlId("delete");
            }
        }

        return result;
    }

    static NowResolvedId ResolveExplicitItem(NowId list, NowId key)
    {
        using (NowControls.KeyedItemIn(list, key))
            return NowControls.GetControlId("row");
    }

    static NowResolvedId ResolveExplicitItemFromHelper(NowId list, NowId key)
    {
        using (NowControls.KeyedItemIn(list, key))
            return NowControls.GetControlId("row");
    }
}
