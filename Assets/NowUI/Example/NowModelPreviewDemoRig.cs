using System;
using System.Collections.Generic;
using NowUI;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Shared, self-contained presentation model for the docs and visual harness.
/// It deliberately owns every generated object and material so examples model
/// the same lifecycle expected from production preview owners.
/// </summary>
internal sealed class NowModelPreviewDemoRig : IDisposable
{
    readonly List<Material> _materials = new List<Material>(4);

    GameObject _source;

    public NowModelPreview preview { get; private set; }

    public NowModelPreviewDemoRig()
    {
        try
        {
            CreateSource();
            preview = new NowModelPreview(_source)
                .SetOrthographic()
                .SetFramingPadding(1.03f)
                .SetRotation(Quaternion.Euler(-6f, -18f, 0f))
                .SetLight(
                    Quaternion.Euler(42f, 148f, 0f),
                    1.25f,
                    new Color(1f, 0.91f, 0.78f, 1f));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        preview?.Dispose();
        preview = null;

        DestroyObject(_source);
        _source = null;

        for (int i = 0; i < _materials.Count; ++i)
            DestroyObject(_materials[i]);

        _materials.Clear();
    }

    void CreateSource()
    {
        _source = new GameObject("NowUI Model Preview Robot")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        var shader = ResolvePresentationShader();

        if (shader == null)
            throw new InvalidOperationException("No supported lit or unlit shader was available for the model-preview demo.");

        var blue = CreateMaterial(shader, "Robot Blue", new Color(0.08f, 0.62f, 1f, 1f), 0.15f, 0.62f);
        var navy = CreateMaterial(shader, "Robot Navy", new Color(0.055f, 0.12f, 0.22f, 1f), 0.3f, 0.48f);
        var orange = CreateMaterial(shader, "Robot Orange", new Color(1f, 0.32f, 0.08f, 1f), 0.05f, 0.58f);
        var dark = CreateMaterial(shader, "Robot Dark", new Color(0.012f, 0.018f, 0.03f, 1f), 0f, 0.25f);

        AddPart("Torso", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(1.02f, 1.05f, 0.58f), blue);
        AddPart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.98f, -0.04f), new Vector3(0.74f, 0.68f, 0.66f), orange);
        AddPart("Left Arm", PrimitiveType.Capsule, new Vector3(-0.69f, 0.02f, 0.02f), new Vector3(0.18f, 0.47f, 0.18f), navy);
        AddPart("Right Arm", PrimitiveType.Capsule, new Vector3(0.69f, 0.02f, 0.02f), new Vector3(0.18f, 0.47f, 0.18f), navy);
        AddPart("Left Leg", PrimitiveType.Capsule, new Vector3(-0.28f, -0.82f, 0f), new Vector3(0.21f, 0.4f, 0.22f), navy);
        AddPart("Right Leg", PrimitiveType.Capsule, new Vector3(0.28f, -0.82f, 0f), new Vector3(0.21f, 0.4f, 0.22f), navy);
        AddPart("Chest", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.32f), new Vector3(0.42f, 0.28f, 0.06f), orange);
        AddPart("Left Eye", PrimitiveType.Sphere, new Vector3(-0.16f, 1.02f, 0.35f), new Vector3(0.11f, 0.12f, 0.08f), dark);
        AddPart("Right Eye", PrimitiveType.Sphere, new Vector3(0.16f, 1.02f, 0.35f), new Vector3(0.11f, 0.12f, 0.08f), dark);
        AddPart("Antenna", PrimitiveType.Cylinder, new Vector3(0f, 1.48f, 0f), new Vector3(0.07f, 0.18f, 0.07f), navy);
        AddPart("Antenna Light", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.16f, orange);
        _source.SetActive(false);
    }

    Material CreateMaterial(
        Shader shader,
        string name,
        Color color,
        float metallic,
        float smoothness)
    {
        var material = new Material(shader)
        {
            name = name,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
        _materials.Add(material);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        return material;
    }

    void AddPart(
        string name,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        var part = GameObject.CreatePrimitive(primitiveType);
        part.name = name;
        part.hideFlags = HideFlags.HideAndDontSave;
        part.transform.SetParent(_source.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Shader ResolvePresentationShader()
    {
        string pipelineName = GraphicsSettings.currentRenderPipeline?.GetType().Name ?? string.Empty;

        if (pipelineName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (pipelineName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0 ||
            pipelineName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
            return Shader.Find("HDRP/Lit") ?? Shader.Find("HDRP/Unlit");

        return Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
    }

    static void DestroyObject(Object value)
    {
        if (value == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(value);
        else
            Object.DestroyImmediate(value);
    }
}
