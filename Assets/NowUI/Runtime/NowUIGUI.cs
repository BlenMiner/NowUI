using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public static class NowUIGUI
    {
        const int ControlHint = 0x4e6f7747;

        const double CacheLifetimeSeconds = 10.0;

        static readonly Dictionary<int, CacheEntry> _entries = new Dictionary<int, CacheEntry>();

        static readonly List<int> _removeIds = new List<int>(8);

        static double _lastCleanupTime;

        public static NowUIGUIScope Auto(Rect rect)
        {
            return Auto(rect, Color.clear);
        }

        public static NowUIGUIScope Auto(Rect rect, Color clearColor)
        {
            return Auto(rect, clearColor, 1f);
        }

        public static NowUIGUIScope Auto(Rect rect, Color clearColor, float pixelsPerPoint)
        {
            if (Event.current == null)
                return NowUIGUIScope.Suppress(rect);

            int controlId = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            var inputSurface = new NowUIInputSurface(new Vector2(rect.width, rect.height), rect);
            var inputScope = NowUIInput.Begin(NowUIIMGUIInputProvider.instance, inputSurface);

            if (Event.current.type != EventType.Repaint)
                return NowUIGUIScope.Suppress(rect, inputScope);

            if (rect.width <= 0f || rect.height <= 0f)
                return NowUIGUIScope.Suppress(rect, inputScope);

            var entry = GetEntry(controlId);
            entry.lastUsedTime = CurrentTime();

            pixelsPerPoint = Mathf.Max(1f, pixelsPerPoint);
            int pixelWidth = Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelsPerPoint));
            int pixelHeight = Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelsPerPoint));

            RenderTexture target = entry.GetTarget(pixelWidth, pixelHeight);
            return NowUIGUIScope.Render(
                rect,
                entry,
                target,
                entry.renderer.Begin(new Vector2(rect.width, rect.height)),
                clearColor,
                inputScope);
        }

        public static void DisposeAll()
        {
            foreach (var entry in _entries.Values)
                entry.Dispose();

            _entries.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            DisposeAll();
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
            double now = CurrentTime();

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

        static double CurrentTime()
        {
            return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
        }

        internal static void CompleteScope(
            CacheEntry entry,
            RenderTexture target,
            Rect rect,
            NowUIDrawScope drawScope,
            Color clearColor)
        {
            drawScope.Dispose();
            entry.renderer.Render(target, true, clearColor);
            GUI.DrawTexture(rect, target, ScaleMode.StretchToFill, true);
            CleanupUnusedEntries();
        }

        internal sealed class CacheEntry : IDisposable
        {
            public readonly NowUIRenderer renderer = new NowUIRenderer();

            public RenderTexture target;

            public double lastUsedTime;

            public RenderTexture GetTarget(int width, int height)
            {
                if (target != null && target.width == width && target.height == height)
                    return target;

                ReleaseTarget();

                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "NowUI IMGUI",
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

    public struct NowUIGUIScope : IDisposable
    {
        NowUIGUI.CacheEntry _entry;

        RenderTexture _target;

        NowUIDrawScope _drawScope;

        Rect _rect;

        Color _clearColor;

        NowUIInputScope _inputScope;

        bool _renders;

        bool _suppresses;

        bool _hasInputScope;

        bool _disposed;

        internal static NowUIGUIScope Render(
            Rect rect,
            NowUIGUI.CacheEntry entry,
            RenderTexture target,
            NowUIDrawScope drawScope,
            Color clearColor,
            NowUIInputScope inputScope)
        {
            return new NowUIGUIScope(rect, entry, target, drawScope, clearColor, true, false, inputScope, true);
        }

        internal static NowUIGUIScope Suppress(Rect rect)
        {
            Now.BeginSuppressDraw();
            return new NowUIGUIScope(rect, null, null, default, Color.clear, false, true);
        }

        internal static NowUIGUIScope Suppress(Rect rect, NowUIInputScope inputScope)
        {
            Now.BeginSuppressDraw();
            return new NowUIGUIScope(rect, null, null, default, Color.clear, false, true, inputScope, true);
        }

        NowUIGUIScope(
            Rect rect,
            NowUIGUI.CacheEntry entry,
            RenderTexture target,
            NowUIDrawScope drawScope,
            Color clearColor,
            bool renders,
            bool suppresses = false,
            NowUIInputScope inputScope = default,
            bool hasInputScope = false)
        {
            _rect = rect;
            _entry = entry;
            _target = target;
            _drawScope = drawScope;
            _clearColor = clearColor;
            _inputScope = inputScope;
            _renders = renders;
            _suppresses = suppresses;
            _hasInputScope = hasInputScope;
            _disposed = false;
        }

        public Rect rect => _rect;

        public float width => _rect.width;

        public float height => _rect.height;

        public bool isRepaint => _renders && !_disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_suppresses)
            {
                Now.EndSuppressDraw();
                DisposeInputScope();
                return;
            }

            if (!_renders)
            {
                DisposeInputScope();
                return;
            }

            NowUIGUI.CompleteScope(_entry, _target, _rect, _drawScope, _clearColor);
            DisposeInputScope();
        }

        void DisposeInputScope()
        {
            if (!_hasInputScope)
                return;

            _inputScope.Dispose();
            _hasInputScope = false;
        }
    }

    public static class NowUIGUILayout
    {
        const float DefaultHeight = 120f;

        public static NowUIGUIScope Auto()
        {
            return Auto(DefaultHeight, Color.clear);
        }

        public static NowUIGUIScope Auto(float height, params GUILayoutOption[] options)
        {
            return Auto(height, Color.clear, options);
        }

        public static NowUIGUIScope Auto(float height, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, float.MaxValue, height, height, options);
            return NowUIGUI.Auto(rect, clearColor);
        }

        public static NowUIGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return Auto(size, Color.clear, options);
        }

        public static NowUIGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(size.x, size.x, size.y, size.y, options);
            return NowUIGUI.Auto(rect, clearColor);
        }
    }
}
