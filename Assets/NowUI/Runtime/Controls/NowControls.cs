using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Immediate-mode controls in NowUI's fluent style:
    /// <code>
    /// if (NowControls.Button("Save").Draw())
    ///     Save();
    ///
    /// NowControls.Checkbox("Shadows").Draw(ref shadows);
    /// NowControls.Slider(0f, 1f).Draw(ref volume);
    ///
    /// if (NowControls.Radio("High", quality == 2).Draw())
    ///     quality = 2;
    /// </code>
    /// Controls are layout-integrated (they reserve through <see cref="NowLayout"/>)
    /// unless given an explicit rect with SetPosition. Visuals come from the ambient
    /// <see cref="theme"/>; values stay owned by the caller via ref parameters.
    ///
    /// Everything controls are built from is public — <see cref="NowUIInput"/>,
    /// <see cref="NowUIFocus"/>, <see cref="NowUIControlState"/>, the layout and
    /// theme — so custom controls are first-class, not second-class.
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
                    return _themeStack[_themeStack.Count - 1];

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
        ///     NowControls.Button("Delete").Draw();
        /// </code>
        /// </summary>
        public static ControlIdScope IdScope(string name)
        {
            int seed = _idStack.Count > 0 ? _idStack[_idStack.Count - 1] : 0;
            _idStack.Add(NowUIInput.GetId(seed, name));
            return new ControlIdScope(true);
        }

        internal static void PopIdScope()
        {
            if (_idStack.Count > 0)
                _idStack.RemoveAt(_idStack.Count - 1);
        }

        /// <summary>Derives a control id from a label within the active id scope.</summary>
        public static int GetControlId(string label)
        {
            int seed = _idStack.Count > 0 ? _idStack[_idStack.Count - 1] : 0;
            return NowUIInput.GetId(seed, label);
        }

        public static void Reset()
        {
            _themeStack.Clear();
            _idStack.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }

        // ------------------------------------------------------------------
        // Factories
        // ------------------------------------------------------------------

        public static NowButton Button(string label)
        {
            return new NowButton(label);
        }

        public static NowCheckbox Checkbox(string label)
        {
            return new NowCheckbox(label);
        }

        public static NowRadio Radio(string label, bool isOn)
        {
            return new NowRadio(label, isOn);
        }

        public static NowSlider Slider(float min, float max)
        {
            return new NowSlider(min, max);
        }

        public static NowScrollView ScrollView(string id)
        {
            return new NowScrollView(id);
        }

        public static NowTextField TextField(string id)
        {
            return new NowTextField(id);
        }

        public static NowDropdown Dropdown(string id, System.Collections.Generic.IReadOnlyList<string> options)
        {
            return new NowDropdown(id, options);
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

        internal static void DrawCenteredLabel(NowUITheme activeTheme, NowRect rect, string label, string textPreset, NowRect mask)
        {
            var text = activeTheme.Text(default, textPreset);
            Vector2 size = text.Measure(label);

            text.rect = new NowRect(
                rect.x + (rect.width - size.x) * 0.5f,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + 1f,
                size.y + 1f);
            text.SetMask(mask).Draw(label);
        }

        internal static void DrawLeftLabel(NowUITheme activeTheme, NowRect rect, string label, string textPreset)
        {
            var text = activeTheme.Text(default, textPreset);
            Vector2 size = text.Measure(label);

            text.rect = new NowRect(
                rect.x,
                rect.y + (rect.height - size.y) * 0.5f,
                size.x + 1f,
                size.y + 1f);
            text.Draw(label);
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
