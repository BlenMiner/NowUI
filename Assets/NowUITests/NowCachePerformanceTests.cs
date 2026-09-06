using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using NowUI;
using NowUI.Internal;

/// <summary>
/// Cache working-set benchmarks. CPU.Batch and allocation samples describe one
/// complete batch, never a per-item average. Fixture construction, imports, cache resets,
/// result checks, and soak pacing are outside the measured region. Cache misses
/// intentionally include admission allocations; warm hits should remain free of
/// managed allocations. These are CPU measurements, not GPU completion times.
/// </summary>
public class NowCachePerformanceTests
{
    const int WarmupBatches = 5;
    const int SampleBatches = 64;
    const int StateBatchSize = 256;
    const int RampBatchSize = 64;

    int _previousLottieLimit;
    long _previousLottieByteLimit;

    [SetUp]
    public void SetUp()
    {
        _previousLottieLimit = NowLottieCache.maxEntries;
        _previousLottieByteLimit = NowLottieCache.maxCachedSourceBytes;
        NowInput.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowGradientMaterials.Reset();
        NowGradientRampCache.Reset();
        NowLottieCache.Reset();
        NowLottieRenderer.ClearCache();
    }

    [TearDown]
    public void TearDown()
    {
        NowLottieRenderer.ClearCache();
        NowLottieCache.Reset();
        NowLottieCache.maxEntries = _previousLottieLimit;
        NowLottieCache.maxCachedSourceBytes = _previousLottieByteLimit;
        NowGradientMaterials.Reset();
        NowGradientRampCache.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowInput.Reset();
    }

    static void MeasureBatch(Action action, Action setup = null)
    {
        using var allocations = new NowBenchmarkAllocations();
        int completed = 0;

        // SetUp/CleanUp run outside Unity's method timer. Allocation readings
        // bracket the same action, including its cache misses and uploads.
        Measure.Method(action)
            .SampleGroup("CPU.Batch")
            .SetUp(() =>
            {
                setup?.Invoke();
                allocations.Begin();
            })
            .CleanUp(() =>
            {
                long allocated = allocations.End();
                if (++completed > WarmupBatches)
                    allocations.Report(allocated);
            })
            .WarmupCount(WarmupBatches)
            .MeasurementCount(SampleBatches)
            .IterationsPerMeasurement(1)
            .Run();
    }

    static void Count(string name, double value)
    {
        Measure.Custom(new SampleGroup(name, SampleUnit.Undefined, false), value);
    }

    static NowResolvedId[] BuildStateIds(int count)
    {
        var ids = new NowResolvedId[count];
        using (NowControls.IdScope("overview-cache-state"))
        {
            for (int i = 0; i < count; ++i)
                ids[i] = NowControls.GetControlId(new NowId(i));
        }
        return ids;
    }

    static int TouchPressStates(NowResolvedId[] ids, int start, int count, bool triggered, float duration = 0.45f)
    {
        int active = 0;
        for (int i = 0; i < count; ++i)
        {
            if (NowControlState.PressAnimation(ids[start + i], triggered, Vector2.zero, duration).active)
                ++active;
        }
        return active;
    }

    [Test, Performance, Category("NowUI.Overview")]
    public void PressState_Idle256_NoSlots()
    {
        var ids = BuildStateIds(StateBatchSize);
        int active = -1;
        MeasureBatch(() => active = TouchPressStates(ids, 0, StateBatchSize, false));
        Assert.AreEqual(0, active);
        Assert.AreEqual(0, NowControlState.pressAnimationStateCount);
        Count("ControlsPerBatch", StateBatchSize);
        Count("State.RetainedSlots", NowControlState.pressAnimationStateCount);
    }

    [Test, Performance, Category("NowUI.Overview")]
    public void PressState_Stable256_WarmSlots()
    {
        var ids = BuildStateIds(StateBatchSize);
        int active = 0;
        MeasureBatch(() => active = TouchPressStates(ids, 0, StateBatchSize, true));
        Assert.AreEqual(StateBatchSize, active);
        Assert.AreEqual(StateBatchSize, NowControlState.pressAnimationStateCount);
        Count("ControlsPerBatch", StateBatchSize);
        Count("State.RetainedSlots", NowControlState.pressAnimationStateCount);
    }

