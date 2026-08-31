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
            var cameraColor = ctx.cameraColorBuffer.rt;
            var sourceDescriptor = cameraColor != null
                ? cameraColor.descriptor
                : NowWorldGlassBackdrop.GetCameraSourceDescriptor(camera, camera.pixelWidth, camera.pixelHeight);
            var drawDescriptor = sourceDescriptor;
            var sourceScaleOffset = new UnityEngine.Vector4(1f, 1f, 0f, 0f);

            if (cameraColor != null)
            {
                // Keep the persistent world backdrop at the RTHandle allocation
                // size. Resizing it to every dynamic-resolution viewport would
                // invalidate the texture still bound to world materials for the
                // current frame. The first copy stretches the active viewport
                // over that stable backdrop instead.
                if (ctx.cameraColorBuffer.useScaling)
                {
                    var rtHandleProperties = ctx.cameraColorBuffer.rtHandleProperties;
                    var viewportSize = ctx.cameraColorBuffer.GetScaledSize(
                        rtHandleProperties.currentViewportSize);
                    drawDescriptor.width = UnityEngine.Mathf.Max(1, viewportSize.x);
                    drawDescriptor.height = UnityEngine.Mathf.Max(1, viewportSize.y);
                    var rtHandleScale = rtHandleProperties.rtHandleScale;
                    sourceScaleOffset = new UnityEngine.Vector4(
                        rtHandleScale.x,
                        rtHandleScale.y,
                        0f,
                        0f);
                }
                else
                {
                    drawDescriptor.width = UnityEngine.Mathf.Max(1, cameraColor.width);
                    drawDescriptor.height = UnityEngine.Mathf.Max(1, cameraColor.height);
                }
            }

            NowWorldGlassBackdrop.PopulateCommandBuffer(
                ctx.cmd,
                camera,
                ctx.cameraColorBuffer,
                sourceDescriptor,
                sourceScaleOffset);

            if (!NowPipelineGraphic.BuildDrawList(camera, _drawList, scale))
                return;

            NowRenderer.Draw(
                ctx.cmd,
                _drawList,
                ctx.cameraColorBuffer,
                drawDescriptor,
                sourceScaleOffset);
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
