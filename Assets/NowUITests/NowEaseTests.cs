using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI;
using NowUI.Internal;

public class NowEaseTests
{
    const int Samples = 2000;

    static readonly (string name, Func<float, float> curve)[] AllFunctions =
    {
        ("Linear", NowEase.Linear),
        ("InSine", NowEase.InSine),
        ("OutSine", NowEase.OutSine),
        ("InOutSine", NowEase.InOutSine),
        ("InQuad", NowEase.InQuad),
        ("OutQuad", NowEase.OutQuad),
        ("InOutQuad", NowEase.InOutQuad),
        ("InCubic", NowEase.InCubic),
        ("OutCubic", NowEase.OutCubic),
        ("InOutCubic", NowEase.InOutCubic),
        ("InQuart", NowEase.InQuart),
        ("OutQuart", NowEase.OutQuart),
        ("InOutQuart", NowEase.InOutQuart),
        ("InQuint", NowEase.InQuint),
        ("OutQuint", NowEase.OutQuint),
        ("InOutQuint", NowEase.InOutQuint),
        ("InExpo", NowEase.InExpo),
        ("OutExpo", NowEase.OutExpo),
        ("InOutExpo", NowEase.InOutExpo),
        ("InCirc", NowEase.InCirc),
        ("OutCirc", NowEase.OutCirc),
        ("InOutCirc", NowEase.InOutCirc),
        ("InBack", t => NowEase.InBack(t)),
        ("OutBack", t => NowEase.OutBack(t)),
        ("InOutBack", t => NowEase.InOutBack(t)),
        ("InBack(3)", t => NowEase.InBack(t, 3f)),
        ("OutBack(3)", t => NowEase.OutBack(t, 3f)),
        ("InOutBack(NaN)", t => NowEase.InOutBack(t, float.NaN)),
        ("InElastic", t => NowEase.InElastic(t)),
        ("OutElastic", t => NowEase.OutElastic(t)),
        ("InOutElastic", t => NowEase.InOutElastic(t)),
        ("InElastic(1.5,0.2)", t => NowEase.InElastic(t, 1.5f, 0.2f)),
        ("OutElastic(1.5,0.2)", t => NowEase.OutElastic(t, 1.5f, 0.2f)),
        ("InOutElastic(0.5,-1)", t => NowEase.InOutElastic(t, 0.5f, -1f)),
        ("InBounce", NowEase.InBounce),
        ("OutBounce", NowEase.OutBounce),
        ("InOutBounce", NowEase.InOutBounce),
        ("Spring", t => NowEase.Spring(t)),
        ("Spring(0,3)", t => NowEase.Spring(t, 0f, 3f)),
        ("Spring(0.2,5)", t => NowEase.Spring(t, 0.2f, 5f)),
        ("Spring(1,2)", t => NowEase.Spring(t, 1f, 2f)),
        ("Spring(0.5,0)", t => NowEase.Spring(t, 0.5f, 0f)),
        ("Smoothstep", NowEase.Smoothstep),
        ("CubicBezier(ease)", t => NowEase.CubicBezier(0.25f, 0.1f, 0.25f, 1f, t)),
        ("CubicBezier(overshoot)", t => NowEase.CubicBezier(0.34f, 1.56f, 0.64f, 1f, t)),
        ("CubicBezier(out of range x)", t => NowEase.CubicBezier(-2f, 0.5f, 3f, 0.5f, t)),
    };

    static readonly NowEasing[] MonotonicEasings =
    {
        NowEasing.Linear,
        NowEasing.InSine, NowEasing.OutSine, NowEasing.InOutSine,
        NowEasing.InQuad, NowEasing.OutQuad, NowEasing.InOutQuad,
        NowEasing.InCubic, NowEasing.OutCubic, NowEasing.InOutCubic,
        NowEasing.InQuart, NowEasing.OutQuart, NowEasing.InOutQuart,
        NowEasing.InQuint, NowEasing.OutQuint, NowEasing.InOutQuint,
        NowEasing.InExpo, NowEasing.OutExpo, NowEasing.InOutExpo,
        NowEasing.InCirc, NowEasing.OutCirc, NowEasing.InOutCirc,
    };

    static readonly NowEasing[] SymmetricEasings =
    {
        NowEasing.Linear,
        NowEasing.InOutSine, NowEasing.InOutQuad, NowEasing.InOutCubic,
        NowEasing.InOutQuart, NowEasing.InOutQuint, NowEasing.InOutExpo,
        NowEasing.InOutCirc, NowEasing.InOutBack, NowEasing.InOutElastic,
        NowEasing.InOutBounce,
    };

