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
    /// NowUIOverlay.Defer(popupRect, static () => DrawPopup());
    /// </code>
    /// </summary>
    public static class NowUIOverlay
    {
        static readonly List<Action> _deferred = new List<Action>(4);

        static readonly List<NowRect> _blocksCurrent = new List<NowRect>(4);

        static readonly List<NowRect> _blocksPrevious = new List<NowRect>(4);

        static int _registryFrame = -1;

        static int _overlayDepth;

        /// <summary>True while deferred overlay callbacks are executing.</summary>
        public static bool isDrawingOverlay => _overlayDepth > 0;

        /// <summary>
        /// Queues a draw callback for the end of the frame and blocks pointer
        /// interaction inside <paramref name="blockRect"/> for everything that is
        /// not itself overlay content. Ignored during layout measure passes.
        /// </summary>
        public static void Defer(NowRect blockRect, Action draw)
        {
            if (draw == null || NowUIInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _deferred.Add(draw);
            _blocksCurrent.Add(blockRect);
        }

        /// <summary>
        /// Blocks pointer interaction inside the rect without deferring a draw —
        /// for overlays that manage their own draw order (modal scrims).
        /// </summary>
        public static void Block(NowRect blockRect)
        {
            if (NowUIInput.isPassive)
                return;

            BeginFrameIfNeeded();
            _blocksCurrent.Add(blockRect);
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
                if (_blocksPrevious[i].Contains(pointerPosition))
                    return true;
            }

            return false;
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

            using var profile = NowUIProfiler.OverlayFlush.Auto();
            ++_overlayDepth;

            try
            {
                // Callbacks may defer more overlays (nested menus); those run within
                // the same flush, drawn after their parents.
                for (int i = 0; i < _deferred.Count; ++i)
                    _deferred[i]();
            }
            finally
            {
                _deferred.Clear();
                --_overlayDepth;
            }
        }

        public static void Reset()
        {
            _deferred.Clear();
            _blocksCurrent.Clear();
            _blocksPrevious.Clear();
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
