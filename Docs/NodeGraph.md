# Node Graph

`NowUI.Extensions.NodeGraph` is a retained graph model with an immediate-mode
canvas for shader-style and visual-scripting-style editors. The canvas draws
Bezier links, typed input/output ports, a grid background, draggable nodes,
middle-button panning, wheel zooming, drag selection, and port-to-port link
creation.

```csharp
using NowUI;
using NowUI.NodeGraph;
using UnityEngine;

public sealed class ShaderGraphPanel : MonoBehaviour
{
    const int Float4 = 4;
    const int TextureSample = 100;
    const int FragmentOutput = 200;
    const int Rgba = 10;
    const int BaseColor = 20;

    readonly NowNodeGraph _graph = new NowNodeGraph();
    readonly NowNodeGraphSchema _schema = new NowNodeGraphSchema();
    readonly NowNodeGraphHistory _history = new NowNodeGraphHistory();
    readonly NowNodeGraphContextMenu _menu = new NowNodeGraphContextMenu();

    void Awake()
    {
        _schema.Node(TextureSample, "Texture Sample")
            .SetSize(210, 150)
            .Output(Rgba, "RGBA", Float4)
            .Render(ctx =>
            {
                ctx.Texture(Texture2D.whiteTexture, ctx.Row(0, 56), 4);
            });

        _schema.Node(FragmentOutput, "Fragment Output")
            .Input(BaseColor, "Base Color", Float4);

        _graph.AddNode(_schema, TextureSample, new Vector2(64, 80), id: "texture");
        _graph.AddNode(_schema, FragmentOutput, new Vector2(360, 96), id: "output");
        _graph.TryAddLink("texture", Rgba, "output", BaseColor);
    }

    void OnPostRender()
    {
        using (Now.StartUI())
        {
            NowNodes.Canvas(_graph, new NowRect(24, 24, Screen.width - 48, Screen.height - 48))
                .SetSchema(_schema)
                .SetHistory(_history)
                .SetContextMenu(_menu)
                .Draw();
        }
    }
}
```

The recommended API is schema-first:

- `kindId` is your integer node type, such as `TextureSample` or `Multiply`.
- Port ids are user-defined integers passed to `Input`, `Output`, and link APIs.
- `typeId` is your integer value type, such as `Float`, `Float4`, or `Texture`.
- `userId` is an optional integer handle back to your own data model.
- For small serialized node metadata, use `node.SetData(key, value)`,
  `node.GetData(key)`, `node.TryGetData(key, out value)`, and
  `node.RemoveData(key)`. Keyed data is copied, pasted, and restored through
  graph history with the node.
- `Render` draws custom node bodies for value setters, previews, swatches, and
  other domain UI. The `ctx.bodyRect` passed to custom content is already
  inset away from the port label lanes.
- `ctx.bodyRect`, `ctx.Row(...)`, and `ctx.Scale(...)` are graph-space. Draw
  normal node-local controls with those rects; the canvas transform scales them
  with zoom automatically. Use `ctx.GraphToScreen(...)`,
  `ctx.ScreenToGraph(...)`, `ctx.GraphVectorToScreen(...)`, and
  `ctx.ScreenVectorToGraph(...)` when a node body needs to anchor an overlay,
  tooltip, popup, or other screen-space behavior. These conversions include
  both graph zoom/pan and any transform applied by a parent view.
- `ctx.Row(index, height, gap)` is for uniform-height rows. For mixed-height
  controls, use `ctx.RowAfter(previousRect, height, gap)` or
  `ctx.RowAt(yOffset, height)` so shorter status rows do not overlap taller
  fields.
- `ctx.bodyRect` and the regular row helpers reserve lanes for port labels.
  For a picker, text field, or other control placed above the ports, use
  `ctx.FullWidthRow(...)`, `ctx.FullWidthRowAfter(...)`, or
  `ctx.FullWidthRowAt(...)`. These span the node's padded width, and the canvas
  keeps the matching `SetContentHeight(...)` area out of node dragging so the
  control receives pointer input across its full width.
- When a control inside `Render` mutates `ctx.node` or graph data, call
  `ctx.RecordChange()` before the mutation. It records the current graph in
  the canvas history when one is provided and marks the canvas result as
  changed. Use `ctx.MarkChanged()` only for changes that are tracked elsewhere
  or do not need undo.
- Use `SetSearchKeywords(...)` on node definitions for aliases, symbols, and
  domain terms that users may type in the Space search palette, such as
  `if`, `print`, `+=`, or `local`.
