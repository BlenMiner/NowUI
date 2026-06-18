using UnityEngine;
using System.Collections.Generic;

namespace NowUI
{
    /// <summary>
    /// Immediate-mode context menu on the overlay layer. The owner opens it on a
    /// right-click and declares items every frame while it is open; a click on an
    /// item reports through that item's call the next frame (deferred overlay
    /// draws run after the owner returns):
    /// <code>
    /// if (selection.rightClicked)
    ///     NowContextMenu.Open(menuId, selection.rightClickPosition);
    ///
    /// if (NowContextMenu.Begin(menuId))
    /// {
    ///     if (NowContextMenu.Item("Copy")) Copy();
    ///     if (NowContextMenu.Item("Select All")) SelectAll();
    ///     NowContextMenu.End();
    /// }
    /// </code>
    /// One menu is open at a time and it is modal: everything beneath is
    /// pointer-blocked so the anchor position stays meaningful, and it closes
    /// on selection, press outside, cancel, or an attempted scroll.
    /// </summary>
    public static class NowContextMenu
    {
        static int _openId;
        static Vector2 _position;
        static bool _fitToView = true;
        static int _activeId;
        static NowRect _popupRect;
        static int _popupPendingId;
        static readonly List<string> _items = new List<string>(8);

        /// <summary>True while any context menu is open.</summary>
        public static bool isOpen => _openId != 0;

        public static void Open(int id, Vector2 position, bool fitToView = true)
        {
            _openId = id;
            _position = position;
            _fitToView = fitToView;
            NowControlState.RequestRepaint();
        }

        public static void Close()
        {
            _openId = 0;
        }

        /// <summary>
        /// True while the menu with this id is open — declare items, then call
        /// <see cref="End"/>. Also true for one frame after an item was clicked
        /// (the menu has closed by then) so the clicked item can deliver.
        /// </summary>
        public static bool Begin(int id)
        {
            if (NowInput.isPassive)
                return false;

            if (_openId != id &&
                NowControlState.Get<int>(id, "ctx-pending") == 0)
            {
                return false;
            }

            _items.Clear();
            _activeId = id;
            return true;
        }

        /// <summary>Adds an item; true when it was clicked (the frame after the click).</summary>
        public static bool Item(string label)
        {
            _items.Add(label);
            int index = _items.Count;
            ref int pending = ref NowControlState.Get<int>(_activeId, "ctx-pending");

            if (pending == index)
            {
                pending = 0;
                return true;
            }

            return false;
        }

        public static void End()
        {
            if (_activeId == 0)
                return;

            int id = _activeId;
            _activeId = 0;

            if (_openId != id)
                return;

            if (_items.Count == 0)
            {
                Close();
                return;
            }

            var theme = NowTheme.themeAsset;
            var styles = theme.controlStyles;
            NowRect popupRect;

            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float width = styles.contextMenuMinWidth;
            float paddingX = styles.contextMenuPaddingX;
            float itemHeight = styles.contextMenuItemHeight;
            float popupPadding = styles.popupPadding;

            for (int i = 0; i < _items.Count; ++i)
                width = Mathf.Max(width, textStyle.Measure(_items[i]).x + paddingX * 2f);

            popupRect = new NowRect(
                _position.x,
                _position.y,
                width,
                _items.Count * itemHeight + popupPadding * 2f);

            if (_fitToView)
                popupRect = NowOverlay.FitScreenToView(popupRect);

            int pendingId = NowInput.GetId(id, "ctx-pending");
            _popupRect = popupRect;
            _popupPendingId = pendingId;

            NowControlState.RequestRepaint();
            NowOverlay.BlockScreen(new NowRect(-100000f, -100000f, 200000f, 200000f));
            NowOverlay.DeferScreen(popupRect, id, DrawDeferred);
        }

        static void DrawDeferred(int id)
        {
            if (_openId != id)
                return;

            var theme = NowTheme.themeAsset;
            var popupRect = _popupRect;
            int pendingId = _popupPendingId;

            float popupPadding = theme.controlStyles.popupPadding;
            float itemHeight = theme.controlStyles.contextMenuItemHeight;

            theme.controlRenderer.DrawPopupBackground(theme, popupRect, menu: true);

            for (int i = 0; i < _items.Count; ++i)
            {
                var itemRect = new NowRect(
                    popupRect.x + popupPadding,
                    popupRect.y + popupPadding + i * itemHeight,
                    popupRect.width - popupPadding * 2f,
                    itemHeight);
                var interaction = NowInput.Interact(NowInput.CombineId(pendingId, i + 1), itemRect);

                theme.controlRenderer.DrawContextMenuItem(new NowPopupItemRenderContext(
                    theme,
                    itemRect,
                    _items[i],
                    false,
                    interaction));

                if (interaction.clicked)
                {
                    NowControlState.Get<int>(pendingId) = i + 1;
                    Close();
                }
            }

            var snapshot = NowInput.current;
            bool pressed = snapshot.primaryPressed ||
                (snapshot.pointerButtonsPressed & NowPointerButtons.Secondary) != 0;

            if ((pressed && !NowOverlay.IsPointerInsideOverlayTree(id, snapshot.pointerPosition)) ||
                (snapshot.cancelPressed && !NowOverlay.HasNestedOverlay(id)) ||
                snapshot.scrollDelta != Vector2.zero)
            {
                Close();
            }
        }

        public static void Reset()
        {
            _openId = 0;
            _fitToView = true;
            _activeId = 0;
            _popupRect = default;
            _popupPendingId = 0;
            _items.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
