# Custom controls

Everything the built-in controls use is public — there is no privileged
layer. This guide is organized by escalation: each level needs more code
than the one before, so pick the cheapest level that gets the look you want.

1. **Restyle** — change what the built-ins draw through the theme. No code.
2. **Wrap** — a variant of an existing control: pre-applied styling or
   custom content. A few lines.
3. **Reshape** — your own visuals and hit shape on top of the standard
   interaction. One function.
4. **Build** — a new control from the same toolkit the built-ins use.

The toolkit reference (every primitive and what it is for) lives in
[Controls.md](Controls.md) under *Building your own controls*; this guide is
the how-to that goes with it.

## 1. Restyle: themes

Built-ins resolve every color, radius, spacing and font through the ambient
`NowTheme`. Before writing any code, check whether the look you want is
just different values for the same slots:

```csharp
// One-off: built-ins take a style enum.
NowLayout.Button("Cancel")
    .SetStyle(NowRectangleStyle.Outline)
    .SetTextStyle(NowTextStyle.Body)
    .Draw();

// A region: push a theme asset.
using (NowTheme.Scope(darkTheme))
    DrawSettingsPanel();
```

Editing the `Accent` preset in a theme asset restyles every button in one
place. The enum styles (`Surface`, `Muted`, `Outline`, `Accent`) are the
blessed set. For app-specific variants such as a danger action, wrap the
built-in control or assign a custom `NowControlRenderer` on the theme.

## 2. Wrap: variants of existing controls

A variant that pre-applies configuration is a one-liner — with one rule that
matters: **forward the caller info**. Identity comes from the call site, and
if your wrapper doesn't pass it through, every use of the wrapper shares the
wrapper's own line as its id:

```csharp
public static class MyControls
{
    public static NowButton DangerButton(
        string label,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return NowLayout.Button(label, file, line)
            .SetStyle(NowRectangleStyle.Outline)
            .SetTextStyle(NowTextStyle.Body);
    }
}

// Each call site is its own control, exactly like the built-in factories:
if (MyControls.DangerButton("Delete").Draw())
    Delete();
```

When the variant is about *content* rather than configuration — an icon, a
spinner, a sub-label — don't build anything: open the control as a scope and
draw inside it (see *Custom content inside controls* in Controls.md):

```csharp
using (var save = NowLayout.Button().SetAlignItems(NowLayoutAlign.Center).Begin())
{
    if (save.clicked)
        Save();

    NowLayout.Lottie(spinner).SetTime(Time.time).SetHeight(18).Draw();
    NowLayout.Label("Save").Draw();
}
```

## 3. Reshape: your own shape on standard interaction

`NowControls.Interact` gives you the full standard bundle — pointer phases,
click-to-focus, focus registration, keyboard/gamepad submit — against a
rect. Your visuals are whatever you draw, and a non-rectangular hit shape is
your own test layered on the rect result. A round icon button:

```csharp
public static bool RoundButton(
    string label,
    [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
{
    var theme = NowTheme.themeAsset;

    NowRect rect = NowLayout.Rect(44f, 44f);
    var interaction = NowControls.Interact(rect, out bool focused, out bool submitted, file, line);

    // Round shape: reject pointer events that land in the rect's corners.
    float radius = rect.width * 0.5f;
    bool inCircle = (interaction.pointerPosition - rect.center).sqrMagnitude <= radius * radius;
    bool clicked = (interaction.clicked && inCircle) || submitted;

    float hoverT = NowControlState.Transition(
        interaction, "hover", interaction.hovered && inCircle);

    var circle = theme.Rectangle(rect, NowRectangleStyle.Accent);
    circle.radius = Vector4.one * radius;
    circle.color = NowControls.StateTint(circle.color, hoverT, interaction.held && inCircle);

    if (focused)
    {
        circle.outline = 2f;
        circle.outlineColor = theme.GetColor(NowColorToken.Text, Color.black);
    }

    circle.Draw();

    var text = theme.ResolveText(NowTextStyle.Button);
    Vector2 size = text.Measure(label);
    text.rect = new NowRect(
        rect.center.x - size.x * 0.5f, rect.center.y - size.y * 0.5f,
        size.x + 2f, size.y);
    text.Draw(label);

    return clicked;
}
```

Note what is *not* re-implemented: focus, submit, press tracking, hover —
only the visuals and the circle test are yours. The corner-rejection idea
generalizes to any shape you can hit-test against a point.

## 4. Build: a new control from scratch

The anatomy is always the same three steps — space, interaction, draw — with
identity carried by `NowControls.Interact` and ephemeral state in
`NowControlState` slots where needed. A rating control, complete:

```csharp
public static bool Rating(
    ref int value, int max = 5,
    [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
{
    const float Dot = 18f, Gap = 6f;

    var theme = NowTheme.themeAsset;

    NowRect rect = NowLayout.Rect(max * Dot + (max - 1) * Gap, Dot);
    var interaction = NowControls.Interact(rect, out bool focused, out bool submitted, file, line);

    int hoveredIndex = interaction.hovered
        ? Mathf.Clamp(Mathf.FloorToInt((interaction.pointerPosition.x - rect.x) / (Dot + Gap)), 0, max - 1)
        : -1;

    int previous = value;

    if (interaction.clicked && hoveredIndex >= 0)
        value = hoveredIndex + 1 == value ? 0 : hoveredIndex + 1;

    if (submitted)
        value = value % max + 1;

    Color lit = theme.GetColor(NowColorToken.Accent, Color.yellow);
    Color unlit = theme.GetColor(NowColorToken.Border, Color.gray);

    for (int i = 0; i < max; ++i)
    {
        var dotRect = new NowRect(rect.x + i * (Dot + Gap), rect.y, Dot, Dot);
        Color color = i < value ? lit : unlit;

        if (hoveredIndex >= 0 && i <= hoveredIndex && i >= value)
            color = Color.Lerp(unlit, lit, 0.45f);

        Now.Rectangle(dotRect).SetColor(color).SetRadius(Dot * 0.5f).Draw();
    }

    if (focused)
    {
        var ring = Now.Rectangle(rect.Outset(4f, 4f));
        ring.color = Color.clear;
        ring.outline = 2f;
        ring.outlineColor = theme.GetColor(NowColorToken.Accent, Color.blue);
        ring.radius = Vector4.one * (Dot * 0.5f + 4f);
        ring.Draw();
    }

    return value != previous;
}
```

