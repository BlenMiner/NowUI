# Rich Text Design Notes

Rich text should support two use cases without forcing one through the other:

```csharp
// Generated/tooling path: IDEs, syntax highlighting, search hits, diagnostics.
NowLayout.RichText(source)
    .SetSpans(spans)
    .Draw();
```

```csharp
// Hand-authored UI copy: labels, tooltips, small docs snippets.
NowLayout.RichText("This is <b>bold</b> and <link=\"docs\">linked</link>.")
    .ParseDefaultTags()
    .Draw();
```

The span model is the primitive. Tag parsing should be a convenience layer that
produces the same plain text, spans, and inline boxes used by direct callers.
That keeps rendering, layout, selection projection, and hit testing on one path.

The first implementation supports default tag parsing and custom self-closing
inline tags. Lottie is the first concrete inline tag handler:

```csharp
var result = NowLayout.RichText("Loading <lottie id=\"spinner\" size=\"18\"/>")
    .ParseDefaultTags()
    .ParseTag("lottie", NowLottieRichTextTag.Parse)
    .Draw();
```

## Layers

`NowRichTextStyle` is visual text styling only:

- font size
- bold / italic
- underline
- strikethrough
- color

`NowRichTextSpan` is a range over plain text plus style and metadata:

- source start and length
- resolved style
- optional tag id or payload reference

`NowRichTextInline` is the non-text extension point:

- fixed or measured size
- layout rect after flow
- optional tag/payload reference
- renderer supplied by an extension

The core rich text flow knows how to place text runs and inline boxes. Tag
handlers own extension-specific loading and rendering. For example,
`NowLottieRichTextTag.Parse` resolves a `NowLottieAsset` from `Resources` and
returns an inline box that draws through `Now.Lottie(...)`.

## Built-In Tags

The first parser pass should cover common UI text:

```xml
<b>bold</b>
<i>italic</i>
<u>underlined</u>
<s>struck</s>
<color=#ffcc00>gold</color>
<size=18>larger</size>
<link="docs/getting-started">link</link>
```

`<link>` should produce a metadata span. Hit testing can then report a tag or
payload id through `NowRichTextHit`, while users decide whether that means
opening a URL, showing a tooltip, drawing a hover affordance, or doing nothing.

## Custom Tags

Custom tags should be builder-config driven rather than global behavior. The
builder can collect tag handlers, then parse once during `Draw()`:

```csharp
var result = NowLayout.RichText("Loading <lottie id=\"spinner\" size=\"18\"/>")
    .ParseDefaultTags()
    .ParseTag("lottie", NowLottieRichTextTag.Parse)
    .Draw();
```

For shared setups, a reusable parser/config object can still be useful:

```csharp
var docsParser = NowRichTextParser.Default
    .WithTag("lottie", NowLottieRichTextTag.Parse);

var result = NowLayout.RichText(text)
    .ParseTags(docsParser)
    .Draw();
```

A custom tag parser should eventually be able to return one of three results:

- a styled span over child text
- a metadata span over child text
- an inline box with size, payload, and extension-owned rendering

The first implementation covers inline boxes.

For example, a Lottie extension could parse:

```xml
<lottie id="spinner" width="18" height="18"/>
```

The included Lottie handler resolves `spinner` with
`Resources.Load<NowLottieAsset>("Lottie/spinner")`, then falls back to
`Resources.Load<NowLottieAsset>("spinner")`. Other extensions can use an asset
registry or caller-provided lookup. Core only receives the measured inline box
and the renderer/payload needed to draw it.

## Hit Testing

Rich text hit testing should stay independent from editing:

```csharp
var result = NowLayout.RichText("Open <link=\"docs\">docs</link>")
    .ParseDefaultTags()
    .Draw();

if (result.clicked && result.TryHit(out var hit) && hit.tag != 0)
{
    // Caller decides what the tag means.
}
```

Core should expose geometry and text positions:

- hit run
- hit line
- plain text index
- run-local text index
- tag/payload id
- rect for a text index

Core should not own caret state, editor navigation, text mutation, or caret
rendering. Editors can build those policies on top of hit indices and rects.

## Parser Rules

The parser should be forgiving enough for UI strings but predictable:

- unrecognized tags render as literal text unless registered
- malformed tags render as literal text
- escaped `<`, `>`, and `&` should be supported
- nested style tags compose
- spans remain ordered and non-overlapping after parsing
- parser output should preserve plain text indices exactly

## Open Questions

- Should links expose payload strings directly, or only stable integer ids into
  a result-owned payload table?
- Should parser instances be immutable so presets can be reused safely?
- Should inline boxes participate in text selection as object replacement
  characters, skipped ranges, or non-selectable holes?
