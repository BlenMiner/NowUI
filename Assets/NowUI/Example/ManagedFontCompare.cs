using NowUI;
using UnityEngine;

/// <summary>
/// Visual A/B of the native and managed font compilers: the same TrueType source
/// is compiled twice — once through the native nowui-msdf plugin and once through
/// the managed Burst fallback — and rendered side by side at several sizes, with
/// outlines, which stress the distance field the hardest.
///
/// Attach to a camera (built-in pipeline). Leave the font field empty to use the
/// default NotoSans, or assign any compiled NowFont with embedded source bytes.
/// </summary>
public class ManagedFontCompare : MonoBehaviour
{
    [SerializeField] NowFont _sourceFont;

    const string SampleText = "Spry gnomes vex Bold Quartz! 0123456789 @#&";

    static readonly float[] Sizes = { 14f, 24f, 48f, 96f };

    NowFont _nativeFont;
    NowFont _managedFont;
    string _error;

    void Start()
    {
        byte[] bytes = ResolveSourceBytes();

        if (bytes == null)
        {
            _error = "No font with embedded source bytes available.";
            return;
        }

        // The compiler backend is picked when a font bakes its first glyph, so
        // bake the sample charset immediately while each flag state is active.
        NowFontCompiler.forceManagedCompiler = false;

        if (NowFontCompiler.TryCompile(bytes, out _nativeFont, out string error))
            BakeSamples(_nativeFont);
        else
            _error = error;

        NowFontCompiler.forceManagedCompiler = true;

        if (NowFontCompiler.TryCompile(bytes, out _managedFont, out error))
            BakeSamples(_managedFont);
        else
            _error = error;

        NowFontCompiler.forceManagedCompiler = false;
    }

    byte[] ResolveSourceBytes()
    {
        if (_sourceFont != null && _sourceFont.TryGetSourceBytes(out byte[] assigned))
            return assigned;

        // Resolve the concrete NowFont behind the default font asset.
        var fallback = Now.defaultFont;

        if (fallback != null &&
            fallback.TryResolveGlyph('A', 32f, NowFontStyle.Regular, out NowFont resolved, out _, out _) &&
            resolved.TryGetSourceBytes(out byte[] bytes))
        {
            return bytes;
        }

        return null;
    }

    static void BakeSamples(NowFont font)
    {
        foreach (float size in Sizes)
            font.EnsureGlyphs(SampleText + "native compiled (plugin)managed compiled (Burst fallback)", size);
    }

    void OnDestroy()
    {
        if (_nativeFont != null)
            Destroy(_nativeFont);

        if (_managedFont != null)
            Destroy(_managedFont);
    }

    void OnPostRender()
    {
        Now.StartUI();

        float columnWidth = Now.screenMask.width * 0.5f - 24f;

        Now.Rectangle(new NowRect(8, 8, Now.screenMask.width - 16, Now.screenMask.height - 16))
            .SetColor(new Color(0.09f, 0.1f, 0.12f, 1f))
            .SetRadius(10)
            .Draw();

        if (_error != null)
        {
            Now.Text(new NowRect(24, 24, Now.screenMask.width - 48, 40))
                .SetFontSize(20)
                .SetColor(new Color(1f, 0.5f, 0.4f, 1f))
                .Draw(_error);

            Now.FlushUI();
            return;
        }

        DrawColumn(_nativeFont, "native compiled (plugin)", 16f, columnWidth);
        DrawColumn(_managedFont, "managed compiled (Burst fallback)", 32f + columnWidth, columnWidth);

        Now.FlushUI();
    }

    static void DrawColumn(NowFont font, string title, float x, float width)
    {
        if (font == null)
            return;

        float y = 24f;

        Now.Text(new NowRect(x + 8, y, width, 30), font)
            .SetFontSize(22)
            .SetColor(new Color(0.55f, 0.8f, 1f, 1f))
            .Draw(title);

        y += 44f;

        foreach (float size in Sizes)
        {
            Now.Text(new NowRect(x + 8, y, width, size * 1.4f), font)
                .SetFontSize(size)
                .SetColor(Color.white)
                .Draw(SampleText);

            y += size * 1.5f;
        }

        // Outlines push the threshold away from 0.5 and expose any field artifacts.
        Now.Text(new NowRect(x + 8, y, width, 70), font)
            .SetFontSize(48)
            .SetColor(Color.white)
            .SetOutline(2)
            .SetOutlineColor(new Color(1f, 0.45f, 0.2f, 1f))
            .Draw("Outline 48px");

        y += 80f;

        Now.Text(new NowRect(x + 8, y, width, 40), font)
            .SetFontSize(26)
            .SetColor(new Color(0.7f, 1f, 0.6f, 1f))
            .SetOutline(-1.5f)
            .SetOutlineColor(Color.black)
            .Draw("Negative outline 26px");
    }
}
