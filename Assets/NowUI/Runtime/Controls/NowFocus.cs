using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    internal interface INowFocusNavigationProxy
    {
        bool hasPendingSelection { get; }

        GameObject owningSelection { get; }

        bool isActiveAndInteractable { get; }

        void RequestSelection();

        bool QueueYieldTab(int step);

        bool QueueYieldDirection(Vector2 direction);

        bool TryYieldTab(int step);
    }

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
        NowResolvedId _resolvedLeft;
        NowResolvedId _resolvedRight;
        NowResolvedId _resolvedUp;
        NowResolvedId _resolvedDown;
        NowResolvedId _resolvedPrevious;
        NowResolvedId _resolvedNext;
        byte _mask;
        byte _resolvedMask;

        public static NowFocusNavigation None => default;

        public static NowFocusNavigation Left(NowId id) => default(NowFocusNavigation).SetLeft(id);

        public static NowFocusNavigation Right(NowId id) => default(NowFocusNavigation).SetRight(id);

        public static NowFocusNavigation Up(NowId id) => default(NowFocusNavigation).SetUp(id);

        public static NowFocusNavigation Down(NowId id) => default(NowFocusNavigation).SetDown(id);

        public static NowFocusNavigation Previous(NowId id) => default(NowFocusNavigation).SetPrevious(id);

        public static NowFocusNavigation Next(NowId id) => default(NowFocusNavigation).SetNext(id);

        public static NowFocusNavigation Left(NowResolvedId id) => default(NowFocusNavigation).SetLeft(id);

        public static NowFocusNavigation Right(NowResolvedId id) => default(NowFocusNavigation).SetRight(id);

        public static NowFocusNavigation Up(NowResolvedId id) => default(NowFocusNavigation).SetUp(id);

        public static NowFocusNavigation Down(NowResolvedId id) => default(NowFocusNavigation).SetDown(id);

        public static NowFocusNavigation Previous(NowResolvedId id) => default(NowFocusNavigation).SetPrevious(id);

        public static NowFocusNavigation Next(NowResolvedId id) => default(NowFocusNavigation).SetNext(id);

        public NowFocusNavigation SetLeft(NowId id) { _left = id; SetMask(LeftMask, id.hasValue); SetResolvedMask(LeftMask, false); return this; }

        public NowFocusNavigation SetRight(NowId id) { _right = id; SetMask(RightMask, id.hasValue); SetResolvedMask(RightMask, false); return this; }

        public NowFocusNavigation SetUp(NowId id) { _up = id; SetMask(UpMask, id.hasValue); SetResolvedMask(UpMask, false); return this; }

        public NowFocusNavigation SetDown(NowId id) { _down = id; SetMask(DownMask, id.hasValue); SetResolvedMask(DownMask, false); return this; }

        public NowFocusNavigation SetPrevious(NowId id) { _previous = id; SetMask(PreviousMask, id.hasValue); SetResolvedMask(PreviousMask, false); return this; }

        public NowFocusNavigation SetNext(NowId id) { _next = id; SetMask(NextMask, id.hasValue); SetResolvedMask(NextMask, false); return this; }

        public NowFocusNavigation SetLeft(NowResolvedId id) { _resolvedLeft = id; SetMask(LeftMask, id.hasValue); SetResolvedMask(LeftMask, true); return this; }

        public NowFocusNavigation SetRight(NowResolvedId id) { _resolvedRight = id; SetMask(RightMask, id.hasValue); SetResolvedMask(RightMask, true); return this; }

        public NowFocusNavigation SetUp(NowResolvedId id) { _resolvedUp = id; SetMask(UpMask, id.hasValue); SetResolvedMask(UpMask, true); return this; }

        public NowFocusNavigation SetDown(NowResolvedId id) { _resolvedDown = id; SetMask(DownMask, id.hasValue); SetResolvedMask(DownMask, true); return this; }

        public NowFocusNavigation SetPrevious(NowResolvedId id) { _resolvedPrevious = id; SetMask(PreviousMask, id.hasValue); SetResolvedMask(PreviousMask, true); return this; }

        public NowFocusNavigation SetNext(NowResolvedId id) { _resolvedNext = id; SetMask(NextMask, id.hasValue); SetResolvedMask(NextMask, true); return this; }

        void SetMask(byte mask, bool enabled)
        {
            if (enabled)
                _mask |= mask;
            else
                _mask &= (byte)~mask;
        }

        void SetResolvedMask(byte mask, bool resolved)
        {
            if (resolved)
                _resolvedMask |= mask;
            else
                _resolvedMask &= (byte)~mask;
        }

        internal ResolvedFocusNavigation Resolve(bool legacyRegistration = false)
        {
            var resolved = default(ResolvedFocusNavigation);

            if ((_mask & LeftMask) != 0)
                resolved.SetLeft(ResolveTarget(LeftMask, _left, _resolvedLeft, legacyRegistration));

            if ((_mask & RightMask) != 0)
                resolved.SetRight(ResolveTarget(RightMask, _right, _resolvedRight, legacyRegistration));

            if ((_mask & UpMask) != 0)
                resolved.SetUp(ResolveTarget(UpMask, _up, _resolvedUp, legacyRegistration));

            if ((_mask & DownMask) != 0)
                resolved.SetDown(ResolveTarget(DownMask, _down, _resolvedDown, legacyRegistration));

            if ((_mask & PreviousMask) != 0)
                resolved.SetPrevious(ResolveTarget(PreviousMask, _previous, _resolvedPrevious, legacyRegistration));

            if ((_mask & NextMask) != 0)
                resolved.SetNext(ResolveTarget(NextMask, _next, _resolvedNext, legacyRegistration));

            return resolved;
        }

        NowResolvedId ResolveTarget(
            byte mask,
            NowId authored,
            NowResolvedId resolved,
            bool legacyRegistration)
        {
            if ((_resolvedMask & mask) != 0)
                return resolved;

            if (!legacyRegistration)
                return NowControls.ResolveNavigationTargetId(authored);

            int legacyId = authored.isString
                ? NowInput.GetLegacyId(authored.stringValue)
                : authored.intValue;
            return NowResolvedId.FromLegacy(legacyId);
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

        NowResolvedId _left;
        NowResolvedId _right;
        NowResolvedId _up;
        NowResolvedId _down;
        NowResolvedId _previous;
        NowResolvedId _next;
        byte _mask;

        public void SetLeft(NowResolvedId id) { _left = id; SetMask(LeftMask, id.hasValue); }

        public void SetRight(NowResolvedId id) { _right = id; SetMask(RightMask, id.hasValue); }

        public void SetUp(NowResolvedId id) { _up = id; SetMask(UpMask, id.hasValue); }

        public void SetDown(NowResolvedId id) { _down = id; SetMask(DownMask, id.hasValue); }

        public void SetPrevious(NowResolvedId id) { _previous = id; SetMask(PreviousMask, id.hasValue); }

        public void SetNext(NowResolvedId id) { _next = id; SetMask(NextMask, id.hasValue); }

        void SetMask(byte mask, bool enabled)
        {
            if (enabled)
                _mask |= mask;
            else
                _mask &= (byte)~mask;
        }

        public bool TryGetDirectional(Vector2 direction, out NowResolvedId id)
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

            id = default;
            return false;
        }

        public bool TryGetOrder(int step, out NowResolvedId id)
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

            id = default;
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

        const string LegacyControlIdObsoleteMessage =
            "Raw integer focus identities were removed. Resolve an authored NowId once and use the NowResolvedId overload.";

        const string LegacyHostIdObsoleteMessage =
            "Use the NowResolvedId overload. Raw integer host handles are mapped only through the isolated legacy FocusHost domain.";

        static NowResolvedId LegacyControlId(int id)
        {
            return NowResolvedId.FromLegacy(id);
        }

        static NowResolvedId LegacyHostId(int hostId)
        {
            return hostId != 0
                ? NowResolvedId.FromLegacy(hostId).InDomain(NowIdDomain.FocusHost)
                : NowResolvedId.None;
        }

        struct Focusable
        {
            public NowResolvedId id;
            public int legacyId;
            public Rect rect;
            public Rect visibleRect;
            public NowResolvedId scrollRegionId;
            public NowResolvedId overlayLayerId;
            public ResolvedFocusNavigation navigation;
            public NowFocusNavigationLock navigationLock;
            public bool consumesCancel;
        }

        sealed class HostRegistry
        {
            public readonly NowResolvedId hostId;

            public List<Focusable> focusables = new List<Focusable>(32);

            public List<Focusable> buildingFocusables = new List<Focusable>(32);

            public Dictionary<NowResolvedId, NowResolvedId> owners =
                new Dictionary<NowResolvedId, NowResolvedId>(16);

            public Dictionary<NowResolvedId, NowResolvedId> buildingOwners =
                new Dictionary<NowResolvedId, NowResolvedId>(16);

            public INowFocusNavigationProxy proxy;

            public NowFocusNavigationLock claimedNavigationLock;

            public int claimedNavigationLockFocusRevision;

            public NowFocusNavigationLock buildingNavigationLock;

            public int buildingNavigationLockFocusRevision;

            public NowResolvedId pendingCancelOwnerId;

            public bool hasPendingEntry;

            public Vector2 pendingEntryDirection;

            public int pendingEntryOrderStep;

            public int pendingTabBoundaryStep;

            public NowResolvedId pendingTabFocusId;

            public int pendingTabFocusRevision;

            public bool hasPendingDirectionalBoundary;

            public Vector2 pendingDirectionalBoundary;

            public NowResolvedId pendingDirectionalBoundaryFocusId;

            public int pendingDirectionalBoundaryFocusRevision;

            public ulong pendingDirectionalBoundaryRegistrationVersion;

            public ulong registrationVersion;

            public NowResolvedId directionalReturnId;

            public bool retainFocus;

            public bool buildingRetainFocus;

            public bool isRegistering;

            public bool unregisterPending;

            public int lastProcessedInputPass = int.MinValue;

            public Vector2 lastNavigation;

            public Vector2 repeatDirection;

            public float nextNavigationRepeatTime;

            public HostRegistry(NowResolvedId hostId)
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

        static readonly List<NowResolvedId> _scrollRegionStack =
            new List<NowResolvedId>(4);

        static readonly List<int> _legacyScrollRegionStack = new List<int>(4);

        static readonly List<Focusable> _current = new List<Focusable>(32);

        static readonly Dictionary<NowResolvedId, int> _currentIndices =
            new Dictionary<NowResolvedId, int>(32);

        static readonly List<Focusable> _previous = new List<Focusable>(32);

        static readonly Dictionary<NowResolvedId, HostRegistry> _hostRegistries =
            new Dictionary<NowResolvedId, HostRegistry>(4);

        static readonly List<HostRegistry> _hostRegistrationStack =
            new List<HostRegistry>(2);

        static readonly NowScopeGuard _hostRegistrationScopes =
            new NowScopeGuard("NowFocus.BeginHostRegistration", 2);

        static NowResolvedId _focusedId;

        static int _focusedLegacyId;

        static NowResolvedId _focusedHostId;

        static int _focusRevision;

        static int _registryFrame = -1;

        static NowFocusNavigationLock _navigationLockCurrent;

        static int _navigationLockCurrentFocusRevision;

        static NowFocusNavigationLock _navigationLockPrevious;

        static int _navigationLockPreviousFocusRevision;

        static NowResolvedId _explicitFocusRequestId;

        static NowResolvedId _explicitFocusRequestHostId;

        static NowResolvedId _pendingCancelOwnerId;

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
        /// A <c>NowUGUINavigationProxy</c> instead remains selected while
        /// its host owns focus and delegates only true boundary moves to UGUI.
        /// </summary>
        public static bool respectEventSystem = true;

        /// <summary>The focused resolved control id, or <see cref="NowResolvedId.None"/>.</summary>
        public static NowResolvedId focusedResolvedId => _focusedId;

        /// <summary>
        /// Source-blocked compatibility view retained only so old callers receive
        /// a compiler-guided migration to <see cref="focusedResolvedId"/>.
        /// </summary>
        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static int focusedId => _focusedLegacyId;

        internal static int focusRevision => _focusRevision;

        internal static int immediateRegistrationCount => _current.Count;

        internal static bool IsFocusedInHost(NowResolvedId hostId)
        {
            return hostId.hasValue && _focusedId.hasValue && _focusedHostId == hostId;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static bool IsFocusedInHost(int hostId)
        {
            return IsFocusedInHost(LegacyHostId(hostId));
        }

        internal static bool IsFocusedOutsideHost(NowResolvedId hostId)
        {
            return _focusedId.hasValue && _focusedHostId != hostId;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static bool IsFocusedOutsideHost(int hostId)
        {
            return IsFocusedOutsideHost(LegacyHostId(hostId));
        }

        internal static void PrepareUGUIEntry(NowResolvedId hostId)
        {
            if (!IsFocusedOutsideHost(hostId))
                return;

            bool preserveExplicitTransfer =
                _explicitFocusRequestId.hasValue &&
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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void PrepareUGUIEntry(int hostId)
        {
            PrepareUGUIEntry(LegacyHostId(hostId));
        }

        public static bool IsFocused(NowResolvedId id)
        {
            return id.hasValue && _focusedId == id && IsFocusedInActiveLayer(id);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static bool IsFocused(int id)
        {
            return IsFocused(LegacyControlId(id));
        }

        static readonly Dictionary<NowResolvedId, NowResolvedId> _ownersCurrent =
            new Dictionary<NowResolvedId, NowResolvedId>(16);

        static readonly Dictionary<NowResolvedId, NowResolvedId> _ownersPrevious =
            new Dictionary<NowResolvedId, NowResolvedId>(16);

        /// <summary>
        /// Declares that a control or overlay layer belongs to an owner for
        /// <see cref="IsFocusedWithin"/>. Call every interactive frame while
        /// the relationship exists, like <see cref="Register"/> — an editor
        /// declares its inline rename field, a control declares the context
        /// menu it opened, a menu declares its submenu overlays.
        /// </summary>
        public static void DeclareOwner(NowResolvedId id, NowResolvedId ownerId)
        {
            if (!id.hasValue || !ownerId.hasValue || id == ownerId || NowInput.isPassive)
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

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static void DeclareOwner(int id, int ownerId)
        {
            DeclareOwner(LegacyControlId(id), LegacyControlId(ownerId));
        }

        /// <summary>
        /// Focus-within: true when this control is focused, when focus sits on
        /// a control it owns (transitively, via <see cref="DeclareOwner"/>), or
        /// when the active overlay focus layer belongs to it. This is what
        /// visuals should test — a parent whose inline field, popup or context
        /// menu is active keeps rendering focused instead of blinking through
        /// every internal handoff.
        /// </summary>
        public static bool IsFocusedWithin(NowResolvedId id)
        {
            if (!id.hasValue)
                return false;

            NowResolvedId hostId = ActiveHostRegistry()?.hostId ?? _focusedHostId;

            if (OwnerChainReaches(_focusedId, id, hostId))
                return true;

            NowResolvedId layerId = NowOverlay.activeFocusLayerSourceId;
            return layerId.hasValue && OwnerChainReaches(layerId, id, hostId);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static bool IsFocusedWithin(int id)
        {
            return IsFocusedWithin(LegacyControlId(id));
        }

        static bool OwnerChainReaches(
            NowResolvedId cursor,
            NowResolvedId id,
            NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            for (int depth = 0; cursor.hasValue && depth < 8; ++depth)
            {
                if (cursor == id)
                    return true;

                NowResolvedId owner;
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

        public static void Focus(NowResolvedId id)
        {
            FocusResolved(id, 0);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static void Focus(int id)
        {
            FocusResolved(LegacyControlId(id), id);
        }

        static void FocusResolved(NowResolvedId id, int legacyId)
        {
            if (id.hasValue)
                NowInput.ClaimFocusForCurrentPrimaryPress();

            NowResolvedId hostId = ResolveFocusHost(id);

            if (_focusedId != id || _focusedHostId != hostId)
                SetFocused(id, hostId, legacyId);
            else if (legacyId != 0)
                _focusedLegacyId = legacyId;

            _explicitFocusRequestId = id;
            _explicitFocusRequestHostId = hostId;

            if (respectEventSystem && id.hasValue)
                NowEventSystemFocusBridge.SynchronizeFocus(hostId);
        }

        public static void Clear()
        {
            _explicitFocusRequestId = default;
            _explicitFocusRequestHostId = default;
            SetFocused(default, default);
        }

        internal static void ClearHostFocus(NowResolvedId hostId)
        {
            if (hostId.hasValue && _focusedHostId == hostId)
                Clear();
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void ClearHostFocus(int hostId)
        {
            ClearHostFocus(LegacyHostId(hostId));
        }

        internal static bool ClearOnUnhandledPrimaryPress()
        {
            if (!_focusedId.hasValue)
                return false;

            HostRegistry host = ActiveHostRegistry();
            NowResolvedId inputHostId = host != null ? host.hostId : default;

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

        static void SetFocused(NowResolvedId id)
        {
            SetFocused(id, ResolveFocusHost(id));
        }

        static void SetFocused(NowResolvedId id, NowResolvedId hostId, int legacyId = 0)
        {
            if (_focusedId == id && _focusedHostId == hostId)
            {
                if (legacyId != 0)
                    _focusedLegacyId = legacyId;

                return;
            }

            if (legacyId == 0 && _focusedId == id)
                legacyId = _focusedLegacyId;

            _focusedId = id;
            _focusedLegacyId = id.hasValue ? legacyId : 0;
            _focusedHostId = id.hasValue ? hostId : default;

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
        public static void Register(NowResolvedId id, NowRect rect)
        {
            Register(id, rect, default);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static void Register(int id, NowRect rect)
        {
            RegisterResolved(LegacyControlId(id), id, rect, default,
                NowFocusNavigationLock.None, false);
        }

        /// <summary>
        /// Adds a control to this frame's focus registry with optional explicit
        /// directional/Tab navigation targets.
        /// </summary>
        public static void Register(
            NowResolvedId id,
            NowRect rect,
            NowFocusNavigation navigation)
        {
            Register(id, rect, navigation, NowFocusNavigationLock.None);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static void Register(int id, NowRect rect, NowFocusNavigation navigation)
        {
            RegisterResolved(LegacyControlId(id), id, rect, navigation,
                NowFocusNavigationLock.None, false);
        }

        /// <summary>
        /// Adds a control with the navigation and cancel inputs it owns while focused.
        /// </summary>
        public static void Register(
            NowResolvedId id,
            NowRect rect,
            NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel = false)
        {
            RegisterResolved(id, 0, rect, navigation, navigationLock, consumesCancel);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static void Register(int id, NowRect rect, NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock, bool consumesCancel = false)
        {
            RegisterResolved(
                LegacyControlId(id), id, rect, navigation, navigationLock, consumesCancel);
        }

        static void RegisterResolved(
            NowResolvedId id,
            int legacyId,
            NowRect rect,
            NowFocusNavigation navigation,
            NowFocusNavigationLock navigationLock,
            bool consumesCancel)
        {
            if (!id.hasValue || NowInput.isPassive || rect.isEmpty)
                return;

            if (NowInput.current.primaryPressed && NowInput.IsHovered(rect))
                NowInput.ClaimFocusForCurrentPrimaryPress();

            NowRect visibleRect = Now.ApplyAmbientMask(rect);
            NowResolvedId scrollRegionId = CurrentScrollRegionId();

            if (visibleRect.isEmpty && !scrollRegionId.hasValue)
                return;

            var focusable = new Focusable
            {
                id = id,
                legacyId = legacyId,
                rect = scrollRegionId.hasValue ? (Rect)rect : (Rect)visibleRect,
                visibleRect = (Rect)visibleRect,
                scrollRegionId = scrollRegionId,
                overlayLayerId = NowOverlay.currentFocusLayerId,
                navigation = navigation.Resolve(legacyId != 0),
                navigationLock = navigationLock,
                consumesCancel = consumesCancel
            };

            HostRegistry host = ActiveHostRegistry();

            if (host != null)
            {
                if (_focusedId == id && !_focusedHostId.hasValue)
                {
                    SetFocused(id, host.hostId, legacyId);

                    if (_explicitFocusRequestId == id && !_explicitFocusRequestHostId.hasValue)
                        _explicitFocusRequestHostId = host.hostId;

                    if (respectEventSystem)
                        NowEventSystemFocusBridge.SynchronizeFocus(host.hostId);
                }

                host.buildingFocusables.Add(focusable);
                return;
            }

            BeginFrameIfNeeded();
            UpsertCurrentFocusable(focusable);
        }

        static void UpsertCurrentFocusable(Focusable focusable)
        {
            if (_currentIndices.TryGetValue(focusable.id, out int index))
            {
                _current[index] = focusable;
                return;
            }

            _currentIndices.Add(focusable.id, _current.Count);
            _current.Add(focusable);
        }

        internal static NowFocusScrollRegionScope BeginScrollRegion(NowResolvedId id)
        {
            return BeginScrollRegionResolved(id, 0);
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage)]
        internal static NowFocusScrollRegionScope BeginScrollRegion(int id)
        {
            return BeginScrollRegionResolved(LegacyControlId(id), id);
        }

        static NowFocusScrollRegionScope BeginScrollRegionResolved(
            NowResolvedId id,
            int legacyId)
        {
            if (!id.hasValue || NowInput.isPassive)
                return new NowFocusScrollRegionScope(false);

            if (ActiveHostRegistry() == null)
                BeginFrameIfNeeded();

            _scrollRegionStack.Add(id);
            _legacyScrollRegionStack.Add(legacyId);
            return new NowFocusScrollRegionScope(true);
        }

        internal static void PopScrollRegion()
        {
            if (_scrollRegionStack.Count > 0)
            {
                _scrollRegionStack.RemoveAt(_scrollRegionStack.Count - 1);
                _legacyScrollRegionStack.RemoveAt(_legacyScrollRegionStack.Count - 1);
            }
        }

        static NowResolvedId CurrentScrollRegionId()
        {
            return _scrollRegionStack.Count > 0
                ? _scrollRegionStack[_scrollRegionStack.Count - 1]
                : default;
        }

        /// <summary>The innermost resolved scroll region enclosing the current draw position.</summary>
        internal static NowResolvedId currentScrollRegionResolvedId => CurrentScrollRegionId();

        /// <summary>Legacy integer view of the current scroll-region identity.</summary>
        [System.Obsolete(LegacyControlIdObsoleteMessage)]
        internal static int currentScrollRegionId => _legacyScrollRegionStack.Count > 0
            ? _legacyScrollRegionStack[_legacyScrollRegionStack.Count - 1]
            : 0;

        internal static bool TryGetFocusedRectInScrollRegion(
            NowResolvedId scrollRegionId,
            out NowRect rect)
        {
            rect = default;

            if (!scrollRegionId.hasValue || !_focusedId.hasValue || NowInput.isPassive)
                return false;

            HostRegistry host = ActiveHostRegistry() ?? GetHostRegistry(_focusedHostId);

            if (host == null)
                BeginFrameIfNeeded();

            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

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

        [System.Obsolete(LegacyControlIdObsoleteMessage)]
        internal static bool TryGetFocusedRectInScrollRegion(
            int scrollRegionId,
            out NowRect rect)
        {
            return TryGetFocusedRectInScrollRegion(LegacyControlId(scrollRegionId), out rect);
        }

        static bool TryGetFocusedRectInScrollRegion(
            List<Focusable> focusables,
            NowResolvedId scrollRegionId,
            NowResolvedId activeLayerId,
            out NowRect rect)
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
        public static bool SubmitPressed(NowResolvedId id)
        {
            bool submitted =
                IsFocused(id) &&
                !NowInput.isPassive &&
                NowInput.current.submitPressed;

            if (submitted)
                NowInput.ConsumeKeyActivity();

            return submitted;
        }

        [System.Obsolete(LegacyControlIdObsoleteMessage, true)]
        public static bool SubmitPressed(int id)
        {
            return SubmitPressed(LegacyControlId(id));
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
            NowResolvedId hostId,
            INowFocusNavigationProxy proxy)
        {
            if (!hostId.hasValue)
                throw new System.ArgumentException("An empty focus host id is reserved.", nameof(hostId));

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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static NowFocusHostRegistrationScope BeginHostRegistration(
            int hostId,
            INowFocusNavigationProxy proxy)
        {
            return BeginHostRegistration(LegacyHostId(hostId), proxy);
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

        internal static void UnregisterHost(NowResolvedId hostId)
        {
            if (!hostId.hasValue ||
                !_hostRegistries.TryGetValue(hostId, out HostRegistry host))
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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void UnregisterHost(int hostId)
        {
            UnregisterHost(LegacyHostId(hostId));
        }

        internal static void ExitUGUINavigation(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                host.directionalReturnId = default;

            if (hostId.hasValue && _focusedHostId == hostId)
                Clear();
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void ExitUGUINavigation(int hostId)
        {
            ExitUGUINavigation(LegacyHostId(hostId));
        }

        internal static void ExitUGUINavigationAtDirectionalBoundary(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
            {
            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;
                host.directionalReturnId =
                    _focusedHostId == hostId &&
                    ContainsFocusableInLayer(
                        host.focusables,
                        _focusedId,
                        activeLayerId)
                        ? _focusedId
                        : default;
            }

            if (hostId.hasValue && _focusedHostId == hostId)
                Clear();
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void ExitUGUINavigationAtDirectionalBoundary(int hostId)
        {
            ExitUGUINavigationAtDirectionalBoundary(LegacyHostId(hostId));
        }

        internal static void DiscardUGUIDirectionalReturn(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                host.directionalReturnId = default;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void DiscardUGUIDirectionalReturn(int hostId)
        {
            DiscardUGUIDirectionalReturn(LegacyHostId(hostId));
        }

        internal static bool DeferUGUIDirectionalBoundary(
            NowResolvedId hostId,
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
                _focusedHostId == hostId ? _focusedId : default;
            host.pendingDirectionalBoundaryFocusRevision = _focusRevision;
            host.pendingDirectionalBoundaryRegistrationVersion =
                host.registrationVersion + (host.isRegistering ? 2UL : 1UL);
            return true;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static bool DeferUGUIDirectionalBoundary(int hostId, Vector2 direction)
        {
            return DeferUGUIDirectionalBoundary(LegacyHostId(hostId), direction);
        }

        internal static void CancelDeferredUGUIDirectionalBoundary(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host != null)
                ClearPendingHostDirectionalBoundary(host);
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void CancelDeferredUGUIDirectionalBoundary(int hostId)
        {
            CancelDeferredUGUIDirectionalBoundary(LegacyHostId(hostId));
        }

        internal static void DeferUGUINavigationEntry(
            NowResolvedId hostId,
            Vector2 direction)
        {
            if (!hostId.hasValue)
                return;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.hasPendingEntry = true;
            host.pendingEntryDirection = direction;
            host.pendingEntryOrderStep = 0;

            if (!TryResolveUGUIDirection(direction, out _))
                host.directionalReturnId = default;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void DeferUGUINavigationEntry(int hostId, Vector2 direction)
        {
            DeferUGUINavigationEntry(LegacyHostId(hostId), direction);
        }

        internal static void DeferUGUITabEntry(NowResolvedId hostId, int step)
        {
            if (!hostId.hasValue || step == 0)
                return;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.directionalReturnId = default;
            host.hasPendingEntry = true;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = step < 0 ? -1 : 1;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void DeferUGUITabEntry(int hostId, int step)
        {
            DeferUGUITabEntry(LegacyHostId(hostId), step);
        }

        internal static void CancelPendingUGUIEntry(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null)
                return;

            host.hasPendingEntry = false;
            host.pendingEntryDirection = default;
            host.pendingEntryOrderStep = 0;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static void CancelPendingUGUIEntry(int hostId)
        {
            CancelPendingUGUIEntry(LegacyHostId(hostId));
        }

        static HostRegistry ActiveHostRegistry()
        {
            int count = _hostRegistrationStack.Count;
            return count > 0 ? _hostRegistrationStack[count - 1] : null;
        }

        static HostRegistry GetHostRegistry(NowResolvedId hostId)
        {
            if (!hostId.hasValue)
                return null;

            _hostRegistries.TryGetValue(hostId, out HostRegistry host);
            return host;
        }

        static HostRegistry GetOrCreateHostRegistry(NowResolvedId hostId)
        {
            if (_hostRegistries.TryGetValue(hostId, out HostRegistry host))
                return host;

            host = new HostRegistry(hostId);
            _hostRegistries.Add(hostId, host);
            return host;
        }

        static NowResolvedId ResolveFocusHost(NowResolvedId id)
        {
            if (!id.hasValue)
                return default;

            HostRegistry active = ActiveHostRegistry();

            if (active != null)
                return active.hostId;

            if (_focusedId == id && _focusedHostId.hasValue)
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

            return default;
        }

        static bool ContainsFocusable(List<Focusable> focusables, NowResolvedId id)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id)
                    return true;
            }

            return false;
        }

        static int LegacyIdForFocusable(
            List<Focusable> focusables,
            NowResolvedId id)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id)
                    return focusables[i].legacyId;
            }

            return 0;
        }

        internal static bool IsOwningProxySelection(
            NowResolvedId hostId,
            GameObject selection)
        {
            HostRegistry host = GetHostRegistry(hostId);
            return host != null &&
                host.proxy != null &&
                host.proxy.owningSelection == selection;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static bool IsOwningProxySelection(int hostId, GameObject selection)
        {
            return IsOwningProxySelection(LegacyHostId(hostId), selection);
        }

        internal static INowFocusNavigationProxy GetHostProxy(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);
            return host != null ? host.proxy : null;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static INowFocusNavigationProxy GetHostProxy(int hostId)
        {
            return GetHostProxy(LegacyHostId(hostId));
        }

        static bool IsOwningProxySelected(INowFocusNavigationProxy proxy)
        {
            return NowEventSystemFocusBridge.IsOwningProxySelected(proxy);
        }

        static void FinalizeHostPendingCancelOwner(HostRegistry host)
        {
            if (!host.pendingCancelOwnerId.hasValue)
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

            host.pendingCancelOwnerId = default;
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
                host.directionalReturnId = default;
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
            NowResolvedId expectedFocusId = host.pendingTabFocusId;
            int expectedFocusRevision = host.pendingTabFocusRevision;
            host.pendingTabBoundaryStep = 0;
            host.pendingTabFocusId = default;
            host.pendingTabFocusRevision = 0;

            if (step == 0 ||
                !IsOwningProxySelected(host.proxy) ||
                _focusRevision != expectedFocusRevision ||
                (_focusedHostId == host.hostId ? _focusedId : default) !=
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
            NowResolvedId expectedFocusId = host.pendingDirectionalBoundaryFocusId;
            int expectedFocusRevision =
                host.pendingDirectionalBoundaryFocusRevision;
            ClearPendingHostDirectionalBoundary(host);

            if (!IsOwningProxySelected(host.proxy) ||
                _focusRevision != expectedFocusRevision ||
                (_focusedHostId == host.hostId ? _focusedId : default) !=
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
            host.pendingDirectionalBoundaryFocusId = default;
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
            _currentIndices.Clear();

            _ownersPrevious.Clear();
            foreach (var owner in _ownersCurrent)
                _ownersPrevious[owner.Key] = owner.Value;
            _ownersCurrent.Clear();

            ProcessNavigation();
        }

        static void FinalizePendingCancelOwner()
        {
            if (!_pendingCancelOwnerId.hasValue)
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

            _pendingCancelOwnerId = default;
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

        /// <summary>
        /// Processes a hostless IMGUI Tab pulse while its native key event is
        /// still current. Editor IMGUI can dispatch several passes without
        /// advancing <see cref="Time.frameCount"/>.
        /// </summary>
        internal static void ProcessImmediateTabNavigationPass()
        {
            if (NowInput.isPassive || ActiveHostRegistry() != null)
                return;

            _preserveInputClaimsOnNextSwap = false;
            _registryFrame = -1;
            BeginFrameIfNeeded();
        }

        static void ProcessNavigation()
        {
            int ignoredPendingTabBoundaryStep = 0;
            NowResolvedId ignoredPendingTabFocusId = default;
            int ignoredPendingTabFocusRevision = 0;
            ProcessNavigation(
                _previous,
                default,
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

            if (host.lastProcessedInputPass == snapshot.inputPass)
                return;

            host.lastProcessedInputPass = snapshot.inputPass;
            host.pendingTabBoundaryStep = 0;
            host.pendingTabFocusId = default;
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
            NowResolvedId hostId,
            INowFocusNavigationProxy proxy,
            bool deferProxyTabBoundary,
            ref int pendingTabBoundaryStep,
            ref NowResolvedId pendingTabFocusId,
            ref int pendingTabFocusRevision,
            NowInputSnapshot snapshot,
            NowFocusNavigationLock claimedNavigationLock,
            int claimedNavigationLockFocusRevision,
            ref NowResolvedId pendingCancelOwnerId,
            bool retainFocus,
            ref Vector2 lastNavigation,
            ref Vector2 repeatDirection,
            ref float nextNavigationRepeatTime)
        {
            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;
            bool ownsFocus = _focusedId.hasValue && _focusedHostId == hostId;
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

            if (respectEventSystem &&
                NowEventSystemFocusBridge.HasForeignSelection(proxy))
            {
                if (ownsFocus)
                    Clear();

                lastNavigation = snapshot.navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (_focusedId.hasValue && _focusedHostId != hostId)
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

                if (ownsFocus)
                    NowInput.ConsumeKeyActivity();

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
            bool protectExplicitFocus = _explicitFocusRequestId.hasValue &&
                _explicitFocusRequestHostId == hostId &&
                _explicitFocusRequestId == _focusedId &&
                !focusedWasRegistered;

            if (_explicitFocusRequestHostId == hostId)
            {
                _explicitFocusRequestId = default;
                _explicitFocusRequestHostId = default;
            }

            if (protectExplicitFocus &&
                (snapshot.focusPreviousPressed || snapshot.focusNextPressed ||
                 ResolveNavigationDirection(navigation) != default))
            {
                if (snapshot.focusPreviousPressed || snapshot.focusNextPressed)
                    NowInput.ConsumeKeyActivity();

                lastNavigation = navigation;
                ResetNavigationRepeat(ref repeatDirection, ref nextNavigationRepeatTime);
                return;
            }

            if (focusedNavigationLock == NowFocusNavigationLock.All)
            {
                if (snapshot.focusPreviousPressed || snapshot.focusNextPressed)
                    NowInput.ConsumeKeyActivity();

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
                        _focusedHostId == hostId ? _focusedId : default;
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

                NowInput.ConsumeKeyActivity();
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
            NowInput.ConsumeKeyActivity();
        }

        internal static NowFocusMoveResult RouteUGUINavigation(
            NowResolvedId hostId,
            Vector2 direction)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || !TryResolveUGUIDirection(direction, out Vector2 resolvedDirection))
                return NowFocusMoveResult.Unavailable;

            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static NowFocusMoveResult RouteUGUINavigation(int hostId, Vector2 direction)
        {
            return RouteUGUINavigation(LegacyHostId(hostId), direction);
        }

        internal static bool IsUGUIDirectionalNavigationLocked(NowResolvedId hostId)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || _focusedHostId != hostId)
                return false;

            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;
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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static bool IsUGUIDirectionalNavigationLocked(int hostId)
        {
            return IsUGUIDirectionalNavigationLocked(LegacyHostId(hostId));
        }

        internal static NowFocusMoveResult RouteUGUITab(NowResolvedId hostId, int step)
        {
            HostRegistry host = GetHostRegistry(hostId);

            if (host == null || step == 0)
                return NowFocusMoveResult.Unavailable;

            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

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

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static NowFocusMoveResult RouteUGUITab(int hostId, int step)
        {
            return RouteUGUITab(LegacyHostId(hostId), step);
        }

        internal static NowFocusMoveResult EnterUGUINavigation(
            NowResolvedId hostId,
            Vector2 direction)
        {
            if (!hostId.hasValue)
                return NowFocusMoveResult.Unavailable;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            bool hasDirection =
                TryResolveUGUIDirection(direction, out Vector2 resolvedDirection);

            if (!hasDirection)
                host.directionalReturnId = default;

            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

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
                host.directionalReturnId = default;
                return NowFocusMoveResult.Consumed;
            }

            NowResolvedId directionalReturnId = host.directionalReturnId;
            host.directionalReturnId = default;

            if (directionalReturnId.hasValue)
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

            NowResolvedId id;

            if (hasDirection)
                id = FindEdgeFocus(host.focusables, resolvedDirection, activeLayerId);
            else
                id = FindFirstFocus(host.focusables, activeLayerId);

            if (!id.hasValue)
                return NowFocusMoveResult.Unavailable;

            SetFocused(id, hostId, LegacyIdForFocusable(host.focusables, id));
            return NowFocusMoveResult.Seeded;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static NowFocusMoveResult EnterUGUINavigation(int hostId, Vector2 direction)
        {
            return EnterUGUINavigation(LegacyHostId(hostId), direction);
        }

        internal static NowFocusMoveResult EnterUGUITab(NowResolvedId hostId, int step)
        {
            if (!hostId.hasValue || step == 0)
                return NowFocusMoveResult.Unavailable;

            HostRegistry host = GetOrCreateHostRegistry(hostId);
            host.directionalReturnId = default;
            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

            if (IsFocusedOutsideHost(hostId))
                return NowFocusMoveResult.Consumed;

            if (_explicitFocusRequestHostId == hostId &&
                _explicitFocusRequestId.hasValue &&
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

            NowResolvedId id = FindRegistrationEdgeFocus(
                host.focusables,
                activeLayerId,
                step < 0 ? -1 : 1);

            if (!id.hasValue)
                return NowFocusMoveResult.Unavailable;

            bool changed = _focusedHostId != hostId || _focusedId != id;
            SetFocused(id, hostId, LegacyIdForFocusable(host.focusables, id));
            return changed ? NowFocusMoveResult.Seeded : NowFocusMoveResult.Consumed;
        }

        [System.Obsolete(LegacyHostIdObsoleteMessage)]
        internal static NowFocusMoveResult EnterUGUITab(int hostId, int step)
        {
            return EnterUGUITab(LegacyHostId(hostId), step);
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
            NowResolvedId hostId,
            NowResolvedId activeLayerId,
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
            NowResolvedId hostId,
            int step,
            NowResolvedId activeLayerId,
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
                Focusable fallback = focusables[fallbackIndex];
                SetFocused(fallback.id, hostId, fallback.legacyId);
                return NowFocusMoveResult.Seeded;
            }

            if (focusables[focusedIndex].navigation.TryGetOrder(
                    step,
                    out NowResolvedId targetId) &&
                TryFocusRegistered(focusables, hostId, targetId, activeLayerId, out _))
            {
                return NowFocusMoveResult.Moved;
            }

            int next = FindNextFocusableIndex(
                focusables, focusedIndex, step, activeLayerId, wrap);

            if (next < 0)
                return NowFocusMoveResult.Boundary;

            Focusable nextFocusable = focusables[next];
            SetFocused(nextFocusable.id, hostId, nextFocusable.legacyId);
            return NowFocusMoveResult.Moved;
        }

        static NowFocusMoveResult MoveFocus(
            List<Focusable> focusables,
            NowResolvedId hostId,
            Vector2 direction,
            NowResolvedId activeLayerId)
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
                NowResolvedId seeded = FindEdgeFocus(focusables, direction, activeLayerId);

                if (!seeded.hasValue)
                    return NowFocusMoveResult.Unavailable;

                SetFocused(seeded, hostId, LegacyIdForFocusable(focusables, seeded));
                return NowFocusMoveResult.Seeded;
            }

            if (focusables[focusedIndex].navigation.TryGetDirectional(
                    direction,
                    out NowResolvedId targetId) &&
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

            Focusable best = focusables[bestIndex];
            SetFocused(best.id, hostId, best.legacyId);

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
            NowResolvedId hostId,
            NowResolvedId id,
            NowResolvedId activeLayerId,
            out Rect rect)
        {
            rect = default;

            if (!id.hasValue)
                return false;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id &&
                    IsFocusableInLayer(focusables[i], activeLayerId))
                {
                    SetFocused(id, hostId, focusables[i].legacyId);
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
        static NowResolvedId FindEdgeFocus(
            List<Focusable> focusables,
            Vector2 direction,
            NowResolvedId activeLayerId)
        {
            float bestVisibleScore = float.MaxValue;
            NowResolvedId bestVisibleId = default;
            float bestScore = float.MaxValue;
            NowResolvedId bestId = default;
            NowResolvedId fallbackId = default;

            for (int i = 0; i < focusables.Count; ++i)
            {
                if (!IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                if (!fallbackId.hasValue)
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

            if (bestVisibleId.hasValue)
                return bestVisibleId;

            return bestId.hasValue ? bestId : fallbackId;
        }

        static NowResolvedId FindFirstFocus(
            List<Focusable> focusables,
            NowResolvedId activeLayerId)
        {
            return FindRegistrationEdgeFocus(focusables, activeLayerId, 1);
        }

        static NowResolvedId FindRegistrationEdgeFocus(
            List<Focusable> focusables,
            NowResolvedId activeLayerId,
            int step)
        {
            NowResolvedId fallbackId = default;

            int start = step < 0 ? focusables.Count - 1 : 0;
            int end = step < 0 ? -1 : focusables.Count;

            for (int i = start; i != end; i += step)
            {
                if (!IsFocusableInLayer(focusables[i], activeLayerId))
                    continue;

                if (!fallbackId.hasValue)
                    fallbackId = focusables[i].id;

                if (focusables[i].visibleRect.width > 0f &&
                    focusables[i].visibleRect.height > 0f)
                {
                    return focusables[i].id;
                }
            }

            return fallbackId;
        }

        static bool IsFocusedInActiveLayer(NowResolvedId id)
        {
            NowResolvedId activeLayerId = NowOverlay.activeFocusLayerId;

            if (!activeLayerId.hasValue)
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

        static bool ContainsFocusableInLayer(
            List<Focusable> focusables,
            NowResolvedId id,
            NowResolvedId activeLayerId)
        {
            for (int i = 0; i < focusables.Count; ++i)
            {
                if (focusables[i].id == id && IsFocusableInLayer(focusables[i], activeLayerId))
                    return true;
            }

            return false;
        }

        static bool HasFocusableInLayer(
            List<Focusable> focusables,
            NowResolvedId activeLayerId)
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
            NowResolvedId activeLayerId,
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

        static bool IsFocusableInLayer(Focusable focusable, NowResolvedId activeLayerId)
        {
            return focusable.overlayLayerId == activeLayerId;
        }

        public static void Reset()
        {
            _current.Clear();
            _currentIndices.Clear();
            _previous.Clear();
            _ownersCurrent.Clear();
            _ownersPrevious.Clear();
            _scrollRegionStack.Clear();
            _legacyScrollRegionStack.Clear();
            _hostRegistries.Clear();
            _hostRegistrationStack.Clear();
            _hostRegistrationScopes.Clear();
            _focusedId = default;
            _focusedLegacyId = 0;
            _focusedHostId = default;
            _focusRevision = 0;
            _registryFrame = -1;
            _navigationLockCurrent = NowFocusNavigationLock.None;
            _navigationLockCurrentFocusRevision = 0;
            _navigationLockPrevious = NowFocusNavigationLock.None;
            _navigationLockPreviousFocusRevision = 0;
            _explicitFocusRequestId = default;
            _explicitFocusRequestHostId = default;
            _pendingCancelOwnerId = default;
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
