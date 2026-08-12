using System;
using NUnit.Framework;
using UnityEngine;
using NowUI;

public class NowFontStackTests
{
    NowFontAsset _savedDefaultFont;

    NowFont _baseFont;

    NowFont _contextFont;

    [SetUp]
    public void SetUp()
    {
        NowLayout.Reset();
        _savedDefaultFont = Now.defaultFont;
        _baseFont = ScriptableObject.CreateInstance<NowFont>();
        _contextFont = ScriptableObject.CreateInstance<NowFont>();
        Now.defaultFont = _baseFont;
    }

    [TearDown]
    public void TearDown()
    {
        Now.defaultFont = _savedDefaultFont;
        UnityEngine.Object.DestroyImmediate(_baseFont);
        UnityEngine.Object.DestroyImmediate(_contextFont);
    }

    [Test]
    public void ActiveFontFallsBackToDefault()
    {
        Assert.AreSame(_baseFont, Now.font);
    }

    [Test]
    public void FontScopesNestAndRestore()
    {
        using (Now.Font(_contextFont))
        {
            Assert.AreSame(_contextFont, Now.font);

            using (Now.Font(_baseFont))
                Assert.AreSame(_baseFont, Now.font);

            Assert.AreSame(_contextFont, Now.font);
        }

        Assert.AreSame(_baseFont, Now.font);
    }

    [Test]
    public void LabelStyleUsesActiveFont()
    {
        using (Now.Font(_contextFont))
            Assert.AreSame(_contextFont, NowLayout.labelStyle.font);

        Assert.AreSame(_baseFont, NowLayout.labelStyle.font);
    }

    [Test]
    public void TextBuilderUsesActiveFont()
    {
        using (Now.Font(_contextFont))
            Assert.AreSame(_contextFont, Now.Text(default(NowRect)).font);

        Assert.AreSame(_baseFont, Now.Text(default(NowRect)).font);
    }

    [Test]
    public void PushingNullFontThrows()
    {
        Assert.Throws<ArgumentNullException>(() => Now.Font(null));
    }

    [Test]
    public void DisposeIsIdempotent()
    {
        var scope = Now.Font(_contextFont);
        scope.Dispose();
        scope.Dispose();

        Assert.AreSame(_baseFont, Now.font);
    }

    [Test]
    public void DisposingCopiedScopeCannotPopANewerScope()
    {
        var first = Now.Font(_contextFont);
        var staleCopy = first;
        first.Dispose();

        var current = Now.Font(_contextFont);

        try
        {
            staleCopy.Dispose();
            Assert.AreSame(_contextFont, Now.font);
        }
        finally
        {
            current.Dispose();
        }

        Assert.AreSame(_baseFont, Now.font);
    }

    [Test]
    public void OutOfOrderDisposeThrowsWithoutCorruptingFontStack()
    {
        var outer = Now.Font(_contextFont);
        var inner = Now.Font(_baseFont);

        try
        {
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.AreSame(_baseFont, Now.font, "a rejected dispose must leave the inner scope active");
        }
        finally
        {
            inner.Dispose();
            outer.Dispose();
        }

        Assert.AreSame(_baseFont, Now.font);
    }
}
