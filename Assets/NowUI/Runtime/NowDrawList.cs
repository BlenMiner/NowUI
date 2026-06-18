using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;

namespace NowUI
{
    public sealed class NowDrawList : IDisposable
    {
        readonly NowMeshLayout _layout;

        readonly string _meshName;

        readonly List<NowCanvasMeshPage> _extraCanvasPages = new List<NowCanvasMeshPage>(2);

        int _canvasPageCount = 1;

        Mesh _renderReplayMesh;

        readonly List<NowMeshBatch> _renderReplayBatches = new List<NowMeshBatch>(4);

        internal List<NowMeshBatch> batches { get; } = new List<NowMeshBatch>(4);

        public Mesh mesh { get; private set; }

        public Vector2 size { get; private set; }

        public int batchCount => batches.Count;

        public bool hasGeometry => mesh != null && mesh.vertexCount > 0 && batches.Count > 0;

        internal Mesh renderReplayMesh => _renderReplayMesh;

        internal List<NowMeshBatch> renderReplayBatches => _renderReplayBatches;

        internal bool hasRenderReplay =>
            _renderReplayMesh != null &&
            _renderReplayMesh.vertexCount > 0 &&
            _renderReplayBatches.Count > 0;

        public NowDrawList()
            : this(NowMeshLayout.Render, "Now Draw List Mesh")
        {
        }

        internal NowDrawList(NowMeshLayout layout, string meshName)
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

        public NowDrawScope Begin(Vector2 size)
        {
            return Begin(size, Vector2.zero);
        }

        /// <summary>
        /// Runs a representative draw once to load materials and grow internal
        /// buffers, then clears the resulting geometry. Use this during scene or
        /// widget initialization when steady-state frames must be allocation-free.
        /// </summary>
        public void Warmup(Vector2 size, Action draw)
        {
            ThrowIfDisposed();

            if (draw == null)
                throw new ArgumentNullException(nameof(draw));

            try
            {
                using (Begin(size))
                    draw();
            }
            finally
            {
                ClearGeometry();
            }
        }

        /// <summary>
        /// Runs a representative input-aware draw once to load materials, create
        /// control state, and grow internal buffers, then clears the resulting
        /// geometry. Use this when the warmed frame contains interactive controls.
        /// </summary>
        public void Warmup(Vector2 size, INowInputProvider inputProvider, Action draw)
        {
            Warmup(new NowInputSurface(size), inputProvider, draw);
        }

        /// <summary>
        /// Runs a representative input-aware draw once against an explicit surface,
        /// then clears the resulting geometry.
        /// </summary>
        public void Warmup(NowInputSurface inputSurface, INowInputProvider inputProvider, Action draw)
        {
            ThrowIfDisposed();

            if (draw == null)
                throw new ArgumentNullException(nameof(draw));

            try
            {
                using (NowInput.Begin(inputProvider, inputSurface))
                using (Begin(inputSurface.size))
                    draw();
            }
            finally
            {
                ClearGeometry();
            }
        }

        internal NowDrawScope Begin(Vector2 size, NowGlassBlurQuality glassBlurQuality)
        {
            return Begin(size, Vector2.zero, false, glassBlurQuality);
        }

        internal NowDrawScope Begin(Vector2 size, Vector2 positionOffset)
        {
            return Begin(size, positionOffset, false);
        }

        internal NowDrawScope Begin(Vector2 size, Vector2 positionOffset, bool inheritContext)
        {
            return Begin(size, positionOffset, inheritContext, NowGlassBlurQuality.Auto);
        }

        internal NowDrawScope Begin(
            Vector2 size,
            Vector2 positionOffset,
            bool inheritContext,
            NowGlassBlurQuality glassBlurQuality)
        {
            ThrowIfDisposed();

            this.size = size;
            ClearGeometry();
            var glassQualityScope = NowGlassSettings.PushBlurQuality(glassBlurQuality);

            if (size.x <= 0f || size.y <= 0f)
            {
                Now.BeginSuppressDraw();
                return new NowDrawScope(this, positionOffset, false, glassQualityScope);
            }

            var mask = new Vector4(0, 0, size.x, size.y);
            Now.BeginMeshCapture(mask, inheritContext);
            return new NowDrawScope(this, positionOffset, true, glassQualityScope);
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

            if (_renderReplayMesh)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_renderReplayMesh);
                else
                    UnityEngine.Object.DestroyImmediate(_renderReplayMesh);
            }

            mesh = null;
            _renderReplayMesh = null;
            batches.Clear();
            _renderReplayBatches.Clear();
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

            ClearRenderReplay();
        }

        internal void EndScope(Vector2 positionOffset, bool capturesMesh)
        {
            if (capturesMesh)
            {
                // Popups and other deferred overlays land inside this capture; hosts
                // that flushed earlier (with their input scope active) make this a no-op.
                NowOverlay.Flush();

                if (_layout == NowMeshLayout.Canvas)
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
                _extraCanvasPages.Add(new NowCanvasMeshPage($"{_meshName} Page {pageIndex + 1}"));
            }

            batches.Clear();

            for (int i = 0; i < _extraCanvasPages.Count; ++i)
                _extraCanvasPages[i].Clear();
        }

        internal Mesh GetCanvasMesh(int pageIndex)
        {
            return pageIndex == 0 ? mesh : _extraCanvasPages[pageIndex - 1].mesh;
        }

        internal List<NowMeshBatch> GetCanvasBatches(int pageIndex)
        {
            return pageIndex == 0 ? batches : _extraCanvasPages[pageIndex - 1].batches;
        }

        internal Mesh EnsureRenderReplayMesh()
        {
            if (_renderReplayMesh != null)
                return _renderReplayMesh;

            _renderReplayMesh = new Mesh
            {
                name = $"{_meshName} Render Replay",
                hideFlags = HideFlags.HideAndDontSave
            };

            _renderReplayMesh.MarkDynamic();
            return _renderReplayMesh;
        }

        internal void ClearRenderReplay()
        {
            if (_renderReplayMesh)
                _renderReplayMesh.Clear();

            _renderReplayBatches.Clear();
        }
    }

    sealed class NowCanvasMeshPage : IDisposable
    {
        public Mesh mesh { get; private set; }

        internal List<NowMeshBatch> batches { get; } = new List<NowMeshBatch>(8);

        public NowCanvasMeshPage(string meshName)
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
    public struct NowDrawScope : IDisposable
    {
        NowDrawList _drawList;

        readonly Vector2 _positionOffset;

        readonly bool _capturesMesh;

        NowGlassQualityScope _glassQualityScope;

        bool _disposed;

        internal NowDrawScope(NowDrawList drawList, Vector2 positionOffset, bool capturesMesh)
            : this(drawList, positionOffset, capturesMesh, default)
        {
        }

        internal NowDrawScope(
            NowDrawList drawList,
            Vector2 positionOffset,
            bool capturesMesh,
            NowGlassQualityScope glassQualityScope)
        {
            _drawList = drawList;
            _positionOffset = positionOffset;
            _capturesMesh = capturesMesh;
            _glassQualityScope = glassQualityScope;
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

            try
            {
                drawList?.EndScope(_positionOffset, _capturesMesh);
            }
            finally
            {
                _glassQualityScope.Dispose();
            }
        }

        internal void Cancel()
        {
            if (_disposed)
                return;

            var drawList = _drawList;
            _drawList = null;
            _disposed = true;

            try
            {
                drawList?.CancelScope(_capturesMesh);
            }
            finally
            {
                _glassQualityScope.Dispose();
            }
        }
    }
}
