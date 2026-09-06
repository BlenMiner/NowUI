using System;
using System.Collections.Generic;

namespace NowUI.NodeGraph
{
    /// <summary>
    /// Computes the value a node produces for one of its output ports.
    /// Pull upstream values with <see cref="NowNodeEvalContext{T}.Input(int, T)"/>.
    /// </summary>
    public delegate T NowNodeEvalHandler<T>(NowNodeEvalContext<T> context);

    /// <summary>
    /// Evaluation state passed to a <see cref="NowNodeEvalHandler{T}"/>: the graph,
    /// the node being evaluated, and the output port being asked for.
    /// </summary>
    public readonly struct NowNodeEvalContext<T>
    {
        public readonly NowNodeGraph graph;
        public readonly NowNode node;
        public readonly NowNodePort port;

        readonly NowNodeGraphEvaluator<T> _evaluator;

        internal NowNodeEvalContext(NowNodeGraphEvaluator<T> evaluator, NowNodeGraph graph, NowNode node, NowNodePort port)
        {
            _evaluator = evaluator;
            this.graph = graph;
            this.node = node;
            this.port = port;
        }

        /// <summary>True when a link feeds the given input port.</summary>
        public bool HasInput(string portId)
        {
            return _evaluator != null && _evaluator.HasInput(graph, node, portId);
        }

        /// <summary>True when a link feeds the given input port.</summary>
        public bool HasInput(int portId)
        {
            return HasInput(NowNodeIds.FromInt(portId));
        }

        /// <summary>
        /// Evaluates the node connected to the given input port. Returns
        /// <paramref name="fallback"/> when the port is unconnected, the upstream
        /// node kind has no handler, or the link is part of a cycle.
        /// </summary>
        public T Input(string portId, T fallback = default)
        {
            return _evaluator != null ? _evaluator.EvaluateInput(graph, node, portId, fallback) : fallback;
        }

        /// <summary>
        /// Evaluates the node connected to the given input port. Returns
        /// <paramref name="fallback"/> when the port is unconnected, the upstream
        /// node kind has no handler, or the link is part of a cycle.
        /// </summary>
        public T Input(int portId, T fallback = default)
        {
            return Input(NowNodeIds.FromInt(portId), fallback);
        }
    }

    /// <summary>
    /// Pull-based graph evaluator. Register one handler per node kind with
    /// <see cref="Kind"/>, then ask for any node's value with <see cref="Evaluate(NowNodeGraph, string, T)"/>.
    /// Each top-level call walks upstream links, memoizes every visited output port
    /// for the duration of that call, and breaks cycles by handing the handler its
    /// fallback value instead of recursing. Wrap several calls in
    /// <see cref="BeginBatch"/> to share the memo table across them.
    /// </summary>
    public sealed class NowNodeGraphEvaluator<T>
    {
        const int DefaultMaximumDepth = 256;

        readonly Dictionary<int, NowNodeEvalHandler<T>> _handlers = new Dictionary<int, NowNodeEvalHandler<T>>(8);
        readonly Dictionary<(string nodeId, string portId), T> _memo = new Dictionary<(string, string), T>(32);
        readonly HashSet<(string nodeId, string portId)> _visiting = new HashSet<(string, string)>();
        Dictionary<string, NowNode> _indexedNodes;
        Dictionary<(string nodeId, string portId), NowNodeLink> _indexedInputs;
        NowNodeGraph _indexedGraph;
        int _batchDepth;
        int _maximumDepth = DefaultMaximumDepth;

        /// <summary>
        /// Maximum number of nested output ports evaluated in one dependency
        /// chain. Inputs beyond the limit resolve to their local fallback, just
        /// like cycles and missing links. The default is 256.
        /// </summary>
        public int maximumDepth
        {
            get => _maximumDepth;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Node evaluation depth must be positive.");

                _maximumDepth = value;
            }
        }

