using UnityEngine;

namespace NowUIInternal
{
    public enum NowMeshKind
    {
        Rectangle,
        Text
    }

    internal struct NowUIMeshBatch
    {
        public readonly Material material;

        public readonly NowMeshKind kind;

        public NowUIMeshBatch(Material material, NowMeshKind kind)
        {
            this.material = material;
            this.kind = kind;
        }
    }

    internal enum NowUIMeshLayout
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

    public struct NowRectVertex
    {
        public Vector4 mask;

        public Vector4 position;

        public Vector4 radius;

        public Vector4 color;

        public Vector4 outlineColor;

        public Vector4 uvwh;

        public bool IsOutsideMask(Vector4 rect)
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

        public Material material;

        public NowMeshKind kind;

        public NowMesh(Material mat, NowMeshKind kind)
        {
            material = mat;
            this.kind = kind;

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
        }

        public void SetMaterial(Material material, NowMeshKind kind)
        {
            this.material = material;
            this.kind = kind;
            ClearVertices();
        }

        static readonly Vector2 _uv0 = new Vector2(0, 0);

        static readonly Vector2 _uv1 = new Vector2(0, 1);

        static readonly Vector2 _uv2 = new Vector2(1, 1);

        static readonly Vector2 _uv3 = new Vector2(1, 0);

        Vector3 _a, _b, _c, _d;

        void EnsureRectCapacity()
        {
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

        public void AddRect(NowRectVertex vertexData, float extraX, float extraY)
        {
            if (vertexData.IsOutsideMask(vertexData.position)) return;

            EnsureRectCapacity();

            int indexOffset = _verts.count;

            Vector4 extra = default;
            extra.x = extraX;
            extra.y = extraY;

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

            var position = vertexData.position;

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

            _a.x = position.x;
            _a.y = position.y;
            _a.z = 0;

            _b.x = _a.x;
            _b.y = _a.y + position.w;
            _b.z = 0;

            _c.x = _a.x + position.z;
            _c.y = _b.y;
            _c.z = 0;

            _d.x = _c.x;
            _d.y = _a.y;
            _d.z = 0;

            var varr = _verts.array;
            var vcount = _verts.count;

            varr[vcount++] = _a;
            varr[vcount++] = _b;
            varr[vcount++] = _c;
            varr[vcount++] = _d;

            _verts.count = vcount;

            var ruvs = _uvs.array;
            var ruvsCount = _uvs.count;

            var uv0 = _uv0;
            var uv1 = _uv1;
            var uv2 = _uv2;
            var uv3 = _uv3;

            var uvwh = vertexData.uvwh;

            uv0.x = uvwh.x + uv0.x * uvwh.z;
            uv0.y = uvwh.y + uv0.y * uvwh.w;

            uv1.x = uvwh.x + uv1.x * uvwh.z;
            uv1.y = uvwh.y + uv1.y * uvwh.w;

            uv2.x = uvwh.x + uv2.x * uvwh.z;
            uv2.y = uvwh.y + uv2.y * uvwh.w;

            uv3.x = uvwh.x + uv3.x * uvwh.z;
            uv3.y = uvwh.y + uv3.y * uvwh.w;

            ruvs[ruvsCount] = uv0;
            ruvs[ruvsCount + 1] = uv1;
            ruvs[ruvsCount + 2] = uv2;
            ruvs[ruvsCount + 3] = uv3;

            var rawuvs = _rawuv.array;
            var rawuvsCount = _rawuv.count;

            rawuvs[rawuvsCount] = _uv0;
            rawuvs[rawuvsCount + 1] = _uv1;
            rawuvs[rawuvsCount + 2] = _uv2;
            rawuvs[rawuvsCount + 3] = _uv3;

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
            int vertexCount = buffer.positions.count;
            int indexCount = buffer.indices.count;

            if (vertexCount == 0 || indexCount == 0)
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

                _verts.count += vertexCount;
                _uvs.count += vertexCount;
                _rawuv.count += vertexCount;
                _rect.count += vertexCount;
                _radius.count += vertexCount;
                _color.count += vertexCount;
                _outlineColor.count += vertexCount;
                _extra.count += vertexCount;
                _mask.count += vertexCount;
                _tris.count += indexCount;
                return;
            }

            float inverseWidth = 1f / width;
            float inverseHeight = 1f / height;

            for (int i = 0; i < vertexCount; ++i)
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

            for (int i = 0; i < count; ++i)
            {
                var vertex = sourceVertices[i];
                vertex.x += positionOffset.x;
                vertex.y += positionOffset.y;
                destinationVertices[destinationBase + i] = vertex;
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

        /// <summary>
        /// Appends this mesh's vertices in the interleaved canvas layout (one
        /// SetVertexBufferData upload instead of eight channel setters).
        /// </summary>
        public void AppendCanvasVertices(ref StaticList<NowCanvasVertex> destination, Vector2 positionOffset)
        {
            bool isText = kind == NowMeshKind.Text;
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

                destination.count += count;
                return;
            }

            var output = destination.array;

            for (int i = 0; i < count; ++i)
            {
                var radius = _radius.array[i];
                var uv = _uvs.array[i];
                var color = _color.array[i];

                NowCanvasVertex vertex;
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
                }
                else
                {
                    vertex.uv0 = new Vector4(uv.x, uv.y, radius.w, 0f);
                }

                vertex.uv1 = _rect.array[i];
                vertex.uv2 = _mask.array[i];
                vertex.uv3 = _extra.array[i];

                output[destinationBase + i] = vertex;
            }

            destination.count += count;
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

            unityMesh.SetVertices(_verts.array, 0, _verts.count);
            unityMesh.SetUVs(0, _uvs.array, 0, _uvs.count);
            unityMesh.SetUVs(1, _rect.array, 0, _rect.count);
            unityMesh.SetUVs(2, _radius.array, 0, _radius.count);
            unityMesh.SetUVs(3, _color.array, 0, _color.count);
            unityMesh.SetUVs(4, _outlineColor.array, 0, _outlineColor.count);
            unityMesh.SetUVs(5, _extra.array, 0, _extra.count);
            unityMesh.SetUVs(6, _mask.array, 0, _mask.count);
            unityMesh.SetUVs(7, _rawuv.array, 0, _rawuv.count);
            unityMesh.SetTriangles(_tris.array, 0, _tris.count, 0);
        }
    }
}
