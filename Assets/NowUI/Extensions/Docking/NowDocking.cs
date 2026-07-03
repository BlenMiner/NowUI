using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.Docking
{
    public enum NowDockSide
    {
        Center,
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>
    /// Retained layout state for a dockable immediate-mode window group. Submit
    /// windows each frame with <see cref="Window(string, Action, string, bool)"/>,
    /// then draw the dock space once with <see cref="NowDock.Space"/>.
    /// </summary>
    public sealed class NowDockSpace
    {
        const float DefaultTabHeight = 28f;
        const float DefaultSplitterSize = 5f;
        const float DefaultMinPaneSize = 88f;
        const float DefaultContentPadding = 8f;

        sealed class WindowState
        {
            public string id;
            public int idHash;
            public string title;
            public Action<NowRect> draw;
            public bool submitted;
            public bool hidden;
            public bool canClose;
            public bool floating;
            public NowRect floatingRect;
        }

        sealed class Node
        {
            public int id;
            public Node parent;
            public Node first;
            public Node second;
            public bool horizontal;
            public float ratio;
            public readonly List<string> tabs = new List<string>(4);
            public string selected;
            public NowRect rect;
            public NowRect contentRect;
            public NowRect splitterRect;

            public bool isLeaf => first == null && second == null;
        }

        internal struct Style
        {
            public float tabHeight;
            public float splitterSize;
            public float minPaneSize;
            public float contentPadding;
            public bool drawBackground;
        }

        struct DropTarget
        {
            public Node leaf;
            public NowDockSide side;
            public int tabIndex;
        }

        static readonly int SplitterSeed = NowInput.GetId("dock-splitter");
        static readonly int ContentSeed = NowInput.GetId("dock-content");
        static readonly int TabSeed = NowInput.GetId("dock-tab");
        static readonly int FloatingSeed = NowInput.GetId("dock-floating");
        static readonly int DragContentSeed = NowInput.GetId("dock-drag-content");

        static readonly Color DockBackground = new Color(0.075f, 0.085f, 0.105f, 1f);
        static readonly Color DockPanel = new Color(0.105f, 0.12f, 0.155f, 1f);
        static readonly Color DockPanelRaised = new Color(0.135f, 0.155f, 0.205f, 1f);
        static readonly Color DockTabBar = new Color(0.075f, 0.085f, 0.11f, 1f);
        static readonly Color DockTabInactive = new Color(0.105f, 0.12f, 0.155f, 1f);
        static readonly Color DockBorder = new Color(0.28f, 0.32f, 0.40f, 1f);
        static readonly Color DockText = new Color(0.90f, 0.925f, 0.975f, 1f);
        static readonly Color DockTextMuted = new Color(0.66f, 0.705f, 0.79f, 1f);

        readonly Dictionary<string, WindowState> _windows = new Dictionary<string, WindowState>(8);
        readonly List<string> _windowOrder = new List<string>(8);
        readonly List<string> _activeWindows = new List<string>(8);
        readonly List<Node> _leaves = new List<Node>(8);

        Node _root;
        NowRect _dockRect;
        int _nextNodeId = 1;
        string _draggingWindowId;
        Vector2 _tabDragOffset;
        bool _hasTabDragOffset;
        string _pendingCloseId;

        /// <summary>Submit a layout-based window for this frame.</summary>
        public void Window(string title, Action draw, string id = null, bool canClose = false)
        {
            Window(title, _ => draw?.Invoke(), id, canClose);
        }

        /// <summary>
        /// Submit an explicit-rect window for this frame. The callback receives the
        /// clipped content rect for the selected tab.
        /// </summary>
        public void Window(string title, Action<NowRect> draw, string id = null, bool canClose = false)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Dock window titles cannot be null or empty.", nameof(title));

            string windowId = string.IsNullOrEmpty(id) ? title : id;

            if (!_windows.TryGetValue(windowId, out var window))
            {
                window = new WindowState
                {
                    id = windowId,
                    idHash = NowInput.GetId(windowId)
                };
                _windows.Add(windowId, window);
                _windowOrder.Add(windowId);
            }

            window.title = title;
            window.draw = draw;
            window.canClose = canClose;
            window.submitted = true;
        }

        public bool IsWindowOpen(string idOrTitle)
        {
            return !string.IsNullOrEmpty(idOrTitle) &&
                (!_windows.TryGetValue(idOrTitle, out var window) || !window.hidden);
        }

        public void OpenWindow(string idOrTitle)
        {
            if (!string.IsNullOrEmpty(idOrTitle) && _windows.TryGetValue(idOrTitle, out var window))
                window.hidden = false;
        }

        public void CloseWindow(string idOrTitle)
        {
            if (string.IsNullOrEmpty(idOrTitle))
                return;

            if (_windows.TryGetValue(idOrTitle, out var window))
            {
                window.hidden = true;
                window.floating = false;
                window.floatingRect = default;
            }

            RemoveWindowFromTree(idOrTitle);
        }

        public void ClearLayout()
        {
            _root = null;
            _draggingWindowId = null;
            _tabDragOffset = default;
            _hasTabDragOffset = false;
            _pendingCloseId = null;
            _nextNodeId = 1;

            foreach (var window in _windows.Values)
            {
                window.floating = false;
                window.floatingRect = default;
            }
        }

        public void Reset()
        {
            ClearLayout();
            _windows.Clear();
            _windowOrder.Clear();
            _activeWindows.Clear();
            _leaves.Clear();
        }

        public bool Dock(string windowId, string targetWindowId, NowDockSide side)
        {
            if (string.IsNullOrEmpty(windowId) || string.IsNullOrEmpty(targetWindowId) ||
                !_windows.ContainsKey(windowId) || !_windows.ContainsKey(targetWindowId))
            {
                return false;
            }

            if (_root == null || FindLeafContaining(_root, targetWindowId) == null)
            {
                var active = GetCurrentlySubmittedWindows(includeFloating: false);

                if (active.Count > 0)
                    EnsureLayout(active);
            }

            var target = FindLeafContaining(_root, targetWindowId);

            if (target == null)
                return false;

            DockWindowToLeaf(windowId, target, side);
            return true;
        }

        internal void Draw(NowRect rect, int controlId, Style style)
        {
            try
            {
                _dockRect = rect;
                var theme = NowTheme.themeAsset;

                EnsureLayout(GetCurrentlySubmittedWindows(includeFloating: false));

                if (style.drawBackground)
                {
                    Now.Rectangle(rect)
                        .SetColor(DockBackground)
                        .SetRadius(6f)
                        .Draw();
                }

                if (_root != null)
                {
                    LayoutNode(_root, rect, style);
                    DrawNode(_root, controlId, style, theme);
                }

                CompleteLostTabDrag(controlId, style, theme);
                DrawFloatingWindows(controlId, style, theme);
                DrawTabDragFeedback(controlId, style, theme);

                if (_pendingCloseId != null)
                {
                    CloseWindow(_pendingCloseId);
                    _pendingCloseId = null;
                    NowControlState.RequestRepaint();
                }
            }
            finally
            {
                ResetFrameSubmissions();
            }
        }

        List<string> GetCurrentlySubmittedWindows(bool includeFloating = true)
        {
            _activeWindows.Clear();

            for (int i = 0; i < _windowOrder.Count; ++i)
            {
                var id = _windowOrder[i];

                if (_windows.TryGetValue(id, out var window) && window.submitted && !window.hidden &&
                    (includeFloating || !window.floating))
                {
                    _activeWindows.Add(id);
                }
            }

            return _activeWindows;
        }

        void ResetFrameSubmissions()
        {
            for (int i = 0; i < _windowOrder.Count; ++i)
            {
                if (!_windows.TryGetValue(_windowOrder[i], out var window))
                    continue;

                window.submitted = false;
                window.draw = null;
            }
        }

        void EnsureLayout(List<string> active)
        {
            if (active.Count == 0)
            {
                _root = null;
                return;
            }

            if (_root == null)
            {
                _root = NewLeaf();
                _root.tabs.AddRange(active);
                _root.selected = active[0];
                return;
            }

            PruneInactive(_root, active);
            _root = CollapseEmpty(_root);

            if (_root != null)
                _root.parent = null;

            if (_root == null)
            {
                _root = NewLeaf();
                _root.tabs.AddRange(active);
                _root.selected = active[0];
                return;
            }

            for (int i = 0; i < active.Count; ++i)
            {
                string id = active[i];

                if (FindLeafContaining(_root, id) != null)
                    continue;

                var leaf = FirstLeaf(_root);
                leaf.tabs.Add(id);

                if (string.IsNullOrEmpty(leaf.selected))
                    leaf.selected = id;
            }

            ValidateSelections(_root);
        }

        Node NewLeaf()
        {
            return new Node
            {
                id = _nextNodeId++,
                ratio = 0.5f
            };
        }

        Node NewSplit(Node first, Node second, bool horizontal, float ratio)
        {
            var node = new Node
            {
                id = _nextNodeId++,
                first = first,
                second = second,
                horizontal = horizontal,
                ratio = Mathf.Clamp01(ratio)
            };

            first.parent = node;
            second.parent = node;
            return node;
        }

        static bool Contains(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; ++i)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }

        static void PruneInactive(Node node, List<string> active)
        {
            if (node == null)
                return;

            if (!node.isLeaf)
            {
                PruneInactive(node.first, active);
                PruneInactive(node.second, active);
                return;
            }

            for (int i = node.tabs.Count - 1; i >= 0; --i)
            {
                if (!Contains(active, node.tabs[i]))
                    node.tabs.RemoveAt(i);
            }
        }

        static Node CollapseEmpty(Node node)
        {
            if (node == null)
                return null;

            if (node.isLeaf)
                return node.tabs.Count == 0 ? null : node;

            node.first = CollapseEmpty(node.first);
            node.second = CollapseEmpty(node.second);

            if (node.first != null)
                node.first.parent = node;

            if (node.second != null)
                node.second.parent = node;

            if (node.first == null && node.second == null)
                return null;

            if (node.first == null)
                return node.second;

            if (node.second == null)
                return node.first;

            return node;
        }

        static void ValidateSelections(Node node)
        {
            if (node == null)
                return;

            if (!node.isLeaf)
            {
                ValidateSelections(node.first);
                ValidateSelections(node.second);
                return;
            }

            if (node.tabs.Count == 0)
            {
                node.selected = null;
                return;
            }

            if (!Contains(node.tabs, node.selected))
                node.selected = node.tabs[0];
        }

        static Node FirstLeaf(Node node)
        {
            while (node != null && !node.isLeaf)
                node = node.first;

            return node;
        }

        static Node FindLeafContaining(Node node, string windowId)
        {
            if (node == null)
                return null;

            if (node.isLeaf)
                return Contains(node.tabs, windowId) ? node : null;

            return FindLeafContaining(node.first, windowId) ?? FindLeafContaining(node.second, windowId);
        }

        void RemoveWindowFromTree(string windowId)
        {
            var leaf = FindLeafContaining(_root, windowId);

            if (leaf == null)
                return;

            leaf.tabs.Remove(windowId);

            if (leaf.selected == windowId)
                leaf.selected = leaf.tabs.Count > 0 ? leaf.tabs[0] : null;

            _root = CollapseEmpty(_root);

            if (_root != null)
                _root.parent = null;
        }

        void DockWindowToLeaf(string windowId, Node target, NowDockSide side)
        {
            if (target == null || !_windows.TryGetValue(windowId, out var window))
                return;

            window.floating = false;

            if (side == NowDockSide.Center)
            {
                MoveWindowToLeaf(windowId, target, -1);
                target.selected = windowId;
                NowControlState.RequestRepaint();
                return;
            }

            var source = FindLeafContaining(_root, windowId);

            if (source == target && target.tabs.Count == 1)
                return;

            if (source != null)
            {
                source.tabs.Remove(windowId);

                if (source.selected == windowId)
                    source.selected = source.tabs.Count > 0 ? source.tabs[0] : null;
            }

            var docked = NewLeaf();
            docked.tabs.Add(windowId);
            docked.selected = windowId;

            bool horizontal = side == NowDockSide.Left || side == NowDockSide.Right;
            bool dockBefore = side == NowDockSide.Left || side == NowDockSide.Top;
            Node first = dockBefore ? docked : target;
            Node second = dockBefore ? target : docked;
            Node targetParent = target.parent;
            var split = NewSplit(first, second, horizontal, 0.5f);

            if (targetParent == null)
            {
                _root = split;
                split.parent = null;
            }
            else
            {
                if (targetParent.first == target)
                    targetParent.first = split;
                else
                    targetParent.second = split;

                split.parent = targetParent;
            }

            _root = CollapseEmpty(_root);

            if (_root != null)
                _root.parent = null;

            NowControlState.RequestRepaint();
        }

        void MoveWindowToLeaf(string windowId, Node target, int insertIndex)
        {
            if (_windows.TryGetValue(windowId, out var window))
                window.floating = false;

            var source = FindLeafContaining(_root, windowId);
            int sourceIndex = source != null ? source.tabs.IndexOf(windowId) : -1;

            if (source == target)
            {
                if (sourceIndex < 0)
                    return;

                if (insertIndex < 0)
                    insertIndex = target.tabs.Count;
                else if (insertIndex > sourceIndex)
                    --insertIndex;

                insertIndex = Mathf.Clamp(insertIndex, 0, target.tabs.Count - 1);

                if (insertIndex == sourceIndex)
                {
                    target.selected = windowId;
                    return;
                }

                target.tabs.RemoveAt(sourceIndex);
                target.tabs.Insert(Mathf.Clamp(insertIndex, 0, target.tabs.Count), windowId);
                target.selected = windowId;
                NowControlState.RequestRepaint();
                return;
            }

            if (source != null)
            {
                source.tabs.RemoveAt(sourceIndex);

                if (source.selected == windowId)
                    source.selected = source.tabs.Count > 0 ? source.tabs[0] : null;
            }

            if (!Contains(target.tabs, windowId))
            {
                if (insertIndex < 0)
                    insertIndex = target.tabs.Count;

                target.tabs.Insert(Mathf.Clamp(insertIndex, 0, target.tabs.Count), windowId);
            }

            _root = CollapseEmpty(_root);

            if (_root != null)
                _root.parent = null;

            target.selected = windowId;
            NowControlState.RequestRepaint();
        }

        static void LayoutNode(Node node, NowRect rect, Style style)
        {
            node.rect = rect;

            if (node.isLeaf)
            {
                float tabHeight = Mathf.Min(style.tabHeight, Mathf.Max(0f, rect.height));
                node.contentRect = new NowRect(
                    rect.x,
                    rect.y + tabHeight,
                    rect.width,
                    Mathf.Max(0f, rect.height - tabHeight));
                node.splitterRect = default;
                return;
            }

            float splitter = style.splitterSize;

            if (node.horizontal)
            {
                float usable = Mathf.Max(1f, rect.width - splitter);
                node.ratio = ClampRatio(node.ratio, usable, style.minPaneSize);
                float firstWidth = Mathf.Round(usable * node.ratio);

                var firstRect = new NowRect(rect.x, rect.y, firstWidth, rect.height);
                node.splitterRect = new NowRect(rect.x + firstWidth, rect.y, splitter, rect.height);
                var secondRect = new NowRect(node.splitterRect.xMax, rect.y, rect.width - firstWidth - splitter, rect.height);

                LayoutNode(node.first, firstRect, style);
                LayoutNode(node.second, secondRect, style);
            }
            else
            {
                float usable = Mathf.Max(1f, rect.height - splitter);
                node.ratio = ClampRatio(node.ratio, usable, style.minPaneSize);
                float firstHeight = Mathf.Round(usable * node.ratio);

                var firstRect = new NowRect(rect.x, rect.y, rect.width, firstHeight);
                node.splitterRect = new NowRect(rect.x, rect.y + firstHeight, rect.width, splitter);
                var secondRect = new NowRect(rect.x, node.splitterRect.yMax, rect.width, rect.height - firstHeight - splitter);

                LayoutNode(node.first, firstRect, style);
                LayoutNode(node.second, secondRect, style);
            }
        }

        static float ClampRatio(float ratio, float usable, float minPaneSize)
        {
            if (usable <= minPaneSize * 2f)
                return 0.5f;

            float min = Mathf.Clamp01(minPaneSize / usable);
            return Mathf.Clamp(ratio, min, 1f - min);
        }

        void DrawNode(Node node, int controlId, Style style, NowThemeAsset theme)
        {
            if (node.isLeaf)
            {
                DrawLeaf(node, controlId, style, theme);
                return;
            }

            DrawNode(node.first, controlId, style, theme);
            DrawNode(node.second, controlId, style, theme);
            DrawSplitter(node, controlId, style, theme);
        }

        void DrawSplitter(Node node, int controlId, Style style, NowThemeAsset theme)
        {
            int splitterId = NowInput.CombineId(NowInput.CombineId(controlId, SplitterSeed), node.id);
            var hitRect = node.horizontal
                ? node.splitterRect.Outset(3f, 0f)
                : node.splitterRect.Outset(0f, 3f);
            var interaction = NowInput.Interact(splitterId, hitRect);

            if (interaction.dragging)
            {
                float usable = node.horizontal
                    ? Mathf.Max(1f, node.rect.width - style.splitterSize)
                    : Mathf.Max(1f, node.rect.height - style.splitterSize);
                float delta = node.horizontal ? interaction.dragDelta.x : interaction.dragDelta.y;
                node.ratio = ClampRatio(node.ratio + delta / usable, usable, style.minPaneSize);
                NowControlState.RequestRepaint();
            }
        }

        void DrawLeaf(Node leaf, int controlId, Style style, NowThemeAsset theme)
        {
            ValidateSelections(leaf);

            Now.Rectangle(leaf.rect)
                .SetColor(DockPanel)
                .SetRadius(5f)
                .Draw();

            var tabBar = new NowRect(leaf.rect.x, leaf.rect.y, leaf.rect.width, Mathf.Min(style.tabHeight, leaf.rect.height));

            Now.Rectangle(tabBar)
                .SetColor(DockTabBar)
                .SetRadius(TopTabRadius(5f))
                .Draw();

            Now.Rectangle(new NowRect(tabBar.x, tabBar.yMax - 1f, tabBar.width, 1f))
                .SetColor(DockBorder)
                .Draw();

            DrawTabs(leaf, tabBar, controlId, style, theme);

            if (leaf.tabs.Count > 0 && !string.IsNullOrEmpty(leaf.selected) &&
                _windows.TryGetValue(leaf.selected, out var selected) && selected.draw != null)
            {
                var contentRect = leaf.contentRect.Inset(style.contentPadding);

                if (contentRect.width > 0f && contentRect.height > 0f)
                {
                    using (Now.Mask(contentRect))
                    using (NowLayout.Area(NowInput.CombineId(NowInput.CombineId(controlId, ContentSeed), selected.idHash), contentRect))
                    {
                        selected.draw(contentRect);
                    }
                }
            }

            DrawPanelOutline(leaf.rect, 5f);
        }

        void DrawTabs(Node leaf, NowRect tabBar, int controlId, Style style, NowThemeAsset theme)
        {
            float x = tabBar.x;
            float overflowLimit = tabBar.xMax - 4f;

            for (int i = 0; i < leaf.tabs.Count; ++i)
            {
                string windowId = leaf.tabs[i];

                if (!_windows.TryGetValue(windowId, out var window))
                    continue;

                float tabWidth = Mathf.Min(TabWidth(theme, window), Mathf.Max(0f, overflowLimit - x));

                if (tabWidth <= 1f)
                    break;

                var tabRect = new NowRect(x, tabBar.y, tabWidth, tabBar.height);
                bool selected = leaf.selected == windowId;

                DrawTab(leaf, window, tabRect, selected, controlId, style, theme);
                x += tabWidth;
            }
        }

        float TabWidth(NowThemeAsset theme, WindowState window)
        {
            return TabWidth(theme, window.title, window.canClose);
        }

        static float TabWidth(NowThemeAsset theme, string title, bool canClose = false)
        {
            var text = theme.ResolveText(NowTextStyle.Button).SetFontSize(12f);
            float labelWidth = text.Measure(title).x;
            float closeWidth = canClose ? 18f : 0f;
            return Mathf.Clamp(labelWidth + closeWidth + 24f, 76f, 180f);
        }

        void DrawTab(Node leaf, WindowState window, NowRect tabRect, bool selected, int controlId, Style style, NowThemeAsset theme)
        {
            int tabId = DockTabId(controlId, window);
            var tabHitRect = tabRect.Inset(0f, 2f, 0f, 2f);
            var tabVisualRect = CompactTabVisualRect(tabRect);
            NowRect closeRect = default;
            bool closeHovered = false;
            bool closeHeld = false;

            if (window.canClose)
            {
                closeRect = new NowRect(tabVisualRect.xMax - 17f, tabVisualRect.y + 3f, 13f, Mathf.Max(12f, tabVisualRect.height - 6f));
                var closeInteraction = NowInput.Interact(NowInput.GetId(tabId, "close"), closeRect);
                closeHovered = closeInteraction.hovered;
                closeHeld = closeInteraction.held;

                if (closeInteraction.clicked)
                    _pendingCloseId = window.id;
            }

            var interaction = NowInput.Interact(tabId, tabHitRect);

            if (interaction.pressed || interaction.clicked)
                leaf.selected = window.id;

            if (interaction.dragStarted)
                BeginTabDrag(window, interaction.pointerPosition, style);

            if (interaction.dragStarted || interaction.dragging)
            {
                ApplyLiveTabDrag(window.id, interaction.pointerPosition, style, theme);
            }

            if (interaction.dragEnded)
            {
                if (_draggingWindowId == window.id)
                    CommitTabDrag(window.id, interaction.pointerPosition, style, theme);

                EndTabDrag(window.id);
            }

            float hover = NowControlState.Transition(tabId, interaction.hovered || interaction.held || selected);
            var accent = theme.GetColor(NowColorToken.Accent, new Color(1f, 0.48f, 0.25f, 1f));

            if (selected)
            {
                Now.Rectangle(new NowRect(tabVisualRect.x + 12f, tabVisualRect.yMax - 1f, Mathf.Max(0f, tabVisualRect.width - 24f), 1f))
                    .SetColor(WithAlpha(accent, 0.86f))
                    .Draw();
            }
            else if (hover > 0.01f)
            {
                Now.Rectangle(new NowRect(tabVisualRect.x + 12f, tabVisualRect.yMax - 1f, Mathf.Max(0f, tabVisualRect.width - 24f), 1f))
                    .SetColor(WithAlpha(accent, hover * 0.32f))
                    .Draw();
            }

            var textRect = tabVisualRect.Inset(9f, 0f, window.canClose ? 20f : 9f, 0f);
            DrawCenteredText(theme, textRect, window.title, NowTextStyle.Button, 12f, selected ? DockText : Color.Lerp(DockTextMuted, DockText, hover * 0.35f));

            if (window.canClose)
            {
                if (closeHovered || closeHeld)
                {
                    Now.Rectangle(closeRect.Outset(1f, 0f))
                        .SetColor(Color.Lerp(DockTabInactive, DockPanelRaised, closeHeld ? 0.9f : 0.55f))
                        .SetRadius(3f)
                        .Draw();
                }

                DrawText(theme, closeRect, "x", NowTextStyle.Button, 12f, closeHovered ? DockText : DockTextMuted);
            }
        }

        static int DockTabId(int controlId, WindowState window)
        {
            return NowInput.CombineId(NowInput.CombineId(controlId, TabSeed), window.idHash);
        }

        static int DockTabId(int controlId, string windowId)
        {
            return NowInput.CombineId(NowInput.CombineId(controlId, TabSeed), NowInput.GetId(windowId));
        }

        static NowRect CompactTabVisualRect(NowRect tabRect)
        {
            return tabRect.Inset(3f, 5f, 3f, 0f);
        }

        static Vector4 TopTabRadius(float radius)
        {
            return new Vector4(radius, 0f, radius, 0f);
        }

        void BeginTabDrag(WindowState window, Vector2 pointer, Style style)
        {
            if (window == null)
                return;

            _draggingWindowId = window.id;
            BringWindowToFront(window.id);

            var size = FloatingSize(window, style);

            if (window.floating && !window.floatingRect.isEmpty)
                _tabDragOffset = pointer - window.floatingRect.position;
            else
                _tabDragOffset = new Vector2(size.x * 0.5f, Mathf.Min(style.tabHeight * 0.5f, size.y * 0.5f));

            _tabDragOffset.x = Mathf.Clamp(_tabDragOffset.x, 12f, Mathf.Max(12f, size.x - 12f));
            _tabDragOffset.y = Mathf.Clamp(_tabDragOffset.y, 8f, Mathf.Max(8f, size.y - 8f));
            _hasTabDragOffset = true;
        }

        void EndTabDrag(string windowId)
        {
            if (_draggingWindowId != windowId)
                return;

            _draggingWindowId = null;
            _tabDragOffset = default;
            _hasTabDragOffset = false;
        }

        void BringWindowToFront(string windowId)
        {
            int index = _windowOrder.IndexOf(windowId);

            if (index < 0 || index == _windowOrder.Count - 1)
                return;

            _windowOrder.RemoveAt(index);
            _windowOrder.Add(windowId);
        }

        void ApplyLiveTabDrag(string windowId, Vector2 pointer, Style style, NowThemeAsset theme)
        {
            if (!_windows.TryGetValue(windowId, out var window) || window.hidden)
                return;

            if (window.floating)
                window.floatingRect = FloatingRectAt(pointer, window, style);

            NowControlState.RequestRepaint();
        }

        void CommitTabDrag(string windowId, Vector2 pointer, Style style, NowThemeAsset theme)
        {
            if (!_windows.TryGetValue(windowId, out var window) || window.hidden)
                return;

            if (_root == null && _dockRect.Contains(pointer))
            {
                window.floating = false;
                _root = NewLeaf();
                MoveWindowToLeaf(windowId, _root, 0);
                return;
            }

            bool wasFloating = window.floating;

            if (TryGetDropTarget(pointer, style, theme, windowId, out var target))
            {
                if (wasFloating && target.side == NowDockSide.Center)
                    target.tabIndex = -1;

                ApplyDropTarget(windowId, target);
                return;
            }

            FloatWindow(windowId, pointer, style);
        }

        void CompleteLostTabDrag(int controlId, Style style, NowThemeAsset theme)
        {
            if (string.IsNullOrEmpty(_draggingWindowId) || NowInput.isPassive)
                return;

            if (NowInput.hasContext && NowInput.IsPointerDown(NowPointerButton.Primary))
                return;

            string windowId = _draggingWindowId;

            if (NowInput.hasContext && NowInput.current.hasPointer)
                CommitTabDrag(windowId, NowInput.current.pointerPosition, style, theme);

            NowInput.ClearActiveIf(DockTabId(controlId, windowId));
            EndTabDrag(windowId);
        }

        void ApplyDropTarget(string windowId, DropTarget target)
        {
            if (target.leaf == null)
                return;

            if (target.side == NowDockSide.Center)
            {
                MoveWindowToLeaf(windowId, target.leaf, target.tabIndex);
                return;
            }

            DockWindowToLeaf(windowId, target.leaf, target.side);
        }

        static void DrawText(NowThemeAsset theme, NowRect rect, string value, NowTextStyle style, float fontSize)
        {
            DrawText(theme, rect, value, style, fontSize, false, default);
        }

        static void DrawText(NowThemeAsset theme, NowRect rect, string value, NowTextStyle style, float fontSize, Color color)
        {
            DrawText(theme, rect, value, style, fontSize, true, color);
        }

        static void DrawCenteredText(NowThemeAsset theme, NowRect rect, string value, NowTextStyle style, float fontSize, Color color)
        {
            DrawText(theme, rect, value, style, fontSize, true, color, true);
        }

        static void DrawText(NowThemeAsset theme, NowRect rect, string value, NowTextStyle style, float fontSize, bool hasColor, Color color)
        {
            DrawText(theme, rect, value, style, fontSize, hasColor, color, false);
        }

        static void DrawText(NowThemeAsset theme, NowRect rect, string value, NowTextStyle style, float fontSize, bool hasColor, Color color, bool centered)
        {
            if (rect.width <= 0f || rect.height <= 0f || string.IsNullOrEmpty(value))
                return;

            var text = theme.ResolveText(style).SetFontSize(fontSize);

            if (hasColor)
                text = text.SetColor(color);

            var size = text.Measure(value);
            float width = centered ? Mathf.Min(rect.width, size.x + 1f) : rect.width;
            float x = centered ? rect.x + Mathf.Max(0f, (rect.width - width) * 0.5f) : rect.x;
            var textRect = new NowRect(
                x,
                rect.y + Mathf.Max(0f, (rect.height - size.y) * 0.5f),
                width,
                size.y + 2f);

            text.SetPosition(textRect)
                .SetMask(rect.Outset(2f, 4f))
                .Draw(value);
        }

        bool TryGetDropTarget(Vector2 pointer, Style style, NowThemeAsset theme, string draggingWindowId, out DropTarget target)
        {
            target = default;

            if (_root == null)
                return false;

            _leaves.Clear();
            CollectLeaves(_root, _leaves);

            for (int i = _leaves.Count - 1; i >= 0; --i)
            {
                var leaf = _leaves[i];

                if (!leaf.rect.Contains(pointer))
                    continue;

                if (!TryResolveDropSide(leaf, pointer, style, out var side))
                    continue;

                target.leaf = leaf;
                target.side = side;
                target.tabIndex = target.side == NowDockSide.Center
                    ? ResolveTabInsertIndex(leaf, pointer, theme, draggingWindowId)
                    : -1;
                return true;
            }

            return false;
        }

        static void CollectLeaves(Node node, List<Node> leaves)
        {
            if (node == null)
                return;

            if (node.isLeaf)
            {
                leaves.Add(node);
                return;
            }

            CollectLeaves(node.first, leaves);
            CollectLeaves(node.second, leaves);
        }

        static bool TryResolveDropSide(Node leaf, Vector2 pointer, Style style, out NowDockSide side)
        {
            side = NowDockSide.Center;

            var rect = leaf.rect;
            float tabHeight = Mathf.Min(style.tabHeight, Mathf.Max(0f, rect.height));
            var tabBar = new NowRect(rect.x, rect.y, rect.width, tabHeight);

            if (tabBar.Contains(pointer))
            {
                side = NowDockSide.Center;
                return true;
            }

            var body = new NowRect(rect.x, rect.y + tabHeight, rect.width, Mathf.Max(0f, rect.height - tabHeight));

            if (body.isEmpty)
                body = rect;

            float edgeX = Mathf.Min(78f, body.width * 0.24f);
            float edgeY = Mathf.Min(78f, body.height * 0.24f);
            float localX = pointer.x - body.x;
            float localY = pointer.y - body.y;
            float bestDistance = float.PositiveInfinity;

            ConsiderEdge(localY, edgeY, NowDockSide.Top, ref side, ref bestDistance);
            ConsiderEdge(body.height - localY, edgeY, NowDockSide.Bottom, ref side, ref bestDistance);
            ConsiderEdge(localX, edgeX, NowDockSide.Left, ref side, ref bestDistance);
            ConsiderEdge(body.width - localX, edgeX, NowDockSide.Right, ref side, ref bestDistance);

            return !float.IsPositiveInfinity(bestDistance);
        }

        static void ConsiderEdge(float distance, float limit, NowDockSide candidate, ref NowDockSide side, ref float bestDistance)
        {
            if (distance >= 0f && distance <= limit && distance < bestDistance)
            {
                side = candidate;
                bestDistance = distance;
            }
        }

        int ResolveTabInsertIndex(Node leaf, Vector2 pointer, NowThemeAsset theme, string draggingWindowId)
        {
            if (leaf == null)
                return 0;

            float x = leaf.rect.x;
            float overflowLimit = leaf.rect.xMax - 4f;
            int insertIndex = 0;

            for (int i = 0; i < leaf.tabs.Count; ++i)
            {
                string tabId = leaf.tabs[i];

                if (tabId == draggingWindowId)
                    continue;

                if (!_windows.TryGetValue(tabId, out var window))
                    continue;

                float tabWidth = Mathf.Min(TabWidth(theme, window), Mathf.Max(0f, overflowLimit - x));

                if (tabWidth <= 1f)
                    break;

                if (pointer.x < x + tabWidth * 0.5f)
                    return insertIndex;

                x += tabWidth;
                ++insertIndex;
            }

            return insertIndex;
        }

        float ResolveTabInsertX(Node leaf, int insertIndex, NowThemeAsset theme, string draggingWindowId)
        {
            if (leaf == null)
                return 0f;

            float x = leaf.rect.x + 3f;
            float overflowLimit = leaf.rect.xMax - 4f;
            int visibleIndex = 0;

            for (int i = 0; i < leaf.tabs.Count; ++i)
            {
                string tabId = leaf.tabs[i];

                if (tabId == draggingWindowId)
                    continue;

                if (visibleIndex >= insertIndex)
                    return x;

                if (!_windows.TryGetValue(tabId, out var window))
                    continue;

                float tabWidth = Mathf.Min(TabWidth(theme, window), Mathf.Max(0f, overflowLimit - x));

                if (tabWidth <= 1f)
                    break;

                x += tabWidth;
                ++visibleIndex;
            }

            return Mathf.Clamp(x, leaf.rect.x + 3f, leaf.rect.xMax - 4f);
        }

        static NowRect DropGuideRect(NowRect rect, NowDockSide side)
        {
            const float Inset = 5f;

            switch (side)
            {
                case NowDockSide.Left:
                    return new NowRect(rect.x + Inset, rect.y + Inset, rect.width * 0.5f - Inset, rect.height - Inset * 2f);
                case NowDockSide.Right:
                    return new NowRect(rect.x + rect.width * 0.5f, rect.y + Inset, rect.width * 0.5f - Inset, rect.height - Inset * 2f);
                case NowDockSide.Top:
                    return new NowRect(rect.x + Inset, rect.y + Inset, rect.width - Inset * 2f, rect.height * 0.5f - Inset);
                case NowDockSide.Bottom:
                    return new NowRect(rect.x + Inset, rect.y + rect.height * 0.5f, rect.width - Inset * 2f, rect.height * 0.5f - Inset);
                default:
                    return rect.Inset(Inset);
            }
        }

        static NowRect DropGuideEdgeRect(NowRect rect, NowDockSide side)
        {
            const float Thickness = 4f;
            const float Inset = 7f;

            switch (side)
            {
                case NowDockSide.Left:
                    return new NowRect(rect.x + Inset, rect.y + Inset, Thickness, Mathf.Max(0f, rect.height - Inset * 2f));
                case NowDockSide.Right:
                    return new NowRect(rect.xMax - Inset - Thickness, rect.y + Inset, Thickness, Mathf.Max(0f, rect.height - Inset * 2f));
                case NowDockSide.Top:
                    return new NowRect(rect.x + Inset, rect.y + Inset, Mathf.Max(0f, rect.width - Inset * 2f), Thickness);
                case NowDockSide.Bottom:
                    return new NowRect(rect.x + Inset, rect.yMax - Inset - Thickness, Mathf.Max(0f, rect.width - Inset * 2f), Thickness);
                default:
                    return rect.Inset(Inset);
            }
        }

        void DrawFloatingWindows(int controlId, Style style, NowThemeAsset theme)
        {
            for (int i = 0; i < _windowOrder.Count; ++i)
            {
                if (!_windows.TryGetValue(_windowOrder[i], out var window) ||
                    !window.submitted || window.hidden || !window.floating || window.draw == null)
                {
                    continue;
                }

                if (window.floatingRect.isEmpty)
                    window.floatingRect = FloatingRectAt(_dockRect.center, window, style);

                int windowControlId = NowInput.CombineId(NowInput.CombineId(controlId, FloatingSeed), window.idHash);
                var draw = window.draw;
                string title = window.title;
                bool canClose = window.canClose;

                if (ShouldUseTabOnlyDragFeedback(window, style, theme))
                {
                    ProcessFloatingTabDrag(controlId, window, title, canClose, style, theme);
                    continue;
                }

                var blockRect = window.floatingRect.Outset(2f);

                NowOverlay.Defer(blockRect, () =>
                {
                    DrawFloatingWindow(controlId, windowControlId, window, title, canClose, draw, style, theme);
                });
            }
        }

        void DrawTabDragFeedback(int controlId, Style style, NowThemeAsset theme)
        {
            if (string.IsNullOrEmpty(_draggingWindowId) || NowInput.isPassive || !NowInput.current.hasPointer ||
                !_windows.TryGetValue(_draggingWindowId, out var window) || window.hidden)
            {
                return;
            }

            var pointer = NowInput.current.pointerPosition;
            bool hasTarget = TryGetDropTarget(pointer, style, theme, window.id, out var target);

            if (hasTarget)
            {
                NowOverlay.Defer(default, () =>
                {
                    DrawDropGuide(target, window.id, style, theme);
                });
            }

            if (hasTarget && target.side == NowDockSide.Center)
            {
                var pill = DraggedTabPillRect(pointer, window.title, style, theme);

                NowOverlay.Defer(default, () =>
                {
                    DrawDraggedTabPill(pill, window.title, theme);
                });
                return;
            }

            if (window.floating || window.draw == null)
                return;

            var ghostRect = FloatingRectAt(pointer, window, style);
            var draw = window.draw;
            string title = window.title;

            NowOverlay.Defer(default, () =>
            {
                DrawDragGhostWindow(controlId, ghostRect, title, draw, style, theme);
            });
        }

        bool ShouldUseTabOnlyDragFeedback(WindowState window, Style style, NowThemeAsset theme)
        {
            if (window == null || _draggingWindowId != window.id || NowInput.isPassive || !NowInput.current.hasPointer)
                return false;

            return TryGetDropTarget(NowInput.current.pointerPosition, style, theme, window.id, out var target) &&
                target.side == NowDockSide.Center;
        }

        void ProcessFloatingTabDrag(
            int dockControlId,
            WindowState window,
            string title,
            bool canClose,
            Style style,
            NowThemeAsset theme)
        {
            if (window == null)
                return;

            var rect = window.floatingRect;

            if (rect.isEmpty)
                rect = window.floatingRect = FloatingRectAt(_dockRect.center, window, style);

            var titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
            float closeSpace = canClose ? 28f : 6f;
            float tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - closeSpace - 44f));
            var tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
            var tabInteraction = NowInput.Interact(DockTabId(dockControlId, window), tabRect);

            if (tabInteraction.dragStarted)
                BeginTabDrag(window, tabInteraction.pointerPosition, style);

            if (tabInteraction.dragStarted || tabInteraction.dragging)
                ApplyLiveTabDrag(window.id, tabInteraction.pointerPosition, style, theme);

            if (tabInteraction.dragEnded)
            {
                if (_draggingWindowId == window.id)
                    CommitTabDrag(window.id, tabInteraction.pointerPosition, style, theme);

                EndTabDrag(window.id);
            }
        }

        void DrawDropGuide(DropTarget target, string draggingWindowId, Style style, NowThemeAsset theme)
        {
            if (target.leaf == null)
                return;

            var accent = theme.GetColor(NowColorToken.Accent, new Color(1f, 0.48f, 0.25f, 1f));

            if (target.side == NowDockSide.Center)
            {
                float tabHeight = Mathf.Min(style.tabHeight, target.leaf.rect.height);
                var tabBar = new NowRect(target.leaf.rect.x, target.leaf.rect.y, target.leaf.rect.width, tabHeight);
                float x = ResolveTabInsertX(target.leaf, target.tabIndex, theme, draggingWindowId);

                Now.Rectangle(new NowRect(tabBar.x + 6f, tabBar.yMax - 2f, Mathf.Max(0f, tabBar.width - 12f), 1f))
                    .SetColor(WithAlpha(accent, 0.35f))
                    .SetRadius(1f)
                    .Draw();

                Now.Rectangle(new NowRect(x - 1f, tabBar.y + 4f, 2f, Mathf.Max(6f, tabBar.height - 8f)))
                    .SetColor(accent)
                    .SetRadius(1f)
                    .Draw();
                return;
            }

            var guide = DropGuideRect(target.leaf.rect, target.side);
            var edge = DropGuideEdgeRect(target.leaf.rect, target.side);

            Now.Rectangle(guide)
                .SetColor(WithAlpha(accent, 0.07f))
                .SetRadius(3f)
                .Draw();

            Now.Rectangle(edge)
                .SetColor(WithAlpha(accent, 0.9f))
                .SetRadius(2f)
                .Draw();
        }

        NowRect DraggedTabPillRect(Vector2 pointer, string title, Style style, NowThemeAsset theme)
        {
            float width = TabWidth(theme, title);
            float height = Mathf.Max(18f, style.tabHeight - 6f);
            float xOffset = _hasTabDragOffset ? Mathf.Clamp(_tabDragOffset.x, 12f, width - 12f) : width * 0.5f;
            return new NowRect(pointer.x - xOffset, pointer.y - height * 0.5f, width, height);
        }

        void DrawDraggedTabPill(NowRect rect, string title, NowThemeAsset theme)
        {
            var accent = theme.GetColor(NowColorToken.Accent, new Color(1f, 0.48f, 0.25f, 1f));

            Now.Rectangle(rect)
                .SetColor(WithAlpha(DockPanelRaised, 0.72f))
                .SetRadius(TopTabRadius(4f))
                .Draw();

            Now.Rectangle(new NowRect(rect.x + 8f, rect.yMax - 2f, Mathf.Max(0f, rect.width - 16f), 2f))
                .SetColor(accent)
                .SetRadius(1f)
                .Draw();

            DrawCenteredText(theme, rect.Inset(10f, 0f), title, NowTextStyle.Button, 12f, DockText);
        }

        void DrawDragGhostWindow(
            int controlId,
            NowRect rect,
            string title,
            Action<NowRect> draw,
            Style style,
            NowThemeAsset theme)
        {
            Now.Rectangle(rect)
                .SetColor(WithAlpha(DockPanel, 0.94f))
                .SetRadius(6f)
                .Draw();

            var titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
            var accent = theme.GetColor(NowColorToken.Accent, new Color(1f, 0.48f, 0.25f, 1f));

            Now.Rectangle(titleRect)
                .SetColor(WithAlpha(DockTabBar, 0.98f))
                .SetRadius(TopTabRadius(6f))
                .Draw();

            float tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - 12f));
            var tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
            var tabVisualRect = CompactTabVisualRect(tabRect);

            Now.Rectangle(tabVisualRect)
                .SetColor(WithAlpha(DockPanelRaised, 0.68f))
                .SetRadius(TopTabRadius(4f))
                .Draw();

            Now.Rectangle(new NowRect(tabVisualRect.x + 8f, tabVisualRect.yMax - 2f, Mathf.Max(0f, tabVisualRect.width - 16f), 2f))
                .SetColor(accent)
                .SetRadius(1f)
                .Draw();

            DrawCenteredText(theme, tabVisualRect.Inset(9f, 0f), title, NowTextStyle.Button, 12f, DockText);

            var contentRect = new NowRect(
                rect.x,
                rect.y + titleRect.height,
                rect.width,
                Mathf.Max(0f, rect.height - titleRect.height)).Inset(style.contentPadding);

            if (contentRect.width > 0f && contentRect.height > 0f && draw != null)
            {
                int dragContentId = NowInput.CombineId(
                    NowInput.CombineId(controlId, DragContentSeed),
                    NowInput.GetId(_draggingWindowId));

                using (Now.Mask(contentRect))
                using (NowLayout.Area(dragContentId, contentRect))
                {
                    draw(contentRect);
                }
            }

            DrawPanelOutline(rect, 6f);
        }

        void DrawFloatingWindow(
            int dockControlId,
            int controlId,
            WindowState window,
            string title,
            bool canClose,
            Action<NowRect> draw,
            Style style,
            NowThemeAsset theme)
        {
            var rect = window.floatingRect;

            if (rect.isEmpty)
                rect = window.floatingRect = FloatingRectAt(_dockRect.center, window, style);

            var titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
            float closeSpace = canClose ? 28f : 6f;
            float tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - closeSpace - 44f));
            var tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
            var moveRect = new NowRect(
                tabRect.xMax,
                titleRect.y,
                Mathf.Max(0f, titleRect.xMax - closeSpace - tabRect.xMax),
                titleRect.height);
            var titleInteraction = NowInput.Interact(NowInput.GetId(controlId, "move-title"), moveRect);
            var tabInteraction = NowInput.Interact(DockTabId(dockControlId, window), tabRect);

            if (titleInteraction.pressed)
                BringWindowToFront(window.id);

            if (tabInteraction.dragStarted)
                BeginTabDrag(window, tabInteraction.pointerPosition, style);

            if (tabInteraction.dragStarted || tabInteraction.dragging)
            {
                ApplyLiveTabDrag(window.id, tabInteraction.pointerPosition, style, theme);

                if (!window.floating)
                    return;
            }

            if (tabInteraction.dragEnded)
            {
                if (_draggingWindowId == window.id)
                    CommitTabDrag(window.id, tabInteraction.pointerPosition, style, theme);

                EndTabDrag(window.id);

                if (!window.floating)
                    return;
            }

            rect = window.floatingRect;
            titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
            closeSpace = canClose ? 28f : 6f;
            tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - closeSpace - 44f));
            tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
            moveRect = new NowRect(
                tabRect.xMax,
                titleRect.y,
                Mathf.Max(0f, titleRect.xMax - closeSpace - tabRect.xMax),
                titleRect.height);

            if (titleInteraction.dragging)
            {
                window.floatingRect = ClampFloatingRectToScreen(rect.Offset(titleInteraction.dragDelta));
                rect = window.floatingRect;
                titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
                closeSpace = canClose ? 28f : 6f;
                tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - closeSpace - 44f));
                tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
                moveRect = new NowRect(
                    tabRect.xMax,
                    titleRect.y,
                    Mathf.Max(0f, titleRect.xMax - closeSpace - tabRect.xMax),
                    titleRect.height);
                NowControlState.RequestRepaint();
            }

            var resizeRect = new NowRect(rect.xMax - 16f, rect.yMax - 16f, 16f, 16f);
            var resize = NowInput.Interact(NowInput.GetId(controlId, "resize"), resizeRect);

            if (resize.dragging)
            {
                window.floatingRect = ClampFloatingRectToScreen(new NowRect(
                    rect.x,
                    rect.y,
                    Mathf.Max(style.minPaneSize, rect.width + resize.dragDelta.x),
                    Mathf.Max(style.minPaneSize, rect.height + resize.dragDelta.y)));
                rect = window.floatingRect;
                titleRect = new NowRect(rect.x, rect.y, rect.width, Mathf.Min(style.tabHeight, rect.height));
                closeSpace = canClose ? 28f : 6f;
                tabWidth = Mathf.Min(TabWidth(theme, title), Mathf.Max(48f, titleRect.width - closeSpace - 44f));
                tabRect = new NowRect(titleRect.x, titleRect.y, tabWidth, titleRect.height);
                moveRect = new NowRect(
                    tabRect.xMax,
                    titleRect.y,
                    Mathf.Max(0f, titleRect.xMax - closeSpace - tabRect.xMax),
                    titleRect.height);
                resizeRect = new NowRect(rect.xMax - 16f, rect.yMax - 16f, 16f, 16f);
                NowControlState.RequestRepaint();
            }

            bool closeHovered = false;
            bool closeHeld = false;
            NowRect closeRect = default;

            if (canClose)
            {
                closeRect = new NowRect(titleRect.xMax - 22f, titleRect.y + 5f, 16f, titleRect.height - 10f);
                var closeInteraction = NowInput.Interact(NowInput.GetId(controlId, "close"), closeRect);
                closeHovered = closeInteraction.hovered;
                closeHeld = closeInteraction.held;

                if (closeInteraction.clicked)
                    _pendingCloseId = window.id;
            }

            Now.Rectangle(rect)
                .SetColor(DockPanel)
                .SetRadius(6f)
                .Draw();

            var titleColor = Color.Lerp(DockTabBar, DockPanelRaised, titleInteraction.hovered ? 0.35f : 0f);
            Now.Rectangle(titleRect)
                .SetColor(titleColor)
                .SetRadius(TopTabRadius(6f))
                .Draw();

            var accent = theme.GetColor(NowColorToken.Accent, new Color(1f, 0.48f, 0.25f, 1f));
            float tabHover = NowControlState.Transition(DockTabId(dockControlId, window), tabInteraction.hovered || tabInteraction.held || _draggingWindowId == window.id);
            var tabVisualRect = CompactTabVisualRect(tabRect);

            Now.Rectangle(tabVisualRect)
                .SetColor(WithAlpha(DockPanelRaised, Mathf.Lerp(0.52f, 0.68f, tabHover)))
                .SetRadius(TopTabRadius(4f))
                .Draw();

            Now.Rectangle(new NowRect(tabVisualRect.x + 8f, tabVisualRect.yMax - 2f, Mathf.Max(0f, tabVisualRect.width - 16f), 2f))
                .SetColor(WithAlpha(accent, Mathf.Lerp(0.85f, 1f, tabHover)))
                .SetRadius(1f)
                .Draw();

            DrawCenteredText(theme, tabVisualRect.Inset(9f, 0f), title, NowTextStyle.Button, 12f, DockText);

            if (canClose)
            {
                if (closeHovered || closeHeld)
                {
                    Now.Rectangle(closeRect.Outset(1f, 0f))
                        .SetColor(Color.Lerp(DockTabInactive, DockPanelRaised, closeHeld ? 0.9f : 0.55f))
                        .SetRadius(3f)
                        .Draw();
                }

                DrawText(theme, closeRect, "x", NowTextStyle.Button, 12f, closeHovered ? DockText : DockTextMuted);
            }

            if (resize.hovered || resize.held)
            {
                Now.Rectangle(resizeRect.Inset(4f, 4f, 2f, 2f))
                    .SetColor(accent)
                    .SetRadius(2f)
                    .Draw();
            }

            var contentRect = new NowRect(
                rect.x,
                rect.y + titleRect.height,
                rect.width,
                Mathf.Max(0f, rect.height - titleRect.height)).Inset(style.contentPadding);

            if (contentRect.width > 0f && contentRect.height > 0f && draw != null)
            {
                using (Now.Mask(contentRect))
                using (NowLayout.Area(NowInput.CombineId(NowInput.CombineId(controlId, ContentSeed), window.idHash), contentRect))
                {
                    draw(contentRect);
                }
            }

            DrawPanelOutline(rect, 6f);
        }

        void FloatWindow(string windowId, Vector2 pointer, Style style)
        {
            if (!_windows.TryGetValue(windowId, out var window) || window.hidden)
                return;

            RemoveWindowFromTree(windowId);
            window.floating = true;
            window.floatingRect = FloatingRectAt(pointer, window, style);
            NowControlState.RequestRepaint();
        }

        NowRect FloatingRectAt(Vector2 pointer, WindowState window, Style style)
        {
            var size = FloatingSize(window, style);
            var offset = _draggingWindowId == window.id && _hasTabDragOffset
                ? _tabDragOffset
                : new Vector2(size.x * 0.5f, Mathf.Min(style.tabHeight * 0.5f, size.y * 0.5f));

            return ClampFloatingRectToScreen(new NowRect(
                pointer.x - offset.x,
                pointer.y - offset.y,
                size.x,
                size.y));
        }

        Vector2 FloatingSize(WindowState window, Style style)
        {
            if (window != null && !window.floatingRect.isEmpty)
                return window.floatingRect.size;

            return new Vector2(
                Mathf.Clamp(_dockRect.width * 0.36f, 240f, 360f),
                Mathf.Clamp(_dockRect.height * 0.46f, Mathf.Max(160f, style.minPaneSize), 280f));
        }

        static NowRect ClampFloatingRectToScreen(NowRect rect)
        {
            var screen = Now.screenMask;

            if (screen.width <= 1f || screen.height <= 1f)
                return rect;

            float width = Mathf.Min(rect.width, screen.width);
            float height = Mathf.Min(rect.height, screen.height);
            float x = Mathf.Clamp(rect.x, screen.x, Mathf.Max(screen.x, screen.xMax - width));
            float y = Mathf.Clamp(rect.y, screen.y, Mathf.Max(screen.y, screen.yMax - height));
            return new NowRect(x, y, width, height);
        }

        static void DrawPanelOutline(NowRect rect, float radius)
        {
            Now.Rectangle(rect)
                .SetColor(WithAlpha(DockPanel, 0f))
                .SetRadius(radius)
                .SetOutline(1f)
                .SetOutlineColor(DockBorder)
                .Draw();
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        internal static Style DefaultStyle()
        {
            return new Style
            {
                tabHeight = DefaultTabHeight,
                splitterSize = DefaultSplitterSize,
                minPaneSize = DefaultMinPaneSize,
                contentPadding = DefaultContentPadding,
                drawBackground = true
            };
        }
    }

    [NowBuilder]
    public struct NowDockSpaceBuilder
    {
        readonly NowDockSpace _space;
        readonly NowRect _rect;
        readonly NowId _id;
        readonly int _site;
        NowDockSpace.Style _style;

        internal NowDockSpaceBuilder(NowDockSpace space, NowRect rect, NowId id, int site)
        {
            _space = space;
            _rect = rect;
            _id = id;
            _site = site;
            _style = NowDockSpace.DefaultStyle();
        }

        public NowDockSpaceBuilder SetTabHeight(float value)
        {
            _style.tabHeight = Mathf.Max(18f, value);
            return this;
        }

        public NowDockSpaceBuilder SetSplitterSize(float value)
        {
            _style.splitterSize = Mathf.Max(1f, value);
            return this;
        }

        public NowDockSpaceBuilder SetMinPaneSize(float value)
        {
            _style.minPaneSize = Mathf.Max(32f, value);
            return this;
        }

        public NowDockSpaceBuilder SetContentPadding(float value)
        {
            _style.contentPadding = Mathf.Max(0f, value);
            return this;
        }

        public NowDockSpaceBuilder SetBackground(bool draw)
        {
            _style.drawBackground = draw;
            return this;
        }

        [NowConsumer]
        public void Draw()
        {
            if (_space == null)
                throw new InvalidOperationException("NowDock.Space requires a NowDockSpace instance.");

            int id = NowControls.GetControlId(_id, _site);

            _space.Draw(_rect, id, _style);
        }
    }

    public static class NowDock
    {
        public static NowDockSpaceBuilder Space(
            NowDockSpace space,
            NowRect rect,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowDockSpaceBuilder(space, rect, id, NowControls.SiteId(file, line));
        }
    }
}
