using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NowUI
{
    /// <summary>
    /// Game-settings key binder: the field shows the bound key, click (or
    /// focus + Enter) to start capturing, and the next key pressed becomes the
    /// binding. Escape cancels the capture, Delete/Backspace clears the binding
    /// (unless disabled via <see cref="SetAllowClear"/>), and any pointer press
    /// outside the field cancels.
    /// </summary>
    [NowBuilder]
    public struct NowKeyBindingField
    {
        NowId _id;
        readonly int _site;
        readonly NowRect _rect;
        readonly bool _hasRect;
        NowLayoutOptions _options;
        NowFocusNavigation _navigation;
        bool _disallowClear;
        string _listeningLabel;
        string _unboundLabel;

        struct ListenState
        {
            public byte listening;
            public int armFrame;
        }

        internal NowKeyBindingField(NowId id, int site)
        {
            _id = id;
            _site = site;
            _rect = default;
            _hasRect = false;
            _options = default;
            _navigation = default;
            _disallowClear = false;
            _listeningLabel = null;
            _unboundLabel = null;
        }

        internal NowKeyBindingField(NowRect rect, NowId id, int site) : this(id, site)
        {
            _rect = rect;
            _hasRect = true;
        }

        public NowKeyBindingField SetOptions(NowLayoutOptions options) { _options = options; return this; }

        public NowKeyBindingField SetWidth(float width) { _options = _options.SetWidth(width); return this; }

        public NowKeyBindingField SetStretchWidth(float weight = 1f) { _options = _options.SetStretchWidth(weight); return this; }

        public NowKeyBindingField SetId(NowId id) { _id = id; return this; }

        public NowKeyBindingField SetNavigation(NowFocusNavigation navigation) { _navigation = navigation; return this; }

        /// <summary>Delete/Backspace while capturing clears the binding to <see cref="Key.None"/> (default on).</summary>
        public NowKeyBindingField SetAllowClear(bool allowClear) { _disallowClear = !allowClear; return this; }

        /// <summary>Text shown while capturing; defaults to "Press a key...".</summary>
        public NowKeyBindingField SetListeningLabel(string label) { _listeningLabel = label; return this; }

        /// <summary>Text shown for <see cref="Key.None"/>; defaults to "None".</summary>
        public NowKeyBindingField SetUnboundLabel(string label) { _unboundLabel = label; return this; }

        public bool Draw(ref Key value)
        {
            var theme = NowTheme.themeAsset;
            int id = NowControls.GetControlId(_id, _site);
            var rect = NowControls.ReserveRect(_hasRect, _rect, _options, new Vector2(120f, 30f));
            var interaction = NowControls.Interact(id, rect, _navigation, out bool focused, out bool submitted);
            ref var state = ref NowControlState.Get<ListenState>(id);
            bool changed = false;

            if (!NowInput.isPassive)
            {
                if (state.listening != 0)
                    changed = Capture(interaction, focused, ref state, ref value);
                else if (interaction.clicked || submitted)
                    BeginListening(id, ref state);
            }

            bool listening = state.listening != 0;
            theme.controlRenderer.DrawTextInputFrame(new NowControlFrameRenderContext(theme, rect, focused || listening));

            if (listening)
                NowControls.DrawCenteredLabel(theme, rect, _listeningLabel ?? "Press a key...", NowTextStyle.Muted, rect);
            else if (value == Key.None)
                NowControls.DrawCenteredLabel(theme, rect, _unboundLabel ?? "None", NowTextStyle.Muted, rect);
            else
                NowControls.DrawCenteredLabel(theme, rect, NowKeyNames.GetName(value), NowTextStyle.Body, rect);

            return changed;
        }

        /// <summary>
        /// While capturing, the field claims cancel presses every frame: the
        /// Escape that cancels a capture must not also close the enclosing
        /// popup or clear keyboard focus through the shared cancel action.
        /// </summary>
        bool Capture(in NowInteraction interaction, bool focused, ref ListenState state, ref Key value)
        {
            NowControlState.RequestRepaint();
            NowFocus.LockNavigation();
            NowInput.ConsumeCancel();
            var snapshot = NowInput.current;

            if (!focused ||
                interaction.clicked ||
                (snapshot.anyPointerPressed && !interaction.hovered))
            {
                state.listening = 0;
                return false;
            }

            if (snapshot.frame == state.armFrame)
                return false;

            var key = NowKeyInput.current.pressedKey;

            if (key == Key.None)
                return false;

            state.listening = 0;

            if (key == Key.Escape)
                return false;

            if (!_disallowClear && (key == Key.Delete || key == Key.Backspace))
            {
                bool cleared = value != Key.None;
                value = Key.None;
                return cleared;
            }

            bool changed = value != key;
            value = key;
            return changed;
        }

        static void BeginListening(int id, ref ListenState state)
        {
            state.listening = 1;
            state.armFrame = NowInput.current.frame;
            NowFocus.Focus(id);
            NowControlState.RequestRepaint();
        }
    }

    /// <summary>
    /// Human-readable names for <see cref="Key"/> values, layout-aware when a
    /// keyboard is present (the key labeled A on QWERTY reads Q on AZERTY) with
    /// a formatted enum-name fallback for headless runs.
    /// </summary>
    public static class NowKeyNames
    {
        struct CachedName
        {
            public string raw;
            public string name;
        }

        static readonly Dictionary<Key, CachedName> _displayNames = new Dictionary<Key, CachedName>(32);
        static readonly Dictionary<Key, string> _fallbackNames = new Dictionary<Key, string>(32);
        static readonly StringBuilder _builder = new StringBuilder(24);

        public static string GetName(Key key)
        {
            if (key == Key.None)
                return "None";

            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                var keys = keyboard.allKeys;
                int index = (int)key - 1;

                if (index >= 0 && index < keys.Count)
                {
                    var control = keys[index];

                    if (control != null && control.keyCode == key && !string.IsNullOrWhiteSpace(control.displayName))
                        return FormatDisplayName(key, control.displayName);
                }
            }

            return FallbackName(key);
        }

        static string FormatDisplayName(Key key, string display)
        {
            if (_displayNames.TryGetValue(key, out var cached) && cached.raw == display)
                return cached.name;

            string name = display.Length == 1 ? char.ToUpperInvariant(display[0]).ToString() : display;
            _displayNames[key] = new CachedName { raw = display, name = name };
            return name;
        }

        static string FallbackName(Key key)
        {
            if (_fallbackNames.TryGetValue(key, out string cached))
                return cached;

            string name = key.ToString();

            if (name.StartsWith("Digit", StringComparison.Ordinal) && name.Length == 6)
                name = name.Substring(5);
            else if (name.StartsWith("Numpad", StringComparison.Ordinal))
                name = "Num " + SpacePascalCase(name.Substring(6));
            else if (!name.StartsWith("OEM", StringComparison.Ordinal))
                name = SpacePascalCase(name);

            _fallbackNames[key] = name;
            return name;
        }

        static string SpacePascalCase(string value)
        {
            _builder.Clear();

            for (int i = 0; i < value.Length; ++i)
            {
                if (i > 0 && char.IsUpper(value[i]) && char.IsLower(value[i - 1]))
                    _builder.Append(' ');

                _builder.Append(value[i]);
            }

            return _builder.ToString();
        }
    }

    public static partial class Now
    {
        public static NowKeyBindingField KeyBindingField(NowRect rect, NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowKeyBindingField(rect, id, NowControls.SiteId(file, line));
        }
    }

    public static partial class NowLayout
    {
        public static NowKeyBindingField KeyBindingField(NowId id = default, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return new NowKeyBindingField(id, NowControls.SiteId(file, line));
        }
    }
}
