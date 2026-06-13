using UnityEngine;
using NowUI;
using NowUI.Markdown;

/// <summary>
/// Renders GitHub-flavored Markdown inside a scroll view. Edit the text in the
/// inspector; clicked links open in the system browser.
/// </summary>
[AddComponentMenu("NowUI/Examples/Now Markdown")]
public class NowMarkdownExample : NowGraphic
{
    const string Sample =
        "# Now Markdown\n" +
        "\n" +
        "Everything below renders through `Now` and `NowLayout` — no HTML, no JavaScript.\n" +
        "\n" +
        "## Inline styles\n" +
        "\n" +
        "Text can be **bold**, *italic*, ***both***, ~~struck through~~, or `inline code`.\n" +
        "Links work too: [the GFM spec](https://github.github.com/gfm/) and bare autolinks\n" +
        "like https://github.com just work.\n" +
        "\n" +
        "## Blocks\n" +
        "\n" +
        "> Block quotes carry a bar and inset their content.\n" +
        "> They can span multiple lines.\n" +
        "\n" +
        "```csharp\n" +
        "if (NowLayout.Button(\"Save\").Draw())\n" +
        "    Save();\n" +
        "```\n" +
        "\n" +
        "## Lists\n" +
        "\n" +
        "- Bullets\n" +
        "- Nest as you expect\n" +
        "  - Like this\n" +
        "1. Ordered too\n" +
        "2. With numbers\n" +
        "- [x] Task lists (GFM)\n" +
        "- [ ] Unchecked\n" +
        "\n" +
        "## Images\n" +
        "\n" +
        "Images download asynchronously and scale to the available width:\n" +
        "\n" +
        "![GitHub logo](https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png)\n" +
        "\n" +
        "## Tables\n" +
        "\n" +
        "| Feature | Status | Notes |\n" +
        "| :--- | :---: | ---: |\n" +
        "| Tables | done | aligned |\n" +
        "| **Styling** | works | in cells |\n" +
        "\n" +
        "---\n" +
        "\n" +
        "That's a thematic break above.\n";

    [SerializeField] NowFontAsset _font;
    [SerializeField, TextArea(10, 30)] string _markdown = Sample;

    protected override void DrawNowUI(NowRect rect)
    {
        if (_font == null)
            return;

        Now.defaultFont = _font;
        var theme = NowControls.themeAsset;
        var bounds = new NowRect(0, 0, rect.width, rect.height);

        theme.Rectangle(bounds, NowRectangleStyle.Surface).SetRadius(14).Draw();

        using (NowLayout.Area(bounds.Inset(16)))
        using (NowLayout.ScrollView().Begin())
        {
            var result = NowMarkdown.Document(_markdown).Draw();

            if (result.clickedLink != null)
                Application.OpenURL(result.clickedLink);
        }
    }
}
