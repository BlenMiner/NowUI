using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// The single clipboard hook for everything in NowUI that copies or pastes
    /// text — selection Ctrl+C, text field copy/cut/paste, markdown copy buttons
    /// and context menus. Defaults to the system clipboard; replace once for
    /// platforms with their own copy flow (WebGL, mobile toasts, tests) and
    /// every path follows.
    /// </summary>
    public static class NowClipboard
    {
        public static System.Action<string> setText = static text => GUIUtility.systemCopyBuffer = text;

        public static System.Func<string> getText = static () => GUIUtility.systemCopyBuffer;

        public static void Copy(string text)
        {
            if (!string.IsNullOrEmpty(text))
                setText?.Invoke(text);
        }

        public static string Paste()
        {
            return getText?.Invoke() ?? string.Empty;
        }
    }
}
