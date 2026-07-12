using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// The control toolkit: id scopes and the shared interaction plumbing that
    /// both the built-in controls and custom controls run on.
    ///
    /// The controls themselves live where they belong:
    /// <see cref="NowLayout"/> for layout-flowing controls
    /// (<c>NowLayout.Button("Save").Draw()</c>) and <see cref="Now"/> for explicit
    /// rects (<c>Now.Button(rect, "Save").Draw()</c>) — mirroring how
    /// <c>NowLayout.Label</c> and <c>Now.Text</c> already split.
    ///
    /// Control API conventions (custom controls should follow them too):
    /// action controls return <c>bool</c> from <c>Draw()</c> — true on click or
    /// submit-while-focused; value controls take <c>Draw(ref value)</c>, mutate
    /// the caller-owned ref and return true when it changed this frame.
    ///
    /// Everything here is public so custom controls are first-class, not
    /// second-class.
    /// </summary>
    public static class NowControls
    {
        static readonly List<int> _idStack = new List<int>(8);

        /// <summary>The active theme, provided by <see cref="NowTheme"/>.</summary>
        public static NowThemeAsset themeAsset => NowTheme.themeAsset;

        /// <summary>Pushes a contextual theme; dispose the scope to restore the previous one.</summary>
        public static ThemeScope Theme(NowThemeAsset value)
        {
            return NowTheme.Scope(value);
        }

        internal static NowText Text(NowThemeAsset activeThemeAsset, NowTextStyle textStyle)
        {
            return activeThemeAsset.Text(default, textStyle);
        }

        /// <summary>
        /// Disambiguates controls with the same label drawn in loops or repeated
        /// panels: ids derive from the label hashed against the innermost scope.
        /// <code>
        /// using (NowControls.IdScope($"row-{i}"))
        ///     NowLayout.Button("Delete").Draw();
        /// </code>
        /// </summary>
        public static ControlIdScope IdScope(string name)
        {
            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            _idStack.Add(NowInput.GetId(seed, name));
            return new ControlIdScope(true);
        }

        /// <summary>
        /// Disambiguates repeated panels using an existing <see cref="NowId"/>;
        /// a default (empty) id leaves the scope stack untouched.
        /// </summary>
        public static ControlIdScope IdScope(NowId id)
        {
            if (!id.hasValue)
                return new ControlIdScope(false);

            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            int resolved = id.ResolveStableId(1);

            if (seed != 0)
                resolved = NowInput.CombineId(seed, resolved);

            _idStack.Add(resolved != 0 ? resolved : 1);
            return new ControlIdScope(true);
        }

        /// <summary>
        /// Disambiguates repeated panels or hosts using an existing stable integer
        /// id, such as a component instance id, without allocating a string.
        /// </summary>
        public static ControlIdScope IdScope(int id)
        {
            if (id == 0)
                throw new ArgumentException("Control id 0 is reserved.", nameof(id));

            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;

            if (seed != 0)
            {
                unchecked
                {
                    id = (seed * 397) ^ id;
                }
            }

            _idStack.Add(id != 0 ? id : 1);
            return new ControlIdScope(true);
        }

        internal static void PopIdScope()
        {
            if (_idStack.Count > 0)
                _idStack.RemoveAt(_idStack.Count - 1);
        }

        static readonly Dictionary<int, int> _labelOccurrences = new Dictionary<int, int>(32);

        static readonly Dictionary<int, int> _passiveOccurrences = new Dictionary<int, int>(32);

        const int InteractionRepaintSeed = 0x4e435249;

        struct InteractionRepaintState
        {
            public bool hovered;

            public bool held;

            public bool focused;
        }

        sealed class ReferenceStringComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(string value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        const int SiteFileHashCacheLimit = 4096;

        static readonly Dictionary<string, int> _siteFileHashes =
            new Dictionary<string, int>(64, new ReferenceStringComparer());

        /// <summary>
        /// Hashes a call site (file + line) into a control identity. The control
        /// factories capture their caller via [CallerFilePath]/[CallerLineNumber]
        /// and pass it here, so every textual call site is automatically its own
        /// control — no explicit id needed. Loops share a site and are
        /// disambiguated by per-frame occurrence in <see cref="GetControlId(int)"/>.
        /// Custom controls get the same behavior by declaring the caller-info
        /// parameters themselves and forwarding them here.
        /// The path's content hash is memoized by reference: caller-info paths
        /// are compiler-interned constants, so each call site pays the
        /// O(path-length) walk once. Non-interned strings still hash correctly
        /// (the cached value is the content hash); the cache is size-capped so
        /// dynamic paths cannot grow it without bound.
        /// </summary>
        public static int SiteId(string file, int line)
        {
            unchecked
            {
                int fileHash;

                if (file == null)
                {
                    fileHash = 0;
                }
                else if (!_siteFileHashes.TryGetValue(file, out fileHash))
                {
                    fileHash = file.GetHashCode();

                    if (_siteFileHashes.Count < SiteFileHashCacheLimit)
                        _siteFileHashes.Add(file, fileHash);
                }

                int hash = (fileHash * 397) ^ line;
                return hash != 0 ? hash : 1;
            }
        }

        /// <summary>
        /// Derives a control id from an explicit string id within the active id
        /// scope. Explicit ids are stable — never occurrence-salted — so the same
        /// name resolves to the same control from anywhere under the same scope;
        /// two controls sharing one explicit id in one frame share state, which
        /// is the caller's bug, not something to silently disambiguate.
        /// </summary>
        public static int GetControlId(string id)
        {
            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            return NowInput.GetId(seed, id);
        }

        /// <summary>
        /// Resolves an optional explicit id to a control id, falling back to the
        /// captured call-site identity when the id is default. Explicit ids are
        /// stable (integers verbatim, strings scoped — see <see cref="NowId"/>);
        /// only the call-site fallback is occurrence-salted.
        /// </summary>
        public static int GetControlId(NowId id, int fallbackIdentity)
        {
            return id.ResolveControlId(fallbackIdentity);
        }

        internal static int ResolveNavigationTargetId(NowId id)
        {
            if (!id.hasValue)
                return 0;

            // Must mirror NowId.ResolveControlId exactly, or navigation links
            // point at ids no control ever registers: int ids verbatim,
            // string ids seeded by the active scope.
            if (id.isString)
            {
                int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
                return NowInput.GetId(seed, id.stringValue);
            }

            return id.intValue;
        }

        /// <summary>
        /// Derives a control id from a call-site identity hash (a
        /// <see cref="SiteId"/>) within the active id scope. Repeated draws of the
        /// same identity in one frame — loop iterations over a single call site —
        /// are salted by occurrence so they never share interaction state; the
        /// first occurrence keeps the stable id. Occurrence order follows draw
        /// order, so when looped controls appear, vanish, or reorder, prefer
        /// <c>SetId</c> or an <see>
        ///     <cref>IdScope</cref>
        /// </see>
        /// keyed by your data — explicit ids are never salted.
        /// </summary>
        public static int GetControlId(int identity)
        {
            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            int id = identity;

            if (seed != 0)
            {
                unchecked
                {
                    id = (seed * 397) ^ identity;
                }
            }

            if (id == 0)
                id = 1;

            return Salt(id);
        }

        static int Salt(int id)
        {
            // Measure passes draw the same controls again, so they count in their
            // own table (cleared each time a pass begins): occurrence N during the
            // pass resolves to the same id as occurrence N in the real pass.
            var occurrences = NowInput.isPassive ? _passiveOccurrences : _labelOccurrences;

            if (occurrences.TryGetValue(id, out int occurrence))
            {
                occurrences[id] = occurrence + 1;

                unchecked
                {
                    int salted = (id * 397) ^ (occurrence * -1521134295);
                    return salted != 0 ? salted : 1;
                }
            }

            occurrences[id] = 1;
            return id;
        }

        /// <summary>Starts a fresh occurrence count; called when an input surface begins.</summary>
        internal static void ResetControlIdOccurrences()
        {
            _labelOccurrences.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _interactedIds.Clear();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly HashSet<int> _interactedIds = new HashSet<int>(64);

        static readonly HashSet<int> _duplicateWarnedIds = new HashSet<int>(8);
#endif

        /// <summary>
        /// Editor/development-build check: warns when two controls resolve to the
        /// same id in one pass. Call-site identity can't collide (occurrence
        /// salting), so a duplicate means an explicit id was reused — the
        /// controls silently share focus, state and interaction, which is
        /// almost never intended. Off in release builds; disable via
        /// <see cref="warnOnDuplicateControlIds"/> if a custom control draws
        /// one identity twice on purpose.
        /// </summary>
        public static bool warnOnDuplicateControlIds = true;

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void CheckDuplicateControlId(int id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnOnDuplicateControlIds || NowInput.isPassive)
                return;

            if (_interactedIds.Add(id) || !_duplicateWarnedIds.Add(id))
                return;

            Debug.LogWarning(
                $"NowUI: two controls resolved to the same id (0x{id:X8}) in one pass — they share focus, state and " +
                "interaction. An explicit id is being reused: give each control its own identity with SetId keyed by " +
                "your data, wrap repeated panels in NowControls.IdScope, or mint sub-region ids with " +
                "NowInput.CombineId(parentId, seed). (Warned once per id; NowControls.warnOnDuplicateControlIds " +
                "disables this check.)");
#endif
        }

        /// <summary>Starts a fresh measure-pass occurrence count; called when a passive pass begins.</summary>
        internal static void ResetPassiveControlIdOccurrences()
        {
            _passiveOccurrences.Clear();
        }

        /// <summary>Clears id scopes, occurrence tables and theme overrides (tests/domain reloads).</summary>
        public static void Reset()
        {
            NowTheme.Reset();
            _idStack.Clear();
            _labelOccurrences.Clear();
            _passiveOccurrences.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _interactedIds.Clear();
            _duplicateWarnedIds.Clear();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        /// <summary>
        /// The standard interaction bundle for a control: pointer interaction, focus
        /// registration, click-to-focus, and submit activation — the same sequence
        /// every built-in control runs first.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(SiteId(file, line)), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with call-site identity and explicit
        /// focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(SiteId(file, line)), rect, navigation, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with optional explicit identity. When
        /// <paramref name="id"/> is default, identity falls back to the call site.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowRect rect,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(id, SiteId(file, line)), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with optional explicit identity and
        /// explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Interact(GetControlId(id, SiteId(file, line)), rect, navigation, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle for builders that already captured a
        /// fallback call-site identity in their factory.
        /// </summary>
        public static NowInteraction Interact(NowId id, int fallbackIdentity, NowRect rect, out bool focused, out bool submitted)
        {
            return Interact(GetControlId(id, fallbackIdentity), rect, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle for builders with a captured fallback
        /// identity and explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(
            NowId id,
            int fallbackIdentity,
            NowRect rect,
            NowFocusNavigation navigation,
            out bool focused,
            out bool submitted)
        {
            return Interact(GetControlId(id, fallbackIdentity), rect, navigation, out focused, out submitted);
        }

        public static NowInteraction Interact(int id, NowRect rect, out bool focused, out bool submitted)
        {
            return Interact(id, rect, default, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(int id, NowRect rect, NowFocusNavigation navigation, out bool focused, out bool submitted)
        {
            CheckDuplicateControlId(id);
            var interaction = NowInput.Interact(id, rect);
            NowFocus.Register(id, rect, navigation);

            if (interaction.pressed)
                NowFocus.Focus(id);

            focused = NowFocus.IsFocused(id);
            submitted = NowFocus.SubmitPressed(id);

            if (!NowInput.isPassive)
            {
                ref var repaint = ref NowControlState.Get<InteractionRepaintState>(NowInput.CombineId(id, InteractionRepaintSeed));

                if (repaint.hovered != interaction.hovered ||
                    repaint.held != interaction.held ||
                    repaint.focused != focused)
                {
                    NowControlState.RequestRepaint();
                }

                repaint.hovered = interaction.hovered;
                repaint.held = interaction.held;
                repaint.focused = focused;
            }

            return interaction;
        }

        /// <summary>
        /// Hover/press state applied on top of a resolved color: mixes toward
        /// white on dark colors and toward black on light ones, so feedback stays
        /// visible in both light and dark themes. Amounts come from the theme's
        /// hover/pressed state opacities.
        /// </summary>
        public static Vector4 StateColor(Vector4 color, float hoverT, bool held)
        {
            return StateColor(NowTheme.themeAsset, color, hoverT, held);
        }

        public static Vector4 StateColor(NowThemeAsset themeAsset, Vector4 color, float hoverT, bool held)
        {
            var styles = themeAsset != null ? themeAsset.controlStyles : NowControlStyleSet.Default;
            float amount = held ? styles.pressedStateOpacity : styles.hoverStateOpacity * Mathf.Clamp01(hoverT);

            if (amount <= 0f)
                return color;

            float luminance = color.x * 0.2126f + color.y * 0.7152f + color.z * 0.0722f;
            float overlay = luminance < 0.5f ? 1f : 0f;
            color.x = Mathf.LerpUnclamped(color.x, overlay, amount);
            color.y = Mathf.LerpUnclamped(color.y, overlay, amount);
            color.z = Mathf.LerpUnclamped(color.z, overlay, amount);
            return color;
        }

        internal static NowRect ReserveRect(bool hasRect, NowRect rect, NowLayoutOptions options, Vector2 contentSize)
        {
            if (hasRect)
                return rect;

            if (!options.Has(NowLayoutOptions.Field.Width) && !options.Has(NowLayoutOptions.Field.StretchWidth))
                options = options.SetWidth(contentSize.x);

            if (!options.Has(NowLayoutOptions.Field.Height) && !options.Has(NowLayoutOptions.Field.StretchHeight))
                options = options.SetHeight(contentSize.y);

            return NowLayout.Rect(options);
        }

        static string _labelMeasureText;
        static NowFontAsset _labelMeasureFont;
        static float _labelMeasureFontSize;
        static NowFontStyle _labelMeasureStyle;
        static Vector2 _labelMeasureSize;

        /// <summary>
        /// One-entry measure memo for the label helpers: controls measure their
        /// label for sizing and again for centering in the same draw, so the
        /// second call is a repeat of the first for free.
        /// </summary>
        static Vector2 MeasureLabel(in NowText text, string label)
        {
            if (label != null &&
                ReferenceEquals(_labelMeasureText, label) &&
                ReferenceEquals(_labelMeasureFont, text.font) &&
                _labelMeasureFontSize == text.fontSize &&
                _labelMeasureStyle == text.fontStyle)
            {
                return _labelMeasureSize;
            }

            Vector2 size = text.Measure(label);
            _labelMeasureText = label;
            _labelMeasureFont = text.font;
            _labelMeasureFontSize = text.fontSize;
            _labelMeasureStyle = text.fontStyle;
            _labelMeasureSize = size;
            return size;
        }

        internal static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask)
        {
            DrawCenteredLabel(activeThemeAsset, rect, label, textStyle, mask, default, false);
        }

        internal static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask, Color color)
        {
            DrawCenteredLabel(activeThemeAsset, rect, label, textStyle, mask, color, true);
        }

        static void DrawCenteredLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, NowRect mask, Color color, bool overrideColor)
        {
            var text = Text(activeThemeAsset, textStyle);
            Vector2 size = MeasureLabel(in text, label);
            float pad = 1f;

            text.rect = new NowRect(
                rect.x + (rect.width - size.x) * 0.5f,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + pad,
                size.y + pad);

            if (overrideColor)
                text = text.SetColor(color);

            text.SetMask(mask).Draw(label);
        }

        /// <summary>
        /// Draws a vertically centered, left-aligned label. The style is built from a
        /// default rect whose zero mask would clip everything, so the mask is reset to
        /// the given area (slightly outset so descenders survive; long values get cut).
        /// </summary>
        internal static void DrawLeftLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle)
        {
            DrawLeftLabel(activeThemeAsset, rect, label, textStyle, default, false);
        }

        internal static void DrawLeftLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, Color color)
        {
            DrawLeftLabel(activeThemeAsset, rect, label, textStyle, color, true);
        }

        static void DrawLeftLabel(NowThemeAsset activeThemeAsset, NowRect rect, string label, NowTextStyle textStyle, Color color, bool overrideColor)
        {
            var text = Text(activeThemeAsset, textStyle);
            Vector2 size = MeasureLabel(in text, label);
            float pad = 1f;

            text.rect = new NowRect(
                rect.x,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + pad,
                size.y + pad);

            if (overrideColor)
                text = text.SetColor(color);

            text.SetMask(rect.Outset(0f, 4f)).Draw(label);
        }
    }

    public struct ControlIdScope : IDisposable
    {
        bool _active;

        internal ControlIdScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            NowControls.PopIdScope();
        }
    }
}
