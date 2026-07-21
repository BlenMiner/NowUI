# Controls

NowUI's immediate-mode controls: buttons, checkboxes, radios, sliders, text
fields, dropdowns and scroll views, built entirely on public primitives so
custom controls are first-class citizens. Controls work identically in the
screen path (`Now.StartUI`), inside UGUI (`NowGraphic` for explicit rects or
`NowLayoutGraphic` for layout), and in URP/HDRP overlays — pointer, touch,
keyboard and gamepad included.

Controls live where drawing already lives: `NowLayout.*` flows in the active
layout group (like `NowLayout.Label`), `Now.*` takes an explicit rect (like
`Now.Text`), `NowTheme` owns the ambient theme, and `NowControls` is the
shared toolkit for id scopes and interaction plumbing.

## API conventions

Every control follows the same three rules, so one control teaches all of
them:

- **Dual factories.** Each control exists both as `Now.X(rect, ...)` for
  explicit placement and `NowLayout.X(...)` for layout flow, with identical
  configuration methods. Identity comes from the call site; use `SetId` when
  looped items can reorder.
- **Actions return `bool` from `Draw()`.** Controls that trigger something
  (`Button`) return true on click or on submit while focused.
- **Values are caller-owned, passed by ref.** Controls that edit a value
  (`Checkbox`, `Slider`, `TextField`, `Dropdown`, ...) take `Draw(ref value)`,
  mutate the ref, and return true when the value changed this frame. NowUI
  never stores your data — state you can't see stays limited to ephemeral
  interaction details (caret position, open popups, hover fades).

`TextField.Draw` returns a `NowTextFieldResult` because text fields also have a
useful resolved rect and a distinct Enter/Return (or touch-keyboard Done)
submission event. The result
converts to `bool` as `changed`, so existing `if (field.Draw(ref value))` code
keeps exactly the convention above. Read `result.submitted` when committing an
unchanged value should still perform an action, and `result.rect` when drawing
an adornment inside a layout-flowing field.

## Using the built-in controls

Layout-flowing controls reserve space sized from their themed content; values
stay owned by you, passed by ref.

```csharp
using (NowLayout.Column(NowScreen.safeArea).Padding(16).Gap(8).Begin())
{
    NowLayout.Label("Settings").SetBold().SetFontSize(18).Draw();
    NowLayout.Lottie(spinnerAsset).SetTime(Time.time).SetHeight(24).Draw();

    if (NowLayout.Button("Save").Draw())
        Save();

    NowLayout.Checkbox("Enable shadows").Draw(ref shadows);

    if (NowLayout.Radio("Low", quality == 0).Draw()) quality = 0;
    if (NowLayout.Radio("High", quality == 1).Draw()) quality = 1;

    NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref volume);

    NowLayout.FloatField().SetRange(0f, 100f).Draw(ref speed);
    NowLayout.IntField().SetRange(0, 99).Draw(ref lives);
    NowLayout.Vector3Field().Draw(ref spawnPosition);
    NowLayout.ColorPicker().SetShowAlpha(false).Draw(ref tint);
    NowLayout.GradientField().Draw(ref ramp);
    NowLayout.AnimationCurveField().SetTimeRange(0f, 1f).Draw(ref falloff);
    if (NowLayout.OpenFileField().SetExtensions("json", "txt").Draw(ref loadPath))
        Load(loadPath);
    if (NowLayout.SaveFileField().SetFilter("Json", "json").Draw(ref savePath))
        Save(savePath);
    NowLayout.DirectoryField().Draw(ref outputDirectory);

    NowLayout.TextField().SetPlaceholder("Name...").Draw(ref playerName);

    NowLayout.TextArea().SetPlaceholder("Notes...").SetLines(3, 8).Draw(ref notes);

    NowLayout.Dropdown(resolutionNames).Draw(ref resolutionIndex);
    NowLayout.EnumDropdown<Quality>().Draw(ref quality);
    NowLayout.EnumFlags<RenderFlags>().Draw(ref renderFlags);
    NowLayout.MaskField(channelNames).Draw(ref channelMask);
    NowLayout.LayerMaskField().Draw(ref hitLayers);

    NowLayout.Foldout("Advanced").Draw(ref advancedOpen);
    if (advancedOpen)
        DrawAdvancedSection();

    using (NowLayout.ScrollView().SetHeight(160).Begin())
        foreach (var line in logLines)
            NowLayout.Label(line).Draw();

    NowLayout.Switch("Notifications").Draw(ref notifications);

    NowLayout.ProgressBar(downloadProgress).SetStretchWidth().Draw();
    NowLayout.ProgressBar().SetIndeterminate().SetTime(Time.time).Draw();

    NowLayout.Badge("3").SetStyle(NowRectangleStyle.Danger).Draw();
    if (NowLayout.Chip("Filter: Active").SetSelected(filtered).Draw()) filtered = !filtered;

    NowLayout.FloatField().SetRange(0f, 10f).SetSpinner(0.5f).Draw(ref spacingValue);

    NowLayout.TabBar(pageNames).Draw(ref page);

    NowLayout.ComboBox(countryNames).Draw(ref countryIndex);

    NowLayout.DatePicker().SetToday(DateTime.Today).Draw(ref dueDate);
    NowLayout.TimePicker().Set24Hour(false).Draw(ref alarmTime);

    NowLayout.KeyBindingField().Draw(ref jumpKey);
}
```

