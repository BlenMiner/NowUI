using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        static EventSystem s_eventSystem;

        static readonly List<RaycastResult> s_results = new List<RaycastResult>(16);

        static int s_cachedFrame = -1;

        static Vector2 s_cachedPosition;

        static bool s_hasCachedResults;

        /// <summary>
        /// True when the topmost EventSystem raycast hit at
        /// <paramref name="screenPosition"/> is <paramref name="host"/> or one of
        /// its children (or when there is no EventSystem / no hit at all).
        /// Requires the host's raycastTarget to be enabled to be hit itself.
        /// </summary>
        public static bool IsPointerAllowed(Component host, Vector2 screenPosition)
        {
            return IsPointerAllowed(host, screenPosition, false);
        }

        /// <summary>
        /// True when the EventSystem hit at <paramref name="screenPosition"/>
        /// belongs to <paramref name="host"/>. When
        /// <paramref name="allowHostOwnedOverlay"/> is true, UGUI hits drawn below
        /// the host are also allowed so NowUI popups can extend beyond the host
        /// rect without losing input.
        /// </summary>
        public static bool IsPointerAllowed(Component host, Vector2 screenPosition, bool allowHostOwnedOverlay)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null || host == null)
                return true;

            RaycastAll(eventSystem, screenPosition);

            if (s_results.Count == 0)
                return true;

            var topResult = s_results[0];
            return IsHostOrChild(host, topResult) ||
                (allowHostOwnedOverlay && !IsRaycastResultAboveHost(host, topResult, screenPosition));
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
        /// True when <paramref name="screenPosition"/> is over any raycastable UI
        /// element. Use this when input has already been sampled outside Unity's
        /// EventSystem pointer module.
        /// </summary>
        public static bool IsPointerOverUGUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null)
                return false;

            RaycastAll(eventSystem, screenPosition);
            return s_results.Count > 0;
        }

        /// <summary>
        /// Drops the shared same-frame raycast cache. Call after mutating the UGUI
        /// hierarchy mid-frame (tests, dynamic canvas edits) so the next gate query
        /// re-raycasts instead of reusing stale results.
        /// </summary>
        public static void InvalidateCache()
        {
            s_hasCachedResults = false;
            s_results.Clear();
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

        internal static bool IsHostAbove(Component candidateHost, Component targetHost, Vector2 screenPosition)
        {
            if (candidateHost == null || targetHost == null || candidateHost == targetHost)
                return false;

            var candidate = ResolveGraphic(candidateHost);
            var target = ResolveGraphic(targetHost);

            if (candidate == null || target == null)
                return false;

            var candidateCanvas = candidate.canvas;
            var targetCanvas = target.canvas;

            if (candidateCanvas == null || targetCanvas == null)
                return false;

            int candidateLayer = SortingLayer.GetLayerValueFromID(candidateCanvas.sortingLayerID);
            int targetLayer = SortingLayer.GetLayerValueFromID(targetCanvas.sortingLayerID);

            if (candidateLayer != targetLayer)
                return candidateLayer > targetLayer;

            if (candidateCanvas.sortingOrder != targetCanvas.sortingOrder)
                return candidateCanvas.sortingOrder > targetCanvas.sortingOrder;

            if (candidateCanvas.rootCanvas == targetCanvas.rootCanvas)
                return candidate.depth > target.depth;

            if (TryGraphicRayDistance(candidate, screenPosition, out float candidateDistance) &&
                TryGraphicRayDistance(target, screenPosition, out float targetDistance))
            {
                const float epsilon = 0.001f;
                return candidateDistance < targetDistance - epsilon;
            }

            return false;
        }

        /// <summary>
        /// Raycasts through the EventSystem at most once per (frame, position):
        /// every host provider queries the same pointer sample each frame, so the
        /// results are shared instead of re-raycasting per host.
        /// </summary>
        static void RaycastAll(EventSystem eventSystem, Vector2 screenPosition)
        {
            if (s_pointerData == null || s_eventSystem != eventSystem)
            {
                s_pointerData = new PointerEventData(eventSystem);
                s_eventSystem = eventSystem;
                s_hasCachedResults = false;
            }

            int frame = Time.frameCount;

            if (s_hasCachedResults &&
                s_cachedFrame == frame &&
                (s_cachedPosition - screenPosition).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            s_pointerData.position = screenPosition;

            s_results.Clear();
            eventSystem.RaycastAll(s_pointerData, s_results);
            s_cachedFrame = frame;
            s_cachedPosition = screenPosition;
            s_hasCachedResults = true;
        }

        static bool IsHostOrChild(Component host, RaycastResult result)
        {
            if (result.gameObject == null)
                return false;

            var top = result.gameObject.transform;
            var root = host.transform;
            return top == root || top.IsChildOf(root);
        }

        static bool IsRaycastResultAboveHost(Component host, RaycastResult result, Vector2 screenPosition)
        {
            if (host == null || result.gameObject == null)
                return true;

            var hostGraphic = ResolveGraphic(host);
            if (hostGraphic == null)
                return true;

            var hostCanvas = hostGraphic.canvas;

            if (hostCanvas == null)
                return true;

            var hostRaycaster = hostCanvas.GetComponent<BaseRaycaster>();

            if (result.module != null && hostRaycaster != null && result.module != hostRaycaster)
            {
                if (result.module.sortOrderPriority != hostRaycaster.sortOrderPriority)
                    return result.module.sortOrderPriority > hostRaycaster.sortOrderPriority;

                if (result.module.renderOrderPriority != hostRaycaster.renderOrderPriority)
                    return result.module.renderOrderPriority > hostRaycaster.renderOrderPriority;
            }

            var hitGraphic = result.gameObject.GetComponent<Graphic>();
            var hitCanvas = hitGraphic != null ? hitGraphic.canvas : null;

            if (hitCanvas != null)
            {
                int hitLayer = SortingLayer.GetLayerValueFromID(hitCanvas.sortingLayerID);
                int hostLayer = SortingLayer.GetLayerValueFromID(hostCanvas.sortingLayerID);

                if (hitLayer != hostLayer)
                    return hitLayer > hostLayer;

                if (hitCanvas.sortingOrder != hostCanvas.sortingOrder)
                    return hitCanvas.sortingOrder > hostCanvas.sortingOrder;

                if (hitCanvas.rootCanvas == hostCanvas.rootCanvas)
                    return result.depth > hostGraphic.depth;
            }

            if (TryRaycastResultInFrontOfHost(hostGraphic, result, screenPosition, out bool inFront))
                return inFront;

            return true;
        }

        static bool TryRaycastResultInFrontOfHost(
            Graphic hostGraphic,
            RaycastResult result,
            Vector2 screenPosition,
            out bool inFront)
        {
            inFront = true;

            var camera = result.module != null ? result.module.eventCamera : null;

            if (camera == null || hostGraphic == null)
                return false;

            var hostTransform = hostGraphic.rectTransform;
            var ray = camera.ScreenPointToRay(screenPosition);

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    hostTransform,
                    screenPosition,
                    camera,
                    out var hostWorldPosition))
            {
                return false;
            }

            float hostDistance = Vector3.Dot(hostWorldPosition - ray.origin, ray.direction);
            float resultDistance = result.distance;

            if (resultDistance <= 0f && result.worldPosition != Vector3.zero)
                resultDistance = Vector3.Dot(result.worldPosition - ray.origin, ray.direction);

            const float epsilon = 0.001f;
            inFront = resultDistance < hostDistance - epsilon;
            return hostDistance >= 0f && resultDistance >= 0f;
        }

        static Graphic ResolveGraphic(Component component)
        {
            var graphic = component as Graphic;
            return graphic != null ? graphic : component.GetComponent<Graphic>();
        }

        static bool TryGraphicRayDistance(Graphic graphic, Vector2 screenPosition, out float distance)
        {
            distance = 0f;

            if (graphic == null)
                return false;

            var canvas = graphic.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(screenPosition);

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    graphic.rectTransform,
                    screenPosition,
                    camera,
                    out var worldPosition))
            {
                return false;
            }

            distance = Vector3.Dot(worldPosition - ray.origin, ray.direction);
            return distance >= 0f;
        }
    }
}
