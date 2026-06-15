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
        const float ItemHeight = 26f;
        const float PaddingX = 14f;
        const float MinWidth = 120f;

        static int _openId;
        static Vector2 _position;
        static int _activeId;
        static NowRect _popupRect;
        static int _popupPendingId;
        static readonly List<string> _items = new List<string>(8);

        /// <summary>True while any context menu is open.</summary>
        public static bool isOpen => _openId != 0;

        public static void Open(int id, Vector2 position)
        {
            _openId = id;
            _position = position;
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
                NowControlState.Get<int>(NowInput.GetId(id, "ctx-pending")) == 0)
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
            ref int pending = ref NowControlState.Get<int>(NowInput.GetId(_activeId, "ctx-pending"));

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

            var textStyle = NowTheme.themeAsset.ResolveText(NowTextStyle.Body);
            float width = MinWidth;

            for (int i = 0; i < _items.Count; ++i)
                width = Mathf.Max(width, textStyle.Measure(_items[i]).x + PaddingX * 2f);

            var popupRect = new NowRect(_position.x, _position.y, width, _items.Count * ItemHeight + 8f);
            int pendingId = NowInput.GetId(id, "ctx-pending");
            _popupRect = popupRect;
            _popupPendingId = pendingId;

            NowControlState.RequestRepaint();
            NowOverlay.Block(new NowRect(-100000f, -100000f, 200000f, 200000f));
            NowOverlay.Defer(popupRect, id, DrawDeferred);
        }

        static void DrawDeferred(int id)
        {
            if (_openId != id)
                return;

            var theme = NowTheme.themeAsset;
            var popupRect = _popupRect;
            int pendingId = _popupPendingId;

            var background = theme.Rectangle(popupRect, NowRectangleStyle.Surface);
            background.radius = new Vector4(6f, 6f, 6f, 6f);
            background.outline = 1f;
            background.outlineColor = theme.GetColor(NowColorToken.Border, Color.gray);
            background.Draw();

            for (int i = 0; i < _items.Count; ++i)
            {
                var itemRect = new NowRect(
                    popupRect.x + 4f,
                    popupRect.y + 4f + i * ItemHeight,
                    popupRect.width - 8f,
                    ItemHeight);
                var interaction = NowInput.Interact(NowInput.CombineId(pendingId, i + 1), itemRect);

                if (interaction.hovered)
                {
                    var highlight = theme.Rectangle(itemRect, NowRectangleStyle.Muted);
                    highlight.radius = new Vector4(4f, 4f, 4f, 4f);
                    highlight.color = NowControls.StateTint(highlight.color, 1f, interaction.held);
                    highlight.Draw();
                }

                NowControls.DrawLeftLabel(theme, itemRect.Inset(PaddingX * 0.7f, 0f, 4f, 0f), _items[i], NowTextStyle.Body);

                if (interaction.clicked)
                {
                    NowControlState.Get<int>(pendingId) = i + 1;
                    Close();
                }
            }

            var snapshot = NowInput.current;
            bool pressed = snapshot.primaryPressed ||
                (snapshot.pointerButtonsPressed & NowPointerButtons.Secondary) != 0;

            if ((pressed && !popupRect.Contains(snapshot.pointerPosition)) ||
                snapshot.cancelPressed ||
                snapshot.scrollDelta != Vector2.zero)
            {
                Close();
            }
        }

        public static void Reset()
        {
            _openId = 0;
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
