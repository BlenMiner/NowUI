using UnityEngine;
using System;
using System.Collections.Generic;

namespace NowUI
{
    /// <summary>
    /// Immediate-mode context menu on the overlay layer. The owner resolves one
    /// stable source id, opens from a source-aware trigger, and declares authored
    /// items every frame while it is open. A click reports through that item's
    /// call the next frame (deferred overlay draws run after the owner returns):
    /// <code>
    /// var menuId = NowControls.GetControlId("selection-context-menu");
    /// var trigger = NowContextAction.Resolve(selectionRect, moreClicked, moreRect);
    /// NowContextMenu.Open(menuId, trigger);
    ///
    /// if (NowContextMenu.Begin(menuId))
    /// {
    ///     if (NowContextMenu.Item("Copy", id: "copy")) Copy();
    ///     if (NowContextMenu.BeginSubmenu("Create", id: "create"))
    ///     {
    ///         if (NowContextMenu.Item("Node", id: "node")) CreateNode();
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
    /// option stays reachable; submenus clamp and scroll independently. A
    /// retained owner may stay idle with its last menu intact, but once that
    /// owner completes another declaration pass it must declare the menu again
    /// or NowUI closes the stale global menu state.
    /// </summary>
    public static class NowContextMenu
    {
        const string LegacyIdObsoleteMessage =
            "Raw integer context-menu identities were removed. Use the NowResolvedId overload.";
        const string PositionalEntryObsoleteMessage =
            "Positional context-menu entries were removed. Supply a stable authored id, for example Item(label, id: \"copy\").";

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

        static NowResolvedId _openId;
        static INowInputProvider _openSurface;
        static object _openRegistrationOwner;
        static int _openOwnerPassSerial;
        static int _openDeclaredOwnerPassSerial;
        static bool _hasOpenedInputPass;
        static int _openedInputPass;
        static Vector2 _position;
        static bool _hasActionAnchor;
        static NowRect _actionAnchor;
        static bool _fitToView = true;
        static NowResolvedId _activeId;
        static object _activeRegistrationOwner;
        static bool _activeBuildsOpenMenu;
        static bool _activeHasPendingDelivery;
        static NowResolvedId[] _activePendingOpenPath;
        static int _menuCount;
        static int _hoverIntentDepth = -1;
        static NowResolvedId _hoverIntentPath;
        static float _hoverIntentStart;
        static NowResolvedId _highlightMenuOverlay;
        static NowResolvedId _highlightEntryPath;
        static bool _highlightMovedByKeyboard;
        static NowResolvedId _pendingHighlightMenuOverlay;
        static Vector2 _lastPointerPosition;
        static Vector2 _previousPointerPosition;
        static bool _pointerMoved;
        static bool _navLeftPulse;
        static bool _navRightPulse;
        static bool _navUpPulse;
        static bool _navDownPulse;

        static readonly List<Menu> _menus = new List<Menu>(4);
        static readonly List<int> _buildStack = new List<int>(4);
        static readonly List<NowResolvedId> _openPath = new List<NowResolvedId>(4);
        static readonly List<NowResolvedId> _pendingRemovalScratch = new List<NowResolvedId>(4);

        static readonly Dictionary<NowResolvedId, PendingDelivery> _pendingDeliveries =
            new Dictionary<NowResolvedId, PendingDelivery>(4);

        static readonly List<OwnerPass> _ownerPasses = new List<OwnerPass>(2);

        static int _nextOwnerPassSerial;

        static readonly Dictionary<NowResolvedId, int> _labelOccurrenceScratch =
            new Dictionary<NowResolvedId, int>(32);

        struct PendingDelivery
        {
            public NowResolvedId deliveryId;
            public object owner;
            public INowInputProvider provider;
            public int createdOwnerPassSerial;
            public NowResolvedId[] openPath;
        }

        struct OwnerPass
        {
            public object owner;
            public int serial;
            public bool failed;
        }

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
            public string shortcut;
            public bool enabled;
            public bool selected;
            public NowResolvedId pathId;
            public NowResolvedId deliveryId;
            public int localIndex;
            public int childMenu;
        }

        sealed class Menu
        {
            public NowResolvedId rootId;
            public NowResolvedId pathId;
            public NowResolvedId overlayId;
            public int parentMenu;
            public int depth;
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

            public void Reset(
                NowResolvedId rootId,
                NowResolvedId pathId,
                NowResolvedId overlayId,
                int parentMenu,
                int depth)
            {
                this.rootId = rootId;
                this.pathId = pathId;
                this.overlayId = overlayId;
                this.parentMenu = parentMenu;
                this.depth = depth;
                width = 0f;
                height = 0f;
                contentHeight = 0f;
                scrolls = false;
                entries.Clear();
            }
        }

