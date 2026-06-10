using UnityEditor;
using UnityEngine;

namespace NowUI.Editor
{
    [InitializeOnLoad]
    public static class NowUIEditorGUI
    {
        static NowUIEditorGUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        public static NowUIGUIScope Auto()
        {
            return NowUIEditorGUILayout.Auto();
        }

        public static NowUIGUIScope Auto(Rect rect)
        {
            return Auto(rect, Color.clear);
        }

        public static NowUIGUIScope Auto(Rect rect, Color clearColor)
        {
            return NowUIGUI.Auto(rect, clearColor, EditorGUIUtility.pixelsPerPoint);
        }

        public static NowUIGUIScope Auto(float height, params GUILayoutOption[] options)
        {
            return NowUIEditorGUILayout.Auto(height, options);
        }

        public static NowUIGUIScope Auto(float height, Color clearColor, params GUILayoutOption[] options)
        {
            return NowUIEditorGUILayout.Auto(height, clearColor, options);
        }

        public static NowUIGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return NowUIEditorGUILayout.Auto(size, options);
        }

        public static NowUIGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            return NowUIEditorGUILayout.Auto(size, clearColor, options);
        }

        public static void DisposeAll()
        {
            NowUIGUI.DisposeAll();
        }
    }

    public static class NowUIEditorGUILayout
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
            return NowUIEditorGUI.Auto(rect, clearColor);
        }

        public static NowUIGUIScope Auto(Vector2 size, params GUILayoutOption[] options)
        {
            return Auto(size, Color.clear, options);
        }

        public static NowUIGUIScope Auto(Vector2 size, Color clearColor, params GUILayoutOption[] options)
        {
            Rect rect = GUILayoutUtility.GetRect(size.x, size.x, size.y, size.y, options);
            return NowUIEditorGUI.Auto(rect, clearColor);
        }
    }
}
