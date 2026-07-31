using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [InitializeOnLoad]
    public static class NowEditorGUI
    {
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object value)
            {
                return value != null ? RuntimeHelpers.GetHashCode(value) : 0;
            }
        }

        readonly struct ScheduledRepaint
        {
            public readonly EditorWindow window;

            public readonly double repaintAt;

            public ScheduledRepaint(EditorWindow window, double repaintAt)
            {
                this.window = window;
                this.repaintAt = repaintAt;
            }
        }

        static readonly HashSet<EditorWindow> PendingRepaints = new HashSet<EditorWindow>();

        static readonly Dictionary<object, EditorWindow> HostWindows =
            new Dictionary<object, EditorWindow>(ReferenceComparer.instance);

        static readonly Dictionary<NowIMGUIInputProvider, ScheduledRepaint> ScheduledRepaints =
            new Dictionary<NowIMGUIInputProvider, ScheduledRepaint>();

        static readonly List<object> StaleHostContexts = new List<object>(4);

        static readonly List<NowIMGUIInputProvider> DueProviders =
            new List<NowIMGUIInputProvider>(4);

        static readonly Assembly EditorAssembly = typeof(EditorWindow).Assembly;

        static readonly Type GUIViewType = EditorAssembly.GetType("UnityEditor.GUIView");

        static readonly Type HostViewType = EditorAssembly.GetType("UnityEditor.HostView");

        static readonly PropertyInfo CurrentGUIViewProperty = GUIViewType?.GetProperty(
            "current",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        static readonly PropertyInfo ActualViewProperty = HostViewType?.GetProperty(
            "actualView",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        const double RepaintInterval = 1.0 / 60.0;

        static bool _repaintFlushQueued;

        static bool _scheduledRepaintFlushQueued;

        static double _nextRepaintAt;

        static bool _lastApplicationFocused;

        static EditorWindow _lastFocusedWindow;

        static NowEditorGUI()
        {
            NowIMGUIInputProvider.repaintRequested = null;
            NowIMGUIInputProvider.hostRepaintRequested = RepaintProviderHost;
            NowIMGUIInputProvider.hostRepaintAfterRequested = ScheduleProviderHostRepaint;
            NowGUI.hostRepaintDeadlineInvalidated = CancelProviderScheduledRepaint;
            _lastApplicationFocused = EditorApplication.isFocused;
            _lastFocusedWindow = EditorWindow.focusedWindow;
            EditorApplication.update += TrackEditorFocusChanges;
            EditorApplication.focusChanged += OnApplicationFocusChanged;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        static void RepaintProviderHost(NowIMGUIInputProvider provider)
        {
            CancelProviderScheduledRepaint(provider);
            QueueRepaint(ResolveProviderWindow(provider));
        }

        static void ScheduleProviderHostRepaint(
            NowIMGUIInputProvider provider,
            float delaySeconds)
        {
            if (provider == null)
                return;

            EditorWindow window = ResolveProviderWindow(provider);

            if (!window)
                return;

            double repaintAt =
                EditorApplication.timeSinceStartup +
                Math.Max(0.0, delaySeconds);

            if (!ScheduledRepaints.TryGetValue(provider, out ScheduledRepaint existing) ||
                !ReferenceEquals(existing.window, window) ||
                repaintAt < existing.repaintAt)
            {
                ScheduledRepaints[provider] = new ScheduledRepaint(window, repaintAt);
            }

            if (_scheduledRepaintFlushQueued)
                return;

            _scheduledRepaintFlushQueued = true;
            EditorApplication.update += FlushScheduledRepaints;
        }

        static void CancelProviderScheduledRepaint(NowIMGUIInputProvider provider)
        {
            if (provider == null || !ScheduledRepaints.Remove(provider))
                return;

            StopScheduledRepaintFlushIfIdle();
        }

        static void StopScheduledRepaintFlushIfIdle()
        {
            if (ScheduledRepaints.Count != 0 || !_scheduledRepaintFlushQueued)
                return;

            EditorApplication.update -= FlushScheduledRepaints;
            _scheduledRepaintFlushQueued = false;
        }

        static EditorWindow ResolveProviderWindow(NowIMGUIInputProvider provider)
        {
            object context = provider?.hostContext;
            EditorWindow window = null;

            if (context != null)
            {
                HostWindows.TryGetValue(context, out window);

                if (!window)
                    window = ResolveEditorWindowFromGUIView(context);

                if (!window)
                    window = context as EditorWindow;
            }
            else
            {
                window = ResolveFallbackWindow();
            }

            return window;
        }

        static void QueueRepaint(EditorWindow window)
        {
            if (!window)
                return;

            PendingRepaints.Add(window);

            if (_repaintFlushQueued)
                return;

            _repaintFlushQueued = true;
            EditorApplication.update += FlushQueuedRepaints;
        }

        static object ResolveCurrentGUIView()
        {
            try
            {
                return CurrentGUIViewProperty?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        internal static EditorWindow ResolveEditorWindowFromGUIView(object guiView)
        {
            if (guiView == null ||
                HostViewType == null ||
                ActualViewProperty == null ||
                !HostViewType.IsInstanceOfType(guiView))
            {
                return null;
            }

            try
            {
                return ActualViewProperty.GetValue(guiView) as EditorWindow;
            }
            catch
            {
                return null;
            }
        }

        static EditorWindow ResolveFallbackWindow()
        {
            return EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
        }

        static void TrackHost(object context, EditorWindow window)
        {
            if (context == null || !window)
                return;

            if (HostWindows.TryGetValue(context, out EditorWindow existing) &&
                ReferenceEquals(existing, window))
            {
                return;
            }

            HostWindows[context] = window;
            RefreshHostFocusState(context, window);
        }

        static void OnApplicationFocusChanged(bool focused)
        {
            _lastApplicationFocused = focused;
            _lastFocusedWindow = EditorWindow.focusedWindow;
            RefreshHostFocusStates();
        }

        static void TrackEditorFocusChanges()
        {
            PruneStaleHosts();

            bool applicationFocused = EditorApplication.isFocused;
            EditorWindow focusedWindow = EditorWindow.focusedWindow;

            if (_lastApplicationFocused == applicationFocused &&
                ReferenceEquals(_lastFocusedWindow, focusedWindow))
            {
                return;
            }

            _lastApplicationFocused = applicationFocused;
            _lastFocusedWindow = focusedWindow;
            RefreshHostFocusStates();
        }

        static void RefreshHostFocusStates()
        {
            PruneStaleHosts();

            EditorWindow focusedWindow = EditorApplication.isFocused
                ? EditorWindow.focusedWindow
                : null;

            foreach (var pair in HostWindows)
            {
                NowGUI.NotifyContextFocus(
                    pair.Key,
                    ReferenceEquals(pair.Value, focusedWindow),
                    releaseNativeCapture: false);
            }
        }

        static void RefreshHostFocusState(object context, EditorWindow window)
        {
            bool focused =
                EditorApplication.isFocused &&
                ReferenceEquals(window, EditorWindow.focusedWindow);
            NowGUI.NotifyContextFocus(
                context,
                focused,
                releaseNativeCapture: false);
        }

        static void PruneStaleHosts()
        {
            StaleHostContexts.Clear();

            try
            {
                foreach (var pair in HostWindows)
                {
                    bool staleContext =
                        pair.Key is UnityEngine.Object unityContext &&
                        !unityContext;

                    if (staleContext || !pair.Value)
                        StaleHostContexts.Add(pair.Key);
                }

                for (int i = 0; i < StaleHostContexts.Count; ++i)
                {
                    object context = StaleHostContexts[i];

                    if (!HostWindows.TryGetValue(context, out EditorWindow window))
                        continue;

                    HostWindows.Remove(context);
                    NowGUI.DisposeContext(context);

                    if (!window)
                        CancelWindowRepaints(window);
                }
            }
            finally
            {
                StaleHostContexts.Clear();
            }
        }

        static void FlushQueuedRepaints()
        {
            double now = EditorApplication.timeSinceStartup;

            if (now < _nextRepaintAt)
                return;

            EditorApplication.update -= FlushQueuedRepaints;
            _repaintFlushQueued = false;
            _nextRepaintAt = now + RepaintInterval;

            try
            {
                foreach (var window in PendingRepaints)
                {
                    if (window)
                        window.Repaint();
                }
            }
            finally
            {
                PendingRepaints.Clear();
            }
        }

        static void FlushScheduledRepaints()
        {
            double now = EditorApplication.timeSinceStartup;
            DueProviders.Clear();

            try
            {
                foreach (var pair in ScheduledRepaints)
                {
                    if (!pair.Value.window || pair.Value.repaintAt <= now)
                        DueProviders.Add(pair.Key);
                }

                for (int i = 0; i < DueProviders.Count; ++i)
                {
                    NowIMGUIInputProvider provider = DueProviders[i];

                    if (!ScheduledRepaints.TryGetValue(
                            provider,
                            out ScheduledRepaint scheduled))
                    {
                        continue;
                    }

                    ScheduledRepaints.Remove(provider);

                    if (scheduled.window)
                        QueueRepaint(scheduled.window);
                }
            }
            finally
            {
                DueProviders.Clear();
                StopScheduledRepaintFlushIfIdle();
            }
        }

        static void CancelWindowRepaints(EditorWindow window)
        {
            PendingRepaints.Remove(window);

            if (PendingRepaints.Count == 0 && _repaintFlushQueued)
            {
                EditorApplication.update -= FlushQueuedRepaints;
                _repaintFlushQueued = false;
                _nextRepaintAt = 0.0;
            }

            DueProviders.Clear();

            try
            {
                foreach (var pair in ScheduledRepaints)
                {
                    if (ReferenceEquals(pair.Value.window, window))
                        DueProviders.Add(pair.Key);
                }

                for (int i = 0; i < DueProviders.Count; ++i)
                    ScheduledRepaints.Remove(DueProviders[i]);
            }
            finally
            {
                DueProviders.Clear();
                StopScheduledRepaintFlushIfIdle();
            }
        }

        static void CancelQueuedRepaints()
        {
            EditorApplication.update -= FlushQueuedRepaints;
            EditorApplication.update -= FlushScheduledRepaints;
            _repaintFlushQueued = false;
            _scheduledRepaintFlushQueued = false;
            _nextRepaintAt = 0.0;
            PendingRepaints.Clear();
            ScheduledRepaints.Clear();
            DueProviders.Clear();
        }

        public static NowGUIScope Auto()
        {
            return NowEditorGUILayout.Auto();
        }

        public static NowGUIScope Auto(Rect rect)
        {
            return Auto(rect, Color.clear);
        }

        public static NowGUIScope Auto(Rect rect, Color clearColor)
        {
            object context = ResolveCurrentGUIView();
            EditorWindow window = ResolveEditorWindowFromGUIView(context);

            if (!window)
                window = ResolveFallbackWindow();

            // A docked HostView is reused when switching tabs. The actual
            // EditorWindow is therefore the stable state/capture identity;
            // fall back to the native GUIView only for non-window hosts.
            context = window ? (object)window : context;

            TrackHost(context, window);
            bool hostFocused = window
                ? EditorApplication.isFocused &&
                    ReferenceEquals(window, EditorWindow.focusedWindow)
                : EditorApplication.isFocused;

            return NowGUI.AutoInContext(
                context,
                rect,
                clearColor,
                EditorGUIUtility.pixelsPerPoint,
                hostFocused);
        }

        public static NowGUIScope Auto(float height, params GUILayoutOption[] options)
        {
            return NowEditorGUILayout.Auto(height, options);
        }

        public static NowGUIScope Auto(float height, Color clearColor, params GUILayoutOption[] options)
        {
            return NowEditorGUILayout.Auto(height, clearColor, options);
        }

        public static NowGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return NowEditorGUILayout.Auto(size, options);
        }

        public static NowGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            return NowEditorGUILayout.Auto(size, clearColor, options);
        }

        public static void DisposeAll()
        {
            CancelQueuedRepaints();
            NowGUI.DisposeAll();
            HostWindows.Clear();
            StaleHostContexts.Clear();
            _lastApplicationFocused = EditorApplication.isFocused;
            _lastFocusedWindow = EditorWindow.focusedWindow;
        }
    }

    public static class NowEditorGUILayout
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
            return NowEditorGUI.Auto(rect, clearColor);
        }

        public static NowGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return Auto(size, Color.clear, options);
        }

        public static NowGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(size.x, size.x, size.y, size.y, options);
            return NowEditorGUI.Auto(rect, clearColor);
        }
    }
}
