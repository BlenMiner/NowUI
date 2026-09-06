using System;
using NUnit.Framework;
using Unity.PerformanceTesting;

public class NowBenchmarkInstrumentationTests
{
    [Test]
    public void ZeroAllocationGateRejectsDeliberateAllocationWithoutPerformanceContext()
    {
        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        var probe = new byte[4096];
        long value = allocations.End();
        GC.KeepAlive(probe);
        Assert.Throws<AssertionException>(() => allocations.AssertZero(value));
    }

    [Test]
    public void MissingCounterCannotPassAZeroAllocationGate()
    {
        using var allocations = new NowBenchmarkAllocations(simulateUnavailable: true);
        allocations.Begin();
        long unavailable = allocations.End();
        Assert.Throws<IgnoreException>(() => allocations.AssertZero(unavailable));
        Assert.Throws<IgnoreException>(() => allocations.AssertBytesAtMost(unavailable, 4096));
    }

    [Test]
    public void ByteBudgetDoesNotAcceptAllocationCalls()
    {
        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        if (allocations.bytesAvailable)
            Assert.Ignore("This runtime has a verified byte counter; fallback-only behavior does not apply.");
        Assert.Throws<IgnoreException>(() => allocations.AssertBytesAtMost(0, 4096));
    }

    [Test, Performance, Category("NowUI.Overview")]
    public void AllocationCounter_DetectsKnownAllocation()
    {
        using var allocations = new NowBenchmarkAllocations();
        if (!allocations.available)
            Assert.Ignore("Neither allocation bytes nor profiler allocation calls are available.");

        // Calibration control: exactly one deliberately retained array per sample.
        // This is an instrumentation check, not a NowUI allocation workload.
        for (int i = 0; i < 64; ++i)
        {
            allocations.Begin();
            var probe = new byte[1024];
            long value = allocations.End();
            GC.KeepAlive(probe);
            Assert.GreaterOrEqual(value, allocations.bytesAvailable ? 1024 : 1);
            allocations.Report(value);

            // Reject stale/accumulated/previous-frame results: an empty region
            // after an allocating region must reset to zero without a frame yield.
            allocations.Begin();
            long empty = allocations.End();
            Assert.AreEqual(0, empty);

            allocations.Begin();
            var first = new byte[1024];
            var second = new byte[1024];
            var third = new byte[1024];
            long multiple = allocations.End();
            GC.KeepAlive(first);
            GC.KeepAlive(second);
            GC.KeepAlive(third);
            if (allocations.bytesAvailable)
                Assert.GreaterOrEqual(multiple, 3 * 1024);
            else
                Assert.AreEqual(3, multiple);
        }
    }
}
