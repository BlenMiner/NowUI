using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Display-density and safe-area helpers for sizing UI on phones and tablets.
    /// Pair with <see cref="Now.StartUI(float)"/>:
    /// <code>
    /// using (Now.StartUI(NowScreen.recommendedUIScale))
    /// {
    ///     NowRect safe = NowScreen.safeArea;
    /// }
    /// </code>
    /// </summary>
    public static class NowScreen
    {
        const float DefaultReferenceDpi = 160f;

        static float _referenceDpi = DefaultReferenceDpi;

        /// <summary>
        /// The dpi at which one UI unit equals one pixel. 160 matches Android's
        /// density-independent pixel and keeps iOS points close to 1:1.
        /// </summary>
        public static float referenceDpi
        {
            get => _referenceDpi;
            set => _referenceDpi = Mathf.Max(1f, value);
        }

        /// <summary>
        /// The display dpi, falling back to <see cref="referenceDpi"/> on platforms
        /// where Unity cannot report it (then <see cref="recommendedUIScale"/> is 1).
        /// </summary>
        public static float dpi
        {
            get
            {
                float value = Screen.dpi;
                return value > 0f ? value : _referenceDpi;
            }
        }

        /// <summary>
        /// Density-based scale for <see cref="Now.StartUI(float)"/>: dpi divided by
        /// <see cref="referenceDpi"/>, never below 1 so UI never shrinks under
        /// low-dpi desktop monitors.
        /// </summary>
        public static float recommendedUIScale => Mathf.Max(1f, dpi / _referenceDpi);

        /// <summary>
        /// <see cref="Screen.safeArea"/> converted into the current frame's UI
        /// coordinates: top-left origin, divided by <see cref="Now.uiScale"/>. Use it
        /// as the root layout rect to stay clear of notches and rounded corners.
        /// </summary>
        public static NowRect safeArea
        {
            get
            {
                Rect area = Screen.safeArea;
                float scale = Now.uiScale;

                return new NowRect(
                    area.x / scale,
                    (Screen.height - area.yMax) / scale,
                    area.width / scale,
                    area.height / scale);
            }
        }

        /// <summary>
        /// Resets user-configurable state; mirrors the other NowUI domain-reload hooks.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            _referenceDpi = DefaultReferenceDpi;
        }
    }
}
