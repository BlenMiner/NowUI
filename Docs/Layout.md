# Layout

`NowLayout` is a flexbox-style immediate-mode layout system: nest horizontal
and vertical groups, mix fixed sizes with weighted stretching, and draw
elements without computing rects by hand.

## Basics

Open an area over a rect, then nest groups. Scopes follow the same
`using`-disposes-the-group pattern as the rest of NowUI:

```csharp
Now.StartUI();

using (NowLayout.Area(NowScreen.safeArea))
using (NowLayout.Vertical(padding: 16, spacing: 8))
{
    NowLayout.Label("Settings", 24).Draw();

    using (NowLayout.Horizontal())
    {
        NowLayout.Label("Volume").Draw();
        NowLayout.FlexibleSpace();
        NowLayout.Label("80%").Draw();
    }
}

Now.FlushUI();
```

`NowLayout.Rect(...)` reserves space and returns the resolved rect, which is
the bridge to free-form drawing and interaction:

```csharp
NowRect rect = NowLayout.Rect(160, 44);
var state = NowInput.Interact("save", rect);

Now.Rectangle(rect)
    .SetColor(state.hovered ? Color.white : Color.gray)
    .SetRadius(6)
    .Draw();
```

## Sizing options

Groups and rects take their common settings as optional parameters — no
options struct needed for the everyday cases:

```csharp
using (NowLayout.Horizontal(spacing: 8, alignItems: NowLayoutAlign.Center)) { ... }
using (NowLayout.Vertical(spacing: 10, stretchWidth: true)) { ... }
using (NowLayout.Area(rect, padding: 16, spacing: 8)) { ... }
NowLayout.Area(rect, DrawContent, padding: 16);           // callback form too
NowRect bar = NowLayout.Rect(height: 22, stretchWidth: true);
```

For everything beyond them — min/max sizes, weighted stretching, per-element
alignment — `NowLayoutOptions` is a fluent struct; a default instance means
"auto" (content size, stretch across the parent's cross axis). Shorthand
factories exist on `NowLayout`:

```csharp
NowLayout.Width(120)                  // fixed main-axis size
NowLayout.Size(120, 44)
NowLayout.StretchWidth()              // weighted share of remaining space
NowLayout.StretchWidth(2f)            // twice the share of weight-1 siblings
options.SetMinWidth(80).SetMaxWidth(240)
options.SetSpacing(8).SetPadding(12)  // groups only
options.SetAlign(NowLayoutAlign.Center)       // this element, on the parent's cross axis
options.SetAlignItems(NowLayoutAlign.Center)  // groups only: default for the children
```

`SetAlign` positions one element on its parent's cross axis (vertical in a
horizontal group); `SetAlignItems` on a group sets the default for all its
children — flexbox's `align-items` — with a child's own `SetAlign` taking
precedence.

`Space(pixels)` inserts a fixed gap; `FlexibleSpace(weight)` absorbs remaining
space like an invisible stretch element.

## Two measurement modes

- **Scope form** (`using (NowLayout.Area(rect))`): single pass; auto and
  stretch sizes come from measurements cached on the previous frame, so a
  brand-new layout settles on its second frame. Cheap and right for layouts
  whose structure is stable.
- **Callback form** (`NowLayout.Area(rect, () => { ... })`): runs the callback
  twice in the same frame — a measure pass with drawing and input suppressed,
  then the real pass. Exact from frame one, at twice the layout cost.

When a layout's identity is ambiguous across frames (rows generated in a loop,
collapsing panels), pass an explicit id: `NowLayout.Vertical($"row-{i}")`.

During the callback form's measure pass, `NowInput.Interact` reports hover
but never presses or drags, so interaction code is safe to run in both passes.
