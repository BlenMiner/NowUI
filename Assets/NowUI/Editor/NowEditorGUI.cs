using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [InitializeOnLoad]
    public static class NowEditorGUI
    {
        static readonly HashSet<EditorWindow> PendingRepaints = new HashSet<EditorWindow>();

        const double RepaintInterval = 1.0 / 60.0;

        static bool _repaintFlushQueued;

        static double _nextRepaintAt;

        static NowEditorGUI()
        {
            NowIMGUIInputProvider.repaintRequested = RepaintCurrentWindow;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        static void RepaintCurrentWindow()
        {
            var window = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;

            if (!window)
                return;

            PendingRepaints.Add(window);

            if (_repaintFlushQueued)
                return;

            _repaintFlushQueued = true;
            EditorApplication.update += FlushQueuedRepaints;
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

        static void CancelQueuedRepaints()
        {
            EditorApplication.update -= FlushQueuedRepaints;
            _repaintFlushQueued = false;
            _nextRepaintAt = 0.0;
            PendingRepaints.Clear();
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
            return NowGUI.Auto(rect, clearColor, EditorGUIUtility.pixelsPerPoint);
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
