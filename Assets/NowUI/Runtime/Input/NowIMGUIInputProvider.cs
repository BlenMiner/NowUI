using UnityEngine;

namespace NowUI
{
    public sealed class NowIMGUIInputProvider : INowInputProvider
    {
        public static readonly NowIMGUIInputProvider instance = new NowIMGUIInputProvider();

        NowPointerButtons _buttonsDown;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            Event current = Event.current;

            if (current == null)
            {
                snapshot = default;
                return false;
            }

            if (!NowInput.TryScreenToSurface(current.mousePosition, surface, out var position))
            {
                snapshot = default;
                return false;
            }

            NowPointerButtons pressed = NowPointerButtons.None;
            NowPointerButtons released = NowPointerButtons.None;

            if (TryGetIMGUIButton(current.button, out var button))
            {
                var buttonMask = NowInputSnapshot.ToButtonMask(button);

                if (current.type == EventType.MouseDown)
                {
                    pressed = buttonMask;
                    _buttonsDown |= buttonMask;
                }
                else if (current.type == EventType.MouseUp)
                {
                    released = buttonMask;
                    _buttonsDown &= ~buttonMask;
                }
                else if (current.type == EventType.MouseDrag)
                {
                    _buttonsDown |= buttonMask;
                }
            }

            if (current.type == EventType.MouseLeaveWindow)
                _buttonsDown = NowPointerButtons.None;

            Vector2 delta = NowInput.ScaleScreenDelta(current.delta, surface);
            Vector2 scrollDelta = current.type == EventType.ScrollWheel ? current.delta : Vector2.zero;

            snapshot = new NowInputSnapshot(
                true,
                position,
                position - delta,
                delta,
                _buttonsDown,
                pressed,
                released,
                scrollDelta,
                default,
                false,
                false,
                false,
                false,
                false,
                false,
                Time.frameCount,
                Time.realtimeSinceStartup);
            return true;
        }

        static bool TryGetIMGUIButton(int button, out NowPointerButton pointerButton)
        {
            switch (button)
            {
                case 0:
                    pointerButton = NowPointerButton.Primary;
                    return true;
                case 1:
                    pointerButton = NowPointerButton.Secondary;
                    return true;
                case 2:
                    pointerButton = NowPointerButton.Middle;
                    return true;
                case 3:
                    pointerButton = NowPointerButton.Back;
                    return true;
                case 4:
                    pointerButton = NowPointerButton.Forward;
                    return true;
                default:
                    pointerButton = default;
                    return false;
            }
        }
    }
}
