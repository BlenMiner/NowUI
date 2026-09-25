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

    /// <summary>
    /// How a modifier splits captured quads before deforming them. Deformers move
    /// vertices, so a large quad stays a flat quad unless it is subdivided; a wave
    /// or a perspective turn needs enough vertices to follow its curve.
    /// </summary>
    public readonly struct NowSubdivision
    {
        internal enum SubdivisionMode
        {
            None,
            Fixed,
            MaxCellSize,
            Auto
        }

        /// <summary>Largest number of cells per axis a single quad is split into.</summary>
        public const int MaxDivisionsPerAxis = 128;

        internal readonly SubdivisionMode mode;
        internal readonly int divisions;
        internal readonly Vector2 maxCellSize;

        NowSubdivision(SubdivisionMode mode, int divisions, Vector2 maxCellSize)
        {
            this.mode = mode;
            this.divisions = divisions;
            this.maxCellSize = maxCellSize;
        }

        /// <summary>No subdivision: every captured vertex is deformed as drawn.</summary>
        public static NowSubdivision None => default;

        /// <summary>
        /// The default for modifiers. Built-in deformers choose the density their
        /// shape needs at the size being deformed: <see cref="NowDeformers.Wave"/>
        /// samples each wavelength along its axis, <see cref="NowDeformers.Genie"/>
        /// finely along the direction it pulls (and coarsely across it), and <see cref="NowDeformers.Perspective(float, float)"/>
        /// both axes. Custom deformers are not subdivided unless the modifier sets a
        /// mode explicitly.
        /// </summary>
        public static NowSubdivision Auto => new NowSubdivision(SubdivisionMode.Auto, 0, default);

        /// <summary>Splits every quad into the same number of cells per axis, whatever its size.</summary>
        public static NowSubdivision Fixed(int divisions)
        {
            return new NowSubdivision(SubdivisionMode.Fixed, Mathf.Clamp(divisions, 1, MaxDivisionsPerAxis), default);
        }

        /// <summary>Splits each quad into cells no larger than <paramref name="size"/> UI units.</summary>
        public static NowSubdivision MaxCellSize(float size)
        {
            size = Mathf.Max(1f, size);
            return new NowSubdivision(SubdivisionMode.MaxCellSize, 0, new Vector2(size, size));
        }

        /// <summary>
        /// Splits each quad into cells no larger than <paramref name="width"/> by
        /// <paramref name="height"/> UI units. Pass <see cref="float.PositiveInfinity"/>
        /// for an axis the deformer does not bend.
        /// </summary>
        public static NowSubdivision MaxCellSize(float width, float height)
        {
            return new NowSubdivision(
                SubdivisionMode.MaxCellSize,
                0,
                new Vector2(float.IsNaN(width) ? float.PositiveInfinity : Mathf.Max(1f, width),
                    float.IsNaN(height) ? float.PositiveInfinity : Mathf.Max(1f, height)));
        }
    }

    /// <summary>
    /// One captured vertex passed to <see cref="INowVertexDeformer.Deform"/>.
    /// </summary>
    public readonly struct NowEffectVertex
    {
        /// <summary>
        /// Position in top-left-origin UI units (y down), after the active
        /// <c>Now.Transform</c> (and any <c>Now.Rotate</c> scope closed inside the
        /// modifier) but before the host's UI scale; it is not a physical pixel
        /// coordinate. Return a position in the same space. A rotation scope that
        /// encloses the modifier turns the deformed result afterwards.
        /// </summary>
        public readonly Vector2 position;

        /// <summary><see cref="position"/> normalized to <see cref="NowEffectContext.sourceRect"/>, clamped to 0..1.</summary>
        public readonly Vector2 normalized;

        /// <summary>The vertex's primary texture coordinate.</summary>
        public readonly Vector2 uv;

        /// <summary>Index of the vertex within its captured batch.</summary>
        public readonly int index;

        internal NowEffectVertex(Vector2 position, Vector2 normalized, Vector2 uv, int index)
        {
            this.position = position;
            this.normalized = normalized;
            this.uv = uv;
            this.index = index;
        }
    }

    /// <summary>
    /// Per-scope values shared by every vertex passed to a deformer.
    /// </summary>
    public readonly struct NowEffectContext
    {
        /// <summary>The modifier's resolved id.</summary>
        public readonly NowResolvedId id;

        /// <summary>
        /// The region the deformer treats as its source, in the same space as
        /// <see cref="NowEffectVertex.position"/>. It is the rect passed to
        /// <see cref="NowModifierBuilder{TDeformer}.SetSourceRect(NowRect)"/>
        /// (mapped through the transform that was active when the modifier began) or,
        /// without one, the bounds of every captured vertex, which include each
        /// shape's anti-aliasing padding. Use its center or a normalized point
        /// inside it as a pivot.
        /// </summary>
        public readonly NowRect sourceRect;

        /// <summary>
        /// Caller-provided time set through
        /// <see cref="NowModifierBuilder{TDeformer}.SetTime(float)"/>; 0 when none
        /// was supplied. NowUI never reads a clock on the caller's behalf.
        /// </summary>
        public readonly float time;

        internal NowEffectContext(NowResolvedId id, NowRect sourceRect, float time)
        {
            this.id = id;
            this.sourceRect = sourceRect;
            this.time = time;
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

        /// <summary>
        /// Twelve samples per wavelength along the axis the offset varies with; the
        /// other axis moves rigidly and needs none.
        /// </summary>
        internal NowSubdivision AutoSubdivision(in NowEffectContext context)
        {
            if (Mathf.Abs(_amplitude) < 0.05f)
                return NowSubdivision.None;

            float cell = _wavelength / 12f;
            return _axis == NowWaveAxis.Y
                ? NowSubdivision.MaxCellSize(cell, float.PositiveInfinity)
                : NowSubdivision.MaxCellSize(float.PositiveInfinity, cell);
        }

        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            var position = vertex.position;
            float distance = _axis == NowWaveAxis.Y
                ? vertex.normalized.x * Mathf.Max(1f, context.sourceRect.width)
                : vertex.normalized.y * Mathf.Max(1f, context.sourceRect.height);
            float offset = Mathf.Sin((distance / _wavelength + _time + context.time) * Mathf.PI * 2f) * _amplitude;

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

        /// <summary>
        /// The pull varies along the genie's direction, so that axis gets fine cells.
        /// Each row still narrows toward the target, and a trapezoid drawn as two
        /// triangles skews its texture along the diagonal, so the cross axis gets
        /// coarser cells too.
        /// </summary>
        internal NowSubdivision AutoSubdivision(in NowEffectContext context)
        {
            if (_progress <= 0f)
                return NowSubdivision.None;

            bool vertical = _direction == NowEffectDirection.Top || _direction == NowEffectDirection.Bottom;
            float along = vertical ? context.sourceRect.height : context.sourceRect.width;
            float across = vertical ? context.sourceRect.width : context.sourceRect.height;
            float alongCell = Mathf.Max(2f, along / 32f);
            float acrossCell = Mathf.Max(2f, across / 16f);
            return vertical
                ? NowSubdivision.MaxCellSize(acrossCell, alongCell)
                : NowSubdivision.MaxCellSize(alongCell, acrossCell);
        }

        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            float eased = Smooth(_progress);
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

            // The edge nearest the target leads and the far edge follows up to
            // 35% of the timeline later, so every vertex reaches the target
            // exactly when progress reaches 1.
            float delay = (1f - along) * 0.35f;
            float localPull = Smooth(Mathf.Clamp01((eased - delay) / (1f - delay)));
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

        static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Rotates captured geometry in 3D around a pivot inside the source rect and
    /// projects it back with a pinhole perspective. Build it with
    /// <see cref="NowDeformers.Perspective(float, float)"/>.
    /// </summary>
    public readonly struct NowPerspectiveDeformer : INowVertexDeformer
    {
        readonly float _cosYaw;
        readonly float _sinYaw;
        readonly float _cosPitch;
        readonly float _sinPitch;
        readonly float _distance;
        readonly Vector2 _pivot;

        internal NowPerspectiveDeformer(float yawDegrees, float pitchDegrees, float distance, Vector2 pivot)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            _cosYaw = Mathf.Cos(yaw);
            _sinYaw = Mathf.Sin(yaw);
            _cosPitch = Mathf.Cos(pitch);
            _sinPitch = Mathf.Sin(pitch);
            _distance = Mathf.Max(0.1f, distance);
            _pivot = pivot;
        }

        /// <summary>
        /// Perspective divides by depth, which a single flat quad cannot express;
        /// sixteen cells across the source keep large quads, gradients and SDF
        /// scenes from folding along their diagonal.
        /// </summary>
        internal NowSubdivision AutoSubdivision(in NowEffectContext context)
        {
            if (Mathf.Abs(_sinYaw) < 0.0001f && Mathf.Abs(_sinPitch) < 0.0001f)
                return NowSubdivision.None;

            float cellWidth = Mathf.Abs(_sinYaw) < 0.0001f ? float.PositiveInfinity : Mathf.Max(2f, context.sourceRect.width / 16f);
            float cellHeight = Mathf.Abs(_sinPitch) < 0.0001f ? float.PositiveInfinity : Mathf.Max(2f, context.sourceRect.height / 16f);
            return NowSubdivision.MaxCellSize(cellWidth, cellHeight);
        }

        public Vector2 Deform(in NowEffectVertex vertex, in NowEffectContext context)
        {
            NowRect source = context.sourceRect;
            Vector2 pivot = new Vector2(
                source.x + source.width * _pivot.x,
                source.y + source.height * _pivot.y);
            Vector2 p = vertex.position - pivot;

            // Yaw turns about the vertical axis (positive: right edge away from the
            // viewer), then pitch about the horizontal axis (positive: top edge away).
            float x = p.x * _cosYaw;
            float z = p.x * _sinYaw;
            float y = p.y * _cosPitch + z * _sinPitch;
            z = z * _cosPitch - p.y * _sinPitch;

            float eye = _distance * Mathf.Max(1f, Mathf.Max(source.width, source.height));
            float scale = eye / Mathf.Max(eye * 0.05f, eye + z);
            return pivot + new Vector2(x * scale, y * scale);
        }
    }

    public static class NowDeformers
    {
        /// <summary>
        /// A 3D card turn around the center of the source rect. The modifier's
        /// default <see cref="NowSubdivision.Auto"/> subdivides large quads,
        /// gradients and SDF scenes so they stay in perspective instead of folding
        /// along their diagonal.
        /// </summary>
        /// <param name="yawDegrees">Turn about the vertical axis; positive moves the right edge away.</param>
        /// <param name="pitchDegrees">Turn about the horizontal axis; positive moves the top edge away.</param>
        public static NowPerspectiveDeformer Perspective(float yawDegrees, float pitchDegrees)
        {
            return new NowPerspectiveDeformer(yawDegrees, pitchDegrees, 3f, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// A 3D card turn around a pivot inside the source rect.
        /// </summary>
        /// <param name="yawDegrees">Turn about the vertical axis; positive moves the right edge away.</param>
        /// <param name="pitchDegrees">Turn about the horizontal axis; positive moves the top edge away.</param>
        /// <param name="distance">Camera distance in multiples of the source rect's larger side
        /// (minimum 0.1); smaller values exaggerate the perspective. The default is 3.</param>
        /// <param name="normalizedPivot">Pivot relative to the source rect: (0, 0) is its
        /// top-left and (1, 1) its bottom-right.</param>
        public static NowPerspectiveDeformer Perspective(
            float yawDegrees,
            float pitchDegrees,
            float distance,
            Vector2 normalizedPivot)
        {
            return new NowPerspectiveDeformer(yawDegrees, pitchDegrees, distance, normalizedPivot);
        }

        /// <summary>
        /// Pulls the captured content into <paramref name="targetRect"/> like a
        /// window minimizing into a dock icon. <paramref name="progress"/> runs from
        /// 0 (untouched) to 1 (fully inside the target); the edge nearest the target
        /// leads and the far edge follows. <paramref name="direction"/> names the side
        /// of the source the target lies toward.
        /// </summary>
        public static NowGenieDeformer Genie(
            NowRect targetRect,
            float progress,
            NowEffectDirection direction = NowEffectDirection.Bottom)
        {
            return new NowGenieDeformer(targetRect, progress, direction);
        }

        /// <summary>
        /// Builds a sine-wave vertex deformer. <paramref name="time"/> is the wave
        /// phase in cycles; it composes additively with the modifier's
        /// <see cref="NowModifierBuilder{TDeformer}.SetTime(float)"/> channel, so
        /// passing 0 here and calling <c>SetTime(Time.time)</c> on the modifier
        /// animates the wave the same way.
        /// </summary>
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

        const int MaxPooledTemporaryEntries = 8;

        static readonly Dictionary<NowResolvedId, Entry> _entries = new Dictionary<NowResolvedId, Entry>(16);
        static readonly List<NowResolvedId> _removeIds = new List<NowResolvedId>(8);
        static readonly Stack<Entry> _temporaryEntryPool = new Stack<Entry>(4);
        static readonly NowScopeGuard _effectScopes = new NowScopeGuard("NowEffects", 8);
        static double _lastCleanupTime;
        static int _textureCaptureDepth;

        internal static bool isCapturingToTexture => _textureCaptureDepth > 0;

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
            NowResolvedId id,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool renderToTexture,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect,
            float time)
            where TDeformer : struct, INowVertexDeformer
        {
            // Captured vertices arrive already transformed; map an authored source
            // rect into that same space so deformers and texture capture agree.
            if (hasSourceRect && !sourceRect.isEmpty)
                sourceRect = Now.TransformScreenRect(sourceRect);

            var entry = GetEntry(id, out bool temporary);
            entry.inUse = true;
            entry.lastUsedTime = NowTime.realtimeSinceStartup;
            NowInput.BeginPassive();
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile = default;
            int scopeToken = 0;

            try
            {
                captureProfile = NowProfiler.EffectsCapture.Auto();
                var drawScope = entry.capture.Begin(
                    CaptureSize(),
                    Vector2.zero,
                    inheritContext: true,
                    flushOverlays: false);
                scopeToken = _effectScopes.Enter();

                if (renderToTexture)
                    ++_textureCaptureDepth;

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
                    time,
                    drawScope,
                    captureProfile,
                    scopeToken);
            }
            catch
            {
                if (scopeToken != 0)
                    _effectScopes.Exit(scopeToken);

                captureProfile.Dispose();
                entry.inUse = false;
                NowInput.EndPassive();

                if (temporary)
                    ReturnTemporaryEntry(entry);

                throw;
            }
        }

        internal static NowSnapshotScope BeginSnapshot(NowResolvedId id, NowRect rect)
        {
            var entry = GetEntry(id, out bool temporary);
            entry.inUse = true;
            entry.lastUsedTime = NowTime.realtimeSinceStartup;
            NowInput.BeginPassive();
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile = default;
            int scopeToken = 0;

            try
            {
                captureProfile = NowProfiler.EffectsCapture.Auto();
                var drawScope = entry.capture.Begin(
                    CaptureSize(),
                    Vector2.zero,
                    inheritContext: true,
                    flushOverlays: false);
                scopeToken = _effectScopes.Enter();
                ++_textureCaptureDepth;
                return new NowSnapshotScope(
                    id,
                    entry,
                    temporary,
                    rect,
                    drawScope,
                    captureProfile,
                    scopeToken);
            }
            catch
            {
                if (scopeToken != 0)
                    _effectScopes.Exit(scopeToken);

                captureProfile.Dispose();
                entry.inUse = false;
                NowInput.EndPassive();

                if (temporary)
                    ReturnTemporaryEntry(entry);

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

                bool hasExplicitSourceRect = scope.hasSourceRect && !scope.sourceRect.isEmpty;

                if (scope.renderToTexture)
                {
                    NowRect sourceRect = hasExplicitSourceRect
                        ? scope.sourceRect
                        : Now.TryGetDrawListBounds(entry.capture, out var inferred)
                            ? inferred
                            : default;

                    if (sourceRect.isEmpty)
                        return;

                    var textureRect = Now.PixelSnapOutward(sourceRect);
                    var target = entry.GetTarget(textureRect);
                    Now.RenderDrawListToTexture(entry.capture, textureRect, target, entry.commandBuffer);

                    using (Now.SuppressShaderMaskCapture())
                    using (entry.surface.Begin(
                        CaptureSize(),
                        Vector2.zero,
                        inheritContext: true,
                        flushOverlays: false))
                    // textureRect is already in screen space and the captured
                    // pixels already carry the ambient tint, so draw the
                    // flattened surface without applying either a second time.
                    using (Now.ApplyTransformSnapshot(new Now.NowTransformSnapshot(true, Now.NowTransform.identity)))
                    using (Now.ApplyTintSnapshot(Vector4.one))
                    {
                        Now.Rectangle(textureRect)
                            .SetTexture(target, premultipliedAlpha: true)
                            .Draw();
                    }

                    Now.DrawCapturedDrawList(
                        entry.surface,
                        scope.deformer,
                        scope.subdivision,
                        scope.subdivideText,
                        true,
                        textureRect,
                        scope.id,
                        scope.time);
                    return;
                }

                Now.DrawCapturedDrawList(
                    entry.capture,
                    scope.deformer,
                    scope.subdivision,
                    scope.subdivideText,
                    hasExplicitSourceRect,
                    hasExplicitSourceRect ? scope.sourceRect : default,
                    scope.id,
                    scope.time);
            }
            finally
            {
                if (scope.renderToTexture)
                    _textureCaptureDepth = Mathf.Max(0, _textureCaptureDepth - 1);

                entry.capture.Clear();
                entry.surface.Clear();
                entry.inUse = false;

                if (scope.temporaryEntry)
                    ReturnTemporaryEntry(entry);
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
                _textureCaptureDepth = Mathf.Max(0, _textureCaptureDepth - 1);
                entry.capture.Clear();
                entry.inUse = false;

                if (scope.temporaryEntry)
                    ReturnTemporaryEntry(entry);
                else
                    CleanupUnusedEntries();
            }
        }

        internal static bool BeginEffectScopeEnd(int token)
        {
            return _effectScopes.BeginEnd(token);
        }

        internal static bool IsEffectScopeCurrent(int token)
        {
            return _effectScopes.IsCurrent(token);
        }

        internal static void EndEffectScope(int token)
        {
            _effectScopes.ExitEnding(token);
        }

        static Vector2 CaptureSize()
        {
            var mask = Now.screenMask;
            if (mask.width > 0f && mask.height > 0f)
                return mask.size;

            return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        static Entry GetEntry(NowResolvedId id, out bool temporary)
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
            return RentTemporaryEntry();
        }

        static Entry RentTemporaryEntry()
        {
            return _temporaryEntryPool.Count > 0 ? _temporaryEntryPool.Pop() : new Entry();
        }

        /// <summary>
        /// Returns a temporary entry (nested same-id scope) to the pool instead of
        /// destroying its meshes and command buffer every occurrence. The render
        /// target is released because pooled entries can sit idle indefinitely.
        /// </summary>
        static void ReturnTemporaryEntry(Entry entry)
        {
            if (entry == null)
                return;

            if (_temporaryEntryPool.Count >= MaxPooledTemporaryEntries)
            {
                entry.Dispose();
                return;
            }

            entry.capture.Clear();
            entry.surface.Clear();
            entry.ReleaseTarget();
            entry.inUse = false;
            _temporaryEntryPool.Push(entry);
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
                NowResolvedId id = _removeIds[i];
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

            while (_temporaryEntryPool.Count > 0)
                _temporaryEntryPool.Pop().Dispose();

            _effectScopes.Clear();
            _lastCleanupTime = 0.0;
            _textureCaptureDepth = 0;
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

            public void ReleaseTarget()
            {
                if (target == null)
                    return;

                Now.ReleaseTextureMaterials(target);
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
        readonly NowCallSiteId _site;
        NowControlIdentity _id;
        NowSubdivision _subdivision;
        bool _renderToTexture;
        bool _subdivideText;
        bool _hasSourceRect;
        NowRect _sourceRect;
        float _time;

        internal NowModifierBuilder(TDeformer deformer, NowCallSiteId site)
        {
            _deformer = deformer;
            _site = site;
            _id = default;
            _subdivision = NowSubdivision.Auto;
            _renderToTexture = false;
            _subdivideText = false;
            _hasSourceRect = false;
            _sourceRect = default;
            _time = 0f;
        }

        public NowModifierBuilder<TDeformer> SetId(NowId id)
        {
            _id = id;
            return this;
        }

        /// <summary>Uses an identity already resolved by the active host.</summary>
        public NowModifierBuilder<TDeformer> SetId(NowResolvedId id)
        {
            _id = id;
            return this;
        }

        /// <summary>
        /// Splits every captured quad into <paramref name="divisions"/> cells per
        /// axis. Prefer the default <see cref="NowSubdivision.Auto"/> for built-in
        /// deformers, or <see cref="NowSubdivision.MaxCellSize(float)"/>, which scales
        /// with the quad's size.
        /// </summary>
        public NowModifierBuilder<TDeformer> SetSubdivision(int divisions)
        {
            _subdivision = NowSubdivision.Fixed(divisions);
            return this;
        }

        /// <summary>
        /// Chooses how captured quads are split before deforming. The default,
        /// <see cref="NowSubdivision.Auto"/>, lets built-in deformers pick their own
        /// density; custom deformers need an explicit mode to bend large quads.
        /// </summary>
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

        /// <summary>
        /// Overrides the region reported as <see cref="NowEffectContext.sourceRect"/>
        /// (and flattened by <see cref="SetRenderToTexture(bool)"/>) instead of the
        /// captured bounds. The rect is authored in the current coordinate space: the
        /// active <c>Now.Transform</c> maps it into the vertex space when the modifier
        /// begins.
        /// </summary>
        public NowModifierBuilder<TDeformer> SetSourceRect(NowRect rect)
        {
            _sourceRect = rect;
            _hasSourceRect = true;
            return this;
        }

        /// <summary>
        /// Time exposed to deformers through <see cref="NowEffectContext.time"/>.
        /// The caller passes its own clock (e.g. <c>SetTime(Time.time)</c>);
        /// without it the context time stays 0. Built-in deformers:
        /// <see cref="NowWaveDeformer"/> adds this to its constructor phase;
        /// <see cref="NowGenieDeformer"/> is driven by its progress argument and
        /// ignores it. Custom deformers opt in by reading the context time.
        /// </summary>
        public NowModifierBuilder<TDeformer> SetTime(float time)
        {
            _time = time;
            return this;
        }

        public NowModifierScope<TDeformer> Begin()
        {
            NowResolvedId id = _id.Resolve(_site).InDomain(NowIdDomain.Effect);
            return NowEffects.BeginModifier(
                id,
                _deformer,
                _subdivision,
                _renderToTexture,
                _subdivideText,
                _hasSourceRect,
                _sourceRect,
                _time);
        }
    }

    [NowScope]
    public struct NowModifierScope<TDeformer> : IDisposable
        where TDeformer : struct, INowVertexDeformer
    {
        internal NowResolvedId id;
        internal NowEffects.Entry entry;
        internal bool temporaryEntry;
        internal TDeformer deformer;
        internal NowSubdivision subdivision;
        internal bool renderToTexture;
        internal bool subdivideText;
        internal bool hasSourceRect;
        internal NowRect sourceRect;
        internal float time;
        internal NowDrawScope drawScope;
        internal Unity.Profiling.ProfilerMarker.AutoScope captureProfile;
        int _token;

        internal NowModifierScope(
            NowResolvedId id,
            NowEffects.Entry entry,
            bool temporaryEntry,
            TDeformer deformer,
            NowSubdivision subdivision,
            bool renderToTexture,
            bool subdivideText,
            bool hasSourceRect,
            NowRect sourceRect,
            float time,
            NowDrawScope drawScope,
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile,
            int token)
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
            this.time = time;
            this.drawScope = drawScope;
            this.captureProfile = captureProfile;
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            if (!NowEffects.IsEffectScopeCurrent(_token))
            {
                _token = 0;
                return;
            }

            // Validate the owned mesh capture before claiming effect teardown.
            // If an unrelated draw scope is still above it, the exception must
            // leave both owners intact so disposal can be retried in order.
            drawScope.ValidateDisposeOrder();

            if (!NowEffects.BeginEffectScopeEnd(_token))
            {
                _token = 0;
                return;
            }

            int token = _token;

            try
            {
                NowEffects.EndModifier(ref this);
            }
            finally
            {
                NowEffects.EndEffectScope(token);
                _token = 0;
            }
        }
    }

    [NowBuilder]
    public struct NowSnapshotBuilder
    {
        readonly NowCallSiteId _site;
        readonly NowRect _rect;
        NowControlIdentity _id;

        internal NowSnapshotBuilder(NowRect rect, NowCallSiteId site)
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

        /// <summary>Uses an identity already resolved by the active host.</summary>
        public NowSnapshotBuilder SetId(NowResolvedId id)
        {
            _id = id;
            return this;
        }

        public NowSnapshotScope Begin()
        {
            NowResolvedId id = _id.Resolve(_site).InDomain(NowIdDomain.Effect);
            return NowEffects.BeginSnapshot(id, _rect);
        }
    }

    [NowScope]
    public struct NowSnapshotScope : IDisposable
    {
        internal NowResolvedId id;
        internal NowEffects.Entry entry;
        internal bool temporaryEntry;
        internal NowRect rect;
        internal NowDrawScope drawScope;
        internal Unity.Profiling.ProfilerMarker.AutoScope captureProfile;
        int _token;

        public RenderTexture texture => entry?.target;

        public Texture Texture => texture;

        internal NowSnapshotScope(
            NowResolvedId id,
            NowEffects.Entry entry,
            bool temporaryEntry,
            NowRect rect,
            NowDrawScope drawScope,
            Unity.Profiling.ProfilerMarker.AutoScope captureProfile,
            int token)
        {
            this.id = id;
            this.entry = entry;
            this.temporaryEntry = temporaryEntry;
            this.rect = rect;
            this.drawScope = drawScope;
            this.captureProfile = captureProfile;
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            if (!NowEffects.IsEffectScopeCurrent(_token))
            {
                _token = 0;
                return;
            }

            // See NowModifierScope.Dispose: cross-family out-of-order disposal
            // must not orphan the underlying mesh capture.
            drawScope.ValidateDisposeOrder();

            if (!NowEffects.BeginEffectScopeEnd(_token))
            {
                _token = 0;
                return;
            }

            int token = _token;

            try
            {
                NowEffects.EndSnapshot(ref this);
            }
            finally
            {
                NowEffects.EndEffectScope(token);
                _token = 0;
            }
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
            NowResolvedId effectId,
            float time)
            where TDeformer : struct, INowVertexDeformer
        {
            NowEffectsMesh.DrawCapturedDrawList(
                drawList,
                deformer,
                subdivision,
                subdivideText,
                hasSourceRect,
                sourceRect,
                effectId,
                time);
        }

        internal static NowRect PixelSnapOutward(NowRect rect)
        {
            return NowEffectsMesh.PixelSnapOutward(rect);
        }
    }
}
