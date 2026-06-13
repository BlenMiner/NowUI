using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using NowUI;

/// <summary>
/// Baseline timings for the immediate-mode hot paths. These exist to catch
/// regressions in draw-call building cost, not to assert absolute numbers:
/// compare against previous runs in the Performance Test Report window.
/// </summary>
public class NowPerformanceTests
{
    const int RectanglesPerFrame = 1000;

    const int LabelsPerFrame = 100;

    [Test, Performance]
    public void RectangleFrameBuild()
    {
        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1920, 1080)))
                    {
                        for (int i = 0; i < RectanglesPerFrame; ++i)
                        {
                            Now.Rectangle(new NowRect((i * 7) % 1800, (i * 13) % 1000, 64, 32))
                                .SetColor(Color.white)
                                .SetRadius(4)
                                .Draw();
                        }
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
        }
    }

    [Test, Performance]
    public void TextFrameBuild()
    {
        Assert.NotNull(Now.defaultFont, "Default font resource is required for the text baseline.");

        var drawList = new NowDrawList();

        try
        {
            Measure.Method(() =>
                {
                    using (drawList.Begin(new Vector2(1920, 1080)))
                    {
                        for (int i = 0; i < LabelsPerFrame; ++i)
                        {
                            Now.Text(new NowRect(8, (i * 24) % 1000, 600, 24))
                                .SetFontSize(18)
                                .SetColor(Color.white)
                                .Draw("The quick brown fox jumps over 0123456789");
                        }
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .Run();
        }
        finally
        {
            drawList.Dispose();
        }
    }
}
