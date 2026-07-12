# Styles And Themes

`NowThemeAsset` makes style data a first-class ScriptableObject while keeping
the runtime API code-forward. The asset stores enum-keyed fixed slots for
tokens, presets and control styles; draw code still builds rectangles and text
explicitly.

Create a theme from Unity with `Create > NowUI > Theme`.

The package includes built-in `Default`, `DefaultDark`, `Material`, and
`MaterialDark` theme assets under `Assets/NowUI/Assets/Themes`. They share the
same enum tokens and presets, so UI code can switch between them without
changing call sites. `Default`/`DefaultDark` use the built-in control renderer
(soft shadows, line-drawn glyphs, offset focus rings, pressed scale); the
Material themes assign `MaterialControlRenderer` for a state-layer/ripple
design language.

Light and dark twins are cross-linked through each asset's counterpart slot,
so one flag switches the whole UI:

```csharp
NowTheme.preferDark = systemDarkMode;
```

## Theme Contents

The theme currently has seven style groups.

- Palette tokens: one fixed color slot per `NowColorToken`. Beyond the base
  eight (background, surfaces, text, border, accent) the palette covers state
  variants (`SurfaceHover`, `SurfacePressed`, `AccentHover`, `AccentPressed`,
  `AccentMuted`), emphasis (`SurfaceElevated`, `BorderStrong`, `FocusRing`),
  status roles (`Success`/`Warning`/`Danger`, each with `*Text` and `*Muted`),
  and depth (`Shadow`, `Scrim`). Themes authored before the extended set keep
  working: missing roles are derived from the base eight on load.
- Spacing tokens: `Vector4(left, top, right, bottom)` insets keyed by
  `NowSpacingToken` (`Xs` 4 through `Xxl` 32, plus `Panel`).
- Radius tokens: `Vector4(top-right, bottom-right, top-left, bottom-left)`
  corner radii keyed by `NowRadiusToken` (`Sm` 6 through `Xl` 24, plus `Pill`).
  This is the packed order raw `Vector4` radii use everywhere (matching
  `NowCornerRadius.packed`); to author corners by name use
  `new NowCornerRadius(topLeft, topRight, bottomRight, bottomLeft).packed`.
- Shadow presets: two-layer (key + ambient) drop shadows keyed by
  `NowElevationToken` (`Raised`, `Overlay`, `Modal`), with a dark-mode alpha
  scale. Draw them with `DrawElevated`:
  `theme.Rectangle(rect, NowRectangleStyle.Elevated).DrawElevated(theme, NowElevationToken.Raised);`
- Rectangle presets: fill color, radius, padding, blur, outline, outline
  color, and optional elevation, with one fixed preset slot per
  `NowRectangleStyle` (`Surface`, `Muted`, `Outline`, `Accent`, `Elevated`,
  `AccentSoft`, `Danger`, `Ghost`).
- Text presets: optional font, font size, font style (weight), color, outline,
  outline color, and padding, with one fixed preset slot per `NowTextStyle`.
  The type scale runs `Display` 34 / `Title` 26 / `Heading` 20 / `Subheading`
  17 / `Body`+`BodyStrong` 15 / `Button` 15 / `Label` 13 / `Muted` 13 /
  `Caption` 12.
- Control styles and an optional `NowControlRenderer` that built-in controls
  use for metrics and visual rendering. Control styles include component
  padding, min heights, radii, state-layer opacity, focus ring offset,
  disabled opacity, minimum touch target, popup metrics, and scrollbar
  metrics.

These groups are not add/remove lists in the inspector; every enum value has a
known serialized field, so themes cannot accidentally omit or duplicate a
built-in slot. The asset preview slot renders the theme while editing.

## Generating Themes

The `NowThemeAsset` inspector includes a generator for quick palette
exploration. Choose light or dark mode, set a key color and an accent color,
then press **Derive From Key Colors** to update the built-in palette tokens and
matching preset fallbacks. Press **Random From Seed** for a reproducible
generated palette, or **New Random** to pick a fresh seed and leave it visible
for later. The generator controls are serialized on the theme asset, so
Undo/Redo works through Unity's normal inspector flow and duplicated themes
keep their generator settings.

## Drawing With A Theme

