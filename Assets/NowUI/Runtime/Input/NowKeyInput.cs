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
    /// Frame-sampled raw key presses for key-binding capture. Reads the Input
    /// System keyboard; replace <see cref="source"/> with a fake in tests, the
    /// same seam <see cref="NowTextInput"/> uses.
    /// </summary>
    public static class NowKeyInput
    {
        static INowKeyInputSource _source;

        static NowKeyInputFrame _frame;

        static int _frameStamp = -1;

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
            _frameStamp = Time.frameCount;
        }

        public static void Reset()
        {
            _source = null;
            _frame = default;
            _frameStamp = -1;
            _inputPassActive = false;
            _activityClaimed = false;
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

        public bool TryGetFrame(out NowKeyInputFrame frame)
        {
            frame = default;
            var keyboard = Keyboard.current;

            if (keyboard == null)
                return false;

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
    }
}