- `Label(text)` flows themed text; builder styling (`SetFontSize`, `SetBold`,
  `SetColor`) overrides the theme presets per call.
- `Lottie(asset)` reserves a layout box for a vector animation; time always
  comes from the caller — `SetTime(Time.time)` plays, `SetNormalizedTime`
  pins a frame for scrubbing (no hidden clock).
- `Switch(...).Draw(ref value)` is the toggle-switch twin of `Checkbox` — same
  contract, sliding-knob visual.
- `ProgressBar(value01)` draws a determinate fill; `SetIndeterminate().SetTime(t)`
  sweeps, with the phase derived entirely from the caller-passed time (no
  hidden clock).
- `Badge(text)` is a non-interactive pill; `Chip(text)` is selectable and
  optionally removable via `SetRemovable().Draw(out bool removed)` — the return
  value reports click/submit only, removal comes solely from the out parameter.
- `SetSpinner(step)` on numeric text fields adds increment/decrement buttons
  with press-and-hold repeat; up/down navigation steps while focused.
- `TabBar(labels).Draw(ref index)` is a caller-owned tab strip;
  `TabView(labels).Begin(ref index)` adds a masked page area below the bar.
- `SplitView(rect).Begin(ref ratio)` returns a plain result describing two panes
  with a draggable, focusable divider. The result itself is not disposable;
  `BeginFirst()`/`BeginSecond()` return the scopes that open each pane.
- `TreeView(state).Begin()` renders collapsible rows; expansion and selection
  live in a caller-owned `NowTreeViewState`.
- `ComboBox(options).Draw(ref index)` is a searchable dropdown: open it and
  type to filter, up/down highlight, submit commits.
  `Draw(ref string value)` stores the selected option text directly; combine it
  with `SetAllowCustomValue()` when a filter that is not in the option list
  should be accepted as a free-form value. Use `SetPopupMinWidth(width)` when
  the field is compact but the searchable options need more room, such as
  method names or file paths. Use `SetOptionDetails(details)` for secondary
  searchable text such as method signatures; popup rows become two-line only
  when details exist.
- `DatePicker().Draw(ref DateTime)` opens a calendar popup (only the date
  component changes; pass `SetToday(DateTime.Today)` for the today ring —
  caller-passed by design). Clicking the header label zooms out to a month
  grid and then a 12-year grid for fast navigation; picking a year or month
  drills back in, and Escape steps one zoom level back before closing.
- `TimePicker().Draw(ref TimeSpan)` edits time with a clock dial: the header
  shows clickable hour and minute segments, tapping or dragging the dial sets
  the active one, and releasing on an hour auto-advances to minutes. 24-hour
  mode uses a dual ring (outer 1–12, inner 13–00); `Set24Hour(false)` adds
  AM/PM chips, and `SetMinuteStep(n)` snaps the dial.
- `NowTooltip.For(rect, "help text")` attaches a hover/long-press tooltip to
  any rect; it renders as a passive overlay that never blocks the pointer.
- Context menus taller than the view clamp their height and scroll (mouse
  wheel, or the top/bottom hover strips) so every option stays reachable;
  submenus clamp and scroll independently. Scrolling over an open menu
  scrolls it; scrolling elsewhere closes it.
- `Button(...).Draw()` returns true on click or on submit while focused.
- `SelectableRow(...).SetSelected(isSelected).Draw()` is the lightweight list
  row primitive. The caller owns selection state; the control only handles
  focus, hover/pressed/selected visuals, and activation.
- `Checkbox(...).Draw(ref value)` / `Slider(...).Draw(ref value)` mutate the
  ref and return true when it changed.
- `FloatField` / `IntField` are typed text-field helpers with optional
  `SetRange(...)`; `Slider(...).Draw(ref int)` snaps to whole numbers, and
  `Slider(...).SetStep(step)` snaps floats to increments.
- `Vector2Field`, `Vector3Field`, `Vector4Field`, `Vector2IntField` and
  `Vector3IntField` draw component fields for Unity vector structs;
  `VectorField().Draw(ref Rect)` / `Draw(ref RectInt)` draw X Y W H rows for
  rect structs.
- Numeric text fields also bind wide types: `TextField().Draw(ref double)` and
  `Draw(ref long)` mirror the float/int helpers, including `SetRange(...)` and
  `SetSpinner(...)`.
- `Foldout(label)` is a collapsible section header. `Draw(ref bool)` edits
  caller-owned expansion and returns true when toggled; `Draw()` keeps the
  expansion in control state and returns whether the section is open.
- `MaskField(options).Draw(ref int mask)` is the multi-select dropdown twin of
  `Dropdown` — option i toggles bit `1 << i`, with Nothing/Everything rows, and
  the popup stays open while toggling. The closed field lists the selected
  names ("Music, Effects") while they fit its label area, falling back to
  "Mixed (n)". `LayerMaskField().Draw(ref LayerMask)` edits layer masks
  against the project's named layers (Everything stores -1, like Unity).
