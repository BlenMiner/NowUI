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

        static readonly int _nowGlassUseBackdropProp = Shader.PropertyToID("_NowGlassUseBackdrop");

        [Header("NowUI")]
        [SerializeField] bool _rebuildEveryFrame;

        [SerializeField, Tooltip("Rebuild automatically when pointer/button/scroll/navigation input changes for this graphic or a control inside it requested a repaint (animations, caret blink). Keeps NowControls live inside retained UGUI without Rebuild Every Frame.")]
        bool _autoRebuildOnInteraction = true;

        [SerializeField, Tooltip("Withhold pointer input when UGUI elements draw above this graphic, so they occlude NowUI controls the same way this graphic's Raycast Target occludes UGUI beneath it.")]
        bool _respectUGUIRaycast = true;

        [SerializeField, Tooltip("Report the measured NowLayout content extent as this graphic's preferred size, so UGUI LayoutGroups and ContentSizeFitters size it like any other layout element. Layout queries run a passive NowLayout measure pass before geometry rebuilds.")]
        bool _driveLayoutSize = true;

        [SerializeField, HideInInspector]
        Texture _uguiBackdropSourceTexture;

        [SerializeField, HideInInspector]
        NowGlassBlurQuality _glassBlurQuality = NowGlassBlurQuality.Auto;

        [NonSerialized] Vector2 _preferredSize;

        [NonSerialized] bool _layoutSizeDirty;

        [NonSerialized] bool _measuringLayoutInput;

        [NonSerialized] bool _hasLayoutInputMeasurement;

        [NonSerialized] int _layoutInputMeasurementFrame;

        [NonSerialized] Vector2 _layoutInputMeasurementSize;

        [NonSerialized] bool _wantsInteractionRepaint;

        [NonSerialized] bool _hasLastInteractionInput;

        [NonSerialized] InteractionInputState _lastInteractionInput;

        [NonSerialized] readonly List<CanvasRenderer> _extraCanvasRenderers = new List<CanvasRenderer>(2);

        [NonSerialized] readonly List<IMaterialModifier> _materialModifiers = new List<IMaterialModifier>(4);

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

        struct InteractionInputState
        {
            const float PositionEpsilonSqr = 0.25f;

            public bool pointerInside;

            public Vector2 pointerPosition;

            public NowPointerButtons pointerButtonsDown;

            public NowPointerButtons pointerButtonsPressed;

            public NowPointerButtons pointerButtonsReleased;

            public Vector2 scrollDelta;

            public Vector2 navigation;

            public bool focusPreviousPressed;

            public bool focusNextPressed;

            public bool submitDown;

            public bool submitPressed;

            public bool submitReleased;

            public bool cancelDown;

            public bool cancelPressed;

            public bool cancelReleased;

            public static InteractionInputState FromSnapshot(NowInputSnapshot snapshot, Vector2 size)
            {
                bool inside = snapshot.hasPointer &&
                    snapshot.pointerPosition.x >= 0f &&
                    snapshot.pointerPosition.y >= 0f &&
                    snapshot.pointerPosition.x <= size.x &&
                    snapshot.pointerPosition.y <= size.y;

                return new InteractionInputState
                {
                    pointerInside = inside,
                    pointerPosition = snapshot.pointerPosition,
                    pointerButtonsDown = snapshot.pointerButtonsDown,
                    pointerButtonsPressed = snapshot.pointerButtonsPressed,
                    pointerButtonsReleased = snapshot.pointerButtonsReleased,
                    scrollDelta = snapshot.scrollDelta,
                    navigation = snapshot.navigation,
                    focusPreviousPressed = snapshot.focusPreviousPressed,
                    focusNextPressed = snapshot.focusNextPressed,
                    submitDown = snapshot.submitDown,
                    submitPressed = snapshot.submitPressed,
                    submitReleased = snapshot.submitReleased,
                    cancelDown = snapshot.cancelDown,
                    cancelPressed = snapshot.cancelPressed,
                    cancelReleased = snapshot.cancelReleased
                };
            }

            public bool HasChangedSince(in InteractionInputState previous)
            {
                bool pointerRelevant =
                    pointerInside ||
                    previous.pointerInside ||
                    pointerButtonsDown != NowPointerButtons.None ||
                    previous.pointerButtonsDown != NowPointerButtons.None;

                if (pointerInside != previous.pointerInside)
                    return true;

                if (pointerRelevant && (pointerPosition - previous.pointerPosition).sqrMagnitude > PositionEpsilonSqr)
                    return true;

                if (pointerButtonsDown != previous.pointerButtonsDown)
                    return true;

                if (pointerButtonsPressed != NowPointerButtons.None ||
                    pointerButtonsReleased != NowPointerButtons.None)
                    return true;

                if (pointerRelevant && scrollDelta != Vector2.zero)
                    return true;

                if ((navigation - previous.navigation).sqrMagnitude > PositionEpsilonSqr)
                    return true;

                if (submitDown != previous.submitDown || cancelDown != previous.cancelDown)
                    return true;

                return focusPreviousPressed || focusNextPressed ||
                    submitPressed || submitReleased || cancelPressed || cancelReleased;
            }
        }

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
        /// When true (the default), each rebuild runs <see cref="DrawNowUI"/> twice —
        /// a NowLayout measure pass (draws suppressed, input passive) followed by
        /// the real pass — so flexible space, stretching and auto-sized groups are
        /// exact within a single rebuild, like Unity IMGUI's Layout and Repaint
        /// events. Override to false to skip the extra pass when the graphic does
        /// not use NowLayout.
        /// </summary>
        protected virtual bool useLayoutMeasurePass => true;

        public void MarkDirty()
        {
            SetVerticesDirty();
        }

        public override void SetVerticesDirty()
        {
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
            var drawRect = new Rect(0, 0, rect.width, rect.height);
            float previousUIScale = Now.uiScale;

            Now.SetUIScale(GetCanvasScaleFactor());
            var scope = _drawList.Begin(new Vector2(rect.width, rect.height), positionOffset, false, _glassBlurQuality);
            bool colorMultiplierActive = false;
            NowControlState.BeginRepaintTracking();
            _wantsInteractionRepaint = false;

            try
            {
                Now.BeginColorMultiplier(color);
                colorMultiplierActive = true;

                var inputSurface = new NowInputSurface(new Vector2(rect.width, rect.height));

                using (NowInput.Begin(GetInputProvider(), inputSurface))
                {
                    StoreLastInteractionInput(NowInput.current, inputSurface.size);

                    if (useLayoutMeasurePass)
                    {
                        using (NowProfiler.MeasurePass.Auto())
                        {
                            int layoutCounter = NowLayout.BeginMeasurePass();

                            try
                            {
                                DrawNowUI(drawRect);
                            }
                            finally
                            {
                                NowLayout.EndMeasurePass(layoutCounter);
                            }
                        }
                    }

                    Vector2 measured;

                    using (NowProfiler.Draw.Auto())
                    {
                        NowLayout.BeginContentTracking();
                        DrawNowUI(drawRect);
                        measured = NowLayout.EndContentTracking();
                    }

                    if (_driveLayoutSize && (measured - _preferredSize).sqrMagnitude > 0.25f)
                    {
                        _preferredSize = measured;
                        _layoutSizeDirty = true;
                    }

                    NowOverlay.Flush();
                }

                Now.EndColorMultiplier();
                colorMultiplierActive = false;
                _wantsInteractionRepaint = NowControlState.EndRepaintTracking();

                scope.Dispose();
            }
            catch (Exception ex)
            {
                if (colorMultiplierActive)
                    Now.EndColorMultiplier();

                NowLayout.EndContentTracking();
                scope.Cancel();
                _drawList.Clear();
                Debug.LogException(ex, this);
            }
            finally
            {
                Now.SetUIScale(previousUIScale);
            }

            ApplyCanvasPages();
        }

        protected override void UpdateMaterial()
        {
            ApplyCanvasPages();
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
                (_autoRebuildOnInteraction && (_wantsInteractionRepaint || HasInteractionInputChanged())))
            {
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Cheap input watcher so hosted controls get a rebuild when pointer,
        /// button, scroll or navigation input changes, while idle hover stays
        /// retained.
        /// </summary>
        bool HasInteractionInputChanged()
        {
            var rect = rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
                return false;

            var provider = GetInputProvider();
            var size = new Vector2(rect.width, rect.height);
            var surface = new NowInputSurface(size);

            if (!provider.TryGetSnapshot(surface, out var snapshot))
                snapshot = default;

            var current = InteractionInputState.FromSnapshot(snapshot, size);

            if (!_hasLastInteractionInput)
            {
                _lastInteractionInput = current;
                _hasLastInteractionInput = true;
                return false;
            }

            return current.HasChangedSince(_lastInteractionInput);
        }

        void StoreLastInteractionInput(NowInputSnapshot snapshot, Vector2 size)
        {
            _lastInteractionInput = InteractionInputState.FromSnapshot(snapshot, size);
            _hasLastInteractionInput = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _hasLastInteractionInput = false;
            EnsureCanvasChannels();
        }

        protected override void OnDisable()
        {
            _hasLastInteractionInput = false;
            ReleaseStencilMaterials();
            ReleaseUGUIGlassBackdrops();
            ClearCanvasRenderer(canvasRenderer);
            ClearExtraCanvasRenderers();
            base.OnDisable();
        }

        protected override void OnCanvasHierarchyChanged()
        {
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
        /// When true (the default), the measured NowLayout content extent is
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

            float previousUIScale = Now.uiScale;
            bool colorMultiplierActive = false;
            bool contentTracking = false;
            int layoutCounter = 0;
            _measuringLayoutInput = true;

            try
            {
                Now.SetUIScale(GetCanvasScaleFactor());
                Now.BeginColorMultiplier(color);
                colorMultiplierActive = true;

                using (NowInput.Begin(GetInputProvider(), new NowInputSurface(size)))
                {
                    layoutCounter = NowLayout.BeginMeasurePass();

                    try
                    {
                        NowLayout.BeginContentTracking();
                        contentTracking = true;

                        DrawNowUI(new NowRect(0f, 0f, size.x, size.y));

                        Vector2 measured = NowLayout.EndContentTracking();
                        contentTracking = false;

                        if ((_preferredSize - measured).sqrMagnitude > 0.25f)
                            _preferredSize = measured;
                    }
                    finally
                    {
                        if (contentTracking)
                            NowLayout.EndContentTracking();

                        NowLayout.EndMeasurePass(layoutCounter);
                    }
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

                Now.SetUIScale(previousUIScale);
                _measuringLayoutInput = false;
            }
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

            _materialModifiers.Clear();
            GetComponents(_materialModifiers);

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

            _materialModifiers.Clear();
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

            while (_extraCanvasRenderers.Count < count)
            {
                int pageIndex = _extraCanvasRenderers.Count + 1;
                var go = new GameObject($"Now Graphic Renderer {pageIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                go.transform.SetParent(transform, false);
                _extraCanvasRenderers.Add(go.GetComponent<CanvasRenderer>());
            }

            for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
            {
                var crenderer = _extraCanvasRenderers[i];

                if (crenderer == null)
                    continue;

                var childTransform = crenderer.transform as RectTransform;

                if (childTransform == null)
                    continue;

                childTransform.SetSiblingIndex(i);
                childTransform.anchorMin = Vector2.zero;
                childTransform.anchorMax = Vector2.one;
                childTransform.pivot = rectTransform.pivot;
                childTransform.offsetMin = Vector2.zero;
                childTransform.offsetMax = Vector2.zero;
                childTransform.localScale = Vector3.one;
                childTransform.localRotation = Quaternion.identity;
                crenderer.cullTransparentMesh = canvasRenderer.cullTransparentMesh;
                ApplyRendererMaskState(crenderer);
            }
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
            entry.source = CreateUGUIGlassTexture(width, height, $"Now UGUI Glass Source {entry.batchIndex}");
            entry.blurred = CreateUGUIGlassTexture(width, height, $"Now UGUI Glass Blur {entry.batchIndex}");
        }

        static RenderTexture CreateUGUIGlassTexture(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                RenderTextureFormat.ARGB32,
                0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.Create();
            return texture;
        }

        void EnsureUGUIGlassMaterial(UguiGlassBackdropEntry entry, Material baseMaterial)
        {
            if (entry.material != null && entry.sourceMaterial == baseMaterial)
                return;

            ReleaseUGUIGlassMaterial(entry);
            entry.sourceMaterial = baseMaterial;
            entry.material = new Material(baseMaterial)
            {
                name = $"{baseMaterial.name} Backdrop",
                hideFlags = HideFlags.HideAndDontSave
            };
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
            if (entry.source != null)
            {
                entry.source.Release();
                DestroyNowObject(entry.source);
                entry.source = null;
            }

            if (entry.blurred != null)
            {
                entry.blurred.Release();
                DestroyNowObject(entry.blurred);
                entry.blurred = null;
            }

            entry.width = 0;
            entry.height = 0;
        }

        void ReleaseUGUIGlassMaterial(UguiGlassBackdropEntry entry)
        {
            if (entry.material == null)
                return;

            DestroyNowObject(entry.material);
            entry.material = null;
        }

        static void DestroyNowObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
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
                    return texturedRect;

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
                return textMaterial;

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
                currentMaterial = _materialModifiers[i].GetModifiedMaterial(currentMaterial);

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
