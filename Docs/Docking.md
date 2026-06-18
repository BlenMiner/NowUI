# Docking Extension

`NowUI.Extensions.Docking` adds a Dear ImGui-style dock space: tabbed panels,
side docking, splitters, floating windows, tab-drag dock guides, and tab
reordering. The dock tree is retained in a `NowDockSpace`, but the content is
still submitted immediately each frame.

```csharp
using NowUI.Docking;

readonly NowDockSpace _dock = new NowDockSpace();

void OnPostRender()
{
    using (Now.StartUI())
    {
        _dock.Window("Scene", DrawScene, id: "Scene");
        _dock.Window("Hierarchy", DrawHierarchy, id: "Hierarchy");
        _dock.Window("Inspector", DrawInspector, id: "Inspector");

        NowDock.Space(_dock, new NowRect(20, 20, Screen.width - 40, Screen.height - 40), 100)
            .SetMinPaneSize(140)
            .Draw();
    }
}
```

Window callbacks run inside a clipped `NowLayout.Area`, so normal layout calls
work directly:

```csharp
void DrawInspector(NowRect rect)
{
    NowLayout.Label("Inspector").SetFontSize(18).Draw();
    NowLayout.Checkbox("Show grid").Draw(ref _showGrid);
}
```

To seed a startup layout, submit the windows first, then call `Dock` before
drawing the dock space:

```csharp
_dock.Window("Scene", DrawScene, id: "Scene");
_dock.Window("Inspector", DrawInspector, id: "Inspector");
_dock.Window("Console", DrawConsole, id: "Console");

_dock.Dock("Inspector", "Scene", NowDockSide.Right);
_dock.Dock("Console", "Scene", NowDockSide.Bottom);

NowDock.Space(_dock, rect, 100).Draw();
```

Window ids are semantic strings inside `NowDockSpace`; the dock-space control
id is a `NowId`, so use a stable non-zero integer when the dock surface is tied
to data or instantiated repeatedly.

Users can drag tabs onto a panel's tab bar to merge or reorder tabs, or onto an
edge to split. Dragging outside the dockspace detaches the tab as a floating
window; dragging the floating tab back over a pane docks it again. While a tab
is dragged, the workspace stays stable and shows a drop guide; the dock tree is
changed when the tab is released. Moving the empty floating title-bar area only
moves the floating window. Dragging a splitter resizes neighboring panes. The
retained state lives only in `NowDockSpace`, so resetting layouts is explicit:

```csharp
_dock.ClearLayout(); // keep registered window open/closed state
_dock.Reset();       // clear layout and all known windows
```
