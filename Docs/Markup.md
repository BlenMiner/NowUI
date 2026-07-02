# Markup

`NowUI.Extensions.Markup` is a constrained, AI-friendly markup layer that maps
known XML-like tags to NowUI layout, rich text, controls, state, and events. It
is not browser HTML, does not run JavaScript, and does not implement full CSS.

## Quick Start

```csharp
using NowUI.Markup;

NowMarkupState uiState = new NowMarkupState();
NowMarkupFile hotFile;

void Awake()
{
    hotFile = NowMarkup.File("Assets/UI/main.nowui");
}

void OnPostRender()
{
    using (Now.StartUI())
    {
        var result = hotFile.Draw(NowScreen.safeArea, uiState);

        if (result.Clicked("save"))
            Save();
    }
}
```

```xml
<style>
  .card { padding: 16; gap: 8; rect-style: Surface; radius: 8; }
  button.primary { variant: Accent; width: 120; }
</style>

<column class="card">
  <text style="font-size: 24">Settings</text>

  <row gap="8" align-items="center">
    <text>Volume</text>
    <slider id="volume" state="volume" min="0" max="1" step="0.05" style="stretch: 1" />
  </row>

  <button id="toggle-details" variant="Outline" on-click="toggle(details)">
    Toggle details
  </button>

  <column visible="details" gap="6">
    <textfield id="display-name" state="name" placeholder="Display name" />
    <button id="save" class="primary" on-click="emit(save)">Save</button>
  </column>
</column>
```

`NowMarkup.Document(markupText)` is for markup strings you already loaded.
`NowMarkup.File(path)` is for hot-reloadable disk files. It uses
`FileSystemWatcher` when possible and falls back to timestamp polling; watcher
callbacks only mark the source dirty, and parsing still happens on the main
thread during `Draw()` / `Refresh()`.

Paths can be absolute or project-relative. In the Unity editor, project-relative
paths such as `Assets/UI/main.nowui` are the normal choice. This is a developer
workflow feature; deployed players still need the target file to exist on disk.

## Docs Scene Demo

Open `Assets/Scenes/DocsScene.unity` and select **Markup demo**. The page
renders a live markup-authored panel, shows the `NowMarkupState` keys that
drive visibility and gallery movement, reports the latest markup event, and
includes the source markup beside the rendered UI.

## Supported Tags

- `column`, `row`: layout containers.
- `panel`, `card`, `section`, `div`: vertical containers, useful with
  background styling.
- `text`, `label`, `p`, `richtext`: rich text using NowUI default tags.
- `button`: click control; reports `NowMarkupEventKind.Click`.
- `checkbox`: boolean control bound to `state`.
- `slider`: float control bound to `state`, with `min`, `max`, and `step`.
- `textfield`, `textarea`: string controls bound to `state`.
- `dropdown`/`select`: integer selected index bound to `state`; use
  `options="One|Two|Three"` or child `<option>One</option>` tags.
- `scroll`: scroll view container.
- `if`, `show`: visibility-only wrappers.
- `gallery`: shows one child at a time, bound by `index` or `state`; add
  `controls="true"` for built-in Prev/Next buttons.
- `slide`, `item`: optional gallery child wrappers.
- `space`, `flex`: fixed or flexible layout gaps.
- `style`: small stylesheet block.

Text supports the default rich-text inline tags:

```xml
<b>bold</b> <i>italic</i> <u>underlined</u>
<color=#ffcc00>gold</color> <size=18>large</size>
<link="docs">link</link> <br/>
```

## Attributes

Common:

- `id`: stable control/layout identity. Use this on every interactive element.
- `class`: space-separated style classes.
- `style`: inline declarations such as `width: 120; gap: 8`.
- `visible`, `show`, `if`, `when`: render when the boolean expression is true.
- `hidden`: render when the boolean expression is false.

State:

- `state`, `bind`, `value-key`: key in `NowMarkupState`.
- If no state key is provided, interactive controls use their `id`.

Events:

- Buttons always report a click event with their `id`.
- Value controls report change events with `id`, state key, and value.
- `on-click` and `on-change` run small UI actions.

Actions:

```xml
on-click="emit(save)"
on-click="toggle(details)"
on-click="show(details); hide(summary)"
on-click="set(tab,2)"
on-click="next(slide,3)"
on-click="prev(slide,3)"
on-click="add(score,1)"
```

`emit(name)` adds an action event. The other actions mutate `NowMarkupState`.
`next`/`prev` wrap when a count is supplied.

## Style Properties

Selectors are intentionally small: `tag`, `.class`, `#id`, and `tag.class`.
Inline `style=""` wins over stylesheet rules.

Supported declarations:

- Layout: `width`, `height`, `min-width`, `max-width`, `min-height`,
  `max-height`, `stretch`, `stretch-width`, `stretch-height`, `padding`,
  `gap`, `spacing`, `align`, `align-items`.
- Text: `font-size`, `size`, `color`, `font-style`, `selectable`.
- Controls and panels: `variant`, `rect-style`, `text-style`, `background`,
  `background-color`, `bg`, `radius`.

Values are NowUI units. `px` suffixes are accepted but treated as unitless
NowUI layout units.

## What AI Should Not Emit

- No `<script>`, JavaScript, event handler code, or arbitrary C#.
- No browser CSS features: descendant selectors, flexbox CSS syntax, media
  queries, pseudo classes, animations, grid, percentages, `calc`, or external
  stylesheets.
- No arbitrary HTML tags unless they are wrappers around supported children.
- No asset loading, images, or remote URLs in this first markup layer.
- No reliance on implicit IDs for controls that appear in lists or can move.

Prefer shallow layouts, explicit IDs, and state keys:

```xml
<button id="next-photo" on-click="next(photo,5)">Next</button>
<gallery id="photos" index="photo">
  <slide><text>Photo 1</text></slide>
  <slide><text>Photo 2</text></slide>
</gallery>
```
