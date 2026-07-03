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
            return graph != null && node != null && graph.TryGetInputLink(node.id, portId, out _);
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
    /// fallback value instead of recursing.
    /// </summary>
    public sealed class NowNodeGraphEvaluator<T>
    {
        readonly Dictionary<int, NowNodeEvalHandler<T>> _handlers = new Dictionary<int, NowNodeEvalHandler<T>>(8);
        readonly Dictionary<(string nodeId, string portId), T> _memo = new Dictionary<(string, string), T>(32);
        readonly HashSet<(string nodeId, string portId)> _visiting = new HashSet<(string, string)>();

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
            var node = graph?.FindNode(nodeId);

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

            if (isRoot)
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
            if (graph == null || node == null || !graph.TryGetInputLink(node.id, portId, out var link))
                return fallback;

            if (!graph.TryFindPort(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, out var sourceNode, out var sourcePort))
                return fallback;

            return EvaluatePort(graph, sourceNode, sourcePort, fallback);
        }

        T EvaluatePort(NowNodeGraph graph, NowNode node, NowNodePort port, T fallback)
        {
            var key = (node.id, port != null ? port.id : string.Empty);

            if (_memo.TryGetValue(key, out var cached))
                return cached;

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
