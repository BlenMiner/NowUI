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
        public NowNodeGraphNodeContext lastNode;
        public NowNodeGraphPortContext lastPort;

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
        NowNodeGraphContextMenu contextMenu = null)
    {
        NowTextInput.Invalidate();

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

            var result = canvas.Draw();
            NowOverlay.Flush();
            return result;
        }
    }

    NowNodeGraphResult Frame(NowNodeGraphView view)
    {
        NowTextInput.Invalidate();

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

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            var canvas = NowNodes.Canvas(view, CanvasRect, "graph").SetNodeContent(content);
            var result = canvas.Draw();
            NowOverlay.Flush();
            return result;
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
    public void DraggingNodeMovesPosition()
    {
        var graph = SampleGraph();
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), new Vector2(40f, 20f), true, false, false);
        var result = Frame(graph);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), false, false, true);
        Frame(graph);

        Assert.IsTrue(result.nodeMoved);
        Assert.AreEqual(new Vector2(80f, 50f), graph.FindNode("a").position);
    }

    [Test]
    public void DraggingOutputToInputCreatesLink()
    {
        var graph = SampleGraph();
        Frame(graph);

        var output = new Vector2(220f, 80f);
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
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(70f, 45f), true, true, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), new Vector2(40f, 20f), true, false, false);
        Frame(graph, history: history);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(110f, 65f), false, false, true);
        Frame(graph, history: history);

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
            deleteSelection = false
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
