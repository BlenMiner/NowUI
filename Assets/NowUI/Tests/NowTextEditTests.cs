using NUnit.Framework;
using NowUI;

/// <summary>Headless tests for the text editing engine — no rendering, no input.</summary>
public class NowTextEditTests
{
    [Test]
    public void InsertAppendsAtCaret()
    {
        string text = "Helo";
        var state = new NowTextEditState { caret = 3, anchor = 3 };

        Assert.IsTrue(NowTextEdit.Insert(ref text, ref state, "l"));
        Assert.AreEqual("Hello", text);
        Assert.AreEqual(4, state.caret);
        Assert.IsFalse(state.hasSelection);
    }

    [Test]
    public void InsertReplacesSelection()
    {
        string text = "Hello world";
        var state = new NowTextEditState { anchor = 6, caret = 11 };

        NowTextEdit.Insert(ref text, ref state, "NowUI");
        Assert.AreEqual("Hello NowUI", text);
        Assert.AreEqual(11, state.caret);
    }

    [Test]
    public void BackspaceRemovesPreviousCodepoint()
    {
        string text = "ab";
        var state = new NowTextEditState { caret = 2, anchor = 2 };

        Assert.IsTrue(NowTextEdit.Backspace(ref text, ref state));
        Assert.AreEqual("a", text);
        Assert.AreEqual(1, state.caret);

        var atStart = new NowTextEditState();
        Assert.IsFalse(NowTextEdit.Backspace(ref text, ref atStart), "Backspace at start is a no-op.");
        Assert.AreEqual("a", text);
    }

    [Test]
    public void SurrogatePairsNeverSplit()
    {
        string text = "a\U0001F600b";
        var state = new NowTextEditState { caret = 3, anchor = 3 };

        Assert.IsTrue(NowTextEdit.Backspace(ref text, ref state));
        Assert.AreEqual("ab", text, "Backspace must remove the whole surrogate pair.");
        Assert.AreEqual(1, state.caret);

        text = "a\U0001F600b";
        state = new NowTextEditState { caret = 1, anchor = 1 };
        NowTextEdit.MoveCaret(ref state, text, 1, select: false);
        Assert.AreEqual(3, state.caret, "Caret must jump over the surrogate pair.");
    }

    [Test]
    public void DeleteRemovesForward()
    {
        string text = "abc";
        var state = new NowTextEditState { caret = 1, anchor = 1 };

        Assert.IsTrue(NowTextEdit.Delete(ref text, ref state));
        Assert.AreEqual("ac", text);
        Assert.AreEqual(1, state.caret);
    }

    [Test]
    public void SelectionDeleteCollapsesToMin()
    {
        string text = "abcdef";
        var state = new NowTextEditState { anchor = 4, caret = 2 };

        Assert.IsTrue(NowTextEdit.Backspace(ref text, ref state));
        Assert.AreEqual("abef", text);
        Assert.AreEqual(2, state.caret);
        Assert.AreEqual(2, state.anchor);
    }

    [Test]
    public void MoveCollapsesSelectionToEdge()
    {
        var state = new NowTextEditState { anchor = 2, caret = 5 };

        NowTextEdit.MoveCaret(ref state, "abcdefgh", -1, select: false);
        Assert.AreEqual(2, state.caret, "Left collapses to selection start without moving.");

        state = new NowTextEditState { anchor = 2, caret = 5 };
        NowTextEdit.MoveCaret(ref state, "abcdefgh", 1, select: false);
        Assert.AreEqual(5, state.caret, "Right collapses to selection end without moving.");
    }

    [Test]
    public void ShiftMoveExtendsSelection()
    {
        var state = new NowTextEditState { anchor = 2, caret = 2 };

        NowTextEdit.MoveCaret(ref state, "abcdef", 1, select: true);
        NowTextEdit.MoveCaret(ref state, "abcdef", 1, select: true);

        Assert.AreEqual(2, state.anchor);
        Assert.AreEqual(4, state.caret);
        Assert.AreEqual("cd", NowTextEdit.GetSelection("abcdef", state));
    }

