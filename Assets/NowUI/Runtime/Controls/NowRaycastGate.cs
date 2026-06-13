using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NowUI
{
    /// <summary>
    /// Lets UGUI occlude NowUI input the way NowUI's raycastTarget already
    /// occludes UGUI: providers ask the EventSystem what is under the pointer and
    /// withhold it when something other than the NowUI host is on top.
    /// </summary>
    internal static class NowRaycastGate
    {
        static PointerEventData s_pointerData;

        static readonly List<RaycastResult> s_results = new List<RaycastResult>(16);

        /// <summary>
        /// True when the topmost EventSystem raycast hit at
        /// <paramref name="screenPosition"/> is <paramref name="host"/> or one of
        /// its children (or when there is no EventSystem / no hit at all).
        /// Requires the host's raycastTarget to be enabled to be hit itself.
        /// </summary>
        public static bool IsPointerAllowed(Component host, Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null || host == null)
                return true;

            s_pointerData ??= new PointerEventData(eventSystem);
            s_pointerData.position = screenPosition;

            s_results.Clear();
            eventSystem.RaycastAll(s_pointerData, s_results);

            if (s_results.Count == 0)
                return true;

            var top = s_results[0].gameObject.transform;
            var root = host.transform;
            bool allowed = top == root || top.IsChildOf(root);
            s_results.Clear();
            return allowed;
        }

        /// <summary>
        /// True when the pointer is over any raycastable UI element — the standard
        /// "don't click through the canvas" test for the screen render path.
        /// </summary>
        public static bool IsPointerOverUGUI()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// Press-latched visibility: the gate is evaluated while idle (including the
        /// frame a press begins), and that verdict is latched for as long as buttons
        /// stay down — so a press that starts on occluding UGUI stays blocked through
        /// its release, while a drag that started on NowUI keeps tracking even when
        /// the pointer crosses occluding UGUI.
        /// </summary>
        public static bool UpdatePressGate(ref bool pressAllowed, bool buttonsWereDown, bool allowedNow)
        {
            if (!buttonsWereDown)
                pressAllowed = allowedNow;

            return pressAllowed;
        }
    }
}
