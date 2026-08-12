using UnityEngine;

namespace NowUI
{
    public sealed class NowRectTransformInputProvider : INowInputProvider, INowSurfaceToScreenMapper
    {
        RectTransform _rectTransform;

        Camera _eventCamera;

        int _lastFrame = -1;

        bool _hasPreviousPosition;

        Vector2 _previousPosition;

        NowInputSnapshot _snapshot;
        bool _rawInputAvailable;

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
            int frame = Time.frameCount;

            if (_lastFrame != frame)
            {
                _lastFrame = frame;

                if (NowMouseInput.TryGet(out var input))
                    _rawInputAvailable = TryGetSnapshot(surface, input, out _snapshot);
                else
                {
                    _snapshot = default;
                    _rawInputAvailable = false;
                }
            }

            snapshot = _snapshot;
            return _rawInputAvailable;
        }

        public void ResetPosition()
        {
            _lastFrame = -1;
            _hasPreviousPosition = false;
            _previousPosition = default;
            _snapshot = default;
            _rawInputAvailable = false;
            _previousButtonsDown = NowPointerButtons.None;
            _pressAllowed = true;
            NowInputSystemInput.Invalidate();
        }

        /// <summary>
        /// Projects a top-left-origin surface point through the configured
        /// RectTransform into upper-left-origin player-window pixels.
        /// </summary>
        public bool TrySurfaceToScreen(
            NowInputSurface surface,
            Vector2 surfacePosition,
            out Vector2 screenPosition)
        {
            screenPosition = default;

            if (_rectTransform == null ||
                (_eventCamera != null && _eventCamera.targetTexture != null) ||
                !NowSurfaceToScreenMapper.IsFinite(surfacePosition) ||
                !NowSurfaceToScreenMapper.IsFinite(surface.size) ||
                surface.size.x <= 0f ||
                surface.size.y <= 0f)
            {
                return false;
            }

            Rect rect = _rectTransform.rect;
            var localPosition = new Vector3(
                rect.xMin + surfacePosition.x * rect.width / surface.size.x,
                rect.yMax - surfacePosition.y * rect.height / surface.size.y,
                0f);
            Vector2 bottomLeft = NowRectTransformProjection.WorldToScreenPoint(
                _eventCamera,
                _rectTransform.TransformPoint(localPosition));
            screenPosition = NowSurfaceToScreenMapper.BottomLeftToTopLeft(bottomLeft);
            return NowSurfaceToScreenMapper.IsFinite(screenPosition);
        }

        internal bool TryGetSnapshot(NowInputSurface surface, NowMouseInput mouseInput, out NowInputSnapshot snapshot)
        {
            if (_rectTransform == null)
            {
                _hasPreviousPosition = false;
                _previousButtonsDown = NowPointerButtons.None;
                snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return true;
            }

            if (!mouseInput.hasPointer)
            {
                _hasPreviousPosition = false;
                _previousButtonsDown = NowPointerButtons.None;
                snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return true;
            }

            bool buttonsWereDown = _previousButtonsDown != NowPointerButtons.None;
            _previousButtonsDown = mouseInput.pointerButtonsDown;

            if (!NowRectTransformProjection.ScreenPointToLocalPointInRectangle(
                    _rectTransform,
                    mouseInput.screenPosition,
                    _eventCamera,
                    out var localPosition))
            {
                _hasPreviousPosition = false;
                snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return true;
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
                snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return true;
            }

            Vector2 previousPosition = _hasPreviousPosition ? _previousPosition : position;
            Vector2 delta = position - previousPosition;

            _previousPosition = position;
            _hasPreviousPosition = true;

            snapshot = new NowInputSnapshot(
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
            return true;
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

    /// <summary>
    /// CoreModule-only equivalent of the two RectTransformUtility projections
    /// NowUI needs. Keeping the math here avoids forcing Unity's low-level UI
    /// module on projects that do not use a Canvas host.
    /// </summary>
    internal static class NowRectTransformProjection
    {
        public static Vector2 WorldToScreenPoint(Camera camera, Vector3 worldPoint)
        {
            if (camera == null)
                return new Vector2(worldPoint.x, worldPoint.y);

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
            return new Vector2(screenPoint.x, screenPoint.y);
        }

        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rectTransform,
            Vector2 screenPoint,
            Camera camera,
            out Vector2 localPoint)
        {
            localPoint = default;

            if (rectTransform == null)
                return false;

            Ray ray;

            if (camera != null)
            {
                ray = camera.ScreenPointToRay(screenPoint);
            }
            else
            {
                var origin = new Vector3(screenPoint.x, screenPoint.y, -100f);
                ray = new Ray(origin, Vector3.forward);
            }

            var plane = new Plane(rectTransform.rotation * Vector3.back, rectTransform.position);
            float distance = 0f;
            float originPlaneDot = Vector3.Dot(
                Vector3.Normalize(rectTransform.position - ray.origin),
                plane.normal);

            // Match RectTransformUtility: a ray whose origin is already on
            // the RectTransform plane succeeds at distance zero. Plane.Raycast
            // alone reports that case as no forward intersection.
            if (originPlaneDot != 0f && !plane.Raycast(ray, out distance))
                return false;

            Vector3 local = rectTransform.InverseTransformPoint(ray.GetPoint(distance));
            localPoint = new Vector2(local.x, local.y);
            return true;
        }
    }
}
