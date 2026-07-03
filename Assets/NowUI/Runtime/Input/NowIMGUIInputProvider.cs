using UnityEngine;

namespace NowUI
{
    public sealed class NowIMGUIInputProvider : INowInputProvider
    {
        public static readonly NowIMGUIInputProvider instance = new NowIMGUIInputProvider();

        NowPointerButtons _buttonsDown;

        bool _leftDown;

        bool _rightDown;

        bool _upDown;

        bool _downDown;

        bool _submitDown;

        bool _cancelDown;

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

            bool focusPreviousPressed = false;
            bool focusNextPressed = false;
            bool submitPressed = false;
            bool submitReleased = false;
            bool cancelPressed = false;
            bool cancelReleased = false;

            if (current.type == EventType.KeyDown)
            {
                ApplyKeyDown(
                    current,
                    ref focusPreviousPressed,
                    ref focusNextPressed,
                    ref submitPressed,
                    ref cancelPressed);
            }
            else if (current.type == EventType.KeyUp)
            {
                ApplyKeyUp(current, ref submitReleased, ref cancelReleased);
            }

            Vector2 delta = NowInput.ScaleScreenDelta(current.delta, surface);
            Vector2 scrollDelta = current.type == EventType.ScrollWheel
                ? new Vector2(current.delta.x, -current.delta.y) / 3f
                : Vector2.zero;

            bool inside = position.x >= 0f && position.y >= 0f &&
                position.x <= surface.size.x && position.y <= surface.size.y;
            NowPointerArbiter.Claim(
                this,
                NowPointerArbiter.TierCanvas,
                0f,
                inside,
                _buttonsDown != NowPointerButtons.None);

            snapshot = new NowInputSnapshot(
                NowPointerArbiter.IsOwner(this),
                position,
                position - delta,
                delta,
                _buttonsDown,
                pressed,
                released,
                scrollDelta,
                ReadNavigation(),
                focusPreviousPressed,
                focusNextPressed,
                _submitDown,
                submitPressed,
                submitReleased,
                _cancelDown,
                cancelPressed,
                cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
            return true;
        }

        void ApplyKeyDown(
            Event current,
            ref bool focusPreviousPressed,
            ref bool focusNextPressed,
            ref bool submitPressed,
            ref bool cancelPressed)
        {
            var navigationKeys = NowInput.navigationKeys;

            switch (current.keyCode)
            {
                case KeyCode.LeftArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.A when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _leftDown = true;
                    break;
                case KeyCode.RightArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.D when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _rightDown = true;
                    break;
                case KeyCode.UpArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.W when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _upDown = true;
                    break;
                case KeyCode.DownArrow when (navigationKeys & NowNavigationKeys.Arrows) != 0:
                case KeyCode.S when (navigationKeys & NowNavigationKeys.Wasd) != 0:
                    _downDown = true;
                    break;
                case KeyCode.Tab when (navigationKeys & NowNavigationKeys.TabFocus) != 0:
                    if (current.shift)
                        focusPreviousPressed = true;
                    else
                        focusNextPressed = true;
                    break;
                case KeyCode.Return when (navigationKeys & NowNavigationKeys.EnterSubmit) != 0:
                case KeyCode.KeypadEnter when (navigationKeys & NowNavigationKeys.EnterSubmit) != 0:
                case KeyCode.Space when (navigationKeys & NowNavigationKeys.SpaceSubmit) != 0:
                    if (!_submitDown)
                        submitPressed = true;

                    _submitDown = true;
                    break;
                case KeyCode.Escape:
                    if (!_cancelDown)
                        cancelPressed = true;

                    _cancelDown = true;
                    break;
            }
        }

        void ApplyKeyUp(Event current, ref bool submitReleased, ref bool cancelReleased)
        {
            switch (current.keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = false;
                    break;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = false;
                    break;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = false;
                    break;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = false;
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    if (_submitDown)
                    {
                        _submitDown = false;
                        submitReleased = true;
                    }

                    break;
                case KeyCode.Escape:
                    if (_cancelDown)
                    {
                        _cancelDown = false;
                        cancelReleased = true;
                    }

                    break;
            }
        }

        Vector2 ReadNavigation()
        {
            float x = 0f;
            float y = 0f;

            if (_leftDown)
                x -= 1f;

            if (_rightDown)
                x += 1f;

            if (_downDown)
                y -= 1f;

            if (_upDown)
                y += 1f;

            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
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
