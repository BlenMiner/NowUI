using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NowUI
{
    /// <summary>One frame of text-editing input, normalized across backends.</summary>
    public struct NowUITextInputFrame
    {
        /// <summary>Printable characters typed this frame (control characters stripped).</summary>
        public string characters;

        public bool backspaceHeld;
        public bool deleteHeld;
        public bool leftHeld;
        public bool rightHeld;
        public bool upHeld;
        public bool downHeld;
        public bool homePressed;
        public bool endPressed;
        public bool enterPressed;
        public bool escapePressed;

        public bool shift;

        /// <summary>Ctrl on Windows/Linux, Command on macOS.</summary>
        public bool command;

        public bool copyPressed;
        public bool pastePressed;
        public bool cutPressed;
        public bool selectAllPressed;
    }

    public interface INowUITextInputSource
    {
        bool TryGetFrame(out NowUITextInputFrame frame);
    }

    /// <summary>
    /// Frame-sampled text-editing input for text fields and custom editors.
    /// Reads the Input System keyboard (legacy input as fallback); replace
    /// <see cref="source"/> with a fake in tests, the same seam the pointer
    /// providers use.
    /// </summary>
    public static class NowUITextInput
    {
        static INowUITextInputSource _source;

        static NowUITextInputFrame _frame;

        static int _frameStamp = -1;

        public static INowUITextInputSource source
        {
            get => _source ??= NowUIKeyboardTextInputSource.instance;
            set => _source = value;
        }

        public static NowUITextInputFrame current
        {
            get
            {
                if (_frameStamp != Time.frameCount)
                {
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
        }

        public static void Reset()
        {
            _source = null;
            _frame = default;
            _frameStamp = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    /// <summary>Default keyboard-backed source.</summary>
    sealed class NowUIKeyboardTextInputSource : INowUITextInputSource
    {
        public static readonly NowUIKeyboardTextInputSource instance = new NowUIKeyboardTextInputSource();

        readonly StringBuilder _pending = new StringBuilder(16);

#if ENABLE_INPUT_SYSTEM
        Keyboard _subscribed;

        void EnsureSubscribed(Keyboard keyboard)
        {
            if (ReferenceEquals(_subscribed, keyboard))
                return;

            if (_subscribed != null)
                _subscribed.onTextInput -= OnTextInput;

            _subscribed = keyboard;

            if (keyboard != null)
                keyboard.onTextInput += OnTextInput;
        }

        void OnTextInput(char character)
        {
            if (!char.IsControl(character))
                _pending.Append(character);
        }
#endif

        public bool TryGetFrame(out NowUITextInputFrame frame)
        {
            frame = default;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            EnsureSubscribed(keyboard);

            if (keyboard != null)
            {
                if (_pending.Length > 0)
                {
                    frame.characters = _pending.ToString();
                    _pending.Clear();
                }

                frame.backspaceHeld = keyboard.backspaceKey.isPressed;
                frame.deleteHeld = keyboard.deleteKey.isPressed;
                frame.leftHeld = keyboard.leftArrowKey.isPressed;
                frame.rightHeld = keyboard.rightArrowKey.isPressed;
                frame.upHeld = keyboard.upArrowKey.isPressed;
                frame.downHeld = keyboard.downArrowKey.isPressed;
                frame.homePressed = keyboard.homeKey.wasPressedThisFrame;
                frame.endPressed = keyboard.endKey.wasPressedThisFrame;
                frame.enterPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                frame.escapePressed = keyboard.escapeKey.wasPressedThisFrame;
                frame.shift = keyboard.shiftKey.isPressed;

                bool ctrl = keyboard.ctrlKey.isPressed;
                bool command = keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
                frame.command = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer
                    ? command
                    : ctrl;

                if (frame.command)
                {
                    frame.copyPressed = keyboard.cKey.wasPressedThisFrame;
                    frame.pastePressed = keyboard.vKey.wasPressedThisFrame;
                    frame.cutPressed = keyboard.xKey.wasPressedThisFrame;
                    frame.selectAllPressed = keyboard.aKey.wasPressedThisFrame;

                    // Command chords never insert their letter.
                    frame.characters = null;
                }

                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                string typed = Input.inputString;

                if (!string.IsNullOrEmpty(typed))
                {
                    _pending.Clear();

                    for (int i = 0; i < typed.Length; ++i)
                    {
                        if (!char.IsControl(typed[i]))
                            _pending.Append(typed[i]);
                    }

                    if (_pending.Length > 0)
                    {
                        frame.characters = _pending.ToString();
                        _pending.Clear();
                    }
                }

                frame.backspaceHeld = Input.GetKey(KeyCode.Backspace);
                frame.deleteHeld = Input.GetKey(KeyCode.Delete);
                frame.leftHeld = Input.GetKey(KeyCode.LeftArrow);
                frame.rightHeld = Input.GetKey(KeyCode.RightArrow);
                frame.upHeld = Input.GetKey(KeyCode.UpArrow);
                frame.downHeld = Input.GetKey(KeyCode.DownArrow);
                frame.homePressed = Input.GetKeyDown(KeyCode.Home);
                frame.endPressed = Input.GetKeyDown(KeyCode.End);
                frame.enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                frame.escapePressed = Input.GetKeyDown(KeyCode.Escape);
                frame.shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
                frame.command = isMac
                    ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                    : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                if (frame.command)
                {
                    frame.copyPressed = Input.GetKeyDown(KeyCode.C);
                    frame.pastePressed = Input.GetKeyDown(KeyCode.V);
                    frame.cutPressed = Input.GetKeyDown(KeyCode.X);
                    frame.selectAllPressed = Input.GetKeyDown(KeyCode.A);
                    frame.characters = null;
                }

                return true;
            }
            catch (System.InvalidOperationException)
            {
            }
#endif

            return false;
        }
    }
}
