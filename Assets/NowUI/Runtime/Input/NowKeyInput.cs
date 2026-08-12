#if NOWUI_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NowUI
{
    /// <summary>One frame of raw key input, used by key-binding capture.</summary>
    public struct NowKeyInputFrame
    {
        /// <summary>
        /// Keyboard key that went down this frame, or <see cref="Key.None"/>.
        /// When several keys land on the same frame the lowest enum value wins.
        /// </summary>
        public Key pressedKey;
    }

    public interface INowKeyInputSource
    {
        bool TryGetFrame(out NowKeyInputFrame frame);
    }

    /// <summary>
    /// Raw key presses for key-binding capture. Editor IMGUI uses the current
    /// provider/input-pass packet; other hosts prefer the Input System keyboard
    /// and fall back to the legacy Input Manager when that backend is enabled.
    /// Replace <see cref="source"/> with a fake in tests, the same seam
    /// <see cref="NowTextInput"/> uses.
    /// </summary>
    public static class NowKeyInput
    {
        static INowKeyInputSource _source;

        static NowKeyInputFrame _frame;

        static int _frameStamp = -1;

        static NowIMGUIInputProvider _frameProvider;

        static int _inputPassStamp = int.MinValue;

        static bool _inputPassActive;

        static bool _activityClaimed;

        public static INowKeyInputSource source
        {
            get => _source ??= NowKeyboardKeyInputSource.instance;
            set => _source = value;
        }

        public static NowKeyInputFrame current
        {
            get
            {
                NowIMGUIInputProvider imgui =
                    NowInput.hasContext
                        ? NowInput.currentProvider as NowIMGUIInputProvider
                        : null;
                int inputPass = imgui != null ? NowInput.current.inputPass : int.MinValue;
                bool usesDefaultSource = _source == null ||
                    object.ReferenceEquals(_source, NowKeyboardKeyInputSource.instance);

                if (usesDefaultSource && imgui != null &&
                    imgui.TryGetKeyInputFrame(inputPass, out NowKeyInputFrame nativeFrame))
                {
                    if (!ReferenceEquals(_frameProvider, imgui) ||
                        _inputPassStamp != inputPass)
                    {
                        _frameProvider = imgui;
                        _inputPassStamp = inputPass;
                        _frameStamp = -1;
                        _frame = nativeFrame;
                    }
                }
                else if (_frameProvider != null || _frameStamp != Time.frameCount)
                {
                    _frameProvider = null;
                    _inputPassStamp = int.MinValue;
                    _frameStamp = Time.frameCount;

                    if (!source.TryGetFrame(out _frame))
                        _frame = default;
                }

                return _frame;
            }
        }

        /// <summary>Forces resampling; used by tests where frameCount is static.</summary>
        public static void Invalidate()
        {
            _frameStamp = -1;
            _frameProvider = null;
            _inputPassStamp = int.MinValue;
        }

        internal static void BeginInputPass()
        {
            _inputPassActive = true;
            _activityClaimed = false;
        }

        /// <summary>
        /// Claims the current raw key press for this input pass. IMGUI consumes
        /// the native KeyDown so another panel cannot bind the same press.
        /// </summary>
        public static void ClaimActivity()
        {
            if (!_inputPassActive || NowInput.isPassive || _activityClaimed)
                return;

            _activityClaimed = true;

            if (NowInput.currentProvider is NowIMGUIInputProvider imgui)
                imgui.NotifyKeyActivityClaimed();
        }

        internal static void EndInputPass()
        {
            if (!_inputPassActive)
                return;

            _inputPassActive = false;

            if (!_activityClaimed)
                return;

            _activityClaimed = false;
            _frame.pressedKey = Key.None;
        }

        /// <summary>
        /// Discards a key edge that armed a capture control. The next native
        /// IMGUI KeyDown invalidates the cache and remains eligible.
        /// </summary>
        public static void DiscardPending()
        {
            _frame = default;

            if (NowInput.hasContext &&
                NowInput.currentProvider is NowIMGUIInputProvider imgui)
            {
                int inputPass = NowInput.current.inputPass;
                imgui.DiscardKeyInputFrame(inputPass);
                _frameProvider = imgui;
                _inputPassStamp = inputPass;
                _frameStamp = -1;
            }
            else
            {
                _frameProvider = null;
                _inputPassStamp = int.MinValue;
                _frameStamp = Time.frameCount;
            }
        }

        public static void Reset()
        {
            _source = null;
            _frame = default;
            _frameStamp = -1;
            _frameProvider = null;
            _inputPassStamp = int.MinValue;
            _inputPassActive = false;
            _activityClaimed = false;
        }

        /// <summary>
        /// Maps Unity's native IMGUI key identity to the Input System's physical
        /// key enum without polling another input clock or allocating strings.
        /// </summary>
        internal static Key FromIMGUIKeyCode(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                return (Key)((int)Key.A + ((int)keyCode - (int)KeyCode.A));

            if (keyCode >= KeyCode.Alpha1 && keyCode <= KeyCode.Alpha9)
                return (Key)((int)Key.Digit1 + ((int)keyCode - (int)KeyCode.Alpha1));

            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
                return (Key)((int)Key.Numpad0 + ((int)keyCode - (int)KeyCode.Keypad0));

            if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
                return (Key)((int)Key.F1 + ((int)keyCode - (int)KeyCode.F1));

            if (keyCode >= KeyCode.F13 && keyCode <= KeyCode.F15)
                return (Key)((int)Key.F13 + ((int)keyCode - (int)KeyCode.F13));

            if (keyCode >= KeyCode.F16 && keyCode <= KeyCode.F24)
                return (Key)((int)Key.F16 + ((int)keyCode - (int)KeyCode.F16));

            switch (keyCode)
            {
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt:
                case KeyCode.AltGr: return Key.RightAlt;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                // Command also covers Unity's same-value LeftMeta/LeftApple
                // aliases; Windows uses a distinct enum value.
                case KeyCode.LeftCommand:
                case KeyCode.LeftWindows: return Key.LeftMeta;
                // Same-value RightMeta/RightApple aliases are covered here.
                case KeyCode.RightCommand:
                case KeyCode.RightWindows: return Key.RightMeta;
                case KeyCode.Menu: return Key.ContextMenu;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.Numlock: return Key.NumLock;
                case KeyCode.Print:
                case KeyCode.SysReq: return Key.PrintScreen;
                case KeyCode.ScrollLock: return Key.ScrollLock;
                case KeyCode.Pause:
                case KeyCode.Break: return Key.Pause;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
                case KeyCode.KeypadEquals: return Key.NumpadEquals;
                default: return Key.None;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    /// <summary>Default keyboard-backed source.</summary>
    sealed class NowKeyboardKeyInputSource : INowKeyInputSource
    {
        public static readonly NowKeyboardKeyInputSource instance = new NowKeyboardKeyInputSource();

#if ENABLE_LEGACY_INPUT_MANAGER
        static readonly KeyCode[] s_legacyKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
#endif

        public bool TryGetFrame(out NowKeyInputFrame frame)
        {
            frame = default;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (!keyboard.anyKey.wasPressedThisFrame)
                    return true;

                var keys = keyboard.allKeys;

                for (int i = 0; i < keys.Count; ++i)
                {
                    var key = keys[i];

                    if (key != null && key.wasPressedThisFrame)
                    {
                        frame.pressedKey = key.keyCode;
                        return true;
                    }
                }

                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (!Input.anyKeyDown)
                    return true;

                Key pressed = Key.None;

                for (int i = 0; i < s_legacyKeyCodes.Length; ++i)
                {
                    KeyCode keyCode = s_legacyKeyCodes[i];

                    if (!Input.GetKeyDown(keyCode))
                        continue;

                    Key mapped = NowKeyInput.FromIMGUIKeyCode(keyCode);

                    if (mapped != Key.None && (pressed == Key.None || (int)mapped < (int)pressed))
                        pressed = mapped;
                }

                frame.pressedKey = pressed;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
#endif
