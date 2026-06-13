# Rich Text

Rich text has two paths:

```csharp
// Generated/tooling path: IDEs, syntax highlighting, search hits, diagnostics.
NowLayout.RichText(source)
    .SetSpans(spans)
    .Draw();
```

```csharp
// Hand-authored UI copy: labels, tooltips, and short docs snippets.
NowLayout.RichText("This is <b>bold</b> and <link=\"docs\">linked</link>.")
    .ParseDefaultTags()
    .Draw();
```

The span model is the primitive. Tag parsing is a convenience layer that
produces the same plain text, spans, inline boxes, layout, and hit-test data.

## Quick Start

Use `NowLayout.RichText(...)` when the rich text should participate in the
current layout, or `Now.RichText(rect, ...)` when you already own the rect:

```csharp
NowLayout.RichText("Status: <b>Ready</b>")
    .ParseDefaultTags()
    .Draw();
```

```csharp
var result = Now.RichText(rect, "Open <link=\"settings\">settings</link>")
    .ParseDefaultTags()
    .Draw();

if (result.clicked && result.TryGetHitTag(out var tag) && tag.name == "link")
    Navigate(tag.value);
```

Default tag parsing is opt-in. Without `.ParseDefaultTags()`, markup-like text
is rendered literally.

## Built-In Tags

The default parser supports common UI text tags:

```xml
<b>bold</b>
<i>italic</i>
<u>underlined</u>
<s>struck</s>
<strikethrough>struck</strikethrough>
<color=#ffcc00>gold</color>
<size=18>larger</size>
<link="docs/getting-started">link</link>
<br/>
<noparse><b>literal</b></noparse>
```

`<link>` produces a metadata span. Hit testing reports a tag id through
`NowRichTextHit`, while the caller decides whether that means opening a URL,
changing pages, showing a tooltip, or doing nothing.

Links render with the theme accent color and underline by default.

## Explicit Spans

Use spans when styling comes from generated ranges: syntax highlighting, search
matches, diagnostics, diffs, or any tool that already knows exact string
indices.

```csharp
var spans = new[]
{
    new NowRichTextSpan(0, 5, new NowRichTextStyle(15f, NowFontStyle.Bold)),
    new NowRichTextSpan(6, 4, new NowRichTextStyle(15f).SetUnderline()),
};

NowLayout.RichText("Hello docs")
    .SetSpans(spans)
    .Draw();
```

Spans apply to the plain source string. If you use parsed tags, the parser
creates the plain text and spans for you.

## Custom Tags

Custom tags are builder-config driven rather than global behavior:

```csharp
var result = NowLayout.RichText("Loading <lottie id=\"spinner\" size=\"18\"/>")
    .ParseDefaultTags()
    .ParseTag("lottie", NowLottieRichTextTag.Parse)
    .Draw();
```

For shared setups, create a reusable parser preset:

```csharp
var docsParser = NowRichTextParser.Default
    .WithTag("lottie", NowLottieRichTextTag.Parse);

var result = NowLayout.RichText(text)
    .UseParser(docsParser)
    .Draw();
```

The first custom tag implementation supports self-closing inline boxes. Styled
custom child tags and metadata child tags are still future work.

## Lottie Tag

The included Lottie handler parses:

```xml
<lottie id="spinner" size="18"/>
<lottie id="spinner" width="18" height="18"/>
```

`NowLottieRichTextTag.Parse` resolves `spinner` with
`Resources.Load<NowLottieAsset>("Lottie/spinner")`, then falls back to
`Resources.Load<NowLottieAsset>("spinner")`. It returns an inline box that
draws through `Now.Lottie(...)`.

If your assets are not in `Resources`, write a local handler that uses your own
registry:

```csharp
bool ParseIconTag(in NowRichTextTagContext context, out NowRichTextTagResult result)
{
    result = default;

    var icon = IconRegistry.Find(context.Attribute("id"));

    if (icon == null)
        return false;

    float size = context.FloatAttribute("size", context.style.fontSize);
    result = NowRichTextTagResult.Inline(new NowRichTextInline
    {
        width = size,
        height = size,
        payload = icon,
        draw = DrawIconInline
    });
    return true;
}
```

Register it on the builder:

```csharp
NowLayout.RichText("Saved <icon id=\"check\" size=\"16\"/>")
    .ParseDefaultTags()
    .ParseTag("icon", ParseIconTag)
    .Draw();
```

## Hit Testing

Rich text hit testing stays independent from editing:

```csharp
var result = NowLayout.RichText("Open <link=\"docs\">docs</link>")
    .ParseDefaultTags()
    .Draw();

if (result.clicked && result.TryHit(out var hit))
{
    // Inspect hit.textIndex, hit.runIndex, hit.lineIndex, hit.rect, hit.tag.
}
```

For default links, the common path is:

```csharp
if (result.clicked && result.TryGetHitTag(out var tag) && tag.name == "link")
    OpenDocsPage(tag.value);
```

Core exposes geometry and text positions:

- hit run
- hit line
- plain text index
- run-local text index
- tag/payload id
- rect for the hit text

Core does not own caret state, editor navigation, text mutation, or caret
rendering. Editors can build those policies on top of hit indices and rects.

## Selection And Copy

Rich text is not selectable by default. Enable selection per builder:

```csharp
NowLayout.RichText("Drag this text, then press Ctrl/Cmd+C.")
    .SetSelectable()
    .Draw();
```

Selectable rich text uses the shared browser-style selection helper:

- drag selects plain text
- double-click selects a word
- triple-click selects a line
- Ctrl/Cmd+A selects all while focused
- Ctrl/Cmd+C copies plain text through `NowClipboard`
- right-click opens Copy / Select All

Selection copies the rendered plain-text projection. It does not preserve
styles, spans, tags, or payload objects.

## Parser Rules

The parser is forgiving enough for UI strings but predictable:

- unrecognized tags render as literal text unless registered
- malformed tags render as literal text
- escaped `<`, `>`, and `&` are supported
- nested style tags compose
- spans remain ordered and non-overlapping after parsing
- parser output preserves plain text indices

## Open Questions

- Should links expose payload strings directly, or only stable integer ids into
  a result-owned payload table?
- Should inline boxes participate in text selection as object replacement
  characters, skipped ranges, or non-selectable holes?
