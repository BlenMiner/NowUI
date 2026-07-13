using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NowUI
{
    public readonly struct NowUGUIGlassDebugInfo
    {
        public readonly int batchIndex;

        public readonly int replayBatchCount;

        public readonly int width;

        public readonly int height;

        public readonly int buildId;

        public readonly int frame;

        public readonly float blurRadius;

        public readonly int blurDownsample;

        public readonly int blurIterations;

        public readonly float blurStep;

        public readonly NowGlassBlurQuality blurQuality;

        public readonly NowGlassFallbackReason fallbackReason;

        public readonly NowRect captureRect;

        public readonly bool hasExternalSource;

        public readonly bool hasSourceTexture;

        public readonly bool hasBlurredTexture;

        public readonly string materialName;

        internal NowUGUIGlassDebugInfo(
            int batchIndex,
            int replayBatchCount,
            int width,
            int height,
            int buildId,
            int frame,
            float blurRadius,
            int blurDownsample,
            int blurIterations,
            float blurStep,
            NowGlassBlurQuality blurQuality,
            NowGlassFallbackReason fallbackReason,
            NowRect captureRect,
            bool hasExternalSource,
            bool hasSourceTexture,
            bool hasBlurredTexture,
            string materialName)
        {
            this.batchIndex = batchIndex;
            this.replayBatchCount = replayBatchCount;
            this.width = width;
            this.height = height;
            this.buildId = buildId;
            this.frame = frame;
            this.blurRadius = blurRadius;
            this.blurDownsample = blurDownsample;
            this.blurIterations = blurIterations;
            this.blurStep = blurStep;
            this.blurQuality = blurQuality;
            this.fallbackReason = fallbackReason;
            this.captureRect = captureRect;
            this.hasExternalSource = hasExternalSource;
            this.hasSourceTexture = hasSourceTexture;
            this.hasBlurredTexture = hasBlurredTexture;
            this.materialName = materialName;
        }
    }

    [AddComponentMenu("NowUI/Now Graphic")]
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class NowGraphic : MaskableGraphic, ILayoutElement
    {
        static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");

        static readonly int _nowCanvasLayoutProp = Shader.PropertyToID("_NowCanvasLayout");

        static readonly int _nowUGUIBackdropTexProp = Shader.PropertyToID("_NowUGUIBackdropTex");

        static readonly int _nowUGUIBackdropSizeProp = Shader.PropertyToID("_NowUGUIBackdropSize");

        static readonly int _nowUGUIBackdropOriginProp = Shader.PropertyToID("_NowUGUIBackdropOrigin");

        static readonly int _nowGlassUseBackdropProp = Shader.PropertyToID("_NowUGUIGlassUseBackdrop");

        [Header("NowUI")]
        [SerializeField] bool _rebuildEveryFrame;

        [SerializeField, Tooltip("Rebuild automatically when pointer, keyboard, scroll, or navigation input changes for this graphic, or a control inside it requests a repaint. Keeps NowControls live inside retained UGUI without Rebuild Every Frame.")]
        bool _autoRebuildOnInteraction = true;

        [SerializeField, Tooltip("Withhold pointer input when UGUI elements draw above this graphic, so they occlude NowUI controls the same way this graphic's Raycast Target occludes UGUI beneath it.")]
        bool _respectUGUIRaycast = true;

        [SerializeField, Tooltip("Report the measured NowLayout content extent as this graphic's preferred size, so UGUI LayoutGroups and ContentSizeFitters size it like any other layout element. Layout queries run a passive NowLayout measure pass before geometry rebuilds.")]
        bool _driveLayoutSize;

        [SerializeField, Tooltip("Allow interactive input (clicks, typing, focus) while not in Play Mode. Off renders edit mode as a pure preview — the Input System keeps devices live in the editor, so without this gate a focused control keeps reacting to the keyboard outside Play Mode.")]
        bool _editModeInteraction;

        [SerializeField, HideInInspector]
        Texture _uguiBackdropSourceTexture;

        [SerializeField, HideInInspector]
        NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

        [NonSerialized] Vector2 _preferredSize;

        [NonSerialized] bool _layoutSizeDirty;

        [NonSerialized] int _scopeId;

        [NonSerialized] bool _measuringLayoutInput;

        [NonSerialized] bool _hasLayoutInputMeasurement;

        [NonSerialized] int _layoutInputMeasurementFrame;

        [NonSerialized] Vector2 _layoutInputMeasurementSize;

        [NonSerialized] NowInteractionRepaintTracker _repaintTracker;

        [NonSerialized] bool _insideGeometryRebuild;

        [NonSerialized] readonly List<CanvasRenderer> _extraCanvasRenderers = new List<CanvasRenderer>(2);

        [NonSerialized] bool _extraRendererStateValid;

        [NonSerialized] Vector2 _extraRendererPivot;

        [NonSerialized] bool _extraRendererCullTransparentMesh;

        [NonSerialized] readonly List<IMaterialModifier> _materialModifiers = new List<IMaterialModifier>(4);

        [NonSerialized] bool _materialModifiersDirty = true;

        [NonSerialized] readonly List<Material> _stencilBaseMaterials = new List<Material>(4);

        [NonSerialized] readonly List<Material> _stencilMaterials = new List<Material>(4);

        [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

        [NonSerialized] readonly List<UguiGlassBackdropEntry> _uguiGlassBackdrops = new List<UguiGlassBackdropEntry>(2);

        [NonSerialized] NowDrawList _drawList;

        [NonSerialized] CommandBuffer _uguiGlassCommandBuffer;

        [NonSerialized] int _uguiGlassBackdropBuildId;

        [NonSerialized] Material _rectangleMaterial;

        [NonSerialized] Material _textMaterialTemplate;

        [NonSerialized] Material _rgbaTextMaterialTemplate;

        [NonSerialized] NowRectTransformInputProvider _inputProvider;

        Rect _clipRect;

        Vector2 _clipSoftness;

        bool _validClipRect;

        sealed class UguiGlassBackdropEntry
        {
            public int batchIndex = -1;

            public int replayBatchCount;

            public RenderTexture source;

            public RenderTexture blurred;

            public Material material;

            public Material sourceMaterial;

            public int width;

            public int height;

            public int lastUsedFrame = -1;

            public int lastUpdatedFrame = -1;

            public float blurRadius;

            public int blurDownsample;

            public int blurIterations;

            public float blurStep;

            public NowGlassBlurQuality blurQuality;

            public NowGlassFallbackReason fallbackReason;

            public NowRect captureRect;

            public bool hasExternalSource;

            public string materialName;
        }

        public event Action<NowGraphic, NowRect> rebuildNowUI;

        public bool autoRebuildOnInteraction
        {
            get => _autoRebuildOnInteraction;
            set => _autoRebuildOnInteraction = value;
        }

        public bool rebuildEveryFrame
        {
            get => _rebuildEveryFrame;
            set
            {
                if (_rebuildEveryFrame == value)
                    return;

                _rebuildEveryFrame = value;
                SetVerticesDirty();
            }
        }

        public Texture uguiBackdropSourceTexture
        {
            get => _uguiBackdropSourceTexture;
            set
            {
                if (_uguiBackdropSourceTexture == value)
                    return;

                _uguiBackdropSourceTexture = value;
                SetVerticesDirty();
            }
        }

        public NowGlassBlurQuality glassBlurQuality
        {
            get => _glassBlurQuality;
            set
            {
                if (_glassBlurQuality == value)
                    return;

                _glassBlurQuality = value;
                SetVerticesDirty();
            }
        }

        protected virtual void DrawNowUI(NowRect rect)
        {
            rebuildNowUI?.Invoke(this, rect);
        }

        protected virtual Texture GetUGUIBackdropSourceTexture()
        {
            if (_uguiBackdropSourceTexture != null)
                return _uguiBackdropSourceTexture;

            var targetCanvas = canvas;
            var targetCamera = targetCanvas != null ? targetCanvas.worldCamera : null;
            return targetCamera != null ? targetCamera.targetTexture : null;
        }

        public int uguiGlassDebugTextureCount => _uguiGlassBackdrops.Count;

        public Texture GetUGUIGlassDebugSourceTexture(int index)
        {
            return index >= 0 && index < _uguiGlassBackdrops.Count
                ? _uguiGlassBackdrops[index].source
                : null;
        }

        public Texture GetUGUIGlassDebugBlurredTexture(int index)
        {
            return index >= 0 && index < _uguiGlassBackdrops.Count
                ? _uguiGlassBackdrops[index].blurred
                : null;
        }

        public bool TryGetUGUIGlassDebugInfo(int index, out NowUGUIGlassDebugInfo info)
        {
            if (index < 0 || index >= _uguiGlassBackdrops.Count)
            {
                info = default;
                return false;
            }

            var entry = _uguiGlassBackdrops[index];
            info = new NowUGUIGlassDebugInfo(
                entry.batchIndex,
                entry.replayBatchCount,
                entry.width,
                entry.height,
                entry.lastUsedFrame,
                entry.lastUpdatedFrame,
                entry.blurRadius,
                entry.blurDownsample,
                entry.blurIterations,
                entry.blurStep,
                entry.blurQuality,
                entry.fallbackReason,
                entry.captureRect,
                entry.hasExternalSource,
                entry.source != null,
                entry.blurred != null,
                entry.materialName);
            return true;
        }

        /// <summary>
        /// When true, each rebuild runs <see cref="DrawNowUI"/> twice —
        /// a NowLayout measure pass (draws suppressed, input passive) followed by
        /// the real pass — so flexible space, stretching and auto-sized groups are
        /// exact within a single rebuild, like Unity IMGUI's Layout and Repaint
        /// events. The base explicit-rect host is one-pass; derive from
        /// <see cref="NowLayoutGraphic"/> when the graphic uses NowLayout.
        /// </summary>
        internal virtual bool useLayoutMeasurePass => false;

        struct FrameContent : INowFrameContent
        {
            readonly NowGraphic _owner;

            public FrameContent(NowGraphic owner)
            {
                _owner = owner;
            }

            public void Draw(NowRect rect)
            {
                _owner.DrawNowUI(rect);
            }
        }

        public void MarkDirty()
        {
            SetVerticesDirty();
        }

        public override void SetVerticesDirty()
        {
            // From inside this graphic's own draw pass (a DrawNowUI handler
            // mutating state), dirtying is illegal mid-canvas-rebuild — UGUI
            // rejects registrations while rebuilding. Convert to a repaint
            // request; the frame tracker picks it up and LateUpdate schedules
            // the next rebuild.
            if (_insideGeometryRebuild)
            {
                NowControlState.RequestRepaint();
                return;
            }

            _hasLayoutInputMeasurement = false;
            base.SetVerticesDirty();

            if (_driveLayoutSize)
                SetLayoutDirty();
        }

        /// <summary>Number of canvas renderer pages currently in use (diagnostics).</summary>
        public int canvasPageCount => _drawList?.canvasPageCount ?? 0;

        /// <summary>
        /// The mesh uploaded to the given page's CanvasRenderer, or null (diagnostics;
        /// do not modify the returned mesh).
        /// </summary>
        public Mesh GetCanvasPageMesh(int pageIndex)
        {
            return _drawList?.GetCanvasMesh(pageIndex);
        }

        protected override void UpdateGeometry()
        {
            if (!IsActive())
                return;

            using var profile = NowProfiler.GraphicRebuild.Auto();
            EnsureDrawList();

            var rect = rectTransform.rect;

            if (rect.width <= 0 || rect.height <= 0)
            {
                _drawList.Clear();
                ReleaseUGUIGlassBackdrops();
                ApplyCanvasPages();
                return;
            }

            var positionOffset = new Vector2(rect.xMin, rect.yMax);
            var drawRect = new NowRect(0, 0, rect.width, rect.height);
            var frame = NowFrame.Begin(GetCanvasScaleFactor(), trackRepaint: true);
            NowDrawScope scope = default;
            bool colorMultiplierActive = false;
            bool passiveInputActive = false;
            bool interactive = Application.isPlaying || _editModeInteraction;
            _insideGeometryRebuild = true;

            try
            {
                _repaintTracker.SetWantsRepaint(false);
                scope = _drawList.Begin(
                    new Vector2(rect.width, rect.height),
                    positionOffset,
                    false,
                    _glassBlurQuality);

                Now.BeginColorMultiplier(color);
                colorMultiplierActive = true;

                var inputSurface = new NowInputSurface(new Vector2(rect.width, rect.height));

                using (NowOverlay.Host(this, rectTransform, GetEventCamera()))
                {
                    var inputScope = NowInput.Begin(interactive ? GetInputProvider() : null, inputSurface);

                    try
                    {
                        using (NowControls.RestoreIdScope(GetScopeId()))
                        {
                            // Edit-mode preview: no pointer (null provider) and a passive
                            // frame, so nothing focuses, types or transitions state.
                            if (!interactive)
                            {
                                NowInput.BeginPassive();
                                passiveInputActive = true;
                            }

                            _repaintTracker.StoreFrameInput(NowInput.current, inputSurface.size);

                            var content = new FrameContent(this);
                            Vector2 measured = NowFrame.DrawContent(
                                ref content,
                                drawRect,
                                useLayoutMeasurePass,
                                trackContent: _driveLayoutSize);

                            if (_driveLayoutSize && (measured - _preferredSize).sqrMagnitude > 0.25f)
                            {
                                _preferredSize = measured;
                                _layoutSizeDirty = true;
                            }
                        }
                    }
                    catch
                    {
                        // Roll back deferred overlays and captured geometry before
                        // input disposal gets a chance to flush the failed frame.
                        scope.Cancel();
                        throw;
                    }
                    finally
                    {
                        inputScope.Dispose();
                    }
                }

                if (passiveInputActive)
                {
                    NowInput.EndPassive();
                    passiveInputActive = false;
                }

                Now.EndColorMultiplier();
                colorMultiplierActive = false;
                _repaintTracker.SetWantsRepaint(frame.EndRepaintTracking());

                scope.Dispose();
            }
            catch (Exception ex)
            {
                if (passiveInputActive)
                {
                    NowInput.EndPassive();
                    passiveInputActive = false;
                }

                if (colorMultiplierActive)
                {
                    Now.EndColorMultiplier();
                    colorMultiplierActive = false;
                }

                scope.Cancel();
                _drawList.Clear();
                Debug.LogException(ex, this);
            }
            finally
            {
                _insideGeometryRebuild = false;
                frame.Dispose();
            }

            ApplyCanvasPages();
        }

        protected override void UpdateMaterial()
        {
            _materialModifiersDirty = true;
            ApplyCanvasPages();
        }

        /// <summary>
        /// Drops the cached <see cref="IMaterialModifier"/> component list so the
        /// next rebuild rescans this GameObject. Material rebuilds, enable, and
        /// canvas hierarchy changes rescan automatically; call this after adding or
        /// removing a modifier component at runtime without dirtying the material.
        /// </summary>
        public void InvalidateMaterialModifiers()
        {
            _materialModifiersDirty = true;
        }

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (m_ShouldRecalculateStencil)
            {
                ReleaseStencilMaterials();

                if (maskable)
                {
                    var rootCanvas = MaskUtilities.FindRootSortOverrideCanvas(transform);
                    m_StencilValue = MaskUtilities.GetStencilDepth(transform, rootCanvas);
                }
                else
                {
                    m_StencilValue = 0;
                }

                m_ShouldRecalculateStencil = false;
            }

            if (m_StencilValue <= 0 || isMaskingGraphic)
                return baseMaterial;

            return GetStencilMaterial(baseMaterial);
        }

        public override void RecalculateMasking()
        {
            ReleaseStencilMaterials();
            base.RecalculateMasking();
        }

        public override void Cull(Rect clipRect, bool validRect)
        {
            base.Cull(clipRect, validRect);
            ApplyCullToExtraCanvasRenderers();
        }

        public override void SetClipRect(Rect clipRect, bool validRect)
        {
            _clipRect = clipRect;
            _validClipRect = validRect;

            base.SetClipRect(clipRect, validRect);
            ApplyClippingToExtraCanvasRenderers();
        }

        public override void SetClipSoftness(Vector2 clipSoftness)
        {
            _clipSoftness = clipSoftness;

            base.SetClipSoftness(clipSoftness);
            ApplyClippingToExtraCanvasRenderers();
        }

        protected virtual void LateUpdate()
        {
            if (_layoutSizeDirty)
            {
                _layoutSizeDirty = false;
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            }

            if (_rebuildEveryFrame ||
                (_autoRebuildOnInteraction && ShouldRepaintForInteraction()))
            {
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Cheap input watcher so hosted controls get a rebuild when pointer,
        /// keyboard, scroll or navigation input changes, while idle hover stays
        /// retained.
        /// </summary>
        bool ShouldRepaintForInteraction()
        {
            if (_repaintTracker.wantsRepaint)
                return true;

            if (!Application.isPlaying && !_editModeInteraction)
                return false;

            var rect = rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
                return false;

            var surface = new NowInputSurface(new Vector2(rect.width, rect.height));
            return _repaintTracker.ShouldRepaint(GetInputProvider(), surface);
        }

        protected override void OnEnable()
        {
            _materialModifiersDirty = true;
            base.OnEnable();
            _repaintTracker.Reset();
            EnsureCanvasChannels();
        }

        protected override void OnDisable()
        {
            _repaintTracker.Reset();
            ReleaseStencilMaterials();
            ReleaseUGUIGlassBackdrops();
            ClearCanvasRenderer(canvasRenderer);
            ClearExtraCanvasRenderers();
            base.OnDisable();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            _materialModifiersDirty = true;
            base.OnCanvasHierarchyChanged();
            EnsureCanvasChannels();
        }

        protected override void OnDestroy()
        {
            ReleaseStencilMaterials();
            ReleaseUGUIGlassBackdrops();

            if (_uguiGlassCommandBuffer != null)
            {
                _uguiGlassCommandBuffer.Release();
                _uguiGlassCommandBuffer = null;
            }

            if (_drawList != null)
            {
                _drawList.Dispose();
                _drawList = null;
            }

            DestroyExtraCanvasRenderers();

            foreach (var mat in _textMaterials.Values)
            {
                if (mat == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(mat);
                else
                    DestroyImmediate(mat);
            }

            _textMaterials.Clear();
            base.OnDestroy();
        }

    #if UNITY_EDITOR
        protected override void OnValidate()
        {
            _materialModifiersDirty = true;
            base.OnValidate();
            EnsureCanvasChannels();
            SetVerticesDirty();
        }
    #endif

        void EnsureDrawList()
        {
            if (_drawList != null)
                return;

            _drawList = new NowDrawList(NowMeshLayout.Canvas, "Now Graphic Mesh");
        }

        public bool respectUGUIRaycast
        {
            get => _respectUGUIRaycast;
            set => _respectUGUIRaycast = value;
        }

        /// <summary>
        /// When true, the measured NowLayout content extent is
        /// reported as this graphic's preferred size, so LayoutGroups and
        /// ContentSizeFitters size it like any other layout element. Unity layout
        /// queries run a passive NowLayout measure pass before geometry rebuilds.
        /// </summary>
        public bool driveLayoutSize
        {
            get => _driveLayoutSize;
            set
            {
                if (_driveLayoutSize == value)
                    return;

                _driveLayoutSize = value;
                _hasLayoutInputMeasurement = false;
                SetLayoutDirty();
            }
        }

        /// <summary>The last measured content extent (origin + content of the root layout areas).</summary>
        public Vector2 measuredContentSize => _preferredSize;

        /// <summary>
        /// Resolves a SetId value inside this host for external focus,
        /// navigation, state, or layout-cache APIs.
        /// </summary>
        public int ResolveControlId(string id)
        {
            return NowControls.ResolveHostControlId(GetScopeId(), id);
        }

        public int ResolveControlId(int id)
        {
            return NowControls.ResolveHostControlId(GetScopeId(), id);
        }

        public virtual void CalculateLayoutInputHorizontal()
        {
            RefreshPreferredLayoutSize();
        }

        public virtual void CalculateLayoutInputVertical()
        {
            RefreshPreferredLayoutSize();
        }

        public virtual float minWidth => -1f;

        public virtual float preferredWidth => _driveLayoutSize ? _preferredSize.x : -1f;

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => -1f;

        public virtual float preferredHeight => _driveLayoutSize ? _preferredSize.y : -1f;

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 0;

        void RefreshPreferredLayoutSize()
        {
            if (!_driveLayoutSize || _measuringLayoutInput || !IsActive())
                return;

            var rect = rectTransform.rect;
            var size = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
            int frame = Time.frameCount;

            if (_hasLayoutInputMeasurement &&
                _layoutInputMeasurementFrame == frame &&
                (_layoutInputMeasurementSize - size).sqrMagnitude <= 0.25f)
            {
                return;
            }

            var frameScope = NowFrame.Begin(GetCanvasScaleFactor());
            bool colorMultiplierActive = false;
            _measuringLayoutInput = true;

            try
            {
                Now.BeginColorMultiplier(color);
                colorMultiplierActive = true;

                using (NowInput.BeginMeasurement(GetInputProvider(), new NowInputSurface(size)))
                using (NowControls.RestoreIdScope(GetScopeId()))
                {
                    var content = new FrameContent(this);
                    Vector2 measured = NowFrame.MeasureContent(
                        ref content,
                        new NowRect(0f, 0f, size.x, size.y));

                    if ((_preferredSize - measured).sqrMagnitude > 0.25f)
                        _preferredSize = measured;
                }

                _layoutInputMeasurementFrame = frame;
                _layoutInputMeasurementSize = size;
                _hasLayoutInputMeasurement = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                if (colorMultiplierActive)
                    Now.EndColorMultiplier();

                frameScope.Dispose();
                _measuringLayoutInput = false;
            }
        }

        int GetScopeId()
        {
            if (_scopeId == 0)
                _scopeId = NowControls.AllocateHostScopeId();

            return _scopeId;
        }

        protected virtual INowInputProvider GetInputProvider()
        {
            if (_inputProvider == null)
                _inputProvider = new NowRectTransformInputProvider();

            _inputProvider.rectTransform = rectTransform;
            _inputProvider.eventCamera = GetEventCamera();
            _inputProvider.raycastGate = _respectUGUIRaycast ? this : null;
            return _inputProvider;
        }

        Camera GetEventCamera()
        {
            var targetCanvas = canvas;

            if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return targetCanvas.worldCamera;
        }

        float GetCanvasScaleFactor()
        {
            var targetCanvas = canvas;

            if (targetCanvas == null)
                return 1f;

            float scale = targetCanvas.scaleFactor;
            return scale > 0f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
        }

        void ApplyCanvasPages()
        {
            using var profile = NowProfiler.ApplyCanvasPages.Auto();
            PruneDestroyedExtraCanvasRenderers();

            if (_drawList == null)
            {
                ClearCanvasRenderer(canvasRenderer);
                ClearExtraCanvasRenderers();
                ReleaseUGUIGlassBackdrops();
                return;
            }

            UpdateUGUIGlassBackdrops();

            if (_materialModifiersDirty)
            {
                _materialModifiers.Clear();
                GetComponents(_materialModifiers);
                _materialModifiersDirty = false;
            }

            int batchOffset = 0;
            var firstPageBatches = _drawList.GetCanvasBatches(0);
            ApplyCanvasPage(canvasRenderer, _drawList.GetCanvasMesh(0), firstPageBatches, batchOffset);
            batchOffset += firstPageBatches?.Count ?? 0;

            int extraPageCount = Mathf.Max(0, _drawList.canvasPageCount - 1);
            EnsureExtraCanvasRendererCount(extraPageCount);

            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
            {
                var crenderer = _extraCanvasRenderers[i];

                if (crenderer == null)
                    continue;

                if (i >= extraPageCount)
                {
                    ClearCanvasRenderer(crenderer);

                    if (crenderer.gameObject.activeSelf)
                        crenderer.gameObject.SetActive(false);

                    continue;
                }

                if (!crenderer.gameObject.activeSelf)
                    crenderer.gameObject.SetActive(true);

                var batches = _drawList.GetCanvasBatches(i + 1);
                ApplyCanvasPage(crenderer, _drawList.GetCanvasMesh(i + 1), batches, batchOffset);
                batchOffset += batches?.Count ?? 0;
            }
        }

        void ApplyCanvasPage(CanvasRenderer crenderer, Mesh mesh, List<NowMeshBatch> batches, int batchOffset)
        {
            if (crenderer == null)
                return;

            if (mesh == null || batches == null || batches.Count == 0 || mesh.vertexCount == 0)
            {
                ClearCanvasRenderer(crenderer);
                return;
            }

            crenderer.SetMesh(mesh);
            crenderer.materialCount = batches.Count;

            for (int i = 0; i < batches.Count; ++i)
                crenderer.SetMaterial(GetCanvasMaterialForRendering(batches[i], batchOffset + i), i);

            if (crenderer != canvasRenderer)
                ApplyRendererMaskState(crenderer);
        }

        static void ClearCanvasRenderer(CanvasRenderer crenderer)
        {
            if (crenderer == null)
                return;

            crenderer.Clear();
            crenderer.materialCount = 0;
        }

        void ClearExtraCanvasRenderers()
        {
            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
                ClearCanvasRenderer(_extraCanvasRenderers[i]);
        }

        void PruneDestroyedExtraCanvasRenderers()
        {
            for (int i = _extraCanvasRenderers.Count - 1; i >= 0; --i)
            {
                if (_extraCanvasRenderers[i] == null)
                    _extraCanvasRenderers.RemoveAt(i);
            }
        }

        void EnsureExtraCanvasRendererCount(int count)
        {
            PruneDestroyedExtraCanvasRenderers();

            Vector2 pivot = rectTransform.pivot;
            bool cullTransparentMesh = canvasRenderer.cullTransparentMesh;

            while (_extraCanvasRenderers.Count < count)
            {
                int pageIndex = _extraCanvasRenderers.Count + 1;
                var go = new GameObject($"Now Graphic Renderer {pageIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                go.transform.SetParent(transform, false);
                var crenderer = go.GetComponent<CanvasRenderer>();
                ConfigureExtraCanvasRenderer(crenderer, _extraCanvasRenderers.Count, pivot, cullTransparentMesh);
                _extraCanvasRenderers.Add(crenderer);
            }

            if (_extraRendererStateValid &&
                _extraRendererPivot == pivot &&
                _extraRendererCullTransparentMesh == cullTransparentMesh)
            {
                return;
            }

            _extraRendererStateValid = true;
            _extraRendererPivot = pivot;
            _extraRendererCullTransparentMesh = cullTransparentMesh;

            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
                ConfigureExtraCanvasRenderer(_extraCanvasRenderers[i], i, pivot, cullTransparentMesh);
        }

        void ConfigureExtraCanvasRenderer(CanvasRenderer crenderer, int siblingIndex, Vector2 pivot, bool cullTransparentMesh)
        {
            if (crenderer == null)
                return;

            var childTransform = crenderer.transform as RectTransform;

            if (childTransform == null)
                return;

            childTransform.SetSiblingIndex(siblingIndex);
            childTransform.anchorMin = Vector2.zero;
            childTransform.anchorMax = Vector2.one;
            childTransform.pivot = pivot;
            childTransform.offsetMin = Vector2.zero;
            childTransform.offsetMax = Vector2.zero;
            childTransform.localScale = Vector3.one;
            childTransform.localRotation = Quaternion.identity;
            crenderer.cullTransparentMesh = cullTransparentMesh;
            ApplyRendererMaskState(crenderer);
        }

        void ApplyCullToExtraCanvasRenderers()
        {
            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
            {
                var crenderer = _extraCanvasRenderers[i];

                if (crenderer != null)
                    crenderer.cull = canvasRenderer.cull;
            }
        }

        void ApplyClippingToExtraCanvasRenderers()
        {
            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
                ApplyRendererMaskState(_extraCanvasRenderers[i]);
        }

        void ApplyRendererMaskState(CanvasRenderer crenderer)
        {
            if (crenderer == null)
                return;

            crenderer.cull = canvasRenderer.cull;
            crenderer.clippingSoftness = _clipSoftness;

            if (_validClipRect)
                crenderer.EnableRectClipping(_clipRect);
            else
                crenderer.DisableRectClipping();
        }

        void UpdateUGUIGlassBackdrops()
        {
            if (_drawList == null ||
                !_drawList.hasRenderReplay ||
                _drawList.renderReplayMesh == null ||
                _drawList.renderReplayBatches == null ||
                _drawList.renderReplayBatches.Count == 0)
            {
                ReleaseUGUIGlassBackdrops();
                return;
            }

            var replayBatches = _drawList.renderReplayBatches;
            bool hasGlass = false;

            for (int i = 0; i < replayBatches.Count; ++i)
            {
                if (replayBatches[i].kind == NowMeshKind.Glass)
                {
                    hasGlass = true;
                    break;
                }
            }

            if (!hasGlass)
            {
                ReleaseUGUIGlassBackdrops();
                return;
            }

            float scale = GetCanvasScaleFactor();
            int fullWidth = Mathf.Max(1, Mathf.CeilToInt(_drawList.size.x * scale));
            int fullHeight = Mathf.Max(1, Mathf.CeilToInt(_drawList.size.y * scale));
            int buildId = ++_uguiGlassBackdropBuildId;
            Texture sourceTexture = GetUGUIBackdropSourceTexture();

            if (_uguiGlassCommandBuffer == null)
            {
                _uguiGlassCommandBuffer = new CommandBuffer
                {
                    name = "Now UGUI Glass Backdrop"
                };
            }

            for (int i = 0; i < replayBatches.Count; ++i)
            {
                var batch = replayBatches[i];

                if (batch.kind != NowMeshKind.Glass)
                    continue;

                var baseMaterial = GetCanvasMaterial(batch);

                if (baseMaterial == null)
                    continue;

                var quality = NowGlassRenderer.GetBatchQuality(batch);

                var capture = NowGlassRenderer.GetCaptureRect(
                    batch.bounds,
                    _drawList.size,
                    fullWidth,
                    fullHeight,
                    batch.data.x);
                var entry = GetUGUIGlassBackdropEntry(i);
                entry.lastUsedFrame = buildId;
                entry.lastUpdatedFrame = Time.frameCount;
                entry.replayBatchCount = i;
                entry.blurRadius = Mathf.Max(0f, batch.data.x) * scale;
                entry.blurQuality = quality;
                entry.captureRect = capture.uiRect;
                var blurPlan = NowGlassRenderer.GetBlurPlan(entry.blurRadius, capture.width, capture.height, quality);
                entry.blurDownsample = blurPlan.downsample;
                entry.blurIterations = blurPlan.iterations;
                entry.blurStep = blurPlan.step;
                entry.fallbackReason = capture.isCropped ? NowGlassFallbackReason.None : NowGlassFallbackReason.FullTargetCapture;
                entry.hasExternalSource = sourceTexture != null;

                if (entry.materialName == null || !ReferenceEquals(entry.sourceMaterial, baseMaterial))
                    entry.materialName = baseMaterial.name;

                EnsureUGUIGlassTextures(entry, capture.width, capture.height);
                EnsureUGUIGlassMaterial(entry, baseMaterial);

                _uguiGlassCommandBuffer.Clear();
                _uguiGlassCommandBuffer.SetRenderTarget(entry.source);

                if (sourceTexture != null)
                {
                    NowGlassRenderer.CopyBackdropRegion(
                        _uguiGlassCommandBuffer,
                        sourceTexture,
                        entry.source,
                        Mathf.Max(1, sourceTexture.width),
                        Mathf.Max(1, sourceTexture.height),
                        capture.sourceScaleOffset);
                }
                else
                {
                    _uguiGlassCommandBuffer.ClearRenderTarget(true, true, Color.clear);
                }

                NowRenderer.DrawRangeTransformed(
                    _uguiGlassCommandBuffer,
                    _drawList.renderReplayMesh,
                    replayBatches,
                    new Vector2(capture.uiRect.width, capture.uiRect.height),
                    entry.source,
                    0,
                    i,
                    Matrix4x4.Translate(new Vector3(-capture.uiRect.x, capture.uiRect.y, 0f)));

                bool blurred = NowGlassRenderer.CopyAndBlurBackdrop(
                    _uguiGlassCommandBuffer,
                    entry.source,
                    entry.blurred,
                    entry.width,
                    entry.height,
                    entry.blurRadius,
                    quality,
                    "UGUI",
                    entry.captureRect,
                    out blurPlan);
                if (!blurred)
                    entry.fallbackReason = NowGlassFallbackReason.MissingBlurMaterial;

                entry.blurDownsample = blurPlan.downsample;
                entry.blurIterations = blurPlan.iterations;
                entry.blurStep = blurPlan.step;

                Graphics.ExecuteCommandBuffer(_uguiGlassCommandBuffer);

                entry.material.CopyPropertiesFromMaterial(baseMaterial);
                entry.material.mainTexture = entry.blurred;
                entry.material.SetTexture(_mainTexProp, entry.blurred);
                entry.material.SetTexture(_nowUGUIBackdropTexProp, entry.blurred);
                entry.material.SetVector(_nowUGUIBackdropSizeProp, new Vector4(entry.captureRect.width, entry.captureRect.height, 0f, 0f));
                entry.material.SetVector(_nowUGUIBackdropOriginProp, new Vector4(entry.captureRect.x, entry.captureRect.y, 0f, 0f));
                entry.material.SetFloat(_nowGlassUseBackdropProp, 1f);
            }

            for (int i = _uguiGlassBackdrops.Count - 1; i >= 0; --i)
            {
                if (_uguiGlassBackdrops[i].lastUsedFrame != buildId)
                {
                    ReleaseUGUIGlassBackdrop(_uguiGlassBackdrops[i]);
                    _uguiGlassBackdrops.RemoveAt(i);
                }
            }
        }

        UguiGlassBackdropEntry GetUGUIGlassBackdropEntry(int batchIndex)
        {
            for (int i = 0; i < _uguiGlassBackdrops.Count; ++i)
            {
                if (_uguiGlassBackdrops[i].batchIndex == batchIndex)
                    return _uguiGlassBackdrops[i];
            }

            var entry = new UguiGlassBackdropEntry
            {
                batchIndex = batchIndex
            };
            _uguiGlassBackdrops.Add(entry);
            return entry;
        }

        void EnsureUGUIGlassTextures(UguiGlassBackdropEntry entry, int width, int height)
        {
            if (entry.source != null &&
                entry.blurred != null &&
                entry.width == width &&
                entry.height == height)
            {
                return;
            }

            ReleaseUGUIGlassTextures(entry);

            entry.width = width;
            entry.height = height;
            entry.source = NowGlassBackdropSurface.CreateTexture(width, height, $"Now UGUI Glass Source {entry.batchIndex}");
            entry.blurred = NowGlassBackdropSurface.CreateTexture(width, height, $"Now UGUI Glass Blur {entry.batchIndex}");
        }

        void EnsureUGUIGlassMaterial(UguiGlassBackdropEntry entry, Material baseMaterial)
        {
            NowGlassBackdropSurface.EnsureDerivedMaterial(ref entry.material, ref entry.sourceMaterial, baseMaterial, " Backdrop");
        }

        Material GetUGUIGlassBackdropMaterial(int batchIndex)
        {
            for (int i = 0; i < _uguiGlassBackdrops.Count; ++i)
            {
                var entry = _uguiGlassBackdrops[i];

                if (entry.batchIndex == batchIndex)
                    return entry.material;
            }

            return null;
        }

        void ReleaseUGUIGlassBackdrops()
        {
            for (int i = 0; i < _uguiGlassBackdrops.Count; ++i)
                ReleaseUGUIGlassBackdrop(_uguiGlassBackdrops[i]);

            _uguiGlassBackdrops.Clear();
        }

        void ReleaseUGUIGlassBackdrop(UguiGlassBackdropEntry entry)
        {
            if (entry == null)
                return;

            ReleaseUGUIGlassTextures(entry);
            ReleaseUGUIGlassMaterial(entry);
            entry.sourceMaterial = null;
            entry.batchIndex = -1;
            entry.replayBatchCount = 0;
            entry.lastUsedFrame = -1;
            entry.lastUpdatedFrame = -1;
            entry.blurRadius = 0f;
            entry.blurDownsample = 0;
            entry.blurIterations = 0;
            entry.blurStep = 0f;
            entry.hasExternalSource = false;
            entry.materialName = null;
        }

        void ReleaseUGUIGlassTextures(UguiGlassBackdropEntry entry)
        {
            NowGlassBackdropSurface.ReleaseTexture(ref entry.source);
            NowGlassBackdropSurface.ReleaseTexture(ref entry.blurred);
            entry.width = 0;
            entry.height = 0;
        }

        void ReleaseUGUIGlassMaterial(UguiGlassBackdropEntry entry)
        {
            NowGlassBackdropSurface.ReleaseMaterial(ref entry.material);
        }

        void DestroyExtraCanvasRenderers()
        {
            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
            {
                var crenderer = _extraCanvasRenderers[i];

                if (crenderer == null)
                    continue;

                var target = crenderer.gameObject;

                if (Application.isPlaying)
                    Destroy(target);
                else
                    DestroyImmediate(target);
            }

            _extraCanvasRenderers.Clear();
        }

        Material GetCanvasMaterial(NowMeshBatch batch)
        {
            if (batch.kind == NowMeshKind.CustomRectangle)
                return batch.canvasMaterial != null ? batch.canvasMaterial : batch.material;

            if (batch.kind == NowMeshKind.Ripple)
                return batch.canvasMaterial != null ? batch.canvasMaterial : batch.material;

            if (batch.kind == NowMeshKind.Rectangle)
            {
                if (_rectangleMaterial == null)
                    _rectangleMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");

                return _rectangleMaterial != null ? _rectangleMaterial : batch.material;
            }

            if (batch.kind == NowMeshKind.Glass)
                return batch.canvasMaterial != null ? batch.canvasMaterial : batch.material;

            if (batch.kind == NowMeshKind.TexturedRectangle)
            {
                if (batch.material == null)
                    return null;

                if (_textMaterials.TryGetValue(batch.material, out var texturedRect) && texturedRect != null)
                {
                    var sourceTexture = batch.material.mainTexture;

                    if (!ReferenceEquals(texturedRect.mainTexture, sourceTexture))
                        texturedRect.mainTexture = sourceTexture;

                    return texturedRect;
                }

                if (_rectangleMaterial == null)
                    _rectangleMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");

                if (_rectangleMaterial == null)
                    return batch.material;

                texturedRect = new Material(_rectangleMaterial)
                {
                    name = "Now Textured Rect UGUI",
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = batch.material.mainTexture
                };

                _textMaterials[batch.material] = texturedRect;
                return texturedRect;
            }

            if (batch.kind == NowMeshKind.Sdf)
                return GetSdfCanvasMaterial(batch.material);

            if (batch.material == null)
                return null;

            if (_textMaterials.TryGetValue(batch.material, out var textMaterial) && textMaterial != null)
            {
                if (batch.material.HasProperty(_mainTexProp))
                {
                    var sourceTexture = batch.material.mainTexture;

                    if (!ReferenceEquals(textMaterial.mainTexture, sourceTexture))
                        textMaterial.mainTexture = sourceTexture;
                }

                return textMaterial;
            }

            Material textMaterialTemplate = GetTextMaterialTemplate(batch.material);

            if (textMaterialTemplate == null)
                return batch.material;

            textMaterial = new Material(textMaterialTemplate)
            {
                name = batch.material.name + " UGUI",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (batch.material.HasProperty(_mainTexProp))
                textMaterial.mainTexture = batch.material.mainTexture;

            _textMaterials[batch.material] = textMaterial;
            return textMaterial;
        }

        Material GetTextMaterialTemplate(Material material)
        {
            if (material != null && material.shader != null && material.shader.name == "NowUI/Text Renderer RGBA")
            {
                if (_rgbaTextMaterialTemplate == null)
                    _rgbaTextMaterialTemplate = Resources.Load<Material>("NowUI/TxtMaterialRGBAUGUI");

                return _rgbaTextMaterialTemplate;
            }

            if (_textMaterialTemplate == null)
                _textMaterialTemplate = Resources.Load<Material>("NowUI/TxtMaterialUGUI");

            return _textMaterialTemplate;
        }

        Material GetSdfCanvasMaterial(Material material)
        {
            if (material == null)
                return null;

            if (!_textMaterials.TryGetValue(material, out var canvasMaterial) || canvasMaterial == null)
            {
                canvasMaterial = new Material(material)
                {
                    name = material.name + " UGUI",
                    hideFlags = HideFlags.HideAndDontSave
                };

                _textMaterials[material] = canvasMaterial;
            }
            else
            {
                canvasMaterial.CopyPropertiesFromMaterial(material);
            }

            if (canvasMaterial.HasProperty(_nowCanvasLayoutProp))
                canvasMaterial.SetFloat(_nowCanvasLayoutProp, 1f);

            return canvasMaterial;
        }

        Material GetCanvasMaterialForRendering(NowMeshBatch batch, int batchIndex)
        {
            var currentMaterial = batch.kind == NowMeshKind.Glass
                ? GetUGUIGlassBackdropMaterial(batchIndex) ?? GetCanvasMaterial(batch)
                : GetCanvasMaterial(batch);

            if (currentMaterial == null)
                return null;

            for (int i = 0; i < _materialModifiers.Count; ++i)
            {
                if (_materialModifiers[i] is Component component && component == null)
                    continue;

                currentMaterial = _materialModifiers[i].GetModifiedMaterial(currentMaterial);
            }

            return currentMaterial;
        }

        Material GetStencilMaterial(Material baseMaterial)
        {
            int index = _stencilBaseMaterials.IndexOf(baseMaterial);

            if (index >= 0)
                return _stencilMaterials[index];

            var stencilMaterial = StencilMaterial.Add(
                baseMaterial,
                (1 << m_StencilValue) - 1,
                StencilOp.Keep,
                CompareFunction.Equal,
                ColorWriteMask.All,
                (1 << m_StencilValue) - 1,
                0);

            if (stencilMaterial != baseMaterial)
            {
                _stencilBaseMaterials.Add(baseMaterial);
                _stencilMaterials.Add(stencilMaterial);
            }

            return stencilMaterial;
        }

        void ReleaseStencilMaterials()
        {
            for (int i = 0; i < _stencilMaterials.Count; ++i)
                StencilMaterial.Remove(_stencilMaterials[i]);

            _stencilBaseMaterials.Clear();
            _stencilMaterials.Clear();
            m_MaskMaterial = null;
        }

        void EnsureCanvasChannels()
        {
            var targetCanvas = canvas;

            if (targetCanvas == null)
                return;

            targetCanvas.additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.TexCoord2 |
                AdditionalCanvasShaderChannels.TexCoord3 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
        }
    }

}
