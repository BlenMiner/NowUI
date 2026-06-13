using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NowUI;
using NowUI.CodeEditor;

/// <summary>
/// Code editor extension tests: the JSON tokenizer and validator, the
/// markdown profile with embedded fences, and the editor's IDE behaviors
/// (auto-pairs, auto-indent, tab, undo) driven through fake input sources.
/// </summary>
public class NowCodeEditorTests
{
    sealed class FakePointer : INowUIInputProvider
    {
        public NowUIInputSnapshot snapshot;

        public bool TryGetSnapshot(NowUIInputSurface surface, out NowUIInputSnapshot result)
        {
            result = snapshot;
            return true;
        }
    }

    sealed class FakeKeyboard : INowUITextInputSource
    {
        public NowUITextInputFrame frame;

        public bool TryGetFrame(out NowUITextInputFrame result)
        {
            result = frame;
            return true;
        }
    }

    static readonly Vector2 Surface = new Vector2(640, 480);
    static readonly NowRect EditorRect = new NowRect(20, 20, 400, 300);

    FakePointer _pointer;
    FakeKeyboard _keyboard;
    NowUIDrawList _drawList;

    [SetUp]
    public void SetUp()
    {
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
        NowUIOverlay.Reset();
        NowUITextInput.Reset();
        NowCodeEditor.ResetCaches();

        _pointer = new FakePointer();
        _keyboard = new FakeKeyboard();
        NowUITextInput.source = _keyboard;
        _drawList = new NowUIDrawList();
    }

    [TearDown]
    public void TearDown()
    {
        _drawList.Dispose();
        NowCodeEditor.ResetCaches();
        NowUITextInput.Reset();
        NowUIOverlay.Reset();
        NowUIInput.Reset();
        NowUIFocus.Reset();
        NowUIControlState.Reset();
        NowControls.Reset();
    }

    NowCodeEditorResult Frame(ref string text, NowUITextInputFrame keys = default)
    {
        _keyboard.frame = keys;
        NowUITextInput.Invalidate();
        NowCodeEditorResult result;

        using (NowUIInput.Begin(_pointer, Surface))
        using (_drawList.Begin(Surface))
            result = NowCode.Editor(EditorRect, NowJsonLanguage.instance, "code").Draw(ref text);

        return result;
    }

    static int Id => NowUIInput.GetId(0, "code");

    void Focus()
    {
        NowUIFocus.Focus(Id);
    }

    ref NowTextEditState State()
    {
        return ref NowUIControlState.Get<NowTextEditState>(Id);
    }

    static List<NowCodeToken> Tokenize(NowCodeLanguage language, string line, int state = 0)
    {
        var tokens = new List<NowCodeToken>();
        language.TokenizeLine(line, 0, line.Length, state, tokens);
        return tokens;
    }

    static List<NowCodeDiagnostic> Validate(string text)
    {
        var diagnostics = new List<NowCodeDiagnostic>();
        NowJsonLanguage.instance.Validate(text, diagnostics);
        return diagnostics;
    }

