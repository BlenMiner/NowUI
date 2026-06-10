#if NOWUI_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NowUI
{
    public sealed class NowUIUniversalRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        NowUIUniversalRenderPass _pass;

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

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;

                if (!NowUIPipelineGraphic.BuildDrawList(camera, _drawList))
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