- Use `SetCategory(...)` on node definitions when a schema grows beyond a few
  nodes. The Space search palette shows the category as muted secondary text
  and matches it in search queries, so users can type broad groups such as
  `Flow`, `Math`, or `Variables`.
- Use `SetSearchDetail(...)` for concise secondary text in the Space search
  palette, such as a method signature or short node description. Details are
  searchable and render on a second line only for rows that need them.
- If a custom node body puts controls above its ports, call
  `SetContentHeight(height)` on the node definition with the same graph-space
  height. The canvas gives render callbacks a `bodyRect` clamped to that
  height, moves port hit/draw rows below it, expands the node's minimum height,
  and keeps that top control area out of node dragging. Pair it with
  `SetWidth(width)` when you want the graph to compute the node's height from
  its title, content, and ports instead of guessing a total
  `SetSize(width, height)` by hand. `SetPortTopOffset(height)` remains available
  for lower-level port-row offset behavior when you do not want the callback
  `bodyRect` clamped.
- Use `Initialize(node => ...)` on a node definition when a created node needs
  per-definition metadata after its size, color, ports, renderer, and preview
  have been applied. This is useful for schema search entries that create a
  preconfigured node rather than a blank generic kind.
- Build schemas once, or call `schema.Clear()` before rebuilding an existing
  schema instance. `Input` and `Output` update an existing port when the same
  port id is reused, and append when the id is new. Use `RemoveInput(id)` and
  `RemoveOutput(id)` for conditional schema ports, or `ClearPorts()` before
  intentionally replacing the full port shape. Use `graph.Clear()` when you want
  to reset nodes, links, and selection while keeping the graph object alive.
- Existing serialized nodes keep their stored `node.size`, so changing a node
  definition later will not silently resize old graphs. If your editor does not
  expose manual node resizing and you want old nodes to adopt the current
  schema dimensions again, call `graph.ResetNodeSizesToSchema(schema)`.
- Treat node port ids as stable keys. `node.AddInput(...)` and
  `node.AddOutput(...)` return and update the existing port when the id already
  exists instead of appending a duplicate, which keeps link resolution and hit
  testing unambiguous. Schema definitions support both integer ids and named
  string ids through `Input(...)`, `Output(...)`, `RemoveInput(...)`, and
  `RemoveOutput(...)`.
- For dynamic node instances whose ports change from node-local state, prefer
  `graph.UpsertNodeInput(...)`, `graph.UpsertNodeOutput(...)`,
  `graph.RemoveNodeInput(...)`, and `graph.RemoveNodeOutput(...)`. These mutate
  the node and immediately clean links that became stale, incompatible,
  duplicate, or over the current connection limit. Use the lower-level
  `node.UpsertInput(...)`, `node.UpsertOutput(...)`, `node.RemoveInput(...)`,
  and `node.RemoveOutput(...)` only while constructing detached nodes or when
  you intentionally want to defer cleanup until an explicit
  `graph.PruneInvalidLinks()` call.

The graph still stores node ids as strings so existing links remain stable, but
the schema and link helpers let you avoid stringly typed ports:

```csharp
_graph.AddNode(_schema, TextureSample, new Vector2(64, 80), userId: 42, id: "texture");
_graph.TryAddLink("texture", Rgba, "output", BaseColor);
```

The graph framework is split from rendering. `NowNodeGraph` and
`NowNodeGraphSchema` own data and rules; the canvas owns interaction, layout,
selection, panning, zooming, and link creation; `INowNodeGraphRenderer` owns the
pixels. The built-in renderer is `NowNodeGraphDefaultRenderer`, which draws with
NowUI primitives, including `SetOutline` for node borders.

The default look uses the theme's elevation shadows under nodes (selected
nodes lift to the overlay level; disable with `style.nodeShadows = false`),
tints the title bar with `node.color` when one is set (the body keeps the
surface color), and emphasizes the hovered node's border. Ports draw filled
when connected and as hollow rings when empty; while a link drag is active,
incompatible ports dim and compatible ones grow. Hovered connections thicken
slightly and the selected connection brightens over a soft halo. Node and
port contexts carry `hovered`, `connected`, and the link context carries
`hovered`/`selected`, so custom renderers can restyle all of it.

Node drags snap to the graph grid by default, matching Unity Visual Scripting's
Snap to Grid preference. Grid snapping uses `style.gridSpacing` (default 24
graph units), so zooming does not change where nodes settle. Normal node boxes
also round their rendered width and height up to grid multiples, keeping their
right and bottom edges on the same grid. Turn snapping off with
`style.snapNodes = false` or `SetNodeSnapping(false)`. Hold Alt/Option while
dragging for temporary free placement.