    static readonly (NowEasing easeIn, NowEasing easeOut)[] MirroredPairs =
    {
        (NowEasing.InSine, NowEasing.OutSine),
        (NowEasing.InQuad, NowEasing.OutQuad),
        (NowEasing.InCubic, NowEasing.OutCubic),
        (NowEasing.InQuart, NowEasing.OutQuart),
        (NowEasing.InQuint, NowEasing.OutQuint),
        (NowEasing.InExpo, NowEasing.OutExpo),
        (NowEasing.InCirc, NowEasing.OutCirc),
        (NowEasing.InBack, NowEasing.OutBack),
        (NowEasing.InElastic, NowEasing.OutElastic),
        (NowEasing.InBounce, NowEasing.OutBounce),
    };

    static NowEasing[] AllEasings()
    {
        return (NowEasing[])Enum.GetValues(typeof(NowEasing));
    }

    static float Sample(int index)
    {
        return index / (float)Samples;
    }

    [Test]
    public void EveryNamedCurveHasExactEndpoints()
    {
        foreach (NowEasing easing in AllEasings())
        {
            Assert.AreEqual(0f, NowEase.Evaluate(easing, 0f), easing + " at 0");
            Assert.AreEqual(1f, NowEase.Evaluate(easing, 1f), easing + " at 1");
        }
    }

    [Test]
    public void EveryFunctionHasExactEndpoints()
    {
        foreach (var (name, curve) in AllFunctions)
        {
            Assert.AreEqual(0f, curve(0f), name + " at 0");
            Assert.AreEqual(1f, curve(1f), name + " at 1");
        }
    }

    [Test]
    public void InputIsClampedAndNaNIsTreatedAsZero()
    {
        float[] below = { -0f, -1e-30f, -0.5f, -1f, float.MinValue, float.NegativeInfinity, float.NaN };
        float[] above = { 1.0000001f, 1.5f, 2f, float.MaxValue, float.PositiveInfinity };

        foreach (var (name, curve) in AllFunctions)
        {
            foreach (float t in below)
                Assert.AreEqual(0f, curve(t), name + " at " + t);
            foreach (float t in above)
                Assert.AreEqual(1f, curve(t), name + " at " + t);
        }

        foreach (NowEasing easing in AllEasings())
        {
            Assert.AreEqual(0f, NowEase.Evaluate(easing, float.NaN), easing + " at NaN");
            Assert.AreEqual(0f, NowEase.Evaluate(easing, -3f), easing + " below range");
            Assert.AreEqual(1f, NowEase.Evaluate(easing, 3f), easing + " above range");
        }
    }

    [Test]
    public void UnknownEasingValueEvaluatesLinearly()
    {
        Assert.AreEqual(0.25f, NowEase.Evaluate((NowEasing)250, 0.25f));
    }

    [Test]
    public void MonotonicCurvesAreMonotonicAndStayInRange()
    {
        foreach (NowEasing easing in MonotonicEasings)
        {
            float previous = 0f;

            for (int i = 1; i <= Samples; ++i)
            {
                float value = NowEase.Evaluate(easing, Sample(i));
                Assert.GreaterOrEqual(value, previous - 1e-6f, easing + " decreases at " + Sample(i));
                Assert.GreaterOrEqual(value, 0f, easing + " below 0 at " + Sample(i));
                Assert.LessOrEqual(value, 1f, easing + " above 1 at " + Sample(i));
                previous = value;
            }
        }

        // A critically damped spring and the CSS keyword curves are monotonic too.
        // The shared bezier solver stops within 1e-4 of the requested x, so
        // neighbouring samples may differ by a few 1e-4 in y.
        AssertMonotonic("Spring(1,2)", t => NowEase.Spring(t, 1f, 2f), 1e-6f);
        AssertMonotonic("ease", t => NowEase.CubicBezier(0.25f, 0.1f, 0.25f, 1f, t), 5e-4f);
        AssertMonotonic("ease-in-out", t => NowEase.CubicBezier(0.42f, 0f, 0.58f, 1f, t), 5e-4f);
    }

    static void AssertMonotonic(string name, Func<float, float> curve, float tolerance)
    {
        float previous = 0f;

        for (int i = 1; i <= Samples; ++i)
        {
            float value = curve(Sample(i));
            Assert.GreaterOrEqual(value, previous - tolerance, name + " decreases at " + Sample(i));
            previous = value;
        }
    }

