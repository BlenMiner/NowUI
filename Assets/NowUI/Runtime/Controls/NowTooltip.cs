using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Attach a tooltip to any rect: shows after the themed hover delay on
    /// desktop, or after a long press on touch. Draws as a passive overlay that
    /// never blocks the pointer. All timing comes from the input snapshot's
    /// caller-supplied time — no hidden clock.
    /// <code>
    /// var rect = NowLayout.ReserveRect(width: 120f, height: 36f);
    /// bool clicked = Now.Button(rect, "Delete").Draw();
    /// NowTooltip.For(rect, "Remove the selected item");
    /// </code>
    /// </summary>
    public static class NowTooltip
    {
        struct HoverState
        {
            public NowResolvedId id;
            public float enterTime;
            public Vector2 enterPosition;
            public bool pressed;
        }

        struct ActiveTooltip
        {
            public NowThemeAsset themeAsset;
            public NowRect rect;
            public string text;
        }

        const float StationaryEpsilonSqr = 9f;

        static HoverState _hover;

        static ActiveTooltip _active;

        /// <summary>Tooltip identity from the call site.</summary>
        public static void For(
            NowRect rect,
            string text,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            For(NowControls.GetControlId(default, NowControls.SiteToken(file, line)), rect, text);
        }

        /// <summary>Tooltip for a control scope opened with Begin().</summary>
        public static void For(in NowControlScope scope, string text)
        {
            For(scope.interaction.GetId("tooltip"), scope.rect, text);
        }

        /// <summary>Tooltip with an explicit id, for looped/data-driven rects.</summary>
        public static void For(NowResolvedId id, NowRect rect, string text)
        {
            if (!id.hasValue)
                throw new ArgumentException("A resolved tooltip id is required.", nameof(id));

            if (string.IsNullOrEmpty(text) || NowInput.isPassive)
                return;

            var snapshot = NowInput.current;
            bool hovered = NowInput.IsHovered(rect);

            if (!hovered)
            {
                if (_hover.id == id)
                    _hover = default;

                return;
            }

            var styles = NowTheme.themeAsset.controlStyles;
            bool held = snapshot.primaryDown;

            if (_hover.id != id ||
                _hover.pressed != held ||
                (!held && (snapshot.pointerPosition - _hover.enterPosition).sqrMagnitude > StationaryEpsilonSqr))
            {
                _hover = new HoverState
                {
                    id = id,
                    enterTime = snapshot.time,
                    enterPosition = snapshot.pointerPosition,
                    pressed = held
                };
            }

            float delay = held ? styles.tooltipLongPressDelay : styles.tooltipDelay;

            if (snapshot.time - _hover.enterTime < delay)
            {
                NowControlState.RequestRepaint();
                return;
            }

            Show(id, NowTheme.themeAsset, rect, text);
        }

        [Obsolete("Raw integer tooltip identities were removed. Use For(NowResolvedId, ...).", true)]
        public static void For(int id, NowRect rect, string text)
        {
            For(NowResolvedId.FromLegacy(id), rect, text);
        }

        static void Show(NowResolvedId id, NowThemeAsset theme, NowRect anchor, string text)
        {
            var styles = theme.controlStyles;
            Vector2 size = theme.controlRenderer.MeasureTooltip(theme, text);

            var rect = new NowRect(
                anchor.x + (anchor.width - size.x) * 0.5f,
                anchor.y - size.y - styles.tooltipGap,
                size.x,
                size.y);

            if (rect.y < 0f)
                rect.y = anchor.yMax + styles.tooltipGap;

            rect = NowOverlay.FitToView(rect);

            _active = new ActiveTooltip
            {
                themeAsset = theme,
                rect = rect,
                text = text
            };

            NowOverlay.DeferPassive(id, 0, DrawActive);
        }

        static void DrawActive(int state)
        {
            if (_active.themeAsset == null)
                return;

            _active.themeAsset.controlRenderer.DrawTooltip(
                new NowTooltipRenderContext(_active.themeAsset, _active.rect, _active.text));
        }

        public static void Reset()
        {
            _hover = default;
            _active = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
