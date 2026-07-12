using UnityEngine;

namespace NowUI
{
    public sealed class NowScreenInputProvider : INowInputProvider
    {
        public static readonly NowScreenInputProvider instance = new NowScreenInputProvider();

        /// <summary>
        /// When true (default), the pointer is withheld while it is over raycastable
        /// UGUI — canvas UI draws above the camera-rendered screen path, so it
        /// should occlude NowUI the same way NowUI's raycastTarget occludes UGUI.
        /// In-flight presses and releases always come through so drags never strand.
        /// </summary>
        public bool blockedWhenPointerOverUGUI = true;

        int _lastFrame = -1;

        bool _hasRawPosition;

        Vector2 _rawPosition;

        NowPointerButtons _pointerButtonsDown;

        NowPointerButtons _pointerButtonsPressed;

        NowPointerButtons _pointerButtonsReleased;

        Vector2 _scrollDelta;

        Vector2 _navigation;

        bool _focusPreviousPressed;

        bool _focusNextPressed;

        bool _submitDown;

        bool _submitPressed;

        bool _submitReleased;

        bool _cancelDown;

        bool _cancelPressed;

        bool _cancelReleased;

        bool _rawInputAvailable = true;

        int _snapshotFrame = -1;

        NowInputSurface _snapshotSurface;

        NowInputSnapshot _snapshot;

        bool _snapshotAvailable;

        /// <summary>
        /// Serves the frame's snapshot from a per-(frame, surface) cache like
        /// <see cref="NowRectTransformInputProvider"/> does, so repeated queries
        /// (measure pass, draw pass, repaint-tracker polls) skip the arbiter
        /// footprint scan and overlay checks.
        /// </summary>
        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            int frame = Time.frameCount;

            if (_snapshotFrame == frame &&
                _snapshotSurface.size.x == surface.size.x &&
                _snapshotSurface.size.y == surface.size.y &&
                _snapshotSurface.screenRect == surface.screenRect)
            {
                snapshot = _snapshot;
                return _snapshotAvailable;
            }

            _snapshotFrame = frame;
            _snapshotSurface = surface;
            _snapshotAvailable = BuildSnapshot(surface, out _snapshot);
            snapshot = _snapshot;
            return _snapshotAvailable;
        }

        bool BuildSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            if (!TryUpdateRawInput())
            {
                snapshot = default;
                return false;
            }

            Vector2 position = default;
            Vector2 delta = default;

            if (_hasRawPosition)
            {
                if (!NowInput.TryScreenToSurface(_rawPosition, surface, out position))
                {
                    snapshot = default;
                    return false;
                }

                delta = NowInput.ScaleScreenDelta(_rawDelta, surface);
            }

            bool hitContent = _hasRawPosition &&
                (NowPointerArbiter.HadContentAt(this, position) ||
                 NowOverlay.IsPointerInsideOverlay(position));
            NowPointerArbiter.Claim(
                this,
                NowPointerArbiter.TierScreen,
                0f,
                hitContent,
                _pointerButtonsDown != NowPointerButtons.None);

            snapshot = new NowInputSnapshot(
                _hasRawPosition && NowPointerArbiter.IsOwner(this),
                position,
                position - delta,
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
            return true;
        }

        bool TryUpdateRawInput()
        {
            int frame = Time.frameCount;

            if (_lastFrame == frame)
                return _rawInputAvailable;

            _lastFrame = frame;

            if (!NowMouseInput.TryGet(out var mouseInput))
            {
                _hasRawPosition = false;
                _pointerButtonsDown = NowPointerButtons.None;
                _pointerButtonsPressed = NowPointerButtons.None;
                _pointerButtonsReleased = NowPointerButtons.None;
                _scrollDelta = default;
                _navigation = default;
                _focusPreviousPressed = false;
                _focusNextPressed = false;
                _submitDown = false;
                _submitPressed = false;
                _submitReleased = false;
                _cancelDown = false;
                _cancelPressed = false;
                _cancelReleased = false;
                _rawInputAvailable = false;
                return _rawInputAvailable;
            }

            bool buttonsWereDown = _previousButtonsDown != NowPointerButtons.None;
            bool allowedNow = !blockedWhenPointerOverUGUI ||
                !mouseInput.hasPointer ||
                !NowRaycastGate.IsPointerOverUGUI(mouseInput.screenPosition);
            bool pointerVisible = mouseInput.hasPointer &&
                NowRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow);

            if (pointerVisible)
            {
                var nextPosition = new Vector2(mouseInput.screenPosition.x, Screen.height - mouseInput.screenPosition.y);
                bool sameSource = _hasRawPosition && _pointerSource == mouseInput.pointerSource;
                _rawDelta = sameSource ? nextPosition - _rawPosition : Vector2.zero;
                _rawPosition = nextPosition;
                _pointerSource = mouseInput.pointerSource;
                _hasRawPosition = true;
            }
            else
            {
                _rawDelta = default;
                _hasRawPosition = false;
            }

            _previousButtonsDown = mouseInput.pointerButtonsDown;

            _pointerButtonsDown = mouseInput.pointerButtonsDown;
            _pointerButtonsPressed = mouseInput.pointerButtonsPressed;
            _pointerButtonsReleased = mouseInput.pointerButtonsReleased;
            _scrollDelta = mouseInput.scrollDelta;
            _navigation = mouseInput.navigation;
            _focusPreviousPressed = mouseInput.focusPreviousPressed;
            _focusNextPressed = mouseInput.focusNextPressed;
            _submitDown = mouseInput.submitDown;
            _submitPressed = mouseInput.submitPressed;
            _submitReleased = mouseInput.submitReleased;
            _cancelDown = mouseInput.cancelDown;
            _cancelPressed = mouseInput.cancelPressed;
            _cancelReleased = mouseInput.cancelReleased;
            _rawInputAvailable = true;
            return _rawInputAvailable;
        }

        Vector2 _rawDelta;

        /// <summary>Which physical source produced <see cref="_rawPosition"/>;
        /// deltas reset when the pointer hops devices (mouse to touch, or one
        /// touch to another) so the jump never registers as movement.</summary>
        int _pointerSource = NowMouseInput.MousePointerSource;

        NowPointerButtons _previousButtonsDown;

        bool _pressAllowed = true;
    }
}
