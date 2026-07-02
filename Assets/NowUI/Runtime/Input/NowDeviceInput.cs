using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NowUI
{
    internal struct NowMouseInput
    {
        public bool hasPointer;

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
        public static bool TryGet(out NowMouseInput input)
        {
            input = default;
            bool hasAnyInput = false;
            Mouse mouse = Mouse.current;

            if (mouse != null)
            {
                input.hasPointer = true;
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
                var primaryTouch = touchscreen.primaryTouch;
                var press = primaryTouch.press;

                // Only treat the touchscreen as the pointer while a touch is in
                // contact (or just lifted, so releases still register). Outside of
                // that window the last touch position is stale and would pin hover
                // states to wherever the finger left the screen.
                if (press.isPressed || press.wasPressedThisFrame || press.wasReleasedThisFrame)
                {
                    input.hasPointer = true;
                    input.screenPosition = primaryTouch.position.ReadValue();
                    AppendPointerButton(press, NowPointerButton.Primary, ref input);
                    hasAnyInput = true;
                }
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                input.navigation += ReadKeyboardNavigation(keyboard);
                input.focusPreviousPressed |= keyboard.tabKey.wasPressedThisFrame && keyboard.shiftKey.isPressed;
                input.focusNextPressed |= keyboard.tabKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed;
                MergeButton(keyboard.enterKey, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
                MergeButton(keyboard.numpadEnterKey, ref input.submitDown, ref input.submitPressed, ref input.submitReleased);
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

        static Vector2 ReadKeyboardNavigation(Keyboard keyboard)
        {
            float x = 0f;
            float y = 0f;

            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
                x -= 1f;

            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
                x += 1f;

            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
                y -= 1f;

            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
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
    }
}
