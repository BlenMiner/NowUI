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
- `Render` draws custom node bodies for value setters, previews, swatches, and
  other domain UI. The `ctx.bodyRect` passed to custom content is already
  inset away from the port label lanes.
- `ctx.Row(...)` and `ctx.Scale(...)` scale custom content dimensions with the
  canvas zoom. Use them for controls inside nodes instead of fixed screen-pixel
  heights.
- Built-in controls drawn inside `Render` also receive a scoped
  `NowControls.controlScale` equal to the canvas zoom, so their text, padding,
  outlines, handles, and deferred popups stay proportional to the node.

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

Ports carry user-defined integer `typeId` values. By default, `0` is a wildcard
and non-zero ids connect only when they match. A schema can make compatibility
explicit with allow rules:

```csharp
_schema.Allow(Float, Float4);
_schema.AllowSameTypes();
```

For complete control, set `graph.canConnect`:

```csharp
graph.canConnect = (_, _, output, _, input) =>
    output.typeId == Float && input.typeId == Float4;
```

Input ports default to one connection, so dragging a new compatible link onto
an occupied input replaces the existing link. Set `maxConnections` to `0` for
unlimited links.

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
declares built-in Undo, Redo, and Delete Selection items and can be extended
with `drawCustomItems`.

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
inside `Render`/`SetNodeContent` callbacks. Build schemas once, cache textures
and labels, and mutate user-owned value data directly.

Useful controls:

- Drag a node by its title bar with the primary pointer button.
- Drag the empty canvas with the primary pointer button to marquee-select nodes.
- Drag from an input or output port to a compatible opposite port to create a link.
- Right-click a port to remove its connected links.
- Right-click the canvas to open the operation context menu when one is provided.
- Press Ctrl/Cmd+Z or Ctrl/Cmd+Y while the canvas is focused to undo or redo.
- Press Delete while the canvas is focused to delete selected nodes.
- Middle-drag the canvas to pan.
- Use the wheel over the canvas to zoom around the pointer.