For Figma-style magnetic alignment instead, set
`style.nodeSnapMode = NowNodeSnapMode.Align` or call
`SetNodeSnapMode(NowNodeSnapMode.Align)`. Alignment mode snaps to nearby node
edges and centers, draws a dashed guide line for the active snap, and uses
graph-unit thresholds: `style.nodeSnapDistance` is the lock distance (default
12 units), and `style.nodeSnapProximity` limits which nearby row or column can
contribute guides (default 64 units).

Use `NowNodeGraphView` when you want one object to carry the graph, schema, and
renderer:

```csharp
var view = new NowNodeGraphView(_graph, _schema)
    .SetRenderer(new ShaderGraphRenderer())
    .SetHistory(_history)
    .SetContextMenu(_menu);

NowNodes.Canvas(view, rect).Draw();
```

Custom renderers can override the whole graph look without reimplementing graph
behavior:

```csharp
sealed class ShaderGraphRenderer : NowNodeGraphDefaultRenderer
{
    public override void DrawNode(in NowNodeGraphNodeContext ctx)
    {
        Now.Rectangle(ctx.nodeRect)
            .SetColor(ctx.selected ? Color.gray : Color.black)
            .SetRadius(5)
            .Draw();
    }
}
```

For semantic title colors, override
`ResolveNodeTitleColor(in NowNodeGraphNodeContext context)` on
`NowNodeGraphDefaultRenderer`. This keeps category coloring in the renderer and
avoids mutating serialized `node.color` values during drawing.

## Previews

A node definition can declare a preview: an extra content area at the bottom
of the node that the user can minimize with the chevron in the title bar.

```csharp
_schema.Node(Sine, "Sine")
    .Input(PortIn, "In", Scalar)
    .Output(PortOut, "sin(in)", Scalar)
    .Preview(ctx => DrawSparkline(ctx), height: 64f);
```

- The preview renderer receives the same reused `NowNodeContentContext` as
  `Render`, with `ctx.isPreview` set and `ctx.bodyRect` covering the preview
  area. The preview adds its height to the node, so authored node sizes stay
  preview-agnostic.
- Custom renderers receive `hasPreview` and `previewRect` in the node
  context; the default renderer draws a separator line above the preview.

Minimizing is the compact toggle in every node's title bar (a minus that
becomes a plus): compact mode strips the body content and preview, shrinking
the node to its title and ports — and narrows very wide nodes — for dense
graphs. `node.compact` serializes and round-trips history, toggling reports
`compactToggled`, and the node context carries `compact` and
`compactToggleRect` (`DrawCompactToggle` is the virtual to restyle it).

## Reroute nodes

`schema.Reroute(kindId, title, typeId)` registers a tiny pass-through node for
tidying wires: it draws as a 26px pill (no title bar, no toggles), is dragged
by its body, and exposes one input and one output at its left/right center
(`NowNodeGraphSchema.RerouteInputPortId` / `RerouteOutputPortId`; the output
fans out to any number of links). Give the evaluator a pass-through handler:

```csharp
_schema.Reroute(RerouteKind);
_evaluator.Kind(RerouteKind, ctx => ctx.Input(NowNodeGraphSchema.RerouteInputPortId));
```

## Node search

Pressing Space over a focused canvas (with a schema attached) opens a
command-palette-style search at the pointer: type to filter node titles,
`SetCategory(...)`, and `SetSearchKeywords(...)`, Up/Down to highlight, Enter
or click to create the node at the position where the palette opened, Escape
or an outside click to dismiss. The query is a standard `NowTextField` (caret,
selection, clipboard, and IME behave like any other field). Recently created
kinds sort first while the query is empty, so re-adding the last node is
Space+Enter. Opening reports `searchOpened` in the canvas result.
The palette shows 8 results by default; schemas with large action or method
catalogs can raise this with `SetSearchResultLimit(maxResults)`.
When a query is active, results are ranked by relevance: title matches outrank
keyword matches, keyword matches outrank detail matches, detail matches outrank
category matches, and schema order is preserved for ties.

The search ignores only the input event that opened it. This avoids Space being
treated as both "open search" and "submit", while still letting the next Enter
press create the highlighted node. Passive measurement/layout passes do not
close or mutate the search state.

