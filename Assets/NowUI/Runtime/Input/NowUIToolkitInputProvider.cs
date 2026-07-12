using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Event-buffered input provider used by <see cref="NowVisualElement"/> and
    /// available for tests/custom UI Toolkit hosts.
    /// </summary>
    public sealed class NowUIToolkitInputProvider : INowInputProvider
    {
        int _lastFrame = -1;

        NowInputSnapshot _snapshot;

        bool _hasPointer;

        Vector2 _pointerPosition;

        Vector2 _previousPointerPosition;

        NowPointerButtons _pointerButtonsDown;

        NowPointerButtons _pointerButtonsPressed;

        NowPointerButtons _pointerButtonsReleased;

        Vector2 _scrollDelta;

        Vector2 _navigation;

        bool _navigationTransient;

        bool _focusPreviousPressed;

        bool _focusNextPressed;

        bool _submitDown;

        bool _submitPressed;

        bool _submitReleased;

        bool _cancelDown;

        bool _cancelPressed;

        bool _cancelReleased;

        bool _leftDown;

        bool _rightDown;

        bool _upDown;

        bool _downDown;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            if (_lastFrame != Time.frameCount)
            {
                _lastFrame = Time.frameCount;
                _snapshot = BuildSnapshot();
                ClearTransient(_snapshot);
            }

            snapshot = _snapshot;
            return true;
        }

        /// <summary>
        /// Forces the next read to rebuild the snapshot and consume buffered
        /// events; used by tests and custom hosts where frameCount is static.
        /// </summary>
        public void Invalidate()
        {
            _lastFrame = -1;
        }

        NowInputSnapshot BuildSnapshot()
        {
            NowPointerArbiter.Claim(
                this,
                NowPointerArbiter.TierCanvas,
                0f,
                _hasPointer,
                _pointerButtonsDown != NowPointerButtons.None);

            bool hasPointer = _hasPointer && NowPointerArbiter.IsOwner(this);
            Vector2 delta = hasPointer ? _pointerPosition - _previousPointerPosition : default;

            return new NowInputSnapshot(
                hasPointer,
                _pointerPosition,
                _previousPointerPosition,
                delta,
                _pointerButtonsDown,
                _pointerButtonsPressed,
                _pointerButtonsReleased,
                _scrollDelta,
                _navigation,
                _focusPreviousPressed,
                _focusNextPressed,
                _submitDown,
                _submitPressed,
                _submitReleased,
                _cancelDown,
                _cancelPressed,
                _cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }

        public void SetPointerPosition(Vector2 position)
        {
            SetPointerPosition(position, (int)_pointerButtonsDown);
        }

        public void SetPointerPosition(Vector2 position, int pressedButtons)
        {
            if (!_hasPointer)
                _previousPointerPosition = position;

            _hasPointer = true;
            _pointerPosition = position;
            _pointerButtonsDown = ToButtonMask(pressedButtons);
        }

        public void SetPointerDown(Vector2 position, int button, int pressedButtons)
        {
            SetPointerPosition(position, pressedButtons);

            if (TryGetButton(button, out var pointerButton))
            {
                var mask = NowInputSnapshot.ToButtonMask(pointerButton);
                _pointerButtonsDown |= mask;
                _pointerButtonsPressed |= mask;
            }
        }

        public void SetPointerUp(Vector2 position, int button, int pressedButtons)
        {
            SetPointerPosition(position, pressedButtons);

            if (TryGetButton(button, out var pointerButton))
            {
                var mask = NowInputSnapshot.ToButtonMask(pointerButton);
                _pointerButtonsDown &= ~mask;
                _pointerButtonsReleased |= mask;
            }
        }

        public void CancelPointer()
        {
            _pointerButtonsReleased |= _pointerButtonsDown;
            _pointerButtonsDown = NowPointerButtons.None;
            _hasPointer = false;
        }

        public void ClearPointer()
        {
            _hasPointer = false;
        }

        /// <summary>
        /// Accumulates scroll from a UI Toolkit <c>WheelEvent.delta</c>
        /// (down-positive, roughly three units per wheel notch), normalizing it
        /// to the canonical snapshot unit: notches with +y scrolling up.
        /// </summary>
        public void AddScrollDelta(Vector2 delta)
        {
            _scrollDelta += new Vector2(delta.x, -delta.y) / 3f;
        }

        public void SetNavigation(Vector2 navigation)
        {
            _navigation = Vector2.ClampMagnitude(navigation, 1f);
            _navigationTransient = true;
        }

        public void PressSubmit()
        {
            _submitDown = true;
            _submitPressed = true;
            _submitReleased = true;
        }

        public void PressCancel()
        {
            _cancelDown = true;
            _cancelPressed = true;
            _cancelReleased = true;
        }

        public bool KeyDown(KeyCode keyCode)
        {
            return KeyDown(keyCode, false);
        }

        public bool KeyDown(KeyCode keyCode, bool shift)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = true;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _submitDown = true;
                    _submitPressed = true;
                    return true;
                case KeyCode.Escape:
                    _cancelDown = true;
                    _cancelPressed = true;
                    return true;
                case KeyCode.Tab:
                    if (shift)
                        _focusPreviousPressed = true;
                    else
                        _focusNextPressed = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool KeyUp(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    _leftDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    _rightDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.UpArrow:
                case KeyCode.W:
                    _upDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    _downDown = false;
                    UpdateKeyNavigation();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _submitDown = false;
                    _submitReleased = true;
                    return true;
                case KeyCode.Escape:
                    _cancelDown = false;
                    _cancelReleased = true;
                    return true;
                default:
                    return false;
            }
        }

        public void Reset()
        {
            _lastFrame = -1;
            _snapshot = default;
            _hasPointer = false;
            _pointerPosition = default;
            _previousPointerPosition = default;
            _pointerButtonsDown = NowPointerButtons.None;
            _pointerButtonsPressed = NowPointerButtons.None;
            _pointerButtonsReleased = NowPointerButtons.None;
            _scrollDelta = default;
            _navigation = default;
            _navigationTransient = false;
            _focusPreviousPressed = false;
            _focusNextPressed = false;
            _submitDown = false;
            _submitPressed = false;
            _submitReleased = false;
            _cancelDown = false;
            _cancelPressed = false;
            _cancelReleased = false;
            _leftDown = false;
            _rightDown = false;
            _upDown = false;
            _downDown = false;
        }

        void ClearTransient(in NowInputSnapshot snapshot)
        {
            _previousPointerPosition = snapshot.pointerPosition;
            _pointerButtonsPressed = NowPointerButtons.None;
            _pointerButtonsReleased = NowPointerButtons.None;
            _scrollDelta = default;
            _focusPreviousPressed = false;
            _focusNextPressed = false;
            _submitPressed = false;
            _cancelPressed = false;

            if (_submitReleased)
            {
                _submitDown = false;
                _submitReleased = false;
            }

            if (_cancelReleased)
            {
                _cancelDown = false;
                _cancelReleased = false;
            }

            if (_navigationTransient)
            {
                _navigation = default;
                _navigationTransient = false;
            }
        }

        void UpdateKeyNavigation()
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

            _navigation = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            _navigationTransient = false;
        }

        static NowPointerButtons ToButtonMask(int pressedButtons)
        {
            NowPointerButtons buttons = NowPointerButtons.None;

            if ((pressedButtons & 1) != 0)
                buttons |= NowPointerButtons.Primary;

            if ((pressedButtons & 2) != 0)
                buttons |= NowPointerButtons.Secondary;

            if ((pressedButtons & 4) != 0)
                buttons |= NowPointerButtons.Middle;

            if ((pressedButtons & 8) != 0)
                buttons |= NowPointerButtons.Back;

            if ((pressedButtons & 16) != 0)
                buttons |= NowPointerButtons.Forward;

            return buttons;
        }

        static bool TryGetButton(int button, out NowPointerButton pointerButton)
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
