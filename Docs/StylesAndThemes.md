# Styles And Themes

`NowTheme` makes style data a first-class ScriptableObject while keeping the
runtime API code-forward. The asset stores named tokens and presets; draw code
still builds rectangles and text explicitly.

Create a theme from Unity with `Create > NowUI > Theme`.

## Theme Contents

The theme currently has five style groups.

- Palette tokens: named colors such as `background`, `surface`, `text`,
  `border`, and `accent`.
- Spacing tokens: named `Vector4(left, top, right, bottom)` insets.
- Radius tokens: named `Vector4(top-left, top-right, bottom-right,
  bottom-left)` corner radii.
- Rectangle presets: fill color, radius, padding, blur, outline, and outline
  color.
- Text presets: optional font, font size, color, outline, outline color, and
  padding.

The inspector includes a live preview so changes to colors and presets are
visible while editing the asset.

## Drawing With A Theme

Use the theme as a factory when the style should be explicit at the call site.

```csharp
using UnityEngine;

public sealed class ThemedOverlay : MonoBehaviour
{
    [SerializeField] NowTheme theme;
    [SerializeField] NowFont font;

    void OnPostRender()
    {
        Now.StartUI();

        Vector4 panel = new Vector4(24, 24, 360, 180);

        theme.Rectangle(panel, "surface")
            .Draw();

        theme.Text(theme.Inset(panel, "panel"), font, "title")
            .Draw("Theme Preview");

        Now.FlushUI();
    }
}
```

Use `SetStyle` when geometry is built first and style is chosen later.

```csharp
Vector4 button = new Vector4(40, 120, 132, 40);

Now.Rectangle(button)
    .SetStyle(theme, "accent")
    .Draw();

Now.Text(button, font)
    .SetStyle(theme, "button")
    .Draw("Save");
```

## Spacing Helpers

Use named spacing tokens to keep layout code readable without adding a retained
layout engine.

```csharp
Vector4 panel = new Vector4(20, 20, 320, 180);
Vector4 content = theme.Inset(panel, "panel");
Vector4 focusRing = theme.Outset(panel, "xs");

theme.Rectangle(panel, "surface").Draw();
theme.Rectangle(focusRing, "outline").Draw();

theme.Text(content, font, "body")
    .Draw("The same rect can be inset or outset by named spacing tokens.");
```

`NowTheme.Inset(rect, spacing)` and `NowTheme.Outset(rect, spacing)` also
accept raw `Vector4` spacing values for one-off calculations.

## Token Lookup

When custom drawing code needs a raw token value, resolve it from the theme.

```csharp
Color accent = theme.GetColor("accent", Color.blue);
Vector4 compact = theme.GetSpacing("sm", new Vector4(8, 8, 8, 8));
Vector4 radius = theme.GetRadius("md", new Vector4(8, 8, 8, 8));

Now.Rectangle(new Vector4(10, 10, 180, 48))
    .SetColor(accent)
    .SetRadius(radius)
    .SetPadding(compact)
    .Draw();
```

`TryGetColor`, `TryGetSpacing`, `TryGetRadius`, `TryGetRectanglePreset`, and
`TryGetTextPreset` are available when callers need to distinguish missing
tokens from fallback values.

## Recommended Preset Naming

Prefer role-based preset names over visual names. A preset named `accent` or
`danger` can change color later without changing call sites. A preset named
`blue-button` leaks one palette decision into code.

Good rectangle preset names:

- `surface`
- `muted`
- `outline`
- `accent`
- `danger`
- `selection`

Good text preset names:

- `title`
- `body`
- `muted`
- `caption`
- `button`
- `code`

Composite elements such as buttons, radio buttons, and input fields should build
on these token and preset layers later. Keep those as separate docs once they
exist so this page remains about the low-level style system.
