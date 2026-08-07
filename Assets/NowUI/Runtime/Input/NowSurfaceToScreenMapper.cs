using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Optional input-provider capability for mapping a point on the active
    /// NowUI surface to player-window pixels. Both coordinate systems use a
    /// top-left origin. Text editors use this to place the platform IME
    /// candidate window on non-screen surfaces.
    /// </summary>
    public interface INowSurfaceToScreenMapper
    {
        /// <summary>
        /// Maps <paramref name="surfacePosition"/> to upper-left-origin screen
        /// pixels. Return false when the surface is not currently visible on a
        /// player display or its projection is unavailable.
        /// </summary>
        bool TrySurfaceToScreen(
            NowInputSurface surface,
            Vector2 surfacePosition,
            out Vector2 screenPosition);
    }

    static class NowSurfaceToScreenMapper
    {
        internal static bool TryResolveCompositionCursor(
            Vector2 localPosition,
            out Vector2 screenPosition)
        {
            screenPosition = localPosition;

            if (!IsFinite(localPosition))
                return false;

            // IMGUI already reports the coordinate convention expected by its
            // native event/IME path. Keep that established behavior unchanged.
            if (!NowInput.hasContext || NowInput.currentProvider is NowIMGUIInputProvider)
                return true;

            Vector2 surfacePosition = Now.TransformScreenPoint(localPosition);

            if (!IsFinite(surfacePosition))
                return false;

            if (NowInput.currentProvider is INowSurfaceToScreenMapper mapper)
            {
                return mapper.TrySurfaceToScreen(
                    NowInput.surface,
                    surfacePosition,
                    out screenPosition) &&
                    IsFinite(screenPosition);
            }

            return TryMapSurfaceRect(
                NowInput.surface,
                surfacePosition,
                out screenPosition);
        }

        internal static bool TryMapSurfaceRect(
            NowInputSurface surface,
            Vector2 surfacePosition,
            out Vector2 screenPosition)
        {
            Rect screenRect = surface.screenRect;

            if (!IsFinite(surfacePosition) ||
                !IsFinite(surface.size) ||
                !IsFinite(screenRect) ||
                surface.size.x <= 0f ||
                surface.size.y <= 0f ||
                screenRect.width <= 0f ||
                screenRect.height <= 0f)
            {
                screenPosition = default;
                return false;
            }

            screenPosition = new Vector2(
                screenRect.x + surfacePosition.x * screenRect.width / surface.size.x,
                screenRect.y + surfacePosition.y * screenRect.height / surface.size.y);
            return IsFinite(screenPosition);
        }

        internal static Vector2 BottomLeftToTopLeft(Vector2 position)
        {
            return new Vector2(position.x, Screen.height - position.y);
        }

        internal static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        internal static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(Rect value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.width) &&
                   IsFinite(value.height);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
