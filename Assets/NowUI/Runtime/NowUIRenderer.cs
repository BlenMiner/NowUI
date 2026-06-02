using System;
using System.Collections.Generic;
using NowUIInternal;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NowUIRenderer : IDisposable
{
    readonly List<NowUIMeshBatch> _batches = new List<NowUIMeshBatch>(4);

    Mesh _mesh;

    CommandBuffer _commandBuffer;

    Vector2 _size;

    public Mesh mesh => _mesh;

    public Vector2 size => _size;

    public int batchCount => _batches.Count;

    public bool hasGeometry => _mesh != null && _mesh.vertexCount > 0 && _batches.Count > 0;

    public NowUIRenderer()
    {
        _mesh = new Mesh
        {
            name = "NowUI Renderer Mesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        _mesh.MarkDynamic();

        _commandBuffer = new CommandBuffer
        {
            name = "NowUI Renderer"
        };
    }

    public void Build(Vector2 size, Action<Rect> drawNowUI)
    {
        ThrowIfDisposed();

        _size = size;
        _mesh.Clear();
        _batches.Clear();

        if (size.x <= 0f || size.y <= 0f || drawNowUI == null)
            return;

        var mask = new Vector4(0, 0, size.x, size.y);
        NowUI.BeginMeshCapture(mask);

        try
        {
            drawNowUI(new Rect(0, 0, size.x, size.y));
            NowUI.EndMeshCaptureForRenderMesh(_mesh, _batches, Vector2.zero);
        }
        catch
        {
            NowUI.CancelMeshCapture();
            _mesh.Clear();
            _batches.Clear();
            throw;
        }
    }

    public void Draw(CommandBuffer commandBuffer)
    {
        ThrowIfDisposed();

        if (commandBuffer == null)
            throw new ArgumentNullException(nameof(commandBuffer));

        if (!hasGeometry)
            return;

        commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, GetProjectionMatrix(_size));

        for (int i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];

            if (batch.material == null)
                continue;

            commandBuffer.DrawMesh(_mesh, Matrix4x4.identity, batch.material, i, 0);
        }
    }

    public void PopulateCommandBuffer(CommandBuffer commandBuffer, RenderTargetIdentifier target)
    {
        PopulateCommandBuffer(commandBuffer, target, false, Color.clear);
    }

    public void PopulateCommandBuffer(CommandBuffer commandBuffer, RenderTargetIdentifier target, bool clear, Color clearColor)
    {
        ThrowIfDisposed();

        if (commandBuffer == null)
            throw new ArgumentNullException(nameof(commandBuffer));

        commandBuffer.SetRenderTarget(target);

        if (clear)
            commandBuffer.ClearRenderTarget(true, true, clearColor);

        Draw(commandBuffer);
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

    public void Render(RenderTexture target, Vector2 size, Action<Rect> drawNowUI, bool clear, Color clearColor)
    {
        Build(size, drawNowUI);
        Render(target, clear, clearColor);
    }

    public void Clear()
    {
        ThrowIfDisposed();

        _mesh.Clear();
        _batches.Clear();
        _size = default;
    }

    public void Dispose()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_mesh);
            else
                UnityEngine.Object.DestroyImmediate(_mesh);

            _mesh = null;
        }

        if (_commandBuffer != null)
        {
            _commandBuffer.Release();
            _commandBuffer = null;
        }
    }

    void ThrowIfDisposed()
    {
        if (_mesh == null || _commandBuffer == null)
            throw new ObjectDisposedException(nameof(NowUIRenderer));
    }

    static Matrix4x4 GetProjectionMatrix(Vector2 size)
    {
        return Matrix4x4.Ortho(0, size.x, -size.y, 0, -1, 100);
    }
}
