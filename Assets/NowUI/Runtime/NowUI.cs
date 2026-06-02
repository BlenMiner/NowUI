using NowUIInternal;
using UnityEngine;

public static class NowUI
{
    static Material m_defaultMaterial;

    public static Vector4 ScreenMask;

    static int m_defaultMesh = -1;

    static StaticList<NowMesh> m_meshes = new StaticList<NowMesh>(100);

    static int m_lastUsedMeshId = -1;

    static Matrix4x4 m_projectionMatrix;

    static float m_projectionWidth = -1;

    static float m_projectionHeight = -1;

    static int CreateMesh(Material mat)
    {
        m_meshes.EnsureCapacity(1);
        int id = m_meshes.Count;
        m_meshes.Array[id] = new NowMesh(mat);
        m_meshes.Count = id + 1;
        return id;
    }

    static NowMesh GetMesh(NowFont font)
    {
        if (font == null || font.Material == null)
            return null;

        int id = font.MaterialID;

        if (id >= 0 && id < m_meshes.Count && ReferenceEquals(m_meshes.Array[id].Material, font.Material)) 
        {
            return m_meshes.Array[id];
        }
        
        id = CreateMesh(font.Material);
        font.MaterialID = id;

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
            m_defaultMesh = CreateMesh(m_defaultMaterial);

        m_lastUsedMeshId = m_defaultMesh;
    }

    public static void StartUI()
    {
        ScreenMask = new Vector4(0, 0, Screen.width, Screen.height);
        Initialize();
    }

    public static void FlushUI()
    {
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
        if (m_defaultMesh < 0)
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

        if (!UseMesh(m_defaultMesh))
            return;

        m_meshes.Array[m_defaultMesh].AddRect(tmpVertex, rectangle.Blur, rectangle.Outline);
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
                        mesh = GetMesh(font);

                        if (mesh == null)
                            return;
                    }

                    if (!UseMesh(font.MaterialID))
                        return;

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
        var mesh = GetMesh(font);

        if (mesh == null)
            return;

        if (!UseMesh(font.MaterialID))
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
