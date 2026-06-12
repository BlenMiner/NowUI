# Controls

NowUI's immediate-mode controls: buttons, checkboxes, radios, sliders, text
fields, dropdowns and scroll views, built entirely on public primitives so
custom controls are first-class citizens. Controls work identically in the
screen path (`Now.StartUI`), inside UGUI (`NowUIGraphic`), and in URP/HDRP
overlays — pointer, touch, keyboard and gamepad included.

Controls live where drawing already lives: `NowLayout.*` flows in the active
layout group (like `NowLayout.Label`), `Now.*` takes an explicit rect (like
`Now.Text`), and `NowControls` is the shared toolkit — theme, id scopes, and
the interaction plumbing.

## Using the built-in controls

Layout-flowing controls reserve space sized from their themed content; values
stay owned by you, passed by ref.

```csharp
using (NowLayout.Area(NowUIScreen.safeArea))
using (NowLayout.Vertical(new NowLayoutOptions().SetPadding(16).SetSpacing(8)))
{
    if (NowLayout.Button("Save").Draw())
        Save();

    NowLayout.Checkbox("Enable shadows").Draw(ref shadows);

    if (NowLayout.Radio("Low", quality == 0).Draw()) quality = 0;
    if (NowLayout.Radio("High", quality == 1).Draw()) quality = 1;

    NowLayout.Slider(0f, 1f).SetStretchWidth().Draw(ref volume);

    NowLayout.TextField("player-name").SetPlaceholder("Name...").Draw(ref playerName);

    NowLayout.Dropdown("res", resolutionNames).Draw(ref resolutionIndex);

    using (NowLayout.ScrollView("log").SetHeight(160).Begin())
        foreach (var line in logLines)
            NowLayout.Label(line).Draw();
}
```

- `Button(...).Draw()` returns true on click or on submit while focused.
- `Checkbox(...).Draw(ref value)` / `Slider(...).Draw(ref value)` mutate the
  ref and return true when it changed.
- `Radio(label, isOn).Draw()` returns true when clicked; set your selection in
  response.
- `TextField` supports click/drag selection (shaped-text cluster aware),
  standard editing keys with repeat, copy/cut/paste/select-all, double-click
  select-all, placeholder text, and the mobile on-screen keyboard.
- `Dropdown` opens an overlay popup that blocks input underneath, scrolls when
  long, and closes on selection, outside click, or cancel. Selection applies
  on the next frame's Draw.
- `ScrollView` scrolls with the wheel while hovered and with the scrollbar
  thumb; content height is the layout group's measured extent (one frame
  late, like all layout measurement). Vertical only for now.

## Explicit rects

Every control has a `Now.*` twin taking a rect, for HUDs and free-form
drawing:

```csharp
if (Now.Button(new NowRect(20, 20, 120, 40), "Save").Draw())
    Save();

Now.Slider(new NowRect(20, 70, 200, 20), 0f, 1f).Draw(ref volume);
Now.TextField(new NowRect(20, 100, 200, 30), "name").Draw(ref playerName);
```

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

    NowLayout.Lottie(spinner).SetHeight(18).Draw();
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
readable inside; `clicked` doubles as "changed this frame". In layout flow
the control sizes to the previous frame's content, like all scope-form
layout; the explicit-rect forms (`Now.Button(rect, "id").Begin()`) are exact
immediately. ScrollView's `Begin()` is the same idea applied to a viewport.

Children of different heights top-align by default. `SetAlignItems` on the
control sets the row's cross-axis default (flexbox `align-items`), and a
child's own `SetAlign` still overrides it:

```csharp
using (var save = NowLayout.Button("save-btn").SetAlignItems(NowLayoutAlign.Center).Begin())
{
    NowLayout.Lottie(bigIcon).SetHeight(64).Draw();   // tallest, defines the row
    NowLayout.Lottie(spinner).SetHeight(18).Draw();   // vertically centered
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
control draws from several code paths — anchor identity to your data instead:

```csharp
NowLayout.Button("Delete").SetId($"delete-{item.id}").Draw();

for (int i = 0; i < rows.Count; ++i)
    using (NowControls.IdScope(rows[i].id))
        if (NowLayout.Button("Delete").Draw())
            Delete(rows[i]);
