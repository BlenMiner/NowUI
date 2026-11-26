# Capture

`NowCapture` lets outside code see what a NowUI host drew: one
`NowCaptureFrame` per host per completed draw, listing every control and every
visible primitive in paint order. Recorders, accessibility bridges, test
harnesses and UI inspectors use it; drawing code does not change.

```csharp
void OnEnable()  => NowCapture.onFrame += OnNowFrame;
void OnDisable() => NowCapture.onFrame -= OnNowFrame;

void OnNowFrame(NowCaptureFrame frame)
{
    // frame.host is the NowGraphic / NowVisualElement / ... that drew it.
    for (int i = 0; i < frame.count; ++i)
    {
        ref readonly NowCaptureItem item = ref frame[i];
        // item.kind, item.id, item.parent, item.rect, item.text, ...
    }
}
```

Nothing is collected while no handler is attached: every draw and control pays
one static `bool` check (`NowCapture.active` reports the same state).

## Frames

- `onFrame` is raised on the main thread, once per host per **completed** draw
  whose geometry is displayed. Measure passes, layout-input measurement and
  failed or cancelled rebuilds never raise it.
- Retained hosts only raise it when they rebuild. A `NowGraphic` that did not
  change this Unity frame raises nothing, so keep the last frame per host.
- The `NowCaptureFrame` instance and its items are reused. Read or copy what
  you need inside the callback; do not keep the reference. Handler exceptions
  are logged and do not break the host.
- `frame.bounds` is the rect the host passed to its draw callback.
- A host whose rect collapses to zero size publishes one empty frame. A
  disabled or destroyed host publishes nothing further: check
  `host is Behaviour b && b.isActiveAndEnabled` (or `VisualElement.panel`)
  before replaying a stored frame.

| Host | `host` object | Raised from | Space of `rect` |
| --- | --- | --- | --- |
| `NowGraphic`, `NowLayoutGraphic` (and subclasses) | the component | `Graphic.Rebuild(CanvasUpdate.PreRender)` → `UpdateGeometry`, after the CanvasRenderer pages are applied | RectTransform local units, origin at the rect's **top-left**, y down: `(0, 0, rect.width, rect.height)` |
| `NowVisualElement`, `NowLayoutVisualElement` | the element | `generateVisualContent`, after the RenderTexture is rendered | `contentRect` units (points), origin at its top-left |
| `NowWorldGraphic`, `NowWorldLayoutGraphic` | the component | `RebuildNowUI()` | surface UI units, `(0, 0, size)` |
| `NowPipelineGraphic`, `NowPipelineLayoutGraphic` | the component | `BuildDrawList(camera, ...)`, after the shared overlay flush | camera UI units; once per rendering camera |
| `Now.StartUI(...)` | `Camera.current`, or `NowCapture.screenHost` when no camera is rendering | disposing the `NowUIScreenScope` | screen UI units (`Now.screenMask`) |

Editor inspector previews, `NowRenderer` / `NowDrawList` captures and
`NowGUI` / `NowEditorGUI` panels are not capture hosts. An independent
`NowRenderer` used inside a host's draw is excluded from that host's frame.

For a `NowGraphic`, convert an item to RectTransform local coordinates with
`localX = rt.rect.xMin + item.rect.x` and `localY = rt.rect.yMax - item.rect.y`
(top edge). Canvas scale is not applied; the units are the same as
`RectTransform.rect`.

## Items

`NowCaptureKind` is `Control`, `Rect`, `Text` or `Image`.

