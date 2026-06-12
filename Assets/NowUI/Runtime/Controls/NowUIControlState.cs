using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Per-control ephemeral state for immediate-mode controls: callers own their
    /// values (ref parameters), this store owns everything transient — hover
    /// transitions, double-click timestamps, key-repeat timers, scroll offsets.
    /// Slots are keyed by control id and evicted after going untouched for a while,
    /// mirroring NowLayout's measurement cache.
    ///
    /// Custom controls build on the same helpers the built-in ones use; nothing
    /// here is internal-only.
    /// </summary>
    public static class NowUIControlState
    {
        const float EVICT_AFTER_SECONDS = 10f;

        const float SWEEP_INTERVAL_SECONDS = 1f;

        sealed class Entry<T>
        {
            public T value;
            public float lastTouch;
        }

        static class Store<T>
        {
            public static readonly Dictionary<int, Entry<T>> entries = new Dictionary<int, Entry<T>>(64);

            public static float lastSweep;

            static Store()
            {
                s_resets.Add(() => entries.Clear());
            }
        }

        static readonly List<Action> s_resets = new List<Action>(8);

        static readonly List<int> s_sweepScratch = new List<int>(16);

        static bool s_repaintRequested;

        /// <summary>
        /// Returns a persistent slot for this control id, created zeroed on first
        /// use. The reference stays valid for the current frame; re-fetch each frame.
        /// </summary>
        public static ref T Get<T>(int id) where T : struct
        {
            var entries = Store<T>.entries;
            float now = Time.realtimeSinceStartup;

            if (!entries.TryGetValue(id, out var entry))
            {
                Sweep<T>(now);
                entry = new Entry<T>();
                entries.Add(id, entry);
            }

            entry.lastTouch = now;
            return ref entry.value;
        }

        static void Sweep<T>(float now)
        {
            if (now - Store<T>.lastSweep < SWEEP_INTERVAL_SECONDS)
                return;

            Store<T>.lastSweep = now;
            s_sweepScratch.Clear();

            foreach (var pair in Store<T>.entries)
            {
                if (now - pair.Value.lastTouch > EVICT_AFTER_SECONDS)
                    s_sweepScratch.Add(pair.Key);
            }

            for (int i = 0; i < s_sweepScratch.Count; ++i)
                Store<T>.entries.Remove(s_sweepScratch[i]);

            s_sweepScratch.Clear();
        }

        // ------------------------------------------------------------------
        // Timing helpers
        // ------------------------------------------------------------------

        struct TransitionState
        {
            public float t;
            public float lastTime;
        }

        /// <summary>
        /// Moves a stored 0..1 value toward 1 (or 0) at <paramref name="speed"/> per
        /// second and returns it — the building block for hover/press fades. Calls
        /// <see cref="RequestRepaint"/> while mid-transition so retained hosts (UGUI)
        /// keep rebuilding until the animation settles.
        /// </summary>
        public static float Transition(int id, bool towardOne, float speed = 10f)
        {
            ref var state = ref Get<TransitionState>(id);
            float now = Time.realtimeSinceStartup;
            float delta = state.lastTime > 0f ? Mathf.Min(now - state.lastTime, 0.1f) : 0f;
            state.lastTime = now;

            float target = towardOne ? 1f : 0f;
            state.t = Mathf.MoveTowards(state.t, target, delta * speed);

            if (!Mathf.Approximately(state.t, target))
                RequestRepaint();

            return state.t;
        }

        struct DoubleClickState
        {
            public float lastClickTime;
        }

        /// <summary>True when this click lands within <paramref name="window"/> of the previous one.</summary>
        public static bool DetectDoubleClick(int id, bool clicked, float window = 0.35f)
        {
            if (!clicked)
                return false;

            ref var state = ref Get<DoubleClickState>(id);
            float now = Time.realtimeSinceStartup;
            bool isDouble = state.lastClickTime > 0f && now - state.lastClickTime <= window;
            state.lastClickTime = isDouble ? 0f : now;
            return isDouble;
        }

        struct RepeatState
        {
            public float heldSince;
            public float lastPulse;
        }

        /// <summary>
        /// Key-repeat pulses: true on the initial press, then after
        /// <paramref name="delay"/> repeats every <paramref name="interval"/> while
        /// <paramref name="held"/> stays true.
        /// </summary>
        public static bool Repeat(int id, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            ref var state = ref Get<RepeatState>(id);
            float now = Time.realtimeSinceStartup;

            if (!held)
            {
                state.heldSince = 0f;
                return false;
            }

            RequestRepaint();

            if (state.heldSince <= 0f)
            {
                state.heldSince = now;
                state.lastPulse = now;
                return true;
            }

            if (now - state.heldSince >= delay && now - state.lastPulse >= interval)
            {
                state.lastPulse = now;
                return true;
            }

            return false;
        }

        /// <summary>Square-wave blink (caret-style); stateless.</summary>
        public static bool Blink(float period = 1f)
        {
            return period <= 0f || Time.realtimeSinceStartup % period < period * 0.5f;
        }

        // ------------------------------------------------------------------
        // Repaint requests (retained hosts)
        // ------------------------------------------------------------------

        /// <summary>
        /// Tells a retained host (a UGUI <see cref="NowUIGraphic"/>) that this
        /// control needs another frame — call while animating, focused with a
        /// blinking caret, or otherwise time-dependent. The immediate-mode screen
        /// path repaints every frame anyway, so calling it there is free.
        /// </summary>
        public static void RequestRepaint()
        {
            s_repaintRequested = true;
        }

        internal static void BeginRepaintTracking()
        {
            s_repaintRequested = false;
        }

        internal static bool EndRepaintTracking()
        {
            return s_repaintRequested;
        }

        public static void Reset()
        {
            for (int i = 0; i < s_resets.Count; ++i)
                s_resets[i]();

            s_repaintRequested = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
