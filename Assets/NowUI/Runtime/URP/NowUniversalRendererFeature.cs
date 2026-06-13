#if NOWUI_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NowUI
{
    public sealed class NowUniversalRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField, Tooltip("Pixels per UI unit. 1 draws in raw pixels; enable Scale By Display Density to follow NowScreen.recommendedUIScale instead.")]
        float _uiScale = 1f;

        [SerializeField, Tooltip("Use NowScreen.recommendedUIScale so UI keeps a consistent physical size on high-DPI displays. Overrides UI Scale.")]
        bool _scaleByDisplayDensity;

        NowUniversalRenderPass _pass;

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

        float ResolveUIScale()
        {
            return _scaleByDisplayDensity ? NowScreen.recommendedUIScale : _uiScale;
        }

        public override void Create()
        {
            _pass = new NowUniversalRenderPass
            {
                renderPassEvent = _renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            if (!NowPipelineGraphic.HasGraphicsFor(camera))
                return;

            if (_pass == null)
                Create();

            _pass.renderPassEvent = _renderPassEvent;
            _pass.uiScale = ResolveUIScale();
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }

        sealed class NowUniversalRenderPass : ScriptableRenderPass
        {
            readonly NowDrawList _drawList = new NowDrawList();

            public float uiScale = 1f;

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;

                if (!NowPipelineGraphic.BuildDrawList(camera, _drawList, uiScale))
                    return;

                var commandBuffer = CommandBufferPool.Get("Now URP");

                try
                {
                    NowRenderer.Draw(commandBuffer, _drawList);
                    context.ExecuteCommandBuffer(commandBuffer);
                }
                finally
                {
                    CommandBufferPool.Release(commandBuffer);
                }
            }

            public void Dispose()
            {
                _drawList.Dispose();
            }
        }
    }
}
#endif
