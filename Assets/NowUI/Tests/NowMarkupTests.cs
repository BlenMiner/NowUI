using System.Text.RegularExpressions;
using NUnit.Framework;
using NowUI;
using NowUI.Markup;
using UnityEngine;
using UnityEngine.TestTools;

public class NowMarkupTests
{
    sealed class FakeProvider : INowInputProvider
    {
        public NowInputSnapshot snapshot;

        public bool TryGetSnapshot(NowInputSurface surface, out NowInputSnapshot result)
        {
            result = snapshot;
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
        NowMarkup.Reset();
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
        NowMarkup.Reset();
    }

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

    [Test]
    public void CachedDocumentsAreReusedAcrossAlternation()
    {
        var a1 = NowMarkup.GetCached("<text>alternating alpha</text>");
        var b1 = NowMarkup.GetCached("<text>alternating beta</text>");

        Assert.AreSame(a1, NowMarkup.GetCached("<text>alternating alpha</text>"),
            "alternating between documents must not evict either one");
        Assert.AreSame(b1, NowMarkup.GetCached("<text>alternating beta</text>"),
            "alternating between documents must not evict either one");
    }

    [Test]
    public void ResultsAreInvalidatedByTheNextDraw()
    {
        var document = NowMarkupDocument.Parse("<column><text>events</text></column>");
        var rect = new NowRect(0, 0, 200f, 120f);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            var first = document.Draw(rect);
            Assert.IsNotNull(first.events, "a fresh result must expose its events");

            var second = document.Draw(rect);

            Assert.Throws<System.InvalidOperationException>(() => { var _ = first.events; },
                "reading a result after the document drew again must throw");
            Assert.IsNotNull(second.events, "the latest result must stay readable");
        }
    }

    [Test]
    public void ParserTreatsHrAndProgressAsVoidTags()
    {
        var root = NowMarkupParser.Parse("<h2>Title</h2><hr><progress value=\"0.5\"><text>after</text>");

        Assert.AreEqual(4, root.children.Count);
        Assert.AreEqual("h2", root.children[0].name);
        Assert.AreEqual("hr", root.children[1].name);
        Assert.AreEqual("progress", root.children[2].name);
        Assert.AreEqual("text", root.children[3].name);
    }

    [Test]
    public void ManifestDeclaresIdsKeysAndActions()
    {
        var document = NowMarkupDocument.Parse(
            "<column>" +
            "<slider id=\"volume\" state=\"volume\" />" +
            "<button id=\"save\" on-click=\"emit(save); toggle(details)\">Save</button>" +
            "<radio group=\"quality\" value=\"2\">High</radio>" +
            "<gallery id=\"photos\" index=\"photo\"><slide><text>One</text></slide></gallery>" +
            "<tabs id=\"pages\"><tab title=\"One\"><text>A</text></tab></tabs>" +
            "</column>");

        var manifest = document.manifest;

        Assert.IsTrue(manifest.DeclaresId("volume"));
        Assert.IsTrue(manifest.DeclaresId("save"));
        Assert.IsTrue(manifest.DeclaresId("photos.prev"), "gallery derives prev/next button ids");
        Assert.IsTrue(manifest.DeclaresKey("volume"));
        Assert.IsTrue(manifest.DeclaresKey("quality"), "radio groups are state keys");
        Assert.IsTrue(manifest.DeclaresKey("photo"));
        Assert.IsTrue(manifest.DeclaresKey("pages.index"), "tabs without a bound key fall back to id + \".index\"");
        Assert.IsTrue(manifest.DeclaresAction("save"));
        Assert.IsFalse(manifest.DeclaresAction("toggle"), "only emit() names are actions");
        Assert.IsFalse(manifest.DeclaresId("missing"));
    }

    [Test]
    public void BindingsGenerateConstantsFromTheDocument()
    {
        var document = NowMarkupDocument.Parse(
            "<slider id=\"master-volume\" state=\"volume\" /><button id=\"save\" on-click=\"emit(save-game)\">Save</button>");
        string source = NowMarkupBindings.GenerateSource(document, "MainMarkup", "MyGame.UI");

        StringAssert.Contains("namespace MyGame.UI", source);
        StringAssert.Contains("public static class MainMarkup", source);
        StringAssert.Contains("public const string MasterVolume = \"master-volume\";", source);
        StringAssert.Contains("public const string Volume = \"volume\";", source);
        StringAssert.Contains("public const string SaveGame = \"save-game\";", source);
    }

    [Test]
    public void UnknownResultQueriesWarnOnce()
    {
        var document = NowMarkupDocument.Parse("<button id=\"save\">Save</button>");
        var rect = new NowRect(0, 0, 200f, 120f);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            var result = document.Draw(rect);

            LogAssert.Expect(LogType.Warning, new Regex("queried click \"sve\""));
            Assert.IsFalse(result.Clicked("sve"));
            Assert.IsFalse(result.Clicked("sve"), "the same unknown query must warn only once");
            Assert.IsFalse(result.Clicked("save"), "declared ids must not warn");
        }

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void HtmlInspiredTagsDrawWithoutErrors()
    {
        var document = NowMarkupDocument.Parse(
            "<column gap=\"6\">" +
            "<h1>Heading</h1><hr/>" +
            "<ul><li>Alpha <strong>bold</strong></li><li>Beta</li></ul>" +
            "<ol><li>One</li><li>Two</li></ol>" +
            "<switch id=\"sound\" state=\"sound\">Sound</switch>" +
            "<progress id=\"download\" state=\"download\" max=\"100\" />" +
            "<radio group=\"quality\" value=\"0\" checked=\"true\">Low</radio>" +
            "<radio group=\"quality\" value=\"1\">High</radio>" +
            "<details id=\"advanced\" open=\"true\"><summary>Advanced</summary><text>Body</text></details>" +
            "<tabs id=\"pages\" state=\"page\"><tab title=\"First\"><text>A</text></tab><tab title=\"Second\"><text>B</text></tab></tabs>" +
            "<badge>New</badge><chip id=\"tag\" state=\"tag\">Tag</chip>" +
            "<text>Mix <strong>strong</strong> and <em>em</em>.</text>" +
            "</column>");
        var state = new NowMarkupState();
        state.SetFloat("download", 40f);
        var rect = new NowRect(0, 0, 480f, 640f);

        using (NowInput.Begin(_provider, Surface))
        using (_drawList.Begin(Surface))
        {
            document.Draw(rect, state);
            document.Draw(rect, state);
        }

        Assert.IsTrue(state.Has("quality"), "a checked radio seeds its group key");
        Assert.AreEqual(0, state.GetInt("quality"));
    }

    [Test]
    public void CachedDocumentDrawIsAllocationFreeAfterWarmup()
    {
        var document = NowMarkupDocument.Parse(
            "<style>.card { background: #202830; radius: 6; padding: 8; gap: 6; }</style>" +
            "<column class=\"card\"><text font-size=\"18\" color=\"#ffcc00\">Steady <b>state</b> heading</text>" +
            "<text>plain body copy that stays unchanged</text></column>");
        var rect = new NowRect(0, 0, 320f, 240f);
        var state = new NowMarkupState();

        void DrawFrame()
        {
            using (NowInput.Begin(_provider, Surface))
            using (_drawList.Begin(Surface))
                document.Draw(rect, state);
        }

        DrawFrame();
        DrawFrame();
        DrawFrame();

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

        DrawFrame();
        long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0, allocated, "steady-state markup draw must not allocate");
    }
}
