using NUnit.Framework;
using NowUI.Editor;

public class NowHarnessAnimationTests
{
    [Test]
    public void FrameTimeIsDerivedOnlyFromFrameIndexAndRate()
    {
        var frame = new NowHarnessAnimationFrame(index: 17, count: 60, framesPerSecond: 30f);

        Assert.AreEqual(17, frame.index);
        Assert.AreEqual(60, frame.count);
        Assert.AreEqual(17f / 30f, frame.timeSeconds, 0.000001f);
        Assert.AreEqual(1f / 30f, frame.deltaTimeSeconds, 0.000001f);
        Assert.AreEqual(2f, frame.durationSeconds, 0.000001f);
        Assert.AreEqual(17f / 60f, frame.normalizedTime, 0.000001f);
    }

    [Test]
    public void LoopTimingDoesNotDuplicateTheFirstFrameAtTheEnd()
    {
        var finalFrame = new NowHarnessAnimationFrame(index: 59, count: 60, framesPerSecond: 30f);

        Assert.Less(finalFrame.normalizedTime, 1f);
        Assert.AreEqual(59f / 60f, finalFrame.normalizedTime, 0.000001f);
        Assert.AreEqual(59f / 30f, finalFrame.timeSeconds, 0.000001f);
    }

    [Test]
    public void ScenarioValidationRejectsUnsafeOutputNames()
    {
        var scenario = new NowHarnessAnimationScenario(
            "../outside",
            640,
            360,
            30,
            30f,
            (rect, frame) => { });

        Assert.Throws<System.InvalidOperationException>(() => scenario.Validate());
    }

    [Test]
    public void ReadmeShowcasesAreRegisteredAsFourSecondLoops()
    {
        var scenarios = NowHarnessAnimationScenarios.All();

        Assert.AreEqual(3, scenarios.Count);
        Assert.AreEqual("sdf-metamorphosis", scenarios[0].name);
        Assert.AreEqual("desktop-fidelity", scenarios[1].name);
        Assert.AreEqual("sdf-shader-xray", scenarios[2].name);

        for (int i = 0; i < scenarios.Count; ++i)
        {
            Assert.AreEqual(960, scenarios[i].width, scenarios[i].name);
            Assert.AreEqual(540, scenarios[i].height, scenarios[i].name);
            Assert.AreEqual(96, scenarios[i].frameCount, scenarios[i].name);
            Assert.AreEqual(24f, scenarios[i].framesPerSecond, scenarios[i].name);
            Assert.AreEqual(4f, scenarios[i].frameCount / scenarios[i].framesPerSecond, scenarios[i].name);
        }
    }
}
