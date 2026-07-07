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

        NowUniversalWorldGlassPass _worldGlassPass;

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

            _worldGlassPass = new NowUniversalWorldGlassPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;

            bool hasPipelineGraphics = NowPipelineGraphic.HasGraphicsFor(camera);
            bool hasWorldGlass = NowWorldGlassBackdrop.HasRequest(camera);
            bool needsWorldGlassDepth = NowWorldGlassBackdrop.RequiresSceneDepth(camera);

            if (!hasPipelineGraphics && !hasWorldGlass && !needsWorldGlassDepth)
                return;

            if (_pass == null)
                Create();

            if (hasWorldGlass || needsWorldGlassDepth)
            {
                _worldGlassPass.ConfigureInput(
                    needsWorldGlassDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
                renderer.EnqueuePass(_worldGlassPass);
            }

            if (hasPipelineGraphics)
            {
                _pass.renderPassEvent = _renderPassEvent;
                _pass.uiScale = ResolveUIScale();
                renderer.EnqueuePass(_pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            _worldGlassPass = null;
        }

        sealed class NowUniversalWorldGlassPass : ScriptableRenderPass
        {
            // Compatibility Mode path (Render Graph disabled). The attribute
            // mirrors the obsolete base member, which is how URP's own samples
            // silence CS0672 while still supporting this mode; Render Graph
            // support requires a RecordRenderGraph port of the pass.
            [System.Obsolete("Compatibility Mode rendering path (Render Graph disabled).", false)]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                var commandBuffer = CommandBufferPool.Get("Now URP World Glass");

                try
                {
                    if (NowWorldGlassBackdrop.PopulateCommandBuffer(
                        commandBuffer,
                        camera,
                        BuiltinRenderTextureType.CameraTarget,
                        camera.pixelWidth,
                        camera.pixelHeight))
                    {
                        context.ExecuteCommandBuffer(commandBuffer);
                    }
                }
                finally
                {
                    CommandBufferPool.Release(commandBuffer);
                }
            }
        }

        sealed class NowUniversalRenderPass : ScriptableRenderPass
        {
            readonly NowDrawList _drawList = new NowDrawList();

            public float uiScale = 1f;

            // Compatibility Mode path (Render Graph disabled) — see above.
            [System.Obsolete("Compatibility Mode rendering path (Render Graph disabled).", false)]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;

                if (!NowPipelineGraphic.BuildDrawList(camera, _drawList, uiScale))
                    return;

                var commandBuffer = CommandBufferPool.Get("Now URP");

                try
                {
                    NowRenderer.Draw(
                        commandBuffer,
                        _drawList,
                        BuiltinRenderTextureType.CameraTarget,
                        camera.pixelWidth,
                        camera.pixelHeight);
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
