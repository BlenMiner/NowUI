using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.NodeGraph;

public class NowNodeGraphTests
{
    const int Float = 1;
    const int Float3 = 3;
    const int Float4 = 4;
    const int TextureNode = 100;
    const int OutputNode = 200;
    const int ValuePort = 10;
    const int ColorPort = 20;
    const int EvalConstant = 900;
    const int EvalAdd = 901;
    const int EvalRelay = 902;
    const int EvalDual = 903;
    const int EvalOut = 1;
    const int EvalA = 2;
    const int EvalB = 3;
    const int EvalSecond = 4;

    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowTextInputSource
    {
        public NowTextInputFrame frame;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    sealed class CountingRenderer : INowNodeGraphRenderer
    {
        public int backgroundCount;
        public int gridCount;
        public int linkCount;
        public int nodeCount;
        public int portCount;
        public int selectionCount;
        public int connectedPortCount;
        public NowNodeGraphNodeContext lastNode;
        public NowNodeGraphPortContext lastPort;
        public NowNodeGraphLinkContext lastLink;

        public void DrawBackground(in NowNodeGraphBackgroundContext context)
        {
            ++backgroundCount;
        }

        public void DrawGrid(in NowNodeGraphGridContext context)
        {
            ++gridCount;
        }

        public void DrawLink(in NowNodeGraphLinkContext context)
        {
            ++linkCount;
            lastLink = context;
        }

        public void DrawNode(in NowNodeGraphNodeContext context)
        {
            ++nodeCount;
            lastNode = context;
        }

        public void DrawPort(in NowNodeGraphPortContext context)
        {
            ++portCount;
            lastPort = context;

            if (context.connected)
                ++connectedPortCount;
        }

        public void DrawSelection(in NowNodeGraphSelectionContext context)
        {
            ++selectionCount;
        }
    }

    static readonly Vector2 Surface = new Vector2(640, 360);
    static readonly NowRect CanvasRect = new NowRect(0, 0, 640, 360);

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowDrawList _drawList;

    static long AllocatedBytesOrIgnore()
    {
        try
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }
        catch (NotImplementedException)
        {
            Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
            return 0;
        }
    }

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowTextInput.Reset();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowTextInput.source = _keyboard;
        _drawList = new NowDrawList();
        NowNodeGraphClipboard.shared.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowContextMenu.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowFocus.Reset();
        NowTextInput.Reset();
        NowInput.Reset();
    }

    NowNodeGraphResult Frame(
        NowNodeGraph graph,
        NowNodeGraphSchema schema = null,
        NowNodeContentRenderer content = null,
        INowNodeGraphRenderer renderer = null,
        NowNodeGraphHistory history = null,
        NowNodeGraphContextMenu contextMenu = null,
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> configure = null)
    {
        NowTextInput.Invalidate();
        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var canvas = NowNodes.Canvas(graph, CanvasRect, "graph");

            if (schema != null)
                canvas = canvas.SetSchema(schema);

            if (content != null)
                canvas = canvas.SetNodeContent(content);

            if (renderer != null)
                canvas = canvas.SetRenderer(renderer);

            if (history != null)
                canvas = canvas.SetHistory(history);

            if (contextMenu != null)
                canvas = canvas.SetContextMenu(contextMenu);

            if (configure != null)
                canvas = configure(canvas);

            var result = canvas.Draw();
            NowOverlay.Flush();
            return result;
        }
    }

    NowNodeGraphResult Frame(NowNodeGraphView view)
    {
        NowTextInput.Invalidate();
        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var result = NowNodes.Canvas(view, CanvasRect, "graph").Draw();
            NowOverlay.Flush();
            return result;
        }
    }

    NowNodeGraphResult Frame(NowNodeGraphView view, NowNodeContentRenderer content)
    {
        NowTextInput.Invalidate();
        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var canvas = NowNodes.Canvas(view, CanvasRect, "graph").SetNodeContent(content);
            var result = canvas.Draw();
            NowOverlay.Flush();
            return result;
        }
    }

    NowNodeGraphResult TransformedFrame(
        NowNodeGraph graph,
        Vector2 scale,
        Vector2 origin,
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> configure = null)
    {
        NowTextInput.Invalidate();
        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        using (Now.Transform(scale, origin))
        {
            var canvas = NowNodes.Canvas(graph, CanvasRect, "transformed-graph");
            if (configure != null)
                canvas = configure(canvas);

            var result = canvas.Draw();
            NowOverlay.Flush();
            return result;
        }
    }

    NowNodeGraphResult PassiveFrame(
        NowNodeGraph graph,
        NowNodeGraphSchema schema = null,
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> configure = null)
    {
        NowTextInput.Invalidate();
        NowOverlay.ForceNewFrame();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowInput.BeginPassive();

            try
            {
                var canvas = NowNodes.Canvas(graph, CanvasRect, "graph");

                if (schema != null)
                    canvas = canvas.SetSchema(schema);

                if (configure != null)
                    canvas = configure(canvas);

                var result = canvas.Draw();
                NowOverlay.Flush();
                return result;
            }
            finally
            {
                NowInput.EndPassive();
            }
        }
    }

    [Test]
    public void AddsTypeCompatibleLink()
    {
        var graph = SampleGraph();

        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("a", graph.links[0].outputNodeId);
        Assert.AreEqual("b", graph.links[0].inputNodeId);
    }

    [Test]
    public void RejectsMismatchedPortTypes()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        a.AddOutput("out", "Out", Float3);
        b.AddInput("in", "In", Float);

        Assert.IsFalse(graph.TryAddLink("a", "out", "b", "in"));
        Assert.AreEqual(0, graph.links.Count);
    }

    [Test]
    public void ReplacesSingleInputLink()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        var c = graph.AddNode("c", "C", Vector2.zero);
        a.AddOutput("out", "Out", Float);
        b.AddOutput("out", "Out", Float);
        c.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("a", "out", "c", "in"));
        Assert.IsTrue(graph.TryAddLink("b", "out", "c", "in"));

        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("b", graph.links[0].outputNodeId);
    }

    [Test]
    public void EnforcesOutputConnectionLimit()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        var output = source.AddOutput("out", "Out", Float);
        output.maxConnections = 1;
        a.AddInput("in", "In", Float);
        b.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("source", "out", "a", "in"));
        Assert.IsFalse(graph.TryAddLink("source", "out", "b", "in"));

        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("a", graph.links[0].inputNodeId);
    }

    [Test]
    public void LinkPreviewRespectsOutputConnectionLimit()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        NowNodePort output = source.AddOutput("out", "Out", Float);
        output.maxConnections = 1;
        a.AddInput("in", "In", Float);
        b.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("source", "out", "a", "in"));
        Assert.IsFalse(graph.TryCreateLink("source", "out", "b", "in", out NowNodeLink blockedLink));
        Assert.IsFalse(graph.CanAddLink(new NowNodeLink("source", "out", "b", "in")));
        Assert.IsFalse(blockedLink.isValid);
    }

    [Test]
    public void LinkPreviewAllowsSingleInputReplacement()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        var sink = graph.AddNode("sink", "Sink", Vector2.zero);
        a.AddOutput("out", "Out", Float);
        b.AddOutput("out", "Out", Float);
        sink.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("a", "out", "sink", "in"));
        Assert.IsTrue(graph.TryCreateLink("b", "out", "sink", "in", out NowNodeLink replacementLink));
        Assert.IsTrue(graph.CanAddLink(replacementLink));
    }

    [Test]
    public void LinkPreviewCanIgnorePickedLimitedOutputLink()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        NowNodePort output = source.AddOutput("out", "Out", Float);
        output.maxConnections = 1;
        a.AddInput("in", "In", Float);
        b.AddInput("in", "In", Float);
        var pickedLink = new NowNodeLink("source", "out", "a", "in");

        Assert.IsTrue(graph.TryAddLink(pickedLink));
        Assert.IsFalse(graph.TryCreateLink("source", "out", "b", "in", out _));
        Assert.IsTrue(graph.TryCreateLink("source", "out", "b", "in", out NowNodeLink replugLink, pickedLink));
        Assert.IsTrue(graph.CanAddLink(replugLink, pickedLink));
    }

    [Test]
    public void NodePortsCanBeUpsertedAndRemovedById()
    {
        var node = new NowNode("node", "Node", Vector2.zero);

        Assert.IsTrue(node.UpsertInput("in", "Old In", Float));
        Assert.IsFalse(node.UpsertInput("in", "Old In", Float));
        Assert.IsTrue(node.UpsertInput("in", "New In", Float4, maxConnections: 2));
        Assert.IsTrue(node.UpsertOutput("out", "Out", Float3, maxConnections: 1));

        Assert.AreEqual(1, node.inputs.Count);
        Assert.AreEqual("New In", node.inputs[0].label);
        Assert.AreEqual(Float4, node.inputs[0].typeId);
        Assert.AreEqual(2, node.inputs[0].maxConnections);
        Assert.AreEqual(1, node.outputs.Count);
        Assert.AreEqual(1, node.outputs[0].maxConnections);

        Assert.IsTrue(node.RemoveInput("in"));
        Assert.IsFalse(node.RemoveInput("in"));
        Assert.IsTrue(node.RemoveOutput("out"));
        Assert.AreEqual(0, node.inputs.Count);
        Assert.AreEqual(0, node.outputs.Count);
    }

    [Test]
    public void AddPortReusesExistingPortId()
    {
        var node = new NowNode("node", "Node", Vector2.zero);
        NowNodePort input = node.AddInput("in", "Old In", Float);
        input.maxConnections = 2;

        NowNodePort updatedInput = node.AddInput("in", "New In", Float4);
        NowNodePort output = node.AddOutput("out", "Old Out", Float);
        NowNodePort updatedOutput = node.AddOutput("out", "New Out", Float3);

        Assert.AreSame(input, updatedInput);
        Assert.AreEqual(1, node.inputs.Count);
        Assert.AreEqual("New In", input.label);
        Assert.AreEqual(Float4, input.typeId);
        Assert.AreEqual(2, input.maxConnections);
        Assert.AreSame(output, updatedOutput);
        Assert.AreEqual(1, node.outputs.Count);
        Assert.AreEqual("New Out", output.label);
        Assert.AreEqual(Float3, output.typeId);
    }

    [Test]
    public void ResetNodeSizesToSchemaRestoresDefinitionSize()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture")
            .SetWidth(220f);
        schema.Node(OutputNode, "Output")
            .SetSize(180f, 92f);

        var graph = new NowNodeGraph();
        var texture = graph.AddNode(schema, TextureNode, Vector2.zero, id: "texture");
        var output = graph.AddNode(schema, OutputNode, Vector2.zero, id: "output");
        texture.size = new Vector2(420f, 260f);
        output.size = new Vector2(320f, 160f);

        Assert.AreEqual(2, graph.ResetNodeSizesToSchema(schema));
        Assert.AreEqual(new Vector2(220f, -1f), texture.size);
        Assert.AreEqual(new Vector2(180f, 92f), output.size);
        Assert.AreEqual(0, graph.ResetNodeSizesToSchema());
    }

    [Test]
    public void NodeDataCanBeStoredByKey()
    {
        var node = new NowNode("node", "Node", Vector2.zero);

        Assert.IsTrue(node.SetData("method", "Game.Api.Score"));
        Assert.IsFalse(node.SetData("method", "Game.Api.Score"));
        Assert.IsTrue(node.SetData("method", "Game.Api.ScoreText"));
        Assert.IsTrue(node.TryGetData("method", out string value));
        Assert.AreEqual("Game.Api.ScoreText", value);
        Assert.AreEqual("Game.Api.ScoreText", node.GetData("method"));
        Assert.AreEqual("fallback", node.GetData("missing", "fallback"));

        Assert.IsTrue(node.RemoveData("method"));
        Assert.IsFalse(node.TryGetData("method", out _));
    }

    [Test]
    public void GraphPortUpsertPrunesInvalidLinks()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var sink = graph.AddNode("sink", "Sink", Vector2.zero);
        source.AddOutput("out", "Out", Float);
        sink.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("source", "out", "sink", "in"));
        Assert.AreEqual(1, graph.links.Count);

        Assert.IsTrue(graph.UpsertNodeInput(sink, "in", "In", Float4));
        Assert.AreEqual(0, graph.links.Count);
    }

    [Test]
    public void GraphPortRemovalDropsPortLinks()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var sink = graph.AddNode("sink", "Sink", Vector2.zero);
        source.AddOutput("out", "Out", Float);
        sink.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("source", "out", "sink", "in"));
        Assert.AreEqual(1, graph.links.Count);

        Assert.IsTrue(graph.RemoveNodeInput(sink, "in"));
        Assert.AreEqual(0, graph.links.Count);
    }

    [Test]
    public void PruneInvalidLinksRemovesStaleDynamicPortLinks()
    {
        var graph = new NowNodeGraph();
        var source = graph.AddNode("source", "Source", Vector2.zero);
        var sink = graph.AddNode("sink", "Sink", Vector2.zero);
        source.AddOutput("out", "Out", Float);
        sink.AddInput("in", "In", Float);

        Assert.IsTrue(graph.TryAddLink("source", "out", "sink", "in"));
        Assert.AreEqual(1, graph.links.Count);

        Assert.IsTrue(sink.UpsertInput("in", "In", Float4));

        Assert.AreEqual(1, graph.PruneInvalidLinks());
        Assert.AreEqual(0, graph.links.Count);
    }

    [Test]
    public void PruneInvalidLinksEnforcesCurrentLimitsAndRemovesDuplicates()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        var sink = graph.AddNode("sink", "Sink", Vector2.zero);
        a.AddOutput("out", "Out", Float);
        b.AddOutput("out", "Out", Float);
        NowNodePort input = sink.AddInput("in", "In", Float);
        input.maxConnections = 0;

        Assert.IsTrue(graph.TryAddLink("a", "out", "sink", "in"));
        Assert.IsTrue(graph.TryAddLink("b", "out", "sink", "in"));
        graph.links.Add(graph.links[0]);
        input.maxConnections = 1;

        Assert.AreEqual(2, graph.PruneInvalidLinks());
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("a", graph.links[0].outputNodeId);
    }

    [Test]
    public void CustomCompatibilityControlsConnections()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", Vector2.zero);
        var b = graph.AddNode("b", "B", Vector2.zero);
        a.AddOutput("out", "Out", Float);
        b.AddInput("in", "In", Float4);

        Assert.IsFalse(graph.TryAddLink("a", "out", "b", "in"));

        graph.canConnect = (_, _, output, _, input) => output.typeId == Float && input.typeId == Float4;

        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        Assert.AreEqual(1, graph.links.Count);
    }

    [Test]
    public void SchemaCreatesNodeDefinitionsWithIntegerIds()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture")
            .SetSize(220f, 140f)
            .Output(ColorPort, "RGBA", Float4);

        var graph = new NowNodeGraph();
        var node = graph.AddNode(schema, TextureNode, new Vector2(12f, 34f), 77, "texture");

        Assert.AreSame(schema, graph.schema);
        Assert.AreEqual(TextureNode, node.kindId);
        Assert.AreEqual(77, node.userId);
        Assert.AreEqual("Texture", node.title);
        Assert.AreEqual(new Vector2(220f, 140f), node.size);
        Assert.AreEqual(NowNodeIds.FromInt(ColorPort), node.outputs[0].id);
        Assert.AreEqual(Float4, node.outputs[0].typeId);
        Assert.IsTrue(graph.TryFindPort("texture", ColorPort, NowNodePortDirection.Output, out _, out var port));
        Assert.AreSame(node.outputs[0], port);
    }

    [Test]
    public void DefinitionPortsUpsertByPortIdAndCanBeCleared()
    {
        var schema = new NowNodeGraphSchema();
        var definition = schema.Node(TextureNode, "Texture")
            .Input(ValuePort, "Old In", Float)
            .Input(ValuePort, "New In", Float4, maxConnections: 2)
            .Output(ColorPort, "Old Out", Float)
            .Output(ColorPort, "New Out", Float4, maxConnections: 3);

        Assert.AreEqual(1, definition.inputs.Count);
        Assert.AreEqual(1, definition.outputs.Count);
        Assert.AreEqual("New In", definition.inputs[0].label);
        Assert.AreEqual(Float4, definition.inputs[0].typeId);
        Assert.AreEqual(2, definition.inputs[0].maxConnections);
        Assert.AreEqual("New Out", definition.outputs[0].label);
        Assert.AreEqual(Float4, definition.outputs[0].typeId);
        Assert.AreEqual(3, definition.outputs[0].maxConnections);

        Assert.AreSame(definition, definition.ClearPorts());
        Assert.AreEqual(0, definition.inputs.Count);
        Assert.AreEqual(0, definition.outputs.Count);
    }

    [Test]
    public void DefinitionPortsCanBeRemovedFluently()
    {
        var schema = new NowNodeGraphSchema();
        var definition = schema.Node(TextureNode, "Texture")
            .Input(ValuePort, "Value", Float)
            .Input("mask", "Mask", Float4)
            .Output(ColorPort, "Color", Float4)
            .Output("preview", "Preview", Float);

        Assert.AreSame(definition, definition.RemoveInput(ValuePort));
        Assert.AreSame(definition, definition.RemoveOutput("preview"));
        Assert.AreSame(definition, definition.RemoveInput("missing"));

        var graph = new NowNodeGraph();
        NowNode node = graph.AddNode(schema, TextureNode, Vector2.zero, id: "texture");

        Assert.AreEqual(1, definition.inputs.Count);
        Assert.AreEqual("mask", definition.inputs[0].id);
        Assert.AreEqual(1, definition.outputs.Count);
        Assert.AreEqual(NowNodeIds.FromInt(ColorPort), definition.outputs[0].id);
        Assert.AreEqual(1, node.inputs.Count);
        Assert.AreEqual("mask", node.inputs[0].id);
        Assert.AreEqual(1, node.outputs.Count);
        Assert.AreEqual(NowNodeIds.FromInt(ColorPort), node.outputs[0].id);
    }

    [Test]
    public void DefinitionInitializerRunsAfterPortsAreApplied()
    {
        bool sawAppliedPort = false;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture")
            .Output(ColorPort, "RGBA", Float4)
            .Initialize(node =>
            {
                sawAppliedPort = node.outputs.Count == 1 && node.outputs[0].id == NowNodeIds.FromInt(ColorPort);
                node.title = "Initialized";
                node.userData = "created-from-definition";
            });

        var graph = new NowNodeGraph();
        var created = graph.AddNode(schema, TextureNode, Vector2.zero);

        Assert.IsTrue(sawAppliedPort);
        Assert.AreEqual("Initialized", created.title);
        Assert.AreEqual("created-from-definition", created.userData);
    }

    [Test]
    public void SchemaConnectionRulesCanBridgeDifferentTypes()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Float").Output(ValuePort, "Value", Float);
        schema.Node(OutputNode, "Output").Input(ColorPort, "Color", Float4);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, Vector2.zero, id: "float");
        graph.AddNode(schema, OutputNode, Vector2.zero, id: "output");

        Assert.IsFalse(graph.TryAddLink("float", ValuePort, "output", ColorPort));

        schema.Allow(Float, Float4);

        Assert.IsTrue(graph.TryAddLink("float", ValuePort, "output", ColorPort));
        Assert.AreEqual(1, graph.links.Count);
    }

    [Test]
    public void SchemaClearRemovesDefinitionsRulesAndTypeColors()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").Output(ValuePort, "Value", Float);
        schema.Allow(Float, Float4);
        schema.TypeColor(Float, Color.red);

        var output = new NowNodePort("out", "Out", NowNodePortDirection.Output, Float);
        var input = new NowNodePort("in", "In", NowNodePortDirection.Input, Float4);

        Assert.AreEqual(1, schema.nodeDefinitionCount);
        Assert.IsTrue(schema.TryGetNode(TextureNode, out _));
        Assert.IsTrue(schema.TryGetTypeColor(Float, out _));
        Assert.IsTrue(schema.CanConnect(null, null, output, null, input));

        Assert.AreSame(schema, schema.Clear());

        Assert.AreEqual(0, schema.nodeDefinitionCount);
        Assert.IsFalse(schema.TryGetNode(TextureNode, out _));
        Assert.IsFalse(schema.TryGetTypeColor(Float, out _));
        Assert.IsFalse(schema.CanConnect(null, null, output, null, input));

        schema.Node(TextureNode, "Texture").Output(ColorPort, "Color", Float4);

        Assert.AreEqual(1, schema.nodeDefinitionCount);
        Assert.IsTrue(schema.TryGetNode(TextureNode, out var rebuilt));
        Assert.AreEqual(1, rebuilt.outputs.Count);
        Assert.AreEqual(NowNodeIds.FromInt(ColorPort), rebuilt.outputs[0].id);
    }

    [Test]
    public void GraphClearRemovesNodesLinksAndSelection()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        graph.SelectAllNodes();

        Assert.AreEqual(2, graph.nodes.Count);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual(2, graph.SelectedNodeCount());

        graph.Clear();

        Assert.AreEqual(0, graph.nodes.Count);
        Assert.AreEqual(0, graph.links.Count);
        Assert.AreEqual(0, graph.SelectedNodeCount());
        Assert.IsFalse(graph.HasSelectedLink());

        var linkedGraph = SampleGraph();
        Assert.IsTrue(linkedGraph.TryAddLink("a", "out", "b", "in"));
        linkedGraph.SelectLink(linkedGraph.links[0]);
        Assert.IsTrue(linkedGraph.HasSelectedLink());

        linkedGraph.Clear();

        Assert.IsFalse(linkedGraph.HasSelectedLink());
    }

    [Test]
    public void CanvasUsesSchemaRendererWithContentContext()
    {
        int renderCount = 0;
        NowNodeContentContext seen = null;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 118f)
            .Render(ctx =>
            {
                ++renderCount;
                seen = ctx;
                ctx.MarkChanged();
            });

        var graph = new NowNodeGraph();
        var node = graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), 4, "preview");

        var result = Frame(graph, schema);

        Assert.IsTrue(result.changed);
        Assert.AreEqual(1, renderCount);
        Assert.AreSame(graph, seen.graph);
        Assert.AreSame(schema, seen.schema);
        Assert.AreSame(node, seen.node);
        Assert.AreEqual(TextureNode, seen.node.kindId);
        Assert.IsFalse(seen.bodyRect.isEmpty);
    }

    [Test]
    public void CanvasDrawIsAllocationFreeAfterWarmup()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 118f)
            .Output(ColorPort, "RGBA", Float4)
            .Render(ctx =>
            {
                Now.Rectangle(ctx.Row(0, 12f))
                    .SetColor(Color.white)
                    .Draw();
            });
        schema.Node(OutputNode, "Output")
            .SetSize(180f, 94f)
            .Input(ValuePort, "Base", Float4);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "texture");
        graph.AddNode(schema, OutputNode, new Vector2(320f, 30f), id: "output");
        graph.TryAddLink("texture", ColorPort, "output", ValuePort);

        Frame(graph, schema);
        Frame(graph, schema);
        Frame(graph, schema);

        long before = AllocatedBytesOrIgnore();
        Frame(graph, schema);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0, allocated, "steady-state node graph draw must not allocate after warmup");
    }

    [Test]
    public void CustomRendererReceivesFrameworkDrawCalls()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var renderer = new CountingRenderer();

        Frame(graph, renderer: renderer);

        Assert.AreEqual(1, renderer.backgroundCount);
        Assert.AreEqual(1, renderer.gridCount);
        Assert.AreEqual(1, renderer.linkCount);
        Assert.AreEqual(2, renderer.nodeCount);
        Assert.AreEqual(2, renderer.portCount);
        Assert.AreSame(graph, renderer.lastNode.graph);
        Assert.AreSame(graph, renderer.lastPort.graph);
        Assert.AreEqual("b", renderer.lastNode.node.id);
    }

    [Test]
    public void GridSnapRoundsNodeSizeToGrid()
    {
        var graph = SampleGraph();
        var renderer = new CountingRenderer();

        Frame(graph, renderer: renderer);

        Assert.AreEqual(new Vector2(192f, 120f), renderer.lastNode.nodeRect.size);
    }

    [Test]
    public void AlignmentSnapKeepsAuthoredNodeSize()
    {
        var graph = SampleGraph();
        var renderer = new CountingRenderer();

        Frame(graph, renderer: renderer, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.AreEqual(new Vector2(180f, 118f), renderer.lastNode.nodeRect.size);
    }

    [Test]
    public void ViewProvidesRendererToCanvas()
    {
        var graph = SampleGraph();
        var renderer = new CountingRenderer();
        var view = new NowNodeGraphView(graph).SetRenderer(renderer);

        Frame(view);

        Assert.AreEqual(2, renderer.nodeCount);
        Assert.AreEqual(2, renderer.portCount);
    }

    [Test]
    public void CanvasDrawsGeometry()
    {
        var graph = SampleGraph();

        Frame(graph);

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void CanvasCullsBuiltInNodesOutsideTheViewport()
    {
        var graph = new NowNodeGraph();
        var node = graph.AddNode("outside", "Outside", new Vector2(900f, 700f));
        node.size = new Vector2(180f, 118f);
        node.AddOutput("out", "Out", Float);

        Frame(graph, configure: canvas => canvas.SetBackground(false).SetGrid(false));

        Assert.IsFalse(_drawList.hasGeometry);
    }

    [Test]
    public void CanvasKeepsBuiltInLinksThatCrossTheViewport()
    {
        var graph = new NowNodeGraph();
        var left = graph.AddNode("left", "Left", new Vector2(-420f, 100f));
        var right = graph.AddNode("right", "Right", new Vector2(820f, 100f));
        left.size = new Vector2(180f, 118f);
        right.size = new Vector2(180f, 118f);
        left.AddOutput("out", "Out", Float);
        right.AddInput("in", "In", Float);
        Assert.IsTrue(graph.TryAddLink("left", "out", "right", "in"));

        Frame(graph, configure: canvas => canvas.SetBackground(false).SetGrid(false));

        Assert.IsTrue(_drawList.hasGeometry, "A Bezier crossing the viewport must survive endpoint culling.");
    }

    [Test]
    public void CustomRendererStillReceivesOffscreenNodes()
    {
        var graph = new NowNodeGraph();
        var node = graph.AddNode("outside", "Outside", new Vector2(900f, 700f));
        node.size = new Vector2(180f, 118f);
        node.AddOutput("out", "Out", Float);
        var renderer = new CountingRenderer();

        Frame(
            graph,
            renderer: renderer,
            configure: canvas => canvas.SetBackground(false).SetGrid(false));

        Assert.AreEqual(1, renderer.nodeCount);
        Assert.AreEqual(1, renderer.portCount);
    }

    [Test]
    public void DraggingNodeMovesPosition()
    {
        var graph = SampleGraph();
        Frame(graph, configure: canvas => canvas.SetNodeSnapping(false));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapping(false));
        int capturedId = NowInput.activeId;
        Assert.AreNotEqual(0, capturedId, "pressing a node must capture the primary pointer");

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), new Vector2(40f, 20f), true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapping(false));
        Assert.AreEqual(capturedId, NowInput.activeId, "node pointer capture must survive into the drag frame");

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), false, false, true);
        Frame(graph, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(80f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeTracksPointerInsideParentTransform()
    {
        var graph = SampleGraph();
        var scale = new Vector2(1.5f, 1.25f);
        var origin = new Vector2(30f, 20f);
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> freeMove = canvas => canvas.SetNodeSnapping(false);
        TransformedFrame(graph, scale, origin, freeMove);

        var startLocal = new Vector2(70f, 45f);
        var endLocal = new Vector2(110f, 65f);
        var startScreen = Vector2.Scale(startLocal, scale) + origin;
        var endScreen = Vector2.Scale(endLocal, scale) + origin;

        _pointer.snapshot = new NowInputSnapshot(startScreen, true, true, false);
        TransformedFrame(graph, scale, origin, freeMove);

        _pointer.snapshot = new NowInputSnapshot(endScreen, endScreen - startScreen, true, false, false);
        var result = TransformedFrame(graph, scale, origin, freeMove);

        _pointer.snapshot = new NowInputSnapshot(endScreen, false, false, true);
        TransformedFrame(graph, scale, origin, freeMove);

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(80f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeBodyBelowControlRowsMovesPosition()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Body Drag")
            .SetSize(220f, 180f)
            .Render(ctx =>
            {
                Now.Button(ctx.Row(0, 24f), "Edit").SetId("body-control").Draw();
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "a");

        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        var start = new Vector2(120f, 145f);
        var end = new Vector2(150f, 165f);
        _pointer.snapshot = new NowInputSnapshot(start, true, true, false);
        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        _pointer.snapshot = new NowInputSnapshot(end, end - start, true, false, false);
        var result = Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(70f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeSnapsToGridByDefault()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), new Vector2(40f, 20f), true, false, false);
        var result = Frame(graph);

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(72f, 48f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeGridSnapUsesGraphUnitsWithZoom()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(
            true,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            false,
            new Vector2(0f, -6f),
            1,
            1f);
        Frame(graph);

        float zoom = Mathf.Pow(NowNodeGraphStyle.Default(NowTheme.themeAsset).zoomStep, -6f);
        var startGraph = new Vector2(70f, 45f);
        var dragGraph = new Vector2(110f, 65f);

        _pointer.snapshot = new NowInputSnapshot(startGraph * zoom, true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(dragGraph * zoom, (dragGraph - startGraph) * zoom, true, false, false);
        var result = Frame(graph);

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(72f, 48f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeSnapsToNearbyNodeAlignment()
    {
        var graph = SampleGraph();
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(347f, 65f), new Vector2(277f, 20f), true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(320f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeReleasesSnapWhenPointerMovesPastThreshold()
    {
        var graph = SampleGraph();
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(347f, 65f), new Vector2(277f, 20f), true, false, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
        Assert.AreEqual(new Vector2(320f, 50f), graph.FindNode("a").position);

        for (int x = 348; x <= 362; ++x)
        {
            _pointer.snapshot = new NowInputSnapshot(new Vector2(x, 65f), new Vector2(1f, 0f), true, false, false);
            Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
            Assert.AreEqual(new Vector2(320f, 50f), graph.FindNode("a").position);
        }

        _pointer.snapshot = new NowInputSnapshot(new Vector2(363f, 65f), new Vector2(1f, 0f), true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(333f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeSnapReleaseUsesGraphDistanceWithZoom()
    {
        var graph = SampleGraph();
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(
            true,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            false,
            false,
            false,
            new Vector2(0f, -6f),
            1,
            1f);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        float zoom = Mathf.Pow(NowNodeGraphStyle.Default(NowTheme.themeAsset).zoomStep, -6f);
        var startGraph = new Vector2(70f, 45f);
        var snapGraph = new Vector2(347f, 65f);
        var stillSnappedGraph = new Vector2(362f, 65f);
        var releasedGraph = new Vector2(363f, 65f);

        _pointer.snapshot = new NowInputSnapshot(startGraph * zoom, true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(snapGraph * zoom, (snapGraph - startGraph) * zoom, true, false, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
        Assert.AreEqual(new Vector2(320f, 50f), graph.FindNode("a").position);

        _pointer.snapshot = new NowInputSnapshot(stillSnappedGraph * zoom, (stillSnappedGraph - snapGraph) * zoom, true, false, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
        Assert.AreEqual(new Vector2(320f, 50f), graph.FindNode("a").position);

        _pointer.snapshot = new NowInputSnapshot(releasedGraph * zoom, (releasedGraph - stillSnappedGraph) * zoom, true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(333f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeIgnoresFarAwayAlignment()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", new Vector2(40f, 30f));
        var far = graph.AddNode("far", "Far", new Vector2(320f, 800f));
        a.size = new Vector2(180f, 118f);
        far.size = new Vector2(180f, 118f);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(347f, 65f), new Vector2(277f, 20f), true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(317f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingNodeDrawsSnapGuide()
    {
        var graph = SampleGraph();
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
        int withoutGuideVertices = _drawList.mesh.vertexCount;

        _pointer.snapshot = new NowInputSnapshot(new Vector2(347f, 65f), new Vector2(277f, 20f), true, false, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));
        int withGuideVertices = _drawList.mesh.vertexCount;

        Assert.Greater(withGuideVertices, withoutGuideVertices);
    }

    [Test]
    public void HoldingOptionWhileDraggingBypassesNodeSnapping()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);

        NowNodeGraphResult result;

        try
        {
            _keyboard.frame = new NowTextInputFrame { option = true };
            _pointer.snapshot = new NowInputSnapshot(new Vector2(347f, 65f), new Vector2(277f, 20f), true, false, false);
            result = Frame(graph);
        }
        finally
        {
            _keyboard.frame = default;
        }

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(317f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingSelectedNodesSnapsSelectionBounds()
    {
        var graph = SampleGraph();
        var c = graph.AddNode("c", "C", new Vector2(610f, 30f));
        c.size = new Vector2(180f, 118f);
        graph.SelectNode("a");
        graph.AddNodeToSelection("b");
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(177f, 65f), new Vector2(107f, 20f), true, false, false);
        var result = Frame(graph, configure: canvas => canvas.SetNodeSnapMode(NowNodeSnapMode.Align));

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(150f, 50f), graph.FindNode("a").position);
        Assert.AreEqual(new Vector2(430f, 50f), graph.FindNode("b").position);
    }

    [Test]
    public void DraggingOutputToInputCreatesLink()
    {
        var graph = SampleGraph();
        Frame(graph);

        var output = new Vector2(232f, 80f);
        var input = new Vector2(320f, 80f);

        _pointer.snapshot = new NowInputSnapshot(output, true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(input, input - output, true, false, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(input, false, false, true);
        var result = Frame(graph);

        Assert.IsTrue(result.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("a", graph.links[0].outputNodeId);
        Assert.AreEqual("b", graph.links[0].inputNodeId);
    }

    [Test]
    public void ContentRectAvoidsPortLabelLane()
    {
        NowNodeContentContext seen = null;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(210f, 152f)
            .Output(ColorPort, "RGBA", Float4)
            .Render(ctx => seen = ctx);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "preview");

        Frame(graph, schema);

        Assert.NotNull(seen);
        Assert.Greater(seen.bodyRect.x, seen.nodeRect.x);
        Assert.LessOrEqual(seen.bodyRect.xMax, seen.nodeRect.xMax - 72f);
    }

    [Test]
    public void PortTopOffsetMovesPortsBelowCustomContentAndExpandsNode()
    {
        const float offset = 56f;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Offset")
            .SetSize(180f, 80f)
            .SetPortTopOffset(offset)
            .Input(ValuePort, "In", Float)
            .Output(ColorPort, "Out", Float);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "offset");
        var renderer = new CountingRenderer();

        Frame(graph, schema, renderer: renderer, configure: canvas => canvas.SetNodeSnapping(false));

        float expectedHeight = 30f + 12f + offset + 24f;
        float expectedPortY = 30f + 30f + 8f + offset + 12f;

        Assert.AreEqual(expectedHeight, renderer.lastNode.nodeRect.height, 0.0001f);
        Assert.AreEqual(expectedPortY, renderer.lastPort.center.y, 0.0001f);
    }

    [Test]
    public void WidthAndContentHeightLetNodeUseComputedMinimumHeight()
    {
        const float contentHeight = 10f;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Auto Height")
            .SetWidth(180f)
            .SetContentHeight(contentHeight)
            .Input(ValuePort, "In", Float)
            .Output(ColorPort, "Out", Float);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "auto-height");
        var renderer = new CountingRenderer();

        Frame(graph, schema, renderer: renderer, configure: canvas => canvas.SetNodeSnapping(false));

        float expectedHeight = 30f + 12f + contentHeight + 24f;
        float expectedPortY = 30f + 30f + 8f + contentHeight + 12f;

        Assert.Less(expectedHeight, graph.defaultNodeSize.y);
        Assert.AreEqual(expectedHeight, renderer.lastNode.nodeRect.height, 0.0001f);
        Assert.AreEqual(expectedPortY, renderer.lastPort.center.y, 0.0001f);
    }

    [Test]
    public void ContentHeightClampsBodyRectAbovePortRows()
    {
        const float contentHeight = 32f;
        NowNodeContentContext seen = null;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Clamped Content")
            .SetWidth(180f)
            .SetContentHeight(contentHeight)
            .Input(ValuePort, "In", Float)
            .Output(ColorPort, "Out", Float)
            .Render(ctx => seen = ctx);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "content");
        var renderer = new CountingRenderer();

        Frame(graph, schema, renderer: renderer, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.NotNull(seen);
        Assert.AreEqual(contentHeight, seen.bodyRect.height, 0.0001f);
        Assert.Less(seen.bodyRect.yMax, renderer.lastPort.center.y);
    }

    [Test]
    public void PortTopOffsetAloneDoesNotClampBodyRect()
    {
        const float offset = 32f;
        NowNodeContentContext seen = null;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Port Offset")
            .SetSize(180f, 160f)
            .SetPortTopOffset(offset)
            .Input(ValuePort, "In", Float)
            .Render(ctx => seen = ctx);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "offset");

        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.NotNull(seen);
        Assert.Greater(seen.bodyRect.height, offset);
    }

    [Test]
    public void ContentRowsStayInGraphSpaceWithCanvasZoom()
    {
        float seenZoom = 0f;
        float seenHeight = 0f;
        float seenControlScale = 0f;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 118f)
            .Render(ctx =>
            {
                seenZoom = ctx.zoom;
                seenControlScale = Now.currentTransform.scale.x;
                seenHeight = ctx.Row(0, 20f).height;
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "preview");

        Frame(graph, schema);
        Assert.AreEqual(1f, seenZoom, 0.0001f);
        Assert.AreEqual(seenZoom, seenControlScale, 0.0001f);
        Assert.AreEqual(20f, seenHeight, 0.0001f);
        Assert.AreEqual(1f, Now.currentTransform.scale.x, 0.0001f);

        _pointer.snapshot = new NowInputSnapshot(
            true,
            new Vector2(100f, 100f),
            new Vector2(100f, 100f),
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            new Vector2(0f, 3f),
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            1,
            1f);
        Frame(graph, schema);

        Assert.Greater(seenZoom, 1f);
        Assert.AreEqual(seenZoom, seenControlScale, 0.0001f);
        Assert.AreEqual(20f, seenHeight, 0.0001f);
        Assert.AreEqual(1f, Now.currentTransform.scale.x, 0.0001f);
    }

    [Test]
    public void ContentRowsCanStackMixedHeights()
    {
        NowRect field = default;
        NowRect status = default;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Mixed Rows")
            .SetSize(220f, 118f)
            .Render(ctx =>
            {
                field = ctx.Row(0, 28f);
                status = ctx.RowAfter(field, 18f);
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "mixed");

        Frame(graph, schema);

        Assert.AreEqual(field.yMax + 4f, status.y, 0.0001f);
        Assert.GreaterOrEqual(status.y, field.yMax);
    }

    [Test]
    public void FullWidthContentRowSpansPortLabelLanes()
    {
        NowRect regular = default;
        NowRect full = default;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Full Width")
            .SetWidth(220f)
            .SetContentHeight(28f)
            .Input(ValuePort, "Input", Float)
            .Output(ColorPort, "Output", Float)
            .Render(ctx =>
            {
                regular = ctx.Row(0, 28f);
                full = ctx.FullWidthRow(0, 28f);
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "full-width");
        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.Less(full.x, regular.x);
        Assert.Greater(full.xMax, regular.xMax);
        Assert.AreEqual(regular.y, full.y, 0.0001f);
        Assert.AreEqual(regular.height, full.height, 0.0001f);
    }

    [Test]
    public void FullWidthContentControlDoesNotStartNodeDrag()
    {
        bool clicked = false;
        NowRect buttonRect = default;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Editable")
            .SetWidth(220f)
            .SetContentHeight(28f)
            .Input(ValuePort, "Input", Float)
            .Output(ColorPort, "Output", Float)
            .Render(ctx =>
            {
                buttonRect = ctx.FullWidthRow(0, 28f);
                if (Now.Button(buttonRect, "Edit").SetId("full-width-edit").Draw())
                    clicked = true;
            });

        var graph = new NowNodeGraph();
        var node = graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "editable");
        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));
        var pointer = new Vector2(buttonRect.x + 5f, buttonRect.center.y);

        _pointer.snapshot = new NowInputSnapshot(pointer, true, true, false);
        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));
        _pointer.snapshot = new NowInputSnapshot(pointer, false, false, true);
        Frame(graph, schema, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.IsTrue(clicked);
        Assert.AreEqual(new Vector2(40f, 30f), node.position);
    }

    [Test]
    public void ContentContextConvertsBetweenGraphAndScreenSpace()
    {
        float seenZoom = 0f;
        NowRect graphRow = default;
        NowRect screenRow = default;
        NowRect transformedRow = default;
        Vector2 roundTripCenter = default;
        Vector2 graphVector = default;
        float graphUnits = 0f;

        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 118f)
            .Render(ctx =>
            {
                seenZoom = ctx.zoom;
                graphRow = ctx.Row(0, 20f);
                screenRow = ctx.GraphToScreen(graphRow);
                transformedRow = Now.TransformScreenRect(graphRow);
                roundTripCenter = ctx.ScreenToGraph(screenRow.center);
                graphVector = ctx.ScreenVectorToGraph(ctx.GraphVectorToScreen(new Vector2(3f, 4f)));
                graphUnits = ctx.ScreenUnitsToGraph(ctx.GraphUnitsToScreen(12f));
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "preview");

        Frame(graph, schema);

        _pointer.snapshot = new NowInputSnapshot(
            true,
            new Vector2(100f, 100f),
            new Vector2(100f, 100f),
            Vector2.zero,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.None,
            new Vector2(0f, 3f),
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            1,
            1f);
        Frame(graph, schema);

        Assert.Greater(seenZoom, 1f);
        Assert.AreEqual(transformedRow.x, screenRow.x, 0.0001f);
        Assert.AreEqual(transformedRow.y, screenRow.y, 0.0001f);
        Assert.AreEqual(transformedRow.width, screenRow.width, 0.0001f);
        Assert.AreEqual(transformedRow.height, screenRow.height, 0.0001f);
        Assert.AreEqual(graphRow.center.x, roundTripCenter.x, 0.0001f);
        Assert.AreEqual(graphRow.center.y, roundTripCenter.y, 0.0001f);
        Assert.AreEqual(3f, graphVector.x, 0.0001f);
        Assert.AreEqual(4f, graphVector.y, 0.0001f);
        Assert.AreEqual(12f, graphUnits, 0.0001f);
        Assert.AreEqual(20f * seenZoom, screenRow.height, 0.0001f);
    }

    [Test]
    public void ContentContextScreenConversionIncludesParentTransform()
    {
        NowRect graphRow = default;
        NowRect screenRow = default;
        Vector2 roundTrip = default;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Transformed Content")
            .SetSize(180f, 118f)
            .Render(ctx =>
            {
                graphRow = ctx.Row(0, 20f);
                screenRow = ctx.GraphToScreen(graphRow);
                roundTrip = ctx.ScreenToGraph(screenRow.center);
            });

        var graph = new NowNodeGraph().SetSchema(schema);
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "transformed-content");
        var scale = new Vector2(1.5f, 1.25f);
        var origin = new Vector2(30f, 20f);
        TransformedFrame(graph, scale, origin);

        Vector2 expectedCenter = Vector2.Scale(graphRow.center, scale) + origin;
        Assert.AreEqual(expectedCenter.x, screenRow.center.x, 0.0001f);
        Assert.AreEqual(expectedCenter.y, screenRow.center.y, 0.0001f);
        Assert.AreEqual(graphRow.center.x, roundTrip.x, 0.0001f);
        Assert.AreEqual(graphRow.center.y, roundTrip.y, 0.0001f);
    }

    [Test]
    public void NodeBodyControlsReceivePointerInput()
    {
        bool clicked = false;
        NowRect buttonRect = default;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Control")
            .SetSize(190f, 118f)
            .Render(ctx =>
            {
                buttonRect = ctx.Row(0, 24f);

                if (Now.Button(buttonRect, "Edit").SetId("node-body-button").Draw())
                {
                    clicked = true;
                    ctx.MarkChanged();
                }
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "control");

        Frame(graph, schema);
        Assert.IsFalse(buttonRect.isEmpty);

        _pointer.snapshot = new NowInputSnapshot(buttonRect.center, true, true, false);
        Frame(graph, schema);

        _pointer.snapshot = new NowInputSnapshot(buttonRect.center, false, false, true);
        var result = Frame(graph, schema);

        Assert.IsTrue(clicked);
        Assert.IsTrue(result.changed);
    }

    [Test]
    public void NodeBodyContentCanRecordUndoableChanges()
    {
        bool changedOnce = false;
        var history = new NowNodeGraphHistory();
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Editable")
            .SetSize(190f, 118f)
            .Render(ctx =>
            {
                if (changedOnce)
                    return;

                ctx.RecordChange();
                ctx.node.userId = 42;
                changedOnce = true;
            });

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), userId: 7, id: "editable");

        var result = Frame(graph, schema, history: history);

        Assert.IsTrue(result.changed);
        Assert.AreEqual(42, graph.FindNode("editable").userId);
        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(7, graph.FindNode("editable").userId);
    }

    [Test]
    public void DragSelectSelectsOverlappingNodes()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(260f, 170f), new Vector2(250f, 160f), true, false, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(260f, 170f), false, false, true);
        var result = Frame(graph);

        Assert.IsTrue(result.dragSelected);
        Assert.IsTrue(result.selectionChanged);
        Assert.AreEqual(1, graph.SelectedNodeCount());
        Assert.IsTrue(graph.IsNodeSelected("a"));
        Assert.IsFalse(graph.IsNodeSelected("b"));
    }

    [Test]
    public void HistoryRestoresNodeMovement()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();

        history.Record(graph);
        graph.FindNode("a").position += new Vector2(24f, 12f);

        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(new Vector2(40f, 30f), graph.FindNode("a").position);

        Assert.IsTrue(history.Redo(graph));
        Assert.AreEqual(new Vector2(64f, 42f), graph.FindNode("a").position);
    }

    [Test]
    public void CanvasNodeDragRecordsHistory()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history, configure: canvas => canvas.SetNodeSnapping(false));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history, configure: canvas => canvas.SetNodeSnapping(false));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), new Vector2(40f, 20f), true, false, false);
        Frame(graph, history: history, configure: canvas => canvas.SetNodeSnapping(false));

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), false, false, true);
        Frame(graph, history: history, configure: canvas => canvas.SetNodeSnapping(false));

        Assert.IsTrue(history.canUndo);
        Assert.AreEqual(new Vector2(80f, 50f), graph.FindNode("a").position);
        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(new Vector2(40f, 30f), graph.FindNode("a").position);
    }

    [Test]
    public void CanvasKeyboardUndoRedoUsesTextInputShortcuts()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, history: history);

        history.Record(graph);
        graph.FindNode("a").position = new Vector2(80f, 50f);

        _keyboard.frame = new NowTextInputFrame { undoPressed = true, command = true };
        var undo = Frame(graph, history: history);

        Assert.IsTrue(undo.undo);
        Assert.AreEqual(new Vector2(40f, 30f), graph.FindNode("a").position);

        _keyboard.frame = new NowTextInputFrame { redoPressed = true, command = true };
        var redo = Frame(graph, history: history);

        Assert.IsTrue(redo.redo);
        Assert.AreEqual(new Vector2(80f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void CanvasDeleteKeyDeletesSelectedNodeWithoutHistory()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph);

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        _pointer.snapshot = default;
        var result = Frame(graph);

        Assert.IsTrue(result.nodesDeleted);
        Assert.IsTrue(result.selectionChanged);
        Assert.IsNull(graph.FindNode("a"));
        Assert.AreEqual(1, graph.nodes.Count);
    }

    [Test]
    public void CanvasDeleteKeyRecordsHistory()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph, history: history);

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        _pointer.snapshot = default;
        var result = Frame(graph, history: history);

        Assert.IsTrue(result.nodesDeleted);
        Assert.IsTrue(history.canUndo);
        Assert.IsNull(graph.FindNode("a"));
        Assert.IsTrue(history.Undo(graph));
        Assert.NotNull(graph.FindNode("a"));
    }

    [Test]
    public void CanvasDeleteKeyDoesNotDeleteWhileOverlayIsOpen()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph, history: history);

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowOverlay.Defer(new NowRect(0f, 0f, 120f, 120f), () => { });
            NowOverlay.Flush();
        }

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        _pointer.snapshot = default;
        var result = Frame(graph, history: history);

        Assert.IsFalse(result.nodesDeleted);
        Assert.NotNull(graph.FindNode("a"));
    }

    [Test]
    public void ContextMenuCanOpenFromCanvas()
    {
        var graph = SampleGraph();
        var menu = new NowNodeGraphContextMenu();
        Frame(graph, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(10f, 10f),
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None);
        Frame(graph, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(10f, 10f),
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.Secondary);
        var result = Frame(graph, contextMenu: menu);

        Assert.IsTrue(result.contextMenuOpened);
    }

    [Test]
    public void ContextMenuCreatesSchemaNodesAtGraphPosition()
    {
        var graph = new NowNodeGraph();
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture")
            .SetSize(220f, 140f)
            .Output(ColorPort, "RGBA", Float4);

        var menu = new NowNodeGraphContextMenu
        {
            undoRedo = false,
            deleteSelection = false,
            clipboardItems = false
        };
        var anchor = new Vector2(120f, 90f);

        Frame(graph, schema: schema, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            anchor,
            NowPointerButtons.Secondary,
            NowPointerButtons.Secondary,
            NowPointerButtons.None);
        Frame(graph, schema: schema, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            anchor,
            NowPointerButtons.None,
            NowPointerButtons.None,
            NowPointerButtons.Secondary);
        var opened = Frame(graph, schema: schema, contextMenu: menu);

        Assert.IsTrue(opened.contextMenuOpened);
        Assert.AreEqual(anchor, opened.contextMenuGraphPosition);

        var styles = NowTheme.themeAsset.controlStyles;
        float rootWidth = Mathf.Max(160f, styles.contextMenuMinWidth);
        var submenuPoint = new Vector2(
            anchor.x + styles.popupPadding + 10f,
            anchor.y + styles.popupPadding + styles.contextMenuItemHeight * 0.5f);
        var itemPoint = new Vector2(
            anchor.x + rootWidth + 10f,
            submenuPoint.y);

        _pointer.snapshot = new NowInputSnapshot(submenuPoint, false, false, false);
        Frame(graph, schema: schema, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(itemPoint, true, true, false);
        Frame(graph, schema: schema, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(itemPoint, false, false, true);
        Frame(graph, schema: schema, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(itemPoint, false, false, false);
        var created = Frame(graph, schema: schema, contextMenu: menu);

        Assert.IsTrue(created.changed);
        Assert.IsTrue(created.selectionChanged);
        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
        Assert.AreEqual(anchor, graph.nodes[0].position);
        Assert.AreEqual(graph.nodes[0].id, graph.selectedNodeId);
    }

    [Test]
    public void CanvasDrawsOffsetLinkGeometry()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowNodes.Canvas(graph, new NowRect(24f, 24f, 500f, 280f), "offset-graph")
                .SetGrid(false)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void PickingUpOccupiedInputLinkRewiresIt()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        var inputB = new Vector2(320f, 80f);
        var inputC = new Vector2(320f, 230f);

        _pointer.snapshot = new NowInputSnapshot(inputB, true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(inputC, inputC - inputB, true, false, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(inputC, false, false, true);
        var result = Frame(graph, history: history);

        Assert.IsTrue(result.linkRemoved);
        Assert.IsTrue(result.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual(new NowNodeLink("a", "out", "c", "in"), graph.links[0]);

        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual(new NowNodeLink("a", "out", "b", "in"), graph.links[0]);
    }

    [Test]
    public void DroppingPickedLinkOutsideUnplugsIt()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        var inputB = new Vector2(320f, 80f);
        var empty = new Vector2(520f, 320f);

        _pointer.snapshot = new NowInputSnapshot(inputB, true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(empty, empty - inputB, true, false, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(empty, false, false, true);
        var result = Frame(graph, history: history);

        Assert.IsTrue(result.linkRemoved);
        Assert.IsFalse(result.linkCreated);
        Assert.AreEqual(0, graph.links.Count);

        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(1, graph.links.Count);
    }

    [Test]
    public void ReplugToSamePortKeepsLinkWithoutHistory()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        var inputB = new Vector2(320f, 80f);
        var away = new Vector2(420f, 150f);

        _pointer.snapshot = new NowInputSnapshot(inputB, true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(away, away - inputB, true, false, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(inputB, inputB - away, true, false, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(inputB, false, false, true);
        var result = Frame(graph, history: history);

        Assert.IsFalse(result.linkRemoved);
        Assert.IsFalse(result.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual(new NowNodeLink("a", "out", "b", "in"), graph.links[0]);
        Assert.IsFalse(history.canUndo);
    }

    [Test]
    public void CopyPasteShortcutsCloneSelectionWithLinks()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        graph.FindNode("a").userData = "method";
        graph.FindNode("a").userData2 = "signature";
        graph.FindNode("a").userData3 = "int,string";
        graph.FindNode("a").SetData("method", "Game.Api.Score");
        graph.FindNode("a").SetData("signature", "int Game.Api.Score(string)");

        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph);

        graph.AddNodeToSelection("b");

        _keyboard.frame = new NowTextInputFrame { copyPressed = true, command = true };
        var copyResult = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(copyResult.nodesCopied);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(450f, 250f), false, false, false);
        _keyboard.frame = new NowTextInputFrame { pastePressed = true, command = true };
        var pasteResult = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(pasteResult.nodesPasted);
        Assert.AreEqual(4, graph.nodes.Count);
        Assert.AreEqual(2, graph.links.Count);
        Assert.AreEqual(2, graph.SelectedNodeCount());

        var pastedA = graph.FindNode(graph.selectedNodeIds[0]);
        var pastedB = graph.FindNode(graph.selectedNodeIds[1]);

        Assert.AreNotEqual("a", pastedA.id);
        Assert.AreNotEqual("b", pastedB.id);
        Assert.AreEqual(new Vector2(280f, 0f), pastedB.position - pastedA.position);
        Assert.AreEqual(new NowNodeLink(pastedA.id, "out", pastedB.id, "in"), graph.links[1]);
        Assert.AreEqual("method", pastedA.userData);
        Assert.AreEqual("signature", pastedA.userData2);
        Assert.AreEqual("int,string", pastedA.userData3);
        Assert.AreEqual("Game.Api.Score", pastedA.GetData("method"));
        Assert.AreEqual("int Game.Api.Score(string)", pastedA.GetData("signature"));
    }

    [Test]
    public void CutShortcutRemovesNodesAndPasteRestoresCopies()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph, history: history);

        _keyboard.frame = new NowTextInputFrame { cutPressed = true, command = true };
        var cutResult = Frame(graph, history: history);
        _keyboard.frame = default;

        Assert.IsTrue(cutResult.nodesCut);
        Assert.IsTrue(cutResult.nodesDeleted);
        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(0, graph.links.Count);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 100f), false, false, false);
        _keyboard.frame = new NowTextInputFrame { pastePressed = true, command = true };
        var pasteResult = Frame(graph, history: history);
        _keyboard.frame = default;

        Assert.IsTrue(pasteResult.nodesPasted);
        Assert.AreEqual(2, graph.nodes.Count);
        Assert.IsNull(graph.FindNode("a"));
    }

    [Test]
    public void DuplicateShortcutOffsetsCopyAndSelectsIt()
    {
        var graph = SampleGraph();

        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph);

        _keyboard.frame = new NowTextInputFrame { duplicatePressed = true, command = true };
        var result = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(result.nodesDuplicated);
        Assert.AreEqual(3, graph.nodes.Count);
        Assert.AreEqual(1, graph.SelectedNodeCount());

        var copy = graph.FindNode(graph.selectedNodeIds[0]);

        Assert.AreNotEqual("a", copy.id);
        Assert.AreEqual(new Vector2(64f, 54f), copy.position);
        Assert.IsTrue(NowNodeGraphClipboard.shared.isEmpty);
    }

    [Test]
    public void SelectAllShortcutSelectsEveryNode()
    {
        var graph = SampleGraph();

        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph);

        _keyboard.frame = new NowTextInputFrame { selectAllPressed = true, command = true };
        var result = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(result.selectionChanged);
        Assert.AreEqual(2, graph.SelectedNodeCount());
    }

    [Test]
    public void EscapeCancelsPickedLinkDrag()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        var inputB = new Vector2(320f, 80f);
        var away = new Vector2(420f, 150f);

        _pointer.snapshot = new NowInputSnapshot(inputB, true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(away, away - inputB, true, false, false);
        Frame(graph, history: history);

        _keyboard.frame = new NowTextInputFrame { escapePressed = true };
        Frame(graph, history: history);
        _keyboard.frame = default;

        _pointer.snapshot = new NowInputSnapshot(away, false, false, true);
        var result = Frame(graph, history: history);

        Assert.IsFalse(result.linkRemoved);
        Assert.IsFalse(result.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual(new NowNodeLink("a", "out", "b", "in"), graph.links[0]);
        Assert.IsFalse(history.canUndo);
    }

    [Test]
    public void FrameShortcutCentersView()
    {
        var graph = SampleGraph();

        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph);

        _keyboard.frame = new NowTextInputFrame { characters = "f" };
        var result = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(result.changed);
        Assert.IsTrue(result.panned);
    }

    [Test]
    public void ClickingLinkSelectsItAndDeleteRemovesIt()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));
        var history = new NowNodeGraphHistory();

        Frame(graph, history: history);

        var midLink = new Vector2(270f, 80f);
        _pointer.snapshot = new NowInputSnapshot(midLink, true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(midLink, false, false, true);
        var selectResult = Frame(graph, history: history);

        Assert.IsTrue(selectResult.selectionChanged);
        Assert.AreEqual(graph.links[0], graph.selectedLink);
        Assert.IsTrue(graph.HasSelectedLink());
        Assert.AreEqual(0, graph.SelectedNodeCount());

        _keyboard.frame = new NowTextInputFrame { deleteHeld = true };
        var deleteResult = Frame(graph, history: history);
        _keyboard.frame = default;

        Assert.IsTrue(deleteResult.linkRemoved);
        Assert.AreEqual(0, graph.links.Count);

        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(1, graph.links.Count);
    }

    [Test]
    public void PortContextsReportConnectivity()
    {
        var graph = TriGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));

        var renderer = new CountingRenderer();
        Frame(graph, renderer: renderer);

        Assert.AreEqual(3, renderer.portCount);
        Assert.AreEqual(2, renderer.connectedPortCount);
    }

    [Test]
    public void RerouteNodesArePillsAndForwardValues()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Constant").SetSize(180f, 100f).Output(ValuePort, "Out", Float);
        schema.Reroute(999, "Reroute", Float);

        var graph = new NowNodeGraph();
        var source = graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "src");
        source.userId = 2500;
        graph.AddNode(schema, 999, new Vector2(320f, 60f), id: "reroute");

        Assert.IsTrue(graph.TryAddLink("src", ValuePort, "reroute", NowNodeGraphSchema.RerouteInputPortId));

        var evaluator = new NowNodeGraphEvaluator<float>()
            .Kind(TextureNode, ctx => ctx.node.userId / 1000f)
            .Kind(999, ctx => ctx.Input(NowNodeGraphSchema.RerouteInputPortId));

        Assert.AreEqual(2.5f, evaluator.Evaluate(graph, "reroute"));

        var renderer = new CountingRenderer();
        graph.SelectNode(null);
        Frame(graph, schema: schema, renderer: renderer);

        Assert.IsTrue(renderer.lastNode.reroute);
        Assert.AreEqual(new Vector2(26f, 26f), renderer.lastNode.nodeRect.size);
        Assert.IsFalse(renderer.lastNode.hasPreview);
        Assert.IsTrue(renderer.lastNode.compactToggleRect.isEmpty);
    }

    [Test]
    public void SpaceOpensNodeSearchAndEnterCreatesNode()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").SetSize(200f, 120f).Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Output").SetSize(200f, 120f).Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();

        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "out" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        var created = Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.IsTrue(created.changed);
        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(OutputNode, graph.nodes[0].kindId);
        Assert.AreEqual(new Vector2(200f, 150f), graph.nodes[0].position);
        Assert.AreEqual(graph.nodes[0].id, graph.selectedNodeId);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var reopened = Frame(graph, schema: schema);
        Assert.IsTrue(reopened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(2, graph.nodes.Count);
        Assert.AreEqual(OutputNode, graph.nodes[1].kindId);
    }

    [Test]
    public void NodeSearchMatchesDefinitionKeywords()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Branch")
            .SetSearchKeywords("if condition else")
            .SetSize(200f, 120f)
            .Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Output")
            .SetSize(200f, 120f)
            .Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "if" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
        Assert.AreEqual("Branch", graph.nodes[0].title);
    }

    [Test]
    public void NodeSearchMatchesDefinitionCategory()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Delay")
            .SetCategory("Flow")
            .SetSize(200f, 120f)
            .Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Output")
            .SetCategory("Values")
            .SetSize(200f, 120f)
            .Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "flow" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
        Assert.AreEqual("Delay", graph.nodes[0].title);
    }

    [Test]
    public void NodeSearchRanksTitleMatchesBeforeKeywordAndCategoryMatches()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Category Match")
            .SetCategory("Score")
            .SetSize(180f, 90f)
            .Output(ValuePort, "Out", Float);
        schema.Node(TextureNode + 1, "Keyword Match")
            .SetSearchKeywords("score")
            .SetSize(180f, 90f)
            .Output(ValuePort, "Out", Float);
        schema.Node(TextureNode + 2, "Score")
            .SetSize(180f, 90f)
            .Output(ValuePort, "Out", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "score" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode + 2, graph.nodes[0].kindId);
        Assert.AreEqual("Score", graph.nodes[0].title);
    }

    [Test]
    public void NodeSearchMatchesDefinitionDetail()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Call")
            .SetSearchDetail("int Game.Api.Score(string text)")
            .SetSize(180f, 90f)
            .Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Output")
            .SetSize(180f, 90f)
            .Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(200f, 150f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "api.score" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
        Assert.AreEqual("Call", graph.nodes[0].title);
    }

    [Test]
    public void SearchResultLimitAllowsSelectingRowsBeyondDefaultLimit()
    {
        var schema = new NowNodeGraphSchema();

        for (int i = 0; i < 12; i++)
        {
            schema.Node(TextureNode + i, "Node " + i.ToString("00"))
                .SetSize(180f, 90f)
                .Output(ValuePort, "Out", Float);
        }

        var graph = new NowNodeGraph();
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> largeSearch =
            canvas => canvas.SetSearchResultLimit(12);

        Frame(graph, schema: schema, configure: largeSearch);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(40f, 40f), true, true, false);
        Frame(graph, schema: schema, configure: largeSearch);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(40f, 40f), false, false, true);
        Frame(graph, schema: schema, configure: largeSearch);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema, configure: largeSearch);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { characters = "node" };
        Frame(graph, schema: schema, configure: largeSearch);
        _keyboard.frame = default;

        _pointer.snapshot = new NowInputSnapshot(new Vector2(60f, 316f), true, true, false);
        Frame(graph, schema: schema, configure: largeSearch);

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode + 10, graph.nodes[0].kindId);
        Assert.AreEqual("Node 10", graph.nodes[0].title);
    }

    [Test]
    public void SpaceOpensSearchWithoutImmediatelyCreatingNode()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "X").SetSize(180f, 90f).Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Output").SetSize(180f, 90f).Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        // Focus the canvas.
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, schema: schema);

        // Space opens the palette; the real Input System also raises submitPressed on
        // the same frame (Space is a submit key), which must NOT confirm a result.
        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(10f, 10f), new Vector2(10f, 10f), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 0, time: 0f);
        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);

        Assert.IsTrue(opened.searchOpened);
        Assert.AreEqual(0, graph.nodes.Count);

        // Enter still confirms.
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, false);
        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
    }

    [Test]
    public void SpaceSearchUsesCanvasLocalPositionInsideParentTransform()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").SetSize(180f, 90f).Output(ValuePort, "Out", Float);
        var graph = new NowNodeGraph().SetSchema(schema);
        var scale = new Vector2(1.4f, 1.2f);
        var origin = new Vector2(24f, 18f);
        var localPointer = new Vector2(200f, 150f);
        var screenPointer = Vector2.Scale(localPointer, scale) + origin;

        TransformedFrame(graph, scale, origin);
        _pointer.snapshot = new NowInputSnapshot(screenPointer, true, true, false);
        TransformedFrame(graph, scale, origin);
        _pointer.snapshot = new NowInputSnapshot(screenPointer, false, false, true);
        TransformedFrame(graph, scale, origin);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = TransformedFrame(graph, scale, origin);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        TransformedFrame(graph, scale, origin);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(localPointer, graph.nodes[0].position);
    }

    [Test]
    public void SearchGenericSubmitCreatesHighlightedNode()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").SetSize(180f, 90f).Output(ValuePort, "Out", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        Frame(graph, schema: schema);

        _keyboard.frame = default;
        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(10f, 10f), new Vector2(10f, 10f), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 0.016f);
        Frame(graph, schema: schema);

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
    }

    [Test]
    public void TypingSpaceInSearchDoesNotSubmit()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Debug Log").SetSize(180f, 90f).Output(ValuePort, "Out", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(10f, 10f), new Vector2(10f, 10f), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: true, submitPressed: true, submitReleased: false,
            cancelDown: false, cancelPressed: false, cancelReleased: false,
            frame: 1, time: 0.016f);
        _keyboard.frame = new NowTextInputFrame { characters = " " };
        Frame(graph, schema: schema);

        Assert.AreEqual(0, graph.nodes.Count);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, false);
        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
    }

    [Test]
    public void SearchCancelClosesWithoutCreatingNode()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").SetSize(180f, 90f).Output(ValuePort, "Out", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        Frame(graph, schema: schema);

        _keyboard.frame = default;
        _pointer.snapshot = new NowInputSnapshot(
            true, new Vector2(10f, 10f), new Vector2(10f, 10f), Vector2.zero,
            NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.None,
            Vector2.zero, Vector2.zero,
            submitDown: false, submitPressed: false, submitReleased: false,
            cancelDown: true, cancelPressed: true, cancelReleased: false,
            frame: 1, time: 0.016f);
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, false);
        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(0, graph.nodes.Count);
    }

    [Test]
    public void OpenSearchSurvivesPassiveDrawBeforeNextInput()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Texture").SetSize(180f, 90f).Output(ValuePort, "Out", Float);

        var graph = new NowNodeGraph();
        Frame(graph, schema: schema);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, true);
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { characters = " " };
        var opened = Frame(graph, schema: schema);
        Assert.IsTrue(opened.searchOpened);

        _keyboard.frame = default;
        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), false, false, false);
        PassiveFrame(graph, schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(1, graph.nodes.Count);
        Assert.AreEqual(TextureNode, graph.nodes[0].kindId);
    }

    [Test]
    public void BackspaceDeletesSelectedNode()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph, history: history);
        Assert.IsTrue(graph.IsNodeSelected("a"));

        _keyboard.frame = new NowTextInputFrame { backspaceHeld = true };
        var result = Frame(graph, history: history);
        _keyboard.frame = default;

        Assert.IsTrue(result.nodesDeleted);
        Assert.IsNull(graph.FindNode("a"));
        Assert.IsTrue(history.Undo(graph));
        Assert.IsNotNull(graph.FindNode("a"));
    }

    [Test]
    public void MarqueeShiftAddsToSelection()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph);
        Assert.AreEqual(1, graph.SelectedNodeCount());

        _keyboard.frame = new NowTextInputFrame { shift = true };
        _pointer.snapshot = new NowInputSnapshot(new Vector2(300f, 10f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), new Vector2(220f, 160f), true, false, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), false, false, true);
        var result = Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(result.dragSelected);
        Assert.IsTrue(graph.IsNodeSelected("a"));
        Assert.IsTrue(graph.IsNodeSelected("b"));
        Assert.AreEqual(2, graph.SelectedNodeCount());
    }

    [Test]
    public void MarqueeCommandSubtractsFromSelection()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 10f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), new Vector2(510f, 160f), true, false, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), false, false, true);
        Frame(graph);
        Assert.AreEqual(2, graph.SelectedNodeCount());

        _keyboard.frame = new NowTextInputFrame { command = true };
        _pointer.snapshot = new NowInputSnapshot(new Vector2(300f, 10f), true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), new Vector2(220f, 160f), true, false, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(520f, 170f), false, false, true);
        Frame(graph);
        _keyboard.frame = default;

        Assert.IsTrue(graph.IsNodeSelected("a"));
        Assert.IsFalse(graph.IsNodeSelected("b"));
        Assert.AreEqual(1, graph.SelectedNodeCount());
    }

    [Test]
    public void ArrowKeyNudgeMovesSelectionAndCoalescesHistory()
    {
        var graph = SampleGraph();
        var history = new NowNodeGraphHistory();
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), false, false, true);
        Frame(graph, history: history);

        var start = graph.FindNode("a").position;

        _keyboard.frame = new NowTextInputFrame { rightHeld = true };
        var result = Frame(graph, history: history);
        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(start + new Vector2(1f, 0f), graph.FindNode("a").position);
        Assert.IsTrue(history.canUndo);

        // Neutral frame ends the nudge burst so the next hold is a fresh undo entry.
        _keyboard.frame = default;
        Frame(graph, history: history);

        _keyboard.frame = new NowTextInputFrame { downHeld = true, shift = true };
        Frame(graph, history: history);
        _keyboard.frame = default;

        Assert.AreEqual(start + new Vector2(1f, 24f), graph.FindNode("a").position);

        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(start + new Vector2(1f, 0f), graph.FindNode("a").position);
        Assert.IsTrue(history.Undo(graph));
        Assert.AreEqual(start, graph.FindNode("a").position);
    }

    [Test]
    public void DragWireToEmptyOpensConnectionSearchAndAutoWires()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Source").SetSize(200f, 120f).Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Sink").SetSize(200f, 120f).Input(ColorPort, "In", Float);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "src");

        Frame(graph, schema: schema);

        var outPos = new Vector2(256f, 80f);
        var empty = new Vector2(450f, 250f);

        _pointer.snapshot = new NowInputSnapshot(outPos, true, true, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(empty, empty - outPos, true, false, false);
        Frame(graph, schema: schema);
        _pointer.snapshot = new NowInputSnapshot(empty, false, false, true);
        var opened = Frame(graph, schema: schema);

        Assert.IsTrue(opened.searchOpened);

        _pointer.snapshot = new NowInputSnapshot(empty, false, false, false);
        _keyboard.frame = new NowTextInputFrame { characters = "sink" };
        Frame(graph, schema: schema);

        _keyboard.frame = new NowTextInputFrame { enterPressed = true };
        var created = Frame(graph, schema: schema);
        _keyboard.frame = default;

        Assert.AreEqual(2, graph.nodes.Count);
        Assert.AreEqual(OutputNode, graph.nodes[1].kindId);
        Assert.IsTrue(created.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("src", graph.links[0].outputNodeId);
        Assert.AreEqual(graph.nodes[1].id, graph.links[0].inputNodeId);
    }

    [Test]
    public void LinkReleaseSnapsToNearbyCompatiblePort()
    {
        var graph = SampleGraph();
        Frame(graph);

        var outPos = new Vector2(232f, 80f);
        var nearInput = new Vector2(334f, 80f); // 14px from b's input, within the snap radius

        _pointer.snapshot = new NowInputSnapshot(outPos, true, true, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(nearInput, nearInput - outPos, true, false, false);
        Frame(graph);
        _pointer.snapshot = new NowInputSnapshot(nearInput, false, false, true);
        var result = Frame(graph);

        Assert.IsTrue(result.linkCreated);
        Assert.AreEqual(1, graph.links.Count);
        Assert.AreEqual("a", graph.links[0].outputNodeId);
        Assert.AreEqual("b", graph.links[0].inputNodeId);
    }

    [Test]
    public void RightDragPansWithoutOpeningMenu()
    {
        var graph = SampleGraph();
        var menu = new NowNodeGraphContextMenu();

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(200f, 200f), NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None);
        var press = Frame(graph, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(280f, 250f), new Vector2(80f, 50f), NowPointerButtons.Secondary, NowPointerButtons.None, NowPointerButtons.None);
        var drag = Frame(graph, contextMenu: menu);

        _pointer.snapshot = new NowInputSnapshot(
            new Vector2(280f, 250f), NowPointerButtons.None, NowPointerButtons.None, NowPointerButtons.Secondary);
        var release = Frame(graph, contextMenu: menu);

        Assert.IsTrue(drag.panned);
        Assert.IsFalse(press.contextMenuOpened);
        Assert.IsFalse(drag.contextMenuOpened);
        Assert.IsFalse(release.contextMenuOpened);
    }

    [Test]
    public void PanWithPrimaryDragReplacesMarquee()
    {
        var graph = SampleGraph();
        Func<NowNodeGraphCanvas, NowNodeGraphCanvas> cfg = c => c.SetPanWithPrimaryDrag(true);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 200f), true, true, false);
        Frame(graph, configure: cfg);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(90f, 240f), new Vector2(80f, 40f), true, false, false);
        var drag = Frame(graph, configure: cfg);
        _pointer.snapshot = new NowInputSnapshot(new Vector2(90f, 240f), false, false, true);
        var release = Frame(graph, configure: cfg);

        Assert.IsTrue(drag.panned);
        Assert.IsFalse(drag.dragSelected);
        Assert.IsFalse(release.dragSelected);
        Assert.AreEqual(0, graph.SelectedNodeCount());
    }

    static NowNodeGraph TriGraph()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", new Vector2(40f, 30f));
        var b = graph.AddNode("b", "B", new Vector2(320f, 30f));
        var c = graph.AddNode("c", "C", new Vector2(320f, 180f));
        a.size = new Vector2(180f, 118f);
        b.size = new Vector2(180f, 118f);
        c.size = new Vector2(180f, 118f);
        a.AddOutput("out", "Out", Float);
        b.AddInput("in", "In", Float);
        c.AddInput("in", "In", Float);
        return graph;
    }

    [Test]
    public void PreviewExpandsNodeBelowBody()
    {
        bool sawPreview = false;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 120f)
            .Output(ValuePort, "Out", Float)
            .Preview(ctx => sawPreview |= ctx.isPreview, 48f);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "a");

        var renderer = new CountingRenderer();
        Frame(graph, schema: schema, renderer: renderer);

        Assert.IsTrue(renderer.lastNode.hasPreview);
        Assert.IsFalse(renderer.lastNode.previewRect.isEmpty);
        Assert.IsTrue(sawPreview);
        Assert.AreEqual(192f, renderer.lastNode.nodeRect.height);
        Assert.LessOrEqual(renderer.lastNode.bodyRect.yMax, renderer.lastNode.previewRect.y);
    }

    [Test]
    public void CompactToggleStripsNodeToPorts()
    {
        bool sawPreview = false;
        var schema = new NowNodeGraphSchema();
        schema.Node(TextureNode, "Preview")
            .SetSize(180f, 120f)
            .Output(ValuePort, "Out", Float)
            .Preview(ctx => sawPreview |= ctx.isPreview, 48f);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "a");
        var history = new NowNodeGraphHistory();

        Frame(graph, schema: schema, history: history);

        var toggle = new Vector2(215f, 45f);
        _pointer.snapshot = new NowInputSnapshot(toggle, true, true, false);
        Frame(graph, schema: schema, history: history);

        _pointer.snapshot = new NowInputSnapshot(toggle, false, false, true);
        var result = Frame(graph, schema: schema, history: history);

        Assert.IsTrue(result.compactToggled);
        Assert.IsTrue(graph.FindNode("a").compact);

        var renderer = new CountingRenderer();
        sawPreview = false;
        _pointer.snapshot = default;
        Frame(graph, schema: schema, renderer: renderer, history: history);

        Assert.IsTrue(renderer.lastNode.compact);
        Assert.IsFalse(renderer.lastNode.hasPreview);
        Assert.IsTrue(renderer.lastNode.previewRect.isEmpty);
        Assert.IsFalse(renderer.lastNode.compactToggleRect.isEmpty);
        Assert.IsFalse(sawPreview);
        Assert.AreEqual(72f, renderer.lastNode.nodeRect.height);
        Assert.AreEqual(1, renderer.portCount);

        Assert.IsTrue(history.Undo(graph));
        Assert.IsFalse(graph.FindNode("a").compact);
    }

    [Test]
    public void TypeColorsDriveLinkGradients()
    {
        var schema = new NowNodeGraphSchema()
            .AllowSameTypes()
            .Allow(Float, Float4)
            .TypeColor(Float, Color.cyan)
            .TypeColor(Float4, Color.magenta);

        schema.Node(TextureNode, "Source").SetSize(180f, 100f).Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Target").SetSize(180f, 100f).Input(ColorPort, "In", Float4);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "source");
        graph.AddNode(schema, OutputNode, new Vector2(320f, 30f), id: "target");
        Assert.IsTrue(graph.TryAddLink("source", ValuePort, "target", ColorPort));

        var renderer = new CountingRenderer();
        Frame(graph, schema: schema, renderer: renderer);

        Assert.AreEqual(1, renderer.linkCount);
        Assert.AreEqual(Color.cyan, renderer.lastLink.color);
        Assert.AreEqual(Color.magenta, renderer.lastLink.colorTo);
        Assert.AreEqual(Color.magenta, renderer.lastPort.color);
    }

    [Test]
    public void CanvasDrawsGradientLinkGeometry()
    {
        var schema = new NowNodeGraphSchema()
            .AllowSameTypes()
            .Allow(Float, Float4)
            .TypeColor(Float, Color.cyan)
            .TypeColor(Float4, Color.magenta);

        schema.Node(TextureNode, "Source").SetSize(180f, 100f).Output(ValuePort, "Out", Float);
        schema.Node(OutputNode, "Target").SetSize(180f, 100f).Input(ColorPort, "In", Float4);

        var graph = new NowNodeGraph();
        graph.AddNode(schema, TextureNode, new Vector2(40f, 30f), id: "source");
        graph.AddNode(schema, OutputNode, new Vector2(320f, 30f), id: "target");
        Assert.IsTrue(graph.TryAddLink("source", ValuePort, "target", ColorPort));

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowNodes.Canvas(graph, CanvasRect, "gradient-graph")
                .SetSchema(schema)
                .SetGrid(false)
                .Draw();
        }

        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void FindsInputLinkFeedingPort()
    {
        var graph = SampleGraph();
        Assert.IsTrue(graph.TryAddLink("a", "out", "b", "in"));

        Assert.IsTrue(graph.TryGetInputLink("b", "in", out var link));
        Assert.AreEqual("a", link.outputNodeId);
        Assert.AreEqual("out", link.outputPortId);

        Assert.IsFalse(graph.TryGetInputLink("a", "out", out _));
        Assert.IsFalse(graph.TryGetInputLink("b", "missing", out _));
    }

    [Test]
    public void EvaluatorComputesConnectedGraph()
    {
        var (graph, schema) = EvaluatorGraph();
        SetEvalConstant(graph.AddNode(schema, EvalConstant, default, id: "two"), 2f);
        SetEvalConstant(graph.AddNode(schema, EvalConstant, default, id: "three"), 3f);
        graph.AddNode(schema, EvalAdd, default, id: "sum");
        Assert.IsTrue(graph.TryAddLink("two", EvalOut, "sum", EvalA));
        Assert.IsTrue(graph.TryAddLink("three", EvalOut, "sum", EvalB));

        Assert.AreEqual(5f, FloatEvaluator().Evaluate(graph, "sum"));
    }

    [Test]
    public void EvaluatorFallsBackForUnconnectedInputs()
    {
        var (graph, schema) = EvaluatorGraph();
        SetEvalConstant(graph.AddNode(schema, EvalConstant, default, id: "two"), 2f);
        graph.AddNode(schema, EvalAdd, default, id: "sum");
        graph.AddNode(schema, EvalRelay, default, id: "relay");
        Assert.IsTrue(graph.TryAddLink("two", EvalOut, "sum", EvalA));

        var evaluator = FloatEvaluator();

        Assert.AreEqual(2f, evaluator.Evaluate(graph, "sum"));
        Assert.AreEqual(8f, evaluator.Evaluate(graph, "relay"));
    }

    [Test]
    public void EvaluatorBreaksCyclesWithFallback()
    {
        var (graph, schema) = EvaluatorGraph();
        graph.AddNode(schema, EvalRelay, default, id: "a");
        graph.AddNode(schema, EvalRelay, default, id: "b");
        Assert.IsTrue(graph.TryAddLink("a", EvalOut, "b", EvalA));
        Assert.IsTrue(graph.TryAddLink("b", EvalOut, "a", EvalA));

        Assert.AreEqual(9f, FloatEvaluator().Evaluate(graph, "a"));
    }

    [Test]
    public void EvaluatorMemoizesSharedUpstreamNodesPerCall()
    {
        var (graph, schema) = EvaluatorGraph();
        SetEvalConstant(graph.AddNode(schema, EvalConstant, default, id: "shared"), 2.5f);
        graph.AddNode(schema, EvalAdd, default, id: "sum");
        Assert.IsTrue(graph.TryAddLink("shared", EvalOut, "sum", EvalA));
        Assert.IsTrue(graph.TryAddLink("shared", EvalOut, "sum", EvalB));

        int constantEvaluations = 0;
        var evaluator = FloatEvaluator(() => ++constantEvaluations);

        Assert.AreEqual(5f, evaluator.Evaluate(graph, "sum"));
        Assert.AreEqual(1, constantEvaluations);

        Assert.AreEqual(5f, evaluator.Evaluate(graph, "sum"));
        Assert.AreEqual(2, constantEvaluations);
    }

    [Test]
    public void EvaluatorEvaluatesRequestedOutputPort()
    {
        var (graph, schema) = EvaluatorGraph();
        graph.AddNode(schema, EvalDual, default, id: "dual");
        var evaluator = FloatEvaluator();

        Assert.AreEqual(10f, evaluator.Evaluate(graph, "dual"));
        Assert.AreEqual(10f, evaluator.Evaluate(graph, "dual", EvalOut));
        Assert.AreEqual(20f, evaluator.Evaluate(graph, "dual", EvalSecond));
    }

    [Test]
    public void EvaluatorTryEvaluateReportsMissingHandlersAndNodes()
    {
        var (graph, schema) = EvaluatorGraph();
        SetEvalConstant(graph.AddNode(schema, EvalConstant, default, id: "two"), 2f);
        graph.AddNode(schema, EvalRelay, default, id: "relay");

        var evaluator = new NowNodeGraphEvaluator<float>()
            .Kind(EvalConstant, ctx => ctx.node.userId / 1000f);

        Assert.IsTrue(evaluator.TryEvaluate(graph, "two", out float value));
        Assert.AreEqual(2f, value);
        Assert.IsFalse(evaluator.TryEvaluate(graph, "relay", out _));
        Assert.IsFalse(evaluator.TryEvaluate(graph, "missing", out _));
        Assert.IsFalse(evaluator.TryEvaluate(graph, "two", NowNodeIds.FromInt(EvalSecond), 0f, out _));
        Assert.IsTrue(evaluator.HasKind(EvalConstant));
        Assert.IsFalse(evaluator.HasKind(EvalRelay));
    }

    static (NowNodeGraph graph, NowNodeGraphSchema schema) EvaluatorGraph()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(EvalConstant, "Constant").Output(EvalOut, "Out", Float);
        schema.Node(EvalAdd, "Add").Input(EvalA, "A", Float).Input(EvalB, "B", Float).Output(EvalOut, "Sum", Float);
        schema.Node(EvalRelay, "Relay").Input(EvalA, "In", Float).Output(EvalOut, "Out", Float);
        schema.Node(EvalDual, "Dual").Output(EvalOut, "First", Float).Output(EvalSecond, "Second", Float);
        return (new NowNodeGraph().SetSchema(schema), schema);
    }

    static NowNodeGraphEvaluator<float> FloatEvaluator(Action onConstantEvaluated = null)
    {
        return new NowNodeGraphEvaluator<float>()
            .Kind(EvalConstant, ctx =>
            {
                onConstantEvaluated?.Invoke();
                return ctx.node.userId / 1000f;
            })
            .Kind(EvalAdd, ctx => ctx.Input(EvalA) + ctx.Input(EvalB))
            .Kind(EvalRelay, ctx => ctx.Input(EvalA, 7f) + 1f)
            .Kind(EvalDual, ctx => ctx.port != null && ctx.port.id == NowNodeIds.FromInt(EvalSecond) ? 20f : 10f);
    }

    static void SetEvalConstant(NowNode node, float value)
    {
        node.userId = Mathf.RoundToInt(value * 1000f);
    }

    static NowNodeGraph SampleGraph()
    {
        var graph = new NowNodeGraph();
        var a = graph.AddNode("a", "A", new Vector2(40f, 30f));
        var b = graph.AddNode("b", "B", new Vector2(320f, 30f));
        a.size = new Vector2(180f, 118f);
        b.size = new Vector2(180f, 118f);
        a.AddOutput("out", "Out", Float);
        b.AddInput("in", "In", Float);
        return graph;
    }
}