    [Test]
    public void WordMovementSkipsWords()
    {
        const string Text = "one two  three";
        var state = new NowTextEditState();

        NowTextEdit.MoveCaret(ref state, Text, 1, select: false, word: true);
        Assert.AreEqual(3, state.caret);

        NowTextEdit.MoveCaret(ref state, Text, 1, select: false, word: true);
        Assert.AreEqual(7, state.caret);

        state.caret = Text.Length;
        state.anchor = Text.Length;
        NowTextEdit.MoveCaret(ref state, Text, -1, select: false, word: true);
        Assert.AreEqual(9, state.caret);
    }

    [Test]
    public void SelectWordExcludesQuotesAndPunctuation()
    {
        const string Text = "\"hello\", next";
        var state = default(NowTextEditState);

        NowTextEdit.SelectWord(ref state, Text, 3);
        Assert.AreEqual("hello", NowTextEdit.GetSelection(Text, state));

        NowTextEdit.SelectWord(ref state, Text, 6);
        Assert.AreEqual("hello", NowTextEdit.GetSelection(Text, state),
            "A hit on the trailing edge of the word must not pull in the quote or comma.");
    }

    [Test]
    public void HomeEndAndSelectAll()
    {
        const string Text = "hello";
        var state = new NowTextEditState { caret = 3, anchor = 3 };

        NowTextEdit.MoveEnd(ref state, Text, select: false);
        Assert.AreEqual(5, state.caret);

        NowTextEdit.MoveHome(ref state, select: true);
        Assert.AreEqual(0, state.caret);
        Assert.AreEqual(5, state.anchor);
        Assert.AreEqual("hello", NowTextEdit.GetSelection(Text, state));

        NowTextEdit.SelectAll(ref state, Text);
        Assert.AreEqual(0, state.anchor);
        Assert.AreEqual(5, state.caret);
    }

    [Test]
    public void ClampRepairsStaleState()
    {
        var state = new NowTextEditState { caret = 99, anchor = -5 };

        NowTextEdit.Clamp(ref state, "abc");
        Assert.AreEqual(3, state.caret);
        Assert.AreEqual(0, state.anchor);
    }

    [Test]
    public void DeleteToLineStartRemovesBackToTheHardLineStart()
    {
        string text = "one\ntwo three";
        var state = new NowTextEditState { caret = 8, anchor = 8 };

        Assert.IsTrue(NowTextEdit.DeleteToLineStart(ref text, ref state));
        Assert.AreEqual("one\nthree", text, "Deletion stops at the newline.");
        Assert.AreEqual(4, state.caret);

        Assert.IsFalse(NowTextEdit.DeleteToLineStart(ref text, ref state),
            "At a line start there is nothing to delete.");
        Assert.AreEqual("one\nthree", text);
    }

    [Test]
    public void DeleteToLineStartPrefersTheSelection()
    {
        string text = "abcdef";
        var state = new NowTextEditState { anchor = 2, caret = 4 };

        Assert.IsTrue(NowTextEdit.DeleteToLineStart(ref text, ref state));
        Assert.AreEqual("abef", text, "An existing selection is deleted instead.");
        Assert.AreEqual(2, state.caret);
    }

    [Test]
    public void ModifierMappingFollowsThePlatformConvention()
    {
        bool previous = NowTextInput.isMacPlatform;

        try
        {
            var frame = new NowTextInputFrame { command = true };

            NowTextInput.isMacPlatform = false;
            Assert.IsTrue(frame.wordModifier, "Ctrl drives word movement on Windows/Linux.");
            Assert.IsFalse(frame.lineModifier, "There is no line modifier outside macOS.");

            NowTextInput.isMacPlatform = true;
            Assert.IsFalse(frame.wordModifier, "Command does not word-jump on macOS.");
            Assert.IsTrue(frame.lineModifier, "Command drives line movement on macOS.");

            frame = new NowTextInputFrame { option = true };
            Assert.IsTrue(frame.wordModifier, "Option drives word movement on macOS.");
            Assert.IsFalse(frame.lineModifier);

            NowTextInput.isMacPlatform = false;
            Assert.IsFalse(frame.wordModifier, "Alt alone does not word-jump on Windows/Linux.");
        }
        finally
        {
            NowTextInput.isMacPlatform = previous;
        }
    }
}
