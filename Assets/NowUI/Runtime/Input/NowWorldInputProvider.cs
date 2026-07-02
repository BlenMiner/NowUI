using UnityEngine;

namespace NowUI
{
    public sealed class NowWorldInputProvider : INowInputProvider
    {
        NowWorldGraphic _graphic;
        Transform _transform;
        Camera _camera;
        Vector2 _size = new Vector2(200f, 80f);
        Vector2 _pivot = new Vector2(0.5f, 0.5f);
        float _pixelsPerUnit = 100f;
        bool _acceptNavigation;
        int _lastFrame = -1;
        bool _hasPreviousPosition;
        Vector2 _previousPosition;
        NowInputSnapshot _snapshot;
        bool _rawInputAvailable;
        NowPointerButtons _previousButtonsDown;
        bool _pressAllowed = true;
        bool _blockedWhenPointerOverUGUI = true;

        public NowWorldGraphic graphic
        {
            get => _graphic;
            set
            {
                if (_graphic == value)
                    return;

                _graphic = value;
                ResetPosition();
            }
        }

        public Transform transform
        {
            get => _transform;
            set
            {
                if (_transform == value)
                    return;

                _transform = value;
                ResetPosition();
            }
        }

        public Camera camera
        {
            get => _camera;
            set => _camera = value;
        }

        public Vector2 size
        {
            get => _graphic ? _graphic.size : _size;
            set => _size = SanitizeSize(value);
        }

        public Vector2 pivot
        {
            get => _graphic ? _graphic.pivot : _pivot;
            set => _pivot = value;
        }

        public float pixelsPerUnit
        {
            get => _graphic ? _graphic.pixelsPerUnit : _pixelsPerUnit;
            set => _pixelsPerUnit = SanitizePixelsPerUnit(value);
        }

        public bool acceptNavigation
        {
            get => _graphic ? _graphic.acceptNavigation : _acceptNavigation;
            set => _acceptNavigation = value;
        }

        public bool blockedWhenPointerOverUGUI
        {
            get => _blockedWhenPointerOverUGUI;
            set => _blockedWhenPointerOverUGUI = value;
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

        internal bool TryGetSnapshot(NowInputSurface surface, NowMouseInput input, out NowInputSnapshot snapshot)
        {
            if (!input.hasPointer)
            {
                _hasPreviousPosition = false;
                _previousButtonsDown = NowPointerButtons.None;
                snapshot = CreateSnapshot(false, default, default, default, input);
                return true;
            }

            bool buttonsWereDown = _previousButtonsDown != NowPointerButtons.None;
            bool allowedNow = !blockedWhenPointerOverUGUI ||
                !NowRaycastGate.IsPointerOverUGUI(input.screenPosition);
            _previousButtonsDown = input.pointerButtonsDown;

            if (!NowRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow))
            {
                _hasPreviousPosition = false;
                snapshot = CreateSnapshot(false, default, default, default, input);
                return true;
            }

            bool hit = TryScreenPointToSurface(input.screenPosition, out var position);
            bool inside = hit &&
                          position is { x: >= 0f, y: >= 0f } &&
                          position.x <= surface.size.x &&
                          position.y <= surface.size.y;
            bool hasPointer = hit && (inside ||
                input.pointerButtonsDown != NowPointerButtons.None ||
                input.pointerButtonsReleased != NowPointerButtons.None);

            var previous = _hasPreviousPosition ? _previousPosition : position;
            var delta = hit ? position - previous : default;

            if (hit)
            {
                _previousPosition = position;
                _hasPreviousPosition = true;
            }
            else switch (_hasPreviousPosition)
            {
                case true when
                    input.pointerButtonsReleased != NowPointerButtons.None:
                    position = _previousPosition;
                    previous = _previousPosition;
                    hasPointer = true;
                    _hasPreviousPosition = false;
                    break;
                case true when
                    input.pointerButtonsDown != NowPointerButtons.None &&
                    input.pointerButtonsPressed == NowPointerButtons.None:
                    position = _previousPosition;
                    previous = _previousPosition;
                    hasPointer = true;
                    break;
                default:
                    _hasPreviousPosition = false;
                    previous = default;
                    position = default;
                    break;
            }

            snapshot = CreateSnapshot(hasPointer, position, previous, delta, input);
            return true;
        }

        public bool TryScreenPointToSurface(Vector2 screenPosition, out Vector2 surfacePosition)
        {
            if (_graphic)
                return _graphic.TryScreenPointToSurface(screenPosition, out surfacePosition);

            surfacePosition = default;

            var targetTransform = _transform;
            var targetCamera = ResolveCamera();

            if (!targetTransform || !targetCamera)
                return false;

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(targetTransform.forward, targetTransform.position);

            if (!plane.Raycast(ray, out float distance))
                return false;

            var local = targetTransform.InverseTransformPoint(ray.GetPoint(distance));
            float ppu = SanitizePixelsPerUnit(_pixelsPerUnit);
            var targetSize = SanitizeSize(_size);
            surfacePosition = new Vector2(
                local.x * ppu + targetSize.x * _pivot.x,
                targetSize.y * (1f - _pivot.y) - local.y * ppu);
            return true;
        }

        public void ResetPosition()
        {
            _lastFrame = -1;
            _hasPreviousPosition = false;
            _previousPosition = default;
            _previousButtonsDown = NowPointerButtons.None;
            _pressAllowed = true;
            _snapshot = default;
        }

        NowInputSnapshot CreateSnapshot(
            bool hasPointer,
            Vector2 position,
            Vector2 previous,
            Vector2 delta,
            NowMouseInput input)
        {
            bool navigation = acceptNavigation;

            return new NowInputSnapshot(
                hasPointer,
                position,
                previous,
                delta,
                input.pointerButtonsDown,
                input.pointerButtonsPressed,
                input.pointerButtonsReleased,
                input.scrollDelta,
                navigation ? input.navigation : Vector2.zero,
                navigation && input.focusPreviousPressed,
                navigation && input.focusNextPressed,
                navigation && input.submitDown,
                navigation && input.submitPressed,
                navigation && input.submitReleased,
                navigation && input.cancelDown,
                navigation && input.cancelPressed,
                navigation && input.cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }

        Camera ResolveCamera()
        {
            if (_camera)
                return _camera;

            return Camera.main;
        }

        static Vector2 SanitizeSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
        }

        static float SanitizePixelsPerUnit(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : 100f;
        }
    }
}