- `ColorPicker` draws a compact swatch field and opens an overlay HSV picker
  with shader-backed saturation/value, hue, optional alpha editing, editable
  hex copy/paste, and Unity-style RGBA channel sliders. Selection applies on
  the next frame's Draw, matching dropdown popup behavior.
- `GradientField` edits Unity `Gradient` values with a texture-backed compact
  gradient preview, Unity-style alpha handles above the ramp and color handles
  below it, add-key-on-double-click, selected-key Location/Alpha/trash controls,
  Delete-key removal, and the core color picker for selected color keys.
- `AnimationCurveField` / `CurveField` edit Unity `AnimationCurve` values with a
  compact curve preview, a Unity-style popup editor, draggable keys and tangent
  handles, add-key-on-double-click, selected-key Time/Value fields, Smooth/Linear/
  Step/Flat tangent commands, exact step preview, trash/Delete-key removal, and
  optional `SetTimeRange(...)` / `SetValueRange(...)` bounds.
- `KeyBindingField` edits an Input System `Key` (game-settings rebinding):
  click it (or press Enter while focused) to capture, and the next key pressed
  becomes the binding. Escape cancels, Delete/Backspace clears to `Key.None`
  (disable via `SetAllowClear(false)`), and a pointer press outside cancels.
  Key names come from the active keyboard layout via `NowKeyNames.GetName`,
  which is public for building your own binding lists.
- `Radio(label, isOn).Draw()` returns true when clicked; set your selection in
  response.
- `TextField` supports click/drag selection (shaped-text cluster aware),
  shift-click to extend the selection, standard editing keys with repeat,
  copy/cut/paste/select-all, undo/redo (Ctrl/Cmd+Z, Ctrl+Y/Cmd+Shift+Z),
  double-click select-all, placeholder text (visible while an empty field is
  focused), IME composition (rendered inline at the caret, underlined), and
  the mobile on-screen keyboard. Enter commits and blurs; Escape reverts the
  field to the value it had when it gained focus, then blurs. Word and line
  navigation follow the platform: Ctrl+arrows jump words on Windows/Linux,
  while macOS uses Option+arrows for words and Cmd+arrows for line ends.
- `TextArea` is the multi-line editor: word-wrapped with every character
  preserved, caret up/down with a pixel goal column, Home/End per line and
  Ctrl+Home/End per document, shift-selection on every movement, click/drag,
  double-click word and triple-click line selection, shift-click extension,
  undo/redo, Enter inserts a newline (Escape blurs), IME composition,
  multi-line clipboard, and a height that grows with content between
  `SetLines(min, max)` with scroll-to-caret, wheel scrolling, and a draggable
  scrollbar thumb.
- `Dropdown` opens an overlay popup that blocks input underneath, scrolls when
  long (clamped to the visible view), and closes on selection, outside press,
  or cancel — the dismissing press is consumed and never activates the control
  beneath. While open, arrow keys move the highlight, Return commits,
  and typing jumps to the first option starting with that letter. Selection
  applies on the next frame's Draw.
- `EnumDropdown<TEnum>` wraps dropdown selection for enum values, and
  `EnumFlags<TEnum>` draws checkboxes for single-bit flag values.
- `OpenFileField`, `SaveFileField`, `DirectoryField`, and the generic
  `FilePicker(mode)` open built-in overlay file popups. Use `SetExtensions(...)`,
  `SetFilter(name, ...)` or `SetFilters(...)` for file filters,
  `SetStartDirectory(...)` for the initial folder, `SetDefaultExtension(...)`
  for save paths, and `SetPopupSize(...)` for larger browsers. They return true
  when the selected path changes; loading and saving the file contents remains
  caller-owned.
- `ScrollView` scrolls with the wheel while hovered and with the scrollbar
  thumb; content size is the layout group's measured extent. A layout host
  resolves it in the same rebuild; a one-pass host uses the previous
  measurement. Bars appear per axis when content
  exceeds the space it was measured against, so a vertical bar reserving
  its gutter never flashes a phantom horizontal bar. Focus navigation can
  move to clipped children and scrolls the viewport to reveal the focused
  control. The scope exposes `scrollOffset`, `maxScrollOffset`,
  `ScrollToEnd()` and `verticalScrollbarVisible` /
  `horizontalScrollbarVisible` for programmatic control (e.g. chat
  stick-to-bottom).
- Scroll views auto-scroll while a scroll-aware drag (text selection, or any
  custom gesture that calls `NowScrollView.RequestDragScroll()` on its
  dragging frames) holds the pointer near or past the viewport edge — speed
  grows with distance, browser-style. Middle-click autoscroll also works like
  a browser: a middle press drops an anchor (drawn via the themable
  `DrawScrollPanAnchor` renderer hook) and the view pans with speed
  proportional to the pointer's distance from it; press-drag-release pans
  once, while a middle click with no drag keeps panning until any button
  press ends it.

## Inspector

`NowLayout.Inspector().Draw(ref target)` (or `Now.Inspector(rect)` at an
explicit rect) renders Unity-style label + control rows for any serializable
type through reflection — built for settings screens, debug panels and modding
UI where the edited types aren't known at compile time.