    /// <summary>
    /// Every measured batch admits 256 new ids. The prebuilt pool supports 256
    /// batches before reuse; the standard 5+64 run never revisits a slot.
    /// </summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void PressState_New256PerBatch_Admission()
    {
        var ids = BuildStateIds(StateBatchSize * 256);
        int cursor = 0;
        int active = 0;
        MeasureBatch(() =>
        {
            active = TouchPressStates(ids, cursor, StateBatchSize, true);
            cursor = (cursor + StateBatchSize) % ids.Length;
        });
        Assert.AreEqual(StateBatchSize, active);
        Assert.Greater(NowControlState.pressAnimationStateCount, StateBatchSize);
        Count("ControlsPerBatch", StateBatchSize);
        Count("State.RetainedSlots", NowControlState.pressAnimationStateCount);
    }

    /// <summary>
    /// Logical UI batches paced roughly 16ms apart for 12-15 seconds. A burst of
    /// 8192 abandoned slots ages out under the real 10s TTL while 256 live slots
    /// are refreshed and eight new ids arrive per batch. Sleep and observation
    /// are excluded. This exercises actual sweep work without a private clock
    /// override; it is not a Unity player-frame or full-process memory soak.
    /// </summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void PressState_ChurnAndCleanup_12SecondSoak()
    {
        const int abandoned = 8192;
        const int incomingPerBatch = 8;
        const int maxBatches = 1000;
        var ids = BuildStateIds(abandoned + StateBatchSize + incomingPerBatch * maxBatches);
        for (int i = 0; i < WarmupBatches; ++i)
            TouchPressStates(ids, abandoned, StateBatchSize, true, 30f);
        TouchPressStates(ids, 0, abandoned, true);

        var cpu = new SampleGroup("CPU.Batch", SampleUnit.Millisecond, false);
        using var allocations = new NowBenchmarkAllocations();
        var slots = new SampleGroup("State.RetainedSlots", SampleUnit.Undefined, false);
        var elapsed = Stopwatch.StartNew();
        int peak = NowControlState.pressAnimationStateCount;
        int previous = peak;
        int batches = 0;
        int active = 0;
        bool observedCleanup = false;

        while (batches < maxBatches && elapsed.Elapsed.TotalSeconds < 15.0)
        {
            allocations.Begin();
            long start = Stopwatch.GetTimestamp();
            // Read/advance the original animations without re-triggering: an
            // incorrectly evicted live entry must remain absent and fail below.
            active = TouchPressStates(ids, abandoned, StateBatchSize, false, 30f);
            TouchPressStates(ids, abandoned + StateBatchSize + batches * incomingPerBatch,
                incomingPerBatch, true);
            long ticks = Stopwatch.GetTimestamp() - start;
            long allocated = allocations.End();

            int retained = NowControlState.pressAnimationStateCount;
            observedCleanup |= retained < previous;
            peak = Math.Max(peak, retained);
            previous = retained;
            ++batches;
            Measure.Custom(cpu, ticks * 1000.0 / Stopwatch.Frequency);
            allocations.Report(allocated);
            Measure.Custom(slots, retained);

            if (observedCleanup && elapsed.Elapsed.TotalSeconds >= 12.0)
                break;
            Thread.Sleep(16);
        }

        Assert.AreEqual(StateBatchSize, active, "Live ids must survive the cleanup sweep.");
        Assert.IsTrue(observedCleanup, "No state eviction was observed within the bounded 15s soak.");
        Assert.Less(NowControlState.pressAnimationStateCount, peak);
        Count("ControlsPerBatch", StateBatchSize + incomingPerBatch);
        Count("State.PeakSlots", peak);
        Count("State.FinalSlots", NowControlState.pressAnimationStateCount);
        Count("Soak.Batches", batches);
        Measure.Custom(new SampleGroup("Soak.Elapsed", SampleUnit.Second, false), elapsed.Elapsed.TotalSeconds);
    }

    static Gradient[] BuildRamps(int count)
    {
        var ramps = new Gradient[count];
        var colors = new[] { new GradientColorKey(Color.cyan, 0f), new GradientColorKey(Color.blue, 1f) };
        var alpha = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        for (int i = 0; i < count; ++i)
        {
            ramps[i] = new Gradient();
            ramps[i].SetKeys(colors, alpha);
        }
        return ramps;
    }

