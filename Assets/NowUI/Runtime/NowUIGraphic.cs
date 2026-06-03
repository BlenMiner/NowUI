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

    [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

    [NonSerialized] NowUIDrawList _drawList;

    [NonSerialized] Material _rectangleMaterial;

    [NonSerialized] Material _textMaterialTemplate;

    [NonSerialized] Material _rgbaTextMaterialTemplate;

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
            canvasRenderer.SetMesh(_drawList.mesh);
            ApplyMaterials();
            return;
        }

        var positionOffset = new Vector2(rect.xMin, rect.yMax);
        var drawRect = new Rect(0, 0, rect.width, rect.height);

        var scope = _drawList.Begin(new Vector2(rect.width, rect.height), positionOffset);

        try
        {
            DrawNowUI(drawRect);
            scope.Dispose();
        }
        catch (Exception ex)
        {
            scope.Cancel();
            _drawList.Clear();
            Debug.LogException(ex, this);
        }

        canvasRenderer.SetMesh(_drawList.mesh);
        ApplyMaterials();
    }

    protected override void UpdateMaterial()
    {
        ApplyMaterials();
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

    void ApplyMaterials()
    {
        var crenderer = canvasRenderer;
        if (_drawList == null)
        {
            crenderer.materialCount = 0;
            return;
        }

        var batches = _drawList.batches;
        crenderer.materialCount = batches.Count;

        for (int i = 0; i < batches.Count; ++i)
            crenderer.SetMaterial(GetCanvasMaterial(batches[i]), i);
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
