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
        internal readonly HashSet<NowTreeNodeKey> expanded = new HashSet<NowTreeNodeKey>(32);

        /// <summary>Semantic key of the selected data node, or <see cref="NowTreeNodeKey.None"/>.</summary>
        public NowTreeNodeKey selectedKey { get; set; }

        public bool IsExpanded(NowTreeNodeKey nodeKey)
        {
            return expanded.Contains(nodeKey);
        }

        public void SetExpanded(NowTreeNodeKey nodeKey, bool value)
        {
            if (!nodeKey.hasValue)
                throw new System.ArgumentException("A tree node key is required.", nameof(nodeKey));

            if (value)
                expanded.Add(nodeKey);
            else
                expanded.Remove(nodeKey);
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
        NowControlIdentity _id;

        NowResolvedId ResolveControlId() => _id.Resolve(_site);

        internal NowTreeView(NowTreeViewState state, int site)
        {
            _state = state;
            _site = site;
            _id = default;
        }

        /// <summary>Explicit control id, decoupling identity from the call site.</summary>
        public NowTreeView SetId(NowId id) { _id = id; return this; }

        /// <summary>Uses an identity that was already resolved by this host.</summary>
        public NowTreeView SetId(NowResolvedId id) { _id = id; return this; }

        public NowTreeViewScope Begin()
        {
            var frame = NowTreeFrame.Rent(out int token);

            try
            {
                frame.state = _state ?? frame.fallbackState;
                frame.theme = NowTheme.themeAsset;
                frame.selectionChanged = false;
                frame.pathIds.Clear();
                frame.pathKeys.Clear();
                frame.counters.Clear();
                frame.pathIds.Add(ResolveControlId());
                frame.pathKeys.Add(NowTreeNodeKey.Root);
                frame.counters.Add(0);
                return new NowTreeViewScope(frame, token);
            }
            catch
            {
                NowTreeFrame.Return(frame, token);
                throw;
            }
        }
    }

    sealed class NowTreeFrame
    {
        static NowTreeFrame s_pooled;

        static readonly NowScopeGuard s_scopes = new NowScopeGuard("NowLayout.TreeView");

        int _leaseToken;

        public NowTreeViewState state;
        public NowThemeAsset theme;
        public bool selectionChanged;
        public readonly List<NowResolvedId> pathIds = new List<NowResolvedId>(8);
        public readonly List<NowTreeNodeKey> pathKeys = new List<NowTreeNodeKey>(8);
        public readonly List<int> counters = new List<int>(8);
        public readonly NowTreeViewState fallbackState = new NowTreeViewState();

        public static NowTreeFrame Rent(out int token)
        {
            var frame = s_pooled ?? new NowTreeFrame();
            s_pooled = null;

            token = 0;

            try
            {
                token = s_scopes.Enter();
                frame._leaseToken = token;
                return frame;
            }
            catch
            {
                if (s_pooled == null)
                    s_pooled = frame;

                throw;
            }
        }

        public static void Return(NowTreeFrame frame, int token)
        {
            // A value-type scope may have been copied. Once the original returns
            // this frame, a stale copy must not return it again after a newer tree
            // has rented it. The lease token belongs to the frame as well as the
            // handle, so reuse invalidates every old copy.
            if (frame == null || token == 0 || frame._leaseToken != token)
                return;

            // Also gives nested tree scopes the same reverse-order guarantee as
            // the other stack-backed NowUI scopes. A failed outer dispose leaves
            // its token live so it can be retried after the inner tree closes.
            if (!s_scopes.Exit(token))
                return;

            frame._leaseToken = 0;
            frame.state = null;
            frame.theme = null;
            s_pooled = frame;
        }

        public static bool IsOwned(NowTreeFrame frame, int token)
        {
            return frame != null && token != 0 && frame._leaseToken == token;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForRuntimeLoad()
        {
            s_pooled = null;
            s_scopes.Clear();
        }
    }

    [NowScope]
    public struct NowTreeViewScope : System.IDisposable
    {
        const int DisclosureSeed = 0x4e544456;

        readonly NowTreeFrame _frame;

        int _token;

        internal NowTreeViewScope(NowTreeFrame frame, int token)
        {
            _frame = frame;
            _token = token;
        }

        /// <summary>The selected row id after this frame's interactions.</summary>
        public NowTreeNodeKey selectedKey => RequireFrame().state.selectedKey;

        /// <summary>True when a row changed the selection this frame.</summary>
        public bool selectionChanged => RequireFrame().selectionChanged;

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
            RequireFrame();
            NodeAddress node = NextNode(id);
            bool expanded = _frame.state.IsExpanded(node.key);

            DrawRow(label, node, hasChildren: true, ref expanded);

            if (!expanded)
                return false;

            _frame.pathIds.Add(node.controlId);
            _frame.pathKeys.Add(node.key);
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
            RequireFrame();
            NodeAddress node = NextNode(id);
            bool expanded = false;
            return DrawRow(label, node, hasChildren: false, ref expanded);
        }

        /// <summary>Closes the children of the last <see cref="BeginNode"/> that returned true.</summary>
        public void EndNode()
        {
            RequireFrame();

            if (_frame.pathIds.Count > 1)
            {
                _frame.pathIds.RemoveAt(_frame.pathIds.Count - 1);
                _frame.pathKeys.RemoveAt(_frame.pathKeys.Count - 1);
                _frame.counters.RemoveAt(_frame.counters.Count - 1);
            }
        }

        readonly struct NodeAddress
        {
            public readonly NowTreeNodeKey key;
            public readonly NowResolvedId controlId;

            public NodeAddress(NowTreeNodeKey key, NowResolvedId controlId)
            {
                this.key = key;
                this.controlId = controlId;
            }
        }

        NodeAddress NextNode(NowId id)
        {
            int depth = _frame.pathIds.Count - 1;
            int position = _frame.counters[depth] + 1;
            _frame.counters[depth] = position;

            NowTreeNodeKey parentKey = _frame.pathKeys[depth];
            NowResolvedId parentControlId = _frame.pathIds[depth];

            if (id.hasValue)
            {
                return new NodeAddress(
                    parentKey.Child(id),
                    parentControlId.Child(id));
            }

            return new NodeAddress(
                parentKey.PositionalChild(position),
                parentControlId.Derive(NowIdDomain.Occurrence, position));
        }

        bool DrawRow(string label, NodeAddress node, bool hasChildren, ref bool expanded)
        {
            var theme = _frame.theme;
            var styles = theme.controlStyles;
            var renderer = theme.controlRenderer;
            int depth = _frame.pathIds.Count - 1;

            NowRect rect = NowLayout.ReserveRect(height: styles.treeRowHeight, stretchWidth: true);

            float indent = depth * styles.treeIndentWidth;
            float disclosure = styles.treeDisclosureSize;
            var disclosureRect = new NowRect(
                rect.x + 4f + indent,
                rect.y + (rect.height - disclosure) * 0.5f,
                disclosure,
                disclosure);

            bool toggled = false;
            var rowRegion = new NowInteractionRegion(rect);

            if (hasChildren)
            {
                var disclosureHit = disclosureRect.Outset(6f);
                rowRegion = rowRegion.Exclude(disclosureHit);
                var disclosureInteraction = NowInput.Interact(node.controlId.Child(DisclosureSeed), disclosureHit);

                if (disclosureInteraction.clicked)
                    toggled = true;
            }

            var interaction = NowControls.Interact(
                node.controlId,
                in rowRegion,
                default,
                out bool focused,
                out bool submitted);
            bool activated = false;

            if (interaction.clicked && !toggled)
            {
                if (_frame.state.selectedKey != node.key)
                {
                    _frame.state.selectedKey = node.key;
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

                if (NowControlState.Repeat(node.controlId, "nav-x", Mathf.Abs(navX) > 0.55f, 0.35f, 0.2f))
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
                _frame.state.SetExpanded(node.key, expanded);
                NowControlState.RequestRepaint();
            }

            bool selected = _frame.state.selectedKey == node.key;
            float hoverT = NowControlState.Transition(interaction, interaction.hovered || interaction.held);

            renderer.DrawTreeRow(new NowTreeRowRenderContext(
                theme, rect, label, depth, hasChildren, expanded, selected, disclosureRect, interaction, focused, hoverT));

            return activated;
        }

        public void Dispose()
        {
            if (_token == 0)
                return;

            NowTreeFrame.Return(_frame, _token);
            _token = 0;
        }

        NowTreeFrame RequireFrame()
        {
            if (!NowTreeFrame.IsOwned(_frame, _token))
                throw new System.ObjectDisposedException(nameof(NowTreeViewScope));

            return _frame;
        }
    }
}