    [Test]
    public void OvershootingCurvesLeaveTheUnitRangeAndSettleOnOne()
    {
        AssertOvershoots("OutBack", t => NowEase.OutBack(t), expectAbove: true);
        AssertOvershoots("InOutBack", t => NowEase.InOutBack(t), expectAbove: true);
        AssertOvershoots("OutElastic", t => NowEase.OutElastic(t), expectAbove: true);
        AssertOvershoots("InOutElastic", t => NowEase.InOutElastic(t), expectAbove: true);
        AssertOvershoots("Spring", t => NowEase.Spring(t), expectAbove: true);
        AssertOvershoots("Spring(0.2,5)", t => NowEase.Spring(t, 0.2f, 5f), expectAbove: true);
        AssertOvershoots("CubicBezier(overshoot)", t => NowEase.CubicBezier(0.34f, 1.56f, 0.64f, 1f, t), expectAbove: true);
        AssertOvershoots("InBack", t => NowEase.InBack(t), expectAbove: false);
        AssertOvershoots("InElastic", t => NowEase.InElastic(t), expectAbove: false);

        // Larger overshoot parameters overshoot further.
        Assert.Greater(Max(t => NowEase.OutBack(t, 3f)), Max(t => NowEase.OutBack(t)));
        Assert.Greater(Max(t => NowEase.OutElastic(t, 1.5f)), Max(t => NowEase.OutElastic(t)));
        Assert.Greater(Max(t => NowEase.Spring(t, 0.2f)), Max(t => NowEase.Spring(t)));
    }

    static void AssertOvershoots(string name, Func<float, float> curve, bool expectAbove)
    {
        if (expectAbove)
            Assert.Greater(Max(curve), 1.01f, name + " should overshoot above 1");
        else
            Assert.Less(Min(curve), -0.01f, name + " should pull back below 0");

        // No visible jump onto the exact endpoints.
        Assert.AreEqual(0f, curve(0f), name + " at 0");
        Assert.AreEqual(1f, curve(1f), name + " at 1");
        Assert.AreEqual(0f, curve(1e-4f), 2e-3f, name + " just after 0");
        Assert.AreEqual(1f, curve(1f - 1e-4f), 2e-3f, name + " just before 1");
    }

    static float Max(Func<float, float> curve)
    {
        float max = float.MinValue;
        for (int i = 0; i <= Samples; ++i)
            max = Math.Max(max, curve(Sample(i)));
        return max;
    }

    static float Min(Func<float, float> curve)
    {
        float min = float.MaxValue;
        for (int i = 0; i <= Samples; ++i)
            min = Math.Min(min, curve(Sample(i)));
        return min;
    }

    [Test]
    public void SpringLandsOnOneWithoutAJump()
    {
        // Even an undamped spring, whose raw oscillation does not decay, is
        // blended onto 1 continuously.
        Func<float, float>[] springs =
        {
            t => NowEase.Spring(t),
            t => NowEase.Spring(t, 0f, 3.3f),
            t => NowEase.Spring(t, 0.1f, 1.7f),
        };

        foreach (var spring in springs)
        {
            for (int i = 1; i <= Samples; ++i)
            {
                float step = Math.Abs(spring(Sample(i)) - spring(Sample(i - 1)));
                Assert.Less(step, 0.05f, "spring step at " + Sample(i));
            }
        }

        Assert.AreEqual(0.4f, NowEase.Spring(0.4f, 0.5f, 0f), "non-positive frequency falls back to linear");
        Assert.AreEqual(0.4f, NowEase.Spring(0.4f, 0.5f, float.NaN), "NaN frequency falls back to linear");
    }

    [Test]
    public void InOutCurvesAreSymmetric()
    {
        foreach (NowEasing easing in SymmetricEasings)
        {
            for (int i = 0; i <= 200; ++i)
            {
                float t = i / 200f;
                float sum = NowEase.Evaluate(easing, t) + NowEase.Evaluate(easing, 1f - t);
                Assert.AreEqual(1f, sum, 1e-5f, easing + " symmetry at " + t);
            }

            Assert.AreEqual(0.5f, NowEase.Evaluate(easing, 0.5f), 1e-6f, easing + " midpoint");
        }
    }

