using NUnit.Framework;
using NowUI.Markup;

public class NowMarkupTests
{
    static string TextNode(NowMarkupFile source)
    {
        return source.document.root.children[0].children[0].text;
    }

    [Test]
    public void ParserBuildsElementsAttributesAndText()
    {
        var root = NowMarkupParser.Parse("<column gap=\"8\"><text>Hello <b>world</b></text><button id=\"save\">Save</button></column>");

        Assert.AreEqual(1, root.children.Count);
        var column = root.children[0];
        Assert.AreEqual("column", column.name);
        Assert.AreEqual("8", column.Attribute("gap"));
        Assert.AreEqual(2, column.children.Count);
        Assert.AreEqual("text", column.children[0].name);
        Assert.AreEqual("button", column.children[1].name);
        Assert.AreEqual("save", column.children[1].Attribute("id"));
    }

    [Test]
    public void ParserKeepsStyleBlocksRaw()
    {
        var root = NowMarkupParser.Parse("<style>.card { padding: 12; }</style><column class=\"card\"/>");

        Assert.AreEqual("style", root.children[0].name);
        Assert.AreEqual(".card { padding: 12; }", root.children[0].children[0].text);
        Assert.AreEqual("column", root.children[1].name);
    }

    [Test]
    public void StateStoresTypedValues()
    {
        var state = new NowMarkupState();
        state.SetBool("details", true);
        state.SetFloat("volume", 0.5f);
        state.SetString("name", "NowUI");

        Assert.IsTrue(state.GetBool("details"));
        Assert.AreEqual(0.5f, state.GetFloat("volume"), 0.0001f);
        Assert.AreEqual("NowUI", state.GetString("name"));
        Assert.IsFalse(state.Toggle("details"));
    }

    [Test]
    public void StateStepsWrappedIndexes()
    {
        var state = new NowMarkupState();
        state.SetInt("slide", 0);

        Assert.AreEqual(2, state.StepInt("slide", -1, 3));
        Assert.AreEqual(0, state.StepInt("slide", 1, 3));
    }

    [Test]
    public void FileSourceReloadsChangedMarkup()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "nowui-markup-" + System.Guid.NewGuid().ToString("N") + ".nowui");

        try
        {
            System.IO.File.WriteAllText(path, "<text>One</text>");
            var source = new NowMarkupFile(path);

            Assert.IsTrue(source.Refresh());
            Assert.AreEqual("One", TextNode(source));

            System.IO.File.WriteAllText(path, "<text>Two</text>");

            Assert.IsTrue(source.Reload());
            Assert.AreEqual("Two", TextNode(source));

            source.Dispose();
        }
        finally
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
    }
}
