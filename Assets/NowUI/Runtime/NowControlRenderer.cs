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

        [System.NonSerialized]
        int _builtInState;

        /// <summary>
        /// True for the built-in renderer types, whose control visuals stay within
        /// the theme's stock reach of the control rect (see
        /// <see cref="NowControls.IsOutsideView"/>), so controls may skip calling them
        /// when that area is clipped away. Subclasses can draw anywhere and are
        /// always called.
        /// </summary>
        internal bool isBuiltIn
        {
            get
            {
                if (_builtInState == 0)
                {
                    var type = GetType();
                    _builtInState =
                        type == typeof(NowControlRenderer) ||
                        type == typeof(NowMaterialControlRenderer) ||
                        type == typeof(NowUnityEditorControlRenderer)
                            ? 1
                            : 2;
                }

                return _builtInState == 1;
            }
        }
    }
}
