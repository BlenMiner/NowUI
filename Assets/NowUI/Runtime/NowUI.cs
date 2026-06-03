using NowUIInternal;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class NowUI
{
    static Material _defaultMaterial;

    public static Vector4 screenMask;

    static int _defaultMesh = -1;

    static StaticList<NowMesh> _meshes = new StaticList<NowMesh>(100);

    static int _lastUsedMeshId = -1;

    static bool _captureMesh;

    static int _suppressDrawDepth;

    static Matrix4x4 _projectionMatrix;

    static float _projectionWidth = -1;

    static float _projectionHeight = -1;

    static readonly List<Vector3> _vertices = new List<Vector3>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector2> _meshUvs = new List<Vector2>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _uvs = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _rects = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _radii = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _colors = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _outlineColors = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _extras = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _masks = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _rawUvs = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Color> _uguiColors = new List<Color>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector3> _uguiNormals = new List<Vector3>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<Vector4> _uguiTangents = new List<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static readonly List<int> _triangles = new List<int>(NowMesh.INITIAL_INDEX_CAPACITY);

    static int CreateMesh(Material mat, NowMeshKind kind)
    {
        _meshes.EnsureCapacity(1);
        int id = _meshes.count;

        if (_meshes.array[id] == null)
            _meshes.array[id] = new NowMesh(mat, kind);
        else
            _meshes.array[id].SetMaterial(mat, kind);

        _meshes.count = id + 1;
        return id;
    }

    static NowMesh UseMaterial(Material material, ref int cachedMeshId, NowMeshKind kind)
    {
        if (material == null)
            return null;

        if (_captureMesh)
        {
            if (_lastUsedMeshId >= 0 &&
                _lastUsedMeshId < _meshes.count &&
                ReferenceEquals(_meshes.array[_lastUsedMeshId].material, material) &&
                _meshes.array[_lastUsedMeshId].kind == kind)
            {
                return _meshes.array[_lastUsedMeshId];
            }

            int captureId = CreateMesh(material, kind);
            _lastUsedMeshId = captureId;
            return _meshes.array[captureId];
        }

        int id = cachedMeshId;

        if (id >= 0 &&
            id < _meshes.count &&
            ReferenceEquals(_meshes.array[id].material, material) &&
            _meshes.array[id].kind == kind)
        {
            if (!UseMesh(id))
                return null;

            return _meshes.array[id];
        }

        id = CreateMesh(material, kind);
        cachedMeshId = id;

        if (!UseMesh(id))
            return null;

        return _meshes.array[id];
    }

    static Matrix4x4 GetProjectionMatrix()
    {
        if (_projectionWidth != screenMask.z || _projectionHeight != screenMask.w)
        {
            _projectionWidth = screenMask.z;
            _projectionHeight = screenMask.w;
            _projectionMatrix = Matrix4x4.Ortho(0, screenMask.z, -screenMask.w, 0, -1, 100);
        }

        return _projectionMatrix;
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
        if (!mesh.hasVertices)
            return;

        if (mesh.material == null)
        {
            mesh.ClearVertices();
            return;
        }

        mesh.UploadMesh();

        if (mesh.unityMesh == null)
        {
            mesh.ClearVertices();
            return;
        }

        mesh.material.SetPass(0);
        Graphics.DrawMeshNow(mesh.unityMesh, drawMatrix);
        mesh.ClearVertices();
    }

    static void FlushMesh(int meshId)
    {
        if (meshId < 0 || meshId >= _meshes.count)
            return;

        var mesh = _meshes.array[meshId];

        if (!mesh.hasVertices)
            return;

        BeginDraw(out var drawMatrix);
        DrawMesh(mesh, drawMatrix);
        GL.PopMatrix();
    }

    static bool UseMesh(int meshId)
    {
        if (_captureMesh)
            return true;

        if (meshId < 0 || meshId >= _meshes.count)
            return false;

        if (_lastUsedMeshId == meshId)
            return true;

        FlushMesh(_lastUsedMeshId);
        _lastUsedMeshId = meshId;
        return true;
    }

    private static void Initialize()
    {
        if (_defaultMaterial == null)
        {
            _defaultMaterial = Resources.Load<Material>("NowUI/UIMaterial");
            _meshes.count = 0;
            _defaultMesh = -1;
        }

        if (_defaultMaterial != null && _defaultMesh < 0)
            _defaultMesh = CreateMesh(_defaultMaterial, NowMeshKind.Rectangle);

        _lastUsedMeshId = _defaultMesh;
    }

    public static void StartUI()
    {
        StartUI(new Vector4(0, 0, Screen.width, Screen.height));
    }

    public static void StartUI(Vector4 screenMask)
    {
        _captureMesh = false;
        NowUI.screenMask = screenMask;
        Initialize();
    }

    internal static void BeginMeshCapture(Vector4 screenMask)
    {
        NowUI.screenMask = screenMask;
        Initialize();
        _captureMesh = true;
        _meshes.count = 0;
        _lastUsedMeshId = -1;
    }

    internal static void BeginSuppressDraw()
    {
        ++_suppressDrawDepth;
    }

    internal static void EndSuppressDraw()
    {
        if (_suppressDrawDepth > 0)
            --_suppressDrawDepth;
    }

    internal static void CancelMeshCapture()
    {
        if (!_captureMesh)
            return;

        for (int i = 0; i < _meshes.count; ++i)
            _meshes.array[i].ClearVertices();

        _captureMesh = false;
        _lastUsedMeshId = -1;
    }

    internal static void EndMeshCapture(Mesh target, List<NowUIMeshBatch> batches, Vector2 positionOffset, NowUIMeshLayout layout)
    {
        if (target == null)
        {
            CancelMeshCapture();
            return;
        }

        _vertices.Clear();
        _meshUvs.Clear();
        _uvs.Clear();
        _rects.Clear();
        _radii.Clear();
        _colors.Clear();
        _outlineColors.Clear();
        _extras.Clear();
        _masks.Clear();
        _rawUvs.Clear();
        _uguiColors.Clear();
        _uguiNormals.Clear();
        _uguiTangents.Clear();
        batches.Clear();

        for (int i = 0; i < _meshes.count; ++i)
        {
            var mesh = _meshes.array[i];

            if (!mesh.hasVertices)
                continue;

            batches.Add(new NowUIMeshBatch(mesh.material, mesh.kind));

            if (layout == NowUIMeshLayout.Canvas)
            {
                mesh.AppendUGUIVertices(
                    _vertices,
                    _uvs,
                    _rects,
                    _masks,
                    _extras,
                    _uguiColors,
                    _uguiNormals,
                    _uguiTangents,
                    positionOffset);
                continue;
            }

            mesh.AppendVertices(
                _vertices,
                _meshUvs,
                _rects,
                _radii,
                _colors,
                _outlineColors,
                _extras,
                _masks,
                _rawUvs,
                positionOffset);
        }

        target.Clear();

        if (_vertices.Count == 0)
        {
            CancelMeshCapture();
            return;
        }

        target.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        target.SetVertices(_vertices);

        if (layout == NowUIMeshLayout.Canvas)
        {
            target.SetUVs(0, _uvs);
            target.SetUVs(1, _rects);
            target.SetUVs(2, _masks);
            target.SetUVs(3, _extras);
            target.SetColors(_uguiColors);
            target.SetNormals(_uguiNormals);
            target.SetTangents(_uguiTangents);
        }
        else
        {
            target.SetUVs(0, _meshUvs);
            target.SetUVs(1, _rects);
            target.SetUVs(2, _radii);
            target.SetUVs(3, _colors);
            target.SetUVs(4, _outlineColors);
            target.SetUVs(5, _extras);
            target.SetUVs(6, _masks);
            target.SetUVs(7, _rawUvs);
        }

        target.subMeshCount = batches.Count;

        int subMesh = 0;
        int vertexOffset = 0;

        for (int i = 0; i < _meshes.count; ++i)
        {
            var mesh = _meshes.array[i];

            if (!mesh.hasVertices)
                continue;

            _triangles.Clear();
            mesh.AppendTriangles(_triangles, vertexOffset);
            target.SetTriangles(_triangles, subMesh, false);
            vertexOffset += mesh.vertexCount;
            ++subMesh;
        }

        target.RecalculateBounds();
        CancelMeshCapture();
    }

    public static void FlushUI()
    {
        if (_captureMesh)
            return;

        var meshArray = _meshes.array;
        int count = _meshes.count;

        bool hasVertices = false;

        for (int i = 0; i < count; ++i)
        {
            if (!meshArray[i].hasVertices)
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

    static readonly Vector4 _defaultUV = new Vector4(0, 0, 1, 1);

    static NowRectVertex _tmpVertex;

    public static void DrawRect(NowUIRectangle rectangle)
    {
        if (_suppressDrawDepth > 0 || _defaultMaterial == null)
            return;

        var position = rectangle.rect;
        var pad = rectangle.padding;

        position.x += pad.x;
        position.y += pad.y;
        position.z = position.z - pad.x - pad.z;
        position.w = position.w - pad.y - pad.w;
        int rectHeight = (int)position.w;

        _tmpVertex.position.x = (int)position.x;
        _tmpVertex.position.y = -(int)position.y - rectHeight;
        _tmpVertex.position.z = (int)position.z;
        _tmpVertex.position.w = rectHeight;

        _tmpVertex.mask = rectangle.mask;
        _tmpVertex.radius = rectangle.radius;
        _tmpVertex.color = rectangle.color;
        _tmpVertex.outlineColor = rectangle.outlineColor;
        _tmpVertex.uvwh = _defaultUV;

        var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

        if (mesh == null)
            return;

        mesh.AddRect(_tmpVertex, rectangle.blur, rectangle.outline);
    }

    public static void DrawString(NowUIText style, string value)
    {
        if (_suppressDrawDepth > 0 || string.IsNullOrEmpty(value) || !style.font)
            return;

        var fontSize = style.fontSize;
        var font = style.font;
        NowMesh mesh = null;

        float leftPos = style.rect.x;

        const int TAB_SPACES = 4;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            switch (codepoint)
            {
                case '\n':
                    style.rect.x = leftPos;
                    style.rect.y += font.GetLineHeight() * fontSize;
                    break;
                case '\t':
                {
                    if (font.GetGlyph(' ', out var space))
                        style.rect.x += space.advance * fontSize * TAB_SPACES;
                    break;
                }
                default:
                {
                    if (!font.GetGlyph(codepoint, out var glyph, out var glyphMaterial))
                        continue;

                    if (!Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right))
                    {
                        if (mesh == null || !ReferenceEquals(mesh.material, glyphMaterial))
                        {
                            int materialId = font.GetMaterialId(codepoint);
                            mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                            font.SetMaterialId(codepoint, materialId);

                            if (mesh == null)
                                return;
                        }

                        DrawCharacter(style, glyph, mesh);
                    }

                    style.rect.x += glyph.advance * fontSize;
                    break;
                }
            }
        }
    }

    public static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph)
    {
        if (_suppressDrawDepth > 0 || style.font == null)
            return;

        var font = style.font;
        var material = font.GetMaterial(glyph.unicode);
        int materialId = font.GetMaterialId(glyph.unicode);
        var mesh = UseMaterial(material, ref materialId, NowMeshKind.Text);
        font.SetMaterialId(glyph.unicode, materialId);

        if (mesh == null)
            return;

        DrawCharacter(style, glyph, mesh);
    }

    static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowMesh mesh)
    {
        var fontSize = style.fontSize;
        var font = style.font;
        var rect = style.rect;
        var planeBounds = glyph.planeBounds;

        float lineHeight = font.GetLineHeight() * fontSize;

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

        _tmpVertex.position.x = px;
        _tmpVertex.position.y = -(py + pw);
        _tmpVertex.position.z = pz;
        _tmpVertex.position.w = pw;

        _tmpVertex.uvwh.x = atlasBounds.left;
        _tmpVertex.uvwh.y = atlasBounds.bottom;
        _tmpVertex.uvwh.z = atlasBounds.right - atlasBounds.left;
        _tmpVertex.uvwh.w = atlasBounds.top - atlasBounds.bottom;

        _tmpVertex.mask = style.mask;
        _tmpVertex.radius = default;
        _tmpVertex.color = style.color;
        _tmpVertex.outlineColor = style.outlineColor;

        mesh.AddRect(_tmpVertex, style.outline, fontSize);
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
