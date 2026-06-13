using System;
using System.Collections.Generic;
using NowUI.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NowUI
{
    [AddComponentMenu("NowUI/NowUI Graphic")]
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class NowGraphic : MaskableGraphic, ILayoutElement
    {
        static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");

        [Header("NowUI")]
        [SerializeField] bool _rebuildEveryFrame;

        [SerializeField, Tooltip("Rebuild automatically while the pointer is over this graphic or a control inside it requested a repaint (focus, animations, caret blink). Keeps NowControls live inside retained UGUI without Rebuild Every Frame.")]
        bool _autoRebuildOnInteraction = true;

        [SerializeField, Tooltip("Withhold pointer input when UGUI elements draw above this graphic, so they occlude NowUI controls the same way this graphic's Raycast Target occludes UGUI beneath it.")]
        bool _respectUGUIRaycast = true;

        [SerializeField, Tooltip("Report the measured NowLayout content extent as this graphic's preferred size, so UGUI LayoutGroups and ContentSizeFitters size it like any other layout element. Settles one rebuild late, like all NowLayout measurement.")]
        bool _driveLayoutSize = true;

        [NonSerialized] Vector2 _preferredSize;

        [NonSerialized] bool _layoutSizeDirty;

        [NonSerialized] bool _wantsInteractionRepaint;

        [NonSerialized] readonly List<CanvasRenderer> _extraCanvasRenderers = new List<CanvasRenderer>(2);

        [NonSerialized] readonly List<IMaterialModifier> _materialModifiers = new List<IMaterialModifier>(4);

        [NonSerialized] readonly List<Material> _stencilBaseMaterials = new List<Material>(4);

        [NonSerialized] readonly List<Material> _stencilMaterials = new List<Material>(4);

        [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

        [NonSerialized] NowDrawList _drawList;

        [NonSerialized] Material _rectangleMaterial;

        [NonSerialized] Material _textMaterialTemplate;

        [NonSerialized] Material _rgbaTextMaterialTemplate;

        [NonSerialized] NowRectTransformInputProvider _inputProvider;

        Rect _clipRect;

        Vector2 _clipSoftness;

        bool _validClipRect;

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

        protected virtual void DrawNowUI(NowRect rect)
        {
            rebuildNowUI?.Invoke(this, rect);
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

            using var profile = NowUIProfiler.GraphicRebuild.Auto();
            EnsureDrawList();

            var rect = rectTransform.rect;

            if (rect.width <= 0 || rect.height <= 0)
            {
                _drawList.Clear();
                ApplyCanvasPages();
                return;
            }

            var positionOffset = new Vector2(rect.xMin, rect.yMax);
            var drawRect = new Rect(0, 0, rect.width, rect.height);

            var scope = _drawList.Begin(new Vector2(rect.width, rect.height), positionOffset);
            bool colorMultiplierActive = false;
            NowControlState.BeginRepaintTracking();
            _wantsInteractionRepaint = false;

            try
            {
                Now.BeginColorMultiplier(color);
                colorMultiplierActive = true;

                using (NowInput.Begin(GetInputProvider(), new NowInputSurface(new Vector2(rect.width, rect.height))))
                {
                    if (useLayoutMeasurePass)
                    {
                        using (NowUIProfiler.MeasurePass.Auto())
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

                    using (NowUIProfiler.Draw.Auto())
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

                    NowUIOverlay.Flush();
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
                (_autoRebuildOnInteraction && (_wantsInteractionRepaint || IsPointerOverGraphic())))
            {
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Cheap pointer-over test so hosted controls get their first hover rebuild
        /// while the graphic is otherwise fully retained.
        /// </summary>
        bool IsPointerOverGraphic()
        {
            var rect = rectTransform.rect;

            if (rect.width <= 0f || rect.height <= 0f)
                return false;

            var provider = GetInputProvider();

            if (!provider.TryGetSnapshot(new NowInputSurface(new Vector2(rect.width, rect.height)), out var snapshot) ||
                !snapshot.hasPointer)
            {
                return false;
            }

            Vector2 position = snapshot.pointerPosition;
            return position.x >= 0f && position.y >= 0f && position.x <= rect.width && position.y <= rect.height;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasChannels();
        }

        protected override void OnDisable()
        {
            ReleaseStencilMaterials();
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

            _drawList = new NowDrawList(NowUIMeshLayout.Canvas, "NowUI Graphic Mesh");
        }

        public bool respectUGUIRaycast
        {
            get => _respectUGUIRaycast;
            set => _respectUGUIRaycast = value;
        }

        /// <summary>
        /// When true (the default), the measured NowLayout content extent is
        /// reported as this graphic's preferred size, so LayoutGroups and
        /// ContentSizeFitters size it like any other layout element. The
        /// measurement settles one rebuild late, like all NowLayout sizing.
        /// </summary>
        public bool driveLayoutSize
        {
            get => _driveLayoutSize;
            set => _driveLayoutSize = value;
        }

        /// <summary>The last measured content extent (origin + content of the root layout areas).</summary>
        public Vector2 measuredContentSize => _preferredSize;

        public virtual void CalculateLayoutInputHorizontal()
        {
        }

        public virtual void CalculateLayoutInputVertical()
        {
        }

        public virtual float minWidth => -1f;

        public virtual float preferredWidth => _driveLayoutSize ? _preferredSize.x : -1f;

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => -1f;

        public virtual float preferredHeight => _driveLayoutSize ? _preferredSize.y : -1f;

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 0;

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

        void ApplyCanvasPages()
        {
            using var profile = NowUIProfiler.ApplyCanvasPages.Auto();
            PruneDestroyedExtraCanvasRenderers();

            if (_drawList == null)
            {
                ClearCanvasRenderer(canvasRenderer);
                ClearExtraCanvasRenderers();
                return;
            }

            _materialModifiers.Clear();
            GetComponents(_materialModifiers);

            ApplyCanvasPage(canvasRenderer, _drawList.GetCanvasMesh(0), _drawList.GetCanvasBatches(0));

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

                ApplyCanvasPage(crenderer, _drawList.GetCanvasMesh(i + 1), _drawList.GetCanvasBatches(i + 1));
            }

            _materialModifiers.Clear();
        }

        void ApplyCanvasPage(CanvasRenderer crenderer, Mesh mesh, List<NowUIMeshBatch> batches)
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
                crenderer.SetMaterial(GetCanvasMaterialForRendering(batches[i]), i);

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
                var go = new GameObject($"NowUI Graphic Renderer {pageIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer))
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

        Material GetCanvasMaterial(NowUIMeshBatch batch)
        {
            if (batch.kind == NowMeshKind.Rectangle)
            {
                if (_rectangleMaterial == null)
                    _rectangleMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");

                return _rectangleMaterial != null ? _rectangleMaterial : batch.material;
            }

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
                    name = "NowUI Textured Rect UGUI",
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = batch.material.mainTexture
                };

                _textMaterials[batch.material] = texturedRect;
                return texturedRect;
            }

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

        Material GetCanvasMaterialForRendering(NowUIMeshBatch batch)
        {
            var currentMaterial = GetCanvasMaterial(batch);

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
