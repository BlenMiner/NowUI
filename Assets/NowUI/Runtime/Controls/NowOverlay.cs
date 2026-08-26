using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Host hook used by popup placement to fit surface-space popup rects to a
    /// visible view. Screen-space hosts use the input surface fallback; world
    /// hosts can keep popups on their UI plane while fitting the active camera.
    /// </summary>
    public interface INowPopupFitProvider
    {
        /// <summary>Returns a rect moved into view, preserving its size whenever possible.</summary>
        NowRect FitPopupRectToView(NowRect rect);

        /// <summary>
        /// Shrinks an oversized popup rect until it can fit the visible view,
        /// then moves it into view. World hosts clamp against the popup's
        /// screen-space projection, so tilted surfaces clamp correctly.
        /// </summary>
        NowRect ClampPopupRectToView(NowRect rect);
    }

    internal static class NowPopupPlacement
    {
        static readonly List<INowPopupFitProvider> _fitProviders = new List<INowPopupFitProvider>(2);

        public static NowPopupFitScope FitProvider(INowPopupFitProvider provider)
        {
            if (provider == null)
                return default;

            _fitProviders.Add(provider);
            return new NowPopupFitScope(true);
        }

        public static NowRect FitToView(NowRect rect)
        {
            if (rect.isEmpty)
                return rect;

            var transformed = Now.TransformScreenRect(rect);
            var fitted = _fitProviders.Count > 0
                ? _fitProviders[_fitProviders.Count - 1].FitPopupRectToView(transformed)
                : FitToSurface(transformed);

            Vector2 delta = fitted.position - transformed.position;

            if (delta.sqrMagnitude <= 0.0001f)
                return rect;

            return rect.Offset(Now.InverseTransformScreenVector(delta));
        }

        public static NowRect FitScreenToView(NowRect rect)
        {
            if (rect.isEmpty)
                return rect;

            return _fitProviders.Count > 0
                ? _fitProviders[_fitProviders.Count - 1].FitPopupRectToView(rect)
                : FitToSurface(rect);
        }

        /// <summary>
        /// Shrinks and moves an ambient-transform-space popup rect until it fits
        /// the visible view, mapping the screen-space clamp back through the
        /// active transform like <see cref="FitToView"/>.
        /// </summary>
        public static NowRect ClampLocalToView(NowRect rect)
        {
            if (rect.isEmpty)
                return rect;

            var transformed = Now.TransformScreenRect(rect);
            var clamped = ClampToView(transformed);

            Vector2 positionDelta = clamped.position - transformed.position;
            var sizeDelta = new Vector2(clamped.width - transformed.width, clamped.height - transformed.height);

            if (positionDelta.sqrMagnitude <= 0.0001f && sizeDelta.sqrMagnitude <= 0.0001f)
                return rect;

            Vector2 localPosition = rect.position + Now.InverseTransformScreenVector(positionDelta);
            Vector2 localSizeDelta = Now.InverseTransformScreenVector(sizeDelta);
            return new NowRect(
                localPosition.x,
                localPosition.y,
                rect.width + localSizeDelta.x,
                rect.height + localSizeDelta.y);
        }

        internal static void PopFitProvider()
        {
            if (_fitProviders.Count > 0)
                _fitProviders.RemoveAt(_fitProviders.Count - 1);
        }

        /// <summary>
        /// Shrinks an oversized popup rect until it fits the visible view, then
        /// moves it into view: the fit provider when one is active (world hosts
        /// clamp against the popup's screen projection), the input surface
        /// otherwise.
        /// </summary>
        public static NowRect ClampToView(NowRect rect)
        {
            if (rect.isEmpty)
                return rect;

            if (_fitProviders.Count > 0)
                return _fitProviders[_fitProviders.Count - 1].ClampPopupRectToView(rect);

            Vector2 size = NowInput.surface.size;

            if (size.x <= 0f || size.y <= 0f)
                return rect;

            const float margin = 8f;
            float maxHeight = Mathf.Max(32f, size.y - margin * 2f);

            if (rect.height > maxHeight)
                rect = new NowRect(rect.x, rect.y, rect.width, maxHeight);

            return FitToSurface(rect);
        }

        public static NowRect FitToSurface(NowRect rect)
        {
            Vector2 size = NowInput.surface.size;

            if (size.x <= 0f || size.y <= 0f)
                return rect;

            float x = rect.width < size.x
                ? Mathf.Clamp(rect.x, 0f, size.x - rect.width)
                : 0f;
            float y = rect.height < size.y
                ? Mathf.Clamp(rect.y, 0f, size.y - rect.height)
                : 0f;

            return new NowRect(x, y, rect.width, rect.height);
        }

        public static void Reset()
        {
            _fitProviders.Clear();
        }
    }

    [NowScope]
    public struct NowPopupFitScope : IDisposable
    {
        readonly bool _active;

        bool _disposed;

        internal NowPopupFitScope(bool active)
        {
            _active = active;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_active)
                NowPopupPlacement.PopFitProvider();
        }
    }

    /// <summary>
    /// Deferred top-layer drawing for popups, dropdowns and tooltips. Deferred
    /// callbacks run after everything else in the frame, so they draw above all
    /// regular content, and their rect blocks pointer interaction for the controls
    /// underneath. The last completed footprint protects controls declared
    /// before an overlay is rebuilt, while blocks registered in the active
    /// transaction protect controls declared later in that same pass.
    /// <code>
    /// NowOverlay.Defer(popupRect, popupId, callbackState, DrawPopup);
    /// </code>
    /// </summary>
    public static class NowOverlay
    {
        const string LegacyOverlayIdObsoleteMessage =
            "Raw integer overlay identities were removed. Use the NowResolvedId overlay-source overload.";

        public delegate void DrawCallback(int state);

        struct DeferredDraw
        {
            public Action draw;
            public DrawCallback drawWithState;
            public int state;
            public NowResolvedId overlaySourceId;
            public NowResolvedId overlayId;
            public Now.NowTransformSnapshot transform;
            public NowThemeAsset theme;
            public NowResolvedId controlIdScope;
            public NowInputContextSnapshot inputContext;
            public OverlayHostContext hostContext;
        }

        struct OverlayBlock
        {
            public NowRect rect;
            public NowResolvedId sourceId;
            public NowResolvedId id;
            public NowResolvedId parentId;
            public bool modal;
            public NowResolvedId modalInteractiveRootId;
            public object registrationOwner;
            public Component host;
            public RectTransform hostRectTransform;
            public Camera hostCamera;
        }

        struct OverlayHostContext
        {
            public Component host;
            public RectTransform rectTransform;
            public Camera camera;
        }

        struct RegistrationOwnerState
        {
            public object owner;
            public int registryVersion;
        }

        internal readonly struct Checkpoint
        {
            internal readonly int deferredCount;

            internal readonly int blockCount;

            internal Checkpoint(int deferredCount, int blockCount)
            {
                this.deferredCount = deferredCount;
                this.blockCount = blockCount;
            }
        }

        static readonly List<DeferredDraw> _deferred = new List<DeferredDraw>(4);

        static readonly List<OverlayBlock> _blocksCurrent = new List<OverlayBlock>(4);

        static readonly List<OverlayBlock> _blocksPrevious = new List<OverlayBlock>(4);

        static readonly List<NowResolvedId> _drawingStack = new List<NowResolvedId>(4);

        static readonly List<NowResolvedId> _drawingSourceStack = new List<NowResolvedId>(4);

        static readonly List<OverlayHostContext> _hostStack = new List<OverlayHostContext>(2);

        static readonly List<RegistrationOwnerState> _registrationOwners = new List<RegistrationOwnerState>(4);

        internal static event Action<object> registrationOwnerReleased;

        internal static event Action<object> registrationOwnerFootprintExpired;

        static int _registryFrame = -1;

        static int _registryVersion;

        static int _overlayDepth;

        static int _flushIndex = -1;

        static bool _resetDuringFlush;

        static int _postResetDeferredStart;

        static readonly List<Checkpoint> _frameTransactions = new List<Checkpoint>(2);

        static readonly List<object> _frameTransactionOwners = new List<object>(2);

        /// <summary>True while deferred overlay callbacks are executing.</summary>
        public static bool isDrawingOverlay => _overlayDepth > 0;

        internal static Checkpoint CaptureCheckpoint()
        {
            BeginFrameIfNeeded();
            return new Checkpoint(_deferred.Count, _blocksCurrent.Count);
        }

        internal static void Rollback(Checkpoint checkpoint)
        {
            if (_deferred.Count > checkpoint.deferredCount)
            {
                _deferred.RemoveRange(
                    checkpoint.deferredCount,
                    _deferred.Count - checkpoint.deferredCount);
            }

            if (_blocksCurrent.Count > checkpoint.blockCount)
            {
                _blocksCurrent.RemoveRange(
                    checkpoint.blockCount,
                    _blocksCurrent.Count - checkpoint.blockCount);
            }
        }

        /// <summary>
        /// Starts the overlay transaction owned by a top-level input/screen
        /// frame. A throwing Flush rolls pointer registrations back to this
        /// boundary; nested draw captures keep using their more precise local
        /// checkpoints.
        /// </summary>
        internal static void BeginFrameTransaction(INowInputProvider provider)
        {
            BeginFrameIfNeeded();
            PruneRegistrationOwners();
            object owner = RegistrationOwner(provider);
            int ownerIndex = FindRegistrationOwner(owner);

            if (ownerIndex < 0)
            {
                _registrationOwners.Add(new RegistrationOwnerState
                {
                    owner = owner,
                    registryVersion = _registryVersion
                });
            }
            else if (_registrationOwners[ownerIndex].registryVersion == _registryVersion)
            {
                PromoteRegistrationOwner(owner);
            }
            else
            {
                var state = _registrationOwners[ownerIndex];
                state.registryVersion = _registryVersion;
                _registrationOwners[ownerIndex] = state;
            }

            _frameTransactions.Add(CaptureCheckpoint());
            _frameTransactionOwners.Add(owner);
            NowContextMenu.BeginOwnerPass(owner);
        }

        internal static void EndFrameTransaction(bool completed = true)
        {
            if (_frameTransactions.Count > 0)
            {
                int last = _frameTransactions.Count - 1;
                object owner = _frameTransactionOwners[last];

                if (!completed)
                {
                    Rollback(_frameTransactions[last]);
                    int ownerIndex = FindRegistrationOwner(owner);

                    if (ownerIndex >= 0)
                    {
                        var state = _registrationOwners[ownerIndex];
                        state.registryVersion = 0;
                        _registrationOwners[ownerIndex] = state;
                    }
                }

                _frameTransactions.RemoveAt(_frameTransactions.Count - 1);
                _frameTransactionOwners.RemoveAt(_frameTransactionOwners.Count - 1);
                NowContextMenu.EndOwnerPass(owner, completed);
                PruneRegistrationOwners();
            }
        }

        internal static void ClearFrameTransactions()
        {
            NowContextMenu.AbandonOwnerPasses();
            _frameTransactions.Clear();
            _frameTransactionOwners.Clear();
        }

        internal static void ReleaseRegistrationOwner(object owner)
        {
            if (owner == null)
                return;

            NowContextMenu.ReleaseOwner(owner);

            for (int i = _blocksCurrent.Count - 1; i >= 0; --i)
            {
                if (!ReferenceEquals(_blocksCurrent[i].registrationOwner, owner))
                    continue;

                RemoveCurrentBlockAt(i);
            }

            for (int i = _blocksPrevious.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(_blocksPrevious[i].registrationOwner, owner))
                    _blocksPrevious.RemoveAt(i);
            }

            for (int i = _registrationOwners.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(_registrationOwners[i].owner, owner))
                    _registrationOwners.RemoveAt(i);
            }

            registrationOwnerReleased?.Invoke(owner);
        }

        /// <summary>
        /// Owner state exists only to replace one provider/host's prior popup
        /// footprint on a later pass. Retained runtime hosts keep their last
        /// completed footprint while they are idle; owners with no surviving
        /// footprint no longer need tracking, and destroyed runtime hosts must
        /// release their last blocks immediately instead of remaining rooted by
        /// this static registry for the rest of the session.
        /// </summary>
        static void PruneRegistrationOwners()
        {
            for (int i = _registrationOwners.Count - 1; i >= 0; --i)
            {
                object owner = _registrationOwners[i].owner;

                if (HasActiveFrameTransaction(owner))
                    continue;

                bool destroyedUnityOwner =
                    owner is UnityEngine.Object unityOwner &&
                    !unityOwner;
                bool inactiveComponentOwner =
                    owner is Component component &&
                    component &&
                    (!component.gameObject.activeInHierarchy ||
                     (component is Behaviour behaviour && !behaviour.isActiveAndEnabled));

                if (destroyedUnityOwner || inactiveComponentOwner)
                {
                    ReleaseRegistrationOwner(owner);
                    i = _registrationOwners.Count;
                    continue;
                }

                if (!HasRegistrationBlocks(owner))
                {
                    if (NowContextMenu.TracksOwner(owner))
                    {
                        int ownerVersion = _registrationOwners[i].registryVersion;
                        bool ranRecently = ownerVersion == 0 ||
                            ownerVersion == _registryVersion ||
                            ownerVersion == _registryVersion - 1;

                        if (RetainsFootprintWhileIdle(owner) || ranRecently)
                            continue;

                        NowContextMenu.ReleaseOwner(owner);
                    }

                    _registrationOwners.RemoveAt(i);
                    registrationOwnerFootprintExpired?.Invoke(owner);
                }
            }
        }

        static bool HasActiveFrameTransaction(object owner)
        {
            for (int i = 0; i < _frameTransactionOwners.Count; ++i)
            {
                if (ReferenceEquals(_frameTransactionOwners[i], owner))
                    return true;
            }

            return false;
        }

        static bool HasRegistrationBlocks(object owner)
        {
            for (int i = 0; i < _blocksCurrent.Count; ++i)
            {
                if (ReferenceEquals(_blocksCurrent[i].registrationOwner, owner))
                    return true;
            }

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (ReferenceEquals(_blocksPrevious[i].registrationOwner, owner))
                    return true;
            }

            return false;
        }

        static bool HasCurrentRegistrationBlocks(object owner)
        {
            for (int i = 0; i < _blocksCurrent.Count; ++i)
            {
                if (ReferenceEquals(_blocksCurrent[i].registrationOwner, owner))
                    return true;
            }

            return false;
        }

        static bool RegisteredInVersion(object owner, int registryVersion)
        {
            int index = FindRegistrationOwner(owner);
            return index >= 0 && _registrationOwners[index].registryVersion == registryVersion;
        }

        static bool RetainsFootprintWhileIdle(object owner)
        {
            // Retained graphics rebuild only when their content or input changes.
            // Their Component (or event-buffered built-in provider) is the
            // registration owner, so the last completed popup footprint must
            // outlive Unity frames in which no draw pass ran. Provider-owned
            // immediate surfaces keep the historical one-frame expiry, avoiding
            // an unbounded root when a caller abandons an arbitrary provider.
            if (owner is NowIMGUIInputProvider || owner is NowUIToolkitInputProvider)
                return true;

            if (owner is not Component component ||
                component == null ||
                !component.gameObject.activeInHierarchy)
            {
                return false;
            }

            return component is not Behaviour behaviour || behaviour.isActiveAndEnabled;
        }

        static void CommitFrameRegistrations(int completedRegistryVersion)
        {
            for (int i = _blocksPrevious.Count - 1; i >= 0; --i)
            {
                object owner = _blocksPrevious[i].registrationOwner;
                bool ownerRan = RegisteredInVersion(owner, completedRegistryVersion) ||
                    HasCurrentRegistrationBlocks(owner);

                if (!RetainsFootprintWhileIdle(owner) || ownerRan)
                    _blocksPrevious.RemoveAt(i);
            }

            _blocksPrevious.AddRange(_blocksCurrent);
            _blocksCurrent.Clear();
        }

        static object RegistrationOwner(INowInputProvider provider)
        {
            var host = CurrentHostContext().host;
            return host ? (object)host : provider;
        }

        /// <summary>
        /// Owner of the active declaration transaction. Nested input providers
        /// do not start their own frame transaction, so context-menu liveness
        /// remains tied to the outer owner while input and deferred rendering
        /// continue to use the nested provider.
        /// </summary>
        internal static object currentRegistrationOwner
        {
            get
            {
                if (_frameTransactionOwners.Count > 0)
                {
                    object owner = _frameTransactionOwners[_frameTransactionOwners.Count - 1];

                    if (owner != null)
                        return owner;
                }

                return RegistrationOwner(NowInput.currentProvider);
            }
        }

        static int FindRegistrationOwner(object owner)
        {
            for (int i = 0; i < _registrationOwners.Count; ++i)
            {
                if (ReferenceEquals(_registrationOwners[i].owner, owner))
                    return i;
            }

            return -1;
        }

        static void PromoteRegistrationOwner(object owner)
        {
            for (int i = _blocksPrevious.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(_blocksPrevious[i].registrationOwner, owner))
                    _blocksPrevious.RemoveAt(i);
            }

            for (int i = 0; i < _blocksCurrent.Count;)
            {
                var block = _blocksCurrent[i];

                if (!ReferenceEquals(block.registrationOwner, owner))
                {
                    ++i;
                    continue;
                }

                _blocksPrevious.Add(block);
                RemoveCurrentBlockAt(i);
            }
        }

        static void RemoveCurrentBlockAt(int index)
        {
            _blocksCurrent.RemoveAt(index);

            for (int i = 0; i < _frameTransactions.Count; ++i)
            {
                var checkpoint = _frameTransactions[i];

                if (index < checkpoint.blockCount)
                {
                    _frameTransactions[i] = new Checkpoint(
                        checkpoint.deferredCount,
                        checkpoint.blockCount - 1);
                }
            }
        }

        static void RollbackFrameTransaction()
        {
            if (_frameTransactions.Count == 0)
                return;

            NowContextMenu.MarkOwnerPassesFailed();

            // Flush owns the global deferred queue and abandons all of it when
            // a callback throws. Roll pointer blocks back to the oldest owner
            // whose callbacks are being discarded as well. Using only the
            // newest nested-input checkpoint could otherwise preserve a block
            // registered before that nested scope while clearing its callback.
            //
            // Owners still end their own transactions in their finally paths.
            // Keeping the stack intact here prevents nested cleanup from
            // accidentally popping the outer screen transaction.
            Rollback(_frameTransactions[0]);

            // A failed pass never produced an authoritative current
            // registration. Keep each active owner's previous successful
            // footprint alive on its next pass instead of promoting (and
            // clearing) it as though this pass had completed.
            for (int i = 0; i < _frameTransactionOwners.Count; ++i)
            {
                int ownerIndex = FindRegistrationOwner(_frameTransactionOwners[i]);

                if (ownerIndex < 0)
                    continue;

                var state = _registrationOwners[ownerIndex];
                state.registryVersion = 0;
                _registrationOwners[ownerIndex] = state;
            }
        }

        /// <summary>
        /// True while any overlay is queued or has a current/last-completed
        /// pointer footprint, including an idle retained host's footprint.
        /// </summary>
        public static bool hasOpenOverlay
        {
            get
            {
                BeginFrameIfNeeded();
                return _deferred.Count > 0 || _blocksCurrent.Count > 0 || _blocksPrevious.Count > 0;
            }
        }

        internal static NowResolvedId currentFocusLayerId => CurrentOverlayId();

        internal static NowResolvedId currentFocusLayerSourceId => CurrentOverlaySourceId();

        internal static NowResolvedId activeFocusLayerId => ActiveFocusLayerBlock().id;

        internal static NowResolvedId activeFocusLayerSourceId => ActiveFocusLayerBlock().sourceId;

        static OverlayBlock ActiveFocusLayerBlock()
        {
            BeginFrameIfNeeded();
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            OverlayBlock current = FindTopOverlayBlock(_blocksCurrent, host, owner);
            OverlayBlock previous = FindTopOverlayBlock(_blocksPrevious, host, owner);

            if (current.id.hasValue && previous.id.hasValue && current.id != previous.id &&
                OverlayIdBelongsToTree(previous.id, current.id, _blocksPrevious, owner))
            {
                return previous;
            }

            if (current.id.hasValue)
                return current;

            return previous;
        }

        internal static NowOverlayHostScope Host(Component host, RectTransform rectTransform, Camera camera)
        {
            if (host == null || rectTransform == null)
                return default;

            _hostStack.Add(new OverlayHostContext
            {
                host = host,
                rectTransform = rectTransform,
                camera = camera
            });

            return new NowOverlayHostScope(true);
        }

        /// <summary>
        /// Host identity without a RectTransform, for surfaces with their own
        /// coordinate space (world graphics). Blocks tagged this way only affect
        /// their own surface's pointer, never other hosts' local coordinates.
        /// </summary>
        internal static NowOverlayHostScope Host(Component host)
        {
            if (host == null)
                return default;

            _hostStack.Add(new OverlayHostContext
            {
                host = host,
                rectTransform = null,
                camera = null
            });

            return new NowOverlayHostScope(true);
        }

        // Deferred callbacks may outlive a nested host scope. Push even the
        // empty context so an outer host cannot accidentally become the owner
        // of overlays queued by a hostless nested input surface.
        static NowOverlayHostScope ApplyHostContext(OverlayHostContext context)
        {
            _hostStack.Add(context);
            return new NowOverlayHostScope(true);
        }

        internal static void PopHost()
        {
            if (_hostStack.Count > 0)
                _hostStack.RemoveAt(_hostStack.Count - 1);
        }

        /// <summary>
        /// Moves an authored popup rect just enough to fit the active visible area.
        /// Screen-space hosts fit to the current input surface; world-space hosts
        /// can provide a camera/FOV-aware fit while keeping the rect on the same
        /// UI plane.
        /// </summary>
        public static NowRect FitToView(NowRect rect)
        {
            return NowPopupPlacement.FitToView(rect);
        }

        /// <summary>
        /// Moves a popup rect that is already in surface coordinates to fit the
        /// active visible area.
        /// </summary>
        public static NowRect FitScreenToView(NowRect rect)
        {
            return NowPopupPlacement.FitScreenToView(rect);
        }

        /// <summary>
        /// Shrinks an oversized popup rect until it fits the visible view, then
        /// moves it into view.
        /// </summary>
        public static NowRect ClampScreenToView(NowRect rect)
        {
            return NowPopupPlacement.ClampToView(rect);
        }

        /// <summary>
        /// Shrinks and moves an ambient-transform-space popup rect until it
        /// fits the visible view — the clamping counterpart of
        /// <see cref="FitToView"/> for dropdown-family popups.
        /// </summary>
        public static NowRect ClampToView(NowRect rect)
        {
            return NowPopupPlacement.ClampLocalToView(rect);
        }

        static NowResolvedId ResolveOverlaySourceId(NowResolvedId overlaySourceId)
        {
            if (!overlaySourceId.hasValue)
                throw new ArgumentException(
                    "A named overlay requires a non-empty resolved source id.",
                    nameof(overlaySourceId));

            return overlaySourceId.InDomain(NowIdDomain.Overlay);
        }

        /// <summary>
        /// Queues a draw callback for the end of the frame and blocks pointer
        /// interaction inside <paramref name="blockRect"/> for everything that is
        /// not itself overlay content. Ignored during layout measure passes.
        /// </summary>
        public static void Defer(NowRect blockRect, Action draw)
        {
            DeferResolved(
                blockRect,
                NowResolvedId.None,
                NowResolvedId.None,
                draw);
        }

        /// <summary>
        /// Queues a named overlay. <paramref name="overlaySourceId"/> is the
        /// resolved control/path identity that owns the overlay; the Overlay
        /// domain boundary is applied exactly once by this API.
        /// </summary>
        public static void Defer(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            Action draw)
        {
            DeferResolved(
                blockRect,
                overlaySourceId,
                ResolveOverlaySourceId(overlaySourceId),
                draw);
        }

        static void DeferResolved(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            NowResolvedId overlayId,
            Action draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                draw = draw,
                overlaySourceId = overlaySourceId,
                overlayId = overlayId,
                transform = Now.CaptureTransform(),
                theme = NowTheme.currentScopeTheme,
                controlIdScope = NowControls.CaptureIdScope(),
                inputContext = NowInput.CaptureContext(),
                hostContext = CurrentHostContext()
            });
            AddBlock(Now.TransformScreenRect(blockRect), overlaySourceId, overlayId);
        }

        /// <summary>
        /// Queues an overlay whose geometry is already in screen space.
        /// </summary>
        public static void DeferScreen(NowRect blockRect, Action draw)
        {
            DeferScreenResolved(
                blockRect,
                NowResolvedId.None,
                NowResolvedId.None,
                draw);
        }

        /// <summary>
        /// Queues a named overlay whose geometry is already in screen space.
        /// </summary>
        public static void DeferScreen(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            Action draw)
        {
            DeferScreenResolved(
                blockRect,
                overlaySourceId,
                ResolveOverlaySourceId(overlaySourceId),
                draw);
        }

        static void DeferScreenResolved(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            NowResolvedId overlayId,
            Action draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                draw = draw,
                overlaySourceId = overlaySourceId,
                overlayId = overlayId,
                theme = NowTheme.currentScopeTheme,
                controlIdScope = NowControls.CaptureIdScope(),
                inputContext = NowInput.CaptureContext(),
                hostContext = CurrentHostContext()
            });
            AddBlock(blockRect, overlaySourceId, overlayId);
        }

        /// <summary>
        /// Queues a non-capturing draw callback. Store per-overlay state under
        /// <paramref name="state"/> and pass a static method to avoid closure
        /// allocation on warmed popup paths.
        /// </summary>
        public static void Defer(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            int state,
            DrawCallback draw)
        {
            DeferResolved(
                blockRect,
                overlaySourceId,
                ResolveOverlaySourceId(overlaySourceId),
                state,
                draw);
        }

        /// <summary>
        /// Queues an anonymous non-capturing overlay. The integer is callback
        /// payload only; use the four-argument overload when the overlay needs
        /// a stable source identity.
        /// </summary>
        public static void Defer(NowRect blockRect, int state, DrawCallback draw)
        {
            DeferResolved(
                blockRect,
                NowResolvedId.None,
                NowResolvedId.None,
                state,
                draw);
        }

        static void DeferResolved(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            NowResolvedId overlayId,
            int state,
            DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                drawWithState = draw,
                state = state,
                overlaySourceId = overlaySourceId,
                overlayId = overlayId,
                transform = Now.CaptureTransform(),
                theme = NowTheme.currentScopeTheme,
                controlIdScope = NowControls.CaptureIdScope(),
                inputContext = NowInput.CaptureContext(),
                hostContext = CurrentHostContext()
            });
            AddBlock(Now.TransformScreenRect(blockRect), overlaySourceId, overlayId);
        }

        /// <summary>
        /// Queues a non-capturing screen-space overlay callback.
        /// </summary>
        public static void DeferScreen(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            int state,
            DrawCallback draw)
        {
            DeferScreenResolved(
                blockRect,
                overlaySourceId,
                ResolveOverlaySourceId(overlaySourceId),
                state,
                draw);
        }

        /// <summary>
        /// Queues an anonymous non-capturing screen-space overlay. The integer
        /// is callback payload only; use the four-argument overload for a named
        /// overlay.
        /// </summary>
        public static void DeferScreen(NowRect blockRect, int state, DrawCallback draw)
        {
            DeferScreenResolved(
                blockRect,
                NowResolvedId.None,
                NowResolvedId.None,
                state,
                draw);
        }

        static void DeferScreenResolved(
            NowRect blockRect,
            NowResolvedId overlaySourceId,
            NowResolvedId overlayId,
            int state,
            DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                drawWithState = draw,
                state = state,
                overlaySourceId = overlaySourceId,
                overlayId = overlayId,
                theme = NowTheme.currentScopeTheme,
                controlIdScope = NowControls.CaptureIdScope(),
                inputContext = NowInput.CaptureContext(),
                hostContext = CurrentHostContext()
            });
            AddBlock(blockRect, overlaySourceId, overlayId);
        }

        /// <summary>
        /// Queues an overlay that draws above everything but never blocks the
        /// pointer — tooltips and other purely informational layers that must not
        /// steal hover or clicks from the controls beneath them.
        /// </summary>
        public static void DeferPassive(
            NowResolvedId overlaySourceId,
            int state,
            DrawCallback draw)
        {
            DeferPassiveResolved(
                overlaySourceId,
                ResolveOverlaySourceId(overlaySourceId),
                state,
                draw);
        }

        /// <summary>
        /// Queues an anonymous passive callback. The integer is callback
        /// payload only; use the three-argument typed overload for a named
        /// passive overlay.
        /// </summary>
        public static void DeferPassive(int state, DrawCallback draw)
        {
            DeferPassiveResolved(
                NowResolvedId.None,
                NowResolvedId.None,
                state,
                draw);
        }

        static void DeferPassiveResolved(
            NowResolvedId overlaySourceId,
            NowResolvedId overlayId,
            int state,
            DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                drawWithState = draw,
                state = state,
                overlaySourceId = overlaySourceId,
                overlayId = overlayId,
                transform = Now.CaptureTransform(),
                theme = NowTheme.currentScopeTheme,
                controlIdScope = NowControls.CaptureIdScope(),
                inputContext = NowInput.CaptureContext(),
                hostContext = CurrentHostContext()
            });
        }

        /// <summary>
        /// Blocks pointer interaction inside the rect without deferring a draw —
        /// for overlays that manage their own draw order (modal scrims).
        /// </summary>
        public static void Block(NowRect blockRect)
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            AddBlock(
                Now.TransformScreenRect(blockRect),
                NowResolvedId.None,
                NowResolvedId.None);
        }

        /// <summary>
        /// Blocks pointer interaction inside a screen-space rect.
        /// </summary>
        public static void BlockScreen(NowRect blockRect)
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            AddBlock(blockRect, NowResolvedId.None, NowResolvedId.None);
        }

        /// <summary>
        /// Blocks pointer interaction on every NowUI surface, not just the
        /// registering host's — the modal guarantee for context menus and modal
        /// dialogs. Base content is blocked everywhere; other overlay content is
        /// blocked too, except the overlay subtree rooted at
        /// modal's interactive root (its own popup subtree), so a
        /// context menu opened from inside another popup wins the pointer over
        /// the popup beneath it.
        /// </summary>
        public static void BlockAllSurfaces()
        {
            BlockAllSurfacesResolved(NowResolvedId.None);
        }

        /// <summary>
        /// Blocks every surface while leaving the overlay tree rooted at
        /// <paramref name="interactiveRootSourceId"/> interactive.
        /// </summary>
        public static void BlockAllSurfaces(NowResolvedId interactiveRootSourceId)
        {
            BlockAllSurfacesResolved(ResolveOverlaySourceId(interactiveRootSourceId));
        }

        [Obsolete(LegacyOverlayIdObsoleteMessage, true)]
        public static void BlockAllSurfaces(int interactiveRootId = 0)
        {
            BlockAllSurfaces(NowResolvedId.FromLegacy(interactiveRootId));
        }

        static void BlockAllSurfacesResolved(NowResolvedId interactiveRootId)
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();

            var host = CurrentHostContext();

            _blocksCurrent.Add(new OverlayBlock
            {
                rect = new NowRect(-100000f, -100000f, 200000f, 200000f),
                sourceId = NowResolvedId.None,
                id = NowResolvedId.None,
                parentId = CurrentOverlayId(),
                modal = true,
                modalInteractiveRootId = interactiveRootId,
                registrationOwner = RegistrationOwner(NowInput.currentProvider),
                host = host.host,
                hostRectTransform = host.rectTransform,
                hostCamera = host.camera
            });
        }

        /// <summary>
        /// True when the pointer position is owned by the last completed overlay
        /// footprint or by an overlay registered earlier in the active input
        /// transaction. Base-layer interactions treat it as hover-blocked. Runtime
        /// retained hosts preserve their completed footprint through idle frames
        /// and replace it on their next draw pass. Arbitrary provider-owned
        /// immediate surfaces retain the historical frame-based expiry.
        /// </summary>
        public static bool IsPointerBlocked(Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return IsOverlayContentBlocked();

            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (ModalBlocksDomain(_blocksPrevious[i], host, owner))
                    return true;

                if (BlockBelongsToDomain(_blocksPrevious[i], host, owner) &&
                    _blocksPrevious[i].rect.Contains(pointerPosition))
                {
                    return true;
                }
            }

            if (CurrentModalBlocksDomain(host, owner))
                return true;

            int start = CurrentTransactionBlockStart();

            for (int i = start; i < _blocksCurrent.Count; ++i)
            {
                if (BlockBelongsToDomain(_blocksCurrent[i], host, owner) &&
                    _blocksCurrent[i].rect.Contains(pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        static int CurrentTransactionBlockStart()
        {
            return _frameTransactions.Count > 0
                ? Mathf.Clamp(
                    _frameTransactions[_frameTransactions.Count - 1].blockCount,
                    0,
                    _blocksCurrent.Count)
                : _blocksCurrent.Count;
        }

        static bool CurrentModalBlocksDomain(Component host, object owner)
        {
            for (int i = 0; i < _blocksCurrent.Count; ++i)
            {
                if (ModalBlocksDomain(_blocksCurrent[i], host, owner))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Scroll-specific ownership also honors overlays declared earlier in
        /// the active input transaction. Deferred popup content runs after an
        /// enclosing scroll scope, so waiting for the next Unity frame would let
        /// the parent consume the popup's wheel event first in editor IMGUI.
        /// Stale registrations from earlier same-frame passes are excluded by
        /// the transaction checkpoint.
        /// </summary>
        internal static bool IsPointerBlockedForScroll(Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return IsOverlayContentBlocked();

            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (CurrentModalBlocksDomain(host, owner))
                return true;

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (BlocksBasePointer(_blocksPrevious[i], host, owner, pointerPosition))
                    return true;
            }

            if (_frameTransactions.Count == 0)
                return false;

            int start = CurrentTransactionBlockStart();

            for (int i = start; i < _blocksCurrent.Count; ++i)
            {
                if (BlocksBasePointer(_blocksCurrent[i], host, owner, pointerPosition))
                    return true;
            }

            return false;
        }

        static bool BlocksBasePointer(
            OverlayBlock block,
            Component host,
            object owner,
            Vector2 pointerPosition)
        {
            return ModalBlocksDomain(block, host, owner) ||
                (BlockBelongsToDomain(block, host, owner) && block.rect.Contains(pointerPosition));
        }

        static bool OwnsRemainingScroll(Vector2 pointerPosition)
        {
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (CurrentModalBlocksDomain(host, owner))
                return true;

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (BlocksBasePointer(_blocksPrevious[i], host, owner, pointerPosition))
                    return true;
            }

            if (_frameTransactions.Count == 0)
                return false;

            int start = CurrentTransactionBlockStart();

            for (int i = start; i < _blocksCurrent.Count; ++i)
            {
                if (BlocksBasePointer(_blocksCurrent[i], host, owner, pointerPosition))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Overlay content is normally exempt from pointer blocks (it sits on
        /// top), but a modal registered by a nested popup — a context menu
        /// opened from inside another popup — must still occlude the overlay
        /// layers beneath it. Only the modal's own overlay subtree stays
        /// interactive.
        /// </summary>
        static bool IsOverlayContentBlocked()
        {
            NowResolvedId drawing = CurrentOverlayId();
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (IsOverlayContentBlocked(
                _blocksPrevious, 0, drawing, host, owner))
            {
                return true;
            }

            return IsOverlayContentBlocked(
                _blocksCurrent,
                0,
                drawing,
                host,
                owner);
        }

        static bool IsOverlayContentBlocked(
            List<OverlayBlock> blocks,
            int start,
            NowResolvedId drawing,
            Component host,
            object owner)
        {
            start = Mathf.Clamp(start, 0, blocks.Count);

            for (int i = start; i < blocks.Count; ++i)
            {
                var block = blocks[i];

                if (!ModalBlocksDomain(block, host, owner))
                    continue;

                if (block.modalInteractiveRootId.hasValue &&
                    drawing.hasValue &&
                    (drawing == block.modalInteractiveRootId ||
                     OverlayIdBelongsToTree(
                         drawing,
                         block.modalInteractiveRootId,
                         _blocksPrevious,
                         owner) ||
                     OverlayIdBelongsToTree(
                         drawing,
                         block.modalInteractiveRootId,
                         _blocksCurrent,
                         owner)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="pointerPosition"/> is inside the overlay
        /// registered for <paramref name="rootSourceId"/> or any nested overlay deferred
        /// while that root was drawing. Use this for popup outside-click checks.
        /// </summary>
        public static bool IsPointerInsideOverlayTree(
            NowResolvedId rootSourceId,
            Vector2 pointerPosition)
        {
            return IsPointerInsideOverlayTreeResolved(
                ResolveOverlaySourceId(rootSourceId),
                pointerPosition);
        }

        [Obsolete(LegacyOverlayIdObsoleteMessage, true)]
        public static bool IsPointerInsideOverlayTree(int rootId, Vector2 pointerPosition)
        {
            return IsPointerInsideOverlayTree(
                NowResolvedId.FromLegacy(rootId),
                pointerPosition);
        }

        internal static bool IsPointerInsideOverlayTreeResolved(
            NowResolvedId rootId,
            Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (_overlayDepth > 0)
                return IsPointerInsideOverlayTreeInBlocks(
                    rootId,
                    pointerPosition,
                    _blocksCurrent,
                    host,
                    owner);

            return IsPointerInsideOverlayTreeInBlocks(
                    rootId,
                    pointerPosition,
                    _blocksCurrent,
                    host,
                    owner) ||
                IsPointerInsideOverlayTreeInBlocks(
                    rootId,
                    pointerPosition,
                    _blocksPrevious,
                    host,
                    owner);
        }

        /// <summary>
        /// True when the pointer is inside any concrete overlay popup. Modal
        /// screen-wide blocks use the empty identity and are intentionally ignored.
        /// </summary>
        internal static bool IsPointerInsideOverlay(Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (_overlayDepth > 0)
                return IsPointerInsideOverlay(pointerPosition, _blocksCurrent, host, owner);

            return IsPointerInsideOverlay(pointerPosition, _blocksCurrent, host, owner) ||
                IsPointerInsideOverlay(pointerPosition, _blocksPrevious, host, owner);
        }

        internal static bool IsPointerInsideOverlay(Component host, Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return IsPointerInsideOverlay(host, pointerPosition, _blocksCurrent);

            return IsPointerInsideOverlay(host, pointerPosition, _blocksCurrent) ||
                IsPointerInsideOverlay(host, pointerPosition, _blocksPrevious);
        }

        internal static bool IsPointerBlockedByForeignOverlay(Component host, Vector2 screenPosition)
        {
            if (host == null || _overlayDepth > 0)
                return false;

            BeginFrameIfNeeded();

            return IsPointerBlockedByForeignOverlay(host, screenPosition, _blocksCurrent) ||
                IsPointerBlockedByForeignOverlay(host, screenPosition, _blocksPrevious);
        }

        /// <summary>
        /// True when an overlay was deferred while <paramref name="rootSourceId"/> or
        /// one of its descendants was drawing. Use this to let cancel close the
        /// topmost nested popup before its parents.
        /// </summary>
        public static bool HasNestedOverlay(NowResolvedId rootSourceId)
        {
            return HasNestedOverlayResolved(ResolveOverlaySourceId(rootSourceId));
        }

        [Obsolete(LegacyOverlayIdObsoleteMessage, true)]
        public static bool HasNestedOverlay(int rootId)
        {
            return HasNestedOverlay(NowResolvedId.FromLegacy(rootId));
        }

        internal static bool HasNestedOverlayResolved(NowResolvedId rootId)
        {
            BeginFrameIfNeeded();
            var host = CurrentHostContext().host;
            object owner = RegistrationOwner(NowInput.currentProvider);

            if (_overlayDepth > 0)
                return HasNestedOverlayInBlocks(rootId, _blocksCurrent, host, owner);

            return HasNestedOverlayInBlocks(rootId, _blocksCurrent, host, owner) ||
                HasNestedOverlayInBlocks(rootId, _blocksPrevious, host, owner);
        }

        static bool HasNestedOverlayInBlocks(
            NowResolvedId rootId,
            List<OverlayBlock> blocks,
            Component host,
            object owner)
        {
            if (!rootId.hasValue)
                return false;

            for (int i = 0; i < blocks.Count; ++i)
            {
                if (!blocks[i].id.hasValue || blocks[i].id == rootId)
                    continue;

                if (!BlockBelongsToDomain(blocks[i], host, owner))
                    continue;

                if (BlockBelongsToTree(blocks[i], rootId, blocks))
                    return true;
            }

            return false;
        }

        static bool IsPointerInsideOverlayTreeInBlocks(
            NowResolvedId rootId,
            Vector2 pointerPosition,
            List<OverlayBlock> blocks,
            Component host,
            object owner)
        {
            if (!rootId.hasValue)
                return false;

            for (int i = 0; i < blocks.Count; ++i)
            {
                if (!BlockBelongsToDomain(blocks[i], host, owner) ||
                    !blocks[i].rect.Contains(pointerPosition))
                {
                    continue;
                }

                if (BlockBelongsToTree(blocks[i], rootId, blocks))
                    return true;
            }

            return false;
        }

        static bool IsPointerInsideOverlay(
            Vector2 pointerPosition,
            List<OverlayBlock> blocks,
            Component host,
            object owner)
        {
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].id.hasValue &&
                    BlockBelongsToDomain(blocks[i], host, owner) &&
                    blocks[i].rect.Contains(pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsPointerInsideOverlay(Component host, Vector2 pointerPosition, List<OverlayBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].id.hasValue &&
                    blocks[i].host == host &&
                    blocks[i].rect.Contains(pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsPointerBlockedByForeignOverlay(Component host, Vector2 screenPosition, List<OverlayBlock> blocks)
        {
            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                var block = blocks[i];

                if (block.host == null || block.host == host)
                    continue;

                if (!BlockContainsScreenPoint(block, screenPosition))
                    continue;

                if (NowRaycastGate.IsHostAbove(block.host, host, screenPosition))
                    return true;
            }

            return false;
        }

        static bool BlockBelongsToDomain(
            OverlayBlock block,
            Component host,
            object registrationOwner)
        {
            if (host != null)
                return block.host == host;

            return block.host == null &&
                ReferenceEquals(block.registrationOwner, registrationOwner);
        }

        static bool ModalBlocksDomain(
            OverlayBlock block,
            Component host,
            object registrationOwner)
        {
            if (!block.modal)
                return false;

            // IMGUI control ids and coordinates are local to one native GUI
            // context. A modal popup in one EditorWindow must not block a
            // different window that happens to use the same local coordinates.
            if (block.registrationOwner is NowIMGUIInputProvider ||
                registrationOwner is NowIMGUIInputProvider)
            {
                return ReferenceEquals(block.registrationOwner, registrationOwner);
            }

            // Runtime modal overlays retain their documented all-surface
            // behavior across screen, canvas, and world hosts.
            return true;
        }

        static bool BlockContainsScreenPoint(OverlayBlock block, Vector2 screenPosition)
        {
            if (block.hostRectTransform == null)
                return false;

            if (!NowRectTransformProjection.ScreenPointToLocalPointInRectangle(
                    block.hostRectTransform,
                    screenPosition,
                    block.hostCamera,
                    out var localPosition))
            {
                return false;
            }

            Rect rect = block.hostRectTransform.rect;
            var position = new Vector2(localPosition.x - rect.xMin, rect.yMax - localPosition.y);
            return block.rect.Contains(position);
        }

        static void AddBlock(
            NowRect rect,
            NowResolvedId sourceId,
            NowResolvedId id)
        {
            var host = CurrentHostContext();

            _blocksCurrent.Add(new OverlayBlock
            {
                rect = rect,
                sourceId = sourceId,
                id = id,
                parentId = CurrentOverlayId(),
                registrationOwner = RegistrationOwner(NowInput.currentProvider),
                host = host.host,
                hostRectTransform = host.rectTransform,
                hostCamera = host.camera
            });
        }

        static OverlayHostContext CurrentHostContext()
        {
            return _hostStack.Count > 0 ? _hostStack[_hostStack.Count - 1] : default;
        }

        static bool BlockBelongsToTree(
            OverlayBlock block,
            NowResolvedId rootId,
            List<OverlayBlock> blocks)
        {
            if (block.id == rootId)
                return true;

            NowResolvedId parentId = block.parentId;
            object owner = block.registrationOwner;

            for (int guard = 0; guard < blocks.Count && parentId.hasValue; ++guard)
            {
                if (parentId == rootId)
                    return true;

                parentId = FindParentId(parentId, blocks, owner);
            }

            return false;
        }

        static bool OverlayIdBelongsToTree(
            NowResolvedId id,
            NowResolvedId rootId,
            List<OverlayBlock> blocks,
            object owner)
        {
            if (!id.hasValue || !rootId.hasValue)
                return false;

            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id == id &&
                    ReferenceEquals(blocks[i].registrationOwner, owner))
                {
                    return BlockBelongsToTree(blocks[i], rootId, blocks);
                }
            }

            return false;
        }

        static NowResolvedId FindParentId(
            NowResolvedId id,
            List<OverlayBlock> blocks,
            object owner)
        {
            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id == id &&
                    ReferenceEquals(blocks[i].registrationOwner, owner))
                {
                    return blocks[i].parentId;
                }
            }

            return NowResolvedId.None;
        }

        static NowResolvedId CurrentOverlayId()
        {
            for (int i = _drawingStack.Count - 1; i >= 0; --i)
            {
                if (_drawingStack[i].hasValue)
                    return _drawingStack[i];
            }

            return NowResolvedId.None;
        }

        static NowResolvedId CurrentOverlaySourceId()
        {
            for (int i = _drawingSourceStack.Count - 1; i >= 0; --i)
            {
                if (_drawingSourceStack[i].hasValue)
                    return _drawingSourceStack[i];
            }

            return NowResolvedId.None;
        }

        static OverlayBlock FindTopOverlayBlock(
            List<OverlayBlock> blocks,
            Component host,
            object owner)
        {
            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id.hasValue &&
                    BlockBelongsToDomain(blocks[i], host, owner))
                {
                    return blocks[i];
                }
            }

            return default;
        }

        static void BeginFrameIfNeeded()
        {
            int frame = Time.frameCount;

            if (_registryFrame == frame)
                return;

            _registryFrame = frame;
            int completedRegistryVersion = _registryVersion;
            unchecked
            {
                ++_registryVersion;

                if (_registryVersion == 0)
                    _registryVersion = 1;
            }

            CommitFrameRegistrations(completedRegistryVersion);
            PruneRegistrationOwners();
        }

        /// <summary>
        /// Forces the frame swap; used by tests where frameCount is static. The
        /// frame is left marked current so queries (which also roll the frame)
        /// do not swap again until the next forced or real frame.
        /// </summary>
        internal static void ForceNewFrame()
        {
            _registryFrame = -1;
            BeginFrameIfNeeded();
        }

        /// <summary>
        /// Runs the deferred callbacks. Called when a <see cref="Now.StartUI()"/>
        /// scope is disposed and at
        /// the end of UGUI mesh capture; safe to call when nothing is queued.
        /// </summary>
        internal static void Flush()
        {
            // A deferred callback may close a nested draw capture. That capture
            // also flushes on dispose, but the outer loop still owns the queue
            // (and already observes callbacks appended while it runs).
            if (_overlayDepth > 0)
                return;

            // A manually drawn scrim can register only Block/BlockScreen or
            // BlockAllSurfaces. It still owns wheel input over that block, and
            // IMGUI needs the native ScrollWheel event consumed even though
            // there is no deferred callback to flush.
            if (_deferred.Count == 0)
            {
                ConsumeRemainingOwnedScroll();
                return;
            }

            using var profile = NowProfiler.OverlayFlush.Auto();
            Now.MarkOverlayBatchStart();
            ++_overlayDepth;

            const int MaxFlushedOverlays = 1024;

            try
            {
                // Callbacks may defer more overlays (nested menus); those run within
                // the same flush, drawn after their parents.
                for (_flushIndex = 0; _flushIndex < _deferred.Count; ++_flushIndex)
                {
                    if (_flushIndex >= MaxFlushedOverlays)
                    {
                        var last = _deferred[_flushIndex];
                        var callback = last.drawWithState?.Method ?? last.draw?.Method;
                        Debug.LogError(
                            $"NowOverlay.Flush aborted after {MaxFlushedOverlays} overlays in one frame — an overlay " +
                            $"is re-deferring itself every pass. Last overlay id {last.overlayId}, callback " +
                            $"{callback?.DeclaringType?.Name}.{callback?.Name}.");
                        RollbackFrameTransaction();
                        break;
                    }

                    var deferred = _deferred[_flushIndex];
                    _drawingSourceStack.Add(deferred.overlaySourceId);
                    _drawingStack.Add(deferred.overlayId);

                    try
                    {
                        using (ApplyHostContext(deferred.hostContext))
                        using (NowInput.ApplyContext(deferred.inputContext))
                        using (NowControls.RestoreIdScope(deferred.controlIdScope))
                        using (Now.ApplyTransformSnapshot(deferred.transform))
                        using (NowTheme.ScopeOrDefault(deferred.theme))
                        {
                            if (deferred.drawWithState != null)
                                deferred.drawWithState(deferred.state);
                            else
                                deferred.draw?.Invoke();
                        }
                    }
                    finally
                    {
                        _drawingStack.RemoveAt(_drawingStack.Count - 1);
                        _drawingSourceStack.RemoveAt(_drawingSourceStack.Count - 1);
                    }

                    if (_resetDuringFlush)
                        break;
                }

                ConsumeRemainingOwnedScroll();
            }
            catch
            {
                // Deferred callbacks are user code. If one aborts a top-level
                // screen/input frame, none of that frame's invisible pointer
                // registrations may survive after its callbacks are abandoned.
                RollbackFrameTransaction();
                throw;
            }
            finally
            {
                if (_resetDuringFlush)
                {
                    int prefix = Mathf.Clamp(_postResetDeferredStart, 0, _deferred.Count);

                    if (prefix > 0)
                        _deferred.RemoveRange(0, prefix);
                }
                else
                {
                    _deferred.Clear();
                }

                _drawingStack.Clear();
                _drawingSourceStack.Clear();
                --_overlayDepth;
                _flushIndex = -1;
                _resetDuringFlush = false;
                _postResetDeferredStart = 0;
            }
        }

        static void ConsumeRemainingOwnedScroll()
        {
            var snapshot = NowInput.current;

            if (snapshot.hasPointer &&
                snapshot.scrollDelta != Vector2.zero &&
                OwnsRemainingScroll(snapshot.pointerPosition))
            {
                NowInput.ConsumeRemainingOverlayScroll();
            }
        }

        /// <summary>
        /// Drops callbacks and pointer blocks owned by a screen frame that crossed
        /// a frame boundary without being disposed. Host and popup-fit scopes are
        /// left alone because their lifetime belongs to the hosting component.
        /// </summary>
        internal static void DiscardAbandonedFrame()
        {
            ClearFrameTransactions();
            _deferred.Clear();
            _blocksCurrent.Clear();
            _blocksPrevious.Clear();
            _drawingStack.Clear();
            _drawingSourceStack.Clear();
            ExpireAllRegistrationOwnerFootprints();
            _registryFrame = -1;
            _registryVersion = 0;
            _overlayDepth = 0;
        }

        public static void Reset()
        {
            if (_overlayDepth > 0)
            {
                // The active callback and Flush finally-block still own the
                // drawing stack/depth. Remove all queued work through the
                // current callback, then preserve anything deferred after this
                // reset for a fresh, non-reentrant flush.
                int prefix = Mathf.Clamp(_flushIndex + 1, 0, _deferred.Count);

                if (_deferred.Count > prefix)
                    _deferred.RemoveRange(prefix, _deferred.Count - prefix);

                _resetDuringFlush = true;
                _postResetDeferredStart = prefix;
                _blocksCurrent.Clear();
                _blocksPrevious.Clear();
                ExpireAllRegistrationOwnerFootprints();
                _registryFrame = -1;
                _registryVersion = 0;
                NowContextMenu.AbandonOwnerPasses();

                for (int i = 0; i < _frameTransactions.Count; ++i)
                {
                    _frameTransactions[i] = default;
                    _frameTransactionOwners[i] = null;
                }

                // Host and popup-fit stacks are ambient scopes owned by the
                // retained host that is currently flushing. Keep them balanced
                // so work deferred after Reset still belongs to that host and
                // uses its view-fitting policy.
                return;
            }

            _deferred.Clear();
            _blocksCurrent.Clear();
            _blocksPrevious.Clear();
            _drawingStack.Clear();
            _drawingSourceStack.Clear();
            _hostStack.Clear();
            ExpireAllRegistrationOwnerFootprints();
            _registryFrame = -1;
            _registryVersion = 0;
            _overlayDepth = 0;
            _flushIndex = -1;
            _resetDuringFlush = false;
            _postResetDeferredStart = 0;
            ClearFrameTransactions();
            NowPopupPlacement.Reset();
        }

        static void ExpireAllRegistrationOwnerFootprints()
        {
            while (_registrationOwners.Count > 0)
            {
                int last = _registrationOwners.Count - 1;
                object owner = _registrationOwners[last].owner;
                _registrationOwners.RemoveAt(last);
                registrationOwnerFootprintExpired?.Invoke(owner);
            }
        }

        internal static int currentBlockCount => _blocksCurrent.Count;

        internal static int previousBlockCount => _blocksPrevious.Count;

        internal static int registrationOwnerCount => _registrationOwners.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }

    [NowScope]
    internal struct NowOverlayHostScope : IDisposable
    {
        readonly bool _active;

        bool _disposed;

        internal NowOverlayHostScope(bool active)
        {
            _active = active;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_active)
                NowOverlay.PopHost();
        }
    }
}
