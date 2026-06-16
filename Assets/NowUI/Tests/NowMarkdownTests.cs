using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NowUI;
using NowUI.Internal;
using NowUI.Markdown;

public class NowMarkdownTests
{
    static NowMarkdownBlock Doc(string text)
    {
        return NowMarkdownParser.Parse(text);
    }

    static string PlainText(List<NowMarkdownInline> inlines)
    {
        var builder = new System.Text.StringBuilder();
        Append(inlines, builder);
        return builder.ToString();
    }

    static void Append(List<NowMarkdownInline> inlines, System.Text.StringBuilder builder)
    {
        foreach (var node in inlines)
        {
            if (node.text != null)
                builder.Append(node.text);

            if (node.children != null)
                Append(node.children, builder);

            if (node.type == NowMarkdownInlineType.SoftBreak)
                builder.Append(' ');
        }
    }

    static float ContrastRatio(Color a, Color b)
    {
        float la = RelativeLuminance(a);
        float lb = RelativeLuminance(b);

        if (la < lb)
        {
            float swap = la;
            la = lb;
            lb = swap;
        }

        return (la + 0.05f) / (lb + 0.05f);
    }

    static float RelativeLuminance(Color color)
    {
        return 0.2126f * SrgbToLinear(color.r) +
            0.7152f * SrgbToLinear(color.g) +
            0.0722f * SrgbToLinear(color.b);
    }