Use the theme as a factory when the style should be explicit at the call site.

```csharp
using UnityEngine;

public sealed class ThemedOverlay : MonoBehaviour
{
    [SerializeField] NowThemeAsset theme;
    [SerializeField] NowFont font;

    void OnPostRender()
    {
        using (Now.StartUI())
        {
            Vector4 panel = new Vector4(24, 24, 360, 180);

            theme.Rectangle(panel, NowRectangleStyle.Surface)
                .Draw();

            theme.Text(theme.Inset(panel, NowSpacingToken.Panel), font, NowTextStyle.Title)
                .Draw("Theme Preview");
        }
    }
}
```

Use `SetStyle` when geometry is built first and style is chosen later.

```csharp
Vector4 button = new Vector4(40, 120, 132, 40);

Now.Rectangle(button)
    .SetStyle(theme, NowRectangleStyle.Accent)
    .Draw();

Now.Text(button, font)
    .SetStyle(theme, NowTextStyle.Button)
    .Draw("Save");
```

## Spacing Helpers

Use spacing tokens to keep layout code readable without adding a retained
layout engine.

```csharp
Vector4 panel = new Vector4(20, 20, 320, 180);
Vector4 content = theme.Inset(panel, NowSpacingToken.Panel);
Vector4 focusRing = theme.Outset(panel, NowSpacingToken.Xs);

theme.Rectangle(panel, NowRectangleStyle.Surface).Draw();
theme.Rectangle(focusRing, NowRectangleStyle.Outline).Draw();

theme.Text(content, font, NowTextStyle.Body)
    .Draw("The same rect can be inset or outset by spacing tokens.");
```

`NowTheme.Inset(rect, spacing)` and `NowTheme.Outset(rect, spacing)` are also
available for one-off calculations with raw `Vector4` spacing values.

## Token Lookup

When custom drawing code needs a raw token value, resolve it from the theme.

```csharp
Color accent = theme.GetColor(NowColorToken.Accent);
Vector4 compact = theme.GetSpacing(NowSpacingToken.Sm, new Vector4(8, 8, 8, 8));
Vector4 radius = theme.GetRadius(NowRadiusToken.Md, new Vector4(10, 10, 10, 10));

Now.Rectangle(new Vector4(10, 10, 180, 48))
    .SetColor(accent)
    .SetRadius(radius)
    .SetPadding(compact)
    .Draw();
```

`TryGetColor`, `TryGetSpacing`, `TryGetRadius`, `TryGetRectanglePreset`, and
`TryGetTextPreset` are available for defensive callers; valid enum values map
to fixed serialized slots.

## Control Renderers

Built-in controls keep their behavior in the control builders, but their metrics
and visuals are routed through the active theme:

```csharp
[CreateAssetMenu(menuName = "NowUI/Control Renderer")]
public sealed class FlatControls : NowControlRenderer
{
    public override void DrawButton(in NowButtonRenderContext context)
    {
        Now.Rectangle(context.rect)
            .SetColor(context.themeAsset.GetColor(NowColorToken.Accent, Color.blue))
            .Draw();
    }
}
```

Assign a renderer on a `NowThemeAsset` to change how built-in buttons,
checkboxes, radios, sliders, text fields, text areas, dropdowns, context menus
and scrollbars draw. Leave it empty to use the default renderer.

The shipped design-system renderers map the small NowUI style surface onto the
closest native variants:

- Default: `Accent` is solid, `Surface`/`Elevated` are filled cards with a
  border (`Elevated` adds a drop shadow), `Outline` is bordered, `Ghost` is
  transparent until hovered, `AccentSoft` is a tinted secondary, and `Danger`
  is the destructive variant.
- Material: `Accent` is filled, `Muted` is tonal, `Outline` is outlined, and
  `Surface` behaves like a text button.

The renderer hook intentionally keeps behavior in the built-in controls. It can
replace visuals and measurements, but exact parity with larger design systems
needs more control-state and component-slot data than NowUI exposes today:

- Pending/loading, read-only, invalid, and required states.
- Focus-visible versus pointer focus, instead of a single `focused` bit.
- Leading/trailing icons, field labels, helper/error text, and clear buttons in
  text fields and dropdowns.
- Line-height and letter-spacing tokens.
