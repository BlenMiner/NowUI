using System;
using System.Reflection;
using UnityEngine;

public interface INowUIInputProvider
{
    bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot);
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

public struct NowUIInputSurface
{
    public Vector2 size;

    public Rect screenRect;

    public NowUIInputSurface(Vector2 size)
        : this(size, new Rect(0f, 0f, size.x, size.y))
    {
    }

    public NowUIInputSurface(Vector2 size, Rect screenRect)
    {
        this.size = size;
        this.screenRect = screenRect;
    }

    public static NowUIInputSurface FromScreen()
    {
        var size = new Vector2(Screen.width, Screen.height);
        return new NowUIInputSurface(size);
    }

    public static NowUIInputSurface FromScreenMask(Vector4 screenMask)
    {
        var size = new Vector2(screenMask.z, screenMask.w);
        var screenRect = new Rect(screenMask.x, screenMask.y, screenMask.z, screenMask.w);
        return new NowUIInputSurface(size, screenRect);
    }

    public static NowUIInputSurface FromCamera(Camera camera)
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
        return new NowUIInputSurface(size, screenRect);
    }
}

public struct NowUIInputSnapshot
{
    public bool hasPointer;

    public Vector2 pointerPosition;

    public Vector2 previousPointerPosition;

    public Vector2 pointerDelta;

    public bool primaryDown;

    public bool primaryPressed;

    public bool primaryReleased;

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

public readonly struct NowUIInteraction
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

