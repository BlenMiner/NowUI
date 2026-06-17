using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NowUI.NodeGraph
{
    public enum NowNodePortDirection
    {
        Input,
        Output
    }

    public delegate bool NowNodePortCompatibility(
        NowNodeGraph graph,
        NowNode outputNode,
        NowNodePort outputPort,
        NowNode inputNode,
        NowNodePort inputPort);

    public delegate bool NowNodeConnectionRule(NowNodeConnectionContext context);

    public delegate void NowNodeContentRenderer(NowNodeContentContext context);

    public static class NowNodeIds
    {
        public static string FromInt(int id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }
    }

    public readonly struct NowNodeConnectionContext
    {
        public readonly NowNodeGraph graph;
        public readonly NowNode outputNode;
        public readonly NowNodePort outputPort;
        public readonly NowNode inputNode;
        public readonly NowNodePort inputPort;

        public NowNodeConnectionContext(
            NowNodeGraph graph,
            NowNode outputNode,
            NowNodePort outputPort,
            NowNode inputNode,
            NowNodePort inputPort)
        {
            this.graph = graph;
            this.outputNode = outputNode;
            this.outputPort = outputPort;
            this.inputNode = inputNode;
            this.inputPort = inputPort;
        }
    }

    public sealed class NowNodeContentContext
    {
        public NowNodeGraph graph;
        public NowNodeGraphSchema schema;
        public NowNode node;
        public NowRect nodeRect;
        public NowRect bodyRect;
        public NowNodeGraphStyle style;
        public float zoom;
        public bool selected;
        public bool changed;

        public float Scale(float value)
        {
            return value * Mathf.Max(zoom, 0.001f);
        }

        public NowRect Row(int index, float height = 20f, float gap = 4f)
        {
            height = Mathf.Max(0f, Scale(height));
            gap = Mathf.Max(0f, Scale(gap));
            float y = bodyRect.y + index * (height + gap);
            return new NowRect(bodyRect.x, y, bodyRect.width, Mathf.Max(0f, Mathf.Min(height, bodyRect.yMax - y)));
        }

        public void Texture(Texture texture, NowRect rect, float radius = 4f)
        {
            Now.Rectangle(rect).SetTexture(texture).SetRadius(Scale(radius)).Draw();
        }

        public void MarkChanged()
        {
            changed = true;
        }
    }

    [Serializable]
    public sealed class NowNodePort
    {
        public string id;
        public string label;
        public int typeId;
        public NowNodePortDirection direction;
        public Color color = Color.clear;
        public int maxConnections;

        public NowNodePort()
        {
            id = Guid.NewGuid().ToString("N");
            label = "Port";
            direction = NowNodePortDirection.Input;
            maxConnections = 1;
        }

        public NowNodePort(string id, string label, NowNodePortDirection direction, int typeId = 0)
        {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            this.label = label ?? string.Empty;
            this.direction = direction;
            this.typeId = typeId;
            maxConnections = direction == NowNodePortDirection.Input ? 1 : 0;
        }

        public bool isInput => direction == NowNodePortDirection.Input;

        public bool isOutput => direction == NowNodePortDirection.Output;
    }

    public sealed class NowNodePortDefinition
    {
        public int intId;
        public string id;
        public string label;
        public int typeId;
        public NowNodePortDirection direction;
        public Color color = Color.clear;
        public int maxConnections;

        public NowNodePortDefinition(int id, string label, NowNodePortDirection direction, int typeId, int maxConnections)
        {
            intId = id;
            this.id = NowNodeIds.FromInt(id);
            this.label = label ?? string.Empty;
            this.direction = direction;
            this.typeId = typeId;
            this.maxConnections = maxConnections;
        }

        public NowNodePortDefinition SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowNodePort CreatePort()
        {
            return new NowNodePort(id, label, direction, typeId)
            {
                color = color,
                maxConnections = maxConnections
            };
        }
    }

    public sealed class NowNodeDefinition
    {
        public readonly int kindId;
        public string title;
        public Vector2 size;
        public Color color = Color.clear;
        public readonly List<NowNodePortDefinition> inputs = new List<NowNodePortDefinition>(4);
        public readonly List<NowNodePortDefinition> outputs = new List<NowNodePortDefinition>(4);
        public NowNodeContentRenderer renderer;

        internal NowNodeDefinition(int kindId, string title)
        {
            this.kindId = kindId;
            this.title = title ?? string.Empty;
        }

        public NowNodeDefinition SetTitle(string title)
        {
            this.title = title ?? string.Empty;
            return this;
        }

        public NowNodeDefinition SetSize(Vector2 size)
        {
            this.size = size;
            return this;
        }

        public NowNodeDefinition SetSize(float width, float height)
        {
            size = new Vector2(width, height);
            return this;
        }

        public NowNodeDefinition SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowNodeDefinition Input(int id, string label, int typeId = 0, int maxConnections = 1)
        {
            inputs.Add(new NowNodePortDefinition(id, label, NowNodePortDirection.Input, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition Output(int id, string label, int typeId = 0, int maxConnections = 0)
        {
            outputs.Add(new NowNodePortDefinition(id, label, NowNodePortDirection.Output, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition Render(NowNodeContentRenderer renderer)
        {
            this.renderer = renderer;
            return this;
        }

        internal void ApplyTo(NowNode node)
        {
            node.kindId = kindId;
            node.title = title;
            node.size = size;

            if (color.a > 0f)
                node.color = color;

            node.inputs.Clear();
            node.outputs.Clear();

            for (int i = 0; i < inputs.Count; ++i)
                node.inputs.Add(inputs[i].CreatePort());

            for (int i = 0; i < outputs.Count; ++i)
                node.outputs.Add(outputs[i].CreatePort());
        }
    }

    public sealed class NowNodeGraphSchema
    {
        readonly Dictionary<int, NowNodeDefinition> _definitions = new Dictionary<int, NowNodeDefinition>();
        readonly List<NowNodeConnectionRule> _connectionRules = new List<NowNodeConnectionRule>(4);

        public NowNodeDefinition Node(int kindId, string title)
        {
            if (!_definitions.TryGetValue(kindId, out var definition))
            {
                definition = new NowNodeDefinition(kindId, title);
                _definitions.Add(kindId, definition);
            }
            else
            {
                definition.SetTitle(title);
            }

            return definition;
        }

        public bool TryGetNode(int kindId, out NowNodeDefinition definition)
        {
            return _definitions.TryGetValue(kindId, out definition);
        }

        public NowNodeGraphSchema Allow(int outputTypeId, int inputTypeId)
        {
            return Connect(context =>
                context.outputPort.typeId == outputTypeId &&
                context.inputPort.typeId == inputTypeId);
        }

        public NowNodeGraphSchema AllowSameTypes(bool allowWildcard = true)
        {
            return Connect(context =>
                context.outputPort.typeId == context.inputPort.typeId ||
                (allowWildcard && (context.outputPort.typeId == 0 || context.inputPort.typeId == 0)));
        }

        public NowNodeGraphSchema AllowWildcard(int wildcardTypeId = 0)
        {
            return Connect(context =>
                context.outputPort.typeId == wildcardTypeId ||
                context.inputPort.typeId == wildcardTypeId);
        }

        public NowNodeGraphSchema Connect(NowNodeConnectionRule rule)
        {
            if (rule != null)
                _connectionRules.Add(rule);

            return this;
        }

        public NowNode CreateNode(
            NowNodeGraph graph,
            int kindId,
            Vector2 position,
            int userId = 0,
            string id = null)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            if (!TryGetNode(kindId, out var definition))
                throw new ArgumentException($"Node kind {kindId} is not registered.", nameof(kindId));

            graph.schema = this;
            var node = graph.AddNode(id, definition.title, position);
            node.userId = userId;
            definition.ApplyTo(node);
            return node;
        }

        public bool CanConnect(
            NowNodeGraph graph,
            NowNode outputNode,
            NowNodePort output,
            NowNode inputNode,
            NowNodePort input)
        {
            if (_connectionRules.Count == 0)
                return NowNodeGraph.PortsAreCompatible(output, input);

            var context = new NowNodeConnectionContext(graph, outputNode, output, inputNode, input);

            for (int i = 0; i < _connectionRules.Count; ++i)
            {
                if (_connectionRules[i] != null && _connectionRules[i](context))
                    return true;
            }

            return false;
        }

        public bool DrawNodeContent(NowNodeContentContext context)
        {
            if (context == null || context.node == null || !TryGetNode(context.node.kindId, out var definition) || definition.renderer == null)
                return false;

            definition.renderer(context);
            return true;
        }
    }

    [Serializable]
    public sealed class NowNode
    {
        public string id;
        public string title;
        public int kindId;
        public int userId;
        public Vector2 position;
        public Vector2 size;
        public Color color = Color.clear;
        public bool selected;
        public List<NowNodePort> inputs = new List<NowNodePort>(4);
        public List<NowNodePort> outputs = new List<NowNodePort>(4);

        public NowNode()
        {
            id = Guid.NewGuid().ToString("N");
            title = "Node";
            size = default;
        }

        public NowNode(string id, string title, Vector2 position)
        {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            this.title = title ?? string.Empty;
            this.position = position;
            size = default;
        }

        public NowNodePort AddInput(string id, string label, int typeId = 0)
        {
            var port = new NowNodePort(id, label, NowNodePortDirection.Input, typeId);
            inputs.Add(port);
            return port;
        }

        public NowNodePort AddInput(int id, string label, int typeId = 0)
        {
            return AddInput(NowNodeIds.FromInt(id), label, typeId);
        }

        public NowNodePort AddOutput(string id, string label, int typeId = 0)
        {
            var port = new NowNodePort(id, label, NowNodePortDirection.Output, typeId);
            outputs.Add(port);
            return port;
        }

        public NowNodePort AddOutput(int id, string label, int typeId = 0)
        {
            return AddOutput(NowNodeIds.FromInt(id), label, typeId);
        }

        public bool TryGetPort(string portId, NowNodePortDirection direction, out NowNodePort port)
        {
            var ports = direction == NowNodePortDirection.Input ? inputs : outputs;

            for (int i = 0; i < ports.Count; ++i)
            {
                if (ports[i] != null && ports[i].id == portId)
                {
                    port = ports[i];
                    return true;
                }
            }

            port = null;
            return false;
        }

        public bool TryGetPort(int portId, NowNodePortDirection direction, out NowNodePort port)
        {
            return TryGetPort(NowNodeIds.FromInt(portId), direction, out port);
        }
    }

    [Serializable]
    public struct NowNodeLink : IEquatable<NowNodeLink>
    {
        public string outputNodeId;
        public string outputPortId;
        public string inputNodeId;
        public string inputPortId;

        public NowNodeLink(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            this.outputNodeId = outputNodeId;
            this.outputPortId = outputPortId;
            this.inputNodeId = inputNodeId;
            this.inputPortId = inputPortId;
        }

        public NowNodeLink(string outputNodeId, int outputPortId, string inputNodeId, int inputPortId)
            : this(outputNodeId, NowNodeIds.FromInt(outputPortId), inputNodeId, NowNodeIds.FromInt(inputPortId))
        {
        }

        public NowNodeLink(int outputNodeId, int outputPortId, int inputNodeId, int inputPortId)
            : this(NowNodeIds.FromInt(outputNodeId), NowNodeIds.FromInt(outputPortId), NowNodeIds.FromInt(inputNodeId), NowNodeIds.FromInt(inputPortId))
        {
        }

        public bool isValid =>
            !string.IsNullOrEmpty(outputNodeId) &&
            !string.IsNullOrEmpty(outputPortId) &&
            !string.IsNullOrEmpty(inputNodeId) &&
            !string.IsNullOrEmpty(inputPortId);

        public bool Equals(NowNodeLink other)
        {
            return outputNodeId == other.outputNodeId &&
                outputPortId == other.outputPortId &&
                inputNodeId == other.inputNodeId &&
                inputPortId == other.inputPortId;
        }

        public override bool Equals(object obj)
        {
            return obj is NowNodeLink other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = outputNodeId != null ? outputNodeId.GetHashCode() : 0;
                hash = (hash * 397) ^ (outputPortId != null ? outputPortId.GetHashCode() : 0);
                hash = (hash * 397) ^ (inputNodeId != null ? inputNodeId.GetHashCode() : 0);
                hash = (hash * 397) ^ (inputPortId != null ? inputPortId.GetHashCode() : 0);
                return hash;
            }
        }

        public static bool operator ==(NowNodeLink a, NowNodeLink b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(NowNodeLink a, NowNodeLink b)
        {
            return !a.Equals(b);
        }
    }

    [Serializable]
    public sealed class NowNodeGraph
    {
        public List<NowNode> nodes = new List<NowNode>(16);
        public List<NowNodeLink> links = new List<NowNodeLink>(16);
        public Vector2 defaultNodeSize = new Vector2(180f, 118f);
        public string selectedNodeId;
        public List<string> selectedNodeIds = new List<string>(4);
        [NonSerialized] public NowNodePortCompatibility canConnect;
        [NonSerialized] public NowNodeGraphSchema schema;

        public NowNode AddNode(string id, string title, Vector2 position)
        {
            var node = new NowNode(id, title, position);
            nodes.Add(node);
            return node;
        }

        public NowNode AddNode(int id, string title, Vector2 position)
        {
            return AddNode(NowNodeIds.FromInt(id), title, position);
        }

        public NowNode AddNode(NowNodeGraphSchema schema, int kindId, Vector2 position, int userId = 0, string id = null)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            return schema.CreateNode(this, kindId, position, userId, id);
        }

        public NowNodeGraph SetSchema(NowNodeGraphSchema schema)
        {
            this.schema = schema;
            return this;
        }

        public bool RemoveNode(string nodeId)
        {
            int index = IndexOfNode(nodeId);

            if (index < 0)
                return false;

            nodes.RemoveAt(index);

            for (int i = links.Count - 1; i >= 0; --i)
            {
                if (links[i].inputNodeId == nodeId || links[i].outputNodeId == nodeId)
                    links.RemoveAt(i);
            }

            if (selectedNodeId == nodeId)
                selectedNodeId = null;

            if (selectedNodeIds != null)
                selectedNodeIds.Remove(nodeId);

            SyncSelectionFlags();
            return true;
        }

        public int RemoveSelectedNodes()
        {
            if (!HasSelection())
                return 0;

            int removed = 0;

            for (int i = nodes.Count - 1; i >= 0; --i)
            {
                var node = nodes[i];

                if (node == null || !IsNodeSelected(node.id))
                    continue;

                nodes.RemoveAt(i);
                RemoveLinksForNode(node.id);
                ++removed;
            }

            if (removed > 0)
            {
                selectedNodeId = null;
                selectedNodeIds?.Clear();
                SyncSelectionFlags();
            }

            return removed;
        }

        public NowNode FindNode(string nodeId)
        {
            int index = IndexOfNode(nodeId);
            return index >= 0 ? nodes[index] : null;
        }

        public int IndexOfNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return -1;

            for (int i = 0; i < nodes.Count; ++i)
            {
                if (nodes[i] != null && nodes[i].id == nodeId)
                    return i;
            }

            return -1;
        }

        public void SelectNode(string nodeId)
        {
            EnsureSelectionList();
            selectedNodeIds.Clear();

            if (!string.IsNullOrEmpty(nodeId) && FindNode(nodeId) != null)
                selectedNodeIds.Add(nodeId);

            selectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[0] : null;
            SyncSelectionFlags();
        }

        public void ClearSelection()
        {
            selectedNodeId = null;
            selectedNodeIds?.Clear();
            SyncSelectionFlags();
        }

        public bool AddNodeToSelection(string nodeId)
        {
            return SetNodeSelected(nodeId, true);
        }

        public bool SetNodeSelected(string nodeId, bool selected)
        {
            EnsureSelectionList();

            if (string.IsNullOrEmpty(nodeId) || FindNode(nodeId) == null)
                return false;

            int index = selectedNodeIds.IndexOf(nodeId);

            if (selected)
            {
                if (index >= 0)
                    return false;

                selectedNodeIds.Add(nodeId);
                SyncSelectionFlags();
                return true;
            }

            if (index < 0)
                return false;

            selectedNodeIds.RemoveAt(index);
            SyncSelectionFlags();
            return true;
        }

        public bool IsNodeSelected(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return false;

            if (selectedNodeIds != null && selectedNodeIds.Count > 0)
                return selectedNodeIds.IndexOf(nodeId) >= 0;

            return selectedNodeId == nodeId;
        }

        public int SelectedNodeCount()
        {
            EnsureSelectionList();
            return selectedNodeIds.Count;
        }

        public bool TryFindPort(
            string nodeId,
            string portId,
            NowNodePortDirection direction,
            out NowNode node,
            out NowNodePort port)
        {
            node = FindNode(nodeId);

            if (node != null && node.TryGetPort(portId, direction, out port))
                return true;

            port = null;
            return false;
        }

        public bool TryFindPort(
            string nodeId,
            int portId,
            NowNodePortDirection direction,
            out NowNode node,
            out NowNodePort port)
        {
            return TryFindPort(nodeId, NowNodeIds.FromInt(portId), direction, out node, out port);
        }

        public bool TryFindPort(
            int nodeId,
            int portId,
            NowNodePortDirection direction,
            out NowNode node,
            out NowNodePort port)
        {
            return TryFindPort(NowNodeIds.FromInt(nodeId), NowNodeIds.FromInt(portId), direction, out node, out port);
        }

        public bool TryAddLink(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId, bool replaceInput = true)
        {
            return TryAddLink(new NowNodeLink(outputNodeId, outputPortId, inputNodeId, inputPortId), replaceInput);
        }

        public bool TryAddLink(string outputNodeId, int outputPortId, string inputNodeId, int inputPortId, bool replaceInput = true)
        {
            return TryAddLink(new NowNodeLink(outputNodeId, outputPortId, inputNodeId, inputPortId), replaceInput);
        }

        public bool TryAddLink(int outputNodeId, int outputPortId, int inputNodeId, int inputPortId, bool replaceInput = true)
        {
            return TryAddLink(new NowNodeLink(outputNodeId, outputPortId, inputNodeId, inputPortId), replaceInput);
        }

        public bool TryAddLink(NowNodeLink link, bool replaceInput = true)
        {
            if (!CanAddLink(link, out var outputPort, out var inputPort))
                return false;

            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i] == link)
                    return false;
            }

            if (inputPort.maxConnections > 0)
            {
                int existing = CountInputLinks(link.inputNodeId, link.inputPortId);

                if (replaceInput && existing >= inputPort.maxConnections)
                {
                    for (int i = links.Count - 1; i >= 0; --i)
                    {
                        if (links[i].inputNodeId == link.inputNodeId && links[i].inputPortId == link.inputPortId)
                            links.RemoveAt(i);
                    }
                }
                else if (existing >= inputPort.maxConnections)
                {
                    return false;
                }
            }

            if (outputPort.maxConnections > 0 && CountOutputLinks(link.outputNodeId, link.outputPortId) >= outputPort.maxConnections)
                return false;

            links.Add(link);
            return true;
        }

        public bool RemoveLink(NowNodeLink link)
        {
            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i] == link)
                {
                    links.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public int RemoveLinksForPort(string nodeId, string portId)
        {
            int removed = 0;

            for (int i = links.Count - 1; i >= 0; --i)
            {
                if ((links[i].inputNodeId == nodeId && links[i].inputPortId == portId) ||
                    (links[i].outputNodeId == nodeId && links[i].outputPortId == portId))
                {
                    links.RemoveAt(i);
                    ++removed;
                }
            }

            return removed;
        }

        public int RemoveLinksForNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return 0;

            int removed = 0;

            for (int i = links.Count - 1; i >= 0; --i)
            {
                if (links[i].inputNodeId == nodeId || links[i].outputNodeId == nodeId)
                {
                    links.RemoveAt(i);
                    ++removed;
                }
            }

            return removed;
        }

        public int RemoveLinksForPort(string nodeId, int portId)
        {
            return RemoveLinksForPort(nodeId, NowNodeIds.FromInt(portId));
        }

        public int RemoveLinksForPort(int nodeId, int portId)
        {
            return RemoveLinksForPort(NowNodeIds.FromInt(nodeId), NowNodeIds.FromInt(portId));
        }

        public bool TryCreateLink(
            string firstNodeId,
            string firstPortId,
            string secondNodeId,
            string secondPortId,
            out NowNodeLink link)
        {
            link = default;

            if (!TryFindAnyPort(firstNodeId, firstPortId, out var firstPort) ||
                !TryFindAnyPort(secondNodeId, secondPortId, out var secondPort))
            {
                return false;
            }

            if (firstPort.direction == NowNodePortDirection.Output && secondPort.direction == NowNodePortDirection.Input)
                link = new NowNodeLink(firstNodeId, firstPortId, secondNodeId, secondPortId);
            else if (firstPort.direction == NowNodePortDirection.Input && secondPort.direction == NowNodePortDirection.Output)
                link = new NowNodeLink(secondNodeId, secondPortId, firstNodeId, firstPortId);
            else
                return false;

            return CanAddLink(link, out _, out _);
        }

        public bool TryCreateLink(
            string firstNodeId,
            int firstPortId,
            string secondNodeId,
            int secondPortId,
            out NowNodeLink link)
        {
            return TryCreateLink(
                firstNodeId,
                NowNodeIds.FromInt(firstPortId),
                secondNodeId,
                NowNodeIds.FromInt(secondPortId),
                out link);
        }

        public bool TryCreateLink(
            int firstNodeId,
            int firstPortId,
            int secondNodeId,
            int secondPortId,
            out NowNodeLink link)
        {
            return TryCreateLink(
                NowNodeIds.FromInt(firstNodeId),
                NowNodeIds.FromInt(firstPortId),
                NowNodeIds.FromInt(secondNodeId),
                NowNodeIds.FromInt(secondPortId),
                out link);
        }

        public bool CanAddLink(NowNodeLink link)
        {
            return CanAddLink(link, out _, out _);
        }

        bool CanAddLink(NowNodeLink link, out NowNodePort outputPort, out NowNodePort inputPort)
        {
            outputPort = null;
            inputPort = null;

            if (!link.isValid || link.outputNodeId == link.inputNodeId)
                return false;

            if (!TryFindPort(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, out var outputNode, out outputPort) ||
                !TryFindPort(link.inputNodeId, link.inputPortId, NowNodePortDirection.Input, out var inputNode, out inputPort))
            {
                return false;
            }

            return PortsAreCompatible(this, outputNode, outputPort, inputNode, inputPort);
        }

        bool TryFindAnyPort(string nodeId, string portId, out NowNodePort port)
        {
            port = null;
            var node = FindNode(nodeId);

            if (node == null)
                return false;

            if (node.TryGetPort(portId, NowNodePortDirection.Input, out port) ||
                node.TryGetPort(portId, NowNodePortDirection.Output, out port))
            {
                return true;
            }

            return false;
        }

        int CountInputLinks(string nodeId, string portId)
        {
            int count = 0;

            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i].inputNodeId == nodeId && links[i].inputPortId == portId)
                    ++count;
            }

            return count;
        }

        int CountOutputLinks(string nodeId, string portId)
        {
            int count = 0;

            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i].outputNodeId == nodeId && links[i].outputPortId == portId)
                    ++count;
            }

            return count;
        }

        internal void SyncSelectionFlags()
        {
            EnsureSelectionList();

            if (!string.IsNullOrEmpty(selectedNodeId) &&
                selectedNodeIds.Count == 0 &&
                FindNode(selectedNodeId) != null)
            {
                selectedNodeIds.Add(selectedNodeId);
            }

            for (int i = selectedNodeIds.Count - 1; i >= 0; --i)
            {
                if (FindNode(selectedNodeIds[i]) == null)
                    selectedNodeIds.RemoveAt(i);
            }

            selectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[0] : null;

            for (int i = 0; i < nodes.Count; ++i)
            {
                if (nodes[i] != null)
                    nodes[i].selected = IsSelectionId(nodes[i].id);
            }
        }

        bool HasSelection()
        {
            EnsureSelectionList();
            return selectedNodeIds.Count > 0 || !string.IsNullOrEmpty(selectedNodeId);
        }

        bool IsSelectionId(string nodeId)
        {
            return selectedNodeIds != null && selectedNodeIds.IndexOf(nodeId) >= 0;
        }

        void EnsureSelectionList()
        {
            if (selectedNodeIds == null)
                selectedNodeIds = new List<string>(4);
        }

        public static bool PortsAreCompatible(
            NowNodeGraph graph,
            NowNode outputNode,
            NowNodePort output,
            NowNode inputNode,
            NowNodePort input)
        {
            if (output == null || input == null || !output.isOutput || !input.isInput)
                return false;

            if (graph?.canConnect != null)
                return graph.canConnect(graph, outputNode, output, inputNode, input);

            if (graph?.schema != null)
                return graph.schema.CanConnect(graph, outputNode, output, inputNode, input);

            return PortsAreCompatible(output, input);
        }

        public static bool PortsAreCompatible(NowNodePort output, NowNodePort input)
        {
            if (output == null || input == null || !output.isOutput || !input.isInput)
                return false;

            return output.typeId == 0 || input.typeId == 0 || output.typeId == input.typeId;
        }
    }

    public sealed class NowNodeGraphHistory
    {
        sealed class Snapshot
        {
            public readonly List<NowNode> nodes;
            public readonly List<NowNodeLink> links;
            public readonly Vector2 defaultNodeSize;
            public readonly string selectedNodeId;
            public readonly List<string> selectedNodeIds;

            Snapshot(
                List<NowNode> nodes,
                List<NowNodeLink> links,
                Vector2 defaultNodeSize,
                string selectedNodeId,
                List<string> selectedNodeIds)
            {
                this.nodes = nodes;
                this.links = links;
                this.defaultNodeSize = defaultNodeSize;
                this.selectedNodeId = selectedNodeId;
                this.selectedNodeIds = selectedNodeIds;
            }

            public static Snapshot Capture(NowNodeGraph graph)
            {
                var nodes = new List<NowNode>(graph.nodes.Count);
                var links = new List<NowNodeLink>(graph.links.Count);
                var selectedNodeIds = new List<string>(graph.selectedNodeIds != null ? graph.selectedNodeIds.Count : 0);

                for (int i = 0; i < graph.nodes.Count; ++i)
                    nodes.Add(CloneNode(graph.nodes[i]));

                for (int i = 0; i < graph.links.Count; ++i)
                    links.Add(graph.links[i]);

                if (graph.selectedNodeIds != null)
                {
                    for (int i = 0; i < graph.selectedNodeIds.Count; ++i)
                        selectedNodeIds.Add(graph.selectedNodeIds[i]);
                }
                else if (!string.IsNullOrEmpty(graph.selectedNodeId))
                {
                    selectedNodeIds.Add(graph.selectedNodeId);
                }

                return new Snapshot(nodes, links, graph.defaultNodeSize, graph.selectedNodeId, selectedNodeIds);
            }

            public void Restore(NowNodeGraph graph)
            {
                graph.nodes.Clear();
                graph.links.Clear();

                for (int i = 0; i < nodes.Count; ++i)
                    graph.nodes.Add(CloneNode(nodes[i]));

                for (int i = 0; i < links.Count; ++i)
                    graph.links.Add(links[i]);

                graph.defaultNodeSize = defaultNodeSize;
                graph.selectedNodeId = selectedNodeId;

                if (graph.selectedNodeIds == null)
                    graph.selectedNodeIds = new List<string>(selectedNodeIds.Count);
                else
                    graph.selectedNodeIds.Clear();

                for (int i = 0; i < selectedNodeIds.Count; ++i)
                    graph.selectedNodeIds.Add(selectedNodeIds[i]);

                graph.SyncSelectionFlags();
            }

            public bool Matches(NowNodeGraph graph)
            {
                if (graph.defaultNodeSize != defaultNodeSize ||
                    graph.nodes.Count != nodes.Count ||
                    graph.links.Count != links.Count ||
                    graph.selectedNodeId != selectedNodeId)
                {
                    return false;
                }

                int selectionCount = graph.selectedNodeIds != null ? graph.selectedNodeIds.Count : 0;

                if (selectionCount != selectedNodeIds.Count)
                    return false;

                for (int i = 0; i < selectedNodeIds.Count; ++i)
                {
                    if (graph.selectedNodeIds[i] != selectedNodeIds[i])
                        return false;
                }

                for (int i = 0; i < nodes.Count; ++i)
                {
                    if (!NodeMatches(nodes[i], graph.nodes[i]))
                        return false;
                }

                for (int i = 0; i < links.Count; ++i)
                {
                    if (links[i] != graph.links[i])
                        return false;
                }

                return true;
            }

            static NowNode CloneNode(NowNode node)
            {
                if (node == null)
                    return null;

                var clone = new NowNode
                {
                    id = node.id,
                    title = node.title,
                    kindId = node.kindId,
                    userId = node.userId,
                    position = node.position,
                    size = node.size,
                    color = node.color,
                    selected = node.selected,
                    inputs = new List<NowNodePort>(node.inputs != null ? node.inputs.Count : 0),
                    outputs = new List<NowNodePort>(node.outputs != null ? node.outputs.Count : 0)
                };

                ClonePorts(node.inputs, clone.inputs);
                ClonePorts(node.outputs, clone.outputs);
                return clone;
            }

            static void ClonePorts(List<NowNodePort> source, List<NowNodePort> target)
            {
                if (source == null)
                    return;

                for (int i = 0; i < source.Count; ++i)
                    target.Add(ClonePort(source[i]));
            }

            static NowNodePort ClonePort(NowNodePort port)
            {
                if (port == null)
                    return null;

                return new NowNodePort(port.id, port.label, port.direction, port.typeId)
                {
                    color = port.color,
                    maxConnections = port.maxConnections
                };
            }

            static bool NodeMatches(NowNode a, NowNode b)
            {
                if (a == null || b == null)
                    return a == b;

                if (a.id != b.id ||
                    a.title != b.title ||
                    a.kindId != b.kindId ||
                    a.userId != b.userId ||
                    a.position != b.position ||
                    a.size != b.size ||
                    a.color != b.color ||
                    a.selected != b.selected)
                {
                    return false;
                }

                return PortsMatch(a.inputs, b.inputs) && PortsMatch(a.outputs, b.outputs);
            }

            static bool PortsMatch(List<NowNodePort> a, List<NowNodePort> b)
            {
                int countA = a != null ? a.Count : 0;
                int countB = b != null ? b.Count : 0;

                if (countA != countB)
                    return false;

                for (int i = 0; i < countA; ++i)
                {
                    if (!PortMatches(a[i], b[i]))
                        return false;
                }

                return true;
            }

            static bool PortMatches(NowNodePort a, NowNodePort b)
            {
                if (a == null || b == null)
                    return a == b;

                return a.id == b.id &&
                    a.label == b.label &&
                    a.typeId == b.typeId &&
                    a.direction == b.direction &&
                    a.color == b.color &&
                    a.maxConnections == b.maxConnections;
            }
        }

        readonly List<Snapshot> _undo = new List<Snapshot>(32);
        readonly List<Snapshot> _redo = new List<Snapshot>(32);

        public int maxSnapshots = 64;

        public bool canUndo => _undo.Count > 0;

        public bool canRedo => _redo.Count > 0;

        public int undoCount => _undo.Count;

        public int redoCount => _redo.Count;

        public void Record(NowNodeGraph graph)
        {
            if (graph == null)
                return;

            if (_undo.Count > 0 && _undo[_undo.Count - 1].Matches(graph))
                return;

            _undo.Add(Snapshot.Capture(graph));
            _redo.Clear();
            TrimUndo();
        }

        public bool Undo(NowNodeGraph graph)
        {
            if (graph == null || _undo.Count == 0)
                return false;

            _redo.Add(Snapshot.Capture(graph));
            var snapshot = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            snapshot.Restore(graph);
            return true;
        }

        public bool Redo(NowNodeGraph graph)
        {
            if (graph == null || _redo.Count == 0)
                return false;

            _undo.Add(Snapshot.Capture(graph));
            var snapshot = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            snapshot.Restore(graph);
            TrimUndo();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        void TrimUndo()
        {
            int limit = Mathf.Max(1, maxSnapshots);

            while (_undo.Count > limit)
                _undo.RemoveAt(0);
        }
    }

    public struct NowNodeGraphResult
    {
        public bool changed;
        public bool selectionChanged;
        public bool nodeMoved;
        public bool panned;
        public bool zoomed;
        public bool linkCreated;
        public bool linkRemoved;
        public bool nodesDeleted;
        public bool undo;
        public bool redo;
        public bool dragSelected;
        public bool contextMenuOpened;
        public string selectedNodeId;
        public NowNodeLink createdLink;
        public NowNodeLink removedLink;
    }

    public struct NowNodeGraphStyle
    {
        public bool drawBackground;
        public bool drawGrid;
        public Color background;
        public Color gridMinor;
        public Color gridMajor;
        public Color node;
        public Color nodeTitle;
        public Color nodeSelected;
        public Color border;
        public Color selectedBorder;
        public Color connection;
        public Color text;
        public Color textMuted;
        public Color inputPort;
        public Color outputPort;
        public Color compatiblePort;
        public Color incompatiblePort;
        public Color selectionFill;
        public Color selectionBorder;
        public float gridSpacing;
        public int gridMajorEvery;
        public float nodeRadius;
        public float titleHeight;
        public float portRadius;
        public float portRowHeight;
        public float portInset;
        public float contentPadding;
        public float portLabelLaneWidth;
        public float connectionWidth;
        public float minZoom;
        public float maxZoom;
        public float zoomStep;

        public static NowNodeGraphStyle Default(NowThemeAsset theme)
        {
            Color background = theme != null
                ? theme.GetColor(NowColorToken.Background, new Color(0.075f, 0.085f, 0.105f, 1f))
                : new Color(0.075f, 0.085f, 0.105f, 1f);
            Color surface = theme != null
                ? theme.GetColor(NowColorToken.Surface, new Color(0.12f, 0.135f, 0.165f, 1f))
                : new Color(0.12f, 0.135f, 0.165f, 1f);
            Color muted = theme != null
                ? theme.GetColor(NowColorToken.SurfaceMuted, new Color(0.16f, 0.18f, 0.22f, 1f))
                : new Color(0.16f, 0.18f, 0.22f, 1f);
            Color text = theme != null
                ? theme.GetColor(NowColorToken.Text, new Color(0.92f, 0.94f, 0.98f, 1f))
                : new Color(0.92f, 0.94f, 0.98f, 1f);
            Color textMuted = theme != null
                ? theme.GetColor(NowColorToken.TextMuted, new Color(0.66f, 0.70f, 0.78f, 1f))
                : new Color(0.66f, 0.70f, 0.78f, 1f);
            Color accent = theme != null
                ? theme.GetColor(NowColorToken.Accent, new Color(0.1f, 0.45f, 0.91f, 1f))
                : new Color(0.1f, 0.45f, 0.91f, 1f);
            Color border = theme != null
                ? theme.GetColor(NowColorToken.Border, new Color(0.28f, 0.32f, 0.40f, 1f))
                : new Color(0.28f, 0.32f, 0.40f, 1f);

            return new NowNodeGraphStyle
            {
                drawBackground = true,
                drawGrid = true,
                background = Darken(background, 0.9f),
                gridMinor = WithAlpha(text, 0.055f),
                gridMajor = WithAlpha(text, 0.105f),
                node = surface,
                nodeTitle = muted,
                nodeSelected = Color.Lerp(surface, accent, 0.12f),
                border = border,
                selectedBorder = accent,
                connection = accent,
                text = text,
                textMuted = textMuted,
                inputPort = new Color(0.45f, 0.73f, 1f, 1f),
                outputPort = new Color(1f, 0.67f, 0.36f, 1f),
                compatiblePort = accent,
                incompatiblePort = new Color(0.85f, 0.22f, 0.20f, 1f),
                selectionFill = WithAlpha(accent, 0.14f),
                selectionBorder = WithAlpha(accent, 0.62f),
                gridSpacing = 24f,
                gridMajorEvery = 4,
                nodeRadius = 6f,
                titleHeight = 30f,
                portRadius = 5f,
                portRowHeight = 24f,
                portInset = 12f,
                contentPadding = 12f,
                portLabelLaneWidth = 72f,
                connectionWidth = 3f,
                minZoom = 0.35f,
                maxZoom = 2.25f,
                zoomStep = 1.12f
            };
        }

        static Color Darken(Color color, float multiplier)
        {
            color.r *= multiplier;
            color.g *= multiplier;
            color.b *= multiplier;
            return color;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }

    public readonly struct NowNodeGraphBackgroundContext
    {
        public readonly NowRect viewport;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphBackgroundContext(NowRect viewport, NowNodeGraphStyle style)
        {
            this.viewport = viewport;
            this.style = style;
        }
    }

    public readonly struct NowNodeGraphGridContext
    {
        public readonly NowRect viewport;
        public readonly Vector2 pan;
        public readonly float zoom;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphGridContext(NowRect viewport, Vector2 pan, float zoom, NowNodeGraphStyle style)
        {
            this.viewport = viewport;
            this.pan = pan;
            this.zoom = zoom;
            this.style = style;
        }
    }

    public readonly struct NowNodeGraphLinkContext
    {
        public readonly NowRect viewport;
        public readonly NowNodeLink link;
        public readonly Vector2 from;
        public readonly Vector2 to;
        public readonly Color color;
        public readonly float width;
        public readonly bool pending;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphLinkContext(
            NowRect viewport,
            NowNodeLink link,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            bool pending,
            NowNodeGraphStyle style)
        {
            this.viewport = viewport;
            this.link = link;
            this.from = from;
            this.to = to;
            this.color = color;
            this.width = width;
            this.pending = pending;
            this.style = style;
        }
    }

    public readonly struct NowNodeGraphNodeContext
    {
        public readonly NowNodeGraph graph;
        public readonly NowNode node;
        public readonly int nodeIndex;
        public readonly NowRect nodeRect;
        public readonly NowRect titleRect;
        public readonly NowRect bodyRect;
        public readonly bool selected;
        public readonly float zoom;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphNodeContext(
            NowNodeGraph graph,
            NowNode node,
            int nodeIndex,
            NowRect nodeRect,
            NowRect titleRect,
            NowRect bodyRect,
            bool selected,
            float zoom,
            NowNodeGraphStyle style)
        {
            this.graph = graph;
            this.node = node;
            this.nodeIndex = nodeIndex;
            this.nodeRect = nodeRect;
            this.titleRect = titleRect;
            this.bodyRect = bodyRect;
            this.selected = selected;
            this.zoom = zoom;
            this.style = style;
        }
    }

    public readonly struct NowNodeGraphPortContext
    {
        public readonly NowNodeGraph graph;
        public readonly NowNode node;
        public readonly NowNodePort port;
        public readonly int nodeIndex;
        public readonly int portIndex;
        public readonly NowNodePortDirection direction;
        public readonly NowRect nodeRect;
        public readonly Vector2 center;
        public readonly float radius;
        public readonly float hover;
        public readonly bool compatibleDropTarget;
        public readonly Color color;
        public readonly float zoom;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphPortContext(
            NowNodeGraph graph,
            NowNode node,
            NowNodePort port,
            int nodeIndex,
            int portIndex,
            NowNodePortDirection direction,
            NowRect nodeRect,
            Vector2 center,
            float radius,
            float hover,
            bool compatibleDropTarget,
            Color color,
            float zoom,
            NowNodeGraphStyle style)
        {
            this.graph = graph;
            this.node = node;
            this.port = port;
            this.nodeIndex = nodeIndex;
            this.portIndex = portIndex;
            this.direction = direction;
            this.nodeRect = nodeRect;
            this.center = center;
            this.radius = radius;
            this.hover = hover;
            this.compatibleDropTarget = compatibleDropTarget;
            this.color = color;
            this.zoom = zoom;
            this.style = style;
        }
    }

    public readonly struct NowNodeGraphSelectionContext
    {
        public readonly NowRect viewport;
        public readonly NowRect rect;
        public readonly NowNodeGraphStyle style;

        public NowNodeGraphSelectionContext(NowRect viewport, NowRect rect, NowNodeGraphStyle style)
        {
            this.viewport = viewport;
            this.rect = rect;
            this.style = style;
        }
    }

    public interface INowNodeGraphRenderer
    {
        void DrawBackground(in NowNodeGraphBackgroundContext context);

        void DrawGrid(in NowNodeGraphGridContext context);

        void DrawLink(in NowNodeGraphLinkContext context);

        void DrawNode(in NowNodeGraphNodeContext context);

        void DrawPort(in NowNodeGraphPortContext context);

        void DrawSelection(in NowNodeGraphSelectionContext context);
    }

    public class NowNodeGraphDefaultRenderer : INowNodeGraphRenderer
    {
        public static readonly NowNodeGraphDefaultRenderer Instance = new NowNodeGraphDefaultRenderer();

        public virtual void DrawBackground(in NowNodeGraphBackgroundContext context)
        {
            Now.Rectangle(context.viewport).SetColor(context.style.background).SetRadius(4f).Draw();
        }

        public virtual void DrawGrid(in NowNodeGraphGridContext context)
        {
            var style = context.style;
            float step = Mathf.Max(4f, style.gridSpacing * context.zoom);
            int majorEvery = Mathf.Max(1, style.gridMajorEvery);
            Vector2 origin = context.viewport.position + context.pan;
            float firstX = FirstGridLine(context.viewport.x, origin.x, step);
            float firstY = FirstGridLine(context.viewport.y, origin.y, step);

            for (float x = firstX; x < context.viewport.xMax; x += step)
            {
                int index = Mathf.RoundToInt((x - origin.x) / step);
                bool major = index % majorEvery == 0;
                Now.Rectangle(new NowRect(x, context.viewport.y, 1f, context.viewport.height))
                    .SetColor(major ? style.gridMajor : style.gridMinor)
                    .Draw();
            }

            for (float y = firstY; y < context.viewport.yMax; y += step)
            {
                int index = Mathf.RoundToInt((y - origin.y) / step);
                bool major = index % majorEvery == 0;
                Now.Rectangle(new NowRect(context.viewport.x, y, context.viewport.width, 1f))
                    .SetColor(major ? style.gridMajor : style.gridMinor)
                    .Draw();
            }
        }

        public virtual void DrawLink(in NowNodeGraphLinkContext context)
        {
            DrawConnection(context.from, context.to, context.color, context.width, context.viewport);
        }

        public virtual void DrawNode(in NowNodeGraphNodeContext context)
        {
            var style = context.style;
            Color nodeColor = context.node.color.a > 0f
                ? context.node.color
                : context.selected ? style.nodeSelected : style.node;

            float outline = context.selected ? 2f : 1f;

            Now.Rectangle(context.nodeRect)
                .SetColor(nodeColor)
                .SetRadius(style.nodeRadius)
                .Draw();

            var titleFill = context.titleRect.Inset(outline, outline, outline, 0f);
            float titleRadius = Mathf.Max(0f, style.nodeRadius - outline);

            Now.Rectangle(titleFill)
                .SetColor(style.nodeTitle)
                .SetRadius(NowCornerRadius.Top(titleRadius))
                .Draw();

            Now.Rectangle(context.nodeRect)
                .SetColor(Color.clear)
                .SetRadius(style.nodeRadius)
                .SetOutline(outline)
                .SetOutlineColor(context.selected ? style.selectedBorder : style.border)
                .Draw();

            DrawText(
                context.titleRect.Inset(10f, 0f),
                context.node.title,
                13f,
                style.text,
                NowTextStyle.Button);
        }

        public virtual void DrawPort(in NowNodeGraphPortContext context)
        {
            var style = context.style;

            Now.Rectangle(new NowRect(
                    context.center.x - context.radius,
                    context.center.y - context.radius,
                    context.radius * 2f,
                    context.radius * 2f))
                .SetColor(context.color)
                .SetRadius(context.radius)
                .SetOutline(1f)
                .SetOutlineColor(style.border)
                .Draw();

            string label = context.port.label ?? string.Empty;

            if (string.IsNullOrEmpty(label))
                return;

            float scaledFontSize = 11.5f * NowControls.controlScale;
            var textStyle = NowTheme.themeAsset.Text(default, NowTextStyle.Muted)
                .SetFontSize(scaledFontSize);

            if (textStyle.font == null)
                return;

            Vector2 size = textStyle.Measure(label);
            float labelLane = style.portLabelLaneWidth * context.zoom;
            NowRect labelRect;

            if (context.direction == NowNodePortDirection.Input)
            {
                float maxWidth = labelLane - (context.radius + 6f);
                float width = Mathf.Min(size.x + 1f, Mathf.Max(1f, maxWidth));
                labelRect = new NowRect(
                    context.center.x + context.radius + 6f,
                    context.center.y - size.y * 0.5f,
                    width,
                    size.y + 2f);
            }
            else
            {
                float maxWidth = labelLane - (context.radius + 6f);
                float width = Mathf.Min(size.x + 1f, Mathf.Max(1f, maxWidth));
                labelRect = new NowRect(
                    context.center.x - context.radius - 6f - width,
                    context.center.y - size.y * 0.5f,
                    width,
                    size.y + 2f);
            }

            textStyle.SetPosition(labelRect).SetMask(labelRect.Outset(2f)).SetColor(style.textMuted).Draw(label);
        }

        public virtual void DrawSelection(in NowNodeGraphSelectionContext context)
        {
            if (context.rect.isEmpty)
                return;

            Now.Rectangle(context.rect)
                .SetColor(context.style.selectionFill)
                .SetRadius(2f)
                .SetOutline(1f)
                .SetOutlineColor(context.style.selectionBorder)
                .Draw();
        }

        static float FirstGridLine(float min, float origin, float step)
        {
            return origin + Mathf.Floor((min - origin) / step) * step;
        }

        static void DrawConnection(Vector2 from, Vector2 to, Color color, float width, NowRect mask)
        {
            float tangent = Mathf.Max(42f, Mathf.Abs(to.x - from.x) * 0.45f);
            Vector2 c1 = from + Vector2.right * tangent;
            Vector2 c2 = to + Vector2.left * tangent;

            Now.Bezier(from, c1, c2, to)
                .SetColor(color)
                .SetWidth(Mathf.Max(1f, width))
                .SetCap(NowLineCap.Round)
                .SetMask(mask)
                .Draw();
        }

        static void DrawText(NowRect rect, string value, float fontSize, Color color, NowTextStyle style)
        {
            if (string.IsNullOrEmpty(value))
                return;

            float scaledFontSize = fontSize * NowControls.controlScale;
            var text = NowTheme.themeAsset.Text(rect, style)
                .SetFontSize(scaledFontSize)
                .SetColor(color)
                .SetMask(rect);

            if (text.font != null)
                text.Draw(value);
        }
    }

    public sealed class NowNodeGraphContextMenu
    {
        public bool undoRedo = true;
        public bool deleteSelection = true;
        public string undoLabel = "Undo";
        public string redoLabel = "Redo";
        public string deleteSelectionLabel = "Delete Selection";
        public Func<NowNodeGraph, NowNodeGraphHistory, bool> drawCustomItems;

        public bool Draw(int id, NowNodeGraph graph, NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            if (graph == null || !NowContextMenu.Begin(id))
                return false;

            bool changed = false;

            if (undoRedo && history != null)
            {
                if (NowContextMenu.Item(undoLabel) && history.Undo(graph))
                {
                    result.changed = true;
                    result.undo = true;
                    result.selectionChanged = true;
                    changed = true;
                }

                if (NowContextMenu.Item(redoLabel) && history.Redo(graph))
                {
                    result.changed = true;
                    result.redo = true;
                    result.selectionChanged = true;
                    changed = true;
                }
            }

            if (deleteSelection && NowContextMenu.Item(deleteSelectionLabel))
            {
                if (graph.SelectedNodeCount() > 0)
                {
                    history?.Record(graph);
                    int removed = graph.RemoveSelectedNodes();

                    if (removed > 0)
                    {
                        result.changed = true;
                        result.nodesDeleted = true;
                        result.selectionChanged = true;
                        changed = true;
                    }
                }
            }

            if (drawCustomItems != null && drawCustomItems(graph, history))
            {
                result.changed = true;
                changed = true;
            }

            NowContextMenu.End();

            if (changed)
                NowControlState.RequestRepaint();

            return changed;
        }
    }

    public sealed class NowNodeGraphView
    {
        public NowNodeGraph graph;
        public NowNodeGraphSchema schema;
        public INowNodeGraphRenderer renderer;
        public NowNodeGraphHistory history;
        public NowNodeGraphContextMenu contextMenu;

        public NowNodeGraphView(NowNodeGraph graph, NowNodeGraphSchema schema = null)
        {
            this.graph = graph;
            this.schema = schema;
        }

        public NowNodeGraphView SetSchema(NowNodeGraphSchema schema)
        {
            this.schema = schema;
            return this;
        }

        public NowNodeGraphView SetRenderer(INowNodeGraphRenderer renderer)
        {
            this.renderer = renderer;
            return this;
        }

        public NowNodeGraphView SetHistory(NowNodeGraphHistory history)
        {
            this.history = history;
            return this;
        }

        public NowNodeGraphView SetContextMenu(NowNodeGraphContextMenu contextMenu)
        {
            this.contextMenu = contextMenu;
            return this;
        }
    }

    public static class NowNodes
    {
        public static NowNodeGraphCanvas Canvas(
            NowNodeGraph graph,
            NowRect rect,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowNodeGraphCanvas(graph, rect, id, NowControls.SiteId(file, line));
        }

        public static NowNodeGraphCanvas Canvas(
            NowNodeGraphView view,
            NowRect rect,
            NowId id = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return new NowNodeGraphCanvas(view, rect, id, NowControls.SiteId(file, line));
        }
    }

    [NowBuilder]
    public struct NowNodeGraphCanvas
    {
        struct CanvasState
        {
            public byte initialized;
            public Vector2 pan;
            public float zoom;
            public byte linkActive;
            public int linkNodeIndex;
            public int linkPortIndex;
            public NowNodePortDirection linkDirection;
            public byte selectionActive;
            public Vector2 selectionStart;
            public Vector2 selectionEnd;
            public NowNodeContentContext contentContext;
        }

        struct PortHit
        {
            public bool valid;
            public int nodeIndex;
            public int portIndex;
            public NowNodePortDirection direction;
            public Vector2 center;
        }

        const int DeleteShortcutSeed = 0x4e474453;

        readonly NowNodeGraphView _view;
        readonly NowNodeGraph _graph;
        readonly NowRect _rect;
        readonly NowId _id;
        readonly int _site;
        NowNodeGraphStyle _style;
        NowNodeGraphSchema _schema;
        INowNodeGraphRenderer _renderer;
        NowNodeGraphHistory _history;
        NowNodeGraphContextMenu _contextMenu;
        bool _hasStyle;
        Action<NowNode, NowRect> _legacyNodeContent;
        NowNodeContentRenderer _nodeContent;

        internal NowNodeGraphCanvas(NowNodeGraph graph, NowRect rect, NowId id, int site)
        {
            _view = null;
            _graph = graph;
            _rect = rect;
            _id = id;
            _site = site;
            _style = default;
            _schema = null;
            _renderer = null;
            _history = null;
            _contextMenu = null;
            _hasStyle = false;
            _legacyNodeContent = null;
            _nodeContent = null;
        }

        internal NowNodeGraphCanvas(NowNodeGraphView view, NowRect rect, NowId id, int site)
        {
            _view = view;
            _graph = view?.graph;
            _rect = rect;
            _id = id;
            _site = site;
            _style = default;
            _schema = null;
            _renderer = null;
            _history = null;
            _contextMenu = null;
            _hasStyle = false;
            _legacyNodeContent = null;
            _nodeContent = null;
        }

        public NowNodeGraphCanvas SetStyle(NowNodeGraphStyle style)
        {
            _style = style;
            _hasStyle = true;
            return this;
        }

        public NowNodeGraphCanvas SetGrid(bool draw)
        {
            EnsureStyle();
            _style.drawGrid = draw;
            return this;
        }

        public NowNodeGraphCanvas SetBackground(bool draw)
        {
            EnsureStyle();
            _style.drawBackground = draw;
            return this;
        }

        public NowNodeGraphCanvas SetZoomRange(float minZoom, float maxZoom)
        {
            EnsureStyle();
            _style.minZoom = Mathf.Max(0.05f, minZoom);
            _style.maxZoom = Mathf.Max(_style.minZoom, maxZoom);
            return this;
        }

        public NowNodeGraphCanvas SetSchema(NowNodeGraphSchema schema)
        {
            _schema = schema;
            return this;
        }

        public NowNodeGraphCanvas SetRenderer(INowNodeGraphRenderer renderer)
        {
            _renderer = renderer;
            return this;
        }

        public NowNodeGraphCanvas SetHistory(NowNodeGraphHistory history)
        {
            _history = history;
            return this;
        }

        public NowNodeGraphCanvas SetContextMenu(NowNodeGraphContextMenu contextMenu)
        {
            _contextMenu = contextMenu;
            return this;
        }

        public NowNodeGraphCanvas SetNodeContent(Action<NowNode, NowRect> draw)
        {
            _legacyNodeContent = draw;
            _nodeContent = null;
            return this;
        }

        public NowNodeGraphCanvas SetNodeContent(NowNodeContentRenderer draw)
        {
            _nodeContent = draw;
            _legacyNodeContent = null;
            return this;
        }

        void EnsureStyle()
        {
            if (_hasStyle)
                return;

            _style = NowNodeGraphStyle.Default(NowTheme.themeAsset);
            _hasStyle = true;
        }

        [NowConsumer]
        public NowNodeGraphResult Draw()
        {
            if (_graph == null)
                throw new InvalidOperationException("NowNodes.Canvas requires a NowNodeGraph instance.");

            var result = default(NowNodeGraphResult);

            if (_rect.isEmpty)
                return result;

            var theme = NowTheme.themeAsset;
            var style = _hasStyle ? _style : NowNodeGraphStyle.Default(theme);
            NormalizeStyle(ref style);
            var schema = _schema ?? _view?.schema ?? _graph.schema;
            var renderer = _renderer ?? _view?.renderer ?? NowNodeGraphDefaultRenderer.Instance;
            var history = _history ?? _view?.history;
            var contextMenu = _contextMenu ?? _view?.contextMenu;

            if (_schema != null)
                _graph.schema = _schema;

            int id = NowControls.GetControlId(_id, _site);
            ref var state = ref NowControlState.Get<CanvasState>(id);
            InitializeState(ref state, style);

            int focusId = NowInput.GetId(id, "focus");
            RegisterCanvasFocus(focusId);
            HandleKeyboardShortcuts(focusId, history, ref result);
            HandleCanvasNavigation(id, ref state, style, ref result);

            if (style.drawBackground)
                renderer.DrawBackground(new NowNodeGraphBackgroundContext(_rect, style));

            using (Now.Mask(_rect))
            {
                if (style.drawGrid)
                    renderer.DrawGrid(new NowNodeGraphGridContext(_rect, state.pan, state.zoom, style));

                HandleInteractions(id, ref state, style, history, contextMenu, ref result);
                DrawLinks(state, style, renderer);

                if (state.linkActive != 0)
                    DrawPendingLink(state, style, renderer);

                DrawNodes(id, ref state, style, schema, renderer, ref result);

                if (state.selectionActive != 0)
                    renderer.DrawSelection(new NowNodeGraphSelectionContext(_rect, SelectionScreenRect(state), style));
            }

            contextMenu?.Draw(NowInput.GetId(id, "context-menu"), _graph, history, ref result);
            result.selectedNodeId = _graph.selectedNodeId;
            return result;
        }

        void RegisterCanvasFocus(int focusId)
        {
            NowFocus.Register(focusId, _rect);

            if (NowInput.isPassive || !NowInput.IsHovered(_rect))
                return;

            if (NowInput.current.pointerButtonsPressed != NowPointerButtons.None)
                NowFocus.Focus(focusId);
        }

        void HandleKeyboardShortcuts(int focusId, NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            if (NowInput.isPassive || !NowFocus.IsFocused(focusId))
                return;

            var frame = NowTextInput.current;

            if (history != null && frame.undoPressed)
            {
                if (history.Undo(_graph))
                {
                    result.changed = true;
                    result.undo = true;
                    NowControlState.RequestRepaint();
                }

                return;
            }

            if (history != null && frame.redoPressed && history.Redo(_graph))
            {
                result.changed = true;
                result.redo = true;
                NowControlState.RequestRepaint();

                return;
            }

            if (!NowContextMenu.isOpen &&
                !NowOverlay.hasOpenOverlay &&
                NowControlState.Repeat(NowInput.CombineId(focusId, DeleteShortcutSeed), frame.deleteHeld))
            {
                DeleteSelectedNodes(history, ref result);
            }
        }

        void DeleteSelectedNodes(NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            if (_graph.SelectedNodeCount() <= 0)
                return;

            history?.Record(_graph);
            int removed = _graph.RemoveSelectedNodes();

            if (removed <= 0)
                return;

            result.changed = true;
            result.nodesDeleted = true;
            result.selectionChanged = true;
            NowControlState.RequestRepaint();
        }

        void HandleCanvasNavigation(int id, ref CanvasState state, NowNodeGraphStyle style, ref NowNodeGraphResult result)
        {
            var pan = NowInput.Interact(NowInput.GetId(id, "pan"), _rect, NowPointerButton.Middle);

            if (pan.dragging)
            {
                state.pan += pan.dragDelta;
                result.changed = true;
                result.panned = true;
                NowControlState.RequestRepaint();
            }

            Vector2 wheel = NowInput.ConsumeScrollDelta(_rect);

            if (wheel.y != 0f && NowInput.current.hasPointer)
            {
                Vector2 pointer = NowInput.current.pointerPosition;
                Vector2 graphPoint = ScreenToGraph(pointer, _rect, state);
                float previousZoom = state.zoom;
                state.zoom = Mathf.Clamp(state.zoom * Mathf.Pow(style.zoomStep, wheel.y), style.minZoom, style.maxZoom);

                if (!Mathf.Approximately(previousZoom, state.zoom))
                {
                    state.pan = pointer - _rect.position - graphPoint * state.zoom;
                    result.changed = true;
                    result.zoomed = true;
                    NowControlState.RequestRepaint();
                }
            }
        }

        void HandleInteractions(
            int id,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            NowNodeGraphContextMenu contextMenu,
            ref NowNodeGraphResult result)
        {
            bool pointerOverNodeOrPort = false;

            if (NowInput.current.hasPointer)
            {
                pointerOverNodeOrPort =
                    FindPortAt(NowInput.current.pointerPosition, state, style, out _) ||
                    FindNodeAt(NowInput.current.pointerPosition, state, style) >= 0;
            }

            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node == null)
                    continue;

                InteractPorts(id, n, node, NowNodePortDirection.Input, ref state, style, history, ref result);
                InteractPorts(id, n, node, NowNodePortDirection.Output, ref state, style, history, ref result);

                var titleRect = NodeTitleScreenRect(node, _rect, state, style);
                var nodeInteraction = NowInput.Interact(NodeControlId(id, n, node), titleRect);

                if (nodeInteraction.pressed && !_graph.IsNodeSelected(node.id))
                    SelectNode(node.id, ref result);

                if (nodeInteraction.dragging)
                {
                    if (nodeInteraction.dragStarted)
                        history?.Record(_graph);

                    MoveSelectedNodes(node, nodeInteraction.dragDelta / Mathf.Max(state.zoom, 0.001f));
                    result.changed = true;
                    result.nodeMoved = true;
                    NowControlState.RequestRepaint();
                }
            }

            if (state.linkActive != 0 && NowInput.WasPointerReleased(NowPointerButton.Primary))
                CommitPendingLink(ref state, style, history, ref result);

            int backgroundId = NowInput.GetId(id, "background");
            bool backgroundOwnsPointer = NowInput.activeId == backgroundId && NowInput.activeButton == NowPointerButton.Primary;
            var backgroundRect = !pointerOverNodeOrPort || backgroundOwnsPointer || state.selectionActive != 0
                ? _rect
                : default;
            var background = NowInput.Interact(backgroundId, backgroundRect);

            if (background.pressed && !pointerOverNodeOrPort && state.linkActive == 0)
            {
                state.selectionActive = 1;
                state.selectionStart = background.pointerPosition;
                state.selectionEnd = background.pointerPosition;
                NowControlState.RequestRepaint();
            }

            if (state.selectionActive != 0 && background.active)
            {
                state.selectionEnd = background.pointerPosition;

                if (background.dragging)
                    NowControlState.RequestRepaint();

                if (background.released)
                    CompleteDragSelection(ref state, style, ref result);
            }
            else if (state.selectionActive != 0 && NowInput.WasPointerReleased(NowPointerButton.Primary))
            {
                CompleteDragSelection(ref state, style, ref result);
            }

            if (background.clicked && !pointerOverNodeOrPort && state.linkActive == 0)
                SelectNode(null, ref result);

            if (contextMenu != null)
            {
                int menuId = NowInput.GetId(id, "context-menu");
                var menuInteraction = NowInput.Interact(menuId, _rect, NowPointerButton.Secondary);

                if (menuInteraction.clicked && state.linkActive == 0)
                {
                    NowContextMenu.Open(menuId, menuInteraction.pointerPosition);
                    result.contextMenuOpened = true;
                    NowControlState.RequestRepaint();
                }
            }
        }

        void InteractPorts(
            int canvasId,
            int nodeIndex,
            NowNode node,
            NowNodePortDirection direction,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

            for (int p = 0; p < ports.Count; ++p)
            {
                var port = ports[p];

                if (port == null)
                    continue;

                Vector2 center = PortScreenPosition(node, direction, p, _rect, state, style);
                var hit = new NowRect(center.x - 9f, center.y - 9f, 18f, 18f);
                int portId = PortControlId(canvasId, nodeIndex, node, direction, p, port);
                var primary = NowInput.Interact(portId, hit);

                if (primary.pressed)
                {
                    state.linkActive = 1;
                    state.linkNodeIndex = nodeIndex;
                    state.linkPortIndex = p;
                    state.linkDirection = direction;
                    SelectNode(node.id, ref result);
                    NowControlState.RequestRepaint();
                }

                if (primary.dragging)
                    NowControlState.RequestRepaint();

                int removeId = NowInput.CombineId(portId, 0x524d);
                var secondary = NowInput.Interact(removeId, hit, NowPointerButton.Secondary);

                if (secondary.clicked)
                {
                    if (!HasLinksForPort(node.id, port.id))
                        continue;

                    history?.Record(_graph);
                    int removed = _graph.RemoveLinksForPort(node.id, port.id);

                    if (removed > 0)
                    {
                        result.changed = true;
                        result.linkRemoved = true;
                        result.removedLink = default;
                        NowControlState.RequestRepaint();
                    }
                }
            }
        }

        void CommitPendingLink(
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            if (!TryGetPort(state.linkNodeIndex, state.linkDirection, state.linkPortIndex, out var sourceNode, out var sourcePort))
            {
                ClearLinkDrag(ref state);
                return;
            }

            if (FindPortAt(NowInput.current.pointerPosition, state, style, out var target) &&
                TryGetPort(target.nodeIndex, target.direction, target.portIndex, out var targetNode, out var targetPort) &&
                _graph.TryCreateLink(sourceNode.id, sourcePort.id, targetNode.id, targetPort.id, out var link) &&
                !HasLink(link))
            {
                history?.Record(_graph);

                if (_graph.TryAddLink(link))
                {
                    result.changed = true;
                    result.linkCreated = true;
                    result.createdLink = link;
                }
            }

            ClearLinkDrag(ref state);
            NowControlState.RequestRepaint();
        }

        void ClearLinkDrag(ref CanvasState state)
        {
            state.linkActive = 0;
            state.linkNodeIndex = 0;
            state.linkPortIndex = 0;
            state.linkDirection = default;
        }

        bool HasLink(NowNodeLink link)
        {
            for (int i = 0; i < _graph.links.Count; ++i)
            {
                if (_graph.links[i] == link)
                    return true;
            }

            return false;
        }

        bool HasLinksForPort(string nodeId, string portId)
        {
            for (int i = 0; i < _graph.links.Count; ++i)
            {
                var link = _graph.links[i];

                if ((link.inputNodeId == nodeId && link.inputPortId == portId) ||
                    (link.outputNodeId == nodeId && link.outputPortId == portId))
                {
                    return true;
                }
            }

            return false;
        }

        void SelectNode(string nodeId, ref NowNodeGraphResult result)
        {
            string previous = _graph.selectedNodeId;
            int previousCount = _graph.SelectedNodeCount();
            _graph.SelectNode(nodeId);

            if (previous != _graph.selectedNodeId || previousCount != _graph.SelectedNodeCount())
            {
                result.changed = true;
                result.selectionChanged = true;
                NowControlState.RequestRepaint();
            }
        }

        void MoveSelectedNodes(NowNode activeNode, Vector2 delta)
        {
            if (delta == Vector2.zero)
                return;

            if (activeNode != null && _graph.IsNodeSelected(activeNode.id) && _graph.SelectedNodeCount() > 1)
            {
                for (int i = 0; i < _graph.nodes.Count; ++i)
                {
                    var node = _graph.nodes[i];

                    if (node != null && _graph.IsNodeSelected(node.id))
                        node.position += delta;
                }
            }
            else if (activeNode != null)
            {
                activeNode.position += delta;
            }
        }

        static NowRect SelectionScreenRect(CanvasState state)
        {
            float xMin = Mathf.Min(state.selectionStart.x, state.selectionEnd.x);
            float yMin = Mathf.Min(state.selectionStart.y, state.selectionEnd.y);
            float xMax = Mathf.Max(state.selectionStart.x, state.selectionEnd.x);
            float yMax = Mathf.Max(state.selectionStart.y, state.selectionEnd.y);
            return new NowRect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        void CompleteDragSelection(ref CanvasState state, NowNodeGraphStyle style, ref NowNodeGraphResult result)
        {
            var rect = SelectionScreenRect(state);
            state.selectionActive = 0;

            if (rect.width <= 2f && rect.height <= 2f)
            {
                SelectNode(null, ref result);
                NowControlState.RequestRepaint();
                return;
            }

            int previousCount = _graph.SelectedNodeCount();
            string previous = _graph.selectedNodeId;
            _graph.ClearSelection();

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node == null)
                    continue;

                if (rect.Overlaps(NodeScreenRect(node, _rect, state, style)))
                    _graph.AddNodeToSelection(node.id);
            }

            if (previous != _graph.selectedNodeId || previousCount != _graph.SelectedNodeCount())
            {
                result.changed = true;
                result.selectionChanged = true;
                result.dragSelected = true;
            }

            NowControlState.RequestRepaint();
        }

        void DrawLinks(CanvasState state, NowNodeGraphStyle style, INowNodeGraphRenderer renderer)
        {
            for (int i = 0; i < _graph.links.Count; ++i)
            {
                var link = _graph.links[i];

                if (!TryGetPortPosition(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, state, style, out var from) ||
                    !TryGetPortPosition(link.inputNodeId, link.inputPortId, NowNodePortDirection.Input, state, style, out var to))
                {
                    continue;
                }

                renderer.DrawLink(new NowNodeGraphLinkContext(
                    _rect,
                    link,
                    from,
                    to,
                    style.connection,
                    style.connectionWidth,
                    false,
                    style));
            }
        }

        void DrawPendingLink(CanvasState state, NowNodeGraphStyle style, INowNodeGraphRenderer renderer)
        {
            if (!TryGetPort(state.linkNodeIndex, state.linkDirection, state.linkPortIndex, out var node, out var port))
                return;

            Vector2 anchor = PortScreenPosition(node, state.linkDirection, state.linkPortIndex, _rect, state, style);
            Vector2 pointer = NowInput.current.hasPointer ? NowInput.current.pointerPosition : anchor;
            Color color = style.connection;

            if (FindPortAt(pointer, state, style, out var target) &&
                TryGetPort(target.nodeIndex, target.direction, target.portIndex, out var targetNode, out var targetPort))
            {
                color = _graph.TryCreateLink(node.id, port.id, targetNode.id, targetPort.id, out _)
                    ? style.compatiblePort
                    : style.incompatiblePort;
            }

            Vector2 from;
            Vector2 to;

            if (state.linkDirection == NowNodePortDirection.Output)
            {
                from = anchor;
                to = pointer;
            }
            else
            {
                from = pointer;
                to = anchor;
            }

            renderer.DrawLink(new NowNodeGraphLinkContext(
                _rect,
                default,
                from,
                to,
                color,
                style.connectionWidth,
                true,
                style));
        }

        void DrawNodes(
            int canvasId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            INowNodeGraphRenderer renderer,
            ref NowNodeGraphResult result)
        {
            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node != null && !_graph.IsNodeSelected(node.id))
                    DrawNode(canvasId, i, node, ref state, style, schema, renderer, false, ref result);
            }

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node != null && _graph.IsNodeSelected(node.id))
                    DrawNode(canvasId, i, node, ref state, style, schema, renderer, true, ref result);
            }
        }

        void DrawNode(
            int canvasId,
            int nodeIndex,
            NowNode node,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            INowNodeGraphRenderer renderer,
            bool selected,
            ref NowNodeGraphResult result)
        {
            var rect = NodeScreenRect(node, _rect, state, style);
            var titleRect = NodeTitleScreenRect(node, _rect, state, style);
            var contentRect = NodeContentScreenRect(node, rect, titleRect, state, style);

            using (NowControls.Scale(state.zoom))
            {
                renderer.DrawNode(new NowNodeGraphNodeContext(
                    _graph,
                    node,
                    nodeIndex,
                    rect,
                    titleRect,
                    contentRect,
                    selected,
                    state.zoom,
                    style));

                DrawNodeContent(node, rect, contentRect, ref state, style, schema, selected, ref result);
                DrawPortList(canvasId, nodeIndex, node, NowNodePortDirection.Input, state, style, renderer);
                DrawPortList(canvasId, nodeIndex, node, NowNodePortDirection.Output, state, style, renderer);
            }
        }

        void DrawNodeContent(
            NowNode node,
            NowRect nodeRect,
            NowRect bodyRect,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            bool selected,
            ref NowNodeGraphResult result)
        {
            if (_legacyNodeContent != null)
            {
                _legacyNodeContent(node, bodyRect);
                return;
            }

            var context = state.contentContext;

            if (context == null)
            {
                context = new NowNodeContentContext();
                state.contentContext = context;
            }

            context.graph = _graph;
            context.schema = schema;
            context.node = node;
            context.nodeRect = nodeRect;
            context.bodyRect = bodyRect;
            context.style = style;
            context.zoom = state.zoom;
            context.selected = selected;
            context.changed = false;

            if (_nodeContent != null)
                _nodeContent(context);
            else
                schema?.DrawNodeContent(context);

            if (context.changed)
                result.changed = true;
        }

        void DrawPortList(
            int canvasId,
            int nodeIndex,
            NowNode node,
            NowNodePortDirection direction,
            CanvasState state,
            NowNodeGraphStyle style,
            INowNodeGraphRenderer renderer)
        {
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;
            var nodeRect = NodeScreenRect(node, _rect, state, style);

            for (int i = 0; i < ports.Count; ++i)
            {
                var port = ports[i];

                if (port == null)
                    continue;

                Vector2 center = PortScreenPosition(node, direction, i, _rect, state, style);
                int portId = PortControlId(canvasId, nodeIndex, node, direction, i, port);
                float hover = NowControlState.Transition(portId, NowInput.IsHovered(new NowRect(center.x - 9f, center.y - 9f, 18f, 18f)));
                float radius = Mathf.Max(3.5f, style.portRadius * state.zoom + hover * 1.5f);
                Color color = PortColor(port, direction, style);
                bool compatible = IsCompatibleDropTarget(nodeIndex, direction, i, state);

                if (compatible)
                    color = style.compatiblePort;

                renderer.DrawPort(new NowNodeGraphPortContext(
                    _graph,
                    node,
                    port,
                    nodeIndex,
                    i,
                    direction,
                    nodeRect,
                    center,
                    radius,
                    hover,
                    compatible,
                    color,
                    state.zoom,
                    style));
            }
        }

        bool IsCompatibleDropTarget(int nodeIndex, NowNodePortDirection direction, int portIndex, CanvasState state)
        {
            if (state.linkActive == 0 ||
                (state.linkNodeIndex == nodeIndex && state.linkPortIndex == portIndex && state.linkDirection == direction) ||
                !TryGetPort(state.linkNodeIndex, state.linkDirection, state.linkPortIndex, out var sourceNode, out var sourcePort) ||
                !TryGetPort(nodeIndex, direction, portIndex, out var targetNode, out var targetPort))
            {
                return false;
            }

            return _graph.TryCreateLink(sourceNode.id, sourcePort.id, targetNode.id, targetPort.id, out _);
        }

        Color PortColor(NowNodePort port, NowNodePortDirection direction, NowNodeGraphStyle style)
        {
            if (port.color.a > 0f)
                return port.color;

            return direction == NowNodePortDirection.Input ? style.inputPort : style.outputPort;
        }

        bool TryGetPortPosition(
            string nodeId,
            string portId,
            NowNodePortDirection direction,
            CanvasState state,
            NowNodeGraphStyle style,
            out Vector2 position)
        {
            position = default;
            int nodeIndex = _graph.IndexOfNode(nodeId);

            if (nodeIndex < 0)
                return false;

            var node = _graph.nodes[nodeIndex];
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

            for (int i = 0; i < ports.Count; ++i)
            {
                if (ports[i] != null && ports[i].id == portId)
                {
                    position = PortScreenPosition(node, direction, i, _rect, state, style);
                    return true;
                }
            }

            return false;
        }

        bool FindPortAt(Vector2 point, CanvasState state, NowNodeGraphStyle style, out PortHit hit)
        {
            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node == null)
                    continue;

                if (FindPortAt(node, n, NowNodePortDirection.Input, point, state, style, out hit) ||
                    FindPortAt(node, n, NowNodePortDirection.Output, point, state, style, out hit))
                {
                    return true;
                }
            }

            hit = default;
            return false;
        }

        bool FindPortAt(
            NowNode node,
            int nodeIndex,
            NowNodePortDirection direction,
            Vector2 point,
            CanvasState state,
            NowNodeGraphStyle style,
            out PortHit hit)
        {
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

            for (int p = 0; p < ports.Count; ++p)
            {
                Vector2 center = PortScreenPosition(node, direction, p, _rect, state, style);

                if (new NowRect(center.x - 10f, center.y - 10f, 20f, 20f).Contains(point))
                {
                    hit = new PortHit
                    {
                        valid = true,
                        nodeIndex = nodeIndex,
                        portIndex = p,
                        direction = direction,
                        center = center
                    };
                    return true;
                }
            }

            hit = default;
            return false;
        }

        int FindNodeAt(Vector2 point, CanvasState state, NowNodeGraphStyle style)
        {
            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node != null && NodeScreenRect(node, _rect, state, style).Contains(point))
                    return n;
            }

            return -1;
        }

        bool TryGetPort(int nodeIndex, NowNodePortDirection direction, int portIndex, out NowNode node, out NowNodePort port)
        {
            node = null;
            port = null;

            if (nodeIndex < 0 || nodeIndex >= _graph.nodes.Count)
                return false;

            node = _graph.nodes[nodeIndex];

            if (node == null)
                return false;

            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

            if (portIndex < 0 || portIndex >= ports.Count)
                return false;

            port = ports[portIndex];
            return port != null;
        }

        NowRect NodeScreenRect(NowNode node, NowRect viewport, CanvasState state, NowNodeGraphStyle style)
        {
            var graphRect = new NowRect(node.position, ResolveNodeSize(node, style));
            Vector2 position = GraphToScreen(graphRect.position, viewport, state);
            Vector2 size = graphRect.size * state.zoom;
            return new NowRect(position, size);
        }

        NowRect NodeTitleScreenRect(NowNode node, NowRect viewport, CanvasState state, NowNodeGraphStyle style)
        {
            var rect = NodeScreenRect(node, viewport, state, style);
            return new NowRect(rect.x, rect.y, rect.width, style.titleHeight * state.zoom);
        }

        NowRect NodeContentScreenRect(
            NowNode node,
            NowRect nodeRect,
            NowRect titleRect,
            CanvasState state,
            NowNodeGraphStyle style)
        {
            float zoom = Mathf.Max(state.zoom, 0.001f);
            float padding = style.contentPadding * zoom;
            float labelLane = style.portLabelLaneWidth * zoom;
            bool hasInputs = node.inputs != null && node.inputs.Count > 0;
            bool hasOutputs = node.outputs != null && node.outputs.Count > 0;
            float left = hasInputs ? labelLane : padding;
            float right = hasOutputs ? labelLane : padding;
            float top = titleRect.yMax + padding * 0.5f;
            float bottom = padding;

            return new NowRect(
                nodeRect.x + left,
                top,
                Mathf.Max(0f, nodeRect.width - left - right),
                Mathf.Max(0f, nodeRect.yMax - top - bottom));
        }

        Vector2 ResolveNodeSize(NowNode node, NowNodeGraphStyle style)
        {
            Vector2 size = node.size;

            if (size.x <= 0f)
                size.x = _graph.defaultNodeSize.x > 0f ? _graph.defaultNodeSize.x : 180f;

            if (size.y <= 0f)
                size.y = _graph.defaultNodeSize.y > 0f ? _graph.defaultNodeSize.y : 118f;

            int rows = Mathf.Max(node.inputs != null ? node.inputs.Count : 0, node.outputs != null ? node.outputs.Count : 0);
            float minHeight = style.titleHeight + 12f + rows * style.portRowHeight;
            return new Vector2(Mathf.Max(size.x, 128f), Mathf.Max(size.y, minHeight));
        }

        Vector2 PortScreenPosition(
            NowNode node,
            NowNodePortDirection direction,
            int index,
            NowRect viewport,
            CanvasState state,
            NowNodeGraphStyle style)
        {
            Vector2 size = ResolveNodeSize(node, style);
            float x = direction == NowNodePortDirection.Input ? node.position.x : node.position.x + size.x;
            float y = node.position.y + style.titleHeight + 8f + index * style.portRowHeight + style.portRowHeight * 0.5f;
            return GraphToScreen(new Vector2(x, y), viewport, state);
        }

        static Vector2 GraphToScreen(Vector2 point, NowRect viewport, CanvasState state)
        {
            return viewport.position + state.pan + point * state.zoom;
        }

        static Vector2 ScreenToGraph(Vector2 point, NowRect viewport, CanvasState state)
        {
            return (point - viewport.position - state.pan) / Mathf.Max(state.zoom, 0.001f);
        }

        static int NodeControlId(int canvasId, int nodeIndex, NowNode node)
        {
            return DataId(canvasId, node.id, 0x4e000 + nodeIndex);
        }

        static int PortControlId(
            int canvasId,
            int nodeIndex,
            NowNode node,
            NowNodePortDirection direction,
            int portIndex,
            NowNodePort port)
        {
            int nodeId = NodeControlId(canvasId, nodeIndex, node);
            int dirId = direction == NowNodePortDirection.Input ? 0x491 : 0x4f1;
            int baseId = NowInput.CombineId(nodeId, dirId);
            return DataId(baseId, port.id, portIndex + 1);
        }

        static int DataId(int seed, string value, int fallback)
        {
            return string.IsNullOrEmpty(value)
                ? NowInput.CombineId(seed, fallback)
                : NowInput.GetId(seed, value);
        }

        static void InitializeState(ref CanvasState state, NowNodeGraphStyle style)
        {
            if (state.initialized != 0)
            {
                state.zoom = Mathf.Clamp(state.zoom <= 0f ? 1f : state.zoom, style.minZoom, style.maxZoom);
                return;
            }

            state.initialized = 1;
            state.zoom = 1f;
            state.pan = default;
            state.linkActive = 0;
        }

        static void NormalizeStyle(ref NowNodeGraphStyle style)
        {
            style.gridSpacing = Mathf.Max(4f, style.gridSpacing);
            style.gridMajorEvery = Mathf.Max(1, style.gridMajorEvery);
            style.nodeRadius = Mathf.Max(0f, style.nodeRadius);
            style.titleHeight = Mathf.Max(18f, style.titleHeight);
            style.portRadius = Mathf.Max(2f, style.portRadius);
            style.portRowHeight = Mathf.Max(16f, style.portRowHeight);
            style.portInset = Mathf.Max(4f, style.portInset);
            style.contentPadding = Mathf.Max(0f, style.contentPadding);
            style.portLabelLaneWidth = Mathf.Max(24f, style.portLabelLaneWidth);
            style.connectionWidth = Mathf.Max(1f, style.connectionWidth);
            style.minZoom = Mathf.Max(0.05f, style.minZoom);
            style.maxZoom = Mathf.Max(style.minZoom, style.maxZoom);
            style.zoomStep = Mathf.Max(1.01f, style.zoomStep);
        }
    }
}
