using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// An angular range for arcs and pies that names its convention at the call
    /// site. The float arc APIs take radians with zero at 3 o'clock, while
    /// gradients, <c>RotateNext</c> and CSS measure degrees from 12 o'clock;
    /// <see cref="Clock"/> uses the second convention, so an arc and a conic
    /// gradient can share the same numbers:
    /// <code>
    /// NowSdf.Scene(dial).SetGradient(a, b).SetGradientConic(0f).Arc(c, r, w, NowSweep.Clock(0f, 270f)).Draw();
    /// Now.Arc(c, r, NowSweep.Clock(0f, progress * 360f)).Draw();
    /// </code>
    /// Positive sweeps turn clockwise on screen in both conventions.
    /// </summary>
    public readonly struct NowSweep
    {
        /// <summary>Start angle in radians, zero at 3 o'clock.</summary>
        public readonly float from;

        /// <summary>Signed sweep in radians; positive turns clockwise.</summary>
        public readonly float sweep;

        NowSweep(float from, float sweep)
        {
            this.from = from;
            this.sweep = sweep;
        }

        /// <summary>Radians with zero at 3 o'clock, the convention of the float arc overloads.</summary>
        public static NowSweep Radians(float from, float sweep)
        {
            return new NowSweep(from, sweep);
        }

        /// <summary>
        /// Degrees measured clockwise from 12 o'clock, like a clock face, conic
        /// gradients and CSS: <c>Clock(0f, 90f)</c> runs from 12 to 3 o'clock.
        /// </summary>
        public static NowSweep Clock(float startDegrees, float sweepDegrees)
        {
            return new NowSweep((startDegrees - 90f) * Mathf.Deg2Rad, sweepDegrees * Mathf.Deg2Rad);
        }

        /// <summary>A whole turn starting at 12 o'clock.</summary>
        public static NowSweep FullCircle => Clock(0f, 360f);

        /// <summary>The start angle in clock degrees (clockwise from 12 o'clock).</summary>
        public float startClockDegrees => from * Mathf.Rad2Deg + 90f;

        /// <summary>The sweep in degrees.</summary>
        public float sweepDegrees => sweep * Mathf.Rad2Deg;
    }
}
