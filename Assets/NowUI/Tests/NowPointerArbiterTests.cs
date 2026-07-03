using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowPointerArbiterTests
{
    readonly object _world = new object();
    readonly object _screen = new object();
    readonly object _canvas = new object();

    [SetUp]
    public void SetUp()
    {
        NowPointerArbiter.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        NowPointerArbiter.Reset();
    }

    [Test]
    public void EveryoneOwnsThePointerWhenNothingClaims()
    {
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_world));
        Assert.IsTrue(NowPointerArbiter.IsOwner(_screen));
    }

    [Test]
    public void HigherTierClaimWinsOwnership()
    {
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: true, buttonsDown: false);
        NowPointerArbiter.Claim(_screen, NowPointerArbiter.TierScreen, 0f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_screen));
        Assert.IsFalse(NowPointerArbiter.IsOwner(_world));
    }

    [Test]
    public void CanvasTierBeatsScreenAndWorld()
    {
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: true, buttonsDown: false);
        NowPointerArbiter.Claim(_screen, NowPointerArbiter.TierScreen, 0f, hit: true, buttonsDown: false);
        NowPointerArbiter.Claim(_canvas, NowPointerArbiter.TierCanvas, 0f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_canvas));
        Assert.IsFalse(NowPointerArbiter.IsOwner(_screen));
        Assert.IsFalse(NowPointerArbiter.IsOwner(_world));
    }

    [Test]
    public void SmallerDepthWinsWithinTheSameTier()
    {
        var near = new object();
        var far = new object();

        NowPointerArbiter.Claim(far, NowPointerArbiter.TierWorld, 8f, hit: true, buttonsDown: false);
        NowPointerArbiter.Claim(near, NowPointerArbiter.TierWorld, 3f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(near));
        Assert.IsFalse(NowPointerArbiter.IsOwner(far));
    }

    [Test]
    public void MissedClaimsNeverWin()
    {
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: false, buttonsDown: false);
        NowPointerArbiter.Claim(_screen, NowPointerArbiter.TierScreen, 0f, hit: false, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_world));
        Assert.IsTrue(NowPointerArbiter.IsOwner(_screen));
    }

    [Test]
    public void LaterClaimWithTheSameKeyReplacesTheEarlierOne()
    {
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: true, buttonsDown: false);
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: false, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_screen), "A replaced miss must not hold ownership.");
    }

    [Test]
    public void OwnershipLatchesWhileTheWinnerHoldsAButton()
    {
        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();
        Assert.IsTrue(NowPointerArbiter.IsOwner(_world));

        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: false, buttonsDown: true);
        NowPointerArbiter.Claim(_canvas, NowPointerArbiter.TierCanvas, 0f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_world), "A drag in progress must keep pointer ownership.");
        Assert.IsFalse(NowPointerArbiter.IsOwner(_canvas));

        NowPointerArbiter.Claim(_world, NowPointerArbiter.TierWorld, 2f, hit: false, buttonsDown: false);
        NowPointerArbiter.Claim(_canvas, NowPointerArbiter.TierCanvas, 0f, hit: true, buttonsDown: false);
        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.IsOwner(_canvas), "Ownership transfers once the drag releases.");
        Assert.IsFalse(NowPointerArbiter.IsOwner(_world));
    }

    [Test]
    public void ContentFootprintAnswersForThePreviousFrame()
    {
        NowPointerArbiter.NoteContent(_screen, new Rect(10f, 10f, 100f, 40f));

        Assert.IsFalse(NowPointerArbiter.HadContentAt(_screen, new Vector2(20f, 20f)), "Content registers for the next frame.");

        NowPointerArbiter.ForceNewFrame();

        Assert.IsTrue(NowPointerArbiter.HadContentAt(_screen, new Vector2(20f, 20f)));
        Assert.IsFalse(NowPointerArbiter.HadContentAt(_screen, new Vector2(200f, 20f)));
        Assert.IsFalse(NowPointerArbiter.HadContentAt(_world, new Vector2(20f, 20f)), "Footprints are per surface.");
    }
}
