using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NowUI;

public class NowResolvedIdTests
{
    [Test]
    public void DefaultIsTheOnlyEmptySentinel()
    {
        var root = NowResolvedId.CreateOwnerRoot(1UL);

        Assert.IsFalse(NowResolvedId.None.hasValue);
        Assert.AreEqual("0000000000000000", NowResolvedId.None.ToString());
        Assert.IsTrue(root.hasValue);
        Assert.AreNotEqual(NowResolvedId.None, root);
        Assert.Throws<ArgumentOutOfRangeException>(() => NowResolvedId.CreateOwnerRoot(0UL));
        Assert.Throws<ArgumentException>(() => NowResolvedId.None.Child("child"));
        Assert.Throws<ArgumentException>(() => root.Child(NowId.None));
        Assert.Throws<ArgumentException>(() => new NowControlIdentity(NowResolvedId.None));
    }

    [Test]
    public void OwnerRootsAreDeterministicAndSeparated()
    {
        var first = NowResolvedId.CreateOwnerRoot(1UL);
        var repeated = NowResolvedId.CreateOwnerRoot(1UL);
        var second = NowResolvedId.CreateOwnerRoot(2UL);

        Assert.AreEqual(first, repeated);
        Assert.AreEqual(first.GetHashCode(), repeated.GetHashCode());
        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(first.Child("control"), second.Child("control"));
    }

    [Test]
    public void HashFormatHasStableGoldenVectors()
    {
        var root = NowResolvedId.CreateOwnerRoot(1UL);
        var site = NowIdHash.DeriveCallSite(
            root,
            NowIdDomain.Control,
            "Widgets/Panel.cs",
            120);

        Assert.AreEqual("026883B751DD4027", root.ToString());
        Assert.AreEqual("4695744ADA39C176", NowResolvedId.CreateOwnerRoot(2UL).ToString());
        Assert.AreEqual("629567307FD145E8", root.Child("alpha").ToString());
        Assert.AreEqual("94D6980370FEF480", root.Child(42).ToString());
        Assert.AreEqual("B946F5533C76B08E", root.Derive(NowIdDomain.Control, "alpha").ToString());
        Assert.AreEqual("037430F41F9077BE", root.Derive(NowIdDomain.Layout, "alpha").ToString());
        Assert.AreEqual("DE6B8D395346D89A", root.InDomain(NowIdDomain.State).ToString());
        Assert.AreEqual("7578D3AFD4926658", site.ToString());
        Assert.AreEqual("6FB3E4D40ACFFF86", NowIdHash.DeriveOccurrence(site, 1).ToString());
    }

    [Test]
    public void SegmentTypesAreSeparated()
    {
        var root = NowResolvedId.CreateOwnerRoot(41UL);

        Assert.AreNotEqual(root.Child(42), root.Child("42"));
        Assert.AreNotEqual(root.Child(-1), root.Child(1));
        Assert.IsTrue(root.Child(0).hasValue);
        Assert.AreNotEqual(root.Child(0), root.Child(1));
        Assert.Throws<ArgumentNullException>(() => root.Child((string)null));
        Assert.Throws<ArgumentException>(() => root.Child(string.Empty));
        Assert.Throws<ArgumentException>(() => root.Child(default(NowId)));
    }

    [Test]
    public void SubsystemDomainsAreSeparated()
    {
        var root = NowResolvedId.CreateOwnerRoot(7UL);
        var control = root.Derive(NowIdDomain.Control, "item");
        var layout = root.Derive(NowIdDomain.Layout, "item");
        var state = root.Derive(NowIdDomain.State, "item");
        var overlay = root.Derive(NowIdDomain.Overlay, "item");

        Assert.AreNotEqual(control, layout);
        Assert.AreNotEqual(control, state);
        Assert.AreNotEqual(control, overlay);
        Assert.AreNotEqual(layout, state);
        Assert.AreNotEqual(layout, overlay);
        Assert.AreNotEqual(state, overlay);
        Assert.AreNotEqual(root.InDomain(NowIdDomain.State), state);
        Assert.AreNotEqual(root.InDomain(NowIdDomain.State), root.InDomain(NowIdDomain.Overlay));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.InDomain(NowIdDomain.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.InDomain(NowIdDomain.OwnerRoot));
    }

