using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;

[assembly: InternalsVisibleTo("Tests")]

/// <summary>
/// Validates allocation instrumentation with real allocations before using it.
/// Some Unity Mono builds expose the byte API but always return zero. In that
/// case GC.Alloc profiler events provide allocation CALLS, never byte estimates.
/// One instance owns the recorder during a serial benchmark; report outside timers.
/// </summary>
public sealed class NowBenchmarkAllocations : IDisposable
{
    readonly Recorder _calls;
    readonly bool _previousEnabled;
    readonly SampleGroup _metric;
    long _before;

    public bool bytesAvailable { get; }
    public bool callsAvailable { get; }
    public bool available => bytesAvailable || callsAvailable;
    public string unit => bytesAvailable ? "managed bytes" : "managed allocation calls";

    public NowBenchmarkAllocations(string suffix = "", bool reportAvailability = true)
        : this(suffix, reportAvailability, true)
    {
    }

    // Deterministic coverage of the unavailable-backend path, independent of
    // which profiler APIs the machine running the correctness tests supports.
    internal NowBenchmarkAllocations(bool simulateUnavailable)
        : this("", false, !simulateUnavailable)
    {
    }

    NowBenchmarkAllocations(string suffix, bool reportAvailability, bool probeBackends)
    {
        if (probeBackends)
        {
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var probe = new byte[4096];
                long after = GC.GetAllocatedBytesForCurrentThread();
                GC.KeepAlive(probe);
                bytesAvailable = after - before >= 4096;
            }
            catch (NotImplementedException) { }
            catch (NotSupportedException) { }
            catch (MissingMethodException) { }

            _calls = Recorder.Get("GC.Alloc");
            if (_calls.isValid)
            {
                _previousEnabled = _calls.enabled;
                _calls.enabled = false;
                _calls.FilterToCurrentThread();
                _calls.enabled = true;
                var probe = new byte[4096];
                _calls.enabled = false;
                int allocating = _calls.sampleBlockCount;
                GC.KeepAlive(probe);
                _calls.enabled = true;
                _calls.enabled = false;
                callsAvailable = allocating == 1 && _calls.sampleBlockCount == 0;
            }
        }
        _metric = new SampleGroup(bytesAvailable ? "GC.Alloc" + suffix : "GC.Alloc.Calls" + suffix,
            bytesAvailable ? SampleUnit.Byte : SampleUnit.Undefined);
        if (reportAvailability)
        {
            Measure.Custom(new SampleGroup("GC.Bytes.Available", SampleUnit.Undefined), bytesAvailable ? 1 : 0);
            Measure.Custom(new SampleGroup("GC.Calls.Available", SampleUnit.Undefined), callsAvailable ? 1 : 0);
        }
    }

    public void Begin()
    {
        if (bytesAvailable)
            _before = GC.GetAllocatedBytesForCurrentThread();
        else if (callsAvailable)
        {
            _calls.enabled = false;
            _calls.enabled = true;
        }
    }

    public long End()
    {
        if (bytesAvailable)
            return GC.GetAllocatedBytesForCurrentThread() - _before;
        if (!callsAvailable)
            return 0;
        _calls.enabled = false;
        return _calls.sampleBlockCount;
    }

    public void Report(double value)
    {
        if (available)
            Measure.Custom(_metric, value);
    }

    /// <summary>
    /// Correctness gates must require usable instrumentation before sampling.
    /// Byte budgets cannot use allocation-call counts as a substitute.
    /// </summary>
    public void RequireAvailable(bool bytesOnly = false)
    {
        if (bytesOnly ? !bytesAvailable : !available)
            Assert.Ignore(bytesOnly
                ? "Verified per-thread allocation bytes are unavailable; this byte budget cannot be checked."
                : "Neither verified per-thread allocation bytes nor allocation-call counts are available.");
    }

    public void AssertZero(long sample, string message = null)
    {
        RequireAvailable();
        Assert.AreEqual(0L, sample, (message ?? "The measured region must not allocate.") + " Counter: " + unit + ".");
    }

    public void AssertBytesAtMost(long sample, long budget, string message = null)
    {
        RequireAvailable(bytesOnly: true);
        Assert.LessOrEqual(sample, budget, message ?? "The measured region exceeded its managed-byte budget.");
    }

    public void Dispose()
    {
        if (_calls == null || !_calls.isValid)
            return;
        _calls.enabled = false;
        _calls.CollectFromAllThreads();
        _calls.enabled = _previousEnabled;
    }
}
