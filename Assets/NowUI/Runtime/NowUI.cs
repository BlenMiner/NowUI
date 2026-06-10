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

    static Vector4 _colorMultiplier = Vector4.one;

    static readonly List<Vector4> _colorMultiplierStack = new List<Vector4>(4);

    static Matrix4x4 _projectionMatrix;

    static float _projectionWidth = -1;

    static float _projectionHeight = -1;

    static StaticList<Vector3> _vertices = new StaticList<Vector3>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector2> _meshUvs = new StaticList<Vector2>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _uvs = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _rects = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _radii = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _colors = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _outlineColors = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _extras = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _masks = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _rawUvs = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Color> _uguiColors = new StaticList<Color>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector3> _uguiNormals = new StaticList<Vector3>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<Vector4> _uguiTangents = new StaticList<Vector4>(NowMesh.INITIAL_VERTEX_CAPACITY);

    static StaticList<int> _triangles = new StaticList<int>(NowMesh.INITIAL_INDEX_CAPACITY);

    static readonly List<int> _capturedMeshIndices = new List<int>(8);

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
        NowUIInput.Update(NowUIInputSurface.FromScreenMask(screenMask));
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

    internal static void BeginColorMultiplier(Color color)
    {
        _colorMultiplierStack.Add(_colorMultiplier);
        _colorMultiplier = new Vector4(
            _colorMultiplier.x * color.r,
            _colorMultiplier.y * color.g,
            _colorMultiplier.z * color.b,
            _colorMultiplier.w * color.a);
    }

    internal static void EndColorMultiplier()
    {
        int index = _colorMultiplierStack.Count - 1;

        if (index < 0)
        {
            _colorMultiplier = Vector4.one;
            return;
        }

        _colorMultiplier = _colorMultiplierStack[index];
        _colorMultiplierStack.RemoveAt(index);
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

        UploadCapturedMeshes(target, batches, positionOffset, layout, 0, int.MaxValue);
        CancelMeshCapture();
    }

    internal static void EndCanvasMeshCapture(NowUIDrawList drawList, Vector2 positionOffset)
    {
        if (drawList == null)
        {
            CancelMeshCapture();
            return;
        }

        int activeCount = CountCapturedMeshes();
        int pageCount = Mathf.Max(1, (activeCount + 7) / 8);
        drawList.PrepareCanvasPages(pageCount);

        for (int pageIndex = 0; pageIndex < pageCount; ++pageIndex)
        {
            UploadCapturedMeshes(
                drawList.GetCanvasMesh(pageIndex),
                drawList.GetCanvasBatches(pageIndex),
                positionOffset,
                NowUIMeshLayout.Canvas,
                pageIndex * 8,
                8);
        }

        CancelMeshCapture();
    }

    static int CountCapturedMeshes()
    {
        int count = 0;

        for (int i = 0; i < _meshes.count; ++i)
        {
            if (_meshes.array[i].hasVertices)
                ++count;
        }

        return count;
    }

    static void UploadCapturedMeshes(
        Mesh target,
        List<NowUIMeshBatch> batches,
        Vector2 positionOffset,
        NowUIMeshLayout layout,
        int activeStart,
        int activeLimit)
    {
        if (target == null || batches == null)
            return;

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

        int activeIndex = 0;
        _capturedMeshIndices.Clear();

        for (int i = 0; i < _meshes.count; ++i)
        {
            var mesh = _meshes.array[i];

            if (!mesh.hasVertices)
                continue;

            if (activeIndex++ < activeStart)
                continue;

            if (_capturedMeshIndices.Count >= activeLimit)
                break;

            _capturedMeshIndices.Add(i);
            batches.Add(new NowUIMeshBatch(mesh.material, mesh.kind));

            if (layout == NowUIMeshLayout.Canvas)
            {
                mesh.AppendUGUIVertices(
                    ref _vertices,
                    ref _uvs,
                    ref _rects,
                    ref _masks,
                    ref _extras,
                    ref _uguiColors,
                    ref _uguiNormals,
                    ref _uguiTangents,
                    positionOffset);
                continue;
            }

            mesh.AppendVertices(
                ref _vertices,
                ref _meshUvs,
                ref _rects,
                ref _radii,
                ref _colors,
                ref _outlineColors,
                ref _extras,
                ref _masks,
                ref _rawUvs,
                positionOffset);
        }

        target.Clear();

        if (_vertices.count == 0)
        {
            return;
        }

        int vertexCount = _vertices.count;
        target.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        target.SetVertices(_vertices.array, 0, vertexCount);

        if (layout == NowUIMeshLayout.Canvas)
        {
            target.SetUVs(0, _uvs.array, 0, vertexCount);
            target.SetUVs(1, _rects.array, 0, vertexCount);
            target.SetUVs(2, _masks.array, 0, vertexCount);
            target.SetUVs(3, _extras.array, 0, vertexCount);
            target.SetColors(_uguiColors.array, 0, vertexCount);
            target.SetNormals(_uguiNormals.array, 0, vertexCount);
            target.SetTangents(_uguiTangents.array, 0, vertexCount);
        }
        else
        {
            target.SetUVs(0, _meshUvs.array, 0, vertexCount);
            target.SetUVs(1, _rects.array, 0, vertexCount);
            target.SetUVs(2, _radii.array, 0, vertexCount);
            target.SetUVs(3, _colors.array, 0, vertexCount);
            target.SetUVs(4, _outlineColors.array, 0, vertexCount);
            target.SetUVs(5, _extras.array, 0, vertexCount);
            target.SetUVs(6, _masks.array, 0, vertexCount);
            target.SetUVs(7, _rawUvs.array, 0, vertexCount);
        }

        target.subMeshCount = batches.Count;

        int vertexOffset = 0;

        for (int subMesh = 0; subMesh < _capturedMeshIndices.Count; ++subMesh)
        {
            var mesh = _meshes.array[_capturedMeshIndices[subMesh]];

            _triangles.Clear();
            mesh.AppendTriangles(ref _triangles, vertexOffset);
            target.SetTriangles(_triangles.array, 0, _triangles.count, subMesh, false);
            vertexOffset += mesh.vertexCount;
        }

        target.RecalculateBounds();
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

    static Vector4 ApplyColorMultiplier(Vector4 color)
    {
        color.x *= _colorMultiplier.x;
        color.y *= _colorMultiplier.y;
        color.z *= _colorMultiplier.z;
        color.w *= _colorMultiplier.w;
        return color;
    }

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
        _tmpVertex.color = ApplyColorMultiplier(rectangle.color);
        _tmpVertex.outlineColor = ApplyColorMultiplier(rectangle.outlineColor);
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
        var fontAsset = style.font;
        NowMesh mesh = null;

        fontAsset.EnsureGlyphs(value, fontSize, style.fontStyle);
        float lineHeight = fontAsset.GetLineHeight(style.fontStyle) * fontSize;
        float leftPos = style.rect.x;

        const int TAB_SPACES = 4;

        for (int i = 0; i < value.Length; ++i)
        {
            int codepoint = NowFont.ReadCodepoint(value, ref i);

            switch (codepoint)
            {
                case '\n':
                    style.rect.x = leftPos;
                    style.rect.y += lineHeight;
                    break;
                case '\t':
                {
                    if (fontAsset.TryResolveGlyph(' ', fontSize, style.fontStyle, out _, out var space, out _))
                        style.rect.x += space.advance * fontSize * TAB_SPACES;
                    break;
                }
                default:
                {
                    if (!fontAsset.TryResolveGlyph(
                        codepoint,
                        fontSize,
                        style.fontStyle,
                        out var resolvedFont,
                        out var glyph,
                        out var glyphMaterial))
                    {
                        continue;
                    }

                    if (!Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right))
                    {
                        if (mesh == null || !ReferenceEquals(mesh.material, glyphMaterial))
                        {
                            int materialId = resolvedFont.GetMaterialId(codepoint, fontSize);
                            mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                            resolvedFont.SetMaterialId(codepoint, fontSize, materialId);

                            if (mesh == null)
                                return;
                        }

                        DrawCharacter(style, glyph, resolvedFont, mesh, lineHeight);
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

        if (!style.font.TryResolveFont(style.fontStyle, out var font))
            return;

        DrawCharacter(style, glyph, font);
    }

    public static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font)
    {
        if (_suppressDrawDepth > 0 || font == null)
            return;

        var material = font.GetMaterial(glyph.unicode, style.fontSize);
        int materialId = font.GetMaterialId(glyph.unicode, style.fontSize);
        var mesh = UseMaterial(material, ref materialId, NowMeshKind.Text);
        font.SetMaterialId(glyph.unicode, style.fontSize, materialId);

        if (mesh == null)
            return;

        DrawCharacter(style, glyph, font, mesh);
    }

    static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh)
    {
        float lineHeight = (style.font != null ? style.font.GetLineHeight(style.fontStyle) : font.GetLineHeight()) * style.fontSize;
        DrawCharacter(style, glyph, font, mesh, lineHeight);
    }

    static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh, float lineHeight)
    {
        var fontSize = style.fontSize;
        var rect = style.rect;
        var planeBounds = glyph.planeBounds;

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
        _tmpVertex.color = ApplyColorMultiplier(style.color);
        _tmpVertex.outlineColor = ApplyColorMultiplier(style.outlineColor);

        mesh.AddRect(_tmpVertex, style.outline, font.GetScreenPixelRange(glyph.unicode, fontSize));
    }

    public static void DrawLottie(NowUILottie lottie)
    {
        if (_suppressDrawDepth > 0 || _defaultMaterial == null)
            return;

        var composition = lottie.asset != null ? lottie.asset.composition : null;

        if (composition == null)
            return;

        var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

        if (mesh == null)
            return;

        float frame = NowLottieRenderer.TimeToFrame(composition, lottie.time, lottie.loop);

        // Quantize to 1/8th of a frame so equal-looking draws hit the cache.
        frame = Mathf.Round(frame * 8f) * 0.125f;

        var buffer = NowLottieRenderer.RenderCached(
            composition,
            frame,
            lottie.rect.z,
            lottie.rect.w,
            lottie.preserveAspect,
            ApplyColorMultiplier(lottie.color));

        mesh.AddGeometry(buffer, new Vector2(lottie.rect.x, lottie.rect.y), lottie.mask);
    }

    public static NowUIRectangle Rectangle(NowUIRectangle rect)
    {
        return rect;
    }

    public static NowUIRectangle Rectangle(Vector4 position)
    {
        return new NowUIRectangle(position);
    }

    public static NowUIText Text(Vector4 position, NowFontAsset font)
    {
        return new NowUIText(position, font);
    }

    public static NowUILottie Lottie(Vector4 position, NowLottieAsset asset)
    {
        return new NowUILottie(position, asset);
    }
}
