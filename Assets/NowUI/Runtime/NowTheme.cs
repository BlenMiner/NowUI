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

        static readonly NowScopeGuard _themeScopes = new NowScopeGuard("NowTheme.Scope");

        static int _scopeStartedAt = int.MinValue;

        static int EnterScope()
        {
            if (_themeScopes.count == 0)
                _scopeStartedAt = Time.frameCount;

            return _themeScopes.Enter();
        }

        internal static bool hasActiveScopesThisFrame =>
            _themeScopes.count > 0 && _scopeStartedAt == Time.frameCount;

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
        /// <remarks>
        /// The no-scope path returns the built-in default by plain reference: the
        /// hidden defaults always match their mode, have no counterpart, and are
        /// never destroyed by NowUI, so mode resolution and the Unity lifetime
        /// check are skipped until domain reload resets the statics.
        /// </remarks>
        public static NowThemeAsset themeAsset
        {
            get
            {
                if (_themeStack.Count > 0)
                    return ResolveMode(_themeStack[^1]);

                var cached = _preferDark == true ? _defaultDarkThemeAsset : _defaultThemeAsset;
                return ReferenceEquals(cached, null) ? DefaultAsset() : cached;
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
            return new ThemeScope(EnterScope());
        }

        /// <summary>Pushes a contextual theme; dispose the scope to restore the previous one.</summary>
        public static ThemeScope Scope(NowThemeAsset value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _themeStack.Add(value);
            return new ThemeScope(EnterScope());
        }

        internal static void PopScope(int token)
        {
            if (_themeScopes.Exit(token) && _themeStack.Count > 0)
                _themeStack.RemoveAt(_themeStack.Count - 1);
        }

        static bool _warnedLeakedThemeScope;

        /// <summary>
        /// Frame-entry self-heal called by <c>Now.StartUI</c>: clears theme scopes a
        /// previous frame leaked so a forgotten Dispose cannot restyle the whole app,
        /// and reports the leak once so it is attributable.
        /// </summary>
        internal static void ResetStackForFrame()
        {
            if (_themeStack.Count == 0)
            {
                _warnedLeakedThemeScope = false;
                return;
            }

            _themeStack.Clear();
            _themeScopes.Clear();
            _scopeStartedAt = int.MinValue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_warnedLeakedThemeScope)
            {
                _warnedLeakedThemeScope = true;
                Debug.LogWarning("NowUI: a NowTheme.Scope from the previous frame was never disposed; the theme stack was reset. Wrap the scope in a using statement.");
            }
#endif
        }

        public static void Reset()
        {
            _themeStack.Clear();
            _themeScopes.Clear();
            _scopeStartedAt = int.MinValue;
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

    [NowScope]
    public struct ThemeScope : IDisposable
    {
        int _token;

        internal ThemeScope(int token)
        {
            _token = token;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowTheme.PopScope(_token);
            _token = 0;
        }
    }
}
