using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NowUI
{
    /// <summary>One frame of text-editing input, normalized across backends.</summary>
    public struct NowTextInputFrame
    {
        /// <summary>Printable characters typed this frame (control characters stripped).</summary>
        public string characters;

        /// <summary>
        /// Uncommitted IME pre-edit text, or null when not composing. Editors
        /// render it inline at the caret and suppress editing keys while it is
        /// non-empty; committed text still arrives through <see cref="characters"/>.
        /// </summary>
        public string composition;

        public bool backspaceHeld;
        public bool deleteHeld;
        public bool leftHeld;
        public bool rightHeld;
        public bool upHeld;
        public bool downHeld;
        public bool homePressed;
        public bool endPressed;
        public bool enterPressed;
        public bool enterHeld;
        public bool escapePressed;
        public bool tabPressed;
        public bool tabHeld;

        public bool shift;

        /// <summary>Ctrl on Windows/Linux, Command on macOS.</summary>
        public bool command;

        /// <summary>Alt on Windows/Linux, Option on macOS.</summary>
        public bool option;

        /// <summary>Word-level movement/deletion modifier: Option on macOS, Ctrl elsewhere.</summary>
        public bool wordModifier => NowTextInput.isMacPlatform ? option : command;

        /// <summary>Line/document-boundary modifier for arrows and Backspace: Command on macOS, unused elsewhere.</summary>
        public bool lineModifier => NowTextInput.isMacPlatform && command;

        public bool copyPressed;
        public bool pastePressed;
        public bool cutPressed;
        public bool selectAllPressed;
        public bool undoPressed;
        public bool redoPressed;
        public bool duplicatePressed;

        /// <summary>Ctrl+/ (Cmd+/ on macOS): toggle line comment in code editors.</summary>
        public bool commentPressed;

        /// <summary>Ctrl+G (Cmd+G on macOS): go to line in code editors.</summary>
        public bool goToLinePressed;

        /// <summary>F2: rename the symbol at the caret in code editors.</summary>
        public bool renamePressed;

        /// <summary>
        /// True when this frame contains keyboard input that an interactive
        /// control may consume. Retained hosts use this to schedule a draw for
        /// shortcuts and editing keys without rebuilding continuously for a
        /// modifier key held on its own.
        /// </summary>
        public bool hasActivity =>
            !string.IsNullOrEmpty(characters) ||
            !string.IsNullOrEmpty(composition) ||
            backspaceHeld || deleteHeld ||
            leftHeld || rightHeld || upHeld || downHeld ||
            homePressed || endPressed ||
            enterPressed || enterHeld || escapePressed ||
            tabPressed || tabHeld || renamePressed ||
            copyPressed || pastePressed || cutPressed || selectAllPressed ||
            undoPressed || redoPressed || duplicatePressed ||
            commentPressed || goToLinePressed;
    }

    public interface INowTextInputSource
    {
        bool TryGetFrame(out NowTextInputFrame frame);
    }

    public interface INowTextInputBuffer
    {
        void DiscardPendingText();
    }

    /// <summary>
    /// Frame-sampled text-editing input for text fields and custom editors.
    /// Reads the Input System keyboard (legacy input as fallback); replace
    /// <see cref="source"/> with a fake in tests, the same seam the pointer
    /// providers use.
    /// </summary>
    public static class NowTextInput
    {
        static INowTextInputSource _source;

        static NowTextInputFrame _frame;

        static int _frameStamp = -1;

        public static INowTextInputSource source
        {
            get => _source ??= NowKeyboardTextInputSource.instance;
            set => _source = value;
        }

        /// <summary>
        /// True on macOS, where Option/Command drive word and line caret
        /// movement. Detected from the running platform; settable so tests can
        /// exercise both conventions.
        /// </summary>
        public static bool isMacPlatform = DetectMacPlatform();

        static bool DetectMacPlatform()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer;
        }

        public static NowTextInputFrame current
        {
            get
            {
                if (_frameStamp != Time.frameCount)
                {
                    _frameStamp = Time.frameCount;
                    MaintainCapture();

                    if (!source.TryGetFrame(out _frame))
                        _frame = default;

                    if (_enterConsumed)
                    {
                        if (Time.realtimeSinceStartup - _enterConsumedTime > EnterConsumedTimeout)
                        {
                            _enterConsumed = false;
                        }
                        else if (_frame.enterPressed || _frame.enterHeld)
                        {
                            _frame.enterPressed = false;
                            _frame.enterHeld = false;
                        }
                        else
                        {
                            _enterConsumed = false;
                        }
                    }
                }

                return _frame;
            }
        }

        static int _captureRequestFrame = -1;

        static bool _captureActive;

        static bool _enterConsumed;

        static float _enterConsumedTime;

        /// <summary>
        /// Release detection samples only when some control reads
        /// <see cref="current"/>; if focus leaves every text control while
        /// Enter is still down, the release goes unobserved and the flag would
        /// stay armed to eat a future, unrelated keystroke. No deliberate
        /// commit-Enter is held this long, so expire the consumption instead.
        /// </summary>
        const float EnterConsumedTimeout = 2f;

        /// <summary>
        /// Swallows the current Enter keystroke until the key is released: the
        /// frame's enter flags read false for every later consumer, including
        /// held-key repeat. A control that commits on Enter and hands focus
        /// away — an inline rename field, a dialog — calls this so the same
        /// keystroke doesn't leak into whatever gains focus next (a code
        /// editor would insert a newline from the still-held key).
        /// </summary>
        public static void ConsumeEnterUntilReleased()
        {
            _enterConsumed = true;
            _enterConsumedTime = Time.realtimeSinceStartup;

            if (_frameStamp == Time.frameCount)
            {
                _frame.enterPressed = false;
                _frame.enterHeld = false;
            }
        }

        /// <summary>
        /// Declares that the calling control consumes text input this frame.
        /// Focused text editors call this every interactive frame; the platform
        /// IME turns on with the first request and off once a full frame passes
        /// with none. Centralizing the transitions here means focus handoffs
        /// between text controls can never race an enable against a disable,
        /// and a control that stops being drawn mid-focus cannot leave text
        /// input dead — the old per-control on/off calls did both.
        /// </summary>
        public static void RequestTextCapture()
        {
            if (NowInput.isPassive)
                return;

            _captureRequestFrame = Time.frameCount;

            if (!_captureActive)
            {
                _captureActive = true;
                setImeEnabled?.Invoke(true);
            }
        }

        /// <summary>
        /// Releases the IME when no control has requested capture for a full
        /// frame. Runs from the interaction plumbing so release doesn't depend
        /// on any text control still drawing.
        /// </summary>
        internal static void MaintainCapture()
        {
            MaintainCapture(Time.frameCount);
        }

        internal static void MaintainCapture(int frame)
        {
            if (_captureActive && _captureRequestFrame < frame - 1)
            {
                _captureActive = false;
                setImeEnabled?.Invoke(false);
            }
        }

        /// <summary>Forces resampling; used by tests where frameCount is static.</summary>
        public static void Invalidate()
        {
            _frameStamp = -1;
        }

        /// <summary>Discards characters captured before an editor became active.</summary>
        public static void DiscardPending()
        {
            if (source is INowTextInputBuffer buffer)
                buffer.DiscardPendingText();

            if (_frameStamp == Time.frameCount)
            {
                _frame.characters = null;
                _frame.composition = null;
            }
        }

        /// <summary>
        /// Turns the platform IME on or off; text editors call this on focus
        /// transitions. Replaceable for hosts with their own IME flow.
        /// </summary>
        public static System.Action<bool> setImeEnabled = DefaultSetImeEnabled;

        /// <summary>
        /// Reports the caret position (surface coordinates) so the IME candidate
        /// window opens next to it. The default forwards to the platform IME;
        /// hosts whose surface is not the screen (UGUI canvases, render
        /// textures) replace this to transform the point first.
        /// </summary>
        public static System.Action<Vector2> setCompositionCursor = DefaultSetCompositionCursor;

        static void DefaultSetImeEnabled(bool enabled)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                keyboard.SetIMEEnabled(enabled);
                return;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                Input.imeCompositionMode = enabled ? IMECompositionMode.On : IMECompositionMode.Auto;
            }
            catch (System.InvalidOperationException)
            {
            }
