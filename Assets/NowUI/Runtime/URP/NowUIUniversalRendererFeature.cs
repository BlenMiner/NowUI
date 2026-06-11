#if NOWUI_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NowUI
{
    public sealed class NowUIUniversalRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField, Tooltip("Pixels per UI unit. 1 draws in raw pixels; enable Scale By Display Density to follow NowUIScreen.recommendedUIScale instead.")]
        float _uiScale = 1f;

        [SerializeField, Tooltip("Use NowUIScreen.recommendedUIScale so UI keeps a consistent physical size on high-DPI displays. Overrides UI Scale.")]
        bool _scaleByDisplayDensity;

        NowUIUniversalRenderPass _pass;

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
            return _scaleByDisplayDensity ? NowUIScreen.recommendedUIScale : _uiScale;
        }

        public override void Create()
        {
            _pass = new NowUIUniversalRenderPass
            {
                renderPassEvent = _renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            if (!NowUIPipelineGraphic.HasGraphicsFor(camera))
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

        sealed class NowUIUniversalRenderPass : ScriptableRenderPass
        {
            readonly NowUIDrawList _drawList = new NowUIDrawList();

            public float uiScale = 1f;

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;

                if (!NowUIPipelineGraphic.BuildDrawList(camera, _drawList, uiScale))
                    return;

                var commandBuffer = CommandBufferPool.Get("NowUI URP");

                try
                {
                    NowUIRenderer.Draw(commandBuffer, _drawList);
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
