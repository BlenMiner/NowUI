using UnityEngine;

namespace NowUI
{
    /// <summary>Horizontal placement of each text line inside a <c>NowText</c> rect.</summary>
    public enum NowTextAlign : byte
    {
        /// <summary>Lines start at the rect's left edge (the default).</summary>
        Left,

        /// <summary>Each line is centered within the rect's width.</summary>
        Center,

        /// <summary>Each line ends at the rect's right edge.</summary>
        Right
    }

    /// <summary>Vertical placement of a text block inside a <c>NowText</c> rect.</summary>
    public enum NowTextVerticalAlign : byte
    {
        /// <summary>The first line box starts at the rect's top edge (the default).</summary>
        Top,

        /// <summary>The block of line boxes is centered within the rect's height.</summary>
        Middle,

        /// <summary>The last line box ends at the rect's bottom edge.</summary>
        Bottom,

        /// <summary>
        /// Optical centering: the span from the first line's cap height to the last
        /// line's baseline is centered, so capitals and digits sit visually centered
        /// in pills, buttons, and badges regardless of the font's ascent and descent.
        /// </summary>
        CapMiddle
    }

    /// <summary>
    /// Vertical font metrics in em units: multiply by the font size for UI units.
    /// Returned by <see cref="NowFontAsset.GetMetrics(NowFontStyle)"/>.
    /// </summary>
    public readonly struct NowFontMetrics
    {
        /// <summary>Distance between consecutive baselines; the height of one line box.</summary>
        public readonly float lineHeight;

        /// <summary>Distance from the top of a line box down to its baseline.</summary>
        public readonly float ascender;

        /// <summary>Distance from the baseline down to the lowest descent (positive).</summary>
        public readonly float descender;

        /// <summary>Height of flat capital letters above the baseline, measured from the font's "H".</summary>
        public readonly float capHeight;

        /// <summary>Height of flat lowercase letters above the baseline, measured from the font's "x".</summary>
        public readonly float xHeight;

        public NowFontMetrics(float lineHeight, float ascender, float descender, float capHeight, float xHeight)
        {
            this.lineHeight = lineHeight;
            this.ascender = ascender;
            this.descender = descender;
            this.capHeight = capHeight;
            this.xHeight = xHeight;
        }

        /// <summary>Returns these metrics multiplied by <paramref name="fontSize"/>.</summary>
        public NowFontMetrics Scale(float fontSize)
        {
            return new NowFontMetrics(
                lineHeight * fontSize,
                ascender * fontSize,
                descender * fontSize,
                capHeight * fontSize,
                xHeight * fontSize);
        }
    }
}
