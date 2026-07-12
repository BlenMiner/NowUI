using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
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

    public delegate void NowNodeInitializer(NowNode node);

    public static class NowNodeIds
    {
        static readonly string[] SmallIds = BuildSmallIds();

        public static string FromInt(int id)
        {
            var cache = SmallIds;
            return (uint)id < (uint)cache.Length
                ? cache[id]
                : id.ToString(CultureInfo.InvariantCulture);
        }

        static string[] BuildSmallIds()
        {
            var ids = new string[256];

            for (int i = 0; i < ids.Length; ++i)
                ids[i] = i.ToString(CultureInfo.InvariantCulture);

            return ids;
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
        public NowNodeGraphHistory history;
        public NowNode node;
        public NowRect nodeRect;
        public NowRect bodyRect;
        public NowNodeGraphStyle style;
        public float zoom;
        public bool selected;
        public bool isPreview;
        public bool changed;
        internal NowRect viewportRect;
        internal Vector2 pan;
        internal Now.NowTransform screenTransform;

        /// <summary>
        /// Returns a graph-space value. Node content is drawn inside the graph
        /// transform, so graph-space dimensions scale with the canvas automatically.
        /// </summary>
        public float Scale(float value)
        {
            return value;
        }

        public NowRect Row(int index, float height = 20f, float gap = 4f)
        {
            height = Mathf.Max(0f, Scale(height));
            gap = Mathf.Max(0f, Scale(gap));
            float y = bodyRect.y + index * (height + gap);
            return BodyRowAt(y, height);
        }

        public NowRect RowAt(float yOffset, float height = 20f)
        {
            height = Mathf.Max(0f, Scale(height));
            return BodyRowAt(bodyRect.y + Scale(yOffset), height);
        }

        public NowRect RowAfter(NowRect previous, float height = 20f, float gap = 4f)
        {
            height = Mathf.Max(0f, Scale(height));
            gap = Mathf.Max(0f, Scale(gap));
            float y = previous.isEmpty ? bodyRect.y : previous.yMax + gap;
            return BodyRowAt(y, height);
        }

        /// <summary>
        /// Returns a row spanning the node's padded width instead of the narrower
        /// lane between port labels. Use this for controls placed above ports.
        /// </summary>
        public NowRect FullWidthRow(int index, float height = 20f, float gap = 4f)
        {
            height = Mathf.Max(0f, Scale(height));
            gap = Mathf.Max(0f, Scale(gap));
            float y = bodyRect.y + index * (height + gap);
            return FullWidthRowAtY(y, height);
        }

        public NowRect FullWidthRowAt(float yOffset, float height = 20f)
        {
            height = Mathf.Max(0f, Scale(height));
            return FullWidthRowAtY(bodyRect.y + Scale(yOffset), height);
        }

        public NowRect FullWidthRowAfter(NowRect previous, float height = 20f, float gap = 4f)
        {
            height = Mathf.Max(0f, Scale(height));
            gap = Mathf.Max(0f, Scale(gap));
            float y = previous.isEmpty ? bodyRect.y : previous.yMax + gap;
            return FullWidthRowAtY(y, height);
        }

        NowRect BodyRowAt(float y, float height)
        {
            return new NowRect(bodyRect.x, y, bodyRect.width, Mathf.Max(0f, Mathf.Min(height, bodyRect.yMax - y)));
        }

        NowRect FullWidthRowAtY(float y, float height)
        {
            if (isPreview)
                return BodyRowAt(y, height);

            float padding = Mathf.Max(0f, style.contentPadding);
            return new NowRect(
                nodeRect.x + padding,
                y,
                Mathf.Max(0f, nodeRect.width - padding * 2f),
                Mathf.Max(0f, Mathf.Min(height, bodyRect.yMax - y)));
        }

        public Vector2 GraphToScreen(Vector2 point)
        {
            return new Vector2(
                point.x * screenTransform.scale.x + screenTransform.origin.x,
                point.y * screenTransform.scale.y + screenTransform.origin.y);
        }

        public NowRect GraphToScreen(NowRect rect)
        {
            if (rect.isEmpty)
                return default;

            Vector2 a = GraphToScreen(rect.position);
            Vector2 b = GraphToScreen(new Vector2(rect.xMax, rect.yMax));
            return new NowRect(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Abs(b.x - a.x),
                Mathf.Abs(b.y - a.y));
        }

        public Vector2 GraphVectorToScreen(Vector2 vector)
        {
            return new Vector2(vector.x * screenTransform.scale.x, vector.y * screenTransform.scale.y);
        }

        public float GraphUnitsToScreen(float value)
        {
            return value * Mathf.Max(Mathf.Abs(screenTransform.scale.x), Mathf.Abs(screenTransform.scale.y));
        }

        public Vector2 ScreenToGraph(Vector2 point)
        {
            return new Vector2(
                Mathf.Approximately(screenTransform.scale.x, 0f) ? 0f : (point.x - screenTransform.origin.x) / screenTransform.scale.x,
                Mathf.Approximately(screenTransform.scale.y, 0f) ? 0f : (point.y - screenTransform.origin.y) / screenTransform.scale.y);
        }

        public Vector2 ScreenVectorToGraph(Vector2 vector)
        {
            return new Vector2(
                Mathf.Approximately(screenTransform.scale.x, 0f) ? 0f : vector.x / screenTransform.scale.x,
                Mathf.Approximately(screenTransform.scale.y, 0f) ? 0f : vector.y / screenTransform.scale.y);
        }

        public float ScreenUnitsToGraph(float value)
        {
            float scale = Mathf.Max(Mathf.Abs(screenTransform.scale.x), Mathf.Abs(screenTransform.scale.y));
            return value / Mathf.Max(scale, 0.001f);
        }

        public void Texture(Texture texture, NowRect rect, float radius = 4f)
        {
            Now.Rectangle(rect).SetTexture(texture).SetRadius(Scale(radius)).Draw();
        }

        public void MarkChanged()
        {
            changed = true;
        }

        public void RecordChange()
        {
            history?.Record(graph);
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

        [NonSerialized] string _idHashSource;
        [NonSerialized] int _idHash;

        /// <summary>
        /// Control-id hash of <see cref="id"/>, cached against the current string
        /// instance and recomputed when the id field is reassigned. Returns 0 for
        /// a null or empty id.
        /// </summary>
        internal int idHash
        {
            get
            {
                string value = id;

                if (string.IsNullOrEmpty(value))
                    return 0;

                if (!ReferenceEquals(_idHashSource, value))
                {
                    _idHash = NowInput.GetId(value);
                    _idHashSource = value;
                }

                return _idHash;
            }
        }
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

        public NowNodePortDefinition(string id, string label, NowNodePortDirection direction, int typeId, int maxConnections)
        {
            intId = 0;
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
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
        public string category;
        public string searchDetail;
        public string searchKeywords;
        public Vector2 size;
        public Color color = Color.clear;
        public readonly List<NowNodePortDefinition> inputs = new List<NowNodePortDefinition>(4);
        public readonly List<NowNodePortDefinition> outputs = new List<NowNodePortDefinition>(4);
        public NowNodeContentRenderer renderer;
        public NowNodeContentRenderer previewRenderer;
        public NowNodeInitializer initializer;
        public float previewHeight;
        public float contentHeight = -1f;
        public float portTopOffset;
        public bool reroute;

        public bool hasPreview => previewRenderer != null && previewHeight > 0f;

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

        public NowNodeDefinition SetCategory(string category)
        {
            this.category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            return this;
        }

        public NowNodeDefinition SetSearchDetail(string detail)
        {
            searchDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
            return this;
        }

        public NowNodeDefinition SetSearchKeywords(params string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
            {
                searchKeywords = null;
                return this;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < keywords.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(keywords[i]))
                    continue;

                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(keywords[i].Trim());
            }

            searchKeywords = builder.Length == 0 ? null : builder.ToString();
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

        public NowNodeDefinition SetWidth(float width)
        {
            size = new Vector2(width, -1f);
            return this;
        }

        public NowNodeDefinition SetColor(Color color)
        {
            this.color = color;
            return this;
        }

        public NowNodeDefinition SetContentHeight(float height)
        {
            contentHeight = Mathf.Max(0f, height);
            portTopOffset = Mathf.Max(portTopOffset, contentHeight);
            return this;
        }

        public NowNodeDefinition SetPortTopOffset(float offset)
        {
            portTopOffset = Mathf.Max(0f, offset);
            return this;
        }

        public NowNodeDefinition ClearPorts()
        {
            inputs.Clear();
            outputs.Clear();
            return this;
        }

        public NowNodeDefinition Input(int id, string label, int typeId = 0, int maxConnections = 1)
        {
            AddOrReplacePort(inputs, new NowNodePortDefinition(id, label, NowNodePortDirection.Input, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition Input(string id, string label, int typeId = 0, int maxConnections = 1)
        {
            AddOrReplacePort(inputs, new NowNodePortDefinition(id, label, NowNodePortDirection.Input, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition Output(int id, string label, int typeId = 0, int maxConnections = 0)
        {
            AddOrReplacePort(outputs, new NowNodePortDefinition(id, label, NowNodePortDirection.Output, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition Output(string id, string label, int typeId = 0, int maxConnections = 0)
        {
            AddOrReplacePort(outputs, new NowNodePortDefinition(id, label, NowNodePortDirection.Output, typeId, maxConnections));
            return this;
        }

        public NowNodeDefinition RemoveInput(int id)
        {
            RemovePort(inputs, NowNodeIds.FromInt(id));
            return this;
        }

        public NowNodeDefinition RemoveInput(string id)
        {
            RemovePort(inputs, id);
            return this;
        }

        public NowNodeDefinition RemoveOutput(int id)
        {
            RemovePort(outputs, NowNodeIds.FromInt(id));
            return this;
        }

        public NowNodeDefinition RemoveOutput(string id)
        {
            RemovePort(outputs, id);
            return this;
        }

        public NowNodeDefinition Render(NowNodeContentRenderer renderer)
        {
            this.renderer = renderer;
            return this;
        }

        public NowNodeDefinition Preview(NowNodeContentRenderer renderer, float height = 64f)
        {
            previewRenderer = renderer;
            previewHeight = Mathf.Max(0f, height);
            return this;
        }

        public NowNodeDefinition Initialize(NowNodeInitializer initializer)
        {
            this.initializer = initializer;
            return this;
        }

        static void AddOrReplacePort(List<NowNodePortDefinition> ports, NowNodePortDefinition port)
        {
            for (int i = 0; i < ports.Count; ++i)
            {
                if (ports[i] != null && ports[i].id == port.id)
                {
                    ports[i] = port;
                    return;
                }
            }

            ports.Add(port);
        }

        static bool RemovePort(List<NowNodePortDefinition> ports, string id)
        {
            if (ports == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = ports.Count - 1; i >= 0; --i)
            {
                if (ports[i] == null || ports[i].id != id)
                    continue;

                ports.RemoveAt(i);
                return true;
            }

            return false;
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

            initializer?.Invoke(node);
        }
    }

    public sealed class NowNodeGraphSchema
    {
        readonly Dictionary<int, NowNodeDefinition> _definitions = new Dictionary<int, NowNodeDefinition>();
        readonly List<NowNodeDefinition> _definitionList = new List<NowNodeDefinition>(8);
        readonly List<NowNodeConnectionRule> _connectionRules = new List<NowNodeConnectionRule>(4);
        readonly Dictionary<int, Color> _typeColors = new Dictionary<int, Color>(8);

        public int nodeDefinitionCount => _definitionList.Count;

        public IReadOnlyList<NowNodeDefinition> nodeDefinitions => _definitionList;

        public NowNodeGraphSchema Clear()
        {
            _definitions.Clear();
            _definitionList.Clear();
            _connectionRules.Clear();
            _typeColors.Clear();
            return this;
        }

        public NowNodeDefinition Node(int kindId, string title)
        {
            if (!_definitions.TryGetValue(kindId, out var definition))
            {
                definition = new NowNodeDefinition(kindId, title);
                _definitions.Add(kindId, definition);
                _definitionList.Add(definition);
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

        public bool TryGetNodeSize(int kindId, out Vector2 size)
        {
            if (_definitions.TryGetValue(kindId, out var definition))
            {
                size = definition.size;
                return true;
            }

            size = default;
            return false;
        }

        public const int RerouteInputPortId = 1;
        public const int RerouteOutputPortId = 2;

        public NowNodeDefinition Reroute(int kindId, string title = "Reroute", int typeId = 0)
        {
            var definition = Node(kindId, title);
            definition.reroute = true;
            definition.SetSize(26f, 26f);
            definition.ClearPorts();
            definition.Input(RerouteInputPortId, string.Empty, typeId);
            definition.Output(RerouteOutputPortId, string.Empty, typeId);
            return definition;
        }

        public bool IsReroute(int kindId)
        {
            return _definitions.TryGetValue(kindId, out var definition) && definition.reroute;
        }

        public NowNodeGraphSchema TypeColor(int typeId, Color color)
        {
            _typeColors[typeId] = color;
            return this;
        }

        public bool TryGetTypeColor(int typeId, out Color color)
        {
            return _typeColors.TryGetValue(typeId, out color);
        }

        public bool TryGetPreviewHeight(int kindId, out float height)
        {
            if (_definitions.TryGetValue(kindId, out var definition) && definition.hasPreview)
            {
                height = definition.previewHeight;
                return true;
            }

            height = 0f;
            return false;
        }

        public bool TryGetPortTopOffset(int kindId, out float offset)
        {
            if (_definitions.TryGetValue(kindId, out var definition) && definition.portTopOffset > 0f)
            {
                offset = definition.portTopOffset;
                return true;
            }

            offset = 0f;
            return false;
        }

        public bool TryGetContentHeight(int kindId, out float height)
        {
            if (_definitions.TryGetValue(kindId, out var definition) && definition.contentHeight >= 0f)
            {
                height = definition.contentHeight;
                return true;
            }

            height = 0f;
            return false;
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

        public bool DrawNodePreview(NowNodeContentContext context)
        {
            if (context == null || context.node == null || !TryGetNode(context.node.kindId, out var definition) || definition.previewRenderer == null)
                return false;

            definition.previewRenderer(context);
            return true;
        }
    }

    [Serializable]
    public sealed class NowNodeData
    {
        public string key;
        public string value;

        public NowNodeData()
        {
        }

        public NowNodeData(string key, string value)
        {
            this.key = key ?? string.Empty;
            this.value = value ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class NowNode
    {
        public string id;
        public string title;
        public int kindId;
        public int userId;
        public string userData;
        public string userData2;
        public string userData3;
        public Vector2 position;
        public Vector2 size;
        public Color color = Color.clear;
        public bool selected;
        public bool compact;
        public List<NowNodeData> data = new List<NowNodeData>(4);
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

        [NonSerialized] string _idHashSource;
        [NonSerialized] int _idHash;

        /// <summary>
        /// Control-id hash of <see cref="id"/>, cached against the current string
        /// instance and recomputed when the id field is reassigned. Returns 0 for
        /// a null or empty id.
        /// </summary>
        internal int idHash
        {
            get
            {
                string value = id;

                if (string.IsNullOrEmpty(value))
                    return 0;

                if (!ReferenceEquals(_idHashSource, value))
                {
                    _idHash = NowInput.GetId(value);
                    _idHashSource = value;
                }

                return _idHash;
            }
        }

        public NowNodePort AddInput(string id, string label, int typeId = 0)
        {
            return AddOrUpdatePort(ref inputs, id, label, NowNodePortDirection.Input, typeId, maxConnections: 1);
        }

        public NowNodePort AddInput(int id, string label, int typeId = 0)
        {
            return AddInput(NowNodeIds.FromInt(id), label, typeId);
        }

        public bool UpsertInput(string id, string label, int typeId = 0, int maxConnections = 1)
        {
            return UpsertPort(ref inputs, id, label, NowNodePortDirection.Input, typeId, maxConnections);
        }

        public bool UpsertInput(int id, string label, int typeId = 0, int maxConnections = 1)
        {
            return UpsertInput(NowNodeIds.FromInt(id), label, typeId, maxConnections);
        }

        public NowNodePort AddOutput(string id, string label, int typeId = 0)
        {
            return AddOrUpdatePort(ref outputs, id, label, NowNodePortDirection.Output, typeId, maxConnections: 0);
        }

        public NowNodePort AddOutput(int id, string label, int typeId = 0)
        {
            return AddOutput(NowNodeIds.FromInt(id), label, typeId);
        }

        public bool UpsertOutput(string id, string label, int typeId = 0, int maxConnections = 0)
        {
            return UpsertPort(ref outputs, id, label, NowNodePortDirection.Output, typeId, maxConnections);
        }

        public bool UpsertOutput(int id, string label, int typeId = 0, int maxConnections = 0)
        {
            return UpsertOutput(NowNodeIds.FromInt(id), label, typeId, maxConnections);
        }

        public bool RemoveInput(string id)
        {
            return RemovePort(inputs, id);
        }

        public bool RemoveInput(int id)
        {
            return RemoveInput(NowNodeIds.FromInt(id));
        }

        public bool RemoveOutput(string id)
        {
            return RemovePort(outputs, id);
        }

        public bool RemoveOutput(int id)
        {
            return RemoveOutput(NowNodeIds.FromInt(id));
        }

        public bool TryGetPort(string portId, NowNodePortDirection direction, out NowNodePort port)
        {
            var ports = direction == NowNodePortDirection.Input ? inputs : outputs;

            for (int i = 0; ports != null && i < ports.Count; ++i)
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

        public string GetData(string key, string fallback = null)
        {
            return TryGetData(key, out string value) ? value : fallback;
        }

        public bool TryGetData(string key, out string value)
        {
            int index = IndexOfData(key);

            if (index >= 0)
            {
                value = data[index]?.value ?? string.Empty;
                return true;
            }

            value = null;
            return false;
        }

        public bool SetData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Data key must be non-empty.", nameof(key));

            if (value == null)
                return RemoveData(key);

            if (data == null)
                data = new List<NowNodeData>(4);

            int index = IndexOfData(key);

            if (index >= 0)
            {
                NowNodeData entry = data[index];

                if (entry == null)
                {
                    data[index] = new NowNodeData(key, value);
                    return true;
                }

                bool changed = entry.value != value || entry.key != key;
                entry.key = key;
                entry.value = value;
                return changed;
            }

            data.Add(new NowNodeData(key, value));
            return true;
        }

        public bool RemoveData(string key)
        {
            int index = IndexOfData(key);

            if (index < 0)
                return false;

            data.RemoveAt(index);
            return true;
        }

        int IndexOfData(string key)
        {
            if (string.IsNullOrEmpty(key) || data == null)
                return -1;

            for (int i = 0; i < data.Count; ++i)
            {
                if (data[i] != null && string.Equals(data[i].key, key, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static bool UpsertPort(
            ref List<NowNodePort> ports,
            string id,
            string label,
            NowNodePortDirection direction,
            int typeId,
            int maxConnections)
        {
            if (ports == null)
                ports = new List<NowNodePort>(4);

            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Port id must be non-empty when upserting.", nameof(id));

            string portId = id;
            label ??= string.Empty;

            for (int i = 0; i < ports.Count; ++i)
            {
                NowNodePort port = ports[i];

                if (port == null || port.id != portId)
                    continue;

                bool changed = port.label != label ||
                    port.direction != direction ||
                    port.typeId != typeId ||
                    port.maxConnections != maxConnections;

                port.label = label;
                port.direction = direction;
                port.typeId = typeId;
                port.maxConnections = maxConnections;
                return changed;
            }

            ports.Add(new NowNodePort(portId, label, direction, typeId)
            {
                maxConnections = maxConnections
            });
            return true;
        }

        static NowNodePort AddOrUpdatePort(
            ref List<NowNodePort> ports,
            string id,
            string label,
            NowNodePortDirection direction,
            int typeId,
            int maxConnections)
        {
            if (ports == null)
                ports = new List<NowNodePort>(4);

            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Port id must be non-empty when adding.", nameof(id));

            label ??= string.Empty;

            for (int i = 0; i < ports.Count; ++i)
            {
                NowNodePort port = ports[i];

                if (port == null || port.id != id)
                    continue;

                port.label = label;
                port.direction = direction;
                port.typeId = typeId;
                return port;
            }

            var created = new NowNodePort(id, label, direction, typeId)
            {
                maxConnections = maxConnections
            };
            ports.Add(created);
            return created;
        }

        static bool RemovePort(List<NowNodePort> ports, string id)
        {
            if (ports == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = ports.Count - 1; i >= 0; --i)
            {
                NowNodePort port = ports[i];

                if (port != null && port.id == id)
                {
                    ports.RemoveAt(i);
                    return true;
                }
            }

            return false;
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
        public NowNodeLink selectedLink;
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

        public bool ResetNodeSizeToSchema(NowNode node)
        {
            if (node == null || schema == null || !schema.TryGetNodeSize(node.kindId, out var size))
                return false;

            if (node.size == size)
                return false;

            node.size = size;
            return true;
        }

        public int ResetNodeSizesToSchema()
        {
            if (schema == null || nodes == null)
                return 0;

            int changed = 0;

            for (int i = 0; i < nodes.Count; ++i)
            {
                if (ResetNodeSizeToSchema(nodes[i]))
                    ++changed;
            }

            return changed;
        }

        public int ResetNodeSizesToSchema(NowNodeGraphSchema schema)
        {
            this.schema = schema;
            return ResetNodeSizesToSchema();
        }

        public void Clear()
        {
            nodes.Clear();
            links.Clear();
            selectedNodeId = null;
            selectedNodeIds?.Clear();
            selectedLink = default;
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
            selectedLink = default;

            if (!string.IsNullOrEmpty(nodeId) && FindNode(nodeId) != null)
                selectedNodeIds.Add(nodeId);

            selectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[0] : null;
            SyncSelectionFlags();
        }

        public void ClearSelection()
        {
            selectedNodeId = null;
            selectedNodeIds?.Clear();
            selectedLink = default;
            SyncSelectionFlags();
        }

        public void SelectLink(NowNodeLink link)
        {
            selectedLink = link;

            if (link.isValid)
            {
                selectedNodeId = null;
                selectedNodeIds?.Clear();
                SyncSelectionFlags();
            }
        }

        public void ClearLinkSelection()
        {
            selectedLink = default;
        }

        public bool HasSelectedLink()
        {
            if (!selectedLink.isValid)
                return false;

            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i] == selectedLink)
                    return true;
            }

            return false;
        }

        public int SelectAllNodes()
        {
            EnsureSelectionList();
            selectedNodeIds.Clear();
            selectedLink = default;

            for (int i = 0; i < nodes.Count; ++i)
            {
                if (nodes[i] != null)
                    selectedNodeIds.Add(nodes[i].id);
            }

            selectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[0] : null;
            SyncSelectionFlags();
            return selectedNodeIds.Count;
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

                selectedLink = default;
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

        public bool UpsertNodeInput(NowNode node, string id, string label, int typeId = 0, int maxConnections = 1)
        {
            return UpsertNodePort(node, id, label, NowNodePortDirection.Input, typeId, maxConnections);
        }

        public bool UpsertNodeInput(NowNode node, int id, string label, int typeId = 0, int maxConnections = 1)
        {
            return UpsertNodeInput(node, NowNodeIds.FromInt(id), label, typeId, maxConnections);
        }

        public bool UpsertNodeOutput(NowNode node, string id, string label, int typeId = 0, int maxConnections = 0)
        {
            return UpsertNodePort(node, id, label, NowNodePortDirection.Output, typeId, maxConnections);
        }

        public bool UpsertNodeOutput(NowNode node, int id, string label, int typeId = 0, int maxConnections = 0)
        {
            return UpsertNodeOutput(node, NowNodeIds.FromInt(id), label, typeId, maxConnections);
        }

        public bool RemoveNodeInput(NowNode node, string id)
        {
            return RemoveNodePort(node, id, NowNodePortDirection.Input);
        }

        public bool RemoveNodeInput(NowNode node, int id)
        {
            return RemoveNodeInput(node, NowNodeIds.FromInt(id));
        }

        public bool RemoveNodeOutput(NowNode node, string id)
        {
            return RemoveNodePort(node, id, NowNodePortDirection.Output);
        }

        public bool RemoveNodeOutput(NowNode node, int id)
        {
            return RemoveNodeOutput(node, NowNodeIds.FromInt(id));
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
            if (!CanAddLink(link, replaceInput, default, false, out var outputPort, out var inputPort))
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

        public int PruneInvalidLinks()
        {
            if (links == null || links.Count == 0)
                return 0;

            int removed = 0;
            var seen = new HashSet<NowNodeLink>();
            var inputCounts = new Dictionary<string, int>();
            var outputCounts = new Dictionary<string, int>();

            for (int i = 0; i < links.Count;)
            {
                NowNodeLink link = links[i];

                if (!seen.Add(link) ||
                    !TryResolveLink(link, out _, out NowNodePort outputPort, out _, out NowNodePort inputPort) ||
                    ExceedsPortLimit(inputCounts, LinkInputKey(link), inputPort.maxConnections) ||
                    ExceedsPortLimit(outputCounts, LinkOutputKey(link), outputPort.maxConnections))
                {
                    links.RemoveAt(i);
                    ++removed;
                    continue;
                }

                IncrementPortCount(inputCounts, LinkInputKey(link));
                IncrementPortCount(outputCounts, LinkOutputKey(link));
                ++i;
            }

            return removed;
        }

        public bool TryCreateLink(
            string firstNodeId,
            string firstPortId,
            string secondNodeId,
            string secondPortId,
            out NowNodeLink link,
            bool replaceInput = true)
        {
            return TryCreateLink(
                firstNodeId,
                firstPortId,
                secondNodeId,
                secondPortId,
                out link,
                default,
                false,
                replaceInput);
        }

        public bool TryCreateLink(
            string firstNodeId,
            string firstPortId,
            string secondNodeId,
            string secondPortId,
            out NowNodeLink link,
            NowNodeLink ignoredLink,
            bool replaceInput = true)
        {
            return TryCreateLink(
                firstNodeId,
                firstPortId,
                secondNodeId,
                secondPortId,
                out link,
                ignoredLink,
                ignoredLink.isValid,
                replaceInput);
        }

        bool TryCreateLink(
            string firstNodeId,
            string firstPortId,
            string secondNodeId,
            string secondPortId,
            out NowNodeLink link,
            NowNodeLink ignoredLink,
            bool hasIgnoredLink,
            bool replaceInput)
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

            if (CanAddLink(link, replaceInput, ignoredLink, hasIgnoredLink, out _, out _))
                return true;

            link = default;
            return false;
        }

        public bool TryCreateLink(
            string firstNodeId,
            int firstPortId,
            string secondNodeId,
            int secondPortId,
            out NowNodeLink link,
            bool replaceInput = true)
        {
            return TryCreateLink(
                firstNodeId,
                NowNodeIds.FromInt(firstPortId),
                secondNodeId,
                NowNodeIds.FromInt(secondPortId),
                out link,
                replaceInput);
        }

        public bool TryCreateLink(
            int firstNodeId,
            int firstPortId,
            int secondNodeId,
            int secondPortId,
            out NowNodeLink link,
            bool replaceInput = true)
        {
            return TryCreateLink(
                NowNodeIds.FromInt(firstNodeId),
                NowNodeIds.FromInt(firstPortId),
                NowNodeIds.FromInt(secondNodeId),
                NowNodeIds.FromInt(secondPortId),
                out link,
                replaceInput);
        }

        public bool CanAddLink(NowNodeLink link, bool replaceInput = true)
        {
            return CanAddLink(link, replaceInput, default, false, out _, out _);
        }

        public bool CanAddLink(NowNodeLink link, NowNodeLink ignoredLink, bool replaceInput = true)
        {
            return CanAddLink(link, replaceInput, ignoredLink, ignoredLink.isValid, out _, out _);
        }

        public bool TryGetInputLink(string inputNodeId, string inputPortId, out NowNodeLink link)
        {
            for (int i = 0; i < links.Count; ++i)
            {
                if (links[i].inputNodeId == inputNodeId && links[i].inputPortId == inputPortId)
                {
                    link = links[i];
                    return true;
                }
            }

            link = default;
            return false;
        }

        public bool TryGetInputLink(string inputNodeId, int inputPortId, out NowNodeLink link)
        {
            return TryGetInputLink(inputNodeId, NowNodeIds.FromInt(inputPortId), out link);
        }

        bool UpsertNodePort(
            NowNode node,
            string id,
            string label,
            NowNodePortDirection direction,
            int typeId,
            int maxConnections)
        {
            if (node == null)
                return false;

            bool changed = direction == NowNodePortDirection.Input
                ? node.UpsertInput(id, label, typeId, maxConnections)
                : node.UpsertOutput(id, label, typeId, maxConnections);

            if (changed)
                PruneInvalidLinks();

            return changed;
        }

        bool RemoveNodePort(NowNode node, string id, NowNodePortDirection direction)
        {
            if (node == null)
                return false;

            bool removed = direction == NowNodePortDirection.Input
                ? node.RemoveInput(id)
                : node.RemoveOutput(id);

            if (removed)
                RemoveLinksForPort(node.id, id);

            return removed;
        }

        bool CanAddLink(
            NowNodeLink link,
            bool replaceInput,
            NowNodeLink ignoredLink,
            bool hasIgnoredLink,
            out NowNodePort outputPort,
            out NowNodePort inputPort)
        {
            outputPort = null;
            inputPort = null;

            if (!TryResolveLink(link, out _, out outputPort, out _, out inputPort))
                return false;

            if (HasLink(link, ignoredLink, hasIgnoredLink))
                return true;

            int inputCount = CountInputLinks(link.inputNodeId, link.inputPortId, ignoredLink, hasIgnoredLink);

            if (inputPort.maxConnections > 0 && inputCount >= inputPort.maxConnections && !replaceInput)
                return false;

            int outputCount = CountOutputLinks(link.outputNodeId, link.outputPortId, ignoredLink, hasIgnoredLink);

            if (outputPort.maxConnections > 0 && outputCount >= outputPort.maxConnections)
                return false;

            return true;
        }

        bool TryResolveLink(
            NowNodeLink link,
            out NowNode outputNode,
            out NowNodePort outputPort,
            out NowNode inputNode,
            out NowNodePort inputPort)
        {
            outputNode = null;
            outputPort = null;
            inputNode = null;
            inputPort = null;

            if (!link.isValid || link.outputNodeId == link.inputNodeId)
                return false;

            if (!TryFindPort(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, out outputNode, out outputPort) ||
                !TryFindPort(link.inputNodeId, link.inputPortId, NowNodePortDirection.Input, out inputNode, out inputPort))
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
            return CountInputLinks(nodeId, portId, default, false);
        }

        int CountInputLinks(string nodeId, string portId, NowNodeLink ignoredLink, bool hasIgnoredLink)
        {
            int count = 0;

            for (int i = 0; i < links.Count; ++i)
            {
                if (hasIgnoredLink && links[i] == ignoredLink)
                    continue;

                if (links[i].inputNodeId == nodeId && links[i].inputPortId == portId)
                    ++count;
            }

            return count;
        }

        int CountOutputLinks(string nodeId, string portId)
        {
            return CountOutputLinks(nodeId, portId, default, false);
        }

        int CountOutputLinks(string nodeId, string portId, NowNodeLink ignoredLink, bool hasIgnoredLink)
        {
            int count = 0;

            for (int i = 0; i < links.Count; ++i)
            {
                if (hasIgnoredLink && links[i] == ignoredLink)
                    continue;

                if (links[i].outputNodeId == nodeId && links[i].outputPortId == portId)
                    ++count;
            }

            return count;
        }

        bool HasLink(NowNodeLink link, NowNodeLink ignoredLink, bool hasIgnoredLink)
        {
            for (int i = 0; i < links.Count; ++i)
            {
                if (hasIgnoredLink && links[i] == ignoredLink)
                    continue;

                if (links[i] == link)
                    return true;
            }

            return false;
        }

        static string LinkInputKey(NowNodeLink link)
        {
            return (link.inputNodeId ?? string.Empty) + "\n" + (link.inputPortId ?? string.Empty);
        }

        static string LinkOutputKey(NowNodeLink link)
        {
            return (link.outputNodeId ?? string.Empty) + "\n" + (link.outputPortId ?? string.Empty);
        }

        static bool ExceedsPortLimit(Dictionary<string, int> counts, string key, int maxConnections)
        {
            return maxConnections > 0 &&
                counts != null &&
                counts.TryGetValue(key, out int count) &&
                count >= maxConnections;
        }

        static void IncrementPortCount(Dictionary<string, int> counts, string key)
        {
            if (counts == null)
                return;

            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
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

        internal static NowNode CloneNode(NowNode node)
        {
            if (node == null)
                return null;

            var clone = new NowNode
            {
                id = node.id,
                title = node.title,
                kindId = node.kindId,
                userId = node.userId,
                userData = node.userData,
                userData2 = node.userData2,
                userData3 = node.userData3,
                position = node.position,
                size = node.size,
                color = node.color,
                selected = node.selected,
                compact = node.compact,
                data = new List<NowNodeData>(node.data != null ? node.data.Count : 0),
                inputs = new List<NowNodePort>(node.inputs != null ? node.inputs.Count : 0),
                outputs = new List<NowNodePort>(node.outputs != null ? node.outputs.Count : 0)
            };

            CloneData(node.data, clone.data);
            ClonePorts(node.inputs, clone.inputs);
            ClonePorts(node.outputs, clone.outputs);
            return clone;
        }

        static void CloneData(List<NowNodeData> source, List<NowNodeData> target)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; ++i)
            {
                NowNodeData entry = source[i];
                target.Add(entry == null ? null : new NowNodeData(entry.key, entry.value));
            }
        }

        internal static void ClonePorts(List<NowNodePort> source, List<NowNodePort> target)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; ++i)
                target.Add(ClonePort(source[i]));
        }

        internal static NowNodePort ClonePort(NowNodePort port)
        {
            if (port == null)
                return null;

            return new NowNodePort(port.id, port.label, port.direction, port.typeId)
            {
                color = port.color,
                maxConnections = port.maxConnections
            };
        }
    }

    public sealed class NowNodeGraphClipboard
    {
        public static readonly NowNodeGraphClipboard shared = new NowNodeGraphClipboard();

        static readonly NowNodeGraphClipboard _duplicateScratch = new NowNodeGraphClipboard();

        readonly List<NowNode> _nodes = new List<NowNode>(8);
        readonly List<NowNodeLink> _links = new List<NowNodeLink>(8);
        Vector2 _center;

        public bool isEmpty => _nodes.Count == 0;

        public int nodeCount => _nodes.Count;

        public void Clear()
        {
            _nodes.Clear();
            _links.Clear();
            _center = default;
        }

        public int Copy(NowNodeGraph graph)
        {
            if (graph == null || graph.SelectedNodeCount() == 0)
                return 0;

            Clear();

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < graph.nodes.Count; ++i)
            {
                var node = graph.nodes[i];

                if (node == null || !graph.IsNodeSelected(node.id))
                    continue;

                _nodes.Add(NowNodeGraph.CloneNode(node));
                min = Vector2.Min(min, node.position);
                max = Vector2.Max(max, node.position + node.size);
            }

            if (_nodes.Count == 0)
                return 0;

            _center = (min + max) * 0.5f;

            for (int i = 0; i < graph.links.Count; ++i)
            {
                var link = graph.links[i];

                if (ContainsNode(link.outputNodeId) && ContainsNode(link.inputNodeId))
                    _links.Add(link);
            }

            return _nodes.Count;
        }

        public int Paste(NowNodeGraph graph, Vector2 position)
        {
            if (graph == null || _nodes.Count == 0)
                return 0;

            Vector2 offset = position - _center;
            var idMap = new Dictionary<string, string>(_nodes.Count);

            graph.ClearSelection();

            for (int i = 0; i < _nodes.Count; ++i)
            {
                var source = _nodes[i];
                var clone = NowNodeGraph.CloneNode(source);
                string newId = Guid.NewGuid().ToString("N");
                idMap[source.id] = newId;
                clone.id = newId;
                clone.position += offset;
                clone.selected = false;
                graph.nodes.Add(clone);
                graph.AddNodeToSelection(newId);
            }

            for (int i = 0; i < _links.Count; ++i)
            {
                var link = _links[i];
                graph.links.Add(new NowNodeLink(
                    idMap[link.outputNodeId],
                    link.outputPortId,
                    idMap[link.inputNodeId],
                    link.inputPortId));
            }

            return _nodes.Count;
        }

        public static int Duplicate(NowNodeGraph graph, Vector2 offset)
        {
            if (_duplicateScratch.Copy(graph) == 0)
                return 0;

            int pasted = _duplicateScratch.Paste(graph, _duplicateScratch._center + offset);
            _duplicateScratch.Clear();
            return pasted;
        }

        bool ContainsNode(string nodeId)
        {
            for (int i = 0; i < _nodes.Count; ++i)
            {
                if (_nodes[i] != null && _nodes[i].id == nodeId)
                    return true;
            }

            return false;
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
            public readonly NowNodeLink selectedLink;

            Snapshot(
                List<NowNode> nodes,
                List<NowNodeLink> links,
                Vector2 defaultNodeSize,
                string selectedNodeId,
                List<string> selectedNodeIds,
                NowNodeLink selectedLink)
            {
                this.nodes = nodes;
                this.links = links;
                this.defaultNodeSize = defaultNodeSize;
                this.selectedNodeId = selectedNodeId;
                this.selectedNodeIds = selectedNodeIds;
                this.selectedLink = selectedLink;
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

                return new Snapshot(nodes, links, graph.defaultNodeSize, graph.selectedNodeId, selectedNodeIds, graph.selectedLink);
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
                graph.selectedLink = selectedLink;

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
                    graph.selectedNodeId != selectedNodeId ||
                    graph.selectedLink != selectedLink)
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
                return NowNodeGraph.CloneNode(node);
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
                    a.selected != b.selected ||
                    a.compact != b.compact)
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
        public bool compactToggled;
        public bool nodesCopied;
        public bool nodesCut;
        public bool nodesPasted;
        public bool nodesDuplicated;
        public bool dragSelected;
        public bool focused;
        public bool hovered;
        public bool hasPointer;
        public Vector2 pointerPosition;
        public Vector2 pointerGraphPosition;
        public bool contextMenuOpened;
        public Vector2 contextMenuPosition;
        public Vector2 contextMenuGraphPosition;
        public bool searchOpened;
        public string selectedNodeId;
        public NowNodeLink createdLink;
        public NowNodeLink removedLink;
    }

    public enum NowNodeSnapMode
    {
        Grid,
        Align
    }

    public struct NowNodeGraphStyle
    {
        public bool drawBackground;
        public bool drawGrid;
        public bool nodeShadows;
        public bool snapNodes;
        public NowNodeSnapMode nodeSnapMode;
        public bool panWithPrimaryDrag;
        public bool dropToSearch;
        public Color background;
        public Color gridMinor;
        public Color gridMajor;
        public Color nodeSnapGuide;
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
        public float portHitRadius;
        public float portSnapRadius;
        public float nudgeStep;
        public float nudgeStepLarge;
        public float nodeSnapDistance;
        public float nodeSnapProximity;
        public float nodeSnapGuideWidth;
        public float minZoom;
        public float maxZoom;
        public float zoomStep;
        public int searchResultLimit;

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
                nodeShadows = true,
                snapNodes = true,
                nodeSnapMode = NowNodeSnapMode.Grid,
                panWithPrimaryDrag = false,
                dropToSearch = true,
                background = Darken(background, 0.9f),
                gridMinor = WithAlpha(text, 0.028f),
                gridMajor = WithAlpha(text, 0.06f),
                nodeSnapGuide = WithAlpha(accent, 0.78f),
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
                portHitRadius = 11f,
                portSnapRadius = 26f,
                nudgeStep = 1f,
                nudgeStepLarge = 24f,
                nodeSnapDistance = 12f,
                nodeSnapProximity = 64f,
                nodeSnapGuideWidth = 1.5f,
                minZoom = 0.35f,
                maxZoom = 2.25f,
                zoomStep = 1.12f,
                searchResultLimit = 8
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
        public readonly Color colorTo;
        public readonly float width;
        public readonly bool pending;
        public readonly bool hovered;
        public readonly bool selected;
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
            : this(viewport, link, from, to, color, color, width, pending, style)
        {
        }

        public NowNodeGraphLinkContext(
            NowRect viewport,
            NowNodeLink link,
            Vector2 from,
            Vector2 to,
            Color color,
            Color colorTo,
            float width,
            bool pending,
            NowNodeGraphStyle style,
            bool hovered = false,
            bool selected = false)
        {
            this.viewport = viewport;
            this.link = link;
            this.from = from;
            this.to = to;
            this.color = color;
            this.colorTo = colorTo;
            this.width = width;
            this.pending = pending;
            this.hovered = hovered;
            this.selected = selected;
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
        public readonly bool hasPreview;
        public readonly NowRect previewRect;
        public readonly bool hovered;
        public readonly bool compact;
        public readonly NowRect compactToggleRect;
        public readonly bool reroute;

        public NowNodeGraphNodeContext(
            NowNodeGraph graph,
            NowNode node,
            int nodeIndex,
            NowRect nodeRect,
            NowRect titleRect,
            NowRect bodyRect,
            bool selected,
            float zoom,
            NowNodeGraphStyle style,
            bool hasPreview = false,
            NowRect previewRect = default,
            bool hovered = false,
            bool compact = false,
            NowRect compactToggleRect = default,
            bool reroute = false)
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
            this.hasPreview = hasPreview;
            this.previewRect = previewRect;
            this.hovered = hovered;
            this.compact = compact;
            this.compactToggleRect = compactToggleRect;
            this.reroute = reroute;
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
        public readonly bool connected;

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
            NowNodeGraphStyle style,
            bool connected = false)
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
            this.connected = connected;
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
            float width = context.width;
            Color from = context.color;
            Color to = context.colorTo;

            if (context.selected)
            {
                DrawConnectionHalo(context.from, context.to, context.style.selectedBorder, width + 5f);
                width += 1.5f;
                from = Brighten(from, 0.2f);
                to = Brighten(to, 0.2f);
            }
            else if (context.hovered)
            {
                width += 1f;
                from = Brighten(from, 0.12f);
                to = Brighten(to, 0.12f);
            }

            DrawConnection(context.from, context.to, from, to, width);
        }

        public virtual void DrawNode(in NowNodeGraphNodeContext context)
        {
            var style = context.style;

            if (context.reroute)
            {
                DrawReroute(context);
                return;
            }

            Color nodeColor = context.selected ? style.nodeSelected : style.node;
            Color titleColor = ResolveNodeTitleColor(context);
            Color borderColor = context.selected
                ? style.selectedBorder
                : context.hovered
                    ? Color.Lerp(style.border, style.selectedBorder, 0.45f)
                    : style.border;

            float outline = context.selected ? 2f : 1f;

            if (style.nodeShadows)
            {
                var theme = NowTheme.themeAsset;
                var controlRenderer = theme != null ? theme.controlRenderer : null;

                controlRenderer?.DrawElevationShadow(
                    theme,
                    context.nodeRect,
                    new Vector4(style.nodeRadius, style.nodeRadius, style.nodeRadius, style.nodeRadius),
                    context.selected ? NowElevationToken.Overlay : NowElevationToken.Raised);
            }

            Now.Rectangle(context.nodeRect)
                .SetColor(nodeColor)
                .SetRadius(style.nodeRadius)
                .Draw();

            var titleFill = context.titleRect.Inset(outline, outline, outline, 0f);
            float titleRadius = Mathf.Max(0f, style.nodeRadius - outline);

            Now.Rectangle(titleFill)
                .SetColor(titleColor)
                .SetRadius(NowCornerRadius.Top(titleRadius))
                .Draw();

            Color separator = style.border;
            separator.a *= 0.6f;

            Now.Rectangle(new NowRect(
                    context.nodeRect.x + outline,
                    context.titleRect.yMax - 1f,
                    context.nodeRect.width - outline * 2f,
                    1f))
                .SetColor(separator)
                .Draw();

            if (context.hasPreview && !context.previewRect.isEmpty)
            {
                Now.Rectangle(new NowRect(
                        context.previewRect.x,
                        context.previewRect.y - style.contentPadding * 0.25f - 1f,
                        context.previewRect.width,
                        1f))
                    .SetColor(separator)
                    .Draw();
            }

            Now.Rectangle(context.nodeRect)
                .SetColor(Color.clear)
                .SetRadius(style.nodeRadius)
                .SetOutline(outline)
                .SetOutlineColor(borderColor)
                .Draw();

            Color titleText = TitleTextColor(titleColor, style);

            DrawText(
                context.titleRect.Inset(10f, 0f),
                context.node.title,
                13f,
                titleText,
                NowTextStyle.Button);

            Color titleGlyph = titleText;
            titleGlyph.a *= 0.8f;
            DrawCompactToggle(context.compactToggleRect, context.compact, titleGlyph, style);
        }

        /// <summary>
        /// Resolves a normal node's title fill. Override this to apply semantic colors
        /// without mutating the serialized <see cref="NowNode.color"/> value.
        /// </summary>
        protected virtual Color ResolveNodeTitleColor(in NowNodeGraphNodeContext context)
        {
            return context.node.color.a > 0f ? context.node.color : context.style.nodeTitle;
        }

        protected virtual void DrawReroute(in NowNodeGraphNodeContext context)
        {
            var style = context.style;
            float radius = context.nodeRect.height * 0.5f;
            Color fill = context.node.color.a > 0f
                ? context.node.color
                : context.selected ? style.nodeSelected : style.nodeTitle;
            Color border = context.selected
                ? style.selectedBorder
                : context.hovered
                    ? Color.Lerp(style.border, style.selectedBorder, 0.45f)
                    : style.border;

            Now.Rectangle(context.nodeRect)
                .SetColor(fill)
                .SetRadius(radius)
                .SetOutline(context.selected ? 2f : 1f)
                .SetOutlineColor(border)
                .Draw();
        }

        protected virtual void DrawCompactToggle(NowRect rect, bool compact, Color color, in NowNodeGraphStyle style)
        {
            if (rect.isEmpty)
                return;

            Vector2 center = rect.center;
            float half = rect.width * 0.28f;

            Now.Line(new Vector2(center.x - half, center.y), new Vector2(center.x + half, center.y))
                .SetColor(color)
                .SetWidth(1.5f)
                .SetCap(NowLineCap.Round)
                .Draw();

            if (compact)
            {
                Now.Line(new Vector2(center.x, center.y - half), new Vector2(center.x, center.y + half))
                    .SetColor(color)
                    .SetWidth(1.5f)
                    .SetCap(NowLineCap.Round)
                    .Draw();
            }
        }

        static Color TitleTextColor(Color titleFill, in NowNodeGraphStyle style)
        {
            float luminance = 0.299f * titleFill.r + 0.587f * titleFill.g + 0.114f * titleFill.b;
            float textLuminance = 0.299f * style.text.r + 0.587f * style.text.g + 0.114f * style.text.b;

            if (luminance < 0.45f)
                return textLuminance > 0.5f ? style.text : new Color(0.96f, 0.97f, 0.99f, 1f);

            return textLuminance <= 0.5f ? style.text : new Color(0.12f, 0.13f, 0.16f, 1f);
        }

        public virtual void DrawPort(in NowNodeGraphPortContext context)
        {
            var style = context.style;
            bool filled = context.connected || context.compatibleDropTarget;
            var portRect = new NowRect(
                context.center.x - context.radius,
                context.center.y - context.radius,
                context.radius * 2f,
                context.radius * 2f);

            if (filled)
            {
                Now.Rectangle(portRect)
                    .SetColor(context.color)
                    .SetRadius(context.radius)
                    .SetOutline(1f)
                    .SetOutlineColor(style.border)
                    .Draw();
            }
            else
            {
                Now.Rectangle(portRect)
                    .SetColor(style.node)
                    .SetRadius(context.radius)
                    .SetOutline(1.5f)
                    .SetOutlineColor(context.color)
                    .Draw();
            }

            string label = context.port.label ?? string.Empty;

            if (string.IsNullOrEmpty(label))
                return;

            float fontSize = 11.5f;
            var textStyle = NowTheme.themeAsset.Text(default, NowTextStyle.Muted)
                .SetFontSize(fontSize);

            if (textStyle.font == null)
                return;

            Vector2 size = textStyle.Measure(label);
            float labelLane = style.portLabelLaneWidth;
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

        static void DrawConnection(Vector2 from, Vector2 to, Color color, Color colorTo, float width)
        {
            float tangent = Mathf.Max(42f, Mathf.Abs(to.x - from.x) * 0.45f);
            Vector2 c1 = from + Vector2.right * tangent;
            Vector2 c2 = to + Vector2.left * tangent;

            var line = Now.Bezier(from, c1, c2, to)
                .SetWidth(Mathf.Max(1f, width))
                .SetCap(NowLineCap.Round);

            line = colorTo == color ? line.SetColor(color) : line.SetGradient(color, colorTo);
            line.Draw();
        }

        static void DrawConnectionHalo(Vector2 from, Vector2 to, Color color, float width)
        {
            color.a *= 0.3f;
            DrawConnection(from, to, color, color, width);
        }

        static Color Brighten(Color color, float amount)
        {
            float alpha = color.a;
            color = Color.Lerp(color, Color.white, amount);
            color.a = alpha;
            return color;
        }

        static void DrawText(NowRect rect, string value, float fontSize, Color color, NowTextStyle style)
        {
            if (string.IsNullOrEmpty(value))
                return;

            var text = NowTheme.themeAsset.Text(rect, style)
                .SetFontSize(fontSize)
                .SetColor(color);

            if (text.font == null)
                return;

            Vector2 size = text.Measure(value);
            var centered = new NowRect(rect.x, rect.y + (rect.height - size.y) * 0.5f, rect.width, size.y + 2f);
            text.SetPosition(centered).SetMask(rect).Draw(value);
        }
    }

    public sealed class NowNodeGraphContextMenu
    {
        public bool undoRedo;
        public bool deleteSelection = true;
        public bool createNodes = true;
        public bool clipboardItems = true;
        public string undoLabel = "Undo";
        public string redoLabel = "Redo";
        public string copyLabel = "Copy";
        public string cutLabel = "Cut";
        public string pasteLabel = "Paste";
        public string duplicateLabel = "Duplicate";
        public string deleteSelectionLabel = "Delete";
        public string createNodeLabel = "Create Node";
        public Func<NowNodeGraph, NowNodeGraphHistory, bool> drawCustomItems;
        public Func<NowNodeGraph, NowNodeGraphHistory, NowNodeGraphResult, bool> drawCustomItemsWithResult;

        public bool Draw(int id, NowNodeGraph graph, NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            return Draw(id, graph, history, result.contextMenuGraphPosition, NowNodeGraphClipboard.shared, ref result);
        }

        public bool Draw(
            int id,
            NowNodeGraph graph,
            NowNodeGraphHistory history,
            Vector2 graphPosition,
            ref NowNodeGraphResult result)
        {
            return Draw(id, graph, history, graphPosition, NowNodeGraphClipboard.shared, ref result);
        }

        public bool Draw(
            int id,
            NowNodeGraph graph,
            NowNodeGraphHistory history,
            Vector2 graphPosition,
            NowNodeGraphClipboard clipboard,
            ref NowNodeGraphResult result)
        {
            if (graph == null || !NowContextMenu.Begin(id))
                return false;

            bool changed = false;
            bool hasBuiltInItems = false;

            if (undoRedo && history != null)
            {
                if (NowContextMenu.Item(undoLabel, history.canUndo) && history.Undo(graph))
                {
                    result.changed = true;
                    result.undo = true;
                    result.selectionChanged = true;
                    changed = true;
                }

                if (NowContextMenu.Item(redoLabel, history.canRedo) && history.Redo(graph))
                {
                    result.changed = true;
                    result.redo = true;
                    result.selectionChanged = true;
                    changed = true;
                }

                hasBuiltInItems = true;
            }

            if (clipboardItems && clipboard != null)
            {
                bool hasSelection = graph.SelectedNodeCount() > 0;

                if (NowContextMenu.Item(copyLabel, hasSelection) && clipboard.Copy(graph) > 0)
                    result.nodesCopied = true;

                if (NowContextMenu.Item(cutLabel, hasSelection) && clipboard.Copy(graph) > 0)
                {
                    result.nodesCopied = true;
                    history?.Record(graph);

                    if (graph.RemoveSelectedNodes() > 0)
                    {
                        result.changed = true;
                        result.nodesCut = true;
                        result.nodesDeleted = true;
                        result.selectionChanged = true;
                        changed = true;
                    }
                }

                if (NowContextMenu.Item(pasteLabel, !clipboard.isEmpty) && !clipboard.isEmpty)
                {
                    history?.Record(graph);

                    if (clipboard.Paste(graph, graphPosition) > 0)
                    {
                        result.changed = true;
                        result.nodesPasted = true;
                        result.selectionChanged = true;
                        changed = true;
                    }
                }

                if (NowContextMenu.Item(duplicateLabel, hasSelection) && hasSelection)
                {
                    history?.Record(graph);

                    if (NowNodeGraphClipboard.Duplicate(graph, new Vector2(24f, 24f)) > 0)
                    {
                        result.changed = true;
                        result.nodesDuplicated = true;
                        result.selectionChanged = true;
                        changed = true;
                    }
                }

                hasBuiltInItems = true;
            }

            if (deleteSelection && NowContextMenu.Item(deleteSelectionLabel, graph.SelectedNodeCount() > 0))
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

            if (deleteSelection)
                hasBuiltInItems = true;

            if (createNodes && graph.schema != null && graph.schema.nodeDefinitionCount > 0)
            {
                if (hasBuiltInItems)
                    NowContextMenu.Separator();

                if (NowContextMenu.BeginSubmenu(createNodeLabel))
                {
                    var definitions = graph.schema.nodeDefinitions;

                    for (int i = 0; i < definitions.Count; ++i)
                    {
                        var definition = definitions[i];

                        if (definition == null)
                            continue;

                        if (NowContextMenu.Item(definition.title))
                        {
                            history?.Record(graph);
                            var node = graph.schema.CreateNode(graph, definition.kindId, graphPosition);
                            graph.SelectNode(node.id);

                            result.changed = true;
                            result.selectionChanged = true;
                            changed = true;
                        }
                    }

                    NowContextMenu.EndSubmenu();
                }

                hasBuiltInItems = true;
            }

            if ((drawCustomItems != null || drawCustomItemsWithResult != null) && hasBuiltInItems)
                NowContextMenu.Separator();

            if (drawCustomItems != null && drawCustomItems(graph, history))
            {
                result.changed = true;
                changed = true;
            }

            if (drawCustomItemsWithResult != null && drawCustomItemsWithResult(graph, history, result))
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
        public NowNodeGraphClipboard clipboard;

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

        public NowNodeGraphView SetClipboard(NowNodeGraphClipboard clipboard)
        {
            this.clipboard = clipboard;
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
            public byte linkPicked;
            public NowNodeLink pickedLink;
            public int hoveredLinkIndex;
            public byte searchOpen;
            public byte searchSuppressInput;
            public Vector2 searchScreenPosition;
            public Vector2 searchGraphPosition;
            public NodeSearchData searchData;
            public byte searchHasLinkSource;
            public string searchLinkSourceNodeId;
            public string searchLinkSourcePortId;
            public byte searchLinkSourceIsOutput;
            public int searchLinkSourceType;
            public byte nudging;
            public byte backgroundPanning;
            public byte selectionActive;
            public byte selectionAdditive;
            public byte selectionSubtractive;
            public Vector2 selectionStart;
            public Vector2 selectionEnd;
            public int nodeDragControlId;
            public byte nodeDragHistoryRecorded;
            public Vector2 nodeDragPointerGraphStart;
            public Vector2 nodeDragActiveNodeStart;
            public Vector2 pointerLocalPosition;
            public byte snapGuideFlags;
            public Vector2 snapGuideXFrom;
            public Vector2 snapGuideXTo;
            public Vector2 snapGuideYFrom;
            public Vector2 snapGuideYTo;
            public Vector2 contextMenuGraphPosition;
            public NowNodeContentContext contentContext;
            public DrawCache drawCache;
        }

        /// <summary>
        /// Per-canvas lookup structures rebuilt lazily within each Draw so hot
        /// loops avoid O(links x ports) and O(nodes x nodes) string scans. All
        /// entries are invalidated at the start of every Draw, again whenever the
        /// canvas mutates the graph mid-frame, and double-checked against the
        /// source collection counts, so mutations between frames never leak stale
        /// results into the next Draw.
        /// </summary>
        sealed class DrawCache
        {
            public readonly Dictionary<string, int> nodeIndexById = new Dictionary<string, int>(32);
            public readonly HashSet<(string nodeId, string portId)> connectedPorts = new HashSet<(string nodeId, string portId)>();
            public readonly HashSet<string> selectedIds = new HashSet<string>();
            public bool nodeIndexValid;
            public bool connectedPortsValid;
            public bool selectionValid;
            public int nodeIndexCount;
            public int connectedLinkCount;
            public int selectionCount;
            public string selectionPrimary;

            public void Invalidate()
            {
                nodeIndexValid = false;
                connectedPortsValid = false;
                selectionValid = false;
            }
        }

        sealed class NodeSearchData
        {
            public readonly List<int> recents = new List<int>(8);
            public readonly List<NowNodeDefinition> results = new List<NowNodeDefinition>(16);
            public readonly List<NodeSearchMatch> matches = new List<NodeSearchMatch>(32);
            public string query = string.Empty;
            public string lastQuery = string.Empty;
            public int fieldId;
            public int highlight;
            public NowRect popupRect;
            public NowNodeGraphStyle style;
            public Action drawAction;
            public bool hasLinkSource;
            public bool linkSourceIsOutput;
            public int linkSourceType;
            public string linkSourceNodeId;
            public string linkSourcePortId;
            public NowNode scratchSourceNode;
            public NowNode scratchTargetNode;
            public NowNodePort scratchSourcePort;
            public NowNodePort scratchTargetPort;
        }

        struct NodeSearchMatch
        {
            public NowNodeDefinition definition;
            public int score;
            public int order;
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
        const int NudgeXShortcutSeed = 0x4e554447;
        const int NudgeYShortcutSeed = 0x4e554448;
        const float CompactMinWidth = 148f;
        const float CompactMaxWidth = 200f;
        const int DefaultSearchResultLimit = 8;
        const int MaxSearchResultLimit = 64;
        const float SearchWidth = 260f;
        const float SearchFieldHeight = 30f;
        const float SearchRowHeight = 24f;
        const float SearchDetailRowHeight = 36f;
        const float SearchPadding = 8f;
        const byte SnapGuideXFlag = 1;
        const byte SnapGuideYFlag = 2;
        const float SnapGuidePadding = 20f;

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
        NowNodeGraphClipboard _clipboardBuffer;
        bool _hasStyle;
        Action<NowNode, NowRect> _legacyNodeContent;
        NowNodeContentRenderer _nodeContent;
        DrawCache _drawCache;

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
            _clipboardBuffer = null;
            _hasStyle = false;
            _legacyNodeContent = null;
            _nodeContent = null;
            _drawCache = null;
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
            _clipboardBuffer = null;
            _hasStyle = false;
            _legacyNodeContent = null;
            _nodeContent = null;
            _drawCache = null;
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

        public NowNodeGraphCanvas SetNodeSnapping(bool snap, float distance = 12f, float proximity = 64f)
        {
            EnsureStyle();
            _style.snapNodes = snap;
            _style.nodeSnapDistance = Mathf.Max(0f, distance);
            _style.nodeSnapProximity = Mathf.Max(0f, proximity);
            return this;
        }

        public NowNodeGraphCanvas SetNodeSnapMode(NowNodeSnapMode mode)
        {
            EnsureStyle();
            _style.nodeSnapMode = mode;
            return this;
        }

        /// <summary>
        /// Screen-space port hit radius and the (larger) radius within which a
        /// dropped link snaps to the nearest compatible port. Raise both for touch.
        /// </summary>
        public NowNodeGraphCanvas SetPortHitRadius(float hitRadius, float snapRadius = 0f)
        {
            EnsureStyle();
            _style.portHitRadius = Mathf.Max(4f, hitRadius);
            _style.portSnapRadius = Mathf.Max(_style.portHitRadius, snapRadius > 0f ? snapRadius : _style.portSnapRadius);
            return this;
        }

        /// <summary>Distance a selected node moves per arrow-key press; Shift uses the larger step.</summary>
        public NowNodeGraphCanvas SetNudge(float step, float largeStep)
        {
            EnsureStyle();
            _style.nudgeStep = Mathf.Max(0f, step);
            _style.nudgeStepLarge = Mathf.Max(0f, largeStep);
            return this;
        }

        /// <summary>
        /// When true, a primary-button drag on empty canvas pans instead of
        /// marquee-selecting (touch-friendly); marquee then needs Shift held.
        /// Right-button drag always pans regardless.
        /// </summary>
        public NowNodeGraphCanvas SetPanWithPrimaryDrag(bool pan)
        {
            EnsureStyle();
            _style.panWithPrimaryDrag = pan;
            return this;
        }

        /// <summary>Whether releasing a fresh link drag on empty canvas opens the connection-aware node search. Default on.</summary>
        public NowNodeGraphCanvas SetDropToSearch(bool enabled)
        {
            EnsureStyle();
            _style.dropToSearch = enabled;
            return this;
        }

        /// <summary>Maximum number of node-search results shown at once. Default 8.</summary>
        public NowNodeGraphCanvas SetSearchResultLimit(int maxResults)
        {
            EnsureStyle();
            _style.searchResultLimit = Mathf.Clamp(maxResults, 1, MaxSearchResultLimit);
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

        public NowNodeGraphCanvas SetClipboard(NowNodeGraphClipboard clipboard)
        {
            _clipboardBuffer = clipboard;
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
            var clipboard = _clipboardBuffer ?? _view?.clipboard ?? NowNodeGraphClipboard.shared;

            if (schema != null)
                _graph.schema = schema;

            int id = NowControls.GetControlId(_id, _site);
            ref var state = ref NowControlState.Get<CanvasState>(id);
            InitializeState(ref state, style);

            if (state.drawCache == null)
                state.drawCache = new DrawCache();

            _drawCache = state.drawCache;
            _drawCache.Invalidate();

            int focusId = NowInput.GetId(id, "focus");
            RegisterCanvasFocus(focusId);
            result.focused = NowFocus.IsFocused(focusId);
            result.hovered = NowInput.IsHovered(_rect);
            result.hasPointer = NowInput.current.hasPointer;
            result.pointerPosition = NowInput.current.pointerPosition;
            state.pointerLocalPosition = result.hasPointer
                ? Now.InverseTransformScreenPoint(result.pointerPosition)
                : _rect.center;
            result.pointerGraphPosition = ScreenToGraph(state.pointerLocalPosition, _rect, state);
            HandleKeyboardShortcuts(focusId, ref state, style, history, clipboard, ref result);
            HandleNodeSearch(focusId, ref state, style, schema, history, ref result);
            HandleCanvasNavigation(id, ref state, style, ref result);

            if (style.drawBackground)
                renderer.DrawBackground(new NowNodeGraphBackgroundContext(_rect, style));

            using (Now.Mask(_rect))
            {
                if (style.drawGrid)
                    renderer.DrawGrid(new NowNodeGraphGridContext(_rect, state.pan, state.zoom, style));

                HandleInteractions(id, ref state, style, schema, history, contextMenu, ref result);

                int hoveredNodeIndex = result.hovered && NowInput.current.hasPointer && state.linkActive == 0
                    ? FindNodeAt(state.pointerLocalPosition, state, style)
                    : -1;

                // Apply transform for graph content (links and nodes)
                using (Now.Transform(scale: state.zoom, origin: _rect.position + state.pan))
                {
                    DrawLinks(state, style, renderer);

                    if (state.linkActive != 0)
                        DrawPendingLink(state, style, renderer);

                    DrawNodeSnapGuide(state, style);
                    DrawNodes(id, ref state, style, schema, history, renderer, hoveredNodeIndex, ref result);
                }

                if (state.selectionActive != 0)
                    renderer.DrawSelection(new NowNodeGraphSelectionContext(_rect, SelectionScreenRect(state), style));
            }

            contextMenu?.Draw(NowInput.GetId(id, "context-menu"), _graph, history, state.contextMenuGraphPosition, clipboard, ref result);
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

        void HandleKeyboardShortcuts(
            int focusId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            NowNodeGraphClipboard clipboard,
            ref NowNodeGraphResult result)
        {
            if (NowInput.isPassive || !NowFocus.IsFocused(focusId))
                return;

            var frame = NowTextInput.current;

            if (state.searchOpen != 0)
                return;

            if (frame.escapePressed && (state.linkActive != 0 || state.selectionActive != 0))
            {
                ClearLinkDrag(ref state);
                state.selectionActive = 0;
                NowControlState.RequestRepaint();
                return;
            }

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

            if (NowContextMenu.isOpen || NowOverlay.hasOpenOverlay)
                return;

            if (frame.selectAllPressed)
            {
                int previousCount = _graph.SelectedNodeCount();
                bool hadLink = _graph.selectedLink.isValid;
                int count = _graph.SelectAllNodes();

                if (count != previousCount || hadLink)
                {
                    result.changed = true;
                    result.selectionChanged = true;
                    NowControlState.RequestRepaint();
                }

                return;
            }

            if (_graph.schema != null && !frame.command && !frame.option && ContainsChar(frame.characters, ' '))
            {
                state.searchHasLinkSource = 0;
                OpenNodeSearch(focusId, ref state, ref result, suppressCurrentInput: true);
                return;
            }

            if (!frame.command && !frame.option &&
                (ContainsChar(frame.characters, 'f') || ContainsChar(frame.characters, 'F')))
            {
                FrameView(ref state, style, ref result);
                return;
            }

            if (clipboard != null && frame.copyPressed)
            {
                if (clipboard.Copy(_graph) > 0)
                    result.nodesCopied = true;

                return;
            }

            if (clipboard != null && frame.cutPressed)
            {
                if (clipboard.Copy(_graph) > 0)
                {
                    result.nodesCopied = true;
                    result.nodesCut = true;
                    DeleteSelectedNodes(history, ref result);
                }

                return;
            }

            if (clipboard != null && frame.pastePressed)
            {
                PasteFromClipboard(clipboard, history, PastePosition(state), ref result);
                return;
            }

            if (frame.duplicatePressed)
            {
                DuplicateSelection(history, ref result);
                return;
            }

            if (NowControlState.Repeat(NowInput.CombineId(focusId, DeleteShortcutSeed), frame.deleteHeld || frame.backspaceHeld))
            {
                if (_graph.SelectedNodeCount() > 0)
                    DeleteSelectedNodes(history, ref result);
                else
                    DeleteSelectedLink(history, ref result);

                return;
            }

            HandleArrowNudge(focusId, ref state, style, history, ref result);
        }

        void HandleArrowNudge(
            int focusId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            var frame = NowTextInput.current;

            if (state.selectionActive != 0 || state.linkActive != 0 || _graph.SelectedNodeCount() == 0)
            {
                state.nudging = 0;
                return;
            }

            bool anyHeld = frame.leftHeld || frame.rightHeld || frame.upHeld || frame.downHeld;

            if (!anyHeld)
            {
                state.nudging = 0;
                return;
            }

            float dx = (frame.rightHeld ? 1f : 0f) - (frame.leftHeld ? 1f : 0f);
            float dy = (frame.downHeld ? 1f : 0f) - (frame.upHeld ? 1f : 0f);

            bool xPulse = dx != 0f && NowControlState.Repeat(NowInput.CombineId(focusId, NudgeXShortcutSeed), frame.leftHeld || frame.rightHeld);
            bool yPulse = dy != 0f && NowControlState.Repeat(NowInput.CombineId(focusId, NudgeYShortcutSeed), frame.upHeld || frame.downHeld);

            if (!xPulse && !yPulse)
                return;

            // Coalesce a whole hold-burst into one undo entry.
            if (state.nudging == 0)
            {
                history?.Record(_graph);
                state.nudging = 1;
            }

            float step = frame.shift ? style.nudgeStepLarge : style.nudgeStep;
            Vector2 offset = new Vector2(xPulse ? dx : 0f, yPulse ? dy : 0f) * step;

            if (offset == Vector2.zero)
                return;

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node != null && _graph.IsNodeSelected(node.id))
                    node.position += offset;
            }

            result.changed = true;
            result.nodeMoved = true;
            NowControlState.RequestRepaint();
        }

        static bool ContainsChar(string characters, char value)
        {
            if (string.IsNullOrEmpty(characters))
                return false;

            for (int i = 0; i < characters.Length; ++i)
            {
                if (characters[i] == value)
                    return true;
            }

            return false;
        }

        void FrameView(ref CanvasState state, NowNodeGraphStyle style, ref NowNodeGraphResult result)
        {
            bool selectionOnly = _graph.SelectedNodeCount() > 0;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node == null || (selectionOnly && !_graph.IsNodeSelected(node.id)))
                    continue;

                Vector2 size = ResolveNodeSize(node, style);
                min = Vector2.Min(min, node.position);
                max = Vector2.Max(max, node.position + size);
                any = true;
            }

            if (!any)
                return;

            const float Margin = 48f;
            min -= new Vector2(Margin, Margin);
            max += new Vector2(Margin, Margin);
            Vector2 boundsSize = Vector2.Max(max - min, new Vector2(1f, 1f));

            float previousZoom = state.zoom;
            Vector2 previousPan = state.pan;
            float zoom = Mathf.Min(_rect.width / boundsSize.x, _rect.height / boundsSize.y);
            state.zoom = Mathf.Clamp(zoom, style.minZoom, 1f);
            state.pan = _rect.center - _rect.position - (min + max) * 0.5f * state.zoom;

            if (!Mathf.Approximately(previousZoom, state.zoom) || previousPan != state.pan)
            {
                result.changed = true;
                result.panned = true;
                result.zoomed = !Mathf.Approximately(previousZoom, state.zoom);
                NowControlState.RequestRepaint();
            }
        }

        void OpenNodeSearch(
            int focusId,
            ref CanvasState state,
            ref NowNodeGraphResult result,
            bool suppressCurrentInput = false)
        {
            var snapshot = NowInput.current;
            Vector2 screen = snapshot.hasPointer && NowInput.IsHovered(_rect)
                ? state.pointerLocalPosition
                : _rect.center;

            state.searchOpen = 1;
            state.searchSuppressInput = (byte)(suppressCurrentInput ? 1 : 0);
            state.searchScreenPosition = screen;
            state.searchGraphPosition = ScreenToGraph(screen, _rect, state);

            var data = state.searchData;

            if (data == null)
            {
                data = new NodeSearchData();
                state.searchData = data;
            }

            data.query = string.Empty;
            data.lastQuery = string.Empty;
            data.highlight = 0;
            data.fieldId = NowInput.CombineId(focusId, 0x53464c44);
            data.hasLinkSource = state.searchHasLinkSource != 0;
            data.linkSourceIsOutput = state.searchLinkSourceIsOutput != 0;
            data.linkSourceType = state.searchLinkSourceType;
            data.linkSourceNodeId = state.searchLinkSourceNodeId;
            data.linkSourcePortId = state.searchLinkSourcePortId;
            NowFocus.Focus(data.fieldId);
            result.searchOpened = true;
            NowControlState.RequestRepaint();
        }

        void HandleNodeSearch(
            int focusId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            if (state.searchOpen == 0)
                return;

            var data = state.searchData;

            if (data == null || schema == null)
            {
                state.searchOpen = 0;
                state.searchHasLinkSource = 0;
                return;
            }

            if (NowInput.isPassive)
                return;

            var snapshot = NowInput.current;
            var frame = NowTextInput.current;
            bool suppressInput = state.searchSuppressInput != 0;
            state.searchSuppressInput = 0;

            if (SearchCancelPressed(suppressInput, snapshot, frame))
            {
                CloseNodeSearch(focusId, ref state);
                return;
            }

            if (data.query.Length > 0 && data.query[0] == ' ')
                data.query = data.query.TrimStart(' ');

            if (!string.Equals(data.query, data.lastQuery, StringComparison.Ordinal))
            {
                data.lastQuery = data.query;
                data.highlight = 0;
                NowControlState.RequestRepaint();
            }

            BuildSearchResults(schema, data, style);

            int count = data.results.Count;
            float rowHeight = SearchRowHeightForResults(data.results);

            if (count > 0)
            {
                data.highlight = Mathf.Clamp(data.highlight, 0, count - 1);
                float navY = snapshot.navigation.y;

                if (NowControlState.Repeat(NowInput.CombineId(focusId, 0x53420002), frame.downHeld || navY < -0.55f))
                {
                    data.highlight = (data.highlight + 1) % count;
                    NowControlState.RequestRepaint();
                }

                if (NowControlState.Repeat(NowInput.CombineId(focusId, 0x53420003), frame.upHeld || navY > 0.55f))
                {
                    data.highlight = (data.highlight - 1 + count) % count;
                    NowControlState.RequestRepaint();
                }
            }

            var popup = SearchPopupRect(state, count, rowHeight);
            data.popupRect = popup;
            data.style = style;

            if (snapshot.hasPointer)
            {
                int row = SearchRowAt(popup, state.pointerLocalPosition, count, rowHeight);

                if (row >= 0 && row != data.highlight && snapshot.pointerDelta != Vector2.zero)
                {
                    data.highlight = row;
                    NowControlState.RequestRepaint();
                }

                if (!suppressInput && NowInput.WasPointerPressed(NowPointerButton.Primary))
                {
                    if (row >= 0 && row < count)
                    {
                        CreateSearchNode(data.results[row], focusId, ref state, schema, history, ref result);
                        return;
                    }

                    if (!popup.Contains(state.pointerLocalPosition))
                    {
                        CloseNodeSearch(focusId, ref state);
                        return;
                    }
                }
            }

            if (SearchConfirmPressed(suppressInput, snapshot, frame) && count > 0)
            {
                CreateSearchNode(data.results[data.highlight], focusId, ref state, schema, history, ref result);
                return;
            }

            if (data.drawAction == null)
            {
                var captured = data;
                data.drawAction = () => DrawNodeSearchPopup(captured);
            }

            NowOverlay.Defer(popup, data.drawAction);
        }

        static bool SearchCancelPressed(bool suppressInput, NowInputSnapshot snapshot, NowTextInputFrame frame)
        {
            return !suppressInput &&
                (frame.escapePressed || (snapshot.cancelPressed && !NowInput.cancelConsumed));
        }

        static bool SearchConfirmPressed(bool suppressInput, NowInputSnapshot snapshot, NowTextInputFrame frame)
        {
            return !suppressInput &&
                (frame.enterPressed || (snapshot.submitPressed && !ContainsChar(frame.characters, ' ')));
        }

        void CloseNodeSearch(int focusId, ref CanvasState state)
        {
            state.searchOpen = 0;
            state.searchHasLinkSource = 0;
            state.searchSuppressInput = 0;
            NowFocus.Focus(focusId);
            NowControlState.RequestRepaint();
        }

        void BuildSearchResults(NowNodeGraphSchema schema, NodeSearchData data, NowNodeGraphStyle style)
        {
            data.results.Clear();
            data.matches.Clear();
            var definitions = schema.nodeDefinitions;
            string query = data.query;
            int limit = SearchResultLimit(style);

            if (string.IsNullOrEmpty(query))
            {
                for (int i = 0; i < data.recents.Count && data.results.Count < limit; ++i)
                {
                    if (schema.TryGetNode(data.recents[i], out var recent) && DefinitionAcceptsSource(schema, data, recent))
                        data.results.Add(recent);
                }
            }

            if (string.IsNullOrEmpty(query))
            {
                for (int i = 0; i < definitions.Count && data.results.Count < limit; ++i)
                {
                    var definition = definitions[i];

                    if (definition == null || data.results.Contains(definition))
                        continue;

                    if (!DefinitionAcceptsSource(schema, data, definition))
                        continue;

                    data.results.Add(definition);
                }

                return;
            }

            for (int i = 0; i < definitions.Count; ++i)
            {
                var definition = definitions[i];

                if (definition == null)
                    continue;

                int score = DefinitionSearchScore(definition, query);
                if (score <= 0 || !DefinitionAcceptsSource(schema, data, definition))
                    continue;

                data.matches.Add(new NodeSearchMatch
                {
                    definition = definition,
                    score = score,
                    order = i
                });
            }

            data.matches.Sort(CompareSearchMatches);

            for (int i = 0; i < data.matches.Count && data.results.Count < limit; ++i)
                data.results.Add(data.matches[i].definition);
        }

        static int CompareSearchMatches(NodeSearchMatch a, NodeSearchMatch b)
        {
            int result = b.score.CompareTo(a.score);
            return result != 0 ? result : a.order.CompareTo(b.order);
        }

        static int DefinitionSearchScore(NowNodeDefinition definition, string query)
        {
            int title = SearchTextScore(definition?.title, query);
            int keywords = SearchTextScore(definition?.searchKeywords, query);
            int detail = SearchTextScore(definition?.searchDetail, query);
            int category = SearchTextScore(definition?.category, query);
            int score = 0;

            if (title > 0)
                score = Mathf.Max(score, 300 + title);

            if (keywords > 0)
                score = Mathf.Max(score, 200 + keywords);

            if (detail > 0)
                score = Mathf.Max(score, 180 + detail);

            if (category > 0)
                score = Mathf.Max(score, 100 + category);

            return score;
        }

        static int SearchTextScore(string value, string query)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(query))
                return 0;

            if (string.Equals(value, query, StringComparison.OrdinalIgnoreCase))
                return 100;

            if (value.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 90;

            if (ContainsTokenPrefix(value, query))
                return 80;

            return ContainsSearchText(value, query) ? 70 : 0;
        }

        static bool ContainsTokenPrefix(string value, string query)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(query) || query.Length > value.Length)
                return false;

            for (int i = 0; i <= value.Length - query.Length; ++i)
            {
                if (i > 0 && char.IsLetterOrDigit(value[i - 1]))
                    continue;

                if (string.Compare(value, i, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            }

            return false;
        }

        static int SearchResultLimit(NowNodeGraphStyle style)
        {
            return style.searchResultLimit > 0
                ? Mathf.Clamp(style.searchResultLimit, 1, MaxSearchResultLimit)
                : DefaultSearchResultLimit;
        }

        static bool ContainsSearchText(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // When the search was opened by dragging a link into empty space, only offer
        // node kinds that have a port the dragged port could actually wire to.
        bool DefinitionAcceptsSource(NowNodeGraphSchema schema, NodeSearchData data, NowNodeDefinition definition)
        {
            if (!data.hasLinkSource)
                return true;

            if (data.scratchSourceNode == null)
            {
                data.scratchSourceNode = new NowNode("__now_src", string.Empty, default);
                data.scratchTargetNode = new NowNode("__now_dst", string.Empty, default);
                data.scratchSourcePort = new NowNodePort("s", string.Empty, NowNodePortDirection.Output);
                data.scratchTargetPort = new NowNodePort("t", string.Empty, NowNodePortDirection.Input);
            }

            data.scratchSourcePort.typeId = data.linkSourceType;
            data.scratchSourcePort.direction = data.linkSourceIsOutput ? NowNodePortDirection.Output : NowNodePortDirection.Input;

            var candidatePorts = data.linkSourceIsOutput ? definition.inputs : definition.outputs;
            var candidateDir = data.linkSourceIsOutput ? NowNodePortDirection.Input : NowNodePortDirection.Output;

            for (int i = 0; i < candidatePorts.Count; ++i)
            {
                var portDef = candidatePorts[i];

                if (portDef == null)
                    continue;

                data.scratchTargetPort.typeId = portDef.typeId;
                data.scratchTargetPort.direction = candidateDir;

                bool ok = data.linkSourceIsOutput
                    ? schema.CanConnect(_graph, data.scratchSourceNode, data.scratchSourcePort, data.scratchTargetNode, data.scratchTargetPort)
                    : schema.CanConnect(_graph, data.scratchTargetNode, data.scratchTargetPort, data.scratchSourceNode, data.scratchSourcePort);

                if (ok)
                    return true;
            }

            return false;
        }

        static float SearchRowHeightForResults(IReadOnlyList<NowNodeDefinition> results)
        {
            if (results == null)
                return SearchRowHeight;

            for (int i = 0; i < results.Count; ++i)
            {
                if (!string.IsNullOrEmpty(results[i]?.searchDetail))
                    return SearchDetailRowHeight;
            }

            return SearchRowHeight;
        }

        NowRect SearchPopupRect(CanvasState state, int resultCount, float rowHeight)
        {
            float rows = Mathf.Max(1, resultCount);
            float height = SearchPadding * 2f + SearchFieldHeight + 4f + rows * rowHeight;
            float x = Mathf.Clamp(state.searchScreenPosition.x, _rect.x, Mathf.Max(_rect.x, _rect.xMax - SearchWidth));
            float y = Mathf.Clamp(state.searchScreenPosition.y, _rect.y, Mathf.Max(_rect.y, _rect.yMax - height));
            return new NowRect(x, y, SearchWidth, height);
        }

        static int SearchRowAt(NowRect popup, Vector2 point, int count, float rowHeight)
        {
            float top = popup.y + SearchPadding + SearchFieldHeight + 4f;

            if (point.x < popup.x + 6f || point.x > popup.xMax - 6f || point.y < top)
                return -1;

            int row = (int)((point.y - top) / rowHeight);
            return row >= 0 && row < count ? row : -1;
        }

        void CreateSearchNode(
            NowNodeDefinition definition,
            int focusId,
            ref CanvasState state,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            history?.Record(_graph);
            var node = schema.CreateNode(_graph, definition.kindId, state.searchGraphPosition);

            var data = state.searchData;

            if (data.hasLinkSource)
                AutoWireSearchNode(data, node, ref result);

            _graph.SelectNode(node.id);
            data.recents.Remove(definition.kindId);
            data.recents.Insert(0, definition.kindId);

            while (data.recents.Count > 6)
                data.recents.RemoveAt(data.recents.Count - 1);

            state.searchOpen = 0;
            state.searchHasLinkSource = 0;
            NowFocus.Focus(focusId);
            result.changed = true;
            result.selectionChanged = true;
            NowControlState.RequestRepaint();
        }

        void AutoWireSearchNode(NodeSearchData data, NowNode node, ref NowNodeGraphResult result)
        {
            if (!_graph.TryFindPort(
                    data.linkSourceNodeId,
                    data.linkSourcePortId,
                    data.linkSourceIsOutput ? NowNodePortDirection.Output : NowNodePortDirection.Input,
                    out _, out _))
            {
                return;
            }

            var candidatePorts = data.linkSourceIsOutput ? node.inputs : node.outputs;

            for (int i = 0; i < candidatePorts.Count; ++i)
            {
                var port = candidatePorts[i];

                if (port != null &&
                    _graph.TryCreateLink(data.linkSourceNodeId, data.linkSourcePortId, node.id, port.id, out var link) &&
                    _graph.TryAddLink(link))
                {
                    result.linkCreated = true;
                    result.createdLink = link;
                    return;
                }
            }
        }

        static void DrawNodeSearchPopup(NodeSearchData data)
        {
            var style = data.style;
            var popup = data.popupRect;
            var theme = NowTheme.themeAsset;
            var controlRenderer = theme != null ? theme.controlRenderer : null;

            controlRenderer?.DrawElevationShadow(theme, popup, new Vector4(8f, 8f, 8f, 8f), NowElevationToken.Overlay);

            Now.Rectangle(popup)
                .SetColor(style.node)
                .SetRadius(8f)
                .SetOutline(1f)
                .SetOutlineColor(style.border)
                .Draw();

            var field = new NowRect(
                popup.x + SearchPadding,
                popup.y + SearchPadding,
                popup.width - SearchPadding * 2f,
                SearchFieldHeight);

            string query = data.query ?? string.Empty;

            if (Now.TextField(field, new NowId(data.fieldId))
                    .SetPlaceholder("Search nodes...")
                    .Draw(ref query))
            {
                data.query = query;
                NowControlState.RequestRepaint();
            }

            float rowTop = field.yMax + 4f;
            float rowHeight = SearchRowHeightForResults(data.results);

            if (data.results.Count == 0)
            {
                var emptyRow = new NowRect(popup.x + 6f, rowTop, popup.width - 12f, rowHeight);
                DrawSearchLabel(emptyRow.Inset(10f, 0f), emptyRow, "No matching nodes", style.textMuted, 12.5f);
                return;
            }

            for (int i = 0; i < data.results.Count; ++i)
            {
                var row = new NowRect(popup.x + 6f, rowTop + i * rowHeight, popup.width - 12f, rowHeight);

                if (i == data.highlight)
                {
                    Color fill = style.selectedBorder;
                    fill.a *= 0.16f;

                    Now.Rectangle(row)
                        .SetColor(fill)
                        .SetRadius(4f)
                        .Draw();
                }

                DrawSearchResult(row.Inset(10f, 0f), row, data.results[i], style);
            }
        }

        static void DrawSearchResult(NowRect rect, NowRect mask, NowNodeDefinition definition, NowNodeGraphStyle style)
        {
            if (definition == null)
                return;

            bool hasDetail = !string.IsNullOrEmpty(definition.searchDetail);
            NowRect titleRect = hasDetail
                ? new NowRect(rect.x, rect.y + 2f, rect.width, 17f)
                : rect;

            if (!string.IsNullOrEmpty(definition.category))
            {
                var categoryStyle = NowTheme.themeAsset.Text(rect, NowTextStyle.Caption)
                    .SetFontSize(10.5f)
                    .SetColor(style.textMuted);
                Vector2 categorySize = categoryStyle.Measure(definition.category);
                float categoryWidth = Mathf.Min(92f, categorySize.x + 1f);
                var categoryRect = new NowRect(rect.xMax - categoryWidth, titleRect.y, categoryWidth, titleRect.height);

                DrawSearchLabel(categoryRect, mask, definition.category, style.textMuted, 10.5f);
                titleRect = new NowRect(rect.x, titleRect.y, Mathf.Max(0f, categoryRect.x - rect.x - 8f), titleRect.height);
            }

            DrawSearchLabel(titleRect, mask, definition.title, style.text, 12.5f);

            if (hasDetail)
            {
                var detailRect = new NowRect(rect.x, rect.y + 18f, rect.width, 15f);
                DrawSearchLabel(detailRect, mask, definition.searchDetail, style.textMuted, 10.5f);
            }
        }

        static void DrawSearchLabel(NowRect rect, NowRect mask, string value, Color color, float fontSize)
        {
            if (string.IsNullOrEmpty(value))
                return;

            var text = NowTheme.themeAsset.Text(rect, NowTextStyle.Body)
                .SetFontSize(fontSize)
                .SetColor(color);

            if (text.font == null)
                return;

            Vector2 size = text.Measure(value);
            var centered = new NowRect(rect.x, rect.y + (rect.height - size.y) * 0.5f, rect.width, size.y + 2f);
            text.SetPosition(centered).SetMask(mask).Draw(value);
        }

        void DeleteSelectedLink(NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            if (!_graph.HasSelectedLink())
                return;

            var link = _graph.selectedLink;
            history?.Record(_graph);

            if (_graph.RemoveLink(link))
            {
                _graph.ClearLinkSelection();
                result.changed = true;
                result.linkRemoved = true;
                result.removedLink = link;
                NowControlState.RequestRepaint();
            }
        }

        Vector2 PastePosition(CanvasState state)
        {
            var snapshot = NowInput.current;

            if (snapshot.hasPointer && NowInput.IsHovered(_rect))
                return ScreenToGraph(state.pointerLocalPosition, _rect, state);

            return ScreenToGraph(_rect.center, _rect, state);
        }

        void PasteFromClipboard(
            NowNodeGraphClipboard clipboard,
            NowNodeGraphHistory history,
            Vector2 position,
            ref NowNodeGraphResult result)
        {
            if (clipboard == null || clipboard.isEmpty)
                return;

            history?.Record(_graph);

            if (clipboard.Paste(_graph, position) > 0)
            {
                result.changed = true;
                result.nodesPasted = true;
                result.selectionChanged = true;
                NowControlState.RequestRepaint();
            }
        }

        void DuplicateSelection(NowNodeGraphHistory history, ref NowNodeGraphResult result)
        {
            if (_graph.SelectedNodeCount() == 0)
                return;

            history?.Record(_graph);

            if (NowNodeGraphClipboard.Duplicate(_graph, new Vector2(24f, 24f)) > 0)
            {
                result.changed = true;
                result.nodesDuplicated = true;
                result.selectionChanged = true;
                NowControlState.RequestRepaint();
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
                Vector2 pointer = state.pointerLocalPosition;
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
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            NowNodeGraphContextMenu contextMenu,
            ref NowNodeGraphResult result)
        {
            int focusId = NowInput.GetId(id, "focus");
            int pointerNodeIndex = -1;
            bool pointerOverPort = false;
            bool pointerOverNodeOrPort = false;

            if (NowInput.current.hasPointer)
            {
                pointerOverPort = FindPortAt(state.pointerLocalPosition, state, style, out _);
                pointerNodeIndex = FindNodeAt(state.pointerLocalPosition, state, style);
                pointerOverNodeOrPort = pointerOverPort || pointerNodeIndex >= 0;
            }

            int hoveredLink = -1;
            bool nodePointerActive = false;

            if (NowInput.current.hasPointer &&
                !pointerOverNodeOrPort &&
                state.linkActive == 0 &&
                state.selectionActive == 0 &&
                NowInput.IsHovered(_rect))
            {
                hoveredLink = FindLinkAt(state.pointerLocalPosition, state, style);
            }

            if (state.hoveredLinkIndex != hoveredLink)
            {
                state.hoveredLinkIndex = hoveredLink;
                NowControlState.RequestRepaint();
            }

            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node == null)
                    continue;

                InteractPorts(id, n, node, NowNodePortDirection.Input, ref state, style, history, ref result);
                InteractPorts(id, n, node, NowNodePortDirection.Output, ref state, style, history, ref result);
                InteractCompactToggle(id, n, node, ref state, style, history, ref result);

                int nodeControlId = NodeControlId(id, n, node);
                var nodeDragRect = NodeDragScreenRect(nodeControlId, node, _rect, state, style);
                var nodeInteraction = NowInput.Interact(nodeControlId, nodeDragRect);

                if (nodeInteraction.active)
                    nodePointerActive = true;

                if (nodeInteraction.pressed)
                {
                    SelectNodeFromPointer(node.id, ref result);
                    BeginNodeDrag(nodeControlId, node, ref state);
                }

                if (nodeInteraction.dragging)
                {
                    bool allowSnapping = !NowTextInput.current.option;
                    Vector2 graphDragDelta = nodeInteraction.dragDelta / Mathf.Max(state.zoom, 0.001f);

                    if (state.nodeDragControlId != nodeControlId)
                        BeginNodeDrag(nodeControlId, node, ref state);

                    if (!allowSnapping || !style.snapNodes || style.nodeSnapMode != NowNodeSnapMode.Align || style.nodeSnapDistance <= 0f)
                        ClearNodeSnapGuide(ref state);

                    bool moved = MoveSelectedNodes(
                        node,
                        NodeDragDeltaFromStart(node, graphDragDelta, state),
                        ref state,
                        style,
                        history,
                        allowSnapping);

                    if (moved)
                    {
                        result.changed = true;
                        result.nodeMoved = true;
                    }

                    NowControlState.RequestRepaint();
                }
            }

            if (!nodePointerActive)
            {
                state.nodeDragControlId = 0;
                state.nodeDragHistoryRecorded = 0;
                ClearNodeSnapGuide(ref state);
            }

            if (state.linkActive != 0 && NowInput.WasPointerReleased(NowPointerButton.Primary))
                CommitPendingLink(focusId, ref state, style, schema, history, ref result);

            int backgroundId = NowInput.GetId(id, "background");
            bool backgroundOwnsPointer = NowInput.activeId == backgroundId && NowInput.activeButton == NowPointerButton.Primary;
            var backgroundRect = !pointerOverNodeOrPort || backgroundOwnsPointer || state.selectionActive != 0
                ? _rect
                : default;
            var background = NowInput.Interact(backgroundId, backgroundRect);

            if (background.pressed && !pointerOverNodeOrPort && state.linkActive == 0 && hoveredLink < 0)
            {
                var frame = NowTextInput.current;

                // Touch-friendly mode: a plain empty-canvas drag pans; marquee needs Shift.
                if (style.panWithPrimaryDrag && !frame.shift)
                {
                    state.backgroundPanning = 1;
                }
                else
                {
                    state.selectionActive = 1;
                    state.selectionStart = background.pointerPosition;
                    state.selectionEnd = background.pointerPosition;
                    state.selectionAdditive = (byte)(frame.shift ? 1 : 0);
                    state.selectionSubtractive = (byte)((!style.panWithPrimaryDrag && frame.command) ? 1 : 0);
                    NowControlState.RequestRepaint();
                }
            }

            if (state.backgroundPanning != 0)
            {
                if (background.active && background.dragDelta != Vector2.zero)
                {
                    state.pan += background.dragDelta;
                    result.changed = true;
                    result.panned = true;
                    NowControlState.RequestRepaint();
                }

                if (background.released || !background.active)
                    state.backgroundPanning = 0;
            }
            else if (state.selectionActive != 0 && background.active)
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

            if (background.clicked && state.backgroundPanning == 0 && !pointerOverNodeOrPort && state.linkActive == 0)
            {
                if (hoveredLink >= 0 && hoveredLink < _graph.links.Count)
                    SelectLink(_graph.links[hoveredLink], ref result);
                else
                    SelectNode(null, ref result);
            }

            // Right-button: drag pans (Unreal/Unity convention), click opens the menu.
            int menuId = NowInput.GetId(id, "context-menu");
            var secondary = NowInput.Interact(menuId, _rect, NowPointerButton.Secondary);

            if (secondary.dragging)
            {
                state.pan += secondary.dragDelta;
                result.changed = true;
                result.panned = true;
                NowControlState.RequestRepaint();
            }
            else if (secondary.clicked && state.linkActive == 0 && contextMenu != null)
            {
                if (!pointerOverPort && pointerNodeIndex >= 0)
                {
                    var node = _graph.nodes[pointerNodeIndex];
                    if (node != null && !_graph.IsNodeSelected(node.id))
                        SelectNode(node.id, ref result);
                }

                NowContextMenu.Open(menuId, secondary.pointerPosition);
                state.contextMenuGraphPosition = ScreenToGraph(secondary.pointerPosition, _rect, state);
                result.contextMenuOpened = true;
                result.contextMenuPosition = secondary.pointerPosition;
                result.contextMenuGraphPosition = state.contextMenuGraphPosition;
                NowControlState.RequestRepaint();
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
                float hitRadius = style.portHitRadius;
                var hit = new NowRect(center.x - hitRadius, center.y - hitRadius, hitRadius * 2f, hitRadius * 2f);
                int portId = PortControlId(canvasId, nodeIndex, node, direction, p, port);
                var primary = NowInput.Interact(portId, hit);

                if (primary.pressed)
                {
                    if (direction != NowNodePortDirection.Input || !TryPickUpLink(node, port, ref state))
                    {
                        state.linkActive = 1;
                        state.linkNodeIndex = nodeIndex;
                        state.linkPortIndex = p;
                        state.linkDirection = direction;
                        state.linkPicked = 0;
                        state.pickedLink = default;
                    }

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
                    _drawCache?.Invalidate();

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

        void InteractCompactToggle(
            int canvasId,
            int nodeIndex,
            NowNode node,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            if (IsRerouteNode(node))
                return;

            var toggleRect = CompactToggleScreenRect(node, _rect, state, style);
            int toggleId = NowInput.CombineId(NodeControlId(canvasId, nodeIndex, node), 0x7043);
            var interaction = NowInput.Interact(toggleId, toggleRect);

            if (interaction.pressed)
                SelectNodeFromPointer(node.id, ref result);

            if (interaction.clicked)
            {
                history?.Record(_graph);
                node.compact = !node.compact;
                result.changed = true;
                result.compactToggled = true;
                NowControlState.RequestRepaint();
            }
        }

        bool TryPickUpLink(NowNode inputNode, NowNodePort inputPort, ref CanvasState state)
        {
            if (!_graph.TryGetInputLink(inputNode.id, inputPort.id, out var link))
                return false;

            int outputNodeIndex = _graph.IndexOfNode(link.outputNodeId);

            if (outputNodeIndex < 0)
                return false;

            var outputNode = _graph.nodes[outputNodeIndex];

            for (int i = 0; i < outputNode.outputs.Count; ++i)
            {
                var candidate = outputNode.outputs[i];

                if (candidate != null && candidate.id == link.outputPortId)
                {
                    state.linkActive = 1;
                    state.linkNodeIndex = outputNodeIndex;
                    state.linkPortIndex = i;
                    state.linkDirection = NowNodePortDirection.Output;
                    state.linkPicked = 1;
                    state.pickedLink = link;
                    return true;
                }
            }

            return false;
        }

        void CommitPendingLink(
            int focusId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            ref NowNodeGraphResult result)
        {
            if (!TryGetPort(state.linkNodeIndex, state.linkDirection, state.linkPortIndex, out var sourceNode, out var sourcePort))
            {
                ClearLinkDrag(ref state);
                return;
            }

            bool picked = state.linkPicked != 0;
            var pickedLink = state.pickedLink;
            bool addNewLink = false;
            bool droppedOnPort = false;
            NowNodeLink newLink = default;
            Vector2 pointer = state.pointerLocalPosition;

            if ((FindPortAt(pointer, state, style, out var target) ||
                 FindCompatiblePortNear(pointer, sourceNode, sourcePort, state, style, out target)) &&
                TryGetPort(target.nodeIndex, target.direction, target.portIndex, out var targetNode, out var targetPort) &&
                _graph.TryCreateLink(sourceNode.id, sourcePort.id, targetNode.id, targetPort.id, out newLink, pickedLink))
            {
                droppedOnPort = true;
                addNewLink = !HasLink(newLink);
            }

            // Fresh drag released on empty canvas: open the connection-aware search
            // so the user can create a node that auto-wires to this port.
            if (!droppedOnPort && !picked && style.dropToSearch && schema != null && schema.nodeDefinitionCount > 0)
            {
                CaptureSearchLinkSource(sourceNode, sourcePort, ref state);
                ClearLinkDrag(ref state);
                OpenNodeSearch(focusId, ref state, ref result);
                return;
            }

            bool replugSamePort = picked && !addNewLink && newLink == pickedLink;
            bool removePicked = picked && !replugSamePort;

            if (addNewLink || removePicked)
                history?.Record(_graph);

            if (removePicked && _graph.RemoveLink(pickedLink))
            {
                result.changed = true;
                result.linkRemoved = true;
                result.removedLink = pickedLink;
            }

            if (addNewLink && _graph.TryAddLink(newLink))
            {
                result.changed = true;
                result.linkCreated = true;
                result.createdLink = newLink;
            }

            _drawCache?.Invalidate();
            ClearLinkDrag(ref state);
            NowControlState.RequestRepaint();
        }

        void CaptureSearchLinkSource(NowNode sourceNode, NowNodePort sourcePort, ref CanvasState state)
        {
            state.searchLinkSourceNodeId = sourceNode.id;
            state.searchLinkSourcePortId = sourcePort.id;
            state.searchLinkSourceIsOutput = (byte)(sourcePort.isOutput ? 1 : 0);
            state.searchLinkSourceType = sourcePort.typeId;
            state.searchHasLinkSource = 1;
        }

        void ClearLinkDrag(ref CanvasState state)
        {
            state.linkActive = 0;
            state.linkNodeIndex = 0;
            state.linkPortIndex = 0;
            state.linkDirection = default;
            state.linkPicked = 0;
            state.pickedLink = default;
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
            var cache = _drawCache;
            var links = _graph.links;

            if (cache == null)
            {
                for (int i = 0; i < links.Count; ++i)
                {
                    var link = links[i];

                    if ((link.inputNodeId == nodeId && link.inputPortId == portId) ||
                        (link.outputNodeId == nodeId && link.outputPortId == portId))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!cache.connectedPortsValid || cache.connectedLinkCount != links.Count)
            {
                cache.connectedPorts.Clear();

                for (int i = 0; i < links.Count; ++i)
                {
                    var link = links[i];
                    cache.connectedPorts.Add((link.inputNodeId, link.inputPortId));
                    cache.connectedPorts.Add((link.outputNodeId, link.outputPortId));
                }

                cache.connectedLinkCount = links.Count;
                cache.connectedPortsValid = true;
            }

            return cache.connectedPorts.Contains((nodeId, portId));
        }

        int CachedIndexOfNode(string nodeId)
        {
            var cache = _drawCache;

            if (cache == null)
                return _graph.IndexOfNode(nodeId);

            var nodes = _graph.nodes;

            if (!cache.nodeIndexValid || cache.nodeIndexCount != nodes.Count)
            {
                cache.nodeIndexById.Clear();

                for (int i = 0; i < nodes.Count; ++i)
                {
                    var node = nodes[i];

                    if (node != null && !string.IsNullOrEmpty(node.id) && !cache.nodeIndexById.ContainsKey(node.id))
                        cache.nodeIndexById.Add(node.id, i);
                }

                cache.nodeIndexCount = nodes.Count;
                cache.nodeIndexValid = true;
            }

            return !string.IsNullOrEmpty(nodeId) && cache.nodeIndexById.TryGetValue(nodeId, out int index)
                ? index
                : -1;
        }

        bool CachedIsNodeSelected(string nodeId)
        {
            var cache = _drawCache;

            if (cache == null)
                return _graph.IsNodeSelected(nodeId);

            var ids = _graph.selectedNodeIds;
            int count = ids != null ? ids.Count : 0;

            if (!cache.selectionValid ||
                cache.selectionCount != count ||
                !ReferenceEquals(cache.selectionPrimary, _graph.selectedNodeId))
            {
                cache.selectedIds.Clear();

                if (count > 0)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        if (ids[i] != null)
                            cache.selectedIds.Add(ids[i]);
                    }
                }
                else if (!string.IsNullOrEmpty(_graph.selectedNodeId))
                {
                    cache.selectedIds.Add(_graph.selectedNodeId);
                }

                cache.selectionCount = count;
                cache.selectionPrimary = _graph.selectedNodeId;
                cache.selectionValid = true;
            }

            return !string.IsNullOrEmpty(nodeId) && cache.selectedIds.Contains(nodeId);
        }

        void SelectNode(string nodeId, ref NowNodeGraphResult result)
        {
            string previous = _graph.selectedNodeId;
            int previousCount = _graph.SelectedNodeCount();
            bool hadLink = _graph.selectedLink.isValid;
            _graph.SelectNode(nodeId);
            _drawCache?.Invalidate();

            if (previous != _graph.selectedNodeId || previousCount != _graph.SelectedNodeCount() || hadLink)
            {
                result.changed = true;
                result.selectionChanged = true;
                NowControlState.RequestRepaint();
            }
        }

        void SelectLink(NowNodeLink link, ref NowNodeGraphResult result)
        {
            if (_graph.selectedLink == link && _graph.SelectedNodeCount() == 0)
                return;

            _graph.SelectLink(link);
            _drawCache?.Invalidate();
            result.changed = true;
            result.selectionChanged = true;
            NowControlState.RequestRepaint();
        }

        int FindLinkAt(Vector2 screenPoint, CanvasState state, NowNodeGraphStyle style)
        {
            Vector2 point = ScreenToGraph(screenPoint, _rect, state);
            float threshold = Mathf.Max(6f, style.connectionWidth * 2f) / Mathf.Max(state.zoom, 0.001f);
            float bestDistanceSquared = threshold * threshold;
            int bestIndex = -1;

            for (int i = 0; i < _graph.links.Count; ++i)
            {
                var link = _graph.links[i];

                if (!TryGetPortGraphPosition(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, state, style, out _, out var from) ||
                    !TryGetPortGraphPosition(link.inputNodeId, link.inputPortId, NowNodePortDirection.Input, state, style, out _, out var to))
                {
                    continue;
                }

                float distanceSquared = DistanceSquaredToConnection(point, from, to, threshold);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Distance from a point to the sampled connection curve, or
        /// float.MaxValue when the point lies further than
        /// <paramref name="earlyOutDistance"/> from the curve's control-polygon
        /// AABB. The bezier and its sampled polyline both stay inside the convex
        /// hull of the control points, so the early-out can never reject a point
        /// within that distance of the curve.
        /// </summary>
        static float DistanceSquaredToConnection(Vector2 point, Vector2 from, Vector2 to, float earlyOutDistance)
        {
            const int Segments = 24;

            float tangent = Mathf.Max(42f, Mathf.Abs(to.x - from.x) * 0.45f);

            if (point.x < Mathf.Min(from.x, to.x - tangent) - earlyOutDistance ||
                point.x > Mathf.Max(from.x + tangent, to.x) + earlyOutDistance ||
                point.y < Mathf.Min(from.y, to.y) - earlyOutDistance ||
                point.y > Mathf.Max(from.y, to.y) + earlyOutDistance)
            {
                return float.MaxValue;
            }

            Vector2 c1 = from + Vector2.right * tangent;
            Vector2 c2 = to + Vector2.left * tangent;

            float best = float.MaxValue;
            Vector2 previous = from;

            for (int i = 1; i <= Segments; ++i)
            {
                float t = i / (float)Segments;
                float u = 1f - t;
                Vector2 current =
                    u * u * u * from +
                    3f * u * u * t * c1 +
                    3f * u * t * t * c2 +
                    t * t * t * to;

                best = Mathf.Min(best, DistanceSquaredToSegment(point, previous, current));
                previous = current;
            }

            return best;
        }

        static float DistanceSquaredToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float lengthSquared = delta.sqrMagnitude;

            if (lengthSquared <= 0.0001f)
                return (point - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / lengthSquared);
            return (point - (a + delta * t)).sqrMagnitude;
        }

        void SelectNodeFromPointer(string nodeId, ref NowNodeGraphResult result)
        {
            var frame = NowTextInput.current;
            bool toggle = frame.command;
            bool additive = frame.shift;

            if (!toggle && !additive)
            {
                if (!_graph.IsNodeSelected(nodeId))
                    SelectNode(nodeId, ref result);

                return;
            }

            string previous = _graph.selectedNodeId;
            int previousCount = _graph.SelectedNodeCount();

            if (toggle && _graph.IsNodeSelected(nodeId))
                _graph.SetNodeSelected(nodeId, false);
            else
                _graph.AddNodeToSelection(nodeId);

            _drawCache?.Invalidate();

            if (previous != _graph.selectedNodeId || previousCount != _graph.SelectedNodeCount())
            {
                result.changed = true;
                result.selectionChanged = true;
                NowControlState.RequestRepaint();
            }
        }

        void BeginNodeDrag(int nodeControlId, NowNode activeNode, ref CanvasState state)
        {
            state.nodeDragControlId = nodeControlId;
            state.nodeDragHistoryRecorded = 0;
            state.nodeDragActiveNodeStart = activeNode != null ? activeNode.position : default;
            state.nodeDragPointerGraphStart = ScreenToGraph(state.pointerLocalPosition, _rect, state);
            ClearNodeSnapGuide(ref state);
        }

        Vector2 NodeDragDeltaFromStart(NowNode activeNode, Vector2 fallbackDelta, CanvasState state)
        {
            if (activeNode == null || state.nodeDragControlId == 0 || !NowInput.current.hasPointer)
                return fallbackDelta;

            Vector2 currentPointer = ScreenToGraph(state.pointerLocalPosition, _rect, state);
            Vector2 targetPosition = state.nodeDragActiveNodeStart + (currentPointer - state.nodeDragPointerGraphStart);
            return targetPosition - activeNode.position;
        }

        bool MoveSelectedNodes(
            NowNode activeNode,
            Vector2 delta,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphHistory history,
            bool allowSnapping)
        {
            if (activeNode == null || delta == Vector2.zero)
                return false;

            int selectedCount = activeNode != null && CachedIsNodeSelected(activeNode.id)
                ? _graph.SelectedNodeCount()
                : 0;
            bool moveSelection = selectedCount > 1;

            if (allowSnapping && style.snapNodes)
            {
                if (style.nodeSnapMode == NowNodeSnapMode.Grid)
                    delta = SnapNodeDragDeltaToGrid(activeNode, delta, style);
                else if (style.nodeSnapMode == NowNodeSnapMode.Align && style.nodeSnapDistance > 0f)
                    delta = SnapNodeDragDeltaToAlignment(activeNode, delta, ref state, style, moveSelection);
            }

            if (delta == Vector2.zero)
                return false;

            if (state.nodeDragHistoryRecorded == 0)
            {
                history?.Record(_graph);
                state.nodeDragHistoryRecorded = 1;
            }

            if (activeNode != null && moveSelection)
            {
                for (int i = 0; i < _graph.nodes.Count; ++i)
                {
                    var node = _graph.nodes[i];

                    if (node != null && CachedIsNodeSelected(node.id))
                        node.position += delta;
                }
            }
            else if (activeNode != null)
            {
                activeNode.position += delta;
            }

            return true;
        }

        Vector2 SnapNodeDragDeltaToGrid(NowNode activeNode, Vector2 delta, NowNodeGraphStyle style)
        {
            if (activeNode == null)
                return delta;

            float step = Mathf.Max(1f, style.gridSpacing);
            Vector2 proposed = activeNode.position + delta;
            Vector2 snapped = new Vector2(
                Mathf.Round(proposed.x / step) * step,
                Mathf.Round(proposed.y / step) * step);
            return delta + (snapped - proposed);
        }

        Vector2 SnapNodeDragDeltaToAlignment(
            NowNode activeNode,
            Vector2 delta,
            ref CanvasState state,
            NowNodeGraphStyle style,
            bool moveSelection)
        {
            ClearNodeSnapGuide(ref state);

            if (activeNode == null || _graph.nodes.Count < 2)
                return delta;

            if (!TryGetMovingNodeBounds(activeNode, style, moveSelection, out var movingBounds))
                return delta;

            float graphSnapDistance = style.nodeSnapDistance;

            if (graphSnapDistance <= 0f)
                return delta;

            var proposedBounds = movingBounds.Offset(delta);
            float bestX = graphSnapDistance;
            float bestY = graphSnapDistance;
            float snapX = 0f;
            float snapY = 0f;
            float graphSnapProximity = style.nodeSnapProximity;
            float graphGuidePadding = SnapGuidePadding / Mathf.Max(state.zoom, 0.001f);
            bool snappedX = false;
            bool snappedY = false;
            Vector2 snapXFrom = default;
            Vector2 snapXTo = default;
            Vector2 snapYFrom = default;
            Vector2 snapYTo = default;

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node == null || IsMovingNode(node, activeNode, moveSelection))
                    continue;

                var target = new NowRect(node.position, ResolveNodeSize(node, style));
                bool canSnapX = RangesWithin(proposedBounds.y, proposedBounds.yMax, target.y, target.yMax, graphSnapProximity);
                bool canSnapY = RangesWithin(proposedBounds.x, proposedBounds.xMax, target.x, target.xMax, graphSnapProximity);

                if (canSnapX)
                {
                    TestNodeSnapAxis(proposedBounds.x, target.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.x, target.center.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.x, target.xMax, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.center.x, target.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.center.x, target.center.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.center.x, target.xMax, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.xMax, target.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.xMax, target.center.x, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                    TestNodeSnapAxis(proposedBounds.xMax, target.xMax, proposedBounds, target, true, graphGuidePadding, ref bestX, ref snapX, ref snappedX, ref snapXFrom, ref snapXTo);
                }

                if (canSnapY)
                {
                    TestNodeSnapAxis(proposedBounds.y, target.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.y, target.center.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.y, target.yMax, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.center.y, target.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.center.y, target.center.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.center.y, target.yMax, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.yMax, target.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.yMax, target.center.y, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                    TestNodeSnapAxis(proposedBounds.yMax, target.yMax, proposedBounds, target, false, graphGuidePadding, ref bestY, ref snapY, ref snappedY, ref snapYFrom, ref snapYTo);
                }
            }

            if (snappedX)
            {
                delta.x += snapX;
                state.snapGuideFlags |= SnapGuideXFlag;
                state.snapGuideXFrom = snapXFrom;
                state.snapGuideXTo = snapXTo;
            }

            if (snappedY)
            {
                delta.y += snapY;
                state.snapGuideFlags |= SnapGuideYFlag;
                state.snapGuideYFrom = snapYFrom;
                state.snapGuideYTo = snapYTo;
            }

            return delta;
        }

        bool TryGetMovingNodeBounds(
            NowNode activeNode,
            NowNodeGraphStyle style,
            bool moveSelection,
            out NowRect bounds)
        {
            if (!moveSelection)
            {
                bounds = new NowRect(activeNode.position, ResolveNodeSize(activeNode, style));
                return true;
            }

            bool hasBounds = false;
            float xMin = 0f;
            float yMin = 0f;
            float xMax = 0f;
            float yMax = 0f;

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node == null || !CachedIsNodeSelected(node.id))
                    continue;

                var rect = new NowRect(node.position, ResolveNodeSize(node, style));

                if (!hasBounds)
                {
                    xMin = rect.x;
                    yMin = rect.y;
                    xMax = rect.xMax;
                    yMax = rect.yMax;
                    hasBounds = true;
                }
                else
                {
                    xMin = Mathf.Min(xMin, rect.x);
                    yMin = Mathf.Min(yMin, rect.y);
                    xMax = Mathf.Max(xMax, rect.xMax);
                    yMax = Mathf.Max(yMax, rect.yMax);
                }
            }

            bounds = hasBounds
                ? new NowRect(xMin, yMin, xMax - xMin, yMax - yMin)
                : default;
            return hasBounds;
        }

        bool IsMovingNode(NowNode node, NowNode activeNode, bool moveSelection)
        {
            if (moveSelection)
                return CachedIsNodeSelected(node.id);

            return ReferenceEquals(node, activeNode);
        }

        static bool RangesWithin(float aMin, float aMax, float bMin, float bMax, float maxGap)
        {
            if (aMax < bMin)
                return bMin - aMax <= maxGap;

            if (bMax < aMin)
                return aMin - bMax <= maxGap;

            return true;
        }

        static void TestNodeSnapAxis(
            float movingAnchor,
            float targetAnchor,
            NowRect movingBounds,
            NowRect targetBounds,
            bool vertical,
            float guidePadding,
            ref float bestDistance,
            ref float adjustment,
            ref bool snapped,
            ref Vector2 guideFrom,
            ref Vector2 guideTo)
        {
            float candidate = targetAnchor - movingAnchor;
            float distance = Mathf.Abs(candidate);

            if ((!snapped && distance <= bestDistance) || (snapped && distance < bestDistance))
            {
                bestDistance = distance;
                adjustment = candidate;
                snapped = true;

                if (vertical)
                {
                    float yMin = Mathf.Min(movingBounds.y, targetBounds.y) - guidePadding;
                    float yMax = Mathf.Max(movingBounds.yMax, targetBounds.yMax) + guidePadding;
                    guideFrom = new Vector2(targetAnchor, yMin);
                    guideTo = new Vector2(targetAnchor, yMax);
                }
                else
                {
                    float xMin = Mathf.Min(movingBounds.x, targetBounds.x) - guidePadding;
                    float xMax = Mathf.Max(movingBounds.xMax, targetBounds.xMax) + guidePadding;
                    guideFrom = new Vector2(xMin, targetAnchor);
                    guideTo = new Vector2(xMax, targetAnchor);
                }
            }
        }

        static void ClearNodeSnapGuide(ref CanvasState state)
        {
            state.snapGuideFlags = 0;
            state.snapGuideXFrom = default;
            state.snapGuideXTo = default;
            state.snapGuideYFrom = default;
            state.snapGuideYTo = default;
        }

        static void DrawNodeSnapGuide(CanvasState state, NowNodeGraphStyle style)
        {
            if (state.snapGuideFlags == 0 || style.nodeSnapGuide.a <= 0f)
                return;

            float zoom = Mathf.Max(state.zoom, 0.001f);
            float width = Mathf.Max(1f, style.nodeSnapGuideWidth) / zoom;
            float dash = 7f / zoom;
            float gap = 5f / zoom;

            if ((state.snapGuideFlags & SnapGuideXFlag) != 0)
            {
                Now.Line(state.snapGuideXFrom, state.snapGuideXTo)
                    .SetColor(style.nodeSnapGuide)
                    .SetWidth(width)
                    .SetCap(NowLineCap.Round)
                    .SetDash(dash, gap)
                    .Draw();
            }

            if ((state.snapGuideFlags & SnapGuideYFlag) != 0)
            {
                Now.Line(state.snapGuideYFrom, state.snapGuideYTo)
                    .SetColor(style.nodeSnapGuide)
                    .SetWidth(width)
                    .SetCap(NowLineCap.Round)
                    .SetDash(dash, gap)
                    .Draw();
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
            bool additive = state.selectionAdditive != 0;
            bool subtractive = state.selectionSubtractive != 0;
            state.selectionActive = 0;
            state.selectionAdditive = 0;
            state.selectionSubtractive = 0;

            if (rect.width <= 2f && rect.height <= 2f)
            {
                // A modifier-click that produced no box must not wipe the selection.
                if (!additive && !subtractive)
                    SelectNode(null, ref result);

                NowControlState.RequestRepaint();
                return;
            }

            int previousCount = _graph.SelectedNodeCount();
            string previous = _graph.selectedNodeId;

            if (!additive && !subtractive)
                _graph.ClearSelection();

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node == null || !rect.Overlaps(NodeScreenRect(node, _rect, state, style)))
                    continue;

                if (subtractive)
                    _graph.SetNodeSelected(node.id, false);
                else
                    _graph.AddNodeToSelection(node.id);
            }

            _drawCache?.Invalidate();

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

                if (state.linkActive != 0 && state.linkPicked != 0 && link == state.pickedLink)
                    continue;

                if (!TryGetPortGraphPosition(link.outputNodeId, link.outputPortId, NowNodePortDirection.Output, state, style, out var outputPort, out var from) ||
                    !TryGetPortGraphPosition(link.inputNodeId, link.inputPortId, NowNodePortDirection.Input, state, style, out var inputPort, out var to))
                {
                    continue;
                }

                renderer.DrawLink(new NowNodeGraphLinkContext(
                    _rect,
                    link,
                    from,
                    to,
                    LinkEndpointColor(outputPort, style),
                    LinkEndpointColor(inputPort, style),
                    style.connectionWidth,
                    false,
                    style,
                    i == state.hoveredLinkIndex,
                    _graph.selectedLink == link));
            }
        }

        Color LinkEndpointColor(NowNodePort port, NowNodeGraphStyle style)
        {
            if (port != null)
            {
                if (port.color.a > 0f)
                    return port.color;

                var schema = _graph.schema;

                if (schema != null && schema.TryGetTypeColor(port.typeId, out var typeColor) && typeColor.a > 0f)
                    return typeColor;
            }

            return style.connection;
        }

        void DrawPendingLink(CanvasState state, NowNodeGraphStyle style, INowNodeGraphRenderer renderer)
        {
            if (!TryGetPort(state.linkNodeIndex, state.linkDirection, state.linkPortIndex, out var node, out var port))
                return;

            Vector2 anchor = PortGraphPosition(node, state.linkDirection, state.linkPortIndex, style);
            Vector2 freeEnd = NowInput.current.hasPointer
                ? ScreenToGraph(state.pointerLocalPosition, _rect, state)
                : anchor;
            Color sourceColor = LinkEndpointColor(port, style);
            Color targetColor = sourceColor;

            if (NowInput.current.hasPointer)
            {
                if (FindPortAt(state.pointerLocalPosition, state, style, out var target) &&
                    TryGetPort(target.nodeIndex, target.direction, target.portIndex, out var targetNode, out var targetPort))
                {
                    if (_graph.TryCreateLink(node.id, port.id, targetNode.id, targetPort.id, out _, state.pickedLink))
                    {
                        targetColor = LinkEndpointColor(targetPort, style);
                        freeEnd = ScreenToGraph(target.center, _rect, state);
                    }
                    else
                    {
                        sourceColor = style.incompatiblePort;
                        targetColor = style.incompatiblePort;
                    }
                }
                else if (FindCompatiblePortNear(state.pointerLocalPosition, node, port, state, style, out var snap) &&
                         TryGetPort(snap.nodeIndex, snap.direction, snap.portIndex, out _, out var snapPort))
                {
                    targetColor = LinkEndpointColor(snapPort, style);
                    freeEnd = ScreenToGraph(snap.center, _rect, state);
                }
            }

            Vector2 from;
            Vector2 to;
            Color fromColor;
            Color toColor;

            if (state.linkDirection == NowNodePortDirection.Output)
            {
                from = anchor;
                to = freeEnd;
                fromColor = sourceColor;
                toColor = targetColor;
            }
            else
            {
                from = freeEnd;
                to = anchor;
                fromColor = targetColor;
                toColor = sourceColor;
            }

            renderer.DrawLink(new NowNodeGraphLinkContext(
                _rect,
                default,
                from,
                to,
                fromColor,
                toColor,
                style.connectionWidth,
                true,
                style));
        }

        void DrawNodes(
            int canvasId,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            INowNodeGraphRenderer renderer,
            int hoveredNodeIndex,
            ref NowNodeGraphResult result)
        {
            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node != null && !CachedIsNodeSelected(node.id))
                    DrawNode(canvasId, i, node, ref state, style, schema, history, renderer, false, i == hoveredNodeIndex, ref result);
            }

            for (int i = 0; i < _graph.nodes.Count; ++i)
            {
                var node = _graph.nodes[i];

                if (node != null && CachedIsNodeSelected(node.id))
                    DrawNode(canvasId, i, node, ref state, style, schema, history, renderer, true, i == hoveredNodeIndex, ref result);
            }
        }

        void DrawNode(
            int canvasId,
            int nodeIndex,
            NowNode node,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            INowNodeGraphRenderer renderer,
            bool selected,
            bool hovered,
            ref NowNodeGraphResult result)
        {
            // Compute rects in graph (local) space
            var nodeSize = ResolveNodeSize(node, style);
            var rect = new NowRect(node.position, nodeSize);
            bool reroute = IsRerouteNode(node);
            var titleRect = new NowRect(rect.x, rect.y, rect.width, reroute ? rect.height : style.titleHeight);
            var contentRect = reroute ? default : NodeContentGraphRect(node, rect, titleRect, style);
            bool hasPreview = !reroute && !node.compact && NodeHasPreview(node, out _);
            var previewRect = hasPreview ? PreviewGraphRect(node, rect, style) : default;
            var compactToggleRect = reroute ? default : CompactToggleGraphRect(rect, style);

            renderer.DrawNode(new NowNodeGraphNodeContext(
                _graph,
                node,
                nodeIndex,
                rect,
                titleRect,
                contentRect,
                selected,
                state.zoom,
                style,
                hasPreview,
                previewRect,
                hovered,
                node.compact,
                compactToggleRect,
                reroute));

            if (!reroute && !node.compact)
            {
                DrawNodeContent(node, rect, contentRect, ref state, style, schema, history, selected, ref result);

                if (hasPreview && !previewRect.isEmpty)
                    DrawNodePreviewContent(node, rect, previewRect, ref state, style, schema, history, selected, ref result);
            }

            DrawPortList(canvasId, nodeIndex, node, NowNodePortDirection.Input, state, style, renderer);
            DrawPortList(canvasId, nodeIndex, node, NowNodePortDirection.Output, state, style, renderer);
        }

        void DrawNodeContent(
            NowNode node,
            NowRect nodeRect,
            NowRect bodyRect,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
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
            context.history = history;
            context.node = node;
            context.nodeRect = nodeRect;
            context.bodyRect = bodyRect;
            context.style = style;
            context.zoom = state.zoom;
            context.viewportRect = _rect;
            context.pan = state.pan;
            context.screenTransform = Now.currentTransform;
            context.selected = selected;
            context.isPreview = false;
            context.changed = false;

            if (_nodeContent != null)
                _nodeContent(context);
            else
                schema?.DrawNodeContent(context);

            if (context.changed)
            {
                result.changed = true;
                _drawCache?.Invalidate();
            }
        }

        void DrawNodePreviewContent(
            NowNode node,
            NowRect nodeRect,
            NowRect previewRect,
            ref CanvasState state,
            NowNodeGraphStyle style,
            NowNodeGraphSchema schema,
            NowNodeGraphHistory history,
            bool selected,
            ref NowNodeGraphResult result)
        {
            var context = state.contentContext;

            if (context == null)
            {
                context = new NowNodeContentContext();
                state.contentContext = context;
            }

            context.graph = _graph;
            context.schema = schema;
            context.history = history;
            context.node = node;
            context.nodeRect = nodeRect;
            context.bodyRect = previewRect;
            context.style = style;
            context.zoom = state.zoom;
            context.viewportRect = _rect;
            context.pan = state.pan;
            context.screenTransform = Now.currentTransform;
            context.selected = selected;
            context.isPreview = true;
            context.changed = false;

            schema?.DrawNodePreview(context);

            if (context.changed)
            {
                result.changed = true;
                _drawCache?.Invalidate();
            }
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
            var nodeSize = ResolveNodeSize(node, style);
            var nodeRect = new NowRect(node.position, nodeSize);

            for (int i = 0; i < ports.Count; ++i)
            {
                var port = ports[i];

                if (port == null)
                    continue;

                Vector2 center = PortGraphPosition(node, direction, i, style);
                int portId = PortControlId(canvasId, nodeIndex, node, direction, i, port);
                float hover = NowControlState.Transition(portId, NowInput.IsHovered(new NowRect(center.x - 9f, center.y - 9f, 18f, 18f)));
                float radius = Mathf.Max(3.5f, style.portRadius + hover * 1.5f);
                Color color = PortColor(port, direction, style);
                bool compatible = IsCompatibleDropTarget(nodeIndex, direction, i, state);
                bool connected = HasLinksForPort(node.id, port.id);

                if (compatible)
                {
                    color = style.compatiblePort;
                    radius += 1f;
                }
                else if (state.linkActive != 0 && !IsLinkDragSource(nodeIndex, direction, i, state))
                {
                    color.a *= 0.35f;
                }

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
                    style,
                    connected));
            }
        }

        static bool IsLinkDragSource(int nodeIndex, NowNodePortDirection direction, int portIndex, CanvasState state)
        {
            return state.linkActive != 0 &&
                state.linkNodeIndex == nodeIndex &&
                state.linkPortIndex == portIndex &&
                state.linkDirection == direction;
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

            return _graph.TryCreateLink(sourceNode.id, sourcePort.id, targetNode.id, targetPort.id, out _, state.pickedLink);
        }

        Color PortColor(NowNodePort port, NowNodePortDirection direction, NowNodeGraphStyle style)
        {
            if (port.color.a > 0f)
                return port.color;

            var schema = _graph.schema;

            if (schema != null && schema.TryGetTypeColor(port.typeId, out var typeColor) && typeColor.a > 0f)
                return typeColor;

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

        bool TryGetPortGraphPosition(
            string nodeId,
            string portId,
            NowNodePortDirection direction,
            CanvasState state,
            NowNodeGraphStyle style,
            out NowNodePort port,
            out Vector2 position)
        {
            port = null;
            position = default;
            int nodeIndex = CachedIndexOfNode(nodeId);

            if (nodeIndex < 0)
                return false;

            var node = _graph.nodes[nodeIndex];
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

            for (int i = 0; i < ports.Count; ++i)
            {
                if (ports[i] != null && ports[i].id == portId)
                {
                    port = ports[i];
                    position = PortGraphPosition(node, direction, i, style);
                    return true;
                }
            }

            return false;
        }

        bool FindPortAt(Vector2 point, CanvasState state, NowNodeGraphStyle style, out PortHit hit)
        {
            // Nearest port center within the hit radius, so enlarged (touch-friendly)
            // targets never grab an adjacent port when they overlap.
            hit = default;
            float bestSqr = style.portHitRadius * style.portHitRadius;
            bool found = false;

            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node == null)
                    continue;

                found |= ClosestPortOnNode(node, n, NowNodePortDirection.Input, point, state, style, ref bestSqr, ref hit);
                found |= ClosestPortOnNode(node, n, NowNodePortDirection.Output, point, state, style, ref bestSqr, ref hit);
            }

            return found;
        }

        bool ClosestPortOnNode(
            NowNode node,
            int nodeIndex,
            NowNodePortDirection direction,
            Vector2 point,
            CanvasState state,
            NowNodeGraphStyle style,
            ref float bestSqr,
            ref PortHit hit)
        {
            var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;
            bool found = false;

            for (int p = 0; p < ports.Count; ++p)
            {
                if (ports[p] == null)
                    continue;

                Vector2 center = PortScreenPosition(node, direction, p, _rect, state, style);
                float distSqr = (center - point).sqrMagnitude;

                if (distSqr <= bestSqr)
                {
                    bestSqr = distSqr;
                    hit = new PortHit
                    {
                        valid = true,
                        nodeIndex = nodeIndex,
                        portIndex = p,
                        direction = direction,
                        center = center
                    };
                    found = true;
                }
            }

            return found;
        }

        // On link release, if the pointer isn't over any port, snap to the nearest
        // port (within the larger snap radius) that would form a valid connection.
        bool FindCompatiblePortNear(
            Vector2 point,
            NowNode sourceNode,
            NowNodePort sourcePort,
            CanvasState state,
            NowNodeGraphStyle style,
            out PortHit hit)
        {
            hit = default;
            float bestSqr = style.portSnapRadius * style.portSnapRadius;
            bool found = false;

            for (int n = _graph.nodes.Count - 1; n >= 0; --n)
            {
                var node = _graph.nodes[n];

                if (node == null || node == sourceNode)
                    continue;

                NowNodePortDirection direction = sourcePort.isOutput ? NowNodePortDirection.Input : NowNodePortDirection.Output;
                var ports = direction == NowNodePortDirection.Input ? node.inputs : node.outputs;

                for (int p = 0; p < ports.Count; ++p)
                {
                    var port = ports[p];

                    if (port == null)
                        continue;

                    Vector2 center = PortScreenPosition(node, direction, p, _rect, state, style);
                    float distSqr = (center - point).sqrMagnitude;

                    if (distSqr <= bestSqr &&
                        _graph.TryCreateLink(sourceNode.id, sourcePort.id, node.id, port.id, out _, state.pickedLink))
                    {
                        bestSqr = distSqr;
                        hit = new PortHit
                        {
                            valid = true,
                            nodeIndex = n,
                            portIndex = p,
                            direction = direction,
                            center = center
                        };
                        found = true;
                    }
                }
            }

            return found;
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

            if (IsRerouteNode(node))
                return rect;

            return new NowRect(rect.x, rect.y, rect.width, style.titleHeight * state.zoom);
        }

        NowRect NodeDragScreenRect(
            int nodeControlId,
            NowNode node,
            NowRect viewport,
            CanvasState state,
            NowNodeGraphStyle style)
        {
            NowRect nodeRect = NodeScreenRect(node, viewport, state, style);

            if (IsRerouteNode(node) ||
                (NowInput.activeId == nodeControlId && NowInput.activeButton == NowPointerButton.Primary))
            {
                return nodeRect;
            }

            if (!NowInput.current.hasPointer)
                return NodeTitleScreenRect(node, viewport, state, style);

            Vector2 pointer = state.pointerLocalPosition;

            if (!nodeRect.Contains(pointer))
                return NodeTitleScreenRect(node, viewport, state, style);

            if (CompactToggleScreenRect(node, viewport, state, style).Contains(pointer))
                return NodeTitleScreenRect(node, viewport, state, style);

            NowRect titleRect = NodeTitleScreenRect(node, viewport, state, style);

            if (titleRect.Contains(pointer))
                return nodeRect;

            NowRect contentRect = NodeContentScreenRect(node, nodeRect, titleRect, state, style);

            if (!contentRect.isEmpty && TopContentControlScreenRect(node, nodeRect, contentRect, state, style).Contains(pointer))
                return titleRect;

            return nodeRect;
        }

        NowRect TopContentControlScreenRect(
            NowNode node,
            NowRect nodeRect,
            NowRect contentRect,
            CanvasState state,
            NowNodeGraphStyle style)
        {
            float zoom = Mathf.Max(state.zoom, 0.001f);
            float offset = TryGetContentHeight(node, out float contentHeight)
                ? contentHeight * zoom
                : PortTopOffset(node) * zoom;

            if (offset <= 0f)
            {
                float rowHeight = Mathf.Max(style.portRowHeight, 28f) * zoom;
                float gap = 4f * zoom;
                offset = rowHeight * 2f + gap;
            }

            float controlHeight = Mathf.Min(contentRect.height, offset);
            return new NowRect(nodeRect.x, contentRect.y, nodeRect.width, controlHeight);
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

            if (!node.compact && NodeHasPreview(node, out float previewHeight))
                bottom += (previewHeight + style.contentPadding * 0.5f) * zoom;

            float height = Mathf.Max(0f, nodeRect.yMax - top - bottom);

            if (TryGetContentHeight(node, out float contentHeight))
                height = Mathf.Min(height, contentHeight * zoom);

            return new NowRect(
                nodeRect.x + left,
                top,
                Mathf.Max(0f, nodeRect.width - left - right),
                height);
        }

        NowRect NodeContentGraphRect(
            NowNode node,
            NowRect nodeRect,
            NowRect titleRect,
            NowNodeGraphStyle style)
        {
            float padding = style.contentPadding;
            float labelLane = style.portLabelLaneWidth;
            bool hasInputs = node.inputs != null && node.inputs.Count > 0;
            bool hasOutputs = node.outputs != null && node.outputs.Count > 0;
            float left = hasInputs ? labelLane : padding;
            float right = hasOutputs ? labelLane : padding;
            float top = titleRect.yMax + padding * 0.5f;
            float bottom = padding;

            if (!node.compact && NodeHasPreview(node, out float previewHeight))
                bottom += previewHeight + style.contentPadding * 0.5f;

            float height = Mathf.Max(0f, nodeRect.yMax - top - bottom);

            if (TryGetContentHeight(node, out float contentHeight))
                height = Mathf.Min(height, contentHeight);

            return new NowRect(
                nodeRect.x + left,
                top,
                Mathf.Max(0f, nodeRect.width - left - right),
                height);
        }

        bool IsRerouteNode(NowNode node)
        {
            var schema = _graph.schema;
            return node != null && schema != null && schema.IsReroute(node.kindId);
        }

        Vector2 ResolveNodeSize(NowNode node, NowNodeGraphStyle style)
        {
            if (IsRerouteNode(node))
                return new Vector2(26f, 26f);

            Vector2 size = node.size;

            if (size.x <= 0f)
                size.x = _graph.defaultNodeSize.x > 0f ? _graph.defaultNodeSize.x : 180f;

            bool autoHeight = size.y < 0f;

            if (autoHeight)
                size.y = 0f;
            else if (size.y <= 0f)
                size.y = _graph.defaultNodeSize.y > 0f ? _graph.defaultNodeSize.y : 118f;

            int rows = Mathf.Max(node.inputs != null ? node.inputs.Count : 0, node.outputs != null ? node.outputs.Count : 0);
            float minHeight = style.titleHeight + 12f + PortTopOffset(node) + rows * style.portRowHeight;

            if (node.compact)
                return RoundNodeSizeToGrid(new Vector2(Mathf.Clamp(Mathf.Max(size.x, 128f), CompactMinWidth, CompactMaxWidth), minHeight), style);

            float height = autoHeight ? minHeight : Mathf.Max(size.y, minHeight);

            if (NodeHasPreview(node, out float previewHeight))
                height += previewHeight + style.contentPadding;

            return RoundNodeSizeToGrid(new Vector2(Mathf.Max(size.x, 128f), height), style);
        }

        static Vector2 RoundNodeSizeToGrid(Vector2 size, NowNodeGraphStyle style)
        {
            if (!style.snapNodes || style.nodeSnapMode != NowNodeSnapMode.Grid)
                return size;

            float step = Mathf.Max(1f, style.gridSpacing);
            return new Vector2(
                Mathf.Ceil(size.x / step) * step,
                Mathf.Ceil(size.y / step) * step);
        }

        bool NodeHasPreview(NowNode node, out float previewHeight)
        {
            previewHeight = 0f;
            var schema = _graph.schema;
            return node != null && schema != null && schema.TryGetPreviewHeight(node.kindId, out previewHeight);
        }

        NowRect PreviewGraphRect(NowNode node, NowRect nodeRect, NowNodeGraphStyle style)
        {
            if (node.compact || !NodeHasPreview(node, out float previewHeight))
                return default;

            float padding = style.contentPadding;
            return new NowRect(
                nodeRect.x + padding,
                nodeRect.yMax - padding - previewHeight,
                Mathf.Max(0f, nodeRect.width - padding * 2f),
                previewHeight);
        }

        NowRect CompactToggleGraphRect(NowRect nodeRect, NowNodeGraphStyle style)
        {
            float size = Mathf.Clamp(style.titleHeight - 12f, 12f, 20f);
            float y = nodeRect.y + (style.titleHeight - size) * 0.5f;
            return new NowRect(nodeRect.xMax - size - 8f, y, size, size);
        }

        NowRect GraphRectToScreen(NowRect rect, NowRect viewport, CanvasState state)
        {
            if (rect.isEmpty)
                return default;

            return new NowRect(GraphToScreen(rect.position, viewport, state), rect.size * state.zoom);
        }

        NowRect CompactToggleScreenRect(NowNode node, NowRect viewport, CanvasState state, NowNodeGraphStyle style)
        {
            var nodeRect = new NowRect(node.position, ResolveNodeSize(node, style));
            return GraphRectToScreen(CompactToggleGraphRect(nodeRect, style), viewport, state);
        }

        Vector2 PortScreenPosition(
            NowNode node,
            NowNodePortDirection direction,
            int index,
            NowRect viewport,
            CanvasState state,
            NowNodeGraphStyle style)
        {
            return GraphToScreen(PortGraphPosition(node, direction, index, style), viewport, state);
        }

        Vector2 PortGraphPosition(NowNode node, NowNodePortDirection direction, int index, NowNodeGraphStyle style)
        {
            Vector2 size = ResolveNodeSize(node, style);
            float x = direction == NowNodePortDirection.Input ? node.position.x : node.position.x + size.x;

            if (IsRerouteNode(node))
                return new Vector2(x, node.position.y + size.y * 0.5f);

            float y = node.position.y + style.titleHeight + 8f + PortTopOffset(node) + index * style.portRowHeight + style.portRowHeight * 0.5f;
            return new Vector2(x, y);
        }

        float PortTopOffset(NowNode node)
        {
            var schema = _graph.schema;
            return node != null && schema != null && schema.TryGetPortTopOffset(node.kindId, out float offset)
                ? offset
                : 0f;
        }

        bool TryGetContentHeight(NowNode node, out float height)
        {
            var schema = _graph.schema;
            if (node != null && schema != null && schema.TryGetContentHeight(node.kindId, out height))
                return true;

            height = 0f;
            return false;
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
            int idHash = node.idHash;
            return idHash != 0
                ? NowInput.CombineId(canvasId, idHash)
                : NowInput.CombineId(canvasId, 0x4e000 + nodeIndex);
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
            int idHash = port.idHash;
            return idHash != 0
                ? NowInput.CombineId(baseId, idHash)
                : NowInput.CombineId(baseId, portIndex + 1);
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
            state.hoveredLinkIndex = -1;
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
            style.portHitRadius = Mathf.Max(4f, style.portHitRadius);
            style.portSnapRadius = Mathf.Max(style.portHitRadius, style.portSnapRadius);
            style.nudgeStep = Mathf.Max(0f, style.nudgeStep);
            style.nudgeStepLarge = Mathf.Max(0f, style.nudgeStepLarge);
            style.nodeSnapDistance = Mathf.Max(0f, style.nodeSnapDistance);
            style.nodeSnapProximity = Mathf.Max(0f, style.nodeSnapProximity);
            style.nodeSnapGuideWidth = Mathf.Max(0f, style.nodeSnapGuideWidth);
            style.minZoom = Mathf.Max(0.05f, style.minZoom);
            style.maxZoom = Mathf.Max(style.minZoom, style.maxZoom);
            style.zoomStep = Mathf.Max(1.01f, style.zoomStep);

            if (style.nodeSnapMode != NowNodeSnapMode.Grid && style.nodeSnapMode != NowNodeSnapMode.Align)
                style.nodeSnapMode = NowNodeSnapMode.Grid;
        }
    }
}