This already behaves like a built-in: it sits in layout flow, hovers with a
preview, click-toggles, participates in spatial focus navigation, and
activates on enter/space/gamepad south. The hover preview repaints without
any extra work because hosts re-render while the pointer is over them.

### The builder form

When a control grows options, switch from a function with parameters to the
builder pattern the built-ins use — a struct made in a factory, configured by
chaining, executed by `Draw()`:

```csharp
[NowBuilder]
public struct MyRating
{
    readonly int _site;
    NowId _id;
    int _max;

    internal MyRating(int site)
    {
        _site = site;
        _id = default;
        _max = 5;
    }

    public MyRating SetId(NowId id) { _id = id; return this; }

    public MyRating SetMax(int max) { _max = max; return this; }

    public bool Draw(ref int value)
    {
        NowRect rect = NowLayout.Rect(_max * 18f + (_max - 1) * 6f, 18f);
        var interaction = NowControls.Interact(_id, _site, rect, out bool focused, out bool submitted);
        // ... body as above, using _max ...
        return false;
    }
}

public static class MyControls
{
    public static MyRating Rating(
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        return new MyRating(NowControls.SiteId(file, line));
    }
}
```

`[NowBuilder]` opts the struct into the bundled analyzer:
`MyControls.Rating();` without `.Draw(...)` becomes a compiler warning, the
same protection the built-ins get (see *Compile-time misuse warnings* in
Controls.md, including `[NowConsumer]` and `[NowScope]`). The `SetId` escape
hatch matters as soon as the control can draw from loops over reorderable
data — same rules as the *Identity* section in Controls.md.

For an explicit-rect twin (`MyControls.Rating(rect)`), add a second factory
and constructor that carry a `NowRect` and skip `NowLayout.Rect` — compare
`NowTextField`'s pair of constructors in the source.

### State, timing, and the rules

- Real values live with the caller (`ref int value`); only ephemera —
  animation phase, scroll offsets, blink anchors — go in
  `NowControlState.Get<T>(id)` or `NowControlState.Get<T>(id, "slot")`
  slots, which are evicted when stale.
  Sub-key extra slots off your interaction id with `interaction.GetId("hover")`
  or `interaction.State<T>("hover")`.
- The timing helpers cover the standard behaviors so you never hand-roll
  them: `Transition` (animated 0..1), `Repeat` (key repeat), `Blink`
  (caret), `DetectDoubleClick`, `ClickStreak` (double = 2, triple = 3).
  Common animation/repeat helpers also accept `NowInteraction` directly.
- Call `NowControlState.RequestRepaint()` whenever the control will look
  different next frame for reasons input can't predict (running animations,
  timers). `Transition` and `Repeat` already do.
- Interact first, mutate state, then draw — visuals must show this frame's
  reality, not last frame's.
- Layout measure passes redraw everything inertly. The interaction helpers
  are already measure-safe; guard any *manual* `NowInput.current` reads
  with `NowInput.isPassive`.

## Reaching for bigger pieces

Larger controls are mostly composition of existing engines — check these
before writing one:

- **Text editing**: `NowTextEdit` is the headless caret/selection engine,
  `NowTextInput.current` the normalized keyboard frame (including IME
  composition), `NowTextWrap` display word-wrap, `NowTextArea.LayoutLines`
  editing-grade line layout, `NowTextSelection` browser-style selection over
  positioned segments.
- **Popups**: `NowOverlay.Defer(blockRect, draw)` draws above everything
  and blocks input beneath; `NowContextMenu` is the ready-made modal menu.
  Remember overlay blocks apply one frame late — deliver results the frame
  after closing, the way `NowDropdown` and `NowContextMenu` do.
- **Clipping**: `using (Now.Mask(rect))` is all a viewport is; ScrollView is
  a mask plus a stored offset.
- **Clipboard**: route copy/paste through `NowClipboard` so platform
  swaps stay one-line.

And when a built-in is *almost* right, copying its source into your project
as a starting point is a legitimate move — the controls in
`Runtime/Controls/` are written against the same public API this guide uses,
and they are the reference for the conventions above.

## Checklist

- [ ] Space from `NowLayout.Rect(options)` (flow) or a rect parameter
      (free-form) — ideally both, as twin factories.
- [ ] `NowControls.Interact` for call-site identity, pointer + focus + submit;
      draw a visible focus state when `focused` is true.
- [ ] `SetId(NowId)` escape hatch when a control can draw from loops over
      reorderable data or from several code paths.
- [ ] Caller owns real values; `NowControlState` holds only ephemera.
- [ ] Colors and metrics from the theme, not constants.
- [ ] `RequestRepaint()` for anything time-driven.
- [ ] Manual input reads guarded by `NowInput.isPassive`.
- [ ] `[NowBuilder]` on builder structs so misuse warns at compile time.
