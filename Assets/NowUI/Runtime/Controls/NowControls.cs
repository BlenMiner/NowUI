using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// The control toolkit: ambient theme, id scopes, and the shared interaction
    /// plumbing that both the built-in controls and custom controls run on.
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
        static NowUITheme _defaultTheme;

        static readonly List<NowUITheme> _themeStack = new List<NowUITheme>(4);

        static readonly List<int> _idStack = new List<int>(8);

        /// <summary>
        /// The active theme: the innermost <see cref="Theme(NowUITheme)"/> scope, or
        /// a built-in default created on first use.
        /// </summary>
        public static NowUITheme theme
        {
            get
            {
                if (_themeStack.Count > 0)
                    return _themeStack[^1];

                if (_defaultTheme == null)
                {
                    _defaultTheme = ScriptableObject.CreateInstance<NowUITheme>();
                    _defaultTheme.name = "NowUI Default Theme";
                    _defaultTheme.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultTheme;
            }
        }

        /// <summary>Pushes a contextual theme; dispose the scope to restore the previous one.</summary>
        public static ThemeScope Theme(NowUITheme value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _themeStack.Add(value);
            return new ThemeScope(true);
        }

        internal static void PopTheme()
        {
            if (_themeStack.Count > 0)
                _themeStack.RemoveAt(_themeStack.Count - 1);
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
            _idStack.Add(NowUIInput.GetId(seed, name));
            return new ControlIdScope(true);
        }

        internal static void PopIdScope()
        {
            if (_idStack.Count > 0)
                _idStack.RemoveAt(_idStack.Count - 1);
        }

        static readonly Dictionary<int, int> _labelOccurrences = new Dictionary<int, int>(32);

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
            return Salt(NowUIInput.GetId(seed, id));
        }

        /// <summary>
        /// Derives a control id from a precomputed identity hash (usually a
        /// <see cref="SiteId"/>) within the active id scope. Repeated draws of the
        /// same identity in one frame — loop iterations over a single call site —
        /// are salted by occurrence so they never share interaction state; the
        /// first occurrence keeps the stable id. Occurrence order follows draw
        /// order, so when looped controls appear, vanish, or reorder, prefer
        /// <c>SetId</c> or an <see cref="IdScope"/> keyed by your data.
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
            // Measure passes draw the same controls again with interactions inert;
            // counting them would desync ids between the passes.
            if (NowUIInput.isPassive)
                return id;

            if (_labelOccurrences.TryGetValue(id, out int occurrence))
            {
                _labelOccurrences[id] = occurrence + 1;

                unchecked
                {
                    int salted = (id * 397) ^ (occurrence * -1521134295);
                    return salted != 0 ? salted : 1;
                }
            }

            _labelOccurrences[id] = 1;
            return id;
        }

        /// <summary>Starts a fresh occurrence count; called when an input surface begins.</summary>
        internal static void ResetControlIdOccurrences()
        {
            _labelOccurrences.Clear();
        }

        public static void Reset()
        {
            _themeStack.Clear();
            _idStack.Clear();
            _labelOccurrences.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        // ------------------------------------------------------------------
        // Shared control plumbing (public: custom controls use the same calls)
        // ------------------------------------------------------------------

        /// <summary>
        /// The standard interaction bundle for a control: pointer interaction, focus
        /// registration, click-to-focus, and submit activation — the same sequence
        /// every built-in control runs first.
        /// </summary>
        public static NowUIInteraction Interact(int id, NowRect rect, out bool focused, out bool submitted)
        {
            var interaction = NowUIInput.Interact(id, rect);
            NowUIFocus.Register(id, rect);

            if (interaction.pressed)
                NowUIFocus.Focus(id);

            focused = NowUIFocus.IsFocused(id);
            submitted = NowUIFocus.SubmitPressed(id);

            if (interaction.hovered || interaction.held || focused)
                NowUIControlState.RequestRepaint();

            return interaction;
        }

        /// <summary>Hover/press tint applied on top of a preset color.</summary>
        public static Vector4 StateTint(Vector4 color, float hoverT, bool held)
        {
            float brightness = held ? 0.86f : Mathf.Lerp(1f, 1.10f, hoverT);
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

        internal static void DrawCenteredLabel(NowUITheme activeTheme, NowRect rect, string label, NowTextStyle textStyle, NowRect mask)
        {
            var text = activeTheme.Text(default, textStyle);
            Vector2 size = text.Measure(label);

            text.rect = new NowRect(
                rect.x + (rect.width - size.x) * 0.5f,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + 1f,
                size.y + 1f);
            text.SetMask(mask).Draw(label);
        }

        internal static void DrawLeftLabel(NowUITheme activeTheme, NowRect rect, string label, NowTextStyle textStyle)
        {
            var text = activeTheme.Text(default, textStyle);
            Vector2 size = text.Measure(label);

            text.rect = new NowRect(
                rect.x,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + 1f,
                size.y + 1f);

            // The style was built from a default rect, which leaves the mask as a
            // zero rect — and a zero mask clips everything. Clip to the area we
            // were given instead (long values get cut, descenders survive).
            text.SetMask(rect.Outset(0f, 4f)).Draw(label);
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
            NowControls.PopTheme();
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