```csharp
[Serializable]
class GameSettings
{
    [Header("Profile")]
    public string playerName = "Player One";
    [Range(1, 99)] public int level = 12;
    public Difficulty difficulty = Difficulty.Normal;   // enum dropdown
    public SpawnAreas spawnAreas = SpawnAreas.Ground;   // [Flags] mask dropdown
    public LayerMask hitLayers = ~0;

    public Vector3 offset;
    public Quaternion facing = Quaternion.identity;     // edited as Euler angles
    public Color tint = Color.white;
    public AnimationCurve falloff = AnimationCurve.Linear(0, 0, 1, 1);

    public GraphicsSection graphics = new();            // nested foldout
    public List<string> tags = new();                   // resizable list
}

GameSettings _settings = new();

void DrawSettings()
{
    if (NowLayout.Inspector().Draw(ref _settings))
        ApplySettings(_settings);
}
```

- Fields follow Unity's serialization rules: public fields plus
  `[SerializeField]` non-public ones, minus `[NonSerialized]`,
  `[HideInInspector]`, readonly and static fields. Base-class fields come
  first and names nicify like Unity (`m_PlayerName` → "Player Name").
- `[Header]`, `[Space]`, `[Range]` (slider), `[Min]`, `[TextArea]` and
  `[Multiline]` are honored.
- Every value control maps to its built-in twin: bool → checkbox, numbers →
  typed text fields (all widths, including double/long), string → text
  field/area, enums → dropdown or mask dropdown for `[Flags]`, Color/Color32,
  vectors, Rect/RectInt, Bounds/BoundsInt, Quaternion (as Euler, drift-free),
  Gradient, AnimationCurve, LayerMask, DateTime/TimeSpan
  (pass `SetToday(...)` for the calendar's today ring — caller-passed by
  design).
- Nested serializable classes/structs render behind foldouts; arrays and
  `List<T>` get a foldout, a size field, per-element rows and +/− buttons.
  Null strings draw as empty; null lists and nested classes are auto-created
  like Unity's serializer (reported as a change).
- `UnityEngine.Object` references render read-only (`name (Type)` or
  `None (Type)`), since there is no asset picker at runtime.
- `Draw(ref value)` works for structs and classes and returns true when any
  field changed this frame; `Draw(object target)` edits an existing instance
  in place.
- `NowInspector.SetDrawer<T>((ref T value) => ...)` takes over the control
  cell for a type everywhere it appears — the way to render otherwise
  unsupported types or restyle built-in rows. `SetLabelWidth`, `SetSpacing`,
  `SetIndent` and `SetMaxDepth` tune the layout.
- Rows read and write fields through reflection every frame (value-typed
  fields box); it is a debugging/modding surface, not a hot path for
  thousands of fields.

## File picker fields

File picker fields are ordinary controls with overlay popups. They only return
paths; opening, saving, importing, and validation beyond the selected mode stay
in your code. The popup includes an address bar, a folder tree for upstream and
local navigation, and a details list with extension-aware file icons.

```csharp
string loadPath = "";
string savePath = "";
string outputDirectory = "";

using (NowLayout.Area(NowScreen.safeArea, padding: 16f, spacing: 8f))
{
    if (NowLayout.OpenFileField("load-config")
        .SetTitle("Open config")
        .SetStartDirectory(Application.dataPath)
        .SetFilters(
            new NowFileFilter("Config", "json", "yaml", "yml"),
            new NowFileFilter("All files", "*"))
        .SetPopupSize(780f, 480f)
        .Draw(ref loadPath))
    {
        LoadConfig(loadPath);
    }

    if (NowLayout.SaveFileField("save-config")
        .SetTitle("Save config")
        .SetStartDirectory(Application.persistentDataPath)
        .SetDefaultFileName("settings")
        .SetDefaultExtension("json")
        .SetFilter("Json", "json")
        .Draw(ref savePath))
    {
        SaveConfig(savePath);
    }

    if (NowLayout.DirectoryField("output-dir")
        .SetTitle("Choose output directory")
        .SetStartDirectory(Application.dataPath)
        .Draw(ref outputDirectory))
    {
        SetOutputDirectory(outputDirectory);
    }
}
```

- `SetExtensions(...)` is the quick form for one unnamed filter; use
  `SetFilter(name, ...)` for one named filter and `SetFilters(...)` for a
  popup filter dropdown.
- Save fields append `SetDefaultExtension(...)` when the selected file name
  has no extension. If no default extension is set, the first concrete filter
  extension is used.
- `SetShowHidden(true)` includes hidden filesystem entries. The default keeps
  them out of the browser list.
- `SetFitToView(false)` disables popup fitting when a host wants exact
  placement.
- Selection is delivered on the next `Draw(ref path)` after the popup commits,
  matching dropdown popup behavior.
- See the file-picker section in the packaged
  [docs browser source](../Example/NowDocsExample.cs) for a complete example.

## View stacks and dialogs

`NowViewStack` is the retained navigation layer for screens, sheets, and modal
popups that are larger than a single dropdown/context menu. Own one stack per
surface, push `INowView` instances in response to controls, and call
`stack.Draw(surface)` every frame from the host that owns the surface. Covered
views still draw, but passively, so they keep their visual state while only the
top view receives live input.

