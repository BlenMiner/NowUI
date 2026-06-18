using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    public enum NowEffectDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }

    public enum NowWaveAxis
    {
        X,
        Y
    }

    public readonly struct NowSubdivision
    {
        internal enum SubdivisionMode
        {
            None,
            Fixed,
            MaxCellSize
        }

        internal readonly SubdivisionMode mode;
        internal readonly int divisions;
        internal readonly float maxCellSize;

        NowSubdivision(SubdivisionMode mode, int divisions, float maxCellSize)
        {
            this.mode = mode;
            this.divisions = divisions;
            this.maxCellSize = maxCellSize;
        }

        public static NowSubdivision None => default;

        public static NowSubdivision Fixed(int divisions)
        {
            return new NowSubdivision(SubdivisionMode.Fixed, Mathf.Max(1, divisions), 0f);
        }

        public static NowSubdivision MaxCellSize(float size)
        {
            return new NowSubdivision(SubdivisionMode.MaxCellSize, 0, Mathf.Max(1f, size));
        }
    }

    public readonly struct NowEffectVertex
    {
        public readonly Vector2 position;
        public readonly Vector2 normalized;
        public readonly Vector2 uv;
        public readonly int index;

        internal NowEffectVertex(Vector2 position, Vector2 normalized, Vector2 uv, int index)
        {
            this.position = position;
            this.normalized = normalized;
            this.uv = uv;
            this.index = index;
        }
    }

    public readonly struct NowEffectContext
    {
        public readonly int id;
        public readonly NowRect sourceRect;
        public readonly float time;

        internal NowEffectContext(int id, NowRect sourceRect)
        {
            this.id = id;
            this.sourceRect = sourceRect;
            time = Time.realtimeSinceStartup;
        }
    }

    public interface INowVertexDeformer
    {
        Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context);
    }

    public readonly struct NowWaveDeformer : INowVertexDeformer
    {
        readonly float _time;
        readonly float _amplitude;
        readonly float _wavelength;
        readonly NowWaveAxis _axis;

        internal NowWaveDeformer(float time, float amplitude, float wavelength, NowWaveAxis axis)
        {
            _time = time;
            _amplitude = amplitude;
            _wavelength = Mathf.Max(1f, wavelength);
            _axis = axis;
        }

        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            var position = vertex.position;
            float distance = _axis == NowWaveAxis.Y
                ? vertex.normalized.x * Mathf.Max(1f, context.sourceRect.width)
                : vertex.normalized.y * Mathf.Max(1f, context.sourceRect.height);
            float offset = Mathf.Sin((distance / _wavelength + _time) * Mathf.PI * 2f) * _amplitude;

            if (_axis == NowWaveAxis.Y)
                position.y += offset;
            else
                position.x += offset;

            return position;
        }
    }

    public readonly struct NowGenieDeformer : INowVertexDeformer
    {
        readonly NowRect _targetRect;
        readonly float _progress;
        readonly NowEffectDirection _direction;

        internal NowGenieDeformer(NowRect targetRect, float progress, NowEffectDirection direction)
        {
            _targetRect = targetRect;
            _progress = Mathf.Clamp01(progress);
            _direction = direction;
        }

        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            float eased = _progress * _progress * (3f - 2f * _progress);
            Vector2 normalized = vertex.normalized;
            Vector2 target = new Vector2(
                Mathf.Lerp(_targetRect.x, _targetRect.xMax, normalized.x),
                Mathf.Lerp(_targetRect.y, _targetRect.yMax, normalized.y));

            float along = _direction switch
            {
                NowEffectDirection.Top => 1f - normalized.y,
                NowEffectDirection.Left => 1f - normalized.x,
                NowEffectDirection.Right => normalized.x,
                _ => normalized.y
            };

            float localPull = Mathf.Clamp01(eased * Mathf.Lerp(0.35f, 1f, along));
            Vector2 result = Vector2.Lerp(vertex.position, target, localPull);

            float curve = Mathf.Sin(along * Mathf.PI) * Mathf.Sin(eased * Mathf.PI) * 0.12f;
            Vector2 sourceCenter = context.sourceRect.center;
            Vector2 targetCenter = _targetRect.center;
            Vector2 pull = targetCenter - sourceCenter;

            if (pull.sqrMagnitude > 0.001f)
            {
                var perpendicular = new Vector2(-pull.y, pull.x).normalized;
                float side = _direction == NowEffectDirection.Top || _direction == NowEffectDirection.Bottom
                    ? normalized.x - 0.5f
                    : normalized.y - 0.5f;
                result += perpendicular * side * curve * Mathf.Max(context.sourceRect.width, context.sourceRect.height);
            }

            return result;
        }
    }

    public static class NowDeformers
    {
        public static NowGenieDeformer Genie(
            NowRect targetRect,
            float progress,
            NowEffectDirection direction = NowEffectDirection.Bottom)
        {
            return new NowGenieDeformer(targetRect, progress, direction);
        }

        public static NowWaveDeformer Wave(
            float time,
            float amplitude,
            float wavelength,
            NowWaveAxis axis = NowWaveAxis.Y)
        {
            return new NowWaveDeformer(time, amplitude, wavelength, axis);
        }
    }

    public static class NowEffects
    {
        const double CacheLifetimeSeconds = 10.0;

        static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(16);
        static readonly List<int> _removeIds = new List<int>(8);
        static double _lastCleanupTime;

        public static NowModifierBuilder<TDeformer> Modifier<TDeformer>(
            TDeformer deformer,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
            where TDeformer : struct, INowVertexDeformer
        {
            return new NowModifierBuilder<TDeformer>(deformer, NowControls.SiteId(file, line));
        }

        public static NowSnapshotBuilder Snapshot(
            NowRect rect,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowSnapshotBuilder(rect, NowControls.SiteId(file, line));
        }

        internal static NowModifierScope<TDeformer> BeginModifier<TDeformer>(
            int id,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool renderToTexture,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect)
            where TDeformer : struct, INowVertexDeformer
        {
            var entry = GetEntry(id, out bool temporary);
            entry.inUse = true;
            entry.lastUsedTime = NowTime.realtimeSinceStartup;
            NowInput.BeginPassive();
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile = default;

            try
            {
                captureProfile = NowProfiler.EffectsCapture.Auto();
                var drawScope = entry.capture.Begin(CaptureSize(), Vector2.zero, inheritContext: true);
                return new NowModifierScope<TDeformer>(
                    id,
                    entry,
                    temporary,
                    deformer,
                    subdivision,
                    renderToTexture,
                    subdivideText,
                    hasSourceRect,
                    sourceRect,
                    drawScope,
                    captureProfile);
            }
            catch
            {
                captureProfile.Dispose();
                entry.inUse = false;
                NowInput.EndPassive();

                if (temporary)
                    entry.Dispose();

                throw;
            }
        }

        internal static NowSnapshotScope BeginSnapshot(int id, NowRect rect)
        {
            var entry = GetEntry(id, out bool temporary);
            entry.inUse = true;
            entry.lastUsedTime = NowTime.realtimeSinceStartup;
            NowInput.BeginPassive();
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile = default;

            try
            {
                captureProfile = NowProfiler.EffectsCapture.Auto();
                var drawScope = entry.capture.Begin(CaptureSize(), Vector2.zero, inheritContext: true);
                return new NowSnapshotScope(id, entry, temporary, rect, drawScope, captureProfile);
            }
            catch
            {
                captureProfile.Dispose();
                entry.inUse = false;
                NowInput.EndPassive();

                if (temporary)
                    entry.Dispose();

                throw;
            }
        }

        internal static void EndModifier<TDeformer>(ref NowModifierScope<TDeformer> scope)
            where TDeformer : struct, INowVertexDeformer
        {
            var entry = scope.entry;

            try
            {
                try
                {
                    scope.drawScope.Dispose();
                }
                finally
                {
                    scope.captureProfile.Dispose();
                    NowInput.EndPassive();
                }

                if (!entry.capture.hasGeometry)
                    return;

                NowRect sourceRect = scope.hasSourceRect && !scope.sourceRect.isEmpty
                    ? scope.sourceRect
                    : Now.TryGetDrawListBounds(entry.capture, out var inferred)
                        ? inferred
                        : default;

                if (sourceRect.isEmpty)
                    return;

                if (scope.renderToTexture)
                {
                    var textureRect = Now.PixelSnapOutward(sourceRect);
                    var target = entry.GetTarget(textureRect);
                    Now.RenderDrawListToTexture(entry.capture, textureRect, target, entry.commandBuffer);

                    using (entry.surface.Begin(CaptureSize(), Vector2.zero, inheritContext: true))
                    {
                        Now.Rectangle(textureRect)
                            .SetTexture(target)
                            .Draw();
                    }

                    Now.DrawCapturedDrawList(
                        entry.surface,
                        scope.deformer,
                        scope.subdivision,
                        scope.subdivideText,
                        true,
                        textureRect,
                        scope.id);
                    return;
                }

                Now.DrawCapturedDrawList(
                    entry.capture,
                    scope.deformer,
                    scope.subdivision,
                    scope.subdivideText,
                    true,
                    sourceRect,
                    scope.id);
            }
            finally
            {
                entry.capture.Clear();
                entry.surface.Clear();
                entry.inUse = false;

                if (scope.temporaryEntry)
                    entry.Dispose();
                else
                    CleanupUnusedEntries();
            }
        }

        internal static void EndSnapshot(ref NowSnapshotScope scope)
        {
            var entry = scope.entry;

            try
            {
                try
                {
                    scope.drawScope.Dispose();
                }
                finally
                {
                    scope.captureProfile.Dispose();
                    NowInput.EndPassive();
                }

                if (!scope.rect.isEmpty && entry.capture.hasGeometry)
                {
                    var textureRect = Now.PixelSnapOutward(scope.rect);
                    var target = entry.GetTarget(textureRect);
                    Now.RenderDrawListToTexture(entry.capture, textureRect, target, entry.commandBuffer);
                }
            }
            finally
            {
                entry.capture.Clear();
                entry.inUse = false;

                if (scope.temporaryEntry)
                    entry.Dispose();
                else
                    CleanupUnusedEntries();
            }
        }

        static Vector2 CaptureSize()
        {
            var mask = Now.screenMask;
            if (mask.width > 0f && mask.height > 0f)
                return mask.size;

            return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        static Entry GetEntry(int id, out bool temporary)
        {
            if (_entries.TryGetValue(id, out var entry) && !entry.inUse)
            {
                temporary = false;
                return entry;
            }

            if (entry == null)
            {
                entry = new Entry();
                _entries[id] = entry;
                temporary = false;
                return entry;
            }

            temporary = true;
            return new Entry();
        }

        static void CleanupUnusedEntries()
        {
            double now = NowTime.realtimeSinceStartup;

            if (now - _lastCleanupTime < 1.0)
                return;

            _lastCleanupTime = now;
            _removeIds.Clear();

            foreach (var kvp in _entries)
            {
                if (!kvp.Value.inUse && now - kvp.Value.lastUsedTime > CacheLifetimeSeconds)
                    _removeIds.Add(kvp.Key);
            }

            for (int i = 0; i < _removeIds.Count; ++i)
            {
                int id = _removeIds[i];
                _entries[id].Dispose();
                _entries.Remove(id);
            }

            _removeIds.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            foreach (var entry in _entries.Values)
                entry.Dispose();

            _entries.Clear();
            _lastCleanupTime = 0.0;
        }

        internal sealed class Entry : IDisposable
        {
            public readonly NowDrawList capture = new NowDrawList();
            public readonly NowDrawList surface = new NowDrawList();
            public readonly CommandBuffer commandBuffer = new CommandBuffer { name = "Now Effects" };
            public RenderTexture target;
            public bool inUse;
            public double lastUsedTime;

            public RenderTexture GetTarget(NowRect rect)
            {
                int width = Mathf.Max(1, Mathf.RoundToInt(Now.UiUnitsToScreenPixels(rect.width)));
                int height = Mathf.Max(1, Mathf.RoundToInt(Now.UiUnitsToScreenPixels(rect.height)));

                if (target != null && target.width == width && target.height == height)
                    return target;

                ReleaseTarget();

                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Now Effects",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1
                };
                target.Create();
                return target;
            }

            public void Dispose()
            {
                capture.Dispose();
                surface.Dispose();
                commandBuffer.Release();
                ReleaseTarget();
            }

            void ReleaseTarget()
            {
                if (target == null)
                    return;

                target.Release();

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(target);
                else
                    UnityEngine.Object.DestroyImmediate(target);

                target = null;
            }
        }
    }

    [NowBuilder]
    public struct NowModifierBuilder<TDeformer>
        where TDeformer : struct, INowVertexDeformer
    {
        readonly TDeformer _deformer;
        readonly int _site;
        NowId _id;
        NowSubdivision _subdivision;
        bool _renderToTexture;
        bool _subdivideText;
        bool _hasSourceRect;
        NowRect _sourceRect;

        internal NowModifierBuilder(TDeformer deformer, int site)
        {
            _deformer = deformer;
            _site = site;
            _id = default;
            _subdivision = NowSubdivision.None;
            _renderToTexture = false;
            _subdivideText = false;
            _hasSourceRect = false;
            _sourceRect = default;
        }

        public NowModifierBuilder<TDeformer> SetId(NowId id)
        {
            _id = id;
            return this;
        }

        public NowModifierBuilder<TDeformer> SetSubdivision(int divisions)
        {
            _subdivision = NowSubdivision.Fixed(divisions);
            return this;
        }

        public NowModifierBuilder<TDeformer> SetSubdivision(NowSubdivision subdivision)
        {
            _subdivision = subdivision;
            return this;
        }

        public NowModifierBuilder<TDeformer> SetRenderToTexture(bool enabled = true)
        {
            _renderToTexture = enabled;
            return this;
        }

        public NowModifierBuilder<TDeformer> SetSubdivideText(bool enabled = true)
        {
            _subdivideText = enabled;
            return this;
        }

        public NowModifierBuilder<TDeformer> SetSourceRect(NowRect rect)
        {
            _sourceRect = rect;
            _hasSourceRect = true;
            return this;
        }

        public NowModifierScope<TDeformer> Begin()
        {
            int id = NowControls.GetControlId(_id, _site);
            return NowEffects.BeginModifier(
                id,
                _deformer,
                _subdivision,
                _renderToTexture,
                _subdivideText,
                _hasSourceRect,
                _sourceRect);
        }
    }

    [NowScope]
    public struct NowModifierScope<TDeformer> : IDisposable
        where TDeformer : struct, INowVertexDeformer
    {
        internal int id;
        internal NowEffects.Entry entry;
        internal bool temporaryEntry;
        internal TDeformer deformer;
        internal NowSubdivision subdivision;
        internal bool renderToTexture;
        internal bool subdivideText;
        internal bool hasSourceRect;
        internal NowRect sourceRect;
        internal NowDrawScope drawScope;
        internal Unity.Profiling.ProfilerMarker.AutoScope captureProfile;
        bool _disposed;

        internal NowModifierScope(
            int id,
            NowEffects.Entry entry,
            bool temporaryEntry,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool renderToTexture,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect,
            NowDrawScope drawScope,
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile)
        {
            this.id = id;
            this.entry = entry;
            this.temporaryEntry = temporaryEntry;
            this.deformer = deformer;
            this.subdivision = subdivision;
            this.renderToTexture = renderToTexture;
            this.subdivideText = subdivideText;
            this.hasSourceRect = hasSourceRect;
            this.sourceRect = sourceRect;
            this.drawScope = drawScope;
            this.captureProfile = captureProfile;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowEffects.EndModifier(ref this);
        }
    }

    [NowBuilder]
    public struct NowSnapshotBuilder
    {
        readonly int _site;
        readonly NowRect _rect;
        NowId _id;

        internal NowSnapshotBuilder(NowRect rect, int site)
        {
            _rect = rect;
            _site = site;
            _id = default;
        }

        public NowSnapshotBuilder SetId(NowId id)
        {
            _id = id;
            return this;
        }

        public NowSnapshotScope Begin()
        {
            int id = NowControls.GetControlId(_id, _site);
            return NowEffects.BeginSnapshot(id, _rect);
        }
    }

    [NowScope]
    public struct NowSnapshotScope : IDisposable
    {
        internal int id;
        internal NowEffects.Entry entry;
        internal bool temporaryEntry;
        internal NowRect rect;
        internal NowDrawScope drawScope;
        internal Unity.Profiling.ProfilerMarker.AutoScope captureProfile;
        bool _disposed;

        public RenderTexture texture => entry?.target;

        public Texture Texture => texture;

        internal NowSnapshotScope(
            int id,
            NowEffects.Entry entry,
            bool temporaryEntry,
            NowRect rect,
            NowDrawScope drawScope,
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile)
        {
            this.id = id;
            this.entry = entry;
            this.temporaryEntry = temporaryEntry;
            this.rect = rect;
            this.drawScope = drawScope;
            this.captureProfile = captureProfile;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowEffects.EndSnapshot(ref this);
        }
    }

    public static partial class Now
    {
        internal static bool TryGetDrawListBounds(NowDrawList drawList, out NowRect bounds)
        {
            return NowEffectsMesh.TryGetDrawListBounds(drawList, out bounds);
        }

        internal static void RenderDrawListToTexture(
            NowDrawList drawList,
            NowRect sourceRect,
            RenderTexture target,
            CommandBuffer commandBuffer)
        {
            NowEffectsMesh.RenderDrawListToTexture(drawList, sourceRect, target, commandBuffer);
        }

        internal static void DrawCapturedDrawList<TDeformer>(
            NowDrawList drawList,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect,
            int effectId)
            where TDeformer : struct, INowVertexDeformer
        {
            NowEffectsMesh.DrawCapturedDrawList(
                drawList,
                deformer,
                subdivision,
                subdivideText,
                hasSourceRect,
                sourceRect,
                effectId);
        }

        internal static NowRect PixelSnapOutward(NowRect rect)
        {
            return NowEffectsMesh.PixelSnapOutward(rect);
        }
    }
}
