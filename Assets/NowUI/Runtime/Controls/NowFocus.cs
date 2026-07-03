using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NowUI
{
    /// <summary>
    /// Optional per-control focus links. Set only the directions you need; any
    /// unset or currently unregistered target falls back to the default resolver.
    /// </summary>
    public struct NowFocusNavigation
    {
        const byte LeftMask = 1 << 0;
        const byte RightMask = 1 << 1;
        const byte UpMask = 1 << 2;
        const byte DownMask = 1 << 3;
        const byte PreviousMask = 1 << 4;
        const byte NextMask = 1 << 5;

        NowId _left;
        NowId _right;
        NowId _up;
        NowId _down;
        NowId _previous;
        NowId _next;
        byte _mask;

        public static NowFocusNavigation None => default;

        public static NowFocusNavigation Left(NowId id) => default(NowFocusNavigation).SetLeft(id);

        public static NowFocusNavigation Right(NowId id) => default(NowFocusNavigation).SetRight(id);

        public static NowFocusNavigation Up(NowId id) => default(NowFocusNavigation).SetUp(id);

        public static NowFocusNavigation Down(NowId id) => default(NowFocusNavigation).SetDown(id);

        public static NowFocusNavigation Previous(NowId id) => default(NowFocusNavigation).SetPrevious(id);

        public static NowFocusNavigation Next(NowId id) => default(NowFocusNavigation).SetNext(id);

        public NowFocusNavigation SetLeft(NowId id) { _left = id; SetMask(LeftMask, id.hasValue); return this; }

        public NowFocusNavigation SetRight(NowId id) { _right = id; SetMask(RightMask, id.hasValue); return this; }

        public NowFocusNavigation SetUp(NowId id) { _up = id; SetMask(UpMask, id.hasValue); return this; }

        public NowFocusNavigation SetDown(NowId id) { _down = id; SetMask(DownMask, id.hasValue); return this; }

        public NowFocusNavigation SetPrevious(NowId id) { _previous = id; SetMask(PreviousMask, id.hasValue); return this; }

        public NowFocusNavigation SetNext(NowId id) { _next = id; SetMask(NextMask, id.hasValue); return this; }

        void SetMask(byte mask, bool enabled)
        {
            if (enabled)
                _mask |= mask;
            else
                _mask &= (byte)~mask;
        }

        internal ResolvedFocusNavigation Resolve()
        {
            var resolved = default(ResolvedFocusNavigation);

            if ((_mask & LeftMask) != 0)
                resolved.SetLeft(NowControls.ResolveNavigationTargetId(_left));

            if ((_mask & RightMask) != 0)
                resolved.SetRight(NowControls.ResolveNavigationTargetId(_right));

            if ((_mask & UpMask) != 0)
                resolved.SetUp(NowControls.ResolveNavigationTargetId(_up));

            if ((_mask & DownMask) != 0)
                resolved.SetDown(NowControls.ResolveNavigationTargetId(_down));

            if ((_mask & PreviousMask) != 0)
                resolved.SetPrevious(NowControls.ResolveNavigationTargetId(_previous));

            if ((_mask & NextMask) != 0)
                resolved.SetNext(NowControls.ResolveNavigationTargetId(_next));

            return resolved;
        }
    }

    internal struct ResolvedFocusNavigation
    {
        const byte LeftMask = 1 << 0;
        const byte RightMask = 1 << 1;
        const byte UpMask = 1 << 2;
        const byte DownMask = 1 << 3;
        const byte PreviousMask = 1 << 4;
        const byte NextMask = 1 << 5;

        int _left;
        int _right;
        int _up;
        int _down;
        int _previous;
        int _next;
        byte _mask;

        public void SetLeft(int id) { _left = id; SetMask(LeftMask, id != 0); }

        public void SetRight(int id) { _right = id; SetMask(RightMask, id != 0); }

        public void SetUp(int id) { _up = id; SetMask(UpMask, id != 0); }

        public void SetDown(int id) { _down = id; SetMask(DownMask, id != 0); }

        public void SetPrevious(int id) { _previous = id; SetMask(PreviousMask, id != 0); }

        public void SetNext(int id) { _next = id; SetMask(NextMask, id != 0); }

        void SetMask(byte mask, bool enabled)
        {
            if (enabled)
                _mask |= mask;
            else
                _mask &= (byte)~mask;
        }

        public bool TryGetDirectional(Vector2 direction, out int id)
        {
            if (direction.x < -0.5f && (_mask & LeftMask) != 0)
            {
                id = _left;
                return true;
            }

            if (direction.x > 0.5f && (_mask & RightMask) != 0)
            {
                id = _right;
                return true;
            }

            if (direction.y < -0.5f && (_mask & UpMask) != 0)
            {
                id = _up;
                return true;
            }

            if (direction.y > 0.5f && (_mask & DownMask) != 0)
            {
                id = _down;
                return true;
            }

            id = 0;
            return false;
        }

        public bool TryGetOrder(int step, out int id)
        {
            if (step < 0 && (_mask & PreviousMask) != 0)
            {
                id = _previous;
                return true;
            }

            if (step > 0 && (_mask & NextMask) != 0)
            {
                id = _next;
                return true;
            }

            id = 0;
            return false;
        }
    }

    internal struct NowFocusScrollRegionScope : System.IDisposable
    {
        bool _active;

        internal NowFocusScrollRegionScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            NowFocus.PopScrollRegion();
        }
    }

    /// <summary>
    /// Keyboard/gamepad focus for immediate-mode controls. Focusable controls
    /// register their rect every frame as they draw; navigation resolves spatially
    /// against the previous frame's registry (immediate mode has no widget tree).
    /// Pointer interaction focuses controls explicitly (<see cref="Focus"/>), the
    /// navigation vector moves focus directionally with a sticky cross-axis
    /// anchor (repeated moves hold the starting row/column across offset
    /// intermediate controls), Tab cycles by draw order,
    /// cancel clears it, and
    /// <see cref="SubmitPressed"/> lets the focused control activate from
    /// keyboard/gamepad submit.
    /// </summary>
    public static class NowFocus
    {
        const float NavigationThreshold = 0.55f;

        const float NavigationRepeatDelay = 0.4f;

        const float NavigationRepeatInterval = 0.12f;

        struct Focusable
        {
            public int id;
            public Rect rect;
            public Rect visibleRect;
            public int scrollRegionId;
            public int overlayLayerId;
            public ResolvedFocusNavigation navigation;
        }

        static readonly List<int> _scrollRegionStack = new List<int>(4);

        static readonly List<Focusable> _current = new List<Focusable>(32);

        static readonly List<Focusable> _previous = new List<Focusable>(32);

        static int _focusedId;

        static int _focusRevision;

        static int _registryFrame = -1;

        static int _navigationLockFrame = -1;

        static int _retainFocusFrame = -1;

        static Vector2 _lastNavigation;

        static Vector2 _repeatDirection;

        static float _nextNavigationRepeatTime;

        static Vector2 _navigationMemory;

        static Vector2 _navigationMemoryFocusedCenter;

        static bool _hasNavigationMemory;

        static int _navigationMemoryRevision;

        /// <summary>
        /// Keeps NowUI focus and Unity's EventSystem selection mutually exclusive
        /// (default on): selecting a UGUI control clears NowUI focus and pauses
        /// NowUI navigation, and focusing a NowUI control deselects the EventSystem.
        /// Cross-system navigation handoff is not attempted.
        /// </summary>
        public static bool respectEventSystem = true;

        /// <summary>The focused control id, or 0 when nothing has focus.</summary>
        public static int focusedId => _focusedId;

        internal static int focusRevision => _focusRevision;

        public static bool IsFocused(int id)
        {
            return id != 0 && _focusedId == id && IsFocusedInActiveLayer(id);
        }

        public static void Focus(int id)
        {
            SetFocused(id);

            if (respectEventSystem && id != 0)
            {
                var eventSystem = EventSystem.current;

                if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                    eventSystem.SetSelectedGameObject(null);
            }
        }

        public static void Clear()
        {
            SetFocused(0);
        }

        static void SetFocused(int id)
        {
            if (_focusedId == id)
                return;

            _focusedId = id;

            unchecked
            {
                ++_focusRevision;

                if (_focusRevision == 0)
                    _focusRevision = 1;
            }
        }

        /// <summary>
        /// Adds a control to this frame's focus registry. Call every frame from the
        /// control's draw, after input interaction; ignored during layout measure
        /// passes so callback-form layouts don't register twice.
        /// </summary>
        public static void Register(int id, NowRect rect)
        {
            Register(id, rect, default);
        }

        /// <summary>
        /// Adds a control to this frame's focus registry with optional explicit
        /// directional/Tab navigation targets.
        /// </summary>
        public static void Register(int id, NowRect rect, NowFocusNavigation navigation)
        {
            if (id == 0 || NowInput.isPassive || rect.isEmpty)
                return;

            NowRect visibleRect = Now.ApplyAmbientMask(rect);
            int scrollRegionId = CurrentScrollRegionId();

            if (visibleRect.isEmpty && scrollRegionId == 0)
                return;

            BeginFrameIfNeeded();
            _current.Add(new Focusable
            {
                id = id,
                rect = scrollRegionId != 0 ? (Rect)rect : (Rect)visibleRect,
                visibleRect = (Rect)visibleRect,
                scrollRegionId = scrollRegionId,
                overlayLayerId = NowOverlay.currentFocusLayerId,
                navigation = navigation.Resolve()
            });
        }

        internal static NowFocusScrollRegionScope BeginScrollRegion(int id)
        {
            if (id == 0 || NowInput.isPassive)
                return new NowFocusScrollRegionScope(false);

            BeginFrameIfNeeded();
            _scrollRegionStack.Add(id);
            return new NowFocusScrollRegionScope(true);
        }

        internal static void PopScrollRegion()
        {
            if (_scrollRegionStack.Count > 0)
                _scrollRegionStack.RemoveAt(_scrollRegionStack.Count - 1);
        }

        static int CurrentScrollRegionId()
        {
            return _scrollRegionStack.Count > 0 ? _scrollRegionStack[_scrollRegionStack.Count - 1] : 0;
        }

        /// <summary>The innermost scroll region enclosing the current draw position, or 0.</summary>
        internal static int currentScrollRegionId => CurrentScrollRegionId();

        internal static bool TryGetFocusedRectInScrollRegion(int scrollRegionId, out NowRect rect)
        {
            rect = default;

            if (scrollRegionId == 0 || _focusedId == 0 || NowInput.isPassive)
                return false;

            BeginFrameIfNeeded();
            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (TryGetFocusedRectInScrollRegion(_previous, scrollRegionId, activeLayerId, out rect))
                return true;

            return TryGetFocusedRectInScrollRegion(_current, scrollRegionId, activeLayerId, out rect);
        }

        static bool TryGetFocusedRectInScrollRegion(List<Focusable> focusables, int scrollRegionId, int activeLayerId, out NowRect rect)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == _focusedId &&
                    focusables[i].scrollRegionId == scrollRegionId &&
                    IsFocusableInLayer(focusables[i], activeLayerId))
                {
                    rect = (NowRect)focusables[i].rect;
                    return true;
                }
            }

            rect = default;
            return false;
        }

        /// <summary>
        /// True when the focused control should activate from submit (enter/space/
        /// gamepad south) this frame.
        /// </summary>
        public static bool SubmitPressed(int id)
        {
            return IsFocused(id) && !NowInput.isPassive && NowInput.current.submitPressed;
        }

        /// <summary>
        /// Suppresses focus navigation while the focused control consumes
        /// keyboard/gamepad input itself — a text field's arrows move the caret
        /// and WASD types characters, neither should move focus. Call every
        /// frame from the focused control's draw; effective on the next frame
        /// swap, like registration.
        /// </summary>
        public static void LockNavigation()
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _navigationLockFrame = Time.frameCount;
        }

        /// <summary>
        /// Keeps pointer presses from clearing focus this frame. Modal overlays
        /// that act on focus-owned state without taking focus themselves — a
        /// context menu over a text selection — call this every frame while
        /// open, so pressing their rows (or dismissing them with a press
        /// outside) leaves the owner focused and its selection alive. Effective
        /// on the next frame swap, like registration.
        /// </summary>
        public static void RetainFocus()
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _retainFocusFrame = Time.frameCount;
        }

        static void BeginFrameIfNeeded()
        {
            int frame = Time.frameCount;

            if (_registryFrame == frame)
                return;

            _registryFrame = frame;

            _previous.Clear();
            _previous.AddRange(_current);
            _current.Clear();
            ProcessNavigation();
        }

        /// <summary>Forces the frame swap; used by tests where frameCount is static.</summary>
        internal static void ForceNewFrame()
        {
            _registryFrame = -1;
            BeginFrameIfNeeded();
            _registryFrame = -1;
        }

        static void ProcessNavigation()
        {
            var snapshot = NowInput.current;
            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (respectEventSystem)
            {
                var eventSystem = EventSystem.current;

                if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                {
                    Clear();
                    _lastNavigation = snapshot.navigation;
                    return;
                }
            }

            if (snapshot.cancelPressed && !NowInput.cancelConsumedForFrameSwap)
            {
                Clear();
                _lastNavigation = snapshot.navigation;
                ResetNavigationRepeat();
                return;
            }

            if (snapshot.primaryPressed && _focusedId != 0 && _retainFocusFrame < Time.frameCount - 1)
            {
                bool overControl = false;

                for (int i = 0; i < _previous.Count; ++i)
                {
                    if (IsFocusableInLayer(_previous[i], activeLayerId) &&
                        _previous[i].visibleRect.width > 0f &&
                        _previous[i].visibleRect.height > 0f &&
                        _previous[i].visibleRect.Contains(snapshot.pointerPosition))
                    {
                        overControl = true;
                        break;
                    }
                }

                if (!overControl)
                {
                    Clear();
                    _lastNavigation = snapshot.navigation;
                    ResetNavigationRepeat();
                    return;
                }
            }

            Vector2 navigation = snapshot.navigation;
            bool navigationLocked = _navigationLockFrame >= Time.frameCount - 1;

            if (navigationLocked)
            {
                _lastNavigation = navigation;
                ResetNavigationRepeat();
                return;
            }

            if (snapshot.focusPreviousPressed || snapshot.focusNextPressed)
            {
                MoveFocusInRegistrationOrder(snapshot.focusPreviousPressed ? -1 : 1, activeLayerId);
                _lastNavigation = navigation;
                ResetNavigationRepeat();
                return;
            }

            Vector2 direction = GetNavigationPulse(navigation, snapshot.time);

            if (direction == default || !HasFocusableInLayer(activeLayerId))
                return;

            MoveFocus(direction, activeLayerId);
        }

        static Vector2 GetNavigationPulse(Vector2 navigation, float time)
        {
            Vector2 direction = ResolveNavigationDirection(navigation);
            Vector2 previousDirection = ResolveNavigationDirection(_lastNavigation);
            _lastNavigation = navigation;

            if (direction == default)
            {
                ResetNavigationRepeat();
                return default;
            }

            NowControlState.RequestRepaint();

            if (direction != previousDirection || direction != _repeatDirection)
            {
                _repeatDirection = direction;
                _nextNavigationRepeatTime = time + NavigationRepeatDelay;
                return direction;
            }

            if (time >= _nextNavigationRepeatTime)
            {
                _nextNavigationRepeatTime = time + NavigationRepeatInterval;
                return direction;
            }

            return default;
        }

        static Vector2 ResolveNavigationDirection(Vector2 navigation)
        {
            float x = Mathf.Abs(navigation.x);
            float y = Mathf.Abs(navigation.y);

            if (x <= NavigationThreshold && y <= NavigationThreshold)
                return default;

            if (x >= y)
                return new Vector2(Mathf.Sign(navigation.x), 0f);

            // Navigation y+ means "up"; focus rect space is y-down screen coords.
            return new Vector2(0f, -Mathf.Sign(navigation.y));
        }

        static void ResetNavigationRepeat()
        {
            _repeatDirection = default;
            _nextNavigationRepeatTime = 0f;
        }

        static void MoveFocusInRegistrationOrder(int step, int activeLayerId)
        {
            if (!HasFocusableInLayer(activeLayerId))
                return;

            int focusedIndex = -1;
            int fallbackIndex = -1;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (!IsFocusableInLayer(_previous[i], activeLayerId))
                    continue;

                if (fallbackIndex < 0 || step < 0)
                    fallbackIndex = i;

                if (_previous[i].id == _focusedId)
                {
                    focusedIndex = i;
                    break;
                }
            }

            if (focusedIndex < 0)
            {
                SetFocused(_previous[fallbackIndex].id);
                return;
            }

            if (_previous[focusedIndex].navigation.TryGetOrder(step, out int targetId) &&
                TryFocusRegistered(targetId, activeLayerId))
            {
                return;
            }

            int next = FindNextFocusableIndex(focusedIndex, step, activeLayerId);

            if (next >= 0)
                SetFocused(_previous[next].id);
        }

        static void MoveFocus(Vector2 direction, int activeLayerId)
        {
            int focusedIndex = -1;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (IsFocusableInLayer(_previous[i], activeLayerId) &&
                    _previous[i].id == _focusedId)
                {
                    focusedIndex = i;
                    break;
                }
            }

            if (focusedIndex < 0)
            {
                SetFocused(FindEdgeFocus(direction, activeLayerId));
                return;
            }

            if (_previous[focusedIndex].navigation.TryGetDirectional(direction, out int targetId) &&
                TryFocusRegistered(targetId, activeLayerId, out Rect targetRect))
            {
                SetNavigationMemory(targetRect.center, targetRect.center);
                return;
            }

            bool vertical = direction.y != 0f;
            Vector2 origin = _previous[focusedIndex].rect.center;

            if (_hasNavigationMemory && _navigationMemoryRevision == _focusRevision)
            {
                Vector2 anchor = _navigationMemory + (origin - _navigationMemoryFocusedCenter);

                if (vertical)
                    origin.x = anchor.x;
                else
                    origin.y = anchor.y;
            }

            float bestScore = float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (i == focusedIndex || !IsFocusableInLayer(_previous[i], activeLayerId))
                    continue;

                Vector2 toCandidate = _previous[i].rect.center - origin;
                float along = Vector2.Dot(toCandidate, direction);

                if (along <= 0.5f)
                    continue;

                float sideways = (toCandidate - direction * along).magnitude;
                float score = along + sideways * 2.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                SetFocused(_previous[bestIndex].id);

                Vector2 focusedCenter = _previous[bestIndex].rect.center;
                Vector2 memory = origin;

                if (vertical)
                    memory.y = focusedCenter.y;
                else
                    memory.x = focusedCenter.x;

                SetNavigationMemory(memory, focusedCenter);
            }
        }

        /// <summary>
        /// Records the virtual cursor after a directional move: the along-axis
        /// coordinate follows the newly focused control while the cross-axis
        /// anchor persists, so repeated moves stay in the starting row/column
        /// even when an intermediate control is offset. The focused center is
        /// stored alongside so the anchor can be translated by however much the
        /// focused rect has moved since — scrolling shifts registered rects in
        /// screen space, and the anchor must shift with them. Stamped with the
        /// focus revision — any non-directional focus change invalidates it.
        /// </summary>
        static void SetNavigationMemory(Vector2 position, Vector2 focusedCenter)
        {
            _navigationMemory = position;
            _navigationMemoryFocusedCenter = focusedCenter;
            _hasNavigationMemory = true;
            _navigationMemoryRevision = _focusRevision;
        }

        static bool TryFocusRegistered(int id, int activeLayerId)
        {
            return TryFocusRegistered(id, activeLayerId, out _);
        }

        static bool TryFocusRegistered(int id, int activeLayerId, out Rect rect)
        {
            rect = default;

            if (id == 0)
                return false;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (_previous[i].id == id && IsFocusableInLayer(_previous[i], activeLayerId))
                {
                    SetFocused(id);
                    rect = _previous[i].rect;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Seeds focus at the edge opposite the pressed direction. Controls
        /// visible in the viewport win over ones registered through a scroll
        /// region but currently clipped away — seeding should land where the
        /// user is looking, not yank the scroll to a far-off control.
        /// </summary>
        static int FindEdgeFocus(Vector2 direction, int activeLayerId)
        {
            float bestVisibleScore = float.MaxValue;
            int bestVisibleId = 0;
            float bestScore = float.MaxValue;
            int bestId = 0;
            int fallbackId = 0;

            for (int i = 0; i < _previous.Count; ++i)
            {
                if (!IsFocusableInLayer(_previous[i], activeLayerId))
                    continue;

                if (fallbackId == 0)
                    fallbackId = _previous[i].id;

                float score = Vector2.Dot(_previous[i].rect.center, direction);

                if (_previous[i].visibleRect.width > 0f &&
                    _previous[i].visibleRect.height > 0f &&
                    score < bestVisibleScore)
                {
                    bestVisibleScore = score;
                    bestVisibleId = _previous[i].id;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestId = _previous[i].id;
                }
            }

            if (bestVisibleId != 0)
                return bestVisibleId;

            return bestId != 0 ? bestId : fallbackId;
        }

        static bool IsFocusedInActiveLayer(int id)
        {
            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (activeLayerId == 0)
                return true;

            return ContainsFocusableInLayer(_current, id, activeLayerId) ||
                ContainsFocusableInLayer(_previous, id, activeLayerId);
        }

        static bool ContainsFocusableInLayer(List<Focusable> focusables, int id, int activeLayerId)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id && IsFocusableInLayer(focusables[i], activeLayerId))
                    return true;
            }

            return false;
        }

        static bool HasFocusableInLayer(int activeLayerId)
        {
            for (int i = 0; i < _previous.Count; ++i)
            {
                if (IsFocusableInLayer(_previous[i], activeLayerId))
                    return true;
            }

            return false;
        }

        static int FindNextFocusableIndex(int focusedIndex, int step, int activeLayerId)
        {
            int count = _previous.Count;

            for (int offset = 1; offset <= count; ++offset)
            {
                int next = (focusedIndex + offset * step) % count;

                if (next < 0)
                    next += count;

                if (IsFocusableInLayer(_previous[next], activeLayerId))
                    return next;
            }

            return -1;
        }

        static bool IsFocusableInLayer(Focusable focusable, int activeLayerId)
        {
            return focusable.overlayLayerId == activeLayerId;
        }

        public static void Reset()
        {
            _current.Clear();
            _previous.Clear();
            _scrollRegionStack.Clear();
            _focusedId = 0;
            _focusRevision = 0;
            _registryFrame = -1;
            _navigationLockFrame = -1;
            _retainFocusFrame = -1;
            _lastNavigation = default;
            _navigationMemory = default;
            _navigationMemoryFocusedCenter = default;
            _hasNavigationMemory = false;
            _navigationMemoryRevision = 0;
            ResetNavigationRepeat();
            respectEventSystem = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
