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
        static readonly List<Entry> _items = new List<Entry>(8);

        enum EntryKind
        {
            Item,
            Label,
            Separator
        }

        struct Entry
        {
            public EntryKind kind;
            public string label;
            public bool enabled;
            public bool selected;
        }

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
            return Item(label, true, false);
        }

        /// <summary>Adds an item; true when it was clicked (the frame after the click).</summary>
        public static bool Item(string label, bool enabled, bool selected = false)
        {
            _items.Add(new Entry
            {
                kind = EntryKind.Item,
                label = label ?? string.Empty,
                enabled = enabled,
                selected = selected
            });

            int index = _items.Count;
            ref int pending = ref NowControlState.Get<int>(_activeId, "ctx-pending");

            if (pending == index)
            {
                pending = 0;
                return enabled;
            }

            return false;
        }

        /// <summary>Adds a non-interactive label row.</summary>
        public static void Label(string label)
        {
            _items.Add(new Entry
            {
                kind = EntryKind.Label,
                label = label ?? string.Empty,
                enabled = false,
                selected = false
            });
        }

        /// <summary>Adds a separator row.</summary>
        public static void Separator()
        {
            _items.Add(new Entry
            {
                kind = EntryKind.Separator,
                label = string.Empty,
                enabled = false,
                selected = false
            });
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

            float height = popupPadding * 2f;

            for (int i = 0; i < _items.Count; ++i)
            {
                var item = _items[i];

                if (item.kind != EntryKind.Separator)
                    width = Mathf.Max(width, textStyle.Measure(item.label).x + paddingX * 2f);

                height += EntryHeight(item, itemHeight);
            }

            popupRect = new NowRect(
                _position.x,
                _position.y,
                width,
                height);

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

            float y = popupRect.y + popupPadding;

            for (int i = 0; i < _items.Count; ++i)
            {
                var item = _items[i];
                float height = EntryHeight(item, itemHeight);
                var itemRect = new NowRect(
                    popupRect.x + popupPadding,
                    y,
                    popupRect.width - popupPadding * 2f,
                    height);

                DrawEntry(theme, pendingId, i, item, itemRect);
                y += height;
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

        static void DrawEntry(NowThemeAsset theme, int pendingId, int index, Entry item, NowRect itemRect)
        {
            if (item.kind == EntryKind.Separator)
            {
                Color border = theme.GetColor(NowColorToken.Border, Color.gray);
                border.a *= 0.72f;

                Now.Rectangle(new NowRect(
                        itemRect.x + theme.controlStyles.contextMenuPaddingX * 0.5f,
                        itemRect.y + itemRect.height * 0.5f,
                        Mathf.Max(0f, itemRect.width - theme.controlStyles.contextMenuPaddingX),
                        1f))
                    .SetColor(border)
                    .Draw();

                return;
            }

            if (item.kind == EntryKind.Label)
            {
                NowControls.DrawLeftLabel(
                    theme,
                    itemRect.Inset(theme.controlStyles.contextMenuPaddingX * 0.7f, 0f, 4f, 0f),
                    item.label,
                    NowTextStyle.Muted);

                return;
            }

            var interaction = item.enabled
                ? NowInput.Interact(NowInput.CombineId(pendingId, index + 1), itemRect)
                : default;

            if (item.selected)
            {
                var accent = theme.GetColor(NowColorToken.Accent, Color.blue);

                Now.Rectangle(new NowRect(itemRect.x + 3f, itemRect.y + 5f, 3f, Mathf.Max(0f, itemRect.height - 10f)))
                    .SetColor(accent)
                    .SetRadius(2f)
                    .Draw();
            }

            if (item.enabled)
            {
                theme.controlRenderer.DrawContextMenuItem(new NowPopupItemRenderContext(
                    theme,
                    itemRect,
                    item.label,
                    item.selected,
                    interaction));
            }
            else
            {
                Color muted = theme.GetColor(NowColorToken.TextMuted, Color.gray);
                muted.a *= 0.62f;

                NowControls.DrawLeftLabel(
                    theme,
                    itemRect.Inset(theme.controlStyles.contextMenuPaddingX * 0.7f, 0f, 4f, 0f),
                    item.label,
                    NowTextStyle.Body,
                    muted);
            }

            if (interaction.clicked && item.enabled)
            {
                NowControlState.Get<int>(pendingId) = index + 1;
                Close();
            }
        }

        static float EntryHeight(Entry item, float itemHeight)
        {
            if (item.kind == EntryKind.Separator)
                return Mathf.Max(6f, itemHeight * 0.35f);

            return itemHeight;
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