    internal NowUIInteraction(
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

public static class NowUIInput
{
    const float DefaultDragThreshold = 4f;

    static INowUIInputProvider _defaultProvider = NowUIScreenInputProvider.instance;

    static NowUIInputSurface _surface;

    static NowUIInputSnapshot _snapshot;

    static bool _hasContext;

    static int _activeId;

    static NowUIPointerButton _activeButton;

    static int _dragId;

    static bool _activeDragged;

    static Vector2 _pressPosition;

    static float _dragThreshold = DefaultDragThreshold;

    public static INowUIInputProvider defaultProvider
    {
        get => _defaultProvider;
        set => _defaultProvider = value;
    }

    public static NowUIInputSurface surface => _surface;

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
        return Begin(_defaultProvider, new NowUIInputSurface(size));
    }

    public static NowUIInputScope Begin(Vector2 size, Rect screenRect)
    {
        return Begin(_defaultProvider, new NowUIInputSurface(size, screenRect));
    }

    public static NowUIInputScope Begin(INowUIInputProvider provider, Vector2 size)
    {
        return Begin(provider, new NowUIInputSurface(size));
    }

    public static NowUIInputScope Begin(INowUIInputProvider provider, NowUIInputSurface surface)
    {
        var scope = new NowUIInputScope(_surface, _snapshot, _hasContext);
        Update(provider, surface);
        return scope;
    }

    public static void Update(Vector2 size)
    {
        Update(_defaultProvider, new NowUIInputSurface(size));
    }

    public static void Update(NowUIInputSurface surface)
    {
        Update(_defaultProvider, surface);
    }

    public static void Update(INowUIInputProvider provider, NowUIInputSurface surface)
    {
        _surface = surface;
        _hasContext = true;

        if (provider != null && provider.TryGetSnapshot(surface, out _snapshot))
            return;

        _snapshot = default;
    }

    public static bool IsHovered(Vector4 rect)
    {
        return IsHovered(ToRect(rect));
    }

    public static bool IsHovered(Rect rect)
    {
        return _hasContext && _snapshot.hasPointer && rect.Contains(_snapshot.pointerPosition);
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

    public static NowUIInteraction Interact(string id, Vector4 rect)
    {
        return Interact(id, rect, NowUIPointerButton.Primary);
    }

    public static NowUIInteraction Interact(string id, Vector4 rect, NowUIPointerButton button)
    {
        return Interact(GetId(id), ToRect(rect), button);
    }

    public static NowUIInteraction Interact(string id, Rect rect)
    {
        return Interact(id, rect, NowUIPointerButton.Primary);
    }

    public static NowUIInteraction Interact(string id, Rect rect, NowUIPointerButton button)
    {
        return Interact(GetId(id), rect, button);
    }

    public static NowUIInteraction Interact(int id, Vector4 rect)
    {
        return Interact(id, rect, NowUIPointerButton.Primary);
    }

    public static NowUIInteraction Interact(int id, Vector4 rect, NowUIPointerButton button)
    {
        return Interact(id, ToRect(rect), button);
    }

    public static NowUIInteraction Interact(int id, Rect rect)
    {
        return Interact(id, rect, NowUIPointerButton.Primary);
    }

    public static NowUIInteraction Interact(int id, Rect rect, NowUIPointerButton button)
    {
        if (id == 0)
            throw new ArgumentException("Control id 0 is reserved.", nameof(id));

        var snapshot = _snapshot;
        bool hasPointer = _hasContext && snapshot.hasPointer;
        bool hovered = hasPointer && rect.Contains(snapshot.pointerPosition);
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

        var interaction = new NowUIInteraction(
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
        _defaultProvider = NowUIScreenInputProvider.instance;
    }

    internal static bool TryScreenToSurface(Vector2 topLeftScreenPosition, NowUIInputSurface surface, out Vector2 position)
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

    internal static Vector2 ScaleScreenDelta(Vector2 topLeftScreenDelta, NowUIInputSurface surface)
    {
        Rect screenRect = surface.screenRect;

        if (screenRect.width <= 0f || screenRect.height <= 0f)
            return default;

        return new Vector2(
            topLeftScreenDelta.x * surface.size.x / screenRect.width,
            topLeftScreenDelta.y * surface.size.y / screenRect.height);
    }

    internal static void Restore(NowUIInputSurface surface, NowUIInputSnapshot snapshot, bool hasContext)
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

public struct NowUIInputScope : IDisposable
{
    readonly NowUIInputSurface _previousSurface;

    readonly NowUIInputSnapshot _previousSnapshot;

    readonly bool _previousHasContext;

    bool _disposed;

    internal NowUIInputScope(
        NowUIInputSurface previousSurface,
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
        NowUIInput.Restore(_previousSurface, _previousSnapshot, _previousHasContext);
    }
}

public sealed class NowUIScreenInputProvider : INowUIInputProvider
{
    public static readonly NowUIScreenInputProvider instance = new NowUIScreenInputProvider();

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

    public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
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
            if (!NowUIInput.TryScreenToSurface(_rawPosition, surface, out position))
            {
                snapshot = default;
                return false;
            }

            delta = NowUIInput.ScaleScreenDelta(_rawDelta, surface);
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

        if (mouseInput.hasPointer)
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
}

public sealed class NowUIIMGUIInputProvider : INowUIInputProvider
{
    public static readonly NowUIIMGUIInputProvider instance = new NowUIIMGUIInputProvider();

    NowUIPointerButtons _buttonsDown;

    public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
    {
        Event current = Event.current;

        if (current == null)
        {
            snapshot = default;
            return false;
        }

        if (!NowUIInput.TryScreenToSurface(current.mousePosition, surface, out var position))
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

        Vector2 delta = NowUIInput.ScaleScreenDelta(current.delta, surface);
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

public sealed class NowUIRectTransformInputProvider : INowUIInputProvider
{
    RectTransform _rectTransform;

    Camera _eventCamera;

    int _lastFrame = -1;

    bool _hasPreviousPosition;

    Vector2 _previousPosition;

    NowUIInputSnapshot _snapshot;

    public NowUIRectTransformInputProvider()
    {
    }

    public NowUIRectTransformInputProvider(RectTransform rectTransform, Camera eventCamera = null)
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

    public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
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
        if (NowUINewInputSystemMouse.TryGet(out input))
            return true;

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            Vector3 mousePosition = Input.mousePosition;
            NowUIPointerButtons down = NowUIPointerButtons.None;
            NowUIPointerButtons pressed = NowUIPointerButtons.None;
            NowUIPointerButtons released = NowUIPointerButtons.None;

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

internal static class NowUINewInputSystemMouse
{
    static bool _initialized;

    static PropertyInfo _mouseCurrentProperty;

    static PropertyInfo _mousePositionProperty;

    static PropertyInfo _mouseScrollProperty;

    static PropertyInfo[] _mouseButtonProperties;

    static PropertyInfo _keyboardCurrentProperty;

    static PropertyInfo[] _keyboardNavigationProperties;

    static PropertyInfo[] _keyboardSubmitProperties;

    static PropertyInfo[] _keyboardCancelProperties;

    static PropertyInfo _gamepadCurrentProperty;

    static PropertyInfo _gamepadLeftStickProperty;

    static PropertyInfo _gamepadDpadProperty;

    static PropertyInfo[] _gamepadSubmitProperties;

    static PropertyInfo[] _gamepadCancelProperties;

    static PropertyInfo _buttonPressedProperty;

    static PropertyInfo _buttonWasPressedProperty;

    static PropertyInfo _buttonWasReleasedProperty;

    public static bool TryGet(out NowUIMouseInput input)
    {
        EnsureInitialized();

        try
        {
            input = default;
            bool hasAnyInput = false;
            object mouse = _mouseCurrentProperty?.GetValue(null);

            if (mouse != null)
            {
                object positionControl = _mousePositionProperty?.GetValue(mouse);
                object scrollControl = _mouseScrollProperty?.GetValue(mouse);

                input.hasPointer = TryReadVector2(positionControl, out input.screenPosition);

                if (TryReadVector2(scrollControl, out var scrollDelta))
                    input.scrollDelta = scrollDelta;

                if (_mouseButtonProperties != null)
                {
                    for (int i = 0; i < _mouseButtonProperties.Length; ++i)
                    {
                        object button = _mouseButtonProperties[i]?.GetValue(mouse);

                        if (!TryReadButton(button, out var down, out var pressed, out var released))
                            continue;

                        var mask = NowUIInputSnapshot.ToButtonMask((NowUIPointerButton)i);

                        if (down)
                            input.pointerButtonsDown |= mask;

                        if (pressed)
                            input.pointerButtonsPressed |= mask;

                        if (released)
                            input.pointerButtonsReleased |= mask;
                    }
                }

                hasAnyInput = input.hasPointer || input.pointerButtonsDown != NowUIPointerButtons.None;
            }

            object keyboard = _keyboardCurrentProperty?.GetValue(null);

            if (keyboard != null)
            {
                input.navigation += ReadKeyboardNavigation(keyboard);
                ReadButtons(keyboard, _keyboardSubmitProperties, out input.submitDown, out input.submitPressed, out input.submitReleased);
                ReadButtons(keyboard, _keyboardCancelProperties, out input.cancelDown, out input.cancelPressed, out input.cancelReleased);
                hasAnyInput = true;
            }

            object gamepad = _gamepadCurrentProperty?.GetValue(null);

            if (gamepad != null)
            {
                if (TryReadVector2(_gamepadLeftStickProperty?.GetValue(gamepad), out var leftStick))
                    input.navigation += leftStick;

                if (TryReadVector2(_gamepadDpadProperty?.GetValue(gamepad), out var dpad))
                    input.navigation += dpad;

                ReadButtons(gamepad, _gamepadSubmitProperties, out var submitDown, out var submitPressed, out var submitReleased);
                ReadButtons(gamepad, _gamepadCancelProperties, out var cancelDown, out var cancelPressed, out var cancelReleased);
                input.submitDown |= submitDown;
                input.submitPressed |= submitPressed;
                input.submitReleased |= submitReleased;
                input.cancelDown |= cancelDown;
                input.cancelPressed |= cancelPressed;
                input.cancelReleased |= cancelReleased;
                hasAnyInput = true;
            }

            input.navigation = Vector2.ClampMagnitude(input.navigation, 1f);
            return hasAnyInput;
        }
        catch
        {
            input = default;
            return false;
        }
    }

    static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;

        Type mouseType = ResolveInputSystemType("UnityEngine.InputSystem.Mouse");
        Type keyboardType = ResolveInputSystemType("UnityEngine.InputSystem.Keyboard");
        Type gamepadType = ResolveInputSystemType("UnityEngine.InputSystem.Gamepad");

        if (mouseType != null)
        {
            _mouseCurrentProperty = GetProperty(mouseType, "current", true);
            _mousePositionProperty = GetProperty(mouseType, "position", false);
            _mouseScrollProperty = GetProperty(mouseType, "scroll", false);
            _mouseButtonProperties = new[]
            {
                GetProperty(mouseType, "leftButton", false),
                GetProperty(mouseType, "rightButton", false),
                GetProperty(mouseType, "middleButton", false),
                GetProperty(mouseType, "backButton", false),
                GetProperty(mouseType, "forwardButton", false)
            };
        }

        if (keyboardType != null)
        {
            _keyboardCurrentProperty = GetProperty(keyboardType, "current", true);
            _keyboardNavigationProperties = new[]
            {
                GetProperty(keyboardType, "leftArrowKey", false),
                GetProperty(keyboardType, "rightArrowKey", false),
                GetProperty(keyboardType, "downArrowKey", false),
                GetProperty(keyboardType, "upArrowKey", false),
                GetProperty(keyboardType, "aKey", false),
                GetProperty(keyboardType, "dKey", false),
                GetProperty(keyboardType, "sKey", false),
                GetProperty(keyboardType, "wKey", false)
            };
            _keyboardSubmitProperties = new[]
            {
                GetProperty(keyboardType, "enterKey", false),
                GetProperty(keyboardType, "numpadEnterKey", false),
                GetProperty(keyboardType, "spaceKey", false)
            };
            _keyboardCancelProperties = new[]
            {
                GetProperty(keyboardType, "escapeKey", false)
            };
        }

        if (gamepadType != null)
        {
            _gamepadCurrentProperty = GetProperty(gamepadType, "current", true);
            _gamepadLeftStickProperty = GetProperty(gamepadType, "leftStick", false);
            _gamepadDpadProperty = GetProperty(gamepadType, "dpad", false);
            _gamepadSubmitProperties = new[]
            {
                GetProperty(gamepadType, "buttonSouth", false),
                GetProperty(gamepadType, "startButton", false)
            };
            _gamepadCancelProperties = new[]
            {
                GetProperty(gamepadType, "buttonEast", false),
                GetProperty(gamepadType, "selectButton", false)
            };
        }
    }

    static Vector2 ReadKeyboardNavigation(object keyboard)
    {
        if (_keyboardNavigationProperties == null)
            return default;

        float x = 0f;
        float y = 0f;

        if (IsButtonDown(keyboard, _keyboardNavigationProperties[0]) || IsButtonDown(keyboard, _keyboardNavigationProperties[4]))
            x -= 1f;

        if (IsButtonDown(keyboard, _keyboardNavigationProperties[1]) || IsButtonDown(keyboard, _keyboardNavigationProperties[5]))
            x += 1f;

        if (IsButtonDown(keyboard, _keyboardNavigationProperties[2]) || IsButtonDown(keyboard, _keyboardNavigationProperties[6]))
            y -= 1f;

        if (IsButtonDown(keyboard, _keyboardNavigationProperties[3]) || IsButtonDown(keyboard, _keyboardNavigationProperties[7]))
            y += 1f;

        return new Vector2(x, y);
    }

    static void ReadButtons(
        object owner,
        PropertyInfo[] properties,
        out bool down,
        out bool pressed,
        out bool released)
    {
        down = false;
        pressed = false;
        released = false;

        if (owner == null || properties == null)
            return;

        for (int i = 0; i < properties.Length; ++i)
        {
            object button = properties[i]?.GetValue(owner);

            if (!TryReadButton(button, out var candidateDown, out var candidatePressed, out var candidateReleased))
                continue;

            down |= candidateDown;
            pressed |= candidatePressed;
            released |= candidateReleased;
        }
    }

    static bool IsButtonDown(object owner, PropertyInfo property)
    {
        if (owner == null || property == null)
            return false;

        object button = property.GetValue(owner);
        return TryReadButton(button, out var down, out _, out _) && down;
    }

    static bool TryReadButton(object button, out bool down, out bool pressed, out bool released)
    {
        EnsureButtonProperties(button);

        if (button == null ||
            _buttonPressedProperty == null ||
            _buttonWasPressedProperty == null ||
            _buttonWasReleasedProperty == null)
        {
            down = false;
            pressed = false;
            released = false;
            return false;
        }

        down = (bool)_buttonPressedProperty.GetValue(button);
        pressed = (bool)_buttonWasPressedProperty.GetValue(button);
        released = (bool)_buttonWasReleasedProperty.GetValue(button);
        return true;
    }

    static void EnsureButtonProperties(object button)
    {
        if (button == null || _buttonPressedProperty != null)
            return;

        Type buttonType = button.GetType();

        _buttonPressedProperty = buttonType.GetProperty("isPressed", BindingFlags.Public | BindingFlags.Instance);
        _buttonWasPressedProperty = buttonType.GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance);
        _buttonWasReleasedProperty = buttonType.GetProperty("wasReleasedThisFrame", BindingFlags.Public | BindingFlags.Instance);
    }

    static bool TryReadVector2(object control, out Vector2 value)
    {
        if (control == null)
        {
            value = default;
            return false;
        }

        MethodInfo readValueMethod = control.GetType().GetMethod("ReadValue", Type.EmptyTypes);

        if (readValueMethod == null)
        {
            value = default;
            return false;
        }

        value = (Vector2)readValueMethod.Invoke(control, null);
        return true;
    }

    static PropertyInfo GetProperty(Type type, string name, bool isStatic)
    {
        var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        return type.GetProperty(name, flags);
    }

    static Type ResolveInputSystemType(string typeName)
    {
        Type inputType = Type.GetType(typeName + ", Unity.InputSystem");

        if (inputType != null)
            return inputType;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; ++i)
        {
            var assembly = assemblies[i];

            if (assembly == null || assembly.GetName().Name != "Unity.InputSystem")
                continue;

            inputType = assembly.GetType(typeName);

            if (inputType != null)
                return inputType;
        }

        return null;
    }
}
