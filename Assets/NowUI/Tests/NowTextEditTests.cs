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
}
