using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.Docking;

public class NowDockingTests
{
    sealed class FakePointer : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(640, 360);
    static readonly NowRect DockRect = new NowRect(0, 0, 420, 260);

    FakePointer _pointer;
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

        _pointer = new FakePointer();
        _drawList = new NowDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowOverlay.Reset();
        NowLayout.Reset();
        NowControls.Reset();
        NowControlState.Reset();
        NowFocus.Reset();
        NowInput.Reset();
    }

    void Frame(NowDockSpace dock, System.Action submit)
    {
        submit();

        using (NowInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
        {
            NowDock.Space(dock, DockRect, "main-dock").Draw();
            NowOverlay.Flush();
        }
    }

    [Test]
    public void DrawsFirstSubmittedWindowAsSelectedTab()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        Frame(dock, () =>
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        });

        Assert.IsTrue(scene);
        Assert.IsFalse(inspector, "Additional windows start as hidden tabs in the same leaf.");
        Assert.IsTrue(_drawList.hasGeometry);
    }

    [Test]
    public void ClickingTabSelectsWindow()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        }

        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsFalse(scene);
        Assert.IsTrue(inspector);
    }

    [Test]
    public void SideDockShowsBothWindows()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;
        NowRect sceneRect = default;
        NowRect inspectorRect = default;

        void Submit()
        {
            dock.Window("Scene", rect => { scene = true; sceneRect = rect; });
            dock.Window("Inspector", rect => { inspector = true; inspectorRect = rect; });
        }

        Frame(dock, Submit);

        Assert.IsTrue(dock.Dock("Inspector", "Scene", NowDockSide.Right));

        scene = false;
        inspector = false;
        Frame(dock, Submit);

        Assert.IsTrue(scene);
        Assert.IsTrue(inspector);
        Assert.Less(sceneRect.x, inspectorRect.x);
        Assert.Greater(sceneRect.width, 0f);
        Assert.Greater(inspectorRect.width, 0f);
    }

    [Test]
    public void SplitterDragResizesDockedPanes()
    {
        var dock = new NowDockSpace();
        NowRect sceneRect = default;
        NowRect inspectorRect = default;

        void Submit()
        {
            dock.Window("Scene", rect => sceneRect = rect);
            dock.Window("Inspector", rect => inspectorRect = rect);
        }

        Frame(dock, Submit);
        Assert.IsTrue(dock.Dock("Inspector", "Scene", NowDockSide.Right));
        Frame(dock, Submit);

        float initialSceneWidth = sceneRect.width;
        float splitterX = sceneRect.x + sceneRect.width + 8f;
        var splitterPoint = new Vector2(splitterX, 120f);

        _pointer.snapshot = new NowInputSnapshot(splitterPoint, true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(splitterPoint + new Vector2(50f, 0f), new Vector2(50f, 0f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(splitterPoint + new Vector2(50f, 0f), false, false, true);
        Frame(dock, Submit);

        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.Greater(sceneRect.width, initialSceneWidth + 20f);
        Assert.Less(sceneRect.xMax, inspectorRect.x);
    }

    [Test]
    public void DroppingOnTabBarMergesAsTab()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        }

        Frame(dock, Submit);
        Assert.IsTrue(dock.Dock("Inspector", "Scene", NowDockSide.Right));
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(230f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(24f, 14f), new Vector2(-206f, 0f), true, false, false);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = new NowInputSnapshot(new Vector2(24f, 14f), Vector2.zero, true, false, false);
        Frame(dock, Submit);

        Assert.IsTrue(scene, "Docking should not commit while the tab is still being dragged.");
        Assert.IsTrue(inspector);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(24f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsFalse(scene, "Merged tabs should draw only the selected dropped window.");
        Assert.IsTrue(inspector);
    }

    [Test]
    public void DraggingTabOutsideDockSpaceFloatsWindow()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;
        NowRect inspectorRect = default;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", rect => { inspector = true; inspectorRect = rect; });
        }

        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), new Vector2(500f, 46f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene);
        Assert.IsTrue(inspector);
        Assert.Greater(inspectorRect.x, 300f);
    }

    [Test]
    public void DroppingOnPaneBodyCenterFloatsInsteadOfTabDocking()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        }

        Frame(dock, Submit);
        Assert.IsTrue(dock.Dock("Inspector", "Scene", NowDockSide.Right));
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(230f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(96f, 130f), new Vector2(-134f, 116f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(96f, 130f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene, "Dropping deep in the pane body should not merge as a tab.");
        Assert.IsTrue(inspector, "The dropped tab should remain visible as a floating window.");
    }

    [Test]
    public void DraggingTabReordersTabs()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;
        bool console = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
            dock.Window("Console", () => console = true);
        }

        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(178f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), new Vector2(-168f, 0f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), false, false, true);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        console = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene, "After reordering, the original first tab should be at the second tab position.");
        Assert.IsFalse(inspector);
        Assert.IsFalse(console);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        console = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsFalse(scene);
        Assert.IsFalse(inspector);
        Assert.IsTrue(console, "The dragged tab should be first after reordering.");
    }

    [Test]
    public void MovingFloatingTitleBarDoesNotDockWindow()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", () => inspector = true);
        }

        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), new Vector2(500f, 46f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), false, false, true);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(590f, 60f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(120f, 60f), new Vector2(-470f, 0f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(120f, 60f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene, "Moving a floating title bar must not merge the floating window into the dock.");
        Assert.IsTrue(inspector);
    }

    [Test]
    public void ReleasingFloatingTabOverTabBarCommitsAndClearsDrag()
    {
        var dock = new NowDockSpace();
        bool scene = false;
        bool inspector = false;
        NowRect inspectorRect = default;

        void Submit()
        {
            dock.Window("Scene", () => scene = true);
            dock.Window("Inspector", rect => { inspector = true; inspectorRect = rect; });
        }

        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(100f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), new Vector2(500f, 46f), true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(600f, 60f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene);
        Assert.IsTrue(inspector);
        Assert.Greater(inspectorRect.x, 300f);

        var floatingTabPoint = new Vector2(inspectorRect.x + 20f, inspectorRect.y - 22f);

        _pointer.snapshot = new NowInputSnapshot(floatingTabPoint, true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(24f, 14f), new Vector2(24f, 14f) - floatingTabPoint, true, false, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(24f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsFalse(scene, "Releasing a floating tab over a tab bar should merge it as the selected docked tab.");
        Assert.IsTrue(inspector);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), true, true, false);
        Frame(dock, Submit);

        _pointer.snapshot = new NowInputSnapshot(new Vector2(10f, 14f), false, false, true);
        Frame(dock, Submit);

        scene = false;
        inspector = false;
        _pointer.snapshot = default;
        Frame(dock, Submit);

        Assert.IsTrue(scene, "The release must also clear NowInput capture so tabs remain clickable afterward.");
        Assert.IsFalse(inspector);
    }
}