        /// <summary>
        /// Starts a caller-owned evaluation batch: every Evaluate/TryEvaluate call
        /// until the returned scope is disposed shares one memo table, so upstream
        /// nodes feeding several roots are computed once per batch instead of once
        /// per call. The caller decides the batch lifetime (typically one rebuild
        /// pass) because graph edits made mid-batch are not observed by output
        /// ports that were already memoized. Scopes nest; the memo clears when the
        /// outermost scope is disposed.
        /// </summary>
        public BatchScope BeginBatch()
        {
            if (_batchDepth == 0)
                _memo.Clear();

            ++_batchDepth;
            return new BatchScope(this);
        }

        /// <summary>
        /// Starts a batch with indexed node IDs and input links for one graph.
        /// Building the index is linear in graph size; dependency lookups then
        /// avoid scanning the public lists. Storage is reused across scopes.
        /// </summary>
        /// <remarks>
        /// Finish the scope before editing node IDs, ports, or topology. Direct
        /// list edits, renames and reordering are picked up at the next scope.
        /// Duplicate IDs/input links resolve to the first entry, as in live
        /// evaluation. Node value fields remain live until their outputs are
        /// memoized. Use BeginBatch or ordinary Evaluate calls when handlers
        /// need to change topology and immediately follow the changed links.
        /// Indexed scopes may nest for the same graph; a different graph or an
        /// existing live evaluation/batch cannot be switched into indexed mode.
        /// Dispose scopes in reverse order and exactly once.
        /// </remarks>
        public BatchScope BeginIndexedBatch(NowNodeGraph graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (_batchDepth != 0)
            {
                if (!ReferenceEquals(_indexedGraph, graph))
                    throw new InvalidOperationException("An indexed batch cannot replace an active evaluation batch.");
                return BeginBatch();
            }
            if (_visiting.Count != 0)
                throw new InvalidOperationException("An indexed batch must begin outside an evaluation handler.");

            // These lists and node IDs are publicly mutable. Rebuild at every
            // explicit scope boundary rather than retaining stale lookups.
            _indexedNodes ??= new Dictionary<string, NowNode>(StringComparer.Ordinal);
            _indexedInputs ??= new Dictionary<(string, string), NowNodeLink>();
            _indexedNodes.Clear();
            _indexedInputs.Clear();
            try
            {
                for (int i = 0; i < graph.nodes.Count; ++i)
                {
                    var node = graph.nodes[i];
                    if (node != null && !string.IsNullOrEmpty(node.id))
                        _indexedNodes.TryAdd(node.id, node);
                }
                for (int i = 0; i < graph.links.Count; ++i)
                {
                    var link = graph.links[i];
                    _indexedInputs.TryAdd((link.inputNodeId, link.inputPortId), link);
                }
            }
            catch
            {
                _indexedNodes?.Clear();
                _indexedInputs?.Clear();
                throw;
            }
            _indexedGraph = graph;
            return BeginBatch();
        }

        void EndBatch()
        {
            if (_batchDepth > 0 && --_batchDepth == 0)
            {
                _memo.Clear();
                _indexedGraph = null;
                _indexedNodes?.Clear();
                _indexedInputs?.Clear();
            }
        }

        /// <summary>Handle for one <see cref="BeginBatch"/> call; dispose exactly once per scope.</summary>
        public readonly struct BatchScope : IDisposable
        {
            readonly NowNodeGraphEvaluator<T> _evaluator;

            internal BatchScope(NowNodeGraphEvaluator<T> evaluator)
            {
                _evaluator = evaluator;
            }

            public void Dispose()
            {
                _evaluator?.EndBatch();
            }
        }

        /// <summary>Registers (or replaces) the handler for a node kind. Pass null to remove.</summary>
        public NowNodeGraphEvaluator<T> Kind(int kindId, NowNodeEvalHandler<T> handler)
        {
            if (handler == null)
                _handlers.Remove(kindId);
            else
                _handlers[kindId] = handler;

            return this;
        }

        /// <summary>True when a handler is registered for the node kind.</summary>
        public bool HasKind(int kindId)
        {
            return _handlers.ContainsKey(kindId);
        }

        /// <summary>Evaluates a node's first output port (or the node itself when it has none).</summary>
        public T Evaluate(NowNodeGraph graph, string nodeId, T fallback = default)
        {
            TryEvaluate(graph, nodeId, null, fallback, out T value);
            return value;
        }

