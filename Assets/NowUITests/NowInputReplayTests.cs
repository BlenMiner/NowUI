using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Docking;
using NowUI.NodeGraph;

public class NowInputReplayTests
{
    static readonly Vector2 Surface = new Vector2(640, 360);
    static readonly NowRect TextRect = new NowRect(20, 20, 240, 34);

    NowInputReplay _replay;
    NowDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowLayout.Reset();
        NowOverlay.Reset();
        NowTextInput.Reset();
        NowContextMenu.Reset();

        _replay = new NowInputReplay();
        _drawList = new NowDrawList();
        NowTextInput.source = _replay;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowContextMenu.Reset();
        NowTextInput.Reset();
        NowOverlay.Reset();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowFocus.Reset();
        NowInput.Reset();
    }

    [Test]
    public void ReplayCanFocusAndTypeIntoTextField()
    {
        string text = "";

        _replay.Press(new Vector2(40f, 36f));
        DrawTextField(ref text);
        _replay.Release(new Vector2(40f, 36f));
        DrawTextField(ref text);
        _replay.Text("abc");

        Assert.IsTrue(DrawTextField(ref text));
        Assert.AreEqual("abc", text);
    }

    [Test]
    public void ReplayCanOpenDropdownAndApplyPendingSelection()
    {
        var options = new List<string> { "Low", "Medium", "High" };
        int selected = 0;
        var rect = new NowRect(20, 20, 160, 30);

        _replay.Press(new Vector2(60f, 35f));
        DrawDropdown(rect, options, ref selected);
        _replay.Release(new Vector2(60f, 35f));
        DrawDropdown(rect, options, ref selected);

        int id;
        using (NowInput.Begin(_replay, Surface))
            id = NowControls.GetControlId("quality");

        Assert.IsTrue(NowControlState.Get<bool>(id), "Dropdown should open after replayed click.");

        NowControlState.Get<int>(id, "pending") = 3;
        _replay.Idle();

        Assert.IsTrue(DrawDropdown(rect, options, ref selected));
        Assert.AreEqual(2, selected);
    }

    [Test]
    public void ReplayCanScrollViewWithWheel()
    {
        var viewport = new NowRect(0, 0, 200, 100);

        _replay.Move(new Vector2(40f, 40f));
        DrawScrollView(viewport);
        DrawScrollView(viewport);

        _replay.Scroll(new Vector2(40f, 40f), new Vector2(0f, -5f));
        DrawScrollView(viewport);

        int id;
        using (NowInput.Begin(_replay, Surface))
            id = NowControls.GetControlId("list");

        ref Vector2 scroll = ref NowControlState.Get<Vector2>(id);
        Assert.Greater(scroll.y, 0f);
    }

    [Test]
    public void ReplayCanSelectDockTab()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        }

        DrawDock(dock, Submit);
        _replay.Press(new Vector2(100f, 14f));
        DrawDock(dock, Submit);
        _replay.Release(new Vector2(100f, 14f));
        DrawDock(dock, Submit);

        scene = false;
        inspector = false;
        _replay.Idle();
        DrawDock(dock, Submit);

        Assert.IsFalse(scene);
        Assert.IsTrue(inspector);
    }

    [Test]
    public void ReplayCanSelectNodeGraphNode()
    {
        var schema = new NowNodeGraphSchema();
        schema.Node(1, "Input").SetSize(160f, 90f).Output(10, "Value", 1);
        schema.Node(2, "Output").SetSize(160f, 90f).Input(20, "Value", 1);
        schema.AllowSameTypes();

        var graph = new NowNodeGraph().SetSchema(schema);
        graph.AddNode(schema, 1, new Vector2(80f, 80f), id: "input");
        graph.AddNode(schema, 2, new Vector2(340f, 120f), id: "output");

        _replay.Press(new Vector2(100f, 100f));
        var result = DrawNodeGraph(graph, schema);
        _replay.Release(new Vector2(100f, 100f));
        DrawNodeGraph(graph, schema);

        Assert.IsTrue(result.selectionChanged);
        Assert.AreEqual("input", result.selectedNodeId);
    }

    bool DrawTextField(ref string text)
    {
        NowTextInput.Invalidate();
        using (NowInput.Begin(_replay, Surface))
        using (_drawList.Begin(Surface))
            return Now.TextField(TextRect, "name").Draw(ref text);
    }

    bool DrawDropdown(NowRect rect, List<string> options, ref int selected)
    {
        using (NowInput.Begin(_replay, Surface))
        using (_drawList.Begin(Surface))
        {
            bool changed = Now.Dropdown(rect, "quality", options).Draw(ref selected);
            NowOverlay.Flush();
            return changed;
        }
    }

    void DrawScrollView(NowRect viewport)
    {
        using (NowInput.Begin(_replay, Surface))
        using (_drawList.Begin(Surface))
        using (Now.ScrollView(viewport, "list").Begin())
        {
            for (int i = 0; i < 18; ++i)
                NowLayout.Label($"Row {i}", NowLayout.Height(22f)).Draw();
        }
    }

    void DrawDock(NowDockSpace dock, System.Action submit)
    {
        submit();
        using (NowInput.Begin(_replay, Surface))
        using (_drawList.Begin(Surface))
        {
            NowDock.Space(dock, new NowRect(0, 0, 420, 260), "main-dock").Draw();
            NowOverlay.Flush();
        }
    }

    NowNodeGraphResult DrawNodeGraph(NowNodeGraph graph, NowNodeGraphSchema schema)
    {
        using (NowInput.Begin(_replay, Surface))
        using (_drawList.Begin(Surface))
            return NowNodes.Canvas(graph, new NowRect(0, 0, 640, 360), "graph").SetSchema(schema).Draw();
    }
}
