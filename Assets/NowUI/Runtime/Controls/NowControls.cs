using System;
using System.Collections.Generic;
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

        struct InteractionRepaintState
        {
            public bool hovered;

            public bool held;

            public bool focused;
        }

        /// <summary>
        /// Hashes a call site (file + line) into a control identity. The control
        /// factories capture their caller via [CallerFilePath]/[CallerLineNumber]
        /// and pass it here, so every textual call site is automatically its own
        /// control — no explicit id needed. Loops share a site and are
        /// disambiguated by per-frame occurrence in <see cref="GetControlId(int)"/>.
        /// Custom controls get the same behavior by declaring the caller-info
        /// parameters themselves and forwarding them here.
        /// </summary>
        public static int SiteId(string file, int line)
        {
            unchecked
            {
                int hash = ((file != null ? file.GetHashCode() : 0) * 397) ^ line;
                return hash != 0 ? hash : 1;
            }
        }

        /// <summary>
        /// Derives a control id from an explicit string id within the active id
        /// scope, with the same per-frame occurrence salting as the site overload.
        /// </summary>
        public static int GetControlId(string id)
        {
            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            return Salt(NowInput.GetId(seed, id));
        }

        public static int GetControlId(NowId id, int fallbackIdentity)
        {
            return id.ResolveControlId(fallbackIdentity);
        }

        internal static int ResolveNavigationTargetId(NowId id)
        {
            if (!id.hasValue)
                return 0;

            int seed = _idStack.Count > 0 ? _idStack[^1] : 0;
            int resolved;

            if (id.isString)
            {
                resolved = NowInput.GetId(seed, id.stringValue);
            }
            else
            {
                resolved = id.intValue;

                if (seed != 0)
                {
                    unchecked
                    {
                        resolved = (seed * 397) ^ resolved;
                    }
                }

                if (resolved == 0)
                    resolved = 1;
            }

            return resolved;
        }

        /// <summary>
        /// Derives a control id from a precomputed identity hash (usually a
        /// <see cref="SiteId"/>) within the active id scope. Repeated draws of the
        /// same identity in one frame — loop iterations over a single call site —
        /// are salted by occurrence so they never share interaction state; the
        /// first occurrence keeps the stable id. Occurrence order follows draw
        /// order, so when looped controls appear, vanish, or reorder, prefer
        /// <c>SetId</c> or an <see>
        ///     <cref>IdScope</cref>
        /// </see>
        /// keyed by your data.
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
        }

        /// <summary>Starts a fresh measure-pass occurrence count; called when a passive pass begins.</summary>
        internal static void ResetPassiveControlIdOccurrences()
        {
            _passiveOccurrences.Clear();
        }

        public static void Reset()
        {
            NowTheme.Reset();
            _idStack.Clear();
            _labelOccurrences.Clear();
            _passiveOccurrences.Clear();
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
        public static NowInteraction Interact(int id, NowRect rect, out bool focused, out bool submitted)
        {
            return Interact(id, rect, default, out focused, out submitted);
        }

        /// <summary>
        /// The standard interaction bundle with explicit focus navigation targets.
        /// </summary>
        public static NowInteraction Interact(int id, NowRect rect, NowFocusNavigation navigation, out bool focused, out bool submitted)
        {
            var interaction = NowInput.Interact(id, rect);
            NowFocus.Register(id, rect, navigation);

            if (interaction.pressed)
                NowFocus.Focus(id);

            focused = NowFocus.IsFocused(id);
            submitted = NowFocus.SubmitPressed(id);

            if (!NowInput.isPassive)
            {
                ref var repaint = ref NowControlState.Get<InteractionRepaintState>(NowInput.GetId(id, "interaction"));

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

        /// <summary>Hover/press tint applied on top of a preset color.</summary>
        public static Vector4 StateTint(Vector4 color, float hoverT, bool held)
        {
            return StateTint(NowTheme.themeAsset, color, hoverT, held);
        }

        public static Vector4 StateTint(NowThemeAsset themeAsset, Vector4 color, float hoverT, bool held)
        {
            var styles = themeAsset != null ? themeAsset.controlStyles : NowControlStyleSet.Default;
            float brightness = held ? styles.pressedBrightness : Mathf.Lerp(1f, styles.hoverBrightness, hoverT);
            color.x *= brightness;
            color.y *= brightness;
            color.z *= brightness;
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
            Vector2 size = text.Measure(label);
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
            Vector2 size = text.Measure(label);
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
