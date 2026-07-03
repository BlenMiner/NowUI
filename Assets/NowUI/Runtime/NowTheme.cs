using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>Ambient theme access and scoped overrides for NowUI drawing.</summary>
    public static class NowTheme
    {
        static NowThemeAsset _defaultThemeAsset;

        static NowThemeAsset _defaultDarkThemeAsset;

        static bool? _preferDark;

        static readonly List<NowThemeAsset> _themeStack = new List<NowThemeAsset>(4);

        /// <summary>
        /// Forces light or dark resolution: when set, the active theme swaps to
        /// its <see cref="NowThemeAsset.counterpart"/> when that twin matches
        /// the requested mode. Null (the default) respects whatever theme is
        /// active. One flag switches the whole UI:
        /// <code>NowTheme.preferDark = systemDarkMode;</code>
        /// </summary>
        public static bool? preferDark
        {
            get => _preferDark;
            set => _preferDark = value;
        }

        /// <summary>
        /// The active theme: the innermost <see cref="Scope(NowThemeAsset)"/> value,
        /// or a built-in default created on first use. When <see cref="preferDark"/>
        /// is set and disagrees with the resolved theme's mode, a linked matching
        /// counterpart wins.
        /// </summary>
        public static NowThemeAsset themeAsset
        {
            get
            {
                return ResolveMode(_themeStack.Count > 0 ? _themeStack[^1] : DefaultAsset());
            }
        }

        static NowThemeAsset ResolveMode(NowThemeAsset asset)
        {
            if (_preferDark == null || asset == null || asset.isDark == _preferDark.Value)
                return asset;

            var twin = asset.counterpart;
            return twin != null && twin.isDark == _preferDark.Value ? twin : asset;
        }

        static NowThemeAsset DefaultAsset()
        {
            if (_preferDark == true)
            {
                if (_defaultDarkThemeAsset == null)
                {
                    _defaultDarkThemeAsset = ScriptableObject.CreateInstance<NowThemeAsset>();
                    _defaultDarkThemeAsset.name = "Now Default Dark Theme";
                    _defaultDarkThemeAsset.hideFlags = HideFlags.HideAndDontSave;
                    _defaultDarkThemeAsset.ResetToDefaults(dark: true);
                }

                return _defaultDarkThemeAsset;
            }

            if (_defaultThemeAsset == null)
            {
                _defaultThemeAsset = ScriptableObject.CreateInstance<NowThemeAsset>();
                _defaultThemeAsset.name = "Now Default Theme";
                _defaultThemeAsset.hideFlags = HideFlags.HideAndDontSave;
                _defaultThemeAsset.ResetToDefaults(dark: false);
            }

            return _defaultThemeAsset;
        }

        /// <summary>
        /// The innermost scoped theme, unresolved; null when no scope is active.
        /// Deferred overlay draws capture this at declare time so popups render
        /// with the theme that was ambient where they were opened, not whatever
        /// happens to be active when the overlay queue flushes.
        /// </summary>
        internal static NowThemeAsset currentScopeTheme => _themeStack.Count > 0 ? _themeStack[^1] : null;

        /// <summary>Pushes a scope when the value is non-null; otherwise a no-op scope.</summary>
        internal static ThemeScope ScopeOrDefault(NowThemeAsset value)
        {
            if (value == null)
                return default;

            _themeStack.Add(value);
            return new ThemeScope(true);
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
            _preferDark = null;
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
