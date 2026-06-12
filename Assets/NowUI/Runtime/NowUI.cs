using NowUI.Internal;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    /// <summary>
    /// NowUI's immediate-mode entry point: frame lifecycle (<see cref="StartUI()"/> /
    /// <see cref="FlushUI"/>) and the drawing factories (<see cref="Rectangle(NowRect)"/>,
    /// <see cref="Text(NowRect)"/>, <see cref="Lottie"/>).
    /// </summary>
    public static class Now
    {
        static Material _defaultMaterial;

        public static NowRect screenMask;

        static float _uiScale = 1f;

        /// <summary>
        /// Pixels per UI unit for the current frame, set by <see cref="StartUI(float)"/>.
        /// 1 means UI units are screen pixels. Pass
        /// <see cref="NowUIScreen.recommendedUIScale"/> to size UI by display density,
        /// which keeps controls a consistent physical size on high-DPI phones.
        /// </summary>
        public static float uiScale => _uiScale;

        /// <summary>
        /// Shapes text through HarfBuzz when available: ligatures, kerning, and
        /// complex-script forms. Segments the shaper cannot fully cover (missing
        /// glyphs, platforms without the native plugin, color fonts) automatically
        /// use the per-codepoint path with font fallbacks, so disabling this only
        /// turns typography features off — text always renders either way.
        /// </summary>
        public static bool textShaping = true;

        static NowFontAsset _defaultFont;

        /// <summary>
        /// Font used by text helpers when no explicit font is provided, such as
        /// <see cref="Text(NowRect)"/> and <see cref="NowLayout.Label(string)"/>.
        /// Defaults to the bundled OpenSans-Regular font unless overridden.
        /// </summary>
        public static NowFontAsset defaultFont
        {
            get
            {
                if (_defaultFont == null)
                    _defaultFont = Resources.Load<NowFontAsset>("NowUI/NotoSans");

                return _defaultFont;
            }
            set => _defaultFont = value;
        }

        static readonly List<NowFontAsset> _fontStack = new List<NowFontAsset>(8);

        /// <summary>
        /// The active font: the innermost font pushed with <see cref="Font(NowFontAsset)"/>,
        /// or <see cref="defaultFont"/> when none is pushed. Font-implicit helpers
        /// (<see cref="Text(NowRect)"/>, <see cref="NowLayout.Label(string)"/>) use this.
        /// </summary>
        public static NowFontAsset font
            => _fontStack.Count > 0 ? _fontStack[^1] : defaultFont;

        /// <summary>
        /// Pushes a contextual font; dispose the returned scope (ideally with a
        /// using statement) to restore the previous one:
        /// <code>
        /// using (Now.Font(headerFont))
        ///     NowLayout.Label("Title").Draw();
        /// </code>
        /// </summary>
        public static NowFontScope Font(NowFontAsset font)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            _fontStack.Add(font);
            return new NowFontScope(true);
        }

        internal static void PopFont()
        {
            if (_fontStack.Count > 0)
                _fontStack.RemoveAt(_fontStack.Count - 1);
        }

        static readonly List<NowRect> _maskStack = new List<NowRect>(4);

        /// <summary>
        /// Pushes an ambient clip rect: every draw inside the scope is masked to the
        /// intersection of all pushed masks and its own mask. The backbone of scroll
        /// views and any clipped container:
        /// <code>
        /// using (Now.Mask(viewport))
        ///     DrawContent();
        /// </code>
        /// </summary>
        public static NowMaskScope Mask(NowRect mask)
        {
            if (_maskStack.Count > 0)
                mask = mask.Intersect(_maskStack[_maskStack.Count - 1]);

            _maskStack.Add(mask);
            return new NowMaskScope(true);
        }

        internal static void PopMask()
        {
            if (_maskStack.Count > 0)
                _maskStack.RemoveAt(_maskStack.Count - 1);
        }

        internal static NowRect ApplyAmbientMask(NowRect mask)
        {
            return _maskStack.Count > 0 ? mask.Intersect(_maskStack[_maskStack.Count - 1]) : mask;
        }

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

        static StaticList<NowCanvasVertex> _canvasVertices = new StaticList<NowCanvasVertex>(NowMesh.INITIAL_VERTEX_CAPACITY);

        static StaticList<int> _triangles = new StaticList<int>(NowMesh.INITIAL_INDEX_CAPACITY);

        /// <summary>Must match the field order of <see cref="NowCanvasVertex"/>.</summary>
        static readonly VertexAttributeDescriptor[] _canvasVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
        };

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

        /// <summary>
        /// UGUI's canvas batcher rejects submeshes above 65535 vertices ("Submesh has
        /// too many verts to include"), so capture meshes are split before reaching it.
        /// </summary>
        const int MAX_VERTICES_PER_MESH = 64000;

        /// <summary>
        /// Returns a mesh with room for the incoming vertices, starting a fresh mesh for
        /// the same material when the current one would exceed the canvas submesh limit.
        /// </summary>
        static NowMesh EnsureMeshCapacity(NowMesh mesh, Material material, NowMeshKind kind, int incomingVertices)
        {
            if (!_captureMesh || mesh == null || material == null)
                return mesh;

            if (mesh.vertexCount + incomingVertices <= MAX_VERTICES_PER_MESH)
                return mesh;

            int id = CreateMesh(material, kind);
            _lastUsedMeshId = id;
            return _meshes.array[id];
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
            if (!Mathf.Approximately(_projectionWidth, screenMask.width) || !Mathf.Approximately(_projectionHeight, screenMask.height))
            {
                _projectionWidth = screenMask.width;
                _projectionHeight = screenMask.height;
                _projectionMatrix = Matrix4x4.Ortho(0, screenMask.width, -screenMask.height, 0, -1, 100);
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
            StartUI(1f);
        }

        /// <summary>
        /// Starts a frame whose UI units cover the whole screen at
        /// <paramref name="uiScale"/> pixels per unit. The projection stretches the
        /// scaled-down logical size across the full viewport and pointer input is
        /// converted into the same units, so drawing code never deals with pixels.
        /// </summary>
        public static void StartUI(float uiScale)
        {
            if (uiScale <= 0f || float.IsNaN(uiScale) || float.IsInfinity(uiScale))
                throw new ArgumentOutOfRangeException(nameof(uiScale), "uiScale must be a positive finite value.");

            _captureMesh = false;
            _fontStack.Clear();
            _maskStack.Clear();
            _uiScale = uiScale;

            screenMask = new NowRect(0f, 0f, Screen.width / uiScale, Screen.height / uiScale);
            NowUIInput.Update(new NowUIInputSurface(
                new Vector2(screenMask.width, screenMask.height),
                new Rect(0f, 0f, Screen.width, Screen.height)));
            Initialize();
        }

        public static void StartUI(NowRect screenMask)
        {
            _captureMesh = false;

            // Self-heal: a font scope leaked in a previous frame must not poison
            // every frame after it.
            _fontStack.Clear();
            _maskStack.Clear();

            _uiScale = 1f;
            Now.screenMask = screenMask;
            NowUIInput.Update(NowUIInputSurface.FromScreenMask(screenMask));
            Initialize();
        }

        internal static void SetUIScale(float uiScale)
        {
            _uiScale = uiScale > 0f ? uiScale : 1f;
        }

        internal static void BeginMeshCapture(Vector4 screenMask)
        {
            Now.screenMask = screenMask;
            _maskStack.Clear();
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

        /// <summary>
        /// CanvasRenderer only accepts meshes with 16 bit indices, so each canvas page
        /// mesh must stay below 65535 vertices in total.
        /// </summary>
        const int MAX_VERTICES_PER_CANVAS_MESH = 65000;

        const int MAX_MESHES_PER_CANVAS_PAGE = 8;

        static readonly List<int> _pageStarts = new List<int>(4);

        static readonly List<int> _pageCounts = new List<int>(4);

        internal static void EndCanvasMeshCapture(NowUIDrawList drawList, Vector2 positionOffset)
        {
            if (drawList == null)
            {
                CancelMeshCapture();
                return;
            }

            // Plan pages: at most 8 materials per CanvasRenderer, and each page mesh
            // must keep 16 bit indices.
            _pageStarts.Clear();
            _pageCounts.Clear();

            int activeIndex = 0;
            int pageStart = 0;
            int pageMeshes = 0;
            int pageVertices = 0;

            for (int i = 0; i < _meshes.count; ++i)
            {
                var mesh = _meshes.array[i];

                if (!mesh.hasVertices)
                    continue;

                bool pageFull = pageMeshes > 0 &&
                    (pageMeshes == MAX_MESHES_PER_CANVAS_PAGE ||
                     pageVertices + mesh.vertexCount > MAX_VERTICES_PER_CANVAS_MESH);

                if (pageFull)
                {
                    _pageStarts.Add(pageStart);
                    _pageCounts.Add(pageMeshes);
                    pageStart = activeIndex;
                    pageMeshes = 0;
                    pageVertices = 0;
                }

                ++pageMeshes;
                pageVertices += mesh.vertexCount;
                ++activeIndex;
            }

            if (pageMeshes > 0)
            {
                _pageStarts.Add(pageStart);
                _pageCounts.Add(pageMeshes);
            }

            int pageCount = Mathf.Max(1, _pageStarts.Count);
            drawList.PrepareCanvasPages(pageCount);

            for (int pageIndex = 0; pageIndex < pageCount; ++pageIndex)
            {
                UploadCapturedMeshes(
                    drawList.GetCanvasMesh(pageIndex),
                    drawList.GetCanvasBatches(pageIndex),
                    positionOffset,
                    NowUIMeshLayout.Canvas,
                    pageIndex < _pageStarts.Count ? _pageStarts[pageIndex] : 0,
                    pageIndex < _pageCounts.Count ? _pageCounts[pageIndex] : 0);
            }

            CancelMeshCapture();
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
            _canvasVertices.Clear();
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
                    mesh.AppendCanvasVertices(ref _canvasVertices, positionOffset);
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

            int vertexCount = layout == NowUIMeshLayout.Canvas ? _canvasVertices.count : _vertices.count;

            if (vertexCount == 0)
            {
                return;
            }

            target.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            if (layout == NowUIMeshLayout.Canvas)
            {
                // One interleaved upload instead of eight per-channel copies.
                target.SetVertexBufferParams(vertexCount, _canvasVertexLayout);
                target.SetVertexBufferData(_canvasVertices.array, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            }
            else
            {
                target.SetVertices(_vertices.array, 0, vertexCount);
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

            // Popups and other deferred overlays draw last, above everything.
            NowUIOverlay.Flush();

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

        internal static void DrawRect(NowUIRectangle rectangle)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null)
                return;

            var position = rectangle.rect;
            var pad = rectangle.padding;

            position.x += pad.x;
            position.y += pad.y;
            position.width = position.width - pad.x - pad.z;
            position.height = position.height - pad.y - pad.w;
            int rectHeight = (int)position.height;

            _tmpVertex.position.x = (int)position.x;
            _tmpVertex.position.y = -(int)position.y - rectHeight;
            _tmpVertex.position.z = (int)position.width;
            _tmpVertex.position.w = rectHeight;

            _tmpVertex.mask = ApplyAmbientMask(rectangle.mask);
            _tmpVertex.radius = rectangle.radius;
            _tmpVertex.color = ApplyColorMultiplier(rectangle.color);
            _tmpVertex.outlineColor = ApplyColorMultiplier(rectangle.outlineColor);
            _tmpVertex.uvwh = _defaultUV;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, 4);
            mesh.AddRect(_tmpVertex, rectangle.blur, rectangle.outline);
        }

        internal static void DrawString(NowUIText style, string value)
        {
            if (_suppressDrawDepth > 0 || string.IsNullOrEmpty(value) || !style.font)
                return;

            style.mask = ApplyAmbientMask(style.mask);

            if (textShaping && TryDrawShapedString(style, value))
                return;

            var fontSize = style.fontSize;
            var fontAsset = style.font;
            NowMesh mesh = null;

            fontAsset.EnsureGlyphs(value, fontSize, style.fontStyle);
            float lineHeight = fontAsset.GetLineHeight(style.fontStyle) * fontSize;
            float baseline = fontAsset.GetAscender(style.fontStyle) * fontSize;
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

                            mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);
                            DrawCharacter(style, glyph, resolvedFont, mesh, baseline);
                        }

                        style.rect.x += glyph.advance * fontSize;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Draws the text through HarfBuzz-shaped runs (ligatures, kerning, complex
        /// scripts). Returns false — drawing nothing — when any segment cannot be
        /// shaped or baked, so the per-codepoint path can take over cleanly; the
        /// validation pass runs before any geometry is emitted to make the handoff
        /// all-or-nothing.
        /// </summary>
        static bool TryDrawShapedString(NowUIText style, string value)
        {
            if (!style.font.TryResolveFont(style.fontStyle, out var font) || font == null)
                return false;

            var fontSize = style.fontSize;
            bool hasTab = false;

            // Validation pass: shape every segment and bake every glyph record.
            // Note: Substring(0, Length) returns the same instance, so single-line
            // text (the common case) allocates nothing here.
            int segmentStart = 0;

            for (int i = 0; i <= value.Length; ++i)
            {
                char control = i < value.Length ? value[i] : '\0';

                if (control == '\t')
                    hasTab = true;

                if (i < value.Length && control != '\n' && control != '\t')
                    continue;

                if (i > segmentStart)
                {
                    string segment = value.Substring(segmentStart, i - segmentStart);

                    if (!font.TryGetShapedRun(segment, out var segmentRun) ||
                        !font.EnsureShapedGlyphs(segmentRun, fontSize))
                    {
                        return false;
                    }
                }

                segmentStart = i + 1;
            }

            float tabAdvance = 0f;

            if (hasTab)
            {
                if (!font.TryGetShapedRun(" ", out var spaceRun) ||
                    !font.EnsureShapedGlyphs(spaceRun, fontSize))
                {
                    return false;
                }

                for (int i = 0; i < spaceRun.Length; ++i)
                    tabAdvance += spaceRun[i].xAdvance;

                tabAdvance *= fontSize * 4; // matches the codepoint path's TAB_SPACES
            }

            // Draw pass: everything below is cache hits.
            float lineHeight = style.font.GetLineHeight(style.fontStyle) * fontSize;
            float baseline = style.font.GetAscender(style.fontStyle) * fontSize;
            float leftPos = style.rect.x;
            NowMesh mesh = null;
            segmentStart = 0;

            for (int i = 0; i <= value.Length; ++i)
            {
                char control = i < value.Length ? value[i] : '\0';

                if (i < value.Length && control != '\n' && control != '\t')
                    continue;

                if (i > segmentStart)
                {
                    string segment = value.Substring(segmentStart, i - segmentStart);
                    font.TryGetShapedRun(segment, out var run);

                    for (int g = 0; g < run.Length; ++g)
                    {
                        var shaped = run[g];

                        if (!font.TryGetShapedGlyph((int)shaped.glyphIndex, fontSize, out var glyph, out var glyphMaterial))
                        {
                            style.rect.x += shaped.xAdvance * fontSize;
                            continue;
                        }

                        if (!Mathf.Approximately(glyph.atlasBounds.left, glyph.atlasBounds.right))
                        {
                            if (mesh == null || !ReferenceEquals(mesh.material, glyphMaterial))
                            {
                                int encoded = NowFont.EncodeGlyphIndexKey((int)shaped.glyphIndex);
                                int materialId = font.GetMaterialId(encoded, fontSize);
                                mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                                font.SetMaterialId(encoded, fontSize, materialId);

                                if (mesh == null)
                                    return true;
                            }

                            mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);

                            var glyphStyle = style;
                            glyphStyle.rect.x = style.rect.x + shaped.xOffset * fontSize;
                            glyphStyle.rect.y = style.rect.y - shaped.yOffset * fontSize;
                            DrawCharacter(glyphStyle, glyph, font, mesh, baseline);
                        }

                        style.rect.x += shaped.xAdvance * fontSize;
                    }
                }

                if (control == '\n')
                {
                    style.rect.x = leftPos;
                    style.rect.y += lineHeight;
                }
                else if (control == '\t')
                {
                    style.rect.x += tabAdvance;
                }

                segmentStart = i + 1;
            }

            return true;
        }

        internal static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph)
        {
            if (_suppressDrawDepth > 0 || style.font == null)
                return;

            style.mask = ApplyAmbientMask(style.mask);

            if (!style.font.TryResolveFont(style.fontStyle, out var resolvedFont))
                return;

            DrawCharacter(style, glyph, resolvedFont);
        }

        internal static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font)
        {
            if (_suppressDrawDepth > 0 || font == null)
                return;

            var material = font.GetMaterial(glyph.unicode, style.fontSize);
            int materialId = font.GetMaterialId(glyph.unicode, style.fontSize);
            var mesh = UseMaterial(material, ref materialId, NowMeshKind.Text);
            font.SetMaterialId(glyph.unicode, style.fontSize, materialId);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Text, 4);
            DrawCharacter(style, glyph, font, mesh);
        }

        static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh)
        {
            float baseline = (style.font != null ? style.font.GetAscender(style.fontStyle) : font.GetAscender()) * style.fontSize;
            DrawCharacter(style, glyph, font, mesh, baseline);
        }

        /// <summary>
        /// <paramref name="baseline"/> is the distance from the top of the line box
        /// to the text baseline (ascender), so descenders stay inside the measured
        /// line height instead of hanging below the rect.
        /// </summary>
        static void DrawCharacter(NowUIText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh, float baseline)
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

            py += baseline - pw;

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

            // Text outline is authored relative to the font size (em units); the
            // shader expects screen pixels.
            mesh.AddRect(_tmpVertex, style.outline * fontSize, font.GetScreenPixelRange(glyph.unicode, fontSize));
        }

        internal static void DrawLottie(NowUILottie lottie)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null)
                return;

            if (lottie.rect.width <= 0f || lottie.rect.height <= 0f)
                return;

            var tint = ApplyColorMultiplier(lottie.color);

            if (tint.w <= 0.0005f)
                return;

            var composition = lottie.asset != null ? lottie.asset.composition : null;

            if (composition == null)
                return;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            float frame = NowLottieRenderer.TimeToFrame(composition, lottie.time, lottie.loop);

            // Optional playback rate cap: hold each displayed frame for several source
            // frames so re-tessellation (and cache pressure) drops proportionally.
            if (lottie.playbackFrameRate > 0f && composition.frameRate > lottie.playbackFrameRate)
            {
                float step = composition.frameRate / lottie.playbackFrameRate;
                frame = Mathf.Floor(frame / step) * step;
            }

            // Quantize to whole frames: animations are authored at frame granularity, so
            // sub-frame sampling only multiplies cache misses.
            frame = Mathf.Round(frame);

            // Tessellate at a capped resolution and scale the vertices up, so a huge
            // (or accidental fullscreen) rect costs sharpness instead of CPU time.
            float renderScale = 1f;
            float maxSize = NowLottieRenderer.maxRenderSize;
            float maxDimension = Mathf.Max(lottie.rect.width, lottie.rect.height);

            if (maxSize > 0f && maxDimension > maxSize)
                renderScale = maxSize / maxDimension;

            var buffer = NowLottieRenderer.RenderCached(
                composition,
                frame,
                lottie.rect.width * renderScale,
                lottie.rect.height * renderScale,
                lottie.preserveAspect);

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, buffer.positions.count);
            mesh.AddGeometry(buffer, new Vector2(lottie.rect.x, lottie.rect.y), 1f / renderScale, tint, ApplyAmbientMask(lottie.mask));
        }

        public static NowUIRectangle Rectangle(NowUIRectangle rect)
        {
            return rect;
        }

        public static NowUIRectangle Rectangle(NowRect position)
        {
            return new NowUIRectangle(position);
        }

        public static NowUIText Text(NowRect position, NowFontAsset font)
        {
            return new NowUIText(position, font);
        }

        public static NowUIText Text(NowRect position)
        {
            return new NowUIText(position, defaultFont);
        }

        public static NowUILottie Lottie(NowRect position, NowLottieAsset asset)
        {
            return new NowUILottie(position, asset);
        }
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Now.Mask(NowRect)"/>; disposing
    /// restores the previous ambient mask.
    /// </summary>
    public struct NowMaskScope : IDisposable
    {
        bool _active;

        internal NowMaskScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            Now.PopMask();
        }
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Now.Font(NowFontAsset)"/>;
    /// disposing restores the previously active font.
    /// </summary>
    public struct NowFontScope : IDisposable
    {
        bool _active;

        internal NowFontScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            Now.PopFont();
        }
    }
}
