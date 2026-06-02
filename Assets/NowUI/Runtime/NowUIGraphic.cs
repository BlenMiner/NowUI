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
    private static readonly int MAIN_TEX_PROP = Shader.PropertyToID("_MainTex");
    [SerializeField] bool m_rebuildEveryFrame;

    [NonSerialized] readonly List<NowUIMeshBatch> m_batches = new List<NowUIMeshBatch>(4);

    [NonSerialized] readonly Dictionary<Material, Material> m_textMaterials = new Dictionary<Material, Material>();

    [NonSerialized] Mesh m_mesh;

    [NonSerialized] Material m_rectangleMaterial;

    [NonSerialized] Material m_textMaterialTemplate;

    public event Action<NowUIGraphic, Rect> RebuildNowUI;

    public bool RebuildEveryFrame
    {
        get => m_rebuildEveryFrame;
        set
        {
            if (m_rebuildEveryFrame == value)
                return;

            m_rebuildEveryFrame = value;
            SetVerticesDirty();
        }
    }

    protected virtual void DrawNowUI(Rect rect)
    {
        RebuildNowUI?.Invoke(this, rect);
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
            m_mesh.Clear();
            m_batches.Clear();
            canvasRenderer.SetMesh(m_mesh);
            ApplyMaterials();
            return;
        }

        Vector4 mask = new Vector4(0, 0, rect.width, rect.height);
        Vector2 positionOffset = new Vector2(rect.xMin, rect.yMax);

        NowUI.BeginMeshCapture(mask);

        try
        {
            DrawNowUI(new Rect(0, 0, rect.width, rect.height));
            NowUI.EndMeshCapture(m_mesh, m_batches, positionOffset);
        }
        catch (Exception ex)
        {
            NowUI.CancelMeshCapture();
            m_mesh.Clear();
            m_batches.Clear();
            Debug.LogException(ex, this);
        }

        canvasRenderer.SetMesh(m_mesh);
        ApplyMaterials();
    }

    protected override void UpdateMaterial()
    {
        ApplyMaterials();
    }

    protected virtual void LateUpdate()
    {
        if (m_rebuildEveryFrame)
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
        if (m_mesh != null)
        {
            if (Application.isPlaying)
                Destroy(m_mesh);
            else
                DestroyImmediate(m_mesh);

            m_mesh = null;
        }

        foreach (var mat in m_textMaterials.Values)
        {
            if (mat == null)
                continue;

            if (Application.isPlaying)
                Destroy(mat);
            else
                DestroyImmediate(mat);
        }

        m_textMaterials.Clear();
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
        if (m_mesh != null)
            return;

        m_mesh = new Mesh
        {
            name = "NowUI Graphic Mesh",
            hideFlags = HideFlags.HideAndDontSave
        };

        m_mesh.MarkDynamic();
    }

    void ApplyMaterials()
    {
        var crenderer = canvasRenderer;
        crenderer.materialCount = m_batches.Count;

        for (int i = 0; i < m_batches.Count; ++i)
            crenderer.SetMaterial(GetCanvasMaterial(m_batches[i]), i);
    }

    Material GetCanvasMaterial(NowUIMeshBatch batch)
    {
        if (batch.Kind == NowMeshKind.Rectangle)
        {
            if (m_rectangleMaterial == null)
                m_rectangleMaterial = Resources.Load<Material>("NowUI/UIMaterialUGUI");

            return m_rectangleMaterial != null ? m_rectangleMaterial : batch.Material;
        }

        if (batch.Material == null)
            return null;

        if (m_textMaterials.TryGetValue(batch.Material, out Material textMaterial) && textMaterial != null)
            return textMaterial;

        if (m_textMaterialTemplate == null)
            m_textMaterialTemplate = Resources.Load<Material>("NowUI/TxtMaterialUGUI");

        if (m_textMaterialTemplate == null)
            return batch.Material;

        textMaterial = new Material(m_textMaterialTemplate)
        {
            name = batch.Material.name + " UGUI",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (batch.Material.HasProperty(MAIN_TEX_PROP))
            textMaterial.mainTexture = batch.Material.mainTexture;

        m_textMaterials[batch.Material] = textMaterial;
        return textMaterial;
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
