using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace NowUI.Internal
{
    public enum NowMeshKind
    {
        Rectangle,
        Text,
        TexturedRectangle,
        CustomRectangle,
        Ripple,
        Sdf,
        Glass,
        Bezier,
        Gradient
    }

    internal struct NowMeshBatch
    {
        public readonly Material material;

        public readonly Material canvasMaterial;

        public readonly NowMeshKind kind;

        public readonly Vector4 data;

        public readonly NowRect bounds;

        /// <summary>Deferred overlay geometry (popups, menus, tooltips) drawn after regular content.</summary>
        public readonly bool overlay;

        /// <summary>Analytic masks active when this ordered material batch was emitted.</summary>
        public readonly NowMaskShaderState maskState;

        public NowMeshBatch(Material material, NowMeshKind kind)
            : this(material, null, kind, default)
        {
        }

        public NowMeshBatch(
            Material material,
            Material canvasMaterial,
            NowMeshKind kind,
            Vector4 data,
            NowRect bounds = default,
            bool overlay = false,
            in NowMaskShaderState maskState = default)
        {
            this.material = material;
            this.canvasMaterial = canvasMaterial;
            this.kind = kind;
            this.data = data;
            this.bounds = bounds;
            this.overlay = overlay;
            this.maskState = maskState;
        }
    }

    internal enum NowMeshLayout
    {
        Render,
        Canvas
    }

    public struct StaticList<T>
    {
        public int count;

        public T[] array;

        public StaticList(int count)
        {
            this.count = 0;
            array = new T[count];
        }

        public void Clear()
        {
            count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int additionalCount)
        {
            int requiredCapacity = count + additionalCount;
            int currentCapacity = array?.Length ?? 0;

            if (requiredCapacity <= currentCapacity)
                return;

            int newCapacity = Mathf.Max(requiredCapacity, currentCapacity > 0 ? currentCapacity * 2 : 4);
            System.Array.Resize(ref array, newCapacity);
        }
    }

    /// <summary>
    /// Interleaved canvas vertex matching the UGUI shader inputs. Field order must
    /// match the VertexAttributeDescriptor layout in NowUI (Position, Normal,
    /// Tangent, Color, TexCoord0..3, all float32).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct NowCanvasVertex
    {
        public Vector3 position;

        public Vector3 normal;

        public Vector4 tangent;

        public Color color;

        public Vector4 uv0;

        public Vector4 uv1;

        public Vector4 uv2;

        public Vector4 uv3;
    }

    /// <summary>
    /// Interleaved render vertex matching the non-UGUI shader inputs. Field order
    /// must match <see cref="NowMesh.RenderVertexLayout"/>.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct NowRenderVertex
    {
        public Vector3 position;

        public Vector2 uv0;

        public Vector4 rect;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 outlineColor;

        public Vector4 extra;

        public Vector4 mask;

        public Vector4 rawUv;
    }

    public struct NowRectVertex
    {
        public Vector4 mask;

        public Vector4 position;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 outlineColor;

        public Vector4 uvwh;

        public readonly bool IsOutsideMask(Vector4 rect)
        {
            return rect.x + rect.z < mask.x ||
                rect.x >= mask.x + mask.z ||
                -rect.y < mask.y ||
                -rect.y - rect.w >= mask.y + mask.w;
        }
    }

    public class NowMesh
    {
        const int INITIAL_RECT_CAPACITY = 64;

        const int VERTICES_PER_RECT = 4;

        const int INDICES_PER_RECT = 6;

        public const int INITIAL_VERTEX_CAPACITY = INITIAL_RECT_CAPACITY * VERTICES_PER_RECT;

        public const int INITIAL_INDEX_CAPACITY = INITIAL_RECT_CAPACITY * INDICES_PER_RECT;

        internal const MeshUpdateFlags VertexStreamUploadFlags = MeshUpdateFlags.DontRecalculateBounds;

        /// <summary>
        /// Index data comes straight from internal emitters that already guarantee
        /// in-range indices, and callers assign explicit bounds after upload, so
        /// validation, bounds recalculation, and user notification are all skipped.
        /// </summary>
        internal const MeshUpdateFlags IndexUploadFlags =
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontNotifyMeshUsers;

        /// <summary>
        /// Shared 16-bit index staging buffer. Safe as a static because
        /// <see cref="UploadMesh"/> only runs on the main thread (Unity Mesh API).
        /// </summary>
        static StaticList<ushort> _indexScratch = new StaticList<ushort>(INITIAL_INDEX_CAPACITY);

        internal static readonly VertexAttributeDescriptor[] RenderVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord6, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord7, VertexAttributeFormat.Float32, 4),
        };

        internal static readonly VertexAttributeDescriptor[] CanvasVertexLayout =
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

        public Mesh unityMesh {get; private set;}

        public bool hasVertices => _verts.count > 0;

        public int vertexCount => _verts.count;

        StaticList<Vector4> _rawuv;

        StaticList<Vector4> _mask;

        StaticList<Vector4> _extra;

        StaticList<Vector4> _outlineColor;

        StaticList<Vector4> _color;

        StaticList<Vector4> _rect;

        StaticList<Vector4> _radius;

        StaticList<Vector3> _verts;

        StaticList<Vector2> _uvs;

        StaticList<int> _tris;

        StaticList<NowRenderVertex> _renderVertices;

        bool _hasBounds;

        float _boundsMinX;

        float _boundsMinY;

        float _boundsMaxX;

        float _boundsMaxY;

        public Material material;

        public Material canvasMaterial;

        public NowMeshKind kind;

        public Vector4 batchData;

        internal NowMaskShaderState maskState;

        public NowMesh(Material mat, NowMeshKind kind)
            : this(mat, null, kind)
        {
        }

        public NowMesh(Material mat, Material canvasMaterial, NowMeshKind kind, Vector4 batchData = default)
            : this(mat, canvasMaterial, kind, batchData, default)
        {
        }

        internal NowMesh(
            Material mat,
            Material canvasMaterial,
            NowMeshKind kind,
            Vector4 batchData,
            in NowMaskShaderState maskState)
        {
            material = mat;
            this.canvasMaterial = canvasMaterial;
            this.kind = kind;
            this.batchData = batchData;
            this.maskState = maskState;

            _radius = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _rect = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _verts = new StaticList<Vector3>(INITIAL_VERTEX_CAPACITY);
            _uvs = new StaticList<Vector2>(INITIAL_VERTEX_CAPACITY);
            _color = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _outlineColor = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _extra = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _tris = new StaticList<int>(INITIAL_INDEX_CAPACITY);
            _mask = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _rawuv = new StaticList<Vector4>(INITIAL_VERTEX_CAPACITY);
            _renderVertices = new StaticList<NowRenderVertex>(INITIAL_VERTEX_CAPACITY);
        }

        public void SetMaterial(Material material, NowMeshKind kind)
        {
            SetMaterial(material, null, kind);
        }

        public void SetMaterial(Material material, Material canvasMaterial, NowMeshKind kind, Vector4 batchData = default)
        {
            SetMaterial(material, canvasMaterial, kind, batchData, default);
        }

        internal void SetMaterial(
            Material material,
            Material canvasMaterial,
            NowMeshKind kind,
            Vector4 batchData,
            in NowMaskShaderState maskState)
        {
            ClearVertices();
            this.material = material;
            this.canvasMaterial = canvasMaterial;
            this.kind = kind;
            this.batchData = batchData;
            this.maskState = maskState;
        }

        static readonly Vector2 _uv0 = new Vector2(0, 0);

        static readonly Vector2 _uv1 = new Vector2(0, 1);

        static readonly Vector2 _uv2 = new Vector2(1, 1);

        static readonly Vector2 _uv3 = new Vector2(1, 0);

        void ClearBounds()
        {
            _hasBounds = false;
            _boundsMinX = 0f;
            _boundsMinY = 0f;
            _boundsMaxX = 0f;
            _boundsMaxY = 0f;
        }

        void EncapsulateBounds(float minX, float minY, float maxX, float maxY)
        {
            if (!_hasBounds)
            {
                _hasBounds = true;
                _boundsMinX = minX;
                _boundsMinY = minY;
                _boundsMaxX = maxX;
                _boundsMaxY = maxY;
                return;
            }

            _boundsMinX = Mathf.Min(_boundsMinX, minX);
            _boundsMinY = Mathf.Min(_boundsMinY, minY);
            _boundsMaxX = Mathf.Max(_boundsMaxX, maxX);
            _boundsMaxY = Mathf.Max(_boundsMaxY, maxY);
        }

        void EncapsulateVertex(Vector3 vertex)
        {
            EncapsulateBounds(vertex.x, vertex.y, vertex.x, vertex.y);
        }

        void EncapsulateRect(Vector4 rect)
        {
            EncapsulateBounds(rect.x, rect.y, rect.x + rect.z, rect.y + rect.w);
        }

        void EnsureRectCapacity()
        {
            if (HasRectCapacity())
                return;

            _mask.EnsureCapacity(VERTICES_PER_RECT);
            _rect.EnsureCapacity(VERTICES_PER_RECT);
            _radius.EnsureCapacity(VERTICES_PER_RECT);
            _color.EnsureCapacity(VERTICES_PER_RECT);
            _outlineColor.EnsureCapacity(VERTICES_PER_RECT);
            _extra.EnsureCapacity(VERTICES_PER_RECT);
            _verts.EnsureCapacity(VERTICES_PER_RECT);
            _uvs.EnsureCapacity(VERTICES_PER_RECT);
            _rawuv.EnsureCapacity(VERTICES_PER_RECT);
            _tris.EnsureCapacity(INDICES_PER_RECT);
        }

        public void AddRect(in NowRectVertex vertexData, float extraX, float extraY)
        {
            AddRect(vertexData, extraX, extraY, 0f);
        }

        public void AddRect(in NowRectVertex vertexData, float extraX, float extraY, float geometryPadding)
        {
            Vector4 extra = default;
            extra.x = extraX;
            extra.y = extraY;
            AddRect(vertexData, extra, geometryPadding);
        }

        public void AddRect(in NowRectVertex vertexData, Vector4 extra)
        {
            AddRect(vertexData, extra, 0f);
        }

        public void AddRect(in NowRectVertex vertexData, Vector4 extra, float geometryPadding)
        {
            var position = vertexData.position;
            float padding = Mathf.Max(0f, geometryPadding);
            Vector4 geometry = position;

            if (padding > 0f)
            {
                geometry.x -= padding;
                geometry.y -= padding;
                geometry.z += padding * 2f;
                geometry.w += padding * 2f;
            }

            if (vertexData.IsOutsideMask(geometry)) return;

            EnsureRectCapacity();
            EncapsulateRect(geometry);

            int indexOffset = _verts.count;

            var maskarr = _mask.array;
            var maskcount = _mask.count;

            var mask = vertexData.mask;

            maskarr[maskcount] = mask;
            maskarr[maskcount + 1] = mask;
            maskarr[maskcount + 2] = mask;
            maskarr[maskcount + 3] = mask;

            _mask.count += 4;

            var rarray = _rect.array;
            var rcount = _rect.count;

            rarray[rcount] = position;
            rarray[rcount + 1] = position;
            rarray[rcount + 2] = position;
            rarray[rcount + 3] = position;

            _rect.count += 4;

            var rrad= _radius.array;
            var rrcount = _radius.count;

            rrad[rrcount] = vertexData.radius;
            rrad[rrcount + 1] = vertexData.radius;
            rrad[rrcount + 2] = vertexData.radius;
            rrad[rrcount + 3] = vertexData.radius;

            _radius.count += 4;

            var rcol = _color.array;
            var ccount = _color.count;

            rcol[ccount] = vertexData.color;
            rcol[ccount + 1] = vertexData.color;
            rcol[ccount + 2] = vertexData.color;
            rcol[ccount + 3] = vertexData.color;

            _color.count += 4;

            var rout = _outlineColor.array;
            var ocount = _outlineColor.count;

            rout[ocount] = vertexData.outlineColor;
            rout[ocount + 1] = vertexData.outlineColor;
            rout[ocount + 2] = vertexData.outlineColor;
            rout[ocount + 3] = vertexData.outlineColor;

            _outlineColor.count += 4;

            var rextra = _extra.array;
            var extraC = _extra.count;

            rextra[extraC] = extra;
            rextra[extraC + 1] = extra;
            rextra[extraC + 2] = extra;
            rextra[extraC + 3] = extra;

            _extra.count += 4;

            Vector3 cornerA = default, cornerB = default, cornerC = default, cornerD = default;

            cornerA.x = geometry.x;
            cornerA.y = geometry.y;

            cornerB.x = cornerA.x;
            cornerB.y = cornerA.y + geometry.w;

            cornerC.x = cornerA.x + geometry.z;
            cornerC.y = cornerB.y;

            cornerD.x = cornerC.x;
            cornerD.y = cornerA.y;

            var varr = _verts.array;
            var vcount = _verts.count;

            varr[vcount++] = cornerA;
            varr[vcount++] = cornerB;
            varr[vcount++] = cornerC;
            varr[vcount++] = cornerD;

            _verts.count = vcount;

            var ruvs = _uvs.array;
            var ruvsCount = _uvs.count;

            float leftRaw = 0f;
            float rightRaw = 1f;
            float bottomRaw = 0f;
            float topRaw = 1f;

            if (padding > 0f && position.z > 0f && position.w > 0f)
            {
                leftRaw = -padding / position.z;
                rightRaw = 1f + padding / position.z;
                bottomRaw = -padding / position.w;
                topRaw = 1f + padding / position.w;
            }

            var uvwh = vertexData.uvwh;

            var raw0 = new Vector2(leftRaw, bottomRaw);
            var raw1 = new Vector2(leftRaw, topRaw);
            var raw2 = new Vector2(rightRaw, topRaw);
            var raw3 = new Vector2(rightRaw, bottomRaw);

            var uv0 = new Vector2(uvwh.x + raw0.x * uvwh.z, uvwh.y + raw0.y * uvwh.w);
            var uv1 = new Vector2(uvwh.x + raw1.x * uvwh.z, uvwh.y + raw1.y * uvwh.w);
            var uv2 = new Vector2(uvwh.x + raw2.x * uvwh.z, uvwh.y + raw2.y * uvwh.w);
            var uv3 = new Vector2(uvwh.x + raw3.x * uvwh.z, uvwh.y + raw3.y * uvwh.w);

            ruvs[ruvsCount] = uv0;
            ruvs[ruvsCount + 1] = uv1;
            ruvs[ruvsCount + 2] = uv2;
            ruvs[ruvsCount + 3] = uv3;

            var rawuvs = _rawuv.array;
            var rawuvsCount = _rawuv.count;

            rawuvs[rawuvsCount] = raw0;
            rawuvs[rawuvsCount + 1] = raw1;
            rawuvs[rawuvsCount + 2] = raw2;
            rawuvs[rawuvsCount + 3] = raw3;

            _rawuv.count += 4;
            _uvs.count += 4;

            int triCount = _tris.count;
            var triArr = _tris.array;

            triArr[triCount + 0] = indexOffset + 0;
            triArr[triCount + 1] = indexOffset + 1;
            triArr[triCount + 2] = indexOffset + 2;
            triArr[triCount + 3] = indexOffset + 0;
            triArr[triCount + 4] = indexOffset + 2;
            triArr[triCount + 5] = indexOffset + 3;

            _tris.count += 6;
        }

        /// <summary>
        /// Emits the ordinary rounded-rectangle quad but reserves UV0.xy for the
        /// gradient shader's packed per-instance tint. Raw UV, SDF, mask, outline,
        /// and padding streams stay identical to rectangles.
        /// </summary>
        internal void AddGradientRect(
            in NowRectVertex vertexData,
            Vector4 extra,
            float geometryPadding,
            Vector2 packedTint)
        {
            int uvStart = _uvs.count;
            AddRect(vertexData, extra, geometryPadding);

            if (_uvs.count == uvStart)
                return;

            var uvs = _uvs.array;
            uvs[uvStart] = packedTint;
            uvs[uvStart + 1] = packedTint;
            uvs[uvStart + 2] = packedTint;
            uvs[uvStart + 3] = packedTint;
        }

        internal void EnsureRawCapacity(int vertexCount, int indexCount)
        {
            if (vertexCount > 0)
            {
                _mask.EnsureCapacity(vertexCount);
                _rect.EnsureCapacity(vertexCount);
                _radius.EnsureCapacity(vertexCount);
                _color.EnsureCapacity(vertexCount);
                _outlineColor.EnsureCapacity(vertexCount);
                _extra.EnsureCapacity(vertexCount);
                _verts.EnsureCapacity(vertexCount);
                _uvs.EnsureCapacity(vertexCount);
                _rawuv.EnsureCapacity(vertexCount);
            }

            if (indexCount > 0)
                _tris.EnsureCapacity(indexCount);
        }

        internal int AddRawVertex(
            Vector3 vertex,
            Vector2 uv,
            Vector4 rect,
            Vector4 radius,
            Vector4 color,
            Vector4 outlineColor,
            Vector4 extra,
            Vector4 mask,
            Vector4 rawUv)
        {
            EnsureRawCapacity(1, 0);
            return AddRawVertexUnchecked(vertex, uv, rect, radius, color, outlineColor, extra, mask, rawUv);
        }

        internal int AddRawVertexUnchecked(
            Vector3 vertex,
            Vector2 uv,
            Vector4 rect,
            Vector4 radius,
            Vector4 color,
            Vector4 outlineColor,
            Vector4 extra,
            Vector4 mask,
            Vector4 rawUv)
        {
            int index = _verts.count;
            _verts.array[_verts.count++] = vertex;
            EncapsulateVertex(vertex);
            _uvs.array[_uvs.count++] = uv;
            _rect.array[_rect.count++] = rect;
            _radius.array[_radius.count++] = radius;
            _color.array[_color.count++] = color;
            _outlineColor.array[_outlineColor.count++] = outlineColor;
            _extra.array[_extra.count++] = extra;
            _mask.array[_mask.count++] = mask;
            _rawuv.array[_rawuv.count++] = rawUv;
            return index;
        }

        internal void AddRawTriangle(int a, int b, int c)
        {
            EnsureRawCapacity(0, 3);
            AddRawTriangleUnchecked(a, b, c);
        }

        internal void AddRawTriangleUnchecked(int a, int b, int c)
        {
            _tris.array[_tris.count++] = a;
            _tris.array[_tris.count++] = b;
            _tris.array[_tris.count++] = c;
        }

        public void AddTextGlyph(
            NowFontAtlasInfo.Glyph glyph,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            Vector4 gradient = default,
            float encodedGradient = 0f,
            bool outlineOnly = false)
        {
            ReserveTextGlyphs(1);
            AddTextGlyphReserved(
                glyph,
                x,
                y,
                fontSize,
                baseline,
                mask,
                color,
                outlineColor,
                outline,
                pixelRange,
                gradient,
                encodedGradient,
                outlineOnly);
        }

        static float ClampTextOutlineToRange(float outline, float pixelRange)
        {
            if (float.IsNaN(outline) || float.IsInfinity(outline))
                return 0f;

            // Keep the threshold just inside the encoded field so filtering and
            // antialiasing never expose the rectangular edge of a glyph cell when
            // a static atlas or configured atlas cap cannot satisfy the request.
            float maxOutline = NowFont.GetSafeSdfEffectReach(pixelRange);
            return Mathf.Clamp(outline, -maxOutline, maxOutline);
        }

        internal void ReserveTextGlyphs(int glyphCount)
        {
            if (glyphCount <= 0)
                return;

            int vertexCount = glyphCount * VERTICES_PER_RECT;
            int indexCount = glyphCount * INDICES_PER_RECT;
            _mask.EnsureCapacity(vertexCount);
            _rect.EnsureCapacity(vertexCount);
            _radius.EnsureCapacity(vertexCount);
            _color.EnsureCapacity(vertexCount);
            _outlineColor.EnsureCapacity(vertexCount);
            _extra.EnsureCapacity(vertexCount);
            _verts.EnsureCapacity(vertexCount);
            _uvs.EnsureCapacity(vertexCount);
            _rawuv.EnsureCapacity(vertexCount);
            _tris.EnsureCapacity(indexCount);
        }

        /// <summary>
        /// Every append path grows all vertex streams in lockstep (same counts, same
        /// EnsureCapacity calls, same initial capacity), so checking one vertex stream
        /// and the index stream covers them all. Rects and text glyphs share the same
        /// 4-vertex/6-index footprint.
        /// </summary>
        bool HasRectCapacity()
        {
            return
                _verts.count + VERTICES_PER_RECT <= _verts.array.Length &&
                _tris.count + INDICES_PER_RECT <= _tris.array.Length;
        }

        internal void AddTextGlyphReserved(
            NowFontAtlasInfo.Glyph glyph,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            Vector4 gradient = default,
            float encodedGradient = 0f,
            bool outlineOnly = false)
        {
            var planeBounds = glyph.planeBounds;
            float left = planeBounds.left * fontSize;
            float right = planeBounds.right * fontSize;
            float bottom = planeBounds.bottom * fontSize;
            float top = planeBounds.top * fontSize;

            float width = right - left;
            float height = top - bottom;

            if (width <= 0f || height <= 0f)
                return;

            float px = x + left;
            float py = y - bottom + baseline - height;
            Vector4 position = new Vector4(px, -(py + height), width, height);

            if (position.x + position.z < mask.x ||
                position.x >= mask.x + mask.z ||
                -position.y < mask.y ||
                -position.y - position.w >= mask.y + mask.w)
            {
                return;
            }

            EncapsulateRect(position);

            if (!HasRectCapacity())
                ReserveTextGlyphs(1);

            int indexOffset = _verts.count;
            var atlasBounds = glyph.atlasBounds;
            Vector4 extra = default;
            extra.x = ClampTextOutlineToRange(outline, pixelRange);
            extra.y = outlineOnly ? -Mathf.Abs(pixelRange) : pixelRange;
            extra.z = gradient.w;
            extra.w = encodedGradient;

            var maskArray = _mask.array;
            int maskCount = _mask.count;
            maskArray[maskCount] = mask;
            maskArray[maskCount + 1] = mask;
            maskArray[maskCount + 2] = mask;
            maskArray[maskCount + 3] = mask;
            _mask.count += 4;

            var rectArray = _rect.array;
            int rectCount = _rect.count;
            rectArray[rectCount] = position;
            rectArray[rectCount + 1] = position;
            rectArray[rectCount + 2] = position;
            rectArray[rectCount + 3] = position;
            _rect.count += 4;

            var radiusArray = _radius.array;
            int radiusCount = _radius.count;
            Vector4 gradientXYZ = new Vector4(gradient.x, gradient.y, gradient.z, 0f);
            radiusArray[radiusCount] = gradientXYZ;
            radiusArray[radiusCount + 1] = gradientXYZ;
            radiusArray[radiusCount + 2] = gradientXYZ;
            radiusArray[radiusCount + 3] = gradientXYZ;
            _radius.count += 4;

            var colorArray = _color.array;
            int colorCount = _color.count;
            colorArray[colorCount] = color;
            colorArray[colorCount + 1] = color;
            colorArray[colorCount + 2] = color;
            colorArray[colorCount + 3] = color;
            _color.count += 4;

            var outlineArray = _outlineColor.array;
            int outlineCount = _outlineColor.count;
            outlineArray[outlineCount] = outlineColor;
            outlineArray[outlineCount + 1] = outlineColor;
            outlineArray[outlineCount + 2] = outlineColor;
            outlineArray[outlineCount + 3] = outlineColor;
            _outlineColor.count += 4;

            var extraArray = _extra.array;
            int extraCount = _extra.count;
            extraArray[extraCount] = extra;
            extraArray[extraCount + 1] = extra;
            extraArray[extraCount + 2] = extra;
            extraArray[extraCount + 3] = extra;
            _extra.count += 4;

            var vertexArray = _verts.array;
            int vcount = _verts.count;
            vertexArray[vcount] = new Vector3(position.x, position.y, 0f);
            vertexArray[vcount + 1] = new Vector3(position.x, position.y + position.w, 0f);
            vertexArray[vcount + 2] = new Vector3(position.x + position.z, position.y + position.w, 0f);
            vertexArray[vcount + 3] = new Vector3(position.x + position.z, position.y, 0f);
            _verts.count += 4;

            var uvArray = _uvs.array;
            int uvCount = _uvs.count;
            uvArray[uvCount] = new Vector2(atlasBounds.left, atlasBounds.bottom);
            uvArray[uvCount + 1] = new Vector2(atlasBounds.left, atlasBounds.top);
            uvArray[uvCount + 2] = new Vector2(atlasBounds.right, atlasBounds.top);
            uvArray[uvCount + 3] = new Vector2(atlasBounds.right, atlasBounds.bottom);
            _uvs.count += 4;

            var rawUvArray = _rawuv.array;
            int rawUvCount = _rawuv.count;
            rawUvArray[rawUvCount] = _uv0;
            rawUvArray[rawUvCount + 1] = _uv1;
            rawUvArray[rawUvCount + 2] = _uv2;
            rawUvArray[rawUvCount + 3] = _uv3;
            _rawuv.count += 4;

            int triCount = _tris.count;
            var triArray = _tris.array;
            triArray[triCount] = indexOffset;
            triArray[triCount + 1] = indexOffset + 1;
            triArray[triCount + 2] = indexOffset + 2;
            triArray[triCount + 3] = indexOffset;
            triArray[triCount + 4] = indexOffset + 2;
            triArray[triCount + 5] = indexOffset + 3;
            _tris.count += 6;
        }

        /// <summary>
        /// Adds a text-gradient payload to glyphs emitted by a bulk/native run.
        /// Text keeps using its normal material and batch; the gradient is entirely
        /// vertex-driven, so differently painted strings can share that batch.
        /// </summary>
        internal void SetTextGradient(int firstVertex, Vector4 gradient, float encodedGradient)
        {
            if (encodedGradient <= 0f || firstVertex < 0 || firstVertex >= _verts.count)
                return;

            Vector4 gradientXYZ = new Vector4(gradient.x, gradient.y, gradient.z, 0f);
            var radii = _radius.array;
            var extras = _extra.array;

            for (int i = firstVertex; i < _verts.count; ++i)
            {
                radii[i] = gradientXYZ;
                extras[i].z = gradient.w;
                extras[i].w = encodedGradient;
            }
        }

        internal float AddShapedTextRunReserved(
            NowFont.PreparedShapedRun run,
            int start,
            int end,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            bool outlineOnly)
        {
            if (run == null || start >= end)
                return x;

            outline = ClampTextOutlineToRange(outline, pixelRange);
            float encodedPixelRange = outlineOnly ? -Mathf.Abs(pixelRange) : pixelRange;
            ReserveTextGlyphs(end - start);

            if (NowLottieNative.textBlitAvailable)
            {
                int vertexBase = _verts.count;
                int indexBase = _tris.count;

                NowLottieNative.BlitTextRun(
                    run.textGlyphs,
                    start,
                    end,
                    x,
                    y,
                    fontSize,
                    baseline,
                    mask,
                    color,
                    outlineColor,
                    outline,
                    encodedPixelRange,
                    _verts.array,
                    _uvs.array,
                    _rawuv.array,
                    _rect.array,
                    _radius.array,
                    _color.array,
                    _outlineColor.array,
                    _extra.array,
                    _mask.array,
                    vertexBase,
                    _tris.array,
                    indexBase,
                    out float nativePenX,
                    out int emittedVertices,
                    out int emittedIndices,
                    out var bounds);

                _mask.count += emittedVertices;
                _rect.count += emittedVertices;
                _radius.count += emittedVertices;
                _color.count += emittedVertices;
                _outlineColor.count += emittedVertices;
                _extra.count += emittedVertices;
                _verts.count += emittedVertices;
                _uvs.count += emittedVertices;
                _rawuv.count += emittedVertices;
                _tris.count += emittedIndices;

                if (emittedVertices > 0)
                    EncapsulateBounds(bounds.x, bounds.y, bounds.z, bounds.w);

                return nativePenX;
            }

            var glyphs = run.glyphs;

            var maskArray = _mask.array;
            var rectArray = _rect.array;
            var radiusArray = _radius.array;
            var colorArray = _color.array;
            var outlineArray = _outlineColor.array;
            var extraArray = _extra.array;
            var vertexArray = _verts.array;
            var uvArray = _uvs.array;
            var rawUvArray = _rawuv.array;
            var triArray = _tris.array;

            int maskCount = _mask.count;
            int rectCount = _rect.count;
            int radiusCount = _radius.count;
            int colorCount = _color.count;
            int outlineCount = _outlineColor.count;
            int extraCount = _extra.count;
            int vertexCount = _verts.count;
            int uvCount = _uvs.count;
            int rawUvCount = _rawuv.count;
            int triCount = _tris.count;

            Vector4 extra = default;
            extra.x = outline;
            extra.y = encodedPixelRange;

            bool hasBounds = false;
            float boundsMinX = 0f;
            float boundsMinY = 0f;
            float boundsMaxX = 0f;
            float boundsMaxY = 0f;
            float penX = x;

            for (int i = start; i < end; ++i)
            {
                var shaped = glyphs[i];

                if (!shaped.visible)
                {
                    penX += shaped.xAdvance * fontSize;
                    continue;
                }

                var glyph = shaped.glyph;
                var planeBounds = glyph.planeBounds;
                float left = planeBounds.left * fontSize;
                float right = planeBounds.right * fontSize;
                float bottom = planeBounds.bottom * fontSize;
                float top = planeBounds.top * fontSize;

                float width = right - left;
                float height = top - bottom;

                if (width <= 0f || height <= 0f)
                {
                    penX += shaped.xAdvance * fontSize;
                    continue;
                }

                float px = penX + shaped.xOffset * fontSize + left;
                float py = y - shaped.yOffset * fontSize - bottom + baseline - height;
                Vector4 position = new Vector4(px, -(py + height), width, height);

                if (position.x + position.z < mask.x ||
                    position.x >= mask.x + mask.z ||
                    -position.y < mask.y ||
                    -position.y - position.w >= mask.y + mask.w)
                {
                    penX += shaped.xAdvance * fontSize;
                    continue;
                }

                float minX = position.x;
                float minY = position.y;
                float maxX = position.x + position.z;
                float maxY = position.y + position.w;

                if (!hasBounds)
                {
                    hasBounds = true;
                    boundsMinX = minX;
                    boundsMinY = minY;
                    boundsMaxX = maxX;
                    boundsMaxY = maxY;
                }
                else
                {
                    boundsMinX = Mathf.Min(boundsMinX, minX);
                    boundsMinY = Mathf.Min(boundsMinY, minY);
                    boundsMaxX = Mathf.Max(boundsMaxX, maxX);
                    boundsMaxY = Mathf.Max(boundsMaxY, maxY);
                }

                int indexOffset = vertexCount;
                var atlasBounds = glyph.atlasBounds;

                maskArray[maskCount] = mask;
                maskArray[maskCount + 1] = mask;
                maskArray[maskCount + 2] = mask;
                maskArray[maskCount + 3] = mask;
                maskCount += 4;

                rectArray[rectCount] = position;
                rectArray[rectCount + 1] = position;
                rectArray[rectCount + 2] = position;
                rectArray[rectCount + 3] = position;
                rectCount += 4;

                radiusArray[radiusCount] = default;
                radiusArray[radiusCount + 1] = default;
                radiusArray[radiusCount + 2] = default;
                radiusArray[radiusCount + 3] = default;
                radiusCount += 4;

                colorArray[colorCount] = color;
                colorArray[colorCount + 1] = color;
                colorArray[colorCount + 2] = color;
                colorArray[colorCount + 3] = color;
                colorCount += 4;

                outlineArray[outlineCount] = outlineColor;
                outlineArray[outlineCount + 1] = outlineColor;
                outlineArray[outlineCount + 2] = outlineColor;
                outlineArray[outlineCount + 3] = outlineColor;
                outlineCount += 4;

                extraArray[extraCount] = extra;
                extraArray[extraCount + 1] = extra;
                extraArray[extraCount + 2] = extra;
                extraArray[extraCount + 3] = extra;
                extraCount += 4;

                vertexArray[vertexCount] = new Vector3(position.x, position.y, 0f);
                vertexArray[vertexCount + 1] = new Vector3(position.x, position.y + position.w, 0f);
                vertexArray[vertexCount + 2] = new Vector3(position.x + position.z, position.y + position.w, 0f);
                vertexArray[vertexCount + 3] = new Vector3(position.x + position.z, position.y, 0f);
                vertexCount += 4;

                uvArray[uvCount] = new Vector2(atlasBounds.left, atlasBounds.bottom);
                uvArray[uvCount + 1] = new Vector2(atlasBounds.left, atlasBounds.top);
                uvArray[uvCount + 2] = new Vector2(atlasBounds.right, atlasBounds.top);
                uvArray[uvCount + 3] = new Vector2(atlasBounds.right, atlasBounds.bottom);
                uvCount += 4;

                rawUvArray[rawUvCount] = _uv0;
                rawUvArray[rawUvCount + 1] = _uv1;
                rawUvArray[rawUvCount + 2] = _uv2;
                rawUvArray[rawUvCount + 3] = _uv3;
                rawUvCount += 4;

                triArray[triCount] = indexOffset;
                triArray[triCount + 1] = indexOffset + 1;
                triArray[triCount + 2] = indexOffset + 2;
                triArray[triCount + 3] = indexOffset;
                triArray[triCount + 4] = indexOffset + 2;
                triArray[triCount + 5] = indexOffset + 3;
                triCount += 6;

                penX += shaped.xAdvance * fontSize;
            }

            _mask.count = maskCount;
            _rect.count = rectCount;
            _radius.count = radiusCount;
            _color.count = colorCount;
            _outlineColor.count = outlineCount;
            _extra.count = extraCount;
            _verts.count = vertexCount;
            _uvs.count = uvCount;
            _rawuv.count = rawUvCount;
            _tris.count = triCount;

            if (hasBounds)
                EncapsulateBounds(boundsMinX, boundsMinY, boundsMaxX, boundsMaxY);

            return penX;
        }

        internal float AddCodepointTextRunReserved(
            NowFont.PreparedCodepointRun run,
            int start,
            int end,
            float x,
            float y,
            float fontSize,
            float baseline,
            Vector4 mask,
            Vector4 color,
            Vector4 outlineColor,
            float outline,
            float pixelRange,
            bool outlineOnly)
        {
            if (run == null || start >= end)
                return x;

            outline = ClampTextOutlineToRange(outline, pixelRange);
            float encodedPixelRange = outlineOnly ? -Mathf.Abs(pixelRange) : pixelRange;
            ReserveTextGlyphs(end - start);

            if (NowLottieNative.textBlitAvailable)
            {
                int vertexBase = _verts.count;
                int indexBase = _tris.count;

                NowLottieNative.BlitTextRun(
                    run.textGlyphs,
                    start,
                    end,
                    x,
                    y,
                    fontSize,
                    baseline,
                    mask,
                    color,
                    outlineColor,
                    outline,
                    encodedPixelRange,
                    _verts.array,
                    _uvs.array,
                    _rawuv.array,
                    _rect.array,
                    _radius.array,
                    _color.array,
                    _outlineColor.array,
                    _extra.array,
                    _mask.array,
                    vertexBase,
                    _tris.array,
                    indexBase,
                    out float nativePenX,
                    out int emittedVertices,
                    out int emittedIndices,
                    out var bounds);

                _mask.count += emittedVertices;
                _rect.count += emittedVertices;
                _radius.count += emittedVertices;
                _color.count += emittedVertices;
                _outlineColor.count += emittedVertices;
                _extra.count += emittedVertices;
                _verts.count += emittedVertices;
                _uvs.count += emittedVertices;
                _rawuv.count += emittedVertices;
                _tris.count += emittedIndices;

                if (emittedVertices > 0)
                    EncapsulateBounds(bounds.x, bounds.y, bounds.z, bounds.w);

                return nativePenX;
            }

            var glyphs = run.glyphs;

            var maskArray = _mask.array;
            var rectArray = _rect.array;
            var radiusArray = _radius.array;
            var colorArray = _color.array;
            var outlineArray = _outlineColor.array;
            var extraArray = _extra.array;
            var vertexArray = _verts.array;
            var uvArray = _uvs.array;
            var rawUvArray = _rawuv.array;
            var triArray = _tris.array;

            int maskCount = _mask.count;
            int rectCount = _rect.count;
            int radiusCount = _radius.count;
            int colorCount = _color.count;
            int outlineCount = _outlineColor.count;
            int extraCount = _extra.count;
            int vertexCount = _verts.count;
            int uvCount = _uvs.count;
            int rawUvCount = _rawuv.count;
            int triCount = _tris.count;

            Vector4 extra = default;
            extra.x = outline;
            extra.y = encodedPixelRange;

            bool hasBounds = false;
            float boundsMinX = 0f;
            float boundsMinY = 0f;
            float boundsMaxX = 0f;
            float boundsMaxY = 0f;
            float penX = x;

            for (int i = start; i < end; ++i)
            {
                var prepared = glyphs[i];

                if (!prepared.visible)
                {
                    penX += prepared.advance * fontSize;
                    continue;
                }

                var glyph = prepared.glyph;
                var planeBounds = glyph.planeBounds;
                float left = planeBounds.left * fontSize;
                float right = planeBounds.right * fontSize;
                float bottom = planeBounds.bottom * fontSize;
                float top = planeBounds.top * fontSize;

                float width = right - left;
                float height = top - bottom;

                if (width <= 0f || height <= 0f)
                {
                    penX += prepared.advance * fontSize;
                    continue;
                }

                float px = penX + left;
                float py = y - bottom + baseline - height;
                Vector4 position = new Vector4(px, -(py + height), width, height);

                if (position.x + position.z < mask.x ||
                    position.x >= mask.x + mask.z ||
                    -position.y < mask.y ||
                    -position.y - position.w >= mask.y + mask.w)
                {
                    penX += prepared.advance * fontSize;
                    continue;
                }

                float minX = position.x;
                float minY = position.y;
                float maxX = position.x + position.z;
                float maxY = position.y + position.w;

                if (!hasBounds)
                {
                    hasBounds = true;
                    boundsMinX = minX;
                    boundsMinY = minY;
                    boundsMaxX = maxX;
                    boundsMaxY = maxY;
                }
                else
                {
                    boundsMinX = Mathf.Min(boundsMinX, minX);
                    boundsMinY = Mathf.Min(boundsMinY, minY);
                    boundsMaxX = Mathf.Max(boundsMaxX, maxX);
                    boundsMaxY = Mathf.Max(boundsMaxY, maxY);
                }

                int indexOffset = vertexCount;
                var atlasBounds = glyph.atlasBounds;

                maskArray[maskCount] = mask;
                maskArray[maskCount + 1] = mask;
                maskArray[maskCount + 2] = mask;
                maskArray[maskCount + 3] = mask;
                maskCount += 4;

                rectArray[rectCount] = position;
                rectArray[rectCount + 1] = position;
                rectArray[rectCount + 2] = position;
                rectArray[rectCount + 3] = position;
                rectCount += 4;

                radiusArray[radiusCount] = default;
                radiusArray[radiusCount + 1] = default;
                radiusArray[radiusCount + 2] = default;
                radiusArray[radiusCount + 3] = default;
                radiusCount += 4;

                colorArray[colorCount] = color;
                colorArray[colorCount + 1] = color;
                colorArray[colorCount + 2] = color;
                colorArray[colorCount + 3] = color;
                colorCount += 4;

                outlineArray[outlineCount] = outlineColor;
                outlineArray[outlineCount + 1] = outlineColor;
                outlineArray[outlineCount + 2] = outlineColor;
                outlineArray[outlineCount + 3] = outlineColor;
                outlineCount += 4;

                extraArray[extraCount] = extra;
                extraArray[extraCount + 1] = extra;
                extraArray[extraCount + 2] = extra;
                extraArray[extraCount + 3] = extra;
                extraCount += 4;

                vertexArray[vertexCount] = new Vector3(position.x, position.y, 0f);
                vertexArray[vertexCount + 1] = new Vector3(position.x, position.y + position.w, 0f);
                vertexArray[vertexCount + 2] = new Vector3(position.x + position.z, position.y + position.w, 0f);
                vertexArray[vertexCount + 3] = new Vector3(position.x + position.z, position.y, 0f);
                vertexCount += 4;

                uvArray[uvCount] = new Vector2(atlasBounds.left, atlasBounds.bottom);
                uvArray[uvCount + 1] = new Vector2(atlasBounds.left, atlasBounds.top);
                uvArray[uvCount + 2] = new Vector2(atlasBounds.right, atlasBounds.top);
                uvArray[uvCount + 3] = new Vector2(atlasBounds.right, atlasBounds.bottom);
                uvCount += 4;

                rawUvArray[rawUvCount] = _uv0;
                rawUvArray[rawUvCount + 1] = _uv1;
                rawUvArray[rawUvCount + 2] = _uv2;
                rawUvArray[rawUvCount + 3] = _uv3;
                rawUvCount += 4;

                triArray[triCount] = indexOffset;
                triArray[triCount + 1] = indexOffset + 1;
                triArray[triCount + 2] = indexOffset + 2;
                triArray[triCount + 3] = indexOffset;
                triArray[triCount + 4] = indexOffset + 2;
                triArray[triCount + 5] = indexOffset + 3;
                triCount += 6;

                penX += prepared.advance * fontSize;
            }

            _mask.count = maskCount;
            _rect.count = rectCount;
            _radius.count = radiusCount;
            _color.count = colorCount;
            _outlineColor.count = outlineCount;
            _extra.count = extraCount;
            _verts.count = vertexCount;
            _uvs.count = uvCount;
            _rawuv.count = rawUvCount;
            _tris.count = triCount;

            if (hasBounds)
                EncapsulateBounds(boundsMinX, boundsMinY, boundsMaxX, boundsMaxY);

            return penX;
        }

        /// <summary>
        /// Appends arbitrary tessellated triangles (positions in UI space, y down).
        /// The shared rect is the padded bounds of the geometry; UVs are derived from
        /// it so the rectangle shader evaluates to full coverage while per-pixel
        /// masking keeps working exactly like it does for rectangles.
        /// Positions are scaled then offset (buffers may be tessellated at a capped
        /// resolution); the tint multiplies the vertex colors during the copy so
        /// cached tessellations stay tint independent.
        /// </summary>
        public void AddGeometry(NowLottieDrawBuffer buffer, Vector2 positionOffset, float positionScale, Vector4 tint, Vector4 mask)
        {
            int vcount = buffer.positions.count;
            int indexCount = buffer.indices.count;

            if (vcount == 0 || indexCount == 0)
                return;

            const float PADDING = 2f;

            float minX = buffer.boundsMin.x * positionScale + positionOffset.x - PADDING;
            float minY = buffer.boundsMin.y * positionScale + positionOffset.y - PADDING;
            float width = (buffer.boundsMax.x - buffer.boundsMin.x) * positionScale + PADDING * 2f;
            float height = (buffer.boundsMax.y - buffer.boundsMin.y) * positionScale + PADDING * 2f;

            // Same packing as AddRect: mesh space rect with y flipped.
            var rect = new Vector4(minX, -minY - height, width, height);

            var cullProbe = new NowRectVertex { mask = mask, position = rect };

            if (cullProbe.IsOutsideMask(rect))
                return;

            EncapsulateRect(rect);

            _mask.EnsureCapacity(vcount);
            _rect.EnsureCapacity(vcount);
            _radius.EnsureCapacity(vcount);
            _color.EnsureCapacity(vcount);
            _outlineColor.EnsureCapacity(vcount);
            _extra.EnsureCapacity(vcount);
            _verts.EnsureCapacity(vcount);
            _uvs.EnsureCapacity(vcount);
            _rawuv.EnsureCapacity(vcount);
            _tris.EnsureCapacity(indexCount);

            int indexOffset = _verts.count;

            if (NowLottieNative.blitAvailable)
            {
                NowLottieNative.BlitMesh(
                    buffer,
                    positionScale,
                    positionOffset,
                    tint,
                    mask,
                    rect,
                    _verts.array,
                    _uvs.array,
                    _rawuv.array,
                    _rect.array,
                    _radius.array,
                    _color.array,
                    _outlineColor.array,
                    _extra.array,
                    _mask.array,
                    indexOffset,
                    _tris.array,
                    _tris.count,
                    indexOffset);

                _verts.count += vcount;
                _uvs.count += vcount;
                _rawuv.count += vcount;
                _rect.count += vcount;
                _radius.count += vcount;
                _color.count += vcount;
                _outlineColor.count += vcount;
                _extra.count += vcount;
                _mask.count += vcount;
                _tris.count += indexCount;
                return;
            }

            float inverseWidth = 1f / width;
            float inverseHeight = 1f / height;

            for (int i = 0; i < vcount; ++i)
            {
                var position = buffer.positions.array[i];
                position.x = position.x * positionScale + positionOffset.x;
                position.y = position.y * positionScale + positionOffset.y;
                float meshY = -position.y;

                float u = (position.x - rect.x) * inverseWidth;
                float v = (meshY - rect.y) * inverseHeight;

                var color = buffer.colors.array[i];
                color.x *= tint.x;
                color.y *= tint.y;
                color.z *= tint.z;
                color.w *= tint.w;

                _verts.array[_verts.count++] = new Vector3(position.x, meshY, 0f);
                _uvs.array[_uvs.count++] = new Vector2(u, v);
                _rawuv.array[_rawuv.count++] = new Vector4(u, v, 0f, 0f);
                _rect.array[_rect.count++] = rect;
                _radius.array[_radius.count++] = default;
                _color.array[_color.count++] = color;
                _outlineColor.array[_outlineColor.count++] = default;
                _extra.array[_extra.count++] = default;
                _mask.array[_mask.count++] = mask;
            }

            for (int i = 0; i < indexCount; ++i)
                _tris.array[_tris.count++] = buffer.indices.array[i] + indexOffset;
        }

        public void ClearVertices()
        {
            _radius.Clear();
            _rect.Clear();
            _verts.Clear();
            _uvs.Clear();
            _tris.Clear();
            _color.Clear();
            _outlineColor.Clear();
            _extra.Clear();
            _mask.Clear();
            _rawuv.Clear();
            _renderVertices.Clear();
            ClearBounds();
            maskState = default;
        }

        public void AppendVertices(
            ref StaticList<Vector3> vertices,
            ref StaticList<Vector2> uvs,
            ref StaticList<Vector4> rects,
            ref StaticList<Vector4> radii,
            ref StaticList<Vector4> colors,
            ref StaticList<Vector4> outlineColors,
            ref StaticList<Vector4> extras,
            ref StaticList<Vector4> masks,
            ref StaticList<Vector4> rawUvs,
            Vector2 positionOffset)
        {
            int count = _verts.count;

            vertices.EnsureCapacity(count);
            uvs.EnsureCapacity(count);
            rects.EnsureCapacity(count);
            radii.EnsureCapacity(count);
            colors.EnsureCapacity(count);
            outlineColors.EnsureCapacity(count);
            extras.EnsureCapacity(count);
            masks.EnsureCapacity(count);
            rawUvs.EnsureCapacity(count);

            var sourceVertices = _verts.array;
            var destinationVertices = vertices.array;
            int destinationBase = vertices.count;

            if (positionOffset.x == 0f && positionOffset.y == 0f)
            {
                System.Array.Copy(sourceVertices, 0, destinationVertices, destinationBase, count);
            }
            else
            {
                for (int i = 0; i < count; ++i)
                {
                    var vertex = sourceVertices[i];
                    vertex.x += positionOffset.x;
                    vertex.y += positionOffset.y;
                    destinationVertices[destinationBase + i] = vertex;
                }
            }

            vertices.count += count;

            System.Array.Copy(_uvs.array, 0, uvs.array, uvs.count, count);
            uvs.count += count;
            System.Array.Copy(_rect.array, 0, rects.array, rects.count, count);
            rects.count += count;
            System.Array.Copy(_radius.array, 0, radii.array, radii.count, count);
            radii.count += count;
            System.Array.Copy(_color.array, 0, colors.array, colors.count, count);
            colors.count += count;
            System.Array.Copy(_outlineColor.array, 0, outlineColors.array, outlineColors.count, count);
            outlineColors.count += count;
            System.Array.Copy(_extra.array, 0, extras.array, extras.count, count);
            extras.count += count;
            System.Array.Copy(_mask.array, 0, masks.array, masks.count, count);
            masks.count += count;
            System.Array.Copy(_rawuv.array, 0, rawUvs.array, rawUvs.count, count);
            rawUvs.count += count;
        }

        public bool TryAppendNativeRenderVertices(ref StaticList<NowRenderVertex> destination, Vector2 positionOffset)
        {
            if (!NowLottieNative.packRenderAvailable)
                return false;

            int count = _verts.count;
            destination.EnsureCapacity(count);
            int destinationBase = destination.count;

            NowLottieNative.PackRender(
                _verts.array,
                _uvs.array,
                _radius.array,
                _rawuv.array,
                _color.array,
                _rect.array,
                _mask.array,
                _extra.array,
                _outlineColor.array,
                count,
                positionOffset,
                destination.array,
                destinationBase);

            destination.count += count;
            return true;
        }

        /// <summary>
        /// Appends this mesh's vertices in the interleaved canvas layout (one
        /// SetVertexBufferData upload instead of eight channel setters).
        /// </summary>
        public void AppendCanvasVertices(
            ref StaticList<NowCanvasVertex> destination,
            Vector2 positionOffset,
            bool canvasVertexColorAlwaysGammaSpace = false)
        {
            bool isText = kind == NowMeshKind.Text;
            bool isRectangleLike =
                kind == NowMeshKind.Rectangle ||
                kind == NowMeshKind.TexturedRectangle ||
                kind == NowMeshKind.CustomRectangle ||
                kind == NowMeshKind.Gradient ||
                kind == NowMeshKind.Ripple;
            int count = _verts.count;

            destination.EnsureCapacity(count);
            int destinationBase = destination.count;

            if (NowLottieNative.packCanvasAvailable)
            {
                NowLottieNative.PackCanvas(
                    _verts.array,
                    _uvs.array,
                    _radius.array,
                    _rawuv.array,
                    _color.array,
                    _rect.array,
                    _mask.array,
                    _extra.array,
                    _outlineColor.array,
                    count,
                    isText,
                    positionOffset,
                    destination.array,
                    destinationBase);

                if (isRectangleLike)
                    PatchRectangleCanvasVertices(destination.array, destinationBase, count);

                if (isText)
                    PatchTextCanvasColors(
                        destination.array,
                        destinationBase,
                        count,
                        canvasVertexColorAlwaysGammaSpace);
                else if (kind == NowMeshKind.Gradient)
                    PatchGradientCanvasColors(
                        destination.array,
                        destinationBase,
                        count,
                        canvasVertexColorAlwaysGammaSpace);

                destination.count += count;
                return;
            }

            var output = destination.array;

            for (int i = 0; i < count; ++i)
            {
                var radius = _radius.array[i];
                var uv = _uvs.array[i];
                var color = _color.array[i];

                NowCanvasVertex vertex = default;
                vertex.position = _verts.array[i];
                vertex.position.x += positionOffset.x;
                vertex.position.y += positionOffset.y;
                vertex.normal = new Vector3(radius.x, radius.y, radius.z);
                vertex.tangent = _outlineColor.array[i];
                vertex.color = new Color(color.x, color.y, color.z, color.w);

                if (isText)
                {
                    var rawUv = _rawuv.array[i];
                    vertex.uv0 = new Vector4(uv.x, uv.y, rawUv.x, rawUv.y);
                    vertex.uv3 = _extra.array[i];
                }
                else if (isRectangleLike)
                {
                    var rawUv = _rawuv.array[i];
                    var extra = _extra.array[i];
                    vertex.uv0 = new Vector4(uv.x, uv.y, radius.w, rawUv.x);
                    vertex.uv3 = new Vector4(extra.x, extra.y, rawUv.y, extra.w);
                }
                else
                {
                    vertex.uv0 = new Vector4(uv.x, uv.y, radius.w, 0f);
                    vertex.uv3 = _extra.array[i];
                }

                vertex.uv1 = _rect.array[i];
                vertex.uv2 = _mask.array[i];

                output[destinationBase + i] = vertex;
            }

            if (isText)
                PatchTextCanvasColors(
                    output,
                    destinationBase,
                    count,
                    canvasVertexColorAlwaysGammaSpace);
            else if (kind == NowMeshKind.Gradient)
                PatchGradientCanvasColors(
                    output,
                    destinationBase,
                    count,
                    canvasVertexColorAlwaysGammaSpace);

            destination.count += count;
        }

        /// <summary>
        /// Canvas only performs its automatic gamma-to-linear conversion on the
        /// COLOR channel, and can be configured to leave even that channel in gamma
        /// space. Text outlines ride TANGENT, so normalize both authored UI colors
        /// to the shader's working space here when Canvas will not do it for us.
        /// </summary>
        static void PatchTextCanvasColors(
            NowCanvasVertex[] output,
            int destinationBase,
            int count,
            bool canvasVertexColorAlwaysGammaSpace)
        {
            ColorSpace colorSpace = QualitySettings.activeColorSpace;

            if (colorSpace != ColorSpace.Linear)
                return;

            for (int i = 0; i < count; ++i)
            {
                int index = destinationBase + i;

                if (canvasVertexColorAlwaysGammaSpace)
                    output[index].color = TextCanvasColorToWorkingSpace(output[index].color, colorSpace);

                output[index].tangent = TextCanvasColorToWorkingSpace(output[index].tangent, colorSpace);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Color TextCanvasColorToWorkingSpace(Color color, ColorSpace colorSpace)
        {
            if (colorSpace != ColorSpace.Linear)
                return color;

            return new Color(
                Mathf.GammaToLinearSpace(color.r),
                Mathf.GammaToLinearSpace(color.g),
                Mathf.GammaToLinearSpace(color.b),
                color.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector4 TextCanvasColorToWorkingSpace(Vector4 color, ColorSpace colorSpace)
        {
            if (colorSpace != ColorSpace.Linear)
                return color;

            return new Vector4(
                Mathf.GammaToLinearSpace(color.x),
                Mathf.GammaToLinearSpace(color.y),
                Mathf.GammaToLinearSpace(color.z),
                color.w);
        }

        /// <summary>
        /// Gradient geometry parameters ride the canvas COLOR channel, which UGUI
        /// stores as a Color32: values clamp to 0..1, quantize to 8 bits, and (unless
        /// the canvas forces gamma vertex colors) are converted gamma to linear on
        /// the way to the shader. Raw parameters do not survive that — every
        /// direction with a negative component collapses to zero and repetition
        /// counts above one clamp. Fold them into 0..1 here and, when the Canvas
        /// performs that conversion, pre-apply its inverse so
        /// <c>UIGradientUGUI</c> receives the authored values back. The non-canvas
        /// render layout keeps full float parameters in TEXCOORD3 and needs none
        /// of this.
        /// </summary>
        static void PatchGradientCanvasColors(
            NowCanvasVertex[] output,
            int destinationBase,
            int count,
            bool canvasVertexColorAlwaysGammaSpace)
        {
            bool compensateCanvasGamma = ShouldCompensateGradientCanvasGamma(
                QualitySettings.activeColorSpace,
                canvasVertexColorAlwaysGammaSpace);

            for (int i = 0; i < count; ++i)
            {
                int index = destinationBase + i;
                var parameters = output[index].color;

                float x = EncodeGradientParameter(parameters.r);
                float y = EncodeGradientParameter(parameters.g);
                float z = EncodeGradientParameter(parameters.b);
                float w = EncodeGradientParameter(parameters.a);

                // The canvas converts rgb only, so alpha stays as encoded.
                if (compensateCanvasGamma)
                {
                    x = Mathf.LinearToGammaSpace(x);
                    y = Mathf.LinearToGammaSpace(y);
                    z = Mathf.LinearToGammaSpace(z);
                }

                output[index].color = new Color(x, y, z, w);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ShouldCompensateGradientCanvasGamma(
            ColorSpace activeColorSpace,
            bool canvasVertexColorAlwaysGammaSpace)
        {
            return activeColorSpace == ColorSpace.Linear && !canvasVertexColorAlwaysGammaSpace;
        }

        /// <summary>
        /// Folds an unbounded gradient parameter into 0..1 with a signed reciprocal
        /// mapping so sign survives a normalized vertex channel. Zero maps to the
        /// midpoint. <c>UIGradientUGUI</c> applies the exact inverse.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float EncodeGradientParameter(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;

            return 0.5f + 0.5f * (value / (1f + Mathf.Abs(value)));
        }

        /// <summary>
        /// Rectangle-family batches differ from the native pack's default layout in
        /// exactly two floats (uv0.w carries rawUv.x, uv3.z carries rawUv.y), so the
        /// native output is fixed up in place instead of taking the full managed swizzle.
        /// </summary>
        void PatchRectangleCanvasVertices(NowCanvasVertex[] output, int destinationBase, int count)
        {
            var rawUvs = _rawuv.array;

            for (int i = 0; i < count; ++i)
            {
                var rawUv = rawUvs[i];
                output[destinationBase + i].uv0.w = rawUv.x;
                output[destinationBase + i].uv3.z = rawUv.y;
            }
        }

        public void AppendTriangles(ref StaticList<int> triangles, int vertexOffset)
        {
            int count = _tris.count;
            triangles.EnsureCapacity(count);

            var source = _tris.array;
            var destination = triangles.array;
            int destinationBase = triangles.count;

            for (int i = 0; i < count; ++i)
                destination[destinationBase + i] = source[i] + vertexOffset;

            triangles.count += count;
        }

        /// <summary>
        /// Appends directly to a 16-bit upload buffer. The capture caller selects
        /// this path only when the combined mesh has at most 65535 vertices.
        /// </summary>
        internal void AppendTriangles(ref StaticList<ushort> triangles, int vertexOffset)
        {
            int count = _tris.count;
            triangles.EnsureCapacity(count);

            var source = _tris.array;
            var destination = triangles.array;
            int destinationBase = triangles.count;

            for (int i = 0; i < count; ++i)
                destination[destinationBase + i] = (ushort)(source[i] + vertexOffset);

            triangles.count += count;
        }

        public NowRect GetBounds(Vector2 positionOffset)
        {
            if (!_hasBounds)
                return default;

            float minX = _boundsMinX + positionOffset.x;
            float minY = _boundsMinY + positionOffset.y;
            float maxX = _boundsMaxX + positionOffset.x;
            float maxY = _boundsMaxY + positionOffset.y;

            return new NowRect(minX, -maxY, Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
        }

        internal static Bounds ToUnityBounds(NowRect rect)
        {
            if (rect.isEmpty)
                return new Bounds(Vector3.zero, Vector3.zero);

            float yMax = -rect.y;
            float yMin = yMax - rect.height;
            return new Bounds(
                new Vector3(rect.x + rect.width * 0.5f, (yMin + yMax) * 0.5f, 0f),
                new Vector3(rect.width, rect.height, 0f));
        }

        public void UploadMesh()
        {
            if (!unityMesh)
            {
                unityMesh = new Mesh
                {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                unityMesh.MarkDynamic();
            }

            unityMesh.Clear(true);

            _renderVertices.Clear();
            if (TryAppendNativeRenderVertices(ref _renderVertices, Vector2.zero))
            {
                unityMesh.SetVertexBufferParams(_renderVertices.count, RenderVertexLayout);
                unityMesh.SetVertexBufferData(
                    _renderVertices.array,
                    0,
                    0,
                    _renderVertices.count,
                    0,
                    MeshUpdateFlags.DontRecalculateBounds);
            }
            else
            {
                unityMesh.SetVertices(_verts.array, 0, _verts.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(0, _uvs.array, 0, _uvs.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(1, _rect.array, 0, _rect.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(2, _radius.array, 0, _radius.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(3, _color.array, 0, _color.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(4, _outlineColor.array, 0, _outlineColor.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(5, _extra.array, 0, _extra.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(6, _mask.array, 0, _mask.count, VertexStreamUploadFlags);
                unityMesh.SetUVs(7, _rawuv.array, 0, _rawuv.count, VertexStreamUploadFlags);
            }

            var indexFormat = _verts.count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            unityMesh.SetIndexBufferParams(_tris.count, indexFormat);

            if (indexFormat == IndexFormat.UInt16)
            {
                _indexScratch.Clear();
                _indexScratch.EnsureCapacity(_tris.count);

                var indexSource = _tris.array;
                var indexDestination = _indexScratch.array;

                for (int i = 0; i < _tris.count; ++i)
                    indexDestination[i] = (ushort)indexSource[i];

                _indexScratch.count = _tris.count;
                unityMesh.SetIndexBufferData(_indexScratch.array, 0, 0, _tris.count, IndexUploadFlags);
            }
            else
            {
                unityMesh.SetIndexBufferData(_tris.array, 0, 0, _tris.count, IndexUploadFlags);
            }

            unityMesh.subMeshCount = 1;

            var descriptor = new SubMeshDescriptor(0, _tris.count)
            {
                firstVertex = 0,
                vertexCount = _verts.count
            };

            unityMesh.SetSubMesh(0, descriptor, IndexUploadFlags);
            unityMesh.bounds = ToUnityBounds(GetBounds(Vector2.zero));
        }
    }
}