    [Test]
    public void ParentAndPathOrderAreSeparated()
    {
        var root = NowResolvedId.CreateOwnerRoot(99UL);
        var parentA = root.Child("parent-a");
        var parentB = root.Child("parent-b");

        Assert.AreNotEqual(parentA.Child("leaf"), parentB.Child("leaf"));
        Assert.AreNotEqual(
            root.Child("first").Child("second"),
            root.Child("second").Child("first"));
    }

    [Test]
    public void RepeatedSegmentsCannotCancelAncestry()
    {
        var root = NowResolvedId.CreateOwnerRoot(1234UL);
        var once = root.Child(17);
        var twice = once.Child(17);
        var alternating = root.Child("a").Child("b").Child("a").Child("b");

        Assert.AreNotEqual(root, once);
        Assert.AreNotEqual(root, twice);
        Assert.AreNotEqual(once, twice);
        Assert.AreNotEqual(root, alternating);
        Assert.AreNotEqual(root.Child("a"), root.Child("a").Child("b").Child("b"));
    }

    [Test]
    public void CallSitesAndOccurrencesHaveTypedStablePaths()
    {
        var root = NowResolvedId.CreateOwnerRoot(55UL);
        var site = NowIdHash.DeriveCallSite(root, NowIdDomain.Control, "Widgets/Panel.cs", 120);
        var repeatedSite = NowIdHash.DeriveCallSite(root, NowIdDomain.Control, "Widgets/Panel.cs", 120);

        Assert.AreEqual(site, repeatedSite);
        Assert.AreNotEqual(site, NowIdHash.DeriveCallSite(root, NowIdDomain.Control, "Widgets/Panel.cs", 121));
        Assert.AreNotEqual(site, NowIdHash.DeriveCallSite(root, NowIdDomain.Layout, "Widgets/Panel.cs", 120));
        Assert.AreNotEqual(site, root.Derive(NowIdDomain.Control, 120));
        Assert.AreNotEqual(site, NowIdHash.DeriveOccurrence(site, 1));
        Assert.AreNotEqual(
            NowIdHash.DeriveOccurrence(site, 1),
            NowIdHash.DeriveOccurrence(site, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NowIdHash.DeriveOccurrence(site, 0));
    }

    [Test]
    public void LegacyValuesAreDeterministicAndIsolated()
    {
        var legacy = NowResolvedId.FromLegacy(42);
        var owner = NowResolvedId.CreateOwnerRoot(42UL);

        Assert.AreEqual(NowResolvedId.None, NowResolvedId.FromLegacy(0));
        Assert.AreEqual(legacy, NowResolvedId.FromLegacy(42));
        Assert.AreNotEqual(legacy, NowResolvedId.FromLegacy(43));
        Assert.AreNotEqual(legacy, owner.Child(42));
        Assert.AreNotEqual(legacy, owner.Derive(NowIdDomain.Legacy, 42));
    }

    [Test]
    public void PublicRawIntegerIdentityAdaptersAreCompilerErrors()
    {
        AssertHardObsoleteIntegerMethods(typeof(NowControlState));
        AssertHardObsoleteIntegerMethods(typeof(NowFocus));
        AssertHardObsoleteIntegerMethods(typeof(NowControls), "Interact");
        AssertHardObsoleteIntegerMethods(typeof(NowTooltip), "For");

        PropertyInfo focusedId = typeof(NowFocus).GetProperty(
            "focusedId",
            BindingFlags.Public | BindingFlags.Static);
        AssertHardObsolete(focusedId);
    }

    [Test]
    public void PublicCallSiteBoundaryUsesAnOpaqueTypedToken()
    {
        MethodInfo siteFactory = typeof(NowControls).GetMethod(
            "SiteId",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(int) },
            null);

        Assert.NotNull(siteFactory);
        Assert.AreEqual(typeof(NowCallSiteId), siteFactory.ReturnType);

        NowCallSiteId first = NowControls.SiteId("Widgets/Panel.cs", 120);
        NowCallSiteId repeated = NowControls.SiteId("Widgets/Panel.cs", 120);
        NowCallSiteId other = NowControls.SiteId("Widgets/Panel.cs", 121);

        Assert.IsTrue(first.hasValue);
        Assert.AreEqual(first, repeated);
        Assert.AreNotEqual(first, other);
        Assert.IsFalse(default(NowCallSiteId).hasValue);

        AssertNoPublicIntegerFallback(typeof(NowControls));
        AssertNoPublicIntegerFallback(typeof(NowInput));
        AssertNoPublicIntegerFallback(typeof(NowControlIdentity));
    }

