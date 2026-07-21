# Layout

NowUI has two complementary ways to place UI:

- Use `Now` when you already have a `NowRect`. It draws exactly where you ask.
- Use `NowLayout` when you want to describe rows, columns, spacing, and growth
  and let the layout system resolve the rects.

That is the whole mental model. A design with known bounds usually stays
simpler with `Now`; a responsive panel usually stays simpler with `NowLayout`.
They can be mixed whenever that is useful.

For a complete side-by-side example, compare the same search landing page
implemented with [explicit rects](../Example/NowLandingPageExample.cs)
and with [NowLayout](../Example/NowLayoutLandingPageExample.cs).

## Explicit rect slicing

When a design has known regions, slice a `NowRect` and keep passing the
remainder forward:

```csharp
NowRect header = view.TakeTop(64, out NowRect body);
NowRect footer = body.TakeBottom(52, out NowRect content);

DrawHeader(header);
DrawContent(content);
DrawFooter(footer);
```

For a sequential stack, the remainder can safely reuse the same variable:

```csharp
NowRect remaining = content;
NowRect title = remaining.TakeTop(48, out remaining);
remaining.TakeTop(12, out remaining); // gap
NowRect field = remaining.TakeTop(40, out remaining);
```

`TakeTop`, `TakeBottom`, `TakeLeft`, and `TakeRight` clamp to the available
size, never produce negative extents, and are alias-safe in this form. Use
`Centered`, `Align`, `Inset`, and `Outset` for placement within a region.

## Preferred layout API

Declare the root rect with `Column(view)` or `Row(view)`, configure containers
fluently, and finish each declaration with `Begin()` in a `using` statement:

```csharp
public sealed class SettingsPanel : NowLayoutGraphic
{
    protected override void DrawNowUI(NowRect view)
    {
        using (NowLayout.Column(view).Padding(16).Gap(12).Begin())
        {
            NowLayout.Label("Settings").SetFontSize(24).Draw();

            using (NowLayout.Row()
                .FillWidth()
                .Gap(8)
                .AlignChildren(NowLayoutAlign.Center)
                .Begin())
            {
                NowLayout.Label("Volume").Draw();
                NowLayout.Spacer();
                NowLayout.Label("80%").Draw();
            }

            using (NowLayout.Column()
                .Grow()
                .Justify(NowLayoutJustify.Center)
                .AlignChildren(NowLayoutAlign.Center)
                .Begin())
            {
                NowLayout.Button("Reset").Draw();
            }
        }
    }
}
```

The container methods cover the common cases:

- `Gap` and `Padding` control space inside a row or column.
- `Width` and `Height` set fixed dimensions; `MinWidth`, `MaxWidth`,
  `MinHeight`, and `MaxHeight` constrain them.
- `FillWidth` and `FillHeight` stretch on that axis.
- `Grow(weight)` shares the parent's remaining main-axis space with sibling
  growers. A weight of `2` receives twice the share of a weight of `1`.
  Min/max constraints freeze only the constrained share; the remaining space
  is redistributed among the other growers instead of overflowing or leaving
  an avoidable gap.
- `AlignChildren` sets the cross-axis alignment for every child;
  `AlignSelf` overrides it for one container.
- `Justify(Start|Center|End|SpaceBetween)` positions a group's children in
  otherwise-unused main-axis space.
- `Spacer(weight)` is an invisible flexible child. `Space(pixels)` inserts a
  fixed-size gap.

Nested containers keep the legacy cross-axis fill only when no alignment is
declared. Once `AlignChildren` or `AlignSelf` expresses placement intent, an
otherwise-unsized child container fits its content and can actually be
centered or end-aligned; call `FillWidth` / `FillHeight` when filling is the
intent.

`Row` and `Column` capture a stable identity from their call site. For
data-backed containers that can reorder or appear conditionally, anchor the
identity with `.SetId(item.id)` and wrap repeated controls in
`NowControls.IdScope(item.id)`.

The older `Area`, `Horizontal`, and `Vertical` scope overloads remain available
as lower-level forms. `NowLayoutOptions` is also available when options need
to be built or forwarded separately, but the fluent row/column API is the
normal starting point.

## Mixing layout with explicit drawing

`NowLayout.ReserveRect(...)` reserves a layout slot and returns its resolved
`NowRect`. Pass that rect to any free-form `Now` primitive or interaction:

```csharp
NowRect swatch = NowLayout.ReserveRect(32, 32, align: NowLayoutAlign.Center);
Now.Circle(swatch.center, 16)
    .SetColor(Color.cyan)
    .Draw();

NowRect customButton = NowLayout.ReserveRect(height: 44, stretchWidth: true);
var state = NowInput.Interact(customButton);
Now.Rectangle(customButton)
    .SetColor(state.hovered ? Color.white : Color.gray)
    .SetRadius(8)
    .Draw();
```

`ReserveRect` replaces the old `NowLayout.Rect` name. The more explicit name
makes it clear that calling it advances the active layout.

## Measurement and hosts

Choose the host that matches the placement API:

