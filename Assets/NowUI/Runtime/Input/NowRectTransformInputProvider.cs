using UnityEngine;

namespace NowUI
{
    public sealed class NowRectTransformInputProvider : INowInputProvider
    {
        RectTransform _rectTransform;

        Camera _eventCamera;

        int _lastFrame = -1;

        bool _hasPreviousPosition;

        Vector2 _previousPosition;

        NowInputSnapshot _snapshot;

        NowPointerButtons _previousButtonsDown;

        bool _pressAllowed = true;

        /// <summary>
        /// When set (NowGraphic assigns its host graphic), the pointer is withheld
        /// unless the EventSystem's topmost raycast hit is this component or one of
        /// its children. UGUI drawn above the host occludes NowUI input, mirroring
        /// how the host's raycastTarget occludes UGUI beneath it. Host-owned NowUI
        /// overlays may extend outside the host rect and still receive input when
        /// only lower UGUI is under the pointer. The verdict latches at press time:
        /// drags that began on this host keep tracking and their release always
        /// arrives, while presses that began on occluding UGUI stay blocked through
        /// release.
        /// </summary>
        public Component raycastGate;

        public NowRectTransformInputProvider()
        {
        }

        public NowRectTransformInputProvider(RectTransform rectTransform, Camera eventCamera = null)
        {
            _rectTransform = rectTransform;
            _eventCamera = eventCamera;
        }

        public RectTransform rectTransform
        {
            get => _rectTransform;
            set
            {
                if (_rectTransform == value)
                    return;

                _rectTransform = value;
                ResetPosition();
            }
        }

        public Camera eventCamera
        {
            get => _eventCamera;
            set => _eventCamera = value;
        }

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
        {
            if (_lastFrame != Time.frameCount)
                UpdateSnapshot();

            snapshot = _snapshot;
            return snapshot.hasPointer;
        }

        public void ResetPosition()
        {
            _lastFrame = -1;
            _hasPreviousPosition = false;
            _previousPosition = default;
            _snapshot = default;
            NowInputSystemInput.Invalidate();
        }

        void UpdateSnapshot()
        {
            _lastFrame = Time.frameCount;

            if (!NowMouseInput.TryGet(out var mouseInput))
            {
                _snapshot = default;
                return;
            }

            if (_rectTransform == null)
            {
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

            if (!mouseInput.hasPointer)
            {
                _hasPreviousPosition = false;
                _previousButtonsDown = NowPointerButtons.None;
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

            bool buttonsWereDown = _previousButtonsDown != NowPointerButtons.None;
            _previousButtonsDown = mouseInput.pointerButtonsDown;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform,
                    mouseInput.screenPosition,
                    _eventCamera,
                    out var localPosition))
            {
                _hasPreviousPosition = false;
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

            Rect rect = _rectTransform.rect;
            var position = new Vector2(localPosition.x - rect.xMin, rect.yMax - localPosition.y);
            bool blockedByForeignOverlay = raycastGate != null &&
                NowOverlay.IsPointerBlockedByForeignOverlay(raycastGate, mouseInput.screenPosition);
            bool insideHostOverlay = raycastGate != null
                ? NowOverlay.IsPointerInsideOverlay(raycastGate, position)
                : NowOverlay.IsPointerInsideOverlay(position);
            bool allowedNow = !blockedByForeignOverlay &&
                (raycastGate == null ||
                    NowRaycastGate.IsPointerAllowed(raycastGate, mouseInput.screenPosition, insideHostOverlay));

            bool insideHost = position.x >= 0f && position.y >= 0f &&
                position.x <= rect.width && position.y <= rect.height;
            NowPointerArbiter.Claim(
                this,
                NowPointerArbiter.TierCanvas,
                0f,
                allowedNow && (insideHost || insideHostOverlay),
                mouseInput.pointerButtonsDown != NowPointerButtons.None);

            if (!NowRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow) ||
                !NowPointerArbiter.IsOwner(this))
            {
                _hasPreviousPosition = false;
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

            Vector2 previousPosition = _hasPreviousPosition ? _previousPosition : position;
            Vector2 delta = position - previousPosition;

            _previousPosition = position;
            _hasPreviousPosition = true;

            _snapshot = new NowInputSnapshot(
                true,
                position,
                previousPosition,
                delta,
                mouseInput.pointerButtonsDown,
                mouseInput.pointerButtonsPressed,
                mouseInput.pointerButtonsReleased,
                mouseInput.scrollDelta,
                mouseInput.navigation,
                mouseInput.focusPreviousPressed,
                mouseInput.focusNextPressed,
                mouseInput.submitDown,
                mouseInput.submitPressed,
                mouseInput.submitReleased,
                mouseInput.cancelDown,
                mouseInput.cancelPressed,
                mouseInput.cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }

        static NowInputSnapshot CreateNavigationOnlySnapshot(NowMouseInput input)
        {
            return new NowInputSnapshot(
                false,
                default,
                default,
                default,
                NowPointerButtons.None,
                NowPointerButtons.None,
                NowPointerButtons.None,
                default,
                input.navigation,
                input.focusPreviousPressed,
                input.focusNextPressed,
                input.submitDown,
                input.submitPressed,
                input.submitReleased,
                input.cancelDown,
                input.cancelPressed,
                input.cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }
    }
}
