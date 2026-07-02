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
    ///     if (NowContextMenu.BeginSubmenu("Create"))
    ///     {
    ///         if (NowContextMenu.Item("Node")) CreateNode();
    ///         NowContextMenu.EndSubmenu();
    ///     }
    ///     NowContextMenu.End();
    /// }
    /// </code>
    /// One root menu is open at a time and it is modal: everything beneath is
    /// pointer-blocked so the anchor position stays meaningful, and it closes
    /// on selection, press outside, cancel, or an attempted scroll.
    /// </summary>
    public static class NowContextMenu
    {
        const float MinimumMenuWidth = 160f;
        const float SubmenuGap = 2f;

        const float HoverIntentDelay = 0.18f;

        static int _openId;
        static Vector2 _position;
        static bool _fitToView = true;
        static int _activeId;
        static int _pendingPathStateId;
        static int _menuCount;
        static int _hoverIntentDepth = -1;
        static int _hoverIntentPath;
        static float _hoverIntentStart;

        static readonly List<Menu> _menus = new List<Menu>(4);
        static readonly List<int> _buildStack = new List<int>(4);
        static readonly List<int> _openPath = new List<int>(4);
        static readonly List<int> _pendingOpenPath = new List<int>(4);

        enum EntryKind
        {
            Item,
            Label,
            Separator,
            Submenu
        }

        struct Entry
        {
            public EntryKind kind;
            public string label;
            public bool enabled;
            public bool selected;
            public int pathId;
            public int localIndex;
            public int childMenu;
        }

        sealed class Menu
        {
            public int rootId;
            public int pathId;
            public int overlayId;
            public int parentMenu;
            public int depth;
            public int itemSeed;
            public float width;
            public float height;
            public NowRect popupRect;
            public readonly List<Entry> entries = new List<Entry>(8);

            public void Reset(int rootId, int pathId, int overlayId, int parentMenu, int depth)
            {
                this.rootId = rootId;
                this.pathId = pathId;
                this.overlayId = overlayId;
                this.parentMenu = parentMenu;
                this.depth = depth;
                itemSeed = NowInput.GetId(overlayId, "ctx-item");
                width = 0f;
                height = 0f;
                popupRect = default;
                entries.Clear();
            }
        }

        /// <summary>True while any context menu is open.</summary>
        public static bool isOpen => _openId != 0;

        public static void Open(int id, Vector2 position, bool fitToView = true)
        {
            _openId = id;
            _position = position;
            _fitToView = fitToView;
            _openPath.Clear();
            _pendingOpenPath.Clear();
            ClearHoverIntent();
            NowControlState.RequestRepaint();
        }

        public static void Close()
        {
            _openId = 0;
            ClearHoverIntent();
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

            int pendingStateId = NowInput.GetId(id, "ctx-pending-path");

            if (_openId != id && NowControlState.Get<int>(pendingStateId) == 0)
                return false;

            _activeId = id;
            _pendingPathStateId = pendingStateId;
            _menuCount = 0;
            _buildStack.Clear();

            int rootIndex = AddMenu(id, id, id, -1, 0);
            _buildStack.Add(rootIndex);
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
            if (_activeId == 0 || _buildStack.Count == 0)
                return false;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;
            int pathId = NowInput.CombineId(menu.pathId, localIndex + 1);

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Item,
                label = label ?? string.Empty,
                enabled = enabled,
                selected = selected,
                pathId = pathId,
                localIndex = localIndex,
                childMenu = -1
            });

            ref int pending = ref NowControlState.Get<int>(_pendingPathStateId);

            if (pending == pathId)
            {
                pending = 0;
                _pendingOpenPath.Clear();
                return enabled;
            }

            return false;
        }

        /// <summary>Adds a submenu row; true while that submenu should declare its children.</summary>
        public static bool BeginSubmenu(string label)
        {
            return BeginSubmenu(label, true, false);
        }

        /// <summary>Adds a submenu row; true while that submenu should declare its children.</summary>
        public static bool BeginSubmenu(string label, bool enabled, bool selected = false)
        {
            if (_activeId == 0 || _buildStack.Count == 0)
                return false;

            int parentIndex = _buildStack[_buildStack.Count - 1];
            var parent = _menus[parentIndex];
            int localIndex = parent.entries.Count;
            int pathId = NowInput.CombineId(parent.pathId, localIndex + 1);
            int childMenu = AddMenu(
                _activeId,
                pathId,
                NowInput.CombineId(_activeId, pathId),
                parentIndex,
                parent.depth + 1);

            parent.entries.Add(new Entry
            {
                kind = EntryKind.Submenu,
                label = label ?? string.Empty,
                enabled = enabled,
                selected = selected,
                pathId = pathId,
                localIndex = localIndex,
                childMenu = childMenu
            });

            bool open = enabled &&
                (IsPathOpen(parent.depth, pathId) || IsPendingPathOpen(parent.depth, pathId));

            if (!open)
                return false;

            _buildStack.Add(childMenu);
            return true;
        }

        /// <summary>Ends the current submenu declaration.</summary>
        public static void EndSubmenu()
        {
            if (_buildStack.Count > 1)
                _buildStack.RemoveAt(_buildStack.Count - 1);
        }

        /// <summary>Adds a non-interactive label row.</summary>
        public static void Label(string label)
        {
            if (_activeId == 0 || _buildStack.Count == 0)
                return;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Label,
                label = label ?? string.Empty,
                enabled = false,
                selected = false,
                pathId = NowInput.CombineId(menu.pathId, localIndex + 1),
                localIndex = localIndex,
                childMenu = -1
            });
        }

        /// <summary>Adds a separator row.</summary>
        public static void Separator()
        {
            if (_activeId == 0 || _buildStack.Count == 0)
                return;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Separator,
                label = string.Empty,
                enabled = false,
                selected = false,
                pathId = NowInput.CombineId(menu.pathId, localIndex + 1),
                localIndex = localIndex,
                childMenu = -1
            });
        }

        public static void End()
        {
            if (_activeId == 0)
                return;

            int id = _activeId;
            _activeId = 0;
            _buildStack.Clear();

            if (_openId != id)
                return;

            var root = _menus[0];

            if (root.entries.Count == 0)
            {
                Close();
                return;
            }

            var theme = NowTheme.themeAsset;

            for (int i = 0; i < _menuCount; ++i)
                MeasureMenu(_menus[i], theme);

            root.popupRect = new NowRect(_position.x, _position.y, root.width, root.height);

            if (_fitToView)
                root.popupRect = NowOverlay.FitScreenToView(root.popupRect);

            NowControlState.RequestRepaint();
            NowOverlay.BlockAllSurfaces(root.overlayId);
            NowOverlay.DeferScreen(root.popupRect, root.overlayId, DrawDeferred);
        }

        static void DrawDeferred(int overlayId)
        {
            var menu = FindMenu(overlayId);

            if (menu == null || _openId != menu.rootId)
                return;

            var theme = NowTheme.themeAsset;
            DrawMenu(theme, menu);

            if (menu.parentMenu >= 0)
                return;

            var snapshot = NowInput.current;
            bool pressed = snapshot.primaryPressed ||
                (snapshot.pointerButtonsPressed & NowPointerButtons.Secondary) != 0;

            if ((pressed && !NowOverlay.IsPointerInsideOverlayTree(menu.rootId, snapshot.pointerPosition)) ||
                snapshot.cancelPressed ||
                snapshot.scrollDelta != Vector2.zero)
            {
                Close();
            }
        }

        static void DrawMenu(NowThemeAsset theme, Menu menu)
        {
            float popupPadding = theme.controlStyles.popupPadding;
            float itemHeight = theme.controlStyles.contextMenuItemHeight;

            theme.controlRenderer.DrawPopupBackground(theme, menu.popupRect, menu: true);

            float y = menu.popupRect.y + popupPadding;

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];
                float height = EntryHeight(entry, itemHeight);
                var itemRect = new NowRect(
                    menu.popupRect.x + popupPadding,
                    y,
                    menu.popupRect.width - popupPadding * 2f,
                    height);

                DrawEntry(theme, menu, entry, itemRect);

                if (entry.kind == EntryKind.Submenu &&
                    entry.enabled &&
                    IsPathOpen(menu.depth, entry.pathId) &&
                    entry.childMenu >= 0 &&
                    entry.childMenu < _menuCount)
                {
                    var child = _menus[entry.childMenu];

                    if (child.entries.Count > 0)
                    {
                        child.popupRect = PlaceSubmenu(child, itemRect, popupPadding);
                        NowOverlay.DeferScreen(child.popupRect, child.overlayId, DrawDeferred);
                    }
                }

                y += height;
            }
        }

        static void DrawEntry(NowThemeAsset theme, Menu menu, Entry entry, NowRect itemRect)
        {
            if (entry.kind == EntryKind.Separator)
            {
                Color border = theme.GetColor(NowColorToken.Border);
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

            if (entry.kind == EntryKind.Label)
            {
                NowControls.DrawLeftLabel(
                    theme,
                    itemRect.Inset(theme.controlStyles.contextMenuPaddingX * 0.7f, 0f, 4f, 0f),
                    entry.label,
                    NowTextStyle.Muted);

                return;
            }

            var interaction = entry.enabled
                ? NowInput.Interact(NowInput.CombineId(menu.itemSeed, entry.localIndex + 1), itemRect)
                : default;
            bool submenuOpen = entry.kind == EntryKind.Submenu && IsPathOpen(menu.depth, entry.pathId);
            bool selected = entry.selected || submenuOpen;

            if (entry.selected)
            {
                var accent = theme.GetColor(NowColorToken.Accent);

                Now.Rectangle(new NowRect(itemRect.x + 3f, itemRect.y + 5f, 3f, Mathf.Max(0f, itemRect.height - 10f)))
                    .SetColor(accent)
                    .SetRadius(2f)
                    .Draw();
            }

            if (entry.enabled)
            {
                theme.controlRenderer.DrawContextMenuItem(new NowPopupItemRenderContext(
                    theme,
                    itemRect,
                    entry.label,
                    selected,
                    interaction,
                    entry.kind == EntryKind.Submenu));
            }
            else
            {
                Color muted = theme.GetColor(NowColorToken.TextMuted);
                muted.a *= 0.62f;

                NowControls.DrawLeftLabel(
                    theme,
                    itemRect.Inset(theme.controlStyles.contextMenuPaddingX * 0.7f, 0f, entry.kind == EntryKind.Submenu ? 22f : 4f, 0f),
                    entry.label,
                    NowTextStyle.Body,
                    muted);
            }

            if (entry.kind == EntryKind.Submenu)
                theme.controlRenderer.DrawContextMenuSubmenuIndicator(theme, itemRect, entry.enabled, submenuOpen);

            if (entry.enabled && interaction.hovered)
                UpdateOpenPathFromHover(menu.depth, entry.kind == EntryKind.Submenu ? entry.pathId : 0);

            if (entry.kind != EntryKind.Item || !interaction.clicked || !entry.enabled)
                return;

            NowControlState.Get<int>(_pendingPathStateId) = entry.pathId;
            CopyOpenPathToPending(menu.depth);
            Close();
        }

        static NowRect PlaceSubmenu(Menu child, NowRect parentItemRect, float popupPadding)
        {
            var rect = new NowRect(
                parentItemRect.xMax + SubmenuGap,
                parentItemRect.y - popupPadding,
                child.width,
                child.height);

            if (_fitToView)
                rect = NowOverlay.FitScreenToView(rect);

            return rect;
        }

        static void MeasureMenu(Menu menu, NowThemeAsset theme)
        {
            var styles = theme.controlStyles;
            var textStyle = NowControls.Text(theme, NowTextStyle.Body);
            float width = Mathf.Max(MinimumMenuWidth, styles.contextMenuMinWidth);
            float paddingX = styles.contextMenuPaddingX;
            float itemHeight = styles.contextMenuItemHeight;
            float popupPadding = styles.popupPadding;
            float height = popupPadding * 2f;

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];

                if (entry.kind != EntryKind.Separator)
                {
                    float rightReserve = entry.kind == EntryKind.Submenu ? 24f : 0f;
                    width = Mathf.Max(width, textStyle.Measure(entry.label).x + paddingX * 2f + rightReserve);
                }

                height += EntryHeight(entry, itemHeight);
            }

            menu.width = width;
            menu.height = height;
        }

        static float EntryHeight(Entry entry, float itemHeight)
        {
            if (entry.kind == EntryKind.Separator)
                return Mathf.Max(6f, itemHeight * 0.35f);

            return itemHeight;
        }

        static int AddMenu(int rootId, int pathId, int overlayId, int parentMenu, int depth)
        {
            if (_menuCount >= _menus.Count)
                _menus.Add(new Menu());

            int index = _menuCount++;
            _menus[index].Reset(rootId, pathId, overlayId, parentMenu, depth);
            return index;
        }

        static Menu CurrentMenu()
        {
            return _menus[_buildStack[_buildStack.Count - 1]];
        }

        static Menu FindMenu(int overlayId)
        {
            for (int i = 0; i < _menuCount; ++i)
            {
                if (_menus[i].overlayId == overlayId)
                    return _menus[i];
            }

            return null;
        }

        static bool IsPathOpen(int depth, int pathId)
        {
            return _openPath.Count > depth && _openPath[depth] == pathId;
        }

        static bool IsPendingPathOpen(int depth, int pathId)
        {
            return _pendingOpenPath.Count > depth && _pendingOpenPath[depth] == pathId;
        }

        /// <summary>
        /// Applies hovered rows to the open-submenu path. Opening into an empty
        /// depth is immediate; switching away from an open submenu (to close it
        /// or open a sibling) waits for a short hover-intent delay so diagonal
        /// pointer paths across neighbouring rows do not snap submenus shut.
        /// Timing comes from the input snapshot's caller-supplied time.
        /// </summary>
        static void UpdateOpenPathFromHover(int depth, int desiredPathId)
        {
            bool alreadyDesired = desiredPathId != 0
                ? IsPathOpen(depth, desiredPathId)
                : _openPath.Count <= depth;

            if (alreadyDesired)
            {
                if (_hoverIntentDepth == depth)
                    ClearHoverIntent();

                return;
            }

            if (_openPath.Count <= depth && desiredPathId != 0)
            {
                SetOpenPath(depth, desiredPathId);
                ClearHoverIntent();
                return;
            }

            float time = NowInput.current.time;

            if (_hoverIntentDepth != depth || _hoverIntentPath != desiredPathId)
            {
                _hoverIntentDepth = depth;
                _hoverIntentPath = desiredPathId;
                _hoverIntentStart = time;
                NowControlState.RequestRepaint();
                return;
            }

            if (time - _hoverIntentStart >= HoverIntentDelay)
            {
                SetOpenPath(depth, desiredPathId);
                ClearHoverIntent();
                return;
            }

            NowControlState.RequestRepaint();
        }

        static void ClearHoverIntent()
        {
            _hoverIntentDepth = -1;
            _hoverIntentPath = 0;
            _hoverIntentStart = 0f;
        }

        static void SetOpenPath(int depth, int pathId)
        {
            int targetCount = pathId != 0 ? depth + 1 : depth;
            bool changed = _openPath.Count != targetCount ||
                (pathId != 0 && (_openPath.Count <= depth || _openPath[depth] != pathId));

            while (_openPath.Count > depth)
                _openPath.RemoveAt(_openPath.Count - 1);

            if (pathId != 0)
                _openPath.Add(pathId);

            if (changed)
                NowControlState.RequestRepaint();
        }

        static void CopyOpenPathToPending(int depth)
        {
            _pendingOpenPath.Clear();

            for (int i = 0; i < depth && i < _openPath.Count; ++i)
                _pendingOpenPath.Add(_openPath[i]);
        }

        public static void Reset()
        {
            _openId = 0;
            _fitToView = true;
            _activeId = 0;
            _pendingPathStateId = 0;
            _menuCount = 0;
            ClearHoverIntent();
            _buildStack.Clear();
            _openPath.Clear();
            _pendingOpenPath.Clear();

            for (int i = 0; i < _menus.Count; ++i)
                _menus[i].entries.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            Reset();
        }
    }
}
