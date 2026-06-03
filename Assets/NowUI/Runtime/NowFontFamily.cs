using UnityEngine;

[CreateAssetMenu(menuName = "NowUI/Font Family")]
public sealed class NowFontFamily : NowFontAsset
{
    [SerializeField]
    NowFont _regular;

    [SerializeField]
    NowFont _bold;

    [SerializeField]
    NowFont _italic;

    [SerializeField]
    NowFont _boldItalic;

    public NowFont regular => _regular;

    public NowFont bold => _bold;

    public NowFont italic => _italic;

    public NowFont boldItalic => _boldItalic;

    protected override bool TryGetOwnFont(NowFontStyle style, out NowFont font)
    {
        bool wantsBold = (style & NowFontStyle.Bold) != 0;
        bool wantsItalic = (style & NowFontStyle.Italic) != 0;

        if (wantsBold && wantsItalic)
        {
            if (_boldItalic != null)
            {
                font = _boldItalic;
                return true;
            }

            if (_bold != null)
            {
                font = _bold;
                return true;
            }

            if (_italic != null)
            {
                font = _italic;
                return true;
            }
        }
        else if (wantsBold)
        {
            if (_bold != null)
            {
                font = _bold;
                return true;
            }
        }
        else if (wantsItalic)
        {
            if (_italic != null)
            {
                font = _italic;
                return true;
            }
        }

        font = _regular;
        return font != null;
    }
}
