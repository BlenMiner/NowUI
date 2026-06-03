using System;
using UnityEngine;

public interface INowUIInputProvider
{
    bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot);
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

    public Vector2 scrollDelta;

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
            primaryDown,
            primaryPressed,
            primaryReleased,
            Vector2.zero,
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
            primaryDown,
            primaryPressed,
            primaryReleased,
            Vector2.zero,
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
    {
        this.hasPointer = hasPointer;
        this.pointerPosition = pointerPosition;
        this.previousPointerPosition = previousPointerPosition;
        this.pointerDelta = pointerDelta;
        this.primaryDown = primaryDown;
        this.primaryPressed = primaryPressed;
        this.primaryReleased = primaryReleased;
        this.scrollDelta = scrollDelta;
        this.frame = frame;
        this.time = time;
    }
}

public readonly struct NowUIInteraction
{
    public readonly int id;

    public readonly Rect rect;

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

    public static NowUIInteraction Interact(string id, Vector4 rect)
    {
        return Interact(GetId(id), ToRect(rect));
    }

    public static NowUIInteraction Interact(string id, Rect rect)
    {
        return Interact(GetId(id), rect);
    }

    public static NowUIInteraction Interact(int id, Vector4 rect)
    {
        return Interact(id, ToRect(rect));
    }

    public static NowUIInteraction Interact(int id, Rect rect)
    {
        if (id == 0)
            throw new ArgumentException("Control id 0 is reserved.", nameof(id));

        var snapshot = _snapshot;
        bool hasPointer = _hasContext && snapshot.hasPointer;
        bool hovered = hasPointer && rect.Contains(snapshot.pointerPosition);
        bool pressed = hovered && snapshot.primaryPressed && (_activeId == 0 || _activeId == id);

        if (pressed)
        {
            _activeId = id;
            _dragId = 0;
            _activeDragged = false;
            _pressPosition = snapshot.pointerPosition;
        }

        bool active = _activeId == id;
        bool held = active && snapshot.primaryDown;
        bool released = active && snapshot.primaryReleased;
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

    Vector2 _rawDelta;

    bool _primaryDown;

    bool _primaryPressed;

    bool _primaryReleased;

    Vector2 _scrollDelta;

    bool _rawInputAvailable = true;

    public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot snapshot)
    {
        if (!TryUpdateRawInput())
        {
            snapshot = default;
            return false;
        }

        if (!NowUIInput.TryScreenToSurface(_rawPosition, surface, out var position))
        {
            snapshot = default;
            return false;
        }

        Vector2 delta = NowUIInput.ScaleScreenDelta(_rawDelta, surface);
        snapshot = new NowUIInputSnapshot(
            true,
            position,
            position - delta,
            delta,
            _primaryDown,
            _primaryPressed,
            _primaryReleased,
            _scrollDelta,
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

        try
        {
            Vector3 mousePosition = Input.mousePosition;
            var nextPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            _rawDelta = _hasRawPosition ? nextPosition - _rawPosition : Vector2.zero;
            _rawPosition = nextPosition;
            _hasRawPosition = true;
            _primaryDown = Input.GetMouseButton(0);
            _primaryPressed = Input.GetMouseButtonDown(0);
            _primaryReleased = Input.GetMouseButtonUp(0);
            _scrollDelta = Input.mouseScrollDelta;
            _rawInputAvailable = true;
            return _rawInputAvailable;
        }
        catch (InvalidOperationException)
        {
            _rawDelta = default;
            _primaryDown = false;
            _primaryPressed = false;
            _primaryReleased = false;
            _scrollDelta = default;
            _rawInputAvailable = false;
            return _rawInputAvailable;
        }
    }
}

public sealed class NowUIIMGUIInputProvider : INowUIInputProvider
{
    public static readonly NowUIIMGUIInputProvider instance = new NowUIIMGUIInputProvider();

    bool _primaryDown;

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

        bool pressed = current.type == EventType.MouseDown && current.button == 0;
        bool released = current.type == EventType.MouseUp && current.button == 0;

        if (pressed)
            _primaryDown = true;
        else if (released || current.type == EventType.MouseLeaveWindow)
            _primaryDown = false;
        else if (current.type == EventType.MouseDrag && current.button == 0)
            _primaryDown = true;

        Vector2 delta = NowUIInput.ScaleScreenDelta(current.delta, surface);
        Vector2 scrollDelta = current.type == EventType.ScrollWheel ? current.delta : Vector2.zero;

        snapshot = new NowUIInputSnapshot(
            true,
            position,
            position - delta,
            delta,
            _primaryDown,
            pressed,
            released,
            scrollDelta,
            Time.frameCount,
            Time.realtimeSinceStartup);
        return true;
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

        if (_rectTransform == null)
        {
            _snapshot = default;
            return;
        }

        Vector3 mousePosition;
        bool primaryDown;
        bool primaryPressed;
        bool primaryReleased;
        Vector2 scrollDelta;

        try
        {
            mousePosition = Input.mousePosition;
            primaryDown = Input.GetMouseButton(0);
            primaryPressed = Input.GetMouseButtonDown(0);
            primaryReleased = Input.GetMouseButtonUp(0);
            scrollDelta = Input.mouseScrollDelta;
        }
        catch (InvalidOperationException)
        {
            _snapshot = default;
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                mousePosition,
                _eventCamera,
                out var localPosition))
        {
            _snapshot = default;
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
            primaryDown,
            primaryPressed,
            primaryReleased,
            scrollDelta,
            Time.frameCount,
            Time.realtimeSinceStartup);
    }
}
