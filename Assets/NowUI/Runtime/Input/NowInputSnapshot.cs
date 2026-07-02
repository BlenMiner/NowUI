using System;
using UnityEngine;

namespace NowUI
{
    public enum NowPointerButton
    {
        Primary = 0,
        Secondary = 1,
        Middle = 2,
        Back = 3,
        Forward = 4
    }

    [Flags]
    public enum NowPointerButtons
    {
        None = 0,
        Primary = 1 << 0,
        Secondary = 1 << 1,
        Middle = 1 << 2,
        Back = 1 << 3,
        Forward = 1 << 4
    }

    public struct NowInputSurface
    {
        public Vector2 size;

        public Rect screenRect;

        public NowInputSurface(Vector2 size)
            : this(size, new Rect(0f, 0f, size.x, size.y))
        {
        }

        public NowInputSurface(Vector2 size, Rect screenRect)
        {
            this.size = size;
            this.screenRect = screenRect;
        }

        public static NowInputSurface FromScreen()
        {
            var size = new Vector2(Screen.width, Screen.height);
            return new NowInputSurface(size);
        }

        public static NowInputSurface FromScreenMask(NowRect screenMask)
        {
            var size = new Vector2(screenMask.width, screenMask.height);
            var screenRect = new Rect(screenMask.x, screenMask.y, screenMask.width, screenMask.height);
            return new NowInputSurface(size, screenRect);
        }

        public static NowInputSurface FromCamera(Camera camera)
        {
            if (camera == null)
                return FromScreen();

            Rect pixelRect = camera.pixelRect;
            var size = new Vector2(camera.pixelWidth, camera.pixelHeight);
            var screenRect = new Rect(
                pixelRect.x,
                Screen.height - pixelRect.yMax,
                pixelRect.width,
                pixelRect.height);
            return new NowInputSurface(size, screenRect);
        }
    }

    public struct NowInputSnapshot
    {
        public readonly bool hasPointer;

        public Vector2 pointerPosition;

        public Vector2 previousPointerPosition;

        public Vector2 pointerDelta;

        public bool primaryDown;

        public bool primaryPressed;

        public bool primaryReleased;

        public readonly NowPointerButtons pointerButtonsDown;

        public readonly NowPointerButtons pointerButtonsPressed;

        public readonly NowPointerButtons pointerButtonsReleased;

        /// <summary>Scroll movement in the canonical unit shared by every provider: wheel notches, one unit per notch, with +y meaning scroll up (wheel away from the user).</summary>
        public Vector2 scrollDelta;

        public Vector2 navigation;

        public readonly bool focusPreviousPressed;

        public readonly bool focusNextPressed;

        public readonly bool submitDown;

        public readonly bool submitPressed;

        public bool submitReleased;

        public bool cancelDown;

        public bool cancelPressed;

        public bool cancelReleased;

        public int frame;

        public float time;

        public NowInputSnapshot(
            Vector2 pointerPosition,
            bool primaryDown,
            bool primaryPressed,
            bool primaryReleased)
            : this(
                true,
                pointerPosition,
                pointerPosition,
                Vector2.zero,
                ToButtonMask(primaryDown, NowPointerButton.Primary),
                ToButtonMask(primaryPressed, NowPointerButton.Primary),
                ToButtonMask(primaryReleased, NowPointerButton.Primary),
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                Time.frameCount,
                Time.realtimeSinceStartup)
        {
        }

        public NowInputSnapshot(
            Vector2 pointerPosition,
            NowPointerButtons pointerButtonsDown,
            NowPointerButtons pointerButtonsPressed,
            NowPointerButtons pointerButtonsReleased)
            : this(
                true,
                pointerPosition,
                pointerPosition,
                Vector2.zero,
                pointerButtonsDown,
                pointerButtonsPressed,
                pointerButtonsReleased,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                Time.frameCount,
                Time.realtimeSinceStartup)
        {
        }

        public NowInputSnapshot(
            Vector2 pointerPosition,
            Vector2 pointerDelta,
            bool primaryDown,
            bool primaryPressed,
            bool primaryReleased)
            : this(
                true,
                pointerPosition,
                pointerPosition - pointerDelta,
                pointerDelta,
                ToButtonMask(primaryDown, NowPointerButton.Primary),
                ToButtonMask(primaryPressed, NowPointerButton.Primary),
                ToButtonMask(primaryReleased, NowPointerButton.Primary),
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                Time.frameCount,
                Time.realtimeSinceStartup)
        {
        }

        public NowInputSnapshot(
            Vector2 pointerPosition,
            Vector2 pointerDelta,
            NowPointerButtons pointerButtonsDown,
            NowPointerButtons pointerButtonsPressed,
            NowPointerButtons pointerButtonsReleased)
            : this(
                true,
                pointerPosition,
                pointerPosition - pointerDelta,
                pointerDelta,
                pointerButtonsDown,
                pointerButtonsPressed,
                pointerButtonsReleased,
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                Time.frameCount,
                Time.realtimeSinceStartup)
        {
        }