| Placement | Host | Measurement behavior |
| --- | --- | --- |
| Explicit `Now` rects | `NowGraphic` | One draw pass |
| UGUI `NowLayout` | `NowLayoutGraphic` | Exact measure + draw in the same rebuild |
| World-space `NowLayout` | `NowWorldLayoutGraphic` | Exact measure + draw in the same rebuild |
| Render-pipeline `NowLayout` | `NowPipelineLayoutGraphic` | Exact measure + draw for that graphic |
| UI Toolkit `NowLayout` | `NowLayoutVisualElement` | Exact measure + draw in the same rebuild |

The base `NowGraphic`, `NowWorldGraphic`, `NowPipelineGraphic`, and
`NowVisualElement` hosts are intentionally one-pass. They avoid paying for a
measurement pass when their content uses explicit rects. If layout code runs
in one of those hosts, its cached measurements settle on a later rebuild.

The layout-specific hosts own the complete two-pass cycle, so code inside
them should use ordinary `Row`/`Column` scopes. Do not wrap that code in
`RunMeasured`; nested measurement is unnecessary. Their `DrawNowUI` callback
runs once with drawing suppressed and input passive, then once for the real
draw. Reacting to a control's returned click/change/submit result is safe
because controls are inert during measurement. Avoid unconditional state
changes in `DrawNowUI`, or guard them with `NowLayout.isMeasurePass`.

Retained hosts reject synchronous recursive rebuilds (one host forcing another
host to rebuild from inside its draw callback), because independent retained
surfaces cannot safely share one immediate-mode frame. Let Unity rebuild the
nested host separately. For compositional content, call a shared draw method
directly from the outer callback so it participates in the current frame.

Each retained host also gives string control IDs a private per-instance
scope, so two host instances can both use `SetId("search")` without sharing
focus or cached state. When code outside the draw callback needs that numeric
ID, resolve it through the host first, for example
`NowFocus.Focus(graphic.ResolveControlId("search"))`. The same
`ResolveControlId` helper is available on all four retained host families.

`NowLayout.RunMeasured` exists for a manual host, such as a camera callback,
that has no layout-aware host class:

```csharp
void OnPostRender()
{
    using (Now.StartUI(NowScreen.recommendedUIScale))
        NowLayout.RunMeasured(NowScreen.safeArea, DrawOverlay);
}

void DrawOverlay()
{
    using (NowLayout.Column().Padding(16).Gap(8).Begin())
    {
        NowLayout.Label("Connected").Draw();
        NowLayout.Button("Disconnect").Draw();
    }
}
```

`RunMeasured` invokes the content once with drawing suppressed and input
passive, then once for the real draw. Reacting to control results is safe;
avoid unconditional state changes in the callback, or guard them with
`NowLayout.isMeasurePass`. Use the generic state overload with a static lambda
when a callback would otherwise capture and allocate.

## Invalid combinations fail at the call site

Dimensions must be finite and non-negative, growth weights must be positive,
and min/max bounds cannot contradict each other. Fixed and stretch sizing on
the same axis are mutually exclusive, so contradictory chains now throw
instead of silently letting the last setter win:

```csharp
// Invalid: choose a fixed width or a stretching width.
var options = default(NowLayoutOptions)
    .SetWidth(240)
    .SetStretchWidth();
```

Likewise, a container cannot combine `Grow` with a fixed or stretching size
on its parent's main axis. These errors are deliberate: the declaration
should communicate one sizing intent.

## Per-instance text-field appearance

Text fields can be styled where they are declared without mutating the theme
or a global renderer. The same appearance methods are available on
`Now.TextField(rect, ...)` and `NowLayout.TextField(...)`:

```csharp
NowLayout.TextField("search")
    .SetPlaceholder("Search NowUI")
    .SetStretchWidth()
    .SetMaxWidth(584)
    .SetHeight(50)
    .SetRadius(NowRadiusToken.Pill)
    .SetBackgroundColor(Color.white)
    .SetBorderColor(new Color32(218, 220, 224, 255))
    .SetFocusColor(new Color32(66, 133, 244, 255))
    .SetTextColor(new Color32(32, 33, 36, 255))
    .SetPlaceholderColor(new Color32(95, 99, 104, 255))
    .SetPadding(24, 12)
    .SetOutlineWidth(1, 2)
    .SetElevation(NowElevationToken.Raised)
    .Draw(ref query);
```

`TextField.Draw` returns a `NowTextFieldResult`, so a layout-flowing field can
host custom adornments without manually reserving its slot or dropping back to
`Now.TextField`:

```csharp
NowTextFieldResult search = NowLayout.TextField("search")
    .SetStretchWidth()
    .SetHeight(50)
    .SetPadding(44, 12, 16, 12) // leave room for the leading icon
    .Draw(ref query);

var icon = new Vector2(search.rect.x + 22, search.rect.center.y);
var iconColor = new Color32(95, 99, 104, 255);
Now.Circle(icon, 6).SetFill(false).SetOutline(2, iconColor).Draw();
Now.Line(icon + new Vector2(4, 4), icon + new Vector2(9, 9))
    .SetWidth(2).SetColor(iconColor).SetCap(NowLineCap.Round).Draw();

if (search.submitted)
    RunSearch(query);
```

The result's boolean conversion tests `changed`, not `submitted`, so
`if (NowLayout.TextField(...).Draw(ref query))` retains the usual value-control
meaning. Submission is false during an exact host's passive measure pass and
is reported only by the active draw pass.
