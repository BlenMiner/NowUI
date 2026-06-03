#if NOWUI_HDRP
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public sealed class NowUIHighDefinitionCustomPass : CustomPass
{
    NowUIDrawList _drawList;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        _drawList = new NowUIDrawList();
    }

    protected override void Execute(CustomPassContext ctx)
    {
        var camera = ctx.hdCamera.camera;

        if (!NowUIPipelineGraphic.BuildDrawList(camera, _drawList))
            return;

        NowUIRenderer.Draw(ctx.cmd, _drawList);
    }

    protected override void Cleanup()
    {
        if (_drawList == null)
            return;

        _drawList.Dispose();
        _drawList = null;
    }
}
#endif