```csharp
public sealed class SettingsPanel : NowLayoutGraphic
{
    readonly NowViewStack _views = new NowViewStack();
    readonly SettingsHomeView _home = new SettingsHomeView();

    protected override void DrawNowUI(NowRect rect)
    {
        var surface = new NowRect(0, 0, rect.width, rect.height);

        if (!_views.ContainsKey("home"))
        {
            _views.Push("home", _home,
                NowViewOptions.FullScreen(NowViewTransitionPreset.None, 0f)
                    .SetCloseOnCancel(false));
        }

        _views.Draw(surface);
    }
}

sealed class SettingsHomeView : INowView
{
    public void Draw(NowViewContext context)
    {
        using (NowLayout.Area(context.rect, padding: 16f, spacing: 8f))
        {
            NowLayout.Label("Settings").SetFontSize(20f).Draw();

            if (NowLayout.Button("Details").Draw())
            {
                context.stack.Push("details", new DetailsView(),
                    NowViewOptions.FullScreen(NowViewTransitionPreset.SlideFromRight));
            }

            if (NowLayout.Button("Confirm").Draw())
            {
                var popup = new NowRect(
                    context.rect.center.x - 190f,
                    context.rect.center.y - 95f,
                    380f,
                    190f);

                context.stack.Push(NowViews.Confirm(
                        "Discard changes?",
                        "The dialog is just another INowView.",
                        onConfirm: () => { }),
                    NowViewOptions.Popup(popup)
                        .SetScrim(new Color(0f, 0f, 0f, 0.38f)));
            }
        }
    }
}

sealed class DetailsView : INowView
{
    public void Draw(NowViewContext context)
    {
        using (NowLayout.Area(context.rect, padding: 16f, spacing: 8f))
        {
            if (NowLayout.Button("Back").Draw())
                context.Close();

            NowLayout.Label("Nested screen").Draw();
        }
    }
}
```

- `Push(view, options)` adds a view; `Push(key, view, options)` prevents
  duplicate keyed views; `PushOrReplace(key, view, options)` updates a known
  slot.
- `Pop()`, `Pop(handle)`, `PopKey(key)`, `PopTo(...)`, and `Clear(...)` close
  views. The `NowViewContext` passed to each view also exposes `Close()`.
- `NowViewOptions.FullScreen(...)` fills the stack surface. `Popup(rect, ...)`
  keeps the view in a fitted rect, blocks input underneath when modal, and
  closes on outside click by default.
- Transitions are built in (`Fade`, `ScaleFade`, `SlideFromBottom`,
  `SlideFromRight`) and can be replaced with a custom transition delegate.
- `NowViews.MessageBox(...)` and `NowViews.Confirm(...)` are ready-made dialog
  views you can push on any stack.
- See the view-stack section in the packaged
  [docs browser source](../Example/NowDocsExample.cs) for a complete example.

## Explicit rects

Every control except `TreeView` has a `Now.*` twin taking a rect, for HUDs
and free-form drawing (`TreeView` flows in the ambient layout — give it a
region with `NowLayout.Area(rect)` or host it in a `ScrollView`):

```csharp
if (Now.Button(new NowRect(20, 20, 120, 40), "Save").Draw())
    Save();

Now.Slider(new NowRect(20, 70, 200, 20), 0f, 1f).Draw(ref volume);
Now.TextField(new NowRect(20, 100, 200, 30)).Draw(ref playerName);
Now.ColorPicker(new NowRect(20, 140, 180, 30)).Draw(ref tint);
Now.GradientField(new NowRect(20, 180, 180, 30)).Draw(ref ramp);
Now.AnimationCurveField(new NowRect(20, 220, 180, 34)).Draw(ref falloff);
Now.OpenFileField(new NowRect(20, 264, 260, 30)).SetFilter("Text", "txt", "md").Draw(ref loadPath);
Now.KeyBindingField(new NowRect(20, 300, 140, 30)).Draw(ref jumpKey);
```

Use `NowCornerRadius` or the four-float `SetRadius(topLeft, topRight,
bottomRight, bottomLeft)` overload when only some corners should be rounded:

```csharp
Now.Rectangle(headerRect).SetRadius(NowCornerRadius.Top(8f)).Draw();
Now.Rectangle(sideRailRect).SetRadius(topLeft: 8f, topRight: 0f, bottomRight: 0f, bottomLeft: 8f).Draw();
```

The raw `Vector4` radius overload remains for compatibility, but it uses the
renderer's packed order and should be avoided in new code when corners differ.

## Custom content inside controls

When a label is not enough — an icon, a sub-label, a Lottie spinner — open
the control as a scope with `Begin()`. Interaction runs immediately, so the
result is readable inside (a `NowControlScope` with `clicked`, `focused`,
`interaction`, `rect`), and children flow in a horizontal row:

```csharp
using (var save = NowLayout.Button().Begin())
{
    if (save.clicked)
        Save();

    NowLayout.Lottie(spinner).SetTime(Time.time).SetHeight(18).Draw();
    NowLayout.Label(saving ? "Saving..." : "Save").Draw();
}

using (var box = NowLayout.Checkbox().Begin(ref shadows))
{
    NowLayout.Label("Shadows").Draw();
    NowLayout.Label("(expensive)").SetFontSize(11).Draw();
}

using (var high = NowLayout.Radio(quality == 2).Begin())
{
    if (high.clicked) quality = 2;
    NowLayout.Label("High").Draw();
}
```

