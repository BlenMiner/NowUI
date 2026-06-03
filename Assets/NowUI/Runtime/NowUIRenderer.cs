using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUIRenderer : IDisposable
{
    readonly NowUIDrawList _drawList = new NowUIDrawList();

    CommandBuffer _commandBuffer;

    public Mesh mesh => _drawList.mesh;

    public Vector2 size => _drawList.size;

    public int batchCount => _drawList.batchCount;

    public bool hasGeometry => _drawList.hasGeometry;

    public NowUIRenderer()
    {
        _commandBuffer = new CommandBuffer
        {
            name = "NowUI Renderer"
        };
    }

    public NowUIDrawScope Begin(Vector2 size)
    {
        ThrowIfDisposed();
        return _drawList.Begin(size);
    }

    public NowUIDrawScope Begin(RenderTexture target)
    {
        ThrowIfDisposed();

        if (target == null)
            throw new ArgumentNullException(nameof(target));

        return Begin(new Vector2(target.width, target.height));
    }

    public void Draw(CommandBuffer commandBuffer)
    {
        ThrowIfDisposed();
        Draw(commandBuffer, _drawList);
    }

    public static void Draw(CommandBuffer commandBuffer, NowUIDrawList drawList)
    {
        if (commandBuffer == null)
            throw new ArgumentNullException(nameof(commandBuffer));

        if (drawList == null)
            throw new ArgumentNullException(nameof(drawList));

        if (!drawList.hasGeometry)
            return;

        commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, GetProjectionMatrix(drawList.size));

        var batches = drawList.batches;
        for (int i = 0; i < batches.Count; ++i)
        {
            var batch = batches[i];

            if (batch.material == null)
                continue;

            commandBuffer.DrawMesh(drawList.mesh, Matrix4x4.identity, batch.material, i, 0);
        }
    }

    public void PopulateCommandBuffer(CommandBuffer commandBuffer, RenderTargetIdentifier target)
    {
        PopulateCommandBuffer(commandBuffer, target, false, Color.clear);
    }

    public void PopulateCommandBuffer(CommandBuffer commandBuffer, RenderTargetIdentifier target, bool clear, Color clearColor)
    {
        ThrowIfDisposed();
        PopulateCommandBuffer(commandBuffer, _drawList, target, clear, clearColor);
    }

    public static void PopulateCommandBuffer(
        CommandBuffer commandBuffer,
        NowUIDrawList drawList,
        RenderTargetIdentifier target,
        bool clear,
        Color clearColor)
    {
        if (commandBuffer == null)
            throw new ArgumentNullException(nameof(commandBuffer));

        if (drawList == null)
            throw new ArgumentNullException(nameof(drawList));

        commandBuffer.SetRenderTarget(target);

        if (clear)
            commandBuffer.ClearRenderTarget(true, true, clearColor);

        Draw(commandBuffer, drawList);
    }

    public void Render(RenderTexture target)
    {
        Render(target, false, Color.clear);
    }

    public void Render(RenderTexture target, bool clear, Color clearColor)
    {
        ThrowIfDisposed();

        if (target == null)
            throw new ArgumentNullException(nameof(target));

        _commandBuffer.Clear();
        PopulateCommandBuffer(_commandBuffer, target, clear, clearColor);
        Graphics.ExecuteCommandBuffer(_commandBuffer);
    }

    public void Clear()
    {
        ThrowIfDisposed();
        _drawList.Clear();
    }

    public void Dispose()
    {
        _drawList.Dispose();

        if (_commandBuffer != null)
        {
            _commandBuffer.Release();
            _commandBuffer = null;
        }
    }

    void ThrowIfDisposed()
    {
        if (_drawList.mesh == null || _commandBuffer == null)
            throw new ObjectDisposedException(nameof(NowUIRenderer));
    }

    static Matrix4x4 GetProjectionMatrix(Vector2 size)
    {
        return Matrix4x4.Ortho(0, size.x, -size.y, 0, -1, 100);
    }
}
