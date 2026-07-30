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

    /// <summary>
    /// Describes which focus-navigation inputs a control owns while focused.
    /// </summary>
    public enum NowFocusNavigationLock
    {
        None = 0,
        Directional = 1,
        All = 2
    }

    internal enum NowFocusMoveResult
    {
        Unavailable = 0,
        Consumed = 1,
        Moved = 2,
        Boundary = 3,
        Seeded = 4
    }

    internal struct NowFocusHostRegistrationScope : System.IDisposable
    {
        readonly int _token;

        bool _disposed;

        internal NowFocusHostRegistrationScope(int token)
        {
            _token = token;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowFocus.EndHostRegistration(_token);
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
            public NowFocusNavigationLock navigationLock;
            public bool consumesCancel;
        }

        sealed class HostRegistry
        {
            public readonly int hostId;

            public List<Focusable> focusables = new List<Focusable>(32);

            public List<Focusable> buildingFocusables = new List<Focusable>(32);

            public Dictionary<int, int> owners = new Dictionary<int, int>(16);

            public Dictionary<int, int> buildingOwners = new Dictionary<int, int>(16);

            public NowUGUINavigationProxy proxy;

            public NowFocusNavigationLock claimedNavigationLock;

            public int claimedNavigationLockFocusRevision;

            public NowFocusNavigationLock buildingNavigationLock;

            public int buildingNavigationLockFocusRevision;

            public int pendingCancelOwnerId;

            public bool hasPendingEntry;

            public Vector2 pendingEntryDirection;

            public int pendingEntryOrderStep;

            public int pendingTabBoundaryStep;

            public int pendingTabFocusId;

            public int pendingTabFocusRevision;

            public bool hasPendingDirectionalBoundary;

            public Vector2 pendingDirectionalBoundary;

            public int pendingDirectionalBoundaryFocusId;

            public int pendingDirectionalBoundaryFocusRevision;

            public ulong pendingDirectionalBoundaryRegistrationVersion;

            public ulong registrationVersion;

            public int directionalReturnId;

            public bool retainFocus;

            public bool buildingRetainFocus;

            public bool isRegistering;

            public bool unregisterPending;

            public int lastProcessedInputFrame = int.MinValue;

            public Vector2 lastNavigation;

            public Vector2 repeatDirection;

            public float nextNavigationRepeatTime;

            public HostRegistry(int hostId)
            {
                this.hostId = hostId;
            }

            public void BeginRegistration()
            {
                buildingFocusables.Clear();
                buildingOwners.Clear();
                buildingNavigationLock = NowFocusNavigationLock.None;
                buildingNavigationLockFocusRevision = 0;
                buildingRetainFocus = false;
                isRegistering = true;
            }

            public void EndRegistration()
            {
                var previousFocusables = focusables;
                focusables = buildingFocusables;
                buildingFocusables = previousFocusables;

                var previousOwners = owners;
                owners = buildingOwners;
                buildingOwners = previousOwners;

                claimedNavigationLock = buildingNavigationLock;
                claimedNavigationLockFocusRevision = buildingNavigationLockFocusRevision;
                retainFocus = buildingRetainFocus;
                isRegistering = false;
                ++registrationVersion;
            }
        }

        static readonly List<int> _scrollRegionStack = new List<int>(4);

        static readonly List<Focusable> _current = new List<Focusable>(32);

        static readonly List<Focusable> _previous = new List<Focusable>(32);

        static readonly Dictionary<int, HostRegistry> _hostRegistries =
            new Dictionary<int, HostRegistry>(4);

        static readonly List<HostRegistry> _hostRegistrationStack =
            new List<HostRegistry>(2);

        static readonly NowScopeGuard _hostRegistrationScopes =
            new NowScopeGuard("NowFocus.BeginHostRegistration", 2);

        static int _focusedId;

        static int _focusedHostId;

        static int _focusRevision;

        static int _registryFrame = -1;

        static NowFocusNavigationLock _navigationLockCurrent;

        static int _navigationLockCurrentFocusRevision;

        static NowFocusNavigationLock _navigationLockPrevious;

        static int _navigationLockPreviousFocusRevision;

        static int _explicitFocusRequestId;

        static int _explicitFocusRequestHostId;

        static int _pendingCancelOwnerId;

        static bool _preserveInputClaimsOnNextSwap;

        static bool _retainFocusCurrent;

        static bool _retainFocusPrevious;

        static Vector2 _lastNavigation;

        static Vector2 _repeatDirection;

        static float _nextNavigationRepeatTime;

        static Vector2 _navigationMemory;

        static Vector2 _navigationMemoryFocusedCenter;

        static bool _hasNavigationMemory;

        static int _navigationMemoryRevision;

        /// <summary>
        /// Coordinates NowUI focus with Unity's EventSystem (default on).
        /// Without a navigation proxy the systems are mutually exclusive. A
        /// <see cref="NowUGUINavigationProxy"/> instead remains selected while
        /// its host owns focus and delegates only true boundary moves to UGUI.
        /// </summary>
        public static bool respectEventSystem = true;

        /// <summary>The focused control id, or 0 when nothing has focus.</summary>
        public static int focusedId => _focusedId;

        internal static int focusRevision => _focusRevision;

        internal static bool IsFocusedInHost(int hostId)
        {
            return hostId != 0 && _focusedId != 0 && _focusedHostId == hostId;
        }

        internal static bool IsFocusedOutsideHost(int hostId)
        {
            return _focusedId != 0 && _focusedHostId != hostId;
        }

        internal static void PrepareUGUIEntry(int hostId)
        {
            if (!IsFocusedOutsideHost(hostId))
                return;

            bool preserveExplicitTransfer =
                _explicitFocusRequestId != 0 &&
                _explicitFocusRequestId == _focusedId &&
                _explicitFocusRequestHostId == _focusedHostId;
            HostRegistry focusedHost = GetHostRegistry(_focusedHostId);

            // A focus request made by another OnSelect handler cannot select
            // its owning proxy reentrantly. Keep that explicit request until
            // the proxy's queued LateUpdate transfer can complete. Otherwise
            // the foreign focus belongs to the previously selected proxy and
            // must not prevent this host from seeding its own control.
            if (preserveExplicitTransfer &&
                focusedHost != null &&
                focusedHost.proxy != null &&
                focusedHost.proxy.hasPendingSelection)
            {
                return;
            }

            Clear();
        }

        public static bool IsFocused(int id)
        {
            return id != 0 && _focusedId == id && IsFocusedInActiveLayer(id);
        }

        static readonly Dictionary<int, int> _ownersCurrent = new Dictionary<int, int>(16);

        static readonly Dictionary<int, int> _ownersPrevious = new Dictionary<int, int>(16);

        /// <summary>
        /// Declares that a control or overlay layer belongs to an owner for
        /// <see cref="IsFocusedWithin"/>. Call every interactive frame while
        /// the relationship exists, like <see cref="Register"/> — an editor
        /// declares its inline rename field, a control declares the context
        /// menu it opened, a menu declares its submenu overlays.
        /// </summary>
        public static void DeclareOwner(int id, int ownerId)
        {
            if (id == 0 || ownerId == 0 || id == ownerId || NowInput.isPassive)
                return;

            HostRegistry host = ActiveHostRegistry();

            if (host != null)
            {
                host.buildingOwners[id] = ownerId;
                return;
            }

            BeginFrameIfNeeded();
            _ownersCurrent[id] = ownerId;
        }

        /// <summary>
        /// Focus-within: true when this control is focused, when focus sits on
        /// a control it owns (transitively, via <see cref="DeclareOwner"/>), or
        /// when the active overlay focus layer belongs to it. This is what
        /// visuals should test — a parent whose inline field, popup or context
        /// menu is active keeps rendering focused instead of blinking through
        /// every internal handoff.
        /// </summary>
        public static bool IsFocusedWithin(int id)
        {
            if (id == 0)
                return false;

            int hostId = ActiveHostRegistry()?.hostId ?? _focusedHostId;

            if (OwnerChainReaches(_focusedId, id, hostId))
                return true;

            int layerId = NowOverlay.activeFocusLayerId;
            return layerId != 0 && OwnerChainReaches(layerId, id, hostId);
        }

        static bool OwnerChainReaches(int cursor, int id, int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            for (int depth = 0; cursor != 0 && depth < 8; ++depth)
            {
                if (cursor == id)
                    return true;

                int owner;
                bool found = host != null
                    ? ((host.isRegistering &&
                        host.buildingOwners.TryGetValue(cursor, out owner)) ||
                       host.owners.TryGetValue(cursor, out owner))
                    : (_ownersCurrent.TryGetValue(cursor, out owner) ||
                       _ownersPrevious.TryGetValue(cursor, out owner));

                if (!found)
                {
                    return false;
                }

                cursor = owner;
            }

            return false;
        }

        public static void Focus(int id)
        {
            if (id != 0)
                NowInput.ClaimFocusForCurrentPrimaryPress();

            int hostId = ResolveFocusHost(id);

            if (_focusedId != id || _focusedHostId != hostId)
                SetFocused(id, hostId);

            _explicitFocusRequestId = id;
            _explicitFocusRequestHostId = hostId;

            if (respectEventSystem && id != 0)
            {
                var eventSystem = EventSystem.current;

                if (!TrySelectOwningProxy(hostId, eventSystem) &&
                    eventSystem != null &&
                    eventSystem.currentSelectedGameObject != null &&
                    !IsOwningProxySelection(hostId, eventSystem.currentSelectedGameObject))
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            }
        }

        public static void Clear()
        {
            _explicitFocusRequestId = 0;
            _explicitFocusRequestHostId = 0;
            SetFocused(0, 0);
        }

        internal static bool ClearOnUnhandledPrimaryPress()
        {
            if (_focusedId == 0)
                return false;

            HostRegistry host = ActiveHostRegistry();
            int inputHostId = host != null ? host.hostId : 0;

            // An input surface may finish while another retained host owns
            // focus. Only the owning host (or the shared immediate-mode host)
            // may clear it.
            if (_focusedHostId != inputHostId)
                return false;

            bool retainFocus = host != null
                ? host.retainFocus || host.buildingRetainFocus
                : _retainFocusPrevious || _retainFocusCurrent;

            if (retainFocus)
                return false;

            Clear();
            NowControlState.RequestRepaint();
            return true;
        }

        static void SetFocused(int id)
        {
            SetFocused(id, ResolveFocusHost(id));
        }

        static void SetFocused(int id, int hostId)
        {
            if (_focusedId == id && _focusedHostId == hostId)
                return;

            _focusedId = id;
            _focusedHostId = id != 0 ? hostId : 0;

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
        /// passes so exact-measure hosts and <c>NowLayout.RunMeasured</c>
        /// do not register twice.
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
            Register(id, rect, navigation, NowFocusNavigationLock.None);
        }

        /// <summary>
        /// Adds a control with the navigation and cancel inputs it owns while focused.
        /// </summary>
        public static void Register(int id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel = false)
        {
            if (id == 0 || NowInput.isPassive || rect.isEmpty)
                return;

            if (NowInput.current.primaryPressed && NowInput.IsHovered(rect))
                NowInput.ClaimFocusForCurrentPrimaryPress();

            NowRect visibleRect = Now.ApplyAmbientMask(rect);
            int scrollRegionId = CurrentScrollRegionId();

            if (visibleRect.isEmpty && scrollRegionId == 0)
                return;

            var focusable = new Focusable
            {
                id = id,
                rect = scrollRegionId != 0 ? (Rect)rect : (Rect)visibleRect,
                visibleRect = (Rect)visibleRect,
                scrollRegionId = scrollRegionId,
                overlayLayerId = NowOverlay.currentFocusLayerId,
                navigation = navigation.Resolve(),
                navigationLock = navigationLock,
                consumesCancel = consumesCancel
            };

            HostRegistry host = ActiveHostRegistry();

            if (host != null)
            {
                if (_focusedId == id && _focusedHostId == 0)
                {
                    SetFocused(id, host.hostId);

                    if (_explicitFocusRequestId == id && _explicitFocusRequestHostId == 0)
                        _explicitFocusRequestHostId = host.hostId;

                    if (respectEventSystem)
                        TrySelectOwningProxy(host.hostId, EventSystem.current);
                }

                host.buildingFocusables.Add(focusable);
                return;
            }

            BeginFrameIfNeeded();
            _current.Add(focusable);
        }

        internal static NowFocusScrollRegionScope BeginScrollRegion(int id)
        {
            if (id == 0 || NowInput.isPassive)
                return new NowFocusScrollRegionScope(false);

            if (ActiveHostRegistry() == null)
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

            HostRegistry host = ActiveHostRegistry() ?? GetHostRegistry(_focusedHostId);

            if (host == null)
                BeginFrameIfNeeded();

            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (host != null)
            {
                if (host.isRegistering &&
                    TryGetFocusedRectInScrollRegion(
                    host.buildingFocusables, scrollRegionId, activeLayerId, out rect))
                {
                    return true;
                }

                return TryGetFocusedRectInScrollRegion(
                    host.focusables, scrollRegionId, activeLayerId, out rect);
            }

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
        /// Suppresses all focus navigation, including Tab, while the focused
        /// control consumes it itself. Call every frame from the focused
        /// control's draw; effective on the next frame swap, like registration.
        /// </summary>
        public static void LockNavigation()
        {
            ClaimNavigationLock(NowFocusNavigationLock.All);
        }

        /// <summary>
        /// Suppresses directional focus navigation (arrows, W/A/S/D, d-pad and
        /// stick) while leaving Tab traversal available. Call every frame from
        /// the focused control's draw; effective on the next frame swap, like
        /// registration.
        /// </summary>
        public static void LockDirectionalNavigation()
        {
            ClaimNavigationLock(NowFocusNavigationLock.Directional);
        }

        static void ClaimNavigationLock(NowFocusNavigationLock navigationLock)
        {
            if (NowInput.isPassive)
                return;

            int focusRevision = _focusRevision;
            HostRegistry host = ActiveHostRegistry();

            if (host != null)
            {
                ClaimHostNavigationLock(host, navigationLock, focusRevision);
                return;
            }

            ClaimNavigationLock(navigationLock, focusRevision);
            BeginFrameIfNeeded();

            if (_focusRevision == focusRevision)
                ClaimNavigationLock(navigationLock, focusRevision);
        }

        static void ClaimHostNavigationLock(
            HostRegistry host,
            NowFocusNavigationLock navigationLock,
            int focusRevision)
        {
            if (host.buildingNavigationLockFocusRevision != focusRevision)
            {
                host.buildingNavigationLockFocusRevision = focusRevision;
                host.buildingNavigationLock = NowFocusNavigationLock.None;
            }

            if (navigationLock > host.buildingNavigationLock)
                host.buildingNavigationLock = navigationLock;
        }

        static void ClaimNavigationLock(NowFocusNavigationLock navigationLock, int focusRevision)
        {
            if (_navigationLockCurrentFocusRevision != focusRevision)
            {
                _navigationLockCurrentFocusRevision = focusRevision;
                _navigationLockCurrent = NowFocusNavigationLock.None;
            }

            if (navigationLock > _navigationLockCurrent)
                _navigationLockCurrent = navigationLock;
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

            HostRegistry host = ActiveHostRegistry();

            if (host != null)
            {
                host.buildingRetainFocus = true;
                return;
            }

            _retainFocusCurrent = true;
            BeginFrameIfNeeded();
            _retainFocusCurrent = true;
        }

        internal static NowFocusHostRegistrationScope BeginHostRegistration(
            int hostId,
            NowUGUINavigationProxy proxy)
        {
            if (hostId == 0)
                throw new System.ArgumentException("Focus host id 0 is reserved.", nameof(hostId));

            HostRegistry host = GetOrCreateHostRegistry(hostId);

            if (host.isRegistering)
            {
                throw new System.InvalidOperationException(
                    $"NowFocus host {hostId} is already registering controls.");
            }

            host.proxy = proxy;
            int token = _hostRegistrationScopes.Enter();
            _hostRegistrationStack.Add(host);

            try
            {
                ProcessHostNavigationIfNeeded(host);
                host.BeginRegistration();
                return new NowFocusHostRegistrationScope(token);
            }
            catch
            {
                _hostRegistrationStack.RemoveAt(_hostRegistrationStack.Count - 1);
                _hostRegistrationScopes.Exit(token);
                throw;
            }
        }

        internal static void EndHostRegistration(int token)
        {
            if (!_hostRegistrationScopes.BeginEnd(token))
                return;

            HostRegistry host = _hostRegistrationStack[_hostRegistrationStack.Count - 1];

            try
            {
                host.EndRegistration();

                if (host.unregisterPending)
                {
                    if (_focusedHostId == host.hostId)
                        Clear();

                    _hostRegistries.Remove(host.hostId);
                }
                else
                {
                    CompletePendingHostEntry(host);
                    ResolvePendingHostTabBoundary(host);
                    ResolvePendingHostDirectionalBoundary(host);
                    FinalizeHostPendingCancelOwner(host);
                }
            }
            finally
            {
                _hostRegistrationStack.RemoveAt(_hostRegistrationStack.Count - 1);
                _hostRegistrationScopes.ExitEnding(token);
            }
        }

        internal static void UnregisterHost(int hostId)
        {
            if (hostId == 0 || !_hostRegistries.TryGetValue(hostId, out HostRegistry host))
                return;

            if (host.isRegistering)
            {
                host.unregisterPending = true;

                if (_focusedHostId == hostId)
                    Clear();

                return;
            }

            if (_focusedHostId == hostId)
                Clear();

            _hostRegistries.Remove(hostId);
        }

        internal static void ExitUGUINavigation(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                host.directionalReturnId = 0;

            if (hostId != 0 && _focusedHostId == hostId)
                Clear();
        }

        internal static void ExitUGUINavigationAtDirectionalBoundary(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
            {
                int activeLayerId = NowOverlay.activeFocusLayerId;
                host.directionalReturnId =
                    _focusedHostId == hostId &&
                    ContainsFocusableInLayer(
                        host.focusables,
                        _focusedId,
                        activeLayerId)
                        ? _focusedId
                        : 0;
            }

            if (hostId != 0 && _focusedHostId == hostId)
                Clear();
        }

        internal static void DiscardUGUIDirectionalReturn(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                host.directionalReturnId = 0;
        }

        internal static bool DeferUGUIDirectionalBoundary(
            int hostId,
            Vector2 direction)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null ||
                !TryResolveUGUIDirection(direction, out _))
            {
                return false;
            }

            host.hasPendingDirectionalBoundary = true;
            host.pendingDirectionalBoundary = direction;
            host.pendingDirectionalBoundaryFocusId =
                _focusedHostId == hostId ? _focusedId : 0;
            host.pendingDirectionalBoundaryFocusRevision = _focusRevision;
            host.pendingDirectionalBoundaryRegistrationVersion =
                host.registrationVersion + (host.isRegistering ? 2UL : 1UL);
            return true;
        }

        internal static void CancelDeferredUGUIDirectionalBoundary(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                ClearPendingHostDirectionalBoundary(host);
        }

        internal static void DeferUGUINavigationEntry(
            int hostId,
            Vector2 direction)
        {
            if (hostId == 0)
                return;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.hasPendingEntry = true;
            host.pendingEntryDirection = direction;
            host.pendingEntryOrderStep = 0;

            if (!TryResolveUGUIDirection(direction, out _))
                host.directionalReturnId = 0;
        }

        internal static void DeferUGUITabEntry(int hostId, int step)
        {
            if (hostId == 0 || step == 0)
                return;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.directionalReturnId = 0;
            host.hasPendingEntry = true;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = step < 0 ? -1 : 1;
        }

        internal static void CancelPendingUGUIEntry(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null)
                return;

            host.hasPendingEntry = false;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = 0;
        }

        static HostRegistry ActiveHostRegistry()
        {
            int count = _hostRegistrationStack.Count;
            return count > 0 ? _hostRegistrationStack[count - 1] : null;
        }

        static HostRegistry GetHostRegistry(int hostId)
        {
            if (hostId == 0)
                return null;

            _hostRegistries.TryGetValue(hostId, out HostRegistry host);
            return host;
        }

        static HostRegistry GetOrCreateHostRegistry(int hostId)
        {
            if (_hostRegistries.TryGetValue(hostId, out HostRegistry host))
                return host;

            host = new HostRegistry(hostId);
            _hostRegistries.Add(hostId, host);
            return host;
        }

        static int ResolveFocusHost(int id)
        {
            if (id == 0)
                return 0;

            HostRegistry active = ActiveHostRegistry();

            if (active != null)
                return active.hostId;

            if (_focusedId == id && _focusedHostId != 0)
                return _focusedHostId;

            foreach (var pair in _hostRegistries)
            {
                HostRegistry host = pair.Value;

                if ((host.isRegistering &&
                     ContainsFocusable(host.buildingFocusables, id)) ||
                    ContainsFocusable(host.focusables, id))
                {
                    return host.hostId;
                }
            }

            return 0;
        }

        static bool ContainsFocusable(List<Focusable> focusables, int id)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id)
                    return true;
            }

            return false;
        }

        static bool IsOwningProxySelection(int hostId, GameObject selection)
        {
            HostRegistry host = GetHostRegistry(hostId);
            return host != null &&
                host.proxy != null &&
                host.proxy.owningSelection == selection;
        }

        static bool TrySelectOwningProxy(int hostId, EventSystem eventSystem)
        {
            HostRegistry host = GetHostRegistry(hostId);
            NowUGUINavigationProxy proxy = host != null ? host.proxy : null;

            if (proxy == null || !proxy.IsActive() || !proxy.IsInteractable())
                return false;

            if (eventSystem == null ||
                eventSystem.currentSelectedGameObject == proxy.owningSelection)
            {
                if (eventSystem == null)
                    proxy.RequestSelection();

                return true;
            }

            // Unity rejects SetSelectedGameObject while dispatching another
            // selection callback. The in-flight proxy OnSelect already owns the
            // handoff; otherwise the next host pass will reject a foreign
            // selection without making a reentrant EventSystem call.
            if (eventSystem.alreadySelecting)
            {
                proxy.RequestSelection();
                return true;
            }

            eventSystem.SetSelectedGameObject(proxy.owningSelection);
            return eventSystem.currentSelectedGameObject == proxy.owningSelection;
        }

        static bool IsOwningProxySelected(NowUGUINavigationProxy proxy)
        {
            if (proxy == null)
                return false;

            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null &&
                eventSystem.currentSelectedGameObject == proxy.owningSelection;
        }

        static void FinalizeHostPendingCancelOwner(HostRegistry host)
        {
            if (host.pendingCancelOwnerId == 0)
                return;

            bool ownerRegistered = false;

            for (int i = 0; i < host.focusables.Count; ++i)
            {
                if (host.focusables[i].id == host.pendingCancelOwnerId &&
                    host.focusables[i].consumesCancel)
                {
                    ownerRegistered = true;
                    break;
                }
            }

            if (_focusedHostId == host.hostId &&
                _focusedId == host.pendingCancelOwnerId &&
                !ownerRegistered)
            {
                Clear();
            }

            host.pendingCancelOwnerId = 0;
        }

        static void CompletePendingHostEntry(HostRegistry host)
        {
            if (!host.hasPendingEntry)
                return;

            Vector2 direction = host.pendingEntryDirection;
            int orderStep = host.pendingEntryOrderStep;
            host.hasPendingEntry = false;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = 0;

            if (!IsOwningProxySelected(host.proxy) ||
                IsFocusedInHost(host.hostId) ||
                IsFocusedOutsideHost(host.hostId))
            {
                host.directionalReturnId = 0;
                return;
            }

            NowFocusMoveResult result = orderStep != 0
                ? EnterUGUITab(host.hostId, orderStep)
                : EnterUGUINavigation(host.hostId, direction);

            if (result ==
                NowFocusMoveResult.Seeded)
            {
                // Entry completes after this frame's controls have drawn.
                // Retained hosts need one more draw so focus visuals, editor
                // input capture, and caret ownership observe the seeded id.
                NowControlState.RequestRepaint();
            }
        }

        static void ResolvePendingHostTabBoundary(HostRegistry host)
        {
            int step = host.pendingTabBoundaryStep;
            int expectedFocusId = host.pendingTabFocusId;
            int expectedFocusRevision = host.pendingTabFocusRevision;
            host.pendingTabBoundaryStep = 0;
            host.pendingTabFocusId = 0;
            host.pendingTabFocusRevision = 0;

            if (step == 0 ||
                !IsOwningProxySelected(host.proxy) ||
                _focusRevision != expectedFocusRevision ||
                (_focusedHostId == host.hostId ? _focusedId : 0) !=
                    expectedFocusId)
            {
                return;
            }

            NowFocusMoveResult result = MoveFocusInRegistrationOrder(
                host.focusables,
                host.hostId,
                step,
                NowOverlay.activeFocusLayerId,
                wrap: false);

            if (result == NowFocusMoveResult.Moved ||
                result == NowFocusMoveResult.Seeded)
            {
                // Registration has already drawn this frame using the previous
                // focus. Schedule one retained repaint for the resolved target.
                NowControlState.RequestRepaint();
                return;
            }

            if ((result == NowFocusMoveResult.Boundary ||
                 result == NowFocusMoveResult.Unavailable) &&
                host.proxy != null &&
                host.proxy.QueueYieldTab(step))
            {
                return;
            }
        }

        static void ResolvePendingHostDirectionalBoundary(HostRegistry host)
        {
            if (!host.hasPendingDirectionalBoundary ||
                host.registrationVersion <
                    host.pendingDirectionalBoundaryRegistrationVersion)
            {
                return;
            }

            Vector2 direction = host.pendingDirectionalBoundary;
            int expectedFocusId = host.pendingDirectionalBoundaryFocusId;
            int expectedFocusRevision =
                host.pendingDirectionalBoundaryFocusRevision;
            ClearPendingHostDirectionalBoundary(host);

            if (!IsOwningProxySelected(host.proxy) ||
                _focusRevision != expectedFocusRevision ||
                (_focusedHostId == host.hostId ? _focusedId : 0) !=
                    expectedFocusId)
            {
                return;
            }

            NowFocusMoveResult result = RouteUGUINavigation(
                host.hostId,
                direction);

            if (result == NowFocusMoveResult.Moved ||
                result == NowFocusMoveResult.Seeded)
            {
                // The newly committed draw used the previous focus. Schedule a
                // retained repaint so its visuals observe the resolved target.
                NowControlState.RequestRepaint();
                return;
            }

            if ((result == NowFocusMoveResult.Boundary ||
                 result == NowFocusMoveResult.Unavailable) &&
                host.proxy != null)
            {
                host.proxy.QueueYieldDirection(direction);
            }
        }

        static void ClearPendingHostDirectionalBoundary(HostRegistry host)
        {
            host.hasPendingDirectionalBoundary = false;
            host.pendingDirectionalBoundary = default;
            host.pendingDirectionalBoundaryFocusId = 0;
            host.pendingDirectionalBoundaryFocusRevision = 0;
            host.pendingDirectionalBoundaryRegistrationVersion = 0;
        }

        static void BeginFrameIfNeeded()
        {
            int frame = Time.frameCount;

            if (_registryFrame == frame)
                return;

            _registryFrame = frame;
            bool preserveInputClaims = _preserveInputClaimsOnNextSwap;
            _preserveInputClaimsOnNextSwap = false;
            if (!preserveInputClaims)
                FinalizePendingCancelOwner();


            if (preserveInputClaims)
            {
                if (_navigationLockCurrentFocusRevision == _navigationLockPreviousFocusRevision)
                {
                    if (_navigationLockCurrent > _navigationLockPrevious)
                        _navigationLockPrevious = _navigationLockCurrent;
                }
                else if (_navigationLockCurrent != NowFocusNavigationLock.None)
                {
                    _navigationLockPrevious = _navigationLockCurrent;
                    _navigationLockPreviousFocusRevision = _navigationLockCurrentFocusRevision;
                }

                _retainFocusPrevious |= _retainFocusCurrent;
            }
            else
            {
                _navigationLockPrevious = _navigationLockCurrent;
                _navigationLockPreviousFocusRevision = _navigationLockCurrentFocusRevision;
                _retainFocusPrevious = _retainFocusCurrent;
            }

            _navigationLockCurrent = NowFocusNavigationLock.None;
            _navigationLockCurrentFocusRevision = 0;
            _retainFocusCurrent = false;

            _previous.Clear();
            _previous.AddRange(_current);
            _current.Clear();

            _ownersPrevious.Clear();
            foreach (var owner in _ownersCurrent)
                _ownersPrevious[owner.Key] = owner.Value;
            _ownersCurrent.Clear();

            ProcessNavigation();
        }

        static void FinalizePendingCancelOwner()
        {
            if (_pendingCancelOwnerId == 0)
                return;

            bool ownerRegistered = false;

            for (int i = 0; i < _current.Count; ++i)
            {
                if (_current[i].id == _pendingCancelOwnerId && _current[i].consumesCancel)
                {
                    ownerRegistered = true;
                    break;
                }
            }

            if (_focusedId == _pendingCancelOwnerId && !ownerRegistered)
                Clear();

            _pendingCancelOwnerId = 0;
        }

        /// <summary>Forces the frame swap; used by tests where frameCount is static.</summary>
        internal static void ForceNewFrame()
        {
            _preserveInputClaimsOnNextSwap = false;
            _registryFrame = -1;
            BeginFrameIfNeeded();
            _registryFrame = -1;
            _preserveInputClaimsOnNextSwap = true;
        }

        static void ProcessNavigation()
        {
            int ignoredPendingTabBoundaryStep = 0;
            int ignoredPendingTabFocusId = 0;
            int ignoredPendingTabFocusRevision = 0;
            ProcessNavigation(
                _previous,
                0,
                null,
                false,
                ref ignoredPendingTabBoundaryStep,
                ref ignoredPendingTabFocusId,
                ref ignoredPendingTabFocusRevision,
                NowInput.current,
                _navigationLockPrevious,
                _navigationLockPreviousFocusRevision,
                ref _pendingCancelOwnerId,
                _retainFocusPrevious,
                ref _lastNavigation,
                ref _repeatDirection,
                ref _nextNavigationRepeatTime);
        }

        static void ProcessHostNavigationIfNeeded(HostRegistry host)
        {
            if (host == null ||
                NowInput.isPassive ||
                !NowInput.hasContext ||
                NowInput.currentProvider == null)
            {
                return;
            }

            NowInputSnapshot snapshot = NowInput.current;

            if (host.lastProcessedInputFrame == snapshot.frame)
                return;

            host.lastProcessedInputFrame = snapshot.frame;
            host.pendingTabBoundaryStep = 0;
            host.pendingTabFocusId = 0;
            host.pendingTabFocusRevision = 0;
            ProcessNavigation(
                host.focusables,
                host.hostId,
                host.proxy,
                true,
                ref host.pendingTabBoundaryStep,
                ref host.pendingTabFocusId,
                ref host.pendingTabFocusRevision,
                snapshot,
                host.claimedNavigationLock,
                host.claimedNavigationLockFocusRevision,
                ref host.pendingCancelOwnerId,
                host.retainFocus,
                ref host.lastNavigation,
                ref host.repeatDirection,
                ref host.nextNavigationRepeatTime);
        }

        static void ProcessNavigation(
            List<Focusable> focusables,
            int hostId,
            NowUGUINavigationProxy proxy,
            bool deferProxyTabBoundary,
            ref int pendingTabBoundaryStep,
            ref int pendingTabFocusId,
            ref int pendingTabFocusRevision,
            NowInputSnapshot snapshot,
            NowFocusNavigationLock claimedNavigationLock,
            int claimedNavigationLockFocusRevision,
            ref int pendingCancelOwnerId,
            bool retainFocus,
            ref Vector2 lastNavigation,
            ref Vector2 repeatDirection,
            ref float nextNavigationRepeatTime)
        {
            int activeLayerId = NowOverlay.activeFocusLayerId;
            bool ownsFocus = _focusedId != 0 && _focusedHostId == hostId;
            bool focusedWasRegistered = TryGetFocusedInputPolicy(
                focusables,
                hostId,
                activeLayerId,
                out NowFocusNavigationLock focusedNavigationLock,
                out bool focusedConsumesCancel);

            if (claimedNavigationLockFocusRevision == _focusRevision &&
                claimedNavigationLock > focusedNavigationLock)
            {
                focusedNavigationLock = claimedNavigationLock;
            }

            bool owningProxySelected = IsOwningProxySelected(proxy);

            if (respectEventSystem)
            {
                EventSystem eventSystem = EventSystem.current;
                GameObject selection = eventSystem != null
                    ? eventSystem.currentSelectedGameObject
                    : null;

                if (selection != null && !owningProxySelected)
                {
                    if (ownsFocus)
                        Clear();

                    lastNavigation = snapshot.navigation;
                    ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                    return;
                }
            }

            if (_focusedId != 0 && _focusedHostId != hostId)
            {
                lastNavigation = snapshot.navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (snapshot.cancelPressed)
            {
                if (ownsFocus && focusedConsumesCancel)
                    pendingCancelOwnerId = _focusedId;
                else if (ownsFocus && !NowInput.cancelConsumedForFrameSwap)
                    Clear();

                lastNavigation = snapshot.navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (snapshot.primaryPressed &&
                ownsFocus &&
                !retainFocus &&
                !NowInput.focusClaimedByPrimaryPress)
            {
                bool overControl = false;

                for (int i = 0; i < focusables.Count; ++i)
                {
                    if (IsFocusableInLayer(focusables[i], activeLayerId) &&
                        focusables[i].visibleRect.width > 0f &&
                        focusables[i].visibleRect.height > 0f &&
                        focusables[i].visibleRect.Contains(snapshot.pointerPosition))
                    {
                        overControl = true;
                        break;
                    }
                }

                if (!overControl)
                {
                    Clear();
                    lastNavigation = snapshot.navigation;
                    ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                    return;
                }
            }

            Vector2 navigation = snapshot.navigation;
            bool protectExplicitFocus = _explicitFocusRequestId != 0 &&
                _explicitFocusRequestHostId == hostId &&
                _explicitFocusRequestId == _focusedId &&
                !focusedWasRegistered;

            if (_explicitFocusRequestHostId == hostId)
            {
                _explicitFocusRequestId = 0;
                _explicitFocusRequestHostId = 0;
            }

            if (protectExplicitFocus &&
                (snapshot.focusPreviousPressed || snapshot.focusNextPressed ||
                 ResolveNavigationDirection(navigation) != default))
            {
                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (focusedNavigationLock == NowFocusNavigationLock.All)
            {
                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (snapshot.focusPreviousPressed || snapshot.focusNextPressed)
            {
                int step = snapshot.focusPreviousPressed ? -1 : 1;

                if (proxy != null && deferProxyTabBoundary)
                {
                    pendingTabBoundaryStep = step;
                    pendingTabFocusId =
                        _focusedHostId == hostId ? _focusedId : 0;
                    pendingTabFocusRevision = _focusRevision;
                }
                else
                {
                    NowFocusMoveResult result = MoveFocusInRegistrationOrder(
                        focusables,
                        hostId,
                        step,
                        activeLayerId,
                        wrap: proxy == null);

                    if (result == NowFocusMoveResult.Boundary &&
                        proxy != null)
                    {
                        proxy.TryYieldTab(step);
                    }
                }

                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (owningProxySelected)
            {
                // InputSystemUIInputModule invokes the proxy's OnMove
                // synchronously. Polling the same vector here would advance a
                // second time during the retained rebuild caused by that event.
                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (focusedNavigationLock == NowFocusNavigationLock.Directional)
            {
                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            Vector2 direction = GetNavigationPulse(
                navigation,
                snapshot.time,
                ref lastNavigation,
                ref repeatDirection,
                ref nextNavigationRepeatTime);

            // Proxy hosts enter through their UGUI Selectable. With no selected
            // proxy there is no unambiguous surrounding navigation graph to
            // hand a boundary back to, so do not seed them by polling.
            if (direction == default ||
                (proxy != null && _focusedHostId != hostId) ||
                !HasFocusableInLayer(focusables, activeLayerId))
            {
                return;
            }

            MoveFocus(focusables, hostId, direction, activeLayerId);
        }

        internal static NowFocusMoveResult RouteUGUINavigation(int hostId, Vector2 direction)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || !TryResolveUGUIDirection(direction, out Vector2 resolvedDirection))
                return NowFocusMoveResult.Unavailable;

            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (!HasFocusableInLayer(host.focusables, activeLayerId))
                return NowFocusMoveResult.Unavailable;

            if (_focusedHostId == hostId)
            {
                TryGetFocusedInputPolicy(
                    host.focusables,
                    hostId,
                    activeLayerId,
                    out NowFocusNavigationLock navigationLock,
                    out _);

                if (host.claimedNavigationLockFocusRevision == _focusRevision &&
                    host.claimedNavigationLock > navigationLock)
                {
                    navigationLock = host.claimedNavigationLock;
                }

                if (navigationLock == NowFocusNavigationLock.Directional ||
                    navigationLock == NowFocusNavigationLock.All)
                {
                    return NowFocusMoveResult.Consumed;
                }
            }

            return MoveFocus(host.focusables, host.hostId, resolvedDirection, activeLayerId);
        }

        internal static bool IsUGUIDirectionalNavigationLocked(int hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || _focusedHostId != hostId)
                return false;

            int activeLayerId = NowOverlay.activeFocusLayerId;
            TryGetFocusedInputPolicy(
                host.focusables,
                hostId,
                activeLayerId,
                out NowFocusNavigationLock navigationLock,
                out _);

            if (host.claimedNavigationLockFocusRevision == _focusRevision &&
                host.claimedNavigationLock > navigationLock)
            {
                navigationLock = host.claimedNavigationLock;
            }

            return navigationLock == NowFocusNavigationLock.Directional ||
                navigationLock == NowFocusNavigationLock.All;
        }

        internal static NowFocusMoveResult RouteUGUITab(int hostId, int step)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || step == 0)
                return NowFocusMoveResult.Unavailable;

            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (!HasFocusableInLayer(host.focusables, activeLayerId))
                return NowFocusMoveResult.Unavailable;

            if (_focusedHostId == hostId)
            {
                TryGetFocusedInputPolicy(
                    host.focusables,
                    hostId,
                    activeLayerId,
                    out NowFocusNavigationLock navigationLock,
                    out _);

                if (host.claimedNavigationLockFocusRevision == _focusRevision &&
                    host.claimedNavigationLock > navigationLock)
                {
                    navigationLock = host.claimedNavigationLock;
                }

                if (navigationLock == NowFocusNavigationLock.All)
                    return NowFocusMoveResult.Consumed;
            }

            return MoveFocusInRegistrationOrder(
                host.focusables,
                hostId,
                step < 0 ? -1 : 1,
                activeLayerId,
                wrap: false);
        }

        internal static NowFocusMoveResult EnterUGUINavigation(int hostId, Vector2 direction)
        {
            if (hostId == 0)
                return NowFocusMoveResult.Unavailable;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            bool hasDirection =
                TryResolveUGUIDirection(direction, out Vector2 resolvedDirection);

            if (!hasDirection)
                host.directionalReturnId = 0;

            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (IsFocusedOutsideHost(hostId))
            {
                host.hasPendingEntry = false;
                host.pendingEntryDirection = default;
                host.pendingEntryOrderStep = 0;
                return NowFocusMoveResult.Consumed;
            }

            if (!HasFocusableInLayer(host.focusables, activeLayerId))
            {
                host.hasPendingEntry = true;
                host.pendingEntryDirection = direction;
                host.pendingEntryOrderStep = 0;
                return NowFocusMoveResult.Unavailable;
            }

            host.hasPendingEntry = false;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = 0;

            if (_focusedHostId == hostId &&
                (ContainsFocusableInLayer(
                     host.focusables,
                     _focusedId,
                     activeLayerId) ||
                 (host.isRegistering &&
                  ContainsFocusableInLayer(
                      host.buildingFocusables,
                      _focusedId,
                      activeLayerId))))
            {
                host.directionalReturnId = 0;
                return NowFocusMoveResult.Consumed;
            }

            int directionalReturnId = host.directionalReturnId;
            host.directionalReturnId = 0;

            if (directionalReturnId != 0)
            {
                if (hasDirection &&
                    TryFocusRegistered(
                        host.focusables,
                        hostId,
                        directionalReturnId,
                        activeLayerId,
                        out _))
                {
                    return NowFocusMoveResult.Seeded;
                }
            }

            int id;

            if (hasDirection)
                id = FindEdgeFocus(host.focusables, resolvedDirection, activeLayerId);
            else
                id = FindFirstFocus(host.focusables, activeLayerId);

            if (id == 0)
                return NowFocusMoveResult.Unavailable;

            SetFocused(id, hostId);
            return NowFocusMoveResult.Seeded;
        }

        internal static NowFocusMoveResult EnterUGUITab(int hostId, int step)
        {
            if (hostId == 0 || step == 0)
                return NowFocusMoveResult.Unavailable;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.directionalReturnId = 0;
            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (IsFocusedOutsideHost(hostId))
                return NowFocusMoveResult.Consumed;

            if (_explicitFocusRequestHostId == hostId &&
                _explicitFocusRequestId != 0 &&
                _explicitFocusRequestId == _focusedId)
            {
                host.hasPendingEntry = false;
                host.pendingEntryDirection = default;
                host.pendingEntryOrderStep = 0;
                return NowFocusMoveResult.Consumed;
            }

            if (!HasFocusableInLayer(host.focusables, activeLayerId))
            {
                host.hasPendingEntry = true;
                host.pendingEntryDirection = default;
                host.pendingEntryOrderStep = step < 0 ? -1 : 1;
                return NowFocusMoveResult.Unavailable;
            }

            host.hasPendingEntry = false;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = 0;

            int id = FindRegistrationEdgeFocus(
                host.focusables,
                activeLayerId,
                step < 0 ? -1 : 1);

            if (id == 0)
                return NowFocusMoveResult.Unavailable;

            bool changed = _focusedHostId != hostId || _focusedId != id;
            SetFocused(id, hostId);
            return changed ? NowFocusMoveResult.Seeded : NowFocusMoveResult.Consumed;
        }

        static bool TryResolveUGUIDirection(Vector2 direction, out Vector2 resolvedDirection)
        {
            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);

            if (x <= 0.5f && y <= 0.5f)
            {
                resolvedDirection = default;
                return false;
            }

            if (x >= y)
            {
                resolvedDirection = new Vector2(Mathf.Sign(direction.x), 0f);
                return true;
            }

            // UGUI navigation uses y+ for up; focus rectangles use y-down.
            resolvedDirection = new Vector2(0f, -Mathf.Sign(direction.y));
            return true;
        }

        static bool TryGetFocusedInputPolicy(
            List<Focusable> focusables,
            int hostId,
            int activeLayerId,
            out NowFocusNavigationLock navigationLock,
            out bool consumesCancel)
        {
            navigationLock = NowFocusNavigationLock.None;
            consumesCancel = false;
            bool found = false;

            if (_focusedHostId != hostId)
                return false;

            for (int i = 0; i < focusables.Count; ++i)
            {
                Focusable focusable = focusables[i];

                if (focusable.id != _focusedId)
                    continue;

                found = true;

                if (!IsFocusableInLayer(focusable, activeLayerId))
                    continue;

                if (focusable.navigationLock > navigationLock)
                    navigationLock = focusable.navigationLock;

                consumesCancel |= focusable.consumesCancel;
            }

            return found;
        }

        static Vector2 GetNavigationPulse(
            Vector2 navigation,
            float time,
            ref Vector2 lastNavigation,
            ref Vector2 repeatDirection,
            ref float nextNavigationRepeatTime)
        {
            Vector2 direction = ResolveNavigationDirection(navigation);
            Vector2 previousDirection = ResolveNavigationDirection(lastNavigation);
            lastNavigation = navigation;

            if (direction == default)
            {
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return default;
            }

            NowControlState.RequestRepaint();

            if (direction != previousDirection || direction != repeatDirection)
            {
                repeatDirection = direction;
                nextNavigationRepeatTime = time + NavigationRepeatDelay;
                return direction;
            }

            if (time >= nextNavigationRepeatTime)
            {
                nextNavigationRepeatTime = time + NavigationRepeatInterval;
                return direction;
            }

            return default;
        }

        static void ResetNavigationRepeat(
            ref Vector2 repeatDirection,
            ref float nextNavigationRepeatTime)
        {
            repeatDirection = default;
            nextNavigationRepeatTime = 0f;
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

        static NowFocusMoveResult MoveFocusInRegistrationOrder(
            List<Focusable> focusables,
            int hostId,
            int step,
            int activeLayerId,
            bool wrap)
        {
            if (!HasFocusableInLayer(focusables, activeLayerId))
                return NowFocusMoveResult.Unavailable;

            int focusedIndex = -1;
            int fallbackIndex = -1;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (!IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                if (fallbackIndex < 0 || step < 0)
                    fallbackIndex = i;

                if (_focusedHostId == hostId && focusables[i].id == _focusedId)
                {
                    focusedIndex = i;
                    break;
                }
            }

            if (focusedIndex < 0)
            {
                SetFocused(focusables[fallbackIndex].id, hostId);
                return NowFocusMoveResult.Seeded;
            }

            if (focusables[focusedIndex].navigation.TryGetOrder(step, out int targetId) &&
                TryFocusRegistered(focusables, hostId, targetId, activeLayerId, out _))
            {
                return NowFocusMoveResult.Moved;
            }

            int next = FindNextFocusableIndex(
                focusables, focusedIndex, step, activeLayerId, wrap);

            if (next < 0)
                return NowFocusMoveResult.Boundary;

            SetFocused(focusables[next].id, hostId);
            return NowFocusMoveResult.Moved;
        }

        static NowFocusMoveResult MoveFocus(
            List<Focusable> focusables,
            int hostId,
            Vector2 direction,
            int activeLayerId)
        {
            int focusedIndex = -1;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (IsFocusableInLayer(focusables[i], activeLayerId) &&
                    _focusedHostId == hostId &&
                    focusables[i].id == _focusedId)
                {
                    focusedIndex = i;
                    break;
                }
            }

            if (focusedIndex < 0)
            {
                int seeded = FindEdgeFocus(focusables, direction, activeLayerId);

                if (seeded == 0)
                    return NowFocusMoveResult.Unavailable;

                SetFocused(seeded, hostId);
                return NowFocusMoveResult.Seeded;
            }

            if (focusables[focusedIndex].navigation.TryGetDirectional(direction, out int targetId) &&
                TryFocusRegistered(
                    focusables, hostId, targetId, activeLayerId, out Rect targetRect))
            {
                SetNavigationMemory(targetRect.center, targetRect.center);
                return NowFocusMoveResult.Moved;
            }

            bool vertical = direction.y != 0f;
            Vector2 origin = focusables[focusedIndex].rect.center;

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

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (i == focusedIndex || !IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                Vector2 toCandidate = focusables[i].rect.center - origin;
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

            if (bestIndex < 0)
                return NowFocusMoveResult.Boundary;

            SetFocused(focusables[bestIndex].id, hostId);

            Vector2 focusedCenter = focusables[bestIndex].rect.center;
            Vector2 memory = origin;

            if (vertical)
                memory.y = focusedCenter.y;
            else
                memory.x = focusedCenter.x;

            SetNavigationMemory(memory, focusedCenter);
            return NowFocusMoveResult.Moved;
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

        static bool TryFocusRegistered(
            List<Focusable> focusables,
            int hostId,
            int id,
            int activeLayerId,
            out Rect rect)
        {
            rect = default;

            if (id == 0)
                return false;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id &&
                    IsFocusableInLayer(focusables[i], activeLayerId))
                {
                    SetFocused(id, hostId);
                    rect = focusables[i].rect;
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
        static int FindEdgeFocus(
            List<Focusable> focusables,
            Vector2 direction,
            int activeLayerId)
        {
            float bestVisibleScore = float.MaxValue;
            int bestVisibleId = 0;
            float bestScore = float.MaxValue;
            int bestId = 0;
            int fallbackId = 0;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (!IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                if (fallbackId == 0)
                    fallbackId = focusables[i].id;

                float score = Vector2.Dot(focusables[i].rect.center, direction);

                if (focusables[i].visibleRect.width > 0f &&
                    focusables[i].visibleRect.height > 0f &&
                    score < bestVisibleScore)
                {
                    bestVisibleScore = score;
                    bestVisibleId = focusables[i].id;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestId = focusables[i].id;
                }
            }

            if (bestVisibleId != 0)
                return bestVisibleId;

            return bestId != 0 ? bestId : fallbackId;
        }

        static int FindFirstFocus(List<Focusable> focusables, int activeLayerId)
        {
            return FindRegistrationEdgeFocus(focusables, activeLayerId, 1);
        }

        static int FindRegistrationEdgeFocus(
            List<Focusable> focusables,
            int activeLayerId,
            int step)
        {
            int fallbackId = 0;

            int start = step < 0 ? focusables.Count - 1 : 0;
            int end = step < 0 ? -1 : focusables.Count;

            for (int i = start; i != end; i += step)
            {
                if (!IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                if (fallbackId == 0)
                    fallbackId = focusables[i].id;

                if (focusables[i].visibleRect.width > 0f &&
                    focusables[i].visibleRect.height > 0f)
                {
                    return focusables[i].id;
                }
            }

            return fallbackId;
        }

        static bool IsFocusedInActiveLayer(int id)
        {
            int activeLayerId = NowOverlay.activeFocusLayerId;

            if (activeLayerId == 0)
                return true;

            HostRegistry host = GetHostRegistry(_focusedHostId);

            if (host != null)
            {
                return (host.isRegistering &&
                        ContainsFocusableInLayer(host.buildingFocusables, id, activeLayerId)) ||
                    ContainsFocusableInLayer(host.focusables, id, activeLayerId);
            }

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

        static bool HasFocusableInLayer(List<Focusable> focusables, int activeLayerId)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (IsFocusableInLayer(focusables[i], activeLayerId))
                    return true;
            }

            return false;
        }

        static int FindNextFocusableIndex(
            List<Focusable> focusables,
            int focusedIndex,
            int step,
            int activeLayerId,
            bool wrap)
        {
            int count = focusables.Count;

            if (!wrap)
            {
                for (int next = focusedIndex + step;
                    next >= 0 && next < count;
                    next += step)
                {
                    if (IsFocusableInLayer(focusables[next], activeLayerId))
                        return next;
                }

                return -1;
            }

            for (int offset = 1; offset <= count; ++offset)
            {
                int next = (focusedIndex + offset * step) % count;

                if (next < 0)
                    next += count;

                if (IsFocusableInLayer(focusables[next], activeLayerId))
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
            _ownersCurrent.Clear();
            _ownersPrevious.Clear();
            _scrollRegionStack.Clear();
            _hostRegistries.Clear();
            _hostRegistrationStack.Clear();
            _hostRegistrationScopes.Clear();
            _focusedId = 0;
            _focusedHostId = 0;
            _focusRevision = 0;
            _registryFrame = -1;
            _navigationLockCurrent = NowFocusNavigationLock.None;
            _navigationLockCurrentFocusRevision = 0;
            _navigationLockPrevious = NowFocusNavigationLock.None;
            _navigationLockPreviousFocusRevision = 0;
            _explicitFocusRequestId = 0;
            _explicitFocusRequestHostId = 0;
            _pendingCancelOwnerId = 0;
            _preserveInputClaimsOnNextSwap = false;
            _retainFocusCurrent = false;
            _retainFocusPrevious = false;
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