With `Begin()` no label is needed at all — identity comes from the call site
(see *Identity* below) and the visible content draws inside the scope. A
label passed anyway is ignored visually.

Checkbox toggles its ref value at `Begin`, so the updated value is also
readable inside; `clicked` doubles as "changed this frame". In layout flow,
content-sized controls resolve in the current measure/draw cycle under a
layout host. A one-pass host uses the previous content measurement. The
explicit-rect forms (`Now.Button(rect).Begin()`) are exact immediately.
ScrollView's `Begin()` is the same idea applied to a viewport.

Children of different heights top-align by default. `SetAlignItems` on the
control sets the row's cross-axis default (flexbox `align-items`), and a
child's own `SetAlign` still overrides it:

```csharp
using (var save = NowLayout.Button().SetAlignItems(NowLayoutAlign.Center).Begin())
{
    NowLayout.Lottie(bigIcon).SetTime(Time.time).SetHeight(64).Draw();   // tallest, defines the row
    NowLayout.Lottie(spinner).SetTime(Time.time).SetHeight(18).Draw();   // vertically centered
    NowLayout.Label("Save").Draw();                   // vertically centered
}
```

## Identity

Control identity is automatic: it comes from the call site. The factories
capture their caller via `[CallerFilePath]`/`[CallerLineNumber]`, so every
textual `Button(...)` in your code is its own control — labels are purely
visual and never part of the id. Two `Button("Delete")`s on different lines
are independent; renaming a label never resets state. Ids are session-scoped
(they shift when code moves between compiles) and must never be persisted.

A loop shares one call site; iterations are salted by per-frame draw order,
so looped buttons work with no ceremony:

```csharp
foreach (var line in logLines)
    NowLayout.Label(line).Draw();

for (int i = 0; i < rows.Count; ++i)
    if (NowLayout.Button(rows[i].name).Draw())   // one site, salted per iteration
        Open(i);
```

Draw-order salting means state follows the *position* in the loop, not the
item. When looped items can reorder, appear, or vanish — or when one logical
control draws from several code paths — anchor identity to your data instead.
`NowId` is the preferred explicit identity type: it can hold a string or a
non-zero integer, and integer ids avoid per-frame string hashing for
data-backed controls. Both forms are local to the active retained host and
`NowControls.IdScope`, so two reusable panels can safely use the same ids.

```csharp
NowLayout.Button("Delete").SetId(item.id).Draw();

for (int i = 0; i < rows.Count; ++i)
    using (NowControls.IdScope(rows[i].id))
        if (NowLayout.Button("Delete").Draw())
            Delete(rows[i]);
```

When an integer is already fully resolved—such as a value returned by
`graphic.ResolveControlId(item.id)` or composed from a resolved parent with
`NowInput.CombineId`—wrap it with `NowId.Resolved(value)` before passing it
back to `SetId`. This explicit escape hatch prevents accidental double-scoping;
ordinary application/data ids should stay as plain integers.

`TextField`, `Dropdown`, and `ScrollView` keep their optional explicit id as
the first parameter (`TextField(player.id)` or `TextField("player-name")`) for
the same purpose; omit it and the call site is the id. Custom controls get site
identity by declaring the caller-info parameters themselves and passing them
through `NowControls.Interact(...)`. Builder-style custom controls can store
`NowControls.SiteId(file, line)` in the factory and pass that fallback identity
to `NowControls.Interact(id, fallbackIdentity, rect, ...)`.

## Compile-time misuse warnings

Builders are inert until consumed, so `NowLayout.Label("Hello");` without
`.Draw()` silently renders nothing. The package bundles a Roslyn analyzer
(`Runtime/Analyzers/NowUI.Analyzers.dll`) that turns the two provably-dead
patterns into compiler warnings — in the Unity console and in your IDE — for
every assembly that references NowUI:

- **NOWUI001** — a builder discarded as a bare statement:
  `NowLayout.Label("Hello");` → *did you forget `.Draw()`?*
- **NOWUI002** — a using-only scope discarded as a bare statement:
  `Now.Mask(rect);` → the scope can never be disposed, so the pushed state
  leaks for the rest of the frame. Wrap it in `using`. Layout areas and groups
  follow the same rule; their old public `EndArea`/`EndHorizontal`/`EndVertical`
  escape hatch has been removed so every public stack API has one lifecycle.

Both rules fire only when the misuse is certain — there are no heuristic
"maybe" warnings. To discard intentionally (rare, mostly tests), assign to
the C# discard: `_ = NowLayout.Label("hello").SetFontSize(99);`.

The detection is attribute-driven and works for your own controls too:

- `[NowBuilder]` on a struct marks it as inert-until-consumed (NOWUI001).
- `[NowConsumer]` on a method marks it as performing the work while returning
  the builder for chaining, like `NowRectangle.Draw()` — statements ending
  in a consumer call are never flagged.
