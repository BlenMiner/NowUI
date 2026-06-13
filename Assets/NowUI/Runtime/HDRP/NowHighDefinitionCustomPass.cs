#if NOWUI_HDRP
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace NowUI
{
    public sealed class NowHighDefinitionCustomPass : CustomPass
    {
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Pixels per UI unit. 1 draws in raw pixels; enable Scale By Display Density to follow NowScreen.recommendedUIScale instead.")]
        float _uiScale = 1f;

        [UnityEngine.SerializeField, UnityEngine.Tooltip("Use NowScreen.recommendedUIScale so UI keeps a consistent physical size on high-DPI displays. Overrides UI Scale.")]
        bool _scaleByDisplayDensity;

        NowDrawList _drawList;

        public float uiScale
        {
            get => _uiScale;
            set => _uiScale = value;
        }

        public bool scaleByDisplayDensity
        {
            get => _scaleByDisplayDensity;
            set => _scaleByDisplayDensity = value;
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            _drawList = new NowDrawList();
        }

        protected override void Execute(CustomPassContext ctx)
        {
            var camera = ctx.hdCamera.camera;
            float scale = _scaleByDisplayDensity ? NowScreen.recommendedUIScale : _uiScale;

            if (!NowPipelineGraphic.BuildDrawList(camera, _drawList, scale))
                return;

            NowRenderer.Draw(ctx.cmd, _drawList);
        }

        protected override void Cleanup()
        {
            if (_drawList == null)
                return;

            _drawList.Dispose();
            _drawList = null;
        }
    }
}
#endif