        /// <summary>Evaluates a specific output port of a node.</summary>
        public T Evaluate(NowNodeGraph graph, string nodeId, int portId, T fallback = default)
        {
            TryEvaluate(graph, nodeId, NowNodeIds.FromInt(portId), fallback, out T value);
            return value;
        }

        /// <summary>Evaluates a specific output port of a node.</summary>
        public T Evaluate(NowNodeGraph graph, string nodeId, string portId, T fallback = default)
        {
            TryEvaluate(graph, nodeId, portId, fallback, out T value);
            return value;
        }

        /// <summary>Evaluates a node's first output port (or the node itself when it has none).</summary>
        public bool TryEvaluate(NowNodeGraph graph, string nodeId, out T value)
        {
            return TryEvaluate(graph, nodeId, null, default, out value);
        }

        /// <summary>
        /// Evaluates a node. Returns false when the node or requested output port does
        /// not exist or the node's kind has no handler; unconnected inputs and cycles
        /// deeper in the walk still succeed and resolve to their local fallbacks.
        /// </summary>
        public bool TryEvaluate(NowNodeGraph graph, string nodeId, string portId, T fallback, out T value)
        {
            value = fallback;
            var node = FindNode(graph, nodeId);

            if (node == null)
                return false;

            NowNodePort port = null;

            if (!string.IsNullOrEmpty(portId))
            {
                if (!node.TryGetPort(portId, NowNodePortDirection.Output, out port))
                    return false;
            }
            else if (node.outputs != null && node.outputs.Count > 0)
            {
                port = node.outputs[0];
            }

            if (!_handlers.ContainsKey(node.kindId))
                return false;

            bool isRoot = _visiting.Count == 0;

            if (isRoot && _batchDepth == 0)
                _memo.Clear();

            try
            {
                value = EvaluatePort(graph, node, port, fallback);
            }
            finally
            {
                if (isRoot)
                    _visiting.Clear();
            }

            return true;
        }

        internal T EvaluateInput(NowNodeGraph graph, NowNode node, string portId, T fallback)
        {
            if (node == null || !TryGetInputLink(graph, node.id, portId, out var link))
                return fallback;

            var sourceNode = FindNode(graph, link.outputNodeId);
            if (sourceNode == null || !sourceNode.TryGetPort(link.outputPortId, NowNodePortDirection.Output, out var sourcePort))
                return fallback;

            return EvaluatePort(graph, sourceNode, sourcePort, fallback);
        }

        internal bool HasInput(NowNodeGraph graph, NowNode node, string portId)
        {
            return node != null && TryGetInputLink(graph, node.id, portId, out _);
        }

        NowNode FindNode(NowNodeGraph graph, string nodeId)
        {
            if (_indexedGraph == null)
                return graph?.FindNode(nodeId);
            RequireIndexedGraph(graph);
            return !string.IsNullOrEmpty(nodeId) && _indexedNodes.TryGetValue(nodeId, out var node) ? node : null;
        }

        bool TryGetInputLink(NowNodeGraph graph, string nodeId, string portId, out NowNodeLink link)
        {
            if (_indexedGraph != null)
            {
                RequireIndexedGraph(graph);
                return _indexedInputs.TryGetValue((nodeId, portId), out link);
            }
            link = default;
            return graph != null && graph.TryGetInputLink(nodeId, portId, out link);
        }

        void RequireIndexedGraph(NowNodeGraph graph)
        {
            if (!ReferenceEquals(_indexedGraph, graph))
                throw new InvalidOperationException("Evaluate the indexed graph until its batch scope is disposed.");
        }

        T EvaluatePort(NowNodeGraph graph, NowNode node, NowNodePort port, T fallback)
        {
            var key = (node.id, port != null ? port.id : string.Empty);

            if (_memo.TryGetValue(key, out var cached))
                return cached;

            if (_visiting.Count >= _maximumDepth)
                return fallback;

            if (!_visiting.Add(key))
                return fallback;

            T value = _handlers.TryGetValue(node.kindId, out var handler)
                ? handler(new NowNodeEvalContext<T>(this, graph, node, port))
                : fallback;

            _visiting.Remove(key);
            _memo[key] = value;
            return value;
        }
    }
}