| Field | Meaning |
| --- | --- |
| `id` | Stable across frames (see [Identity](#identity)); unique within a frame. |
| `parent` | Index of the innermost enclosing `Control` in the same frame, or `-1`. Parents always precede children. |
| `rect` | Bounds in host space with transforms applied. |
| `clip` | Effective clip: ambient masks (and an explicit item mask) intersected with the host bounds. |
| `rotation` | Degrees, clockwise on screen, about the centre of `rect`, from `Now.Rotate`. Straight lines report a rotated bar. |
| `color` | Authored display (sRGB) colour with `Now.Opacity`/`Now.Tint` and the host's colour applied. Fill for Rect/Image, text colour for Text, background (or clear) for Control. A gradient reports the average of its end colours; an outline-only rectangle its outline colour. |
| `fontSize` | Text: font size in host units. |
| `text` | Text: the string as drawn (after the text preprocessor and markup). Control: its label or value. `null` for Secret items. |
| `texture` | Image: texture, sprite texture or Lottie asset name; `NowSdf` for an SDF scene. |
| `control` | Control: `"Button"`, `"Toggle"`, `"Radio"`, `"Switch"`, `"Slider"`, `"TextField"`, `"NumberField"`, `"TextArea"`, `"CodeEditor"`, `"Dropdown"`, `"DropdownItem"`, `"ComboBox"`, `"Tab"`, `"Foldout"`, `"TreeItem"`, `"SelectableRow"`, `"Chip"`, `"ScrollView"`, `"Scrollbar"`, `"ProgressBar"`, `"Splitter"`, `"MaskField"`, `"KeyBindingField"`, `"MenuItem"`, or `"Control"` for an undescribed interaction. |
| `value` | Control: normalized slider/progress/scroll value, `1`/`0` for toggles, tabs, foldouts. |
| `flags` | `Hovered`, `Pressed`, `Focused`, `Disabled`, `Selected`, `Editable`, `Secret`. |

What is reported:

- **Controls**: everything that runs NowUI interaction (`NowInput.Interact`,
  `NowControls.Interact`), so built-in and custom controls alike. Hover and
  press come from the interaction, focus from `NowFocus`. Repeated
  interactions with the same id in a row (several pointer buttons) are one
  item.
- **Rect**: rectangles (rounded, outlined, 9-sliced without texture,
  custom-material), gradients, circles, triangles, polygons, lines,
  polylines and arcs (their bounds).
- **Text**: one item per drawn text run (a wrapped block is one item).
- **Image**: textured rectangles and sprites, Lottie animations, SDF scenes and
  model previews.
- Skipped: fully transparent items, items fully outside their clip, blurred
  rectangles (shadows, glows), glass panes, and the flattened surface of
  texture effects (their contents are reported item by item, before
  deformation). Deferred overlays (popups, menus, tooltips) are reported in
  the frame of the host that queued them, after its content, with parent `-1`.

### Parenting

A control stays open for the primitives and controls that follow it until one
of them is drawn outside its rect (a 2-unit tolerance covers focus rings and
thumbs), the ambient mask or id scope it opened in ends, or the next sibling
control starts. Built-in controls draw their visuals after interacting, so
their background, label and glyphs are its children. A label drawn outside a
control's rect (a slider's caption column) is its sibling, and a control's
`text` falls back to its first direct Text child when it was not described.

### Identity

- Controls hash their `NowResolvedId`, so the same control keeps its id while
  its NowUI identity holds (see [Identity](Identity.md)). Use keyed ids for
  reorderable data, exactly as for focus and state.
- Primitives hash their parent control's id (or the host) and their ordinal
  among the primitives drawn directly inside it. Inserting a primitive
  renumbers only its siblings.
- Collisions are disambiguated deterministically by occurrence.
- Ids are runtime-local like `NowResolvedId`: stable within a session, not a
  persistent key across domain reloads.

## Secret and editable text

`TextField.SetPassword()` masks a field: it draws one `•` (or the given
character) per UTF-16 unit, disables copy/cut, makes double/triple click select
all, and opens the touch keyboard in secure mode. Captured, the field is
`Editable | Secret` with `text == null`, and every item drawn inside it carries
`Secret` with `text == null`.

Text fields, text areas and code editors report `Editable` and their current
text.

## Custom controls

Controls built with [Custom Controls](CustomControls.md) are captured
automatically as `"Control"`. Describe them right after interacting so a
consumer sees their type, value and state; the call does nothing while capture
is off:

```csharp
var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);
NowCapture.Describe(interaction.id, "Rating", value / (float)max,
    value > 0 ? NowCaptureFlags.Selected : NowCaptureFlags.None, $"{value}/{max}");
```

`Describe` applies to the most recently opened control with that id. Pass
`NowCaptureFlags.Secret` for masked content and `NowCaptureFlags.Disabled` for
inert controls.

## Cost

- Capture off: one static check per primitive and control; no allocation.
- Capture on: the frame, item and id buffers are pooled. Text drawn from a
  `string` is reported as that same instance; span and number text reuses the
  string captured for that item last frame while it is unchanged; texture and
  asset names are cached. A steady captured frame does not allocate after
  warmup.
