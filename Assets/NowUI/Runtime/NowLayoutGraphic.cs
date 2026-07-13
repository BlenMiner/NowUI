using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// UGUI host for NowLayout content. It owns the exact measure/draw cycle, so
    /// root layout code uses ordinary Row/Column scopes and never needs its own
    /// RunMeasured wrapper. <see cref="NowGraphic.DrawNowUI"/> runs once with
    /// drawing suppressed and input passive, then once for the real draw.
    /// Reacting to control results is safe; guard unconditional state changes
    /// with <see cref="NowLayout.isMeasurePass"/>.
    /// </summary>
    [AddComponentMenu("NowUI/Now Layout Graphic")]
    public class NowLayoutGraphic : NowGraphic
    {
        internal sealed override bool useLayoutMeasurePass => true;
    }
}
