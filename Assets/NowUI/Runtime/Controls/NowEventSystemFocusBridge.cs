using UnityEngine;

#if NOWUI_UGUI
using UnityEngine.EventSystems;
#endif

namespace NowUI
{
    internal static class NowEventSystemFocusBridge
    {
        public static void SynchronizeFocus(int hostId)
        {
#if NOWUI_UGUI
            EventSystem eventSystem = EventSystem.current;

            if (!TrySelectOwningProxy(hostId, eventSystem) &&
                eventSystem != null &&
                eventSystem.currentSelectedGameObject != null &&
                !NowFocus.IsOwningProxySelection(hostId, eventSystem.currentSelectedGameObject))
            {
                eventSystem.SetSelectedGameObject(null);
            }
#endif
        }

        public static bool IsOwningProxySelected(INowFocusNavigationProxy proxy)
        {
#if NOWUI_UGUI
            EventSystem eventSystem = EventSystem.current;
            return proxy != null &&
                eventSystem != null &&
                eventSystem.currentSelectedGameObject == proxy.owningSelection;
#else
            return false;
#endif
        }

        public static bool HasForeignSelection(INowFocusNavigationProxy proxy)
        {
#if NOWUI_UGUI
            EventSystem eventSystem = EventSystem.current;
            GameObject selection = eventSystem != null
                ? eventSystem.currentSelectedGameObject
                : null;
            return selection != null &&
                (proxy == null || selection != proxy.owningSelection);
#else
            return false;
#endif
        }

#if NOWUI_UGUI
        static bool TrySelectOwningProxy(int hostId, EventSystem eventSystem)
        {
            INowFocusNavigationProxy proxy = NowFocus.GetHostProxy(hostId);

            if (proxy == null || !proxy.isActiveAndInteractable)
                return false;

            if (eventSystem == null ||
                eventSystem.currentSelectedGameObject == proxy.owningSelection)
            {
                if (eventSystem == null)
                    proxy.RequestSelection();

                return true;
            }

            // Unity rejects SetSelectedGameObject while dispatching another
            // selection callback. The in-flight proxy OnSelect already owns the
            // handoff; otherwise the next host pass will reject a foreign
            // selection without making a reentrant EventSystem call.
            if (eventSystem.alreadySelecting)
            {
                proxy.RequestSelection();
                return true;
            }

            eventSystem.SetSelectedGameObject(proxy.owningSelection);
            return eventSystem.currentSelectedGameObject == proxy.owningSelection;
        }
#endif
    }
}
