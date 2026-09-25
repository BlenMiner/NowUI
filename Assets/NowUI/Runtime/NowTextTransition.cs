using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>How a text value changes into the next one.</summary>
    public enum NowTextTransitionKind : byte
    {
        /// <summary>The old value fades out while the new one fades in, in place.</summary>
        Fade,

        /// <summary>The old value rises and fades out; the new one rises in from below.</summary>
        SlideUp,

        /// <summary>The old value drops and fades out; the new one drops in from above.</summary>
        SlideDown,

        /// <summary>
        /// Like <see cref="SlideUp"/>, but only the characters that changed move, like
        /// an odometer: 109 → 110 rolls the last two digits and leaves the 1. Values of
        /// different lengths slide as a whole.
        /// </summary>
        Roll
    }

    /// <summary>
    /// A text value transition: its kind, duration, travel distance and easing.
    /// Use it with <see cref="NowText.DrawValue(string, NowTextTransition, float, string, int)"/>,
    /// which animates whenever the value changes, or with
    /// <see cref="NowText.DrawTransition(string, string, float, NowTextTransition)"/> when
    /// the caller owns the progress.
    /// </summary>
    public readonly struct NowTextTransition
    {
        public readonly NowTextTransitionKind kind;

        /// <summary>Seconds on the caller's clock.</summary>
        public readonly float duration;

        /// <summary>Travel of sliding transitions, in ems of the text's font size.</summary>
        public readonly float distance;

        public readonly NowEasing easing;

        public NowTextTransition(NowTextTransitionKind kind, float duration, float distance, NowEasing easing)
        {
            this.kind = kind;
            this.duration = Mathf.Max(0f, duration);
            this.distance = distance;
            this.easing = easing;
        }

        public static NowTextTransition Fade(float duration = 0.2f)
        {
            return new NowTextTransition(NowTextTransitionKind.Fade, duration, 0f, NowEasing.OutCubic);
        }

        public static NowTextTransition SlideUp(float duration = 0.25f, float distance = 0.6f)
        {
            return new NowTextTransition(NowTextTransitionKind.SlideUp, duration, distance, NowEasing.OutCubic);
        }

        public static NowTextTransition SlideDown(float duration = 0.25f, float distance = 0.6f)
        {
            return new NowTextTransition(NowTextTransitionKind.SlideDown, duration, distance, NowEasing.OutCubic);
        }

        public static NowTextTransition Roll(float duration = 0.3f, float distance = 0.7f)
        {
            return new NowTextTransition(NowTextTransitionKind.Roll, duration, distance, NowEasing.OutCubic);
        }

        /// <summary>The same transition with another easing curve.</summary>
        public NowTextTransition WithEasing(NowEasing value)
        {
            return new NowTextTransition(kind, duration, distance, value);
        }
    }

    public partial struct NowText
    {
        struct ValueTransitionState
        {
            public bool initialized;
            public string previous;
            public string current;
            public float changedAt;
        }

        /// <summary>
        /// Draws the change from <paramref name="from"/> to <paramref name="to"/> at
        /// <paramref name="progress"/> (0 shows <paramref name="from"/>, 1 shows
        /// <paramref name="to"/>), eased by the transition's curve. Alignment, masks,
        /// gradients and scopes apply to both values. The transition's duration is not
        /// used; the caller owns the progress.
        /// </summary>
        public NowText DrawTransition(string from, string to, float progress, NowTextTransition transition)
        {
            float t = float.IsNaN(progress) ? 1f : Mathf.Clamp01(progress);

            if (t >= 1f || from == null || string.Equals(from, to))
                return Draw(to);

            float eased = NowEase.Evaluate(transition.easing, t);
            float travel = transition.distance * fontSize;
            var outgoing = this;
            var incoming = this;

            switch (transition.kind)
            {
                case NowTextTransitionKind.Fade:
                    using (Now.Opacity(1f - eased))
                        outgoing.Draw(from);
                    using (Now.Opacity(eased))
                        incoming.Draw(to);
                    return this;

                case NowTextTransitionKind.Roll when TryDrawRoll(from, to, eased, travel):
                    return this;

                default:
                {
                    float direction = transition.kind == NowTextTransitionKind.SlideDown ? 1f : -1f;
                    outgoing.rect.y += direction * travel * eased;
                    incoming.rect.y -= direction * travel * (1f - eased);

                    using (Now.Opacity(1f - eased))
                        outgoing.Draw(from);
                    using (Now.Opacity(eased))
                        incoming.Draw(to);
                    return this;
                }
            }
        }

        /// <summary>
        /// Draws <paramref name="value"/> and animates with <paramref name="transition"/>
        /// whenever it differs from the value drawn at this call site last time, such
        /// as a counter, a price or a clock. <paramref name="time"/> is the caller's
        /// clock (for example <c>Time.unscaledTime</c>); repaints are requested while a
        /// transition runs. Use the <see cref="NowId"/> overload when one call site
        /// draws several values, such as in a loop.
        /// <code>
        /// Now.Text(scoreRect).SetAlign(NowTextAlign.Right).DrawValue(score.ToString(), NowTextTransition.Roll(), Time.unscaledTime);
        /// </code>
        /// </summary>
        public NowText DrawValue(
            string value,
            NowTextTransition transition,
            float time,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            var id = NowControls.ResolveCallSite(NowControls.SiteToken(file, line), NowIdDomain.State);
            return DrawValue(id, value, transition, time);
        }

        /// <summary>Like <see cref="DrawValue(string, NowTextTransition, float, string, int)"/>, keyed by an explicit id.</summary>
        public NowText DrawValue(NowId id, string value, NowTextTransition transition, float time)
        {
            return DrawValue(NowControls.ResolveScopedId(NowIdDomain.State, id), value, transition, time);
        }

        NowText DrawValue(NowResolvedId id, string value, NowTextTransition transition, float time)
        {
            ref var state = ref NowControlState.Get<ValueTransitionState>(id);

            if (!state.initialized)
            {
                state.initialized = true;
                state.current = value;
                state.previous = null;
                state.changedAt = float.NegativeInfinity;
            }
            else if (!string.Equals(state.current, value))
            {
                state.previous = state.current;
                state.current = value;
                state.changedAt = time;
            }

            // A clock that moved backwards (a replayed capture) shows the settled value.
            float progress = transition.duration > 0f && time >= state.changedAt
                ? (time - state.changedAt) / transition.duration
                : 1f;

            if (progress >= 1f || state.previous == null)
                return Draw(value);

            NowControlState.RequestRepaint();
            return DrawTransition(state.previous, value, progress, transition);
        }

        bool TryDrawRoll(string from, string to, float eased, float travel)
        {
            // Rolls compare character by character; anything that is not one unit per
            // UTF-16 character of equal count slides as a whole.
            if (from.Length != to.Length ||
                from.Length > RollAnimator.MaxUnits ||
                Now.CountTextUnits(this, from) != from.Length ||
                Now.CountTextUnits(this, to) != to.Length)
            {
                return false;
            }

            ulong changed = 0UL;

            for (int i = 0; i < to.Length; ++i)
            {
                if (from[i] != to[i])
                    changed |= 1UL << i;
            }

            var animator = RollAnimator.shared;
            var outgoing = this;
            var incoming = this;

            animator.Set(changed, eased, travel, incoming: false);
            outgoing.SetAnimation(NowTextAnimations.Custom(animator, Mathf.Abs(travel))).SetTime(eased).Draw(from);
            animator.Set(changed, eased, travel, incoming: true);
            incoming.SetAnimation(NowTextAnimations.Custom(animator, Mathf.Abs(travel))).SetTime(eased).Draw(to);
            return true;
        }

        /// <summary>
        /// Per-character offsets for <see cref="NowTextTransitionKind.Roll"/>. One shared
        /// instance is reconfigured before each draw, which consumes it immediately.
        /// </summary>
        sealed class RollAnimator : INowTextGlyphAnimator
        {
            public const int MaxUnits = 64;

            public static readonly RollAnimator shared = new RollAnimator();

            ulong _changed;
            float _eased;
            float _travel;
            bool _incoming;

            public void Set(ulong changed, float eased, float travel, bool incoming)
            {
                _changed = changed;
                _eased = eased;
                _travel = travel;
                _incoming = incoming;
            }

            public NowTextGlyphState Evaluate(int unitIndex, int unitCount, float time)
            {
                bool moves = unitIndex < MaxUnits && (_changed & (1UL << unitIndex)) != 0;

                if (!moves)
                {
                    // Unchanged characters are drawn once, by the incoming value.
                    return _incoming
                        ? new NowTextGlyphState(Vector2.zero)
                        : new NowTextGlyphState(Vector2.zero, 1f, 0f);
                }

                return _incoming
                    ? new NowTextGlyphState(new Vector2(0f, _travel * (1f - _eased)), 1f, _eased)
                    : new NowTextGlyphState(new Vector2(0f, -_travel * _eased), 1f, 1f - _eased);
            }
        }
    }
}
