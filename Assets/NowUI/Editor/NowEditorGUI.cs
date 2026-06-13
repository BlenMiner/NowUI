using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [InitializeOnLoad]
    public static class NowEditorGUI
    {
        static NowEditorGUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
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
