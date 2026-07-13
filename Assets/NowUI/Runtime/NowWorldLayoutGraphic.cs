using UnityEngine;

namespace NowUI
{
    /// <summary>World-space host that owns an exact NowLayout measure/draw cycle.</summary>
    [AddComponentMenu("NowUI/Now World Layout Graphic")]
    public class NowWorldLayoutGraphic : NowWorldGraphic
    {
        internal sealed override bool useLayoutMeasurePass => true;
    }
}