    [Test]
    public void JsonTokenizerClassifiesPropertiesStringsNumbersAndKeywords()
    {
        var tokens = Tokenize(NowJsonLanguage.instance, "{ \"name\": \"now\", \"count\": 3, \"on\": true }");

        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Property && t.start == 2),
            "\"name\" must classify as a property.");
        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.String && t.start == 10),
            "\"now\" must classify as a value string.");
        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Number));
        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Keyword));
        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Punctuation));
    }

    [Test]
    public void JsonValidatorAcceptsValidDocuments()
    {
        Assert.IsEmpty(Validate("{ \"a\": [1, 2.5, -3e2], \"b\": { \"c\": null }, \"d\": \"x\\nq\" }"));
        Assert.IsEmpty(Validate("[]"));
        Assert.IsEmpty(Validate("  \n  "), "Whitespace-only text stays quiet.");
    }

    [Test]
    public void JsonValidatorPositionsMissingCommaErrors()
    {
        var diagnostics = Validate("{ \"a\": 1 \"b\": 2 }");

        Assert.AreEqual(1, diagnostics.Count);
        Assert.AreEqual(9, diagnostics[0].start, "The error points at the unexpected quote.");
        StringAssert.Contains("','", diagnostics[0].message);
    }

    [Test]
    public void JsonValidatorFlagsTrailingCommasUnterminatedStringsAndComments()
    {
        StringAssert.Contains("Trailing", Validate("[1, 2,]")[0].message);
        StringAssert.Contains("Unterminated", Validate("\"abc")[0].message);
        StringAssert.Contains("Comments", Validate("// hi\n{}")[0].message);
        StringAssert.Contains("after the JSON value", Validate("{} {}")[0].message);
    }

    [Test]
    public void MarkdownTokenizerHighlightsStructure()
    {
        Assert.IsTrue(Tokenize(NowMarkdownCodeLanguage.instance, "# Title").Exists(t => t.kind == NowCodeTokenKind.Heading));
        Assert.IsTrue(Tokenize(NowMarkdownCodeLanguage.instance, "- item").Exists(t => t.kind == NowCodeTokenKind.ListMarker));
        Assert.IsTrue(Tokenize(NowMarkdownCodeLanguage.instance, "a `code` span").Exists(t => t.kind == NowCodeTokenKind.CodeSpan));
        Assert.IsTrue(Tokenize(NowMarkdownCodeLanguage.instance, "a **bold** word").Exists(t => t.kind == NowCodeTokenKind.Strong));
        Assert.IsTrue(Tokenize(NowMarkdownCodeLanguage.instance, "[t](http://x)").Exists(t => t.kind == NowCodeTokenKind.Link));
    }

    [Test]
    public void MarkdownFencesDelegateToTheEmbeddedLanguage()
    {
        var markdown = NowMarkdownCodeLanguage.instance;
        var tokens = new List<NowCodeToken>();

        int state = markdown.TokenizeLine("```json", 0, 7, 0, tokens);
        Assert.AreNotEqual(0, state, "An open fence must carry state to the next line.");

        tokens.Clear();
        const string Line = "{ \"a\": true }";
        int inner = markdown.TokenizeLine(Line, 0, Line.Length, state, tokens);

        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Property), "Fence content must highlight as JSON.");
        Assert.IsTrue(tokens.Exists(t => t.kind == NowCodeTokenKind.Keyword));

        tokens.Clear();
        int closed = markdown.TokenizeLine("```", 0, 3, inner, tokens);
        Assert.AreEqual(0, closed, "The closing fence returns to markdown.");
    }

    [Test]
    public void MarkdownValidatorWarnsOnUnclosedFence()
    {
        var diagnostics = new List<NowCodeDiagnostic>();
        NowMarkdownCodeLanguage.instance.Validate("text\n```json\n{}", diagnostics);

        Assert.AreEqual(1, diagnostics.Count);
        StringAssert.Contains("Unclosed", diagnostics[0].message);
        Assert.AreEqual(5, diagnostics[0].start, "The warning points at the opening fence line.");

        diagnostics.Clear();
        NowMarkdownCodeLanguage.instance.Validate("```json\n{}\n```", diagnostics);
        Assert.IsEmpty(diagnostics);
    }

    [Test]
    public void TypingOpenBraceAutoClosesWithCaretBetween()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text, new NowUITextInputFrame { characters = "{" });

        Assert.AreEqual("{}", text);
        Assert.AreEqual(1, State().caret, "The caret sits between the pair.");
    }

    [Test]
    public void TypingClosingBraceSkipsOverTheAutoClosedOne()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text, new NowUITextInputFrame { characters = "{" });
        Frame(ref text, new NowUITextInputFrame { characters = "}" });

        Assert.AreEqual("{}", text, "No duplicate closer.");
        Assert.AreEqual(2, State().caret);
    }

    [Test]
    public void BackspaceInsideEmptyPairDeletesBoth()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text, new NowUITextInputFrame { characters = "[" });
        Frame(ref text, new NowUITextInputFrame { backspaceHeld = true });

        Assert.AreEqual(string.Empty, text);
    }

    [Test]
    public void EnterBetweenBracesExpandsWithIndent()
    {
        string text = string.Empty;
        Focus();

        Frame(ref text, new NowUITextInputFrame { characters = "{" });
        Frame(ref text, new NowUITextInputFrame { enterPressed = true });

        Assert.AreEqual("{\n  \n}", text);
        Assert.AreEqual(4, State().caret, "The caret sits on the indented middle line.");
    }

    [Test]
    public void QuoteWrapsTheSelection()
    {
        string text = "hello";
        Focus();

        Frame(ref text, new NowUITextInputFrame { selectAllPressed = true, command = true });
        Frame(ref text, new NowUITextInputFrame { characters = "\"" });

        Assert.AreEqual("\"hello\"", text, "Typing a quote around a selection wraps it.");
    }

    [Test]
    public void TabIndentsAndShiftTabDedents()
    {
        string text = "a\nb";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowUITextInputFrame { selectAllPressed = true, command = true });
        Frame(ref text, new NowUITextInputFrame { tabPressed = true });
        Assert.AreEqual("  a\n  b", text, "Tab with a multi-line selection indents the lines.");

        Frame(ref text, new NowUITextInputFrame { tabPressed = true, shift = true });
        Assert.AreEqual("a\nb", text, "Shift+Tab dedents them back.");
    }

    [Test]
    public void UndoAndRedoRoundTrip()
    {
        string text = "{}";
        Focus();

        Frame(ref text);
        Frame(ref text, new NowUITextInputFrame { enterPressed = true });
        Assert.AreNotEqual("{}", text);

        Frame(ref text, new NowUITextInputFrame { undoPressed = true, command = true });
        Assert.AreEqual("{}", text, "Undo restores the previous text.");

        Frame(ref text, new NowUITextInputFrame { redoPressed = true, command = true });
        Assert.AreNotEqual("{}", text, "Redo reapplies the edit.");
    }

    [Test]
    public void ResultReportsValidity()
    {
        string text = "{ \"a\": 1 }";
        var result = Frame(ref text);

        Assert.IsTrue(result.isValid);
        Assert.AreEqual(0, result.diagnosticCount);

        text = "{ \"a\": }";
        result = Frame(ref text);

        Assert.IsFalse(result.isValid);
        Assert.Greater(result.diagnosticCount, 0);
    }

    [Test]
    public void TypingUnfocusedChangesNothing()
    {
        string text = "{}";

        var result = Frame(ref text, new NowUITextInputFrame { characters = "x", tabPressed = true });

        Assert.IsFalse(result.changed);
        Assert.AreEqual("{}", text);
    }
}
