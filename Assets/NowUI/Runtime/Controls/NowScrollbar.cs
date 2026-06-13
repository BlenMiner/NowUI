using UnityEngine;

namespace NowUI
{
    public enum NowScrollbarAxis
    {
        Vertical,
        Horizontal
    }

    public struct NowScrollbarMetrics
    {
        public bool visible;
        public float maxValue;
        public float travel;
        public NowRect track;
        public NowRect thumb;
    }

    /// <summary>Reusable immediate-mode scrollbar geometry, interaction and drawing.</summary>
    public static class NowScrollbar
    {
        public static NowScrollbarMetrics Calculate(
            NowScrollbarAxis axis,
            NowRect track,
            float viewportSize,
            float contentSize,
            float value,
            float minThumbSize = 18f)
        {
            float maxValue = Mathf.Max(0f, contentSize - viewportSize);
            bool visible = maxValue > 0.5f && viewportSize > 0f && contentSize > 0f;
            float trackLength = axis == NowScrollbarAxis.Vertical ? track.height : track.width;
            float cross = axis == NowScrollbarAxis.Vertical ? track.width : track.height;
            float thumbLength = visible
                ? Mathf.Max(minThumbSize, trackLength * Mathf.Clamp01(viewportSize / contentSize))
                : trackLength;
            float travel = Mathf.Max(0f, trackLength - thumbLength);
            float normalized = maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;

            NowRect thumb = axis == NowScrollbarAxis.Vertical
                ? new NowRect(track.x, track.y + travel * normalized, cross, thumbLength)
                : new NowRect(track.x + travel * normalized, track.y, thumbLength, cross);

            return new NowScrollbarMetrics
            {
                visible = visible,
                maxValue = maxValue,
                travel = travel,
                track = track,
                thumb = thumb
            };
        }

        public static bool Interact(
            int id,
            NowScrollbarAxis axis,
            in NowScrollbarMetrics metrics,
            ref float value,
            float grabOutsetMain = 2f,
            float grabOutsetCross = 4f)
        {
            if (!metrics.visible || NowInput.isPassive)
                return false;

            NowRect grab = axis == NowScrollbarAxis.Vertical
                ? metrics.track.Outset(grabOutsetCross, grabOutsetMain)
                : metrics.track.Outset(grabOutsetMain, grabOutsetCross);
            var interaction = NowInput.Interact(id, grab);

            if (!interaction.held || metrics.travel <= 0f)
                return false;

            float pointer = axis == NowScrollbarAxis.Vertical
                ? interaction.pointerPosition.y - metrics.track.y - metrics.thumb.height * 0.5f
                : interaction.pointerPosition.x - metrics.track.x - metrics.thumb.width * 0.5f;
            float t = Mathf.Clamp01(pointer / metrics.travel);
            value = t * metrics.maxValue;
            NowControlState.RequestRepaint();
            return true;
        }

        public static void Draw(NowThemeAsset themeAsset, int id, NowScrollbarAxis axis, in NowScrollbarMetrics metrics)
        {
            if (!metrics.visible)
                return;

            float radius = axis == NowScrollbarAxis.Vertical ? metrics.track.width * 0.5f : metrics.track.height * 0.5f;
            var interaction = NowInput.Interact(id, metrics.track.Outset(4f, 2f));
            float hoverT = NowControlState.Transition(id, interaction.hovered || interaction.held);

            var track = themeAsset.Rectangle(metrics.track, NowRectangleStyle.Muted);
            track.radius = new Vector4(radius, radius, radius, radius);
            track.Draw();

            var thumb = themeAsset.Rectangle(metrics.thumb, NowRectangleStyle.Accent);
            thumb.radius = track.radius;
            thumb.color = NowControls.StateTint(thumb.color, hoverT, interaction.held);
            thumb.Draw();
        }
    }
}
