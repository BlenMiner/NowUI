using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public interface INowInputProvider
    {
        bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot snapshot);
    }


    public readonly struct NowInteraction
    {
        public readonly NowResolvedId id;

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

        /// <summary>True when native pointer capture was lost without a normal release.</summary>
        public readonly bool cancelled;

        /// <summary>True when a drag was active when native pointer capture was lost.</summary>
        public readonly bool dragCancelled;

        internal NowInteraction(
            NowResolvedId id,
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
            bool dragEnded,
            bool cancelled,
            bool dragCancelled)
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
            this.cancelled = cancelled;
            this.dragCancelled = dragCancelled;
        }

        /// <summary>Derives a stable sub-id from this interaction's resolved control id.</summary>
        public NowResolvedId GetId(string key)
        {
            return id.Child(key);
        }

        /// <summary>Derives a stable numeric sub-id from this interaction's resolved control id.</summary>
        public NowResolvedId GetId(int key)
        {
            return id.Child(key);
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

        static NowResolvedId _activeId;

        static INowInputProvider _activeProvider;

        static NowPointerButton _activeButton;

        static NowResolvedId _dragId;

        static bool _activeDragged;

        static bool _activeSeenThisFrame;

        static int _activeLastSeenFrame = -1;

        static Vector2 _pressPosition;

        static float _dragThresholdOverride = -1f;

        static NowNavigationKeys _navigationKeys = NowNavigationKeys.All;

        static int _passiveDepth;

        static int _scopeDepth;

        static readonly NowScopeGuard _scopes = new NowScopeGuard("NowInput.Begin");

        static int _scopeStartedAt = int.MinValue;

        static bool _screenFrameActive;

        internal static bool hasActiveScopesThisFrame =>
            _scopes.count > 0 && _scopeStartedAt == Time.frameCount;

        internal static void DiscardAbandonedScopes()
        {
            NowOverlay.ClearFrameTransactions();
            _scopes.Clear();
            _scopeDepth = 0;
            _passiveDepth = 0;
            _scopeStartedAt = int.MinValue;
            _screenFrameActive = false;
        }

        internal static void BeginScreenFrame(NowInputSurface surface)
        {
            if (_screenFrameActive)
                throw new InvalidOperationException("A NowInput screen frame is already active.");

            Update(_defaultProvider, surface);
            _screenFrameActive = true;
        }

        internal static void CancelScreenFrame()
        {
            _screenFrameActive = false;
            NowOverlay.EndFrameTransaction(completed: false);
        }

        static bool _scrollConsumed;

        static bool _focusClaimedByPrimaryPress;

        static int _cancelClaimInputPass = int.MinValue;

        static int _cancelClaimFrame = int.MinValue;

        static INowInputProvider _cancelClaimProvider;

        struct PointerPressClaim
        {
            public INowInputProvider provider;
            public int inputPass;
            public NowPointerButtons buttons;
        }

        // A draw can temporarily enter another input provider and then return to
        // the original one. Key claims by both provider and input-pass instead
        // of keeping one global flag so nested surfaces cannot erase or inherit
        // each other's pointer-down ownership.
        static readonly List<PointerPressClaim> _pointerPressClaims =
            new List<PointerPressClaim>(4);

        public static INowInputProvider defaultProvider
        {
            get => _defaultProvider;
            set => _defaultProvider = value;
        }

        public static NowInputSurface surface => _surface;

        public static NowInputSnapshot current => _snapshot;

        /// <summary>The provider serving the active input scope — the surface identity for surface-owned state.</summary>
        internal static INowInputProvider currentProvider => _currentProvider;

        public static bool hasContext => _hasContext;

        public static NowResolvedId activeId => _activeId;

        public static NowPointerButton activeButton => _activeButton;

        internal static bool hasActiveInteraction =>
            _activeId.hasValue && ReferenceEquals(_currentProvider, _activeProvider);

        internal static bool IsActiveControl(
            NowResolvedId id,
            NowPointerButton button = NowPointerButton.Primary)
        {
            return _activeId == id &&
                _activeButton == button &&
                ReferenceEquals(_currentProvider, _activeProvider);
        }

        internal static bool focusClaimedByPrimaryPress => _focusClaimedByPrimaryPress;

        /// <summary>
        /// Distance in surface units a press may travel before it becomes a drag
        /// and stops counting as a click. Defaults to 4 scaled by display density
        /// (4 * Screen.dpi / 160, floored at 4) so taps on dense touch screens
        /// stay clicks; setting an explicit value wins unscaled until
        /// <see cref="Reset"/> restores the density-scaled default.
        /// </summary>
        public static float dragThreshold
        {
            get => _dragThresholdOverride >= 0f ? _dragThresholdOverride : ScaledDefaultDragThreshold();
            set => _dragThresholdOverride = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Which keyboard keys the built-in device input maps to UI navigation,
        /// focus, and submit. Defaults to <see cref="NowNavigationKeys.All"/> so
        /// arrows, WASD, Tab, Space, and Enter all drive the UI; games that use
        /// WASD or Space for gameplay can mask those out.
        /// </summary>
        public static NowNavigationKeys navigationKeys
        {
            get => _navigationKeys;
            set => _navigationKeys = value;
        }

        static float ScaledDefaultDragThreshold()
        {
            float dpi = Screen.dpi;
            return dpi > 0f ? DefaultDragThreshold * Mathf.Max(1f, dpi / 160f) : DefaultDragThreshold;
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

        static bool _warnedLeakedPassiveScope;

        /// <summary>
        /// Frame-entry self-heal: a NowEffects modifier/snapshot scope (or other
        /// passive scope) leaked in a previous frame would otherwise leave input
        /// disabled for the rest of the session. Clears the depth at every
        /// top-level frame start and reports the leak once so it is attributable.
        /// </summary>
        static void HealLeakedPassiveScope()
        {
            if (_passiveDepth == 0)
            {
                _warnedLeakedPassiveScope = false;
                return;
            }

            _passiveDepth = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_warnedLeakedPassiveScope)
            {
                _warnedLeakedPassiveScope = true;
                Debug.LogWarning("NowUI: a NowEffects or passive input scope from the previous frame was never disposed; input was re-enabled. Wrap the scope in a using statement.");
            }
#endif
        }

        public static NowInputScope Begin(INowInputProvider provider, NowInputSurface surface)
        {
            int previousScopeDepth = _scopeDepth;
            bool topLevel = previousScopeDepth == 0;
            bool beganOverlayTransaction = false;

            if (previousScopeDepth == 0)
                _scopeStartedAt = Time.frameCount;

            if (topLevel)
            {
                if (!_screenFrameActive)
                {
                    HealLeakedPassiveScope();
                    CompletePreviousFrameTransaction();
                }

            }

            var previousProvider = _currentProvider;
            var previousSurface = _surface;
            var previousSnapshot = _snapshot;
            bool previousHasContext = _hasContext;
            bool previousScrollConsumed = _scrollConsumed;
            int token = _scopes.Enter();
            var scope = new NowInputScope(
                previousProvider,
                previousSurface,
                previousSnapshot,
                previousHasContext,
                previousScrollConsumed,
                topLevel,
                token);
            bool previousActiveSeenThisFrame = _activeSeenThisFrame;
            ++_scopeDepth;

            try
            {
                Update(
                    provider,
                    surface,
                    resetFrameTracking: topLevel,
                    resetControlOccurrences: topLevel);

                if (topLevel)
                {
                    NowOverlay.BeginFrameTransaction(provider);
                    beganOverlayTransaction = true;
                }

                return scope;
            }
            catch
            {
                if (beganOverlayTransaction)
                    NowOverlay.EndFrameTransaction(completed: false);

                _scopes.Exit(token);
                _scopeDepth = previousScopeDepth;
                Restore(
                    previousProvider,
                    previousSurface,
                    previousSnapshot,
                    previousHasContext);
                _scrollConsumed = previousScrollConsumed;
                _activeSeenThisFrame = previousActiveSeenThisFrame;
                throw;
            }
        }

        internal static NowInputScope BeginMeasurement(INowInputProvider provider, NowInputSurface surface)
        {
            int previousScopeDepth = _scopeDepth;

            if (previousScopeDepth == 0)
                _scopeStartedAt = Time.frameCount;

            var previousProvider = _currentProvider;
            var previousSurface = _surface;
            var previousSnapshot = _snapshot;
            bool previousHasContext = _hasContext;
            bool previousScrollConsumed = _scrollConsumed;
            int token = _scopes.Enter();
            var scope = new NowInputScope(
                previousProvider,
                previousSurface,
                previousSnapshot,
                previousHasContext,
                previousScrollConsumed,
                false,
                token);
            ++_scopeDepth;

            try
            {
                Update(
                    provider,
                    surface,
                    resetFrameTracking: false,
                    resetControlOccurrences: previousScopeDepth == 0);
                return scope;
            }
            catch
            {
                _scopes.Exit(token);
                _scopeDepth = previousScopeDepth;
                Restore(
                    previousProvider,
                    previousSurface,
                    previousSnapshot,
                    previousHasContext);
                _scrollConsumed = previousScrollConsumed;
                throw;
            }
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
            if (_screenFrameActive)
            {
                throw new InvalidOperationException(
                    "NowInput.Update cannot replace the input context inside Now.StartUI. " +
                    "Use a scoped NowInput.Begin(...) for a temporary custom surface.");
            }

            bool topLevel = _scopeDepth == 0;
            bool beganOverlayTransaction = false;

            if (topLevel)
            {
                HealLeakedPassiveScope();
                CompletePreviousFrameTransaction();
            }

            try
            {
                Update(
                    provider,
                    surface,
                    resetFrameTracking: topLevel,
                    resetControlOccurrences: topLevel);

                if (topLevel)
                {
                    NowOverlay.BeginFrameTransaction(provider);
                    beganOverlayTransaction = true;
                }
            }
            catch
            {
                if (beganOverlayTransaction)
                    NowOverlay.EndFrameTransaction(completed: false);

                throw;
            }
        }

        static void CompletePreviousFrameTransaction()
        {
            bool completed = false;

            try
            {
                CompleteFrame();
                completed = true;
            }
            finally
            {
                NowOverlay.EndFrameTransaction(completed);
            }
        }

        static void Update(
            INowInputProvider provider,
            NowInputSurface surface,
            bool resetFrameTracking,
            bool resetControlOccurrences)
        {
            NowInputSnapshot snapshot = default;
            bool hasSnapshot = provider != null && provider.TryGetSnapshot(surface, out snapshot);

            _currentProvider = provider;
            _surface = surface;
            _snapshot = hasSnapshot ? snapshot : default;
            _hasContext = true;

            if (resetControlOccurrences)
                NowControls.ResetControlIdOccurrences();

            if (resetFrameTracking)
            {
                NowTextInput.BeginInputPass();
#if NOWUI_INPUT_SYSTEM
                NowKeyInput.BeginInputPass();
#endif
                _scrollConsumed = false;
                _pointerPressClaims.Clear();
                _focusClaimedByPrimaryPress = false;
                _activeSeenThisFrame = false;
            }

            if (hasSnapshot &&
                resetFrameTracking &&
                provider is NowIMGUIInputProvider imguiProvider &&
                !imguiProvider.isHostBacked &&
                (snapshot.focusPreviousPressed || snapshot.focusNextPressed))
            {
                NowFocus.ProcessImmediateTabNavigationPass();
            }

            if (hasSnapshot && resetFrameTracking)
                ClearStaleActiveFromMissingProvider();
        }

        public static bool IsHovered(NowRect rect)
        {
            return IsHovered((Rect)rect);
        }

        /// <summary>
        /// True when the pointer belongs to a composite region's bounds and is
        /// outside all of its child exclusions.
        /// </summary>
        public static bool IsHovered(in NowInteractionRegion region)
        {
            Rect screenRect = Now.TransformScreenRect(region.bounds);
            Vector2 pointer = _snapshot.pointerPosition;

            return _hasContext && _snapshot.hasPointer &&
                screenRect.Contains(pointer) &&
                !region.IsExcluded(Now.InverseTransformScreenPoint(pointer)) &&
                Now.IsInsideAmbientMask(pointer) &&
                !NowOverlay.IsPointerBlocked(pointer);
        }

        internal static void ClaimFocusForCurrentPrimaryPress()
        {
            if (_passiveDepth == 0 &&
                _hasContext &&
                _snapshot.hasPointer &&
                _snapshot.primaryPressed)
            {
                _focusClaimedByPrimaryPress = true;
            }
        }

        public static bool IsHovered(Rect rect)
        {
            rect = Now.TransformScreenRect(rect);

            return _hasContext && _snapshot.hasPointer &&
                rect.Contains(_snapshot.pointerPosition) &&
                Now.IsInsideAmbientMask(_snapshot.pointerPosition) &&
                !NowOverlay.IsPointerBlocked(_snapshot.pointerPosition);
        }

        /// <summary>
        /// True when a secondary-button press landed inside <paramref name="rect"/>
        /// this frame and the pointer actually belongs to that rect — ambient masks
        /// and overlay pointer blocks are respected and measure passes report false.
        /// The blessed check for opening a context menu over a region; raw snapshot
        /// checks leak right-clicks through open popups.
        /// </summary>
        public static bool WasRightClicked(NowRect rect)
        {
            return WasRightClicked((Rect)rect);
        }

        /// <summary>
        /// True when a secondary-button press belongs to a composite region and
        /// not to one of its excluded child controls.
        /// </summary>
        public static bool WasRightClicked(in NowInteractionRegion region)
        {
            return _passiveDepth == 0 &&
                _hasContext &&
                WasPointerPressedUnclaimed(NowPointerButton.Secondary) &&
                IsHovered(in region);
        }

        /// <summary>
        /// True when a secondary-button press landed inside <paramref name="rect"/>
        /// this frame, respecting ambient masks and overlay pointer blocks.
        /// </summary>
        public static bool WasRightClicked(Rect rect)
        {
            return _passiveDepth == 0 &&
                _hasContext &&
                WasPointerPressedUnclaimed(NowPointerButton.Secondary) &&
                IsHovered(rect);
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
                NowOverlay.IsPointerBlockedForScroll(_snapshot.pointerPosition))
            {
                return default;
            }

            _scrollConsumed = true;

            if (_currentProvider is NowIMGUIInputProvider imgui)
                imgui.NotifyScrollConsumed();

            return scroll;
        }

        /// <summary>
        /// Claims wheel input reserved by a popup even when its inner scroll
        /// view is already at an edge. This prevents an enclosing NowUI or
        /// native IMGUI scroll view from moving the popup's background.
        /// </summary>
        internal static void ConsumeRemainingOverlayScroll()
        {
            if (_passiveDepth > 0 ||
                _scrollConsumed ||
                !_hasContext ||
                _snapshot.scrollDelta == Vector2.zero)
            {
                return;
            }

            _scrollConsumed = true;

            if (_currentProvider is NowIMGUIInputProvider imgui)
                imgui.NotifyScrollConsumed();
        }

        /// <summary>
        /// Claims cancel presses for the calling control — a key-capture field
        /// swallowing Escape, for example. Call every frame the claim should
        /// hold, like the <see cref="NowFocus"/> navigation locks: dismissal paths
        /// that run later in the frame (popups, dialogs, menus) skip a claimed
        /// cancel via <see cref="cancelConsumed"/>, and the focus swap — which
        /// processes input before the claimant draws — honours the previous
        /// frame's claim, one frame late like overlay pointer blocks.
        /// </summary>
        public static void ConsumeCancel()
        {
            if (_passiveDepth > 0 || !_hasContext)
                return;

            _cancelClaimInputPass = _snapshot.inputPass;
            _cancelClaimFrame = _snapshot.frame;
            _cancelClaimProvider = _currentProvider;

            if (_snapshot.cancelPressed)
                ConsumeKeyActivity();
        }

        /// <summary>
        /// Claims a keyboard action that NowUI actually handled so a native
        /// IMGUI control outside the panel cannot process the same KeyDown.
        /// </summary>
        public static void ConsumeKeyActivity()
        {
            if (_passiveDepth > 0 || !_hasContext)
                return;

            if (_currentProvider is NowIMGUIInputProvider imgui)
                imgui.NotifyKeyActivityClaimed();
        }

        /// <summary>
        /// Claims a pointer-down that NowUI handled without turning it into a
        /// draggable control capture, such as modal popup dismissal.
        /// </summary>
        public static void ConsumePointerPress()
        {
            if (_passiveDepth > 0 ||
                !_hasContext ||
                !_snapshot.anyPointerPressed)
            {
                return;
            }

            ClaimPointerPresses(_snapshot.pointerButtonsPressed, notifyNative: true);
        }

        /// <summary>
        /// Claims one pointer button's press for the current input pass without
        /// creating a draggable control capture. Later NowUI controls in the
        /// same provider/pass no longer observe that press.
        /// </summary>
        public static void ConsumePointerPress(NowPointerButton button)
        {
            if (_passiveDepth > 0 ||
                !_hasContext ||
                !_snapshot.WasPointerPressed(button))
            {
                return;
            }

            ClaimPointerPresses(
                NowInputSnapshot.ToButtonMask(button),
                notifyNative: true);
        }

        static void ClaimPointerPresses(
            NowPointerButtons buttons,
            bool notifyNative)
        {
            if (buttons == NowPointerButtons.None)
                return;

            for (int i = 0; i < _pointerPressClaims.Count; ++i)
            {
                var claim = _pointerPressClaims[i];

                if (!ReferenceEquals(claim.provider, _currentProvider) ||
                    claim.inputPass != _snapshot.inputPass)
                {
                    continue;
                }

                claim.buttons |= buttons;
                _pointerPressClaims[i] = claim;

                if (notifyNative)
                    NotifyPointerPressConsumed();

                return;
            }

            _pointerPressClaims.Add(new PointerPressClaim
            {
                provider = _currentProvider,
                inputPass = _snapshot.inputPass,
                buttons = buttons
            });

            if (notifyNative)
                NotifyPointerPressConsumed();
        }

        static void NotifyPointerPressConsumed()
        {
            if (_currentProvider is NowIMGUIInputProvider imgui)
                imgui.NotifyPointerPressConsumed();
        }

        static bool WasPointerPressedUnclaimed(NowPointerButton button)
        {
            if (!_snapshot.WasPointerPressed(button))
                return false;

            NowPointerButtons mask = NowInputSnapshot.ToButtonMask(button);

            for (int i = _pointerPressClaims.Count - 1; i >= 0; --i)
            {
                var claim = _pointerPressClaims[i];

                if (ReferenceEquals(claim.provider, _currentProvider) &&
                    claim.inputPass == _snapshot.inputPass)
                {
                    return (claim.buttons & mask) == 0;
                }
            }

            return true;
        }

        /// <summary>True when a control claimed this frame's cancel press, so
        /// cancel-driven dismissal consumers must stand down.</summary>
        public static bool cancelConsumed =>
            _hasContext &&
            ReferenceEquals(_cancelClaimProvider, _currentProvider) &&
            _cancelClaimInputPass == _snapshot.inputPass;

        /// <summary>
        /// Cancel-claim check for consumers that process input at the frame
        /// swap, before the claiming control has drawn this frame: honours the
        /// previous frame's claim too.
        /// </summary>
        internal static bool cancelConsumedForFrameSwap =>
            _hasContext &&
            ReferenceEquals(_cancelClaimProvider, _currentProvider) &&
            _cancelClaimFrame != int.MinValue &&
            _cancelClaimFrame >= _snapshot.frame - 1;

        public static bool IsPointerDown(NowPointerButton button)
        {
            return _hasContext && _snapshot.IsPointerDown(button);
        }

        public static bool WasPointerPressed(NowPointerButton button)
        {
            return _hasContext && WasPointerPressedUnclaimed(button);
        }

        public static bool WasPointerReleased(NowPointerButton button)
        {
            return _hasContext && _snapshot.WasPointerReleased(button);
        }

        /// <summary>
        /// Derives a typed sub-element identity from an already-resolved parent.
        /// The edge is order-sensitive and preserves the parent's complete owner
        /// and ancestry; it cannot algebraically cancel an earlier path segment.
        /// </summary>
        public static NowResolvedId CombineId(NowResolvedId parent, int child)
        {
            return parent.Child(child);
        }

        /// <summary>Legacy 32-bit combiner retained for one migration cycle.</summary>
        [Obsolete("Use NowResolvedId.Child or CombineId(NowResolvedId, int). Raw integer identity composition is no longer supported.", true)]
        public static int CombineId(int a, int b)
        {
            unchecked
            {
                int id = (a * 397) ^ b;
                return id != 0 ? id : 1;
            }
        }

        static NowResolvedId CallerControlId(string file, int line)
        {
            return NowControls.GetControlId(NowControls.SiteToken(file, line));
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
            return Interact(NowControls.GetControlId(id), (Rect)rect, button);
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
            return Interact(NowControls.GetControlId(id), rect, button);
        }

        public static NowInteraction Interact(NowId id, Rect rect, NowPointerButton button)
        {
            return Interact(RequireExplicitId(id, nameof(id)), rect, button);
        }

        public static NowInteraction Interact(NowResolvedId id, NowRect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(NowResolvedId id, NowRect rect, NowPointerButton button)
        {
            return Interact(id, (Rect)rect, button);
        }

        public static NowInteraction Interact(NowResolvedId id, Rect rect)
        {
            return Interact(id, rect, NowPointerButton.Primary);
        }

        public static NowInteraction Interact(NowResolvedId id, Rect rect, NowPointerButton button)
        {
            var region = new NowInteractionRegion(rect);
            return Interact(id, in region, button);
        }

        /// <summary>
        /// Interacts with a composite region whose inline exclusions reserve
        /// child-control rectangles from the parent's hit target.
        /// </summary>
        public static NowInteraction Interact(
            NowResolvedId id,
            in NowInteractionRegion region)
        {
            return Interact(id, in region, NowPointerButton.Primary);
        }

        /// <summary>
        /// Interacts with a composite region using the requested pointer button.
        /// Bounds and exclusions are authored in the active local transform.
        /// </summary>
        public static NowInteraction Interact(
            NowResolvedId id,
            in NowInteractionRegion region,
            NowPointerButton button)
        {
            if (!id.hasValue)
                throw new ArgumentException("A resolved control id is required.", nameof(id));

            Rect localRect = region.bounds;
            Rect screenRect = Now.TransformScreenRect(region.bounds);

            if (_passiveDepth == 0 &&
                _currentProvider != null &&
                _currentProvider is not NowIMGUIInputProvider)
            {
                NowPointerArbiter.NoteContent(_currentProvider, screenRect);
            }

            ref readonly var snapshot = ref _snapshot;
            bool hasPointer = _hasContext && snapshot.hasPointer;
            Vector2 localPointerPosition = Now.InverseTransformScreenPoint(snapshot.pointerPosition);
            bool hovered = hasPointer &&
                screenRect.Contains(snapshot.pointerPosition) &&
                !region.IsExcluded(localPointerPosition) &&
                Now.IsInsideAmbientMask(snapshot.pointerPosition) &&
                !NowOverlay.IsPointerBlocked(snapshot.pointerPosition);
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
                    IsActiveControl(id, button),
                    false,
                    false,
                    false,
                    false,
                    false);
            }

            NowTextInput.MaintainCapture();

            bool freshPress =
                hovered &&
                WasPointerPressedUnclaimed(button);

            if (freshPress && snapshot.pointerCaptureCancelled)
            {
                if (_activeProvider is NowIMGUIInputProvider staleCapture)
                    staleCapture.CancelTrackedCapture(releaseNativeCapture: false);

                ClearActive();
                _snapshot.pointerCaptureCancelled = false;
            }

            if (freshPress &&
                _activeId.hasValue &&
                _activeButton == button &&
                !ReferenceEquals(_activeProvider, _currentProvider))
            {
                if (_activeProvider is NowIMGUIInputProvider staleIMGUI)
                    staleIMGUI.CancelTrackedCapture(releaseNativeCapture: false);

                ClearActive();
            }

            bool pressed = freshPress &&
                (!_activeId.hasValue || IsActiveControl(id, button));

            if (pressed &&
                _currentProvider is NowIMGUIInputProvider imgui &&
                !imgui.NotifyPointerCaptured(button))
            {
                pressed = false;
            }

            if (pressed)
            {
                // Native capture already consumed the host event above. Record
                // the semantic claim too so a later raw context-action check
                // cannot observe a child control's press in the same pass.
                ClaimPointerPresses(
                    NowInputSnapshot.ToButtonMask(button),
                    notifyNative: false);
                _activeId = id;
                _activeProvider = _currentProvider;
                _activeButton = button;
                _dragId = NowResolvedId.None;
                _activeDragged = false;
                _pressPosition = snapshot.pointerPosition;

            }

            bool active = IsActiveControl(id, button);

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
            bool cancelled = active &&
                (snapshot.pointerCaptureCancelled ||
                 (!snapshot.IsPointerDown(button) && !snapshot.WasPointerReleased(button)));
            bool dragCancelled = cancelled && _dragId == id;
            Vector2 dragDelta = default;

            if (cancelled)
            {
                active = false;
                held = false;
                ClearActive();
            }
            else if (held)
            {
                Vector2 dragOffset = snapshot.pointerPosition - _pressPosition;
                float threshold = dragThreshold;

                if (_dragId == id || dragOffset.sqrMagnitude >= threshold * threshold)
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
                dragEnded,
                cancelled,
                dragCancelled);

            if (released)
                ClearActive();

            return interaction;
        }

        [Obsolete("Use Interact(NowResolvedId, ...). Raw integer control ids are no longer accepted.", true)]
        public static NowInteraction Interact(int id, NowRect rect)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect);
        }

        [Obsolete("Use Interact(NowResolvedId, ...). Raw integer control ids are no longer accepted.", true)]
        public static NowInteraction Interact(int id, NowRect rect, NowPointerButton button)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect, button);
        }

        [Obsolete("Use Interact(NowResolvedId, ...). Raw integer control ids are no longer accepted.", true)]
        public static NowInteraction Interact(int id, Rect rect)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect);
        }

        [Obsolete("Use Interact(NowResolvedId, ...). Raw integer control ids are no longer accepted.", true)]
        public static NowInteraction Interact(int id, Rect rect, NowPointerButton button)
        {
            return Interact(NowResolvedId.FromLegacy(id), rect, button);
        }

        /// <summary>Derives a named child from an already-resolved parent.</summary>
        public static NowResolvedId GetId(NowResolvedId parent, string value)
        {
            return parent.Child(value);
        }

        /// <summary>Derives an integer child from an already-resolved parent.</summary>
        public static NowResolvedId GetId(NowResolvedId parent, int value)
        {
            return parent.Child(value);
        }

        [Obsolete("Use NowControls.GetControlId(string) or NowResolvedId.Child(string). Raw 32-bit identity hashes are no longer supported.", true)]
        public static int GetId(string value)
        {
            return GetLegacyId(value);
        }

        internal static int GetLegacyId(string value)
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

        [Obsolete("Use GetId(NowResolvedId, string). Raw 32-bit identity composition is no longer supported.", true)]
        public static int GetId(int seed, string value)
        {
            return GetLegacyId(seed, value);
        }

        internal static int GetLegacyId(int seed, string value)
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

        public static NowResolvedId GetId(NowId id, NowCallSiteId fallback)
        {
            return NowControls.GetControlId(id, fallback);
        }

        static NowResolvedId RequireExplicitId(NowId id, string paramName)
        {
            if (!id.hasValue)
                throw new ArgumentException("This API requires an explicit NowId.", paramName);

            return NowControls.ResolveNavigationTargetId(id);
        }

        public static void Reset()
        {
            NowOverlay.ClearFrameTransactions();
            _surface = default;
            _snapshot = default;
            _hasContext = false;
            _currentProvider = null;
            _activeId = NowResolvedId.None;
            _activeProvider = null;
            _activeButton = NowPointerButton.Primary;
            _dragId = NowResolvedId.None;
            _activeDragged = false;
            _activeSeenThisFrame = false;
            _activeLastSeenFrame = -1;
            _pressPosition = default;
            _dragThresholdOverride = -1f;
            _navigationKeys = NowNavigationKeys.All;
            _defaultProvider = NowScreenInputProvider.instance;
            _passiveDepth = 0;
            _scopeDepth = 0;
            _scopes.Clear();
            _scopeStartedAt = int.MinValue;
            _screenFrameActive = false;
            _scrollConsumed = false;
            _focusClaimedByPrimaryPress = false;
            _cancelClaimInputPass = int.MinValue;
            _cancelClaimFrame = int.MinValue;
            _cancelClaimProvider = null;
            _pointerPressClaims.Clear();
            NowRaycastGate.InvalidateCache();
            NowPointerArbiter.Reset();
            NowInputSystemInput.Invalidate();
            NowIMGUIInputProvider.instance.ResetState();
            NowGUI.ResetInputProviders();
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
            if (_passiveDepth <= 0)
                return;

            --_passiveDepth;

            if (_passiveDepth == 0)
                NowControls.CommitPassiveControlIdOccurrences();
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

        internal static void Restore(
            INowInputProvider provider,
            NowInputSurface surface,
            NowInputSnapshot snapshot,
            bool hasContext)
        {
            _currentProvider = provider;
            _surface = surface;
            _snapshot = snapshot;
            _hasContext = hasContext;
        }

        /// <summary>
        /// Captures the ambient input coordinates and event snapshot used by a
        /// deferred draw. Pointer claims and capture ownership stay global and
        /// are keyed by provider/input-pass, so restoring this context cannot
        /// resurrect a consumed press.
        /// </summary>
        internal static NowInputContextSnapshot CaptureContext()
        {
            return new NowInputContextSnapshot(
                _currentProvider,
                _surface,
                _snapshot,
                _hasContext);
        }

        /// <summary>Temporarily applies a previously captured input context.</summary>
        internal static NowInputContextScope ApplyContext(in NowInputContextSnapshot context)
        {
            var previous = CaptureContext();
            Restore(context.provider, context.surface, context.snapshot, context.hasContext);
            return new NowInputContextScope(previous);
        }

        internal static void EndScope(
            INowInputProvider previousProvider,
            NowInputSurface previousSurface,
            NowInputSnapshot previousSnapshot,
            bool previousHasContext,
            bool previousScrollConsumed,
            bool completeFrame,
            int token)
        {
            bool flushed = !completeFrame;

            try
            {
                if (completeFrame)
                {
                    NowOverlay.Flush();
                    flushed = true;
                }
            }
            finally
            {
                _scopes.ExitEnding(token);

                if (_scopeDepth > 0)
                    --_scopeDepth;

                if (_scopeDepth == 0)
                    _scopeStartedAt = int.MinValue;

                try
                {
                    if (completeFrame)
                        EndScopedFrame(flushed);
                }
                finally
                {
                    Restore(previousProvider, previousSurface, previousSnapshot, previousHasContext);
                    _scrollConsumed |= previousScrollConsumed;
                }
            }
        }

        internal static bool BeginScopeEnd(int token)
        {
            return _scopes.BeginEnd(token);
        }

        internal static void EndFrame()
        {
            bool completed = false;

            try
            {
                CompleteFrame();
                completed = true;
            }
            finally
            {
                _screenFrameActive = false;
                NowOverlay.EndFrameTransaction(completed);
            }
        }

        static void EndScopedFrame(bool drawCompleted)
        {
            bool completed = false;

            try
            {
                CompleteFrame();
                completed = drawCompleted;
            }
            finally
            {
                NowOverlay.EndFrameTransaction(completed);
            }
        }

        internal static void ClearActiveIf(NowResolvedId id, NowPointerButton button = NowPointerButton.Primary)
        {
            if (IsActiveControl(id, button))
            {
                if (_activeProvider is NowIMGUIInputProvider imgui)
                    imgui.CancelTrackedCapture(releaseNativeCapture: true);

                ClearActive();
            }
        }

        [Obsolete("Use ClearActiveIf(NowResolvedId). Raw integer control ids are no longer accepted.", true)]
        internal static void ClearActiveIf(int id, NowPointerButton button = NowPointerButton.Primary)
        {
            ClearActiveIf(NowResolvedId.FromLegacy(id), button);
        }

        static void CompleteFrame()
        {
            try
            {
                if (_activeId.hasValue &&
                    ReferenceEquals(_currentProvider, _activeProvider) &&
                    _hasContext &&
                    (_snapshot.pointerCaptureCancelled ||
                     (!_snapshot.IsPointerDown(_activeButton) &&
                      !_snapshot.WasPointerReleased(_activeButton)) ||
                     (!_activeSeenThisFrame &&
                      (!_snapshot.hasPointer ||
                       _snapshot.WasPointerReleased(_activeButton)))))
                {
                    ClearActive();
                }

                if (_passiveDepth > 0 ||
                    !_hasContext ||
                    !_snapshot.hasPointer ||
                    !_snapshot.primaryPressed ||
                    _focusClaimedByPrimaryPress)
                {
                    return;
                }

                if (NowFocus.ClearOnUnhandledPrimaryPress() &&
                    _currentProvider is NowIMGUIInputProvider imgui)
                {
                    imgui.NotifyFocusCleared();
                }
            }
            finally
            {
#if NOWUI_INPUT_SYSTEM
                NowKeyInput.EndInputPass();
#endif
                NowTextInput.EndInputPass();
            }
        }

        static Rect ToRect(Vector4 rect)
        {
            return new Rect(rect.x, rect.y, rect.z, rect.w);
        }

        static void ClearActive()
        {
            _activeId = NowResolvedId.None;
            _activeProvider = null;
            _activeButton = NowPointerButton.Primary;
            _dragId = NowResolvedId.None;
            _activeDragged = false;
            _activeSeenThisFrame = false;
            _activeLastSeenFrame = -1;
            _pressPosition = default;
        }

        static void ClearStaleActiveFromMissingProvider()
        {
            if (!_activeId.hasValue ||
                ReferenceEquals(_currentProvider, _activeProvider) ||
                _activeLastSeenFrame < 0)
            {
                return;
            }

            // Let another provider draw earlier in the next frame; clear only
            // after the capture owner has missed a whole input frame.
            if (_snapshot.frame > _activeLastSeenFrame + 1)
            {
                if (_activeProvider is NowIMGUIInputProvider imgui)
                    imgui.CancelTrackedCapture(releaseNativeCapture: false);

                ClearActive();
            }
        }
    }

    internal readonly struct NowInputContextSnapshot
    {
        internal readonly INowInputProvider provider;

        internal readonly NowInputSurface surface;

        internal readonly NowInputSnapshot snapshot;

        internal readonly bool hasContext;

        internal NowInputContextSnapshot(
            INowInputProvider provider,
            NowInputSurface surface,
            NowInputSnapshot snapshot,
            bool hasContext)
        {
            this.provider = provider;
            this.surface = surface;
            this.snapshot = snapshot;
            this.hasContext = hasContext;
        }
    }

    [NowScope]
    internal struct NowInputContextScope : IDisposable
    {
        readonly NowInputContextSnapshot _previous;

        bool _disposed;

        internal NowInputContextScope(NowInputContextSnapshot previous)
        {
            _previous = previous;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowInput.Restore(
                _previous.provider,
                _previous.surface,
                _previous.snapshot,
                _previous.hasContext);
        }
    }

    [NowScope]
    public struct NowInputScope : IDisposable
    {
        readonly INowInputProvider _previousProvider;

        readonly NowInputSurface _previousSurface;

        readonly NowInputSnapshot _previousSnapshot;

        readonly bool _previousHasContext;

        readonly bool _previousScrollConsumed;

        readonly bool _completeFrame;

        readonly int _token;

        bool _disposed;

        internal NowInputScope(
            INowInputProvider previousProvider,
            NowInputSurface previousSurface,
            NowInputSnapshot previousSnapshot,
            bool previousHasContext,
            bool previousScrollConsumed,
            bool completeFrame,
            int token)
        {
            _previousProvider = previousProvider;
            _previousSurface = previousSurface;
            _previousSnapshot = previousSnapshot;
            _previousHasContext = previousHasContext;
            _previousScrollConsumed = previousScrollConsumed;
            _completeFrame = completeFrame;
            _token = token;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!NowInput.BeginScopeEnd(_token))
            {
                _disposed = true;
                return;
            }

            NowInput.EndScope(
                _previousProvider,
                _previousSurface,
                _previousSnapshot,
                _previousHasContext,
                _previousScrollConsumed,
                _completeFrame,
                _token);
            _disposed = true;
        }
    }
}
