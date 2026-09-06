using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    public static class NowGUI
    {
        const int ControlHint = 0x4e6f7747;

        const double CacheLifetimeSeconds = 10.0;

        const double CacheCleanupIntervalSeconds = 1.0;

        readonly struct CacheKey : IEquatable<CacheKey>
        {
            readonly object _context;

            readonly int _controlId;

            public CacheKey(object context, int controlId)
            {
                _context = context;
                _controlId = controlId;
            }

            public bool Equals(CacheKey other)
            {
                return _controlId == other._controlId &&
                    ReferenceEquals(_context, other._context);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int contextHash = _context != null
                        ? RuntimeHelpers.GetHashCode(_context)
                        : 0;
                    return contextHash * 397 ^ _controlId;
                }
            }

            public bool MatchesContext(object context)
            {
                return ReferenceEquals(_context, context);
            }

            public bool hasLiveUnityContext =>
                _context is UnityEngine.Object unityContext &&
                unityContext;

            public bool hasDestroyedUnityContext =>
                _context is UnityEngine.Object unityContext &&
                !unityContext;
        }

        static readonly Dictionary<CacheKey, CacheEntry> _entries =
            new Dictionary<CacheKey, CacheEntry>();

        static readonly List<CacheKey> _removeKeys = new List<CacheKey>(8);

        static readonly NowScopeGuard _scopes = new NowScopeGuard("NowGUI.Auto", 8);

        static int _scopeFrame = -1;

        static double _lastCleanupTime;

        internal static Action<NowIMGUIInputProvider> hostRepaintDeadlineInvalidated;

        public static NowGUIScope Auto(Rect rect)
        {
            return Auto(rect, Color.clear);
        }

        public static NowGUIScope Auto(Rect rect, Color clearColor)
        {
            return Auto(rect, clearColor, 1f);
        }

        public static NowGUIScope Auto(Rect rect, Color clearColor, float pixelsPerPoint)
        {
            if (Event.current == null)
                return AutoWithoutEvent(rect);

            int controlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            EventType eventType = Event.current.type;
            return AutoForEvent(
                null,
                controlId,
                rect,
                clearColor,
                pixelsPerPoint,
                eventType == EventType.Repaint,
                true,
                eventType != EventType.Layout &&
                    eventType != EventType.Repaint);
        }

        internal static NowGUIScope AutoInContext(
            object context,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint)
        {
            return AutoInContext(context, rect, clearColor, pixelsPerPoint, true);
        }

        internal static NowGUIScope AutoInContext(
            object context,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint,
            bool hostFocused)
        {
            if (Event.current == null)
                return AutoWithoutEvent(rect);

            int controlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            EventType eventType = Event.current.type;
            return AutoForEvent(
                context,
                controlId,
                rect,
                clearColor,
                pixelsPerPoint,
                eventType == EventType.Repaint,
                hostFocused,
                eventType != EventType.Layout &&
                    eventType != EventType.Repaint);
        }

        internal static NowGUIScope AutoWithoutEvent(Rect rect)
        {
            var surface = new NowInputSurface(new Vector2(rect.width, rect.height), rect);
            var inputScope = NowInput.Begin(null, surface);

            try
            {
                return NowGUIScope.Suppress(rect, inputScope);
            }
            catch
            {
                inputScope.Dispose();
                throw;
            }
        }

        internal static NowGUIScope AutoForEvent(
            int controlId,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint,
            bool repaint)
        {
            return AutoForEvent(null, controlId, rect, clearColor, pixelsPerPoint, repaint);
        }

        internal static NowGUIScope AutoForEvent(
            object hostKey,
            int controlId,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint,
            bool repaint)
        {
            return AutoForEvent(
                hostKey,
                controlId,
                rect,
                clearColor,
                pixelsPerPoint,
                repaint,
                true,
                false);
        }

        internal static NowGUIScope AutoForEvent(
            object hostKey,
            int controlId,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint,
            bool repaint,
            bool hostFocused)
        {
            return AutoForEvent(
                hostKey,
                controlId,
                rect,
                clearColor,
                pixelsPerPoint,
                repaint,
                hostFocused,
                false);
        }

        internal static NowGUIScope AutoForEvent(
            object hostKey,
            int controlId,
            Rect rect,
            Color clearColor,
            float pixelsPerPoint,
            bool repaint,
            bool hostFocused,
            bool trackInputRepaint)
        {
            var entry = GetEntry(hostKey, controlId);

            entry.NotifyHostFocus(hostFocused, releaseNativeCapture: true);
            var inputSurface = new NowInputSurface(new Vector2(rect.width, rect.height), rect);
            var inputScope = NowInput.Begin(entry.inputProvider, inputSurface);
            bool ownsInputScope = true;
            NowFocusHostRegistrationScope focusScope = default;
            bool ownsFocusScope = false;
            ControlIdScope controlIdScope = default;
            bool ownsControlIdScope = false;
            NowFrameScope frameScope = default;
            bool ownsFrameScope = false;
            NowDrawScope drawScope = default;
            bool ownsDrawScope = false;

            try
            {
                entry.MarkUsed(NowTime.realtimeSinceStartup);
                focusScope = NowFocus.BeginHostRegistration(entry.focusHostId, null);
                ownsFocusScope = true;
                controlIdScope = NowControls.RestoreIdScope(entry.scopeId);
                ownsControlIdScope = true;
                pixelsPerPoint = Mathf.Max(1f, pixelsPerPoint);

                if (!repaint)
                {
                    if (trackInputRepaint)
                    {
                        frameScope = NowFrame.Begin(pixelsPerPoint, trackRepaint: true);
                        ownsFrameScope = true;
                    }

                    var suppressed = NowGUIScope.Suppress(
                        rect,
                        entry,
                        inputScope,
                        focusScope,
                        frameScope,
                        ownsFrameScope,
                        controlIdScope);
                    ownsControlIdScope = false;
                    ownsFrameScope = false;
                    ownsFocusScope = false;
                    ownsInputScope = false;
                    return suppressed;
                }

                if (rect.width <= 0f || rect.height <= 0f)
                {
                    var suppressed = NowGUIScope.Suppress(
                        rect,
                        inputScope,
                        focusScope,
                        controlIdScope);
                    ownsControlIdScope = false;
                    ownsFocusScope = false;
                    ownsInputScope = false;
                    return suppressed;
                }

                int pixelWidth = Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelsPerPoint));
                int pixelHeight = Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelsPerPoint));

                RenderTexture target = entry.GetTarget(pixelWidth, pixelHeight);
                frameScope = NowFrame.Begin(pixelsPerPoint, trackRepaint: true);
                ownsFrameScope = true;
                drawScope = entry.renderer.Begin(new Vector2(rect.width, rect.height));
                ownsDrawScope = true;

                var rendered = NowGUIScope.Render(
                    rect,
                    entry,
                    target,
                    drawScope,
                    clearColor,
                    inputScope,
                    focusScope,
                    frameScope,
                    controlIdScope);

                ownsDrawScope = false;
                ownsFrameScope = false;
                ownsControlIdScope = false;
                ownsFocusScope = false;
                ownsInputScope = false;
                return rendered;
            }
            catch
            {
                NowFocus.UnregisterHost(entry.focusHostId);

                try
                {
                    if (ownsDrawScope)
                        drawScope.Cancel();
                }
                finally
                {
                    try
                    {
                        if (ownsFrameScope)
                            frameScope.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            if (ownsControlIdScope)
                                controlIdScope.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                if (ownsInputScope)
                                    inputScope.Dispose();
                            }
                            finally
                            {
                                if (ownsFocusScope)
                                    focusScope.Dispose();
                            }
                        }
                    }
                }

                throw;
            }
        }

        public static void DisposeAll()
        {
            try
            {
                foreach (var entry in _entries.Values)
                    entry.Dispose();
            }
            finally
            {
                _entries.Clear();
                _removeKeys.Clear();
                _lastCleanupTime = 0.0;
            }
        }

        internal static void DisposeContext(object context)
        {
            _removeKeys.Clear();

            try
            {
                foreach (var pair in _entries)
                {
                    if (pair.Key.MatchesContext(context))
                        _removeKeys.Add(pair.Key);
                }

                for (int i = 0; i < _removeKeys.Count; ++i)
                {
                    CacheKey key = _removeKeys[i];
                    _entries[key].Dispose();
                    _entries.Remove(key);
                }
            }
            finally
            {
                _removeKeys.Clear();
            }
        }

        internal static void ResetInputProviders()
        {
            foreach (var entry in _entries.Values)
                entry.inputProvider.ResetState(releaseNativeCapture: false);
        }

        internal static void NotifyContextFocus(
            object context,
            bool focused,
            bool releaseNativeCapture)
        {
            foreach (var pair in _entries)
            {
                if (pair.Key.MatchesContext(context))
                    pair.Value.NotifyHostFocus(focused, releaseNativeCapture);
            }
        }

        internal static int BeginScope()
        {
            if (_scopes.count == 0)
                _scopeFrame = Time.frameCount;

            return _scopes.Enter();
        }

        internal static bool hasActiveScopesThisFrame =>
            _scopes.count > 0 && _scopeFrame == Time.frameCount;

        internal static void DiscardAbandonedScopes()
        {
            _scopes.Clear();
            _scopeFrame = -1;
        }

        internal static bool BeginScopeEnd(int token)
        {
            return _scopes.BeginEnd(token);
        }

        internal static void EndScope(int token)
        {
            _scopes.ExitEnding(token);

            if (_scopes.count == 0)
                _scopeFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            DisposeAll();
            DiscardAbandonedScopes();
            _lastCleanupTime = 0.0;
        }

        static CacheEntry GetEntry(object hostKey, int controlId)
        {
            var key = new CacheKey(hostKey, controlId);

            if (_entries.TryGetValue(key, out var entry))
                return entry;

            CacheContextActivity contextActivity = null;

            foreach (var pair in _entries)
            {
                if (pair.Key.MatchesContext(hostKey))
                {
                    contextActivity = pair.Value.contextActivity;
                    break;
                }
            }

            entry = new CacheEntry(
                controlId,
                hostKey,
                contextActivity ?? new CacheContextActivity());
            _entries.Add(key, entry);
            return entry;
        }

        static void CleanupUnusedEntries()
        {
            CleanupUnusedEntriesCore(null, false);
        }

        static void CleanupUnusedEntriesForActiveContext(object activeContext)
        {
            CleanupUnusedEntriesCore(activeContext, true);
        }

        // A host that keeps drawing IMGUI while its own panel is hidden still counts
        // as context activity, so idle siblings are reclaimed on the same terms as
        // for a drawing panel.
        internal static void CleanupUnusedEntriesForIdleContext(object activeContext)
        {
            foreach (var pair in _entries)
            {
                if (pair.Key.MatchesContext(activeContext))
                {
                    pair.Value.contextActivity.MarkUsed(NowTime.realtimeSinceStartup);
                    break;
                }
            }

            CleanupUnusedEntriesForActiveContext(activeContext);
        }

        static void CleanupUnusedEntriesCore(
            object activeContext,
            bool hasActiveContext)
        {
            double now = NowTime.realtimeSinceStartup;

            if (now - _lastCleanupTime < CacheCleanupIntervalSeconds)
                return;

            _lastCleanupTime = now;
            _removeKeys.Clear();

            try
            {
                foreach (var pair in _entries)
                {
                    bool expired =
                        now - pair.Value.lastUsedTime > CacheLifetimeSeconds;
                    bool expiredNonUnityContext =
                        !pair.Key.hasLiveUnityContext &&
                        !pair.Key.hasDestroyedUnityContext &&
                        expired;
                    bool expiredActiveUnityContextEntry =
                        hasActiveContext &&
                        pair.Key.hasLiveUnityContext &&
                        pair.Key.MatchesContext(activeContext) &&
                        now >= pair.Value.contextActivity.cleanupEligibleTime &&
                        expired;

                    if (pair.Key.hasDestroyedUnityContext ||
                        expiredNonUnityContext ||
                        expiredActiveUnityContextEntry)
                    {
                        _removeKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < _removeKeys.Count; ++i)
                {
                    CacheKey key = _removeKeys[i];
                    _entries[key].Dispose();
                    _entries.Remove(key);
                }
            }
            finally
            {
                _removeKeys.Clear();
            }
        }

        internal static void CompleteScope(
            CacheEntry entry,
            RenderTexture target,
            Rect rect,
            NowDrawScope drawScope,
            Color clearColor)
        {
            drawScope.Dispose();
            entry.renderer.Render(target, true, clearColor);
            // An opaque surface has already composited coverage into its clear
            // color. Alpha-blending that finished surface through IMGUI a
            // second time attenuates glyph and vector AA edge pixels.
            bool alphaBlend = clearColor.a < 1f;
            GUI.DrawTexture(rect, target, ScaleMode.StretchToFill, alphaBlend);
            CleanupUnusedEntriesForActiveContext(entry.inputProvider.hostContext);
        }

        internal sealed class CacheContextActivity
        {
            public double lastUsedTime = double.NegativeInfinity;

            public double cleanupEligibleTime = double.NegativeInfinity;

            public void MarkUsed(double now)
            {
                if (now - lastUsedTime > CacheLifetimeSeconds)
                    cleanupEligibleTime = now + CacheCleanupIntervalSeconds;

                lastUsedTime = now;
            }
        }

        internal sealed class CacheEntry : IDisposable
        {
            public readonly NowRenderer renderer = new NowRenderer();

            public readonly NowResolvedId scopeId;

            public readonly NowResolvedId focusHostId;

            public readonly NowIMGUIInputProvider inputProvider;

            public readonly CacheContextActivity contextActivity;

            public RenderTexture target;

            public double lastUsedTime;

            public CacheEntry(
                int controlId,
                object hostContext,
                CacheContextActivity contextActivity)
            {
                scopeId = NowControls.AllocateOwnerScope();
                focusHostId = scopeId.InDomain(NowIdDomain.FocusHost);
                inputProvider = new NowIMGUIInputProvider(controlId, hostContext);
                this.contextActivity = contextActivity;
            }

            public void MarkUsed(double now)
            {
                contextActivity.MarkUsed(now);
                lastUsedTime = now;
            }

            public void NotifyHostFocus(bool focused, bool releaseNativeCapture)
            {
                if (inputProvider.NotifyHostFocusChanged(focused, releaseNativeCapture))
                    NowFocus.ClearHostFocus(focusHostId);
            }

            public RenderTexture GetTarget(int width, int height)
            {
                if (target != null && target.width == width && target.height == height)
                    return target;

                ReleaseTarget();

                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Now IMGUI",
                    hideFlags = HideFlags.HideAndDontSave
                };

                target.Create();
                return target;
            }

            public void Dispose()
            {
                hostRepaintDeadlineInvalidated?.Invoke(inputProvider);
                NowOverlay.ReleaseRegistrationOwner(inputProvider);
                inputProvider.ResetState(releaseNativeCapture: false);
                NowFocus.UnregisterHost(focusHostId);
                ReleaseTarget();
                renderer.Dispose();
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

    [NowScope]
    public struct NowGUIScope : IDisposable
    {
        NowGUI.CacheEntry _entry;

        RenderTexture _target;

        NowDrawScope _drawScope;

        Rect _rect;

        Color _clearColor;

        NowInputScope _inputScope;

        NowFocusHostRegistrationScope _focusScope;

        NowFrameScope _frameScope;

        ControlIdScope _controlIdScope;

        bool _renders;

        bool _suppresses;

        bool _hasInputScope;

        bool _hasFocusScope;

        bool _hasFrameScope;

        bool _hasControlIdScope;

        int _token;

        internal static NowGUIScope Render(
            Rect rect,
            NowGUI.CacheEntry entry,
            RenderTexture target,
            NowDrawScope drawScope,
            Color clearColor,
            NowInputScope inputScope,
            NowFocusHostRegistrationScope focusScope,
            NowFrameScope frameScope,
            ControlIdScope controlIdScope)
        {
            return new NowGUIScope(
                rect,
                entry,
                target,
                drawScope,
                clearColor,
                true,
                false,
                inputScope,
                true,
                focusScope,
                true,
                frameScope,
                true,
                controlIdScope,
                true,
                NowGUI.BeginScope());
        }

        internal static NowGUIScope Suppress(Rect rect)
        {
            Now.BeginSuppressDraw();
            return new NowGUIScope(
                rect,
                null,
                null,
                default,
                Color.clear,
                false,
                true,
                token: NowGUI.BeginScope());
        }

        internal static NowGUIScope Suppress(
            Rect rect,
            NowInputScope inputScope,
            ControlIdScope controlIdScope = default)
        {
            Now.BeginSuppressDraw();
            return new NowGUIScope(
                rect,
                null,
                null,
                default,
                Color.clear,
                false,
                true,
                inputScope,
                true,
                controlIdScope: controlIdScope,
                hasControlIdScope: true,
                token: NowGUI.BeginScope());
        }

        internal static NowGUIScope Suppress(
            Rect rect,
            NowInputScope inputScope,
            NowFocusHostRegistrationScope focusScope,
            ControlIdScope controlIdScope = default)
        {
            return Suppress(
                rect,
                null,
                inputScope,
                focusScope,
                default,
                false,
                controlIdScope);
        }

        internal static NowGUIScope Suppress(
            Rect rect,
            NowGUI.CacheEntry entry,
            NowInputScope inputScope,
            NowFocusHostRegistrationScope focusScope,
            NowFrameScope frameScope,
            bool hasFrameScope,
            ControlIdScope controlIdScope)
        {
            Now.BeginSuppressDraw();
            return new NowGUIScope(
                rect,
                entry,
                null,
                default,
                Color.clear,
                false,
                true,
                inputScope,
                true,
                focusScope,
                true,
                frameScope,
                hasFrameScope,
                controlIdScope: controlIdScope,
                hasControlIdScope: true,
                token: NowGUI.BeginScope());
        }

        NowGUIScope(
            Rect rect,
            NowGUI.CacheEntry entry,
            RenderTexture target,
            NowDrawScope drawScope,
            Color clearColor,
            bool renders,
            bool suppresses = false,
            NowInputScope inputScope = default,
            bool hasInputScope = false,
            NowFocusHostRegistrationScope focusScope = default,
            bool hasFocusScope = false,
            NowFrameScope frameScope = default,
            bool hasFrameScope = false,
            ControlIdScope controlIdScope = default,
            bool hasControlIdScope = false,
            int token = 0)
        {
            _rect = rect;
            _entry = entry;
            _target = target;
            _drawScope = drawScope;
            _clearColor = clearColor;
            _inputScope = inputScope;
            _focusScope = focusScope;
            _frameScope = frameScope;
            _controlIdScope = controlIdScope;
            _renders = renders;
            _suppresses = suppresses;
            _hasInputScope = hasInputScope;
            _hasFocusScope = hasFocusScope;
            _hasFrameScope = hasFrameScope;
            _hasControlIdScope = hasControlIdScope;
            _token = token;
        }

        public Rect rect => _rect;

        public float width => _rect.width;

        public float height => _rect.height;

        public void Dispose()
        {
            if (_token == 0)
                return;

            if (!NowGUI.BeginScopeEnd(_token))
            {
                _token = 0;
                return;
            }

            int token = _token;

            try
            {
                if (_suppresses)
                {
                    try
                    {
                        DisposeControlIdScope();
                    }
                    finally
                    {
                        try
                        {
                            DisposeInputScope();
                        }
                        finally
                        {
                            try
                            {
                                DisposeFocusScope();
                            }
                            finally
                            {
                                try
                                {
                                    DisposeFrameScope();
                                }
                                finally
                                {
                                    Now.EndSuppressDraw();
                                }
                            }
                        }
                    }

                    return;
                }

                if (!_renders)
                {
                    try
                    {
                        DisposeFrameScope();
                    }
                    finally
                    {
                        try
                        {
                            DisposeControlIdScope();
                        }
                        finally
                        {
                            try
                            {
                                DisposeInputScope();
                            }
                            finally
                            {
                                DisposeFocusScope();
                            }
                        }
                    }

                    return;
                }

                try
                {
                    NowGUI.CompleteScope(_entry, _target, _rect, _drawScope, _clearColor);
                }
                finally
                {
                    try
                    {
                        DisposeFrameScope();
                    }
                    finally
                    {
                        try
                        {
                            DisposeControlIdScope();
                        }
                        finally
                        {
                            try
                            {
                                DisposeInputScope();
                            }
                            finally
                            {
                                DisposeFocusScope();
                            }
                        }
                    }
                }
            }
            finally
            {
                NowGUI.EndScope(token);
                _token = 0;
            }
        }

        void DisposeInputScope()
        {
            if (!_hasInputScope)
                return;

            _inputScope.Dispose();
            _hasInputScope = false;
        }

        void DisposeFocusScope()
        {
            if (!_hasFocusScope)
                return;

            _focusScope.Dispose();
            _hasFocusScope = false;
        }

        void DisposeFrameScope()
        {
            if (!_hasFrameScope)
                return;

            bool wantsRepaint = _frameScope.EndRepaintTracking(out float nextRepaintAt);
            _frameScope.Dispose();
            _hasFrameScope = false;
            NowGUI.hostRepaintDeadlineInvalidated?.Invoke(_entry.inputProvider);

            if (wantsRepaint)
            {
                _entry.inputProvider.RequestHostRepaint(markGUIChanged: false);
            }
            else if (!float.IsInfinity(nextRepaintAt) &&
                     !float.IsNaN(nextRepaintAt))
            {
                _entry.inputProvider.RequestHostRepaintAfter(
                    Mathf.Max(0f, nextRepaintAt - Time.realtimeSinceStartup));
            }
        }

        void DisposeControlIdScope()
        {
            if (!_hasControlIdScope)
                return;

            _controlIdScope.Dispose();
            _hasControlIdScope = false;
        }
    }

    public static class NowGUILayout
    {
        const float DefaultHeight = 120f;

        public static NowGUIScope Auto()
        {
            return Auto(DefaultHeight, Color.clear);
        }

        public static NowGUIScope Auto(float height, params GUILayoutOption[] options)
        {
            return Auto(height, Color.clear, options);
        }

        public static NowGUIScope Auto(float height, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, float.MaxValue, height, height, options);
            return NowGUI.Auto(rect, clearColor);
        }

        public static NowGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return Auto(size, Color.clear, options);
        }

        public static NowGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(size.x, size.x, size.y, size.y, options);
            return NowGUI.Auto(rect, clearColor);
        }
    }
}
