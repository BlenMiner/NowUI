# Styles And Themes

`NowThemeAsset` makes style data a first-class ScriptableObject while keeping
the runtime API code-forward. The asset stores enum-keyed fixed slots for
tokens, presets and control styles; draw code still builds rectangles and text
explicitly.

Create a theme from Unity with `Create > NowUI > Theme`.

The package includes built-in `White`, `Dark`, `Night`, `Material`,
`MaterialDark`, `HeroUI`, `HeroUIDark`, `UnityEditor`, and `UnityEditorDark`
theme assets under `Assets/NowUI/Assets/Themes`. They share the same enum tokens
and presets, so UI code can switch between them without changing call sites. The
Material themes assign `MaterialControlRenderer`, the HeroUI themes assign
`HeroUIControlRenderer`, and the Unity Editor themes assign
`UnityEditorControlRenderer`, so built-in controls can use their own metrics,
rounded shapes, focus rings, state layers, popup surfaces, and field treatment.

## Theme Contents

The theme currently has six style groups.

- Palette tokens: one fixed color slot per `NowColorToken`.
- Spacing tokens: `Vector4(left, top, right, bottom)` insets keyed by
  `NowSpacingToken`.
- Radius tokens: `Vector4(top-left, top-right, bottom-right, bottom-left)`
  corner radii keyed by `NowRadiusToken`.
- Rectangle presets: fill color, radius, padding, blur, outline, and outline
  color, with one fixed preset slot per `NowRectangleStyle`.
- Text presets: optional font, font size, color, outline, outline color, and
  padding, with one fixed preset slot per `NowTextStyle`.
- Control styles and an optional `NowControlRenderer` that built-in controls
  use for metrics and visual rendering. Control styles include component
  padding, min heights, radii, state-layer opacity, toggle/slider state-layer
  size, popup metrics, and scrollbar metrics.

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
Color accent = theme.GetColor(NowColorToken.Accent, Color.blue);
Vector4 compact = theme.GetSpacing(NowSpacingToken.Sm, new Vector4(8, 8, 8, 8));
Vector4 radius = theme.GetRadius(NowRadiusToken.Md, new Vector4(8, 8, 8, 8));

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

- Material: `Accent` is filled, `Muted` is tonal, `Outline` is outlined, and
  `Surface` behaves like a text button.
- HeroUI: `Accent` is solid, `Muted` is flat/default, `Outline` is bordered,
  and `Surface` is ghost-like.
- Unity Editor: all button-like variants stay compact and grey; selected rows
  and text selections use the Editor highlight token.

The renderer hook intentionally keeps behavior in the built-in controls. It can
replace visuals and measurements, but exact parity with larger design systems
needs more control-state and component-slot data than NowUI exposes today:

- Disabled, pending/loading, read-only, invalid, and required states.
- Focus-visible versus pointer focus, instead of a single `focused` bit.
- Per-component variants beyond the four rectangle styles, such as Material
  elevated buttons or HeroUI shadow/faded/light variants.
- First-class elevation/shadow and pressed-scale/shape animation tokens instead
  of renderer-local approximations.
- Leading/trailing icons, field labels, helper/error text, and clear buttons in
  text fields and dropdowns.
- Typography weight, line-height, letter-spacing, and font-family tokens.
