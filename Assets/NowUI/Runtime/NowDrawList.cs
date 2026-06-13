using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    public sealed class NowDrawList : IDisposable
    {
        readonly NowUIMeshLayout _layout;

        readonly string _meshName;

        readonly List<NowUICanvasMeshPage> _extraCanvasPages = new List<NowUICanvasMeshPage>(2);

        int _canvasPageCount = 1;

        internal List<NowUIMeshBatch> batches { get; } = new List<NowUIMeshBatch>(4);

        public Mesh mesh { get; private set; }

        public Vector2 size { get; private set; }

        public int batchCount => batches.Count;

        public bool hasGeometry => mesh != null && mesh.vertexCount > 0 && batches.Count > 0;

        public NowDrawList()
            : this(NowUIMeshLayout.Render, "NowUI Draw List Mesh")
        {
        }

        internal NowDrawList(NowUIMeshLayout layout, string meshName)
        {
            _layout = layout;
            _meshName = meshName;
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
                Now.BeginSuppressDraw();
                return new NowUIDrawScope(this, positionOffset, false);
            }

            var mask = new Vector4(0, 0, size.x, size.y);
            Now.BeginMeshCapture(mask);
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

            for (int i = 0; i < _extraCanvasPages.Count; ++i)
                _extraCanvasPages[i].Dispose();

            mesh = null;
            batches.Clear();
            _extraCanvasPages.Clear();
            size = default;
        }

        void ClearGeometry()
        {
            mesh.Clear();
            batches.Clear();
            _canvasPageCount = 1;

            for (int i = 0; i < _extraCanvasPages.Count; ++i)
                _extraCanvasPages[i].Clear();
        }

        internal void EndScope(Vector2 positionOffset, bool capturesMesh)
        {
            if (capturesMesh)
            {
                // Popups and other deferred overlays land inside this capture; hosts
                // that flushed earlier (with their input scope active) make this a no-op.
                NowUIOverlay.Flush();

                if (_layout == NowUIMeshLayout.Canvas)
                    Now.EndCanvasMeshCapture(this, positionOffset);
                else
                    Now.EndMeshCapture(mesh, batches, positionOffset, _layout);

                return;
            }

            Now.EndSuppressDraw();
        }

        internal void CancelScope(bool capturesMesh)
        {
            if (capturesMesh)
                Now.CancelMeshCapture();
            else
                Now.EndSuppressDraw();

            ClearGeometry();
        }

        void ThrowIfDisposed()
        {
            if (mesh == null)
                throw new ObjectDisposedException(nameof(NowDrawList));
        }

        internal int canvasPageCount => _canvasPageCount;

        internal void PrepareCanvasPages(int pageCount)
        {
            _canvasPageCount = Mathf.Max(1, pageCount);

            while (_extraCanvasPages.Count < _canvasPageCount - 1)
            {
                int pageIndex = _extraCanvasPages.Count + 1;
                _extraCanvasPages.Add(new NowUICanvasMeshPage($"{_meshName} Page {pageIndex + 1}"));
            }

            batches.Clear();

            for (int i = 0; i < _extraCanvasPages.Count; ++i)
                _extraCanvasPages[i].Clear();
        }

        internal Mesh GetCanvasMesh(int pageIndex)
        {
            return pageIndex == 0 ? mesh : _extraCanvasPages[pageIndex - 1].mesh;
        }

        internal List<NowUIMeshBatch> GetCanvasBatches(int pageIndex)
        {
            return pageIndex == 0 ? batches : _extraCanvasPages[pageIndex - 1].batches;
        }
    }

    sealed class NowUICanvasMeshPage : IDisposable
    {
        public Mesh mesh { get; private set; }

        internal List<NowUIMeshBatch> batches { get; } = new List<NowUIMeshBatch>(8);

        public NowUICanvasMeshPage(string meshName)
        {
            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave
            };

            mesh.MarkDynamic();
        }

        public void Clear()
        {
            if (mesh)
                mesh.Clear();

            batches.Clear();
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
        }
    }

    [NowScope]
    public struct NowUIDrawScope : IDisposable
    {
        NowDrawList _drawList;

        readonly Vector2 _positionOffset;

        readonly bool _capturesMesh;

        bool _disposed;

        internal NowUIDrawScope(NowDrawList drawList, Vector2 positionOffset, bool capturesMesh)
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
}
