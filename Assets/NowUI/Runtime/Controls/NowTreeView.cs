using System.Collections.Generic;
using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Caller-owned tree state: expansion and selection live with your data, not
    /// inside NowUI. Allocate once and reuse across frames.
    /// </summary>
    public sealed class NowTreeViewState
    {
        internal readonly HashSet<int> expanded = new HashSet<int>(32);

        /// <summary>Resolved id of the selected row, or 0 when nothing is selected.</summary>
        public int selectedId { get; set; }

        public bool IsExpanded(int nodeId)
        {
            return expanded.Contains(nodeId);
        }

        public void SetExpanded(int nodeId, bool value)
        {
            if (value)
                expanded.Add(nodeId);
            else
                expanded.Remove(nodeId);
        }

        public void CollapseAll()
        {
            expanded.Clear();
        }
    }

    /// <summary>
    /// Hierarchical tree of collapsible rows flowing in the ambient layout (host
    /// it inside a ScrollView). Rows are declared immediate-mode; expansion and
    /// selection live in a caller-owned <see cref="NowTreeViewState"/>:
    /// <code>
    /// using (var tree = NowLayout.TreeView(treeState).Begin())
    /// {
    ///     if (tree.BeginNode("Assets"))
    ///     {
    ///         if (tree.Node("Readme.md")) Open("Readme.md");
    ///         tree.EndNode();
    ///     }
    /// }
    /// </code>
    /// Node identity follows the parent chain and declaration order; use the
    /// explicit-id overloads when sibling order can change.
    /// </summary>
    [NowBuilder]
    public struct NowTreeView
    {
        readonly NowTreeViewState _state;
        readonly int _site;
        NowId _id;

        int ResolveControlId() => NowControls.GetControlId(_id, _site);

        internal NowTreeView(NowTreeViewState state, int site)
        {
            _state = state;
            _site = site;
            _id = default;
        }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTreeView SetId(NowId id) { _id = id; return this; }

        public NowTreeViewScope Begin()
        {
            var frame = NowTreeFrame.Rent();
            frame.state = _state ?? frame.fallbackState;
            frame.theme = NowTheme.themeAsset;
            frame.selectionChanged = false;
            frame.pathIds.Clear();
            frame.counters.Clear();
            frame.pathIds.Add(ResolveControlId());
            frame.counters.Add(0);
            return new NowTreeViewScope(frame);
        }
    }

    sealed class NowTreeFrame
    {
        static NowTreeFrame s_pooled;

        public NowTreeViewState state;
        public NowThemeAsset theme;
        public bool selectionChanged;
        public readonly List<int> pathIds = new List<int>(8);
        public readonly List<int> counters = new List<int>(8);
        public readonly NowTreeViewState fallbackState = new NowTreeViewState();

        public static NowTreeFrame Rent()
        {
            var frame = s_pooled ?? new NowTreeFrame();
            s_pooled = null;
            return frame;
        }

        public static void Return(NowTreeFrame frame)
        {
            frame.state = null;
            frame.theme = null;
            s_pooled = frame;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            s_pooled = null;
        }
    }

    [NowScope]
    public struct NowTreeViewScope : System.IDisposable
    {
        const int DisclosureSeed = 0x4e544456;

        readonly NowTreeFrame _frame;
        bool _disposed;

        internal NowTreeViewScope(NowTreeFrame frame)
        {
            _frame = frame;
            _disposed = false;
        }

        /// <summary>The selected row id after this frame's interactions.</summary>
        public int selectedId => _frame.state.selectedId;

        /// <summary>True when a row changed the selection this frame.</summary>
        public bool selectionChanged => _frame.selectionChanged;

        /// <summary>
        /// A parent row; returns true while expanded. Draw children inside and
        /// close with <see cref="EndNode"/> only when it returned true.
        /// </summary>
        public bool BeginNode(string label)
        {
            return BeginNode(label, default);
        }

        public bool BeginNode(string label, NowId id)
        {
            int nodeId = NextNodeId(id);
            bool expanded = _frame.state.IsExpanded(nodeId);

            DrawRow(label, nodeId, hasChildren: true, ref expanded);

            if (!expanded)
                return false;

            _frame.pathIds.Add(nodeId);
            _frame.counters.Add(0);
            return true;
        }

        /// <summary>A leaf row; returns true on activation (click or submit).</summary>
        public bool Node(string label)
        {
            return Node(label, default);
        }

        public bool Node(string label, NowId id)
        {
            int nodeId = NextNodeId(id);
            bool expanded = false;
            return DrawRow(label, nodeId, hasChildren: false, ref expanded);
        }

        /// <summary>Closes the children of the last <see cref="BeginNode"/> that returned true.</summary>
        public void EndNode()
        {
            if (_frame.pathIds.Count > 1)
            {
                _frame.pathIds.RemoveAt(_frame.pathIds.Count - 1);
                _frame.counters.RemoveAt(_frame.counters.Count - 1);
            }
        }

        int NextNodeId(NowId id)
        {
            int depth = _frame.pathIds.Count - 1;
            _frame.counters[depth] = _frame.counters[depth] + 1;

            if (id.hasValue)
                return NowInput.CombineId(_frame.pathIds[depth], id.ResolveStableId(_frame.counters[depth]));

            return NowInput.CombineId(_frame.pathIds[depth], _frame.counters[depth]);
        }

        bool DrawRow(string label, int nodeId, bool hasChildren, ref bool expanded)
        {
            var theme = _frame.theme;
            var styles = theme.controlStyles;
            var renderer = theme.controlRenderer;
            int depth = _frame.pathIds.Count - 1;

            NowRect rect = NowLayout.Rect(height: styles.treeRowHeight, stretchWidth: true);

            float indent = depth * styles.treeIndentWidth;
            float disclosure = styles.treeDisclosureSize;
            var disclosureRect = new NowRect(
                rect.x + 4f + indent,
                rect.y + (rect.height - disclosure) * 0.5f,
                disclosure,
                disclosure);

            bool toggled = false;

            if (hasChildren)
            {
                var disclosureHit = disclosureRect.Outset(6f);
                var disclosureInteraction = NowInput.Interact(NowInput.CombineId(nodeId, DisclosureSeed), disclosureHit);

                if (disclosureInteraction.clicked)
                    toggled = true;
            }

            var interaction = NowControls.Interact(nodeId, rect, default, out bool focused, out bool submitted);
            bool activated = false;

            if (interaction.clicked && !toggled)
            {
                if (_frame.state.selectedId != nodeId)
                {
                    _frame.state.selectedId = nodeId;
                    _frame.selectionChanged = true;
                }

                activated = !hasChildren;
            }

            if (submitted)
            {
                if (hasChildren)
                    toggled = true;
                else
                    activated = true;
            }

            if (focused && !NowInput.isPassive && hasChildren)
            {
                float navX = NowInput.current.navigation.x;

                if (NowControlState.Repeat(nodeId, "nav-x", Mathf.Abs(navX) > 0.55f, 0.35f, 0.2f))
                {
                    if (navX > 0f && !expanded)
                        toggled = true;
                    else if (navX < 0f && expanded)
                        toggled = true;
                }
            }

            if (toggled)
            {
                expanded = !expanded;
                _frame.state.SetExpanded(nodeId, expanded);
                NowControlState.RequestRepaint();
            }

            bool selected = _frame.state.selectedId == nodeId;
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            renderer.DrawTreeRow(new NowTreeRowRenderContext(
                theme, rect, label, depth, hasChildren, expanded, selected, disclosureRect, interaction, focused, hoverT));

            return activated;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            NowTreeFrame.Return(_frame);
        }
    }
}
