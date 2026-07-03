using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Central pointer-ownership arbitration across input surfaces. Every
    /// provider that samples the shared physical pointer registers a claim each
    /// frame — its layering tier, its depth within that tier, whether it
    /// actually has content under the pointer, and whether buttons are held.
    /// The winner is resolved from the previous frame's claims (one frame late,
    /// like the focus and overlay registries, so the result cannot depend on
    /// host update order) and every other surface is told it does not have the
    /// pointer. Ownership latches while the winner holds a button so drags
    /// never transfer mid-press.
    ///
    /// When no surface claimed the pointer last frame, everyone owns it — a
    /// single-surface app or a test with fake providers never pays for
    /// arbitration it does not need.
    /// </summary>
    public static class NowPointerArbiter
    {
        /// <summary>World-space surfaces, ordered among themselves by ray distance.</summary>
        public const int TierWorld = 100;

        /// <summary>The camera overlay/screen path, drawn over the 3D scene.</summary>
        public const int TierScreen = 300;

        /// <summary>Screen-space canvas and UI Toolkit hosts, drawn over everything.</summary>
        public const int TierCanvas = 400;

        struct SurfaceClaim
        {
            public object key;
            public int tier;
            public float depth;
            public bool hit;
            public bool buttonsDown;
        }

        struct ContentRect
        {
            public object key;
            public Rect rect;
        }

        static readonly List<SurfaceClaim> _claimsCurrent = new List<SurfaceClaim>(4);

        static readonly List<SurfaceClaim> _claimsPrevious = new List<SurfaceClaim>(4);

        static readonly List<ContentRect> _contentCurrent = new List<ContentRect>(64);

        static readonly List<ContentRect> _contentPrevious = new List<ContentRect>(64);

        static object _winner;

        static int _registryFrame = -1;

        /// <summary>
        /// Registers this frame's claim for a surface. Later claims with the
        /// same key replace earlier ones, so providers may claim once per
        /// snapshot rebuild without bookkeeping.
        /// </summary>
        public static void Claim(object key, int tier, float depth, bool hit, bool buttonsDown)
        {
            if (key == null)
                return;

            BeginFrameIfNeeded();

            for (int i = 0; i < _claimsCurrent.Count; ++i)
            {
                if (ReferenceEquals(_claimsCurrent[i].key, key))
                {
                    _claimsCurrent[i] = new SurfaceClaim { key = key, tier = tier, depth = depth, hit = hit, buttonsDown = buttonsDown };
                    return;
                }
            }

            _claimsCurrent.Add(new SurfaceClaim { key = key, tier = tier, depth = depth, hit = hit, buttonsDown = buttonsDown });
        }

        /// <summary>
        /// True when this surface may treat the pointer as its own: either it
        /// won last frame's arbitration, or nothing claimed the pointer at all.
        /// </summary>
        public static bool IsOwner(object key)
        {
            BeginFrameIfNeeded();
            return _winner == null || ReferenceEquals(_winner, key);
        }

        /// <summary>
        /// Records an interactive rect drawn by a surface this frame, in that
        /// surface's own coordinates. <see cref="NowInput.Interact(int, Rect, NowPointerButton)"/>
        /// feeds this automatically, giving surfaces with no fixed bounds (the
        /// screen path) a footprint to claim with.
        /// </summary>
        public static void NoteContent(object key, Rect rect)
        {
            if (key == null)
                return;

            BeginFrameIfNeeded();
            _contentCurrent.Add(new ContentRect { key = key, rect = rect });
        }

        /// <summary>True when the surface drew interactive content containing this position last frame.</summary>
        public static bool HadContentAt(object key, Vector2 position)
        {
            if (key == null)
                return false;

            BeginFrameIfNeeded();

            for (int i = 0; i < _contentPrevious.Count; ++i)
            {
                if (ReferenceEquals(_contentPrevious[i].key, key) && _contentPrevious[i].rect.Contains(position))
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
            SwapAndResolve();
        }

        /// <summary>Forces the frame swap; used by tests where frameCount is static.</summary>
        public static void ForceNewFrame()
        {
            _registryFrame = -1;
            BeginFrameIfNeeded();
        }

        static void SwapAndResolve()
        {
            _claimsPrevious.Clear();
            _claimsPrevious.AddRange(_claimsCurrent);
            _claimsCurrent.Clear();

            _contentPrevious.Clear();
            _contentPrevious.AddRange(_contentCurrent);
            _contentCurrent.Clear();

            if (_winner != null)
            {
                for (int i = 0; i < _claimsPrevious.Count; ++i)
                {
                    if (ReferenceEquals(_claimsPrevious[i].key, _winner) && _claimsPrevious[i].buttonsDown)
                        return;
                }
            }

            _winner = null;
            int bestTier = int.MinValue;
            float bestDepth = float.PositiveInfinity;

            for (int i = 0; i < _claimsPrevious.Count; ++i)
            {
                var claim = _claimsPrevious[i];

                if (!claim.hit)
                    continue;

                if (claim.tier < bestTier || (claim.tier == bestTier && claim.depth >= bestDepth))
                    continue;

                _winner = claim.key;
                bestTier = claim.tier;
                bestDepth = claim.depth;
            }
        }

        public static void Reset()
        {
            _claimsCurrent.Clear();
            _claimsPrevious.Clear();
            _contentCurrent.Clear();
            _contentPrevious.Clear();
            _winner = null;
            _registryFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
