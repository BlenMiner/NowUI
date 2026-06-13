using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NowUI
{
    public interface INowInputProvider
    {
        bool TryGetSnapshot(NowInputSurface surface, out NowUIInputSnapshot snapshot);
    }

    public enum NowUIPointerButton
    {
        Primary = 0,
        Secondary = 1,
        Middle = 2,
        Back = 3,
        Forward = 4
    }

    [Flags]
    public enum NowUIPointerButtons
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

    public struct NowUIInputSnapshot
    {
        public readonly bool hasPointer;

        public Vector2 pointerPosition;

        public Vector2 previousPointerPosition;

        public Vector2 pointerDelta;

        public bool primaryDown;

        public bool primaryPressed;

        public bool primaryReleased;

        public readonly NowUIPointerButtons pointerButtonsDown;

        public readonly NowUIPointerButtons pointerButtonsPressed;

        public readonly NowUIPointerButtons pointerButtonsReleased;

        public Vector2 scrollDelta;

        public Vector2 navigation;

        public readonly bool submitDown;

        public readonly bool submitPressed;

        public bool submitReleased;

        public bool cancelDown;

        public bool cancelPressed;

        public bool cancelReleased;

        public int frame;

        public float time;

        public NowUIInputSnapshot(
            Vector2 pointerPosition,
            bool primaryDown,
            bool primaryPressed,
            bool primaryReleased)
            : this(
                true,
                pointerPosition,
                pointerPosition,
                Vector2.zero,
                ToButtonMask(primaryDown, NowUIPointerButton.Primary),
                ToButtonMask(primaryPressed, NowUIPointerButton.Primary),
                ToButtonMask(primaryReleased, NowUIPointerButton.Primary),
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

        public NowUIInputSnapshot(
            Vector2 pointerPosition,
            NowUIPointerButtons pointerButtonsDown,
            NowUIPointerButtons pointerButtonsPressed,
            NowUIPointerButtons pointerButtonsReleased)
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

        public NowUIInputSnapshot(
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
                ToButtonMask(primaryDown, NowUIPointerButton.Primary),
                ToButtonMask(primaryPressed, NowUIPointerButton.Primary),
                ToButtonMask(primaryReleased, NowUIPointerButton.Primary),
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

        public NowUIInputSnapshot(
            Vector2 pointerPosition,
            Vector2 pointerDelta,
            NowUIPointerButtons pointerButtonsDown,
            NowUIPointerButtons pointerButtonsPressed,
            NowUIPointerButtons pointerButtonsReleased)
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

        public NowUIInputSnapshot(
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
                ToButtonMask(primaryDown, NowUIPointerButton.Primary),
                ToButtonMask(primaryPressed, NowUIPointerButton.Primary),
                ToButtonMask(primaryReleased, NowUIPointerButton.Primary),
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

        public NowUIInputSnapshot(
            bool hasPointer,
            Vector2 pointerPosition,
            Vector2 previousPointerPosition,
            Vector2 pointerDelta,
            NowUIPointerButtons pointerButtonsDown,
            NowUIPointerButtons pointerButtonsPressed,
            NowUIPointerButtons pointerButtonsReleased,
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
        {
            this.hasPointer = hasPointer;
            this.pointerPosition = pointerPosition;
            this.previousPointerPosition = previousPointerPosition;
            this.pointerDelta = pointerDelta;
            this.pointerButtonsDown = pointerButtonsDown;
            this.pointerButtonsPressed = pointerButtonsPressed;
            this.pointerButtonsReleased = pointerButtonsReleased;
            primaryDown = IsSet(pointerButtonsDown, NowUIPointerButton.Primary);
            primaryPressed = IsSet(pointerButtonsPressed, NowUIPointerButton.Primary);
            primaryReleased = IsSet(pointerButtonsReleased, NowUIPointerButton.Primary);
            this.scrollDelta = scrollDelta;
            this.navigation = navigation;
            this.submitDown = submitDown;
            this.submitPressed = submitPressed;
            this.submitReleased = submitReleased;
            this.cancelDown = cancelDown;
            this.cancelPressed = cancelPressed;
            this.cancelReleased = cancelReleased;
            this.frame = frame;
            this.time = time;
        }

        public bool IsPointerDown(NowUIPointerButton button)
        {
            return IsSet(pointerButtonsDown, button);
        }

        public bool WasPointerPressed(NowUIPointerButton button)
        {
            return IsSet(pointerButtonsPressed, button);
        }

        public bool WasPointerReleased(NowUIPointerButton button)
        {
            return IsSet(pointerButtonsReleased, button);
        }

        public static NowUIPointerButtons ToButtonMask(bool value, NowUIPointerButton button)
        {
            return value ? ToButtonMask(button) : NowUIPointerButtons.None;
        }

        public static NowUIPointerButtons ToButtonMask(NowUIPointerButton button)
        {
            return (NowUIPointerButtons)(1 << (int)button);
        }

        static bool IsSet(NowUIPointerButtons buttons, NowUIPointerButton button)
        {
            return (buttons & ToButtonMask(button)) != 0;
        }
    }

    public readonly struct NowInteraction
    {
        public readonly int id;

        public readonly Rect rect;

        public readonly NowUIPointerButton button;

        public readonly bool hasPointer;

        public readonly Vector2 pointerPosition;

        public readonly Vector2 pointerDelta;

        public readonly Vector2 dragDelta;

        public readonly bool hovered;

        public readonly bool pressed;

        public readonly bool held;

        public readonly bool released;

        public readonly bool clicked;

        public readonly bool active;

        public readonly bool dragging;

        public readonly bool dragStarted;

        public readonly bool dragEnded;

        internal NowInteraction(
            int id,
            Rect rect,
            NowUIPointerButton button,
            bool hasPointer,
            Vector2 pointerPosition,
            Vector2 pointerDelta,
            Vector2 dragDelta,
            bool hovered,
            bool pressed,
            bool held,
            bool released,
            bool clicked,
            bool active,
            bool dragging,
            bool dragStarted,
            bool dragEnded)
        {
            this.id = id;
            this.rect = rect;
            this.button = button;
            this.hasPointer = hasPointer;
            this.pointerPosition = pointerPosition;
            this.pointerDelta = pointerDelta;
            this.dragDelta = dragDelta;
            this.hovered = hovered;
            this.pressed = pressed;
            this.held = held;
            this.released = released;
            this.clicked = clicked;
            this.active = active;
            this.dragging = dragging;
            this.dragStarted = dragStarted;
            this.dragEnded = dragEnded;
        }
    }

    public static class NowInput
    {
        const float DefaultDragThreshold = 4f;

        static INowInputProvider _defaultProvider = NowScreenInputProvider.instance;

        static NowInputSurface _surface;

        static NowUIInputSnapshot _snapshot;

        static bool _hasContext;

        static int _activeId;

        static NowUIPointerButton _activeButton;

        static int _dragId;

        static bool _activeDragged;

        static Vector2 _pressPosition;

        static float _dragThreshold = DefaultDragThreshold;

        static int _passiveDepth;

        public static INowInputProvider defaultProvider
        {
            get => _defaultProvider;
            set => _defaultProvider = value;
        }

        public static NowInputSurface surface => _surface;

        public static NowUIInputSnapshot current => _snapshot;

        public static bool hasContext => _hasContext;

        public static int activeId => _activeId;

        public static NowUIPointerButton activeButton => _activeButton;

        public static float dragThreshold
        {
            get => _dragThreshold;
            set => _dragThreshold = Mathf.Max(0f, value);
        }

        public static NowUIInputScope Begin(Vector2 size)
        {
            return Begin(_defaultProvider, new NowInputSurface(size));
        }

        public static NowUIInputScope Begin(Vector2 size, Rect screenRect)
        {
            return Begin(_defaultProvider, new NowInputSurface(size, screenRect));
        }

        public static NowUIInputScope Begin(INowInputProvider provider, Vector2 size)
        {
            return Begin(provider, new NowInputSurface(size));
        }

        public static NowUIInputScope Begin(INowInputProvider provider, NowInputSurface surface)
        {
            var scope = new NowUIInputScope(_surface, _snapshot, _hasContext);
            Update(provider, surface);
            return scope;
        }

        public static void Update(Vector2 size)
        {
            Update(_defaultProvider, new NowInputSurface(size));
        }

        public static void Update(NowInputSurface surface)
        {
            Update(_defaultProvider, surface);
        }

        public static void Update(INowInputProvider provider, NowInputSurface surface)
        {
            _surface = surface;
            _hasContext = true;
            NowControls.ResetControlIdOccurrences();

            if (provider != null && provider.TryGetSnapshot(surface, out _snapshot))
                return;

            _snapshot = default;
        }

        public static bool IsHovered(NowRect rect)
        {
            return IsHovered((Rect)rect);
        }

        public static bool IsHovered(Rect rect)
        {
            return _hasContext && _snapshot.hasPointer &&
                rect.Contains(_snapshot.pointerPosition) &&
                !NowUIOverlay.IsPointerBlocked(_snapshot.pointerPosition);
        }

        public static bool IsPointerDown(NowUIPointerButton button)
        {
            return _hasContext && _snapshot.IsPointerDown(button);
        }

        public static bool WasPointerPressed(NowUIPointerButton button)
        {
            return _hasContext && _snapshot.WasPointerPressed(button);
        }

        public static bool WasPointerReleased(NowUIPointerButton button)
        {
            return _hasContext && _snapshot.WasPointerReleased(button);
        }

        /// <summary>
        /// Combines two ids into one (a parent control id and a sub-element
        /// index, for example) — the blessed way to mint ids for interactive
        /// sub-regions without strings. Never returns 0.
        /// </summary>
        public static int CombineId(int a, int b)
        {
            unchecked
            {
                int id = (a * 397) ^ b;
                return id != 0 ? id : 1;
            }
        }

        /// <summary>
        /// Interaction with no id at all: identity comes from the call site, and
        /// repeated calls from one site (a loop over sub-elements) are salted by
        /// per-frame occurrence — draw-order stable, like control identity. Use
        /// an explicit id instead when looped items can reorder mid-press.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return Interact(NowControls.GetControlId(NowControls.SiteId(file, line)), rect);
        }

        public static NowInteraction Interact(string id, NowRect rect)
        {
            return Interact(id, rect, NowUIPointerButton.Primary);
        }

        public static NowInteraction Interact(string id, NowRect rect, NowUIPointerButton button)
        {
            return Interact(GetId(id), (Rect)rect, button);
        }

        public static NowInteraction Interact(string id, Rect rect)
        {
            return Interact(id, rect, NowUIPointerButton.Primary);
        }

        public static NowInteraction Interact(string id, Rect rect, NowUIPointerButton button)
        {
            return Interact(GetId(id), rect, button);
        }

        public static NowInteraction Interact(int id, NowRect rect)
        {
            return Interact(id, rect, NowUIPointerButton.Primary);
        }

        public static NowInteraction Interact(int id, NowRect rect, NowUIPointerButton button)
        {
            return Interact(id, (Rect)rect, button);
        }

        public static NowInteraction Interact(int id, Rect rect)
        {
            return Interact(id, rect, NowUIPointerButton.Primary);
        }

        public static NowInteraction Interact(int id, Rect rect, NowUIPointerButton button)
        {
            if (id == 0)
                throw new ArgumentException("Control id 0 is reserved.", nameof(id));

            var snapshot = _snapshot;
            bool hasPointer = _hasContext && snapshot.hasPointer;
            bool hovered = hasPointer && rect.Contains(snapshot.pointerPosition) &&
                !NowUIOverlay.IsPointerBlocked(snapshot.pointerPosition);

            if (_passiveDepth > 0)
            {
                return new NowInteraction(
                    id,
                    rect,
                    button,
                    hasPointer,
                    snapshot.pointerPosition,
                    snapshot.pointerDelta,
                    default,
                    hovered,
                    false,
                    false,
                    false,
                    false,
                    _activeId == id && _activeButton == button,
                    false,
                    false,
                    false);
            }

            bool pressed =
                hovered &&
                snapshot.WasPointerPressed(button) &&
                (_activeId == 0 || (_activeId == id && _activeButton == button));

            if (pressed)
            {
                _activeId = id;
                _activeButton = button;
                _dragId = 0;
                _activeDragged = false;
                _pressPosition = snapshot.pointerPosition;
            }

            bool active = _activeId == id && _activeButton == button;
            bool held = active && snapshot.IsPointerDown(button);
            bool released = active && snapshot.WasPointerReleased(button);
            bool dragging = false;
            bool dragStarted = false;
            bool dragEnded = false;
            Vector2 dragDelta = default;

            if (held)
            {
                Vector2 dragOffset = snapshot.pointerPosition - _pressPosition;

                if (_dragId == id || dragOffset.sqrMagnitude >= _dragThreshold * _dragThreshold)
                {
                    dragStarted = _dragId != id;
                    _dragId = id;
                    _activeDragged = true;
                    dragging = true;
                    dragDelta = snapshot.pointerDelta;
                }
            }

            if (released && _dragId == id)
            {
                dragEnded = true;
                dragDelta = snapshot.pointerDelta;
            }

            bool clicked = released && hovered && !_activeDragged;

            var interaction = new NowInteraction(
                id,
                rect,
                button,
                hasPointer,
                snapshot.pointerPosition,
                snapshot.pointerDelta,
                dragDelta,
                hovered,
                pressed,
                held,
                released,
                clicked,
                active,
                dragging,
                dragStarted,
                dragEnded);

            if (released)
                ClearActive();

            return interaction;
        }

        public static int GetId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Control id strings cannot be null or empty.", nameof(value));

            unchecked
            {
                const int offset = unchecked((int)2166136261u);
                const int prime = 16777619;
                int hash = offset;

                for (int i = 0; i < value.Length; ++i)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash != 0 ? hash : 1;
            }
        }

        public static int GetId(int seed, string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Control id strings cannot be null or empty.", nameof(value));

            unchecked
            {
                int hash = seed != 0 ? seed : unchecked((int)2166136261u);

                for (int i = 0; i < value.Length; ++i)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return hash != 0 ? hash : 1;
            }
        }

        public static void Reset()
        {
            _surface = default;
            _snapshot = default;
            _hasContext = false;
            _activeId = 0;
            _activeButton = NowUIPointerButton.Primary;
            _dragId = 0;
            _activeDragged = false;
            _pressPosition = default;
            _dragThreshold = DefaultDragThreshold;
            _defaultProvider = NowScreenInputProvider.instance;
            _passiveDepth = 0;
        }

        /// <summary>
        /// True during layout measure passes, when interactions are inert.
        /// Custom controls that read input outside <see cref="Interact"/> (raw
        /// snapshots, right-click checks) should stand down while this is set,
        /// or the measure pass double-processes their input.
        /// </summary>
        public static bool isPassive => _passiveDepth > 0;

        /// <summary>Enters passive mode (e.g. a layout measure pass): pure reads like hover
        /// still report so styling stays consistent, but press/drag state never transitions —
        /// the same control will interact for real later this frame.</summary>
        internal static void BeginPassive()
        {
            if (_passiveDepth == 0)
                NowControls.ResetPassiveControlIdOccurrences();

            ++_passiveDepth;
        }

        internal static void EndPassive()
        {
            if (_passiveDepth > 0)
                --_passiveDepth;
        }

        internal static bool TryScreenToSurface(Vector2 topLeftScreenPosition, NowInputSurface surface, out Vector2 position)
        {
            Rect screenRect = surface.screenRect;

            if (screenRect.width <= 0f || screenRect.height <= 0f || surface.size.x <= 0f || surface.size.y <= 0f)
            {
                position = default;
                return false;
            }

            position = new Vector2(
                (topLeftScreenPosition.x - screenRect.x) * surface.size.x / screenRect.width,
                (topLeftScreenPosition.y - screenRect.y) * surface.size.y / screenRect.height);
            return true;
        }

        internal static Vector2 ScaleScreenDelta(Vector2 topLeftScreenDelta, NowInputSurface surface)
        {
            Rect screenRect = surface.screenRect;

            if (screenRect.width <= 0f || screenRect.height <= 0f)
                return default;

            return new Vector2(
                topLeftScreenDelta.x * surface.size.x / screenRect.width,
                topLeftScreenDelta.y * surface.size.y / screenRect.height);
        }

        internal static void Restore(NowInputSurface surface, NowUIInputSnapshot snapshot, bool hasContext)
        {
            _surface = surface;
            _snapshot = snapshot;
            _hasContext = hasContext;
        }

        static Rect ToRect(Vector4 rect)
        {
            return new Rect(rect.x, rect.y, rect.z, rect.w);
        }

        static void ClearActive()
        {
            _activeId = 0;
            _activeButton = NowUIPointerButton.Primary;
            _dragId = 0;
            _activeDragged = false;
            _pressPosition = default;
        }
    }

    [NowScope]
    public struct NowUIInputScope : IDisposable
    {
        readonly NowInputSurface _previousSurface;

        readonly NowUIInputSnapshot _previousSnapshot;

        readonly bool _previousHasContext;

        bool _disposed;

        internal NowUIInputScope(
            NowInputSurface previousSurface,
            NowUIInputSnapshot previousSnapshot,
            bool previousHasContext)
        {
            _previousSurface = previousSurface;
            _previousSnapshot = previousSnapshot;
            _previousHasContext = previousHasContext;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowInput.Restore(_previousSurface, _previousSnapshot, _previousHasContext);
        }
    }

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

        NowUIPointerButtons _pointerButtonsDown;

        NowUIPointerButtons _pointerButtonsPressed;

        NowUIPointerButtons _pointerButtonsReleased;

        Vector2 _scrollDelta;

        Vector2 _navigation;

        bool _submitDown;

        bool _submitPressed;

        bool _submitReleased;

        bool _cancelDown;

        bool _cancelPressed;

        bool _cancelReleased;

        bool _rawInputAvailable = true;

        public bool TryGetSnapshot(NowInputSurface surface, out NowUIInputSnapshot snapshot)
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

            snapshot = new NowUIInputSnapshot(
                _hasRawPosition,
                position,
                position - delta,
                delta,
                _pointerButtonsDown,
                _pointerButtonsPressed,
                _pointerButtonsReleased,
                _scrollDelta,
                _navigation,
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

            if (!NowUIMouseInput.TryGet(out var mouseInput))
            {
                _hasRawPosition = false;
                _pointerButtonsDown = NowUIPointerButtons.None;
                _pointerButtonsPressed = NowUIPointerButtons.None;
                _pointerButtonsReleased = NowUIPointerButtons.None;
                _scrollDelta = default;
                _navigation = default;
                _submitDown = false;
                _submitPressed = false;
                _submitReleased = false;
                _cancelDown = false;
                _cancelPressed = false;
                _cancelReleased = false;
                _rawInputAvailable = false;
                return _rawInputAvailable;
            }

            bool buttonsWereDown = _previousButtonsDown != NowUIPointerButtons.None;
            bool allowedNow = !blockedWhenPointerOverUGUI || !NowUIRaycastGate.IsPointerOverUGUI();
            bool pointerVisible = mouseInput.hasPointer &&
                NowUIRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow);

            if (pointerVisible)
            {
                var nextPosition = new Vector2(mouseInput.screenPosition.x, Screen.height - mouseInput.screenPosition.y);
                _rawDelta = _hasRawPosition ? nextPosition - _rawPosition : Vector2.zero;
                _rawPosition = nextPosition;
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

        NowUIPointerButtons _previousButtonsDown;

        bool _pressAllowed = true;
    }

    public sealed class NowIMGUIInputProvider : INowInputProvider
    {
        public static readonly NowIMGUIInputProvider instance = new NowIMGUIInputProvider();

        NowUIPointerButtons _buttonsDown;

        public bool TryGetSnapshot(NowInputSurface surface, out NowUIInputSnapshot snapshot)
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

            NowUIPointerButtons pressed = NowUIPointerButtons.None;
            NowUIPointerButtons released = NowUIPointerButtons.None;

            if (TryGetIMGUIButton(current.button, out var button))
            {
                var buttonMask = NowUIInputSnapshot.ToButtonMask(button);

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
                _buttonsDown = NowUIPointerButtons.None;

            Vector2 delta = NowInput.ScaleScreenDelta(current.delta, surface);
            Vector2 scrollDelta = current.type == EventType.ScrollWheel ? current.delta : Vector2.zero;

            snapshot = new NowUIInputSnapshot(
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

        static bool TryGetIMGUIButton(int button, out NowUIPointerButton pointerButton)
        {
            switch (button)
            {
                case 0:
                    pointerButton = NowUIPointerButton.Primary;
                    return true;
                case 1:
                    pointerButton = NowUIPointerButton.Secondary;
                    return true;
                case 2:
                    pointerButton = NowUIPointerButton.Middle;
                    return true;
                case 3:
                    pointerButton = NowUIPointerButton.Back;
                    return true;
                case 4:
                    pointerButton = NowUIPointerButton.Forward;
                    return true;
                default:
                    pointerButton = default;
                    return false;
            }
        }
    }

    public sealed class NowRectTransformInputProvider : INowInputProvider
    {
        RectTransform _rectTransform;

        Camera _eventCamera;

        int _lastFrame = -1;

        bool _hasPreviousPosition;

        Vector2 _previousPosition;

        NowUIInputSnapshot _snapshot;

        NowUIPointerButtons _previousButtonsDown;

        bool _pressAllowed = true;

        /// <summary>
        /// When set (NowUIGraphic assigns its host graphic), the pointer is withheld
        /// unless the EventSystem's topmost raycast hit is this component or one of
        /// its children — UGUI drawn above the host occludes NowUI input, mirroring
        /// how the host's raycastTarget occludes UGUI beneath it. The verdict latches
        /// at press time: drags that began on this host keep tracking and their
        /// release always arrives, while presses that began on occluding UGUI stay
        /// blocked through release.
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

        public bool TryGetSnapshot(NowInputSurface surface, out NowUIInputSnapshot snapshot)
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
        }

        void UpdateSnapshot()
        {
            _lastFrame = Time.frameCount;

            if (!NowUIMouseInput.TryGet(out var mouseInput))
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
                _previousButtonsDown = NowUIPointerButtons.None;
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

            bool buttonsWereDown = _previousButtonsDown != NowUIPointerButtons.None;
            _previousButtonsDown = mouseInput.pointerButtonsDown;

            bool allowedNow = raycastGate == null ||
                NowUIRaycastGate.IsPointerAllowed(raycastGate, mouseInput.screenPosition);

            if (!NowUIRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow))
            {
                _hasPreviousPosition = false;
                _snapshot = CreateNavigationOnlySnapshot(mouseInput);
                return;
            }

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
            Vector2 previousPosition = _hasPreviousPosition ? _previousPosition : position;
            Vector2 delta = position - previousPosition;

            _previousPosition = position;
            _hasPreviousPosition = true;

            _snapshot = new NowUIInputSnapshot(
                true,
                position,
                previousPosition,
                delta,
                mouseInput.pointerButtonsDown,
                mouseInput.pointerButtonsPressed,
                mouseInput.pointerButtonsReleased,
                mouseInput.scrollDelta,
                mouseInput.navigation,
                mouseInput.submitDown,
                mouseInput.submitPressed,
                mouseInput.submitReleased,
                mouseInput.cancelDown,
                mouseInput.cancelPressed,
                mouseInput.cancelReleased,
                Time.frameCount,
                Time.realtimeSinceStartup);
        }

        static NowUIInputSnapshot CreateNavigationOnlySnapshot(NowUIMouseInput input)
        {
            return new NowUIInputSnapshot(
                false,
                default,
                default,
                default,
                NowUIPointerButtons.None,
                NowUIPointerButtons.None,
                NowUIPointerButtons.None,
                default,
                input.navigation,
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

    internal struct NowUIMouseInput
    {
        public bool hasPointer;

        public Vector2 screenPosition;

        public NowUIPointerButtons pointerButtonsDown;

        public NowUIPointerButtons pointerButtonsPressed;

        public NowUIPointerButtons pointerButtonsReleased;

        public Vector2 scrollDelta;

        public Vector2 navigation;

        public bool submitDown;

        public bool submitPressed;

        public bool submitReleased;

        public bool cancelDown;

        public bool cancelPressed;

        public bool cancelReleased;

        public static bool TryGet(out NowUIMouseInput input)
        {
            if (NowUIInputSystemInput.TryGet(out input))
                return true;

    #if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                Vector3 mousePosition = Input.mousePosition;
                NowUIPointerButtons down = NowUIPointerButtons.None;
                NowUIPointerButtons pressed = NowUIPointerButtons.None;
                NowUIPointerButtons released = NowUIPointerButtons.None;

                if (Input.touchCount > 0)
                {
                    UnityEngine.Touch touch = Input.GetTouch(0);
                    mousePosition = touch.position;
                    var primaryMask = NowUIInputSnapshot.ToButtonMask(NowUIPointerButton.Primary);

                    if (touch.phase == UnityEngine.TouchPhase.Began)
                        pressed |= primaryMask;

                    if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                        released |= primaryMask;
                    else
                        down |= primaryMask;
                }

                AppendLegacyMouseButton(0, NowUIPointerButton.Primary, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(1, NowUIPointerButton.Secondary, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(2, NowUIPointerButton.Middle, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(3, NowUIPointerButton.Back, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(4, NowUIPointerButton.Forward, ref down, ref pressed, ref released);
                Vector2 navigation = ReadLegacyNavigation();
                input = new NowUIMouseInput
                {
                    hasPointer = true,
                    screenPosition = mousePosition,
                    pointerButtonsDown = down,
                    pointerButtonsPressed = pressed,
                    pointerButtonsReleased = released,
                    scrollDelta = Input.mouseScrollDelta,
                    navigation = navigation,
                    submitDown = IsLegacySubmitDown(),
                    submitPressed = WasLegacySubmitPressed(),
                    submitReleased = WasLegacySubmitReleased(),
                    cancelDown = IsLegacyCancelDown(),
                    cancelPressed = WasLegacyCancelPressed(),
                    cancelReleased = WasLegacyCancelReleased()
                };
                return true;
            }
            catch (InvalidOperationException)
            {
            }
    #endif

            input = default;
            return false;
        }

    #if ENABLE_LEGACY_INPUT_MANAGER
        static void AppendLegacyMouseButton(
            int index,
            NowUIPointerButton button,
            ref NowUIPointerButtons down,
            ref NowUIPointerButtons pressed,
            ref NowUIPointerButtons released)
        {
            var mask = NowUIInputSnapshot.ToButtonMask(button);

            if (Input.GetMouseButton(index))
                down |= mask;

            if (Input.GetMouseButtonDown(index))
                pressed |= mask;

            if (Input.GetMouseButtonUp(index))
                released |= mask;
        }

        static Vector2 ReadLegacyNavigation()
        {
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
                x -= 1f;

            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
                x += 1f;

            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                y -= 1f;

            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                y += 1f;

            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        static bool IsLegacySubmitDown()
        {
            return
                Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.KeypadEnter) ||
                Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.JoystickButton0);
        }

        static bool WasLegacySubmitPressed()
        {
            return
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton0);
        }

        static bool WasLegacySubmitReleased()
        {
            return
                Input.GetKeyUp(KeyCode.Return) ||
                Input.GetKeyUp(KeyCode.KeypadEnter) ||
                Input.GetKeyUp(KeyCode.Space) ||
                Input.GetKeyUp(KeyCode.JoystickButton0);
        }

        static bool IsLegacyCancelDown()
        {
            return Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.JoystickButton1);
        }

        static bool WasLegacyCancelPressed()
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
        }

        static bool WasLegacyCancelReleased()
        {
            return Input.GetKeyUp(KeyCode.Escape) || Input.GetKeyUp(KeyCode.JoystickButton1);
        }
    #endif
    }

    internal static class NowUIInputSystemInput
    {
        public static bool TryGet(out NowUIMouseInput input)
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
                AppendPointerButton(mouse.leftButton, NowUIPointerButton.Primary, ref input);
                AppendPointerButton(mouse.rightButton, NowUIPointerButton.Secondary, ref input);
                AppendPointerButton(mouse.middleButton, NowUIPointerButton.Middle, ref input);
                AppendPointerButton(mouse.backButton, NowUIPointerButton.Back, ref input);
                AppendPointerButton(mouse.forwardButton, NowUIPointerButton.Forward, ref input);
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
                    AppendPointerButton(press, NowUIPointerButton.Primary, ref input);
                    hasAnyInput = true;
                }
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                input.navigation += ReadKeyboardNavigation(keyboard);
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

        static void AppendPointerButton(ButtonControl control, NowUIPointerButton button, ref NowUIMouseInput input)
        {
            if (control == null)
                return;

            var mask = NowUIInputSnapshot.ToButtonMask(button);

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
