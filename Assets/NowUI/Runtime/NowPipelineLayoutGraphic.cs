using UnityEngine;

namespace NowUI
{
    /// <summary>Render-pipeline host that owns an exact NowLayout measure/draw cycle.</summary>
    [AddComponentMenu("NowUI/Now Pipeline Layout Graphic")]
    public class NowPipelineLayoutGraphic : NowPipelineGraphic
    {
        internal sealed override bool useLayoutMeasurePass => true;
    }
}
