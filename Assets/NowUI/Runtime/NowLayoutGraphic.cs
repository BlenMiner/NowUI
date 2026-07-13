using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// UGUI host for NowLayout content. It owns the exact measure/draw cycle, so
    /// root layout code uses ordinary Row/Column scopes and never needs its own
    /// RunMeasured wrapper.
    /// </summary>
    [AddComponentMenu("NowUI/Now Layout Graphic")]
    public class NowLayoutGraphic : NowGraphic
    {
        internal sealed override bool useLayoutMeasurePass => true;
    }
}