        /// <summary>True while any context menu is open.</summary>
        public static bool isOpen => _openId.hasValue;

        /// <summary>True when the menu with this id is the one currently open.</summary>
        public static bool IsOpen(NowResolvedId id) => id.hasValue && _openId == id;

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static bool IsOpen(int id) => IsOpen(NowResolvedId.FromLegacy(id));

        public static void Open(NowResolvedId id, Vector2 position, bool fitToView = true)
        {
            OpenCore(id, position, fitToView, false, default);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Open(int id, Vector2 position, bool fitToView = true) =>
            Open(NowResolvedId.FromLegacy(id), position, fitToView);

        /// <summary>
        /// Opens from a source-aware trigger. Secondary-pointer triggers open at
        /// the pointer; explicit actions open adjacent to their control. A
        /// default, untriggered value is a no-op.
        /// </summary>
        public static void Open(NowResolvedId id, in NowContextTrigger trigger, bool fitToView = true)
        {
            if (!trigger.triggered)
                return;

            if (trigger.source == NowContextTriggerSource.SecondaryPointer)
            {
                OpenCore(id, trigger.screenPointerPosition, fitToView, false, default);
                NowInput.ConsumePointerPress(NowPointerButton.Secondary);
                return;
            }

            OpenCore(
                id,
                trigger.screenActionAnchor.position,
                fitToView,
                true,
                trigger.screenActionAnchor);
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static void Open(int id, in NowContextTrigger trigger, bool fitToView = true) =>
            Open(NowResolvedId.FromLegacy(id), in trigger, fitToView);

        static void OpenCore(
            NowResolvedId id,
            Vector2 position,
            bool fitToView,
            bool hasActionAnchor,
            NowRect actionAnchor)
        {
            RequireMenuId(id);
            _openId = id;
            _openSurface = NowInput.currentProvider;
            _openRegistrationOwner = NowOverlay.currentRegistrationOwner;
            _openOwnerPassSerial = CurrentOwnerPassSerial(_openRegistrationOwner);
            _openDeclaredOwnerPassSerial = 0;
            _hasOpenedInputPass = NowInput.hasContext;
            _openedInputPass = _hasOpenedInputPass ? NowInput.current.inputPass : 0;
            _position = position;
            _hasActionAnchor = hasActionAnchor;
            _actionAnchor = actionAnchor;
            _fitToView = fitToView;
            _openPath.Clear();
            ClearHoverIntent();
            ClearHighlight();
            NowControlState.Get<float>(id, "ctx-scroll") = 0f;
            NowControlState.RequestRepaint();
        }

        public static void Close()
        {
            NowResolvedId closingId = _openId;

            if (_openId.hasValue)
                NowControlState.RequestRepaint();

            _openId = NowResolvedId.None;
            _openSurface = null;
            _openRegistrationOwner = null;
            _openOwnerPassSerial = 0;
            _openDeclaredOwnerPassSerial = 0;
            _hasOpenedInputPass = false;
            _hasActionAnchor = false;
            _actionAnchor = default;

            if (_activeBuildsOpenMenu && _activeId == closingId)
            {
                _activeId = NowResolvedId.None;
                _activeRegistrationOwner = null;
                _activeBuildsOpenMenu = false;
                _activeHasPendingDelivery = false;
                _activePendingOpenPath = null;
                _buildStack.Clear();
            }

            ClearHoverIntent();
            ClearHighlight();
        }

        static void ClearHighlight()
        {
            _highlightMenuOverlay = NowResolvedId.None;
            _highlightEntryPath = NowResolvedId.None;
            _highlightMovedByKeyboard = false;
            _pendingHighlightMenuOverlay = NowResolvedId.None;
        }

        /// <summary>
        /// True while the menu with this id is open — declare items, then call
        /// <see cref="End"/>. Also true during the owner's next completed pass
        /// after an item was clicked (the menu has closed by then) so the
        /// clicked item can deliver.
        /// </summary>
        public static bool Begin(NowResolvedId id)
        {
            RequireMenuId(id);

            if (NowInput.isPassive)
                return false;

            object owner = NowOverlay.currentRegistrationOwner;
            bool buildsOpenMenu = _openId == id &&
                ReferenceEquals(_openRegistrationOwner, owner) &&
                ReferenceEquals(_openSurface, NowInput.currentProvider);
            bool hasPendingDelivery =
                _pendingDeliveries.TryGetValue(id, out var pending) &&
                ReferenceEquals(pending.owner, owner) &&
                ReferenceEquals(pending.provider, NowInput.currentProvider);

            if (!buildsOpenMenu && !hasPendingDelivery)
                return false;

            _activeId = id;
            _activeRegistrationOwner = owner;
            _activeBuildsOpenMenu = buildsOpenMenu;
            _activeHasPendingDelivery = hasPendingDelivery;
            _activePendingOpenPath = hasPendingDelivery ? pending.openPath : null;
            _menuCount = 0;
            _buildStack.Clear();
            _labelOccurrenceScratch.Clear();

            int rootIndex = AddMenu(id, id, id, -1, 0);
            _buildStack.Add(rootIndex);
            return true;
        }

        [Obsolete(LegacyIdObsoleteMessage, true)]
        public static bool Begin(int id) => Begin(NowResolvedId.FromLegacy(id));

        static void RequireMenuId(NowResolvedId id)
        {
            if (!id.hasValue)
                throw new ArgumentException("A resolved context-menu id is required.", nameof(id));
        }

        /// <summary>Adds an item; true when it was clicked (the frame after the click).</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool Item(string label)
        {
            return ItemPositional(label, null, true, false);
        }

        /// <summary>Adds an item; true when it was clicked (the frame after the click).</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool Item(string label, bool enabled, bool selected = false)
        {
            return ItemPositional(label, null, enabled, selected);
        }

        /// <summary>Adds an item with a right-aligned shortcut hint ("Ctrl+C").</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool Item(string label, string shortcut)
        {
            return ItemPositional(label, shortcut, true, false);
        }

        /// <summary>Adds an item with a right-aligned shortcut hint ("Ctrl+C").</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool Item(string label, string shortcut, bool enabled, bool selected = false)
        {
            return ItemPositional(label, shortcut, enabled, selected);
        }

        static bool ItemPositional(string label, string shortcut, bool enabled, bool selected)
        {
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return false;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;
            NowResolvedId pathId = menu.pathId.Child(localIndex + 1);
            NowResolvedId deliveryId = ItemDeliveryId(menu, label ?? string.Empty);
            return AddItem(menu, label, shortcut, enabled, selected, pathId, deliveryId, localIndex);
        }

        /// <summary>
        /// Adds an item whose menu-local authored id remains stable when its
        /// label changes or sibling rows are inserted, removed, or reordered.
        /// </summary>
        public static bool Item(
            string label,
            NowId id,
            bool enabled = true,
            bool selected = false,
            string shortcut = null)
        {
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return false;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;
            NowResolvedId pathId = AuthoredEntryId(menu, id);
            NowResolvedId deliveryId = pathId.Child("ctx-delivery");
            return AddItem(menu, label, shortcut, enabled, selected, pathId, deliveryId, localIndex);
        }

        static bool AddItem(
            Menu menu,
            string label,
            string shortcut,
            bool enabled,
            bool selected,
            NowResolvedId pathId,
            NowResolvedId deliveryId,
            int localIndex)
        {
            menu.entries.Add(new Entry
            {
                kind = EntryKind.Item,
                label = label ?? string.Empty,
                shortcut = shortcut,
                enabled = enabled,
                selected = selected,
                pathId = pathId,
                deliveryId = deliveryId,
                localIndex = localIndex,
                childMenu = -1
            });

            if (_activeHasPendingDelivery &&
                _pendingDeliveries.TryGetValue(_activeId, out var pending) &&
                ReferenceEquals(pending.owner, _activeRegistrationOwner) &&
                ReferenceEquals(pending.provider, NowInput.currentProvider) &&
                pending.deliveryId == deliveryId)
            {
                _pendingDeliveries.Remove(_activeId);
                _activeHasPendingDelivery = false;
                _activePendingOpenPath = null;
                return enabled;
            }

            return false;
        }

        static NowResolvedId AuthoredEntryId(Menu menu, NowId id)
        {
            if (!id.hasValue)
                throw new ArgumentException(
                    "Stable context-menu entries require an explicit id.",
                    nameof(id));

            return menu.pathId.Child("ctx-authored-entry").Child(id);
        }

        /// <summary>
        /// Click-delivery identity for an item: label-based (plus an occurrence
        /// counter for duplicate labels) rather than positional. Clicks deliver
        /// one frame after the menu closes, and conditionally declared items can
        /// shift positions between the click and the delivery frame — a
        /// positional id would then hand the click to whichever item inherited
        /// the slot.
        /// </summary>
        static NowResolvedId ItemDeliveryId(Menu menu, string label)
        {
            NowResolvedId seed = menu.pathId.Child("ctx-legacy-label-delivery");
            NowResolvedId labelId = label.Length > 0
                ? seed.Child(label)
                : seed.Child(-1);
            _labelOccurrenceScratch.TryGetValue(labelId, out int occurrence);
            _labelOccurrenceScratch[labelId] = occurrence + 1;
            return labelId.Child(occurrence + 1);
        }

        /// <summary>Adds a submenu row; true while that submenu should declare its children.</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool BeginSubmenu(string label)
        {
            return BeginSubmenuPositional(label, true, false);
        }

        /// <summary>Adds a submenu row; true while that submenu should declare its children.</summary>
        [Obsolete(PositionalEntryObsoleteMessage, true)]
        public static bool BeginSubmenu(string label, bool enabled, bool selected = false)
        {
            return BeginSubmenuPositional(label, enabled, selected);
        }

        static bool BeginSubmenuPositional(string label, bool enabled, bool selected)
        {
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return false;

            int parentIndex = _buildStack[_buildStack.Count - 1];
            var parent = _menus[parentIndex];
            int localIndex = parent.entries.Count;
            NowResolvedId pathId = parent.pathId.Child(localIndex + 1);
            return AddSubmenu(parentIndex, parent, label, enabled, selected, localIndex, pathId);
        }

        /// <summary>
        /// Adds a submenu whose menu-local authored id remains stable when its
        /// label changes or sibling rows are inserted, removed, or reordered.
        /// </summary>
        public static bool BeginSubmenu(
            string label,
            NowId id,
            bool enabled = true,
            bool selected = false)
        {
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return false;

            int parentIndex = _buildStack[_buildStack.Count - 1];
            var parent = _menus[parentIndex];
            int localIndex = parent.entries.Count;
            NowResolvedId pathId = AuthoredEntryId(parent, id);
            return AddSubmenu(parentIndex, parent, label, enabled, selected, localIndex, pathId);
        }

        static bool AddSubmenu(
            int parentIndex,
            Menu parent,
            string label,
            bool enabled,
            bool selected,
            int localIndex,
            NowResolvedId pathId)
        {
            // Keep the submenu source beneath its authored row. NowOverlay enters
            // the Overlay domain exactly once when this source is deferred.
            int childMenu = AddMenu(
                _activeId,
                pathId,
                pathId.Child("ctx-submenu-overlay"),
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
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Label,
                label = label ?? string.Empty,
                enabled = false,
                selected = false,
                pathId = menu.pathId.Child(localIndex + 1),
                localIndex = localIndex,
                childMenu = -1
            });
        }

        /// <summary>Adds a separator row.</summary>
        public static void Separator()
        {
            if (!_activeId.hasValue || _buildStack.Count == 0)
                return;

            var menu = CurrentMenu();
            int localIndex = menu.entries.Count;

            menu.entries.Add(new Entry
            {
                kind = EntryKind.Separator,
                label = string.Empty,
                enabled = false,
                selected = false,
                pathId = menu.pathId.Child(localIndex + 1),
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
            if (!_activeId.hasValue)
                return;

            NowResolvedId id = _activeId;
            object owner = _activeRegistrationOwner;
            bool buildsOpenMenu = _activeBuildsOpenMenu;
            bool hasPendingDelivery = _activeHasPendingDelivery;
            _activeId = NowResolvedId.None;
            _activeRegistrationOwner = null;
            _activeBuildsOpenMenu = false;
            _activeHasPendingDelivery = false;
            _activePendingOpenPath = null;
            _buildStack.Clear();

            // The owner's next declaration pass is the sole delivery window.
            // If no declared item claimed it, discard it instead of allowing a
            // later, unrelated menu layout to receive the old click.
            if (hasPendingDelivery &&
                _pendingDeliveries.TryGetValue(id, out var pending) &&
                ReferenceEquals(pending.owner, owner))
            {
                _pendingDeliveries.Remove(id);
            }

            if (!buildsOpenMenu ||
                _openId != id ||
                !ReferenceEquals(_openRegistrationOwner, owner))
            {
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
            root.popupRect = PlaceRootMenu(root, theme);

            if (_fitToView)
            {
                root.popupRect = NowOverlay.ClampScreenToView(root.popupRect);
                root.height = root.popupRect.height;
                root.scrolls = root.height < root.contentHeight - 0.5f;
            }

            NowOverlay.BlockAllSurfaces(root.overlayId);
            NowOverlay.DeferScreen(root.popupRect, root.overlayId, 0, DrawDeferred);
            _openDeclaredOwnerPassSerial = CurrentOwnerPassSerial(owner);
        }

        static NowRect PlaceRootMenu(Menu root, NowThemeAsset theme)
        {
            if (!_hasActionAnchor)
                return new NowRect(_position.x, _position.y, root.width, root.height);

            float gap = Mathf.Max(0f, theme.controlStyles.dropdownPopupGap);
            float x = _actionAnchor.xMax - root.width;
            float y = _actionAnchor.yMax + gap;

            if (_fitToView && y + root.height > NowInput.surface.size.y)
            {
                float above = _actionAnchor.y - gap - root.height;

                if (above >= 0f)
                    y = above;
            }

            return new NowRect(x, y, root.width, root.height);
        }

        static void DrawDeferred(int menuIndex)
        {
            if (menuIndex < 0 || menuIndex >= _menuCount)
                return;

            var menu = _menus[menuIndex];

            if (_openId != menu.rootId)
                return;

            if (menu.parentMenu < 0)
                UpdateTreeInput(menu);

            var theme = NowTheme.themeAsset;
            DrawMenu(theme, menu);

            if (menu.parentMenu >= 0)
                return;

            var snapshot = NowInput.current;
            bool openingInputPass = _hasOpenedInputPass &&
                ReferenceEquals(_openSurface, NowInput.currentProvider) &&
                snapshot.inputPass == _openedInputPass;

            if (_hasOpenedInputPass && !openingInputPass)
                _hasOpenedInputPass = false;

            bool pressed = snapshot.anyPointerPressed && !openingInputPass;
            bool pointerInsideTree = NowOverlay.IsPointerInsideOverlayTree(menu.rootId, snapshot.pointerPosition);

            if ((pressed && !pointerInsideTree) ||
                (snapshot.cancelPressed && !NowInput.cancelConsumed) ||
                (snapshot.scrollDelta != Vector2.zero && !pointerInsideTree))
            {
                if (pressed && !pointerInsideTree)
                    NowInput.ConsumePointerPress();

                if (snapshot.cancelPressed)
                    NowInput.ConsumeKeyActivity();

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

            if (_navLeftPulse || _navRightPulse || _navUpPulse || _navDownPulse)
                NowInput.ConsumeKeyActivity();

            NowFocus.LockNavigation();
            NowFocus.RetainFocus();

            if (!_highlightMenuOverlay.hasValue)
                return;

            var highlighted = FindMenu(_highlightMenuOverlay);

            if (highlighted == null ||
                highlighted.rootId != _openId ||
                !IsMenuDrawn(highlighted) ||
                FindEntryIndex(highlighted, _highlightEntryPath) < 0)
            {
                ClearHighlight();
            }
        }

        static bool NavPulse(Menu root, int seed, bool held)
        {
            return NowControlState.Repeat(
                root.rootId.Child(seed),
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

            if (_pendingHighlightMenuOverlay.hasValue && _pendingHighlightMenuOverlay == menu.overlayId)
            {
                _pendingHighlightMenuOverlay = NowResolvedId.None;
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
            int highlightedIndex = _highlightMenuOverlay == menu.overlayId
                ? FindEntryIndex(menu, _highlightEntryPath)
                : -1;
            bool ownsHighlight = highlightedIndex >= 0;

            if (!_navUpPulse && !_navDownPulse)
                return;

            if (!ownsHighlight)
            {
                if (_highlightMenuOverlay.hasValue || HasOpenChild(menu))
                    return;

                SetHighlight(menu, FindSelectableEntry(menu, _navDownPulse ? -1 : menu.entries.Count, _navDownPulse ? 1 : -1));
                return;
            }

            SetHighlight(menu, FindSelectableEntry(menu, highlightedIndex, _navDownPulse ? 1 : -1));
        }

        static void SetHighlight(Menu menu, int entryIndex)
        {
            if (entryIndex < 0)
                return;

            _highlightMenuOverlay = menu.overlayId;
            _highlightEntryPath = menu.entries[entryIndex].pathId;
            _highlightMovedByKeyboard = true;
            NowControlState.RequestRepaint();
        }

        static int FindEntryIndex(Menu menu, NowResolvedId pathId)
        {
            if (!pathId.hasValue)
                return -1;

            for (int i = 0; i < menu.entries.Count; ++i)
            {
                if (menu.entries[i].pathId == pathId)
                    return i;
            }

            return -1;
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
            int highlightedIndex = FindEntryIndex(menu, _highlightEntryPath);

            if (!_highlightMovedByKeyboard ||
                _highlightMenuOverlay != menu.overlayId ||
                highlightedIndex < 0)
            {
                return;
            }

            _highlightMovedByKeyboard = false;
            float itemHeight = theme.controlStyles.contextMenuItemHeight;
            float offset = 0f;

            for (int i = 0; i < highlightedIndex; ++i)
                offset += EntryHeight(menu.entries[i], itemHeight);

            float entryHeight = EntryHeight(menu.entries[highlightedIndex], itemHeight);
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
                        SetOpenPath(menu.depth, NowResolvedId.None);
                    }
                    else
                    {
                        var child = _menus[entry.childMenu];

                        if (child.entries.Count > 0)
                        {
                            child.popupRect = PlaceSubmenu(menu, child, itemRect, popupPadding);
                            NowOverlay.DeferScreen(
                                child.popupRect,
                                child.overlayId,
                                entry.childMenu,
                                DrawDeferred);
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
                    UpdateOpenPathFromHover(
                        menu.depth,
                        entry.kind == EntryKind.Submenu ? entry.pathId : NowResolvedId.None,
                        entry.kind == EntryKind.Submenu ? entry.childMenu : -1);
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
                ? NowInput.Interact(entry.pathId.Child("ctx-interaction"), itemRect)
                : default;

            if (entry.enabled && interaction.hovered && _pointerMoved &&
                (_highlightMenuOverlay != menu.overlayId || _highlightEntryPath != entry.pathId))
            {
                _highlightMenuOverlay = menu.overlayId;
                _highlightEntryPath = entry.pathId;
                _highlightMovedByKeyboard = false;
                NowControlState.RequestRepaint();
            }

            bool highlighted = _highlightMenuOverlay == menu.overlayId &&
                _highlightEntryPath == entry.pathId;
            bool submitted = entry.enabled && highlighted && NowInput.current.submitPressed;

            if (submitted)
                NowInput.ConsumeKeyActivity();

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

            if (!string.IsNullOrEmpty(entry.shortcut))
            {
                Color shortcutColor = theme.GetColor(NowColorToken.TextMuted);

                if (!entry.enabled)
                    shortcutColor.a *= 0.62f;

                float shortcutWidth = theme.ResolveText(NowTextStyle.Muted).Measure(entry.shortcut).x;
                float shortcutInset = theme.controlStyles.contextMenuPaddingX * 0.7f +
                    (entry.kind == EntryKind.Submenu ? 22f : 0f);

                NowControls.DrawLeftLabel(
                    theme,
                    new NowRect(itemRect.xMax - shortcutWidth - shortcutInset, itemRect.y, shortcutWidth + 6f, itemRect.height),
                    entry.shortcut,
                    NowTextStyle.Muted,
                    shortcutColor);
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
            {
                UpdateOpenPathFromHover(
                    menu.depth,
                    entry.kind == EntryKind.Submenu ? entry.pathId : NowResolvedId.None,
                    entry.kind == EntryKind.Submenu ? entry.childMenu : -1);
            }

            if (entry.enabled && highlighted)
                HandleEntryKeyboard(menu, entry, submenuOpen, submitted);

            if (entry.kind != EntryKind.Item || !entry.enabled || (!interaction.clicked && !submitted))
                return;

            _pendingDeliveries[menu.rootId] = new PendingDelivery
            {
                deliveryId = entry.deliveryId,
                owner = _openRegistrationOwner,
                provider = _openSurface,
                createdOwnerPassSerial = CurrentOwnerPassSerial(_openRegistrationOwner),
                openPath = CopyOpenPath(menu.depth)
            };
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
                ResetSubmenuScroll(entry.childMenu);
                ClearHoverIntent();
                _pendingHighlightMenuOverlay = _menus[entry.childMenu].overlayId;
            }

            if (menu.depth > 0 && _navLeftPulse && menu.parentMenu >= 0)
            {
                var parent = _menus[menu.parentMenu];
                SetOpenPath(menu.depth - 1, NowResolvedId.None);
                ClearHoverIntent();
                SetHighlight(parent, FindParentEntryIndex(parent, menu.pathId));
            }
        }

        static int FindParentEntryIndex(Menu parent, NowResolvedId childPathId)
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

                    if (!string.IsNullOrEmpty(entry.shortcut))
                        rightReserve += textStyle.Measure(entry.shortcut).x + 28f;

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

        static int AddMenu(
            NowResolvedId rootId,
            NowResolvedId pathId,
            NowResolvedId overlayId,
            int parentMenu,
            int depth)
        {
            if (_menuCount >= _menus.Count)
                _menus.Add(new Menu());

            int index = _menuCount++;
            _menus[index].Reset(rootId, pathId, overlayId, parentMenu, depth);

            // Submenu overlays chain to the root menu id, so a control that
            // declared itself the menu's owner reads as focused-within while
            // any depth of the tree is the active focus layer.
            if (parentMenu >= 0)
                NowFocus.DeclareOwner(overlayId, rootId);

            return index;
        }

        static Menu CurrentMenu()
        {
            return _menus[_buildStack[_buildStack.Count - 1]];
        }

        static Menu FindMenu(NowResolvedId overlayId)
        {
            for (int i = 0; i < _menuCount; ++i)
            {
                if (_menus[i].overlayId == overlayId)
                    return _menus[i];
            }

            return null;
        }

        static bool IsPathOpen(int depth, NowResolvedId pathId)
        {
            return _openPath.Count > depth && _openPath[depth] == pathId;
        }

        static bool IsPendingPathOpen(int depth, NowResolvedId pathId)
        {
            return _activePendingOpenPath != null &&
                _activePendingOpenPath.Length > depth &&
                _activePendingOpenPath[depth] == pathId;
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
        static void UpdateOpenPathFromHover(
            int depth,
            NowResolvedId desiredPathId,
            int desiredChildMenu)
        {
            bool alreadyDesired = desiredPathId.hasValue
                ? IsPathOpen(depth, desiredPathId)
                : _openPath.Count <= depth;

            if (alreadyDesired)
            {
                if (_hoverIntentDepth == depth)
                    ClearHoverIntent();

                return;
            }

            bool hasOpenPathAtDepth = _openPath.Count > depth;

            if (desiredPathId.hasValue &&
                (!hasOpenPathAtDepth || (_pointerMoved && !PointerMovingTowardOpenChild(depth))))
            {
                SetOpenPath(depth, desiredPathId);
                ResetSubmenuScroll(desiredChildMenu);
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
                ResetSubmenuScroll(desiredChildMenu);
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

            NowResolvedId pathId = _openPath[depth];

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

        static void ResetSubmenuScroll(int childMenu)
        {
            if (childMenu < 0 || childMenu >= _menuCount || !_openId.hasValue)
                return;

            var menu = _menus[childMenu];

            if (menu.parentMenu < 0 || menu.rootId != _openId)
                return;

            NowControlState.Get<float>(menu.overlayId, "ctx-scroll") = 0f;
        }

        static void ClearHoverIntent()
        {
            _hoverIntentDepth = -1;
            _hoverIntentPath = NowResolvedId.None;
            _hoverIntentStart = 0f;
        }

        static void SetOpenPath(int depth, NowResolvedId pathId)
        {
            int targetCount = pathId.hasValue ? depth + 1 : depth;
            bool changed = _openPath.Count != targetCount ||
                (pathId.hasValue && (_openPath.Count <= depth || _openPath[depth] != pathId));

            while (_openPath.Count > depth)
                _openPath.RemoveAt(_openPath.Count - 1);

            if (pathId.hasValue)
                _openPath.Add(pathId);

            if (changed)
                NowControlState.RequestRepaint();
        }

        static NowResolvedId[] CopyOpenPath(int depth)
        {
            int count = Mathf.Min(depth, _openPath.Count);

            if (count <= 0)
                return null;

            var result = new NowResolvedId[count];

            for (int i = 0; i < count; ++i)
                result[i] = _openPath[i];

            return result;
        }

        /// <summary>
        /// Starts a declaration pass for the owner selected by the overlay
        /// transaction. The pass serial is deliberately independent of Unity's
        /// frame count because IMGUI can run several input/layout/repaint passes
        /// inside one Unity frame.
        /// </summary>
        internal static void BeginOwnerPass(object owner)
        {
            unchecked
            {
                ++_nextOwnerPassSerial;

                if (_nextOwnerPassSerial == 0)
                    _nextOwnerPassSerial = 1;
            }

            _ownerPasses.Add(new OwnerPass
            {
                owner = owner,
                serial = _nextOwnerPassSerial
            });
        }

        /// <summary>
        /// Completes one owner's declaration pass. An open menu is retained
        /// while its owner is idle, but once that owner actually runs again it
        /// must finish declaring the menu. Pending item delivery gets the same
        /// single-subsequent-pass lifetime.
        /// </summary>
        internal static void EndOwnerPass(object owner, bool completed)
        {
            int index = FindOwnerPass(owner);

            if (index < 0)
                return;

            OwnerPass pass = _ownerPasses[index];
            _ownerPasses.RemoveAt(index);

            if (!completed || pass.failed)
            {
                DropPendingCreatedInPass(owner, pass.serial);
                return;
            }

            DropPendingBeforeCompletedPass(owner, pass.serial);

            if (_openId.hasValue &&
                ReferenceEquals(_openRegistrationOwner, owner) &&
                _openOwnerPassSerial != pass.serial &&
                _openDeclaredOwnerPassSerial != pass.serial)
            {
                Close();
            }
        }

        /// <summary>Marks active passes failed without applying liveness cleanup.</summary>
        internal static void MarkOwnerPassesFailed()
        {
            for (int i = 0; i < _ownerPasses.Count; ++i)
            {
                OwnerPass pass = _ownerPasses[i];
                pass.failed = true;
                _ownerPasses[i] = pass;
            }
        }

        /// <summary>
        /// Drops lifecycle bookkeeping for transactions abandoned by a reset or
        /// leaked input scope. Abandonment is not evidence that an owner
        /// successfully completed a declaration pass, so it never closes a
        /// previously valid open menu.
        /// </summary>
        internal static void AbandonOwnerPasses()
        {
            for (int i = 0; i < _ownerPasses.Count; ++i)
            {
                OwnerPass pass = _ownerPasses[i];
                DropPendingCreatedInPass(pass.owner, pass.serial);
            }

            _ownerPasses.Clear();
        }

        /// <summary>Releases all menu state rooted by a disposed overlay owner.</summary>
        internal static void ReleaseOwner(object owner)
        {
            if (_openId.hasValue && ReferenceEquals(_openRegistrationOwner, owner))
                Close();

            DropAllPendingForOwner(owner);

            if (ReferenceEquals(_activeRegistrationOwner, owner))
            {
                _activeId = NowResolvedId.None;
                _activeRegistrationOwner = null;
                _activeBuildsOpenMenu = false;
                _activeHasPendingDelivery = false;
                _activePendingOpenPath = null;
                _buildStack.Clear();
            }

            for (int i = _ownerPasses.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(_ownerPasses[i].owner, owner))
                    _ownerPasses.RemoveAt(i);
            }
        }

        /// <summary>
        /// True while the context-menu subsystem still needs an overlay owner
        /// for an open menu or a pending click delivery.
        /// </summary>
        internal static bool TracksOwner(object owner)
        {
            if (_openId.hasValue && ReferenceEquals(_openRegistrationOwner, owner))
                return true;

            foreach (var pair in _pendingDeliveries)
            {
                if (ReferenceEquals(pair.Value.owner, owner))
                    return true;
            }

            return false;
        }

        static int CurrentOwnerPassSerial(object owner)
        {
            int index = FindOwnerPass(owner);
            return index >= 0 ? _ownerPasses[index].serial : 0;
        }

        static int FindOwnerPass(object owner)
        {
            for (int i = _ownerPasses.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(_ownerPasses[i].owner, owner))
                    return i;
            }

            return -1;
        }

        static void DropPendingBeforeCompletedPass(object owner, int currentPassSerial)
        {
            _pendingRemovalScratch.Clear();

            foreach (var pair in _pendingDeliveries)
            {
                PendingDelivery pending = pair.Value;

                if (ReferenceEquals(pending.owner, owner) &&
                    pending.createdOwnerPassSerial != currentPassSerial)
                {
                    _pendingRemovalScratch.Add(pair.Key);
                }
            }

            RemovePendingScratch();
        }

        static void DropPendingCreatedInPass(object owner, int passSerial)
        {
            _pendingRemovalScratch.Clear();

            foreach (var pair in _pendingDeliveries)
            {
                PendingDelivery pending = pair.Value;

                if (ReferenceEquals(pending.owner, owner) &&
                    pending.createdOwnerPassSerial == passSerial)
                {
                    _pendingRemovalScratch.Add(pair.Key);
                }
            }

            RemovePendingScratch();
        }

        static void DropAllPendingForOwner(object owner)
        {
            _pendingRemovalScratch.Clear();

            foreach (var pair in _pendingDeliveries)
            {
                if (ReferenceEquals(pair.Value.owner, owner))
                    _pendingRemovalScratch.Add(pair.Key);
            }

            RemovePendingScratch();
        }

        static void RemovePendingScratch()
        {
            for (int i = 0; i < _pendingRemovalScratch.Count; ++i)
                _pendingDeliveries.Remove(_pendingRemovalScratch[i]);

            _pendingRemovalScratch.Clear();
        }

        internal static int pendingDeliveryCount => _pendingDeliveries.Count;

        public static void Reset()
        {
            _openId = NowResolvedId.None;
            _openSurface = null;
            _openRegistrationOwner = null;
            _openOwnerPassSerial = 0;
            _openDeclaredOwnerPassSerial = 0;
            _hasOpenedInputPass = false;
            _openedInputPass = 0;
            _position = default;
            _hasActionAnchor = false;
            _actionAnchor = default;
            _fitToView = true;
            _activeId = NowResolvedId.None;
            _activeRegistrationOwner = null;
            _activeBuildsOpenMenu = false;
            _activeHasPendingDelivery = false;
            _activePendingOpenPath = null;
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
            _pendingRemovalScratch.Clear();
            _pendingDeliveries.Clear();
            _ownerPasses.Clear();
            _nextOwnerPassSerial = 0;

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
