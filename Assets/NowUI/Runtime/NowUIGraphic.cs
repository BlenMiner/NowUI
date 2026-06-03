using System;
using System.Collections.Generic;
using NowUIInternal;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("NowUI/NowUI Graphic")]
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class NowUIGraphic : Graphic
{
    static readonly int _mainTexProp = Shader.PropertyToID("_MainTex");

    [SerializeField] bool _rebuildEveryFrame;

    [NonSerialized] readonly List<CanvasRenderer> _extraCanvasRenderers = new List<CanvasRenderer>(2);

    [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

    [NonSerialized] NowUIDrawList _drawList;

    [NonSerialized] Material _rectangleMaterial;

    [NonSerialized] Material _textMaterialTemplate;

    [NonSerialized] Material _rgbaTextMaterialTemplate;

    [NonSerialized] NowUIRectTransformInputProvider _inputProvider;

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

    public void MarkDirty()
    {
        SetVerticesDirty();
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

        try
        {
            using (NowUIInput.Begin(GetInputProvider(), new NowUIInputSurface(new Vector2(rect.width, rect.height))))
                DrawNowUI(drawRect);

            scope.Dispose();
        }
        catch (Exception ex)
        {
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
        if (_drawList == null)
        {
            ClearCanvasRenderer(canvasRenderer);
            ClearExtraCanvasRenderers();
            return;
        }

        ApplyCanvasPage(canvasRenderer, _drawList.GetCanvasMesh(0), _drawList.GetCanvasBatches(0));

        int extraPageCount = Mathf.Max(0, _drawList.canvasPageCount - 1);
        EnsureExtraCanvasRendererCount(extraPageCount);

        for (int i = 0; i < _extraCanvasRenderers.Count; ++i)
        {
            var crenderer = _extraCanvasRenderers[i];

            if (i >= extraPageCount)
            {
                ClearCanvasRenderer(crenderer);

                if (crenderer != null && crenderer.gameObject.activeSelf)
                    crenderer.gameObject.SetActive(false);

                continue;
            }

            if (!crenderer.gameObject.activeSelf)
                crenderer.gameObject.SetActive(true);

            ApplyCanvasPage(crenderer, _drawList.GetCanvasMesh(i + 1), _drawList.GetCanvasBatches(i + 1));
        }
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
            crenderer.SetMaterial(GetCanvasMaterial(batches[i]), i);
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

    void EnsureExtraCanvasRendererCount(int count)
    {
        while (_extraCanvasRenderers.Count < count)
        {
            int pageIndex = _extraCanvasRenderers.Count + 1;
            var go = new GameObject($"NowUI Graphic Renderer {pageIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var childTransform = (RectTransform)go.transform;
            childTransform.SetParent(transform, false);
            childTransform.anchorMin = Vector2.zero;
            childTransform.anchorMax = Vector2.one;
            childTransform.pivot = rectTransform.pivot;
            childTransform.offsetMin = Vector2.zero;
            childTransform.offsetMax = Vector2.zero;
            childTransform.localScale = Vector3.one;
            childTransform.localRotation = Quaternion.identity;
            childTransform.SetSiblingIndex(pageIndex - 1);

            var crenderer = go.GetComponent<CanvasRenderer>();
            crenderer.cullTransparentMesh = canvasRenderer.cullTransparentMesh;
            _extraCanvasRenderers.Add(crenderer);
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
        }
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
