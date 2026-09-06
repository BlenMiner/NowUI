using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NowUI.Editor;
using UnityEngine;

public class NowPerfSmokeRunnerTests
{
    [Serializable]
    sealed class SmokeReport
    {
        public SmokeScenario[] scenarios;
    }

    [Serializable]
    sealed class SmokeScenario
    {
        public string name;
        public bool allocationBytesAvailable;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SmokeJsonDistinguishesUnavailableAllocationFromKnownZero(bool available)
    {
        var metricType = typeof(NowPerfSmokeRunner).GetNestedType("PerfMetric", BindingFlags.NonPublic);
        Assert.NotNull(metricType);
        object metric = Activator.CreateInstance(metricType, nonPublic: true);
        metricType.GetField("name").SetValue(metric, "allocation-fixture");
        metricType.GetField("allocatedBytes").SetValue(metric, available ? (object)0L : null);
        var metrics = Array.CreateInstance(metricType, 1);
        metrics.SetValue(metric, 0);

        var buildJson = PrivateStaticMethod("BuildJson");
        string json = (string)buildJson.Invoke(null, new object[] { metrics });
        var report = JsonUtility.FromJson<SmokeReport>(json);
        Assert.NotNull(report);
        Assert.AreEqual(1, report.scenarios.Length);
        Assert.AreEqual("allocation-fixture", report.scenarios[0].name);
        Assert.AreEqual(available, report.scenarios[0].allocationBytesAvailable);
        string expectedValue = available ? "0" : "null";
        Assert.IsTrue(
            Regex.IsMatch(json, @"""allocatedBytes""\s*:\s*" + expectedValue + @"\s*[,}]"),
            "An unavailable counter must serialize null; a measured zero must remain numeric zero.");
    }

    [Test]
    public void SmokeAllocationProbeMatchesObservedRuntimeWithoutPerformanceTestContext()
    {
        // This is an ordinary test, intentionally without [Performance]. The
        // smoke runner must probe capabilities without requiring a report sink.
        long? before = ReadAllocationCounter();
        var allocation = new byte[8192];
        long? after = ReadAllocationCounter();
        GC.KeepAlive(allocation);
        bool reportsBytes = before.HasValue && after.HasValue && after.Value - before.Value >= 8192;

        object observed = PrivateStaticMethod("ProbeAllocationBytes").Invoke(null, null);
        Assert.AreEqual(reportsBytes, observed != null,
            "A missing or non-reporting allocation counter must not be advertised as available.");
        if (reportsBytes)
            Assert.GreaterOrEqual((long)observed, after.Value);
    }

    static MethodInfo PrivateStaticMethod(string name)
    {
        var method = typeof(NowPerfSmokeRunner).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method, "Missing smoke-runner implementation method: " + name);
        return method;
    }

    static long? ReadAllocationCounter()
    {
        try
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (MissingMethodException) { return null; }
    }
}