- `[NowScope]` on a disposable struct marks it as using-only (NOWUI002).

Analyzer sources live in [`Analyzers~`](../Analyzers~) (ignored by Unity);
rebuild with `dotnet build -c Release` and copy the DLL over
`Runtime/Analyzers/NowUI.Analyzers.dll`.

## Theming

Controls read the ambient theme — a built-in default, a pushed scope, or your
own `NowThemeAsset`:

```csharp
using (NowTheme.Scope(myTheme))
    DrawSettingsPanel();
```

Styles are enums — no magic strings:

```csharp
NowLayout.Button("Cancel")
    .SetStyle(NowRectangleStyle.Outline)
    .SetTextStyle(NowTextStyle.Body)
    .Draw();
theme.Rectangle(rect, NowRectangleStyle.Accent).Draw();
theme.GetColor(NowColorToken.Text, Color.black);
theme.GetSpacing(NowSpacingToken.Md, fallback);
```

Buttons default to `NowRectangleStyle.Accent` + `NowTextStyle.Button`;
checkboxes/radios/fields use `Outline`/`Muted`/`Body`. Restyle what the enums
mean by editing the matching presets in your theme asset.

For styling beyond the built-in set, compose your own control: the `.Draw()`
separation means a `MyDangerButton()` function that pre-applies everything is
a one-liner wrapper. To change built-in control visuals globally, assign a
custom `NowControlRenderer` on the active theme.

## Focus, keyboard and gamepad

Focusable controls register with `NowFocus` every frame. Arrows, WASD, the
d-pad or left stick move focus spatially, including held-input repeat after
a short delay; when nothing is focused, directional navigation starts from
the opposite edge of the control set, preferring controls currently visible
in the viewport. Spatial moves keep a sticky cross-axis anchor: navigating
down a form holds the column you started in even when a row in between only
has one offset control, until you deliberately move sideways (or focus
changes by pointer, Tab, or an explicit link). Tab and Shift+Tab cycle
through controls in draw order. Submit (enter/space/gamepad south) activates the focused
control; cancel clears focus (and closes popups). Clicking a control focuses
it. Focused controls draw a focus outline.

Override individual focus hops with `SetNavigation`. Targets should be stable
control ids, usually from `SetId`; unset links keep using the default spatial
or draw-order resolver, and links to controls that are not registered this frame
fall back too.

```csharp
NowLayout.Button("Name")
    .SetId("name")
    .SetNavigation(NowFocusNavigation.Right("email").SetDown("save"))
    .Draw();

NowLayout.TextField("email").SetId("email").Draw(ref email);
NowLayout.Button("Save").SetId("save").Draw();
```

NowUI focus and Unity's EventSystem stay mutually exclusive by default:
selecting a UGUI control (clicking a classic Button, for example) clears
NowUI focus and pauses NowUI navigation until that selection clears, and
focusing a NowUI control deselects the EventSystem. Disable with
`NowFocus.respectEventSystem = false`. Seamless navigation handoff between
the two systems is not attempted.

Pointer occlusion is mutual too: a `NowGraphic`'s Raycast Target blocks
UGUI beneath it, and UGUI drawn above NowUI blocks NowUI's pointer — the
graphic withholds input unless the EventSystem's topmost hit is the graphic
itself (**Respect UGUI Raycast**, on by default; needs Raycast Target
enabled), and the screen render path withholds the pointer while it is over
any raycastable UI (`NowScreenInputProvider.blockedWhenPointerOverUGUI`).
In-flight drags and releases always come through, so controls never strand
mid-interaction.

## Hosting in UGUI

Use a `NowGraphic` subclass when controls receive explicit rects. Use
`NowLayoutGraphic` when `DrawNowUI` contains `NowLayout`: the layout-specific
host owns an exact measure/draw cycle, so stretch, growth, flexible space, and
content-sized controls are correct in the same rebuild. Neither host needs
`Now.StartUI`, and layout code in `NowLayoutGraphic` does not need
`NowLayout.RunMeasured`.

Input arrives through the RectTransform provider, and **Auto Rebuild On
Interaction** (on by default) re-renders when pointer, button, scroll, or
navigation input changes for the graphic, or when a control requests a repaint
(caret blink or animation), staying fully retained while idle.
`raycastTarget` blocks UGUI Selectables underneath, so NowUI controls layer
correctly with UGUI.

Both hosts are also UGUI layout elements. **Drive Layout Size** is off by
default; enable it only when the NowUI content should report a preferred
width/height to a `VerticalLayoutGroup`, `ContentSizeFitter`, or other UGUI
layout controller. Layout queries use a passive measure pass, and the latest
value is available through `measuredContentSize`.

---

## Building your own controls

Everything the built-in controls use is public. This section is the
reference; the walkthrough guide — restyling, wrapping variants, custom
shapes, full builds — is [CustomControls.md](CustomControls.md). The anatomy
of a control:

