using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public static class NowGUI
    {
        const int ControlHint = 0x4e6f7747;

        const double CacheLifetimeSeconds = 10.0;

        static readonly Dictionary<int, CacheEntry> _entries = new Dictionary<int, CacheEntry>();

        static readonly List<int> _removeIds = new List<int>(8);

        static readonly NowScopeGuard _scopes = new NowScopeGuard("NowGUI.Auto", 8);

        static int _scopeFrame = -1;

        static double _lastCleanupTime;

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
            return AutoForEvent(
                controlId,
                rect,
                clearColor,
                pixelsPerPoint,
                Event.current.type == EventType.Repaint);
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
            var inputSurface = new NowInputSurface(new Vector2(rect.width, rect.height), rect);
            var inputScope = NowInput.Begin(NowIMGUIInputProvider.instance, inputSurface);
            bool ownsInputScope = true;
            ControlIdScope controlIdScope = default;
            bool ownsControlIdScope = false;
            NowFrameScope frameScope = default;
            bool ownsFrameScope = false;
            NowDrawScope drawScope = default;
            bool ownsDrawScope = false;

            try
            {
                var entry = GetEntry(controlId);
                entry.lastUsedTime = NowTime.realtimeSinceStartup;
                controlIdScope = NowControls.RestoreIdScope(entry.scopeId);
                ownsControlIdScope = true;

                if (!repaint)
                {
                    var suppressed = NowGUIScope.Suppress(rect, inputScope, controlIdScope);
                    ownsControlIdScope = false;
                    ownsInputScope = false;
                    return suppressed;
                }

                if (rect.width <= 0f || rect.height <= 0f)
                {
                    var suppressed = NowGUIScope.Suppress(rect, inputScope, controlIdScope);
                    ownsControlIdScope = false;
                    ownsInputScope = false;
                    return suppressed;
                }

                pixelsPerPoint = Mathf.Max(1f, pixelsPerPoint);
                int pixelWidth = Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelsPerPoint));
                int pixelHeight = Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelsPerPoint));

                RenderTexture target = entry.GetTarget(pixelWidth, pixelHeight);
                frameScope = NowFrame.Begin(pixelsPerPoint);
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
                    frameScope,
                    controlIdScope);

                ownsDrawScope = false;
                ownsFrameScope = false;
                ownsControlIdScope = false;
                ownsInputScope = false;
                return rendered;
            }
            catch
            {
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
                            if (ownsInputScope)
                                inputScope.Dispose();
                        }
                    }
                }

                throw;
            }
        }

        public static void DisposeAll()
        {
            foreach (var entry in _entries.Values)
                entry.Dispose();

            _entries.Clear();
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

        static CacheEntry GetEntry(int controlId)
        {
            if (_entries.TryGetValue(controlId, out var entry))
                return entry;

            entry = new CacheEntry();
            _entries.Add(controlId, entry);
            return entry;
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
                if (now - kvp.Value.lastUsedTime > CacheLifetimeSeconds)
                    _removeIds.Add(kvp.Key);
            }

            for (int i = 0; i < _removeIds.Count; ++i)
            {
                var id = _removeIds[i];
                _entries[id].Dispose();
                _entries.Remove(id);
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
            GUI.DrawTexture(rect, target, ScaleMode.StretchToFill, true);
            CleanupUnusedEntries();
        }

        internal sealed class CacheEntry : IDisposable
        {
            public readonly NowRenderer renderer = new NowRenderer();

            public readonly int scopeId = NowControls.AllocateHostScopeId();

            public RenderTexture target;

            public double lastUsedTime;

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

        NowFrameScope _frameScope;

        ControlIdScope _controlIdScope;

        bool _renders;

        bool _suppresses;

        bool _hasInputScope;

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
            _frameScope = frameScope;
            _controlIdScope = controlIdScope;
            _renders = renders;
            _suppresses = suppresses;
            _hasInputScope = hasInputScope;
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
                            Now.EndSuppressDraw();
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
                            DisposeInputScope();
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
                            DisposeInputScope();
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

        void DisposeFrameScope()
        {
            if (!_hasFrameScope)
                return;

            _frameScope.Dispose();
            _hasFrameScope = false;
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
