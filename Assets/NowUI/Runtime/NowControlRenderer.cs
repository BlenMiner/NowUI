using UnityEngine;

namespace NowUI
{
    public partial class NowControlRenderer : ScriptableObject
    {
        static NowControlRenderer _defaultRenderer;

        public static NowControlRenderer defaultRenderer
        {
            get
            {
                if (_defaultRenderer == null)
                {
                    _defaultRenderer = CreateInstance<NowControlRenderer>();
                    _defaultRenderer.name = "Now Default Control Renderer";
                    _defaultRenderer.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultRenderer;
            }
        }
    }
}