    static void ReportRampAtlas()
    {
        // rampCount includes diagnostic row zero, which is not a cached ramp.
        Count("Gradient.RetainedRamps", NowGradientRampCache.rampCount - 1);
        Measure.Custom(new SampleGroup("Gradient.AtlasUnityReportedBytes", SampleUnit.Byte, false),
            Profiler.GetRuntimeMemorySizeLong(NowGradientRampCache.texture));
        Count("RampRequestsPerBatch", RampBatchSize);
    }

    [Test, Performance, Category("NowUI.Overview")]
    public void GradientRamps_Reuse64References_CacheHits()
    {
        var ramps = BuildRamps(RampBatchSize);
        int sum = 0;
        MeasureBatch(() =>
        {
            sum = 0;
            for (int i = 0; i < ramps.Length; ++i)
                sum += NowGradientRampCache.Get(ramps[i], 0).row;
        });
        Assert.Greater(sum, 0);
        Assert.AreEqual(RampBatchSize + 1, NowGradientRampCache.rampCount);
        ReportRampAtlas();
    }

    /// <summary>Revision increments force 64 row rebakes and texture uploads per batch.</summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void GradientRamps_Revise64References_RebakeAndUpload()
    {
        var ramps = BuildRamps(RampBatchSize);
        int revision = 0;
        int sum = 0;
        MeasureBatch(() =>
        {
            ++revision;
            sum = 0;
            for (int i = 0; i < ramps.Length; ++i)
                sum += NowGradientRampCache.Get(ramps[i], revision).row;
        });
        Assert.Greater(sum, 0);
        Assert.AreEqual(RampBatchSize + 1, NowGradientRampCache.rampCount);
        ReportRampAtlas();
    }

    /// <summary>
    /// 64 cache-cold precreated Gradient objects per batch, all with equal keys.
    /// Cache reset and atlas construction are outside the timer; row admission,
    /// baking and uploads are inside. No consumer Gradient construction is timed.
    /// </summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void GradientRamps_Admit64References_EmptyAtlas()
    {
        var ramps = BuildRamps(RampBatchSize);
        int sum = 0;
        MeasureBatch(() =>
        {
            sum = 0;
            for (int i = 0; i < ramps.Length; ++i)
                sum += NowGradientRampCache.Get(ramps[i], 0).row;
        }, () =>
        {
            NowGradientRampCache.Reset();
            _ = NowGradientRampCache.texture;
        });
        Assert.Greater(sum, 0);
        Assert.AreEqual(RampBatchSize + 1, NowGradientRampCache.rampCount);
        ReportRampAtlas();
    }

    /// <summary>
    /// Explicit failure-mode benchmark: a full atlas rejects every new reference
    /// with diagnostic row zero. Its fast timing is not successful gradient work.
    /// The one-time diagnostic is checked before warmup, outside measurement.
    /// </summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void GradientRamps_Full255_64RejectedReferences()
    {
        var resident = BuildRamps(NowGradientRampCache.TextureHeight - 1);
        var incoming = BuildRamps(RampBatchSize);
        for (int i = 0; i < resident.Length; ++i)
            Assert.Greater(NowGradientRampCache.Get(resident[i], 0).row, 0);
        LogAssert.Expect(LogType.Error,
            "NowUI gradient ramp atlas is full (255 unique ramps). Reuse Color pairs or Gradient instances instead of creating unbounded ramp values.");
        Assert.AreEqual(0, NowGradientRampCache.Get(incoming[0], 0).row);

        int rejected = 0;
        MeasureBatch(() =>
        {
            rejected = 0;
            for (int i = 0; i < incoming.Length; ++i)
            {
                if (NowGradientRampCache.Get(incoming[i], 0).row == 0)
                    ++rejected;
            }
        });
        Assert.AreEqual(RampBatchSize, rejected);
        Assert.AreEqual(NowGradientRampCache.TextureHeight, NowGradientRampCache.rampCount);
        Count("Gradient.RejectedRequestsPerBatch", rejected);
        ReportRampAtlas();
    }

