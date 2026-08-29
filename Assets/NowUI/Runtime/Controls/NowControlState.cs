using System;
using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    public readonly struct NowPressAnimation
    {
        public readonly bool active;
        public readonly Vector2 origin;
        public readonly float progress;

        public NowPressAnimation(bool active, Vector2 origin, float progress)
        {
            this.active = active;
            this.origin = origin;
            this.progress = progress;
        }
    }

    /// <summary>
    /// Per-control ephemeral state for immediate-mode controls: callers own their
    /// values (ref parameters), this store owns everything transient — hover
    /// transitions, double-click timestamps, key-repeat timers, scroll offsets.
    /// Typed slots are keyed by resolved control identity inside the State domain.
    /// Raw integer overloads are source-blocked; the internal compatibility importer
    /// remains isolated from typed state. Slots are evicted after going untouched for
    /// a while, mirroring NowLayout's measurement cache.
    ///
    /// Custom controls build on the same helpers the built-in ones use; nothing
    /// here is internal-only.
    /// </summary>
    public static class NowControlState
    {
        const float EVICT_AFTER_SECONDS = 10f;

        const float SWEEP_INTERVAL_SECONDS = 1f;

        const string LegacyIdObsoleteMessage =
            "Raw integer state identities were removed. Resolve an authored NowId once and use the NowResolvedId overload.";

        sealed class Entry<T>
        {
            public T value;
            public float lastTouch;
        }

        static class Store<T>
        {
            public static readonly Dictionary<NowResolvedId, Entry<T>> entries =
                new Dictionary<NowResolvedId, Entry<T>>(64);

            public static float lastSweep;

            static Store()
            {
                s_resets.Add(() =>
                {
                    entries.Clear();
                    lastSweep = 0f;
                });
            }
        }

        static readonly List<Action> s_resets = new List<Action>(8);

        static readonly List<NowResolvedId> s_sweepScratch = new List<NowResolvedId>(16);

        static bool s_repaintRequested;

        static float s_nextRepaintAt = float.PositiveInfinity;

        /// <summary>
        /// Returns a persistent slot for this resolved control id, created zeroed on first
        /// use. The reference stays valid for the current frame; re-fetch each frame.
        /// </summary>
        public static ref T Get<T>(NowResolvedId id) where T : struct
        {
            return ref GetByStateKey<T>(StateKey(id));
        }

        /// <summary>
        /// Source-blocked compatibility slot. It remains only to produce a precise
        /// migration error for callers that still pass a raw integer identity.
        /// </summary>
        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static ref T Get<T>(int id) where T : struct
        {
            return ref GetByStateKey<T>(LegacyKey(id));
        }

        /// <summary>
        /// Reads an existing slot without creating it or extending its lifetime.
        /// Internal hot paths use this to keep untouched controls at their
        /// implicit zero state without populating the persistent store.
        /// </summary>
        internal static bool TryRead<T>(NowResolvedId id, out T value) where T : struct
        {
            return TryReadByStateKey(StateKey(id), out value);
        }

        internal static bool TryRead<T>(int id, out T value) where T : struct
        {
            return TryReadByStateKey(LegacyKey(id), out value);
        }

        static bool TryReadByStateKey<T>(NowResolvedId stateKey, out T value) where T : struct
        {
            if (Store<T>.entries.TryGetValue(stateKey, out var entry))
            {
                value = entry.value;
                return true;
            }

            value = default;
            return false;
        }

        static ref T GetByStateKey<T>(NowResolvedId stateKey) where T : struct
        {
            float now = Time.realtimeSinceStartup;
            var entry = GetOrCreateEntry<T>(stateKey, now);
            entry.lastTouch = now;
            return ref entry.value;
        }

        static Entry<T> GetOrCreateEntry<T>(NowResolvedId stateKey, float now) where T : struct
        {
            var entries = Store<T>.entries;

            if (!entries.TryGetValue(stateKey, out var entry))
            {
                Sweep<T>(now);
                entry = new Entry<T>();
                entries.Add(stateKey, entry);
            }

            return entry;
        }

        static NowResolvedId StateKey(NowResolvedId id)
        {
            return id.InDomain(NowIdDomain.State);
        }

        static NowResolvedId StateKey(NowResolvedId id, string key)
        {
            return StateKey(id).Child(key);
        }

        static NowResolvedId LegacyKey(int id)
        {
            return NowResolvedId.FromLegacy(id);
        }

        static NowResolvedId LegacyKey(int id, string key)
        {
            // Preserve the exact legacy aliasing contract: a named integer slot
            // is the same slot as the old pre-composed integer id. Typed state
            // instead derives its named child after entering the State domain.
            return LegacyKey(NowInput.GetLegacyId(id, key));
        }

        /// <summary>
        /// Returns a persistent slot for a named sub-state under this control id.
        /// </summary>
        public static ref T Get<T>(NowResolvedId id, string key) where T : struct
        {
            return ref GetByStateKey<T>(StateKey(id, key));
        }

        /// <summary>Source-blocked compatibility adapter for a named integer slot.</summary>
        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static ref T Get<T>(int id, string key) where T : struct
        {
            return ref GetByStateKey<T>(LegacyKey(id, key));
        }

        /// <summary>
        /// Creates this control-state slot outside a measured frame. Use during
        /// scene/widget initialization for known stable ids so the first interactive
        /// frame does not allocate the slot.
        /// </summary>
        public static void Warmup<T>(NowResolvedId id) where T : struct
        {
            Warmup(id, default(T));
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Warmup<T>(int id) where T : struct
        {
            WarmupByStateKey(LegacyKey(id), default(T));
        }

        /// <summary>
        /// Creates a named sub-state slot outside a measured frame.
        /// </summary>
        public static void Warmup<T>(NowResolvedId id, string key) where T : struct
        {
            WarmupByStateKey(StateKey(id, key), default(T));
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Warmup<T>(int id, string key) where T : struct
        {
            WarmupByStateKey(LegacyKey(id, key), default(T));
        }

        /// <summary>
        /// Creates this control-state slot with an initial value if it is missing.
        /// Existing slots are left untouched.
        /// </summary>
        public static void Warmup<T>(NowResolvedId id, T initialValue) where T : struct
        {
            WarmupByStateKey(StateKey(id), initialValue);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Warmup<T>(int id, T initialValue) where T : struct
        {
            WarmupByStateKey(LegacyKey(id), initialValue);
        }

        static void WarmupByStateKey<T>(NowResolvedId stateKey, T initialValue) where T : struct
        {
            var entries = Store<T>.entries;
            float now = Time.realtimeSinceStartup;

            if (!entries.TryGetValue(stateKey, out var entry))
            {
                Sweep<T>(now);
                entry = new Entry<T>
                {
                    value = initialValue
                };
                entries.Add(stateKey, entry);
            }

            entry.lastTouch = now;
        }

        /// <summary>
        /// Creates a named sub-state slot with an initial value if it is missing.
        /// Existing slots are left untouched.
        /// </summary>
        public static void Warmup<T>(NowResolvedId id, string key, T initialValue) where T : struct
        {
            WarmupByStateKey(StateKey(id, key), initialValue);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Warmup<T>(int id, string key, T initialValue) where T : struct
        {
            WarmupByStateKey(LegacyKey(id, key), initialValue);
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
        public static float Transition(NowResolvedId id, bool towardOne, float speed = 10f)
        {
            return TransitionByStateKey(StateKey(id), towardOne, speed);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static float Transition(int id, bool towardOne, float speed = 10f)
        {
            return TransitionByStateKey(LegacyKey(id), towardOne, speed);
        }

        static float TransitionByStateKey(NowResolvedId stateKey, bool towardOne, float speed)
        {
            // Get and advance from one timestamp. Get<T> and the old transition
            // path each sampled realtime independently, doubling the clock calls
            // made by every animated control while adding no useful precision.
            float now = Time.realtimeSinceStartup;
            var entry = GetOrCreateEntry<TransitionState>(stateKey, now);
            entry.lastTouch = now;
            return AdvanceTransition(ref entry.value, towardOne, speed, now);
        }

        static float AdvanceTransition(ref TransitionState state, bool towardOne, float speed, float now)
        {
            if (NowInput.isPassive)
                return state.t;

            float delta = state.lastTime > 0f ? Mathf.Min(now - state.lastTime, 0.1f) : 0f;
            state.lastTime = now;

            float target = towardOne ? 1f : 0f;
            state.t = Mathf.MoveTowards(state.t, target, delta * speed);

            if (!Mathf.Approximately(state.t, target))
                RequestRepaint();

            return state.t;
        }

        /// <summary>
        /// Moves a stored 0..1 value under this interaction's control id.
        /// </summary>
        public static float Transition(NowInteraction interaction, bool towardOne, float speed = 10f)
        {
            return TransitionByStateKey(StateKey(interaction.id), towardOne, speed);
        }

        /// <summary>
        /// Moves a stored 0..1 value under a named sub-state of this interaction.
        /// </summary>
        public static float Transition(NowInteraction interaction, string key, bool towardOne, float speed = 10f)
        {
            return TransitionByStateKey(StateKey(interaction.id, key), towardOne, speed);
        }

        struct DoubleClickState
        {
            public float lastClickTime;
        }

        /// <summary>True when this click lands within <paramref name="window"/> of the previous one.</summary>
        public static bool DetectDoubleClick(NowResolvedId id, bool clicked, float window = 0.35f)
        {
            return DetectDoubleClickByStateKey(StateKey(id), clicked, window);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static bool DetectDoubleClick(int id, bool clicked, float window = 0.35f)
        {
            return DetectDoubleClickByStateKey(LegacyKey(id), clicked, window);
        }

        static bool DetectDoubleClickByStateKey(NowResolvedId stateKey, bool clicked, float window)
        {
            if (!clicked)
                return false;

            ref var state = ref GetByStateKey<DoubleClickState>(stateKey);
            float now = Time.realtimeSinceStartup;
            bool isDouble = state.lastClickTime > 0f && now - state.lastClickTime <= window;
            state.lastClickTime = isDouble ? 0f : now;
            return isDouble;
        }

        struct ClickStreakState
        {
            public float lastClickTime;
            public Vector2 lastPosition;
            public int count;
            public bool hasPosition;
        }

        /// <summary>
        /// Consecutive-click count for this click: 1 single, 2 double, 3 triple
        /// and so on; 0 on non-click frames. Each click must land within
        /// <paramref name="window"/> of the previous one to extend the streak.
        /// </summary>
        public static int ClickStreak(NowResolvedId id, bool clicked, float window = 0.35f)
        {
            return ClickStreak(id, clicked, default, -1f, window);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static int ClickStreak(int id, bool clicked, float window = 0.35f)
        {
            return ClickStreakByStateKey(LegacyKey(id), clicked, default, -1f, window);
        }

        /// <summary>
        /// Consecutive-click count for this click, requiring subsequent clicks to
        /// land near the previous click as well as inside the time window.
        /// </summary>
        public static int ClickStreak(
            NowResolvedId id,
            bool clicked,
            Vector2 position,
            float maxDistance = 6f,
            float window = 0.35f)
        {
            return ClickStreakByStateKey(StateKey(id), clicked, position, maxDistance, window);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static int ClickStreak(int id, bool clicked, Vector2 position, float maxDistance = 6f, float window = 0.35f)
        {
            return ClickStreakByStateKey(LegacyKey(id), clicked, position, maxDistance, window);
        }

        static int ClickStreakByStateKey(
            NowResolvedId stateKey,
            bool clicked,
            Vector2 position,
            float maxDistance,
            float window)
        {
            if (!clicked)
                return 0;

            ref var state = ref GetByStateKey<ClickStreakState>(stateKey);
            float now = Time.realtimeSinceStartup;
            bool inWindow = state.lastClickTime > 0f && now - state.lastClickTime <= window;
            bool usePosition = maxDistance >= 0f;
            bool inRange = !usePosition || (state.hasPosition &&
                (position - state.lastPosition).sqrMagnitude <= maxDistance * maxDistance);

            state.count = inWindow && inRange ? state.count + 1 : 1;
            state.lastClickTime = now;

            if (usePosition)
            {
                state.lastPosition = position;
                state.hasPosition = true;
            }
            else
            {
                state.hasPosition = false;
            }

            return state.count;
        }

        struct RepeatState
        {
            public float heldSince;
            public float lastPulse;
            public bool active;
        }

        struct PressAnimationState
        {
            public Vector2 origin;
            public float startTime;
            public bool active;
        }

        /// <summary>
        /// Key-repeat pulses: true on the initial press, then after
        /// <paramref name="delay"/> repeats every <paramref name="interval"/> while
        /// <paramref name="held"/> stays true.
        /// </summary>
        public static bool Repeat(NowResolvedId id, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            return RepeatByStateKey(StateKey(id), held, delay, interval);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static bool Repeat(int id, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            return RepeatByStateKey(LegacyKey(id), held, delay, interval);
        }

        static bool RepeatByStateKey(NowResolvedId stateKey, bool held, float delay, float interval)
        {
            // Measurement/replay passes must observe repeat state without
            // mutating it. In particular, passive interactions report
            // held=false; treating that as a release makes the following real
            // draw pulse as though the same physical press were new.
            if (NowInput.isPassive)
                return false;

            ref var state = ref GetByStateKey<RepeatState>(stateKey);
            float now = NowInput.hasContext
                ? NowInput.current.time
                : Time.realtimeSinceStartup;

            if (!held)
            {
                state.active = false;
                return false;
            }

            RequestRepaint();

            if (!state.active)
            {
                state.active = true;
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

        /// <summary>
        /// Key-repeat pulses for a named sub-state under <paramref name="id"/>.
        /// </summary>
        public static bool Repeat(
            NowResolvedId id,
            string key,
            bool held,
            float delay = 0.4f,
            float interval = 0.05f)
        {
            return RepeatByStateKey(StateKey(id, key), held, delay, interval);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static bool Repeat(int id, string key, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            return RepeatByStateKey(LegacyKey(id, key), held, delay, interval);
        }

        /// <summary>
        /// Key-repeat pulses under this interaction's control id.
        /// </summary>
        public static bool Repeat(NowInteraction interaction, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            return RepeatByStateKey(StateKey(interaction.id), held, delay, interval);
        }

        /// <summary>
        /// Key-repeat pulses for a named sub-state under this interaction.
        /// </summary>
        public static bool Repeat(NowInteraction interaction, string key, bool held, float delay = 0.4f, float interval = 0.05f)
        {
            return RepeatByStateKey(StateKey(interaction.id, key), held, delay, interval);
        }

        /// <summary>
        /// Tracks a press-triggered 0..1 animation for visual effects such as
        /// Material ripples. Returns the active animation and requests repaints
        /// until the effect has finished.
        /// </summary>
        public static NowPressAnimation PressAnimation(
            NowResolvedId id,
            bool triggered,
            Vector2 origin,
            float duration = 0.45f)
        {
            if (!id.hasValue || duration <= 0f)
                return default;

            return PressAnimationByStateKey(StateKey(id), triggered, origin, duration);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static NowPressAnimation PressAnimation(int id, bool triggered, Vector2 origin, float duration = 0.45f)
        {
            if (id == 0 || duration <= 0f)
                return default;

            return PressAnimationByStateKey(LegacyKey(id), triggered, origin, duration);
        }

        static NowPressAnimation PressAnimationByStateKey(
            NowResolvedId stateKey,
            bool triggered,
            Vector2 origin,
            float duration)
        {
            var entries = Store<PressAnimationState>.entries;

            // An animation that has never started has an implicit inactive state.
            // Keep idle controls out of the store and avoid sampling Unity's clock;
            // inactive retained entries are likewise left untouched so they can age out.
            if (!entries.TryGetValue(stateKey, out var entry))
            {
                if (!triggered)
                    return default;
            }
            else if (!triggered && !entry.value.active)
            {
                return default;
            }

            float now = Time.realtimeSinceStartup;

            if (entry == null)
                entry = GetOrCreateEntry<PressAnimationState>(stateKey, now);

            entry.lastTouch = now;
            ref var state = ref entry.value;

            if (!NowInput.isPassive && triggered)
            {
                state.origin = origin;
                state.startTime = now;
                state.active = true;
                RequestRepaint();
            }

            if (!state.active)
                return default;

            float progress = Mathf.Clamp01((now - state.startTime) / duration);

            if (progress < 1f)
            {
                RequestRepaint();
            }
            else
            {
                state.active = false;
            }

            return new NowPressAnimation(state.active, state.origin, progress);
        }

        internal static int pressAnimationStateCount => Store<PressAnimationState>.entries.Count;

        /// <summary>
        /// Tracks a press-triggered animation under a named sub-state of this interaction.
        /// </summary>
        public static NowPressAnimation PressAnimation(
            NowInteraction interaction,
            string key,
            bool triggered,
            Vector2 origin,
            float duration = 0.45f)
        {
            return PressAnimationByStateKey(
                StateKey(interaction.id, key),
                triggered,
                origin,
                duration);
        }

        /// <summary>Square-wave blink (caret-style); stateless.</summary>
        public static bool Blink(float period = 1f)
        {
            return period <= 0f || Time.realtimeSinceStartup % period < period * 0.5f;
        }

        /// <summary>
        /// Blink anchored to a moment: visible for the first half-period after
        /// <paramref name="anchor"/>, so a caret that keeps moving (anchor
        /// refreshed on every move) stays solid instead of blinking away.
        /// </summary>
        public static bool Blink(float period, float anchor)
        {
            return period <= 0f || (Time.realtimeSinceStartup - anchor) % period < period * 0.5f;
        }

        /// <summary>
        /// Anchored caret blink that schedules only the next phase boundary.
        /// This preserves blinking without forcing an idle host to rebuild on
        /// every update tick.
        /// </summary>
        public static bool ScheduledBlink(float period, float anchor)
        {
            if (period > 0f)
            {
                float halfPeriod = period * 0.5f;
                float now = Time.realtimeSinceStartup;
                float elapsed = Mathf.Max(0f, now - anchor);
                float completedPhases = Mathf.Floor(elapsed / halfPeriod);
                float nextBoundary = anchor + (completedPhases + 1f) * halfPeriod;

                if (nextBoundary <= now + 0.0001f)
                    nextBoundary += halfPeriod;

                RequestRepaintAt(nextBoundary);
            }

            return Blink(period, anchor);
        }

        /// <summary>
        /// Tells a retained host (a UGUI <c>NowGraphic</c>) that this
        /// control needs another frame — call while animating, focused with a
        /// blinking caret, or otherwise time-dependent. Immediate-mode IMGUI
        /// hosts forward tracked requests through their coalesced repaint bridge.
        /// </summary>
        public static void RequestRepaint()
        {
            s_repaintRequested = true;
        }

        /// <summary>
        /// Requests a host repaint no earlier than an absolute
        /// <see cref="Time.realtimeSinceStartup"/> timestamp.
        /// </summary>
        public static void RequestRepaintAt(float realtime)
        {
            if (!float.IsNaN(realtime) &&
                !float.IsInfinity(realtime) &&
                realtime < s_nextRepaintAt)
            {
                s_nextRepaintAt = realtime;
            }
        }

        internal static void BeginRepaintTracking()
        {
            s_repaintRequested = false;
            s_nextRepaintAt = float.PositiveInfinity;
        }

        internal static bool EndRepaintTracking()
        {
            return s_repaintRequested;
        }

        internal static bool EndRepaintTracking(out float nextRepaintAt)
        {
            nextRepaintAt = s_nextRepaintAt;
            return s_repaintRequested;
        }

        public static void Reset()
        {
            for (int i = 0; i < s_resets.Count; ++i)
                s_resets[i]();

            s_sweepScratch.Clear();
            s_repaintRequested = false;
            s_nextRepaintAt = float.PositiveInfinity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
