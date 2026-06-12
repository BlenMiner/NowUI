# Markdown

`NowUI.Extensions.Markdown` (its own assembly under `Assets/NowUI/Extensions/`)
renders GitHub-flavored Markdown through `Now`/`NowLayout` primitives — no
HTML, no JavaScript, no WebView.

## Usage

```csharp
using NowUI.Markdown;

// In layout flow — stretches to the available width, height settles one
// frame late like all scope-form layout:
var result = NowMarkdown.Draw(changelogText);

if (result.clickedLink != null)
    Application.OpenURL(result.clickedLink);

// Explicit rect:
NowMarkdown.Draw(rect, readmeText);

// Retained, for content you control the lifetime of:
var doc = NowMarkdownDocument.Parse(text);
float height = doc.MeasureHeight(width);
doc.Draw(new NowRect(x, y, width, height));
```

`NowMarkdown.Draw(string)` caches parsed documents by text (capped, cleared on
overflow). Layout — word wrap, tables, positions — is cached per width and
recomputed only when the width or font changes, so steady-state drawing
allocates nothing.

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
  Images wrapped in a link (`[![alt](img)](dest)`) are clickable. Textures
  cache in `NowMarkdownImages` (inject art with `SetTexture`).
- Code blocks carry a Copy button; the click handler defaults to the system
  clipboard and is replaceable (`NowMarkdownDocument.copyToClipboard`) for
  platforms that need their own copy flow
- Syntax highlighting in fenced code blocks for `csharp`/`cs`, `json`, and a
  C-like generic (`js`, `ts`, `c`, `cpp`, `java`): keywords, strings,
  numbers and comments, with multiline comment/verbatim-string state carried
  across lines; unknown languages render plain

## Deliberately out of scope

- Raw HTML, blocks and inline: rendered as plain text, never interpreted
- JavaScript in any form
- Indented (4-space) code blocks — use fences
- Link reference definitions (`[text][ref]`) and entity references

Code blocks render in the theme font at a slightly reduced size; NowUI has no
bundled monospace face. Assign one via a theme `code` preset if you need true
monospace.
