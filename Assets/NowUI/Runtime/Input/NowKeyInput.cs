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
