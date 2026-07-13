using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public enum NowGlassBlurQuality
    {
        Auto = 0,
        Fast = 2,
        Balanced = 3,
        High = 4,
        Ultra = 5
    }

    public enum NowGlassFallbackReason
    {
        None,
        MissingTargetContext,
        MissingBlurMaterial,
        LegacyImmediatePath,
        FullTargetCapture
    }

    public readonly struct NowGlassDiagnosticEntry
    {
        public readonly int frame;
        public readonly string host;
        public readonly NowGlassBlurQuality quality;
        public readonly NowGlassFallbackReason fallbackReason;
        public readonly float blurRadius;
        public readonly int sourceWidth;
        public readonly int sourceHeight;
        public readonly int blurredWidth;
        public readonly int blurredHeight;
        public readonly NowRect captureRect;
        public readonly int blurDownsample;
        public readonly int blurIterations;
        public readonly float blurStep;
        public readonly int copiedPixels;
        public readonly int blurredPixels;
        public readonly int blurPasses;

        internal NowGlassDiagnosticEntry(
            string host,
            NowGlassBlurQuality quality,
            NowGlassFallbackReason fallbackReason,
            float blurRadius,
            int sourceWidth,
            int sourceHeight,
            int blurredWidth,
            int blurredHeight,
            NowRect captureRect,
            int blurDownsample,
            int blurIterations,
            float blurStep,
            int copiedPixels,
            int blurredPixels,
            int blurPasses)
        {
            frame = Time.frameCount;
            this.host = host;
            this.quality = quality;
            this.fallbackReason = fallbackReason;
            this.blurRadius = blurRadius;
            this.sourceWidth = sourceWidth;
            this.sourceHeight = sourceHeight;
            this.blurredWidth = blurredWidth;
            this.blurredHeight = blurredHeight;
            this.captureRect = captureRect;
            this.blurDownsample = blurDownsample;
            this.blurIterations = blurIterations;
            this.blurStep = blurStep;
            this.copiedPixels = copiedPixels;
            this.blurredPixels = blurredPixels;
            this.blurPasses = blurPasses;
        }
    }

    public readonly struct NowGlassFrameDiagnostics
    {
        public readonly int frame;
        public readonly int paneCount;
        public readonly int entryCount;
        public readonly int droppedEntryCount;
        public readonly int copiedPixels;
        public readonly int blurredPixels;
        public readonly int blurPasses;
        public readonly int fallbackCount;

        internal NowGlassFrameDiagnostics(
            int frame,
            int paneCount,
            int entryCount,
            int droppedEntryCount,
            int copiedPixels,
            int blurredPixels,
            int blurPasses,
            int fallbackCount)
        {
            this.frame = frame;
            this.paneCount = paneCount;
            this.entryCount = entryCount;
            this.droppedEntryCount = droppedEntryCount;
            this.copiedPixels = copiedPixels;
            this.blurredPixels = blurredPixels;
            this.blurPasses = blurPasses;
            this.fallbackCount = fallbackCount;
        }
    }

    public static class NowGlassSettings
    {
        static readonly List<NowGlassBlurQuality> _qualityStack = new List<NowGlassBlurQuality>(8);

        static readonly NowScopeGuard _qualityScopes = new NowScopeGuard("NowGlassSettings blur quality", 8);

        static readonly List<NowGlassDiagnosticEntry> _diagnosticEntries = new List<NowGlassDiagnosticEntry>(16);

        static NowGlassFrameDiagnostics _lastFrameDiagnostics =
            new NowGlassFrameDiagnostics(-1, 0, 0, 0, 0, 0, 0, 0);

        static int _diagnosticFrame = -1;

        static int _diagnosticEntryLimit = 16;

        static bool _diagnosticsEnabled;

        static NowGlassBlurQuality _defaultBlurQuality = NowGlassBlurQuality.Balanced;

        public static NowGlassBlurQuality defaultBlurQuality
        {
            get => _defaultBlurQuality;
            set => _defaultBlurQuality = NormalizeDefault(value);
        }

        public static bool diagnosticsEnabled
        {
            get => _diagnosticsEnabled;
            set
            {
                if (_diagnosticsEnabled == value)
                    return;

                _diagnosticsEnabled = value;

                if (value)
                {
                    _diagnosticFrame = -1;
                    TouchFrame();
                    return;
                }

                _diagnosticEntries.Clear();
                _diagnosticFrame = -1;
                _lastFrameDiagnostics = new NowGlassFrameDiagnostics(-1, 0, 0, 0, 0, 0, 0, 0);
            }
        }

        public static NowGlassFrameDiagnostics lastFrameDiagnostics => _lastFrameDiagnostics;

        public static int diagnosticEntryCapacity => _diagnosticEntryLimit;

        /// <summary>
        /// Reserves storage for per-pane diagnostics. Reserving does not start
        /// recording — set <see cref="diagnosticsEnabled"/> to record. Recording
        /// never grows this list implicitly; entries beyond the reserved capacity
        /// are counted in <see cref="NowGlassFrameDiagnostics.droppedEntryCount"/>
        /// so diagnostics can stay allocation-free even when enabled.
        /// </summary>
        public static void ReserveDiagnostics(int paneCapacity)
        {
            _diagnosticEntryLimit = Mathf.Max(0, paneCapacity);

            if (_diagnosticEntryLimit > _diagnosticEntries.Capacity)
                _diagnosticEntries.Capacity = paneCapacity;

            if (_diagnosticEntries.Count <= _diagnosticEntryLimit)
                return;

            int removed = _diagnosticEntries.Count - _diagnosticEntryLimit;
            _diagnosticEntries.RemoveRange(_diagnosticEntryLimit, removed);
            _lastFrameDiagnostics = new NowGlassFrameDiagnostics(
                _lastFrameDiagnostics.frame,
                _lastFrameDiagnostics.paneCount,
                _diagnosticEntries.Count,
                _lastFrameDiagnostics.droppedEntryCount + removed,
                _lastFrameDiagnostics.copiedPixels,
                _lastFrameDiagnostics.blurredPixels,
                _lastFrameDiagnostics.blurPasses,
                _lastFrameDiagnostics.fallbackCount);
        }

        public static bool TryGetLastFrameDiagnostic(int index, out NowGlassDiagnosticEntry entry)
        {
            if (index < 0 || index >= _diagnosticEntries.Count)
            {
                entry = default;
                return false;
            }

            entry = _diagnosticEntries[index];
            return true;
        }

        public static int CopyLastFrameDiagnosticsTo(NowGlassDiagnosticEntry[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int count = Mathf.Min(destination.Length, _diagnosticEntries.Count);

            for (int i = 0; i < count; ++i)
                destination[i] = _diagnosticEntries[i];

            return count;
        }

        internal static NowGlassBlurQuality currentBlurQuality =>
            Resolve(_qualityStack.Count > 0 ? _qualityStack[^1] : _defaultBlurQuality);

        internal static NowGlassQualityScope PushBlurQuality(NowGlassBlurQuality quality)
        {
            TouchFrame();
            _qualityStack.Add(Resolve(quality));
            return new NowGlassQualityScope(_qualityScopes.Enter());
        }

        internal static void PopBlurQuality(int token)
        {
            if (!_qualityScopes.Exit(token))
                return;

            int index = _qualityStack.Count - 1;
            if (index >= 0)
                _qualityStack.RemoveAt(index);
        }

        internal static void DiscardAbandonedQualityScopes()
        {
            _qualityStack.Clear();
            _qualityScopes.Clear();
        }

        internal static NowGlassBlurQuality Resolve(NowGlassBlurQuality quality)
        {
            if (quality == NowGlassBlurQuality.Auto)
                return NormalizeDefault(_defaultBlurQuality);

            return NormalizeDefault(quality);
        }

        internal static void Record(in NowGlassDiagnosticEntry entry)
        {
            if (!diagnosticsEnabled)
                return;

            TouchFrame();

            bool stored = _diagnosticEntries.Count < _diagnosticEntryLimit;

            if (stored)
                _diagnosticEntries.Add(entry);

            int fallbackIncrement =
                entry.fallbackReason != NowGlassFallbackReason.None &&
                entry.fallbackReason != NowGlassFallbackReason.FullTargetCapture
                    ? 1
                    : 0;
            _lastFrameDiagnostics = new NowGlassFrameDiagnostics(
                _diagnosticFrame,
                _lastFrameDiagnostics.paneCount + 1,
                _diagnosticEntries.Count,
                _lastFrameDiagnostics.droppedEntryCount + (stored ? 0 : 1),
                _lastFrameDiagnostics.copiedPixels + entry.copiedPixels,
                _lastFrameDiagnostics.blurredPixels + entry.blurredPixels,
                _lastFrameDiagnostics.blurPasses + entry.blurPasses,
                _lastFrameDiagnostics.fallbackCount + fallbackIncrement);
        }

        internal static void TouchFrame()
        {
            if (!diagnosticsEnabled)
                return;

            int frame = Time.frameCount;

            if (_diagnosticFrame == frame)
                return;

            _diagnosticFrame = frame;
            _diagnosticEntries.Clear();
            _lastFrameDiagnostics = new NowGlassFrameDiagnostics(
                frame,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        static NowGlassBlurQuality NormalizeDefault(NowGlassBlurQuality quality)
        {
            return quality switch
            {
                NowGlassBlurQuality.Fast => NowGlassBlurQuality.Fast,
                NowGlassBlurQuality.High => NowGlassBlurQuality.High,
                NowGlassBlurQuality.Ultra => NowGlassBlurQuality.Ultra,
                _ => NowGlassBlurQuality.Balanced
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _qualityStack.Clear();
            _qualityScopes.Clear();
            _diagnosticEntries.Clear();
            _diagnosticFrame = -1;
            _diagnosticEntryLimit = 16;
            _defaultBlurQuality = NowGlassBlurQuality.Balanced;
            _diagnosticsEnabled = false;
            _lastFrameDiagnostics = new NowGlassFrameDiagnostics(-1, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    internal struct NowGlassQualityScope : IDisposable
    {
        int _token;

        internal NowGlassQualityScope(int token)
        {
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowGlassSettings.PopBlurQuality(_token);
            _token = 0;
        }
    }
}
