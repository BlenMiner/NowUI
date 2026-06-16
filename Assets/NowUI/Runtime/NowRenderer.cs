using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    public sealed class NowRenderer : IDisposable
    {
        readonly NowDrawList _drawList = new NowDrawList();

        CommandBuffer _commandBuffer = new()
        {
            name = "Now Renderer"
        };

        static readonly int _selfReplaySourceId = Shader.PropertyToID("_NowGlassSelfReplaySource");

        static readonly int _selfReplayBlurredId = Shader.PropertyToID("_NowGlassSelfReplayBlurred");

        public NowGlassBlurQuality glassBlurQuality { get; set; } = NowGlassBlurQuality.Auto;

        public Mesh mesh => _drawList.mesh;

        public Vector2 size => _drawList.size;

        public int batchCount => _drawList.batchCount;

        public bool hasGeometry => _drawList.hasGeometry;

        /// <summary>
        /// Runs a representative draw once to load materials and grow internal
        /// buffers, then clears the resulting geometry. Use this before the first
        /// measured frame when steady-state rendering must be allocation-free.
        /// </summary>
        public void Warmup(Vector2 size, Action draw)
        {
            ThrowIfDisposed();
            _drawList.Warmup(size, draw);
        }

        public void Warmup(RenderTexture target, Action draw)
        {
            ThrowIfDisposed();

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            Warmup(new Vector2(target.width, target.height), draw);
        }

        public NowDrawScope Begin(Vector2 size)
        {
            ThrowIfDisposed();
            return _drawList.Begin(size, glassBlurQuality);
        }

        public NowDrawScope Begin(RenderTexture target)
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

        public void Draw(CommandBuffer commandBuffer, RenderTargetIdentifier target)
        {
            ThrowIfDisposed();
            Draw(commandBuffer, _drawList, target);
        }

        public void Draw(CommandBuffer commandBuffer, RenderTargetIdentifier target, int targetWidth, int targetHeight)
        {
            ThrowIfDisposed();
            Draw(commandBuffer, _drawList, target, targetWidth, targetHeight);
        }

        public static void Draw(CommandBuffer commandBuffer, NowDrawList drawList)
        {
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));

            if (drawList == null)
                throw new ArgumentNullException(nameof(drawList));

            if (!drawList.hasGeometry)
                return;

            int targetWidth = Mathf.RoundToInt(Now.UiUnitsToScreenPixels(drawList.size.x));
            int targetHeight = Mathf.RoundToInt(Now.UiUnitsToScreenPixels(drawList.size.y));
            DrawBatches(
                commandBuffer,
                drawList.mesh,
                drawList.batches,
                drawList.size,
                default,
                0,
                int.MaxValue,
                true,
                BuiltinRenderTextureType.CameraTarget,
                targetWidth,
                targetHeight);
        }

        public static void Draw(CommandBuffer commandBuffer, NowDrawList drawList, RenderTargetIdentifier target)
        {
            Draw(
                commandBuffer,
                drawList,
                target,
                Mathf.RoundToInt(Now.UiUnitsToScreenPixels(drawList != null ? drawList.size.x : 0f)),
                Mathf.RoundToInt(Now.UiUnitsToScreenPixels(drawList != null ? drawList.size.y : 0f)));
        }

        public static void Draw(
            CommandBuffer commandBuffer,
            NowDrawList drawList,
            RenderTargetIdentifier target,
            int targetWidth,
            int targetHeight)
        {
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));

            if (drawList == null)
                throw new ArgumentNullException(nameof(drawList));

            if (!drawList.hasGeometry)
                return;

            commandBuffer.SetRenderTarget(target);
            DrawBatches(
                commandBuffer,
                drawList.mesh,
                drawList.batches,
                drawList.size,
                new NowRenderTargetContext(target, targetWidth, targetHeight),
                0,
                int.MaxValue,
                false,
                default,
                0,
                0);
        }

        internal static void DrawRange(
            CommandBuffer commandBuffer,
            Mesh mesh,
            System.Collections.Generic.IReadOnlyList<NowUI.Internal.NowMeshBatch> batches,
            Vector2 size,
            RenderTargetIdentifier target,
            int targetWidth,
            int targetHeight,
            int start,
            int count)
        {
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));

            if (mesh == null || batches == null || batches.Count == 0 || mesh.vertexCount == 0 || count <= 0)
                return;

            commandBuffer.SetRenderTarget(target);
            DrawBatches(
                commandBuffer,
                mesh,
                batches,
                size,
                new NowRenderTargetContext(target, targetWidth, targetHeight),
                start,
                count,
                false,
                default,
                0,
                0);
        }

        internal static void DrawRangeTransformed(
            CommandBuffer commandBuffer,
            Mesh mesh,
            System.Collections.Generic.IReadOnlyList<NowUI.Internal.NowMeshBatch> batches,
            Vector2 projectionSize,
            RenderTargetIdentifier target,
            int start,
            int count,
            Matrix4x4 drawMatrix)
        {
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));

            if (mesh == null || batches == null || batches.Count == 0 || mesh.vertexCount == 0 || count <= 0)
                return;

            commandBuffer.SetRenderTarget(target);
            DrawBatchesTransformed(
                commandBuffer,
                mesh,
                batches,
                projectionSize,
                default,
                start,
                count,
                drawMatrix,
                false,
                default,
                0,
                0);
        }

        static void DrawBatches(
            CommandBuffer commandBuffer,
            Mesh mesh,
            IReadOnlyList<NowMeshBatch> batches,
            Vector2 size,
            NowRenderTargetContext targetContext,
            int start,
            int count,
            bool allowSelfReplay,
            RenderTargetIdentifier selfReplayTarget,
            int selfReplayTargetWidth,
            int selfReplayTargetHeight)
        {
            DrawBatchesTransformed(
                commandBuffer,
                mesh,
                batches,
                size,
                targetContext,
                start,
                count,
                Matrix4x4.identity,
                allowSelfReplay,
                selfReplayTarget,
                selfReplayTargetWidth,
                selfReplayTargetHeight);
        }

        static void DrawBatchesTransformed(
            CommandBuffer commandBuffer,
            Mesh mesh,
            IReadOnlyList<NowMeshBatch> batches,
            Vector2 size,
            NowRenderTargetContext targetContext,
            int start,
            int count,
            Matrix4x4 drawMatrix,
            bool allowSelfReplay,
            RenderTargetIdentifier selfReplayTarget,
            int selfReplayTargetWidth,
            int selfReplayTargetHeight)
        {
            var projection = GetProjectionMatrix(size);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);

            int safeStart = Mathf.Max(0, start);
            long requestedEnd = (long)safeStart + count;
            int end = Mathf.Min(batches.Count, requestedEnd > int.MaxValue ? int.MaxValue : (int)requestedEnd);

            for (int i = safeStart; i < end; ++i)
            {
                var batch = batches[i];

                if (batch.material == null)
                    continue;

                if (NowGlassRenderer.CanDrawBackdrop(batch, targetContext))
                {
                    NowGlassRenderer.DrawBackdropGlass(
                        commandBuffer,
                        mesh,
                        projection,
                        batch.material,
                        i,
                        batch,
                        size,
                        targetContext);
                    commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                    continue;
                }

                if (batch.kind == NowMeshKind.Glass)
                {
                    if (!targetContext.isValid &&
                        allowSelfReplay &&
                        NowGlassRenderer.CanDrawSelfReplay(batch))
                    {
                        DrawSelfReplayGlass(
                            commandBuffer,
                            mesh,
                            batches,
                            projection,
                            batch.material,
                            i,
                            batch,
                            size,
                            Mathf.Max(1, selfReplayTargetWidth),
                            Mathf.Max(1, selfReplayTargetHeight),
                            selfReplayTarget,
                            safeStart,
                            drawMatrix);
                        commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                        continue;
                    }

                    NowGlassRenderer.DisableBackdrop(commandBuffer);
                    var quality = NowGlassRenderer.GetBatchQuality(batch);
                    NowGlassRenderer.RecordFallback(
                        "NowRenderer",
                        quality,
                        targetContext.isValid
                            ? NowGlassFallbackReason.MissingBlurMaterial
                            : NowGlassFallbackReason.MissingTargetContext,
                        batch.data.x,
                        batch.bounds);
                }

                commandBuffer.DrawMesh(mesh, drawMatrix, batch.material, i, 0);
            }
        }

        static void DrawSelfReplayGlass(
            CommandBuffer commandBuffer,
            Mesh mesh,
            IReadOnlyList<NowMeshBatch> batches,
            Matrix4x4 projection,
            Material material,
            int subMesh,
            in NowMeshBatch batch,
            Vector2 drawSize,
            int targetWidth,
            int targetHeight,
            RenderTargetIdentifier target,
            int replayStart,
            Matrix4x4 drawMatrix)
        {
            var quality = NowGlassRenderer.GetBatchQuality(batch);
            var capture = NowGlassRenderer.GetCaptureRect(batch.bounds, drawSize, targetWidth, targetHeight, batch.data.x);
            commandBuffer.GetTemporaryRT(_selfReplaySourceId, capture.width, capture.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            commandBuffer.GetTemporaryRT(_selfReplayBlurredId, capture.width, capture.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            commandBuffer.SetRenderTarget(_selfReplaySourceId);
            commandBuffer.ClearRenderTarget(true, true, Color.clear);

            DrawBatchesTransformed(
                commandBuffer,
                mesh,
                batches,
                new Vector2(capture.uiRect.width, capture.uiRect.height),
                default,
                replayStart,
                subMesh - replayStart,
                Matrix4x4.Translate(new Vector3(-capture.uiRect.x, capture.uiRect.y, 0f)),
                false,
                default,
                0,
                0);

            NowGlassRenderer.CopyAndBlurBackdrop(
                commandBuffer,
                _selfReplaySourceId,
                _selfReplayBlurredId,
                capture.width,
                capture.height,
                batch.data.x,
                quality,
                "NowRendererSelfReplay",
                capture.uiRect,
                out _);

            commandBuffer.SetRenderTarget(target);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            NowGlassRenderer.EnableBackdrop(commandBuffer, _selfReplayBlurredId, capture.backdropUvTransform);
            commandBuffer.DrawMesh(mesh, drawMatrix, material, subMesh, 0);
            NowGlassRenderer.DisableBackdrop(commandBuffer);
            commandBuffer.ReleaseTemporaryRT(_selfReplayBlurredId);
            commandBuffer.ReleaseTemporaryRT(_selfReplaySourceId);
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
            NowDrawList drawList,
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

            Draw(commandBuffer, drawList, target);
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
                throw new ObjectDisposedException(nameof(NowRenderer));
        }

        static Matrix4x4 GetProjectionMatrix(Vector2 size)
        {
            return Matrix4x4.Ortho(0, size.x, -size.y, 0, -1, 100);
        }
    }
}