    [Test]
    public void InCurvesMirrorTheirOutCurves()
    {
        foreach (var (easeIn, easeOut) in MirroredPairs)
        {
            for (int i = 0; i <= 200; ++i)
            {
                float t = i / 200f;
                float mirrored = 1f - NowEase.Evaluate(easeOut, 1f - t);
                Assert.AreEqual(mirrored, NowEase.Evaluate(easeIn, t), 1e-5f, easeIn + " vs " + easeOut + " at " + t);
            }
        }
    }

    [Test]
    public void CubicPolynomialsMatchTheirClosedForms()
    {
        for (int i = 0; i <= 100; ++i)
        {
            float t = i / 100f;
            float inverse = 1f - t;
            Assert.AreEqual(t * t * t, NowEase.InCubic(t), 1e-6f);
            Assert.AreEqual(1f - inverse * inverse * inverse, NowEase.OutCubic(t), 1e-6f);
            Assert.AreEqual(t * t, NowEase.InQuad(t), 1e-6f);
            Assert.AreEqual(t, NowEase.Linear(t));
        }

        Assert.AreEqual(1f + 2.70158f * 0.125f * -1f + 1.70158f * 0.25f, NowEase.OutBack(0.5f), 1e-6f);
    }

    [Test]
    public void CubicBezierMatchesCssTimingFunctions()
    {
        float[] xs = { 0.1f, 0.25f, 0.5f, 0.75f, 0.9f };
        // Reference values from a double-precision solve of the CSS keyword curves.
        AssertBezier(0.25f, 0.1f, 0.25f, 1f, xs, new[] { 0.0948f, 0.40851f, 0.8024f, 0.96046f, 0.99432f }, "ease");
        AssertBezier(0.42f, 0f, 1f, 1f, xs, new[] { 0.01703f, 0.09346f, 0.31536f, 0.62186f, 0.83943f }, "ease-in");
        AssertBezier(0f, 0f, 0.58f, 1f, xs, new[] { 0.16057f, 0.37814f, 0.68464f, 0.90654f, 0.98297f }, "ease-out");
        AssertBezier(0.42f, 0f, 0.58f, 1f, xs, new[] { 0.01972f, 0.12916f, 0.5f, 0.87084f, 0.98028f }, "ease-in-out");

        for (int i = 0; i <= 200; ++i)
        {
            float t = i / 200f;
            Assert.AreEqual(t, NowEase.CubicBezier(0f, 0f, 1f, 1f, t), 1e-6f, "linear bezier");
            Assert.AreEqual(t, NowEase.CubicBezier(0.3f, 0.3f, 0.7f, 0.7f, t), 1e-6f, "diagonal bezier");

            float sum = NowEase.CubicBezier(0.42f, 0f, 0.58f, 1f, t) + NowEase.CubicBezier(0.42f, 0f, 0.58f, 1f, 1f - t);
            Assert.AreEqual(1f, sum, 1e-3f, "ease-in-out symmetry at " + t);
        }
    }

    static void AssertBezier(float x1, float y1, float x2, float y2, float[] xs, float[] expected, string name)
    {
        for (int i = 0; i < xs.Length; ++i)
            Assert.AreEqual(expected[i], NowEase.CubicBezier(x1, y1, x2, y2, xs[i]), 1e-3f, name + " at " + xs[i]);
    }

    [Test]
    public void CubicBezierClampsControlPointXLikeCss()
    {
        for (int i = 0; i <= 100; ++i)
        {
            float t = i / 100f;
            Assert.AreEqual(
                NowEase.CubicBezier(0f, 0.2f, 1f, 0.8f, t),
                NowEase.CubicBezier(-3f, 0.2f, 4f, 0.8f, t),
                "x1/x2 clamp at " + t);
        }
    }

    [Test]
    public void LottieEasingSharesTheCubicBezierSolver()
    {
        var random = new System.Random(20260925);

        for (int c = 0; c < 200; ++c)
        {
            float x1 = (float)random.NextDouble();
            float y1 = (float)(random.NextDouble() * 3.0 - 1.0);
            float x2 = (float)random.NextDouble();
            float y2 = (float)(random.NextDouble() * 3.0 - 1.0);

            for (int i = -1; i <= 21; ++i)
            {
                float t = i / 20f;
                Assert.AreEqual(
                    NowEase.CubicBezier(x1, y1, x2, y2, t),
                    NowLottieEasing.Evaluate(x1, y1, x2, y2, t),
                    "Lottie keyframe easing at " + t);
            }
        }
    }