```csharp
public static bool MyToggleSwitch(
    ref bool value,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0)
{
    var theme = NowTheme.themeAsset;

    // 1. Reserve space (layout) or take a rect parameter (free-form).
    NowRect rect = NowLayout.ReserveRect(52f, 28f);

    // 2. The standard interaction bundle: pointer + focus + submit.
    var interaction = NowControls.Interact(rect, out bool focused, out bool submitted, file, line);

    if (interaction.clicked || submitted)
        value = !value;

    // 3. Ephemeral state: animations, timers — keyed by the control id.
    float t = NowControlState.Transition(interaction, value, speed: 12f);

    // 4. Draw with theme styles.
    var track = theme.Rectangle(rect, value ? NowRectangleStyle.Accent : NowRectangleStyle.Muted);
    track.radius = new Vector4(rect.height, rect.height, rect.height, rect.height) * 0.5f;
    track.color = NowControls.StateColor(track.color, NowControlState.Transition(
        interaction, "hover", interaction.hovered), interaction.held);

    if (focused)
    {
        track.outline = 2f;
        track.outlineColor = theme.GetColor(NowColorToken.Text, Color.black);
    }

    track.Draw();

    float knob = rect.height - 6f;
    float x = Mathf.Lerp(rect.x + 3f, rect.xMax - knob - 3f, t);
    Now.Rectangle(new NowRect(x, rect.y + 3f, knob, knob))
        .SetColor(theme.GetColor(NowColorToken.AccentText, Color.white))
        .SetRadius(knob * 0.5f)
        .Draw();

    return interaction.clicked || submitted;
}
```

The toolkit pieces:

| Primitive | Purpose |
| --- | --- |
| `NowControls.Interact(rect, out focused, out submitted, file, line)` | Call-site identity + pointer interaction + focus registration + click-to-focus + submit |
| `NowControls.Interact(id, fallback, rect, out focused, out submitted)` | Builder-style identity with optional explicit `NowId` |
| `NowInput.Interact(rect)` | Id-less interaction: identity from the call site |
| `interaction.GetId("slot")` / `interaction.State<T>("slot")` | Sub-state keys derived from the resolved control id |
| `NowInput.CombineId(a, b)` | Mint sub-element ids (rows, links, items) without strings |
| `using (Now.Transform(scale, origin))` | Scale/pan drawing and input together when a host already scales explicit rects, such as zoomable node content |
| `NowControlState.Get<T>(id)` / `Get<T>(id, "slot")` | Persistent ephemeral slot (struct), evicted when stale |
| `NowControlState.Transition / Repeat / DetectDoubleClick / ClickStreak / Blink` | The standard timing behaviors; common animation/repeat helpers also accept `NowInteraction` |
| `NowControlState.RequestRepaint()` | Tell retained hosts (UGUI) to render another frame |
| `NowFocus.IsFocused / Focus / Clear / LockNavigation` | Focus queries, explicit control, nav suppression while editing |
| `Now.Mask(rect)` | Ambient clipping scope (what ScrollView uses) |
| `NowOverlay.Defer(blockRect, draw)` | Draw above everything; input beneath is blocked |
| `NowContextMenu.Open / Begin / Item / End` | Modal right-click menus on the overlay layer |
| `NowTextInput.current` | Frame-sampled keyboard text/editing input, including IME composition |
| `NowTextInput.setImeEnabled / setCompositionCursor` | IME hooks: editors toggle on focus and report the caret for the candidate window |
| `NowTextEdit` | Headless caret/selection/editing engine for custom editors |
| `NowTextWrap.Layout / Draw` | Word wrap: lay out once into positioned runs, draw many frames |
| `NowTextArea.LayoutLines / LineOf` | Editing-grade line layout: every character covered, caret-exact metrics |
| `NowTextSelection.Draw / Interact / DrawHighlights` | Browser-style text selection over positioned line segments |
| `NowClipboard.Copy / Paste / setText / getText` | The single clipboard hook every copy/paste path uses |
| `NowLayout.ContentRect()` → `content.End(height)` | Reserve/measure for width-dependent content; same-cycle in layout hosts, cached in one-pass hosts |
| `theme.Rectangle / theme.Text / theme.ResolveText / theme.GetColor ...` | Themed visuals; `ResolveText` is the rect-free, mask-free starting point |
| `font.MeasureText(text, start, length)` / span overloads | Allocation-free measuring for wrap engines and dynamic text |

Conventions that keep custom controls consistent:

- Run `NowControls.Interact` first; draw after mutating state so visuals show
  this frame's reality.
- Store only ephemera in `NowControlState`; the caller owns real values.
- Call `RequestRepaint()` whenever the control is time-dependent (the
  `Transition`/`Repeat` helpers do it for you).
- Everything must behave inertly during layout measure passes — interaction
  helpers already are; guard manual input reads with `NowInput.isPassive`.

## Current limitations

- ScrollView does not yet capture touch flick-drags that start on child
  controls (wheel, scrollbar thumbs, and scroll-aware drags via
  `NowScrollView.RequestDragScroll()` work everywhere).
- IME composition renders inline with the default screen-space cursor
  reporting; hosts whose surface is not the screen (UGUI canvases, render
  textures) should replace `NowTextInput.setCompositionCursor` to transform
  the caret point.
- Dropdown popups are pointer-driven; focus navigation inside the popup is
  not yet wired.
- File picker popups are built-in NowUI controls, not native OS dialogs, and
  currently use emoji placeholders for file/folder/action icons.