```

`TextField`, `Dropdown`, and `ScrollView` keep their optional explicit string
id as the first parameter (`TextField("player-name")`) for the same purpose;
omit it and the call site is the id. Custom controls get site identity by
declaring the caller-info parameters themselves and passing them through
`NowControls.SiteId(file, line)` into `NowControls.GetControlId(int)`.

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
  leaks for the rest of the frame. Wrap it in `using`. (`NowLayout.Area` and
  the other layout groups are exempt: closing them with
  `NowLayout.EndArea()`-style calls instead of `using` is supported.)

Both rules fire only when the misuse is certain — there are no heuristic
"maybe" warnings. To discard intentionally (rare, mostly tests), assign to
the C# discard: `_ = NowLayout.Label("hello").SetFontSize(99);`.

The detection is attribute-driven and works for your own controls too:

- `[NowBuilder]` on a struct marks it as inert-until-consumed (NOWUI001).
- `[NowConsumer]` on a method marks it as performing the work while returning
  the builder for chaining, like `NowUIRectangle.Draw()` — statements ending
  in a consumer call are never flagged.
- `[NowScope]` on a disposable struct marks it as using-only (NOWUI002).

Analyzer sources live in `Assets/NowUI/Analyzers~` (ignored by Unity);
rebuild with `dotnet build -c Release` and copy the DLL over
`Runtime/Analyzers/NowUI.Analyzers.dll`.

## Theming

Controls read the ambient theme — a built-in default, a pushed scope, or your
own `NowUITheme` asset:

```csharp
using (NowControls.Theme(myTheme))
    DrawSettingsPanel();