    [Test]
    public void ProgressHandlesWindowsAndEdgeCases()
    {
        Assert.AreEqual(0.5f, NowEase.Progress(5f, 0f, 10f));
        Assert.AreEqual(0.25f, NowEase.Progress(3.5f, 3f, 5f));
        Assert.AreEqual(0f, NowEase.Progress(-1f, 0f, 10f));
        Assert.AreEqual(0f, NowEase.Progress(0f, 0f, 10f));
        Assert.AreEqual(1f, NowEase.Progress(10f, 0f, 10f));
        Assert.AreEqual(1f, NowEase.Progress(25f, 0f, 10f));

        // Empty and inverted windows step at end.
        Assert.AreEqual(0f, NowEase.Progress(1.99f, 2f, 2f));
        Assert.AreEqual(1f, NowEase.Progress(2f, 2f, 2f));
        Assert.AreEqual(1f, NowEase.Progress(3f, 2f, 2f));
        Assert.AreEqual(0f, NowEase.Progress(0.5f, 5f, 1f));
        Assert.AreEqual(1f, NowEase.Progress(1f, 5f, 1f));
        Assert.AreEqual(1f, NowEase.Progress(4f, 5f, 1f));

        Assert.AreEqual(0f, NowEase.Progress(float.NaN, 0f, 1f));
        Assert.AreEqual(1f, NowEase.Progress(float.PositiveInfinity, 0f, 1f));
        Assert.AreEqual(0f, NowEase.Progress(float.NegativeInfinity, 0f, 1f));
    }

    [Test]
    public void EvaluateOverTimeWindowComposesProgressAndCurve()
    {
        foreach (NowEasing easing in AllEasings())
        {
            for (int i = -2; i <= 12; ++i)
            {
                float time = 4f + i * 0.25f;
                Assert.AreEqual(
                    NowEase.Evaluate(easing, NowEase.Progress(time, 4.5f, 6.5f)),
                    NowEase.Evaluate(easing, time, 4.5f, 6.5f),
                    easing + " at " + time);
            }

            Assert.AreEqual(0f, NowEase.Evaluate(easing, 1f, 2f, 2f), easing + " before empty window");
            Assert.AreEqual(1f, NowEase.Evaluate(easing, 2f, 2f, 2f), easing + " at empty window");
        }

        Assert.AreEqual(NowEase.OutCubic(0.5f), NowEase.Evaluate(NowEasing.OutCubic, 7.5f, 5f, 10f));
    }

    [Test]
    public void NamedCurvesMatchTheirFunctions()
    {
        var functions = new Dictionary<string, Func<float, float>>();
        foreach (var (name, curve) in AllFunctions)
            functions[name] = curve;

        foreach (NowEasing easing in AllEasings())
        {
            Assert.IsTrue(functions.TryGetValue(easing.ToString(), out var curve), "missing function for " + easing);

            for (int i = 0; i <= 50; ++i)
            {
                float t = i / 50f;
                Assert.AreEqual(curve(t), NowEase.Evaluate(easing, t), easing + " at " + t);
            }
        }
    }

    [Test]
    public void EasingIsAllocationFree()
    {
        NowEasing[] easings = AllEasings();
        float sink = EvaluateEverything(easings);

        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        sink += EvaluateEverything(easings);
        long allocated = allocations.End();
        allocations.AssertZero(allocated, "NowEase evaluation must not allocate");
        Assert.IsFalse(float.IsNaN(sink));
    }

    static float EvaluateEverything(NowEasing[] easings)
    {
        float sum = 0f;

        for (int i = -4; i <= 104; ++i)
        {
            float t = i / 100f;

            for (int e = 0; e < easings.Length; ++e)
            {
                sum += NowEase.Evaluate(easings[e], t);
                sum += NowEase.Evaluate(easings[e], t * 3f, 0.5f, 2.5f);
            }

            sum += NowEase.InBack(t, 2.5f) + NowEase.OutBack(t, 2.5f) + NowEase.InOutBack(t, 2.5f);
            sum += NowEase.InElastic(t, 1.4f, 0.25f) + NowEase.OutElastic(t, 1.4f, 0.25f) + NowEase.InOutElastic(t, 1.4f, 0.5f);
            sum += NowEase.Spring(t, 0.3f, 3f);
            sum += NowEase.CubicBezier(0.25f, 0.1f, 0.25f, 1f, t);
            sum += NowEase.Progress(t, 0.2f, 0.8f);
        }

        return sum;
    }
}
