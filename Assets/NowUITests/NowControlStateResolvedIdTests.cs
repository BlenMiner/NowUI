using NUnit.Framework;
using NowUI;

public class NowControlStateResolvedIdTests
{
    [SetUp]
    public void SetUp()
    {
        NowControlState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowControlState.Reset();
    }

    [Test]
    public void ResolvedStateSlotsWithTheSameAuthoredPathAreIsolatedByOwner()
    {
        NowResolvedId first = ControlId(101UL, 17);
        NowResolvedId second = ControlId(202UL, 17);

        NowControlState.Get<int>(first) = 11;
        NowControlState.Get<int>(second) = 22;

        Assert.That(Read(first), Is.EqualTo(11));
        Assert.That(Read(second), Is.EqualTo(22));
    }

    [Test]
    public void ResolvedStateSlotsPreserveTheSourceIdentityDomain()
    {
        NowResolvedId owner = NowResolvedId.CreateOwnerRoot(303UL);
        NowResolvedId control = owner.Derive(NowIdDomain.Control, 17);
        NowResolvedId layout = owner.Derive(NowIdDomain.Layout, 17);

        NowControlState.Get<int>(control) = 31;
        NowControlState.Get<int>(layout) = 32;

        Assert.That(Read(control), Is.EqualTo(31));
        Assert.That(Read(layout), Is.EqualTo(32));
    }

    [Test]
    public void ResolvedNamedStateSlotsAreIsolatedByOwnerAndName()
    {
        NowResolvedId first = ControlId(404UL, 17);
        NowResolvedId second = ControlId(505UL, 17);

        NowControlState.Get<int>(first, "scroll") = 41;
        NowControlState.Get<int>(first, "selection") = 42;
        NowControlState.Get<int>(second, "scroll") = 43;

        Assert.That(Read(first, "scroll"), Is.EqualTo(41));
        Assert.That(Read(first, "selection"), Is.EqualTo(42));
        Assert.That(Read(second, "scroll"), Is.EqualTo(43));
    }

    static NowResolvedId ControlId(ulong ownerNonce, int authoredId)
    {
        return NowResolvedId.CreateOwnerRoot(ownerNonce)
            .Derive(NowIdDomain.Control, authoredId);
    }

    static int Read(NowResolvedId id)
    {
        return NowControlState.Get<int>(id);
    }

    static int Read(NowResolvedId id, string key)
    {
        return NowControlState.Get<int>(id, key);
    }
}
