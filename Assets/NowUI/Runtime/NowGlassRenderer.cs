using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    internal readonly struct NowRenderTargetContext
    {
        public readonly RenderTargetIdentifier target;
        public readonly int width;
        public readonly int height;
        public readonly NowGlassTextureLayout layout;

        /// <summary>
        /// Maps logical target UVs into the backing texture allocation. This is
        /// identity for ordinary render textures and the RTHandle viewport scale
        /// for dynamically-scaled HDRP targets.
        /// </summary>
        public readonly Vector4 sourceScaleOffset;

        public readonly bool overrideViewport;

        public readonly bool isValid;

        public NowRenderTargetContext(RenderTargetIdentifier target, int width, int height)
            : this(target, width, height, default)
        {
        }

        public NowRenderTargetContext(
            RenderTargetIdentifier target,
            int width,
            int height,
            in NowGlassTextureLayout layout)
            : this(
                target,
                width,
                height,
                layout,
                new Vector4(1f, 1f, 0f, 0f),
                false)
        {
        }

        public NowRenderTargetContext(
            RenderTargetIdentifier target,
            int width,
            int height,
            in NowGlassTextureLayout layout,
            Vector4 sourceScaleOffset)
            : this(target, width, height, layout, sourceScaleOffset, true)
        {
        }

        public NowRenderTargetContext(
            RenderTargetIdentifier target,
            int width,
            int height,
            in NowGlassTextureLayout layout,
            Vector4 sourceScaleOffset,
            bool overrideViewport)
        {
            this.target = target;
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            this.layout = layout;
            this.sourceScaleOffset = sourceScaleOffset;
            this.overrideViewport = overrideViewport;
            isValid = width > 0 && height > 0;
        }

        public NowRenderTargetContext(
            RenderTargetIdentifier target,
            in RenderTextureDescriptor descriptor)
            : this(
                target,
                descriptor.width,
                descriptor.height,
                NowGlassTextureLayout.FromDescriptor(descriptor))
        {
        }

        public NowRenderTargetContext(
            RenderTargetIdentifier target,
            in RenderTextureDescriptor descriptor,
            Vector4 sourceScaleOffset)
            : this(
                target,
                descriptor.width,
                descriptor.height,
                NowGlassTextureLayout.FromDescriptor(descriptor),
                sourceScaleOffset)
        {
        }
    }

    internal readonly struct NowGlassBlurPlan
    {
        public readonly int downsample;

        public readonly int width;

        public readonly int height;

        public readonly int iterations;

        public readonly float step;

        public readonly NowGlassBlurQuality quality;

        public NowGlassBlurPlan(int downsample, int width, int height, int iterations, float step)
            : this(downsample, width, height, iterations, step, NowGlassBlurQuality.Balanced)
        {
        }

        public NowGlassBlurPlan(
            int downsample,
            int width,
            int height,
            int iterations,
            float step,
            NowGlassBlurQuality quality)
        {
            this.downsample = downsample;
            this.width = width;
            this.height = height;
            this.iterations = iterations;
            this.step = step;
            this.quality = quality;
        }
    }

    internal readonly struct NowGlassCaptureRect
    {
        public readonly NowRect uiRect;
        public readonly int x;
        public readonly int y;
        public readonly int width;
        public readonly int height;
        public readonly Vector4 sourceScaleOffset;
        public readonly Vector4 backdropUvTransform;
        public readonly bool isCropped;

        public NowGlassCaptureRect(
            NowRect uiRect,
            int x,
            int y,
            int width,
            int height,
            Vector4 sourceScaleOffset,
            Vector4 backdropUvTransform,
            bool isCropped)
        {
            this.uiRect = uiRect;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.sourceScaleOffset = sourceScaleOffset;
            this.backdropUvTransform = backdropUvTransform;
            this.isCropped = isCropped;
        }
    }

    internal static class NowGlassRenderer
    {
        // Matches the largest offset in the hidden bilinear-optimized blur kernel.
        const float MaxGaussianOffset = 7.3849121445f;

        static readonly int _blurDirectionId = Shader.PropertyToID("_NowBlurDirection");
        static readonly int _blurSourceTexId = Shader.PropertyToID("_NowBlurSourceTex");
        static readonly int _blurSourceArrayTexId = Shader.PropertyToID("_NowBlurSourceArrayTex");
        static readonly int _blurSourceMsaaTexId = Shader.PropertyToID("_NowBlurSourceMSAATex");
        static readonly int _blurSourceMsaaArrayTexId = Shader.PropertyToID("_NowBlurSourceMSAAArrayTex");
        static readonly int _blurSourceSliceId = Shader.PropertyToID("_NowBlurSourceSlice");
        static readonly int _blurSourceScaleOffsetId = Shader.PropertyToID("_NowBlurSourceScaleOffset");
        static readonly int _blurTexelSizeId = Shader.PropertyToID("_NowBlurTexelSize");
        static readonly int _backdropTexId = Shader.PropertyToID("_NowBackdropTex");
        static readonly int _backdropArrayTexId = Shader.PropertyToID("_NowBackdropArrayTex");
        static readonly int _sharpBackdropTexId = Shader.PropertyToID("_NowGlassSharpBackdropTex");
        static readonly int _sharpBackdropArrayTexId = Shader.PropertyToID("_NowGlassSharpBackdropArrayTex");
        static readonly int _backdropUvTransformId = Shader.PropertyToID("_NowBackdropUVTransform");
        static readonly int _useBackdropId = Shader.PropertyToID("_NowGlassUseBackdrop");
        static readonly int _useStereoBackdropId = Shader.PropertyToID("_NowGlassUseStereoBackdrop");
        static readonly int _backdropSliceCountId = Shader.PropertyToID("_NowGlassBackdropSliceCount");
        static readonly int _sourceId = Shader.PropertyToID("_NowGlassSource");
        static readonly int _scratchId = Shader.PropertyToID("_NowGlassScratch");

        static Material _blurMaterial;

        internal static bool CanDrawBackdrop(in NowMeshBatch batch, in NowRenderTargetContext context)
        {
            return batch.kind == NowUI.Internal.NowMeshKind.Glass &&
                context.isValid &&
                batch.material != null &&
                GetBlurMaterial() != null;
        }

        internal static bool CanDrawSelfReplay(in NowMeshBatch batch)
        {
            return batch.kind == NowUI.Internal.NowMeshKind.Glass &&
                batch.material != null &&
                GetBlurMaterial() != null;
        }

        internal static void DrawBackdropGlass(
            CommandBuffer commandBuffer,
            Mesh mesh,
            Matrix4x4 projection,
            Material material,
            int subMesh,
            in NowMeshBatch batch,
            Vector2 drawSize,
            in NowRenderTargetContext context)
        {
            var blurMaterial = GetBlurMaterial();

            if (commandBuffer == null || mesh == null || material == null || blurMaterial == null || !context.isValid)
                return;

            var quality = GetBatchQuality(batch);
            var capture = GetCaptureRect(batch.bounds, drawSize, context.width, context.height, batch.data.x);
            var plan = GetBlurPlan(batch.data.x, capture.width, capture.height, quality);
            var singleSampledLayout = context.layout.AsSingleSampled();

            GetTemporaryRT(commandBuffer, _sourceId, plan.width, plan.height, singleSampledLayout);
            commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
            CopySource(
                commandBuffer,
                context.target,
                _sourceId,
                context.width,
                context.height,
                blurMaterial,
                ComposeSourceScaleOffset(capture.sourceScaleOffset, context.sourceScaleOffset),
                context.layout);

            if (plan.iterations > 0)
            {
                GetTemporaryRT(commandBuffer, _scratchId, plan.width, plan.height, singleSampledLayout);

                for (int i = 0; i < plan.iterations; ++i)
                {
                    float passStep = plan.step;
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(passStep, 0f, 0f, 0f));
                    BlitBlur(commandBuffer, _sourceId, _scratchId, plan.width, plan.height, blurMaterial, singleSampledLayout);
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(0f, passStep, 0f, 0f));
                    BlitBlur(commandBuffer, _scratchId, _sourceId, plan.width, plan.height, blurMaterial, singleSampledLayout);
                }
            }

            SetRenderTarget(commandBuffer, context);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            if (context.layout.isArray)
            {
                commandBuffer.SetGlobalTexture(_backdropArrayTexId, _sourceId);
                commandBuffer.SetGlobalTexture(_sharpBackdropArrayTexId, _sourceId);
                commandBuffer.SetGlobalTexture(_backdropTexId, Texture2D.blackTexture);
                commandBuffer.SetGlobalTexture(_sharpBackdropTexId, Texture2D.blackTexture);
                commandBuffer.SetGlobalFloat(_useStereoBackdropId, 1f);
                commandBuffer.SetGlobalFloat(_backdropSliceCountId, context.layout.sliceCount);
            }
            else
            {
                commandBuffer.SetGlobalTexture(_backdropTexId, _sourceId);
                commandBuffer.SetGlobalTexture(_sharpBackdropTexId, _sourceId);
                commandBuffer.SetGlobalFloat(_useStereoBackdropId, 0f);
                commandBuffer.SetGlobalFloat(_backdropSliceCountId, 1f);
            }
            commandBuffer.SetGlobalVector(_backdropUvTransformId, capture.backdropUvTransform);
            commandBuffer.SetGlobalFloat(_useBackdropId, 1f);
            commandBuffer.DrawMesh(
                mesh,
                Matrix4x4.identity,
                material,
                subMesh,
                0,
                NowMaskShader.GetPropertyBlock(batch.maskState));
            commandBuffer.SetGlobalFloat(_useBackdropId, 0f);
            commandBuffer.SetGlobalFloat(_useStereoBackdropId, 0f);
            commandBuffer.SetGlobalFloat(_backdropSliceCountId, 1f);
            commandBuffer.SetGlobalVector(_backdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));

            if (plan.iterations > 0)
                commandBuffer.ReleaseTemporaryRT(_scratchId);

            commandBuffer.ReleaseTemporaryRT(_sourceId);

            RecordDiagnostics(
                "NowRenderer",
                quality,
                capture.isCropped ? NowGlassFallbackReason.None : NowGlassFallbackReason.FullTargetCapture,
                batch.data.x,
                capture,
                plan);
        }

        /// <summary>
        /// Blurs an already-captured backdrop at blur-plan resolution and draws
        /// the glass batch sampling that plan-sized result directly, so no
        /// capture-sized upsample target is allocated per pane.
        /// <paramref name="captureSourceScaleOffset"/> selects the capture
        /// region inside <paramref name="captureSource"/> in normalized UVs;
        /// pass (1, 1, 0, 0) when the source is already cropped to the capture.
        /// </summary>
        internal static void DrawGlassWithBlurredCapture(
            CommandBuffer commandBuffer,
            Mesh mesh,
            Matrix4x4 projection,
            Material material,
            int subMesh,
            in NowMeshBatch batch,
            in NowGlassCaptureRect capture,
            RenderTargetIdentifier captureSource,
            int captureSourceWidth,
            int captureSourceHeight,
            Vector4 captureSourceScaleOffset,
            RenderTargetIdentifier drawTarget,
            Matrix4x4 drawMatrix,
            string host)
        {
            var blurMaterial = GetBlurMaterial();

            if (commandBuffer == null || mesh == null || material == null || blurMaterial == null)
                return;

            var quality = GetBatchQuality(batch);
            var plan = GetBlurPlan(batch.data.x, capture.width, capture.height, quality);

            commandBuffer.GetTemporaryRT(_sourceId, plan.width, plan.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
            BlitBlur(commandBuffer, captureSource, _sourceId, captureSourceWidth, captureSourceHeight, blurMaterial, captureSourceScaleOffset);

            if (plan.iterations > 0)
            {
                commandBuffer.GetTemporaryRT(_scratchId, plan.width, plan.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

                for (int i = 0; i < plan.iterations; ++i)
                {
                    float passStep = plan.step;
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(passStep, 0f, 0f, 0f));
                    BlitBlur(commandBuffer, _sourceId, _scratchId, plan.width, plan.height, blurMaterial);
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(0f, passStep, 0f, 0f));
                    BlitBlur(commandBuffer, _scratchId, _sourceId, plan.width, plan.height, blurMaterial);
                }
            }

            commandBuffer.SetRenderTarget(drawTarget);
            commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            commandBuffer.SetGlobalTexture(_backdropTexId, _sourceId);
            commandBuffer.SetGlobalVector(_backdropUvTransformId, capture.backdropUvTransform);
            commandBuffer.SetGlobalFloat(_useBackdropId, 1f);
            commandBuffer.DrawMesh(
                mesh,
                drawMatrix,
                material,
                subMesh,
                0,
                NowMaskShader.GetPropertyBlock(batch.maskState));
            commandBuffer.SetGlobalFloat(_useBackdropId, 0f);
            commandBuffer.SetGlobalVector(_backdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));

            if (plan.iterations > 0)
                commandBuffer.ReleaseTemporaryRT(_scratchId);

            commandBuffer.ReleaseTemporaryRT(_sourceId);

            RecordDiagnostics(
                host,
                quality,
                NowGlassFallbackReason.None,
                batch.data.x,
                capture,
                plan);
        }

        internal static bool CopyAndBlurBackdrop(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            float radius)
        {
            return CopyAndBlurBackdrop(
                commandBuffer,
                source,
                destination,
                width,
                height,
                radius,
                NowGlassBlurQuality.Auto,
                "Glass",
                default,
                out _);
        }

        internal static bool CopyBackdropRegion(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Vector4 sourceScaleOffset)
        {
            return CopyBackdropRegion(
                commandBuffer,
                source,
                destination,
                sourceWidth,
                sourceHeight,
                sourceScaleOffset,
                default);
        }

        internal static bool CopyBackdropRegion(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Vector4 sourceScaleOffset,
            in NowGlassTextureLayout layout)
        {
            if (commandBuffer == null)
                return false;

            var blurMaterial = GetBlurMaterial();

            if (blurMaterial == null)
            {
                if (layout.sourceRequiresExplicitResolve)
                {
                    ClearCopyDestination(commandBuffer, destination, layout);
                    return false;
                }

                BlitCopyFallback(commandBuffer, source, destination, sourceScaleOffset, layout);
                return false;
            }

            commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
            CopySource(
                commandBuffer,
                source,
                destination,
                Mathf.Max(1, sourceWidth),
                Mathf.Max(1, sourceHeight),
                blurMaterial,
                sourceScaleOffset,
                layout);
            return true;
        }

        internal static bool CopyAndBlurBackdrop(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            float radius,
            NowGlassBlurQuality quality,
            string host,
            NowRect captureRect,
            out NowGlassBlurPlan plan)
        {
            return CopyAndBlurBackdrop(
                commandBuffer,
                source,
                destination,
                width,
                height,
                radius,
                quality,
                host,
                captureRect,
                default,
                out plan);
        }

        internal static bool CopyAndBlurBackdrop(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            float radius,
            NowGlassBlurQuality quality,
            string host,
            NowRect captureRect,
            in NowGlassTextureLayout layout,
            out NowGlassBlurPlan plan)
        {
            return CopyAndBlurBackdrop(
                commandBuffer,
                source,
                destination,
                width,
                height,
                radius,
                quality,
                host,
                captureRect,
                layout,
                new Vector4(1f, 1f, 0f, 0f),
                out plan);
        }

        internal static bool CopyAndBlurBackdrop(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int width,
            int height,
            float radius,
            NowGlassBlurQuality quality,
            string host,
            NowRect captureRect,
            in NowGlassTextureLayout layout,
            Vector4 sourceScaleOffset,
            out NowGlassBlurPlan plan)
        {
            quality = NowGlassSettings.Resolve(quality);
            plan = GetBlurPlan(radius, width, height, quality);

            if (commandBuffer == null || width <= 0 || height <= 0)
                return false;

            var blurMaterial = GetBlurMaterial();

            if (blurMaterial == null)
            {
                if (layout.sourceRequiresExplicitResolve)
                    ClearCopyDestination(commandBuffer, destination, layout);
                else
                    BlitCopyFallback(
                        commandBuffer,
                        source,
                        destination,
                        sourceScaleOffset,
                        layout);
                RecordDiagnostics(
                    host,
                    quality,
                    NowGlassFallbackReason.MissingBlurMaterial,
                    radius,
                    width,
                    height,
                    width,
                    height,
                    captureRect,
                    plan);
                return false;
            }

            if (plan.iterations == 0 && plan.downsample == 1)
            {
                commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
                CopySource(
                    commandBuffer,
                    source,
                    destination,
                    width,
                    height,
                    blurMaterial,
                    sourceScaleOffset,
                    layout);
                RecordDiagnostics(host, quality, NowGlassFallbackReason.None, radius, width, height, width, height, captureRect, plan);
                return true;
            }

            var singleSampledLayout = layout.AsSingleSampled();
            GetTemporaryRT(commandBuffer, _sourceId, plan.width, plan.height, singleSampledLayout);
            commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
            CopySource(
                commandBuffer,
                source,
                _sourceId,
                width,
                height,
                blurMaterial,
                sourceScaleOffset,
                layout);

            bool finalPassWritesDestination = plan.iterations > 0 && plan.downsample == 1;

            if (plan.iterations > 0)
            {
                GetTemporaryRT(commandBuffer, _scratchId, plan.width, plan.height, singleSampledLayout);

                for (int i = 0; i < plan.iterations; ++i)
                {
                    float passStep = plan.step;
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(passStep, 0f, 0f, 0f));
                    BlitBlur(commandBuffer, _sourceId, _scratchId, plan.width, plan.height, blurMaterial, singleSampledLayout);
                    commandBuffer.SetGlobalVector(_blurDirectionId, new Vector4(0f, passStep, 0f, 0f));

                    if (finalPassWritesDestination && i == plan.iterations - 1)
                        BlitBlur(commandBuffer, _scratchId, destination, plan.width, plan.height, blurMaterial, singleSampledLayout);
                    else
                        BlitBlur(commandBuffer, _scratchId, _sourceId, plan.width, plan.height, blurMaterial, singleSampledLayout);
                }

                commandBuffer.ReleaseTemporaryRT(_scratchId);
            }

            if (!finalPassWritesDestination)
            {
                commandBuffer.SetGlobalVector(_blurDirectionId, Vector4.zero);
                BlitBlur(commandBuffer, _sourceId, destination, plan.width, plan.height, blurMaterial, singleSampledLayout);
            }

            commandBuffer.ReleaseTemporaryRT(_sourceId);
            RecordDiagnostics(host, quality, NowGlassFallbackReason.None, radius, width, height, plan.width, plan.height, captureRect, plan);
            return true;
        }

        internal static void DisableBackdrop(CommandBuffer commandBuffer)
        {
            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalFloat(_useBackdropId, 0f);
                commandBuffer.SetGlobalFloat(_useStereoBackdropId, 0f);
                commandBuffer.SetGlobalVector(_backdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));
            }
        }

        internal static void DisableBackdropGlobal()
        {
            Shader.SetGlobalFloat(_useBackdropId, 0f);
            Shader.SetGlobalFloat(_useStereoBackdropId, 0f);
            Shader.SetGlobalVector(_backdropUvTransformId, new Vector4(1f, 1f, 0f, 0f));
        }

        internal static void EnableBackdrop(CommandBuffer commandBuffer, RenderTargetIdentifier texture, Vector4 uvTransform)
        {
            if (commandBuffer == null)
                return;

            commandBuffer.SetGlobalTexture(_backdropTexId, texture);
            commandBuffer.SetGlobalVector(_backdropUvTransformId, uvTransform);
            commandBuffer.SetGlobalFloat(_useStereoBackdropId, 0f);
            commandBuffer.SetGlobalFloat(_useBackdropId, 1f);
        }

        internal static void EnableBackdropGlobal(Texture texture, Vector4 uvTransform)
        {
            Shader.SetGlobalTexture(_backdropTexId, texture);
            Shader.SetGlobalVector(_backdropUvTransformId, uvTransform);
            Shader.SetGlobalFloat(_useStereoBackdropId, 0f);
            Shader.SetGlobalFloat(_useBackdropId, texture != null ? 1f : 0f);
        }

        internal static NowGlassBlurPlan GetBlurPlan(float radius, int targetWidth, int targetHeight)
        {
            return GetBlurPlan(radius, targetWidth, targetHeight, NowGlassBlurQuality.Auto);
        }

        internal static NowGlassBlurPlan GetBlurPlan(
            float radius,
            int targetWidth,
            int targetHeight,
            NowGlassBlurQuality quality)
        {
            radius = Mathf.Max(0f, radius);
            quality = NowGlassSettings.Resolve(quality);

            if (radius < 0.25f)
            {
                return new NowGlassBlurPlan(
                    1,
                    Mathf.Max(1, targetWidth),
                    Mathf.Max(1, targetHeight),
                    0,
                    0f,
                    quality);
            }

            int downsample = quality switch
            {
                NowGlassBlurQuality.Fast => radius >= 18f ? 2 : 1,
                NowGlassBlurQuality.High => radius >= 48f ? 2 : 1,
                NowGlassBlurQuality.Ultra => radius >= 72f ? 2 : 1,
                _ => radius >= 28f ? 2 : 1
            };

            int width = Mathf.Max(1, targetWidth / downsample);
            int height = Mathf.Max(1, targetHeight / downsample);
            int iterations = quality switch
            {
                NowGlassBlurQuality.Fast => Mathf.Clamp(Mathf.CeilToInt(radius / 6f), 1, 4),
                NowGlassBlurQuality.High => Mathf.Clamp(Mathf.CeilToInt(radius / 3.5f), 2, 10),
                NowGlassBlurQuality.Ultra => Mathf.Clamp(Mathf.CeilToInt(radius / 3f), 3, 14),
                _ => Mathf.Clamp(Mathf.CeilToInt(radius / 4f), 2, 8)
            };

            float denominator = Mathf.Sqrt(iterations) * downsample * MaxGaussianOffset;
            float step = denominator > 0f ? radius / denominator : 0f;

            return new NowGlassBlurPlan(downsample, width, height, iterations, step, quality);
        }

        internal static NowGlassBlurQuality GetBatchQuality(in NowMeshBatch batch)
        {
            return NowGlassSettings.Resolve((NowGlassBlurQuality)Mathf.RoundToInt(batch.data.w));
        }

        internal static NowGlassCaptureRect GetCaptureRect(
            NowRect bounds,
            Vector2 drawSize,
            int targetWidth,
            int targetHeight,
            float blurRadius)
        {
            targetWidth = Mathf.Max(1, targetWidth);
            targetHeight = Mathf.Max(1, targetHeight);

            var fullRect = new NowRect(0f, 0f, Mathf.Max(1f, drawSize.x), Mathf.Max(1f, drawSize.y));

            if (bounds.isEmpty || drawSize.x <= 0f || drawSize.y <= 0f)
                return FullCaptureRect(fullRect, targetWidth, targetHeight);

            float pad = Mathf.Ceil(Mathf.Max(0f, blurRadius) + 2f);
            var capture = bounds.Outset(pad).Intersect(fullRect);

            if (capture.isEmpty)
                return FullCaptureRect(fullRect, targetWidth, targetHeight);

            float scaleX = targetWidth / Mathf.Max(0.0001f, drawSize.x);
            float scaleY = targetHeight / Mathf.Max(0.0001f, drawSize.y);
            int left = Mathf.Clamp(Mathf.FloorToInt(capture.x * scaleX), 0, targetWidth);
            int top = Mathf.Clamp(Mathf.FloorToInt(capture.y * scaleY), 0, targetHeight);
            int right = Mathf.Clamp(Mathf.CeilToInt(capture.xMax * scaleX), left + 1, targetWidth);
            int bottom = Mathf.Clamp(Mathf.CeilToInt(capture.yMax * scaleY), top + 1, targetHeight);
            int width = Mathf.Max(1, right - left);
            int height = Mathf.Max(1, bottom - top);

            if (left == 0 && top == 0 && width == targetWidth && height == targetHeight)
                return FullCaptureRect(fullRect, targetWidth, targetHeight);

            int sourceBottom = targetHeight - bottom;
            var sourceScaleOffset = new Vector4(
                width / (float)targetWidth,
                height / (float)targetHeight,
                left / (float)targetWidth,
                sourceBottom / (float)targetHeight);
            var backdropUvTransform = new Vector4(
                targetWidth / (float)width,
                targetHeight / (float)height,
                -left / (float)width,
                -sourceBottom / (float)height);

            return new NowGlassCaptureRect(
                capture,
                left,
                top,
                width,
                height,
                sourceScaleOffset,
                backdropUvTransform,
                true);
        }

        internal static void RecordFallback(
            string host,
            NowGlassBlurQuality quality,
            NowGlassFallbackReason reason,
            float blurRadius,
            NowRect captureRect = default)
        {
            if (!NowGlassSettings.diagnosticsEnabled)
                return;

            NowGlassSettings.Record(new NowGlassDiagnosticEntry(
                host,
                quality,
                reason,
                blurRadius,
                0,
                0,
                0,
                0,
                captureRect,
                1,
                0,
                0f,
                0,
                0,
                0));
        }

        static void BlitBlur(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Material blurMaterial)
        {
            BlitBlur(
                commandBuffer,
                source,
                destination,
                sourceWidth,
                sourceHeight,
                blurMaterial,
                new Vector4(1f, 1f, 0f, 0f),
                default);
        }

        static void BlitBlur(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Material blurMaterial,
            in NowGlassTextureLayout layout)
        {
            BlitBlur(
                commandBuffer,
                source,
                destination,
                sourceWidth,
                sourceHeight,
                blurMaterial,
                new Vector4(1f, 1f, 0f, 0f),
                layout);
        }

        static void BlitBlur(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Material blurMaterial,
            Vector4 sourceScaleOffset)
        {
            BlitBlur(
                commandBuffer,
                source,
                destination,
                sourceWidth,
                sourceHeight,
                blurMaterial,
                sourceScaleOffset,
                default);
        }

        static void BlitBlur(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Material blurMaterial,
            Vector4 sourceScaleOffset,
            in NowGlassTextureLayout layout)
        {
            commandBuffer.SetGlobalVector(_blurSourceScaleOffsetId, sourceScaleOffset);
            commandBuffer.SetGlobalVector(
                _blurTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(1, sourceWidth),
                    1f / Mathf.Max(1, sourceHeight),
                    Mathf.Max(1, sourceWidth),
                    Mathf.Max(1, sourceHeight)));

            if (layout.isArray)
            {
                commandBuffer.SetGlobalTexture(_blurSourceArrayTexId, source);

                for (int slice = 0; slice < layout.sliceCount; ++slice)
                {
                    commandBuffer.SetGlobalFloat(_blurSourceSliceId, slice);
                    commandBuffer.SetRenderTarget(destination, 0, CubemapFace.Unknown, slice);
                    commandBuffer.DrawProcedural(
                        Matrix4x4.identity,
                        blurMaterial,
                        1,
                        MeshTopology.Triangles,
                        3,
                        1);
                }

                commandBuffer.SetGlobalFloat(_blurSourceSliceId, 0f);
                return;
            }

            commandBuffer.SetGlobalTexture(_blurSourceTexId, source);
            commandBuffer.Blit(source, destination, blurMaterial, 0);
        }

        static void CopySource(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            int sourceWidth,
            int sourceHeight,
            Material blurMaterial,
            Vector4 sourceScaleOffset,
            in NowGlassTextureLayout layout)
        {
            if (layout.sourceRequiresExplicitResolve)
            {
                if (layout.isArray)
                {
                    ResolveMultisampledArray(
                        commandBuffer,
                        source,
                        destination,
                        blurMaterial,
                        sourceScaleOffset,
                        layout);
                    return;
                }

                ResolveMultisampled2D(
                    commandBuffer,
                    source,
                    destination,
                    blurMaterial,
                    sourceScaleOffset);
                return;
            }

            BlitBlur(
                commandBuffer,
                source,
                destination,
                sourceWidth,
                sourceHeight,
                blurMaterial,
                sourceScaleOffset,
                layout);
        }

        static void ResolveMultisampled2D(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Material blurMaterial,
            Vector4 sourceScaleOffset)
        {
            commandBuffer.SetGlobalTexture(_blurSourceMsaaTexId, source);
            commandBuffer.SetGlobalVector(_blurSourceScaleOffsetId, sourceScaleOffset);
            commandBuffer.SetRenderTarget(destination);
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                blurMaterial,
                3,
                MeshTopology.Triangles,
                3,
                1);
        }

        static void ClearCopyDestination(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier destination,
            in NowGlassTextureLayout layout)
        {
            SetRenderTarget(commandBuffer, destination, layout.AsSingleSampled());
            commandBuffer.ClearRenderTarget(false, true, Color.clear);
        }

        static void ResolveMultisampledArray(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Material blurMaterial,
            Vector4 sourceScaleOffset,
            in NowGlassTextureLayout layout)
        {
            commandBuffer.SetGlobalTexture(_blurSourceMsaaArrayTexId, source);
            commandBuffer.SetGlobalVector(_blurSourceScaleOffsetId, sourceScaleOffset);

            for (int slice = 0; slice < layout.sliceCount; ++slice)
            {
                commandBuffer.SetGlobalFloat(_blurSourceSliceId, slice);
                commandBuffer.SetRenderTarget(destination, 0, CubemapFace.Unknown, slice);
                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    blurMaterial,
                    2,
                    MeshTopology.Triangles,
                    3,
                    1);
            }

            commandBuffer.SetGlobalFloat(_blurSourceSliceId, 0f);
        }

        static void BlitCopyFallback(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Vector4 sourceScaleOffset,
            in NowGlassTextureLayout layout)
        {
            var scale = new Vector2(sourceScaleOffset.x, sourceScaleOffset.y);
            var offset = new Vector2(sourceScaleOffset.z, sourceScaleOffset.w);

            if (layout.isArray)
            {
                for (int slice = 0; slice < layout.sliceCount; ++slice)
                    commandBuffer.Blit(source, destination, scale, offset, slice, slice);

                return;
            }

            commandBuffer.Blit(source, destination, scale, offset);
        }

        static void GetTemporaryRT(
            CommandBuffer commandBuffer,
            int id,
            int width,
            int height,
            in NowGlassTextureLayout layout)
        {
            var descriptor = NowGlassBackdropSurface.CreateDescriptor(width, height, layout);
            commandBuffer.GetTemporaryRT(id, descriptor, FilterMode.Bilinear);
        }

        internal static void SetRenderTarget(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier target,
            in NowGlassTextureLayout layout)
        {
            if (layout.isArray)
            {
                commandBuffer.SetRenderTarget(
                    target,
                    0,
                    CubemapFace.Unknown,
                    RenderTargetIdentifier.AllDepthSlices);
                return;
            }

            commandBuffer.SetRenderTarget(target);
        }

        internal static void SetRenderTarget(
            CommandBuffer commandBuffer,
            in NowRenderTargetContext context)
        {
            SetRenderTarget(commandBuffer, context.target, context.layout);

            if (context.overrideViewport)
                commandBuffer.SetViewport(new Rect(0f, 0f, context.width, context.height));
        }

        /// <summary>
        /// Composes a crop in logical target UVs with the logical target's
        /// location inside its backing allocation.
        /// </summary>
        internal static Vector4 ComposeSourceScaleOffset(
            Vector4 logicalRegion,
            Vector4 allocationScaleOffset)
        {
            return new Vector4(
                logicalRegion.x * allocationScaleOffset.x,
                logicalRegion.y * allocationScaleOffset.y,
                logicalRegion.z * allocationScaleOffset.x + allocationScaleOffset.z,
                logicalRegion.w * allocationScaleOffset.y + allocationScaleOffset.w);
        }

        static NowGlassCaptureRect FullCaptureRect(NowRect fullRect, int targetWidth, int targetHeight)
        {
            return new NowGlassCaptureRect(
                fullRect,
                0,
                0,
                Mathf.Max(1, targetWidth),
                Mathf.Max(1, targetHeight),
                new Vector4(1f, 1f, 0f, 0f),
                new Vector4(1f, 1f, 0f, 0f),
                false);
        }

        static void RecordDiagnostics(
            string host,
            NowGlassBlurQuality quality,
            NowGlassFallbackReason fallbackReason,
            float blurRadius,
            NowGlassCaptureRect capture,
            NowGlassBlurPlan plan)
        {
            RecordDiagnostics(
                host,
                quality,
                fallbackReason,
                blurRadius,
                capture.width,
                capture.height,
                plan.width,
                plan.height,
                capture.uiRect,
                plan);
        }

        static void RecordDiagnostics(
            string host,
            NowGlassBlurQuality quality,
            NowGlassFallbackReason fallbackReason,
            float blurRadius,
            int sourceWidth,
            int sourceHeight,
            int blurredWidth,
            int blurredHeight,
            NowRect captureRect,
            NowGlassBlurPlan plan)
        {
            if (!NowGlassSettings.diagnosticsEnabled)
                return;

            int copiedPixels = Mathf.Max(0, sourceWidth) * Mathf.Max(0, sourceHeight);
            int blurredPixels = Mathf.Max(0, plan.width) * Mathf.Max(0, plan.height);
            int blurPasses = plan.iterations * 2;

            NowGlassSettings.Record(new NowGlassDiagnosticEntry(
                host,
                quality,
                fallbackReason,
                blurRadius,
                sourceWidth,
                sourceHeight,
                blurredWidth,
                blurredHeight,
                captureRect,
                plan.downsample,
                plan.iterations,
                plan.step,
                copiedPixels,
                blurredPixels,
                blurPasses));
        }

        static Material GetBlurMaterial()
        {
            if (_blurMaterial == null)
                _blurMaterial = Now.LoadRequiredResource<Material>("NowUI/GlassBlurMaterial");

            return _blurMaterial;
        }
    }
}