        public NowInputSnapshot(
            bool hasPointer,
            Vector2 pointerPosition,
            Vector2 previousPointerPosition,
            Vector2 pointerDelta,
            bool primaryDown,
            bool primaryPressed,
            bool primaryReleased,
            Vector2 scrollDelta,
            int frame,
            float time)
            : this(
                hasPointer,
                pointerPosition,
                previousPointerPosition,
                pointerDelta,
                ToButtonMask(primaryDown, NowPointerButton.Primary),
                ToButtonMask(primaryPressed, NowPointerButton.Primary),
                ToButtonMask(primaryReleased, NowPointerButton.Primary),
                scrollDelta,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                frame,
                time)
        {
        }

        public NowInputSnapshot(
            bool hasPointer,
            Vector2 pointerPosition,
            Vector2 previousPointerPosition,
            Vector2 pointerDelta,
            NowPointerButtons pointerButtonsDown,
            NowPointerButtons pointerButtonsPressed,
            NowPointerButtons pointerButtonsReleased,
            Vector2 scrollDelta,
            Vector2 navigation,
            bool submitDown,
            bool submitPressed,
            bool submitReleased,
            bool cancelDown,
            bool cancelPressed,
            bool cancelReleased,
            int frame,
            float time)
            : this(
                hasPointer,
                pointerPosition,
                previousPointerPosition,
                pointerDelta,
                pointerButtonsDown,
                pointerButtonsPressed,
                pointerButtonsReleased,
                scrollDelta,
                navigation,
                false,
                false,
                submitDown,
                submitPressed,
                submitReleased,
                cancelDown,
                cancelPressed,
                cancelReleased,
                frame,
                time)
        {
        }

        public NowInputSnapshot(
            bool hasPointer,
            Vector2 pointerPosition,
            Vector2 previousPointerPosition,
            Vector2 pointerDelta,
            NowPointerButtons pointerButtonsDown,
            NowPointerButtons pointerButtonsPressed,
            NowPointerButtons pointerButtonsReleased,
            Vector2 scrollDelta,
            Vector2 navigation,
            bool focusPreviousPressed,
            bool focusNextPressed,
            bool submitDown,
            bool submitPressed,
            bool submitReleased,
            bool cancelDown,
            bool cancelPressed,
            bool cancelReleased,
            int frame,
            float time)
        {
            this.hasPointer = hasPointer;
            this.pointerPosition = pointerPosition;
            this.previousPointerPosition = previousPointerPosition;
            this.pointerDelta = pointerDelta;
            this.pointerButtonsDown = pointerButtonsDown;
            this.pointerButtonsPressed = pointerButtonsPressed;
            this.pointerButtonsReleased = pointerButtonsReleased;
            primaryDown = IsSet(pointerButtonsDown, NowPointerButton.Primary);
            primaryPressed = IsSet(pointerButtonsPressed, NowPointerButton.Primary);
            primaryReleased = IsSet(pointerButtonsReleased, NowPointerButton.Primary);
            this.scrollDelta = scrollDelta;
            this.navigation = navigation;
            this.focusPreviousPressed = focusPreviousPressed;
            this.focusNextPressed = focusNextPressed;
            this.submitDown = submitDown;
            this.submitPressed = submitPressed;
            this.submitReleased = submitReleased;
            this.cancelDown = cancelDown;
            this.cancelPressed = cancelPressed;
            this.cancelReleased = cancelReleased;
            this.frame = frame;
            this.time = time;
        }

        /// <summary>True when any pointer button was pressed this frame — the
        /// outside-press dismissal check shared by every popup layer.</summary>
        public bool anyPointerPressed => pointerButtonsPressed != NowPointerButtons.None;

        public bool IsPointerDown(NowPointerButton button)
        {
            return IsSet(pointerButtonsDown, button);
        }

        public bool WasPointerPressed(NowPointerButton button)
        {
            return IsSet(pointerButtonsPressed, button);
        }

        public bool WasPointerReleased(NowPointerButton button)
        {
            return IsSet(pointerButtonsReleased, button);
        }

        public static NowPointerButtons ToButtonMask(bool value, NowPointerButton button)
        {
            return value ? ToButtonMask(button) : NowPointerButtons.None;
        }

        public static NowPointerButtons ToButtonMask(NowPointerButton button)
        {
            return (NowPointerButtons)(1 << (int)button);
        }

        static bool IsSet(NowPointerButtons buttons, NowPointerButton button)
        {
            return (buttons & ToButtonMask(button)) != 0;
        }
    }
}
