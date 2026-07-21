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

Markup also embeds inside rendered Markdown: a ` ```markup ` fence drawn
with the `NowMarkupEmbeds` bridge renders as live markup instead of code —
see [Markdown.md](Markdown.md#live-embeds).

`NowMarkup.Document(markupText)` is for markup strings you already loaded.
`NowMarkup.File(path)` is for hot-reloadable disk files. It uses
`FileSystemWatcher` when possible and falls back to timestamp polling; watcher
callbacks only mark the source dirty, and parsing still happens on the main
thread during `Draw()` / `Refresh()`.

Paths can be absolute or project-relative. In the Unity editor, project-relative
paths such as `Assets/UI/main.nowui` are the normal choice. This is a developer
workflow feature; deployed players still need the target file to exist on disk.

## Example

The packaged [docs browser source](../Example/NowDocsExample.cs) contains a
live markup-authored panel, shows the `NowMarkupState` keys that drive
visibility and gallery movement, reports the latest event, and displays the
source beside the rendered UI.

## Supported Tags

Layout and text:

- `column`, `row`: layout containers.
- `panel`, `card`, `section`, `div`: vertical containers, useful with
  background styling.
- `text`, `label`, `p`, `richtext`: rich text using NowUI default tags.
- `h1` … `h6`: headings — bold text with a size scale (32/26/22/18/16/14);
  `font-size`/`font-style` styles override the defaults.
- `hr` (or `divider`): themed horizontal rule; style with `color` and
  `height`.
- `ul`, `ol`, `li`: bulleted or numbered lists; items hold inline text or
  nested block elements.
- `scroll`: scroll view container.
- `space`, `flex`: fixed or flexible layout gaps.
- `style`: small stylesheet block.

Controls:

- `button`: click control; reports `NowMarkupEventKind.Click`.
- `checkbox`: boolean control bound to `state`.
- `switch` (or `toggle`): boolean switch bound to `state`; `checked` seeds
  the initial value.
- `radio`: HTML-style radio group. All radios sharing a `group` key write
  their `value` (int or string) into that state key; `checked` seeds the
  group once.
- `slider`: float control bound to `state`, with `min`, `max`, and `step`.
- `progress`: progress bar; bind `state` or set `value`, with `max`
  (default 1). Without a value it renders indeterminate — point `time` at a
  state key you update with `Time.time` to animate the sweep (no hidden
  clocks).
- `textfield`, `textarea`: string controls bound to `state`.
- `dropdown`/`select`: integer selected index bound to `state`; use
  `options="One|Two|Three"` or child `<option>One</option>` tags. A child
  `<option selected>` seeds the default row.
- `badge`, `chip`: status label and clickable pill; a chip with `state`
  toggles that boolean key when clicked.

Structure:

- `if`, `show`: visibility-only wrappers.
- `details`/`summary`: collapsible section rendered as a foldout; `open`
  seeds the initial expansion and the open flag lives in `state` (or the
  element id).
- `tabs` with `tab title="..."` children: tab bar plus the selected pane,
  bound by `index` or `state`; `stretch-tabs="true"` makes a segmented bar.
- `gallery`: shows one child at a time, bound by `index` or `state`; add
  `controls="true"` for built-in Prev/Next buttons.
- `slide`, `item`: optional gallery child wrappers.

Text supports the default rich-text inline tags, plus HTML aliases
(`strong` → `b`, `em` → `i`, `del`/`strike` → `s`):

```xml
<b>bold</b> <i>italic</i> <u>underlined</u> <s>strikethrough</s>
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
- `group`: shared state key for a radio group (takes precedence on `radio`).
- If no state key is provided, interactive controls use their `id`.
- `checked` (checkbox/switch/radio), `open` (details), and
  `<option selected>` (dropdown) seed initial values, HTML-style.

Events:

- Buttons always report a click event with their `id`; radios and chips
  report both a click and a change.
- Value controls report change events with `id`, state key, and value.
- `result.Changed(name)` matches either the element id or the state key.
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

## Avoiding Hard-Coded Strings

Markup lookups are strings by nature, so the extension attacks the two ways
strings go wrong — silent typos and scattered duplication:

- **Query validation.** Every parsed document builds a manifest of the ids,
  state keys, and `emit(...)` action names it declares
  (`document.manifest`). In the editor and development builds, a result
  query that names something the document never declares —
  `result.Clicked("sve")` — logs a one-time warning listing what is
  declared, instead of silently returning false forever. Release players
  skip the check; opt out with `NowMarkup.validateQueries = false`.
- **Generated bindings.** Right-click a markup file and choose
  **Assets → NowUI → Generate Markup Bindings** (or call
  `NowMarkupBindings.GenerateSource(document, "MainMarkup")`) to emit a
  constants class next to the file:

```csharp
public static class MainMarkup
{
    public static class Ids { public const string Save = "save"; }
    public static class Keys { public const string Volume = "volume"; }
    public static class Actions { public const string SaveGame = "save-game"; }
}
```

```csharp
if (result.Clicked(MainMarkup.Ids.Save))
    Save(uiState.GetFloat(MainMarkup.Keys.Volume));
```

The markup file stays the single source of truth: rename an id, regenerate,
and every stale C# reference becomes a compile error instead of a dead
lookup.

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