**Drag a wire into empty canvas** to open the same palette wired to the port
you dragged from: the list is filtered to node kinds that expose a compatible
opposite-direction port (honoring the schema's `Allow`/`AllowSameTypes` rules),
and choosing one creates the node at the drop point and auto-connects it — the
fastest way to grow a graph outward. This applies only to fresh drags; dragging
an existing input's wire out and dropping on empty still unplugs it. Turn it off
with `SetDropToSearch(false)`.

## Port types, colors, and conversions

Ports carry user-defined integer `typeId` values. By default, `0` is a wildcard
and non-zero ids connect only when they match. A schema can make compatibility
explicit with allow rules:

```csharp
_schema.Allow(Float, Float4);
_schema.AllowSameTypes();
```

Register a color per type id and both port dots and connections pick it up
(explicit `port.color` still wins):

```csharp
_schema.TypeColor(Scalar, new Color(0.38f, 0.75f, 1f, 1f));
_schema.TypeColor(Integer, new Color(1f, 0.62f, 0.3f, 1f));
```

A link between ports of different types — an allowed conversion such as
`Allow(Scalar, Integer)` — draws with a gradient from the output type's color
to the input type's color, so conversions are visible at a glance. The link
drag preview shows the same gradient while hovering a compatible port and
turns red over an incompatible one. Links between untyped or same-colored
ports keep the solid `style.connection` color. Custom renderers get both ends
via `NowNodeGraphLinkContext.color` and `colorTo`; the default renderer draws
them with the core `NowLine.SetGradient` support.

For complete control, set `graph.canConnect`:

```csharp
graph.canConnect = (_, _, output, _, input) =>
    output.typeId == Float && input.typeId == Float4;
```

Input ports default to one connection, so dragging a new compatible link onto
an occupied input replaces the existing link. Set `maxConnections` to `0` for
unlimited links.

Use `graph.TryCreateLink(...)` or `graph.CanAddLink(...)` for custom previews
and drop validation. They check type compatibility and current connection
limits, matching `TryAddLink(...)`; when replugging an existing connection, pass
that picked link as the ignored link so the source port's own limit does not
block the preview.

## Evaluation

`NowNodeGraphEvaluator<T>` turns a graph into values. Register one handler per
node kind, pull upstream values inside handlers with `ctx.Input(portId)`, and
ask for any node's value:

```csharp
var evaluator = new NowNodeGraphEvaluator<float>()
    .Kind(Constant, ctx => ctx.node.userId / 1000f)
    .Kind(Add, ctx => ctx.Input(PortA) + ctx.Input(PortB))
    .Kind(Multiply, ctx => ctx.Input(PortA, 1f) * ctx.Input(PortB, 1f));

float result = evaluator.Evaluate(_graph, "sum");
```

- `Evaluate(graph, nodeId)` evaluates the node's first output port; overloads
  take an explicit output port id. `TryEvaluate` reports whether the node,
  port, and handler exist.
- `ctx.Input(portId, fallback)` follows the link into that input port and
  evaluates the upstream node. Unconnected ports, unregistered upstream kinds,
  and cycles all resolve to the fallback instead of failing, so a graph stays
  evaluable while the user is mid-edit.
- Each top-level `Evaluate` call memoizes every visited output port, so
  diamond-shaped graphs evaluate shared upstream nodes once. The memo resets
  on the next top-level call — mutate your value state (sliders, fields) and
  re-evaluate freely each frame.
- `graph.TryGetInputLink(nodeId, portId, out var link)` exposes the same
  link-following the evaluator uses when you need to walk dependencies
  yourself.

Store compact numeric state in `node.userId`, small serialized strings in
`node.SetData(key, value)`, or larger domain objects in your own dictionary
keyed by `node.id`. `userId` and keyed node data both round-trip through
history snapshots, copy/paste, and duplicate, so undo/redo restores values
edited through node content controls.

`NowMathGraphExample` (`Assets/Scenes/MathGraph.unity`) is a live math
playground built on the evaluator: an `X` variable node, constant sliders, and
arithmetic nodes feed a plot node that samples `f(x)` across its domain and
draws the curve with sample points, an axis cross, and a scrubbable preview
marker — rewiring the graph or dragging a constant reshapes the curve
immediately.

## Operations

Undo/redo is explicit so graph owners can use the same history for framework
and application-owned changes:

```csharp
_history.Record(_graph);
_graph.AddNode(_schema, TextureSample, position, id: "texture-2");

_history.Undo(_graph);
_history.Redo(_graph);
```

Pass the same `NowNodeGraphHistory` to the canvas to record node drags, link
creation, link removal, and context-menu deletes. `NowNodeGraphContextMenu`
declares built-in Copy, Cut, Paste, Duplicate, and Delete items and can be
extended with `drawCustomItems`; Undo/Redo menu items are off by default
(the keyboard shortcuts always work) — set `undoRedo = true` to show them.

Dragging from an input port that already has a connection picks that
connection up instead of starting a new one: the wire follows the pointer
from its output end, dropping it on another compatible input rewires it,
dropping it anywhere else unplugs (deletes) it, and dropping it back on the
same port is a no-op that records no history.

Links are selectable: clicking a connection selects it (clearing the node
selection — `graph.selectedLink` holds it), Delete removes it, and clicking
empty canvas or any node deselects it. Escape cancels an in-flight link drag
or marquee, Ctrl/Cmd+A selects every node, and `F` frames the selection (or
the whole graph when nothing is selected) with the zoom clamped at 100%.

Copy, cut, paste, and duplicate work on the node selection through
`NowNodeGraphClipboard`. The canvas and context menu use
`NowNodeGraphClipboard.shared` by default — pass your own instance with
`SetClipboard` (canvas or view) to isolate an editor. Copies capture the
selected nodes plus the links between them; pasting clones with fresh node
ids, remaps those links, keeps the relative layout centered on the paste
position (the pointer for the hotkey, the click position for the context
menu), and selects the pasted nodes. `NowNodeGraphClipboard.Duplicate(graph,
offset)` clones the selection in place without touching the shared clipboard.

When the canvas has focus, it consumes the same text-input shortcuts as the
code editor: Ctrl/Cmd+Z for undo, Ctrl/Cmd+Y for redo, and Shift+Ctrl/Cmd+Z
for redo. Delete removes the current node selection; if a history object is
provided, the deletion is undoable.

## Performance notes

The canvas is immediate-mode but keeps transient state in `NowControlState`, so
steady-state drawing is allocation-free after warmup for the framework-owned
path. First use allocates control state, node content context storage, and any
user-created graph/schema objects. Schema renderers receive a reused
`NowNodeContentContext`; use it only during the callback and store your own data
by `node.id` or `userId`.

Avoid creating strings, lambdas, textures, lists, or temporary data objects from
inside `Render`/`SetNodeContent` callbacks. Build schemas once, cache textures,
use `Now.Text(...).Draw(value, "0.00")` for simple numeric labels, use a
stack-backed `NowTextBuffer` for composed dynamic labels such as
`f(x) = value`, and mutate user-owned value data directly.

Useful controls:

- Drag a node by its title bar with the primary pointer button.
- Hold Alt/Option while dragging a node to bypass node snapping.
- Drag the empty canvas with the primary pointer button to marquee-select nodes.
  Hold Shift to add to the selection, or Ctrl/Cmd to subtract from it.
- Drag from an input or output port to a compatible opposite port to create a link.
  Releasing near a compatible port snaps to it, so you don't have to hit it exactly
  (the range is `style.portSnapRadius`; `style.portHitRadius` sizes the press target
  — raise both for touch with `SetPortHitRadius`).
- Drag an occupied input port to pick up its connection: drop on another input
  to rewire, drop on empty canvas to unplug, drop back to keep it.
- Drag a fresh wire onto empty canvas to open the connection-aware node search.
- Click a connection to select it; press Delete or Backspace to remove it.
- Click the minus/plus in a node's title bar to minimize it to title and
  ports (hiding body content and preview) or restore it.
- Press Escape to cancel a link drag or marquee selection.
- Press Ctrl/Cmd+A to select all nodes, or F to frame the selection (the
  whole graph when nothing is selected).
- Nudge selected nodes with the arrow keys (Shift for the larger step; tune with
  `SetNudge`). A hold-burst is one undo entry.
- Press Space to open the node search palette at the pointer; type to filter,
  Enter/Submit to add, or Escape/Cancel to close. Recently added kinds are
  offered first.
- Press Ctrl/Cmd+C, X, V, or D while the canvas is focused to copy, cut,
  paste (at the pointer), or duplicate the selected nodes.
- Right-click a port to remove its connected links.
- Right-click the canvas to open the operation context menu when one is provided.
- Press Ctrl/Cmd+Z or Ctrl/Cmd+Y while the canvas is focused to undo or redo.
- Press Delete or Backspace while the canvas is focused to delete the selection.
- Middle-drag OR right-drag the canvas to pan (a right *click* still opens the
  context menu). Enable `SetPanWithPrimaryDrag(true)` so a one-finger/primary drag
  pans on touch (marquee then needs Shift).
- Use the wheel over the canvas to zoom around the pointer.