```

Styles are enums — no magic strings:

```csharp
NowLayout.Button("Cancel").SetStyle(NowRectangleStyle.Outline).Draw();
theme.Rectangle(rect, NowRectangleStyle.Accent).Draw();
theme.GetColor(NowColorToken.Text, Color.black);
theme.GetSpacing(NowSpacingToken.Md, fallback);
```

Buttons default to `NowRectangleStyle.Accent` + `NowTextStyle.Button`;
checkboxes/radios/fields use `Outline`/`Muted`/`Body`. Restyle what the enums
mean by editing the matching presets in your theme asset.

For styling beyond the built-in set, compose your own control: the `.Draw()`
separation means a `MyDangerButton()` function that pre-applies everything is
a one-liner wrapper, and the string-id theme methods
(`theme.Rectangle(rect, "danger")`) remain the low-level layer for custom
preset names defined in theme assets.

## Focus, keyboard and gamepad

Focusable controls register with `NowUIFocus` every frame. Arrows, WASD, the
d-pad or left stick move focus spatially; submit (enter/space/gamepad south)
activates the focused control; cancel clears focus (and closes popups).
Clicking a control focuses it. Focused controls draw a focus outline.

NowUI focus and Unity's EventSystem stay mutually exclusive by default:
selecting a UGUI control (clicking a classic Button, for example) clears
NowUI focus and pauses NowUI navigation until that selection clears, and
focusing a NowUI control deselects the EventSystem. Disable with
`NowUIFocus.respectEventSystem = false`. Seamless navigation handoff between
the two systems is not attempted.

Pointer occlusion is mutual too: a `NowUIGraphic`'s Raycast Target blocks
UGUI beneath it, and UGUI drawn above NowUI blocks NowUI's pointer — the
graphic withholds input unless the EventSystem's topmost hit is the graphic
itself (**Respect UGUI Raycast**, on by default; needs Raycast Target
enabled), and the screen render path withholds the pointer while it is over
any raycastable UI (`NowUIScreenInputProvider.blockedWhenPointerOverUGUI`).
In-flight drags and releases always come through, so controls never strand
mid-interaction.

## Hosting in UGUI

Drop a `NowUIGraphic` subclass on a Canvas and draw controls inside
`DrawNowUI` — input arrives through the RectTransform provider, and the
graphic's **Auto Rebuild On Interaction** (on by default) re-renders while
the pointer is over it or a control requests a repaint (focus ring, caret
blink, animations), staying fully retained otherwise. `raycastTarget` blocks
UGUI Selectables underneath, so NowUI controls layer correctly with UGUI.

The graphic is also a UGUI layout element: with **Drive Layout Size** (on by
default) it reports the measured extent of its root `NowLayout` areas as its
preferred width/height, so it sits inside a `VerticalLayoutGroup` or under a
`ContentSizeFitter` like any Image or Text — sized by its NowUI content. The
measurement settles one rebuild late, like all NowLayout sizing; read it from
code via `measuredContentSize`.

---

## Building your own controls

Everything the built-in controls use is public. The anatomy of a control:

```csharp
public static bool MyToggleSwitch(string label, ref bool value)
{
    var theme = NowControls.theme;
    int id = NowControls.GetControlId(label);

    // 1. Reserve space (layout) or take a rect parameter (free-form).
    NowRect rect = NowLayout.Rect(new NowLayoutOptions().SetSize(52f, 28f));

    // 2. The standard interaction bundle: pointer + focus + submit.
    var interaction = NowControls.Interact(id, rect, out bool focused, out bool submitted);

    if (interaction.clicked || submitted)
        value = !value;

    // 3. Ephemeral state: animations, timers — keyed by the control id.
    float t = NowUIControlState.Transition(id, value, speed: 12f);

    // 4. Draw with theme styles.
    var track = theme.Rectangle(rect, value ? NowRectangleStyle.Accent : NowRectangleStyle.Muted);
    track.radius = new Vector4(rect.height, rect.height, rect.height, rect.height) * 0.5f;
    track.color = NowControls.StateTint(track.color, NowUIControlState.Transition(
        NowUIInput.GetId(id, "hover"), interaction.hovered), interaction.held);

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
| `NowControls.SiteId(file, line)` + `GetControlId(id)` | Call-site identity with id-scope seeding and loop salting |
| `NowControls.Interact(id, rect, out focused, out submitted)` | Pointer interaction + focus registration + click-to-focus + submit |
| `NowUIInput.Interact(rect)` | Id-less interaction: identity from the call site |
| `NowUIInput.CombineId(a, b)` | Mint sub-element ids (rows, links, items) without strings |
| `NowUIControlState.Get<T>(id)` | Persistent ephemeral slot (struct), evicted when stale |
| `NowUIControlState.Transition / Repeat / DetectDoubleClick / Blink` | The standard timing behaviors |
| `NowUIControlState.RequestRepaint()` | Tell retained hosts (UGUI) to render another frame |
| `NowUIFocus.IsFocused / Focus / Clear / LockNavigation` | Focus queries, explicit control, nav suppression while editing |
| `Now.Mask(rect)` | Ambient clipping scope (what ScrollView uses) |
| `NowUIOverlay.Defer(blockRect, draw)` | Draw above everything; input beneath is blocked |
| `NowUIContextMenu.Open / Begin / Item / End` | Modal right-click menus on the overlay layer |
| `NowUITextInput.current` | Frame-sampled keyboard text/editing input |
| `NowTextEdit` | Headless caret/selection/editing engine for custom editors |
| `NowTextWrap.Layout / Draw` | Word wrap: lay out once into positioned runs, draw many frames |
| `NowTextSelection.Draw / Interact / DrawHighlights` | Browser-style text selection over positioned line segments |
| `NowUIClipboard.Copy / Paste / setText / getText` | The single clipboard hook every copy/paste path uses |
| `NowLayout.ContentRect()` → `content.End(height)` | Frame-late reserve/measure for content sized by its width |
| `theme.Rectangle / theme.Text / theme.ResolveText / theme.GetColor ...` | Themed visuals; `ResolveText` is the rect-free, mask-free starting point |
| `font.MeasureText(text, start, length)` / span overloads | Allocation-free measuring for wrap engines and dynamic text |

Conventions that keep custom controls consistent:

- Run `NowControls.Interact` first; draw after mutating state so visuals show
  this frame's reality.
- Store only ephemera in `NowUIControlState`; the caller owns real values.
- Call `RequestRepaint()` whenever the control is time-dependent (the
  `Transition`/`Repeat` helpers do it for you).
- Everything must behave inertly during layout measure passes — interaction
  helpers already are; guard manual input reads with `NowUIInput.isPassive`.

## Current limitations

- ScrollView is vertical-only and does not yet capture touch drags that start
  on child controls (wheel and scrollbar work everywhere).
- TextField is single-line; IME composition is not yet handled (typed
  characters and the mobile on-screen keyboard are).
- Dropdown popups are pointer-driven; focus navigation inside the popup is
  not yet wired.