    static NowLottieAsset LoadLottie()
    {
        var asset = AssetDatabase.LoadAssetAtPath<NowLottieAsset>("Assets/NowUI/Assets/AnimatedEmoji/1f600.lottie");
        Assert.NotNull(asset, "The bundled animated emoji is required for Lottie cache benchmarks.");
        Assert.NotNull(asset.composition);
        return asset;
    }

    static string[] BuildCacheKeys(int count)
    {
        var keys = new string[count];
        for (int i = 0; i < count; ++i)
            keys[i] = "overview-lottie-" + i;
        return keys;
    }

    void ConfigureLottieCache(int limit)
    {
        NowLottieCache.maxEntries = limit;
        NowLottieCache.maxCachedSourceBytes = long.MaxValue;
    }

    [Test, Performance, Category("NowUI.Overview")]
    public void LottieUrlCache_64Resident_256Hits()
    {
        const int resident = 64;
        const int requests = 256;
        var asset = LoadLottie();
        var keys = BuildCacheKeys(resident);
        ConfigureLottieCache(resident);
        for (int i = 0; i < keys.Length; ++i)
            NowLottieCache.SetAsset(keys[i], asset);

        int hits = 0;
        MeasureBatch(() =>
        {
            hits = 0;
            for (int i = 0; i < requests; ++i)
            {
                if (ReferenceEquals(asset, NowLottieCache.GetAsset(keys[i % resident])))
                    ++hits;
            }
        });
        Assert.AreEqual(requests, hits);
        Assert.AreEqual(resident, NowLottieCache.cachedEntryCount);
        Count("Cache.RequestsPerBatch", requests);
        Count("Lottie.RetainedUrlEntries", NowLottieCache.cachedEntryCount);
    }

    /// <summary>
    /// Inserts 16 new caller-owned asset references into a full 64-entry LRU.
    /// Downloads, JSON parsing and asset construction are deliberately absent;
    /// entry allocation, retention accounting, victim selection and removal run.
    /// </summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void LottieUrlCache_64Resident_16AdmissionsAndEvictions()
    {
        const int resident = 64;
        const int incoming = 16;
        var asset = LoadLottie();
        var keys = BuildCacheKeys(4096);
        ConfigureLottieCache(resident);
        for (int i = 0; i < resident; ++i)
            NowLottieCache.SetAsset(keys[i], asset);
        int cursor = resident;
        int latest = 0;

        MeasureBatch(() =>
        {
            for (int i = 0; i < incoming; ++i)
            {
                latest = cursor;
                NowLottieCache.SetAsset(keys[cursor], asset);
                cursor = (cursor + 1) % keys.Length;
            }
        });
        Assert.AreEqual(resident, NowLottieCache.cachedEntryCount);
        Assert.AreSame(asset, NowLottieCache.GetAsset(keys[latest]));
        Count("Cache.AdmissionsPerBatch", incoming);
        Count("Lottie.RetainedUrlEntries", NowLottieCache.cachedEntryCount);
    }

    /// <summary>32 fixed frame keys exactly fit the documented tessellation LRU.</summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void LottieGeometry_32FrameWorkingSet_WarmHits()
    {
        MeasureLottieGeometry(32);
    }

    /// <summary>40 fixed frame keys repeatedly overflow the 32-entry tessellation LRU.</summary>
    [Test, Performance, Category("NowUI.Overview")]
    public void LottieGeometry_40FrameWorkingSet_LruThrashing()
    {
        MeasureLottieGeometry(40);
    }

    static void MeasureLottieGeometry(int workingSet)
    {
        var asset = LoadLottie();
        var composition = asset.composition;
        Assert.GreaterOrEqual(composition.durationFrames, workingSet);
        int vertices = 0;
        MeasureBatch(() =>
        {
            vertices = 0;
            for (int i = 0; i < workingSet; ++i)
            {
                var buffer = NowLottieRenderer.RenderCached(composition, composition.inPoint + i, 64f, 64f, true);
                vertices += buffer.positions.count;
            }
        });
        Assert.Greater(vertices, 0, "The working set must produce tessellated geometry.");
        Count("Lottie.RequestsPerBatch", workingSet);
        Count("Lottie.WorkingSetFrameKeys", workingSet);
        Count("Lottie.ReturnedVerticesPerBatch", vertices);
        Count("Lottie.NativeTessellation", NowLottieNative.tessellationAvailable ? 1 : 0);
    }
}
