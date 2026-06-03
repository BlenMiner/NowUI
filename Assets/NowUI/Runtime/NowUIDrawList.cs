using System;
using System.Collections.Generic;
using NowUIInternal;
using UnityEngine;

public sealed class NowUIDrawList : IDisposable
{
    readonly NowUIMeshLayout _layout;

    internal List<NowUIMeshBatch> batches { get; } = new List<NowUIMeshBatch>(4);

    public Mesh mesh { get; private set; }

    public Vector2 size { get; private set; }

    public int batchCount => batches.Count;

    public bool hasGeometry => mesh != null && mesh.vertexCount > 0 && batches.Count > 0;

    public NowUIDrawList()
        : this(NowUIMeshLayout.Render, "NowUI Draw List Mesh")
    {
    }

    internal NowUIDrawList(NowUIMeshLayout layout, string meshName)
    {
        _layout = layout;
        mesh = new Mesh
        {
            name = meshName,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.MarkDynamic();
    }

    public NowUIDrawScope Begin(Vector2 size)
    {
        return Begin(size, Vector2.zero);
    }

    internal NowUIDrawScope Begin(Vector2 size, Vector2 positionOffset)
    {
        ThrowIfDisposed();

        this.size = size;
        ClearGeometry();

        if (size.x <= 0f || size.y <= 0f)
        {
            NowUI.BeginSuppressDraw();
            return new NowUIDrawScope(this, positionOffset, false);
        }

        var mask = new Vector4(0, 0, size.x, size.y);
        NowUI.BeginMeshCapture(mask);
        return new NowUIDrawScope(this, positionOffset, true);
    }

    public void Clear()
    {
        ThrowIfDisposed();

        ClearGeometry();
        size = default;
    }

    public void Dispose()
    {
        if (!mesh)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(mesh);
        else
            UnityEngine.Object.DestroyImmediate(mesh);

        mesh = null;
        batches.Clear();
        size = default;
    }

    void ClearGeometry()
    {
        mesh.Clear();
        batches.Clear();
    }

    internal void EndScope(Vector2 positionOffset, bool capturesMesh)
    {
        if (capturesMesh)
        {
            NowUI.EndMeshCapture(mesh, batches, positionOffset, _layout);
            return;
        }

        NowUI.EndSuppressDraw();
    }

    internal void CancelScope(bool capturesMesh)
    {
        if (capturesMesh)
            NowUI.CancelMeshCapture();
        else
            NowUI.EndSuppressDraw();

        ClearGeometry();
    }

    void ThrowIfDisposed()
    {
        if (mesh == null)
            throw new ObjectDisposedException(nameof(NowUIDrawList));
    }
}

public struct NowUIDrawScope : IDisposable
{
    NowUIDrawList _drawList;

    readonly Vector2 _positionOffset;

    readonly bool _capturesMesh;

    bool _disposed;

    internal NowUIDrawScope(NowUIDrawList drawList, Vector2 positionOffset, bool capturesMesh)
    {
        _drawList = drawList;
        _positionOffset = positionOffset;
        _capturesMesh = capturesMesh;
        _disposed = false;
    }

    public bool capturesMesh => !_disposed && _drawList != null && _capturesMesh;

    public void Dispose()
    {
        if (_disposed)
            return;

        var drawList = _drawList;
        _drawList = null;
        _disposed = true;

        drawList?.EndScope(_positionOffset, _capturesMesh);
    }

    internal void Cancel()
    {
        if (_disposed)
            return;

        var drawList = _drawList;
        _drawList = null;
        _disposed = true;

        drawList?.CancelScope(_capturesMesh);
    }
}
