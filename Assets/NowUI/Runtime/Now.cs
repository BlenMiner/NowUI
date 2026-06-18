using NowUI.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI
{
    /// <summary>
    /// NowUI's immediate-mode entry point: frame lifecycle (<see cref="StartUI()"/>)
    /// and the drawing factories (<see cref="Rectangle(NowRect)"/>,
    /// <see cref="Text(NowRect)"/>, <see cref="Lottie"/>).
    /// </summary>
    public static partial class Now
    {
        static Material _defaultMaterial;

        public static NowRect screenMask;

        static float _uiScale = 1f;

        /// <summary>
        /// Pixels per UI unit for the current frame, set by <see cref="StartUI(float)"/>.
        /// 1 means UI units are screen pixels. Pass
        /// <see cref="NowScreen.recommendedUIScale"/> to size UI by display density,
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
            if (_transformStack.Count > 0)
                mask = ApplyTransformRect(mask);

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

        static readonly NowRect UnboundedMask = new NowRect(-100000f, -100000f, 200000f, 200000f);

        /// <summary>Intersects a mask with the ambient mask stack. An empty mask means
        /// "no mask": styles built from a default rect end up with a zero-size mask, and
        /// clipping everything against it (which renders nothing) is never the intent.</summary>
        internal static NowRect ApplyAmbientMask(NowRect mask)
        {
            if (mask.isEmpty)
                mask = UnboundedMask;

            return _maskStack.Count > 0 ? mask.Intersect(_maskStack[_maskStack.Count - 1]) : mask;
        }

        internal static bool IsInsideAmbientMask(Vector2 position)
        {
            return _maskStack.Count == 0 || _maskStack[_maskStack.Count - 1].Contains(position);
        }

        #region Transform System

        /// <summary>
        /// Represents an effective 2D transform with scale and translation.
        /// Used by the transform stack to automatically transform drawing and input rects.
        /// </summary>
        public readonly struct NowTransform
        {
            /// <summary>Translation offset (pan position).</summary>
            public readonly Vector2 origin;

            /// <summary>Scale factors for X and Y axes.</summary>
            public readonly Vector2 scale;

            /// <summary>Combined transform matrix (T * S).</summary>
            public readonly Matrix4x4 matrix;

            /// <summary>Identity transform (no scale and no translation).</summary>
            public static readonly NowTransform identity = new NowTransform(Vector2.one, Vector2.zero);

            public NowTransform(Vector2 scale, Vector2 origin)
            {
                this.scale = scale;
                this.origin = origin;
                this.matrix = CalculateMatrix(scale, origin);
            }

            static Matrix4x4 CalculateMatrix(Vector2 scale, Vector2 origin)
            {
                var matrix = Matrix4x4.identity;
                matrix.m00 = scale.x;
                matrix.m11 = scale.y;
                matrix.m03 = origin.x;
                matrix.m13 = origin.y;
                return matrix;
            }

            internal static NowTransform Compose(NowTransform parent, NowTransform local)
            {
                return new NowTransform(
                    new Vector2(parent.scale.x * local.scale.x, parent.scale.y * local.scale.y),
                    new Vector2(
                        local.origin.x * parent.scale.x + parent.origin.x,
                        local.origin.y * parent.scale.y + parent.origin.y));
            }
        }

        internal readonly struct NowTransformSnapshot
        {
            public readonly bool active;
            public readonly NowTransform transform;

            public NowTransformSnapshot(bool active, NowTransform transform)
            {
                this.active = active;
                this.transform = transform;
            }
        }

        static readonly List<NowTransform> _transformStack = new List<NowTransform>(4);

        /// <summary>
        /// The active transform from the transform stack, or identity if none.
        /// </summary>
        public static NowTransform currentTransform =>
            _transformStack.Count > 0 ? _transformStack[_transformStack.Count - 1] : NowTransform.identity;

        /// <summary>
        /// Pushes a transform scope. All drawing inside the scope is transformed
        /// by the given scale and origin (translation). Nested transforms compose
        /// with the active parent transform.
        /// <code>
        /// using (Now.Transform(scale: 0.5f, origin: new Vector2(100, 100)))
        /// {
        ///     // Everything drawn here is automatically scaled by 0.5x
        ///     // and translated by (100, 100)
        ///     Now.Rectangle(rect).Draw();
        ///     Now.Label("Text", 13f);  // Font size also scaled
        /// }
        /// </code>
        /// </summary>
        public static NowTransformScope Transform(Vector2 scale, Vector2 origin = default)
        {
            var local = new NowTransform(scale, origin);
            var effective = _transformStack.Count > 0
                ? NowTransform.Compose(_transformStack[_transformStack.Count - 1], local)
                : local;
            _transformStack.Add(effective);
            return new NowTransformScope(true);
        }

        /// <summary>
        /// Pushes a transform scope with uniform scale.
        /// </summary>
        public static NowTransformScope Transform(float scale, Vector2 origin = default)
        {
            return Transform(new Vector2(scale, scale), origin);
        }

        internal static NowTransformSnapshot CaptureTransform()
        {
            return _transformStack.Count > 0
                ? new NowTransformSnapshot(true, _transformStack[_transformStack.Count - 1])
                : default;
        }

        internal static NowTransformScope ApplyTransformSnapshot(NowTransformSnapshot snapshot)
        {
            if (!snapshot.active)
                return default;

            _transformStack.Add(snapshot.transform);
            return new NowTransformScope(true);
        }

        internal static void PopTransform()
        {
            if (_transformStack.Count > 0)
                _transformStack.RemoveAt(_transformStack.Count - 1);
        }

        /// <summary>
        /// Applies current transform to a position. Returns the transformed position.
        /// </summary>
        static Vector2 ApplyTransform(Vector2 position)
        {
            if (_transformStack.Count == 0)
                return position;

            var transform = _transformStack[_transformStack.Count - 1];
            return new Vector2(
                position.x * transform.scale.x + transform.origin.x,
                position.y * transform.scale.y + transform.origin.y);
        }

        /// <summary>
        /// Applies current transform to a size. Returns the scaled size (no translation).
        /// </summary>
        static Vector2 ApplyTransformSize(Vector2 size)
        {
            if (_transformStack.Count == 0)
                return size;

            var transform = _transformStack[_transformStack.Count - 1];
            return new Vector2(size.x * Mathf.Abs(transform.scale.x), size.y * Mathf.Abs(transform.scale.y));
        }

        /// <summary>
        /// Applies current transform to a scalar value (like radius, outline width).
        /// Returns the scaled value using the larger scale component.
        /// </summary>
        static float ApplyTransformScalar(float value)
        {
            if (_transformStack.Count == 0)
                return value;

            var transform = _transformStack[_transformStack.Count - 1];
            return value * Mathf.Max(Mathf.Abs(transform.scale.x), Mathf.Abs(transform.scale.y));
        }

        /// <summary>
        /// Applies current transform to a rect. Returns a new transformed rect.
        /// </summary>
        static NowRect ApplyTransformRect(NowRect rect)
        {
            if (_transformStack.Count == 0)
                return rect;

            Vector2 a = ApplyTransform(rect.position);
            Vector2 b = ApplyTransform(new Vector2(rect.xMax, rect.yMax));
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float xMax = Mathf.Max(a.x, b.x);
            float yMax = Mathf.Max(a.y, b.y);
            return new NowRect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        public static bool hasTransform => _transformStack.Count > 0;

        public static NowRect TransformScreenRect(NowRect rect)
        {
            return _transformStack.Count > 0 ? ApplyTransformRect(rect) : rect;
        }

        internal static Vector2 InverseTransformScreenPoint(Vector2 position)
        {
            if (_transformStack.Count == 0)
                return position;

            var transform = _transformStack[_transformStack.Count - 1];
            return new Vector2(
                Mathf.Approximately(transform.scale.x, 0f) ? 0f : (position.x - transform.origin.x) / transform.scale.x,
                Mathf.Approximately(transform.scale.y, 0f) ? 0f : (position.y - transform.origin.y) / transform.scale.y);
        }

        internal static Vector2 InverseTransformScreenVector(Vector2 vector)
        {
            if (_transformStack.Count == 0)
                return vector;

            var transform = _transformStack[_transformStack.Count - 1];
            return new Vector2(
                Mathf.Approximately(transform.scale.x, 0f) ? 0f : vector.x / transform.scale.x,
                Mathf.Approximately(transform.scale.y, 0f) ? 0f : vector.y / transform.scale.y);
        }

        #endregion

        static int _defaultMesh = -1;

        static StaticList<NowMesh> _meshes = new StaticList<NowMesh>(100);

        static int _lastUsedMeshId = -1;

        static bool _captureMesh;

        static Mesh _legacyGlassReplayMesh;

        static readonly List<NowMeshBatch> _legacyGlassReplayBatches = new List<NowMeshBatch>(16);

        static CommandBuffer _legacyGlassCommandBuffer;

        sealed class MeshCaptureState
        {
            public StaticList<NowMesh> meshes;
            public int lastUsedMeshId;
            public bool captureMesh;
            public NowRect screenMask;
            public readonly List<NowRect> maskStack = new List<NowRect>(4);
            public readonly List<NowTransform> transformStack = new List<NowTransform>(4);
        }

        static readonly List<MeshCaptureState> _meshCaptureStack = new List<MeshCaptureState>(4);

        static readonly Stack<MeshCaptureState> _meshCaptureStatePool = new Stack<MeshCaptureState>(4);

        static readonly Stack<NowMesh[]> _meshCapturePool = new Stack<NowMesh[]>(4);

        static int _suppressDrawDepth;

        static readonly HashSet<NowFontAsset> _textGlyphResolveVisited = new HashSet<NowFontAsset>();

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

        static StaticList<NowRenderVertex> _renderVertices = new StaticList<NowRenderVertex>(NowMesh.INITIAL_VERTEX_CAPACITY);

        static StaticList<NowCanvasVertex> _canvasVertices = new StaticList<NowCanvasVertex>(NowMesh.INITIAL_VERTEX_CAPACITY);

        static StaticList<int> _triangles = new StaticList<int>(NowMesh.INITIAL_INDEX_CAPACITY);

        static readonly List<int> _capturedMeshIndices = new List<int>(8);

        static int CreateMesh(Material mat, NowMeshKind kind)
        {
            return CreateMesh(mat, null, kind);
        }

        static int CreateMesh(Material mat, Material canvasMaterial, NowMeshKind kind)
        {
            return CreateMesh(mat, canvasMaterial, kind, default);
        }

        static int CreateMesh(Material mat, Material canvasMaterial, NowMeshKind kind, Vector4 batchData)
        {
            _meshes.EnsureCapacity(1);
            int id = _meshes.count;

            if (_meshes.array[id] == null)
                _meshes.array[id] = new NowMesh(mat, canvasMaterial, kind, batchData);
            else
                _meshes.array[id].SetMaterial(mat, canvasMaterial, kind, batchData);

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

            int id = CreateMesh(material, mesh.canvasMaterial, kind, mesh.batchData);
            _lastUsedMeshId = id;
            return _meshes.array[id];
        }

        static void ReserveTextGlyphs(NowMesh mesh, int remainingGlyphs)
        {
            if (mesh == null || remainingGlyphs <= 0)
                return;

            int pageRoom = Mathf.Max(1, (MAX_VERTICES_PER_MESH - mesh.vertexCount) / 4);
            mesh.ReserveTextGlyphs(Mathf.Min(remainingGlyphs, pageRoom));
        }

        sealed class TextureMaterialEntry
        {
            public Material material;
            public int meshId = -1;
        }

        static readonly Dictionary<Texture, TextureMaterialEntry> _textureMaterials =
            new Dictionary<Texture, TextureMaterialEntry>();

        sealed class MaterialMeshEntry
        {
            public int meshId = -1;
        }

        static readonly Dictionary<Material, MaterialMeshEntry> _materialMeshes =
            new Dictionary<Material, MaterialMeshEntry>();

        readonly struct RectangleMaterialKey : IEquatable<RectangleMaterialKey>
        {
            readonly Material _material;

            readonly Material _canvasMaterial;

            readonly NowMeshKind _kind;

            public RectangleMaterialKey(Material material, Material canvasMaterial, NowMeshKind kind)
            {
                _material = material;
                _canvasMaterial = canvasMaterial;
                _kind = kind;
            }

            public bool Equals(RectangleMaterialKey other)
            {
                return ReferenceEquals(_material, other._material) &&
                    ReferenceEquals(_canvasMaterial, other._canvasMaterial) &&
                    _kind == other._kind;
            }

            public override bool Equals(object obj)
            {
                return obj is RectangleMaterialKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (_material != null ? RuntimeHelpers.GetHashCode(_material) : 0);
                    hash = hash * 31 + (_canvasMaterial != null ? RuntimeHelpers.GetHashCode(_canvasMaterial) : 0);
                    hash = hash * 31 + (int)_kind;
                    return hash;
                }
            }
        }

        readonly struct TexturedMaterialKey : IEquatable<TexturedMaterialKey>
        {
            readonly Material _material;

            readonly Texture _texture;

            public TexturedMaterialKey(Material material, Texture texture)
            {
                _material = material;
                _texture = texture;
            }

            public bool Equals(TexturedMaterialKey other)
            {
                return ReferenceEquals(_material, other._material) &&
                    ReferenceEquals(_texture, other._texture);
            }

            public override bool Equals(object obj)
            {
                return obj is TexturedMaterialKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (_material != null ? RuntimeHelpers.GetHashCode(_material) : 0);
                    hash = hash * 31 + (_texture != null ? RuntimeHelpers.GetHashCode(_texture) : 0);
                    return hash;
                }
            }
        }

        sealed class TexturedMaterialEntry
        {
            public Material material;
        }

        static readonly Dictionary<RectangleMaterialKey, MaterialMeshEntry> _rectangleMaterialMeshes =
            new Dictionary<RectangleMaterialKey, MaterialMeshEntry>();

        static readonly Dictionary<TexturedMaterialKey, TexturedMaterialEntry> _texturedMaterialCache =
            new Dictionary<TexturedMaterialKey, TexturedMaterialEntry>();

        static NowMesh UseTextureMaterial(Texture texture)
        {
            if (_defaultMaterial == null)
                return null;

            if (!_textureMaterials.TryGetValue(texture, out var entry) || entry.material == null)
            {
                entry = new TextureMaterialEntry
                {
                    material = new Material(_defaultMaterial)
                    {
                        name = "Now Textured Rect",
                        hideFlags = HideFlags.HideAndDontSave,
                        mainTexture = texture
                    }
                };
                _textureMaterials[texture] = entry;
            }

            return UseMaterial(entry.material, ref entry.meshId, NowMeshKind.TexturedRectangle);
        }

        static NowMesh UseSdfMaterial(Material material)
        {
            if (material == null)
                return null;

            if (!_materialMeshes.TryGetValue(material, out var entry))
            {
                entry = new MaterialMeshEntry();
                _materialMeshes[material] = entry;
            }

            return UseMaterial(material, ref entry.meshId, NowMeshKind.Sdf);
        }

        static NowMesh UseMaterial(Material material, ref int cachedMeshId, NowMeshKind kind)
        {
            return UseMaterial(material, null, ref cachedMeshId, kind);
        }

        static NowMesh UseMaterial(Material material, Material canvasMaterial, ref int cachedMeshId, NowMeshKind kind)
        {
            return UseMaterial(material, canvasMaterial, ref cachedMeshId, kind, default);
        }

        static NowMesh UseMaterial(
            Material material,
            Material canvasMaterial,
            ref int cachedMeshId,
            NowMeshKind kind,
            Vector4 batchData)
        {
            if (material == null)
                return null;

            if (_captureMesh)
            {
                if (_lastUsedMeshId >= 0 &&
                    _lastUsedMeshId < _meshes.count &&
                    ReferenceEquals(_meshes.array[_lastUsedMeshId].material, material) &&
                    ReferenceEquals(_meshes.array[_lastUsedMeshId].canvasMaterial, canvasMaterial) &&
                    _meshes.array[_lastUsedMeshId].kind == kind &&
                    _meshes.array[_lastUsedMeshId].batchData == batchData)
                {
                    return _meshes.array[_lastUsedMeshId];
                }

                int captureId = CreateMesh(material, canvasMaterial, kind, batchData);
                _lastUsedMeshId = captureId;
                return _meshes.array[captureId];
            }

            if (_lastUsedMeshId >= 0 &&
                _lastUsedMeshId < _meshes.count &&
                ReferenceEquals(_meshes.array[_lastUsedMeshId].material, material) &&
                ReferenceEquals(_meshes.array[_lastUsedMeshId].canvasMaterial, canvasMaterial) &&
                _meshes.array[_lastUsedMeshId].kind == kind &&
                _meshes.array[_lastUsedMeshId].batchData == batchData)
            {
                return _meshes.array[_lastUsedMeshId];
            }

            int orderedId = CreateMesh(material, canvasMaterial, kind, batchData);
            cachedMeshId = orderedId;
            _lastUsedMeshId = orderedId;
            return _meshes.array[orderedId];
        }

        internal static NowMesh UseEffectMaterial(Material material, ref int cachedMeshId, NowMeshKind kind)
        {
            return UseMaterial(material, ref cachedMeshId, kind);
        }

        static NowMesh UseRectangleMaterial(Material material, Material canvasMaterial, NowMeshKind kind)
        {
            if (material == null)
                return null;

            var key = new RectangleMaterialKey(material, canvasMaterial, kind);

            if (!_rectangleMaterialMeshes.TryGetValue(key, out var entry))
            {
                entry = new MaterialMeshEntry();
                _rectangleMaterialMeshes[key] = entry;
            }

            return UseMaterial(material, canvasMaterial, ref entry.meshId, kind);
        }

        static Material GetTexturedMaterial(Material source, Texture texture)
        {
            if (source == null)
                return null;

            if (texture == null)
                return source;

            var key = new TexturedMaterialKey(source, texture);

            if (!_texturedMaterialCache.TryGetValue(key, out var entry))
            {
                entry = new TexturedMaterialEntry();
                _texturedMaterialCache[key] = entry;
            }

            if (entry.material == null || entry.material.shader != source.shader)
            {
                entry.material = new Material(source)
                {
                    name = source.name + " Textured",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                entry.material.CopyPropertiesFromMaterial(source);
            }

            entry.material.mainTexture = texture;
            return entry.material;
        }

        static NowMesh UseCustomRectangleMaterial(NowRectangle rectangle)
        {
            Material material = rectangle.material;
            Material canvasMaterial = rectangle.canvasMaterial;

            if (material == null)
                material = rectangle.texture != null ? GetTexturedMaterial(_defaultMaterial, rectangle.texture) : _defaultMaterial;
            else if (rectangle.texture != null)
                material = GetTexturedMaterial(material, rectangle.texture);

            if (canvasMaterial != null && rectangle.texture != null)
                canvasMaterial = GetTexturedMaterial(canvasMaterial, rectangle.texture);

            return UseRectangleMaterial(material, canvasMaterial, NowMeshKind.CustomRectangle);
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

            if (mesh.kind == NowMeshKind.Glass)
            {
                NowGlassRenderer.DisableBackdropGlobal();
                var quality = NowGlassSettings.Resolve((NowGlassBlurQuality)Mathf.RoundToInt(mesh.batchData.w));
                NowGlassRenderer.RecordFallback(
                    "Legacy",
                    quality,
                    NowGlassFallbackReason.LegacyImmediatePath,
                    mesh.batchData.x,
                    mesh.GetBounds(Vector2.zero));
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

            try
            {
                DrawMesh(mesh, drawMatrix);
            }
            finally
            {
                GL.PopMatrix();
            }
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

        public static NowUIScreenScope StartUI()
        {
            return StartUI(1f);
        }

        /// <summary>
        /// Starts a frame whose UI units cover the whole screen at
        /// <paramref name="uiScale"/> pixels per unit. The projection stretches the
        /// scaled-down logical size across the full viewport and pointer input is
        /// converted into the same units, so drawing code never deals with pixels.
        /// Dispose the returned scope to submit rendering and finalize input for
        /// the frame.
        /// </summary>
        public static NowUIScreenScope StartUI(float uiScale)
        {
            if (uiScale <= 0f || float.IsNaN(uiScale) || float.IsInfinity(uiScale))
                throw new ArgumentOutOfRangeException(nameof(uiScale), "uiScale must be a positive finite value.");

            _captureMesh = false;
            _meshes.count = 0;
            _lastUsedMeshId = -1;
            _fontStack.Clear();
            _maskStack.Clear();
            _transformStack.Clear();
            _uiScale = uiScale;

            screenMask = new NowRect(0f, 0f, Screen.width / uiScale, Screen.height / uiScale);
            NowInput.Update(new NowInputSurface(
                new Vector2(screenMask.width, screenMask.height),
                new Rect(0f, 0f, Screen.width, Screen.height)));
            Initialize();
            return new NowUIScreenScope(true);
        }

        /// <summary>
        /// Starts a frame using an explicit screen-space mask as the logical UI
        /// surface. Dispose the returned scope to submit rendering and finalize
        /// input for the frame.
        /// </summary>
        public static NowUIScreenScope StartUI(NowRect screenMask)
        {
            _captureMesh = false;
            _meshes.count = 0;
            _lastUsedMeshId = -1;

            _fontStack.Clear();
            _maskStack.Clear();
            _transformStack.Clear();

            _uiScale = 1f;
            Now.screenMask = screenMask;
            NowInput.Update(NowInputSurface.FromScreenMask(screenMask));
            Initialize();
            return new NowUIScreenScope(true);
        }

        internal static void SetUIScale(float uiScale)
        {
            _uiScale = uiScale > 0f && !float.IsNaN(uiScale) && !float.IsInfinity(uiScale)
                ? uiScale
                : 1f;
        }

        internal static float ScreenPixelsToUiUnits(float pixels)
        {
            return pixels / _uiScale;
        }

        internal static float UiUnitsToScreenPixels(float units)
        {
            return units * _uiScale;
        }

        internal static void BeginMeshCapture(Vector4 screenMask)
        {
            BeginMeshCapture(screenMask, false);
        }

        internal static void BeginMeshCapture(Vector4 screenMask, bool inheritContext)
        {
            MeshCaptureState state = _meshCaptureStatePool.Count > 0
                ? _meshCaptureStatePool.Pop()
                : new MeshCaptureState();

            state.meshes = _meshes;
            state.lastUsedMeshId = _lastUsedMeshId;
            state.captureMesh = _captureMesh;
            state.screenMask = Now.screenMask;
            state.maskStack.Clear();
            state.maskStack.AddRange(_maskStack);
            state.transformStack.Clear();
            state.transformStack.AddRange(_transformStack);
            _meshCaptureStack.Add(state);

            Now.screenMask = screenMask;
            if (!inheritContext)
            {
                _maskStack.Clear();
                _transformStack.Clear();
            }
            Initialize();
            if (inheritContext)
            {
                _maskStack.AddRange(state.maskStack);
                _transformStack.AddRange(state.transformStack);
            }
            _captureMesh = true;
            _meshes = RentCaptureMeshes();
            _lastUsedMeshId = -1;
        }

        static StaticList<NowMesh> RentCaptureMeshes()
        {
            return new StaticList<NowMesh>
            {
                array = _meshCapturePool.Count > 0 ? _meshCapturePool.Pop() : new NowMesh[100],
                count = 0
            };
        }

        static void ReturnCaptureMeshes(StaticList<NowMesh> meshes)
        {
            if (meshes.array == null)
                return;

            for (int i = 0; i < meshes.count; ++i)
                meshes.array[i]?.ClearVertices();

            meshes.count = 0;
            _meshCapturePool.Push(meshes.array);
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

            var currentMeshes = _meshes;
            ReturnCaptureMeshes(currentMeshes);

            if (_meshCaptureStack.Count == 0)
            {
                _meshes = new StaticList<NowMesh>(100);
                _captureMesh = false;
                _lastUsedMeshId = -1;
                return;
            }

            var state = _meshCaptureStack[^1];
            _meshCaptureStack.RemoveAt(_meshCaptureStack.Count - 1);

            _meshes = state.meshes;
            _lastUsedMeshId = state.lastUsedMeshId;
            _captureMesh = state.captureMesh;
            Now.screenMask = state.screenMask;
            _maskStack.Clear();
            _maskStack.AddRange(state.maskStack);
            _transformStack.Clear();
            _transformStack.AddRange(state.transformStack);
            state.maskStack.Clear();
            state.transformStack.Clear();

            state.meshes = default;
            _meshCaptureStatePool.Push(state);
        }

        internal static void EndMeshCapture(Mesh target, List<NowMeshBatch> batches, Vector2 positionOffset, NowMeshLayout layout)
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

        /// <summary>A CanvasRenderer accepts at most 8 materials, so canvas pages are
        /// planned around at most 8 mesh/material pairs.</summary>
        const int MAX_MESHES_PER_CANVAS_PAGE = 8;

        static readonly List<int> _pageStarts = new List<int>(4);

        static readonly List<int> _pageCounts = new List<int>(4);

        internal static void EndCanvasMeshCapture(NowDrawList drawList, Vector2 positionOffset)
        {
            if (drawList == null)
            {
                CancelMeshCapture();
                return;
            }

            _pageStarts.Clear();
            _pageCounts.Clear();

            int activeIndex = 0;
            int pageStart = 0;
            int pageMeshes = 0;
            int pageVertices = 0;
            bool hasGlass = false;

            for (int i = 0; i < _meshes.count; ++i)
            {
                var mesh = _meshes.array[i];

                if (!mesh.hasVertices)
                    continue;

                hasGlass |= mesh.kind == NowMeshKind.Glass;

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
                    NowMeshLayout.Canvas,
                    pageIndex < _pageStarts.Count ? _pageStarts[pageIndex] : 0,
                    pageIndex < _pageCounts.Count ? _pageCounts[pageIndex] : 0);
            }

            if (hasGlass)
            {
                UploadCapturedMeshes(
                    drawList.EnsureRenderReplayMesh(),
                    drawList.renderReplayBatches,
                    Vector2.zero,
                    NowMeshLayout.Render,
                    0,
                    int.MaxValue);
            }
            else
            {
                drawList.ClearRenderReplay();
            }

            CancelMeshCapture();
        }

        static void UploadCapturedMeshes(
            Mesh target,
            List<NowMeshBatch> batches,
            Vector2 positionOffset,
            NowMeshLayout layout,
            int activeStart,
            int activeLimit)
        {
            if (target == null || batches == null)
                return;

            using var profile = NowProfiler.MeshUpload.Auto();
            bool useNativeRenderPacking = layout == NowMeshLayout.Render && NowLottieNative.packRenderAvailable;

            if (layout == NowMeshLayout.Canvas)
            {
                _canvasVertices.Clear();
            }
            else if (useNativeRenderPacking)
            {
                _renderVertices.Clear();
            }
            else
            {
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
            }

            batches.Clear();

            int activeIndex = 0;
            _capturedMeshIndices.Clear();
            NowRect combinedBounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _meshes.count; ++i)
            {
                var mesh = _meshes.array[i];

                if (!mesh.hasVertices)
                    continue;

                if (activeIndex++ < activeStart)
                    continue;

                if (_capturedMeshIndices.Count >= activeLimit)
                    break;

                var bounds = mesh.GetBounds(positionOffset);
                if (!bounds.isEmpty)
                {
                    combinedBounds = hasBounds ? combinedBounds.Union(bounds) : bounds;
                    hasBounds = true;
                }

                _capturedMeshIndices.Add(i);
                batches.Add(new NowMeshBatch(
                    mesh.material,
                    mesh.canvasMaterial,
                    mesh.kind,
                    mesh.batchData,
                    bounds));

                if (layout == NowMeshLayout.Canvas)
                {
                    mesh.AppendCanvasVertices(ref _canvasVertices, positionOffset);
                    continue;
                }

                if (useNativeRenderPacking)
                {
                    mesh.TryAppendNativeRenderVertices(ref _renderVertices, positionOffset);
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

            int vertexCount = layout == NowMeshLayout.Canvas
                ? _canvasVertices.count
                : useNativeRenderPacking ? _renderVertices.count : _vertices.count;

            if (vertexCount == 0)
            {
                return;
            }

            target.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            if (layout == NowMeshLayout.Canvas)
            {
                target.SetVertexBufferParams(vertexCount, NowMesh.CanvasVertexLayout);
                target.SetVertexBufferData(_canvasVertices.array, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            }
            else
            {
                if (useNativeRenderPacking)
                {
                    target.SetVertexBufferParams(vertexCount, NowMesh.RenderVertexLayout);
                    target.SetVertexBufferData(_renderVertices.array, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
                }
                else
                {
                    target.SetVertices(_vertices.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(0, _meshUvs.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(1, _rects.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(2, _radii.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(3, _colors.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(4, _outlineColors.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(5, _extras.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(6, _masks.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                    target.SetUVs(7, _rawUvs.array, 0, vertexCount, NowMesh.VertexStreamUploadFlags);
                }
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

            target.bounds = hasBounds ? NowMesh.ToUnityBounds(combinedBounds) : new Bounds(Vector3.zero, Vector3.zero);
        }

        internal static void FinishUIScreenFrame()
        {
            if (_captureMesh)
                return;

            using var profile = NowProfiler.ScreenFrameEnd.Auto();
            try
            {
                NowOverlay.Flush();

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

                try
                {
                    bool hasGlass = false;

                    for (int i = 0; i < count; ++i)
                    {
                        if (meshArray[i].hasVertices && meshArray[i].kind == NowMeshKind.Glass)
                        {
                            hasGlass = true;
                            break;
                        }
                    }

                    if (hasGlass && FlushLegacyGlassReplay(drawMatrix))
                    {
                        ClearImmediateMeshes(count);
                        return;
                    }

                    for (int i = 0; i < count; ++i)
                        DrawMesh(meshArray[i], drawMatrix);
                }
                finally
                {
                    GL.PopMatrix();
                }
            }
            finally
            {
                NowInput.EndFrame();
            }
        }

        static bool FlushLegacyGlassReplay(Matrix4x4 drawMatrix)
        {
            var replayMesh = EnsureLegacyGlassReplayMesh();

            if (replayMesh == null)
                return false;

            UploadCapturedMeshes(
                replayMesh,
                _legacyGlassReplayBatches,
                Vector2.zero,
                NowMeshLayout.Render,
                0,
                int.MaxValue);

            if (replayMesh.vertexCount == 0 || _legacyGlassReplayBatches.Count == 0)
                return false;

            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(UiUnitsToScreenPixels(screenMask.width)));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(UiUnitsToScreenPixels(screenMask.height)));

            for (int i = 0; i < _legacyGlassReplayBatches.Count; ++i)
            {
                var batch = _legacyGlassReplayBatches[i];

                if (batch.kind == NowMeshKind.Glass && NowGlassRenderer.CanDrawSelfReplay(batch))
                {
                    DrawLegacySelfReplayGlass(i, batch, drawMatrix, targetWidth, targetHeight);
                    continue;
                }

                DrawLegacyReplayBatch(i, drawMatrix, batch.kind == NowMeshKind.Glass);
            }

            NowGlassRenderer.DisableBackdropGlobal();
            return true;
        }

        static Mesh EnsureLegacyGlassReplayMesh()
        {
            if (_legacyGlassReplayMesh != null)
                return _legacyGlassReplayMesh;

            _legacyGlassReplayMesh = new Mesh
            {
                name = "Now Legacy Glass Replay",
                hideFlags = HideFlags.HideAndDontSave
            };
            _legacyGlassReplayMesh.MarkDynamic();
            return _legacyGlassReplayMesh;
        }

        static CommandBuffer EnsureLegacyGlassCommandBuffer()
        {
            return _legacyGlassCommandBuffer ??= new CommandBuffer
            {
                name = "Now Legacy Glass Backdrop"
            };
        }

        static void DrawLegacySelfReplayGlass(
            int batchIndex,
            in NowMeshBatch batch,
            Matrix4x4 drawMatrix,
            int targetWidth,
            int targetHeight)
        {
            var capture = NowGlassRenderer.GetCaptureRect(
                batch.bounds,
                new Vector2(screenMask.width, screenMask.height),
                targetWidth,
                targetHeight,
                batch.data.x);
            var source = RenderTexture.GetTemporary(capture.width, capture.height, 0, RenderTextureFormat.ARGB32);
            var blurred = RenderTexture.GetTemporary(capture.width, capture.height, 0, RenderTextureFormat.ARGB32);
            source.filterMode = FilterMode.Bilinear;
            source.wrapMode = TextureWrapMode.Clamp;
            blurred.filterMode = FilterMode.Bilinear;
            blurred.wrapMode = TextureWrapMode.Clamp;

            var previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = source;
                GL.PushMatrix();
                try
                {
                    GL.LoadIdentity();
                    GL.LoadProjectionMatrix(Matrix4x4.Ortho(0, capture.uiRect.width, -capture.uiRect.height, 0, -1, 100));
                    GL.Clear(true, true, Color.clear);
                    var replayMatrix = drawMatrix * Matrix4x4.Translate(new Vector3(-capture.uiRect.x, capture.uiRect.y, 0f));

                    for (int i = 0; i < batchIndex; ++i)
                        DrawLegacyReplayBatch(i, replayMatrix, false);
                }
                finally
                {
                    GL.PopMatrix();
                }

                var commandBuffer = EnsureLegacyGlassCommandBuffer();
                commandBuffer.Clear();
                NowGlassRenderer.CopyAndBlurBackdrop(
                    commandBuffer,
                    source,
                    blurred,
                    capture.width,
                    capture.height,
                    batch.data.x,
                    NowGlassRenderer.GetBatchQuality(batch),
                    "LegacySelfReplay",
                    capture.uiRect,
                    out _);
                Graphics.ExecuteCommandBuffer(commandBuffer);

                RenderTexture.active = previousActive;
                NowGlassRenderer.EnableBackdropGlobal(blurred, capture.backdropUvTransform);
                DrawLegacyReplayBatch(batchIndex, drawMatrix, false);
                NowGlassRenderer.DisableBackdropGlobal();
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(blurred);
                RenderTexture.ReleaseTemporary(source);
            }
        }

        static void DrawLegacyReplayBatch(int batchIndex, Matrix4x4 drawMatrix, bool recordGlassFallback)
        {
            if (_legacyGlassReplayMesh == null ||
                batchIndex < 0 ||
                batchIndex >= _legacyGlassReplayBatches.Count)
            {
                return;
            }

            var batch = _legacyGlassReplayBatches[batchIndex];

            if (batch.material == null)
                return;

            if (batch.kind == NowMeshKind.Glass)
            {
                NowGlassRenderer.DisableBackdropGlobal();

                if (recordGlassFallback)
                {
                    NowGlassRenderer.RecordFallback(
                        "Legacy",
                        NowGlassRenderer.GetBatchQuality(batch),
                        NowGlassFallbackReason.MissingBlurMaterial,
                        batch.data.x,
                        batch.bounds);
                }
            }

            batch.material.SetPass(0);
            Graphics.DrawMeshNow(_legacyGlassReplayMesh, drawMatrix, batchIndex);
        }

        static void ClearImmediateMeshes(int count)
        {
            var safeCount = Mathf.Min(count, _meshes.count);

            for (int i = 0; i < safeCount; ++i)
                _meshes.array[i]?.ClearVertices();
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

        static float RectangleVisualPadding(float blur, float outline)
        {
            return 2f + Mathf.Max(0f, blur) + Mathf.Max(0f, outline);
        }

        /// <summary>Draws a rounded rectangle. The style constructor defaults the mask to
        /// the rect itself; that default mask is outset so the SDF edge, outline and blur
        /// fall off instead of clipping the anti-aliasing hard at the bounds. Explicit
        /// masks stay exact.</summary>
        internal static void DrawRect(NowRectangle rectangle)
        {
            if (_suppressDrawDepth > 0 || _defaultMaterial == null)
                return;

            var position = rectangle.rect;
            var pad = rectangle.padding;

            position.x += pad.x;
            position.y += pad.y;
            position.width = position.width - pad.x - pad.z;
            position.height = position.height - pad.y - pad.w;

            if (rectangle.texture != null && rectangle.preserveAspect && !rectangle.sliced &&
                position.width > 0f && position.height > 0f)
            {
                float sourceAspect = rectangle.uvRect.z * rectangle.texture.width /
                    Mathf.Max(rectangle.uvRect.w * rectangle.texture.height, 1f);
                float rectAspect = position.width / position.height;

                if (rectAspect > sourceAspect)
                {
                    float fitted = position.height * sourceAspect;
                    position.x += (position.width - fitted) * 0.5f;
                    position.width = fitted;
                }
                else
                {
                    float fitted = position.width / sourceAspect;
                    position.y += (position.height - fitted) * 0.5f;
                    position.height = fitted;
                }
            }

            // Snap to pixels by rounding EDGES, not origin + size: truncating them
            // independently shifted fractional-position rects by up to a pixel, so
            // nested glyphs (a radio dot inside its circle) came out visibly
            // off-center depending on where layout placed the control.
            // Skip pixel snapping when transform is active for smooth zooming.
            bool hasTransform = _transformStack.Count > 0;
            float x0, y0, rectWidth, rectHeight;

            if (hasTransform)
            {
                x0 = position.x;
                y0 = position.y;
                rectWidth = position.width;
                rectHeight = position.height;
            }
            else
            {
                x0 = Mathf.RoundToInt(position.x);
                y0 = Mathf.RoundToInt(position.y);
                rectWidth = Mathf.RoundToInt(position.x + position.width) - x0;
                rectHeight = Mathf.RoundToInt(position.y + position.height) - y0;
            }

            if (rectWidth <= 0 || rectHeight <= 0)
                return;

            var rectMask = rectangle.mask;
            float visualPadding = RectangleVisualPadding(rectangle.blur, rectangle.outline);

            float blur = rectangle.blur;
            float outline = rectangle.outline;
            Vector4 radius = rectangle.radius;
            float geometryPadding = visualPadding;

            if (hasTransform)
            {
                float s = ApplyTransformScalar(1f);
                blur *= s;
                outline *= s;
                radius *= s;
                geometryPadding = RectangleVisualPadding(blur, outline);
            }

            if (!rectMask.isEmpty && rectMask == rectangle.rect)
                rectMask = rectMask.Outset(visualPadding);

            if (hasTransform)
                rectMask = ApplyTransformRect(rectMask);

            _tmpVertex.mask = ApplyAmbientMask(rectMask);
            _tmpVertex.radius = radius;
            _tmpVertex.color = ApplyColorMultiplier(rectangle.color);
            _tmpVertex.outlineColor = ApplyColorMultiplier(rectangle.outlineColor);
            _tmpVertex.uvwh = rectangle.uvRect;

            NowMesh mesh;

            if (rectangle.material != null || rectangle.canvasMaterial != null)
            {
                mesh = UseCustomRectangleMaterial(rectangle);

                if (mesh == null)
                    return;

                int quads = rectangle.sliced ? 9 : 1;
                mesh = EnsureMeshCapacity(mesh, mesh.material, NowMeshKind.CustomRectangle, quads * 4);

                if (rectangle.sliced)
                {
                    DrawSliced(rectangle, mesh, x0, y0, rectWidth, rectHeight, hasTransform);
                    return;
                }
            }
            else if (rectangle.texture != null)
            {
                mesh = UseTextureMaterial(rectangle.texture);

                if (mesh == null)
                    return;

                int quads = rectangle.sliced ? 9 : 1;
                mesh = EnsureMeshCapacity(mesh, mesh.material, NowMeshKind.TexturedRectangle, quads * 4);

                if (rectangle.sliced)
                {
                    DrawSliced(rectangle, mesh, x0, y0, rectWidth, rectHeight, hasTransform);
                    return;
                }
            }
            else
            {
                mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

                if (mesh == null)
                    return;

                mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, 4);
            }

            // Apply transform to position and scale to size
            if (hasTransform)
            {
                Vector2 transformedPos = ApplyTransform(new Vector2(x0, y0));
                Vector2 scaledSize = ApplyTransformSize(new Vector2(rectWidth, rectHeight));
                _tmpVertex.position.x = transformedPos.x;
                _tmpVertex.position.y = -transformedPos.y - scaledSize.y;
                _tmpVertex.position.z = scaledSize.x;
                _tmpVertex.position.w = scaledSize.y;
            }
            else
            {
                _tmpVertex.position.x = x0;
                _tmpVertex.position.y = -y0 - rectHeight;
                _tmpVertex.position.z = rectWidth;
                _tmpVertex.position.w = rectHeight;
            }

            mesh.AddRect(_tmpVertex, blur, outline, geometryPadding);
        }

        /// <summary>
        /// Draws a material-driven full-rect effect using the same quad, mask,
        /// texture coordinate and canvas capture path as NowRectangle. Extensions
        /// provide the material shader and per-material data.
        /// </summary>
        internal static void DrawSdf(NowRect rect, NowRect mask, Material material, Vector4 color)
        {
            if (_suppressDrawDepth > 0 || material == null || rect.width <= 0f || rect.height <= 0f)
                return;

            bool hasTransform = _transformStack.Count > 0;

            float x0, y0, rectWidth, rectHeight;

            if (hasTransform)
            {
                // When transform is active, use float positions for smooth scaling
                x0 = rect.x;
                y0 = rect.y;
                rectWidth = rect.width;
                rectHeight = rect.height;
            }
            else
            {
                // Use pixel rounding for crisp edges when no transform
                x0 = Mathf.RoundToInt(rect.x);
                y0 = Mathf.RoundToInt(rect.y);
                rectWidth = Mathf.RoundToInt(rect.x + rect.width) - x0;
                rectHeight = Mathf.RoundToInt(rect.y + rect.height) - y0;
            }

            if (rectWidth <= 0 || rectHeight <= 0)
                return;

            if (!mask.isEmpty && mask == rect)
                mask = mask.Outset(2f);

            if (hasTransform && !mask.isEmpty)
                mask = ApplyTransformRect(mask);

            _tmpVertex.mask = ApplyAmbientMask(mask);
            _tmpVertex.radius = default;
            _tmpVertex.color = ApplyColorMultiplier(color);
            _tmpVertex.outlineColor = default;
            _tmpVertex.uvwh = _defaultUV;

            Vector2 size = new Vector2(rectWidth, rectHeight);
            Vector2 position;

            if (hasTransform)
            {
                Vector2 top = ApplyTransform(new Vector2(x0, y0));
                size = ApplyTransformSize(size);
                position = new Vector2(top.x, -top.y - size.y);
            }
            else
            {
                position = new Vector2(x0, -y0 - rectHeight);
            }

            _tmpVertex.position.x = position.x;
            _tmpVertex.position.y = position.y;
            _tmpVertex.position.z = size.x;
            _tmpVertex.position.w = size.y;

            var mesh = UseSdfMaterial(material);

            if (mesh == null)
                return;

            mesh = EnsureMeshCapacity(mesh, material, NowMeshKind.Sdf, 4);
            mesh.AddRect(_tmpVertex, 0f, 0f);
        }

        /// <summary>
        /// Emits the nine quads of a sliced sprite: corners at source pixel size
        /// (scaled down when the rect is smaller than the borders), edges and the
        /// center stretched. Cell edges share rounded coordinates so slices never
        /// seam. Radius, outline and blur do not apply.
        /// </summary>
        static void DrawSliced(NowRectangle rectangle, NowMesh mesh, float x0, float y0, float rectWidth, float rectHeight, bool hasTransform)
        {
            Vector4 border = rectangle.spriteBorder;
            float sourceWidth = Mathf.Max(rectangle.spritePixelSize.x, 1f);
            float sourceHeight = Mathf.Max(rectangle.spritePixelSize.y, 1f);

            float scale = Mathf.Min(
                1f,
                rectWidth / Mathf.Max(border.x + border.z, 1f),
                rectHeight / Mathf.Max(border.y + border.w, 1f));

            // When transform is active, use float positions for smooth scaling
            float xLeft, xRight, yTop, yBottom;

            if (hasTransform)
            {
                xLeft = x0 + border.x * scale;
                xRight = x0 + rectWidth - border.z * scale;
                yTop = y0 + border.w * scale;
                yBottom = y0 + rectHeight - border.y * scale;
            }
            else
            {
                xLeft = x0 + Mathf.RoundToInt(border.x * scale);
                xRight = x0 + rectWidth - Mathf.RoundToInt(border.z * scale);
                yTop = y0 + Mathf.RoundToInt(border.w * scale);
                yBottom = y0 + rectHeight - Mathf.RoundToInt(border.y * scale);
            }

            Vector4 uv = rectangle.uvRect;
            float uLeft = uv.z * (border.x / sourceWidth);
            float uRight = uv.z * (border.z / sourceWidth);
            float vBottom = uv.w * (border.y / sourceHeight);
            float vTop = uv.w * (border.w / sourceHeight);

            _tmpVertex.radius = default;

            // Define 9-slice grid positions
            Span<float> xs = stackalloc float[4] { x0, xLeft, xRight, x0 + rectWidth };
            Span<float> ys = stackalloc float[4] { y0, yTop, yBottom, y0 + rectHeight };
            Span<float> us = stackalloc float[4] { uv.x, uv.x + uLeft, uv.x + uv.z - uRight, uv.x + uv.z };
            Span<float> vs = stackalloc float[4] { uv.y + uv.w, uv.y + uv.w - vTop, uv.y + vBottom, uv.y };

            for (int row = 0; row < 3; ++row)
            {
                float cellY = ys[row];
                float cellHeight = ys[row + 1] - cellY;

                if (cellHeight <= 0)
                    continue;

                for (int col = 0; col < 3; ++col)
                {
                    float cellX = xs[col];
                    float cellWidth = xs[col + 1] - cellX;

                    if (cellWidth <= 0)
                        continue;

                    // Apply transform to each cell
                    if (hasTransform)
                    {
                        Vector2 transformedPos = ApplyTransform(new Vector2(cellX, cellY));
                        Vector2 scaledSize = ApplyTransformSize(new Vector2(cellWidth, cellHeight));
                        _tmpVertex.position.x = transformedPos.x;
                        _tmpVertex.position.y = -transformedPos.y - scaledSize.y;
                        _tmpVertex.position.z = scaledSize.x;
                        _tmpVertex.position.w = scaledSize.y;
                    }
                    else
                    {
                        _tmpVertex.position.x = Mathf.RoundToInt(cellX);
                        _tmpVertex.position.y = -Mathf.RoundToInt(cellY + cellHeight);
                        _tmpVertex.position.z = Mathf.RoundToInt(cellWidth);
                        _tmpVertex.position.w = Mathf.RoundToInt(cellHeight);
                    }

                    _tmpVertex.uvwh = new Vector4(
                        us[col],
                        vs[row + 1],
                        us[col + 1] - us[col],
                        vs[row] - vs[row + 1]);

                    mesh.AddRect(_tmpVertex, 0f, 0f);
                }
            }
        }

        /// <summary>Draws a text block. The default mask (= the layout rect) is outset
        /// because glyphs legitimately overhang the advance box — descenders, italics;
        /// explicit masks stay exact, and empty masks mean "no mask". Fully masked
        /// text skips all shaping/glyph work, so scrolled-out content costs nothing.</summary>
        internal static void DrawString(NowText style, string value)
        {
            if (_suppressDrawDepth > 0 || string.IsNullOrEmpty(value) || !style.font)
                return;

            using var profile = NowProfiler.TextDraw.Auto();

            if (!style.mask.isEmpty && style.mask == style.rect)
                style.mask = style.mask.Outset(4f);

            // Transform only the mask to screen-space when transform is active
            // (rect transformation is handled by DrawStringCodepoints)
            bool hasTransform = _transformStack.Count > 0;
            if (hasTransform && !style.mask.isEmpty)
                style.mask = ApplyTransformRect(style.mask);

            style.mask = ApplyAmbientMask(style.mask);

            NowRect overlapRect = hasTransform ? ApplyTransformRect(style.rect) : style.rect;

            if (style.mask.isEmpty || !style.mask.Overlaps(overlapRect.Outset(8f)))
                return;

            if (textShaping && TryDrawShapedString(style, value))
                return;

            if (style.font.TryResolveFont(style.fontStyle, out var preparedFont) &&
                preparedFont != null &&
                preparedFont.TryGetPreparedCodepointRun(value, style.fontSize, style.fontStyle, 4, out var preparedRun))
            {
                DrawPreparedCodepointRun(style, preparedFont, preparedRun);
                return;
            }

            if (ShouldEnsureGlyphsBeforeCodepointDraw(style.font))
                style.font.EnsureGlyphs(value, style.fontSize, style.fontStyle);

            DrawStringCodepoints(style, value.AsSpan());
        }

        /// <summary>
        /// Span draw for dynamic text (counters, timers) without allocating a
        /// string. Always the per-codepoint path — HarfBuzz shaping is keyed by
        /// string and does not apply to spans.
        /// </summary>
        internal static void DrawString(NowText style, ReadOnlySpan<char> value)
        {
            if (_suppressDrawDepth > 0 || value.IsEmpty || !style.font)
                return;

            using var profile = NowProfiler.TextDraw.Auto();

            if (!style.mask.isEmpty && style.mask == style.rect)
                style.mask = style.mask.Outset(4f);

            // Transform only the mask to screen-space when transform is active
            // (rect transformation is handled by DrawStringCodepoints)
            bool hasTransform = _transformStack.Count > 0;
            if (hasTransform && !style.mask.isEmpty)
                style.mask = ApplyTransformRect(style.mask);

            style.mask = ApplyAmbientMask(style.mask);

            NowRect overlapRect = hasTransform ? ApplyTransformRect(style.rect) : style.rect;

            if (style.mask.isEmpty || !style.mask.Overlaps(overlapRect.Outset(8f)))
                return;

            DrawStringCodepoints(style, value);
        }

        static bool ShouldEnsureGlyphsBeforeCodepointDraw(NowFontAsset fontAsset)
        {
            if (fontAsset is NowFont font && !font.HasEmbeddedSource)
            {
                var fallbacks = fontAsset.fallbacks;
                return fallbacks != null && fallbacks.Count > 0;
            }

            return true;
        }

        static void DrawStringCodepoints(NowText style, ReadOnlySpan<char> value)
        {
            bool hasTransform = _transformStack.Count > 0;

            var fontSize = style.fontSize;
            var fontAsset = style.font;
            var color = ApplyColorMultiplier(style.color);
            var outlineColor = ApplyColorMultiplier(style.outlineColor);
            NowMesh mesh = null;
            NowFont pixelRangeFont = null;
            Material pixelRangeMaterial = null;
            float pixelRange = 0f;

            // Scale font size by transform for consistent text size
            float textScale = hasTransform ? ApplyTransformScalar(1f) : 1f;
            float scaledFontSize = fontSize * textScale;
            float scaledBaseline = fontAsset.GetAscender(style.fontStyle) * scaledFontSize;
            float scaledOutline = style.outline * scaledFontSize;

            // Use original font size for layout calculations when transform is active
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
                        if (TryResolveTextGlyph(fontAsset, ' ', fontSize, style.fontStyle, out _, out var space, out _))
                            style.rect.x += space.advance * fontSize * TAB_SPACES;
                        break;
                    }
                    default:
                    {
                        if (!TryResolveTextGlyph(
                            fontAsset,
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

                                ReserveTextGlyphs(mesh, value.Length - i);
                            }

                            if (!ReferenceEquals(pixelRangeFont, resolvedFont) ||
                                !ReferenceEquals(pixelRangeMaterial, glyphMaterial))
                            {
                                pixelRange = resolvedFont.GetScreenPixelRange(glyph.unicode, fontSize) * textScale;
                                pixelRangeFont = resolvedFont;
                                pixelRangeMaterial = glyphMaterial;
                            }

                            var previousMesh = mesh;
                            mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);
                            if (!ReferenceEquals(mesh, previousMesh))
                                ReserveTextGlyphs(mesh, value.Length - i);

                            // Transform position before drawing glyph
                            float glyphX = style.rect.x;
                            float glyphY = style.rect.y;

                            if (hasTransform)
                            {
                                Vector2 transformedPos = ApplyTransform(new Vector2(glyphX, glyphY));
                                glyphX = transformedPos.x;
                                glyphY = transformedPos.y;
                            }

                            mesh.AddTextGlyphReserved(
                                glyph,
                                glyphX,
                                glyphY,
                                scaledFontSize,
                                scaledBaseline,
                                style.mask,
                                color,
                                outlineColor,
                                scaledOutline,
                                pixelRange);
                        }

                        style.rect.x += glyph.advance * fontSize;
                        break;
                    }
                }
            }
        }

        static bool TryResolveTextGlyph(
            NowFontAsset fontAsset,
            int codepoint,
            float fontSize,
            NowFontStyle style,
            out NowFont font,
            out NowFontAtlasInfo.Glyph glyph,
            out Material material)
        {
            font = null;
            glyph = default;
            material = null;

            if (fontAsset == null)
                return false;

            if (fontAsset is NowFont directFont &&
                directFont.GetGlyph(codepoint, fontSize, out glyph, out material))
            {
                font = directFont;
                return true;
            }

            var fallbacks = fontAsset.fallbacks;

            if ((fallbacks == null || fallbacks.Count == 0) && style == NowFontStyle.Regular)
                return false;

            var visited = _textGlyphResolveVisited;

            try
            {
                visited.Clear();

                if (fontAsset.TryResolveGlyph(codepoint, fontSize, style, visited, out font, out glyph, out material))
                    return true;

                if (style == NowFontStyle.Regular)
                    return false;

                visited.Clear();
                return fontAsset.TryResolveGlyph(codepoint, fontSize, NowFontStyle.Regular, visited, out font, out glyph, out material);
            }
            finally
            {
                visited.Clear();
            }
        }

        static void DrawPreparedCodepointRun(NowText style, NowFont font, NowFont.PreparedCodepointRun run)
        {
            bool hasTransform = _transformStack.Count > 0;
            var fontSize = style.fontSize;
            var color = ApplyColorMultiplier(style.color);
            var outlineColor = ApplyColorMultiplier(style.outlineColor);
            NowMesh mesh = null;
            NowFont pixelRangeFont = null;
            Material pixelRangeMaterial = null;
            float pixelRange = 0f;
            float textScale = hasTransform ? ApplyTransformScalar(1f) : 1f;
            float scaledFontSize = fontSize * textScale;
            float scaledBaseline = style.font.GetAscender(style.fontStyle) * scaledFontSize;
            float scaledOutline = style.outline * scaledFontSize;
            float lineHeight = style.font.GetLineHeight(style.fontStyle) * fontSize;
            float leftPos = style.rect.x;

            if (!hasTransform)
            {
                DrawPreparedCodepointRunUntransformed(
                    ref style,
                    font,
                    run,
                    fontSize,
                    scaledBaseline,
                    color,
                    outlineColor,
                    scaledOutline,
                    lineHeight,
                    leftPos,
                    ref mesh,
                    ref pixelRangeFont,
                    ref pixelRangeMaterial,
                    ref pixelRange);
                return;
            }

            var glyphs = run.glyphs;
            bool reservedCurrentMesh = false;

            for (int i = 0; i < run.length; ++i)
            {
                var prepared = glyphs[i];

                if (prepared.lineBreak)
                {
                    style.rect.x = leftPos;
                    style.rect.y += lineHeight;
                    reservedCurrentMesh = false;
                    continue;
                }

                if (prepared.visible)
                {
                    var glyphMaterial = prepared.material;
                    bool sameMaterial = mesh != null && ReferenceEquals(mesh.material, glyphMaterial);

                    if (!sameMaterial)
                    {
                        int materialId = font.GetMaterialId(prepared.codepoint, fontSize);
                        mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                        font.SetMaterialId(prepared.codepoint, fontSize, materialId);

                        if (mesh == null)
                            return;

                        ReserveTextGlyphs(mesh, run.length - i);
                        reservedCurrentMesh = true;
                    }
                    else if (!reservedCurrentMesh)
                    {
                        ReserveTextGlyphs(mesh, run.length - i);
                        reservedCurrentMesh = true;
                    }

                    if (!ReferenceEquals(pixelRangeFont, font) ||
                        !ReferenceEquals(pixelRangeMaterial, glyphMaterial))
                    {
                        pixelRange = font.GetScreenPixelRange(prepared.glyph.unicode, fontSize) * textScale;
                        pixelRangeFont = font;
                        pixelRangeMaterial = glyphMaterial;
                    }

                    var previousMesh = mesh;
                    mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);
                    if (!ReferenceEquals(mesh, previousMesh))
                        ReserveTextGlyphs(mesh, run.length - i);

                    float glyphX = style.rect.x;
                    float glyphY = style.rect.y;
                    Vector2 transformedPos = ApplyTransform(new Vector2(glyphX, glyphY));

                    mesh.AddTextGlyphReserved(
                        prepared.glyph,
                        transformedPos.x,
                        transformedPos.y,
                        scaledFontSize,
                        scaledBaseline,
                        style.mask,
                        color,
                        outlineColor,
                        scaledOutline,
                        pixelRange);
                }

                style.rect.x += prepared.advance * fontSize;
            }
        }

        static void DrawPreparedCodepointRunUntransformed(
            ref NowText style,
            NowFont font,
            NowFont.PreparedCodepointRun run,
            float fontSize,
            float baseline,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float lineHeight,
            float leftPos,
            ref NowMesh mesh,
            ref NowFont pixelRangeFont,
            ref Material pixelRangeMaterial,
            ref float pixelRange)
        {
            int i = 0;
            float penX = style.rect.x;
            var glyphs = run.glyphs;

            while (i < run.length)
            {
                var prepared = glyphs[i];

                if (prepared.lineBreak)
                {
                    penX = leftPos;
                    style.rect.y += lineHeight;
                    ++i;
                    continue;
                }

                if (!prepared.visible)
                {
                    penX += prepared.advance * fontSize;
                    ++i;
                    continue;
                }

                var glyphMaterial = prepared.material;
                int materialId = font.GetMaterialId(prepared.codepoint, fontSize);
                mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                font.SetMaterialId(prepared.codepoint, fontSize, materialId);

                if (mesh == null)
                    return;

                if (!ReferenceEquals(pixelRangeFont, font) ||
                    !ReferenceEquals(pixelRangeMaterial, glyphMaterial))
                {
                    pixelRange = font.GetScreenPixelRange(prepared.glyph.unicode, fontSize);
                    pixelRangeFont = font;
                    pixelRangeMaterial = glyphMaterial;
                }

                mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);

                if (mesh == null)
                    return;

                int pageRoom = _captureMesh
                    ? Mathf.Max(1, (MAX_VERTICES_PER_MESH - mesh.vertexCount) / 4)
                    : run.length - i;
                int end = i + 1;
                int visibleGlyphs = 1;

                while (end < run.length && visibleGlyphs < pageRoom)
                {
                    var next = glyphs[end];

                    if (next.lineBreak)
                        break;

                    if (next.visible)
                    {
                        if (!ReferenceEquals(next.material, glyphMaterial))
                            break;

                        ++visibleGlyphs;
                    }

                    ++end;
                }

                penX = mesh.AddCodepointTextRunReserved(
                    run,
                    i,
                    end,
                    penX,
                    style.rect.y,
                    fontSize,
                    baseline,
                    style.mask,
                    color,
                    outlineColor,
                    outline,
                    pixelRange);
                i = end;
            }

            style.rect.x = penX;
        }

        /// <summary>
        /// Draws the text through HarfBuzz-shaped runs (ligatures, kerning, complex
        /// scripts). Returns false — drawing nothing — when any segment cannot be
        /// shaped or baked, so the per-codepoint path can take over cleanly; the
        /// validation pass runs before any geometry is emitted to make the handoff
        /// all-or-nothing. Tabs advance by four spaces, matching the codepoint path's
        /// TAB_SPACES.
        /// </summary>
        static bool TryDrawShapedString(NowText style, string value)
        {
            if (!style.font.TryResolveFont(style.fontStyle, out var font) || font == null)
                return false;

            var fontSize = style.fontSize;

            if (!HasShapedControlCharacters(value))
                return TryDrawSingleShapedLine(style, font, value, fontSize);

            bool hasTab = false;

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
                    string segment = segmentStart == 0 && i == value.Length
                        ? value
                        : value.Substring(segmentStart, i - segmentStart);

                    if (!font.TryGetPreparedShapedRun(segment, fontSize, out _))
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

                tabAdvance *= fontSize * 4;
            }

            float lineHeight = style.font.GetLineHeight(style.fontStyle) * fontSize;
            float baseline = style.font.GetAscender(style.fontStyle) * fontSize;
            float leftPos = style.rect.x;
            var color = ApplyColorMultiplier(style.color);
            var outlineColor = ApplyColorMultiplier(style.outlineColor);
            float outline = style.outline * fontSize;
            NowMesh mesh = null;
            NowFont pixelRangeFont = null;
            Material pixelRangeMaterial = null;
            float pixelRange = 0f;
            segmentStart = 0;

            for (int i = 0; i <= value.Length; ++i)
            {
                char control = i < value.Length ? value[i] : '\0';

                if (i < value.Length && control != '\n' && control != '\t')
                    continue;

                if (i > segmentStart)
                {
                    string segment = segmentStart == 0 && i == value.Length
                        ? value
                        : value.Substring(segmentStart, i - segmentStart);
                    font.TryGetPreparedShapedRun(segment, fontSize, out var run);

                    if (!AppendShapedRun(
                        ref style,
                        font,
                        run,
                        fontSize,
                        baseline,
                        color,
                        outlineColor,
                        outline,
                        ref mesh,
                        ref pixelRangeFont,
                        ref pixelRangeMaterial,
                        ref pixelRange))
                    {
                        return true;
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

        static bool HasShapedControlCharacters(string value)
        {
            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];

                if (character == '\n' || character == '\t')
                    return true;
            }

            return false;
        }

        static bool TryDrawSingleShapedLine(NowText style, NowFont font, string value, float fontSize)
        {
            if (!font.TryGetPreparedShapedRun(value, fontSize, out var run))
                return false;

            float baseline = style.font.GetAscender(style.fontStyle) * fontSize;
            var color = ApplyColorMultiplier(style.color);
            var outlineColor = ApplyColorMultiplier(style.outlineColor);
            float outline = style.outline * fontSize;
            NowMesh mesh = null;
            NowFont pixelRangeFont = null;
            Material pixelRangeMaterial = null;
            float pixelRange = 0f;

            AppendShapedRun(
                ref style,
                font,
                run,
                fontSize,
                baseline,
                color,
                outlineColor,
                outline,
                ref mesh,
                ref pixelRangeFont,
                ref pixelRangeMaterial,
                ref pixelRange);

            return true;
        }

        static bool AppendShapedRun(
            ref NowText style,
            NowFont font,
            NowFont.PreparedShapedRun run,
            float fontSize,
            float baseline,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            ref NowMesh mesh,
            ref NowFont pixelRangeFont,
            ref Material pixelRangeMaterial,
            ref float pixelRange)
        {
            bool hasTransform = _transformStack.Count > 0;

            if (!hasTransform)
            {
                return AppendShapedRunUntransformed(
                    ref style,
                    font,
                    run,
                    fontSize,
                    baseline,
                    color,
                    outlineColor,
                    outline,
                    ref mesh,
                    ref pixelRangeFont,
                    ref pixelRangeMaterial,
                    ref pixelRange);
            }

            float textScale = hasTransform ? ApplyTransformScalar(1f) : 1f;
            float scaledFontSize = fontSize * textScale;
            float scaledBaseline = baseline * textScale;
            float scaledOutline = outline * textScale;
            bool reservedCurrentMesh = false;

            var glyphs = run.glyphs;

            for (int g = 0; g < run.length; ++g)
            {
                var shaped = glyphs[g];

                var glyph = shaped.glyph;
                var glyphMaterial = shaped.material;

                if (shaped.visible)
                {
                    bool sameMaterial = mesh != null && ReferenceEquals(mesh.material, glyphMaterial);

                    if (!sameMaterial)
                    {
                        int materialId = font.GetMaterialId(shaped.encodedKey, fontSize);
                        mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                        font.SetMaterialId(shaped.encodedKey, fontSize, materialId);

                        if (mesh == null)
                            return false;

                        ReserveTextGlyphs(mesh, run.length - g);
                        reservedCurrentMesh = true;
                    }
                    else if (!reservedCurrentMesh)
                    {
                        ReserveTextGlyphs(mesh, run.length - g);
                        reservedCurrentMesh = true;
                    }

                    if (!ReferenceEquals(pixelRangeFont, font) ||
                        !ReferenceEquals(pixelRangeMaterial, glyphMaterial))
                    {
                        pixelRange = font.GetScreenPixelRange(glyph.unicode, fontSize) * textScale;
                        pixelRangeFont = font;
                        pixelRangeMaterial = glyphMaterial;
                    }

                    var previousMesh = mesh;
                    mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);
                    if (!ReferenceEquals(mesh, previousMesh))
                        ReserveTextGlyphs(mesh, run.length - g);

                    float glyphX = style.rect.x + shaped.xOffset * fontSize;
                    float glyphY = style.rect.y - shaped.yOffset * fontSize;

                    if (hasTransform)
                    {
                        Vector2 transformedPos = ApplyTransform(new Vector2(glyphX, glyphY));
                        glyphX = transformedPos.x;
                        glyphY = transformedPos.y;
                    }

                    mesh.AddTextGlyphReserved(
                        glyph,
                        glyphX,
                        glyphY,
                        scaledFontSize,
                        scaledBaseline,
                        style.mask,
                        color,
                        outlineColor,
                        scaledOutline,
                        pixelRange);
                }

                style.rect.x += shaped.xAdvance * fontSize;
            }

            return true;
        }

        static bool AppendShapedRunUntransformed(
            ref NowText style,
            NowFont font,
            NowFont.PreparedShapedRun run,
            float fontSize,
            float baseline,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            ref NowMesh mesh,
            ref NowFont pixelRangeFont,
            ref Material pixelRangeMaterial,
            ref float pixelRange)
        {
            int g = 0;
            float penX = style.rect.x;
            var glyphs = run.glyphs;

            while (g < run.length)
            {
                var shaped = glyphs[g];

                if (!shaped.visible)
                {
                    penX += shaped.xAdvance * fontSize;
                    ++g;
                    continue;
                }

                var glyph = shaped.glyph;
                var glyphMaterial = shaped.material;
                int materialId = font.GetMaterialId(shaped.encodedKey, fontSize);
                mesh = UseMaterial(glyphMaterial, ref materialId, NowMeshKind.Text);
                font.SetMaterialId(shaped.encodedKey, fontSize, materialId);

                if (mesh == null)
                    return false;

                if (!ReferenceEquals(pixelRangeFont, font) ||
                    !ReferenceEquals(pixelRangeMaterial, glyphMaterial))
                {
                    pixelRange = font.GetScreenPixelRange(glyph.unicode, fontSize);
                    pixelRangeFont = font;
                    pixelRangeMaterial = glyphMaterial;
                }

                mesh = EnsureMeshCapacity(mesh, glyphMaterial, NowMeshKind.Text, 4);

                if (mesh == null)
                    return false;

                int pageRoom = _captureMesh
                    ? Mathf.Max(1, (MAX_VERTICES_PER_MESH - mesh.vertexCount) / 4)
                    : run.length - g;
                int end = g + 1;
                int visibleGlyphs = 1;

                while (end < run.length && visibleGlyphs < pageRoom)
                {
                    var next = glyphs[end];

                    if (next.visible)
                    {
                        if (!ReferenceEquals(next.material, glyphMaterial))
                            break;

                        ++visibleGlyphs;
                    }

                    ++end;
                }

                penX = mesh.AddShapedTextRunReserved(
                    run,
                    g,
                    end,
                    penX,
                    style.rect.y,
                    fontSize,
                    baseline,
                    style.mask,
                    color,
                    outlineColor,
                    outline,
                    pixelRange);
                g = end;
            }

            style.rect.x = penX;
            return true;
        }

        internal static void DrawCharacter(NowText style, NowFontAtlasInfo.Glyph glyph)
        {
            if (_suppressDrawDepth > 0 || style.font == null)
                return;

            if (!style.mask.isEmpty && style.mask == style.rect)
                style.mask = style.mask.Outset(4f);

            // Transform only the mask to screen-space when transform is active
            bool hasTransform = _transformStack.Count > 0;
            if (hasTransform && !style.mask.isEmpty)
                style.mask = ApplyTransformRect(style.mask);

            style.mask = ApplyAmbientMask(style.mask);

            NowRect overlapRect = hasTransform ? ApplyTransformRect(style.rect) : style.rect;

            if (style.mask.isEmpty || !style.mask.Overlaps(overlapRect.Outset(8f)))
                return;

            if (!style.font.TryResolveFont(style.fontStyle, out var resolvedFont))
                return;

            DrawCharacter(style, glyph, resolvedFont);
        }

        internal static void DrawCharacter(NowText style, NowFontAtlasInfo.Glyph glyph, NowFont font)
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

        static void DrawCharacter(NowText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh)
        {
            float baseline = (style.font != null ? style.font.GetAscender(style.fontStyle) : font.GetAscender()) * style.fontSize;
            DrawCharacter(style, glyph, font, mesh, baseline);
        }

        /// <summary>
        /// <paramref name="baseline"/> is the distance from the top of the line box
        /// to the text baseline (ascender), so descenders stay inside the measured
        /// line height instead of hanging below the rect.
        /// </summary>
        static void DrawCharacter(NowText style, NowFontAtlasInfo.Glyph glyph, NowFont font, NowMesh mesh, float baseline)
        {
            bool hasTransform = _transformStack.Count > 0;
            var fontSize = style.fontSize;
            float textScale = hasTransform ? ApplyTransformScalar(1f) : 1f;
            float scaledFontSize = fontSize * textScale;
            float scaledBaseline = baseline * textScale;

            float glyphX = style.rect.x;
            float glyphY = style.rect.y;

            if (hasTransform)
            {
                Vector2 transformedPos = ApplyTransform(new Vector2(glyphX, glyphY));
                glyphX = transformedPos.x;
                glyphY = transformedPos.y;
            }

            mesh.AddTextGlyph(
                glyph,
                glyphX,
                glyphY,
                scaledFontSize,
                scaledBaseline,
                style.mask,
                ApplyColorMultiplier(style.color),
                ApplyColorMultiplier(style.outlineColor),
                style.outline * scaledFontSize,
                font.GetScreenPixelRange(glyph.unicode, fontSize) * textScale);
        }

        internal static void DrawLottie(NowLottie lottie)
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

            bool hasTransform = _transformStack.Count > 0;
            NowRect drawRect = hasTransform ? ApplyTransformRect(lottie.rect) : lottie.rect;
            var lottieMask = !lottie.mask.isEmpty && lottie.mask == lottie.rect ? lottie.mask.Outset(2f) : lottie.mask;

            if (hasTransform && !lottieMask.isEmpty)
                lottieMask = ApplyTransformRect(lottieMask);

            lottieMask = ApplyAmbientMask(lottieMask);

            if (lottieMask.isEmpty || !lottieMask.Overlaps(drawRect))
                return;

            var mesh = UseMaterial(_defaultMaterial, ref _defaultMesh, NowMeshKind.Rectangle);

            if (mesh == null)
                return;

            float frame = NowLottieRenderer.TimeToFrame(composition, lottie.time, lottie.loop);

            if (lottie.playbackFrameRate > 0f && composition.frameRate > lottie.playbackFrameRate)
            {
                float step = composition.frameRate / lottie.playbackFrameRate;
                frame = Mathf.Floor(frame / step) * step;
            }

            frame = Mathf.Round(frame);

            float renderScale = Mathf.Max(_uiScale, 0.0001f);
            float maxSize = NowLottieRenderer.maxRenderSize;
            float maxDimension = Mathf.Max(drawRect.width, drawRect.height) * renderScale;

            if (maxSize > 0f && maxDimension > maxSize)
                renderScale *= maxSize / maxDimension;

            var buffer = NowLottieRenderer.RenderCached(
                composition,
                frame,
                drawRect.width * renderScale,
                drawRect.height * renderScale,
                lottie.preserveAspect);

            mesh = EnsureMeshCapacity(mesh, _defaultMaterial, NowMeshKind.Rectangle, buffer.positions.count);
            mesh.AddGeometry(buffer, new Vector2(drawRect.x, drawRect.y), 1f / renderScale, tint, lottieMask);
        }

        public static NowRectangle Rectangle(NowRectangle rect)
        {
            return rect;
        }

        public static NowRectangle Rectangle(NowRect position)
        {
            return new NowRectangle(position);
        }

        public static NowText Text(NowRect position, NowFontAsset font)
        {
            return new NowText(position, font);
        }

        public static NowText Text(NowRect position)
        {
            return new NowText(position, defaultFont);
        }

        public static NowLottie Lottie(NowRect position, NowLottieAsset asset)
        {
            return new NowLottie(position, asset);
        }
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Now.StartUI()"/>; disposing
    /// finalizes overlays, screen rendering, and input capture even when the
    /// frame exits early.
    /// </summary>
    [NowScope]
    public struct NowUIScreenScope : IDisposable
    {
        bool _active;

        internal NowUIScreenScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            Now.FinishUIScreenFrame();
        }
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Now.Mask(NowRect)"/>; disposing
    /// restores the previous ambient mask.
    /// </summary>
    [NowScope]
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
    [NowScope]
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

    /// <summary>
    /// Disposable handle returned by <see cref="Now.Transform(float)"/>;
    /// disposing restores the previously active transform.
    /// </summary>
    [NowScope]
    public struct NowTransformScope : IDisposable
    {
        bool _active;

        internal NowTransformScope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;

            _active = false;
            Now.PopTransform();
        }
    }
}