#endif
        }

        static void DefaultSetCompositionCursor(Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                keyboard.SetIMECursorPosition(position);
                return;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                Input.compositionCursorPos = position;
            }
            catch (System.InvalidOperationException)
            {
            }
#endif
        }

        public static void Reset()
        {
            _source = null;
            _frame = default;
            _frameStamp = -1;
            _captureRequestFrame = -1;
            _captureActive = false;
            _enterConsumed = false;
            isMacPlatform = DetectMacPlatform();
            setImeEnabled = DefaultSetImeEnabled;
            setCompositionCursor = DefaultSetCompositionCursor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    /// <summary>Default keyboard-backed source.</summary>
    sealed class NowKeyboardTextInputSource : INowTextInputSource, INowTextInputBuffer
    {
        public static readonly NowKeyboardTextInputSource instance = new NowKeyboardTextInputSource();

        readonly StringBuilder _pending = new StringBuilder(16);

#if ENABLE_INPUT_SYSTEM
        Keyboard _subscribed;

        string _composition;

        void EnsureSubscribed(Keyboard keyboard)
        {
            if (ReferenceEquals(_subscribed, keyboard))
                return;

            if (_subscribed != null)
            {
                _subscribed.onTextInput -= OnTextInput;
                _subscribed.onIMECompositionChange -= OnIMECompositionChange;
            }

            _subscribed = keyboard;
            _composition = null;

            if (keyboard != null)
            {
                keyboard.onTextInput += OnTextInput;
                keyboard.onIMECompositionChange += OnIMECompositionChange;
            }
        }

        void OnTextInput(char character)
        {
            if (!char.IsControl(character))
                _pending.Append(character);
        }

        void OnIMECompositionChange(UnityEngine.InputSystem.LowLevel.IMECompositionString composition)
        {
            _composition = composition.Count > 0 ? composition.ToString() : null;
        }
#endif

        public void DiscardPendingText()
        {
            _pending.Clear();

#if ENABLE_INPUT_SYSTEM
            _composition = null;
#endif
        }

        public bool TryGetFrame(out NowTextInputFrame frame)
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
                frame.enterHeld = keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed;
                frame.escapePressed = keyboard.escapeKey.wasPressedThisFrame;
                frame.tabPressed = keyboard.tabKey.wasPressedThisFrame;
                frame.tabHeld = keyboard.tabKey.isPressed;
                frame.renamePressed = keyboard.f2Key.wasPressedThisFrame;
                frame.shift = keyboard.shiftKey.isPressed;
                frame.composition = _composition;

                bool ctrl = keyboard.ctrlKey.isPressed;
                bool command = keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
                frame.command = NowTextInput.isMacPlatform ? command : ctrl;
                frame.option = keyboard.altKey.isPressed;

                // AltGr arrives as Ctrl+Alt on Windows/Linux; chords and
                // character stripping must not swallow the typed characters.
                if (frame.command && !frame.option)
                {
                    frame.copyPressed = keyboard.cKey.wasPressedThisFrame;
                    frame.pastePressed = keyboard.vKey.wasPressedThisFrame;
                    frame.cutPressed = keyboard.xKey.wasPressedThisFrame;
                    frame.selectAllPressed = keyboard.aKey.wasPressedThisFrame;
                    frame.undoPressed = keyboard.zKey.wasPressedThisFrame && !frame.shift;
                    frame.redoPressed = keyboard.yKey.wasPressedThisFrame ||
                        (keyboard.zKey.wasPressedThisFrame && frame.shift);
                    frame.duplicatePressed = keyboard.dKey.wasPressedThisFrame;
                    frame.commentPressed = keyboard.slashKey.wasPressedThisFrame;
                    frame.goToLinePressed = keyboard.gKey.wasPressedThisFrame;

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
                frame.enterHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
                frame.escapePressed = Input.GetKeyDown(KeyCode.Escape);
                frame.tabPressed = Input.GetKeyDown(KeyCode.Tab);
                frame.tabHeld = Input.GetKey(KeyCode.Tab);
                frame.shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                frame.renamePressed = Input.GetKeyDown(KeyCode.F2);

                string composing = Input.compositionString;
                frame.composition = string.IsNullOrEmpty(composing) ? null : composing;

                frame.command = NowTextInput.isMacPlatform
                    ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                    : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                frame.option = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);

                if (frame.command && !frame.option)
                {
                    frame.copyPressed = Input.GetKeyDown(KeyCode.C);
                    frame.pastePressed = Input.GetKeyDown(KeyCode.V);
                    frame.cutPressed = Input.GetKeyDown(KeyCode.X);
                    frame.selectAllPressed = Input.GetKeyDown(KeyCode.A);
                    frame.undoPressed = Input.GetKeyDown(KeyCode.Z) && !frame.shift;
                    frame.redoPressed = Input.GetKeyDown(KeyCode.Y) || (Input.GetKeyDown(KeyCode.Z) && frame.shift);
                    frame.duplicatePressed = Input.GetKeyDown(KeyCode.D);
                    frame.commentPressed = Input.GetKeyDown(KeyCode.Slash);
                    frame.goToLinePressed = Input.GetKeyDown(KeyCode.G);
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
