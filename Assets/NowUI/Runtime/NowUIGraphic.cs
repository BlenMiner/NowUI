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

    [NonSerialized] readonly List<NowUIMeshBatch> _batches = new List<NowUIMeshBatch>(4);

    [NonSerialized] readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();

    [NonSerialized] Mesh _mesh;

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

        EnsureMesh();

        var rect = rectTransform.rect;

        if (rect.width <= 0 || rect.height <= 0)
        {
            _mesh.Clear();
            _batches.Clear();
            canvasRenderer.SetMesh(_mesh);
            ApplyMaterials();
            return;
        }

        var mask = new Vector4(0, 0, rect.width, rect.height);
        var positionOffset = new Vector2(rect.xMin, rect.yMax);

        NowUI.BeginMeshCapture(mask);

        try
        {
            DrawNowUI(new Rect(0, 0, rect.width, rect.height));
            NowUI.EndMeshCapture(_mesh, _batches, positionOffset);
        }
        catch (Exception ex)
        {
            NowUI.CancelMeshCapture();
            _mesh.Clear();
            _batches.Clear();
            Debug.LogException(ex, this);
        }

        canvasRenderer.SetMesh(_mesh);
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
        if (_mesh != null)
        {
            if (Application.isPlaying)
                Destroy(_mesh);
            else
                DestroyImmediate(_mesh);

            _mesh = null;
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

    void EnsureMesh()
    {
        if (_mesh != null)
            return;

        _mesh = new Mesh
        {
            name = "NowUI Graphic Mesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        _mesh.MarkDynamic();
    }

    void ApplyMaterials()
    {
        var crenderer = canvasRenderer;
        crenderer.materialCount = _batches.Count;

        for (int i = 0; i < _batches.Count; ++i)
            crenderer.SetMaterial(GetCanvasMaterial(_batches[i]), i);
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
