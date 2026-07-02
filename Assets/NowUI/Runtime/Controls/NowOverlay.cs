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

        internal static void PopFitProvider()
        {
            if (_fitProviders.Count > 0)
                _fitProviders.RemoveAt(_fitProviders.Count - 1);
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
    /// underneath (resolved one frame late, like the focus registry — immediate
    /// mode has no z-order to query).
    /// <code>
    /// NowOverlay.Defer(popupRect, popupId, DrawPopup);
    /// </code>
    /// </summary>
    public static class NowOverlay
    {
        public delegate void DrawCallback(int state);

        struct DeferredDraw
        {
            public Action draw;
            public DrawCallback drawWithState;
            public int state;
            public int overlayId;
            public Now.NowTransformSnapshot transform;
        }

        struct OverlayBlock
        {
            public NowRect rect;
            public int id;
            public int parentId;
            public bool modal;
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

        static readonly List<DeferredDraw> _deferred = new List<DeferredDraw>(4);

        static readonly List<OverlayBlock> _blocksCurrent = new List<OverlayBlock>(4);

        static readonly List<OverlayBlock> _blocksPrevious = new List<OverlayBlock>(4);

        static readonly List<int> _drawingStack = new List<int>(4);

        static readonly List<OverlayHostContext> _hostStack = new List<OverlayHostContext>(2);

        static int _registryFrame = -1;

        static int _overlayDepth;

        /// <summary>True while deferred overlay callbacks are executing.</summary>
        public static bool isDrawingOverlay => _overlayDepth > 0;

        /// <summary>True while any overlay is registered or queued for this or the previous frame.</summary>
        public static bool hasOpenOverlay
        {
            get
            {
                BeginFrameIfNeeded();
                return _deferred.Count > 0 || _blocksCurrent.Count > 0 || _blocksPrevious.Count > 0;
            }
        }

        internal static int currentFocusLayerId => CurrentOverlayId();

        internal static int activeFocusLayerId
        {
            get
            {
                BeginFrameIfNeeded();

                int current = FindTopOverlayId(_blocksCurrent);
                int previous = FindTopOverlayId(_blocksPrevious);

                if (current != 0 && previous != 0 && current != previous &&
                    OverlayIdBelongsToTree(previous, current, _blocksPrevious))
                {
                    return previous;
                }

                if (current != 0)
                    return current;

                return previous;
            }
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
        /// Queues a draw callback for the end of the frame and blocks pointer
        /// interaction inside <paramref name="blockRect"/> for everything that is
        /// not itself overlay content. Ignored during layout measure passes.
        /// </summary>
        public static void Defer(NowRect blockRect, Action draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw { draw = draw, transform = Now.CaptureTransform() });
            AddBlock(Now.TransformScreenRect(blockRect), 0);
        }

        /// <summary>
        /// Queues an overlay whose geometry is already in screen space.
        /// </summary>
        public static void DeferScreen(NowRect blockRect, Action draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw { draw = draw });
            AddBlock(blockRect, 0);
        }

        /// <summary>
        /// Queues a non-capturing draw callback. Store per-overlay state under
        /// <paramref name="state"/> and pass a static method to avoid closure
        /// allocation on warmed popup paths.
        /// </summary>
        public static void Defer(NowRect blockRect, int state, DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                drawWithState = draw,
                state = state,
                overlayId = state,
                transform = Now.CaptureTransform()
            });
            AddBlock(Now.TransformScreenRect(blockRect), state);
        }

        /// <summary>
        /// Queues a non-capturing screen-space overlay callback.
        /// </summary>
        public static void DeferScreen(NowRect blockRect, int state, DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw { drawWithState = draw, state = state, overlayId = state });
            AddBlock(blockRect, state);
        }

        /// <summary>
        /// Queues an overlay that draws above everything but never blocks the
        /// pointer — tooltips and other purely informational layers that must not
        /// steal hover or clicks from the controls beneath them.
        /// </summary>
        public static void DeferPassive(int state, DrawCallback draw)
        {
            if (draw == null || NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(new DeferredDraw
            {
                drawWithState = draw,
                state = state,
                overlayId = state,
                transform = Now.CaptureTransform()
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
            AddBlock(Now.TransformScreenRect(blockRect), 0);
        }

        /// <summary>
        /// Blocks pointer interaction inside a screen-space rect.
        /// </summary>
        public static void BlockScreen(NowRect blockRect)
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();
            AddBlock(blockRect, 0);
        }

        /// <summary>
        /// Blocks base-content pointer interaction on every NowUI surface, not
        /// just the registering host's — the modal guarantee for context menus
        /// and modal dialogs. Overlay content itself stays interactive.
        /// </summary>
        public static void BlockAllSurfaces()
        {
            if (NowInput.isPassive)
                return;

            BeginFrameIfNeeded();

            var host = CurrentHostContext();

            _blocksCurrent.Add(new OverlayBlock
            {
                rect = new NowRect(-100000f, -100000f, 200000f, 200000f),
                id = 0,
                parentId = CurrentOverlayId(),
                modal = true,
                host = host.host,
                hostRectTransform = host.rectTransform,
                hostCamera = host.camera
            });
        }

        /// <summary>
        /// True when the pointer position is owned by overlay content registered
        /// last frame; base-layer interactions treat it as hover-blocked. Queries
        /// roll the frame too, so blocks expire even when no overlay registers
        /// this frame (a context menu that just closed must release the pointer).
        /// </summary>
        public static bool IsPointerBlocked(Vector2 pointerPosition)
        {
            if (_overlayDepth > 0)
                return false;

            BeginFrameIfNeeded();

            var host = CurrentHostContext().host;

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (_blocksPrevious[i].modal)
                    return true;

                if (BlockBelongsToHost(_blocksPrevious[i], host) &&
                    _blocksPrevious[i].rect.Contains(pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="pointerPosition"/> is inside the overlay
        /// registered for <paramref name="rootId"/> or any nested overlay deferred
        /// while that root was drawing. Use this for popup outside-click checks.
        /// </summary>
        public static bool IsPointerInsideOverlayTree(int rootId, Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return IsPointerInsideOverlayTree(rootId, pointerPosition, _blocksCurrent);

            return IsPointerInsideOverlayTree(rootId, pointerPosition, _blocksCurrent) ||
                IsPointerInsideOverlayTree(rootId, pointerPosition, _blocksPrevious);
        }

        /// <summary>
        /// True when the pointer is inside any concrete overlay popup. Modal
        /// screen-wide blocks use id 0 and are intentionally ignored.
        /// </summary>
        internal static bool IsPointerInsideOverlay(Vector2 pointerPosition)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return IsPointerInsideOverlay(pointerPosition, _blocksCurrent);

            return IsPointerInsideOverlay(pointerPosition, _blocksCurrent) ||
                IsPointerInsideOverlay(pointerPosition, _blocksPrevious);
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
        /// True when an overlay was deferred while <paramref name="rootId"/> or
        /// one of its descendants was drawing. Use this to let cancel close the
        /// topmost nested popup before its parents.
        /// </summary>
        public static bool HasNestedOverlay(int rootId)
        {
            BeginFrameIfNeeded();

            if (_overlayDepth > 0)
                return HasNestedOverlay(rootId, _blocksCurrent);

            return HasNestedOverlay(rootId, _blocksCurrent) ||
                HasNestedOverlay(rootId, _blocksPrevious);
        }

        static bool HasNestedOverlay(int rootId, List<OverlayBlock> blocks)
        {
            if (rootId == 0)
                return false;

            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].id == 0 || blocks[i].id == rootId)
                    continue;

                if (BlockBelongsToTree(blocks[i], rootId, blocks))
                    return true;
            }

            return false;
        }

        static bool IsPointerInsideOverlayTree(int rootId, Vector2 pointerPosition, List<OverlayBlock> blocks)
        {
            if (rootId == 0)
                return false;

            for (int i = 0; i < blocks.Count; ++i)
            {
                if (!blocks[i].rect.Contains(pointerPosition))
                    continue;

                if (BlockBelongsToTree(blocks[i], rootId, blocks))
                    return true;
            }

            return false;
        }

        static bool IsPointerInsideOverlay(Vector2 pointerPosition, List<OverlayBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].id != 0 && blocks[i].rect.Contains(pointerPosition))
                    return true;
            }

            return false;
        }

        static bool IsPointerInsideOverlay(Component host, Vector2 pointerPosition, List<OverlayBlock> blocks)
        {
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (blocks[i].id != 0 &&
                    BlockBelongsToHost(blocks[i], host) &&
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

        static bool BlockBelongsToHost(OverlayBlock block, Component host)
        {
            if (host == null)
                return block.host == null;

            return block.host == host;
        }

        static bool BlockContainsScreenPoint(OverlayBlock block, Vector2 screenPosition)
        {
            if (block.hostRectTransform == null)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
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

        static void AddBlock(NowRect rect, int id)
        {
            var host = CurrentHostContext();

            _blocksCurrent.Add(new OverlayBlock
            {
                rect = rect,
                id = id,
                parentId = CurrentOverlayId(),
                host = host.host,
                hostRectTransform = host.rectTransform,
                hostCamera = host.camera
            });
        }

        static OverlayHostContext CurrentHostContext()
        {
            return _hostStack.Count > 0 ? _hostStack[_hostStack.Count - 1] : default;
        }

        static bool BlockBelongsToTree(OverlayBlock block, int rootId, List<OverlayBlock> blocks)
        {
            if (block.id == rootId)
                return true;

            int parentId = block.parentId;

            for (int guard = 0; guard < blocks.Count && parentId != 0; ++guard)
            {
                if (parentId == rootId)
                    return true;

                parentId = FindParentId(parentId, blocks);
            }

            return false;
        }

        static bool OverlayIdBelongsToTree(int id, int rootId, List<OverlayBlock> blocks)
        {
            if (id == 0 || rootId == 0)
                return false;

            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id == id)
                    return BlockBelongsToTree(blocks[i], rootId, blocks);
            }

            return false;
        }

        static int FindParentId(int id, List<OverlayBlock> blocks)
        {
            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id == id)
                    return blocks[i].parentId;
            }

            return 0;
        }

        static int CurrentOverlayId()
        {
            for (int i = _drawingStack.Count - 1; i >= 0; --i)
            {
                if (_drawingStack[i] != 0)
                    return _drawingStack[i];
            }

            return 0;
        }

        static int FindTopOverlayId(List<OverlayBlock> blocks)
        {
            for (int i = blocks.Count - 1; i >= 0; --i)
            {
                if (blocks[i].id != 0)
                    return blocks[i].id;
            }

            return 0;
        }

        static void BeginFrameIfNeeded()
        {
            int frame = Time.frameCount;

            if (_registryFrame == frame)
                return;

            _registryFrame = frame;
            _blocksPrevious.Clear();
            _blocksPrevious.AddRange(_blocksCurrent);
            _blocksCurrent.Clear();
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
            if (_deferred.Count == 0)
                return;

            using var profile = NowProfiler.OverlayFlush.Auto();
            ++_overlayDepth;

            try
            {
                // Callbacks may defer more overlays (nested menus); those run within
                // the same flush, drawn after their parents.
                for (int i = 0; i < _deferred.Count; ++i)
                {
                    var deferred = _deferred[i];
                    _drawingStack.Add(deferred.overlayId);

                    try
                    {
                        using (Now.ApplyTransformSnapshot(deferred.transform))
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
                    }
                }
            }
            finally
            {
                _deferred.Clear();
                _drawingStack.Clear();
                --_overlayDepth;
            }
        }

        public static void Reset()
        {
            _deferred.Clear();
            _blocksCurrent.Clear();
            _blocksPrevious.Clear();
            _drawingStack.Clear();
            _hostStack.Clear();
            _registryFrame = -1;
            _overlayDepth = 0;
            NowPopupPlacement.Reset();
        }

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
