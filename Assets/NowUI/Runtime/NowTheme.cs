using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>Ambient theme access and scoped overrides for NowUI drawing.</summary>
    public static class NowTheme
    {
        static NowThemeAsset _defaultThemeAsset;

        static readonly List<NowThemeAsset> _themeStack = new List<NowThemeAsset>(4);

        /// <summary>
        /// The active theme: the innermost <see cref="Scope(NowThemeAsset)"/> value,
        /// or a built-in default created on first use.
        /// </summary>
        public static NowThemeAsset themeAsset
        {
            get
            {
                if (_themeStack.Count > 0)
                    return _themeStack[^1];

                if (_defaultThemeAsset == null)
                {
                    _defaultThemeAsset = ScriptableObject.CreateInstance<NowThemeAsset>();
                    _defaultThemeAsset.name = "Now Default Theme";
                    _defaultThemeAsset.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultThemeAsset;
            }
        }

        /// <summary>Pushes a contextual theme; dispose the scope to restore the previous one.</summary>
        public static ThemeScope Scope(NowThemeAsset value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _themeStack.Add(value);
            return new ThemeScope(true);
        }

        internal static void PopScope()
        {
            if (_themeStack.Count > 0)
                _themeStack.RemoveAt(_themeStack.Count - 1);
        }

        public static void Reset()
        {
            _themeStack.Clear();
        }

        public static NowRect Inset(NowRect rect, Vector4 spacing)
        {
            return NowThemeAsset.Inset(rect, spacing);
        }

        public static NowRect Outset(NowRect rect, Vector4 spacing)
        {
            return NowThemeAsset.Outset(rect, spacing);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    public struct ThemeScope : IDisposable
    {
        bool _active;

        internal ThemeScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            NowTheme.PopScope();
        }
    }
}
