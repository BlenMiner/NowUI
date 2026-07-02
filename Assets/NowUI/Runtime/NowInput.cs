using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NowUI
{
    public interface INowInputProvider
    {
        bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot);
    }

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

    public readonly struct NowInteraction
    {
        public readonly int id;

        public readonly Rect rect;

        public readonly NowPointerButton button;

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
            NowPointerButton button,
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

        /// <summary>Derives a stable sub-id from this interaction's resolved control id.</summary>
        public int GetId(string key)
        {
            return NowInput.GetId(id, key);
        }

        /// <summary>Derives a stable numeric sub-id from this interaction's resolved control id.</summary>
        public int GetId(int key)
        {
            return NowInput.CombineId(id, key);
        }

        /// <summary>Returns a persistent control-state slot keyed under this interaction.</summary>
        public ref T State<T>(string key) where T : struct
        {
            return ref NowControlState.Get<T>(GetId(key));
        }

        /// <summary>Returns a persistent control-state slot keyed under this interaction.</summary>
        public ref T State<T>(int key) where T : struct
        {
            return ref NowControlState.Get<T>(GetId(key));
        }
    }

    public static class NowInput
    {
        const float DefaultDragThreshold = 4f;

        static INowInputProvider _defaultProvider = NowScreenInputProvider.instance;

        static INowInputProvider _currentProvider;

        static NowInputSurface _surface;

        static NowInputSnapshot _snapshot;

        static bool _hasContext;

        static int _activeId;

        static INowInputProvider _activeProvider;

        static NowPointerButton _activeButton;

        static int _dragId;

        static bool _activeDragged;

        static bool _activeSeenThisFrame;

        static int _activeLastSeenFrame = -1;

        static Vector2 _pressPosition;

        static float _dragThreshold = DefaultDragThreshold;

        static int _passiveDepth;

        static int _scopeDepth;

        static bool _scrollConsumed;

        public static INowInputProvider defaultProvider
        {
            get => _defaultProvider;
            set => _defaultProvider = value;
        }

        public static NowInputSurface surface => _surface;

        public static NowInputSnapshot current => _snapshot;

        public static bool hasContext => _hasContext;

        public static int activeId => _activeId;

        public static NowPointerButton activeButton => _activeButton;

        public static float dragThreshold
        {
            get => _dragThreshold;
            set => _dragThreshold = Mathf.Max(0f, value);
        }

        public static NowInputScope Begin(Vector2 size)
        {
            return Begin(_defaultProvider, new NowInputSurface(size));
        }

        public static NowInputScope Begin(Vector2 size, Rect screenRect)
        {
            return Begin(_defaultProvider, new NowInputSurface(size, screenRect));
        }

        public static NowInputScope Begin(INowInputProvider provider, Vector2 size)
        {
            return Begin(provider, new NowInputSurface(size));
        }

        public static NowInputScope Begin(INowInputProvider provider, NowInputSurface surface)
        {
            bool topLevel = _scopeDepth == 0;

            if (topLevel)
                CompleteFrame();

            var scope = new NowInputScope(_surface, _snapshot, _hasContext, topLevel);
            ++_scopeDepth;
            Update(provider, surface, topLevel);
            return scope;
        }

        internal static NowInputScope BeginMeasurement(INowInputProvider provider, NowInputSurface surface)
        {
            var scope = new NowInputScope(_surface, _snapshot, _hasContext, false);
            ++_scopeDepth;
            Update(provider, surface, false);
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
            if (_scopeDepth == 0)
                CompleteFrame();

            Update(provider, surface, _scopeDepth == 0);
        }

        static void Update(INowInputProvider provider, NowInputSurface surface, bool resetFrameTracking)
        {
            _currentProvider = provider;
            _surface = surface;
            _hasContext = true;
            NowControls.ResetControlIdOccurrences();
            _scrollConsumed = false;

            if (resetFrameTracking)
                _activeSeenThisFrame = false;

            if (provider != null && provider.TryGetSnapshot(surface, out _snapshot))
            {
                if (resetFrameTracking)
                    ClearStaleActiveFromMissingProvider();

                return;
            }

            _snapshot = default;
        }

        public static bool IsHovered(NowRect rect)
        {
            return IsHovered((Rect)rect);
        }

        public static bool IsHovered(Rect rect)
        {
            rect = Now.TransformScreenRect(rect);

            return _hasContext && _snapshot.hasPointer &&
                rect.Contains(_snapshot.pointerPosition) &&
                Now.IsInsideAmbientMask(_snapshot.pointerPosition) &&
                !NowOverlay.IsPointerBlocked(_snapshot.pointerPosition);
        }

        public static Vector2 ConsumeScrollDelta(NowRect rect)
        {
            return ConsumeScrollDelta((Rect)rect);
        }

        public static Vector2 ConsumeScrollDelta(Rect rect)
        {
            if (_passiveDepth > 0 || _scrollConsumed || !_hasContext || !_snapshot.hasPointer)
                return default;

            rect = Now.TransformScreenRect(rect);

            Vector2 scroll = _snapshot.scrollDelta;

            if (scroll == Vector2.zero ||
                !rect.Contains(_snapshot.pointerPosition) ||
                !Now.IsInsideAmbientMask(_snapshot.pointerPosition) ||
                NowOverlay.IsPointerBlocked(_snapshot.pointerPosition))
            {
                return default;
            }

            _scrollConsumed = true;
            return scroll;
        }

        public static bool IsPointerDown(NowPointerButton button)
        {
            return _hasContext && _snapshot.IsPointerDown(button);
        }

        public static bool WasPointerPressed(NowPointerButton button)
        {
            return _hasContext && _snapshot.WasPointerPressed(button);
        }

        public static bool WasPointerReleased(NowPointerButton button)
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

        static int CallerControlId(string file, int line)
        {
            return NowControls.GetControlId(NowControls.SiteId(file, line));
        }

        /// <summary>
        /// Interaction with no explicit id: identity comes from the call site, and
        /// repeated calls from one site (a loop over sub-elements) are salted by
        /// per-frame occurrence — draw-order stable, like control identity. Use
        /// an explicit id instead when looped items can reorder mid-press.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return Interact(CallerControlId(file, line), rect);
        }

        /// <summary>
        /// Interaction with no explicit id and a non-primary pointer button.
        /// Identity comes from the call site; use an explicit id for reordered
        /// loop data or when several call sites represent one logical target.
        /// </summary>
        public static NowInteraction Interact(
            NowRect rect,
            NowPointerButton button,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return Interact(CallerControlId(file, line), rect, button);
        }

        /// <summary>
        /// Interaction with no explicit id for callers that already use Unity
        /// <see cref="Rect"/> values. Identity comes from the call site.
        /// </summary>
        public static NowInteraction Interact(
            Rect rect,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return Interact(CallerControlId(file, line), rect);
        }

        /// <summary>
        /// Interaction with no explicit id for Unity <see cref="Rect"/> values and
        /// a non-primary pointer button. Identity comes from the call site.
        /// </summary>
        public static NowInteraction Interact(
            Rect rect,
            NowPointerButton button,
            [System.Runtime.CompilerServices.CallerFilePath] string file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            return Interact(CallerControlId(file, line), rect, button);
        }

        public static NowInteraction Interact(string id, NowRect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(NowId id, NowRect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(string id, NowRect rect, NowPointerButton button)
        {
            return Interact(GetId(id), (Rect)rect, button);
        }

        public static NowInteraction Interact(NowId id, NowRect rect, NowPointerButton button)
        {
            return Interact(RequireExplicitId(id, nameof(id)), (Rect)rect, button);
        }

        public static NowInteraction Interact(string id, Rect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(NowId id, Rect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(string id, Rect rect, NowPointerButton button)
        {
            return Interact(GetId(id), rect, button);
        }

        public static NowInteraction Interact(NowId id, Rect rect, NowPointerButton button)
        {
            return Interact(RequireExplicitId(id, nameof(id)), rect, button);
        }

        public static NowInteraction Interact(int id, NowRect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(int id, NowRect rect, NowPointerButton button)
        {
            return Interact(id, (Rect)rect, button);
        }

        public static NowInteraction Interact(int id, Rect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(int id, Rect rect, NowPointerButton button)
        {
            if (id == 0)
                throw new ArgumentException("Control id 0 is reserved.", nameof(id));

            Rect localRect = rect;
            Rect screenRect = Now.TransformScreenRect(rect);

            var snapshot = _snapshot;
            bool hasPointer = _hasContext && snapshot.hasPointer;
            bool hovered = hasPointer && screenRect.Contains(snapshot.pointerPosition) &&
                Now.IsInsideAmbientMask(snapshot.pointerPosition) &&
                !NowOverlay.IsPointerBlocked(snapshot.pointerPosition);
            Vector2 localPointerPosition = Now.InverseTransformScreenPoint(snapshot.pointerPosition);
            Vector2 localPointerDelta = Now.InverseTransformScreenVector(snapshot.pointerDelta);

            if (_passiveDepth > 0)
            {
                return new NowInteraction(
                    id,
                    localRect,
                    button,
                    hasPointer,
                    localPointerPosition,
                    localPointerDelta,
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
                _activeProvider = _currentProvider;
                _activeButton = button;
                _dragId = 0;
                _activeDragged = false;
                _pressPosition = snapshot.pointerPosition;
            }

            bool active = _activeId == id && _activeButton == button;

            if (_passiveDepth == 0 && active)
            {
                _activeSeenThisFrame = true;
                _activeLastSeenFrame = snapshot.frame;
            }

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
                localRect,
                button,
                hasPointer,
                localPointerPosition,
                localPointerDelta,
                Now.InverseTransformScreenVector(dragDelta),
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

        public static int GetId(NowId id, int fallback)
        {
            return id.ResolveStableId(fallback);
        }

        static int RequireExplicitId(NowId id, string paramName)
        {
            if (!id.hasValue)
                throw new ArgumentException("This API requires an explicit NowId.", paramName);

            return id.ResolveStableId(0);
        }

        public static void Reset()
        {
            _surface = default;
            _snapshot = default;
            _hasContext = false;
            _currentProvider = null;
            _activeId = 0;
            _activeProvider = null;
            _activeButton = NowPointerButton.Primary;
            _dragId = 0;
            _activeDragged = false;
            _activeSeenThisFrame = false;
            _activeLastSeenFrame = -1;
            _pressPosition = default;
            _dragThreshold = DefaultDragThreshold;
            _defaultProvider = NowScreenInputProvider.instance;
            _passiveDepth = 0;
            _scopeDepth = 0;
            _scrollConsumed = false;
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

        internal static void Restore(NowInputSurface surface, NowInputSnapshot snapshot, bool hasContext)
        {
            _surface = surface;
            _snapshot = snapshot;
            _hasContext = hasContext;
        }

        internal static void EndScope(NowInputSurface previousSurface, NowInputSnapshot previousSnapshot, bool previousHasContext, bool completeFrame)
        {
            if (completeFrame)
                NowOverlay.Flush();

            if (_scopeDepth > 0)
                --_scopeDepth;

            if (completeFrame)
                EndFrame();

            Restore(previousSurface, previousSnapshot, previousHasContext);
        }

        internal static void EndFrame()
        {
            CompleteFrame();
        }

        internal static void ClearActiveIf(int id, NowPointerButton button = NowPointerButton.Primary)
        {
            if (_activeId == id && _activeButton == button)
                ClearActive();
        }

        static void CompleteFrame()
        {
            if (_activeId == 0 || _activeSeenThisFrame)
                return;

            if (!ReferenceEquals(_currentProvider, _activeProvider))
                return;

            if (!_hasContext)
                return;

            if (!_snapshot.hasPointer ||
                !_snapshot.IsPointerDown(_activeButton) ||
                _snapshot.WasPointerReleased(_activeButton))
            {
                ClearActive();
            }
        }

        static Rect ToRect(Vector4 rect)
        {
            return new Rect(rect.x, rect.y, rect.z, rect.w);
        }

        static void ClearActive()
        {
            _activeId = 0;
            _activeProvider = null;
            _activeButton = NowPointerButton.Primary;
            _dragId = 0;
            _activeDragged = false;
            _activeSeenThisFrame = false;
            _activeLastSeenFrame = -1;
            _pressPosition = default;
        }

        static void ClearStaleActiveFromMissingProvider()
        {
            if (_activeId == 0 ||
                ReferenceEquals(_currentProvider, _activeProvider) ||
                _activeLastSeenFrame < 0)
            {
                return;
            }

            // Let another provider draw earlier in the next frame; clear only
            // after the capture owner has missed a whole input frame.
            if (_snapshot.frame > _activeLastSeenFrame + 1)
                ClearActive();
        }
    }

    [NowScope]
    public struct NowInputScope : IDisposable
    {
        readonly NowInputSurface _previousSurface;

        readonly NowInputSnapshot _previousSnapshot;

        readonly bool _previousHasContext;

        readonly bool _completeFrame;

        bool _disposed;

        internal NowInputScope(
            NowInputSurface previousSurface,
            NowInputSnapshot previousSnapshot,
            bool previousHasContext,
            bool completeFrame)
        {
            _previousSurface = previousSurface;
            _previousSnapshot = previousSnapshot;
            _previousHasContext = previousHasContext;
            _completeFrame = completeFrame;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowInput.EndScope(_previousSurface, _previousSnapshot, _previousHasContext, _completeFrame);
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

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot)
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

            snapshot = new NowInputSnapshot(
                _hasRawPosition,
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
            bool allowedNow = !blockedWhenPointerOverUGUI || !NowRaycastGate.IsPointerOverUGUI();
            bool pointerVisible = mouseInput.hasPointer &&
                NowRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow);

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

        NowPointerButtons _previousButtonsDown;

        bool _pressAllowed = true;
    }

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

            if (!NowRaycastGate.UpdatePressGate(ref _pressAllowed, buttonsWereDown, allowedNow))
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
            if (NowInputSystemInput.TryGet(out input))
                return true;

    #if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                Vector3 mousePosition = Input.mousePosition;
                NowPointerButtons down = NowPointerButtons.None;
                NowPointerButtons pressed = NowPointerButtons.None;
                NowPointerButtons released = NowPointerButtons.None;

                if (Input.touchCount > 0)
                {
                    UnityEngine.Touch touch = Input.GetTouch(0);
                    mousePosition = touch.position;
                    var primaryMask = NowInputSnapshot.ToButtonMask(NowPointerButton.Primary);

                    if (touch.phase == UnityEngine.TouchPhase.Began)
                        pressed |= primaryMask;

                    if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                        released |= primaryMask;
                    else
                        down |= primaryMask;
                }

                AppendLegacyMouseButton(0, NowPointerButton.Primary, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(1, NowPointerButton.Secondary, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(2, NowPointerButton.Middle, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(3, NowPointerButton.Back, ref down, ref pressed, ref released);
                AppendLegacyMouseButton(4, NowPointerButton.Forward, ref down, ref pressed, ref released);
                Vector2 navigation = ReadLegacyNavigation();
                input = new NowMouseInput
                {
                    hasPointer = true,
                    screenPosition = mousePosition,
                    pointerButtonsDown = down,
                    pointerButtonsPressed = pressed,
                    pointerButtonsReleased = released,
                    scrollDelta = Input.mouseScrollDelta,
                    navigation = navigation,
                    focusPreviousPressed = WasLegacyFocusPreviousPressed(),
                    focusNextPressed = WasLegacyFocusNextPressed(),
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
            NowPointerButton button,
            ref NowPointerButtons down,
            ref NowPointerButtons pressed,
            ref NowPointerButtons released)
        {
            var mask = NowInputSnapshot.ToButtonMask(button);

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

        static bool WasLegacyFocusPreviousPressed()
        {
            return Input.GetKeyDown(KeyCode.Tab) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        }

        static bool WasLegacyFocusNextPressed()
        {
            return Input.GetKeyDown(KeyCode.Tab) &&
                !Input.GetKey(KeyCode.LeftShift) &&
                !Input.GetKey(KeyCode.RightShift);
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
