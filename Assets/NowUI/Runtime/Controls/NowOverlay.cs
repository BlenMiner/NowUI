using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
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
        }

        struct OverlayBlock
        {
            public NowRect rect;
            public int id;
            public int parentId;
        }

        static readonly List<DeferredDraw> _deferred = new List<DeferredDraw>(4);

        static readonly List<OverlayBlock> _blocksCurrent = new List<OverlayBlock>(4);

        static readonly List<OverlayBlock> _blocksPrevious = new List<OverlayBlock>(4);

        static readonly List<int> _drawingStack = new List<int>(4);

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
            _deferred.Add(new DeferredDraw { draw = draw });
            _blocksCurrent.Add(new OverlayBlock { rect = blockRect, parentId = CurrentOverlayId() });
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
            _deferred.Add(new DeferredDraw { drawWithState = draw, state = state, overlayId = state });
            _blocksCurrent.Add(new OverlayBlock { rect = blockRect, id = state, parentId = CurrentOverlayId() });
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
            _blocksCurrent.Add(new OverlayBlock { rect = blockRect, parentId = CurrentOverlayId() });
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

            for (int i = 0; i < _blocksPrevious.Count; ++i)
            {
                if (_blocksPrevious[i].rect.Contains(pointerPosition))
                    return true;
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
        /// Runs the deferred callbacks. Called by <see cref="Now.FlushUI"/> and at
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
                        if (deferred.drawWithState != null)
                            deferred.drawWithState(deferred.state);
                        else
                            deferred.draw?.Invoke();
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
            _registryFrame = -1;
            _overlayDepth = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