    [Test]
    public void IdentityBearingBuildersAcceptAlreadyResolvedIdentity()
    {
        Type modifier = typeof(NowModifierBuilder<NowWaveDeformer>);
        Type snapshot = typeof(NowSnapshotBuilder);
        Type vector = typeof(NowVectorField);

        Assert.NotNull(modifier.GetMethod("SetId", new[] { typeof(NowResolvedId) }));
        Assert.NotNull(snapshot.GetMethod("SetId", new[] { typeof(NowResolvedId) }));
        Assert.NotNull(vector.GetMethod("SetId", new[] { typeof(NowId) }));
        Assert.NotNull(vector.GetMethod("SetId", new[] { typeof(NowResolvedId) }));

        AssertResolvedVectorFactories(typeof(Now), hasRect: true);
        AssertResolvedVectorFactories(typeof(NowLayout), hasRect: false);
    }

    [Test]
    public void PracticalGeneratedCorpusHasNoCollisions()
    {
        const int ownerCount = 16;
        const int count = 250000;
        var owners = new NowResolvedId[ownerCount];
        var ids = new HashSet<NowResolvedId>(count);

        for (int i = 0; i < owners.Length; ++i)
            owners[i] = NowResolvedId.CreateOwnerRoot((ulong)(i + 1));

        for (int i = 0; i < count; ++i)
        {
            var owner = owners[i & (ownerCount - 1)];
            var scope = owner.Derive(NowIdDomain.Scope, (i >> 4) + 1);
            var domain = (i & 1) == 0 ? NowIdDomain.Control : NowIdDomain.Layout;
            var id = scope.Derive(domain, i + 1);

            if (!id.hasValue)
                Assert.Fail($"Generated identity {i} resolved to the empty sentinel.");

            if (!ids.Add(id))
                Assert.Fail($"Collision at generated identity {i}: {id}");
        }

        Assert.AreEqual(count, ids.Count);
    }

    static void AssertHardObsoleteIntegerMethods(Type type, string methodName = null)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        int found = 0;

        for (int i = 0; i < methods.Length; ++i)
        {
            MethodInfo method = methods[i];
            ParameterInfo[] parameters = method.GetParameters();

            if ((methodName == null || method.Name == methodName) &&
                parameters.Length > 0 &&
                parameters[0].ParameterType == typeof(int))
            {
                ++found;
                AssertHardObsolete(method);
            }
        }

        Assert.Greater(found, 0, $"Expected a raw integer adapter on {type.Name}.");
    }

    static void AssertNoPublicIntegerFallback(Type type)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        for (int i = 0; i < methods.Length; ++i)
        {
            ParameterInfo[] parameters = methods[i].GetParameters();

            for (int p = 0; p < parameters.Length; ++p)
            {
                ParameterInfo parameter = parameters[p];

                if (parameter.ParameterType == typeof(int) &&
                    parameter.Name != null &&
                    parameter.Name.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Assert.Fail(
                        $"{type.Name}.{methods[i].Name} still exposes raw integer call-site fallback '{parameter.Name}'.");
                }
            }
        }
    }

    static void AssertResolvedVectorFactories(Type owner, bool hasRect)
    {
        string[] names =
        {
            "Vector2Field",
            "Vector3Field",
            "Vector4Field",
            "Vector2IntField",
            "Vector3IntField"
        };

        Type[] parameters = hasRect
            ? new[] { typeof(NowRect), typeof(NowResolvedId), typeof(string), typeof(int) }
            : new[] { typeof(NowResolvedId), typeof(string), typeof(int) };

        for (int i = 0; i < names.Length; ++i)
        {
            Assert.NotNull(
                owner.GetMethod(names[i], parameters),
                $"{owner.Name}.{names[i]} must accept NowResolvedId directly.");
        }
    }

    static void AssertHardObsolete(MemberInfo member)
    {
        Assert.NotNull(member);
        var attribute = member.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(attribute, $"{member.DeclaringType?.Name}.{member.Name} must be obsolete.");
        Assert.IsTrue(attribute.IsError, $"{member.DeclaringType?.Name}.{member.Name} must be a compile-time error.");
    }
}
