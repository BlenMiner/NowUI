# Markdown

`NowUI.Extensions.Markdown` (its own assembly under `Assets/NowUI/Extensions/`)
renders GitHub-flavored Markdown through `Now`/`NowLayout` primitives — no
HTML, no JavaScript, no WebView.

## Usage

```csharp
using NowUI.Markdown;

// In layout flow — stretches to the available width, height settles one
// frame late like all scope-form layout:
var result = NowMarkdown.Document(changelogText).Draw();

if (result.clickedLink != null)
    Application.OpenURL(result.clickedLink);

// Explicit rect:
NowMarkdown.Document(readmeText).Draw(rect);

// Builder options:
NowMarkdown.Document(readmeText)
    .SetFontSize(16)
    .SetWidth(520)
    .Draw();

// Retained, for content you control the lifetime of:
var doc = NowMarkdown.Parse(text);
float height = doc.MeasureHeight(width);
doc.Draw(new NowRect(x, y, width, height));
```

`NowMarkdown.Document(string).Draw()` caches parsed documents by text (capped,
cleared on overflow). Layout — word wrap, tables, positions — is cached per
width and recomputed only when the width or font changes, so steady-state
drawing allocates nothing.

Colors come from the ambient theme (`Text`, `TextMuted`, `Accent` for links
and quote bars, `SurfaceMuted` for code panels, `Border` for rules and table
lines). The base font size comes from `NowMarkdownStyle` (default 15).

Links are not opened automatically: the result reports `clickedLink` /
`hoveredLink` and the caller decides (browser, in-app navigation, nothing).

## Supported syntax

- ATX (`#`..`######`) and setext (`===`/`---`) headings
- Paragraphs, hard breaks (trailing `\` or two spaces), soft breaks
- `**bold**`, `*italic*`, `~~strikethrough~~` (GFM), `` `inline code` ``
- Fenced code blocks (``` and ~~~, info string preserved)
- Block quotes, thematic breaks
- Bullet and ordered lists (nested), GFM task lists (`- [x]`)
- GFM pipe tables with `:---:` alignment and `\|` escapes
- `[links](url)`, http/https/www autolinks (GFM, trailing punctuation
  trimmed), backslash escapes
- `![images](url)` — http/https URLs download asynchronously, other paths
  load from `Resources`; drawn at native size scaled down to the available
  width with a placeholder while loading and the alt text on failure.
  Images wrapped in a link (`[![alt](img)](dest)`) are clickable, and
  right-clicking an image offers "Copy image address" (Unity has no managed
  image-clipboard API, so there is no bitmap copy). Textures cache in
  `NowMarkdownImages` (inject art with `SetTexture`).
- Text is selectable like a website: drag across paragraphs, headings and
  code blocks in one sweep (double-click for a word, triple-click for a
  line, Ctrl/Cmd+A for all),
  Ctrl/Cmd+C copies, and right-click offers Copy / Select All. Code blocks
  also carry a hover Copy button for the whole block. Everything copies
  through the single `NowClipboard` hook — replace `setText`/`getText`
  once for platforms with their own clipboard flow and every path follows.
- Syntax highlighting in fenced code blocks for `csharp`/`cs`, `json`, and a
  C-like generic (`js`, `ts`, `c`, `cpp`, `java`): keywords, strings,
  numbers and comments, with multiline comment/verbatim-string state carried
  across lines; unknown languages render plain

## Live embeds

Fenced blocks can render as live content instead of highlighted code. Embeds
are opt-in per draw: pass a caller-owned `NowMarkdownEmbedSet` mapping fence
info strings to renderers, and any fence whose info string's first word
matches renders through the registered `NowMarkdownEmbedRenderer` — drawn
live every frame inside the document flow, with the measured height fed back
into layout (one frame late, like images). Without a set, every fence stays
an ordinary code block, so documents degrade gracefully on GitHub and in
renderers that never wire embeds up.

The bundled `NowUI.Extensions.Markdown.Markup` bridge turns ` ```markup `
(or ` ```nowui `) fences into live [NowUI markup](Markup.md) — controls,
state bindings, and events included:

```csharp
readonly NowMarkupEmbeds _embeds = new NowMarkupEmbeds();

var result = NowMarkdown.Document(text).SetEmbeds(_embeds).Draw();

if (_embeds.Clicked("save"))
    Save();
```

````markdown
# Settings

Adjust and save:

```markup
<column gap="8">
  <row gap="8" align-items="center">
    <text>Volume</text>
    <slider state="volume" min="0" max="1" style="stretch: 1" />
  </row>
  <button id="save" variant="Accent" on-click="emit(save)">Save</button>
</column>
```
````

Every embedded block shares the `NowMarkupEmbeds` instance's
`NowMarkupState`, so a slider in one block can drive visibility in another
block of the same document; interaction identity is scoped per embed, so two
identical snippets never fight over focus. Seed and read values through
`embeds.state`, and query events with `Clicked`/`Changed`/`Action` after the
draw. Custom embeds (charts, diagrams, anything drawable) register the same
way: `new NowMarkdownEmbedSet().Add("chart", DrawChart)`.

Embeds run arbitrary UI from document content — only enable them for
markdown you control, not for untrusted user or network content.

## Deliberately out of scope

- Raw HTML, blocks and inline: rendered as plain text, never interpreted
- JavaScript in any form
- Indented (4-space) code blocks — use fences
- Link reference definitions (`[text][ref]`) and entity references

Code blocks render in the theme font at a slightly reduced size; NowUI has no
bundled monospace face. Assign one via a theme `code` preset if you need true
monospace.
