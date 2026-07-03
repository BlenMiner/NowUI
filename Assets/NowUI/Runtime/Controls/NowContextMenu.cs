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
    /// on selection, any press outside, cancel, or a scroll outside the menu.
    /// Items are keyboard-reachable: arrows move the highlight, submit
    /// activates, right/submit dives into a submenu and left backs out.
    /// Menus taller than the visible view clamp their height and scroll (mouse
    /// wheel, keyboard focus, or hovering the top/bottom edge strips) so every
    /// option stays reachable; submenus clamp and scroll independently.
    /// </summary>
    public static class NowContextMenu
    {
        const float MinimumMenuWidth = 160f;
        const float SubmenuGap = 2f;
        const float ScrollStripHeight = 16f;

        const float HoverIntentDelay = 0.18f;

        const float NavThreshold = 0.55f;
        const float NavRepeatDelay = 0.4f;
        const float NavRepeatInterval = 0.12f;
        const int NavLeftSeed = 0x43784e4c;
        const int NavRightSeed = 0x43784e52;
        const int NavUpSeed = 0x43784e55;
        const int NavDownSeed = 0x43784e44;

        static int _openId;
        static object _openSurface;
        static Vector2 _position;
        static bool _fitToView = true;
        static int _activeId;
        static int _pendingPathStateId;
        static int _menuCount;
        static int _hoverIntentDepth = -1;
        static int _hoverIntentPath;
        static float _hoverIntentStart;
        static int _highlightMenuOverlay;
        static int _highlightEntryIndex = -1;
        static bool _highlightMovedByKeyboard;
        static int _pendingHighlightMenuOverlay;
        static Vector2 _lastPointerPosition;
        static Vector2 _previousPointerPosition;
        static bool _pointerMoved;
        static bool _navLeftPulse;
        static bool _navRightPulse;
        static bool _navUpPulse;
        static bool _navDownPulse;

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
            public int deliveryId;
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
            public float contentHeight;
            public bool scrolls;

            /// <summary>
            /// Placed fresh each drawn frame (End for the root, PlaceSubmenu for
            /// children) and deliberately NOT cleared on rebuild: ancestors read
            /// last frame's rect for the occlusion test before this menu places
            /// itself, one frame late like overlay pointer blocks.
            /// </summary>
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
                contentHeight = 0f;
                scrolls = false;
                entries.Clear();
            }
        }

        /// <summary>True while any context menu is open.</summary>
        public static bool isOpen => _openId != 0;

        public static void Open(int id, Vector2 position, bool fitToView = true)
        {
            _openId = id;
            _openSurface = NowInput.currentProvider;
            _position = position;
            _fitToView = fitToView;
            _openPath.Clear();
            _pendingOpenPath.Clear();
            ClearHoverIntent();
            ClearHighlight();
            NowControlState.Get<float>(id, "ctx-scroll") = 0f;
            NowControlState.RequestRepaint();
        }

        public static void Close()
        {
            _openId = 0;
            ClearHoverIntent();
            ClearHighlight();
        }

        static void ClearHighlight()
        {
            _highlightMenuOverlay = 0;
            _highlightEntryIndex = -1;
            _highlightMovedByKeyboard = false;
            _pendingHighlightMenuOverlay = 0;
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

            if (_openSurface != null && !ReferenceEquals(_openSurface, NowInput.currentProvider))
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
            int deliveryId = ItemDeliveryId(menu, label ?? string.Empty);

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Item,
                label = label ?? string.Empty,
                enabled = enabled,
                selected = selected,
                pathId = pathId,
                deliveryId = deliveryId,
                localIndex = localIndex,
                childMenu = -1
            });

            ref int pending = ref NowControlState.Get<int>(_pendingPathStateId);

            if (pending == deliveryId)
            {
                pending = 0;
                _pendingOpenPath.Clear();
                return enabled;
            }

            return false;
        }

        /// <summary>
        /// Click-delivery identity for an item: label-based (plus an occurrence
        /// counter for duplicate labels) rather than positional. Clicks deliver
        /// one frame after the menu closes, and conditionally declared items can
        /// shift positions between the click and the delivery frame — a
        /// positional id would then hand the click to whichever item inherited
        /// the slot.
        /// </summary>
        static int ItemDeliveryId(Menu menu, string label)
        {
            int occurrence = 0;

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];

                if (entry.kind == EntryKind.Item && string.Equals(entry.label, label))
                    ++occurrence;
            }

            return NowInput.CombineId(NowInput.GetId(menu.pathId, label), occurrence);
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

            // The overlay id must not be CombineId(_activeId, pathId): for root-level
            // rows pathId is CombineId(_activeId, row + 1), and CombineId's xor mix
            // cancels to the constant row + 1 — colliding with any menu whose own id
            // is that small. A colliding FindMenu resolves the child's deferred draw
            // back to its ancestor, which re-defers the child in the same flush,
            // looping until the overlay cap trips.
            int childMenu = AddMenu(
                _activeId,
                pathId,
                NowInput.CombineId(NowInput.GetId(_activeId, "ctx-submenu"), pathId),
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

        /// <summary>
        /// Ends the declaration pass. The delivery pass (the one Begin grants
        /// after the menu closed) is the only chance to claim a pending click:
        /// anything unclaimed — the clicked item was not re-declared — is
        /// dropped here rather than left waiting to match a later layout.
        /// </summary>
        public static void End()
        {
            if (_activeId == 0)
                return;

            int id = _activeId;
            _activeId = 0;
            _buildStack.Clear();

            if (_openId != id)
            {
                NowControlState.Get<int>(_pendingPathStateId) = 0;
                _pendingOpenPath.Clear();
                return;
            }

            var root = _menus[0];

            if (root.entries.Count == 0)
            {
                Close();
                return;
            }

            var theme = NowTheme.themeAsset;

            for (int i = 0; i < _menuCount; ++i)
                MeasureMenu(_menus[i], theme);

            root.contentHeight = root.height;
            root.popupRect = new NowRect(_position.x, _position.y, root.width, root.height);

            if (_fitToView)
            {
                root.popupRect = NowOverlay.ClampScreenToView(root.popupRect);
                root.height = root.popupRect.height;
                root.scrolls = root.height < root.contentHeight - 0.5f;
            }

            NowControlState.RequestRepaint();
            NowOverlay.BlockAllSurfaces(root.overlayId);
            NowOverlay.DeferScreen(root.popupRect, root.overlayId, DrawDeferred);
        }

        static void DrawDeferred(int overlayId)
        {
            var menu = FindMenu(overlayId);

            if (menu == null || _openId != menu.rootId)
                return;

            if (menu.parentMenu < 0)
                UpdateTreeInput(menu);

            var theme = NowTheme.themeAsset;
            DrawMenu(theme, menu);

            if (menu.parentMenu >= 0)
                return;

            var snapshot = NowInput.current;
            bool pressed = snapshot.anyPointerPressed;
            bool pointerInsideTree = NowOverlay.IsPointerInsideOverlayTree(menu.rootId, snapshot.pointerPosition);

            if ((pressed && !pointerInsideTree) ||
                (snapshot.cancelPressed && !NowInput.cancelConsumed) ||
                (snapshot.scrollDelta != Vector2.zero && !pointerInsideTree))
            {
                Close();
            }
        }

        /// <summary>
        /// Per-frame tree-wide input state, sampled once while the root draws:
        /// whether the pointer actually moved (hover only retargets submenus and
        /// the highlight on real movement, so it never fights the keyboard) and
        /// the navigation pulses that drive the menu-local highlight. The menu
        /// never takes <see cref="NowFocus"/> focus — stealing it would clear
        /// selections and other focus-owned state the menu items act on — so
        /// base focus navigation is locked while the menu is open, and focus is
        /// retained so pressing a menu row (or dismissing the menu) does not
        /// clear the owner's focus and collapse the selection an item is about
        /// to act on.
        /// </summary>
        static void UpdateTreeInput(Menu root)
        {
            var snapshot = NowInput.current;
            Vector2 pointer = snapshot.pointerPosition;
            _previousPointerPosition = _lastPointerPosition;
            _pointerMoved = pointer != _lastPointerPosition;
            _lastPointerPosition = pointer;
            _navLeftPulse = NavPulse(root, NavLeftSeed, snapshot.navigation.x < -NavThreshold);
            _navRightPulse = NavPulse(root, NavRightSeed, snapshot.navigation.x > NavThreshold);
            _navUpPulse = NavPulse(root, NavUpSeed, snapshot.navigation.y > NavThreshold);
            _navDownPulse = NavPulse(root, NavDownSeed, snapshot.navigation.y < -NavThreshold);
            NowFocus.LockNavigation();
            NowFocus.RetainFocus();

            if (_highlightMenuOverlay == 0)
                return;

            var highlighted = FindMenu(_highlightMenuOverlay);

            if (highlighted == null || highlighted.rootId != _openId || !IsMenuDrawn(highlighted))
                ClearHighlight();
        }

        static bool NavPulse(Menu root, int seed, bool held)
        {
            return NowControlState.Repeat(
                NowInput.CombineId(root.rootId, seed),
                held,
                NavRepeatDelay,
                NavRepeatInterval);
        }

        static bool IsMenuDrawn(Menu menu)
        {
            return menu.parentMenu < 0 || IsPathOpen(menu.depth - 1, menu.pathId);
        }

        static void DrawMenu(NowThemeAsset theme, Menu menu)
        {
            float popupPadding = theme.controlStyles.popupPadding;

            theme.controlRenderer.DrawPopupBackground(theme, menu.popupRect, menu: true);

            if (_pendingHighlightMenuOverlay != 0 && _pendingHighlightMenuOverlay == menu.overlayId)
            {
                _pendingHighlightMenuOverlay = 0;
                SetHighlight(menu, FindSelectableEntry(menu, -1, 1));
            }

            UpdateMenuHighlightFromKeyboard(menu);
            bool occluded = PointerInsideDeeperMenu(menu);

            if (!menu.scrolls)
            {
                DrawMenuEntries(theme, menu, 0f, occluded);
                return;
            }

            float maxScroll = Mathf.Max(0f, menu.contentHeight - menu.popupRect.height);
            ref float scroll = ref NowControlState.Get<float>(menu.overlayId, "ctx-scroll");

            if (!occluded)
            {
                Vector2 wheel = NowInput.ConsumeScrollDelta(menu.popupRect);

                if (wheel.y != 0f)
                {
                    scroll -= wheel.y * theme.controlStyles.scrollWheelStep;
                    NowControlState.RequestRepaint();
                }
            }

            var itemArea = new NowRect(
                menu.popupRect.x,
                menu.popupRect.y + popupPadding,
                menu.popupRect.width,
                Mathf.Max(0f, menu.popupRect.height - popupPadding * 2f));

            if (!occluded)
                UpdateScrollStrips(theme, menu, ref scroll, maxScroll);

            ScrollHighlightIntoView(theme, menu, itemArea, ref scroll);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);

            using (Now.Mask(itemArea))
                DrawMenuEntries(theme, menu, scroll, occluded);

            DrawScrollStrips(theme, menu, scroll, maxScroll);
        }

        /// <summary>
        /// Applies up/down pulses to this menu's highlight. A menu owns the
        /// highlight after hover or keyboard placed it there; with no highlight
        /// anywhere, the deepest open menu claims it on the first pulse — down
        /// starts at the top row, up at the bottom row, and movement wraps.
        /// </summary>
        static void UpdateMenuHighlightFromKeyboard(Menu menu)
        {
            bool ownsHighlight = _highlightMenuOverlay == menu.overlayId && _highlightEntryIndex >= 0;

            if (!_navUpPulse && !_navDownPulse)
                return;

            if (!ownsHighlight)
            {
                if (_highlightMenuOverlay != 0 || HasOpenChild(menu))
                    return;

                SetHighlight(menu, FindSelectableEntry(menu, _navDownPulse ? -1 : menu.entries.Count, _navDownPulse ? 1 : -1));
                return;
            }

            SetHighlight(menu, FindSelectableEntry(menu, _highlightEntryIndex, _navDownPulse ? 1 : -1));
        }

        static void SetHighlight(Menu menu, int entryIndex)
        {
            if (entryIndex < 0)
                return;

            _highlightMenuOverlay = menu.overlayId;
            _highlightEntryIndex = entryIndex;
            _highlightMovedByKeyboard = true;
            NowControlState.RequestRepaint();
        }

        /// <summary>
        /// Next enabled item/submenu row from <paramref name="start"/> in
        /// <paramref name="direction"/>, wrapping past the ends; -1 when the
        /// menu has no selectable row.
        /// </summary>
        static int FindSelectableEntry(Menu menu, int start, int direction)
        {
            int count = menu.entries.Count;

            for (int step = 1; step <= count; ++step)
            {
                int index = start + step * direction;

                if (index >= count)
                    index -= count;

                if (index < 0)
                    index += count;

                if (index < 0 || index >= count)
                    return -1;

                var entry = menu.entries[index];

                if (entry.enabled && (entry.kind == EntryKind.Item || entry.kind == EntryKind.Submenu))
                    return index;
            }

            return -1;
        }

        static bool HasOpenChild(Menu menu)
        {
            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];

                if (entry.kind == EntryKind.Submenu &&
                    entry.enabled &&
                    IsPathOpen(menu.depth, entry.pathId) &&
                    entry.childMenu >= 0 &&
                    entry.childMenu < _menuCount &&
                    _menus[entry.childMenu].entries.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Shifts a clamped menu's scroll just enough to reveal the highlighted
        /// row after the keyboard moved it. Hover placement and free wheel
        /// scrolling never trigger it.
        /// </summary>
        static void ScrollHighlightIntoView(NowThemeAsset theme, Menu menu, NowRect itemArea, ref float scroll)
        {
            if (!_highlightMovedByKeyboard ||
                _highlightMenuOverlay != menu.overlayId ||
                _highlightEntryIndex < 0 ||
                _highlightEntryIndex >= menu.entries.Count)
            {
                return;
            }

            _highlightMovedByKeyboard = false;
            float itemHeight = theme.controlStyles.contextMenuItemHeight;
            float offset = 0f;

            for (int i = 0; i < _highlightEntryIndex; ++i)
                offset += EntryHeight(menu.entries[i], itemHeight);

            float entryHeight = EntryHeight(menu.entries[_highlightEntryIndex], itemHeight);
            float top = offset - scroll;

            if (top < 0f)
                scroll += top;
            else if (top + entryHeight > itemArea.height)
                scroll += top + entryHeight - itemArea.height;
            else
                return;

            NowControlState.RequestRepaint();
        }

        static void DrawMenuEntries(NowThemeAsset theme, Menu menu, float scroll, bool occluded)
        {
            float popupPadding = theme.controlStyles.popupPadding;
            float itemHeight = theme.controlStyles.contextMenuItemHeight;
            float visibleTop = menu.popupRect.y + popupPadding;
            float visibleBottom = menu.popupRect.yMax - popupPadding;
            float y = visibleTop - scroll;

            UpdateHoverPathBeforeDraw(menu, scroll, occluded, popupPadding, itemHeight, visibleTop, visibleBottom);

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];
                float height = EntryHeight(entry, itemHeight);
                var itemRect = new NowRect(
                    menu.popupRect.x + popupPadding,
                    y,
                    menu.popupRect.width - popupPadding * 2f,
                    height);

                bool visible = !menu.scrolls || (itemRect.yMax > visibleTop - 0.5f && itemRect.y < visibleBottom + 0.5f);

                if (visible)
                    DrawEntry(theme, menu, entry, itemRect, occluded);

                if (entry.kind == EntryKind.Submenu &&
                    entry.enabled &&
                    IsPathOpen(menu.depth, entry.pathId) &&
                    entry.childMenu >= 0 &&
                    entry.childMenu < _menuCount)
                {
                    if (!visible)
                    {
                        SetOpenPath(menu.depth, 0);
                    }
                    else
                    {
                        var child = _menus[entry.childMenu];

                        if (child.entries.Count > 0)
                        {
                            child.popupRect = PlaceSubmenu(menu, child, itemRect, popupPadding);
                            NowOverlay.DeferScreen(child.popupRect, child.overlayId, DrawDeferred);
                        }
                    }
                }

                y += height;
            }
        }

        /// <summary>
        /// Resolves hover-driven path changes before this menu queues any child
        /// overlays. Without this pre-pass, hovering a sibling submenu after an
        /// earlier row was already visited can leave the old child queued for
        /// one more frame, which feels sticky when moving back through submenu
        /// rows.
        /// </summary>
        static void UpdateHoverPathBeforeDraw(
            Menu menu,
            float scroll,
            bool occluded,
            float popupPadding,
            float itemHeight,
            float visibleTop,
            float visibleBottom)
        {
            if (occluded)
                return;

            float y = visibleTop - scroll;

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                var entry = menu.entries[i];
                float height = EntryHeight(entry, itemHeight);
                var itemRect = new NowRect(
                    menu.popupRect.x + popupPadding,
                    y,
                    menu.popupRect.width - popupPadding * 2f,
                    height);

                bool visible = !menu.scrolls || (itemRect.yMax > visibleTop - 0.5f && itemRect.y < visibleBottom + 0.5f);

                if (visible && entry.enabled && IsEntryHovered(itemRect))
                {
                    UpdateOpenPathFromHover(menu.depth, entry.kind == EntryKind.Submenu ? entry.pathId : 0);
                    return;
                }

                y += height;
            }
        }

        static bool IsEntryHovered(NowRect itemRect)
        {
            var snapshot = NowInput.current;
            return snapshot.hasPointer &&
                itemRect.Contains(snapshot.pointerPosition) &&
                Now.IsInsideAmbientMask(snapshot.pointerPosition) &&
                !NowOverlay.IsPointerBlocked(snapshot.pointerPosition);
        }

        /// <summary>
        /// True when the pointer sits inside a menu drawn above this one — open
        /// menus form a single root-to-leaf chain, so every deeper drawn menu
        /// overlaps on top. This menu's rows, wheel and scroll strips stand down
        /// there: a clamped submenu can cover its ancestors, and hover or press
        /// claims leaking to the rows beneath would retarget the open path or
        /// deliver the wrong item.
        /// </summary>
        static bool PointerInsideDeeperMenu(Menu menu)
        {
            var pointer = NowInput.current.pointerPosition;

            for (int i = 0; i < _menuCount; ++i)
            {
                var other = _menus[i];

                if (other.depth > menu.depth &&
                    other.rootId == menu.rootId &&
                    IsMenuDrawn(other) &&
                    other.popupRect.Contains(pointer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// OS-style scroll strips: hovering the top/bottom edge of an oversized
        /// menu scrolls it, so every option is reachable without a wheel.
        /// </summary>
        static void UpdateScrollStrips(NowThemeAsset theme, Menu menu, ref float scroll, float maxScroll)
        {
            if (scroll > 0f)
            {
                var topStrip = new NowRect(menu.popupRect.x, menu.popupRect.y, menu.popupRect.width, ScrollStripHeight);

                if (NowInput.IsHovered(topStrip) &&
                    NowControlState.Repeat(menu.overlayId, "ctx-scroll-up", true, 0.05f, 0.02f))
                {
                    scroll -= 9f;
                    NowControlState.RequestRepaint();
                }
            }

            if (scroll < maxScroll)
            {
                var bottomStrip = new NowRect(menu.popupRect.x, menu.popupRect.yMax - ScrollStripHeight, menu.popupRect.width, ScrollStripHeight);

                if (NowInput.IsHovered(bottomStrip) &&
                    NowControlState.Repeat(menu.overlayId, "ctx-scroll-down", true, 0.05f, 0.02f))
                {
                    scroll += 9f;
                    NowControlState.RequestRepaint();
                }
            }
        }

        /// <summary>
        /// Each strip is the popup's own rounded shape clipped to the edge band,
        /// so its silhouette matches the popup outline exactly — a plain strip
        /// rect cannot round correctly when the corner radius exceeds the band's
        /// half height.
        /// </summary>
        static void DrawScrollStrips(NowThemeAsset theme, Menu menu, float scroll, float maxScroll)
        {
            Color surface = theme.GetColor(NowColorToken.SurfaceElevated);
            Color chevron = theme.GetColor(NowColorToken.TextMuted);
            float radius = Mathf.Max(0f, theme.controlStyles.contextMenuRadius - 1f);
            var inner = new NowRect(menu.popupRect.x + 1f, menu.popupRect.y + 1f, menu.popupRect.width - 2f, menu.popupRect.height - 2f);

            if (scroll > 0f)
            {
                var strip = new NowRect(inner.x, inner.y, inner.width, ScrollStripHeight);

                using (Now.Mask(strip))
                {
                    Now.Rectangle(inner)
                        .SetColor(surface)
                        .SetRadius(radius)
                        .Draw();
                }

                DrawStripChevron(strip, chevron, up: true);
            }

            if (scroll < maxScroll)
            {
                var strip = new NowRect(inner.x, inner.yMax - ScrollStripHeight, inner.width, ScrollStripHeight);

                using (Now.Mask(strip))
                {
                    Now.Rectangle(inner)
                        .SetColor(surface)
                        .SetRadius(radius)
                        .Draw();
                }

                DrawStripChevron(strip, chevron, up: false);
            }
        }

        static void DrawStripChevron(NowRect strip, Color color, bool up)
        {
            Vector2 center = strip.center;
            float w = 5f;
            float h = 3f;
            var a = new Vector2(center.x - w, up ? center.y + h * 0.5f : center.y - h * 0.5f);
            var mid = new Vector2(center.x, up ? center.y - h * 0.5f : center.y + h * 0.5f);
            var b = new Vector2(center.x + w, up ? center.y + h * 0.5f : center.y - h * 0.5f);

            Now.Line(a, mid).SetColor(color).SetWidth(1.4f).SetCap(NowLineCap.Round).Draw();
            Now.Line(mid, b).SetColor(color).SetWidth(1.4f).SetCap(NowLineCap.Round).Draw();
        }


        static void DrawEntry(NowThemeAsset theme, Menu menu, Entry entry, NowRect itemRect, bool occluded)
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

            var interaction = entry.enabled && !occluded
                ? NowInput.Interact(NowInput.CombineId(menu.itemSeed, entry.localIndex + 1), itemRect)
                : default;

            if (entry.enabled && interaction.hovered && _pointerMoved &&
                (_highlightMenuOverlay != menu.overlayId || _highlightEntryIndex != entry.localIndex))
            {
                _highlightMenuOverlay = menu.overlayId;
                _highlightEntryIndex = entry.localIndex;
                _highlightMovedByKeyboard = false;
                NowControlState.RequestRepaint();
            }

            bool highlighted = _highlightMenuOverlay == menu.overlayId && _highlightEntryIndex == entry.localIndex;
            bool submitted = entry.enabled && highlighted && NowInput.current.submitPressed;
            bool submenuOpen = entry.kind == EntryKind.Submenu && IsPathOpen(menu.depth, entry.pathId);
            bool selected = entry.selected || submenuOpen || highlighted;

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

            if (entry.selected)
            {
                var accent = theme.GetColor(NowColorToken.Accent);

                Now.Rectangle(new NowRect(itemRect.x + 3f, itemRect.y + 5f, 3f, Mathf.Max(0f, itemRect.height - 10f)))
                    .SetColor(accent)
                    .SetRadius(2f)
                    .Draw();
            }

            if (entry.kind == EntryKind.Submenu)
                theme.controlRenderer.DrawContextMenuSubmenuIndicator(theme, itemRect, entry.enabled, submenuOpen);

            if (entry.enabled && interaction.hovered)
                UpdateOpenPathFromHover(menu.depth, entry.kind == EntryKind.Submenu ? entry.pathId : 0);

            if (entry.enabled && highlighted)
                HandleEntryKeyboard(menu, entry, submenuOpen, submitted);

            if (entry.kind != EntryKind.Item || !entry.enabled || (!interaction.clicked && !submitted))
                return;

            NowControlState.Get<int>(_pendingPathStateId) = entry.deliveryId;
            CopyOpenPathToPending(menu.depth);
            Close();
        }

        /// <summary>
        /// Keyboard driving for the highlighted row: submit or a right pulse
        /// opens a submenu and highlights its first row; a left pulse closes the
        /// containing submenu and returns the highlight to the row that opened
        /// it. Up/down movement lives in
        /// <see cref="UpdateMenuHighlightFromKeyboard"/> and item activation in
        /// the click path.
        /// </summary>
        static void HandleEntryKeyboard(Menu menu, Entry entry, bool submenuOpen, bool submitted)
        {
            if (entry.kind == EntryKind.Submenu &&
                (submitted || _navRightPulse) &&
                !submenuOpen &&
                entry.childMenu >= 0 &&
                entry.childMenu < _menuCount)
            {
                SetOpenPath(menu.depth, entry.pathId);
                ResetSubmenuScroll(entry.pathId);
                ClearHoverIntent();
                _pendingHighlightMenuOverlay = _menus[entry.childMenu].overlayId;
            }

            if (menu.depth > 0 && _navLeftPulse && menu.parentMenu >= 0)
            {
                var parent = _menus[menu.parentMenu];
                SetOpenPath(menu.depth - 1, 0);
                ClearHoverIntent();
                SetHighlight(parent, FindParentEntryIndex(parent, menu.pathId));
            }
        }

        static int FindParentEntryIndex(Menu parent, int childPathId)
        {
            for (int i = 0; i < parent.entries.Count; ++i)
            {
                if (parent.entries[i].pathId == childPathId)
                    return i;
            }

            return -1;
        }

        static NowRect PlaceSubmenu(Menu parent, Menu child, NowRect parentItemRect, float popupPadding)
        {
            child.contentHeight = child.height;
            child.scrolls = false;

            var right = SubmenuCandidate(parentItemRect, child, popupPadding, true);
            var left = SubmenuCandidate(parentItemRect, child, popupPadding, false);
            var rect = right;

            if (_fitToView)
            {
                bool preferRight = PreferSubmenuRight(parent);
                var preferred = preferRight ? right : left;
                var alternate = preferRight ? left : right;

                var clampedPreferred = NowOverlay.ClampScreenToView(preferred);
                var clampedAlternate = NowOverlay.ClampScreenToView(alternate);
                float preferredError = HorizontalClampError(preferred, clampedPreferred);
                float alternateError = HorizontalClampError(alternate, clampedAlternate);

                rect = preferredError <= alternateError + 0.5f ? clampedPreferred : clampedAlternate;
                child.height = rect.height;
                child.scrolls = child.height < child.contentHeight - 0.5f;
            }

            return rect;
        }

        /// <summary>
        /// How far the view clamp displaced or shrank a submenu candidate
        /// horizontally. Zero means the side fits as placed; larger values mean
        /// the clamp dragged the submenu back over its ancestors, so the side
        /// with the smaller error wins even when neither fits outright (angled
        /// world panels routinely leave no perfectly fitting side).
        /// </summary>
        static float HorizontalClampError(NowRect candidate, NowRect clamped)
        {
            return Mathf.Abs(clamped.x - candidate.x) + Mathf.Abs(clamped.width - candidate.width);
        }

        static NowRect SubmenuCandidate(NowRect parentItemRect, Menu child, float popupPadding, bool right)
        {
            float x = right
                ? parentItemRect.xMax + SubmenuGap
                : parentItemRect.x - SubmenuGap - child.width;

            return new NowRect(
                x,
                parentItemRect.y - popupPadding,
                child.width,
                child.height);
        }

        static bool PreferSubmenuRight(Menu parent)
        {
            if (parent.parentMenu < 0 || parent.parentMenu >= _menuCount)
                return true;

            var ancestor = _menus[parent.parentMenu];
            return parent.popupRect.center.x >= ancestor.popupRect.center.x;
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
        /// depth is immediate; moving onto another visible submenu row is also
        /// immediate unless the pointer is heading toward the currently-open
        /// child menu. Plain item rows and stationary sibling-submenu hovers
        /// still wait for a short hover-intent delay, so diagonal pointer paths
        /// into submenus do not snap them shut. New intents only start while the
        /// pointer is actually moving, so a resting pointer never overrides
        /// keyboard-opened submenus. Timing comes from the input snapshot's
        /// caller-supplied time.
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

            bool hasOpenPathAtDepth = _openPath.Count > depth;

            if (desiredPathId != 0 &&
                (!hasOpenPathAtDepth || (_pointerMoved && !PointerMovingTowardOpenChild(depth))))
            {
                SetOpenPath(depth, desiredPathId);
                ResetSubmenuScroll(desiredPathId);
                ClearHoverIntent();
                return;
            }

            float time = NowInput.current.time;

            if (_hoverIntentDepth != depth || _hoverIntentPath != desiredPathId)
            {
                if (!_pointerMoved)
                    return;

                _hoverIntentDepth = depth;
                _hoverIntentPath = desiredPathId;
                _hoverIntentStart = time;
                NowControlState.RequestRepaint();
                return;
            }

            if (time - _hoverIntentStart >= HoverIntentDelay)
            {
                SetOpenPath(depth, desiredPathId);
                ResetSubmenuScroll(desiredPathId);
                ClearHoverIntent();
                return;
            }

            NowControlState.RequestRepaint();
        }

        static bool PointerMovingTowardOpenChild(int depth)
        {
            if (!_pointerMoved || _openPath.Count <= depth)
                return false;

            var child = FindOpenChildMenu(depth);

            if (child == null || child.popupRect.isEmpty)
                return false;

            Vector2 current = NowInput.current.pointerPosition;
            Vector2 previous = _previousPointerPosition;
            bool childToRight = child.popupRect.center.x >= previous.x;

            if (childToRight)
            {
                if (current.x <= previous.x)
                    return false;

                return PointInTriangle(
                    current,
                    previous,
                    new Vector2(child.popupRect.x, child.popupRect.y - 4f),
                    new Vector2(child.popupRect.x, child.popupRect.yMax + 4f));
            }

            if (current.x >= previous.x)
                return false;

            return PointInTriangle(
                current,
                previous,
                new Vector2(child.popupRect.xMax, child.popupRect.y - 4f),
                new Vector2(child.popupRect.xMax, child.popupRect.yMax + 4f));
        }

        static Menu FindOpenChildMenu(int depth)
        {
            if (_openPath.Count <= depth)
                return null;

            int pathId = _openPath[depth];

            for (int i = 0; i < _menuCount; ++i)
            {
                var menu = _menus[i];

                if (menu.depth == depth + 1 && menu.pathId == pathId)
                    return menu;
            }

            return null;
        }

        static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Cross(point, a, b);
            float bc = Cross(point, b, c);
            float ca = Cross(point, c, a);
            bool hasNegative = ab < 0f || bc < 0f || ca < 0f;
            bool hasPositive = ab > 0f || bc > 0f || ca > 0f;
            return !(hasNegative && hasPositive);
        }

        static float Cross(Vector2 point, Vector2 a, Vector2 b)
        {
            return (point.x - b.x) * (a.y - b.y) - (a.x - b.x) * (point.y - b.y);
        }

        static void ResetSubmenuScroll(int pathId)
        {
            if (pathId == 0 || _openId == 0)
                return;

            NowControlState.Get<float>(NowInput.CombineId(_openId, pathId), "ctx-scroll") = 0f;
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
            _openSurface = null;
            _fitToView = true;
            _activeId = 0;
            _pendingPathStateId = 0;
            _menuCount = 0;
            ClearHoverIntent();
            ClearHighlight();
            _lastPointerPosition = default;
            _previousPointerPosition = default;
            _pointerMoved = false;
            _navLeftPulse = false;
            _navRightPulse = false;
            _navUpPulse = false;
            _navDownPulse = false;
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
