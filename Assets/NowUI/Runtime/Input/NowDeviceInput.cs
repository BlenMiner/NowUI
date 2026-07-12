using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NowUI
{
    /// <summary>
    /// Keyboard keys the built-in device input maps to UI navigation, focus,
    /// and submit. Configured through <see cref="NowInput.navigationKeys"/>;
    /// Escape-as-cancel is always mapped.
    /// </summary>
    [Flags]
    public enum NowNavigationKeys
    {
        None = 0,
        Arrows = 1 << 0,
        Wasd = 1 << 1,
        TabFocus = 1 << 2,
        SpaceSubmit = 1 << 3,
        EnterSubmit = 1 << 4,
        All = Arrows | Wasd | TabFocus | SpaceSubmit | EnterSubmit
    }

    internal struct NowMouseInput
    {
        /// <summary>Sentinel <see cref="pointerSource"/> value for the mouse.</summary>
        public const int MousePointerSource = -1;

        public bool hasPointer;

        /// <summary>Identifies which physical source drives the pointer this frame
        /// (<see cref="MousePointerSource"/> for the mouse, the touch id for a
        /// touch) so consumers can zero deltas when the source changes.</summary>
        public int pointerSource;

        public Vector2 screenPosition;

        public NowPointerButtons pointerButtonsDown;

        public NowPointerButtons pointerButtonsPressed;

        public NowPointerButtons pointerButtonsReleased;

        public Vector2 scrollDelta;

        public Vector2 navigation;

        public bool focusPreviousPressed;

        public bool focusNextPressed;

        public bool submitDown;

        public bool submitPressed;

        public bool submitReleased;

        public bool cancelDown;

        public bool cancelPressed;

        public bool cancelReleased;

        public static bool TryGet(out NowMouseInput input)
        {
            return NowInputSystemInput.TryGet(out input);
        }
    }

    internal static class NowInputSystemInput
    {
        static NowMouseInput s_input;

        static bool s_available;

        static int s_frameStamp = -1;

        static NowNavigationKeys s_navigationKeys;

        /// <summary>
        /// Frame-cached device read shared by every provider: the ~40 Input
        /// System control reads run once per frame no matter how many hosts
        /// poll. Control state cannot change within an Input System update, so
        /// the cache is behavior-preserving; a mid-frame
        /// <see cref="NowInput.navigationKeys"/> change still resamples.
        /// </summary>
        public static bool TryGet(out NowMouseInput input)
        {
            var navigationKeys = NowInput.navigationKeys;

            if (s_frameStamp != Time.frameCount || s_navigationKeys != navigationKeys)
            {
                s_frameStamp = Time.frameCount;
                s_navigationKeys = navigationKeys;
                s_available = Read(navigationKeys, out s_input);
            }

            input = s_input;
            return s_available;
        }

        /// <summary>Forces resampling; used by tests where frameCount is static.</summary>
        public static void Invalidate()
        {
            s_frameStamp = -1;
        }

        static bool Read(NowNavigationKeys navigationKeys, out NowMouseInput input)
        {
            input = default;
            bool hasAnyInput = false;
            Mouse mouse = Mouse.current;

            if (mouse != null)
            {
                input.hasPointer = true;
                input.pointerSource = NowMouseInput.MousePointerSource;
                input.screenPosition = mouse.position.ReadValue();

                // Windows reports wheel ticks as ±120 through the input system
                // (legacy and macOS report small values); normalize to notches so
                // consumers can scale in pixels without teleporting.
                Vector2 scroll = mouse.scroll.ReadValue();

                if (Mathf.Abs(scroll.x) >= 60f || Mathf.Abs(scroll.y) >= 60f)
                    scroll /= 120f;

                input.scrollDelta = scroll;
                AppendPointerButton(mouse.leftButton, NowPointerButton.Primary, ref input);
                AppendPointerButton(mouse.rightButton, NowPointerButton.Secondary, ref input);
                AppendPointerButton(mouse.middleButton, NowPointerButton.Middle, ref input);
                AppendPointerButton(mouse.backButton, NowPointerButton.Back, ref input);
                AppendPointerButton(mouse.forwardButton, NowPointerButton.Forward, ref input);
                hasAnyInput = true;
            }

            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen != null)
            {
                // Only treat the touchscreen as the pointer while a touch is in
                // contact (or just lifted, so releases still register). Outside of
                // that window the last touch position is stale and would pin hover
                // states to wherever the finger left the screen.
                var touch = SelectTrackedTouch(touchscreen);

                if (touch != null)
                {
                    input.hasPointer = true;
                    input.pointerSource = touch.touchId.ReadValue();
                    input.screenPosition = touch.position.ReadValue();
                    AppendPointerButton(touch.press, NowPointerButton.Primary, ref input);
                    hasAnyInput = true;
                }
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                input.navigation += ReadKeyboardNavigation(keyboard, navigationKeys);

                if ((navigationKeys & NowNavigationKeys.TabFocus) != 0)
                {
                    input.focusPreviousPressed |= keyboard.tabKey.wasPressedThisFrame && keyboard.shiftKey.isPressed;
                    input.focusNextPressed |= keyboard.tabKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed;
                }

                if ((navigationKeys & NowNavigationKeys.EnterSubmit) != 0)
                {
                    MergeButton(keyboard.enterKey, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
                    MergeButton(keyboard.numpadEnterKey, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
                }

                if ((navigationKeys & NowNavigationKeys.SpaceSubmit) != 0)
                    MergeButton(keyboard.spaceKey, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);

                MergeButton(keyboard.escapeKey, ref input.cancelDown, ref input.cancelPressed, ref input.cancelReleased);
                hasAnyInput = true;
            }

            Gamepad gamepad = Gamepad.current;

            if (gamepad != null)
            {
                input.navigation += gamepad.leftStick.ReadValue();
                input.navigation += gamepad.dpad.ReadValue();
                MergeButton(gamepad.buttonSouth, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
                MergeButton(gamepad.startButton, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
                MergeButton(gamepad.buttonEast, ref input.cancelDown, ref input.cancelPressed, ref input.cancelReleased);
                MergeButton(gamepad.selectButton, ref input.cancelDown, ref input.cancelPressed, ref input.cancelReleased);
                hasAnyInput = true;
            }

            input.navigation = Vector2.ClampMagnitude(input.navigation, 1f);
            return hasAnyInput;
        }

        static int s_trackedTouchId = -1;

        /// <summary>
        /// Picks the touch that drives the pointer: the currently tracked touch
        /// while it stays pressed, else the earliest active touch (so lifting one
        /// finger hands the pointer to another without killing it), else the
        /// tracked touch's release so drags always see their pointer-up.
        /// </summary>
        static TouchControl SelectTrackedTouch(Touchscreen touchscreen)
        {
            var touches = touchscreen.touches;
            TouchControl tracked = null;
            TouchControl earliest = null;
            TouchControl trackedRelease = null;
            double earliestStartTime = double.MaxValue;

            for (int i = 0; i < touches.Count; ++i)
            {
                var touch = touches[i];
                var press = touch.press;

                if (press.isPressed || press.wasPressedThisFrame)
                {
                    if (touch.touchId.ReadValue() == s_trackedTouchId)
                        tracked = touch;

                    double startTime = touch.startTime.ReadValue();

                    if (startTime < earliestStartTime)
                    {
                        earliestStartTime = startTime;
                        earliest = touch;
                    }
                }
                else if (press.wasReleasedThisFrame && touch.touchId.ReadValue() == s_trackedTouchId)
                {
                    trackedRelease = touch;
                }
            }

            var selected = tracked ?? earliest ?? trackedRelease;
            bool selectedPressed = selected != null &&
                (selected.press.isPressed || selected.press.wasPressedThisFrame);
            s_trackedTouchId = selectedPressed ? selected.touchId.ReadValue() : -1;
            return selected;
        }

        static void AppendPointerButton(ButtonControl control, NowPointerButton button, ref NowMouseInput input)
        {
            if (control == null)
                return;

            var mask = NowInputSnapshot.ToButtonMask(button);

            if (control.isPressed)
                input.pointerButtonsDown |= mask;

            if (control.wasPressedThisFrame)
                input.pointerButtonsPressed |= mask;

            if (control.wasReleasedThisFrame)
                input.pointerButtonsReleased |= mask;
        }

        static Vector2 ReadKeyboardNavigation(Keyboard keyboard, NowNavigationKeys navigationKeys)
        {
            bool arrows = (navigationKeys & NowNavigationKeys.Arrows) != 0;
            bool wasd = (navigationKeys & NowNavigationKeys.Wasd) != 0;
            float x = 0f;
            float y = 0f;

            if ((arrows && keyboard.leftArrowKey.isPressed) || (wasd && keyboard.aKey.isPressed))
                x -= 1f;

            if ((arrows && keyboard.rightArrowKey.isPressed) || (wasd && keyboard.dKey.isPressed))
                x += 1f;

            if ((arrows && keyboard.downArrowKey.isPressed) || (wasd && keyboard.sKey.isPressed))
                y -= 1f;

            if ((arrows && keyboard.upArrowKey.isPressed) || (wasd && keyboard.wKey.isPressed))
                y += 1f;

            return new Vector2(x, y);
        }

        static void MergeButton(ButtonControl control, ref bool down, ref bool pressed, ref bool released)
        {
            if (control == null)
                return;

            down |= control.isPressed;
            pressed |= control.wasPressedThisFrame;
            released |= control.wasReleasedThisFrame;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            s_frameStamp = -1;
            s_trackedTouchId = -1;
        }
    }
}
