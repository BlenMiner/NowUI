using System;
using System.Collections.Generic;
using NowUIInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[AddComponentMenu("NowUI/NowUI Graphic")]
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class NowUIGraphic : MaskableGraphic
{
    static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");

    [SerializeField] bool _rebuildEveryFrame;

    [NonSerialized] readonly List<CanvasRenderer> _extraCanvasRenderers = new List<CanvasRenderer>(2);

    [NonSerialized] readonly List<IMaterialModifier> _materialModifiers = new List<IMaterialModifier>(4);

    [NonSerialized] readonly List<Material> _stencilBaseMaterials = new List<Material>(4);

    [NonSerialized] readonly List<Material> _stencilMaterials = new List<Material>(4);

    [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

    [NonSerialized] NowUIDrawList _drawList;

    [NonSerialized] Material _rectangleMaterial;

    [NonSerialized] Material _textMaterialTemplate;

    [NonSerialized] Material _rgbaTextMaterialTemplate;

    [NonSerialized] NowUIRectTransformInputProvider _inputProvider;

    Rect _clipRect;

    Vector2 _clipSoftness;

    bool _validClipRect;

    public event Action<NowUIGraphic, Rect> rebuildNowUI;

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

    protected virtual void DrawNowUI(Rect rect)
    {
        rebuildNowUI?.Invoke(this, rect);
    }

    /// <summary>
    /// When true (the default), each rebuild runs <see cref="DrawNowUI"/> twice —
    /// a NowUILayout measure pass (draws suppressed, input passive) followed by
    /// the real pass — so flexible space, stretching and auto-sized groups are
    /// exact within a single rebuild, like Unity IMGUI's Layout and Repaint
    /// events. Override to false to skip the extra pass when the graphic does
    /// not use NowUILayout.
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

        try
        {
            NowUI.BeginColorMultiplier(color);
            colorMultiplierActive = true;

            using (NowUIInput.Begin(GetInputProvider(), new NowUIInputSurface(new Vector2(rect.width, rect.height))))
            {
                if (useLayoutMeasurePass)
                {
                    int layoutCounter = NowUILayout.BeginMeasurePass();

                    try
                    {
                        DrawNowUI(drawRect);
                    }
                    finally
                    {
                        NowUILayout.EndMeasurePass(layoutCounter);
                    }
                }

                DrawNowUI(drawRect);
            }

            NowUI.EndColorMultiplier();
            colorMultiplierActive = false;

            scope.Dispose();
        }
        catch (Exception ex)
        {
            if (colorMultiplierActive)
                NowUI.EndColorMultiplier();

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
        if (_rebuildEveryFrame)
            SetVerticesDirty();
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

        _drawList = new NowUIDrawList(NowUIMeshLayout.Canvas, "NowUI Graphic Mesh");
    }

    protected virtual INowUIInputProvider GetInputProvider()
    {
        if (_inputProvider == null)
            _inputProvider = new NowUIRectTransformInputProvider();

        _inputProvider.rectTransform = rectTransform;
        _inputProvider.eventCamera = GetEventCamera();
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

        // The loop below (re)applies the full transform and renderer state to every entry,
        // including the ones just created.
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