    static float SrgbToLinear(float value)
    {
        value = Mathf.Clamp01(value);
        return value <= 0.03928f
            ? value / 12.92f
            : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    [Test]
    public void AtxHeadingsParseLevels()
    {
        var doc = Doc("# One\n### Three\n###### Six\n####### Seven");

        Assert.AreEqual(NowMarkdownBlockType.Heading, doc.children[0].type);
        Assert.AreEqual(1, doc.children[0].level);
        Assert.AreEqual("One", PlainText(doc.children[0].inlines));
        Assert.AreEqual(3, doc.children[1].level);
        Assert.AreEqual(6, doc.children[2].level);
        Assert.AreEqual(NowMarkdownBlockType.Paragraph, doc.children[3].type, "seven hashes is not a heading");
    }

    [Test]
    public void ClosingHashesAreTrimmed()
    {
        var doc = Doc("## Title ##");
        Assert.AreEqual("Title", PlainText(doc.children[0].inlines));
    }

    [Test]
    public void SetextHeadingsParse()
    {
        var doc = Doc("Top\n===\nSecond\n---");

        Assert.AreEqual(NowMarkdownBlockType.Heading, doc.children[0].type);
        Assert.AreEqual(1, doc.children[0].level);
        Assert.AreEqual("Top", PlainText(doc.children[0].inlines));
        Assert.AreEqual(2, doc.children[1].level);
    }

    [Test]
    public void ParagraphsMergeLinesAndSplitOnBlank()
    {
        var doc = Doc("line one\nline two\n\nsecond para");

        Assert.AreEqual(2, doc.children.Count);
        Assert.AreEqual("line one line two", PlainText(doc.children[0].inlines));
        Assert.AreEqual("second para", PlainText(doc.children[1].inlines));
    }

    [Test]
    public void ThematicBreakVariantsParse()
    {
        var doc = Doc("***\n- - -\n___");

        Assert.AreEqual(3, doc.children.Count);
        Assert.AreEqual(NowMarkdownBlockType.ThematicBreak, doc.children[0].type);
        Assert.AreEqual(NowMarkdownBlockType.ThematicBreak, doc.children[1].type);
        Assert.AreEqual(NowMarkdownBlockType.ThematicBreak, doc.children[2].type);
    }

    [Test]
    public void FencedCodeBlockKeepsContentVerbatim()
    {
        var doc = Doc("```csharp\nint x = 1; // *not* emphasis\n\n    indented\n```\nafter");

        Assert.AreEqual(NowMarkdownBlockType.CodeBlock, doc.children[0].type);
        Assert.AreEqual("csharp", doc.children[0].info);
        Assert.AreEqual("int x = 1; // *not* emphasis\n\n    indented", doc.children[0].literal);
        Assert.AreEqual(NowMarkdownBlockType.Paragraph, doc.children[1].type);
    }

    [Test]
    public void UnclosedFenceRunsToEnd()
    {
        var doc = Doc("```\ncode");
        Assert.AreEqual(NowMarkdownBlockType.CodeBlock, doc.children[0].type);
        Assert.AreEqual("code", doc.children[0].literal);
    }

    [Test]
    public void BlockQuoteNestsContent()
    {
        var doc = Doc("> quoted *text*\n> second line\n\nplain");

        Assert.AreEqual(NowMarkdownBlockType.Quote, doc.children[0].type);
        Assert.AreEqual(NowMarkdownBlockType.Paragraph, doc.children[0].children[0].type);
        Assert.AreEqual(NowMarkdownBlockType.Paragraph, doc.children[1].type);
    }

    [Test]
    public void BulletListParsesItems()
    {
        var doc = Doc("- one\n- two\n- three");
        var list = doc.children[0];

        Assert.AreEqual(NowMarkdownBlockType.List, list.type);
        Assert.IsFalse(list.ordered);
        Assert.AreEqual(3, list.children.Count);
        Assert.AreEqual("two", PlainText(list.children[1].children[0].inlines));
    }

    [Test]
    public void OrderedListKeepsStartNumber()
    {
        var doc = Doc("3. three\n4. four");
        var list = doc.children[0];

        Assert.IsTrue(list.ordered);
        Assert.AreEqual(3, list.start);
        Assert.AreEqual(2, list.children.Count);
    }

    [Test]
    public void NestedListsAttachToParentItem()
    {
        var doc = Doc("- outer\n  - inner one\n  - inner two");
        var list = doc.children[0];

        Assert.AreEqual(1, list.children.Count);
        var item = list.children[0];
        Assert.AreEqual(NowMarkdownBlockType.Paragraph, item.children[0].type);
        Assert.AreEqual(NowMarkdownBlockType.List, item.children[1].type);
        Assert.AreEqual(2, item.children[1].children.Count);
    }

    [Test]
    public void TaskListItemsCarryState()
    {
        var doc = Doc("- [x] done\n- [ ] todo");
        var list = doc.children[0];

        Assert.IsTrue(list.children[0].isTask);
        Assert.IsTrue(list.children[0].isChecked);
        Assert.IsTrue(list.children[1].isTask);
        Assert.IsFalse(list.children[1].isChecked);
        Assert.AreEqual("done", PlainText(list.children[0].children[0].inlines));
    }

    [Test]
    public void TableParsesHeaderAlignmentAndRows()
    {
        var doc = Doc("| a | b | c |\n| :--- | :---: | ---: |\n| 1 | 2 | 3 |\n| x | y | z |");
        var table = doc.children[0];

        Assert.AreEqual(NowMarkdownBlockType.Table, table.type);
        Assert.AreEqual(3, table.tableRows.Count);
        Assert.AreEqual(NowMarkdownAlign.Left, table.tableAligns[0]);
        Assert.AreEqual(NowMarkdownAlign.Center, table.tableAligns[1]);
        Assert.AreEqual(NowMarkdownAlign.Right, table.tableAligns[2]);
        Assert.AreEqual("y", PlainText(table.tableRows[2][1]));
    }

    [Test]
    public void EscapedPipesStayInsideCells()
    {
        var doc = Doc("| a | b |\n| --- | --- |\n| x \\| y | z |");
        Assert.AreEqual("x | y", PlainText(doc.children[0].tableRows[1][0]));
    }

    [Test]
    public void EmphasisAndStrongNest()
    {
        var inlines = NowMarkdownInlineParser.Parse("plain *em* **strong** ***both***");

        Assert.AreEqual(NowMarkdownInlineType.Emphasis, inlines[1].type);
        Assert.AreEqual("em", PlainText(inlines[1].children));
        Assert.AreEqual(NowMarkdownInlineType.Strong, inlines[3].type);
        Assert.AreEqual("strong", PlainText(inlines[3].children));

        var both = inlines[5];
        Assert.IsTrue(both.type == NowMarkdownInlineType.Strong || both.type == NowMarkdownInlineType.Emphasis);
        Assert.AreEqual("both", PlainText(both.children));
    }

    [Test]
    public void IntrawordUnderscoresDoNotEmphasize()
    {
        var inlines = NowMarkdownInlineParser.Parse("snake_case_name");
        Assert.AreEqual("snake_case_name", PlainText(inlines));

        foreach (var node in inlines)
            Assert.AreEqual(NowMarkdownInlineType.Text, node.type);
    }

    [Test]
    public void StrikethroughRequiresDoubleTilde()
    {
        var inlines = NowMarkdownInlineParser.Parse("~~gone~~ and ~not~");

        Assert.AreEqual(NowMarkdownInlineType.Strikethrough, inlines[0].type);
        Assert.AreEqual("gone", PlainText(inlines[0].children));
        Assert.AreEqual("gone and ~not~", PlainText(inlines));
    }

    [Test]
    public void CodeSpansIgnoreEmphasisAndBalanceBackticks()
    {
        var inlines = NowMarkdownInlineParser.Parse("`a *b* c` and ``with ` tick``");

        Assert.AreEqual(NowMarkdownInlineType.Code, inlines[0].type);
        Assert.AreEqual("a *b* c", inlines[0].text);
        Assert.AreEqual(NowMarkdownInlineType.Code, inlines[2].type);
        Assert.AreEqual("with ` tick", inlines[2].text);
    }

    [Test]
    public void LinksParseTextAndDestination()
    {
        var inlines = NowMarkdownInlineParser.Parse("see [the *spec*](https://example.com/a(b)) now");

        Assert.AreEqual(NowMarkdownInlineType.Link, inlines[1].type);
        Assert.AreEqual("https://example.com/a(b)", inlines[1].url);
        Assert.AreEqual("the spec", PlainText(inlines[1].children));
        Assert.AreEqual(NowMarkdownInlineType.Emphasis, inlines[1].children[1].type);
    }

    [Test]
    public void ImagesParseAsImageNodes()
    {
        var inlines = NowMarkdownInlineParser.Parse("![alt text](https://example.com/img.png)");

        Assert.AreEqual(NowMarkdownInlineType.Image, inlines[0].type);
        Assert.AreEqual("https://example.com/img.png", inlines[0].url);
        Assert.AreEqual("alt text", PlainText(inlines[0].children));
    }

    [Test]
    public void CSharpTokenizerClassifiesKindsAndCarriesState()
    {
        var tokens = new List<NowMarkdownToken>();
        var state = default(NowMarkdownSyntaxState);

        NowMarkdownSyntax.TokenizeLine("var x = 42; // answer", NowMarkdownSyntax.Language.CSharp, ref state, tokens);

        Assert.AreEqual(NowMarkdownTokenKind.Keyword, tokens[0].kind, "var");
        bool sawNumber = false;

        foreach (var token in tokens)
            sawNumber |= token.kind == NowMarkdownTokenKind.Number;

        Assert.IsTrue(sawNumber, "42");
        Assert.AreEqual(NowMarkdownTokenKind.Comment, tokens[tokens.Count - 1].kind);

        NowMarkdownSyntax.TokenizeLine("string s = \"hi \\\" there\"; /* open", NowMarkdownSyntax.Language.CSharp, ref state, tokens);

        Assert.AreEqual(NowMarkdownTokenKind.Keyword, tokens[0].kind, "string");
        bool sawString = false;

        foreach (var token in tokens)
            sawString |= token.kind == NowMarkdownTokenKind.String;

        Assert.IsTrue(sawString);
        Assert.IsTrue(state.inBlockComment, "block comment must carry to the next line");

        NowMarkdownSyntax.TokenizeLine("still comment */ return 1;", NowMarkdownSyntax.Language.CSharp, ref state, tokens);

        Assert.AreEqual(NowMarkdownTokenKind.Comment, tokens[0].kind);
        Assert.IsFalse(state.inBlockComment);
        bool sawKeyword = false;

        foreach (var token in tokens)
            sawKeyword |= token.kind == NowMarkdownTokenKind.Keyword;

        Assert.IsTrue(sawKeyword, "return after the comment closes");
    }

    [Test]
    public void UnknownLanguageStaysPlain()
    {
        var tokens = new List<NowMarkdownToken>();
        var state = default(NowMarkdownSyntaxState);

        NowMarkdownSyntax.TokenizeLine("var if \"x\" 42", NowMarkdownSyntax.Language.None, ref state, tokens);

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(NowMarkdownTokenKind.Plain, tokens[0].kind);
    }

    [Test]
    public void JsonTokenizerColorsLiterals()
    {
        var tokens = new List<NowMarkdownToken>();
        var state = default(NowMarkdownSyntaxState);

        NowMarkdownSyntax.TokenizeLine("{ \"a\": true, \"b\": 1.5 }", NowMarkdownSyntax.Language.Json, ref state, tokens);

        bool sawKeyword = false, sawString = false, sawNumber = false;

        foreach (var token in tokens)
        {
            sawKeyword |= token.kind == NowMarkdownTokenKind.Keyword;
            sawString |= token.kind == NowMarkdownTokenKind.String;
            sawNumber |= token.kind == NowMarkdownTokenKind.Number;
        }

        Assert.IsTrue(sawKeyword, "true");
        Assert.IsTrue(sawString);
        Assert.IsTrue(sawNumber);
    }

    [Test]
    public void AutolinksTrimTrailingPunctuation()
    {
        var inlines = NowMarkdownInlineParser.Parse("go to https://example.com/x. Then www.test.org, ok?");

        Assert.AreEqual(NowMarkdownInlineType.Link, inlines[1].type);
        Assert.AreEqual("https://example.com/x", inlines[1].url);
        Assert.AreEqual(NowMarkdownInlineType.Link, inlines[3].type);
        Assert.AreEqual("http://www.test.org", inlines[3].url);
    }

    [Test]
    public void HardAndSoftBreaksParse()
    {
        var inlines = NowMarkdownInlineParser.Parse("one  \ntwo\nthree");

        Assert.AreEqual("one", inlines[0].text);
        Assert.AreEqual(NowMarkdownInlineType.HardBreak, inlines[1].type);
        Assert.AreEqual(NowMarkdownInlineType.SoftBreak, inlines[3].type);
    }

    [Test]
    public void BackslashEscapesPunctuation()
    {
        var inlines = NowMarkdownInlineParser.Parse(@"\*not em\* and \[not link\]");
        Assert.AreEqual("*not em* and [not link]", PlainText(inlines));
    }

    [Test]
    public void HtmlIsTreatedAsPlainText()
    {
        var doc = Doc("<div>hello</div>\n\n<script>alert(1)</script>");

        foreach (var block in doc.children)
            Assert.AreEqual(NowMarkdownBlockType.Paragraph, block.type, "raw HTML must stay inert text");
    }

    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowTextInputSource
    {
        public NowTextInputFrame frame;

        public bool TryGetFrame(out NowTextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    NowFontAsset _font;
    FakeProvider _provider;
    NowDrawList _drawList;
    static readonly Vector2 Surface = new Vector2(512, 512);

    [OneTimeSetUp]
    public void LoadFont()
    {
        _font = Resources.Load<NowFontAsset>("NowUI/NotoSans");
        Assert.NotNull(_font, "Default font resource missing.");
    }

    [SetUp]
    public void SetUp()
    {
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowMarkdown.Reset();
        _provider = new FakeProvider();
        _drawList = new NowDrawList();
        Now.defaultFont = _font;
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        Now.defaultFont = null;
        NowInput.Reset();
        NowFocus.Reset();
        NowControlState.Reset();
        NowControls.Reset();
        NowOverlay.Reset();
        NowContextMenu.Reset();
        NowMarkdown.Reset();
    }

    [Test]
    public void LoadedImagesLayoutAtAspectSizeAndFailedOnesFallBackToAlt()
    {
        var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false);

        try
        {
            NowMarkdownImages.SetTexture("https://example.com/pic.png", texture);

            var withImage = NowMarkdownDocument.Parse("![pic](https://example.com/pic.png)");
            var withBrokenImage = NowMarkdownDocument.Parse("![alt only](notaurl.png)");

            using (NowInput.Begin(_provider, Surface))
            {
                float imageHeight = withImage.MeasureHeight(400f);
                Assert.GreaterOrEqual(imageHeight, 32f, "native-size image plus line spacing");

                float clamped = withImage.MeasureHeight(32f);
                Assert.Less(clamped, imageHeight, "image scales down with the available width");

                float altHeight = withBrokenImage.MeasureHeight(400f);
                Assert.Greater(altHeight, 0f, "failed images render their alt text");
                Assert.Less(altHeight, 64f);

                using (_drawList.Begin(Surface))
                    withImage.Draw(new NowRect(0, 0, 400f, imageHeight));

                Assert.IsTrue(_drawList.hasGeometry, "loaded image must emit textured geometry");
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
            NowMarkdownImages.Reset();
        }
    }

    [Test]
    public void MultiWordLinksActAsOneLink()
    {
        var document = NowMarkdownDocument.Parse("[two words here](https://example.com/multi)");
        var rect = new NowRect(0, 0, 400f, 60f);
        var style = NowMarkdownStyle.Default;

        float firstWordWidth = _font.MeasureText("two", style.fontSize).x;
        float spaceWidth = _font.MeasureText(" ", style.fontSize).x;
        Vector2 overFirstWord = new Vector2(3f, 8f);
        Vector2 overSecondWord = new Vector2(firstWordWidth + spaceWidth + 3f, 8f);

        _provider.snapshot = new NowInputSnapshot(overSecondWord, false, false, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            var hover = document.Draw(rect);
            Assert.AreEqual("https://example.com/multi", hover.hoveredLink,
                "hovering any word of a multi-word link must report the link");
        }

        _provider.snapshot = new NowInputSnapshot(overFirstWord, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            document.Draw(rect);

        _provider.snapshot = new NowInputSnapshot(overSecondWord, false, false, true);
        string clicked;

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            clicked = document.Draw(rect).clickedLink;

        Assert.AreEqual("https://example.com/multi", clicked,
            "press on one word and release on another must still click the link");
    }

    [Test]
    public void CodeBlockTextIsSelectableAndCopyable()
    {
        var previousCopy = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;

        try
        {
            var document = NowMarkdownDocument.Parse("```\nint value = 42;\n```");
            var rect = new NowRect(0, 0, 400f, 200f);
            float pad = NowMarkdownStyle.Default.fontSize * 0.6f;
            float codeSize = NowMarkdownStyle.Default.fontSize * 0.92f;
            float lineMidY = pad + 8f;
            var from = new Vector2(pad + 1f, lineMidY);
            var to = new Vector2(pad + _font.MeasureText("int", codeSize).x + 1f, lineMidY);

            void Frame(Vector2 pointer, bool down, bool pressed, bool released, NowTextInputFrame keys = default)
            {
                keyboard.frame = keys;
                NowTextInput.Invalidate();
                _provider.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                    document.Draw(rect);
            }

            Frame(from, down: true, pressed: true, released: false);
            Frame(to, down: true, pressed: false, released: false);
            Frame(to, down: false, pressed: false, released: true);
            Frame(to, down: false, pressed: false, released: false, new NowTextInputFrame { copyPressed = true });

            Assert.AreEqual("int", copied, "dragging over code and pressing Ctrl+C must copy the selected range");
        }
        finally
        {
            NowClipboard.setText = previousCopy;
            NowTextInput.Reset();
        }
    }

    [Test]
    public void ParagraphTextIsSelectableAcrossStyledWords()
    {
        var previousCopy = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;

        try
        {
            var document = NowMarkdownDocument.Parse("plain **bold** words");
            var rect = new NowRect(0, 0, 400f, 100f);

            void Frame(Vector2 pointer, bool down, bool pressed, bool released, NowTextInputFrame keys = default)
            {
                keyboard.frame = keys;
                NowTextInput.Invalidate();
                _provider.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                    document.Draw(rect);
            }

            var lineMid = new Vector2(1f, 9f);
            var lineEnd = new Vector2(390f, 9f);

            Frame(lineMid, down: true, pressed: true, released: false);
            Frame(lineEnd, down: true, pressed: false, released: false);
            Frame(lineEnd, down: false, pressed: false, released: true);
            Frame(lineEnd, down: false, pressed: false, released: false, new NowTextInputFrame { copyPressed = true });

            Assert.AreEqual("plain bold words", copied,
                "selecting a styled paragraph must copy its plain text with spaces");
        }
        finally
        {
            NowClipboard.setText = previousCopy;
            NowTextInput.Reset();
        }
    }

    [Test]
    public void RightClickContextMenuCopiesTheSelection()
    {
        var previousCopy = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;

        try
        {
            var document = NowMarkdownDocument.Parse("```\nint value = 42;\n```");
            var rect = new NowRect(0, 0, 400f, 200f);
            float pad = NowMarkdownStyle.Default.fontSize * 0.6f;
            float codeSize = NowMarkdownStyle.Default.fontSize * 0.92f;
            float lineMidY = pad + 8f;
            var from = new Vector2(pad + 1f, lineMidY);
            var to = new Vector2(pad + _font.MeasureText("int", codeSize).x + 1f, lineMidY);

            void Frame(NowInputSnapshot snapshot)
            {
                keyboard.frame = default;
                NowTextInput.Invalidate();
                _provider.snapshot = snapshot;

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                {
                    document.Draw(rect);
                    NowOverlay.Flush();
                }
            }

            Frame(new NowInputSnapshot(from, true, true, false));
            Frame(new NowInputSnapshot(to, true, false, false));
            Frame(new NowInputSnapshot(to, false, false, true));

            Frame(new NowInputSnapshot(to, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None));
            Assert.IsTrue(NowContextMenu.isOpen, "right-clicking the code block must open the context menu");

            var copyItem = new Vector2(to.x + 30f, to.y + 4f + 13f);
            Frame(new NowInputSnapshot(copyItem, true, true, false));
            Frame(new NowInputSnapshot(copyItem, false, false, true));
            Frame(new NowInputSnapshot(copyItem, false, false, false));

            Assert.AreEqual("int", copied, "the Copy item must copy the selected range");
            Assert.IsFalse(NowContextMenu.isOpen, "choosing an item closes the menu");
        }
        finally
        {
            NowClipboard.setText = previousCopy;
            NowTextInput.Reset();
        }
    }

    [Test]
    public void ImageContextMenuCopiesTheAddress()
    {
        var previousCopy = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;
        var injected = new Texture2D(200, 80, TextureFormat.RGBA32, false);

        try
        {
            NowMarkdownImages.SetTexture("https://example.com/photo.png", injected);
            var document = NowMarkdownDocument.Parse("![photo](https://example.com/photo.png)");
            var rect = new NowRect(0, 0, 400f, 200f);

            void Frame(NowInputSnapshot snapshot)
            {
                _provider.snapshot = snapshot;

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                {
                    document.Draw(rect);
                    NowOverlay.Flush();
                }
            }

            var onImage = new Vector2(50f, 40f);
            Frame(new NowInputSnapshot(onImage, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None));
            Assert.IsTrue(NowContextMenu.isOpen, "right-clicking an image must open its menu");

            var addressItem = new Vector2(onImage.x + 30f, onImage.y + 4f + 13f);
            Frame(new NowInputSnapshot(addressItem, true, true, false));
            Frame(new NowInputSnapshot(addressItem, false, false, true));
            Frame(new NowInputSnapshot(addressItem, false, false, false));

            Assert.AreEqual("https://example.com/photo.png", copied, "Copy image address must copy the URL");
        }
        finally
        {
            NowClipboard.setText = previousCopy;
            Object.DestroyImmediate(injected);
            NowMarkdownImages.Reset();
        }
    }

    [Test]
    public void DragAcrossBlocksSelectsLikeAWebpage()
    {
        var previousCopy = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;

        try
        {
            var document = NowMarkdownDocument.Parse("first words\n\nsecond words");
            var rect = new NowRect(0, 0, 400f, 200f);

            void Frame(Vector2 pointer, bool down, bool pressed, bool released, NowTextInputFrame keys = default)
            {
                keyboard.frame = keys;
                NowTextInput.Invalidate();
                _provider.snapshot = new NowInputSnapshot(pointer, down, pressed, released);

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                    document.Draw(rect);
            }

            var inFirst = new Vector2(1f, 8f);
            var inSecond = new Vector2(390f, 38f);

            Frame(inFirst, down: true, pressed: true, released: false);
            Frame(inSecond, down: true, pressed: false, released: false);
            Frame(inSecond, down: false, pressed: false, released: true);
            Frame(inSecond, down: false, pressed: false, released: false, new NowTextInputFrame { copyPressed = true });

            Assert.AreEqual("first words\n\nsecond words", copied,
                "dragging across blocks must select and copy everything in between");
        }
        finally
        {
            NowClipboard.setText = previousCopy;
            NowTextInput.Reset();
        }
    }

    [Test]
    public void OpenContextMenuBlocksHoverBeneathAndScrollDismisses()
    {
        var keyboard = new FakeKeyboard();
        NowTextInput.source = keyboard;

        try
        {
            var document = NowMarkdownDocument.Parse("[link text](https://example.com/x)\n\n```\ncode\n```");
            var rect = new NowRect(0, 0, 400f, 300f);
            NowMarkdownResult last = default;

            void Frame(NowInputSnapshot snapshot, bool forceOverlayFrame = false)
            {
                keyboard.frame = default;
                NowTextInput.Invalidate();
                _provider.snapshot = snapshot;

                using (NowInput.Begin(_provider, Surface))
                using (_drawList.Begin(Surface))
                {
                    if (forceOverlayFrame)
                        NowOverlay.ForceNewFrame();

                    last = document.Draw(rect);
                    NowOverlay.Flush();
                }
            }

            var overLink = new Vector2(5f, 9f);
            Frame(new NowInputSnapshot(overLink, false, false, false));
            Assert.AreEqual("https://example.com/x", last.hoveredLink, "sanity: the link hovers before the menu opens");

            var overCode = new Vector2(12f, 42f);
            Frame(new NowInputSnapshot(overCode, NowPointerButtons.Secondary, NowPointerButtons.Secondary, NowPointerButtons.None));
            Assert.IsTrue(NowContextMenu.isOpen, "right-clicking the code block must open the menu");

            Frame(new NowInputSnapshot(overLink, false, false, false), forceOverlayFrame: true);
            Assert.IsNull(last.hoveredLink, "an open context menu must block hover beneath it");
            Assert.IsTrue(NowContextMenu.isOpen, "hovering elsewhere must not dismiss the menu");

            Frame(new NowInputSnapshot(true, overLink, overLink, Vector2.zero, false, false, false, new Vector2(0f, 1f), 1, 1f));
            Assert.IsFalse(NowContextMenu.isOpen, "scrolling must dismiss the menu");

            Frame(new NowInputSnapshot(overLink, false, false, false), forceOverlayFrame: true);
            Frame(new NowInputSnapshot(overLink, false, false, false), forceOverlayFrame: true);
            Assert.AreEqual("https://example.com/x", last.hoveredLink,
                "closing the menu must release the pointer block");
        }
        finally
        {
            NowTextInput.Reset();
        }
    }

    [Test]
    public void CopyButtonCopiesTheCodeBlock()
    {
        var previous = NowClipboard.setText;
        string copied = null;
        NowClipboard.setText = text => copied = text;

        try
        {
            var document = NowMarkdownDocument.Parse("```\nint x = 1;\n```");
            var rect = new NowRect(0, 0, 400f, 200f);
            float buttonSize = NowMarkdownStyle.Default.fontSize;
            Vector2 inside = new Vector2(400f - 6f - buttonSize * 1.8f, 6f + buttonSize * 0.75f);

            _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
                document.Draw(rect);

            _provider.snapshot = new NowInputSnapshot(inside, false, false, true);

            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
                document.Draw(rect);

            Assert.AreEqual("int x = 1;", copied);
        }
        finally
        {
            NowClipboard.setText = previous;
        }
    }

    [Test]
    public void ImagesInsideLinksReportClicks()
    {
        var texture = new Texture2D(48, 24, TextureFormat.RGBA32, false);

        try
        {
            NowMarkdownImages.SetTexture("https://example.com/badge.png", texture);

            var document = NowMarkdownDocument.Parse("[![badge](https://example.com/badge.png)](https://example.com/dest)");
            var rect = new NowRect(0, 0, 400f, 100f);
            Vector2 inside = new Vector2(10f, 10f);
            string clicked = null;

            _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
                document.Draw(rect);

            _provider.snapshot = new NowInputSnapshot(inside, false, false, true);

            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
                clicked = document.Draw(rect).clickedLink;

            Assert.AreEqual("https://example.com/dest", clicked);
        }
        finally
        {
            Object.DestroyImmediate(texture);
            NowMarkdownImages.Reset();
        }
    }

    [Test]
    public void DocumentMeasuresAndRendersGeometry()
    {
        var document = NowMarkdownDocument.Parse("# Title\n\nBody with **bold** and `code`.\n\n- item one\n- item two");

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            float height = document.MeasureHeight(400f);
            Assert.Greater(height, 0f);

            var result = document.Draw(new NowRect(0, 0, 400f, height));
            Assert.AreEqual(height, result.height, 0.01f);
        }

        Assert.IsTrue(_drawList.hasGeometry, "Markdown drew no geometry.");
    }

    [Test]
    public void CodeSyntaxColorsRemainReadableInMaterialDarkTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<NowThemeAsset>("Assets/NowUI/Assets/Themes/MaterialDark.asset");
        Assert.NotNull(theme);

        var document = NowMarkdownDocument.Parse(
            "```csharp\nstring name = \"value\";\nint count = 42;\n// muted comment\n```");
        Color panel = theme.GetColor(NowColorToken.SurfaceMuted, Color.gray);
        var colors = new List<Vector4>();

        using (NowTheme.Scope(theme))
        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            document.Draw(new NowRect(0, 0, 400f, 200f));

        _drawList.mesh.GetUVs(3, colors);

        for (int i = 0; i < _drawList.batches.Count; ++i)
        {
            if (_drawList.batches[i].kind != NowMeshKind.Text)
                continue;

            var indices = _drawList.mesh.GetIndices(i);

            for (int j = 0; j < indices.Length; ++j)
            {
                Color color = colors[indices[j]];

                if (color.a <= 0f)
                    continue;

                Assert.GreaterOrEqual(ContrastRatio(color, panel), 4.5f,
                    $"Markdown code text color {color} is not readable over Material Dark code panel {panel}.");
            }
        }
    }

    [Test]
    public void WrappingIncreasesHeightAtNarrowWidths()
    {
        var document = NowMarkdownDocument.Parse(
            "a long paragraph with quite a few words that will definitely need wrapping");

        using (NowInput.Begin(_provider, Surface))
        {
            float wide = document.MeasureHeight(500f);
            float narrow = document.MeasureHeight(120f);
            Assert.Greater(narrow, wide);
        }
    }

    [Test]
    public void ClickingALinkReportsItsDestination()
    {
        var document = NowMarkdownDocument.Parse("[click me](https://example.com/target)");
        var rect = new NowRect(0, 0, 400f, 60f);
        Vector2 inside = new Vector2(8f, 8f);
        string clicked = null;

        _provider.snapshot = new NowInputSnapshot(inside, true, true, false);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            document.Draw(rect);

        _provider.snapshot = new NowInputSnapshot(inside, false, false, true);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
            clicked = document.Draw(rect).clickedLink;

        Assert.AreEqual("https://example.com/target", clicked);
    }

    [Test]
    public void CachedDrawIsAllocationFreeAfterWarmup()
    {
        var document = NowMarkdownDocument.Parse("steady *state* drawing with [a link](https://x.y)");
        var rect = new NowRect(0, 0, 300f, 100f);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            document.Draw(rect);
            document.Draw(rect);

            long before;

            try
            {
                before = System.GC.GetAllocatedBytesForCurrentThread();
            }
            catch (System.NotImplementedException)
            {
                Assert.Ignore("Per-thread allocation tracking unavailable on this runtime.");
                return;
            }

            document.Draw(rect);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "steady-state markdown draw must not allocate");
        }
    }
}
