using NowUIInternal;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class NowUI
{
    static Material m_defaultMaterial;

    public static Vector4 ScreenMask;

    static int m_defaultMesh = -1;

    static StaticList<NowMesh> m_meshes = new StaticList<NowMesh>(100);

    static int m_lastUsedMeshId = -1;

    static bool m_captureMesh;

    static Matrix4x4 m_projectionMatrix;

    static float m_projectionWidth = -1;

    static float m_projectionHeight = -1;

    static readonly List<Vector3> s_vertices = new List<Vector3>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_uvs = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_rects = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_radii = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_colors = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_outlineColors = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_extras = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_masks = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_rawUvs = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<Color> s_uguiColors = new List<Color>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector3> s_uguiNormals = new List<Vector3>(NowMesh.InitialVertexCapacity);

    static readonly List<Vector4> s_uguiTangents = new List<Vector4>(NowMesh.InitialVertexCapacity);

    static readonly List<int> s_triangles = new List<int>(NowMesh.InitialIndexCapacity);

    static int CreateMesh(Material mat, NowMeshKind kind)
    {
        m_meshes.EnsureCapacity(1);
        int id = m_meshes.Count;

        if (m_meshes.Array[id] == null)
            m_meshes.Array[id] = new NowMesh(mat, kind);
        else
            m_meshes.Array[id].SetMaterial(mat, kind);

        m_meshes.Count = id + 1;
        return id;
    }

    static NowMesh UseMaterial(Material material, ref int cachedMeshId, NowMeshKind kind)
    {
        if (material == null)
            return null;

        if (m_captureMesh)
        {
            if (m_lastUsedMeshId >= 0 &&
                m_lastUsedMeshId < m_meshes.Count &&
                ReferenceEquals(m_meshes.Array[m_lastUsedMeshId].Material, material) &&
                m_meshes.Array[m_lastUsedMeshId].Kind == kind)
            {
                return m_meshes.Array[m_lastUsedMeshId];
            }

            int captureId = CreateMesh(material, kind);
            m_lastUsedMeshId = captureId;
            return m_meshes.Array[captureId];
        }

        int id = cachedMeshId;

        if (id >= 0 &&
            id < m_meshes.Count &&
            ReferenceEquals(m_meshes.Array[id].Material, material) &&
            m_meshes.Array[id].Kind == kind)
        {
            if (!UseMesh(id))
                return null;

            return m_meshes.Array[id];
        }

        id = CreateMesh(material, kind);
        cachedMeshId = id;

        if (!UseMesh(id))
            return null;

        return m_meshes.Array[id];
    }

    static Matrix4x4 GetProjectionMatrix()
    {
        if (m_projectionWidth != ScreenMask.z || m_projectionHeight != ScreenMask.w)
        {
            m_projectionWidth = ScreenMask.z;
            m_projectionHeight = ScreenMask.w;
            m_projectionMatrix = Matrix4x4.Ortho(0, ScreenMask.z, -ScreenMask.w, 0, -1, 100);
        }

        return m_projectionMatrix;
    }

    static Matrix4x4 GetDrawMatrix()
    {
        return Camera.current != null
            ? Camera.current.transform.localToWorldMatrix
            : Matrix4x4.identity;
    }

    static void BeginDraw(out Matrix4x4 drawMatrix)
    {
        GL.PushMatrix();
        GL.LoadIdentity();
        GL.LoadProjectionMatrix(GetProjectionMatrix());
        drawMatrix = GetDrawMatrix();
    }

    static void DrawMesh(NowMesh mesh, Matrix4x4 drawMatrix)
    {
        if (!mesh.HasVertices)
            return;

        if (mesh.Material == null)
        {
            mesh.ClearVertices();
            return;
        }

        mesh.UploadMesh();

        if (mesh.UnityMesh == null)
        {
            mesh.ClearVertices();
            return;
        }

        mesh.Material.SetPass(0);
        Graphics.DrawMeshNow(mesh.UnityMesh, drawMatrix);
        mesh.ClearVertices();
    }

    static void FlushMesh(int meshId)
    {
        if (meshId < 0 || meshId >= m_meshes.Count)
            return;

        var mesh = m_meshes.Array[meshId];

        if (!mesh.HasVertices)
            return;

        BeginDraw(out var drawMatrix);
        DrawMesh(mesh, drawMatrix);
        GL.PopMatrix();
    }

    static bool UseMesh(int meshId)
    {
        if (m_captureMesh)
            return true;

        if (meshId < 0 || meshId >= m_meshes.Count)
            return false;

        if (m_lastUsedMeshId == meshId)
            return true;

        FlushMesh(m_lastUsedMeshId);
        m_lastUsedMeshId = meshId;
        return true;
    }

    private static void Initialize()
    {
        if (m_defaultMaterial == null)
        {
            m_defaultMaterial = Resources.Load<Material>("NowUI/UIMaterial");
            m_meshes.Count = 0;
            m_defaultMesh = -1;
        }

        if (m_defaultMaterial != null && m_defaultMesh < 0)
            m_defaultMesh = CreateMesh(m_defaultMaterial, NowMeshKind.Rectangle);

        m_lastUsedMeshId = m_defaultMesh;
    }

    public static void StartUI()
    {
        StartUI(new Vector4(0, 0, Screen.width, Screen.height));
    }

    public static void StartUI(Vector4 screenMask)
    {
        m_captureMesh = false;
        ScreenMask = screenMask;
        Initialize();
    }

    internal static void BeginMeshCapture(Vector4 screenMask)
    {
        ScreenMask = screenMask;
        Initialize();
        m_captureMesh = true;
        m_meshes.Count = 0;
        m_lastUsedMeshId = -1;
    }

    internal static void CancelMeshCapture()
    {
        if (!m_captureMesh)
            return;

        for (int i = 0; i < m_meshes.Count; ++i)
            m_meshes.Array[i].ClearVertices();

        m_captureMesh = false;
        m_lastUsedMeshId = -1;
    }

    internal static void EndMeshCapture(Mesh target, List<NowUIMeshBatch> batches, Vector2 positionOffset)
    {
        if (target == null)
        {
            CancelMeshCapture();
            return;
        }

        s_vertices.Clear();
        s_uvs.Clear();
        s_rects.Clear();
        s_radii.Clear();
        s_colors.Clear();
        s_outlineColors.Clear();
        s_extras.Clear();
        s_masks.Clear();
        s_rawUvs.Clear();
        s_uguiColors.Clear();
        s_uguiNormals.Clear();
        s_uguiTangents.Clear();
        batches.Clear();

        for (int i = 0; i < m_meshes.Count; ++i)
        {
            NowMesh mesh = m_meshes.Array[i];

            if (!mesh.HasVertices)
                continue;

            batches.Add(new NowUIMeshBatch(mesh.Material, mesh.Kind));
            mesh.AppendUGUIVertices(
                s_vertices,
                s_uvs,
                s_rects,
                s_masks,
                s_extras,
                s_uguiColors,
                s_uguiNormals,
                s_uguiTangents,
                positionOffset);
        }

        target.Clear();

        if (s_vertices.Count == 0)
        {
            CancelMeshCapture();
            return;
        }

        target.indexFormat = s_vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        target.SetVertices(s_vertices);
        target.SetUVs(0, s_uvs);
        target.SetUVs(1, s_rects);
        target.SetUVs(2, s_masks);
        target.SetUVs(3, s_extras);
        target.SetColors(s_uguiColors);
        target.SetNormals(s_uguiNormals);
        target.SetTangents(s_uguiTangents);
        target.subMeshCount = batches.Count;

        int subMesh = 0;
        int vertexOffset = 0;

        for (int i = 0; i < m_meshes.Count; ++i)
        {
            NowMesh mesh = m_meshes.Array[i];

            if (!mesh.HasVertices)
                continue;

            s_triangles.Clear();
            mesh.AppendTriangles(s_triangles, vertexOffset);
            target.SetTriangles(s_triangles, subMesh, false);
            vertexOffset += mesh.VertexCount;
            ++subMesh;
        }

        target.RecalculateBounds();
        CancelMeshCapture();
    }

    public static void FlushUI()
    {
        if (m_captureMesh)
            return;

        var meshArray = m_meshes.Array;
        int count = m_meshes.Count;

        bool hasVertices = false;

        for (int i = 0; i < count; ++i)
        {
            if (!meshArray[i].HasVertices)
                continue;

            hasVertices = true;
            break;
        }

        if (!hasVertices)
            return;

        BeginDraw(out var drawMatrix);

        for (int i = 0; i < count; ++i)
            DrawMesh(meshArray[i], drawMatrix);

        GL.PopMatrix();
    }

    static readonly Vector4 defaultUV = new Vector4(0, 0, 1, 1);

    static NowRectVertex tmpVertex;

    public static void DrawRect(NowUIRectangle rectangle)
    {
        if (m_defaultMaterial == null)
            return;

        var position = rectangle.Rect;
        var pad = rectangle.Padding;

        position.x += pad.x;
        position.y += pad.y;
        position.z = position.z - pad.x - pad.z;
        position.w = position.w - pad.y - pad.w;
        int rectHeight = (int)position.w;

        tmpVertex.position.x = (int)position.x;
        tmpVertex.position.y = (-(int)position.y) - rectHeight;
        tmpVertex.position.z = (int)position.z;
        tmpVertex.position.w = (int)rectHeight;

        tmpVertex.mask = rectangle.Mask;
        tmpVertex.radius = rectangle.Radius;
        tmpVertex.color = rectangle.Color;
        tmpVertex.outlineColor = rectangle.OutlineColor;
        tmpVertex.uvwh = defaultUV;

        NowMesh mesh = UseMaterial(m_defaultMaterial, ref m_defaultMesh, NowMeshKind.Rectangle);

        if (mesh == null)
            return;

        mesh.AddRect(tmpVertex, rectangle.Blur, rectangle.Outline);
    }

    public static void DrawString(NowUIText style, string value)
    {
        if (string.IsNullOrEmpty(value) || style.Font == null)
            return;

        var fontSize = style.FontSize;
        var font = style.Font;
        NowMesh mesh = null;

        float leftPos = style.Rect.x;

        const int tabSpaces = 4;

        for (int i = 0; i < value.Length; ++i)
        {
            if (value[i] == '\n')
            {
                style.Rect.x = leftPos;
                style.Rect.y += font.AtlasInfo.metrics.lineHeight * fontSize;
            }
            else if (value[i] == '\t')
            {
                if (font.GetGlyph(' ', out var space))
                    style.Rect.x += space.advance * fontSize * tabSpaces;
            }
            else
            {
                if (!font.GetGlyph(value[i], out var glyph))
                    continue;

                if (glyph.atlasBounds.left != glyph.atlasBounds.right)
                {
                    if (mesh == null)
                    {
                        mesh = UseMaterial(font.Material, ref font.MaterialID, NowMeshKind.Text);

                        if (mesh == null)
                            return;
                    }

                    DrawCharacter(style, glyph, mesh);
                }

                style.Rect.x += glyph.advance * fontSize;
            }
        }
    }

    public static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph)
    {
        if (style.Font == null)
            return;

        var font = style.Font;
        var mesh = UseMaterial(font.Material, ref font.MaterialID, NowMeshKind.Text);

        if (mesh == null)
            return;

        DrawCharacter(style, glyph, mesh);
    }

    static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowMesh mesh)
    {
        var fontSize = style.FontSize;
        var font = style.Font;
        var rect = style.Rect;
        var planeBounds = glyph.planeBounds;

        float lineHeight = font.AtlasInfo.metrics.lineHeight * fontSize;

        planeBounds.left *= fontSize;
        planeBounds.right *= fontSize;
        planeBounds.bottom *= fontSize;
        planeBounds.top *= fontSize;

        float px = rect.x + planeBounds.left;
        float py = rect.y - planeBounds.bottom;
        float pz = planeBounds.right - planeBounds.left;
        float pw = planeBounds.top - planeBounds.bottom;

        py += lineHeight - pw;

        var atlasBounds = glyph.atlasBounds;
        float rectHeight = pw;

        tmpVertex.position.x = px;
        tmpVertex.position.y = -(py + rectHeight);
        tmpVertex.position.z = pz;
        tmpVertex.position.w = rectHeight;

        tmpVertex.uvwh.x = atlasBounds.left;
        tmpVertex.uvwh.y = atlasBounds.bottom;
        tmpVertex.uvwh.z = atlasBounds.right - atlasBounds.left;
        tmpVertex.uvwh.w = atlasBounds.top - atlasBounds.bottom;

        tmpVertex.mask = style.Mask;
        tmpVertex.radius = default;
        tmpVertex.color = style.Color;
        tmpVertex.outlineColor = style.OutlineColor;

        mesh.AddRect(tmpVertex, style.Outline, fontSize);
    }

    public static NowUIRectangle Rectangle(NowUIRectangle rect)
    {
        return rect;
    }

    public static NowUIRectangle Rectangle(Vector4 position)
    {
        return new NowUIRectangle(position);
    }

    public static NowUIText Text(Vector4 position, NowFont font)
    {
        return new NowUIText(position, font);
    }
}
