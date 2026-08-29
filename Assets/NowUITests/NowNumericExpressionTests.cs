using NUnit.Framework;

public class NowNumericExpressionTests
{
    [TestCase("1 + 1", 2d)]
    [TestCase("1 + 2 * 3", 7d)]
    [TestCase("(1 + 2) * 3", 9d)]
    [TestCase("-.5 * (-4)", 2d)]
    [TestCase("8 / 4 / 2", 1d)]
    [TestCase("5 % 2", 1d)]
    [TestCase("2 ^ 3", 8d)]
    [TestCase("1--2", 3d)]
    [TestCase("1+-2", -1d)]
    [TestCase("1e2 + 1", 101d)]
    [TestCase("1.5E+2 / 3e1", 5d)]
    [TestCase("2 ^ 3 ^ 2", 512d)]
    [TestCase("-2 ^ 2", -4d)]
    [TestCase("2 ^ 3 ^ 2e-0", 512d)]
    [TestCase("4 ^ 5e-1", 2d)]
    public void EvaluatesStrictArithmetic(string text, double expected)
    {
        Assert.IsTrue(NowUI.NowNumericExpression.TryEvaluate(text, out double actual));
        Assert.AreEqual(expected, actual, 0.0000001d);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(".")]
    [TestCase("1+")]
    [TestCase("()")]
    [TestCase("( )")]
    [TestCase("(1+2")]
    [TestCase("1+2)")]
    [TestCase("2(3)")]
    [TestCase("(2)3")]
    [TestCase("1 2")]
    [TestCase("1**2")]
    [TestCase("1//2")]
    [TestCase("1e + 1")]
    [TestCase("1e- + 1")]
    [TestCase("value + 1")]
    [TestCase("1,5 + 1")]
    public void RejectsIncompleteOrUnsupportedSyntax(string text)
    {
        Assert.IsFalse(NowUI.NowNumericExpression.TryEvaluate(text, out double actual));
        Assert.AreEqual(0d, actual);
    }

    [TestCase("1 / 0")]
    [TestCase("0 / 0")]
    [TestCase("1 / (2 - 2)")]
    [TestCase("1 % (2 - 2)")]
    [TestCase("10 ^ 10000")]
    public void RejectsNonFiniteResults(string text)
    {
        Assert.IsFalse(NowUI.NowNumericExpression.TryEvaluate(text, out double actual));
        Assert.AreEqual(0d, actual);
    }

    [Test]
    public void AllowsAtMostThirtyTwoNestedParentheses()
    {
        string accepted = new string('(', 32) + "1 + 1" + new string(')', 32);
        string rejected = new string('(', 33) + "1 + 1" + new string(')', 33);

        Assert.IsTrue(NowUI.NowNumericExpression.TryEvaluate(accepted, out double actual));
        Assert.AreEqual(2d, actual);
        Assert.IsFalse(NowUI.NowNumericExpression.TryEvaluate(rejected, out actual));
        Assert.AreEqual(0d, actual);
    }

    [Test]
    public void RejectsInputLongerThanTwoHundredFiftySixCharacters()
    {
        string text = new string('1', 257);

        Assert.IsFalse(NowUI.NowNumericExpression.TryEvaluate(text, out double actual));
        Assert.AreEqual(0d, actual);
    }

    [TestCase("9007199254740992 + 1", 9007199254740993L)]
    [TestCase("18014398509481986 / 2", 9007199254740993L)]
    [TestCase("9007199254740993 % 2", 1L)]
    [TestCase("2 ^ 62", 4611686018427387904L)]
    [TestCase("9223372036854775807 * 2 / 2", long.MaxValue)]
    [TestCase("-9223372036854775807 - 1", long.MinValue)]
    [TestCase("9007199254740993.75 + .25", 9007199254740994L)]
    [TestCase("9.007199254740993e15 + 1", 9007199254740994L)]
    [TestCase("-3 / 2", -1L)]
    [TestCase("9223372036854775807.9", long.MaxValue)]
    [TestCase("-9223372036854775808.9", long.MinValue)]
    [TestCase("2 ^ 3 ^ 2", 512L)]
    [TestCase("-2 ^ 2", -4L)]
    [TestCase("4 ^ 5e-1", 2L)]
    public void EvaluatesLongArithmeticExactly(string text, long expected)
    {
        Assert.IsTrue(NowUI.NowNumericExpression.TryEvaluateLong(text, out long actual));
        Assert.AreEqual(expected, actual);
    }

    [TestCase("9223372036854775807 + 1")]
    [TestCase("-9223372036854775808 - 1")]
    [TestCase("2 ^ 63")]
    [TestCase("1 / 0")]
    [TestCase("1 % 0")]
    [TestCase("10 ^ 10000")]
    [TestCase("1e10000")]
    public void RejectsLongOverflowAndInvalidMath(string text)
    {
        Assert.IsFalse(NowUI.NowNumericExpression.TryEvaluateLong(text, out long actual));
        Assert.AreEqual(0L, actual);
    }
}
